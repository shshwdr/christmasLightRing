using System.Collections.Generic;

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
    public HashSet<string> shownTutorials = new HashSet<string>(); // 已显示的教程identifier列表
    
    // Boss战前状态保存（用于retry boss）
    public int bossPreHealth = 3;
    public int bossPreCoins = 0;
    public int bossPreFlashlights = 0;
    public List<CardType> bossPrePurchasedCards = new List<CardType>();
    public List<string> bossPreOwnedUpgrades = new List<string>();
}
