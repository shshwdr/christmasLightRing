using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;
    
    public GameObject shopPanel;
    public Button continueButton;
    public GameObject shopItemPrefab;
    public Transform shopItemParent;
    public GameObject shopUpgradeItemPrefab;
    public Transform shopUpgradeItemParent;
    
    private List<ShopItem> shopItems = new List<ShopItem>();
    private List<ShopUpgradeItem> shopUpgradeItems = new List<ShopUpgradeItem>();
    
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
        
        // 进入商店时隐藏descPanel
        if (UIManager.Instance != null && UIManager.Instance.descPanel != null)
        {
            UIManager.Instance.descPanel.SetActive(false);
        }
        
        // 进入商店时回两滴血，不超过起始血量
        if (GameManager.Instance != null)
        {
            GameManager.Instance.gameData.health += 1;
            if (GameManager.Instance.gameData.health > GameManager.Instance.initialHealth)
            {
                GameManager.Instance.gameData.health = GameManager.Instance.initialHealth;
            }
            GameManager.Instance.ShowFloatingText("health", 1);
            GameManager.Instance.CheckAndUpdateShake(); // 更新抖动状态
            GameManager.Instance.uiManager?.UpdateUI();
        }
        
        // 进入商店时停止抖动
        ShakeManager.Instance?.StopShake();
        
        // 禁用flashLight和ringBell按钮
        if (UIManager.Instance != null)
        {
            if (UIManager.Instance.flashlightButton != null)
            {
                UIManager.Instance.flashlightButton.interactable = false;
            }
            if (UIManager.Instance.bellButton != null)
            {
                UIManager.Instance.bellButton.interactable = false;
                FindObjectOfType<UIManager>().bellButtonInteractableObject.SetActive(false);

            }
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
        
        // 清除现有升级项
        foreach (ShopUpgradeItem item in shopUpgradeItems)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        shopUpgradeItems.Clear();
        
        if (CardInfoManager.Instance == null || shopItemPrefab == null || shopItemParent == null)
            return;
        
        // 获取可购买的卡牌
        List<CardInfo> purchasableCards = CardInfoManager.Instance.GetPurchasableCards();

        // 随机选择显示（可以根据需求调整显示数量）
        List<CardInfo> cardsToShow = new List<CardInfo>();
        List<CardInfo> availableCards = new List<CardInfo>(purchasableCards);
        for (int i = 0; i < 3 && availableCards.Count > 0; i++)
        {
            cardsToShow.Add(availableCards.PickItem());
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
        
        // 处理升级项
        if (CSVLoader.Instance != null && shopUpgradeItemPrefab != null && shopUpgradeItemParent != null)
        {
            // 获取可购买的升级项（canDraw为true且当前没有拥有的）
            List<UpgradeInfo> availableUpgrades = new List<UpgradeInfo>();
            if (GameManager.Instance != null)
            {
                foreach (var kvp in CSVLoader.Instance.upgradeDict)
                {
                    UpgradeInfo upgradeInfo = kvp.Value;
                    if (upgradeInfo.canDraw && 
                        !GameManager.Instance.gameData.ownedUpgrades.Contains(upgradeInfo.identifier))
                    {
                        availableUpgrades.Add(upgradeInfo);
                    }
                }
            }
            
            // 随机选择3个升级项显示
            List<UpgradeInfo> upgradesToShow = new List<UpgradeInfo>();
            List<UpgradeInfo> availableUpgradesCopy = new List<UpgradeInfo>(availableUpgrades);
            for (int i = 0; i < 3 && availableUpgradesCopy.Count > 0; i++)
            {
                upgradesToShow.Add(availableUpgradesCopy.PickItem());
            }
            
            // 打乱顺序
            for (int i = upgradesToShow.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                UpgradeInfo temp = upgradesToShow[i];
                upgradesToShow[i] = upgradesToShow[j];
                upgradesToShow[j] = temp;
            }
            
            // 创建升级项物品
            foreach (UpgradeInfo upgradeInfo in upgradesToShow)
            {
                GameObject itemObj = Instantiate(shopUpgradeItemPrefab, shopUpgradeItemParent);
                ShopUpgradeItem shopUpgradeItem = itemObj.GetComponent<ShopUpgradeItem>();
                if (shopUpgradeItem != null)
                {
                    shopUpgradeItem.Setup(upgradeInfo);
                    shopUpgradeItems.Add(shopUpgradeItem);
                }
            }
        }
    }
    
    public void HideShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
        
        // 恢复flashLight和ringBell按钮的状态
        if (UIManager.Instance != null)
        {
            // 恢复flashlight按钮状态
            UIManager.Instance.UpdateFlashlightButton();
            
            // 恢复bell按钮状态（如果按钮是显示的，则恢复可点击状态）
            //if (UIManager.Instance.bellButton != null && UIManager.Instance.bellButton.gameObject.activeSelf)
            {
                UIManager.Instance.bellButton.interactable = true;
            }
        }
    }
    
    // 更新所有商店物品的按钮状态（不刷新整个商店）
    public void UpdateAllBuyButtons()
    {
        foreach (ShopItem item in shopItems)
        {
            if (item != null)
            {
                item.UpdateBuyButton();
            }
        }
        
        foreach (ShopUpgradeItem item in shopUpgradeItems)
        {
            if (item != null)
            {
                item.UpdateBuyButton();
            }
        }
    }
    
    private void OnContinueClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 播放离开商店音效
        SFXManager.Instance?.PlaySFX("leaveShop");
        
        GameManager.Instance?.NextLevel();
    }
}
