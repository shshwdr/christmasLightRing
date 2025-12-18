using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI coinsText;
    public TextMeshProUGUI giftsText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI flashlightsText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI hintText;
    
    public Button flashlightButton;
    public Button bellButton;
    
    public GameObject hintPanel;
    public GameObject gameOverPanel;
    
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
            hintPanel.SetActive(true);
        }
    }
    
    public void HideHint()
    {
        if (hintPanel != null)
        {
            hintPanel.SetActive(false);
        }
    }
    
    public void ShowGameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }
}


