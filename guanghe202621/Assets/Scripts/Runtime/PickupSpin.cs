using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Level Kit/Pickup Spin")]
    public sealed class PickupSpin : MonoBehaviour
    {
        [SerializeField, Tooltip("只指定美术子物体，保留根物体上的拾取碰撞。")]
        private Transform visual;
        [SerializeField, Tooltip("每秒绕本地Y轴旋转的角度；负数反向，0停止。")]
        private float degreesPerSecond = 90f;
        [SerializeField, Tooltip("启用时的旋转角度。")]
        private float startAngle;

        private Quaternion baseRotation;
        private float angle;
        private bool initialized;

        public Transform Visual => visual;
        public float DegreesPerSecond { get => degreesPerSecond; set => degreesPerSecond = value; }

        private void OnEnable()
        {
            if (!visual) visual = CreateVisual();
            if (!visual || visual == transform || !visual.IsChildOf(transform)) return;
            if (!initialized) { baseRotation = visual.localRotation; initialized = true; }
            angle = startAngle;
            ApplyRotation();
        }

        private void Update() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            if (!initialized || !visual || deltaTime <= 0f) return;
            angle = Mathf.Repeat(angle + degreesPerSecond * deltaTime, 360f);
            ApplyRotation();
        }

        private void ApplyRotation() => visual.localRotation = baseRotation * Quaternion.AngleAxis(angle, Vector3.up);

        private void OnDisable()
        {
            if (initialized && visual) visual.localRotation = baseRotation;
        }

        private Transform CreateVisual()
        {
            var source = GetComponent<SpriteRenderer>();
            if (!source || !source.enabled) return null;
            var child = new GameObject("SpinVisual");
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);
            CopyRenderer(source, child.AddComponent<SpriteRenderer>());
            source.enabled = false;
            return child.transform;
        }

        public static void CopyRenderer(SpriteRenderer source, SpriteRenderer destination)
        {
            destination.sprite = source.sprite;
            destination.color = source.color;
            destination.flipX = source.flipX;
            destination.flipY = source.flipY;
            destination.drawMode = source.drawMode;
            destination.size = source.size;
            destination.tileMode = source.tileMode;
            destination.maskInteraction = source.maskInteraction;
            destination.sortingLayerID = source.sortingLayerID;
            destination.sortingOrder = source.sortingOrder;
            destination.sharedMaterials = source.sharedMaterials;
            destination.renderingLayerMask = source.renderingLayerMask;
            destination.shadowCastingMode = source.shadowCastingMode;
            destination.receiveShadows = source.receiveShadows;
            destination.lightProbeUsage = source.lightProbeUsage;
            destination.reflectionProbeUsage = source.reflectionProbeUsage;
            destination.enabled = source.enabled;
        }
    }
}
