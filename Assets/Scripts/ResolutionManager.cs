using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 分辨率管理器，控制Canvas使其最终大小始终是1280x720的倍数
/// </summary>
public class ResolutionManager : Singleton<ResolutionManager>
{
    private const int DEFAULT_WIDTH = 1280;
    private const int DEFAULT_HEIGHT = 720;
    
    private Canvas mainCanvas;
    private CanvasScaler canvasScaler;
    private Camera mainCamera;
    
    // 当前使用的倍数
    private int currentMultiplier = 1;
    
    protected override void Awake()
    {
        base.Awake();
    }
    
    private void Start()
    {
        // 查找主Canvas
        mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            Debug.LogError("ResolutionManager: Main Canvas not found!");
            return;
        }
        
        canvasScaler = mainCanvas.GetComponent<CanvasScaler>();
        if (canvasScaler == null)
        {
            Debug.LogError("ResolutionManager: CanvasScaler not found on Canvas!");
            return;
        }
        
        // 确保CanvasScaler使用Scale With Screen Size模式
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(DEFAULT_WIDTH, DEFAULT_HEIGHT);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        
        // 查找主Camera
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }
        
        if (mainCamera != null)
        {
            // 设置Camera背景色为黑色（用于显示黑边）
            mainCamera.backgroundColor = Color.black;
        }
        
        // 初始设置
        UpdateResolution();
    }
    
    private void Update()
    {
        // 检测分辨率变化（仅在Windows平台）
        #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            UpdateResolution();
        }
        #endif
    }
    
    private int lastScreenWidth = 0;
    private int lastScreenHeight = 0;
    
    /// <summary>
    /// 更新分辨率，计算并设置Canvas为1280x720的整数倍
    /// </summary>
    public void UpdateResolution()
    {
        if (mainCanvas == null || canvasScaler == null) return;
        
        int screenWidth = Screen.width;
        int screenHeight = Screen.height;
        
        // 计算水平和垂直方向分别可以容纳多少倍
        int multiplierX = screenWidth / DEFAULT_WIDTH;
        int multiplierY = screenHeight / DEFAULT_HEIGHT;
        
        // 选择较小的倍数，确保Canvas不会超出屏幕
        currentMultiplier = Mathf.Max(1, Mathf.Min(multiplierX, multiplierY));
        
        // 计算Canvas的目标实际分辨率（1280x720的整数倍）
        int targetCanvasWidth = DEFAULT_WIDTH * currentMultiplier;
        int targetCanvasHeight = DEFAULT_HEIGHT * currentMultiplier;
        
        // 关键：设置referenceResolution为目标分辨率
        // 这样CanvasScaler会以这个分辨率作为参考，实际Canvas大小会接近这个值
        canvasScaler.referenceResolution = new Vector2(targetCanvasWidth, targetCanvasHeight);
        
        // 计算屏幕和目标Canvas的宽高比
        float screenAspect = (float)screenWidth / screenHeight;
        float targetAspect = (float)targetCanvasWidth / targetCanvasHeight;
        
        // 根据屏幕宽高比决定matchWidthOrHeight的值
        // 这样可以让Canvas在屏幕中正确显示，同时保持目标分辨率
        if (screenAspect > targetAspect)
        {
            // 屏幕更宽，以高度为基准（matchWidthOrHeight = 1）
            // Canvas会填满屏幕高度，左右会有黑边
            canvasScaler.matchWidthOrHeight = 1f;
        }
        else if (screenAspect < targetAspect)
        {
            // 屏幕更高，以宽度为基准（matchWidthOrHeight = 0）
            // Canvas会填满屏幕宽度，上下会有黑边
            canvasScaler.matchWidthOrHeight = 0f;
        }
        else
        {
            // 宽高比相同，使用平衡缩放
            canvasScaler.matchWidthOrHeight = 0.5f;
        }
        
        Debug.Log($"ResolutionManager: Screen={screenWidth}x{screenHeight}, Canvas={targetCanvasWidth}x{targetCanvasHeight} (Multiplier={currentMultiplier})");
    }
    
    /// <summary>
    /// 获取当前使用的倍数
    /// </summary>
    public int GetCurrentMultiplier()
    {
        return currentMultiplier;
    }
    
    /// <summary>
    /// 获取当前Canvas的实际分辨率
    /// </summary>
    public Vector2Int GetCurrentCanvasResolution()
    {
        return new Vector2Int(DEFAULT_WIDTH * currentMultiplier, DEFAULT_HEIGHT * currentMultiplier);
    }
}
