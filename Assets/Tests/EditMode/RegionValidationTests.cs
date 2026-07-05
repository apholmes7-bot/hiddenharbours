#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Pins the pure predicates behind the Region Validator (owner level-design toolkit, Phase 1) — the
    /// tide-swing maths and the wet/dry reads it reports on. The dryness/depth checks defer to the sim's
    /// own <see cref="TidalExposure"/> rule over a stub terrain, so these tests double as a guard that the
    /// validator's "this floods at high tide" agrees with gameplay by construction (ADR 0009).
    /// </summary>
    public class RegionValidationTests
    {
        sealed class StubTerrain : ITidalTerrain
        {
            readonly Func<Vector2, float> _elevation;
            public StubTerrain(Func<Vector2, float> elevation) { _elevation = elevation; }
            public float ElevationAt(Vector2 worldPos) => _elevation(worldPos);
        }

        [Test]
        public void SwingOf_isMeanPlusMinusAmplitude()
        {
            var s = RegionValidation.SwingOf(0f, 3.5f);
            Assert.AreEqual(-3.5f, s.Low, 1e-4f);
            Assert.AreEqual(3.5f, s.High, 1e-4f);
        }

        [Test]
        public void SwingOf_takesAmplitudeAbsolute_soANegativeTypoCannotInvertTheSwing()
        {
            var s = RegionValidation.SwingOf(2f, -1f);
            Assert.AreEqual(1f, s.Low, 1e-4f);
            Assert.AreEqual(3f, s.High, 1e-4f);
        }

        [Test]
        public void TideSwing_normalisesLowBelowHigh_whateverOrderTheInputsArrive()
        {
            var s = new RegionValidation.TideSwing(5f, -2f);
            Assert.AreEqual(-2f, s.Low, 1e-4f);
            Assert.AreEqual(5f, s.High, 1e-4f);
        }

        [Test]
        public void WidestSwing_takesTheLowestLowAndTheHighestHigh()
        {
            var region = new RegionValidation.TideSwing(-1f, 1f);
            var start = new RegionValidation.TideSwing(-3.5f, 0.5f);
            var w = RegionValidation.WidestSwing(region, start);
            Assert.AreEqual(-3.5f, w.Low, 1e-4f);
            Assert.AreEqual(1f, w.High, 1e-4f);
        }

        [Test]
        public void RectCovers_trueWhenOuterFullyContainsInner()
        {
            Assert.IsTrue(RegionValidation.RectCovers(
                Vector2.zero, new Vector2(100f, 80f),
                Vector2.zero, new Vector2(50f, 40f), 0f));
        }

        [Test]
        public void RectCovers_falseWhenInnerPokesOut()
        {
            Assert.IsFalse(RegionValidation.RectCovers(
                Vector2.zero, new Vector2(50f, 40f),
                new Vector2(20f, 0f), new Vector2(40f, 40f), 0f));
        }

        [Test]
        public void RectCovers_toleranceForgivesASubTileOverflow()
        {
            // inner overflows the outer by 0.3 m on +x: a 0.5 m slack forgives it, 0.1 m does not.
            Assert.IsTrue(RegionValidation.RectCovers(
                Vector2.zero, new Vector2(100f, 100f),
                new Vector2(0.3f, 0f), new Vector2(100f, 100f), 0.5f));
            Assert.IsFalse(RegionValidation.RectCovers(
                Vector2.zero, new Vector2(100f, 100f),
                new Vector2(0.3f, 0f), new Vector2(100f, 100f), 0.1f));
        }

        [Test]
        public void IsIntertidal_trueOnlyStrictlyInsideTheSwing()
        {
            var swing = new RegionValidation.TideSwing(-2f, 2f);
            Assert.IsTrue(RegionValidation.IsIntertidal(0f, swing));    // bares AND floods (a real tide gate)
            Assert.IsFalse(RegionValidation.IsIntertidal(2f, swing));   // at high water: never floods (causeway)
            Assert.IsFalse(RegionValidation.IsIntertidal(-2f, swing));  // at low water: never bares (submerged)
            Assert.IsFalse(RegionValidation.IsIntertidal(5f, swing));
        }

        [Test]
        public void IsDryAt_usesTheSimRule_highGroundDry_seabedWet()
        {
            var land = new StubTerrain(_ => 5f);           // well above the surface
            Assert.IsTrue(RegionValidation.IsDryAt(land, Vector2.zero, 2f));
            var seabed = new StubTerrain(_ => -3f);        // well below the surface
            Assert.IsFalse(RegionValidation.IsDryAt(seabed, Vector2.zero, 2f));
        }

        [Test]
        public void IsDryAt_nullTerrainIsOpenWater_neverDry()
        {
            Assert.IsFalse(RegionValidation.IsDryAt(null, Vector2.zero, 0f));
        }

        [Test]
        public void DepthAt_isWaterLevelMinusElevation()
        {
            var seabed = new StubTerrain(_ => -3f);
            Assert.AreEqual(5f, RegionValidation.DepthAt(seabed, Vector2.zero, 2f), 1e-4f);
            var land = new StubTerrain(_ => 5f);
            Assert.LessOrEqual(RegionValidation.DepthAt(land, Vector2.zero, 2f), 0f);   // dry = non-positive depth
        }

        [Test]
        public void DepthAt_nullTerrainIsBottomless()
        {
            Assert.AreEqual(float.PositiveInfinity, RegionValidation.DepthAt(null, Vector2.zero, 0f));
        }

        [Test]
        public void SampleElevationRange_reportsMinAndMaxAcrossTheRect()
        {
            // elevation ramps with x; a rect centred at origin with half-width 10 spans x in [-10, 10].
            var ramp = new StubTerrain(p => p.x);
            bool any = RegionValidation.SampleElevationRange(
                ramp, Vector2.zero, new Vector2(20f, 20f), 5, out float min, out float max);
            Assert.IsTrue(any);
            Assert.AreEqual(-10f, min, 1e-3f);
            Assert.AreEqual(10f, max, 1e-3f);
        }

        [Test]
        public void SampleElevationRange_falseForNullTerrainOrTooFewSamples()
        {
            Assert.IsFalse(RegionValidation.SampleElevationRange(
                null, Vector2.zero, Vector2.one, 8, out _, out _));
            var flat = new StubTerrain(_ => 0f);
            Assert.IsFalse(RegionValidation.SampleElevationRange(
                flat, Vector2.zero, Vector2.one, 1, out _, out _));   // needs >= 2 samples/axis
        }
    }
}
#endif
