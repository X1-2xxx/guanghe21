using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D), typeof(ManipulableBlock))]
    [AddComponentMenu("Level Kit/Spring Block")]
    public sealed class SpringBlock : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField, Min(0.1f)] private float launchSpeed = 6.5f;
        [SerializeField, Min(0.02f)] private float extensionSeconds = 0.3f;

        private const float RestHeight = 0.2f;
        private const float ExtendedHeight = 1f;
        private const float ContactTolerance = 0.04f;
        private const float Clearance = 0.02f;

        private BoxCollider2D platform;
        private Collider2D rider;
        private float currentHeight;
        private float extensionLeft;

        private void Awake()
        {
            platform = GetComponent<BoxCollider2D>();
            SetHeight(RestHeight);
        }

        private void OnCollisionEnter2D(Collision2D collision) => TryLaunch(collision);
        private void OnCollisionStay2D(Collision2D collision) => TryLaunch(collision);

        private void TryLaunch(Collision2D collision)
        {
            if (!isActiveAndEnabled || rider || extensionLeft > 0f) return;
            var incomingBody = collision.rigidbody;
            if (!incomingBody || !incomingBody.TryGetComponent<PlayerActionController>(out var player)) return;

            var incoming = collision.collider;
            Vector2 up = transform.up;
            if (up.y <= 0.05f) return;
            float top = platform.offset.y + platform.size.y * 0.5f;
            Vector2 center = transform.InverseTransformPoint(incoming.bounds.center);
            if (center.y <= top) return;

            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                var point = transform.InverseTransformPoint(contact.point);
                if (Mathf.Abs(Vector2.Dot(contact.normal, up)) < 0.5f || point.y < top - ContactTolerance) continue;
                if (!player.TryLaunchUpwards(launchSpeed)) return;
                rider = incoming;
                extensionLeft = extensionSeconds;
                return;
            }
        }

        private void FixedUpdate()
        {
            if (rider)
            {
                var riderBounds = rider.bounds;
                Vector2 right = transform.right;
                float rightScale = Mathf.Max(transform.TransformVector(Vector3.right).magnitude, 0.001f);
                float centerX = transform.InverseTransformPoint(riderBounds.center).x;
                float halfWidth = ProjectExtent(riderBounds, right) / rightScale;
                if (!rider.isActiveAndEnabled || Mathf.Abs(centerX - platform.offset.x) >= platform.size.x * 0.5f + halfWidth)
                    rider = null;
            }

            if (extensionLeft <= 0f) return;
            extensionLeft -= Time.fixedDeltaTime;
            if (extensionLeft <= 0f)
            {
                SetHeight(RestHeight);
                return;
            }

            float nextHeight = ExtendedHeight;
            if (rider)
            {
                Vector2 up = transform.up;
                var riderBounds = rider.bounds;
                float verticalScale = Mathf.Max(transform.TransformVector(Vector3.up).magnitude, 0.001f);
                float bottom = Vector2.Dot(riderBounds.center - transform.position, up) - ProjectExtent(riderBounds, up);
                float clearanceHeight = (bottom - Clearance) / verticalScale;
                nextHeight = Mathf.Clamp(clearanceHeight, RestHeight, ExtendedHeight);
            }
            SetHeight(nextHeight);
        }

        private static float ProjectExtent(Bounds bounds, Vector2 axis) =>
            Mathf.Abs(axis.x) * bounds.extents.x + Mathf.Abs(axis.y) * bounds.extents.y;

        private void OnDisable()
        {
            rider = null;
            extensionLeft = 0f;
            if (platform) SetHeight(RestHeight);
        }

        private void SetHeight(float height)
        {
            if (Mathf.Approximately(currentHeight, height)) return;
            currentHeight = height;
            platform.size = new Vector2(platform.size.x, height);
            platform.offset = new Vector2(platform.offset.x, height * 0.5f);
            if (!visual) return;

            var scale = visual.localScale;
            scale.y = height;
            visual.localScale = scale;
            var position = visual.localPosition;
            position.y = height * 0.5f;
            visual.localPosition = position;
        }
    }
}
