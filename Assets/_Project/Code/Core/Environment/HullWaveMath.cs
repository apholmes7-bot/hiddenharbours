using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The pure math of the waves a moving hull makes. Engine-light and allocation-free, so EditMode
    /// tests walk it headless; the water shader carries a line-for-line twin of
    /// <see cref="SourceHeight"/> (<c>HullWaveSourceHeight</c>) and the tests read the shader to pin it.
    ///
    /// <para><b>The model in one breath.</b> As she makes way, a hull drops wave SOURCES along her track
    /// (one chain at the stern, one at the bow). Each source sends out one ring: a band of crests
    /// <see cref="PacketWidth"/> wide whose centre spreads at <see cref="RingSpeed"/>, while the crests
    /// themselves run outward through the band at her speed (the phase speed of the wave that keeps
    /// pace with her, <see cref="StationaryWavelength"/>). A line of rings dropped by a source moving at
    /// V, each spreading at V·sin(α), is bounded by a V of half-angle α: with α = 19.47° that is the
    /// Kelvin wedge. Behind her on the track the crests of successive rings line up across the track
    /// (the transverse waves), because along the track the crests travel at exactly her speed and so
    /// keep station with her. Once laid, a ring belongs to the water: it keeps spreading, loses height
    /// (<see cref="TimeFade"/>, <see cref="Spread"/>) and dies where it is, whatever she does next, so
    /// a turn bends the wake and a stop leaves it running out across the water.</para>
    ///
    /// <para><b>Deterministic</b> (rule 5): no randomness and no frame counts; everything is a function of
    /// the source's birth time, place and the hull's speed and length then.</para>
    /// </summary>
    public static class HullWaveMath
    {
        /// <summary>Gravity, m/s². The same value the water's dispersion uses.</summary>
        public const float Gravity = 9.81f;

        /// <summary>The ring's band is cut beyond this many band half-widths (exp(-9) ≈ 1e-4 of the
        /// crest: invisible). Mirrored as <c>HH_HULL_WAVE_PACKET_REACH</c> in the water shader.</summary>
        public const float PacketReach = 3f;

        /// <summary>The budget spacing is widened by this factor (one part in ten thousand: invisible) so
        /// that rounding can never fit one more live source into a life than the budget.</summary>
        private const float BudgetMargin = 1.0001f;

        /// <summary>
        /// How fast a ring's band spreads for a hull at <paramref name="speed"/>: <c>V·sin α</c>. The
        /// envelope of rings dropped along her track is then a V of half-angle exactly α.
        /// </summary>
        public static float RingSpeed(float speed, float halfAngleDegrees)
        {
            if (speed <= 0f) return 0f;
            return speed * Mathf.Sin(Mathf.Clamp(halfAngleDegrees, 0f, 89f) * Mathf.Deg2Rad);
        }

        /// <summary>
        /// The half-angle of the V a line of rings makes, degrees: <c>asin(u / V)</c>. The inverse of
        /// <see cref="RingSpeed"/>; tests use it to prove the wedge.
        /// </summary>
        public static float WedgeHalfAngleDegrees(float ringSpeed, float speed)
        {
            if (speed <= 0f) return 0f;
            return Mathf.Asin(Mathf.Clamp01(ringSpeed / speed)) * Mathf.Rad2Deg;
        }

        /// <summary>The wavelength that keeps station behind a hull at <paramref name="speed"/>:
        /// <c>λ = 2πV²/g</c>. Its deep-water phase speed is exactly V.</summary>
        public static float StationaryWavelength(float speed)
        {
            if (speed <= 0f) return 0f;
            return 2f * Mathf.PI * speed * speed / Gravity;
        }

        /// <summary>The wavelength she draws: <see cref="StationaryWavelength"/> held inside the
        /// owner's range.</summary>
        public static float Wavelength(float speed, in HullWaveSettings s)
        {
            float lo = Mathf.Max(s.MinWavelengthMetres, 0.05f);
            float hi = Mathf.Max(s.MaxWavelengthMetres, lo);
            return Mathf.Clamp(StationaryWavelength(speed), lo, hi);
        }

        /// <summary>Deep-water phase speed of a wave of <paramref name="wavelength"/>: <c>√(gλ/2π)</c>.</summary>
        public static float PhaseSpeed(float wavelength)
        {
            if (wavelength <= 0f) return 0f;
            return Mathf.Sqrt(Gravity * wavelength / (2f * Mathf.PI));
        }

        /// <summary>Froude number on length, <c>V / √(gL)</c>. 0 for no length.</summary>
        public static float Froude(float speed, float lengthMetres)
        {
            if (lengthMetres <= 0f || speed <= 0f) return 0f;
            return speed / Mathf.Sqrt(Gravity * lengthMetres);
        }

        /// <summary>
        /// Her crest height, metres: <c>HeightPerMetreLength · L · min(Fr / knee, 1)²</c>, eased in over
        /// one <c>MinSpeed</c> above <c>MinSpeed</c> and capped at <c>MaxHeightMetres</c>. So a rowed
        /// dory raises centimetres and a cargo vessel at speed raises tens of centimetres, from her
        /// length and her speed alone. Exactly 0 at or below <c>MinSpeed</c>.
        /// </summary>
        public static float CrestHeight(float speed, float lengthMetres, in HullWaveSettings s)
        {
            if (speed <= s.MinSpeed || lengthMetres <= 0f || s.HeightPerMetreLength <= 0f) return 0f;
            float onset = Mathf.Clamp01((speed - s.MinSpeed) / Mathf.Max(s.MinSpeed, 0.05f));
            float response = Mathf.Min(Froude(speed, lengthMetres) / Mathf.Max(s.FroudeKnee, 1e-3f), 1f);
            float h = s.HeightPerMetreLength * lengthMetres * response * response * onset;
            return Mathf.Min(h, Mathf.Max(s.MaxHeightMetres, 0f));
        }

        /// <summary>How long her waves live, seconds: per metre of her length, held in range.</summary>
        public static float Lifetime(float lengthMetres, in HullWaveSettings s)
        {
            float lo = Mathf.Max(s.MinLifetimeSeconds, 0.1f);
            float hi = Mathf.Max(s.MaxLifetimeSeconds, lo);
            return Mathf.Clamp(s.LifetimeSecondsPerMetreLength * Mathf.Max(lengthMetres, 0f), lo, hi);
        }

        /// <summary>
        /// Seconds between two of her sources on one chain: the spacing the owner asked for in
        /// wavelengths, or wider when that would keep more than <paramref name="budget"/> alive over a
        /// life of <paramref name="lifetime"/>. The second term is what makes the budget a hard bound:
        /// a chain can never hold more than <c>budget</c> live sources. Infinity for a hull at rest.
        /// </summary>
        public static float DepositInterval(float speed, float wavelength, float lifetime, int budget,
                                            float spacingWavelengths)
        {
            if (speed <= 0f || wavelength <= 0f || budget <= 0) return float.PositiveInfinity;
            float bySpacing = Mathf.Max(spacingWavelengths, 1e-3f) * wavelength / speed;
            // A hair wider than life/budget, so float rounding can never let budget + 1 live at once.
            float byBudget = Mathf.Max(lifetime, 0f) / budget * BudgetMargin;
            return Mathf.Max(bySpacing, byBudget);
        }

        /// <summary>
        /// The height one source carries for a spacing of <paramref name="spacingMetres"/> along her
        /// track: her crest height times <c>min(1, gain · spacing / λ)</c>. Rings overlap about
        /// λ / spacing deep, so the sum stays near her crest height whatever the spacing.
        /// </summary>
        public static float SourceAmplitude(float crestHeight, float spacingMetres, float wavelength, float gain)
        {
            if (crestHeight <= 0f || wavelength <= 0f || spacingMetres <= 0f) return 0f;
            return crestHeight * Mathf.Min(1f, Mathf.Max(gain, 0f) * spacingMetres / wavelength);
        }

        /// <summary>A ring's band width, metres: <c>PacketWavelengths · λ</c>.</summary>
        public static float PacketWidth(float wavelength, in HullWaveSettings s)
        {
            return Mathf.Max(s.PacketWavelengths, 0.05f) * Mathf.Max(wavelength, 0f);
        }

        /// <summary>
        /// How much of its height a ring keeps at <paramref name="age"/>: <c>exp(-age/decay) · (1 - age/life)²</c>.
        /// 1 at birth, falling all its life, and EXACTLY 0 at the end of life and after, so retiring a
        /// source never pops. 0 for no life.
        /// </summary>
        public static float TimeFade(float age, float lifetime, float decaySeconds)
        {
            if (lifetime <= 0f || age >= lifetime) return 0f;
            float a = Mathf.Max(age, 0f);
            float end = 1f - a / lifetime;
            float decay = decaySeconds > 0f ? Mathf.Exp(-a / decaySeconds) : 1f;
            return decay * end * end;
        }

        /// <summary>Height lost to spreading round the ring: <c>1/√(1 + r/R)</c>.</summary>
        public static float Spread(float radius, float spreadMetres)
        {
            if (spreadMetres <= 0f) return 1f;
            return 1f / Mathf.Sqrt(1f + Mathf.Max(radius, 0f) / spreadMetres);
        }

        /// <summary>The band's profile across the ring: a Gaussian, cut at <see cref="PacketReach"/>.</summary>
        public static float Packet(float x)
        {
            if (x <= -PacketReach || x >= PacketReach) return 0f;
            return Mathf.Exp(-x * x);
        }

        /// <summary>
        /// The phase of a source's crests at <paramref name="age"/>: <c>k·c·age</c>, wrapped to [0, 2π) in
        /// double so the shader never sees a large angle.
        /// </summary>
        public static float Phase(float wavenumber, float phaseSpeed, double age)
        {
            double p = (double)wavenumber * phaseSpeed * System.Math.Max(age, 0.0);
            p %= 2.0 * System.Math.PI;
            if (p < 0.0) p += 2.0 * System.Math.PI;
            return (float)p;
        }

        /// <summary>
        /// The height one source draws at distance <paramref name="radius"/> from where it was laid —
        /// the whole spatial law, and the C# twin of the water shader's <c>HullWaveSourceHeight</c>:
        /// <c>amp · Packet((r - front)/W) · Spread(r) · cos(k·r - phase)</c>.
        /// </summary>
        /// <param name="amplitude">Its height now (birth height × <see cref="TimeFade"/>).</param>
        /// <param name="front">Its band's radius now (ring speed × age).</param>
        /// <param name="wavenumber">2π/λ.</param>
        /// <param name="phase">From <see cref="Phase"/>.</param>
        /// <param name="invWidth">1 / band width.</param>
        /// <param name="invSpread">1 / <c>SpreadMetres</c> (0 = no spreading loss).</param>
        public static float SourceHeight(float radius, float amplitude, float front, float wavenumber,
                                         float phase, float invWidth, float invSpread)
        {
            if (amplitude == 0f) return 0f;
            float x = (radius - front) * invWidth;
            if (x <= -PacketReach || x >= PacketReach) return 0f;
            float env = Mathf.Exp(-x * x);
            float spread = 1f / Mathf.Sqrt(1f + radius * invSpread);
            return amplitude * env * spread * Mathf.Cos(wavenumber * radius - phase);
        }

        /// <summary>
        /// d(height)/d(radius) of <see cref="SourceHeight"/>, analytic — what lights the wave faces.
        /// Twin: the slope half of the shader's <c>HullWaveSourceHeight</c>.
        /// </summary>
        public static float SourceSlope(float radius, float amplitude, float front, float wavenumber,
                                        float phase, float invWidth, float invSpread)
        {
            if (amplitude == 0f) return 0f;
            float x = (radius - front) * invWidth;
            if (x <= -PacketReach || x >= PacketReach) return 0f;
            float env = Mathf.Exp(-x * x);
            float spread = 1f / Mathf.Sqrt(1f + radius * invSpread);
            float ph = wavenumber * radius - phase;
            float c = Mathf.Cos(ph);
            float s = Mathf.Sin(ph);
            float dEnv = -2f * x * invWidth * env;
            float dSpread = -0.5f * invSpread * spread * spread * spread;
            return amplitude * (dEnv * spread * c + env * dSpread * c - env * spread * wavenumber * s);
        }
    }
}
