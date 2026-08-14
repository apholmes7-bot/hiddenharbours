using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE ROADS OF NINE MILE CREEK — do the tiles ride the routes the plan published?</b>
    ///
    /// <para>The load-bearing test here is <see cref="EveryRoadsCentreLineIsPavedEndToEnd"/>, and it is the
    /// tile-level descendant of a defect this region has already shipped once: the first draft of the bar
    /// road had dry NODES and ran straight through the marsh pool in between, which
    /// <c>NineMileCreekMainlandTerrainTests.EveryRoadStaysAboveTheHighestWaterAlongItsWholeLength</c> was
    /// written to catch. That test still guards the ROUTES; this file guards the CELLS, which is a
    /// different question now that a carriageway is 5 m wide and can lose ground its centre-line never
    /// touched.</para>
    ///
    /// <para>The terrain is built through the SAME configure call the builder uses
    /// (<see cref="NineMileCreekMainland.ConfigureTerrain"/>), so a test can never assert a road the scene
    /// does not carry. Nothing here loads an asset: the paving is a pure function of the published plan
    /// plus that terrain, which is why it can be asserted headlessly at all.</para>
    /// </summary>
    public class NineMileCreekRoadsTests
    {
        private GameObject _terrainGo;
        private MainlandTidalTerrain _terrain;
        private NineMileCreekRoads.Paving _paving;

        // Resolved once. The per-cell sweeps below run over thousands of cells, and re-building the
        // network inside the loop would rebuild the town walks (and re-project every lot onto every road)
        // once per square metre of carriageway.
        private Dictionary<string, NineMileCreekRoads.Way> _ways;
        private Dictionary<string, NineMileCreekRoads.Pad> _pads;

        private static float SpringHigh => NineMileCreekMainland.SpringHighWater;

        [SetUp]
        public void SetUp()
        {
            _terrainGo = new GameObject("NineMileCreekMainland_RoadTest");
            _terrain = _terrainGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(_terrain);
            _paving = NineMileCreekRoads.Pave(_terrain);

            _ways = new Dictionary<string, NineMileCreekRoads.Way>();
            foreach (var way in NineMileCreekRoads.Ways()) _ways[way.Name] = way;
            _pads = new Dictionary<string, NineMileCreekRoads.Pad>();
            foreach (var pad in NineMileCreekRoads.Pads()) _pads[pad.Name] = pad;
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            GameServices.Reset();
        }

        // =============================================================================================
        //  1. THE NETWORK IS CANON'S, AND THE SURFACES ARE THE KIT'S
        // =============================================================================================

        [Test]
        public void EverySurfaceThisRegionPavesIsOneTheKitActuallyBakes()
        {
            var kit = new List<string>(RoadKitV3.Surfaces);

            foreach (var way in NineMileCreekRoads.Ways())
                Assert.That(kit, Contains.Item(way.Surface),
                    $"{way.Name} is paved in '{way.Surface}', which the road kit v3 does not bake. Its " +
                    $"eleven are: {string.Join(", ", RoadKitV3.Surfaces)}.");

            foreach (var pad in NineMileCreekRoads.Pads())
                Assert.That(kit, Contains.Item(pad.Surface),
                    $"{pad.Name} is paved in '{pad.Surface}', which the road kit v3 does not bake.");
        }

        [Test]
        public void TheCanonFourAreLaidOnTheRoutesTheGeographyFilePublishes()
        {
            // ⭐ REFERENCE equality, not value equality. The point is that this pass reads the plan's own
            // arrays rather than a copy of their numbers — a copy would pass a value check and then drift
            // silently the first time a road was re-sited.
            Assert.That(Way(NineMileCreekRoads.BarRoadName).Route,
                Is.SameAs(NineMileCreekMainland.BarRoad));
            Assert.That(Way(NineMileCreekRoads.WharfRoadName).Route,
                Is.SameAs(NineMileCreekMainland.WharfRoad));
            Assert.That(Way(NineMileCreekRoads.ThroughRoadName).Route,
                Is.SameAs(NineMileCreekMainland.ThroughRoad));
            Assert.That(Way(NineMileCreekRoads.GullyPathName).Route,
                Is.SameAs(NineMileCreekMainland.GullyPath));
        }

        [Test]
        public void TheSurfacesAreTheOnesCanonNames()
        {
            // Mainland doc §1.2: the bar road (dirt) · Wharf Road (gravel) · the through-road (gravel) ·
            // the gully path (foot). Wharf doc §3: a concrete apron at the winch, gravel for the yard.
            Assert.That(Way(NineMileCreekRoads.BarRoadName).Surface,
                Is.EqualTo(NineMileCreekRoads.DirtSurface), "the bar road is red dirt in canon");
            Assert.That(Way(NineMileCreekRoads.WharfRoadName).Surface,
                Is.EqualTo(NineMileCreekRoads.GravelSurface), "Wharf Road is gravel in canon");
            Assert.That(Way(NineMileCreekRoads.ThroughRoadName).Surface,
                Is.EqualTo(NineMileCreekRoads.GravelSurface), "the through-road is gravel in canon");

            Assert.That(Way(NineMileCreekRoads.GullyPathName).HalfWidth,
                Is.EqualTo(NineMileCreekRoads.FootpathHalfWidthMetres).Within(1e-4f),
                "the gully path is a FOOT path — it must be laid at the footpath width, not a road's");

            Assert.That(Pad("WinchApron").Surface, Is.EqualTo(NineMileCreekRoads.ApronSurface));
            Assert.That(Pad("BuyersParking").Surface, Is.EqualTo(NineMileCreekRoads.GravelSurface));
        }

        [Test]
        public void TheDirtRoadIsNarrowerThanTheGravelOnes()
        {
            // Not decoration: the surfaces differ because the roads differ, and a 4 m red-dirt track and a
            // 5 m gravel road drawn at the same width would read as one road painted two colours.
            Assert.That(NineMileCreekRoads.DirtRoadHalfWidthMetres,
                Is.LessThan(NineMileCreekRoads.CarriagewayHalfWidthMetres));
            Assert.That(NineMileCreekRoads.FootpathHalfWidthMetres,
                Is.LessThan(NineMileCreekRoads.DirtRoadHalfWidthMetres));
        }

        // =============================================================================================
        //  2. THE CORRIDOR — the tiles go INSIDE the ground the plan already reserves
        // =============================================================================================

        [Test]
        public void EveryCarriagewayFitsInsideTheCorridorThePlanReserves()
        {
            // NineMileCreekMainland.RoadHalfWidth is the CLEARED corridor — "nothing may be sited inside
            // it" — and it is what the town-lot test, the dressing tests and the pole line all measure
            // against. A carriageway wider than its own corridor would pave ground another pass believes
            // it is allowed to stand a prop on.
            foreach (var way in NineMileCreekRoads.Ways())
                Assert.That(way.HalfWidth,
                    Is.LessThanOrEqualTo(NineMileCreekMainland.RoadHalfWidth + 1e-4f),
                    $"{way.Name} is laid {way.HalfWidth} m either side of its centre-line, wider than " +
                    $"the {NineMileCreekMainland.RoadHalfWidth} m corridor the plan clears for it");
        }

        [Test]
        public void EveryPavedCellRidesTheWayThatClaimedIt()
        {
            // ⭐ THE "DERIVE, NEVER EYEBALL" TEST. Every cell has to be explicable by the way that owns
            // it — within its half-width of its published route, or inside its published pad. A cell that
            // cannot be explained is a cell somebody placed.
            foreach (var cell in _paving.Cells)
            {
                if (_ways.TryGetValue(cell.Way, out NineMileCreekRoads.Way way))
                {
                    float d = NineMileCreekMainland.DistanceToRoute(way.Route, cell.Centre);
                    Assert.That(d, Is.LessThanOrEqualTo(way.HalfWidth + 1e-3f),
                        $"a cell at {cell.Centre} is attributed to {way.Name} but lies {d:0.00} m from " +
                        $"its route, outside the {way.HalfWidth} m carriageway");
                    continue;
                }

                if (_pads.TryGetValue(cell.Way, out NineMileCreekRoads.Pad pad))
                {
                    Assert.IsTrue(pad.Area.Contains(cell.Centre),
                        $"a cell at {cell.Centre} is attributed to {pad.Name} but lies outside its " +
                        $"pad {pad.Area}");
                    continue;
                }

                Assert.Fail($"a cell at {cell.Centre} is attributed to '{cell.Way}', which is neither a " +
                            "published way nor a published pad");
            }
        }

        [Test]
        public void EveryPavedCellIsInsideTheRegionsOwnRectangle()
        {
            Rect region = NineMileCreekRoads.RegionRect();
            foreach (var cell in _paving.Cells)
                Assert.IsTrue(region.Contains(cell.Centre),
                    $"{cell.Way} paves {cell.Centre}, off the edge of a {region.width:0} × " +
                    $"{region.height:0} m region");
        }

        // =============================================================================================
        //  3. THE LEGALITY GUARD — a road may taper, but it may not have a hole
        // =============================================================================================

        [Test]
        public void EveryRoadsCentreLineIsPavedEndToEnd()
        {
            // ⭐ THE MARSH-POOL GUARD, ONE LAYER DOWN. The dry-ground rule lets a carriageway lose its
            // OUTER cells wherever the ground goes under — which is right at the landing, where the bar
            // road runs down to a beach. It must never let a road lose its MIDDLE: a route driven across
            // the marsh pool or the barachois comes back in two pieces, and this is where that shows.
            foreach (var way in NineMileCreekRoads.Ways())
            {
                if (!way.CentreLineCritical) continue;

                bool whole = NineMileCreekRoads.CentreLineIsContinuous(_paving, way, out Vector2 gap);
                Assert.IsTrue(whole,
                    $"{way.Name} has a HOLE in it: the cell at {gap} is on its own centre-line and was " +
                    $"not paved, so the ground there is at or below spring high water ({SpringHigh:0.0} " +
                    "m). Roads do not ford ponds — re-site the route, do not widen the rule.");
            }
        }

        [Test]
        public void NoPavedCellStandsInEitherPond()
        {
            // Said geometrically as well as by elevation, because the two ponds are the named hazard: the
            // barachois behind the spit and the marsh pool the road runs the neck of.
            foreach (var carve in NineMileCreekMainland.Carves)
            {
                Rect pond = Rect.MinMaxRect(carve.Center.x - carve.HalfSize.x,
                                            carve.Center.y - carve.HalfSize.y,
                                            carve.Center.x + carve.HalfSize.x,
                                            carve.Center.y + carve.HalfSize.y);
                foreach (var cell in _paving.Cells)
                    Assert.IsFalse(pond.Contains(cell.Centre),
                        $"{cell.Way} paves {cell.Centre}, which is inside a pond ({pond}). The neck " +
                        "between the two ponds is 12 m wide and the road goes through it, not over them.");
            }
        }

        [Test]
        public void EveryPavedCellStandsOnGroundThatStaysDry()
        {
            foreach (var cell in _paving.Cells)
            {
                float e = _terrain.ElevationAt(cell.Centre);
                Assert.That(e, Is.GreaterThan(SpringHigh),
                    $"{cell.Way} paves {cell.Centre} where the ground is {e:0.00} m — at or below spring " +
                    $"high water ({SpringHigh:0.0} m)");
            }
        }

        [Test]
        public void TheDryGroundRuleTrimsTheClaimRatherThanTheRouteBeingTrimmedByHand()
        {
            // The claimed footprint (no terrain) must be a strict SUPERSET of the laid one, and the
            // difference must be exactly what the paving reports as trimmed. If those two ever disagree,
            // something is dropping cells for a reason nobody wrote down.
            var claimed = NineMileCreekRoads.Pave(null);

            Assert.That(claimed.Cells.Count, Is.GreaterThanOrEqualTo(_paving.Cells.Count));
            Assert.That(claimed.Cells.Count - _paving.Cells.Count, Is.EqualTo(_paving.Trimmed),
                "every cell the water took back must be counted in Paving.Trimmed — that number is the " +
                "one the owner reads to know how far a road has tapered");

            foreach (var cell in _paving.Cells)
                Assert.IsTrue(claimed.IsPaved(cell.Cell.x, cell.Cell.y),
                    $"{cell.Centre} is laid but was never claimed — the terrain cannot ADD road");
        }

        // =============================================================================================
        //  4. THE TILES — a network, not a row of stubs
        // =============================================================================================

        [Test]
        public void EveryPavedCellResolvesToARealAtlasCell()
        {
            // This exercises RoadKitContract, so it also proves the shipped contract still answers for
            // every neighbourhood this region can produce. A mask that canonicalises outside the 47 means
            // the C# canon() and the rig's have drifted — a kit fault the contract throws on.
            foreach (var cell in _paving.Cells)
            {
                int index = NineMileCreekRoads.AtlasIndexAt(_paving, cell.Cell.x, cell.Cell.y);
                Assert.That(index, Is.InRange(0, RoadKitV3.BlobCount - 1),
                    $"the cell at {cell.Centre} resolved to atlas index {index}, outside the kit's " +
                    $"{RoadKitV3.BlobCount}");
            }
        }

        [Test]
        public void NoPavedCellIsDrawnAsAnIsolatedStub()
        {
            // ⭐ THE BLOB KIT'S OWN FAILURE MODE, and the reason its contract is a lookup rather than a
            // derivation: a tile renders perfectly in isolation and tears at every junction, so only a
            // rendered NETWORK shows the defect. A cell with no paved cardinal neighbour draws the
            // "isolated" cell — a square of road in a field — which on a connected network means the
            // width or the sampling has left a gap.
            foreach (var cell in _paving.Cells)
            {
                int x = cell.Cell.x, y = cell.Cell.y;
                bool joined = _paving.IsPaved(x, y + 1) || _paving.IsPaved(x + 1, y) ||
                              _paving.IsPaved(x, y - 1) || _paving.IsPaved(x - 1, y);
                Assert.IsTrue(joined,
                    $"{cell.Way} lays an ISOLATED square metre at {cell.Centre} — no paved neighbour " +
                    "north, east, south or west, so the kit draws it as a stub in a field");
            }
        }

        [Test]
        public void TheNetworkUsesTheKitsJunctionsAndCornersRatherThanOneStraightTile()
        {
            var used = new HashSet<int>();
            foreach (var cell in _paving.Cells)
                used.Add(NineMileCreekRoads.AtlasIndexAt(_paving, cell.Cell.x, cell.Cell.y));

            // A road that bends, meets another road and ends at a shore cannot be drawn out of two tiles.
            // The number is a floor, not a target: it says the mask machinery is answering, which is the
            // one thing a per-tile check can never show.
            Assert.That(used.Count, Is.GreaterThan(8),
                $"the whole network resolved to only {used.Count} distinct atlas cells — the neighbour " +
                "mask is not varying, which is what a blob kit looks like when its mask is wrong");
        }

        // =============================================================================================
        //  5. DETERMINISM (rule 5) — and there is no random source to seed
        // =============================================================================================

        [Test]
        public void ThePavingIsAPureFunctionOfThePlan_AcrossRuns()
        {
            var again = NineMileCreekRoads.Pave(_terrain);

            Assert.That(again.Cells.Count, Is.EqualTo(_paving.Cells.Count));
            for (int i = 0; i < again.Cells.Count; i++)
            {
                Assert.That(again.Cells[i].Cell, Is.EqualTo(_paving.Cells[i].Cell),
                    $"cell {i} moved between two runs of the same paving — the order is written into a " +
                    "committed scene, so an unstable one makes every rebuild read as a real diff");
                Assert.That(again.Cells[i].Surface, Is.EqualTo(_paving.Cells[i].Surface));
                Assert.That(again.Cells[i].Way, Is.EqualTo(_paving.Cells[i].Way));
            }
        }

        [Test]
        public void TheCellsComeBackInASortedOrder()
        {
            for (int i = 1; i < _paving.Cells.Count; i++)
            {
                Vector2Int a = _paving.Cells[i - 1].Cell, b = _paving.Cells[i].Cell;
                bool ordered = b.y > a.y || (b.y == a.y && b.x > a.x);
                Assert.IsTrue(ordered,
                    $"cell {i} ({b}) does not follow {a} in south→north, west→east order — a dictionary " +
                    "walk order is not guaranteed across runtimes and this list is serialised");
            }
        }

        // =============================================================================================
        //  6. THE APRON AND THE PARKING — the wharf doc's own kit table
        // =============================================================================================

        [Test]
        public void TheConcreteApronCoversTheWorkingEndAndStopsAtTheQuay()
        {
            Rect concrete = NineMileCreekRoads.WinchApronArea();
            Rect apron = NineMileCreekWharf.ApronFootprint();
            Rect quay = NineMileCreekWharf.DeckFootprint();

            Assert.That(concrete.xMin, Is.GreaterThanOrEqualTo(apron.xMin - 1e-3f));
            Assert.That(concrete.xMax, Is.LessThanOrEqualTo(apron.xMax + 1e-3f));
            Assert.That(concrete.yMin, Is.GreaterThanOrEqualTo(apron.yMin - 1e-3f),
                "the concrete may not run off the south end of the west wall");
            Assert.That(concrete.yMax, Is.LessThanOrEqualTo(quay.yMin + 1e-3f),
                "the concrete must stop where the apron goes under the north wall's deck — the same line " +
                "NineMileCreekDressing.ApronFaceNorth stops the drawn face at, and for the same reason");

            Assert.IsTrue(concrete.Contains(new Vector2(NineMileCreekMainland.WinchPos.x,
                                                        NineMileCreekMainland.WinchPos.y)),
                "the apron is FOR the winch — the winch has to be standing on it");
            Assert.IsTrue(concrete.Contains(new Vector2(NineMileCreekMainland.UnloadApronPos.x,
                                                        NineMileCreekMainland.UnloadApronPos.y)),
                "…and the catch is landed at the authored unloading point, which must be on it too");
        }

        [Test]
        public void TheParkingStandsOnTheMadeGroundUnderTheBuyersTrucks()
        {
            Rect parking = NineMileCreekRoads.ParkingArea();
            Rect spit = NineMileCreekWharf.FootprintOf(NineMileCreekMainland.SpitFill);

            Assert.IsTrue(spit.Contains(new Vector2(parking.xMin, parking.yMin)) &&
                          spit.Contains(new Vector2(parking.xMax - 1e-3f, parking.yMax - 1e-3f)),
                $"the parking ({parking}) must stand entirely on the spit's made ground ({spit}) — gravel " +
                "spilled off the fill is gravel in the marsh");

            Assert.IsTrue(parking.Contains(new Vector2(NineMileCreekMainland.ParkingPos.x,
                                                       NineMileCreekMainland.ParkingPos.y)),
                "the pad covers the authored parking site, not the ground beside it");
            Assert.IsTrue(parking.Contains(new Vector2(NineMileCreekMainland.FishBuyerPos.x,
                                                       NineMileCreekMainland.FishBuyerPos.y)),
                "…and the buyer's tailgate, which is the whole reason the gravel is there: the man you " +
                "sell your first clams to has to be standing on something");
            Assert.IsFalse(parking.Contains(new Vector2(NineMileCreekMainland.DerelictDoryPos.x,
                                                        NineMileCreekMainland.DerelictDoryPos.y)),
                "the derelict dory is on the hard, not in the car park");
        }

        // =============================================================================================
        //  7. THE TOWN WALKS — the one thing here canon does not ask for
        // =============================================================================================

        [Test]
        public void EveryTownWalkJoinsARoadAtOneEndAndStopsAtItsOwnDoor()
        {
            var walks = NineMileCreekRoads.TownWalks();
            var lots = NineMileCreekRoads.PublicTownLots;
            Assert.That(walks.Count, Is.EqualTo(lots.Length),
                "every public lot gets exactly one walk");

            for (int i = 0; i < walks.Count; i++)
            {
                Vector2[] route = walks[i].Route;
                Assert.That(route.Length, Is.EqualTo(2), "a front walk is one straight run, not a lane");

                float toRoad = Mathf.Min(
                    NineMileCreekMainland.DistanceToRoute(NineMileCreekMainland.ThroughRoad, route[0]),
                    NineMileCreekMainland.DistanceToRoute(NineMileCreekMainland.WharfRoad, route[0]));
                Assert.That(toRoad, Is.LessThan(0.5f),
                    $"{walks[i].Name} starts {toRoad:0.00} m from the nearest carriageway — a walk that " +
                    "does not reach the road is a path from nowhere");

                // ⚠ IT STOPS ON THE LOT'S RESERVED RADIUS, not at the lot centre: a walk run to the
                // middle of the lot would lay shell UNDER whatever building stands there.
                float toDoor = Vector2.Distance(route[1], lots[i]);
                Assert.That(toDoor, Is.EqualTo(NineMileCreekMainland.TownLotRadius).Within(1e-3f),
                    $"{walks[i].Name} ends {toDoor:0.00} m from its lot centre — it must stop on the " +
                    $"{NineMileCreekMainland.TownLotRadius} m radius every town lot reserves");

                // …and it must point AT the lot, not past it.
                float alongLine = Vector2.Distance(route[0], route[1]) + toDoor;
                Assert.That(alongLine, Is.EqualTo(Vector2.Distance(route[0], lots[i])).Within(1e-2f),
                    $"{walks[i].Name} does not run straight from the road to its lot");
            }
        }

        [Test]
        public void TheHousesGetNoWalk()
        {
            // ⭐ THE RESTRAINT, PINNED. A dooryard on a farming coast is grass and ruts. If a later pass
            // wants to pave one, it has to delete this test and say why.
            var houses = new[]
            {
                NineMileCreekMainland.HouseNorthPos, NineMileCreekMainland.HouseNorthWestPos,
                NineMileCreekMainland.HouseSouthPos, NineMileCreekMainland.BoatShedPos,
            };

            foreach (var walk in NineMileCreekRoads.TownWalks())
            foreach (var house in houses)
                Assert.That(Vector2.Distance(walk.Route[1], new Vector2(house.x, house.y)),
                    Is.GreaterThan(1f),
                    $"{walk.Name} ends at a house lot — the walks serve the five PUBLIC lots only");
        }

        [Test]
        public void NoWalkIsLaidAlongARuralRoad()
        {
            // ⚠ THE OWNER'S CONSTRAINT, ENCODED. "Do not spread them along rural roads; the photographs
            // show red dirt and gravel, not curbs." A walk is a spur ACROSS the verge to a door; a walk
            // that ran ALONGSIDE the bar road or down the gully would be a sidewalk, which this region
            // does not have.
            foreach (var cell in _paving.Cells)
            {
                if (cell.Surface != NineMileCreekRoads.WalkSurface) continue;

                float toBarRoad = NineMileCreekMainland.DistanceToRoute(
                    NineMileCreekMainland.BarRoad, cell.Centre);
                Assert.That(toBarRoad, Is.GreaterThan(NineMileCreekMainland.RoadHalfWidth),
                    $"a shell walk is laid at {cell.Centre}, {toBarRoad:0.0} m from the bar road — the " +
                    "walks are town-side only");

                float toGully = NineMileCreekMainland.DistanceToRoute(
                    NineMileCreekMainland.GullyPath, cell.Centre);
                Assert.That(toGully, Is.GreaterThan(NineMileCreekMainland.RoadHalfWidth),
                    $"a shell walk is laid at {cell.Centre}, near the gully path — the walks are " +
                    "town-side only");
            }
        }

        [Test]
        public void TheWalksStaySparse()
        {
            // A budget, so "keep it sparse" is a number rather than an intention. Five walks of about
            // thirty metres at a metre and a half is ~250 m²; twice that would mean somebody had started
            // paving the town.
            int shell = 0;
            foreach (var cell in _paving.Cells)
                if (cell.Surface == NineMileCreekRoads.WalkSurface) shell++;

            Assert.That(shell, Is.GreaterThan(0), "the walks were laid out and none of them paved a cell");
            Assert.That(shell, Is.LessThan(500),
                $"{shell} m² of shell walk — this region has five short front walks, not a paved town");
        }

        [Test]
        public void AWalkGivesWayToEveryRoadItJoins()
        {
            // Rank, measured rather than trusted: where a walk reaches the carriageway the ROAD is what
            // is drawn, or the gravel would be interrupted by a square of shell at every junction.
            foreach (var cell in _paving.Cells)
            {
                if (cell.Surface != NineMileCreekRoads.WalkSurface) continue;

                foreach (var road in new[] { NineMileCreekMainland.ThroughRoad,
                                             NineMileCreekMainland.WharfRoad })
                    Assert.That(NineMileCreekMainland.DistanceToRoute(road, cell.Centre),
                        Is.GreaterThan(NineMileCreekRoads.CarriagewayHalfWidthMetres),
                        $"a shell cell at {cell.Centre} stands inside a gravel carriageway — the road " +
                        "outranks the walk and should have kept the cell");
            }
        }

        // =============================================================================================
        //  helpers
        // =============================================================================================

        private NineMileCreekRoads.Way Way(string name)
        {
            Assert.IsTrue(_ways.ContainsKey(name), $"no way named '{name}'");
            return _ways[name];
        }

        private NineMileCreekRoads.Pad Pad(string name)
        {
            Assert.IsTrue(_pads.ContainsKey(name), $"no pad named '{name}'");
            return _pads[name];
        }
    }
}
