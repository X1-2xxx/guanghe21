using System.Collections.Generic;
using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D), typeof(BlockBoundary))]
    [AddComponentMenu("Level Kit/Manipulable Block")]
    public sealed class ManipulableBlock : MonoBehaviour
    {
        internal static readonly HashSet<ManipulableBlock> ActiveBlocks = new HashSet<ManipulableBlock>();

        private Rigidbody2D body;
        private Vector2 initialPosition;
        private float initialRotation;
        private float nextImpactSound;
        internal BlockBoundary Boundary { get; private set; }

        public Rigidbody2D Body => body ? body : body = GetComponent<Rigidbody2D>();
        public bool CanBeGrabbed => !TryGetComponent<HotelBounceBlock>(out var bounce) || bounce.Kind != HotelBounceBlock.BlockKind.Bed;

        private void Awake()
        {
            initialPosition = Body.position;
            initialRotation = Body.rotation;
            if (!TryGetComponent<BlockBoundary>(out var boundary))
                boundary = gameObject.AddComponent<BlockBoundary>();
            Boundary = boundary;
            Boundary.SetOrigin(new Vector3(initialPosition.x, initialPosition.y, transform.position.z));
        }

        private void OnEnable() => ActiveBlocks.Add(this);
        private void OnDisable() => ActiveBlocks.Remove(this);

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (GameStateManager.Current != GameState.Playing || Time.timeScale <= 0f ||
                Body.bodyType != RigidbodyType2D.Dynamic || Time.time < nextImpactSound ||
                collision.relativeVelocity.sqrMagnitude < 1.44f) return;
            // Player landing and spring feedback have their own sounds.
            if (collision.collider.GetComponentInParent<PlayerActionController>()) return;
            // A block pair reports the same impact twice; let one body emit it.
            var other = collision.rigidbody ? collision.rigidbody.GetComponent<ManipulableBlock>() : null;
            if (other && other.isActiveAndEnabled && collision.rigidbody.bodyType == RigidbodyType2D.Dynamic &&
                GetEntityId().GetHashCode() > other.GetEntityId().GetHashCode()) return;
            nextImpactSound = Time.time + .2f;
            GameEventCenter.RequestSfx(GameplaySfx.PhysicsBlockImpact);
        }

        internal void RestoreInitialPose()
        {
            var current = Body;
            current.linearVelocity = Vector2.zero;
            current.angularVelocity = 0f;
            current.position = initialPosition;
            current.rotation = initialRotation;
            current.WakeUp();
        }
    }
}
