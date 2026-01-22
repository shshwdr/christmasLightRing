using UnityEngine;
using UnityEngine.UI;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance;
    
    public GameObject cursorImageObject;
    public Sprite flashlightCursor;
    
    private bool isUsingCustomCursor = false;
    private bool isManuallyHidden = false; // 是否手动隐藏鼠标
    
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
        if (cursorImageObject != null)
        {
            cursorImageObject.SetActive(false);
        }
        Cursor.visible = true;
    }
    
    private void Update()
    {
        // 检测 M 键切换鼠标显示/隐藏
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleCursorVisibility();
        }
        
        if (cursorImageObject != null && isUsingCustomCursor)
        {
            Vector3 mousePos = Input.mousePosition;
            cursorImageObject.transform.position = mousePos;
        }
    }
    
    /// <summary>
    /// 切换鼠标光标的显示/隐藏状态
    /// </summary>
    private void ToggleCursorVisibility()
    {
        isManuallyHidden = !isManuallyHidden;
        UpdateCursorVisibility();
    }
    
    /// <summary>
    /// 更新鼠标光标的可见性（考虑手动隐藏和自定义光标状态）
    /// </summary>
    private void UpdateCursorVisibility()
    {
        // 如果正在使用自定义光标，系统光标应该隐藏
        // 如果手动隐藏，系统光标也应该隐藏
        Cursor.visible = !isManuallyHidden && !isUsingCustomCursor;
    }
    
    public void SetFlashlightCursor()
    {
        if (cursorImageObject != null)
        {
            Image img = cursorImageObject.GetComponent<Image>();
            if (img != null && flashlightCursor != null)
            {
                img.sprite = flashlightCursor;
                cursorImageObject.SetActive(true);
                isUsingCustomCursor = true;
                UpdateCursorVisibility(); // 使用统一的方法更新光标可见性
                
                // 播放 light 循环音效
                SFXManager.Instance?.PlayLoopSFX("lightsOn");
            }
        }
    }
    
    public void ResetCursor()
    {
        if (cursorImageObject != null)
        {
            cursorImageObject.SetActive(false);
            isUsingCustomCursor = false;
            UpdateCursorVisibility(); // 使用统一的方法更新光标可见性
            
            // 停止 light 循环音效
            SFXManager.Instance?.StopLoopSFX();
        }
    }
}

