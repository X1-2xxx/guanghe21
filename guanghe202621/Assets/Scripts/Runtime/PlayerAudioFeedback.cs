using UnityEngine;

namespace LevelKit
{
    /// <summary>Uses travelled distance for footsteps and airborne-to-ground transitions for landings.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerActionController))]
    public sealed class PlayerAudioFeedback : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float strideDistance = .65f;
        [SerializeField, Min(.05f)] private float minimumStepInterval = .14f;
        private PlayerActionController player;
        private Rigidbody2D body;
        private Vector2 previous;
        private bool sampled, wasGrounded;
        private float distance, lastStep = -10f, airborneSeconds, fallSpeed;

        private void Awake() { player = GetComponent<PlayerActionController>(); body = GetComponent<Rigidbody2D>(); }
        private void OnEnable() => sampled = false;

        private void FixedUpdate()
        {
            var position = body.position;
            bool grounded = player.AudioGrounded;
            if (!sampled || !player.isActiveAndEnabled || !body.simulated || player.IsRespawning ||
                GameStateManager.Current != GameState.Playing || player.IsInConversation)
            {
                ResetSample(position, grounded);
                return;
            }
            Vector2 delta = position - previous;
            previous = position;
            // Respawn / editor repositioning must not produce a landing or a burst of steps.
            if (delta.sqrMagnitude > 4f) { ResetSample(position, grounded); return; }
            if (!grounded)
            {
                airborneSeconds += Time.fixedDeltaTime;
                fallSpeed = Mathf.Max(fallSpeed, -delta.y / Time.fixedDeltaTime, -body.linearVelocityY);
                distance = 0f;
            }
            else
            {
                if (!wasGrounded && airborneSeconds >= .1f && fallSpeed >= .5f)
                    GameEventCenter.RequestSfx(GameplaySfx.JumpLanding);
                airborneSeconds = fallSpeed = 0f;
                if (Mathf.Abs(delta.x) > .001f && !player.IsJumping && !player.IsWallStopping)
                {
                    distance += Mathf.Abs(delta.x);
                    if (distance >= strideDistance && Time.time - lastStep >= minimumStepInterval)
                    {
                        distance %= strideDistance; lastStep = Time.time;
                        // A dedicated dash cue can be supplied later; ordinary steps remain until then.
                        GameEventCenter.RequestSfx(player.BreakEnabled ? GameplaySfx.Dash : GameplaySfx.Footstep);
                    }
                }
                else distance = 0f;
            }
            wasGrounded = grounded;
        }

        private void ResetSample(Vector2 position, bool grounded)
        {
            previous = position; wasGrounded = grounded; sampled = true;
            distance = airborneSeconds = fallSpeed = 0f;
        }
    }
}
