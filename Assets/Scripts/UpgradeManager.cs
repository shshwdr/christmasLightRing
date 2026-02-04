using UnityEngine;
using System.Collections.Generic;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance;
    
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
    }
    
    // 初始化升级项（start为true的升级项在初始即拥有）
    public void InitializeUpgrades()
    {
        if (CSVLoader.Instance == null || GameManager.Instance == null) return;
        
        MainGameData data = GameManager.Instance.mainGameData;
        data.ownedUpgrades.Clear();
        
        foreach (var kvp in CSVLoader.Instance.upgradeDict)
        {
            UpgradeInfo upgradeInfo = kvp.Value;
            if (upgradeInfo.start == 1) // start为1表示初始拥有
            {
                data.ownedUpgrades.Add(upgradeInfo.identifier);
                // 应用初始升级项的效果（如AsceticVow）
                OnUpgradeObtained(upgradeInfo.identifier);
            }
        }
    }
    
    // 检查是否拥有某个升级项
    public bool HasUpgrade(string identifier)
    {
        if (GameManager.Instance == null) return false;
        return GameManager.Instance.mainGameData.ownedUpgrades.Contains(identifier);
    }
    
    // 获取升级项信息
    public UpgradeInfo GetUpgradeInfo(string identifier)
    {
        if (CSVLoader.Instance == null) return null;
        if (CSVLoader.Instance.upgradeDict.ContainsKey(identifier))
        {
            return CSVLoader.Instance.upgradeDict[identifier];
        }
        return null;
    }
    
    // 获取升级项的value值
    public int GetUpgradeValue(string identifier)
    {
        UpgradeInfo info = GetUpgradeInfo(identifier);
        return info != null ? info.value : 0;
    }
    
    // chaseGrinchGiveGift: 每次用light赶走一个grinch给value的coin
    public void OnChaseGrinchWithLight()
    {
        if (!HasUpgrade("chaseGrinchGiveGift")) return;
        
        int value = GetUpgradeValue("chaseGrinchGiveGift");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.mainGameData.coins += value;
            GameManager.Instance.ShowFloatingText("coin", value);
            GameManager.Instance.uiManager?.UpdateUI();
            GameManager.Instance.uiManager?.TriggerUpgradeAnimation("chaseGrinchGiveGift");
        }
    }
    
    // knownEvilPays: 翻开bell的时候，每个已经reveal的grinch给与value个gift
    public void OnBellRevealed()
    {
        if (!HasUpgrade("knownEvilPays")) return;
        
        int value = GetUpgradeValue("knownEvilPays");
        if (GameManager.Instance == null || GameManager.Instance.boardManager == null) return;
        
        List<Vector2Int> revealedEnemies = new List<Vector2Int>();
        int rows = GameManager.Instance.boardManager.GetCurrentRow();
        int cols = GameManager.Instance.boardManager.GetCurrentCol();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (GameManager.Instance.boardManager.IsEnemyCard(row, col) &&
                    GameManager.Instance.boardManager.IsRevealed(row, col))
                {
                    revealedEnemies.Add(new Vector2Int(row, col));
                }
            }
        }
        
        int totalGifts = revealedEnemies.Count * value;
        // 应用lastChance倍数
        int multiplier = GetGiftMultiplier();
        totalGifts *= multiplier;
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.mainGameData.gifts += totalGifts;
            if (totalGifts > 0)
            {
                GameManager.Instance.ShowFloatingText("gift", totalGifts);
            }
            GameManager.Instance.uiManager?.UpdateUI();
            GameManager.Instance.uiManager?.TriggerUpgradeAnimation("knownEvilPays");
        }
    }
    
    // churchRing: when find the ring bell, all grinch adjacent to church would reveal itself
    public void OnBellFound()
    {
        if (!HasUpgrade("churchRing")) return;
        
        GameManager.Instance?.uiManager?.TriggerUpgradeAnimation("churchRing");
        
        if (GameManager.Instance == null || GameManager.Instance.boardManager == null) return;
        
        // 找到所有church（PoliceStation）的位置
        List<Vector2Int> churches = new List<Vector2Int>();
        int rows = GameManager.Instance.boardManager.GetCurrentRow();
        int cols = GameManager.Instance.boardManager.GetCurrentCol();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (GameManager.Instance.boardManager.GetCardType(row, col) == CardType.PoliceStation)
                {
                    churches.Add(new Vector2Int(row, col));
                }
            }
        }
        
        // 找到所有与church相邻的grinch并reveal
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        
        foreach (Vector2Int church in churches)
        {
            for (int i = 0; i < 4; i++)
            {
                int newRow = church.x + dx[i];
                int newCol = church.y + dy[i];
                
                if (newRow >= 0 && newRow < GameManager.Instance.boardManager.GetCurrentRow() && 
                    newCol >= 0 && newCol < GameManager.Instance.boardManager.GetCurrentCol())
                {
                    if (GameManager.Instance.boardManager.IsEnemyCard(newRow, newCol) &&
                        !GameManager.Instance.boardManager.IsRevealed(newRow, newCol))
                    {
                        // 使用churchRing效果reveal，等同于用light翻开（但不消耗light）
                        GameManager.Instance.RevealTileWithChurchRing(newRow, newCol);
                    }
                }
            }
        }
    }
    
    // familiarSteet: at the beginning of a level, randomly reveal a hint tile
    // 注意：所有reveal的tile，逻辑和policeStation一样，如果不和player相邻的话，不会拓展周围的格子为revealable
    public void OnLevelStart()
    {
        if (!HasUpgrade("familiarSteet")) return;
        
        if (GameManager.Instance == null || GameManager.Instance.boardManager == null) return;
        
        // 检查是否是第一关或第二关，且tutorialForceBoard开启
        int currentLevel = GameManager.Instance.mainGameData.currentLevel;
        bool tutorialForceBoard = TutorialManager.Instance != null && TutorialManager.Instance.tutorialForceBoard;
        bool isLevel1 = currentLevel == 1 && tutorialForceBoard;
        bool isLevel2 = currentLevel == 2 && tutorialForceBoard;
        
        // 找到所有未reveal的hint tile
        List<Vector2Int> hintTiles = new List<Vector2Int>();
        int rows = GameManager.Instance.boardManager.GetCurrentRow();
        int cols = GameManager.Instance.boardManager.GetCurrentCol();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (GameManager.Instance.boardManager.GetCardType(row, col) == CardType.Hint &&
                    !GameManager.Instance.boardManager.IsRevealed(row, col))
                {
                    hintTiles.Add(new Vector2Int(row, col));
                }
            }
        }
        
        if (hintTiles.Count > 0)
        {
            Vector2Int selectedHint;
            
            // 如果是第一关或第二关且tutorialForceBoard开启，优先选择第一个hint（教程中的第一个hint）
            if (isLevel1 || isLevel2)
            {
                Vector2Int playerPos = GameManager.Instance.boardManager.GetPlayerPosition();
                Vector2Int tutorialHintPos = Vector2Int.zero;
                
                if (isLevel1)
                {
                    // 第一关：第一个hint在玩家上方
                    tutorialHintPos = new Vector2Int(playerPos.x - 1, playerPos.y);
                }
                else if (isLevel2)
                {
                    // 第二关：第一个hint在玩家下方
                    tutorialHintPos = new Vector2Int(playerPos.x + 1, playerPos.y);
                }
                
                // 检查教程中的第一个hint是否存在且未reveal
                if (hintTiles.Contains(tutorialHintPos))
                {
                    selectedHint = tutorialHintPos;
                }
                else
                {
                    // 如果教程中的第一个hint不存在，随机选择一个
                    selectedHint = hintTiles[Random.Range(0, hintTiles.Count)];
                }
            }
            else
            {
                // 其他情况：随机选择一个hint tile
                selectedHint = hintTiles[Random.Range(0, hintTiles.Count)];
            }
            
            // 直接reveal hint tile（BoardManager的RevealTile方法会自动处理是否拓展周围的格子，逻辑和policeStation一样）
            GameManager.Instance.boardManager.RevealTile(selectedHint.x, selectedHint.y);
            GameManager.Instance.uiManager?.TriggerUpgradeAnimation("familiarSteet");
        }
    }
    
    // peacefulNight: when reveal the last tile, heal 1 hp
    public void OnLastTileRevealed()
    {
        if (!HasUpgrade("peacefulNight")) return;
        
        if (GameManager.Instance == null) return;
        
        GameManager.Instance.AddHealth(1, false);
        GameManager.Instance.uiManager?.TriggerUpgradeAnimation("peacefulNight");
    }
    
    // greedIsGood: when reveal the last safe tile, heal 1 hp（safe tile指的是除了grinch之外的tile）
    public void OnLastSafeTileRevealed()
    {
        if (!HasUpgrade("greedIsGood")) return;
        
        if (GameManager.Instance == null) return;
        
        int value = GetUpgradeValue("greedIsGood");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.mainGameData.coins += value;
            GameManager.Instance.ShowFloatingText("coin", value);
            GameManager.Instance.uiManager?.UpdateUI();
            GameManager.Instance.uiManager?.TriggerUpgradeAnimation("greedIsGood");
        }
    }
    
    // patternRecognition: when you open value safe tile in sequence, get a gift（并清空sequence，也就是重新从0计数）
    public void OnSafeTileRevealed()
    {
        if (!HasUpgrade("patternRecognition")) return;
        
        if (GameManager.Instance == null) return;
        
        MainGameData mainData = GameManager.Instance.mainGameData;
        mainData.patternRecognitionSequence++;
        
        int value = GetUpgradeValue("patternRecognition");
        if (mainData.patternRecognitionSequence >= value)
        {
            int giftAmount = 1;
            // 应用lastChance倍数
            int multiplier = GetGiftMultiplier();
            giftAmount *= multiplier;
            
            mainData.gifts += giftAmount;
            if (giftAmount > 0)
            {
                GameManager.Instance.ShowFloatingText("gift", giftAmount);
            }
            mainData.patternRecognitionSequence = 0; // 清空sequence，重新从0计数
            GameManager.Instance.uiManager?.UpdateUI();
            GameManager.Instance.uiManager?.TriggerUpgradeAnimation("patternRecognition");
        }
    }
    
    // patternRecognition: 当翻开非safe tile时，重置sequence
    public void OnNonSafeTileRevealed()
    {
        if (!HasUpgrade("patternRecognition")) return;
        
        if (GameManager.Instance == null) return;
        
        GameManager.Instance.mainGameData.patternRecognitionSequence = 0;
    }
    
    // lastChance: when you only have 1 hp, you get gift doubles
    public int GetGiftMultiplier()
    {
        if (!HasUpgrade("lastChance")) return 1;
        
        if (GameManager.Instance == null) return 1;
        
        if (GameManager.Instance.mainGameData.health == 1)
        {
            return 2; // gift翻倍
        }
        return 1;
    }
    
    // steadyHand: when you light on a safe tile, reveal an adjacent safe tile
    public void OnLightRevealSafeTile(int row, int col)
    {
        if (!HasUpgrade("steadyHand")) return;
        
        if (GameManager.Instance == null || GameManager.Instance.boardManager == null) return;
        
        // 检查相邻的safe tile（不是Enemy的tile）
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        
        List<Vector2Int> adjacentSafeTiles = new List<Vector2Int>();
        for (int i = 0; i < 4; i++)
        {
            int newRow = row + dx[i];
            int newCol = col + dy[i];
            
            if (newRow >= 0 && newRow < GameManager.Instance.boardManager.GetCurrentRow() && 
                newCol >= 0 && newCol < GameManager.Instance.boardManager.GetCurrentCol())
            {
                if (!GameManager.Instance.boardManager.IsEnemyCard(newRow, newCol) && 
                    !GameManager.Instance.boardManager.IsRevealed(newRow, newCol))
                {
                    adjacentSafeTiles.Add(new Vector2Int(newRow, newCol));
                }
            }
        }
        
        if (adjacentSafeTiles.Count > 0)
        {
            // 随机选择一个相邻的safe tile并reveal
            Vector2Int selectedTile = adjacentSafeTiles[Random.Range(0, adjacentSafeTiles.Count)];
            GameManager.Instance.boardManager.RevealTile(selectedTile.x, selectedTile.y,false);
            GameManager.Instance.uiManager?.TriggerUpgradeAnimation("steadyHand");
        }
    }
    
    // lateMending: when reveal a grinch without using light, reveal a safe tile adjacent
    public void OnRevealGrinchWithoutLight(int row, int col)
    {
        if (!HasUpgrade("lateMending")) return;
        
        if (GameManager.Instance == null || GameManager.Instance.boardManager == null) return;
        
        // 检查相邻的safe tile（不是Enemy的tile）
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        
        List<Vector2Int> adjacentSafeTiles = new List<Vector2Int>();
        for (int i = 0; i < 4; i++)
        {
            int newRow = row + dx[i];
            int newCol = col + dy[i];
            
            if (newRow >= 0 && newRow < GameManager.Instance.boardManager.GetCurrentRow() && 
                newCol >= 0 && newCol < GameManager.Instance.boardManager.GetCurrentCol())
            {
                if (!GameManager.Instance.boardManager.IsEnemyCard(newRow, newCol) && 
                    !GameManager.Instance.boardManager.IsRevealed(newRow, newCol))
                {
                    adjacentSafeTiles.Add(new Vector2Int(newRow, newCol));
                }
            }
        }
        
        if (adjacentSafeTiles.Count > 0)
        {
            // 随机选择一个相邻的safe tile并reveal
            Vector2Int selectedTile = adjacentSafeTiles[Random.Range(0, adjacentSafeTiles.Count)];
            GameManager.Instance.boardManager.RevealTile(selectedTile.x, selectedTile.y);
            GameManager.Instance.uiManager?.TriggerUpgradeAnimation("lateMending");
        }
    }
    
    // LastLight: if flashLight more than one when go to next level, keep one light to the next level
    public int GetFlashlightForNextLevel(int currentFlashlights)
    {
        if (!HasUpgrade("LastLight")) return 0;
        
        // 如果手电筒数量大于1，保留1个到下一关
        if (currentFlashlights >= 1)
        {
            return 1;
        }
        return 0;
    }
    
    // AsceticVow: 获得时血量上限-2，商店回血时多回1点，卖出时血量上限+2
    public void OnUpgradeObtained(string identifier)
    {
        if (identifier == "AsceticVow" && GameManager.Instance != null)
        {
            GameManager.Instance.mainGameData.maxHealth -= 2;
            // 如果当前血量超过新的上限，调整血量
            if (GameManager.Instance.mainGameData.health > GameManager.Instance.mainGameData.maxHealth)
            {
                GameManager.Instance.mainGameData.health = GameManager.Instance.mainGameData.maxHealth;
            }
            GameManager.Instance.uiManager?.UpdateUI();
        }
    }
    
    // AsceticVow: 卖出时血量上限+2
    public void OnUpgradeSold(string identifier)
    {
        if (identifier == "AsceticVow" && GameManager.Instance != null)
        {
            GameManager.Instance.mainGameData.maxHealth += 2;
            GameManager.Instance.uiManager?.UpdateUI();
        }
    }
    
    // JingleGuide: 卖掉的时候翻开铃铛，会正常触发翻开铃铛的其他效果
    public void OnJingleGuideSold()
    {
        if (GameManager.Instance == null || GameManager.Instance.boardManager == null) return;
        
        // 找到所有未翻开的铃铛
        List<Vector2Int> bells = new List<Vector2Int>();
        int rows = GameManager.Instance.boardManager.GetCurrentRow();
        int cols = GameManager.Instance.boardManager.GetCurrentCol();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (GameManager.Instance.boardManager.GetCardType(row, col) == CardType.Bell &&
                    !GameManager.Instance.boardManager.IsRevealed(row, col))
                {
                    bells.Add(new Vector2Int(row, col));
                }
            }
        }
        
        // 如果有铃铛，翻开第一个（等同于玩家自己翻开）
        if (bells.Count > 0)
        {
            Vector2Int bellPos = bells[0];
            GameManager.Instance.boardManager.RevealTile(bellPos.x, bellPos.y);
        }
    }
    
    // Spotter: 卖掉的时候翻开一个随机敌人并眩晕它（等同于用灯照射着翻开）
    public void OnSpotterSold()
    {
        if (GameManager.Instance == null || GameManager.Instance.boardManager == null) return;
        
        // 找到所有未翻开的敌人
        List<Vector2Int> enemies = new List<Vector2Int>();
        int rows = GameManager.Instance.boardManager.GetCurrentRow();
        int cols = GameManager.Instance.boardManager.GetCurrentCol();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (GameManager.Instance.boardManager.IsEnemyCard(row, col) &&
                    !GameManager.Instance.boardManager.IsRevealed(row, col))
                {
                    enemies.Add(new Vector2Int(row, col));
                }
            }
        }
        
        // 如果有敌人，随机选择一个并用灯照射翻开
        if (enemies.Count > 0)
        {
            Vector2Int enemyPos = enemies[Random.Range(0, enemies.Count)];
            // 使用 RevealTileWithFlashlight 方法（等同于用灯照射）
            GameManager.Instance.RevealTileWithFlashlight(enemyPos.x, enemyPos.y);
        }
    }
    
    // Owl: 卖掉的时候逐个翻开所有的hint
    public void OnOwlSold()
    {
        if (GameManager.Instance == null || GameManager.Instance.boardManager == null) return;
        
        // 找到所有未翻开的hint
        List<Vector2Int> hints = new List<Vector2Int>();
        int rows = GameManager.Instance.boardManager.GetCurrentRow();
        int cols = GameManager.Instance.boardManager.GetCurrentCol();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (GameManager.Instance.boardManager.GetCardType(row, col) == CardType.Hint &&
                    !GameManager.Instance.boardManager.IsRevealed(row, col))
                {
                    hints.Add(new Vector2Int(row, col));
                }
            }
        }
        
        // 逐个翻开所有hint
        if (hints.Count > 0)
        {
            GameManager.Instance.StartCoroutine(RevealHintsSequentially(hints));
        }
    }
    
    // 逐个翻开hint的协程
    private System.Collections.IEnumerator RevealHintsSequentially(List<Vector2Int> hints)
    {
        foreach (Vector2Int hintPos in hints)
        {
            GameManager.Instance.boardManager.RevealTile(hintPos.x, hintPos.y);
            yield return new WaitForSeconds(0.2f); // 每个hint之间间隔0.2秒
        }
    }
}

