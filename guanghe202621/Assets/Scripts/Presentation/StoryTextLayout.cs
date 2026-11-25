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
        private const int PageCharacterLimit = 64;
        private const string NoLineStart = "，。、．！？；：…‥）》〉】〕］｝」』〗”’％°!?.,:;)]}";
        private const string NoLineEnd = "（《〈【〔［｛「『〖“‘([{";
        private const string SentenceEnd = "。！？!?…";
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
                foreach (string part in SplitLongParagraph(paragraph.Trim()))
                {
                    var lines = WrapLines(part);
                    string page = string.Empty;
                    int lineCount = 0;
                    foreach (string line in lines)
                    {
                        string next = lineCount == 0 ? line : page + "\n" + line;
                        if (lineCount > 0 && (lineCount >= maxLines || MeasureHeight(next) > height))
                        {
                            pages.Add(page);
                            page = line;
                            lineCount = 1;
                        }
                        else
                        {
                            page = next;
                            lineCount++;
                        }
                    }
                    if (lineCount > 0) pages.Add(page);
                }
            }
            return pages;
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

        private static IEnumerable<string> SplitLongParagraph(string paragraph)
        {
            int[] characters = StringInfo.ParseCombiningCharacters(paragraph);
            for (int start = 0; start < characters.Length;)
            {
                int end = Mathf.Min(start + PageCharacterLimit, characters.Length);
                if (end < characters.Length)
                {
                    int best = -1;
                    for (int i = end; i > start + PageCharacterLimit / 2; i--)
                    {
                        char last = paragraph[characters[i - 1]];
                        if (SentenceEnd.IndexOf(last) >= 0 || last == '\n')
                        {
                            best = i;
                            break;
                        }
                        if (best < 0 && "，；：、,;: \t".IndexOf(last) >= 0) best = i;
                    }
                    if (best > start) end = best;
                    while (end < characters.Length && !CanBreakAt(paragraph, characters, end, false)) end++;
                }
                int offset = Offset(paragraph, characters, end);
                string part = paragraph.Substring(characters[start], offset - characters[start]).Trim();
                if (part.Length > 0) yield return part;
                start = SkipWhitespace(paragraph, characters, end);
            }
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
