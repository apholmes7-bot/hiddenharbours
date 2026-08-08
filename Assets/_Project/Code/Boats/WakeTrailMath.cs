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
        /// The vector the trail is LAID ALONG this tick — the fix for the owner's 2026-08-07 read, <i>"the
        /// wake originates at the CENTRE of the hull, obvious when turning"</i>.
        ///
        /// <para><b>What was wrong.</b> The deposit rate AND the whole emergent-V geometry were taken from
        /// the STERN ANCHOR's own swept segment (<c>prevStern → stern</c>). The anchor is half a hull length
        /// aft of the boat origin, so in a turn that segment is dominated by the anchor's <b>rotational swing
        /// about the boat's centre</b>, not by the hull's travel: a 7 m skiff at 6 m/s turning at 90°/s swings
        /// her stern anchor sideways at <c>ω·r</c> = 5.7 m/s against 6 m/s of headway, which throws
        /// <see cref="TrackDir"/> ~44° off the heading. Every consumer then rotates with it — the shoulder
        /// laterals (<see cref="ShoulderPoint"/>), the arm locus (<see cref="ArmDir"/>), the churn band and
        /// the stern roll — and the deposits string out along an arc <b>centred on the hull's midpoint</b>.
        /// That arc, swinging about amidships, is exactly the "originates at the centre / sweeps with the
        /// hull's centre" read. The swing also inflates the DISTANCE the emitter thinks she covered, so a
        /// hard turn lays over twice the foam a straight run at the same speed does — the fan is denser as
        /// well as wrongly aimed. (A boat pivoting on the spot is not affected: the speed gate in
        /// <c>DepositTrail</c> already stops her laying anything without way on.)</para>
        ///
        /// <para><b>The fix.</b> Lay the trail along the hull's TRAVEL through the water (course made good),
        /// blended toward the stern-swept segment by <paramref name="swingFraction"/> — because a real
        /// transom kicking out does throw some water, and refusing it entirely would be its own lie. The
        /// deposit POSITION is untouched and still lerps the stern segment (<see cref="PointOnTrack"/>), so
        /// every puff is still born at the transom; only the RATE and the GEOMETRY BASIS move to the course.
        /// At <paramref name="swingFraction"/> = <b>1</b> this returns the stern-swept segment and the whole
        /// change reverts bit-for-bit from one number, the <c>FoamAeration</c> idiom. Pure + static.</para>
        /// </summary>
        public static Vector2 TrackVector(Vector2 travel, Vector2 sternSwept, float swingFraction)
            => Vector2.Lerp(travel, sternSwept, Mathf.Clamp01(swingFraction));

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

        // ==== THE RENDERED READ (owner playtest 2026-07-23: "it looks like small horizontal lines") ========
        //
        // The deposition above was right; the RENDER of it read wrong, three ways at once:
        //   1. Shoulder streaks were oriented along their VELOCITY — which for a deposit is mostly-lateral
        //      spread + astern drift, and which decays toward the current. On most headings that painted the
        //      trail as rows of screen-horizontal dashes, not V arms.
        //   2. Streak length (≤1.1 m, shrinking with age) vs 0.55 m deposit spacing left visible gaps — a
        //      dotted line, not a wake.
        //   3. Nothing dense lived right behind the transom — no "bubble/foam close to the boat".
        // The three functions below fix each cause with geometry the tests can pin headless.

        /// <summary>
        /// The unit direction of the EMERGENT V ARM a shoulder deposit belongs to — what its streak sprite
        /// must be oriented ALONG (never its decaying velocity). Deposits are laid on the track and spread
        /// outward at <c>speed·tan(θ)</c> while the boat runs on at <c>speed</c>, so at any snapshot the
        /// locus of one shoulder's deposits is a straight line <b>astern + outward, exactly θ off the
        /// dead-astern line</b>: <c>normalize(−trackDir + lateral(side)·tan θ)</c>. Orienting each streak
        /// along this locus makes the overlapping streaks fuse into one long coherent arm (the owner's
        /// "long wake pattern"); orienting along velocity (the old read) painted near-perpendicular dashes.
        /// Degenerate track directions fall back to −Y (an astern guess), never NaN. Pure + static.
        /// </summary>
        public static Vector2 ArmDir(Vector2 trackDir, int side, float kelvinHalfAngleDeg)
        {
            Vector2 t = trackDir.sqrMagnitude > 1e-8f ? trackDir.normalized : Vector2.up;
            float tan = Mathf.Tan(Mathf.Clamp(kelvinHalfAngleDeg, 0f, 80f) * Mathf.Deg2Rad);
            Vector2 d = -t + Lateral(t, side) * tan;
            return d.sqrMagnitude > 1e-8f ? d.normalized : -t;
        }

        /// <summary>The render rotation (deg, sprite long axis = +X) of <see cref="ArmDir"/> — baked into the
        /// particle at deposit time so the arm stays world-locked as the velocity decays. Pure + static.</summary>
        public static float ArmOrientDeg(Vector2 trackDir, int side, float kelvinHalfAngleDeg)
        {
            Vector2 d = ArmDir(trackDir, side, kelvinHalfAngleDeg);
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The rendered LENGTH (m) of a trail arm streak — the OVERLAP LAW that makes the arm continuous by
        /// construction. Consecutive shoulder deposits sit <c>spacing/cos θ</c> apart ALONG the arm locus
        /// (spacing metres apart along the track, plus the lateral spread the age gap adds), so a streak at
        /// least <paramref name="overlapFactor"/> (clamped ≥ 1) times that distance is GUARANTEED to overlap
        /// its neighbours — no gaps, whatever the tuning. Trail streaks keep this full length for life (the
        /// alpha fade dissolves them); shrinking with age was cause 2 of the dotted read. Pure + static.
        /// </summary>
        public static float ArmStreakLength(float spacingMeters, float kelvinHalfAngleDeg, float overlapFactor)
        {
            float spacing = Mathf.Max(1e-3f, spacingMeters);
            float cos = Mathf.Cos(Mathf.Clamp(kelvinHalfAngleDeg, 0f, 80f) * Mathf.Deg2Rad);
            float alongArm = spacing / Mathf.Max(0.2f, cos);
            return alongArm * Mathf.Max(1f, overlapFactor);
        }

        /// <summary>Hard ceiling on churn puffs one deposit may lay — the explicit per-deposit pool budget
        /// (rule 7): one deposit is ≤ 2 shoulder streaks + (1 centre + this) foam puffs.</summary>
        public const int MaxChurnPuffsPerDeposit = 4;

        /// <summary>The clamped churn-puff count per deposit (0..<see cref="MaxChurnPuffsPerDeposit"/>) —
        /// mis-tuned configs can never flood the foam pool. Pure + static.</summary>
        public static int ChurnPuffCount(in WakeTrailConfig c)
            => Mathf.Clamp(c.ChurnPuffsPerDeposit, 0, MaxChurnPuffsPerDeposit);

        /// <summary>
        /// The WORST-CASE particles one tick can emit into the pools under a config — the explicit per-boat
        /// budget (rule 7), counted ACROSS the pools: <c>MaxDepositsPerTick · (2 shoulder crests +
        /// 1 stern-roll crest + 1 centre puff + churn puffs)</c>. The stern roll
        /// (<see cref="WakeWaveConfig.TransomCrest"/>) draws from its own small pool, and is counted here
        /// whether or not it is currently switched on — a budget that only holds while a toggle is off is
        /// not a budget. The emitter's pools (96 foam + 48 crests + 24 stern rolls by default) must
        /// comfortably exceed this. Pure + static.
        /// </summary>
        public static int MaxParticlesPerTick(in WakeTrailConfig c)
            => Mathf.Max(0, c.MaxDepositsPerTick) * (2 + 1 + 1 + ChurnPuffCount(in c));

        /// <summary>
        /// The half-width (m) of the near-stern CHURN BAND — the lateral strip right behind the transom the
        /// bubbling foam is laid across, as a tunable fraction of hull length (the beam stand-in, the
        /// <see cref="ShoulderHalfWidth"/> idiom). Always ≥ 0. Pure + static.
        /// </summary>
        public static float ChurnHalfWidth(float hullLengthMeters, in WakeTrailConfig c)
            => Mathf.Max(0f, hullLengthMeters) * Mathf.Max(0f, c.ChurnHalfWidthFraction);

        /// <summary>
        /// Where a churn puff lands: the track point pushed laterally by <paramref name="lat01"/> (−1..1,
        /// the deterministic per-puff dice) of the churn half-width — a dense, jittered white band across
        /// the trail centre, not a single file of dots. Pure + static.
        /// </summary>
        public static Vector2 ChurnPoint(Vector2 trackPoint, Vector2 trackDir, float lat01, float halfWidthMeters)
            => trackPoint + Lateral(trackDir, +1) * (Mathf.Clamp(lat01, -1f, 1f) * Mathf.Max(0f, halfWidthMeters));

        /// <summary>
        /// The BUBBLING pulse of a foam puff, weighted by age: full <paramref name="amount"/> at birth
        /// (the churn right off the transom visibly boils) easing to exactly 1 (calm) by end of life, so
        /// the near-stern band bubbles and the far trail lies quiet — the owner's "bubble close to the
        /// boat" as a render-only, bounded, deterministic multiplier (<see cref="ChurnPulse"/> under an
        /// age-scaled amount; rule 5). Pure + static.
        /// </summary>
        public static float AgedPulse(float time, float seed, float hz, float amount, float life01)
            => ChurnPulse(time, seed, hz, Mathf.Max(0f, amount) * (1f - Mathf.Clamp01(life01)));

        // ==== DISPERSAL (owner playtest 2026-08-07: the trail must go back to being water) ==================
        //
        // Owner, verbatim: the foam "would be bubbling as it goes and dispersing back to water over time as
        // the tide and wind gradually manipulate it." Three separable behaviours, three functions, each with
        // an explicit value that restores the shipped behaviour exactly:
        //   1. the SEA MOVES IT      — DriftVelocity: the tide already did; the WIND never did.
        //   2. it TEARS APART        — DispersalOffset: neighbouring puffs must not translate as one sheet.
        //   3. it OPENS UP           — StageAeration: the raft grows holes with age until it is water again.

        /// <summary>
        /// What carries a laid foam puff: the tidal current (which always did) PLUS a tunable fraction of
        /// the live sim WIND (which never did — the gap behind the owner's "as the tide and wind gradually
        /// manipulate it").
        ///
        /// <para>A fraction, not the whole wind: foam floats <i>in</i> the water and is only dragged across
        /// it by the air, so it makes a fraction of the wind's way — the same reasoning the shore foam and
        /// the grass already use against the one sim wind. The wind arrives from
        /// <c>EnvironmentSample.WindVector</c> on the tick that reads the sea, so this is a LIVE source, not
        /// a tuned strength standing in for one (the <c>foam-buffer-unsourced</c> lesson: a visual strength
        /// with nothing feeding it is a decal). <paramref name="windFraction"/> = 0 returns the current
        /// alone, bit-for-bit today. Pure + static.</para>
        /// </summary>
        public static Vector2 DriftVelocity(Vector2 current, Vector2 wind, float windFraction)
            => current + wind * Mathf.Clamp01(windFraction);

        /// <summary>
        /// The render-only DISPERSAL offset (m) of one foam puff at <paramref name="ageSeconds"/> — a small
        /// per-puff divergence, in a direction fixed by the puff's own seed, growing linearly with age.
        ///
        /// <para><b>Why a per-puff direction and not a shared one.</b> A band of foam that only advects
        /// (current + wind) slides across the sea RIGIDLY: every puff keeps its neighbour's spacing forever,
        /// which is what lets a dense trail keep reading as one painted object however much it fades. Give
        /// each puff its own small drift and the band SHEARS — the spacing opens unevenly, the overlap that
        /// made it solid breaks down, and what is left is a scatter of bubbles going back to water. Real
        /// foam does this because the water under it is turbulent at exactly this scale.</para>
        ///
        /// <para>Deterministic from the particle seed — no RNG, no time input, so the same puff always
        /// disperses the same way (rule 5) — and applied at RENDER, so the integrated sim position is
        /// untouched (the <see cref="WakeParticleSystem.WaveDistort"/> idiom).
        /// <paramref name="metersPerSecond"/> = 0 returns <see cref="Vector2.zero"/>, today bit-for-bit.
        /// Pure + static.</para>
        /// </summary>
        public static Vector2 DispersalOffset(float seed, float ageSeconds, float metersPerSecond)
        {
            float rate = Mathf.Max(0f, metersPerSecond);
            float age = Mathf.Max(0f, ageSeconds);
            if (rate <= 0f || age <= 0f) return Vector2.zero;
            float angle = Mathf.Repeat(seed, 1f) * (2f * Mathf.PI);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (rate * age);
        }

        /// <summary>
        /// Which rung of the AERATION LADDER a puff of normalized life <paramref name="life01"/> draws —
        /// 0 (freshly laid, the tightest raft) .. <paramref name="stageCount"/>−1 (about to vanish, the most
        /// open). Banded rather than continuous because the rungs are baked sprites: a puff swaps its raft a
        /// handful of times over a whole lifetime instead of every frame (rule 7). Degenerate counts clamp
        /// to a single stage. Monotonic non-decreasing in life. Pure + static.
        /// </summary>
        public static int AgeStageIndex(float life01, int stageCount)
        {
            int n = Mathf.Max(1, stageCount);
            int s = Mathf.FloorToInt(Mathf.Clamp01(life01) * n);
            return Mathf.Clamp(s, 0, n - 1);
        }

        /// <summary>
        /// How much of a puff's foam film has DISSOLVED on rung <paramref name="stage"/> of the age ladder:
        /// 0 on the rung a puff is laid at, rising to <paramref name="ageErosion01"/> on the last. Feeds
        /// <see cref="WakeFoamTexture.ErodeCoverage"/>, so an old puff is a scatter of surviving bubble
        /// rims with water between them rather than the same solid shape dimmed — the foam "dispersing
        /// back to water" as COVERAGE, which is the part fading alpha cannot do and the reason the owner
        /// still read the trail as painted after the fade was already in.
        ///
        /// <para>Monotonic non-decreasing in stage; rung 0 is always exactly 0, so a freshly laid puff
        /// draws precisely the raft that shipped. <paramref name="ageErosion01"/> = 0 makes every rung 0 —
        /// one raft for the whole life, bit-for-bit today (and the emitter then builds only one).
        /// Pure + static.</para>
        /// </summary>
        public static float StageErosion(int stage, int stageCount, float ageErosion01)
        {
            int n = Mathf.Max(1, stageCount);
            if (n == 1) return 0f;
            float t = Mathf.Clamp(stage, 0, n - 1) / (float)(n - 1);
            return Mathf.Clamp01(Mathf.Max(0f, ageErosion01) * t);
        }

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
        [Tooltip("How much of the STERN'S ROTATIONAL SWING counts toward the laid track (owner playtest " +
                 "2026-08-07: \"the wake originates at the centre of the hull, obvious when turning\"). The " +
                 "stern anchor sits half a hull aft of the origin, so in a turn its swept segment is mostly " +
                 "the swing about the boat's CENTRE, not her travel — which is what fanned the trail around " +
                 "amidships. 0 = lay purely along the course made good; 1 = the shipped stern-swept " +
                 "behaviour, bit-for-bit. The deposit POSITION is at the transom either way.")]
        [Range(0f, 1f)] public float SternSwingFraction;

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
                 "prop/oar wash down the middle of the trail). High = a continuous fading centre lane.")]
        public float CenterChurnFraction;

        [Header("The long pattern (arm streaks fuse into coherent V arms — owner playtest 2026-07-23)")]
        [Tooltip("Rendered arm-streak length as a multiple of the deposit spacing measured ALONG the arm " +
                 "(spacing/cos(Kelvin angle)). ≥1 guarantees consecutive streaks OVERLAP — the arm reads as " +
                 "one long line, never a dotted row. Clamped to ≥1 in the math.")]
        public float ArmOverlapFactor;

        [Header("The near-stern CHURN BAND (\"bubble close to the boat, be foamy close to the boat\")")]
        [Tooltip("Big overlapping foam puffs laid PER DEPOSIT across the churn band right behind the " +
                 "transom. They die young, so the dense white band lives only near the boat and hands the " +
                 "read to the long pattern. Hard-clamped 0..4 per deposit (pool safety).")]
        public int ChurnPuffsPerDeposit;
        [Tooltip("Churn-puff lifetime as a fraction of the foam config's Lifetime. SHORT (<1) on purpose — " +
                 "the bubbling band's length astern is speed·(this·Lifetime): it clings to the transom and " +
                 "fades with distance, exactly the owner's near-zone.")]
        public float ChurnLifetimeScale;
        [Tooltip("Churn-puff birth size as a multiple of the foam config's FoamSize (also graded by the " +
                 "wake magnitude). BIG (>1) so the puffs overlap into solid white coverage, not dots.")]
        public float ChurnSizeScale;
        [Tooltip("Half-width of the churn band as a fraction of hull LengthMeters (the beam stand-in) — " +
                 "the lateral strip behind the transom the bubbling foam is jittered across.")]
        public float ChurnHalfWidthFraction;
        [Tooltip("Bubbling frequency (Hz) of laid foam — each puff's size/alpha boils at this rate while " +
                 "young, easing to calm with age (render-only, deterministic).")]
        public float FoamPulseHz;
        [Tooltip("± bubbling amount of a FRESH foam puff (0 = calm). Ages to 0 by end of life, so only the " +
                 "near-stern band churns.")]
        public float FoamPulseAmount;
        [Tooltip("How AERATED the unit of foam is (owner playtest 2026-08-06: \"it should BUBBLE, not " +
                 "paint a solid line\"). 0 = the old solid disc, bit-for-bit — and a dense trail of solid " +
                 "discs is a painted stripe however it is spaced, which is why this is a change to the " +
                 "MATTER and not to the spacing above. 1 = bubble films with real holes through them, so " +
                 "the same dense overlap reads as white water. See WakeFoamTexture.")]
        public float FoamAeration;

        [Header("Dispersal (owner playtest 2026-08-07: \"bubbling as it goes and dispersing back to water\")")]
        [Tooltip("Fraction of the live sim WIND that drags laid foam across the water, on top of the tidal " +
                 "current it has always drifted with — the owner's \"as the tide and wind gradually " +
                 "manipulate it\". A fraction, not the whole wind: foam floats IN the water and only makes " +
                 "part of the air's way. 0 = the current alone, bit-for-bit today.")]
        [Range(0f, 1f)] public float FoamWindDriftFraction;
        [Tooltip("How much of a puff's foam FILM has dissolved by the end of its life. The thin film " +
                 "between bubbles goes first, so the holes eat outward and an old puff is a scatter of " +
                 "surviving bubble rims — foam going back to WATER as coverage, not as a solid shape " +
                 "dimmed. (Walking the aeration up instead was tried and MEASURED: it raises coverage " +
                 "rather than lowering it — see WakeFoamTexture.ErodeCoverage.) 0 = one raft for the " +
                 "whole life, bit-for-bit today, and no extra textures are built at all.")]
        [Range(0f, 1f)] public float FoamAgeErosion;
        [Tooltip("Per-puff dispersal drift (m/s) in a direction fixed by that puff's own seed — the SHEAR " +
                 "that tears a laid band apart instead of sliding it across the sea in one piece. " +
                 "Render-only and deterministic. 0 = no shear, bit-for-bit today.")]
        [Min(0f)] public float FoamDispersalMetersPerSecond;

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
            // ≤ (2 crests + 1 stern roll + 1 centre + 2 churn) per deposit → MaxParticlesPerTick = 36
            // (18 foam + 12 crests + 6 stern rolls) of the 96-foam + 48-crest + 24-roll pools per tick —
            // the explicit budget (rule 7).
            MaxDepositsPerTick         = 6,
            TeleportResetMeters        = 20f,
            // A quarter of the stern's swing: the transom kicking out still throws a little water where it
            // went, but the trail is laid along the course and no longer fans about amidships in a turn.
            SternSwingFraction         = 0.25f,

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
            CenterChurnFraction        = 0.85f,  // a near-continuous fading centre lane, not sporadic dots

            ArmOverlapFactor           = 1.7f,   // streaks ≈ 1.0 m vs 0.58 m along-arm spacing → solid arms
            ChurnPuffsPerDeposit       = 2,      // 2 big puffs / 0.55 m = dense white coverage at the transom
            ChurnLifetimeScale         = 0.4f,   // ×2.2 s foam life → ~0.9 s: the band clings to the boat
            ChurnSizeScale             = 1.9f,   // ×0.35 m foam → ~0.67 m puffs, overlapping at the spacing
            ChurnHalfWidthFraction     = 0.10f,  // dory 4.5 m → a 0.45 m half-width churn strip
            FoamPulseHz                = 2.8f,   // a lively boil, faster than the plume's 1.7 Hz wash
            FoamPulseAmount            = 0.22f,  // fresh foam visibly bubbles; calm by end of life
            FoamAeration               = 0.85f,  // bubble films with holes — the 2026-08-06 "not a stripe"

            FoamWindDriftFraction        = 0.30f, // the wind visibly walks the trail off the track…
            // …the film dissolves as it goes (measured: mean coverage 0.172 → 0.070, lit texels 73 → 38
            // across the four rungs, so over half the foam matter is gone by the end)…
            FoamAgeErosion               = 0.55f,
            FoamDispersalMetersPerSecond = 0.16f, // …and the band shears apart: ~0.35 m over a 2.2 s life

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
