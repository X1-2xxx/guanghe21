using System.Collections.Generic;
using UnityEngine;

namespace GamePresentation
{
    /// <summary>只移动现有面板，关闭或中断时归还原位置。</summary>
    internal sealed class StoryPanelTransition
    {
        sealed class Pose { public RectTransform Rect; public CanvasGroup Group; public Vector2 Rest; }
        readonly Dictionary<RectTransform, Pose> poses = new Dictionary<RectTransform, Pose>();
        Pose current;
        Vector2 offset;
        float elapsed, duration;
        public bool IsEntering { get; private set; }

        public void Begin(GameObject panel, float seconds, Vector2 fromOffset)
        {
            Reset();
            if (!panel || !(panel.transform is RectTransform rect)) return;
            if (!poses.TryGetValue(rect, out current))
            {
                var group = panel.GetComponent<CanvasGroup>();
                if (!group) group = panel.AddComponent<CanvasGroup>();
                current = new Pose { Rect = rect, Group = group, Rest = rect.anchoredPosition };
                poses.Add(rect, current);
            }
            elapsed = 0f; duration = Mathf.Max(0f, seconds); offset = fromOffset;
            IsEntering = duration > 0f;
            if (!IsEntering) { Complete(); return; }
            current.Rect.anchoredPosition = current.Rest + offset;
            current.Group.alpha = 0f; current.Group.interactable = current.Group.blocksRaycasts = false;
        }
        public void Tick(float delta)
        {
            if (!IsEntering || current == null || !current.Rect) return;
            elapsed += Mathf.Max(0f, delta);
            float t = Mathf.Clamp01(elapsed / duration), eased = 1f - Mathf.Pow(1f - t, 3f);
            current.Rect.anchoredPosition = current.Rest + offset * (1f - eased);
            current.Group.alpha = eased;
            if (t >= 1f) Complete();
        }
        public void Complete()
        {
            IsEntering = false;
            if (current == null || !current.Rect || !current.Group) return;
            current.Rect.anchoredPosition = current.Rest;
            current.Group.alpha = 1f; current.Group.interactable = current.Group.blocksRaycasts = true;
        }
        public void Reset() { Complete(); current = null; }
    }
}
