using UnityEngine;
using UnityEngine.UI;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// A one-axis colour ramp across a uGUI graphic — the shell's only shading (M1 §7.8).
    ///
    /// <para><b>Why a vertex effect and not a texture.</b> The title's wash has to be heavy where the type
    /// is and nearly gone where the harbour is, which is a gradient; a gradient is normally a PNG. A PNG
    /// would be an art asset to author, an import setting to get right, LFS to carry it and texture memory
    /// to spend (rule 7) — for four numbers. This tints the quad's own corners instead: no texture, no
    /// allocation, and it re-runs only when the rect changes, which for a page built once is never.</para>
    ///
    /// <para>It <b>multiplies</b> into whatever colour the graphic already has rather than replacing it, so
    /// the <see cref="Image"/>'s own tint (and a <see cref="CanvasGroup"/>'s fade) still mean what they
    /// mean. Position-derived rather than vertex-index-derived: a quad's corner order is uGUI's business,
    /// and reading each vertex's own place in the rect is correct whatever it decides to emit.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShellGradient : BaseMeshEffect
    {
        [SerializeField] private Color _near = Color.white;   // at the low end of the axis (left / bottom)
        [SerializeField] private Color _far  = Color.white;   // at the high end (right / top)
        [SerializeField] private bool _horizontal = true;

        /// <summary>Set the ramp and repaint. <paramref name="horizontal"/> runs it left→right; otherwise
        /// bottom→top.</summary>
        public void Set(Color near, Color far, bool horizontal)
        {
            _near = near;
            _far = far;
            _horizontal = horizontal;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh == null || graphic == null) return;

            Rect rect = graphic.rectTransform.rect;
            float min = _horizontal ? rect.xMin : rect.yMin;
            float max = _horizontal ? rect.xMax : rect.yMax;
            if (max - min <= Mathf.Epsilon) return;   // a zero-width rect has no axis to ramp along

            var vertex = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float t = Mathf.InverseLerp(min, max,
                                            _horizontal ? vertex.position.x : vertex.position.y);
                vertex.color = (Color)vertex.color * Color.Lerp(_near, _far, t);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
