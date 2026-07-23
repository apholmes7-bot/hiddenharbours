using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The PURE-logic guard for the world-DEPOSITED wake trail + dynamic bow wave (owner ask 2026-07-23:
    /// "the boats wakes are currently static lines, they should be … a representation that leaves a trail
    /// behind the boat, same with bow waves when they crash against the bow"). Every claim is exercised on
    /// the side-effect-free <see cref="WakeTrailMath"/> headless — no scene, no sprites:
    /// <list type="bullet">
    /// <item><description><b>Deposition spacing</b> — deposits are laid exactly per spacing-metre of track,
    /// the fractional carry is conserved across ticks, and the count is HARD-clamped per tick so emission
    /// can never exceed the pool (rule 7).</description></item>
    /// <item><description><b>Track geometry</b> — deposit points lie ON the swept segment, ordered
    /// oldest-first; shoulders sit symmetric and perpendicular to the TRACK; spread velocity opens the
    /// emergent V at boatSpeed·tan(Kelvin angle), clamped.</description></item>
    /// <item><description><b>Persistence grading</b> — lifetime/size/width grow monotonically with the wake
    /// magnitude and never go negative.</description></item>
    /// <item><description><b>The live plume</b> — the churn pulse is bounded and deterministic; the turn
    /// fade is 1 on a straight run, 0 past the fade range, monotone in between.</description></item>
    /// <item><description><b>Bow droplets</b> — none at rest, count clamped to the pool budget, and every
    /// droplet flies inside the fan at the configured fraction of boat speed.</description></item>
    /// </list>
    /// Plus the pool-bound guarantee at the SYSTEM level: <see cref="WakeParticleSystem.EmitAt"/> recycles
    /// slots, so live particles can never exceed the fixed pool no matter how much is emitted.
    /// </summary>
    public class WakeTrailMathTests
    {
        private static WakeTrailConfig Cfg() => WakeTrailConfig.Default;
        private static BowWaveConfig Bow() => BowWaveConfig.Default;

        // ==== DepositCount (spacing, carry, clamp — the pool-safety core) ================================

        [Test]
        public void DepositCount_LaysOnePerSpacingMetre()
        {
            float carry = 0f;
            int n = WakeTrailMath.DepositCount(2.2f, 0.5f, ref carry, 100);
            Assert.AreEqual(4, n, "2.2 m at 0.5 m spacing lays 4 deposits");
            Assert.AreEqual(0.2f, carry, 1e-4f, "the 0.2 m remainder is carried");
        }

        [Test]
        public void DepositCount_CarryIsConservedAcrossTicks()
        {
            float carry = 0f;
            int total = 0;
            // 10 ticks of 0.3 m = 3.0 m of track at 0.5 m spacing → exactly 6 deposits, whatever the phase.
            for (int i = 0; i < 10; i++)
                total += WakeTrailMath.DepositCount(0.3f, 0.5f, ref carry, 100);
            Assert.AreEqual(6, total, "3.0 m of track at 0.5 m spacing lays exactly 6 deposits across ticks");
        }

        [Test]
        public void DepositCount_ZeroOrNegativeDistance_LaysNothing()
        {
            float carry = 0.4f;
            Assert.AreEqual(0, WakeTrailMath.DepositCount(0f, 0.5f, ref carry, 100));
            Assert.AreEqual(0, WakeTrailMath.DepositCount(-1f, 0.5f, ref carry, 100));
            Assert.AreEqual(0.4f, carry, 1e-4f, "an idle tick leaves the carry untouched");
        }

        [Test]
        public void DepositCount_IsHardClampedToTheTickBudget_SoEmissionCanNeverExceedThePool()
        {
            // The sabotage-checked pool-bound assert: a teleport-sized 1000 m sweep in one tick.
            float carry = 0f;
            int n = WakeTrailMath.DepositCount(1000f, 0.5f, ref carry, 6);
            Assert.LessOrEqual(n, 6, "one tick can never lay more than MaxDepositsPerTick");
            // And the carry cannot hoard an unbounded backlog that would flood every following tick.
            int next = WakeTrailMath.DepositCount(0.01f, 0.5f, ref carry, 6);
            Assert.LessOrEqual(next, 6, "the post-clamp backlog is bounded too");
        }

        [Test]
        public void DepositCount_DegenerateSpacingOrBudget_IsSafe()
        {
            float carry = 0f;
            Assert.DoesNotThrow(() => WakeTrailMath.DepositCount(1f, 0f, ref carry, 6), "0 spacing never divides by zero");
            carry = 0f;
            Assert.AreEqual(0, WakeTrailMath.DepositCount(5f, 0.5f, ref carry, 0), "a 0 budget lays nothing");
        }

        // ==== Track geometry =============================================================================

        [Test]
        public void DepositT_IsOrderedOldestFirst_WithinTheSegment()
        {
            float last = 0f;
            for (int i = 0; i < 5; i++)
            {
                float t = WakeTrailMath.DepositT(i, 5);
                Assert.Greater(t, last, "deposit t strictly increases (oldest laid nearest where she WAS)");
                Assert.LessOrEqual(t, 1f, "never past the current stern");
                last = t;
            }
            Assert.AreEqual(1f, WakeTrailMath.DepositT(4, 5), 1e-5f, "the newest deposit lands at the stern");
        }

        [Test]
        public void PointOnTrack_LiesOnTheSweptSegment()
        {
            Vector2 a = new Vector2(3f, -2f), b = new Vector2(7f, 6f);
            Vector2 p = WakeTrailMath.PointOnTrack(a, b, 0.25f);
            Assert.AreEqual((a + (b - a) * 0.25f).x, p.x, 1e-5f);
            Assert.AreEqual((a + (b - a) * 0.25f).y, p.y, 1e-5f);
        }

        [Test]
        public void TrackDir_FallsBackToTheBow_WhenTheSegmentIsDegenerate()
        {
            Vector2 pos = new Vector2(4f, 4f);
            Vector2 dir = WakeTrailMath.TrackDir(pos, pos, Vector2.right);
            Assert.AreEqual(1f, dir.x, 1e-5f, "degenerate segment → the live bow direction, never NaN");
            Vector2 both = WakeTrailMath.TrackDir(pos, pos, Vector2.zero);
            Assert.AreEqual(1f, both.magnitude, 1e-4f, "even a degenerate bow yields a unit fallback");
        }

        [Test]
        public void ShoulderPoints_AreSymmetric_AndPerpendicularToTheTrack()
        {
            Vector2 basePos = new Vector2(10f, 5f);
            Vector2 track = new Vector2(1f, 1f).normalized;
            Vector2 port = WakeTrailMath.ShoulderPoint(basePos, track, -1, 0.8f);
            Vector2 stbd = WakeTrailMath.ShoulderPoint(basePos, track, +1, 0.8f);

            Assert.AreEqual(basePos.x, (port.x + stbd.x) * 0.5f, 1e-5f, "shoulders straddle the track point");
            Assert.AreEqual(basePos.y, (port.y + stbd.y) * 0.5f, 1e-5f);
            Assert.AreEqual(0.8f, (port - basePos).magnitude, 1e-5f, "at the half-width");
            Assert.AreEqual(0f, Vector2.Dot(port - basePos, track), 1e-5f, "perpendicular to the TRACK, not the heading");
        }

        [Test]
        public void ShoulderSpreadSpeed_OpensTheKelvinAngle_AndClamps()
        {
            WakeTrailConfig c = Cfg();
            c.KelvinHalfAngleDeg = 19f;
            c.SpreadSpeedMin = 0.1f;
            c.SpreadSpeedMax = 1.6f;

            float atThree = WakeTrailMath.ShoulderSpreadSpeed(3f, in c);
            Assert.AreEqual(3f * Mathf.Tan(19f * Mathf.Deg2Rad), atThree, 1e-4f,
                "spread = speed·tan(θ) — deposits laid on the track and spreading at this rate ARE the Kelvin V");

            Assert.AreEqual(0.1f, WakeTrailMath.ShoulderSpreadSpeed(0f, in c), 1e-5f, "floor at rest-ish speeds");
            Assert.AreEqual(1.6f, WakeTrailMath.ShoulderSpreadSpeed(50f, in c), 1e-5f, "ceiling at silly speeds");

            // Monotone between the clamps.
            float prev = 0f;
            for (float v = 0.5f; v <= 4.5f; v += 0.5f)
            {
                float s = WakeTrailMath.ShoulderSpreadSpeed(v, in c);
                Assert.GreaterOrEqual(s, prev, "spread never shrinks as speed grows");
                prev = s;
            }
        }

        [Test]
        public void ShoulderVelocity_PointsOutward_PerSide_WithAsternDrift()
        {
            WakeTrailConfig c = Cfg();
            Vector2 track = Vector2.up;    // she swept north
            Vector2 port = WakeTrailMath.ShoulderVelocity(track, -1, 0.5f, 2f, in c);
            Vector2 stbd = WakeTrailMath.ShoulderVelocity(track, +1, 0.5f, 2f, in c);

            Assert.AreEqual(-stbd.x, port.x, 1e-5f, "the two shoulders spread in mirror");
            Assert.Greater(Mathf.Abs(port.x), 0f, "there IS lateral spread");
            Assert.Less(port.y, 0f, "both drift astern (south of a northward track)");
            Assert.AreEqual(port.y, stbd.y, 1e-5f, "identical astern drift");
        }

        // ==== Grading ====================================================================================

        [Test]
        public void ShoulderHalfWidth_And_Graded_GrowMonotonicallyWithMagnitude()
        {
            WakeTrailConfig c = Cfg();
            float prevW = -1f, prevLife = -1f;
            for (float m = 0f; m <= 1f; m += 0.25f)
            {
                float w = WakeTrailMath.ShoulderHalfWidth(4.5f, m, in c);
                float life = WakeTrailMath.Graded(c.LifetimeScaleAtMagnitude0, c.LifetimeScaleAtMagnitude1, m);
                Assert.GreaterOrEqual(w, prevW, "a bigger wake lays a wider trail");
                Assert.GreaterOrEqual(life, prevLife, "a bigger wake persists longer");
                Assert.GreaterOrEqual(w, 0f);
                prevW = w; prevLife = life;
            }
            Assert.AreEqual(0f, WakeTrailMath.ShoulderHalfWidth(-5f, 0.5f, in c), 1e-5f, "negative length is guarded");
        }

        // ==== The live plume =============================================================================

        [Test]
        public void TurnFade_FullOnAStraightRun_GoneInAHardTurn_MonotoneBetween()
        {
            WakeTrailConfig c = Cfg();
            c.PlumeTurnFadeOnsetDegPerSec = 20f;
            c.PlumeTurnFadeRangeDegPerSec = 45f;

            Assert.AreEqual(1f, WakeTrailMath.TurnFade01(0f, in c), 1e-5f, "straight run keeps the full plume");
            Assert.AreEqual(1f, WakeTrailMath.TurnFade01(20f, in c), 1e-5f, "at the onset it is still full");
            Assert.AreEqual(0f, WakeTrailMath.TurnFade01(65f, in c), 1e-5f, "past onset+range the rigid V is gone");
            Assert.AreEqual(0f, WakeTrailMath.TurnFade01(300f, in c), 1e-5f);

            float prev = 2f;
            for (float r = 0f; r <= 120f; r += 10f)
            {
                float f = WakeTrailMath.TurnFade01(r, in c);
                Assert.LessOrEqual(f, prev, "more turn never brings the rigid plume back");
                prev = f;
            }
        }

        [Test]
        public void HeadingRate_MeasuresDegreesPerSecond_AndGuardsDegenerates()
        {
            float rate = WakeTrailMath.HeadingRateDegPerSec(Vector2.up, Vector2.right, 1f);
            Assert.AreEqual(90f, rate, 1e-3f, "a quarter turn in a second is 90 deg/s");
            Assert.AreEqual(0f, WakeTrailMath.HeadingRateDegPerSec(Vector2.up, Vector2.right, 0f), "dt 0 → 0, never NaN");
            Assert.AreEqual(0f, WakeTrailMath.HeadingRateDegPerSec(Vector2.zero, Vector2.right, 1f), "degenerate bow → 0");
        }

        [Test]
        public void ChurnPulse_IsBounded_Deterministic_AndNeutralAtZeroAmount()
        {
            for (float t = 0f; t < 10f; t += 0.13f)
            {
                float p = WakeTrailMath.ChurnPulse(t, 0.42f, 1.7f, 0.2f);
                Assert.GreaterOrEqual(p, 0.8f - 1e-4f, "never below 1 − amount");
                Assert.LessOrEqual(p, 1.2f + 1e-4f, "never above 1 + amount");
            }
            Assert.AreEqual(WakeTrailMath.ChurnPulse(3.7f, 0.42f, 1.7f, 0.2f),
                            WakeTrailMath.ChurnPulse(3.7f, 0.42f, 1.7f, 0.2f),
                            "same inputs, same pulse — deterministic, no RNG");
            Assert.AreEqual(1f, WakeTrailMath.ChurnPulse(3.7f, 0.42f, 1.7f, 0f), 1e-6f,
                "amount 0 restores the static decal exactly (the A/B)");
        }

        // ==== Bow droplets ===============================================================================

        [Test]
        public void DropletCount_NoneAtRest_ClampedUnderway_CarryResetWhileGated()
        {
            BowWaveConfig c = Bow();
            float carry = 0.9f;
            Assert.AreEqual(0, WakeTrailMath.DropletCount(0f, c.DropletsPerSecond, 0.033f, ref carry, 4),
                "no bow wave without way on");
            Assert.AreEqual(0f, carry, 1e-5f, "the carry resets while gated — no burp on restart");

            carry = 0f;
            int n = WakeTrailMath.DropletCount(1f, 10000f, 1f, ref carry, 4);
            Assert.AreEqual(4, n, "one tick can never shed more than MaxDropletsPerTick (pool safety)");
        }

        [Test]
        public void DropletVelocity_FliesInsideTheFan_AtTheConfiguredSpeedFraction()
        {
            BowWaveConfig c = Bow();
            c.FanHalfAngleDeg = 55f;
            c.DropletSpeedScale = 0.5f;
            Vector2 bow = new Vector2(1f, 2f).normalized;

            for (float fan = -1f; fan <= 1f; fan += 0.25f)
            {
                Vector2 v = WakeTrailMath.DropletVelocity(bow, 4f, fan, in c);
                Assert.AreEqual(2f, v.magnitude, 1e-4f, "droplet speed = boatSpeed · DropletSpeedScale");
                Assert.LessOrEqual(Vector2.Angle(bow, v), 55f + 1e-3f, "always inside the fan around the bow");
            }
            Vector2 centre = WakeTrailMath.DropletVelocity(bow, 4f, 0f, in c);
            Assert.AreEqual(0f, Vector2.Angle(bow, centre), 1e-3f, "fan 0 flies dead ahead");
            Assert.AreEqual(1f, WakeTrailMath.DropletVelocity(Vector2.zero, 1f, 0f, in c).magnitude / c.DropletSpeedScale,
                            1e-3f, "degenerate bow falls back to a unit direction, never NaN");
        }

        // ==== The rendered READ (owner playtest 2026-07-23: "small horizontal lines" → a real wake) ======

        [Test]
        public void ArmDir_IsExactlyTheKelvinAngleOffDeadAstern_AndMirrorsPerSide()
        {
            // A straight northward run: the emergent arm's locus must sit θ off the dead-astern line —
            // deposits fall astern at `speed` and spread laterally at `speed·tanθ`, so the locus direction
            // is normalize(−track + lateral·tanθ). The streak sprites are baked ALONG this (cause 1 fix).
            Vector2 track = Vector2.up;
            float theta = 19f;

            Vector2 port = WakeTrailMath.ArmDir(track, -1, theta);
            Vector2 stbd = WakeTrailMath.ArmDir(track, +1, theta);

            Assert.AreEqual(1f, port.magnitude, 1e-4f, "unit direction");
            Assert.AreEqual(Mathf.Cos(theta * Mathf.Deg2Rad), Vector2.Dot(port, -track), 1e-4f,
                "the arm lies EXACTLY the Kelvin half-angle off dead-astern (the analytic locus)");
            Assert.AreEqual(Mathf.Cos(theta * Mathf.Deg2Rad), Vector2.Dot(stbd, -track), 1e-4f);
            Assert.AreEqual(-stbd.x, port.x, 1e-5f, "the two arms mirror across the track");
            Assert.AreEqual(stbd.y, port.y, 1e-5f);
            Assert.Less(port.y, 0f, "both arms run ASTERN of a northward track");

            // The renderer-facing degrees agree with the vector (sprite long axis = +X).
            float deg = WakeTrailMath.ArmOrientDeg(track, +1, theta);
            Vector2 fromDeg = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
            Assert.AreEqual(stbd.x, fromDeg.x, 1e-4f);
            Assert.AreEqual(stbd.y, fromDeg.y, 1e-4f);

            // Degenerates never NaN.
            Vector2 degen = WakeTrailMath.ArmDir(Vector2.zero, +1, theta);
            Assert.AreEqual(1f, degen.magnitude, 1e-4f, "degenerate track still yields a unit direction");
        }

        [Test]
        public void ArmStreakLength_GuaranteesNeighbourOverlap_TheContinuityLaw()
        {
            // Cause 2 fix — the overlap law: consecutive shoulder deposits sit spacing/cosθ apart ALONG the
            // arm, and the rendered streak is that distance × overlap (clamped ≥ 1), so neighbouring streaks
            // ALWAYS overlap — the arm is continuous by construction, never a dotted row.
            float spacing = 0.55f;
            float theta = 19f;
            float len = WakeTrailMath.ArmStreakLength(spacing, theta, 1.7f);
            Assert.AreEqual(spacing / Mathf.Cos(theta * Mathf.Deg2Rad) * 1.7f, len, 1e-4f,
                "length = along-arm spacing × overlap factor");
            Assert.GreaterOrEqual(len, spacing * 1.7f, "≥ the track spacing × factor (cosθ ≤ 1)");

            for (float th = 0f; th <= 45f; th += 5f)
            {
                float l = WakeTrailMath.ArmStreakLength(spacing, th, 1.0f);
                Assert.GreaterOrEqual(l + 1e-5f, spacing / Mathf.Cos(th * Mathf.Deg2Rad),
                    "at any Kelvin angle a streak at least spans the along-arm gap to its neighbour");
            }

            Assert.GreaterOrEqual(WakeTrailMath.ArmStreakLength(spacing, theta, 0.2f) + 1e-5f,
                                  spacing / Mathf.Cos(theta * Mathf.Deg2Rad),
                "a mis-tuned overlap < 1 is clamped — continuity survives any tuning");
            Assert.DoesNotThrow(() => WakeTrailMath.ArmStreakLength(0f, 89f, 1f), "degenerate inputs are safe");
        }

        [Test]
        public void ChurnBand_IsDense_ShortLived_AndPoolBounded_TheNearSternRead()
        {
            // Cause 3 fix — "bubble close to the boat, be foamy close to the boat": the churn puffs are the
            // near-stern band. Density: with defaults the near band lays ≥ 3× the foam-per-metre of the far
            // centre lane. Short-lived: the band's astern reach is speed·(scale·Lifetime) — it clings to
            // the transom because the scale is well under 1. Bounded: the per-deposit count is hard-clamped.
            WakeTrailConfig c = Cfg();

            float nearFoamPerMeter = (WakeTrailMath.ChurnPuffCount(in c) + Mathf.Clamp01(c.CenterChurnFraction))
                                     / c.DepositSpacingMeters;
            float farFoamPerMeter = Mathf.Clamp01(c.CenterChurnFraction) / c.DepositSpacingMeters;
            Assert.GreaterOrEqual(nearFoamPerMeter, farFoamPerMeter * 3f,
                "the churn band right behind the transom is at least 3× as foam-dense as the far lane");

            Assert.Less(c.ChurnLifetimeScale, 1f, "churn dies young — the band exists only near the boat");
            Assert.Greater(c.ChurnSizeScale, 1f, "churn puffs are BIGGER than lane foam — solid coverage, not dots");

            // The hard clamp survives any config sabotage (rule 7).
            c.ChurnPuffsPerDeposit = 9999;
            Assert.AreEqual(WakeTrailMath.MaxChurnPuffsPerDeposit, WakeTrailMath.ChurnPuffCount(in c));
            c.ChurnPuffsPerDeposit = -5;
            Assert.AreEqual(0, WakeTrailMath.ChurnPuffCount(in c));

            // Geometry: churn puffs land inside the band, symmetric dice reaching both rails.
            Vector2 basePos = new Vector2(3f, 7f);
            Vector2 track = Vector2.right;
            WakeTrailConfig cw = Cfg();          // default ChurnHalfWidthFraction = 0.10
            float half = WakeTrailMath.ChurnHalfWidth(4.5f, in cw);
            Assert.AreEqual(0.45f, half, 1e-5f, "dory 4.5 m at 0.10 fraction → a 0.45 m half-width strip");
            Vector2 rail = WakeTrailMath.ChurnPoint(basePos, track, 1f, half);
            Assert.AreEqual(half, (rail - basePos).magnitude, 1e-5f, "full dice lands on the band rail");
            Assert.AreEqual(0f, Vector2.Dot(rail - basePos, track), 1e-5f, "jitter is LATERAL to the track");
            Vector2 centre = WakeTrailMath.ChurnPoint(basePos, track, 0f, half);
            Assert.AreEqual(basePos.x, centre.x, 1e-5f, "dice 0 lands on the track");
            Assert.AreEqual(0f, WakeTrailMath.ChurnHalfWidth(-3f, in cw), "negative hull length is guarded");
        }

        [Test]
        public void MaxParticlesPerTick_IsTheExplicitBudget_WellUnderThePools()
        {
            WakeTrailConfig c = Cfg();
            int budget = WakeTrailMath.MaxParticlesPerTick(in c);
            Assert.AreEqual(c.MaxDepositsPerTick * (2 + 1 + WakeTrailMath.ChurnPuffCount(in c)), budget,
                "budget = deposits × (2 shoulder streaks + 1 centre + churn puffs)");
            Assert.LessOrEqual(budget, 48,
                "the default per-tick worst case stays well inside the 96-foam + 48-line pools (rule 7)");
            c.MaxDepositsPerTick = -3;
            Assert.AreEqual(0, WakeTrailMath.MaxParticlesPerTick(in c), "a negative cap is guarded to 0");
        }

        [Test]
        public void AgedPulse_BubblesAtBirth_CalmsWithAge_BoundedAndDeterministic()
        {
            // The bubbling read: fresh foam boils at the full amount, and by end of life the pulse is
            // EXACTLY 1 — the far trail lies quiet while the near-stern band churns.
            for (float t = 0f; t < 6f; t += 0.17f)
            {
                float fresh = WakeTrailMath.AgedPulse(t, 0.3f, 2.8f, 0.22f, 0f);
                Assert.GreaterOrEqual(fresh, 1f - 0.22f - 1e-4f, "bounded below at full amount");
                Assert.LessOrEqual(fresh, 1f + 0.22f + 1e-4f, "bounded above at full amount");
                Assert.AreEqual(1f, WakeTrailMath.AgedPulse(t, 0.3f, 2.8f, 0.22f, 1f), 1e-6f,
                    "end of life is EXACTLY calm — the far trail never shimmers");
            }
            Assert.AreEqual(WakeTrailMath.AgedPulse(2.2f, 0.3f, 2.8f, 0.22f, 0.4f),
                            WakeTrailMath.AgedPulse(2.2f, 0.3f, 2.8f, 0.22f, 0.4f),
                            "same inputs, same pulse — deterministic (rule 5)");
        }

        [Test]
        public void EmitAt_BakesTheOrientation_AndDerivesFromVelocityWhenUnspecified()
        {
            var sys = new WakeParticleSystem(4);
            WakeConfig cfg = WakeConfig.Default;

            // Explicit orientation (the trail's arm direction) is baked verbatim.
            sys.EmitAt(Vector2.zero, new Vector2(5f, 0f), in cfg, 1f, 1f, 1f, orientDeg: 123.5f);
            Assert.AreEqual(123.5f, sys.Pool[0].OrientDeg, 1e-5f,
                "the laid orientation is BAKED — world-locked, never re-read from the decaying velocity");

            // Unspecified → derived from the emit velocity (the legacy template contract).
            sys.EmitAt(Vector2.zero, new Vector2(0f, 3f), in cfg, 1f, 1f, 1f);
            Assert.AreEqual(90f, sys.Pool[1].OrientDeg, 1e-3f, "NaN default derives from the emit velocity");
            Assert.AreEqual(0f, WakeParticleSystem.OrientFromVelocity(Vector2.zero), 1e-6f,
                "a degenerate velocity orients to 0, never NaN");
        }

        // ==== Pool bound at the SYSTEM level =============================================================

        [Test]
        public void EmitAt_CanNeverExceedThePool_AndBakesBirthStrength()
        {
            var sys = new WakeParticleSystem(8);
            WakeConfig cfg = WakeConfig.Default;

            for (int i = 0; i < 100; i++)
                sys.EmitAt(new Vector2(i, 0f), Vector2.up, in cfg, 1f, 1f, 0.6f);

            Assert.AreEqual(8, sys.AliveCount, "100 emits into an 8-slot pool leave exactly 8 live (recycled)");
            foreach (var p in sys.Pool)
            {
                Assert.IsTrue(p.Alive);
                Assert.AreEqual(0.6f, p.BirthStrength, 1e-5f,
                    "the birth strength is BAKED — the laid trail keeps it after the boat stops");
                Assert.Greater(p.BaseSize, 0f);
                Assert.Greater(p.Lifetime, 0f);
            }
        }

        [Test]
        public void EmitAt_ScalesLifetimeAndSize_AndJittersDeterministically()
        {
            var a = new WakeParticleSystem(4);
            var b = new WakeParticleSystem(4);
            WakeConfig cfg = WakeConfig.Default;

            for (int i = 0; i < 4; i++)
            {
                a.EmitAt(Vector2.zero, Vector2.zero, in cfg, 2f, 3f, 1f);
                b.EmitAt(Vector2.zero, Vector2.zero, in cfg, 2f, 3f, 1f);
            }
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(a.Pool[i].Lifetime, b.Pool[i].Lifetime, 1e-6f, "identical emit streams are bit-stable");
                Assert.AreEqual(a.Pool[i].BaseSize, b.Pool[i].BaseSize, 1e-6f);
                // 2× lifetime / 3× size land inside the jitter band around the scaled base.
                Assert.GreaterOrEqual(a.Pool[i].Lifetime, cfg.Lifetime * 2f * (1f - cfg.LifetimeJitter) - 1e-4f);
                Assert.LessOrEqual(a.Pool[i].Lifetime, cfg.Lifetime * 2f * (1f + cfg.LifetimeJitter) + 1e-4f);
                Assert.GreaterOrEqual(a.Pool[i].BaseSize, cfg.FoamSize * 3f * (1f - cfg.SizeJitter) - 1e-4f);
                Assert.LessOrEqual(a.Pool[i].BaseSize, cfg.FoamSize * 3f * (1f + cfg.SizeJitter) + 1e-4f);
            }
        }
    }
}
