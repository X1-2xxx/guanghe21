using System.Collections.Generic;
using LevelKit;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GamePresentation
{
    /// <summary>全局演出入口。只实例化预制体；教学标记跨场景保留，台词资产始终只读。</summary>
    [DefaultExecutionOrder(200)]
    public sealed class PresentationRuntime : MonoBehaviour
    {
        public static PresentationRuntime Instance { get; private set; }
        [InspectorName("剧情与台词配置")] public PresentationLibrary library;
        [InspectorName("屏幕剧情预制体实例")] public StoryPanelView storyView;
        [InspectorName("头顶气泡预制体实例")] public BubbleDialogView headView;
        [InspectorName("游玩时道具栏")] public GameObject inventoryTray;
        [InspectorName("镜头演出组件")] public CameraPerformanceManager cameraPerformance;
        [InspectorName("镜头返回位置提供器")] public PresentationCameraAnchor cameraAnchor;
        [InspectorName("游玩小台词使用打字机")] public bool useTypewriter;
        [InspectorName("每秒显示字数"), Min(1)] public float charactersPerSecond = 32f;
        [InspectorName("全局教学：半路叫停"), SerializeField] public bool flag_taught_midstop;
        [InspectorName("全局教学：脚下物块不可拖"), SerializeField] public bool flag_taught_underfoot_lock;
        [InspectorName("全局教学：奔跑时不可编辑"), SerializeField] public bool flag_taught_run_lock;
        [SerializeField, Min(0f), InspectorName("触发剧情后继续运行的秒数")] private float checkpointPauseSeconds = .5f;
        [SerializeField, Min(0f), InspectorName("气泡最短阅读秒数")] private float minimumBubbleSeconds = 2f;
        [SerializeField, InspectorName("正式对白期间暂停玩法世界")] private bool pauseWorldDuringDialogue = false;
        internal float CheckpointPauseSeconds => checkpointPauseSeconds;
        internal bool PauseWorldDuringDialogue => pauseWorldDuringDialogue;
        private float PresentationDelta => Director != null && Director.WorldPaused ? Time.unscaledDeltaTime : Time.deltaTime;
        public StoryDirector Director { get; private set; }
        public PresentationSceneBinding Scene { get; private set; }
        public bool CanPresent => Scene && Scene.player && GameStateManager.Current == GameState.Playing && (Time.timeScale > 0f || (Director != null && Director.WorldPaused)) &&
            !PlayerOpeningSequence.BlocksPresentation(Scene.player);
        public bool HeadVisible => headRemaining > 0f;
        internal bool HeadProtected => !headIsStory && HeadVisible && headMinimumRemaining > 0f;
        public bool CheckpointResetAvailable => !V4Mode || Scene.initialEpisode != "village.opening" || resetTutorialUnlocked;
        private bool resetTutorialUnlocked;
        private float headMinimumRemaining;
        private PendingBubble pendingBubble;
        private sealed class PendingBubble
        {
            public string text; public float seconds; public int priority;
        }
        public string VisibleHeadText { get; private set; }
        public bool V4Mode => Scene && Scene.enableMainline;
        public bool UsesCollectionQuest => V4Mode && Scene.preserveCollectionQuest && Scene.legacyInnQuest is InnQuest;

        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        private readonly Dictionary<string, float> cooldowns = new Dictionary<string, float>();
        private readonly HashSet<string> onceBarks = new HashSet<string>();
        private readonly HashSet<string> shownGuidance = new HashSet<string>();
        private readonly HashSet<string> shownVillageNudges = new HashSet<string>();
        private readonly Queue<float> turns = new Queue<float>();
        private readonly Queue<string> pickupBarks = new Queue<string>();
        private float headRemaining, headElapsed, lastInput, runStarted, nudgeAt;
        private int headPriority, shownFrame, quickStops, tapeCount;
        private bool headIsStory, headIsGuidance, began, forcedRun, villageSunk, openingReset, jumpAcquired, wallBroken;
        private bool openingPitFallStarted, openingPitImpactStarted;
        private float openingPitImpactRemaining;
        private bool pendingBridgeIntroduction, pendingJumpIntroduction, pendingJumpDiscovery;
        private int bridgeArrivalFrame = -1, jumpArrivalFrame = -1, jumpDiscoveryFrame = -1;
        private int shownCharacters;
        private Rigidbody2D body;
        private CapsuleCollider2D capsule;
        private BlockManipulation manipulation;
        private AudioManager audioRouter;
        private GameFlowTemplate gameFlow;
        private bool actorHeld;
        private float lastFall, stationarySeconds;
        private bool villageGlueReviewPending;
        private float villageGlueReviewAt, villageGlueStableFor, nextVillageProbe;
        private Vector2 previousPosition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            Director = new StoryDirector(this);
            storyView.GetComponent<Canvas>().enabled = true;
            if (cameraPerformance) cameraPerformance.PerformanceEnded += OnCameraEnded;
            storyView.Bind(Director.Advance, Director.Choose);
            headView.SetVisible(false); inventoryTray.SetActive(false);
        }
        private void OnEnable() => GameEventCenter.StateChanged += OnStateChanged;
        private void OnDisable() { GameEventCenter.StateChanged -= OnStateChanged; Director?.Clear(); TooltipManager.SetSuppressed(this, false); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Attach(PresentationSceneBinding binding)
        {
            if (Scene == binding) return;
            if (!binding || !binding.player || !binding.levelCamera)
            { Debug.LogError("剧情场景绑定缺少玩家或相机，请在 Inspector 中拖入场景引用。", binding); return; }
            CancelOpeningRun(); Director.Clear(); Scene = binding;
            body = Scene.player.GetComponent<Rigidbody2D>(); capsule = Scene.player.GetComponent<CapsuleCollider2D>();
            manipulation = null;
            foreach (var controls in FindObjectsByType<BlockManipulation>(FindObjectsSortMode.None))
                if (controls.gameObject.scene == Scene.gameObject.scene) { manipulation = controls; break; }
            audioRouter = FindAnyObjectByType<AudioManager>();
            gameFlow = FindAnyObjectByType<GameFlowTemplate>();
            began = forcedRun = pendingBridgeIntroduction = pendingJumpIntroduction = pendingJumpDiscovery = villageSunk = openingReset = jumpAcquired = wallBroken = actorHeld = false;
            resetTutorialUnlocked = false;
            ItemUnlockManager.SetUnlocked(GameItemId.Cardboard, !V4Mode || Scene.initialEpisode != "village.opening");
            PrepareInitialBlack();
            ResetVillageGlueReview();
            counts.Clear(); cooldowns.Clear(); turns.Clear(); pickupBarks.Clear(); tapeCount = quickStops = 0;
            previousPosition = body.position; stationarySeconds = 0f; lastInput = Time.unscaledTime;
            shownGuidance.Clear(); shownVillageNudges.Clear(); nudgeAt = float.PositiveInfinity;
            // 其他关卡若已经带有镜头管理器，复用它；不因重复单例而丢失场景引用。
            if (!cameraPerformance) cameraPerformance = CameraPerformanceManager.Instance;
            if (!cameraPerformance) cameraPerformance = gameObject.AddComponent<CameraPerformanceManager>();
            cameraPerformance.PerformanceEnded -= OnCameraEnded;
            cameraPerformance.PerformanceEnded += OnCameraEnded;
            cameraPerformance.Configure(Scene.levelCamera, Scene.player.transform, cameraAnchor);
            cameraAnchor.Capture(Scene.levelCamera, Scene.player.transform);
            if (V4Mode && !UsesCollectionQuest)
            {
                // V4 是独立演出模式；不删除队友旧收集任务，关掉此开关即可恢复旧任务。
                if (Scene.legacyInnQuest) Scene.legacyInnQuest.enabled = false;
                if (Scene.legacyQuestItems != null) foreach (var item in Scene.legacyQuestItems) if (item) item.SetActive(false);
                if (Scene.shoes)
                {
                    if (Scene.shoesPoint) Scene.shoes.transform.position = Scene.shoesPoint.position;
                    Scene.shoes.SetActive(true);
                }
            }
        }
        public void Detach(PresentationSceneBinding binding)
        {
            if (Scene != binding) return;
            CancelOpeningRun(); Director.Clear(); Scene = null; body = null; capsule = null;
            storyView.ClearEquipmentNotice(); pickupBarks.Clear();
            inventoryTray.SetActive(false);
        }
        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Playing) PrepareInitialBlack();
            if (state == GameState.MainMenu) { ResetVillageGlueReview(); CancelOpeningRun(); Director.Clear(); storyView.ClearEquipmentNotice(); pickupBarks.Clear(); began = false; pendingBridgeIntroduction = pendingJumpIntroduction = pendingJumpDiscovery = false; resetTutorialUnlocked = false; shownGuidance.Clear(); shownVillageNudges.Clear(); ItemUnlockManager.SetUnlocked(GameItemId.Cardboard, false); }
            if (state != GameState.Playing && !(state == GameState.Paused && headIsStory)) InterruptBubble();
        }
        private void PrepareInitialBlack()
        {
            if (began || !V4Mode || !Scene.player || GameStateManager.Current != GameState.Playing ||
                PlayerOpeningSequence.BlocksPresentation(Scene.player)) return;
            var initial = library.FindEpisode(Scene.initialEpisode);
            if (initial == null || initial.steps.Find(s => s.id == initial.entry)?.kind != StoryStepKind.BlackTitle) return;
            // Cover the first gameplay frame, including the short pre-dialogue pause.
            storyView.Show(new StoryStep { kind = StoryStepKind.BlackTitle, autoAdvance = true, text = string.Empty }, null);
        }

        private void Update()
        {
            bool live = CanPresent;
            RefreshVisibility();
            if (!live) { headView.SetVisible(false); return; }
            if (!began)
            {
                began = true;
                if (V4Mode && !string.IsNullOrEmpty(Scene.initialEpisode)) Director.Enqueue(Scene.initialEpisode);
            }
            if (V4Mode) TickCheckpointStories();
            Director.Tick(PresentationDelta);
            if (Director.IsEnding)
            {
                if (storyView.EndingReadyForRestart && EndingRestartPressed() && gameFlow)
                    gameFlow.RestartGameToMainMenu();
                return;
            }
            var keyboard = Keyboard.current;
            if (keyboard != null && (!gameFlow || !gameFlow.HasOpenGameplayModal) &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) Director.Advance();
            if (Director.Active || HadInput()) lastInput = Time.unscaledTime;
            // 末件失物先报拾取，再报集齐；这些气泡不会持有剧情输入锁。
            if (!Director.Active && !HeadVisible && pickupBarks.Count > 0) PlayBark(pickupBarks.Dequeue());
            if (!Director.Active && Time.unscaledTime - lastInput >= 18f)
            { lastInput = Time.unscaledTime; PlayBark("idle"); }
            if (V4Mode) TickVillage();
            if (!Director.Active && body && !Scene.player.IsRespawning)
            {
                if (body.linearVelocityY < -4f && Time.time - lastFall > 5f)
                { lastFall = Time.time; PlayBark("fall", Scene.player); }
                stationarySeconds = Vector2.Distance(body.position, previousPosition) < 0.005f ? stationarySeconds + Time.deltaTime : 0f;
                if (stationarySeconds > 8f && Scene.villagePit.Contains(body.position))
                { stationarySeconds = 0f; PlayBark("stuck", Scene.player); }
                previousPosition = body.position;
            }
        }
        private void LateUpdate()
        {
            if (!CanPresent || !Scene.player) return;
            headMinimumRemaining = Mathf.Max(0f, headMinimumRemaining - PresentationDelta);
            if (!Director.Active && !Director.HasPendingEpisodes && !headIsStory && !HeadProtected && pendingBubble != null)
            {
                var next = pendingBubble; pendingBubble = null;
                ShowHead(next.text, next.seconds, next.priority);
            }
            // Gameplay input still works, but cannot erase text before its reading window.
            if (!headIsStory && HeadVisible && Time.frameCount > shownFrame && HadInterruptInput()) InterruptBubble(false);
            if (!HeadVisible) { headView.SetVisible(false); return; }
            headRemaining -= PresentationDelta; headElapsed += PresentationDelta;
            if (headRemaining <= 0f) { InterruptBubble(); return; }
            Scene.player.HideLegacySpeechForPresentation();
            headView.SetVisible(true);
            headView.FollowHead(capsule, Scene.player.transform, Scene.levelCamera);
            if (headIsStory || useTypewriter)
            {
                int count = Mathf.FloorToInt(headElapsed * Mathf.Max(1f, charactersPerSecond));
                if (shownCharacters != count) { shownCharacters = count; headView.Reveal(count); }
            }
        }
        private static bool EndingRestartPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                mouse.middleButton.wasPressedThisFrame || mouse.forwardButton.wasPressedThisFrame || mouse.backButton.wasPressedThisFrame)) return true;
            var gamepad = Gamepad.current;
            if (gamepad != null)
                foreach (var control in gamepad.allControls)
                    if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.wasPressedThisFrame) return true;
            return false;
        }

        private static bool HadInput() => HadInterruptInput() ||
            (Mouse.current != null && (Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f || Mouse.current.scroll.ReadValue().sqrMagnitude > 0f));
        private static bool HadInterruptInput() =>
            (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
            (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame ||
                Mouse.current.scroll.ReadValue().sqrMagnitude > 0f || (Mouse.current.leftButton.isPressed && Mouse.current.delta.ReadValue().sqrMagnitude > .1f)));

        /// <summary>供机关或关卡脚本调用；location 决定“同一位置”的降频计数。</summary>
        public bool PlayBark(string id, Component location = null, bool guidance = false)
        {
            if (!CanPresent || Director.Active) return false;
            var rule = library.FindBark(id);
            if (rule == null || (rule.once && onceBarks.Contains(id))) return false;
            string key = id + "@" + LocationKey(location);
            counts.TryGetValue(key, out int count); count++; counts[key] = count;
            if (cooldowns.TryGetValue(key, out float until) && Time.unscaledTime < until) return false;
            if (HeadVisible && headPriority > rule.priority) return false;
            string[] lines = count >= 3 && rule.brief != null && rule.brief.Length > 0 ? rule.brief :
                count == 2 && rule.repeat != null && rule.repeat.Length > 0 ? rule.repeat : rule.lines;
            if (lines == null || lines.Length == 0) return false;
            var available = new List<string>();
            foreach (var line in lines) if (!string.IsNullOrWhiteSpace(line)) available.Add(line);
            if (available.Count == 0) return false;
            string text = available[Random.Range(0, available.Count)];
            float duration = count >= 3 && rule.brief != null && rule.brief.Length > 0 ? Mathf.Min(1.8f, rule.duration) : rule.duration;
            cooldowns[key] = Time.unscaledTime + (count >= 3 ? Mathf.Max(12f, rule.cooldown) : rule.cooldown);
            if (rule.once) onceBarks.Add(id);
            ShowHead(text, duration, guidance ? 0 : rule.priority, guidance: guidance); return true;
        }
        private string LocationKey(Component source)
        {
            if (!source) return Scene ? Scene.gameObject.scene.path : "global";
            string path = source.name;
            for (Transform parent = source.transform.parent; parent; parent = parent.parent) path = parent.name + "/" + path;
            return source.gameObject.scene.path + "/" + path;
        }
        public void ShowHead(string text, float seconds = 3f, int priority = 10, bool story = false, bool guidance = false)
        {
            if (!Scene || !Scene.player || string.IsNullOrWhiteSpace(text)) return;
            // Optional instructions never replace gameplay feedback or queue across a story boundary.
            if (guidance && (HeadVisible || pendingBubble != null || Director.Active || Director.HasPendingEpisodes)) return;
            if (!story && HeadProtected)
            {
                if (text != VisibleHeadText && (pendingBubble == null || priority >= pendingBubble.priority))
                    pendingBubble = new PendingBubble { text = text, seconds = seconds, priority = priority };
                return;
            }
            pendingBubble = null;
            VisibleHeadText = text; headRemaining = Mathf.Max(story ? 0f : minimumBubbleSeconds, seconds, .1f); headElapsed = 0f;
            headPriority = priority; headIsStory = story; headIsGuidance = guidance; shownFrame = Time.frameCount; shownCharacters = -1;
            headView.Initialize(Scene.levelCamera, text);
            bool animate = story || useTypewriter;
            headMinimumRemaining = story ? 0f : Mathf.Max(minimumBubbleSeconds, animate ? headView.CharacterCount / Mathf.Max(1f, charactersPerSecond) + .6f : 0f);
            headRemaining = Mathf.Max(headRemaining, headMinimumRemaining);
            if (story) headRemaining = Mathf.Max(headRemaining, headView.CharacterCount / Mathf.Max(1f, charactersPerSecond) + 0.9f);
            headView.Reveal(animate ? 0 : headView.CharacterCount);
            headView.SetVisible(true);
            GameEventCenter.RequestSfx(GameplaySfx.SpeechBubblePop);
        }
        internal bool CompleteStoryHeadText()
        {
            if (!headIsStory || !HeadVisible || shownCharacters >= headView.CharacterCount) return false;
            shownCharacters = headView.CharacterCount;
            headElapsed = (headView.CharacterCount + 1f) / Mathf.Max(1f, charactersPerSecond);
            headRemaining = Mathf.Max(headRemaining, .6f);
            headView.Reveal(headView.CharacterCount, false);
            return true;
        }
        internal void DismissGuidance()
        {
            if (!headIsGuidance) return;
            // Keep any real gameplay feedback waiting behind the obsolete instruction.
            headRemaining = headMinimumRemaining = 0f;
            headPriority = 0; headIsStory = headIsGuidance = false;
            VisibleHeadText = string.Empty;
            if (headView) headView.SetVisible(false);
        }
        public void InterruptBubble(bool force = true)
        {
            if (!force && HeadProtected) return;
            if (force) pendingBubble = null;
            headRemaining = headMinimumRemaining = 0f; headPriority = 0; headIsStory = headIsGuidance = false;
            VisibleHeadText = string.Empty; headView.SetVisible(false);
        }

        /// <summary>角色/机关的事实通知。普通小台词不会调用对话锁定和时间控制接口。</summary>
        public void Observe(PlayEvent kind, Component actor, Component subject, float value)
        {
            if (!Scene || (actor && actor.gameObject.scene != Scene.gameObject.scene) || !CanPresent) return;
            switch (kind)
            {
                case PlayEvent.RunStarted:
                    runStarted = Time.time; tapeCount = 0; DismissGuidance(); InterruptBubble(false);
                    if (V4Mode && Director.HasCompleted("village.depart") && !Director.HasScheduled("village.crossed")) ShowHead("真的跑起来了！", 2.5f, 50);
                    else if (Scene.player.BreakEnabled) PlayBark("first_shoe_run");
                    break;
                case PlayEvent.StopBySpace:
                case PlayEvent.StopByClick:
                    bool quickStop = Time.time - runStarted < 1f;
                    quickStops = quickStop ? quickStops + 1 : 0;
                    if (!flag_taught_midstop)
                    {
                        flag_taught_midstop = true;
                        string id = kind == PlayEvent.StopBySpace ? "teach_midstop_space" : "teach_midstop_click";
                        if (V4Mode && Director.HasCompleted("inn.intro")) id = "inn_midstop_success";
                        PlayBark(id);
                    }
                    else
                    {
                        PlayBark(quickStop ? (quickStops >= 2 ? "stop_repeated" : "stop_early") : "stop_normal");
                    }
                    break;
                case PlayEvent.UnderfootDenied:
                    if (!flag_taught_underfoot_lock) { flag_taught_underfoot_lock = true; PlayBark("teach_underfoot"); }
                    else PlayBark("underfoot", subject);
                    if (library.deniedClip && audioRouter) audioRouter.PlaySfx(library.deniedClip);
                    break;
                case PlayEvent.RunningEditDenied:
                    if (!flag_taught_run_lock) { flag_taught_run_lock = true; PlayBark("teach_runlock"); }
                    break;
                case PlayEvent.Turned:
                    turns.Enqueue(Time.time); while (turns.Count > 0 && Time.time - turns.Peek() > 3f) turns.Dequeue();
                    if (turns.Count >= 3) { PlayBark("turns"); turns.Clear(); } break;
                case PlayEvent.CheckpointReset:
                    CancelOpeningRun(); forcedRun = false; InterruptBubble(); pickupBarks.Clear();
                    if (Director.ResetOnly) { Director.AllowResetOnly(false); openingReset = true; Signal("village.reset", actor); }
                    else PlayBark("reset"); break;
                case PlayEvent.PitDeath:
                    PlayBark(V4Mode && Scene.villagePit.Contains(body.position) ? "village_fall" : "pit", subject); break;
                case PlayEvent.Jumped: PlayBark("first_jump"); break;
                case PlayEvent.Bounced:
                    if (V4Mode && !onceBarks.Contains("inn_bed_first")) PlayBark("inn_bed_first");
                    else PlayBark("bounce", subject); break;
                case PlayEvent.JumpAcquired:
                    jumpAcquired = true; UnlockAbilityWithNotice(GameItemId.JumpKey);
                    if (V4Mode) Director.Enqueue("jump.acquired"); break;
                case PlayEvent.ShoesAcquired:
                    UnlockAbilityWithNotice(GameItemId.Rocinante);
                    if (V4Mode) Director.Enqueue("inn.shoes"); break;
                case PlayEvent.InnVaseCollected: pickupBarks.Enqueue("inn_vase"); break;
                case PlayEvent.InnChocolateCollected: pickupBarks.Enqueue("inn_chocolate"); break;
                case PlayEvent.InnPortraitCollected:
                    pickupBarks.Enqueue("inn_portrait"); pickupBarks.Enqueue("inn_all_items"); break;
                case PlayEvent.WallBlocked:
                    if (forcedRun && IsBelowVillagePitEntrance()) TryCompleteOpeningFall();
                    else if (actor is PlayerActionController stoppedPlayer && stoppedPlayer.LastWallStopReason == PlayerActionController.WallStopReason.SlopeTooSteep)
                        PlayBark("slope_too_steep", subject ? subject : actor);
                    else if (body.position.x > 80f && !Scene.player.BreakEnabled) PlayBark("wall_no_shoes");
                    else PlayBark(V4Mode && Scene.villagePit.Contains(body.position) ? "village_blocked" : "blocked", subject); break;
                case PlayEvent.WallBroken:
                    wallBroken = true; PlayBark("wall_break", subject); break;
                case PlayEvent.TapeConnected:
                    DismissGuidance();
                    tapeCount++; if (tapeCount >= 4) PlayBark("many_tapes");
                    if (V4Mode) QueueVillageGlueReview(subject); break;
                case PlayEvent.TapeRemoved: ResetVillageGlueReview(); DismissGuidance(); PlayBark("rebuild", subject); break;
                case PlayEvent.BlockGrabbed: ResetVillageGlueReview(); DismissGuidance(); InterruptBubble(false); break;
                case PlayEvent.CheckpointReached: Checkpoint(subject); break;
                case PlayEvent.Falling: PlayBark("fall", subject); break;
                case PlayEvent.StuckInPit: PlayBark("stuck", subject); break;
                case PlayEvent.BridgeShook: PlayBark("bridge_shook", subject); break;
                case PlayEvent.CrookedBridgePassed: PlayBark("crooked_bridge", subject); break;
                case PlayEvent.SupportCollapsed: PlayBark("collapse", subject); break;
                case PlayEvent.TreeNearMiss: PlayBark("tree_near_miss", subject); break;
                case PlayEvent.Idle: PlayBark("idle"); break;
            }
        }
        private void UnlockAbilityWithNotice(GameItemId item)
        {
            // Restoration/UI synchronization uses SetUnlocked directly; only a new acquisition shows this notice.
            if (ItemUnlockManager.Unlock(item)) storyView.ShowEquipmentNotice(item);
        }
        private void Checkpoint(Component source)
        {
            DismissGuidance();
            if (!V4Mode || Scene.checkpoints == null) return;
            int index = System.Array.FindIndex(Scene.checkpoints, c => c == source);
            if (index == 1) ResetVillageGlueReview();
            if (index == 1)
            {
                pendingBridgeIntroduction = !Director.HasScheduled("bridge.intro");
                bridgeArrivalFrame = Time.frameCount;
            }
            if (index == 2) PlayBark("bridge_complete");
            if (index == 3) { PlayBark("forest_complete"); RequestJumpDiscovery(); }
        }
        private void RequestJumpIntroduction()
        {
            if (jumpAcquired || pendingJumpIntroduction || Director.HasScheduled("jump.intro")) return;
            pendingJumpIntroduction = true;
            jumpArrivalFrame = Time.frameCount;
        }
        private void RequestJumpDiscovery()
        {
            if (jumpAcquired || pendingJumpDiscovery || Director.HasScheduled("jump.discover")) return;
            pendingJumpDiscovery = true;
            jumpDiscoveryFrame = Time.frameCount;
        }
        private void TickCheckpointStories()
        {
            if (Scene.player.IsGameplayCameraMoving || Scene.player.IsRespawning || Director.CameraBusy) return;
            if (pendingBridgeIntroduction && Time.frameCount > bridgeArrivalFrame)
            {
                pendingBridgeIntroduction = false;
                if (openingReset) Director.Enqueue("village.crossed");
                Director.Enqueue("bridge.intro");
            }
            if (pendingJumpIntroduction && Time.frameCount > jumpArrivalFrame)
            {
                pendingJumpIntroduction = false;
                Director.Enqueue("jump.intro");
            }
            if (pendingJumpDiscovery && Time.frameCount > jumpDiscoveryFrame && !Director.Active && !Director.HasPendingEpisodes)
            {
                pendingJumpDiscovery = false;
                Director.Enqueue("jump.discover");
            }
        }
        public void Signal(string id, Component source = null)
        {
            if (!CanPresent) return;
            Director.Signal(id);
            if (!V4Mode) return;
            switch (id)
            {
                // Bridge introduction is owned by checkpoint arrival, never by the earlier region trigger.
                case "bridge.enter": break;
                case "forest.enter": if (Scene.includeForest) Director.Enqueue("forest.intro"); break;
                case "jump.enter": RequestJumpIntroduction(); break;
                case "inn.approach": Director.Enqueue("inn.approach"); break;
                case "inn.enter":
                    pickupBarks.Clear();
                    if (UsesCollectionQuest) ((InnQuest)Scene.legacyInnQuest).TryTalk(Scene.player);
                    else Director.Enqueue("inn.intro");
                    break;
                case "jump.lower": RequestJumpDiscovery(); break;
                case "inn.exit": if (wallBroken && Scene.player.BreakEnabled) Director.Enqueue("inn.ending"); break;
                case "elevator.ending": if (source is ElevatorPlatform) Director.Enqueue("game.ending"); break;
                case "inventory.cardboard": nudgeAt = Mathf.Min(nudgeAt, Time.time + 10f); break;
            }
        }
        private void TickVillage()
        {
            if (forcedRun) TryCompleteOpeningFall();
            if (!Director.Active && VillageNeedsGlueReview)
            {
                // Tape can be attached before the stone finishes settling or before its final adjustment.
                // Recheck the actual connection and surface every frame, not just on the click that attached it.
                if (HasClearedVillagePit()) { ResetVillageGlueReview(); DismissGuidance(); return; }
                InspectVillageGlue();
                var collider = Scene.villageStone.GetComponent<Collider2D>();
                if (!Director.HasScheduled("village.depart") && !villageSunk && manipulation && !manipulation.IsHolding(Scene.villageStone) && !manipulation.HasWorldTapeConnection(Scene.villageStone) && Scene.villageStone.position.x >= Scene.villagePit.xMin && Scene.villageStone.position.x <= Scene.villagePit.xMax && collider.bounds.max.y < Scene.villageGroundY - 0.1f)
                { villageSunk = true; Director.Enqueue("village.sunk"); }
                if (Time.time >= nudgeAt && !HeadVisible && pendingBubble == null)
                {
                    nudgeAt = float.PositiveInfinity;
                    string nudge = villageSunk ? "stone_attach_hint" : "stone_drag_hint";
                    if (shownVillageNudges.Add(nudge)) PlayBark(nudge, guidance: true);
                }
            }
        }
        private bool VillageNeedsGlueReview => Scene && Scene.villageStone && Director.HasCompleted("village.reunion") &&
            !Director.HasScheduled("village.depart") && !Director.HasScheduled("bridge.intro") && !pendingBridgeIntroduction;
        private bool HasClearedVillagePit() => capsule && capsule.bounds.min.x > Scene.villagePit.xMax + .05f &&
            capsule.bounds.min.y >= Scene.villageGroundY - Scene.player.StepHeight - .1f;
        private void ResetVillageGlueReview()
        { villageGlueReviewPending = false; villageGlueStableFor = 0f; villageGlueReviewAt = nextVillageProbe = 0f; }
        private void QueueVillageGlueReview(Component subject)
        {
            if (!VillageNeedsGlueReview || !manipulation) return;
            var stone = Scene.villageStone;
            if (subject && subject.transform != stone.transform && !manipulation.HasWorldTapeConnection(stone)) return;
            villageGlueReviewPending = true; villageGlueStableFor = 0f;
            villageGlueReviewAt = Time.time + .4f; nextVillageProbe = 0f;
        }
        private void InspectVillageGlue()
        {
            if (!VillageNeedsGlueReview || !manipulation) return;
            var stone = Scene.villageStone;
            var surface = stone.GetComponent<Collider2D>();
            if (!surface || !surface.enabled || !manipulation.HasWorldTapeConnection(stone))
            { ResetVillageGlueReview(); return; }
            if (manipulation.IsHolding(stone) || stone.linearVelocity.sqrMagnitude > .04f || Mathf.Abs(stone.angularVelocity) > 8f)
            { villageGlueStableFor = 0f; return; }
            villageGlueStableFor += Time.deltaTime;
            if (villageGlueStableFor < .2f || Time.time < villageGlueReviewAt || Time.time < nextVillageProbe) return;
            nextVillageProbe = Time.time + .15f;
            bool passable = Scene.player.CanWalkAcrossForPresentation(Scene.villagePit, Scene.villageGroundY);
            if (passable)
            {
                ResetVillageGlueReview(); DismissGuidance();
                if (!villageSunk) Director.Enqueue("village.clever");
                Director.Enqueue("village.depart");
            }
            else if (villageGlueReviewPending && !Director.Active && !Director.HasPendingEpisodes)
            {
                villageGlueReviewPending = false;
                // A conservative query cannot prove failure: offer one optional trial hint, never a verdict.
                PlayBark("stone_wrong", guidance: true);
            }
        }
        private bool IsBelowVillagePitEntrance()
        {
            // Pit depth is authored by the terrain collider, never by the old rectangle bottom.
            return Scene && body && body.position.x >= Scene.villagePit.xMin &&
                body.position.x <= Scene.villagePit.xMax && body.position.y < Scene.villageGroundY - .25f;
        }
        private void TryCompleteOpeningFall()
        {
            if (!V4Mode || !forcedRun) return;
            if (!openingPitFallStarted)
            {
                if (!IsBelowVillagePitEntrance()) return;
                openingPitFallStarted = true;
                Scene.player.BeginOpeningPitFall();
            }
            if (!openingPitImpactStarted)
            {
                if (!Scene.player.HasLandedForPresentation) return;
                openingPitImpactStarted = true;
                openingPitImpactRemaining = Scene.player.PlayOpeningPitLandingImpact();
                if (library.pitImpactClip && audioRouter) audioRouter.PlaySfx(library.pitImpactClip);
                return;
            }
            openingPitImpactRemaining -= PresentationDelta;
            if (openingPitImpactRemaining > 0f) return;
            forcedRun = false;
            storyView.SetResetHint(resetTutorialUnlocked);
            Director.Signal("village.fell");
        }
        private void CancelOpeningRun()
        {
            forcedRun = openingPitFallStarted = openingPitImpactStarted = false;
            openingPitImpactRemaining = 0f;
            if (Scene && Scene.player) Scene.player.CancelOpeningPitAnimation();
        }
        internal void HoldActor(bool hold)
        {
            RefreshVisibility();
            if (!Scene || !Scene.player) return;
            if (hold && TooltipManager.Instance) TooltipManager.Instance.Hide();
            if (hold && !actorHeld) { Scene.player.StopForPresentation(); Scene.player.BeginConversation(); actorHeld = true; }
            else if (!hold && actorHeld) { Scene.player.EndConversation(); actorHeld = false; }
        }
        // 在剧情结束/取消的当帧归还悬浮提示，防止鼠标已停在道具上却收不到新的移入事件。
        internal void RefreshVisibility()
        {
            bool live = CanPresent;
            TooltipManager.SetSuppressed(this, live && Director.Active);
            storyView.SetPaused(!live);
            inventoryTray.SetActive(live && !Director.IsEnding && CheckpointResetAvailable);
            if (gameFlow) gameFlow.SetOpeningHudVisible(CheckpointResetAvailable);
        }
        internal void Command(string command)
        {
            switch (command)
            {
                case "opening.emerge":
                    HoldActor(false);
                    if (!PlayerOpeningSequence.BeginFromStory(Scene.player)) Director.Signal("opening.arrived");
                    break;
                case "opening.run": CancelOpeningRun(); HoldActor(false); forcedRun = true; Scene.player.RunForPresentation(1); break;
                case "opening.show_reset_hint": storyView.SetResetHint(true); break;
                case "opening.reset_only": resetTutorialUnlocked = true; HoldActor(false); Director.AllowResetOnly(true); storyView.SetResetHint(true); break;
                case "opening.reset_done": Director.AllowResetOnly(false); storyView.SetResetHint(false); HoldActor(true); break;
                case "inventory.unlock_cardboard": UnlockAbilityWithNotice(GameItemId.Cardboard); break;
                case "hint.stone": nudgeAt = Time.time + 15f; break;
            }
        }
        internal void PlayRoomCamera(string key)
        {
            if (!Scene || !Scene.player) return;
            if (string.IsNullOrWhiteSpace(key)) { Scene.player.EnterRoomCamera(Scene.entranceCamera, refreshSide: true); return; }
            var target = Scene.FindCameraTarget(key);
            if (target?.value) Scene.player.EnterNarrativeCamera(target.value, .8f);
            else Debug.LogError("剧情关卡镜头缺少目标：" + key, Scene);
        }
        public void PlayCamera(string keys, float hold = 1f)
        {
            if (!Scene || !Scene.levelCamera) return;
            cameraAnchor.Capture(Scene.levelCamera, Scene.player.transform);
            var shots = new List<CameraPerformanceManager.Shot>();
            foreach (string key in keys.Split(','))
            {
                var target = Scene.FindCameraTarget(key.Trim());
                if (target?.value) shots.Add(new CameraPerformanceManager.Shot
                {
                    target = target.value,
                    horizontalOnly = target.horizontalOnly,
                    orthographicSize = target.orthographicSize,
                    moveDuration = 0.8f,
                    holdDuration = Mathf.Max(0f, hold)
                });
                else Debug.LogWarning("请在剧情场景绑定器填写镜头目标：" + key, Scene);
            }
            if (shots.Count > 0) cameraPerformance.PlaySequence(shots);
        }
        internal void EpisodeFinished(string id)
        {
            if (id == "village.opening") Director.Enqueue("village.reunion");
            if (id == "village.reunion" || id == "village.sunk") nudgeAt = Time.time + 15f;
            string hint = id switch {
                "village.reunion" => "随时按空格起跑或叫停，按 R 回到火堆。试着用石块铺路吧！",
                "village.sunk" => "把石头提到和地面齐平，贴紧坑边点右键用硬纸板粘住！",
                "village.depart" => "按下space空格键！让吾动起来吧！",
                "bridge.intro" => "转动木板调好角度，用硬纸板把桥和岸边都粘稳吧！",
                "forest.intro" => "试着转动那些歪掉的树，给吾搭出台阶吧！",
                "jump.discover" => "把石块放在跷跷板翘起的一端作配重，压下石板后再前进拿跳跃键！",
                "inn.intro" when !flag_taught_midstop => "跑动时再按空格或点吾，就能让吾半路停下！",
                "inn.quest_done" => "帕洛梅克把好马抛下来了，去拿起驽骍难得吧！",
                "inn.shoes" => "对准右边的裂墙按空格冲锋，撞碎它！", _ => null };
            // Chapter-end guidance is shown once at low priority; facts and later chapters take precedence.
            if (hint != null && shownGuidance.Add(id)) ShowHead(hint, 7f, 0, guidance: true);
            lastInput = Time.unscaledTime;
        }
        private void OnCameraEnded(bool completed) { if (Scene && Scene.player) Scene.player.FinishPresentationCamera(); }
        /// <summary>只有开始全新周目时主动调用；R 重置和切换场景不清空全局教学。</summary>
        public void ResetTutorialProgress()
        { flag_taught_midstop = flag_taught_underfoot_lock = flag_taught_run_lock = false; onceBarks.Clear(); shownGuidance.Clear(); shownVillageNudges.Clear(); counts.Clear(); cooldowns.Clear(); }
        /// <summary>外部切换剧情/编辑器预览取消时，归还输入、重力和相机。</summary>
        public void CancelCurrentStory() { ResetVillageGlueReview(); CancelOpeningRun(); forcedRun = false; pendingBridgeIntroduction = pendingJumpIntroduction = pendingJumpDiscovery = false; Director.Clear(); }
    }
}
