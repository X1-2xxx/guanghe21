using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>挂到 NPC 或剧情触发器，在 Inspector 填写多段、多句可复用对话。</summary>
[DisallowMultipleComponent]
public sealed class BubbleDialogSequence : MonoBehaviour
{
    [Serializable]
    public sealed class Line
    {
        [Tooltip("留空使用组件的默认说话角色，可为同一段中的不同角色分别指定。")]
        [InspectorName("说话角色")] public Transform speaker;
        [InspectorName("显示的角色名称")] public string speakerName;
        [InspectorName("这句是旁白")] public bool narration;
        [TextArea(2, 8), InspectorName("台词内容")] public string text;
        [Min(0.1f), Tooltip("文字显示完整之后的阅读时间。")]
        [InspectorName("阅读时长")] public float duration = 3f;
        [Min(0f), InspectorName("句后间隔")] public float delayAfter = 0.2f;
        [InspectorName("打字机效果")] public bool typewriter;
        [HideInInspector] public Vector3 headOffset = new Vector3(0f, 1.8f, 0f);
    }

    [Serializable]
    public sealed class Conversation
    {
        [Tooltip("供玩法脚本调用，例如 intro、quest_done、after_battle。")]
        [InspectorName("分组标识")] public string id = "intro";
        [InspectorName("允许重复播放")] public bool allowReplay = true;
        [InspectorName("台词列表")] public List<Line> lines = new List<Line>();
    }

    public enum TriggerMode
    {
        [InspectorName("手动调用")] Manual,
        [InspectorName("组件启用时")] OnEnable,
        [InspectorName("进入二维触发区")] Trigger2D
    }
    public bool IsPlaying { get; private set; }

    [Header("角色和触发方式")]
    [SerializeField, InspectorName("默认说话角色")] private Transform defaultSpeaker;
    [SerializeField, InspectorName("触发方式")] private TriggerMode triggerMode;
    [SerializeField, InspectorName("玩家标签")] private string triggerTag = "Player";
    [SerializeField, Tooltip("下一段播放完以后，再次触发是否循环回第一段。")]
    [InspectorName("循环播放所有分组")] private bool loopConversations;
    [Header("在这里依次填写多段剧情，每段可以添加多句")]
    [SerializeField, InspectorName("对话分组")] private List<Conversation> conversations = new List<Conversation>();
    [Header("可在面板中绑定相机演出或其他剧情事件")]
    [SerializeField, InspectorName("对话开始时")] private UnityEvent onConversationStarted = new UnityEvent();
    [SerializeField, InspectorName("对话结束时")] private UnityEvent onConversationFinished = new UnityEvent();

    private readonly HashSet<int> completedConversations = new HashSet<int>();
    private readonly HashSet<Collider2D> overlappingColliders = new HashSet<Collider2D>();
    private BubbleDialogManager manager;
    private Conversation activeConversation;
    private int activeIndex;
    private int nextConversationIndex;
    private int lineIndex;
    private int bubbleId;
    private float delayRemaining;
    private bool waitingForBubble;
    private bool pendingAutoPlay;
    private int playbackVersion;

    private void OnEnable()
    {
        // 延迟到 Update，保证场景里的管理器已经执行完 Awake。
        pendingAutoPlay = triggerMode == TriggerMode.OnEnable;
    }

    private void OnDisable()
    {
        pendingAutoPlay = false;
        overlappingColliders.Clear();
        StopConversation();
    }

    private void Update()
    {
        if (pendingAutoPlay)
        {
            pendingAutoPlay = false;
            PlayNextConversation();
        }
        if (!IsPlaying) return;
        if (manager == null || !manager.isActiveAndEnabled)
        {
            StopConversation();
            return;
        }
        if (waitingForBubble)
        {
            if (manager.IsBubbleActive(bubbleId)) return;
            waitingForBubble = false;
            bubbleId = 0;
            delayRemaining = Mathf.Max(0f, activeConversation.lines[lineIndex].delayAfter);
            lineIndex++;
        }
        else
        {
            delayRemaining -= manager.UsesUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
        if (delayRemaining <= 0f) BeginNextLine();
    }

    [ContextMenu("播放下一段对话")]
    public void PlayNextConversation()
    {
        if (IsPlaying || conversations.Count == 0) return;
        int index = nextConversationIndex;
        if (index >= conversations.Count)
        {
            if (!loopConversations) return;
            index = 0;
        }
        // 循环时跳过已经播放过、且设置为禁止重复的分组。
        for (int checkedCount = 0; checkedCount < conversations.Count; checkedCount++)
        {
            Conversation conversation = conversations[index];
            if (conversation != null && (conversation.allowReplay || !completedConversations.Contains(index)))
            {
                PlayConversationAtIndex(index);
                return;
            }
            index++;
            if (index >= conversations.Count)
            {
                if (!loopConversations) return;
                index = 0;
            }
        }
    }

    /// <summary>例如任务完成时调用 PlayConversation("quest_done")。</summary>
    public void PlayConversation(string id)
    {
        int index = conversations.FindIndex(item => item != null && item.id == id);
        if (index < 0)
        {
            Debug.LogWarning($"找不到气泡对话分组：{id}", this);
            return;
        }
        PlayConversationAtIndex(index);
    }

    public void PlayConversationAtIndex(int index)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || IsPlaying) return;
        if (index < 0 || index >= conversations.Count) return;
        Conversation conversation = conversations[index];
        if (conversation == null || conversation.lines == null || conversation.lines.Count == 0) return;
        if (!conversation.allowReplay && completedConversations.Contains(index)) return;
        manager = BubbleDialogManager.Instance;
        if (manager == null || !manager.isActiveAndEnabled)
        {
            Debug.LogWarning("场景里需要有已启用的 BubbleDialogManager。", this);
            return;
        }
        activeConversation = conversation;
        activeIndex = index;
        lineIndex = 0;
        bubbleId = 0;
        delayRemaining = 0f;
        IsPlaying = true;
        int version = ++playbackVersion;
        onConversationStarted.Invoke();
        if (IsPlaying && version == playbackVersion) BeginNextLine();
    }

    public void StopConversation()
    {
        playbackVersion++;
        IsPlaying = false;
        if (manager != null && bubbleId != 0) manager.HideBubble(bubbleId);
        bubbleId = 0;
        waitingForBubble = false;
    }

    /// <summary>同场景重新开始剧情时调用，可再次播放禁止重复的分组。</summary>
    public void ResetProgress()
    {
        StopConversation();
        completedConversations.Clear();
        nextConversationIndex = 0;
    }

    private void BeginNextLine()
    {
        // 空行会跳过，避免填表过程中暂留的空项阻断后续剧情。
        while (lineIndex < activeConversation.lines.Count)
        {
            Line line = activeConversation.lines[lineIndex];
            if (line == null || string.IsNullOrWhiteSpace(line.text))
            {
                lineIndex++;
                continue;
            }
            Transform speaker = line.speaker != null ? line.speaker : defaultSpeaker;
            if (speaker == null) speaker = transform;
            bubbleId = line.narration
                ? manager.ShowDialogue("旁白", line.text, line.duration, line.typewriter)
                : manager.ShowBubble(speaker, line.text, line.duration, line.headOffset, line.typewriter, line.speakerName);
            if (bubbleId == 0)
            {
                StopConversation();
                return;
            }
            waitingForBubble = true;
            return;
        }
        completedConversations.Add(activeIndex);
        nextConversationIndex = activeIndex + 1;
        IsPlaying = false;
        onConversationFinished.Invoke();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggerMode != TriggerMode.Trigger2D || !other.CompareTag(triggerTag)) return;
        overlappingColliders.Add(other);
        if (overlappingColliders.Count == 1) PlayNextConversation();
    }

    private void OnTriggerExit2D(Collider2D other) => overlappingColliders.Remove(other);
}
