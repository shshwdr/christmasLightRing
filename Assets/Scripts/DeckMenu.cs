using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class DeckMenu : MonoBehaviour
{
    public GameObject menuPanel;
    public Transform contentParent; // ScrollView的Content Transform
    public GameObject cardItemPrefab; // 卡牌项预制体
    public Button closeButton;
    public TMP_Text cardsFill;
    public TMP_Text titleText; // 标题文本（显示"Remove Card"或"All Cards"）
    
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
            UpdateMenu(false);
        }
    }
    
    public void ShowMenuInRemoveMode()
    {
        if (menuPanel != null)
        {
            menuPanel.SetActive(true);
            UpdateMenu(true);
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
            foreach (CardType purchasedType in GameManager.Instance.mainGameData.purchasedCards)
            {
                if (purchasedType == cardType)
                {
                    count++;
                }
            }
        }
        
        // 减去移除的数量
        foreach (CardType removedType in GameManager.Instance.mainGameData.removedCards)
        {
            if (removedType == cardType)
            {
                count--;
            }
        }
        
        return Mathf.Max(0, count); // 确保不会返回负数
    }
    
    public void UpdateMenu(bool isRemoveMode = false)
    {
        if (contentParent == null || CardInfoManager.Instance == null || GameManager.Instance == null) return;
        
        // 更新标题
        if (titleText != null)
        {
            if (isRemoveMode)
            {
                // 从 Localization 获取 "Remove Card" 文字
                var removeCardLocalizedString = new LocalizedString("GameText", "Remove Card");
                var removeCardHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(removeCardLocalizedString.TableReference, removeCardLocalizedString.TableEntryReference);
                titleText.text = removeCardHandle.WaitForCompletion();
            }
            else
            {
                // 从 Localization 获取 "All Cards" 文字
                var allCardsLocalizedString = new LocalizedString("GameText", "All Cards");
                var allCardsHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(allCardsLocalizedString.TableReference, allCardsLocalizedString.TableEntryReference);
                titleText.text = allCardsHandle.WaitForCompletion();
            }
        }
        
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
            
            // 如果是移除模式，显示所有卡牌（canBeRemoved为false的也会显示，但不显示移除按钮）
            // 不再过滤canBeRemoved为false的卡牌
            
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
                    cardItem.Setup(cardData.cardInfo, cardData.cardType, cardData.count, isRemoveMode);
                }
            }
        }
        else
        {
            // 如果没有预制体，使用简单的Text显示
            foreach (CardDisplayData cardData in cardDataList)
            {
                GameObject itemObj = new GameObject($"CardItem_{cardData.cardInfo.identifier}");
                itemObj.transform.SetParent(contentParent);
                
                TextMeshProUGUI text = itemObj.AddComponent<TextMeshProUGUI>();
                // 从 Localization 获取卡牌名称
                string nameKey = "cardName_" + cardData.cardInfo.identifier;
                var nameLocalizedString = new LocalizedString("GameText", nameKey);
                var nameHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(nameLocalizedString.TableReference, nameLocalizedString.TableEntryReference);
                string localizedName = nameHandle.WaitForCompletion();
                text.text = $"{localizedName} x{cardData.count}";
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

