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
}
