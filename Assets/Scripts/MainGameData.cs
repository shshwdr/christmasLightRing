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
    public int flashlights = 0;
    public int currentLevel = 1;
    public string currentScene = ""; // 当前场景标识符
    public List<CardType> purchasedCards = new List<CardType>(); // 商店购买的卡牌
    public List<string> ownedUpgrades = new List<string>(); // 拥有的升级项identifier列表
    public int patternRecognitionSequence = 0; // patternRecognition升级项的连续safe tile计数
    public bool finishedForceTutorial;
    public List<string> shownTutorials = new List<string>(); // 已显示的教程identifier列表（序列化用）
    public List<string> readStories = new List<string>(); // 已阅读的故事identifier列表（序列化用）
    
    // 用于访问的HashSet（不序列化）
    [System.NonSerialized]
    private HashSet<string> _shownTutorialsSet = null;
    [System.NonSerialized]
    private HashSet<string> _readStoriesSet = null;
    
    public HashSet<string> GetShownTutorials()
    {
        if (_shownTutorialsSet == null)
        {
            _shownTutorialsSet = new HashSet<string>(shownTutorials);
        }
        return _shownTutorialsSet;
    }
    
    public HashSet<string> GetReadStories()
    {
        if (_readStoriesSet == null)
        {
            _readStoriesSet = new HashSet<string>(readStories);
        }
        return _readStoriesSet;
    }
    
    public void SyncShownTutorials()
    {
        shownTutorials = _shownTutorialsSet != null ? _shownTutorialsSet.ToList() : new List<string>();
    }
    
    public void SyncReadStories()
    {
        readStories = _readStoriesSet != null ? _readStoriesSet.ToList() : new List<string>();
    }
    
    /// <summary>
    /// 重置所有数据到初始值
    /// </summary>
    public void Reset()
    {
        coins = 0;
        gifts = 0;
        health = 3;
        flashlights = 0;
        currentLevel = 1;
        currentScene = "";
        purchasedCards.Clear();
        ownedUpgrades.Clear();
        patternRecognitionSequence = 0;
    }
}

