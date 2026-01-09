using UnityEngine;
using System.Collections.Generic;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;
    
    private Dictionary<string, TutorialInfo> tutorialDict = new Dictionary<string, TutorialInfo>();
    
    private bool _tutorialForceBoard = true; // 控制第一关和第二关的特殊设定
    
    public bool tutorialForceBoard
    {
        get => _tutorialForceBoard;
        set
        {
            if (_tutorialForceBoard != value)
            {
                _tutorialForceBoard = value;
                // 保存到GameData
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.gameData.tutorialForceBoard = value;
                    GameManager.Instance.SaveGameData();
                }
            }
        }
    }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeTutorialInfo();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // 从GameData加载tutorialForceBoard（延迟一帧，确保DataManager已经加载数据）
        StartCoroutine(LoadTutorialForceBoardDelayed());
    }
    
    private System.Collections.IEnumerator LoadTutorialForceBoardDelayed()
    {
        yield return null; // 等待一帧，确保DataManager已经加载数据
        
        // 从GameData加载tutorialForceBoard
        if (GameManager.Instance != null)
        {
            _tutorialForceBoard = GameManager.Instance.gameData.tutorialForceBoard;
        }
    }
    
    private void InitializeTutorialInfo()
    {
        // 从CSVLoader加载TutorialInfo
        if (CSVLoader.Instance != null)
        {
            tutorialDict = CSVLoader.Instance.tutorialDict;
        }
    }
    
    public TutorialInfo GetTutorialInfo(string identifier)
    {
        if (tutorialDict.ContainsKey(identifier))
        {
            return tutorialDict[identifier];
        }
        return null;
    }
    
    public void ShowTutorial(string identifier,bool forceShow = false)
    {
        // 如果tutorialForceBoard开启，即使显示过也会再次显示
        bool shouldShow = true;
        if (!forceShow && GameManager.Instance != null && GameManager.Instance.mainGameData.GetShownTutorials().Contains(identifier))
        {
            shouldShow = false; // 已经显示过，不再显示
        }
        
        if (!shouldShow) return;
        
        TutorialInfo tutorialInfo = GetTutorialInfo(identifier);
        if (tutorialInfo != null && UIManager.Instance != null)
        {
            // 显示教程
            UIManager.Instance.ShowTutorial(tutorialInfo.desc);
            
            // 记录已显示（只有在tutorialForceBoard关闭时才记录，避免重复记录）
            if (GameManager.Instance != null)
            {
                if (!GameManager.Instance.mainGameData.GetShownTutorials().Contains(identifier))
                {
                    GameManager.Instance.mainGameData.GetShownTutorials().Add(identifier);
                    // mainGameData不序列化，不需要保存
                }
            }
        }
    }
}








