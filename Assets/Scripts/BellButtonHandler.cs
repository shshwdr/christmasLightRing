using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Localization;

public class BellButtonHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UIManager.Instance != null)
        {
            var localizedString = new LocalizedString("GameText", "Click bell to enter next level");
            UIManager.Instance.ShowDescText(localizedString.GetLocalizedString());
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







