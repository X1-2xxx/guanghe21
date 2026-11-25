using UnityEngine;
using UnityEngine.UI;

namespace GamePresentation
{
    /// <summary>逐步显示已排好版的字幕，字数变化时纸框和换行保持稳定。</summary>
    [RequireComponent(typeof(Text))]
    [DisallowMultipleComponent]
    public sealed class StoryTextReveal : BaseMeshEffect
    {
        private int visibleCharacters = int.MaxValue;

        public void SetVisibleCharacters(int count)
        {
            if (visibleCharacters == count) return;
            visibleCharacters = count;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            var vertex = new UIVertex();
            for (int i = Mathf.Min(visibleCharacters, mesh.currentVertCount / 4) * 4; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                vertex.color.a = 0;
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
