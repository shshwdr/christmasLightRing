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
        isFlashlightRevealing = false;
        currentHintPosition = new Vector2Int(-1, -1);
        CursorManager.Instance?.ResetCursor();
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
                // 如果使用手电筒，敌人不造成伤害，但礼物清零
                if (isFlashlightRevealing)
                {
                    //gameData.gifts = 0;
                }
                else
                {
                    gameData.health--;
                    gameData.gifts = 0;
                    if (gameData.health <= 0)
                    {
                        GameOver();
                        return;
                    }
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
            }
            
            isUsingFlashlight = false;
            uiManager?.UpdateFlashlightButton();
            CursorManager.Instance?.ResetCursor();
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
