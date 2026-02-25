using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;
    
    public GameObject shopPanel;
    public Button continueButton;
    public Button refreshButton; // 刷新按钮
    public Button removeCardButton; // 移除卡牌按钮
    public TextMeshProUGUI refreshCostText; // 刷新费用显示文本
    public GameObject shopItemPrefab;
    public Transform shopItemParent;
    public GameObject shopUpgradeItemPrefab;
    public Transform shopUpgradeItemParent;
    public TextMeshProUGUI freeText;

    public GameObject shopItemGO;
    public GameObject shopUpgradeGO;
    
    private List<ShopItem> shopItems = new List<ShopItem>();
    private List<ShopUpgradeItem> shopUpgradeItems = new List<ShopUpgradeItem>();
    
    // 免费模式状态
    private bool isFreeMode = false;
    private FreeModeType freeModeType = FreeModeType.None;
    private int freeCurrentCount = 0;
    private int freeTotalCount = 0;
    
    // 跟踪本回合是否购买了任何东西（用于Miser升级项）
    private bool hasPurchasedThisTurn = false;
    // 跟踪商店是否被打开过（用于Miser升级项，只有打开过商店且没花钱才触发）
    private bool hasShopBeenShown = false;
    
    // 刷新费用（每次进入商店重置为1，每次刷新+1）
    private int refreshCost = 1;
    
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
        
        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(OnRefreshClicked);
        }
        
        if (removeCardButton != null)
        {
            removeCardButton.onClick.AddListener(OnRemoveCardClicked);
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
        
        // 重置购买状态（用于Miser升级项）
        hasPurchasedThisTurn = false;
        // 标记商店已被打开（用于Miser升级项）
        hasShopBeenShown = true;
        
        // 重置刷新费用为1
        refreshCost = 1;
        
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
        
        // 重置购买状态（用于Miser升级项）
        hasPurchasedThisTurn = false;
        // 标记商店已被打开（用于Miser升级项）
        hasShopBeenShown = true;
        
            ShowShopInternal();
        
    }
    
    public void ShowShopWithFreeUpgrade(int count)
    {
        isFreeMode = true;
        freeModeType = FreeModeType.FreeUpgrade;
        freeCurrentCount = 0;
        freeTotalCount = count;
        
        // 重置购买状态（用于Miser升级项）
        hasPurchasedThisTurn = false;
        // 标记商店已被打开（用于Miser升级项）
        hasShopBeenShown = true;
        
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
        
        // 进入商店时回血，不超过最大血量（仅在非免费模式；noHeal 模式下不进商店回血）
        var sceneInfo = GameManager.Instance?.GetCurrentSceneInfo();
        bool noHealMode = sceneInfo != null && sceneInfo.HasType("noHeal");
        if (!isFreeMode && GameManager.Instance != null && !noHealMode)
        {
            int healthBeforeHeal = GameManager.Instance.mainGameData.health;
            GameManager.Instance.AddHealth(1, true); // 使用AddHealth方法，isShopHeal=true以触发AsceticVow效果
            int healthAfterHeal = GameManager.Instance.mainGameData.health;
            int maxHealth = GameManager.Instance.GetMaxHealth();
            
            // damageDiscount: 进入商店并回血后，血量依然不满时，随机一个商品价格为0
            if (healthAfterHeal < maxHealth && GameManager.Instance.upgradeManager != null && 
                GameManager.Instance.upgradeManager.HasUpgrade("damageDiscount"))
            {
                // 在UpdateShopItems之后设置免费商品（因为UpdateShopItems会清除所有商品）
                // 所以我们需要在UpdateShopItems之后调用
                StartCoroutine(ApplyDamageDiscountAfterShopItemsUpdate());
            }
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
                        shopUpgradeGO.gameObject.SetActive(false);
                    }
                    // 显示 itemParent
                    if (shopItemGO != null)
                    {
                        shopItemGO.gameObject.SetActive(true);
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
                    if (shopItemGO != null)
                    {
                        shopItemGO.gameObject.SetActive(false);
                    }
                    // 显示 upgradeParent
                    if (shopUpgradeItemParent != null)
                    {
                        shopUpgradeGO.gameObject.SetActive(true);
                    }
                }
            }
            
            // 隐藏 continue 按钮（免费模式下选择后自动继续）
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
            }
            
            // 免费模式：隐藏刷新按钮
            if (refreshButton != null)
            {
                refreshButton.gameObject.SetActive(false);
            }
        }
        else
        {
            // 正常模式：显示两个 parent，隐藏 freeText
            if (shopItemGO != null)
            {
                shopItemGO.gameObject.SetActive(true);
            }
            if (shopUpgradeItemParent != null)
            {
                shopUpgradeGO.gameObject.SetActive(true);
            }
            if (freeText != null)
            {
                freeText.gameObject.SetActive(false);
            }
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
            }
            
            // 更新刷新按钮（仅在正常模式显示）
            UpdateRefreshButton();
        }
        
        UpdateShopItems();
    }
    
    // damageDiscount: 在UpdateShopItems之后应用免费商品
    private System.Collections.IEnumerator ApplyDamageDiscountAfterShopItemsUpdate()
    {
        // 等待一帧，确保UpdateShopItems已完成
        yield return null;
        
        if (shopItems == null || shopItems.Count == 0) yield break;
        
        // 随机选择一个商品设置为免费
        List<ShopItem> availableItems = shopItems.Where(item => item != null).ToList();
        if (availableItems.Count > 0)
        {
            ShopItem freeItem = availableItems[Random.Range(0, availableItems.Count)];
            freeItem.SetDamageDiscountFree();
        }
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

        // 检查是否需要强制出现coin卡牌
        // 如果玩家deck中的coin卡牌数量不足3张，且level数<=3，shop必定出现一张coin
        CardInfo coinCardInfo = CardInfoManager.Instance.GetCardInfo(CardType.Coin);
        bool shouldForceCoin = false;
        if (coinCardInfo != null && GameManager.Instance != null)
        {
            int currentLevel = GameManager.Instance.mainGameData.currentLevel;
            if (currentLevel <= 3)
            {
                // 计算deck中coin卡牌的数量
                int coinCount = GetCoinCardCount();
                if (coinCount < 3)
                {
                    shouldForceCoin = true;
                }
            }
        }
        
        // 检查是否需要强制出现hint卡牌
        // 如果level<=3且玩家持有的hint数量<=2，商店必定出现hint
        CardInfo hintCardInfo = CardInfoManager.Instance.GetCardInfo(CardType.Hint);
        bool shouldForceHint = false;
        if (hintCardInfo != null && GameManager.Instance != null)
        {
            int currentLevel = GameManager.Instance.mainGameData.currentLevel;
            if (currentLevel <= 3)
            {
                // 计算deck中hint卡牌的数量
                int hintCount = GetHintCardCount();
                if (hintCount <= 2)
                {
                    shouldForceHint = true;
                }
            }
        }

        // 随机选择显示（可以根据需求调整显示数量）
        List<CardInfo> cardsToShow = new List<CardInfo>();
        List<CardInfo> availableCards = new List<CardInfo>(purchasableCards);
        
        // 如果需要强制出现coin，先确保coin在可购买列表中，然后优先选择coin
        if (shouldForceCoin && coinCardInfo != null && purchasableCards.Contains(coinCardInfo))
        {
            cardsToShow.Add(coinCardInfo);
            availableCards.Remove(coinCardInfo); // 从可用列表中移除，避免重复选择
        }
        
        // 如果需要强制出现hint，先确保hint在可购买列表中，然后优先选择hint
        if (shouldForceHint && hintCardInfo != null && purchasableCards.Contains(hintCardInfo))
        {
            cardsToShow.Add(hintCardInfo);
            availableCards.Remove(hintCardInfo); // 从可用列表中移除，避免重复选择
        }
        
        // 继续选择其他卡牌（如果还需要更多卡牌）
        int remainingSlots = 3 - cardsToShow.Count;
        for (int i = 0; i < remainingSlots && availableCards.Count > 0; i++)
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
                string currentScene = GameManager.Instance.mainGameData.currentScene;
                foreach (var kvp in CSVLoader.Instance.upgradeDict)
                {
                    UpgradeInfo upgradeInfo = kvp.Value;
                    if (upgradeInfo.canDraw && 
                        !GameManager.Instance.mainGameData.ownedUpgrades.Contains(upgradeInfo.identifier))
                    {
                        // 检查scene解锁：如果scene不为空，需要当前场景 > scene（转换为int比较）
                        if (!string.IsNullOrEmpty(upgradeInfo.scene))
                        {
                            if (string.IsNullOrEmpty(currentScene))
                            {
                                continue; // 当前没有场景，无法解锁
                            }
                            
                            // 尝试将scene转换为int进行比较
                            if (int.TryParse(upgradeInfo.scene, out int requiredScene) && 
                                int.TryParse(currentScene, out int currentSceneInt))
                            {
                                if (currentSceneInt <= requiredScene)
                                {
                                    continue; // 当前场景小于等于所需场景，无法解锁
                                }
                            }
                            else
                            {
                                // 如果无法转换为int，则跳过scene检查（保持向后兼容）
                            }
                        }
                        
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
    
    // 获取deck中coin卡牌的数量
    private int GetCoinCardCount()
    {
        if (GameManager.Instance == null || CardInfoManager.Instance == null) return 0;
        
        CardInfo coinCardInfo = CardInfoManager.Instance.GetCardInfo(CardType.Coin);
        if (coinCardInfo == null) return 0;
        
        // 起始数量
        int count = coinCardInfo.start;
        
        // 加上购买的数量
        foreach (CardType purchasedType in GameManager.Instance.mainGameData.purchasedCards)
        {
            if (purchasedType == CardType.Coin)
            {
                count++;
            }
        }
        
        return count;
    }
    
    // 获取deck中hint卡牌的数量
    private int GetHintCardCount()
    {
        if (GameManager.Instance == null || CardInfoManager.Instance == null) return 0;
        
        CardInfo hintCardInfo = CardInfoManager.Instance.GetCardInfo(CardType.Hint);
        if (hintCardInfo == null) return 0;
        
        // 起始数量
        int count = hintCardInfo.start;
        
        // 加上购买的数量
        foreach (CardType purchasedType in GameManager.Instance.mainGameData.purchasedCards)
        {
            if (purchasedType == CardType.Hint)
            {
                count++;
            }
        }
        
        return count;
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
            string currentScene = GameManager.Instance.mainGameData.currentScene;
            foreach (var kvp in CSVLoader.Instance.upgradeDict)
            {
                UpgradeInfo upgradeInfo = kvp.Value;
                if (upgradeInfo.canDraw && 
                    !GameManager.Instance.mainGameData.ownedUpgrades.Contains(upgradeInfo.identifier))
                {
                    // 检查scene解锁：如果scene不为空，需要当前场景 >= scene（转换为int比较）
                    if (!string.IsNullOrEmpty(upgradeInfo.scene))
                    {
                        if (string.IsNullOrEmpty(currentScene))
                        {
                            continue; // 当前没有场景，无法解锁
                        }
                        
                        // 尝试将scene转换为int进行比较
                        if (int.TryParse(upgradeInfo.scene, out int requiredScene) && 
                            int.TryParse(currentScene, out int currentSceneInt))
                        {
                            if (currentSceneInt <= requiredScene)
                            {
                                continue; // 当前场景小于所需场景，无法解锁
                            }
                        }
                        else
                        {
                            // 如果无法转换为int，则跳过scene检查（保持向后兼容）
                        }
                    }
                    
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
        
        // Miser: 如果商店被打开过，且本回合没有在商店购买任何东西，金币+3
        if (hasShopBeenShown && !isFreeMode && !hasPurchasedThisTurn && GameManager.Instance != null)
        {
            if (GameManager.Instance.upgradeManager != null && 
                GameManager.Instance.upgradeManager.HasUpgrade("Miser"))
            {
                GameManager.Instance.mainGameData.coins += 3;
                GameManager.Instance.ShowFloatingText("coin", 3);
                GameManager.Instance.uiManager?.UpdateUI();
            }
        }
        
        // 重置商店打开标记（用于下次检查）
        hasShopBeenShown = false;
        
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
    
    // 标记已购买（用于Miser升级项）
    public void MarkPurchased()
    {
        hasPurchasedThisTurn = true;
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
        
        // 更新刷新按钮状态（因为购买后金币可能变化）
        UpdateRefreshButton();
    }
    
    // 更新所有商店物品的价格显示（用于Coupon升级项）
    public void UpdateAllShopItemPrices()
    {
        foreach (ShopItem item in shopItems)
        {
            if (item != null)
            {
                item.UpdateCostText();
                item.UpdateBuyButton();
            }
        }
        
        foreach (ShopUpgradeItem item in shopUpgradeItems)
        {
            if (item != null)
            {
                item.UpdateCostText();
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
    
    // 刷新按钮点击处理
    private void OnRefreshClicked()
    {
        if (GameManager.Instance == null) return;
        
        // 检查是否有足够的金币
        if (GameManager.Instance.mainGameData.coins < refreshCost)
        {
            // 金币不足，播放错误音效（如果有）
            SFXManager.Instance?.PlayClickSound();
            return;
        }
        
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 扣除金币
        GameManager.Instance.mainGameData.coins -= refreshCost;
        GameManager.Instance.ShowFloatingText("coin", -refreshCost);
        GameManager.Instance.uiManager?.UpdateUI();
        
        // 刷新费用+1
        refreshCost++;
        
        // 刷新所有物品（卡牌和升级项）
        UpdateShopItems();
        
        // 更新刷新按钮显示
        UpdateRefreshButton();
    }
    
    // 更新刷新按钮的显示和可点击状态
    private void UpdateRefreshButton()
    {
        if (refreshButton == null) return;
        
        // 仅在正常模式显示刷新按钮
        if (isFreeMode)
        {
            refreshButton.gameObject.SetActive(false);
            return;
        }
        
        refreshButton.gameObject.SetActive(true);
        
        // 更新费用文本
        if (refreshCostText != null)
        {
            refreshCostText.text =$"{LocalizationHelper.GetLocalizedString("Refresh")}({refreshCost.ToString()})";
        }
        
        // 更新按钮可点击状态（根据是否有足够金币）
        if (GameManager.Instance != null)
        {
            bool canAfford = GameManager.Instance.mainGameData.coins >= refreshCost;
            refreshButton.interactable = canAfford;
        }
    }
    
    // 移除卡牌按钮点击处理
    private void OnRemoveCardClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 显示DeckMenu，并设置为移除模式
        if (UIManager.Instance != null && UIManager.Instance.deckMenu != null)
        {
            UIManager.Instance.deckMenu.ShowMenuInRemoveMode();
        }
    }
}
