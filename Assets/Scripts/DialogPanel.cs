using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogPanel : MonoBehaviour
{
    public static DialogPanel Instance;
    
    public GameObject dialogPanel;
    public TextMeshProUGUI dialogText;
    public Button continueButton;
    
    private System.Action onContinueCallback;
    
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
        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
        }
        
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }
    }
    
    public void ShowDialog(string text, System.Action onContinue = null)
    {
        if (dialogText != null)
        {
            dialogText.text = text;
        }
        
        if (dialogPanel != null)
        {
            dialogPanel.SetActive(true);
        }
        
        onContinueCallback = onContinue;
    }
    
    public void HideDialog()
    {
        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
        }
        
        onContinueCallback = null;
    }
    
    private void OnContinueClicked()
    {
        System.Action callback = onContinueCallback;
        HideDialog();
        
        // 检查GameManager是否有pendingBossCallback，如果有，说明是boss弹窗
        if (GameManager.Instance != null && GameManager.Instance.HasPendingBossCallback())
        {
            // 激活bossIcon按钮，让玩家点击bossIcon后再执行回调
            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetBossIconInteractable(true);
            }
        }
        else if (callback != null)
        {
            // 非boss弹窗，直接执行回调
            callback();
        }
    }
}


