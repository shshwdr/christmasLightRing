using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class FadeInAndOut : MonoBehaviour
{
    [Header("Alpha Range")]
    [Range(0f, 1f)]
    public float startA = 0f;
    [Range(0f, 1f)]
    public float endA = 1f;

    [Header("Timing")]
    [Min(0.01f)]
    public float duration = 0.6f;

    [Header("Behavior")]
    public bool playOnEnable = true;

    private Image targetImage;
    private float timer;
    private bool forward = true;

    private void Awake()
    {
        targetImage = GetComponent<Image>();
        ApplyAlpha(startA);
    }

    private void OnEnable()
    {
        if (!playOnEnable)
            return;

        timer = 0f;
        forward = true;
        ApplyAlpha(startA);
    }

    private void Update()
    {
        if (!playOnEnable || targetImage == null)
            return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);
        float alpha = forward
            ? Mathf.Lerp(startA, endA, t)
            : Mathf.Lerp(endA, startA, t);

        ApplyAlpha(alpha);

        if (timer >= duration)
        {
            timer = 0f;
            forward = !forward;
        }
    }

    public void RestartLoop()
    {
        timer = 0f;
        forward = true;
        ApplyAlpha(startA);
    }

    private void ApplyAlpha(float alpha)
    {
        if (targetImage == null)
            return;

        Color c = targetImage.color;
        c.a = alpha;
        targetImage.color = c;
    }
}
