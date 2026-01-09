using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class GalleryMenu : MonoBehaviour
{
    public static GalleryMenu Instance;
    
    [Header("Menu Panel")]
    public GameObject galleryMenuPanel;
    public Button closeButton;
    
    [Header("Gallery Content")]
    public Transform contentParent; // GridLayout的Content Transform
    public GameObject storyItemPrefab; // 故事项预制体（包含Image和Text）
    
    private Dictionary<string, GameObject> storyItemObjects = new Dictionary<string, GameObject>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        if (galleryMenuPanel != null)
        {
            galleryMenuPanel.SetActive(false);
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseMenu);
        }
    }
    
    /// <summary>
    /// 打开画廊菜单
    /// </summary>
    public void OpenMenu()
    {
        if (galleryMenuPanel != null)
        {
            galleryMenuPanel.SetActive(true);
            SFXManager.Instance?.PlayClickSound();
            UpdateGallery();
        }
    }
    
    /// <summary>
    /// 关闭画廊菜单
    /// </summary>
    public void CloseMenu()
    {
        if (galleryMenuPanel != null)
        {
            galleryMenuPanel.SetActive(false);
            SFXManager.Instance?.PlayClickSound();
        }
    }
    
    /// <summary>
    /// 更新画廊内容
    /// </summary>
    private void UpdateGallery()
    {
        if (contentParent == null || CSVLoader.Instance == null)
        {
            return;
        }
        
        // 清理现有的故事项
        foreach (var kvp in storyItemObjects)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        storyItemObjects.Clear();
        
        // 获取所有故事的identifier（去重）
        HashSet<string> storyIdentifiers = new HashSet<string>();
        foreach (var kvp in CSVLoader.Instance.storyDict)
        {
            storyIdentifiers.Add(kvp.Key);
        }
        
        // 为每个identifier创建故事项
        foreach (string identifier in storyIdentifiers)
        {
            if (!CSVLoader.Instance.storyDict.ContainsKey(identifier) || 
                CSVLoader.Instance.storyDict[identifier].Count == 0)
            {
                continue;
            }
            
            // 获取第一个storyInfo
            StoryInfo firstStory = CSVLoader.Instance.storyDict[identifier][0];
            
            // 创建故事项
            if (storyItemPrefab != null)
            {
                GameObject storyItemObj = Instantiate(storyItemPrefab, contentParent);
                storyItemObj.name = $"StoryItem_{identifier}";
                
                // 设置图片
                Image storyImage = storyItemObj.GetComponentInChildren<Image>();
                if (storyImage != null && !string.IsNullOrEmpty(firstStory.image))
                {
                    Sprite storySprite = Resources.Load<Sprite>("story/" + firstStory.image);
                    if (storySprite != null)
                    {
                        storyImage.sprite = storySprite;
                    }
                }
                
                // 设置标题
                TextMeshProUGUI titleText = storyItemObj.GetComponentInChildren<TextMeshProUGUI>();
                if (titleText != null)
                {
                    titleText.text = !string.IsNullOrEmpty(firstStory.title) ? firstStory.title : "";
                }
                
                // 检查是否已阅读
                bool isRead = GameManager.Instance != null && 
                              GameManager.Instance.mainGameData.GetReadStories().Contains(identifier);
                
                // 设置按钮点击事件data
                Button storyButton = storyItemObj.GetComponent<Button>();
                if (storyButton != null)
                {
                    if (isRead)
                    {
                        storyButton.interactable = true;
                        storyButton.onClick.AddListener(() => OnStoryItemClicked(identifier));
                    }
                    else
                    {
                        storyButton.interactable = false;
                        // 变黑效果
                        // if (storyImage != null)
                        // {
                        //     Color imageColor = storyImage.color;
                        //     imageColor = new Color(0.3f, 0.3f, 0.3f, imageColor.a); // 变黑
                        //     storyImage.color = imageColor;
                        // }
                        // if (titleText != null)
                        // {
                        //     Color textColor = titleText.color;
                        //     textColor = new Color(0.3f, 0.3f, 0.3f, textColor.a); // 变黑
                        //     titleText.color = textColor;
                        // }
                    }
                }
                
                storyItemObjects[identifier] = storyItemObj;
            }
        }
    }
    
    /// <summary>
    /// 故事项点击事件
    /// </summary>
    private void OnStoryItemClicked(string identifier)
    {
        SFXManager.Instance?.PlayClickSound();
        
        // 关闭画廊菜单
        CloseMenu();
        
        // 播放故事
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.PlayStoryFromGallery(identifier, () =>
            {
                // 故事播放完成或退出后，重新打开画廊菜单并更新
                // 注意：标记为已阅读的逻辑已经在StoryManager的EndStory中处理
                OpenMenu();
            });
        }
    }
    
    /// <summary>
    /// 刷新画廊显示（用于更新已阅读状态）
    /// </summary>
    public void RefreshGallery()
    {
        UpdateGallery();
    }
}

