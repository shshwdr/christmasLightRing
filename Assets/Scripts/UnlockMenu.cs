using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class UnlockMenu : MonoBehaviour
{
    public static UnlockMenu Instance;
    
    public GameObject unlockMenuPanel;
    public Button closeButton;
    public Transform contentParent; // 统一的父节点，用于放置所有卡牌和升级项
    public GameObject shopItemPrefab; // 商店卡牌prefab（复用）
    public GameObject shopUpgradeItemPrefab; // 商店升级prefab（复用）
    
    private List<ShopItem> displayedCardItems = new List<ShopItem>();
    private List<ShopUpgradeItem> displayedUpgradeItems = new List<ShopUpgradeItem>();
    private System.Action onCloseCallback;
    
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
        if (unlockMenuPanel != null)
        {
            unlockMenuPanel.SetActive(false);
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseClicked);
        }
    }
    
    /// <summary>
    /// 显示解锁菜单
    /// </summary>
    /// <param name="scene">当前场景标识</param>
    /// <param name="onClose">关闭回调</param>
    public void ShowUnlockMenu(string scene, System.Action onClose = null)
    {
        if (unlockMenuPanel == null) return;
        
        onCloseCallback = onClose;
        
        // 清除现有内容
        ClearContent();
        
        // 获取该scene解锁的所有card和upgrade
        List<CardInfo> unlockedCards = GetUnlockedCardsForScene(scene);
        List<UpgradeInfo> unlockedUpgrades = GetUnlockedUpgradesForScene(scene);
        
        // 按identifier排序
        unlockedCards = unlockedCards.OrderBy(c => c.identifier).ToList();
        unlockedUpgrades = unlockedUpgrades.OrderBy(u => u.identifier).ToList();
        
        // 在统一的parent下显示所有内容
        if (contentParent != null)
        {
            // 先显示所有卡牌
            if (shopItemPrefab != null)
            {
                foreach (CardInfo cardInfo in unlockedCards)
                {
                    GameObject itemObj = Instantiate(shopItemPrefab, contentParent);
                    ShopItem shopItem = itemObj.GetComponent<ShopItem>();
                    if (shopItem != null)
                    {
                        shopItem.Setup(cardInfo, false, true); // 第三个参数为true表示unlock模式
                        // 隐藏购买按钮
                        if (shopItem.buyButton != null)
                        {
                            shopItem.buyButton.gameObject.SetActive(false);
                        }
                        displayedCardItems.Add(shopItem);
                    }
                }
            }
            
            // 然后显示所有升级项
            if (shopUpgradeItemPrefab != null)
            {
                foreach (UpgradeInfo upgradeInfo in unlockedUpgrades)
                {
                    GameObject itemObj = Instantiate(shopUpgradeItemPrefab, contentParent);
                    ShopUpgradeItem shopUpgradeItem = itemObj.GetComponent<ShopUpgradeItem>();
                    if (shopUpgradeItem != null)
                    {
                        shopUpgradeItem.Setup(upgradeInfo, false, true); // 第三个参数为true表示unlock模式
                        // 隐藏购买按钮
                        if (shopUpgradeItem.buyButton != null)
                        {
                            shopUpgradeItem.buyButton.gameObject.SetActive(false);
                        }
                        displayedUpgradeItems.Add(shopUpgradeItem);
                    }
                }
            }
        }
        
        // 显示菜单
        unlockMenuPanel.SetActive(true);
        SFXManager.Instance?.PlayClickSound();
    }
    
    /// <summary>
    /// 获取指定scene解锁的所有卡牌
    /// </summary>
    private List<CardInfo> GetUnlockedCardsForScene(string scene)
    {
        List<CardInfo> unlockedCards = new List<CardInfo>();
        
        if (CSVLoader.Instance == null || string.IsNullOrEmpty(scene))
        {
            return unlockedCards;
        }
        
        foreach (var kvp in CSVLoader.Instance.cardDict)
        {
            CardInfo cardInfo = kvp.Value;
            // 检查scene是否匹配
            if (!string.IsNullOrEmpty(cardInfo.scene) && cardInfo.scene == scene)
            {
                unlockedCards.Add(cardInfo);
            }
        }
        
        return unlockedCards;
    }
    
    /// <summary>
    /// 获取指定scene解锁的所有升级项
    /// </summary>
    private List<UpgradeInfo> GetUnlockedUpgradesForScene(string scene)
    {
        List<UpgradeInfo> unlockedUpgrades = new List<UpgradeInfo>();
        
        if (CSVLoader.Instance == null || string.IsNullOrEmpty(scene))
        {
            return unlockedUpgrades;
        }
        
        foreach (var kvp in CSVLoader.Instance.upgradeDict)
        {
            UpgradeInfo upgradeInfo = kvp.Value;
            // 检查scene是否匹配
            if (!string.IsNullOrEmpty(upgradeInfo.scene) && upgradeInfo.scene == scene)
            {
                unlockedUpgrades.Add(upgradeInfo);
            }
        }
        
        return unlockedUpgrades;
    }
    
    /// <summary>
    /// 清除显示的内容
    /// </summary>
    private void ClearContent()
    {
        // 清除卡牌项
        foreach (ShopItem item in displayedCardItems)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        displayedCardItems.Clear();
        
        // 清除升级项
        foreach (ShopUpgradeItem item in displayedUpgradeItems)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        displayedUpgradeItems.Clear();
    }
    
    /// <summary>
    /// 关闭按钮点击事件
    /// </summary>
    private void OnCloseClicked()
    {
        SFXManager.Instance?.PlayClickSound();
        
        if (unlockMenuPanel != null)
        {
            unlockMenuPanel.SetActive(false);
        }
        
        // 调用关闭回调
        onCloseCallback?.Invoke();
        onCloseCallback = null;
    }
    
    /// <summary>
    /// 隐藏解锁菜单
    /// </summary>
    public void HideUnlockMenu()
    {
        if (unlockMenuPanel != null)
        {
            unlockMenuPanel.SetActive(false);
        }
        
        ClearContent();
        onCloseCallback = null;
    }
}

