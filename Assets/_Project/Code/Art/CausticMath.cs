using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure shared caustic arithmetic (ADR 0027 #2) and the hull's reflected-sun law.
    /// The caller supplies the five heights already sampled on the shader's pixelized world grid.
    /// This does not sample or advance waves, change depth, or supply gameplay lighting.
    /// All artistic strengths are arguments from material data; constants below mirror shader guards.
    /// Change the shader and this twin together if the optical approximation changes.
    /// </summary>
    public static class CausticMath
    {
        /// <summary>Schlick water reflectance; elevation is the sun's angle above the horizon.</summary>
        public static float FresnelAtDegrees(float elevationDegrees, float f0, float power)
            => FresnelFromSun(Mathf.Sin(elevationDegrees * Mathf.Deg2Rad), f0, power);

        public static float FresnelFromSun(float sunElevation, float f0, float power)
        {
            float reflectance = Mathf.Clamp01(f0);
            return reflectance + (1 - reflectance)
                * Mathf.Pow(1 - Mathf.Clamp01(sunElevation), Mathf.Max(1, power));
        }

        public static float Irradiance(float sunElevation, float power, float floor)
            => Mathf.Max(Mathf.Clamp01(floor), Mathf.Pow(Mathf.Clamp01(sunElevation), Mathf.Max(.01f, power)));

        /// <summary>Schlick at the wave facet's incidence, not at the sun's elevation.</summary>
        public static float FacetFresnel(float sunElevation, float chop, float f0, float power, float tiltDegrees)
        {
            float elevation = Mathf.Asin(Mathf.Clamp01(sunElevation)) * Mathf.Rad2Deg;
            float incidence = Mathf.Clamp(90 - elevation - Mathf.Max(0, tiltDegrees) * Mathf.Clamp01(chop), 0, 90);
            return FresnelFromSun(Mathf.Cos(incidence * Mathf.Deg2Rad), f0, power);
        }

        public static float EncodeAlpha(float gain, float pattern, float weight)
            => Mathf.Max(128f / 255f, 1 - Mathf.Clamp01(gain * pattern * weight) * .5f);

        public static float DecodeAlpha(float alpha) => alpha >= .5f && alpha < 1 ? (1-alpha)*2 : 0;

        public static float QuantizeAlpha8(float alpha) => Mathf.Round(Mathf.Clamp01(alpha)*255)/255;

        public struct VeinSettings
        {
            public float PixelsPerUnit, Scale, Flow, PaintScale, UntileStrength;
            public float NoiseScale, BandScaleResponse, Chop, TextureStrength;
        }

        /// <summary>CPU twin of the shared world-anchored independent/painted veins.
        /// Texture reads are supplied by the caller; runtime hulls evaluate this on the GPU.</summary>
        public static float Veins(Vector2 world, float time, in VeinSettings settings,
                                  Func<Vector2, float> texture = null)
        {
            Vector2 cp = Pixelize(world * settings.Scale
                + new Vector2(time * settings.Flow, -time * settings.Flow * 0.7f), settings.PixelsPerUnit);
            float a = Noise(cp), b = Noise(cp * 1.7f + Vector2.one * 11.1f);
            float ridge = Saturate(1f - Math.Abs(a - b) * 3f);
            float independent = ridge * ridge;
            if (texture == null) return independent;
            Vector2 scroll = new Vector2(time * settings.Flow * 0.6f, -time * settings.Flow * 0.4f);
            float painted = Untile(world, scroll, in settings, texture)
                * Untile(world, -scroll * 1.3f, in settings, texture);
            return independent + (painted * 2f - independent) * settings.TextureStrength;
        }

        public static Vector2 ProjectToWater(Vector3 world, Vector4 plane)
        {
            Vector3 normal = new Vector3(plane.x, plane.y, plane.z);
            float height = Vector3.Dot(normal, world) + plane.w;
            float scale = height / Mathf.Max(Vector3.Dot(normal, normal), 1e-6f);
            return new Vector2(world.x - plane.x * scale, world.y - plane.y * scale);
        }

        private static Vector2 Pixelize(Vector2 p, float pixelsPerUnit)
        {
            float ppu = Math.Max(pixelsPerUnit, 1f);
            return new Vector2(Mathf.Floor(p.x * ppu), Mathf.Floor(p.y * ppu)) / ppu;
        }

        private static float Frac(float value) => value - Mathf.Floor(value);
        private static float Hash(Vector2 p)
        {
            p = new Vector2(Frac(p.x * 123.34f), Frac(p.y * 345.45f));
            float d = Vector2.Dot(p, p + Vector2.one * 34.345f);
            p += Vector2.one * d;
            return Frac(p.x * p.y);
        }

        private static Vector2 Hash2(Vector2 p)
            => new Vector2(Frac(Mathf.Sin(Vector2.Dot(p, new Vector2(127.1f, 311.7f))) * 43758.5453f),
                Frac(Mathf.Sin(Vector2.Dot(p, new Vector2(269.5f, 183.3f))) * 43758.5453f));

        private static float Noise(Vector2 p)
        {
            Vector2 i = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
            Vector2 f = p - i;
            Vector2 u = new Vector2(f.x * f.x * (3f - 2f * f.x), f.y * f.y * (3f - 2f * f.y));
            return Mathf.LerpUnclamped(Mathf.LerpUnclamped(Hash(i), Hash(i + Vector2.right), u.x),
                Mathf.LerpUnclamped(Hash(i + Vector2.up), Hash(i + Vector2.one), u.x), u.y);
        }

        private static float Untile(Vector2 world, Vector2 scroll, in VeinSettings settings,
                                    Func<Vector2, float> texture)
        {
            float strength = Saturate(settings.UntileStrength);
            float frequency = settings.NoiseScale
                / (1f + Math.Max(settings.BandScaleResponse, 0f) * Saturate(settings.Chop));
            Vector2 low = new Vector2(Noise(world * frequency * .5f + Vector2.one * 3.1f),
                Noise(world * frequency * .5f + Vector2.one * 8.7f)) - Vector2.one * .5f;
            Vector2 high = new Vector2(Noise(world * frequency * 1.7f + Vector2.one * 17.3f),
                Noise(world * frequency * 1.7f + Vector2.one * 42.9f)) - Vector2.one * .5f;
            Vector2 warped = world + (low + high * .4f) * (strength * 1.5f);
            float scale = Math.Max(settings.PaintScale * 2f, 1e-4f);
            float raw = texture(Pixelize(warped + scroll, settings.PixelsPerUnit) * scale);
            if (strength <= .001f) return raw;
            Vector2 uv = Pixelize(warped + scroll, settings.PixelsPerUnit) * scale;
            Vector2 cell = new Vector2(Mathf.Floor(uv.x), Mathf.Floor(uv.y));
            Vector2 f = uv - cell;
            float v00 = texture(Pixelize(warped + Hash2(cell) * 64f + scroll, settings.PixelsPerUnit) * scale);
            float v10 = texture(Pixelize(warped + Hash2(cell + Vector2.right) * 64f + scroll, settings.PixelsPerUnit) * scale);
            float v01 = texture(Pixelize(warped + Hash2(cell + Vector2.up) * 64f + scroll, settings.PixelsPerUnit) * scale);
            float v11 = texture(Pixelize(warped + Hash2(cell + Vector2.one) * 64f + scroll, settings.PixelsPerUnit) * scale);
            float bx = f.x * f.x * (3f - 2f * f.x), by = f.y * f.y * (3f - 2f * f.y);
            float untiled = Mathf.LerpUnclamped(Mathf.LerpUnclamped(v00, v10, bx),
                Mathf.LerpUnclamped(v01, v11, bx), by);
            return Mathf.LerpUnclamped(raw, untiled, strength);
        }

        /// <summary>Arithmetic twin of Include/CausticPattern.hlsl. Sampling is the caller's:
        /// the independent/painted value and the five heights refer to the same world point.</summary>
        public static float Pattern(float independentPattern, float centre, float positiveX, float negativeX,
                                    float positiveY, float negativeY, float step, float gain,
                                    float blend, float waveEnvelope)
            => BlendWithField(independentPattern,
                FocusedBrightness(centre, positiveX, negativeX, positiveY, negativeY, step, gain),
                blend, waveEnvelope);

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
