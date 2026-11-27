using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LevelKit
{
    [DisallowMultipleComponent, AddComponentMenu("Level Kit/Block Manipulation")]
    public sealed class BlockManipulation : MonoBehaviour
    {
        [SerializeField] private Camera levelCamera = null;
        [SerializeField, Min(1f)] private float grabForce = 1800f;
        [SerializeField, Min(0.1f)] private float grabFrequency = 5f;
        [SerializeField, Range(1f, 90f)] private float rotationStep = 10f;
        [SerializeField] private Sprite tapeSprite;
        [SerializeField, Min(0.05f), Tooltip("Width of the tape artwork in world units. Its original aspect ratio is preserved.")]
        private float tapeWidth = 0.5f;
        [SerializeField, Min(0f)] private float tapeStiffness = 400f;
        [SerializeField, Min(0f)] private float tapeDamping = 32f;
        [SerializeField] private PlayerActionController playerAction;
        [SerializeField] private GameObject actionWarning;
        [SerializeField, Min(0.1f)] private float warningSeconds = 1.25f;

        private readonly List<Collider2D> hits = new List<Collider2D>();
        private readonly List<RaycastHit2D> standingHits = new List<RaycastHit2D>();
        private readonly List<BalancePlatform> placementPlatforms = new List<BalancePlatform>();
        private readonly ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        private BlockGlue glue;
        private TargetJoint2D joint;
        private Rigidbody2D held;
        private HingedBlock heldPivot;
        private Vector2 heldCursor;
        private Vector2 lastPivotPointer;
        private float pivotTargetAngle;
        private bool turning;
        private float targetRotation;
        private float warningUntil;

        internal bool IsHolding(Rigidbody2D body) => held == body;
        internal bool HasWorldTapeConnection(Rigidbody2D body) => glue != null && glue.IsLinkedToStaticGround(body);

        private bool ControlsLocked => LevelGameFlow.BlocksGameplayInput(this) || GamePresentation.PresentationEvents.BlocksInput || GamePresentation.PresentationEvents.ResetOnly ||
            (playerAction && (playerAction.IsActing || playerAction.IsInConversation));

        private void Awake()
        {
            glue = new BlockGlue(transform, gameObject.scene.GetPhysicsScene2D(), tapeSprite, tapeWidth);
            HideWarning();
        }

        private void Start()
        {
            if (playerAction) return;
            PlayerActionController found = null;
            foreach (var candidate in FindObjectsByType<PlayerActionController>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene != gameObject.scene) continue;
                if (found) return;
                found = candidate;
            }
            playerAction = found;
        }

        private void Update()
        {
            if (LevelGameFlow.BlocksGameplayInput(this) || LevelGameFlow.PointerOverUi(this))
            {
                Release();
                HideWarning();
                return;
            }
            if (playerAction && playerAction.ConversationBlocksInput)
            {
                Release();
                HideWarning();
                return;
            }
            bool locked = ControlsLocked;
            if (actionWarning && actionWarning.activeSelf && (!locked || Time.unscaledTime >= warningUntil))
                HideWarning();
            if (locked && (held || joint)) Release();
            if (held && (!held.GetComponent<ManipulableBlock>().CanBeGrabbed || IsSupportingPlayer(held))) Release();

            var mouse = Mouse.current;
            var view = levelCamera ? levelCamera : Camera.main;
            var keyboard = Keyboard.current;
            if (mouse == null || !view || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
            {
                Release();
                return;
            }

            var plane = new Plane(Vector3.forward, Vector3.zero);
            var ray = view.ScreenPointToRay(mouse.position.ReadValue());
            if (!plane.Raycast(ray, out float distance))
            {
                if (!mouse.leftButton.isPressed) Release();
                return;
            }
            Vector2 cursor = ray.GetPoint(distance);
            bool leftClick = mouse.leftButton.wasPressedThisFrame && (!playerAction || !playerAction.ConsumedLeftClick);
            bool rightClick = mouse.rightButton.wasPressedThisFrame && (!playerAction || !playerAction.ConsumedRightClick);
            float scroll = mouse.scroll.ReadValue().y;
            bool rotated = Mathf.Abs(scroll) > 0.01f;

            if (locked)
            {
                if ((rightClick && glue.HasTapeAt(cursor)) ||
                    ((leftClick || rightClick || rotated) && IsOverBlock(cursor)))
                {
                    ShowWarning();
                    if (playerAction && playerAction.IsActing && !playerAction.IsRespawning && !playerAction.IsWallStopping)
                        GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.RunningEditDenied, playerAction);
                }
                return;
            }

            if (leftClick)
            {
                gameObject.scene.GetPhysicsScene2D().OverlapPoint(cursor, filter, hits);
                foreach (var hit in hits)
                {
                    var block = GetBlock(hit);
                    if (block && TryGrab(block, cursor)) break;
                }
            }

            if (!held || (!joint && !heldPivot))
            {
                if (rightClick) glue.RemoveNearest(cursor);
                return;
            }

            heldCursor = cursor;
            if (heldPivot)
            {
                UpdatePivot(cursor, scroll);
            }
            else
            {
                joint.target = cursor;
                if (rotated)
                {
                    if (!turning) targetRotation = held.rotation;
                    targetRotation += Mathf.Sign(scroll) * rotationStep;
                    turning = true;
                }
            }

            if (rightClick)
            {
                glue.UpdateCandidate(held, cursor, 0f);
                if (glue.ConnectCandidate(requireSteady: false) && glue.IsLinkedToBalancePlatform(held))
                    Release();
            }

            if (!held || mouse.leftButton.isPressed) return;
            Release();
        }

        private void FixedUpdate()
        {
            if (ControlsLocked && (held || joint)) Release();
            if (held && IsSupportingPlayer(held)) Release();
            CheckBoundary();
            if (held) BeginPlacement();
            glue.ApplyTapeResistance(tapeStiffness, tapeDamping, Time.fixedDeltaTime);
            if (ControlsLocked || !held) return;
            if (!joint && !heldPivot)
            {
                Release();
                return;
            }
            if (turning)
            {
                float error = Mathf.DeltaAngle(held.rotation, targetRotation) * Mathf.Deg2Rad;
                float spin = held.angularVelocity * Mathf.Deg2Rad;
                held.AddTorque(Mathf.Clamp((90f * error - 16f * spin) * held.inertia, -400f, 400f));
            }
            glue.UpdateCandidate(held, heldCursor, Time.fixedDeltaTime);
        }

        private void UpdatePivot(Vector2 cursor, float scroll)
        {
            Vector2 direction = cursor - heldPivot.PivotPosition;
            const float deadZoneSquared = 0.01f;
            if (direction.sqrMagnitude < deadZoneSquared) direction = Vector2.zero;
            if (direction != Vector2.zero && lastPivotPointer.sqrMagnitude >= deadZoneSquared)
                pivotTargetAngle += Vector2.SignedAngle(lastPivotPointer, direction);
            lastPivotPointer = direction;
            if (Mathf.Abs(scroll) > 0.01f) pivotTargetAngle += Mathf.Sign(scroll) * rotationStep;
            heldPivot.SetTargetAngle(pivotTargetAngle);
        }

        private void BeginPlacement()
        {
            foreach (var platform in BalancePlatform.ActivePlatforms)
            {
                if (!platform || platform.gameObject.scene != gameObject.scene || placementPlatforms.Contains(platform)) continue;
                platform.BeginPlacement(this);
                placementPlatforms.Add(platform);
            }
        }

        private void CheckBoundary()
        {
            var scene = gameObject.scene;
            foreach (var block in ManipulableBlock.ActiveBlocks)
            {
                if (!block || block.gameObject.scene != scene) continue;
                var boundary = block.Boundary;
                if (!boundary || !boundary.isActiveAndEnabled) continue;
                var body = block.Body;
                if (!boundary.ShouldReturn(body, held)) continue;
                if (body == held) Release();
                glue.RemoveLinks(body);
                block.RestoreInitialPose();
                if (body.TryGetComponent<HingedBlock>(out var hinge)) hinge.HoldCurrentAngle();
            }
        }

        private void LateUpdate() => glue.UpdateMarks();

        private bool IsOverBlock(Vector2 point)
        {
            gameObject.scene.GetPhysicsScene2D().OverlapPoint(point, filter, hits);
            foreach (var hit in hits)
            {
                var block = GetBlock(hit);
                if (!block || !block.CanBeGrabbed || !block.isActiveAndEnabled) continue;
                var body = block.Body;
                if (body && body.simulated && body.bodyType == RigidbodyType2D.Dynamic) return true;
            }
            return false;
        }

        private static ManipulableBlock GetBlock(Collider2D collider) =>
            collider.attachedRigidbody ? collider.attachedRigidbody.GetComponent<ManipulableBlock>() : null;

        private void ShowWarning()
        {
            if (!actionWarning) return;
            warningUntil = Time.unscaledTime + warningSeconds;
            actionWarning.SetActive(true);
        }

        private void HideWarning()
        {
            warningUntil = 0f;
            if (actionWarning && actionWarning.activeSelf) actionWarning.SetActive(false);
        }

        private bool TryGrab(ManipulableBlock block, Vector2 point)
        {
            var body = block.Body;
            if (ControlsLocked || !block.CanBeGrabbed || !block.isActiveAndEnabled || !body || !body.simulated || body.bodyType != RigidbodyType2D.Dynamic)
                return false;
            if (IsSupportingPlayer(body))
            {
                GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.UnderfootDenied, playerAction, body);
                return false;
            }
            if (body.TryGetComponent<HingedBlock>(out var pivot) && !pivot.isActiveAndEnabled) return false;
            Release();
            held = body;
            GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.BlockGrabbed, playerAction, body);
            heldCursor = point;
            heldPivot = pivot;
            BeginPlacement();
            if (heldPivot)
            {
                lastPivotPointer = point - heldPivot.PivotPosition;
                pivotTargetAngle = heldPivot.Angle;
                heldPivot.SetTargetAngle(pivotTargetAngle);
                body.WakeUp();
                return true;
            }
            joint = body.gameObject.AddComponent<TargetJoint2D>();
            joint.autoConfigureTarget = false;
            joint.anchor = body.transform.InverseTransformPoint(point);
            joint.target = point;
            joint.maxForce = grabForce;
            joint.frequency = grabFrequency;
            joint.dampingRatio = 0.95f;
            body.WakeUp();
            return true;
        }

        private bool IsSupportingPlayer(Rigidbody2D block)
        {
            if (!playerAction || !block) return false;
            var capsule = playerAction.GetComponent<CapsuleCollider2D>();
            var rider = capsule ? capsule.attachedRigidbody : null;
            if (!capsule || !capsule.isActiveAndEnabled || capsule.isTrigger || !rider || !rider.simulated) return false;

            const float probe = 0.08f;
            var solids = new ContactFilter2D { useTriggers = false };
            solids.SetLayerMask(Physics2D.GetLayerCollisionMask(rider.gameObject.layer));
            capsule.Cast(rider.position, rider.rotation, Vector2.down, solids, standingHits, probe);
            var bounds = capsule.bounds;
            float footTop = bounds.min.y + Mathf.Min(bounds.extents.x, bounds.extents.y) + probe;
            float nearest = float.PositiveInfinity;
            float onBlock = float.PositiveInfinity;
            foreach (var hit in standingHits)
            {
                if (!hit.collider || hit.normal.y <= 0.15f || hit.point.y > footTop) continue;
                Vector2 supportVelocity = hit.rigidbody ? hit.rigidbody.GetPointVelocity(hit.point) : Vector2.zero;
                float normalSpeed = Vector2.Dot(rider.GetPointVelocity(hit.point) - supportVelocity, hit.normal);
                // A nearby surface is not supporting a character that is jumping or falling past it.
                if (Mathf.Abs(normalSpeed) > 0.5f) continue;
                nearest = Mathf.Min(nearest, hit.distance);
                if (hit.rigidbody == block) onBlock = Mathf.Min(onBlock, hit.distance);
            }
            // Include both feet across a seam, but not a block underneath another supporting platform.
            return !float.IsPositiveInfinity(onBlock) && onBlock <= nearest + 0.01f;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) Release();
        }

        private void OnDisable()
        {
            Release();
            HideWarning();
        }

        private void OnDestroy() => glue?.Dispose();

        private void Release()
        {
            if (held && glue.IsLinkedToBalancePlatform(held)) glue.StopGroupMotion(held);
            if (heldPivot) heldPivot.HoldCurrentAngle();
            if (joint)
            {
                joint.enabled = false;
                if (Application.isPlaying) Destroy(joint);
                else DestroyImmediate(joint);
            }
            joint = null;
            held = null;
            heldPivot = null;
            turning = false;
            glue?.ClearCandidate();
            foreach (var platform in placementPlatforms)
                if (platform) platform.EndPlacement(this);
            placementPlatforms.Clear();
        }
    }
}
