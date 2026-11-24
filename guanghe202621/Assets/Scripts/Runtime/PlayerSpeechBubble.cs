using System.Collections.Generic;
using UnityEngine;

namespace LevelKit
{
    [DefaultExecutionOrder(50), DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("Level Kit/Player Speech Bubble")]
    public sealed class PlayerSpeechBubble : MonoBehaviour
    {
        public enum Placement { Right, Left, Above }

        [Header("Artwork")]
        [SerializeField] private Sprite rightSprite;
        [SerializeField] private Sprite leftSprite;
        [SerializeField] private Sprite aboveSprite;
        [SerializeField] private int sortingOrder = 100;
        [Header("Size and distance (world units)")]
        [SerializeField, Min(.2f)] private float bubbleWidth = 2.6f;
        [SerializeField, Min(0f)] private float horizontalGap = .12f;
        [SerializeField, Min(0f)] private float aboveGap = .12f;
        [SerializeField, Min(0f)] private float sideHeadOverlap = .18f;
        [SerializeField, Min(0f)] private float nearbyAdjustment = .22f;
        [Header("Avoid covering the level")]
        [Tooltip("物块、平台等需要避开的层。不可见触发区不参与检测。")]
        [SerializeField] private LayerMask obstructionLayers = ~0;
        [SerializeField, Min(0f)] private float clearance = .06f;
        [SerializeField, Min(0f)] private float screenPaddingPixels = 24f;
        [SerializeField] private bool avoidArtworkWithoutSolidCollider = true;
        [SerializeField, Min(.02f)] private float recheckSeconds = .1f;
        [SerializeField, Min(0f)] private float switchImprovement = .08f;
        [SerializeField, Min(0f)] private float minimumHoldSeconds = .16f;
        [Header("Speaker")]
        [SerializeField] private PlayerActionController owner;
        [SerializeField] private Collider2D speakerCollider;
        [SerializeField] private Camera levelCamera;

        private SpriteRenderer bubbleRenderer;
        private readonly List<Collider2D> hits = new List<Collider2D>(16);
        private readonly List<SpriteRenderer> artwork = new List<SpriteRenderer>(64);
        private bool hasPlacement;
        private Candidate current;
        private float nextCheck, nextArtworkScan, lastSwitch;

        public Placement CurrentPlacement => current.direction;
        public Rect CurrentWorldRect => current.full;
        public float CurrentOcclusion => current.score;

        private struct Candidate
        {
            public Placement direction;
            public int variation;
            public Sprite sprite;
            public Vector2 center, size;
            public Rect full, paper;
            public bool onScreen, clear, fitted;
            public float score;
        }

        public void Initialize(PlayerActionController speaker, Collider2D hitbox, Camera view)
        {
            owner = speaker;
            speakerCollider = hitbox;
            if (view) levelCamera = view;
            CacheReferences();
        }

        public void Show()
        {
            CacheReferences();
            gameObject.SetActive(true);
            ScanArtwork();
            UpdatePlacement(true);
        }

        public void Hide()
        {
            hasPlacement = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void Awake() => CacheReferences();
        private void OnEnable() { CacheReferences(); hasPlacement = false; nextCheck = 0f; }
        private void OnDisable() { hasPlacement = false; artwork.Clear(); }

        private void CacheReferences()
        {
            if (!bubbleRenderer) bubbleRenderer = GetComponent<SpriteRenderer>();
            if (!owner) owner = GetComponentInParent<PlayerActionController>();
            if (!speakerCollider && owner) speakerCollider = owner.GetComponent<CapsuleCollider2D>();
            if (!levelCamera) levelCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (!speakerCollider) CacheReferences();
            if (!speakerCollider || !bubbleRenderer) return;
            float now = Time.unscaledTime;
            if (now >= nextArtworkScan) ScanArtwork();
            if (!hasPlacement || now >= nextCheck) UpdatePlacement(!hasPlacement);
            else
            {
                var follow = Build(current.direction, current.variation);
                if (current.fitted) follow = ClampToScreen(follow);
                follow.clear = current.clear;
                follow.onScreen = InsideScreen(follow.full);
                follow.score = current.score;
                Apply(follow);
            }
        }

        private void ScanArtwork()
        {
            artwork.Clear();
            if (avoidArtworkWithoutSolidCollider)
                foreach (var renderer in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (IsRelevantArtwork(renderer)) artwork.Add(renderer);
            nextArtworkScan = Time.unscaledTime + .5f;
        }

        private bool BelongsToSpeaker(Transform other) => owner && (other == owner.transform || other.IsChildOf(owner.transform));

        private bool IsRelevantArtwork(SpriteRenderer renderer)
        {
            if (!renderer || renderer == bubbleRenderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy || !renderer.sprite || renderer.color.a < .05f || BelongsToSpeaker(renderer.transform)) return false;
            if ((obstructionLayers.value & (1 << renderer.gameObject.layer)) == 0) return false;
            if (levelCamera && renderer.transform.IsChildOf(levelCamera.transform)) return false;
            if (renderer.sortingOrder < 0) return false;
            var solid = renderer.GetComponentInParent<Collider2D>();
            if (solid && solid.enabled && !solid.isTrigger) return false; // The actual physics shape is checked separately.
            if (levelCamera && levelCamera.orthographic)
            {
                float height = 2f * levelCamera.orthographicSize;
                if (renderer.bounds.size.x >= height * levelCamera.aspect * .9f && renderer.bounds.size.y >= height * .9f) return false;
            }
            return true;
        }

        private Sprite SpriteFor(Placement direction)
        {
            var desired = direction == Placement.Right ? rightSprite : direction == Placement.Left ? leftSprite : aboveSprite;
            return desired ? desired : bubbleRenderer ? bubbleRenderer.sprite : null;
        }

        private Candidate Build(Placement direction, int variation)
        {
            var sprite = SpriteFor(direction);
            float aspect = sprite ? sprite.bounds.size.x / Mathf.Max(.001f, sprite.bounds.size.y) : 2f;
            float width = bubbleWidth;
            if (levelCamera && levelCamera.orthographic)
            {
                float mx = Mathf.Min(.2f, screenPaddingPixels / Mathf.Max(1f, levelCamera.pixelWidth));
                float my = Mathf.Min(.2f, screenPaddingPixels / Mathf.Max(1f, levelCamera.pixelHeight));
                float viewHeight = 2f * levelCamera.orthographicSize;
                width = Mathf.Min(width, viewHeight * levelCamera.aspect * (1f - 2f * mx), viewHeight * (1f - 2f * my) * aspect);
            }
            var size = new Vector2(Mathf.Max(.02f, width), Mathf.Max(.02f, width / aspect));
            var bounds = speakerCollider.bounds;
            Vector2 center;
            if (direction == Placement.Above)
            {
                float shift = variation == 1 ? nearbyAdjustment : variation == 2 ? -nearbyAdjustment : 0f;
                center = new Vector2(bounds.center.x + shift, bounds.max.y + aboveGap + size.y * .5f);
            }
            else
            {
                float x = direction == Placement.Right ? bounds.max.x + horizontalGap + size.x * .5f : bounds.min.x - horizontalGap - size.x * .5f;
                center = new Vector2(x, bounds.max.y + size.y * .5f - sideHeadOverlap + nearbyAdjustment * variation);
            }
            var result = new Candidate { direction = direction, variation = variation, sprite = sprite, center = center, size = size };
            SetRects(ref result);
            return result;
        }

        private void SetRects(ref Candidate candidate)
        {
            candidate.full = new Rect(candidate.center - candidate.size * .5f, candidate.size);
            // Ignore the transparent rim and the tail; score the part which actually hides the level.
            candidate.paper = new Rect(candidate.full.xMin + candidate.size.x * .025f - clearance,
                candidate.full.yMin + candidate.size.y * .21f - clearance,
                candidate.size.x * .95f + 2f * clearance, candidate.size.y * .74f + 2f * clearance);
        }

        private float PlaneZ => owner ? owner.transform.position.z : transform.position.z;

        private void ViewportRange(Rect rect, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 4; i++)
            {
                var p = levelCamera.WorldToViewportPoint(new Vector3((i & 1) == 0 ? rect.xMin : rect.xMax, (i & 2) == 0 ? rect.yMin : rect.yMax, PlaneZ));
                min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            }
        }

        private Vector2 ViewportMargin => new Vector2(
            Mathf.Min(.2f, screenPaddingPixels / Mathf.Max(1f, levelCamera.pixelWidth)),
            Mathf.Min(.2f, screenPaddingPixels / Mathf.Max(1f, levelCamera.pixelHeight)));

        private bool InsideScreen(Rect rect)
        {
            if (!levelCamera) return true;
            ViewportRange(rect, out var min, out var max);
            var margin = ViewportMargin;
            return min.z > 0f && min.x >= margin.x - .0001f && max.x <= 1f - margin.x + .0001f && min.y >= margin.y - .0001f && max.y <= 1f - margin.y + .0001f;
        }

        private Candidate ClampToScreen(Candidate candidate)
        {
            if (!levelCamera || InsideScreen(candidate.full)) return candidate;
            ViewportRange(candidate.full, out var min, out var max);
            if (min.z <= 0f) return candidate;
            var margin = ViewportMargin;
            float dx = min.x < margin.x ? margin.x - min.x : max.x > 1f - margin.x ? 1f - margin.x - max.x : 0f;
            float dy = min.y < margin.y ? margin.y - min.y : max.y > 1f - margin.y ? 1f - margin.y - max.y : 0f;
            var at = levelCamera.WorldToViewportPoint(new Vector3(candidate.center.x, candidate.center.y, PlaneZ));
            var shift = levelCamera.ViewportToWorldPoint(at + new Vector3(dx, dy, 0f)) - levelCamera.ViewportToWorldPoint(at);
            candidate.center += (Vector2)shift;
            candidate.fitted = true;
            SetRects(ref candidate);
            return candidate;
        }

        private static float OverlapArea(Rect a, Bounds b)
        {
            float width = Mathf.Max(0f, Mathf.Min(a.xMax, b.max.x) - Mathf.Max(a.xMin, b.min.x));
            float height = Mathf.Max(0f, Mathf.Min(a.yMax, b.max.y) - Mathf.Max(a.yMin, b.min.y));
            return width * height;
        }

        private Candidate Evaluate(Candidate candidate)
        {
            candidate.onScreen = InsideScreen(candidate.full);
            float area = Mathf.Max(.001f, candidate.paper.width * candidate.paper.height);
            float overlap = 0f;
            bool blocked = false;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(obstructionLayers);
            hits.Clear();
            Physics2D.OverlapBox(candidate.paper.center, candidate.paper.size, 0f, filter, hits);
            foreach (var collider in hits)
            {
                if (!collider || !collider.enabled || BelongsToSpeaker(collider.transform)) continue;
                float covered = OverlapArea(candidate.paper, collider.bounds);
                if (covered <= .00001f) continue;
                blocked = true; overlap += covered / area + .015f;
            }
            if (avoidArtworkWithoutSolidCollider)
                foreach (var renderer in artwork)
                {
                    if (!IsRelevantArtwork(renderer)) continue;
                    float covered = OverlapArea(candidate.paper, renderer.bounds);
                    if (covered <= .00001f) continue;
                    blocked = true; overlap += covered / area;
                }
            float selfOverlap = speakerCollider ? OverlapArea(candidate.paper, speakerCollider.bounds) / area : 0f;
            if (selfOverlap > .002f) blocked = true;
            candidate.clear = candidate.onScreen && !blocked;
            candidate.score = overlap + 12f * selfOverlap + (candidate.onScreen ? 0f : 1000f);
            return candidate;
        }

        private void UpdatePlacement(bool force)
        {
            if (!speakerCollider || !bubbleRenderer) return;
            float now = Time.unscaledTime;
            nextCheck = now + recheckSeconds;
            var previous = hasPlacement ? Build(current.direction, current.variation) : default;
            if (hasPlacement && current.fitted) previous = ClampToScreen(previous);
            if (hasPlacement) previous = Evaluate(previous);
            if (!force && hasPlacement && previous.clear) { Apply(previous); return; }

            Candidate best = default;
            float bestScore = float.PositiveInfinity;
            bool found = false;
            for (int direction = 0; direction < 3 && !found; direction++)
                for (int variation = 0; variation < 3; variation++)
                {
                    var choice = Evaluate(Build((Placement)direction, variation));
                    if (choice.clear) { best = choice; found = true; break; }
                    var fitted = Evaluate(ClampToScreen(choice));
                    float shift = Vector2.Distance(choice.center, fitted.center);
                    float score = fitted.score + shift * .035f + direction * .0001f + variation * .00001f;
                    if (score < bestScore) { bestScore = score; best = fitted; }
                }

            if (!force && hasPlacement && previous.onScreen && best.direction != current.direction)
            {
                bool smallImprovement = !best.clear && previous.score - best.score < switchImprovement;
                bool justSwitched = now - lastSwitch < minimumHoldSeconds;
                if (smallImprovement || justSwitched) { Apply(previous); return; }
            }
            if (!hasPlacement || best.direction != current.direction || best.variation != current.variation) lastSwitch = now;
            hasPlacement = true;
            Apply(best);
        }

        private void Apply(Candidate choice)
        {
            if (!choice.sprite) return;
            current = choice;
            bubbleRenderer.sprite = choice.sprite;
            bubbleRenderer.drawMode = SpriteDrawMode.Simple;
            bubbleRenderer.flipX = bubbleRenderer.flipY = false;
            bubbleRenderer.color = Color.white;
            bubbleRenderer.sortingOrder = sortingOrder;
            Vector3 parentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;
            float nativeWidth = Mathf.Max(.001f, choice.sprite.bounds.size.x);
            float scale = choice.size.x / nativeWidth;
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(scale / SafeScale(parentScale.x), scale / SafeScale(parentScale.y), 1f / SafeScale(parentScale.z));
            Vector3 localCenter = choice.sprite.bounds.center;
            Vector3 offset = transform.TransformVector(localCenter);
            transform.position = new Vector3(choice.center.x, choice.center.y, PlaneZ) - offset;
        }

        private static float SafeScale(float value) => Mathf.Abs(value) < .0001f ? .0001f : value;
    }
}
