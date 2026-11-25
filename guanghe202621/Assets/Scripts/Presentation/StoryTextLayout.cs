using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace GamePresentation
{
    /// <summary>按实际字体和文本区域排版，兼顾中文标点和剧本中的手动分行。</summary>
    internal sealed class StoryTextLayout
    {
        // 只约束被迫拆开的长句；剧本原有的短句（例如“好快！”）不受这个下限影响。
        private const int MinimumFragmentCharacters = 6;
        private const string NoLineStart = "，。、．！？；：…‥）》〉】〕］｝」』〗”’％°!?.,:;)]}";
        private const string NoLineEnd = "（《〈【〔［｛「『〖“‘([{";
        private const string SentenceEnd = "。！？!?";
        private const string ClosingPunctuation = "）》〉】〕］｝」』〗”’)]}";
        private static readonly Regex ParagraphBreak = new Regex(@"\n[ \t]*\n(?:[ \t]*\n)*");
        private static readonly Regex StrandedPunctuation =
            new Regex(@"(?<=\S)[ \t]*\n(?:[ \t]*\n)*[ \t]*(?=[" + Regex.Escape(NoLineStart).Replace("]", "\\]") + "])");

        private readonly TextGenerator generator = new TextGenerator();
        private readonly TextGenerationSettings settings;
        private readonly float width;
        private readonly float characterSpacing;

        public StoryTextLayout(Text label, int fontSize, float characterSpacing = 0f)
        {
            width = Mathf.Max(1f, label.rectTransform.rect.width - 2f);
            settings = label.GetGenerationSettings(new Vector2(width, 0f));
            settings.resizeTextForBestFit = false;
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            if (label.font && label.font.dynamic) settings.fontSize = fontSize;
            this.characterSpacing = characterSpacing;
        }

        public float MeasureHeight(string text) => !settings.font || string.IsNullOrEmpty(text)
            ? 0f : generator.GetPreferredHeight(text, settings) / Mathf.Max(0.01f, settings.scaleFactor);

        public string Wrap(string text) => string.Join("\n", WrapLines(Normalize(text)));

        public List<string> Paginate(string text, float height, int maxLines)
        {
            var pages = new List<string>();
            foreach (string paragraph in ParagraphBreak.Split(Normalize(text)))
            {
                string page = string.Empty;
                foreach (string sentence in SplitSentences(paragraph.Trim()))
                {
                    // 能放下的完整句子一起排版；放不下时整句留到下一页。
                    foreach (string part in SplitLongSentence(sentence, height, maxLines))
                    {
                        string next = page + part;
                        if (page.Length > 0 && !FitsPage(next.Trim(), height, maxLines))
                        {
                            pages.Add(Wrap(page.Trim()));
                            page = part.TrimStart();
                        }
                        else page = next;
                    }
                }
                // 空行是剧本明确的分段要求，不把两个段落重新挤回同一张卡片。
                if (!string.IsNullOrWhiteSpace(page)) pages.Add(Wrap(page.Trim()));
            }
            return pages;
        }

        private bool FitsPage(string text, float height, int maxLines)
        {
            var lines = WrapLines(text);
            return lines.Count <= Mathf.Max(1, maxLines) &&
                MeasureHeight(string.Join("\n", lines)) <= Mathf.Max(1f, height);
        }

        private List<string> WrapLines(string text)
        {
            var lines = new List<string>();
            foreach (string segment in text.Split('\n'))
            {
                string line = segment.Trim();
                if (line.Length == 0)
                {
                    lines.Add(string.Empty);
                    continue;
                }

                int[] characters = StringInfo.ParseCombiningCharacters(line);
                for (int start = 0; start < characters.Length;)
                {
                    int end = FittingEnd(line, characters, start);
                    if (end < characters.Length)
                    {
                        int safeEnd = end;
                        while (safeEnd > start && !CanBreakAt(line, characters, safeEnd, false)) safeEnd--;
                        if (safeEnd > start) end = safeEnd;
                        else
                        {
                            // 窄框兜底。优先让一整个标点组跟着前面的字。
                            while (end < characters.Length && !CanBreakAt(line, characters, end, true)) end++;
                        }
                    }

                    int offset = Offset(line, characters, end);
                    lines.Add(line.Substring(characters[start], offset - characters[start]).TrimEnd());
                    start = SkipWhitespace(line, characters, end);
                }
            }
            return lines;
        }

        private int FittingEnd(string line, int[] characters, int start)
        {
            if (!settings.font) return characters.Length;
            int low = start + 1;
            int high = characters.Length;
            while (low < high)
            {
                int test = low + (high - low + 1) / 2;
                string candidate = line.Substring(characters[start], Offset(line, characters, test) - characters[start]).TrimEnd();
                float advance = generator.GetPreferredWidth(candidate, settings) / Mathf.Max(0.01f, settings.scaleFactor);
                if (advance + Mathf.Max(0, candidate.Length - 1) * characterSpacing <= width) low = test;
                else high = test - 1;
            }
            return low;
        }

        private static IEnumerable<string> SplitSentences(string paragraph)
        {
            int start = 0;
            for (int i = 0; i < paragraph.Length; i++)
            {
                char c = paragraph[i];
                // 省略号是语气停顿，不能把开头的“……”切成一张空有标点的卡片。
                bool period = c == '.' && (i == 0 || paragraph[i - 1] != '.') &&
                    (i + 1 == paragraph.Length || (paragraph[i + 1] != '.' && !char.IsDigit(paragraph[i + 1])));
                if (SentenceEnd.IndexOf(c) < 0 && !period) continue;
                int end = i + 1;
                while (end < paragraph.Length &&
                    (SentenceEnd.IndexOf(paragraph[end]) >= 0 || ClosingPunctuation.IndexOf(paragraph[end]) >= 0 ||
                     paragraph[end] == '…' || paragraph[end] == '.')) end++;
                yield return paragraph.Substring(start, end - start);
                start = end;
                i = end - 1;
            }
            if (start < paragraph.Length) yield return paragraph.Substring(start);
        }

        private IEnumerable<string> SplitLongSentence(string sentence, float height, int maxLines)
        {
            if (FitsPage(sentence.Trim(), height, maxLines))
            {
                yield return sentence;
                yield break;
            }

            string leadingWhitespace = sentence.Substring(0, sentence.Length - sentence.TrimStart().Length);
            string remaining = sentence.Trim();
            bool firstPart = true;
            while (remaining.Length > 0)
            {
                if (FitsPage(remaining, height, maxLines))
                {
                    yield return firstPart ? leadingWhitespace + remaining : remaining;
                    yield break;
                }

                int[] characters = StringInfo.ParseCombiningCharacters(remaining);
                int fittingEnd = FittingPageEnd(remaining, characters, height, maxLines);
                int minimumTail = Mathf.Min(MinimumFragmentCharacters, Mathf.Max(1, MeaningfulLength(remaining) / 2));
                // 先在逗号、分号和手动换行处找自然停顿，避免下一页只剩一两个尾字。
                int end = NaturalPageBreak(remaining, characters, fittingEnd, minimumTail, false);
                if (end == 0) end = NaturalPageBreak(remaining, characters, fittingEnd, minimumTail, true);
                if (end == 0)
                {
                    end = fittingEnd;
                    if (MeaningfulLength(remaining.Substring(Offset(remaining, characters, end))) < minimumTail)
                        end = Mathf.Min(end, Mathf.Max(1, characters.Length / 2));
                    int safeEnd = end;
                    while (safeEnd > 0 && !CanBreakAt(remaining, characters, safeEnd, false)) safeEnd--;
                    if (safeEnd > 0) end = safeEnd;
                    // 超长英文词无合法词界时，在可容纳的位置拆开，避免逐字各占一页。
                    else while (end < characters.Length && !CanBreakAt(remaining, characters, end, true)) end++;
                }

                int offset = Offset(remaining, characters, end);
                string part = remaining.Substring(0, offset).Trim();
                if (part.Length > 0)
                {
                    yield return firstPart ? leadingWhitespace + part : part;
                    firstPart = false;
                }
                remaining = remaining.Substring(offset).TrimStart();
            }
        }

        private int FittingPageEnd(string text, int[] characters, float height, int maxLines)
        {
            int low = 1, high = characters.Length;
            while (low < high)
            {
                int end = low + (high - low + 1) / 2;
                if (FitsPage(text.Substring(0, Offset(text, characters, end)).TrimEnd(), height, maxLines)) low = end;
                else high = end - 1;
            }
            return low;
        }

        private static int NaturalPageBreak(string text, int[] characters, int limit, int minimumTail, bool secondary)
        {
            for (int end = Mathf.Min(limit, characters.Length - 1); end > 0; end--)
            {
                char last = text[characters[end - 1]];
                bool boundary = secondary ? last == '、' || last == ' ' || last == '\t' : "，；：,;:\n".IndexOf(last) >= 0;
                if (!boundary || !CanBreakAt(text, characters, end, false)) continue;
                int offset = Offset(text, characters, end);
                if (MeaningfulLength(text.Substring(0, offset)) < minimumTail ||
                    MeaningfulLength(text.Substring(offset)) < minimumTail) continue;
                return end;
            }
            return 0;
        }

        private static int MeaningfulLength(string text)
        {
            int count = 0;
            foreach (int index in StringInfo.ParseCombiningCharacters(text))
                if (!char.IsWhiteSpace(text[index]) && !char.IsPunctuation(text[index])) count++;
            return count;
        }

        private static string Normalize(string text) =>
            StrandedPunctuation.Replace((text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n'), string.Empty);

        private static int Offset(string text, int[] characters, int index) =>
            index < characters.Length ? characters[index] : text.Length;

        private static int SkipWhitespace(string text, int[] characters, int index)
        {
            while (index < characters.Length && char.IsWhiteSpace(text[characters[index]])) index++;
            return index;
        }

        private static bool CanBreakAt(string text, int[] characters, int index, bool allowInsideWord)
        {
            int next = SkipWhitespace(text, characters, index);
            if (next == characters.Length) return true;
            int previous = index - 1;
            while (previous > 0 && char.IsWhiteSpace(text[characters[previous]])) previous--;
            char before = text[characters[previous]];
            char after = text[characters[next]];
            if (NoLineEnd.IndexOf(before) >= 0 || NoLineStart.IndexOf(after) >= 0) return false;
            return allowInsideWord || next != index || previous != index - 1 || !IsLatinWord(before) || !IsLatinWord(after);
        }

        private static bool IsLatinWord(char c) =>
            (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
    }
}
