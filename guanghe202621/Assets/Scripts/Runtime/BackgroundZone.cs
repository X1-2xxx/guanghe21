using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    [AddComponentMenu("Level Kit/Background Zone")]
    public sealed class BackgroundZone : MonoBehaviour
    {
        [SerializeField] private CameraBackgroundController controller;
        [SerializeField] private SpriteRenderer background;
        [Tooltip("Higher values win in overlapping zones. Equal priority uses the most recently entered zone.")]
        [SerializeField] private int priority;
        [Tooltip("Extra distance allowed when leaving, to reduce flicker along the edge.")]
        [SerializeField, Min(0f)] private float exitPadding = 0.1f;

        private BoxCollider2D area;
        private CameraBackgroundController registeredController;
        private bool wasInside;

        internal SpriteRenderer Background => background;
        internal int Priority => priority;

        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void Awake()
        {
            area = GetComponent<BoxCollider2D>();
            area.isTrigger = true;
        }

        private void Start()
        {
            if (!controller && Camera.main && Camera.main.gameObject.scene == gameObject.scene)
                controller = Camera.main.GetComponent<CameraBackgroundController>();
            if (controller && !controller.Manages(background))
                Debug.LogWarning("Background Zone: assign a background listed in the camera's Backgrounds array.", this);
        }

        private void FixedUpdate()
        {
            if (registeredController != controller)
            {
                if (registeredController) registeredController.SetZoneActive(this, false);
                registeredController = controller;
                wasInside = false;
            }
            bool inside = false;
            var player = controller ? controller.Player : null;
            var hitbox = controller ? controller.PlayerCollider : null;
            if (controller && controller.isActiveAndEnabled && controller.gameObject.scene == gameObject.scene &&
                area && area.isActiveAndEnabled && player && player.isActiveAndEnabled &&
                hitbox && hitbox.isActiveAndEnabled && hitbox.attachedRigidbody && hitbox.attachedRigidbody.simulated)
            {
                // Check actual overlap rather than relying on Exit callbacks:
                // teleporting to a campfire or disabling a collider also releases the zone.
                var separation = area.Distance(hitbox);
                inside = separation.isValid && separation.distance <= (wasInside ? exitPadding : 0f);
            }
            wasInside = inside;
            if (controller) controller.SetZoneActive(this, inside);
        }

        private void OnDisable()
        {
            if (registeredController) registeredController.SetZoneActive(this, false);
            registeredController = null;
            wasInside = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (!TryGetComponent<BoxCollider2D>(out var box)) return;
            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.35f, 0.65f, 1f, 0.15f);
            Gizmos.DrawCube(box.offset, box.size);
            Gizmos.color = new Color(0.35f, 0.65f, 1f, 1f);
            Gizmos.DrawWireCube(box.offset, box.size);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
