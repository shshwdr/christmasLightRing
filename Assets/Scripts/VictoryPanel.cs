using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class VictoryPanel : MonoBehaviour
{
    public static VictoryPanel Instance;
    
    public GameObject victoryPanel;
    public Button restartButton;
    
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
        
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartClicked);
        }
    }
    
    public void ShowVictory()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }
    }
    
    public void HideVictory()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
    }
    
    private void OnRestartClicked()
    {
        // 重新加载游戏
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}



