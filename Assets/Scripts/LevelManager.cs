using UnityEngine;
using System.Collections.Generic;

public class LevelManager : Singleton<LevelManager>
{
    private List<LevelInfo> levels = new List<LevelInfo>();
    
    private void Awake()
    {
        if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        LoadLevels();
    }
    
    private void LoadLevels()
    {
        if (CSVLoader.Instance != null)
        {
            levels = CSVLoader.Instance.levelInfos;
        }
    }
    
    // 获取指定关卡的信息（关卡编号从1开始）
    public LevelInfo GetLevelInfo(int levelNumber)
    {
        if (levelNumber < 1 || levelNumber > levels.Count)
        {
            Debug.LogWarning($"Level {levelNumber} not found, returning last level or default");
            if (levels.Count > 0)
            {
                return levels[levels.Count - 1];
            }
            // 返回默认值
            return new LevelInfo { enemyCount = 1, col = 5, row = 5 };
        }
        
        return levels[levelNumber - 1];
    }
    
    // 获取当前关卡的信息
    public LevelInfo GetCurrentLevelInfo()
    {
        if (GameManager.Instance == null)
        {
            return GetLevelInfo(1);
        }
        
        int currentLevel = GameManager.Instance.mainGameData.currentLevel;
        return GetLevelInfo(currentLevel);
    }
    
    // 计算玩家位置（尽量最中间，如果是偶数则往下一行）
    public Vector2Int GetPlayerPosition(int row, int col)
    {
        // 列位置：尽量中间（向下取整）
        int centerCol = col / 2;
        
        // 行位置：尽量中间，如果是偶数则往下一行
        // 对于奇数行：中间位置是 (row-1)/2
        // 对于偶数行：中间偏下位置是 row/2（已经是向下取整的结果）
        int centerRow = row / 2;
        
        return new Vector2Int(centerRow, centerCol);
    }
    
    // 获取关卡总数
    public int GetTotalLevels()
    {
        return levels.Count;
    }
}

