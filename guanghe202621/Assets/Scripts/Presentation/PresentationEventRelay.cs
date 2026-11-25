using UnityEngine;

namespace GamePresentation
{
    /// <summary>给其他程序/UnityEvent 的轻量转接器。只报告事实，不包含角色或关卡玩法。</summary>
    public sealed class PresentationEventRelay : MonoBehaviour
    {
        [InspectorName("事实类型")] public PlayEvent playEvent;
        [InspectorName("剧情章节标识")] public string episodeId;
        [InspectorName("剧情信号")] public string signal;
        [InspectorName("小台词标识")] public string barkId;
        [InspectorName("对应角色（可留空使用本关主角）")] public Component actor;
        public void ReportEvent() => PresentationEvents.Report(playEvent, actor ? actor : this, this);
        public void SendSignal() => PresentationEvents.Signal(signal, this);
        public void PlayStory()
        { if (PresentationRuntime.Instance) PresentationRuntime.Instance.Director.Enqueue(episodeId); }
        public void PlayBark()
        { if (PresentationRuntime.Instance) PresentationRuntime.Instance.PlayBark(barkId, this); }
    }
}
