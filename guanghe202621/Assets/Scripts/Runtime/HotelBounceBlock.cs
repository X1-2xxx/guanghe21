using System.Collections.Generic;
using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D), typeof(ManipulableBlock))]
    [AddComponentMenu("Level Kit/Hotel Bounce Block")]
    public sealed class HotelBounceBlock : MonoBehaviour
    {
        public enum BlockKind { Pillow, Bed }

        [SerializeField] private BlockKind kind;
        [SerializeField, Min(0.1f)] private float pillowLaunchSpeed = 10f;
        [SerializeField, Range(0f, 1f)] private float bounceRetention = 1f;
        [SerializeField, Range(0f, 1f)] private float tangentRetention = 1f;
        [SerializeField, Min(0.1f)] private float minimumImpactSpeed = 0.1f;
        [Header("Consecutive Bed Bounces")]
        [Tooltip("Each consecutive bed impact applies this factor again to the rebound coefficient. Landing on regular ground or respawning resets the chain.")]
        [SerializeField, Range(0f, 1f)] private float consecutiveBounceDecay = 0.8f;
        [Tooltip("Stop bouncing when the decayed rebound speed falls below this value.")]
        [SerializeField, Min(0.1f)] private float stopBounceSpeed = 1f;
        [Header("Pillow Animation")]
        [Tooltip("The artwork child to squash. Uses the Visual child when unassigned.")]
        [SerializeField] private Transform pillowVisual;
        [SerializeField, Range(0.2f, 1f)] private float pillowCompressedScale = 0.65f;
        [SerializeField, Range(1f, 1.5f)] private float pillowStretchedScale = 1.15f;
        [SerializeField, Min(0.05f)] private float pillowAnimationSeconds = 0.3f;

        public BlockKind Kind => kind;

        private readonly HashSet<Collider2D> activatedContacts = new HashSet<Collider2D>();
        private Rigidbody2D body;
        private BoxCollider2D surface;
        private Vector2 surfaceVelocity;
        private Vector2 surfaceCenter;
        private float surfaceSpin;
        private Vector3 pillowRestScale;
        private Vector3 pillowRestPosition;
        private float pillowAnimationElapsed;
        private float pillowStartScale = 1f;
        private float pillowCurrentScale = 1f;
        private float pillowAnimationAnchorY;
        private bool pillowAnimating;
        private bool pillowVisualReady;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            surface = GetComponent<BoxCollider2D>();
            CacheSurfaceMotion();
            if (kind == BlockKind.Pillow)
            {
                if (!pillowVisual) pillowVisual = transform.Find("Visual");
                // Animate artwork only; never scale the physical block or its collider.
                if (pillowVisual && pillowVisual != transform && pillowVisual.IsChildOf(transform)
                    && !pillowVisual.GetComponentInChildren<Collider2D>(true)
                    && !pillowVisual.GetComponentInChildren<Rigidbody2D>(true))
                {
                    pillowRestScale = pillowVisual.localScale;
                    pillowRestPosition = pillowVisual.localPosition;
                    pillowVisualReady = true;
                }
            }
        }

        private void FixedUpdate() => CacheSurfaceMotion();

        private void CacheSurfaceMotion()
        {
            surfaceVelocity = body.linearVelocity;
            surfaceCenter = body.worldCenterOfMass;
            surfaceSpin = body.angularVelocity * Mathf.Deg2Rad;
        }

        private void OnCollisionEnter2D(Collision2D collision) => TryBounce(collision);
        private void OnCollisionStay2D(Collision2D collision) => TryBounce(collision);
        private void OnCollisionExit2D(Collision2D collision) => activatedContacts.Remove(collision.collider);
        private void OnDisable()
        {
            activatedContacts.Clear();
            pillowAnimating = false;
            if (pillowVisualReady && pillowVisual)
            {
                pillowVisual.localScale = pillowRestScale;
                pillowVisual.localPosition = pillowRestPosition;
                pillowCurrentScale = 1f;
            }
        }

        private void LateUpdate()
        {
            if (!pillowAnimating || !pillowVisual) return;
            pillowAnimationElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(pillowAnimationElapsed / Mathf.Max(0.05f, pillowAnimationSeconds));
            float scale;
            if (progress < 0.25f)
                scale = Mathf.SmoothStep(pillowStartScale, pillowCompressedScale, progress / 0.25f);
            else if (progress < 0.6f)
                scale = Mathf.SmoothStep(pillowCompressedScale, pillowStretchedScale, (progress - 0.25f) / 0.35f);
            else
                scale = Mathf.SmoothStep(pillowStretchedScale, 1f, (progress - 0.6f) / 0.4f);

            pillowCurrentScale = scale;
            var visualScale = pillowRestScale;
            visualScale.y *= scale;
            pillowVisual.localScale = visualScale;
            var position = pillowRestPosition;
            position.y = pillowAnimationAnchorY + (pillowRestPosition.y - pillowAnimationAnchorY) * scale;
            pillowVisual.localPosition = position;
            if (progress >= 1f)
            {
                pillowVisual.localScale = pillowRestScale;
                pillowVisual.localPosition = pillowRestPosition;
                pillowCurrentScale = 1f;
                pillowAnimating = false;
            }
        }

        private void PlayPillowAnimation(bool onTopFace)
        {
            if (!pillowVisualReady || !pillowVisual) return;
            // Hold the opposite face still, including when the pillow is upside down.
            pillowAnimationAnchorY = surface.offset.y + surface.size.y * (onTopFace ? -0.5f : 0.5f);
            pillowStartScale = pillowCurrentScale;
            pillowAnimationElapsed = 0f;
            pillowAnimating = true;
        }

        private void TryBounce(Collision2D collision)
        {
            if (!isActiveAndEnabled || activatedContacts.Contains(collision.collider)) return;
            var visitor = collision.rigidbody;
            if (!visitor || !visitor.TryGetComponent<PlayerActionController>(out var player) || !player.isActiveAndEnabled)
                return;

            var rotation = Quaternion.Euler(0f, 0f, body.rotation);
            Vector2 normal = rotation * Vector2.up;
            float top = surface.offset.y + surface.size.y * 0.5f;
            float bottom = surface.offset.y - surface.size.y * 0.5f;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                Vector2 local = Quaternion.Inverse(rotation) * (Vector3)(contact.point - body.position);
                local = new Vector2(local.x / transform.lossyScale.x, local.y / transform.lossyScale.y);
                if (Mathf.Abs(Vector2.Dot(contact.normal, normal)) < 0.65f) continue;
                bool onTopFace = local.y >= top - 0.06f;
                if (kind == BlockKind.Pillow)
                {
                    if (!onTopFace && local.y > bottom + 0.06f) continue;
                    Vector2 faceNormal = onTopFace ? normal : -normal;
                    // Either broad face can be stepped on. Head and side impacts do not launch.
                    if (faceNormal.y <= 0.05f ||
                        Vector2.Dot((Vector2)collision.collider.bounds.center - contact.point, faceNormal) <= 0f) continue;
                    if (player.TryLaunchUpwards(pillowLaunchSpeed))
                    {
                        activatedContacts.Add(collision.collider);
                        PlayPillowAnimation(onTopFace);
                    }
                    return;
                }
                if (!onTopFace) continue;
                activatedContacts.Add(collision.collider);

                // Collision2D reports the incoming velocities before this physics
                // step's gravity. Include it so each bounce retains impact speed.
                Vector2 relative = collision.relativeVelocity
                    + Physics2D.gravity * ((visitor.gravityScale - body.gravityScale) * Time.fixedDeltaTime);
                float normalSpeed = Mathf.Abs(Vector2.Dot(relative, normal));
                float retention = Mathf.Clamp01(bounceRetention) * Mathf.Pow(Mathf.Clamp01(consecutiveBounceDecay), player.ConsecutiveBedBounces);
                float reboundSpeed = normalSpeed * retention;
                if (normalSpeed < minimumImpactSpeed || reboundSpeed < stopBounceSpeed)
                {
                    player.StopOnBed();
                    return;
                }

                if (Vector2.Dot(relative, normal) > 0f) relative = -relative;
                Vector2 tangent = relative - normal * Vector2.Dot(relative, normal);
                // The solver has already pushed the bed by this callback. Use its
                // motion before the collision so that impulse does not reduce the rebound.
                Vector2 radius = contact.point - surfaceCenter;
                Vector2 pointVelocity = surfaceVelocity + new Vector2(-radius.y, radius.x) * surfaceSpin;
                Vector2 rebound = pointVelocity + tangent * tangentRetention
                    + normal * reboundSpeed;
                player.BounceFromBed(rebound);
                return;
            }
        }
    }
}
