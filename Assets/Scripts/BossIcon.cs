using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BossIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CardType bossCardType;
    private Image iconImage;
    private Button button;
    
    private void Awake()
    {
        iconImage = GetComponent<Image>();
        button = GetComponent<Button>();
        
        // 如果没有Button组件，添加一个
        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
        }
        
        // 设置按钮点击事件
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClicked);
        }
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
                FindObjectOfType<UIManager>().bossIconInteractableObject.GetComponent<Image>().sprite = sprite;
            }
        }

        // 初始状态设为不可点击
        SetInteractable(false);
    }
    
    public void SetInteractable(bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
            FindObjectOfType<UIManager>().bossIconInteractableObject.SetActive(interactable);
        }
    }
    
    private void OnButtonClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 点击bossIcon时，执行GameManager中的回调
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnBossIconClicked();
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


