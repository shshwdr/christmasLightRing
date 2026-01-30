using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class DeckCardItem : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;
    
    private CardInfo cardInfo;
    private CardType cardType;
    private int count;
    
    public void Setup(CardInfo info, CardType type, int cardCount)
    {
        cardInfo = info;
        cardType = type;
        count = cardCount;
        
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
    }
}


















