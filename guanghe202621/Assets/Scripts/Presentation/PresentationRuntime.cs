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
        [InspectorName("气泡使用打字机")] public bool useTypewriter;
        [InspectorName("每秒显示字数"), Min(1)] public float charactersPerSecond = 32f;
        [InspectorName("显示大立绘时隐藏场景角色外观")] public bool hideWorldActorWithPortrait = true;
        [InspectorName("全局教学：半路叫停"), SerializeField] public bool flag_taught_midstop;
        [InspectorName("全局教学：脚下物块不可拖"), SerializeField] public bool flag_taught_underfoot_lock;
        [InspectorName("全局教学：奔跑时不可编辑"), SerializeField] public bool flag_taught_run_lock;
        public StoryDirector Director { get; private set; }
        public PresentationSceneBinding Scene { get; private set; }
        public bool CanPresent => Scene && Scene.player && GameStateManager.Current == GameState.Playing && Time.timeScale > 0f;
        public bool HeadVisible => headRemaining > 0f;
        public string VisibleHeadText { get; private set; }
        public bool V4Mode => Scene && Scene.enableMainline;
        public bool BlocksRun => V4Mode && Director.HasCompleted("village.reunion") && !Director.HasCompleted("village.depart");

        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        private readonly Dictionary<string, float> cooldowns = new Dictionary<string, float>();
        private readonly HashSet<string> onceBarks = new HashSet<string>();
        private readonly Dictionary<Renderer, bool> hiddenActorRenderers = new Dictionary<Renderer, bool>();
        private readonly Queue<float> turns = new Queue<float>();
        private float headRemaining, headElapsed, lastInput, runStarted, nudgeAt;
        private int headPriority, shownFrame, quickStops, tapeCount;
        private bool headIsStory, began, forcedRun, pitHeld, villageSunk, openingReset, jumpAcquired, wallBroken;
        private float pitGravity;
        private int shownCharacters;
        private Rigidbody2D body;
        private CapsuleCollider2D capsule;
        private AudioManager audioRouter;
        private bool actorHeld;
        private float lastFall, stationarySeconds;
        private string currentHint;
        private float nextHint;
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
        private void OnDisable() { GameEventCenter.StateChanged -= OnStateChanged; TooltipManager.SetSuppressed(this, false); RestoreActorArt(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Attach(PresentationSceneBinding binding)
        {
            if (Scene == binding) return;
            if (!binding || !binding.player || !binding.levelCamera)
            { Debug.LogError("剧情场景绑定缺少玩家或相机，请在 Inspector 中拖入场景引用。", binding); return; }
            RestoreActorArt(); RestorePit(); Director.Clear(); Scene = binding;
            body = Scene.player.GetComponent<Rigidbody2D>(); capsule = Scene.player.GetComponent<CapsuleCollider2D>();
            audioRouter = FindAnyObjectByType<AudioManager>();
            began = forcedRun = pitHeld = villageSunk = openingReset = jumpAcquired = wallBroken = actorHeld = false;
            counts.Clear(); cooldowns.Clear(); turns.Clear(); tapeCount = quickStops = 0;
            previousPosition = body.position; stationarySeconds = 0f; lastInput = Time.unscaledTime;
            currentHint = null;
            // 其他关卡若已经带有镜头管理器，复用它；不因重复单例而丢失场景引用。
            if (!cameraPerformance) cameraPerformance = CameraPerformanceManager.Instance;
            if (!cameraPerformance) cameraPerformance = gameObject.AddComponent<CameraPerformanceManager>();
            cameraPerformance.PerformanceEnded -= OnCameraEnded;
            cameraPerformance.PerformanceEnded += OnCameraEnded;
            cameraPerformance.Configure(Scene.levelCamera, Scene.player.transform, cameraAnchor);
            cameraAnchor.Capture(Scene.levelCamera, Scene.player.transform);
            if (V4Mode)
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
            RestoreActorArt(); RestorePit(); Director.Clear(); Scene = null; body = null; capsule = null;
            inventoryTray.SetActive(false);
        }
        private void OnStateChanged(GameState state)
        {
            if (state == GameState.MainMenu) { RestorePit(); Director.Clear(); began = false; }
            if (state != GameState.Playing && !(state == GameState.Paused && headIsStory)) InterruptBubble();
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
            Director.Tick(Time.deltaTime);
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) Director.Advance();
            if (Director.Active || HadInput()) lastInput = Time.unscaledTime;
            if (!Director.Active && !HeadVisible && currentHint != null && Time.time >= nextHint)
            { ShowHead(currentHint, 5f, 15); nextHint = Time.time + 18f; }
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
            UpdateActorArt();
            if (!CanPresent || !Scene.player) return;
            // 触发行为的同一帧不会清掉新气泡；玩家之后的操作可立即打断小台词。
            if (!headIsStory && HeadVisible && Time.frameCount > shownFrame && HadInterruptInput()) InterruptBubble();
            if (!HeadVisible) { headView.SetVisible(false); return; }
            headRemaining -= Time.deltaTime; headElapsed += Time.deltaTime;
            if (headRemaining <= 0f) { InterruptBubble(); return; }
            Scene.player.HideLegacySpeechForPresentation();
            headView.SetVisible(true);
            Vector3 position = capsule ? new Vector3(capsule.bounds.center.x, capsule.bounds.max.y + 0.2f, 0f) : Scene.player.transform.position + Vector3.up * 2f;
            if (Scene.levelCamera && Scene.levelCamera.orthographic)
            {
                // 角色站在画面边缘时把气泡留在可见范围，兼容不同 Game 窗口宽高比。
                var view = Scene.levelCamera;
                float halfWidth = view.orthographicSize * view.aspect;
                float bubbleHalf = headView.Rect.rect.width * Mathf.Abs(headView.transform.lossyScale.x) * .5f;
                float height = headView.Rect.rect.height * Mathf.Abs(headView.transform.lossyScale.y);
                float left = view.transform.position.x - halfWidth + bubbleHalf + .08f;
                float right = view.transform.position.x + halfWidth - bubbleHalf - .08f;
                if (left <= right) position.x = Mathf.Clamp(position.x, left, right);
                position.y = Mathf.Min(position.y, view.transform.position.y + view.orthographicSize - height - .08f);
            }
            headView.transform.position = position;
            headView.transform.rotation = Quaternion.identity;
            if (useTypewriter)
            {
                int count = Mathf.FloorToInt(headElapsed * charactersPerSecond);
                if (shownCharacters != count) { shownCharacters = count; headView.Reveal(count); }
            }
        }
        private static bool HadInput() => HadInterruptInput() ||
            (Mouse.current != null && (Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f || Mouse.current.scroll.ReadValue().sqrMagnitude > 0f));
        private static bool HadInterruptInput() =>
            (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
            (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame ||
                Mouse.current.scroll.ReadValue().sqrMagnitude > 0f || (Mouse.current.leftButton.isPressed && Mouse.current.delta.ReadValue().sqrMagnitude > .1f)));

        /// <summary>供机关或关卡脚本调用；location 决定“同一位置”的降频计数。</summary>
        public bool PlayBark(string id, Component location = null)
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
            string text = lines[Random.Range(0, lines.Length)];
            float duration = count >= 3 && rule.brief != null && rule.brief.Length > 0 ? Mathf.Min(1.8f, rule.duration) : rule.duration;
            cooldowns[key] = Time.unscaledTime + (count >= 3 ? Mathf.Max(12f, rule.cooldown) : rule.cooldown);
            if (rule.once) onceBarks.Add(id);
            ShowHead(text, duration, rule.priority); return true;
        }
        private string LocationKey(Component source)
        {
            if (!source) return Scene ? Scene.gameObject.scene.path : "global";
            string path = source.name;
            for (Transform parent = source.transform.parent; parent; parent = parent.parent) path = parent.name + "/" + path;
            return source.gameObject.scene.path + "/" + path;
        }
        public void ShowHead(string text, float seconds = 3f, int priority = 10, bool story = false)
        {
            if (!Scene || !Scene.player || string.IsNullOrWhiteSpace(text)) return;
            VisibleHeadText = text; headRemaining = Mathf.Max(0.1f, seconds); headElapsed = 0f;
            headPriority = priority; headIsStory = story; shownFrame = Time.frameCount; shownCharacters = -1;
            headView.Initialize(Scene.levelCamera, text);
            headView.Reveal(useTypewriter ? 0 : headView.CharacterCount);
            headView.SetVisible(true);
        }
        public void InterruptBubble()
        { headRemaining = 0f; headPriority = 0; headIsStory = false; VisibleHeadText = string.Empty; headView.SetVisible(false); }

        // 大立绘只替代世界中的外观，保留碰撞、刚体、动画与角色控制组件。
        private void UpdateActorArt()
        {
            bool hide = hideWorldActorWithPortrait && CanPresent && Director.Active &&
                storyView.dialoguePanel.activeSelf && storyView.portrait.gameObject.activeSelf;
            if (!hide) { RestoreActorArt(); return; }
            if (hiddenActorRenderers.Count == 0)
                foreach (var renderer in Scene.player.GetComponentsInChildren<Renderer>(true)) hiddenActorRenderers[renderer] = renderer.enabled;
            foreach (var pair in hiddenActorRenderers) if (pair.Key) pair.Key.enabled = false;
        }
        private void RestoreActorArt()
        {
            foreach (var pair in hiddenActorRenderers) if (pair.Key) pair.Key.enabled = pair.Value;
            hiddenActorRenderers.Clear();
        }

        /// <summary>角色/机关的事实通知。普通小台词不会调用对话锁定和时间控制接口。</summary>
        public void Observe(PlayEvent kind, Component actor, Component subject, float value)
        {
            if (!Scene || (actor && actor.gameObject.scene != Scene.gameObject.scene) || !CanPresent) return;
            switch (kind)
            {
                case PlayEvent.RunStarted:
                    runStarted = Time.time; tapeCount = 0; currentHint = null; InterruptBubble();
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
                    RestorePit(); forcedRun = false; InterruptBubble();
                    if (Director.ResetOnly) { Director.AllowResetOnly(false); openingReset = true; Signal("village.reset", actor); }
                    else PlayBark("reset"); break;
                case PlayEvent.PitDeath:
                    PlayBark(V4Mode && Scene.villagePit.Contains(body.position) ? "village_fall" : "pit", subject); break;
                case PlayEvent.Jumped: PlayBark("first_jump"); break;
                case PlayEvent.Bounced:
                    if (V4Mode && !onceBarks.Contains("inn_bed_first")) PlayBark("inn_bed_first");
                    else PlayBark("bounce", subject); break;
                case PlayEvent.JumpAcquired:
                    jumpAcquired = true; ItemUnlockManager.Unlock(GameItemId.JumpKey);
                    if (V4Mode) Director.Enqueue("jump.acquired"); break;
                case PlayEvent.ShoesAcquired:
                    ItemUnlockManager.Unlock(GameItemId.Rocinante);
                    if (V4Mode) Director.Enqueue("inn.shoes"); break;
                case PlayEvent.WallBlocked:
                    if (forcedRun && Scene.villagePit.Contains(body.position)) CatchOpeningFall(Scene.player);
                    else if (body.position.x > 80f && !Scene.player.BreakEnabled) PlayBark("wall_no_shoes");
                    else PlayBark(V4Mode && Scene.villagePit.Contains(body.position) ? "village_blocked" : "blocked", subject); break;
                case PlayEvent.WallBroken:
                    wallBroken = true; PlayBark("wall_break", subject); break;
                case PlayEvent.TapeConnected:
                    tapeCount++; if (tapeCount >= 4) PlayBark("many_tapes");
                    if (V4Mode) InspectVillageGlue(subject); break;
                case PlayEvent.TapeRemoved: PlayBark("rebuild", subject); break;
                case PlayEvent.BlockGrabbed: InterruptBubble(); break;
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
        private void Checkpoint(Component source)
        {
            if (!V4Mode || Scene.checkpoints == null) return;
            int index = System.Array.FindIndex(Scene.checkpoints, c => c == source);
            if (index == 1 && openingReset) Director.Enqueue("village.crossed");
            if (index == 2) { Scene.player.StopForPresentation(); PlayBark("bridge_complete"); }
            if (index == 3) { Scene.player.StopForPresentation(); PlayBark("forest_complete"); }
        }
        public void Signal(string id, Component source = null)
        {
            if (!CanPresent) return;
            Director.Signal(id);
            if (!V4Mode) return;
            switch (id)
            {
                case "bridge.enter": Director.Enqueue("bridge.intro"); break;
                case "forest.enter": if (Scene.includeForest) Director.Enqueue("forest.intro"); break;
                case "jump.enter": Director.Enqueue("jump.intro"); break;
                case "inn.enter": Director.Enqueue("inn.intro"); break;
                case "jump.lower": if (!jumpAcquired) Director.Enqueue("jump.discover"); break;
                case "inn.exit": if (wallBroken && Scene.player.BreakEnabled) Director.Enqueue("inn.ending"); break;
                case "inventory.cardboard": nudgeAt = Mathf.Min(nudgeAt, Time.time + 10f); break;
            }
        }
        private void TickVillage()
        {
            if (forcedRun && body && Scene.villagePit.Contains(body.position) && body.position.y < Scene.villageGroundY - 0.25f)
                CatchOpeningFall(Scene.player);
            if (!Director.Active && Director.HasCompleted("village.reunion") && !Director.HasScheduled("village.depart") && Scene.villageStone)
            {
                var collider = Scene.villageStone.GetComponent<Collider2D>();
                if (!villageSunk && Scene.villagePit.Contains(Scene.villageStone.position) && collider.bounds.max.y < Scene.villageGroundY - 0.1f)
                { villageSunk = true; Director.Enqueue("village.sunk"); }
                if (Time.time >= nudgeAt)
                { nudgeAt = Time.time + 15f; PlayBark(villageSunk ? "stone_attach_hint" : "stone_drag_hint"); }
            }
        }
        private void InspectVillageGlue(Component subject)
        {
            if (!Scene.villageStone || Director.HasScheduled("village.depart") || !Director.HasCompleted("village.reunion")) return;
            var stone = Scene.villageStone;
            if (subject && subject.transform != stone.transform) return;
            var bounds = stone.GetComponent<Collider2D>().bounds;
            bool level = Mathf.Abs(bounds.max.y - Scene.villageGroundY) <= 0.12f &&
                Mathf.Abs(Mathf.DeltaAngle(stone.rotation, 0f)) <= 12f &&
                stone.position.x >= Scene.villagePit.xMin - 0.5f && stone.position.x <= Scene.villagePit.xMax + 0.5f;
            if (level) { if (!villageSunk) Director.Enqueue("village.clever"); Director.Enqueue("village.depart"); }
            else PlayBark("stone_wrong");
        }
        public bool CatchOpeningFall(Component actor)
        {
            if (!V4Mode || !forcedRun || pitHeld || actor != Scene.player || !body) return false;
            forcedRun = false; pitHeld = true; pitGravity = body.gravityScale;
            storyView.SetResetHint(true);
            Scene.player.StopForPresentation(); body.gravityScale = 0f; body.linearVelocity = Vector2.zero;
            body.position = new Vector2(Mathf.Clamp(body.position.x, Scene.villagePit.xMin + 0.55f, Scene.villagePit.xMax - 0.55f), Scene.villagePit.yMin + 0.1f);
            if (library.pitImpactClip && audioRouter) audioRouter.PlaySfx(library.pitImpactClip);
            Director.Signal("village.fell"); return true;
        }
        private void RestorePit()
        { if (pitHeld && body) body.gravityScale = pitGravity; pitHeld = false; }
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
            inventoryTray.SetActive(live);
        }
        internal void Command(string command)
        {
            switch (command)
            {
                case "opening.run": HoldActor(false); forcedRun = true; Scene.player.RunForPresentation(1); break;
                case "opening.reset_only": HoldActor(false); Director.AllowResetOnly(true); storyView.SetResetHint(true); break;
                case "opening.reset_done": Director.AllowResetOnly(false); storyView.SetResetHint(false); HoldActor(true); break;
                case "hint.stone": nudgeAt = Time.time + 15f; break;
            }
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
                "village.reunion" => "试着把石头拖到坑里！",
                "village.sunk" => "把石头提到和地面齐平，贴紧坑边点右键用硬纸板粘住！",
                "village.depart" => "按下space空格键！让吾动起来吧！",
                "bridge.intro" => "转动木板调好角度，用硬纸板把桥和岸边都粘稳吧！",
                "forest.intro" => "试着转动那些歪掉的树，给吾搭出台阶吧！",
                "jump.discover" => "用石头和木板搭个向上的缓坡，让吾去拿跳跃键！",
                "inn.intro" when !flag_taught_midstop => "跑动时再按空格或点吾，就能让吾半路停下！",
                "inn.shoes" => "往左走到老板头顶跳回一楼，去撞破右边的裂墙！", _ => null };
            if (hint != null) ShowHead(hint, 7f, 40);
            currentHint = hint; nextHint = Time.time + 18f; lastInput = Time.unscaledTime;
        }
        private void OnCameraEnded(bool completed) { if (Scene && Scene.player) Scene.player.FinishPresentationCamera(); }
        /// <summary>只有开始全新周目时主动调用；R 重置和切换场景不清空全局教学。</summary>
        public void ResetTutorialProgress()
        { flag_taught_midstop = flag_taught_underfoot_lock = flag_taught_run_lock = false; onceBarks.Clear(); counts.Clear(); cooldowns.Clear(); }
        /// <summary>外部切换剧情/编辑器预览取消时，归还输入、重力和相机。</summary>
        public void CancelCurrentStory() { RestorePit(); forcedRun = false; Director.Clear(); }
    }
}
