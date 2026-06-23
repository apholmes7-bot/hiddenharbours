using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters CLAM-HOLE SCATTER (the owner-reported "no holes visible / can't dig" fix): clam holes must
    /// appear DETERMINISTICALLY wherever the falling tide bares ground. Drives the builder's pure
    /// <see cref="StPetersBuilder.ScatterClamHoles"/> against the SAME authored terrain the scene is built
    /// from (<see cref="StPetersBuilder.ConfigureTidalTerrain"/>), proving the field is reproducible (no RNG
    /// drift — CLAUDE.md rule 5), lands only on intertidal ground (bares low, floods high), and spreads
    /// across the bar rather than sitting at one or two fixed points.
    /// </summary>
    public class ClamScatterTests
    {
        // The TRUE extremes of the region's tide swing (StPetersBuilder constants): mean 0, amplitude 3.5 →
        // the water peaks at +3.5 (highest astronomical) and troughs at -3.5 (lowest). The scatter keeps a
        // hole only when its ground is intertidal between these extremes, so flooding/baring is asserted at
        // the actual peak/trough the swing reaches — not an arbitrary mid sample.
        private static readonly float HighWater = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude; // +3.5
        private static readonly float LowWater  = StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude; // -3.5

        private TidalTerrain _terrain;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TidalTerrain_Test");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);   // the same zones the scene is built with
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            GameServices.Reset();
        }

        [Test]
        public void Scatter_IsDeterministic_SameFieldEveryCall()
        {
            var a = StPetersBuilder.ScatterClamHoles(_terrain);
            var b = StPetersBuilder.ScatterClamHoles(_terrain);
            Assert.AreEqual(a.Count, b.Count, "same number of holes every run (no RNG)");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].x, b[i].x, 1e-6f, $"hole {i} x is stable");
                Assert.AreEqual(a[i].y, b[i].y, 1e-6f, $"hole {i} y is stable");
            }
        }

        [Test]
        public void Scatter_ProducesAScatteredSpread_NotOneOrTwoPoints()
        {
            var holes = StPetersBuilder.ScatterClamHoles(_terrain);
            Assert.Greater(holes.Count, 6,
                "the falling tide bares a whole flat — there should be many holes to find, not a fixed handful");

            // Spread along the bar's axis: the holes span a wide X range (not clustered at one spot).
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var h in holes) { minX = Mathf.Min(minX, h.x); maxX = Mathf.Max(maxX, h.x); }
            Assert.Greater(maxX - minX, 20f, "holes spread across the bar, not bunched at a single point");
        }

        [Test]
        public void Scatter_OnlyLandsOnIntertidalGround_BaresLow_FloodsHigh()
        {
            var holes = StPetersBuilder.ScatterClamHoles(_terrain);
            Assert.IsNotEmpty(holes, "there must be holes on the flats");

            foreach (var h in holes)
            {
                float ground = _terrain.ElevationAt(h);
                // Every kept hole bares at low water (so you can dig it)...
                Assert.IsTrue(TidalExposure.IsExposed(LowWater, ground),
                    $"hole at {h} (elev {ground}) must bare as the tide falls");
                // ...and floods at high water (so it isn't permanently-dry island — it's a real tide-gate).
                Assert.IsFalse(TidalExposure.IsExposed(HighWater, ground),
                    $"hole at {h} (elev {ground}) must flood at high water — a real intertidal flat, not island");
            }
        }

        [Test]
        public void Scatter_NoTerrain_IsEmpty_NotAThrow()
        {
            var holes = StPetersBuilder.ScatterClamHoles(null);
            Assert.IsNotNull(holes);
            Assert.IsEmpty(holes, "no height map (open water) → no flats to scatter holes on");
        }
    }
}
