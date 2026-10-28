using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class MinimapPlayerArrow : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        void Arrow(float scale, Color tint)
        {
            int first = helper.currentVertCount;
            foreach (var p in new[] { new Vector2(0, 9), new Vector2(-6, -6), new Vector2(0, -3), new Vector2(6, -6) })
                helper.AddVert((Vector3)(p * scale), tint, Vector2.zero);
            helper.AddTriangle(first, first + 1, first + 2); helper.AddTriangle(first, first + 2, first + 3);
        }
        Arrow(1.3f, new Color(.035f, .04f, .03f)); Arrow(1, color);
    }
}
