using System.Collections.Generic;
using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, AddComponentMenu("Level Kit/Campfire")]
    public sealed class Campfire : MonoBehaviour
    {
        internal static readonly HashSet<Campfire> InScene = new HashSet<Campfire>();

        [SerializeField, HideInInspector] private SpriteRenderer marker;
        [SerializeField] private SpriteRenderer wood;
        [SerializeField] private Transform flame;
        [SerializeField] private CameraTarget cameraTarget;
        [SerializeField, Min(0.05f), Tooltip("水平中心对齐时允许的脚部高度误差；不再用身体边缘接触来触发停步。")] private float reachRadius = 0.4f;
        [Header("Flame Animation")]
        [SerializeField, Min(0f)] private float igniteSeconds = 0.25f;
        [SerializeField, Min(0f)] private float extinguishSeconds = 0.2f;
        [SerializeField, Range(1f, 1.25f)] private float igniteOvershoot = 1.1f;

        private bool isCurrent;
        private bool initialized;
        private bool animating;
        private Vector3 flameRestScale;
        private float flameScale;
        private float startScale;
        private float elapsed;

        internal bool IsCurrent => isCurrent;
        internal float ReachRadius => reachRadius;
        internal Vector2 Position => transform.position;
        internal CameraTarget CameraTarget => cameraTarget;

        /// <summary>Pure center-line arrival check; leaves all movement and gravity to the player controller.</summary>
        public static bool HasReachedCenter(Vector2 fromFeet, Vector2 toFeet, Vector2 marker, float verticalTolerance, out float progress)
        {
            const float centerTolerance = .025f;
            progress = 0f;
            float dx = toFeet.x - fromFeet.x;
            bool crossed = (fromFeet.x < marker.x && toFeet.x >= marker.x) || (fromFeet.x > marker.x && toFeet.x <= marker.x);
            if (crossed && Mathf.Abs(dx) > .000001f) progress = Mathf.Clamp01((marker.x - fromFeet.x) / dx);
            else
            {
                if (Mathf.Abs(toFeet.x - marker.x) > centerTolerance) return false;
                progress = 1f;
            }
            float feetY = Mathf.Lerp(fromFeet.y, toFeet.y, progress);
            return Mathf.Abs(feetY - marker.y) <= Mathf.Max(.05f, verticalTolerance);
        }

        private void Awake()
        {
            if (!wood && transform.Find("Wood")) wood = transform.Find("Wood").GetComponent<SpriteRenderer>();
            if (!flame) flame = transform.Find("Flame");
            if (marker && wood && marker != wood) marker.enabled = false;
            if (flame) flameRestScale = flame.localScale;
            initialized = true;
        }

        private void OnEnable()
        {
            InScene.Add(this);
            animating = false;
            ApplyFlameScale(isCurrent ? 1f : 0f);
        }

        private void OnDisable()
        {
            InScene.Remove(this);
            animating = false;
        }

        internal void SetCurrent(bool current)
        {
            if (isCurrent == current) return;
            isCurrent = current;
            // Player Awake can select its initial checkpoint before this Awake runs.
            if (!initialized || !flame) return;
            startScale = flameScale;
            elapsed = 0f;
            animating = true;
            flame.gameObject.SetActive(true);
            if ((current ? igniteSeconds : extinguishSeconds) <= 0f)
            {
                animating = false;
                ApplyFlameScale(current ? 1f : 0f);
            }
        }

        private void Update() => AdvanceFlame(Time.deltaTime);

        private void AdvanceFlame(float deltaTime)
        {
            if (!animating || !flame) return;
            elapsed += Mathf.Max(0f, deltaTime);
            float duration = isCurrent ? igniteSeconds : extinguishSeconds;
            float progress = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            float scale;
            if (!isCurrent)
                scale = Mathf.SmoothStep(startScale, 0f, progress);
            else if (progress < 0.75f)
                scale = Mathf.SmoothStep(startScale, igniteOvershoot, progress / 0.75f);
            else
                scale = Mathf.SmoothStep(igniteOvershoot, 1f, (progress - 0.75f) / 0.25f);
            ApplyFlameScale(scale);
            if (progress >= 1f)
            {
                animating = false;
                ApplyFlameScale(isCurrent ? 1f : 0f);
            }
        }

        private void ApplyFlameScale(float scale)
        {
            flameScale = scale;
            if (!flame) return;
            flame.localScale = new Vector3(flameRestScale.x * scale, flameRestScale.y * scale, flameRestScale.z);
            flame.gameObject.SetActive(scale > 0f || animating);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, new Vector3(.05f, reachRadius * 2f, 0f));
        }
    }
}
