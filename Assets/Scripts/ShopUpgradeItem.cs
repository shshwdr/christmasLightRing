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
    private bool isFreeMode = false;
    
    public void Setup(UpgradeInfo info, bool freeMode = false)
    {
        upgradeInfo = info;
        isFreeMode = freeMode;
        
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
            if (isFreeMode)
            {
                costText.text = "PICK";
            }
            else
            {
                costText.text = $"BUY {info.cost.ToString()}";
            }
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
        
        if (isFreeMode)
        {
            // 免费模式：所有按钮都可以点击
            if (buyButton != null)
            {
                buyButton.interactable = true;
            }
            if (costText != null)
            {
                costText.color = Color.white;
            }
        }
        else
        {
            bool canAfford = GameManager.Instance.mainGameData.coins >= upgradeInfo.cost;
            bool hasUpgrade = GameManager.Instance.mainGameData.ownedUpgrades.Contains(upgradeInfo.identifier);
            bool canBuy = canAfford && !hasUpgrade/* && GameManager.Instance.mainGameData.ownedUpgrades.Count < 5*/;
            
            if (buyButton != null)
            {
                buyButton.interactable = canBuy;
            }
            
            if (costText != null)
            {
                costText.color = canBuy ? Color.white : Color.red;
            }
        }
    }
    
    public void OnBuyClicked()
    {
        if (GameManager.Instance == null || upgradeInfo == null) return;
        
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        if (isFreeMode)
        {
            // 免费模式：免费获得
            // 检查是否已拥有
            if (GameManager.Instance.mainGameData.ownedUpgrades.Contains(upgradeInfo.identifier))
            {
                return;
            }
            
            // 播放购买音效
            SFXManager.Instance?.PlaySFX("buyItem");
            
            GameManager.Instance.mainGameData.ownedUpgrades.Add(upgradeInfo.identifier);
            GameManager.Instance.uiManager?.UpdateUI();
            GameManager.Instance.uiManager?.UpdateUpgradeDisplay();
            
            // 隐藏content
            if (content != null)
            {
                content.SetActive(false);
            }
            
            // 通知 ShopManager 免费升级已选择
            ShopManager.Instance?.OnFreeUpgradePicked();
        }
        else
        {
            // 检查是否已拥有
            if (GameManager.Instance.mainGameData.ownedUpgrades.Contains(upgradeInfo.identifier))
            {
                return;
            }
            
            // 检查是否已满5个
            if (GameManager.Instance.mainGameData.ownedUpgrades.Count >= 5)
            {
                // 显示对话框提示
                if (DialogPanel.Instance != null)
                {
                    DialogPanel.Instance.ShowDialog("Reached limit, sell upgrades to purchase new ones", null);
                }
                return;
            }
            
            // 检查是否有足够的金币
            if (GameManager.Instance.mainGameData.coins >= upgradeInfo.cost)
            {
                // 播放购买音效
                SFXManager.Instance?.PlaySFX("buyItem");
                
                GameManager.Instance.mainGameData.coins -= upgradeInfo.cost;
                GameManager.Instance.ShowFloatingText("coin", -upgradeInfo.cost);
                GameManager.Instance.mainGameData.ownedUpgrades.Add(upgradeInfo.identifier);
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
}



