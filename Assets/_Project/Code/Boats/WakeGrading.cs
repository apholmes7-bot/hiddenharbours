using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The PURE, engine-light selection math that grades a boat's wake by hull SIZE + WEIGHT + SPEED — the
    /// owner's brief: "bigger/heavier hulls and higher speed → a bigger wake". Three inputs are each normalized
    /// against a tunable reference range, blended by tunable weights into one <see cref="Magnitude01"/>
    /// (0 = a whisper of a wake, 1 = a full churning plume), then mapped to a discrete wake TIER
    /// (<see cref="TierIndex"/> 0..3 = Small/Medium/Large/Huge) by tunable thresholds. <see cref="BoatWakeEmitter"/>
    /// selects the authored graded sprite for that tier per boat, per tick.
    ///
    /// <para><b>Why a blend (not just speed).</b> Size and weight are mostly static per hull (a laden trader
    /// shoves a bigger wake than the dory even at a crawl); speed is dynamic (the SAME boat throws a bigger
    /// wake pushed hard than idling). Blending all three is the only way both reads are true at once — and it
    /// scales to future heavier hulls automatically because it drives purely off
    /// <see cref="BoatHullDef.LengthMeters"/> / <see cref="BoatHullDef.MassKg"/> / speed, never a per-hull
    /// hard-code.</para>
    ///
    /// <para><b>No magic numbers (CLAUDE.md rule 6).</b> Every reference range, blend weight and tier threshold
    /// arrives via <see cref="WakeGradeConfig"/>, which the owner-facing <see cref="BoatWakeEmitter"/> serializes.
    /// <b>Determinism / purity (rule 5).</b> Side-effect-free static functions of their inputs — no RNG, no state,
    /// no sim coupling; the whole thing is VISUAL-only and EditMode-testable headless.</para>
    /// </summary>
    public static class WakeGrading
    {
        /// <summary>The number of authored wake tiers (Small, Medium, Large, Huge).</summary>
        public const int TierCount = 4;

        /// <summary>
        /// Normalize a raw input to 0..1 against a [min,max] reference range (clamped). A degenerate range
        /// (max ≤ min) collapses to a 0/1 step at <paramref name="min"/> so a mis-tuned config never divides by
        /// zero. Monotonic non-decreasing in <paramref name="value"/>. Pure + static.
        /// </summary>
        public static float Normalize01(float value, float min, float max)
        {
            if (max <= min) return value <= min ? 0f : 1f;
            return Mathf.Clamp01((value - min) / (max - min));
        }

        /// <summary>
        /// The blended wake magnitude in 0..1 from the three inputs. Each is normalized against its reference
        /// range then combined by the config's weights (the weights are normalized internally so they always
        /// sum to 1 — an owner can retune the balance without worrying the total drifts). Monotonic
        /// non-decreasing in EACH input (more size, weight or speed never shrinks the wake). Returns 0 if all
        /// weights are zero. Pure + static.
        /// </summary>
        public static float Magnitude01(float lengthMeters, float massKg, float speed, in WakeGradeConfig c)
        {
            float s = Normalize01(lengthMeters, c.LengthRefMin, c.LengthRefMax);
            float w = Normalize01(massKg, c.MassRefMin, c.MassRefMax);
            float v = Normalize01(speed, c.SpeedRefMin, c.SpeedRefMax);

            float ws = Mathf.Max(0f, c.WeightSize);
            float wm = Mathf.Max(0f, c.WeightMass);
            float wv = Mathf.Max(0f, c.WeightSpeed);
            float sum = ws + wm + wv;
            if (sum <= 1e-6f) return 0f;

            return Mathf.Clamp01((s * ws + w * wm + v * wv) / sum);
        }

        /// <summary>
        /// Map a 0..1 magnitude to a discrete tier index in [0, <see cref="TierCount"/>-1]. The three
        /// thresholds are clamped and sorted ascending defensively, so the mapping is ALWAYS monotonic
        /// non-decreasing in magnitude no matter how the owner tunes them (a mis-ordered threshold can never
        /// make a bigger wake pick a smaller tier). Pure + static.
        /// </summary>
        public static int TierIndex(float magnitude01, in WakeGradeConfig c)
        {
            float m = Mathf.Clamp01(magnitude01);
            float t1 = Mathf.Clamp01(c.Threshold1);
            float t2 = Mathf.Clamp01(Mathf.Max(c.Threshold2, t1));
            float t3 = Mathf.Clamp01(Mathf.Max(c.Threshold3, t2));
            if (m >= t3) return 3;
            if (m >= t2) return 2;
            if (m >= t1) return 1;
            return 0;
        }

        /// <summary>
        /// The one-call selection the emitter uses: blend the three inputs into a magnitude, then pick the tier.
        /// Monotonic non-decreasing in each of length/mass/speed (composition of two monotonic steps). Pure +
        /// static.
        /// </summary>
        public static int SelectTier(float lengthMeters, float massKg, float speed, in WakeGradeConfig c)
            => TierIndex(Magnitude01(lengthMeters, massKg, speed, c), c);

        /// <summary>
        /// The continuous PLUME size multiplier for a given magnitude — a smooth ramp from
        /// <paramref name="c"/>.PlumeMinScale (magnitude 0) to PlumeMaxScale (magnitude 1). Layered ON TOP of
        /// the discrete tier sprite so the wake grows smoothly WITHIN and across tiers (no hard pop at a
        /// threshold): the tier swaps the authored art, this scales it. Never negative. Pure + static.
        /// </summary>
        public static float PlumeScale(float magnitude01, in WakeGradeConfig c)
            => Mathf.Max(0f, Mathf.Lerp(c.PlumeMinScale, c.PlumeMaxScale, Mathf.Clamp01(magnitude01)));

        /// <summary>
        /// Speed-onset ramp for the plume/foam growth, 0..1: 0 at/below <paramref name="c"/>.PlumeSpeedOnset
        /// (no plume at rest), rising linearly to 1 over the next PlumeSpeedOnsetRange (m/s), then saturating.
        /// Monotonic non-decreasing in speed. Pure + static.
        /// </summary>
        public static float SpeedOnset(float speed, in WakeGradeConfig c)
        {
            float range = Mathf.Max(1e-3f, c.PlumeSpeedOnsetRange);
            return Mathf.Clamp01((speed - c.PlumeSpeedOnset) / range);
        }

        /// <summary>
        /// The foam/V-extent growth factor for a given magnitude: 1 at magnitude 0, rising to
        /// <c>1 + FoamMagnitudeInfluence</c> at magnitude 1, so the foam bubbles and the crisp Kelvin arms
        /// grow together with the graded plume (a big hull's whole wake is bigger, not just the plume sprite).
        /// Influence 0 leaves the foam exactly as tuned. Always ≥ 1. Pure + static.
        /// </summary>
        public static float FoamExtentFactor(float magnitude01, in WakeGradeConfig c)
            => 1f + Mathf.Max(0f, c.FoamMagnitudeInfluence) * Mathf.Clamp01(magnitude01);
    }

    /// <summary>
    /// Every tunable that grades the wake by hull size, weight and speed — in one serialized struct so the
    /// selection stays free of magic numbers (CLAUDE.md rule 6). <see cref="BoatWakeEmitter"/> serializes an
    /// owner-editable instance. Defaults grade the current small hulls (Dory ≈ 4.5 m / 400 kg) as
    /// Small at a crawl → Medium underway, and ramp toward Large/Huge with speed and with the future heavier
    /// T2+ hulls — driven purely from LengthMeters/MassKg/speed so new hulls scale automatically (no per-hull
    /// hard-code).
    /// </summary>
    [System.Serializable]
    public struct WakeGradeConfig
    {
        [Header("Reference ranges (normalize each input to 0..1)")]
        [Tooltip("Hull length (m) mapped to 0 → the smallest reference. The dory (~4.5 m) sits near the bottom.")]
        public float LengthRefMin;
        [Tooltip("Hull length (m) mapped to 1 → a big offshore hull. Set beyond the biggest planned hull so " +
                 "size never saturates early.")]
        public float LengthRefMax;
        [Tooltip("Hull mass (kg) mapped to 0. The dory (~400 kg) sits near the bottom.")]
        public float MassRefMin;
        [Tooltip("Hull mass (kg) mapped to 1 → a laden trader/dragger. Set beyond the heaviest planned hull.")]
        public float MassRefMax;
        [Tooltip("Boat speed (m/s) mapped to 0 → the same idle threshold the foam uses (a drifting boat has " +
                 "no speed contribution).")]
        public float SpeedRefMin;
        [Tooltip("Boat speed (m/s) mapped to 1 → a boat driven hard. Above this, speed no longer grows the wake.")]
        public float SpeedRefMax;

        [Header("Blend weights (normalized internally — relative balance)")]
        [Tooltip("How much the (static) hull SIZE pulls the wake up. Default 0.35.")]
        public float WeightSize;
        [Tooltip("How much the (static) hull WEIGHT pulls the wake up. Default 0.25.")]
        public float WeightMass;
        [Tooltip("How much the (dynamic) SPEED pulls the wake up — so the same boat's wake grows when pushed " +
                 "hard. Default 0.40 (the biggest single lever, per the brief).")]
        public float WeightSpeed;

        [Header("Tier thresholds (magnitude 0..1 → Small/Medium/Large/Huge)")]
        [Tooltip("Magnitude at/above which the wake steps from Small (0) to Medium (1).")]
        public float Threshold1;
        [Tooltip("Magnitude at/above which the wake steps from Medium (1) to Large (2).")]
        public float Threshold2;
        [Tooltip("Magnitude at/above which the wake steps from Large (2) to Huge (3).")]
        public float Threshold3;

        [Header("Graded plume render (the authored stern-trailing wake sprite)")]
        [Tooltip("Draw the authored graded plume sprite behind the boat (the primary size read). Off = keep " +
                 "only the procedural foam/crest wake (still grown by FoamMagnitudeInfluence).")]
        public bool PlumeEnabled;
        [Tooltip("Continuous plume size multiplier at magnitude 0 (the smallest wake). <1 shrinks the Small " +
                 "sprite a touch so a dawdling dory's plume is modest.")]
        public float PlumeMinScale;
        [Tooltip("Continuous plume size multiplier at magnitude 1. >1 lets the Huge sprite grow further so the " +
                 "biggest, fastest hull's wake reads as genuinely massive.")]
        public float PlumeMaxScale;
        [Tooltip("How far astern of the stern the plume's apex sits (m) — a small nudge so it trails behind the hull.")]
        public float PlumeAsternOffset;
        [Tooltip("Plume opacity at full onset (0..1). Kept subtler than the white foam — it's the broad wash the " +
                 "foam bubbles and crest lines sit on top of.")]
        public float PlumeStartAlpha;
        [Tooltip("Vertical pivot (0..1) of the authored plume sprite. The art's narrow apex (the boat end) is at " +
                 "the TOP, so 1 pins the apex at the stern and the plume widens astern. Lower it if art is reauthored.")]
        public float PlumePivotY;
        [Tooltip("Boat speed (m/s) at which the plume begins to appear — no plume at rest. Usually the foam threshold.")]
        public float PlumeSpeedOnset;
        [Tooltip("Speed range (m/s) over which the plume ramps from just-appearing to full opacity/scale onset.")]
        public float PlumeSpeedOnsetRange;

        [Header("Foam coupling (grow the whole wake, not just the plume)")]
        [Tooltip("How much the blended magnitude grows the foam bubbles + crisp Kelvin arms (0 = foam exactly " +
                 "as tuned; 0.6 = up to 1.6× the foam footprint at max magnitude) so a big hull's whole wake " +
                 "scales, keeping the foam coherent with the graded plume.")]
        public float FoamMagnitudeInfluence;

        /// <summary>The greybox default grade. The owner tunes on the component.</summary>
        public static WakeGradeConfig Default => new WakeGradeConfig
        {
            LengthRefMin = 4f,
            LengthRefMax = 20f,
            MassRefMin   = 300f,
            MassRefMax   = 8000f,
            SpeedRefMin  = 0.4f,     // the foam's own idle threshold — below it, no speed contribution
            SpeedRefMax  = 5f,

            WeightSize   = 0.35f,
            WeightMass   = 0.25f,
            WeightSpeed  = 0.40f,

            Threshold1   = 0.16f,    // Small → Medium (a dory reaches this when clearly underway)
            Threshold2   = 0.40f,    // Medium → Large (needs real size/weight, or a big hull at speed)
            Threshold3   = 0.66f,    // Large → Huge  (a heavy hull driven hard)

            PlumeEnabled         = true,
            PlumeMinScale        = 0.6f,
            PlumeMaxScale        = 1.15f,
            PlumeAsternOffset    = 0.3f,
            PlumeStartAlpha      = 0.5f,
            PlumePivotY          = 1.0f,
            PlumeSpeedOnset      = 0.4f,
            PlumeSpeedOnsetRange = 2.0f,

            FoamMagnitudeInfluence = 0.6f,
        };
    }
}
