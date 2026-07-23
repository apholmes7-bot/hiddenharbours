using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Pins the <see cref="RodLineMath"/> visual-state reads (Rod Fishing v2 wave 1 — the line/bobber
    /// presentation maths, no gameplay). Everything here is pure and headless-safe: no scene, no clock,
    /// no graphics device (CI runs with a null device — nothing in this fixture renders).
    /// </summary>
    public class RodLineMathTests
    {
        const float Eps = 1e-4f;

        // ==== SampleLine — the catenary twin =====================================================

        [Test]
        public void SampleLine_PinsBothEndpoints_AtAnyTaut([Values(0f, 0.3f, 1f)] float taut)
        {
            var buf = new Vector2[9];
            var tip = new Vector2(1f, 4f);
            var end = new Vector2(7f, 2f);
            RodLineMath.SampleLine(tip, end, taut, 1.5f, buf);

            Assert.AreEqual(tip.x, buf[0].x, Eps);
            Assert.AreEqual(tip.y, buf[0].y, Eps);
            Assert.AreEqual(end.x, buf[buf.Length - 1].x, Eps);
            Assert.AreEqual(end.y, buf[buf.Length - 1].y, Eps);
        }

        [Test]
        public void SampleLine_FullyTaut_IsDeadStraight()
        {
            var buf = new Vector2[7];
            var tip = new Vector2(0f, 0f);
            var end = new Vector2(6f, 3f);
            RodLineMath.SampleLine(tip, end, 1f, 2f, buf);

            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)(buf.Length - 1);
                Vector2 straight = Vector2.Lerp(tip, end, t);
                Assert.AreEqual(straight.x, buf[i].x, Eps, $"sample {i} drifted off the chord (x)");
                Assert.AreEqual(straight.y, buf[i].y, Eps, $"sample {i} drifted off the chord (y)");
            }
        }

        [Test]
        public void SampleLine_FullySlack_BelliesDownByMaxSag_AtTheMidpoint()
        {
            const float maxSag = 1.5f;
            var buf = new Vector2[9];   // odd count → an exact midpoint sample
            var tip = new Vector2(0f, 2f);
            var end = new Vector2(8f, 2f);
            RodLineMath.SampleLine(tip, end, 0f, maxSag, buf);

            Vector2 chordMid = Vector2.Lerp(tip, end, 0.5f);
            Assert.AreEqual(chordMid.y - maxSag, buf[4].y, Eps,
                "at taut 0 the midpoint must droop by exactly maxSag (straight down)");
            Assert.AreEqual(chordMid.x, buf[4].x, Eps, "sag must be vertical — no sideways drift");
        }

        [Test]
        public void SampleLine_SagShrinksMonotonically_AsTautRises()
        {
            var buf = new Vector2[9];
            float previousDroop = float.MaxValue;
            for (float taut = 0f; taut <= 1.001f; taut += 0.25f)
            {
                RodLineMath.SampleLine(new Vector2(0f, 5f), new Vector2(8f, 5f), taut, 2f, buf);
                float droop = 5f - buf[4].y;
                Assert.LessOrEqual(droop, previousDroop + Eps,
                    $"droop must never grow as taut rises (taut {taut})");
                previousDroop = droop;
            }
            Assert.AreEqual(0f, previousDroop, Eps, "fully taut must carry no droop at all");
        }

        [Test]
        public void SampleLine_TinyBuffers_AreSafe()
        {
            RodLineMath.SampleLine(Vector2.zero, Vector2.one, 0.5f, 1f, new Vector2[0]);   // no throw

            var one = new Vector2[1];
            RodLineMath.SampleLine(Vector2.zero, new Vector2(3f, 1f), 0.5f, 1f, one);
            Assert.AreEqual(3f, one[0].x, Eps, "a 1-sample line collapses onto the far end");

            var two = new Vector2[2];
            RodLineMath.SampleLine(new Vector2(1f, 1f), new Vector2(2f, 2f), 0f, 5f, two);
            Assert.AreEqual(1f, two[0].x, Eps);
            Assert.AreEqual(2f, two[1].x, Eps);   // both endpoints, no sag applied to either
        }

        [Test]
        public void SampleLine_OutOfRangeTautAndNegativeSag_Clamp()
        {
            var buf = new Vector2[5];
            RodLineMath.SampleLine(Vector2.zero, new Vector2(4f, 0f), 7f, 2f, buf);
            Assert.AreEqual(0f, buf[2].y, Eps, "taut > 1 clamps to straight");

            RodLineMath.SampleLine(Vector2.zero, new Vector2(4f, 0f), 0f, -3f, buf);
            Assert.AreEqual(0f, buf[2].y, Eps, "negative maxSag clamps to no sag, never lifts the line");
        }

        // ==== BobberDip01 — the two-tap bite tell ================================================

        [Test]
        public void BobberDip_IsZeroAtBothLoopEnds()
        {
            Assert.AreEqual(0f, RodLineMath.BobberDip01(0f), Eps);
            Assert.AreEqual(0f, RodLineMath.BobberDip01(1f), Eps);
        }

        [Test]
        public void BobberDip_HasExactlyTwoTaps_EachSnappingUnderAndReleasing()
        {
            // Peak of each tap sits at start + attack; between and outside the taps it returns to 0.
            float peak1 = RodLineMath.BobberDip01(RodLineMath.FirstTapPhase + RodLineMath.TapAttack);
            float peak2 = RodLineMath.BobberDip01(RodLineMath.SecondTapPhase + RodLineMath.TapAttack);
            Assert.AreEqual(1f, peak1, Eps, "first tap must reach a full dip");
            Assert.AreEqual(1f, peak2, Eps, "second tap must reach a full dip");

            float betweenPhase = (RodLineMath.FirstTapPhase + RodLineMath.TapAttack + RodLineMath.TapRelease
                                  + RodLineMath.SecondTapPhase) * 0.5f;
            Assert.AreEqual(0f, RodLineMath.BobberDip01(betweenPhase), Eps,
                "the bobber must fully surface BETWEEN the two taps — two distinct nibbles, not one wobble");
        }

        [Test]
        public void BobberDip_AttackIsSharperThanRelease()
        {
            // Sample the same small offset into the attack and into the release of tap 1: the attack
            // must have covered more of the dip — the duck is a SNAP, the pop-back is slower.
            const float dt = 0.02f;
            float intoAttack = RodLineMath.BobberDip01(RodLineMath.FirstTapPhase + dt);
            float intoRelease = 1f - RodLineMath.BobberDip01(
                RodLineMath.FirstTapPhase + RodLineMath.TapAttack + dt);
            Assert.Greater(intoAttack, intoRelease,
                "equal time into each side, the attack must be further along — sharp duck, slow pop");
        }

        [Test]
        public void BobberDip_WrapsPhase_SoALoopingDriverCanFeedRawTime()
        {
            Assert.AreEqual(RodLineMath.BobberDip01(0.25f), RodLineMath.BobberDip01(3.25f), Eps);
            Assert.AreEqual(RodLineMath.BobberDip01(0.25f), RodLineMath.BobberDip01(-0.75f), Eps);
        }

        [Test]
        public void BobberDip_StaysInUnitRange_EverywhereOnTheLoop()
        {
            for (int i = 0; i <= 200; i++)
            {
                float v = RodLineMath.BobberDip01(i / 200f);
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
                Assert.IsFalse(float.IsNaN(v));
            }
        }

        // ==== SinkRipplePhase — the fall-time depth read =========================================

        [Test]
        public void SinkRipple_IsPhasedByDistance_SoAFasterFallPulsesFaster()
        {
            // Two lures, same elapsed time, different fall speeds → the heavier one has fallen further
            // and therefore shows MORE pulses. Distance-phasing gives this with no time plumbing.
            const float ripplesPerMeter = 0.5f;
            float light = RodLineMath.SinkRipplePhase(metersFallen: 4f, ripplesPerMeter);
            float heavy = RodLineMath.SinkRipplePhase(metersFallen: 10f, ripplesPerMeter);
            Assert.Greater(heavy, light);
            Assert.AreEqual(2f, light, Eps);
            Assert.AreEqual(5f, heavy, Eps);
        }

        [Test]
        public void SinkRipple_PulseCount_IsALiteralDepthCount()
        {
            // The owner's decision #4: counting the pulses IS counting the fall. 1 ring per 2 m fallen
            // → 3 whole rings at 6 m.
            float phase = RodLineMath.SinkRipplePhase(metersFallen: 6f, ripplesPerMeter: 0.5f);
            Assert.AreEqual(3, Mathf.FloorToInt(phase));
        }

        [Test]
        public void SinkRipple_NegativeAndZeroInputs_ClampToZero()
        {
            Assert.AreEqual(0f, RodLineMath.SinkRipplePhase(-2f, 0.5f), Eps);
            Assert.AreEqual(0f, RodLineMath.SinkRipplePhase(5f, -1f), Eps);
            Assert.AreEqual(0f, RodLineMath.RingAge01(-3f), Eps);
        }

        [Test]
        public void RingAge_IsTheFractionalPart_OfThePhase()
        {
            Assert.AreEqual(0.25f, RodLineMath.RingAge01(2.25f), Eps);
            Assert.AreEqual(0f, RodLineMath.RingAge01(3f), Eps);
        }

        // ==== SlackOvershoot — the bottom tell pops ==============================================

        [Test]
        public void SlackOvershoot_StartsAtRest_KicksPastIt_AndSettlesBack()
        {
            const float overshoot = 0.5f, settle = 0.6f;

            Assert.AreEqual(1f, RodLineMath.SlackOvershoot(0f, overshoot, settle), Eps,
                "at the instant of slack the multiplier is rest (the sag drive itself does the growing)");
            Assert.AreEqual(1f, RodLineMath.SlackOvershoot(-1f, overshoot, settle), Eps,
                "before the slack state there is no kick");

            // Somewhere inside the settle window the belly must exceed rest — that's the POP.
            float peak = 0f;
            for (int i = 1; i <= 100; i++)
                peak = Mathf.Max(peak, RodLineMath.SlackOvershoot(settle * i / 100f, overshoot, settle));
            Assert.Greater(peak, 1.05f, "the kick must visibly exceed rest sag");
            Assert.LessOrEqual(peak, 1f + overshoot + Eps, "the kick never exceeds 1 + overshoot01");

            Assert.AreEqual(1f, RodLineMath.SlackOvershoot(settle * 20f, overshoot, settle), 1e-3f,
                "long after the hit the line sits at rest sag");
        }

        [Test]
        public void SlackOvershoot_IsNaNSafe_AtDegenerateSettleTimes()
        {
            float v = RodLineMath.SlackOvershoot(0.5f, 0.5f, 0f);
            Assert.IsFalse(float.IsNaN(v), "zero settle time must clamp, not divide by zero");
            Assert.AreEqual(1f, v, 1e-3f, "a degenerate (instant) settle reads as already settled");
        }

        // ==== the strain reads ===================================================================

        [Test]
        public void StrainShudder_IsSilentBelowTheStart_AndFullAtFullStrain()
        {
            const float start = 0.6f;
            Assert.AreEqual(0f, RodLineMath.StrainShudder01(0f, start), Eps);
            Assert.AreEqual(0f, RodLineMath.StrainShudder01(0.59f, start), Eps,
                "below the threshold the everyday fight stays calm (cozy §7)");
            Assert.AreEqual(1f, RodLineMath.StrainShudder01(1f, start), Eps);

            float previous = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float v = RodLineMath.StrainShudder01(i / 20f, start);
                Assert.GreaterOrEqual(v, previous - Eps, "shudder must rise monotonically with strain");
                previous = v;
            }
        }

        [Test]
        public void StrainShudder_DegenerateThresholdAtOne_NeverShudders()
        {
            Assert.AreEqual(0f, RodLineMath.StrainShudder01(1f, 1f), Eps);
        }

        [Test]
        public void ShudderOffset_PinsBothEnds_AndMovesTheMiddle()
        {
            const int n = 9;
            Assert.AreEqual(0f, RodLineMath.ShudderOffset(0, n, 1.3f, 0.1f), Eps, "the rod tip is pinned");
            Assert.AreEqual(0f, RodLineMath.ShudderOffset(n - 1, n, 1.3f, 0.1f), Eps, "the far end is pinned");

            float mid = RodLineMath.ShudderOffset(n / 2, n, phase: Mathf.PI * 0.13f, amplitude: 0.1f);
            Assert.AreNotEqual(0f, mid, "the belly must vibrate");
            Assert.LessOrEqual(Mathf.Abs(mid), 0.1f + Eps, "never beyond the amplitude");
        }

        [Test]
        public void ShudderOffset_ScalesLinearlyWithAmplitude_AndIsSafeOnTinyLines()
        {
            float a = RodLineMath.ShudderOffset(3, 9, 0.7f, 0.05f);
            float b = RodLineMath.ShudderOffset(3, 9, 0.7f, 0.10f);
            Assert.AreEqual(a * 2f, b, Eps);

            Assert.AreEqual(0f, RodLineMath.ShudderOffset(0, 2, 0.7f, 0.1f), Eps,
                "a 2-sample line is both-ends — nothing to vibrate, no index error");
            Assert.AreEqual(0f, RodLineMath.ShudderOffset(1, 2, 0.7f, 0.1f), Eps);
        }

        [Test]
        public void Whiten_ArrivesLate_WithBias_AndLinearAtBiasOne()
        {
            Assert.AreEqual(0f, RodLineMath.Whiten01(0f, 3f), Eps);
            Assert.AreEqual(1f, RodLineMath.Whiten01(1f, 3f), Eps);
            Assert.AreEqual(0.5f, RodLineMath.Whiten01(0.5f, 1f), Eps, "bias 1 is the trap rope's linear shade");
            Assert.Less(RodLineMath.Whiten01(0.5f, 3f), 0.2f,
                "with a late bias, mid-strain barely whitens — the read is reserved for near-snap");
            Assert.AreEqual(RodLineMath.Whiten01(0.5f, 0.2f), RodLineMath.Whiten01(0.5f, 1f), Eps,
                "bias below 1 clamps to linear — whitening may never arrive EARLY");
        }

        // ==== the shared language ================================================================

        [Test]
        public void ShudderWaveNumber_MatchesTheTrapRopesShudder()
        {
            // TrapHaulController vibrates its strained rope at sin(phase + i·1.7). One coast, one
            // language: a straining line and a straining rope must read as the same physical event.
            Assert.AreEqual(1.7f, RodLineMath.ShudderWaveNumber, Eps);
        }
    }
}
