using System.Collections.Generic;
using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D), typeof(HingeJoint2D))]
    [AddComponentMenu("Level Kit/Balance Platform")]
    public sealed class BalancePlatform : MonoBehaviour
    {
        internal static readonly HashSet<BalancePlatform> ActivePlatforms = new HashSet<BalancePlatform>();

        [SerializeField, Range(1f, 85f)] private float maxTiltAngle = 35f;
        private readonly HashSet<BlockManipulation> handlers = new HashSet<BlockManipulation>();
        private Rigidbody2D body;
        private RigidbodyConstraints2D freeConstraints;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            var hinge = GetComponent<HingeJoint2D>();
            body.centerOfMass = Vector2.zero;
            hinge.anchor = Vector2.zero;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.connectedBody = null;
            hinge.connectedAnchor = body.position;
            ConfigureLimits();
        }

        private void OnEnable() => ActivePlatforms.Add(this);

        private void OnDisable()
        {
            ActivePlatforms.Remove(this);
            if (handlers.Count > 0)
            {
                body.constraints = freeConstraints;
                body.angularVelocity = 0f;
                handlers.Clear();
            }
        }

        internal void BeginPlacement(BlockManipulation handler)
        {
            if (!handlers.Add(handler) || handlers.Count != 1) return;
            freeConstraints = body.constraints;
            body.angularVelocity = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        internal void EndPlacement(BlockManipulation handler)
        {
            if (!handlers.Remove(handler) || handlers.Count > 0) return;
            body.constraints = freeConstraints;
            body.angularVelocity = 0f;
            body.WakeUp();
        }

        private void OnValidate() => ConfigureLimits();

        private void ConfigureLimits()
        {
            if (!TryGetComponent<HingeJoint2D>(out var hinge)) return;
            hinge.useMotor = false;
            hinge.useLimits = true;
            hinge.limits = new JointAngleLimits2D
            {
                min = -maxTiltAngle,
                max = maxTiltAngle
            };
        }

        private void OnDrawGizmosSelected()
        {
            var original = Gizmos.color;
            Gizmos.color = Color.yellow;
            var center = Application.isPlaying && TryGetComponent<HingeJoint2D>(out var hinge)
                ? (Vector3)hinge.connectedAnchor : transform.position;
            Gizmos.DrawWireSphere(center, 0.16f);
            Gizmos.color = original;
        }
    }
}
