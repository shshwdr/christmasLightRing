using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制一个 Fill 类型 Image 的 fillAmount，用于竞速模式倒计时等。
/// 挂在 Tile 上时，仅在 player 格且竞速模式下显示。
/// </summary>
public class ProgressBar : MonoBehaviour
{
    public Image fillImage;
    
    /// <summary> 设置进度 0~1 </summary>
    public void SetProgress(float normalized)
    {
        if (fillImage == null) return;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = Mathf.Clamp01(normalized);
    }
}
