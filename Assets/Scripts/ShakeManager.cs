using System;
using UnityEngine;
using DG.Tweening;

public class ShakeManager : MonoBehaviour
{
    public static ShakeManager Instance;
    
    private Canvas canvas;
    private RectTransform canvasRect;
    private Tween shakeTween;
    private bool isShaking = false;
    
    [SerializeField]
    private float minShakeStrength = 5f; // 血量3时的最小抖动幅度
    [SerializeField]
    private float maxShakeStrength = 20f; // 血量1时的最大抖动幅度
    [SerializeField]
    private float shakeDuration = 0.1f; // 每次抖动的持续时间
    [SerializeField]
    private int shakeVibrato = 10; // 抖动次数
    [SerializeField]
    private float shakeRandomness = 90f; // 随机性
    
    private Vector3 originalPosition;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // 查找Canvas
        canvas = GameManager.Instance.canvas;
        if (canvas != null)
        {
            canvasRect = GameManager.Instance.boardManager.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                originalPosition = canvasRect.localPosition;
            }
        }
    }
    
    // 开始抖动，根据血量计算抖动强度
    public void StartShake(int currentHealth)
    {
        if (canvasRect == null || currentHealth > 3) return;
        
        // 如果已经在抖动，先停止
        StopShake();
        
        isShaking = true;
        
        // 根据血量计算抖动强度（血量越少抖动越厉害）
        // 血量3 -> minShakeStrength, 血量1 -> maxShakeStrength
        float healthRatio = (3f - currentHealth) / 2f; // 血量3时为0，血量1时为1
        float shakeStrength = Mathf.Lerp(minShakeStrength, maxShakeStrength, healthRatio);
        
        // 持续抖动
        ShakeLoop(shakeStrength);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            StartShake(1);
        }
    }

    // 循环抖动
    private void ShakeLoop(float strength)
    {
        if (!isShaking || canvasRect == null) return;
        
        // 重置位置
        canvasRect.localPosition = originalPosition;
        
        // 创建抖动动画
        shakeTween = canvasRect.DOShakePosition(shakeDuration, strength, shakeVibrato, shakeRandomness, false, true)
            .SetEase(Ease.Linear)
            .OnComplete(() => {
                if (isShaking)
                {
                    // 继续抖动
                    ShakeLoop(strength);
                }
                else
                {
                    // 停止抖动，恢复位置
                    canvasRect.localPosition = originalPosition;
                }
            });
    }
    
    // 停止抖动
    public void StopShake()
    {
        isShaking = false;
        
        if (shakeTween != null)
        {
            shakeTween.Kill();
            shakeTween = null;
        }
        
        if (canvasRect != null)
        {
            canvasRect.localPosition = originalPosition;
        }
    }
    
    // 更新抖动强度（当血量变化时）
    public void UpdateShakeStrength(int currentHealth)
    {
        if (currentHealth > 3)
        {
            StopShake();
            return;
        }
        
        if (isShaking)
        {
            // 重新开始抖动以应用新的强度
            StartShake(currentHealth);
        }
    }
    
    private void OnDestroy()
    {
        StopShake();
    }
}














