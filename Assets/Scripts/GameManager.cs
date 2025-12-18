using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    
    public BoardManager boardManager;
    public UIManager uiManager;
    public ShopManager shopManager;
    
    public GameData gameData = new GameData();
    public int initialHealth = 3;
    
    private bool isUsingFlashlight = false;
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

        boardManager = FindObjectOfType<BoardManager>();
        uiManager = FindObjectOfType<UIManager>();
        shopManager = FindObjectOfType<ShopManager>();
    }
    
    private void Start()
    {
        gameData.health = initialHealth;
        StartNewLevel();
    }
    
    public void StartNewLevel()
    {
        if (boardManager != null)
        {
            boardManager.ClearBoard();
            boardManager.GenerateBoard();
        }
        
        isUsingFlashlight = false;
        currentHintPosition = new Vector2Int(-1, -1);
        uiManager?.UpdateUI();
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
    
    public void OnTileRevealed(int row, int col, CardType cardType)
    {
        bool safeReveal = isUsingFlashlight;
        isUsingFlashlight = false;
        uiManager?.UpdateFlashlightButton();
        
        if (safeReveal && cardType == CardType.Enemy)
        {
            // 手电筒保护，敌人不造成伤害
            return;
        }
        
        switch (cardType)
        {
            case CardType.Blank:
                break;
            case CardType.Coin:
                gameData.coins++;
                break;
            case CardType.Gift:
                gameData.gifts++;
                break;
            case CardType.Enemy:
                gameData.health--;
                gameData.gifts = 0;
                if (gameData.health <= 0)
                {
                    GameOver();
                    return;
                }
                break;
            case CardType.Flashlight:
                gameData.flashlights++;
                break;
            case CardType.Hint:
                ShowHint(row, col);
                break;
            case CardType.PoliceStation:
                break;
            case CardType.Player:
                break;
        }
        
        uiManager?.UpdateUI();
    }
    
    public void ShowHint(int row, int col)
    {
        currentHintPosition = new Vector2Int(row, col);
        List<Vector2Int> enemies = boardManager.GetAllEnemyPositions();
        
        List<string> hints = new List<string>();
        
        // 当前行敌人数量
        int rowEnemies = 0;
        for (int c = 0; c < 5; c++)
        {
            if (boardManager.GetCardType(row, c) == CardType.Enemy)
                rowEnemies++;
        }
        hints.Add($"当前行有 {rowEnemies} 个敌人");
        
        // 当前列敌人数量
        int colEnemies = 0;
        for (int r = 0; r < 5; r++)
        {
            if (boardManager.GetCardType(r, col) == CardType.Enemy)
                colEnemies++;
        }
        hints.Add($"当前列有 {colEnemies} 个敌人");
        
        // 周围3x3区域敌人数量
        int nearbyEnemies = 0;
        for (int r = row - 1; r <= row + 1; r++)
        {
            for (int c = col - 1; c <= col + 1; c++)
            {
                if (boardManager.GetCardType(r, c) == CardType.Enemy)
                    nearbyEnemies++;
            }
        }
        hints.Add($"周围3x3区域有 {nearbyEnemies} 个敌人");
        
        // 敌人分布在几行
        HashSet<int> enemyRows = new HashSet<int>();
        foreach (Vector2Int enemy in enemies)
        {
            enemyRows.Add(enemy.x);
        }
        hints.Add($"敌人分布在 {enemyRows.Count} 行");
        
        // 敌人分布在几列
        HashSet<int> enemyCols = new HashSet<int>();
        foreach (Vector2Int enemy in enemies)
        {
            enemyCols.Add(enemy.y);
        }
        hints.Add($"敌人分布在 {enemyCols.Count} 列");
        
        string hint = hints[Random.Range(0, hints.Count)];
        uiManager?.ShowHint(hint);
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
        }
    }
    
    public void UseFlashlightToReveal(int row, int col)
    {
        if (isUsingFlashlight && gameData.flashlights > 0)
        {
            gameData.flashlights--;
            isUsingFlashlight = false;
            uiManager?.UpdateFlashlightButton();
            if (!boardManager.IsRevealed(row, col))
            {
                boardManager.RevealTile(row, col);
            }
        }
    }
    
    public void EndTurn()
    {
        gameData.coins += gameData.gifts;
        gameData.gifts = 0;
        shopManager?.ShowShop();
    }
    
    public void NextLevel()
    {
        gameData.currentLevel++;
        StartNewLevel();
        shopManager?.HideShop();
    }
    
    private void GameOver()
    {
        uiManager?.ShowGameOver();
    }
}
