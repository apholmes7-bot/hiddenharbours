using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// The PURE elevation→colour ramp for the Terrain Paint Tool's edit-mode height overlay (ADR 0014) —
    /// the determinism guard for <see cref="TerrainHeightPalette"/>: the ramp is monotone-by-band, brackets
    /// flat below/above its stops, returns each stop's own colour at that stop, classifies submerged vs dry
    /// against a preview waterline, and is deterministic (a pure function of elevation, no RNG). The overlay
    /// is an editor designer aid; this verifies the colour LOGIC headless so the aid reads correctly without
    /// opening Unity.
    /// </summary>
    public class TerrainHeightPaletteTests
    {
        private const float Eps = 1e-3f;

        [Test]
        public void ColorForElevation_BelowLowestStop_ClampsToTheDeepColour()
        {
            Color deep = TerrainHeightPalette.ColorForElevation(TerrainHeightPalette.MinStopElevation);
            Color wayBelow = TerrainHeightPalette.ColorForElevation(-1000f);
            Assert.AreEqual(deep, wayBelow, "elevations below the lowest stop clamp to the deepest colour");
        }

        [Test]
        public void ColorForElevation_AboveHighestStop_ClampsToTheRockColour()
        {
            Color rock = TerrainHeightPalette.ColorForElevation(TerrainHeightPalette.MaxStopElevation);
            Color wayAbove = TerrainHeightPalette.ColorForElevation(1000f);
            Assert.AreEqual(rock, wayAbove, "elevations above the highest stop clamp to the highest colour");
        }

        [Test]
        public void ColorForElevation_IsDeterministic_ForTheSameElevation()
        {
            float e = 1.6f;
            Color first = TerrainHeightPalette.ColorForElevation(e);
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(first, TerrainHeightPalette.ColorForElevation(e), "no RNG — identical every call");
        }

        [Test]
        public void ColorForElevation_RisesFromBlueTowardWarmAsElevationRises()
        {
            // Deep water reads blue-dominant (B > R); high land reads warm (R >= B). The ramp must cross over.
            Color deep = TerrainHeightPalette.ColorForElevation(-4f);
            Color land = TerrainHeightPalette.ColorForElevation(6f);
            Assert.Greater(deep.b, deep.r, "deep water is blue-dominant");
            Assert.GreaterOrEqual(land.r, land.b, "land is warm (not blue-dominant)");
        }

        [Test]
        public void ColorForElevation_BetweenTwoStops_LiesBetweenTheirColours()
        {
            // Between the -4 (deep navy) and -2 (blue) stops, the midpoint -3 blends the two — each channel is
            // within the [min,max] of the two endpoints (interpolation never overshoots).
            Color a = TerrainHeightPalette.ColorForElevation(-4f);
            Color b = TerrainHeightPalette.ColorForElevation(-2f);
            Color mid = TerrainHeightPalette.ColorForElevation(-3f);
            Assert.That(mid.r, Is.InRange(Mathf.Min(a.r, b.r) - Eps, Mathf.Max(a.r, b.r) + Eps), "R interpolated");
            Assert.That(mid.g, Is.InRange(Mathf.Min(a.g, b.g) - Eps, Mathf.Max(a.g, b.g) + Eps), "G interpolated");
            Assert.That(mid.b, Is.InRange(Mathf.Min(a.b, b.b) - Eps, Mathf.Max(a.b, b.b) + Eps), "B interpolated");
        }

        [Test]
        public void IsSubmerged_ComparesElevationToWaterline()
        {
            Assert.IsTrue(TerrainHeightPalette.IsSubmerged(-0.6f, 0.5f), "below the waterline → submerged");
            Assert.IsFalse(TerrainHeightPalette.IsSubmerged(1.6f, 0.5f), "above the waterline → dry");
            Assert.IsFalse(TerrainHeightPalette.IsSubmerged(0.5f, 0.5f), "exactly at the waterline → not submerged (strict)");
        }

        [Test]
        public void LegendStops_AreOrderedLowToHigh_AndNonEmpty()
        {
            var stops = TerrainHeightPalette.LegendStops();
            Assert.Greater(stops.Length, 1, "the legend has multiple bands");
            for (int i = 1; i < stops.Length; i++)
                Assert.Greater(stops[i].elevation, stops[i - 1].elevation,
                    "legend stops ascend in elevation (low → high)");
            Assert.AreEqual(TerrainHeightPalette.MinStopElevation, stops[0].elevation, Eps, "first stop = min");
            Assert.AreEqual(TerrainHeightPalette.MaxStopElevation, stops[stops.Length - 1].elevation, Eps, "last stop = max");
        }

        [Test]
        public void LegendStopColours_MatchTheRampAtTheStopElevations()
        {
            foreach (var (elev, color) in TerrainHeightPalette.LegendStops())
                Assert.AreEqual(color, TerrainHeightPalette.ColorForElevation(elev),
                    $"the ramp returns the legend colour at its own stop {elev} m");
        }
    }
}
