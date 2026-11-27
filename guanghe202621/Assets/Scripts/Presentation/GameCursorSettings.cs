using UnityEngine;

namespace GamePresentation
{
    [CreateAssetMenu(menuName = "堂吉诃德/鼠标指针设置")]
    public sealed class GameCursorSettings : ScriptableObject
    {
        [InspectorName("鼠标美术")] public Texture2D artwork;
        [Range(16, 128), InspectorName("显示尺寸（最长边像素）")] public int pixelSize = 64;
        [InspectorName("点击热点（从左上角起，像素）")] public Vector2 hotspot = new Vector2(4f, 4f);
        [InspectorName("自动去除透明边距")] public bool trimTransparentPadding = true;
    }
}
