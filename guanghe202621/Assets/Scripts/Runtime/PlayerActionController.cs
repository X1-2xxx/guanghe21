using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using Spine.Unity;

namespace LevelKit
{
    [DefaultExecutionOrder(-20), DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(SpriteRenderer))]
    [AddComponentMenu("Level Kit/Player Action Controller")]
    public sealed class PlayerActionController : MonoBehaviour
    {
        public enum WallStopReason { ObstacleTooHigh, SlopeTooSteep, ClearanceBlocked }
        private enum Reaction { None, WallStop, Respawn }

        [SerializeField] private Camera levelCamera;
        [SerializeField] private GameObject dialogueBox;
        [SerializeField] private Campfire initialCampfire;
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float dialogueSeconds = 1.2f;
        [SerializeField, Range(0.15f, 0.6f)] private float doubleClickSeconds = 0.28f;
        [Header("Character Visual")]
        [SerializeField] private SkeletonRenderer spineVisual;
        [SerializeField] private Animator animator;
        [Tooltip("Whether the unflipped character artwork faces right.")]
        [SerializeField] private bool spriteFacesRight = true;
        [Header("Traversal")]
        [SerializeField, Min(0f)] private float stepHeight = 0.4f;
        [SerializeField, Range(1f, 80f)] private float maxSlopeAngle = 30f;
        [SerializeField, Min(0f)] private float jumpHeight = 0.8f;
        [Header("Wall Reaction")]
        [SerializeField, Min(0f)] private float wallBounceDistance = 0.3f;
        [SerializeField, Min(0.02f)] private float wallBounceSeconds = 0.18f;
        [SerializeField] private UnityEvent<WallStopReason> onWallStopped = new UnityEvent<WallStopReason>();
        private bool jumpEnabled;
        private bool breakEnabled;

        private static readonly int MovingParameter = Animator.StringToHash("isMoving");
        private static readonly int JumpingParameter = Animator.StringToHash("isJumping");
        private static readonly int RespawnParameter = Animator.StringToHash("respawn");
        private static readonly int WallStopParameter = Animator.StringToHash("wallStop");
        private const float SpawnLift = 0.02f;

        private Rigidbody2D body;
        private CapsuleCollider2D hitbox;
        private PlayerTraversal traversal;
        private SpriteRenderer visual;
        private PlayerSpeechBubble speechBubble;
        private bool hasMovingParameter;
        private bool hasJumpingParameter;
        private bool hasRespawnParameter;
        private bool hasWallStopParameter;
        private Reaction reaction;
        private float reactionRemaining;
        private float bounceElapsed;
        private float bounceStartX;
        private int bounceDirection;
        private Campfire checkpoint;
        private Vector2 returnPosition;
        private Vector2 lastPosition;
        private readonly Dictionary<CameraTarget, bool> cameraSides = new Dictionary<CameraTarget, bool>();
        private readonly List<(double time, Vector2 position)> cameraPositionHistory = new List<(double, Vector2)>();
        private Transform movingCamera;
        private CameraTarget activeCameraTarget;
        private Transform activeCameraCenter;
        private Vector3 cameraStart;
        private Vector3 cameraDestination;
        private float cameraDuration;
        private float cameraElapsed;
        private int facing = 1;
        private int walkDirection;
        private int leftClickFrame = -1;
        private int rightClickFrame = -1;
        private double lastPlayerClick = double.NegativeInfinity;
        private double dialogueDeadline = double.PositiveInfinity;

        public bool IsActing { get; private set; }
        public bool JumpEnabled => jumpEnabled;
        public bool BreakEnabled => breakEnabled;
        public bool IsInConversation { get; private set; }
        public bool IsJumping => traversal != null && traversal.IsJumping;
        public bool IsRespawning => reaction == Reaction.Respawn;
        public bool IsWallStopping => reaction == Reaction.WallStop;
        public WallStopReason LastWallStopReason { get; private set; }
        public UnityEvent<WallStopReason> OnWallStopped => onWallStopped;
        internal int WalkDirection => walkDirection;
        internal bool IsGameplayCameraMoving => movingCamera != null;
        private int conversationInputFrame = -1;
        internal bool ConversationBlocksInput => IsInConversation || conversationInputFrame == Time.frameCount;
        internal void UnlockJump() => jumpEnabled = true;
        internal void UnlockBreak() => breakEnabled = true;
        public event Action<bool> ActionChanged;
        internal bool ConsumedLeftClick => leftClickFrame == Time.frameCount;
        internal bool ConsumedRightClick => rightClickFrame == Time.frameCount;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<CapsuleCollider2D>();
            if (dialogueBox)
            {
                speechBubble = dialogueBox.GetComponent<PlayerSpeechBubble>();
                if (speechBubble) speechBubble.Initialize(this, hitbox, levelCamera);
            }
            visual = GetComponent<SpriteRenderer>();
            if (!spineVisual) spineVisual = GetComponentInChildren<SkeletonRenderer>(true);
            if (!animator && spineVisual) animator = spineVisual.GetComponent<Animator>();
            if (!animator) animator = GetComponent<Animator>();
            if (animator && animator.runtimeAnimatorController)
            {
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.type == AnimatorControllerParameterType.Bool)
                    {
                        if (parameter.nameHash == MovingParameter) hasMovingParameter = true;
                        if (parameter.nameHash == JumpingParameter) hasJumpingParameter = true;
                    }
                    else if (parameter.type == AnimatorControllerParameterType.Trigger)
                    {
                        if (parameter.nameHash == RespawnParameter) hasRespawnParameter = true;
                        if (parameter.nameHash == WallStopParameter) hasWallStopParameter = true;
                    }
                }
                SetWalkingAnimation(false);
            }

            traversal = new PlayerTraversal(this, body, hitbox, maxSlopeAngle);
            lastPosition = returnPosition = body.position;
            bool flipped = spineVisual ? spineVisual.initialFlipX : visual.flipX;
            facing = flipped == spriteFacesRight ? -1 : 1;
            if (initialCampfire && initialCampfire.gameObject.scene == gameObject.scene)
                SetCheckpoint(initialCampfire, snapCamera: true);
            HideDialogue();
        }

        private void OnEnable()
        {
            ResetCameraHistory(body.position);
            if (spineVisual) spineVisual.OnRebuild += OnSpineRebuild;
            ApplyFacing();
        }

        private void Update()
        {
            if (LevelGameFlow.BlocksGameplayInput(this) || GamePresentation.PresentationEvents.BlocksInput) return;
            var now = Time.unscaledTimeAsDouble;
            if (now >= dialogueDeadline) HideDialogue();
            if (ConversationBlocksInput) return;

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                ReturnToCheckpoint();
                GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.CheckpointReset, this);
                return;
            }
            if (reaction != Reaction.None) return;
            bool overUi = LevelGameFlow.PointerOverUi(this);
            bool left = !overUi && mouse != null && mouse.leftButton.wasPressedThisFrame;
            bool right = !overUi && mouse != null && mouse.rightButton.wasPressedThisFrame;
            bool overPlayer = false;
            if (mouse != null && TryGetPointer(mouse.position.ReadValue(), out var pointer))
            {
                if (!IsActing && !overUi) FacePointer(pointer.x);
                overPlayer = (left || right) && hitbox.OverlapPoint(pointer);
            }
            if (left && overPlayer) leftClickFrame = Time.frameCount;
            if (right && overPlayer) rightClickFrame = Time.frameCount;
            if (left && !overPlayer) lastPlayerClick = double.NegativeInfinity;

            if (left && overPlayer)
            {
                if (now - lastPlayerClick <= doubleClickSeconds)
                {
                    ReturnToCheckpoint();
                    GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.CheckpointReset, this);
                    return;
                }
                lastPlayerClick = now;
            }

            if (GamePresentation.PresentationEvents.ResetOnly) return;
            bool space = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
            if (space && GamePresentation.PresentationEvents.BlocksRun) return;
            if (IsActing && (space || (left && overPlayer)))
            {
                StopWalking();
                ShowDialogue(now);
                GamePresentation.PresentationEvents.Report(space ? GamePresentation.PlayEvent.StopBySpace : GamePresentation.PlayEvent.StopByClick, this);
            }
            else if (space)
            {
                walkDirection = facing;
                HideDialogue();
                SetActing(true);
                GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.RunStarted, this);
            }
            else if (!IsActing && right && overPlayer)
            {
                ShowDialogue(now);
            }
        }

        private void FixedUpdate()
        {
            if (LevelGameFlow.BlocksGameplayInput(this)) return;
            Vector2 position = body.position;
            RecordCameraPosition(position, Time.fixedTimeAsDouble);
            if (IsInConversation)
            {
                body.linearVelocityX = 0f;
                return;
            }
            UpdateCheckpoint(lastPosition, position);
            UpdateCameraCrossing(lastPosition, position);
            lastPosition = position;

            if (reaction != Reaction.None)
            {
                AdvanceReaction(Time.fixedDeltaTime);
                SetWalkingAnimation(false);
                return;
            }

            traversal.UpdateAirborneState();
            if (traversal.UpdateBedBounce())
            {
                SetWalkingAnimation(IsActing);
                return;
            }
            if (IsActing)
                traversal.Tick(walkDirection, moveSpeed, stepHeight, jumpEnabled ? jumpHeight : 0f, Time.fixedDeltaTime);
            else
                body.linearVelocityX = 0f;
            SetWalkingAnimation(IsActing && reaction == Reaction.None);
        }

        private void OnDisable()
        {
            if (spineVisual) spineVisual.OnRebuild -= OnSpineRebuild;
            if (body) StopWalking();
            HideDialogue();
            movingCamera = null;
            cameraPositionHistory.Clear();
        }

        private void LateUpdate()
        {
            if (!GamePresentation.PresentationEvents.CameraOwned) AdvanceCamera(Time.deltaTime);
        }

        private void AdvanceCamera(float deltaTime)
        {
            if (!movingCamera) return;
            cameraElapsed += deltaTime;
            float progress = Mathf.Clamp01(cameraElapsed / cameraDuration);
            movingCamera.position = Vector3.Lerp(cameraStart, cameraDestination, Mathf.SmoothStep(0f, 1f, progress));
            if (progress >= 1f) movingCamera = null;
        }

        private bool TryGetPointer(Vector2 screenPosition, out Vector2 worldPosition)
        {
            worldPosition = default;
            var view = levelCamera ? levelCamera : Camera.main;
            if (!view) return false;
            var ray = view.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.forward, transform.position);
            if (!plane.Raycast(ray, out float distance)) return false;
            worldPosition = ray.GetPoint(distance);
            return true;
        }

        private void FacePointer(float pointerX)
        {
            float offset = pointerX - body.position.x;
            if (Mathf.Abs(offset) < 0.025f) return;
            int nextFacing = offset > 0f ? 1 : -1;
            if (nextFacing != facing) GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.Turned, this);
            facing = nextFacing;
            ApplyFacing();
        }

        private void OnSpineRebuild(ISkeletonRenderer renderer) => ApplyFacing();

        private void ApplyFacing()
        {
            bool flip = spriteFacesRight ? facing < 0 : facing > 0;
            if (visual) visual.flipX = flip;
            var skeleton = spineVisual ? spineVisual.skeleton : null;
            if (skeleton != null)
                skeleton.ScaleX = Mathf.Abs(skeleton.ScaleX) * (flip ? -1f : 1f);
        }

        private void UpdateCheckpoint(Vector2 from, Vector2 to)
        {
            Vector2 path = to - from;
            float lengthSquared = path.sqrMagnitude;
            Campfire reached = null;
            float latest = -1f;
            float nearest = float.PositiveInfinity;
            bool canTouch = hitbox.isActiveAndEnabled && body.simulated;
            foreach (var campfire in Campfire.InScene)
            {
                if (!campfire || campfire.gameObject.scene != gameObject.scene) continue;
                Vector2 marker = campfire.Position;
                float progress = lengthSquared > 0.000001f
                    ? Mathf.Clamp01(Vector2.Dot(marker - from, path) / lengthSquared) : 0f;
                float distance = (marker - (from + path * progress)).sqrMagnitude;
                float radiusSquared = campfire.ReachRadius * campfire.ReachRadius;
                if (distance > radiusSquared &&
                    (!canTouch || (marker - hitbox.ClosestPoint(marker)).sqrMagnitude > radiusSquared)) continue;
                if (progress < latest || (progress == latest && distance >= nearest)) continue;
                reached = campfire;
                latest = progress;
                nearest = distance;
            }
            if (reached && reached != checkpoint) SetCheckpoint(reached);
        }

        private void SetCheckpoint(Campfire campfire, bool snapCamera = false)
        {
            if (checkpoint) checkpoint.SetCurrent(false);
            checkpoint = campfire;
            checkpoint.SetCurrent(true);
            returnPosition = campfire.Position + Vector2.up * SpawnLift;
            MoveCameraToCheckpoint(snapCamera);
            if (!snapCamera) GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.CheckpointReached, this, campfire);
        }

        private void UpdateCameraCrossing(Vector2 from, Vector2 to)
        {
            if (from == to) return;
            CameraTarget lastTarget = null;
            bool lastRight = false;
            float lastProgress = -1f;
            foreach (var target in CameraTarget.InScene)
            {
                if (!target || target.gameObject.scene != gameObject.scene || !target.HasTwoPositions) continue;
                if (!cameraSides.TryGetValue(target, out bool previous)) previous = target.IsPosition2Side(from);
                bool crossed = target.TryCross(from, to, previous, out bool right, out float progress);
                cameraSides[target] = right;
                if (!crossed || progress <= lastProgress) continue;
                lastTarget = target;
                lastRight = ResolveCameraArrival(target, right);
                lastProgress = progress;
            }
            if (lastTarget) MoveCamera(lastTarget, lastRight, snap: false);
        }

        private void ResetCameraHistory(Vector2 position)
        {
            cameraPositionHistory.Clear();
            RecordCameraPosition(position, Time.fixedTimeAsDouble);
        }

        private void RecordCameraPosition(Vector2 position, double now)
        {
            var sample = (time: now, position: position);
            int count = cameraPositionHistory.Count;
            if (count > 0 && cameraPositionHistory[count - 1].time == now)
                cameraPositionHistory[count - 1] = sample;
            else
                cameraPositionHistory.Add(sample);

            double cutoff = now - CameraTarget.MaximumHistorySeconds;
            int expired = 0;
            while (expired + 1 < cameraPositionHistory.Count && cameraPositionHistory[expired + 1].time < cutoff)
                expired++;
            if (expired > 0) cameraPositionHistory.RemoveRange(0, expired);
        }

        private Vector2 CameraPositionAgo(float seconds)
        {
            if (cameraPositionHistory.Count == 0) return body.position;
            double time = Time.fixedTimeAsDouble - seconds;
            var before = cameraPositionHistory[0];
            if (time <= before.time) return before.position;
            for (int i = 1; i < cameraPositionHistory.Count; i++)
            {
                var after = cameraPositionHistory[i];
                if (time <= after.time)
                {
                    float progress = (float)((time - before.time) / (after.time - before.time));
                    return Vector2.Lerp(before.position, after.position, progress);
                }
                before = after;
            }
            return before.position;
        }

        private bool ResolveCameraArrival(CameraTarget target, bool crossingSide)
        {
            if (!target.UsesYHistory) return crossingSide;
            bool fallback = activeCameraTarget == target && activeCameraCenter
                ? activeCameraCenter == target.GetCenter(true) : crossingSide;
            return target.ResolveArrivalSide(CameraPositionAgo(target.YHistorySeconds), fallback);
        }

        private void MoveCameraToCheckpoint(bool snap)
        {
            var target = checkpoint ? checkpoint.CameraTarget : null;
            bool right = target && target.IsPosition2Side(returnPosition);
            if (snap)
            {
                if (target && cameraSides.TryGetValue(target, out bool previous))
                    right = target.ResolveSide(returnPosition, previous);
                cameraSides.Clear();
                if (target && target.HasTwoPositions) cameraSides[target] = right;
            }
            if (target && (snap || !target.HasTwoPositions)) MoveCamera(target, right, snap);
        }

        private void MoveCamera(CameraTarget target, bool right, bool snap)
        {
            var center = target.GetCenter(right);
            if (!center) return;
            if (!snap && activeCameraTarget == target && activeCameraCenter == center) return;

            var view = levelCamera ? levelCamera : Camera.main;
            if (!view) return;
            activeCameraTarget = target;
            activeCameraCenter = center;

            var current = view.transform.position;
            var destination = new Vector3(center.position.x, center.position.y, current.z);
            if (snap || target.TransitionSeconds <= 0f)
            {
                movingCamera = null;
                view.transform.position = destination;
                return;
            }

            cameraStart = current;
            cameraDestination = destination;
            cameraElapsed = 0f;
            cameraDuration = target.TransitionSeconds;
            movingCamera = view.transform;
        }

        internal void EnterRoomCamera(CameraTarget target, bool refreshSide = false)
        {
            if (!target || (!refreshSide && activeCameraTarget == target)) return;
            bool crossingSide = cameraSides.TryGetValue(target, out bool previous)
                ? target.ResolveSide(body.position, previous)
                : target.IsPosition2Side(body.position);
            if (target.HasTwoPositions) cameraSides[target] = crossingSide;
            MoveCamera(target, ResolveCameraArrival(target, crossingSide), snap: false);
        }

        private void ReturnToCheckpoint()
        {
            if (reaction == Reaction.Respawn) return;
            StopWalking();
            HideDialogue();
            lastPlayerClick = double.NegativeInfinity;
            body.position = returnPosition;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            lastPosition = returnPosition;
            ResetCameraHistory(returnPosition);
            MoveCameraToCheckpoint(snap: true);
            if (TriggerAnimation(RespawnParameter, hasRespawnParameter))
            {
                reaction = Reaction.Respawn;
                reactionRemaining = AnimationSeconds("Respawn", 1.1f);
                SetActing(true);
            }
        }

        internal void Die() => ReturnToCheckpoint();

        // 仅供主线演出调用；沿用原来的行走和碰撞，不改写寻路、速度或角色能力。
        public void RunForPresentation(int direction)
        {
            StopWalking(); EndConversation(); facing = walkDirection = direction >= 0 ? 1 : -1;
            ApplyFacing(); HideDialogue(); SetActing(true);
        }
        public void StopForPresentation() { StopWalking(); HideDialogue(); }
        public void HideLegacySpeechForPresentation() => HideDialogue();
        // 镜头演出使用原关卡当前相机目标，返回后清理旧插值，避免镜头再次跳走。
        public bool TryGetPresentationCameraPosition(out Vector3 position)
        {
            var view = levelCamera ? levelCamera : Camera.main;
            position = view ? view.transform.position : Vector3.zero;
            if (!activeCameraCenter) return false;
            position.x = activeCameraCenter.position.x; position.y = activeCameraCenter.position.y;
            return true;
        }
        public void FinishPresentationCamera() => movingCamera = null;

        internal bool BeginConversation()
        {
            if (!isActiveAndEnabled || IsInConversation || reaction != Reaction.None) return false;
            IsInConversation = true;
            StopWalking();
            HideDialogue();
            return true;
        }

        internal void EndConversation()
        {
            IsInConversation = false;
            conversationInputFrame = Time.frameCount;
        }

        internal void StopOnBed()
        {
            StopWalking();
            body.linearVelocity = Vector2.zero;
            ShowDialogue(Time.unscaledTimeAsDouble);
        }

        internal void BounceFromBed(Vector2 velocity)
        {
            if (reaction == Reaction.None && isActiveAndEnabled && body && body.simulated && body.bodyType == RigidbodyType2D.Dynamic)
            {
                traversal.BounceFromBed(velocity);
                GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.Bounced, this);
            }
        }

        internal bool TryLaunchUpwards(float speed)
        {
            if (reaction != Reaction.None || !isActiveAndEnabled || !body || !body.simulated || body.bodyType != RigidbodyType2D.Dynamic)
                return false;
            traversal.LaunchUpwards(speed);
            GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.Bounced, this);
            return true;
        }

        private void StopWalking()
        {
            reaction = Reaction.None;
            reactionRemaining = 0f;
            bounceElapsed = 0f;
            traversal?.Cancel();
            body.linearVelocityX = 0f;
            if (animator && animator.isActiveAndEnabled && animator.isInitialized && animator.runtimeAnimatorController)
            {
                if (hasRespawnParameter) animator.ResetTrigger(RespawnParameter);
                if (hasWallStopParameter) animator.ResetTrigger(WallStopParameter);
            }
            SetWalkingAnimation(false);
            SetActing(false);
        }

        private void SetActing(bool acting)
        {
            if (IsActing == acting) return;
            IsActing = acting;
            ActionChanged?.Invoke(acting);
        }

        private void SetWalkingAnimation(bool moving)
        {
            if (!animator || !animator.isActiveAndEnabled || !animator.isInitialized) return;
            if (hasMovingParameter)
            {
                if (animator.GetBool(MovingParameter) != moving)
                    animator.SetBool(MovingParameter, moving);
            }
            if (hasJumpingParameter)
            {
                bool jumping = reaction == Reaction.None && IsJumping;
                if (animator.GetBool(JumpingParameter) != jumping)
                    animator.SetBool(JumpingParameter, jumping);
            }
        }

        internal void HitWall(int direction, WallStopReason reason)
        {
            if (reaction != Reaction.None || IsInConversation) return;
            StopWalking();
            LastWallStopReason = reason;
            bounceDirection = -direction;
            bounceStartX = body.position.x;
            body.linearVelocityY = Mathf.Min(0f, body.linearVelocityY);
            reaction = Reaction.WallStop;
            reactionRemaining = Mathf.Max(wallBounceSeconds + Time.fixedDeltaTime, AnimationSeconds("WallStop", 1f));
            SetActing(true);
            TriggerAnimation(WallStopParameter, hasWallStopParameter);
            ShowDialogue(Time.unscaledTimeAsDouble);
            onWallStopped?.Invoke(reason);
            GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.WallBlocked, this);
        }

        private void AdvanceReaction(float deltaTime)
        {
            body.linearVelocityX = 0f;
            if (reaction == Reaction.WallStop && bounceElapsed < wallBounceSeconds)
            {
                bounceElapsed = Mathf.Min(wallBounceSeconds, bounceElapsed + deltaTime);
                float current = bounceElapsed / wallBounceSeconds;
                float targetX = bounceStartX + bounceDirection * wallBounceDistance * (2f * current - current * current);
                float distance = Mathf.Max(0f, (targetX - body.position.x) * bounceDirection);
                distance = traversal.LimitRetreat(bounceDirection, distance);
                body.linearVelocityX = bounceDirection * distance / deltaTime;
            }
            reactionRemaining -= deltaTime;
            if (reactionRemaining <= 0f) StopWalking();
        }

        private bool TriggerAnimation(int parameter, bool exists)
        {
            if (!exists || !animator || !animator.isActiveAndEnabled || !animator.runtimeAnimatorController) return false;
            animator.ResetTrigger(parameter);
            animator.SetTrigger(parameter);
            return true;
        }

        private float AnimationSeconds(string name, float fallback)
        {
            if (animator && animator.runtimeAnimatorController)
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    if (clip.name == name) return Mathf.Max(0.02f, clip.length / Mathf.Max(0.01f, animator.speed));
            return fallback;
        }

        private void ShowDialogue(double now)
        {
            if (!dialogueBox) return;
            if (speechBubble) speechBubble.Show();
            else dialogueBox.SetActive(true);
            dialogueDeadline = now + dialogueSeconds;
        }

        private void HideDialogue()
        {
            if (speechBubble) speechBubble.Hide();
            else if (dialogueBox && dialogueBox.activeSelf) dialogueBox.SetActive(false);
            dialogueDeadline = double.PositiveInfinity;
        }
    }
}
