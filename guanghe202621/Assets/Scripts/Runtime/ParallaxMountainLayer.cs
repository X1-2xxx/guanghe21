using System.Collections.Generic;
using UnityEngine;

namespace LevelKit
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("Level Kit/Parallax Mountain Layer")]
    public sealed class ParallaxMountainLayer : MonoBehaviour
    {
        public enum ScrollSource { Player, Camera }

        [SerializeField] private Camera targetCamera;
        [Tooltip("Player scrolls while the hero walks, including when the camera is stationary.")]
        [SerializeField] private ScrollSource scrollSource = ScrollSource.Player;
        [SerializeField] private PlayerActionController player;
        [Tooltip("The outdoor sky renderer. Mountains follow its visibility and opacity.")]
        [SerializeField] private SpriteRenderer skyBackground;
        [Tooltip("Screen movement per world unit travelled by the selected source. Smaller values feel farther away.")]
        [SerializeField, Range(0f, 1f)] private float horizontalParallax = 0.25f;
        [SerializeField, Range(0f, 1f)] private float verticalParallax;
        [Tooltip("Optional world units per second. Keep at 0 to scroll only when the selected source moves.")]
        [SerializeField] private float automaticScrollSpeed;
        [Tooltip("Mirror every other copy so the same edge meets itself, avoiding a cut in the silhouette.")]
        [SerializeField] private bool alternateMirroring = true;

        private readonly List<SpriteRenderer> copies = new List<SpriteRenderer>();
        private SpriteRenderer source;
        private Vector3 initialLocalPosition;
        private Vector3 motionOrigin;
        private Transform motionSource;
        private Color initialColor;
        private bool initialFlipX, initialEnabled, initialized;
        private float automaticOffset;

        private void OnEnable()
        {
            if (Application.isPlaying) Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;
            source = GetComponent<SpriteRenderer>();
            if (!targetCamera) targetCamera = GetComponentInParent<Camera>();
            if (!targetCamera || !source.sprite) return;
            initialLocalPosition = transform.localPosition;
            initialColor = source.color;
            initialFlipX = source.flipX;
            initialEnabled = source.enabled;
            motionSource = ResolveMotionSource();
            motionOrigin = motionSource.position;
            automaticOffset = 0f;
            initialized = true;
            UpdateLayer(0f);
        }

        private void LateUpdate()
        {
            if (!initialized) Initialize();
            UpdateLayer(Time.deltaTime);
        }

        private Transform ResolveMotionSource()
        {
            if (scrollSource == ScrollSource.Player)
            {
                if (!player)
                {
                    foreach (var candidate in FindObjectsByType<PlayerActionController>(FindObjectsSortMode.None))
                        if (candidate.gameObject.scene == gameObject.scene) { player = candidate; break; }
                }
                if (player) return player.transform;
            }
            return targetCamera.transform;
        }

        private void UpdateLayer(float deltaTime)
        {
            if (!initialized || !targetCamera || !source.sprite) return;
            float localWidth = source.sprite.bounds.size.x;
            float worldWidth = transform.TransformVector(Vector3.right * localWidth).magnitude;
            if (worldWidth < 0.0001f) return;
            automaticOffset += automaticScrollSpeed * Mathf.Max(0f, deltaTime);
            var selectedSource = ResolveMotionSource();
            if (selectedSource != motionSource)
            {
                motionSource = selectedSource;
                motionOrigin = selectedSource.position;
            }
            Vector3 travel = motionSource.position - motionOrigin;
            float period = worldWidth * (alternateMirroring ? 2f : 1f);
            // A mirrored pair is the repeat period. Wrapping by one tile would flip the image abruptly.
            float x = Mathf.Repeat(-travel.x * horizontalParallax + automaticOffset + period * 0.5f, period) - period * 0.5f;
            Vector3 worldOffset = new Vector3(x, -travel.y * verticalParallax, 0f);
            transform.localPosition = initialLocalPosition + (transform.parent
                ? transform.parent.InverseTransformVector(worldOffset) : worldOffset);

            // Cover both sides even at wide aspect ratios, zoom changes, and checkpoint camera jumps.
            Vector3 initialCenter = (transform.parent ? transform.parent.TransformPoint(initialLocalPosition) : initialLocalPosition)
                + transform.TransformVector(new Vector3(initialFlipX ? -source.sprite.bounds.center.x : source.sprite.bounds.center.x, 0f, 0f));
            float centerOffset = Mathf.Abs(initialCenter.x - targetCamera.transform.position.x);
            float halfViewport = targetCamera.orthographicSize * targetCamera.aspect;
            int halfCount = Mathf.Max(1, Mathf.CeilToInt((halfViewport + centerOffset + period * 0.5f) / worldWidth));
            ResizeCopies(halfCount * 2);

            Color tint = initialColor;
            bool visible = initialEnabled;
            if (skyBackground)
            {
                tint.a *= skyBackground.color.a;
                visible &= skyBackground.enabled && skyBackground.gameObject.activeInHierarchy;
                source.sortingLayerID = skyBackground.sortingLayerID;
                source.sortingOrder = skyBackground.sortingOrder + 1;
            }
            source.color = tint;
            source.enabled = visible;
            float center = source.sprite.bounds.center.x;
            float sourceCenter = initialFlipX ? -center : center;
            for (int i = 0; i < copies.Count; i++)
            {
                int index = i < halfCount ? i - halfCount : i - halfCount + 1;
                var copy = copies[i];
                bool flipped = initialFlipX ^ (alternateMirroring && (Mathf.Abs(index) % 2 == 1));
                // Account for an off-center sprite pivot when mirrored.
                float copyCenter = flipped ? -center : center;
                copy.transform.localPosition = new Vector3(index * localWidth + sourceCenter - copyCenter, 0f, 0f);
                copy.sprite = source.sprite;
                copy.sharedMaterial = source.sharedMaterial;
                copy.flipX = flipped;
                copy.flipY = source.flipY;
                copy.color = tint;
                copy.enabled = visible;
                copy.sortingLayerID = source.sortingLayerID;
                copy.sortingOrder = source.sortingOrder;
                copy.maskInteraction = source.maskInteraction;
            }
        }

        private void ResizeCopies(int count)
        {
            while (copies.Count < count)
            {
                var tile = new GameObject("Mountain repeat");
                tile.transform.SetParent(transform, false);
                tile.layer = gameObject.layer;
                copies.Add(tile.AddComponent<SpriteRenderer>());
            }
            while (copies.Count > count)
            {
                int last = copies.Count - 1;
                copies[last].gameObject.SetActive(false);
                DestroyCopy(copies[last].gameObject);
                copies.RemoveAt(last);
            }
        }

        private void OnDisable()
        {
            foreach (var copy in copies)
                if (copy) { copy.gameObject.SetActive(false); DestroyCopy(copy.gameObject); }
            copies.Clear();
            if (initialized)
            {
                transform.localPosition = initialLocalPosition;
                source.color = initialColor;
                source.flipX = initialFlipX;
                source.enabled = initialEnabled;
            }
            initialized = false;
        }

        private static void DestroyCopy(GameObject copy)
        {
            if (Application.isPlaying) Destroy(copy);
            else DestroyImmediate(copy);
        }
    }
}
