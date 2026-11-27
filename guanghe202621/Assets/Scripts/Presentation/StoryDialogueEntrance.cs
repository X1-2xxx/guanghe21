using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace GamePresentation
{
    /// <summary>Plays the artist's entrance once on demand; ordinary line/choice changes never restart it.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Animator), typeof(CanvasGroup))]
    public sealed class StoryDialogueEntrance : MonoBehaviour
    {
        [SerializeField, InspectorName("美术对白出场动画")] private AnimationClip entranceClip;
        private Animator animator;
        private CanvasGroup inputGroup;
        private PlayableGraph graph;
        private AnimationClipPlayable playable;
        private float elapsed;
        private readonly List<RectPose> rects = new List<RectPose>();
        private readonly List<(Graphic image, float alpha)> images = new List<(Graphic, float)>();
        private readonly List<(CanvasGroup group, float alpha)> groups = new List<(CanvasGroup, float)>();
        private bool savedInteractable, savedRaycasts;
        private sealed class RectPose
        {
            public RectTransform rect;
            public Vector2 anchored, size;
            public Vector3 scale;
            public Quaternion rotation;
        }
        public bool IsEntering { get; private set; }
        public bool IsConfigured => entranceClip;

        public void Begin()
        {
            Complete();
            if (!entranceClip) return;
            animator = GetComponent<Animator>(); inputGroup = GetComponent<CanvasGroup>();
            rects.Clear(); images.Clear(); groups.Clear();
            foreach (var rect in GetComponentsInChildren<RectTransform>(true))
                rects.Add(new RectPose { rect = rect, anchored = rect.anchoredPosition, size = rect.sizeDelta,
                    scale = rect.localScale, rotation = rect.localRotation });
            foreach (var image in GetComponentsInChildren<Graphic>(true)) images.Add((image, image.color.a));
            foreach (var group in GetComponentsInChildren<CanvasGroup>(true)) groups.Add((group, group.alpha));
            savedInteractable = inputGroup.interactable; savedRaycasts = inputGroup.blocksRaycasts;
            inputGroup.interactable = inputGroup.blocksRaycasts = false;
            elapsed = 0f; IsEntering = true; animator.enabled = true;
            graph = PlayableGraph.Create("Story dialogue entrance");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimationClipPlayable.Create(graph, entranceClip);
            playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
            var output = AnimationPlayableOutput.Create(graph, "Paper and portraits", animator);
            output.SetSourcePlayable(playable);
            graph.Play(); playable.SetTime(0d); graph.Evaluate(0f);
        }

        public void Tick(float delta)
        {
            if (!IsEntering || !graph.IsValid()) return;
            elapsed = Mathf.Min(entranceClip.length, elapsed + Mathf.Max(0f, delta));
            playable.SetTime(elapsed); graph.Evaluate(0f);
            if (elapsed >= entranceClip.length) Complete();
        }

        public void Complete()
        {
            if (graph.IsValid()) graph.Destroy();
            if (animator) animator.enabled = false;
            if (!IsEntering) return;
            IsEntering = false;
            foreach (var pose in rects) if (pose.rect)
            { pose.rect.anchoredPosition = pose.anchored; pose.rect.sizeDelta = pose.size; pose.rect.localScale = pose.scale; pose.rect.localRotation = pose.rotation; }
            foreach (var entry in images) if (entry.image)
            { var color = entry.image.color; color.a = entry.alpha; entry.image.color = color; }
            foreach (var entry in groups) if (entry.group) entry.group.alpha = entry.alpha;
            if (inputGroup) { inputGroup.interactable = savedInteractable; inputGroup.blocksRaycasts = savedRaycasts; }
            rects.Clear(); images.Clear(); groups.Clear();
        }
        private void OnDisable() => Complete();
        private void OnDestroy() => Complete();
    }
}
