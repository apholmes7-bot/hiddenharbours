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
    }
}
