using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BoardManager : MonoBehaviour
{
    public GameObject tilePrefab;
    public Transform boardParent;
    
    public CardDeckConfig deckConfig;
    
    public Sprite blankSprite;
    public Sprite coinSprite;
    public Sprite giftSprite;
    public Sprite enemySprite;
    public Sprite flashlightSprite;
    public Sprite hintSprite;
    public Sprite policeStationSprite;
    public Sprite playerSprite;
    
    private Image[,] tileImages = new Image[5, 5];
    private CardType[,] cardTypes = new CardType[5, 5];
    private bool[,] isRevealed = new bool[5, 5];
    private List<CardType> cardDeck = new List<CardType>();
    
    public void GenerateBoard()
    {
        CreateCardDeck();
        ShuffleDeck();
        
        int deckIndex = 0;
        int centerRow = 2;
        int centerCol = 2;
        
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                GameObject tileObj = Instantiate(tilePrefab, boardParent);
                tileObj.name = $"Tile_{row}_{col}";
                
                RectTransform rect = tileObj.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(col * 100 - 200, (4 - row) * 100 - 200);
                rect.sizeDelta = new Vector2(90, 90);
                
                Image image = tileObj.GetComponent<Image>();
                Button button = tileObj.GetComponent<Button>();
                
                CardType cardType;
                bool revealed = false;
                
                if (row == centerRow && col == centerCol)
                {
                    cardType = CardType.Player;
                    revealed = true;
                }
                else
                {
                    cardType = cardDeck[deckIndex++];
                }
                
                cardTypes[row, col] = cardType;
                isRevealed[row, col] = revealed;
                tileImages[row, col] = image;
                
                int r = row;
                int c = col;
                if (button != null)
                {
                    button.onClick.AddListener(() => OnTileClicked(r, c));
                }
                
                UpdateTileVisual(row, col);
            }
        }
    }
    
    private void CreateCardDeck()
    {
        cardDeck.Clear();
        
        for (int i = 0; i < deckConfig.coinCount; i++)
            cardDeck.Add(CardType.Coin);
        for (int i = 0; i < deckConfig.giftCount; i++)
            cardDeck.Add(CardType.Gift);
        for (int i = 0; i < deckConfig.enemyCount; i++)
            cardDeck.Add(CardType.Enemy);
        for (int i = 0; i < deckConfig.flashlightCount; i++)
            cardDeck.Add(CardType.Flashlight);
        for (int i = 0; i < deckConfig.hintCount; i++)
            cardDeck.Add(CardType.Hint);
        for (int i = 0; i < deckConfig.policeStationCount; i++)
            cardDeck.Add(CardType.PoliceStation);
        
        int totalUsed = deckConfig.coinCount + deckConfig.giftCount + deckConfig.enemyCount +
                       deckConfig.flashlightCount + deckConfig.hintCount + deckConfig.policeStationCount;
        int blankCount = 25 - totalUsed - 1; // -1 for center player
        
        for (int i = 0; i < blankCount; i++)
            cardDeck.Add(CardType.Blank);
    }
    
    private void ShuffleDeck()
    {
        for (int i = cardDeck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            CardType temp = cardDeck[i];
            cardDeck[i] = cardDeck[j];
            cardDeck[j] = temp;
        }
    }
    
    private void UpdateTileVisual(int row, int col)
    {
        Image image = tileImages[row, col];
        if (image == null) return;
        
        CardType cardType = cardTypes[row, col];
        Sprite sprite = null;
        
        switch (cardType)
        {
            case CardType.Blank:
                sprite = blankSprite;
                break;
            case CardType.Coin:
                sprite = coinSprite;
                break;
            case CardType.Gift:
                sprite = giftSprite;
                break;
            case CardType.Enemy:
                sprite = enemySprite;
                break;
            case CardType.Flashlight:
                sprite = flashlightSprite;
                break;
            case CardType.Hint:
                sprite = hintSprite;
                break;
            case CardType.PoliceStation:
                sprite = policeStationSprite;
                break;
            case CardType.Player:
                sprite = playerSprite;
                break;
        }
        
        if (sprite != null)
        {
            image.sprite = sprite;
        }
        
        if (isRevealed[row, col])
        {
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        }
    }
    
    private void OnTileClicked(int row, int col)
    {
        if (GameManager.Instance == null) return;
        
        if (!isRevealed[row, col])
        {
            if (GameManager.Instance.IsUsingFlashlight())
            {
                GameManager.Instance.UseFlashlightToReveal(row, col);
            }
            else if (GameManager.Instance.CanRevealTile(row, col))
            {
                RevealTile(row, col);
            }
        }
        else if (isRevealed[row, col] && cardTypes[row, col] == CardType.Hint)
        {
            GameManager.Instance.ShowHint(row, col);
        }
    }
    
    public void RevealTile(int row, int col)
    {
        if (isRevealed[row, col]) return;
        
        isRevealed[row, col] = true;
        UpdateTileVisual(row, col);
        GameManager.Instance.OnTileRevealed(row, col, cardTypes[row, col]);
    }
    
    public CardType GetCardType(int row, int col)
    {
        if (row < 0 || row >= 5 || col < 0 || col >= 5)
            return CardType.Blank;
        return cardTypes[row, col];
    }
    
    public bool IsRevealed(int row, int col)
    {
        if (row < 0 || row >= 5 || col < 0 || col >= 5)
            return false;
        return isRevealed[row, col];
    }
    
    public bool IsAdjacentToRevealed(int row, int col)
    {
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        
        for (int i = 0; i < 4; i++)
        {
            int newRow = row + dx[i];
            int newCol = col + dy[i];
            
            if (IsRevealed(newRow, newCol))
            {
                return true;
            }
        }
        
        return false;
    }
    
    public List<Vector2Int> GetAllEnemyPositions()
    {
        List<Vector2Int> enemies = new List<Vector2Int>();
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                if (cardTypes[row, col] == CardType.Enemy)
                {
                    enemies.Add(new Vector2Int(row, col));
                }
            }
        }
        return enemies;
    }
    
    public void ClearBoard()
    {
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                if (tileImages[row, col] != null)
                {
                    Destroy(tileImages[row, col].gameObject);
                }
            }
        }
    }
}
