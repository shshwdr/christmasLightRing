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
    private Vector2Int currentHintPosition = new Vector2Int(-1, -1);
    
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
        if (boardManager != null)
        {
            boardManager.ClearBoard();
            boardManager.GenerateBoard();
        }
        
        isUsingFlashlight = false;
        isFlashlightRevealing = false;
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
    
    public void OnTileRevealed(int row, int col, CardType cardType, bool isLastTile = false, bool isLastSafeTile = false)
    {
        bool isSafeTile = (cardType != CardType.Enemy);
        
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
                
                // 如果使用手电筒，敌人不造成伤害，但礼物清零
                if (isFlashlightRevealing)
                {
                    // 触发chaseGrinchGiveGift升级项效果
                    upgradeManager?.OnChaseGrinchWithLight();
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
        }
        
        // patternRecognition: 当翻开safe tile时，增加sequence计数
        // safe tile包括：所有非Enemy的tile，以及用light翻开的Enemy（因为不会造成伤害）
        bool isPatternSafeTile = isSafeTile || (cardType == CardType.Enemy && isFlashlightRevealing);
        if (isPatternSafeTile)
        {
            upgradeManager?.OnSafeTileRevealed();
        }
        else if (cardType == CardType.Enemy && !isFlashlightRevealing)
        {
            // 不用light翻开Enemy时，重置sequence
            upgradeManager?.OnNonSafeTileRevealed();
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
        
        uiManager?.UpdateUI();
        uiManager?.UpdateEnemyCount();
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
                // 检查是否是未翻开的安全格子（非Enemy）
                if (!boardManager.IsRevealed(newRow, newCol))
                {
                    CardType cardType = boardManager.GetCardType(newRow, newCol);
                    if (cardType != CardType.Enemy)
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
        gameData.currentLevel++;
        shopManager?.HideShop();
        StartNewLevel();
    }
    
    private void GameOver()
    {
        uiManager?.ShowGameOver();
    }
}
