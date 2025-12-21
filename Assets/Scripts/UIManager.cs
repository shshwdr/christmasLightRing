using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    
    public TextMeshProUGUI coinsText;
    public TextMeshProUGUI giftsText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI flashlightsText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI hintText;
    public TextMeshProUGUI enemyCountText;
    
    public Button flashlightButton;
    public Button bellButton;
    public Button deckButton;
    
    public GameObject hintPanel;
    public GameObject gameOverPanel;
    public Button hintPanelButton;
    
    public GameObject descPanel;
    public Vector2 descOffset;
    public TextMeshProUGUI descText;
    
    public DeckMenu deckMenu; // Deck菜单组件
    
    public UpgradeDisplaySlot[] upgradeSlots = new UpgradeDisplaySlot[5];
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        if (hintPanel != null)
        {
            hintPanel.SetActive(false);
        }
        
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        
        if (descPanel != null)
        {
            descPanel.SetActive(false);
        }
        
        if (hintPanelButton != null)
        {
            hintPanelButton.onClick.AddListener(OnHintPanelClicked);
        }
        
        if (flashlightButton != null)
        {
            flashlightButton.onClick.AddListener(OnFlashlightButtonClicked);
        }
        
        if (bellButton != null)
        {
            bellButton.onClick.AddListener(OnBellButtonClicked);
            bellButton.gameObject.SetActive(false); // 初始隐藏
        }
        
        if (deckButton != null)
        {
            deckButton.onClick.AddListener(OnDeckButtonClicked);
        }
    }
    
    public void ShowBellButton()
    {
        if (bellButton != null)
        {
            bellButton.gameObject.SetActive(true);
        }
    }
    
    public void HideBellButton()
    {
        if (bellButton != null)
        {
            bellButton.gameObject.SetActive(false);
        }
    }
    
    public void UpdateUI()
    {
        if (GameManager.Instance == null) return;
        
        GameData data = GameManager.Instance.gameData;
        
        if (coinsText != null)
            coinsText.text = $"{data.coins}";
        if (giftsText != null)
            giftsText.text = $"{data.gifts}";
        if (healthText != null)
            healthText.text = $"{data.health}";
        if (flashlightsText != null)
            flashlightsText.text = $"FlashLight: {data.flashlights}";
        if (levelText != null)
            levelText.text = $"{data.currentLevel}";
        
        UpdateFlashlightButton();
        UpdateEnemyCount();
    }
    
    public void UpdateEnemyCount()
    {
        if (enemyCountText == null || GameManager.Instance == null || GameManager.Instance.boardManager == null) return;
        
        int revealedEnemies = GameManager.Instance.boardManager.GetRevealedEnemyCount();
        int totalEnemies = GameManager.Instance.boardManager.GetTotalEnemyCount();
        
        enemyCountText.text = $"{revealedEnemies}/{totalEnemies}";
    }
    
    public void UpdateFlashlightButton()
    {
        if (flashlightButton != null && GameManager.Instance != null)
        {
            bool canUse = GameManager.Instance.gameData.flashlights > 0 && 
                         !GameManager.Instance.IsUsingFlashlight();
            flashlightButton.interactable = canUse;
        }
    }
    
    public void OnFlashlightButtonClicked()
    {
        GameManager.Instance?.UseFlashlight();
    }
    
    public void OnBellButtonClicked()
    {
        GameManager.Instance?.EndTurn();
    }
    
    public void ShowHint(string hint)
    {
        if (hintText != null)
        {
            hintText.text = hint;
        }
        if (hintPanel != null)
        {
            // 如果已经显示，则隐藏；否则显示
            bool isActive = hintPanel.activeSelf;
            hintPanel.SetActive(!isActive);
        }
    }
    
    public void HideHint()
    {
        if (hintPanel != null)
        {
            hintPanel.SetActive(false);
        }
    }
    
    private void OnHintPanelClicked()
    {
        HideHint();
    }
    
    public void ShowGameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }
    
    public void ShowDesc(CardType cardType)
    {
        if (CardInfoManager.Instance == null) return;
        
        CardInfo cardInfo = CardInfoManager.Instance.GetCardInfo(cardType);
        if (cardInfo != null)
        {
            if (descText != null)
            {
                string text = $"{cardInfo.name}\n{cardInfo.desc}";
                descText.text = text;
            }
            if (descPanel != null)
            {
                descPanel.SetActive(true);
                // 更新位置到鼠标位置
                UpdateDescPosition();
            }
        }
    }
    
    private void UpdateDescPosition()
    {
        if (descPanel != null)
        {
            RectTransform rect = descPanel.GetComponent<RectTransform>();
            if (rect != null)
            {
                Vector2 mousePos = Input.mousePosition;
                rect.position = mousePos + descOffset;
            }
        }
    }
    
    
    public void HideDesc()
    {
        if (descPanel != null)
        {
            descPanel.SetActive(false);
        }
    }
    
    public void UpdateUpgradeDisplay()
    {
        if (GameManager.Instance == null) return;
        
        List<string> ownedUpgrades = GameManager.Instance.gameData.ownedUpgrades;
        
        for (int i = 0; i < 5; i++)
        {
            if (upgradeSlots[i] != null)
            {
                if (i < ownedUpgrades.Count)
                {
                    upgradeSlots[i].Setup(ownedUpgrades[i]);
                }
                else
                {
                    upgradeSlots[i].ClearSlot();
                }
            }
        }
    }
    
    public void OnDeckButtonClicked()
    {
        if (deckMenu != null)
        {
            deckMenu.ToggleMenu();
        }
    }
}


