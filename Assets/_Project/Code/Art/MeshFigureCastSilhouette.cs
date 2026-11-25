using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The mesh figure's own outline, as a sprite a shadow can cast.</b> The player (and a mesh NPC)
    /// is drawn as a posed mesh while the old body sprite stays <c>enabled</c> and is hidden with
    /// <c>forceRenderingOff</c>. The sun shadow (<see cref="SpriteShadow"/>) and the see-through-foliage
    /// outline (<see cref="SilhouetteMaskSource"/>) both trace a SPRITE, so they kept tracing the hidden
    /// standing sprite whatever the mesh was doing: digging, rowing, sitting. This traces the mesh
    /// instead.
    ///
    /// <para><b>How.</b> The posed vertices are projected exactly as the facet pass projects them
    /// (screen xy = the figure transform's world xy; the camera looks along z), into the CASTER's local
    /// space, and the triangles are filled into a small CPU coverage buffer at the caster's own PPU, so
    /// the outline lands on the art's pixel grid. The buffer is uploaded into ONE texture backing ONE
    /// sprite, both made once; a re-trace rewrites the texels, never the sprite, so the shadow and the
    /// mask keep the same sprite reference and pay nothing to follow it.</para>
    ///
    /// <para><b>Cost (rule 7).</b> No allocation after construction. A trace runs only when the pose was
    /// re-skinned (<see cref="IsoCharacterFigureRenderer.PoseVersion"/>) or the figure moved relative to
    /// the caster by more than <see cref="RetraceTolerance"/>; walking straight or standing still never
    /// re-traces. Projection is one matrix multiply per vertex, the fill is bounded by each triangle's
    /// few-pixel box, and the upload is <see cref="Width"/> x <see cref="Height"/> texels.</para>
    ///
    /// <para><b>What it does not carry.</b> Held props (a rod, a pail, a fish) are separate renderers and
    /// are not in the outline, as they never were. Below a waterline the mesh is clipped by its shader
    /// but not here; the shadow is stood down while swimming anyway.</para>
    /// </summary>
    public sealed class MeshFigureCastSilhouette : System.IDisposable
    {
        /// <summary>Outline window, in caster pixels: wide enough for a figure seated at the oars with
        /// his legs out, tall enough for a raised arm, and under the ground-contact pool's height gate
        /// (a figure must not grow a tree's pool because its window is tall).</summary>
        public const int Width = 80, Height = 88;

        /// <summary>The caster's feet (its transform origin) within the window, in pixels: centred, with
        /// room below the feet for a foot or a knee drawn in front of them.</summary>
        public const int PivotX = Width / 2, PivotY = 16;

        /// <summary>Caster-local metres the figure may move relative to the caster before the outline is
        /// traced again, about a quarter of an art pixel.</summary>
        public const float RetraceTolerance = 1f / 128f;

        private readonly Texture2D _texture;
        private readonly Color32[] _pixels = new Color32[Width * Height];
        private Vector2[] _projected;
        private readonly float _ppu;
        private int _tracedVersion = int.MinValue;
        private Matrix4x4 _tracedMatrix;
        private IsoCharacterFigureRenderer _tracedFigure;

        /// <summary>The outline sprite. Same reference for the life of this object.</summary>
        public Sprite Sprite { get; }

        /// <summary>How many traces have run (tests and the look probe's cost line).</summary>
        public int TraceCount { get; private set; }

        public MeshFigureCastSilhouette(float pixelsPerUnit)
        {
            _ppu = pixelsPerUnit > 0f ? pixelsPerUnit : 32f;
            _texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "MeshFigureCastSilhouette",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);
            Sprite = Sprite.Create(_texture, new Rect(0f, 0f, Width, Height), new Vector2((float)PivotX / Width, (float)PivotY / Height),
                                   _ppu, 0, SpriteMeshType.FullRect);
            Sprite.name = "MeshFigureCastSilhouette";
            Sprite.hideFlags = HideFlags.DontSave;
        }

        /// <summary>
        /// Trace <paramref name="figure"/>'s drawn pose into the sprite, in <paramref name="caster"/>'s
        /// local space. Returns false (and leaves the sprite alone) when the figure has no pose to read.
        /// </summary>
        public bool Refresh(IsoCharacterFigureRenderer figure, Transform caster)
        {
            if (figure == null || caster == null || figure.FacetTransform == null) return false;
            if (!figure.TryGetPosedGeometry(out Vector3[] verts, out int[] tris)) return false;

            Matrix4x4 m = caster.worldToLocalMatrix * figure.FacetTransform.localToWorldMatrix;
            if (figure == _tracedFigure && figure.PoseVersion == _tracedVersion && SameXY(m, _tracedMatrix, RetraceTolerance))
                return CoveredPixels > 0;

            if (_projected == null || _projected.Length < verts.Length) _projected = new Vector2[verts.Length];
            Project(verts, m, _ppu, PivotX, PivotY, _projected);
            System.Array.Clear(_pixels, 0, _pixels.Length);
            CoveredPixels = Fill(_projected, tris, _pixels, Width, Height);
            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);

            _tracedFigure = figure;
            _tracedVersion = figure.PoseVersion;
            _tracedMatrix = m;
            TraceCount++;
            // A figure drawn nowhere near its body (aboard, the body can be left a metre or two off the
            // seat) traces an empty window. An empty outline would erase the shadow, so the body's own
            // sprite keeps casting instead, as it did before.
            return CoveredPixels > 0;
        }

        /// <summary>Pixels the last trace covered (0 before the first).</summary>
        public int CoveredPixels { get; private set; }

        /// <summary>Whether two figure-to-caster maps put every point within <paramref name="tolerance"/>
        /// of each other in xy (the only axes the outline uses), for a figure about a metre across.</summary>
        public static bool SameXY(in Matrix4x4 a, in Matrix4x4 b, float tolerance)
        {
            return Mathf.Abs(a.m03 - b.m03) <= tolerance && Mathf.Abs(a.m13 - b.m13) <= tolerance
                && Mathf.Abs(a.m00 - b.m00) <= tolerance && Mathf.Abs(a.m01 - b.m01) <= tolerance
                && Mathf.Abs(a.m02 - b.m02) <= tolerance && Mathf.Abs(a.m10 - b.m10) <= tolerance
                && Mathf.Abs(a.m11 - b.m11) <= tolerance && Mathf.Abs(a.m12 - b.m12) <= tolerance;
        }

        /// <summary>Every vertex through <paramref name="toCasterLocal"/>, to window pixels. Pure.</summary>
        public static void Project(Vector3[] verts, in Matrix4x4 toCasterLocal, float ppu, int pivotX, int pivotY, Vector2[] into)
        {
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = toCasterLocal.MultiplyPoint3x4(verts[i]);
                into[i] = new Vector2(p.x * ppu + pivotX, p.y * ppu + pivotY);
            }
        }

        /// <summary>
        /// Fill the triangles into an opaque-white coverage buffer (<c>width x height</c>, row 0 at the
        /// bottom), testing each pixel's CENTRE, either winding. Pixels outside the buffer are dropped.
        /// Pure and allocation-free, so EditMode holds the outline to the mesh exactly. Returns how many
        /// pixels were newly covered.
        /// </summary>
        public static int Fill(Vector2[] points, int[] triangles, Color32[] pixels, int width, int height)
        {
            var on = new Color32(255, 255, 255, 255);
            int covered = 0;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                Vector2 a = points[triangles[t]], b = points[triangles[t + 1]], c = points[triangles[t + 2]];
                float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                if (Mathf.Abs(area) < 1e-6f) continue;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
                int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
                int y1 = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
                float s = area > 0f ? 1f : -1f;
                for (int y = y0; y <= y1; y++)
                {
                    float py = y + 0.5f;
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = x + 0.5f;
                        float w0 = ((b.x - a.x) * (py - a.y) - (b.y - a.y) * (px - a.x)) * s;
                        float w1 = ((c.x - b.x) * (py - b.y) - (c.y - b.y) * (px - b.x)) * s;
                        float w2 = ((a.x - c.x) * (py - c.y) - (a.y - c.y) * (px - c.x)) * s;
                        if (w0 >= 0f && w1 >= 0f && w2 >= 0f && pixels[y * width + x].a == 0)
                        {
                            pixels[y * width + x] = on;
                            covered++;
                        }
                    }
                }
            }
            return covered;
        }

        public void Dispose()
        {
            Kill(Sprite);
            Kill(_texture);
        }

        private static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
