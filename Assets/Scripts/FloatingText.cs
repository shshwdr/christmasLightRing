using UnityEngine;
using TMPro;
using DG.Tweening;

public class FloatingText : MonoBehaviour
{
    private TextMeshProUGUI textMesh;
    private RectTransform rectTransform;
    
    [SerializeField]
    private float moveDistance = 100f; // 向上移动的距离
    [SerializeField]
    private float duration = 1f; // 动画持续时间
    
    private void Awake()
    {
        textMesh = GetComponentInChildren<TextMeshProUGUI>();
        rectTransform = GetComponent<RectTransform>();
    }
    
    public void Show(string text, Color color, Vector2 startPosition)
    {
        if (textMesh == null || rectTransform == null) return;
        
        // 设置文本和颜色
        textMesh.text = text;
        textMesh.color = color;
        
        // 设置初始位置
        rectTransform.position = startPosition;
        
        // 设置初始透明度
        Color startColor = color;
        startColor.a = 1f;
        textMesh.color = startColor;
        
        // 创建动画序列
        Sequence sequence = DOTween.Sequence();
        
        // 向上移动
        sequence.Append(rectTransform.DOMoveY(startPosition.y + moveDistance, duration));
        
        // 同时淡出
        sequence.Join(textMesh.DOFade(0f, duration));
        
        // 动画完成后销毁对象
        sequence.OnComplete(() => {
            Destroy(gameObject);
        });
    }
}
















