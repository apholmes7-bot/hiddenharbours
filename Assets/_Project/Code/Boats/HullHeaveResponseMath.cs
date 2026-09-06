using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// One hull's heave character, resolved once from her own data: how many points of her waterline
    /// the sea is read at, and the mass-spring she answers it with. A value, so the resolve happens
    /// per wiring and the tick does arithmetic only.
    /// </summary>
    public readonly struct HullHeaveResponse
    {
        /// <summary>Half her length overall, along the keel (m). 0 = no honest size — see
        /// <see cref="IsHonest"/>.</summary>
        public readonly float HalfLengthMeters;

        /// <summary>Half her beam (m), from the one derivation (<c>HullPresence.HalfBeamOf</c>).</summary>
        public readonly float HalfBeamMeters;

        /// <summary>Her natural heave period T (s) — <c>2π√(m / ρ g A_wp)</c>.</summary>
        public readonly float NaturalPeriodSeconds;

        /// <summary>ω = 2π / T (rad/s) — what the chase integrates with.</summary>
        public readonly float NaturalFrequencyRadPerSec;

        /// <summary>Her damping ratio ζ, from her own <c>BoatHullDef.SeakeepingDamping</c> inside the
        /// settings' band.</summary>
        public readonly float DampingRatio;

        /// <summary>How many points of her waterline the field is read at each tick.</summary>
        public readonly int FootprintSamples;

        public HullHeaveResponse(float halfLengthMeters, float halfBeamMeters,
                                 float naturalPeriodSeconds, float dampingRatio, int footprintSamples)
        {
            HalfLengthMeters = Mathf.Max(0f, halfLengthMeters);
            HalfBeamMeters = Mathf.Max(0f, halfBeamMeters);
            NaturalPeriodSeconds = Mathf.Max(1e-3f, naturalPeriodSeconds);
            NaturalFrequencyRadPerSec = (2f * Mathf.PI) / NaturalPeriodSeconds;
            DampingRatio = Mathf.Max(0f, dampingRatio);
            FootprintSamples = Mathf.Max(1, footprintSamples);
        }

        /// <summary>True when this hull has an honest length to spread her footprint over. A hull with
        /// none (a bare test rig, a decor boat with no def) rides as she always did: one sample,
        /// surface-bolted — the same neutral reading every other per-hull channel gives her.</summary>
        public bool IsHonest => HalfLengthMeters > 0f && FootprintSamples > 1;

        /// <summary>The point-sampled, surface-bolted reading — what a hull with no data gets, and
        /// what the whole fleet gets with <see cref="HullWeightSettings.Enabled"/> off.</summary>
        public static readonly HullHeaveResponse Bolted = default;
    }

    /// <summary>
    /// <b>A HULL IS A LOW-PASS FILTER WITH A NATURAL PERIOD, NOT A FLOAT ON THE SURFACE</b> — the
    /// pure, headless-testable core of water-fidelity PR 10 (owner playtest 2026-09-05: <i>"the boats
    /// seem to have too much hangtime after a big wave and bob up and down too fast and jerky as if
    /// they have no weight"</i>).
    ///
    /// <para><b>What was measured before it existed.</b> On <c>f2c7105b</c> the ride was one
    /// <c>WaveMath.Sample</c> at the hull's origin. Swept over wavelength on a pure 1 m train the
    /// ride/wave transfer function read <b>1.000 at every wavelength from 2 m to 64 m for the dory
    /// (4.5 m), the cape islander (12.9 m) AND the 110 m tanker</b> — three hulls two orders of
    /// magnitude apart, all riding a 2 m ripple in full, in lockstep, with zero lag. Nothing about
    /// that is a boat.</para>
    ///
    /// <para><b>The two mechanisms, both physical, neither tuned.</b>
    /// <list type="number">
    /// <item><b>The FOOTPRINT.</b> Read the field at N points along her waterline and average: a
    /// wave shorter than the hull cancels under her, a wave longer lifts the whole boat. The
    /// continuous limit is <c>sinc(L/λ)</c> (<see cref="FootprintGain01"/>), zero at λ = L and → 1
    /// for λ ≫ L. The fleet's spread comes out of the hull list for free — the shorter the hull, the
    /// livelier — with nothing to tune per boat.</item>
    /// <item><b>The NATURAL PERIOD.</b> A floating body is a mass on the spring of its own
    /// waterplane: <c>T = 2π√(m / (ρ g A_wp))</c> (<see cref="NaturalPeriodSeconds"/>). She answers a
    /// long swell nearly fully, resonates near her own period, and cannot follow anything much
    /// faster — that is the lag, the settle, and the absence of twitch, all from one number.</item>
    /// </list></para>
    ///
    /// <para><b>⚠️ The mass is her DISPLACEMENT, not <c>BoatHullDef.MassKg</c>.</b> The charter named
    /// <c>MassKg</c>; the data refuses it. That field is the 2D physics body's mass for the helm
    /// model and is nowhere near her displacement — the cape islander carries 6 000 kg against a
    /// ~44 t block estimate for a 12.9 × 4.8 × 1.4 m hull, and the tanker 7 668 t against ~20 900 t.
    /// Fed to the period formula it lands the cape at <b>0.67 s</b> and the dory at <b>0.50 s</b>:
    /// still a twitch, and with the fleet's spread wrong as well (1.35× between a dory and a
    /// 42-footer). Archimedes instead — <c>m = ρ · L · B · draught · C_b</c>, plus one displacement
    /// of added mass — puts them at <b>1.25 s and 2.70 s</b>, which is what the charter's own target
    /// numbers say ("a lobster boat lands near 2–3 s, a dory nearer 1 s") and what a boat of each
    /// size actually heaves at. Note what then happens to the formula: L and B appear in the
    /// displacement AND in the waterplane and CANCEL, so a wall-sided body's heave period depends
    /// only on her DRAUGHT — the classical result, and the reason this reads the one dimension
    /// every hull asset authors honestly. Both terms are still computed explicitly so the
    /// cancellation is a stated property rather than a hidden assumption, and so a hull that ever
    /// authors a real waterplane area can pass it.</para>
    ///
    /// <para><b>Determinism &amp; scope (rules 5, 7).</b> Pure, static, allocation-free, engine-light
    /// (Mathf only) — the same discipline as <see cref="BoatWaveMotionMath"/> /
    /// <see cref="StormRockMath"/>. Nothing here is random and nothing is saved; the STATE is the one
    /// spring the caller owns (<see cref="HeaveWeightState"/>), reset on wake, and it settles inside
    /// one natural period. The per-tick cost is <see cref="FootprintSamples"/> field samples per
    /// hull; the cap that bounds it lives on the settings.</para>
    /// </summary>
    public static class HullHeaveResponseMath
    {
        /// <summary>
        /// How many points of her waterline to read: length ÷ the settings' spacing, rounded, inside
        /// the settings' band — and forced ODD.
        ///
        /// <para><b>Odd on purpose.</b> With segment-centre placement an odd count puts one sample
        /// exactly amidships, which is the sample the SLOPE and the crest factor already need, so the
        /// footprint costs N field reads in total rather than N + 1. It also keeps the pitch lever
        /// symmetric about her centre.</para>
        ///
        /// <para>A hull with no length reads 1 — the point sample, i.e. exactly what she did
        /// before.</para>
        /// </summary>
        public static int FootprintSampleCount(float lengthMeters, in HullWeightSettings settings)
        {
            if (!(lengthMeters > 0f)) return 1;
            float spacing = Mathf.Max(0.25f, settings.FootprintSampleSpacingMeters);
            int lo = Mathf.Max(1, settings.MinFootprintSamples);
            int hi = Mathf.Max(lo, settings.MaxFootprintSamples);
            // CEIL, not round: rounding DOWN would sample her coarser than the settings ask and
            // reintroduce the alias the spacing exists to keep out (measured: boat.zodiac_frc at
            // 6.66 m rounded to 3 points = 2.22 m spacing against a 2 m setting).
            int n = Mathf.Clamp(Mathf.CeilToInt(lengthMeters / spacing), lo, hi);
            if ((n & 1) == 0) n = n + 1 <= hi ? n + 1 : n - 1;   // odd, and never outside the band
            return Mathf.Max(1, n);
        }

        /// <summary>
        /// Where sample <paramref name="index"/> of <paramref name="count"/> sits along the keel, in
        /// metres from amidships (negative = aft). The points are the CENTRES of
        /// <paramref name="count"/> equal segments of her length, not her bow and stern themselves —
        /// midpoint quadrature of the mean, which has no endpoint bias and lands the null exactly on
        /// λ = L even at the 3-sample floor (endpoint-inclusive sampling reads a wave TWICE the hull's
        /// length at 0.33 with 3 points, where the truth is 0.64).
        /// </summary>
        public static float FootprintOffsetMeters(int index, int count, float halfLengthMeters)
        {
            int n = Mathf.Max(1, count);
            if (n == 1) return 0f;
            int i = Mathf.Clamp(index, 0, n - 1);
            float u = (2f * i + 1f) / n - 1f;          // segment centres over [-1, 1]
            return u * Mathf.Max(0f, halfLengthMeters);
        }

        /// <summary>The distance between the outermost samples (m) — the lever the bow-minus-stern
        /// difference is a slope over. 0 for a single sample.</summary>
        public static float FootprintSpanMeters(int count, float halfLengthMeters)
        {
            int n = Mathf.Max(1, count);
            if (n == 1) return 0f;
            return 2f * Mathf.Max(0f, halfLengthMeters) * (n - 1f) / n;
        }

        /// <summary>
        /// <b>The footprint's transfer function in the continuous limit</b>: how much of a wave of
        /// wavelength <paramref name="wavelengthMeters"/> survives being averaged over an extent of
        /// <paramref name="extentMeters"/> — <c>|sinc(extent/λ)| = |sin(πx)/(πx)|</c>. 1 for a wave
        /// far longer than the extent, 0 at exactly one wave per extent.
        ///
        /// <para>The discrete N-point mean approaches this from above and is EXACT at the null for
        /// midpoint sampling; it is used directly on the attitude ENVELOPES (which ride a train's own
        /// slowly-varying amplitude, never its instantaneous value — the phase-lock rule
        /// <c>BoatWaveMotion</c>'s mesh path is built on), and it is what the tests measure the
        /// discrete footprint against.</para>
        /// </summary>
        public static float FootprintGain01(float extentMeters, float wavelengthMeters)
        {
            float extent = Mathf.Abs(extentMeters);
            float lambda = Mathf.Abs(wavelengthMeters);
            if (extent <= 1e-4f || lambda <= 1e-4f) return 1f;
            float x = Mathf.PI * extent / lambda;
            if (x < 1e-4f) return 1f;
            return Mathf.Abs(Mathf.Sin(x) / x);
        }

        /// <summary>Her waterplane area (m²) — <c>L × B × C_wp</c>, the spring the sea pushes on.</summary>
        public static float WaterplaneAreaSqM(float lengthMeters, float beamMeters,
                                              in HullWeightSettings settings)
            => Mathf.Max(0f, lengthMeters) * Mathf.Max(0f, beamMeters)
             * Mathf.Clamp(settings.WaterplaneCoefficient, 0.05f, 1f);

        /// <summary>What she DISPLACES (kg) — Archimedes on her own box, <c>ρ · L · B · draught ·
        /// C_b</c>. See the class doc for why this and not <c>BoatHullDef.MassKg</c>.</summary>
        public static float DisplacedMassKg(float lengthMeters, float beamMeters, float draughtMeters,
                                            in HullWeightSettings settings)
            => Mathf.Max(1f, settings.WaterDensityKgPerCubicMeter)
             * Mathf.Max(0f, lengthMeters) * Mathf.Max(0f, beamMeters) * Mathf.Max(0f, draughtMeters)
             * Mathf.Clamp(settings.BlockCoefficient, 0.05f, 1f);

        /// <summary>The mass that actually has to be accelerated in heave — her displacement plus the
        /// water she carries with her (<see cref="HullWeightSettings.AddedMassFactor"/>).</summary>
        public static float HeavingMassKg(float displacedMassKg, in HullWeightSettings settings)
            => Mathf.Max(0f, displacedMassKg) * (1f + Mathf.Max(0f, settings.AddedMassFactor));

        /// <summary>
        /// <b>The charter's formula, unchanged:</b> <c>T = 2π√(m / (ρ g A_wp))</c> — the period of a
        /// mass on the spring its own waterplane makes. Clamped into the settings' guard band; a
        /// degenerate hull (no waterplane, no mass) returns the ceiling rather than a division.
        /// </summary>
        public static float NaturalPeriodSeconds(float heavingMassKg, float waterplaneAreaSqM,
                                                 float gravity, in HullWeightSettings settings)
        {
            float lo = Mathf.Max(0.05f, settings.MinNaturalPeriodSeconds);
            float hi = Mathf.Max(lo, settings.MaxNaturalPeriodSeconds);
            float stiffness = Mathf.Max(1f, settings.WaterDensityKgPerCubicMeter)
                            * Mathf.Max(0f, gravity) * Mathf.Max(0f, waterplaneAreaSqM);
            if (stiffness <= 0f || heavingMassKg <= 0f) return hi;
            return Mathf.Clamp(2f * Mathf.PI * Mathf.Sqrt(heavingMassKg / stiffness), lo, hi);
        }

        /// <summary>Her damping ratio ζ: her own <c>BoatHullDef.SeakeepingDamping</c> (which already
        /// ladders with size across the fleet) held inside the settings' band, so an authored 0 cannot
        /// ring and nothing wallows past critical.</summary>
        public static float DampingRatio(float hullSeakeepingDamping, in HullWeightSettings settings)
        {
            float lo = Mathf.Max(0f, settings.MinDampingRatio);
            float hi = Mathf.Max(lo, settings.MaxDampingRatio);
            return Mathf.Clamp(Mathf.Max(0f, hullSeakeepingDamping), lo, hi);
        }

        /// <summary>
        /// One hull's whole heave character, from the data she already carries: her length and beam
        /// (the footprint and the waterplane), her draught (the displacement, hence the period) and
        /// her <c>SeakeepingDamping</c> (the ratio). Resolve once per wiring — the tick reads it.
        ///
        /// <para>A hull with no honest length returns <see cref="HullHeaveResponse.Bolted"/>: one
        /// sample, no spring, exactly the ride she had before — the same neutral every other per-hull
        /// channel gives a boat nobody has measured.</para>
        /// </summary>
        public static HullHeaveResponse Resolve(float lengthMeters, float halfBeamMeters,
                                                float draughtMeters, float hullSeakeepingDamping,
                                                float gravity, in HullWeightSettings settings)
        {
            if (!settings.Enabled || !(lengthMeters > 0f)) return HullHeaveResponse.Bolted;

            float beam = Mathf.Max(0f, halfBeamMeters) * 2f;
            float waterplane = WaterplaneAreaSqM(lengthMeters, beam, in settings);
            float displaced = DisplacedMassKg(lengthMeters, beam, draughtMeters, in settings);
            float period = NaturalPeriodSeconds(HeavingMassKg(displaced, in settings), waterplane,
                                                gravity, in settings);
            return new HullHeaveResponse(lengthMeters * 0.5f, Mathf.Max(0f, halfBeamMeters), period,
                                         DampingRatio(hullSeakeepingDamping, in settings),
                                         FootprintSampleCount(lengthMeters, in settings));
        }
    }
}
