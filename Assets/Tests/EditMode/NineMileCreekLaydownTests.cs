using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.Vehicles;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE LAYDOWN — is there ground for the road fleet, can it be driven to, and does the pair
    /// actually offer the hitch where it stands?</b>
    ///
    /// <para>The yard is nine machines on ground the truck park could not hold, and the whole of it
    /// derives from one published point (<see cref="NineMileCreekMainland.LaydownPos"/>). That is the
    /// point of this file, exactly as it is the point of <c>NineMileCreekTruckParkTests</c>: the site is
    /// a PROPOSAL awaiting the owner's walk, so every claim has to survive him moving it. Nothing here
    /// hard-codes a coordinate the plan does not publish.</para>
    ///
    /// <para><b>The load-bearing tests are three.</b> <see cref="TheApronIsDryOnEveryLastSquareMetre"/>,
    /// because the dry-ground rule in <see cref="NineMileCreekRoads.Pave"/> silently drops wet cells and
    /// a yard sited in the barachois would come out as a smaller yard with nothing failing.
    /// <see cref="EveryDeclaredEnvelopeIsMetByTheShippedDefs"/>, because the apron is sized from
    /// declared envelopes and the binding to the actual bake is a test's job — the same one-way
    /// dependency the roads file states for the truck park. And
    /// <see cref="ThePairStandsCoupleReadyWhereItIsPlaced"/>, which is the owner's ask in one assertion:
    /// a semi placed right offers the hitch.</para>
    ///
    /// <para>⚠️ Nothing here requires a BAKE to have run locally: the terrain is arithmetic and the
    /// vehicle defs are committed assets. A missing def fails loudly rather than passing vacuously —
    /// every test that walks the placements asserts the expected COUNT first.</para>
    /// </summary>
    public class NineMileCreekLaydownTests
    {
        private GameObject _terrainGo;
        private MainlandTidalTerrain _terrain;
        private NineMileCreekRoads.Paving _paving;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        /// <summary>Nine machines: five driven, four towed. The ask, as a number.</summary>
        private const int ExpectedUnits = 9;
        private const int ExpectedDriven = 5;
        private const int ExpectedTowed = 4;

        private static Rect Apron => NineMileCreekLaydown.ApronArea();

        private static Vector2 YardCentre => new Vector2(NineMileCreekMainland.LaydownPos.x,
                                                         NineMileCreekMainland.LaydownPos.y);

        [SetUp]
        public void SetUp()
        {
            _terrainGo = new GameObject("NineMileCreekMainland_LaydownTest");
            _terrain = _terrainGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(_terrain);
            _paving = NineMileCreekRoads.Pave(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();

            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            GameServices.Reset();
        }

        // =============================================================================================
        //  1. THE GROUND IS THERE, AND ALL OF IT IS DRY
        // =============================================================================================

        /// <summary>
        /// ⭐ Every square metre the apron claims survives the dry-ground filter.
        ///
        /// <para>Counted against a second paving run with no terrain — which <c>Pave</c> documents as
        /// "skip the dry-ground rule and return the full claimed footprint". The difference is exactly
        /// the ground the tide took, and for a yard it must be zero.</para>
        /// </summary>
        [Test]
        public void TheApronIsDryOnEveryLastSquareMetre()
        {
            int claimed = CellsOwnedBy(NineMileCreekRoads.Pave(null), NineMileCreekRoads.LaydownName);
            int paved = CellsOwnedBy(_paving, NineMileCreekRoads.LaydownName);

            Assert.That(claimed, Is.GreaterThan(0),
                "the laydown claimed no cells at all — ApronArea() is empty or off the region.");

            Assert.That(paved, Is.EqualTo(claimed),
                $"{claimed - paved} of the laydown's {claimed} m² is at or below spring high water " +
                $"({NineMileCreekMainland.SpringHighWater} m) and was trimmed away, so the yard would " +
                $"be drawn smaller than it is planned — silently. It is centred on {YardCentre} " +
                $"({Apron.width:0.#} × {Apron.height:0.#} m); move NineMileCreekMainland.LaydownPos " +
                "onto higher ground.");
        }

        /// <summary>The spur from the park is dry over its whole length — a spur that fords is a spur
        /// you cannot tow a 53-ft trailer up.</summary>
        [Test]
        public void TheSpurIsDryOverItsWholeLength()
        {
            int claimed = CellsOwnedBy(NineMileCreekRoads.Pave(null), NineMileCreekRoads.LaydownSpurName);
            int paved = CellsOwnedBy(_paving, NineMileCreekRoads.LaydownSpurName);

            Assert.That(claimed, Is.GreaterThan(0), "the laydown spur claimed no cells at all.");
            Assert.That(paved, Is.EqualTo(claimed),
                $"{claimed - paved} m² of the laydown spur is under spring high water.");
        }

        /// <summary>⭐ The spur's centre-line is paved end to end, so there is continuous gravel from the
        /// truck park into the yard. Sampled finer than a cell, so a gap cannot hide between samples.</summary>
        [Test]
        public void TheSpurReachesTheYardFromTheTruckPark()
        {
            NineMileCreekRoads.Way spur = WayNamed(NineMileCreekRoads.LaydownSpurName);

            Assert.That(spur.Route, Is.Not.Null.And.Length.EqualTo(2),
                "the laydown spur should be the two points LaydownSpurRoute() publishes.");

            float run = Vector2.Distance(spur.Route[0], spur.Route[1]);
            Assert.That(run, Is.GreaterThan(NineMileCreekRoads.CarriagewayHalfWidthMetres),
                $"the yard is only {run:0.##} m from the park, which is inside the road's own width — " +
                "it is one pad, not a yard off a spur.");

            Assert.That(NineMileCreekRoads.CentreLineIsContinuous(_paving, spur, out Vector2 gap), Is.True,
                $"the laydown spur's centre-line is not paved at {gap} — the yard cannot be driven to.");
        }

        /// <summary>Inside the region — a yard past the edge is cells nobody can drive to.</summary>
        [Test]
        public void TheApronAndItsSpurLieInsideTheRegion()
        {
            Rect region = NineMileCreekRoads.RegionRect();
            Rect apron = Apron;

            Assert.That(region.Contains(new Vector2(apron.xMin, apron.yMin)) &&
                        region.Contains(new Vector2(apron.xMax, apron.yMax)), Is.True,
                $"the laydown {RectText(apron)} is not wholly inside the region {RectText(region)}.");

            foreach (Vector2 p in NineMileCreekLaydown.LaydownSpurRoute())
                Assert.That(region.Contains(p), Is.True,
                    $"the laydown spur passes outside the region at {p}.");
        }

        // =============================================================================================
        //  2. IT IS WHERE NOTHING ELSE IS
        // =============================================================================================

        /// <summary>Clear of every town lot at the radius each reserves — the yard must not be gravel
        /// poured over somebody's dooryard.</summary>
        [Test]
        public void TheYardStandsClearOfEveryTownLot()
        {
            Rect apron = Apron;

            for (int i = 0; i < NineMileCreekMainland.TownLots.Length; i++)
            {
                Vector3 lot3 = NineMileCreekMainland.TownLots[i];
                var lot = new Vector2(lot3.x, lot3.y);

                Assert.That(DistanceFromRect(apron, lot),
                    Is.GreaterThan(NineMileCreekMainland.TownLotRadius),
                    $"the laydown reaches inside the {NineMileCreekMainland.TownLotRadius} m a town lot " +
                    $"at {lot} reserves. Walk NineMileCreekMainland.LaydownPos clear of it.");
            }
        }

        /// <summary>Clear of both ponds INCLUDING their falloff skirts — a yard half on the barachois'
        /// margin floods at its east end long before it fails the dry test.</summary>
        [Test]
        public void TheYardStandsClearOfBothCarves()
        {
            Rect apron = Apron;

            foreach (MainlandZone carve in NineMileCreekMainland.Carves)
            {
                Rect wet = Rect.MinMaxRect(
                    carve.Center.x - carve.HalfSize.x - carve.Falloff,
                    carve.Center.y - carve.HalfSize.y - carve.Falloff,
                    carve.Center.x + carve.HalfSize.x + carve.Falloff,
                    carve.Center.y + carve.HalfSize.y + carve.Falloff);

                Assert.That(apron.Overlaps(wet), Is.False,
                    $"the laydown {RectText(apron)} reaches into a carve's skirt {RectText(wet)}.");
            }
        }

        /// <summary>
        /// ⚠️⚠️ <b>The yard is its own ground — not the truck park, and not the wharf yard.</b>
        ///
        /// <para>The park matters because the two share a rank: an overlap would be resolved by
        /// declaration order rather than by anything meant. The buyers' gravel matters for the same
        /// reason. And the SPIT matters because it is full — the fish market misses its own site by
        /// 0.38 m — so a laydown that reached onto it would be taking ground three other things are
        /// already queued for (memory <c>nmc-wharf-yard-is-full</c>).</para>
        /// </summary>
        [Test]
        public void TheYardIsNotTheParkNorTheBuyersGravelNorTheWharfYard()
        {
            Rect apron = Apron;

            Assert.That(apron.Overlaps(NineMileCreekRoads.TruckParkArea()), Is.False,
                "the laydown overlaps the truck park. They share a rank, so the overlap would be " +
                "resolved by which was declared first rather than by anything meant.");

            Assert.That(apron.Overlaps(NineMileCreekRoads.ParkingArea()), Is.False,
                "the laydown overlaps the buyers' gravel on the spit — that yard is FULL.");

            Assert.That(apron.Overlaps(NineMileCreekRoads.WinchApronArea()), Is.False,
                "the laydown overlaps the winch apron. The working wharf is not a truck yard.");
        }

        // =============================================================================================
        //  3. THE ENVELOPES ARE THE FLEET'S — derived, never transcribed
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Every envelope the yard declares is re-measured off the SHIPPED defs.</b>
        ///
        /// <para>The apron is region geometry and must be answerable without loading vehicle content,
        /// so <see cref="NineMileCreekLaydown"/> declares its envelopes as constants — exactly as
        /// <c>NineMileCreekRoads</c> declares the truck park's, and for the same stated reason. This is
        /// the binding in the other direction: a future longer trailer, or a re-bake that widens a cab,
        /// hangs a red here rather than a tail over the lane.</para>
        ///
        /// <para>The lane is the one worth reading twice: it is one full-lock turn radius, and that
        /// radius is COMPUTED from each machine's own wheelbase and Ackermann pair
        /// (<c>VehicleMeshDef.FullLockTurnRadiusMeters</c>) rather than transcribed from a handoff.</para>
        /// </summary>
        [Test]
        public void EveryDeclaredEnvelopeIsMetByTheShippedDefs()
        {
            List<NineMileCreekLaydown.Placement> placed = Solved();

            foreach (NineMileCreekLaydown.Placement p in placed)
            {
                VehicleMeshDef m = p.Mesh;
                float width = m.ColliderMaxMeters.x - m.ColliderMinMeters.x;
                float loa = m.ColliderMaxMeters.y - m.ColliderMinMeters.y;

                Assert.That(width, Is.LessThanOrEqualTo(NineMileCreekLaydown.WidestUnitMetres),
                    $"{p.Unit.Name} is {width:0.###} m wide, past the yard's declared " +
                    $"{NineMileCreekLaydown.WidestUnitMetres} m envelope — every bay is cut to it.");

                Assert.That(loa, Is.LessThanOrEqualTo(NineMileCreekLaydown.LongestUnitMetres),
                    $"{p.Unit.Name} is {loa:0.###} m long, past the declared " +
                    $"{NineMileCreekLaydown.LongestUnitMetres} m.");

                if (p.Unit.IsTowed) continue;

                float radius = m.FullLockTurnRadiusMeters;
                Assert.That(radius, Is.LessThanOrEqualTo(NineMileCreekLaydown.LaneWidthMetres),
                    $"{p.Unit.Name} turns at {radius:0.###} m full lock, wider than the lane's " +
                    $"{NineMileCreekLaydown.LaneWidthMetres} m — she cannot swing into her bay in one " +
                    "movement, which is the whole basis the lane is sized on.");
            }
        }

        /// <summary>
        /// ⭐ The coupled pair really is as long as the bay claims. Re-adds the four MEASURED numbers —
        /// the tractor's nose ahead of her origin, her plate behind it, the trailer's pin ahead of HERS
        /// and her tail behind it — rather than trusting the constant's own doc comment.
        /// </summary>
        [Test]
        public void TheCoupledPairFitsTheBayDepthItSizes()
        {
            NineMileCreekLaydown.Placement tractor = UnitNamed("AeroSemiAtTheLaydown");
            NineMileCreekLaydown.Placement trailer = UnitNamed("Flatbed53AtTheLaydown");

            // ⚠️ Measured off the SOLVED placements, not re-added from the seat arithmetic. Those two
            // answers differ by 0.31 m — the pin is parked at the capture window's middle, not on its
            // fore boundary — and a test that re-adds the ideal numbers agrees with an arithmetic the
            // builder does not use. It passed that way while the envelope was 0.28 m short of the real
            // pair, hidden by the bay's own slack.
            Rect tractorFoot = FootprintOf(tractor);
            Rect trailerFoot = FootprintOf(trailer);
            float pair = Mathf.Max(tractorFoot.yMax, trailerFoot.yMax) -
                         Mathf.Min(tractorFoot.yMin, trailerFoot.yMin);

            Assert.That(pair, Is.LessThanOrEqualTo(NineMileCreekLaydown.LongestPairMetres),
                $"the pair as PLACED measures {pair:0.###} m nose to tail, past the declared " +
                $"{NineMileCreekLaydown.LongestPairMetres} m that sizes every bay's depth.");

            // And the seat arithmetic, kept as the SECOND opinion: the pair standing exactly coupled
            // must be shorter than the pair standing couple-ready, or the pin is on the wrong side of
            // the plate.
            float seated = tractor.Mesh.ColliderMaxMeters.y
                           + -tractor.Mesh.FifthWheel.CouplingPointLocal.y
                           + trailer.Mesh.Kingpin.CouplingPointLocal.y
                           + -trailer.Mesh.ColliderMinMeters.y;
            Assert.That(seated, Is.LessThan(pair).And.GreaterThan(pair - 1f),
                $"coupled she would be {seated:0.###} m and couple-ready she is {pair:0.###} m. " +
                "Couple-ready must be slightly LONGER (the pin sits aft of the seat) and by well " +
                "under a metre — anything else means she is not backed under the plate at all.");

            Assert.That(NineMileCreekLaydown.BayDepthMetres,
                Is.GreaterThanOrEqualTo(pair + 2f * NineMileCreekLaydown.NoseSetbackMetres - 0.01f),
                "a bay is not deep enough to hold the pair with a setback at each end.");
        }

        // =============================================================================================
        //  4. THE YARD IS DERIVED FROM THE ONE CONSTANT
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Everything hangs off <see cref="NineMileCreekMainland.LaydownPos"/>.</b> The apron is
        /// centred on it exactly; every bay and the nose line are expressed off the apron; the spur ends
        /// on it. A hard-coded coordinate anywhere in the derivation breaks one of these.
        /// </summary>
        [Test]
        public void TheWholeYardHangsOffTheOnePublishedPoint()
        {
            Assert.That(Apron.center.x, Is.EqualTo(YardCentre.x).Within(1e-3f),
                "the apron is not centred on LaydownPos in x — something is hard-coded.");
            Assert.That(Apron.center.y, Is.EqualTo(YardCentre.y).Within(1e-3f),
                "the apron is not centred on LaydownPos in y — something is hard-coded.");

            Vector2[] spur = NineMileCreekLaydown.LaydownSpurRoute();
            Assert.That(spur[1], Is.EqualTo(YardCentre),
                "the spur does not end on LaydownPos, so moving the yard would leave the road behind.");
            Assert.That(spur[0], Is.EqualTo((Vector2)NineMileCreekMainland.TruckParkPos),
                "the spur does not start at the truck park — it was asked to run OFF the spur.");

            Assert.That(NineMileCreekLaydown.NoseLineY(),
                Is.EqualTo(Apron.yMin + NineMileCreekLaydown.LaneWidthMetres +
                           NineMileCreekLaydown.NoseSetbackMetres).Within(1e-3f),
                "the nose line is not the lane's north edge plus a setback.");

            for (int i = 0; i < NineMileCreekLaydown.BayCount; i++)
                Assert.That(Apron.Contains(new Vector2(NineMileCreekLaydown.BayCentreX(i),
                                                       Apron.center.y)), Is.True,
                    $"bay {i}'s centre-line falls outside the apron.");
        }

        /// <summary>No two bays claim the same ground, and every bay is inside the apron.</summary>
        [Test]
        public void NoTwoBaysOverlapAndAllAreOnTheApron()
        {
            Rect apron = Apron;

            for (int i = 0; i < NineMileCreekLaydown.BayCount; i++)
            {
                Rect a = NineMileCreekLaydown.BayArea(i);

                Assert.That(apron.xMin - 1e-3f <= a.xMin && a.xMax <= apron.xMax + 1e-3f &&
                            apron.yMin - 1e-3f <= a.yMin && a.yMax <= apron.yMax + 1e-3f, Is.True,
                    $"bay {i} {RectText(a)} is not inside the apron {RectText(apron)}.");

                Assert.That(a.Overlaps(NineMileCreekLaydown.LaneArea()), Is.False,
                    $"bay {i} reaches into the access lane — a machine parked in it blocks the yard.");

                for (int j = i + 1; j < NineMileCreekLaydown.BayCount; j++)
                    Assert.That(a.Overlaps(NineMileCreekLaydown.BayArea(j)), Is.False,
                        $"bays {i} and {j} overlap.");
            }
        }

        /// <summary>
        /// ⭐ Every machine's own footprint lands inside her bay — nose, tail and both flanks. This is
        /// what makes the bay table a claim about MACHINES rather than about rectangles.
        /// </summary>
        [Test]
        public void EveryMachineStandsInsideHerOwnBay()
        {
            foreach (NineMileCreekLaydown.Placement p in Solved())
            {
                Rect bay = NineMileCreekLaydown.BayArea(p.Unit.Bay);
                Rect foot = FootprintOf(p);

                Assert.That(bay.xMin - 1e-3f <= foot.xMin && foot.xMax <= bay.xMax + 1e-3f &&
                            bay.yMin - 1e-3f <= foot.yMin && foot.yMax <= bay.yMax + 1e-3f, Is.True,
                    $"{p.Unit.Name} {RectText(foot)} does not fit bay {p.Unit.Bay} {RectText(bay)}.");

                Assert.That(foot.Overlaps(NineMileCreekLaydown.LaneArea()), Is.False,
                    $"{p.Unit.Name} overhangs the access lane.");
            }
        }

        /// <summary>No two machines occupy the same ground — the pair excepted, which is the one place
        /// two footprints are MEANT to meet.</summary>
        [Test]
        public void NoTwoMachinesOverlapExceptTheCoupledPair()
        {
            List<NineMileCreekLaydown.Placement> placed = Solved();

            for (int i = 0; i < placed.Count; i++)
                for (int j = i + 1; j < placed.Count; j++)
                {
                    if (placed[i].Unit.Bay == placed[j].Unit.Bay) continue;   // the pair

                    Assert.That(FootprintOf(placed[i]).Overlaps(FootprintOf(placed[j])), Is.False,
                        $"{placed[i].Unit.Name} and {placed[j].Unit.Name} stand on the same ground.");
                }
        }

        // =============================================================================================
        //  5. ONE OF EACH IS ACTUALLY THERE
        // =============================================================================================

        /// <summary>⭐ The ask, as a count: nine machines, five driven and four towed, one of each body
        /// the drop shipped and no duplicates.</summary>
        [Test]
        public void OneOfEachOfTheNineStandsInTheYard()
        {
            List<NineMileCreekLaydown.Placement> placed = Solved();

            Assert.That(placed.Count, Is.EqualTo(ExpectedUnits),
                $"the yard solved {placed.Count} machines, not {ExpectedUnits}. If a def is missing " +
                "from the bake this is where it shows.");

            var ids = new HashSet<string>();
            int driven = 0, towed = 0;

            foreach (NineMileCreekLaydown.Placement p in placed)
            {
                Assert.That(ids.Add(p.Mesh.Id), Is.True,
                    $"{p.Mesh.Id} stands in the yard twice — 'one of each' means one.");

                if (p.Unit.IsTowed) { towed++; Assert.That(p.Def, Is.Null, "a towed body carries a def."); }
                else { driven++; Assert.That(p.Def, Is.Not.Null, $"{p.Unit.Name} carries no def."); }
            }

            Assert.That(driven, Is.EqualTo(ExpectedDriven), "wrong number of driven machines.");
            Assert.That(towed, Is.EqualTo(ExpectedTowed), "wrong number of towed bodies.");
        }

        /// <summary>The builder stands them all up: a <see cref="ParkedVehicle"/> per driven machine and
        /// a <see cref="ParkedTrailer"/> per towed body, each on her solved position and heading, and
        /// every driven one with her gravityScale serialized to zero.</summary>
        [Test]
        public void TheBuilderStandsAllNineOnTheirSolvedPlacements()
        {
            List<NineMileCreekLaydown.Placement> solved = Solved();
            List<GameObject> made = Placed();

            Assert.That(made.Count, Is.EqualTo(solved.Count),
                "Place() built a different number of machines than Solve() answered.");

            foreach (NineMileCreekLaydown.Placement p in solved)
            {
                GameObject go = made.Find(g => g != null && g.name == p.Unit.Name);
                Assert.That(go, Is.Not.Null, $"{p.Unit.Name} was never placed.");

                Assert.That(((Vector2)go.transform.position - p.Position).magnitude,
                    Is.LessThan(1e-3f), $"{p.Unit.Name} does not stand where Solve() put her.");

                Assert.That(BoatKinematics.BearingDegrees(go.transform.up),
                    Is.EqualTo(NineMileCreekLaydown.YardHeadingDegrees).Within(0.01f),
                    $"{p.Unit.Name} is not on the yard heading — her picture would face the wrong way.");

                if (p.Unit.IsTowed)
                {
                    var parked = go.GetComponent<ParkedTrailer>();
                    Assert.That(parked, Is.Not.Null, $"{p.Unit.Name} has no ParkedTrailer.");
                    Assert.That(parked.Body, Is.Not.Null, $"{p.Unit.Name} carries no mesh.");
                    Assert.That(parked.Trailer, Is.Not.Null,
                        $"{p.Unit.Name} has no TowedBody — nothing could ever couple to her.");
                }
                else
                {
                    var parked = go.GetComponent<ParkedVehicle>();
                    Assert.That(parked, Is.Not.Null, $"{p.Unit.Name} has no ParkedVehicle.");
                    Assert.That(parked.Vehicle, Is.Not.Null, $"{p.Unit.Name} carries no def.");
                    Assert.That(go.GetComponent<VehicleDoor>(), Is.Not.Null,
                        $"{p.Unit.Name} has no driver's door — parked scenery, not a drivable machine.");

                    // ⚠️ Top-down world: a Rigidbody2D ships with gravityScale 1 and would pull her
                    // south forever. The SERIALIZED state must be zero too.
                    Assert.That(go.GetComponent<Rigidbody2D>().gravityScale, Is.EqualTo(0f),
                        $"{p.Unit.Name}'s serialized gravityScale is not 0 — a machine accelerating " +
                        "south through the village.");
                }
            }
        }

        /// <summary>
        /// ⚠️⚠️ <b>A scene-placed trailer must come back on the angle she was placed on.</b>
        /// <c>TowedBody.HeadingDegrees</c> is not serialized — the transform's rotation is — so the
        /// heading is seeded from the transform on enable. Before that, a trailer authored into a scene
        /// loaded at heading 0 while her picture drew on the authored angle, and the tractor parked
        /// nose to nose with her refused the couple.
        /// </summary>
        [Test]
        public void EveryPlacedTrailerKnowsTheHeadingSheWasPlacedOn()
        {
            foreach (GameObject go in Placed())
            {
                var parked = go.GetComponent<ParkedTrailer>();
                if (parked == null) continue;

                Assert.That(parked.Trailer.HeadingDegrees,
                    Is.EqualTo(NineMileCreekLaydown.YardHeadingDegrees).Within(0.01f),
                    $"{go.name}'s TowedBody reads heading {parked.Trailer.HeadingDegrees:0.##}° while " +
                    "she is DRAWN on " + NineMileCreekLaydown.YardHeadingDegrees + "°. Her pin is " +
                    "reported somewhere she is not.");
            }
        }

        // =============================================================================================
        //  6. ⭐ THE PAIR OFFERS THE HITCH — the owner's ask, in one assertion
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>A semi placed right offers the couple.</b> The whole point of the yard's centrepiece,
        /// asked through the SHIPPED capture code rather than through a re-derivation of it: the real
        /// <see cref="VehicleHitch.CapturedTrailer"/>, over the real <c>TowedBody</c> registry, with all
        /// four trailers standing in the world so a false positive on the wrong one would fail here.
        ///
        /// <para>This no longer pins the yard's HEADING. It did: the coupling math rotated
        /// counter-clockwise while the transform frame the hitch reads is clockwise compass, so a yard
        /// turned off the north–south axis reddened here. The frames agree now, and
        /// <see cref="ThePairCouplesOnAnyHeadingTheWalkMayPick"/> is the proof.</para>
        /// </summary>
        [Test]
        public void ThePairStandsCoupleReadyWhereItIsPlaced()
        {
            List<GameObject> made = Placed();

            GameObject tractorGo = made.Find(g => g != null && g.name == "AeroSemiAtTheLaydown");
            GameObject trailerGo = made.Find(g => g != null && g.name == "Flatbed53AtTheLaydown");

            Assert.That(tractorGo, Is.Not.Null, "the pair's tractor was never placed.");
            Assert.That(trailerGo, Is.Not.Null, "the pair's trailer was never placed.");

            VehicleMeshDef tractorMesh = tractorGo.GetComponent<ParkedVehicle>().Vehicle.Mesh;
            Assert.That(tractorMesh.CanTow, Is.True,
                "the pair's tractor publishes no fifth wheel — re-run the bake.");

            // EditMode registers no presentation service, so the skinner installs no hitch. Wire the
            // real one by hand — the shape VehicleCouplingTests uses — so the assertion below runs
            // through the shipped capture code and not a copy of it.
            var hitch = tractorGo.AddComponent<VehicleHitch>();
            hitch.Configure(tractorMesh, tractorGo.GetComponent<VehicleController>(),
                            tractorGo.GetComponent<ParkedVehicle>().Vehicle.Id);

            TowedBody captured = hitch.CapturedTrailer();

            Assert.That(captured, Is.Not.Null,
                "the semi in bay 0 is offered NO trailer, so the hitch affordance the owner asked for " +
                "is not there. She stands at " + (Vector2)tractorGo.transform.position +
                " on " + hitch.HeadingDegrees.ToString("0.##") + "°, and the trailer's pin is at " +
                trailerGo.GetComponent<ParkedTrailer>().Trailer.KingpinWorld + ".");

            Assert.That(captured.gameObject, Is.EqualTo(trailerGo),
                $"the semi is offered {captured.gameObject.name} rather than the trailer standing on " +
                "her own plate — a trailer parked elsewhere in the yard is being captured across the " +
                "apron.");
        }

        /// <summary>
        /// ⭐ The pin sits in the MIDDLE of the capture window, not on its boundary. The seat is the
        /// window's fore edge, and a trailer parked exactly on it is one whose couple offer turns on the
        /// last bit of a float — the failure the coupling code documents having already had once.
        /// </summary>
        [Test]
        public void ThePairsPinStandsClearOfBothEndsOfTheCaptureWindow()
        {
            NineMileCreekLaydown.Placement tractor = UnitNamed("AeroSemiAtTheLaydown");
            NineMileCreekLaydown.Placement trailer = UnitNamed("Flatbed53AtTheLaydown");

            VehicleFifthWheel wheel = tractor.Mesh.FifthWheel;

            // The pin in the tractor's own frame, through a real transform — the frame the slot is
            // drawn in, and the one VehicleHitch.CapturedTrailer measures in.
            var probe = new GameObject("LaydownPairProbe");
            _spawned.Add(probe);
            probe.transform.position = new Vector3(tractor.Position.x, tractor.Position.y, 0f);
            probe.transform.rotation = tractor.Rotation;

            Vector2 pinWorld = NineMileCreekLaydown.CoupleReadyPinWorld(tractor);
            Vector3 local = probe.transform.InverseTransformPoint(
                new Vector3(pinWorld.x, pinWorld.y, 0f));

            float aft = Mathf.Min(wheel.RampMouthY, wheel.SlotSeatY);
            float fore = Mathf.Max(wheel.RampMouthY, wheel.SlotSeatY);

            Assert.That(local.y, Is.GreaterThan(aft + 0.1f).And.LessThan(fore - 0.1f),
                $"the pin sits at y {local.y:0.###} in the plate's frame, within 0.1 m of the window " +
                $"[{aft:0.###}, {fore:0.###}]. Parked on a boundary, the offer is a coin flip.");

            Assert.That(Mathf.Abs(local.x), Is.LessThan(wheel.SlotHalfWidthMeters),
                $"the pin is {Mathf.Abs(local.x):0.####} m off the slot's centreline.");

            // And she is where the solve says: the trailer's own origin agrees with the pin.
            Vector2 fromPin = VehicleCouplingMath.BodyOriginFromKingpin(
                pinWorld, NineMileCreekLaydown.YardHeadingDegrees, trailer.Mesh.Kingpin);
            Assert.That((fromPin - trailer.Position).magnitude, Is.LessThan(1e-3f),
                "the trailer's solved position does not put her pin on the plate.");
        }

        /// <summary>
        /// ⭐⭐ <b>The pair couples on ANY heading the walk may pick.</b> The yard faces south because its
        /// lane is on its south edge — not, any longer, because the coupling arithmetic only worked
        /// there. Stood through the builder's own <see cref="NineMileCreekLaydown.PlaceOne"/> and the
        /// pair's own <see cref="NineMileCreekLaydown.CoupleReadyTrailer"/> at eight headings and asked
        /// of the SHIPPED hitch: this is the test that reddened at 45° before PR 0 of the driveable
        /// charter, with the pin reported on the wrong side of the plate.
        /// </summary>
        [Test]
        public void ThePairCouplesOnAnyHeadingTheWalkMayPick()
        {
            NineMileCreekLaydown.Placement aero = UnitNamed("AeroSemiAtTheLaydown");
            NineMileCreekLaydown.Placement flatbed = UnitNamed("Flatbed53AtTheLaydown");
            VehicleFifthWheel wheel = aero.Mesh.FifthWheel;
            float aft = Mathf.Min(wheel.RampMouthY, wheel.SlotSeatY);
            float fore = Mathf.Max(wheel.RampMouthY, wheel.SlotSeatY);

            for (int i = 0; i < 8; i++)
            {
                float heading = 45f * i;
                var tractor = new NineMileCreekLaydown.Placement(aero.Unit, YardCentre, heading,
                                                                  aero.Mesh, aero.Def);
                NineMileCreekLaydown.Placement trailer =
                    NineMileCreekLaydown.CoupleReadyTrailer(tractor, flatbed.Unit, flatbed.Mesh);
                Assert.That(trailer.HeadingDegrees, Is.EqualTo(heading).Within(1e-4f),
                    "the pair's trailer is not on her tractor's heading.");

                GameObject tractorGo = NineMileCreekLaydown.PlaceOne(tractor);
                GameObject trailerGo = NineMileCreekLaydown.PlaceOne(trailer);
                _spawned.Add(tractorGo);
                _spawned.Add(trailerGo);

                // EditMode registers no presentation service, so the skinner installs no hitch — wire
                // the real one by hand, the shape ThePairStandsCoupleReadyWhereItIsPlaced uses.
                var hitch = tractorGo.AddComponent<VehicleHitch>();
                hitch.Configure(tractor.Mesh, tractorGo.GetComponent<VehicleController>(), tractor.Def.Id);
                TowedBody body = trailerGo.GetComponent<ParkedTrailer>().Trailer;
                Assert.That(body, Is.Not.Null, $"on {heading}°: the placed trailer grew no TowedBody.");

                // ⭐ Her pin where the PICTURE draws it — rotation × local, through her transform — and
                // then in the tractor's transform frame, the frame the plate is drawn in: in the middle
                // of the window, on the slot's centre-line. Asked of the drawn pin and not of
                // KingpinWorld, because a coupling that rotates the wrong way agrees with ITSELF at every
                // heading (the placement and the report were the same wrong turn) and only the picture
                // can see it. That is the arm this test lost first time round.
                var pinLocal = new Vector3(flatbed.Mesh.Kingpin.CouplingPointLocal.x,
                                           flatbed.Mesh.Kingpin.CouplingPointLocal.y, 0f);
                Vector2 pinWorld = trailerGo.transform.TransformPoint(pinLocal);
                Assert.That(Vector2.Distance(body.KingpinWorld, pinWorld), Is.LessThan(1e-3f),
                    $"on {heading}°: KingpinWorld reports the pin {Vector2.Distance(body.KingpinWorld, pinWorld):0.###} m " +
                    "from where the picture draws it — the coupling and the transform disagree.");
                Vector3 local = tractorGo.transform.InverseTransformPoint(
                    new Vector3(pinWorld.x, pinWorld.y, 0f));
                Assert.That(local.y, Is.GreaterThan(aft + 0.1f).And.LessThan(fore - 0.1f),
                    $"on {heading}°: the pin sits at y {local.y:0.###} in the plate's frame, outside the " +
                    $"middle of [{aft:0.###}, {fore:0.###}] — the pair is placed in a different frame " +
                    "from the one the plate is drawn in.");
                Assert.That(Mathf.Abs(local.x), Is.LessThan(wheel.SlotHalfWidthMeters),
                    $"on {heading}°: the pin is {Mathf.Abs(local.x):0.####} m off the slot's centre-line.");

                TowedBody captured = hitch.CapturedTrailer();
                Assert.That(captured, Is.SameAs(body),
                    $"on {heading}°: the tractor is offered " +
                    (captured == null ? "NOTHING" : captured.gameObject.name) +
                    " — the pair only couples facing north or south.");

                Object.DestroyImmediate(trailerGo);
                Object.DestroyImmediate(tractorGo);
            }
        }

        // =============================================================================================
        //  7. ⭐ EVERY DOOR, CONTROL AND COUPLE POINT HAS GROUND TO STAND ON
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>A reach audit never asks if there is GROUND</b> (memory <c>gas-station-arc</c>) — so
        /// this one does. Every published reach point in the yard, every drive door, and the pair's
        /// release handle must land on a PAVED cell: paving already excludes everything at or below
        /// spring high water, so paved means dry means standable, in one question.
        ///
        /// <para>A control the owner cannot stand at is a control that is not there, however correctly
        /// the art published it.</para>
        /// </summary>
        [Test]
        public void EveryDoorControlAndCouplePointHasStandableGround()
        {
            int checkedPoints = 0;

            foreach (NineMileCreekLaydown.Placement p in Solved())
            {
                // Her drive door, when she has one — the way a player gets in.
                if (!p.Unit.IsTowed && p.Mesh.DriveDoorLocal != Vector2.zero)
                    checkedPoints += AssertStandable(p, p.Mesh.DriveDoorLocal, "drive door");

                // Her fifth-wheel release handle — the couple point's own control.
                if (p.Mesh.CanTow)
                    checkedPoints += AssertStandable(p, p.Mesh.FifthWheel.ReleaseHandleLocal,
                                                     "fifth-wheel release");

                // Every worked opening the art published a place to stand for: rollups, liftgates,
                // barn doors, the landing-gear crank.
                if (p.Mesh.DoorGroups == null) continue;
                foreach (VehicleDoorGroup group in p.Mesh.DoorGroups)
                {
                    if (!group.HasReachPoint) continue;
                    checkedPoints += AssertStandable(p, group.ReachPointLocal, "the '" + group.Id + "' control");
                }
            }

            Assert.That(checkedPoints, Is.GreaterThan(ExpectedUnits),
                $"only {checkedPoints} reach points were audited across nine machines — the defs " +
                "publish more than that, so the walk over DoorGroups is not finding them.");
        }

        // =============================================================================================
        //  HELPERS
        // =============================================================================================

        List<NineMileCreekLaydown.Placement> Solved()
        {
            List<NineMileCreekLaydown.Placement> placed = NineMileCreekLaydown.Solve();
            Assert.That(placed.Count, Is.EqualTo(ExpectedUnits),
                $"the yard solved {placed.Count} of {ExpectedUnits} machines — a def is missing from " +
                "the bake, and every measurement below would be vacuously true.");
            return placed;
        }

        List<GameObject> Placed()
        {
            List<GameObject> made = NineMileCreekLaydown.Place();
            _spawned.AddRange(made);
            Assert.That(made.Count, Is.EqualTo(ExpectedUnits),
                $"Place() stood up {made.Count} of {ExpectedUnits} machines.");
            return made;
        }

        static NineMileCreekLaydown.Placement UnitNamed(string name)
        {
            foreach (NineMileCreekLaydown.Placement p in NineMileCreekLaydown.Solve())
                if (p.Unit.Name == name) return p;

            Assert.Fail($"no machine named '{name}' was solved into the yard.");
            return default;
        }

        /// <summary>A machine's world footprint. The yard faces due south, so her local box maps to an
        /// axis-aligned rectangle: a local +Y offset lands south, and +X lands west.</summary>
        static Rect FootprintOf(in NineMileCreekLaydown.Placement p)
        {
            Vector3 min = p.Mesh.ColliderMinMeters, max = p.Mesh.ColliderMaxMeters;
            Vector2 a = p.Position + RotateLocal(new Vector2(min.x, min.y), p.HeadingDegrees);
            Vector2 b = p.Position + RotateLocal(new Vector2(max.x, max.y), p.HeadingDegrees);

            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                                   Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>A local offset in the world on a compass heading — the transform's convention
        /// (z = −bearing), computed here so the test does not borrow the code it is checking.</summary>
        static Vector2 RotateLocal(Vector2 local, float headingDegrees)
        {
            float rad = -headingDegrees * Mathf.Deg2Rad;
            float s = Mathf.Sin(rad), c = Mathf.Cos(rad);
            return new Vector2(local.x * c - local.y * s, local.x * s + local.y * c);
        }

        int AssertStandable(in NineMileCreekLaydown.Placement p, Vector2 local, string what)
        {
            Vector2 world = p.Position + RotateLocal(local, p.HeadingDegrees);
            Vector2Int cell = NineMileCreekRoads.CellOf(world);

            Assert.That(_paving.IsPaved(cell.x, cell.y), Is.True,
                $"{p.Unit.Name}: {what} reaches to {world}, which is not paved ground. Either it is " +
                "off the apron or the tide took the cell — either way nobody can stand there to work " +
                "it, and the control is not really in the world.");
            return 1;
        }

        static int CellsOwnedBy(NineMileCreekRoads.Paving paving, string wayName)
        {
            int n = 0;
            foreach (NineMileCreekRoads.PavedCell cell in paving.Cells)
                if (cell.Way == wayName) n++;
            return n;
        }

        static NineMileCreekRoads.Way WayNamed(string name)
        {
            foreach (NineMileCreekRoads.Way way in NineMileCreekRoads.Ways())
                if (way.Name == name) return way;

            Assert.Fail($"no way named '{name}' — NineMileCreekRoads.Ways() does not publish it.");
            return default;
        }

        /// <summary>Distance from a point to the nearest edge of a rect, or 0 inside it.</summary>
        static float DistanceFromRect(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static string RectText(Rect r) =>
            $"x[{r.xMin:0.#}..{r.xMax:0.#}] y[{r.yMin:0.#}..{r.yMax:0.#}]";
    }
}
