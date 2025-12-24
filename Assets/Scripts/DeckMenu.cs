using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class DeckMenu : MonoBehaviour
{
    public GameObject menuPanel;
    public Transform contentParent; // ScrollView的Content Transform
    public GameObject cardItemPrefab; // 卡牌项预制体
    public Button closeButton;
    public TMP_Text cardsFill;
    
    private void Awake()
    {
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }
        closeButton.onClick.AddListener(ToggleMenu);
    }
    
    public void ToggleMenu()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        if (menuPanel != null)
        {
            bool isActive = menuPanel.activeSelf;
            menuPanel.SetActive(!isActive);
            
            if (!isActive)
            {
                UpdateMenu();
            }
        }
    }
    
    public void ShowMenu()
    {
        if (menuPanel != null)
        {
            menuPanel.SetActive(true);
            UpdateMenu();
        }
    }
    
    public void HideMenu()
    {
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }
    }
    
    private int GetCardCount(CardType cardType)
    {
        if (GameManager.Instance == null || CardInfoManager.Instance == null) return 0;
        
        CardInfo cardInfo = CardInfoManager.Instance.GetCardInfo(cardType);
        if (cardInfo == null) return 0;
        
        // 起始数量
        int count = cardInfo.start;
        
        // 如果是敌人（基于isEnemy），使用关卡配置的数量
        if (CardInfoManager.Instance != null && CardInfoManager.Instance.IsEnemyCard(cardType))
        {
            // 只有CardType.Enemy使用关卡配置的数量，其他isEnemy的卡牌使用start值
            if (cardType == CardType.Enemy)
            {
                if (LevelManager.Instance != null)
                {
                    LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
                    if (levelInfo != null)
                    {
                        count = levelInfo.enemyCount;
                    }
                }
            }
            // nun boss关卡：显示实际nun的数量（3个）
            else if (cardType == CardType.Nun)
            {
                if (LevelManager.Instance != null)
                {
                    LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
                    if (levelInfo != null && levelInfo.boss != null && levelInfo.boss.ToLower() == "nun")
                    {
                        count = 3; // nun关卡中，实际有3个nun
                    }
                }
            }
        }
        else
        {
            // 加上购买的数量
            foreach (CardType purchasedType in GameManager.Instance.gameData.purchasedCards)
            {
                if (purchasedType == cardType)
                {
                    count++;
                }
            }
        }
        
        return count;
    }
    
    private void UpdateMenu()
    {
        if (contentParent == null || CardInfoManager.Instance == null || GameManager.Instance == null) return;
        
        // 清除现有内容
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }
        
        // 获取所有卡牌信息
        List<CardInfo> allCards = CardInfoManager.Instance.GetAllCards();
        
        // 创建卡牌数据列表，包含卡牌信息和数量
        List<CardDisplayData> cardDataList = new List<CardDisplayData>();
        
        foreach (CardInfo cardInfo in allCards)
        {
            CardType cardType = CardInfoManager.Instance.GetCardType(cardInfo.identifier);
            if (cardType == CardType.Blank || cardType == CardType.Player) continue; // 跳过空白卡和玩家卡
            
            int count = GetCardCount(cardType);
            // 只显示数量大于0的卡牌
            if (count > 0)
            {
                cardDataList.Add(new CardDisplayData
                {
                    cardInfo = cardInfo,
                    cardType = cardType,
                    count = count
                });
            }
        }
        
        // 排序：isFixed的在前按cardInfo顺序，非isFixed的按数量排序
        List<CardInfo> allCardsOrdered = CardInfoManager.Instance.GetAllCards();
        Dictionary<string, int> cardOrderDict = new Dictionary<string, int>();
        for (int i = 0; i < allCardsOrdered.Count; i++)
        {
            cardOrderDict[allCardsOrdered[i].identifier] = i;
        }
        
        cardDataList = cardDataList.OrderBy(cardData =>
        {
            if (cardData.cardInfo.isFixed)
            {
                // isFixed的按cardInfo顺序排序
                return cardOrderDict.ContainsKey(cardData.cardInfo.identifier) 
                    ? cardOrderDict[cardData.cardInfo.identifier] 
                    : int.MaxValue;
            }
            else
            {
                // 非isFixed的按数量降序排序，但需要放在isFixed之后
                return int.MaxValue / 2 - cardData.count;
            }
        }).ToList();
        
        // 创建UI项
        if (cardItemPrefab != null)
        {
            foreach (CardDisplayData cardData in cardDataList)
            {
                GameObject itemObj = Instantiate(cardItemPrefab, contentParent);
                DeckCardItem cardItem = itemObj.GetComponent<DeckCardItem>();
                if (cardItem != null)
                {
                    cardItem.Setup(cardData.cardInfo, cardData.cardType, cardData.count);
                }
            }
        }
        else
        {
            // 如果没有预制体，使用简单的Text显示
            foreach (CardDisplayData cardData in cardDataList)
            {
                GameObject itemObj = new GameObject($"CardItem_{cardData.cardInfo.name}");
                itemObj.transform.SetParent(contentParent);
                
                TextMeshProUGUI text = itemObj.AddComponent<TextMeshProUGUI>();
                text.text = $"{cardData.cardInfo.name} x{cardData.count}";
                text.fontSize = 24;
                
                RectTransform rect = itemObj.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(200, 30);
            }
        }
        
        // 更新cardsFill：显示所有卡片的和/board的长乘以宽
        if (cardsFill != null)
        {
            // 计算所有卡片的和
            int totalCardCount = 0;
            foreach (CardInfo cardInfo in allCards)
            {
                CardType cardType = CardInfoManager.Instance.GetCardType(cardInfo.identifier);
                if (cardType == CardType.Blank || cardType == CardType.Player) continue; // 跳过空白卡和玩家卡
                totalCardCount += GetCardCount(cardType);
            }
            
            // 获取board的长乘以宽
            int boardArea = 0;
            if (LevelManager.Instance != null)
            {
                LevelInfo levelInfo = LevelManager.Instance.GetCurrentLevelInfo();
                if (levelInfo != null)
                {
                    boardArea = levelInfo.row * levelInfo.col;
                }
            }
            
            // 更新文本
            cardsFill.text = $"{totalCardCount}/{boardArea}";
        }
        
    }
    
    private class CardDisplayData
    {
        public CardInfo cardInfo;
        public CardType cardType;
        public int count;
    }
}

