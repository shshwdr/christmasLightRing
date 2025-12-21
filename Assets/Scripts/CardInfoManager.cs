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
        foreach (var kvp in cardInfoDict)
        {
            if (kvp.Value.canDraw)
            {
                purchasable.Add(kvp.Value);
            }
        }
        return purchasable;
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
