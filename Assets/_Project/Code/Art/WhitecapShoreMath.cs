using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The near-shore law for the wind whitecaps: the C# twin of the water shader's <c>ShoreCapShare</c>
    /// (HiddenHarboursWater.shader). Inside the shore band (<c>_ShoreWaveReach</c> metres offshore, read from
    /// the same shore-wave field the wave trains turn along) the caps keep less of their opacity as the water
    /// shallows (<see cref="Keep"/>), and their streak texture cross-fades from the wind's axis onto the turned
    /// crest line, parallel to the shore (<see cref="AlignShare"/>); the shader samples that streak in the two fixed crest bases either side of the shore's direction (16 round the turn) so the pattern never shears. Visual only: no sim, no save, no per-frame
    /// CPU work (rule 5). Both dials at 0 give keep exactly 1 and share exactly 0, so the caps draw as before.
    /// Change one side, change both in the same commit.
    /// </summary>
    public static class WhitecapShoreMath
    {
        /// <summary>Below this field gradient length (m per m) the crest-line turn is fully off.</summary>
        public const float GradientGateLow = 0.3f;

        /// <summary>At and above this field gradient length the crest-line turn is fully on.</summary>
        public const float GradientGateHigh = 0.8f;

        /// <summary>The shared shore-band weight: 1 at and inside the shoreline, easing to 0 by <paramref name="reach"/>.</summary>
        public static float NearShore(float metresOffshore, float reach)
            => 1f - SmoothStep(0f, Mathf.Max(reach, 1e-3f), metresOffshore);

        /// <summary>The share of the cap opacity kept at this distance offshore (1 = untouched).</summary>
        public static float Keep(float metresOffshore, float fade, float reach)
            => 1f - Mathf.Clamp01(fade) * NearShore(metresOffshore, reach);

        /// <summary>How much of the cap texture comes from the crest-line basis (0 = the wind's axis).</summary>
        /// <param name="gradientLength">Length of the field's gradient here; it collapses on the ridge between
        /// two shores, where the crest direction is undefined, so the turn fades instead of flipping.</param>
        public static float AlignShare(float metresOffshore, float gradientLength, float align, float reach)
            => Mathf.Clamp01(align) * NearShore(metresOffshore, reach)
               * SmoothStep(GradientGateLow, GradientGateHigh, gradientLength);

        /// <summary>HLSL <c>smoothstep</c>, edge order as written.</summary>
        public static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Half-width of the soft edge where a blob noise switches a streak (in noise units).</summary>
        public const float EdgeSoft = 0.12f;

        /// <summary>
        /// The caps and streaks pass through the foam sheet's threshold, so the shader never dims them by a
        /// share: a fixed world blob noise <paramref name="n"/> (0..1) picks, by area, which streaks have taken
        /// the share <paramref name="s"/>. 0 below share 0, 1 at share 1, a soft edge between.
        /// </summary>
        public static float Dissolve(float n, float s)
        {
            if (s <= 0f) return 0f;
            if (s >= 1f) return 1f;
            return SmoothStep(1f - s - EdgeSoft, 1f - s + EdgeSoft, n);
        }

        /// <summary>Whether a streak stays at this kept share: keep 1 keeps every streak exactly; lower keeps
        /// fewer streaks, each at full strength.</summary>
        public static float Thin(float n, float keep) => keep >= 1f ? 1f : Dissolve(n, keep);
    }
}
