using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The PURE, engine-light selection math that grades a boat's BOW SPRAY by hull SIZE + WEIGHT + SPEED —
    /// the owner's brief: spray is a <i>speed</i> phenomenon, and "the dory will be the slowest in game …
    /// it will only be gradual compared to faster moving boats". It reuses the exact machinery the graded
    /// wake plume proved out (<see cref="WakeGrading"/>'s normalize/blend/tier/ramp cores) with its OWN
    /// tunable config, deliberately tuned <b>speed-forward</b>:
    /// <list type="bullet">
    /// <item><description><b>Heavier speed weight</b> (default 0.65 vs the wake's 0.40) — the bow throws
    /// spray because it hits water fast, far more than because the hull is big.</description></item>
    /// <item><description><b>Higher speed onset</b> — the ramp starts just BELOW the dory's real rowed top
    /// speed (<b>2.0 m/s, MEASURED</b> on real physics in <c>PilotableFleetPlayTests</c>), so rowing hard
    /// <i>just</i> starts a subtle occasional spray, and the full sheet is reserved for speeds the dory cannot
    /// reach (the faster hulls: the fishing skiff 2.4, the console 3.8, the sport skiffs 4.6 / 5.6 m/s).
    /// <para><b>Do not re-derive that top speed from the stats.</b> An <c>OarPower/ForwardDrag</c> ratio is
    /// wrong twice over — BOTH oars pull (<see cref="BoatController.OarThrust"/> sums them), and the
    /// rigidbody's own <c>linearDamping</c> is ~40–50% of the dory's resistance and appears in no stat. The
    /// old note here read "300/120 ≈ 2.5 m/s" and was wrong on both counts: she really did <b>2.95</b>, which
    /// is how a boat the owner calls the slowest in the game ended up throwing spray she was never meant to
    /// throw. She is now tuned (ForwardDrag 215) and MEASURED at 2.0. Measure; never restate.</para></description></item>
    /// </list>
    ///
    /// <para><b>No magic numbers (CLAUDE.md rule 6).</b> Every reference range, blend weight, threshold and
    /// onset arrives via <see cref="BowSprayGradeConfig"/>, serialized on the owner-facing
    /// <see cref="BoatWakeEmitter"/>. <b>Determinism / purity (rule 5).</b> Side-effect-free static functions
    /// — no RNG, no state, no sim coupling; VISUAL-only and EditMode-testable headless. Future heavier hulls
    /// scale automatically because it drives purely off <see cref="BoatHullDef.LengthMeters"/> /
    /// <see cref="BoatHullDef.MassKg"/> / live speed — never a per-hull hard-code.</para>
    /// </summary>
    public static class BowSprayGrading
    {
        /// <summary>
        /// The blended spray magnitude in 0..1 — same normalize-then-weight-blend as the wake
        /// (<see cref="WakeGrading.Blend01"/>) against the spray's own reference ranges and speed-forward
        /// weights. Monotonic non-decreasing in each input. Pure + static.
        /// </summary>
        public static float Magnitude01(float lengthMeters, float massKg, float speed, in BowSprayGradeConfig c)
        {
            float s = WakeGrading.Normalize01(lengthMeters, c.LengthRefMin, c.LengthRefMax);
            float w = WakeGrading.Normalize01(massKg, c.MassRefMin, c.MassRefMax);
            float v = WakeGrading.Normalize01(speed, c.SpeedRefMin, c.SpeedRefMax);
            return WakeGrading.Blend01(s, w, v, c.WeightSize, c.WeightMass, c.WeightSpeed);
        }

        /// <summary>Map a 0..1 spray magnitude to a tier via the shared defensive-sorted threshold core. Pure + static.</summary>
        public static int TierIndex(float magnitude01, in BowSprayGradeConfig c)
            => WakeGrading.TierIndexCore(magnitude01, c.Threshold1, c.Threshold2, c.Threshold3);

        /// <summary>One-call selection: blend the three inputs, then pick the spray tier. Monotonic. Pure + static.</summary>
        public static int SelectTier(float lengthMeters, float massKg, float speed, in BowSprayGradeConfig c)
            => TierIndex(Magnitude01(lengthMeters, massKg, speed, c), c);

        /// <summary>
        /// The spray's speed-onset ramp, 0..1: 0 at/below <paramref name="c"/>.SpraySpeedOnset, saturating 1
        /// over the next SpraySpeedOnsetRange (m/s). This is THE dory-gentleness lever: the onset (1.7 m/s) sits
        /// just under the dory's MEASURED 2.0 m/s top speed and the ramp tops out far beyond it, so the slowest
        /// boat in the game only ever sees the very bottom of this ramp — ~15% opacity flat-out, nothing at
        /// cruise. Monotonic non-decreasing in speed. Pure + static.
        /// </summary>
        public static float SpeedOnset(float speed, in BowSprayGradeConfig c)
            => WakeGrading.Ramp01(speed, c.SpraySpeedOnset, c.SpraySpeedOnsetRange);

        /// <summary>
        /// The continuous spray size multiplier for a magnitude — a smooth ramp SprayMinScale→SprayMaxScale
        /// layered on the discrete tier sprite, so the spray grows smoothly within and across tiers (no pop at
        /// a threshold). Never negative. Pure + static.
        /// </summary>
        public static float SprayScale(float magnitude01, in BowSprayGradeConfig c)
            => Mathf.Max(0f, Mathf.Lerp(c.SprayMinScale, c.SprayMaxScale, Mathf.Clamp01(magnitude01)));

        /// <summary>
        /// Where the spray's IMPACT pivot goes: at the boat's actual BOW (half the hull length ahead of the
        /// boat origin, which sits at the hull's centre) plus a small tunable nudge — the cutwater, where the
        /// stem actually hits the water. The mirror of <see cref="WakeGrading.SternAnchor"/>. A degenerate bow
        /// vector falls back to +Y so the anchor is never NaN. Pure + static.
        /// </summary>
        public static Vector2 BowAnchor(Vector2 boatPos, Vector2 bow, float hullLengthMeters, float aheadOffset)
        {
            Vector2 dir = bow.sqrMagnitude > 1e-8f ? bow.normalized : Vector2.up;
            float ahead = Mathf.Max(0f, hullLengthMeters) * 0.5f + aheadOffset;
            return boatPos + dir * ahead;
        }
    }

    /// <summary>
    /// Every tunable that grades the bow spray — in one serialized struct so the selection stays free of
    /// magic numbers (CLAUDE.md rule 6). <see cref="BoatWakeEmitter"/> serializes an owner-editable instance.
    /// Defaults are deliberately SPEED-FORWARD and gentle on the dory: below 1.7 m/s she shows no spray at all,
    /// and at a flat-out row (2.0 m/s — her MEASURED terminal speed, see <see cref="BowSprayGrading"/>) it is a
    /// barely-there wisp; the full prominent sheet only appears at speeds the dory cannot reach.
    /// </summary>
    [System.Serializable]
    public struct BowSprayGradeConfig
    {
        [Header("Master switch")]
        [Tooltip("Draw the graded bow spray at the cutwater. Off = no spray at all (the wake is untouched).")]
        public bool SprayEnabled;

        [Header("Reference ranges (normalize each input to 0..1)")]
        [Tooltip("Hull length (m) mapped to 0 — the smallest reference. The dory (~4.5 m) sits near the bottom.")]
        public float LengthRefMin;
        [Tooltip("Hull length (m) mapped to 1 — a big offshore hull. Beyond the biggest planned hull so size " +
                 "never saturates early.")]
        public float LengthRefMax;
        [Tooltip("Hull mass (kg) mapped to 0. The dory (~400 kg) sits near the bottom.")]
        public float MassRefMin;
        [Tooltip("Hull mass (kg) mapped to 1 — a laden trader/dragger.")]
        public float MassRefMax;
        [Tooltip("Boat speed (m/s) mapped to 0 for the magnitude blend. Kept at the spray's own onset speed — " +
                 "below it, speed contributes nothing (spray is a speed phenomenon).")]
        public float SpeedRefMin;
        [Tooltip("Boat speed (m/s) mapped to 1 — a fast hull driven hard. Set far beyond the dory's reach (her " +
                 "rowed terminal MEASURES 2.0 m/s) so the top of the spray scale belongs to the fast hulls — the " +
                 "sport skiffs settle at 4.6 and 5.6 m/s.")]
        public float SpeedRefMax;

        [Header("Blend weights (normalized internally — SPEED-FORWARD by design)")]
        [Tooltip("How much hull SIZE pulls the spray up. Kept low (default 0.20) — spray is about speed.")]
        public float WeightSize;
        [Tooltip("How much hull WEIGHT pulls the spray up. Kept lowest (default 0.15).")]
        public float WeightMass;
        [Tooltip("How much SPEED pulls the spray up — the dominant lever (default 0.65, vs the wake's 0.40): " +
                 "the bow throws spray because it hits water fast.")]
        public float WeightSpeed;

        [Header("Tier thresholds (magnitude 0..1 → Small/Medium/Large/Huge)")]
        [Tooltip("Magnitude at/above which the spray steps from Small (0) to Medium (1).")]
        public float Threshold1;
        [Tooltip("Magnitude at/above which the spray steps from Medium (1) to Large (2).")]
        public float Threshold2;
        [Tooltip("Magnitude at/above which the spray steps from Large (2) to Huge (3).")]
        public float Threshold3;

        [Header("Render (the authored spray sprite at the cutwater)")]
        [Tooltip("Continuous spray size multiplier at magnitude 0 (the faintest spray).")]
        public float SprayMinScale;
        [Tooltip("Continuous spray size multiplier at magnitude 1 (the biggest, fastest hull).")]
        public float SprayMaxScale;
        [Tooltip("How far AHEAD of the bow tip the spray's impact pivot sits (m). The bow tip itself is found " +
                 "from the hull's LengthMeters (the boat origin is the hull centre); this is just the nudge.")]
        public float SprayBowOffset;
        [Tooltip("Spray opacity at FULL speed onset (0..1). The onset ramp scales it down from here, which is " +
                 "what keeps the dory's spray a subtle wisp — it never reaches full onset.")]
        public float SprayStartAlpha;
        [Tooltip("Vertical pivot (0..1) of the authored spray sprite. The art's dense IMPACT churn (the " +
                 "cutwater end) is at the BOTTOM of the image (pixel-verified by WakeArtOrientationTests), so " +
                 "0 pins the impact at the bow and the droplet fan spreads ahead.")]
        public float SprayPivotY;
        [Tooltip("Flip the spray 180° (and mirror its pivot) — the no-code escape hatch if the spray art is " +
                 "ever re-authored with the impact churn at the TOP of the image. Leave OFF for the current " +
                 "art (impact at the bottom, verified from the pixels).")]
        public bool SprayFlip;

        [Header("Speed onset (the dory-gentleness lever)")]
        [Tooltip("Boat speed (m/s) at which spray BEGINS, for EVERY hull — not just the dory. Default 1.7 sits " +
                 "just under her MEASURED 2.0 m/s rowed top speed, so only a flat-out row starts a subtle wisp " +
                 "and cruising shows none. (This number was originally chosen as '⅔ of the dory's 2.5' — but " +
                 "2.5 was a bad derivation and she really did 2.95, so she had been crossing well into a spray " +
                 "she was never meant to throw. Slowing her to 2.0 is what fixed that; 1.7 was left alone " +
                 "deliberately, because it gates the skiffs too and lowering it would retune their spray.)")]
        public float SpraySpeedOnset;
        [Tooltip("Speed range (m/s) over which the spray ramps to full opacity. Default reaches full only " +
                 "beyond the dory's top speed — the prominent sheet belongs to the faster hulls to come.")]
        public float SpraySpeedOnsetRange;

        /// <summary>The greybox default — speed-forward, gentle on the dory. The owner tunes on the component.</summary>
        public static BowSprayGradeConfig Default => new BowSprayGradeConfig
        {
            SprayEnabled = true,

            LengthRefMin = 4f,       // same hull-normalization frame as the wake grade
            LengthRefMax = 20f,
            MassRefMin   = 300f,
            MassRefMax   = 8000f,
            SpeedRefMin  = 1.7f,     // = the onset: below it speed contributes nothing
            SpeedRefMax  = 6f,       // full speed credit belongs to hulls far faster than the dory (the twin: 5.6)

            WeightSize   = 0.20f,    // speed-forward: size and weight together weigh half of speed
            WeightMass   = 0.15f,
            WeightSpeed  = 0.65f,

            Threshold1   = 0.30f,    // the dory tops out well under this flat-out → always the Small spray
            Threshold2   = 0.55f,
            Threshold3   = 0.78f,

            SprayMinScale        = 0.5f,
            SprayMaxScale        = 1.2f,
            SprayBowOffset       = 0.05f,
            SprayStartAlpha      = 0.85f,
            SprayPivotY          = 0f,     // impact churn at the image BOTTOM (pixel-verified)
            SprayFlip            = false,
            SpraySpeedOnset      = 1.7f,   // just under the dory's MEASURED 2.0 m/s rowed terminal (~15% flat-out)
            SpraySpeedOnsetRange = 2.0f,   // full sheet at 3.7 m/s — far beyond the dory's reach
        };
    }
}
