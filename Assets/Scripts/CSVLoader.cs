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
    public bool isEnemy; // 是否是敌人卡牌
    public string scene; // 场景标识，如果为空则无限制，否则需要当前场景 > scene（转换为int比较）才能被选择
    public bool canBeRemoved; // 是否可以被移除
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
    public string scene; // 场景标识，如果为空则无限制，否则需要当前场景 > scene（转换为int比较）才能被选择
}
public class LevelInfo
{
    public int enemyCount;
    public int col;
    public int row;
    public string boss; // boss关卡标识，如果为空则不是boss关卡，否则是对应的boss的卡片（nun, snowman, horribleman）
    public string map; // 背景地图标识，如果为空则使用默认背景，否则从Resources/bk/加载对应的图片
    public string scene; // 场景标识，用于标识该关卡属于哪个scene
}
public class SceneInfo
{
    public string identifier; // 场景标识符
    public int freeUpgrade; // 免费升级数量
    public int freeItem; // 免费物品数量
    public string prev; // 前置scene标识符，如果为空或已通过，则可以进入
    public string name; // 场景名称
    /// <summary> 游戏模式列表，如 origin、revealHint、noHeal、noRing 等，CSV 中用 | 分隔多个 </summary>
    public List<string> type;
    
    /// <summary> 检查是否包含指定模式（type 不为空且列表中包含该 type） </summary>
    public bool HasType(string t)
    {
        return type != null && type.Contains(t);
    }
    /// <summary> 初始血量（进入该 scene 时的 health 与 maxHealth）。若未配置或≤0 则使用默认值 3 </summary>
    public int hp;
}
public class TutorialInfo
{
    public string identifier;
    public string desc;
}
public class StoryInfo
{
    public string identifier;
    public string id;
    public string title;
    public string desc;
    public string image;
    public bool isEnd;
}
public class CSVLoader : Singleton<CSVLoader>
{
    public Dictionary<string, CardInfo> cardDict = new Dictionary<string, CardInfo>();
    public Dictionary<string, UpgradeInfo> upgradeDict = new Dictionary<string, UpgradeInfo>();
    public Dictionary<string, TutorialInfo> tutorialDict = new Dictionary<string, TutorialInfo>();
    public Dictionary<string, List<StoryInfo>> storyDict = new Dictionary<string, List<StoryInfo>>();
    public List<LevelInfo> levelInfos = new List<LevelInfo>();
    public List<SceneInfo> sceneInfos = new List<SceneInfo>();
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
            upgradeDict[cardInfo.identifier] = cardInfo;
        }
        
        // 加载教程信息
        var tutorialInfos = CsvUtil.LoadObjects<TutorialInfo>("tutorial");
        foreach (var tutorialInfo in tutorialInfos)
        {
            tutorialDict.Add(tutorialInfo.identifier, tutorialInfo);
        }
        
        // 加载关卡信息
        levelInfos = CsvUtil.LoadObjects<LevelInfo>("level");
        
        // 加载场景信息
        sceneInfos = CsvUtil.LoadObjects<SceneInfo>("scene");
        
        // 加载故事信息
        var storyInfos = CsvUtil.LoadObjects<StoryInfo>("story");
        foreach (var storyInfo in storyInfos)
        {
            if (!storyDict.ContainsKey(storyInfo.identifier))
            {
                storyDict[storyInfo.identifier] = new List<StoryInfo>();
            }
            storyDict[storyInfo.identifier].Add(storyInfo);
        }
    }

   
}
