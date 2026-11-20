using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>对话预置体显示组件，支持底部对话框和兼容原有气泡。</summary>
[DisallowMultipleComponent]
public sealed class BubbleDialogView : MonoBehaviour
{
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private RectTransform bubbleRect;
    [SerializeField] private Text contentText;
    [SerializeField, InspectorName("固定在屏幕底部")] private bool screenBottom;
    [SerializeField, InspectorName("角色名文字")] private Text speakerText;
    [Header("文字自动换行与边距")]
    [SerializeField, Min(32f)] private float minWidth = 100f;
    [SerializeField, Min(32f)] private float maxWidth = 340f;
    [SerializeField, Min(16f)] private float minHeight = 64f;
    [SerializeField] private Vector2 padding = new Vector2(32f, 24f);

    public RectTransform Rect => bubbleRect;
    public bool IsConfigured => worldCanvas != null && bubbleRect != null && contentText != null;
    public int CharacterCount => characterStarts.Length;
    public bool IsScreenSpace => screenBottom;

    private string fullText = string.Empty;
    private int[] characterStarts = System.Array.Empty<int>();
    private int revealedCount = -1;

    /// <summary>先按完整文本预留空间，打字时气泡尺寸保持稳定。</summary>
    public void Initialize(Camera renderCamera, string text, string speaker = "")
    {
        worldCanvas.renderMode = screenBottom ? RenderMode.ScreenSpaceOverlay : RenderMode.WorldSpace;
        worldCanvas.worldCamera = renderCamera;
        worldCanvas.overrideSorting = true;
        DialogueFontUtility.ApplyFallback(contentText);
        if (speakerText != null)
        {
            DialogueFontUtility.ApplyFallback(speakerText);
            speakerText.text = speaker ?? string.Empty;
        }
        fullText = text;
        characterStarts = StringInfo.ParseCombiningCharacters(fullText);
        revealedCount = -1;
        contentText.supportRichText = false;
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow = VerticalWrapMode.Overflow;
        contentText.text = fullText;

        if (screenBottom)
        {
            // 宽度、锚点和边距由美术在预置体中编辑，长台词只增大对话框高度。
            Canvas.ForceUpdateCanvases();
            float panelHeight = Mathf.Max(minHeight, contentText.preferredHeight + Mathf.Max(0f, padding.y));
            bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
            return;
        }

        float horizontalPadding = Mathf.Max(0f, padding.x);
        float verticalPadding = Mathf.Max(0f, padding.y);
        float width = Mathf.Clamp(contentText.preferredWidth + horizontalPadding,
            Mathf.Max(32f, minWidth), Mathf.Max(minWidth, maxWidth));
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        var textRect = contentText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(horizontalPadding, verticalPadding) * 0.5f;
        textRect.offsetMax = -textRect.offsetMin;
        float height = Mathf.Max(minHeight, contentText.preferredHeight + verticalPadding);
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    public void Reveal(int count)
    {
        count = Mathf.Clamp(count, 0, CharacterCount);
        if (count == revealedCount) return;
        revealedCount = count;
        int end = count == CharacterCount ? fullText.Length : characterStarts[count];
        contentText.text = fullText.Substring(0, end);
    }

    public void SetVisible(bool visible) => worldCanvas.enabled = visible;
}

/// <summary>没有字体资产时优先使用系统中文字体；已指定的美术字体保持原样。</summary>
public static class DialogueFontUtility
{
    private static Font cachedFont;

    public static Font GetFallbackFont()
    {
        if (cachedFont != null) return cachedFont;
        string[] installed = Font.GetOSInstalledFontNames();
        string[] preferred = { "Microsoft YaHei", "Microsoft YaHei UI", "PingFang SC", "Noto Sans CJK SC", "SimHei" };
        foreach (string name in preferred)
        {
            if (System.Array.IndexOf(installed, name) < 0) continue;
            cachedFont = Font.CreateDynamicFontFromOSFont(name, 24);
            if (cachedFont != null) return cachedFont;
        }
        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }

    public static void ApplyFallback(Text text)
    {
        if (text == null) return;
        if (text.font == null || text.font.name == "Arial" || text.font.name == "LegacyRuntime")
            text.font = GetFallbackFont();
    }
}
