using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class GameData
{
    public int coins = 0;
    public int gifts = 0;
    public int health = 3;
    public int flashlights = 0;
    public int currentLevel = 1;
    public List<CardType> purchasedCards = new List<CardType>(); // 商店购买的卡牌
    public List<string> ownedUpgrades = new List<string>(); // 拥有的升级项identifier列表
    public int patternRecognitionSequence = 0; // patternRecognition升级项的连续safe tile计数
    public List<string> shownTutorials = new List<string>(); // 已显示的教程identifier列表（序列化用）
    public List<string> readStories = new List<string>(); // 已阅读的故事identifier列表（序列化用）
    
    // 设置数据
    public float sfxVolume = 1f;
    public float musicVolume = 1f;
    public int fullscreenMode = 0; // 0: Fullscreen, 1: FullscreenWindow, 2: Windowed
    
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
    
    // Boss战前状态保存（用于retry boss）
    public int bossPreHealth = 3;
    public int bossPreCoins = 0;
    public int bossPreFlashlights = 0;
    public List<CardType> bossPrePurchasedCards = new List<CardType>();
    public List<string> bossPreOwnedUpgrades = new List<string>();
    
    // Level开始状态保存（用于retry level）
    public int levelStartHealth = 3;
    public int levelStartCoins = 0;
    public int levelStartFlashlights = 0;
    public List<CardType> levelStartPurchasedCards = new List<CardType>();
    public List<string> levelStartOwnedUpgrades = new List<string>();
    public int levelStartLevel = 1;
}
