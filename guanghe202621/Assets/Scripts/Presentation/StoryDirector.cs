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
        public bool Active => episode != null;
        public string EpisodeId => episode?.id;
        public string StepId => step?.id;
        public bool ResetOnly { get; private set; }
        public bool CameraBusy => host.cameraPerformance && host.cameraPerformance.IsPerforming;
        public bool BlocksInput => (Active && !ResetOnly) || pending.Count > 0;
        public bool HasCompleted(string id) => completed.Contains(id);
        public bool HasScheduled(string id) => scheduled.Contains(id);
        public StoryDirector(PresentationRuntime runtime) { host = runtime; }

        public void Enqueue(string id)
        {
            var requested = host.library.FindEpisode(id);
            if (requested == null) { Debug.LogError("未配置剧情：" + id, host); return; }
            if ((requested.once && scheduled.Contains(id)) || EpisodeId == id || pending.Contains(id)) return;
            scheduled.Add(id); pending.Enqueue(id);
        }
        public void Tick(float delta)
        {
            if (!Active)
            {
                if (pending.Count == 0) return;
                episode = host.library.FindEpisode(pending.Dequeue());
                host.storyView.ResetDialogueMemory();
                host.InterruptBubble(); host.HoldActor(true);
                Enter(episode.entry); return;
            }
            if (deferredNext) { deferredNext = false; Enter(step.next); return; }
            if (step == null || Time.frameCount <= enteredFrame) return;
            if (step.kind == StoryStepKind.HeadBubble)
            {
                remaining -= delta;
                if (remaining <= 0f) Enter(step.next);
            }
            else if (step.kind == StoryStepKind.Camera && !CameraBusy) Enter(step.next);
            else if (step.kind == StoryStepKind.FadeOut)
            { remaining -= delta; host.storyView.FadeBlack(Mathf.Clamp01(remaining / Mathf.Max(0.1f, step.seconds))); if (remaining <= 0f) Enter(step.next); }
        }
        public void Advance()
        {
            if (!Active || step == null || Time.frameCount <= enteredFrame || !host.CanPresent) return;
            if (step.kind == StoryStepKind.Dialogue || step.kind == StoryStepKind.Narration || step.kind == StoryStepKind.BlackTitle)
                deferredNext = true;
        }
        public void Choose(int index)
        {
            if (!Active || step?.kind != StoryStepKind.Choice || Time.frameCount <= enteredFrame || !host.CanPresent) return;
            if (index < 0 || index >= step.choices.Count) return;
            Enter(step.choices[index].next);
        }
        public void Signal(string id)
        {
            if (Active) received.Add(id);
            if (step?.kind == StoryStepKind.WaitSignal && step.argument == id) deferredNext = true;
        }
        private void Enter(string id)
        {
            // 即时命令和条件节点允许连续执行；上限同时防止策划误配死循环。
            for (int guard = 0; guard < 64; guard++)
            {
                if (string.IsNullOrEmpty(id)) { Finish(); return; }
                step = episode.steps.Find(s => s.id == id);
                if (step == null) { Debug.LogError("剧情节点不存在：" + episode.id + "/" + id, host); Finish(); return; }
                enteredFrame = Time.frameCount; remaining = Mathf.Max(0.1f, step.seconds);
                host.storyView.Hide(); host.InterruptBubble();
                if (step.kind == StoryStepKind.BranchMidStop) { id = host.flag_taught_midstop ? step.alternate : step.next; continue; }
                if (step.kind == StoryStepKind.BranchEpisodeComplete) { id = HasCompleted(step.argument) ? step.alternate : step.next; continue; }
                if (step.kind == StoryStepKind.Command) { host.Command(step.argument); id = step.next; continue; }
                if (step.kind == StoryStepKind.WaitSignal)
                {
                    if (!string.IsNullOrEmpty(step.text)) host.ShowHead(step.text, step.seconds, 2000, true);
                    if (received.Contains(step.argument)) deferredNext = true;
                    return;
                }
                if (step.kind == StoryStepKind.FadeOut) { host.storyView.FadeBlack(1f); return; }
                if (step.kind == StoryStepKind.Camera) { host.PlayCamera(step.argument, step.seconds); return; }
                if (step.kind == StoryStepKind.HeadBubble)
                {
                    host.storyView.RememberDialogue(step.text, step.speaker, host.library.heroPortrait);
                    host.ShowHead(step.text, step.seconds, 2000, true); return;
                }
                if (step.kind == StoryStepKind.End) { host.storyView.ShowEnding(host.library.endingSprite); host.HoldActor(true); return; }
                host.HoldActor(true);
                host.storyView.Show(step, step.speaker == "旅馆老板" ? host.library.innkeeperPortrait : host.library.heroPortrait);
                return;
            }
            Debug.LogError("剧情即时节点形成循环，请检查下一节点。", host); Finish();
        }
        private void Finish()
        {
            string finished = episode.id; completed.Add(finished);
            episode = null; step = null; ResetOnly = false;
            received.Clear();
            host.storyView.Hide(); host.InterruptBubble(); host.HoldActor(false);
            host.EpisodeFinished(finished);
        }
        public void AllowResetOnly(bool value) { ResetOnly = value; }
        public void Clear()
        {
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
