using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>跟随脚本可实现此接口，让演出准确返回当前应有的跟随位置。</summary>
public interface ICameraFollowPositionProvider
{
    bool TryGetFollowPosition(out Vector3 position);
}

/// <summary>2D 正交相机演出。仅依赖 Camera，通过 Inspector 指定要暂时关闭的跟随脚本。</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public sealed class CameraPerformanceManager : MonoBehaviour
{
    [Serializable]
    public sealed class Shot
    {
        [Tooltip("填写目标时跟随目标移动；留空时使用世界坐标。")]
        [InspectorName("聚焦目标")] public Transform target;
        [InspectorName("世界坐标")] public Vector2 position;
        [InspectorName("聚焦偏移")] public Vector2 offset;
        [Min(0f), Tooltip("正交尺寸越小，画面越近。0 表示保持上一镜头的尺寸。")]
        [InspectorName("变焦尺寸")] public float orthographicSize;
        [Min(0f), InspectorName("移动时长")] public float moveDuration = 0.8f;
        [Min(0f), InspectorName("停留时长")] public float holdDuration = 1f;
    }

    public static CameraPerformanceManager Instance { get; private set; }
    public bool IsPerforming => phase != Phase.Idle;
    public string StatusText { get; private set; } = "等待触发";
    public event Action PerformanceStarted;
    // true 为正常结束，false 为中断。通知发生前已恢复跟随脚本。
    public event Action<bool> PerformanceEnded;

    [Header("相机与玩家跟随")]
    [SerializeField, InspectorName("演出相机")] private Camera controlledCamera;
    [Tooltip("拖入原有相机跟随组件。不要拖入 Camera、本组件或整个人物对象。")]
    [SerializeField, InspectorName("需要暂停的跟随组件")] private MonoBehaviour[] followBehaviours = Array.Empty<MonoBehaviour>();
    [SerializeField, InspectorName("玩家返回目标")]
    [Tooltip("拖入玩家。留空时播放演出会尝试查找 Player 标签。")]
    private Transform playerTarget;
    [SerializeField, InspectorName("返回玩家时的偏移")]
    private Vector2 playerReturnOffset = new Vector2(0f, 1f);
    [Header("演出设置")]
    [SerializeField, InspectorName("缓动曲线")] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Min(0f), InspectorName("返回玩家时长")] private float returnDuration = 0.6f;
    [SerializeField, InspectorName("忽略游戏暂停")] private bool useUnscaledTime;
    [Header("可在面板配置多段镜头")]
    [SerializeField, InspectorName("镜头列表")] private List<Shot> configuredShots = new List<Shot>();

    private enum Phase { Idle, Moving, Holding, Returning }
    private Phase phase;
    private readonly Dictionary<MonoBehaviour, bool> savedFollowStates = new Dictionary<MonoBehaviour, bool>();
    private readonly List<Shot> shots = new List<Shot>();
    private int shotIndex;
    private float elapsed;
    private float activeReturnDuration;
    private Vector3 savedPosition;
    private float savedSize;
    private Vector3 phaseStartPosition;
    private float phaseStartSize;
    private Vector2 lastFocus;
    private int operationVersion;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(this);
            return;
        }
        Instance = this;
        if (controlledCamera == null) controlledCamera = GetComponent<Camera>();
        if (controlledCamera == null) controlledCamera = Camera.main;
    }

    private void OnDisable() => StopPerformance();

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>可绑定按钮、触发器或其他组件的 UnityEvent。</summary>
    [ContextMenu("播放面板配置的演出")]
    public void PlayConfiguredPerformance() => PlaySequence(configuredShots);

    public void FocusOn(Transform target, float size = 0f, float moveSeconds = 0.8f, float holdSeconds = 1f)
    {
        if (target == null) return;
        PlaySequence(new[] { new Shot
        {
            target = target, orthographicSize = size,
            moveDuration = moveSeconds, holdDuration = holdSeconds
        } });
    }

    public void FocusAt(Vector2 worldPosition, float size = 0f, float moveSeconds = 0.8f, float holdSeconds = 1f)
    {
        PlaySequence(new[] { new Shot
        {
            position = worldPosition, orthographicSize = size,
            moveDuration = moveSeconds, holdDuration = holdSeconds
        } });
    }

    public void ZoomTo(float size, float moveSeconds = 0.8f, float holdSeconds = 1f)
    {
        if (controlledCamera != null)
            FocusAt(controlledCamera.transform.position, size, moveSeconds, holdSeconds);
    }

    /// <summary>新演出替换当前演出。结束时返回玩家当前所在位置。</summary>
    public void PlaySequence(IEnumerable<Shot> sequence)
    {
        if (!Application.isPlaying) return;
        if (!isActiveAndEnabled)
        {
            StatusText = "无法播放：演出组件已关闭";
            Debug.LogWarning(StatusText, this);
            return;
        }
        if (controlledCamera == null)
        {
            StatusText = "无法播放：未绑定相机";
            Debug.LogError(StatusText, this);
            return;
        }
        if (!controlledCamera.orthographic)
        {
            StatusText = "无法播放：相机需要设为正交";
            Debug.LogError("相机演出需要正交相机，请勾选 Camera 的 Orthographic。", this);
            return;
        }
        if (sequence == null)
        {
            StatusText = "无法播放：没有镜头配置";
            Debug.LogWarning(StatusText, this);
            return;
        }

        // 先复制，支持调用方传入内部配置列表，也避免运行中修改配置影响当前演出。
        var requestedShots = new List<Shot>();
        foreach (var shot in sequence)
        {
            if (shot == null) continue;
            requestedShots.Add(new Shot
            {
                target = shot.target, position = shot.position, offset = shot.offset,
                orthographicSize = shot.orthographicSize,
                moveDuration = Mathf.Max(0f, shot.moveDuration),
                holdDuration = Mathf.Max(0f, shot.holdDuration)
            });
        }
        if (requestedShots.Count == 0)
        {
            StatusText = "无法播放：镜头列表为空";
            Debug.LogWarning("请在相机演出组件的“镜头列表”中至少填写一个目标或世界坐标。", this);
            return;
        }
        int requestVersion = ++operationVersion;
        if (IsPerforming) Finish(false, false, true);
        // 结束通知可能触发新的演出，以最后一次外部调用为准。
        if (requestVersion != operationVersion) return;

        savedPosition = controlledCamera.transform.position;
        savedSize = controlledCamera.orthographicSize;
        if (playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTarget = player.transform;
        }
        savedFollowStates.Clear();
        foreach (var behaviour in followBehaviours ?? Array.Empty<MonoBehaviour>())
        {
            if (behaviour == null || behaviour == this || savedFollowStates.ContainsKey(behaviour)) continue;
            savedFollowStates.Add(behaviour, behaviour.enabled);
            behaviour.enabled = false;
        }
        shots.Clear();
        shots.AddRange(requestedShots);
        shotIndex = 0;
        activeReturnDuration = Mathf.Max(0f, returnDuration);
        BeginPhase(Phase.Moving);
        PerformanceStarted?.Invoke();
    }

    /// <summary>立即中断，默认返回玩家当前跟随位置，恢复变焦和原有跟随开关。</summary>
    public void StopPerformance(bool restoreCamera = true)
    {
        operationVersion++;
        if (IsPerforming) Finish(false, restoreCamera, true);
    }

    /// <summary>角色动态生成或切换时，可由玩法代码更新返回目标。</summary>
    public void SetPlayerTarget(Transform target, Vector2 offset)
    {
        playerTarget = target;
        playerReturnOffset = offset;
    }

    private void LateUpdate()
    {
        if (!IsPerforming) return;
        if (controlledCamera == null)
        {
            Finish(false, false, true);
            return;
        }
        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (delta <= 0f) return;
        elapsed += delta;

        if (phase == Phase.Returning)
        {
            float progress = Progress(activeReturnDuration);
            ApplyPose(GetPlayerReturnPosition(), savedSize, Ease(progress));
            if (progress >= 1f) Finish(true, true, true);
            return;
        }

        Shot shot = shots[shotIndex];
        if (shot.target != null) lastFocus = shot.target.position;
        Vector2 focus = lastFocus + shot.offset;
        var targetPosition = new Vector3(focus.x, focus.y, savedPosition.z);
        float targetSize = shot.orthographicSize > 0f ? shot.orthographicSize : phaseStartSize;
        if (phase == Phase.Moving)
        {
            float progress = Progress(shot.moveDuration);
            ApplyPose(targetPosition, targetSize, Ease(progress));
            if (progress >= 1f)
            {
                phase = Phase.Holding;
                StatusText = "聚焦停留中";
                elapsed = 0f;
            }
        }
        else
        {
            // 停留阶段也跟随演出目标，目标被销毁后保持最后一个有效位置。
            if (shot.target != null) controlledCamera.transform.position = targetPosition;
            if (elapsed < shot.holdDuration) return;
            shotIndex++;
            BeginPhase(shotIndex < shots.Count ? Phase.Moving : Phase.Returning);
        }
    }

    private void BeginPhase(Phase next)
    {
        phase = next;
        StatusText = next == Phase.Returning ? "正在返回玩家" : "正在移向目标";
        elapsed = 0f;
        phaseStartPosition = controlledCamera.transform.position;
        phaseStartSize = controlledCamera.orthographicSize;
        if (next == Phase.Moving)
        {
            Shot shot = shots[shotIndex];
            lastFocus = shot.target != null ? (Vector2)shot.target.position : shot.position;
        }
    }

    private float Progress(float duration) => duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);

    private float Ease(float progress)
    {
        if (progress >= 1f) return 1f;
        return easing == null ? progress : Mathf.Clamp01(easing.Evaluate(progress));
    }

    private void ApplyPose(Vector3 targetPosition, float targetSize, float progress)
    {
        controlledCamera.transform.position = Vector3.Lerp(phaseStartPosition, targetPosition, progress);
        controlledCamera.orthographicSize = Mathf.Lerp(phaseStartSize, Mathf.Max(0.01f, targetSize), progress);
    }

    private Vector3 GetPlayerReturnPosition()
    {
        // 优先使用正式跟随脚本的实时目标，兼容自定义边界、偏移等规则。
        foreach (var pair in savedFollowStates)
        {
            if (pair.Key == null || !pair.Value) continue;
            if (pair.Key is ICameraFollowPositionProvider provider &&
                provider.TryGetFollowPosition(out Vector3 position)) return position;
        }
        if (playerTarget == null) return savedPosition;
        return new Vector3(playerTarget.position.x + playerReturnOffset.x,
            playerTarget.position.y + playerReturnOffset.y, savedPosition.z);
    }

    private void Finish(bool completed, bool restoreCamera, bool notify)
    {
        phase = Phase.Idle;
        StatusText = completed ? "演出结束，已返回玩家" : "演出已中断";
        if (restoreCamera && controlledCamera != null)
        {
            controlledCamera.transform.position = GetPlayerReturnPosition();
            controlledCamera.orthographicSize = savedSize;
        }
        foreach (var pair in savedFollowStates)
            if (pair.Key != null) pair.Key.enabled = pair.Value;
        savedFollowStates.Clear();
        shots.Clear();
        if (notify) PerformanceEnded?.Invoke(completed);
    }
}
