using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;
using TMPro;

namespace UnityEngine.Localization.Events
{
    [System.Serializable]
    public class UnityEventTmpFont : UnityEvent<TMP_FontAsset> { }

    [AddComponentMenu("Localization/Asset/Localize TMP Font Event")]
    public class LocalizeTmpFontEvent : LocalizedAssetEvent<TMP_FontAsset, LocalizedTmpFont, UnityEventTmpFont>
    {
        private TMP_Text _text;

        // 移除 override，直接使用 Awake
        private void Awake()
        {
            // 如果基类没有声明 virtual Awake，这里就不需要 base.Awake()
            _text = GetComponent<TMP_Text>();
        }

        protected override void UpdateAsset(TMP_FontAsset localizedAsset)
        {
            base.UpdateAsset(localizedAsset);

            if (_text != null && localizedAsset != null)
            {
                _text.font = localizedAsset;
                _text.SetAllDirty(); // 强制 TMP 刷新渲染
            }
        }
    }

    [System.Serializable]
    public class LocalizedTmpFont : UnityEngine.Localization.LocalizedAsset<TMP_FontAsset> { }
}