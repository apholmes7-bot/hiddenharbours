using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;                 // StationPieceDef / StationInteractable
using HiddenHarbours.Art.Editor;          // StationCatalog / StationReachAudit
using HiddenHarbours.Core;
using HiddenHarbours.Economy;             // FuelStationDef / FuelGrades
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>NINE MILE CREEK BUYS FUEL</b> — the wharf's two dock pedestals and the Route 91 forecourt,
    /// sited, turned, reached and wired. Sibling of <c>NineMileCreekShopsTests</c>, and here for the same
    /// reason: <b>a placement bug in this kit is SILENT</b>. A pump turned a quarter of a turn wrong still
    /// draws. A pump whose standing spot is out over the basin still draws, and the offer never appears.
    /// A forecourt eight metres into Route 91's carriageway draws perfectly.
    ///
    /// <para><b>⭐ THE LOAD-BEARING TESTS ARE THE THREE POSITIVE CONTROLS.</b> Every "it is clear" claim
    /// below is paired with a deliberate WRONG placement that must fail — because a clearance guard that
    /// has never seen a red is a guard that will start agreeing with whatever the layout happens to hold.
    /// That is the discipline <c>TheOldRestaurantLot_WouldHaveStoodOnTheQuay</c> established in this
    /// region, and it earned its keep here: the whole-scene reach pass PASSED a pedestal standing in
    /// mid-air until the dry-ground test was added, and the control is what found it.</para>
    ///
    /// <para>Every bound is DERIVED from <see cref="NineMileCreekMainland"/>, from the station Defs and
    /// from the site Defs, never copied out of any of them — this region's test convention, and what
    /// makes these checks rather than a second copy of the plan.</para>
    /// </summary>
    public class NineMileCreekStationTests
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

        static bool KitInstalled => StationCatalog.IsInstalled;

        static void RequireKit()
        {
            if (!KitInstalled)
                Assert.Ignore("the gas-station kit has not been baked/sliced/Def-built in this checkout");
        }

        // =============================================================================================
        //  1. THE KIT IS THERE — every piece both sites name resolves to a Def AND to a prefab
        // =============================================================================================

        static IEnumerable<string> EveryKeyBothSitesUse()
        {
            yield return "dispenser_sDock";
            foreach (var p in NineMileCreekStation.Route91Layout().Pieces) yield return p.Key;
        }

        [Test]
        public void EveryPieceTheStationNames_HasADefAndAPrefabAndBakedArt()
        {
            RequireKit();

            foreach (string key in EveryKeyBothSitesUse().Distinct())
            {
                StationPieceDef def = StationCatalog.Find(key);
                Assert.That(def, Is.Not.Null, $"no StationPieceDef named '{key}' — #613 builds them from " +
                                              "the bake contract, so a missing one means the kit is " +
                                              "half-installed rather than that the site is wrong");
                Assert.That(StationCatalog.Prefab(key), Is.Not.Null, $"no prefab for '{key}'");
                Assert.That(def.HasArt, Is.True, $"'{key}' has a Def but no baked frames");
            }
        }

        [Test]
        public void TheContractSaysClockwise_AndTheFacingArithmeticFollowsIt()
        {
            RequireKit();

            Assert.That(StationCatalog.IsClockwise, Is.True,
                "the gas-station kit is registered Clockwise in RigCatalog. If the contract now says " +
                "otherwise the ART changed, and every facing in NineMileCreekStation must be re-measured " +
                "rather than re-labelled.");

            // Cell i depicts heading +45i, on the piece's local +Y. Measured in the V8 harness through the
            // rig's own project(); re-stated here as the contract this code depends on.
            Assert.That(StationCatalog.HeadingOfFacing(0), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(StationCatalog.HeadingOfFacing(2), Is.EqualTo(90f).Within(1e-4f));
            Assert.That(StationCatalog.HeadingOfFacing(4), Is.EqualTo(180f).Within(1e-4f));
            Assert.That(StationCatalog.HeadingOfFacing(6), Is.EqualTo(270f).Within(1e-4f));

            Assert.That(StationCatalog.ForwardOf(2).x, Is.EqualTo(1f).Within(1e-4f), "+Y at cell 2 is EAST");
            Assert.That(StationCatalog.ForwardOf(2).y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(StationCatalog.RightOf(2).x, Is.EqualTo(0f).Within(1e-4f), "+X at cell 2 is SOUTH");
            Assert.That(StationCatalog.RightOf(2).y, Is.EqualTo(-1f).Within(1e-4f));
        }

        [Test]
        public void TheAzimuthConvention_AgreesWithTheRestOfTheRepo()
        {
            // StationCatalog.HeadingOf restates atan2(east, north) because HiddenHarbours.Art.Editor
            // cannot see IsoPackSprites or CliffWallGeometry. This is what holds the copy honest.
            Assert.That(StationCatalog.HeadingOf(Vector2.up), Is.EqualTo(0f).Within(1e-4f), "north");
            Assert.That(StationCatalog.HeadingOf(Vector2.right), Is.EqualTo(90f).Within(1e-4f), "east");
            Assert.That(StationCatalog.HeadingOf(Vector2.down), Is.EqualTo(180f).Within(1e-4f), "south");
            Assert.That(StationCatalog.HeadingOf(Vector2.left), Is.EqualTo(270f).Within(1e-4f), "west");
        }

        // =============================================================================================
        //  2. SITE A — the wharf pedestals
        // =============================================================================================

        [Test]
        public void TheWharfPumps_TurnTheirHosesOverTheWater_NotAlongTheWall()
        {
            RequireKit();

            var def = StationCatalog.Find("dispenser", "sDock");
            Assert.That(def.Interactables.Length, Is.EqualTo(1),
                "the dock pedestal is a ONE-HOSE machine — the whole reason it is the piece used here, " +
                "because a side with one hose has an unambiguous standing spot");

            int facing = NineMileCreekStation.WharfPumpFacing;
            Vector3 nozzleLocal = def.Interactables[0].Position;
            Vector2 hoseWorld = StationCatalog.LocalDirToWorld(
                new Vector2(nozzleLocal.x, nozzleLocal.y).normalized, facing);

            float bearing = StationCatalog.HeadingOf(hoseWorld);
            Assert.That(bearing, Is.EqualTo(NineMileCreekStation.ApronSeawardHeading).Within(0.01f),
                $"the pedestal's hose must bear {NineMileCreekStation.ApronSeawardHeading}° (the apron's " +
                "own seaward heading) and it bears " + bearing + "°. ⚠ A dispenser's business axis is " +
                "local +X, not +Y — FacingForHeading would be a quarter turn wrong here and would draw " +
                "perfectly.");

            // The POSITIVE CONTROL: the naive helper really is wrong, so this test cannot pass by luck.
            int naive = StationCatalog.FacingForHeading(NineMileCreekStation.ApronSeawardHeading);
            Assert.That(naive, Is.Not.EqualTo(facing),
                "if FacingForHeading and FacingForLocalDirection now agree for this piece, the pedestal's " +
                "nozzle has moved onto its +Y axis and this test is no longer measuring anything");
        }

        [Test]
        public void TheWharfPumps_StandOnTheDeck_ClearOfTheQuayWall()
        {
            RequireKit();
            var terrain = MakeCreekTerrain();

            float wallBand = PropertyBoundary.DefaultThicknessMetres * 0.5f
                           + StationReachAudit.BodyRadiusMetres;
            var def = StationCatalog.Find("dispenser", "sDock");
            float reachOut = Mathf.Abs(def.Interactables[0].ReachPoint.x);

            foreach (Vector2 at in NineMileCreekStation.WharfPumpPositions)
            {
                float standX = at.x + reachOut;                       // where the body actually stands
                float lip = NineMileCreekStation.Apron.xMax;

                Assert.That(standX, Is.LessThan(lip - wallBand),
                    $"a body using the pump at {at} stands at x={standX:0.000}, which is inside the quay " +
                    $"wall band (lip {lip}, half-band {wallBand:0.000}). The pump must stand further back.");

                Assert.That(terrain.ElevationAt(new Vector2(standX, at.y)),
                            Is.GreaterThanOrEqualTo(NineMileCreekMainland.SpringHighWater),
                    "…and it must be dry at spring high water, or the standing spot is only there for " +
                    "half the day");

                Assert.That(at.x, Is.GreaterThan(NineMileCreekStation.Apron.xMin),
                    "the pedestal itself must be on the apron");
            }
        }

        [Test]
        public void TheWharfPumps_ClearTheBrowTheLadderTheBerthsAndTheSlipway()
        {
            RequireKit();

            Vector2 brow = NineMileCreekMainland.GangwayShoreEnd;
            float slipwaySouth = NineMileCreekMainland.SlipwayHeadPos.y
                               - NineMileCreekMainland.SlipwayHalfWidthMetres;
            float slipwayNorth = NineMileCreekMainland.SlipwayHeadPos.y
                               + NineMileCreekMainland.SlipwayHalfWidthMetres;

            var ladder = NineMileCreekWharf.Fittings().First(f => f.Name == "ladder");

            foreach (Vector2 at in NineMileCreekStation.WharfPumpPositions)
            {
                Assert.That(Vector2.Distance(at, brow),
                            Is.GreaterThanOrEqualTo(NineMileCreekStation.WharfPumpBrowClearMetres - 0.01f),
                    "a pump must not be something you squeeze past walking down to the float");

                Assert.That(at.y, Is.GreaterThan(slipwayNorth),
                    $"the pumps must stay north of the slipway ({slipwaySouth}…{slipwayNorth})");

                Assert.That(Vector2.Distance(at, ladder.Position), Is.GreaterThan(10f),
                    "…and nowhere near the ladder, which is how you get down to a punt");

                for (int i = 0; i < NineMileCreekWharf.BerthCount; i++)
                    Assert.That(Vector2.Distance(at, NineMileCreekWharf.BerthPos(i)), Is.GreaterThan(5f),
                        $"a pump must not crowd berth {i}");
            }

            // …and the two do not crowd each other: FuelPump reaches 2 m, so 4 m apart means standing at
            // one you are never offered the other.
            var pumps = NineMileCreekStation.WharfPumpPositions;
            Assert.That(Vector2.Distance(pumps[0], pumps[1]), Is.GreaterThanOrEqualTo(3.99f));
        }

        [Test]
        public void AHullAlongsideEitherWharfPump_IsInTheDredgedChannelAtSpringLow()
        {
            RequireKit();

            // The claim the site rests on: "alongside the wharf" is not everywhere a wet berth here — the
            // basin bares at spring low and only the channel keeps its water. A hull lying half a beam off
            // this face must be IN it.
            float halfBeam = NineMileCreekMainland.WidestResidentBeamMetres * 0.5f;
            Vector2[] channel = NineMileCreekMainland.ChannelWaypoints;
            float half = NineMileCreekMainland.ChannelHalfWidthMetres;

            foreach (Vector2 at in NineMileCreekStation.WharfPumpPositions)
            {
                var alongside = new Vector2(NineMileCreekStation.Apron.xMax + halfBeam, at.y);
                float d = MainlandTidalTerrain.DistanceToPolyline(channel, alongside);

                Assert.That(d, Is.LessThan(half),
                    $"a hull lying alongside the pump at {at} sits {d:0.00} m off the channel's own line " +
                    $"(half-width {half} m) — outside the water that survives spring low. Either move the " +
                    "pump, or say in the PR that this hose serves a boat only above half tide.");
            }
        }

        [Test]
        public void TheWharfPumpsHoseReaches_AHullLyingAlongside()
        {
            RequireKit();

            var def = StationCatalog.Find("dispenser", "sDock");
            float halfBeam = NineMileCreekMainland.WidestResidentBeamMetres * 0.5f;
            float nozzleOut = Mathf.Abs(def.Interactables[0].Position.x);

            foreach (Vector2 at in NineMileCreekStation.WharfPumpPositions)
            {
                float nozzleX = at.x + nozzleOut;
                float hullCentreX = NineMileCreekStation.Apron.xMax + halfBeam;
                float run = hullCentreX - nozzleX;

                Assert.That(run, Is.LessThanOrEqualTo(def.HoseReachMeters),
                    $"the hose is {def.HoseReachMeters} m and the widest resident's centreline is " +
                    $"{run:0.00} m from the nozzle. The hose is never baked — StationIso.serve() measures " +
                    "honest metres — so this is the number that decides whether a fill is possible.");
            }
        }

        // =============================================================================================
        //  3. SITE B — the Route 91 forecourt
        // =============================================================================================

        [Test]
        public void TheForecourtSitsAtTheJunctionTheOwnerNamed()
        {
            // Both polylines carry the junction as the SAME vertex — that is what makes it a junction, and
            // it is read rather than restated.
            Vector2 fromWharfRoad = NineMileCreekMainland.WharfRoad[0];
            Assert.That(NineMileCreekMainland.ThroughRoad.Any(v => (v - fromWharfRoad).sqrMagnitude < 1e-6f),
                Is.True, "Wharf Road's landward end must be a vertex of the through-road, or 'the wharf " +
                         "road's intersection with Route 91' is not a point this region has");

            float toJunction = Vector2.Distance(NineMileCreekStation.Route91ForecourtPos,
                                                NineMileCreekStation.Route91Junction);
            Assert.That(toJunction, Is.LessThan(25f),
                $"the station is {toJunction:0.0} m from the junction. Past about 25 m it stops reading " +
                "as 'at the intersection' and starts reading as 'up the road', which is a different site.");
        }

        [Test]
        public void TheForecourtsApronMeetsRoute91_WithoutStandingInIt()
        {
            Rect extent = NineMileCreekStation.Route91Extent();
            Vector2[] road = NineMileCreekMainland.ThroughRoad;
            float corridor = NineMileCreekMainland.RoadHalfWidth;

            float nearest = float.MaxValue;
            foreach (Vector2 p in EdgeSamples(extent))
                nearest = Mathf.Min(nearest, NineMileCreekMainland.DistanceToRoute(road, p));

            Assert.That(nearest, Is.GreaterThan(corridor),
                "no part of the forecourt may stand in Route 91's cleared corridor");
            Assert.That(nearest - corridor, Is.LessThan(2.5f),
                $"…and it must MEET the road: the nearest edge is {nearest - corridor:0.00} m outside the " +
                "corridor. A forecourt you cannot drive onto is a shed in a field.");
        }

        [Test]
        public void TheForecourtClearsEveryClaimTheVillageMakes()
        {
            var offenders = new List<string>();
            float worst = float.MaxValue;
            string worstName = "";

            foreach (var (name, slack) in VillageClearances(NineMileCreekStation.Route91Extent()))
            {
                if (slack < 0f) offenders.Add($"{name} ({slack:0.00} m)");
                if (slack < worst) { worst = slack; worstName = name; }
            }

            Assert.That(offenders, Is.Empty,
                "the forecourt overlaps: " + string.Join(", ", offenders));

            Assert.That(worst, Is.GreaterThan(1f),
                $"the tightest clearance is {worst:0.00} m to {worstName}. Under a metre this is the fish " +
                "market's problem all over again — legal on paper and wrong on the ground.");
        }

        /// <summary>⭐ THE POSITIVE CONTROL for the test above: the same measurement, on a placement that
        /// is deliberately wrong, must come back negative. Without this the clearance guard would pass
        /// whatever the layout happened to hold.</summary>
        [Test]
        public void AForecourtShovedAtTheRoad_WouldFailTheSameClearanceTest()
        {
            Rect good = NineMileCreekStation.Route91Extent();
            var shoved = new Rect(good.x + 8f, good.y, good.width, good.height);

            bool anyNegative = VillageClearances(shoved).Any(c => c.slack < 0f);
            Assert.That(anyNegative, Is.True,
                "a forecourt shoved eight metres east — into Route 91 — must FAIL the clearance sweep. " +
                "If it passes, the sweep is not measuring the roads and the green above is worthless.");
        }

        [Test]
        public void TheForecourtsLayoutIsTheOneTheRigProduced()
        {
            RequireKit();
            var layout = NineMileCreekStation.Route91Layout();

            Assert.That(layout.Facing, Is.EqualTo(2),
                "the road side looks EAST onto Route 91, which is cell 2 under this kit's clockwise " +
                "convention");

            // The island is the origin — plan()'s own frame — and the two slots are the ISLAND's own,
            // re-derived from the Def rather than restated.
            var island = StationCatalog.Find("island", "s2");
            float pitch = island.SlotPitchMeters;

            var dispensers = layout.Pieces.Where(p => p.Key == "dispenser_sMpd").ToList();
            Assert.That(dispensers.Count, Is.EqualTo(island.Slots),
                "one machine per bolt-down slot");
            Assert.That(Mathf.Abs(dispensers[0].Local.x - dispensers[1].Local.x),
                        Is.EqualTo(pitch).Within(0.001f),
                $"the machines must sit at the island's own {pitch} m slot pitch");
            Assert.That(dispensers.Sum(d => d.Local.x), Is.EqualTo(0f).Within(0.001f),
                "…straddling the island's centre");

            // The sign is on the ROAD side and the store is behind: plan()'s one rule about which way a
            // forecourt faces, checked rather than assumed.
            var sign = layout.Pieces.Single(p => p.Key == "sign_sPylon");
            var store = layout.Pieces.Single(p => p.Key == "store_sStore");
            Assert.That(sign.Local.y, Is.GreaterThan(0f), "the price sign reads from the road (+y)");
            Assert.That(store.Local.y, Is.LessThan(0f), "the store is on the building side (−y)");

            // The seamless interior shares the shell's cell AND pivot exactly (ADR 0036).
            var room = layout.Pieces.Single(p => p.Key == "interior_sStore");
            Assert.That(room.Local, Is.EqualTo(store.Local),
                "the sales floor paints INTO the storefront's own cell — same position, same pivot. Any " +
                "offset at all and the seamless interior stops registering.");
            Assert.That(room.Facing, Is.EqualTo(store.Facing));
        }

        [Test]
        public void ThePavedForecourtIsInsideTheStationsOwnExtent_AndStopsAtTheStore()
        {
            RequireKit();

            Rect apron = NineMileCreekStation.Route91ApronArea();
            Rect extent = NineMileCreekStation.Route91Extent();

            // ⚠️ Bounds, not Rect.Contains: the apron shares three of the extent's four edges, and
            // Rect.Contains is half-open (x < xMax), so a shared MAX edge reads as outside. That is a
            // property of the API rather than of the placement, and it failed this test once already.
            Assert.That(apron.xMin, Is.GreaterThanOrEqualTo(extent.xMin - 1e-3f));
            Assert.That(apron.yMin, Is.GreaterThanOrEqualTo(extent.yMin - 1e-3f));
            Assert.That(apron.xMax, Is.LessThanOrEqualTo(extent.xMax + 1e-3f));
            Assert.That(apron.yMax, Is.LessThanOrEqualTo(extent.yMax + 1e-3f));
            Assert.That(apron.width * apron.height, Is.LessThan(extent.width * extent.height),
                "only the DRIVING half is paved — paving under a building is pointless");

            // …and the region's own pad table carries it, which is what makes the meadow step off it.
            var pad = NineMileCreekRoads.Pads()
                                        .FirstOrDefault(p => p.Name == NineMileCreekRoads.StationForecourtName);
            Assert.That(pad.Name, Is.EqualTo(NineMileCreekRoads.StationForecourtName),
                "the forecourt must be in NineMileCreekRoads.Pads(), or NineMileCreekFields.IsGrassGround " +
                "will grow the meadow up between the pumps");
            Assert.That(pad.Area, Is.EqualTo(apron));
        }

        // =============================================================================================
        //  4. THE WHOLE-SCENE REACH PASS — the one the kit's README says is owed
        // =============================================================================================

        [Test]
        public void EveryFittingAtBothSites_HasSomewhereToStand()
        {
            RequireKit();
            var terrain = MakeCreekTerrain();
            var blocks = NineMileCreekStation.SceneObstructions(terrain);

            foreach (var (site, placed) in BothSites())
            {
                List<StationReachAudit.Result> results = StationReachAudit.Audit(placed, blocks);
                Assert.That(results, Is.Not.Empty, $"{site} audited nothing");

                var stranded = results.Where(r => !r.Usable).ToList();
                Assert.That(stranded, Is.Empty,
                    $"{site}: {stranded.Count} fitting(s) with nowhere to stand — " +
                    string.Join(" · ", stranded.Select(r => r.ToString())) +
                    ". A siting fault, not a note: move the piece rather than shrinking the body.");
            }
        }

        /// <summary>⭐⭐ THE POSITIVE CONTROL THAT FOUND A REAL GAP. With only the wall and road tests, a
        /// pedestal standing at the apron's lip PASSED — its standing spot is 1.06 m out over the basin,
        /// inside no blocker and nowhere a body can be. The dry-ground test in
        /// <see cref="NineMileCreekStation.SceneObstructions"/> exists because of this control.</summary>
        [Test]
        public void APumpSetAtTheVeryLip_IsCaughtByTheWholeScenePass()
        {
            RequireKit();
            var terrain = MakeCreekTerrain();
            var blocks = NineMileCreekStation.SceneObstructions(terrain);
            var def = StationCatalog.Find("dispenser", "sDock");

            var atTheLip = new List<StationReachAudit.Placed>
            {
                new StationReachAudit.Placed(
                    def,
                    new Vector2(NineMileCreekStation.Apron.xMax, NineMileCreekStation.WharfPumpPositions[0].y),
                    NineMileCreekStation.WharfPumpFacing, "control_at_the_lip"),
            };

            var results = StationReachAudit.Audit(atTheLip, blocks);
            Assert.That(results.Any(r => r.Outcome != StationReachAudit.Verdict.Ok), Is.True,
                "a pedestal set at the very lip publishes a standing spot out over the basin. If the " +
                "whole-scene pass calls that OK it is not testing whether there is any ground to stand " +
                "on, and every green above is worth nothing.");
        }

        /// <summary>…and the CROSS-PIECE arm of the same control: the bake's audit is blind to it by
        /// construction, so if this passes the pass is only re-running the per-piece one.</summary>
        [Test]
        public void OnePieceStoodOnAnothersStandingSpot_IsCaughtByTheWholeScenePass()
        {
            RequireKit();

            var cluster = StationCatalog.Find("fillport", "sCluster");
            var vent = StationCatalog.Find("fillport", "sVent");
            Assert.That(cluster.Interactables.Length, Is.GreaterThan(0));

            // Stand the risers exactly on the cluster's first published standing spot.
            Vector3 spot = cluster.Interactables[0].ReachPoint;
            var stacked = new List<StationReachAudit.Placed>
            {
                new StationReachAudit.Placed(cluster, Vector2.zero, 0, "fillport_sCluster"),
                new StationReachAudit.Placed(vent, new Vector2(spot.x, spot.y), 0, "fillport_sVent"),
            };

            var results = StationReachAudit.Audit(stacked);
            var moved = results.Where(r => r.Outcome != StationReachAudit.Verdict.Ok).ToList();

            Assert.That(moved, Is.Not.Empty,
                "the bake audits ONE piece at a time, so a fitting whose standing spot is inside a " +
                "DIFFERENT piece is invisible to it. That is the entire reason this pass exists — if it " +
                "cannot see this, it is only re-running the per-piece audit.");
            Assert.That(moved.Any(r => r.BlockedBy.StartsWith("fillport_sVent")), Is.True,
                "…and it must name the piece that was in the way");
        }

        // =============================================================================================
        //  5. THE BAND (ADR 0032)
        // =============================================================================================

        [Test]
        public void EveryStationPiece_SortsInsideTheBandRatherThanSaturatingIt()
        {
            RequireKit();

            foreach (var (site, placed) in BothSites())
                foreach (StationReachAudit.Placed p in placed)
                {
                    int order = StationForecourt.SortingOrderAt(p.Origin.y);
                    Assert.That(StationForecourt.BandResolves(p.Origin.y), Is.True,
                        $"{site}/{p.Name} at y={p.Origin.y} sorts at {order}, which is the band's own " +
                        $"floor or ceiling ({SortingBands.DecorFloor}…{SortingBands.DecorCeiling}). A " +
                        "saturated piece interleaves with everything around it by draw order rather than " +
                        "by position — the defect ADR 0032 exists to prevent.");
                }
        }

        [Test]
        public void TheSalesFloorSortsUnderItsOwnShell()
        {
            RequireKit();

            var shell = StationCatalog.Prefab("store_sStore");
            var room = StationCatalog.Prefab("interior_sStore");
            Assert.That(shell, Is.Not.Null);
            Assert.That(room, Is.Not.Null);

            float Base(GameObject go)
            {
                var so = new UnityEditor.SerializedObject(go.GetComponent<YSortSprite>());
                return so.FindProperty("_baseOrder").floatValue;
            }

            Assert.That(Base(room), Is.LessThan(Base(shell)),
                "the room and its shell occupy the SAME cell at the SAME world position, so nothing about " +
                "their positions can separate them. The offset must live in _baseOrder — a hand-set " +
                "sortingOrder is overwritten the moment YSortSprite.OnEnable runs, and the floor then " +
                "draws over its own walls.");
        }

        // =============================================================================================
        //  6. THE WIRING — grades, prices, and the owner's actual ask
        // =============================================================================================

        [Test]
        public void BothSitesHaveTheirDef_AndSellEveryGradeTheyAreWiredFor()
        {
            var wharf = NineMileCreekStation.LoadStation(NineMileCreekStation.WharfStationId);
            var road = NineMileCreekStation.LoadStation(NineMileCreekStation.RoadStationId);

            Assert.That(wharf, Is.Not.Null, "no FuelStationDef with id " + NineMileCreekStation.WharfStationId);
            Assert.That(road, Is.Not.Null, "no FuelStationDef with id " + NineMileCreekStation.RoadStationId);

            foreach (string g in NineMileCreekStation.WharfPumpGrades)
                Assert.That(wharf.Sells(g), Is.True,
                    $"a wharf hose is wired for '{g}' but the site Def does not sell it — the hose would " +
                    "refuse every press");

            foreach (string g in NineMileCreekStation.Route91FaceGrades.Distinct())
                Assert.That(road.Sells(g), Is.True,
                    $"a Route 91 face is wired for '{g}' but the site Def does not sell it");
        }

        [Test]
        public void TheWharfChargesMoreThanTheRoad_WhichIsTheWholePointOfHavingTwo()
        {
            var wharf = NineMileCreekStation.LoadStation(NineMileCreekStation.WharfStationId);
            var road = NineMileCreekStation.LoadStation(NineMileCreekStation.RoadStationId);
            if (wharf == null || road == null) Assert.Ignore("site Defs not present");

            foreach (string g in NineMileCreekStation.WharfPumpGrades)
                Assert.That(wharf.PricePerLitre(g), Is.GreaterThan(road.PricePerLitre(g)),
                    $"the owner's ask was 'a couple fuel pumps at NMC wharf which charge a HIGHER price'. " +
                    $"{g}: wharf {wharf.PricePerLitre(g):0.00}, road {road.PricePerLitre(g):0.00}.");
        }

        [Test]
        public void OneHosePerFace_BecauseAMultiProductMachinesNozzlesShareAStandingSpot()
        {
            RequireKit();

            var mpd = StationCatalog.Find("dispenser", "sMpd");
            var sideA = mpd.Interactables.Where(i => i.Action == "fuel" && i.Id.StartsWith("nozzle_a"))
                                         .ToList();
            Assert.That(sideA.Count, Is.GreaterThan(1), "the multi-product machine has several hoses a side");

            foreach (var n in sideA)
                Assert.That(new Vector2(n.ReachPoint.x, n.ReachPoint.y),
                            Is.EqualTo(new Vector2(sideA[0].ReachPoint.x, sideA[0].ReachPoint.y)),
                    "⚠ THE FINDING THIS TEST PINS: every nozzle on one side publishes the SAME ground " +
                    "reach point and differs only in Z. InteractResolver's order is priority, then " +
                    "distance, then LOWEST ID — so several FuelPumps there would hand every press to one " +
                    "of them forever. If this ever stops being true, the station may wire a pump per " +
                    "NOZZLE and the player can pick a grade at one machine.");

            Assert.That(NineMileCreekStation.Route91FaceGrades.Length,
                        Is.EqualTo(2 * NineMileCreekStation.Route91Layout()
                                            .Pieces.Count(p => p.Key == "dispenser_sMpd")),
                "one hose per dispenser SIDE, two sides per machine");

            Assert.That(NineMileCreekStation.Route91FaceGrades.Distinct().Count(),
                        Is.EqualTo(StationCatalog.Find("dispenser", "sMpd").BakedGrades.Length),
                "…and between them the faces must cover every grade the ART was baked with, or a grade " +
                "is drawn on the machine and unreachable by the player");
        }

        // =============================================================================================
        //  7. THE BUILD — what actually lands in the scene
        // =============================================================================================

        [Test]
        public void ThePlacedStation_HangsAHoseOnEveryFace_AndAnAnchorOnEveryThingForSale()
        {
            RequireKit();
            var terrain = MakeCreekTerrain();

            int placed = NineMileCreekStation.Place(terrain);
            var root = GameObject.Find(NineMileCreekStation.RootName);
            Assert.That(root, Is.Not.Null, "nothing was built");
            _spawned.Add(root);

            Assert.That(placed, Is.EqualTo(2 + NineMileCreekStation.Route91Layout().Pieces.Count),
                "two pedestals plus every piece of the forecourt");

            // The hoses: one per wharf pedestal, one per forecourt dispenser SIDE, each pointed at its
            // own site and grade.
            var pumps = root.GetComponentsInChildren<FuelPump>(includeInactive: true);
            Assert.That(pumps.Length,
                        Is.EqualTo(NineMileCreekStation.WharfPumpGrades.Length +
                                   NineMileCreekStation.Route91FaceGrades.Length));

            Assert.That(pumps.Select(p => p.Id).Distinct().Count(), Is.EqualTo(pumps.Length),
                "⚠ two pumps sharing an interact id make InteractResolver's order non-total — the one " +
                "hole in its tie-break, and an authoring error rather than a tie");

            foreach (FuelPump p in pumps)
            {
                Assert.That(p.Station, Is.Not.Null, $"'{p.Id}' points at no site");
                Assert.That(p.Offer.Available, Is.True,
                    $"'{p.Id}' dispenses '{p.Grade}', which {p.Station.Id} does not sell — it would " +
                    "refuse every press");
            }

            var wharfPumps = pumps.Where(p => p.Station.Id == NineMileCreekStation.WharfStationId).ToList();
            var roadPumps = pumps.Where(p => p.Station.Id == NineMileCreekStation.RoadStationId).ToList();
            Assert.That(wharfPumps.Select(p => p.Grade).OrderBy(g => g),
                        Is.EqualTo(NineMileCreekStation.WharfPumpGrades.OrderBy(g => g)));
            Assert.That(roadPumps.Select(p => p.Grade).OrderBy(g => g),
                        Is.EqualTo(NineMileCreekStation.Route91FaceGrades.OrderBy(g => g)));

            // …and the things with NO verb still get their measured standing spot, which is what "placed
            // and measured, not wired" means on the ground.
            var anchors = root.GetComponentsInChildren<Transform>(includeInactive: true)
                              .Where(t => t.name.StartsWith(StationForecourt.FittingPrefix))
                              .Select(t => t.name)
                              .ToList();

            foreach (string id in new[] { "till", "cooler", "coffee", "snacks", "hotcase", "atm" })
                Assert.That(anchors.Any(n => n.Contains(id)), Is.True,
                    $"the C-store's '{id}' has no anchor. Its VERB does not exist yet (#613 recorded " +
                    "that), but the standing spot the whole-scene pass verified should still be " +
                    "something the owner can select.");
        }

        // =============================================================================================
        //  HELPERS
        // =============================================================================================

        IEnumerable<(string site, List<StationReachAudit.Placed> placed)> BothSites()
        {
            var def = StationCatalog.Find("dispenser", "sDock");
            var wharf = NineMileCreekStation.WharfPumpPositions
                .Select(at => new StationReachAudit.Placed(def, at, NineMileCreekStation.WharfPumpFacing,
                                                           "dispenser_sDock"))
                .ToList();

            yield return ("the wharf pumps", wharf);
            yield return ("the Route 91 station", NineMileCreekStation.Route91Layout().AsPlaced());
        }

        static IEnumerable<Vector2> EdgeSamples(Rect r, int perEdge = 24)
        {
            for (int i = 0; i <= perEdge; i++)
            {
                float t = i / (float)perEdge;
                yield return new Vector2(Mathf.Lerp(r.xMin, r.xMax, t), r.yMin);
                yield return new Vector2(Mathf.Lerp(r.xMin, r.xMax, t), r.yMax);
                yield return new Vector2(r.xMin, Mathf.Lerp(r.yMin, r.yMax, t));
                yield return new Vector2(r.xMax, Mathf.Lerp(r.yMin, r.yMax, t));
            }
        }

        /// <summary>Every claim the village makes on this ground, as a signed slack in metres. Negative
        /// means the rectangle overlaps it.</summary>
        static IEnumerable<(string name, float slack)> VillageClearances(Rect footprint)
        {
            var samples = EdgeSamples(footprint).ToList();

            foreach (var (name, route) in new[]
                     {
                         ("Wharf Road", NineMileCreekMainland.WharfRoad),
                         ("Route 91", NineMileCreekMainland.ThroughRoad),
                         ("the bar road", NineMileCreekMainland.BarRoad),
                     })
            {
                float d = samples.Min(p => NineMileCreekMainland.DistanceToRoute(route, p));
                yield return (name, d - NineMileCreekMainland.RoadHalfWidth);
            }

            for (int i = 0; i < NineMileCreekMainland.TownLots.Length; i++)
            {
                Vector3 lot = NineMileCreekMainland.TownLots[i];
                float d = DistanceFromRect(footprint, new Vector2(lot.x, lot.y));
                yield return ($"town lot {i}", d - NineMileCreekMainland.TownLotRadius);
            }

            foreach (var yard in NineMileCreekYards.Yards)
            {
                float d = samples.Min(p => yard.Polygon.Contains(p) ? -1f : yard.Polygon.DistanceToEdge(p));
                yield return ($"{yard.Name}'s yard", d);
            }

            yield return ("the truck park",
                          DistanceFromRect(footprint,
                                           new Vector2(NineMileCreekMainland.TruckParkPos.x,
                                                       NineMileCreekMainland.TruckParkPos.y)) - 8f);
        }

        static float DistanceFromRect(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
