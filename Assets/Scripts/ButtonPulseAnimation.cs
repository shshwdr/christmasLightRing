using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public class ButtonPulseAnimation : MonoBehaviour
{
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Sequence animationSequence;
    
    [SerializeField]
    private float scaleTarget = 1.2f; // 放大目标倍数
    [SerializeField]
    private float fadeTarget = 0.3f; // fade目标透明度
    [SerializeField]
    private float duration = 1f; // 动画持续时间
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        
        // 如果没有CanvasGroup，添加一个
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
    
    private void OnEnable()
    {
        StartAnimation();
    }
    
    private void OnDisable()
    {
        StopAnimation();
    }
    
    public void StartAnimation()
    {
        StopAnimation();
        
        if (rectTransform == null || canvasGroup == null) return;
        
        // 重置初始状态
        rectTransform.localScale = Vector3.one;
        canvasGroup.alpha = 1f;
        
        // 创建动画序列
        animationSequence = DOTween.Sequence();
        
        // 放大到1.2倍，同时fade到0.3
        animationSequence.Append(rectTransform.DOScale(Vector3.one * scaleTarget, duration));
        animationSequence.Join(canvasGroup.DOFade(fadeTarget, duration));
        
        // 设置loop方式为restart
        animationSequence.SetLoops(-1, LoopType.Restart);
    }
    
    public void StopAnimation()
    {
        if (animationSequence != null)
        {
            animationSequence.Kill();
            animationSequence = null;
        }
        
        // 重置状态
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }
    
    private void OnDestroy()
    {
        StopAnimation();
    }
}



