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
            descText.text = LocalizationHelper.GetLocalizedString(descKey);
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
        int count = GetCardCount();
        return cardInfo.cost + cardInfo.costIncrease * count;
    }
    
    private void UpdateCostText()
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
                costText.color = Color.white;
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
                costText.color = canAfford ? Color.white : Color.red;
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

