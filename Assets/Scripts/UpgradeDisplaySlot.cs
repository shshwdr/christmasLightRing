using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class UpgradeDisplaySlot : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;
    public Button sellButton;
    public GameObject emptySlotIndicator;
    
    private string upgradeIdentifier;
    private bool isSelected = false;
    private RectTransform rectTransform;
    
    [SerializeField]
    private float pulseScale = 1.2f; // 放大倍数
    [SerializeField]
    private float pulseDuration = 0.3f; // 动画持续时间
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }
    
    public void Setup(string identifier)
    {
        upgradeIdentifier = identifier;
        isSelected = false;
        
        if (CSVLoader.Instance == null || !CSVLoader.Instance.upgradeDict.ContainsKey(identifier))
        {
            ClearSlot();
            return;
        }
        
        UpgradeInfo upgradeInfo = CSVLoader.Instance.upgradeDict[identifier];
        
        if (nameText != null)
        {
            // 从 Localization 获取升级名称
            string nameKey = "upgradeName_" + identifier;
            var nameLocalizedString = new LocalizedString("GameText", nameKey);
            var nameHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(nameLocalizedString.TableReference, nameLocalizedString.TableEntryReference);
            nameText.text = nameHandle.WaitForCompletion();
        }
        
        if (descText != null)
        {
            // 从 Localization 获取升级描述
            string descKey = "upgradeDesc_" + identifier;
            var descLocalizedString = new LocalizedString("GameText", descKey);
            var descHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(descLocalizedString.TableReference, descLocalizedString.TableEntryReference);
            descText.text = descHandle.WaitForCompletion();
        }
        
        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(false);
            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(OnSellClicked);
            
            // 更新sell按钮的文字显示，从本地化读取
            TextMeshProUGUI sellButtonText = sellButton.GetComponentInChildren<TextMeshProUGUI>();
            if (sellButtonText != null)
            {
                // 计算卖出价格（不包含Cashback的额外1金币，因为那是额外获得的）
                // 需要和OnSellClicked中的逻辑保持一致
                int sellPrice = 0;
                if (upgradeIdentifier == "Loan")
                {
                    sellPrice = -15; // Loan卖出时需要付出15金币
                }
                else if (upgradeIdentifier == "GreedJackpot" || upgradeIdentifier == "MedicalBill")
                {
                    sellPrice = 0; // GreedJackpot 和 MedicalBill 卖出价格为0
                }
                else
                {
                    sellPrice = upgradeInfo.cost / 2;
                    // Cashback的额外1金币不在卖出价格中显示，因为那是额外获得的
                }
                
                // 从 Localization 获取 SELL 文字
                var sellLocalizedString = new LocalizedString("GameText", "SELL");
                var sellHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(sellLocalizedString.TableReference, sellLocalizedString.TableEntryReference);
                string sellText = sellHandle.WaitForCompletion();
                sellButtonText.text = $"{sellText} ({sellPrice})";
            }
        }
        
        if (emptySlotIndicator != null)
        {
            emptySlotIndicator.SetActive(false);
        }
        
        // 添加点击事件来显示/隐藏出售按钮
        Button slotButton = GetComponent<Button>();
        if (slotButton == null)
        {
            slotButton = gameObject.AddComponent<Button>();
        }
        slotButton.onClick.RemoveAllListeners();
        slotButton.onClick.AddListener(OnSlotClicked);
    }
    
    public void ClearSlot()
    {
        upgradeIdentifier = null;
        isSelected = false;
        
        if (nameText != null)
        {
            nameText.text = "";
        }
        
        if (descText != null)
        {
            descText.text = "";
        }
        
        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(false);
        }
        
        if (emptySlotIndicator != null)
        {
            emptySlotIndicator.SetActive(true);
        }
    }
    
    private void OnSlotClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        if (string.IsNullOrEmpty(upgradeIdentifier)) return;
        
        isSelected = !isSelected;
        
        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(isSelected);
            
            // 更新sell按钮的文字显示，从本地化读取
            if (isSelected && CSVLoader.Instance != null && CSVLoader.Instance.upgradeDict.ContainsKey(upgradeIdentifier))
            {
                UpgradeInfo upgradeInfo = CSVLoader.Instance.upgradeDict[upgradeIdentifier];
                TextMeshProUGUI sellButtonText = sellButton.GetComponentInChildren<TextMeshProUGUI>();
                if (sellButtonText != null)
                {
                    // 计算卖出价格（不包含Cashback的额外1金币，因为那是额外获得的）
                    // 需要和OnSellClicked中的逻辑保持一致
                    int sellPrice = 0;
                    if (upgradeIdentifier == "Loan")
                    {
                        sellPrice = -15; // Loan卖出时需要付出15金币
                    }
                    else if (upgradeIdentifier == "GreedJackpot" || upgradeIdentifier == "MedicalBill")
                    {
                        sellPrice = 0; // GreedJackpot 和 MedicalBill 卖出价格为0
                    }
                    else
                    {
                        sellPrice = upgradeInfo.cost / 2;
                        // Cashback的额外1金币不在卖出价格中显示，因为那是额外获得的
                    }
                    
                    // 从 Localization 获取 SELL 文字
                    var sellLocalizedString = new LocalizedString("GameText", "SELL");
                    var sellHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(sellLocalizedString.TableReference, sellLocalizedString.TableEntryReference);
                    string sellText = sellHandle.WaitForCompletion();
                    sellButtonText.text = $"{sellText} ({sellPrice})";
                }
            }
        }
    }
    
    private void OnSellClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        if (string.IsNullOrEmpty(upgradeIdentifier) || GameManager.Instance == null) return;
        
        if (!CSVLoader.Instance.upgradeDict.ContainsKey(upgradeIdentifier)) return;
        
        UpgradeInfo upgradeInfo = CSVLoader.Instance.upgradeDict[upgradeIdentifier];
        
        // Loan: 卖出时需要付出10金币
        if (upgradeIdentifier == "Loan")
        {
            // 检查是否有足够的金币支付
            if (GameManager.Instance.mainGameData.coins < 10)
            {
                // 显示提示：金币不足
                if (DialogPanel.Instance != null)
                {
                    string notEnoughCoinsText = LocalizationHelper.GetLocalizedString("NotEnoughCoins");
                    DialogPanel.Instance.ShowDialog(notEnoughCoinsText, null);
                }
                return; // 不能卖出
            }
            
            GameManager.Instance.mainGameData.coins -= 10;
            GameManager.Instance.ShowFloatingText("coin", -10);
        }
        
        // GreedJackpot 和 MedicalBill: 卖出价格为0
        int sellPrice = 0;
        if (upgradeIdentifier != "GreedJackpot" && upgradeIdentifier != "Loan" && upgradeIdentifier != "MedicalBill")
        {
            sellPrice = upgradeInfo.cost / 2;
        }
        
        // Cashback: 卖掉其他升级项的时候额外获得1金币（在移除升级项之前检查）
        bool hasCashback = upgradeIdentifier != "Cashback" && GameManager.Instance.upgradeManager != null && 
            GameManager.Instance.upgradeManager.HasUpgrade("Cashback");
        
        // 先处理卖出时的特殊效果（在移除升级项之前）
        // CashOut: 现在改为在翻开铃铛或Door时转换礼物为金币，卖出时不再转换
        
        // Band-Aid: 卖出时恢复1点血
        if (upgradeIdentifier == "Band-Aid")
        {
            GameManager.Instance.AddHealth(1, false);
        }
        
        // JingleGuide: 卖掉的时候翻开铃铛
        if (upgradeIdentifier == "JingleGuide")
        {
            if (GameManager.Instance.upgradeManager != null)
            {
                GameManager.Instance.upgradeManager.OnJingleGuideSold();
            }
        }
        
        // Spotter: 卖掉的时候翻开一个随机敌人并眩晕它
        if (upgradeIdentifier == "Spotter")
        {
            if (GameManager.Instance.upgradeManager != null)
            {
                GameManager.Instance.upgradeManager.OnSpotterSold();
            }
        }
        
        // Owl: 卖掉的时候逐个翻开所有的hint
        if (upgradeIdentifier == "Owl")
        {
            if (GameManager.Instance.upgradeManager != null)
            {
                GameManager.Instance.upgradeManager.OnOwlSold();
            }
        }
        
        // GreedJackpot: 卖出时金币翻倍
        if (upgradeIdentifier == "GreedJackpot")
        {
            GameManager.Instance.mainGameData.coins *= 2;
            GameManager.Instance.ShowFloatingText("coin", GameManager.Instance.mainGameData.coins / 2); // 显示增加的金币数
        }
        else if (upgradeIdentifier != "Loan") // Loan已经在上面处理了，不需要再给金币
        {
            // 普通卖出：获得金币
            GameManager.Instance.mainGameData.coins += sellPrice;
            if (sellPrice > 0)
            {
                GameManager.Instance.ShowFloatingText("coin", sellPrice);
            }
            
            // Cashback: 额外获得1金币（分开显示）
            if (hasCashback)
            {
                GameManager.Instance.mainGameData.coins += 1;
                GameManager.Instance.ShowFloatingText("coin", 1);
            }
        }
        
        // 移除升级项
        GameManager.Instance.mainGameData.ownedUpgrades.Remove(upgradeIdentifier);
        
        // 通知升级项已卖出（用于处理特殊效果，如AsceticVow）
        if (GameManager.Instance.upgradeManager != null)
        {
            GameManager.Instance.upgradeManager.OnUpgradeSold(upgradeIdentifier);
        }
        
        // Coupon: 出售这个升级的时候，立刻更新目前商店里所有的物品价格
        if (upgradeIdentifier == "Coupon")
        {
            ShopManager.Instance?.UpdateAllShopItemPrices();
        }
        
        GameManager.Instance.uiManager?.UpdateUI();
        GameManager.Instance.uiManager?.UpdateUpgradeDisplay();
        // 只更新按钮状态，不刷新整个商店
        ShopManager.Instance?.UpdateAllBuyButtons();
    }
    
    // 检查这个slot是否显示指定的upgrade
    public bool IsDisplayingUpgrade(string identifier)
    {
        return upgradeIdentifier == identifier;
    }
    
    // 播放放大缩小动画
    public void PlayPulseAnimation()
    {
        if (rectTransform == null) return;
        
        // 停止之前的动画
        rectTransform.DOKill();
        
        // 保存原始缩放
        Vector3 originalScale = Vector3.one;
        
        // 创建动画序列：放大 -> 缩小回原尺寸
        Sequence sequence = DOTween.Sequence();
        sequence.Append(rectTransform.DOScale(originalScale * pulseScale, pulseDuration * 0.5f).SetEase(Ease.OutQuad));
        sequence.Append(rectTransform.DOScale(originalScale, pulseDuration * 0.5f).SetEase(Ease.InQuad));
    }
}





