using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The water type's law (ADR 0027 #7, gains 2) and the C# twin of the shader's type terms: the
    /// <c>hhWaterTypeSigma</c> added inside <c>AbsorptionSigma</c>, <c>WaterTypeAt</c>,
    /// <c>WaterTypeSeamWeight</c>, <c>WaterPlumeWeight</c> and <c>WaterBodyWeight</c>. Change one, change
    /// both in the same PR.
    ///
    /// <para><b>Extinction, per channel.</b> σ = turbidity × ratio + stain × S_stain + algae × S_algae +
    /// silt(t) × S_silt, each term clamped at 0 (<see cref="WaterAbsorption.Sigma(float, Vector3, Vector3)"/>).
    /// The first term is today's and still the mood's: the weather ease moves turbidity, so a storm still
    /// stirs the water and a fog still mutes it. The type adds its own character on top and takes none
    /// away, so the default type (no constituent) is today's σ bit for bit, and a clear type is today's
    /// water. Stain eats blue first, algae spares green, silt dims every channel.</para>
    ///
    /// <para><b>Silt after rain.</b> silt(t) = dry silt + response × the rain averaged over the window, on
    /// <see cref="WaveMemory"/>'s fixed world-time grid (<see cref="WaveMemory.AverageSignal"/>). Pure in
    /// (worldSeed, gameTime) with no history: a jump to t reads what playing to t would, and nothing is
    /// saved. The average is an exact box over the held taps, so after a wet spell the silt is back at the
    /// dry silt within one window and one tap.</para>
    ///
    /// <para><b>The body (in-scatter).</b> The water's own colour moves toward the type's (the stain's and the
    /// silt's colours, mixed by their shares) by strength × (1 − exp(−σ_in × 2d)), σ_in = stain + silt, the
    /// scatter posterized by the absorption bands. It lands before the palette grade, which bounds it like
    /// every layer. No strength, or no stain and silt, is no tint.</para>
    ///
    /// <para><b>Place.</b> A plume lerps toward its own type inside its reach; then the whole fades to the
    /// default over a seam band at the surface's edges, so two regions' water meets without a step.</para>
    ///
    /// <para>Visual only: col.rgb, never depth, the height read or anything a hull rides (rule 5).</para>
    /// </summary>
    public static class WaterType
    {
        /// <summary>Plume slots the shader carries (mirrors <c>WATER_MAX_PLUMES</c>).</summary>
        public const int MaxPlumes = 4;

        /// <summary>A plume's rim never closes to nothing (mirrors <c>WATER_PLUME_MIN_FEATHER</c>, metres):
        /// a guard on the smoothstep's span, not a dial.</summary>
        public const float MinFeatherMeters = 1e-3f;

        /// <summary>silt = dry silt + response × the mean rain (clamped 0 to 1), in 1/m.</summary>
        public static float Silt(in WaterTypeValues type, float meanRain)
            => Mathf.Max(type.DrySilt, 0f) + Mathf.Max(type.SiltResponse, 0f) * Mathf.Clamp01(meanRain);

        /// <summary>The silt at <paramref name="gameTimeSeconds"/>: the rain averaged over the type's window on
        /// WaveMemory's grid. A type the rain cannot move (no response, no rain source or no day length)
        /// reads its dry silt without a tap.</summary>
        public static float SiltAt(in WaterTypeValues type, IWorldTimeSignal rain, double gameTimeSeconds,
                                   double secondsPerDay)
        {
            if (rain == null || !(type.SiltResponse > 0f) || !(secondsPerDay > 0.0)) return Silt(type, 0f);
            float window = (float)(Mathf.Clamp(type.SiltWindowDays, WaterTypeValues.MinWindowDays,
                WaterTypeValues.MaxWindowDays) * secondsPerDay);
            return Silt(type, WaveMemory.AverageSignal(rain, gameTimeSeconds, window, type.SiltTaps));
        }

        /// <summary>The type's own extinction, 1/m per channel, added to the mood's. Zero for the default
        /// type.</summary>
        public static Vector3 TypeSigma(in WaterTypeValues type, float silt)
        {
            float stain = Mathf.Max(type.Stain, 0f);
            float algae = Mathf.Max(type.Algae, 0f);
            float mud = Mathf.Max(silt, 0f);
            return new Vector3(
                stain * Mathf.Max(type.StainSpectrum.x, 0f) + algae * Mathf.Max(type.AlgaeSpectrum.x, 0f)
                    + mud * Mathf.Max(type.SiltSpectrum.x, 0f),
                stain * Mathf.Max(type.StainSpectrum.y, 0f) + algae * Mathf.Max(type.AlgaeSpectrum.y, 0f)
                    + mud * Mathf.Max(type.SiltSpectrum.y, 0f),
                stain * Mathf.Max(type.StainSpectrum.z, 0f) + algae * Mathf.Max(type.AlgaeSpectrum.z, 0f)
                    + mud * Mathf.Max(type.SiltSpectrum.z, 0f));
        }

        /// <summary>The extinction that tints the body: stain + silt, 1/m.</summary>
        public static float InscatterSigma(in WaterTypeValues type, float silt)
            => Mathf.Max(type.Stain, 0f) + Mathf.Max(silt, 0f);

        /// <summary>The body's colour, linear: the stain's and the silt's colours mixed by their shares of
        /// the in-scatter. Black, and unused, when there is neither.</summary>
        public static Vector3 BodyColour(in WaterTypeValues type, float silt)
        {
            float stain = Mathf.Max(type.Stain, 0f);
            float mud = Mathf.Max(silt, 0f);
            float sum = stain + mud;
            if (!(sum > 0f)) return Vector3.zero;
            Color a = SeaPalette.ToLinear(type.StainColour);
            Color b = SeaPalette.ToLinear(type.SiltColour);
            float w = mud / sum;
            return new Vector3(Mathf.Lerp(a.r, b.r, w), Mathf.Lerp(a.g, b.g, w), Mathf.Lerp(a.b, b.b, w));
        }

        /// <summary>The two values a type pushes: sigma = (the type's σ, σ_in) and the premultiplied body
        /// (colour × strength, strength). Both zero for the default type; the strength is zero wherever there
        /// is nothing to scatter, so a blend toward a plume never darkens.</summary>
        public static void Uniforms(in WaterTypeValues type, float silt, out Vector4 sigma, out Vector4 body)
        {
            Vector3 s = TypeSigma(type, silt);
            float inscatter = InscatterSigma(type, silt);
            sigma = new Vector4(s.x, s.y, s.z, inscatter);
            float strength = inscatter > 0f ? Mathf.Clamp01(type.BodyStrength) : 0f;
            Vector3 c = BodyColour(type, silt) * strength;
            body = new Vector4(c.x, c.y, c.z, strength);
        }

        /// <summary>1 inside the surface, easing to 0 over <paramref name="seam"/> metres at its edges
        /// (rect = min x, min y, max x, max y); 1 everywhere when the seam is 0.</summary>
        public static float SeamWeight(Vector2 p, Vector4 rect, float seam)
        {
            if (!(seam > 0f)) return 1f;
            float d = Mathf.Min(Mathf.Min(p.x - rect.x, rect.z - p.x), Mathf.Min(p.y - rect.y, rect.w - p.y));
            return WaterSurface.Smoothstep(0f, seam, d);
        }

        /// <summary>1 from a plume's mouth out to its reach less its feather, easing to 0 at the reach
        /// (shape = mouth x, mouth y, reach, feather).</summary>
        public static float PlumeWeight(Vector2 p, Vector4 shape)
        {
            float reach = shape.z;
            if (!(reach > 0f)) return 0f;
            float inner = Mathf.Max(reach - Mathf.Max(shape.w, MinFeatherMeters), 0f);
            return 1f - WaterSurface.Smoothstep(inner, reach, Vector2.Distance(p, new Vector2(shape.x, shape.y)));
        }

        /// <summary>The type at a point: the region's, each live plume lerped in by its weight, the whole
        /// faded by the seam. The twin of the shader's <c>WaterTypeAt</c>.</summary>
        public static void At(Vector2 p, Vector4 sigma, Vector4 body, Vector4 rect, float seam,
                              Vector4[] plumeShape, Vector4[] plumeSigma, Vector4[] plumeBody, int plumeCount,
                              out Vector4 sigmaAt, out Vector4 bodyAt)
        {
            sigmaAt = sigma;
            bodyAt = body;
            int n = Mathf.Min(plumeCount, MaxPlumes);
            for (int i = 0; i < n; i++)
            {
                float w = PlumeWeight(p, plumeShape[i]);
                sigmaAt = Vector4.Lerp(sigmaAt, plumeSigma[i], w);
                bodyAt = Vector4.Lerp(bodyAt, plumeBody[i], w);
            }
            float s = SeamWeight(p, rect, seam);
            sigmaAt *= s;
            bodyAt *= s;
        }

        /// <summary>How far the body moves toward the type's colour at this depth: strength × the in-scatter
        /// over the down-and-back path, the scatter posterized by <paramref name="bands"/> (the absorption
        /// bands, round to nearest; under 1 is smooth).</summary>
        public static float BodyWeight(float inscatterSigma, float strength, float depth, float bands)
        {
            float scatter = 1f - Mathf.Exp(-Mathf.Max(inscatterSigma, 0f)
                                           * (WaterAbsorption.DownAndBack * Mathf.Max(depth, 0f)));
            if (bands >= 1f) scatter = Mathf.Floor(Mathf.Clamp01(scatter) * bands + 0.5f) / bands;
            return Mathf.Clamp01(strength) * scatter;
        }

        /// <summary>The frag's body step: the colour moves toward body.rgb / body.a by
        /// <see cref="BodyWeight"/>. No strength is no change.</summary>
        public static Vector3 ApplyBody(Vector3 col, Vector4 sigma, Vector4 body, float depth, float bands)
        {
            if (!(body.w > 0f)) return col;
            float w = BodyWeight(sigma.w, body.w, depth, bands);
            Vector3 target = new Vector3(body.x, body.y, body.z) / body.w;
            return col + (target - col) * w;
        }
    }
}
