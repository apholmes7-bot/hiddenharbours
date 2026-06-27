using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The hand-painted seabed/terrain <b>height map ASSET</b> (ADR 0014) — authored DATA (CLAUDE.md
    /// rule 2), one per region, that the owner paints with <c>World.Editor.SeabedPaintTool</c> and that
    /// becomes BOTH the water render's depth source AND the tide sim's elevation source (the one-height-map
    /// / three-consumers invariant, ADR 0009/0010/0012). It wraps:
    /// <list type="bullet">
    /// <item><description>a <b>CPU-readable</b> <see cref="Texture2D"/> whose <b>R channel</b> encodes
    /// normalized elevation (0..1);</description></item>
    /// <item><description>the <b>world rectangle</b> it covers (<see cref="WorldCenter"/> /
    /// <see cref="WorldSize"/>) — the same frame the shader's <c>_HeightWorldMin/_HeightWorldSize</c> use;</description></item>
    /// <item><description>the <b>elevation range</b> (<see cref="MinElevation"/> / <see cref="MaxElevation"/>,
    /// m above chart datum) the R channel maps across — the same <c>_HeightMin/_HeightMax</c> the shader
    /// lerps.</description></item>
    /// </list>
    ///
    /// <para><b>Why CPU-readable + a cached float grid.</b> A GPU-only texture cannot be sampled by the sim
    /// (the known gotcha — then painted ≠ sailed). So the texture is imported <c>isReadable = true</c> and
    /// this asset <b>decodes it ONCE</b> into a cached <see cref="PaintedHeightField"/> (a flat
    /// <c>float[]</c> of metres-above-datum). <see cref="PaintedTidalTerrain"/> samples that field — never a
    /// <c>GetPixel</c> per <c>ElevationAt</c> call (rule 7). The render feeds the <b>same texture</b> to
    /// the shader (<see cref="HiddenHarbours.Art.WaterSurface"/>'s painted path), so render and sim read
    /// the same bytes — no second bake to drift.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> The map is authored data read at runtime, never written at
    /// runtime; the decoded field is a pure function of the painted bytes. Nothing here is saved to the
    /// game save — the tide is still recomputed from <c>(worldSeed, gameTime)</c>; only the terrain
    /// elevation SOURCE changed (analytic → painted), and both are deterministic.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "PaintedHeightMap", menuName = "Hidden Harbours/Painted Height Map", order = 60)]
    public sealed class PaintedHeightMap : ScriptableObject
    {
        [Header("Painted height texture (R = normalized elevation; MUST be CPU-readable)")]
        [Tooltip("The painted height texture. R channel 0..1 maps to Min..Max elevation. Authored by the " +
                 "Seabed Paint Tool as an EXTERNAL .png next to this .asset (LFS-friendly, smart-mergeable). " +
                 "Must be readable (isReadable) + linear so the sim can decode it — the paint tool sets that.")]
        [SerializeField] private Texture2D _heightTexture;

        [Header("World rectangle the map covers (same frame as the water shader)")]
        [Tooltip("World-space CENTRE of the rectangle the height map covers.")]
        [SerializeField] private Vector2 _worldCenter = new Vector2(0f, 0f);
        [Tooltip("World-space SIZE (width, height) of the covered rectangle. Should span the visible water.")]
        [SerializeField] private Vector2 _worldSize = new Vector2(160f, 120f);

        [Header("Elevation range the R channel maps across (m above chart datum)")]
        [Tooltip("Elevation (m above datum) the R=0 (black) end maps to. Set BELOW the deepest seabed AND " +
                 "the lowest tide so deep water never clips.")]
        [SerializeField] private float _minElevation = -4f;
        [Tooltip("Elevation (m above datum) the R=1 (white) end maps to. Set ABOVE the highest land so the " +
                 "island always stays dry.")]
        [SerializeField] private float _maxElevation = 6f;

        public Texture2D HeightTexture => _heightTexture;
        public Vector2 WorldCenter => _worldCenter;
        public Vector2 WorldSize => _worldSize;
        public float MinElevation => _minElevation;
        public float MaxElevation => _maxElevation;

        // Lazily-decoded cached field. Built once from the texture's R channel; invalidated by Rebuild().
        private PaintedHeightField _field;

        /// <summary>
        /// The decoded, cached <see cref="PaintedHeightField"/> the sim samples. Built on first access and
        /// reused; call <see cref="Rebuild"/> after the texture changes (the paint tool does). Null only
        /// when there is no readable texture (then the terrain reports "open water").
        /// </summary>
        public PaintedHeightField Field => _field ??= Decode();

        /// <summary>Force a re-decode of the cached field (after the texture's pixels change).</summary>
        public void Rebuild() => _field = Decode();

        /// <summary>
        /// Decode the texture's R channel into a <see cref="PaintedHeightField"/> of metres-above-datum.
        /// Returns null if there is no texture or it isn't CPU-readable (the sim then treats the region as
        /// open water rather than throwing). Uses <see cref="Texture2D.GetPixels"/> (one allocation, once).
        /// </summary>
        private PaintedHeightField Decode()
        {
            if (_heightTexture == null) return null;
            if (!_heightTexture.isReadable)
            {
                Debug.LogWarning($"[PaintedHeightMap] '{name}' height texture is not CPU-readable — the sim " +
                                 "cannot sample painted heights. Re-paint/re-export so isReadable is set " +
                                 "(ADR 0014). Treating region as open water for now.");
                return null;
            }

            int w = _heightTexture.width;
            int h = _heightTexture.height;
            Color[] px = _heightTexture.GetPixels();   // row-major, y outer — matches PaintedHeightField
            var elev = new float[w * h];
            for (int i = 0; i < elev.Length && i < px.Length; i++)
                elev[i] = PaintedHeightField.DecodeElevation(px[i].r, _minElevation, _maxElevation);

            return new PaintedHeightField(elev, w, h, _worldCenter, _worldSize);
        }
    }
}
