using LevelKit;
using UnityEngine;

namespace GamePresentation
{
    /// <summary>只观察接触和旋转，不给物理块施力。可复用于其他关卡的桥板和转轴树。</summary>
    public sealed class BarkWorldObserver : MonoBehaviour
    {
        public enum Kind { Bridge, RotatingTree }
        [InspectorName("观察类型")] public Kind kind;
        [InspectorName("对应物理块")] public Rigidbody2D target;
        private Collider2D surface;
        private float stableTime, readyAt;
        private bool wasTouching;
        private float entrySide;
        private void Start() { if (target) surface = target.GetComponent<Collider2D>(); }
        private void Update()
        {
            var runtime = PresentationRuntime.Instance;
            if (!target || !surface || !runtime || !runtime.CanPresent || runtime.Director.Active) return;
            var player = runtime.Scene.player;
            var capsule = player.GetComponent<CapsuleCollider2D>();
            if (!capsule) return;
            bool touching = capsule.IsTouching(surface);
            float tilt = Mathf.Abs(Mathf.DeltaAngle(target.rotation, 0f));
            float side = player.transform.position.x - target.position.x;
            if (kind == Kind.Bridge)
            {
                if (touching && !wasTouching) entrySide = side;
                stableTime = touching && tilt >= 8f && tilt < 55f ? stableTime + Time.deltaTime : 0f;
                if (stableTime >= .45f && Time.time >= readyAt)
                { PresentationEvents.Report(PlayEvent.BridgeShook, player, target); readyAt = Time.time + 15f; }
                if (!touching && wasTouching && tilt > 10f && entrySide * side < 0f)
                    PresentationEvents.Report(PlayEvent.CrookedBridgePassed, player, target);
            }
            else if (Mathf.Abs(target.angularVelocity) > 60f && Time.time >= readyAt)
            {
                var distance = capsule.Distance(surface);
                if (distance.isValid && distance.distance > 0f && distance.distance < .9f)
                { PresentationEvents.Report(PlayEvent.TreeNearMiss, player, target); readyAt = Time.time + 12f; }
            }
            wasTouching = touching;
        }
    }
}
