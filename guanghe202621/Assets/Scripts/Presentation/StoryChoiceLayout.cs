using UnityEngine;
using UnityEngine.UI;

namespace GamePresentation
{
    /// <summary>在右侧选项区域内按文字高度调整按钮，短选项沿用预制体的排版。</summary>
    internal sealed class StoryChoiceLayout
    {
        private const float MinimumGap = 12f;
        private const float TextHeightMargin = 6f;
        private const float ExpandedTopInset = 16f;
        private readonly RectTransform panel;
        private readonly Row first, second;
        private Vector2 lastSize;

        public StoryChoiceLayout(RectTransform panel, Text firstLabel, Text secondLabel,
            RectTransform firstButton, RectTransform secondButton)
        {
            this.panel = panel;
            first = new Row(firstButton, firstLabel);
            second = new Row(secondButton, secondLabel);
        }

        public void RefreshIfNeeded()
        {
            if (panel.gameObject.activeInHierarchy && panel.rect.size != lastSize) Apply();
        }

        public void Apply(string firstText, string secondText)
        {
            first.Content = firstText;
            second.Content = secondText;
            Apply();
        }

        private void Apply()
        {
            lastSize = panel.rect.size;
            if (!first.Button.gameObject.activeSelf || lastSize.x <= 0f || lastSize.y <= 0f) return;
            first.Restore(panel);
            second.Restore(panel);

            bool hasSecond = second.Button.gameObject.activeSelf;
            first.Measure();
            if (hasSecond) second.Measure();
            if (first.FitsOriginal && (!hasSecond || second.FitsOriginal)) return;

            float bottom = hasSecond ? second.Bottom : panel.rect.yMin;
            float gap = hasSecond ? Mathf.Clamp(first.Bottom - second.Top, 0f, MinimumGap) : 0f;
            float available = Mathf.Max(first.Top, panel.rect.yMax) - bottom - gap;
            int fontLimit = Mathf.Max(first.FontSize, second.FontSize);
            float firstHeight = first.RequiredHeight(fontLimit);
            float secondHeight = hasSecond ? second.RequiredHeight(fontLimit) : 0f;

            // 优先增高纸框。两框加起来超过上限时，仅缩小需要扩容的选项。
            while (firstHeight + secondHeight > available && fontLimit > 1)
            {
                fontLimit--;
                firstHeight = first.RequiredHeight(fontLimit);
                secondHeight = hasSecond ? second.RequiredHeight(fontLimit) : 0f;
            }

            float firstTop = Mathf.Max(first.Top, bottom + firstHeight + secondHeight + gap);
            first.Place(panel, firstTop - firstHeight, firstTop, fontLimit);
            if (hasSecond) second.Place(panel, bottom, bottom + secondHeight, fontLimit);
        }

        private sealed class Row
        {
            public string Content { get; set; }
            public RectTransform Button { get; }
            public float Bottom { get; private set; }
            public float Top { get; private set; }
            public int FontSize { get; }
            public bool FitsOriginal { get; private set; }

            private readonly Text label;
            private readonly int originalFontSize;
            private readonly bool bestFit;
            private readonly float buttonOffsetMin, buttonOffsetMax;
            private readonly float labelAnchorMin, labelAnchorMax, labelOffsetMin, labelOffsetMax;
            private float bottomPadding, topPadding;
            private float OriginalHeight => Top - Bottom;

            public Row(RectTransform button, Text label)
            {
                Button = button;
                this.label = label;
                bestFit = label.resizeTextForBestFit;
                originalFontSize = label.fontSize;
                FontSize = bestFit ? label.resizeTextMaxSize : originalFontSize;
                buttonOffsetMin = button.offsetMin.y;
                buttonOffsetMax = button.offsetMax.y;
                var labelRect = label.rectTransform;
                labelAnchorMin = labelRect.anchorMin.y;
                labelAnchorMax = labelRect.anchorMax.y;
                labelOffsetMin = labelRect.offsetMin.y;
                labelOffsetMax = labelRect.offsetMax.y;
            }

            public void Restore(RectTransform panel)
            {
                var rect = panel.rect;
                Bottom = rect.yMin + rect.height * Button.anchorMin.y + buttonOffsetMin;
                Top = rect.yMin + rect.height * Button.anchorMax.y + buttonOffsetMax;
                SetOffsets(Button, buttonOffsetMin, buttonOffsetMax);
                var labelRect = label.rectTransform;
                SetAnchors(labelRect, labelAnchorMin, labelAnchorMax);
                SetOffsets(labelRect, labelOffsetMin, labelOffsetMax);
                label.fontSize = originalFontSize;
                label.resizeTextForBestFit = bestFit;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            public void Measure()
            {
                bottomPadding = Mathf.Max(0f, OriginalHeight * labelAnchorMin + labelOffsetMin);
                // 长文首行留出纸框花边与手写字体的上沿。
                topPadding = Mathf.Max(0f, OriginalHeight * (1f - labelAnchorMax) - labelOffsetMax) + ExpandedTopInset;
                FitsOriginal = TextHeight(FontSize, out string formatted) + TextHeightMargin + ExpandedTopInset <= label.rectTransform.rect.height;
                if (FitsOriginal)
                {
                    label.resizeTextForBestFit = false;
                    label.fontSize = FontSize;
                    label.text = formatted;
                }
            }

            public float RequiredHeight(int fontLimit) => FitsOriginal
                ? OriginalHeight
                : Mathf.Max(OriginalHeight, Mathf.Ceil(TextHeight(Mathf.Min(FontSize, fontLimit), out _) + TextHeightMargin + bottomPadding + topPadding));

            public void Place(RectTransform panel, float bottom, float top, int fontLimit)
            {
                var parentRect = panel.rect;
                SetOffsets(Button, bottom - parentRect.yMin - parentRect.height * Button.anchorMin.y,
                    top - parentRect.yMin - parentRect.height * Button.anchorMax.y);
                if (FitsOriginal) return;

                // 拉高时固定纸框内部留白，不让留白跟着变大。
                var labelRect = label.rectTransform;
                SetAnchors(labelRect, 0f, 1f);
                SetOffsets(labelRect, bottomPadding, -topPadding);
                label.resizeTextForBestFit = false;
                label.fontSize = Mathf.Min(FontSize, fontLimit);
                label.text = new StoryTextLayout(label, label.fontSize).Wrap(Content);
            }

            private float TextHeight(int size, out string formatted)
            {
                var layout = new StoryTextLayout(label, size);
                formatted = layout.Wrap(Content);
                return layout.MeasureHeight(formatted);
            }

            private static void SetOffsets(RectTransform rect, float min, float max)
            {
                rect.offsetMin = new Vector2(rect.offsetMin.x, min);
                rect.offsetMax = new Vector2(rect.offsetMax.x, max);
            }

            private static void SetAnchors(RectTransform rect, float min, float max)
            {
                rect.anchorMin = new Vector2(rect.anchorMin.x, min);
                rect.anchorMax = new Vector2(rect.anchorMax.x, max);
            }
        }
    }
}
