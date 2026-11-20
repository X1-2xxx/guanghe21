using System.Collections.Generic;
using UnityEngine;

/// <summary>对话显示与计时。默认在屏幕底部显示，不依赖角色控制器或相机演出。</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class BubbleDialogManager : MonoBehaviour
{
    public static BubbleDialogManager Instance { get; private set; }
    public bool UsesUnscaledTime => useUnscaledTime;

    [Header("对话显示方式")]
    [SerializeField, InspectorName("兼容的头顶气泡预置体")] private BubbleDialogView bubblePrefab;
    [SerializeField, InspectorName("使用底部对话框")] private bool useBottomDialogue = true;
    [SerializeField, InspectorName("底部对话框预置体")] private BubbleDialogView bottomDialoguePrefab;
    [SerializeField, InspectorName("显示相机")] private Camera renderCamera;
    [Header("计时")]
    [SerializeField, HideInInspector] private Vector3 defaultHeadOffset = new Vector3(0f, 1.8f, 0f);
    [SerializeField, InspectorName("忽略游戏暂停")] private bool useUnscaledTime;
    [Header("打字机效果")]
    [SerializeField, InspectorName("默认启用打字机")] private bool typewriterByDefault;
    [SerializeField, Min(1f), InspectorName("每秒显示字数")] private float charactersPerSecond = 25f;
    [Header("屏幕避让")]
    [SerializeField, Min(0f)] private float screenMargin = 12f;
    [SerializeField, Min(0f)] private float gapPixels = 8f;

    private sealed class Bubble
    {
        public int id;
        public Transform target;
        public Vector3 offset;
        public BubbleDialogView view;
        public bool typewriter;
        public float age;
        public float duration;
        public Vector3 baseScale;
    }

    private readonly List<Bubble> bubbles = new List<Bubble>();
    private readonly List<Rect> occupiedRects = new List<Rect>();
    private readonly Vector3[] corners = new Vector3[4];
    private int nextId;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(this);
            return;
        }
        Instance = this;
        if (renderCamera == null) renderCamera = Camera.main;
    }

    private void OnDisable() => ClearAll();

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>兼容旧调用。默认将角色台词显示在底部；计时从文字全部出现后开始。</summary>
    public int ShowBubble(Transform target, string text, float duration = 3f,
        Vector3? headOffset = null, bool? typewriter = null, string speakerName = null)
    {
        if (!isActiveAndEnabled || target == null || string.IsNullOrWhiteSpace(text)) return 0;
        HideBubbles(target);
        string name = string.IsNullOrWhiteSpace(speakerName) ? target.name : speakerName;
        return CreateDialogue(target, name, text, duration, headOffset, typewriter);
    }

    /// <summary>旁白或无角色台词也可以直接显示在底部，多次调用按顺序播放。</summary>
    public int ShowDialogue(string speaker, string text, float duration = 3f, bool? typewriter = null)
    {
        if (!useBottomDialogue || !isActiveAndEnabled || string.IsNullOrWhiteSpace(text)) return 0;
        return CreateDialogue(null, speaker, text, duration, null, typewriter);
    }

    private int CreateDialogue(Transform target, string speaker, string text, float duration,
        Vector3? headOffset, bool? typewriter)
    {
        BubbleDialogView prefab = useBottomDialogue ? bottomDialoguePrefab : bubblePrefab;
        if (prefab == null || !prefab.IsConfigured)
        {
            Debug.LogError("请给对话管理器指定对应预置体，并绑定显示组件的 Canvas、Rect 和 Text。", this);
            return 0;
        }
        var view = Instantiate(prefab);
        int id = ++nextId;
        view.name = $"对话_{speaker}_{id}";
        view.Initialize(CurrentCamera(), text, speaker);
        view.SetVisible(false);
        bool useTypewriter = typewriter ?? typewriterByDefault;
        view.Reveal(useTypewriter ? 0 : view.CharacterCount);
        bubbles.Add(new Bubble
        {
            id = id, target = target, view = view,
            duration = Mathf.Max(0.1f, duration),
            offset = headOffset ?? defaultHeadOffset,
            typewriter = useTypewriter,
            baseScale = view.transform.localScale
        });
        return id;
    }

    public bool IsBubbleActive(int id) => bubbles.Exists(item => item.id == id && item.view != null);

    public void HideBubble(int id)
    {
        for (int i = bubbles.Count - 1; i >= 0; i--)
            if (bubbles[i].id == id) RemoveAt(i);
    }

    public void HideBubbles(Transform target)
    {
        for (int i = bubbles.Count - 1; i >= 0; i--)
            if (bubbles[i].target == target) RemoveAt(i);
    }

    public void ClearAll()
    {
        for (int i = bubbles.Count - 1; i >= 0; i--) RemoveAt(i);
    }

    private Camera CurrentCamera()
    {
        if (renderCamera != null && renderCamera.isActiveAndEnabled) return renderCamera;
        return Camera.main;
    }

    private void LateUpdate()
    {
        Camera camera = CurrentCamera();
        occupiedRects.Clear();
        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        bool bottomDialogueShown = false;
        for (int i = 0; i < bubbles.Count; i++)
        {
            Bubble bubble = bubbles[i];
            if (bubble.view == null || (!bubble.view.IsScreenSpace && bubble.target == null))
            {
                RemoveAt(i--);
                continue;
            }
            bool visible;
            if (bubble.view.IsScreenSpace)
            {
                visible = !bottomDialogueShown;
                if (visible) bottomDialogueShown = true;
            }
            else
            {
                visible = bubble.target.gameObject.activeInHierarchy && camera != null && PlaceBubble(bubble, camera);
            }
            if (!visible)
            {
                // 屏外或屏幕已放不下的气泡等待位置，等待期间不消耗阅读时间。
                bubble.view.SetVisible(false);
                continue;
            }
            bubble.view.SetVisible(true);
            bubble.age += delta;
            float typingSeconds = bubble.typewriter
                ? bubble.view.CharacterCount / Mathf.Max(1f, charactersPerSecond) : 0f;
            int count = bubble.typewriter
                ? Mathf.FloorToInt(bubble.age * Mathf.Max(1f, charactersPerSecond))
                : bubble.view.CharacterCount;
            bubble.view.Reveal(count);
            if (bubble.age >= typingSeconds + bubble.duration) RemoveAt(i--);
        }
    }

    private bool PlaceBubble(Bubble bubble, Camera camera)
    {
        Transform viewTransform = bubble.view.transform;
        viewTransform.localScale = bubble.baseScale;
        viewTransform.SetPositionAndRotation(bubble.target.position + bubble.offset, camera.transform.rotation);
        Vector3 anchor = camera.WorldToScreenPoint(viewTransform.position);
        Rect screen = camera.pixelRect;
        if (anchor.z <= camera.nearClipPlane || anchor.z >= camera.farClipPlane) return false;
        if (!screen.Contains(new Vector2(anchor.x, anchor.y))) return false;

        bubble.view.Rect.GetWorldCorners(corners);
        Vector3 bottomLeft = camera.WorldToScreenPoint(corners[0]);
        Vector3 topRight = camera.WorldToScreenPoint(corners[2]);
        float width = Mathf.Abs(topRight.x - bottomLeft.x);
        float height = Mathf.Abs(topRight.y - bottomLeft.y);
        var available = new Rect(screen.x + screenMargin, screen.y + screenMargin,
            Mathf.Max(0f, screen.width - screenMargin * 2f),
            Mathf.Max(0f, screen.height - screenMargin * 2f));
        if (width <= 0f || height <= 0f || available.width <= 0f || available.height <= 0f) return false;
        // 极长文本或相机大幅拉近时缩小整体，避免单个气泡永远无法放进屏幕。
        float fitScale = Mathf.Min(1f, available.width / width, available.height / height);
        viewTransform.localScale = bubble.baseScale * fitScale;
        width *= fitScale;
        height *= fitScale;

        float x = Mathf.Clamp(anchor.x - width * 0.5f, available.xMin, available.xMax - width);
        float y = Mathf.Clamp(anchor.y, available.yMin, available.yMax - height);
        var candidate = new Rect(x, y, width, height);

        // 每次冲突都抬到已有气泡上方。若上方放不下，则从屏幕底部寻找空位。
        if (!TryStack(ref candidate, available))
        {
            candidate.y = available.yMin;
            if (!TryStack(ref candidate, available)) return false;
        }
        occupiedRects.Add(candidate);
        var placedAnchor = new Vector3(candidate.center.x, candidate.yMin, anchor.z);
        viewTransform.position = camera.ScreenToWorldPoint(placedAnchor);
        return true;
    }

    private bool TryStack(ref Rect candidate, Rect available)
    {
        for (int pass = 0; pass <= occupiedRects.Count; pass++)
        {
            bool moved = false;
            foreach (Rect other in occupiedRects)
            {
                var padded = new Rect(other.x - gapPixels, other.y - gapPixels,
                    other.width + gapPixels * 2f, other.height + gapPixels * 2f);
                if (!candidate.Overlaps(padded)) continue;
                candidate.y = other.yMax + gapPixels;
                moved = true;
            }
            if (candidate.yMax > available.yMax) return false;
            if (!moved) return true;
        }
        return false;
    }

    private void RemoveAt(int index)
    {
        BubbleDialogView view = bubbles[index].view;
        bubbles.RemoveAt(index);
        if (view != null)
        {
            view.SetVisible(false);
            Destroy(view.gameObject);
        }
    }
}
