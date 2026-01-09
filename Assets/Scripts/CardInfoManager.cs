using UnityEngine;
using System.Collections.Generic;

public class CardInfoManager : MonoBehaviour
{
    public static CardInfoManager Instance;
    
    private Dictionary<string, CardInfo> cardInfoDict = new Dictionary<string, CardInfo>();
    private Dictionary<string, CardInfo> temporaryCardsDict = new Dictionary<string, CardInfo>(); // 临时卡字典，不修改cardInfoDict
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
        identifierToCardType["nun"] = CardType.Nun;
        identifierToCardType["snowman"] = CardType.Snowman;
        identifierToCardType["horribleman"] = CardType.Horribleman;
        identifierToCardType["door"] = CardType.Door;
        
        
        // 从CSVLoader加载CardInfo
        if (CSVLoader.Instance != null)
        {
            cardInfoDict = CSVLoader.Instance.cardDict;
        }
    }
    
    public CardInfo GetCardInfo(string identifier)
    {
        // 优先检查临时卡字典
        if (temporaryCardsDict.ContainsKey(identifier))
        {
            return temporaryCardsDict[identifier];
        }
        // 然后检查原始字典
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
            currentLevel = GameManager.Instance.mainGameData.currentLevel;
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
        foreach (CardType purchasedType in GameManager.Instance.mainGameData.purchasedCards)
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
        // 合并原始字典和临时卡字典
        Dictionary<string, CardInfo> mergedDict = new Dictionary<string, CardInfo>();
        
        // 先添加原始字典中的所有卡
        foreach (var kvp in cardInfoDict)
        {
            mergedDict[kvp.Key] = kvp.Value;
        }
        
        // 然后用临时卡覆盖（临时卡优先级更高）
        foreach (var kvp in temporaryCardsDict)
        {
            mergedDict[kvp.Key] = kvp.Value;
        }
        
        List<CardInfo> allCards = new List<CardInfo>();
        foreach (var kvp in mergedDict)
        {
            allCards.Add(kvp.Value);
        }
        return allCards;
    }
    
    // 临时添加卡牌（用于boss关卡）
    public void AddTemporaryCard(string identifier, CardInfo cardInfo)
    {
        // 添加到临时卡字典，不修改cardInfoDict
        temporaryCardsDict[identifier] = cardInfo;
    }
    
    // 移除临时卡牌
    public void RemoveTemporaryCard(string identifier)
    {
        // 从临时卡字典中移除，不修改cardInfoDict
        if (temporaryCardsDict.ContainsKey(identifier))
        {
            temporaryCardsDict.Remove(identifier);
        }
    }
    
    // 清空所有临时卡
    public void ClearTemporaryCards()
    {
        temporaryCardsDict.Clear();
    }
    
    // 检查卡牌是否存在（包括临时卡和原始卡）
    public bool HasCard(string identifier)
    {
        return temporaryCardsDict.ContainsKey(identifier) || cardInfoDict.ContainsKey(identifier);
    }
    
    // 检查卡牌是否是敌人（基于isEnemy字段）
    public bool IsEnemyCard(CardType cardType)
    {
        CardInfo cardInfo = GetCardInfo(cardType);
        if (cardInfo != null)
        {
            return cardInfo.isEnemy;
        }
        return false;
    }
    
    // 检查卡牌是否是敌人（基于identifier）
    public bool IsEnemyCard(string identifier)
    {
        CardInfo cardInfo = GetCardInfo(identifier);
        if (cardInfo != null)
        {
            return cardInfo.isEnemy;
        }
        return false;
    }
    
    // 获取敌人的identifier（用于获取基础图片）
    public string GetEnemyIdentifier(CardType cardType)
    {
        CardInfo cardInfo = GetCardInfo(cardType);
        if (cardInfo != null)
        {
            return cardInfo.identifier;
        }
        return null;
    }
    
    // 获取敌人的hurt图片（被灯光照开时的图片）
    public Sprite GetEnemyHurtSprite(CardType cardType)
    {
        CardInfo cardInfo = GetCardInfo(cardType);
        if (cardInfo != null)
        {
            string path = $"icon/{cardInfo.identifier}_hurt";
            Sprite sprite = Resources.Load<Sprite>(path);
            return sprite; // 如果不存在返回null
        }
        return null;
    }
    
    // 获取敌人的atk图片（攻击时的图片）
    public Sprite GetEnemyAtkSprite(CardType cardType)
    {
        CardInfo cardInfo = GetCardInfo(cardType);
        if (cardInfo != null)
        {
            string path = $"icon/{cardInfo.identifier}_atk";
            Sprite sprite = Resources.Load<Sprite>(path);
            return sprite; // 如果不存在返回null
        }
        return null;
    }
}




