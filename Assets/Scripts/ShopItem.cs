using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopItem : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI costText;
    public Button buyButton;
    
    private CardInfo cardInfo;
    
    public void Setup(CardInfo info)
    {
        cardInfo = info;
        
        if (iconImage != null && CardInfoManager.Instance != null)
        {
            CardType cardType = CardInfoManager.Instance.GetCardType(info.identifier);
            Sprite sprite = CardInfoManager.Instance.GetCardSprite(cardType);
            if (sprite != null)
            {
                iconImage.sprite = sprite;
            }
        }
        
        if (nameText != null)
        {
            nameText.text = info.name;
        }
        
        if (descText != null)
        {
            descText.text = info.desc;
        }
        
        if (costText != null)
        {
            costText.text = $"Buy {info.cost.ToString()}";
        }
        
        UpdateBuyButton();
        
        buyButton.onClick.AddListener(OnBuyClicked);
    }
    
    public void UpdateBuyButton()
    {
        if (GameManager.Instance == null || cardInfo == null) return;
        
        bool canAfford = GameManager.Instance.gameData.coins >= cardInfo.cost;
        
        if (buyButton != null)
        {
            buyButton.interactable = canAfford;
        }
        
        if (costText != null)
        {
            costText.color = canAfford ? Color.black : Color.red;
        }
    }
    
    public void OnBuyClicked()
    {
        if (GameManager.Instance == null || cardInfo == null) return;
        
        if (GameManager.Instance.gameData.coins >= cardInfo.cost)
        {
            GameManager.Instance.gameData.coins -= cardInfo.cost;
            CardType cardType = CardInfoManager.Instance.GetCardType(cardInfo.identifier);
            GameManager.Instance.gameData.purchasedCards.Add(cardType);
            GameManager.Instance.uiManager?.UpdateUI();
            
            // 更新所有商店物品的按钮状态
            ShopManager.Instance?.UpdateShopItems();
        }
    }
}

