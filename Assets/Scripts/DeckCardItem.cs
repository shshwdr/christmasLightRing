using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
            nameText.text = $"{cardInfo.name} X{count}";
        }
        
        
        // 设置描述
        if (descText != null)
        {
            descText.text = cardInfo.desc;
        }
    }
}












