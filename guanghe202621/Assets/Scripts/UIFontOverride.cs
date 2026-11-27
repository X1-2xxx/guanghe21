using UnityEngine;
using UnityEngine.UI;

/// <summary>为指定 UGUI 标签保留独立字体，避免全局中文字体同步覆盖。</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Text))]
public sealed class UIFontOverride : MonoBehaviour
{
    [InspectorName("指定字体")] public Font font;
}
