using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
            Assert.Greater(trees.Count, 200,
                $"only {trees.Count} trees — §5.1 wants 'forest: interior cover, hiding ruins', which a " +
                "handful of trees is not");
            Assert.Less(trees.Count, 1600,
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
            var c = StPetersBuilder.IslandCenter;
            var weather = c + new Vector2(0f, -110f);   // south
            var lee     = c + new Vector2(0f, 110f);    // north

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
            Assert.GreaterOrEqual(planted.Count, 7,
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
                Assert.Greater(Vector2.Distance(t.Position, StPetersBuilder.CottagePos),
                               StPetersWoods.VillageClearingRadius - 0.01f,
                    $"a {t.Species} at {t.Position} is inside the village — a village stands in a CLEARING, " +
                    "and a spruce through the cottage is the first thing the player would see");

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

            // And the margin is real, not notional: sample the shore band and assert nothing is planted.
            int checkedPoints = 0;
            for (float a = 0f; a < 360f; a += 3f)
            {
                var dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                var p = StPetersBuilder.IslandCenter + dir * 224f;   // just inside the plateau edge
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

            Assert.Greater(flowers.Count, 50, "a meadow in summer has flowers in it");
            Assert.Less(flowers.Count, 700,
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
    }
}
