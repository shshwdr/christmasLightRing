using UnityEngine;
using UnityEngine.UI;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance;
    
    public GameObject cursorImageObject;
    public Sprite flashlightCursor;
    
    private bool isUsingCustomCursor = false;
    
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
        if (cursorImageObject != null && isUsingCustomCursor)
        {
            Vector3 mousePos = Input.mousePosition;
            cursorImageObject.transform.position = mousePos;
        }
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
                Cursor.visible = false;
                
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
            Cursor.visible = true;
            
            // 停止 light 循环音效
            SFXManager.Instance?.StopLoopSFX();
        }
    }
}

