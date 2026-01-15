using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public enum AttributeType
{
    Coin,
    Gift,
    Hint,
    Health,
    Enemy
}

public class AttributeHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public AttributeType attributeType;
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UIManager.Instance == null || GameManager.Instance == null) return;
        
        string descText = "";
        BoardManager boardManager = GameManager.Instance.boardManager;
        MainGameData mainData = GameManager.Instance.mainGameData;
        
        switch (attributeType)
        {
            case AttributeType.Coin:
                int unrevealedCoins = boardManager != null ? boardManager.GetUnrevealedCoinCount() : 0;
                descText = $"Current: {mainData.coins}\nUnrevealed: {unrevealedCoins}\nCoins are used to purchase items.";
                break;
            case AttributeType.Gift:
                int unrevealedGifts = boardManager != null ? boardManager.GetUnrevealedGiftCount() : 0;
                descText = $"Current: {mainData.gifts}\nUnrevealed: {unrevealedGifts}\nGifts convert to coins in the shop. Enemies steal gifts.";
                break;
            case AttributeType.Hint:
                int unrevealedHints = boardManager != null ? boardManager.GetUnrevealedHintCount() : 0;
                int totalHints = boardManager != null ? boardManager.GetTotalHintCount() : 0;
                descText = $"Unrevealed: {unrevealedHints}\nTotal Hints: {totalHints}";
                break;
            case AttributeType.Health:
                descText = $"Current Health: {mainData.health}\nRestore 1 when entering the shop.";
                break;
            case AttributeType.Enemy:
                int unrevealedEnemies = boardManager != null ? boardManager.GetUnrevealedEnemyCount() : 0;
                int totalEnemies = boardManager != null ? boardManager.GetTotalEnemyCount() : 0;
                descText = $"Unrevealed: {unrevealedEnemies}\nTotal Enemies: {totalEnemies}";
                break;
        }
        
        if (!string.IsNullOrEmpty(descText))
        {
            UIManager.Instance.ShowDescText(descText);
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideDesc();
        }
    }
}



