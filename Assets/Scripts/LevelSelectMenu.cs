using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class LevelSelectMenu : MonoBehaviour
{
    public static LevelSelectMenu Instance;
    
    [Header("Menu Panel")]
    public GameObject levelSelectMenuPanel;
    public Button closeButton;
    
    [Header("Level Select Content")]
    public Transform contentParent; // GridLayout的Content Transform
    public GameObject sceneItemPrefab; // 场景项预制体（包含Image和Button）
    public GameObject finishedOb; // 完成标记GameObject（会在已完成的scene上显示）
    
    private Dictionary<string, GameObject> sceneItemObjects = new Dictionary<string, GameObject>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        if (levelSelectMenuPanel != null)
        {
            levelSelectMenuPanel.SetActive(false);
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseMenu);
        }
    }
    
    /// <summary>
    /// 打开选关菜单
    /// </summary>
    public void OpenMenu()
    {
        if (levelSelectMenuPanel != null)
        {
            levelSelectMenuPanel.SetActive(true);
            SFXManager.Instance?.PlayClickSound();
            UpdateLevelSelect();
        }
    }
    
    /// <summary>
    /// 关闭选关菜单
    /// </summary>
    public void CloseMenu()
    {
        if (levelSelectMenuPanel != null)
        {
            levelSelectMenuPanel.SetActive(false);
            //SFXManager.Instance?.PlayClickSound();
        }
    }
    
    /// <summary>
    /// 更新选关内容
    /// </summary>
    private void UpdateLevelSelect()
    {
        if (contentParent == null || sceneItemPrefab == null || CSVLoader.Instance == null)
        {
            return;
        }
        
        // 清理现有的场景项
        foreach (var kvp in sceneItemObjects)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        sceneItemObjects.Clear();
        
        // 为每个scene创建场景项
        foreach (SceneInfo sceneInfo in CSVLoader.Instance.sceneInfos)
        {
            if (string.IsNullOrEmpty(sceneInfo.identifier))
            {
                continue;
            }
            
            // 创建场景项
            GameObject sceneItemObj = Instantiate(sceneItemPrefab, contentParent);
            sceneItemObj.name = $"SceneItem_{sceneInfo.identifier}";
            
            // 设置图片（从Resources/scene/中加载）
            Image sceneImage = sceneItemObj.GetComponentInChildren<Image>();
            if (sceneImage != null)
            {
                Sprite sceneSprite = Resources.Load<Sprite>("scene/" + sceneInfo.identifier);
                if (sceneSprite != null)
                {
                    sceneImage.sprite = sceneSprite;
                }
            }
            
            // 检查是否已完成，显示finishedOb
            bool isCompleted = GameManager.Instance != null && 
                               GameManager.Instance.gameData.completedScenes.Contains(sceneInfo.identifier);
            if (isCompleted && finishedOb != null)
            {
                GameObject finishedMarker = Instantiate(finishedOb, sceneItemObj.transform);
                finishedMarker.name = "FinishedMarker";
            }
            
            // 检查是否可以进入（prev为空或已通过）
            bool canEnter = true;
            if (!string.IsNullOrEmpty(sceneInfo.prev))
            {
                canEnter = GameManager.Instance != null && 
                          GameManager.Instance.gameData.completedScenes.Contains(sceneInfo.prev);
            }
            
            // 设置按钮点击事件和可点击性
            Button sceneButton = sceneItemObj.GetComponent<Button>();
            if (sceneButton != null)
            {
                sceneButton.interactable = canEnter;
                
                string sceneIdentifier = sceneInfo.identifier; // 保存到局部变量
                sceneButton.onClick.AddListener(() => OnSceneItemClicked(sceneIdentifier));
            }
            
            sceneItemObjects[sceneInfo.identifier] = sceneItemObj;
        }
    }
    
    /// <summary>
    /// 场景项点击事件
    /// </summary>
    private void OnSceneItemClicked(string sceneIdentifier)
    {
        //SFXManager.Instance?.PlayClickSound();
        
        // 检查是否在游戏进行中（通过检查UIManager是否激活，或者是否有currentScene）
        bool isGameInProgress = IsGameInProgress();
        
        if (isGameInProgress)
        {
            // 游戏进行中，显示确认对话框
            if (DialogPanel.Instance != null)
            {
                DialogPanel.Instance.ShowDialog(
                    "SureLoadNewLevel",
                    () => OnConfirmStartScene(sceneIdentifier), // 确认回调
                    () => { } // 取消回调（只关闭对话框，不做任何事）
                );
            }
        }
        else
        {
            // 不在游戏中（从主页或设置进入），直接开始场景
            OnConfirmStartScene(sceneIdentifier);
        }
    }
    
    /// <summary>
    /// 检查是否在游戏进行中
    /// </summary>
    private bool IsGameInProgress()
    {
        // 如果存在MainMenu，说明不在游戏中
        if (FindObjectOfType<MainMenu>().mainMenuPanel.activeSelf)
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 确认开始场景
    /// </summary>
    private void OnConfirmStartScene(string sceneIdentifier)
    {
        // 清除mainGameData存档（重置到初始状态，但保留shownTutorials和readStories）
        if (GameManager.Instance != null)
        {
            // 重置数据 - 确保每次开始新游戏都清空MainGameData
            GameManager.Instance.mainGameData.Reset();
        }
        
        // 关闭选关菜单
        CloseMenu();
        
        // 隐藏主菜单
        if (MainMenu.Instance != null && MainMenu.Instance.mainMenuPanel != null)
        {
            MainMenu.Instance.mainMenuPanel.SetActive(false);
        }
        
        // 显示游戏UI
        if (UIManager.Instance != null)
        {
            UIManager.Instance.gameObject.SetActive(true);
        }
        
        // 开始该场景的游戏
        StartScene(sceneIdentifier);
    }
    
    /// <summary>
    /// 开始指定场景的游戏
    /// </summary>
    private void StartScene(string sceneIdentifier)
    {
        if (GameManager.Instance == null)
        {
            return;
        }
        
        // 设置当前场景
        GameManager.Instance.mainGameData.currentScene = sceneIdentifier;
        
        // 找到该场景的第一个关卡
        int firstLevelIndex = -1;
        for (int i = 0; i < CSVLoader.Instance.levelInfos.Count; i++)
        {
            if (CSVLoader.Instance.levelInfos[i].scene == sceneIdentifier)
            {
                firstLevelIndex = i;
                break;
            }
        }
        
        if (firstLevelIndex >= 0)
        {
            // 设置当前关卡为场景的第一个关卡（关卡编号从1开始）
            GameManager.Instance.mainGameData.currentLevel = firstLevelIndex + 1;
            GameManager.Instance.mainGameData.currentScene = sceneIdentifier;
            
            // 保存游戏数据
            GameManager.Instance.gameData.currentLevel = GameManager.Instance.mainGameData.currentLevel;
            GameManager.Instance.gameData.currentScene = GameManager.Instance.mainGameData.currentScene;
            GameManager.Instance.SaveGameData();
            
            // 开始新游戏
            GameManager.Instance.StartNewLevel();
        }
        else
        {
            Debug.LogWarning($"No levels found for scene: {sceneIdentifier}");
        }
    }
}

