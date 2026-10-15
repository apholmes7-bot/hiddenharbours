using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure arithmetic twin of HiddenHarboursWater.shader's caustic block (ADR 0027 #2).
    /// The caller supplies the five heights already sampled on the shader's pixelized world grid.
    /// This does not sample or advance waves, change depth, or supply gameplay lighting.
    /// All artistic strengths are arguments from material data; constants below mirror shader guards.
    /// Change the shader and this twin together if the optical approximation changes.
    /// </summary>
    public static class CausticMath
    {
        public static float FocusedBrightness(float centre, float positiveX, float negativeX,
                                               float positiveY, float negativeY, float step, float gain)
        {
            float spacing = Math.Max(step, 1e-3f);
            float laplacian = (positiveX + negativeX + positiveY + negativeY - 4f * centre)
                              / (spacing * spacing);
            return Saturate(-laplacian * Math.Max(gain, 0f));
        }

        /// <summary>
        /// Blend zero (including the shader's 0.001 branch threshold) is an exact passthrough.
        /// Zero wave envelope preserves the independent pattern; zero curvature in a LIVE field
        /// contributes no focused light, which is a different condition from an absent wave field.
        /// </summary>
        public static float BlendWithField(float independentPattern, float focusedBrightness,
                                           float blend, float waveEnvelope)
        {
            if (blend > 0.001f)
            {
                float fieldLive = Saturate(waveEnvelope * 40f);
                float weight = Saturate(blend) * fieldLive;
                return independentPattern + (focusedBrightness - independentPattern) * weight;
            }
            return independentPattern;
        }

        /// <summary>
        /// tintSum is _DayNightTint.r + .g + .b. An unset cycle (sum at most 0.001) means full day;
        /// a running cycle at the horizon (sun elevation zero) does not mean an unset cycle.
        /// </summary>
        public static float DayGate(float sunElevation, float tintSum, float strength)
        {
            float sunUp = tintSum > 1e-3f ? Saturate(sunElevation) : 1f;
            return 1f + (sunUp - 1f) * Saturate(strength);
        }

        /// <summary>
        /// C2a: attenuate the light nets with the seabed's per-channel, down-and-back law.
        /// Use real column depth (not the caustic placement bias), and band before blending.
        /// Clarity is owner look policy, not mood-eased; zero strength or inactive sigma is
        /// exactly Vector3.one. All optical arithmetic stays in WaterAbsorption.
        /// </summary>
        public static Vector3 ClarityTransmission(Vector3 sigma, float depth, float clarity, float bands)
        {
            if (clarity <= 0f || !WaterAbsorption.IsActive(sigma)) return Vector3.one;
            Vector3 transmission = WaterAbsorption.BandTransmission(
                WaterAbsorption.Transmission(sigma, depth), bands);
            return Vector3.Lerp(Vector3.one, transmission, Saturate(clarity));
        }

        private static float Saturate(float value) => Math.Min(Math.Max(value, 0f), 1f);
    }
}
