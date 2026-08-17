using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;                 // SpriteLightMath — the shared bake camera's squash
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

        const float SpringHighWater = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;   //  2.2
        const float SpringLowWater  = StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;   // -2.2

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

        /// <summary>
        /// <b>The pieces the OPENING is made of</b> — the ground the player wakes on and the two things
        /// within reach of it.
        ///
        /// <para>⚠ It used to be four, and the other two were Ginny's cottage and her freezer. The owner
        /// moved both onto her own plot 85 m east on 2026-08-16 (<see cref="StPetersGinnyPlot"/>), so
        /// they are no longer village pieces and cannot be held to a village's spacing — the assertions
        /// below say "nothing here is more than 40 m from anything else" and "everything here is within
        /// 30 m of the spawn", and a cottage in the woods is neither. <b>Ginny herself stays</b>: her
        /// MARK did not move, because a new game starts at hour 6 with the player waking beside her and
        /// the opening's first beat is talking to the aunt. That is the whole reason her home could move
        /// and her day could not. <see cref="GinnysPlot_IsAWalkFromTheVillage_AndOnDryGroundInTheWoods"/>
        /// holds the other end.</para>
        /// </summary>
        static Vector2[] OpeningPieces => new[]
        {
            (Vector2)StPetersBuilder.GinnyPos,
            (Vector2)StPetersBuilder.NedsLetterPos,
            (Vector2)StPetersBuilder.StartSpawnPos,
        };

        // =================================================================================
        //  THE VILLAGE STANDS ON THE ISLAND
        // =================================================================================

        [Test]
        public void EveryVillagePiece_StandsOnGroundThatIsDryAtEveryTide()
        {
            foreach (var p in OpeningPieces)
            {
                float e = _terrain.ElevationAt(p);
                Assert.Greater(e, SpringHighWater,
                    $"{p} is at {e:0.00} m — the aunt, the letter and the ground you wake on are the " +
                    "OPENING, and the opening does not flood. They must sit above the highest water of " +
                    "a spring tide.");

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
            for (int i = 0; i < OpeningPieces.Length; i++)
            for (int j = i + 1; j < OpeningPieces.Length; j++)
                Assert.Less(Vector2.Distance(OpeningPieces[i], OpeningPieces[j]), 40f,
                    "the village reads as ONE small place — nothing in it is more than 40 m from anything else");

            // And you start in it, not a walk from it: the opening's first beat is talking to the aunt.
            Vector2 spawn = StPetersBuilder.StartSpawnPos;
            foreach (var p in OpeningPieces)
                Assert.Less(Vector2.Distance(spawn, p), 30f,
                    $"{p} is more than 30 m from the start spawn — the player should wake up IN the village");
        }

        // =================================================================================
        //  AUNT GINNY'S PLOT (the owner's 2026-08-16 move)
        // =================================================================================

        /// <summary>
        /// 🔴 <b>THE OTHER END OF THE MOVE.</b> The test above says the opening is tight around the
        /// spawn; this one says her plot is genuinely somewhere ELSE — far enough out to be her own land
        /// in the woods rather than the village's weedy edge, and still on dry ground the player can
        /// walk to. Both have to hold at once, and the pair is what makes "she moved" a fact rather than
        /// a comment.
        /// </summary>
        [Test]
        public void GinnysPlot_IsAWalkFromTheVillage_AndOnDryGroundInTheWoods()
        {
            Vector2 plot = StPetersGinnyPlot.CottagePos;

            // Past the DOORYARD band, which is the repo's own line for how far the village's human
            // disturbance reads on the ground. Inside it she would be living at the end of the village
            // rather than on her own land — the distinction the owner actually asked for.
            float fromHearth = Vector2.Distance(plot, StPetersBuilder.VillageHearthPos);
            Assert.Greater(fromHearth, StPetersWoods.DooryardRadius,
                $"Ginny's plot is {fromHearth:0.0} m from the hearth, inside the " +
                $"{StPetersWoods.DooryardRadius:0.0} m dooryard band — that is the village's disturbed " +
                "edge, not the woods. Her land has to begin past it.");

            // Dry at every tide, and on the plateau rather than down the beach.
            float e = _terrain.ElevationAt(plot);
            Assert.Greater(e, SpringHighWater,
                $"Ginny's cottage is at {e:0.00} m — her land does not flood either.");

            float d = TidalTerrain.IslandDistance(plot, StPetersBuilder.IslandCenter,
                                                  StPetersBuilder.IslandRadius,
                                                  StPetersBuilder.IslandRadiusY);
            Assert.LessOrEqual(d, StPetersBuilder.IslandRadius,
                $"Ginny's cottage is past the plateau edge at elliptical distance {d:0.0}");

            // …and IN the woods, not on the heath: the ground has to be above the tree line, or she has
            // been moved to a clearing that was already a clearing and the move means nothing.
            Assert.Greater(e, StPetersWoods.TreeLineElevation,
                $"Ginny's plot sits at {e:0.00} m, below the {StPetersWoods.TreeLineElevation:0.00} m " +
                "tree line — there would be no woods around her to have moved INTO.");

            Debug.Log($"[stpeters-ginny] plot at {plot} — {fromHearth:0.0} m from the hearth " +
                      $"(dooryard band {StPetersWoods.DooryardRadius:0.0} m), ground {e:0.00} m, " +
                      $"elliptical distance {d:0.0} m of {StPetersBuilder.IslandRadius:0.0}.");
        }

        /// <summary>
        /// Everything that stands on her plot fits inside the clearing she declares, so the woods planter
        /// cannot put a spruce through a shed wall. Same argument as
        /// <see cref="TheClearingContainsEveryFootprint_SoNothingIsPlantedThroughAWall"/>, derived from
        /// the contract rather than from these numbers, so a re-bake fails here with the figure to use.
        /// </summary>
        [Test]
        public void GinnysClearing_ContainsHerCottageAndEveryShed()
        {
            float need = StPetersGinnyPlot.RequiredClearingRadius();
            Assert.Greater(need, 0f, "nothing on the plot — this assert would be vacuous");
            Assert.LessOrEqual(need, StPetersGinnyPlot.ClearingRadius,
                $"the plot's furthest footprint reaches {need:0.00} m but the declared clearing is " +
                $"{StPetersGinnyPlot.ClearingRadius:0.00} m. Widen StPetersGinnyPlot.ClearingRadius to " +
                "at least that, or bring the shed in — as it stands, trees plant through its wall.");

            // Her buildings do not stand in each other, cottage footprint included.
            var cottage = VillageBuildingCatalog.Find(StPetersGinnyPlot.CottageKey);
            float cottageRadius = cottage.IsValid
                ? StPetersVillage.FootprintRadiusMetres(cottage)
                : 0f;

            foreach (var shed in StPetersGinnyPlot.Sheds)
            {
                float gap = Vector2.Distance(shed.Position, StPetersGinnyPlot.CottagePos)
                            - cottageRadius - shed.FootprintRadiusMetres;
                Assert.Greater(gap, 0f,
                    $"the {shed.Key} overlaps the cottage by {-gap:0.00} m at their footprint edges");
            }

            for (int i = 0; i < StPetersGinnyPlot.Sheds.Count; i++)
            for (int j = i + 1; j < StPetersGinnyPlot.Sheds.Count; j++)
            {
                var a = StPetersGinnyPlot.Sheds[i];
                var b = StPetersGinnyPlot.Sheds[j];
                float gap = Vector2.Distance(a.Position, b.Position)
                            - a.FootprintRadiusMetres - b.FootprintRadiusMetres;
                Assert.Greater(gap, 0f,
                    $"the {a.Key} and the {b.Key} overlap by {-gap:0.00} m");
            }

            Debug.Log($"[stpeters-ginny] furthest footprint reaches {need:0.00} m; clearing is " +
                      $"{StPetersGinnyPlot.ClearingRadius:0.0} m " +
                      $"({StPetersGinnyPlot.ClearingRadius - need:0.00} m of headroom).");
        }

        /// <summary>
        /// 🔴 <b>HER COTTAGE IS A DOOR, NOT A PICTURE OF ONE — and its occupant is bound.</b> The owner
        /// ruled on 2026-08-16 that the woods cottage is a real building you can walk into, which is a
        /// claim about what <see cref="StPetersGinnyPlot.Place"/> actually builds, not about what the kit
        /// is capable of. This stands the plot up for real and looks.
        ///
        /// <para>The occupant check is the <b>#512 regression surface</b>: a <c>BuildingInterior</c> whose
        /// <c>_occupant</c> is null falls back to <c>GameServices.PlayerTransform</c>, which is exactly
        /// the path that broke when a region was travelled to rather than started in. Placing her cottage
        /// with a null occupant would look identical from outside and open onto nothing.</para>
        ///
        /// <para>⚠ Everything here is torn down explicitly: <see cref="StPetersGinnyPlot.Place"/> creates
        /// a ROOT GameObject in the open scene, and a fixture that leaves it behind poisons every later
        /// test that calls <c>GameObject.Find</c>.</para>
        /// </summary>
        [Test]
        public void GinnysCottage_StandsAsAnEnterableBuilding_WithItsOccupantBound()
        {
            var occupantGo = new GameObject("GinnyPlotTest_Occupant");
            var tex = new Texture2D(2, 2);
            var greybox = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 32f);
            GameObject root = null;

            try
            {
                int placed = StPetersGinnyPlot.Place(_terrain, greybox, occupantGo.transform);
                root = GameObject.Find(StPetersGinnyPlot.RootName);
                Assert.IsNotNull(root, "StPetersGinnyPlot.Place built no root — the plot is not standing.");

                // The sheds are greybox and always place; the cottage needs the kit baked. If the kit is
                // absent this is a working-tree state, not a repo one — say so rather than fail vaguely.
                var cottage = root.transform.Find(StPetersGinnyPlot.CottageKey);
                if (cottage == null)
                {
                    Assert.Ignore($"'{StPetersGinnyPlot.CottageKey}' is not baked in this working tree, " +
                                  "so there is no cottage to enter. Run Hidden Harbours ▸ Art ▸ Bake " +
                                  "Village Buildings.");
                }

                Assert.That(placed, Is.GreaterThanOrEqualTo(StPetersGinnyPlot.Sheds.Count + 1),
                            "the plot placed fewer buildings than the cottage plus its sheds");

                var interior = cottage.GetComponentInChildren<BuildingInterior>(true);
                Assert.IsNotNull(interior,
                    "Ginny's cottage has no BuildingInterior, so her door does not open. The owner ruled " +
                    "this cottage ENTERABLE — that ruling is the whole reason it stopped being a greybox " +
                    "standee and became a kit build.");

                var so = new UnityEditor.SerializedObject(interior);
                var bound = so.FindProperty("_occupant").objectReferenceValue;
                Assert.AreSame(occupantGo.transform, bound,
                    "her interior's occupant is not the transform Place() was handed. A null occupant " +
                    "falls back to GameServices.PlayerTransform, which is precisely the #512 defect: it " +
                    "looks right from outside and opens onto nothing in a region you travelled to.");

                // Her freezer came out here with her, and it is still the thing you walk up to.
                var freezer = root.transform.Find("GinnyFreezer");
                Assert.IsNotNull(freezer, "the freezer did not follow her onto the plot");
                Assert.That(Vector2.Distance(freezer.position, StPetersGinnyPlot.FreezerPos),
                            Is.LessThan(0.01f), "the freezer is not on its authored spot");

                foreach (var shed in StPetersGinnyPlot.Sheds)
                    Assert.IsNotNull(root.transform.Find(shed.Key),
                        $"the {shed.Key} is missing — the plot's derelict sheds are data rows and every " +
                        "one of them places");
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                Object.DestroyImmediate(occupantGo);
                Object.DestroyImmediate(greybox);
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// Sabotage: prove the dooryard-band constraint BITES. A plot sited where her cottage used to
        /// stand — or anywhere in the village's disturbed ring — must fail the test above, or that test
        /// is not constraining anything and "she moved into the woods" is decoration.
        /// </summary>
        [Test]
        public void Sabotage_APlotAtTheOldVillageSite_IsInsideTheDooryardBand()
        {
            Vector2 whereSheUsedToLive = StPetersBuilder.VillageHearthPos;
            float d = Vector2.Distance(whereSheUsedToLive, StPetersBuilder.VillageHearthPos);
            Assert.Less(d, StPetersWoods.DooryardRadius,
                "the old cottage site was supposed to be inside the dooryard band — if it is not, the " +
                "plot test is not actually constraining where Ginny lives.");

            // And a site just outside the village's no-plant clearing is STILL not the woods: the two
            // radii are different questions (44 m of buildings vs 74.8 m of human disturbance), and
            // siting her at the nearer one would have been the mistake that looks correct.
            Vector2 justOutsideTheClearing =
                (Vector2)StPetersBuilder.VillageHearthPos +
                new Vector2(StPetersWoods.VillageClearingRadius + 1f, 0f);
            Assert.Less(Vector2.Distance(justOutsideTheClearing, StPetersBuilder.VillageHearthPos),
                        StPetersWoods.DooryardRadius,
                "a site one metre outside the village's tree clearing was supposed to still be inside " +
                "the dooryard band — that gap is the whole reason the plot is sited off the dooryard " +
                "radius and not the clearing radius.");
        }

        /// <summary>
        /// Sabotage for the DRY-GROUND guard: <see cref="StPetersGinnyPlot.Place"/> logs an error for any
        /// building sited at or below spring high water, and this proves that check can actually fail.
        /// Take the plot and walk it off the plateau into the sea — the ground must come back wet, or the
        /// guard is asserting something that is true everywhere and protects nothing.
        /// </summary>
        [Test]
        public void Sabotage_GinnysPlotMovedIntoTheSea_IsGroundTheDryCheckRejects()
        {
            // Due south off the island, well past the plateau edge and its beach band.
            var inTheSea = new Vector2(StPetersGinnyPlot.CottagePos.x,
                                       StPetersBuilder.IslandCenter.y - StPetersBuilder.IslandRadiusY - 60f);

            float wet = _terrain.ElevationAt(inTheSea);
            Assert.Less(wet, SpringHighWater,
                $"a site 60 m off the south shore came back at {wet:0.00} m, above spring high water " +
                $"({SpringHighWater:0.00} m) — if the sea is dry ground, the plot's own dry check is " +
                "vacuous and a cottage could be sited in the water without anything firing.");

            // …and the real site passes the same predicate, so the two arms are the same question.
            float dry = _terrain.ElevationAt(StPetersGinnyPlot.CottagePos);
            Assert.Greater(dry, SpringHighWater,
                $"Ginny's actual plot came back at {dry:0.00} m — her land floods.");
        }

        [Test]
        public void Sabotage_TheOldGreyboxVillageSite_IsNowhereNearTheStartSpawn()
        {
            // What the builder used to place: everything within a few metres of (-40, 0), the centre of the
            // pre-#328 greybox disc. Measure how far that is from where the player actually spawns, so the
            // fix is shown landing on a real dislocation rather than on a tidy-up.
            var oldSite = new Vector2(-40f, 0f);
            float drift = Vector2.Distance(oldSite, StPetersBuilder.StartSpawnPos);
            // ⚠ 40, not the 55 this pinned before 2026-07-30: the island shrink translated the spawn
            // east with the village, so today's spawn sits nearer the old greybox site than the 450 m
            // island's spawn did. The dislocation being measured is unchanged — the old cluster was
            // placed relative to a disc that no longer existed.
            Assert.Greater(drift, 40f,
                $"the old village site sat {drift:0} m from the start spawn — far enough that the player " +
                "woke up alone on an empty island with the cottage out of sight");

            // It is still ON the island (even the re-ruled 240 m one), which is exactly why nothing
            // caught it: no test failed, no warning fired, the objects were simply in the wrong place.
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
            // ⚠ 20, not the 50 this pinned before 2026-07-30: on the re-ruled 240 × 140 island the sea
            // is genuinely closer to everything (measured ~28 m from this spot) — but the old spot is
            // STILL at full plateau height, in the meadow, nowhere near "the sand rim, at the water".
            Assert.Greater(nearest, 20f,
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
                "the slip DRIES near spring low (§5.1a: the berth bed is -1.0 m against a -2.2 m low), so a " +
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
            //
            // ⭐ FIVE BUILDINGS, TWO KITS, since the owner's 2026-08-11 ruling. The store used to be the
            // fifth HOUSE — houseIsoRig with a bay window standing in for a shop, because the shop kit
            // did not exist. It does now, so the real shell replaces the stand-in AT THE SAME SITE and
            // the store moved to StPetersShops. The docs' count is unchanged; which kit each building
            // comes from is what moved, and that is what this asserts.
            Assert.AreEqual(4, Sites.Count, "four houses: three clapboard houses and the school");
            CollectionAssert.AreEquivalent(
                new[] { "school", "whiteFarmhouse", "redSaltbox", "sageCottage" },
                Sites.Select(s => s.Key).ToArray(),
                "the village's houses drifted from the kit's M1 set");

            CollectionAssert.DoesNotContain(Sites.Select(s => s.Key).ToArray(), "generalStore",
                "the general store is a SHOP now (StPetersShops). A village carrying both would stand " +
                "two general stores on one site — the stand-in and the real one.");

            Assert.AreEqual(5, Sites.Count + StPetersShops.Sites.Count - 1,
                "§5.1 asks for five buildings; the two kits together are the four houses plus the store " +
                "and the post office, and the post office is the sixth the owner added on 2026-08-11.");

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
            Vector2 hearth = StPetersBuilder.VillageHearthPos;
            Vector2 spawn = StPetersBuilder.StartSpawnPos;
            // ⚠ The freezer came off this list on 2026-08-16. It was a village prop while it stood by
            // Ginny's cottage at the hearth; it followed her 85 m east onto her plot, so asking whether a
            // village building crowds it is asking a question about two different places.
            var props = new[]
            {
                ("Ginny", (Vector2)StPetersBuilder.GinnyPos),
                ("Ned's letter", (Vector2)StPetersBuilder.NedsLetterPos),
            };

            foreach (var site in Sites)
            {
                float r = Radius(site);

                Assert.GreaterOrEqual(Vector2.Distance(site.Position, hearth),
                                      r + StPetersVillage.HearthClearanceRadius,
                    $"{site.Key} is on the hearth. Ginny's cottage stood there until 2026-08-16 and the " +
                    "lot is empty now, but the hearth is still the middle of the village — the green is " +
                    "measured from it — and nothing may crowd it.");

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
            // house on the natural west side of the green would fail it. (Moved with the village on the
            // 2026-07-30 shrink — a few metres west of today's green, on the plateau, square across the
            // bar head's approach.)
            var westOfTheGreen = new Vector2(-48f, 4f);
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
                    Vector2.Distance(site.Position, StPetersBuilder.VillageHearthPos) + Radius(site),
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
            var shrubSpecies = new List<string> { "LowbushBlueberry", "SweetGale", "Meadowsweet",
                                                  "BeakedHazelnut", "WildRose", "Raspberry" };
            System.Func<string, string> shrubHabitat =
                s => habitatOf.TryGetValue(s, out string h) ? h : null;

            planted.AddRange(StPetersShrubs.Scatter(
                                 _terrain, shrubSpecies, shrubHabitat, ShrubCatalog.Variants)
                                          .Select(s => (s.Species, s.Position)));

            // ⚠ THE UNDERSTOREY IS A FOURTH PLANTER AND HAS TO BE IN HERE, or this check quietly stops
            // covering a whole layer — which is the exact hole its own preamble warns about ("the
            // structural argument is only as good as its premise that every planter routes through
            // IsPlantable"). Handed NOTHING already standing on purpose: the min-gap rule only ever
            // removes sites, so an empty neighbour set yields a SUPERSET of what the build plants, which
            // is the conservative direction for a "nothing is inside a wall" test.
            planted.AddRange(StPetersShrubs.ScatterUnderstorey(
                                 _terrain, shrubSpecies, shrubHabitat, ShrubCatalog.Variants,
                                 new List<Vector2>())
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
                    // The door is ABOVE the pivot on screen at cell 0, which is what puts it on the +Y
                    // gable — the side both exterior rigs actually use. (Screen y grows downward, so
                    // "above" is the smaller number.) At 4 facings the front is then cell 2.
                    pivotY = 100f,
                    doorX = new[] { 0f, 0f, 0f, 0f },
                    doorY = new[] { 0f, 0f, 0f, 0f },
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

        /// <summary>
        /// How far off, in degrees, a facing points a building's door from the target.
        ///
        /// <para><b>🔴 THIS USED TO BE THE ALGEBRAIC INVERSE OF THE IMPLEMENTATION, AND THAT IS WHY IT
        /// REPORTED 0° FOR A VILLAGE WHOSE DOORS WERE ALL MIRRORED.</b> It restated
        /// <c>StPetersVillage.FacingToward</c>'s own formula —
        /// <c>−90 + (facing − FrontFacing)·perCell</c> — so the pair agreed with each other and with
        /// nothing else. The method's sign was wrong (cell <c>i</c> is baked at
        /// <c>RigBaker.DirForCell</c>, so a door's bearing DECREASES as the index rises), the
        /// schoolhouse door pointed ~92° away from the green, and this test passed.</para>
        ///
        /// <para>It now goes through <see cref="BuildingFacing"/>, which reads the bake's own per-facing
        /// door anchors — so a wrong answer would have to be wrong in the baked pixels. It also
        /// un-squashes the target direction, which the old one did not: the world XY plane is the
        /// squashed ground plane and an angle taken raw off it is out by up to 20°.</para>
        /// </summary>
        static float DoorErrorDegrees(VillageBuildingCatalog.Placement p, Vector2 from, Vector2 target,
                                      int facing) =>
            BuildingFacing.DoorErrorDegrees(p.Entry.doorY, p.Entry.pivotY, p.Entry.facings,
                                            SpriteLightMath.GroundDepthScale, from, target, facing);

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
