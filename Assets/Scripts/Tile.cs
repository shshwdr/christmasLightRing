using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Tile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image backgroundImage;
    public Image frontImage;
    public Image backImage;
    public Image revealableImage;
    
    private int row;
    private int col;
    private CardType cardType;
    private bool isRevealed = false;
    private bool isRevealable = false;
    private Button button;
    
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
        isRevealed = revealed;
        UpdateVisual();
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
    }
    
    public void SetFrontSprite(Sprite sprite)
    {
        if (frontImage != null)
        {
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
            else
            {
                frontImage.sprite = sprite;
                frontImage.gameObject.SetActive(true);
                backgroundImage.sprite = Resources.Load<Sprite>($"icon/cardgreen");
                
                // 如果是Sign卡，重置角度（箭头图片初始角度是向右，即0度）
                if (cardType == CardType.Sign && frontImage != null)
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
            if (cardType == CardType.Hint)
            {
                GameManager.Instance.ShowHint(row, col);
            }
            else if (GameManager.Instance.IsUsingFlashlight())
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
}

