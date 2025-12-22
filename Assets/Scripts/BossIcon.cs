using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BossIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CardType bossCardType;
    private Image iconImage;
    
    private void Awake()
    {
        iconImage = GetComponent<Image>();
    }
    
    public void Setup(CardType cardType)
    {
        bossCardType = cardType;
        
        // 加载boss图片
        if (iconImage != null && CardInfoManager.Instance != null)
        {
            Sprite sprite = CardInfoManager.Instance.GetCardSprite(cardType);
            if (sprite != null)
            {
                iconImage.sprite = sprite;
            }
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        // hover时显示desc，和hover在对应的boss tile上一样
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowDesc(bossCardType);
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        // 离开时隐藏desc
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideDesc();
        }
    }
}

