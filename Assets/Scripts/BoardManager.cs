using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using System.Collections;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

public class BoardManager : Singleton<BoardManager>
{
    public GameObject tilePrefab;
    public Transform boardParent;
    
    [Header("Map Scaling")]
    [Tooltip("当地图任意一边(row/col)超过该值时，将缩小整个地图以适配视觉包围范围")]
    public int baseMaxSideForScale = 5;
    
    private Vector3 initialBoardParentScale = Vector3.one;
    private bool hasCachedInitialBoardParentScale = false;
    
    [Header("Tile Reveal Animation")]
    [Tooltip("每个tile出现动画之间的间隔时间（秒）")]
    public float tileRevealInterval = 0.1f;
    [Tooltip("每个tile出现动画的持续时间（秒）")]
    public float tileRevealDuration = 0.3f;
    
    private Tile[,] tiles;
    private CardType[,] cardTypes;
    private bool[,] isRevealed;
    /// <summary> 迷雾格子：2x2 随机区域，其下敌人不被 hint 观测 </summary>
    private bool[,] isMistTile;
    /// <summary> 寒冰格子：3x3 随机区域，翻开数量超过阈值后每次翻开扣血 </summary>
    private bool[,] isFrozenTile;
    /// <summary> 本关寒冰区域尺寸（行x列） </summary>
    private int frozenPatchRowSpan;
    private int frozenPatchColSpan;
    private List<CardType> cardDeck = new List<CardType>();
    private Dictionary<Vector2Int, string> hintContents = new Dictionary<Vector2Int, string>();
    private Dictionary<Vector2Int, string> hintKeys = new Dictionary<Vector2Int, string>(); // 存储每个hint的key（本地化前的值）
    private HashSet<string> usedHints = new HashSet<string>();
    // 存储每个hint的相关位置（在hint被设置时计算）
    private Dictionary<Vector2Int, HashSet<Vector2Int>> hintRelatedPositions = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
    
    private HashSet<Vector2Int> revealedTiles = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> unrevealedTiles = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> revealableTiles = new HashSet<Vector2Int>();
    private Vector2Int fakeRevealAllHintTilePos = new Vector2Int(-1, -1);
    private bool fakeRevealAllHintTileResolved = false;
    
    /// <summary> 已翻开的 hint 数量（不含 familiarStreet 自动翻开的那次） </summary>
    private int hintRevealedCountExcludingFamiliarStreet = 0;
    
    private int currentRow = 5;
    private int currentCol = 5;
    
    private void ApplyBoardParentScale(int row, int col)
    {
        if (boardParent == null) return;
        
        if (!hasCachedInitialBoardParentScale)
        {
            initialBoardParentScale = boardParent.localScale;
            hasCachedInitialBoardParentScale = true;
        }

        int maxDim = Mathf.Max(row, col);
        if (maxDim <= 0)
        {
            boardParent.localScale = initialBoardParentScale;
            return;
        }

        float scale = 1f;
        if (maxDim > baseMaxSideForScale)
        {
            // 保证更长的那条边在视觉上仍落在 baseMaxSideForScale 对应的包围范围内
            scale = (float)baseMaxSideForScale / maxDim;
        }
        
        boardParent.localScale = initialBoardParentScale * scale;
    }
    
    public void GenerateBoard()
    {
        // 获取当前关卡信息
        LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
        currentRow = levelInfo.row;
        currentCol = levelInfo.col;
        ApplyBoardParentScale(currentRow, currentCol);
        
        // 初始化数组
        tiles = new Tile[currentRow, currentCol];
        cardTypes = new CardType[currentRow, currentCol];
        isRevealed = new bool[currentRow, currentCol];
        isMistTile = new bool[currentRow, currentCol];
        isFrozenTile = new bool[currentRow, currentCol];
        frozenPatchRowSpan = 0;
        frozenPatchColSpan = 0;
        
        CreateCardDeck();
        ShuffleDeck();
        
        revealedTiles.Clear();
        unrevealedTiles.Clear();
        revealableTiles.Clear();
        hintContents.Clear();
        hintKeys.Clear();
        usedHints.Clear();
        hintRelatedPositions.Clear();
        fakeRevealAllHintTilePos = new Vector2Int(-1, -1);
        fakeRevealAllHintTileResolved = false;
        hintRevealedCountExcludingFamiliarStreet = 0;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.mainGameData.doublebladeNextRevealPending = false;
            GameManager.Instance.mainGameData.doublebladeStunThisEnemyReveal = false;
        }
        
        // 初始化棋盘为空白
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                cardTypes[row, col] = CardType.Blank;
                isRevealed[row, col] = false;
            }
        }
        
        // 场景迷雾/寒冰：须在放置玩家与任何卡牌之前确定，供后续放置（如 shadow 避开迷雾）使用
        PlaceMistAndFrozenTiles();
        
        // 使用LevelManager计算玩家位置（尽量最中间，如果是偶数则往下一行）
        Vector2Int playerPos = LevelManager.Instance.GetPlayerPosition(currentRow, currentCol);
        int centerRow = playerPos.x;
        int centerCol = playerPos.y;
        cardTypes[centerRow, centerCol] = CardType.Player;
        isRevealed[centerRow, centerCol] = true;
        revealedTiles.Add(new Vector2Int(centerRow, centerCol));

        // shadow boss：在 crack/snowman 等与其它卡组牌之前占格，且不占迷雾格
        bool isShadowBossLevel = !string.IsNullOrEmpty(levelInfo.boss) && levelInfo.boss.ToLower() == "shadow";
        bool shadowPlacedAtStart = isShadowBossLevel && PlaceShadowBossFirst(centerRow, centerCol);

        // boss=crack：先生成 crack 链条（生成逻辑优先于其他 enemy 的随机摆放）
        bool isCrackBossLevel = !string.IsNullOrEmpty(levelInfo.boss) && levelInfo.boss.ToLower() == "crack";
        if (isCrackBossLevel)
        {
            PlaceCrackBossFirst(centerRow, centerCol);
        }
        
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

        // 处理snowsnake boss：在nun之后，先放置snowsnakeHead，再按蛇形放置snowsnakeBody
        bool isSnowsnakeBossLevel = !string.IsNullOrEmpty(levelInfo.boss) && levelInfo.boss.ToLower().StartsWith("snowsnake");
        if (isSnowsnakeBossLevel)
        {
            PlaceSnowsnakeBossFirst(centerRow, centerCol, levelInfo.boss);
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
        
        // 如果已经放置了snowsnake boss，从卡组中移除（防止头/身体被随机打散）
        if (isSnowsnakeBossLevel)
        {
            remainingDeck.RemoveAll(card => card == CardType.SnowsnakeHead || card == CardType.SnowsnakeBody);
        }
        
        // shadow 已在 PlaceShadowBossFirst 放入棋盘，从随机卡组中移除
        if (shadowPlacedAtStart)
        {
            remainingDeck.Remove(CardType.Shadow);
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
            // 先放置isFixed卡：按优先级排序（bell第一，sign第二，其他保持原样）
            List<CardType> fixedCardsToPlace = new List<CardType>();
            foreach (var kvp in fixedCardCounts)
            {
                for (int i = 0; i < kvp.Value; i++)
                {
                    fixedCardsToPlace.Add(kvp.Key);
                }
            }
            
            // 排序：bell第一，sign第二，其他保持原样
            fixedCardsToPlace.Sort((a, b) =>
            {
                if (a == CardType.Bell && b != CardType.Bell) return -1;
                if (a != CardType.Bell && b == CardType.Bell) return 1;
                if (a == CardType.Sign && b != CardType.Sign) return -1;
                if (a != CardType.Sign && b == CardType.Sign) return 1;
                return 0;
            });
            
            // 打乱剩余位置
            for (int i = availablePositions.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                Vector2Int temp = availablePositions[i];
                availablePositions[i] = availablePositions[j];
                availablePositions[j] = temp;
            }
            
            // 放置isFixed卡
            List<Vector2Int> bellPositions = new List<Vector2Int>(); // 记录所有bell的位置
            
            foreach (CardType cardType in fixedCardsToPlace)
            {
                if (cardType == CardType.Bell)
                {
                    // 放置bell：从可用位置中随机选择
                    if (availablePositions.Count > 0)
                    {
                        int randomIndex = Random.Range(0, availablePositions.Count);
                        Vector2Int pos = availablePositions[randomIndex];
                        cardTypes[pos.x, pos.y] = CardType.Bell;
                        bellPositions.Add(pos);
                        availablePositions.RemoveAt(randomIndex);
                        unrevealedTiles.Add(pos);
                    }
                }
                else if (cardType == CardType.Sign)
                {
                    // 放置sign：必须在bell的同一行或同一列（如果没有bell，则不放置sign）
                    if (bellPositions.Count > 0)
                    {
                        // 找到和任意一个bell在同一行或同一列的位置
                        List<Vector2Int> validSignPositions = new List<Vector2Int>();
                        foreach (Vector2Int pos in availablePositions)
                        {
                            foreach (Vector2Int bellPos in bellPositions)
                            {
                                if (pos.x == bellPos.x || pos.y == bellPos.y)
                                {
                                    validSignPositions.Add(pos);
                                    break; // 找到一个bell满足条件即可
                                }
                            }
                        }
                        
                        // 如果有合适的位置，随机选择一个
                        if (validSignPositions.Count > 0)
                        {
                            int randomIndex = Random.Range(0, validSignPositions.Count);
                            Vector2Int pos = validSignPositions[randomIndex];
                            cardTypes[pos.x, pos.y] = CardType.Sign;
                            availablePositions.Remove(pos);
                            unrevealedTiles.Add(pos);
                        }
                        // 如果没有合适的位置，不放置这个sign（跳过）
                    }
                    // 如果没有bell，不放置sign（跳过）
                }
                else
                {
                    // 其他isFixed卡：从可用位置中随机选择
                    if (availablePositions.Count > 0)
                    {
                        int randomIndex = Random.Range(0, availablePositions.Count);
                        Vector2Int pos = availablePositions[randomIndex];
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
                        
                        availablePositions.RemoveAt(randomIndex);
                    }
                }
            }
            
            // 打乱其他卡；若场上初始只有9格，则非 isFixed 卡中优先放入 hint
            int totalTiles = currentRow * currentCol;
            if (totalTiles == 9)
            {
                List<CardType> hintCards = new List<CardType>();
                List<CardType> restCards = new List<CardType>();
                foreach (CardType ct in otherCards)
                {
                    if (ct == CardType.Hint)
                        hintCards.Add(ct);
                    else
                        restCards.Add(ct);
                }
                for (int i = hintCards.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    CardType temp = hintCards[i];
                    hintCards[i] = hintCards[j];
                    hintCards[j] = temp;
                }
                for (int i = restCards.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    CardType temp = restCards[i];
                    restCards[i] = restCards[j];
                    restCards[j] = temp;
                }
                otherCards.Clear();
                otherCards.AddRange(hintCards);
                otherCards.AddRange(restCards);
            }
            else
            {
                for (int i = otherCards.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    CardType temp = otherCards[i];
                    otherCards[i] = otherCards[j];
                    otherCards[j] = temp;
                }
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
                tile.SetMist(isMistTile[row, col]);
                tile.SetFrozen(isFrozenTile[row, col]);
                
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
        
        // 关卡中第一次 revealableTiles 更新不在 GenerateBoard 内执行，改为在 GameManager 中
        // 在所有逻辑（如 familiarStreet、RevealAllHintTiles）执行完毕后，调用 RefreshRevealableTilesFromPlayerBFS()
        
        // 处理boss逻辑（snowman boss已经在之前处理了，这里只需要处理其他boss）
        // 注意：snowman boss已经在PlaceSnowmanBossFirst中处理了
        if (string.IsNullOrEmpty(levelInfo.boss) || levelInfo.boss.ToLower() != "snowman")
        {
            HandleBossGeneration(levelInfo);
        }
        
        // 更新所有Sign卡片的箭头指向
        UpdateSignArrows();
    }
    
    /// <summary>
    /// 从玩家格开始 BFS：只沿已翻开的格子扩展，遇到未翻开的格子则加入 revealableTiles 并停止从该格继续搜索。
    /// 关卡中第一次调用应在所有其他逻辑（如 RevealAllHintTiles、familiarStreet）执行完毕后再执行。
    /// </summary>
    public void RefreshRevealableTilesFromPlayerBFS()
    {
        revealableTiles.Clear();
        Vector2Int playerPos = GetPlayerPosition();
        if (playerPos.x < 0 || playerPos.y < 0) return;
        
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(playerPos);
        visited.Add(playerPos);
        
        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                int nr = p.x + dx[i];
                int nc = p.y + dy[i];
                if (nr < 0 || nr >= currentRow || nc < 0 || nc >= currentCol) continue;
                Vector2Int np = new Vector2Int(nr, nc);
                if (visited.Contains(np)) continue;
                visited.Add(np);
                if (isRevealed[nr, nc])
                    queue.Enqueue(np);
                else
                    revealableTiles.Add(np);
            }
        }
        
        UpdateRevealableVisuals();
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

    /// <summary> shadow 关卡：在其它卡牌摆放之前占一格，且不占迷雾格 </summary>
    /// <returns> 是否成功在棋盘上放入 Shadow </returns>
    private bool PlaceShadowBossFirst(int playerRow, int playerCol)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] != CardType.Blank)
                    continue;
                if (row == playerRow && col == playerCol)
                    continue;
                if (isMistTile != null && isMistTile[row, col])
                    continue;
                candidates.Add(new Vector2Int(row, col));
            }
        }
        if (candidates.Count == 0)
        {
            Debug.LogWarning("PlaceShadowBossFirst: no non-mist blank cell for Shadow.");
            return false;
        }
        Vector2Int pos = candidates[Random.Range(0, candidates.Count)];
        cardTypes[pos.x, pos.y] = CardType.Shadow;
        unrevealedTiles.Add(pos);
        return true;
    }

    // noRing + boss=crack：在最左列随机选起点，然后只向右 / 右上 / 右下随机拓展到最右列
    private void PlaceCrackBossFirst(int playerRow, int playerCol)
    {
        if (currentCol <= 0 || currentRow <= 0) return;

        int leftCol = 0;
        int rightCol = currentCol - 1;

        // 起点只能在最左列的空白格（不能覆盖 player）
        List<int> startRows = new List<int>();
        for (int r = 0; r < currentRow; r++)
        {
            if (cardTypes[r, leftCol] == CardType.Blank && !(r == playerRow && leftCol == playerCol))
            {
                startRows.Add(r);
            }
        }

        if (startRows.Count == 0) return;

        // 为避免路径落在 player 上，尝试多次随机生成一条可行链
        int maxAttempts = 80;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int startRow = startRows[Random.Range(0, startRows.Count)];
            Vector2Int pos = new Vector2Int(startRow, leftCol);
            List<Vector2Int> path = new List<Vector2Int> { pos };

            bool ok = true;
            while (pos.y < rightCol)
            {
                int nextCol = pos.y + 1;
                List<Vector2Int> candidates = new List<Vector2Int>(3);

                // right
                TryAddCrackCandidate(candidates, pos.x, nextCol, playerRow, playerCol);
                // up-right
                TryAddCrackCandidate(candidates, pos.x - 1, nextCol, playerRow, playerCol);
                // down-right
                TryAddCrackCandidate(candidates, pos.x + 1, nextCol, playerRow, playerCol);

                if (candidates.Count == 0)
                {
                    ok = false;
                    break;
                }

                pos = candidates[Random.Range(0, candidates.Count)];
                path.Add(pos);
            }

            if (!ok) continue;

            // 落在 player 之外且每一步都保证可行后，正式写入 crack 卡牌
            foreach (var p in path)
            {
                cardTypes[p.x, p.y] = CardType.Crack;
            }
            return;
        }

        // 随机失败兜底：固定行号只向右铺到最右列（只要不落在 player 位置上即可）
        List<int> fallbackStartRows = new List<int>();
        for (int r = 0; r < currentRow; r++)
        {
            if (cardTypes[r, leftCol] == CardType.Blank && r != playerRow)
            {
                fallbackStartRows.Add(r);
            }
        }

        if (fallbackStartRows.Count == 0) return;

        int fallbackRow = fallbackStartRows[Random.Range(0, fallbackStartRows.Count)];
        for (int c = leftCol; c <= rightCol; c++)
        {
            cardTypes[fallbackRow, c] = CardType.Crack;
        }
    }

    private void TryAddCrackCandidate(List<Vector2Int> candidates, int row, int col, int playerRow, int playerCol)
    {
        if (row < 0 || row >= currentRow || col < 0 || col >= currentCol) return;
        if (row == playerRow && col == playerCol) return;
        // 生成时尽量只走空白格（本逻辑只会在生成早期调用，理论上除 player 外都应为空白）
        if (cardTypes[row, col] != CardType.Blank) return;

        candidates.Add(new Vector2Int(row, col));
    }

    // 放置snowsnake boss：snowsnake_数字
    // - 数字 = 蛇长（长度为N => 1个Head + (N-1)个Body）
    // - frozen 场景：Head 与整条蛇仅占用寒冰格（与 PlaceMistAndFrozenTiles 的 3x3 一致）
    // - 非 frozen 场景：Head 任意空白格，Body 四向邻接（兼容未来配置）
    private void PlaceSnowsnakeBossFirst(int playerRow, int playerCol, string bossIdentifier)
    {
        if (string.IsNullOrEmpty(bossIdentifier))
        {
            Debug.LogError("PlaceSnowsnakeBossFirst: bossIdentifier is null/empty.");
            return;
        }

        int length = 0;
        string[] parts = bossIdentifier.Split('_');
        if (parts.Length >= 2)
        {
            int.TryParse(parts[1], out length);
        }
        if (length <= 0) length = 1;
        int bodyCount = Mathf.Max(0, length - 1);

        bool snakeOnFrozenOnly = GameManager.Instance != null
            && GameManager.Instance.GetCurrentSceneInfo() != null
            && (GameManager.Instance.GetCurrentSceneInfo().HasType("frozen") || GameManager.Instance.GetCurrentSceneInfo().HasType("frozenNew"));

        // 收集所有可用的Head位置（player和已占用格不允许；frozen 场景仅限寒冰格）
        List<Vector2Int> availablePositions = new List<Vector2Int>();
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] != CardType.Blank) continue;
                if (snakeOnFrozenOnly && !isFrozenTile[row, col]) continue;
                availablePositions.Add(new Vector2Int(row, col));
            }
        }

        if (availablePositions.Count == 0)
        {
            Debug.LogError("PlaceSnowsnakeBossFirst: no available positions.");
            return;
        }

        // 尝试多次找到可行的蛇形链条
        int maxAttempts = 80;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int randomIndex = Random.Range(0, availablePositions.Count);
            Vector2Int headPos = availablePositions[randomIndex];

            cardTypes[headPos.x, headPos.y] = CardType.SnowsnakeHead;
            bool ok = TryPlaceSnowsnakeBodyChain(bodyCount, headPos, snakeOnFrozenOnly);
            if (ok) return;

            // 回滚：失败则清空Head（Body的回滚在递归中完成）
            cardTypes[headPos.x, headPos.y] = CardType.Blank;
        }

        Debug.LogError($"PlaceSnowsnakeBossFirst: failed to place snake (length={length}).");
    }

    private bool TryPlaceSnowsnakeBodyChain(int remainingBodyCount, Vector2Int tailPos, bool bodyOnFrozenOnly)
    {
        if (remainingBodyCount <= 0) return true;

        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int i = 0; i < 4; i++)
        {
            int nr = tailPos.x + dx[i];
            int nc = tailPos.y + dy[i];
            if (nr < 0 || nr >= currentRow || nc < 0 || nc >= currentCol) continue;
            if (cardTypes[nr, nc] != CardType.Blank) continue;
            if (bodyOnFrozenOnly && !isFrozenTile[nr, nc]) continue;
            candidates.Add(new Vector2Int(nr, nc));
        }

        // 随机打乱候选，增加成功率的随机性
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector2Int temp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = temp;
        }

        foreach (var pos in candidates)
        {
            cardTypes[pos.x, pos.y] = CardType.SnowsnakeBody;
            if (TryPlaceSnowsnakeBodyChain(remainingBodyCount - 1, pos, bodyOnFrozenOnly))
                return true;
            // 回滚
            cardTypes[pos.x, pos.y] = CardType.Blank;
        }

        return false;
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
        // shadow boss 已在 PlaceShadowBossFirst 中处理；horribleman 等仍由战后生成
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
            if (cardTypes[hintRow, hintCol] == CardType.Blank)
            {
                int hintIndex = remainingDeck.FindIndex(card => card == CardType.Hint);
                if (hintIndex >= 0)
                {
                    cardTypes[hintRow, hintCol] = CardType.Hint;
                    remainingDeck.RemoveAt(hintIndex);
                    unrevealedTiles.Add(new Vector2Int(hintRow, hintCol));
                }
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
        if (playerCol - 1 >= 0 && cardTypes[playerRow, playerCol - 1] == CardType.Blank)
            coinGiftBellPositions.Add(new Vector2Int(playerRow, playerCol - 1));
        // 右
        if (playerCol + 1 < currentCol && cardTypes[playerRow, playerCol + 1] == CardType.Blank)
            coinGiftBellPositions.Add(new Vector2Int(playerRow, playerCol + 1));
        // 左上
        if (playerRow - 1 >= 0 && playerCol - 1 >= 0 && cardTypes[playerRow - 1, playerCol - 1] == CardType.Blank)
            coinGiftBellPositions.Add(new Vector2Int(playerRow - 1, playerCol - 1));
        // 右上
        if (playerRow - 1 >= 0 && playerCol + 1 < currentCol && cardTypes[playerRow - 1, playerCol + 1] == CardType.Blank)
            coinGiftBellPositions.Add(new Vector2Int(playerRow - 1, playerCol + 1));
        
        // 先放置所有coin
        int posIndex = 0;
        List<CardType> remainingCoins = new List<CardType>();
        foreach (CardType coin in coins)
        {
            if (posIndex < coinGiftBellPositions.Count)
            {
                Vector2Int pos = coinGiftBellPositions[posIndex++];
                cardTypes[pos.x, pos.y] = CardType.Coin;
                unrevealedTiles.Add(pos);
            }
            else
            {
                remainingCoins.Add(coin);
            }
        }

        // 将未放置的 coin 放回卡组
        remainingDeck.AddRange(remainingCoins);
        
        // 再放置所有gift
        List<CardType> remainingGifts = new List<CardType>();
        foreach (CardType gift in gifts)
        {
            if (posIndex < coinGiftBellPositions.Count)
            {
                Vector2Int pos = coinGiftBellPositions[posIndex++];
                cardTypes[pos.x, pos.y] = CardType.Gift;
                unrevealedTiles.Add(pos);
            }
            else
            {
                remainingGifts.Add(gift);
            }
        }

        // 将未放置的 gift 放回卡组
        remainingDeck.AddRange(remainingGifts);
        
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
            if (cardTypes[hintRow, hintCol] == CardType.Blank)
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
        }
        
        // 第二个hint（如果有）放在最左上角(0,0)，且提示关于这一列有几个敌人
        if (cardTypes[0, 0] == CardType.Blank)
        {
            int secondHintIndex = remainingDeck.FindIndex(card => card == CardType.Hint);
            if (secondHintIndex >= 0)
            {
                cardTypes[0, 0] = CardType.Hint;
                remainingDeck.RemoveAt(secondHintIndex);
                unrevealedTiles.Add(new Vector2Int(0, 0));
            }
        }
        
        // 第三个hint（如果有）放在最右下角，且提示关于这一行有几个敌人
        int bottomRightRow = currentRow - 1;
        int bottomRightCol = currentCol - 1;
        if (cardTypes[bottomRightRow, bottomRightCol] == CardType.Blank)
        {
            int thirdHintIndex = remainingDeck.FindIndex(card => card == CardType.Hint);
            if (thirdHintIndex >= 0)
            {
                cardTypes[bottomRightRow, bottomRightCol] = CardType.Hint;
                remainingDeck.RemoveAt(thirdHintIndex);
                unrevealedTiles.Add(new Vector2Int(bottomRightRow, bottomRightCol));
            }
        }
        
        // 收集所有enemy，从remainingDeck中移除
        List<CardType> enemies = remainingDeck.FindAll(card => card == CardType.Enemy);
        remainingDeck.RemoveAll(card => card == CardType.Enemy);
        
        // 第一个enemy生成在玩家上方
        int enemyRow = playerRow - 1;
        int enemyCol = playerCol;
        if (enemies.Count > 0 &&
            enemyRow >= 0 && enemyRow < currentRow &&
            enemyCol >= 0 && enemyCol < currentCol &&
            cardTypes[enemyRow, enemyCol] == CardType.Blank)
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
        if (bells.Count > 0 &&
            bellRow >= 0 && bellRow < currentRow &&
            bellCol >= 0 && bellCol < currentCol &&
            cardTypes[bellRow, bellCol] == CardType.Blank)
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

    public void SpawnShadowBoss()
    {
        // 在所有其他敌人被击败后生成 shadow（开局已放置时不会走进来）；不占迷雾格
        List<Vector2Int> availablePositions = new List<Vector2Int>();
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                // 不能是player位置，不能是已经放置boss的位置，必须是未翻开的位置
                if (cardTypes[row, col] != CardType.Player &&
                    cardTypes[row, col] != CardType.Shadow &&
                    !isRevealed[row, col] &&
                    (isMistTile == null || !isMistTile[row, col]))
                {
                    availablePositions.Add(new Vector2Int(row, col));
                }
            }
        }

        // 随机选择一个位置放置 shadow boss
        if (availablePositions.Count > 0)
        {
            int randomIndex = Random.Range(0, availablePositions.Count);
            Vector2Int bossPos = availablePositions[randomIndex];
            cardTypes[bossPos.x, bossPos.y] = CardType.Shadow;
            if (tiles[bossPos.x, bossPos.y] != null)
            {
                Sprite bossSprite = GetSpriteForCardType(CardType.Shadow);
                if (bossSprite == null)
                {
                    bossSprite = CardInfoManager.Instance.GetCardSprite(CardType.Blank);
                }
                tiles[bossPos.x, bossPos.y].SetFrontSprite(bossSprite);
                tiles[bossPos.x, bossPos.y].Initialize(bossPos.x, bossPos.y, CardType.Shadow, isRevealed[bossPos.x, bossPos.y]);
            }
        }
    }

    public void SpawnGhostBoss()
    {
        // 在所有其他敌人被击败后，生成 ghost boss
        List<Vector2Int> availablePositions = new List<Vector2Int>();
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                // 不能是player位置，不能是已经放置boss的位置，必须是未翻开的位置
                if (cardTypes[row, col] != CardType.Player &&
                    cardTypes[row, col] != CardType.Ghost &&
                    !isRevealed[row, col])
                {
                    availablePositions.Add(new Vector2Int(row, col));
                }
            }
        }

        // 随机选择一个位置放置 ghost boss
        if (availablePositions.Count > 0)
        {
            int randomIndex = Random.Range(0, availablePositions.Count);
            Vector2Int bossPos = availablePositions[randomIndex];
            cardTypes[bossPos.x, bossPos.y] = CardType.Ghost;
            if (tiles[bossPos.x, bossPos.y] != null)
            {
                Sprite bossSprite = GetSpriteForCardType(CardType.Ghost);
                if (bossSprite == null)
                {
                    bossSprite = CardInfoManager.Instance.GetCardSprite(CardType.Blank);
                }
                tiles[bossPos.x, bossPos.y].SetFrontSprite(bossSprite);
                tiles[bossPos.x, bossPos.y].Initialize(bossPos.x, bossPos.y, CardType.Ghost, isRevealed[bossPos.x, bossPos.y]);
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
                // 排除boss卡
                CardType cardType = cardTypes[row, col];
                if (cardType != CardType.Nun &&
                    cardType != CardType.Snowman &&
                    cardType != CardType.SnowsnakeHead &&
                    cardType != CardType.Horribleman &&
                    cardType != CardType.Ghost &&
                    cardType != CardType.Shadow)
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
            else if (bossType.StartsWith("snowsnake"))
            {
                // snowsnake关卡：sign指向snowsnake boss（Head）
                targetPos = GetBossPosition(CardType.SnowsnakeHead);
            }
            else if (bossType == "snowman")
            {
                // snowman关卡：sign指向snowman boss
                targetPos = GetBossPosition(CardType.Snowman);
            }
            else if (BossLevelIds.IsHorriblemanStyleBoss(bossType))
            {
                // horribleman / horriblemanNew 关卡：sign 指向 horribleman 卡
                targetPos = GetBossPosition(CardType.Horribleman);
            }
            else if (bossType == "shadow")
            {
                // shadow关卡：sign指向 shadow boss
                targetPos = GetBossPosition(CardType.Shadow);
            }
            else if (bossType == "ghost")
            {
                // ghost关卡：sign指向 ghost boss
                targetPos = GetBossPosition(CardType.Ghost);
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
    
    public Sprite GetSpriteForCardType(CardType cardType)
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
            
            // boss 关卡：不加入铃铛卡牌（boss 关有单独的铃铛逻辑）
            if (isBossLevel && cardType == CardType.Bell)
            {
                continue;
            }
            
            int count = cardInfo.start;
            
            // 如果是敌人（grinch），使用关卡配置的数量
            if (cardType == CardType.Enemy)
            {
                count = targetEnemyCount;
            }
            else
            {
                // 如果是购买的卡牌，增加数量
                if (GameManager.Instance != null && GameManager.Instance.mainGameData.purchasedCards.Contains(cardType))
                {
                    int c = GameManager.Instance.mainGameData.purchasedCards.Count(x => x == cardType);
                    count+=c;
                }
            }
            
            // 减去移除的数量
            if (GameManager.Instance != null)
            {
                int removedCount = GameManager.Instance.mainGameData.removedCards.Count(x => x == cardType);
                count -= removedCount;
            }
            
            // 确保count不为负数
            count = Mathf.Max(0, count);
            
            // isFixed的卡牌确保被使用（至少1张），但不固定位置（除了player）
            // player会单独处理，所以这里如果是player且isFixed，不需要减少
            
            for (int i = 0; i < count; i++)
            {
                cardDeck.Add(cardType);
            }
        }
        
        // noRing 模式：直接从卡组中去除铃铛（该模式下随时可敲铃铛，无需铃铛牌）
        bool noRingMode = GameManager.Instance != null && GameManager.Instance.GetCurrentSceneInfo() != null &&
            GameManager.Instance.GetCurrentSceneInfo().HasType("noRing");
        if (noRingMode)
        {
            cardDeck.RemoveAll(card => card == CardType.Bell);
        }

        // crack 仅在对应 noRing + boss=crack 的关卡里由 Board 逻辑手动生成
        cardDeck.RemoveAll(card => card == CardType.Crack);
        
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
    
    /// <summary> 根据场景类型放置迷雾(2x2)与寒冰(3x3)格子 </summary>
    private void PlaceMistAndFrozenTiles()
    {
        if (GameManager.Instance == null || GameManager.Instance.GetCurrentSceneInfo() == null)
            return;
        var sceneInfo = GameManager.Instance.GetCurrentSceneInfo();
        frozenPatchRowSpan = 0;
        frozenPatchColSpan = 0;
        
        if (sceneInfo.HasType("mist"))
        {
            // 总格数 > 30：三组互不重叠的 2x2；否则 >=5x5 两组；否则一组 2x2
            if (currentRow >= 2 && currentCol >= 2)
            {
                int tileCount = currentRow * currentCol;
                int mistBlockCount = 1;
                if (tileCount > 30)
                    mistBlockCount = 3;
                else if (currentRow >= 5 && currentCol >= 5)
                    mistBlockCount = 2;
                
                var mistCorners = new List<Vector2Int>();
                for (int b = 0; b < mistBlockCount; b++)
                {
                    for (int attempt = 0; attempt < 50; attempt++)
                    {
                        int r0 = Random.Range(0, currentRow - 1);
                        int c0 = Random.Range(0, currentCol - 1);
                        bool overlapsExisting = false;
                        for (int i = 0; i < mistCorners.Count; i++)
                        {
                            int pr = mistCorners[i].x, pc = mistCorners[i].y;
                            if (!(r0 + 1 < pr || pr + 1 < r0 || c0 + 1 < pc || pc + 1 < c0))
                            {
                                overlapsExisting = true;
                                break;
                            }
                        }
                        if (overlapsExisting) continue;
                        for (int r = r0; r <= r0 + 1 && r < currentRow; r++)
                            for (int c = c0; c <= c0 + 1 && c < currentCol; c++)
                                isMistTile[r, c] = true;
                        mistCorners.Add(new Vector2Int(r0, c0));
                        break;
                    }
                }
            }
        }
        
        if (sceneInfo.HasType("frozen") || sceneInfo.HasType("frozenNew"))
        {
            // frozen：随机 3x3；frozenNew：随机 ceil(row/2) x ceil(col/2)
            int frozenHeight = sceneInfo.HasType("frozenNew") ? Mathf.CeilToInt(currentRow / 2f) : 3;
            int frozenWidth = sceneInfo.HasType("frozenNew") ? Mathf.CeilToInt(currentCol / 2f) : 3;
            if (currentRow >= frozenHeight && currentCol >= frozenWidth)
            {
                int r0 = Random.Range(0, currentRow - frozenHeight + 1);
                int c0 = Random.Range(0, currentCol - frozenWidth + 1);
                for (int r = r0; r < r0 + frozenHeight && r < currentRow; r++)
                    for (int c = c0; c < c0 + frozenWidth && c < currentCol; c++)
                        isFrozenTile[r, c] = true;
                frozenPatchRowSpan = frozenHeight;
                frozenPatchColSpan = frozenWidth;
            }
        }
    }
    
    /// <summary> 获取本关寒冰区域尺寸（行,列） </summary>
    public Vector2Int GetFrozenPatchSize()
    {
        return new Vector2Int(frozenPatchRowSpan, frozenPatchColSpan);
    }
    
    public void RevealTile(int row, int col, bool isFirst = true, bool fromFamiliarStreet = false, bool fromPlayerClick = false,
        bool fromHintOneMoreUpgrade = false, bool suppressUpgradePropagation = false)
    {
        if (isRevealed[row, col]) return;
        
        Vector2Int pos = new Vector2Int(row, col);
        
        // 变色龙：先显示自身 0.2 秒，再 shake 0.3 秒，然后变身为相邻牌再执行正常翻牌逻辑
        if (cardTypes[row, col] == CardType.Chameleon && GameManager.Instance != null)
        {
            GameManager.Instance.StartCoroutine(GameManager.Instance.PlayChameleonAndReveal(row, col, isFirst, fromFamiliarStreet, fromPlayerClick, fromHintOneMoreUpgrade, suppressUpgradePropagation));
            return;
        }
        
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
        bool isHorriblemanBossLevel = !string.IsNullOrEmpty(levelInfo.boss) && BossLevelIds.IsHorriblemanStyleBoss(levelInfo.boss);

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
                        if (cardType != CardType.Nun &&
                            cardType != CardType.Snowman &&
                            cardType != CardType.SnowsnakeHead &&
                            cardType != CardType.Horribleman)
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
                RevealTile(row, col, isFirst, false, fromPlayerClick, fromHintOneMoreUpgrade, suppressUpgradePropagation);
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
                    RevealTile(row, col, isFirst, false, fromPlayerClick, fromHintOneMoreUpgrade, suppressUpgradePropagation);
                    return; // 不继续执行后面的逻辑，因为已经递归调用了RevealTile
                }
            }
        }
        
        // Hint 保底：按配置的阈值检查，若已翻开比例超过阈值但 hint 翻开数不足，且当前牌可交换，则与未翻开的 hint 交换后再翻开。
        // 注意：当本次 reveal 来自“连锁自动揭示”（suppressUpgradePropagation==true）时，不参与 hint 交换，避免连锁过程中被插入 hint。
        if (!suppressUpgradePropagation &&
            CardInfoManager.Instance != null && CardInfoManager.Instance.hintGuaranteeThresholds != null &&
            CardInfoManager.Instance.hintGuaranteeMinCounts != null && !tutorialForceBoard)
        {
            CardInfo info = CardInfoManager.Instance.GetCardInfo(cardTypes[row, col]);
            if (info != null && info.canSwapWithHint)
            {
                int totalTiles = currentRow * currentCol;
                float flippedCount = revealedTiles.Count;
                flippedCount -= 1;//remove player
                List<Vector2Int> unflippedHintPositions = new List<Vector2Int>();
                foreach (Vector2Int p in unrevealedTiles)
                {
                    if (p.x == row && p.y == col) continue;
                    if (cardTypes[p.x, p.y] == CardType.Hint)
                        unflippedHintPositions.Add(p);
                    
                }

                foreach (Vector2Int p in revealedTiles)
                {

                    if (cardTypes[p.x, p.y] == CardType.PoliceStation)
                        flippedCount -= 0.5f;
                }
                
                if (unflippedHintPositions.Count > 0)
                {
                    bool needSwapWithHint = false;
                    int thresholdsLen = Mathf.Min(CardInfoManager.Instance.hintGuaranteeThresholds.Length,
                        CardInfoManager.Instance.hintGuaranteeMinCounts.Length);
                    for (int i = 0; i < thresholdsLen && unflippedHintPositions.Count > 0; i++)
                    {
                        float threshold = CardInfoManager.Instance.hintGuaranteeThresholds[i];
                        int minHints = CardInfoManager.Instance.hintGuaranteeMinCounts[i];
                        if (flippedCount > totalTiles * threshold &&
                            hintRevealedCountExcludingFamiliarStreet < minHints)
                        {
                            needSwapWithHint = true;
                            break;
                        }
                    }

                    if (needSwapWithHint && unflippedHintPositions.Count > 0)
                    {
                        int randomIndex = Random.Range(0, unflippedHintPositions.Count);
                        Vector2Int hintPos = unflippedHintPositions[randomIndex];
                        CardType tempCardType = cardTypes[row, col];
                        cardTypes[row, col] = cardTypes[hintPos.x, hintPos.y];
                        tiles[row, col].UpdateType(cardTypes[row, col]);
                        
                        cardTypes[hintPos.x, hintPos.y] = tempCardType;
                        tiles[hintPos.x, hintPos.y].UpdateType(cardTypes[hintPos.x, hintPos.y]);
                            
                        //     Sprite horriblemanSprite = GetSpriteForCardType(CardType.Horribleman);
                        // if (horriblemanSprite == null)
                        // {
                        //     horriblemanSprite = CardInfoManager.Instance.GetCardSprite(CardType.Blank);
                        // }
                        // Sprite enemySprite = GetSpriteForCardType(cardTypes[row, col]);
                        // if (enemySprite == null)
                        // {
                        //     enemySprite = CardInfoManager.Instance.GetCardSprite(CardType.Blank);
                        // }
                        //
                        // // 更新tile的sprite和cardType
                        // tiles[row, col].SetFrontSprite(enemySprite);
                        // tiles[row, col].Initialize(row, col, cardTypes[row, col], isRevealed[row, col]);
                        //
                        // tiles[enemyPos.x, enemyPos.y].SetFrontSprite(horriblemanSprite);
                        // tiles[enemyPos.x, enemyPos.y].Initialize(enemyPos.x, enemyPos.y, CardType.Horribleman, isRevealed[enemyPos.x, enemyPos.y]);
                    }
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

            // 第二关：第一个hint（在玩家下方）和第二个hint（在(0,0)最左上角）一定关于这一列有几个敌人
            bool forceColHint = false;
            // 第二关：第三个hint（在最右下角）一定关于这一行有几个敌人
            bool forceRowHint = false;
            if (isLevel2)
            {
                Vector2Int hintPlayerPos = GetPlayerPosition();
                if ((row == hintPlayerPos.x + 1 && col == hintPlayerPos.y) || (row == 0 && col == 0))
                {
                    forceColHint = true;
                }
                if (row == currentRow - 1 && col == currentCol - 1)
                {
                    forceRowHint = true;
                }
            }
            
            string hint = CalculateHint(row, col, force3x3Hint, forceColHint, forceRowHint);
            hintContents[pos] = hint;
            
            // 对于强制hint，也需要计算相关位置（使用key）
            if (force3x3Hint || forceColHint || forceRowHint)
            {
                string hintKey = "";
                if (hintKeys.ContainsKey(pos))
                {
                    hintKey = hintKeys[pos];
                }
                if (!string.IsNullOrEmpty(hintKey))
                {
                    HashSet<Vector2Int> relatedPositions = CalculateHintRelatedPositions(row, col, hintKey, null);
                    hintRelatedPositions[pos] = relatedPositions;
                }
            }
        }
        
        // 从未翻开列表移除
        unrevealedTiles.Remove(pos);
        // 从可翻开列表移除
        revealableTiles.Remove(pos);
        // 加入已翻开列表
        revealedTiles.Add(pos);
        
        isRevealed[row, col] = true;
        
        if (cardTypes[row, col] == CardType.Hint && !fromFamiliarStreet && !fromHintOneMoreUpgrade)
            hintRevealedCountExcludingFamiliarStreet++;
        
        if (tiles[row, col] != null)
        {
            tiles[row, col].SetRevealed(true);
        }
        
        // 每次揭开一个 tile 后，用与关卡开始时相同的 BFS 逻辑从玩家格重新计算 revealableTiles
        RefreshRevealableTilesFromPlayerBFS();
        
        // 如果翻开的是Sign卡或Bell卡，更新所有Sign的箭头
        if (cardTypes[row, col] == CardType.Sign || cardTypes[row, col] == CardType.Bell)
        {
            UpdateSignArrows();
        }
        
        // 检查是否是最后一个tile或最后一个safe tile
        bool isLastTile = unrevealedTiles.Count == 0;
        bool isLastSafeTile = IsLastSafeTile(row, col);
        
        GameManager.Instance.OnTileRevealed(row, col, cardTypes[row, col], isLastTile, isLastSafeTile, isFirst, fromPlayerClick, fromFamiliarStreet, fromHintOneMoreUpgrade, suppressUpgradePropagation);
        
    }
    
    private bool IsLastSafeTile(int row, int col)
    {
        // 首先检查当前被翻开的tile是否是一个safe tile
        if (IsEnemyCard(row, col))
        {
            return false; // 当前翻开的tile是敌人，不是safe tile
        }
        
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

    /// <summary> Offset 模式：数量类 hint 显示值在真实值基础上 ±1，先算 +1 和 -1 的结果，在可行（[1, totalEnemies]）的结果里随机一个；若都不可行则取 +1 再 clamp。</summary>
    private int ApplyOffsetCount(int realValue, int totalEnemies)
    {
        if (GameManager.Instance?.GetCurrentSceneInfo()?.HasType("offset") != true || totalEnemies <= 0)
            return realValue;
        return ApplyOffsetWithRange(realValue, 1, totalEnemies);
    }

    /// <summary> Offset 模式：左右/上下差值 hint 显示值 ±1，先算 +1 和 -1 的结果，在可行（[0, totalEnemies]）的结果里随机一个；若都不可行则取 +1 再 clamp。</summary>
    private int ApplyOffsetDiff(int realDiff, int totalEnemies)
    {
        if (GameManager.Instance?.GetCurrentSceneInfo()?.HasType("offset") != true || totalEnemies <= 0)
            return realDiff;
        return ApplyOffsetWithRange(realDiff, 0, totalEnemies);
    }

    /// <summary> Offset 模式：带自定义上限的数量（如“分布在 x 列/行”），显示值 ±1，限制在 [1, maxCap]，maxCap 一般为 min(敌人数量, 列数/行数)。</summary>
    private int ApplyOffsetCountCapped(int realValue, int totalEnemies, int maxCap)
    {
        if (GameManager.Instance?.GetCurrentSceneInfo()?.HasType("offset") != true || totalEnemies <= 0)
            return realValue;
        int cap = Mathf.Min(totalEnemies, maxCap);
        if (cap < 1) return realValue;
        return ApplyOffsetWithRange(realValue, 1, cap);
    }

    private int ApplyOffsetWithRange(int realValue, int minInclusive, int maxInclusive)
    {
        int plusVal = realValue + 1;
        int minusVal = realValue - 1;
        bool plusFeasible = plusVal >= minInclusive && plusVal <= maxInclusive;
        bool minusFeasible = minusVal >= minInclusive && minusVal <= maxInclusive;
        if (plusFeasible && minusFeasible)
            return Random.Range(0, 2) == 0 ? plusVal : minusVal;
        if (plusFeasible) return plusVal;
        if (minusFeasible) return minusVal;
        return Mathf.Clamp(plusVal, minInclusive, maxInclusive);
    }

    private int ApplyFakeOffsetWithRange(int realValue, int minInclusive, int maxInclusive, out bool changed)
    {
        changed = false;
        if (maxInclusive < minInclusive) return realValue;
        List<int> candidates = new List<int>(2);
        int plusVal = realValue + 1;
        int minusVal = realValue - 1;
        if (plusVal >= minInclusive && plusVal <= maxInclusive && plusVal != realValue)
            candidates.Add(plusVal);
        if (minusVal >= minInclusive && minusVal <= maxInclusive && minusVal != realValue && minusVal != plusVal)
            candidates.Add(minusVal);
        if (candidates.Count == 0) return realValue;
        changed = true;
        return candidates[Random.Range(0, candidates.Count)];
    }

    private bool IsFakeRevealAllMode()
    {
        return GameManager.Instance?.GetCurrentSceneInfo()?.HasType("fakeRevealAll") == true;
    }

    private void EnsureFakeRevealAllHintTileResolved()
    {
        if (fakeRevealAllHintTileResolved) return;
        fakeRevealAllHintTileResolved = true;
        if (!IsFakeRevealAllMode() || cardTypes == null) return;
        List<Vector2Int> allHints = new List<Vector2Int>();
        for (int r = 0; r < currentRow; r++)
        {
            for (int c = 0; c < currentCol; c++)
            {
                if (cardTypes[r, c] == CardType.Hint)
                    allHints.Add(new Vector2Int(r, c));
            }
        }
        if (allHints.Count > 0)
            fakeRevealAllHintTilePos = allHints[Random.Range(0, allHints.Count)];
    }

    private string CalculateHint(int row, int col, bool force3x3Hint = false, bool forceColHint = false, bool forceRowHint = false)
    {
        // 迷雾格子下的敌人不被 hint 观测，只计可见敌人
        List<Vector2Int> enemies = GetAllEnemyPositions();
        List<Vector2Int> visibleEnemiesList = new List<Vector2Int>();
        foreach (var p in enemies) { if (IsEnemyVisibleForHint(p.x, p.y)) visibleEnemiesList.Add(p); }
        int totalEnemies = visibleEnemiesList.Count;
        
        // shadow boss：当该关存在影子时，所有涉及到影子的“数字提示”都显示为“？”
        // 同时不生成左右/上下敌人数量比较这两类 hint
        LevelInfo levelInfoForHint = LevelManager.Instance != null ? LevelManager.Instance.GetCurrentLevelInfo() : null;
        bool isShadowBossLevel = levelInfoForHint != null &&
                                 !string.IsNullOrEmpty(levelInfoForHint.boss) &&
                                 levelInfoForHint.boss.ToLower() == "shadow";

        string LocalizeWithShadowQuestion(string hintKey, int displayValue)
        {
            string localized = LocalizationHelper.GetLocalizedString(hintKey, new object[] { displayValue });
            return localized.Replace(displayValue.ToString(), "?");
        }

        bool ContainsShadowAt(List<Vector2Int> positions)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                Vector2Int p = positions[i];
                if (cardTypes[p.x, p.y] == CardType.Shadow)
                    return true;
            }
            return false;
        }
        
        List<string> hints = new List<string>();
        List<string> hintsKey = new List<string>(); // 存储本地化前的key
        List<bool> hintsGuaranteedFake = new List<bool>();
        List<string> usefulHints = new List<string>();
        List<string> usefulHintsKey = new List<string>(); // 存储本地化前的key
        List<bool> usefulHintsGuaranteedFake = new List<bool>();
        EnsureFakeRevealAllHintTileResolved();
        bool isFakeTile = IsFakeRevealAllMode() && fakeRevealAllHintTilePos.x == row && fakeRevealAllHintTilePos.y == col;
        
        // 如果强制使用3x3 hint，直接返回（相关位置会在RevealTile中计算）
        if (force3x3Hint)
        {
            int forcedNearbyEnemies = 0;
            bool forcedNearbyHasShadow = false;
            for (int r = row - 1; r <= row + 1; r++)
            {
                for (int c = col - 1; c <= col + 1; c++)
                {
                    if (r >= 0 && r < currentRow && c >= 0 && c < currentCol)
                    {
                        if (IsEnemyVisibleForHint(r, c))
                        {
                            forcedNearbyEnemies++;
                            if (cardTypes[r, c] == CardType.Shadow)
                                forcedNearbyHasShadow = true;
                        }
                    }
                }
            }
            forcedNearbyEnemies = isFakeTile
                ? ApplyFakeOffsetWithRange(forcedNearbyEnemies, 1, totalEnemies, out _)
                : ApplyOffsetCount(forcedNearbyEnemies, totalEnemies);
        
            string hintKey = "3x3 area around has {nearbyEnemies:plural:{} enemy|{} enemies}";
            bool shouldMaskForcedNearby = isShadowBossLevel && forcedNearbyHasShadow;
            string localizedText = shouldMaskForcedNearby
                ? LocalizeWithShadowQuestion(hintKey, forcedNearbyEnemies)
                : LocalizationHelper.GetLocalizedString(hintKey, new object[] { forcedNearbyEnemies });
            
            // 存储key
            Vector2Int force3x3HintPos = new Vector2Int(row, col);
            hintKeys[force3x3HintPos] = hintKey;
            
            return localizedText;
        }
        
        // 如果强制使用列hint，直接返回（相关位置会在RevealTile中计算）
        if (forceColHint)
        {
            int forcedColEnemies = 0;
            bool forcedColHasShadow = false;
            for (int r = 0; r < currentRow; r++)
            {
                if (IsEnemyVisibleForHint(r, col))
                {
                    forcedColEnemies++;
                    if (cardTypes[r, col] == CardType.Shadow)
                        forcedColHasShadow = true;
                }
            }
            forcedColEnemies = isFakeTile
                ? ApplyFakeOffsetWithRange(forcedColEnemies, 1, totalEnemies, out _)
                : ApplyOffsetCount(forcedColEnemies, totalEnemies);
        
            string hintKey = "This column has {colEnemies:plural:{} enemy|{} enemies}";
            bool shouldMaskForcedCol = isShadowBossLevel && forcedColHasShadow;
            string localizedText = shouldMaskForcedCol
                ? LocalizeWithShadowQuestion(hintKey, forcedColEnemies)
                : LocalizationHelper.GetLocalizedString(hintKey, new object[] { forcedColEnemies });
            
            // 存储key
            Vector2Int forceColHintPos = new Vector2Int(row, col);
            hintKeys[forceColHintPos] = hintKey;
            
            return localizedText;
        }
        
        // 如果强制使用行hint，直接返回（相关位置会在RevealTile中计算）
        if (forceRowHint)
        {
            int forcedRowEnemies = 0;
            bool forcedRowHasShadow = false;
            for (int c = 0; c < currentCol; c++)
            {
                if (IsEnemyVisibleForHint(row, c))
                {
                    forcedRowEnemies++;
                    if (cardTypes[row, c] == CardType.Shadow)
                        forcedRowHasShadow = true;
                }
            }
            forcedRowEnemies = isFakeTile
                ? ApplyFakeOffsetWithRange(forcedRowEnemies, 1, totalEnemies, out _)
                : ApplyOffsetCount(forcedRowEnemies, totalEnemies);
        
            string hintKey = "This row has {rowEnemies:plural:{} enemy|{} enemies}";
            bool shouldMaskForcedRow = isShadowBossLevel && forcedRowHasShadow;
            string localizedText = shouldMaskForcedRow
                ? LocalizeWithShadowQuestion(hintKey, forcedRowEnemies)
                : LocalizationHelper.GetLocalizedString(hintKey, new object[] { forcedRowEnemies });
            
            // 存储key
            Vector2Int forceRowHintPos = new Vector2Int(row, col);
            hintKeys[forceRowHintPos] = hintKey;
            
            return localizedText;
        }
        
        // Hint所在行有几个敌人（基于isEnemy，迷雾内敌人不计）
        int rowEnemies = 0;
        bool rowContainsShadow = false;
        bool rowHasUnrevealed = false;
        for (int c = 0; c < currentCol; c++)
        {
            if (IsEnemyVisibleForHint(row, c))
            {
                rowEnemies++;
                if (cardTypes[row, c] == CardType.Shadow)
                    rowContainsShadow = true;
            }
            if (!isRevealed[row, c] && (c != col))
                rowHasUnrevealed = true;
        }
        
        bool rowHintFakeChanged = false;
        int displayRowEnemies = isFakeTile
            ? ApplyFakeOffsetWithRange(rowEnemies, 1, totalEnemies, out rowHintFakeChanged)
            : ApplyOffsetCount(rowEnemies, totalEnemies);
        string rowHintKey = "This row has {rowEnemies:plural:{} enemy|{} enemies}";
        bool shouldMaskRowHint = isShadowBossLevel && rowContainsShadow;
        string rowHint = shouldMaskRowHint
            ? LocalizeWithShadowQuestion(rowHintKey, displayRowEnemies)
            : LocalizationHelper.GetLocalizedString(rowHintKey, new object[] { displayRowEnemies });
        hints.Add(rowHint);
        hintsKey.Add(rowHintKey);
        hintsGuaranteedFake.Add(rowHintFakeChanged);
        if (rowHasUnrevealed)
        {
            usefulHints.Add(rowHint);
            usefulHintsKey.Add(rowHintKey);
            usefulHintsGuaranteedFake.Add(rowHintFakeChanged);
        }
        
        // Hint所在列有几个敌人（基于isEnemy，迷雾内敌人不计）
        int colEnemies = 0;
        bool colContainsShadow = false;
        bool colHasUnrevealed = false;
        for (int r = 0; r < currentRow; r++)
        {
            if (IsEnemyVisibleForHint(r, col))
            {
                colEnemies++;
                if (cardTypes[r, col] == CardType.Shadow)
                    colContainsShadow = true;
            }
            if (!isRevealed[r, col] && (r != row))
                colHasUnrevealed = true;
        }
        
        bool colHintFakeChanged = false;
        int displayColEnemies = isFakeTile
            ? ApplyFakeOffsetWithRange(colEnemies, 1, totalEnemies, out colHintFakeChanged)
            : ApplyOffsetCount(colEnemies, totalEnemies);
        string colHintKey = "This column has {colEnemies:plural:{} enemy|{} enemies}";
        bool shouldMaskColHint = isShadowBossLevel && colContainsShadow;
        string colHint = shouldMaskColHint
            ? LocalizeWithShadowQuestion(colHintKey, displayColEnemies)
            : LocalizationHelper.GetLocalizedString(colHintKey, new object[] { displayColEnemies });
        hints.Add(colHint);
        hintsKey.Add(colHintKey);
        hintsGuaranteedFake.Add(colHintFakeChanged);
        if (colHasUnrevealed)
        {
            usefulHints.Add(colHint);
            usefulHintsKey.Add(colHintKey);
            usefulHintsGuaranteedFake.Add(colHintFakeChanged);
        }
        
        // 左右敌人数量比较（只在不是最左或最右的位置生成）
        if (!isShadowBossLevel && col > 0 && col < currentCol - 1)
        {
            int leftEnemies = 0;
            int rightEnemies = 0;
            bool leftRightHasUnrevealed = true;
            
            // 计算整个board上在这个格子左边的所有敌人（包括不同行），迷雾内不计
            for (int r = 0; r < currentRow; r++)
            {
                for (int c = 0; c < col; c++)
                {
                    if (IsEnemyVisibleForHint(r, c))
                        leftEnemies++;
                    if (!isRevealed[r, c])
                        leftRightHasUnrevealed = true;
                }
            }
            
            // 计算整个board上在这个格子右边的所有敌人（包括不同行），迷雾内不计
            for (int r = 0; r < currentRow; r++)
            {
                for (int c = col + 1; c < currentCol; c++)
                {
                    if (IsEnemyVisibleForHint(r, c))
                        rightEnemies++;
                    if (!isRevealed[r, c])
                        leftRightHasUnrevealed = true;
                }
            }
            
            int leftRightRealDiff = leftEnemies - rightEnemies; // 正=左多，负=右多
            int leftRightAbsDiff = leftRightRealDiff > 0 ? leftRightRealDiff : -leftRightRealDiff;
            bool leftRightHintFakeChanged = false;
            int displayLeftRightDiff = isFakeTile
                ? ApplyFakeOffsetWithRange(leftRightAbsDiff, 0, totalEnemies, out leftRightHintFakeChanged)
                : ApplyOffsetDiff(leftRightAbsDiff, totalEnemies);
            // offset 模式下 displayDiff 可能为 0（显示“一样多”）；若从“一样多”变成 1，随机选左或右
            bool showLeftMore = leftRightRealDiff > 0;
            if (leftRightRealDiff == 0 && displayLeftRightDiff == 1)
                showLeftMore = Random.Range(0, 2) == 0;
            else if (leftRightRealDiff > 0)
                showLeftMore = true;
            else if (leftRightRealDiff < 0)
                showLeftMore = false;
            
            string leftRightHintKey;
            string leftRightHint;
            if (displayLeftRightDiff == 0)
            {
                leftRightHintKey = "Same number of enemies on the left and right sides";
                var localizedString = new LocalizedString("GameText", leftRightHintKey);
                var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(localizedString.TableReference, localizedString.TableEntryReference);
                leftRightHint = handle.WaitForCompletion();
            }
            else if (showLeftMore)
            {
                leftRightHintKey = "{diff:plural:{} more enemy|{} more enemies} on left than on right";
                var localizedString = new LocalizedString("GameText", leftRightHintKey);
                localizedString.Arguments = new object[] { displayLeftRightDiff };
                var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(localizedString.TableReference, localizedString.TableEntryReference, localizedString.Arguments);
                leftRightHint = handle.WaitForCompletion();
            }
            else
            {
                leftRightHintKey = "{diff:plural:{} more enemy|{} more enemies} on right than on left";
                var localizedString = new LocalizedString("GameText", leftRightHintKey);
                localizedString.Arguments = new object[] { displayLeftRightDiff };
                var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(localizedString.TableReference, localizedString.TableEntryReference, localizedString.Arguments);
                leftRightHint = handle.WaitForCompletion();
            }
            
            hints.Add(leftRightHint);
            hintsKey.Add(leftRightHintKey);
            hintsGuaranteedFake.Add(leftRightHintFakeChanged);
            // 若左、右、所在列三个组中已有两个组全部揭开，则此 hint 无新信息，不算 useful（排除当前翻开的格子）
            bool leftGroupFullyRevealed = true;
            for (int r = 0; r < currentRow && leftGroupFullyRevealed; r++)
                for (int c = 0; c < col; c++)
                    if ((r != row || c != col) && !isRevealed[r, c]) { leftGroupFullyRevealed = false; break; }
            bool rightGroupFullyRevealed = true;
            for (int r = 0; r < currentRow && rightGroupFullyRevealed; r++)
                for (int c = col + 1; c < currentCol; c++)
                    if ((r != row || c != col) && !isRevealed[r, c]) { rightGroupFullyRevealed = false; break; }
            bool colGroupFullyRevealed = true;
            for (int r = 0; r < currentRow; r++)
                if (r != row && !isRevealed[r, col]) { colGroupFullyRevealed = false; break; }
            int leftRightRevealedGroups = (leftGroupFullyRevealed ? 1 : 0) + (rightGroupFullyRevealed ? 1 : 0) + (colGroupFullyRevealed ? 1 : 0);
            if (leftRightHasUnrevealed && leftRightRevealedGroups < 2)
            {
                usefulHints.Add(leftRightHint);
                usefulHintsKey.Add(leftRightHintKey);
                usefulHintsGuaranteedFake.Add(leftRightHintFakeChanged);
            }
        }
        
        // 上下敌人数量比较（只在不是最上或最下的位置生成）
        if (!isShadowBossLevel && row > 0 && row < currentRow - 1)
        {
            int topEnemies = 0;
            int bottomEnemies = 0;
            bool topBottomHasUnrevealed = true;
            
            // 计算整个board上在这个格子上边的所有敌人（包括不同列），迷雾内不计
            for (int r = 0; r < row; r++)
            {
                for (int c = 0; c < currentCol; c++)
                {
                    if (IsEnemyVisibleForHint(r, c))
                        topEnemies++;
                    if (!isRevealed[r, c])
                        topBottomHasUnrevealed = true;
                }
            }
            
            // 计算整个board上在这个格子下边的所有敌人（包括不同列），迷雾内不计
            for (int r = row + 1; r < currentRow; r++)
            {
                for (int c = 0; c < currentCol; c++)
                {
                    if (IsEnemyVisibleForHint(r, c))
                        bottomEnemies++;
                    if (!isRevealed[r, c])
                        topBottomHasUnrevealed = true;
                }
            }
            
            int topBottomRealDiff = topEnemies - bottomEnemies;
            int topBottomAbsDiff = topBottomRealDiff > 0 ? topBottomRealDiff : -topBottomRealDiff;
            bool topBottomHintFakeChanged = false;
            int displayTopBottomDiff = isFakeTile
                ? ApplyFakeOffsetWithRange(topBottomAbsDiff, 0, totalEnemies, out topBottomHintFakeChanged)
                : ApplyOffsetDiff(topBottomAbsDiff, totalEnemies);
            bool showTopMore = topBottomRealDiff > 0;
            if (topBottomRealDiff == 0 && displayTopBottomDiff == 1)
                showTopMore = Random.Range(0, 2) == 0;
            else if (topBottomRealDiff > 0)
                showTopMore = true;
            else if (topBottomRealDiff < 0)
                showTopMore = false;
            
            string topBottomHintKey;
            string topBottomHint;
            if (displayTopBottomDiff == 0)
            {
                topBottomHintKey = "Same number of enemies on top and bottom";
                var localizedString = new LocalizedString("GameText", topBottomHintKey);
                var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(localizedString.TableReference, localizedString.TableEntryReference);
                topBottomHint = handle.WaitForCompletion();
            }
            else if (showTopMore)
            {
                topBottomHintKey = "{diff:plural:{} more enemy|{} more enemies} on top than on bottom";
                var localizedString = new LocalizedString("GameText", topBottomHintKey);
                localizedString.Arguments = new object[] { displayTopBottomDiff };
                var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(localizedString.TableReference, localizedString.TableEntryReference, localizedString.Arguments);
                topBottomHint = handle.WaitForCompletion();
            }
            else
            {
                topBottomHintKey = "{diff:plural:{} more enemy|{} more enemies} on bottom than on top";
                var localizedString = new LocalizedString("GameText", topBottomHintKey);
                localizedString.Arguments = new object[] { displayTopBottomDiff };
                var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(localizedString.TableReference, localizedString.TableEntryReference, localizedString.Arguments);
                topBottomHint = handle.WaitForCompletion();
            }
            
            hints.Add(topBottomHint);
            hintsKey.Add(topBottomHintKey);
            hintsGuaranteedFake.Add(topBottomHintFakeChanged);
            // 若上、下、所在行三个组中已有两个组全部揭开，则此 hint 无新信息，不算 useful（排除当前翻开的格子）
            bool topGroupFullyRevealed = true;
            for (int r = 0; r < row && topGroupFullyRevealed; r++)
                for (int c = 0; c < currentCol; c++)
                    if ((r != row || c != col) && !isRevealed[r, c]) { topGroupFullyRevealed = false; break; }
            bool bottomGroupFullyRevealed = true;
            for (int r = row + 1; r < currentRow && bottomGroupFullyRevealed; r++)
                for (int c = 0; c < currentCol; c++)
                    if ((r != row || c != col) && !isRevealed[r, c]) { bottomGroupFullyRevealed = false; break; }
            bool rowGroupFullyRevealed = true;
            for (int c = 0; c < currentCol; c++)
                if (c != col && !isRevealed[row, c]) { rowGroupFullyRevealed = false; break; }
            int topBottomRevealedGroups = (topGroupFullyRevealed ? 1 : 0) + (bottomGroupFullyRevealed ? 1 : 0) + (rowGroupFullyRevealed ? 1 : 0);
            if (topBottomHasUnrevealed && topBottomRevealedGroups < 2)
            {
                usefulHints.Add(topBottomHint);
                usefulHintsKey.Add(topBottomHintKey);
                usefulHintsGuaranteedFake.Add(topBottomHintFakeChanged);
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
                if (IsEnemyVisibleForHint(corner.x, corner.y))
                    cornerEnemies++;
                if (!isRevealed[corner.x, corner.y] && (corner.y != col || corner.x != row))
                    cornersHaveUnrevealed = true;
            }
        }
        
        bool cornerHintFakeChanged = false;
        int displayCornerEnemies = isFakeTile
            ? ApplyFakeOffsetWithRange(cornerEnemies, 1, totalEnemies, out cornerHintFakeChanged)
            : ApplyOffsetCount(cornerEnemies, totalEnemies);
        string cornerHintKey = "There {cornerEnemies:plural:is {} enemy|are {} enemies} in the four corners";
        bool cornersContainShadow = false;
        foreach (Vector2Int corner in corners)
        {
            if (corner.x >= 0 && corner.x < currentRow && corner.y >= 0 && corner.y < currentCol &&
                IsEnemyVisibleForHint(corner.x, corner.y) &&
                cardTypes[corner.x, corner.y] == CardType.Shadow)
            {
                cornersContainShadow = true;
                break;
            }
        }
        bool shouldMaskCornerHint = isShadowBossLevel && cornersContainShadow;
        string cornerHint = shouldMaskCornerHint
            ? LocalizeWithShadowQuestion(cornerHintKey, displayCornerEnemies)
            : LocalizationHelper.GetLocalizedString(cornerHintKey, new object[] { displayCornerEnemies });
        hints.Add(cornerHint);
        hintsKey.Add(cornerHintKey);
        hintsGuaranteedFake.Add(cornerHintFakeChanged);
        if (cornersHaveUnrevealed)
        {
            usefulHints.Add(cornerHint);
            usefulHintsKey.Add(cornerHintKey);
            usefulHintsGuaranteedFake.Add(cornerHintFakeChanged);
        }
        
        // 保留原有的提示类型
        // Nearby 3x3 area enemy count（基于isEnemy，迷雾内不计）
        int nearbyEnemies = 0;
        bool nearbyContainsShadow = false;
        bool nearbyHasUnrevealed = false;
        for (int r = row - 1; r <= row + 1; r++)
        {
            for (int c = col - 1; c <= col + 1; c++)
            {
                if (r >= 0 && r < currentRow && c >= 0 && c < currentCol)
                {
                    if (IsEnemyVisibleForHint(r, c))
                    {
                        nearbyEnemies++;
                        if (cardTypes[r, c] == CardType.Shadow)
                            nearbyContainsShadow = true;
                    }
                    if (!isRevealed[r, c] && (c != col || r != row))
                        nearbyHasUnrevealed = true;
                }
            }
        }
        
        bool nearbyHintFakeChanged = false;
        int displayNearbyEnemies = isFakeTile
            ? ApplyFakeOffsetWithRange(nearbyEnemies, 1, totalEnemies, out nearbyHintFakeChanged)
            : ApplyOffsetCount(nearbyEnemies, totalEnemies);
        string nearbyHintKey = "3x3 area around has {nearbyEnemies:plural:{} enemy|{} enemies}";
        bool shouldMaskNearbyHint = isShadowBossLevel && nearbyContainsShadow;
        string nearbyHint = shouldMaskNearbyHint
            ? LocalizeWithShadowQuestion(nearbyHintKey, displayNearbyEnemies)
            : LocalizationHelper.GetLocalizedString(nearbyHintKey, new object[] { displayNearbyEnemies });
        hints.Add(nearbyHint);
        hintsKey.Add(nearbyHintKey);
        hintsGuaranteedFake.Add(nearbyHintFakeChanged);
        if (nearbyHasUnrevealed)
        {
            usefulHints.Add(nearbyHint);
            usefulHintsKey.Add(nearbyHintKey);
            usefulHintsGuaranteedFake.Add(nearbyHintFakeChanged);
        }

        // 距离最近敌人的曼哈顿距离（横向距离 + 纵向距离）
        if (visibleEnemiesList.Count > 0)
        {
            int minDistance = int.MaxValue;
            Vector2Int nearestEnemyPos = new Vector2Int(-1, -1);
            foreach (Vector2Int enemy in visibleEnemiesList)
            {
                int dist = Mathf.Abs(row - enemy.x) + Mathf.Abs(col - enemy.y);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestEnemyPos = enemy;
                }
            }

            int displayNearestDistance = minDistance;
            string nearestDistanceHintKey = "Euclidean distance to nearest enemy is {nearestDistance}";
            bool nearestEnemyContainsShadow = nearestEnemyPos.x >= 0 && cardTypes[nearestEnemyPos.x, nearestEnemyPos.y] == CardType.Shadow;
            string nearestDistanceHint = (isShadowBossLevel && nearestEnemyContainsShadow)
                ? LocalizationHelper.GetLocalizedString(nearestDistanceHintKey, new object[] { "?" })
                : LocalizationHelper.GetLocalizedString(nearestDistanceHintKey, new object[] { displayNearestDistance });
            hints.Add(nearestDistanceHint);
            hintsKey.Add(nearestDistanceHintKey);
            hintsGuaranteedFake.Add(false);

            // 最近敌人还未翻开时，这条提示通常更有信息量
            if (nearestEnemyPos.x >= 0 && !isRevealed[nearestEnemyPos.x, nearestEnemyPos.y])
            {
                usefulHints.Add(nearestDistanceHint);
                usefulHintsKey.Add(nearestDistanceHintKey);
                usefulHintsGuaranteedFake.Add(false);
            }
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
                    if (IsEnemyVisibleForHint(newRow, newCol))
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
        
        // 检查：如果有churchRing升级，并且bell已经翻开了（即这个效果触发过），那么churchAdjacentHasUnrevealed = false
        if (GameManager.Instance?.upgradeManager?.HasUpgrade("churchRing") == true)
        {
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
            
            if (bellRevealed)
            {
                churchAdjacentHasUnrevealed = false;
            }
        }
        
        bool churchHintFakeChanged = false;
        int displayChurchAdjacent = isFakeTile
            ? ApplyFakeOffsetWithRange(enemiesAdjacentToChurch.Count, 1, totalEnemies, out churchHintFakeChanged)
            : ApplyOffsetCount(enemiesAdjacentToChurch.Count, totalEnemies);
        string churchHintKey = "There {enemiesAdjacentToChurch:plural:is no enemy|is 1 enemy|are {} enemies} adjacent to church";
        int churchDisplayForShadow = displayChurchAdjacent == 0 ? 2 : displayChurchAdjacent;
        bool churchContainsShadow = ContainsShadowAt(enemiesAdjacentToChurch.ToList());
        bool shouldMaskChurchHint = isShadowBossLevel && churchContainsShadow;
        string churchHint = shouldMaskChurchHint
            ? LocalizeWithShadowQuestion(churchHintKey, churchDisplayForShadow)
            : LocalizationHelper.GetLocalizedString(churchHintKey, new object[] { displayChurchAdjacent });
        hints.Add(churchHint);
        hintsKey.Add(churchHintKey);
        hintsGuaranteedFake.Add(churchHintFakeChanged);
        if (churchAdjacentHasUnrevealed)
        {
            usefulHints.Add(churchHint);
            usefulHintsKey.Add(churchHintKey);
            usefulHintsGuaranteedFake.Add(churchHintFakeChanged);
        }

        // 边界上的敌人数（只在地图 >=5x5 且边界未完全揭开时出现）
        if (currentRow > 5 || currentCol > 5)
        {
            int borderEnemyCount = 0;
            bool borderHasUnrevealed = false;
            bool borderContainsShadow = false;
            for (int r = 0; r < currentRow; r++)
            {
                for (int c = 0; c < currentCol; c++)
                {
                    bool isBorder = r == 0 || r == currentRow - 1 || c == 0 || c == currentCol - 1;
                    if (!isBorder)
                        continue;

                    if (IsEnemyVisibleForHint(r, c))
                    {
                        borderEnemyCount++;
                        if (cardTypes[r, c] == CardType.Shadow)
                            borderContainsShadow = true;
                    }

                    if ((r != row || c != col) && !isRevealed[r, c])
                    {
                        borderHasUnrevealed = true;
                    }
                }
            }

            if (borderHasUnrevealed)
            {
                bool borderHintFakeChanged = false;
                int displayBorderEnemyCount = isFakeTile
                    ? ApplyFakeOffsetWithRange(borderEnemyCount, 1, totalEnemies, out borderHintFakeChanged)
                    : ApplyOffsetCount(borderEnemyCount, totalEnemies);
                string borderHintKey = "There {borderEnemies:plural:is {} enemy|are {} enemies} on the border";
                string borderHint = (isShadowBossLevel && borderContainsShadow)
                    ? LocalizeWithShadowQuestion(borderHintKey, displayBorderEnemyCount)
                    : LocalizationHelper.GetLocalizedString(borderHintKey, new object[] { displayBorderEnemyCount });
                hints.Add(borderHint);
                hintsKey.Add(borderHintKey);
                hintsGuaranteedFake.Add(borderHintFakeChanged);
                usefulHints.Add(borderHint);
                usefulHintsKey.Add(borderHintKey);
                usefulHintsGuaranteedFake.Add(borderHintFakeChanged);
            }
        }

        if (visibleEnemiesList.Count > 1 && !isShadowBossLevel)
        {
            
            // 找到最大的敌人group（四向邻接，仅可见敌人）
            int maxGroupSize = 0;
            HashSet<Vector2Int> maxGroup = new HashSet<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            int[] dx = { 0, 0, 1, -1 }; // 上下左右
            int[] dy = { 1, -1, 0, 0 };
            
            foreach (Vector2Int enemy in visibleEnemiesList)
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
                
                    // 检查四个方向的邻居（基于isEnemy，仅可见敌人）
                    for (int i = 0; i < 4; i++)
                    {
                        int newRow = current.x + dx[i];
                        int newCol = current.y + dy[i];
                        Vector2Int neighbor = new Vector2Int(newRow, newCol);
                    
                        if (newRow >= 0 && newRow < currentRow && newCol >= 0 && newCol < currentCol &&
                            IsEnemyVisibleForHint(newRow, newCol) && !visited.Contains(neighbor))
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
            
            bool groupHintFakeChanged = false;
            int displayMaxGroupSize = isFakeTile
                ? ApplyFakeOffsetWithRange(maxGroupSize, 1, totalEnemies, out groupHintFakeChanged)
                : ApplyOffsetCount(maxGroupSize, totalEnemies);
            bool allVisibleContainsShadow = ContainsShadowAt(visibleEnemiesList);
            bool shouldMaskGroupHint = isShadowBossLevel && allVisibleContainsShadow;
            string groupHintKey;
            string groupHint;
            if (displayMaxGroupSize == 1 && !shouldMaskGroupHint)
            {
                groupHintKey = "No enemies are adjacent to each other";
                var localizedString = new LocalizedString("GameText", groupHintKey);
                groupHint = localizedString.GetLocalizedString();
            }
            else
            {
                groupHintKey = "The largest group of enemy is {maxGroupSize}";
                groupHint = LocalizationHelper.GetLocalizedString(groupHintKey, new object[] { displayMaxGroupSize });;
            }
            
            if (shouldMaskGroupHint)
                groupHint = LocalizeWithShadowQuestion("The largest group of enemy is {maxGroupSize}", displayMaxGroupSize == 0 ? 2 : displayMaxGroupSize);
            hints.Add(groupHint);
            hintsKey.Add(groupHintKey);
            hintsGuaranteedFake.Add(groupHintFakeChanged);
            // 检查：只有在存在敌人，且敌人周围有没有被翻开的格子时，才认为有用
            bool groupHasUnrevealed = false;
            //if (maxGroup != null && maxGroup.Count > 0)
            {
                int[] groupDx = { 0, 0, 1, -1 };
                int[] groupDy = { 1, -1, 0, 0 };
                
                // 检查最大组中的每个敌人，看其周围是否有未翻开的格子（仅可见敌人组）
                foreach (Vector2Int enemyPos in maxGroup)
                {
                    if (!isRevealed[enemyPos.x, enemyPos.y])
                    {
                        continue;
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        int newRow = enemyPos.x + groupDx[i];
                        int newCol = enemyPos.y + groupDy[i];
                        if (newRow >= 0 && newRow < currentRow && newCol >= 0 && newCol < currentCol && (newCol!=currentCol || newRow!=currentRow))
                        {
                            if (!isRevealed[newRow, newCol])
                            {
                                groupHasUnrevealed = true;
                                break;
                            }
                        }
                    }
                    if (groupHasUnrevealed)
                        break;
                }
            }
            if (groupHasUnrevealed)
            {
                usefulHints.Add(groupHint);
                usefulHintsKey.Add(groupHintKey);
                usefulHintsGuaranteedFake.Add(groupHintFakeChanged);
            }

            if (!isShadowBossLevel)
            {
                
            // Enemy rows count（仅统计对 hint 可见的敌人，迷雾下不计）
            HashSet<int> enemyRows = new HashSet<int>();
            foreach (Vector2Int enemy in visibleEnemiesList)
            {
                enemyRows.Add(enemy.x);
            }
            bool enemyRowsHintFakeChanged = false;
            int displayEnemyRows = isFakeTile
                ? ApplyFakeOffsetWithRange(enemyRows.Count, 1, Mathf.Min(totalEnemies, currentRow), out enemyRowsHintFakeChanged)
                : ApplyOffsetCountCapped(enemyRows.Count, totalEnemies, currentRow);
            string rowsHintKey = "Enemies are in {enemyRows:plural:{} row|{} rows}";
            bool shouldMaskRowsHint = isShadowBossLevel && ContainsShadowAt(visibleEnemiesList);
            string rowsHint = shouldMaskRowsHint
                ? LocalizeWithShadowQuestion(rowsHintKey, displayEnemyRows)
                : LocalizationHelper.GetLocalizedString(rowsHintKey, new object[] { displayEnemyRows });
            hints.Add(rowsHint);
            hintsKey.Add(rowsHintKey);
            hintsGuaranteedFake.Add(enemyRowsHintFakeChanged);
            // 对于提示敌人分布在x行的hint，只有：
            // 1. 目前存在和这个hint不在同一行的敌人翻开了
            // 2. 这个敌人所在的行还有没翻开的格子
            // 才会触发
            bool enemyRowsHaveUnrevealed = false;
            foreach (Vector2Int enemy in visibleEnemiesList)
            {
                // 检查这个敌人是否和hint不在同一行，且已经翻开
                if (enemy.x != row && isRevealed[enemy.x, enemy.y])
                {
                    // 检查这个敌人所在的行是否还有未翻开的格子
                    bool enemyRowHasUnrevealed = false;
                    for (int c = 0; c < currentCol; c++)
                    {
                        if (!isRevealed[enemy.x, c] && (c != col || enemy.x != row))
                        {
                            enemyRowHasUnrevealed = true;
                            break;
                        }
                    }
                    if (enemyRowHasUnrevealed)
                    {
                        enemyRowsHaveUnrevealed = true;
                        break;
                    }
                }
            }
            if (enemyRowsHaveUnrevealed)
            {
                usefulHints.Add(rowsHint);
                usefulHintsKey.Add(rowsHintKey);
                usefulHintsGuaranteedFake.Add(enemyRowsHintFakeChanged);
            }
            
            // Enemy columns count（仅统计对 hint 可见的敌人，迷雾下不计）
            HashSet<int> enemyCols = new HashSet<int>();
            foreach (Vector2Int enemy in visibleEnemiesList)
            {
                enemyCols.Add(enemy.y);
            }
            bool enemyColsHintFakeChanged = false;
            int displayEnemyCols = isFakeTile
                ? ApplyFakeOffsetWithRange(enemyCols.Count, 1, Mathf.Min(totalEnemies, currentCol), out enemyColsHintFakeChanged)
                : ApplyOffsetCountCapped(enemyCols.Count, totalEnemies, currentCol);
            string colsHintKey = "Enemies are in {enemyCols:plural:{} column|{} columns}";
            bool shouldMaskColsHint = isShadowBossLevel && ContainsShadowAt(visibleEnemiesList);
            string colsHint = shouldMaskColsHint
                ? LocalizeWithShadowQuestion(colsHintKey, displayEnemyCols)
                : LocalizationHelper.GetLocalizedString(colsHintKey, new object[] { displayEnemyCols });
            hints.Add(colsHint);
            hintsKey.Add(colsHintKey);
            hintsGuaranteedFake.Add(enemyColsHintFakeChanged);
            // 对于提示敌人分布在x列的hint，只有：
            // 1. 目前存在和这个hint不在同一列的敌人翻开了
            // 2. 这个敌人所在的列还有没翻开的格子
            // 才会触发
            bool enemyColsHaveUnrevealed = false;
            foreach (Vector2Int enemy in visibleEnemiesList)
            {
                // 检查这个敌人是否和hint不在同一列，且已经翻开
                if (enemy.y != col && isRevealed[enemy.x, enemy.y])
                {
                    // 检查这个敌人所在的列是否还有未翻开的格子
                    bool enemyColHasUnrevealed = false;
                    for (int r = 0; r < currentRow; r++)
                    {
                        if (!isRevealed[r, enemy.y] && (r != row || enemy.y != col))
                        {
                            enemyColHasUnrevealed = true;
                            break;
                        }
                    }
                    if (enemyColHasUnrevealed)
                    {
                        enemyColsHaveUnrevealed = true;
                        break;
                    }
                }
            }
            if (enemyColsHaveUnrevealed)
            {
                usefulHints.Add(colsHint);
                usefulHintsKey.Add(colsHintKey);
                usefulHintsGuaranteedFake.Add(enemyColsHintFakeChanged);
            }

        }
        
        }
        
        // 选择hint的逻辑：先尝试从usefulHints移除usedHints，如果存在直接在它里面随机
        // 否则从hints移除usedHints里面随机，否则所有hints随机
        List<string> availableHints = new List<string>();
        List<string> availableHintsKey = new List<string>(); // 存储对应的key
        List<bool> availableHintsGuaranteedFake = new List<bool>();
        
        // 先尝试从usefulHints移除usedHints
        List<string> availableUsefulHints = new List<string>();
        List<string> availableUsefulHintsKey = new List<string>();
        List<bool> availableUsefulHintsGuaranteedFake = new List<bool>();
        for (int i = 0; i < usefulHints.Count; i++)
        {
            if (!usedHints.Contains(usefulHints[i]))
            {
                availableUsefulHints.Add(usefulHints[i]);
                availableUsefulHintsKey.Add(usefulHintsKey[i]);
                availableUsefulHintsGuaranteedFake.Add(usefulHintsGuaranteedFake[i]);
            }
        }
        
        if (availableUsefulHints.Count > 0)
        {
            availableHints = availableUsefulHints;
            availableHintsKey = availableUsefulHintsKey;
            availableHintsGuaranteedFake = availableUsefulHintsGuaranteedFake;
        }
        else
        {
            // 从hints移除usedHints
            for (int i = 0; i < hints.Count; i++)
            {
                if (!usedHints.Contains(hints[i]))
                {
                    availableHints.Add(hints[i]);
                    availableHintsKey.Add(hintsKey[i]);
                    availableHintsGuaranteedFake.Add(hintsGuaranteedFake[i]);
                }
            }
            
            // 如果还是没有可用的hint，使用所有hints
            if (availableHints.Count == 0)
            {
                availableHints = usefulHints;
                availableHintsKey = usefulHintsKey;
                availableHintsGuaranteedFake = usefulHintsGuaranteedFake;
            }
            if (availableHints.Count == 0)
            {
                availableHints = hints;
                availableHintsKey = hintsKey;
                availableHintsGuaranteedFake = hintsGuaranteedFake;
            }
        }
        
        // 保存maxGroup以便后续使用（如果计算了的话）；与上面最大组 hint 一致，仅可见敌人、迷雾下不计
        HashSet<Vector2Int> savedMaxGroup = null;
        if (visibleEnemiesList.Count > 1)
        {
            int maxGroupSize = 0;
            HashSet<Vector2Int> maxGroup = new HashSet<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };
            
            foreach (Vector2Int enemy in visibleEnemiesList)
            {
                if (visited.Contains(enemy))
                    continue;
            
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                HashSet<Vector2Int> currentGroup = new HashSet<Vector2Int>();
                queue.Enqueue(enemy);
                visited.Add(enemy);
                currentGroup.Add(enemy);
                int groupSize = 1;
            
                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                
                    for (int i = 0; i < 4; i++)
                    {
                        int newRow = current.x + dx[i];
                        int newCol = current.y + dy[i];
                        Vector2Int neighbor = new Vector2Int(newRow, newCol);
                    
                        if (newRow >= 0 && newRow < currentRow && newCol >= 0 && newCol < currentCol &&
                            IsEnemyVisibleForHint(newRow, newCol) && !visited.Contains(neighbor))
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
            savedMaxGroup = maxGroup;
        }
        
        // 随机选择一个hint
        int selectedIndex;
        if (isFakeTile)
        {
            List<int> guaranteedFakeIndices = new List<int>();
            for (int i = 0; i < availableHintsGuaranteedFake.Count; i++)
            {
                if (availableHintsGuaranteedFake[i])
                    guaranteedFakeIndices.Add(i);
            }
            if (guaranteedFakeIndices.Count > 0)
                selectedIndex = guaranteedFakeIndices[Random.Range(0, guaranteedFakeIndices.Count)];
            else
                selectedIndex = Random.Range(0, availableHints.Count);
        }
        else
        {
            selectedIndex = Random.Range(0, availableHints.Count);
        }
        string selectedHint = availableHints[selectedIndex];
        string selectedHintKey = availableHintsKey[selectedIndex];
        usedHints.Add(selectedHint);
        
        // 存储key
        Vector2Int hintPos = new Vector2Int(row, col);
        hintKeys[hintPos] = selectedHintKey;
        
        // 计算并存储这个hint的相关位置（使用key）
        HashSet<Vector2Int> relatedPositions = CalculateHintRelatedPositions(row, col, selectedHintKey, savedMaxGroup);
        hintRelatedPositions[hintPos] = relatedPositions;
        
        return selectedHint;
    }
    
    // 计算hint的相关位置（使用key而不是localized文本）
    private HashSet<Vector2Int> CalculateHintRelatedPositions(int hintRow, int hintCol, string selectedHintKey, HashSet<Vector2Int> maxGroup)
    {
        HashSet<Vector2Int> relatedPositions = new HashSet<Vector2Int>();
        
        // 通过比较key字符串来判断hint类型
        // 检查是否是3x3周围hint
        if (selectedHintKey.Contains("3x3"))
        {
            // 3x3周围：只影响hint周围3x3的格子
            for (int r = hintRow - 1; r <= hintRow + 1; r++)
            {
                for (int c = hintCol - 1; c <= hintCol + 1; c++)
                {
                    if (r >= 0 && r < currentRow && c >= 0 && c < currentCol)
                    {
                        relatedPositions.Add(new Vector2Int(r, c));
                    }
                }
            }
            return relatedPositions;
        }
        
        // 检查是否是行hint
        if (selectedHintKey.Contains("This row"))
        {
            // 行hint：影响这一行的所有格子
            for (int c = 0; c < currentCol; c++)
            {
                relatedPositions.Add(new Vector2Int(hintRow, c));
            }
            return relatedPositions;
        }
        
        // 检查是否是列hint
        if (selectedHintKey.Contains("This column"))
        {
            // 列hint：影响这一列的所有格子
            for (int r = 0; r < currentRow; r++)
            {
                relatedPositions.Add(new Vector2Int(r, hintCol));
            }
            return relatedPositions;
        }
        
        // 检查是否是church周围hint
        if (selectedHintKey.Contains("adjacent to church"))
        {
            // church周围hint：影响所有church周围的位置
            List<Vector2Int> churches = new List<Vector2Int>();
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
            
            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };
            foreach (Vector2Int church in churches)
            {
                for (int i = 0; i < 4; i++)
                {
                    int newRow = church.x + dx[i];
                    int newCol = church.y + dy[i];
                    if (newRow >= 0 && newRow < currentRow && newCol >= 0 && newCol < currentCol)
                    {
                        relatedPositions.Add(new Vector2Int(newRow, newCol));
                    }
                }
            }
            return relatedPositions;
        }
        
        // 检查是否是corner hint
        if (selectedHintKey.Contains("four corners"))
        {
            // corner hint：影响四个角落
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
                    relatedPositions.Add(corner);
                }
            }
            return relatedPositions;
        }
        
        // 检查是否是group hint
        if (selectedHintKey.Contains("group") || selectedHintKey.Contains("adjacent to each other"))
        {
            // group hint：预计算时包含所有方块，相关性在hover时动态检查
            for (int r = 0; r < currentRow; r++)
            {
                for (int c = 0; c < currentCol; c++)
                {
                    relatedPositions.Add(new Vector2Int(r, c));
                }
            }
            return relatedPositions;
        }
        
        // 其他hint（左右比较、上下比较、行数、列数等）：影响所有位置
        for (int r = 0; r < currentRow; r++)
        {
            for (int c = 0; c < currentCol; c++)
            {
                relatedPositions.Add(new Vector2Int(r, c));
            }
        }
        
        return relatedPositions;
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
    
    // 获取所有已翻开的hint列表
    public List<Vector2Int> GetAllRevealedHints()
    {
        List<Vector2Int> revealedHints = new List<Vector2Int>();
        foreach (var kvp in hintContents)
        {
            Vector2Int hintPos = kvp.Key;
            if (isRevealed[hintPos.x, hintPos.y])
            {
                revealedHints.Add(hintPos);
            }
        }
        return revealedHints;
    }
    
    // 重置所有hint的大小和Canvas sort order
    public void ResetAllHints()
    {
        List<Vector2Int> allRevealedHints = GetAllRevealedHints();
        foreach (Vector2Int hintPos in allRevealedHints)
        {
            Tile hintTile = GetTile(hintPos.x, hintPos.y);
            if (hintTile != null && hintTile.IsRevealed())
            {
                // 停止之前的动画（如果有）
                hintTile.transform.DOKill();
                
                // 缩小回原始大小并恢复Canvas sort order
                hintTile.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutQuad);
                hintTile.ResetCanvasSortOrder();
            }
        }
    }
    
    // 获取与指定位置相关的已翻开hint列表
    public List<Vector2Int> GetRelatedHints(int row, int col)
    {
        List<Vector2Int> relatedHints = new List<Vector2Int>();
        Vector2Int hoverPos = new Vector2Int(row, col);
        
        // 遍历所有已翻开的hint
        foreach (var kvp in hintContents)
        {
            Vector2Int hintPos = kvp.Key;
            
            // 只检查已翻开的hint
            if (!isRevealed[hintPos.x, hintPos.y])
                continue;
            
            // 获取hint的key
            string hintKey = "";
            if (hintKeys.ContainsKey(hintPos))
            {
                hintKey = hintKeys[hintPos];
            }
            
            // 检查是否是group hint（使用key而不是hintText）
            if (hintKey.Contains("group") || hintKey.Contains("adjacent to each other"))
            {
                // group hint：检查hover的块是否与翻开的敌人相邻
                int[] dx = { 0, 0, 1, -1 };
                int[] dy = { 1, -1, 0, 0 };
                
                // 检查hover位置的四个邻居
                for (int i = 0; i < 4; i++)
                {
                    int newRow = row + dx[i];
                    int newCol = col + dy[i];
                    if (newRow >= 0 && newRow < currentRow && newCol >= 0 && newCol < currentCol)
                    {
                        // 如果邻居是已翻开且对 hint 可见的敌人，就认为相关（迷雾下不计）
                        if (isRevealed[newRow, newCol] && IsEnemyVisibleForHint(newRow, newCol))
                        {
                            relatedHints.Add(hintPos);
                            break;
                        }
                    }
                }
            }
            else
            {
                // 其他hint：检查hover位置是否在相关位置集合中
                if (hintRelatedPositions.ContainsKey(hintPos))
                {
                    HashSet<Vector2Int> relatedPositions = hintRelatedPositions[hintPos];
                    if (relatedPositions.Contains(hoverPos))
                    {
                        relatedHints.Add(hintPos);
                    }
                }
            }
        }
        
        return relatedHints;
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
    
    /// <summary> 是否为迷雾格子（2x2 随机区域） </summary>
    public bool IsMistTile(int row, int col)
    {
        if (isMistTile == null || row < 0 || row >= currentRow || col < 0 || col >= currentCol)
            return false;
        return isMistTile[row, col];
    }
    
    /// <summary> 是否为寒冰格子（3x3 随机区域） </summary>
    public bool IsFrozenTile(int row, int col)
    {
        if (isFrozenTile == null || row < 0 || row >= currentRow || col < 0 || col >= currentCol)
            return false;
        return isFrozenTile[row, col];
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
    
    /// <summary>磁铁：允许翻开当前不可达的相邻格（由磁铁效果触发）。</summary>
    public void EnsureTileRevealableForMagnet(int row, int col)
    {
        if (row < 0 || row >= currentRow || col < 0 || col >= currentCol) return;
        Vector2Int p = new Vector2Int(row, col);
        if (!revealableTiles.Contains(p))
            revealableTiles.Add(p);
    }
    
    public CardType ResolveChameleonMimic(int row, int col)
    {
        int[] dr = { 0, 0, 1, -1 };
        int[] dc = { 1, -1, 0, 0 };
        Dictionary<CardType, int> freq = new Dictionary<CardType, int>();
        for (int i = 0; i < 4; i++)
        {
            int nr = row + dr[i], nc = col + dc[i];
            if (nr < 0 || nr >= currentRow || nc < 0 || nc >= currentCol) continue;
            CardType t = cardTypes[nr, nc];
            if (!freq.ContainsKey(t)) freq[t] = 0;
            freq[t]++;
        }
        if (freq.Count == 0) return CardType.Blank;
        int max = 0;
        foreach (var v in freq.Values) max = Mathf.Max(max, v);
        CardType best = CardType.Blank;
        int bestOrder = 99999;
        foreach (var kvp in freq)
        {
            if (kvp.Value < max) continue;
            int ord = CardInfoManager.Instance != null
                ? CardInfoManager.Instance.GetCardCsvOrderIndex(kvp.Key)
                : 9999;
            if (ord < bestOrder)
            {
                bestOrder = ord;
                best = kvp.Key;
            }
        }
        return best;
    }

    public void SetCardTypeForChameleon(int row, int col, CardType newType)
    {
        if (row < 0 || row >= currentRow || col < 0 || col >= currentCol) return;
        cardTypes[row, col] = newType;
        if (tiles != null && tiles[row, col] != null)
            tiles[row, col].UpdateType(newType);
    }

    public bool TryRelocateGhostBossOnReveal(int revealedRow, int revealedCol)
    {
        if (revealedRow < 0 || revealedRow >= currentRow || revealedCol < 0 || revealedCol >= currentCol)
            return false;

        if (cardTypes[revealedRow, revealedCol] != CardType.Ghost)
            return false;

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (isRevealed[row, col])
                    continue;

                CardType t = cardTypes[row, col];
                if (t == CardType.Hint)
                    continue;
                if (IsEnemyCard(row, col))
                    continue;
                candidates.Add(new Vector2Int(row, col));
            }
        }

        if (candidates.Count == 0)
            return false;

        Vector2Int targetPos = candidates[Random.Range(0, candidates.Count)];
        CardType targetType = cardTypes[targetPos.x, targetPos.y];

        // ghost 迁移到目标未翻开安全格
        cardTypes[targetPos.x, targetPos.y] = CardType.Ghost;
        cardTypes[revealedRow, revealedCol] = targetType;

        if (tiles != null)
        {
            if (tiles[targetPos.x, targetPos.y] != null)
                tiles[targetPos.x, targetPos.y].UpdateType(CardType.Ghost);
            if (tiles[revealedRow, revealedCol] != null)
                tiles[revealedRow, revealedCol].UpdateType(targetType);
        }

        // 当前格已被翻开，若变成了需要朝向目标的牌，刷新箭头
        if (targetType == CardType.Sign || targetType == CardType.Bell)
            UpdateSignArrows();

        return true;
    }

    public void ResetRevealedHintsForGhostBoss()
    {
        if (cardTypes == null || isRevealed == null) return;

        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] != CardType.Hint || !isRevealed[row, col])
                    continue;

                Vector2Int pos = new Vector2Int(row, col);
                isRevealed[row, col] = false;
                revealedTiles.Remove(pos);
                unrevealedTiles.Add(pos);

                if (tiles != null && tiles[row, col] != null)
                {
                    tiles[row, col].SetRevealed(false);
                }
            }
        }

        // hint 重新揭示时应重新抽文案与相关位置
        hintContents.Clear();
        hintKeys.Clear();
        hintRelatedPositions.Clear();
        usedHints.Clear();

        RefreshRevealableTilesFromPlayerBFS();
    }
    
    /// <summary>
    /// 不改变牌面布局，将所有格恢复为未翻开，再按 <see cref="GenerateBoard"/> 的规则重新翻开玩家格与教堂（PoliceStation）。
    /// 同时清空 hint 缓存并重建 reveal 集合，等同于撤销本局已翻牌（保留初始即翻开的格）。
    /// </summary>
    public void ResetAllTilesUnrevealedThenInitialRevealsOnly()
    {
        if (cardTypes == null || isRevealed == null || tiles == null) return;
        
        revealedTiles.Clear();
        unrevealedTiles.Clear();
        revealableTiles.Clear();
        hintRevealedCountExcludingFamiliarStreet = 0;
        hintContents.Clear();
        hintKeys.Clear();
        hintRelatedPositions.Clear();
        usedHints.Clear();
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.mainGameData.doublebladeNextRevealPending = false;
            GameManager.Instance.mainGameData.doublebladeStunThisEnemyReveal = false;
        }
        
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                isRevealed[row, col] = false;
                if (tiles[row, col] != null)
                    tiles[row, col].SetRevealed(false);
            }
        }
        
        void RevealCell(int row, int col)
        {
            Vector2Int pos = new Vector2Int(row, col);
            isRevealed[row, col] = true;
            revealedTiles.Add(pos);
            if (tiles[row, col] != null)
                tiles[row, col].SetRevealed(true);
        }
        
        Vector2Int playerPos = GetPlayerPosition();
        if (playerPos.x >= 0 && playerPos.y >= 0)
            RevealCell(playerPos.x, playerPos.y);
        
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.PoliceStation)
                    RevealCell(row, col);
            }
        }
        
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (!isRevealed[row, col])
                    unrevealedTiles.Add(new Vector2Int(row, col));
            }
        }
        
        // 未翻开格需重新显示迷雾（翻开时 FadeOutMist 会关掉 mist）
        if (isMistTile != null)
        {
            for (int row = 0; row < currentRow; row++)
            {
                for (int col = 0; col < currentCol; col++)
                {
                    if (tiles[row, col] != null)
                        tiles[row, col].SetMist(isMistTile[row, col]);
                }
            }
        }
        
        RefreshRevealableTilesFromPlayerBFS();
        UpdateSignArrows();
    }
    
    /// <summary> 对 hint 可见的敌人（迷雾格子下的敌人不被 hint 观测） </summary>
    private bool IsEnemyVisibleForHint(int row, int col)
    {
        return IsEnemyCard(row, col) && !IsMistTile(row, col);
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
        
        if (cardTypes == null || isRevealed == null)
        {
            return 0;
        }
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
        
        if (cardTypes == null || isRevealed == null)
        {
            return 0;
        }
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
        if (cardTypes == null || isRevealed == null)
        {
            return 0;
        }
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
        
        if (cardTypes == null || isRevealed == null)
        {
            return 0;
        }
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
        
        if (cardTypes == null || isRevealed == null)
        {
            return 0;
        }
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
        
        if (cardTypes == null || isRevealed == null)
        {
            return 0;
        }
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
    
    /// <summary>
    /// 揭示本关所有 hint 格子（用于 revealHint 模式）
    /// </summary>
    public void RevealAllHintTiles()
    {
        if (cardTypes == null || isRevealed == null) return;
        List<Vector2Int> toReveal = new List<Vector2Int>();
        for (int row = 0; row < currentRow; row++)
        {
            for (int col = 0; col < currentCol; col++)
            {
                if (cardTypes[row, col] == CardType.Hint && !isRevealed[row, col])
                    toReveal.Add(new Vector2Int(row, col));
            }
        }
        foreach (var pos in toReveal)
            RevealTile(pos.x, pos.y, true, false, false, false, true);
    }
    
    // 获取未翻开的敌人数量
    public int GetUnrevealedEnemyCount()
    {
        int count = 0;
        
        if (cardTypes == null || isRevealed == null)
        {
            return 0;
        }
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
    
    /// <summary> 寒冰模式：更新 player 格上的 frozenData 文本 </summary>
    public void UpdatePlayerFrozenDataText()
    {
        Vector2Int pos = GetPlayerPosition();
        if (pos.x < 0) return;
        Tile playerTile = GetTile(pos.x, pos.y);
        if (playerTile == null || GameManager.Instance == null) return;
        var sceneInfo = GameManager.Instance.GetCurrentSceneInfo();
        // frozenNew 不显示 player 上的数量文本
        bool isFrozenScene = sceneInfo != null && sceneInfo.HasType("frozen");
        playerTile.UpdateFrozenDataText(GameManager.Instance.GetFrozenRevealedCount(), GameManager.Instance.frozenDamageThreshold, isFrozenScene);
    }
    
    /// <summary> 本关初始已揭示且位于寒冰格上的教堂数量，用于计入 frozenRevealedCount </summary>
    public int GetInitialRevealedFrozenCount()
    {
        if (isRevealed == null || isFrozenTile == null) return 0;
        int n = 0;
        for (int r = 0; r < currentRow; r++)
            for (int c = 0; c < currentCol; c++)
                if (isRevealed[r, c] && isFrozenTile[r, c] && cardTypes[r, c] == CardType.PoliceStation)
                    n++;
        return n;
    }
    
    /// <summary> 竞速模式：仅 player 格显示 progressBar，其余格隐藏 </summary>
    public void UpdatePlayerProgressBarVisibility()
    {
        if (tiles == null) return;
        Vector2Int playerPos = GetPlayerPosition();
        var sceneInfo = GameManager.Instance != null ? GameManager.Instance.GetCurrentSceneInfo() : null;
        bool isSpeedScene = sceneInfo != null && sceneInfo.HasType("speed");
        for (int r = 0; r < currentRow; r++)
            for (int c = 0; c < currentCol; c++)
                if (tiles[r, c] != null && tiles[r, c].progressBar != null)
                    tiles[r, c].progressBar.gameObject.SetActive(false);
        if (playerPos.x < 0) return;
        Tile playerTile = GetTile(playerPos.x, playerPos.y);
        if (playerTile == null || playerTile.progressBar == null) return;
        playerTile.progressBar.gameObject.SetActive(isSpeedScene);
        int cellCount = currentRow * currentCol;
        if (isSpeedScene && sceneInfo != null && GameManager.ComputeSpeedModeCountdownSeconds(sceneInfo, cellCount) > 0f)
            playerTile.progressBar.SetProgress(1f);
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
    
    // 检测快捷键输入（仅在 Unity Editor 中运行）
#if UNITY_EDITOR
    private void Update()
    {
        
        // 检测 Shift + 数字键
        if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && GameManager.Instance.isCheat)
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
#endif
    
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
        ApplyBoardParentScale(currentRow, currentCol);
        
        // 初始化数组
        tiles = new Tile[currentRow, currentCol];
        cardTypes = new CardType[currentRow, currentCol];
        isRevealed = new bool[currentRow, currentCol];
        
        // 清空相关集合
        revealedTiles.Clear();
        unrevealedTiles.Clear();
        revealableTiles.Clear();
        hintContents.Clear();
        hintKeys.Clear();
        usedHints.Clear();
        hintRelatedPositions.Clear();
        hintRevealedCountExcludingFamiliarStreet = 0;
        
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
        string rowHint = LocalizationHelper.GetLocalizedString("This row has {rowEnemies:plural:{} enemy|{} enemies}", new object[] { rowEnemies });;
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
                if (isMistTile != null) tile.SetMist(isMistTile[row, col]);
                if (isFrozenTile != null) tile.SetFrozen(isFrozenTile[row, col]);
                
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

