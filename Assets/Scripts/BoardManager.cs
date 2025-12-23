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
        
        // 处理snowman boss：在board中最先加入，加入后再在周围四个方向都变成enemy
        // 然后再加入别的enemy。snowman不会添加在player的四个方向
        if (!string.IsNullOrEmpty(levelInfo.boss) && levelInfo.boss.ToLower() == "snowman")
        {
            PlaceSnowmanBossFirst(centerRow, centerCol);
        }
        
        // 处理nun boss：在snowman之后，找到T形布局放置3个nun和1个door
        bool isNunBossLevel = !string.IsNullOrEmpty(levelInfo.boss) && levelInfo.boss.ToLower() == "nun";
        if (isNunBossLevel)
        {
            PlaceNunBossAndDoor(centerRow, centerCol);
        }
        
        // 从卡组中移除player（如果存在）
        List<CardType> remainingDeck = new List<CardType>(cardDeck);
        remainingDeck.Remove(CardType.Player);
        
        // 如果已经放置了snowman boss，从卡组中移除
        if (!string.IsNullOrEmpty(levelInfo.boss) && levelInfo.boss.ToLower() == "snowman")
        {
            remainingDeck.Remove(CardType.Snowman);
        }
        
        // 如果已经放置了nun boss和door，从卡组中移除
        if (isNunBossLevel)
        {
            // 移除所有nun和door（因为它们已经被特殊放置了）
            remainingDeck.RemoveAll(card => card == CardType.Nun || card == CardType.Door);
        }
        
        // 收集所有剩余位置（Blank位置）
        List<Vector2Int> availablePositions = new List<Vector2Int>();
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.Blank)
                {
                    availablePositions.Add(new Vector2Int(row, col));
                }
            }
        }
        
        // 收集所有isFixed的卡类型（除了player，因为已经放置了）
        List<CardInfo> allCards = CardInfoManager.Instance.GetAllCards();
        HashSet<CardType> fixedCardTypes = new HashSet<CardType>();
        foreach (CardInfo cardInfo in allCards)
        {
            if (cardInfo.isFixed)
            {
                CardType cardType = CardInfoManager.Instance.GetCardType(cardInfo.identifier);
                if (cardType != CardType.Player)
                {
                    fixedCardTypes.Add(cardType);
                }
            }
        }
        
        // 统计isFixed卡的数量（从remainingDeck中）
        Dictionary<CardType, int> fixedCardCounts = new Dictionary<CardType, int>();
        List<CardType> otherCards = new List<CardType>();
        
        foreach (CardType cardType in remainingDeck)
        {
            if (fixedCardTypes.Contains(cardType))
            {
                if (!fixedCardCounts.ContainsKey(cardType))
                {
                    fixedCardCounts[cardType] = 0;
                }
                fixedCardCounts[cardType]++;
            }
            else
            {
                otherCards.Add(cardType);
            }
        }
        
        // 计算isFixed卡的总数量
        int totalFixedCards = 0;
        foreach (var count in fixedCardCounts.Values)
        {
            totalFixedCards += count;
        }
        
        // 如果isFixed卡数量比剩余位置多，报错并停止放置
        if (totalFixedCards > availablePositions.Count)
        {
            Debug.LogError($"isFixed cards count ({totalFixedCards}) exceeds available positions ({availablePositions.Count})!");
            // 停止放置，剩余位置保持为Blank
            // foreach (Vector2Int pos in availablePositions)
            // {
            //     unrevealedTiles.Add(pos);
            // }
        }
        //else
        {
            // 先放置isFixed卡：随机分配到剩余位置
            List<CardType> fixedCardsToPlace = new List<CardType>();
            foreach (var kvp in fixedCardCounts)
            {
                for (int i = 0; i < kvp.Value; i++)
                {
                    fixedCardsToPlace.Add(kvp.Key);
                }
            }
            
            // 打乱isFixed卡
            for (int i = fixedCardsToPlace.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                CardType temp = fixedCardsToPlace[i];
                fixedCardsToPlace[i] = fixedCardsToPlace[j];
                fixedCardsToPlace[j] = temp;
            }
            
            // 打乱剩余位置
            for (int i = availablePositions.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                Vector2Int temp = availablePositions[i];
                availablePositions[i] = availablePositions[j];
                availablePositions[j] = temp;
            }
            
            // 放置isFixed卡
            for (int i = 0; i < fixedCardsToPlace.Count && i < availablePositions.Count; i++)
            {
                Vector2Int pos = availablePositions[i];
                CardType cardType = fixedCardsToPlace[i];
                cardTypes[pos.x, pos.y] = cardType;
                
                if (cardType == CardType.PoliceStation)
                {
                    isRevealed[pos.x, pos.y] = true;
                    revealedTiles.Add(pos);
                }
                else
                {
                    unrevealedTiles.Add(pos);
                }
            }
            
            // 移除已使用的位置
            availablePositions.RemoveRange(0, Mathf.Min(fixedCardsToPlace.Count, availablePositions.Count));
            
            // 打乱其他卡
            for (int i = otherCards.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                CardType temp = otherCards[i];
                otherCards[i] = otherCards[j];
                otherCards[j] = temp;
            }
            
            // 放置其他卡到剩余位置
            int otherCardIndex = 0;
            for (int i = 0; i < availablePositions.Count && otherCardIndex < otherCards.Count; i++)
            {
                Vector2Int pos = availablePositions[i];
                CardType cardType = otherCards[otherCardIndex++];
                cardTypes[pos.x, pos.y] = cardType;
                
                if (cardType == CardType.PoliceStation)
                {
                    isRevealed[pos.x, pos.y] = true;
                    revealedTiles.Add(pos);
                }
                else
                {
                    unrevealedTiles.Add(pos);
                }
            }
            
            // 如果还有剩余位置，保持为Blank（它们已经是Blank了，只需要加入到unrevealedTiles）
            for (int i = otherCardIndex; i < availablePositions.Count; i++)
            {
                Vector2Int pos = availablePositions[i];
                // 确保这些位置是Blank（虽然初始化时已经是Blank了）
                cardTypes[pos.x, pos.y] = CardType.Blank;
                unrevealedTiles.Add(pos);
            }
        }
        
        // 统一处理所有未revealed的位置，将它们加入到unrevealedTiles中
        // 这样就不需要单独处理snowman和其周围的enemy了
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (!isRevealed[row, col])
                {
                    Vector2Int pos = new Vector2Int(row, col);
                    // 如果还没有加入到unrevealedTiles中，就加入
                    if (!unrevealedTiles.Contains(pos))
                    {
                        unrevealedTiles.Add(pos);
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
        
        // 处理boss逻辑（snowman boss已经在之前处理了，这里只需要处理其他boss）
        // 注意：snowman boss已经在PlaceSnowmanBossFirst中处理了
        if (string.IsNullOrEmpty(levelInfo.boss) || levelInfo.boss.ToLower() != "snowman")
        {
            HandleBossGeneration(levelInfo);
        }
        
        // 更新所有tile的revealable状态
        UpdateRevealableVisuals();
        
        // 更新所有Sign卡片的箭头指向
        UpdateSignArrows();
    }
    
    private void PlaceSnowmanBossFirst(int playerRow, int playerCol)
    {
        // snowman boss在board中最先加入，不会添加在player的四个方向
        List<Vector2Int> availablePositions = new List<Vector2Int>();
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        HashSet<Vector2Int> playerAdjacent = new HashSet<Vector2Int>();
        
        // 标记player的四个方向为不可用
        for (int i = 0; i < 4; i++)
        {
            int newRow = playerRow + dx[i];
            int newCol = playerCol + dy[i];
            if (newRow >= 0 && newRow < currentRow && newCol >= 0 && newCol < currentCol)
            {
                playerAdjacent.Add(new Vector2Int(newRow, newCol));
            }
        }
        
        // 找到所有可用的位置（不是player，不是player的四个方向）
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                Vector2Int pos = new Vector2Int(row, col);
                if (cardTypes[row, col] == CardType.Blank && !playerAdjacent.Contains(pos))
                {
                    availablePositions.Add(pos);
                }
            }
        }
        
        // 随机选择snowman boss位置
        if (availablePositions.Count > 0)
        {
            int randomIndex = Random.Range(0, availablePositions.Count);
            Vector2Int snowmanPos = availablePositions[randomIndex];
            cardTypes[snowmanPos.x, snowmanPos.y] = CardType.Snowman;
            availablePositions.RemoveAt(randomIndex);
            
            // 在snowman boss相邻位置生成敌人（在填充其他卡牌之前）
            for (int i = 0; i < 4; i++)
            {
                int newRow = snowmanPos.x + dx[i];
                int newCol = snowmanPos.y + dy[i];
                
                if (newRow >= 0 && newRow < currentRow && newCol >= 0 && newCol < currentCol)
                {
                    // 如果这个位置是空白，可以放置敌人
                    if (cardTypes[newRow, newCol] == CardType.Blank)
                    {
                        cardTypes[newRow, newCol] = CardType.Enemy;
                    }
                }
            }
        }
    }
    
    private void HandleBossGeneration(LevelInfo levelInfo)
    {
        if (string.IsNullOrEmpty(levelInfo.boss))
        {
            return; // 不是boss关卡
        }
        
        string bossType = levelInfo.boss.ToLower();
        
        // snowman boss已经在PlaceSnowmanBossFirst中处理了，这里不需要再处理
        // nun boss和door已经在PlaceNunBossAndDoor中处理了，这里不需要再处理
        // horribleman boss会在所有敌人被击败后生成
    }
    
    // 放置nun boss和door：找到T形布局，放置3个nun和1个door
    private void PlaceNunBossAndDoor(int playerRow, int playerCol)
    {
        // 找到所有可用的位置（不是player，不是player的四个方向）
        List<Vector2Int> availablePositions = new List<Vector2Int>();
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        HashSet<Vector2Int> playerAdjacent = new HashSet<Vector2Int>();
        
        // 标记player的四个方向为不可用（door不能直接与player相邻）
        for (int i = 0; i < 4; i++)
        {
            int newRow = playerRow + dx[i];
            int newCol = playerCol + dy[i];
            if (newRow >= 0 && newRow < currentRow && newCol >= 0 && newCol < currentCol)
            {
                playerAdjacent.Add(new Vector2Int(newRow, newCol));
            }
        }
        
        // 找到所有可用的位置
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                Vector2Int pos = new Vector2Int(row, col);
                if (cardTypes[row, col] == CardType.Blank)
                {
                    availablePositions.Add(pos);
                }
            }
        }
        
        // 尝试找到T形布局，door与所有3个nun都相邻
        // T形布局：3个nun形成一个T形，door在T形的中心位置，与所有nun相邻
        // T形示例：
        //   N
        // N D N  (door在中心，与所有3个nun相邻)
        // 或者：
        // N D N
        //   N    (door在中心，与所有3个nun相邻)
        
        List<Vector2Int[]> tShapes = new List<Vector2Int[]>();
        
        // T形1：水平T，door在中心，3个nun在上下左右（door与所有nun相邻）
        // nun在(row-1, col), (row, col-1), (row, col+1)，door在(row, col)
        for (int row = 1; row < currentRow - 1; row++)
        {
            for (int col = 1; col < currentCol - 1; col++)
            {
                Vector2Int[] tShape = new Vector2Int[4];
                tShape[0] = new Vector2Int(row - 1, col); // nun1在上
                tShape[1] = new Vector2Int(row, col - 1); // nun2在左
                tShape[2] = new Vector2Int(row, col + 1); // nun3在右
                tShape[3] = new Vector2Int(row, col); // door在中心
                
                if (IsValidTShape(tShape, playerAdjacent, availablePositions))
                {
                    tShapes.Add(tShape);
                }
            }
        }
        
        // T形2：水平T，door在中心，3个nun在上下左右（旋转90度）
        // nun在(row, col-1), (row, col+1), (row+1, col)，door在(row, col)
        for (int row = 0; row < currentRow - 2; row++)
        {
            for (int col = 1; col < currentCol - 1; col++)
            {
                Vector2Int[] tShape = new Vector2Int[4];
                tShape[0] = new Vector2Int(row, col - 1); // nun1在左
                tShape[1] = new Vector2Int(row, col + 1); // nun2在右
                tShape[2] = new Vector2Int(row + 1, col); // nun3在下
                tShape[3] = new Vector2Int(row, col); // door在中心
                
                if (IsValidTShape(tShape, playerAdjacent, availablePositions))
                {
                    tShapes.Add(tShape);
                }
            }
        }
        
        // T形3：水平T，door在中心，3个nun在上下左右（旋转180度）
        // nun在(row+1, col), (row, col-1), (row, col+1)，door在(row, col)
        for (int row = 0; row < currentRow - 2; row++)
        {
            for (int col = 1; col < currentCol - 1; col++)
            {
                Vector2Int[] tShape = new Vector2Int[4];
                tShape[0] = new Vector2Int(row + 1, col); // nun1在下
                tShape[1] = new Vector2Int(row, col - 1); // nun2在左
                tShape[2] = new Vector2Int(row, col + 1); // nun3在右
                tShape[3] = new Vector2Int(row, col); // door在中心
                
                if (IsValidTShape(tShape, playerAdjacent, availablePositions))
                {
                    tShapes.Add(tShape);
                }
            }
        }
        
        // T形4：水平T，door在中心，3个nun在上下左右（旋转270度）
        // nun在(row-1, col), (row, col-1), (row+1, col)，door在(row, col)
        for (int row = 1; row < currentRow - 2; row++)
        {
            for (int col = 1; col < currentCol - 1; col++)
            {
                Vector2Int[] tShape = new Vector2Int[4];
                tShape[0] = new Vector2Int(row - 1, col); // nun1在上
                tShape[1] = new Vector2Int(row, col - 1); // nun2在左
                tShape[2] = new Vector2Int(row + 1, col); // nun3在下
                tShape[3] = new Vector2Int(row, col); // door在中心
                
                if (IsValidTShape(tShape, playerAdjacent, availablePositions))
                {
                    tShapes.Add(tShape);
                }
            }
        }
        
        // 随机选择一个T形布局
        if (tShapes.Count > 0)
        {
            int randomIndex = Random.Range(0, tShapes.Count);
            Vector2Int[] selectedTShape = tShapes[randomIndex];
            
            // 放置3个nun（前3个位置）
            for (int i = 0; i < 3; i++)
            {
                Vector2Int nunPos = selectedTShape[i];
                cardTypes[nunPos.x, nunPos.y] = CardType.Nun;
            }
            
            // 放置1个door（第4个位置）
            Vector2Int doorPos = selectedTShape[3];
            cardTypes[doorPos.x, doorPos.y] = CardType.Door;
        }
        else
        {
            Debug.LogError("Could not find valid T-shape for nun boss and door placement!");
        }
    }
    
    // 检查T形布局是否有效
    private bool IsValidTShape(Vector2Int[] tShape, HashSet<Vector2Int> playerAdjacent, List<Vector2Int> availablePositions)
    {
        // 检查所有位置是否在范围内且是空白的
        for (int i = 0; i < tShape.Length; i++)
        {
            Vector2Int pos = tShape[i];
            if (pos.x < 0 || pos.x >= currentRow || pos.y < 0 || pos.y >= currentCol)
            {
                return false;
            }
            if (cardTypes[pos.x, pos.y] != CardType.Blank)
            {
                return false;
            }
            if (!availablePositions.Contains(pos))
            {
                return false;
            }
        }
        
        // 检查door（最后一个位置）是否与player直接相邻
        Vector2Int doorPos = tShape[3];
        if (playerAdjacent.Contains(doorPos))
        {
            return false;
        }
        
        return true;
    }
    
    public void SpawnHorriblemanBoss()
    {
        // 在所有其他敌人被击败后，生成horribleman boss
        List<Vector2Int> availablePositions = new List<Vector2Int>();
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                // 不能是player位置，不能是已经放置boss的位置，必须是未翻开的位置
                if (cardTypes[row, col] != CardType.Player && 
                    cardTypes[row, col] != CardType.Horribleman &&
                    !isRevealed[row, col])
                {
                    availablePositions.Add(new Vector2Int(row, col));
                }
            }
        }
        
        // 随机选择一个位置放置horribleman boss
        if (availablePositions.Count > 0)
        {
            int randomIndex = Random.Range(0, availablePositions.Count);
            Vector2Int bossPos = availablePositions[randomIndex];
            cardTypes[bossPos.x, bossPos.y] = CardType.Horribleman;
            if (tiles[bossPos.x, bossPos.y] != null)
            {
                Sprite bossSprite = GetSpriteForCardType(CardType.Horribleman);
                if (bossSprite == null)
                {
                    bossSprite = CardInfoManager.Instance.GetCardSprite(CardType.Blank);
                }
                tiles[bossPos.x, bossPos.y].SetFrontSprite(bossSprite);
                tiles[bossPos.x, bossPos.y].Initialize(bossPos.x, bossPos.y, CardType.Horribleman, isRevealed[bossPos.x, bossPos.y]);
            }
        }
    }
    
    public bool AreAllRegularEnemiesDefeated()
    {
        // 检查是否所有普通敌人（isEnemy为true但不是boss）都被击败了
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                // 排除boss卡（nun, snowman, horribleman）
                CardType cardType = cardTypes[row, col];
                if (cardType != CardType.Nun && cardType != CardType.Snowman && cardType != CardType.Horribleman)
                {
                    if (IsEnemyCard(row, col) && !isRevealed[row, col])
                    {
                        return false; // 还有未翻开的普通敌人
                    }
                }
            }
        }
        return true; // 所有普通敌人都被击败了
    }
    
    public Vector2Int GetBossPosition(CardType bossType)
    {
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == bossType)
                {
                    return new Vector2Int(row, col);
                }
            }
        }
        return new Vector2Int(-1, -1);
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
        // 获取当前关卡信息
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        bool isBossLevel = !string.IsNullOrEmpty(levelInfo.boss);
        
        Vector2Int targetPos = new Vector2Int(-1, -1);
        
        if (isBossLevel)
        {
            string bossType = levelInfo.boss.ToLower();
            
            if (bossType == "nun")
            {
                // nun关卡：sign指向door（找到第一个door）
                targetPos = GetDoorPosition();
            }
            else if (bossType == "snowman")
            {
                // snowman关卡：sign指向snowman boss
                targetPos = GetBossPosition(CardType.Snowman);
            }
            else if (bossType == "horribleman")
            {
                // horribleman关卡：sign指向horribleman boss
                targetPos = GetBossPosition(CardType.Horribleman);
            }
        }
        else
        {
            // 非boss关卡：sign指向bell
            targetPos = GetBellPosition();
        }
        
        if (targetPos.x < 0) return; // 没有找到目标，不需要更新箭头
        
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.Sign && tiles[row, col] != null)
                {
                    tiles[row, col].UpdateSignArrow(targetPos.x, targetPos.y, row, col);
                }
            }
        }
    }
    
    public Vector2Int GetDoorPosition()
    {
        // 找到第一个door的位置
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.Door)
                {
                    return new Vector2Int(row, col);
                }
            }
        }
        return new Vector2Int(-1, -1); // 未找到door
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
        bool isBossLevel = !string.IsNullOrEmpty(levelInfo.boss);
        bool isSnowmanBossLevel = isBossLevel && levelInfo.boss.ToLower() == "snowman";
        
        // 从CardInfo获取起始数量，包括购买的卡牌
        List<CardInfo> allCards = CardInfoManager.Instance.GetAllCards();
        
        foreach (CardInfo cardInfo in allCards)
        {
            CardType cardType = CardInfoManager.Instance.GetCardType(cardInfo.identifier);
            if (cardType == CardType.Blank) continue; // 空白卡不在这里添加
            
            // boss关卡中移除铃铛卡牌
            if (isBossLevel && cardType == CardType.Bell)
            {
                continue;
            }
            
            int count = cardInfo.start;
            bool isNunBossLevel = isBossLevel && levelInfo.boss.ToLower() == "nun";
            
            // 如果是敌人（grinch），使用关卡配置的数量
            if (cardType == CardType.Enemy)
            {
                // nun关卡：移除所有敌人，不添加Enemy卡
                if (isNunBossLevel)
                {
                    continue; // 跳过Enemy卡，不添加到卡组
                }
                
                // snowman boss会在周围生成4个enemy，这些enemy不算在targetEnemyCount中
                // 所以需要从targetEnemyCount中减去4
                // if (isSnowmanBossLevel)
                // {
                //     count = Mathf.Max(0, targetEnemyCount - 4);
                // }
                // else
                {
                    count = targetEnemyCount;
                }
            }
            // nun关卡：添加3个nun（替换敌人）
            else if (isNunBossLevel && cardType == CardType.Nun)
            {
                count = 3; // nun关卡中，添加3个nun（替换敌人）
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
        
        // // 计算需要的空白卡数量
        // int totalTiles = currentRow * currentCol;
        // int totalUsed = 1; // player固定在中间，占1个位置
        // totalUsed += cardDeck.Count;
        // int blankCount = totalTiles - totalUsed;
        //
        // if (blankCount > 0)
        // {
        //     for (int i = 0; i < blankCount; i++)
        //     {
        //         cardDeck.Add(CardType.Blank);
        //     }
        // }
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
    
    public void RevealTile(int row, int col,bool isFirst = true)
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
        
        GameManager.Instance.OnTileRevealed(row, col, cardTypes[row, col], isLastTile, isLastSafeTile,isFirst);
    }
    
    private bool IsLastSafeTile()
    {
        // 检查是否还有未reveal的safe tile（除了isEnemy的卡牌之外的tile）
        foreach (Vector2Int pos in unrevealedTiles)
        {
            if (!IsEnemyCard(pos.x, pos.y))
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
        
        // Hint所在行有几个敌人（基于isEnemy）
        int rowEnemies = 0;
        for (int c = 0; c < currentCol; c++)
        {
            if (IsEnemyCard(row, c))
                rowEnemies++;
        }
        hints.Add($"This row has {rowEnemies} enem{(rowEnemies != 1 ? "ies" : "y")}");
        
        // Hint所在列有几个敌人（基于isEnemy）
        int colEnemies = 0;
        for (int r = 0; r < currentRow; r++)
        {
            if (IsEnemyCard(r, col))
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
        
        
        
        // 有几个敌人在四个角落（基于isEnemy）
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
                IsEnemyCard(corner.x, corner.y))
                cornerEnemies++;
        }
        hints.Add($"There {(cornerEnemies == 1 ? "is" : "are")} {cornerEnemies} enem{(cornerEnemies != 1 ? "ies" : "y")} in the four corners");
        
        // 保留原有的提示类型
        // Nearby 3x3 area enemy count（基于isEnemy）
        int nearbyEnemies = 0;
        for (int r = row - 1; r <= row + 1; r++)
        {
            for (int c = col - 1; c <= col + 1; c++)
            {
                if (r >= 0 && r < currentRow && c >= 0 && c < currentCol && IsEnemyCard(r, c))
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
                
                    // 检查四个方向的邻居（基于isEnemy）
                    for (int i = 0; i < 4; i++)
                    {
                        int newRow = current.x + dx[i];
                        int newCol = current.y + dy[i];
                        Vector2Int neighbor = new Vector2Int(newRow, newCol);
                    
                        if (newRow >= 0 && newRow < currentRow && newCol >= 0 && newCol < currentCol &&
                            IsEnemyCard(newRow, newCol) && !visited.Contains(neighbor))
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
    
    // 检查指定位置的卡牌是否是敌人（基于isEnemy字段）
    public bool IsEnemyCard(int row, int col)
    {
        if (row < 0 || row >= currentRow || col < 0 || col >= currentCol)
            return false;
        
        CardType cardType = cardTypes[row, col];
        if (CardInfoManager.Instance != null)
        {
            return CardInfoManager.Instance.IsEnemyCard(cardType);
        }
        return false;
    }
    
    public List<Vector2Int> GetAllEnemyPositions()
    {
        List<Vector2Int> enemies = new List<Vector2Int>();
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (IsEnemyCard(row, col))
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
                if (IsEnemyCard(row, col))
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
                if (IsEnemyCard(row, col) && isRevealed[row, col])
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
    
    // 获取指定位置的Tile对象
    public Tile GetTile(int row, int col)
    {
        if (row < 0 || row >= currentRow || col < 0 || col >= currentCol)
            return null;
        return tiles[row, col];
    }
    
    // 获取player的位置
    public Vector2Int GetPlayerPosition()
    {
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.Player)
                {
                    return new Vector2Int(row, col);
                }
            }
        }
        return new Vector2Int(-1, -1); // 未找到player
    }
}
