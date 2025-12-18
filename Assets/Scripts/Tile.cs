using UnityEngine;
using UnityEngine.UI;

public class Tile : MonoBehaviour
{
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
        if (frontImage != null)
        {
            frontImage.gameObject.SetActive(isRevealed);
        }
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
            frontImage.sprite = sprite;
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
}

