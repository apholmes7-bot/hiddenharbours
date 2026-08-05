using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The <b>green-over's budget and its determinism</b> — what covering St Peters actually costs, and
    /// the proof that a rebuild reproduces it rather than drifting or doubling.
    ///
    /// <para><b>⚠ THE BUDGET IS AN ACCEPTANCE CRITERION, NOT A HOPE.</b> This layer went from ~590
    /// tufts to several thousand in one change, and the number is invisible in the editor until the
    /// frame rate says so on someone else's machine. So it is pinned here, with headroom: the test
    /// fails if the field silently doubles again, and the message says which knob moved it. It cannot
    /// tell you the frame cost — that is the owner's GPU and nothing headless can stand in for it —
    /// but it can stop the COUNT changing without anyone noticing.</para>
    ///
    /// <para><b>⚠ AND DETERMINISM IS THE OTHER HALF.</b> Rule 5: the scatter is a pure function of
    /// position. Two calls must produce byte-identical fields, or the builder's "rebuild the scene"
    /// becomes "get a different island", and the owner's proof screenshots stop meaning anything.</para>
    /// </summary>
    public class StPetersGreenOverTests
    {
        GameObject _go;
        TidalTerrain _terrain;

        /// <summary>The terrain the SCENE is built with — <see cref="StPetersBuilder.ConfigureTidalTerrain"/>
        /// is the single source of truth the builder itself calls, so the scatter under test reads exactly
        /// the ground that ships rather than a test-local approximation of it.</summary>
        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TidalTerrain_GreenOverTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        ITidalTerrain Terrain() => _terrain;

        // =====================================================================================

        [Test]
        public void TheMeadow_IsDenseEnoughToReadGreen_AndStaysInsideItsBudget()
        {
            var terrain = Terrain();
            var sites = StPetersGrass.Scatter(terrain);

            // The floor: the whole point of the green-over. Below this the island reads as "some grass
            // on it" again, which is the look the owner asked to replace.
            Assert.Greater(sites.Count, 3000,
                $"Only {sites.Count} tufts — the island will not read GREEN. The knobs are " +
                $"StPetersGrass.GrassStep ({StPetersGrass.GrassStep} m, count goes as its inverse " +
                $"SQUARE) and SwatheThreshold ({StPetersGrass.SwatheThreshold}).");

            // The ceiling: every tuft is a SpriteRenderer, and this is the number that decides whether
            // the island still runs. Generous against the measured ~5.5k so ordinary tuning does not
            // trip it, tight enough that a doubling cannot land unnoticed.
            Assert.Less(sites.Count, 9000,
                $"{sites.Count} tufts is past the budget this layer was signed off at. Every one is a " +
                "SpriteRenderer; raise GrassStep before raising this number.");
        }

        [Test]
        public void EveryTuftGetsAHabitat_AndTheIslandGrowsAllOfThem()
        {
            var sites = StPetersGrass.Scatter(Terrain());
            var counts = new Dictionary<string, int>();
            foreach (var s in sites)
            {
                Assert.IsNotNull(s.Habitat, "A site came out with no habitat — the planter would have " +
                                            "nothing to choose art with.");
                counts[s.Habitat] = counts.TryGetValue(s.Habitat, out int n) ? n + 1 : 1;
            }

            // ⚠ The one that has actually been wrong. The first cut asked "is there sand nearby?"
            // BEFORE "am I on an edge?", and dune swallowed fringe completely — zero fringe sites on
            // the whole island, silently, because the grass band's seaward edge always has the marram
            // band within reach. Both edges exist and they look different.
            foreach (string habitat in new[]
            {
                StPetersGrass.HabitatSward, StPetersGrass.HabitatMeadow,
                StPetersGrass.HabitatFringe, StPetersGrass.HabitatDune,
                StPetersGrass.HabitatHeadland,
            })
                Assert.IsTrue(counts.ContainsKey(habitat) && counts[habitat] > 0,
                    $"No site on the island resolved to '{habitat}'. The library bakes art for it and " +
                    $"nothing would ever use it. Got: {Summary(counts)}.");

            // The inland grass must dominate — if the edges outnumber the middle, a threshold has
            // inverted and the island would read as all coast.
            int inland = counts[StPetersGrass.HabitatSward] + counts[StPetersGrass.HabitatMeadow];
            Assert.Greater(inland, sites.Count / 3,
                $"Inland sward+meadow is only {inland} of {sites.Count}. Got: {Summary(counts)}.");
        }

        [Test]
        public void TheScatter_IsDeterministic_SoARebuildReproducesTheIsland()
        {
            var terrain = Terrain();
            var a = StPetersGrass.Scatter(terrain);
            var b = StPetersGrass.Scatter(terrain);

            Assert.AreEqual(a.Count, b.Count, "Two scatters produced different counts.");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Position, b[i].Position, $"Tuft {i} moved between scatters.");
                Assert.AreEqual(a[i].Habitat, b[i].Habitat, $"Tuft {i} changed habitat between scatters.");
                Assert.AreEqual(a[i].Roll, b[i].Roll, $"Tuft {i} would draw a different blade.");
                Assert.AreEqual(a[i].Tint, b[i].Tint, $"Tuft {i} changed colour between scatters.");
            }
        }

        // =====================================================================================
        //  the shore
        // =====================================================================================

        [Test]
        public void TheShore_PlantsEveryTidalZone_IncludingTheWetOnes()
        {
            var sites = StPetersShorePlants.Scatter(Terrain());
            var byZone = new Dictionary<string, int>();
            foreach (var s in sites)
                byZone[s.Zone] = byZone.TryGetValue(s.Zone, out int n) ? n + 1 : 1;

            foreach (string zone in new[]
            {
                StPetersShorePlants.ZoneFringe, StPetersShorePlants.ZoneMid,
                StPetersShorePlants.ZoneLowMarsh, StPetersShorePlants.ZoneHighMarsh,
                StPetersShorePlants.ZoneUpland,
            })
                Assert.IsTrue(byZone.ContainsKey(zone) && byZone[zone] > 0,
                    $"Nothing planted in the '{zone}' zone. Got: {Summary(byZone)}.");

            // ⚠ THE ONE THAT WAS WRONG, and it is the owner's actual ask. The first cut put the mid
            // intertidal's floor at −1.6 m, which swallowed the whole 25 m reef shelf into "intertidal"
            // and left the subtidal with 38 plants against 865 rockweed. That is not "the first pass at
            // populating the ocean floor" by any reading. The shelf is the fringe.
            Assert.Greater(byZone[StPetersShorePlants.ZoneFringe], 200,
                "The subtidal fringe is nearly empty — this is the ocean-floor pass, and it is the " +
                "shelf that carries it. Check StPetersShorePlants.MidIntertidalFloorElevation against " +
                $"the reef shelf's own {StPetersBuilder.ReefShelfInnerElevation} m. Got: {Summary(byZone)}.");

            Assert.Less(sites.Count, 2500,
                $"{sites.Count} shore plants is past budget — every one is a SpriteRenderer plus a " +
                "tide view.");
        }

        [Test]
        public void NothingPlantsInDeepWater()
        {
            var terrain = Terrain();
            foreach (var s in StPetersShorePlants.Scatter(terrain))
            {
                float e = terrain.ElevationAt(s.Position);
                Assert.GreaterOrEqual(e, StPetersShorePlants.SubtidalFloorElevation,
                    $"A {s.SpeciesKey} was planted at {e:F2} m, below the " +
                    $"{StPetersShorePlants.SubtidalFloorElevation} m limit. Past that the water column " +
                    "is deeper than the seabed reads through AND the painted ground has stopped — it " +
                    "would be an invisible draw call.");
            }
        }

        [Test]
        public void SubtidalBedsThinOutAsTheWaterDeepens()
        {
            // The owner's words: beds where the water is shallow enough to see through, thinning out as
            // it deepens. Keyed to the water shader's own see-through depth, not to taste.
            Assert.AreEqual(1f, StPetersShorePlants.DepthFade(-StPetersShorePlants.ShallowSeeThroughDepthM),
                            1e-4f, "At the shader's see-through depth a bed should still be full density.");
            Assert.AreEqual(1f, StPetersShorePlants.DepthFade(0f), 1e-4f);
            Assert.AreEqual(0f, StPetersShorePlants.DepthFade(StPetersShorePlants.SubtidalFloorElevation),
                            1e-4f, "At the planting floor the density must have reached zero, or there " +
                                   "is a hard edge to the beds instead of a fade.");

            float mid = StPetersShorePlants.DepthFade(
                (StPetersShorePlants.SubtidalFloorElevation - StPetersShorePlants.ShallowSeeThroughDepthM)
                * 0.5f);
            Assert.Greater(mid, 0f);
            Assert.Less(mid, 1f, "The fade is not actually fading between the two rungs.");
        }

        [Test]
        public void TheZoneStaircase_RisesAndCoversTheWholePaintedShore()
        {
            // Walk the whole authored range and check the bands are contiguous and ordered — a gap
            // would be a ring of shore with nothing growing on it, which reads as a bug, not a beach.
            string previous = null;
            var order = new List<string>();
            for (float e = StPetersShorePlants.SubtidalFloorElevation;
                 e < StPetersShoreMap.GrassFloorElevation; e += 0.05f)
            {
                string zone = StPetersShorePlants.ZoneAt(e);
                Assert.IsNotNull(zone, $"Elevation {e:F2} m is inside the shore but plants nothing.");
                if (zone != previous) { order.Add(zone); previous = zone; }
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    StPetersShorePlants.ZoneFringe, StPetersShorePlants.ZoneMid,
                    StPetersShorePlants.ZoneLowMarsh, StPetersShorePlants.ZoneHighMarsh,
                    StPetersShorePlants.ZoneUpland,
                },
                order,
                "The tidal staircase is out of order or a band is missing — walking up from the seabed " +
                $"gave: {string.Join(" → ", order)}.");

            Assert.IsNull(StPetersShorePlants.ZoneAt(StPetersShorePlants.SubtidalFloorElevation - 0.1f),
                          "Deep water must plant nothing.");
            Assert.IsNull(StPetersShorePlants.ZoneAt(StPetersShoreMap.GrassFloorElevation + 0.1f),
                          "The meadow is the grass layer's job, not the shore's.");
        }

        [Test]
        public void EveryZoneSpecies_IsOneTheKitActuallyDeclares()
        {
            // A typo here plants nothing and warns at build time; catching it in a test is cheaper than
            // catching it in a screenshot of an empty shore.
            var known = new HashSet<string>(HiddenHarbours.Art.Editor.ShorePlantCatalog.SpeciesKeys);
            foreach (var kv in StPetersShorePlants.ZoneSpecies)
            {
                Assert.IsNotEmpty(kv.Value, $"Zone '{kv.Key}' lists no species.");
                foreach (string s in kv.Value)
                    Assert.IsTrue(known.Contains(s),
                        $"Zone '{kv.Key}' lists '{s}', which the shore plant kit does not declare.");
            }
        }

        static string Summary(Dictionary<string, int> d) =>
            string.Join(", ", d.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}"));
    }
}
