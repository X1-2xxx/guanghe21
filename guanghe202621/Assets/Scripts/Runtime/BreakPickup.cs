using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    [AddComponentMenu("Level Kit/Break Pickup")]
    public sealed class BreakPickup : MonoBehaviour
    {
        private bool collected;
        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;

        private void OnTriggerEnter2D(Collider2D other) => TryPickup(other);
        private void OnTriggerStay2D(Collider2D other) => TryPickup(other);

        private void TryPickup(Collider2D other)
        {
            var body = other.attachedRigidbody;
            if (!body || !body.TryGetComponent<PlayerActionController>(out var player) ||
                !player.isActiveAndEnabled || !isActiveAndEnabled || collected) return;

            collected = true;
            player.UnlockBreak();
            GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.ShoesAcquired, player, this);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
