using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Collections.Generic;
using System.Linq;

public class DeckCardItem : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;
    public Button removeButton; // 移除按钮
    
    private CardInfo cardInfo;
    private CardType cardType;
    private int count;
    private bool isRemoveMode = false;
    
    public void Setup(CardInfo info, CardType type, int cardCount, bool removeMode = false)
    {
        cardInfo = info;
        cardType = type;
        count = cardCount;
        isRemoveMode = removeMode;
        
        UpdateDisplay();
    }
    
    private void UpdateDisplay()
    {
        if (cardInfo == null) return;
        
        // 设置图标
        if (iconImage != null && CardInfoManager.Instance != null)
        {
            Sprite sprite = CardInfoManager.Instance.GetCardSprite(cardType);
            if (sprite != null)
            {
                iconImage.sprite = sprite;
            }
        }
        
        // 设置名称
        if (nameText != null)
        {
            // 从 Localization 获取卡牌名称
            string nameKey = "cardName_" + cardInfo.identifier;
            var nameLocalizedString = new LocalizedString("GameText", nameKey);
            var nameHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(nameLocalizedString.TableReference, nameLocalizedString.TableEntryReference);
            string localizedName = nameHandle.WaitForCompletion();
            nameText.text = $"{localizedName} X{count}";
        }
        
        
        // 设置描述
        if (descText != null)
        {
            // 从 Localization 获取卡牌描述
            string descKey = "cardDesc_" + cardInfo.identifier;
            var descLocalizedString = new LocalizedString("GameText", descKey);
            var descHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(descLocalizedString.TableReference, descLocalizedString.TableEntryReference);
            descText.text = descHandle.WaitForCompletion();
        }
        
        // 设置移除按钮
        if (removeButton != null)
        {
            // 只在移除模式下且canBeRemoved为true时显示移除按钮
            bool shouldShowRemoveButton = isRemoveMode && cardInfo.canBeRemoved;
            removeButton.gameObject.SetActive(shouldShowRemoveButton);
            nameText.gameObject.SetActive(!shouldShowRemoveButton);
            if (shouldShowRemoveButton)
            {
                descText.text = descText.text + $" X{count}";
                removeButton.onClick.RemoveAllListeners();
                removeButton.onClick.AddListener(OnRemoveClicked);
            }
        }
    }
    
    private void OnRemoveClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        if (GameManager.Instance == null || count <= 0) return;
        
        MainGameData mainData = GameManager.Instance.mainGameData;
        List<CardType> purchasedCards = mainData.purchasedCards;
        List<CardType> removedCards = mainData.removedCards;
        
        // 计算当前卡牌数量
        int startCount = cardInfo.start;
        int purchasedCount = purchasedCards.Count(x => x == cardType);
        int removedCount = removedCards.Count(x => x == cardType);
        int currentCount = startCount + purchasedCount - removedCount;
        
        if (currentCount <= 0) return;
        
        // 优先从purchasedCards中移除
        if (purchasedCount > 0)
        {
            int indexToRemove = purchasedCards.LastIndexOf(cardType);
            if (indexToRemove >= 0)
            {
                purchasedCards.RemoveAt(indexToRemove);
            }
        }
        else
        {
            // 如果purchasedCards中没有，但start中有，则添加到removedCards
            if (startCount > 0)
            {
                removedCards.Add(cardType);
            }
        }
        
        // 更新数量
        count--;
        
        // 如果数量为0，通知DeckMenu更新（会重新创建所有items）
        if (count <= 0)
        {
            // 通知DeckMenu更新
            if (UIManager.Instance != null && UIManager.Instance.deckMenu != null)
            {
                UIManager.Instance.deckMenu.UpdateMenu(true); // 重新更新菜单
            }
        }
        else
        {
            // 只更新当前item的显示
            UpdateDisplay();
        }
        
        // 更新UI
        GameManager.Instance.uiManager?.UpdateUI();
    }
}


















