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
