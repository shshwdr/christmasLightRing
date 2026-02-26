using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 分支选择菜单中的一个按钮：显示分支的 nameIdentifier 本地化文本，可点击时进入该分支场景。
/// </summary>
public class SubLevelSelectCell : MonoBehaviour
{
    public Button button;
    public TextMeshProUGUI label;

    private string _sceneIdentifier;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnClick);
    }

    /// <summary>
    /// 设置显示文本、是否可点、点击后要加载的 scene identifier。
    /// </summary>
    public void Setup(string displayName, bool interactable, string sceneIdentifier)
    {
        _sceneIdentifier = sceneIdentifier;
        if (label != null)
            label.text = displayName ?? "";
        if (button != null)
            button.interactable = interactable;
    }

    private void OnClick()
    {
        if (string.IsNullOrEmpty(_sceneIdentifier)) return;
        var sub = GetComponentInParent<SubLevelSelectMenu>();
        if (sub != null)
            sub.Close();
        if (LevelSelectMenu.Instance != null)
            LevelSelectMenu.Instance.OnConfirmStartScene(_sceneIdentifier);
    }
}
