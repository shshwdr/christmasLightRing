using System.Collections;
using System.Collections.Generic;
using Sinbad;
using UnityEngine;

public class CardInfo
{
    public string identifier;
    public string name;
    public int cost;
    public int costIncrease;
    public string desc;
    public bool canDraw;
    public int start;
    public bool isFixed;
    public int level; // 第几关后会解锁可以被抽到
    public int maxCount; // 最多可以买几张，0表示无限制
}
public class UpgradeInfo
{
    public string identifier;
    public string name;
    public int cost;
    public string desc;
    public bool canDraw;
    public int start;
    public int value;
}
public class LevelInfo
{
    public int enemyCount;
    public int col;
    public int row;
}
public class TutorialInfo
{
    public string identifier;
    public string desc;
}
public class CSVLoader : Singleton<CSVLoader>
{
    public Dictionary<string, CardInfo> cardDict = new Dictionary<string, CardInfo>();
    public Dictionary<string, UpgradeInfo> upgradeDict = new Dictionary<string, UpgradeInfo>();
    public Dictionary<string, TutorialInfo> tutorialDict = new Dictionary<string, TutorialInfo>();
    public List<LevelInfo> levelInfos = new List<LevelInfo>();
    // Start is called before the first frame update
    public void Init()
    {
        // 加载普通形状信息
        var cardInfos = CsvUtil.LoadObjects<CardInfo>("card");
        foreach (var cardInfo in cardInfos)
        {
            cardDict.Add(cardInfo.identifier, cardInfo);
        }

        
        var upgradeInfos = CsvUtil.LoadObjects<UpgradeInfo>("upgrade");
        foreach (var cardInfo in upgradeInfos)
        {
            upgradeDict.Add(cardInfo.identifier, cardInfo);
        }
        
        // 加载教程信息
        var tutorialInfos = CsvUtil.LoadObjects<TutorialInfo>("tutorial");
        foreach (var tutorialInfo in tutorialInfos)
        {
            tutorialDict.Add(tutorialInfo.identifier, tutorialInfo);
        }
        
        // 加载关卡信息
        levelInfos = CsvUtil.LoadObjects<LevelInfo>("level");
    }

   
}
