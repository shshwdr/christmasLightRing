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
        string key = GetSceneKeyForLevels(sceneIdentifier);
        // 检查当前关卡是否属于指定scene
        if (currentLevelInfo.scene != key)
            return false;
        // 检查下一个关卡是否还属于同一个scene
        if (currentLevel < levels.Count)
        {
            LevelInfo nextLevelInfo = levels[currentLevel];
            return nextLevelInfo.scene != key;
        }
        
        // 如果已经是最后一个关卡，返回true
        return true;
    }
    
    /// <summary>
    /// 读取 level 时使用的 scene 键：若 level 中不存在该 identifier，则用 mainScene 匹配。
    /// </summary>
    public string GetSceneKeyForLevels(string sceneIdentifier)
    {
        if (string.IsNullOrEmpty(sceneIdentifier)) return sceneIdentifier;
        for (int i = 0; i < levels.Count; i++)
            if (levels[i].scene == sceneIdentifier) return sceneIdentifier;
        if (CSVLoader.Instance != null && CSVLoader.Instance.sceneInfos != null)
        {
            foreach (SceneInfo s in CSVLoader.Instance.sceneInfos)
                if (s.identifier == sceneIdentifier && !string.IsNullOrEmpty(s.mainScene)) return s.mainScene;
        }
        return sceneIdentifier;
    }

    /// <summary>
    /// 获取指定scene的所有关卡索引（从0开始）
    /// </summary>
    public List<int> GetLevelIndicesForScene(string sceneIdentifier)
    {
        List<int> indices = new List<int>();
        string key = GetSceneKeyForLevels(sceneIdentifier);
        if (string.IsNullOrEmpty(key)) return indices;
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i].scene == key)
                indices.Add(i);
        }
        return indices;
    }
    
    /// <summary>
    /// 检查当前关卡是否是scene中第一个boss关卡
    /// </summary>
    public bool IsFirstBossLevelInScene(string sceneIdentifier, string bossType)
    {
        if (string.IsNullOrEmpty(sceneIdentifier) || string.IsNullOrEmpty(bossType))
        {
            return false;
        }
        
        int currentLevel = GameManager.Instance != null ? GameManager.Instance.mainGameData.currentLevel : 1;
        if (currentLevel < 1 || currentLevel > levels.Count)
        {
            return false;
        }
        
        LevelInfo currentLevelInfo = levels[currentLevel - 1];
        string key = GetSceneKeyForLevels(sceneIdentifier);
        if (currentLevelInfo.scene != key ||
            string.IsNullOrEmpty(currentLevelInfo.boss) ||
            currentLevelInfo.boss.ToLower() != bossType.ToLower())
            return false;
        for (int i = 0; i < currentLevel - 1; i++)
        {
            if (levels[i].scene == key &&
                !string.IsNullOrEmpty(levels[i].boss) &&
                levels[i].boss.ToLower() == bossType.ToLower())
                return false;
        }
        return true;
    }

    /// <summary>
    /// 检查当前关卡是否是scene中最后一个boss关卡
    /// </summary>
    public bool IsLastBossLevelInScene(string sceneIdentifier, string bossType)
    {
        if (string.IsNullOrEmpty(sceneIdentifier) || string.IsNullOrEmpty(bossType))
            return false;
        int currentLevel = GameManager.Instance != null ? GameManager.Instance.mainGameData.currentLevel : 1;
        if (currentLevel < 1 || currentLevel > levels.Count)
            return false;
        LevelInfo currentLevelInfo = levels[currentLevel - 1];
        string key = GetSceneKeyForLevels(sceneIdentifier);
        if (currentLevelInfo.scene != key ||
            string.IsNullOrEmpty(currentLevelInfo.boss) ||
            currentLevelInfo.boss.ToLower() != bossType.ToLower())
            return false;
        for (int i = currentLevel; i < levels.Count; i++)
        {
            if (levels[i].scene == key &&
                !string.IsNullOrEmpty(levels[i].boss) &&
                levels[i].boss.ToLower() == bossType.ToLower())
                return false;
        }
        return true;
    }

    /// <summary>
    /// 检查scene中是否还有指定类型的boss关卡（不包括当前关卡）
    /// </summary>
    public bool HasMoreBossLevelsInScene(string sceneIdentifier, string bossType)
    {
        if (string.IsNullOrEmpty(sceneIdentifier) || string.IsNullOrEmpty(bossType))
            return false;
        int currentLevel = GameManager.Instance != null ? GameManager.Instance.mainGameData.currentLevel : 1;
        if (currentLevel < 1 || currentLevel > levels.Count)
            return false;
        string key = GetSceneKeyForLevels(sceneIdentifier);
        for (int i = currentLevel; i < levels.Count; i++)
        {
            if (levels[i].scene == key &&
                !string.IsNullOrEmpty(levels[i].boss) &&
                levels[i].boss.ToLower() == bossType.ToLower())
                return true;
        }
        return false;
    }
}

