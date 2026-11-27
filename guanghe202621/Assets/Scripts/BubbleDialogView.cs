using System.Globalization;
using GamePresentation;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>对话预置体显示组件，支持底部对话框和兼容原有气泡。</summary>
[DisallowMultipleComponent]
public sealed class BubbleDialogView : MonoBehaviour, IPointerClickHandler
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
    [SerializeField, Min(0f), InspectorName("正文上移（为气泡尾巴留空）")] private float contentLift;

    [Header("头顶气泡美术（默认上，屏幕边缘时改用左/右）")]
    [SerializeField] private Image headBackground;
    [SerializeField] private Sprite headAboveSprite, headLeftSprite, headRightSprite;
    private Vector3 headRestScale;
    private bool headScaleCached;

    public void FollowHead(Collider2D speaker, Transform fallback, Camera camera)
    {
        if (!headScaleCached) { headRestScale = transform.localScale; headScaleCached = true; }
        transform.localScale = headRestScale;
        transform.rotation = Quaternion.identity;
        Vector2 head = speaker ? new Vector2(speaker.bounds.center.x, speaker.bounds.max.y) : (Vector2)fallback.position + Vector2.up * 2f;
        var size = Vector2.Scale(bubbleRect.rect.size, new Vector2(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)));
        var result = new HeadBubbleScreenLayout.Result(head + Vector2.up * (.2f + size.y * .5f), HeadBubbleScreenLayout.Artwork.Above);
        if (camera && camera.orthographic)
        {
            var visible = HeadBubbleScreenLayout.VisibleArea(camera, .08f);
            float scale = Mathf.Min(1f, visible.width / Mathf.Max(.001f, size.x), visible.height / Mathf.Max(.001f, size.y));
            transform.localScale = headRestScale * scale; size *= scale;
            result = HeadBubbleScreenLayout.Solve(head, size, visible, .2f);
        }
        var sprite = result.artwork == HeadBubbleScreenLayout.Artwork.Left ? headLeftSprite : result.artwork == HeadBubbleScreenLayout.Artwork.Right ? headRightSprite : headAboveSprite;
        if (headBackground && sprite) { headBackground.sprite = sprite; headBackground.overrideSprite = null; }
        var pivot = bubbleRect.pivot;
        Vector2 origin = result.center + Vector2.Scale(pivot - Vector2.one * .5f, size);
        transform.position = new Vector3(origin.x, origin.y, 0f);
    }

    public RectTransform Rect => bubbleRect;
    public bool IsConfigured => worldCanvas != null && bubbleRect != null && contentText != null;
    public int CharacterCount => characterStarts.Length;
    public bool IsScreenSpace => screenBottom;
    public bool IsFullyRevealed => revealedCount >= CharacterCount;
    public event System.Action AdvanceRequested;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) AdvanceRequested?.Invoke();
    }

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
        contentText.supportRichText = false;
        contentText.horizontalOverflow = HorizontalWrapMode.Overflow;
        contentText.verticalOverflow = VerticalWrapMode.Overflow;
        contentText.text = text ?? string.Empty;

        if (screenBottom)
        {
            // 宽度、锚点和边距由美术在预置体中编辑，长台词只增大对话框高度。
            Canvas.ForceUpdateCanvases();
            WrapText(text);
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
        var halfPadding = new Vector2(horizontalPadding, verticalPadding) * 0.5f;
        var lift = Vector2.up * Mathf.Min(contentLift, halfPadding.y);
        textRect.offsetMin = halfPadding + lift;
        textRect.offsetMax = -halfPadding + lift;
        WrapText(text);
        float height = Mathf.Max(minHeight, contentText.preferredHeight + verticalPadding);
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private void WrapText(string text)
    {
        fullText = new StoryTextLayout(contentText, contentText.fontSize).Wrap(text);
        characterStarts = StringInfo.ParseCombiningCharacters(fullText);
        revealedCount = -1;
        contentText.text = fullText;
    }

    public void Reveal(int count, bool audible = true)
    {
        count = Mathf.Clamp(count, 0, CharacterCount);
        if (count == revealedCount) return;
        if (audible && revealedCount >= 0 && count > revealedCount && count - revealedCount <= 3)
        {
            int start = revealedCount < CharacterCount ? characterStarts[revealedCount] : fullText.Length;
            int finish = count == CharacterCount ? fullText.Length : characterStarts[count];
            AudioManager.TypeCharacters(fullText, start, finish, !screenBottom);
        }
        revealedCount = count;
        int end = count == CharacterCount ? fullText.Length : characterStarts[count];
        contentText.text = fullText.Substring(0, end);
    }

    public void SetVisible(bool visible) => worldCanvas.enabled = visible;
}

/// <summary>保留原有调用接口，对话及测试面板统一使用项目 UI 字体。</summary>
public static class DialogueFontUtility
{
    public static Font GetFallbackFont() => UIFontUtility.GetFont();
    public static void ApplyFallback(Text text) => UIFontUtility.Apply(text);
}
