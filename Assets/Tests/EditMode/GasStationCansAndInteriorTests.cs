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
    /// <para><b>⭐ THE LOAD-BEARING TESTS ARE THE FRAME EQUIVALENCE AND THE ART ALIGNMENT.</b> Opening
    /// the storefront reuses the house family's <see cref="BuildingInterior"/>, whose geometry rotates on
    /// the ground and then squashes world Y by <c>sin 40°</c>. So does the station kit, since ADR 0042:
    /// <see cref="StationCatalog.LocalToWorld"/> is the same rotate-then-squash, and every collider on
    /// the forecourt is placed with it. That agreement is not an opinion held in a comment — it is
    /// measured here at all eight facings, with a control that shows the frame really is squashed and
    /// really inverts — and the alignment tests are the guard the whole arc earned: every piece's ground
    /// footprint, projected the way the kit places it, fits inside the piece's own drawn cell, and the
    /// UNSQUASHED projection (the kit's placement until ADR 0042) does not. That second test is the one
    /// that would have caught the original defect on day one.</para>
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
        //  1. THE FRAME — the claim the whole interior reuse rests on (ADR 0042)
        // =============================================================================================

        /// <summary>Off-axis on both axes and asymmetric, so a swapped axis, a sign flip or a
        /// transpose all move the answer.</summary>
        static readonly Vector2[] ProbePoints =
        {
            new Vector2(5.8f, 4.1f), new Vector2(-5.8f, 4.1f), new Vector2(0.3f, 4.1f),
            new Vector2(-2.35f, 1.7f), new Vector2(3.9f, -2.6f), new Vector2(0f, -4.1f),
        };

        [Test]
        public void TheInteriorsFrameAndTheStationsFrameAreTheSameFrame_AtTheSharedBakeSquash()
        {
            var centre = new Vector2(-191f, 81.6f);

            for (int facing = 0; facing < StationCatalog.Facings; facing++)
            {
                var footprint = new InteriorFootprint(centre, 11.6f, 8.2f, facing,
                                                      StationCatalog.Facings,
                                                      SpriteLightMath.GroundDepthScale);

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
        public void AndTheStationsFrame_IsRotateThenSquash_NotThePureRotationItUsedToBe()
        {
            // The control for the test above. Both sides could agree because both were wrong the same
            // way — and they did, until ADR 0042, at a depth scale of 1. So: the frame the station places
            // in must differ VISIBLY from the pure rotation it used to be on the axis the squash touches,
            // agree exactly on the axis it does not, and invert exactly.
            const int facing = 0;                       // local +y straight up the screen: the worst case
            var backWall = new Vector2(0f, -4.1f);      // the C-store's back wall, in its own frame

            Vector2 placed = StationCatalog.LocalDirToWorld(backWall, facing);
            Vector2 rotatedOnly = StationCatalog.RightOf(facing) * backWall.x
                                + StationCatalog.ForwardOf(facing) * backWall.y;

            Assert.That(placed.x, Is.EqualTo(rotatedOnly.x).Within(1e-5f),
                "the across axis is never squashed");
            Assert.That(Vector2.Distance(placed, rotatedOnly),
                        Is.EqualTo(4.1f * (1f - SpriteLightMath.GroundDepthScale)).Within(1e-3f),
                "the back wall moves 1.46 m under the squash — the figure the interim's remark had as " +
                "'three metres' (2.93 m is the change in TOTAL depth, not the wall's move)");
            Assert.That(StationCatalog.DepthScale, Is.EqualTo(SpriteLightMath.GroundDepthScale),
                "one squash for every kit: the station's depth scale IS the shared bake camera's");

            var centre = new Vector2(-191f, 81.6f);
            for (int f = 0; f < StationCatalog.Facings; f++)
                foreach (Vector2 p in ProbePoints)
                {
                    Vector2 back = StationCatalog.WorldToLocal(
                        StationCatalog.LocalToWorld(p, centre, f), centre, f);
                    Assert.That(Vector2.Distance(back, p), Is.LessThan(1e-4f),
                        $"cell {f}: {p} did not survive the round trip ({back}) — the inverse the reach " +
                        "audit stands on is not an inverse");
                }
        }

        // ---- the guard this arc earned: the colliders fit the PICTURE -------------------------------

        /// <summary>Every ground footprint the piece publishes, projected at <paramref name="facing"/>
        /// about the piece's origin — the kit's own way (rotate, then squash) or, for the control,
        /// rotated only. By default ALL blockers, whether or not they stop a body — a step-over kerb is
        /// ground the art draws; <paramref name="collidersOnly"/> narrows it to the blockers that become
        /// colliders, which is what a body actually hits.</summary>
        static IEnumerable<Vector2> GroundFootprintAt(StationPieceDef def, int facing, bool squashed,
                                                      bool collidersOnly = false)
        {
            foreach (StationBlocker b in def.Blockers)
            {
                if (b == null || (collidersOnly && !b.Blocks)) continue;
                Vector2[] local = b.IsCircle ? StationCatalog.CirclePolygon(b.Center, b.Radius) : b.Footprint;
                if (local == null || local.Length < 3) continue;
                foreach (Vector2 p in local)
                    yield return squashed
                        ? StationCatalog.LocalDirToWorld(p, facing)
                        : StationCatalog.RightOf(facing) * p.x + StationCatalog.ForwardOf(facing) * p.y;
            }
        }

        /// <summary>How far a set of points pokes outside a cell (m); 0 when every one fits.</summary>
        static float OverflowOf(IEnumerable<Vector2> points, Bounds cell)
        {
            float worst = 0f;
            foreach (Vector2 p in points)
            {
                worst = Mathf.Max(worst, cell.min.x - p.x, p.x - cell.max.x);
                worst = Mathf.Max(worst, cell.min.y - p.y, p.y - cell.max.y);
            }
            return worst;
        }

        [Test]
        public void EveryPiecesColliders_FitInsideItsOwnDrawnCell_AtEveryFacing()
        {
            RequireKit();

            // The drawn cell is the sprite's own bounds: cell px ÷ PPU about the pivot, and the pivot is
            // the ground centre of the footprint (ADR 0026) — the same point the footprints are stated
            // from. One pixel of slack: the cells are cropped to ink, and a footprint corner can land on
            // an anti-aliased edge the rasteriser did not paint. Measured on the shipped kit the worst
            // collider is 0.4 px out (the vent risers at cell 0); every other one is exact.
            int piecesChecked = 0;
            foreach (var kv in StationCatalog.Defs())
            {
                StationPieceDef def = kv.Value;
                if (def == null || def.IsInterior || !def.HasArt) continue;

                bool any = false;
                for (int facing = 0; facing < StationCatalog.Facings; facing++)
                {
                    Sprite sprite = def.Frame(facing);
                    Assert.That(sprite, Is.Not.Null, $"{def.name} cell {facing} has no sprite");

                    var points = new List<Vector2>(
                        GroundFootprintAt(def, facing, squashed: true, collidersOnly: true));
                    if (points.Count == 0) continue;
                    any = true;

                    float overflow = OverflowOf(points, sprite.bounds);
                    Assert.That(overflow, Is.LessThanOrEqualTo(1f / sprite.pixelsPerUnit + 1e-4f),
                        $"{def.name} at cell {facing}: a collider, placed the kit's way, pokes " +
                        $"{overflow:0.###} m ({overflow * sprite.pixelsPerUnit:0.#} px) outside the drawn " +
                        $"cell {sprite.bounds.min}..{sprite.bounds.max}. A collider outside the picture " +
                        "is a wall you walk into in empty air — the defect ADR 0042 measured at Route 91.");
                }
                if (any) piecesChecked++;
            }

            Assert.That(piecesChecked, Is.GreaterThan(10),
                "fewer than eleven exterior pieces carry a collider — this test is passing vacuously");
        }

        [Test]
        public void TheIslandsAndTheStores_WholeGroundFootprint_FitsTheirDrawnCells_AtEveryFacing()
        {
            RequireKit();

            // The stronger claim, for the two families the ruling was measured on: EVERYTHING they
            // publish as ground — the step-over kerb that IS the island, the building plan and what is
            // bolted to it — fits the picture, at every cell. ⚠️ Not asserted kit-wide on purpose: the
            // small dispensers' `flat` plinths are drawn inside their published footprints and overhang
            // the ink by up to 3.6 px at the diagonals (kerb post, globe-top, cardlock). Nothing collides
            // with a plinth, so that is an art nuance rather than a placement fault; the test above is
            // the one that guards what a body hits.
            int piecesChecked = 0;
            foreach (var kv in StationCatalog.Defs())
            {
                StationPieceDef def = kv.Value;
                if (def == null || def.IsInterior || !def.HasArt) continue;
                if (def.PieceType != "island" && def.PieceType != "store") continue;

                for (int facing = 0; facing < StationCatalog.Facings; facing++)
                {
                    Sprite sprite = def.Frame(facing);
                    float overflow = OverflowOf(GroundFootprintAt(def, facing, squashed: true), sprite.bounds);
                    Assert.That(overflow, Is.LessThanOrEqualTo(1f / sprite.pixelsPerUnit + 1e-4f),
                        $"{def.name} at cell {facing}: the ground footprint pokes {overflow:0.###} m " +
                        $"({overflow * sprite.pixelsPerUnit:0.#} px) outside the drawn cell " +
                        $"{sprite.bounds.min}..{sprite.bounds.max}");
                }
                piecesChecked++;
            }

            Assert.That(piecesChecked, Is.GreaterThanOrEqualTo(6),
                "four islands and two storefronts — the kit ships six; fewer means this ran on nothing");
        }

        [Test]
        public void TheIslandAndTheCStore_WouldOverflowTheirCells_IfTheGroundWereNotSquashed()
        {
            RequireKit();

            // ⭐ THE CONTROL, and the test that would have caught the original defect on day one: the
            // kit's placement until ADR 0042 was the pure rotation, and at the SHIPPED facing it puts the
            // island kerb and the C-store's walls a metre outside the pictures they belong to.
            int facing = NineMileCreekStation.Route91Layout().Facing;

            foreach (string key in new[] { "island_s2", "store_sStore" })
            {
                StationPieceDef def = StationCatalog.Find(key);
                if (def == null) Assert.Ignore($"{key} is not installed");

                Sprite sprite = def.Frame(facing);
                float squashed = OverflowOf(GroundFootprintAt(def, facing, squashed: true), sprite.bounds);
                float flat = OverflowOf(GroundFootprintAt(def, facing, squashed: false), sprite.bounds);

                Assert.That(squashed, Is.LessThanOrEqualTo(1f / sprite.pixelsPerUnit + 1e-4f),
                    $"{key}: squashed, the footprint must fit — it overflows by {squashed:0.###} m");
                Assert.That(flat, Is.GreaterThan(0.5f),
                    $"{key} at cell {facing}: the UNSQUASHED footprint overflows the drawn cell by only " +
                    $"{flat:0.###} m ({flat * sprite.pixelsPerUnit:0} px). If it fits, the picture and " +
                    "the ground plane agree here and the alignment test above cannot tell the two " +
                    "placements apart. Measured on the shipped kit: 1.04 m (island), 1.07 m (store).");
            }
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

            // At cell 0 with the kit's own squash (ADR 0042) — the frame the walls are cut in. The probes
            // are MODEL-frame points projected the same way the walls were, so this asks about the wall
            // as it stands in the world, not about the plan on paper.
            var centre = Vector2.zero;
            var footprint = new InteriorFootprint(centre, plan.WidthMetres, plan.LengthMetres,
                                                  0, StationCatalog.Facings,
                                                  SpriteLightMath.GroundDepthScale,
                                                  plan.DoorOnPlusY ? 1f : -1f, plan.DoorAcrossMetres);

            Vector2[][] walls = footprint.WallQuads(plan.WallThicknessMetres, plan.DoorwayWidthMetres);

            float doorWallY = (plan.DoorOnPlusY ? 1f : -1f)
                            * (plan.LengthMetres * 0.5f - plan.WallThicknessMetres * 0.5f);

            // The threshold itself: a gap, or there is no way in.
            Vector2 threshold = footprint.ModelToWorld(new Vector2(plan.DoorAcrossMetres, doorWallY));
            Assert.That(InsideAny(walls, threshold), Is.False,
                "the doorway the sidecar publishes is filled in with wall — the room would be sealed");

            // …and a doorway's width further along the SAME wall: solid, or the front of the shop is a
            // hole. This is the half that a gap cut at the wall's centre would fail.
            Vector2 offToTheSide = footprint.ModelToWorld(
                new Vector2(plan.DoorAcrossMetres + plan.DoorwayWidthMetres, doorWallY));
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

        /// <summary>A wall with exactly one rectangular footprint and nothing bolted to it — the
        /// fixture the pen control needs, and deliberately not a piece out of the kit.</summary>
        StationPieceDef Slab(float half)
        {
            var def = ScriptableObject.CreateInstance<StationPieceDef>();
            def.name = "test_slab";
            _spawned.Add(def);
            def.Blockers = new[]
            {
                new StationBlocker
                {
                    Kind = "slab",
                    Level = "",                 // no level named reads as ground, as the shell's does
                    Treatment = "wall",         // only wall and waist_block stop a body
                    Footprint = new[]
                    {
                        new Vector2(-half, -half), new Vector2(half, -half),
                        new Vector2(half, half), new Vector2(-half, half),
                    },
                },
            };
            return def;
        }

        [Test]
        public void ACanWalledInOnEverySide_IsRefusedEvenThoughItsOwnSpotIsClear()
        {
            // The third check earns its keep here and only here: this point is on good ground and inside
            // nothing, and it is still no place for a can, because a body cannot get to it. Without the
            // standing-spot probe, Standable would say yes.
            // ⚠️ A SYNTHETIC SLAB, not a shipped storefront, and that is the point of the fixture.
            // Two earlier versions of this control used store_sStore as a wall and failed on its
            // BOLTED-ON blockers rather than on the code under test: the C-store carries `ice`,
            // `propane_cage` and `ladder_cage` outside its building plan, and at any offset that
            // puts the wall 0.3 m off the origin one of them reaches across the gap. The pen only
            // needs to be an arrangement where the centre is clear and everything around it is
            // blocked, so it is built from one clean rectangle and cannot drift when the kit is
            // re-baked.
            const float faceOff = 0.3f;     // clear of a can (0.22) and inside a body's first ring (0.44)
            const float slabHalf = 4f;      // wide enough to close the diagonals out to the full reach
            StationPieceDef slab = Slab(slabHalf);

            // ⚠️ The slabs stand where their DRAWN faces would be. A slab's depth projects through the
            // kit's own squash (ADR 0042), so the north and south faces are slabHalf × 0.643 from their
            // centres, not slabHalf; placed unsquashed they would stand 1.4 m further off, the pen would
            // have a gap a body fits through, and this control would fail for the wrong reason.
            float across = slabHalf + faceOff;
            float deep = StationCatalog.LocalDirToWorld(new Vector2(0f, slabHalf), 0).y + faceOff;

            var forecourt = new List<StationReachAudit.Placed>
            {
                new StationReachAudit.Placed(slab, new Vector2(across, 0f), 0, "east"),
                new StationReachAudit.Placed(slab, new Vector2(-across, 0f), 0, "west"),
                new StationReachAudit.Placed(slab, new Vector2(0f, deep), 0, "north"),
                new StationReachAudit.Placed(slab, new Vector2(0f, -deep), 0, "south"),
            };

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
