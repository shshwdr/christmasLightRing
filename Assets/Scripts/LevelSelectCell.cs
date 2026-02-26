using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 选关界面中一行（一个 mainScene）的 UI：图片、名称、分支进度、点击打开分支选择。
/// </summary>
public class LevelSelectCell : MonoBehaviour
{
    [Header("Display")]
    public Image sceneImage;
    public TextMeshProUGUI sceneNameText;

    [Header("Progress - 分支完成状态")]
    public ToggleObject[] progressToggles;

    [HideInInspector] public Button button;

    private string _mainScene; // 点击时传给 LevelSelectMenu 用于打开 SubLevelSelectMenu

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    /// <summary>
    /// 初始化该 cell：主图、名称（第一条的 nameIdentifier 本地化）、进度、是否可点击。
    /// </summary>
    public void Init(SceneInfo firstScene, List<SceneInfo> branches, bool canEnterMainScene)
    {
        if (firstScene == null) return;

        _mainScene = firstScene.mainScene ?? firstScene.identifier;

        // 图片：第一条 scene 的 Resources/scene/identifier
        if (sceneImage != null)
        {
            Sprite sp = Resources.Load<Sprite>("scene/" + firstScene.identifier);
            if (sp != null) sceneImage.sprite = sp;
        }

        // 名称：用第一个 sceneInfo 的 name 作为 localization key 查找显示文字
        if (sceneNameText != null)
        {
            string key = firstScene.name;
            sceneNameText.text = string.IsNullOrEmpty(key) ? key : LocalizationHelper.GetLocalizedString(key);
        }

        // 进度：只显示前 N 个 Toggle，N = 分支数；每个显示完成/未完成
        int branchCount = branches != null ? branches.Count : 0;
        var completedScenes = GameManager.Instance != null ? GameManager.Instance.gameData.completedScenes : null;

        for (int i = 0; progressToggles != null && i < progressToggles.Length; i++)
        {
            bool show = i < branchCount;
            var toggle = progressToggles[i];
            if (toggle != null)
                toggle.gameObject.SetActive(show);

            if (show && i < branchCount)
            {
                bool completed = completedScenes != null && completedScenes.Contains(branches[i].identifier);
                toggle.SetState(completed); // 完成显示 object1，否则 object2
            }
        }

        if (button != null)
        {
            button.interactable = canEnterMainScene;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnClick()
    {
        if (LevelSelectMenu.Instance != null)
            LevelSelectMenu.Instance.OnMainSceneCellClicked(_mainScene);
    }
}
