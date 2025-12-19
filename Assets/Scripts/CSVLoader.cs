using System.Collections;
using System.Collections.Generic;
using Sinbad;
using UnityEngine;

public class CardInfo
{
    public string identifier;
    public string name;
    public int cost;
    public string desc;
    public bool canDraw;
    public int start;
    public bool isFixed;
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
public class CSVLoader : Singleton<CSVLoader>
{
    public Dictionary<string, CardInfo> cardDict = new Dictionary<string, CardInfo>();
    public Dictionary<string, UpgradeInfo> upgradeDict = new Dictionary<string, UpgradeInfo>();
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
    }

   
}
