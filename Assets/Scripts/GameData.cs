using System.Collections.Generic;

[System.Serializable]
public class GameData
{
    public int coins = 0;
    public int gifts = 0;
    public int health = 3;
    public int flashlights = 0;
    public int currentLevel = 1;
}

[System.Serializable]
public class CardDeckConfig
{
    public int coinCount = 5;
    public int giftCount = 5;
    public int enemyCount = 3;
    public int flashlightCount = 3;
    public int hintCount = 5;
    public int policeStationCount = 2;
    public int blankCount = 0; // 自动计算：25 - 其他卡牌总数
}


