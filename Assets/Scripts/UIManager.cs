using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    
    public TextMeshProUGUI coinsText;
    public TextMeshProUGUI giftsText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI flashlightsText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI sceneText; // 场景名称显示
    public TextMeshProUGUI hintText;
    public TextMeshProUGUI enemyCountText;
    public TextMeshProUGUI hintCountText; // hint数量显示
    
    public Button flashlightButton;
    public Button bellButton;
    public Button deckButton;
    public Button settingsButton; // 设置按钮
    
    public GameObject hintPanel;
    public Button hintPanelButton;
    
    public GameObject descPanel;
    public Vector2 descOffset;
    public TextMeshProUGUI descText;
    [Tooltip("Hover显示描述前的延迟时间（秒）")]
    public float descHoverDelay = 1f; // hover延迟时间，可在inspector中配置
    
    public GameObject tutorialPanel;
    public TextMeshProUGUI tutorialText;
    public Button tutorialPanelButton;
    [SerializeField]
    [Tooltip("是否允许显示教程面板")]
    private bool enableTutorialPanel = true; // 可以通过Inspector控制是否显示tutorialPanel
    
    public GameObject bossDescPanel; // boss描述面板，只在boss关卡显示
    public TextMeshProUGUI bossDescText; // boss描述文本
    
    public GameObject bossIcon; // boss图标，只在boss关卡显示
    public GameObject bossIconInteractableObject; // bossIcon的可交互提示对象
    public GameObject bellButtonInteractableObject; // bellButton的可交互提示对象
    
    public DeckMenu deckMenu; // Deck菜单组件
    
    public UpgradeDisplaySlot[] upgradeSlots = new UpgradeDisplaySlot[5];
    
    public GameObject floatingTextPrefab; // 漂浮字prefab
    
    public Image bkImage; // 背景图片
    
    // 延迟显示相关的私有变量
    private Coroutine descDelayCoroutine;
    private Vector2 lastHoverPosition;
    private bool isHovering = false;
    private CardType pendingCardType;
    private string pendingDescText;
    private bool isPendingCardType = false; // true表示pending的是CardType，false表示pending的是string
    
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
        if (hintPanel != null)
        {
            hintPanel.SetActive(false);
        }
        
        
        if (descPanel != null)
        {
            descPanel.SetActive(false);
        }
        
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
        
        if (bossDescPanel != null)
        {
            bossDescPanel.SetActive(false);
        }
        
        if (bossIcon != null)
        {
            bossIcon.SetActive(false);
        }
        
        if (hintPanelButton != null)
        {
            hintPanelButton.onClick.AddListener(OnHintPanelClicked);
        }
        
        if (tutorialPanelButton != null)
        {
            tutorialPanelButton.onClick.AddListener(OnTutorialPanelClicked);
        }
        
        if (flashlightButton != null)
        {
            flashlightButton.onClick.AddListener(OnFlashlightButtonClicked);
            
            // 添加FlashlightButtonHandler组件（如果还没有）
            if (flashlightButton.GetComponent<FlashlightButtonHandler>() == null)
            {
                flashlightButton.gameObject.AddComponent<FlashlightButtonHandler>();
            }
        }
        
        if (bellButton != null)
        {
            bellButton.onClick.AddListener(OnBellButtonClicked);
            bellButton.gameObject.SetActive(false); // 初始隐藏
            
            // 添加BellButtonHandler组件（如果还没有）
            if (bellButton.GetComponent<BellButtonHandler>() == null)
            {
                bellButton.gameObject.AddComponent<BellButtonHandler>();
            }
        }
        
        if (deckButton != null)
        {
            deckButton.onClick.AddListener(OnDeckButtonClicked);
        }
        
        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        }
        
        // if (retryBossButton != null)
        // {
        //     retryBossButton.onClick.AddListener(OnRetryBossButtonClicked);
        //     retryBossButton.gameObject.SetActive(false); // 初始隐藏
        // }
    }
    
    private void OnRetryBossButtonClicked()
    {
        GameManager.Instance?.RetryBoss();
    }
    
    public void ShowBellButton()
    {
        if (bellButton != null)
        {
            bellButton.gameObject.SetActive(true);
            // 显示时设为可点击
            SetBellButtonInteractable(true);
        }
    }
    
    public void HideBellButton()
    {
        if (bellButton != null)
        {
            bellButton.gameObject.SetActive(false);
        }
        
        // 隐藏interactableObject
        if (bellButtonInteractableObject != null)
        {
            bellButtonInteractableObject.SetActive(false);
        }
    }
    
    public void UpdateUI()
    {
        if (GameManager.Instance == null) return;
        
        
        MainGameData mainData = GameManager.Instance.mainGameData;
        BoardManager boardManager = GameManager.Instance.boardManager;
        
        // 更新金币显示：x(y)格式，y是未翻开的金币数量
        if (coinsText != null)
        {
            int unrevealedCoins = boardManager != null ? boardManager.GetUnrevealedCoinCount() : 0;
            coinsText.text = $"{mainData.coins}({unrevealedCoins})";
        }
        
        // 更新礼物显示：x(y)格式，y是未翻开的礼物数量
        if (giftsText != null)
        {
            int unrevealedGifts = boardManager != null ? boardManager.GetUnrevealedGiftCount() : 0;
            giftsText.text = $"{mainData.gifts}({unrevealedGifts})";
        }
        
        if (healthText != null)
            healthText.text = $"{mainData.health}";
        if (flashlightsText != null)
            flashlightsText.text = $"{mainData.flashlights}";
        
        // 更新场景名称显示
        if (sceneText != null)
        {
            string currentScene = mainData.currentScene;
            if (!string.IsNullOrEmpty(currentScene) && CSVLoader.Instance != null)
            {
                SceneInfo sceneInfo = CSVLoader.Instance.sceneInfos.Find(s => s.identifier == currentScene);
                if (sceneInfo != null)
                {
                    // 从 Localization 获取场景名称
                    string sceneNameKey = "sceneName_" + sceneInfo.identifier;
                    var localizedString = new LocalizedString("GameText", sceneNameKey);
                    var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(localizedString.TableReference, localizedString.TableEntryReference);
                    sceneText.text = handle.WaitForCompletion();
                }
                else
                {
                    sceneText.text = currentScene;
                }
            }
            else
            {
                sceneText.text = "";
            }
        }
        
        // 更新关卡显示（显示scene中的level序号，不是全局level序号）
        if (levelText != null)
        {
            string currentScene = mainData.currentScene;
            int sceneLevelNumber = 1; // 默认值
            
            if (!string.IsNullOrEmpty(currentScene) && CSVLoader.Instance != null && LevelManager.Instance != null)
            {
                // 获取当前scene的所有关卡索引
                List<int> sceneLevelIndices = LevelManager.Instance.GetLevelIndicesForScene(currentScene);
                
                // 找到当前level在scene中的位置
                int currentLevelIndex = mainData.currentLevel - 1; // 转换为0-based索引
                int sceneLevelIndex = sceneLevelIndices.IndexOf(currentLevelIndex);
                
                if (sceneLevelIndex >= 0)
                {
                    sceneLevelNumber = sceneLevelIndex + 1; // 转换为1-based序号
                }
            }
            
            levelText.text = $"LV {sceneLevelNumber}";
        }
        
        UpdateFlashlightButton();
        UpdateEnemyCount();
        UpdateHintCount();
    }
    
    public void UpdateEnemyCount()
    {
        if (enemyCountText == null || GameManager.Instance == null || GameManager.Instance.boardManager == null) return;
        
        int revealedEnemies = GameManager.Instance.boardManager.GetRevealedEnemyCount();
        int totalEnemies = GameManager.Instance.boardManager.GetTotalEnemyCount();
        
        enemyCountText.text = $"{revealedEnemies}/{totalEnemies}";
    }
    
    public void UpdateHintCount()
    {
        if (hintCountText == null || GameManager.Instance == null || GameManager.Instance.boardManager == null) return;
        
        
        int unrevealedHints = GameManager.Instance.boardManager.GetUnrevealedHintCount();
        int totalHints = GameManager.Instance.boardManager.GetTotalHintCount();
        
        hintCountText.text = $"{unrevealedHints}/{totalHints}";
    }
    
    public void UpdateFlashlightButton()
    {
        if (flashlightButton != null && GameManager.Instance != null)
        {
            // 如果在商店中，禁用flashlight按钮
            bool isInShop = ShopManager.Instance != null && 
                           ShopManager.Instance.shopPanel != null && 
                           ShopManager.Instance.shopPanel.activeSelf;
            
            if (isInShop)
            {
                flashlightButton.interactable = false;
            }
            else
            {
                bool canUse = GameManager.Instance.mainGameData.flashlights > 0 && 
                             !GameManager.Instance.IsUsingFlashlight();
                flashlightButton.interactable = canUse;
            }
        }
    }
    
    public void OnFlashlightButtonClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        GameManager.Instance?.UseFlashlight();
    }
    
    public void OnBellButtonClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        TutorialManager.Instance.ShowTutorial("nextLevel");
        // 在离开board前，先reveal所有未翻开的卡牌
        if (GameManager.Instance != null)
        {
            SFXManager.Instance?.PlayCardRevealSound("bell");
            GameManager.Instance.RevealAllCardsBeforeLeaving(() =>
            {
                GameManager.Instance?.EndTurn();
            });
        }
        else
        {
            GameManager.Instance?.EndTurn();
        }
    }
    
    public void ShowHint(string hint)
    {
        if (hintText != null)
        {
            hintText.text = hint;
        }
        if (hintPanel != null)
        {
            // 如果已经显示，则隐藏；否则显示
            bool isActive = hintPanel.activeSelf;
            hintPanel.SetActive(!isActive);
        }
    }
    
    public void HideHint()
    {
        if (hintPanel != null)
        {
            hintPanel.SetActive(false);
        }
    }
    
    private void OnHintPanelClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        HideHint();
    }
    
    public Button retryBossButton; // retry boss按钮
    
    public void ShowGameOver(bool showRetryBoss = false)
    {
        LoseMenu.Instance.ShowLoseMenu(showRetryBoss);
        
        
    }
    
    public void HideGameOver()
    {
        LoseMenu.Instance.HideLoseMenu();
    }
    
    public void ShowDesc(CardType cardType)
    {
        if (CardInfoManager.Instance == null) return;
        
        Vector2 currentMousePos = Input.mousePosition;
        
        // 如果鼠标位置改变，重置计时器
        if (isHovering && Vector2.Distance(currentMousePos, lastHoverPosition) > 10f)
        {
            // 位置改变，重置计时器
            if (descDelayCoroutine != null)
            {
                StopCoroutine(descDelayCoroutine);
                descDelayCoroutine = null;
            }
        }
        
        // 更新hover状态和位置
        isHovering = true;
        lastHoverPosition = currentMousePos;
        pendingCardType = cardType;
        isPendingCardType = true;
        
        // 如果协程没有运行，启动新的协程
        if (descDelayCoroutine == null)
        {
            descDelayCoroutine = StartCoroutine(ShowDescDelayed());
        }
    }
    
    private IEnumerator ShowDescDelayed()
    {
        float elapsedTime = 0f;
        Vector2 referencePosition = lastHoverPosition; // 记录开始时的位置作为参考
        
        // 在等待期间持续检查位置
        while (elapsedTime < descHoverDelay && isHovering)
        {
            Vector2 currentMousePos = Input.mousePosition;
            
            // 如果位置改变超过阈值，重置计时器
            if (Vector2.Distance(currentMousePos, referencePosition) > 10f)
            {
                // 位置改变，重置计时器并更新参考位置
                referencePosition = currentMousePos;
                lastHoverPosition = currentMousePos;
                elapsedTime = 0f;
            }
            
            yield return null;
            elapsedTime += Time.deltaTime;
        }
        
        // 延迟时间到了，检查是否还在hover且位置没有大幅改变
        if (isHovering)
        {
            Vector2 currentMousePos = Input.mousePosition;
            // 检查最终位置是否还在参考位置附近
            if (Vector2.Distance(currentMousePos, referencePosition) <= 10f)
            {
                // 显示描述
                if (isPendingCardType)
                {
                    ShowDescImmediate(pendingCardType);
                }
                else
                {
                    ShowDescTextImmediate(pendingDescText);
                }
            }
        }
        
        descDelayCoroutine = null;
    }
    
    private void ShowDescImmediate(CardType cardType)
    {
        if (CardInfoManager.Instance == null) return;
        
        CardInfo cardInfo = CardInfoManager.Instance.GetCardInfo(cardType);
        if (cardInfo != null)
        {
            if (descText != null)
            {
                // 从 Localization 获取卡牌名称
                string nameKey = "cardName_" + cardInfo.identifier;
                var nameLocalizedString = new LocalizedString("GameText", nameKey);
                var nameHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(nameLocalizedString.TableReference, nameLocalizedString.TableEntryReference);
                string localizedName = nameHandle.WaitForCompletion();
                
                // 从 Localization 获取卡牌描述
                string descKey = "cardDesc_" + cardInfo.identifier;
                var descLocalizedString = new LocalizedString("GameText", descKey);
                var descHandle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(descLocalizedString.TableReference, descLocalizedString.TableEntryReference);
                string localizedDesc = descHandle.WaitForCompletion();
                
                string text = $"{localizedName}\n{localizedDesc}";
                descText.text = text;
            }
            if (descPanel != null)
            {
                // 停止之前的淡出动画
                CanvasGroup canvasGroup = descPanel.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = descPanel.AddComponent<CanvasGroup>();
                }
                else
                {
                    // 停止所有正在进行的动画
                    canvasGroup.DOKill();
                }
                
                descPanel.SetActive(true);
                // 更新位置到鼠标位置
                UpdateDescPosition();
                
                // 如果已经显示，直接设置为不透明，否则淡入
                if (canvasGroup.alpha > 0.9f)
                {
                    canvasGroup.alpha = 1f;
                }
                else
                {
                    canvasGroup.alpha = 0f;
                    canvasGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuad);
                }
            }
        }
    }
    
    // 显示自定义描述文本（用于attribute hover）
    public void ShowDescText(string text)
    {
        Vector2 currentMousePos = Input.mousePosition;
        
        // 如果鼠标位置改变，重置计时器
        if (isHovering && Vector2.Distance(currentMousePos, lastHoverPosition) > 10f)
        {
            // 位置改变，重置计时器
            if (descDelayCoroutine != null)
            {
                StopCoroutine(descDelayCoroutine);
                descDelayCoroutine = null;
            }
        }
        
        // 更新hover状态和位置
        isHovering = true;
        lastHoverPosition = currentMousePos;
        pendingDescText = text;
        isPendingCardType = false;
        
        // 如果协程没有运行，启动新的协程
        if (descDelayCoroutine == null)
        {
            descDelayCoroutine = StartCoroutine(ShowDescDelayed());
        }
    }
    
    private void ShowDescTextImmediate(string text)
    {
        if (descText != null)
        {
            descText.text = text;
        }
        if (descPanel != null)
        {
            // 停止之前的淡出动画
            CanvasGroup canvasGroup = descPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = descPanel.AddComponent<CanvasGroup>();
            }
            else
            {
                // 停止所有正在进行的动画
                canvasGroup.DOKill();
            }
            
            descPanel.SetActive(true);
            // 更新位置到鼠标位置
            UpdateDescPosition();
            
            // 如果已经显示，直接设置为不透明，否则淡入
            if (canvasGroup.alpha > 0.9f)
            {
                canvasGroup.alpha = 1f;
            }
            else
            {
                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuad);
            }
        }
    }
    
    private void UpdateDescPosition()
    {
        if (descPanel != null)
        {
            RectTransform rect = descPanel.GetComponent<RectTransform>();
            if (rect != null)
            {
                Vector2 mousePos = Input.mousePosition;
                rect.position = mousePos + descOffset;
            }
        }
    }
    
    
    public void HideDesc()
    {
        // 停止延迟协程
        if (descDelayCoroutine != null)
        {
            StopCoroutine(descDelayCoroutine);
            descDelayCoroutine = null;
        }
        
        // 重置hover状态
        isHovering = false;
        
        if (descPanel != null)
        {
            // 停止之前的淡入动画
            CanvasGroup canvasGroup = descPanel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                // 停止所有正在进行的动画
                canvasGroup.DOKill();
                
                // 淡出效果
                canvasGroup.DOFade(0f, 0.2f).SetEase(Ease.InQuad).OnComplete(() =>
                {
                    descPanel.SetActive(false);
                });
            }
            else
            {
                descPanel.SetActive(false);
            }
        }
    }
    
    public void UpdateUpgradeDisplay()
    {
        if (GameManager.Instance == null) return;
        
        List<string> ownedUpgrades = GameManager.Instance.mainGameData.ownedUpgrades;
        
        for (int i = 0; i < 5; i++)
        {
            if (upgradeSlots[i] != null)
            {
                if (i < ownedUpgrades.Count)
                {
                    upgradeSlots[i].Setup(ownedUpgrades[i]);
                }
                else
                {
                    upgradeSlots[i].ClearSlot();
                }
            }
        }
    }
    
    public void OnDeckButtonClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        if (deckMenu != null)
        {
            deckMenu.ToggleMenu();
        }
    }
    
    /// <summary>
    /// 设置按钮点击事件
    /// </summary>
    public void OnSettingsButtonClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        // 打开设置菜单
        if (SettingsMenu.Instance != null)
        {
            SettingsMenu.Instance.ToggleMenu();
        }
    }
    
    public void ShowTutorial(string desc)
    {
        // 如果禁用了tutorialPanel，则不显示
        if (!enableTutorialPanel)
        {
            return;
        }
        
        if (tutorialText != null)
        {
            // 将"\n"替换为真正的换行符
            string processedDesc = desc.Replace("\\n", "\n");
            tutorialText.text = processedDesc;
        }
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
            
            // 播放放大缩小动画
            RectTransform panelRect = tutorialPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                // 停止之前的动画
                panelRect.DOKill();
                
                // 重置缩放
                panelRect.localScale = Vector3.zero;
                
                // 创建动画序列：放大 -> 稍微缩小 -> 回到正常大小
                Sequence sequence = DOTween.Sequence();
                sequence.Append(panelRect.DOScale(Vector3.one * 1.1f, 0.2f).SetEase(Ease.OutQuad));
                sequence.Append(panelRect.DOScale(Vector3.one, 0.15f).SetEase(Ease.InQuad));
            }
        }
    }
    
    public void HideTutorial()
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
    }
    
    private void OnTutorialPanelClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        HideTutorial();
    }
    
    public void ShowBossDesc(string bossName, string bossDesc)
    {
        if (bossDescPanel != null)
        {
            bossDescPanel.SetActive(true);
        }
        
        if (bossDescText != null)
        {
            string text = $"{bossName}\n\n{bossDesc}";
            bossDescText.text = text;
        }
    }
    
    public void HideBossDesc()
    {
        if (bossDescPanel != null)
        {
            bossDescPanel.SetActive(false);
        }
    }
    
    public void ShowBossIcon(CardType bossCardType)
    {
        if (bossIcon != null)
        {
            bossIcon.SetActive(true);
            
            // 设置bossIcon的CardType
            BossIcon bossIconComponent = bossIcon.GetComponent<BossIcon>();
            if (bossIconComponent == null)
            {
                bossIconComponent = bossIcon.AddComponent<BossIcon>();
            }
            bossIconComponent.Setup(bossCardType);
            
            // 初始状态设为不可点击
            SetBossIconInteractable(false);
        }
    }
    
    public void HideBossIcon()
    {
        if (bossIcon != null)
        {
            bossIcon.SetActive(false);
        }
        
        // 隐藏interactableObject
        if (bossIconInteractableObject != null)
        {
            bossIconInteractableObject.SetActive(false);
        }
    }
    
    public void SetBossIconInteractable(bool interactable)
    {
        if (bossIcon != null)
        {
            BossIcon bossIconComponent = bossIcon.GetComponent<BossIcon>();
            if (bossIconComponent != null)
            {
                bossIconComponent.SetInteractable(interactable);
            }
        }
        
        // 设置interactableObject的active状态
        if (bossIconInteractableObject != null)
        {
            bossIconInteractableObject.SetActive(interactable);
        }
    }
    
    public void SetBellButtonInteractable(bool interactable)
    {
        if (bellButton != null)
        {
            bellButton.interactable = interactable;

        }
        
        // 设置interactableObject的active状态
        if (bellButtonInteractableObject != null)
        {
            bellButtonInteractableObject.SetActive(interactable);
        }
    }
    
    // 触发指定upgrade的放大缩小动画
    public void TriggerUpgradeAnimation(string upgradeIdentifier)
    {
        if (upgradeSlots == null) return;
        
        // 遍历所有upgrade slots，找到显示该upgrade的slot并播放动画
        foreach (UpgradeDisplaySlot slot in upgradeSlots)
        {
            if (slot != null && slot.IsDisplayingUpgrade(upgradeIdentifier))
            {
                slot.PlayPulseAnimation();
                break; // 只触发第一个找到的slot
            }
        }
    }

    public Transform allAttributeTransform;
    
    // 浮动文本队列系统
    private Dictionary<string, Queue<FloatingTextRequest>> floatingTextQueues = new Dictionary<string, Queue<FloatingTextRequest>>();
    private Dictionary<string, Coroutine> floatingTextCoroutines = new Dictionary<string, Coroutine>();
    
    // 浮动文本请求数据结构
    private class FloatingTextRequest
    {
        public int changeAmount;
        public RectTransform targetRect;
    }
    
    // 显示漂浮字效果（加入队列）
    public void ShowFloatingText(string resourceType, int changeAmount, RectTransform targetRect)
    {
        if (floatingTextPrefab == null || targetRect == null) return;
        if (changeAmount == 0) return; // 没有变化，不显示
        
        // 确保该资源类型的队列存在
        if (!floatingTextQueues.ContainsKey(resourceType))
        {
            floatingTextQueues[resourceType] = new Queue<FloatingTextRequest>();
        }
        
        // 将请求加入队列
        floatingTextQueues[resourceType].Enqueue(new FloatingTextRequest
        {
            changeAmount = changeAmount,
            targetRect = targetRect
        });
        
        // 如果该资源类型的协程没有运行，启动它
        if (!floatingTextCoroutines.ContainsKey(resourceType) || floatingTextCoroutines[resourceType] == null)
        {
            floatingTextCoroutines[resourceType] = StartCoroutine(ProcessFloatingTextQueue(resourceType));
        }
    }
    
    // 处理浮动文本队列的协程
    private IEnumerator ProcessFloatingTextQueue(string resourceType)
    {
        while (floatingTextQueues.ContainsKey(resourceType) && floatingTextQueues[resourceType].Count > 0)
        {
            // 从队列中取出一个请求
            FloatingTextRequest request = floatingTextQueues[resourceType].Dequeue();
            
            // 显示浮动文本
            ShowFloatingTextInternal(resourceType, request.changeAmount, request.targetRect);
            
            // 等待0.2秒
            yield return new WaitForSeconds(0.35f);
        }
        
        // 队列处理完毕，清除协程引用
        if (floatingTextCoroutines.ContainsKey(resourceType))
        {
            floatingTextCoroutines[resourceType] = null;
        }
    }
    
    // 实际显示浮动文本的内部方法
    private void ShowFloatingTextInternal(string resourceType, int changeAmount, RectTransform targetRect)
    {
        if (floatingTextPrefab == null || targetRect == null) return;
        
        // 获取Canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        
        // 实例化prefab
        GameObject floatingObj = Instantiate(floatingTextPrefab, allAttributeTransform);
        FloatingText floatingText = floatingObj.GetComponentInChildren<FloatingText>();
        if (floatingText == null) return;
        
        // 确定文本内容和颜色
        string text = "";
        Color color = Color.white;
        
        if (changeAmount > 0)
        {
            text = $"+{changeAmount}";
            color = Color.green;
        }
        else if (changeAmount < 0)
        {
            text = $"{changeAmount}";
            color = Color.red;
        }
        else
        {
            return; // 没有变化，不显示
        }
        
        // 获取目标位置（右侧）
        Vector2 targetPosition = targetRect.transform.position;
        Vector2 startPosition = new Vector2(targetPosition.x + targetRect.rect.width * 0.5f + 50f, targetPosition.y);
        
        // 显示漂浮字
        floatingText.Show(text, color, targetPosition);
    }
    
    // 更新背景图片
    public void UpdateBackgroundImage()
    {
        if (bkImage == null) return;
        
        // 获取当前关卡信息
        LevelInfo levelInfo = LevelManager.Instance?.GetCurrentLevelInfo();
        if (levelInfo == null) return;
        
        // 如果map有值，则加载对应的背景图片
        if (!string.IsNullOrEmpty(levelInfo.map))
        {
            string resourcePath = $"bk/{levelInfo.map}";
            Sprite backgroundSprite = Resources.Load<Sprite>(resourcePath);
            
            if (backgroundSprite != null)
            {
                bkImage.sprite = backgroundSprite;
            }
            else
            {
                Debug.LogWarning($"Background image not found at path: {resourcePath}");
            }
        }
    }
}


