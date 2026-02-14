using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class ShopItem : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI costText;
    public Button buyButton;
    public GameObject content; // content的GameObject，购买后隐藏
    public TextMeshProUGUI newCardText; // 显示"NewCard"的文本，仅在unlock界面显示
    
    private CardInfo cardInfo;
    private bool isFreeMode = false;
    private bool isUnlockMode = false;
    private bool isDamageDiscountFree = false; // damageDiscount升级项：标记此商品是否免费
    
    public void Setup(CardInfo info, bool freeMode = false, bool unlockMode = false)
    {
        cardInfo = info;
        isFreeMode = freeMode;
        isUnlockMode = unlockMode;
        
        // 在unlock界面显示"NewCard"文本
        if (newCardText != null)
        {
            if (isUnlockMode)
            {
                newCardText.gameObject.SetActive(true);
                newCardText.text = LocalizationHelper.GetLocalizedString("NewCard");
            }
            else
            {
                newCardText.gameObject.SetActive(false);
            }
        }
        
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
            // 从 Localization 获取卡牌名称
            string nameKey = "cardName_" + info.identifier;
            nameText.text = LocalizationHelper.GetLocalizedString(nameKey);
        }
        
        if (descText != null)
        {
            // 从 Localization 获取卡牌描述
            string descKey = "cardDesc_" + info.identifier;
            var descString  = LocalizationHelper.GetLocalizedString(descKey);
            if (isFreeMode)
            {
                descString += $"({LocalizationHelper.GetLocalizedString("Cost")}:{GetCurrentCost()})";
            }

            descText.text = descString;
        }
        
        UpdateCostText();
        UpdateBuyButton();
        
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyClicked);
    }
    
    private int GetCardCount()
    {
        if (GameManager.Instance == null || cardInfo == null) return 0;
        
        CardType cardType = CardInfoManager.Instance.GetCardType(cardInfo.identifier);
        int count = 0;
        foreach (CardType purchasedType in GameManager.Instance.mainGameData.purchasedCards)
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
        
        // damageDiscount: 如果此商品被标记为免费，价格为0
        if (isDamageDiscountFree) return 0;
        
        int count = GetCardCount();
        int cost = cardInfo.cost + cardInfo.costIncrease * count;
        
        // Coupon: 拥有这个升级项时，商店所有物品价格减1
        if (GameManager.Instance != null && GameManager.Instance.upgradeManager != null && 
            GameManager.Instance.upgradeManager.HasUpgrade("Coupon"))
        {
            cost = Mathf.Max(0, cost - 1); // 价格不能为负
        }
        
        return cost;
    }
    
    // damageDiscount: 设置此商品为免费
    public void SetDamageDiscountFree()
    {
        isDamageDiscountFree = true;
        UpdateCostText();
        UpdateBuyButton();
        // 播放升级项触发音效
        SFXManager.Instance?.PlaySFX("buyItem");
    }
    
    public void UpdateCostText()
    {
        if (costText != null && cardInfo != null)
        {
            if (isFreeMode)
            {
                costText.text = LocalizationHelper.GetLocalizedString("PICK");
            }
            else
            {
                int currentCost = GetCurrentCost();
                string buyText = LocalizationHelper.GetLocalizedString("BUY");
                costText.text = $"{buyText} {currentCost.ToString()}";
            }
        }
    }
    
    public void UpdateBuyButton()
    {
        if (GameManager.Instance == null || cardInfo == null) return;
        
        if (isFreeMode)
        {
            // 免费模式：所有按钮都可以点击
            if (buyButton != null)
            {
                buyButton.interactable = true;
            }
            if (costText != null)
            {
                costText.color =new Color(0.96f,0.82f,0.45f);
            }
        }
        else
        {
            int currentCost = GetCurrentCost();
            bool canAfford = GameManager.Instance.mainGameData.coins >= currentCost;
            
            if (buyButton != null)
            {
                buyButton.interactable = canAfford;
            }
            
            if (costText != null)
            {
                costText.color = canAfford ? new Color(0.96f,0.82f,0.45f) : new Color(0.78f,0.21f,0.26f);
            }
        }
    }
    
    public void OnBuyClicked()
    {
        if (GameManager.Instance == null || cardInfo == null) return;
        
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        if (isFreeMode)
        {
            // 免费模式：免费获得，但仍需增加后续价格
            CardType cardType = CardInfoManager.Instance.GetCardType(cardInfo.identifier);
            GameManager.Instance.mainGameData.purchasedCards.Add(cardType);
            GameManager.Instance.uiManager?.UpdateUI();
            
            // 播放购买音效
            SFXManager.Instance?.PlaySFX("buyItem");
            
            // 创建卡牌飞向deckButton的动效
            CreateCardFlyToDeckButton();
            
            // 禁用button并播放pop动画后隐藏
            if (buyButton != null)
            {
                buyButton.interactable = false;
            }
            PlayPopAnimationAndHide();
            
            // 通知 ShopManager 免费物品已选择
            ShopManager.Instance?.OnFreeItemPicked();
        }
        else
        {
            int currentCost = GetCurrentCost();
            if (GameManager.Instance.mainGameData.coins >= currentCost)
            {
                // 播放购买音效
                SFXManager.Instance?.PlaySFX("buyItem");
                
                GameManager.Instance.mainGameData.coins -= currentCost;
                GameManager.Instance.ShowFloatingText("coin", -currentCost);
                CardType cardType = CardInfoManager.Instance.GetCardType(cardInfo.identifier);
                GameManager.Instance.mainGameData.purchasedCards.Add(cardType);
                GameManager.Instance.uiManager?.UpdateUI();
                
                // 创建卡牌飞向deckButton的动效
                CreateCardFlyToDeckButton();
                
                // 标记已购买（用于Miser升级项）
                if (ShopManager.Instance != null)
                {
                    ShopManager.Instance.MarkPurchased();
                }
                
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
    
    // 创建卡牌飞向deckButton的动效
    private void CreateCardFlyToDeckButton()
    {
        if (iconImage == null || iconImage.sprite == null) return;
        if (GameManager.Instance == null || GameManager.Instance.uiManager == null) return;
        if (GameManager.Instance.uiManager.deckButton == null) return;
        
        // 获取目标位置（deckButton的位置）
        RectTransform targetRect = GameManager.Instance.uiManager.deckButton.GetComponent<RectTransform>();
        if (targetRect == null) return;
        
        // 获取Canvas
        Canvas canvas = GameManager.Instance.canvas;
        if (canvas == null) return;
        
        // 创建新的GameObject用于飞行
        GameObject flyObj = new GameObject("CardFlyToDeckEffect");
        flyObj.transform.SetParent(canvas.transform, false);
        
        // 添加RectTransform
        RectTransform flyRect = flyObj.AddComponent<RectTransform>();
        flyRect.sizeDelta = new Vector2(100, 100); // 固定长宽为100
        
        // 添加Image组件并复制sprite
        Image flyImage = flyObj.AddComponent<Image>();
        flyImage.sprite = iconImage.sprite;
        flyImage.preserveAspect = true;
        flyImage.color = iconImage.color; // 复制颜色
        
        // 设置层级，确保在最上层显示
        flyObj.transform.SetAsLastSibling();
        
        // 添加CardFlyEffect组件
        CardFlyEffect flyEffect = flyObj.AddComponent<CardFlyEffect>();
        
        // 获取起始位置（iconImage的世界坐标）
        Vector3 startPos = iconImage.rectTransform.position;
        
        // 获取目标位置（deckButton的世界坐标）
        Vector3 targetPos = targetRect.position;
        
        // 触发飞行动画
        flyEffect.FlyToTarget(startPos, targetPos);
    }
}

