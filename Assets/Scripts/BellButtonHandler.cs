using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BellButtonHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowDescText("Click bell to enter next level");
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






