using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The crest glow's twin (ADR 0027 #7): low sun lights the thin top of a crest, independently of its
    /// slope. Height above the onset and the sun side's elevation gate determine the amount. The sun's
    /// colour passes through the water's absorption over the crest thickness: light, not white foam.
    /// Change this law and the shader's crest-glow block together in the same PR.
    ///
    /// <para>Material strength 0 remains an exact passthrough. Water types select the shipped arm;
    /// fog fades its strength to zero. Rounded bands precede strength and the calm gate. The shader
    /// requires live wave trains because the height is relative to their envelope. Visual only:
    /// col.rgb, never depth, the height read or anything a hull rides (rule 5).</para>
    /// </summary>
    public static class CrestGlow
    {
        /// <summary>The sun side's fade-in over the first sliver of elevation: the shader's shared
        /// <c>smoothstep(0.0, 0.12, e)</c>, not a second constant.</summary>
        public const float SunRiseFade = 0.12f;

        /// <summary>The add ceiling, the swell-read idiom's 0.15 (the shader's <c>* 0.15</c>).</summary>
        public const float AddCeiling = 0.15f;

        /// <summary>The top's onset is capped below 1 so its smoothstep never degenerates (the deep-blue
        /// idiom's 0.99).</summary>
        public const float MaxTopOnset = 0.99f;

        /// <summary>The sun side's elevation gate, shared: sin^4 of the solar arc angle, with the sun above the
        /// horizon as a precondition. 0 at noon and at night.</summary>
        public static float ElevationGate(float sunElevation)
        {
            float sinSq = 1f - sunElevation * sunElevation;
            return WaterSurface.Smoothstep(0f, SunRiseFade, sunElevation) * sinSq * sinSq;
        }

        /// <summary>The thin top's envelope-relative height above the onset times the elevation gate,
        /// posterized by bands, then multiplied by strength and the calm gate. No slope-facing factor:
        /// a crest's slope approaches zero at its top. Zero below the strength or calm threshold.</summary>
        public static float Amount(float strength, float height01, float top, float bands,
                                   float swellReadGate, float sunElevation)
        {
            if (!(strength > 0.001f) || !(swellReadGate > 0.001f)) return 0f;
            float crest = WaterSurface.Smoothstep(Mathf.Min(Mathf.Clamp01(top), MaxTopOnset), 1f,
                                                  Mathf.Clamp01(height01));
            float amount = crest * ElevationGate(sunElevation);
            if (bands >= 1f) amount = Mathf.Floor(Mathf.Clamp01(amount) * bands + 0.5f) / bands;
            return amount * Mathf.Max(strength, 0f) * swellReadGate;
        }

        /// <summary>The glow's colour, linear: the sun's tint through a thin sheet of the water, its own σ over
        /// the crest's thickness in metres.</summary>
        public static Vector3 Colour(Vector3 sunTint, Vector3 sigma, float thicknessMeters)
        {
            float t = Mathf.Max(thicknessMeters, 0f);
            return new Vector3(sunTint.x * Mathf.Exp(-Mathf.Max(sigma.x, 0f) * t),
                               sunTint.y * Mathf.Exp(-Mathf.Max(sigma.y, 0f) * t),
                               sunTint.z * Mathf.Exp(-Mathf.Max(sigma.z, 0f) * t));
        }

        /// <summary>The frag's add: col + colour × amount × the ceiling.</summary>
        public static Vector3 Apply(Vector3 col, Vector3 colour, float amount)
            => col + colour * (amount * AddCeiling);
    }
}
