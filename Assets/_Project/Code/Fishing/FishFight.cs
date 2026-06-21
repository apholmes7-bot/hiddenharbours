using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>How a fight ended (or that it's still going).</summary>
    public enum FishFightResult { None = 0, Landed, Snapped }

    /// <summary>
    /// Tuning for one fight, in normalised 0..1 meter space, per second. Derived from the fish
    /// (weight + category) by <see cref="For"/> — bigger/stronger fish get a tighter tension band,
    /// more surges, and a longer haul. All values are data-derived, none hard-coded in the fight logic.
    ///
    /// Forgiving-cove invariants (kept true across the whole strength range):
    ///  • <c>TensionRisePerSec &gt; LandingFillPerSec</c> — you can never just hold to win; a sustained
    ///    reel always snaps before it lands (the core skill: pulse, don't pin).
    ///  • <c>SurgePressure &lt; TensionFallPerSec</c> — easing ALWAYS recovers tension, even mid-surge,
    ///    so a surge is a "back off now" tell, never an unavoidable snap (P5 cozy-with-teeth, gentle end).
    /// </summary>
    public readonly struct FishFightTuning
    {
        public readonly float TensionRisePerSec;  // while reeling
        public readonly float TensionFallPerSec;  // while easing
        public readonly float LandingFillPerSec;  // while reeling
        public readonly float SurgeIntervalSec;   // avg gap between surges (0 = no surges)
        public readonly float SurgeDurationSec;   // how long a surge lasts
        public readonly float SurgePressure;      // extra tension/sec during a surge
        public readonly bool  SnapEnabled;        // false for the hand-gather "tend" variant

        public FishFightTuning(float risePerSec, float fallPerSec, float landingPerSec,
                               float surgeInterval, float surgeDuration, float surgePressure, bool snapEnabled)
        {
            TensionRisePerSec = risePerSec;
            TensionFallPerSec = fallPerSec;
            LandingFillPerSec = landingPerSec;
            SurgeIntervalSec = surgeInterval;
            SurgeDurationSec = surgeDuration;
            SurgePressure = surgePressure;
            SnapEnabled = snapEnabled;
        }

        /// <summary>
        /// Build the tuning for a species + rolled weight. Shellfish/Tidepool are hand-gathered, so they
        /// get the lighter "tend" variant (no snap, quick fill). Everything else fights on a tension
        /// band that tightens with strength. Strength blends a per-category base with where the rolled
        /// weight sits in the species' size range.
        /// </summary>
        public static FishFightTuning For(FishCategory category, float weightKg, float minKg, float maxKg)
        {
            if (IsHandGathered(category))
            {
                // Tend: hold to gently bring it in. No tension danger, no surges — the "lighter" variant.
                return new FishFightTuning(
                    risePerSec: 0f, fallPerSec: 0.4f, landingPerSec: 0.7f,
                    surgeInterval: 0f, surgeDuration: 0f, surgePressure: 0f, snapEnabled: false);
            }

            float weight01 = (maxKg > minKg) ? Mathf.Clamp01((weightKg - minKg) / (maxKg - minKg)) : 0f;
            float strength = Mathf.Clamp01(0.5f * CategoryBase(category) + 0.5f * weight01);

            // Bigger = tension rises faster (tighter band) and lands slower (longer fight); easing stays
            // generous so the fight reads as forgiving. Surges get more frequent with strength.
            return new FishFightTuning(
                risePerSec:    Mathf.Lerp(0.60f, 1.00f, strength),
                fallPerSec:    Mathf.Lerp(0.70f, 0.50f, strength),
                landingPerSec: Mathf.Lerp(0.40f, 0.22f, strength),
                surgeInterval: Mathf.Lerp(3.0f, 1.6f, strength),
                surgeDuration: 0.8f,
                surgePressure: Mathf.Lerp(0.15f, 0.40f, strength),
                snapEnabled:   true);
        }

        public static bool IsHandGathered(FishCategory category)
            => category == FishCategory.Shellfish || category == FishCategory.Tidepool;

        // A per-category difficulty floor (0 easy .. 1 hard). Kept gentle for the cove's starter fish.
        private static float CategoryBase(FishCategory category) => category switch
        {
            FishCategory.InshoreGroundfish => 0.40f,
            FishCategory.Estuary           => 0.40f,
            FishCategory.Pelagic           => 0.55f,
            FishCategory.Deepwater         => 0.70f,
            FishCategory.Storm             => 0.85f,
            FishCategory.Legendary         => 1.00f,
            _                              => 0.50f,
        };
    }

    /// <summary>
    /// The pure, deterministic fishing-fight simulation (VS-13). Drive it with <see cref="Tick"/> each
    /// frame, passing whether the player is holding (reel) or not (ease):
    ///  • reel  → landing gauge fills, tension rises toward the snap zone;
    ///  • ease  → tension falls while the fish runs; landing pauses;
    ///  • surge → the fish pulls hard (extra tension) — ease through it, reel when it tires.
    /// Lands when <see cref="Landing01"/> reaches 1; snaps when <see cref="Tension01"/> reaches 1.
    ///
    /// Engine-light POCO (CLAUDE.md §5): no MonoBehaviour, RNG injected, so it's fully unit-testable.
    /// The fight is real-time, NOT part of the deterministic world-sim contract (it doesn't read the
    /// world seed/clock) — seeding here is only so tests can pin surge timing.
    /// </summary>
    public sealed class FishFight
    {
        private readonly FishFightTuning _t;
        private readonly System.Random _rng;
        private float _surgeCooldown;   // seconds until the next surge can start
        private float _surgeRemaining;  // seconds left in the active surge

        public float Tension01 { get; private set; }
        public float Landing01 { get; private set; }
        public FishFightResult Result { get; private set; }
        public bool IsSurging => _surgeRemaining > 0f;
        public bool IsOver => Result != FishFightResult.None;

        public FishFight(in FishFightTuning tuning, System.Random rng)
        {
            _t = tuning;
            _rng = rng ?? new System.Random();
            _surgeCooldown = NextSurgeDelay();
        }

        /// <summary>Advance the fight by <paramref name="dt"/> seconds. <paramref name="reeling"/> is the
        /// single hold/release input (true = hold to reel/tend).</summary>
        public void Tick(float dt, bool reeling)
        {
            if (IsOver || dt <= 0f) return;

            UpdateSurge(dt);
            float surge = IsSurging ? _t.SurgePressure : 0f;

            // Tension: reeling pushes up (plus any surge); easing pulls down, but a surge resists it.
            // The SurgePressure < TensionFallPerSec invariant guarantees easing still nets downward.
            float tensionDelta = (reeling ? _t.TensionRisePerSec : -_t.TensionFallPerSec) + surge;
            Tension01 = Mathf.Clamp01(Tension01 + tensionDelta * dt);

            // Landing only progresses while reeling; easing pauses it (the fish runs).
            if (reeling)
                Landing01 = Mathf.Clamp01(Landing01 + _t.LandingFillPerSec * dt);

            // Resolve: a maxed line snaps (if snapping is enabled); a full landing gauge lands the fish.
            if (_t.SnapEnabled && Tension01 >= 1f) Result = FishFightResult.Snapped;
            else if (Landing01 >= 1f)              Result = FishFightResult.Landed;
        }

        private void UpdateSurge(float dt)
        {
            if (_t.SurgeIntervalSec <= 0f) return;   // tend variant: never surges
            if (_surgeRemaining > 0f) { _surgeRemaining -= dt; return; }

            _surgeCooldown -= dt;
            if (_surgeCooldown <= 0f)
            {
                _surgeRemaining = _t.SurgeDurationSec;
                _surgeCooldown = NextSurgeDelay();
            }
        }

        // Jitter the gap around the interval (0.7x..1.3x) so surges don't feel metronomic. Deterministic
        // for a seeded rng (tests), varied in play.
        private float NextSurgeDelay()
            => _t.SurgeIntervalSec * (0.7f + (float)_rng.NextDouble() * 0.6f);
    }
}
