using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 菜单基类：统一管理 panel 显示/隐藏与关闭按钮。
/// </summary>
public abstract class MenuBase : MonoBehaviour
{
    [Header("Menu Panel")]
    [Tooltip("不指定则使用本 GameObject 作为 panel")]
    public GameObject menuPanel;
    public Button closeButton;

    protected GameObject Panel => menuPanel != null ? menuPanel : gameObject;

    protected virtual void Start()
    {
        Panel.SetActive(false);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    public virtual void Open()
    {
        Panel.SetActive(true);
    }

    public virtual void Close()
    {
        Panel.SetActive(false);
    }
}
