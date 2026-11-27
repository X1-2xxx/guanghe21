using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GamePresentation
{
    /// <summary>Reveals the existing layout by source character offsets; whitespace never consumes a glyph quad.</summary>
    [RequireComponent(typeof(Text)), DisallowMultipleComponent]
    public sealed class StoryTextReveal : BaseMeshEffect
    {
        private int visibleCharacters = int.MaxValue;
        private readonly List<int> sourceIndices = new List<int>();
        public void SetVisibleCharacters(int count)
        {
            if (visibleCharacters == count) return;
            visibleCharacters = Mathf.Max(0, count);graphic.SetVerticesDirty();
        }
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || visibleCharacters == int.MaxValue || graphic is not Text label) return;
            StoryGlyphMap.FillSourceIndices(label.text, label.supportRichText, sourceIndices);
            if (sourceIndices.Count == 0) return;
            var vertex = new UIVertex();
            for (int glyph = 0; glyph < mesh.currentVertCount / 4; glyph++)
            {
                if (sourceIndices[glyph % sourceIndices.Count] < visibleCharacters) continue;
                for (int corner = 0; corner < 4; corner++)
                {
                    int index = glyph * 4 + corner;mesh.PopulateUIVertex(ref vertex, index);vertex.color.a = 0;mesh.SetUIVertex(vertex, index);
                }
            }
        }
    }
}
