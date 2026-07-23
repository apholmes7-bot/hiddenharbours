using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The PURE, engine-light math that turns the boat wake from a boat-locked stamp into a WORLD-DEPOSITED
    /// TRAIL — the owner's ask (2026-07-23, verbatim): "the boats wakes are currently static lines, they
    /// should be dynamic small waves or at least a representation that leaves a trail behind the boat, same
    /// with bow waves when they crash against the bow."
    ///
    /// <para><b>Why deposition (the design in one breath).</b> The shipped wake emitted every foam puff and
    /// crest streak ALONG a Kelvin-V template hung off the boat's CURRENT pose — up to <c>ArmLength</c> metres
    /// astern of wherever the boat is NOW. Because fresh (brightest) puffs kept appearing at fixed offsets
    /// relative to the hull, the visible pattern was glued to the boat: turn hard and the whole V swings with
    /// you; that is the "static lines" read. The fix is how a real wake works: the disturbance is DEPOSITED at
    /// the track — laid at the stern as she passes — and then <i>spreads laterally</i> where it was laid. The
    /// deposits persist and decay in world space, so the trail traces the boat's actual path, CURVING through
    /// turns, and the Kelvin V is not drawn — it <b>emerges</b>, because a track-line of deposits each
    /// spreading outward at <c>speed·tan(θ)</c> is exactly the stationary V pattern (see
    /// <see cref="ShoulderSpreadSpeed"/>).</para>
    ///
    /// <para><b>Everything here is deterministic, side-effect-free and EditMode-testable headless</b> (rule 5):
    /// the only state (the fractional deposit carries) is threaded by ref exactly like
    /// <see cref="WakeParticleSystem.EmissionCount"/>'s. Every tunable arrives via <see cref="WakeTrailConfig"/>
    /// / <see cref="BowWaveConfig"/>, serialized on <see cref="BoatWakeEmitter"/> (rule 6). Deposit counts are
    /// HARD-CLAMPED per tick so emission can never exceed the fixed pools (rule 7).</para>
    /// </summary>
    public static class WakeTrailMath
    {
        // ==== DEPOSITION (distance-based: the trail is laid per metre of track, not per second) ============

        /// <summary>
        /// How many trail deposits to lay for <paramref name="distanceMeters"/> of stern travel this tick:
        /// one every <paramref name="spacingMeters"/> along the track, with the fractional remainder carried
        /// (<paramref name="carry"/> = metres travelled since the last deposit) so the spacing is exact across
        /// ticks and any speed. Distance-based (not time-based) is what makes the laid trail's density uniform
        /// along the TRACK — the property that reads as "she left this behind". The count is HARD-CLAMPED to
        /// <paramref name="maxPerTick"/> (≥0) so a spike (teleport, giant dt) can never flood the fixed pool
        /// (rule 7); the carry is consumed for the clamped count only, so a clamped tick simply lays the rest
        /// on the next ticks. Non-positive spacing is guarded to a minimum so a mis-tuned config never
        /// divides by zero. Pure + static (the ref carry is the only threaded state).
        /// </summary>
        public static int DepositCount(float distanceMeters, float spacingMeters, ref float carry, int maxPerTick)
        {
            float spacing = Mathf.Max(1e-3f, spacingMeters);
            int max = Mathf.Max(0, maxPerTick);
            if (distanceMeters <= 0f || max == 0) return 0;

            carry += distanceMeters;
            int whole = Mathf.FloorToInt(carry / spacing);
            if (whole > max) whole = max;
            carry -= whole * spacing;
            // Never let the carry hoard more than one spacing after a clamp — a long clamped burst should
            // resume clean spacing, not burp a backlog forever.
            if (carry > spacing * (max + 1)) carry = spacing * (max + 1);
            return whole;
        }

        /// <summary>
        /// Where along the prev→curr stern track the <paramref name="index"/>-th of <paramref name="count"/>
        /// deposits lands, as a 0..1 lerp factor: deposits are spaced evenly across the swept segment, ordered
        /// oldest-first (small t = closer to where she WAS). Even spacing across the segment (rather than the
        /// exact carry phase) keeps the function pure of the carry and is indistinguishable at trail scale.
        /// Degenerate counts clamp safely. Pure + static.
        /// </summary>
        public static float DepositT(int index, int count)
        {
            if (count <= 0) return 1f;
            int i = Mathf.Clamp(index, 0, count - 1);
            return (i + 1f) / count;
        }

        /// <summary>The world point of a deposit: the lerp along the swept stern segment. Pure + static.</summary>
        public static Vector2 PointOnTrack(Vector2 prevStern, Vector2 currStern, float t01)
            => Vector2.Lerp(prevStern, currStern, Mathf.Clamp01(t01));

        /// <summary>
        /// The unit direction the boat's stern swept this tick (prev→curr). When the segment is degenerate
        /// (she barely moved) it falls back to the boat's live bow direction so a deposit never gets a NaN
        /// frame. Pure + static.
        /// </summary>
        public static Vector2 TrackDir(Vector2 prevStern, Vector2 currStern, Vector2 fallbackBow)
        {
            Vector2 d = currStern - prevStern;
            if (d.sqrMagnitude > 1e-8f) return d.normalized;
            Vector2 b = fallbackBow;
            return b.sqrMagnitude > 1e-8f ? b.normalized : Vector2.up;
        }

        /// <summary>The left-hand perpendicular of a unit track direction (side +1); side −1 mirrors it.</summary>
        public static Vector2 Lateral(Vector2 trackDir, int side)
            => new Vector2(-trackDir.y, trackDir.x) * (side >= 0 ? 1f : -1f);

        /// <summary>
        /// Where a SHOULDER deposit (one of the two wavelet lines that become the V arms) is laid: the track
        /// point pushed <paramref name="halfWidthMeters"/> to one side, perpendicular to the TRACK (not the
        /// live heading — the trail belongs to where she was). Pure + static.
        /// </summary>
        public static Vector2 ShoulderPoint(Vector2 trackPoint, Vector2 trackDir, int side, float halfWidthMeters)
            => trackPoint + Lateral(trackDir, side) * Mathf.Max(0f, halfWidthMeters);

        /// <summary>
        /// The half-width (m) the shoulder deposits start at: a tunable fraction of the hull's length (the
        /// closest stable stand-in for beam — <c>BoatHullDef</c> carries no beam), grown by the wake grade so
        /// a big/fast hull lays a wider trail. Always ≥ 0. Pure + static.
        /// </summary>
        public static float ShoulderHalfWidth(float hullLengthMeters, float magnitude01, in WakeTrailConfig c)
        {
            float baseHalf = Mathf.Max(0f, hullLengthMeters) * Mathf.Max(0f, c.ShoulderHalfWidthFraction);
            return baseHalf * (1f + Mathf.Max(0f, c.WidthMagnitudeBoost) * Mathf.Clamp01(magnitude01));
        }

        /// <summary>
        /// The lateral SPREAD speed (m/s) a freshly-laid shoulder deposit moves outward at —
        /// <c>boatSpeed · tan(kelvinHalfAngle)</c>, clamped to a tunable floor/ceiling. This single line is
        /// what makes the Kelvin V an EMERGENT, world-locked pattern: deposits laid along the track and
        /// spreading outward at this rate form straight arms at exactly the half-angle behind a straight
        /// run, and a curved, still-spreading trail behind a turn — the trail geometry the owner asked for.
        /// The velocity decay then slows the spread with age (arms soften far astern), which reads natural.
        /// Monotonic non-decreasing in speed between the clamps. Pure + static.
        /// </summary>
        public static float ShoulderSpreadSpeed(float boatSpeed, in WakeTrailConfig c)
        {
            float tan = Mathf.Tan(Mathf.Clamp(c.KelvinHalfAngleDeg, 0f, 80f) * Mathf.Deg2Rad);
            float v = Mathf.Max(0f, boatSpeed) * tan;
            return Mathf.Clamp(v, Mathf.Max(0f, c.SpreadSpeedMin), Mathf.Max(c.SpreadSpeedMin, c.SpreadSpeedMax));
        }

        /// <summary>
        /// A shoulder deposit's birth velocity: outward (perpendicular to the track, per side) at the spread
        /// speed, plus a small astern drift (a fraction of boat speed — the wash the hull dragged along).
        /// The existing per-particle decay then bleeds both away until only the tidal current moves it.
        /// Pure + static.
        /// </summary>
        public static Vector2 ShoulderVelocity(Vector2 trackDir, int side, float spreadSpeed, float boatSpeed,
                                               in WakeTrailConfig c)
            => Lateral(trackDir, side) * Mathf.Max(0f, spreadSpeed)
               - trackDir * (Mathf.Max(0f, boatSpeed) * Mathf.Clamp01(c.AsternDriftFraction));

        /// <summary>A graded lerp between a min/max pair by the wake magnitude — the one shape every trail
        /// grading knob (size, lifetime) uses. Clamped magnitude; never returns below min(a,b). Pure + static.</summary>
        public static float Graded(float atMagnitude0, float atMagnitude1, float magnitude01)
            => Mathf.Lerp(atMagnitude0, atMagnitude1, Mathf.Clamp01(magnitude01));

        // ==== the LIVE plume (the boat-attached churn is allowed to be attached — but must be alive) ========

        /// <summary>
        /// The drawn heading's turn rate (deg/s) from two successive bow directions — what the plume's turn
        /// fade reads. dt ≤ 0 or a degenerate bow returns 0 (never NaN). Pure + static.
        /// </summary>
        public static float HeadingRateDegPerSec(Vector2 prevBow, Vector2 bow, float dt)
        {
            if (dt <= 0f || prevBow.sqrMagnitude <= 1e-8f || bow.sqrMagnitude <= 1e-8f) return 0f;
            return Mathf.Abs(Vector2.SignedAngle(prevBow, bow)) / dt;
        }

        /// <summary>
        /// How much of the rigid authored plume survives a turn, 0..1: 1 below <paramref name="c"/>.
        /// PlumeTurnFadeOnsetDegPerSec, easing to 0 over the next PlumeTurnFadeRangeDegPerSec. The authored
        /// plume is a straight V — honest on a straight run, a lie in a hard turn (it cannot bend). Fading it
        /// with turn rate hands the turn to the deposited trail, which CAN curve. Monotonic non-increasing
        /// in turn rate; degenerate range collapses to a step. Pure + static.
        /// </summary>
        public static float TurnFade01(float turnRateDegPerSec, in WakeTrailConfig c)
        {
            float range = Mathf.Max(1e-3f, c.PlumeTurnFadeRangeDegPerSec);
            return 1f - Mathf.Clamp01((Mathf.Abs(turnRateDegPerSec) - Mathf.Max(0f, c.PlumeTurnFadeOnsetDegPerSec)) / range);
        }

        /// <summary>
        /// The CHURN PULSE — a deterministic, bounded multiplier around 1 that makes the boat-attached
        /// plume/spray sprites read as living churn instead of a decal: two incommensurate sine bands (so the
        /// beat never visibly loops) keyed by time + a per-boat seed. Guaranteed within
        /// [1 − amount, 1 + amount]; amount 0 returns exactly 1 (the decal behaviour, for A/B). Same inputs,
        /// same output — no RNG (rule 5). Pure + static.
        /// </summary>
        public static float ChurnPulse(float time, float seed, float hz, float amount)
        {
            float a = Mathf.Max(0f, amount);
            if (a <= 0f) return 1f;
            float w = 2f * Mathf.PI * Mathf.Max(0f, hz);
            float phase = seed * 12.9898f;
            // 0.62/0.38 split keeps |sum| ≤ 1 while the 1.73× band de-loops the beat.
            float s = Mathf.Sin(time * w + phase) * 0.62f + Mathf.Sin(time * w * 1.73f + phase * 2.17f) * 0.38f;
            return 1f + s * a;
        }

        // ==== BOW WAVE (droplets thrown at the cutwater, deposited in world space) =========================

        /// <summary>
        /// How many bow droplets to shed this tick: <paramref name="ratePerSecond"/> scaled by the spray's
        /// 0..1 speed-onset ramp (0 at rest — no bow wave without way on), integrated over dt with the
        /// fractional carry, HARD-CLAMPED to <paramref name="maxPerTick"/> so the droplet pool can never be
        /// flooded (rule 7). Carry resets while gated so a stopped boat never "burps" a sheet on restart —
        /// the same discipline as <see cref="WakeParticleSystem.EmissionCount"/>. Pure + static.
        /// </summary>
        public static int DropletCount(float onset01, float ratePerSecond, float dt, ref float carry, int maxPerTick)
        {
            int max = Mathf.Max(0, maxPerTick);
            if (onset01 <= 0f || dt <= 0f || max == 0)
            {
                carry = 0f;
                return 0;
            }
            carry += Mathf.Max(0f, ratePerSecond) * Mathf.Clamp01(onset01) * dt;
            int whole = Mathf.FloorToInt(carry);
            if (whole > max) whole = max;
            carry -= whole;
            if (carry > max + 1f) carry = max + 1f;
            return whole;
        }

        /// <summary>
        /// A bow droplet's birth velocity: thrown FORWARD off the cutwater inside a fan of
        /// ±<paramref name="c"/>.FanHalfAngleDeg around the bow direction (<paramref name="fan01"/> −1..1
        /// picks the ray deterministically), at a tunable fraction of boat speed — the water the stem throws
        /// aside, which the boat then drives PAST, leaving the droplets astern in world space (the crash
        /// read). Degenerate bow falls back to +Y. Pure + static.
        /// </summary>
        public static Vector2 DropletVelocity(Vector2 bow, float boatSpeed, float fan01, in BowWaveConfig c)
        {
            Vector2 fwd = bow.sqrMagnitude > 1e-8f ? bow.normalized : Vector2.up;
            float ang = Mathf.Clamp(fan01, -1f, 1f) * Mathf.Clamp(c.FanHalfAngleDeg, 0f, 89f) * Mathf.Deg2Rad;
            float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
            Vector2 dir = new Vector2(fwd.x * cs - fwd.y * sn, fwd.x * sn + fwd.y * cs);
            return dir * (Mathf.Max(0f, boatSpeed) * Mathf.Max(0f, c.DropletSpeedScale));
        }
    }

    /// <summary>
    /// Every tunable of the DEPOSITED trail + the live plume, in one serialized struct (rule 6 — no magic
    /// numbers; <see cref="BoatWakeEmitter"/> serializes an owner-editable instance). Defaults lay a clearly
    /// visible curving trail behind the greybox fleet without flooding the shipped pools.
    /// </summary>
    [System.Serializable]
    public struct WakeTrailConfig
    {
        [Header("Master switch")]
        [Tooltip("Lay the world-deposited trail (the owner's ask). Off = the legacy boat-locked V stamp.")]
        public bool Enabled;

        [Header("Deposition (per metre of track, not per second)")]
        [Tooltip("Metres of stern travel between trail deposits. Smaller = a denser, more continuous trail " +
                 "(and more pool pressure). One deposit = 2 shoulder wavelets + up to 1 centre churn puff.")]
        public float DepositSpacingMeters;
        [Tooltip("Extra nudge (m) past the transom where the trail is laid (on top of the hull-length stern " +
                 "anchor). Small — the trail starts just clear of the hull.")]
        public float DepositAsternOffset;
        [Tooltip("Hard cap on deposits laid in one tick — the pool-safety valve. Emission can NEVER exceed " +
                 "this per tick, whatever the dt/speed spike (rule 7).")]
        public int MaxDepositsPerTick;
        [Tooltip("A stern jump longer than this in one tick (region travel, dev teleport) RESETS the trail " +
                 "instead of laying a straight line of foam across the map.")]
        public float TeleportResetMeters;

        [Header("The emergent V (spread where laid)")]
        [Tooltip("The Kelvin half-angle (deg) the emergent V opens at: shoulder deposits spread outward at " +
                 "boatSpeed·tan(this). ~19° is the physical Kelvin angle.")]
        public float KelvinHalfAngleDeg;
        [Tooltip("Floor (m/s) on the lateral spread so a slow boat's trail still opens a little.")]
        public float SpreadSpeedMin;
        [Tooltip("Ceiling (m/s) on the lateral spread so a screaming hull can't fling the arms apart.")]
        public float SpreadSpeedMax;
        [Tooltip("Fraction of boat speed a fresh deposit keeps as astern drift (the dragged wash). Decays to " +
                 "current-only like all wake momentum.")]
        public float AsternDriftFraction;
        [Tooltip("Shoulder start half-width as a fraction of hull LengthMeters (the stable stand-in for beam " +
                 "— the trail starts about the hull's quarters and spreads from there).")]
        public float ShoulderHalfWidthFraction;
        [Tooltip("How much the wake grade widens the laid trail (0 = ungraded, 0.5 = +50% half-width at max " +
                 "magnitude).")]
        public float WidthMagnitudeBoost;

        [Header("Trail persistence (graded by the wake magnitude)")]
        [Tooltip("Deposit lifetime multiplier (× the foam config's Lifetime) at magnitude 0. >1 = the trail " +
                 "outlives the near-boat churn — it should linger where it was laid.")]
        public float LifetimeScaleAtMagnitude0;
        [Tooltip("Deposit lifetime multiplier at magnitude 1 — a big hull driven hard leaves a long-lived scar.")]
        public float LifetimeScaleAtMagnitude1;
        [Tooltip("Deposit birth-size multiplier (× the foam config's FoamSize) at magnitude 0.")]
        public float SizeScaleAtMagnitude0;
        [Tooltip("Deposit birth-size multiplier at magnitude 1.")]
        public float SizeScaleAtMagnitude1;
        [Tooltip("Fraction (0..1) of deposits that also lay a CENTRE churn puff between the shoulders (the " +
                 "prop/oar wash down the middle of the trail).")]
        public float CenterChurnFraction;

        [Header("The live plume (the boat-attached churn sprite — allowed to be attached, must be alive)")]
        [Tooltip("Churn-pulse frequency (Hz) of the authored plume sprite — the boil at the transom.")]
        public float PlumePulseHz;
        [Tooltip("± scale amount of the plume churn pulse (0 = the old static decal).")]
        public float PlumePulseScaleAmount;
        [Tooltip("± alpha amount of the plume churn pulse.")]
        public float PlumePulseAlphaAmount;
        [Tooltip("Turn rate (deg/s) above which the rigid straight-V plume starts to fade — it cannot bend, " +
                 "so a hard turn hands the wake read to the deposited trail (which curves).")]
        public float PlumeTurnFadeOnsetDegPerSec;
        [Tooltip("Turn-rate range (deg/s) over which the plume fades from full to gone past the onset.")]
        public float PlumeTurnFadeRangeDegPerSec;

        /// <summary>The greybox default trail — visible, curving, pool-safe. The owner tunes from here.</summary>
        public static WakeTrailConfig Default => new WakeTrailConfig
        {
            Enabled                    = true,
            DepositSpacingMeters       = 0.55f,
            DepositAsternOffset        = 0.15f,
            MaxDepositsPerTick         = 6,      // ≤ 3 particles per deposit → ≤ 18 of the 96+48 pool slots per tick
            TeleportResetMeters        = 20f,

            KelvinHalfAngleDeg         = 19f,    // the physical Kelvin angle — the emergent V opens at this
            SpreadSpeedMin             = 0.10f,
            SpreadSpeedMax             = 1.60f,
            AsternDriftFraction        = 0.12f,
            ShoulderHalfWidthFraction  = 0.14f,  // dory 4.5 m → ~0.63 m half-width at the quarters
            WidthMagnitudeBoost        = 0.5f,

            LifetimeScaleAtMagnitude0  = 1.4f,   // the trail lingers past the near-boat churn…
            LifetimeScaleAtMagnitude1  = 2.4f,   // …and a big hull driven hard leaves a long scar
            SizeScaleAtMagnitude0      = 0.85f,
            SizeScaleAtMagnitude1      = 1.6f,
            CenterChurnFraction        = 0.65f,

            PlumePulseHz               = 1.7f,
            PlumePulseScaleAmount      = 0.05f,
            PlumePulseAlphaAmount      = 0.20f,
            PlumeTurnFadeOnsetDegPerSec = 20f,
            PlumeTurnFadeRangeDegPerSec = 45f,
        };
    }

    /// <summary>
    /// Every tunable of the DYNAMIC bow wave — the churn pulse on the authored spray sheet plus the pooled
    /// droplets thrown off the cutwater and left behind in world space (rule 6; serialized on
    /// <see cref="BoatWakeEmitter"/>). The spray's GRADE (which tier, how big, the dory-gentle speed onset)
    /// stays entirely in <see cref="BowSprayGradeConfig"/> — this struct only animates it.
    /// </summary>
    [System.Serializable]
    public struct BowWaveConfig
    {
        [Header("Droplets (pooled, deposited in world space)")]
        [Tooltip("Shed bow droplets at the cutwater. Off = only the (still pulsing) authored spray sheet.")]
        public bool DropletsEnabled;
        [Tooltip("Droplets per second at FULL spray onset (scaled down by the same speed-onset ramp that " +
                 "keeps the dory gentle — she sees a few flecks, the fast hulls the full spatter).")]
        public float DropletsPerSecond;
        [Tooltip("Hard cap on droplets shed in one tick — the pool-safety valve (rule 7).")]
        public int MaxDropletsPerTick;
        [Tooltip("Half-angle (deg) of the fan the droplets are thrown into, around the bow direction.")]
        public float FanHalfAngleDeg;
        [Tooltip("Droplet launch speed as a fraction of boat speed — thrown forward, then the boat drives " +
                 "past them (the crash read).")]
        public float DropletSpeedScale;
        [Tooltip("Seconds a droplet lives — short; spray dies fast.")]
        public float DropletLifetime;
        [Tooltip("Droplet size at birth (m). Small — flecks, not foam.")]
        public float DropletSize;
        [Tooltip("How much the spray magnitude grows droplet size (0 = ungraded, 1 = doubles at max).")]
        public float DropletSizeMagnitudeBoost;
        [Tooltip("Per-second retention of a droplet's own momentum (0..1) — low: spray loses force almost " +
                 "at once and the sea keeps it.")]
        public float DropletVelocityDecay;

        [Header("The spray sheet churn (the authored sprite must read as crashing, not glued)")]
        [Tooltip("Churn-pulse frequency (Hz) of the authored spray sheet — faster than the plume's boil " +
                 "(impact, not wash).")]
        public float SprayPulseHz;
        [Tooltip("± scale amount of the spray churn pulse (0 = the old static decal).")]
        public float SprayPulseScaleAmount;
        [Tooltip("± alpha amount of the spray churn pulse.")]
        public float SprayPulseAlphaAmount;

        /// <summary>The greybox default bow wave. The owner tunes from here.</summary>
        public static BowWaveConfig Default => new BowWaveConfig
        {
            DropletsEnabled          = true,
            DropletsPerSecond        = 14f,
            MaxDropletsPerTick       = 4,
            FanHalfAngleDeg          = 55f,
            DropletSpeedScale        = 0.55f,
            DropletLifetime          = 0.55f,
            DropletSize              = 0.13f,
            DropletSizeMagnitudeBoost = 0.8f,
            DropletVelocityDecay     = 0.10f,   // loses force almost immediately — spray, not wash

            SprayPulseHz             = 2.6f,
            SprayPulseScaleAmount    = 0.09f,
            SprayPulseAlphaAmount    = 0.28f,
        };
    }
}
