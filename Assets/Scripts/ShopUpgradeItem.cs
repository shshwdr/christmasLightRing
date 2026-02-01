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
    
    private UpgradeInfo upgradeInfo;
    private bool isFreeMode = false;
    
    public void Setup(UpgradeInfo info, bool freeMode = false)
    {
        upgradeInfo = info;
        isFreeMode = freeMode;
        
        if (nameText != null)
        {
            // 从 Localization 获取升级名称
            string nameKey = "upgradeName_" + info.identifier;
            var nameLocalizedString = new LocalizedString("GameText", nameKey);
            var nameHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(nameLocalizedString.TableReference, nameLocalizedString.TableEntryReference);
            nameText.text = nameHandle.WaitForCompletion();
        }
        
        if (descText != null)
        {
            // 从 Localization 获取升级描述
            string descKey = "upgradeDesc_" + info.identifier;
            var descLocalizedString = new LocalizedString("GameText", descKey);
            var descHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(descLocalizedString.TableReference, descLocalizedString.TableEntryReference);
            descText.text = descHandle.WaitForCompletion();
        }
        
        if (costText != null)
        {
            if (isFreeMode)
            {
                // 使用 Localization
                var pickLocalizedString = new LocalizedString("GameText", "PICK");
                var pickHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(pickLocalizedString.TableReference, pickLocalizedString.TableEntryReference);
                costText.text = pickHandle.WaitForCompletion();
            }
            else
            {
                // 从 Localization 获取"BUY"字符串
                var buyLocalizedString = new LocalizedString("GameText", "BUY");
                var buyHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(buyLocalizedString.TableReference, buyLocalizedString.TableEntryReference);
                string buyText = buyHandle.WaitForCompletion();
                costText.text = $"{buyText} {info.cost.ToString()}";
            }
        }
        
        UpdateBuyButton();
        
        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyClicked);
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
                costText.color = Color.white;
            }
        }
        else
        {
            bool canAfford = GameManager.Instance.mainGameData.coins >= upgradeInfo.cost;
            bool hasUpgrade = GameManager.Instance.mainGameData.ownedUpgrades.Contains(upgradeInfo.identifier);
            bool canBuy = canAfford && !hasUpgrade/* && GameManager.Instance.mainGameData.ownedUpgrades.Count < 5*/;
            
            if (buyButton != null)
            {
                buyButton.interactable = canBuy;
            }
            
            if (costText != null)
            {
                costText.color = canBuy ? Color.white : Color.red;
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
                    // 使用 Localization
                    var limitLocalizedString = new LocalizedString("GameText", "ReachedLimitSellUpgrades");
                    var limitHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(limitLocalizedString.TableReference, limitLocalizedString.TableEntryReference);
                    string limitText = limitHandle.WaitForCompletion();
                    DialogPanel.Instance.ShowDialog(limitText, null);
                }
                return;
            }
            
            // 检查是否有足够的金币
            if (GameManager.Instance.mainGameData.coins >= upgradeInfo.cost)
            {
                // 播放购买音效
                SFXManager.Instance?.PlaySFX("buyItem");
                
                GameManager.Instance.mainGameData.coins -= upgradeInfo.cost;
                GameManager.Instance.ShowFloatingText("coin", -upgradeInfo.cost);
                GameManager.Instance.mainGameData.ownedUpgrades.Add(upgradeInfo.identifier);
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



