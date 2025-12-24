using UnityEngine;
using System.Collections.Generic;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;
    
    private Dictionary<string, TutorialInfo> tutorialDict = new Dictionary<string, TutorialInfo>();
    
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
    
    public void ShowTutorial(string identifier)
    {
        // 检查是否已经显示过
        if (GameManager.Instance != null && GameManager.Instance.gameData.shownTutorials.Contains(identifier))
        {
            return; // 已经显示过，不再显示
        }
        
        TutorialInfo tutorialInfo = GetTutorialInfo(identifier);
        if (tutorialInfo != null && UIManager.Instance != null)
        {
            // 显示教程
            UIManager.Instance.ShowTutorial(tutorialInfo.desc);
            
            // 记录已显示
            if (GameManager.Instance != null)
            {
                GameManager.Instance.gameData.shownTutorials.Add(identifier);
            }
        }
    }
}




