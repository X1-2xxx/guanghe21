using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    [AddComponentMenu("Level Kit/Death Zone")]
    public sealed class DeathZone : MonoBehaviour
    {
        [Header("Optional world-boundary safety net")]
        [SerializeField] private bool guardWorldBounds;
        [SerializeField] private PlayerActionController boundaryPlayer;
        [SerializeField] private Rect worldBounds;
        private Rigidbody2D boundaryBody;

        private void Awake()
        {
            if (guardWorldBounds && boundaryPlayer) boundaryBody = boundaryPlayer.GetComponent<Rigidbody2D>();
        }

        private void LateUpdate()
        {
            if (!guardWorldBounds || !boundaryPlayer || !boundaryPlayer.isActiveAndEnabled ||
                !boundaryBody || !boundaryBody.simulated || boundaryPlayer.IsRespawning ||
                GameStateManager.Current != GameState.Playing) return;
            // Position check catches fast bodies even if physics skips a trigger volume in one step.
            if (!worldBounds.Contains(boundaryBody.position)) Respawn(boundaryPlayer);
        }

        private void Respawn(PlayerActionController player)
        {
            if (!player || !player.isActiveAndEnabled || player.IsRespawning) return;
            GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.PitDeath, player, this);
            player.Die();
        }

        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;

        private void OnTriggerEnter2D(Collider2D other)
        {
            var body = other.attachedRigidbody;
            if (body && body.TryGetComponent<PlayerActionController>(out var player) && player.isActiveAndEnabled)
            {
                Respawn(player);
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
