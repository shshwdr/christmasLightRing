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
    /// 更新分辨率，确保Canvas保持16:9宽高比，不拉伸，超出部分显示黑边
    /// Canvas的实际大小会尽可能接近1280x720的整数倍
    /// </summary>
    public void UpdateResolution()
    {
        if (mainCanvas == null || canvasScaler == null) return;
        
        int screenWidth = Screen.width;
        int screenHeight = Screen.height;
        
        // referenceResolution始终是设计分辨率（1280x720）
        // 这样Canvas会保持16:9的宽高比
        canvasScaler.referenceResolution = new Vector2(DEFAULT_WIDTH, DEFAULT_HEIGHT);
        
        // 计算设计分辨率的宽高比
        float designAspect = (float)DEFAULT_WIDTH / DEFAULT_HEIGHT; // 16:9 ≈ 1.778
        float screenAspect = (float)screenWidth / screenHeight;
        
        // 计算以宽度为基准和以高度为基准时的缩放比例
        float scaleByWidth = (float)screenWidth / DEFAULT_WIDTH;
        float scaleByHeight = (float)screenHeight / DEFAULT_HEIGHT;
        
        // 选择较小的缩放比例，确保Canvas不会超出屏幕
        // 这样Canvas会保持16:9宽高比，超出部分会显示黑边
        if (scaleByWidth < scaleByHeight)
        {
            // 以宽度为基准（matchWidthOrHeight = 0）
            // Canvas宽度会填满屏幕宽度，高度按比例缩放，上下会有黑边
            canvasScaler.matchWidthOrHeight = 0f;
            currentMultiplier = Mathf.Max(1, Mathf.FloorToInt(scaleByWidth));
        }
        else
        {
            // 以高度为基准（matchWidthOrHeight = 1）
            // Canvas高度会填满屏幕高度，宽度按比例缩放，左右会有黑边
            canvasScaler.matchWidthOrHeight = 1f;
            currentMultiplier = Mathf.Max(1, Mathf.FloorToInt(scaleByHeight));
        }
        
        // 计算Canvas的实际渲染大小（保持16:9宽高比）
        float actualScale = Mathf.Min(scaleByWidth, scaleByHeight);
        int actualCanvasWidth = Mathf.RoundToInt(DEFAULT_WIDTH * actualScale);
        int actualCanvasHeight = Mathf.RoundToInt(DEFAULT_HEIGHT * actualScale);
        
        Debug.Log($"ResolutionManager: Screen={screenWidth}x{screenHeight}, Canvas={actualCanvasWidth}x{actualCanvasHeight} (Multiplier≈{actualScale:F2}, Target={DEFAULT_WIDTH * currentMultiplier}x{DEFAULT_HEIGHT * currentMultiplier})");
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
