using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The PURE bilinear sampler at the heart of painted seabed authoring (ADR 0014) — a plain POCO (no
    /// <see cref="MonoBehaviour"/>, no <see cref="ScriptableObject"/>, no scene) that owns a decoded
    /// elevation grid and answers <see cref="ElevationAt"/> in <b>metres above chart datum</b>. It is what
    /// makes the painted height map a true <c>ITidalTerrain</c> source: the sim
    /// (<see cref="PaintedTidalTerrain"/>) samples THIS, and the water render samples the same painted
    /// texture, so the visible depth and the gameplay depth come from the <b>same interpolation</b> — they
    /// cannot diverge (the P1 integrity rule, ADR 0009/0010/0012).
    ///
    /// <para><b>World↔texel mapping (matches the shader EXACTLY).</b> The shader's <c>SeabedElevation</c>
    /// maps a world position to UV as <c>uv = (worldXY − worldMin) / worldSize</c> and reads
    /// <c>lerp(min, max, R)</c> with bilinear filtering. This field uses the identical mapping +
    /// bilinear interpolation over the decoded float grid, so what the shader draws and what the sim reads
    /// agree at every position (ADR 0014 §2). Out-of-rect positions <b>clamp to the edge texel</b> (a boat
    /// far offshore reads the edge depth rather than throwing).</para>
    ///
    /// <para><b>Determinism (rule 5).</b> A pure function of (decoded grid, position): no RNG, nothing
    /// saved at runtime. The grid is authored DATA decoded once from the painted texture; same position →
    /// same elevation forever. Combined with the deterministic water level this composes into the same
    /// exposure/depth reads <see cref="HiddenHarbours.Core.TidalExposure"/> already defines.</para>
    ///
    /// <para><b>Performance (rule 7).</b> Holds the elevations as a flat <c>float[]</c> (row-major, y
    /// outer) so <see cref="ElevationAt"/> is four array reads + a lerp — never a per-call texture read.
    /// <see cref="PaintedHeightMap"/> decodes the texture into one of these once on enable / on rebuild.</para>
    /// </summary>
    public sealed class PaintedHeightField
    {
        private readonly float[] _elev;     // metres above datum, row-major (index = y*res + x)
        private readonly int _width;
        private readonly int _height;
        private readonly Vector2 _worldMin; // bottom-left corner of the covered rect (world units)
        private readonly Vector2 _worldSize;// width/height of the covered rect (world units)

        /// <summary>Grid resolution (columns).</summary>
        public int Width => _width;
        /// <summary>Grid resolution (rows).</summary>
        public int Height => _height;
        /// <summary>Bottom-left corner of the covered world rectangle.</summary>
        public Vector2 WorldMin => _worldMin;
        /// <summary>Size of the covered world rectangle (world units).</summary>
        public Vector2 WorldSize => _worldSize;

        /// <summary>
        /// Build a field from a decoded elevation grid (metres above datum, row-major y-outer) over a world
        /// rectangle given by its centre + size. The same centre/size frame the shader's
        /// <c>_HeightWorldMin/_HeightWorldSize</c> use (this stores the derived bottom-left min).
        /// </summary>
        public PaintedHeightField(float[] elevations, int width, int height, Vector2 worldCenter, Vector2 worldSize)
        {
            _width = Mathf.Max(1, width);
            _height = Mathf.Max(1, height);
            _elev = elevations != null && elevations.Length >= _width * _height
                ? elevations
                : new float[_width * _height];
            _worldSize = new Vector2(Mathf.Max(worldSize.x, 1e-3f), Mathf.Max(worldSize.y, 1e-3f));
            _worldMin = worldCenter - _worldSize * 0.5f;
        }

        /// <summary>
        /// Authored ground/seabed elevation at <paramref name="worldPos"/>, metres above chart datum
        /// (higher = drier). Bilinear over the decoded grid, with the shader's exact world→uv mapping;
        /// out-of-rect positions clamp to the edge. Deterministic — a pure function of the position.
        /// </summary>
        public float ElevationAt(Vector2 worldPos)
        {
            // World → normalized UV (the shader's mapping), then UV → fractional texel coordinate. Texel
            // centres sit at (i + 0.5)/res in UV (matching how the bake samples cell centres), so the
            // texel-space coordinate is uv*res − 0.5.
            float u = (worldPos.x - _worldMin.x) / _worldSize.x;
            float v = (worldPos.y - _worldMin.y) / _worldSize.y;

            float fx = u * _width - 0.5f;
            float fy = v * _height - 0.5f;

            // Integer corner + fractional weight, clamped so out-of-rect samples read the edge texel.
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;

            int x0c = Mathf.Clamp(x0, 0, _width - 1);
            int x1c = Mathf.Clamp(x0 + 1, 0, _width - 1);
            int y0c = Mathf.Clamp(y0, 0, _height - 1);
            int y1c = Mathf.Clamp(y0 + 1, 0, _height - 1);

            float e00 = _elev[y0c * _width + x0c];
            float e10 = _elev[y0c * _width + x1c];
            float e01 = _elev[y1c * _width + x0c];
            float e11 = _elev[y1c * _width + x1c];

            float ex0 = Mathf.Lerp(e00, e10, tx);
            float ex1 = Mathf.Lerp(e01, e11, tx);
            return Mathf.Lerp(ex0, ex1, ty);
        }

        /// <summary>
        /// The world-space centre of texel <c>(x, y)</c> — the inverse of the world→texel mapping
        /// <see cref="ElevationAt"/> uses. The paint tool maps a brush's world position to texels through
        /// this/its inverse so a painted dab lands where the cursor is.
        /// </summary>
        public Vector2 TexelToWorld(int x, int y)
        {
            float u = (x + 0.5f) / _width;
            float v = (y + 0.5f) / _height;
            return new Vector2(_worldMin.x + u * _worldSize.x, _worldMin.y + v * _worldSize.y);
        }

        /// <summary>Map a normalized 0..1 R sample to metres above datum: <c>lerp(min, max, r)</c>.</summary>
        public static float DecodeElevation(float r01, float minElevation, float maxElevation)
            => Mathf.Lerp(minElevation, maxElevation, Mathf.Clamp01(r01));

        /// <summary>
        /// Map metres above datum to the normalized 0..1 R the texture stores: the inverse of
        /// <see cref="DecodeElevation"/>, clamped to 0..1 (heights outside the range saturate). The paint
        /// tool encodes brushed elevations through this before writing the texture.
        /// </summary>
        public static float EncodeElevation(float elevation, float minElevation, float maxElevation)
        {
            float span = Mathf.Max(maxElevation - minElevation, 1e-3f);
            return Mathf.Clamp01((elevation - minElevation) / span);
        }
    }
}
