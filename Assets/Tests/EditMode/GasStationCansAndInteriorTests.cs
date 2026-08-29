using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;                 // FuelContainerDef / StationPieceDef
using HiddenHarbours.Art.Editor;          // StationCatalog / StationReachAudit
using HiddenHarbours.Core;
using HiddenHarbours.World;               // InteriorFootprint

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE C-STORE OPENS, AND THERE ARE CANS TO FILL</b> — the two halves of the owner's 2026-08-28
    /// ask, and both of them guard failures that DRAW PERFECTLY.
    ///
    /// <para><b>What was actually wrong, and why nothing caught it.</b> The gas station's sales floor has
    /// shipped since #612/#613 and has stood at Route 91 since #626 — furnished, reach-audited, drawn one
    /// sorting order under its own shell — and in all that time it has been sealed inside a solid
    /// <c>building</c> blocker with no way in. Every test that existed passed, because every one of them
    /// asked whether the room was PLACED and none asked whether it could be ENTERED. The owner found it
    /// by walking up to the building.</para>
    ///
    /// <para><b>⭐ THE LOAD-BEARING TEST IS THE FRAME EQUIVALENCE, AND ITS CONTROL.</b> Opening the
    /// storefront reuses the house family's <see cref="BuildingInterior"/>, whose geometry squashes world
    /// Y by <c>sin 40°</c>. The station kit does not squash — it says so in
    /// <see cref="StationCatalog.LocalToWorld"/>, and every collider on the forecourt is built that way.
    /// The reuse is therefore safe only at <see cref="StationInteriorPlacement.NoSquash"/>, and that is
    /// not an opinion to be held in a comment: it is measured here at all eight facings, and paired with
    /// the control that shows the house's own scale would be metres wrong.</para>
    ///
    /// <para>Every can spot is likewise paired with a deliberate WRONG one, in this region's convention
    /// (<c>NineMileCreekStationTests</c>) and for its hard-won reason: the whole-scene reach pass once
    /// passed a pedestal standing in mid-air, and only a positive control found it.</para>
    /// </summary>
    public class GasStationCansAndInteriorTests
    {
        readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        MainlandTidalTerrain MakeCreekTerrain()
        {
            var go = new GameObject("TidalTerrain");
            _spawned.Add(go);
            var terrain = go.AddComponent<MainlandTidalTerrain>();
            NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);
            return terrain;
        }

        static void RequireKit()
        {
            if (!StationCatalog.IsInstalled)
                Assert.Ignore("the gas-station kit has not been baked/sliced/Def-built in this checkout");
        }

        static void RequireContainers()
        {
            if (StationFuelCans.LoadContainer(NineMileCreekStation.LoanerCanDefIds[0]) == null)
                Assert.Ignore("the fuel-container kit has not been baked in this checkout");
        }

        // =============================================================================================
        //  1. THE FRAME — the claim the whole interior reuse rests on
        // =============================================================================================

        /// <summary>Off-axis on both axes and asymmetric, so a swapped axis, a sign flip or a
        /// transpose all move the answer.</summary>
        static readonly Vector2[] ProbePoints =
        {
            new Vector2(5.8f, 4.1f), new Vector2(-5.8f, 4.1f), new Vector2(0.3f, 4.1f),
            new Vector2(-2.35f, 1.7f), new Vector2(3.9f, -2.6f), new Vector2(0f, -4.1f),
        };

        [Test]
        public void AtNoSquash_TheInteriorsFrameAndTheStationsFrameAreTheSameFrame()
        {
            var centre = new Vector2(-191f, 81.6f);

            for (int facing = 0; facing < StationCatalog.Facings; facing++)
            {
                var footprint = new InteriorFootprint(centre, 11.6f, 8.2f, facing,
                                                      StationCatalog.Facings,
                                                      StationInteriorPlacement.NoSquash);

                foreach (Vector2 p in ProbePoints)
                {
                    Vector2 asRoom = footprint.ModelToWorld(p);
                    Vector2 asStation = StationCatalog.LocalToWorld(p, centre, facing);

                    Assert.That(Vector2.Distance(asRoom, asStation), Is.LessThan(1e-4f),
                        $"at cell {facing} the room geometry puts local {p} at {asRoom} and the station " +
                        $"kit puts it at {asStation}. These MUST agree: the doorway is cut with the " +
                        "former and every other collider on the forecourt is placed with the latter, so " +
                        "a disagreement is a wall in a different place from the building it belongs to.");
                }
            }
        }

        [Test]
        public void AndTheHouseFamilysSquash_WouldPutTheBackWallMetresWrong()
        {
            // The control for the test above. Without it that test could pass because BOTH sides were
            // wrong in the same way, or because the probe points happened to sit on the one axis the
            // squash does not touch.
            var centre = new Vector2(-191f, 81.6f);
            const int facing = 0;           // local +y straight up the screen: the squash's worst case

            var honest = new InteriorFootprint(centre, 11.6f, 8.2f, facing, StationCatalog.Facings,
                                               StationInteriorPlacement.NoSquash);
            var squashed = new InteriorFootprint(centre, 11.6f, 8.2f, facing, StationCatalog.Facings,
                                                 SpriteLightMath.GroundDepthScale);

            var backWall = new Vector2(0f, -4.1f);
            float gap = Vector2.Distance(honest.ModelToWorld(backWall), squashed.ModelToWorld(backWall));

            Assert.That(gap, Is.GreaterThan(1f),
                "the house family's depth scale must be VISIBLY wrong for a station piece — if it were " +
                "not, the equivalence test above would be proving nothing. Measured gap at the back " +
                $"wall: {gap:0.###} m.");

            Assert.That(StationInteriorPlacement.NoSquash, Is.EqualTo(1f),
                "the station kit places in unsquashed ground metres — StationCatalog.LocalToWorld is a " +
                "pure rotation. A NoSquash that was not 1 would be a different claim.");
        }

        // =============================================================================================
        //  2. THE WALL IS DERIVED FROM THE ROOM INSIDE IT
        // =============================================================================================

        static IEnumerable<StationPieceDef> Storefronts()
        {
            foreach (var kv in StationCatalog.Defs())
                if (kv.Value != null && !kv.Value.IsInterior &&
                    StationInteriorPlacement.RoomFor(kv.Value) != null)
                    yield return kv.Value;
        }

        [Test]
        public void EveryStorefrontWithARoom_DerivesOneWallThicknessThatBothAxesAgreeOn()
        {
            RequireKit();

            int checkedPairs = 0;
            foreach (StationPieceDef shell in Storefronts())
            {
                StationPieceDef room = StationInteriorPlacement.RoomFor(shell);

                Assert.That(StationInteriorPlacement.TryPlan(shell, room,
                                out StationInteriorPlacement.Plan plan, out string why),
                    Is.True, $"'{shell.name}' cannot be opened onto '{room.name}': {why}");

                // The wall is the ground the shell covers that the floor does not, and the whole point
                // of taking it on BOTH axes is that they can then be compared. A shell and a room that
                // were never meant for each other disagree here and nothing else would notice.
                Assert.That(plan.WallThicknessMetres, Is.GreaterThan(0f));
                Assert.That(plan.WallThicknessMetres, Is.LessThan(1f),
                    $"'{shell.name}' derives a {plan.WallThicknessMetres:0.###} m wall — that is a room " +
                    "much smaller than its shell, not a wall thickness");

                // …and the room really is inside the walls it just sized.
                Assert.That(plan.WidthMetres, Is.GreaterThan(0f));
                Assert.That(plan.LengthMetres, Is.GreaterThan(0f));
                checkedPairs++;
            }

            Assert.That(checkedPairs, Is.GreaterThan(0),
                "no shell/room pair was found at all — this test would pass vacuously. The kit ships " +
                "store_sStore/interior_sStore and store_sKiosk/interior_sKiosk.");
        }

        [Test]
        public void TheCStore_DerivesTheWallItsOwnTwoFootprintsImply()
        {
            RequireKit();

            StationPieceDef shell = StationCatalog.Find("store_sStore");
            StationPieceDef room = StationCatalog.Find("interior_sStore");
            if (shell == null || room == null) Assert.Ignore("the C-store pair is not installed");

            Assert.That(StationInteriorPlacement.TryPlan(shell, room,
                            out StationInteriorPlacement.Plan plan, out string why), Is.True, why);

            // 11.6 x 8.2 m of building around 11.2 x 7.8 m of floor: 0.20 m of wall on every side. The
            // numbers are the kit's, not this test's — asserted so a re-bake that moves either footprint
            // shows up here rather than in a wall standing somewhere new.
            Assert.That(plan.WidthMetres, Is.EqualTo(11.6f).Within(0.01f));
            Assert.That(plan.LengthMetres, Is.EqualTo(8.2f).Within(0.01f));
            Assert.That(plan.WallThicknessMetres, Is.EqualTo(0.2f).Within(0.005f));

            // ⚠️ MEASURED, not remembered. The house family's doorway is on −Y; this kit's is on +Y and
            // 0.30 m off centre. Carrying the wrong one across is the #509 trap, and it opens a
            // building onto its own back wall while drawing perfectly.
            Assert.That(plan.DoorOnPlusY, Is.True);
            Assert.That(plan.DoorAcrossMetres, Is.EqualTo(0.3f).Within(0.005f));
            Assert.That(plan.DoorwayWidthMetres, Is.EqualTo(2.4f).Within(0.01f));

            // And the Def's declared width is NOT the building's — it reaches out to the ice box and the
            // propane cage. Walling to it would wall the forecourt.
            Assert.That(shell.WidthMeters, Is.GreaterThan(plan.WidthMetres),
                "if these ever became equal the distinction this plan draws would be untested");
        }

        [Test]
        public void AShellAndARoomThatAreNotAPair_AreRefusedRatherThanOpened()
        {
            RequireKit();

            StationPieceDef bigShell = StationCatalog.Find("store_sStore");
            StationPieceDef smallRoom = StationCatalog.Find("interior_sKiosk");
            if (bigShell == null || smallRoom == null) Assert.Ignore("both storefronts are not installed");

            // The kiosk's floor fits inside the C-store's walls with metres to spare, so "is the floor
            // inside" alone would ACCEPT this. It is the two-axis agreement that refuses it — which is
            // exactly why the thickness is taken twice.
            Assert.That(StationInteriorPlacement.TryPlan(bigShell, smallRoom, out _, out string why),
                Is.False,
                "a C-store walled around a kiosk's sales floor would draw a room floating in the middle " +
                "of a much bigger building, and the player would walk through the walls of the room they " +
                "can see");
            Assert.That(why, Is.Not.Null.And.Not.Empty, "a refusal must say why");
        }

        [Test]
        public void TheDoorwayIsCutWhereTheEntryIsDRAWN_AndTheRestOfThatWallIsSolid()
        {
            RequireKit();

            StationPieceDef shell = StationCatalog.Find("store_sStore");
            StationPieceDef room = StationCatalog.Find("interior_sStore");
            if (shell == null || room == null) Assert.Ignore("the C-store pair is not installed");
            Assert.That(StationInteriorPlacement.TryPlan(shell, room,
                            out StationInteriorPlacement.Plan plan, out _), Is.True);

            var centre = Vector2.zero;
            var footprint = new InteriorFootprint(centre, plan.WidthMetres, plan.LengthMetres,
                                                  0, StationCatalog.Facings,
                                                  StationInteriorPlacement.NoSquash,
                                                  plan.DoorOnPlusY ? 1f : -1f, plan.DoorAcrossMetres);

            Vector2[][] walls = footprint.WallQuads(plan.WallThicknessMetres, plan.DoorwayWidthMetres);

            // The threshold itself: a gap, or there is no way in.
            var threshold = new Vector2(plan.DoorAcrossMetres, plan.LengthMetres * 0.5f
                                                               - plan.WallThicknessMetres * 0.5f);
            Assert.That(InsideAny(walls, threshold), Is.False,
                "the doorway the sidecar publishes is filled in with wall — the room would be sealed");

            // …and a doorway's width further along the SAME wall: solid, or the front of the shop is a
            // hole. This is the half that a gap cut at the wall's centre would fail.
            var offToTheSide = new Vector2(plan.DoorAcrossMetres + plan.DoorwayWidthMetres,
                                           threshold.y);
            Assert.That(InsideAny(walls, offToTheSide), Is.True,
                $"the front wall is open at x={offToTheSide.x:0.##} m, which is not where the door is " +
                "drawn — a gap in the wrong place is a player walking in through the window");
        }

        static bool InsideAny(Vector2[][] quads, Vector2 p)
        {
            foreach (Vector2[] q in quads)
            {
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (Vector2 v in q)
                {
                    minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                    minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
                }
                if (p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY) return true;
            }
            return false;
        }

        // =============================================================================================
        //  3. THE CANS — placed content, and every spot proved
        // =============================================================================================

        static int CanFacing => StationCatalog.FacingForHeading(
            NineMileCreekStation.Route91RoadHeadingDegrees);

        static Vector2 CanWorld(in StationFuelCans.Spot spot) =>
            StationCatalog.LocalToWorld(spot.Local, NineMileCreekStation.Route91ForecourtPos, CanFacing);

        [Test]
        public void ThereAreCansAtAll_AndEveryOneIsACarriableDefWithAnEmptyFrameBaked()
        {
            RequireContainers();

            IReadOnlyList<StationFuelCans.Spot> spots = NineMileCreekStation.Route91CanSpots();
            Assert.That(spots.Count, Is.GreaterThan(0),
                "the owner asked for 'a few emtpy fuel cansiters to test filling' — none is not a few");

            foreach (StationFuelCans.Spot spot in spots)
            {
                FuelContainerDef def = StationFuelCans.LoadContainer(spot.DefId);
                Assert.That(def, Is.Not.Null, $"no FuelContainerDef with id '{spot.DefId}'");
                Assert.That(def.Carriable, Is.True,
                    $"'{spot.DefId}' cannot be picked up, so standing one at a pump is a can the player " +
                    "can see and never lift");

                // Empty has to be a picture, not just a number: the fuel kit bakes a fill LADDER and the
                // can is placed at its first rung. Without a 0 rung an "empty" can would draw as a
                // quarter-full one and the fill would show no change at the bottom of the range.
                Assert.That(def.FillCount, Is.GreaterThan(1), $"'{spot.DefId}' has no fill ladder");
                Assert.That(def.FillFractions[0], Is.EqualTo(0f).Within(1e-4f),
                    $"'{spot.DefId}'s lowest baked rung is {def.FillFractions[0]}, so an empty can does " +
                    "not draw empty");
                Assert.That(def.Frame(0, 0), Is.Not.Null, $"'{spot.DefId}' has no empty frame baked");
            }
        }

        [Test]
        public void EveryCanStandsOnTheForecourtsOwnPaving()
        {
            Rect apron = NineMileCreekStation.Route91ApronArea();

            foreach (StationFuelCans.Spot spot in NineMileCreekStation.Route91CanSpots())
            {
                Vector2 at = CanWorld(spot);
                Assert.That(apron.Contains(at), Is.True,
                    $"a can at {at} is outside the paved apron {apron}. The pad is what stops the meadow " +
                    "growing between the pumps (NineMileCreekFields.IsGrassGround steps off any pad), so " +
                    "a can off it stands in grass in the middle of a forecourt.");
            }
        }

        [Test]
        public void EveryCanIsFarEnoughFromTheNextToBeItsOwnCandidate()
        {
            IReadOnlyList<StationFuelCans.Spot> spots = NineMileCreekStation.Route91CanSpots();
            float apart = 2f * StationFuelCans.CanClearanceMetres;

            for (int i = 0; i < spots.Count; i++)
                for (int j = i + 1; j < spots.Count; j++)
                {
                    float d = Vector2.Distance(CanWorld(spots[i]), CanWorld(spots[j]));
                    Assert.That(d, Is.GreaterThan(apart),
                        $"cans {i} and {j} are {d:0.##} m apart and each wants " +
                        $"{StationFuelCans.CanClearanceMetres:0.##} m of room — two cans in one spot " +
                        "make the interact resolver pick by id ordinal forever, and one of them can " +
                        "never be reached");
                }
        }

        [Test]
        public void EveryCanHasGroundUnderIt_AndSomewhereToStandBesideIt()
        {
            RequireKit();
            MainlandTidalTerrain terrain = MakeCreekTerrain();

            var blocks = NineMileCreekStation.SceneObstructions(terrain);
            var forecourt = NineMileCreekStation.Route91Layout().AsPlaced();

            foreach (StationFuelCans.Spot spot in NineMileCreekStation.Route91CanSpots())
            {
                Vector2 at = CanWorld(spot);
                Assert.That(StationFuelCans.Standable(at, forecourt, blocks, out string why), Is.True,
                    $"the can at {at} ({spot.DefId}) cannot be placed: {why}");
            }
        }

        // ---- the positive controls: a green above must be EARNED ------------------------------------

        [Test]
        public void ACanOverTheBasin_IsRefused()
        {
            RequireKit();
            MainlandTidalTerrain terrain = MakeCreekTerrain();

            var blocks = NineMileCreekStation.SceneObstructions(terrain);
            var forecourt = NineMileCreekStation.Route91Layout().AsPlaced();

            // Find real water rather than typing a point at one: a hard-coded "wet" coordinate that
            // silently became dry would turn this control off without failing.
            Vector2 wet = Vector2.zero;
            bool found = false;
            for (float x = -60f; x <= 160f && !found; x += 4f)
                for (float y = 0f; y <= 160f && !found; y += 4f)
                {
                    var p = new Vector2(x, y);
                    if (terrain.ElevationAt(p) < NineMileCreekMainland.SpringHighWater)
                    { wet = p; found = true; }
                }

            Assert.That(found, Is.True,
                "no point in the region floods at spring high — this control cannot fire, so the " +
                "dry-ground check above is unproven");
            Assert.That(blocks(wet, StationReachAudit.Level.Ground), Is.True,
                $"{wet} was chosen because it floods, so the region must call it blocked");

            Assert.That(StationFuelCans.Standable(wet, forecourt, blocks, out string why), Is.False,
                "a can standing in the harbour was accepted. This is the gas-station arc's own finding " +
                "spelled again: a pass that asks only 'is this inside a solid' passed a pump standing " +
                "1.06 m out over the basin.");
            Assert.That(why, Does.Contain("stand"), "the refusal should say what is missing");
        }

        [Test]
        public void ACanInsideTheCStore_IsRefused()
        {
            RequireKit();
            MainlandTidalTerrain terrain = MakeCreekTerrain();

            var blocks = NineMileCreekStation.SceneObstructions(terrain);
            var forecourt = NineMileCreekStation.Route91Layout().AsPlaced();

            // The store's own ground centre — dry land, on the pad, and squarely inside the building.
            Vector2 inTheShop = StationCatalog.LocalToWorld(new Vector2(0f, -11.6f),
                                                           NineMileCreekStation.Route91ForecourtPos,
                                                           CanFacing);

            Assert.That(blocks(inTheShop, StationReachAudit.Level.Ground), Is.False,
                "this control is meant to be caught by the FORECOURT's blockers, not by the region's — " +
                "if the region already refuses it, it proves nothing about the piece test");

            Assert.That(StationReachAudit.BlockerAt(forecourt, inTheShop,
                            StationReachAudit.Level.Ground, StationFuelCans.CanClearanceMetres),
                Is.Not.Null, "the C-store's own plan must cover its own centre");

            Assert.That(StationFuelCans.Standable(inTheShop, forecourt, blocks, out _), Is.False,
                "a loaner can was accepted inside the shop building");
        }

        [Test]
        public void ACanWalledInOnEverySide_IsRefusedEvenThoughItsOwnSpotIsClear()
        {
            RequireKit();

            // The third check earns its keep here and only here: this point is on good ground and inside
            // nothing, and it is still no place for a can, because a body cannot get to it. Without the
            // standing-spot probe, Standable would say yes.
            var forecourt = new List<StationReachAudit.Placed>();
            StationPieceDef store = StationCatalog.Find("store_sStore");
            if (store == null) Assert.Ignore("the C-store is not installed");

            // Four storefronts in a pinwheel leave a hole at the middle that is smaller than a body.
            float r = 5.9f;     // just over half the C-store's 11.6 m plan: the walls close the gap
            forecourt.Add(new StationReachAudit.Placed(store, new Vector2(r, 0f), 0, "east"));
            forecourt.Add(new StationReachAudit.Placed(store, new Vector2(-r, 0f), 0, "west"));
            forecourt.Add(new StationReachAudit.Placed(store, new Vector2(0f, 4.2f), 2, "north"));
            forecourt.Add(new StationReachAudit.Placed(store, new Vector2(0f, -4.2f), 2, "south"));

            Vector2 pen = Vector2.zero;
            Assert.That(StationReachAudit.BlockerAt(forecourt, pen, StationReachAudit.Level.Ground,
                            StationFuelCans.CanClearanceMetres),
                Is.Null, "the pen's own centre must be CLEAR, or this tests the wrong check");

            Assert.That(StationFuelCans.HasStandingSpot(pen, forecourt, null), Is.False,
                "a body was found somewhere inside a sealed pen");
            Assert.That(StationFuelCans.Standable(pen, forecourt, null, out string why), Is.False,
                "a can with no way to reach it was accepted");
            StringAssert.Contains("lift", why);
        }
    }
}
