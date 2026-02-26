using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class LevelSelectMenu : MonoBehaviour
{
    public static LevelSelectMenu Instance;

    [Header("Menu Panel")]
    public GameObject levelSelectMenuPanel;
    public Button closeButton;

    [Header("Level Select Content")]
    public Transform contentParent;
    public GameObject sceneItemPrefab; // LevelSelectCell 预制体
    public SubLevelSelectMenu subLevelSelectMenu; // 点击 cell 后弹出的分支选择

    private Dictionary<string, GameObject> sceneItemObjects = new Dictionary<string, GameObject>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        if (levelSelectMenuPanel != null)
            levelSelectMenuPanel.SetActive(false);
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseMenu);
    }

    public void OpenMenu()
    {
        if (levelSelectMenuPanel != null)
        {
            levelSelectMenuPanel.SetActive(true);
            SFXManager.Instance?.PlayClickSound();
            UpdateLevelSelect();
        }
    }

    public void CloseMenu()
    {
        if (levelSelectMenuPanel != null)
            levelSelectMenuPanel.SetActive(false);
        if (subLevelSelectMenu != null)
            subLevelSelectMenu.Close();
    }

    /// <summary>
    /// 只显示每个 mainScene 的第一条 scene，用 LevelSelectCell 初始化。
    /// </summary>
    private void UpdateLevelSelect()
    {
        if (contentParent == null || sceneItemPrefab == null || CSVLoader.Instance == null)
            return;

        foreach (var kvp in sceneItemObjects)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        sceneItemObjects.Clear();

        List<SceneInfo> firstScenes = GetFirstScenePerMainScene();
        var completedScenes = GameManager.Instance != null ? GameManager.Instance.gameData.completedScenes : null;

        foreach (SceneInfo firstScene in firstScenes)
        {
            if (string.IsNullOrEmpty(firstScene.identifier))
                continue;

            GameObject go = Instantiate(sceneItemPrefab, contentParent);
            go.name = $"LevelSelectCell_{firstScene.mainScene}";
            var cell = go.GetComponent<LevelSelectCell>();
            if (cell == null)
            {
                Debug.LogWarning("LevelSelectMenu: sceneItemPrefab 需要挂载 LevelSelectCell 组件。");
                continue;
            }

            List<SceneInfo> branches = GetBranchesForMainScene(firstScene.mainScene);
            bool canEnter = true;
            if (!string.IsNullOrEmpty(firstScene.prev) && completedScenes != null)
                canEnter = completedScenes.Contains(firstScene.prev);

            cell.Init(firstScene, branches, canEnter);
            sceneItemObjects[firstScene.mainScene ?? firstScene.identifier] = go;
        }
    }

    /// <summary>
    /// 每个 mainScene 只取 CSV 中第一次出现的那条 SceneInfo。
    /// </summary>
    private List<SceneInfo> GetFirstScenePerMainScene()
    {
        var list = new List<SceneInfo>();
        var seen = new HashSet<string>();
        foreach (SceneInfo s in CSVLoader.Instance.sceneInfos)
        {
            string key = s.mainScene ?? s.identifier;
            if (string.IsNullOrEmpty(key)) continue;
            if (seen.Contains(key)) continue;
            seen.Add(key);
            list.Add(s);
        }
        return list;
    }

    /// <summary>
    /// 按 CSV 顺序返回同一 mainScene 的所有分支。
    /// </summary>
    private List<SceneInfo> GetBranchesForMainScene(string mainScene)
    {
        if (string.IsNullOrEmpty(mainScene)) return new List<SceneInfo>();
        return CSVLoader.Instance.sceneInfos.Where(s => s.mainScene == mainScene).ToList();
    }

    /// <summary>
    /// 点击了某个 mainScene 的 LevelSelectCell，弹出分支选择。
    /// </summary>
    public void OnMainSceneCellClicked(string mainScene)
    {
        if (subLevelSelectMenu == null) return;
        List<SceneInfo> branches = GetBranchesForMainScene(mainScene);
        if (branches == null || branches.Count == 0) return;
        subLevelSelectMenu.Open(branches);
    }

    /// <summary>
    /// 供 SubLevelSelectMenu 调用：关闭子菜单并开始指定场景（分支）。
    /// </summary>
    public void OnConfirmStartScene(string sceneIdentifier)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.mainGameData.Reset();

        CloseMenu();
        if (subLevelSelectMenu != null)
            subLevelSelectMenu.Close();

        if (MainMenu.Instance != null && MainMenu.Instance.mainMenuPanel != null)
            MainMenu.Instance.mainMenuPanel.SetActive(false);
        if (UIManager.Instance != null)
            UIManager.Instance.gameObject.SetActive(true);

        StartScene(sceneIdentifier);
    }

    private void StartScene(string sceneIdentifier)
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.mainGameData.currentScene = sceneIdentifier;
        string levelKey = LevelManager.Instance != null ? LevelManager.Instance.GetSceneKeyForLevels(sceneIdentifier) : sceneIdentifier;
        int firstLevelIndex = -1;
        for (int i = 0; i < CSVLoader.Instance.levelInfos.Count; i++)
        {
            if (CSVLoader.Instance.levelInfos[i].scene == levelKey)
            {
                firstLevelIndex = i;
                break;
            }
        }

        if (firstLevelIndex >= 0)
        {
            GameManager.Instance.mainGameData.currentLevel = firstLevelIndex + 1;
            GameManager.Instance.mainGameData.currentScene = sceneIdentifier;
            GameManager.Instance.gameData.currentLevel = GameManager.Instance.mainGameData.currentLevel;
            GameManager.Instance.gameData.currentScene = GameManager.Instance.mainGameData.currentScene;
            GameManager.Instance.SaveGameData();
            GameManager.Instance.StartNewLevel();
        }
        else
        {
            Debug.LogWarning($"No levels found for scene: {sceneIdentifier}");
        }
    }
}
