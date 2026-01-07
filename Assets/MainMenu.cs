using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public static MainMenu Instance;
    
    [Header("Menu Panel")]
    public GameObject mainMenuPanel;
    
    [Header("Menu Buttons")]
    public Button startGameButton;
    public Button settingsButton;
    public Button galleryButton;
    
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
        // 确保主菜单初始显示
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }
        
        // 隐藏游戏UI（如果存在）- 延迟一帧执行，确保UIManager已经初始化
        StartCoroutine(HideUIManagerDelayed());
        
        // 初始化按钮事件
        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(OnStartGameClicked);
        }
        
        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }
        
        if (galleryButton != null)
        {
            galleryButton.onClick.AddListener(OnGalleryClicked);
        }
    }
    
    private System.Collections.IEnumerator HideUIManagerDelayed()
    {
        yield return null; // 等待一帧
        
        // 隐藏游戏UI（如果存在）
        if (UIManager.Instance != null)
        {
            UIManager.Instance.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// 开始游戏按钮点击事件
    /// </summary>
    private void OnStartGameClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 隐藏主菜单
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
        }
        
        // 显示游戏UI
        if (UIManager.Instance != null)
        {
            UIManager.Instance.gameObject.SetActive(true);
        }
        
        // 开始游戏逻辑（GameManager会在Start时自动加载存档数据）
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartNewLevel();
        }
    }
    
    /// <summary>
    /// 设置按钮点击事件
    /// </summary>
    private void OnSettingsClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 打开设置菜单
        if (SettingsMenu.Instance != null)
        {
            SettingsMenu.Instance.OpenMenu();
        }
    }
    
    /// <summary>
    /// 画廊按钮点击事件
    /// </summary>
    private void OnGalleryClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 打开画廊菜单
        if (GalleryMenu.Instance != null)
        {
            GalleryMenu.Instance.OpenMenu();
        }
    }
}
