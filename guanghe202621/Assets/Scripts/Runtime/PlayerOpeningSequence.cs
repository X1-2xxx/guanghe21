using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LevelKit
{
    [DefaultExecutionOrder(-10), DisallowMultipleComponent]
    [AddComponentMenu("Level Kit/Player Opening Sequence")]
    public sealed class PlayerOpeningSequence : MonoBehaviour
    {
        [SerializeField] private PlayerActionController player;
        [SerializeField] private Transform emergePoint;
        [SerializeField] private Transform destination;
        [SerializeField] private Camera levelCamera;
        [SerializeField] private Transform openingCameraPosition;
        [Tooltip("Existing Walk clip. Temporarily replaces Run during the opening.")]
        [SerializeField] private AnimationClip walkingClip;
        [SerializeField, Min(0f)] private float fadeSeconds = .65f;
        [SerializeField, Min(.1f)] private float walkingSpeed = 2.5f;
        [SerializeField, Min(0f)] private float cameraReturnSeconds = 1f;

        private enum Phase { Fade, Walk, CameraReturn }
        private static readonly Dictionary<Scene, PlayerOpeningSequence> scenes = new Dictionary<Scene, PlayerOpeningSequence>();
        private Rigidbody2D body;
        private SpriteRenderer[] sprites;
        private Color[] spriteColors;
        private SkeletonRenderer[] skeletons;
        private Color[] skeletonColors;
        private Animator animator;
        private RuntimeAnimatorController originalController;
        private AnimatorOverrideController openingController;
        private Vector3 emergence, arrival, cameraRestPosition, cameraOpeningPosition;
        private bool prepared, played, bodyWasSimulated, startInvoked;
        private int releaseAfterFrame = -1;
        private float elapsed, walkSeconds;
        private Phase phase;
        public bool IsRunning { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => scenes.Clear();

        private void Awake() { if (enabled) Prepare(); }

        private void Prepare()
        {
            if (prepared || played || !player || !emergePoint || !destination) return;
            body = player.GetComponent<Rigidbody2D>();
            if (!body) return;
            arrival = destination.position;
            emergence = emergePoint.position;
            bodyWasSimulated = body.simulated;
            // Prefer the visible Spine actor over an old placeholder Animator on the player root.
            var spineAnimator = player.GetComponentInChildren<SkeletonMecanim>(true);
            animator = spineAnimator ? spineAnimator.GetComponent<Animator>() : player.GetComponentInChildren<Animator>(true);
            sprites = player.GetComponentsInChildren<SpriteRenderer>(true);
            spriteColors = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) spriteColors[i] = sprites[i].color;
            skeletons = player.GetComponentsInChildren<SkeletonRenderer>(true);
            skeletonColors = new Color[skeletons.Length];
            for (int i = 0; i < skeletons.Length; i++)
            {
                skeletons[i].Initialize(false);
                if (skeletons[i].Skeleton != null) skeletonColors[i] = skeletons[i].Skeleton.GetColor();
            }
            if (!levelCamera) levelCamera = Camera.main;
            cameraRestPosition = levelCamera ? levelCamera.transform.position : Vector3.zero;
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            prepared = true;
            SetOpacity(0f);
        }

        private void OnEnable()
        {
            Prepare();
            scenes[gameObject.scene] = this;
            GameEventCenter.StateChanged += OnStateChanged;
            if (startInvoked && GameStateManager.Current == GameState.Playing) Begin();
        }

        private void Start()
        {
            startInvoked = true;
            if (GameStateManager.Current == GameState.Playing) Begin();
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Playing) Begin();
            else if (state == GameState.MainMenu && IsRunning) Finish();
        }

        private void Begin()
        {
            if (!prepared || played) return;
            played = true;
            IsRunning = true;
            elapsed = 0f;
            phase = Phase.Fade;
            emergence = emergePoint.position;
            arrival = destination.position;
            cameraRestPosition = levelCamera ? levelCamera.transform.position : Vector3.zero;
            cameraOpeningPosition = openingCameraPosition ? openingCameraPosition.position : cameraRestPosition;
            if (levelCamera) levelCamera.transform.position = cameraOpeningPosition;
            SetPosition(emergence);
            player.SetOpeningWalking(arrival.x < emergence.x ? -1 : 1, false);
            SetOpacity(0f);
            walkSeconds = Vector2.Distance(emergence, arrival) / Mathf.Max(.1f, walkingSpeed);
        }

        private void Update() => Tick(Time.deltaTime);

        private void Tick(float deltaTime)
        {
            if (!IsRunning || deltaTime <= 0f) return;
            float remaining = deltaTime;
            while (IsRunning && remaining > 0f)
            {
                float duration = phase == Phase.Fade ? Mathf.Max(0f, fadeSeconds) : phase == Phase.Walk ? walkSeconds : Mathf.Max(0f, cameraReturnSeconds);
                float step = Mathf.Min(remaining, Mathf.Max(0f, duration - elapsed));
                elapsed += step;
                remaining -= step;
                float progress = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                if (phase == Phase.Fade) SetOpacity(Mathf.SmoothStep(0f, 1f, progress));
                else if (phase == Phase.Walk)
                {
                    SetPosition(Vector3.Lerp(emergence, arrival, progress));
                    player.SetOpeningWalking(arrival.x < emergence.x ? -1 : 1, true);
                }
                else if (levelCamera)
                    levelCamera.transform.position = Vector3.Lerp(cameraOpeningPosition, cameraRestPosition, Mathf.SmoothStep(0f, 1f, progress));
                if (progress < 1f) break;
                elapsed = 0f;
                if (phase == Phase.Fade) { phase = Phase.Walk; BeginWalkingAnimation(); }
                else if (phase == Phase.Walk) { phase = Phase.CameraReturn; player.SetOpeningWalking(arrival.x < emergence.x ? -1 : 1, false); RestoreAnimation(); }
                else Finish();
            }
        }

        private void BeginWalkingAnimation()
        {
            if (animator && animator.runtimeAnimatorController && walkingClip)
            {
                originalController = animator.runtimeAnimatorController;
                foreach (var clip in originalController.animationClips)
                    if (clip.name == "Run")
                    {
                        openingController = new AnimatorOverrideController(originalController);
                        openingController[clip] = walkingClip;
                        animator.runtimeAnimatorController = openingController;
                        break;
                    }
            }
            player.SetOpeningWalking(arrival.x < emergence.x ? -1 : 1, true);
        }

        private void SetPosition(Vector3 position)
        {
            body.position = position;
            player.transform.position = position;
        }

        private void SetOpacity(float opacity)
        {
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i]) { var color = spriteColors[i]; color.a *= opacity; sprites[i].color = color; }
            for (int i = 0; i < skeletons.Length; i++)
                if (skeletons[i] && skeletons[i].Skeleton != null)
                { var color = skeletonColors[i]; skeletons[i].Skeleton.SetColor(color.r, color.g, color.b, color.a * opacity); }
        }

        private void RestoreAnimation()
        {
            if (animator && originalController) animator.runtimeAnimatorController = originalController;
            originalController = null;
            if (openingController)
            {
                if (Application.isPlaying) Destroy(openingController); else DestroyImmediate(openingController);
                openingController = null;
            }
        }

        private void Finish()
        {
            if (!prepared) return;
            RestoreAnimation();
            if (player && body)
            {
                SetPosition(arrival);
                SetOpacity(1f);
                body.simulated = bodyWasSimulated;
                player.FinishOpeningAt(arrival);
            }
            if (IsRunning && levelCamera) levelCamera.transform.position = cameraRestPosition;
            IsRunning = false;
            prepared = false;
            releaseAfterFrame = Time.frameCount;
        }

        internal static bool BlocksGameplayInput(Component source)
        {
            if (!source || !scenes.TryGetValue(source.gameObject.scene, out var sequence) || !sequence || !sequence.isActiveAndEnabled) return false;
            return sequence.prepared || sequence.IsRunning || Time.frameCount <= sequence.releaseAfterFrame;
        }

        private void OnDisable()
        {
            GameEventCenter.StateChanged -= OnStateChanged;
            Finish();
            if (scenes.TryGetValue(gameObject.scene, out var owner) && owner == this) scenes.Remove(gameObject.scene);
        }
    }
}
