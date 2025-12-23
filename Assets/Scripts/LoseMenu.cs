using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoseMenu : MonoBehaviour
{
    public static LoseMenu Instance;
    
    public GameObject loseMenuPanel;
    public Button restartButton;
    public Button retryBossButton; // retry boss按钮（仅在boss战失败时显示）
    
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
        
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartClicked);
        }
        
        if (retryBossButton != null)
        {
            retryBossButton.onClick.AddListener(OnRetryBossClicked);
            retryBossButton.gameObject.SetActive(false); // 初始隐藏
        }
    }
    
    public void ShowLoseMenu(bool showRetryBoss = false)
    {
        if (loseMenuPanel != null)
        {
            loseMenuPanel.SetActive(true);
        }
        
        // 如果是boss关卡失败，显示retry boss按钮
        if (retryBossButton != null)
        {
            retryBossButton.gameObject.SetActive(showRetryBoss);
        }
    }
    
    public void HideLoseMenu()
    {
        if (loseMenuPanel != null)
        {
            loseMenuPanel.SetActive(false);
        }
        
        if (retryBossButton != null)
        {
            retryBossButton.gameObject.SetActive(false);
        }
    }
    
    private void OnRestartClicked()
    {
        // 重新加载游戏
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
    private void OnRetryBossClicked()
    {
        // 调用GameManager的RetryBoss方法
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RetryBoss();
        }
        
        // 隐藏lose menu
        HideLoseMenu();
    }
}


