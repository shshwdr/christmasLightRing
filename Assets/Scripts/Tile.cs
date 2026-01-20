using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using System.Collections;

public class Tile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDropHandler
{
    public Image backgroundImage;
    public Image frontImage;
    public Image backImage;
    public Image revealableImage;
    public Image frontEffect; // 翻开时的特效
    
    private int row;
    private int col;
    private CardType cardType;
    private bool isRevealed = false;
    private bool isRevealable = false;
    private Button button;
    public TMP_Text hintText;
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
    
    public void SetRevealed(bool revealed)
    {
        bool wasRevealed = isRevealed;
        isRevealed = revealed;
        UpdateVisual();
        
        // 如果是从未revealed变为revealed，触发frontEffect动画
        if (!wasRevealed && revealed)
        {
            PlayFrontEffectAnimation(false);
        }
    }
    
    public void SetRevealable(bool revealable)
    {
        isRevealable = revealable;
        UpdateVisual();
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
            revealableImage.gameObject.SetActive(!isRevealed && isRevealable);
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
        hintText.gameObject.SetActive(false);
        if (frontImage != null)
        {
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
        // 只有当tile已翻开时才显示desc
        if (isRevealed && UIManager.Instance != null)
        {
            UIManager.Instance.ShowDesc(cardType);
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideDesc();
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
    public void SwitchEnemySprite(Sprite newSprite, bool usePunchScale = true, bool isAttackAnimation = false)
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
            PlayFrontEffectAnimation(true);
        }
    }
    
    // 播放frontEffect动画
    public void PlayFrontEffectAnimation(bool isLargeScale = false)
    {
        if (frontEffect == null || frontImage == null || !frontImage.gameObject.activeSelf) return;
        
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
        
        // 动画完成后隐藏
        sequence.OnComplete(() => {
            if (frontEffect != null)
            {
                frontEffect.gameObject.SetActive(false);
            }
        });
    }
}

