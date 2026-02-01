using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;
    
    public GameObject shopPanel;
    public Button continueButton;
    public GameObject shopItemPrefab;
    public Transform shopItemParent;
    public GameObject shopUpgradeItemPrefab;
    public Transform shopUpgradeItemParent;
    public TextMeshProUGUI freeText;
    
    private List<ShopItem> shopItems = new List<ShopItem>();
    private List<ShopUpgradeItem> shopUpgradeItems = new List<ShopUpgradeItem>();
    
    // 免费模式状态
    private bool isFreeMode = false;
    private FreeModeType freeModeType = FreeModeType.None;
    private int freeCurrentCount = 0;
    private int freeTotalCount = 0;
    
    private enum FreeModeType
    {
        None,
        FreeItem,
        FreeUpgrade
    }
    
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
        
        if (freeText != null)
        {
            freeText.gameObject.SetActive(false);
        }
    }
    
    public void ShowShop()
    {
        // 重置免费模式
        isFreeMode = false;
        freeModeType = FreeModeType.None;
        freeCurrentCount = 0;
        freeTotalCount = 0;
        
        // 在显示商店前，先reveal所有未翻开的卡牌
        if (GameManager.Instance != null && GameManager.Instance.boardManager != null)
        {
            GameManager.Instance.RevealAllCardsBeforeLeaving(() =>
            {
                ShowShopInternal();
            });
        }
        else
        {
            ShowShopInternal();
        }
    }
    
    public void ShowShopWithFreeItem(int count)
    {
        isFreeMode = true;
        freeModeType = FreeModeType.FreeItem;
        freeCurrentCount = 0;
        freeTotalCount = count;
        
            ShowShopInternal();
        
    }
    
    public void ShowShopWithFreeUpgrade(int count)
    {
        isFreeMode = true;
        freeModeType = FreeModeType.FreeUpgrade;
        freeCurrentCount = 0;
        freeTotalCount = count;
        
            ShowShopInternal();
        
    }
    
    private void ShowShopInternal()
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
        
        // 进入商店时回两滴血，不超过起始血量（仅在非免费模式）
        if (!isFreeMode && GameManager.Instance != null)
        {
            GameManager.Instance.mainGameData.health += 1;
            if (GameManager.Instance.mainGameData.health > GameManager.Instance.initialHealth)
            {
                GameManager.Instance.mainGameData.health = GameManager.Instance.initialHealth;
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
        
        // 设置免费模式UI
        if (isFreeMode)
        {
            if (freeText != null)
            {
                freeText.gameObject.SetActive(true);
                if (freeModeType == FreeModeType.FreeItem)
                {
                    // 使用 Localization
                    var chooseFreeLocalizedString = new LocalizedString("GameText", "ChooseFreeStartingCard");
                    chooseFreeLocalizedString.Arguments = new object[] { freeCurrentCount + 1, freeTotalCount };
                    freeText.text = chooseFreeLocalizedString.GetLocalizedString();
                    // 设置 freeText 位置为 upgradeParent 的位置
                    if (shopUpgradeItemParent != null)
                    {
                        freeText.transform.position = shopUpgradeItemParent.position;
                    }
                    // 隐藏 upgradeParent
                    if (shopUpgradeItemParent != null)
                    {
                        shopUpgradeItemParent.gameObject.SetActive(false);
                    }
                    // 显示 itemParent
                    if (shopItemParent != null)
                    {
                        shopItemParent.gameObject.SetActive(true);
                    }
                }
                else if (freeModeType == FreeModeType.FreeUpgrade)
                {
                    // 使用 Localization
                    var chooseFreeUpgradeLocalizedString = new LocalizedString("GameText", "ChooseFreeStartingUpgrade");
                    chooseFreeUpgradeLocalizedString.Arguments = new object[] { freeCurrentCount + 1, freeTotalCount };
                    freeText.text = chooseFreeUpgradeLocalizedString.GetLocalizedString();
                    // 设置 freeText 位置为 itemParent 的位置
                    if (shopItemParent != null)
                    {
                        freeText.transform.position = shopItemParent.position;
                    }
                    // 隐藏 itemParent
                    if (shopItemParent != null)
                    {
                        shopItemParent.gameObject.SetActive(false);
                    }
                    // 显示 upgradeParent
                    if (shopUpgradeItemParent != null)
                    {
                        shopUpgradeItemParent.gameObject.SetActive(true);
                    }
                }
            }
            
            // 隐藏 continue 按钮（免费模式下选择后自动继续）
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
            }
        }
        else
        {
            // 正常模式：显示两个 parent，隐藏 freeText
            if (shopItemParent != null)
            {
                shopItemParent.gameObject.SetActive(true);
            }
            if (shopUpgradeItemParent != null)
            {
                shopUpgradeItemParent.gameObject.SetActive(true);
            }
            if (freeText != null)
            {
                freeText.gameObject.SetActive(false);
            }
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
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
        
        // 免费模式：只加载对应的类型
        if (isFreeMode && freeModeType == FreeModeType.FreeItem)
        {
            UpdateFreeItems();
            return;
        }
        
        if (isFreeMode && freeModeType == FreeModeType.FreeUpgrade)
        {
            UpdateFreeUpgrades();
            return;
        }
        
        // 正常模式：加载所有
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
                shopItem.Setup(cardInfo, false);
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
                        !GameManager.Instance.mainGameData.ownedUpgrades.Contains(upgradeInfo.identifier))
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
                    shopUpgradeItem.Setup(upgradeInfo, false);
                    shopUpgradeItems.Add(shopUpgradeItem);
                }
            }
        }
    }
    
    private void UpdateFreeItems()
    {
        if (CardInfoManager.Instance == null || shopItemPrefab == null || shopItemParent == null)
            return;
        
        // 获取可购买的卡牌
        List<CardInfo> purchasableCards = CardInfoManager.Instance.GetPurchasableCards();

        // 随机选择显示3个
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
        
        // 创建商店物品（免费模式）
        foreach (CardInfo cardInfo in cardsToShow)
        {
            GameObject itemObj = Instantiate(shopItemPrefab, shopItemParent);
            ShopItem shopItem = itemObj.GetComponent<ShopItem>();
            if (shopItem != null)
            {
                shopItem.Setup(cardInfo, true);
                shopItems.Add(shopItem);
            }
        }
    }
    
    private void UpdateFreeUpgrades()
    {
        if (CSVLoader.Instance == null || shopUpgradeItemPrefab == null || shopUpgradeItemParent == null)
            return;
        
        // 获取可购买的升级项（canDraw为true且当前没有拥有的）
        List<UpgradeInfo> availableUpgrades = new List<UpgradeInfo>();
        if (GameManager.Instance != null)
        {
            foreach (var kvp in CSVLoader.Instance.upgradeDict)
            {
                UpgradeInfo upgradeInfo = kvp.Value;
                if (upgradeInfo.canDraw && 
                    !GameManager.Instance.mainGameData.ownedUpgrades.Contains(upgradeInfo.identifier))
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
        
        // 创建升级项物品（免费模式）
        foreach (UpgradeInfo upgradeInfo in upgradesToShow)
        {
            GameObject itemObj = Instantiate(shopUpgradeItemPrefab, shopUpgradeItemParent);
            ShopUpgradeItem shopUpgradeItem = itemObj.GetComponent<ShopUpgradeItem>();
            if (shopUpgradeItem != null)
            {
                shopUpgradeItem.Setup(upgradeInfo, true);
                shopUpgradeItems.Add(shopUpgradeItem);
            }
        }
    }
    
    public void OnFreeItemPicked()
    {
        if (!isFreeMode || freeModeType != FreeModeType.FreeItem)
            return;
        
        freeCurrentCount++;
        
        // 清除所有现有的 items
        foreach (ShopItem item in shopItems)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        shopItems.Clear();
        
        // 更新 freeText
        if (freeText != null)
        {
            // 使用 Localization
            var chooseFreeLocalizedString = new LocalizedString("GameText", "ChooseFreeStartingCard");
            chooseFreeLocalizedString.Arguments = new object[] { freeCurrentCount + 1, freeTotalCount };
            freeText.text = chooseFreeLocalizedString.GetLocalizedString();
        }
        
        // 检查是否完成
        if (freeCurrentCount >= freeTotalCount)
        {
            // 完成所有选择，关闭商店
            HideShop();
        }
        else
        {
            // 重新加载 items
            UpdateFreeItems();
        }
    }
    
    public void OnFreeUpgradePicked()
    {
        if (!isFreeMode || freeModeType != FreeModeType.FreeUpgrade)
            return;
        
        freeCurrentCount++;
        
        // 清除所有现有的 upgrades
        foreach (ShopUpgradeItem item in shopUpgradeItems)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        shopUpgradeItems.Clear();
        
        // 更新 freeText
        if (freeText != null)
        {
            // 使用 Localization
            var chooseFreeUpgradeLocalizedString = new LocalizedString("GameText", "ChooseFreeStartingUpgrade");
            chooseFreeUpgradeLocalizedString.Arguments = new object[] { freeCurrentCount + 1, freeTotalCount };
            freeText.text = chooseFreeUpgradeLocalizedString.GetLocalizedString();
        }
        
        // 检查是否完成
        if (freeCurrentCount >= freeTotalCount)
        {
            // 完成所有选择，关闭商店
            HideShop();
        }
        else
        {
            // 重新加载 upgrades
            UpdateFreeUpgrades();
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
        
        // 如果是免费模式，通知 GameManager 继续处理下一个免费商店
        if (isFreeMode)
        {
            bool wasFreeMode = isFreeMode;
            FreeModeType wasFreeModeType = freeModeType;
            
            // 重置免费模式状态
            isFreeMode = false;
            freeModeType = FreeModeType.None;
            freeCurrentCount = 0;
            freeTotalCount = 0;
            
            // 通知 GameManager 免费商店已关闭
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnFreeShopClosed(wasFreeModeType == FreeModeType.FreeItem);
            }
        }
        else
        {
            GameManager.Instance.boardManager.RestartAnimateBoard();
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
