using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>THE HULL HAS WEIGHT</b> — every constant of the hull's own heave response, named and
    /// owner-tunable (rule 6). Owner playtest 2026-09-05, verbatim: <i>"the boats seem to have too
    /// much hangtime after a big wave and bob up and down too fast and jerky as if they have no
    /// weight."</i>
    ///
    /// <para><b>The defect these knobs close, measured on <c>f2c7105b</c>.</b> A hull's vertical ride
    /// was ONE <c>WaveMath.Sample</c> at her transform origin, handed straight to the screen. Swept
    /// over wavelength on a pure 1 m train, the ride/wave transfer function of the dory (4.5 m), the
    /// cape islander (12.9 m) and the 110 m tanker read <b>1.000 at every wavelength from 2 m to
    /// 64 m, all three hulls</b>: a 110 m ship followed a 2 m ripple at full amplitude, in lockstep,
    /// with no lag. That is a cork, not a hull — and it is exactly what the owner is describing.</para>
    ///
    /// <para><b>Two physical facts put the weight back, and this block is their dials.</b>
    /// <list type="number">
    /// <item><b>A hull is a FOOTPRINT, not a point.</b> Sampled at N points along her waterline and
    /// averaged, a wave shorter than the hull cancels under her (she bridges it) while a wave longer
    /// than the hull lifts the whole boat. That is a <c>sinc(L/λ)</c> low-pass she gets for free from
    /// her own length — no tuning, and the fleet's spread falls out of the hull list.</item>
    /// <item><b>A hull has a NATURAL PERIOD.</b> A floating body heaves as a mass on the spring of
    /// its own waterplane: <c>T = 2π√(m / (ρ g A_wp))</c>. She answers a long swell nearly fully,
    /// resonates near her own period, and cannot follow anything much faster. That is the lag,
    /// the settle, and the absence of twitch.</item>
    /// </list></para>
    ///
    /// <para><b>Why it lives in Core (beside <see cref="StormRockSettings"/>).</b> Same split, same
    /// reason: this is world-wide policy carried on the <c>GameConfig</c> the owner tunes, and Core
    /// cannot reference Boats (rule 4). The consuming math is Boats-side
    /// (<c>HullHeaveResponseMath</c>), and the PER-HULL character stays where it already lives — a
    /// hull's own <c>BoatHullDef</c> draught and <c>SeakeepingDamping</c>. There is deliberately no
    /// second per-hull knob.</para>
    ///
    /// <para><b><see cref="Enabled"/> off is the A/B.</b> With it off the ride is the point sample
    /// and the storm-gated chase exactly as <c>f2c7105b</c> drew them, bit for bit — that is the
    /// negative control the tests pin, and it is the one number that reverses this whole PR.</para>
    /// </summary>
    [Serializable]
    public struct HullWeightSettings
    {
        [Tooltip("Master switch. ON = a hull rides her own footprint through her own natural period. " +
                 "OFF restores the point-sampled, surface-bolted ride exactly (bit-identical), which " +
                 "is the A/B the tests pin — one number reverses the whole change.\n\n" +
                 "⚠ Unlike the storm block, this one has NO calm identity by construction: putting " +
                 "weight under the hull is precisely what changes the CALM ride, because the calm " +
                 "ride is what the owner called the defect.")]
        public bool Enabled;

        [Header("The footprint (a wave shorter than the hull averages out under her)")]
        [Tooltip("Nominal spacing between waterline sample points (metres). The sample count is " +
                 "round(length / this), clamped into [Min, Max] below — so a 4.5 m dory takes 3 " +
                 "points and a 12.9 m cape islander takes 7. Smaller = a finer footprint and more " +
                 "cost per hull per frame; larger = a coarser one. 2 m resolves every wave the " +
                 "shipped field carries at a hull's scale (its shortest secondary train is 0.22 × " +
                 "the dominant, which at a working breeze is still ~3 m).")]
        [Min(0.25f)] public float FootprintSampleSpacingMeters;

        [Tooltip("Floor on the sample count. 3 (bow, amidships, stern) is the fewest that can " +
                 "average a wave at all AND still read a bow-to-stern difference; 1 would BE the " +
                 "point sample this change exists to retire, and it is the sabotage arm the tests " +
                 "require to fail.")]
        [Min(3)] public int MinFootprintSamples;

        [Tooltip("Ceiling on the sample count — the rule-7 cost guard. Every sample is one " +
                 "WaveMath.Sample per hull per frame; measured at 460 ns over the shipped field's " +
                 "8 live trains, so Nine Mile Creek's 30 moored hulls plus the player cost 0.10 ms " +
                 "of a 16.7 ms frame (they were 0.014 ms as point samples), and the longest hull in " +
                 "the game asks for 55 samples = 0.025 ms on her own.\n\n" +
                 "⚠ 65 is a GUARD, not a budget: it is set so that no hull on the ladder ever " +
                 "samples COARSER than the spacing above, because a footprint sampled at spacing s " +
                 "reads a wave of exactly λ = s at FULL amplitude — the sampling alias. Measured " +
                 "with a cap of 9: the 110 m tanker sampled every 12.2 m and rode a 12 m wave at " +
                 "0.82 where her true gain is 0.03, which is the twitch this whole change exists to " +
                 "remove, reintroduced on the one hull that should be steadiest. If you lower this, " +
                 "check the longest hull in the fleet against the field's dominant wavelength.")]
        [Min(3)] public int MaxFootprintSamples;

        [Header("The natural period T = 2π√(m / ρ g A_wp)")]
        [Tooltip("Waterplane coefficient C_wp: what fraction of her length × beam rectangle her " +
                 "waterplane actually fills (A_wp = L × B × C_wp). A pontoon is 1.0, a fine yacht " +
                 "~0.7; 0.85 is the standard working-displacement-hull figure and is what the fleet " +
                 "ships at.")]
        [Range(0.3f, 1f)] public float WaterplaneCoefficient;

        [Tooltip("Block coefficient C_b: what fraction of her length × beam × draught box her " +
                 "underwater body fills. It is how her DISPLACEMENT is derived (Archimedes: " +
                 "m = ρ · L · B · draught · C_b), because BoatHullDef.MassKg is a physics-body mass " +
                 "for the 2D helm and is NOT her displacement — the cape islander carries 6 000 kg " +
                 "against a ~44 t block estimate, and feeding it to the period formula lands her at " +
                 "0.67 s instead of the 2.7 s a 42-footer actually heaves at. A barge is 0.85, a " +
                 "fine launch 0.4; 0.55 is a working hull.")]
        [Range(0.2f, 1f)] public float BlockCoefficient;

        [Tooltip("Added mass in heave, as a multiple of her displacement — the water she must " +
                 "accelerate with her. For a shallow-draught hull heaving vertically it is close to " +
                 "the displaced mass itself, so 1.0 is the textbook first approximation and the one " +
                 "that lands the fleet on the periods a real boat of each size keeps.")]
        [Min(0f)] public float AddedMassFactor;

        [Tooltip("Seawater density (kg/m³) — North Atlantic. It appears in BOTH the displacement and " +
                 "the waterplane stiffness and therefore cancels out of the period entirely; it is " +
                 "here because the formula is written honestly, not because the number is a dial.")]
        [Min(1f)] public float WaterDensityKgPerCubicMeter;

        [Tooltip("Guard floor on the resulting natural period (seconds). The shipped fleet spans " +
                 "1.25 s (dory) to 5.82 s (tanker), so these never bind — they exist so a hull " +
                 "authored with a zero or absurd draught cannot hand the integrator a spring stiff " +
                 "enough to explode. A guard, not a tunable.")]
        [Min(0.05f)] public float MinNaturalPeriodSeconds;

        [Tooltip("Guard ceiling on the natural period (seconds). See the floor.")]
        [Min(0.1f)] public float MaxNaturalPeriodSeconds;

        [Header("Damping ratio (per hull, from her own BoatHullDef.SeakeepingDamping)")]
        [Tooltip("Floor on the damping ratio. The hull's own SeakeepingDamping IS the ratio — it " +
                 "already runs as a clean ladder with size across the fleet (dory 0, punt 0.15, " +
                 "console skiff 0.5, cape islander 0.65, side dragger 0.75, tanker 0.9) — but a " +
                 "ratio of 0 is an undamped spring that RINGS forever, and no hull in water does " +
                 "that. 0.35 leaves the liveliest boats one clear overshoot (31 % of the step) and " +
                 "then a settle: under-damped enough to feel the sea, never ringing.")]
        [Range(0f, 2f)] public float MinDampingRatio;

        [Tooltip("Ceiling on the damping ratio. 1.0 is critical damping — she returns to the water " +
                 "as fast as she can without overshoot. Above it a hull wallows, which no hull on " +
                 "this ladder should.")]
        [Range(0.1f, 3f)] public float MaxDampingRatio;

        /// <summary>
        /// The shipped tuning. Footprint sampled every 2 m (3 points on the dory, 7 on the cape
        /// islander, 55 on the tanker — the 65-point cap is a guard nothing on the ladder reaches); the period from a 0.55 block / 0.85
        /// waterplane hull carrying one displacement of added mass, which puts the fleet at
        /// dory 1.25 s · lobster boat 2.60 s · cape islander 2.70 s · side dragger 3.89 s ·
        /// tanker 5.82 s; the damping ratio each hull's own <c>SeakeepingDamping</c>, floored at
        /// 0.35 so the dory does not ring and capped at critical so nothing wallows.
        /// </summary>
        public static HullWeightSettings Default => new HullWeightSettings
        {
            Enabled = true,
            FootprintSampleSpacingMeters = 2f,
            MinFootprintSamples = 3,
            MaxFootprintSamples = 65,
            WaterplaneCoefficient = 0.85f,
            BlockCoefficient = 0.55f,
            AddedMassFactor = 1f,
            WaterDensityKgPerCubicMeter = 1025f,
            MinNaturalPeriodSeconds = 0.5f,
            MaxNaturalPeriodSeconds = 8f,
            MinDampingRatio = 0.35f,
            MaxDampingRatio = 1f,
        };
    }
}
