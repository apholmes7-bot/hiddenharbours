using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The crest glow's twin (ADR 0027 #7 amendment, gains 3): light through a thin crest when the sun is low
    /// and behind it. It mirrors the shader's crest-glow block; change one, change both in the same PR.
    ///
    /// <para>It shares the sea's one facing (<c>faceSigned</c>: the field slope against the sun, computed
    /// once) and the sun side's elevation gate. The face shade darkens the back of a swell (value) and the sun
    /// side cools it (hue); the glow lights only the thin top of that same back, where the low light crosses
    /// the crest to the eye, in the sun's colour seen through the water: jade through the open sea, warmer
    /// through a stain. It is light, not foam: a hue apart from the foam's white, on the back of the crest
    /// rather than its breaking face.</para>
    ///
    /// <para>Strength 0 (the shipped value) is an exact passthrough. Dark at night and at noon (the elevation
    /// gate), gone in fog (a mood float whose fog anchor reads 0), posterized like every layer. The shader runs
    /// it only while the wave trains are live (<c>trainsLive</c>): the crest's height is read against the
    /// trains' envelope, and without the field there is no envelope to read. Visual only: col.rgb, never
    /// depth, the height read or anything a hull rides (rule 5).</para>
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

        /// <summary>How much of the crest's back is lit: the back's facing (−faceSigned) × the top of the crest
        /// (<paramref name="height01"/>, the envelope-relative height, above <paramref name="top"/>) × the
        /// elevation gate, posterized by <paramref name="bands"/>, then × the strength and the calm gate.
        /// 0 below the strength's or the calm gate's threshold.</summary>
        public static float Amount(float strength, float faceSigned, float height01, float top, float bands,
                                   float swellReadGate, float sunElevation)
        {
            if (!(strength > 0.001f) || !(swellReadGate > 0.001f)) return 0f;
            float back = Mathf.Clamp01(-faceSigned);
            float crest = WaterSurface.Smoothstep(Mathf.Min(Mathf.Clamp01(top), MaxTopOnset), 1f,
                                                  Mathf.Clamp01(height01));
            float amount = back * crest * ElevationGate(sunElevation);
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
