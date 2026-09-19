using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The px cliff kit's <b>palette relight</b>, in C#: the same arithmetic the shader's px branch runs
    /// (<c>HiddenHarboursCliffFace.shader</c>, keyword <see cref="CliffCatalog.PxKeyword"/>), held
    /// here so EditMode tests can drive it over the rig's own bake without a GPU.
    ///
    /// <para><b>⭐ STEP THE INDEX, DO NOT MULTIPLY THE PIXEL.</b> The kit's
    /// <c>shaders/relight_palette.glsl</c> is the source: the live sun moves a texel ONE band up or down,
    /// cast shadow moves a rock texel one or two TIERS down, and the result is looked up in the rock's
    /// own palette. Every colour on screen is one a pixel artist put in the ramp.</para>
    ///
    /// <para><b>⚠ The shadow is RECOVERED, not supplied.</b> The glsl takes <c>_SunShadow</c> from "the
    /// engine's own shadow map", and this engine has none for a wall. But <c>_mask.R</c> is
    /// <c>N·L × (1 − shadow × 0.78)</c> at the bake key (<c>pxCliffFaceRig.js</c> :576), so dividing
    /// the bake's own <c>N·L</c> back out leaves the shadow (<see cref="RecoverShadow"/>). The divide is
    /// guarded where the bake's <c>N·L</c> is near zero, because there the mask carries no shadow at
    /// all.</para>
    ///
    /// <para><b>⚠ And the bake key must be in the PACKED frame.</b> The rig's key is y-DOWN the face;
    /// the normal it packs into <c>_normal.G</c> is y-UP. <see cref="PackedFrame"/> is the flip. Scored
    /// against the shadow the rig cut, on the three faces <c>PxCliffRigBakeTests</c> bakes: flipped,
    /// the recovered tier agrees with the rig's on 96.2–99.9% of cells and finds 94–99% of the shadowed
    /// cells the guard can see; unflipped, it agrees on 69.2–81.7%.</para>
    /// </summary>
    public static class CliffPxRelightMath
    {
        /// <summary>The live <c>N·L</c> above which a texel takes one band UP — the sidecar's
        /// <c>LIGHTING.thresholds.band_up</c>.</summary>
        public const float BandUp = 0.74f;

        /// <summary>The live <c>N·L</c> below which a texel takes one band DOWN —
        /// <c>band_down</c>.</summary>
        public const float BandDown = 0.40f;

        /// <summary>The recovered shadow above which a rock texel drops one tier —
        /// <c>shadow_one_tier</c>.</summary>
        public const float ShadowOneTier = 0.45f;

        /// <summary>…and two tiers — <c>shadow_two_tiers</c>.</summary>
        public const float ShadowTwoTiers = 0.85f;

        /// <summary>How deep the rig cuts the cast shadow into <c>_mask.R</c>:
        /// <c>mr = N·L × (1 − shadow × 0.78)</c>, <c>pxCliffFaceRig.js</c> :576. Not in the sidecar;
        /// the rig is the source.</summary>
        public const float ShadowDepth = 0.78f;

        /// <summary>Below this bake <c>N·L</c> the texel faced away from the bake key, the rig wrote
        /// <c>mask.R = 0</c> whatever the shadow was, and the divide recovers nothing — so no shadow is
        /// claimed there (the live light's band step still darkens it).</summary>
        public const float ShadowGuard = 0.02f;

        /// <summary>The batter clamp the rig's <c>setSlope()</c> applies before it tips the key.</summary>
        public const float MinBatter = 30f, MaxBatter = 90f;

        /// <summary>Each rock owns six consecutive LUT rows, darkest tier first (tier −3 … +2).</summary>
        public const int TiersPerRock = 6;

        /// <summary>
        /// The rig's key light, tipped into a battered face's frame — <c>PxCliffFace.keyFor()</c> after
        /// <c>setSlope(batter)</c>. Still in the RIG frame (y down the face); see
        /// <see cref="PackedFrame"/>.
        /// </summary>
        public static Vector3 BakeKeyAt(Vector3 key, float batterDegrees)
        {
            float b = Mathf.Clamp(batterDegrees, MinBatter, MaxBatter);
            float th = (90f - b) * Mathf.Deg2Rad, ct = Mathf.Cos(th), st = Mathf.Sin(th);
            return new Vector3(key.x, key.y * ct + key.z * st, -key.y * st + key.z * ct);
        }

        /// <summary>A rig-frame vector (y DOWN the face) in the frame the rig packs <c>_normal</c> in
        /// (y UP the face, <c>ry = −ny × 0.5 + 0.5</c>).</summary>
        public static Vector3 PackedFrame(Vector3 rig) => new Vector3(rig.x, -rig.y, rig.z);

        /// <summary>
        /// The live <c>N·L</c> the band step reads. With the day/night cycle running and the sun at or
        /// below the horizon (<paramref name="sinElevation"/> ≤ 0) there is no direct light, and in a
        /// palette that is <c>N·L</c> 0: one band down everywhere. It is the px twin of the v10 path's
        /// day term, and it stops a wall that faces the sun's bearing lighting up at midnight from a sun
        /// on the far side of the planet. The night itself stays the overlay's (owner, 09-18: "day only
        /// for now").
        /// </summary>
        public static float LiveNdl(Vector3 normal, Vector3 light, bool cycleOn, float sinElevation) =>
            cycleOn && sinElevation <= 0f ? 0f : Vector3.Dot(normal, light);

        /// <summary>
        /// The cast shadow (0 lit … 1 fully shadowed) recovered from <c>_mask.R</c> by dividing the
        /// bake's own <c>N·L</c> back out. <paramref name="bakeNdl"/> is the decoded normal dotted with
        /// the PACKED-frame bake key; at or below <see cref="ShadowGuard"/> it returns 0.
        /// </summary>
        public static float RecoverShadow(float maskR, float bakeNdl)
        {
            if (!(bakeNdl > ShadowGuard)) return 0f;
            float ratio = Mathf.Clamp01(maskR / bakeNdl);
            return Mathf.Clamp01((1f - ratio) / ShadowDepth);
        }

        /// <summary>How many tiers a shadow drops a rock texel: 0, 1 or 2.</summary>
        public static int TierStep(float shadow) =>
            shadow > ShadowTwoTiers ? 2 : shadow > ShadowOneTier ? 1 : 0;

        /// <summary>
        /// The whole relight on one texel: the index's (row, band), whether it is a rock texel, the live
        /// <c>N·L</c> and the recovered shadow in; the LUT cell out. Accessory rows (18..24) take the
        /// band step and never a tier step — lichen does not go the colour of shadowed rock.
        /// </summary>
        public static void Relight(int row, int band, bool rock, float ndl, float shadow,
                                   out int litRow, out int litBand)
        {
            if (ndl > BandUp) band += 1;
            else if (ndl < BandDown) band -= 1;

            if (rock)
            {
                int tier = row % TiersPerRock, baseRow = row - tier;
                tier -= TierStep(shadow);
                row = baseRow + Mathf.Clamp(tier, 0, TiersPerRock - 1);
            }

            litRow = row;
            litBand = Mathf.Clamp(band, 0, CliffCatalog.PxPaletteBands - 1);
        }

        /// <summary>
        /// The UV of a LUT cell's centre. <b>⚠ Unity's v = 0 is the BOTTOM of the PNG</b>, while the
        /// glsl (WebGL, no flip on upload) reads row 0 at v = 0 — so the row is counted down from the
        /// top here. Get it backwards and sandstone samples the padding rows.
        /// </summary>
        public static Vector2 LutUv(int row, int band) => new Vector2(
            (band + 0.5f) / CliffCatalog.PxPaletteWidth,
            1f - (row + 0.5f) / CliffCatalog.PxPaletteHeight);
    }
}
