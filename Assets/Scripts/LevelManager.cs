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
    
    /// <summary>
    /// 检查是否是当前scene的最后一个level
    /// </summary>
    public bool IsLastLevelInScene(string sceneIdentifier)
    {
        if (string.IsNullOrEmpty(sceneIdentifier) || CSVLoader.Instance == null)
        {
            return false;
        }
        
        // 找到当前关卡
        int currentLevel = GameManager.Instance != null ? GameManager.Instance.mainGameData.currentLevel : 1;
        if (currentLevel < 1 || currentLevel > levels.Count)
        {
            return false;
        }
        
        LevelInfo currentLevelInfo = levels[currentLevel - 1];
        
        // 检查当前关卡是否属于指定scene
        if (currentLevelInfo.scene != sceneIdentifier)
        {
            return false;
        }
        
        // 检查下一个关卡是否还属于同一个scene
        if (currentLevel < levels.Count)
        {
            LevelInfo nextLevelInfo = levels[currentLevel];
            // 如果下一个关卡不属于同一个scene，说明当前是最后一个
            return nextLevelInfo.scene != sceneIdentifier;
        }
        
        // 如果已经是最后一个关卡，返回true
        return true;
    }
    
    /// <summary>
    /// 获取指定scene的所有关卡索引（从0开始）
    /// </summary>
    public List<int> GetLevelIndicesForScene(string sceneIdentifier)
    {
        List<int> indices = new List<int>();
        
        if (string.IsNullOrEmpty(sceneIdentifier))
        {
            return indices;
        }
        
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i].scene == sceneIdentifier)
            {
                indices.Add(i);
            }
        }
        
        return indices;
    }
}

