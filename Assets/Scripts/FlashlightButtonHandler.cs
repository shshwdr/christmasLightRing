using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FlashlightButtonHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    private Button button;
    private bool isDragging = false;
    
    private void Awake()
    {
        button = GetComponent<Button>();
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        // 如果按钮不可交互，不允许拖拽
        if (button == null || !button.interactable)
        {
            return;
        }
        
        // 如果还没有激活flashlight，先激活它
        if (GameManager.Instance != null && !GameManager.Instance.IsUsingFlashlight())
        {
            GameManager.Instance.UseFlashlight();
        }
        
        isDragging = true;
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        // 拖拽过程中保持flashlight激活状态
        // 不需要额外操作，因为UseFlashlight已经激活了状态
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        
        // 检查是否拖拽到了Tile上
        if (eventData.pointerCurrentRaycast.gameObject != null)
        {
            Tile tile = eventData.pointerCurrentRaycast.gameObject.GetComponent<Tile>();
            if (tile != null)
            {
                // Tile的OnDrop会处理这个
                // 这里不需要额外操作，因为Tile会调用UseFlashlightToReveal
            }
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowDescText("Drag light to tiles that may have enemies to dazzle them");
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideDesc();
        }
    }
}






