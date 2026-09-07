using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// 🔴 <b>THE HEIGHT-MAP CHAIN, AS ARITHMETIC — how the drawn waterline is built from the sim's.</b>
    ///
    /// <para>The water shader's wet/dry edge is not a picture: it is
    /// <c>clip(_WaterLevel - lerp(_HeightMin, _HeightMax, tex2D(_HeightTex, uv).r))</c>. Every term in
    /// that line is a number this class can produce, so <b>where the drawn edge sits relative to the
    /// sim's true contour is computable without rendering anything</b> — no camera, no clock, no
    /// palette, no noise floor. That is what makes register rows 9 + 10 answerable offline.</para>
    ///
    /// <para><b>Two independent quantizations live in this chain, and they are not the same defect:</b></para>
    /// <list type="bullet">
    /// <item><description><b>SPATIAL</b> — the bake is <c>res * res</c> texels over the height rect.
    /// Nine Mile Creek asks for a grid derived from its region and the water surface clamps it to 256,
    /// which over 760 m is <b>2.97 m per texel</b>. Bilinear filtering makes the reconstructed field
    /// piecewise-bilinear, so its iso-contour kinks at every cell wall — a comb whose teeth are one
    /// texel long and whose edges follow the grid axes.</description></item>
    /// <item><description><b>VALUE</b> — the bake is <c>R8</c>, so elevation lands on a
    /// <see cref="MetresPerCode"/> grid (4.7 cm over Nine Mile Creek's -6..+6 m range). ⚠️ Bilinear
    /// filtering interpolates BETWEEN those codes, so the sampled field is continuous and the value
    /// grid makes <b>no plateaus</b>. What it does is jog each texel NODE by up to half a code, which
    /// displaces the contour sideways by <c>0.5 * MetresPerCode / slope</c> — a jitter whose
    /// correlation length is, again, one texel.</description></item>
    /// </list>
    ///
    /// <para><b>Both therefore produce a comb at the SAME period</b>, and only the amplitude tells them
    /// apart: <see cref="LateralJitterFromValueQuantum"/> is the value grid's whole contribution, so if
    /// the measured tooth is far taller than it, the pitch is the cause and a deeper texture will not
    /// help. Separating them is the point — the register must not widen a texture that was never the
    /// problem (CLAUDE.md rule 7).</para>
    ///
    /// <para>Pure and static: no clock, no RNG, no scene, no graphics device.</para>
    /// </summary>
    public static class SeabedBakeMath
    {
        /// <summary>Codes available in the <c>R8</c> bake — 0..255, so 255 intervals.</summary>
        public const int CodeCount = 255;

        /// <summary>
        /// 🔴 <b>THE SHIPPED ENCODE.</b> One elevation (metres above datum) to its <c>R8</c> code, exactly
        /// as the water surface writes it into the bake texture. Called by that writer, so the number
        /// this class reasons about is the number that is stored, not a restatement of it.
        /// </summary>
        public static byte Encode(float elevationMetres, float heightMin, float heightMax)
        {
            float span = Mathf.Max(heightMax - heightMin, 1e-3f);
            return (byte)Mathf.Clamp(
                Mathf.RoundToInt((elevationMetres - heightMin) / span * CodeCount), 0, CodeCount);
        }

        /// <summary>The shader's <c>lerp(_HeightMin, _HeightMax, r)</c> — the inverse of
        /// <see cref="Encode"/>, for one code.</summary>
        public static float Decode(byte code, float heightMin, float heightMax)
            => Mathf.Lerp(heightMin, heightMax, code / (float)CodeCount);

        /// <summary>Metres of world per texel: the SPATIAL quantum, and the comb's predicted tooth.</summary>
        public static float MetresPerTexel(float worldSpanMetres, int resolution)
            => resolution > 0 ? worldSpanMetres / resolution : 0f;

        /// <summary>Metres of elevation per code: the VALUE quantum.</summary>
        public static float MetresPerCode(float heightMin, float heightMax)
            => Mathf.Max(heightMax - heightMin, 1e-3f) / CodeCount;

        /// <summary>
        /// How far sideways the drawn waterline can be moved by the VALUE grid alone, in metres, on a
        /// seabed of the given slope — half a code of elevation converted into ground distance. This is
        /// the whole budget of the 8-bit texture, and the number that says whether a wider format could
        /// possibly account for an observed tooth.
        /// </summary>
        public static float LateralJitterFromValueQuantum(float heightMin, float heightMax, float slope)
            => slope > 1e-4f ? 0.5f * MetresPerCode(heightMin, heightMax) / slope : 0f;

        /// <summary>
        /// ⚠️ <b>THE MODELLED HALF.</b> The shader's <c>SAMPLE_TEXTURE2D</c> of a
        /// <c>FilterMode.Bilinear</c>, <c>TextureWrapMode.Clamp</c> texture, in managed code: texel
        /// centres at <c>(i + 0.5) / res</c>, the four neighbours blended, edges clamped.
        ///
        /// <para>Everything else in this class is arithmetic the engine also runs; this one is a
        /// statement about what the sampler does. It is exactly specified — bilinear filtering is not a
        /// vendor choice — but it is still a model, and a fixture built on it must say so.</para>
        /// </summary>
        public static float SampleBilinear01(byte[] codes, int resolution, Vector2 uv)
        {
            if (codes == null || resolution <= 0 || codes.Length < resolution * resolution) return 0f;

            float tx = uv.x * resolution - 0.5f;
            float ty = uv.y * resolution - 0.5f;
            int x0 = Mathf.FloorToInt(tx), y0 = Mathf.FloorToInt(ty);
            float fx = tx - x0, fy = ty - y0;

            float C(int x, int y) =>
                codes[Mathf.Clamp(y, 0, resolution - 1) * resolution +
                      Mathf.Clamp(x, 0, resolution - 1)] / (float)CodeCount;

            float bottom = Mathf.Lerp(C(x0, y0),     C(x0 + 1, y0),     fx);
            float top    = Mathf.Lerp(C(x0, y0 + 1), C(x0 + 1, y0 + 1), fx);
            return Mathf.Lerp(bottom, top, fy);
        }

        /// <summary>
        /// 🔴 <b>THE DRAWN SEABED at a world position</b> — the full chain the fragment runs:
        /// world to uv over the height rect, bilinear code, then <c>lerp(min, max)</c>. Compare against
        /// the sim's own <c>ITidalTerrain.ElevationAt</c> at the same position and the difference IS the
        /// disagreement rows 9 + 10 are about.
        /// </summary>
        public static float DrawnElevationAt(byte[] codes, int resolution, Vector2 worldPos,
                                             Vector2 worldMin, Vector2 worldSize,
                                             float heightMin, float heightMax)
        {
            var uv = new Vector2((worldPos.x - worldMin.x) / Mathf.Max(worldSize.x, 1e-3f),
                                 (worldPos.y - worldMin.y) / Mathf.Max(worldSize.y, 1e-3f));
            return Mathf.Lerp(heightMin, heightMax, SampleBilinear01(codes, resolution, uv));
        }

        /// <summary>
        /// Bake an elevation field to codes exactly as the water surface does — sampled at texel
        /// CENTRES, <c>(x + 0.5) / res</c>, which is the convention the bilinear read above assumes. The
        /// two must agree or the drawn contour sits half a texel off the sim's for no reason.
        /// </summary>
        public static byte[] Bake(System.Func<Vector2, float> elevationAt, int resolution,
                                  Vector2 worldMin, Vector2 worldSize, float heightMin, float heightMax)
        {
            var codes = new byte[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                var p = new Vector2(worldMin.x + (x + 0.5f) / resolution * worldSize.x,
                                    worldMin.y + (y + 0.5f) / resolution * worldSize.y);
                codes[y * resolution + x] = Encode(elevationAt(p), heightMin, heightMax);
            }
            return codes;
        }
    }
}
