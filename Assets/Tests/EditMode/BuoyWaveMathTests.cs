using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The wave-driven BUOY math (<see cref="BuoyWaveMath"/>, Build 1 of the trap arc, visual-only):
    /// the pure mapping from the live wave <c>Height</c> under the buoy to (a) the bob offset, (b) the
    /// waterline fraction climbing its side, and (c) the vanish decision under a big crest — plus the
    /// determinism guard that the field it reads (<see cref="WaveMath.Sample"/>) is a pure function.
    /// All headless, no scene, no allocation — the real coverage for a feature whose look can't be asserted.
    /// </summary>
    public class BuoyWaveMathTests
    {
        private const float Eps = 1e-5f;

        // ---- BOB: the crest lifts, the trough drops, glass is dead still, the cap is hard --------

        [Test]
        public void Bob_ScalesWithHeight_CrestUp_TroughDown()
        {
            Assert.AreEqual(0.35f, BuoyWaveMath.BobOffset(1f, 0.35f, 10f), Eps, "a 1 m crest lifts by bobPerMeter");
            Assert.AreEqual(-0.35f, BuoyWaveMath.BobOffset(-1f, 0.35f, 10f), Eps, "a 1 m trough drops by the same");
        }

        [Test]
        public void Bob_Glass_IsExactlyZero()
        {
            Assert.AreEqual(0f, BuoyWaveMath.BobOffset(0f, 0.35f, 10f), 0f, "glass is sacred: flat surface, no bob");
        }

        [Test]
        public void Bob_ClampedToCap_BothWays()
        {
            Assert.AreEqual(0.6f, BuoyWaveMath.BobOffset(100f, 0.35f, 0.6f), Eps, "a freak crest can't fling the sprite past the cap");
            Assert.AreEqual(-0.6f, BuoyWaveMath.BobOffset(-100f, 0.35f, 0.6f), Eps, "…nor a freak trough below it");
        }

        [Test]
        public void Bob_NaNHeight_IsSafeZero()
        {
            Assert.AreEqual(0f, BuoyWaveMath.BobOffset(float.NaN, 0.35f, 0.6f), 0f, "garbage clamps, never propagates");
        }

        // ---- WATERLINE: baseline draught, rides up a crest, drops in a trough, clamps [0,1] ------

        [Test]
        public void Waterline_AtRest_IsTheFloatLine()
        {
            // height == restOffset ⇒ exactly the resting draught.
            float frac = BuoyWaveMath.WaterlineFraction(0f, 0.4f, 0f, 1f);
            Assert.AreEqual(0.4f, frac, Eps, "in calm the buoy sits at its float line (low + wet)");
        }

        [Test]
        public void Waterline_RisesOnACrest_FallsInATrough()
        {
            // buoyHeight 1 m ⇒ a +0.3 m crest climbs 0.3 of the side; a −0.3 m trough drops it 0.3.
            float onCrest = BuoyWaveMath.WaterlineFraction(0.3f, 0.4f, 0f, 1f);
            float inTrough = BuoyWaveMath.WaterlineFraction(-0.3f, 0.4f, 0f, 1f);
            Assert.AreEqual(0.7f, onCrest, Eps, "the crest climbs the flank");
            Assert.AreEqual(0.1f, inTrough, Eps, "the trough leaves more of the buoy proud");
            Assert.Greater(onCrest, inTrough, "monotonic: higher water ⇒ higher line");
        }

        [Test]
        public void Waterline_ClampsToUnitInterval()
        {
            Assert.AreEqual(1f, BuoyWaveMath.WaterlineFraction(5f, 0.4f, 0f, 1f), Eps, "a huge crest tops out at 1 (fully awash)");
            Assert.AreEqual(0f, BuoyWaveMath.WaterlineFraction(-5f, 0.4f, 0f, 1f), Eps, "a deep trough bottoms out at 0 (base out of water)");
        }

        [Test]
        public void Waterline_TallerBuoy_SameCrestClimbsLess()
        {
            float shortBuoy = BuoyWaveMath.WaterlineFraction(0.5f, 0.4f, 0f, 1f);
            float tallBuoy = BuoyWaveMath.WaterlineFraction(0.5f, 0.4f, 0f, 2f);
            Assert.Greater(shortBuoy, tallBuoy, "the same crest is a smaller fraction of a taller buoy");
        }

        [Test]
        public void Waterline_DegenerateHeight_CollapsesToBaseline_NoDivideByZero()
        {
            float frac = BuoyWaveMath.WaterlineFraction(1f, 0.4f, 0f, 0f);
            Assert.AreEqual(0.4f, frac, Eps, "buoyHeight 0 collapses to the clamped baseline, never NaN/Inf");
        }

        // ---- SWAMP THRESHOLD + VANISH: the owner's ask — big crests hide the buoy ----------------

        [Test]
        public void SwampThreshold_ScalesWithSeaState_ButHasAFloor()
        {
            // A big sea (envelope 2 m) at fraction 0.7 ⇒ 1.4 m bar; a tiny sea is held at the floor.
            Assert.AreEqual(1.4f, BuoyWaveMath.SwampThreshold(2f, 0.7f, 0.6f), Eps, "big sea: bar scales with the envelope");
            Assert.AreEqual(0.6f, BuoyWaveMath.SwampThreshold(0.1f, 0.7f, 0.6f), Eps, "near-glass: the floor holds so a nothing crest can't hide it");
        }

        [Test]
        public void Vanish_BelowThreshold_FullyVisible()
        {
            Assert.AreEqual(1f, BuoyWaveMath.VanishAlpha(0.5f, 1.4f, 0.35f), Eps, "under the swamp bar the buoy is fully visible");
        }

        [Test]
        public void Vanish_AboveThresholdPlusBand_FullyGone()
        {
            Assert.AreEqual(0f, BuoyWaveMath.VanishAlpha(1.4f + 0.35f, 1.4f, 0.35f), Eps, "a full band over the bar ⇒ alpha 0, the owner's complete disappear");
            Assert.IsTrue(BuoyWaveMath.IsSwamped(1.4f + 0.5f, 1.4f, 0.35f), "well over ⇒ swamped");
        }

        [Test]
        public void Vanish_AcrossTheBand_FadesContinuously_Monotonic()
        {
            // No pop: alpha decreases continuously from 1 to 0 as the crest climbs through the band.
            float prev = 1f;
            for (int i = 0; i <= 10; i++)
            {
                float h = 1.4f + 0.35f * (i / 10f);
                float a = BuoyWaveMath.VanishAlpha(h, 1.4f, 0.35f);
                Assert.LessOrEqual(a, prev + Eps, "alpha never increases as the crest rises (continuous vanish)");
                prev = a;
            }
            Assert.AreEqual(1f, BuoyWaveMath.VanishAlpha(1.4f, 1.4f, 0.35f), Eps, "exactly at the bar: still fully visible");
            Assert.AreEqual(0f, prev, Eps, "by the top of the band: fully gone");
        }

        [Test]
        public void Vanish_NaNHeight_FailsSafeVisible()
        {
            Assert.AreEqual(1f, BuoyWaveMath.VanishAlpha(float.NaN, 1.4f, 0.35f), 0f, "a garbage read never hides the buoy");
        }

        // ---- the same rising crest lifts AND submerges (one Height, two reads) -------------------

        [Test]
        public void RisingCrest_BobsUpAndWaterlineClimbs_Together()
        {
            // The design contract: one Height read drives both the lift and the flank climb, in step.
            float lowBob = BuoyWaveMath.BobOffset(0.1f, 0.35f, 1f);
            float highBob = BuoyWaveMath.BobOffset(0.6f, 0.35f, 1f);
            float lowLine = BuoyWaveMath.WaterlineFraction(0.1f, 0.4f, 0f, 1f);
            float highLine = BuoyWaveMath.WaterlineFraction(0.6f, 0.4f, 0f, 1f);
            Assert.Greater(highBob, lowBob, "a bigger crest lifts the buoy more");
            Assert.Greater(highLine, lowLine, "…and climbs further up its side");
        }

        // ---- DETERMINISM (rule 5): the field the buoy reads is a pure function --------------------

        [Test]
        public void WaveSample_IsPure_SameInputsSameHeight_Forever()
        {
            Vector2 wind = new Vector2(5.5f, -3.2f);
            Vector2 buoyPos = new Vector2(-40f, -26f);   // a St Peters test-buoy spot
            const float sea = 0.55f;
            const double t = 987.654;

            var trainsA = WaveMath.TrainsFrom(wind, sea, WaveFieldSettings.Default);
            WaveSample a = WaveMath.Sample(buoyPos, t, in trainsA);

            var trainsB = WaveMath.TrainsFrom(wind, sea, WaveFieldSettings.Default);
            WaveSample b = WaveMath.Sample(buoyPos, t, in trainsB);

            Assert.AreEqual(a.Height, b.Height, 0f, "deterministic: same (pos, time, weather) ⇒ same Height, bit-exact");
            Assert.AreEqual(a.Slope.x, b.Slope.x, 0f);
            Assert.AreEqual(a.Slope.y, b.Slope.y, 0f);
        }

        [Test]
        public void FullPipeline_GlassSea_BuoyIsDeadStillAndAtRest()
        {
            // seaState01 = 0 ⇒ every amplitude exactly 0 ⇒ flat surface ⇒ no bob, resting draught, never vanishes.
            var trains = WaveMath.TrainsFrom(new Vector2(7f, 1f), 0f, WaveFieldSettings.Default);
            WaveSample wave = WaveMath.Sample(new Vector2(-40f, -26f), 1234.0, in trains);

            float bob = BuoyWaveMath.BobOffset(wave.Height, 0.35f, 0.6f);
            float line = BuoyWaveMath.WaterlineFraction(wave.Height, 0.4f, 0f, 1f);
            float threshold = BuoyWaveMath.SwampThreshold(trains.TotalAmplitude, 0.7f, 0.6f);
            float alpha = BuoyWaveMath.VanishAlpha(wave.Height, threshold, 0.35f);

            Assert.AreEqual(0f, bob, 0f, "glass: no bob");
            Assert.AreEqual(0.4f, line, Eps, "glass: the buoy sits exactly at its float line");
            Assert.AreEqual(1f, alpha, Eps, "glass: never vanishes (the floor holds the threshold above 0)");
        }

        [Test]
        public void FullPipeline_BigSea_ACrestSomewhereSwampsABuoy()
        {
            // A stormy sea (high sea-state, strong wind) must, somewhere along the surface, throw a crest
            // tall enough to bury a buoy — otherwise the owner's headline effect is silently dead.
            var trains = WaveMath.TrainsFrom(new Vector2(18f, 0f), 1f, WaveFieldSettings.Default);
            float threshold = BuoyWaveMath.SwampThreshold(trains.TotalAmplitude, 0.7f, 0.6f);

            bool anySwamped = false;
            for (int i = 0; i < 200 && !anySwamped; i++)
            {
                // walk along the primary swell direction (+X here) so we cross crests
                WaveSample wave = WaveMath.Sample(new Vector2(i * 0.5f, -26f), 100.0, in trains);
                anySwamped = BuoyWaveMath.IsSwamped(wave.Height, threshold, 0.35f);
            }
            Assert.IsTrue(anySwamped, "a full storm must bury a buoy under a crest somewhere — the headline vanish is alive");
        }
    }
}
