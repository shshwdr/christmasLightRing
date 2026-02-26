using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

/// <summary>
/// 点击 LevelSelectCell 后弹出的分支选择菜单，在指定 transform 下显示若干 SubLevelSelectCell。
/// </summary>
public class SubLevelSelectMenu : MenuBase
{
    [Header("Sub Level Select")]
    [Tooltip("SubLevelSelectCell 所在的父节点，将按子节点顺序取前 N 个作为分支")]
    public Transform subLevelContentParent;

    private List<SubLevelSelectCell> _cells;
    private List<SceneInfo> _currentBranches;

    private void Awake()
    {
        if (menuPanel == null)
            menuPanel = gameObject;
        RefreshCells();
    }

    private void RefreshCells()
    {
        _cells = new List<SubLevelSelectCell>();
        if (subLevelContentParent != null)
        {
            var found = subLevelContentParent.GetComponentsInChildren<SubLevelSelectCell>(true);
            _cells = found.OrderBy(c => c.transform.GetSiblingIndex()).ToList();
        }
        if (_cells.Count == 0)
        {
            var fallback = FindObjectsOfType<SubLevelSelectCell>(true);
            _cells = fallback.OrderBy(c => c.transform.GetSiblingIndex()).ToList();
        }
    }

    /// <summary>
    /// 打开分支选择，显示该 mainScene 的所有分支（按 info 顺序）。
    /// </summary>
    public void Open(List<SceneInfo> branches)
    {
        if (branches == null || branches.Count == 0) return;
        _currentBranches = branches;

        if (_cells == null || _cells.Count == 0)
            RefreshCells();

        var completedScenes = GameManager.Instance != null ? GameManager.Instance.gameData.completedScenes : null;

        for (int i = 0; i < _cells.Count; i++)
        {
            bool inRange = i < branches.Count;
            _cells[i].gameObject.SetActive(inRange);
            if (inRange)
            {
                SceneInfo branch = branches[i];
                // 第一个分支可点；只要第一个分支完成了，后面分支都可点
                bool firstCompleted = completedScenes != null && branches.Count > 0 && completedScenes.Contains(branches[0].identifier);
                bool canClick = (i == 0) || firstCompleted;
                string displayName = string.IsNullOrEmpty(branch.nameIdentifier) ? branch.name : LocalizationHelper.GetLocalizedString(branch.nameIdentifier);
                _cells[i].Setup(displayName, canClick, branch.identifier);
            }
        }

        base.Open();
    }
}
