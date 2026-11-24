using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>管理同一个屏幕 Canvas 中的悬浮提示，不依赖菜单、暂停或物品业务脚本。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas))]
public sealed class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [Header("预制体中的显示组件")]
    [SerializeField, InspectorName("提示文字")] private Text contentText;
    [Header("尺寸与位置，单位为 Canvas UI 单位")]
    [SerializeField, InspectorName("鼠标偏移（向右、向下）")] private Vector2 pointerOffset = new Vector2(20f, 20f);
    [SerializeField, Min(1f), InspectorName("最小宽度")] private float minWidth = 120f;
    [SerializeField, Min(1f), InspectorName("最大宽度")] private float maxWidth = 360f;
    [SerializeField, InspectorName("文字左右、上下总边距")] private Vector2 padding = new Vector2(32f, 24f);
    [SerializeField, Min(0f), InspectorName("屏幕边距")] private float screenMargin = 8f;
    [SerializeField, InspectorName("显示层级")] private int sortingOrder = 1000;

    private RectTransform panelRect;
    private RectTransform canvasRect;
    private Canvas rootCanvas;
    private Canvas tooltipCanvas;
    private CanvasGroup visibility;
    private TooltipTrigger currentTrigger;
    private Vector2 pointerPosition;
    private Camera eventCamera;
    private string displayedText;
    private Vector2 previousCanvasSize;
    private static readonly HashSet<Object> suppressionOwners = new HashSet<Object>();

    public static bool IsSuppressed
    {
        get
        {
            suppressionOwners.RemoveWhere(owner => owner == null);
            return suppressionOwners.Count > 0;
        }
    }

    /// <summary>弹窗以自身作为屏蔽来源。多个弹窗关闭顺序不同，也不会提前恢复提示。</summary>
    public static void SetSuppressed(Object owner, bool suppressed)
    {
        if (suppressed)
        {
            if (owner == null) return;
            suppressionOwners.Add(owner);
            if (Instance != null) Instance.Hide();
        }
        else suppressionOwners.Remove(owner);
    }

    public bool IsVisible => currentTrigger != null && visibility != null && visibility.alpha > 0f;

    private void Awake() => InitializeView();
    private void OnEnable() => InitializeView();

    private void InitializeView()
    {
        panelRect = (RectTransform)transform;
        // 尺寸由文字和 Canvas 单位计算，避免编辑器拖放 Canvas 时留下零缩放。
        panelRect.localScale = Vector3.one;
        visibility = GetComponent<CanvasGroup>();
        // 提示不参与鼠标射线，避免遮住物品后反复触发移入、移出。
        visibility.interactable = false;
        visibility.blocksRaycasts = false;
        visibility.alpha = 0f;
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

        Canvas parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
        if (parentCanvas == null || parentCanvas.renderMode == RenderMode.WorldSpace || contentText == null)
        {
            Debug.LogError("TooltipPanel 需要放在屏幕 Canvas 下，并关联提示文字 Text。", this);
            enabled = false;
            return;
        }
        if (Instance != null && Instance != this)
        {
            Debug.LogError("场景中只需一份 TooltipPanel，请保留 Canvas 下的一份实例。", this);
            enabled = false;
            return;
        }
        Instance = this;
        rootCanvas = parentCanvas.rootCanvas;
        canvasRect = transform.parent as RectTransform;
        tooltipCanvas = GetComponent<Canvas>();
        tooltipCanvas.overrideSorting = true;
        tooltipCanvas.sortingLayerID = rootCanvas.sortingLayerID;
        UpdateDrawOrder(null);
        panelRect.pivot = new Vector2(0f, 1f);
        ApplyFallbackFont(contentText);
        contentText.supportRichText = false;
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow = VerticalWrapMode.Truncate;
    }

    /// <summary>由 Trigger 传入内容及鼠标位置；锁定状态只影响文字，不改变物品业务状态。</summary>
    public void Show(TooltipTrigger source, PointerEventData pointer)
    {
        if (IsSuppressed || !isActiveAndEnabled || source == null || pointer == null || canvasRect == null) return;
        Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
        if (sourceCanvas == null || sourceCanvas.rootCanvas != rootCanvas) return;
        currentTrigger = source;
        pointerPosition = pointer.position;
        eventCamera = pointer.enterEventCamera;
        transform.SetAsLastSibling();
        UpdateDrawOrder(sourceCanvas);
        RefreshContent();
        if (currentTrigger == null) return;
        UpdatePosition();
        visibility.alpha = 1f;
    }

    /// <summary>1000 是最低显示层级；正式关卡的父 Canvas 也可能是 1000，必须再高一层。</summary>
    private void UpdateDrawOrder(Canvas sourceCanvas)
    {
        if (tooltipCanvas == null || rootCanvas == null) return;
        int menuOrder = Mathf.Max(rootCanvas.sortingOrder, sourceCanvas != null ? sourceCanvas.sortingOrder : rootCanvas.sortingOrder);
        // 不能依靠同层的兄弟顺序：嵌套 Canvas 的实际绘制顺序可能把提示压在书本下。
        tooltipCanvas.sortingOrder = Mathf.Clamp(Mathf.Max(sortingOrder, menuOrder + 1), short.MinValue, short.MaxValue);
    }

    /// <summary>只接受当前悬浮对象的位置更新，避免旧物品事件影响新提示。</summary>
    public void Move(TooltipTrigger source, Vector2 screenPosition)
    {
        if (source == currentTrigger) pointerPosition = screenPosition;
    }

    /// <summary>任意外部 UI 控制器都可主动清除提示。</summary>
    public void Hide()
    {
        currentTrigger = null;
        displayedText = null;
        if (visibility != null) visibility.alpha = 0f;
    }

    /// <summary>旧物品的移出或关闭事件不能误关新物品的提示。</summary>
    public void Hide(TooltipTrigger source)
    {
        if (currentTrigger == source) Hide();
    }

    private void LateUpdate()
    {
        if (IsSuppressed) { Hide(); return; }
        if (currentTrigger == null)
        {
            // 销毁物品时 Unity 对象会变成空引用，同样清除残留显示。
            if (visibility.alpha > 0f) Hide();
            return;
        }
        if (!currentTrigger.isActiveAndEnabled || !rootCanvas.isActiveAndEnabled || !IsSourceVisible())
        {
            Hide();
            return;
        }
        // 暂停时也跟随鼠标；完全不依赖 Time.timeScale 或协程。
        if (displayedText != currentTrigger.DisplayText || previousCanvasSize != canvasRect.rect.size)
            RefreshContent();
        if (currentTrigger != null) UpdatePosition();
    }

    private bool IsSourceVisible()
    {
        // SetActive 由 Trigger.OnDisable 立即处理；这里补充 Canvas/CanvasGroup 隐藏方式。
        for (Transform item = currentTrigger.transform; item != null; item = item.parent)
        {
            if (item.TryGetComponent<Canvas>(out var canvas) && !canvas.enabled) return false;
            foreach (var group in item.GetComponents<CanvasGroup>())
                if (group.alpha <= 0f) return false;
        }
        return true;
    }

    private void RefreshContent()
    {
        displayedText = currentTrigger.DisplayText;
        if (string.IsNullOrWhiteSpace(displayedText)) { Hide(); return; }
        contentText.text = displayedText;
        previousCanvasSize = canvasRect.rect.size;
        float availableWidth = Mathf.Max(1f, previousCanvasSize.x - screenMargin * 2f);
        float width = Mathf.Clamp(contentText.preferredWidth + padding.x,
            Mathf.Min(minWidth, availableWidth), Mathf.Min(Mathf.Max(minWidth, maxWidth), availableWidth));
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        var textRect = contentText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = padding * 0.5f;
        textRect.offsetMax = -padding * 0.5f;
        float availableHeight = Mathf.Max(1f, previousCanvasSize.y - screenMargin * 2f);
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Min(availableHeight, Mathf.Max(1f, contentText.preferredHeight + padding.y)));
    }

    private void UpdatePosition()
    {
        Camera camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null :
            (rootCanvas.worldCamera != null ? rootCanvas.worldCamera : eventCamera);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pointerPosition, camera, out var mouse)) return;
        Rect area = canvasRect.rect;
        Vector2 size = panelRect.rect.size;
        float marginX = Mathf.Min(screenMargin, area.width * 0.5f);
        float marginY = Mathf.Min(screenMargin, area.height * 0.5f);
        float x = mouse.x + pointerOffset.x;
        float y = mouse.y - pointerOffset.y;
        // 靠近右边或底边时翻转到鼠标另一侧，再限制在可见区域内。
        if (x + size.x > area.xMax - marginX) x = mouse.x - pointerOffset.x - size.x;
        if (y - size.y < area.yMin + marginY) y = mouse.y + pointerOffset.y + size.y;
        x = Mathf.Clamp(x, area.xMin + marginX, Mathf.Max(area.xMin + marginX, area.xMax - marginX - size.x));
        y = Mathf.Clamp(y, Mathf.Min(area.yMax - marginY, area.yMin + marginY + size.y), area.yMax - marginY);
        panelRect.localPosition = new Vector3(x, y, 0f);
    }

    private void OnDisable() { Hide(); if (Instance == this) Instance = null; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void OnApplicationFocus(bool focused) { if (!focused) Hide(); }

    /// <summary>保留原有接口，悬浮提示、名称和弹窗统一使用项目 UI 字体。</summary>
    public static void ApplyFallbackFont(Text target) => UIFontUtility.Apply(target);
}
