using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class UpgradeDisplaySlot : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;
    public Button sellButton;
    public GameObject emptySlotIndicator;
    
    private string upgradeIdentifier;
    private bool isSelected = false;
    private RectTransform rectTransform;
    
    [SerializeField]
    private float pulseScale = 1.2f; // 放大倍数
    [SerializeField]
    private float pulseDuration = 0.3f; // 动画持续时间
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }
    
    public void Setup(string identifier)
    {
        upgradeIdentifier = identifier;
        isSelected = false;
        
        if (CSVLoader.Instance == null || !CSVLoader.Instance.upgradeDict.ContainsKey(identifier))
        {
            ClearSlot();
            return;
        }
        
        UpgradeInfo upgradeInfo = CSVLoader.Instance.upgradeDict[identifier];
        
        if (nameText != null)
        {
            nameText.text = upgradeInfo.name;
        }
        
        if (descText != null)
        {
            descText.text = upgradeInfo.desc;
        }
        
        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(false);
            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(OnSellClicked);
        }
        
        if (emptySlotIndicator != null)
        {
            emptySlotIndicator.SetActive(false);
        }
        
        // 添加点击事件来显示/隐藏出售按钮
        Button slotButton = GetComponent<Button>();
        if (slotButton == null)
        {
            slotButton = gameObject.AddComponent<Button>();
        }
        slotButton.onClick.RemoveAllListeners();
        slotButton.onClick.AddListener(OnSlotClicked);
    }
    
    public void ClearSlot()
    {
        upgradeIdentifier = null;
        isSelected = false;
        
        if (nameText != null)
        {
            nameText.text = "";
        }
        
        if (descText != null)
        {
            descText.text = "";
        }
        
        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(false);
        }
        
        if (emptySlotIndicator != null)
        {
            emptySlotIndicator.SetActive(true);
        }
    }
    
    private void OnSlotClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        if (string.IsNullOrEmpty(upgradeIdentifier)) return;
        
        isSelected = !isSelected;
        
        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(isSelected);
        }
    }
    
    private void OnSellClicked()
    {
        // 播放点击音效
        SFXManager.Instance?.PlayClickSound();
        
        if (string.IsNullOrEmpty(upgradeIdentifier) || GameManager.Instance == null) return;
        
        if (!CSVLoader.Instance.upgradeDict.ContainsKey(upgradeIdentifier)) return;
        
        UpgradeInfo upgradeInfo = CSVLoader.Instance.upgradeDict[upgradeIdentifier];
        
        // 出售价格为cost的一半
        int sellPrice = upgradeInfo.cost / 2;
        GameManager.Instance.gameData.coins += sellPrice;
        GameManager.Instance.gameData.ownedUpgrades.Remove(upgradeIdentifier);
        
        GameManager.Instance.uiManager?.UpdateUI();
        GameManager.Instance.uiManager?.UpdateUpgradeDisplay();
        ShopManager.Instance?.UpdateShopItems();
    }
    
    // 检查这个slot是否显示指定的upgrade
    public bool IsDisplayingUpgrade(string identifier)
    {
        return upgradeIdentifier == identifier;
    }
    
    // 播放放大缩小动画
    public void PlayPulseAnimation()
    {
        if (rectTransform == null) return;
        
        // 停止之前的动画
        rectTransform.DOKill();
        
        // 保存原始缩放
        Vector3 originalScale = Vector3.one;
        
        // 创建动画序列：放大 -> 缩小回原尺寸
        Sequence sequence = DOTween.Sequence();
        sequence.Append(rectTransform.DOScale(originalScale * pulseScale, pulseDuration * 0.5f).SetEase(Ease.OutQuad));
        sequence.Append(rectTransform.DOScale(originalScale, pulseDuration * 0.5f).SetEase(Ease.InQuad));
    }
}





