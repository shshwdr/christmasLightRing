using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using System.Collections;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

public class BoardManager : MonoBehaviour
{
    public GameObject tilePrefab;
    public Transform boardParent;
    
    [Header("Tile Reveal Animation")]
    [Tooltip("每个tile出现动画之间的间隔时间（秒）")]
    public float tileRevealInterval = 0.1f;
    [Tooltip("每个tile出现动画的持续时间（秒）")]
    public float tileRevealDuration = 0.3f;
    
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
        
        // 检查是否是第一关或第二关，且tutorialForceBoard开启
        int currentLevel = GameManager.Instance != null ? GameManager.Instance.mainGameData.currentLevel : 1;
        bool tutorialForceBoard = TutorialManager.Instance != null && TutorialManager.Instance.tutorialForceBoard;
        bool isLevel1 = currentLevel == 1 && tutorialForceBoard;
        bool isLevel2 = currentLevel == 2 && tutorialForceBoard;
        
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
        
        // 第一关和第二关的特殊放置逻辑
        if (isLevel1)
        {
            PlaceLevel1SpecialCards(centerRow, centerCol, remainingDeck);
        }
        else if (isLevel2)
        {
            PlaceLevel2SpecialCards(centerRow, centerCol, remainingDeck);
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
            for (int i = 0;i<100&& availablePositions.Count>0 && otherCardIndex < otherCards.Count; i++)
            {
                Vector2Int pos = availablePositions.RandomItem();
                CardType cardType = otherCards[otherCardIndex++];
                cardTypes[pos.x, pos.y] = cardType;
                availablePositions.Remove(pos);
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
        
        allTiles = new List<Tile>();
        
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                GameObject tileObj = Instantiate(tilePrefab, boardParent);
                tileObj.name = $"Tile_{row}_{col}";
                
                RectTransform rect = tileObj.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(col * tileSize - offsetX, (currentRow - 1 - row) * tileSize - offsetY);
                rect.sizeDelta = new Vector2(tileSize, tileSize);
                
                // 先把scale.x设为0
                Vector3 currentScale = rect.localScale;
                rect.localScale = new Vector3(0, currentScale.y, currentScale.z);
                
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
                allTiles.Add(tile);
            }
        }

        // 随机排序所有tile
        for (int i = allTiles.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Tile temp = allTiles[i];
            allTiles[i] = allTiles[j];
            allTiles[j] = temp;
        }
        RestartAnimateBoard();
        
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

    private List<Tile> allTiles;
    public void RestartAnimateBoard()
    {
        
        
        // 逐个播放动画
        StartCoroutine(AnimateTilesReveal(allTiles));
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
    
    // 第一关特殊放置逻辑
    private void PlaceLevel1SpecialCards(int playerRow, int playerCol, List<CardType> remainingDeck)
    {
        // 第一个hint一定在玩家上面一个格子，且一定关于周围3x3的hint
        int hintRow = playerRow - 1;
        int hintCol = playerCol;
        if (hintRow >= 0 && hintRow < currentRow && hintCol >= 0 && hintCol < currentCol)
        {
            // 从卡组中找到第一个hint
            int hintIndex = remainingDeck.FindIndex(card => card == CardType.Hint);
            if (hintIndex >= 0)
            {
                cardTypes[hintRow, hintCol] = CardType.Hint;
                remainingDeck.RemoveAt(hintIndex);
                unrevealedTiles.Add(new Vector2Int(hintRow, hintCol));
            }
        }


        // 收集所有coin、gift和bell，从remainingDeck中移除
        List<CardType> coins = remainingDeck.FindAll(card => card == CardType.Coin);
        List<CardType> gifts = remainingDeck.FindAll(card => card == CardType.Gift);
        List<CardType> bells = remainingDeck.FindAll(card => card == CardType.Bell);
        remainingDeck.RemoveAll(card => card == CardType.Coin || card == CardType.Gift || card == CardType.Bell);
        
        // 金币、礼物和铃铛生成在玩家的左右、左上和右上（四个格子）
        List<Vector2Int> coinGiftBellPositions = new List<Vector2Int>();
        // 左
        if (playerCol - 1 >= 0)
            coinGiftBellPositions.Add(new Vector2Int(playerRow, playerCol - 1));
        // 右
        if (playerCol + 1 < currentCol)
            coinGiftBellPositions.Add(new Vector2Int(playerRow, playerCol + 1));
        // 左上
        if (playerRow - 1 >= 0 && playerCol - 1 >= 0)
            coinGiftBellPositions.Add(new Vector2Int(playerRow - 1, playerCol - 1));
        // 右上
        if (playerRow - 1 >= 0 && playerCol + 1 < currentCol)
            coinGiftBellPositions.Add(new Vector2Int(playerRow - 1, playerCol + 1));
        
        // 先放置所有coin
        int posIndex = 0;
        foreach (CardType coin in coins)
        {
            if (posIndex < coinGiftBellPositions.Count)
            {
                Vector2Int pos = coinGiftBellPositions[posIndex++];
                cardTypes[pos.x, pos.y] = CardType.Coin;
                unrevealedTiles.Add(pos);
            }
        }
        
        // 再放置所有gift
        foreach (CardType gift in gifts)
        {
            if (posIndex < coinGiftBellPositions.Count)
            {
                Vector2Int pos = coinGiftBellPositions[posIndex++];
                cardTypes[pos.x, pos.y] = CardType.Gift;
                unrevealedTiles.Add(pos);
            }
        }
        
        // 最后放置bell（只放置第一个bell，如果有多个bell，剩余的会在后续随机生成）
        if (bells.Count > 0 && posIndex < coinGiftBellPositions.Count)
        {
            Vector2Int pos = coinGiftBellPositions[posIndex++];
            cardTypes[pos.x, pos.y] = CardType.Bell;
            bells.RemoveAt(0); // 移除已放置的bell
            unrevealedTiles.Add(pos);
        }
        
        // 将剩余的bells放回remainingDeck（如果有的话）
        remainingDeck.AddRange(bells);
        
        // 注意：hint和enemy会在后续的随机生成中放置，不在这里放置
    }
    
    // 第二关特殊放置逻辑
    private void PlaceLevel2SpecialCards(int playerRow, int playerCol, List<CardType> remainingDeck)
    {
        // 第一个hint一定在玩家下面一个格子，一定关于这一列有几个敌人
        int hintRow = playerRow + 1;
        int hintCol = playerCol;
        if (hintRow >= 0 && hintRow < currentRow && hintCol >= 0 && hintCol < currentCol)
        {
            // 从卡组中找到第一个hint
            int hintIndex = remainingDeck.FindIndex(card => card == CardType.Hint);
            if (hintIndex >= 0)
            {
                cardTypes[hintRow, hintCol] = CardType.Hint;
                remainingDeck.RemoveAt(hintIndex);
                unrevealedTiles.Add(new Vector2Int(hintRow, hintCol));
            }
        }
        
        // 收集所有enemy，从remainingDeck中移除
        List<CardType> enemies = remainingDeck.FindAll(card => card == CardType.Enemy);
        remainingDeck.RemoveAll(card => card == CardType.Enemy);
        
        // 第一个enemy生成在玩家上方
        int enemyRow = playerRow - 1;
        int enemyCol = playerCol;
        if (enemies.Count > 0 && enemyRow >= 0 && enemyRow < currentRow && enemyCol >= 0 && enemyCol < currentCol)
        {
            cardTypes[enemyRow, enemyCol] = CardType.Enemy;
            enemies.RemoveAt(0); // 移除已放置的enemy
            unrevealedTiles.Add(new Vector2Int(enemyRow, enemyCol));
        }
        
        // 收集所有bell，从remainingDeck中移除
        List<CardType> bells = remainingDeck.FindAll(card => card == CardType.Bell);
        remainingDeck.RemoveAll(card => card == CardType.Bell);
        
        // 铃铛生成在敌人上方
        int bellRow = enemyRow - 1;
        int bellCol = enemyCol;
        if (bells.Count > 0 && bellRow >= 0 && bellRow < currentRow && bellCol >= 0 && bellCol < currentCol)
        {
            cardTypes[bellRow, bellCol] = CardType.Bell;
            bells.RemoveAt(0); // 移除已放置的bell
            unrevealedTiles.Add(new Vector2Int(bellRow, bellCol));
        }
        
        // 将剩余的enemies和bells放回remainingDeck（如果有的话），它们会在后续随机生成
        remainingDeck.AddRange(enemies);
        remainingDeck.AddRange(bells);
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
        
        // T形1：上T（0度），door在中心，3个nun在上、左、右
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
        
        // T形2：右T（旋转90度），door在中心，3个nun在右、上、下
        // nun在(row-1, col), (row, col+1), (row+1, col)，door在(row, col)
        for (int row = 1; row < currentRow - 2; row++)
        {
            for (int col = 0; col < currentCol - 1; col++)
            {
                Vector2Int[] tShape = new Vector2Int[4];
                tShape[0] = new Vector2Int(row - 1, col); // nun1在上
                tShape[1] = new Vector2Int(row, col + 1); // nun2在右
                tShape[2] = new Vector2Int(row + 1, col); // nun3在下
                tShape[3] = new Vector2Int(row, col); // door在中心
                
                if (IsValidTShape(tShape, playerAdjacent, availablePositions))
                {
                    tShapes.Add(tShape);
                }
            }
        }
        
        // T形3：下T（旋转180度），door在中心，3个nun在下、左、右
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
        
        // T形4：左T（旋转270度/-90度），door在中心，3个nun在左、上、下
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
                if (GameManager.Instance != null && GameManager.Instance.mainGameData.purchasedCards.Contains(cardType))
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
        
        // 检查是否是第一关或第二关，且tutorialForceBoard开启
        int currentLevel = GameManager.Instance != null ? GameManager.Instance.mainGameData.currentLevel : 1;
        bool tutorialForceBoard = TutorialManager.Instance != null && TutorialManager.Instance.tutorialForceBoard;
        bool isLevel1 = currentLevel == 1 && tutorialForceBoard;
        bool isLevel2 = currentLevel == 2 && tutorialForceBoard;
        
        Vector2Int playerPos = GetPlayerPosition();
        
        // 第一关：检查是否试图点击下方和斜下方的格子
        if (isLevel1)
        {
            // 下方：(row > playerRow)
            // 斜下方：(row > playerRow && col != playerCol)
            if (row > playerPos.x)
            {
                // 检查是否已经点开铃铛
                bool bellRevealed = false;
                for (int r = 0; r < currentRow; r++)
                {
                    for (int c = 0; c < currentCol; c++)
                    {
                        if (cardTypes[r, c] == CardType.Bell && isRevealed[r, c])
                        {
                            bellRevealed = true;
                            break;
                        }
                    }
                    if (bellRevealed) break;
                }
                
                string tutorialId = bellRevealed ? "hint1_2" : "hint1_1";
                if (TutorialManager.Instance != null)
                {
                    TutorialManager.Instance.ShowTutorial(tutorialId,true);
                }
                return; // 不执行翻开
            }
        }
        
        // 第二关：检查是否试图点击上方或再上方的格子
        if (isLevel2)
        {
            // 上方或再上方：(row < playerRow)
            if (row < playerPos.x && col == playerPos.y)
            {
                // 检查是否已经拿到灯笼（flashlights > 0）
                bool hasFlashlight = GameManager.Instance != null && GameManager.Instance.mainGameData.flashlights > 0;
                bool usingFlashlight = GameManager.Instance.IsUsingFlashlight();
                if (!hasFlashlight && !usingFlashlight)
                {
                    // 还没有拿到灯笼也没用灯笼，显示hint1_3
                    if (TutorialManager.Instance != null)
                    {
                        TutorialManager.Instance.ShowTutorial("hint1_3",true);
                    }
                    return; // 不执行翻开
                }
            }
        }
        
        // 处理horribleman boss战的特殊逻辑
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        bool isHorriblemanBossLevel = !string.IsNullOrEmpty(levelInfo.boss) && levelInfo.boss.ToLower() == "horribleman";
        
        if (isHorriblemanBossLevel && cardTypes[row, col] == CardType.Horribleman)
        {
            // 查找其他还未reveal的enemy（排除horribleman本身）
            List<Vector2Int> unrevealedEnemies = new List<Vector2Int>();
            for (int r = 0; r < currentRow; r++)
            {
                for (int c = 0; c < currentCol; c++)
                {
                    if (r == row && c == col) continue; // 跳过horribleman本身
                    if (IsEnemyCard(r, c) && !isRevealed[r, c])
                    {
                        // 排除boss卡（nun, snowman, horribleman）
                        CardType cardType = cardTypes[r, c];
                        if (cardType != CardType.Nun && cardType != CardType.Snowman && cardType != CardType.Horribleman)
                        {
                            unrevealedEnemies.Add(new Vector2Int(r, c));
                        }
                    }
                }
            }
            
            // 如果存在其他还未reveal的enemy，交换位置并翻开enemy
            if (unrevealedEnemies.Count > 0)
            {
                // 随机选择一个enemy
                int randomIndex = Random.Range(0, unrevealedEnemies.Count);
                Vector2Int enemyPos = unrevealedEnemies[randomIndex];
                
                // 交换两张卡的位置
                CardType tempCardType = cardTypes[row, col];
                cardTypes[row, col] = cardTypes[enemyPos.x, enemyPos.y];
                cardTypes[enemyPos.x, enemyPos.y] = tempCardType;
                
                // 交换tile的sprite和cardType
                if (tiles[row, col] != null && tiles[enemyPos.x, enemyPos.y] != null)
                {
                    // 获取sprite
                    Sprite horriblemanSprite = GetSpriteForCardType(CardType.Horribleman);
                    if (horriblemanSprite == null)
                    {
                        horriblemanSprite = CardInfoManager.Instance.GetCardSprite(CardType.Blank);
                    }
                    Sprite enemySprite = GetSpriteForCardType(cardTypes[row, col]);
                    if (enemySprite == null)
                    {
                        enemySprite = CardInfoManager.Instance.GetCardSprite(CardType.Blank);
                    }
                    
                    // 更新tile的sprite和cardType
                    tiles[row, col].SetFrontSprite(enemySprite);
                    tiles[row, col].Initialize(row, col, cardTypes[row, col], isRevealed[row, col]);
                    
                    tiles[enemyPos.x, enemyPos.y].SetFrontSprite(horriblemanSprite);
                    tiles[enemyPos.x, enemyPos.y].Initialize(enemyPos.x, enemyPos.y, CardType.Horribleman, isRevealed[enemyPos.x, enemyPos.y]);
                }
                
                // 更新revealableTiles：确保(row, col)位置在revealableTiles中，这样才能被翻开
                // 注意：交换位置后，两个位置都还在unrevealedTiles中，不需要更新
                if (!revealableTiles.Contains(pos))
                {
                    revealableTiles.Add(pos);
                }
                
                // 翻开enemy（它现在在玩家点的卡的位置了）
                RevealTile(row, col, isFirst);
                return; // 不继续执行后面的逻辑，因为已经递归调用了RevealTile
            }
            // 如果不存在其他还未reveal的enemy，正常翻开horribleman（继续执行下面的逻辑）
        }
        
        // 第一关：如果点到了铃铛，且coin和gift还有没点完的，交换铃铛和点击的格子的位置
        if (isLevel1 && cardTypes[row, col] == CardType.Bell)
        {
            int unrevealedCoinCount = GetUnrevealedCoinCount();
            int unrevealedGiftCount = GetUnrevealedGiftCount();
            
            // 如果还有未点开的coin或gift，交换位置
            if (unrevealedCoinCount > 0 || unrevealedGiftCount > 0)
            {
                // 查找其他未翻开的非铃铛位置（用于交换）
                List<Vector2Int> availableSwapPositions = new List<Vector2Int>();
                for (int r = 0; r < currentRow; r++)
                {
                    for (int c = 0; c < currentCol; c++)
                    {
                        if (r == row && c == col) continue; // 跳过当前点击的位置
                        if (!isRevealed[r, c] && (cardTypes[r, c] == CardType.Coin || cardTypes[r, c] == CardType.Gift ) )
                        {
                            availableSwapPositions.Add(new Vector2Int(r, c));
                        }
                    }
                }
                
                if (availableSwapPositions.Count > 0)
                {
                    // 随机选择一个位置进行交换
                    int randomIndex = Random.Range(0, availableSwapPositions.Count);
                    Vector2Int swapPos = availableSwapPositions[randomIndex];
                    
                    // 交换两张卡的位置
                    CardType tempCardType = cardTypes[row, col];
                    cardTypes[row, col] = cardTypes[swapPos.x, swapPos.y];
                    cardTypes[swapPos.x, swapPos.y] = tempCardType;
                    
                    // 交换tile的sprite和cardType
                    if (tiles[row, col] != null && tiles[swapPos.x, swapPos.y] != null)
                    {
                        // 获取sprite
                        Sprite bellSprite = GetSpriteForCardType(CardType.Bell);
                        if (bellSprite == null)
                        {
                            bellSprite = CardInfoManager.Instance.GetCardSprite(CardType.Blank);
                        }
                        Sprite swapSprite = GetSpriteForCardType(cardTypes[row, col]);
                        if (swapSprite == null)
                        {
                            swapSprite = CardInfoManager.Instance.GetCardSprite(CardType.Blank);
                        }
                        
                        // 更新tile的sprite和cardType
                        tiles[row, col].SetFrontSprite(swapSprite);
                        tiles[row, col].Initialize(row, col, cardTypes[row, col], isRevealed[row, col]);
                        
                        tiles[swapPos.x, swapPos.y].SetFrontSprite(bellSprite);
                        tiles[swapPos.x, swapPos.y].Initialize(swapPos.x, swapPos.y, CardType.Bell, isRevealed[swapPos.x, swapPos.y]);
                    }
                    
                    // 更新revealableTiles：确保(row, col)位置在revealableTiles中
                    if (!revealableTiles.Contains(pos))
                    {
                        revealableTiles.Add(pos);
                    }
                    
                    // 翻开交换后的卡（它现在在玩家点的位置了）
                    RevealTile(row, col, isFirst);
                    return; // 不继续执行后面的逻辑，因为已经递归调用了RevealTile
                }
            }
        }
        
        // 如果是hint卡，在翻开时计算并保存提示内容
        if (cardTypes[row, col] == CardType.Hint && !hintContents.ContainsKey(pos))
        {
            // 第一关：第一个hint（在玩家上方）一定关于周围3x3的hint
            bool force3x3Hint = false;
            if (isLevel1)
            {
                Vector2Int hintPlayerPos = GetPlayerPosition();
                if (row == hintPlayerPos.x - 1 && col == hintPlayerPos.y)
                {
                    force3x3Hint = true;
                }
            }

            // 第二关：第一个hint（在玩家下方）一定关于这一列有几个敌人
            bool forceColHint = false;
            if (isLevel2)
            {
                Vector2Int hintPlayerPos = GetPlayerPosition();
                if (row == hintPlayerPos.x + 1 && col == hintPlayerPos.y)
                {
                    forceColHint = true;
                }
            }
            
            string hint = CalculateHint(row, col, force3x3Hint, forceColHint);
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

    private string CalculateHint(int row, int col, bool force3x3Hint = false, bool forceColHint = false)
    {
        List<Vector2Int> enemies = GetAllEnemyPositions();
        List<string> hints = new List<string>();
        List<string> usefulHints = new List<string>();

        // 如果强制使用3x3 hint，直接返回
        if (force3x3Hint)
        {
            int forcedNearbyEnemies = 0;
            for (int r = row - 1; r <= row + 1; r++)
            {
                for (int c = col - 1; c <= col + 1; c++)
                {
                    if (r >= 0 && r < currentRow && c >= 0 && c < currentCol)
                    {
                        if (IsEnemyCard(r, c))
                            forcedNearbyEnemies++;
                    }
                }
            }

            var localizedString = new LocalizedString("GameText", "3x3 area around has {nearbyEnemies:plural:{} enemy|{} enemies}");
            localizedString.Arguments = new object[] { forcedNearbyEnemies };
            var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(localizedString.TableReference, localizedString.TableEntryReference, localizedString.Arguments);
            return handle.WaitForCompletion();
        }

        // 如果强制使用列hint，直接返回
        if (forceColHint)
        {
            int forcedColEnemies = 0;
            for (int r = 0; r < currentRow; r++)
            {
                if (IsEnemyCard(r, col))
                    forcedColEnemies++;
            }

            var localizedString = new LocalizedString("GameText", "This column has {colEnemies:plural:{} enemy|{} enemies}");
            localizedString.Arguments = new object[] { forcedColEnemies };
            var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(localizedString.TableReference, localizedString.TableEntryReference, localizedString.Arguments);
            return handle.WaitForCompletion();
        }
        
        // Hint所在行有几个敌人（基于isEnemy）
        int rowEnemies = 0;
        bool rowHasUnrevealed = false;
        for (int c = 0; c < currentCol; c++)
        {
            if (IsEnemyCard(row, c))
                rowEnemies++;
            if (!isRevealed[row, c])
                rowHasUnrevealed = true;
        }
        
        var rowLocalizedString = new LocalizedString("GameText", "This row has {rowEnemies:plural:{} enemy|{} enemies}");
        rowLocalizedString.Arguments = new object[] { rowEnemies };
        string rowHint = rowLocalizedString.GetLocalizedString();
        hints.Add(rowHint);
        if (rowHasUnrevealed)
        {
            usefulHints.Add(rowHint);
        }
        
        // Hint所在列有几个敌人（基于isEnemy）
        int colEnemies = 0;
        bool colHasUnrevealed = false;
        for (int r = 0; r < currentRow; r++)
        {
            if (IsEnemyCard(r, col))
                colEnemies++;
            if (!isRevealed[r, col])
                colHasUnrevealed = true;
        }
        
        var colLocalizedString = new LocalizedString("GameText", "This column has {colEnemies:plural:{} enemy|{} enemies}");
        colLocalizedString.Arguments = new object[] { colEnemies };
        var colHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(colLocalizedString.TableReference, colLocalizedString.TableEntryReference, colLocalizedString.Arguments);
        string colHint = colHandle.WaitForCompletion();
        hints.Add(colHint);
        if (colHasUnrevealed)
        {
            usefulHints.Add(colHint);
        }

        // 左右敌人数量比较（只在不是最左或最右的位置生成）
        if (col > 0 && col < currentCol - 1)
        {
            int leftEnemies = 0;
            int rightEnemies = 0;
            bool leftRightHasUnrevealed = false;
            
            // 计算整个board上在这个格子左边的所有敌人（包括不同行）
            for (int r = 0; r < currentRow; r++)
            {
                for (int c = 0; c < col; c++)
                {
                    if (IsEnemyCard(r, c))
                        leftEnemies++;
                    if (!isRevealed[r, c])
                        leftRightHasUnrevealed = true;
                }
            }
            
            // 计算整个board上在这个格子右边的所有敌人（包括不同行）
            for (int r = 0; r < currentRow; r++)
            {
                for (int c = col + 1; c < currentCol; c++)
                {
                    if (IsEnemyCard(r, c))
                        rightEnemies++;
                    if (!isRevealed[r, c])
                        leftRightHasUnrevealed = true;
                }
            }
            
            string leftRightHint;
            if (leftEnemies > rightEnemies)
            {
                int diff = leftEnemies - rightEnemies;
                var localizedString = new LocalizedString("GameText", "{diff:plural:{} more enemy|{} more enemies} on left than on right");
                localizedString.Arguments = new object[] { diff };
                var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(localizedString.TableReference, localizedString.TableEntryReference, localizedString.Arguments);
                leftRightHint = handle.WaitForCompletion();
            }
            else if (rightEnemies > leftEnemies)
            {
                int diff = rightEnemies - leftEnemies;
                var localizedString = new LocalizedString("GameText", "{diff:plural:{} more enemy|{} more enemies} on right than on left");
                localizedString.Arguments = new object[] { diff };
                var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(localizedString.TableReference, localizedString.TableEntryReference, localizedString.Arguments);
                leftRightHint = handle.WaitForCompletion();
            }
            else
            {
                var localizedString = new LocalizedString("GameText", "Same number of enemies on the left and right sides");
                var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(localizedString.TableReference, localizedString.TableEntryReference);
                leftRightHint = handle.WaitForCompletion();
            }
            
            hints.Add(leftRightHint);
            if (leftRightHasUnrevealed)
            {
                usefulHints.Add(leftRightHint);
            }
        }

        // 上下敌人数量比较（只在不是最上或最下的位置生成）
        if (row > 0 && row < currentRow - 1)
        {
            int topEnemies = 0;
            int bottomEnemies = 0;
            bool topBottomHasUnrevealed = false;
            
            // 计算整个board上在这个格子上边的所有敌人（包括不同列）
            for (int r = 0; r < row; r++)
            {
                for (int c = 0; c < currentCol; c++)
                {
                    if (IsEnemyCard(r, c))
                        topEnemies++;
                    if (!isRevealed[r, c])
                        topBottomHasUnrevealed = true;
                }
            }
            
            // 计算整个board上在这个格子下边的所有敌人（包括不同列）
            for (int r = row + 1; r < currentRow; r++)
            {
                for (int c = 0; c < currentCol; c++)
                {
                    if (IsEnemyCard(r, c))
                        bottomEnemies++;
                    if (!isRevealed[r, c])
                        topBottomHasUnrevealed = true;
                }
            }
            
            string topBottomHint;
            if (topEnemies > bottomEnemies)
            {
                int diff = topEnemies - bottomEnemies;
                var localizedString = new LocalizedString("GameText", "{diff:plural:{} more enemy|{} more enemies} on top than on bottom");
                localizedString.Arguments = new object[] { diff };
                var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(localizedString.TableReference, localizedString.TableEntryReference, localizedString.Arguments);
                topBottomHint = handle.WaitForCompletion();
            }
            else if (bottomEnemies > topEnemies)
            {
                int diff = bottomEnemies - topEnemies;
                var localizedString = new LocalizedString("GameText", "{diff:plural:{} more enemy|{} more enemies} on bottom than on top");
                localizedString.Arguments = new object[] { diff };
                var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(localizedString.TableReference, localizedString.TableEntryReference, localizedString.Arguments);
                topBottomHint = handle.WaitForCompletion();
            }
            else
            {
                var localizedString = new LocalizedString("GameText", "Same number of enemies on top and bottom");
                var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(localizedString.TableReference, localizedString.TableEntryReference);
                topBottomHint = handle.WaitForCompletion();
            }
            
            hints.Add(topBottomHint);
            if (topBottomHasUnrevealed)
            {
                usefulHints.Add(topBottomHint);
            }
        }

        // 有几个敌人在四个角落（基于isEnemy）
        int cornerEnemies = 0;
        bool cornersHaveUnrevealed = false;
        Vector2Int[] corners =
        {
            new Vector2Int(0, 0),
            new Vector2Int(0, currentCol - 1),
            new Vector2Int(currentRow - 1, 0),
            new Vector2Int(currentRow - 1, currentCol - 1)
        };
        foreach (Vector2Int corner in corners)
        {
            if (corner.x >= 0 && corner.x < currentRow && corner.y >= 0 && corner.y < currentCol)
            {
                if (IsEnemyCard(corner.x, corner.y))
                    cornerEnemies++;
                if (!isRevealed[corner.x, corner.y] && (corner.y != col || corner.x != row))
                    cornersHaveUnrevealed = true;
            }
        }
        
        var cornerLocalizedString = new LocalizedString("GameText", "There {cornerEnemies:plural:is {} enemy|are {} enemies} in the four corners");
        cornerLocalizedString.Arguments = new object[] { cornerEnemies };
        string cornerHint = cornerLocalizedString.GetLocalizedString();
        hints.Add(cornerHint);
        if (cornersHaveUnrevealed)
        {
            usefulHints.Add(cornerHint);
        }
        
        // 保留原有的提示类型
        // Nearby 3x3 area enemy count（基于isEnemy）
        int nearbyEnemies = 0;
        bool nearbyHasUnrevealed = false;
        for (int r = row - 1; r <= row + 1; r++)
        {
            for (int c = col - 1; c <= col + 1; c++)
            {
                if (r >= 0 && r < currentRow && c >= 0 && c < currentCol)
                {
                    if (IsEnemyCard(r, c))
                        nearbyEnemies++;
                    if (!isRevealed[r, c] && (c != col || r != row))
                        nearbyHasUnrevealed = true;
                }
            }
        }
        
        var nearbyLocalizedString = new LocalizedString("GameText", "3x3 area around has {nearbyEnemies:plural:{} enemy|{} enemies}");
        nearbyLocalizedString.Arguments = new object[] { nearbyEnemies };
        string nearbyHint = nearbyLocalizedString.GetLocalizedString();
        hints.Add(nearbyHint);
        if (nearbyHasUnrevealed)
        {
            usefulHints.Add(nearbyHint);
        }

        // Enemies adjacent to church (基于isEnemy)
        HashSet<Vector2Int> enemiesAdjacentToChurch = new HashSet<Vector2Int>();
        List<Vector2Int> churches = new List<Vector2Int>();
        // 找到所有与church相邻的敌人
        bool churchAdjacentHasUnrevealed = false;
        {
        int[] dx = { 0, 0, 1, -1 }; // 上下左右
        int[] dy = { 1, -1, 0, 0 };

        // 找到所有church位置
        for (int r = 0; r < currentRow; r++)
        {
            for (int c = 0; c < currentCol; c++)
            {
                if (cardTypes[r, c] == CardType.PoliceStation)
                {
                    churches.Add(new Vector2Int(r, c));
                }
            }
        }

        foreach (Vector2Int church in churches)
        {
            for (int i = 0; i < 4; i++)
            {
                int newRow = church.x + dx[i];
                int newCol = church.y + dy[i];

                if (newRow >= 0 && newRow < currentRow && newCol >= 0 && newCol < currentCol)
                {
                    if (IsEnemyCard(newRow, newCol))
                    {
                        enemiesAdjacentToChurch.Add(new Vector2Int(newRow, newCol));
                    }

                    if (!isRevealed[newRow, newCol] &&( newRow!=row || newCol!=col))
                    {
                        churchAdjacentHasUnrevealed = true;
                    }
                }
            }
        }

        }
    string churchHint;
        var churchLocalizedString = new LocalizedString("GameText", "There {enemiesAdjacentToChurch:plural:is no enemy|is 1 enemy|are {} enemies} adjacent to church");
        churchLocalizedString.Arguments = new object[] { enemiesAdjacentToChurch.Count };
        churchHint = churchLocalizedString.GetLocalizedString();
        hints.Add(churchHint);
        if (churchAdjacentHasUnrevealed)
        {
            usefulHints.Add(churchHint);
        }

        if (enemies.Count > 1)
        {
            
            // 找到最大的敌人group（四向邻接）
            int maxGroupSize = 0;
            HashSet<Vector2Int> maxGroup = new HashSet<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            int[] dx = { 0, 0, 1, -1 }; // 上下左右
            int[] dy = { 1, -1, 0, 0 };
        
            foreach (Vector2Int enemy in enemies)
            {
                if (visited.Contains(enemy))
                    continue;
            
                // BFS找到当前敌人所在的group
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                HashSet<Vector2Int> currentGroup = new HashSet<Vector2Int>();
                queue.Enqueue(enemy);
                visited.Add(enemy);
                currentGroup.Add(enemy);
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
                            currentGroup.Add(neighbor);
                            groupSize++;
                        }
                    }
                }
            
                if (groupSize > maxGroupSize)
                {
                    maxGroupSize = groupSize;
                    maxGroup = new HashSet<Vector2Int>(currentGroup);
                }
            }
        
            string groupHint;
            if (maxGroupSize == 1)
            {
                var localizedString = new LocalizedString("GameText", "No enemies are adjacent to each other");
                groupHint = localizedString.GetLocalizedString();
            }
            else
            {
                var localizedString = new LocalizedString("GameText", "The largest group of enemy is {maxGroupSize}");
                localizedString.Arguments = new object[] { maxGroupSize };
                groupHint = localizedString.GetLocalizedString();
            }
            hints.Add(groupHint);
            // 检查最大组是否有未翻开的格子
            bool groupHasUnrevealed = false;
            foreach (Vector2Int pos in maxGroup)
            {
                if (!isRevealed[pos.x, pos.y])
                {
                    groupHasUnrevealed = true;
                    break;
                }
            }
            if (groupHasUnrevealed)
            {
                usefulHints.Add(groupHint);
            }
            
            
            // Enemy rows count
            HashSet<int> enemyRows = new HashSet<int>();
            foreach (Vector2Int enemy in enemies)
            {
                enemyRows.Add(enemy.x);
            }
            var rowsLocalizedString = new LocalizedString("GameText", "Enemies are in {enemyRows:plural:{} row|{} rows}");
            rowsLocalizedString.Arguments = new object[] { enemyRows.Count };
            string rowsHint = rowsLocalizedString.GetLocalizedString();
            hints.Add(rowsHint);
            // 检查这些行是否有未翻开的格子
            bool enemyRowsHaveUnrevealed = false;
            foreach (int r in enemyRows)
            {
                for (int c = 0; c < currentCol; c++)
                {
                    if (!isRevealed[r, c]&&(c!=col||r!=row))
                    {
                        enemyRowsHaveUnrevealed = true;
                        break;
                    }
                }
                if (enemyRowsHaveUnrevealed)
                    break;
            }
            if (enemyRowsHaveUnrevealed)
            {
                usefulHints.Add(rowsHint);
            }
        
            // Enemy columns count
            HashSet<int> enemyCols = new HashSet<int>();
            foreach (Vector2Int enemy in enemies)
            {
                enemyCols.Add(enemy.y);
            }
            var colsLocalizedString = new LocalizedString("GameText", "Enemies are in {enemyCols:plural:{} column|{} columns}");
            colsLocalizedString.Arguments = new object[] { enemyCols.Count };
            string colsHint = colsLocalizedString.GetLocalizedString();
            hints.Add(colsHint);
            // 检查这些列是否有未翻开的格子
            bool enemyColsHaveUnrevealed = false;
            foreach (int c in enemyCols)
            {
                for (int r = 0; r < currentRow; r++)
                {
                    if (!isRevealed[r, c]&&(c!=col||r!=row))
                    {
                        enemyColsHaveUnrevealed = true;
                        break;
                    }
                }
                if (enemyColsHaveUnrevealed)
                    break;
            }
            if (enemyColsHaveUnrevealed)
            {
                usefulHints.Add(colsHint);
            }
        }
        
        
        // 选择hint的逻辑：先尝试从usefulHints移除usedHints，如果存在直接在它里面随机
        // 否则从hints移除usedHints里面随机，否则所有hints随机
        List<string> availableHints = new List<string>();
        
        // 先尝试从usefulHints移除usedHints
        List<string> availableUsefulHints = new List<string>();
        foreach (string hint in usefulHints)
        {
            if (!usedHints.Contains(hint))
            {
                availableUsefulHints.Add(hint);
            }
        }
        
        if (availableUsefulHints.Count > 0)
        {
            availableHints = availableUsefulHints;
        }
        else
        {
            // 从hints移除usedHints
            foreach (string hint in hints)
            {
                if (!usedHints.Contains(hint))
                {
                    availableHints.Add(hint);
                }
            }
            
            // 如果还是没有可用的hint，使用所有hints
            if (availableHints.Count == 0)
            {
                availableHints = usefulHints;
            }
            if (availableHints.Count == 0)
            {
                availableHints = hints;
            }
        }
        
        // 随机选择一个hint
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
    
    // 获取未翻开的金币数量
    public int GetUnrevealedCoinCount()
    {
        int count = 0;
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.Coin && !isRevealed[row, col])
                {
                    count++;
                }
            }
        }
        return count;
    }
    
    // 获取未翻开的礼物数量
    public int GetUnrevealedGiftCount()
    {
        int count = 0;
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.Gift && !isRevealed[row, col])
                {
                    count++;
                }
            }
        }
        return count;
    }
    
    // 获取未翻开的hint数量
    public int GetUnrevealedHintCount()
    {
        int count = 0;
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.Hint && !isRevealed[row, col])
                {
                    count++;
                }
            }
        }
        return count;
    }
    
    // 获取总hint数量
    public int GetTotalHintCount()
    {
        int count = 0;
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.Hint)
                {
                    count++;
                }
            }
        }
        return count;
    }
    
    // 获取未翻开的敌人数量
    public int GetUnrevealedEnemyCount()
    {
        int count = 0;
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (IsEnemyCard(row, col) && !isRevealed[row, col])
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
    
    // Reveal所有未翻开的卡牌，使用scaleX动画（从0到1）
    public void RevealAllUnrevealedCards(System.Action onComplete = null)
    {
        if (tiles == null) 
        {
            onComplete?.Invoke();
            return;
        }
        
        List<Tile> unrevealedTilesList = new List<Tile>();
        
        // 收集所有未翻开的tile
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (!isRevealed[row, col] && tiles[row, col] != null)
                {
                    unrevealedTilesList.Add(tiles[row, col]);
                }
            }
        }
        
        if (unrevealedTilesList.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }
        
        // 先reveal所有卡牌（不播放动画，只是设置状态）
        foreach (Tile tile in unrevealedTilesList)
        {
            int row = tile.GetRow();
            int col = tile.GetCol();
            Vector2Int pos = new Vector2Int(row, col);
            
            // 更新状态
            if (!isRevealed[row, col])
            {
                unrevealedTiles.Remove(pos);
                revealableTiles.Remove(pos);
                revealedTiles.Add(pos);
                isRevealed[row, col] = true;
                tile.SetRevealed(true);
            }
        }
        
        // 执行scaleX动画：先设置为0，然后动画到1
        StartCoroutine(RevealAllCardsAnimation(unrevealedTilesList, onComplete));
    }
    
    private IEnumerator RevealAllCardsAnimation(List<Tile> tilesToReveal, System.Action onComplete)
    {
        // 保存所有tile的原始scale
        Dictionary<Tile, Vector3> originalScales = new Dictionary<Tile, Vector3>();
        foreach (Tile tile in tilesToReveal)
        {
            if (tile != null && tile.transform != null)
            {
                originalScales[tile] = tile.transform.localScale;
            }
        }
        
        // 将所有tile的scaleX设置为0
        foreach (Tile tile in tilesToReveal)
        {
            if (tile != null && tile.transform != null)
            {
                Vector3 currentScale = tile.transform.localScale;
                tile.transform.localScale = new Vector3(0, currentScale.y, currentScale.z);
            }
        }
        
        // 等待一帧
        yield return null;
        
        // 使用DOTween动画所有tile的scaleX从0到原始值
        Sequence sequence = DOTween.Sequence();
        
        foreach (Tile tile in tilesToReveal)
        {
            if (tile != null && tile.transform != null && originalScales.ContainsKey(tile))
            {
                Vector3 originalScale = originalScales[tile];
                // 动画scaleX从0到原始值
                sequence.Join(tile.transform.DOScaleX(originalScale.x, 0.3f).SetEase(Ease.OutQuad));
            }
        }
        
        // 等待动画完成
        yield return sequence.WaitForCompletion();
        
        onComplete?.Invoke();
    }
    
    // 逐个播放tile出现动画
    private IEnumerator AnimateTilesReveal(List<Tile> tilesToAnimate)
    {
        if (tilesToAnimate != null)
        {
            foreach (Tile tile in tilesToAnimate)
            {
                if (tile == null || tile.transform == null) continue;
            
                RectTransform rect = tile.transform as RectTransform;
                if (rect == null) continue;
            
                // 保存原始scale
                Vector3 originalScale = rect.localScale;
                rect.localScale = new Vector3(0, 1, 1);
                // 动画scale.x从0到原始值
                rect.DOScaleX(1, tileRevealDuration).SetEase(Ease.OutQuad);
            
                // 等待间隔时间
                yield return new WaitForSeconds(tileRevealInterval);
            }
        }
    }
    
    // 检测快捷键输入
    private void Update()
    {
        // 检测 Shift + 数字键
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            for (int i = 1; i <= 9; i++)
            {
                KeyCode keyCode = KeyCode.Alpha0 + i;
                if (Input.GetKeyDown(keyCode))
                {
                    LoadTestLevel(i);
                    break;
                }
            }
        }
    }
    
    // 加载测试关卡
    private void LoadTestLevel(int levelNumber)
    {
        Debug.Log($"加载测试关卡 {levelNumber}");
        
        // 根据关卡编号加载不同的预设
        switch (levelNumber)
        {
            case 1:
                LoadTestLevel1();
                break;
            // 可以在这里添加更多测试关卡
            default:
                Debug.LogWarning($"测试关卡 {levelNumber} 未定义");
                break;
        }
    }
    
    // 测试关卡1：3x3 的格子
    // 第一个格子是敌人，第二个格子是关于这一行有几个敌人的hint（已翻开），第三个是铃铛
    // 剩下的填充：coin, empty, empty, gift
    private void LoadTestLevel1()
    {
        // 清理旧的board
        ClearBoard();
        
        // 设置board大小为3x3
        currentRow = 3;
        currentCol = 3;
        
        // 初始化数组
        tiles = new Tile[currentRow, currentCol];
        cardTypes = new CardType[currentRow, currentCol];
        isRevealed = new bool[currentRow, currentCol];
        
        // 清空相关集合
        revealedTiles.Clear();
        unrevealedTiles.Clear();
        revealableTiles.Clear();
        hintContents.Clear();
        usedHints.Clear();
        
        // 先初始化所有位置为Blank
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                cardTypes[row, col] = CardType.Blank;
            }
        }
        
        // 设置卡牌类型
        // 第一行：[Enemy] [Hint] [Bell]
        cardTypes[0, 0] = CardType.Enemy;
        cardTypes[0, 1] = CardType.Hint;
        cardTypes[0, 2] = CardType.Bell; 
        
        // 第二行：[Coin] [Player] [Empty]
        cardTypes[1, 0] = CardType.Coin;
        cardTypes[1, 1] = CardType.Player;  // 中心位置是玩家
        // cardTypes[1, 2] = CardType.Blank; // 保持为Blank
        
        // 第三行：[Flashlight] [Empty] [Gift]
        cardTypes[2, 0] = CardType.Flashlight;
        // cardTypes[2, 1] = CardType.Blank; // 保持为Blank
        cardTypes[2, 2] = CardType.Gift;
        
        // 设置哪些卡牌是翻开的
        // 中心位置(1,1)的玩家牌是翻开的
        isRevealed[1, 1] = true;
        revealedTiles.Add(new Vector2Int(1, 1));
        
        // Hint (0,1) 也是翻开的
        isRevealed[0, 1] = true;
        revealedTiles.Add(new Vector2Int(0, 1));
        
        // 其他都是未翻开的
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (!isRevealed[row, col])
                {
                    unrevealedTiles.Add(new Vector2Int(row, col));
                }
            }
        }
        
        // 计算hint内容（关于这一行有几个敌人）
        Vector2Int hintPos = new Vector2Int(0, 1);
        int rowEnemies = 0;
        for (int c = 0; c < currentCol; c++)
        {
            if (IsEnemyCard(0, c))
                rowEnemies++;
        }
        var rowLocalizedString = new LocalizedString("GameText", "This row has {rowEnemies:plural:{} enemy|{} enemies}");
        rowLocalizedString.Arguments = new object[] { rowEnemies };
        string rowHint = rowLocalizedString.GetLocalizedString();
        hintContents[hintPos] = rowHint;
        usedHints.Add(rowHint);
        
        // 创建tile对象
        float tileSize = 100f;
        float offsetX = (currentCol - 1) * tileSize * 0.5f;
        float offsetY = (currentRow - 1) * tileSize * 0.5f;
        
        allTiles = new List<Tile>();
        
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                GameObject tileObj = Instantiate(tilePrefab, boardParent);
                tileObj.name = $"Tile_{row}_{col}";
                
                RectTransform rect = tileObj.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(col * tileSize - offsetX, (currentRow - 1 - row) * tileSize - offsetY);
                rect.sizeDelta = new Vector2(tileSize, tileSize);
                
                // 先把scale.x设为0
                Vector3 currentScale = rect.localScale;
                rect.localScale = new Vector3(0, currentScale.y, currentScale.z);
                
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
                allTiles.Add(tile);
            }
        }
        
        // 随机排序所有tile（用于动画效果）
        for (int i = allTiles.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Tile temp = allTiles[i];
            allTiles[i] = allTiles[j];
            allTiles[j] = temp;
        }
        
        // 更新所有tile的视觉（确保hint内容正确显示）
        UpdateAllTilesVisual();
        
        // 播放翻转动画
        RestartAnimateBoard();
        
        // 设置可翻开的格子（玩家周围的格子）
        AddNeighborsToRevealable(1, 1);
        
        // 检查玩家是否和police相邻，如果相邻，则把police周围的格子也加入可翻开列表
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        for (int i = 0; i < 4; i++)
        {
            int newRow = 1 + dx[i];
            int newCol = 1 + dy[i];
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
}

