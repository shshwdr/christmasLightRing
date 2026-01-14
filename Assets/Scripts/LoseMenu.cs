using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoseMenu : MonoBehaviour
{
    public static LoseMenu Instance;
    
    public GameObject loseMenuPanel;
    public Button mainMenuButton;
    public Button retryBossButton; // retry按钮（保持名称以兼容场景，但行为改为retry level）
    
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
        if (loseMenuPanel != null)
        {
            loseMenuPanel.SetActive(false);
        }
        
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(FindObjectOfType<SettingsMenu>().OnConfirmBackToMainMenu);
        }
        
        if (retryBossButton != null)
        {
            retryBossButton.onClick.AddListener(FindObjectOfType<SettingsMenu>().OnConfirmRestartLevel);
        }
    }
    
    public void ShowLoseMenu(bool showRetry = true)
    {
        if (loseMenuPanel != null)
        {
            loseMenuPanel.SetActive(true);
        }
        
        // 始终显示retry按钮
        // if (retryBossButton != null)
        // {
        //     retryBossButton.gameObject.SetActive(showRetry);
        // }
    }
    
    public void HideLoseMenu()
    {
        if (loseMenuPanel != null)
        {
            loseMenuPanel.SetActive(false);
        }
        
        // if (retryBossButton != null)
        // {
        //     retryBossButton.gameObject.SetActive(false);
        // }
    }
    
    private void OnRestartClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 重新加载游戏
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
    private void OnRetryClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 调用GameManager的RetryLevel方法（回到level开始的状态）
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RetryLevel();
        }
        
        // 隐藏lose menu
        HideLoseMenu();
    }
}



