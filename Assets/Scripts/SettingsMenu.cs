using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 设置菜单，包含全屏模式切换和音量调节
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public static SettingsMenu Instance;
    
    [Header("Menu Panel")]
    public GameObject settingsMenuPanel;
    public Button closeButton;
    
    [Header("Fullscreen Mode Buttons")]
    public Button fullscreenButton;
    public Button fullscreenWindowButton;
    public Button windowedButton;
    
    [Header("Volume Sliders")]
    public Slider sfxVolumeSlider;
    public Slider musicVolumeSlider;
    
    [Header("Volume Labels")]
    public TextMeshProUGUI sfxVolumeLabel;
    public TextMeshProUGUI musicVolumeLabel;
    
    [Header("Other Buttons")]
    public Button galleryButton; // 画廊按钮
    
    private int currentFullscreenMode = 0; // 0: Fullscreen, 1: FullscreenWindow, 2: Windowed
    
    private const int DEFAULT_WIDTH = 1280;
    private const int DEFAULT_HEIGHT = 720;
    
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
        if (settingsMenuPanel != null)
        {
            settingsMenuPanel.SetActive(false);
        }
        
        // 初始化按钮事件
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseMenu);
        }
        
        if (fullscreenButton != null)
        {
            fullscreenButton.onClick.AddListener(() => SetFullscreenMode(0));
        }
        
        if (fullscreenWindowButton != null)
        {
            fullscreenWindowButton.onClick.AddListener(() => SetFullscreenMode(1));
        }
        
        if (windowedButton != null)
        {
            windowedButton.onClick.AddListener(() => SetFullscreenMode(2));
        }
        
        // 初始化音量滑块
        if (sfxVolumeSlider != null)
        {
            float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
            sfxVolumeSlider.value = sfxVolume;
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            UpdateSFXVolumeLabel(sfxVolume);
        }
        
        if (musicVolumeSlider != null)
        {
            float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
            musicVolumeSlider.value = musicVolume;
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            UpdateMusicVolumeLabel(musicVolume);
        }
        
        // 加载保存的全屏模式设置
        currentFullscreenMode = PlayerPrefs.GetInt("FullscreenMode", 0);
        UpdateFullscreenModeButtons();
        
        // 应用保存的全屏模式
        ApplyFullscreenMode(currentFullscreenMode);
        
        // 初始化画廊按钮事件
        if (galleryButton != null)
        {
            galleryButton.onClick.AddListener(OnGalleryClicked);
        }
    }
    
    /// <summary>
    /// 打开设置菜单
    /// </summary>
    public void OpenMenu()
    {
        if (settingsMenuPanel != null)
        {
            settingsMenuPanel.SetActive(true);
            SFXManager.Instance?.PlayClickSound();
        }
    }
    
    /// <summary>
    /// 关闭设置菜单
    /// </summary>
    public void CloseMenu()
    {
        if (settingsMenuPanel != null)
        {
            settingsMenuPanel.SetActive(false);
            SFXManager.Instance?.PlayClickSound();
        }
    }
    
    /// <summary>
    /// 切换设置菜单显示状态
    /// </summary>
    public void ToggleMenu()
    {
        if (settingsMenuPanel != null)
        {
            bool isActive = settingsMenuPanel.activeSelf;
            settingsMenuPanel.SetActive(!isActive);
            SFXManager.Instance?.PlayClickSound();
        }
    }
    
    /// <summary>
    /// 设置全屏模式
    /// </summary>
    /// <param name="mode">0: Fullscreen, 1: FullscreenWindow, 2: Windowed</param>
    private void SetFullscreenMode(int mode)
    {
        if (currentFullscreenMode == mode) return;
        
        currentFullscreenMode = mode;
        SFXManager.Instance?.PlayClickSound();
        
        ApplyFullscreenMode(mode);
        UpdateFullscreenModeButtons();
        
        // 保存设置
        PlayerPrefs.SetInt("FullscreenMode", mode);
        PlayerPrefs.Save();
        
        // 保存到GameData
        if (GameManager.Instance != null)
        {
            GameManager.Instance.gameData.fullscreenMode = mode;
            GameManager.Instance.SaveGameData();
        }
        
        // 更新分辨率（可能需要重新计算黑边）
        if (ResolutionManager.Instance != null)
        {
            // 延迟一帧更新，确保分辨率已经改变
            Invoke(nameof(UpdateResolution), 0.1f);
        }
    }
    
    private void UpdateResolution()
    {
        if (ResolutionManager.Instance != null)
        {
            ResolutionManager.Instance.UpdateResolution();
        }
    }
    
    /// <summary>
    /// 应用全屏模式
    /// </summary>
    private void ApplyFullscreenMode(int mode)
    {
        switch (mode)
        {
            case 0: // Fullscreen
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.ExclusiveFullScreen);
                break;
            case 1: // FullscreenWindow
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
                break;
            case 2: // Windowed (默认分辨率)
                Screen.SetResolution(DEFAULT_WIDTH, DEFAULT_HEIGHT, FullScreenMode.Windowed);
                break;
        }
    }
    
    /// <summary>
    /// 更新全屏模式按钮的选中状态
    /// </summary>
    private void UpdateFullscreenModeButtons()
    {
        // 这里使用简单的颜色变化来表示选中状态
        // 如果需要更复杂的UI，可以使用Toggle Group或者自定义按钮状态
        
        Color selectedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        Color normalColor = Color.white;
        
        if (fullscreenButton != null)
        {
            var colors = fullscreenButton.colors;
            colors.normalColor = currentFullscreenMode == 0 ? selectedColor : normalColor;
            fullscreenButton.colors = colors;
        }
        
        if (fullscreenWindowButton != null)
        {
            var colors = fullscreenWindowButton.colors;
            colors.normalColor = currentFullscreenMode == 1 ? selectedColor : normalColor;
            fullscreenWindowButton.colors = colors;
        }
        
        if (windowedButton != null)
        {
            var colors = windowedButton.colors;
            colors.normalColor = currentFullscreenMode == 2 ? selectedColor : normalColor;
            windowedButton.colors = colors;
        }
    }
    
    /// <summary>
    /// SFX音量改变回调
    /// </summary>
    private void OnSFXVolumeChanged(float value)
    {
        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.SetVolume(value);
        }
        UpdateSFXVolumeLabel(value);
        
        // 保存到GameData
        if (GameManager.Instance != null)
        {
            GameManager.Instance.gameData.sfxVolume = value;
            GameManager.Instance.SaveGameData();
        }
    }
    
    /// <summary>
    /// 音乐音量改变回调
    /// </summary>
    private void OnMusicVolumeChanged(float value)
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SetVolume(value);
        }
        UpdateMusicVolumeLabel(value);
        
        // 保存到GameData
        if (GameManager.Instance != null)
        {
            GameManager.Instance.gameData.musicVolume = value;
            GameManager.Instance.SaveGameData();
        }
    }
    
    /// <summary>
    /// 应用加载的设置（从GameData加载后调用）
    /// </summary>
    public void ApplyLoadedSettings()
    {
        if (GameManager.Instance == null) return;
        
        // 更新滑块值
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = GameManager.Instance.gameData.sfxVolume;
            UpdateSFXVolumeLabel(GameManager.Instance.gameData.sfxVolume);
        }
        
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = GameManager.Instance.gameData.musicVolume;
            UpdateMusicVolumeLabel(GameManager.Instance.gameData.musicVolume);
        }
        
        // 更新全屏模式
        currentFullscreenMode = GameManager.Instance.gameData.fullscreenMode;
        UpdateFullscreenModeButtons();
        ApplyFullscreenMode(currentFullscreenMode);
    }
    
    /// <summary>
    /// 更新SFX音量标签
    /// </summary>
    private void UpdateSFXVolumeLabel(float value)
    {
        if (sfxVolumeLabel != null)
        {
            sfxVolumeLabel.text = $"SFX: {Mathf.RoundToInt(value * 100)}%";
        }
    }
    
    /// <summary>
    /// 更新音乐音量标签
    /// </summary>
    private void UpdateMusicVolumeLabel(float value)
    {
        if (musicVolumeLabel != null)
        {
            musicVolumeLabel.text = $"Music: {Mathf.RoundToInt(value * 100)}%";
        }
    }
    
    /// <summary>
    /// 画廊按钮点击事件
    /// </summary>
    private void OnGalleryClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 关闭设置菜单
        CloseMenu();
        
        // 打开画廊菜单
        if (GalleryMenu.Instance != null)
        {
            GalleryMenu.Instance.OpenMenu();
        }
    }
}

