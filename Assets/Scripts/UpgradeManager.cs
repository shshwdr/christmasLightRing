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
        
        GameData data = GameManager.Instance.gameData;
        data.ownedUpgrades.Clear();
        
        foreach (var kvp in CSVLoader.Instance.upgradeDict)
        {
            UpgradeInfo upgradeInfo = kvp.Value;
            if (upgradeInfo.start == 1) // start为1表示初始拥有
            {
                data.ownedUpgrades.Add(upgradeInfo.identifier);
            }
        }
    }
    
    // 检查是否拥有某个升级项
    public bool HasUpgrade(string identifier)
    {
        if (GameManager.Instance == null) return false;
        return GameManager.Instance.gameData.ownedUpgrades.Contains(identifier);
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
            GameManager.Instance.gameData.coins += value;
            GameManager.Instance.uiManager?.UpdateUI();
        }
    }
    
    // knownEvilPays: 翻开bell的时候，每个已经reveal的grinch给与value个gift
    public void OnBellRevealed()
    {
        if (!HasUpgrade("knownEvilPays")) return;
        
        int value = GetUpgradeValue("knownEvilPays");
        if (GameManager.Instance == null || GameManager.Instance.boardManager == null) return;
        
        List<Vector2Int> revealedEnemies = new List<Vector2Int>();
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                if (GameManager.Instance.boardManager.GetCardType(row, col) == CardType.Enemy &&
                    GameManager.Instance.boardManager.IsRevealed(row, col))
                {
                    revealedEnemies.Add(new Vector2Int(row, col));
                }
            }
        }
        
        int totalGifts = revealedEnemies.Count * value;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.gameData.gifts += totalGifts;
            GameManager.Instance.uiManager?.UpdateUI();
        }
    }
    
    // churchRing: when find the ring bell, all grinch adjacent to church would reveal itself
    public void OnBellFound()
    {
        if (!HasUpgrade("churchRing")) return;
        
        if (GameManager.Instance == null || GameManager.Instance.boardManager == null) return;
        
        // 找到所有church（PoliceStation）的位置
        List<Vector2Int> churches = new List<Vector2Int>();
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 5; col++)
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
                
                if (newRow >= 0 && newRow < 5 && newCol >= 0 && newCol < 5)
                {
                    if (GameManager.Instance.boardManager.GetCardType(newRow, newCol) == CardType.Enemy &&
                        !GameManager.Instance.boardManager.IsRevealed(newRow, newCol))
                    {
                        GameManager.Instance.boardManager.RevealTile(newRow, newCol);
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
        
        // 找到所有未reveal的hint tile
        List<Vector2Int> hintTiles = new List<Vector2Int>();
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 5; col++)
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
            // 随机选择一个hint tile
            Vector2Int selectedHint = hintTiles[Random.Range(0, hintTiles.Count)];
            
            // 直接reveal hint tile（BoardManager的RevealTile方法会自动处理是否拓展周围的格子，逻辑和policeStation一样）
            GameManager.Instance.boardManager.RevealTile(selectedHint.x, selectedHint.y);
        }
    }
    
    // peacefulNight: when reveal the last tile, heal 1 hp
    public void OnLastTileRevealed()
    {
        if (!HasUpgrade("peacefulNight")) return;
        
        if (GameManager.Instance == null) return;
        
        GameManager.Instance.gameData.health++;
        if (GameManager.Instance.gameData.health > GameManager.Instance.initialHealth)
        {
            GameManager.Instance.gameData.health = GameManager.Instance.initialHealth;
        }
        GameManager.Instance.uiManager?.UpdateUI();
    }
    
    // greedIsGood: when reveal the last safe tile, heal 1 hp（safe tile指的是除了grinch之外的tile）
    public void OnLastSafeTileRevealed()
    {
        if (!HasUpgrade("greedIsGood")) return;
        
        if (GameManager.Instance == null) return;
        
        int value = GetUpgradeValue("greedIsGood");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.gameData.coins += value;
            GameManager.Instance.uiManager?.UpdateUI();
        }
    }
}

