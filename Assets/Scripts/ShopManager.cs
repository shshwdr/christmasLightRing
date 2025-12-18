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


