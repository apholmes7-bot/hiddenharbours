using Unity.Collections;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The hand-painted seabed/terrain <b>height map ASSET</b> (ADR 0014) — authored DATA (CLAUDE.md
    /// rule 2), one per region, that the owner paints with <c>App.Editor.TerrainPaintTool</c> and that
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
    /// <para><b>One height source, at the map's own precision (ADR 0046 §8).</b> The paint tool writes new
    /// maps as 16-bit (<c>R16</c>) PNGs imported as <c>R16</c>, so a map's step is its range / 65 535
    /// (0.17 mm over −4..+7 m) instead of range / 255 (4.3 cm). The decode reads whatever the texture holds
    /// at that texture's precision (<see cref="ReadNormalizedR"/>): the committed 8-bit maps decode exactly
    /// as they always have, and a 16-bit map decodes to its full 16 bits. The range is each map's own data
    /// — nothing global moved.</para>
    ///
    /// <para><b>Still water (ADR 0046).</b> A map may also carry an optional <b>still-level texture</b>: the
    /// fresh water standing above the tide, on this map's rectangle and range, 0 = none. It is decoded into
    /// a <see cref="PaintedStillWater"/> (<see cref="StillWater"/>) that <see cref="PaintedTidalTerrain"/>
    /// registers beside the terrain. No committed map carries one yet: without it there is no still water
    /// anywhere, and every region reads the tide alone, as before.</para>
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
                 "Terrain Paint Tool as an EXTERNAL .png next to this .asset (LFS-friendly, smart-mergeable). " +
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

        [Header("Still water above the tide (ADR 0046) — optional")]
        [Tooltip("OPTIONAL still-level texture: the fresh water standing above the tide (ponds, a brook's " +
                 "fresh reach). R = the water surface on THIS map's rectangle and Min..Max range, 0 = no still " +
                 "water. 16-bit (R16), CPU-readable, DERIVED from the terrain plan — never painted by hand. " +
                 "Empty = no still water anywhere.")]
        [SerializeField] private Texture2D _stillLevelTexture;

        public Texture2D HeightTexture => _heightTexture;
        public Vector2 WorldCenter => _worldCenter;
        public Vector2 WorldSize => _worldSize;
        public float MinElevation => _minElevation;
        public float MaxElevation => _maxElevation;
        /// <summary>The optional still-level texture (ADR 0046); null = no still water.</summary>
        public Texture2D StillLevelTexture => _stillLevelTexture;

        // Lazily-decoded cached field. Built once from the texture's R channel; invalidated by Rebuild().
        private PaintedHeightField _field;
        // Lazily-decoded still water, likewise. Null while there is no (readable) still-level texture.
        private PaintedStillWater _still;

        /// <summary>
        /// The decoded, cached <see cref="PaintedHeightField"/> the sim samples. Built on first access and
        /// reused; call <see cref="Rebuild"/> after the texture changes (the paint tool does). Null only
        /// when there is no readable texture (then the terrain reports "open water").
        /// </summary>
        public PaintedHeightField Field => _field ??= Decode();

        /// <summary>
        /// The decoded still water (ADR 0046), cached like <see cref="Field"/>. Null when this map carries no
        /// readable still-level texture — no still water anywhere, which is every committed map today.
        /// </summary>
        public PaintedStillWater StillWater => _still ??= DecodeStill();

        /// <summary>Force a re-decode of the cached field and still water (after a texture's pixels change).</summary>
        public void Rebuild()
        {
            _field = Decode();
            _still = DecodeStill();
        }

        /// <summary>
        /// A texture's R channel as normalized 0..1 values — row-major, row 0 at the BOTTOM (Unity's pixel
        /// order, and <see cref="PaintedHeightField"/>'s index) — at the texture's OWN precision. An
        /// <c>R16</c> texture reads its 16-bit codes exactly (<c>code / 65535</c>, the GPU's UNORM decode)
        /// through <see cref="Texture2D.GetPixelData{T}(int)"/>; every other format reads through
        /// <see cref="Texture2D.GetPixels()"/>, exactly as this decode always has. Null for a null or
        /// non-readable texture.
        /// </summary>
        public static float[] ReadNormalizedR(Texture2D texture)
        {
            if (texture == null || !texture.isReadable) return null;

            int n = texture.width * texture.height;
            var r01 = new float[n];
            if (texture.format == TextureFormat.R16)
            {
                float codes = SeabedBakeMath.CodeCountFor(TextureFormat.R16);
                NativeArray<ushort> raw = texture.GetPixelData<ushort>(0);   // a view, no copy
                for (int i = 0; i < n && i < raw.Length; i++) r01[i] = raw[i] / codes;
            }
            else
            {
                Color[] px = texture.GetPixels();   // row-major, y outer — matches PaintedHeightField
                for (int i = 0; i < n && i < px.Length; i++) r01[i] = px[i].r;
            }
            return r01;
        }

        /// <summary>
        /// Decode the texture's R channel into a <see cref="PaintedHeightField"/> of metres-above-datum.
        /// Returns null if there is no texture or it isn't CPU-readable (the sim then treats the region as
        /// open water rather than throwing). One allocation, once (<see cref="ReadNormalizedR"/>).
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
            float[] elev = ReadNormalizedR(_heightTexture);
            for (int i = 0; i < elev.Length; i++)
                elev[i] = PaintedHeightField.DecodeElevation(elev[i], _minElevation, _maxElevation);

            return new PaintedHeightField(elev, w, h, _worldCenter, _worldSize);
        }

        /// <summary>
        /// Decode the optional still-level texture into a <see cref="PaintedStillWater"/> on this map's
        /// rectangle and range. Null when there is none, or when it isn't CPU-readable (warned: a still map
        /// the sim cannot read must not be drawn either, so the region plays without it).
        /// </summary>
        private PaintedStillWater DecodeStill()
        {
            if (_stillLevelTexture == null) return null;
            if (!_stillLevelTexture.isReadable)
            {
                Debug.LogWarning($"[PaintedHeightMap] '{name}' still-level texture is not CPU-readable — the " +
                                 "sim cannot read its ponds. Re-derive/re-import so isReadable is set " +
                                 "(ADR 0046). Treating the region as having no still water for now.");
                return null;
            }

            return new PaintedStillWater(_stillLevelTexture, _worldCenter, _worldSize, _minElevation, _maxElevation);
        }
    }
}
