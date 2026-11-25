using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace LevelKit
{
    [DisallowMultipleComponent, AddComponentMenu("Level Kit/Camera Target")]
    public sealed class CameraTarget : MonoBehaviour
    {
        internal static readonly HashSet<CameraTarget> InScene = new HashSet<CameraTarget>();

        [SerializeField] private Transform position1;
        [SerializeField, FormerlySerializedAs("center")] private Transform position2;
        [SerializeField, Tooltip("Off: X selects left/right. On: Y selects lower/upper. Position1 is the negative side; Position2 is the positive side.")]
        private bool useYAxis;
        internal const float MaximumHistorySeconds = 10f;
        [SerializeField, Range(0.1f, MaximumHistorySeconds)]
        [Tooltip("Y mode only: compare the player's position this many seconds ago with the gate to determine the arrival side.")]
        private float yHistorySeconds = 2f;
        [SerializeField, Min(0.1f)]
        [Tooltip("Y mode only: total horizontal detection width in world units, centered on this target. A width of 16 covers 8 units left and 8 units right.")]
        private float yDetectionWidth = 16f;
        [SerializeField, Min(0f)] private float transitionSeconds = 0.7f;
        [SerializeField, FormerlySerializedAs("triggerHeight"), Min(0.1f)]
        [Tooltip("Vertical detection height in X mode. Y mode uses Y Detection Width instead.")]
        private float triggerSpan = 3f;
        [SerializeField, Min(0.01f)] private float crossingMargin = 0.1f;

        internal bool HasTwoPositions => position1 && position2;
        internal float TransitionSeconds => transitionSeconds;
        internal bool UsesYHistory => useYAxis;
        internal float YHistorySeconds => Mathf.Clamp(yHistorySeconds, 0.1f, MaximumHistorySeconds);
        private float DetectionSpan => Mathf.Max(0.1f, useYAxis ? yDetectionWidth : triggerSpan);

        private void OnEnable() => InScene.Add(this);
        private void OnDisable() => InScene.Remove(this);

        internal Transform GetCenter(bool right) => position1 && (!right || !position2) ? position1 : position2;

        private float AxisValue(Vector2 point) => useYAxis ? point.y : point.x;
        private float CrossAxisValue(Vector2 point) => useYAxis ? point.x : point.y;

        internal bool IsPosition2Side(Vector2 point) => AxisValue(point) > AxisValue(transform.position);

        internal bool ResolveArrivalSide(Vector2 historicalPosition, bool fallback)
        {
            if (!useYAxis) return fallback;
            float oldDistance = historicalPosition.y - transform.position.y;
            if (oldDistance < -crossingMargin) return true;
            if (oldDistance > crossingMargin) return false;
            return fallback;
        }

        internal bool ResolveSide(Vector2 point, bool previous)
        {
            float distance = AxisValue(point) - AxisValue(transform.position);
            if (distance > crossingMargin) return true;
            if (distance < -crossingMargin) return false;
            return previous;
        }

        internal bool TryCross(Vector2 from, Vector2 to, bool previous, out bool right, out float progress)
            => TryCrossBody(from, to, previous, Vector2.zero, Vector2.zero, out right, out progress);

        internal bool TryCrossBody(Vector2 from, Vector2 to, bool previous,
            Vector2 bodyCenterOffset, Vector2 bodyExtents, out bool right, out float progress)
        {
            progress = 0f;
            right = previous;
            float fromAxis = AxisValue(from);
            float toAxis = AxisValue(to);
            if (Mathf.Approximately(fromAxis, toAxis)) return false;
            right = ResolveSide(to, previous);
            if (right == previous) return false;
            Vector2 gate = transform.position;
            float edge = AxisValue(gate) + (right ? crossingMargin : -crossingMargin);
            progress = Mathf.Clamp01((edge - fromAxis) / (toAxis - fromAxis));
            float perpendicular = Mathf.Lerp(CrossAxisValue(from), CrossAxisValue(to), progress)
                + CrossAxisValue(bodyCenterOffset);
            float perpendicularExtent = Mathf.Max(0f, CrossAxisValue(bodyExtents));
            return Mathf.Abs(perpendicular - CrossAxisValue(gate)) <= DetectionSpan * 0.5f + perpendicularExtent;
        }

        private void OnDrawGizmosSelected()
        {
            var original = Gizmos.color;
            var gate = transform.position;
            Gizmos.color = Color.yellow;
            var lineDirection = useYAxis ? Vector3.right : Vector3.up;
            var start = gate - lineDirection * (DetectionSpan * 0.5f);
            var end = gate + lineDirection * (DetectionSpan * 0.5f);
            Gizmos.DrawLine(start, end);
            var tick = (useYAxis ? Vector3.up : Vector3.right) * 0.15f;
            Gizmos.DrawLine(start - tick, start + tick);
            Gizmos.DrawLine(end - tick, end + tick);
            DrawCenter(position1, Color.cyan);
            DrawCenter(position2, Color.magenta);
            Gizmos.color = original;
        }

        private void DrawCenter(Transform center, Color color)
        {
            if (!center) return;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(center.position, 0.18f);
            Gizmos.DrawLine(transform.position, center.position);
        }
    }
}
