using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

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
    public Button hintPanelButton;
    
    public GameObject descPanel;
    public Vector2 descOffset;
    public TextMeshProUGUI descText;
    
    public GameObject tutorialPanel;
    public TextMeshProUGUI tutorialText;
    public Button tutorialPanelButton;
    
    public GameObject bossDescPanel; // boss描述面板，只在boss关卡显示
    public TextMeshProUGUI bossDescText; // boss描述文本
    
    public GameObject bossIcon; // boss图标，只在boss关卡显示
    public GameObject bossIconInteractableObject; // bossIcon的可交互提示对象
    public GameObject bellButtonInteractableObject; // bellButton的可交互提示对象
    
    public DeckMenu deckMenu; // Deck菜单组件
    
    public UpgradeDisplaySlot[] upgradeSlots = new UpgradeDisplaySlot[5];
    
    public GameObject floatingTextPrefab; // 漂浮字prefab
    
    public Image bkImage; // 背景图片
    
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
        
        
        if (descPanel != null)
        {
            descPanel.SetActive(false);
        }
        
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
        
        if (bossDescPanel != null)
        {
            bossDescPanel.SetActive(false);
        }
        
        if (bossIcon != null)
        {
            bossIcon.SetActive(false);
        }
        
        if (hintPanelButton != null)
        {
            hintPanelButton.onClick.AddListener(OnHintPanelClicked);
        }
        
        if (tutorialPanelButton != null)
        {
            tutorialPanelButton.onClick.AddListener(OnTutorialPanelClicked);
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
        
        // if (retryBossButton != null)
        // {
        //     retryBossButton.onClick.AddListener(OnRetryBossButtonClicked);
        //     retryBossButton.gameObject.SetActive(false); // 初始隐藏
        // }
    }
    
    private void OnRetryBossButtonClicked()
    {
        GameManager.Instance?.RetryBoss();
    }
    
    public void ShowBellButton()
    {
        if (bellButton != null)
        {
            bellButton.gameObject.SetActive(true);
            // 显示时设为可点击
            SetBellButtonInteractable(true);
        }
    }
    
    public void HideBellButton()
    {
        if (bellButton != null)
        {
            bellButton.gameObject.SetActive(false);
        }
        
        // 隐藏interactableObject
        if (bellButtonInteractableObject != null)
        {
            bellButtonInteractableObject.SetActive(false);
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
            flashlightsText.text = $"{data.flashlights}";
        if (levelText != null)
            levelText.text = $"LV {data.currentLevel}";
        
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
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        GameManager.Instance?.UseFlashlight();
    }
    
    public void OnBellButtonClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
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
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        HideHint();
    }
    
    public Button retryBossButton; // retry boss按钮
    
    public void ShowGameOver(bool showRetryBoss = false)
    {
        LoseMenu.Instance.ShowLoseMenu(showRetryBoss);
        
        
    }
    
    public void HideGameOver()
    {
        LoseMenu.Instance.HideLoseMenu();
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
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        if (deckMenu != null)
        {
            deckMenu.ToggleMenu();
        }
    }
    
    public void ShowTutorial(string desc)
    {
        if (tutorialText != null)
        {
            // 将"\n"替换为真正的换行符
            string processedDesc = desc.Replace("\\n", "\n");
            tutorialText.text = processedDesc;
        }
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
            
            // 播放放大缩小动画
            RectTransform panelRect = tutorialPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                // 停止之前的动画
                panelRect.DOKill();
                
                // 重置缩放
                panelRect.localScale = Vector3.zero;
                
                // 创建动画序列：放大 -> 稍微缩小 -> 回到正常大小
                Sequence sequence = DOTween.Sequence();
                sequence.Append(panelRect.DOScale(Vector3.one * 1.1f, 0.2f).SetEase(Ease.OutQuad));
                sequence.Append(panelRect.DOScale(Vector3.one, 0.15f).SetEase(Ease.InQuad));
            }
        }
    }
    
    public void HideTutorial()
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
    }
    
    private void OnTutorialPanelClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        HideTutorial();
    }
    
    public void ShowBossDesc(string bossName, string bossDesc)
    {
        if (bossDescPanel != null)
        {
            bossDescPanel.SetActive(true);
        }
        
        if (bossDescText != null)
        {
            string text = $"{bossName}\n\n{bossDesc}";
            bossDescText.text = text;
        }
    }
    
    public void HideBossDesc()
    {
        if (bossDescPanel != null)
        {
            bossDescPanel.SetActive(false);
        }
    }
    
    public void ShowBossIcon(CardType bossCardType)
    {
        if (bossIcon != null)
        {
            bossIcon.SetActive(true);
            
            // 设置bossIcon的CardType
            BossIcon bossIconComponent = bossIcon.GetComponent<BossIcon>();
            if (bossIconComponent == null)
            {
                bossIconComponent = bossIcon.AddComponent<BossIcon>();
            }
            bossIconComponent.Setup(bossCardType);
            
            // 初始状态设为不可点击
            SetBossIconInteractable(false);
        }
    }
    
    public void HideBossIcon()
    {
        if (bossIcon != null)
        {
            bossIcon.SetActive(false);
        }
        
        // 隐藏interactableObject
        if (bossIconInteractableObject != null)
        {
            bossIconInteractableObject.SetActive(false);
        }
    }
    
    public void SetBossIconInteractable(bool interactable)
    {
        if (bossIcon != null)
        {
            BossIcon bossIconComponent = bossIcon.GetComponent<BossIcon>();
            if (bossIconComponent != null)
            {
                bossIconComponent.SetInteractable(interactable);
            }
        }
        
        // 设置interactableObject的active状态
        if (bossIconInteractableObject != null)
        {
            bossIconInteractableObject.SetActive(interactable);
        }
    }
    
    public void SetBellButtonInteractable(bool interactable)
    {
        if (bellButton != null)
        {
            bellButton.interactable = interactable;

        }
        
        // 设置interactableObject的active状态
        if (bellButtonInteractableObject != null)
        {
            bellButtonInteractableObject.SetActive(interactable);
        }
    }
    
    // 触发指定upgrade的放大缩小动画
    public void TriggerUpgradeAnimation(string upgradeIdentifier)
    {
        if (upgradeSlots == null) return;
        
        // 遍历所有upgrade slots，找到显示该upgrade的slot并播放动画
        foreach (UpgradeDisplaySlot slot in upgradeSlots)
        {
            if (slot != null && slot.IsDisplayingUpgrade(upgradeIdentifier))
            {
                slot.PlayPulseAnimation();
                break; // 只触发第一个找到的slot
            }
        }
    }

    public Transform allAttributeTransform;
    // 显示漂浮字效果
    public void ShowFloatingText(string resourceType, int changeAmount, RectTransform targetRect)
    {
        if (floatingTextPrefab == null || targetRect == null) return;
        
        // 获取Canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        
        // 实例化prefab
        GameObject floatingObj = Instantiate(floatingTextPrefab, allAttributeTransform);
        FloatingText floatingText = floatingObj.GetComponentInChildren<FloatingText>();
        if (floatingText == null) return;
        
        // 确定文本内容和颜色
        string text = "";
        Color color = Color.white;
        
        if (changeAmount > 0)
        {
            text = $"+{changeAmount}";
            color = Color.green;
        }
        else if (changeAmount < 0)
        {
            text = $"{changeAmount}";
            color = Color.red;
        }
        else
        {
            return; // 没有变化，不显示
        }
        
        // 获取目标位置（右侧）
        Vector2 targetPosition = targetRect.transform.position;
        Vector2 startPosition = new Vector2(targetPosition.x + targetRect.rect.width * 0.5f + 50f, targetPosition.y);
        
        // 显示漂浮字
        floatingText.Show(text, color, targetPosition);
    }
    
    // 更新背景图片
    public void UpdateBackgroundImage()
    {
        if (bkImage == null) return;
        
        // 获取当前关卡信息
        LevelInfo levelInfo = LevelManager.Instance?.GetCurrentLevelInfo();
        if (levelInfo == null) return;
        
        // 如果map有值，则加载对应的背景图片
        if (!string.IsNullOrEmpty(levelInfo.map))
        {
            string resourcePath = $"bk/{levelInfo.map}";
            Sprite backgroundSprite = Resources.Load<Sprite>(resourcePath);
            
            if (backgroundSprite != null)
            {
                bkImage.sprite = backgroundSprite;
            }
            else
            {
                Debug.LogWarning($"Background image not found at path: {resourcePath}");
            }
        }
    }
}


