using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE VILLAGE AND THE ONE DOCK.</b> Two things that were quietly wrong after #328 scaled the island
    /// and one that was never built:
    /// <list type="bullet">
    /// <item>the whole village dressing — cottage, Aunt Ginny, Ned's letter, the freezer, the wet bucket —
    /// still sat within a few metres of <c>(-40, 0)</c>, the centre of the 44 m greybox disc the island used
    /// to BE, so it was adrift on a 450 × 260 m landmass;</item>
    /// <item>the wet-bucket spot, whose own comment reads "the sand rim, at the water", was ~100 m inland
    /// on grass;</item>
    /// <item>the "dock" was a 2.5 × 1.2 m brown rectangle, on a shore the island no longer has.</item>
    /// </list>
    /// These assert the fixed placements against the AUTHORED TERRAIN rather than against the numbers —
    /// a position test that re-states the constant it is checking proves nothing.
    /// </summary>
    public class StPetersVillageTests
    {
        private GameObject _go;
        private TidalTerrain _terrain;

        const float SpringHighWater = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;   //  3.5
        const float SpringLowWater  = StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;   // -3.5

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("StPetersTerrain_VillageTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        static Vector2[] VillageBuildings => new[]
        {
            (Vector2)StPetersBuilder.CottagePos,
            (Vector2)StPetersBuilder.GinnyPos,
            (Vector2)StPetersBuilder.NedsLetterPos,
            (Vector2)StPetersBuilder.FreezerPos,
        };

        // =================================================================================
        //  THE VILLAGE STANDS ON THE ISLAND
        // =================================================================================

        [Test]
        public void EveryVillagePiece_StandsOnGroundThatIsDryAtEveryTide()
        {
            foreach (var p in VillageBuildings)
            {
                float e = _terrain.ElevationAt(p);
                Assert.Greater(e, SpringHighWater,
                    $"{p} is at {e:0.00} m — the cottage, the aunt, the letter and the freezer are the " +
                    "HOME, and home does not flood. They must sit above the highest water of a spring tide.");

                float d = TidalTerrain.IslandDistance(p, StPetersBuilder.IslandCenter,
                                                     StPetersBuilder.IslandRadius,
                                                     StPetersBuilder.IslandRadiusY);
                Assert.LessOrEqual(d, StPetersBuilder.IslandRadius,
                    $"{p} is past the plateau edge — a house belongs on the island, not on its beach");
            }
        }

        [Test]
        public void TheVillageIsCloseAndItIsBesideTheStartSpawn()
        {
            // §6.0's mood is the point: "Quiet, close, the whole world the size of a low-tide walk." A
            // village whose pieces are a hundred metres apart is a hamlet strung along a road, not this.
            for (int i = 0; i < VillageBuildings.Length; i++)
            for (int j = i + 1; j < VillageBuildings.Length; j++)
                Assert.Less(Vector2.Distance(VillageBuildings[i], VillageBuildings[j]), 40f,
                    "the village reads as ONE small place — nothing in it is more than 40 m from anything else");

            // And you start in it, not a walk from it: the opening's first beat is talking to the aunt.
            Vector2 spawn = StPetersBuilder.StartSpawnPos;
            foreach (var p in VillageBuildings)
                Assert.Less(Vector2.Distance(spawn, p), 30f,
                    $"{p} is more than 30 m from the start spawn — the player should wake up IN the village");
        }

        [Test]
        public void Sabotage_TheOldGreyboxVillageSite_IsNowhereNearTheStartSpawn()
        {
            // What the builder used to place: everything within a few metres of (-40, 0), the centre of the
            // pre-#328 greybox disc. Measure how far that is from where the player actually spawns, so the
            // fix is shown landing on a real dislocation rather than on a tidy-up.
            var oldSite = new Vector2(-40f, 0f);
            float drift = Vector2.Distance(oldSite, StPetersBuilder.StartSpawnPos);
            Assert.Greater(drift, 55f,
                $"the old village site sat {drift:0} m from the start spawn — far enough that the player " +
                "woke up alone on an empty island with the cottage out of sight");

            // It is still ON the island (the island is 450 m long), which is exactly why nothing caught it:
            // no test failed, no warning fired, the objects were simply in the wrong place.
            float d = TidalTerrain.IslandDistance(oldSite, StPetersBuilder.IslandCenter,
                                                  StPetersBuilder.IslandRadius, StPetersBuilder.IslandRadiusY);
            Assert.Less(d, StPetersBuilder.IslandRadius,
                "and it was still on dry land — which is why this drifted silently for a whole rescale");
        }

        // =================================================================================
        //  THE WET BUCKET IS AT THE WATER
        // =================================================================================

        [Test]
        public void TheWetBucketSitsAtTheHeadOfTheFlats_DryButOnlyJust()
        {
            Vector2 bucket = StPetersBuilder.WetBucketPos;
            float e = _terrain.ElevationAt(bucket);

            Assert.Greater(e, SpringHighWater,
                $"the barrel is at {e:0.00} m — it holds live shellfish in seawater, so it must never be " +
                "swimming, not even at the top of a spring tide");
            Assert.Less(e, SpringHighWater + 2f,
                $"the barrel is at {e:0.00} m — more than 2 m clear of high water means it is up the beach " +
                "and no longer 'at the water', which is the whole point of where it stands (its own " +
                "comment used to say so while sitting 100 m inland)");

            // It is on the BAR's footprint — the head of the flats you dig on — so a bucket of clams
            // travels a few metres, not a hundred.
            float dBar = StPetersShoreMap.DistanceToSegment(
                bucket, StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo);
            Assert.LessOrEqual(dBar, StPetersBuilder.SandbarHalfWidth,
                "the wet bucket stands on the bar's own footprint — where the digging is");

            // And a few metres west the ground genuinely floods, which is what makes this "the head".
            float justWest = _terrain.ElevationAt(bucket + new Vector2(-20f, 0f));
            Assert.Less(justWest, SpringHighWater,
                $"20 m west the ground is {justWest:0.00} m and still above high water — the barrel is not " +
                "at the head of anything");
        }

        [Test]
        public void Sabotage_TheOldWetBucketSpot_WasOnGroundTheSeaNeverReaches()
        {
            // The old position, verbatim from the builder, with the comment "the sand rim, at the water".
            var old = new Vector2(-30f, -6f);
            float e = _terrain.ElevationAt(old);
            Assert.AreEqual(StPetersBuilder.IslandElevation, e, 0.01f,
                $"the old spot was at {e:0.00} m — the island's full plateau height, i.e. the middle of a " +
                "meadow. Its own comment claimed 'the sand rim, at the water'.");

            // Measure the lie: how far the nearest water actually was.
            float nearest = float.MaxValue;
            for (float a = 0f; a < 360f; a += 2f)
            for (float r = 1f; r < 200f; r += 1f)
            {
                var p = old + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * r;
                if (_terrain.ElevationAt(p) < SpringHighWater) { nearest = Mathf.Min(nearest, r); break; }
            }
            Assert.Greater(nearest, 50f,
                $"the nearest water to the old wet-bucket spot was {nearest:0} m away — that is the size of " +
                "the drift, and it is why this needed measuring rather than eyeballing");
        }

        // =================================================================================
        //  THE ONE DOCK
        // =================================================================================

        [Test]
        public void ThePierRootsOnDryGround_AndItsHeadReachesTheMooring()
        {
            // A pier has to start somewhere you can walk onto and end where the boat is. The berth's carve
            // reaches past its own shoreward end, so "the last dry ground" is a measured thing, not a guess.
            var root = new Vector2(StPetersWharf.RootCellX + 0.5f, 0f);
            Assert.Greater(_terrain.ElevationAt(root), SpringHighWater,
                $"the pier root at {root} is at {_terrain.ElevationAt(root):0.00} m — it must be on ground " +
                "that is dry at every tide, or the shore end of the dock is in the sea");

            // The head is over the dredged slip, not over the beach — a pier that stops short of the water
            // is a boardwalk.
            var head = new Vector2(StPetersWharf.HeadCellX + 0.5f, 0f);
            Assert.Less(_terrain.ElevationAt(head), 0f,
                $"the pier head at {head} is at {_terrain.ElevationAt(head):0.00} m — it must stand over the " +
                "slip, or there is nothing to moor against");

            // The ratified disembark point lands ON the planks: that is the reason the pier is this long.
            Vector2 disembark = StPetersBuilder.DisembarkPos;
            Assert.GreaterOrEqual(disembark.x, StPetersWharf.RootCellX,
                "the ratified disembark must fall inside the deck's footprint");
            Assert.LessOrEqual(disembark.x, StPetersWharf.HeadCellX + 1,
                "the ratified disembark must fall inside the deck's footprint");
            Assert.GreaterOrEqual(disembark.y, StPetersWharf.MinCellY,
                "the ratified disembark must fall inside the deck's width");
            Assert.LessOrEqual(disembark.y, StPetersWharf.MaxCellY + 1,
                "the ratified disembark must fall inside the deck's width");

            // And the mooring is just off the head, not under it.
            Assert.Greater(StPetersBuilder.DoryMooredPos.x, StPetersWharf.HeadCellX,
                "the boat lies off the pier head — a mooring under the planks is a mooring you cannot see");
        }

        [Test]
        public void ThePierStaysInsideTheDredgedSlip_SoItNeverBlocksTheOneDoor()
        {
            // The berth is the ONE way in through the reef ring (§5.1a). A pier wider than the slip would
            // wall it off; a pier that wandered outside it would stand on the reef.
            foreach (var cell in StPetersWharf.DeckCellsBackToFront())
            {
                var p = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                float dBerth = StPetersShoreMap.DistanceToSegment(
                    p, StPetersBuilder.BerthFrom, StPetersBuilder.BerthTo);
                Assert.LessOrEqual(dBerth, StPetersBuilder.BerthHalfWidth,
                    $"deck cell {cell} sits {dBerth:0.0} m from the slip's centre-line, outside its " +
                    $"{StPetersBuilder.BerthHalfWidth} m half-width — the pier has wandered onto the reef");
            }

            // Leaving room either side to actually come alongside.
            Assert.Less(StPetersWharf.WidthCells, StPetersBuilder.BerthHalfWidth * 2f,
                "the deck must be narrower than the slip, or there is no water left to bring a boat into");
        }

        [Test]
        public void TheDeckIsDrawnBackToFront_BecauseAWharfCellOverhangsTheCellBelowIt()
        {
            // The kit's whole contract: a cell is 32 x 56 px, the top 32 are the deck and the bottom 24 are
            // the vertical face, which hangs DOWN over the cell to the south. Get the order wrong and every
            // face overdraws its southern neighbour's planks.
            Assert.AreEqual(WharfKitCatalog.DeckCellHeight,
                            WharfKitCatalog.DeckHeight + WharfKitCatalog.FaceHeight,
                "sanity: the kit's cell really is deck + face, so the overhang is real");
            Assert.Greater(WharfKitCatalog.FaceHeight, 0,
                "if the face ever became zero-height the back-to-front rule would be pointless — and this " +
                "test would be the thing that noticed");

            // North draws first (lowest order), south draws last (highest).
            int north = StPetersWharf.SortingOrderFor(StPetersWharf.MaxCellY);
            int south = StPetersWharf.SortingOrderFor(StPetersWharf.MinCellY);
            Assert.Less(north, south,
                "the northern row must draw BEHIND the southern one, or the overhang covers the deck");

            // Strictly monotonic per row, so no two rows can ever tie and flicker.
            for (int y = StPetersWharf.MinCellY; y < StPetersWharf.MaxCellY; y++)
                Assert.Greater(StPetersWharf.SortingOrderFor(y), StPetersWharf.SortingOrderFor(y + 1),
                    $"rows {y} and {y + 1} must have distinct orders — the band has to be at least as wide " +
                    "as the pier");

            // And the iteration order the placer follows is itself back to front.
            var rows = StPetersWharf.DeckCellsBackToFront().Select(c => c.y).ToArray();
            for (int i = 1; i < rows.Length; i++)
                Assert.LessOrEqual(rows[i], rows[i - 1],
                    "DeckCellsBackToFront must never step NORTH — the order is the contract");
        }

        [Test]
        public void TheDeckSortsAboveTheWaterAndBelowThePlayer()
        {
            // The pier stands over water, so it must draw above the Sea plane (-5); the player walks on it,
            // so it must draw below the on-foot band (YSortSprite's floor is 2). Six rows into six orders is
            // exactly why the pier is six metres wide.
            Assert.Greater(StPetersWharf.SortingOrderMin, -5,
                "the deck must sort ABOVE the Sea plane at -5, or the water draws over the planks");
            Assert.Less(StPetersWharf.SortingOrderMax, 2,
                "the deck must sort BELOW the on-foot character band, or a fisher standing on the pier " +
                "disappears behind it");
            Assert.GreaterOrEqual(StPetersWharf.SortingOrderMax - StPetersWharf.SortingOrderMin + 1,
                                  StPetersWharf.WidthCells,
                "the sorting band must hold one distinct order per deck row");
        }

        [Test]
        public void EveryDeckCellAsksForAVariantTheKitActuallyBakes_AndNeverACapOrADiagonal()
        {
            var known = new System.Collections.Generic.HashSet<string>(WharfKitCatalog.DeckVariants);
            int centre = 0, edges = 0, corners = 0;

            foreach (var cell in StPetersWharf.DeckCellsBackToFront())
            {
                string v = StPetersWharf.DeckVariantAt(cell.x, cell.y);
                Assert.IsTrue(known.Contains(v), $"cell {cell} asked for '{v}', which the kit does not bake");
                Assert.IsFalse(v.StartsWith("cap"),
                    $"cell {cell} asked for an END CAP — a rectangle six cells wide never has three sides " +
                    "open at once, so a cap here means the pier stopped being a rectangle");
                Assert.IsFalse(v.StartsWith("di"),
                    $"cell {cell} asked for a 45 DEGREE cut — the pier has no diagonals");

                if (v == "ctr") centre++;
                else if (v.StartsWith("ed")) edges++;
                else corners++;

                // And the material/variant pair must resolve — AtlasIndex throws on a bad one rather than
                // handing back an index that paints the wrong thing.
                Assert.DoesNotThrow(() => WharfKitCatalog.AtlasIndex(StPetersWharf.DeckMaterial, v));
            }

            Assert.AreEqual(4, corners, "a rectangle has exactly four outer corners");
            Assert.Greater(centre, 0, "and an interior");
            Assert.Greater(edges, 0, "and edges");
            Assert.AreEqual(StPetersWharf.LengthCells * StPetersWharf.WidthCells,
                            centre + edges + corners,
                "every cell of the footprint is accounted for exactly once");
        }

        [Test]
        public void TheDeckMaterialIsFixedTimber_NotAFloatThatWouldSitOnTheMud()
        {
            Assert.Contains(StPetersWharf.DeckMaterial, WharfKitCatalog.DeckMaterials,
                "the deck material must be one the kit bakes");
            Assert.AreNotEqual("float", StPetersWharf.DeckMaterial,
                "the slip DRIES near spring low (§5.1a: the berth bed is -1.0 m against a -3.5 m low), so a " +
                "floating dock would spend part of every tide aground — and it would need a driver for its " +
                "four bob frames that nothing here provides");

            // A fixed deck has no bob frames, and asking for one is a caller bug the kit refuses.
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => WharfKitCatalog.AtlasRow(StPetersWharf.DeckMaterial, 1),
                "the kit throws rather than silently ignoring a bob frame on a fixed deck — worth pinning, " +
                "because a silent ignore is how you ship a quay that thinks it is animating");
        }

        [Test]
        public void EveryFittingIsAKitFitting_AndTheyAllSitOnTheDeckYouCanSee()
        {
            var fittings = StPetersWharf.Fittings();
            Assert.IsNotEmpty(fittings, "a working wharf has gear on it");

            foreach (var f in fittings)
            {
                Assert.Contains(f.Name, WharfKitCatalog.Fittings,
                    $"'{f.Name}' is not one of the kit's 14 fittings");

                // On or immediately at the deck's footprint — a bollard floating beside the pier is worse
                // than no bollard.
                Assert.GreaterOrEqual(f.Position.x, StPetersWharf.RootCellX - 0.5f, $"{f.Name} is west of the pier");
                Assert.LessOrEqual(f.Position.x, StPetersWharf.HeadCellX + 1.5f, $"{f.Name} is east of the pier");
                Assert.GreaterOrEqual(f.Position.y, StPetersWharf.MinCellY - 0.5f, $"{f.Name} is south of the pier");
                Assert.LessOrEqual(f.Position.y, StPetersWharf.MaxCellY + 1f, $"{f.Name} is north of the pier");
            }

            // Nothing commercial: this is a modest island wharf, not a freight berth.
            Assert.IsFalse(fittings.Any(f => f.Name == "dolphin"),
                "a mooring dolphin is a commercial berth's fitting — §5.1a says 'modest'");
            Assert.IsFalse(fittings.Any(f => f.Name == "gangway"),
                "a gangway belongs to a FLOAT, and this deck is fixed timber");

            // The two fittings the brief actually requires: something to tie to, and a way aboard.
            Assert.IsTrue(fittings.Any(f => f.Name == "bollard"), "you have to be able to tie up");
            Assert.IsTrue(fittings.Any(f => f.Name == "ladder"),
                "'modest, but can take powerboats' needs a way down to a hull at low water");
        }

        [Test]
        public void TheFittingsAreDeterministic()
        {
            var a = StPetersWharf.Fittings();
            var b = StPetersWharf.Fittings();
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Name, b[i].Name);
                Assert.AreEqual(a[i].Position, b[i].Position);
            }
        }

        // =================================================================================
        //  THE FIVE BUILDINGS — §5.1's three houses, the school and the general store
        //
        //  These are AUTHORED sites, so a test that re-stated the constants would prove nothing.
        //  Every clearance below is re-derived from the BUILDING CONTRACT'S OWN FOOTPRINTS, so a
        //  re-bake that changes a footprint, or a site nudged by hand, fails here with the number
        //  to use — and the reasons the arc has the shape it does are each pinned separately.
        // =================================================================================

        static IReadOnlyList<StPetersVillage.Site> Sites => StPetersVillage.Sites;

        /// <summary>The footprint radius of a site's building, from the contract. Fails the test rather
        /// than returning 0 for an unbaked one — an unbaked village is a missing village.</summary>
        static float Radius(StPetersVillage.Site site)
        {
            var p = VillageBuildingCatalog.Find(site.Key);
            Assert.IsTrue(p.IsValid,
                $"'{site.Key}' is not in the building contract. The five M1 builds ship baked (#352) — " +
                "if this fired, the contract is stale or the key was camel-cased from a label.");
            return StPetersVillage.FootprintRadiusMetres(p);
        }

        [Test]
        public void TheVillageIsTheFiveBuildingsTheDocsAskFor_AndTheyAreAllBaked()
        {
            // §5.1: "three clapboard houses, a one-room school, and a general store."
            Assert.AreEqual(5, Sites.Count, "five buildings: three houses, a school, a store");
            CollectionAssert.AreEquivalent(
                new[] { "school", "generalStore", "whiteFarmhouse", "redSaltbox", "sageCottage" },
                Sites.Select(s => s.Key).ToArray(),
                "the village's buildings drifted from the kit's M1 set");

            foreach (var site in Sites)
            {
                var p = VillageBuildingCatalog.Find(site.Key);
                Assert.IsTrue(p.IsValid, $"{site.Key} is not baked");
                Assert.IsFalse(string.IsNullOrWhiteSpace(site.Reason),
                    $"{site.Key} has no authoring reason. These sites are hand-placed identity, not a " +
                    "scatter — if there is no reason for one, it should have been hashed.");
            }
            Debug.Log("[stpeters-village] " +
                      VillageBuildingCatalog.Summary(VillageBuildingCatalog.Scan()));
        }

        [Test]
        public void EveryBuildingStandsOnGroundThatIsDryAtEveryTide()
        {
            foreach (var site in Sites)
            {
                float e = _terrain.ElevationAt(site.Position);
                Assert.Greater(e, SpringHighWater,
                    $"{site.Key} at {site.Position} sits at {e:0.00} m — at or below spring high water. " +
                    "A house does not flood.");

                float d = TidalTerrain.IslandDistance(site.Position, StPetersBuilder.IslandCenter,
                                                     StPetersBuilder.IslandRadius,
                                                     StPetersBuilder.IslandRadiusY);
                Assert.LessOrEqual(d, StPetersBuilder.IslandRadius,
                    $"{site.Key} is past the plateau edge — a house belongs on the island, not its beach");
            }
        }

        [Test]
        public void NoTwoBuildingsOverlap_AndThereIsALaneBetweenThem()
        {
            // Footprints as CIRCLES (the half-diagonal), because the facing is derived and a quarter-turned
            // building presents its diagonal — see StPetersVillage's remarks.
            for (int i = 0; i < Sites.Count; i++)
            for (int j = i + 1; j < Sites.Count; j++)
            {
                float gap = Vector2.Distance(Sites[i].Position, Sites[j].Position)
                            - Radius(Sites[i]) - Radius(Sites[j]);
                Assert.GreaterOrEqual(gap, StPetersVillage.LaneGap,
                    $"{Sites[i].Key} and {Sites[j].Key} are {gap:0.00} m apart at their footprint edges — " +
                    $"under the {StPetersVillage.LaneGap} m lane. Two buildings closer than that read as " +
                    "one building with a seam.");
            }
        }

        [Test]
        public void NoBuildingCrowdsTheHearth_TheGreen_OrTheProps()
        {
            Vector2 cottage = StPetersBuilder.CottagePos;
            Vector2 spawn = StPetersBuilder.StartSpawnPos;
            var props = new[]
            {
                ("Ginny", (Vector2)StPetersBuilder.GinnyPos),
                ("Ned's letter", (Vector2)StPetersBuilder.NedsLetterPos),
                ("the freezer", (Vector2)StPetersBuilder.FreezerPos),
            };

            foreach (var site in Sites)
            {
                float r = Radius(site);

                Assert.GreaterOrEqual(Vector2.Distance(site.Position, cottage),
                                      r + StPetersVillage.CottageFootprintRadius,
                    $"{site.Key} is inside Ginny's cottage. The hearth is the middle of the village and " +
                    "nothing may crowd it.");

                // §6.0: "you wake up IN the village" — the spawn is the green, and it stays open ground.
                Assert.GreaterOrEqual(Vector2.Distance(site.Position, spawn),
                                      r + StPetersWoods.SpawnClearingRadius,
                    $"{site.Key} is inside the start spawn's clearing. The first thing the player sees " +
                    "should be the village, not a wall.");

                foreach (var (name, p) in props)
                    Assert.GreaterOrEqual(Vector2.Distance(site.Position, p),
                                          r + StPetersVillage.PropClearance,
                        $"{site.Key} has {name} inside its footprint");
            }
        }

        [Test]
        public void NoBuildingBlocksTheViewOfTheBar_BecauseTheCrossingIsTheRegionsOneLesson()
        {
            // The same clearance the trees keep, and it matters MORE for a building: §6.0's single teeth-
            // of-tide lesson is the bar, and you have to be able to SEE it from the island. This is the
            // constraint that decides the village's whole shape — it rules out the entire west side, which
            // is why the arc opens south-east instead of ringing the green evenly.
            foreach (var site in Sites)
            {
                float d = StPetersShoreMap.DistanceToSegment(
                    site.Position, StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo);
                Assert.GreaterOrEqual(d, Radius(site) + StPetersWoods.CrossingClearance,
                    $"{site.Key} stands {d:0.0} m from the bar's centre-line, inside the " +
                    $"{StPetersWoods.CrossingClearance} m the crossing keeps clear. A house across the " +
                    "approach hides the one thing this island has to teach.");
            }

            // …and the sabotage: prove the constraint BITES rather than being satisfied by accident. A
            // house on the natural west side of the green would fail it.
            var westOfTheGreen = new Vector2(-125f, 8f);
            float wd = StPetersShoreMap.DistanceToSegment(
                westOfTheGreen, StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo);
            Assert.Less(wd, StPetersWoods.CrossingClearance,
                "a site west of the green was supposed to be inside the bar's clearance — if it is not, " +
                "this test is not actually constraining the layout and the arc's shape is unexplained.");
        }

        [Test]
        public void TheClearingContainsEveryFootprint_SoNothingIsPlantedThroughAWall()
        {
            // 🔴 THE ONE THAT MADE THIS PR EDIT StPetersWoods. Trees, shrubs and flowers all skip the
            // village clearing, so containment is what keeps vegetation out of the buildings — and at the
            // 34 m the clearing was authored at (#345, when the village WAS the cottage and three props)
            // the outermost house stood 7 m outside it. Derived from the contract, so a re-bake with bigger
            // footprints fails here with the number to use.
            float need = StPetersVillage.RequiredClearingRadius();
            Assert.Greater(need, 0f, "nothing baked — this assert would be vacuous");
            Assert.LessOrEqual(need, StPetersWoods.VillageClearingRadius,
                $"the village's furthest footprint reaches {need:0.00} m from the cottage but the clearing " +
                $"is {StPetersWoods.VillageClearingRadius:0.00} m. Widen " +
                "StPetersWoods.VillageClearingRadius to at least that, or bring the site in — as it " +
                "stands, trees and shrubs plant through that building's walls.");

            // Per building, so the failure names the one that is out rather than just the worst.
            foreach (var site in Sites)
                Assert.LessOrEqual(
                    Vector2.Distance(site.Position, StPetersBuilder.CottagePos) + Radius(site),
                    StPetersWoods.VillageClearingRadius,
                    $"{site.Key}'s footprint reaches outside the village clearing");

            Debug.Log($"[stpeters-village] furthest footprint reaches {need:0.00} m from the cottage; " +
                      $"clearing is {StPetersWoods.VillageClearingRadius:0.0} m " +
                      $"({StPetersWoods.VillageClearingRadius - need:0.00} m of headroom).");
        }

        [Test]
        public void NothingIsPlantedInsideABuilding()
        {
            // The empirical other half of the containment argument above: plant the real layers and check
            // no tree, shrub or flower actually lands in a wall. Containment says it CANNOT happen; this
            // says it DIDN'T. Both, because the structural argument is only as good as its premise that
            // every planter routes through IsPlantable.
            var species = new List<string>
            {
                "RedSpruce", "BlackSpruce", "BalsamFir", "WhitePine", "WhiteCedar",
                "WhiteBirch", "RedMaple", "RedOak", "TremblingAspen",
            };
            var contract = ShrubCatalog.Load();
            Assert.IsNotNull(contract);
            var habitatOf = contract.Species.ToDictionary(e => e.Key, e => e.Habitat);

            var planted = new List<(string what, Vector2 at)>();
            planted.AddRange(StPetersWoods.ScatterTrees(_terrain, species, _ => 4)
                                          .Select(t => (t.Species, t.Position)));
            planted.AddRange(StPetersWoods.ScatterFlowers(
                                 _terrain, new List<string> { "Buttercup", "LupinBlue", "BlueFlag" })
                                          .Select(f => (f.Species, f.Position)));
            planted.AddRange(StPetersShrubs.Scatter(
                                 _terrain,
                                 new List<string> { "LowbushBlueberry", "SweetGale", "Meadowsweet",
                                                    "BeakedHazelnut", "WildRose", "Raspberry" },
                                 s => habitatOf.TryGetValue(s, out string h) ? h : null,
                                 ShrubCatalog.Variants)
                                          .Select(s => (s.Species, s.Position)));

            Assert.IsNotEmpty(planted, "nothing was planted at all — this check would be vacuous");

            foreach (var site in Sites)
            {
                float r = Radius(site);
                foreach (var (what, at) in planted)
                    Assert.Greater(Vector2.Distance(at, site.Position), r,
                        $"a {what} at {at} is standing inside {site.Key}'s footprint");
            }
            Debug.Log($"[stpeters-village] {planted.Count} planted trees/shrubs/flowers checked against " +
                      $"{Sites.Count} footprints — none inside a wall.");
        }

        [Test]
        public void EveryDoorFacesTheGreen_AndTheDerivationSurvivesAReBakeWithFewerFacings()
        {
            // ⭐ The facing is DERIVED from the green, never a hard-coded cell index, because the kit ships
            // 8 facings today and the owner may halve that — a re-bake, not a code change. So the real
            // assert is not "which cell" but "whichever cell points the door closest to the green", at any
            // facing count.
            Vector2 green = StPetersBuilder.VillageGreen;
            var used = new List<int>();

            foreach (var site in Sites)
            {
                var p = VillageBuildingCatalog.Find(site.Key);
                int facing = StPetersVillage.FacingFor(p, site);
                Assert.That(facing, Is.InRange(0, p.Entry.facings - 1),
                    $"{site.Key} was given facing {facing} of {p.Entry.facings}");
                used.Add(facing);

                // No other facing may point the door closer to the green than the chosen one.
                float chosen = DoorErrorDegrees(p, site.Position, green, facing);
                for (int f = 0; f < p.Entry.facings; f++)
                    Assert.LessOrEqual(chosen, DoorErrorDegrees(p, site.Position, green, f) + 1e-3f,
                        $"{site.Key} faces {facing} but {f} points its door closer to the green");

                // Half a cell is the worst a nearest-cell rounding can be off by.
                Assert.LessOrEqual(chosen, 180f / p.Entry.facings + 1e-3f,
                    $"{site.Key}'s door is {chosen:0.0}° off the green — worse than the " +
                    $"{180f / p.Entry.facings:0.0}° a nearest-of-{p.Entry.facings} rounding allows");
            }

            // A village where every door happened to land on the same cell would satisfy everything above
            // and would mean the derivation is not actually reading the geometry.
            Assert.Greater(used.Distinct().Count(), 2,
                "every door landed on one of two facings — the buildings stand in an arc, so their doors " +
                "must point in genuinely different directions: " + string.Join(",", used));

            // And the derivation must degrade rather than break at a coarser bake. Re-derive at 4 facings
            // with a stub and check each still lands within half a cell.
            foreach (var site in Sites)
            {
                var real = VillageBuildingCatalog.Find(site.Key).Entry;
                var coarse = new VillageBuildingKit.Entry
                {
                    key = real.key, facings = 4,
                    doorY = new[] { 0f, 0f, 1f, 0f },      // front = cell 2 at this count
                    footprintWidthMetres = real.footprintWidthMetres,
                    footprintLengthMetres = real.footprintLengthMetres,
                };
                int f4 = StPetersVillage.FacingToward(coarse, site.Position, green);
                Assert.That(f4, Is.InRange(0, 3),
                    $"{site.Key} at 4 facings resolved to {f4} — the derivation assumed 8 somewhere");
            }

            Debug.Log($"[stpeters-village] doors: " +
                      string.Join(", ", Sites.Select((s, i) => $"{s.Key} d{used[i]}")) +
                      $" — all turned toward the green at {green}.");
        }

        /// <summary>How far off, in degrees, a facing points a building's door from the target.</summary>
        static float DoorErrorDegrees(VillageBuildingCatalog.Placement p, Vector2 from, Vector2 target,
                                      int facing)
        {
            float perCell = 360f / p.Entry.facings;
            // Inverse of StPetersVillage.FacingToward: the front facing points at the camera (−90°).
            float doorDegrees = -90f + (facing - VillageBuildingKit.FrontFacing(p.Entry)) * perCell;
            Vector2 d = target - from;
            return Mathf.Abs(Mathf.DeltaAngle(doorDegrees, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg));
        }

        [Test]
        public void EveryBuildingSpriteThePlacerAsksFor_ExistsOnTheCommittedSheet_OnTheContractsPivot()
        {
            // 🔴 THE SILENT ONE, and the pattern this lane now reaches for every time it places baked art:
            // Place() looks its sprite up BY NAME and SKIPS on a miss, so a naming mismatch does not throw
            // — it builds a village with no buildings in it. The two ends of the name come from different
            // places (the slicer writes them; the catalog composes them), and there is no way to catch it
            // with a scene build: RegionBuildGuard.ConfirmOverwrite cancels in batch mode and still exits 0.
            foreach (var site in Sites)
            {
                var p = VillageBuildingCatalog.Find(site.Key);
                Assert.IsTrue(p.IsValid);

                int facing = StPetersVillage.FacingFor(p, site);
                var sprite = VillageBuildingCatalog.LoadFacing(p, facing);
                Assert.IsNotNull(sprite,
                    $"the placer would ask for facing {facing} of {site.Key} and " +
                    $"{p.SheetPath} does not have it. A miss here places NOTHING and logs a warning " +
                    "nobody reads.");
                Assert.AreEqual(VillageBuildingKit.SpriteNameFor(site.Key, facing), sprite.name);

                // The pivot is the ground CENTRE — which is why Place() applies no offset. Bottom-centre
                // would sink this building metres into the dirt, and the contract says how many.
                Vector2 want = VillageBuildingKit.NormalizedPivot(p.Entry);
                var got = new Vector2(sprite.pivot.x / sprite.rect.width,
                                      sprite.pivot.y / sprite.rect.height);
                Assert.AreEqual(want.x, got.x, 1e-3f, $"{sprite.name}: pivot.x vs the contract");
                Assert.AreEqual(want.y, got.y, 1e-3f,
                    $"{sprite.name}: pivot.y vs the contract. Bottom-centre would sink it " +
                    $"{VillageBuildingCatalog.BelowGroundMetres(p.Entry, VillageBuildingCatalog.PixelsPerUnit()):0.00} m.");

                // Every OTHER facing has to exist too — the owner can re-face a placed building in the
                // scene, and SetFacing returning false would silently leave it pointing the old way.
                Assert.AreEqual(p.Entry.facings, VillageBuildingCatalog.LoadAllFacings(p).Count,
                    $"{site.Key} is missing at least one facing's sprite");
            }
            Debug.Log("[stpeters-village] every sprite the placer asks for exists on the committed sheets, " +
                      "on the contract's ground-centre pivot, with all facings present.");
        }

        [Test]
        public void TheVillageIsDeterministic_AndItsSitesAreAuthoredNotHashed()
        {
            var a = StPetersVillage.Sites;
            var b = StPetersVillage.Sites;
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Key, b[i].Key);
                Assert.AreEqual(a[i].Position, b[i].Position);
            }

            // The village must read as ONE small place — §6.0's "the whole world the size of a low-tide
            // walk". Measured across the buildings themselves rather than the props.
            float widest = 0f;
            for (int i = 0; i < a.Count; i++)
            for (int j = i + 1; j < a.Count; j++)
                widest = Mathf.Max(widest, Vector2.Distance(a[i].Position, a[j].Position));
            Assert.Less(widest, 80f,
                $"the village's two furthest buildings are {widest:0.0} m apart — that is a hamlet strung " +
                "along a road, not a low-tide walk");
            Debug.Log($"[stpeters-village] widest span between two buildings: {widest:0.0} m.");
        }
    }
}
