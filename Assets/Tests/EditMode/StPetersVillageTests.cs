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
    }
}
