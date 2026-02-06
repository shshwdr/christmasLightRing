using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CardFlyEffect : MonoBehaviour
{
    private Image cardImage;
    private RectTransform rectTransform;
    
    [SerializeField]
    private float flyDuration = 0.5f; // 飞行持续时间
    [SerializeField]
    private float scaleDuringFly = 0.8f; // 飞行过程中的缩放
    
    private void Awake()
    {
        cardImage = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
    }
    
    public void FlyToTarget(Vector3 startPosition, Vector3 targetPosition, System.Action onComplete = null)
    {
        if (rectTransform == null || cardImage == null) return;
        
        // 设置初始位置
        rectTransform.position = startPosition;
        
        // 设置初始缩放
        rectTransform.localScale = Vector3.one;
        
        // 创建动画序列
        Sequence sequence = DOTween.Sequence();
        
        // 飞行到目标位置
        sequence.Append(rectTransform.DOMove(targetPosition, flyDuration).SetEase(Ease.InQuad));
        
        // 同时缩小
        sequence.Join(rectTransform.DOScale(Vector3.one * scaleDuringFly, flyDuration));
        
        // 动画完成后销毁对象
        sequence.OnComplete(() => {
            onComplete?.Invoke();
            Destroy(gameObject);
        });
    }
}

















