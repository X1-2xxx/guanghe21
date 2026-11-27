using System.Collections.Generic;
using UnityEngine;

namespace GamePresentation
{
    /// <summary>数据驱动的剧情节点播放器；对白、选项、等待信号和镜头不混入角色控制器。</summary>
    public sealed class StoryDirector
    {
        private readonly PresentationRuntime host;
        private readonly Queue<string> pending = new Queue<string>();
        private readonly HashSet<string> completed = new HashSet<string>();
        private readonly HashSet<string> scheduled = new HashSet<string>();
        private readonly HashSet<string> received = new HashSet<string>();
        private StoryEpisode episode;
        private StoryStep step;
        private float remaining;
        private bool deferredNext;
        private int enteredFrame;
        private float checkpointPauseRemaining, savedWorldTimeScale = 1f;
        private string delayedEntry;
        private bool formalSegmentActive;
        private bool ownsWorldPause;
        public bool WorldPaused => ownsWorldPause;
        public bool Active => episode != null;
        public bool HasPendingEpisodes => pending.Count > 0;
        public bool IsEnding => Active && step?.kind == StoryStepKind.End;
        public string EpisodeId => episode?.id;
        public string StepId => step?.id;
        public bool ResetOnly { get; private set; }
        public bool CameraBusy => host.cameraPerformance && host.cameraPerformance.IsPerforming;
        public bool WaitingToEnter => checkpointPauseRemaining > 0f && delayedEntry != null;
        public bool BlocksInput => Active && !ResetOnly && !WaitingToEnter;
        public bool HasCompleted(string id) => completed.Contains(id);
        public bool HasScheduled(string id) => scheduled.Contains(id);
        public StoryDirector(PresentationRuntime runtime) { host = runtime; }

        public void Enqueue(string id)
        {
            var requested = host.library.FindEpisode(id);
            if (requested == null) { Debug.LogError("未配置剧情：" + id, host); return; }
            if ((requested.once && scheduled.Contains(id)) || EpisodeId == id || pending.Contains(id)) return;
            host.DismissGuidance();
            scheduled.Add(id); pending.Enqueue(id);
        }
        public void Tick(float delta)
        {
            if (ownsWorldPause && GameStateManager.Current == GameState.Playing) Time.timeScale = 0f;
            host.storyView.TickEntrance(delta);
            if (checkpointPauseRemaining > 0f)
            {
                checkpointPauseRemaining = Mathf.Max(0f, checkpointPauseRemaining - delta);
                if (checkpointPauseRemaining <= 0f && Active)
                {
                    var entry = delayedEntry; delayedEntry = null;
                    host.InterruptBubble(); host.HoldActor(true);
                    Enter(entry, true);
                }
                return;
            }
            if (host.storyView.IsEntering) return;
            if (!Active)
            {
                if (pending.Count == 0) return;
                episode = host.library.FindEpisode(pending.Dequeue());
                host.storyView.BeginEpisode(episode.showInnkeeper, host.library.ResolveHeroPortrait(StoryExpression.NeutralClosedMouth),
                    host.library.ResolveInnkeeperPortrait(InnkeeperExpression.Normal));
                // Detection starts a grace period; stop the actor only when the timer expires.
                host.HoldActor(false); SetWorldPaused(false);
                formalSegmentActive = false; delayedEntry = episode.entry;
                checkpointPauseRemaining = Mathf.Max(0f, host.CheckpointPauseSeconds);
                if (checkpointPauseRemaining <= 0f)
                {
                    delayedEntry = null; host.InterruptBubble(); host.HoldActor(true);
                    Enter(episode.entry, true);
                }
                return;
            }
            if (deferredNext) { deferredNext = false; Enter(step.next); return; }
            if (step == null || Time.frameCount <= enteredFrame) return;
            host.storyView.TickSubtitle(delta);
            if (IsTimedText(step))
            {
                if (host.storyView.IsPageRevealed && TimedTextFinished(delta)) Enter(step.next);
                return;
            }
            if (step.kind == StoryStepKind.HeadBubble)
            {
                if (!host.HeadVisible) Enter(step.next);
            }
            else if (step.kind == StoryStepKind.Camera && !CameraBusy) Enter(step.next);
            else if (step.kind == StoryStepKind.RoomEntranceCamera && !host.Scene.player.IsGameplayCameraMoving) Enter(step.next);
            else if (step.kind == StoryStepKind.FadeOut)
            { remaining -= delta; host.storyView.FadeBlack(Mathf.Clamp01(remaining / Mathf.Max(0.1f, step.seconds))); if (remaining <= 0f) Enter(step.next); }
        }
        public void Advance()
        {
            if (!Active || step == null || deferredNext || Time.frameCount <= enteredFrame || !host.CanPresent) return;
            if (step.kind == StoryStepKind.HeadBubble)
            {
                if (host.CompleteStoryHeadText()) enteredFrame = Time.frameCount;
                else deferredNext = true;
                return;
            }
            if (step.kind == StoryStepKind.WaitSignal && step.showHeadWhileWaiting)
            {
                // Reveal the scripted line quickly while its gameplay event remains authoritative.
                if (host.CompleteStoryHeadText()) enteredFrame = Time.frameCount;
                return;
            }
            bool text = step.kind == StoryStepKind.Dialogue || step.kind == StoryStepKind.Narration ||
                step.kind == StoryStepKind.BlackTitle || step.kind == StoryStepKind.SkySubtitle;
            if (!text) return; // Space never chooses an answer or bypasses gameplay-signal waits.
            if (host.storyView.IsEntering)
            {
                host.storyView.CompleteEntrance(); host.storyView.CompleteSubtitle();
                enteredFrame = Time.frameCount;
                if (IsTimedText(step)) remaining = ReadingTime();
                return;
            }
            if (host.storyView.CompleteSubtitle() || host.storyView.TryAdvanceTextPage())
            {
                enteredFrame = Time.frameCount;
                if (IsTimedText(step)) remaining = ReadingTime();
            }
            else deferredNext = true;
        }
        public void Choose(int index)
        {
            if (!Active || step?.kind != StoryStepKind.Choice || Time.frameCount <= enteredFrame || !host.CanPresent) return;
            if (index < 0 || index >= step.choices.Count) return;
            GameEventCenter.RequestSfx(GameplaySfx.DialogueOptionClick);
            Enter(step.choices[index].next);
        }
        public void Signal(string id)
        {
            if (Active) received.Add(id);
            if (step?.kind == StoryStepKind.WaitSignal && step.argument == id) deferredNext = true;
        }
        private void Enter(string id, bool skipDelay = false)
        {
            // 即时命令和条件节点允许连续执行；上限同时防止策划误配死循环。
            for (int guard = 0; guard < 64; guard++)
            {
                if (string.IsNullOrEmpty(id)) { Finish(); return; }
                step = episode.steps.Find(s => s.id == id);
                if (step == null) { Debug.LogError("剧情节点不存在：" + episode.id + "/" + id, host); Finish(); return; }
                bool formal = step.kind == StoryStepKind.Dialogue || step.kind == StoryStepKind.Choice;
                if (formal && !formalSegmentActive && !skipDelay && !ownsWorldPause && !host.Scene.player.IsInConversation && host.CheckpointPauseSeconds > 0f)
                {
                    delayedEntry = id; step = null; ResetOnly = false; formalSegmentActive = true;
                    checkpointPauseRemaining = host.CheckpointPauseSeconds;
                    host.storyView.Hide(); host.InterruptBubble();
                    host.HoldActor(false); SetWorldPaused(false);
                    return;
                }
                if (formal) formalSegmentActive = true;
                else if (step.kind == StoryStepKind.WaitSignal || step.kind == StoryStepKind.Camera ||
                    step.kind == StoryStepKind.RoomEntranceCamera || step.kind == StoryStepKind.FadeOut ||
                    (step.kind == StoryStepKind.Command && step.argument != null && step.argument.StartsWith("opening.")))
                    formalSegmentActive = false;
                enteredFrame = Time.frameCount; remaining = Mathf.Max(0.1f, step.seconds);
                bool visualText = step.kind == StoryStepKind.Dialogue || step.kind == StoryStepKind.Choice ||
                    step.kind == StoryStepKind.Narration || step.kind == StoryStepKind.SkySubtitle || step.kind == StoryStepKind.BlackTitle;
                bool instant = step.kind == StoryStepKind.Command || step.kind == StoryStepKind.BranchMidStop || step.kind == StoryStepKind.BranchEpisodeComplete;
                if (!visualText && !instant) host.storyView.Hide();
                if (!instant) SetWorldPaused(host.PauseWorldDuringDialogue &&
                    (visualText || step.kind == StoryStepKind.HeadBubble || step.kind == StoryStepKind.FadeOut || step.kind == StoryStepKind.End));
                host.InterruptBubble();
                // 空白对白不能变成需要玩家点击的空纸框；选项节点可以没有正文。
                if (step.kind == StoryStepKind.Dialogue && string.IsNullOrWhiteSpace(step.text)) { id = step.next; continue; }
                if (step.kind == StoryStepKind.BranchMidStop) { id = host.flag_taught_midstop ? step.alternate : step.next; continue; }
                if (step.kind == StoryStepKind.BranchEpisodeComplete) { id = HasCompleted(step.argument) ? step.alternate : step.next; continue; }
                if (step.kind == StoryStepKind.Command)
                {
                    if (step.argument != null && step.argument.StartsWith("opening.")) SetWorldPaused(false);
                    host.Command(step.argument); id = step.next; continue;
                }
                if (step.kind == StoryStepKind.WaitSignal)
                {
                    if (step.showHeadWhileWaiting)
                    {
                        host.storyView.ResetDialogueMemory();
                        host.ShowHead(step.text, step.seconds, 2000, true);
                    }
                    if (received.Contains(step.argument)) deferredNext = true;
                    return;
                }
                if (step.kind == StoryStepKind.FadeOut) { host.storyView.FadeBlack(1f); return; }
                if (step.kind == StoryStepKind.Camera) { host.PlayCamera(step.argument, step.seconds); return; }
                if (step.kind == StoryStepKind.RoomEntranceCamera)
                {
                    host.PlayRoomCamera(step.argument);
                    return;
                }
                if (step.kind == StoryStepKind.HeadBubble)
                {
                    host.storyView.ResetDialogueMemory();
                    host.ShowHead(step.text, step.seconds, 2000, true);
                    return;
                }
                if (step.kind == StoryStepKind.End)
                {
                    host.storyView.ClearEquipmentNotice(); host.storyView.SetResetHint(false);
                    host.storyView.ShowEnding(step.argument == "elevator" ? null : host.library.endingSprite,
                        string.IsNullOrWhiteSpace(step.text) ? "To Be Continued" : step.text);
                    host.HoldActor(true); return;
                }
                host.HoldActor(true);
                host.storyView.Show(step, host.library.ResolvePortrait(step));
                if (IsTimedText(step)) remaining = ReadingTime();
                return;
            }
            Debug.LogError("剧情即时节点形成循环，请检查下一节点。", host); Finish();
        }

        private static bool IsTimedText(StoryStep node) => node.kind == StoryStepKind.Narration || node.kind == StoryStepKind.SkySubtitle ||
            (node.kind == StoryStepKind.BlackTitle && node.autoAdvance);

        private float ReadingTime() => Mathf.Max(step.seconds, 0.65f + host.storyView.CurrentPageLength / 22f);

        private bool TimedTextFinished(float delta)
        {
            remaining -= delta;
            if (remaining > 0f) return false;
            if (!host.storyView.TryAdvanceTextPage()) return true;
            remaining = ReadingTime();
            return false;
        }

        private void Finish()
        {
            SetWorldPaused(false); checkpointPauseRemaining = 0f; delayedEntry = null; formalSegmentActive = false;
            string finished = episode.id; completed.Add(finished);
            episode = null; step = null; ResetOnly = false;
            received.Clear();
            host.storyView.Hide(); host.InterruptBubble(); host.HoldActor(false);
            host.EpisodeFinished(finished);
        }
        private void SetWorldPaused(bool paused)
        {
            if (paused)
            {
                if (!ownsWorldPause)
                {
                    savedWorldTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                    ownsWorldPause = true;
                }
                if (GameStateManager.Current == GameState.Playing) Time.timeScale = 0f;
                return;
            }
            if (ownsWorldPause && GameStateManager.Current == GameState.Playing && Mathf.Approximately(Time.timeScale, 0f))
                Time.timeScale = savedWorldTimeScale;
            ownsWorldPause = false;
        }
        public void AllowResetOnly(bool value) { ResetOnly = value; }
        public void Clear()
        {
            SetWorldPaused(false); checkpointPauseRemaining = 0f; delayedEntry = null; formalSegmentActive = false;
            episode = null; step = null; ResetOnly = false; deferredNext = false;
            pending.Clear(); completed.Clear(); scheduled.Clear();
            received.Clear();
            host.storyView.Hide(); host.storyView.SetResetHint(false); host.InterruptBubble();
            host.storyView.ResetDialogueMemory();
            host.HoldActor(false);
            if (host.cameraPerformance) host.cameraPerformance.StopPerformance();
        }
    }
}
