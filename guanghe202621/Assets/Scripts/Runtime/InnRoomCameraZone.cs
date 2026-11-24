using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    [AddComponentMenu("Level Kit/Inn Room Camera Zone")]
    public sealed class InnRoomCameraZone : MonoBehaviour
    {
        [SerializeField] private CameraTarget cameraTarget;
        private BoxCollider2D area;
        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void Awake() { area = GetComponent<BoxCollider2D>(); area.isTrigger = true; }
        private void OnTriggerEnter2D(Collider2D other) => UpdateCamera(other);
        private void OnTriggerStay2D(Collider2D other) => UpdateCamera(other);
        private void UpdateCamera(Collider2D other)
        {
            var body = other.attachedRigidbody;
            if (!cameraTarget || !body || !body.TryGetComponent<PlayerActionController>(out var player) || !player.isActiveAndEnabled) return;
            var point = body.position + Vector2.up * 0.5f;
            var bounds = area.bounds;
            if (point.x < bounds.min.x || point.x >= bounds.max.x || point.y < bounds.min.y || point.y >= bounds.max.y) return;
            player.EnterRoomCamera(cameraTarget);
        }
    }
}
