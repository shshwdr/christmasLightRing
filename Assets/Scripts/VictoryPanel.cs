using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class VictoryPanel : MonoBehaviour
{
    public static VictoryPanel Instance;
    
    public GameObject victoryPanel;
    public Button levelSelectButton; // 进入选关页面按钮
    public Button nextSceneButton; // 继续下一关按钮
    public Button mainMenuButton;
    public TextMeshProUGUI victoryText; // 胜利文本 
    
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
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
        
        if (levelSelectButton != null)
        {
            levelSelectButton.onClick.AddListener(OnLevelSelectClicked);
        }
        
        if (nextSceneButton != null)
        {
            nextSceneButton.onClick.AddListener(OnNextSceneClicked);
        }
        
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }
        
    }
    
    public void ShowVictory()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            
            // 检查是否是最后一个scene
            bool isLastScene = IsLastScene();
            
            // 检查是否是最后一个scene，如果是则隐藏继续下一关按钮
            if (nextSceneButton != null)
            {
                nextSceneButton.gameObject.SetActive(!isLastScene);
            }
            
            // 更新胜利文本
            if (victoryText != null)
            {
                if (isLastScene)
                {
                    victoryText.text = "You saved Christmas! Thanks for playing!";
                }
                else
                {
                    victoryText.text = "You successfully passed this level! The adventure continues!";
                }
            }
        }
    }
    
    public void HideVictory()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
    }
    
    /// <summary>
    /// 检查是否是最后一个scene
    /// </summary>
    private bool IsLastScene()
    {
        if (GameManager.Instance == null || CSVLoader.Instance == null)
        {
            return false;
        }
        
        string currentScene = GameManager.Instance.mainGameData.currentScene;
        if (string.IsNullOrEmpty(currentScene) || CSVLoader.Instance.sceneInfos.Count == 0)
        {
            return false;
        }
        
        // 找到当前scene在列表中的位置
        int currentSceneIndex = -1;
        for (int i = 0; i < CSVLoader.Instance.sceneInfos.Count; i++)
        {
            if (CSVLoader.Instance.sceneInfos[i].identifier == currentScene)
            {
                currentSceneIndex = i;
                break;
            }
        }
        
        // 如果是最后一个scene，返回true
        return currentSceneIndex >= 0 && currentSceneIndex == CSVLoader.Instance.sceneInfos.Count - 1;
    }
    
    private void OnLevelSelectClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 隐藏胜利面板
        HideVictory();
        
        // 隐藏游戏UI
        if (UIManager.Instance != null)
        {
            UIManager.Instance.gameObject.SetActive(false);
        }
        
        // 显示主菜单
        if (MainMenu.Instance != null && MainMenu.Instance.mainMenuPanel != null)
        {
            MainMenu.Instance.mainMenuPanel.SetActive(true);
        }
        
        // 打开选关菜单
        if (LevelSelectMenu.Instance != null)
        {
            LevelSelectMenu.Instance.OpenMenu();
        }
    }
    
    private void OnNextSceneClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 隐藏胜利面板
        HideVictory();
        
        // 加载下一个scene
        LoadNextScene();
    }
    
    /// <summary>
    /// 加载下一个scene
    /// </summary>
    private void LoadNextScene()
    {
        if (GameManager.Instance == null || CSVLoader.Instance == null)
        {
            return;
        }
        
        string currentScene = GameManager.Instance.mainGameData.currentScene;
        if (string.IsNullOrEmpty(currentScene))
        {
            return;
        }
        
        // 找到当前scene在列表中的位置
        int currentSceneIndex = -1;
        for (int i = 0; i < CSVLoader.Instance.sceneInfos.Count; i++)
        {
            if (CSVLoader.Instance.sceneInfos[i].identifier == currentScene)
            {
                currentSceneIndex = i;
                break;
            }
        }
        
        // 如果找到下一个scene
        if (currentSceneIndex >= 0 && currentSceneIndex < CSVLoader.Instance.sceneInfos.Count - 1)
        {
            string nextSceneIdentifier = CSVLoader.Instance.sceneInfos[currentSceneIndex + 1].identifier;
            
            // 设置当前场景
            GameManager.Instance.mainGameData.currentScene = nextSceneIdentifier;
            
            // 找到该场景的第一个关卡
            int firstLevelIndex = -1;
            for (int i = 0; i < CSVLoader.Instance.levelInfos.Count; i++)
            {
                if (CSVLoader.Instance.levelInfos[i].scene == nextSceneIdentifier)
                {
                    firstLevelIndex = i;
                    break;
                }
            }
            
            if (firstLevelIndex >= 0)
            {
                // 设置当前关卡为场景的第一个关卡（关卡编号从1开始）
                GameManager.Instance.mainGameData.currentLevel = firstLevelIndex + 1;
                
                // 开始新游戏
                GameManager.Instance.StartNewLevel();
            }
        }
    }
    
    /// <summary>
    /// 主菜单按钮点击事件
    /// </summary>
    private void OnMainMenuClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 隐藏胜利面板
        HideVictory();
        
        // 调用SettingsMenu的OnBackToMainMenuClicked（它会检查是否是最后一关）
        if (SettingsMenu.Instance != null)
        {
            SettingsMenu.Instance.OnBackToMainMenuClicked();
        }
    }
}



