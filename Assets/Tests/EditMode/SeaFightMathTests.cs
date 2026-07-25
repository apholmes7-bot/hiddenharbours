using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE SEA IN THE FIGHT (owner's ruling 2026-07-25) — the pure maths that makes Pillar 1 true of
    /// fishing. Pins the owner's trade in both directions: rough water FISHES BETTER (quicker bites,
    /// bigger fish) and FIGHTS HARDER (the swell loading the line), a flat calm is bit-for-bit the
    /// weather-blind fight that shipped, and — the load-bearing pair — the everyday sea stays cozy while
    /// a real blow genuinely has teeth.
    /// </summary>
    public class SeaFightMathTests
    {
        private static SeaFishingSettings S => SeaFishingSettings.Default;

        // ---- a flat calm changes NOTHING (the A/B baseline) ---------------------------------------

        [Test]
        public void FlatCalm_IsExactlyTheWeatherBlindFight()
        {
            Assert.AreEqual(0f, SeaFightMath.TensionPerSec(0f, S.SeaFightFactor), 1e-6f,
                "glass water must put no pressure on the line at all");
            Assert.AreEqual(1f, SeaFightMath.BiteDelayScale(0f, S.SeaBoldness01), 1e-6f,
                "…nor quicken the bite");
            Assert.AreEqual(0f, SeaFightMath.WeightBias01(0f, S.SeaBigFishBias01), 1e-6f,
                "…nor lean the size roll");
            for (float u = 0f; u <= 1f; u += 0.1f)
                Assert.AreEqual(u, SeaFightMath.BiasedWeight01(u, 0f), 1e-6f,
                    "a zero bias is the identity — the shipped uniform roll, untouched");
        }

        [Test]
        public void EveryFactorAtZero_IsTheOffSwitch_AtAnySeaState()
        {
            for (float sea = 0f; sea <= 1f; sea += 0.25f)
            {
                Assert.AreEqual(0f, SeaFightMath.TensionPerSec(sea, 0f), 1e-6f);
                Assert.AreEqual(1f, SeaFightMath.BiteDelayScale(sea, 0f), 1e-6f);
                Assert.AreEqual(0f, SeaFightMath.WeightBias01(sea, 0f), 1e-6f);
            }
        }

        // ---- the cost side: the sea fights you ----------------------------------------------------

        [Test]
        public void SeaPressure_GrowsWithTheSea_AndBitesLateNotEarly()
        {
            float prev = -1f;
            for (float sea = 0f; sea <= 1f + 1e-4f; sea += 0.1f)
            {
                float p = SeaFightMath.TensionPerSec(sea, S.SeaFightFactor);
                Assert.GreaterOrEqual(p, prev, "pressure never falls as the sea gets up");
                prev = p;
            }

            // The SQUARE law is the whole shape of the owner's teeth: a chop is a nuisance, a gale is
            // dangerous. Half a sea must cost far less than half the storm's pressure.
            float half = SeaFightMath.TensionPerSec(0.5f, S.SeaFightFactor);
            float storm = SeaFightMath.TensionPerSec(1f, S.SeaFightFactor);
            Assert.Less(half, storm * 0.35f,
                "a middling sea must stay well under half the storm's bite — otherwise every pleasant " +
                "day is taxed and the gale still doesn't frighten anyone");
        }

        [Test]
        public void SeaPressure_IsOneSided_NeverPaysTheAngler()
        {
            for (float sea = -1f; sea <= 2f; sea += 0.25f)
                Assert.GreaterOrEqual(SeaFightMath.TensionPerSec(sea, S.SeaFightFactor), 0f,
                    "the sea only ever loads the line — a calm can never GIVE you slack");
        }

        // ---- the reward side: the sea fishes for you ----------------------------------------------

        [Test]
        public void RoughWater_BitesQuicker_ButNeverInstantly()
        {
            float calm = SeaFightMath.BiteDelayScale(0f, S.SeaBoldness01);
            float chop = SeaFightMath.BiteDelayScale(0.3f, S.SeaBoldness01);
            float gale = SeaFightMath.BiteDelayScale(1f, S.SeaBoldness01);

            Assert.Less(chop, calm, "even a chop already fishes noticeably better — the reward arrives early");
            Assert.Less(gale, chop, "…and a real sea better still");
            Assert.GreaterOrEqual(gale, SeaFightMath.MinBiteDelayScale - 1e-6f,
                "but the wait never collapses to nothing — an instant bite reads as a bug");

            // The reward is LINEAR where the cost is square: at mid sea the player should already have
            // most of the benefit while the danger is still mostly ahead of them. That asymmetry is what
            // makes the middle of the range the sweet spot rather than a wash.
            float midBenefit = (calm - SeaFightMath.BiteDelayScale(0.5f, S.SeaBoldness01)) / (calm - gale);
            Assert.Greater(midBenefit, 0.45f, "half a sea should already deliver about half the reward");
        }

        [Test]
        public void RoughWater_BringsUpTheBigOnes_WithoutBreakingTheRange()
        {
            float bias = SeaFightMath.WeightBias01(1f, S.SeaBigFishBias01);
            Assert.Greater(bias, 0f, "a storm must actually lean the roll");

            // One-sided and in-range: never smaller than the plain roll, never past the top of the range.
            for (float u = 0f; u <= 1f + 1e-4f; u += 0.1f)
            {
                float b = SeaFightMath.BiasedWeight01(u, bias);
                Assert.GreaterOrEqual(b, u - 1e-6f, "the sea never makes a fish SMALLER");
                Assert.LessOrEqual(b, 1f + 1e-6f, "…nor pushes it past the species' authored maximum");
            }

            // The roll keeps its own randomness — a low draw is still a smaller fish than a high one, so
            // a storm raises the whole distribution instead of flattening it to "always maximum".
            Assert.Less(SeaFightMath.BiasedWeight01(0.1f, bias), SeaFightMath.BiasedWeight01(0.9f, bias),
                "a storm should raise the odds of a good fish, not delete the size lottery");
        }

        // ---- THE GUARD-RAILS: where cozy ends and the teeth begin ---------------------------------

        [Test]
        public void EverydayWeather_StaysCozy_OnTheReferenceTuning()
        {
            var f = RodFightSettings.Default;
            Assert.IsTrue(SeaFightMath.MaintainOutbleedsTheEverydaySea(
                    f.RunTensionPressure, f.DeckAngleFactor, S.SeaFightFactor, f.TensionFallPerSec),
                "in any everyday sea — at the worst deck stance, mid-run — easing off must still recover. " +
                "This is the promise that keeps the daily loop dependable whatever the forecast.");
        }

        [Test]
        public void AStorm_ReallyCanBeatYou_OtherwiseTheSeaIsBackToBeingCosmetic()
        {
            var f = RodFightSettings.Default;
            Assert.IsTrue(SeaFightMath.AStormCanBeatYou(
                    f.RunTensionPressure, S.SeaFightFactor, f.TensionFallPerSec),
                "the owner asked for teeth: in a full storm, easing off must NOT be enough to save a " +
                "strong fish. If this ever goes false the weather has been tuned back into decoration.");
        }

        [Test]
        public void TheCozyCeiling_SitsInEverydayWeather_NotAtTheEdgeOfAGale()
        {
            Assert.That(SeaFightMath.CozySeaCeiling01, Is.InRange(0.35f, 0.7f),
                "the line between 'dependable' and 'dangerous' has to sit in the middle of the range: " +
                "too low and ordinary days bite, too high and the teeth never arrive in real play");
        }

        // ---- guards ------------------------------------------------------------------------------

        [Test]
        public void NaNAndOutOfRangeInputs_AreSafe()
        {
            float nan = float.NaN;
            Assert.AreEqual(0f, SeaFightMath.TensionPerSec(nan, nan), 1e-6f);
            Assert.IsFalse(float.IsNaN(SeaFightMath.BiteDelayScale(nan, nan)));
            Assert.IsFalse(float.IsNaN(SeaFightMath.WeightBias01(nan, nan)));
            Assert.IsFalse(float.IsNaN(SeaFightMath.BiasedWeight01(nan, nan)));

            // Out-of-range sea states clamp rather than extrapolate into nonsense.
            Assert.AreEqual(SeaFightMath.TensionPerSec(1f, S.SeaFightFactor),
                            SeaFightMath.TensionPerSec(9f, S.SeaFightFactor), 1e-6f);
            Assert.AreEqual(0f, SeaFightMath.TensionPerSec(-5f, S.SeaFightFactor), 1e-6f);
        }
    }
}
