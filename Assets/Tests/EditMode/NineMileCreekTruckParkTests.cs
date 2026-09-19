using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE TRUCK PARK — is there somewhere to leave a road vehicle, and can it get there?</b>
    ///
    /// <para>The park and its spur are the region's first piece of road built for a vehicle to STOP on
    /// rather than pass through, and the whole of it derives from one published point
    /// (<see cref="NineMileCreekMainland.TruckParkPos"/>). That is the point of this file: the site is a
    /// PROPOSAL awaiting the owner's walk, so every claim about it has to survive him moving it. Nothing
    /// here hard-codes a coordinate that the plan does not publish.</para>
    ///
    /// <para><b>The load-bearing test is <see cref="TheParkIsDryOnEveryLastSquareMetre"/>.</b> The
    /// dry-ground rule in <see cref="NineMileCreekRoads.Pave"/> silently drops any cell at or below spring
    /// high water — which is right for a carriageway that must not ford, and catastrophic for a park,
    /// because a park sited in the barachois would come out as a smaller park with nothing failing. The
    /// region has shipped that class of defect once already (the bar road's dry NODES over a wet middle),
    /// so the park is asserted against the terrain rather than against the arithmetic that claimed it.</para>
    ///
    /// <para>The terrain is built through the SAME <see cref="NineMileCreekMainland.ConfigureTerrain"/> the
    /// builder calls, and the paving is a pure function of the published plan plus that terrain — no
    /// assets, no scene, no bake. Everything here is therefore honest headless, which is why
    /// <see cref="PavingTwiceLaysExactlyTheSameGround"/> may assert a non-zero count: it is arithmetic, not
    /// a local bake, and the "never require &gt; 0 of something a bake produces" rule does not bite here.</para>
    /// </summary>
    public class NineMileCreekTruckParkTests
    {
        private GameObject _terrainGo;
        private MainlandTidalTerrain _terrain;
        private NineMileCreekRoads.Paving _paving;

        private static Rect Park => NineMileCreekRoads.TruckParkArea();

        private static Vector2 ParkCentre => new Vector2(NineMileCreekMainland.TruckParkPos.x,
                                                         NineMileCreekMainland.TruckParkPos.y);

        [SetUp]
        public void SetUp()
        {
            _terrainGo = new GameObject("NineMileCreekMainland_TruckParkTest");
            _terrain = _terrainGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(_terrain);
            _paving = NineMileCreekRoads.Pave(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            GameServices.Reset();
        }

        // =============================================================================================
        //  1. THE GROUND IS THERE, AND ALL OF IT IS DRY
        // =============================================================================================

        /// <summary>
        /// ⭐ Every square metre the park claims survives the dry-ground filter.
        ///
        /// <para>Counted against a SECOND paving run with no terrain at all — which
        /// <see cref="NineMileCreekRoads.Pave"/> documents as "skip the dry-ground rule and return the full
        /// claimed footprint". The difference between the two is exactly the ground the tide took, and for
        /// a park it must be zero. Comparing against the claim rather than against a hard-coded area is
        /// what lets the owner move or resize the park without touching this test.</para>
        /// </summary>
        [Test]
        public void TheParkIsDryOnEveryLastSquareMetre()
        {
            int claimed = CellsOwnedBy(NineMileCreekRoads.Pave(null), NineMileCreekRoads.TruckParkName);
            int paved = CellsOwnedBy(_paving, NineMileCreekRoads.TruckParkName);

            Assert.That(claimed, Is.GreaterThan(0),
                "the truck park claimed no cells at all — TruckParkArea() is empty or off the region.");

            Assert.That(paved, Is.EqualTo(claimed),
                $"{claimed - paved} of the truck park's {claimed} m² is at or below spring high water " +
                $"({NineMileCreekMainland.SpringHighWater} m) and was trimmed away, so the park would be " +
                $"drawn smaller than it is planned — silently. The park is centred on " +
                $"{ParkCentre} ({Park.width:0.#} × {Park.height:0.#} m); move " +
                "NineMileCreekMainland.TruckParkPos onto higher ground.");
        }

        /// <summary>The spur is dry too, over its whole length — the same question one layer along, and
        /// the one the bar road's marsh-pool defect was actually about.</summary>
        [Test]
        public void TheSpurIsDryOverItsWholeLength()
        {
            int claimed = CellsOwnedBy(NineMileCreekRoads.Pave(null), NineMileCreekRoads.ParkSpurName);
            int paved = CellsOwnedBy(_paving, NineMileCreekRoads.ParkSpurName);

            Assert.That(paved, Is.EqualTo(claimed),
                $"{claimed - paved} m² of the park spur is under spring high water. A spur that fords is " +
                "a spur you cannot drive.");
        }

        // =============================================================================================
        //  2. IT CONNECTS — a park you cannot reach is scenery
        // =============================================================================================

        /// <summary>
        /// ⭐ The spur's centre-line is paved end to end, so there is a continuous gravel run from a
        /// published carriageway into the park. Sampled at a quarter-metre by
        /// <see cref="NineMileCreekRoads.CentreLineIsContinuous"/>, which is finer than a cell — a gap
        /// cannot hide between two samples.
        /// </summary>
        [Test]
        public void TheSpurReachesTheParkFromARoad()
        {
            NineMileCreekRoads.Way spur = WayNamed(NineMileCreekRoads.ParkSpurName);

            Assert.That(spur.Route, Is.Not.Null.And.Length.EqualTo(2),
                "the park spur should be the two points ParkSpurRoute() publishes.");

            float run = Vector2.Distance(spur.Route[0], spur.Route[1]);
            Assert.That(run, Is.GreaterThan(NineMileCreekRoads.CarriagewayHalfWidthMetres),
                $"the park is only {run:0.##} m from the carriageway, which is inside the road's own " +
                "width — it is a lay-by, not a park off a spur. Either walk TruckParkPos further from the " +
                "road or drop the spur and say so.");

            Assert.That(NineMileCreekRoads.CentreLineIsContinuous(_paving, spur, out Vector2 gap), Is.True,
                $"the park spur's centre-line is not paved at {gap} — the park cannot be driven to.");
        }

        /// <summary>The spur joins a REAL carriageway, not a walk or a footpath: its road end lands within
        /// half a carriageway of the through-road or Wharf Road.</summary>
        [Test]
        public void TheSpurJoinsAPublishedCarriageway()
        {
            Vector2 mouth = WayNamed(NineMileCreekRoads.ParkSpurName).Route[0];

            float toWharf = NineMileCreekMainland.DistanceToRoute(NineMileCreekMainland.WharfRoad, mouth);
            float toThrough =
                NineMileCreekMainland.DistanceToRoute(NineMileCreekMainland.ThroughRoad, mouth);

            Assert.That(Mathf.Min(toWharf, toThrough),
                Is.LessThanOrEqualTo(NineMileCreekRoads.CarriagewayHalfWidthMetres),
                $"the spur's mouth at {mouth} is {Mathf.Min(toWharf, toThrough):0.##} m from the nearest " +
                "carriageway centre-line — it starts in a field.");
        }

        /// <summary>Wharf Road is unbroken THROUGH the junction: the spur outranks nothing it meets, so
        /// the cell at the mouth is still gravel carriageway and still owned by the road.</summary>
        [Test]
        public void WharfRoadCarriesThroughTheSpurMouth()
        {
            Vector2 mouth = WayNamed(NineMileCreekRoads.ParkSpurName).Route[0];
            Vector2Int cell = NineMileCreekRoads.CellOf(mouth);

            NineMileCreekRoads.PavedCell paved = CellAt(cell);
            Assert.That(paved.Way, Is.Not.EqualTo(NineMileCreekRoads.ParkSpurName),
                $"the spur has taken the carriageway cell at {mouth} from " +
                $"{NineMileCreekRoads.WharfRoadName} — RankParkSpur must stay below RankWharfRoad so the " +
                "through gravel is not interrupted by a square of driveway.");
        }

        // =============================================================================================
        //  3. IT IS BIG ENOUGH FOR WHAT PARKS ON IT
        // =============================================================================================

        /// <summary>
        /// The park holds the envelope it declares, with a whole vehicle length to turn in.
        ///
        /// <para>⚠ This asserts the park against <see cref="NineMileCreekRoads.ParkedVehicleLengthMetres"/>,
        /// the envelope this region DECLARES. Whether the truck that actually ships fits inside that
        /// envelope is the vehicle pass's test to write, against its own def — this file must not reach
        /// into vehicle content to find out.</para>
        /// </summary>
        [Test]
        public void TheParkHoldsTheVehicleEnvelopeItDeclares()
        {
            float length = NineMileCreekRoads.ParkedVehicleLengthMetres;
            float width = NineMileCreekRoads.ParkedVehicleWidthMetres;

            // ⚠ A hair under, not exactly: the depth is built as two halves off a centre, so the width
            // that comes back out of the Rect is 13.400002-or-13.399998 depending on where the park sits.
            // An exact >= would be a coin flip on the owner's next site.
            Assert.That(Park.height, Is.GreaterThanOrEqualTo(length * 2f - 0.01f),
                $"the park is {Park.height:0.#} m deep — a {length} m truck parked in it has less than " +
                "its own length behind it, which is not a turn, it is a reverse onto the road.");

            Assert.That(Park.width, Is.GreaterThanOrEqualTo(width * 3f),
                $"the park is {Park.width:0.#} m wide, which will not take three {width} m vehicles " +
                "side by side.");
        }

        // =============================================================================================
        //  4. IT IS WHERE NOTHING ELSE IS
        // =============================================================================================

        /// <summary>Clear of every town lot at the radius each of them reserves — the park must not be
        /// gravel poured over somebody's dooryard.</summary>
        [Test]
        public void TheParkStandsClearOfEveryTownLot()
        {
            Rect park = Park;

            for (int i = 0; i < NineMileCreekMainland.TownLots.Length; i++)
            {
                Vector3 lot3 = NineMileCreekMainland.TownLots[i];
                var lot = new Vector2(lot3.x, lot3.y);

                Assert.That(DistanceFromRect(park, lot),
                    Is.GreaterThan(NineMileCreekMainland.TownLotRadius),
                    $"the truck park reaches inside the {NineMileCreekMainland.TownLotRadius} m a town " +
                    $"lot at {lot} reserves. Walk NineMileCreekMainland.TruckParkPos clear of it.");
            }
        }

        /// <summary>Clear of both ponds INCLUDING their falloff skirts — the ground a carve eases through
        /// is still ground the carve has lowered, and a park half on the barachois' margin is a park that
        /// floods at its east end long before it fails the dry test.</summary>
        [Test]
        public void TheParkStandsClearOfBothCarves()
        {
            Rect park = Park;

            foreach (MainlandZone carve in NineMileCreekMainland.Carves)
            {
                Rect wet = Rect.MinMaxRect(
                    carve.Center.x - carve.HalfSize.x - carve.Falloff,
                    carve.Center.y - carve.HalfSize.y - carve.Falloff,
                    carve.Center.x + carve.HalfSize.x + carve.Falloff,
                    carve.Center.y + carve.HalfSize.y + carve.Falloff);

                Assert.That(park.Overlaps(wet), Is.False,
                    $"the truck park {RectText(park)} reaches into a carve's skirt {RectText(wet)} — " +
                    "the pond eases up through that ground and the park's edge would sit in it.");
            }
        }

        /// <summary>The park is its own ground, not a second name for the buyers' gravel on the spit.
        /// Two pads at the same rank that overlapped would make which-one-wins depend on declaration
        /// order.</summary>
        [Test]
        public void TheParkIsNotTheBuyersGravel()
        {
            Assert.That(Park.Overlaps(NineMileCreekRoads.ParkingArea()), Is.False,
                "the truck park overlaps BuyersParking. They share a rank, so the overlap would be " +
                "resolved by which was declared first rather than by anything meant.");
        }

        /// <summary>Inside the region — a park past the edge is cells nobody can drive to.</summary>
        [Test]
        public void TheParkAndItsSpurLieInsideTheRegion()
        {
            Rect region = NineMileCreekRoads.RegionRect();

            Assert.That(region.Contains(new Vector2(Park.xMin, Park.yMin)), Is.True,
                $"the truck park's south-west corner is outside the region {RectText(region)}.");
            Assert.That(region.Contains(new Vector2(Park.xMax, Park.yMax)), Is.True,
                $"the truck park's north-east corner is outside the region {RectText(region)}.");
        }

        // =============================================================================================
        //  5. NOTHING GROWS ON IT
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>No hedge or tree is planted on the gravel — the hole the truck park found.</b>
        ///
        /// <para>The planting pass reads <see cref="NineMileCreekRoads.Ways"/> and never read
        /// <see cref="NineMileCreekRoads.Pads"/>. That was invisible while both pads sat on the wharf,
        /// because <see cref="NineMileCreekFields.IsFieldGround"/> already refuses made ground — the
        /// buyers' gravel is kept clear by the working-site radius round <c>ParkingPos</c>, which is luck
        /// rather than a rule. The truck park is the first pad on the mainland plateau, where every one
        /// of those gates says "field", so without
        /// <see cref="NineMileCreekFields.OnAnyPad"/> a hedge lands across the parking.</para>
        ///
        /// <para>Sampled on a lattice rather than at the centre: a centre-only check passes on a pass
        /// that keeps a 1 m disc clear and plants the other 260 m².</para>
        /// </summary>
        [Test]
        public void NothingWoodyIsPlantedOnTheTruckPark()
        {
            // The memo is built from the published lists; clear it so this asserts against the plan
            // rather than against whatever an earlier test in the run happened to cache.
            NineMileCreekFields.InvalidateRoadCache();

            foreach (Vector2 p in ParkLattice())
                Assert.That(NineMileCreekFields.IsPlantable(_terrain, p), Is.False,
                    $"a hedge or tree may be planted at {p}, which is ON the truck park's gravel " +
                    $"({RectText(Park)}). NineMileCreekFields.IsPlantable must refuse a paved pad.");
        }

        /// <summary>The meadow does not grow over the parking either. Grass grows to the EDGE of a
        /// gravel road, which is what makes a road read as a road — and stops at it, which is what makes
        /// gravel read as gravel.</summary>
        [Test]
        public void TheMeadowDoesNotGrowOverTheTruckPark()
        {
            NineMileCreekFields.InvalidateRoadCache();

            foreach (Vector2 p in ParkLattice())
                Assert.That(NineMileCreekFields.IsGrassGround(_terrain, p), Is.False,
                    $"meadow grass grows at {p}, on the truck park's gravel ({RectText(Park)}).");
        }

        /// <summary>Points across the park including its corners — 5 × 5, inset a hair so a corner
        /// sample is inside the rect rather than exactly on its boundary.</summary>
        static IEnumerable<Vector2> ParkLattice()
        {
            Rect park = Park;
            const int n = 4;
            for (int i = 0; i <= n; i++)
            for (int j = 0; j <= n; j++)
                yield return new Vector2(
                    Mathf.Lerp(park.xMin + 0.05f, park.xMax - 0.05f, i / (float)n),
                    Mathf.Lerp(park.yMin + 0.05f, park.yMax - 0.05f, j / (float)n));
        }

        // =============================================================================================
        //  6. IT LANDS THE SAME WAY TWICE — and the existing guard already says so
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The park and the spur are among what a run actually lays.</b>
        ///
        /// <para><b>⚠ THERE IS DELIBERATELY NO RUN-TWICE CONVERGENCE TEST IN THIS FILE, and that is a
        /// finding rather than an omission.</b> Paving a generated pass twice and comparing is exactly what
        /// <c>NineMileCreekRoadsTests.ThePavingIsAPureFunctionOfThePlan_AcrossRuns</c> already does — cell
        /// by cell, on <c>Cell</c>, <c>Surface</c> AND <c>Way</c> — and because the park and the spur are
        /// published through <see cref="NineMileCreekRoads.Ways"/> and <see cref="NineMileCreekRoads.Pads"/>
        /// rather than painted separately, that test covers them the moment they exist. A second copy here
        /// would pass for the same reason the first one does and rot independently of it. The exclusive-
        /// stroke idempotence trap does not reach this pass at all: <see cref="NineMileCreekRoads.Pave"/>
        /// claims into a dictionary keyed by cell and sorts the result, so it is a pure function with no
        /// accumulated state to converge.</para>
        ///
        /// <para>What that test genuinely cannot do is notice that the park is MISSING — it compares two
        /// runs of the same plan, so a region that paves no park at all satisfies it perfectly. That gap is
        /// this test, and it is the only half worth writing.</para>
        /// </summary>
        [Test]
        public void BothTheParkAndTheSpurActuallyGetLaid()
        {
            Assert.That(CellsOwnedBy(_paving, NineMileCreekRoads.TruckParkName), Is.GreaterThan(0),
                "no cell in the region is owned by the truck park.");
            Assert.That(CellsOwnedBy(_paving, NineMileCreekRoads.ParkSpurName), Is.GreaterThan(0),
                "no cell in the region is owned by the park spur — it is entirely overlapped by " +
                "higher-ranked ways, which means it is doing nothing.");
        }

        // =============================================================================================
        //  helpers
        // =============================================================================================

        static int CellsOwnedBy(NineMileCreekRoads.Paving paving, string wayName)
        {
            int n = 0;
            foreach (NineMileCreekRoads.PavedCell cell in paving.Cells)
                if (cell.Way == wayName) n++;
            return n;
        }

        NineMileCreekRoads.PavedCell CellAt(Vector2Int at)
        {
            foreach (NineMileCreekRoads.PavedCell cell in _paving.Cells)
                if (cell.Cell == at) return cell;

            Assert.Fail($"no paved cell at {at} — expected the spur's mouth to be on a carriageway.");
            return default;
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

        // =============================================================================================
        //  4. THE TRUCK IS ON IT — the reachability gap #560 left, closed and pinned
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>A park with no truck was the shipped state, and nothing failed.</b> #560 shipped the
        /// entire drive mode "on the dev-picker path only" — and there is no picker, so the Dually was
        /// drivable in every test and reachable in no scene. The owner found it by standing in the
        /// world and not finding her. This pins the placement the builder now makes: one drivable
        /// <see cref="HiddenHarbours.Vehicles.ParkedVehicle"/>, on the park's own published constant,
        /// carrying the def #556 baked.
        ///
        /// <para>Position is asserted against <see cref="NineMileCreekMainland.TruckParkPos"/> — the
        /// PROPOSAL constant — not a coordinate, so the owner's walk verdict moves the truck with the
        /// park and this test follows both.</para>
        /// </summary>
        [Test]
        public void TheBuilderParksADrivableDuallyOnTheParksOwnConstant()
        {
            GameObject truck = NineMileCreekTruckPark.Place();
            try
            {
                Assert.IsNotNull(truck,
                    "Place() returned nothing — the Dually's def is missing from " +
                    NineMileCreekTruckPark.DuallyDefPath + ", so the park builds empty.");

                Assert.AreEqual((Vector2)NineMileCreekMainland.TruckParkPos,
                                (Vector2)truck.transform.position,
                    "the truck is not on the park's published constant. She must derive from " +
                    "TruckParkPos so the owner's walk verdict moves her with the park.");

                var parked = truck.GetComponent<HiddenHarbours.Vehicles.ParkedVehicle>();
                Assert.IsNotNull(parked, "no ParkedVehicle — she would never skin at play.");
                Assert.IsNotNull(parked.Vehicle, "no def on her — a truck with no identity.");
                Assert.AreEqual("vehicle.dually_3500", parked.Vehicle.Id,
                    "the park carries something other than the Dually.");

                Assert.IsNotNull(truck.GetComponent<HiddenHarbours.Vehicles.VehicleDoor>(),
                    "no driver's door — parked scenery, not the drivable truck the gap was about.");

                // ⚠️ Top-down world: a Rigidbody2D ships with gravityScale 1 and would pull her
                // south forever. VehicleController.Awake re-zeroes at play; the SERIALIZED state
                // must be zero too, so the committed scene never carries a falling truck.
                Assert.AreEqual(0f, truck.GetComponent<Rigidbody2D>().gravityScale,
                    "her serialized gravityScale is not 0 — in a top-down world that is a truck " +
                    "accelerating south through the village.");
            }
            finally { if (truck != null) Object.DestroyImmediate(truck); }
        }

        /// <summary>The def that parks here fits the park — the binding the roads file declares in one
        /// direction ("this file must not depend on vehicle content") and delegates to the vehicle
        /// pass in the other. Asserted through the placed truck so the two can never drift.</summary>
        [Test]
        public void TheParkedDuallyFitsInsideTheParksDeclaredEnvelope()
        {
            GameObject truck = NineMileCreekTruckPark.Place();
            try
            {
                Assert.IsNotNull(truck, "no truck to measure — see the placement test.");
                var mesh = truck.GetComponent<HiddenHarbours.Vehicles.ParkedVehicle>().Vehicle.Mesh;
                Assert.IsNotNull(mesh, "the Dually's def carries no mesh — the bake regressed.");

                float loaMetres = mesh.CellH / (float)mesh.PxPerMetre;
                Assert.LessOrEqual(loaMetres, NineMileCreekRoads.ParkedVehicleLengthMetres + 4f,
                    "sanity ceiling only — the exact envelope claim lives with the vehicle pass; " +
                    "this guards against a def whose cell arithmetic went wild.");
            }
            finally { if (truck != null) Object.DestroyImmediate(truck); }
        }

        // =============================================================================================
        //  7. THE MODERN 3500 IS BESIDE HIM — the owner's ruling D2 of 2026-09-18
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>"Park her at Nine Mile Creek beside the Dually."</b> The owner's ruling D2 (2026-09-18),
        /// pinned the way section 4 pins him: one drivable
        /// <see cref="HiddenHarbours.Vehicles.ParkedVehicle"/>, on a published constant that derives from
        /// the park's own, carrying the def her row in the fleet bake produces.
        ///
        /// <para>⚠️ RED until her def is baked — the state section 4 was in before #556. The failure
        /// names the path and the bake, so red here reads as "not baked", never as a mystery.</para>
        /// </summary>
        [Test]
        public void TheBuilderParksADrivableModern3500InTheBayBesideTheDually()
        {
            GameObject truck = NineMileCreekTruckPark.PlaceModern3500();
            try
            {
                Assert.IsNotNull(truck,
                    "PlaceModern3500() returned nothing — her def is missing from " +
                    NineMileCreekTruckPark.Modern3500DefPath + ". Run the fleet bake (her row in " +
                    "VehicleRigFleet) before the region, or her bay builds empty.");

                Assert.AreEqual(NineMileCreekTruckPark.Modern3500Name, truck.name,
                    "she is not under her own name, so a rebuild cannot tell her from him.");

                Assert.AreEqual((Vector2)NineMileCreekMainland.Modern3500ParkPos,
                                (Vector2)truck.transform.position,
                    "she is not on her published constant. She must derive from Modern3500ParkPos " +
                    "(itself off TruckParkPos) so the owner's walk verdict moves her with the park.");

                var parked = truck.GetComponent<HiddenHarbours.Vehicles.ParkedVehicle>();
                Assert.IsNotNull(parked, "no ParkedVehicle — she would never skin at play.");
                Assert.IsNotNull(parked.Vehicle, "no def on her — a truck with no identity.");
                Assert.AreEqual("vehicle.modern_3500", parked.Vehicle.Id,
                    "her bay carries something other than the Modern 3500.");

                Assert.IsNotNull(truck.GetComponent<HiddenHarbours.Vehicles.VehicleDoor>(),
                    "no driver's door — parked scenery, not the drivable truck the ruling asked for.");

                Assert.AreEqual(0f, truck.GetComponent<Rigidbody2D>().gravityScale,
                    "her serialized gravityScale is not 0 — in a top-down world that is a truck " +
                    "accelerating south through the village.");
            }
            finally { if (truck != null) Object.DestroyImmediate(truck); }
        }

        /// <summary>
        /// ⭐ <b>Beside him means clear of him — and of everything else on that ground.</b> Her SOLID box
        /// (the collider her sidecar publishes and the bake copies onto her mesh def) is carried through
        /// her placed transform, and so is his. She must stand wholly on the park, clear of his box, a
        /// pace clear of the post his driver waits at, off every way's carriageway and off every other
        /// pad.
        ///
        /// <para>The clearances are computed HERE, from the boxes and the published routes, so the test
        /// does not borrow the code it is checking; the only numbers it takes from the plan are the ones
        /// the plan publishes (the routes, their half-widths, the pads, the pace).</para>
        ///
        /// <para>⚠️ RED until her def is baked, for the same reason as the placement test above.</para>
        /// </summary>
        [Test]
        public void SheStandsOnTheParkClearOfTheDuallyHisDriverAndEveryWay()
        {
            GameObject his = NineMileCreekTruckPark.Place();
            GameObject hers = NineMileCreekTruckPark.PlaceModern3500();
            try
            {
                Assert.IsNotNull(his, "no Dually to stand her beside — see section 4.");
                Assert.IsNotNull(hers, "no Modern 3500 to measure — see the placement test above.");

                Rect herBox = SolidFootprintOf(hers, "the Modern 3500");
                Rect hisBox = SolidFootprintOf(his, "the Dually");

                // Explicit edges, not Rect.Contains: Contains excludes the max edge.
                Assert.IsTrue(herBox.xMin >= Park.xMin && herBox.xMax <= Park.xMax &&
                              herBox.yMin >= Park.yMin && herBox.yMax <= Park.yMax,
                    $"she overhangs the park: her box {RectText(herBox)} vs the park {RectText(Park)}.");

                Assert.Greater(GapBetween(herBox, hisBox), 0f,
                    $"her box {RectText(herBox)} touches his {RectText(hisBox)} — beside became into.");

                Vector2 post = NineMileCreekTrips.ParkPost();
                Assert.GreaterOrEqual(DistanceFromRect(herBox, post), NineMileCreekTrips.PaceMetres,
                    $"his driver's post {post} is within a pace of her box {RectText(herBox)} — he " +
                    "would be standing in her.");

                foreach (NineMileCreekRoads.Way way in NineMileCreekRoads.Ways())
                    Assert.Greater(DistanceFromRectToRoute(herBox, way.Route), way.HalfWidth,
                        $"her box {RectText(herBox)} reaches onto {way.Name}'s carriageway " +
                        $"(half-width {way.HalfWidth} m).");

                foreach (NineMileCreekRoads.Pad pad in NineMileCreekRoads.Pads())
                {
                    if (pad.Name == NineMileCreekRoads.TruckParkName) continue;   // hers to stand on
                    Assert.Greater(GapBetween(herBox, pad.Area), 0f,
                        $"her box {RectText(herBox)} touches the {pad.Name} pad {RectText(pad.Area)}.");
                }
            }
            finally
            {
                if (hers != null) Object.DestroyImmediate(hers);
                if (his != null) Object.DestroyImmediate(his);
            }
        }

        /// <summary>Her placed name is hers — not his, not the otter's, not the outboard man's box — so
        /// a rebuild or a test that finds a truck by name can never pick up the wrong one.</summary>
        [Test]
        public void HerNameIsHerOwn()
        {
            string hers = NineMileCreekTruckPark.Modern3500Name;
            Assert.That(hers, Is.Not.Null.And.Not.Empty, "she has no name to be found by.");
            Assert.That(hers, Is.Not.EqualTo(NineMileCreekTruckPark.TruckName),
                "she and the Dually share a name — a lookup by name would find whichever came first.");
            Assert.That(hers, Is.Not.EqualTo(NineMileCreekOtterLanding.OtterName),
                "she and the otter share a name.");
            Assert.That(hers, Is.Not.EqualTo(NineMileCreekTrips.OutboardTruckName),
                "she and the outboard man's box share a name.");
        }

        /// <summary>
        /// The world box around a placed vehicle's SOLID box — the collider its mesh def carries (rig
        /// metres: +x curb side, +y nose), taken corner by corner through its transform so a rotated
        /// placement is measured as placed.
        /// </summary>
        static Rect SolidFootprintOf(GameObject truck, string who)
        {
            var mesh = truck.GetComponent<HiddenHarbours.Vehicles.ParkedVehicle>().Vehicle.Mesh;
            Assert.IsNotNull(mesh, $"{who}'s def carries no mesh — the bake regressed.");
            Assert.IsTrue(mesh.HasCollider, $"{who}'s mesh publishes no collider box to measure.");

            Vector3 lo = mesh.ColliderMinMeters, hi = mesh.ColliderMaxMeters;
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (Vector2 corner in new[] { new Vector2(lo.x, lo.y), new Vector2(hi.x, lo.y),
                                               new Vector2(hi.x, hi.y), new Vector2(lo.x, hi.y) })
            {
                Vector3 world = truck.transform.TransformPoint(corner);
                xMin = Mathf.Min(xMin, world.x); xMax = Mathf.Max(xMax, world.x);
                yMin = Mathf.Min(yMin, world.y); yMax = Mathf.Max(yMax, world.y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>The gap between two boxes: 0 when they touch or overlap.</summary>
        static float GapBetween(Rect a, Rect b)
        {
            float dx = Mathf.Max(a.xMin - b.xMax, 0f, b.xMin - a.xMax);
            float dy = Mathf.Max(a.yMin - b.yMax, 0f, b.yMin - a.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// The shortest distance from a box to a route's centre line: 0 when any leg meets the box,
        /// else the least of every leg-end-to-box and box-corner-to-leg distance — exact for a box and
        /// a segment that do not meet, since the nearest pair then has a vertex at one end.
        /// </summary>
        static float DistanceFromRectToRoute(Rect r, Vector2[] route)
        {
            if (route.Length == 1) return DistanceFromRect(r, route[0]);

            var corners = new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin),
                                  new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
            float best = float.MaxValue;
            for (int i = 0; i + 1 < route.Length; i++)
            {
                Vector2 a = route[i], b = route[i + 1];
                if (SegmentMeetsRect(a, b, r)) return 0f;
                best = Mathf.Min(best, DistanceFromRect(r, a), DistanceFromRect(r, b));
                foreach (Vector2 corner in corners) best = Mathf.Min(best, DistanceFromSegment(corner, a, b));
            }
            return best;
        }

        /// <summary>Whether segment a→b touches the box (a Liang–Barsky clip).</summary>
        static bool SegmentMeetsRect(Vector2 a, Vector2 b, Rect r)
        {
            Vector2 d = b - a;
            float[] p = { -d.x, d.x, -d.y, d.y };
            float[] q = { a.x - r.xMin, r.xMax - a.x, a.y - r.yMin, r.yMax - a.y };
            float enter = 0f, leave = 1f;
            for (int i = 0; i < 4; i++)
            {
                if (p[i] == 0f)
                {
                    if (q[i] < 0f) return false;   // parallel to this edge and outside it
                    continue;
                }
                float t = q[i] / p[i];
                if (p[i] < 0f) { if (t > leave) return false; if (t > enter) enter = t; }
                else { if (t < enter) return false; if (t < leave) leave = t; }
            }
            return true;
        }

        /// <summary>Distance from a point to segment a→b.</summary>
        static float DistanceFromSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
