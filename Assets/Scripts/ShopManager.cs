using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;
    
    public GameObject shopPanel;
    public Button continueButton;
    public GameObject shopItemPrefab;
    public Transform shopItemParent;
    
    private List<ShopItem> shopItems = new List<ShopItem>();
    
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
        
        UpdateShopItems();
    }
    
    public void UpdateShopItems()
    {
        // 清除现有物品
        foreach (ShopItem item in shopItems)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        shopItems.Clear();
        
        if (CardInfoManager.Instance == null || shopItemPrefab == null || shopItemParent == null)
            return;
        
        // 获取可购买的卡牌
        List<CardInfo> purchasableCards = CardInfoManager.Instance.GetPurchasableCards();

        // 随机选择显示（可以根据需求调整显示数量）
        List<CardInfo> cardsToShow = new List<CardInfo>();
        for (int i = 0; i < 3; i++)
        {
            cardsToShow.Add(purchasableCards.PickItem());
        }
        
        
        // 打乱顺序
        for (int i = cardsToShow.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            CardInfo temp = cardsToShow[i];
            cardsToShow[i] = cardsToShow[j];
            cardsToShow[j] = temp;
        }
        
        // 创建商店物品
        foreach (CardInfo cardInfo in cardsToShow)
        {
            GameObject itemObj = Instantiate(shopItemPrefab, shopItemParent);
            ShopItem shopItem = itemObj.GetComponent<ShopItem>();
            if (shopItem != null)
            {
                shopItem.Setup(cardInfo);
                shopItems.Add(shopItem);
            }
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
