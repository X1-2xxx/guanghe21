using UnityEngine;
using UnityEngine.UI;

namespace GamePresentation
{
    /// <summary>为旧版 UGUI 正文提供可在预制体上调整的字距。</summary>
    [RequireComponent(typeof(Text))]
    [DisallowMultipleComponent]
    public sealed class StoryDialogueLetterSpacing : BaseMeshEffect
    {
        [SerializeField, Min(0f), InspectorName("正文字距")] private float spacing = 1.5f;
        public float Spacing => isActiveAndEnabled ? spacing : 0f;

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || spacing <= 0f || graphic is not Text label) return;

            var lines = label.cachedTextGenerator.lines;
            int glyphCount = mesh.currentVertCount / 4;
            float alignment = Text.GetTextAnchorPivot(label.alignment).x;
            var vertex = new UIVertex();
            for (int line = 0; line < lines.Count; line++)
            {
                int start = Mathf.Min(lines[line].startCharIdx, glyphCount);
                int end = line + 1 < lines.Count ? Mathf.Min(lines[line + 1].startCharIdx, glyphCount) : glyphCount;
                int visibleEnd = end;
                while (visibleEnd > start && visibleEnd <= label.text.Length && char.IsWhiteSpace(label.text[visibleEnd - 1]))
                    visibleEnd--;
                for (int glyph = start; glyph < end; glyph++)
                {
                    float offset = (glyph - start - Mathf.Max(0, visibleEnd - start - 1) * alignment) * spacing;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        int index = glyph * 4 + corner;
                        mesh.PopulateUIVertex(ref vertex, index);
                        vertex.position.x += offset;
                        mesh.SetUIVertex(vertex, index);
                    }
                }
            }
        }
    }
}
