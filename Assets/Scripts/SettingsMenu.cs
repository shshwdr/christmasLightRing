using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Collections;

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

    public Transform hideInMainMenu;
    
    [Header("Other Buttons")]
    public Button galleryButton; // 画廊按钮
    public Button clearSaveDataButton; // 清除存档按钮
    public Button backToMainMenuButton; // 回到主菜单按钮
    public Button selectLevelButton; // 选择关卡按钮
    public Button restartLevelButton; // 重新开始关卡按钮
    
    [Header("Language Buttons")]
    public Button chineseButton; // 中文按钮
    public Button englishButton; // 英文按钮
    
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
        
        // 初始化清除存档按钮事件
        if (clearSaveDataButton != null)
        {
            clearSaveDataButton.onClick.AddListener(OnClearSaveDataClicked);
        }
        
        // 初始化回到主菜单按钮事件
        if (backToMainMenuButton != null)
        {
            backToMainMenuButton.onClick.AddListener(OnBackToMainMenuClicked);
        }
        
        // 初始化选择关卡按钮事件
        if (selectLevelButton != null)
        {
            selectLevelButton.onClick.AddListener(OnSelectLevelClicked);
        }
        
        // 初始化重新开始关卡按钮事件
        if (restartLevelButton != null)
        {
            restartLevelButton.onClick.AddListener(OnRestartLevelClicked);
        }
        
        // 初始化语言切换按钮事件
        if (chineseButton != null)
        {
            chineseButton.onClick.AddListener(() => OnLanguageButtonClicked("zh-Hans"));
        }
        
        if (englishButton != null)
        {
            englishButton.onClick.AddListener(() => OnLanguageButtonClicked("en"));
        }
        
        // 更新语言按钮状态
        UpdateLanguageButtons();
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

            if (FindObjectOfType<MainMenu>().mainMenuPanel.activeSelf)
            {
                hideInMainMenu.gameObject.SetActive(false);
            }
            else
            {
                hideInMainMenu.gameObject.SetActive(true);
                
            }
            
            // 更新语言按钮状态
            UpdateLanguageButtons();
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
            //sfxVolumeLabel.text = $"SFX: {Mathf.RoundToInt(value * 100)}%";
        }
    }
    
    /// <summary>
    /// 更新音乐音量标签
    /// </summary>
    private void UpdateMusicVolumeLabel(float value)
    {
        if (musicVolumeLabel != null)
        {
            //musicVolumeLabel.text = $"Music: {Mathf.RoundToInt(value * 100)}%";
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
    
    /// <summary>
    /// 清除存档按钮点击事件
    /// </summary>
    private void OnClearSaveDataClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 显示确认对话框
        if (DialogPanel.Instance != null)
        {
            DialogPanel.Instance.ShowDialog(
                "SureClearData",
                OnConfirmClearSaveData, // 确认回调
                null // 取消回调（直接关闭对话框）
            );
        }
    }
    
    /// <summary>
    /// 确认清除存档
    /// </summary>
    private void OnConfirmClearSaveData()
    {
        // 清除所有存档数据
        if (DataManager.Instance != null)
        {
            DataManager.Instance.ClearAllSaveData();
            
            // 显示清除成功提示
            if (DialogPanel.Instance != null)
            {
                // 使用 Localization
                var clearedLocalizedString = new LocalizedString("GameText", "AllSaveDataCleared");
                var clearedHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(clearedLocalizedString.TableReference, clearedLocalizedString.TableEntryReference);
                string clearedText = clearedHandle.WaitForCompletion();
                DialogPanel.Instance.ShowDialog(clearedText, null);
            }
            
            Debug.Log("All save data cleared successfully.");
        }
    }
    
    /// <summary>
    /// 回到主菜单按钮点击事件
    /// </summary>
    public void OnBackToMainMenuClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 检查是否是最后一个scene（从VictoryPanel调用时）
        bool isLastScene = IsLastScene();
        
        if (isLastScene)
        {
            // 如果是最后一关，直接返回主菜单，不弹窗
            OnConfirmBackToMainMenu();
        }
        else
        {
            // 显示确认对话框
            if (DialogPanel.Instance != null)
            {
                DialogPanel.Instance.ShowDialog(
                    "SureReturnMainMenu",
                    OnConfirmBackToMainMenu, // 确认回调
                    () => { } // 取消回调（只关闭对话框，不做任何事）
                );
            }
        }
    }
    
    /// <summary>
    /// 检查是否是最后一个scene（用于判断是否需要弹窗）
    /// </summary>
    private bool IsLastScene()
    {
        if (GameManager.Instance == null || CSVLoader.Instance == null)
        {
            return false;
        }
        
        string currentScene = GameManager.Instance.mainGameData.currentScene;
        if (string.IsNullOrEmpty(currentScene) || CSVLoader.Instance.sceneInfos.Count == 0)
        {
            return false;
        }
        
        // 找到当前scene在列表中的位置
        int currentSceneIndex = -1;
        for (int i = 0; i < CSVLoader.Instance.sceneInfos.Count; i++)
        {
            if (CSVLoader.Instance.sceneInfos[i].identifier == currentScene)
            {
                currentSceneIndex = i;
                break;
            }
        }
        
        // 如果是最后一个scene，返回true
        return currentSceneIndex >= 0 && currentSceneIndex == CSVLoader.Instance.sceneInfos.Count - 1;
    }
    
    /// <summary>
    /// 确认回到主菜单
    /// </summary>
    public void OnConfirmBackToMainMenu()
    {
        // 清除mainGameData存档（重置到初始状态）
        if (GameManager.Instance != null)
        {
            GameManager.Instance.mainGameData.Reset();
            
            // 保存游戏数据（清除进度）
            GameManager.Instance.gameData.currentLevel = 1;
            GameManager.Instance.gameData.currentScene = "";
            GameManager.Instance.SaveGameData();
        }
        
        // 关闭设置菜单
        CloseMenu();
        
        // 隐藏游戏UI
        if (UIManager.Instance != null)
        {
            UIManager.Instance.gameObject.SetActive(false);
        }
        
        // 显示主菜单
        if (MainMenu.Instance != null && MainMenu.Instance.mainMenuPanel != null)
        {
            MainMenu.Instance.mainMenuPanel.SetActive(true);
        }
    }
    
    /// <summary>
    /// 选择关卡按钮点击事件
    /// </summary>
    private void OnSelectLevelClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 关闭设置菜单
        CloseMenu();
        
        // 打开选关菜单
        if (LevelSelectMenu.Instance != null)
        {
            LevelSelectMenu.Instance.OpenMenu();
        }
    }
    
    /// <summary>
    /// 重新开始关卡按钮点击事件
    /// </summary>
    private void OnRestartLevelClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 显示确认对话框
        if (DialogPanel.Instance != null)
        {
            DialogPanel.Instance.ShowDialog(
                "SureRestartLevel",
                OnConfirmRestartLevel, // 确认回调
                () => { } // 取消回调（只关闭对话框，不做任何事）
            );
        }
    }
    
    /// <summary>
    /// 确认重新开始关卡（重新开始整个scene）
    /// </summary>
    public void OnConfirmRestartLevel()
    {
        if (GameManager.Instance == null || CSVLoader.Instance == null)
        {
            return;
        }
        
        // 获取当前scene
        string currentScene = GameManager.Instance.mainGameData.currentScene;
        if (string.IsNullOrEmpty(currentScene))
        {
            return;
        }
        
        // 找到该scene的第一个关卡
        int firstLevelIndex = -1;
        for (int i = 0; i < CSVLoader.Instance.levelInfos.Count; i++)
        {
            if (CSVLoader.Instance.levelInfos[i].scene == currentScene)
            {
                firstLevelIndex = i;
                break;
            }
        }
        
        if (firstLevelIndex >= 0)
        {
            // 重置mainGameData（但保留shownTutorials和readStories）
            MainGameData mainData = GameManager.Instance.mainGameData;
            GameData gameData = GameManager.Instance.gameData;
            int savedCoins = mainData.coins;
            int savedGifts = mainData.gifts;
            int savedHealth = mainData.health;
            int savedFlashlights = mainData.flashlights;
            List<string> savedShownTutorials = new List<string>(gameData.shownTutorials);
            List<string> savedReadStories = new List<string>(gameData.readStories);
            List<CardType> savedPurchasedCards = new List<CardType>(mainData.purchasedCards);
            List<string> savedOwnedUpgrades = new List<string>(mainData.ownedUpgrades);
            
            // 重置数据
            mainData.Reset();
            
            // 恢复教程和故事数据（这些不会被清除，保存在gameData中）
            mainData.purchasedCards = savedPurchasedCards;
            mainData.ownedUpgrades = savedOwnedUpgrades;
            
            // 设置当前关卡为scene的第一个关卡（关卡编号从1开始）
            mainData.currentLevel = firstLevelIndex + 1;
            mainData.currentScene = currentScene;
            
            // 保存游戏数据
            GameManager.Instance.gameData.currentLevel = mainData.currentLevel;
            GameManager.Instance.gameData.currentScene = mainData.currentScene;
            GameManager.Instance.SaveGameData();
            
            // 关闭设置菜单
            CloseMenu();
            
            // 重新开始关卡
            GameManager.Instance.StartNewLevel();
        }
    }
    
    /// <summary>
    /// 检查游戏是否已开始
    /// </summary>
    private bool IsGameStarted()
    {
        if (GameManager.Instance == null) return false;
        return GameManager.Instance.mainGameData.currentLevel > 0 || 
               !string.IsNullOrEmpty(GameManager.Instance.mainGameData.currentScene);
    }

    public bool IsGameEntered()
    {
        return !FindObjectOfType<MainMenu>().mainMenuPanel.activeSelf;
    }
    
    /// <summary>
    /// 语言切换按钮点击事件
    /// </summary>
    private void OnLanguageButtonClicked(string languageCode)
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 获取当前语言
        string currentLanguage = GetCurrentLanguage();
        
        // 如果与当前语言相同，不响应
        if (currentLanguage == languageCode)
        {
            return;
        }
        
        // 检查游戏是否已开始
        bool gameStarted = IsGameEntered();
        
        if (gameStarted)
        {
            // 显示确认对话框
            if (DialogPanel.Instance != null)
            {
                DialogPanel.Instance.ShowDialog(
                    "SureSwitchLanguage",
                    () => OnConfirmLanguageSwitch(languageCode), // 确认回调
                    () => { } // 取消回调（只关闭对话框）
                );
            }
        }
        else
        {
            // 游戏未开始，直接切换
            SwitchLanguage(languageCode);
        }
    }
    
    /// <summary>
    /// 确认切换语言
    /// </summary>
    private void OnConfirmLanguageSwitch(string languageCode)
    {
        SwitchLanguage(languageCode);
    }
    
    /// <summary>
    /// 切换语言并刷新游戏
    /// </summary>
    private void SwitchLanguage(string languageCode)
    {
        // 保存语言设置到 PlayerPrefs
        PlayerPrefs.SetString("GameLanguage", languageCode);
        PlayerPrefs.Save();
        
        // 切换语言
        StartCoroutine(SwitchLanguageCoroutine(languageCode));
    }
    
    /// <summary>
    /// 切换语言的协程
    /// </summary>
    private IEnumerator SwitchLanguageCoroutine(string languageCode)
    {
        // 等待 Localization 系统初始化
        yield return LocalizationSettings.InitializationOperation;
        
        // 获取所有可用的语言
        var availableLocales = LocalizationSettings.AvailableLocales.Locales;
        Locale targetLocale = null;
        
        foreach (var locale in availableLocales)
        {
            if (locale.Identifier.Code == languageCode)
            {
                targetLocale = locale;
                break;
            }
        }
        
        if (targetLocale != null)
        {
            // 设置语言
            LocalizationSettings.SelectedLocale = targetLocale;
            
            // 等待语言切换完成
            yield return new WaitForSeconds(0.1f);
            
            // 更新按钮状态
            UpdateLanguageButtons();
            
            // 如果游戏已开始，刷新游戏
            if (IsGameStarted() && GameManager.Instance != null)
            {
                // 关闭设置菜单
                CloseMenu();
                GameManager.Instance.restartGame();
                // 重新开始当前关卡
                //GameManager.Instance.StartNewLevel();
                
            }
        }
    }
    
    /// <summary>
    /// 获取当前语言代码
    /// </summary>
    private string GetCurrentLanguage()
    {
        if (LocalizationSettings.SelectedLocale != null)
        {
            return LocalizationSettings.SelectedLocale.Identifier.Code;
        }
        return "en"; // 默认英文
    }
    
    /// <summary>
    /// 更新语言按钮的选中状态
    /// </summary>
    private void UpdateLanguageButtons()
    {
        string currentLanguage = GetCurrentLanguage();
        
        Color selectedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        Color normalColor = Color.white;
        
        if (chineseButton != null)
        {
            var colors = chineseButton.colors;
            colors.normalColor = currentLanguage == "zh-Hans" ? selectedColor : normalColor;
            chineseButton.colors = colors;
        }
        
        if (englishButton != null)
        {
            var colors = englishButton.colors;
            colors.normalColor = currentLanguage == "en" ? selectedColor : normalColor;
            englishButton.colors = colors;
        }
    }
}

