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
    
    private Tile[,] tiles = new Tile[5, 5];
    private CardType[,] cardTypes = new CardType[5, 5];
    private bool[,] isRevealed = new bool[5, 5];
    private List<CardType> cardDeck = new List<CardType>();
    
    private HashSet<Vector2Int> revealedTiles = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> unrevealedTiles = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> revealableTiles = new HashSet<Vector2Int>();
    
    public void GenerateBoard()
    {
        CreateCardDeck();
        ShuffleDeck();
        
        revealedTiles.Clear();
        unrevealedTiles.Clear();
        revealableTiles.Clear();
        
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
                
                Tile tile = tileObj.GetComponent<Tile>();
                Button button = tileObj.GetComponent<Button>();
                
                CardType cardType;
                bool revealed = false;
                
                if (row == centerRow && col == centerCol)
                {
                    cardType = CardType.Player;
                    revealed = true;
                    revealedTiles.Add(new Vector2Int(row, col));
                }
                else
                {
                    cardType = cardDeck[deckIndex++];
                    if (cardType == CardType.PoliceStation)
                    {
                        revealed = true;
                        revealedTiles.Add(new Vector2Int(row, col));
                    }
                    else
                    {
                        unrevealedTiles.Add(new Vector2Int(row, col));
                    }
                }
                
                cardTypes[row, col] = cardType;
                isRevealed[row, col] = revealed;
                
                Sprite frontSprite = GetSpriteForCardType(cardType);
                tile.Initialize(row, col, cardType, revealed);
                tile.SetFrontSprite(frontSprite);
                
                tiles[row, col] = tile;
            }
        }
        
        // 初始化可翻开列表：所有已翻开tile（player和station）的邻居
        foreach (Vector2Int revealedPos in revealedTiles)
        {
            AddNeighborsToRevealable(revealedPos.x, revealedPos.y);
        }
        
        // 更新所有tile的revealable状态
        UpdateRevealableVisuals();
    }
    
    private void UpdateRevealableVisuals()
    {
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                if (tiles[row, col] != null)
                {
                    Vector2Int pos = new Vector2Int(row, col);
                    bool revealable = revealableTiles.Contains(pos);
                    tiles[row, col].SetRevealable(revealable);
                }
            }
        }
    }
    
    private Sprite GetSpriteForCardType(CardType cardType)
    {
        switch (cardType)
        {
            case CardType.Blank:
                return blankSprite;
            case CardType.Coin:
                return coinSprite;
            case CardType.Gift:
                return giftSprite;
            case CardType.Enemy:
                return enemySprite;
            case CardType.Flashlight:
                return flashlightSprite;
            case CardType.Hint:
                return hintSprite;
            case CardType.PoliceStation:
                return policeStationSprite;
            case CardType.Player:
                return playerSprite;
            default:
                return blankSprite;
        }
    }
    
    private void AddNeighborsToRevealable(int row, int col)
    {
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        
        for (int i = 0; i < 4; i++)
        {
            int newRow = row + dx[i];
            int newCol = col + dy[i];
            
            if (newRow < 0 || newRow >= 5 || newCol < 0 || newCol >= 5)
                continue;
            
            Vector2Int pos = new Vector2Int(newRow, newCol);
            
            // 如果未翻开，加入可翻开列表
            if (!isRevealed[newRow, newCol] && unrevealedTiles.Contains(pos))
            {
                revealableTiles.Add(pos);
            }
        }
    }
    
    private void UpdateRevealableForTile(int row, int col)
    {
        if (tiles[row, col] != null)
        {
            Vector2Int pos = new Vector2Int(row, col);
            bool revealable = revealableTiles.Contains(pos);
            tiles[row, col].SetRevealable(revealable);
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
    
    public void RevealTile(int row, int col)
    {
        if (isRevealed[row, col]) return;
        
        Vector2Int pos = new Vector2Int(row, col);
        
        // 从未翻开列表移除
        unrevealedTiles.Remove(pos);
        // 从可翻开列表移除
        revealableTiles.Remove(pos);
        // 加入已翻开列表
        revealedTiles.Add(pos);
        
        isRevealed[row, col] = true;
        
        if (tiles[row, col] != null)
        {
            tiles[row, col].SetRevealed(true);
        }
        
        // 把周围的未翻开格子加入可翻开列表
        AddNeighborsToRevealable(row, col);
        
        // 更新所有tile的revealable状态
        UpdateRevealableVisuals();
        
        GameManager.Instance.OnTileRevealed(row, col, cardTypes[row, col]);
    }
    
    public bool CanRevealTile(int row, int col)
    {
        Vector2Int pos = new Vector2Int(row, col);
        return revealableTiles.Contains(pos);
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
                if (tiles[row, col] != null)
                {
                    Destroy(tiles[row, col].gameObject);
                }
            }
        }
    }
}
