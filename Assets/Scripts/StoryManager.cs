using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class StoryManager : MonoBehaviour
{
    public static StoryManager Instance;
    
    public GameObject storyPanel;
    public Image storyImageTop; // 上层图片（用于显示新图片）
    public Image storyImageBottom; // 下层图片（用于显示之前的图片）
    public TextMeshProUGUI storyText;
    public Button storyButton; // 点击区域，用于切换故事
    public GameObject readOB;
    
    private List<StoryInfo> currentStories = new List<StoryInfo>();
    private int currentStoryIndex = 0;
    private System.Action onStoryEndCallback;
    private bool isPlaying = false;
    private bool isFirstStory = true; // 标记是否是第一个故事
    private bool isFromGallery = false; // 标记是否从画廊播放
    private string currentStoryIdentifier = ""; // 当前播放的故事identifier
    
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
        if (storyPanel != null)
        {
            storyPanel.SetActive(false);
        }
        
        if (storyButton != null)
        {
            storyButton.onClick.AddListener(OnStoryClicked);
        }
    }
    
    private void Update()
    {
        // 检测ESC键退出故事播放
        if (isPlaying && Input.GetKeyDown(KeyCode.Escape))
        {
            EndStory();
        }
    }
    
    public void PlayStory(string identifier, System.Action onEnd = null)
    {
        if (CSVLoader.Instance == null || !CSVLoader.Instance.storyDict.ContainsKey(identifier))
        {
            Debug.LogWarning($"Story with identifier '{identifier}' not found!");
            onEnd?.Invoke();
            return;
        }
        
        currentStories = new List<StoryInfo>(CSVLoader.Instance.storyDict[identifier]);
        if (currentStories.Count == 0)
        {
            Debug.LogWarning($"Story with identifier '{identifier}' is empty!");
            onEnd?.Invoke();
            return;
        }
        bool isRead = GameManager.Instance != null && 
                      GameManager.Instance.mainGameData.GetReadStories().Contains(identifier);
        readOB.SetActive(isRead);
        
        currentStoryIndex = 0;
        onStoryEndCallback = onEnd;
        isPlaying = true;
        isFirstStory = true; // 重置为第一个故事
        isFromGallery = false; // 普通播放，不是从画廊
        currentStoryIdentifier = identifier;
        
        // 显示故事面板
        if (storyPanel != null)
        {
            storyPanel.SetActive(true);
        }
        
        // 初始化图片状态
        InitializeImages();
        
        // 播放第一个故事
        ShowCurrentStory();
    }
    
    /// <summary>
    /// 从画廊播放故事（会标记为已阅读）
    /// </summary>
    public void PlayStoryFromGallery(string identifier, System.Action onEnd = null)
    {
        if (CSVLoader.Instance == null || !CSVLoader.Instance.storyDict.ContainsKey(identifier))
        {
            Debug.LogWarning($"Story with identifier '{identifier}' not found!");
            onEnd?.Invoke();
            return;
        }
        
        currentStories = new List<StoryInfo>(CSVLoader.Instance.storyDict[identifier]);
        if (currentStories.Count == 0)
        {
            Debug.LogWarning($"Story with identifier '{identifier}' is empty!");
            onEnd?.Invoke();
            return;
        }
        
        readOB.SetActive(true);
        currentStoryIndex = 0;
        onStoryEndCallback = onEnd;
        isPlaying = true;
        isFirstStory = true; // 重置为第一个故事
        isFromGallery = true; // 从画廊播放
        currentStoryIdentifier = identifier;
        
        // 显示故事面板
        if (storyPanel != null)
        {
            storyPanel.SetActive(true);
        }
        
        // 初始化图片状态
        InitializeImages();
        
        // 播放第一个故事
        ShowCurrentStory();
    }
    
    private void InitializeImages()
    {
        // 初始化上层和下层图片
        if (storyImageTop != null)
        {
            Color topColor = storyImageTop.color;
            topColor.a = 0f;
            storyImageTop.color = topColor;
        }
        
        if (storyImageBottom != null)
        {
            Color bottomColor = storyImageBottom.color;
            bottomColor.a = 0f;
            storyImageBottom.color = bottomColor;
        }
    }
    
    private void ShowCurrentStory()
    {
        if (currentStoryIndex >= currentStories.Count)
        {
            EndStory();
            return;
        }
        
        StoryInfo story = currentStories[currentStoryIndex];
        
        // 加载新图片
        Sprite newSprite = null;
        if (!string.IsNullOrEmpty(story.image))
        {
            newSprite = Resources.Load<Sprite>("story/" + story.image);
            if (newSprite == null)
            {
                Debug.LogWarning($"Story image '{story.image}' not found!");
            }
        }
        
        if (isFirstStory)
        {
            // 第一个故事：直接在上层显示
            if (storyImageTop != null && newSprite != null)
            {
                storyImageTop.sprite = newSprite;
                storyImageTop.DOFade(1f, 0.5f).SetEase(Ease.InQuad);
            }
            isFirstStory = false;
        }
        else
        {
            // 后续故事：将上层图片移到下层，新图片在上层淡入
            if (storyImageTop != null && storyImageBottom != null)
            {
                // 如果上层有图片，将其移到下层
                if (storyImageTop.sprite != null)
                {
                    storyImageBottom.sprite = storyImageTop.sprite;
                    Color bottomColor = storyImageBottom.color;
                    bottomColor.a = 1f;
                    storyImageBottom.color = bottomColor;
                }
                
                // 设置新图片到上层并淡入
                if (newSprite != null)
                {
                    storyImageTop.sprite = newSprite;
                    Color topColor = storyImageTop.color;
                    topColor.a = 0f;
                    storyImageTop.color = topColor;
                    storyImageTop.DOFade(1f, 0.5f).SetEase(Ease.InQuad);
                }
                else
                {
                    // 如果没有新图片，淡出上层
                    storyImageTop.DOFade(0f, 0.3f);
                }
            }
        }
        
        // 设置文字（将\n替换为换行符）
        if (storyText != null)
        {
            string processedDesc = story.desc.Replace("\\n", "\n");
            storyText.text = processedDesc;
            
            // 设置初始透明度为0
            Color textColor = storyText.color;
            textColor.a = 0f;
            storyText.color = textColor;
            
            // 淡入动画
            storyText.DOFade(1f, 0.5f).SetEase(Ease.InQuad);
        }
    }
    
    private void OnStoryClicked()
    {
        if (!isPlaying) return;
        
        StoryInfo currentStory = currentStories[currentStoryIndex];
        
        // 如果当前故事是结束故事，淡出UI
        if (currentStory.isEnd)
        {
            EndStory();
        }
        else
        {
            // 切换到下一个故事
            currentStoryIndex++;
            
            // 淡出当前文字
            if (storyText != null)
            {
                storyText.DOFade(0f, 0.3f).OnComplete(() =>
                {
                    ShowCurrentStory();
                });
            }
            else
            {
                ShowCurrentStory();
            }
        }
    }
    
    private void EndStory()
    {
        isPlaying = false;
        
        // 标记为已阅读（无论是游戏中还是画廊中播放，包括ESC退出）
        if (!string.IsNullOrEmpty(currentStoryIdentifier) && GameManager.Instance != null)
        {
            GameManager.Instance.mainGameData.GetReadStories().Add(currentStoryIdentifier);
            // 同步到GameData并保存
            GameManager.Instance.mainGameData.SyncReadStories();
            Debug.Log($"Story '{currentStoryIdentifier}' marked as read. Saving game data...");
            // 保存数据
            GameManager.Instance.SaveGameData();
        }
        
        // 淡出整个面板
        if (storyPanel != null)
        {
            CanvasGroup canvasGroup = storyPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = storyPanel.AddComponent<CanvasGroup>();
            }
            
            canvasGroup.DOFade(0f, 0.5f).OnComplete(() =>
            {
                storyPanel.SetActive(false);
                canvasGroup.alpha = 1f; // 重置透明度
                
                // 重置图片状态
                InitializeImages();
                
                // 执行回调
                onStoryEndCallback?.Invoke();
                onStoryEndCallback = null;
            });
        }
        else
        {
            // 如果没有面板，直接执行回调
            InitializeImages();
            onStoryEndCallback?.Invoke();
            onStoryEndCallback = null;
        }
    }
    
    public bool IsPlaying()
    {
        return isPlaying;
    }
}

