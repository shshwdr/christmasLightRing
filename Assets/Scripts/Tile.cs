using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

public class Tile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDropHandler
{
    public Image backgroundImage;
    public Image frontImage;
    public Image backImage;
    public Image revealableImage;
    public Image frontEffect; // 翻开时的特效
    [Header("场景格子特效（mist/frozen）")]
    public GameObject mist;   // 迷雾：该格为迷雾时显示，其下敌人不被hint观测
    public GameObject frozen; // 寒冰：该格为寒冰时显示，翻开寒冰超过阈值后每次翻开扣血
    [Header("寒冰模式：仅在 player 格显示")]
    public GameObject frozenData;   // 寒冰模式下 player 格显示的 GameObject
    public TMP_Text frozenDataText; // 显示 翻开的寒冰格子/frozenDamageThreshold
    [Header("竞速模式：仅在 player 格显示")]
    public ProgressBar progressBar; // 竞速模式倒计时条
    
    private int row;
    private int col;
    private CardType cardType;
    private bool isRevealed = false;
    private bool isRevealable = false;
    private Button button;
    public TMP_Text hintText;
    private Tween revealableHoverTween; // 用于存储revealableImage的hover动画
    private List<Tween> relatedHintTweens = new List<Tween>(); // 用于存储相关hint的hover动画
    private Canvas tileCanvas; // 用于存储Tile的Canvas组件
    private int originalSortOrder = 0; // 存储原始的sort order
    
    // 设置Canvas的sort order
    public void SetCanvasSortOrder(int sortOrder)
    {
        if (tileCanvas == null)
        {
            tileCanvas = GetComponent<Canvas>();
            if (tileCanvas == null)
            {
                tileCanvas = gameObject.AddComponent<Canvas>();
                tileCanvas.overrideSorting = true;
                originalSortOrder = 0;
            }
            else
            {
                if (!tileCanvas.overrideSorting)
                {
                    tileCanvas.overrideSorting = true;
                }
                originalSortOrder = tileCanvas.sortingOrder;
            }
        }
        tileCanvas.sortingOrder = sortOrder;
    }
    
    // 恢复Canvas的sort order
    public void ResetCanvasSortOrder()
    {
        if (tileCanvas != null)
        {
            tileCanvas.sortingOrder = 1;
        }
    }
    public void Initialize(int r, int c, CardType type, bool revealed = false)
    {
        row = r;
        col = c;
        cardType = type;
        isRevealed = revealed;
        button = GetComponent<Button>();
        
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnTileClicked);
        }
        
        UpdateVisual();
    }

    public void UpdateType(CardType type)
    {
        
        cardType = type;
        UpdateVisual();
        
        Sprite frontSprite = BoardManager.Instance.GetSpriteForCardType(cardType);
        if (frontSprite == null)
        {
            frontSprite = CardInfoManager.Instance.GetCardSprite(CardType.Blank);
        }
        SetFrontSprite(frontSprite);
    }
    
    public void SetRevealed(bool revealed)
    {
        bool wasRevealed = isRevealed;
        isRevealed = revealed;
        UpdateVisual();
        
        // 如果是从未revealed变为revealed，触发frontEffect动画；迷雾格下为敌人时保留迷雾视觉与 hint 语义
        if (!wasRevealed && revealed)
        {
            PlayFrontEffectAnimation(false);
            if (!ShouldKeepMistVisibleWhenRevealed())
                FadeOutMist();
            else
                ApplyMistOverlayForRevealedEnemyUnderMist();
        }
    }
    
    /// <summary> 迷雾格上翻开敌人后仍显示迷雾层（与 BoardManager.IsMistTile + IsEnemy 一致） </summary>
    private bool ShouldKeepMistVisibleWhenRevealed()
    {
        if (BoardManager.Instance == null || CardInfoManager.Instance == null)
            return false;
        return BoardManager.Instance.IsMistTile(row, col) && CardInfoManager.Instance.IsEnemyCard(cardType);
    }
    
    private void ApplyMistOverlayForRevealedEnemyUnderMist()
    {
        if (mist == null) return;
        CanvasGroup cg = mist.GetComponent<CanvasGroup>();
        if (cg == null) cg = mist.AddComponent<CanvasGroup>();
        cg.DOKill();
        mist.SetActive(true);
        cg.alpha = 1f;
    }
    
    public void SetRevealable(bool revealable)
    {
        isRevealable = revealable;
        UpdateVisual();
    }
    
    /// <summary> 设置是否为迷雾格子。未揭示时用 FadeIn；已揭示时仅「敌人+迷雾格」保留遮罩，其余驱散。 </summary>
    public void SetMist(bool active)
    {
        if (mist == null) return;
        CanvasGroup cg = mist.GetComponent<CanvasGroup>();
        if (cg == null) cg = mist.AddComponent<CanvasGroup>();
        cg.DOKill();
        if (!active)
        {
            cg.alpha = 0f;
            mist.SetActive(false);
            return;
        }
        if (isRevealed && !ShouldKeepMistVisibleWhenRevealed())
        {
            cg.alpha = 0f;
            mist.SetActive(false);
            return;
        }
        mist.SetActive(true);
        if (isRevealed && ShouldKeepMistVisibleWhenRevealed())
        {
            cg.alpha = 1f;
            return;
        }
        cg.alpha = 0f;
        cg.DOFade(1f, 0.3f).SetEase(Ease.InQuad);
    }
    
    /// <summary> 格子被揭示时驱散迷雾（DOTween FadeOut），player/教堂自动翻开也会触发 </summary>
    public void FadeOutMist()
    {
        if (mist == null || !mist.activeSelf) return;
        CanvasGroup cg = mist.GetComponent<CanvasGroup>();
        if (cg == null) cg = mist.AddComponent<CanvasGroup>();
        cg.DOKill();
        cg.DOFade(0f, 0.3f).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            if (mist != null) mist.SetActive(false);
        });
    }
    
    /// <summary> 设置是否为寒冰格子（显示/隐藏 frozen GameObject） </summary>
    public void SetFrozen(bool active)
    {
        if (frozen != null)
            frozen.SetActive(active);
    }
    
    /// <summary> 寒冰模式：更新 player 格上的 frozenData 文本（翻开数/阈值），仅当本格为 Player 且场景为 frozen 时显示 </summary>
    public void UpdateFrozenDataText(int frozenRevealedCount, int frozenDamageThreshold, bool isFrozenScene)
    {
        if (frozenData == null) return;
        if (cardType != CardType.Player || !isFrozenScene)
        {
            frozenData.SetActive(false);
            return;
        }
        frozenData.SetActive(true);
        if (frozenDataText != null)
        {
            frozenDataText.text = $"{frozenRevealedCount}/{frozenDamageThreshold}";
            bool reached = frozenRevealedCount >= frozenDamageThreshold;
            // 颜色：未达阈值用默认色，达到或超过阈值用红色
            frozenDataText.color = reached ? new Color(0.78f, 0.21f, 0.26f) : new Color(0.96f, 0.82f, 0.45f);
        }
    }
    
    public void UpdateVisual()
    {
        // if (frontImage != null)
        // {
        //     frontImage.gameObject.SetActive(isRevealed);
        // }
        if (backImage != null)
        {
            backImage.gameObject.SetActive(!isRevealed);
        }
        if (revealableImage != null)
        {
            bool shouldBeActive = !isRevealed && isRevealable;
            revealableImage.gameObject.SetActive(shouldBeActive);
            
            // 如果状态改变，重置缩放并停止动画
            if (shouldBeActive)
            {
                // 停止之前的动画（如果有）
                if (revealableHoverTween != null && revealableHoverTween.IsActive())
                {
                    
                    revealableHoverTween.Kill();
                }
                // 确保缩放为原始大小
                revealableImage.transform.localScale = Vector3.one;
            }
        }
        
        // 已翻开的tile（除了hint）在非手电筒状态下禁用button
        if (button != null)
        {
            if (isRevealed && cardType != CardType.Hint)
            {
                // 如果使用手电筒，允许点击以退出手电筒状态
                bool usingFlashlight = false;
                if (GameManager.Instance != null)
                {
                    usingFlashlight = GameManager.Instance.IsUsingFlashlight();
                }
                button.interactable = usingFlashlight;
                
            }
            else
            {
                button.interactable = true;
            }
        }
        
        if(cardType == CardType.Hint)
            hintText.text = FindObjectOfType<BoardManager>().GetHintContent(row, col);
    }
    
    public void SetFrontSprite(Sprite sprite)
    {
        if (hintText == null)
        {
            return;
        }
        hintText.gameObject.SetActive(false);
        if (frontImage != null)
        {
            backgroundImage.sprite = Resources.Load<Sprite>($"icon/cardgreen");
            // 检查是否是敌人（基于isEnemy字段）
            bool isEnemy = false;
            if (CardInfoManager.Instance != null)
            {
                isEnemy = CardInfoManager.Instance.IsEnemyCard(cardType);
            }
            
            if (cardType == CardType.Blank)
            {
                //backgroundImage.sprite = sprite;
                frontImage.gameObject.SetActive(false);
            }
            else if (cardType == CardType.Iceground)
            {
                backgroundImage.sprite = sprite;
                frontImage.gameObject.SetActive(false);
            }
            else if (isEnemy)
            {
                // 所有isEnemy的卡牌显示红色底色
                frontImage.sprite = sprite;
                frontImage.gameObject.SetActive(true);
                backgroundImage.sprite = Resources.Load<Sprite>($"icon/cardred");
            }
            else if (cardType == CardType.Hint)
            {
                hintText.gameObject.SetActive(true);
                frontImage.gameObject.SetActive(false);
                
                backgroundImage.sprite = Resources.Load<Sprite>($"icon/blank1");
            }
            else
            {
                frontImage.sprite = sprite;
                frontImage.gameObject.SetActive(true);
                backgroundImage.sprite = Resources.Load<Sprite>($"icon/cardgreen");
                
                // 如果是Sign卡，重置角度（箭头图片初始角度是向右，即0度）
                //if (cardType == CardType.Sign && frontImage != null)
                {
                    frontImage.transform.rotation = Quaternion.identity;
                }
            }
        }
    }
    
    public int GetRow() => row;
    public int GetCol() => col;
    public CardType GetCardType() => cardType;
    public bool IsRevealed() => isRevealed;
    
    public void OnTileClicked()
    {
        if (GameManager.Instance == null) return;
        
        // 如果玩家输入被禁用，不允许点击
        if (GameManager.Instance.IsPlayerInputDisabled())
        {
            return;
        }
        
        if (!isRevealed)
        {
            if (GameManager.Instance.IsUsingFlashlight())
            {
                GameManager.Instance.UseFlashlightToReveal(row, col);
            }
            else if (GameManager.Instance.CanRevealTile(row, col))
            {
                GameManager.Instance.RevealTile(row, col);
            }
        }
        else
        {
            // 已翻开的tile：如果是hint，显示提示；如果使用手电筒，退出手电筒状态
            // if (cardType == CardType.Hint)
            // {
            //     GameManager.Instance.ShowHint(row, col);
            // }
            // else 
            if (GameManager.Instance.IsUsingFlashlight())
            {
                // 点击已翻开的tile时，退出手电筒状态
                GameManager.Instance.CancelFlashlight();
            }
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 已翻开时显示 desc；迷雾格未翻开时也显示 desc（仅 mistDesc）
        if (UIManager.Instance != null && (isRevealed || (BoardManager.Instance != null && BoardManager.Instance.IsMistTile(row, col))))
        {
            if (cardType == CardType.Hint)
            {
                SceneInfo sceneInfo = GameManager.Instance != null ? GameManager.Instance.GetCurrentSceneInfo() : null;
                bool isForgetHiddenHint = sceneInfo != null && sceneInfo.HasType("forget") &&
                                          hintText != null && !hintText.gameObject.activeSelf;
                if (isForgetHiddenHint)
                {
                    UIManager.Instance.ShowDescText(LocalizationHelper.GetLocalizedString("forgetHintDesc"));
                }
                else
                {
                    // 只显示具体该 hint 的内容，不使用通用 cardDesc_...（也就是 hint.desc）
                    BoardManager boardManager = FindObjectOfType<BoardManager>();
                    if (boardManager != null)
                    {
                        string hintContent = boardManager.GetHintContent(row, col);
                        if (!string.IsNullOrEmpty(hintContent))
                        {
                            UIManager.Instance.ShowDescText(hintContent);
                        }
                        else
                        {
                            // 兜底：如果没取到 hint 内容，仍然显示 CardType.Hint 的 desc
                            UIManager.Instance.ShowDesc(cardType, row, col);
                        }
                    }
                    else
                    {
                        // 兜底：如果找不到 BoardManager，仍然显示 CardType.Hint 的 desc
                        UIManager.Instance.ShowDesc(cardType, row, col);
                    }
                }
            }
            else
            {
                UIManager.Instance.ShowDesc(cardType, row, col);
            }
        }
        
        // 如果tile已经revealed，重置所有hint的大小和Canvas sort order
        if (isRevealed)
        {
            if (true) // 方便开关的if(true)
            {
                BoardManager boardManager = FindObjectOfType<BoardManager>();
                if (boardManager != null)
                {
                    boardManager.ResetAllHints();
                }
            }
        }
        
        // 如果tile还没有reveal，并且可以reveal的话，鼠标移动上去时revealable微微放大
        if (!isRevealed && isRevealable && revealableImage != null && revealableImage.gameObject.activeSelf)
        {
            // 停止之前的动画（如果有）
            if (revealableHoverTween != null && revealableHoverTween.IsActive())
            {
                revealableHoverTween.Kill();
            }
            
            // 放大到1.1倍并设置Canvas sort order
            SetCanvasSortOrder(8);
            revealableHoverTween = revealableImage.transform.DOScale(Vector3.one * 1.1f, 0.2f).SetEase(Ease.OutQuad);
        }
        
        // 如果tile还没有reveal，处理所有hint的缩放
        if (!isRevealed)
        {
            if (true) // 方便开关的if(true)
            {
                BoardManager boardManager = FindObjectOfType<BoardManager>();
                if (boardManager != null)
                {
                    List<Vector2Int> relatedHints = boardManager.GetRelatedHints(row, col);
                    List<Vector2Int> allRevealedHints = boardManager.GetAllRevealedHints();
                    
                    // 放大相关的hint
                    foreach (Vector2Int hintPos in relatedHints)
                    {
                        Tile hintTile = boardManager.GetTile(hintPos.x, hintPos.y);
                        var image = hintTile.frontImage;
                        if (hintTile != null && hintTile.IsRevealed())
                        {
                            // 停止之前的动画（如果有）
                            image.transform.DOKill();
                            
                            // 放大hint的transform并设置Canvas sort order
                            Tween hintTween = image.transform.DOScale(Vector3.one * 1.1f, 0.2f).SetEase(Ease.OutQuad);
                            hintTile.SetCanvasSortOrder(8);
                            relatedHintTweens.Add(hintTween);
                        }
                    }
                    
                    // 缩小不相关的hint
                    foreach (Vector2Int hintPos in allRevealedHints)
                    {
                        if (!relatedHints.Contains(hintPos))
                        {
                            Tile hintTile = boardManager.GetTile(hintPos.x, hintPos.y);
                            var image = hintTile.frontImage;
                            if (hintTile != null && hintTile.IsRevealed())
                            {
                                // 停止之前的动画（如果有）
                                image.transform.DOKill();
                                
                                // 缩小hint的transform并恢复Canvas sort order
                                image.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutQuad);
                                hintTile.ResetCanvasSortOrder();
                            }
                        }
                    }
                }
            }
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideDesc();
        }
        
        // 如果tile还没有reveal，并且可以reveal的话，鼠标移开时revealable缩小回去
        if (!isRevealed && isRevealable && revealableImage != null && revealableImage.gameObject.activeSelf)
        {
            // 停止之前的动画（如果有）
            if (revealableHoverTween != null && revealableHoverTween.IsActive())
            {
                revealableHoverTween.Kill();
            }
            
            // 缩小回原始大小并恢复Canvas sort order
            ResetCanvasSortOrder();
            revealableHoverTween = revealableImage.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutQuad);
        }
        
        // 恢复所有相关hint的缩放
        foreach (Tween tween in relatedHintTweens)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }
        }
        relatedHintTweens.Clear();
        
        // 恢复所有hint的transform缩放和Canvas sort order
        if (!isRevealed)
        {
            if (true) // 方便开关的if(true)
            {
                BoardManager boardManager = FindObjectOfType<BoardManager>();
                if (boardManager != null)
                {
                    List<Vector2Int> allRevealedHints = boardManager.GetAllRevealedHints();
                    foreach (Vector2Int hintPos in allRevealedHints)
                    {
                        Tile hintTile = boardManager.GetTile(hintPos.x, hintPos.y);
                        if (hintTile != null && hintTile.IsRevealed())
                        {
                            // 停止之前的动画（如果有）
                            hintTile.transform.DOKill();
                            
                            // 缩小回原始大小并恢复Canvas sort order
                            hintTile.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutQuad);
                            hintTile.ResetCanvasSortOrder();
                        }
                    }
                }
            }
        }
    }
    
    public void OnDrop(PointerEventData eventData)
    {
        if (GameManager.Instance == null) return;
        
        // 如果玩家输入被禁用，不允许使用手电筒
        if (GameManager.Instance.IsPlayerInputDisabled())
        {
            return;
        }
        
        // 检查是否正在使用flashlight
        if (GameManager.Instance.IsUsingFlashlight())
        {
            // 使用flashlight来翻开这个tile
            GameManager.Instance.UseFlashlightToReveal(row, col);
        }
    }
    
    public void UpdateSignArrow(int bellRow, int bellCol, int signRow, int signCol)
    {
        if (cardType != CardType.Sign || frontImage == null) return;
        
        int deltaRow = bellRow - signRow;
        int deltaCol = bellCol - signCol;
        
        // 计算角度（优先上下左右，如果是45度斜向，优先上下左右）
        float angle = 0f;
        
        if (deltaRow == 0 && deltaCol == 0)
        {
            // 如果sign就是bell本身，不旋转
            angle = 0f;
        }
        else if (Mathf.Abs(deltaRow) == Mathf.Abs(deltaCol))
        {
            // 45度斜向，优先上下左右（优先上下）
            if (deltaRow < 0) // bell在上方（包括右上方和左上方）
            {
                angle = 90f; // 向上
            }
            else if (deltaRow > 0) // bell在下方（包括右下方和左下方）
            {
                angle = -90f; // 向下
            }
            else if (deltaCol < 0) // bell在左方（理论上不会到这里，因为已经处理了上下）
            {
                angle = 180f; // 向左
            }
            else // bell在右方（理论上不会到这里）
            {
                angle = 0f; // 向右
            }
        }
        else
        {
            // 计算最接近的上下左右角度
            if (Mathf.Abs(deltaRow) > Mathf.Abs(deltaCol))
            {
                // 垂直方向更近
                if (deltaRow < 0) // bell在上方
                {
                    angle = 90f; // 向上
                }
                else // bell在下方
                {
                    angle = -90f; // 向下
                }
            }
            else
            {
                // 水平方向更近
                if (deltaCol < 0) // bell在左方
                {
                    angle = 180f; // 向左
                }
                else // bell在右方
                {
                    angle = 0f; // 向右
                }
            }
        }
        
        // 设置旋转角度（箭头图片初始角度是向右，即0度）
        frontImage.transform.rotation = Quaternion.Euler(0, 0, angle);
    }
    
    // 切换敌人图片（带punchScale动效）
    public void SwitchEnemySprite(Sprite newSprite, bool usePunchScale = true, bool isAttackAnimation = false, System.Action onAnimationComplete = null)
    {
        if (frontImage == null || newSprite == null) return;
        
        // 切换sprite
        frontImage.sprite = newSprite;
        
        // 使用DOTween做punchScale动效
        if (usePunchScale && frontImage.transform != null)
        {
            // 先重置scale
            frontImage.transform.localScale = Vector3.one;
            
            // 执行punchScale动画
            frontImage.transform.DOPunchScale(Vector3.one * 0.5f, 0.3f, 5, 0.5f);
        }
        
        // 如果是攻击动画（不是用灯打开的敌人），触发更大的frontEffect动画
        if (isAttackAnimation)
        {
            PlayFrontEffectAnimation(true, onAnimationComplete);
        }
        else if (onAnimationComplete != null)
        {
            // 如果不是攻击动画，但需要回调，等待punchScale动画完成
            if (usePunchScale && frontImage.transform != null)
            {
                // punchScale动画时长是0.3秒
                StartCoroutine(DelayedCallback(0.3f, onAnimationComplete));
            }
            else
            {
                // 没有动画，立即调用回调
                onAnimationComplete?.Invoke();
            }
        }
    }

    /// <summary>
    /// 变色龙动画：先显示自身图标并翻开，等待 0.2 秒，再 shake 0.3 秒，然后结束（逻辑层会在之后替换为目标牌并执行正式翻牌）。
    /// </summary>
    public IEnumerator PlayChameleonTransform(CardType targetType)
    {
        // 显示变色龙自身的正面图
        if (frontImage != null && CardInfoManager.Instance != null)
        {
            Sprite chamSprite = CardInfoManager.Instance.GetCardSprite(CardType.Chameleon);
            if (chamSprite != null)
            {
                SetFrontSprite(chamSprite);
                SetRevealed(true);
            }
        }

        yield return new WaitForSeconds(0.2f);

        // shake 一下
        if (frontImage != null && frontImage.transform != null)
        {
            frontImage.transform.localScale = Vector3.one;
            frontImage.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 5, 0.5f);
            yield return new WaitForSeconds(0.3f);
        }
    }
    
    // 延迟回调协程
    private System.Collections.IEnumerator DelayedCallback(float delay, System.Action callback)
    {
        yield return new WaitForSeconds(delay);
        callback?.Invoke();
    }
    
    // 播放frontEffect动画
    public void PlayFrontEffectAnimation(bool isLargeScale = false, System.Action onAnimationComplete = null)
    {
        if (frontEffect == null || frontImage == null || !frontImage.gameObject.activeSelf)
        {
            onAnimationComplete?.Invoke();
            return;
        }
        
        // 设置frontEffect的sprite和位置
        frontEffect.sprite = frontImage.sprite;
        frontEffect.rectTransform.position = frontImage.rectTransform.position;
        frontEffect.rectTransform.sizeDelta = frontImage.rectTransform.sizeDelta;
        
        // 设置初始状态
        frontEffect.gameObject.SetActive(true);
        frontEffect.transform.localScale = Vector3.one;
        Color effectColor = Color.white;
        effectColor.a = 1f;
        frontEffect.color = effectColor;
        
        // 根据isLargeScale决定放大倍数
        float scaleMultiplier = isLargeScale ? 4f : 1.3f;
        float duration = isLargeScale ? 0.5f : 0.4f;
        
        // 创建动画序列
        Sequence sequence = DOTween.Sequence();
        
        // 获取屏幕中心位置（Canvas的中心）
        Vector3 screenCenter = frontEffect.rectTransform.position;
        if (isLargeScale)
        {
            // 获取Canvas的中心位置
            Canvas canvas = GameManager.Instance.canvas;
            if (canvas != null)
            {
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    // Canvas的中心点就是屏幕中心
                    screenCenter = canvasRect.position;
                }
            }
            else
            {
                // 如果找不到Canvas，使用屏幕中心的世界坐标
                Canvas foundCanvas = FindObjectOfType<Canvas>();
                if (foundCanvas != null)
                {
                    RectTransform canvasRect = foundCanvas.GetComponent<RectTransform>();
                    if (canvasRect != null)
                    {
                        screenCenter = canvasRect.position;
                    }
                }
            }
        }
        
        // 放大动画
        sequence.Append(frontEffect.transform.DOScale(Vector3.one * scaleMultiplier, duration * 0.4f).SetEase(Ease.OutQuad));
        
        // 如果是large scale，同时移动到屏幕中心
        if (isLargeScale)
        {
            sequence.Join(frontEffect.rectTransform.DOMove(screenCenter, duration * 0.5f).SetEase(Ease.OutQuad));
        }
        
        // 同时fade out
        sequence.Join(frontEffect.DOFade(0f, duration).SetEase(Ease.InQuad));
        
        // 动画完成后隐藏并调用回调
        sequence.OnComplete(() => {
            if (frontEffect != null)
            {
                frontEffect.gameObject.SetActive(false);
            }
            onAnimationComplete?.Invoke();
        });
    }
}

