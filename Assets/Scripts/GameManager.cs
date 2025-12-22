using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    
    public BoardManager boardManager;
    public UIManager uiManager;
    public ShopManager shopManager;
    public UpgradeManager upgradeManager;
    public TutorialManager tutorialManager;
    
    public GameData gameData = new GameData();
    public int initialHealth = 3;
    public int initialFlashlights = 0;
    
    private bool isUsingFlashlight = false;
    private bool isFlashlightRevealing = false; // 标记正在使用手电筒翻开
    private bool isChurchRingRevealing = false; // 标记正在使用churchRing效果翻开
    private Vector2Int currentHintPosition = new Vector2Int(-1, -1);
    
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
        
        // 如果是boss关卡，保存boss战前状态
        if (isBossLevel)
        {
            SaveBossPreState();
            // 在进入关卡且抽取前，添加boss卡和door卡，移除bell卡
            PrepareBossLevelCards(levelInfo.boss);
        }
        
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
        
        // LastLight升级项：如果手电筒数量大于1，保留1个到下一关
        int keptFlashlights = upgradeManager?.GetFlashlightForNextLevel(gameData.flashlights) ?? 0;
        gameData.flashlights = initialFlashlights + keptFlashlights;
        
        gameData.patternRecognitionSequence = 0; // 重置patternRecognition计数器
        CursorManager.Instance?.ResetCursor();
        uiManager?.HideBellButton(); // 新关卡开始时隐藏bell按钮
        uiManager?.UpdateUI();
        uiManager?.UpdateEnemyCount();
        uiManager?.UpdateUpgradeDisplay();
        
        // 触发familiarSteet升级项效果
        upgradeManager?.OnLevelStart();
        
        // 显示start教程
        tutorialManager?.ShowTutorial("start");
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
        boardManager.RevealTile(row, col);
    }
    
    // 用于churchRing升级项：reveal tile时等同于用light翻开（但不消耗light）
    public void RevealTileWithChurchRing(int row, int col)
    {
        if (boardManager == null) return;
        
        isChurchRingRevealing = true;
        boardManager.RevealTile(row, col);
        isChurchRingRevealing = false;
    }
    
    public void OnTileRevealed(int row, int col, CardType cardType, bool isLastTile = false, bool isLastSafeTile = false)
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
                break;
            case CardType.Coin:
                gameData.coins++;
                break;
            case CardType.Gift:
                int giftMultiplier = upgradeManager?.GetGiftMultiplier() ?? 1;
                gameData.gifts += giftMultiplier; // lastChance升级项：如果只有1 hp，gift翻倍
                break;
            case CardType.Enemy:
                // 显示enemy教程（第一次翻出敌人牌）
                tutorialManager?.ShowTutorial("enemy");
                
                // 所有isEnemy的卡牌都走这个逻辑
                if (isEnemy)
                {
                    // 检查是否是nun boss，nun boss不怕光
                    bool isNunBoss = (cardType == CardType.Nun);
                    
                    // 如果使用手电筒或churchRing效果，敌人不造成伤害，也不抢礼物（除非是nun boss）
                    if (isFlashlightRevealing && !isNunBoss)
                {
                    // 触发chaseGrinchGiveGift升级项效果（只有用light翻开时才触发）
                    upgradeManager?.OnChaseGrinchWithLight();
                }
                    else if (isChurchRingRevealing && !isNunBoss)
                {
                    // churchRing效果：等同于用light翻开，但不消耗light，也不触发chaseGrinchGiveGift
                        // 敌人不会扣血，也不会抢礼物（除非是nun boss）
                }
                else
                {
                    gameData.health--;
                    gameData.gifts = 0;
                    // 触发lateMending升级项效果：不用light翻开grinch时，reveal相邻的safe tile
                    upgradeManager?.OnRevealGrinchWithoutLight(row, col);
                    if (gameData.health <= 0)
                    {
                        GameOver();
                        return;
                    }
                }
                }
                break;
            case CardType.Nun:
                // nun boss处理
                if (isEnemy)
                {
                    // 如果nun boss是敌人，走敌人逻辑（nun boss不怕光）
                    // nun boss即使使用light也会造成伤害
                    gameData.health--;
                    gameData.gifts = 0;
                    if (gameData.health <= 0)
                    {
                        GameOver();
                        return;
                    }
                }
                // 执行nun boss的特殊逻辑（门等）
                HandleNunBossRevealed(row, col);
                break;
            case CardType.Snowman:
                // snowman boss处理
                if (isEnemy)
                {
                    // 如果snowman boss是敌人，走敌人逻辑（但nun boss不怕光，snowman怕光）
                    if (isFlashlightRevealing)
                    {
                        upgradeManager?.OnChaseGrinchWithLight();
                    }
                    else if (isChurchRingRevealing)
                    {
                        // churchRing效果
                    }
                    else
                    {
                        gameData.health--;
                        gameData.gifts = 0;
                        upgradeManager?.OnRevealGrinchWithoutLight(row, col);
                        if (gameData.health <= 0)
                        {
                            GameOver();
                            return;
                        }
                    }
                }
                // 执行snowman boss的特殊逻辑（照射计数等）
                HandleSnowmanBossRevealed(row, col);
                break;
            case CardType.Horribleman:
                // horribleman boss处理
                if (isEnemy)
                {
                    // 如果horribleman boss是敌人，走敌人逻辑
                    if (isFlashlightRevealing)
                    {
                        upgradeManager?.OnChaseGrinchWithLight();
                    }
                    else if (isChurchRingRevealing)
                    {
                        // churchRing效果
                    }
                    else
                    {
                        gameData.health--;
                        gameData.gifts = 0;
                        upgradeManager?.OnRevealGrinchWithoutLight(row, col);
                        if (gameData.health <= 0)
                        {
                            GameOver();
                            return;
                        }
                    }
                }
                // 执行horribleman boss的特殊逻辑（捕获计数等）
                HandleHorriblemanBossRevealed(row, col);
                break;
            case CardType.Flashlight:
                gameData.flashlights++;
                // 显示light教程（翻出flashLight）
                tutorialManager?.ShowTutorial("light");
                break;
            case CardType.Hint:
                ShowHint(row, col);
                break;
            case CardType.PoliceStation:
                break;
            case CardType.Player:
                break;
            case CardType.Bell:
                // 翻开Bell卡后显示ringBell按钮
                uiManager?.ShowBellButton();
                // 显示bell教程（翻出bell）
                tutorialManager?.ShowTutorial("bell");
                // 触发升级项效果
                upgradeManager?.OnBellRevealed();
                upgradeManager?.OnBellFound();
                break;
            case CardType.Iceground:
                // 翻开iceground时，如果四个方向有还未翻开的安全格子，直接翻开
                RevealAdjacentSafeTiles(row, col);
                break;
            case CardType.Sign:
                // Sign卡翻开时不需要特殊处理，箭头方向在生成时已设置
                break;
            case CardType.Door:
                HandleDoorRevealed();
                break;
        }
        
        // patternRecognition: 当翻开safe tile时，增加sequence计数
        // safe tile包括：所有非isEnemy的tile，以及用light或churchRing翻开的isEnemy（因为不会造成伤害，除非是nun boss）
        {
            
        bool isNunBoss = (cardType == CardType.Nun);
        bool isPatternSafeTile = isSafeTile || (isEnemy && (isFlashlightRevealing || isChurchRingRevealing) && !isNunBoss);
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
        if (isFlashlightRevealing && isSafeTile)
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
                        // 直接翻开这个安全格子
                        boardManager.RevealTile(newRow, newCol);
                    }
                }
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
        }
    }
    
    public void UseFlashlightToReveal(int row, int col)
    {
        if (isUsingFlashlight && gameData.flashlights > 0)
        {
            if (!boardManager.IsRevealed(row, col))
            {
                // 无论是不是敌人，都减一手电筒
                gameData.flashlights--;
                
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
        }
    }
    
    public void EndTurn()
    {
        // 显示win教程（第一次点击ringbell过关）
        tutorialManager?.ShowTutorial("win");
        
        // 所有gift变成gold
        gameData.coins += gameData.gifts;
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
        nunDoorCount++;
        
        if (nunDoorCount < 3)
        {
            // 显示"Keep Running"弹窗
            if (DialogPanel.Instance != null)
            {
                DialogPanel.Instance.ShowDialog($"Keep Running({3-nunDoorCount} door left)", () =>
                {
                    // Continue后直接刷新board（light不会清除，不会进入下一关）
                    RefreshBoard();
                });
            }
        }
        else
        {
            // 翻开第三个门后，显示"You escape from the nun!"
            if (DialogPanel.Instance != null)
            {
                DialogPanel.Instance.ShowDialog("You escaped from the nun!", () =>
                {
                    // 进入shop的流程
                    EndBossBattle();
                });
            }
        }
    }
    
    private void HandleNunBossRevealed(int row, int col)
    {
        // nun boss不怕光，即使使用light也会造成伤害
        // 注意：如果nun boss的isEnemy为true，伤害已经在case中处理了
        // 这里只处理nun boss的特殊逻辑（如果有的话）
    }
    
    private void HandleSnowmanBossRevealed(int row, int col)
    {
        // 玩家必须用light照射boss，否则不算
        if (isFlashlightRevealing)
        {
            snowmanLightCount++;
            
            if (snowmanLightCount < 3)
            {
                // 显示"He's getting hurt, do it again!"
                if (DialogPanel.Instance != null)
                {
                    DialogPanel.Instance.ShowDialog($"He's getting hurt, do it again!({3-snowmanLightCount}hit left)", () =>
                    {
                        // Continue后直接刷新board（light不会清除，不会进入下一关）
                        RefreshBoard();
                    });
                }
            }
            else
            {
                // boss被照射3次后，显示"The snowman is stunned!"
                if (DialogPanel.Instance != null)
                {
                    DialogPanel.Instance.ShowDialog("The snowman is stunned!", () =>
                    {
                        // 进入shop的流程
                        EndBossBattle();
                    });
                }
            }
        }
        else
        {
            // 如果没用light，那么会弹窗"You have to use light to shock him!"
            if (DialogPanel.Instance != null)
            {
                DialogPanel.Instance.ShowDialog("You have to use light to shock him!", () =>
                {
                    // 之后流程一样，只是这次不算boss被照射了
                    RefreshBoard();
                });
            }
        }
    }
    
    private void HandleHorriblemanBossRevealed(int row, int col)
    {
        // horribleman boss需要被捕获3次（用flashlight或不用都可以）
        horriblemanCatchCount++;
        
        if (horriblemanCatchCount < 3)
        {
            // 前面两次，每次照射弹窗"Do one more time!"
            if (DialogPanel.Instance != null)
            {
                DialogPanel.Instance.ShowDialog($"Do one more time!({3-horriblemanCatchCount} time left)", () =>
                {
                    // Continue后刷新board
                    RefreshBoard();
                });
            }
        }
        else
        {
            // 第三次捕获后，显示"You escape from the nun!"（原文如此）
            if (DialogPanel.Instance != null)
            {
                DialogPanel.Instance.ShowDialog("You escape from the nun!", () =>
                {
                    // 进入胜利流程
                    ShowVictory();
                });
            }
        }
    }
    
    private void RefreshBoard()
    {
        // 刷新board（light不会清除，不会进入下一关）
        // 保持当前状态，只重新生成board
        // 如果是nun boss关卡且还有门没开完，需要重新添加door卡
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        bool isNunBossLevel = levelInfo.boss != null && levelInfo.boss.ToLower() == "nun";
        
        if (isNunBossLevel && nunDoorCount < 3)
        {
            // 如果还有门没开完，重新添加door卡
            if (CardInfoManager.Instance != null)
            {
                CardInfo doorCardInfo = CreateBossCardInfo("door", "Door", CardType.Door);
                if (doorCardInfo != null && !CardInfoManager.Instance.HasCard("door"))
                {
                    CardInfoManager.Instance.AddTemporaryCard(doorCardInfo.identifier, doorCardInfo);
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
        // 所有gift变成gold
        gameData.coins += gameData.gifts;
        gameData.gifts = 0;
        
        // 清空board
        if (boardManager != null)
        {
            boardManager.ClearBoard();
        }
        
        // 结束boss关卡时，移除boss卡和其他新加入的卡，并加回bell卡
        CleanupBossLevelCards();
        
        shopManager?.ShowShop();
    }
    
    private void PrepareBossLevelCards(string bossType)
    {
        if (CardInfoManager.Instance == null || CSVLoader.Instance == null) return;
        
        // 保存bell卡信息（如果存在）
        bellCardInfo = CardInfoManager.Instance.GetCardInfo("bell");
        
        // 移除bell卡（无论是否存在，都尝试移除）
        CardInfoManager.Instance.RemoveTemporaryCard("bell");
        
        string bossTypeLower = bossType.ToLower();
        
        // 添加boss卡
        CardInfo bossCardInfo = null;
        if (bossTypeLower == "nun")
        {
            bossCardInfo = CreateBossCardInfo("nun", "Nun", CardType.Nun);
        }
        else if (bossTypeLower == "snowman")
        {
            bossCardInfo = CreateBossCardInfo("snowman", "Snowman", CardType.Snowman);
        }
        else if (bossTypeLower == "horribleman")
        {
            bossCardInfo = CreateBossCardInfo("horribleman", "Horribleman", CardType.Horribleman);
        }
        
        if (bossCardInfo != null)
        {
            CardInfoManager.Instance.AddTemporaryCard(bossCardInfo.identifier, bossCardInfo);
        }
        
        // nun boss还需要加入door卡
        if (bossTypeLower == "nun")
        {
            CardInfo doorCardInfo = CreateBossCardInfo("door", "Door", CardType.Door);
            if (doorCardInfo != null)
            {
                CardInfoManager.Instance.AddTemporaryCard(doorCardInfo.identifier, doorCardInfo);
            }
        }
    }
    
    private CardInfo CreateBossCardInfo(string identifier, string name, CardType cardType)
    {
        // 创建boss卡或door卡的CardInfo
        CardInfo cardInfo = new CardInfo
        {
            identifier = identifier,
            name = name,
            cost = 0,
            costIncrease = 0,
            desc = "",
            canDraw = false,
            start = 1, // boss卡和door卡在卡组中至少出现1次
            isFixed = false,
            level = 0,
            maxCount = 0,
            isEnemy = false // 默认不是敌人，需要在CSV中设置
        };
        
        // 根据boss类型设置数量和isEnemy
        if (identifier == "nun")
        {
            cardInfo.start = 1; // nun boss出现1次
            cardInfo.isEnemy = true; // nun boss是敌人
        }
        else if (identifier == "snowman")
        {
            cardInfo.start = 1; // snowman boss出现1次
            cardInfo.isEnemy = true; // snowman boss是敌人
        }
        else if (identifier == "horribleman")
        {
            cardInfo.start = 1; // horribleman boss出现1次
            cardInfo.isEnemy = true; // horribleman boss是敌人
        }
        else if (identifier == "door")
        {
            cardInfo.start = 1; // door卡每次只添加1个
            cardInfo.isEnemy = false; // door卡不是敌人
        }
        
        return cardInfo;
    }
    
    private void CleanupBossLevelCards()
    {
        if (CardInfoManager.Instance == null) return;
        
        // 移除boss卡
        CardInfoManager.Instance.RemoveTemporaryCard("nun");
        CardInfoManager.Instance.RemoveTemporaryCard("snowman");
        CardInfoManager.Instance.RemoveTemporaryCard("horribleman");
        
        // 移除door卡
        CardInfoManager.Instance.RemoveTemporaryCard("door");
        
        // 加回bell卡
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
}
