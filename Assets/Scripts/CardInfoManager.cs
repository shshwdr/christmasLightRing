using UnityEngine;
using System.Collections.Generic;

public class CardInfoManager : MonoBehaviour
{
    public static CardInfoManager Instance;
    
    private Dictionary<string, CardInfo> cardInfoDict = new Dictionary<string, CardInfo>();
    private Dictionary<string, CardType> identifierToCardType = new Dictionary<string, CardType>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeCardInfo();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeCardInfo()
    {
        // 初始化identifier到CardType的映射
        identifierToCardType["coin"] = CardType.Coin;
        identifierToCardType["gift"] = CardType.Gift;
        identifierToCardType["enemy"] = CardType.Enemy;
        identifierToCardType["grinch"] = CardType.Enemy;
        identifierToCardType["flashlight"] = CardType.Flashlight;
        identifierToCardType["hint"] = CardType.Hint;
        identifierToCardType["police"] = CardType.PoliceStation;
        identifierToCardType["church"] = CardType.PoliceStation;
        identifierToCardType["player"] = CardType.Player;
        identifierToCardType["blank"] = CardType.Blank;
        identifierToCardType["bell"] = CardType.Bell;
        identifierToCardType["sign"] = CardType.Sign;
        identifierToCardType["iceground"] = CardType.Iceground;
        
        
        // 从CSVLoader加载CardInfo
        if (CSVLoader.Instance != null)
        {
            cardInfoDict = CSVLoader.Instance.cardDict;
        }
    }
    
    public CardInfo GetCardInfo(string identifier)
    {
        if (cardInfoDict.ContainsKey(identifier))
        {
            return cardInfoDict[identifier];
        }
        return null;
    }
    
    public CardInfo GetCardInfo(CardType cardType)
    {
        foreach (var kvp in identifierToCardType)
        {
            if (kvp.Value == cardType)
            {
                return GetCardInfo(kvp.Key);
            }
        }
        return null;
    }
    
    public CardType GetCardType(string identifier)
    {
        if (identifierToCardType.ContainsKey(identifier))
        {
            return identifierToCardType[identifier];
        }
        return CardType.Blank;
    }
    
    public Sprite GetCardSprite(CardType cardType)
    {
        foreach (var kvp in identifierToCardType)
        {
            if (kvp.Value == cardType)
            {
                CardInfo info = GetCardInfo(kvp.Key);
                if (info != null)
                {
                    string path = $"icon/{info.identifier}";
                    return Resources.Load<Sprite>(path);
                }
            }
        }
        return null;
    }
    public Sprite GetCardBack(string type)
    {
        
        return Resources.Load<Sprite>($"icon/type");
    }
    
    public List<CardInfo> GetPurchasableCards()
    {
        List<CardInfo> purchasable = new List<CardInfo>();
        
        // 获取当前关卡
        int currentLevel = 1;
        if (GameManager.Instance != null)
        {
            currentLevel = GameManager.Instance.gameData.currentLevel;
        }
        
        foreach (var kvp in cardInfoDict)
        {
            CardInfo cardInfo = kvp.Value;
            
            // 检查canDraw
            if (!cardInfo.canDraw)
            {
                continue;
            }
            
            // 检查level解锁：如果level <= 0，则默认解锁；否则需要当前关卡 >= level
            if (cardInfo.level > 0 && currentLevel < cardInfo.level)
            {
                continue;
            }
            
            // 检查maxCount上限：如果maxCount <= 0，则无限制；否则检查已购买数量
            if (cardInfo.maxCount > 0)
            {
                CardType cardType = GetCardType(cardInfo.identifier);
                int purchasedCount = GetPurchasedCardCount(cardType);
                
                // 如果已购买数量 >= maxCount，则不再出现在商店
                if (purchasedCount >= cardInfo.maxCount)
                {
                    continue;
                }
            }
            
            purchasable.Add(cardInfo);
        }
        return purchasable;
    }
    
    // 获取已购买某张卡的数量
    private int GetPurchasedCardCount(CardType cardType)
    {
        if (GameManager.Instance == null)
        {
            return 0;
        }
        
        int count = 0;
        foreach (CardType purchasedType in GameManager.Instance.gameData.purchasedCards)
        {
            if (purchasedType == cardType)
            {
                count++;
            }
        }
        return count;
    }
    
    public List<CardInfo> GetAllCards()
    {
        List<CardInfo> allCards = new List<CardInfo>();
        foreach (var kvp in cardInfoDict)
        {
            allCards.Add(kvp.Value);
        }
        return allCards;
    }
}
