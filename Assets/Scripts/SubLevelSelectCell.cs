using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 分支选择菜单中的一个按钮：显示分支的 nameIdentifier 本地化文本与 desc，可点击时进入该分支场景。
/// </summary>
public class SubLevelSelectCell : MonoBehaviour
{
    public Button button;
    public TextMeshProUGUI label;
    [Tooltip("分支描述，从 localization 读取 branch.nameIdentifier + \"_desc\"")]
    public TextMeshProUGUI desc;

    private string _sceneIdentifier;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnClick);
    }

    /// <summary>
    /// 使用 SceneInfo 初始化：在内部从 localization 读取 nameIdentifier 与 nameIdentifier+"_desc" 并显示。
    /// </summary>
    public void Setup(SceneInfo branch, bool interactable)
    {
        if (branch == null) return;
        _sceneIdentifier = branch.identifier;
        string displayName = string.IsNullOrEmpty(branch.nameIdentifier) ? branch.name : LocalizationHelper.GetLocalizedString(branch.nameIdentifier);
        if (label != null)
            label.text = displayName ?? "";
        if (desc != null)
        {
            string descKey = string.IsNullOrEmpty(branch.nameIdentifier) ? "" : (branch.nameIdentifier + "_desc");
            desc.text = string.IsNullOrEmpty(descKey) ? "" : LocalizationHelper.GetLocalizedString(descKey);
            UpgradeInfo rewardUpgrade = GetFirstUpgradeForScene(branch.identifier);
            if (rewardUpgrade != null)
            {
                string rewardLabel = LocalizationHelper.GetLocalizedString("rewardText");
                string upgradeDesc = LocalizationHelper.GetLocalizedString("upgradeName_" + rewardUpgrade.identifier);
                desc.text += "\n\n" + rewardLabel + upgradeDesc;
            }
            var completedScenes = GameManager.Instance != null ? GameManager.Instance.gameData.completedScenes : null;
            if (completedScenes != null && completedScenes.Contains(branch.identifier))
                desc.text += "\n" + LocalizationHelper.GetLocalizedString("CompleteText");
        }
        if (button != null)
            button.interactable = interactable;
    }

    /// <summary>
    /// 仅用于展示当前分支信息（如 upgradeUI 中）：只设置名称与描述，不响应点击切关。
    /// </summary>
    public void SetupDisplayOnly(SceneInfo sceneInfo)
    {
        if (sceneInfo == null) return;
        if (label != null)
            label.text = string.IsNullOrEmpty(sceneInfo.nameIdentifier) ? sceneInfo.name : LocalizationHelper.GetLocalizedString(sceneInfo.nameIdentifier);
        if (desc != null)
        {
            string descKey = string.IsNullOrEmpty(sceneInfo.nameIdentifier) ? "" : (sceneInfo.nameIdentifier + "_desc");
            desc.text = string.IsNullOrEmpty(descKey) ? "" : LocalizationHelper.GetLocalizedString(descKey);
        }
        if (button != null)
            button.gameObject.SetActive(false);
    }

    /// <summary>
    /// 从 CSVLoader 的 upgradeDict 中返回第一个 scene 等于指定 identifier 的升级项。
    /// </summary>
    private static UpgradeInfo GetFirstUpgradeForScene(string sceneIdentifier)
    {
        if (CSVLoader.Instance == null || string.IsNullOrEmpty(sceneIdentifier)) return null;
        foreach (var kvp in CSVLoader.Instance.upgradeDict)
        {
            UpgradeInfo info = kvp.Value;
            if (info != null && !string.IsNullOrEmpty(info.scene) && info.scene == sceneIdentifier)
                return info;
        }
        return null;
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
