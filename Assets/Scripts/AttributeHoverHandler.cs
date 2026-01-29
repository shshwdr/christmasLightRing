using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.Localization;

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
                var coinLocalizedString = new LocalizedString("GameText", "Current: {0}\nUnrevealed: {1}\nCoins are used to purchase items.");
                coinLocalizedString.Arguments = new object[] { mainData.coins, unrevealedCoins };
                descText = coinLocalizedString.GetLocalizedString();
                break;
            case AttributeType.Gift:
                int unrevealedGifts = boardManager != null ? boardManager.GetUnrevealedGiftCount() : 0;
                var giftLocalizedString = new LocalizedString("GameText", "Current: {0}\nUnrevealed: {1}\nGifts convert to coins in the shop. Enemies steal gifts.");
                giftLocalizedString.Arguments = new object[] { mainData.gifts, unrevealedGifts };
                descText = giftLocalizedString.GetLocalizedString();
                break;
            case AttributeType.Hint:
                int unrevealedHints = boardManager != null ? boardManager.GetUnrevealedHintCount() : 0;
                int totalHints = boardManager != null ? boardManager.GetTotalHintCount() : 0;
                var hintLocalizedString = new LocalizedString("GameText", "Unrevealed: {0}\nTotal Hints: {1}");
                hintLocalizedString.Arguments = new object[] { unrevealedHints, totalHints };
                descText = hintLocalizedString.GetLocalizedString();
                break;
            case AttributeType.Health:
                var healthLocalizedString = new LocalizedString("GameText", "Current Health: {0}\nRestore 1 when entering the shop.");
                healthLocalizedString.Arguments = new object[] { mainData.health };
                descText = healthLocalizedString.GetLocalizedString();
                break;
            case AttributeType.Enemy:
                int unrevealedEnemies = boardManager != null ? boardManager.GetUnrevealedEnemyCount() : 0;
                int totalEnemies = boardManager != null ? boardManager.GetTotalEnemyCount() : 0;
                var enemyLocalizedString = new LocalizedString("GameText", "Unrevealed: {0}\nTotal Enemies: {1}");
                enemyLocalizedString.Arguments = new object[] { unrevealedEnemies, totalEnemies };
                descText = enemyLocalizedString.GetLocalizedString();
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



