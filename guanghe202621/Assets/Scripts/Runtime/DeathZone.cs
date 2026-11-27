using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    [AddComponentMenu("Level Kit/Death Zone")]
    public sealed class DeathZone : MonoBehaviour
    {
        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;

        private void OnTriggerEnter2D(Collider2D other)
        {
            var body = other.attachedRigidbody;
            if (body && body.TryGetComponent<PlayerActionController>(out var player) && player.isActiveAndEnabled)
            {
                GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.PitDeath, player, this);
                player.Die();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!TryGetComponent<BoxCollider2D>(out var area)) return;
            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.2f, 0.18f, 0.18f);
            Gizmos.DrawCube(area.offset, area.size);
            Gizmos.color = new Color(1f, 0.2f, 0.18f);
            Gizmos.DrawWireCube(area.offset, area.size);
            Gizmos.color = previousColor;
            Gizmos.matrix = previousMatrix;
        }
    }
}
