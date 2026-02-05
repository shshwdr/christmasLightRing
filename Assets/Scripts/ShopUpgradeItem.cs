using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class ShopUpgradeItem : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI costText;
    public Button buyButton;
    public GameObject content; // content的GameObject，购买后隐藏
    public TextMeshProUGUI newUpgradeText; // 显示"NewUpgrade"的文本，仅在unlock界面显示
    
    private UpgradeInfo upgradeInfo;
    private bool isFreeMode = false;
    private bool isUnlockMode = false;
    
    public void Setup(UpgradeInfo info, bool freeMode = false, bool unlockMode = false)
    {
        upgradeInfo = info;
        isFreeMode = freeMode;
        isUnlockMode = unlockMode;
        
        // 在unlock界面显示"NewUpgrade"文本
        if (newUpgradeText != null)
        {
            if (isUnlockMode)
            {
                newUpgradeText.gameObject.SetActive(true);
                newUpgradeText.text = LocalizationHelper.GetLocalizedString("NewUpgrade");
            }
            else
            {
                newUpgradeText.gameObject.SetActive(false);
            }
        }
        
        if (nameText != null)
        {
            // 从 Localization 获取升级名称
            string nameKey = "upgradeName_" + info.identifier;
            nameText.text = LocalizationHelper.GetLocalizedString(nameKey);
        }
        
        if (descText != null)
        {
            // 从 Localization 获取升级描述
            string descKey = "upgradeDesc_" + info.identifier;
            
            var descString  = LocalizationHelper.GetLocalizedString(descKey);
            if (isFreeMode)
            {
                descString += $"({LocalizationHelper.GetLocalizedString("Cost")}:{GetCurrentCost()})";
            }

            descText.text = descString;
        }
        
        UpdateCostText();
        
        UpdateBuyButton();
        
        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyClicked);
        }
    }
    
    private int GetCurrentCost()
    {
        if (upgradeInfo == null) return 0;
        int cost = upgradeInfo.cost;
        
        // Coupon: 拥有这个升级项时，商店所有物品价格减1
        if (GameManager.Instance != null && GameManager.Instance.upgradeManager != null && 
            GameManager.Instance.upgradeManager.HasUpgrade("Coupon"))
        {
            cost = Mathf.Max(0, cost - 1); // 价格不能为负
        }
        
        return cost;
    }
    
    // 更新价格文本显示
    public void UpdateCostText()
    {
        if (costText != null && upgradeInfo != null)
        {
            if (isFreeMode)
            {
                costText.text = LocalizationHelper.GetLocalizedString("PICK");
            }
            else
            {
                int displayCost = GetCurrentCost();
                string buyText = LocalizationHelper.GetLocalizedString("BUY");
                costText.text = $"{buyText} {displayCost.ToString()}";
            }
        }
    }
    
    public void UpdateBuyButton()
    {
        if (GameManager.Instance == null || upgradeInfo == null) return;
        
        if (isFreeMode)
        {
            // 免费模式：所有按钮都可以点击
            if (buyButton != null)
            {
                buyButton.interactable = true;
            }
            if (costText != null)
            {
                costText.color = new Color(0.96f,0.82f,0.45f) ;
            }
        }
        else
        {
            int currentCost = GetCurrentCost();
            bool canAfford = GameManager.Instance.mainGameData.coins >= currentCost;
            bool hasUpgrade = GameManager.Instance.mainGameData.ownedUpgrades.Contains(upgradeInfo.identifier);
            bool canBuy = canAfford && !hasUpgrade/* && GameManager.Instance.mainGameData.ownedUpgrades.Count < 5*/;
            
            if (buyButton != null)
            {
                buyButton.interactable = canBuy;
            }
            
            if (costText != null)
            {
                costText.color = canBuy ? new Color(0.96f,0.82f,0.45f) : new Color(0.78f,0.21f,0.26f);
            }
        }
    }
    
    public void OnBuyClicked()
    {
        if (GameManager.Instance == null || upgradeInfo == null) return;
        
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        if (isFreeMode)
        {
            // 免费模式：免费获得
            // 检查是否已拥有
            if (GameManager.Instance.mainGameData.ownedUpgrades.Contains(upgradeInfo.identifier))
            {
                return;
            }
            
            // 播放购买音效
            SFXManager.Instance?.PlaySFX("buyItem");
            
            GameManager.Instance.mainGameData.ownedUpgrades.Add(upgradeInfo.identifier);
            
            // 通知升级项已获得（用于处理特殊效果，如AsceticVow）
            if (GameManager.Instance.upgradeManager != null)
            {
                GameManager.Instance.upgradeManager.OnUpgradeObtained(upgradeInfo.identifier);
            }
            
            GameManager.Instance.uiManager?.UpdateUI();
            GameManager.Instance.uiManager?.UpdateUpgradeDisplay();
            
            // 禁用button并播放pop动画后隐藏
            if (buyButton != null)
            {
                buyButton.interactable = false;
            }
            PlayPopAnimationAndHide();
            
            // 通知 ShopManager 免费升级已选择
            ShopManager.Instance?.OnFreeUpgradePicked();
        }
        else
        {
            // 检查是否已拥有
            if (GameManager.Instance.mainGameData.ownedUpgrades.Contains(upgradeInfo.identifier))
            {
                return;
            }
            
            // 检查是否已满5个
            if (GameManager.Instance.mainGameData.ownedUpgrades.Count >= 5)
            {
                // 显示对话框提示
                if (DialogPanel.Instance != null)
                {
                    string limitText = LocalizationHelper.GetLocalizedString("ReachedLimitSellUpgrades");
                    DialogPanel.Instance.ShowDialog(limitText, null);
                }
                return;
            }
            
            int currentCost = GetCurrentCost();
            
            // MedicalBill: 只要有金币就可以买（至少1金币），然后消耗所有金币
            if (upgradeInfo.identifier == "MedicalBill")
            {
                if (GameManager.Instance.mainGameData.coins < 1)
                {
                    // 显示提示：金币不足
                    if (DialogPanel.Instance != null)
                    {
                        string notEnoughCoinsText = LocalizationHelper.GetLocalizedString("NotEnoughCoins");
                        DialogPanel.Instance.ShowDialog(notEnoughCoinsText, null);
                    }
                    return; // 不能购买
                }
            }
            // PaidDonation: 检查是否有至少1点血
            else if (upgradeInfo.identifier == "PaidDonation")
            {
                if (GameManager.Instance.mainGameData.health <= 1)
                {
                    // 显示提示：血量不足
                    if (DialogPanel.Instance != null)
                    {
                        string notEnoughHealthText = LocalizationHelper.GetLocalizedString("NotEnoughHealth");
                        if (string.IsNullOrEmpty(notEnoughHealthText))
                        {
                            notEnoughHealthText = "Not enough health";
                        }
                        DialogPanel.Instance.ShowDialog(notEnoughHealthText, null);
                    }
                    return; // 不能购买
                }
            }
            
            // 检查是否有足够的金币（Loan升级项购买时立刻获得5金币，所以需要检查是否有足够的金币支付成本）
            int requiredCoins = currentCost;
            if (upgradeInfo.identifier == "Loan")
            {
                // Loan: 购买时立刻获得5金币，所以实际需要的金币是 cost - 5
                requiredCoins = Mathf.Max(0, currentCost - 10);
            }
            
            if (GameManager.Instance.mainGameData.coins >= requiredCoins)
            {
                // 播放购买音效
                SFXManager.Instance?.PlaySFX("buyItem");
                
                // MedicalBill: 购买时消耗所有金币，获得1点血
                if (upgradeInfo.identifier == "MedicalBill")
                {
                    int allCoins = GameManager.Instance.mainGameData.coins;
                    GameManager.Instance.mainGameData.coins = 0;
                    GameManager.Instance.ShowFloatingText("coin", -allCoins);
                    GameManager.Instance.AddHealth(1, false);
                }
                // PaidDonation: 购买时消耗1点血，获得5金币
                else if (upgradeInfo.identifier == "PaidDonation")
                {
                    GameManager.Instance.mainGameData.health -= 1;
                    GameManager.Instance.ShowFloatingText("health", -1);
                    GameManager.Instance.mainGameData.coins += 5;
                    GameManager.Instance.ShowFloatingText("coin", 5);
                }
                // Loan: 购买时立刻获得5金币
                else if (upgradeInfo.identifier == "Loan")
                {
                    GameManager.Instance.mainGameData.coins += 10;
                    GameManager.Instance.ShowFloatingText("coin", 5);
                }
                
                // MedicalBill 不消耗金币（因为已经消耗了所有金币）
                if (upgradeInfo.identifier != "MedicalBill")
                {
                    GameManager.Instance.mainGameData.coins -= currentCost;
                    GameManager.Instance.ShowFloatingText("coin", -currentCost);
                }
                
                GameManager.Instance.mainGameData.ownedUpgrades.Add(upgradeInfo.identifier);
                
                // 标记已购买（用于Miser升级项）
                if (ShopManager.Instance != null)
                {
                    ShopManager.Instance.MarkPurchased();
                }
                
                // 通知升级项已获得（用于处理特殊效果，如AsceticVow）
                if (GameManager.Instance.upgradeManager != null)
                {
                    GameManager.Instance.upgradeManager.OnUpgradeObtained(upgradeInfo.identifier);
                }
                
                // Coupon: 购买这个升级的时候，立刻更新目前商店里所有的物品价格
                if (upgradeInfo.identifier == "Coupon")
                {
                    ShopManager.Instance?.UpdateAllShopItemPrices();
                }
                
                GameManager.Instance.uiManager?.UpdateUI();
                GameManager.Instance.uiManager?.UpdateUpgradeDisplay();
                
                // 禁用button并播放pop动画后隐藏
                if (buyButton != null)
                {
                    buyButton.interactable = false;
                }
                PlayPopAnimationAndHide();
                
                // 更新所有商店物品的按钮状态（不刷新商店）
                ShopManager.Instance?.UpdateAllBuyButtons();
            }
        }
    }
    
    // 播放pop动画后隐藏content
    private void PlayPopAnimationAndHide()
    {
        if (content == null) return;
        
        RectTransform contentRect = content.GetComponent<RectTransform>();
        if (contentRect == null) return;
        
        // 创建pop动画序列
        Sequence sequence = DOTween.Sequence();
        
        // 先放大（pop效果）
        sequence.Append(contentRect.DOScale(Vector3.one * 1.2f, 0.15f).SetEase(Ease.OutQuad));
        
        // 然后缩小并fade out
        sequence.Append(contentRect.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InQuad));
        
        // 同时fade out（如果有CanvasGroup）
        CanvasGroup canvasGroup = content.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = content.AddComponent<CanvasGroup>();
        }
        sequence.Join(canvasGroup.DOFade(0f, 0.2f));
        
        // 动画完成后隐藏
        sequence.OnComplete(() => {
            if (content != null)
            {
                content.SetActive(false);
                // 重置状态以便下次使用
                if (contentRect != null)
                {
                    contentRect.localScale = Vector3.one;
                }
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }
            }
        });
    }
}



