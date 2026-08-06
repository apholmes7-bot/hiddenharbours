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

        /// <summary>
        /// <b>The owner's ask, measured.</b> "The whole screen covered in grass almost" is not a tuft
        /// COUNT — a count says nothing about whether the ground between them shows — so the floor here
        /// is the coverage fraction <see cref="StPetersGrass.MeadowCoverage"/> computes: how much of the
        /// meadow lies within one tuft's own footprint of a tuft. Everything else about the layer is
        /// free to move as long as that number holds.
        ///
        /// <para>The ceiling above it is a different kind of claim and stays a literal on purpose: it
        /// is a COST the layer was signed off at (every tuft is a SpriteRenderer in a scene the owner
        /// has to open), not a property of the field, so it cannot be derived from the knobs that
        /// spend it. It is the one number in this file a reviewer should argue about.</para>
        /// </summary>
        [Test]
        public void TheMeadow_ReadsAsAField_AndStaysInsideItsBudget()
        {
            var terrain = Terrain();
            var sites = StPetersGrass.Scatter(terrain);

            // The measure counts each tuft as the ground its OWN art hides, so it is fed the widths
            // the planter will actually draw with — resolved through the planter's own chooser, which
            // is why that is a shared class rather than a lambda inside PlantGrass. Without the
            // library on disk it falls back to the nominal 1 m tuft, which under-reports.
            var library = HiddenHarbours.Art.Editor.GrassLibraryCatalog.Load();
            var imported = HiddenHarbours.Art.Editor.GrassLibraryCatalog.Imported(library);
            System.Func<StPetersGrass.GrassTuftSite, float> width = null;
            if (imported.Count > 0)
            {
                var chooser = new StPetersWoodsPlanter.GrassArtChooser(imported);
                width = s =>
                {
                    var e = chooser.Choose(s);
                    return e == null
                        ? StPetersGrass.TuftWidthMetres
                        : e.Width / (float)HiddenHarbours.Art.Editor.GrassLibraryCatalog.Ppu;
                };
            }

            float coverage = StPetersGrass.MeadowCoverage(terrain, sites, width);

            // The number CI reports, so a reviewer reads the retune without running Unity.
            Debug.Log($"[GreenOver] {sites.Count} tufts · meadow coverage {coverage:P1} · " +
                      $"step {StPetersGrass.GrassStep} m · threshold {StPetersGrass.SwatheThreshold} · " +
                      $"broad {sites.Count(s => s.Broad) * 100f / Mathf.Max(1, sites.Count):F0}% · " +
                      $"widths {(width == null ? "nominal (no library)" : "from the library")}");

            Assert.Greater(coverage, MeadowCoverageFloor,
                $"the meadow covers {coverage:P1} of the ground it stands on. The owner's ask was a " +
                $"FIELD — 'the whole screen covered in grass almost' — and below {MeadowCoverageFloor:P0} " +
                "it reads as tufts on bare ground again. The knobs are StPetersGrass.GrassStep " +
                $"({StPetersGrass.GrassStep} m; coverage rises as its inverse square), SwatheThreshold " +
                $"({StPetersGrass.SwatheThreshold}) and BroadClumpShare " +
                $"({StPetersGrass.BroadClumpShare}).");

            Assert.Less(sites.Count, TuftBudget,
                $"{sites.Count} tufts is past the {TuftBudget} this layer was signed off at. Every one " +
                "is a SpriteRenderer in a scene the owner opens; spend the budget on COVERAGE (broad " +
                "clumps cover twice the ground per renderer) before raising this number.");
        }

        /// <summary>
        /// How much of the meadow must fall under a tuft's own footprint.
        ///
        /// <para><b>0.80, and it is nearer the ceiling than it sounds.</b> The gates leave one: the
        /// swathe field rejects ~6% of the meadow outright and <c>ChanceOpen</c> another 3%, so only
        /// about 91% of candidate cells ever carry grass at ANY step — and that worn ground is wanted,
        /// it is what stops a field reading as a lawn. 0.80 is ~88% of what the gates leave reachable.
        /// The retune measures 0.826 at 35,026 tufts; the green-over it replaces measured 0.17.</para>
        ///
        /// <para>And the measure under it is deliberately conservative
        /// (<see cref="StPetersGrass.TuftFootprintDepthFraction"/> claims 0.7 of a sprite that is drawn
        /// standing over the ground behind it), so this is a floor on what the picture does, not an
        /// estimate of it. Buying the last few points costs renderers linearly — GrassStep 0.85 → 0.80
        /// measured ~0.86 at ~39.5k tufts — which is the owner's call to make on his own screen, not a
        /// bar to move here.</para>
        /// </summary>
        const float MeadowCoverageFloor = 0.80f;

        /// <summary>The signed-off renderer cost of the ground cover — the retune measured 35,026, and
        /// this carries ~20% over it: room for ordinary tuning, not room for a doubling. See the test
        /// above for why this one is a literal and the coverage floor is not.</summary>
        const int TuftBudget = 42000;

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
                // ⭐ VERGE joined the list at the 2026-08-05 retune. The library has baked art for it
                // since #425 and nothing had ever resolved to it, because until the paths were carved
                // out of the meadow there was no path EDGE to stand on.
                StPetersGrass.HabitatVerge,
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
                Assert.AreEqual(a[i].Broad, b[i].Broad, $"Tuft {i} changed clump size between scatters.");
                Assert.AreEqual(a[i].Mirror, b[i].Mirror, $"Tuft {i} flipped between scatters.");
                Assert.AreEqual(a[i].Tint, b[i].Tint, $"Tuft {i} changed colour between scatters.");
            }
        }

        // =====================================================================================
        //  the walked paths
        // =====================================================================================

        /// <summary>
        /// 🔴 <b>The paths have to survive the meadow.</b> This is the retune's one genuinely new
        /// failure mode: at a sixth of coverage a 2.5 m dirt track showed through whatever grew on it,
        /// and at the coverage the owner asked for it would simply be buried. So the splat's path and
        /// the meadow's bare tread are ONE number, and this is the pin that they still are.
        /// </summary>
        [Test]
        public void TheWalkedPaths_StayBare_AndWearAVerge()
        {
            Assert.AreEqual(StPetersStarterSplat.PathWidthMetres * 0.5f,
                            StPetersGrass.PathBareHalfWidthMetres, 1e-4f,
                "The meadow's bare tread has come off the splat's path width. Two numbers for one path " +
                "is how the ground ends up painted as dirt with grass growing over it.");

            var terrain = Terrain();
            var sites = StPetersGrass.Scatter(terrain);

            int verge = 0;
            foreach (var s in sites)
            {
                float d = StPetersGrass.DistanceToWalkedPath(s.Position);
                Assert.GreaterOrEqual(d, StPetersGrass.PathBareHalfWidthMetres,
                    $"a tuft at {s.Position} stands {d:F2} m from a path centre-line, inside the " +
                    "tread that is supposed to be walked bare");
                if (s.Habitat == StPetersGrass.HabitatVerge) verge++;
            }

            // The verge band is thin by construction, so this is a floor on "it exists", not a share.
            Assert.Greater(verge, 20,
                $"only {verge} tufts wear the trodden VERGE. The paths run the length of the village's " +
                "ground; a verge this thin means the band width or the habitat order is wrong.");

            // ⚠ And the paths must still be ON the meadow. A carve that missed the island entirely
            // would pass every assertion above by doing nothing at all.
            int nearPath = 0;
            foreach (var p in StPetersGrass.WalkedPaths)
                foreach (var pt in p)
                    if (StPetersGrass.IsPlantableMeadow(terrain, pt + Vector2.up * 4f)) nearPath++;
            Assert.Greater(nearPath, 0,
                "no point on either walked path has plantable meadow beside it — the carve is cutting " +
                "grass out of ground that never had any, so it is not doing what it claims.");
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

        /// <summary>
        /// <see cref="StPetersGrass.TuftWidthMetres"/> is the SIZE OF THE ART restated in a pure
        /// classifier that cannot read the manifest — the fallback the coverage measure uses when no
        /// caller supplies real widths. Checked against the manifest here: if the rig ever bakes
        /// narrower tufts, the fallback silently becomes a claim about a thinner field.
        /// </summary>
        [Test]
        public void TheCoverageMeasuresFallbackWidth_IsOneTheLibraryActuallyBakes()
        {
            var library = HiddenHarbours.Art.Editor.GrassLibraryCatalog.Load();
            if (library == null || library.Entries.Count == 0)
                Assert.Ignore("The grass library manifest is not on disk in this checkout.");

            // The narrowest variant the island can plant. A fallback wider than that would report
            // coverage the thinnest art cannot deliver.
            int narrowest = library.Entries.Min(e => e.Width);
            float narrowestM = narrowest / (float)HiddenHarbours.Art.Editor.GrassLibraryCatalog.Ppu;

            Assert.LessOrEqual(StPetersGrass.TuftWidthMetres, narrowestM + 1e-4f,
                $"the measure falls back to a {StPetersGrass.TuftWidthMetres} m tuft, but the " +
                $"narrowest baked variant is {narrowest} px — {narrowestM:F2} m.");
        }

        // =====================================================================================
        //  the shore's size
        // =====================================================================================

        /// <summary>
        /// <b>The owner's other note: the sea plants "stand out a little too much in their size".</b>
        /// The rig bakes every species at FULL GROWTH — right for a bake, and a shore where every plant
        /// is the biggest of its kind reads oversized — so the placement layer carries an authored
        /// per-species scale. This pins the two properties that make that safe rather than the taste
        /// itself: the scales are AUTHORED (a re-run of the Def builder must not reset them) and none
        /// of them shrinks a plant below what 32 px to the metre can draw.
        /// </summary>
        [Test]
        public void EveryShorePlant_IsDrawnAtAScaleThatIsAuthoredAndLegible()
        {
            var keys = HiddenHarbours.Art.Editor.ShorePlantCatalog.SpeciesKeys;
            int scaledDown = 0;

            foreach (string key in keys)
            {
                var def = UnityEditor.AssetDatabase.LoadAssetAtPath<HiddenHarbours.Art.ShorePlantDef>(
                    $"{HiddenHarbours.Art.Editor.ShorePlantDefBuilder.DefFolder}/{key}.asset");
                if (def == null) continue;

                Assert.That(def.PlantedScale, Is.InRange(0.4f, 1f),
                    $"{key} is planted at ×{def.PlantedScale}. Above 1 the placement layer is doing the " +
                    "rig's job — re-bake the species instead; below 0.4 the sprite has stopped being " +
                    "legible pixel art and wants a smaller bake, not a smaller transform.");

                // 🔴 The legibility floor. At 32 px = 1 m a plant drawn under a quarter of a metre is
                // under 8 px tall, which is mush however botanically correct the number is — this is
                // why Irish moss stops at ×0.5 (0.27 m) instead of at its real 0.05–0.2 m.
                Assert.Greater(def.PlantedStandingHeightM, 0.25f,
                    $"{key} would be drawn {def.PlantedStandingHeightM:F2} m tall — under 8 px. Below " +
                    "this the pixels stop describing a plant.");

                if (def.PlantedScale < 1f) scaledDown++;
            }

            Assert.Greater(scaledDown, 0,
                "no species carries a planted scale at all — the .asset rows are missing the key, so " +
                "every Def deserialized to the C# default and the owner's note went unanswered. (A " +
                "field the YAML omits takes the C# default silently; that is exactly the trap.)");
        }

        static string Summary(Dictionary<string, int> d) =>
            string.Join(", ", d.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}"));
    }
}
