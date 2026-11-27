using System.Collections.Generic;
using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, AddComponentMenu("Level Kit/Elevator Platform")]
    public sealed class ElevatorPlatform : MonoBehaviour
    {
        [Header("Platform and travel endpoints")]
        [SerializeField] private Rigidbody2D platformBody;
        [SerializeField] private BoxCollider2D platformCollider;
        [Tooltip("LowerPoint/UpperPoint表示台面位置。请移动这两个子物体来设置行程。")]
        [SerializeField] private Transform lowerPoint;
        [SerializeField] private Transform upperPoint;
        [SerializeField, Min(.05f)] private float moveSpeed = 1.2f;
        [SerializeField, Min(.1f)] private float acceleration = 3f;
        [SerializeField, Min(0f)] private float endpointPause = .7f;
        [Tooltip("默认主角第一次站上后持续往返；勾选后，空载时暂停。")]
        [SerializeField] private bool pauseWhenEmpty;
        [SerializeField, InspectorName("走到台面中心时停止主角主动行走")] private bool stopPlayerOnBoard = true;
        [SerializeField] private bool stopAtHeadObstruction = true;
        [Header("关卡结尾（只为最后一台升降机启用）")]
        [SerializeField] private bool triggersEnding;
        [SerializeField, Min(.1f), Tooltip("主角站在平台上累计实际行驶的秒数；端点停顿不计时，离开平台后重新计时。")]
        private float endingRideSeconds = 4f;
        private float riddenMovementSeconds;
        private bool endingRequested;
        [Header("Standing detection")]
        [SerializeField] private LayerMask playerLayers = ~0;
        [SerializeField, Range(.02f, .2f)] private float standingTolerance = .09f;
        [SerializeField, Range(.01f, .25f), InspectorName("台面中心触发容差")] private float boardingCenterTolerance = .06f;
        [Header("Chain artwork")]
        [SerializeField] private Transform leftChainAnchor;
        [SerializeField] private Transform rightChainAnchor;
        [SerializeField] private Transform leftChainAttachment;
        [SerializeField] private Transform rightChainAttachment;
        [SerializeField] private SpriteRenderer leftChain;
        [SerializeField] private SpriteRenderer rightChain;
        [SerializeField, Min(.02f)] private float chainWidth = .28f;

        private readonly List<Collider2D> nearby = new List<Collider2D>(8);
        private readonly List<RaycastHit2D> castHits = new List<RaycastHit2D>(8);
        private readonly List<Rider> riders = new List<Rider>(2);
        private bool activated;
        private bool goingUp = true;
        private float speed;
        private float pauseRemaining;
        private Vector2 lastVelocity;

        private sealed class Rider
        {
            public PlayerActionController player;
            public Rigidbody2D body;
            public Collider2D collider;
            public RigidbodyInterpolation2D interpolation;
            public bool found, reachedCenter;
            public float previousCenterOffset;
        }

        public bool IsActivated => activated;
        public bool IsGoingUp => goingUp;
        public int RiderCount => riders.Count;
        public Vector2 PlatformVelocity => lastVelocity;

        private void Awake()
        {
            if (!platformBody) platformBody = GetComponentInChildren<Rigidbody2D>();
            if (!platformCollider && platformBody) platformCollider = platformBody.GetComponent<BoxCollider2D>();
            if (!lowerPoint) lowerPoint = transform.Find("LowerPoint");
            if (!upperPoint) upperPoint = transform.Find("UpperPoint");
            RefreshChains();
        }

        private void FixedUpdate()
        {
            if (!platformBody || !platformCollider || !lowerPoint || !upperPoint || !platformBody.simulated) return;
            FindRiders();
            float dt = Time.fixedDeltaTime;
            Vector2 delta = Vector2.zero;
            if (activated && (!pauseWhenEmpty || riders.Count > 0))
            {
                if (pauseRemaining > 0f) pauseRemaining = Mathf.Max(0f, pauseRemaining - dt);
                else
                {
                    Vector2 destination = goingUp ? upperPoint.position : lowerPoint.position;
                    Vector2 remaining = destination - platformBody.position;
                    float distance = remaining.magnitude;
                    if (distance <= .0001f) ReachEndpoint();
                    else
                    {
                        float targetSpeed = Mathf.Min(moveSpeed, Mathf.Sqrt(2f * acceleration * distance));
                        speed = Mathf.MoveTowards(speed, targetSpeed, acceleration * dt);
                        delta = remaining.normalized * Mathf.Min(distance, speed * dt);
                        if (stopAtHeadObstruction && delta.y > 0f && HeadBlocked(delta.y)) { delta = Vector2.zero; speed = 0f; }
                        if (delta.sqrMagnitude > 0f && delta.magnitude >= distance - .0001f) ReachEndpoint();
                    }
                }
            }
            else speed = 0f;

            TickEnding(dt, delta);
            lastVelocity = dt > 0f ? delta / dt : Vector2.zero;
            platformBody.MovePosition(platformBody.position + delta);
            foreach (var rider in riders)
            {
                if (!rider.body || !rider.player || rider.player.IsJumping) continue;
                // Matching vertical support velocity keeps contact on descents too. Horizontal player movement remains its own.
                rider.body.linearVelocityY = lastVelocity.y;
            }
        }

        private void TickEnding(float dt, Vector2 movement)
        {
            if (!triggersEnding || endingRequested || GameStateManager.Current != GameState.Playing) return;
            if (riders.Count == 0) { riddenMovementSeconds = 0f; return; }
            if (movement.sqrMagnitude < .00000001f) return;
            riddenMovementSeconds += dt;
            if (riddenMovementSeconds < endingRideSeconds) return;
            endingRequested = true;
            GamePresentation.PresentationEvents.Signal("elevator.ending", this);
        }

        private void ReachEndpoint()
        {
            goingUp = !goingUp;
            pauseRemaining = endpointPause;
            speed = 0f;
        }

        private void FindRiders()
        {
            foreach (var rider in riders) rider.found = false;
            var bounds = platformCollider.bounds;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(playerLayers);
            nearby.Clear();
            Physics2D.OverlapBox(new Vector2(bounds.center.x, bounds.max.y), new Vector2(bounds.size.x + .04f, standingTolerance * 2f), 0f, filter, nearby);
            foreach (var collider in nearby)
            {
                if (!collider || collider == platformCollider) continue;
                var player = collider.GetComponentInParent<PlayerActionController>();
                if (!player || !player.isActiveAndEnabled || player.IsJumping || player.IsRespawning) continue;
                var body = player.GetComponent<Rigidbody2D>();
                if (!body || !body.simulated || body.bodyType != RigidbodyType2D.Dynamic || body.linearVelocityY > lastVelocity.y + .45f) continue;
                var feet = collider.bounds;
                float gap = feet.min.y - bounds.max.y;
                float overlap = Mathf.Min(feet.max.x, bounds.max.x) - Mathf.Max(feet.min.x, bounds.min.x);
                if (gap < -.065f || gap > standingTolerance || overlap < .08f) continue;
                Rider rider = null;
                foreach (var entry in riders) if (entry.body == body) { rider = entry; break; }
                if (rider == null)
                {
                    rider = new Rider { player = player, body = body, collider = collider, interpolation = body.interpolation, previousCenterOffset = feet.center.x - bounds.center.x };
                    riders.Add(rider);body.interpolation = RigidbodyInterpolation2D.Interpolate;
                }
                float centerOffset = feet.center.x - bounds.center.x;
                if (!rider.reachedCenter && HasReachedBoardingCenter(rider.previousCenterOffset, centerOffset, boardingCenterTolerance))
                {
                    rider.reachedCenter = true;
                    activated = true;
                    if (stopPlayerOnBoard) player.StopForPresentation();
                }
                rider.previousCenterOffset = centerOffset;
                rider.found = true;
            }
            for (int i = riders.Count - 1; i >= 0; i--)
                if (!riders[i].found) { Release(riders[i]); riders.RemoveAt(i); }
        }

        // Swept center crossing prevents a walking step from skipping the center; never snap the actor's position.
        internal static bool HasReachedBoardingCenter(float previousOffset, float currentOffset, float tolerance) =>
            Mathf.Abs(currentOffset) <= tolerance || previousOffset < 0f && currentOffset > 0f || previousOffset > 0f && currentOffset < 0f;

        private bool HeadBlocked(float rise)
        {
            foreach (var rider in riders)
            {
                if (!rider.collider) continue;
                var filter = new ContactFilter2D { useTriggers = false };
                filter.SetLayerMask(Physics2D.GetLayerCollisionMask(rider.body.gameObject.layer));
                castHits.Clear();
                rider.collider.Cast(Vector2.up, filter, castHits, rise + .02f);
                foreach (var hit in castHits)
                {
                    if (!hit.collider || hit.rigidbody == platformBody || hit.rigidbody == rider.body) continue;
                    if (hit.rigidbody && hit.rigidbody.bodyType == RigidbodyType2D.Dynamic) continue;
                    if (hit.normal.y < -.05f) return true;
                }
            }
            return false;
        }

        private static void Release(Rider rider)
        {
            if (rider.body) rider.body.interpolation = rider.interpolation;
        }

        private void LateUpdate() => RefreshChains();

        public void RefreshChains()
        {
            UpdateChain(leftChain, leftChainAnchor, leftChainAttachment);
            UpdateChain(rightChain, rightChainAnchor, rightChainAttachment);
        }

        private void UpdateChain(SpriteRenderer renderer, Transform anchor, Transform attachment)
        {
            if (!renderer || !renderer.sprite || !anchor || !attachment) return;
            Vector3 from = attachment.position, to = anchor.position;
            Vector2 span = to - from;
            float length = span.magnitude;
            renderer.enabled = length > .02f;
            if (!renderer.enabled) return;
            float width = chainWidth * Mathf.Abs(transform.lossyScale.x);
            float scale = width / Mathf.Max(.001f, renderer.sprite.bounds.size.x);
            var parentScale = renderer.transform.parent ? renderer.transform.parent.lossyScale : Vector3.one;
            renderer.transform.localScale = new Vector3(scale / SafeScale(parentScale.x), scale / SafeScale(parentScale.y), 1f / SafeScale(parentScale.z));
            renderer.transform.rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, span));
            renderer.transform.position = (from + to) * .5f;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = new Vector2(renderer.sprite.bounds.size.x, length / scale);
        }

        private static float SafeScale(float value) => Mathf.Abs(value) < .0001f ? .0001f : value;

        private void OnDisable()
        {
            if (platformBody) platformBody.linearVelocity = Vector2.zero;
            foreach (var rider in riders) Release(rider);
            riders.Clear();lastVelocity = Vector2.zero;speed = 0f;
        }

        [ContextMenu("Reset to lower point")]
        public void ResetToLowerPoint()
        {
            if (!platformBody || !lowerPoint) return;
            platformBody.position = lowerPoint.position;
            platformBody.linearVelocity = Vector2.zero;
            activated = false;goingUp = true;pauseRemaining = 0f;speed = 0f;lastVelocity = Vector2.zero;
            endingRequested = false; riddenMovementSeconds = 0f;
            foreach (var rider in riders) Release(rider);riders.Clear();RefreshChains();
        }

        private void OnDrawGizmosSelected()
        {
            if (!lowerPoint || !upperPoint) return;
            Gizmos.color = new Color(1f,.75f,.2f,.8f);Gizmos.DrawLine(lowerPoint.position,upperPoint.position);
            Gizmos.DrawWireCube(lowerPoint.position, new Vector3(3f,.06f,.01f));Gizmos.DrawWireCube(upperPoint.position,new Vector3(3f,.06f,.01f));
        }
    }
}
