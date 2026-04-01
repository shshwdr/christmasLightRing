using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public bool isCheat = false;
    public BoardManager boardManager;
    public UIManager uiManager;
    public ShopManager shopManager;
    public UpgradeManager upgradeManager;
    public TutorialManager tutorialManager;
    public StoryManager storyManager;
    public Canvas canvas;
    
    public GameData gameData = new GameData();
    public MainGameData mainGameData = new MainGameData(); // 主游戏数据（仅保存在内存中，不序列化）
    public int initialHealth = 3;
    public int initialFlashlights = 0;
    
    [Header("寒冰场景 (frozen)")]
    [Tooltip("本关翻开的寒冰格子数超过此值后，每次翻开（含提灯）都会扣血")]
    public int frozenDamageThreshold = 6;
    
    private bool isUsingFlashlight = false;
    private bool isFlashlightRevealing = false; // 标记正在使用手电筒翻开
    private bool isChurchRingRevealing = false; // 标记正在使用churchRing效果翻开
    private Vector2Int currentHintPosition = new Vector2Int(-1, -1);
    private bool isPlayerInputDisabled = false; // 标记是否禁用玩家点击
    private System.Action pendingBossCallback = null; // 待执行的boss回调

    // 变色龙变身：用于标记“当前这次翻牌结算来自变色龙产物”。
    // 需求：变色龙变成的敌人不攻击、不眩晕，贴图保持原样。
    private bool isChameleonEnemyRevealPending = false;
    private int chameleonEnemyRevealRow = -1;
    private int chameleonEnemyRevealCol = -1;
    
    // Boss战斗状态
    private int nunDoorCount = 0; // nun boss已翻开的门数量
    private int snowmanLightCount = 0; // snowman boss被light照射的次数
    private int snowsnakeLightCount = 0; // snowsnake boss被light照射的次数
    private int horriblemanCatchCount = 0; // horribleman boss被捕获的次数
    private int shadowCatchCount = 0; // shadow boss被捕获的次数
    
    /// <summary> 本关已翻开的寒冰格子数量，超过 frozenDamageThreshold 后每次翻开扣血 </summary>
    private int frozenRevealedCount = 0;
    /// <summary> frozenNew：上一张玩家手动翻开的格子是否为寒冰格子（自动翻开不计入） </summary>
    private bool lastManualRevealWasFrozenNewTile = false;
    /// <summary> forget：记录上一次翻开的提示格，用于在翻开下一格时隐藏其 hintText。 </summary>
    private Vector2Int lastForgetHintTilePosition = new Vector2Int(-1, -1);
    
    public int GetFrozenRevealedCount() => frozenRevealedCount;
    
    public Vector2Int GetFrozenPatchSize()
    {
        if (boardManager == null) return Vector2Int.zero;
        return boardManager.GetFrozenPatchSize();
    }
    
    /// <summary> 竞速模式：本关倒计时剩余秒数 </summary>
    private float speedCountdownRemaining = 0f;
    /// <summary> 竞速模式：本关倒计时总秒数（基础 + 每格×格子数，见 ComputeSpeedModeCountdownSeconds） </summary>
    private float speedCountdownTotal = 0f;
    /// <summary> 竞速模式：玩家翻开第一个 tile 后才开始倒计时 </summary>
    private bool speedCountdownStarted = false;
    
    /// <summary> 开局是否待显示模式提示弹窗（在免费商店结束后触发） </summary>
    private bool pendingStartModePopup = false;
    /// <summary> 当前是否正在播放开局 story（用于将模式弹窗延后到 story 之后） </summary>
    private bool isOpeningStoryPlaying = false;
    /// <summary> 若免费商店在 story 期间结束，则在 story 后补弹模式提示 </summary>
    private bool pendingModePopupAfterStory = false;
    
    /// <summary> 作弊：V 键切换场上卡牌底色半透明（透视未翻开内容）</summary>
    private bool cheatCardBackgroundPeekActive = false;
    
    // 保存bell卡信息，用于boss关卡结束后恢复
    private CardInfo bellCardInfo = null;
    
    // 全屏透明按钮（用于reveal动画后等待玩家点击）
    private GameObject fullscreenClickButton = null;



    public bool alwaysShowUnlock = true;
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

        // 确保DataManager存在（在Awake中创建，确保在Start之前初始化）
        EnsureDataManager();

        // 初始化语言设置（在Awake中同步初始化，避免延迟）
        InitializeLanguage();

        CSVLoader.Instance.Init();
        boardManager = FindObjectOfType<BoardManager>();
        uiManager = FindObjectOfType<UIManager>();
        shopManager = FindObjectOfType<ShopManager>();
        upgradeManager = FindObjectOfType<UpgradeManager>();
        tutorialManager = FindObjectOfType<TutorialManager>();
        storyManager = FindObjectOfType<StoryManager>();
        
        if (upgradeManager == null)
        {
            GameObject upgradeManagerObj = new GameObject("UpgradeManager");
            upgradeManager = upgradeManagerObj.AddComponent<UpgradeManager>();
        }
        
        if (tutorialManager == null)
        {
            GameObject tutorialManagerObj = new GameObject("TutorialManager");
            tutorialManager = tutorialManagerObj.AddComponent<TutorialManager>();
        }
        
        if (storyManager == null)
        {
            GameObject storyManagerObj = new GameObject("StoryManager");
            storyManager = storyManagerObj.AddComponent<StoryManager>();
        }
    }
    
    private void Start()
    {
        // 加载游戏数据（延迟一帧，确保所有Manager都已初始化）
        StartCoroutine(LoadGameDataDelayed());
        
        // 不在这里自动开始游戏，等待MainMenu的"开始游戏"按钮触发
        // StartNewLevel();
    }
    
    /// <summary>
    /// 初始化语言设置（在Awake中调用，同步初始化）
    /// </summary>
    private void InitializeLanguage()
    {
        // 检查 PlayerPrefs 中是否已保存语言设置
        string savedLanguage = PlayerPrefs.GetString("GameLanguage", "");
        
        if (string.IsNullOrEmpty(savedLanguage))
        {
            // 如果没有保存的语言，读取系统语言
            SystemLanguage systemLanguage = Application.systemLanguage;
            
            // 根据系统语言设置
            if (systemLanguage == SystemLanguage.Chinese || 
                systemLanguage == SystemLanguage.ChineseSimplified || 
                systemLanguage == SystemLanguage.ChineseTraditional)
            {
                savedLanguage = "zh-Hans";
            }
            else
            {
                // 其他语言默认使用英文
                savedLanguage = "en";
            }
            
            // 保存到 PlayerPrefs
            PlayerPrefs.SetString("GameLanguage", savedLanguage);
            PlayerPrefs.Save();
        }
        
        // 同步初始化 Localization 系统（如果还未初始化）
        if (!LocalizationSettings.InitializationOperation.IsDone)
        {
            LocalizationSettings.InitializationOperation.WaitForCompletion();
        }
        
        // 获取所有可用的语言
        var availableLocales = LocalizationSettings.AvailableLocales.Locales;
        Locale targetLocale = null;
        
        foreach (var locale in availableLocales)
        {
            if (locale.Identifier.Code == savedLanguage)
            {
                targetLocale = locale;
                break;
            }
        }
        
        if (targetLocale != null)
        {
            // 设置语言
            LocalizationSettings.SelectedLocale = targetLocale;
        }
    }
    
    private System.Collections.IEnumerator LoadGameDataDelayed()
    {
        yield return null; // 等待一帧，确保所有Manager都已初始化
        
        // 加载游戏数据
        LoadGameData();
    }
    
    /// <summary>
    /// 确保DataManager存在
    /// </summary>
    private void EnsureDataManager()
    {
        if (DataManager.Instance == null)
        {
            GameObject dataManagerObj = new GameObject("DataManager");
            dataManagerObj.AddComponent<DataManager>();
        }
    }
    
    /// <summary>
    /// 加载游戏数据
    /// </summary>
    private void LoadGameData()
    {
        if (DataManager.Instance != null)
        {
            Debug.Log("Loading game data on game start...");
            DataManager.Instance.LoadGameData();
        }
        else
        {
            Debug.LogWarning("DataManager.Instance is null, cannot load game data.");
        }
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // 应用暂停时保存数据
            //SaveGameData();
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            // 应用失去焦点时保存数据
            //SaveGameData();
        }
    }
    
    private void OnApplicationQuit()
    {
        // 应用退出时保存数据
        //SaveGameData();
    }
    
    /// <summary>
    /// 保存游戏数据
    /// </summary>
    public void SaveGameData()
    {
        if (DataManager.Instance != null)
        {
            Debug.Log("Saving game data...");
            DataManager.Instance.SaveGameData();
        }
        else
        {
            Debug.LogWarning("DataManager.Instance is null, cannot save game data. Creating DataManager...");
            EnsureDataManager();
            if (DataManager.Instance != null)
            {
                DataManager.Instance.SaveGameData();
            }
        }
    }
    
    private void Update()
    {
        // 检测点击空白区域，退出手电筒状态
        if (isUsingFlashlight && Input.GetMouseButtonDown(0))
        {
            // 检查是否点击在UI元素上
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                // 检查点击的是否是tile
                PointerEventData pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };
                
                var results = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);
                
                bool clickedOnTile = false;
                foreach (var result in results)
                {
                    if (result.gameObject.GetComponent<Tile>() != null)
                    {
                        clickedOnTile = true;
                        break;
                    }
                }
                
                // 如果点击的不是tile，退出手电筒状态
                if (!clickedOnTile)
                {
                    CancelFlashlight();
                }
            }
            else
            {
                // 点击在屏幕外，退出手电筒状态
                CancelFlashlight();
            }
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            // 仅将当前棋盘恢复为未翻开（保留与 GenerateBoard 一致的初始翻开：玩家、教堂），不重载关卡或场景
            CancelFlashlight();
            isFlashlightRevealing = false;
            isChurchRingRevealing = false;
            currentHintPosition = new Vector2Int(-1, -1);
            isPlayerInputDisabled = false;
            pendingBossCallback = null;
            
            boardManager?.ResetAllTilesUnrevealedThenInitialRevealsOnly();
            
            SceneInfo sceneInfoR = GetCurrentSceneInfo();
            if (sceneInfoR != null && (sceneInfoR.HasType("frozen") || sceneInfoR.HasType("frozenToDamageMax")) && boardManager != null)
            {
                frozenRevealedCount = 1 + boardManager.GetInitialRevealedFrozenCount();
                boardManager.UpdatePlayerFrozenDataText();
            }
            else
            {
                frozenRevealedCount = 0;
            }
            lastManualRevealWasFrozenNewTile = false;
            
            mainGameData.patternRecognitionSequence = 0;
            mainGameData.isFirstTileRevealedThisTurn = false;
            mainGameData.churchLightUsedThisLevel = false;
            mainGameData.hasTriggeredEnemyThisLevel = false;
            mainGameData.GetCompletedRows().Clear();
            
            if (sceneInfoR != null && sceneInfoR.HasType("speed") && LevelManager.Instance != null)
            {
                LevelInfo li = LevelManager.Instance.GetCurrentLevelInfo();
                int cellCount = li.row * li.col;
                float totalSec = ComputeSpeedModeCountdownSeconds(sceneInfoR, cellCount);
                speedCountdownTotal = totalSec > 0f ? totalSec : 0f;
                speedCountdownRemaining = speedCountdownTotal;
                speedCountdownStarted = false;
            }
            
            boardManager?.UpdatePlayerProgressBarVisibility();
            uiManager?.UpdateUI();
            uiManager?.UpdateEnemyCount();
            uiManager?.UpdateHintCount();
            boardManager?.UpdateAllTilesVisual();
        }
        
        // 竞速模式：仅在翻牌场景倒计时，商店/弹窗/胜利/失败时不倒计时
        if (IsSpeedCountdownActive())
        {
            speedCountdownRemaining -= Time.deltaTime;
            if (boardManager != null)
            {
                Vector2Int playerPos = boardManager.GetPlayerPosition();
                if (playerPos.x >= 0)
                {
                    Tile playerTile = boardManager.GetTile(playerPos.x, playerPos.y);
                    if (playerTile != null && playerTile.progressBar != null && speedCountdownTotal > 0)
                        playerTile.progressBar.SetProgress(speedCountdownRemaining / speedCountdownTotal);
                }
            }
            if (speedCountdownRemaining <= 0f)
            {
                TakeDamage(1);
                speedCountdownRemaining = speedCountdownTotal;
            }
        }

        // 作弊功能
        if (Input.GetKeyDown(KeyCode.P)  && GameManager.Instance.isCheat)
        {
            // P键：直接胜利本关
            EndTurn();
        }
        
        if (Input.GetKeyDown(KeyCode.T) && GameManager.Instance.isCheat)
        {
            // T键：增加一滴血
            mainGameData.health++;
            ShowFloatingTextForResource("health", 1);
            uiManager?.UpdateUI();
            CheckAndUpdateShake();
        }
        
        if (Input.GetKeyDown(KeyCode.Y) && GameManager.Instance.isCheat)
        {
            // Y键：减少一滴血
            mainGameData.health--;
            ShowFloatingTextForResource("health", -1);
            uiManager?.UpdateUI();
            CheckAndTriggerShake();
            
            // Amulet: 在血量归零进入失败结算前，恢复一点血并不进入结算，并且把这个升级项丢弃
            if (mainGameData.health <= 0 && upgradeManager != null && upgradeManager.HasUpgrade("Amulet"))
            {
                mainGameData.health = 1; // 恢复1点血
                ShowFloatingText("health", 1);
                uiManager?.UpdateUI();
                
                // 丢弃Amulet升级项（等同于卖掉，但不获得金币）
                mainGameData.ownedUpgrades.Remove("Amulet");
                uiManager?.UpdateUpgradeDisplay();
                uiManager?.TriggerUpgradeAnimation("Amulet");
            }
            // BloodMoney: 在护身符之后检查，如果血量<=0且金币>=10，消耗10金币获得1点血
            if (mainGameData.health <= 0 && upgradeManager != null && upgradeManager.HasUpgrade("BloodMoney"))
            {
                if (mainGameData.coins >= 10)
                {
                    mainGameData.coins -= 10;
                    mainGameData.health = 1; // 恢复1点血
                    ShowFloatingText("coin", -10);
                    ShowFloatingText("health", 1);
                    uiManager?.UpdateUI();
                    uiManager?.TriggerUpgradeAnimation("BloodMoney");
                }
                else
                {
                    GameOver();
                }
            }
            else if (mainGameData.health <= 0)
            {
                GameOver();
            }
        }
        
        if (Input.GetKeyDown(KeyCode.U) && GameManager.Instance.isCheat)
        {
            // U键：增加一个light
            mainGameData.flashlights++;
            ShowFloatingTextForResource("light", 1);
            uiManager?.UpdateUI();
        }
        
        if (Input.GetKeyDown(KeyCode.Q) && GameManager.Instance.isCheat)
        {
            // Q键：增加5个dollar
            mainGameData.coins += 5;
            ShowFloatingTextForResource("coin", 5);
            uiManager?.UpdateUI();
        }
        
        if (Input.GetKeyDown(KeyCode.W) && GameManager.Instance.isCheat)
        {
            // W键：增加5个gift
            mainGameData.gifts += 5;
            ShowFloatingTextForResource("gift", 5);
            uiManager?.UpdateUI();
        }

        if (Input.GetKeyDown(KeyCode.F) && GameManager.Instance.isCheat)
        {
            // F键：强制触发铃铛按钮逻辑（不依赖是否存在铃铛/按钮）
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OnBellButtonClicked();
            }
            else
            {
                SFXManager.Instance?.PlayClickSound();
                StopSpeedCountdown();
                TutorialManager.Instance?.ShowTutorial("nextLevel");
                RevealAllCardsBeforeLeaving(() =>
                {
                    EndTurn();
                });
            }
        }
        
        if (Input.GetKeyDown(KeyCode.E) && GameManager.Instance.isCheat)
        {
            // E键：跳到当前场景的最后一关，并给予20个light和20滴血
            if (string.IsNullOrEmpty(mainGameData.currentScene) || LevelManager.Instance == null)
                return;
            var indices = LevelManager.Instance.GetLevelIndicesForScene(mainGameData.currentScene);
            if (indices == null || indices.Count == 0)
                return;
            int lastLevelIndex = indices[indices.Count - 1];
            mainGameData.currentLevel = lastLevelIndex + 1;
            StartNewLevel();
            mainGameData.health = 20;
            mainGameData.maxHealth = 20;
            mainGameData.flashlights = 20;
            uiManager?.UpdateUI();
            CheckAndUpdateShake();
        }
        
        if (Input.GetKeyDown(KeyCode.V) && GameManager.Instance.isCheat)
        {
            // V键：场上卡牌 background 半透明；若 background 未显示则对 revealableImage 半透明（再按恢复）
            cheatCardBackgroundPeekActive = !cheatCardBackgroundPeekActive;
            ApplyCheatCardBackgroundPeek(cheatCardBackgroundPeekActive);
        }
    }

    /// <summary>
    /// 作弊用：将可见的 background 或（background 不显示时）可见的 revealableImage 设为半透明/恢复不透明。
    /// </summary>
    private void ApplyCheatCardBackgroundPeek(bool enabled)
    {
        if (boardManager == null) return;
        const float peekAlpha = 0.8f;
        float targetA = enabled ? peekAlpha : 1f;
        int rows = boardManager.GetCurrentRow();
        int cols = boardManager.GetCurrentCol();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Tile tile = boardManager.GetTile(r, c);
                if (tile == null) continue;
                Image bg = tile.backImage;
                Image rev = tile.revealableImage;
                if (!enabled)
                {
                    if (bg != null)
                    {
                        Color co = bg.color;
                        co.a = 1f;
                        bg.color = co;
                    }
                    if (rev != null)
                    {
                        Color co = rev.color;
                        co.a = 1f;
                        rev.color = co;
                    }
                    continue;
                }
                bool revShows = rev != null && rev.gameObject.activeInHierarchy && rev.color.a > 0.01f;
                if (revShows)
                {
                    Color co = rev.color;
                    co.a = targetA;
                    rev.color = co;
                    
                    
                    Color co2 = bg.color;
                    co2.a = 0;
                    bg.color = co2;
                }
                else
                {
                    Color co = bg.color;
                    co.a = targetA;
                    bg.color = co;
                }
            }
        }
    }

    public void restartGame()
    {
        // 清空MainGameData（重新加载场景时会重新创建GameManager，但为了确保数据清空，这里也清空一次）
        mainGameData.Reset();
        
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    void initializeScene()
    {
        // 初始血量从当前 scene 的 SceneInfo.hp 读取，未配置或≤0 时用 initialHealth（默认3）
        SceneInfo sceneInfo = GetCurrentSceneInfo();
        int initialHp = (sceneInfo != null && sceneInfo.hp > 0) ? sceneInfo.hp : initialHealth;
        mainGameData.health = initialHp;
        mainGameData.maxHealth = initialHp;
        // AsceticVow 场景类型：效果与拥有 AsceticVow 相同，血量上限-2
        if (sceneInfo != null && sceneInfo.HasType("AsceticVow"))
        {
            mainGameData.maxHealth -= 2;
            if (mainGameData.health > mainGameData.maxHealth)
                mainGameData.health = mainGameData.maxHealth;
        }
        mainGameData.flashlights = initialFlashlights;
        
        // 初始化升级项
        if (upgradeManager != null)
        {
            upgradeManager.InitializeUpgrades();
        }
        
        // 标记：当前 scene 若包含指定 mode，则在免费商店流程结束后显示模式提示弹窗
        pendingStartModePopup = sceneInfo != null && (
            sceneInfo.HasType("mist") ||
            sceneInfo.HasType("frozen") ||
            sceneInfo.HasType("frozenToDamageMax") ||
            sceneInfo.HasType("frozenNew") ||
            sceneInfo.HasType("speed") ||
            sceneInfo.HasType("speedMode")
        );
        
        // forget：每关重置“上一张提示格”记录
        lastForgetHintTilePosition = new Vector2Int(-1, -1);
        
        // 检查并显示免费商店
        CheckAndShowFreeShop();
    }
    
    /// <summary>
    /// 获取当前的最大血量（考虑升级项的影响，如AsceticVow）
    /// </summary>
    public int GetMaxHealth()
    {
        return mainGameData.maxHealth;
    }
    
    /// <summary>
    /// 增加血量（处理BloodtoGold溢出转换和AsceticVow额外回血）
    /// </summary>
    /// <param name="amount">要增加的血量</param>
    /// <param name="isShopHeal">是否是在商店回血</param>
    public void AddHealth(int amount, bool isShopHeal = false)
    {
        if (amount <= 0) return;
        
        int maxHealth = GetMaxHealth();
        int currentHealth = mainGameData.health;
        
        // 如果是商店回血且拥有 AsceticVow 或场景类型为 AsceticVow，额外回1点
        bool asceticVowEffect = upgradeManager != null && upgradeManager.HasUpgrade("AsceticVow");
        if (!asceticVowEffect)
        {
            var sceneInfo = GetCurrentSceneInfo();
            asceticVowEffect = sceneInfo != null && sceneInfo.HasType("AsceticVow");
        }
        if (isShopHeal && asceticVowEffect)
        {
            amount += 1;
        }
        
        int newHealth = currentHealth + amount;
        int overflow = 0;
        
        // 计算溢出量
        if (newHealth > maxHealth)
        {
            overflow = newHealth - maxHealth;
            newHealth = maxHealth;
        }
        
        mainGameData.health = newHealth;
        
        // 如果有BloodtoGold且溢出，将溢出量转换为金币
        if (overflow > 0 && upgradeManager != null && upgradeManager.HasUpgrade("BloodtoGold"))
        {
            mainGameData.coins += overflow;
            if (overflow > 0)
            {
                ShowFloatingText("coin", overflow);
            }
        }
        
        // 显示血量增加文本
        int actualHeal = newHealth - currentHealth;
        if (actualHeal > 0)
        {
            ShowFloatingText("health", actualHeal);
        }
        
        CheckAndUpdateShake();
        uiManager?.UpdateUI();
    }
    
    /// <summary>
    /// 扣血（用于寒冰/竞速/敌人/事件等）。护盾按 1:1 抵消伤害点数，优先消耗护盾，并在护盾/血量位置显示漂浮文字。
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;
        int absorb = Mathf.Min(damage, mainGameData.shield);
        if (absorb > 0)
        {
            mainGameData.shield -= absorb;
            ShowFloatingTextForResource("shield", -absorb);
        }
        damage -= absorb;
        uiManager?.UpdateUI();
        if (damage <= 0)
            return;
        mainGameData.health -= damage;
        ShowFloatingTextForResource("health", -damage);
        upgradeManager?.OnHealthLost();
        CheckAndTriggerShake();
        uiManager?.UpdateUI();
        StartCoroutine(ShowPlayerHurt());
        StartCoroutine(CheckGameOverAfterDamageDelayed());
    }
    
    /// <summary>
    /// 冰冻类场景扣血入口：支持 frozenToDamageMax（扣血同时降低最大血量）。
    /// </summary>
    private void ApplyFrozenSceneDamage(int damage, SceneInfo sceneInfo)
    {
        if (damage <= 0) return;
        int healthBefore = mainGameData.health;
        TakeDamage(damage);
        
        if (sceneInfo != null && sceneInfo.HasType("frozenToDamageMax"))
        {
            int actualHealthDamage = Mathf.Max(0, healthBefore - mainGameData.health);
            if (actualHealthDamage > 0)
            {
                ReduceMaxHealth(actualHealthDamage);
            }
        }
    }
    
    /// <summary>
    /// 降低最大血量，并确保当前血量不超过上限。
    /// </summary>
    private void ReduceMaxHealth(int amount)
    {
        if (amount <= 0) return;
        
        int oldMax = mainGameData.maxHealth;
        int newMax = Mathf.Max(1, oldMax - amount);
        if (newMax >= oldMax) return;
        
        mainGameData.maxHealth = newMax;
        if (mainGameData.health > mainGameData.maxHealth)
        {
            mainGameData.health = mainGameData.maxHealth;
        }
        
        uiManager?.UpdateUI();
    }
    
    private IEnumerator CheckGameOverAfterDamageDelayed()
    {
        yield return new WaitForSeconds(0.1f);
        CheckGameOverAfterEnemyAnimation();
    }
    
    /// <summary> 点击铃铛后停止竞速倒计时（本关不再计时） </summary>
    public void StopSpeedCountdown()
    {
        speedCountdownStarted = false;
    }
    
    /// <summary> 竞速模式倒计时秒数：extraValues[0] 为基础值，extraValues[1] 为每个格子额外秒数；仅一项时第二项视为 0。 </summary>
    public static float ComputeSpeedModeCountdownSeconds(SceneInfo sceneInfo, int cellCount)
    {
        if (sceneInfo?.extraValues == null || sceneInfo.extraValues.Count == 0) return 0f;
        int baseSec = sceneInfo.extraValues[0];
        int perCell = sceneInfo.extraValues.Count > 1 ? sceneInfo.extraValues[1] : 0;
        return baseSec + perCell * cellCount;
    }
    
    /// <summary> 竞速模式倒计时是否在运行（玩家翻开第一个 tile 后才开始，且仅在翻牌场景） </summary>
    private bool IsSpeedCountdownActive()
    {
        if (boardManager == null || GetCurrentSceneInfo() == null || !GetCurrentSceneInfo().HasType("speed"))
            return false;
        if (!speedCountdownStarted || speedCountdownTotal <= 0f) return false;
        if (shopManager != null && shopManager.shopPanel != null && shopManager.shopPanel.activeSelf)
            return false;
        if (DialogPanel.Instance != null && DialogPanel.Instance.dialogPanel != null && DialogPanel.Instance.dialogPanel.activeSelf)
            return false;
        if (VictoryPanel.Instance != null && VictoryPanel.Instance.victoryPanel != null && VictoryPanel.Instance.victoryPanel.activeSelf)
            return false;
        if (LoseMenu.Instance != null && LoseMenu.Instance.loseMenuPanel != null && LoseMenu.Instance.loseMenuPanel.activeSelf)
            return false;
        return true;
    }
    
    /// <summary>
    /// 获取当前 scene 的 SceneInfo
    /// </summary>
    public SceneInfo GetCurrentSceneInfo()
    {
        if (string.IsNullOrEmpty(mainGameData.currentScene) || CSVLoader.Instance == null)
        {
            return null;
        }
        
        foreach (SceneInfo sceneInfo in CSVLoader.Instance.sceneInfos)
        {
            if (sceneInfo.identifier == mainGameData.currentScene)
            {
                return sceneInfo;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 检查并显示免费商店
    /// </summary>
    private void CheckAndShowFreeShop()
    {
        SceneInfo sceneInfo = GetCurrentSceneInfo();
        if (sceneInfo == null || shopManager == null)
        {
            return;
        }
        
        // 如果两个都大于零，先显示 ShowShopWithFreeItem，等 shop 关闭后，再显示 ShowShopWithFreeUpgrades
        if (sceneInfo.freeItem > 0 && sceneInfo.freeUpgrade > 0)
        {
            // 先显示免费物品商店
            shopManager.ShowShopWithFreeItem(sceneInfo.freeItem);
        }
        else if (sceneInfo.freeItem > 0)
        {
            shopManager.ShowShopWithFreeItem(sceneInfo.freeItem);
        }
        else if (sceneInfo.freeUpgrade > 0)
        {
            shopManager.ShowShopWithFreeUpgrade(sceneInfo.freeUpgrade);
        }
        else
        {
            shopManager.HideShop();
            ShowPendingStartModePopupIfNeeded(() =>
            {
                GameManager.Instance.boardManager.RestartAnimateBoard();
            });
        }
    }
    
    /// <summary>
    /// 免费商店关闭后的回调（当免费物品商店关闭后，如果还有免费升级商店，则显示它）
    /// </summary>
    public void OnFreeShopClosed(bool wasFreeItem)
    {
        if (!wasFreeItem)
        {
            // 如果是免费升级商店关闭，所有免费商店都结束了，刷新board
            ShowPendingStartModePopupIfNeeded(() =>
            {
                RefreshBoard();
            });
            return;
        }
        
        // 如果是免费物品商店关闭，检查是否还有免费升级商店
        SceneInfo sceneInfo = GetCurrentSceneInfo();
        if (sceneInfo != null && sceneInfo.freeUpgrade > 0 && shopManager != null)
        {
            shopManager.ShowShopWithFreeUpgrade(sceneInfo.freeUpgrade);
        }
        else
        {
            // 如果没有免费升级商店，所有免费商店都结束了，刷新board
            ShowPendingStartModePopupIfNeeded(() =>
            {
                RefreshBoard();
            });
        }
    }

    /// <summary>
    /// 若当前 scene 需要开局模式提示，则显示单按钮弹窗；否则直接继续。
    /// </summary>
    private void ShowPendingStartModePopupIfNeeded(System.Action onComplete)
    {
        if (!pendingStartModePopup)
        {
            onComplete?.Invoke();
            return;
        }
        
        // 开局 story 正在播放时，不立即弹，改为 story 结束后再弹
        if (isOpeningStoryPlaying)
        {
            pendingModePopupAfterStory = true;
            pendingStartModePopup = false;
            onComplete?.Invoke();
            return;
        }
        
        pendingStartModePopup = false;
        ShowModePopupForCurrentSceneIfAny(onComplete);
    }

    private void ShowModePopupForCurrentSceneIfAny(System.Action onComplete)
    {
        SceneInfo sceneInfo = GetCurrentSceneInfo();
        if (sceneInfo == null || DialogPanel.Instance == null)
        {
            onComplete?.Invoke();
            return;
        }
        
        List<string> popupTexts = new List<string>();
        if (sceneInfo.HasType("mist"))
            popupTexts.Add(LocalizationHelper.GetLocalizedString("mistModePopup"));
        if (sceneInfo.HasType("frozen"))
            popupTexts.Add(LocalizationHelper.GetLocalizedString("frozenModePopup"));
        if (sceneInfo.HasType("frozenToDamageMax"))
            popupTexts.Add(LocalizationHelper.GetLocalizedString("frozenModePopup"));
        if (sceneInfo.HasType("frozenNew"))
            popupTexts.Add(LocalizationHelper.GetLocalizedString("frozenModePopup"));
        if (sceneInfo.HasType("speed") || sceneInfo.HasType("speedMode"))
            popupTexts.Add(LocalizationHelper.GetLocalizedString("speedModePopup"));
        
        if ((sceneInfo.HasType("frozen") || sceneInfo.HasType("frozenToDamageMax") || sceneInfo.HasType("frozenNew")) && boardManager != null)
        {
            Vector2Int frozenSize = boardManager.GetFrozenPatchSize();
            if (frozenSize.x > 0 && frozenSize.y > 0)
                popupTexts.Add(string.Format(LocalizationHelper.GetLocalizedString("frozenAreaExpand"), frozenSize.x, frozenSize.y));
        }
        
        if (popupTexts.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }
        
        string popupText = "";
        for (int i = 0; i < popupTexts.Count; i++)
        {
            popupText += popupTexts[i];
            if (i < popupTexts.Count - 1)
                popupText += "\n";
        }
        
        DialogPanel.Instance.ShowDialog(popupText, onComplete);
    }
    public void StartNewLevel()
    {
        // 清理boss关卡卡牌（确保每次开始新关卡时都先清理）
        CleanupBossLevelCards();
        
        // 隐藏所有对话框
        if (DialogPanel.Instance != null)
        {
            DialogPanel.Instance.HideDialog();
        }
        
        // 隐藏gameover和victory menu
        if (uiManager != null)
        {
            uiManager.HideGameOver();
        }
        if (VictoryPanel.Instance != null)
        {
            VictoryPanel.Instance.HideVictory();
        }
        
        // 获取当前关卡信息
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        bool isBossLevel = !string.IsNullOrEmpty(levelInfo.boss);
        bool isNunBossLevel = isBossLevel && levelInfo.boss.ToLower() == "nun";
        bool isSnowmanBossLevel = isBossLevel && levelInfo.boss.ToLower() == "snowman";
        bool isSnowsnakeBossLevel = isBossLevel && levelInfo.boss.ToLower().StartsWith("snowsnake");
        bool isHorriblemanBossLevel = isBossLevel && BossLevelIds.IsHorriblemanStyleBoss(levelInfo.boss);
        bool isShadowBossLevel = isBossLevel && levelInfo.boss.ToLower() == "shadow";
        bool isGhostBossLevel = isBossLevel && levelInfo.boss.ToLower() == "ghost";
        bool isCrackBossLevel = isBossLevel && levelInfo.boss.ToLower() == "crack";
        
        // 隐藏bossDesc panel和bossIcon（如果不是boss关卡）
        if ((!isBossLevel || isCrackBossLevel) && uiManager != null)
        {
            uiManager.HideBossDesc();
            uiManager.HideBossIcon();
        }
        
        // 如果是boss关卡
        if (isBossLevel && !isCrackBossLevel)
        {
            // 在进入关卡且抽取前，添加boss卡和door卡，移除bell卡
            PrepareBossLevelCards(levelInfo.boss);
            
            // 显示bossIcon
            if (uiManager != null && CardInfoManager.Instance != null)
            {
                CardType bossCardType = GetBossCardType(levelInfo.boss);
                if (bossCardType != CardType.Blank)
                {
                    uiManager.ShowBossIcon(bossCardType);
                }
            }
        }
        
        // 如果是boss关卡，且是scene中第一个该boss的关卡，在生成board之前播放对应的before story（crack 也走此逻辑）
        if (isBossLevel && storyManager != null)
        {
            string currentScene = mainGameData.currentScene;
            bool isFirstBossLevel = false;
            string beforeStoryIdentifier = "";
            
            if (isNunBossLevel)
            {
                isFirstBossLevel = LevelManager.Instance.IsFirstBossLevelInSceneForStory(currentScene, "nun");
                beforeStoryIdentifier = "beforeNun";
            }
            else if (isSnowmanBossLevel)
            {
                isFirstBossLevel = LevelManager.Instance.IsFirstBossLevelInSceneForStory(currentScene, "snowman");
                beforeStoryIdentifier = "beforeSnowman";
            }
            else if (isSnowsnakeBossLevel)
            {
                isFirstBossLevel = LevelManager.Instance.IsFirstBossLevelInSceneForStory(currentScene, "snowsnake");
                beforeStoryIdentifier = "beforeSnowsnake";
            }
            else if (isHorriblemanBossLevel)
            {
                string bm = levelInfo.boss.ToLowerInvariant();
                if (bm == BossLevelIds.HorriblemanNew)
                {
                    isFirstBossLevel = LevelManager.Instance.IsFirstBossLevelInSceneForStory(currentScene, BossLevelIds.HorriblemanNew);
                    beforeStoryIdentifier = "beforeHorriblemanNew";
                }
                else
                {
                    isFirstBossLevel = LevelManager.Instance.IsFirstBossLevelInSceneForStory(currentScene, BossLevelIds.Horribleman);
                    beforeStoryIdentifier = "beforeHorribleman";
                }
            }
            else if (isShadowBossLevel)
            {
                isFirstBossLevel = LevelManager.Instance.IsFirstBossLevelInSceneForStory(currentScene, "shadow");
                beforeStoryIdentifier = "beforeShadow";
            }
            else if (isGhostBossLevel)
            {
                isFirstBossLevel = LevelManager.Instance.IsFirstBossLevelInSceneForStory(currentScene, "ghost");
                beforeStoryIdentifier = "beforeGhost";
            }
            else if (isCrackBossLevel)
            {
                isFirstBossLevel = LevelManager.Instance.IsFirstBossLevelInSceneForStory(currentScene, "crack");
                beforeStoryIdentifier = "beforeCrack";
            }
            
            if (isFirstBossLevel && !string.IsNullOrEmpty(beforeStoryIdentifier))
            {
                // 如果该 boss 关同时也是当前 scene 的第一关，先执行 scene 初始化（包含初始升级项）
                // 并提前标记 story 播放中，避免免费商店/模式弹窗在 story 期间弹出。
                if (IsFirstLevelInScene())
                {
                    isOpeningStoryPlaying = true;
                    initializeScene();
                }

                // 在播放故事之前，先初始化游戏主体（board、upgrades、UI等）
                InitializeLevelGameplay(levelInfo, isBossLevel);
                
                // 然后播放故事，故事播放完成后只执行后续操作
                isOpeningStoryPlaying = true;
                storyManager.PlayStory(beforeStoryIdentifier, () =>
                {
                    isOpeningStoryPlaying = false;
                    ContinueAfterStory(levelInfo, isBossLevel);
                });
                return; // 等待story播放完成后再继续
            }
        }
        
        // 检查是否是scene的第一个level，如果是，检查是否有{sceneIdentifier}start的story
        if (IsFirstLevelInScene() && storyManager != null && !string.IsNullOrEmpty(mainGameData.currentScene))
        {
            string sceneStartStoryIdentifier = mainGameData.currentScene + "start";
            bool hasStartStory = CSVLoader.Instance != null && CSVLoader.Instance.storyDict.ContainsKey(sceneStartStoryIdentifier);

            // 若该 story 存在，则在 initializeScene 期间就先标记“story 正在播放”
            // 这样免费商店/模式弹窗的触发会被延后到 story 结束后。
            if (hasStartStory)
            {
                isOpeningStoryPlaying = true;
            }

            initializeScene();
            
            // 检查是否存在该story
            if (hasStartStory)
            {
                // 在播放故事之前，先初始化游戏主体（board、upgrades、UI等）
                InitializeLevelGameplay(levelInfo, isBossLevel);
                
                // 然后播放故事，故事播放完成后只执行后续操作
                storyManager.PlayStory(sceneStartStoryIdentifier, () =>
                {
                    isOpeningStoryPlaying = false;
                    ContinueAfterStory(levelInfo, isBossLevel);
                });
                return; // 等待story播放完成后再继续
            }
        }
        
        // 直接继续（没有story需要播放）
        isOpeningStoryPlaying = false;
        ContinueStartNewLevelAfterStory(levelInfo, isBossLevel);
    }
    
    /// <summary>
    /// 检查是否是当前scene的第一个level
    /// </summary>
    private bool IsFirstLevelInScene()
    {
        if (string.IsNullOrEmpty(mainGameData.currentScene) || CSVLoader.Instance == null)
        {
            return false;
        }
        
        // 找到当前scene的第一个关卡（identifier 在 level 中不存在时用 mainScene 匹配）
        string levelKey = LevelManager.Instance != null ? LevelManager.Instance.GetSceneKeyForLevels(mainGameData.currentScene) : mainGameData.currentScene;
        int firstLevelIndex = -1;
        for (int i = 0; i < CSVLoader.Instance.levelInfos.Count; i++)
        {
            if (CSVLoader.Instance.levelInfos[i].scene == levelKey)
            {
                firstLevelIndex = i;
                break;
            }
        }
        return firstLevelIndex >= 0 && mainGameData.currentLevel == firstLevelIndex + 1;
    }
    
    /// <summary>
    /// 初始化关卡的游戏主体（board、upgrades、UI等），在播放故事之前调用
    /// </summary>
    private void InitializeLevelGameplay(LevelInfo levelInfo, bool isBossLevel)
    {
        SceneInfo sceneInfo = GetCurrentSceneInfo();
        
        // scene.hero 非空时，将本场景 player 卡牌映射为对应 hero identifier
        if (CardInfoManager.Instance != null)
        {
            string heroIdentifier = (sceneInfo != null) ? sceneInfo.hero : null;
            CardInfoManager.Instance.SetCurrentPlayerIdentifier(heroIdentifier);
        }
        
        // 重置boss战斗状态
        nunDoorCount = 0;
        snowmanLightCount = 0;
        snowsnakeLightCount = 0;
        horriblemanCatchCount = 0;
        shadowCatchCount = 0;
        
        if (boardManager != null)
        {
            boardManager.ClearBoard();
            boardManager.GenerateBoard();
            // 寒冰场景：初始为 1，并加上本关初始已揭示的寒冰格（如 player、教堂自动翻开）
            frozenRevealedCount = 1 + boardManager.GetInitialRevealedFrozenCount();
            lastManualRevealWasFrozenNewTile = false;
            lastForgetHintTilePosition = new Vector2Int(-1, -1);
            boardManager.UpdatePlayerFrozenDataText();
            boardManager.UpdatePlayerProgressBarVisibility();
        }
        
        // 竞速模式：初始化倒计时，等玩家翻开第一个 tile 后才开始（总秒数 = 基础 + 每格×格子数）
        if (sceneInfo != null && sceneInfo.HasType("speed"))
        {
            int cellCount = levelInfo.row * levelInfo.col;
            float totalSec = ComputeSpeedModeCountdownSeconds(sceneInfo, cellCount);
            if (sceneInfo.extraValues == null || sceneInfo.extraValues.Count == 0 || totalSec <= 0f)
            {
                Debug.LogError("[Speed] 场景已标注为 speed 但 extraValues 未配置或计算结果为 0，请在 extraValues 中设置「基础秒数|每格秒数」（CSV 用 | 分隔）。");
            }
            speedCountdownTotal = totalSec > 0f ? totalSec : 0f;
            speedCountdownRemaining = speedCountdownTotal;
            speedCountdownStarted = false;
        }
        else
        {
            speedCountdownTotal = 0f;
            speedCountdownRemaining = 0f;
            speedCountdownStarted = false;
        }
        
        isUsingFlashlight = false;
        isFlashlightRevealing = false;
        isChurchRingRevealing = false;
        currentHintPosition = new Vector2Int(-1, -1);
        isPlayerInputDisabled = false; // 恢复玩家输入
        pendingBossCallback = null; // 清空待执行的boss回调
        
        // LastLight升级项：如果手电筒数量大于1，保留1个到下一关
        int keptFlashlights = upgradeManager?.GetFlashlightForNextLevel(mainGameData.flashlights) ?? 0;
        mainGameData.flashlights = initialFlashlights + keptFlashlights;
        
        mainGameData.patternRecognitionSequence = 0; // 重置patternRecognition计数器
        mainGameData.isFirstTileRevealedThisTurn = false; // 重置第一张卡标记（用于FirstLuck）
        mainGameData.churchLightUsedThisLevel = false; // 重置churchLight使用标记
        mainGameData.hasTriggeredEnemyThisLevel = false; // 重置触发敌人标记（用于noOneNotice）
        mainGameData.GetCompletedRows().Clear(); // 清空已完成的行记录（用于showRowToGift升级项）
        CursorManager.Instance?.ResetCursor();
        // noRing 模式：非 boss 关时始终显示铃铛按钮，可随时敲铃铛离开
        bool noRingMode = sceneInfo != null && sceneInfo.HasType("noRing");
        bool isCrackBossLevel = isBossLevel && !string.IsNullOrEmpty(levelInfo.boss) && levelInfo.boss.ToLower() == "crack";
        if (noRingMode && (!isBossLevel || isCrackBossLevel))
            uiManager?.ShowBellButton();
        else
            uiManager?.HideBellButton(); // 新关卡开始时隐藏bell按钮
        uiManager?.UpdateUI();
        uiManager?.UpdateEnemyCount();
        uiManager?.UpdateHintCount();
        uiManager?.UpdateUpgradeDisplay();
        uiManager?.UpdateBackgroundImage(); // 更新背景图片
        
        // 新关卡开始时检查抖动状态
        CheckAndUpdateShake();
        
        // 触发familiarSteet升级项效果
        upgradeManager?.OnLevelStart();
        
        // revealHint/fakeRevealAll 模式：每关开始直接揭示所有 hint 格子
        if (sceneInfo != null && (sceneInfo.HasType("revealHint") || sceneInfo.HasType("fakeRevealAll")) && boardManager != null)
        {
            boardManager.RevealAllHintTiles();
        }
        
        // 关卡中第一次更新 revealableTiles：在 familiarStreet、RevealAllHintTiles 等逻辑之后，从玩家格 BFS 更新可翻开格子
        boardManager?.RefreshRevealableTilesFromPlayerBFS();
    }
    
    /// <summary>
    /// 故事播放完成后的后续操作（不重复更新游戏主体）
    /// </summary>
    private void ContinueAfterStory(LevelInfo levelInfo, bool isBossLevel)
    {
        if (pendingModePopupAfterStory)
        {
            pendingModePopupAfterStory = false;
            ShowModePopupForCurrentSceneIfAny(() =>
            {
                ContinueAfterStoryCore(levelInfo, isBossLevel);
            });
            return;
        }
        
        ContinueAfterStoryCore(levelInfo, isBossLevel);
    }
    
    private void ContinueAfterStoryCore(LevelInfo levelInfo, bool isBossLevel)
    {
        bool isCrackBossLevel = isBossLevel && !string.IsNullOrEmpty(levelInfo.boss) && levelInfo.boss.ToLower() == "crack";
        // 检查是否需要显示MoreEnemy提示（在显示boss描述或教程之前）
        CheckAndShowMoreEnemyMessage(levelInfo, isBossLevel, () =>
        {
            // 如果是boss关卡，显示boss的desc弹窗
            if (isBossLevel && !isCrackBossLevel)
            {
                ShowBossDesc(levelInfo.boss, GetSnowsnakeBodyCount(levelInfo.boss));
            }
            else
            {
                // 显示start教程
                tutorialManager?.ShowTutorial("start");
            }
        });
    }
    
    private void ContinueStartNewLevelAfterStory(LevelInfo levelInfo, bool isBossLevel)
    {
        bool isCrackBossLevel = isBossLevel && !string.IsNullOrEmpty(levelInfo.boss) && levelInfo.boss.ToLower() == "crack";
        // 初始化游戏主体
        InitializeLevelGameplay(levelInfo, isBossLevel);
        
        // 检查是否需要显示MoreEnemy提示（在显示boss描述或教程之前）
        CheckAndShowMoreEnemyMessage(levelInfo, isBossLevel, () =>
        {
            // 如果是boss关卡，显示boss的desc弹窗
            if (isBossLevel && !isCrackBossLevel)
            {
                ShowBossDesc(levelInfo.boss, GetSnowsnakeBodyCount(levelInfo.boss));
            }
            else
            {
                // 显示start教程
                tutorialManager?.ShowTutorial("start");
            }
        });
    }
    
    private void ShowBossDesc(string bossType, int snowsnakeBodyCount = 0)
    {
        if (CardInfoManager.Instance == null || DialogPanel.Instance == null) return;
        
        string bossTypeLower = bossType.ToLower();
        CardInfo bossCardInfo = null;
        
        // 获取boss的CardInfo
        if (bossTypeLower == "nun")
        {
            bossCardInfo = CardInfoManager.Instance.GetCardInfo("nun");
        }
        else if (bossTypeLower == "snowman")
        {
            bossCardInfo = CardInfoManager.Instance.GetCardInfo("snowman");
        }
        else if (bossTypeLower.StartsWith("snowsnake"))
        {
            // snowsnake_数字：boss信息只展示 head
            bossCardInfo = CardInfoManager.Instance.GetCardInfo("snowsnakeHead");
        }
        else if (BossLevelIds.IsHorriblemanStyleBoss(bossTypeLower))
        {
            bossCardInfo = CardInfoManager.Instance.GetCardInfo("horribleman");
        }
        else if (bossTypeLower == "shadow")
        {
            bossCardInfo = CardInfoManager.Instance.GetCardInfo("shadow");
        }
        else if (bossTypeLower == "ghost")
        {
            bossCardInfo = CardInfoManager.Instance.GetCardInfo("ghost");
        }
        else if (bossTypeLower == "crack")
        {
            bossCardInfo = CardInfoManager.Instance.GetCardInfo("crack");
        }
        
        // 如果找到了boss的CardInfo，使用DialogPanel显示desc
        if (bossCardInfo != null/* && !string.IsNullOrEmpty(bossCardInfo.desc)*/)
        {
            string nameKey = "cardName_" + bossCardInfo.identifier;
            string descKey = "cardDesc_" + bossCardInfo.identifier;

            // 从 Localization 获取卡牌名称
            var nameLocalizedString = new LocalizedString("GameText", nameKey);
            var nameHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(nameLocalizedString.TableReference, nameLocalizedString.TableEntryReference);
            string localizedName = nameHandle.WaitForCompletion();
            
            // 从 Localization 获取卡牌描述
            string localizedDesc;
            if (bossTypeLower.StartsWith("snowsnake"))
            {
                // snowsnake 描述支持 {0}：传入 body 数量（总长度 - 1）
                localizedDesc = LocalizationHelper.GetLocalizedString(descKey, new object[] { snowsnakeBodyCount });
            }
            else
            {
                var descLocalizedString = new LocalizedString("GameText", descKey);
                var descHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(descLocalizedString.TableReference, descLocalizedString.TableEntryReference);
                localizedDesc = descHandle.WaitForCompletion();
            }
            
            string bossDesc = $"{localizedName}\n\n{localizedDesc}";
            
            // 检查是否有其他enemy（除了boss）
            LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
            if (levelInfo != null && levelInfo.enemyCount > 0)
            {
                // 从 Localization 获取extraEnemy文本
                var extraEnemyLocalizedString = new LocalizedString("GameText", "extraEnemy");
                extraEnemyLocalizedString.Arguments = new object[] { levelInfo.enemyCount };
                var extraEnemyHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(extraEnemyLocalizedString.TableReference, extraEnemyLocalizedString.TableEntryReference, extraEnemyLocalizedString.Arguments);
                string extraEnemyText = extraEnemyHandle.WaitForCompletion();
                
                bossDesc += $"\n\n{extraEnemyText}";
            }
            
            DialogPanel.Instance.ShowDialog(bossDesc, () => { });
        }
    }

    private int GetSnowsnakeBodyCount(string bossType)
    {
        if (string.IsNullOrEmpty(bossType)) return 0;

        string[] parts = bossType.Split('_');
        if (parts.Length < 2) return 0;

        if (!int.TryParse(parts[1], out int totalLength))
        {
            return 0;
        }

        return Mathf.Max(0, totalLength - 1);
    }
    
    /// <summary>
    /// 检查并显示MoreEnemy提示（如果当前关卡敌人数量比上一个非boss关卡多）
    /// </summary>
    private void CheckAndShowMoreEnemyMessage(LevelInfo currentLevelInfo, bool isBossLevel, System.Action onComplete)
    {
        // 如果当前关卡是第一关或是boss关，直接执行回调
        if (mainGameData.currentLevel <= 1 || isBossLevel)
        {
            onComplete?.Invoke();
            return;
        }
        
        // 从当前关卡往前找，找到上一个不是boss关的关卡
        int previousNonBossLevel = -1;
        for (int i = mainGameData.currentLevel - 1; i >= 1; i--)
        {
            LevelInfo prevLevelInfo = LevelManager.Instance.GetLevelInfo(i);
            if (string.IsNullOrEmpty(prevLevelInfo.boss))
            {
                previousNonBossLevel = i;
                break;
            }
        }
        
        // 如果找到了上一个非boss关卡，比较敌人数量
        if (previousNonBossLevel > 0)
        {
            LevelInfo prevLevelInfo = LevelManager.Instance.GetLevelInfo(previousNonBossLevel);
            if (currentLevelInfo.enemyCount > prevLevelInfo.enemyCount)
            {
                // 当前关卡敌人更多，显示MoreEnemy提示
                var moreEnemyLocalizedString = new LocalizedString("GameText", "MoreEnemy");
                var moreEnemyHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(moreEnemyLocalizedString.TableReference, moreEnemyLocalizedString.TableEntryReference);
                string moreEnemyText = moreEnemyHandle.WaitForCompletion();
                
                DialogPanel.Instance.ShowDialog(moreEnemyText, () =>
                {
                    onComplete?.Invoke();
                });
                return;
            }
        }
        
        // 不需要显示提示，直接执行回调
        onComplete?.Invoke();
    }
    
    public void RetryLevel()
    {
        // 重新开始关卡（mainGameData保持当前状态，不恢复）
        StartNewLevel();
        
        // 隐藏gameover面板
        if (LoseMenu.Instance != null)
        {
            LoseMenu.Instance.HideLoseMenu();
        }
        else
        {
            uiManager?.HideGameOver();
        }
    }
    
    public void RetryBoss()
    {
        // 重新开始关卡（mainGameData保持当前状态，不恢复）
        StartNewLevel();
        
        // 隐藏gameover面板
        if (LoseMenu.Instance != null)
        {
            LoseMenu.Instance.HideLoseMenu();
        }
        else
        {
            uiManager?.HideGameOver();
        }
    }
    
    public bool CanRevealTile(int row, int col)
    {
        if (isUsingFlashlight)
            return true;
            
        return boardManager.CanRevealTile(row, col);
    }
    
    public void RevealTile(int row, int col)
    {
        // 如果玩家输入被禁用，不允许翻开
        if (isPlayerInputDisabled)
        {
            return;
        }
        
        // 检查是否是第二关，且tutorialForceBoard开启
        int currentLevel = mainGameData.currentLevel;
        bool tutorialForceBoard = TutorialManager.Instance != null && TutorialManager.Instance.tutorialForceBoard;
        bool isLevel2 = currentLevel == 2 && tutorialForceBoard;
        
        if (isLevel2)
        {
            Vector2Int playerPos = boardManager.GetPlayerPosition();
            // 如果试图不使用灯笼就点击上方或再上方的格子，显示hint1_4
            if (row < playerPos.x && playerPos.y ==col)
            {
                // 检查是否已经拿到灯笼（flashlights > 0）
                bool hasFlashlight = GameManager.Instance != null && GameManager.Instance.mainGameData.flashlights > 0;
                bool usingFlashlight = GameManager.Instance.IsUsingFlashlight();
                
                if(!usingFlashlight)
                {
                    if (hasFlashlight)
                    {
                        // 没有使用灯笼，显示hint1_4
                        if (TutorialManager.Instance != null)
                        {
                            TutorialManager.Instance.ShowTutorial("hint1_4",true);
                        }
                    }
                    else
                    {
                        
                        TutorialManager.Instance.ShowTutorial("hint1_3",true);
                    }
                    return; // 不执行翻开
                }
                else
                {
                    TutorialManager.Instance.tutorialForceBoard = false;
                }
            }
        }
        
        boardManager.RevealTile(row, col, true, false, true);
    }
    
    public bool IsPlayerInputDisabled()
    {
        return isPlayerInputDisabled;
    }
    
    // 用于churchRing升级项：reveal tile时等同于用light翻开（但不消耗light）
    public void RevealTileWithChurchRing(int row, int col)
    {
        if (boardManager == null) return;
        
        isChurchRingRevealing = true;
        boardManager.RevealTile(row, col);
        isChurchRingRevealing = false;
    }
    
    // 用于Spotter升级项：reveal tile时等同于用light翻开（但不消耗light）
    public void RevealTileWithFlashlight(int row, int col)
    {
        if (boardManager == null) return;
        
        isFlashlightRevealing = true;
        boardManager.RevealTile(row, col);
        isFlashlightRevealing = false;
    }

    /// <summary>
    /// 变色龙翻牌动画：先显示自身 0.2 秒，再 shake 0.3 秒，然后变身为相邻牌并执行正常翻牌逻辑。
    /// </summary>
    public IEnumerator PlayChameleonAndReveal(int row, int col, bool isFirst, bool fromFamiliarStreet, bool fromPlayerClick)
    {
        if (boardManager == null)
            yield break;
        
        Tile tile = boardManager.GetTile(row, col);
        CardType targetType = boardManager.ResolveChameleonMimic(row, col);
        
        if (tile != null)
        {
            yield return StartCoroutine(tile.PlayChameleonTransform(targetType));
        }
        
        boardManager.SetCardTypeForChameleon(row, col, targetType);

        // 标记本次翻牌来自变色龙产物（只影响本次 OnTileRevealed 结算）
        isChameleonEnemyRevealPending = true;
        chameleonEnemyRevealRow = row;
        chameleonEnemyRevealCol = col;
        boardManager.RevealTile(row, col, isFirst, fromFamiliarStreet, fromPlayerClick);
    }
    
    public void OnTileRevealed(int row, int col, CardType cardType, bool isLastTile = false, bool isLastSafeTile = false, bool isFirst = true, bool fromPlayerClick = false)
    {
        SceneInfo sceneInfo = GetCurrentSceneInfo();
        // 竞速模式：仅当玩家点击翻开 tile 时开始倒计时，程序自动翻开不算
        if (fromPlayerClick && sceneInfo != null && sceneInfo.HasType("speed") && !speedCountdownStarted)
            speedCountdownStarted = true;
        
        // forget：翻开任意下一格时，隐藏上一张提示格的 hintText；当前若是提示格则更新记录
        if (sceneInfo != null && sceneInfo.HasType("forget") && boardManager != null)
        {
            if (lastForgetHintTilePosition.x >= 0 && lastForgetHintTilePosition.y >= 0 &&
                (lastForgetHintTilePosition.x != row || lastForgetHintTilePosition.y != col))
            {
                Tile lastHintTile = boardManager.GetTile(lastForgetHintTilePosition.x, lastForgetHintTilePosition.y);
                if (lastHintTile != null && lastHintTile.GetCardType() == CardType.Hint && lastHintTile.hintText != null)
                {
                    lastHintTile.hintText.gameObject.SetActive(false);
                }
            }
            
            if (cardType == CardType.Hint)
            {
                lastForgetHintTilePosition = new Vector2Int(row, col);
            }
            else
            {
                lastForgetHintTilePosition = new Vector2Int(-1, -1);
            }
        }
        
        // 寒冰场景：若本格为寒冰则累计，超过阈值后每次翻开扣血（含提灯翻开）
        if (sceneInfo != null && (sceneInfo.HasType("frozen")) && boardManager != null && boardManager.IsFrozenTile(row, col))
        {
            frozenRevealedCount++;
            if (frozenRevealedCount > frozenDamageThreshold)
            {
                ApplyFrozenSceneDamage(1, sceneInfo);
            }
            boardManager?.UpdatePlayerFrozenDataText();
        }
        
        // frozenNew：仅检查玩家手动点击，自动翻开（提灯/教堂等）不参与连续判定
        if (sceneInfo != null && sceneInfo.HasType("frozenNew") && boardManager != null && fromPlayerClick)
        {
            bool isCurrentFrozenTile = boardManager.IsFrozenTile(row, col);
            if (isCurrentFrozenTile && lastManualRevealWasFrozenNewTile)
            {
                ApplyFrozenSceneDamage(1, sceneInfo);
            }
            lastManualRevealWasFrozenNewTile = isCurrentFrozenTile;
            boardManager?.UpdatePlayerFrozenDataText();
        }
        
        // 双刃剑：上一张翻开的是双刃剑时，本次翻开结算（玩家点击 / 提灯 / 教堂指环）
        bool countsAsDoublebladeNext = fromPlayerClick || isFlashlightRevealing || isChurchRingRevealing;
        if (mainGameData.doublebladeNextRevealPending && countsAsDoublebladeNext)
        {
            mainGameData.doublebladeNextRevealPending = false;
            bool isEnemyNext = CardInfoManager.Instance != null && CardInfoManager.Instance.IsEnemyCard(cardType);
            if (isEnemyNext)
                mainGameData.doublebladeStunThisEnemyReveal = true;
            else
            {
                mainGameData.coins += 1;
                ShowFloatingTextForResource("coin", 1);
            }
        }
        
        // 检查是否是敌人（基于isEnemy字段）
        bool isEnemy = false;
        if (CardInfoManager.Instance != null)
        {
            isEnemy = CardInfoManager.Instance.IsEnemyCard(cardType);
        }
        bool isSafeTile = !isEnemy;

        // 是否来自变色龙产物（本次结算只会匹配一次）
        bool isFromChameleon = false;
        if (isChameleonEnemyRevealPending && row == chameleonEnemyRevealRow && col == chameleonEnemyRevealCol)
        {
            isFromChameleon = true;
            isChameleonEnemyRevealPending = false; // 消耗该标记
        }
        bool isFromChameleonEnemy = isFromChameleon && isEnemy;
        
        // 标记第一张卡已翻开（在检查FirstLuck之前）
        bool wasFirstTile = !mainGameData.isFirstTileRevealedThisTurn;
        if (wasFirstTile)
        {
            mainGameData.isFirstTileRevealedThisTurn = true;
        }
        
        // FirstLuck: 每回合，如果翻开的第一张是敌人，得到两个礼物并且恢复一点血
        if (wasFirstTile && isEnemy && upgradeManager != null && upgradeManager.HasUpgrade("FirstLuck"))
        {
            mainGameData.gifts += 2;
            ShowFloatingText("gift", 2);
            AddHealth(1, false);
            uiManager?.TriggerUpgradeAnimation("FirstLuck");
        }
        
        switch (cardType)
        {
            case CardType.Blank:
                // 播放空白卡音效
                if (CardInfoManager.Instance != null)
                {
                    SFXManager.Instance?.PlayCardRevealSound("blank");
                }
                break;
            case CardType.Coin:
                int coinReward = 1;
                // greedFragile: 金币收益+1
                coinReward += upgradeManager?.GetCoinRewardModifier() ?? 0;
                mainGameData.coins += coinReward;
                ShowFloatingTextForResource("coin", coinReward);
                CreateCardFlyEffect(row, col, "coin");
                // 播放硬币卡音效
                SFXManager.Instance?.PlayCardRevealSound("coin");
                break;
            case CardType.Gift:
                int giftMultiplier = upgradeManager?.GetGiftMultiplier() ?? 1;
                int giftReward = giftMultiplier; // lastChance升级项：如果只有1 hp，gift翻倍
                // greedFragile: 礼物收益+1
                giftReward += upgradeManager?.GetGiftRewardModifier() ?? 0;
                mainGameData.gifts += giftReward;
                ShowFloatingTextForResource("gift", giftReward);
                CreateCardFlyEffect(row, col, "gift");
                // 播放礼物卡音效
                SFXManager.Instance?.PlayCardRevealSound("gift");
                break;
            case CardType.Enemy:
                // 变色龙产物敌人：不执行攻击/眩晕结算，贴图保持原样。
                if (isFromChameleonEnemy)
                {
                    // 保持与“翻开敌人”一致：用于 noOneNotice 等逻辑
                    mainGameData.hasTriggeredEnemyThisLevel = true;
                    // doubleblade 的“下次敌人眩晕”在这里也应消耗掉，否则会延续到下一只敌人翻牌
                    mainGameData.doublebladeStunThisEnemyReveal = false;
                    // 仍然播放普通翻开音效（不触发攻击/眩晕）
                    tutorialManager?.ShowTutorial("enemy");
                    if (isEnemy)
                    {
                        string enemyIdentifier = CardInfoManager.Instance?.GetEnemyIdentifier(cardType);
                        if (!string.IsNullOrEmpty(enemyIdentifier))
                        {
                            SFXManager.Instance?.PlayEnemySound(enemyIdentifier, "normal");
                        }
                    }
                    break;
                }
                // 显示enemy教程（第一次翻出敌人牌）
                tutorialManager?.ShowTutorial("enemy");
                
                // 所有isEnemy的卡牌都走这个逻辑
                if (isEnemy)
                {
                    // 播放敌人normal音效（显示时）
                    string enemyIdentifier = CardInfoManager.Instance?.GetEnemyIdentifier(cardType);
                    if (!string.IsNullOrEmpty(enemyIdentifier))
                    {
                        SFXManager.Instance?.PlayEnemySound(enemyIdentifier, "normal");
                    }
                    // 处理敌人图片切换和伤害逻辑
                    StartCoroutine(HandleEnemyRevealed(row, col, cardType));
                }
                break;
            case CardType.Crack:
                // crack 作为“boss-like”敌人：处理方式按普通敌人走（会受灯光安全翻开影响），但不会触发 boss 的过关逻辑。
                if (isFromChameleonEnemy)
                {
                    mainGameData.hasTriggeredEnemyThisLevel = true;
                    // doubleblade 的“下次敌人眩晕”在这里应消耗掉
                    mainGameData.doublebladeStunThisEnemyReveal = false;
                    tutorialManager?.ShowTutorial("enemy");
                    if (isEnemy)
                    {
                        string enemyIdentifier = CardInfoManager.Instance?.GetEnemyIdentifier(cardType);
                        if (!string.IsNullOrEmpty(enemyIdentifier))
                        {
                            SFXManager.Instance?.PlayEnemySound(enemyIdentifier, "normal");
                        }
                    }
                    break;
                }

                tutorialManager?.ShowTutorial("enemy");
                if (isEnemy)
                {
                    string enemyIdentifier = CardInfoManager.Instance?.GetEnemyIdentifier(cardType);
                    if (!string.IsNullOrEmpty(enemyIdentifier))
                    {
                        SFXManager.Instance?.PlayEnemySound(enemyIdentifier, "normal");
                    }
                    StartCoroutine(HandleEnemyRevealed(row, col, cardType));
                }
                break;
            case CardType.Nun:
                if (isFromChameleonEnemy)
                {
                    mainGameData.hasTriggeredEnemyThisLevel = true;
                    mainGameData.doublebladeStunThisEnemyReveal = false;
                    if (isEnemy)
                    {
                        // 不触发攻击/眩晕
                        SFXManager.Instance?.PlayEnemySound("nun", "normal");
                    }
                    break;
                }
                // nun boss处理
                if (isEnemy)
                {
                    // 播放nun normal音效
                    SFXManager.Instance?.PlayEnemySound("nun", "normal");
                    // 处理敌人图片切换和伤害逻辑（nun现在和其他敌人一样，受灯光影响）
                    StartCoroutine(HandleEnemyRevealed(row, col, cardType));
                }
                // 执行nun boss的特殊逻辑（门等）
                HandleNunBossRevealed(row, col);
                break;
            case CardType.Snowman:
                if (isFromChameleonEnemy)
                {
                    mainGameData.hasTriggeredEnemyThisLevel = true;
                    mainGameData.doublebladeStunThisEnemyReveal = false;
                    if (isEnemy)
                    {
                        // 不触发攻击/眩晕
                        SFXManager.Instance?.PlayEnemySound("snowman", "normal");
                    }
                    break;
                }
                // snowman boss处理
                if (isEnemy)
                {
                    // 播放snowman normal音效
                    SFXManager.Instance?.PlayEnemySound("snowman", "normal");
                    // 处理敌人图片切换和伤害逻辑
                    StartCoroutine(HandleEnemyRevealed(row, col, cardType));
                }
                // 执行snowman boss的特殊逻辑（照射计数等）
                // 保存isFlashlightRevealing的值，因为可能在协程中被重置
                bool wasFlashlightRevealingForSnowman = isFlashlightRevealing;
                bool isEffectiveLightForSnowman = IsBossEffectiveLightReveal(row, wasFlashlightRevealingForSnowman);
                HandleSnowmanBossRevealed(row, col, isEffectiveLightForSnowman);
                break;
            case CardType.SnowsnakeBody:
                if (isFromChameleonEnemy)
                {
                    mainGameData.hasTriggeredEnemyThisLevel = true;
                    mainGameData.doublebladeStunThisEnemyReveal = false;
                    if (isEnemy)
                    {
                        string enemyIdentifier = CardInfoManager.Instance?.GetEnemyIdentifier(cardType);
                        if (!string.IsNullOrEmpty(enemyIdentifier))
                        {
                            SFXManager.Instance?.PlayEnemySound(enemyIdentifier, "normal");
                        }
                    }
                    break;
                }
                tutorialManager?.ShowTutorial("enemy");
                if (isEnemy)
                {
                    string enemyIdentifier = CardInfoManager.Instance?.GetEnemyIdentifier(cardType);
                    if (!string.IsNullOrEmpty(enemyIdentifier))
                    {
                        SFXManager.Instance?.PlayEnemySound(enemyIdentifier, "normal");
                    }
                    StartCoroutine(HandleEnemyRevealed(row, col, cardType));
                }
                break;
            case CardType.SnowsnakeHead:
                if (isFromChameleonEnemy)
                {
                    mainGameData.hasTriggeredEnemyThisLevel = true;
                    mainGameData.doublebladeStunThisEnemyReveal = false;
                    if (isEnemy)
                    {
                        string enemyIdentifier = CardInfoManager.Instance?.GetEnemyIdentifier(cardType);
                        if (!string.IsNullOrEmpty(enemyIdentifier))
                        {
                            SFXManager.Instance?.PlayEnemySound(enemyIdentifier, "normal");
                        }
                    }
                    break;
                }
                // snowsnake boss head也表现为敌人：灯光翻开视为“安全翻开”
                if (isEnemy)
                {
                    string enemyIdentifier = CardInfoManager.Instance?.GetEnemyIdentifier(cardType);
                    if (!string.IsNullOrEmpty(enemyIdentifier))
                    {
                        SFXManager.Instance?.PlayEnemySound(enemyIdentifier, "normal");
                    }
                    StartCoroutine(HandleEnemyRevealed(row, col, cardType));
                }
                // 保存isFlashlightRevealing的值，因为可能在协程中被重置
                bool wasFlashlightRevealingForSnowsnake = isFlashlightRevealing;
                bool isEffectiveLightForSnowsnake = IsBossEffectiveLightReveal(row, wasFlashlightRevealingForSnowsnake);
                HandleSnowsnakeBossRevealed(row, col, isEffectiveLightForSnowsnake);
                break;
            case CardType.Horribleman:
                if (isFromChameleonEnemy)
                {
                    mainGameData.hasTriggeredEnemyThisLevel = true;
                    mainGameData.doublebladeStunThisEnemyReveal = false;
                    if (isEnemy)
                    {
                        // 不触发攻击/眩晕
                        SFXManager.Instance?.PlayEnemySound("horribleman", "normal");
                    }
                    break;
                }
                // horribleman boss处理
                if (isEnemy)
                {
                    // 播放horribleman normal音效
                    SFXManager.Instance?.PlayEnemySound("horribleman", "normal");
                    // 处理敌人图片切换和伤害逻辑
                    StartCoroutine(HandleEnemyRevealed(row, col, cardType));
                }
                // 执行horribleman boss的特殊逻辑（捕获计数等）
                HandleHorriblemanBossRevealed(row, col);
                break;
            case CardType.Shadow:
                if (isFromChameleonEnemy)
                {
                    mainGameData.hasTriggeredEnemyThisLevel = true;
                    mainGameData.doublebladeStunThisEnemyReveal = false;
                    if (isEnemy)
                    {
                        // 不触发攻击/眩晕
                        SFXManager.Instance?.PlayEnemySound("shadow", "normal");
                    }
                    break;
                }
                // shadow boss处理
                if (isEnemy)
                {
                    // 播放shadow normal音效
                    SFXManager.Instance?.PlayEnemySound("shadow", "normal");
                    // 处理敌人图片切换和伤害逻辑
                    StartCoroutine(HandleEnemyRevealed(row, col, cardType));
                }
                // 执行shadow boss的特殊逻辑（揭示即推进）
                HandleShadowBossRevealed(row, col);
                break;
            case CardType.Ghost:
                // ghost boss处理：先按普通敌人结算（受灯光/攻击），再尝试“换位遁形”
                if (isFromChameleonEnemy)
                {
                    mainGameData.hasTriggeredEnemyThisLevel = true;
                    mainGameData.doublebladeStunThisEnemyReveal = false;
                    if (isEnemy)
                    {
                        // 不触发攻击/眩晕
                        SFXManager.Instance?.PlayEnemySound("ghost", "normal");
                    }
                    break;
                }
                if (isEnemy)
                {
                    SFXManager.Instance?.PlayEnemySound("ghost", "normal");
                    StartCoroutine(HandleEnemyRevealed(row, col, cardType, () => ResolveGhostRelocateOrCapture(row, col)));
                }
                else
                {
                    ResolveGhostRelocateOrCapture(row, col);
                }
                break;
            case CardType.Flashlight:
                mainGameData.flashlights++;
                ShowFloatingTextForResource("light", 1);
                CreateCardFlyEffect(row, col, "light");
                // 显示light教程（翻出flashLight）
                tutorialManager?.ShowTutorial("light");
                // 播放手电筒卡音效
                SFXManager.Instance?.PlayCardRevealSound("flashlight");
                break;
            case CardType.Hint:
                ShowHint(row, col);
                // 播放提示卡音效
                SFXManager.Instance?.PlayCardRevealSound("hint");
                break;
            case CardType.PoliceStation:
                // 播放警察局卡音效
                SFXManager.Instance?.PlayCardRevealSound("police");
                break;
            case CardType.Player:
                break;
            case CardType.Bell:
                // 翻开Bell卡后显示ringBell按钮
                uiManager?.ShowBellButton();
                // 显示bell教程（翻出bell）
                tutorialManager?.ShowTutorial("bell");
                CreateCardFlyEffect(row, col, "bell");
                // 触发升级项效果
                upgradeManager?.OnBellRevealed();
                upgradeManager?.OnBellFound();
                // 播放铃铛卡音效
                SFXManager.Instance?.PlayCardRevealSound("bell");
                break;
            case CardType.Iceground:
                // 翻开iceground时，如果四个方向有还未翻开的安全格子，直接翻开
                RevealAdjacentSafeTiles(row, col);
                // 播放冰面卡音效
                SFXManager.Instance?.PlayCardRevealSound("iceground");
                break;
            case CardType.Sign:
                // Sign卡翻开时不需要特殊处理，箭头方向在生成时已设置
                // 播放标志卡音效
                SFXManager.Instance?.PlayCardRevealSound("sign");
                break;
            case CardType.Door:
                HandleDoorRevealed();
                // 播放门卡音效
                SFXManager.Instance?.PlayCardRevealSound("door");
                break;
            case CardType.Alarm:
                // 处理alarm卡牌的特殊逻辑
                StartCoroutine(HandleAlarmRevealed(row, col));
                SFXManager.Instance?.PlayCardRevealSound("alarm");
                // 播放alarm卡音效
                break;
            case CardType.Doubleblade:
                mainGameData.doublebladeNextRevealPending = true;
                SFXManager.Instance?.PlayCardRevealSound("doubleblade");
                break;
            case CardType.Magnet:
                StartCoroutine(HandleMagnetRevealed(row, col));
                SFXManager.Instance?.PlayCardRevealSound("magnet");
                break;
            case CardType.Carrot:
                mainGameData.shield++;
                ShowFloatingTextForResource("shield", 1);
                uiManager?.UpdateUI();
                SFXManager.Instance?.PlayCardRevealSound("carrot");
                break;
        }
        
        // patternRecognition: 当翻开safe tile时，增加sequence计数
        // safe tile包括：所有非isEnemy的tile，以及用light或churchRing翻开的isEnemy（因为不会造成伤害）
        {
            
        bool isPatternSafeTile = isSafeTile || (isEnemy && (isFlashlightRevealing || isChurchRingRevealing));
        if (isPatternSafeTile)
        {
            upgradeManager?.OnSafeTileRevealed();
        }
        else if (isEnemy && !isFlashlightRevealing && !isChurchRingRevealing)
        {
            // 不用light或churchRing翻开isEnemy时，重置sequence
            upgradeManager?.OnNonSafeTileRevealed();
        }
        
        }
        // steadyHand: 当用light翻开safe tile时，reveal相邻的safe tile
        if (isFlashlightRevealing && isSafeTile && isFirst)
        {
            upgradeManager?.OnLightRevealSafeTile(row, col);
        }
        
        // 检查是否是最后一个tile或最后一个safe tile
        if (isLastTile)
        {
            upgradeManager?.OnLastTileRevealed();
        }
        
        if (isLastSafeTile)
        {
            upgradeManager?.OnLastSafeTileRevealed();
        }
        
        // 检查horribleman boss是否应该出现
        CheckAndSpawnHorriblemanBoss();
        
        // 检查shadow boss是否应该出现
        CheckAndSpawnShadowBoss();
        
        // 检查ghost boss是否应该出现
        CheckAndSpawnGhostBoss();
        
        // showRowToGift: 检查是否揭露完一整行
        upgradeManager?.OnRowCompleted(row);
        
        // enclose: 检查未揭露的敌人相邻的格子是否都被揭示了
        upgradeManager?.CheckEnclose(row, col);
        
        uiManager?.UpdateUI();
        uiManager?.UpdateEnemyCount();
        uiManager?.UpdateHintCount();
    }
    
    private void CheckAndSpawnHorriblemanBoss()
    {
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        bool isHorriblemanBossLevel = levelInfo.boss != null && BossLevelIds.IsHorriblemanStyleBoss(levelInfo.boss);
        
        if (isHorriblemanBossLevel && boardManager != null)
        {
            // 检查是否所有普通敌人都被击败了
            if (boardManager.AreAllRegularEnemiesDefeated())
            {
                // 检查horribleman boss是否已经生成
                Vector2Int bossPos = boardManager.GetBossPosition(CardType.Horribleman);
                if (bossPos.x < 0)
                {
                    // 生成horribleman boss
                    boardManager.SpawnHorriblemanBoss();
                }
            }
        }
    }

    private void CheckAndSpawnShadowBoss()
    {
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        bool isShadowBossLevel = levelInfo.boss != null && levelInfo.boss.ToLower() == "shadow";
        
        if (isShadowBossLevel && boardManager != null)
        {
            // 检查是否所有普通敌人都被击败了
            if (boardManager.AreAllRegularEnemiesDefeated())
            {
                // 检查shadow boss是否已经生成
                Vector2Int bossPos = boardManager.GetBossPosition(CardType.Shadow);
                if (bossPos.x < 0)
                {
                    // 生成shadow boss
                    boardManager.SpawnShadowBoss();
                }
            }
        }
    }
    
    private void CheckAndSpawnGhostBoss()
    {
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        bool isGhostBossLevel = levelInfo.boss != null && levelInfo.boss.ToLower() == "ghost";
        
        if (isGhostBossLevel && boardManager != null)
        {
            // 检查是否所有普通敌人都被击败了
            if (boardManager.AreAllRegularEnemiesDefeated())
            {
                // 检查ghost boss是否已经生成
                Vector2Int bossPos = boardManager.GetBossPosition(CardType.Ghost);
                if (bossPos.x < 0)
                {
                    // 生成ghost boss
                    boardManager.SpawnGhostBoss();
                }
            }
        }
    }
    
    private void RevealAdjacentSafeTiles(int row, int col)
    {
        if (boardManager == null) return;
        
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        
        // 收集所有相邻的未翻开安全格子
        List<Vector2Int> adjacentSafeTiles = new List<Vector2Int>();
        for (int i = 0; i < 4; i++)
        {
            int newRow = row + dx[i];
            int newCol = col + dy[i];
            
            if (newRow >= 0 && newRow < boardManager.GetCurrentRow() && newCol >= 0 && newCol < boardManager.GetCurrentCol())
            {
                // 检查是否是未翻开的安全格子（非isEnemy）
                if (!boardManager.IsRevealed(newRow, newCol))
                {
                    if (!boardManager.IsEnemyCard(newRow, newCol))
                    {
                        adjacentSafeTiles.Add(new Vector2Int(newRow, newCol));
                    }
                }
            }
        }
        
        // 只reveal一个相邻的安全格子
        if (adjacentSafeTiles.Count > 0)
        {
            Vector2Int selectedTile = adjacentSafeTiles[Random.Range(0, adjacentSafeTiles.Count)];
            CardType revealedCardType = boardManager.GetCardType(selectedTile.x, selectedTile.y);
            boardManager.RevealTile(selectedTile.x, selectedTile.y);
            
            // 如果reveal的格子是iceground，递归处理
            if (revealedCardType == CardType.Iceground)
            {
                RevealAdjacentSafeTiles(selectedTile.x, selectedTile.y);
            }
        }
    }
    
    public void ShowHint(int row, int col)
    {
        currentHintPosition = new Vector2Int(row, col);
        string hint = boardManager.GetHintContent(row, col);
        if (!string.IsNullOrEmpty(hint))
        {
            uiManager?.ShowHint(hint);
        }
    }
    
    public bool IsUsingFlashlight()
    {
        return isUsingFlashlight;
    }
    
    public void UseFlashlight()
    {
        if (mainGameData.flashlights > 0 && !isUsingFlashlight)
        {
            isUsingFlashlight = true;
            uiManager?.UpdateFlashlightButton();
            CursorManager.Instance?.SetFlashlightCursor();
            boardManager?.UpdateAllTilesVisual();
            // light 音效由 CursorManager 管理
        }
    }
    
    public void UseFlashlightToReveal(int row, int col)
    {
        // 如果玩家输入被禁用，不允许使用手电筒
        if (isPlayerInputDisabled)
        {
            return;
        }
        
        if (isUsingFlashlight && mainGameData.flashlights > 0)
        {
            if (!boardManager.IsRevealed(row, col))
            {
                // 检查是否是第二关，且tutorialForceBoard开启
                int currentLevel = mainGameData.currentLevel;
                bool tutorialForceBoard = TutorialManager.Instance != null && TutorialManager.Instance.tutorialForceBoard;
                bool isLevel2 = currentLevel == 2 && tutorialForceBoard;
                
                if (isLevel2)
                {
                    Vector2Int playerPos = boardManager.GetPlayerPosition();
                    // 如果使用灯笼试图点击不是上方或在上方的未翻开的格子，显示hint1_5
                    if (col != playerPos.y)
                    {
                        if (TutorialManager.Instance != null)
                        {
                            TutorialManager.Instance.ShowTutorial("hint1_5",true);
                        }
                        return; // 灯笼不被使用，格子也不会被翻出来
                    }
                    else
                    {
                            TutorialManager.Instance.tutorialForceBoard = false;
                        
                    }
                    
                    // 如果试图不使用灯笼就点击上方或再上方的格子，显示hint1_4
                    // 这个检查在RevealTile中已经做了，这里不需要重复
                }
                
                // 无论是不是敌人，都减一手电筒
                mainGameData.flashlights--;
                ShowFloatingTextForResource("light", -1);
                
                // 标记正在使用手电筒翻开
                isFlashlightRevealing = true;
                
                // 翻开tile（如果是敌人，OnTileRevealed中会跳过伤害）；玩家用提灯点开算玩家翻开
                boardManager.RevealTile(row, col, true, false, true);
                
                isFlashlightRevealing = false;
                
                // 成功翻开后，保持手电筒状态（点击拖动保持），但如果flashlights用完了，则退出
                if (mainGameData.flashlights <= 0)
                {
                    isUsingFlashlight = false;
                    uiManager?.UpdateFlashlightButton();
                    CursorManager.Instance?.ResetCursor();
                }
                else
                {
                    // 保持激活状态，允许继续使用
                    uiManager?.UpdateFlashlightButton();
                }
            }
            // 如果tile已经翻开，不退出手电筒状态，允许继续点击其他tile
        }
    }
    
    
    public void CancelFlashlight()
    {
        if (isUsingFlashlight)
        {
            isUsingFlashlight = false;
            uiManager?.UpdateFlashlightButton();
            CursorManager.Instance?.ResetCursor();
            boardManager?.UpdateAllTilesVisual();
            // light 音效由 CursorManager 管理
        }
    }
    
    public void EndTurn()
    {
        // 显示win教程（第一次点击ringbell过关）
        tutorialManager?.ShowTutorial("win");
        
        // 播放完成关卡音效
        SFXManager.Instance?.PlaySFX("finishLevel");
        
        // 停止抖动
        ShakeManager.Instance?.StopShake();
        
        // 所有gift变成gold
        int giftAmount = mainGameData.gifts;
        mainGameData.coins += giftAmount;
        if (giftAmount > 0)
        {
            ShowFloatingTextForResource("gift", -giftAmount);
            ShowFloatingTextForResource("coin", giftAmount);
        }
        mainGameData.gifts = 0;
        uiManager?.UpdateUI();
        
        // noOneNotice: 若不触发任何敌人就离开本层，获得 2 金币
        upgradeManager?.OnLevelEnd();
        
        // 清空board
        if (boardManager != null)
        {
            boardManager.ClearBoard();
        }
        
        // crack 关卡走普通胜利，但在 scene 内最后一次 crack 时播放 after story（与 EndBossBattle 分支一致）
        LevelInfo endTurnLevelInfo = LevelManager.Instance != null ? LevelManager.Instance.GetCurrentLevelInfo() : null;
        bool isCrackBossLevelEnd = endTurnLevelInfo != null && !string.IsNullOrEmpty(endTurnLevelInfo.boss) &&
                                   endTurnLevelInfo.boss.ToLower() == "crack";
        if (isCrackBossLevelEnd && storyManager != null && CSVLoader.Instance != null &&
            CSVLoader.Instance.storyDict.ContainsKey("afterCrack") &&
            LevelManager.Instance.IsLastBossLevelInSceneForStory(mainGameData.currentScene, "crack"))
        {
            bool lastInScene = LevelManager.Instance.IsLastLevelInScene(mainGameData.currentScene);
            if (lastInScene)
            {
                storyManager.PlayStory("afterCrack", () =>
                {
                    shopManager?.HideShop();
                    ShowSceneVictory();
                });
                return;
            }
            storyManager.PlayStory("afterCrack", () => { shopManager?.ShowShop(); });
            return;
        }
        
        shopManager?.ShowShop();
    }
    
    public void NextLevel()
    {
        // 检查是否是当前scene的最后一个level
        string currentScene = mainGameData.currentScene;
        bool isLastLevelInScene = LevelManager.Instance.IsLastLevelInScene(currentScene);
        
        if (isLastLevelInScene)
        {
            // 是scene的最后一个level，显示victory（可能先播放story）
            shopManager?.HideShop();
            ShowSceneVictory();
        }
        else
        {
            // 继续下一个level
            mainGameData.currentLevel++;
            shopManager?.HideShop();
            StartNewLevel();
        }
    }
    
    /// <summary>
    /// 显示scene胜利（播放story后显示victory）
    /// </summary>
    private void ShowSceneVictory()
    {
        string currentScene = mainGameData.currentScene;
        
        // 检查是否是第一次通关这个scene
        bool isFirstTime = false;
        if (!string.IsNullOrEmpty(currentScene) && GameManager.Instance != null)
        {
            if (!GameManager.Instance.gameData.completedScenes.Contains(currentScene))
            {
                isFirstTime = true;
                GameManager.Instance.gameData.completedScenes.Add(currentScene);
                GameManager.Instance.SaveGameData();
            }
        }

        if (alwaysShowUnlock)
        {
            isFirstTime = true;
        }
        // 如果是第一次通关，检查是否有解锁的内容
        if (isFirstTime && !string.IsNullOrEmpty(currentScene))
        {
            // 检查所有card和upgrade，看是否有scene是当前scene的
            bool hasUnlockContent = false;
            
            // 检查card
            if (CSVLoader.Instance != null)
            {
                foreach (var kvp in CSVLoader.Instance.cardDict)
                {
                    CardInfo cardInfo = kvp.Value;
                    if (!string.IsNullOrEmpty(cardInfo.scene) && cardInfo.scene == currentScene)
                    {
                        hasUnlockContent = true;
                        break;
                    }
                }
            }
            
            // 检查upgrade
            if (!hasUnlockContent && CSVLoader.Instance != null)
            {
                foreach (var kvp in CSVLoader.Instance.upgradeDict)
                {
                    UpgradeInfo upgradeInfo = kvp.Value;
                    if (!string.IsNullOrEmpty(upgradeInfo.scene) && upgradeInfo.scene == currentScene)
                    {
                        hasUnlockContent = true;
                        break;
                    }
                }
            }
            
            // 如果有解锁内容，显示unlockMenu
            if (hasUnlockContent && UnlockMenu.Instance != null)
            {
                UnlockMenu.Instance.ShowUnlockMenu(currentScene, () =>
                {
                    // unlockMenu关闭后，继续显示victory
                    ContinueAfterUnlockMenu();
                });
                return;
            }
        }
        
        // 继续正常的victory流程
        ContinueAfterUnlockMenu();
    }
    
    /// <summary>
    /// 在unlockMenu关闭后继续显示victory
    /// </summary>
    private void ContinueAfterUnlockMenu()
    {
        string currentScene = mainGameData.currentScene;
        
        // 尝试播放scene对应的story（identifier为scene的identifier）
        if (storyManager != null && !string.IsNullOrEmpty(currentScene))
        {
            // 检查是否有对应的story
            if (CSVLoader.Instance != null && CSVLoader.Instance.storyDict.ContainsKey(currentScene))
            {
                // 播放story，播放完成后显示victory
                storyManager.PlayStory(currentScene, () =>
                {
                    ShowVictory();
                });
                return;
            }
        }
        
        // 如果没有story，直接显示victory
        ShowVictory();
    }
    
    // 在离开board前，先reveal所有未翻开的卡牌，然后显示全屏按钮等待玩家点击
    public void RevealAllCardsBeforeLeaving(System.Action onContinue)
    {
        if (boardManager == null)
        {
            onContinue?.Invoke();
            return;
        }
        
        // 检查board是否已经被清空（tiles为null或没有未翻开的卡牌）
        if (boardManager.GetCurrentRow() <= 0 || boardManager.GetCurrentCol() <= 0)
        {
            onContinue?.Invoke();
            return;
        }
        
        // 检查是否有未翻开的卡牌
        bool hasUnrevealedCards = false;
        for (int row = 0; row < boardManager.GetCurrentRow(); row++)
        {
            for (int col = 0; col < boardManager.GetCurrentCol(); col++)
            {
                if (!boardManager.IsRevealed(row, col))
                {
                    hasUnrevealedCards = true;
                    break;
                }
            }
            if (hasUnrevealedCards) break;
        }
        
        if (!hasUnrevealedCards)
        {
            onContinue?.Invoke();
            return;
        }
        
        // 禁用玩家输入
        isPlayerInputDisabled = true;
        
        // Reveal所有未翻开的卡牌
        boardManager.RevealAllUnrevealedCards(() =>
        {
            // 动画完成后，创建全屏透明按钮
            CreateFullscreenClickButton(() =>
            {
                // 玩家点击后，恢复输入并继续
                isPlayerInputDisabled = false;
                onContinue?.Invoke();
            });
        });
    }
    
    // 创建全屏透明按钮
    private void CreateFullscreenClickButton(System.Action onClick)
    {
        // 如果已经存在，先销毁
        if (fullscreenClickButton != null)
        {
            Destroy(fullscreenClickButton);
        }
        
        // 获取Canvas
        Canvas canvas = GameManager.Instance.canvas;
        if (canvas == null)
        {
            onClick?.Invoke();
            return;
        }
        
        // 创建全屏透明按钮
        GameObject buttonObj = new GameObject("FullscreenClickButton");
        buttonObj.transform.SetParent(canvas.transform, false);
        
        // 添加RectTransform
        RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        
        // 添加Image组件（透明）
        Image image = buttonObj.AddComponent<Image>();
        image.color = new Color(0, 0, 0, 0); // 完全透明
        image.raycastTarget = true; // 可以接收点击
        
        // 添加Button组件
        Button button = buttonObj.AddComponent<Button>();
        button.onClick.AddListener(() =>
        {
            // 点击后销毁按钮并执行回调
            if (fullscreenClickButton != null)
            {
                Destroy(fullscreenClickButton);
                fullscreenClickButton = null;
            }
            onClick?.Invoke();
        });
        
        fullscreenClickButton = buttonObj;
        
        // 确保按钮在最上层
        buttonObj.transform.SetAsLastSibling();
    }
    
    private void GameOver()
    {
        // 播放游戏结束音效
        SFXManager.Instance?.PlaySFX("gameover");
        
        // 停止抖动
        ShakeManager.Instance?.StopShake();
        
        // 使用LoseMenu显示失败菜单，始终显示retry按钮
        if (LoseMenu.Instance != null)
        {
            LoseMenu.Instance.ShowLoseMenu(true);
        }
        else
        {
            // 如果没有LoseMenu，使用旧的UIManager方法
            uiManager?.ShowGameOver(true);
        }
    }
    
    // CashOut: 统一处理所有礼物转换为金币的效果（在bossIcon出现前触发）
    private void TriggerCashOutEffect()
    {
        if (upgradeManager != null && upgradeManager.HasUpgrade("CashOut"))
        {
            int giftAmount = mainGameData.gifts;
            if (giftAmount > 0)
            {
                mainGameData.coins += giftAmount;
                mainGameData.gifts = 0;
                ShowFloatingText("gift", -giftAmount);
                ShowFloatingText("coin", giftAmount);
                // 播放coin卡牌翻出的音效
                SFXManager.Instance?.PlayCardRevealSound("coin");
                uiManager?.UpdateUI();
                uiManager?.TriggerUpgradeAnimation("CashOut");
            }
        }
    }
    
    private void HandleDoorRevealed()
    {
        StartCoroutine(HandleDoorRevealedCoroutine());
    }
    
    private IEnumerator HandleDoorRevealedCoroutine()
    {
        // 禁用玩家点击
        isPlayerInputDisabled = true;
        
        // CashOut: 在bossIcon出现前统一触发
        TriggerCashOutEffect();
        
        // 等待1秒
        yield return new WaitForSeconds(1f);
        
        nunDoorCount++;
        
        // 检查scene中是否还有nun boss关卡
        string currentScene = mainGameData.currentScene;
        bool hasMoreNunBoss = LevelManager.Instance.HasMoreBossLevelsInScene(currentScene, "nun");
        
        if (hasMoreNunBoss)
        {
            // 如果还有nun boss关卡，显示"Keep Running"弹窗
            if (DialogPanel.Instance != null)
            {
                // 保存回调，不直接执行
                pendingBossCallback = () =>
                {
                    // 点击boss按钮后进入商店，然后进入下一关
                    EndBossBattle();
                };
                // 使用 Localization
                var nunRunningLocalizedString = new LocalizedString("GameText", "NunKeepRunning");
                nunRunningLocalizedString.Arguments = new object[] { 1 }; // 显示剩余1个门
                var nunRunningHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(nunRunningLocalizedString.TableReference, nunRunningLocalizedString.TableEntryReference, nunRunningLocalizedString.Arguments);
                string nunRunningText = nunRunningHandle.WaitForCompletion();
                DialogPanel.Instance.ShowDialog(nunRunningText, null, null, false, false);
                
                // 启用boss按钮，让玩家可以点击
                uiManager?.SetBossIconInteractable(true);
            }
        }
        else
        {
            // 这是最后一个nun boss关卡，显示"You escape from the nun!"
            if (DialogPanel.Instance != null)
            {
                // 保存回调，不直接执行
                pendingBossCallback = () =>
                {
                    // 进入shop的流程
                    EndBossBattle();
                };
                // 使用 Localization
                var nunEscapedLocalizedString = new LocalizedString("GameText", "NunEscaped");
                var nunEscapedHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(nunEscapedLocalizedString.TableReference, nunEscapedLocalizedString.TableEntryReference);
                string nunEscapedText = nunEscapedHandle.WaitForCompletion();
                DialogPanel.Instance.ShowDialog(nunEscapedText, null, null, false, false);
                
                // 启用boss按钮，让玩家可以点击
                uiManager?.SetBossIconInteractable(true);
            }
        }
        
        // 显示弹窗后，恢复玩家点击（让玩家可以继续翻牌）
        isPlayerInputDisabled = false;
    }

    public float enemyRevealWaitTime = 0.5f;

    /// <summary>
    /// Boss 关卡里用于“照射/受伤”计数的统一判定（不产生副作用）。
    /// 包含：手电、churchRing、doubleblade 眩晕，以及 churchLight 可触发条件。
    /// </summary>
    private bool IsBossEffectiveLightReveal(int row, bool wasFlashlightRevealing)
    {
        if (wasFlashlightRevealing || isChurchRingRevealing || mainGameData.doublebladeStunThisEnemyReveal)
            return true;

        if (upgradeManager == null || !upgradeManager.HasUpgrade("churchLight"))
            return false;
        if (mainGameData.churchLightUsedThisLevel || boardManager == null)
            return false;

        int cols = boardManager.GetCurrentCol();
        for (int c = 0; c < cols; c++)
        {
            if (boardManager.GetCardType(row, c) == CardType.PoliceStation)
                return true;
        }

        return false;
    }
    // 处理敌人翻开的逻辑：先显示identifier图片0.3秒，然后切换到对应图片
    private IEnumerator HandleEnemyRevealed(int row, int col, CardType cardType, System.Action onResolved = null)
    {
        // 在协程开始时保存状态，因为isFlashlightRevealing可能在等待期间被重置
        bool wasFlashlightRevealing = isFlashlightRevealing;
        bool wasChurchRingRevealing = isChurchRingRevealing;
        
        // 先显示identifier图片（已经通过SetFrontSprite显示了）
        // 等待0.3秒
        yield return new WaitForSeconds(enemyRevealWaitTime);
        
        // 获取Tile对象
        Tile tile = boardManager?.GetTile(row, col);
        if (tile == null) yield break;
        
        // churchLight: 每关一次，不使用灯光揭示敌人时：如果同一行有教堂，则眩晕敌人
        bool churchLightTriggered = false;
        if (!wasFlashlightRevealing && !wasChurchRingRevealing)
        {
            churchLightTriggered = upgradeManager?.CheckChurchLight(row, col) ?? false;
        }
        
        bool doublebladeStun = mainGameData.doublebladeStunThisEnemyReveal;
        if (doublebladeStun)
            mainGameData.doublebladeStunThisEnemyReveal = false;
        
        // 判断是否是用灯光照开的（或churchRing的升级项翻开的，即不扣血的方式）
        bool isSafeReveal = wasFlashlightRevealing || wasChurchRingRevealing || churchLightTriggered || doublebladeStun;
        
        // 根据是否用灯光照开切换到对应的图片
        Sprite targetSprite = null;
        if (isSafeReveal)
        {
            // 用灯光照开的，切换到identifier_hurt
            targetSprite = CardInfoManager.Instance?.GetEnemyHurtSprite(cardType);
        }
        else
        {
            // 不是用灯光照开的，切换到identifier_atk
            targetSprite = CardInfoManager.Instance?.GetEnemyAtkSprite(cardType);
        }
        
        // 获取敌人identifier并播放对应音效
        string enemyIdentifier = CardInfoManager.Instance?.GetEnemyIdentifier(cardType);
        
        mainGameData.hasTriggeredEnemyThisLevel = true;
        // 处理伤害逻辑
        if (isSafeReveal)
        {
            // 如果使用手电筒或churchRing效果，敌人不造成伤害，也不抢礼物
            // 播放hurt音效（被灯光照开）
            if (!string.IsNullOrEmpty(enemyIdentifier))
            {
                SFXManager.Instance?.PlayEnemySound(enemyIdentifier, "hurt");
            }
            if (wasFlashlightRevealing)
            {
                // 触发chaseGrinchGiveGift升级项效果（只有用light翻开时才触发）
                upgradeManager?.OnChaseGrinchWithLight();
            }
            // churchRing效果：等同于用light翻开，但不消耗light，也不触发chaseGrinchGiveGift
            
            // 如果对应的图片不存在，就保持之前的图片（不切换）
            if (targetSprite != null)
            {
                // 安全翻开，不是攻击动画
                tile.SwitchEnemySprite(targetSprite, true, false, onResolved);
            }
            else
            {
                onResolved?.Invoke();
            }
        }
        else
        {
            // 不用灯光照开的，造成伤害
            // 播放atk音效（攻击时）
            if (!string.IsNullOrEmpty(enemyIdentifier))
            {
                SFXManager.Instance?.PlayEnemySound(enemyIdentifier, "atk");
            }
            ApplyEnemyAttackDamageAndPenalties(row, col);
            
            // 如果对应的图片不存在，就保持之前的图片（不切换）
            if (targetSprite != null)
            {
                // 如果不是安全翻开（即敌人会攻击），传递isAttackAnimation=true，并在动画完成后检查游戏结束
                bool isAttackAnimation = true;
                tile.SwitchEnemySprite(targetSprite, true, isAttackAnimation, () =>
                {
                    // 动画完成后检查游戏结束
                    CheckGameOverAfterEnemyAnimation();
                    onResolved?.Invoke();
                });
            }
            else
            {
                // 如果没有图片切换，立即检查游戏结束
                CheckGameOverAfterEnemyAnimation();
                onResolved?.Invoke();
            }
        }
    }

    private void ResolveGhostRelocateOrCapture(int row, int col)
    {
        if (boardManager != null && boardManager.TryRelocateGhostBossOnReveal(row, col))
        {
            boardManager.ResetRevealedHintsForGhostBoss();
            // 本次触发了ghost位移，不进入boss推进逻辑
            mainGameData.hasTriggeredEnemyThisLevel = true;
            uiManager?.UpdateUI();
            uiManager?.UpdateEnemyCount();
            uiManager?.UpdateHintCount();
            return;
        }

        // 无可用换位目标时，ghost 才算真正被翻开
        HandleGhostBossRevealed(row, col);
    }

    private void ApplyEnemyAttackDamageAndPenalties(int row, int col)
    {
        // 计算伤害
        int damage = 1;
        // greedFragile: 敌人伤害+1
        damage += upgradeManager?.GetDamageModifier() ?? 0;
        // poorPower: 金币为0时，伤害-1（即不扣血）
        bool poorPowerTriggered = false;
        if (upgradeManager?.ShouldReduceDamage() == true)
        {
            damage = 0;
            poorPowerTriggered = true;
        }

        if (poorPowerTriggered)
        {
            // poorPower触发：播放音效和动画
            uiManager?.TriggerUpgradeAnimation("poorPower");
            SFXManager.Instance?.PlaySFX("buyItem");
        }

        int hpBeforeEnemyHit = mainGameData.health;
        TakeDamage(damage); // 内含护盾按点数抵消

        // 护盾完全挡住本次血量伤害时不丢礼物；poorPower 伤害为 0 仍丢礼物
        bool fullyShieldedHp = damage > 0 && mainGameData.health == hpBeforeEnemyHit;
        if (!fullyShieldedHp)
        {
            int lostGifts = mainGameData.gifts;
            mainGameData.gifts = 0;
            if (lostGifts > 0)
                ShowFloatingTextForResource("gift", -lostGifts);
            uiManager?.UpdateUI();
        }
        // 触发lateMending升级项效果：不用light翻开grinch时，reveal相邻的safe tile
        upgradeManager?.OnRevealGrinchWithoutLight(row, col);
    }
    
    /// <summary>
    /// 在敌人攻击动画完成后检查游戏是否结束
    /// </summary>
    private void CheckGameOverAfterEnemyAnimation()
    {
        // Amulet: 在血量归零进入失败结算前，恢复一点血并不进入结算，并且把这个升级项丢弃
        if (mainGameData.health <= 0 && upgradeManager != null && upgradeManager.HasUpgrade("Amulet"))
        {
            mainGameData.health = 1; // 恢复1点血
            ShowFloatingText("health", 1);
            uiManager?.UpdateUI();
            
            // 丢弃Amulet升级项（等同于卖掉，但不获得金币）
            mainGameData.ownedUpgrades.Remove("Amulet");
            uiManager?.UpdateUpgradeDisplay();
            uiManager?.TriggerUpgradeAnimation("Amulet");
            
            // 不进入GameOver，继续游戏
            return;
        }
        
        // BloodMoney: 在护身符之后检查，如果血量<=0且金币>=10，消耗10金币获得1点血
        if (mainGameData.health <= 0 && upgradeManager != null && upgradeManager.HasUpgrade("BloodMoney"))
        {
            if (mainGameData.coins >= 10)
            {
                mainGameData.coins -= 10;
                mainGameData.health = 1; // 恢复1点血
                ShowFloatingText("coin", -10);
                ShowFloatingText("health", 1);
                uiManager?.UpdateUI();
                uiManager?.TriggerUpgradeAnimation("BloodMoney");
                
                // 不进入GameOver，继续游戏
                return;
            }
        }
        
        if (mainGameData.health <= 0)
        {
            GameOver();
        }
    }
    
    private IEnumerator HandleAlarmRevealed(int row, int col)
    {
        // 先显示alarm本身的图片（已经通过SetFrontSprite显示了）
        // 等待0.3秒
        yield return new WaitForSeconds(0.3f);
        
        // 检查周围上下左右是否有未翻开的敌人
        bool hasUnrevealedEnemy = false;
        if (boardManager != null)
        {
            int[] dx = { 0, 0, 1, -1 }; // 上下左右
            int[] dy = { 1, -1, 0, 0 };
            
            for (int i = 0; i < 4; i++)
            {
                int newRow = row + dx[i];
                int newCol = col + dy[i];
                
                if (boardManager.IsEnemyCard(newRow, newCol) && !boardManager.IsRevealed(newRow, newCol))
                {
                    hasUnrevealedEnemy = true;
                    break;
                }
            }
        }
        
        // 如果周围有未翻开的敌人，切换到alarm_has图片并触发大的scale效果
        if (hasUnrevealedEnemy)
        {
            Tile tile = boardManager?.GetTile(row, col);
            if (tile != null)
            {
                // 加载alarm_has图片
                string path = "icon/alarm_has";
                Sprite alarmHasSprite = Resources.Load<Sprite>(path);
                
                if (alarmHasSprite != null)
                {
                    // 切换图片
                    tile.SetFrontSprite(alarmHasSprite);
                    // 触发大的scale效果（类似敌人攻击）
                    tile.PlayFrontEffectAnimation(true);
                }
                
                SFXManager.Instance?.PlayCardRevealSound("alarm_reveal");
            }
        }
    }
    
    private void HandleNunBossRevealed(int row, int col)
    {
        // nun boss现在和其他敌人一样，受灯光影响
        // 这里只处理nun boss的特殊逻辑（门等）
    }
    
    private void HandleSnowmanBossRevealed(int row, int col, bool isEffectiveLightReveal)
    {
        StartCoroutine(HandleSnowmanBossRevealedCoroutine(isEffectiveLightReveal));
    }
    
    private IEnumerator HandleSnowmanBossRevealedCoroutine(bool isEffectiveLightReveal)
    {
        // 禁用玩家点击
        isPlayerInputDisabled = true;
        
        // CashOut: 在bossIcon出现前统一触发
        TriggerCashOutEffect();
        
        // 等待1秒
        yield return new WaitForSeconds(1f);
        
        // 任何会让 boss 呈现受伤/眩晕表现的方式都计入“照射”
        if (isEffectiveLightReveal)
        {
            snowmanLightCount++;
            
            // 检查scene中是否还有snowman boss关卡
            string currentScene = mainGameData.currentScene;
            bool hasMoreSnowmanBoss = LevelManager.Instance.HasMoreBossLevelsInScene(currentScene, "snowman");
            
            if (hasMoreSnowmanBoss)
            {
                // 如果还有snowman boss关卡，显示"He's getting hurt, do it again!"
                if (DialogPanel.Instance != null)
                {
                    // 保存回调，不直接执行
                    pendingBossCallback = () =>
                    {
                        // 点击boss按钮后进入商店，然后进入下一关
                        EndBossBattle();
                    };
                    // 使用 Localization
                    var snowmanDazzledLocalizedString = new LocalizedString("GameText", "SnowmanGettingDazzled");
                    snowmanDazzledLocalizedString.Arguments = new object[] { 1 }; // 显示剩余1次
                    var snowmanDazzledHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(snowmanDazzledLocalizedString.TableReference, snowmanDazzledLocalizedString.TableEntryReference, snowmanDazzledLocalizedString.Arguments);
                    string snowmanDazzledText = snowmanDazzledHandle.WaitForCompletion();
                    DialogPanel.Instance.ShowDialog(snowmanDazzledText, null, null, false, false);
                    
                    // 启用boss按钮，让玩家可以点击
                    uiManager?.SetBossIconInteractable(true);
                }
            }
            else
            {
                // 这是最后一个snowman boss关卡，显示"The snowman is stunned!"
                if (DialogPanel.Instance != null)
                {
                    // 保存回调，不直接执行
                    pendingBossCallback = () =>
                    {
                        // 进入shop的流程
                        EndBossBattle();
                    };
                    // 使用 Localization
                    var snowmanStunnedLocalizedString = new LocalizedString("GameText", "SnowmanStunned");
                    var snowmanStunnedHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(snowmanStunnedLocalizedString.TableReference, snowmanStunnedLocalizedString.TableEntryReference);
                    string snowmanStunnedText = snowmanStunnedHandle.WaitForCompletion();
                    DialogPanel.Instance.ShowDialog(snowmanStunnedText, null, null, false, false);
                    
                    // 启用boss按钮，让玩家可以点击
                    uiManager?.SetBossIconInteractable(true);
                }
            }
        }
        else
        {
            // 如果没用light，那么会弹窗"You have to use light to shock him!"
            if (DialogPanel.Instance != null)
            {
                // 保存回调，不直接执行
                pendingBossCallback = () =>
                {
                    // 之后流程一样，只是这次不算boss被照射了
                    RefreshBoard();
                };
                    // 使用 Localization
                    var snowmanUseLightLocalizedString = new LocalizedString("GameText", "SnowmanUseLight");
                    var snowmanUseLightHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(snowmanUseLightLocalizedString.TableReference, snowmanUseLightLocalizedString.TableEntryReference);
                    string snowmanUseLightText = snowmanUseLightHandle.WaitForCompletion();
                    DialogPanel.Instance.ShowDialog(snowmanUseLightText, null, null, false, false);
                    
                    // 启用boss按钮，让玩家可以点击
                    uiManager?.SetBossIconInteractable(true);
            }
        }
        
        // 显示弹窗后，恢复玩家点击（让玩家可以继续翻牌）
        isPlayerInputDisabled = false;
    }

    private void HandleSnowsnakeBossRevealed(int row, int col, bool isEffectiveLightReveal)
    {
        StartCoroutine(HandleSnowsnakeBossRevealedCoroutine(isEffectiveLightReveal));
    }

    private IEnumerator HandleSnowsnakeBossRevealedCoroutine(bool isEffectiveLightReveal)
    {
        // 禁用玩家点击
        isPlayerInputDisabled = true;

        // CashOut: 在bossIcon出现前统一触发
        TriggerCashOutEffect();

        // 等待1秒
        yield return new WaitForSeconds(1f);

        // 任何会让 boss 呈现受伤/眩晕表现的方式都计入“照射”
        if (isEffectiveLightReveal)
        {
            snowsnakeLightCount++;

            // 检查scene中是否还有snowsnake boss关卡（不区分 _ 数字，只要以 snowsnake 开头即可）
            string currentScene = mainGameData.currentScene;
            bool hasMoreSnowsnakeBoss = LevelManager.Instance.HasMoreBossLevelsInScene(currentScene, "snowsnake");

            if (hasMoreSnowsnakeBoss)
            {
                // 如果还有snowsnake boss关卡，显示 SnowsnakeGettingDazzled
                if (DialogPanel.Instance != null)
                {
                    pendingBossCallback = () =>
                    {
                        EndBossBattle();
                    };

                    var localized = new LocalizedString("GameText", "SnowsnakeGettingDazzled");
                    localized.Arguments = new object[] { 1 };
                    var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
                        localized.TableReference,
                        localized.TableEntryReference,
                        localized.Arguments);
                    string text = handle.WaitForCompletion();
                    DialogPanel.Instance.ShowDialog(text, null, null, false, false);

                    uiManager?.SetBossIconInteractable(true);
                }
            }
            else
            {
                // 这是最后一个snowsnake boss关卡，显示 SnowsnakeStunned
                if (DialogPanel.Instance != null)
                {
                    pendingBossCallback = () =>
                    {
                        EndBossBattle();
                    };

                    var localized = new LocalizedString("GameText", "SnowsnakeStunned");
                    var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
                        localized.TableReference,
                        localized.TableEntryReference);
                    string text = handle.WaitForCompletion();
                    DialogPanel.Instance.ShowDialog(text, null, null, false, false);

                    uiManager?.SetBossIconInteractable(true);
                }
            }
        }
        else
        {
            // 如果没用light，那么会弹窗 SnowsnakeUseLight 并重开关卡
            if (DialogPanel.Instance != null)
            {
                pendingBossCallback = () =>
                {
                    RefreshBoard();
                };

                var localized = new LocalizedString("GameText", "SnowsnakeUseLight");
                var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
                    localized.TableReference,
                    localized.TableEntryReference);
                string text = handle.WaitForCompletion();
                DialogPanel.Instance.ShowDialog(text, null, null, false, false);

                uiManager?.SetBossIconInteractable(true);
            }
        }

        // 显示弹窗后，恢复玩家点击（让玩家可以继续翻牌）
        isPlayerInputDisabled = false;
    }
    
    private void HandleHorriblemanBossRevealed(int row, int col)
    {
        StartCoroutine(HandleHorriblemanBossRevealedCoroutine());
    }
    
    private IEnumerator HandleHorriblemanBossRevealedCoroutine()
    {
        // 禁用玩家点击
        isPlayerInputDisabled = true;
        
        // CashOut: 在bossIcon出现前统一触发
        TriggerCashOutEffect();
        
        // 等待1秒
        yield return new WaitForSeconds(1f);
        
        // horribleman boss需要被捕获（用flashlight或不用都可以）
        horriblemanCatchCount++;
        
        // 检查scene中是否还有horribleman boss关卡
        string currentScene = mainGameData.currentScene;
        bool hasMoreHorriblemanBoss = LevelManager.Instance.HasMoreBossLevelsInScene(currentScene, "horribleman");
        
        if (hasMoreHorriblemanBoss)
        {
            // 如果还有horribleman boss关卡，显示"Do one more time!"
            if (DialogPanel.Instance != null)
            {
                // 保存回调，不直接执行
                pendingBossCallback = () =>
                {
                    // 点击boss按钮后进入商店，然后进入下一关
                    EndBossBattle();
                };
                // 使用 Localization
                var horribleManRevealLocalizedString = new LocalizedString("GameText", "HorribleManRevealAgain");
                horribleManRevealLocalizedString.Arguments = new object[] { 1 }; // 显示剩余1次
                var horribleManRevealHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(horribleManRevealLocalizedString.TableReference, horribleManRevealLocalizedString.TableEntryReference, horribleManRevealLocalizedString.Arguments);
                string horribleManRevealText = horribleManRevealHandle.WaitForCompletion();
                DialogPanel.Instance.ShowDialog(horribleManRevealText, null, null, false, false);
                
                // 启用boss按钮，让玩家可以点击
                uiManager?.SetBossIconInteractable(true);
            }
        }
        else
        {
            // 这是最后一个horribleman boss关卡，显示"You caught the horrible man!"
            if (DialogPanel.Instance != null)
            {
                // 保存回调，不直接执行
                pendingBossCallback = () =>
                {
                    // 点击boss按钮后进入商店，然后进入下一关
                    EndBossBattle();
                };
                // 使用 Localization
                var horribleManCaughtLocalizedString = new LocalizedString("GameText", "HorribleManCaught");
                var horribleManCaughtHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(horribleManCaughtLocalizedString.TableReference, horribleManCaughtLocalizedString.TableEntryReference);
                string horribleManCaughtText = horribleManCaughtHandle.WaitForCompletion();
                DialogPanel.Instance.ShowDialog(horribleManCaughtText, null, null, false, false);
                
                // 启用boss按钮，让玩家可以点击
                uiManager?.SetBossIconInteractable(true);
            }
        }
        
        // 显示弹窗后，恢复玩家点击（让玩家可以继续翻牌）
        isPlayerInputDisabled = false;
    }

    private void HandleShadowBossRevealed(int row, int col)
    {
        StartCoroutine(HandleShadowBossRevealedCoroutine());
    }

    private void HandleGhostBossRevealed(int row, int col)
    {
        StartCoroutine(HandleGhostBossRevealedCoroutine());
    }

    private IEnumerator HandleShadowBossRevealedCoroutine()
    {
        // 禁用玩家点击
        isPlayerInputDisabled = true;

        // CashOut: 在bossIcon出现前统一触发
        TriggerCashOutEffect();

        // 等待1秒
        yield return new WaitForSeconds(1f);

        // shadow boss需要被“揭示/捕获”（用flashlight或不用都可以）
        shadowCatchCount++;

        bool isZhLocale = LocalizationSettings.SelectedLocale != null &&
                           !string.IsNullOrEmpty(LocalizationSettings.SelectedLocale.Identifier.Code) &&
                           LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("zh");

        // shadow 只要翻开，统一显示 ShadowRevealAgain
        if (DialogPanel.Instance != null)
        {
            pendingBossCallback = () =>
            {
                // 点击boss按钮后进入商店，然后进入下一关
                EndBossBattle();
            };

            string revealFallbackText = isZhLocale
                ? "干的好！\n点击影子图标继续追赶他。"
                : "Good job!\nClick the shadow icon to keep chasing it.";

            string shadowManRevealText = revealFallbackText;
            try
            {
                var shadowManRevealLocalizedString = new LocalizedString("GameText", "ShadowManRevealAgain");
                shadowManRevealLocalizedString.Arguments = new object[] { 1 };
                var shadowManRevealHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
                    shadowManRevealLocalizedString.TableReference,
                    shadowManRevealLocalizedString.TableEntryReference,
                    shadowManRevealLocalizedString.Arguments);
                string localized = shadowManRevealHandle.WaitForCompletion();
                if (!string.IsNullOrEmpty(localized) && localized != "ShadowManRevealAgain")
                {
                    shadowManRevealText = localized;
                }
            }
            catch
            {
                // Localization key 可能不存在：使用 fallback
            }
            DialogPanel.Instance.ShowDialog(shadowManRevealText, null, null, false, false);

            // 启用boss按钮，让玩家可以点击
            uiManager?.SetBossIconInteractable(true);
        }

        // 显示弹窗后，恢复玩家点击（让玩家可以继续翻牌）
        isPlayerInputDisabled = false;
    }
    
    private IEnumerator HandleGhostBossRevealedCoroutine()
    {
        // 禁用玩家点击
        isPlayerInputDisabled = true;

        // CashOut: 在bossIcon出现前统一触发
        TriggerCashOutEffect();

        // 等待1秒
        yield return new WaitForSeconds(1f);

        if (DialogPanel.Instance != null)
        {
            pendingBossCallback = () =>
            {
                EndBossBattle();
            };

            bool isZhLocale = LocalizationSettings.SelectedLocale != null &&
                              !string.IsNullOrEmpty(LocalizationSettings.SelectedLocale.Identifier.Code) &&
                              LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("zh");

            string fallbackText = isZhLocale
                ? "你抓到了幽灵！\n点击幽灵图标继续。"
                : "You caught the ghost!\nClick the ghost icon to continue.";
            string ghostRevealText = fallbackText;

            try
            {
                var ghostLocalizedString = new LocalizedString("GameText", "GhostRevealAgain");
                var ghostHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
                    ghostLocalizedString.TableReference,
                    ghostLocalizedString.TableEntryReference);
                string localized = ghostHandle.WaitForCompletion();
                if (!string.IsNullOrEmpty(localized) && localized != "GhostRevealAgain")
                {
                    ghostRevealText = localized;
                }
            }
            catch
            {
                // Localization key 可能不存在：使用 fallback
            }

            DialogPanel.Instance.ShowDialog(ghostRevealText, null, null, false, false);
            uiManager?.SetBossIconInteractable(true);
        }

        // 显示弹窗后，恢复玩家点击（让玩家可以继续翻牌）
        isPlayerInputDisabled = false;
    }
    
    // 当bossIcon被点击时调用
    public void OnBossIconClicked()
    {
        // 立即禁用，防止玩家快速双击；下次可点击需等场景格子重新加载（新关卡）后再由逻辑启用
        uiManager?.SetBossIconInteractable(false);
        
        if (pendingBossCallback != null)
        {
            System.Action callback = pendingBossCallback;
            pendingBossCallback = null;
            
            // 在离开board前，先reveal所有未翻开的卡牌
            RevealAllCardsBeforeLeaving(() =>
            {
                // 恢复玩家点击
                isPlayerInputDisabled = false;
                
                // 执行回调
                callback();
            });
        }
    }
    
    // 检查是否有待执行的boss回调
    public bool HasPendingBossCallback()
    {
        return pendingBossCallback != null;
    }
    
    // 显示player受伤动画
    private IEnumerator ShowPlayerHurt()
    {
        // 延迟0.3秒开始播放
        yield return new WaitForSeconds(0.8f);
        
        if (boardManager == null) yield break;
        
        // 获取player位置
        Vector2Int playerPos = boardManager.GetPlayerPosition();
        if (playerPos.x < 0) yield break; // 未找到player
        
        // 获取player tile
        Tile playerTile = boardManager.GetTile(playerPos.x, playerPos.y);
        if (playerTile == null) yield break;
        
        string playerIdentifier = "player";
        if (CardInfoManager.Instance != null)
        {
            playerIdentifier = CardInfoManager.Instance.GetCurrentPlayerIdentifier();
        }
        
        // 优先使用 hero 对应的双下划线受伤图（identifier__hurt），并保留旧命名回退
        Sprite hurtSprite = Resources.Load<Sprite>($"icon/{playerIdentifier}__hurt");
        if (hurtSprite == null)
        {
            hurtSprite = Resources.Load<Sprite>($"icon/{playerIdentifier}_hurt");
        }
        if (hurtSprite == null)
        {
            hurtSprite = Resources.Load<Sprite>("icon/player__hurt");
        }
        if (hurtSprite == null)
        {
            hurtSprite = Resources.Load<Sprite>("icon/player_hurt");
        }
        if (hurtSprite != null)
        {
            // 切换图片并触发frontEffect
            playerTile.SetFrontSprite(hurtSprite);
            playerTile.PlayFrontEffectAnimation(false);
        }
        
        // 等待1秒
        yield return new WaitForSeconds(1f);
        
        // 切换回当前场景 player 对应图片并触发frontEffect
        Sprite normalSprite = Resources.Load<Sprite>($"icon/{playerIdentifier}");
        if (normalSprite == null)
        {
            normalSprite = Resources.Load<Sprite>("icon/player");
        }
        if (normalSprite != null)
        {
            playerTile.SetFrontSprite(normalSprite);
           // playerTile.PlayFrontEffectAnimation(false);
        }
    }
    
    public void RefreshBoard()
    {
        // 禁用bossIcon按钮
        uiManager?.SetBossIconInteractable(false);
        
        // 刷新board（light不会清除，不会进入下一关）
        // 保持当前状态，只重新生成board
        // 如果是nun boss关卡且scene中还有nun boss，需要重新添加door卡
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        bool isNunBossLevel = levelInfo.boss != null && levelInfo.boss.ToLower() == "nun";
        string currentScene = mainGameData.currentScene;
        bool hasMoreNunBoss = isNunBossLevel && LevelManager.Instance.HasMoreBossLevelsInScene(currentScene, "nun");
        
        if (isNunBossLevel && hasMoreNunBoss)
        {
            // 如果还有门没开完，重新添加door卡（从原始字典读取，并设置start为1）
            if (CardInfoManager.Instance != null && CSVLoader.Instance != null && CSVLoader.Instance.cardDict != null)
            {
                CardInfo doorCardInfo = null;
                if (CSVLoader.Instance.cardDict.ContainsKey("door"))
                {
                    doorCardInfo = CSVLoader.Instance.cardDict["door"];
                }
                if (doorCardInfo != null && !CardInfoManager.Instance.HasCard("door"))
                {
                    CardInfo doorCardCopy = CreateCardInfoCopy(doorCardInfo);
                    doorCardCopy.start = 1; // door卡每次只添加1个
                    CardInfoManager.Instance.AddTemporaryCard(doorCardCopy.identifier, doorCardCopy);
                }
            }
        }
        
        if (boardManager != null)
        {
            boardManager.ClearBoard();
            boardManager.GenerateBoard();
        }
        
        // boss战中，每次更新board，upgrade中board初始触发的效果也会触发
        // 比如familiarSteet依然会翻一个牌
        upgradeManager?.OnLevelStart();
        
        uiManager?.UpdateUI();
        uiManager?.UpdateEnemyCount();
        uiManager?.UpdateHintCount();
    }
    
    private void EndBossBattle()
    {
        // 播放击败boss音效
        SFXManager.Instance?.PlaySFX("winBoss");
        
        // 所有gift变成gold
        int giftAmount = mainGameData.gifts;
        mainGameData.coins += giftAmount;
        if (giftAmount > 0)
        {
            ShowFloatingTextForResource("gift", -giftAmount);
            ShowFloatingTextForResource("coin", giftAmount);
        }
        mainGameData.gifts = 0;
        
        // 清空board
        if (boardManager != null)
        {
            boardManager.ClearBoard();
        }
        
        // 结束boss关卡时，移除boss卡和其他新加入的卡，并加回bell卡
        CleanupBossLevelCards();
        
        // 隐藏bossIcon
        uiManager?.HideBossIcon();
        
        // 更新UI
        uiManager?.UpdateUI();
        
        // 检查是否是当前scene的最后一个level
        string currentScene = mainGameData.currentScene;
        bool isLastLevelInScene = LevelManager.Instance.IsLastLevelInScene(currentScene);
        
        // 检查是否是当前scene的最后一个level
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        bool isBossLevel = !string.IsNullOrEmpty(levelInfo.boss);
        bool isNunBossLevel = isBossLevel && levelInfo.boss.ToLower() == "nun";
        bool isSnowmanBossLevel = isBossLevel && levelInfo.boss.ToLower() == "snowman";
        bool isSnowsnakeBossLevel = isBossLevel && levelInfo.boss.ToLower().StartsWith("snowsnake");
        bool isHorriblemanBossLevel = isBossLevel && BossLevelIds.IsHorriblemanStyleBoss(levelInfo.boss);
        bool isShadowBossLevel = isBossLevel && levelInfo.boss.ToLower() == "shadow";
        bool isGhostBossLevel = isBossLevel && levelInfo.boss.ToLower() == "ghost";
        
        // 检查是否是scene中最后一个该boss的关卡
        bool isLastBossLevel = false;
        string afterStoryIdentifier = "";
        if (isNunBossLevel)
        {
            isLastBossLevel = LevelManager.Instance.IsLastBossLevelInSceneForStory(currentScene, "nun");
            afterStoryIdentifier = "afterNun";
        }
        else if (isSnowmanBossLevel)
        {
            isLastBossLevel = LevelManager.Instance.IsLastBossLevelInSceneForStory(currentScene, "snowman");
            afterStoryIdentifier = "afterSnowman";
        }
        else if (isSnowsnakeBossLevel)
        {
            isLastBossLevel = LevelManager.Instance.IsLastBossLevelInSceneForStory(currentScene, "snowsnake");
            afterStoryIdentifier = "afterSnowsnake";
        }
        else if (isHorriblemanBossLevel)
        {
            string bm = levelInfo.boss.ToLowerInvariant();
            if (bm == BossLevelIds.HorriblemanNew)
            {
                isLastBossLevel = LevelManager.Instance.IsLastBossLevelInSceneForStory(currentScene, BossLevelIds.HorriblemanNew);
                afterStoryIdentifier = "afterHorriblemanNew";
            }
            else
            {
                isLastBossLevel = LevelManager.Instance.IsLastBossLevelInSceneForStory(currentScene, BossLevelIds.Horribleman);
                afterStoryIdentifier = "afterHorribleman";
            }
        }
        else if (isShadowBossLevel)
        {
            isLastBossLevel = LevelManager.Instance.IsLastBossLevelInSceneForStory(currentScene, "shadow");
            afterStoryIdentifier = "afterShadow";
        }
        else if (isGhostBossLevel)
        {
            isLastBossLevel = LevelManager.Instance.IsLastBossLevelInSceneForStory(currentScene, "ghost");
            afterStoryIdentifier = "afterGhost";
        }
        
        if (isLastLevelInScene)
        {
            // 是scene的最后一个level，显示victory（可能先播放story）
            // 如果是最后一个boss关卡，先播放boss的after story（如果存在）
            if (isLastBossLevel && !string.IsNullOrEmpty(afterStoryIdentifier) && storyManager != null)
            {
                // 先播放boss的after story，然后播放scene的story，最后显示victory
                storyManager.PlayStory(afterStoryIdentifier, () =>
                {
                    ShowSceneVictory();
                });
            }
            else
            {
                // 直接显示scene victory
                ShowSceneVictory();
            }
        }
        else
        {
            // 不是scene的最后一个level，继续显示shop
            // 如果是最后一个boss关卡，播放对应的after story
            if (isLastBossLevel && !string.IsNullOrEmpty(afterStoryIdentifier) && storyManager != null)
            {
                storyManager.PlayStory(afterStoryIdentifier, () =>
                {
                    shopManager?.ShowShop();
                });
            }
            else
            {
                shopManager?.ShowShop();
            }
        }
    }
    
    private void EndBossBattleForHorribleman()
    {
        // 播放击败boss音效
        SFXManager.Instance?.PlaySFX("winBoss");
        
        // 所有gift变成gold
        int giftAmount = mainGameData.gifts;
        mainGameData.coins += giftAmount;
        if (giftAmount > 0)
        {
            ShowFloatingTextForResource("gift", -giftAmount);
            ShowFloatingTextForResource("coin", giftAmount);
        }
        mainGameData.gifts = 0;
        
        // 清空board
        if (boardManager != null)
        {
            boardManager.ClearBoard();
        }
        
        // 结束boss关卡时，移除boss卡和其他新加入的卡，并加回bell卡
        CleanupBossLevelCards();
        
        // 隐藏bossIcon
        uiManager?.HideBossIcon();
        
        // 更新UI
        uiManager?.UpdateUI();
    }
    
    private void PrepareBossLevelCards(string bossType)
    {
        if (CardInfoManager.Instance == null || CSVLoader.Instance == null) return;
        
        // 先清理所有之前的临时卡（包括boss卡、door卡、bell卡），确保状态干净
        CardInfoManager.Instance.ClearTemporaryCards();
        
        // 保存bell卡信息（从原始字典中获取，如果存在）
        if (CSVLoader.Instance != null && CSVLoader.Instance.cardDict.ContainsKey("bell"))
        {
            bellCardInfo = CSVLoader.Instance.cardDict["bell"];
        }
        else
        {
            bellCardInfo = CardInfoManager.Instance.GetCardInfo("bell");
        }
        
        // 从原始字典中读取boss卡的CardInfo（不通过GetCardInfo，因为可能被临时卡覆盖）
        string bossTypeLower = bossType.ToLower();
        CardInfo bossCardInfo = null;
        if (CSVLoader.Instance != null && CSVLoader.Instance.cardDict != null)
        {
            if (bossTypeLower == "nun" && CSVLoader.Instance.cardDict.ContainsKey("nun"))
            {
                bossCardInfo = CSVLoader.Instance.cardDict["nun"];
            }
            else if (bossTypeLower == "snowman" && CSVLoader.Instance.cardDict.ContainsKey("snowman"))
            {
                bossCardInfo = CSVLoader.Instance.cardDict["snowman"];
            }
            else if (bossTypeLower.StartsWith("snowsnake") && CSVLoader.Instance.cardDict.ContainsKey("snowsnakeHead"))
            {
                bossCardInfo = CSVLoader.Instance.cardDict["snowsnakeHead"];
            }
            else if (BossLevelIds.IsHorriblemanStyleBoss(bossTypeLower) && CSVLoader.Instance.cardDict.ContainsKey("horribleman"))
            {
                bossCardInfo = CSVLoader.Instance.cardDict["horribleman"];
            }
            else if (bossTypeLower == "shadow" && CSVLoader.Instance.cardDict.ContainsKey("shadow"))
            {
                bossCardInfo = CSVLoader.Instance.cardDict["shadow"];
            }
            else if (bossTypeLower == "ghost" && CSVLoader.Instance.cardDict.ContainsKey("ghost"))
            {
                bossCardInfo = CSVLoader.Instance.cardDict["ghost"];
            }
            else if (bossTypeLower == "crack" && CSVLoader.Instance.cardDict.ContainsKey("crack"))
            {
                bossCardInfo = CSVLoader.Instance.cardDict["crack"];
            }
        }
        
        // 如果找到了boss的CardInfo，创建副本并设置start为1，然后添加到临时卡牌中
        if (bossCardInfo != null)
        {
            CardInfo bossCardCopy = CreateCardInfoCopy(bossCardInfo);
            bossCardCopy.start = 1; // boss关卡中，boss卡出现1次
            CardInfoManager.Instance.AddTemporaryCard(bossCardCopy.identifier, bossCardCopy);
        }
        
        // nun boss还需要加入door卡
        if (bossTypeLower == "nun")
        {
            CardInfo doorCardInfo = null;
            if (CSVLoader.Instance != null && CSVLoader.Instance.cardDict != null && CSVLoader.Instance.cardDict.ContainsKey("door"))
            {
                doorCardInfo = CSVLoader.Instance.cardDict["door"];
            }
            if (doorCardInfo != null)
            {
                CardInfo doorCardCopy = CreateCardInfoCopy(doorCardInfo);
                doorCardCopy.start = 1; // door卡每次只添加1个
                CardInfoManager.Instance.AddTemporaryCard(doorCardCopy.identifier, doorCardCopy);
            }
        }
    }
    
    private CardInfo CreateCardInfoCopy(CardInfo original)
    {
        // 创建CardInfo的副本
        return new CardInfo
        {
            identifier = original.identifier,
            name = original.name,
            cost = original.cost,
            costIncrease = original.costIncrease,
            desc = original.desc,
            canDraw = original.canDraw,
            start = original.start, // 会在调用后修改
            isFixed = original.isFixed,
            level = original.level,
            maxCount = original.maxCount,
            isEnemy = original.isEnemy
        };
    }
    
    private void CleanupBossLevelCards()
    {
        if (CardInfoManager.Instance == null) return;
        
        // 清空所有临时卡（包括boss卡、door卡等）
        CardInfoManager.Instance.ClearTemporaryCards();
        
        // 加回bell卡到临时卡字典（如果需要的话，bell卡应该在原始字典中，这里只是为了确保）
        if (bellCardInfo != null)
        {
            // 使用保存的bell卡信息
            CardInfoManager.Instance.AddTemporaryCard("bell", bellCardInfo);
            bellCardInfo = null; // 清空保存的bell卡信息
        }
        else if (CSVLoader.Instance != null && CSVLoader.Instance.cardDict.ContainsKey("bell"))
        {
            // 如果bell卡信息不存在，从CSVLoader获取原始bell卡信息
            CardInfoManager.Instance.AddTemporaryCard("bell", CSVLoader.Instance.cardDict["bell"]);
        }
    }
    
    private void ShowVictory()
    {
        // 播放胜利音效
        SFXManager.Instance?.PlaySFX("victory");
        
        // 显示胜利菜单
        if (VictoryPanel.Instance != null)
        {
            VictoryPanel.Instance.ShowVictory();
        }
    }
    
    public bool IsBossLevel()
    {
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        return !string.IsNullOrEmpty(levelInfo.boss);
    }
    
    public bool IsBossCard(CardType cardType)
    {
        return cardType == CardType.Nun ||
               cardType == CardType.Snowman ||
               cardType == CardType.SnowsnakeHead ||
               cardType == CardType.Horribleman ||
               cardType == CardType.Ghost ||
               cardType == CardType.Shadow ||
               cardType == CardType.Crack;
    }
    
    private CardType GetBossCardType(string bossType)
    {
        if (string.IsNullOrEmpty(bossType)) return CardType.Blank;
        
        string bossTypeLower = bossType.ToLower();
        if (bossTypeLower == "nun")
        {
            return CardType.Nun;
        }
        else if (bossTypeLower == "snowman")
        {
            return CardType.Snowman;
        }
        else if (bossTypeLower.StartsWith("snowsnake"))
        {
            return CardType.SnowsnakeHead;
        }
        else if (BossLevelIds.IsHorriblemanStyleBoss(bossTypeLower))
        {
            return CardType.Horribleman;
        }
        else if (bossTypeLower == "shadow")
        {
            return CardType.Shadow;
        }
        else if (bossTypeLower == "ghost")
        {
            return CardType.Ghost;
        }
        else if (bossTypeLower == "crack")
        {
            return CardType.Crack;
        }
        
        return CardType.Blank;
    }
    
    // 显示漂浮字效果（内部调用）
    private void ShowFloatingTextForResource(string resourceType, int changeAmount)
    {
        ShowFloatingText(resourceType, changeAmount);
    }
    
    // 显示漂浮字效果（公共方法，供其他类调用）
    public void ShowFloatingText(string resourceType, int changeAmount)
    {
        if (uiManager == null) return;
        
        RectTransform targetRect = null;
        
        // 根据资源类型找到对应的UI元素
        switch (resourceType.ToLower())
        {
            case "coin":
                if (uiManager.coinsText != null)
                {
                    targetRect = uiManager.coinsText.GetComponent<RectTransform>();
                }
                break;
            case "health":
                if (uiManager.healthText != null)
                {
                    targetRect = uiManager.healthText.GetComponent<RectTransform>();
                }
                break;
            case "shield":
                if (uiManager.shieldText != null)
                {
                    targetRect = uiManager.shieldText.GetComponent<RectTransform>();
                }
                break;
            case "gift":
                if (uiManager.giftsText != null)
                {
                    targetRect = uiManager.giftsText.GetComponent<RectTransform>();
                }
                break;
            case "light":
                if (uiManager.flashlightsText != null)
                {
                    targetRect = uiManager.flashlightsText.GetComponent<RectTransform>();
                }
                break;
        }
        
        if (targetRect != null)
        {
            uiManager.ShowFloatingText(resourceType, changeAmount, targetRect);
        }
    }
    
    // 创建卡牌飞行效果
    private void CreateCardFlyEffect(int row, int col, string resourceType)
    {
        if (boardManager == null || uiManager == null) return;
        
        // 获取tile对象
        Tile tile = boardManager.GetTile(row, col);
        if (tile == null) return;
        
        // 获取tile的frontImage
        Image frontImage = tile.frontImage;
        if (frontImage == null || frontImage.sprite == null || !frontImage.gameObject.activeSelf) return;
        
        // 获取目标位置
        RectTransform targetRect = null;
        switch (resourceType.ToLower())
        {
            case "coin":
                if (uiManager.coinsText != null)
                {
                    targetRect = uiManager.coinsText.GetComponent<RectTransform>();
                }
                break;
            case "gift":
                if (uiManager.giftsText != null)
                {
                    targetRect = uiManager.giftsText.GetComponent<RectTransform>();
                }
                break;
            case "light":
                if (uiManager.flashlightsText != null)
                {
                    targetRect = uiManager.flashlightsText.GetComponent<RectTransform>();
                }
                break;
            case "bell":
                if (uiManager.bellButton != null)
                {
                    targetRect = uiManager.bellButton.GetComponent<RectTransform>();
                }
                break;
        }
        
        if (targetRect == null) return;
        
        // 获取Canvas
        Canvas canvas = GameManager.Instance.canvas;
        
        // 创建新的GameObject用于飞行
        GameObject flyObj = new GameObject("CardFlyEffect");
        flyObj.transform.SetParent(canvas.transform, false);
        
        // 添加RectTransform
        RectTransform flyRect = flyObj.AddComponent<RectTransform>();
        flyRect.sizeDelta = new Vector2(100, 100); // 固定长宽为100
        
        // 添加Image组件并复制sprite
        Image flyImage = flyObj.AddComponent<Image>();
        flyImage.sprite = frontImage.sprite;
        flyImage.preserveAspect = true;
        flyImage.color = frontImage.color; // 复制颜色
        
        // 设置层级，确保在最上层显示
        flyObj.transform.SetAsLastSibling();
        
        // 添加CardFlyEffect组件
        CardFlyEffect flyEffect = flyObj.AddComponent<CardFlyEffect>();
        
        // 获取起始位置（tile的世界坐标）
        Vector3 startPos = frontImage.rectTransform.position;
        
        // 获取目标位置（资源UI的世界坐标）
        Vector3 targetPos = targetRect.position;
        
        // 触发飞行动画
        flyEffect.FlyToTarget(startPos, targetPos);
    }
    
    // 检查并触发屏幕抖动
    private IEnumerator HandleMagnetRevealed(int magnetRow, int magnetCol)
    {
        isPlayerInputDisabled = true;
        int[] dr = { 0, 0, 1, -1 };
        int[] dc = { 1, -1, 0, 0 };
        List<Vector2Int> targets = new List<Vector2Int>();
        if (boardManager != null)
        {
            int rows = boardManager.GetCurrentRow();
            int cols = boardManager.GetCurrentCol();
            for (int i = 0; i < 4; i++)
            {
                int nr = magnetRow + dr[i], nc = magnetCol + dc[i];
                if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) continue;
                CardType t = boardManager.GetCardType(nr, nc);
                if (t == CardType.Coin || t == CardType.Gift || t == CardType.Hint)
                    targets.Add(new Vector2Int(nr, nc));
            }
        }
        targets.Sort((a, b) =>
        {
            int c = a.x.CompareTo(b.x);
            return c != 0 ? c : a.y.CompareTo(b.y);
        });
        
        int flyTotal = targets.Count;
        int flyDoneCount = 0;
        foreach (Vector2Int p in targets)
        {
            int fr = p.x, fc = p.y;
            CreateGiftFlyMagnetEffect(fr, fc, magnetRow, magnetCol, () => { flyDoneCount++; });
        }
        float flyWait = 0f;
        while (flyDoneCount < flyTotal && flyWait < 4f)
        {
            flyWait += Time.deltaTime;
            yield return null;
        }
        
        // 礼物全部飞到磁铁后，在磁铁格子一次性获得礼物，不翻开其它格子
        if (targets.Count > 0)
        {
            mainGameData.gifts += targets.Count;
            ShowFloatingTextForResource("gift", targets.Count);
            CreateCardFlyEffect(magnetRow, magnetCol, "gift");
            SFXManager.Instance?.PlayCardRevealSound("gift");
        }
        
        isPlayerInputDisabled = false;
        uiManager?.UpdateUI();
    }
    
    private void CreateGiftFlyMagnetEffect(int fromRow, int fromCol, int toRow, int toCol, System.Action onComplete)
    {
        if (boardManager == null || canvas == null)
        {
            onComplete?.Invoke();
            return;
        }
        Tile fromT = boardManager.GetTile(fromRow, fromCol);
        Tile toT = boardManager.GetTile(toRow, toCol);
        RectTransform startRt = fromT != null && fromT.backImage != null && fromT.backImage.gameObject.activeSelf
            ? fromT.backImage.rectTransform
            : (fromT != null && fromT.frontImage != null ? fromT.frontImage.rectTransform : null);
        RectTransform endRt = toT != null && toT.frontImage != null && toT.frontImage.gameObject.activeSelf
            ? toT.frontImage.rectTransform
            : (toT != null && toT.backImage != null ? toT.backImage.rectTransform : null);
        if (startRt == null || endRt == null)
        {
            onComplete?.Invoke();
            return;
        }
        Sprite giftSp = Resources.Load<Sprite>("icon/gift");
        GameObject flyObj = new GameObject("MagnetGiftFly");
        flyObj.transform.SetParent(canvas.transform, false);
        RectTransform flyRect = flyObj.AddComponent<RectTransform>();
        flyRect.sizeDelta = new Vector2(100, 100);
        Image flyImage = flyObj.AddComponent<Image>();
        flyImage.sprite = giftSp;
        if (flyImage.sprite == null && fromT.frontImage != null)
            flyImage.sprite = fromT.frontImage.sprite;
        flyImage.preserveAspect = true;
        flyObj.transform.SetAsLastSibling();
        CardFlyEffect fx = flyObj.AddComponent<CardFlyEffect>();
        fx.FlyToTarget(startRt.position, endRt.position, onComplete);
    }
    
    private void ApplyMagnetEffectOnAlreadyRevealed(int row, int col)
    {
        if (boardManager == null) return;
        CardType t = boardManager.GetCardType(row, col);
        switch (t)
        {
            case CardType.Coin:
                {
                    int coinReward = 1 + (upgradeManager?.GetCoinRewardModifier() ?? 0);
                    mainGameData.coins += coinReward;
                    ShowFloatingTextForResource("coin", coinReward);
                    CreateCardFlyEffect(row, col, "coin");
                    SFXManager.Instance?.PlayCardRevealSound("coin");
                    break;
                }
            case CardType.Gift:
                {
                    int giftReward = (upgradeManager?.GetGiftMultiplier() ?? 1) + (upgradeManager?.GetGiftRewardModifier() ?? 0);
                    mainGameData.gifts += giftReward;
                    ShowFloatingTextForResource("gift", giftReward);
                    CreateCardFlyEffect(row, col, "gift");
                    SFXManager.Instance?.PlayCardRevealSound("gift");
                    break;
                }
            case CardType.Hint:
                ShowHint(row, col);
                SFXManager.Instance?.PlayCardRevealSound("hint");
                break;
        }
    }
    
    private void CheckAndTriggerShake()
    {
        if (mainGameData.health <= 3 && mainGameData.health > 0)
        {
            ShakeManager.Instance?.StartShake(mainGameData.health);
        }
        else if (mainGameData.health > 3)
        {
            ShakeManager.Instance?.StopShake();
        }
    }
    
    // 检查并更新抖动（当血量恢复时）
    public void CheckAndUpdateShake()
    {
        if (mainGameData.health > 3)
        {
            ShakeManager.Instance?.StopShake();
        }
        else if (mainGameData.health > 0 && mainGameData.health <= 3)
        {
            ShakeManager.Instance?.UpdateShakeStrength(mainGameData.health);
        }
    }
}
