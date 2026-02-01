using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class DialogPanel : MonoBehaviour
{
    public static DialogPanel Instance;
    
    public GameObject dialogPanel;
    public TextMeshProUGUI dialogText;
    public Button continueButton;
    public Button cancelButton; // 第二个按钮（可选）
    
    private System.Action onContinueCallback;
    private System.Action onCancelCallback;
    
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
        
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
            cancelButton.gameObject.SetActive(false); // 默认隐藏第二个按钮
        }
    }
    
    // 单个按钮的ShowDialog（保持向后兼容）
    public void ShowDialog(string text, System.Action onContinue = null, bool revealCardsBeforeShowing = false,bool requireLocalization = false)
    {
        // 如果指定要reveal卡牌，在显示对话前先reveal所有未翻开的卡牌
        if (revealCardsBeforeShowing && GameManager.Instance != null && GameManager.Instance.boardManager != null)
        {
            GameManager.Instance.RevealAllCardsBeforeLeaving(() =>
            {
                ShowDialogInternal(text, onContinue, null,requireLocalization);
            });
        }
        else
        {
            ShowDialogInternal(text, onContinue, null,requireLocalization);
        }
    }
    
    // 两个按钮的ShowDialog（新增）
    public void ShowDialog(string text, System.Action onContinue, System.Action onCancel, bool revealCardsBeforeShowing = false,bool requireLocalization = true)
    {
        
        // 如果指定要reveal卡牌，在显示对话前先reveal所有未翻开的卡牌
        if (revealCardsBeforeShowing && GameManager.Instance != null && GameManager.Instance.boardManager != null)
        {
            GameManager.Instance.RevealAllCardsBeforeLeaving(() =>
            {
                ShowDialogInternal(text, onContinue, onCancel,requireLocalization);
            });
        }
        else
        {
            ShowDialogInternal(text, onContinue, onCancel,requireLocalization);
        }
    }
    
    private void ShowDialogInternal(string text, System.Action onContinue = null, System.Action onCancel = null,bool requireLocalization = false)
    {
        // 播放弹窗音效
        SFXManager.Instance?.PlaySFX("popup");
        
        if (dialogText != null)
        {
            if (requireLocalization)
            {
                
                var dialogLocalizedText = new LocalizedString("GameText", text);
                var dialogLocalizedHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(dialogLocalizedText.TableReference, dialogLocalizedText.TableEntryReference);
                text = dialogLocalizedHandle.WaitForCompletion();
            }
            
            text = text.Replace("\\n", "\n");
            dialogText.text = text;
        }
        
        if (dialogPanel != null)
        {
            dialogPanel.SetActive(true);
        }
        
        onContinueCallback = onContinue;
        onCancelCallback = onCancel;
        
        // 根据是否有第二个回调来显示/隐藏第二个按钮
        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(onCancel != null);
        }
    }
    
    public void HideDialog()
    {
        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
        }
        
        onContinueCallback = null;
        onCancelCallback = null;
        
        // 隐藏第二个按钮
        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(false);
        }
    }
    
    private void OnContinueClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
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
    
    private void OnCancelClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        System.Action callback = onCancelCallback;
        HideDialog();
        
        // 执行取消回调
        if (callback != null)
        {
            callback();
        }
    }
}


