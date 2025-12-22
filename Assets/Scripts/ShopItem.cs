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
    public GameObject content; // content的GameObject，购买后隐藏
    
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
        
        UpdateCostText();
        UpdateBuyButton();
        
        buyButton.onClick.AddListener(OnBuyClicked);
    }
    
    private int GetCardCount()
    {
        if (GameManager.Instance == null || cardInfo == null) return 0;
        
        CardType cardType = CardInfoManager.Instance.GetCardType(cardInfo.identifier);
        int count = 0;
        foreach (CardType purchasedType in GameManager.Instance.gameData.purchasedCards)
        {
            if (purchasedType == cardType)
            {
                count++;
            }
        }
        return count;
    }
    
    private int GetCurrentCost()
    {
        if (cardInfo == null) return 0;
        int count = GetCardCount();
        return cardInfo.cost + cardInfo.costIncrease * count;
    }
    
    private void UpdateCostText()
    {
        if (costText != null && cardInfo != null)
        {
            int currentCost = GetCurrentCost();
            costText.text = $"BUY {currentCost.ToString()}";
        }
    }
    
    public void UpdateBuyButton()
    {
        if (GameManager.Instance == null || cardInfo == null) return;
        
        int currentCost = GetCurrentCost();
        bool canAfford = GameManager.Instance.gameData.coins >= currentCost;
        
        if (buyButton != null)
        {
            buyButton.interactable = canAfford;
        }
        
        if (costText != null)
        {
            costText.color = canAfford ? Color.white : Color.red;
        }
    }
    
    public void OnBuyClicked()
    {
        if (GameManager.Instance == null || cardInfo == null) return;
        
        int currentCost = GetCurrentCost();
        if (GameManager.Instance.gameData.coins >= currentCost)
        {
            GameManager.Instance.gameData.coins -= currentCost;
            CardType cardType = CardInfoManager.Instance.GetCardType(cardInfo.identifier);
            GameManager.Instance.gameData.purchasedCards.Add(cardType);
            GameManager.Instance.uiManager?.UpdateUI();
            
            // 隐藏content，不刷新整个商店
            if (content != null)
            {
                content.SetActive(false);
            }
            
            // 更新所有商店物品的按钮状态（不刷新商店）
            ShopManager.Instance?.UpdateAllBuyButtons();
        }
    }
}

