using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    public GameObject shopPanel;
    public Button continueButton;
    
    private void Start()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }
        
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
    }
    
    public void ShowShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
        }
        
        // 进入商店时回两滴血，不超过起始血量
        if (GameManager.Instance != null)
        {
            GameManager.Instance.gameData.health += 2;
            if (GameManager.Instance.gameData.health > GameManager.Instance.initialHealth)
            {
                GameManager.Instance.gameData.health = GameManager.Instance.initialHealth;
            }
            GameManager.Instance.uiManager?.UpdateUI();
        }
    }
    
    public void HideShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
    }
    
    private void OnContinueClicked()
    {
        GameManager.Instance?.NextLevel();
    }
}


