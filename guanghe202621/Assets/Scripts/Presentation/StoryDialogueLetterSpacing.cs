using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GamePresentation
{
    /// <summary>Adjusts actual glyph quads using their source character columns, including whitespace advances.</summary>
    [RequireComponent(typeof(Text)), DisallowMultipleComponent]
    public sealed class StoryDialogueLetterSpacing : BaseMeshEffect
    {
        [SerializeField, Min(0f), InspectorName("正文字距")] private float spacing = 1.5f;
        public float Spacing => isActiveAndEnabled ? spacing : 0f;
        private readonly List<int> sourceIndices = new List<int>();

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || spacing <= 0f || graphic is not Text label) return;
            StoryGlyphMap.FillSourceIndices(label.text, label.supportRichText, sourceIndices);
            if (sourceIndices.Count == 0) return;
            var lines = label.cachedTextGenerator.lines;
            if (lines.Count == 0) return;
            int glyphCount = mesh.currentVertCount / 4;
            float alignment = Text.GetTextAnchorPivot(label.alignment).x;
            var vertex = new UIVertex();
            for (int glyph = 0; glyph < glyphCount; glyph++)
            {
                // Mesh effects such as Shadow can repeat a full copy of the original glyph stream.
                int source = sourceIndices[glyph % sourceIndices.Count];
                int line = 0;
                while (line + 1 < lines.Count && source >= lines[line + 1].startCharIdx) line++;
                int start = lines[line].startCharIdx;
                int end = line + 1 < lines.Count ? lines[line + 1].startCharIdx : label.text.Length;
                while (end > start && char.IsWhiteSpace(label.text[end - 1])) end--;
                float offset = (source - start - Mathf.Max(0, end - start - 1) * alignment) * spacing;
                for (int corner = 0; corner < 4; corner++)
                {
                    int index = glyph * 4 + corner;
                    mesh.PopulateUIVertex(ref vertex, index);vertex.position.x += offset;mesh.SetUIVertex(vertex, index);
                }
            }
        }
    }
}
