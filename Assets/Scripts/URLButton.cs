using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 超链接按钮，点击后打开指定的URL地址
/// </summary>
[RequireComponent(typeof(Button))]
public class URLButton : MonoBehaviour
{
    [Header("URL Settings")]
    [Tooltip("要打开的URL地址（例如：https://www.example.com）")]
    public string url = "";
    
    private Button button;
    
    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClicked);
        }
    }
    
    /// <summary>
    /// 按钮点击事件
    /// </summary>
    private void OnButtonClicked()
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogWarning($"URLButton: URL is empty on {gameObject.name}");
            return;
        }
        
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 打开URL
        Application.OpenURL(url);
    }
    
    /// <summary>
    /// 设置URL地址（可以在运行时动态设置）
    /// </summary>
    public void SetURL(string newUrl)
    {
        url = newUrl;
    }
}












