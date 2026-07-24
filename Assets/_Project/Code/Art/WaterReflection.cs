using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, headless-testable twins of the WATER REFLECTION layer's sea-state response curve
    /// (HiddenHarboursWater.shader). The reflection itself — the in-shader faked mirror sheen that reflects
    /// the day/night sky colour and a sun streak down the surface — is GPU value-noise + texture-free maths
    /// and can't be evaluated headless. But the two scalars that decide HOW the reflection reads as the sea
    /// changes mood are pure functions of the already-pushed sea-state uniforms, mirrored here so the
    /// calm→stormy behaviour is locked without opening Unity:
    ///
    ///   • <see cref="ReflectionStrength"/> — the master OPACITY of the reflection: full on glassy/CALM water,
    ///     falling to ~0 by a tunable sea-state (<c>_Chop</c>) where the sea stops mirroring (a storm doesn't
    ///     reflect), and additionally dimmed by wind whitecaps (<c>_Roughness</c>). Scaled by the master dial.
    ///   • <see cref="ReflectionSharpness"/> — how SHARP the reflection reads: a clean mirror at CALM (≈1),
    ///     smearing/scattering toward 0 as chop + wind rise (the reflection breaks up across the chop).
    ///
    /// Both are derived in-shader from <c>_Chop</c> (0 = glass .. 1 = storm; WaterSurface sets it from the
    /// sea-state) and <c>_Roughness</c> (the wind whitecap scalar) — there is NO new C# uniform push and
    /// WaterSurface.cs is untouched; these are local mirrors for the determinism/feel guard only.
    ///
    /// Everything the reflection drives in the shader is VISUAL-ONLY: it adds to <c>col.rgb</c> like every
    /// other water layer and NEVER touches depth/clip/the deep-tint/the caustic gate/<c>_WaterLevel</c>, so it
    /// saves nothing and feeds no sim (P1 integrity, CLAUDE.md rule 5). Reuses <see cref="WaterSurface.Smoothstep"/>
    /// so the twins share the exact smoothstep the shader uses.
    /// </summary>
    public static class WaterReflection
    {
        /// <summary>
        /// Twin of the shader's reflection STRENGTH curve. Returns a 0..1 master opacity for the reflection
        /// layer: <c>1</c> on glassy/CALM water, fading to <c>0</c> by <paramref name="fadeChop"/> sea-state
        /// (a storm doesn't mirror), and further dimmed by wind whitecaps. The result is scaled by
        /// <paramref name="master"/> (<c>_ReflectionStrength</c>; 0 = reflections fully off = today's look).
        /// </summary>
        /// <param name="chop">The sea-state choppiness (<c>_Chop</c>; 0 = glass .. 1 = storm).</param>
        /// <param name="roughness">The wind whitecap scalar (<c>_Roughness</c>; 0..1).</param>
        /// <param name="fadeChop">The <c>_Chop</c> value at which the reflection has fully faded to 0
        /// (<c>_ReflectionFadeChop</c>). Clamped to a small floor so a zero never divides.</param>
        /// <param name="windFade">How much wind/roughness additionally dims the reflection
        /// (<c>_ReflectionWindFade</c>; 0 = wind doesn't dim, 1 = full wind dims it out).</param>
        /// <param name="master">The master strength dial (<c>_ReflectionStrength</c>; 0 = off).</param>
        public static float ReflectionStrength(
            float chop, float roughness, float fadeChop, float windFade, float master)
        {
            // calm→stormy chop falloff: 1 at glass, 0 by fadeChop (smooth, so the mirror dissolves not snaps).
            float fade = Mathf.Max(fadeChop, 1e-3f);
            float chopFalloff = 1f - WaterSurface.Smoothstep(0f, fade, Mathf.Max(chop, 0f));
            // wind whitecaps scatter the mirror: a calm but breezy sea dims a touch, a gale kills what chop left.
            float windDim = 1f - Mathf.Clamp01(roughness) * Mathf.Clamp01(windFade);
            return Mathf.Clamp01(master) * chopFalloff * windDim;
        }

        /// <summary>
        /// Twin of the shader's reflection SHARPNESS curve. Returns a 0..1 sharpness: <c>1</c> = a clean,
        /// mirror-like reflection (CALM/glassy); falling toward <c>0</c> = a smeared/scattered reflection
        /// (lively→stormy), as chop + wind break the surface up. The shader uses this to widen the vertical
        /// smear of the reflected sky/sun streak (sharp = a tight streak, soft = a broad scattered band).
        /// </summary>
        /// <param name="chop">The sea-state choppiness (<c>_Chop</c>; 0 = glass .. 1 = storm).</param>
        /// <param name="roughness">The wind whitecap scalar (<c>_Roughness</c>; 0..1).</param>
        /// <param name="chopWeight">How much chop reduces sharpness (<c>_ReflectionChopScatter</c>).</param>
        /// <param name="windWeight">How much wind reduces sharpness (<c>_ReflectionWindScatter</c>).</param>
        public static float ReflectionSharpness(
            float chop, float roughness, float chopWeight, float windWeight)
        {
            // a combined "agitation" the sharpness falls off against; both terms positive and clamped.
            float agitation = Mathf.Max(chop, 0f) * Mathf.Max(chopWeight, 0f)
                            + Mathf.Clamp01(roughness) * Mathf.Max(windWeight, 0f);
            return Mathf.Clamp01(1f - agitation);
        }

        // ===== SKY-CONTENT REFLECTIONS (clouds + moon glitter path + stars + the sun glitter path) =======
        // The sea is the ONLY place the sky appears in this ¾ top-down game, so the reflection also mirrors
        // SKY CONTENT, not just the flat sky colour: drifting clouds (day + night), the moon with a vertical
        // glitter path (night), faint star sparkle (night), and the moon column's golden-hour twin — a warm
        // SUN glitter path toward the LOW sun at dawn/dusk (gated by SunGlitterGate over _SunElevation,
        // sharing the moon column's geometry knobs). The in-shader fields are GPU value-noise and
        // can't run headless, but the DIRECTION + the day/night GATES that decide WHEN/WHERE each reads are
        // pure functions, mirrored here for the determinism/feel guard. WaterSurface.cs is NOT touched — the
        // shader reads the already-published globals (_DayNightTint / _SunDir / _SunElevation / _WindWorld).
        //
        // PLACEMENT + complete-dark composition (mirrors the shader, for the record):
        //  • The reflected moon disc is ANCHORED at the CAMERA's ground position (_WorldSpaceCameraPos.xy) and
        //    offset along MoonDirection — it travels WITH the viewer like a real reflection of a body at
        //    infinity, so it always lands on water near the play area. (It was anchored at the height-map world
        //    centre, which on St Peters is the middle of the bared SANDBAR — the owner could never see it.)
        //  • The NIGHT-gated content (moon/glitter/stars + the clouds' night share) is composited AFTER the
        //    palette guard-rail (ADR 0015) and PRE-COMPENSATED for the day/night multiply overlay via
        //    LightMath.CompensateForDayNightTint (divide by max(_DayNightTint.rgb, 0.02)) so complete dark
        //    doesn't crush it to ~3%; the day share stays in the pre-grade composite so daylight is unchanged.
        //    The compensation maths + its bounds are pinned in LightMathTests.

        /// <summary>
        /// Twin of the shader's <c>MoonDir</c>. The moon sits roughly OPPOSITE the sun in the sky, so its
        /// reflected ground direction is the negated sun direction. Normalized (NaN-safe; falls back to a
        /// fixed night arc <c>(0, 1)</c> on a near-zero sun direction, e.g. the cycle not running, matching
        /// the shader). It need not be astronomically exact — just a believable, stable moon position.
        /// </summary>
        /// <param name="sunDirX">The ground-plane sun direction X (the <c>_SunDir.x</c> global).</param>
        /// <param name="sunDirY">The ground-plane sun direction Y (the <c>_SunDir.y</c> global).</param>
        public static Vector2 MoonDirection(float sunDirX, float sunDirY)
        {
            Vector2 opp = new Vector2(-sunDirX, -sunDirY);
            if (opp.sqrMagnitude < 1e-6f)
                return new Vector2(0f, 1f);   // no sun dir (cycle off) -> a fixed believable night arc (+Y)
            return opp.normalized;
        }

        /// <summary>
        /// Twin of the shader's <c>NightFactor</c>: a 0..1 darkness gate from the day/night multiply tint's
        /// luminance. <c>0</c> in full daylight (bright tint), rising to <c>1</c> at deep night (dark tint),
        /// with a smooth dusk ramp so the moon/stars fade in as the sky darkens. Mirrors the boat-light night
        /// gate convention (Rec.601 luma of the tint, then <c>smoothstep(threshold, threshold+soft, darkness)</c>).
        /// When the cycle is not running the tint is near-black/unset and the caller passes a fallback instead.
        /// </summary>
        /// <param name="tintLuma">Rec.601 luminance of the day/night tint (<c>1</c> day .. <c>~0</c> night).</param>
        /// <param name="threshold">Darkness at which the night content STARTS to read (<c>_NightStart</c>).</param>
        /// <param name="softness">The dusk ramp width above the threshold (<c>_NightSoftness</c>).</param>
        public static float NightFactor(float tintLuma, float threshold, float softness)
        {
            float darkness = Mathf.Clamp01(1f - Mathf.Max(tintLuma, 0f));
            float lo = Mathf.Clamp01(threshold);
            float hi = Mathf.Clamp01(threshold + Mathf.Max(softness, 1e-4f));
            return WaterSurface.Smoothstep(lo, hi, darkness);
        }

        /// <summary>The shader's <c>_CloudMoonlitVis</c> default: how strongly the clouds' NIGHT share reads
        /// under a FULL, high moon (see <see cref="MoonlitCloudVisibility"/>). 0.35 = faint moonlit bands;
        /// 1 = the pre-fix full-strength night clouds exactly (the legacy passthrough).</summary>
        public const float DefaultCloudMoonlitVisibility = 0.35f;

        /// <summary>
        /// Twin of the shader's moonlit night-cloud gate (owner playtest 2026-07-23, the "whole sea becomes
        /// white" defect): the clouds' NIGHT share rides the COMPENSATED post-grade bucket, which cancels
        /// the day/night multiply EXACTLY — so a full-strength night share painted daylight-strength cloud
        /// bands over a sea the overlay had dimmed to a few percent, a milky veil that smothered every
        /// water detail from dusk on. Clouds are a REFLECTION of the sky, not a light source: at night they
        /// read only by MOONLIGHT. The night share's weight is therefore
        /// <c>nightFactor × saturate(moonPresence × moonBrightness) × visibility</c> — full-moon-up nights
        /// keep faint moonlit bands, a moonless/new-moon night shows none, and the no-MoonCycle fallback
        /// (presence = brightness = 1) keeps a bare-scene preview sane. The moon disc/glitter/stars/boat
        /// beam are genuine LIGHT content and keep the compensated bucket ungated. <paramref name="visibility"/>
        /// = 1 restores the pre-fix behaviour exactly (the legacy passthrough contract).
        /// </summary>
        /// <param name="nightFactor">The <see cref="NightFactor"/> darkness gate (0 day .. 1 night).</param>
        /// <param name="moonPresence">The moon's above-horizon presence (<c>_MoonPhaseState.w</c>; fallback 1).</param>
        /// <param name="moonBrightness">The moon's live brightness (<c>_MoonPhaseState.z</c>; fallback 1).</param>
        /// <param name="visibility">The owner dial (<c>_CloudMoonlitVis</c>,
        /// default <see cref="DefaultCloudMoonlitVisibility"/>).</param>
        public static float MoonlitCloudVisibility(float nightFactor, float moonPresence,
                                                   float moonBrightness, float visibility)
        {
            return Mathf.Clamp01(nightFactor)
                 * Mathf.Clamp01(moonPresence * moonBrightness)
                 * Mathf.Clamp01(visibility);
        }

        /// <summary>The sun elevation by which the golden-hour gate has fully risen (the shader's
        /// <c>SUN_GLITTER_RISE_END</c>): the glitter fades in just above the horizon.</summary>
        public const float SunGlitterRiseEnd = 0.02f;

        /// <summary>The sun elevation at which the golden-hour gate starts to fall (the shader's
        /// <c>SUN_GLITTER_FALL_START</c>): above this the sun is getting high and the column shortens away.</summary>
        public const float SunGlitterFallStart = 0.35f;

        /// <summary>The sun elevation by which the golden-hour gate has fully faded (the shader's
        /// <c>SUN_GLITTER_FALL_END</c>): a high sun casts no glitter column (the specular carries it).</summary>
        public const float SunGlitterFallEnd = 0.5f;

        /// <summary>
        /// Twin of the shader's <c>SunGlitterGate</c>: the GOLDEN-HOUR window over the sun's elevation that
        /// gates the warm SUN glitter path (the moon glitter column's daytime/dusk twin). A smooth 0..1
        /// window over <c>_SunElevation</c> (−1..1; positive = sun up) that PEAKS while the sun is LOW but UP
        /// (the long glitter path across the water at dawn/dusk): it rises 0 → 1 across elevation
        /// 0..<see cref="SunGlitterRiseEnd"/>, holds 1 through the golden-hour band, and falls 1 → 0 across
        /// <see cref="SunGlitterFallStart"/>..<see cref="SunGlitterFallEnd"/>. Exactly 0 at and below the
        /// horizon (the moon's glitter takes over at night) and 0 by high sun. When the day/night cycle is
        /// not running <c>_SunElevation</c> is 0 (unset) → the gate is 0 → no phantom glitter in a bare art
        /// scene, the same "unset" convention the night content uses. In the shader the gated column is
        /// composited into the COMPENSATED post-grade share (with the moon/stars/boat beam) so the dusk
        /// tint's downstream multiply can't mute its authored warm gold; at midday the tint is ~1 so the
        /// compensation is a natural no-op — and this gate is ~0 there anyway (daylight unchanged).
        /// </summary>
        /// <param name="sunElevation">The sun's height (<c>_SunElevation</c>; 1 noon, 0 horizon, ≤0 night).</param>
        public static float SunGlitterGate(float sunElevation)
        {
            float rise = WaterSurface.Smoothstep(0f, SunGlitterRiseEnd, sunElevation);
            float fall = 1f - WaterSurface.Smoothstep(SunGlitterFallStart, SunGlitterFallEnd, sunElevation);
            return Mathf.Clamp01(rise * fall);
        }

        /// <summary>
        /// Twin of the shader's per-element sky-content strength: the master <see cref="ReflectionStrength"/>
        /// (sea-state fade — clouds/moon/stars all die in a storm, like the rest of the reflection) × the
        /// element's own tunable strength × a day/night gate. <paramref name="nightGated"/> selects whether
        /// the element is NIGHT-only (moon, stars — multiply by the night factor) or all-day (clouds —
        /// multiply by 1). Reuses <see cref="ReflectionStrength"/> so clouds/moon/stars inherit the SAME
        /// calm→stormy fade as the sky-colour mirror (strongest on glass, gone in chop), and the moon/stars
        /// additionally peak at night.
        /// </summary>
        /// <param name="seaStateStrength">The result of <see cref="ReflectionStrength"/> (the master sea-state fade).</param>
        /// <param name="elementStrength">The element's own tunable strength (cloud/moon/star).</param>
        /// <param name="nightFactor">The <see cref="NightFactor"/> darkness gate (0 day .. 1 night).</param>
        /// <param name="nightGated">True = night-only (moon/stars); false = all-day (clouds).</param>
        public static float SkyElementStrength(
            float seaStateStrength, float elementStrength, float nightFactor, bool nightGated)
        {
            float gate = nightGated ? Mathf.Clamp01(nightFactor) : 1f;
            return Mathf.Max(seaStateStrength, 0f) * Mathf.Max(elementStrength, 0f) * gate;
        }
    }
}
