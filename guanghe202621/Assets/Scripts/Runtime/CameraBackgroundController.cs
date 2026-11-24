using System.Collections.Generic;
using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    [AddComponentMenu("Level Kit/Camera Background Controller")]
    public sealed class CameraBackgroundController : MonoBehaviour
    {
        [SerializeField] private PlayerActionController player;
        [Tooltip("Camera child sprites. Leave empty to use all child Sprite Renderers.")]
        [SerializeField] private SpriteRenderer[] backgrounds;
        [SerializeField] private SpriteRenderer defaultBackground;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.7f;
        [Tooltip("Backgrounds use consecutive orders in the default background's Sorting Layer.")]
        [SerializeField] private int firstSortingOrder = -200;

        private readonly List<BackgroundZone> zones = new List<BackgroundZone>();
        private SpriteRenderer[] layers;
        private Color[] colors;
        private float[] weights;
        private float[] startWeights;
        private Collider2D playerCollider;
        private int defaultIndex;
        private int targetIndex = -1;
        private float elapsed;
        private bool fading;

        internal PlayerActionController Player => player;
        internal Collider2D PlayerCollider => playerCollider;

        private void Awake()
        {
            var candidates = backgrounds != null && backgrounds.Length > 0
                ? backgrounds : GetComponentsInChildren<SpriteRenderer>(true);
            var valid = new List<SpriteRenderer>();
            foreach (var candidate in candidates)
            {
                if (!candidate || candidate.transform == transform ||
                    !candidate.transform.IsChildOf(transform) || valid.Contains(candidate)) continue;
                valid.Add(candidate);
            }
            layers = valid.ToArray();
            colors = new Color[layers.Length];
            weights = new float[layers.Length];
            startWeights = new float[layers.Length];
            defaultIndex = Mathf.Max(0, FindLayer(defaultBackground));
            int sortingLayer = layers.Length > 0 ? layers[defaultIndex].sortingLayerID : 0;
            for (int i = 0; i < layers.Length; i++)
            {
                colors[i] = layers[i].color;
                layers[i].sortingLayerID = sortingLayer;
                layers[i].sortingOrder = firstSortingOrder + i;
                layers[i].gameObject.SetActive(true);
            }
        }

        private void OnEnable() => ShowDefaultImmediately();

        private void Start() => ResolvePlayer();

        private void ResolvePlayer()
        {
            if (!player)
            {
                PlayerActionController found = null;
                foreach (var candidate in FindObjectsByType<PlayerActionController>(FindObjectsSortMode.None))
                {
                    if (candidate.gameObject.scene != gameObject.scene) continue;
                    if (found) return;
                    found = candidate;
                }
                player = found;
            }
            playerCollider = player ? player.GetComponent<CapsuleCollider2D>() : null;
        }

        private void LateUpdate()
        {
            if (!player || !playerCollider) ResolvePlayer();
            RefreshSelection();
            AdvanceBlend(Time.deltaTime);
        }

        private void OnDisable()
        {
            zones.Clear();
            ShowDefaultImmediately();
        }

        internal bool Manages(SpriteRenderer background) => FindLayer(background) >= 0;

        internal void SetZoneActive(BackgroundZone zone, bool inside)
        {
            if (!zone) return;
            if (inside && isActiveAndEnabled && Manages(zone.Background))
            {
                if (zones.Contains(zone)) return;
                zones.Add(zone);
            }
            else if (!zones.Remove(zone)) return;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (layers == null || layers.Length == 0) return;
            for (int i = zones.Count - 1; i >= 0; i--)
                if (!zones[i] || !zones[i].isActiveAndEnabled || !Manages(zones[i].Background)) zones.RemoveAt(i);

            BackgroundZone selected = null;
            foreach (var zone in zones)
                if (!selected || zone.Priority >= selected.Priority) selected = zone;
            int next = selected ? FindLayer(selected.Background) : defaultIndex;
            if (next == targetIndex) return;
            targetIndex = next;
            elapsed = 0f;
            for (int i = 0; i < weights.Length; i++) startWeights[i] = weights[i];
            fading = true;
            if (fadeSeconds <= 0f) AdvanceBlend(0f);
        }

        private void ShowDefaultImmediately()
        {
            if (layers == null || layers.Length == 0) return;
            targetIndex = defaultIndex;
            fading = false;
            for (int i = 0; i < weights.Length; i++) weights[i] = i == defaultIndex ? 1f : 0f;
            ApplyBlend();
        }

        private void AdvanceBlend(float deltaTime)
        {
            if (!fading) return;
            elapsed += Mathf.Max(0f, deltaTime);
            float progress = fadeSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            for (int i = 0; i < weights.Length; i++)
                weights[i] = Mathf.Lerp(startWeights[i], i == targetIndex ? 1f : 0f, eased);
            ApplyBlend();
            if (progress >= 1f) fading = false;
        }

        private void ApplyBlend()
        {
            // Convert blend weights to stacked sprite opacity. Opaque background
            // images stay opaque as a group, avoiding a dark dip halfway through.
            float remaining = 1f;
            for (int i = layers.Length - 1; i >= 0; i--)
            {
                float opacity = remaining > 0.00001f ? Mathf.Clamp01(weights[i] / remaining) : 0f;
                remaining = Mathf.Max(0f, remaining - weights[i]);
                if (!layers[i]) continue;
                var color = colors[i];
                color.a *= opacity;
                layers[i].color = color;
                layers[i].enabled = weights[i] > 0f;
            }
        }

        private int FindLayer(SpriteRenderer background)
        {
            if (!background || layers == null) return -1;
            for (int i = 0; i < layers.Length; i++)
                if (layers[i] == background) return i;
            return -1;
        }
    }
}
