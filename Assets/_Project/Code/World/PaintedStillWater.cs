using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The PAINTED still-water source (ADR 0046) — a plain POCO that decodes a region's <b>still-level
    /// map</b> once and answers <see cref="IStillWater.StillLevelAt"/>: the fresh-water surface standing
    /// above the tide at a plan point (a pond, a brook's fresh reach), or
    /// <see cref="StillWaterLevels.None"/> where there is none. <see cref="PaintedHeightMap"/> builds it from
    /// its optional still-level texture and <see cref="PaintedTidalTerrain"/> registers it into
    /// <see cref="GameServices.StillWater"/>, beside the terrain.
    ///
    /// <para><b>The map.</b> The R channel holds the still surface on the HEIGHT MAP's own scale — the same
    /// world rectangle and the same <c>min..max</c> range — at 16 bits; <b>code 0 is "no still water"</b>.
    /// It is DERIVED data (the terrain plan's ponds and brook steps, rendered by a deriver), never painted
    /// by hand and never saved (rule 5).</para>
    ///
    /// <para><b>Render == sim.</b> The water and tidal-face shaders sample the SAME texture (through
    /// <see cref="Map"/>) with the same world→uv mapping and bilinear filtering, THEN apply the same "0 is
    /// none" decode (<see cref="StillWaterLevels.DecodeCode01"/>). So this filters the normalized CODES —
    /// through <see cref="PaintedHeightField"/>'s shader-matching bilinear — and decodes the filtered value,
    /// never the other way round: filtering decoded levels would average a pond with −∞ at its rim. Off the
    /// rectangle there is no still water (the height map clamps to its edge; a pond must not).</para>
    ///
    /// <para><b>Performance (rule 7).</b> One decode into a flat <c>float[]</c> (4 bytes a texel: 6.3 MB for
    /// a 1520×1040 map), then four array reads and a lerp per query — never a texture read per call.</para>
    /// </summary>
    public sealed class PaintedStillWater : IStillWater
    {
        // The filtered NORMALIZED CODES (0..1, 0 = none), not metres — see the class doc for why.
        private readonly PaintedHeightField _codes;
        private readonly float _minLevel;
        private readonly float _maxLevel;
        private readonly StillWaterMap _map;

        /// <summary>
        /// Decode <paramref name="texture"/>'s R channel (at the texture's own precision,
        /// <see cref="PaintedHeightMap.ReadNormalizedR"/>) over the world rectangle given by its centre and
        /// size, on the <paramref name="minLevel"/>..<paramref name="maxLevel"/> scale. A null or
        /// non-readable texture reports no still water anywhere, with an unbound <see cref="Map"/> — the
        /// render must not draw what the sim cannot read.
        /// </summary>
        public PaintedStillWater(Texture2D texture, Vector2 worldCenter, Vector2 worldSize,
                                 float minLevel, float maxLevel)
            : this(texture, worldCenter, worldSize, minLevel, maxLevel, 0) { }

        /// <summary>
        /// As above, with the still SURFACE smoothed over <paramref name="smoothTexels"/> texels
        /// (<see cref="SmoothLevels"/>): a brook's water is laid texel by texel as its bed plus its depth, so
        /// its level jumps from one texel row to the next and its waterline jogs sideways at each jump. The
        /// smoothed codes are what both the sim and the render read: they live in a runtime copy of the map
        /// (<see cref="Map"/>), never in the committed asset. 0 reads the map as it is.
        /// </summary>
        public PaintedStillWater(Texture2D texture, Vector2 worldCenter, Vector2 worldSize,
                                 float minLevel, float maxLevel, int smoothTexels)
            : this(texture, worldCenter, worldSize, minLevel, maxLevel, smoothTexels, null) { }

        /// <summary>
        /// As above, and with the <paramref name="ground"/> handed in, every rim is sealed
        /// (<see cref="SealRims"/>): where the water still stands above the ground at its last texel, the
        /// texel past it takes its own ground as its level, so the waterline follows the ground instead of
        /// the map's texel edge. Null ground leaves the rims as the map has them.
        /// </summary>
        public PaintedStillWater(Texture2D texture, Vector2 worldCenter, Vector2 worldSize,
                                 float minLevel, float maxLevel, int smoothTexels, PaintedHeightField ground)
        {
            _minLevel = minLevel;
            _maxLevel = maxLevel;

            float[] codes = PaintedHeightMap.ReadNormalizedR(texture);
            if (codes == null) return;   // _codes null, _map default: no still water, nothing to draw

            int w = texture.width, h = texture.height;
            Texture drawn = texture;
            int changed = SmoothLevels(codes, w, h, smoothTexels);
            if (ground != null)
            {
                float cx = worldSize.x / w, cy = worldSize.y / h;
                Vector2 min = worldCenter - worldSize * 0.5f;
                System.Func<int, int, float> groundAt =
                    (x, y) => ground.ElevationAt(new Vector2(min.x + (x + 0.5f) * cx, min.y + (y + 0.5f) * cy));
                changed += SealRims(codes, w, h, groundAt, minLevel, maxLevel);
            }
            if (changed > 0)
            {
                _ownedTexture = BuildCodeTexture(codes, w, h, texture);
                drawn = _ownedTexture;
            }

            _codes = new PaintedHeightField(codes, w, h, worldCenter, worldSize);
            _map = new StillWaterMap(drawn, _codes.WorldMin, _codes.WorldSize, minLevel, maxLevel);
        }

        // The runtime copy of the map carrying the smoothed surface (null when nothing changed).
        private readonly Texture2D _ownedTexture;

        /// <summary>True when a readable still map was decoded.</summary>
        public bool IsBound => _codes != null;

        /// <summary>Free the runtime copy of the map, if this made one (a re-decode replaces it).</summary>
        public void Dispose()
        {
            if (_ownedTexture == null) return;
            if (Application.isPlaying) Object.Destroy(_ownedTexture);
            else Object.DestroyImmediate(_ownedTexture);
        }

        /// <summary>
        /// Smooth the still surface in place: every texel that holds still water takes the mean code of
        /// the still-water texels within <paramref name="radius"/> texels (a square window, read from the
        /// map as it was). Texels with no still water stay none and never vote, so no water is added and
        /// no pond is pulled toward −∞ at its rim. A flat pond stays exactly flat; a brook's stepped
        /// surface becomes a ramp, so its waterline follows the bank instead of jogging at every step.
        /// The decode is linear for codes above 0, so the mean code is the mean level.
        ///
        /// Returns how many texels changed. Pure and deterministic (a fixed summation order).
        /// </summary>
        public static int SmoothLevels(float[] codes, int width, int height, int radius)
        {
            if (codes == null || radius <= 0 || width <= 0 || height <= 0) return 0;
            float[] src = (float[])codes.Clone();
            int changed = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (!(src[i] > 0f)) continue;
                double sum = 0.0; int n = 0;
                for (int ny = Mathf.Max(0, y - radius); ny <= Mathf.Min(height - 1, y + radius); ny++)
                for (int nx = Mathf.Max(0, x - radius); nx <= Mathf.Min(width - 1, x + radius); nx++)
                {
                    float c = src[ny * width + nx];
                    if (c > 0f) { sum += c; n++; }
                }
                float mean = (float)(sum / n);
                if (mean != src[i]) { codes[i] = mean; changed++; }
            }
            return changed;
        }

        /// <summary>
        /// Seal the rims in place. A texel with no still water that touches still water (its 8 neighbours,
        /// read from the map as it was) takes the lower of the highest neighbouring level and its OWN
        /// ground. Where the ground stands at or above the water that is the usual dry support texel;
        /// where the water still stands above the ground at its last texel, the level is the ground's, so
        /// the depth reaches zero at this texel's centre and the drawn waterline runs along the ground
        /// rather than along the map's texel edge. Either way the texel's own depth is zero or less: no
        /// water is added (the ground's code is rounded down). Ground at or below the map's floor is left
        /// as none.
        ///
        /// Returns how many texels changed. Pure and deterministic.
        /// </summary>
        public static int SealRims(float[] codes, int width, int height, System.Func<int, int, float> groundAt,
                                   float minLevel, float maxLevel)
        {
            if (codes == null || groundAt == null || width <= 0 || height <= 0 || !(maxLevel > minLevel)) return 0;
            float count = SeabedBakeMath.CodeCountFor(TextureFormat.R16);
            float[] src = (float[])codes.Clone();
            int changed = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (src[i] > 0f) continue;
                float highest = 0f;
                for (int ny = Mathf.Max(0, y - 1); ny <= Mathf.Min(height - 1, y + 1); ny++)
                for (int nx = Mathf.Max(0, x - 1); nx <= Mathf.Min(width - 1, x + 1); nx++)
                    if (src[ny * width + nx] > highest) highest = src[ny * width + nx];
                if (!(highest > 0f)) continue;

                float g = groundAt(x, y);
                float groundCode = Mathf.Floor((g - minLevel) / (maxLevel - minLevel) * count) / count;
                float code = Mathf.Min(highest, groundCode);
                if (!(code > 0f)) continue;   // ground at the floor (or NaN): leave it none
                codes[i] = code;
                changed++;
            }
            return changed;
        }

        // The smoothed codes as an R16 map the shaders sample exactly as they sampled the asset (same
        // filter and wrap), with the CPU codes snapped to the 16-bit values the GPU will read.
        private static Texture2D BuildCodeTexture(float[] codes, int width, int height, Texture2D like)
        {
            float count = SeabedBakeMath.CodeCountFor(TextureFormat.R16);
            var raw = new ushort[codes.Length];
            for (int i = 0; i < codes.Length; i++)
            {
                int q = Mathf.Clamp(Mathf.RoundToInt(codes[i] * count), 0, (int)count);
                raw[i] = (ushort)q;
                codes[i] = q / count;
            }
            var tex = new Texture2D(width, height, TextureFormat.R16, false, true)
            {
                name = like.name + " (smoothed)",
                filterMode = like.filterMode,
                wrapMode = like.wrapMode,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixelData(raw, 0);
            tex.Apply(false, false);
            return tex;
        }

        /// <inheritdoc/>
        public StillWaterMap Map => _map;

        /// <inheritdoc/>
        public float StillLevelAt(Vector2 worldPos)
        {
            if (_codes == null) return StillWaterLevels.None;

            // Off the map's rectangle there is no still water. Written so a NaN position fails it too.
            Vector2 min = _codes.WorldMin, size = _codes.WorldSize;
            float u = (worldPos.x - min.x) / size.x;
            float v = (worldPos.y - min.y) / size.y;
            if (!(u >= 0f && u <= 1f && v >= 0f && v <= 1f)) return StillWaterLevels.None;

            return StillWaterLevels.DecodeCode01(_codes.ElevationAt(worldPos), _minLevel, _maxLevel);
        }
    }
}
