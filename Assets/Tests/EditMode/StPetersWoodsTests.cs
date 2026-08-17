using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE WOODS AND THE MEADOW.</b> <see cref="StPetersWoods"/> decides where a tree stands, which
    /// species it is and which bloom grows there, all as pure functions of the authored terrain — so these
    /// assert the forest the scene is planted from without loading anything.
    ///
    /// <para>What matters, and why each is here:</para>
    /// <list type="bullet">
    /// <item><b>Tamarack cannot be planted.</b> The pass-2 kit holds it back by ruling and it is absent from
    /// <c>Trees.json</c>. The planter only plants what the kit's own scan returns, so this is asserted at the
    /// contract rather than trusted to nobody typing the name.</item>
    /// <item><b>The interior is REVERTING, not forested</b> (§5.1) — a mosaic of stands and open ground,
    /// which means the woods must cover a fraction of the island, not all of it. Sabotage-measured against
    /// a fill.</item>
    /// <item><b>Species follow habitat</b>: hardwoods only in the sheltered core, black spruce on the
    /// blasted fringe. That gradient is the real Acadian one and it is what makes the island read as one
    /// place.</item>
    /// <item><b>Nothing grows where the player has to be</b>: not in the village, not on the spawn, not
    /// across the crossing's approach, not on the dock.</item>
    /// <item><b>There is a treeless margin all round.</b> Trees do not grow to the tideline.</item>
    /// </list>
    /// </summary>
    public class StPetersWoodsTests
    {
        private GameObject _go;
        private TidalTerrain _terrain;

        /// <summary>The nine species the pass-2 kit ships. Named here ONLY as the expectation to check the
        /// contract against — the planter never reads a list like this.</summary>
        static readonly string[] NineSpecies =
        {
            "RedSpruce", "BlackSpruce", "BalsamFir", "WhitePine", "WhiteCedar",
            "WhiteBirch", "RedMaple", "RedOak", "TremblingAspen",
        };

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("StPetersTerrain_WoodsTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        static List<string> Available() => NineSpecies.ToList();

        List<StPetersWoods.TreeSite> Trees() =>
            StPetersWoods.ScatterTrees(_terrain, Available(), _ => 4);

        // =================================================================================
        //  TAMARACK IS HELD BACK
        // =================================================================================

        [Test]
        public void TheKitClaimsNineSpecies_AndTamarackIsNotOneOfThem()
        {
            var contract = TreeKitCatalog.Load();
            Assert.IsNotNull(contract?.trees,
                "the committed tree contract must be readable — the planter plants what it claims");

            var species = contract.trees.Select(t => t.species).ToArray();
            Assert.AreEqual(9, species.Length,
                "the pass-2 kit ships NINE placeable species (#334). Tamarack is held back by ruling so the " +
                "sprite-light gate stays unconditional; if this becomes 10, that ruling changed and the " +
                "held-back species needs a decision, not a silent inclusion.");
            CollectionAssert.DoesNotContain(species, "Tamarack",
                "Tamarack is HELD BACK. Its PNG is on disk, which is exactly why this is asserted against " +
                "the CONTRACT and not against the folder.");
            CollectionAssert.AreEquivalent(NineSpecies, species,
                "the nine are the nine — a re-bake that swaps one out should fail here, where the message " +
                "says what the habitat model is choosing between");
        }

        [Test]
        public void NoPlantedTreeIsEverASpeciesTheKitDidNotOffer()
        {
            var offered = new HashSet<string>(Available());
            foreach (var t in Trees())
                Assert.IsTrue(offered.Contains(t.Species),
                    $"'{t.Species}' was planted but the kit did not offer it — the habitat model must only " +
                    "ever pick from what it is handed, or a held-back species reaches the island");
        }

        [Test]
        public void EveryHabitatResolvesToARealSpecies_EvenWhenItsFirstChoiceIsMissing()
        {
            // Preferences are ORDERED lists precisely so a habitat degrades to its next-best species rather
            // than leaving bare ground. Prove it bites: offer only ONE species and every habitat must still
            // find it.
            var onlyOne = new List<string> { "BalsamFir" };
            for (float ex = 0f; ex <= 1f; ex += 0.1f)
            for (float wet = 0f; wet <= 1f; wet += 0.1f)
            {
                var pick = StPetersWoods.FirstAvailable(StPetersWoods.SpeciesPreference(ex, wet), onlyOne);
                Assert.AreEqual("BalsamFir", pick,
                    $"exposure {ex:0.0} / wetness {wet:0.0} found nothing when only balsam fir was offered — " +
                    "a habitat whose ideal species is missing must fall back, not leave a hole");
            }

            // And with NOTHING offered it must answer null rather than throw or invent.
            Assert.IsNull(StPetersWoods.FirstAvailable(
                StPetersWoods.SpeciesPreference(0.5f, 0.5f), new List<string>()));
        }

        // =================================================================================
        //  THE INTERIOR IS REVERTING, NOT FORESTED
        // =================================================================================

        [Test]
        public void TheWoodsAreStandsWithMeadowBetween_NotAFill()
        {
            var trees = Trees();
            // ⚠ Bounds re-pinned 2026-07-30 with the island shrink (240 × 140 m ≈ 29% of the old area):
            // the old floor of 200 was sized for a 450 × 260 island. Measured ~73 trees at the new
            // scale with ExposureDepthMetres scaled to 45 — the same trees-per-square-metre the big
            // island carried, on the small one.
            Assert.Greater(trees.Count, 40,
                $"only {trees.Count} trees — §5.1 wants 'forest: interior cover, hiding ruins', which a " +
                "handful of trees is not, even on the re-ruled 240 m island");
            Assert.Less(trees.Count, 500,
                $"{trees.Count} trees is a plantation, not a reverting island — and it is a lot of " +
                "GameObjects for one region (rule 7)");

            // Measure the mosaic directly: walk the plantable interior and count how much of it is stand.
            int stand = 0, open = 0;
            for (float x = -150f; x < 290f; x += 4f)
            for (float y = -125f; y < 125f; y += 4f)
            {
                var p = new Vector2(x, y);
                if (!StPetersWoods.IsPlantable(_terrain, p)) continue;
                if (StPetersWoods.InStand(p, _terrain.ElevationAt(p))) stand++; else open++;
            }

            Assert.Greater(stand + open, 500, "sanity: the interior was actually walked");
            float wooded = stand / (float)(stand + open);
            Assert.Greater(wooded, 0.1f,
                $"only {wooded:P0} of the interior carries woods — the island reads as bald");
            Assert.Less(wooded, 0.7f,
                $"{wooded:P0} of the interior is wooded — 'the reverting interior' is mostly open with woods " +
                "coming back in patches, and a near-solid canopy hides the meadows, the marsh and the ruins " +
                "the same section asks for");
        }

        [Test]
        public void Sabotage_AUniformForest_WouldCoverTheWholeInterior()
        {
            // The obvious wrong implementation: plant every plantable cell. Measured so the mosaic
            // assertion above is shown to be doing work rather than passing on a technicality.
            int plantable = 0, standing = 0;
            for (float x = -150f; x < 290f; x += 4f)
            for (float y = -125f; y < 125f; y += 4f)
            {
                var p = new Vector2(x, y);
                if (!StPetersWoods.IsPlantable(_terrain, p)) continue;
                plantable++;
                if (StPetersWoods.InStand(p, _terrain.ElevationAt(p))) standing++;
            }
            Assert.Greater(plantable, standing * 3 / 2,
                $"a uniform fill would plant {plantable} cells where the stand field plants {standing} — if " +
                "those were close, the mosaic would not be a mosaic");
        }

        // =================================================================================
        //  HABITAT
        // =================================================================================

        [Test]
        public void HardwoodsOnlyGetIntoTheShelteredCore()
        {
            var hardwoods = new HashSet<string> { "RedMaple", "RedOak", "WhiteBirch", "TremblingAspen" };

            // On the blasted fringe the preference list must not even mention a hardwood — a red oak does
            // not grow in salt wind, and the whole point of the gradient is that the fringe looks different.
            for (float wet = 0f; wet <= 1f; wet += 0.1f)
            foreach (string s in StPetersWoods.SpeciesPreference(0.9f, wet))
                Assert.IsFalse(hardwoods.Contains(s),
                    $"'{s}' is offered on the most exposed ground (wetness {wet:0.0}) — the fringe is " +
                    "spruce and fir country");

            // And in the sheltered core they must lead, or the island is all conifer and the two halves of
            // the gradient read the same.
            var core = StPetersWoods.SpeciesPreference(0.05f, 0.2f);
            Assert.IsTrue(hardwoods.Contains(core[0]),
                $"the sheltered core leads with '{core[0]}' — the hardwoods belong here, and if conifers " +
                "lead everywhere the habitat model is decoration");
        }

        [Test]
        public void TheDampHollowsGetCedarAndBlackSpruce_AndTheDryRidgesDoNot()
        {
            var wetLovers = new HashSet<string> { "WhiteCedar", "BlackSpruce" };

            for (float ex = 0f; ex <= 1f; ex += 0.1f)
            {
                var wet = StPetersWoods.SpeciesPreference(ex, 0.95f);
                Assert.IsTrue(wetLovers.Contains(wet[0]),
                    $"damp ground at exposure {ex:0.0} leads with '{wet[0]}' — cedar and black spruce are " +
                    "the ones that take wet feet");
            }

            var dryCore = StPetersWoods.SpeciesPreference(0.05f, 0.05f);
            Assert.IsFalse(wetLovers.Contains(dryCore[0]),
                $"the dry sheltered core leads with '{dryCore[0]}' — if the wet-ground species led here too, " +
                "wetness would be doing nothing");
        }

        [Test]
        public void TheExposureFieldReadsTheWeatherCoastHarderThanTheLee()
        {
            // Same elevation, opposite coasts. The weather side must come out more exposed, or the §5.1
            // split that the ground already draws stops at the ground.
            // ⚠ ±55, sized to the re-ruled 70 m Y radius (2026-07-30): the probes must sit ON the
            // island's collar where the inland fraction is a real fraction — the old ±110 probes fell
            // into open water at the new size, where BOTH coasts clamp to full exposure and the
            // comparison measures nothing.
            var c = StPetersBuilder.IslandCenter;
            var weather = c + new Vector2(0f, -55f);   // south
            var lee     = c + new Vector2(0f, 55f);    // north

            Assert.IsTrue(StPetersShoreMap.IsWeatherCoast(weather), "sanity: south is the weather coast");
            Assert.IsFalse(StPetersShoreMap.IsWeatherCoast(lee), "sanity: north is the lee");

            Assert.AreEqual(StPetersWoods.InlandFraction(weather), StPetersWoods.InlandFraction(lee), 1e-4f,
                "sanity: the two probes sit the same distance inside the plateau edge, so the SECTOR is the " +
                "only variable between them");
            Assert.Greater(StPetersWoods.ExposureAt(weather), StPetersWoods.ExposureAt(lee),
                "the weather coast must read as more exposed than the lee at the same distance inland");
        }

        [Test]
        public void ExposureFallsInlandAndIsAlwaysABoundedFraction()
        {
            var c = StPetersBuilder.IslandCenter;
            Assert.AreEqual(0f, StPetersWoods.ExposureAt(c), 1e-4f,
                "the middle of the plateau is the most sheltered ground there is");

            // And the collar really is exposed, or the field is flat and the gradient is decoration.
            var edge = c + new Vector2(StPetersBuilder.IslandRadius - 6f, 0f);
            Assert.Greater(StPetersWoods.ExposureAt(edge), 0.7f,
                "ground six metres inside the plateau edge must read as nearly fully exposed");

            for (float x = -150f; x < 290f; x += 9f)
            for (float y = -125f; y < 125f; y += 9f)
            {
                var p = new Vector2(x, y);
                float ex = StPetersWoods.ExposureAt(p);
                Assert.GreaterOrEqual(ex, 0f, $"exposure at {p} is a 0..1 fraction");
                Assert.LessOrEqual(ex, 1f, $"exposure at {p} is a 0..1 fraction");
            }
        }

        // =================================================================================
        //  THE STANDS ARE MIXED
        // =================================================================================

        [Test]
        public void MostOfTheKitActuallyReachesTheIsland()
        {
            // The first version of the picker always took each habitat's LEADER, so every patch of a given
            // habitat resolved to the same tree. The island came out with FOUR species out of nine — red
            // oak, birch, aspen, pine and fir sat second and third on lists whose head was always available
            // and never appeared at all. A nine-species kit that plants four is a kit half wasted.
            var planted = new HashSet<string>(Trees().Select(t => t.Species));
            // ⚠ 6 of 9, not the 7 this held on the 450 m island: the 2026-07-30 shrink leaves ~73
            // trees, and at that sample size a rare tail species legitimately misses some layouts.
            // The monoculture bug this guards against planted FOUR.
            Assert.GreaterOrEqual(planted.Count, 6,
                $"only {planted.Count} of the kit's 9 species reached the island ({string.Join(", ", planted.OrderBy(s => s))}) " +
                "— a habitat must plant a MIXED stand, not one species repeated");
        }

        [Test]
        public void EachHabitatStillReadsAsItsOwn_TheLeaderDominates()
        {
            // Mixing must not become mush: a hardwood grove has to be mostly hardwood or the gradient stops
            // meaning anything. Roll the picker across its whole input range for one habitat and check the
            // leader takes the largest share.
            var pref = StPetersWoods.SpeciesPreference(0.05f, 0.1f);   // sheltered + dry: hardwood country
            var counts = new Dictionary<string, int>();
            const int rolls = 1000;
            for (int i = 0; i < rolls; i++)
            {
                string s = StPetersWoods.PickSpecies(pref, Available(), i / (float)rolls);
                counts.TryGetValue(s, out int n);
                counts[s] = n + 1;
            }

            var leader = pref[0];
            Assert.AreEqual(leader, counts.OrderByDescending(kv => kv.Value).First().Key,
                $"'{leader}' leads this habitat's list so it must win the most picks — otherwise the habitat " +
                "model is a shuffle with extra steps");
            Assert.Greater(counts[leader] / (float)rolls, 0.25f,
                $"the leader took only {counts[leader] / (float)rolls:P0} of picks — too flat to read as a " +
                "hardwood grove");
            Assert.Less(counts[leader] / (float)rolls, 0.75f,
                $"the leader took {counts[leader] / (float)rolls:P0} of picks — that is back to a monoculture");
            Assert.Greater(counts.Count, 3,
                "and the rest of the list has to get in behind it, or the stand is one species again");
        }

        [Test]
        public void MoreThanOneFlowerSpeciesReachesTheMeadow()
        {
            var all = new List<string>
            {
                "BlueFlag", "Buttercup", "Fireweed", "Goldenrod", "LadySlipper",
                "LupinBlue", "LupinPink", "LupinPurple", "LupinWhite",
                "OxeyeDaisy", "QueenAnne", "WildRose",
            };
            var planted = new HashSet<string>(
                StPetersWoods.ScatterFlowers(_terrain, all).Select(f => f.Species));
            Assert.GreaterOrEqual(planted.Count, 7,
                $"only {planted.Count} flower species reached the island ({string.Join(", ", planted.OrderBy(s => s))}) " +
                "— the same monoculture bug the trees had, on the meadow");
            Assert.IsTrue(planted.Contains("WildRose") || planted.Contains("Buttercup")
                          || planted.Contains("OxeyeDaisy") || planted.Contains("QueenAnne"),
                "the open-meadow crowd must actually appear — §5.1's brief names wild roses for the " +
                "reverting interior by name");
        }

        // =================================================================================
        //  THE CLEARINGS
        // =================================================================================

        [Test]
        public void NothingGrowsWhereThePlayerHasToBe()
        {
            foreach (var t in Trees())
            {
                Assert.Greater(Vector2.Distance(t.Position, StPetersBuilder.VillageHearthPos),
                               StPetersWoods.VillageClearingRadius - 0.01f,
                    $"a {t.Species} at {t.Position} is inside the village — a village stands in a CLEARING, " +
                    "and a spruce through the schoolhouse is the first thing the player would see");

                // Ginny's plot is the island's SECOND clearing (2026-08-16). She lives IN these woods
                // now, so this is the assertion that stops the planter from walling her in.
                Assert.Greater(Vector2.Distance(t.Position, StPetersGinnyPlot.CottagePos),
                               StPetersGinnyPlot.ClearingRadius - 0.01f,
                    $"a {t.Species} at {t.Position} stands inside Ginny's plot — her cottage, her garden " +
                    "and her three sheds are in a clearing, not under a canopy");

                Assert.Greater(Vector2.Distance(t.Position, StPetersBuilder.StartSpawnPos),
                               StPetersWoods.SpawnClearingRadius - 0.01f,
                    $"a {t.Species} at {t.Position} is on the start spawn");

                Assert.Greater(StPetersShoreMap.DistanceToSegment(
                                   t.Position, StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo),
                               StPetersWoods.CrossingClearance - 0.01f,
                    $"a {t.Species} at {t.Position} crowds the crossing — the bar is the region's one lesson " +
                    "and you must be able to SEE it from the island");

                Assert.Greater(Vector2.Distance(t.Position, StPetersBuilder.DockZonePos),
                               StPetersWoods.DockClearance - 0.01f,
                    $"a {t.Species} at {t.Position} is on the dock");
            }
        }

        [Test]
        public void ThereIsATreelessMarginAllRound_BecauseTreesDoNotGrowToTheTideline()
        {
            float springHigh = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;
            Assert.Greater(StPetersWoods.TreeLineElevation, springHigh,
                "the tree line must sit above the highest water — a tree in the surf is not a tree");
            Assert.Greater(StPetersWoods.TreeLineElevation, StPetersShoreMap.GrassFloorElevation,
                "and above the meadow's own floor, so there is a strip of open grass between the woods and " +
                "the beach rather than a canopy overhanging the shore");

            foreach (var t in Trees())
                Assert.GreaterOrEqual(_terrain.ElevationAt(t.Position), StPetersWoods.TreeLineElevation,
                    $"a {t.Species} at {t.Position} stands below the tree line");

            // And the margin is real, not notional: sample just inside the plateau edge all the way
            // round. ⚠ On the ellipse's own parametric ring (rx−1, ry−1), not a fixed radius — a
            // 224 m circle was "just inside" only the OLD island's long axis, and after the
            // 2026-07-30 shrink it is open sea on every bearing.
            int checkedPoints = 0;
            for (float a = 0f; a < 360f; a += 3f)
            {
                var p = StPetersBuilder.IslandCenter + new Vector2(
                    Mathf.Cos(a * Mathf.Deg2Rad) * (StPetersBuilder.IslandRadius - 1f),
                    Mathf.Sin(a * Mathf.Deg2Rad) * (StPetersBuilder.IslandRadiusY - 1f));
                if (_terrain.ElevationAt(p) < StPetersWoods.TreeLineElevation) continue;
                checkedPoints++;
            }
            Assert.Greater(checkedPoints, 0, "sanity: the ring was sampled");
        }

        // =================================================================================
        //  FLOWERS
        // =================================================================================

        [Test]
        public void TheFlowersFollowTheirRealHabitats()
        {
            // Blue flag is an iris: it grows with its feet in water.
            var wet = StPetersWoods.FlowerPreference(Vector2.zero, inWoods: false, wetness: 0.9f,
                                                    distanceToVillage: 500f);
            Assert.AreEqual("BlueFlag", wet[0], "the wet hollows are blue flag iris");

            // Lady's slipper is the provincial flower and it grows in damp shade under conifers — the one
            // bloom that belongs INSIDE a stand.
            var woods = StPetersWoods.FlowerPreference(Vector2.zero, inWoods: true, wetness: 0.3f,
                                                      distanceToVillage: 500f);
            Assert.AreEqual("LadySlipper", woods[0], "the forest floor is lady's slipper");

            // Fireweed takes disturbed ground — dooryards and old gardens gone over.
            var near = StPetersWoods.FlowerPreference(Vector2.zero, inWoods: false, wetness: 0.3f,
                                                     distanceToVillage: 10f);
            Assert.AreEqual("Fireweed", near[0], "disturbed ground by the village is fireweed");

            // And the open meadow leads with a lupin, whichever colour this hillside is.
            var meadow = StPetersWoods.FlowerPreference(new Vector2(120f, 40f), inWoods: false,
                                                       wetness: 0.3f, distanceToVillage: 500f);
            Assert.IsTrue(meadow[0].StartsWith("Lupin"),
                $"the open meadow leads with '{meadow[0]}' — lupin is the iconic one and the docs name it");
        }

        [Test]
        public void TheLupinColourVariesAcrossTheIsland_ButIsStableInPlace()
        {
            var colours = new HashSet<string>();
            for (float x = -150f; x < 290f; x += 17f)
            for (float y = -120f; y < 120f; y += 17f)
            {
                var p = new Vector2(x, y);
                var pref = StPetersWoods.FlowerPreference(p, false, 0.3f, 500f);
                if (pref[0].StartsWith("Lupin")) colours.Add(pref[0]);
            }
            Assert.Greater(colours.Count, 1,
                "the island should carry more than one lupin colour — they grow in coloured drifts, and one " +
                "shade everywhere would read as a bug");

            // Stable in place: the same metre is always the same colour.
            var probe = new Vector2(33f, -21f);
            Assert.AreEqual(StPetersWoods.FlowerPreference(probe, false, 0.3f, 500f)[0],
                            StPetersWoods.FlowerPreference(probe, false, 0.3f, 500f)[0]);
        }

        [Test]
        public void TheFlowersAreAccentsOnTheMeadow_NotACarpet()
        {
            var flowers = StPetersWoods.ScatterFlowers(_terrain, new List<string>
            {
                "BlueFlag", "Buttercup", "Fireweed", "Goldenrod", "LadySlipper",
                "LupinBlue", "LupinPink", "LupinPurple", "LupinWhite",
                "OxeyeDaisy", "QueenAnne", "WildRose",
            });

            // ⚠ Bounds re-pinned 2026-07-30 for the 240 × 140 island (measured ~43 blooms — the same
            // blooms-per-metre the 450 m island carried at its ~225).
            Assert.Greater(flowers.Count, 20, "a meadow in summer has flowers in it");
            Assert.Less(flowers.Count, 350,
                $"{flowers.Count} blooms is a carpet — flowers read as flowers because the meadow around " +
                "them is grass (and every one is a GameObject, rule 7)");

            // They obey the same clearings the trees do — a lupin on the pier would be as wrong as a spruce.
            foreach (var f in flowers)
                Assert.IsTrue(StPetersWoods.IsPlantable(_terrain, f.Position),
                    $"a {f.Species} at {f.Position} grew somewhere nothing should");
        }

        [Test]
        public void ThePatchTierIsNeverAskedFor_BecauseTheOwnerHasNotRuledOnIt()
        {
            // #215 shipped the flowers; the PATCH tier is still awaiting the owner's verdict. The scatter
            // only ever expresses "clump or not", so there is no way for a patch to reach the island.
            var flowers = StPetersWoods.ScatterFlowers(_terrain, new List<string> { "Buttercup" });
            Assert.IsNotEmpty(flowers);
            Assert.IsTrue(flowers.Any(f => f.Clump), "some blooms are clumps");
            Assert.IsTrue(flowers.Any(f => !f.Clump), "and some are single stems");
            // The type carries no third state — asserted by construction, and pinned here so adding one
            // is a deliberate act rather than a slip.
            Assert.AreEqual(typeof(bool), typeof(StPetersWoods.FlowerSite).GetField("Clump").FieldType,
                "the tier choice is a BOOL on purpose: clump or single, and no way to say 'patch'");
        }

        // =================================================================================
        //  DETERMINISM
        // =================================================================================

        [Test]
        public void ThePlantingIsDeterministic_NoRng()
        {
            var a = Trees();
            var b = Trees();
            Assert.AreEqual(a.Count, b.Count, "the forest must reproduce exactly on a rebuild (rule 5)");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Species, b[i].Species);
                Assert.AreEqual(a[i].Variant, b[i].Variant);
                Assert.AreEqual(a[i].Position.x, b[i].Position.x, 1e-6f);
                Assert.AreEqual(a[i].Position.y, b[i].Position.y, 1e-6f);
            }

            var species = new List<string> { "Buttercup", "LupinBlue", "BlueFlag", "LadySlipper" };
            var fa = StPetersWoods.ScatterFlowers(_terrain, species);
            var fb = StPetersWoods.ScatterFlowers(_terrain, species);
            Assert.AreEqual(fa.Count, fb.Count);
            for (int i = 0; i < fa.Count; i++)
            {
                Assert.AreEqual(fa[i].Species, fb[i].Species);
                Assert.AreEqual(fa[i].Clump, fb[i].Clump);
                Assert.AreEqual(fa[i].Position, fb[i].Position);
            }
        }

        [Test]
        public void EveryVariantAskedForExistsOnItsSheet()
        {
            // The variant is hashed, so it must be clamped into the species' real variant count — asking
            // for variant 4 of a 3-variant sheet resolves to nothing and plants an invisible tree.
            foreach (var t in StPetersWoods.ScatterTrees(_terrain, Available(), _ => 3))
            {
                Assert.GreaterOrEqual(t.Variant, 0, $"{t.Species} got variant {t.Variant}");
                Assert.Less(t.Variant, 3, $"{t.Species} got variant {t.Variant} from a 3-variant sheet");
            }

            // And a species that ships a single variant must never be asked for a second one.
            foreach (var t in StPetersWoods.ScatterTrees(_terrain, Available(), _ => 1))
                Assert.AreEqual(0, t.Variant, "a one-variant sheet only has variant 0");
        }

        // =================================================================================
        //  THE SHRUB LAYER — heath, gale, meadowsweet, hazel, rose and raspberry
        //
        //  The shrubs are planted from the SAME habitat fields as the woods, but by the rig's own
        //  five-habitat scheme, and every species is placed by the habitat THE CONTRACT gives it.
        //  So what is asserted here is that this file decides which habitat a patch of island IS,
        //  and the contract decides what grows in one.
        // =================================================================================

        /// <summary>The six species St Peters bakes, and the habitat the contract puts each in. Named
        /// here as the EXPECTATION to check the contract against — the planter reads the contract.</summary>
        static readonly (string species, string habitat)[] SixShrubs =
        {
            ("LowbushBlueberry", "barren"), ("SweetGale", "bog"), ("Meadowsweet", "swale"),
            ("BeakedHazelnut", "woods"), ("WildRose", "edge"), ("Raspberry", "edge"),
        };

        static List<string> ShrubsAvailable() => SixShrubs.Select(s => s.species).ToList();

        List<StPetersShrubs.ShrubSite> Shrubs(int variants = 4) =>
            StPetersShrubs.Scatter(_terrain, ShrubsAvailable(), HabitatFromContract(), variants);

        /// <summary>The species→habitat lookup the planter uses: straight out of the committed
        /// contract, never a table in this file.</summary>
        static System.Func<string, string> HabitatFromContract()
        {
            var contract = ShrubCatalog.Load();
            Assert.IsNotNull(contract, $"no readable contract at {ShrubCatalog.ContractPath}");
            var byKey = contract.Species.ToDictionary(e => e.Key, e => e.Habitat);
            return s => byKey.TryGetValue(s, out string h) ? h : null;
        }

        [Test]
        public void TheSixBakedSpecies_CoverAllFiveHabitats_AndTheContractAgreesWhichIsWhich()
        {
            // The slice was chosen to be the SMALLEST bake that can dress the island: five habitats, six
            // species (edge gets two — §5.1's brief names "wild roses/raspberries" together). If the
            // contract ever moves a species between habitats, the island changes with no code edit —
            // so the pairing is asserted against the contract rather than trusted to this list.
            var habitatOf = HabitatFromContract();
            foreach (var (species, habitat) in SixShrubs)
                Assert.AreEqual(habitat, habitatOf(species),
                    $"the slice places {species} for the '{habitat}' habitat, but the contract calls it " +
                    $"'{habitatOf(species)}'. Re-pick the slice, or re-read the contract — do not " +
                    "hard-code the habitat.");

            CollectionAssert.AreEquivalent(ShrubCatalog.HabitatKeys,
                                           SixShrubs.Select(s => s.habitat).Distinct().ToArray(),
                "the six-species slice must cover all five of the rig's habitats and invent none");
        }

        [Test]
        public void EveryHabitatOnTheIslandGrowsItsOwnShrub_AndNoneGrowsSomewhereElse()
        {
            var habitatOf = HabitatFromContract();
            var perHabitat = new Dictionary<string, int>();

            foreach (var s in Shrubs())
            {
                string want = StPetersShrubs.HabitatAt(_terrain, s.Position);
                Assert.AreEqual(want, habitatOf(s.Species),
                    $"a {s.Species} ({habitatOf(s.Species)}) is standing on '{want}' ground at " +
                    $"{s.Position}. Sweet gale in a bog and blueberry on a barren is the whole point — " +
                    "a species on the wrong ground makes the habitat fields decorative.");
                perHabitat.TryGetValue(want, out int n);
                perHabitat[want] = n + 1;
            }

            // …and the island must actually HAVE most of these grounds, or the assert above is vacuous
            // because nothing was ever placed on them.
            Assert.GreaterOrEqual(perHabitat.Count, 4,
                "fewer than four of the five habitats occur anywhere on the island: " +
                string.Join(", ", perHabitat.Select(kv => $"{kv.Key} {kv.Value}")) +
                ". A habitat scheme the terrain cannot express is a scheme that is not doing anything.");
            Debug.Log("[stpeters-shrubs] by habitat: " +
                      string.Join(", ", perHabitat.OrderByDescending(kv => kv.Value)
                                                  .Select(kv => $"{kv.Key} {kv.Value}")));
        }

        [Test]
        public void TheShrubLayerIsPatchyHeath_NotASecondCanopy()
        {
            // A shrub layer that covered the island would be a second forest, and the interior is
            // REVERTING (§5.1) — a mosaic. Sabotage-measured the way the woods are: against a fill.
            var shrubs = Shrubs();
            Assert.Greater(shrubs.Count, 40, "a shrub layer that barely exists is not a layer");

            float minX = StPetersBuilder.IslandCenter.x - StPetersBuilder.IslandRadius;
            float minY = StPetersBuilder.IslandCenter.y - StPetersBuilder.IslandRadiusY;
            int nx = Mathf.Max(1, Mathf.CeilToInt(StPetersBuilder.IslandRadius * 2f / StPetersShrubs.ShrubStep));
            int ny = Mathf.Max(1, Mathf.CeilToInt(StPetersBuilder.IslandRadiusY * 2f / StPetersShrubs.ShrubStep));

            // How many candidate cells are plantable at all — the honest denominator. A fill would take
            // every one of them; the layer must take a fraction.
            int plantable = 0;
            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                var p = new Vector2(minX + (ix + 0.5f) * StPetersShrubs.ShrubStep,
                                    minY + (iy + 0.5f) * StPetersShrubs.ShrubStep);
                // The shrub floor, not the tree floor — the same distinction the scatter has to make.
                if (StPetersWoods.IsPlantable(_terrain, p, StPetersShrubs.ShrubLineElevation)) plantable++;
            }

            Assert.Greater(plantable, shrubs.Count,
                "the layer took every plantable cell — that is a fill, not a heath");
            float taken = shrubs.Count / (float)Mathf.Max(1, plantable);
            Assert.Less(taken, 0.8f, $"the shrub layer covers {taken:P0} of the plantable island");
            Debug.Log($"[stpeters-shrubs] {shrubs.Count} shrubs on {plantable} plantable cells " +
                      $"({taken:P0}); a fill would have been {plantable}.");
        }

        /// <summary>
        /// 🔴 <b>"SHE MOVED INTO THE WOODS" HAS TO BE TRUE OF THE BUILT WORLD, not just of the
        /// elevation.</b> Her plot clears trees within its own radius — that is what a clearing is — so
        /// every other test about it would pass just as happily if she had been dropped on open heath
        /// with no woods for a hundred metres. This is the one that looks OUTWARD: there must be real
        /// planted trees in the ring just outside her clearing, or the owner asked for a cottage in the
        /// forest and got a hut in a field.
        ///
        /// <para>Trees come from the same scatter the region plants, so this moves with the site: re-site
        /// the plot onto bare ground and this fails with the count it found.</para>
        /// </summary>
        [Test]
        public void GinnysPlotIsAClearingInTheWoods_NotAHutOnTheHeath()
        {
            const float RingMetres = 34f;   // the clearing edge (18 m) out to ~2 tree steps beyond it

            var trees = Trees();
            Assert.That(trees.Count, Is.GreaterThan(0), "nothing planted — this assert would be vacuous");

            int inRing = trees.Count(t =>
            {
                float d = Vector2.Distance(t.Position, StPetersGinnyPlot.CottagePos);
                return d >= StPetersGinnyPlot.ClearingRadius && d < RingMetres;
            });

            Assert.That(inRing, Is.GreaterThan(10),
                $"only {inRing} tree(s) stand between Ginny's clearing edge " +
                $"({StPetersGinnyPlot.ClearingRadius:0.0} m) and {RingMetres:0.0} m out. Her plot is not " +
                "in the woods — it is a clearing with nothing around it, which is a field. Re-site the " +
                "plot into planted ground, or the owner's 'into the St Peters woods' is not what shipped.");

            float nearest = trees.Min(t => Vector2.Distance(t.Position, StPetersGinnyPlot.CottagePos));
            Debug.Log($"[stpeters-ginny] {inRing} tree(s) in the ring {StPetersGinnyPlot.ClearingRadius:0.0}–" +
                      $"{RingMetres:0.0} m off her cottage; nearest tree {nearest:0.0} m away.");
        }

        [Test]
        public void NoShrubGrowsWhereThePlayerHasToBe()
        {
            // The same clearings the trees respect. A raspberry thicket across the crossing's approach
            // would be exactly as wrong as a spruce — worse, being thorny.
            foreach (var s in Shrubs())
            {
                Assert.Greater(Vector2.Distance(s.Position, StPetersBuilder.VillageHearthPos),
                               StPetersWoods.VillageClearingRadius - 0.01f,
                    $"a {s.Species} at {s.Position} is inside the village clearing");
                Assert.Greater(Vector2.Distance(s.Position, StPetersGinnyPlot.CottagePos),
                               StPetersGinnyPlot.ClearingRadius - 0.01f,
                    $"a {s.Species} at {s.Position} is inside Ginny's plot — a raspberry thicket across " +
                    "her own dooryard is exactly as wrong as one across the crossing");
                Assert.Greater(Vector2.Distance(s.Position, StPetersBuilder.StartSpawnPos),
                               StPetersWoods.SpawnClearingRadius - 0.01f,
                    $"a {s.Species} at {s.Position} is on the start spawn");
                Assert.Greater(StPetersShoreMap.DistanceToSegment(
                                   s.Position, StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo),
                               StPetersWoods.CrossingClearance - 0.01f,
                    $"a {s.Species} at {s.Position} crowds the crossing — the bar is the region's one " +
                    "lesson and you must be able to SEE it from the island");
                Assert.Greater(Vector2.Distance(s.Position, StPetersBuilder.DockZonePos),
                               StPetersWoods.DockClearance - 0.01f,
                    $"a {s.Species} at {s.Position} is on the dock");
            }
        }

        [Test]
        public void TheHeathReachesLowerThanTheWoods_BecauseThatIsWhatAHeathIs()
        {
            // The point of a shrub layer on an exposed coast: it takes the ground the forest cannot.
            // If the shrub line sat at or above the tree line the layer would just be understorey.
            Assert.Less(StPetersShrubs.ShrubLineElevation, StPetersWoods.TreeLineElevation,
                "the shrub line must sit BELOW the tree line — the heath is what grows where the " +
                "forest gives up, so a shrub layer that starts higher than the woods is not a heath");

            float lowestShrub = Shrubs().Min(s => _terrain.ElevationAt(s.Position));
            float lowestTree = Trees().Min(t => _terrain.ElevationAt(t.Position));
            Assert.Less(lowestShrub, lowestTree,
                $"the lowest shrub stands at {lowestShrub:0.00} m and the lowest tree at " +
                $"{lowestTree:0.00} m. The constants allow the heath lower; the terrain has to actually " +
                "put one there or the rule is theoretical.");
            Debug.Log($"[stpeters-shrubs] lowest shrub {lowestShrub:0.00} m vs lowest tree " +
                      $"{lowestTree:0.00} m (shrub line {StPetersShrubs.ShrubLineElevation} m, tree line " +
                      $"{StPetersWoods.TreeLineElevation} m).");
        }

        [Test]
        public void TheEdgeIsMixed_NotOneLongHedge()
        {
            // Edge is the one habitat with two species, so a margin must actually alternate. If the
            // hash always picked the same one, half the slice would never appear on the island.
            var habitatOf = HabitatFromContract();
            var onEdge = Shrubs().Where(s => habitatOf(s.Species) == "edge")
                                 .Select(s => s.Species).ToList();
            Assert.IsNotEmpty(onEdge, "no edge shrubs at all — the wood margins went unplanted");

            var distinct = onEdge.Distinct().ToArray();
            Assert.AreEqual(2, distinct.Length,
                "the edge baked rose AND raspberry, so both must reach the island: got " +
                string.Join(", ", distinct) + ". One long hedge of the same bush is the failure.");

            int rose = onEdge.Count(s => s == "WildRose");
            float share = rose / (float)onEdge.Count;
            Assert.That(share, Is.InRange(0.2f, 0.8f),
                $"the margins are {share:P0} wild rose — a 50/50 hash has drifted into a monoculture");
        }

        [Test]
        public void EveryShrubVariantAskedForExistsOnItsSheet()
        {
            // ⭐ THE VARIETY COMES FROM THE VARIANT COLUMN, NOT THE SWAY ROW, and that is load-bearing.
            // Measured on the committed slice at the rest row: the four variants differ from each other
            // by 27–54% of the cell, while the four SWAY frames differ by 0–4.8% — and on lowbush
            // blueberry, a stiff mat, the sway frames are BIT-IDENTICAL (0.0%). An earlier draft of this
            // layer took its variety from the sway row because ShrubBaker could not bake the variant
            // axis (fixed in #347); every blueberry on the barren would have been the same sprite.
            Assert.AreEqual(4, ShrubCatalog.Variants);
            foreach (var s in Shrubs(ShrubCatalog.Variants))
            {
                Assert.GreaterOrEqual(s.Variant, 0, $"{s.Species} got variant {s.Variant}");
                Assert.Less(s.Variant, ShrubCatalog.Variants,
                    $"{s.Species} got variant {s.Variant} from a {ShrubCatalog.Variants}-column sheet — " +
                    "an out-of-range column resolves to no sprite and plants an invisible shrub");
            }

            // A one-column sheet must never be asked for a second column.
            foreach (var s in Shrubs(1))
                Assert.AreEqual(0, s.Variant, "a one-variant sheet only has variant 0");

            // All four columns must actually get used, or the bake is paying for variety it never shows.
            var used = Shrubs(ShrubCatalog.Variants).Select(s => s.Variant).Distinct().OrderBy(v => v);
            CollectionAssert.AreEqual(Enumerable.Range(0, ShrubCatalog.Variants).ToArray(), used.ToArray(),
                "not every variant column reaches the island — the sheet is 4 individuals wide and the " +
                "hash has to spend all of them");
        }

        [Test]
        public void EveryShrubSpriteThePlanterAsksFor_ExistsOnTheCommittedSheet_OnTheContractsPivot()
        {
            // 🔴 THE ONE THAT WOULD OTHERWISE FAIL SILENTLY. PlantShrubs looks a sprite up BY NAME and
            // `continue`s when it misses, so a naming mismatch does not throw — it plants an empty
            // island. And the two ends of that name come from different places: the slicer writes
            // `<stem>_c<col>_f<row>` (ShrubSheetSlicer.BuildRects) and the planter composes the same
            // string from the catalog's stem plus its own column and row. Nothing but this test makes
            // the two agree, and the full builder cannot be driven headless to catch it — its
            // RegionBuildGuard refuses in batch mode, correctly, because a rebuild wipes hand-authored
            // work.
            var contract = ShrubCatalog.Load();
            Assert.IsNotNull(contract);

            int checkedSprites = 0;
            foreach (string species in StPetersShrubBake.Species)
            {
                var entry = contract.Find(species);
                Assert.IsNotNull(entry, $"{species} is not in the contract");

                string stem = ShrubCatalog.VariantSheetStem(species, StPetersShrubBake.Stage,
                                                            StPetersShrubBake.Phase);
                string path = ShrubCatalog.SheetPath(stem, ShrubCatalog.Channel.Albedo);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();

                // ⚠️ LoadAssetAtPath<Sprite> returns null on a Multiple-mode sheet — LoadAllAssetsAtPath
                // is the only thing that sees the rects.
                Assert.IsNotEmpty(sprites,
                    $"no sprites at {path}. Either the committed slice is missing or the sheet imported " +
                    "un-sliced — run Hidden Harbours ▸ Art ▸ Bake St Peters Shrub Slice.");
                Assert.AreEqual(ShrubCatalog.Variants * ShrubCatalog.SwayFrames, sprites.Length,
                    $"{stem} carries {sprites.Length} sprite(s); the variant sheet is " +
                    $"{ShrubCatalog.Variants} columns × {ShrubCatalog.SwayFrames} sway rows");

                Vector2 wantPivot = ShrubCatalog.NormalizedPivot(entry);
                for (int variant = 0; variant < ShrubCatalog.Variants; variant++)
                {
                    // Composed exactly as PlantShrubs composes it.
                    string want = $"{stem}_c{variant}_f{StPetersWoodsPlanter.RestSwayRow}";
                    var sprite = sprites.FirstOrDefault(sp => sp.name == want);
                    Assert.IsNotNull(sprite,
                        $"the planter would ask for '{want}' and the sheet does not have it. Names on " +
                        $"the sheet: {string.Join(", ", sprites.Take(4).Select(s => s.name))}… A miss " +
                        "here plants NOTHING and logs nothing.");

                    // The pivot is the ground contact at the root crown, which is why the planter sets
                    // the position and applies no offset. If it drifted, every shrub would float or sink.
                    var got = new Vector2(sprite.pivot.x / sprite.rect.width,
                                          sprite.pivot.y / sprite.rect.height);
                    Assert.AreEqual(wantPivot.x, got.x, 1e-3f, $"{want}: pivot.x vs the contract");
                    Assert.AreEqual(wantPivot.y, got.y, 1e-3f, $"{want}: pivot.y vs the contract");

                    Assert.AreEqual(entry.CellW, Mathf.RoundToInt(sprite.rect.width),
                        $"{want}: cell width vs the contract's union cell");
                    Assert.AreEqual(entry.CellH, Mathf.RoundToInt(sprite.rect.height),
                        $"{want}: cell height vs the contract's union cell");
                    checkedSprites++;
                }
            }

            Assert.AreEqual(StPetersShrubBake.Species.Length * ShrubCatalog.Variants, checkedSprites);
            Debug.Log($"[stpeters-shrubs] all {checkedSprites} sprites the planter asks for exist on the " +
                      $"committed sheets, each on its species' root-crown pivot at its union cell.");
        }

        [Test]
        public void TheShrubLayerIsDeterministic_NoRng()
        {
            var a = Shrubs();
            var b = Shrubs();
            Assert.AreEqual(a.Count, b.Count, "the heath must reproduce exactly on a rebuild (rule 5)");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Species, b[i].Species);
                Assert.AreEqual(a[i].Variant, b[i].Variant);
                Assert.AreEqual(a[i].Position, b[i].Position);
            }
        }

        [Test]
        public void AnUnbakedSpeciesLeavesItsHabitatEmpty_RatherThanThrowing()
        {
            // The kit is 20 species and St Peters bakes 6, so "declared in the contract" and "on disk"
            // are different questions and most of the kit is legitimately absent. The scatter must cope
            // with being offered less than it wants — including nothing at all — halfway through a
            // region build.
            var habitatOf = HabitatFromContract();

            Assert.IsEmpty(StPetersShrubs.Scatter(_terrain, new List<string>(), habitatOf, 4),
                "no species offered must mean no shrubs, not an exception");
            Assert.IsEmpty(StPetersShrubs.Scatter(_terrain, null, habitatOf, 4));

            // Offer ONLY the bog species: the bogs fill and every other habitat stays bare.
            var bogOnly = StPetersShrubs.Scatter(_terrain, new List<string> { "SweetGale" }, habitatOf, 4);
            Assert.IsNotEmpty(bogOnly, "St Peters has bog ground, so sweet gale alone must still plant");
            foreach (var s in bogOnly)
                Assert.AreEqual("SweetGale", s.Species);
            Assert.Less(bogOnly.Count, Shrubs().Count,
                "one species must plant FEWER shrubs than six — if not, habitat is being ignored");
        }

        // =================================================================================
        //  THE UNDERSTOREY — the floor of the island's three woodland lots
        //
        //  ⭐ Two shrub passes, exactly as there are two tree passes: the AMBIENT heath over the whole
        //  island (Shrubs() above) and the layer inside the authored lots, at a grain derived from the
        //  canopy over it. Before this pass a lot's ground could still resolve to open-meadow habitat —
        //  the owner's densest woods growing rose and raspberry under a closed canopy.
        // =================================================================================

        /// <summary>Every trunk on the island, ambient mosaic and woodland lots together — what the
        /// planter hands the understorey, and what it has to stand between.</summary>
        List<Vector2> Trunks()
        {
            var ambient = Trees();
            var trunks = ambient.Select(t => t.Position).ToList();
            trunks.AddRange(StPetersWoods.ScatterLotTrees(_terrain, Available(), _ => 4, ambient)
                                         .Select(t => t.Position));
            return trunks;
        }

        /// <summary>Everything standing when the understorey runs: the whole canopy plus the ambient heath,
        /// which walks a lot's ground on its way past because it does not know lots exist.</summary>
        List<Vector2> Standing()
        {
            var standing = Trunks();
            standing.AddRange(Shrubs().Select(s => s.Position));
            return standing;
        }

        List<StPetersShrubs.ShrubSite> Understorey(int variants = 4) =>
            StPetersShrubs.ScatterUnderstorey(_terrain, ShrubsAvailable(), HabitatFromContract(),
                                              variants, Standing());

        [Test]
        public void TheLotsHaveAFloorNow_AndItIsInsideTheLots()
        {
            var understorey = Understorey();
            Assert.IsNotEmpty(understorey,
                "the island's woodland lots grew no understorey — the owner's closed stands are still " +
                "canopy standing on a meadow floor, which is the gap this layer exists to close");

            foreach (var s in understorey)
            {
                float share = StPetersWoodlandZones.LotShareAt(s.Position, out string lot);
                Assert.Greater(share, 0f,
                    $"a {s.Species} the understorey planted at {s.Position} is outside every declared " +
                    "woodland lot — this layer is bounded to the lots and nothing else");
                Assert.IsFalse(StPetersWoodlandZones.IsCleared(s.Position),
                    $"a {s.Species} at {s.Position} stands in '{lot}'s cut lane or in a clearing — the " +
                    "owner's ruling cuts the canopy AND what grows under it, or the corridor is a path " +
                    "with a thicket down the middle of it");
            }

            Debug.Log($"[stpeters-shrubs] understorey: {understorey.Count} shrubs in " +
                      $"{StPetersWoodlandZones.LotCount} lots, one per " +
                      $"{WoodlandZones.UnderstoreyTrunksPerShrub} trunks over them.");
        }

        [Test]
        public void TheUnderstoreyGrowsWhatTheContractPutsUnderAWood()
        {
            // The whole point of the ask. `woods` is the kit's own key for the understorey habitat and
            // BeakedHazelnut is the species the CONTRACT puts in it — never a label chosen here.
            var habitatOf = HabitatFromContract();
            Assert.AreEqual(StPetersShrubs.WoodsHabitat, habitatOf("BeakedHazelnut"),
                "the contract has moved the hazel out of the woods habitat; re-read it rather than " +
                "hard-coding a species here");
            CollectionAssert.Contains(ShrubCatalog.HabitatKeys, StPetersShrubs.WoodsHabitat,
                "StPetersShrubs.WoodsHabitat is not one of the rig's own habitat keys, so nothing baked " +
                "will ever match it and every lot goes back to a bare floor with nothing failing");

            var understorey = Understorey();
            var perHabitat = new Dictionary<string, int>();

            foreach (var s in understorey)
            {
                string ground = StPetersShrubs.HabitatAt(_terrain, s.Position);
                Assert.AreEqual(ground, habitatOf(s.Species),
                    $"a {s.Species} ({habitatOf(s.Species)}) is standing on '{ground}' ground at " +
                    $"{s.Position}");

                // 🔴 THE MEADOW FLOOR, SAID AS AN ASSERTION. Under a canopy the only honest answers are
                // the understorey and — where the ground is wet — a bog or a swale, both of which really
                // do occur under trees. `edge` (rose and raspberry) or `barren` (blueberry heath) would
                // mean a lot's floor had gone back to being open meadow, which is the exact state this
                // pass exists to end. It cannot happen while HabitatAt asks InWoods; this is what notices
                // if it stops.
                Assert.That(ground, Is.EqualTo(StPetersShrubs.WoodsHabitat)
                                      .Or.EqualTo("bog").Or.EqualTo("swale"),
                    $"a {s.Species} in the understorey stands on '{ground}' ground at {s.Position} — " +
                    "inside a closed stand, which is open-meadow habitat under a canopy");

                perHabitat.TryGetValue(ground, out int n);
                perHabitat[ground] = n + 1;
            }

            perHabitat.TryGetValue(StPetersShrubs.WoodsHabitat, out int woods);
            Assert.Greater(woods, 0,
                "not one understorey shrub asked for the woods habitat — the layer is planting inside " +
                "the lots but the habitat field is still calling every one of their floors something " +
                "else, so the hazel the slice was baked for never reaches the island");

            Debug.Log("[stpeters-shrubs] understorey by habitat: " +
                      string.Join(", ", perHabitat.OrderByDescending(kv => kv.Value)
                                                  .Select(kv => $"{kv.Key} {kv.Value}")));
        }

        [Test]
        public void TheWoodsHabitatIsNeverAskedForOutOnTheOpenMeadow()
        {
            // ⭐ THE ISLAND'S HALF OF THE BOUND Nine Mile Creek's TheWoodsHabitatIsAskedOnlyInsideAStand
            // states. Here the answer is INWOODS rather than "in a lot" — the island genuinely has an
            // ambient mosaic of stands and hazel belongs under those too — so the claim is: woods habitat
            // means a canopy overhead, and never open ground.
            var habitatOf = HabitatFromContract();
            foreach (var s in Shrubs().Concat(Understorey()))
            {
                if (habitatOf(s.Species) != StPetersShrubs.WoodsHabitat) continue;
                Assert.IsTrue(StPetersWoods.InWoods(s.Position, _terrain.ElevationAt(s.Position)),
                    $"a {s.Species} is growing the woods understorey at {s.Position}, where there is no " +
                    "canopy at all — neither the reverting mosaic nor an authored lot. The kit's woods " +
                    "habitat belongs INSIDE a stand");
            }
        }

        [Test]
        public void TheUnderstoreyIsSparserThanTheCanopyOverIt()
        {
            // "Depth, not a second canopy" (owner, on the density of the closed stands) as a measurement.
            //
            // ⚠ THE RATIO IS ASSERTED ON THE POOLED SAMPLE and only the weaker claim per lot, on purpose.
            // The dial is one shrub per UnderstoreyTrunksPerShrub trunks, but an individual 20 m lot
            // carries a couple of dozen shrubs against a grid of ~30 candidate cells — small enough that a
            // per-lot 2:1 bar would be measuring where the lane happened to fall as much as the density
            // law. Pooled, the sample is the island's whole understorey and the claim is the real one.
            var trunks = Trunks();
            var understorey = Understorey();
            int overAll = 0, underAll = 0;

            foreach (var lot in StPetersWoodlandZones.Lots)
            {
                int over = trunks.Count(p => lot.Contains(p));
                int under = understorey.Count(s => lot.Contains(s.Position));
                overAll += over; underAll += under;

                Assert.Greater(under, 0,
                    $"'{lot.Name}' grew no understorey at all — one of the island's closed stands is still " +
                    "canopy on a meadow floor");
                Assert.Less(under, over,
                    $"'{lot.Name}' carries {under} understorey shrubs under {over} trunks — a floor at " +
                    "least as dense as the canopy over it is a thicket, and you cannot see into a thicket");
            }

            Assert.Greater(underAll, 10,
                $"the island's three lots grew {underAll} understorey shrubs between them, which is not a " +
                "layer — every other assertion here would pass vacuously on it");
            Assert.Less(underAll * 2, overAll,
                $"the lots carry {underAll} understorey shrubs under {overAll} trunks " +
                $"(1 : {overAll / (float)Mathf.Max(1, underAll):0.0}). The dial asks for one shrub per " +
                $"{WoodlandZones.UnderstoreyTrunksPerShrub} trunks; better than one in two stops reading " +
                "as depth under a canopy and starts reading as brush you cannot see through");

            Debug.Log($"[stpeters-shrubs] understorey vs canopy: {underAll} shrubs under {overAll} trunks " +
                      $"(1 : {overAll / (float)Mathf.Max(1, underAll):0.0}).");
        }

        [Test]
        public void TheUnderstoreyStandsBetweenTheTrunksRatherThanOnThem()
        {
            // Trees pivot at the trunk FOOT and shrubs at the root crown, so a shrub rolled onto a
            // trunk's position is drawn growing out of the tree. ⚠ Nearest pair found first and asserted
            // once — this is tens of thousands of pairs and NUnit constraints are not free.
            var standing = Standing();
            var understorey = Understorey();

            float worst = float.MaxValue;
            Vector2 worstShrub = Vector2.zero, worstNeighbour = Vector2.zero;
            foreach (var s in understorey)
            foreach (var p in standing)
            {
                float d = Vector2.Distance(s.Position, p);
                if (d >= worst) continue;
                worst = d; worstShrub = s.Position; worstNeighbour = p;
            }

            Assert.GreaterOrEqual(worst, WoodlandZones.MinUnderstoreyGapMetres - 1e-3f,
                $"the closest an understorey shrub gets to something already planted is {worst:0.00} m — " +
                $"a shrub at {worstShrub} against a trunk or heath bush at {worstNeighbour}, inside the " +
                $"{WoodlandZones.MinUnderstoreyGapMetres:0.00} m that keeps a shrub beside a trunk rather " +
                "than inside one");
        }

        [Test]
        public void GinnysDooryardStaysClearOfTheUnderstoreyToo()
        {
            // 🔴 Her plot is a CLEARING IN THE WOODS and NorthWood stands just east of it. A layer that
            // planted a hazel thicket across the dooryard she walks out of twice a day would be exactly
            // the bug NoShrubGrowsWhereThePlayerHasToBe catches for the heath — asserted separately
            // because it is a separate scatter and would not be covered by that one.
            foreach (var s in Understorey())
            {
                Assert.Greater(Vector2.Distance(s.Position, StPetersGinnyPlot.CottagePos),
                               StPetersGinnyPlot.ClearingRadius - 0.01f,
                    $"an understorey {s.Species} at {s.Position} is inside Ginny's plot");
                Assert.Greater(Vector2.Distance(s.Position, StPetersBuilder.VillageHearthPos),
                               StPetersWoods.VillageClearingRadius - 0.01f,
                    $"an understorey {s.Species} at {s.Position} is inside the village clearing");
                Assert.IsFalse(StPetersWoods.OnPaintedPath(s.Position),
                    $"an understorey {s.Species} at {s.Position} stands on a painted dirt path");
            }
        }

        [Test]
        public void TheUnderstoreyIsDeterministic_NoRng()
        {
            var a = Understorey();
            var b = Understorey();
            Assert.AreEqual(a.Count, b.Count,
                "the understorey must reproduce exactly on a rebuild (rule 5)");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Species, b[i].Species);
                Assert.AreEqual(a[i].Variant, b[i].Variant);
                Assert.AreEqual(a[i].Position, b[i].Position);
            }

            // A one-column sheet must never be asked for a second column — the heath's own rule.
            foreach (var s in Understorey(1))
                Assert.AreEqual(0, s.Variant, "a one-variant sheet only has variant 0");
        }
    }
}
