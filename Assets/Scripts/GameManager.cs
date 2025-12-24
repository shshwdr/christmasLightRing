using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    
    public BoardManager boardManager;
    public UIManager uiManager;
    public ShopManager shopManager;
    public UpgradeManager upgradeManager;
    public TutorialManager tutorialManager;
    public StoryManager storyManager;
    
    public GameData gameData = new GameData();
    public int initialHealth = 3;
    public int initialFlashlights = 0;
    
    private bool isUsingFlashlight = false;
    private bool isFlashlightRevealing = false; // 标记正在使用手电筒翻开
    private bool isChurchRingRevealing = false; // 标记正在使用churchRing效果翻开
    private Vector2Int currentHintPosition = new Vector2Int(-1, -1);
    private bool isPlayerInputDisabled = false; // 标记是否禁用玩家点击
    private System.Action pendingBossCallback = null; // 待执行的boss回调
    
    // Boss战斗状态
    private int nunDoorCount = 0; // nun boss已翻开的门数量
    private int snowmanLightCount = 0; // snowman boss被light照射的次数
    private int horriblemanCatchCount = 0; // horribleman boss被捕获的次数
    
    // 保存bell卡信息，用于boss关卡结束后恢复
    private CardInfo bellCardInfo = null;
    
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
        gameData.health = initialHealth;
        gameData.flashlights = initialFlashlights;
        
        // 初始化升级项
        if (upgradeManager != null)
        {
            upgradeManager.InitializeUpgrades();
        }
        
        StartNewLevel();
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
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
    
    public void StartNewLevel()
    {
        // 获取当前关卡信息
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        bool isBossLevel = !string.IsNullOrEmpty(levelInfo.boss);
        bool isNunBossLevel = isBossLevel && levelInfo.boss.ToLower() == "nun";
        
        // 隐藏bossDesc panel和bossIcon（如果不是boss关卡）
        if (!isBossLevel && uiManager != null)
        {
            uiManager.HideBossDesc();
            uiManager.HideBossIcon();
        }
        
        // 如果是boss关卡，保存boss战前状态
        if (isBossLevel)
        {
            SaveBossPreState();
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
        
        // 如果是nun boss关卡，在生成board之前播放beforeNun story
        if (isNunBossLevel && storyManager != null)
        {
            storyManager.PlayStory("beforeNun", () =>
            {
                ContinueStartNewLevelAfterStory(levelInfo, isBossLevel);
            });
            return; // 等待story播放完成后再继续
        }
        
        // 游戏开始时播放start story（只在第一关）
        if (gameData.currentLevel == 1 && storyManager != null)
        {
            storyManager.PlayStory("start", () =>
            {
                ContinueStartNewLevelAfterStory(levelInfo, isBossLevel);
            });
            return; // 等待story播放完成后再继续
        }
        
        // 直接继续（没有story需要播放）
        ContinueStartNewLevelAfterStory(levelInfo, isBossLevel);
    }
    
    private void ContinueStartNewLevelAfterStory(LevelInfo levelInfo, bool isBossLevel)
    {
        // 重置boss战斗状态
        nunDoorCount = 0;
        snowmanLightCount = 0;
        horriblemanCatchCount = 0;
        
        if (boardManager != null)
        {
            boardManager.ClearBoard();
            boardManager.GenerateBoard();
        }
        
        isUsingFlashlight = false;
        isFlashlightRevealing = false;
        isChurchRingRevealing = false;
        currentHintPosition = new Vector2Int(-1, -1);
        isPlayerInputDisabled = false; // 恢复玩家输入
        pendingBossCallback = null; // 清空待执行的boss回调
        
        // LastLight升级项：如果手电筒数量大于1，保留1个到下一关
        int keptFlashlights = upgradeManager?.GetFlashlightForNextLevel(gameData.flashlights) ?? 0;
        gameData.flashlights = initialFlashlights + keptFlashlights;
        
        gameData.patternRecognitionSequence = 0; // 重置patternRecognition计数器
        CursorManager.Instance?.ResetCursor();
        uiManager?.HideBellButton(); // 新关卡开始时隐藏bell按钮
        uiManager?.UpdateUI();
        uiManager?.UpdateEnemyCount();
        uiManager?.UpdateUpgradeDisplay();
        uiManager?.UpdateBackgroundImage(); // 更新背景图片
        
        // 新关卡开始时检查抖动状态
        CheckAndUpdateShake();
        
        // 触发familiarSteet升级项效果
        upgradeManager?.OnLevelStart();
        
        // 如果是boss关卡，显示boss的desc弹窗
        if (isBossLevel)
        {
            ShowBossDesc(levelInfo.boss);
        }
        else
        {
            // 显示start教程
            tutorialManager?.ShowTutorial("start");
        }
    }
    
    private void ShowBossDesc(string bossType)
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
        else if (bossTypeLower == "horribleman")
        {
            bossCardInfo = CardInfoManager.Instance.GetCardInfo("horribleman");
        }
        
        // 如果找到了boss的CardInfo，使用DialogPanel显示desc
        if (bossCardInfo != null && !string.IsNullOrEmpty(bossCardInfo.desc))
        {
            string bossDesc = $"{bossCardInfo.name}\n\n{bossCardInfo.desc}";
            DialogPanel.Instance.ShowDialog(bossDesc, () => { });
        }
    }
    
    private void SaveBossPreState()
    {
        gameData.bossPreHealth = gameData.health;
        gameData.bossPreCoins = gameData.coins;
        gameData.bossPreFlashlights = gameData.flashlights;
        gameData.bossPrePurchasedCards = new List<CardType>(gameData.purchasedCards);
        gameData.bossPreOwnedUpgrades = new List<string>(gameData.ownedUpgrades);
    }
    
    public void RetryBoss()
    {
        // 恢复boss战前状态
        gameData.health = gameData.bossPreHealth;
        gameData.coins = gameData.bossPreCoins;
        gameData.flashlights = gameData.bossPreFlashlights;
        gameData.purchasedCards = new List<CardType>(gameData.bossPrePurchasedCards);
        gameData.ownedUpgrades = new List<string>(gameData.bossPreOwnedUpgrades);
        
        // 重新开始关卡
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
        boardManager.RevealTile(row, col);
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
    
    public void OnTileRevealed(int row, int col, CardType cardType, bool isLastTile = false, bool isLastSafeTile = false,bool isFirst = true)
    {
        // 检查是否是敌人（基于isEnemy字段）
        bool isEnemy = false;
        if (CardInfoManager.Instance != null)
        {
            isEnemy = CardInfoManager.Instance.IsEnemyCard(cardType);
        }
        bool isSafeTile = !isEnemy;
        
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
                gameData.coins++;
                ShowFloatingTextForResource("coin", 1);
                CreateCardFlyEffect(row, col, "coin");
                // 播放硬币卡音效
                SFXManager.Instance?.PlayCardRevealSound("coin");
                break;
            case CardType.Gift:
                int giftMultiplier = upgradeManager?.GetGiftMultiplier() ?? 1;
                gameData.gifts += giftMultiplier; // lastChance升级项：如果只有1 hp，gift翻倍
                ShowFloatingTextForResource("gift", giftMultiplier);
                CreateCardFlyEffect(row, col, "gift");
                // 播放礼物卡音效
                SFXManager.Instance?.PlayCardRevealSound("gift");
                break;
            case CardType.Enemy:
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
            case CardType.Nun:
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
                HandleSnowmanBossRevealed(row, col, wasFlashlightRevealingForSnowman);
                break;
            case CardType.Horribleman:
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
            case CardType.Flashlight:
                gameData.flashlights++;
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
        
        uiManager?.UpdateUI();
        uiManager?.UpdateEnemyCount();
    }
    
    private void CheckAndSpawnHorriblemanBoss()
    {
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        bool isHorriblemanBossLevel = levelInfo.boss != null && levelInfo.boss.ToLower() == "horribleman";
        
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
        if (gameData.flashlights > 0 && !isUsingFlashlight)
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
        
        if (isUsingFlashlight && gameData.flashlights > 0)
        {
            if (!boardManager.IsRevealed(row, col))
            {
                // 无论是不是敌人，都减一手电筒
                gameData.flashlights--;
                ShowFloatingTextForResource("light", -1);
                
                // 标记正在使用手电筒翻开
                isFlashlightRevealing = true;
                
                // 翻开tile（如果是敌人，OnTileRevealed中会跳过伤害）
                boardManager.RevealTile(row, col);
                
                isFlashlightRevealing = false;
                
                // 成功翻开后，退出手电筒状态
                isUsingFlashlight = false;
                uiManager?.UpdateFlashlightButton();
                CursorManager.Instance?.ResetCursor();
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
        int giftAmount = gameData.gifts;
        gameData.coins += giftAmount;
        if (giftAmount > 0)
        {
            ShowFloatingTextForResource("gift", -giftAmount);
            ShowFloatingTextForResource("coin", giftAmount);
        }
        gameData.gifts = 0;
        
        // 清空board
        if (boardManager != null)
        {
            boardManager.ClearBoard();
        }
        
        shopManager?.ShowShop();
    }
    
    public void NextLevel()
    {
        // 如果当前是boss关卡，清理boss卡
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        bool isBossLevel = !string.IsNullOrEmpty(levelInfo.boss);
        if (isBossLevel)
        {
            CleanupBossLevelCards();
        }
        
        gameData.currentLevel++;
        shopManager?.HideShop();
        StartNewLevel();
    }
    
    private void GameOver()
    {
        // 播放游戏结束音效
        SFXManager.Instance?.PlaySFX("gameover");
        
        // 停止抖动
        ShakeManager.Instance?.StopShake();
        
        // 检查是否是boss关卡
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        bool isBossLevel = !string.IsNullOrEmpty(levelInfo.boss);
        
        // 使用LoseMenu显示失败菜单
        if (LoseMenu.Instance != null)
        {
            LoseMenu.Instance.ShowLoseMenu(isBossLevel);
        }
        else
        {
            // 如果没有LoseMenu，使用旧的UIManager方法
            uiManager?.ShowGameOver(isBossLevel);
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
        
        // 等待1秒
        yield return new WaitForSeconds(1f);
        
        nunDoorCount++;
        
        if (nunDoorCount < 1)
        {
            // 显示"Keep Running"弹窗
            if (DialogPanel.Instance != null)
            {
                // 保存回调，不直接执行
                pendingBossCallback = () =>
                {
                    // Continue后直接刷新board（light不会清除，不会进入下一关）
                    RefreshBoard();
                };
                DialogPanel.Instance.ShowDialog($"Click the Nun icon to keep running! ({3-nunDoorCount} door left)", null);
            }
        }
        else
        {
            // 翻开第三个门后，显示"You escape from the nun!"
            if (DialogPanel.Instance != null)
            {
                // 保存回调，不直接执行
                pendingBossCallback = () =>
                {
                    // 进入shop的流程
                    EndBossBattle();
                };
                DialogPanel.Instance.ShowDialog("You escaped from the nun! Click the Nun icon to Leave this place.", null);
            }
        }
        
        // 显示弹窗后，恢复玩家点击（让玩家可以继续翻牌）
        isPlayerInputDisabled = false;
    }

    public float enemyRevealWaitTime = 0.5f;
    // 处理敌人翻开的逻辑：先显示identifier图片0.3秒，然后切换到对应图片
    private IEnumerator HandleEnemyRevealed(int row, int col, CardType cardType)
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
        
        // 判断是否是用灯光照开的（或churchRing的升级项翻开的，即不扣血的方式）
        bool isSafeReveal = wasFlashlightRevealing || wasChurchRingRevealing;
        
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
        
        // 如果对应的图片不存在，就保持之前的图片（不切换）
        if (targetSprite != null)
        {
            tile.SwitchEnemySprite(targetSprite, true);
        }
        
        // 获取敌人identifier并播放对应音效
        string enemyIdentifier = CardInfoManager.Instance?.GetEnemyIdentifier(cardType);
        
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
        }
        else
        {
            // 不用灯光照开的，造成伤害
            // 播放atk音效（攻击时）
            if (!string.IsNullOrEmpty(enemyIdentifier))
            {
                SFXManager.Instance?.PlayEnemySound(enemyIdentifier, "atk");
            }
            gameData.health--;
            ShowFloatingTextForResource("health", -1);
            CheckAndTriggerShake(); // 检查并触发抖动
            uiManager?.UpdateUI(); // 立即更新UI，确保血量显示更新
            
            // 切换player图片到player_hurt.png
            StartCoroutine(ShowPlayerHurt());
            
            int lostGifts = gameData.gifts;
            gameData.gifts = 0;
            if (lostGifts > 0)
            {
                ShowFloatingTextForResource("gift", -lostGifts);
            }
            // 触发lateMending升级项效果：不用light翻开grinch时，reveal相邻的safe tile
            upgradeManager?.OnRevealGrinchWithoutLight(row, col);
            if (gameData.health <= 0)
            {
                GameOver();
                yield break;
            }
        }
    }
    
    private void HandleNunBossRevealed(int row, int col)
    {
        // nun boss现在和其他敌人一样，受灯光影响
        // 这里只处理nun boss的特殊逻辑（门等）
    }
    
    private void HandleSnowmanBossRevealed(int row, int col, bool wasFlashlightRevealing)
    {
        StartCoroutine(HandleSnowmanBossRevealedCoroutine(wasFlashlightRevealing));
    }
    
    private IEnumerator HandleSnowmanBossRevealedCoroutine(bool wasFlashlightRevealing)
    {
        // 禁用玩家点击
        isPlayerInputDisabled = true;
        
        // 等待1秒
        yield return new WaitForSeconds(1f);
        
        // 玩家必须用light照射boss，否则不算
        if (wasFlashlightRevealing)
        {
            snowmanLightCount++;
            
            if (snowmanLightCount < 3)
            {
                // 显示"He's getting hurt, do it again!"
                if (DialogPanel.Instance != null)
                {
                    // 保存回调，不直接执行
                    pendingBossCallback = () =>
                    {
                        // Continue后直接刷新board（light不会清除，不会进入下一关）
                        RefreshBoard();
                    };
                    DialogPanel.Instance.ShowDialog($"He's getting dazzled, do it again!({3-snowmanLightCount}hit left)\n Click the Snowman icon to keep chasing him.", null);
                }
            }
            else
            {
                // boss被照射3次后，显示"The snowman is stunned!"
                if (DialogPanel.Instance != null)
                {
                    // 保存回调，不直接执行
                    pendingBossCallback = () =>
                    {
                        // 进入shop的流程
                        EndBossBattle();
                    };
                    DialogPanel.Instance.ShowDialog("The Snowman is stunned!", null);
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
                    DialogPanel.Instance.ShowDialog("You have to use light to dazzle him! Click the Snowman icon to keep chasing him.", null);
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
        
        // 等待1秒
        yield return new WaitForSeconds(1f);
        
        // horribleman boss需要被捕获3次（用flashlight或不用都可以）
        horriblemanCatchCount++;
        
        if (horriblemanCatchCount < 3)
        {
            // 前面两次，每次照射弹窗"Do one more time!"
            if (DialogPanel.Instance != null)
            {
                // 保存回调，不直接执行
                pendingBossCallback = () =>
                {
                    // Continue后刷新board
                    RefreshBoard();
                };
                DialogPanel.Instance.ShowDialog($"Reveal him one more time!({3-horriblemanCatchCount} time left)\n Click the monster icon to keep chasing him.", null);
            }
        }
        else
        {
            // 第三次捕获后，显示"You escape from the nun!"（原文如此）
            if (DialogPanel.Instance != null)
            {
                // 保存回调，不直接执行
                pendingBossCallback = () =>
                {
                    // 进入胜利流程
                    ShowVictory();
                };
                DialogPanel.Instance.ShowDialog("You caught the horrible man!", null);
            }
        }
        
        // 显示弹窗后，恢复玩家点击（让玩家可以继续翻牌）
        isPlayerInputDisabled = false;
    }
    
    // 当bossIcon被点击时调用
    public void OnBossIconClicked()
    {
        if (pendingBossCallback != null)
        {
            System.Action callback = pendingBossCallback;
            pendingBossCallback = null;
            
            // 恢复玩家点击
            isPlayerInputDisabled = false;
            
            // 禁用bossIcon按钮
            uiManager?.SetBossIconInteractable(false);
            
            // 执行回调
            callback();
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
        if (boardManager == null) yield break;
        
        // 获取player位置
        Vector2Int playerPos = boardManager.GetPlayerPosition();
        if (playerPos.x < 0) yield break; // 未找到player
        
        // 获取player tile
        Tile playerTile = boardManager.GetTile(playerPos.x, playerPos.y);
        if (playerTile == null) yield break;
        
        // 加载player_hurt图片
        Sprite hurtSprite = Resources.Load<Sprite>("icon/player_hurt");
        if (hurtSprite != null)
        {
            // 切换图片
            playerTile.SetFrontSprite(hurtSprite);
        }
        
        // 等待1秒
        yield return new WaitForSeconds(1f);
        
        // 切换回player图片
        Sprite normalSprite = Resources.Load<Sprite>("icon/player");
        if (normalSprite != null)
        {
            playerTile.SetFrontSprite(normalSprite);
        }
    }
    
    private void RefreshBoard()
    {
        // 禁用bossIcon按钮
        uiManager?.SetBossIconInteractable(false);
        
        // 刷新board（light不会清除，不会进入下一关）
        // 保持当前状态，只重新生成board
        // 如果是nun boss关卡且还有门没开完，需要重新添加door卡
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        bool isNunBossLevel = levelInfo.boss != null && levelInfo.boss.ToLower() == "nun";
        
        if (isNunBossLevel && nunDoorCount < 3)
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
    }
    
    private void EndBossBattle()
    {
        // 播放击败boss音效
        SFXManager.Instance?.PlaySFX("winBoss");
        
        // 所有gift变成gold
        int giftAmount = gameData.gifts;
        gameData.coins += giftAmount;
        if (giftAmount > 0)
        {
            ShowFloatingTextForResource("gift", -giftAmount);
            ShowFloatingTextForResource("coin", giftAmount);
        }
        gameData.gifts = 0;
        
        // boss level结束后，血量回满
        gameData.health = initialHealth;
        
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
        
        // 检查是否是nun boss关卡，如果是，播放afterNun story
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        bool isNunBossLevel = !string.IsNullOrEmpty(levelInfo.boss) && levelInfo.boss.ToLower() == "nun";
        
        if (isNunBossLevel && storyManager != null)
        {
            storyManager.PlayStory("afterNun", () =>
            {
                shopManager?.ShowShop();
            });
        }
        else
        {
            shopManager?.ShowShop();
        }
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
            else if (bossTypeLower == "horribleman" && CSVLoader.Instance.cardDict.ContainsKey("horribleman"))
            {
                bossCardInfo = CSVLoader.Instance.cardDict["horribleman"];
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
        return cardType == CardType.Nun || cardType == CardType.Snowman || cardType == CardType.Horribleman;
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
        else if (bossTypeLower == "horribleman")
        {
            return CardType.Horribleman;
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
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }
        if (canvas == null) return;
        
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
    private void CheckAndTriggerShake()
    {
        if (gameData.health <= 3 && gameData.health > 0)
        {
            ShakeManager.Instance?.StartShake(gameData.health);
        }
        else if (gameData.health > 3)
        {
            ShakeManager.Instance?.StopShake();
        }
    }
    
    // 检查并更新抖动（当血量恢复时）
    public void CheckAndUpdateShake()
    {
        if (gameData.health > 3)
        {
            ShakeManager.Instance?.StopShake();
        }
        else if (gameData.health > 0 && gameData.health <= 3)
        {
            ShakeManager.Instance?.UpdateShakeStrength(gameData.health);
        }
    }
}
