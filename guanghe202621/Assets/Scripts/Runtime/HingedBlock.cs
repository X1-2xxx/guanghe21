using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(HingeJoint2D), typeof(ManipulableBlock))]
    [AddComponentMenu("Level Kit/Hinged Block")]
    public sealed class HingedBlock : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float motorTorque = 600f;

        private HingeJoint2D hinge;
        private Rigidbody2D body;
        private float targetAngle;

        internal Vector2 PivotPosition => hinge.connectedAnchor;
        internal float Angle => body.rotation;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hinge = GetComponent<HingeJoint2D>();
            Vector2 worldPivot = transform.TransformPoint(hinge.anchor);
            hinge.autoConfigureConnectedAnchor = false;
            hinge.connectedBody = null;
            hinge.connectedAnchor = worldPivot;
        }

        private void OnEnable()
        {
            HoldCurrentAngle();
            hinge.useMotor = true;
        }

        private void OnDisable()
        {
            if (hinge) hinge.useMotor = false;
        }

        internal void SetTargetAngle(float angle) => targetAngle = angle;
        internal void HoldCurrentAngle() => targetAngle = body.rotation;

        private void FixedUpdate()
        {
            var motor = hinge.motor;
            motor.motorSpeed = Mathf.Clamp(Mathf.DeltaAngle(targetAngle, body.rotation) * 12f, -240f, 240f);
            motor.maxMotorTorque = motorTorque;
            hinge.motor = motor;
        }
    }
}
