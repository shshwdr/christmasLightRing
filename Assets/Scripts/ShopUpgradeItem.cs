using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopUpgradeItem : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI costText;
    public Button buyButton;
    public GameObject content; // content的GameObject，购买后隐藏
    
    private UpgradeInfo upgradeInfo;
    
    public void Setup(UpgradeInfo info)
    {
        upgradeInfo = info;
        
        if (nameText != null)
        {
            nameText.text = info.name;
        }
        
        if (descText != null)
        {
            descText.text = info.desc;
        }
        
        if (costText != null)
        {
            costText.text = $"BUY {info.cost.ToString()}";
        }
        
        UpdateBuyButton();
        
        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyClicked);
        }
    }
    
    public void UpdateBuyButton()
    {
        if (GameManager.Instance == null || upgradeInfo == null) return;
        
        bool canAfford = GameManager.Instance.gameData.coins >= upgradeInfo.cost;
        bool hasUpgrade = GameManager.Instance.gameData.ownedUpgrades.Contains(upgradeInfo.identifier);
        bool canBuy = canAfford && !hasUpgrade && GameManager.Instance.gameData.ownedUpgrades.Count < 5;
        
        if (buyButton != null)
        {
            buyButton.interactable = canBuy;
        }
        
        if (costText != null)
        {
            costText.color = canBuy ? Color.white : Color.red;
        }
    }
    
    public void OnBuyClicked()
    {
        if (GameManager.Instance == null || upgradeInfo == null) return;
        
        // 检查是否已拥有
        if (GameManager.Instance.gameData.ownedUpgrades.Contains(upgradeInfo.identifier))
        {
            return;
        }
        
        // 检查是否已满5个
        if (GameManager.Instance.gameData.ownedUpgrades.Count >= 5)
        {
            return;
        }
        
        // 检查是否有足够的金币
        if (GameManager.Instance.gameData.coins >= upgradeInfo.cost)
        {
            GameManager.Instance.gameData.coins -= upgradeInfo.cost;
            GameManager.Instance.ShowFloatingText("coin", -upgradeInfo.cost);
            GameManager.Instance.gameData.ownedUpgrades.Add(upgradeInfo.identifier);
            GameManager.Instance.uiManager?.UpdateUI();
            GameManager.Instance.uiManager?.UpdateUpgradeDisplay();
            
            // 隐藏content，不刷新整个商店
            if (content != null)
            {
                content.SetActive(false);
            }
            
            // 更新所有商店物品的按钮状态（不刷新商店）
            ShopManager.Instance?.UpdateAllBuyButtons();
        }
    }
}



