using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 主游戏数据，在新游戏时需要清空的数据（仅保存在内存中，不序列化到文件）
/// </summary>
public class MainGameData
{
    public int coins = 0;
    public int gifts = 0;
    public int health = 3;
    public int maxHealth = 3; // 血量上限（初始值等于initialHealth，会被AsceticVow等升级项修改）
    public int flashlights = 0;
    public int currentLevel = 1;
    public string currentScene = ""; // 当前场景标识符
    public List<CardType> purchasedCards = new List<CardType>(); // 商店购买的卡牌
    public List<string> ownedUpgrades = new List<string>(); // 拥有的升级项identifier列表
    public int patternRecognitionSequence = 0; // patternRecognition升级项的连续safe tile计数
    public bool finishedForceTutorial;
    public bool isFirstTileRevealedThisTurn = false; // 每回合第一张翻开的卡（用于FirstLuck升级项）
    
    // 用于访问的HashSet（不序列化）
    [System.NonSerialized]
    private HashSet<string> _shownTutorialsSet = null;
    [System.NonSerialized]
    private HashSet<string> _readStoriesSet = null;
    
    public HashSet<string> GetShownTutorials()
    {
        if (_shownTutorialsSet == null)
        {
            // 从 GameData 读取 shownTutorials
            if (GameManager.Instance != null && GameManager.Instance.gameData != null)
            {
                _shownTutorialsSet = new HashSet<string>(GameManager.Instance.gameData.shownTutorials);
            }
            else
            {
                _shownTutorialsSet = new HashSet<string>();
            }
        }
        return _shownTutorialsSet;
    }
    
    public HashSet<string> GetReadStories()
    {
        if (_readStoriesSet == null)
        {
            // 从 GameData 读取 readStories
            if (GameManager.Instance != null && GameManager.Instance.gameData != null)
            {
                _readStoriesSet = new HashSet<string>(GameManager.Instance.gameData.readStories);
            }
            else
            {
                _readStoriesSet = new HashSet<string>();
            }
        }
        return _readStoriesSet;
    }
    
    public void SyncShownTutorials()
    {
        // 同步到 GameData
        if (GameManager.Instance != null && GameManager.Instance.gameData != null)
        {
            GameManager.Instance.gameData.shownTutorials = _shownTutorialsSet != null ? _shownTutorialsSet.ToList() : new List<string>();
        }
    }
    
    public void SyncReadStories()
    {
        // 同步到 GameData
        if (GameManager.Instance != null && GameManager.Instance.gameData != null)
        {
            GameManager.Instance.gameData.readStories = _readStoriesSet != null ? _readStoriesSet.ToList() : new List<string>();
        }
    }
    
    /// <summary>
    /// 刷新HashSet缓存（当从GameData加载数据后调用）
    /// </summary>
    public void RefreshShownTutorialsCache()
    {
        _shownTutorialsSet = null;
    }
    
    /// <summary>
    /// 刷新HashSet缓存（当从GameData加载数据后调用）
    /// </summary>
    public void RefreshReadStoriesCache()
    {
        _readStoriesSet = null;
    }
    
    /// <summary>
    /// 初始化缓存（从GameData加载数据后立即初始化HashSet）
    /// </summary>
    public void InitializeCaches()
    {
        // 初始化 shownTutorials 缓存
        if (GameManager.Instance != null && GameManager.Instance.gameData != null)
        {
            _shownTutorialsSet = new HashSet<string>(GameManager.Instance.gameData.shownTutorials);
        }
        else
        {
            _shownTutorialsSet = new HashSet<string>();
        }
        
        // 初始化 readStories 缓存
        if (GameManager.Instance != null && GameManager.Instance.gameData != null)
        {
            _readStoriesSet = new HashSet<string>(GameManager.Instance.gameData.readStories);
        }
        else
        {
            _readStoriesSet = new HashSet<string>();
        }
    }
    
    /// <summary>
    /// 重置所有数据到初始值
    /// </summary>
    public void Reset()
    {
        coins = 0;
        gifts = 0;
        health = 3;
        maxHealth = 3;
        flashlights = 0;
        currentLevel = 1;
        currentScene = "";
        purchasedCards.Clear();
        ownedUpgrades.Clear();
        patternRecognitionSequence = 0;
    }
}

