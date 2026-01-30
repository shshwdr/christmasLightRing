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
                var coinLocalizedString = new LocalizedString("GameText", "Coin_Attribute");
                coinLocalizedString.Arguments = new object[] { mainData.coins, unrevealedCoins };
                descText = coinLocalizedString.GetLocalizedString();
                // 将 \n 替换为实际换行符
                descText = descText.Replace("\\n", "\n");
                break;
            case AttributeType.Gift:
                int unrevealedGifts = boardManager != null ? boardManager.GetUnrevealedGiftCount() : 0;
                var giftLocalizedString = new LocalizedString("GameText", "Gift_Attribute");
                giftLocalizedString.Arguments = new object[] { mainData.gifts, unrevealedGifts };
                descText = giftLocalizedString.GetLocalizedString();
                // 将 \n 替换为实际换行符
                descText = descText.Replace("\\n", "\n");
                break;
            case AttributeType.Hint:
                int unrevealedHints = boardManager != null ? boardManager.GetUnrevealedHintCount() : 0;
                int totalHints = boardManager != null ? boardManager.GetTotalHintCount() : 0;
                var hintLocalizedString = new LocalizedString("GameText", "Hint_Attribute");
                hintLocalizedString.Arguments = new object[] { unrevealedHints, totalHints };
                descText = hintLocalizedString.GetLocalizedString();
                // 将 \n 替换为实际换行符
                descText = descText.Replace("\\n", "\n");
                break;
            case AttributeType.Health:
                var healthLocalizedString = new LocalizedString("GameText", "Health_Attribute");
                healthLocalizedString.Arguments = new object[] { mainData.health };
                descText = healthLocalizedString.GetLocalizedString();
                // 将 \n 替换为实际换行符
                descText = descText.Replace("\\n", "\n");
                break;
            case AttributeType.Enemy:
                int unrevealedEnemies = boardManager != null ? boardManager.GetUnrevealedEnemyCount() : 0;
                int totalEnemies = boardManager != null ? boardManager.GetTotalEnemyCount() : 0;
                var enemyLocalizedString = new LocalizedString("GameText", "Enemy_Attribute");
                enemyLocalizedString.Arguments = new object[] { unrevealedEnemies, totalEnemies };
                descText = enemyLocalizedString.GetLocalizedString();
                // 将 \n 替换为实际换行符
                descText = descText.Replace("\\n", "\n");
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



