using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    [AddComponentMenu("Level Kit/Jump Pickup")]
    public sealed class JumpPickup : MonoBehaviour
    {
        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;

        private void OnTriggerEnter2D(Collider2D other)
        {
            var body = other.attachedRigidbody;
            if (!body || !body.TryGetComponent<PlayerActionController>(out var player) ||
                !player.isActiveAndEnabled || !isActiveAndEnabled) return;

            player.UnlockJump();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
