using LevelKit;
using UnityEngine;

namespace GamePresentation
{
    /// <summary>关卡程序可拖放的触发区：只通知剧情，不传送、不重置、不暂停玩家。</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class StorySignalTrigger : MonoBehaviour
    {
        [InspectorName("剧情信号")] public string signal;
        [InspectorName("只触发一次")] public bool once = true;
        [InspectorName("附加小台词标识（可留空）")] public string bark;
        private bool fired;
        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void OnTriggerEnter2D(Collider2D other)
        {
            if ((once && fired) || !other.attachedRigidbody ||
                !other.attachedRigidbody.TryGetComponent<PlayerActionController>(out var player)) return;
            fired = true;
            if (!string.IsNullOrEmpty(signal)) PresentationEvents.Signal(signal, player);
            if (!string.IsNullOrEmpty(bark) && PresentationRuntime.Instance)
                PresentationRuntime.Instance.PlayBark(bark, this);
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.35f);
            var box = GetComponent<BoxCollider2D>();
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.offset, box.size);
        }
    }
}
