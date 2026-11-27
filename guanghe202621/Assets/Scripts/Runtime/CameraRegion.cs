using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    [AddComponentMenu("Level Kit/Camera Region")]
    public sealed class CameraRegion : MonoBehaviour
    {
        internal static readonly HashSet<CameraRegion> InScene = new HashSet<CameraRegion>();
        [SerializeField, InspectorName("区域对应机位")] private Transform center;
        [SerializeField, Min(0f), InspectorName("镜头移动秒数")] private float transitionSeconds = .7f;
        [SerializeField, Min(0f), InspectorName("离开边界缓冲")] private float exitMargin = .15f;
        [SerializeField, InspectorName("重叠时优先级")] private int priority;
        [SerializeField, InspectorName("同优先级选择顺序")] private int selectionOrder;
        private BoxCollider2D area;
        internal Transform Center => center;
        internal float TransitionSeconds => transitionSeconds;
        internal BoxCollider2D Area => area ? area : area = GetComponent<BoxCollider2D>();
        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void OnEnable() { area = GetComponent<BoxCollider2D>(); area.isTrigger = true; InScene.Add(this); }
        private void OnDisable() => InScene.Remove(this);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => InScene.Clear();

        // Enter only when the player's complete collision bounds fit in the region.
        // While the body straddles a boundary, keep the previous view.
        internal bool Contains(Bounds bodyBounds, float margin = 0f)
        {
            if (!center || !isActiveAndEnabled || !Area.enabled) return false;
            Vector2 localCenter = transform.InverseTransformPoint(bodyBounds.center);
            localCenter -= Area.offset;
            Vector3 xExtent = transform.InverseTransformVector(Vector3.right * bodyBounds.extents.x);
            Vector3 yExtent = transform.InverseTransformVector(Vector3.up * bodyBounds.extents.y);
            Vector2 localExtents = new Vector2(Mathf.Abs(xExtent.x) + Mathf.Abs(yExtent.x),
                Mathf.Abs(xExtent.y) + Mathf.Abs(yExtent.y));
            Vector2 half = Area.size * .5f;
            half += new Vector2(margin / Mathf.Max(.0001f, Mathf.Abs(transform.lossyScale.x)),
                margin / Mathf.Max(.0001f, Mathf.Abs(transform.lossyScale.y)));
            return Mathf.Abs(localCenter.x) + localExtents.x <= half.x &&
                Mathf.Abs(localCenter.y) + localExtents.y <= half.y;
        }

        internal static CameraRegion Select(Scene scene, Bounds bodyBounds, CameraRegion current = null)
            => Choose(InScene, scene, bodyBounds, current);

        internal static CameraRegion Choose(IEnumerable<CameraRegion> regions, Scene scene, Bounds bodyBounds, CameraRegion current)
        {
            CameraRegion best = null;
            foreach (var region in regions)
            {
                if (!region || region.gameObject.scene != scene || !region.Contains(bodyBounds)) continue;
                if (!best || region.priority > best.priority ||
                    (region.priority == best.priority && region.selectionOrder < best.selectionOrder)) best = region;
            }
            bool validCurrent = current && current.gameObject.scene == scene && current.isActiveAndEnabled &&
                current.Center && current.Area.enabled;
            if (validCurrent && (!best || (current.Contains(bodyBounds, current.exitMargin) && best.priority <= current.priority)))
                return current;
            return best;
        }

        private void OnDrawGizmosSelected()
        {
            var saved = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(.2f, .9f, .75f, .12f);
            Gizmos.DrawCube(Area.offset, Area.size);
            Gizmos.color = new Color(.2f, .9f, .75f, 1f);
            Gizmos.DrawWireCube(Area.offset, Area.size);
            Gizmos.matrix = saved;
            if (center) { Gizmos.DrawLine(transform.position, center.position); Gizmos.DrawWireSphere(center.position, .16f); }
        }
    }
}
