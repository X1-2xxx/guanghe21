using System.Collections.Generic;
using System.Globalization;

namespace GamePresentation
{
    /// <summary>UGUI emits quads for visible glyphs, not for spaces and line breaks. Values are UTF-16 source offsets.</summary>
    public static class StoryGlyphMap
    {
        public static void FillSourceIndices(string text, bool richText, List<int> result)
        {
            result.Clear();
            if (string.IsNullOrEmpty(text)) return;
            for (int i = 0; i < text.Length; i++)
            {
                if (richText && text[i] == '<')
                {
                    int end = text.IndexOf('>', i + 1);
                    if (end >= 0)
                    {
                        string tag = text.Substring(i + 1, end - i - 1).Trim().ToLowerInvariant();
                        if (tag.StartsWith("quad ")) { result.Add(i); i = end; continue; }
                        if (tag == "b" || tag == "/b" || tag == "i" || tag == "/i" || tag == "/size" || tag == "/color" || tag == "/material" ||
                            tag.StartsWith("size=") || tag.StartsWith("color=") || tag.StartsWith("material=")) { i = end; continue; }
                    }
                }
                if (char.IsWhiteSpace(text[i]) || char.IsControl(text[i]) || char.GetUnicodeCategory(text[i]) == UnicodeCategory.Format) continue;
                result.Add(i);
            }
        }
    }
}
