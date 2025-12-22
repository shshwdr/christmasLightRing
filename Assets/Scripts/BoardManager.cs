using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class BoardManager : MonoBehaviour
{
    public GameObject tilePrefab;
    public Transform boardParent;
    
    private Tile[,] tiles;
    private CardType[,] cardTypes;
    private bool[,] isRevealed;
    private List<CardType> cardDeck = new List<CardType>();
    private Dictionary<Vector2Int, string> hintContents = new Dictionary<Vector2Int, string>();
    private HashSet<string> usedHints = new HashSet<string>();
    
    private HashSet<Vector2Int> revealedTiles = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> unrevealedTiles = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> revealableTiles = new HashSet<Vector2Int>();
    
    private int currentRow = 5;
    private int currentCol = 5;
    
    public void GenerateBoard()
    {
        // 获取当前关卡信息
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        currentRow = levelInfo.row;
        currentCol = levelInfo.col;
        
        // 初始化数组
        tiles = new Tile[currentRow, currentCol];
        cardTypes = new CardType[currentRow, currentCol];
        isRevealed = new bool[currentRow, currentCol];
        
        CreateCardDeck();
        ShuffleDeck();
        
        revealedTiles.Clear();
        unrevealedTiles.Clear();
        revealableTiles.Clear();
        hintContents.Clear();
        usedHints.Clear();
        
        // 初始化棋盘为空白
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                cardTypes[row, col] = CardType.Blank;
                isRevealed[row, col] = false;
            }
        }
        
        // 使用LevelManager计算玩家位置（尽量最中间，如果是偶数则往下一行）
        Vector2Int playerPos = LevelManager.Instance.GetPlayerPosition(currentRow, currentCol);
        int centerRow = playerPos.x;
        int centerCol = playerPos.y;
        cardTypes[centerRow, centerCol] = CardType.Player;
        isRevealed[centerRow, centerCol] = true;
        revealedTiles.Add(new Vector2Int(centerRow, centerCol));
        
        // 从卡组中移除player（如果存在）
        List<CardType> remainingDeck = new List<CardType>(cardDeck);
        remainingDeck.Remove(CardType.Player);
        
        // 确保isFixed的卡牌被使用（除了player）
        List<CardType> fixedCards = new List<CardType>();
        List<CardInfo> allCards = CardInfoManager.Instance.GetAllCards();
        foreach (CardInfo cardInfo in allCards)
        {
            if (cardInfo.isFixed)
            {
                CardType cardType = CardInfoManager.Instance.GetCardType(cardInfo.identifier);
                if (cardType != CardType.Player && !remainingDeck.Contains(cardType))
                {
                    // 如果卡组中没有这个isFixed的卡，添加一张
                    fixedCards.Add(cardType);
                }
            }
        }
        remainingDeck.AddRange(fixedCards);
        
        // 打乱卡组
        for (int i = remainingDeck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            CardType temp = remainingDeck[i];
            remainingDeck[i] = remainingDeck[j];
            remainingDeck[j] = temp;
        }
        
        // 随机抽取卡牌填充空白位置
        int deckIndex = 0;
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.Blank)
                {
                    if (deckIndex < remainingDeck.Count)
                    {
                        CardType cardType = remainingDeck[deckIndex++];
                        cardTypes[row, col] = cardType;
                        
                        if (cardType == CardType.PoliceStation)
                        {
                            isRevealed[row, col] = true;
                            revealedTiles.Add(new Vector2Int(row, col));
                        }
                        else
                        {
                            unrevealedTiles.Add(new Vector2Int(row, col));
                        }
                    }
                    else
                    {
                        // 如果卡组用完了，剩余位置保持为Blank，也要加入unrevealedTiles
                        unrevealedTiles.Add(new Vector2Int(row, col));
                    }
                }
            }
        }
        
        // 创建tile对象
        float tileSize = 100f;
        float offsetX = (currentCol - 1) * tileSize * 0.5f;
        float offsetY = (currentRow - 1) * tileSize * 0.5f;
        
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                GameObject tileObj = Instantiate(tilePrefab, boardParent);
                tileObj.name = $"Tile_{row}_{col}";
                
                RectTransform rect = tileObj.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(col * tileSize - offsetX, (currentRow - 1 - row) * tileSize - offsetY);
                rect.sizeDelta = new Vector2(tileSize, tileSize);
                
                Tile tile = tileObj.GetComponent<Tile>();
                CardType cardType = cardTypes[row, col];
                bool revealed = isRevealed[row, col];
                
                Sprite frontSprite = GetSpriteForCardType(cardType);
                if (frontSprite == null)
                {
                    frontSprite = CardInfoManager.Instance.GetCardSprite(CardType.Blank);
                }
                tile.Initialize(row, col, cardType, revealed);
                tile.SetFrontSprite(frontSprite);
                
                tiles[row, col] = tile;
            }
        }
        
        AddNeighborsToRevealable(centerRow, centerCol);
        
        // 检查player是否和police相邻，如果相邻，则把police周围的格子也加入可翻开列表
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        for (int i = 0; i < 4; i++)
        {
            int newRow = centerRow + dx[i];
            int newCol = centerCol + dy[i];
            if (newRow >= 0 && newRow < currentRow && newCol >= 0 && newCol < currentCol)
            {
                if (cardTypes[newRow, newCol] == CardType.PoliceStation)
                {
                    // player和police相邻，把police周围的格子加入可翻开列表
                    AddNeighborsToRevealable(newRow, newCol);
                    break;
                }
            }
        }
        
        // 更新所有tile的revealable状态
        UpdateRevealableVisuals();
        
        // 更新所有Sign卡片的箭头指向
        UpdateSignArrows();
    }
    
    public Vector2Int GetBellPosition()
    {
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.Bell)
                {
                    return new Vector2Int(row, col);
                }
            }
        }
        return new Vector2Int(-1, -1); // 未找到bell
    }
    
    private void UpdateSignArrows()
    {
        Vector2Int bellPos = GetBellPosition();
        if (bellPos.x < 0) return; // 没有bell，不需要更新箭头
        
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.Sign && tiles[row, col] != null)
                {
                    tiles[row, col].UpdateSignArrow(bellPos.x, bellPos.y, row, col);
                }
            }
        }
    }
    
    private void UpdateRevealableVisuals()
    {
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
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
    
    public void UpdateAllTilesVisual()
    {
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (tiles[row, col] != null)
                {
                    tiles[row, col].UpdateVisual();
                }
            }
        }
    }
    
    private Sprite GetSpriteForCardType(CardType cardType)
    {
        if (CardInfoManager.Instance != null)
        {
            return CardInfoManager.Instance.GetCardSprite(cardType);
        }
        return null;
    }
    
    private void AddNeighborsToRevealable(int row, int col)
    {
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        
        for (int i = 0; i < 4; i++)
        {
            int newRow = row + dx[i];
            int newCol = col + dy[i];
            
            if (newRow < 0 || newRow >= currentRow || newCol < 0 || newCol >= currentCol)
                continue;
            
            Vector2Int pos = new Vector2Int(newRow, newCol);
            
            // 如果未翻开，加入可翻开列表（不检查unrevealedTiles，因为所有未翻开的位置都应该可以被探索）
            if (!isRevealed[newRow, newCol])
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
        
        if (CardInfoManager.Instance == null) return;
        
        // 获取当前关卡信息
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        int targetEnemyCount = levelInfo.enemyCount;
        
        // 从CardInfo获取起始数量，包括购买的卡牌
        List<CardInfo> allCards = CardInfoManager.Instance.GetAllCards();
        
        foreach (CardInfo cardInfo in allCards)
        {
            CardType cardType = CardInfoManager.Instance.GetCardType(cardInfo.identifier);
            if (cardType == CardType.Blank) continue; // 空白卡不在这里添加
            
            int count = cardInfo.start;
            
            // 如果是敌人（grinch），使用关卡配置的数量
            if (cardType == CardType.Enemy)
            {
                count = targetEnemyCount;
            }
            else
            {
                // 如果是购买的卡牌，增加数量
                if (GameManager.Instance != null && GameManager.Instance.gameData.purchasedCards.Contains(cardType))
                {
                    count++;
                }
            }
            
            // isFixed的卡牌确保被使用（至少1张），但不固定位置（除了player）
            // player会单独处理，所以这里如果是player且isFixed，不需要减少
            
            for (int i = 0; i < count; i++)
            {
                cardDeck.Add(cardType);
            }
        }
        
        // 计算需要的空白卡数量
        int totalTiles = currentRow * currentCol;
        int totalUsed = 1; // player固定在中间，占1个位置
        totalUsed += cardDeck.Count;
        int blankCount = totalTiles - totalUsed;
        
        if (blankCount > 0)
        {
            for (int i = 0; i < blankCount; i++)
            {
                cardDeck.Add(CardType.Blank);
            }
        }
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
        
        // 如果是hint卡，在翻开时计算并保存提示内容
        if (cardTypes[row, col] == CardType.Hint && !hintContents.ContainsKey(pos))
        {
            string hint = CalculateHint(row, col);
            hintContents[pos] = hint;
        }
        
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
        
        // 检查翻开的格子是否与police相邻，如果相邻，则把police周围的格子也加入可翻开列表
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        for (int i = 0; i < 4; i++)
        {
            int newRow = row + dx[i];
            int newCol = col + dy[i];
            if (newRow >= 0 && newRow < currentRow && newCol >= 0 && newCol < currentCol)
            {
                if (cardTypes[newRow, newCol] == CardType.PoliceStation && isRevealed[newRow, newCol])
                {
                    // 翻开的格子与police相邻，把police周围的格子加入可翻开列表
                    AddNeighborsToRevealable(newRow, newCol);
                }
            }
        }
        
        // 更新所有tile的revealable状态
        UpdateRevealableVisuals();
        
        // 如果翻开的是Sign卡或Bell卡，更新所有Sign的箭头
        if (cardTypes[row, col] == CardType.Sign || cardTypes[row, col] == CardType.Bell)
        {
            UpdateSignArrows();
        }
        
        // 检查是否是最后一个tile或最后一个safe tile
        bool isLastTile = unrevealedTiles.Count == 0;
        bool isLastSafeTile = IsLastSafeTile();
        
        GameManager.Instance.OnTileRevealed(row, col, cardTypes[row, col], isLastTile, isLastSafeTile);
    }
    
    private bool IsLastSafeTile()
    {
        // 检查是否还有未reveal的safe tile（除了grinch之外的tile）
        foreach (Vector2Int pos in unrevealedTiles)
        {
            if (cardTypes[pos.x, pos.y] != CardType.Enemy)
            {
                return false;
            }
        }
        return true;
    }
    
    private string CalculateHint(int row, int col)
    {
        List<Vector2Int> enemies = GetAllEnemyPositions();
        List<string> hints = new List<string>();
        
        // Hint所在行有几个敌人
        int rowEnemies = 0;
        for (int c = 0; c < currentCol; c++)
        {
            if (cardTypes[row, c] == CardType.Enemy)
                rowEnemies++;
        }
        hints.Add($"This row has {rowEnemies} enem{(rowEnemies != 1 ? "ies" : "y")}");
        
        // Hint所在列有几个敌人
        int colEnemies = 0;
        for (int r = 0; r < currentRow; r++)
        {
            if (cardTypes[r, col] == CardType.Enemy)
                colEnemies++;
        }
        hints.Add($"This column has {colEnemies} enem{(colEnemies != 1 ? "ies" : "y")}");
        
        // // 左边和右边的敌人数量的比较（相对于hint所在位置）
        // int leftEnemies = 0;
        // int rightEnemies = 0;
        // // 计算hint所在行的左边部分（0到col-1）
        // for (int c = 0; c < col; c++)
        // {
        //     if (cardTypes[row, c] == CardType.Enemy)
        //         leftEnemies++;
        // }
        // // 计算hint所在行的右边部分（col+1到currentCol-1）
        // for (int c = col + 1; c < currentCol; c++)
        // {
        //     if (cardTypes[row, c] == CardType.Enemy)
        //         rightEnemies++;
        // }
        // if (leftEnemies > rightEnemies)
        // {
        //     int diff = leftEnemies - rightEnemies;
        //     hints.Add($"Left side of this position has {diff} more enemy{(diff != 1 ? "ies" : "y")} than right side");
        // }
        // else if (rightEnemies > leftEnemies)
        // {
        //     int diff = rightEnemies - leftEnemies;
        //     hints.Add($"Right side of this position has {diff} more enemy{(diff != 1 ? "ies" : "y")} than left side");
        // }
        // else
        // {
        //     hints.Add($"Left and right sides of this position have the same number of enemies ({leftEnemies})");
        // }
        //
        // // 上边和下面的敌人数量的比较（相对于hint所在位置）
        // int topEnemies = 0;
        // int bottomEnemies = 0;
        // // 计算hint所在列的上边部分（0到row-1）
        // for (int r = 0; r < row; r++)
        // {
        //     if (cardTypes[r, col] == CardType.Enemy)
        //         topEnemies++;
        // }
        // // 计算hint所在列的下边部分（row+1到currentRow-1）
        // for (int r = row + 1; r < currentRow; r++)
        // {
        //     if (cardTypes[r, col] == CardType.Enemy)
        //         bottomEnemies++;
        // }
        // if (topEnemies > bottomEnemies)
        // {
        //     int diff = topEnemies - bottomEnemies;
        //     hints.Add($"Top side of this position has {diff} more enemy{(diff != 1 ? "ies" : "y")} than bottom side");
        // }
        // else if (bottomEnemies > topEnemies)
        // {
        //     int diff = bottomEnemies - topEnemies;
        //     hints.Add($"Bottom side of this position has {diff} more enemy{(diff != 1 ? "ies" : "y")} than top side");
        // }
        // else
        // {
        //     hints.Add($"Top and bottom sides of this position have the same number of enemies ({topEnemies})");
        // }
        // there are more enemies to the left of the hint than to the right
        
        
        
        // 有几个敌人在四个角落
        int cornerEnemies = 0;
        Vector2Int[] corners = { 
            new Vector2Int(0, 0), 
            new Vector2Int(0, currentCol - 1), 
            new Vector2Int(currentRow - 1, 0), 
            new Vector2Int(currentRow - 1, currentCol - 1) 
        };
        foreach (Vector2Int corner in corners)
        {
            if (corner.x >= 0 && corner.x < currentRow && corner.y >= 0 && corner.y < currentCol &&
                cardTypes[corner.x, corner.y] == CardType.Enemy)
                cornerEnemies++;
        }
        hints.Add($"There {(cornerEnemies == 1 ? "is" : "are")} {cornerEnemies} enem{(cornerEnemies != 1 ? "ies" : "y")} in the four corners");
        
        // 保留原有的提示类型
        // Nearby 3x3 area enemy count
        int nearbyEnemies = 0;
        for (int r = row - 1; r <= row + 1; r++)
        {
            for (int c = col - 1; c <= col + 1; c++)
            {
                if (r >= 0 && r < currentRow && c >= 0 && c < currentCol && cardTypes[r, c] == CardType.Enemy)
                    nearbyEnemies++;
            }
        }
        hints.Add($"3x3 area around has {nearbyEnemies} enem{(nearbyEnemies != 1 ? "ies" : "y")}");

        if (enemies.Count > 1)
        {
            
            // 找到最大的敌人group（四向邻接）
            int maxGroupSize = 0;
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            int[] dx = { 0, 0, 1, -1 }; // 上下左右
            int[] dy = { 1, -1, 0, 0 };
        
            foreach (Vector2Int enemy in enemies)
            {
                if (visited.Contains(enemy))
                    continue;
            
                // BFS找到当前敌人所在的group
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(enemy);
                visited.Add(enemy);
                int groupSize = 1;
            
                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                
                    // 检查四个方向的邻居
                    for (int i = 0; i < 4; i++)
                    {
                        int newRow = current.x + dx[i];
                        int newCol = current.y + dy[i];
                        Vector2Int neighbor = new Vector2Int(newRow, newCol);
                    
                        if (newRow >= 0 && newRow < currentRow && newCol >= 0 && newCol < currentCol &&
                            cardTypes[newRow, newCol] == CardType.Enemy && !visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                            groupSize++;
                        }
                    }
                }
            
                if (groupSize > maxGroupSize)
                {
                    maxGroupSize = groupSize;
                }
            }
        
            hints.Add($"The largest group of enemy is {maxGroupSize}");
            
            
            // Enemy rows count
            HashSet<int> enemyRows = new HashSet<int>();
            foreach (Vector2Int enemy in enemies)
            {
                enemyRows.Add(enemy.x);
            }
            hints.Add($"Enemies are in {enemyRows.Count} row{(enemyRows.Count != 1 ? "s" : "")}");
        
            // Enemy columns count
            HashSet<int> enemyCols = new HashSet<int>();
            foreach (Vector2Int enemy in enemies)
            {
                enemyCols.Add(enemy.y);
            }
            hints.Add($"Enemies are in {enemyCols.Count} column{(enemyCols.Count != 1 ? "s" : "")}");
        }
        
        
        // 排除已使用的hint内容
        List<string> availableHints = new List<string>();
        foreach (string hint in hints)
        {
            if (!usedHints.Contains(hint))
            {
                availableHints.Add(hint);
            }
        }
        
        // 如果没有可用的hint，使用所有hint（理论上不应该发生）
        if (availableHints.Count == 0)
        {
            availableHints = hints;
        }
        
        // 随机选择一个未使用的hint
        string selectedHint = availableHints[Random.Range(0, availableHints.Count)];
        usedHints.Add(selectedHint);
        
        return selectedHint;
    }
    
    public string GetHintContent(int row, int col)
    {
        Vector2Int pos = new Vector2Int(row, col);
        if (hintContents.ContainsKey(pos))
        {
            return hintContents[pos];
        }
        return "";
    }
    
    public bool CanRevealTile(int row, int col)
    {
        Vector2Int pos = new Vector2Int(row, col);
        return revealableTiles.Contains(pos);
    }
    
    public CardType GetCardType(int row, int col)
    {
        if (row < 0 || row >= currentRow || col < 0 || col >= currentCol)
            return CardType.Blank;
        return cardTypes[row, col];
    }
    
    public bool IsRevealed(int row, int col)
    {
        if (row < 0 || row >= currentRow || col < 0 || col >= currentCol)
            return false;
        return isRevealed[row, col];
    }
    
    public List<Vector2Int> GetAllEnemyPositions()
    {
        List<Vector2Int> enemies = new List<Vector2Int>();
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.Enemy)
                {
                    enemies.Add(new Vector2Int(row, col));
                }
            }
        }
        return enemies;
    }
    
    public int GetTotalEnemyCount()
    {
        int count = 0;
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.Enemy)
                {
                    count++;
                }
            }
        }
        return count;
    }
    
    public int GetRevealedEnemyCount()
    {
        int count = 0;
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.Enemy && isRevealed[row, col])
                {
                    count++;
                }
            }
        }
        return count;
    }
    
    public void ClearBoard()
    {
        if (tiles == null) return;
        
        // 清理所有现有的 tiles，不管大小
        int rows = tiles.GetLength(0);
        int cols = tiles.GetLength(1);
        
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (tiles[row, col] != null)
                {
                    Destroy(tiles[row, col].gameObject);
                }
            }
        }
        
        // 清理 boardParent 下的所有子对象（以防有遗漏）
        if (boardParent != null)
        {
            for (int i = boardParent.childCount - 1; i >= 0; i--)
            {
                Destroy(boardParent.GetChild(i).gameObject);
            }
        }
    }
    
    // 获取当前地图的行数
    public int GetCurrentRow()
    {
        return currentRow;
    }
    
    // 获取当前地图的列数
    public int GetCurrentCol()
    {
        return currentCol;
    }
}
