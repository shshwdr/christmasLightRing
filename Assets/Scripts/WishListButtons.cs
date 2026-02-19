using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;

/// <summary>
/// 管理愿望单按钮组，根据语言显示不同的按钮
/// 中文时显示QQ群按钮，其他语言显示Discord按钮
/// </summary>
public class WishListButtons : MonoBehaviour
{
    [Header("Button References")]
    [Tooltip("QQ群按钮（中文时显示）")]
    public GameObject qqButton;
    
    [Tooltip("Discord按钮（非中文时显示）")]
    public GameObject discordButton;
    
    private void Start()
    {
        UpdateButtonVisibility();
        
        // 监听语言变化事件
        LocalizationSettings.SelectedLocaleChanged += OnLanguageChanged;
    }
    
    private void OnDestroy()
    {
        // 取消监听语言变化事件
        LocalizationSettings.SelectedLocaleChanged -= OnLanguageChanged;
    }
    
    /// <summary>
    /// 语言变化时的回调
    /// </summary>
    private void OnLanguageChanged(UnityEngine.Localization.Locale locale)
    {
        UpdateButtonVisibility();
    }
    
    /// <summary>
    /// 根据当前语言更新按钮显示状态
    /// </summary>
    private void UpdateButtonVisibility()
    {
        bool isChinese = IsChineseLanguage();
        
        // 显示/隐藏QQ按钮
        if (qqButton != null)
        {
            qqButton.SetActive(isChinese);
        }
        
        // 显示/隐藏Discord按钮
        if (discordButton != null)
        {
            discordButton.SetActive(!isChinese);
        }
    }
    
    /// <summary>
    /// 检查当前语言是否为中文
    /// </summary>
    private bool IsChineseLanguage()
    {
        if (LocalizationSettings.SelectedLocale != null)
        {
            string languageCode = LocalizationSettings.SelectedLocale.Identifier.Code;
            return languageCode == "zh-Hans" || languageCode == "zh-Hant";
        }
        return false;
    }
}




