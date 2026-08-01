using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The 2026-08-01 decoration pass — the grass layer and the interior erratics (owner: "decorate the
    /// island with our grass rigs, add more trees, add rocks"). Everything here asserts the PURE deciders
    /// (<see cref="StPetersGrass"/>, <see cref="StPetersShoreMap.ScatterFieldRocks"/>), the same
    /// instrument <c>StPetersWoodsTests</c> uses: the scene is a build artifact, the decider is the truth.
    ///
    /// <para>The tree densification rides the existing <c>StPetersWoodsTests</c> pins — count budget
    /// (40–500), wooded fraction (&lt; 0.7), mosaic sabotage — which is exactly what those pins are FOR:
    /// "more trees" had to fit inside the reverting-island thesis or fail loudly here.</para>
    /// </summary>
    public class StPetersDecorTests
    {
        private GameObject _go;
        private TidalTerrain _terrain;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("StPetersTerrain_DecorTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // =====================================================================================
        //  GRASS
        // =====================================================================================

        [Test]
        public void TheGrassIsDeterministic_NoRng()
        {
            var a = StPetersGrass.Scatter(_terrain);
            var b = StPetersGrass.Scatter(_terrain);

            Assert.AreEqual(a.Count, b.Count, "two scatters must plant the same meadow");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Less((a[i].Position - b[i].Position).magnitude, 1e-6f, $"tuft {i} moved");
                Assert.AreEqual(a[i].Variant, b[i].Variant, $"tuft {i} changed sprite");
                Assert.AreEqual(a[i].Scale, b[i].Scale, 1e-6f, $"tuft {i} changed size");
                Assert.AreEqual(a[i].Tint, b[i].Tint, $"tuft {i} changed colour");
            }
        }

        [Test]
        public void TheSward_IsAMeadowsWorthOfTufts_NotACarpetAndNotAMange()
        {
            // The renderer budget (rule 7) from above and "the island visibly has grass" from below.
            // Measured 593 at the shipped knobs (review-verified by an offline port of the decider);
            // the band is wide enough to survive tuning, tight enough that a broken gate (everything
            // rejected, or every cell at 3 tufts) fails.
            var sites = StPetersGrass.Scatter(_terrain);
            Assert.Greater(sites.Count, 400, "the meadow is nearly bare — a gate is rejecting everything");
            Assert.Less(sites.Count, 1800, "the meadow is a solid carpet — the swathe/chance gates are dead");
        }

        [Test]
        public void EveryTuft_StandsOnTheGrassBand_AndOutOfTheClearings()
        {
            // Per BLADE, not per cell — the sub-tuft offsets re-pass the gate, and this is the pin.
            foreach (var s in StPetersGrass.Scatter(_terrain))
            {
                Assert.GreaterOrEqual(_terrain.ElevationAt(s.Position),
                    StPetersShoreMap.GrassFloorElevation - 1e-3f,
                    $"a tuft at {s.Position} sits below the grass band");

                Assert.Greater(Vector2.Distance(s.Position, StPetersBuilder.CottagePos),
                    StPetersWoods.VillageClearingRadius, "a tuft is inside the village clearing");
                Assert.Greater(Vector2.Distance(s.Position, StPetersBuilder.StartSpawnPos),
                    StPetersWoods.SpawnClearingRadius, "a tuft is on the spawn");
                Assert.Greater(StPetersShoreMap.DistanceToSegment(s.Position,
                        StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo),
                    StPetersWoods.CrossingClearance, "a tuft is on the crossing's approach");
                Assert.Greater(Vector2.Distance(s.Position, StPetersBuilder.DockZonePos),
                    StPetersWoods.DockClearance, "a tuft is on the dock");
            }
        }

        [Test]
        public void TheSwardHasWornGround_TheSwatheFieldActuallyGates()
        {
            // Sample open meadow points (plantable at the grass floor, outside stands) and require the
            // swathe field to say "no grass" on a real fraction of them — the mosaic must exist, at a
            // smaller grain than the stands.
            int meadow = 0, sward = 0;
            for (float x = -50f; x <= 190f; x += 3f)
            for (float y = -68f; y <= 68f; y += 3f)
            {
                var p = new Vector2(x, y);
                if (!StPetersWoods.IsPlantable(_terrain, p, StPetersShoreMap.GrassFloorElevation))
                    continue;
                if (StPetersWoods.InStand(p, _terrain.ElevationAt(p))) continue;
                meadow++;
                if (StPetersGrass.InSwathe(p)) sward++;
            }
            Assert.Greater(meadow, 200, "sanity: the sweep found a meadow to measure");
            float f = (float)sward / meadow;
            Assert.Greater(f, 0.3f, $"only {f:P0} of the meadow carries grass — the sward broke");
            Assert.Less(f, 0.85f, $"{f:P0} of the meadow carries grass — the worn ground is gone");
        }

        [Test]
        public void UnderTheWoods_TheGrassThins()
        {
            var sites = StPetersGrass.Scatter(_terrain);
            int inWoods = sites.Count(s => StPetersWoods.InStand(s.Position,
                                                                _terrain.ElevationAt(s.Position)));
            float f = (float)inWoods / Mathf.Max(1, sites.Count);
            Assert.Less(f, 0.25f,
                $"{f:P0} of the sward stands under a canopy — the shade gate (ChanceWoods) is dead");
        }

        [Test]
        public void EveryTuft_IsAValidVariantAtAValidScale()
        {
            foreach (var s in StPetersGrass.Scatter(_terrain))
            {
                Assert.That(s.Variant, Is.InRange(0, 2), "variant must name one of the three tuft sprites");
                Assert.That(s.Scale, Is.InRange(StPetersGrass.ScaleMin, StPetersGrass.ScaleMax));
                Assert.That(s.Tint.a, Is.EqualTo(1f), "a translucent tuft is a bug, not a look");
            }
        }

        [Test]
        public void TheWeatherCoastBleaches_TheShelterStaysGreen()
        {
            // The tint gradient, at a fixed jitter so only EXPOSURE varies: the blasted south-east coast
            // must sit closer to straw (blue drops hardest) than the sheltered core.
            var exposed = new Vector2(StPetersBuilder.IslandCenter.x + 15f,
                                      StPetersBuilder.IslandCenter.y - StPetersBuilder.IslandRadiusY + 12f);
            var core = StPetersBuilder.IslandCenter;

            Assert.Greater(StPetersWoods.ExposureAt(exposed), StPetersWoods.ExposureAt(core),
                "sanity: the probe points must actually differ in exposure");
            Assert.Less(StPetersGrass.TintAt(exposed, 0.5f).b, StPetersGrass.TintAt(core, 0.5f).b,
                "the exposed coast's grass must bleach toward straw (blue falls) relative to the core");
        }

        // =====================================================================================
        //  FIELD ROCKS
        // =====================================================================================

        [Test]
        public void TheErraticsAreDeterministic_NoRng()
        {
            var a = StPetersShoreMap.ScatterFieldRocks(_terrain);
            var b = StPetersShoreMap.ScatterFieldRocks(_terrain);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Less((a[i].Position - b[i].Position).magnitude, 1e-6f, $"rock {i} moved");
                Assert.AreEqual(a[i].Sprite, b[i].Sprite, $"rock {i} changed sprite");
            }
        }

        [Test]
        public void ADozenOddErratics_NotAQuarryAndNotNone()
        {
            var rocks = StPetersShoreMap.ScatterFieldRocks(_terrain);
            Assert.Greater(rocks.Count, 4, "the field lost its stones — a gate is rejecting everything");
            Assert.LessOrEqual(rocks.Count, StPetersShoreMap.FieldRockAttempts,
                "more survivors than attempts — the scatter is double-placing");
            Assert.Less(rocks.Count, 30, "the meadow reads as a quarry — the gates are dead");
        }

        [Test]
        public void EveryErratic_KeepsTheClearings_AndTheOpenGround()
        {
            foreach (var r in StPetersShoreMap.ScatterFieldRocks(_terrain))
            {
                Assert.GreaterOrEqual(_terrain.ElevationAt(r.Position),
                    StPetersShoreMap.GrassFloorElevation - 1e-3f, "an erratic sits below the field");
                Assert.IsFalse(StPetersWoods.InStand(r.Position, _terrain.ElevationAt(r.Position)),
                    "an erratic hides under a closed canopy — invisible ground cost");

                Assert.Greater(Vector2.Distance(r.Position, StPetersBuilder.CottagePos),
                    StPetersWoods.VillageClearingRadius, "an erratic is inside the village clearing");
                Assert.Greater(Vector2.Distance(r.Position, StPetersBuilder.StartSpawnPos),
                    StPetersWoods.SpawnClearingRadius, "an erratic is on the spawn");
                Assert.Greater(StPetersShoreMap.DistanceToSegment(r.Position,
                        StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo),
                    StPetersWoods.CrossingClearance, "an erratic is on the crossing's approach");
                Assert.Greater(Vector2.Distance(r.Position, StPetersBuilder.DockZonePos),
                    StPetersWoods.DockClearance, "an erratic is on the dock");
            }
        }

        [Test]
        public void TheBarrensAreStonier_TheExposureBiasHolds()
        {
            var rocks = StPetersShoreMap.ScatterFieldRocks(_terrain);
            Assert.Greater(rocks.Count, 0, "sanity");
            int exposedRocks = rocks.Count(r =>
                StPetersWoods.ExposureAt(r.Position) >= StPetersShoreMap.FieldRockExposure);
            Assert.GreaterOrEqual((float)exposedRocks / rocks.Count, 0.5f,
                "most erratics must stand on exposed ground — the bias gate is dead");
        }

        [Test]
        public void EveryErratic_IsAKitBoulder()
        {
            var boulders = new[] { "bs", "bm", "bl" };
            foreach (var r in StPetersShoreMap.ScatterFieldRocks(_terrain))
                Assert.Contains(r.Sprite, boulders,
                    "field rocks are the kit's boulders — sea stacks and reef belong to the shore rings");
        }
    }
}
