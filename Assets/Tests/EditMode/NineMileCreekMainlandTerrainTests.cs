using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ WHAT THE TIDE DOES TO THE NINE MILE CREEK MAINLAND — the crossing that spans a region seam,
    /// the ruled depth gate at the wharf, and whether the roads and the lots are on ground that stays
    /// dry.
    ///
    /// <para>The load-bearing test in this file is
    /// <see cref="TheCrossingIsOneBarEitherSideOfTheSeam"/>. Nine Mile Creek's half of the tidal bar is
    /// the FIRST piece of terrain in the game that has to agree with terrain in another scene: the
    /// player walks 305 m in St Peters, takes a scene load, and walks 305 m more here. If the two halves
    /// disagree about the crest, the shoulder or the tide, the crossing is dry on one side of the load
    /// and flooded on the other — the region's whole lesson, broken by arithmetic that nothing else
    /// would catch.</para>
    ///
    /// <para><b>Two grounds since NMC H2.</b> The scene stands on G3 (<see cref="NineMileCreekLiveGround.Plan"/>),
    /// and what the scene must keep is asserted on it: the crossing against St Peters' terrain (built through
    /// <see cref="StPetersBuilder.ConfigureTidalTerrain"/>, the call its builder makes), the bar, the ponds, the
    /// open bay, the arrival, the wharf's markers, the roads on the lines step 06 paves and the town's lots. The
    /// analytic ground (<see cref="NineMileCreekMainland.ConfigureTerrain"/>: its tide reference, its fills, its
    /// berth trench, its plateau and its cached coast run) is the plan's AUTHORING, the premise the builder and
    /// the later waves still build on: those tests stand it up on their own, each says so, and none of them
    /// claims the activated scene.</para>
    /// </summary>
    public class NineMileCreekMainlandTerrainTests
    {
        private ITidalTerrain _ground;            // G3: the ground the scene stands on
        private GameObject _planGo;
        private MainlandTidalTerrain _plan;       // the analytic premise, stood up only by the authoring tests
        private GameObject _islandGo;
        private TidalTerrain _island;             // St Peters' half of the bar, stood up only by the crossing

        private static float SpringHigh => NineMileCreekMainland.SpringHighWater;   //  2.2
        private static float SpringLow => NineMileCreekMainland.SpringLowWater;     // -2.2

        [SetUp]
        public void SetUp() => _ground = NineMileCreekLiveGround.Plan;

        [TearDown]
        public void TearDown()
        {
            if (_planGo != null) Object.DestroyImmediate(_planGo);
            if (_islandGo != null) Object.DestroyImmediate(_islandGo);
            _planGo = null;
            _plan = null;
            _islandGo = null;
            _island = null;
            GameServices.Reset();
        }

        /// <summary>The plan's analytic ground, built through the call the builder still makes for the premise.</summary>
        private MainlandTidalTerrain Plan
        {
            get
            {
                if (_plan != null) return _plan;
                _planGo = new GameObject("NineMileCreekMainland_Test");
                _plan = _planGo.AddComponent<MainlandTidalTerrain>();
                NineMileCreekMainland.ConfigureTerrain(_plan);
                return _plan;
            }
        }

        /// <summary>St Peters' terrain, built through the call its builder makes.</summary>
        private TidalTerrain Island
        {
            get
            {
                if (_island != null) return _island;
                _islandGo = new GameObject("StPeters_SeamReference");
                _island = _islandGo.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(_island);
                return _island;
            }
        }

        private float Elev(Vector2 p) => _ground.ElevationAt(p);
        private float Elev(Vector3 p) => _ground.ElevationAt(new Vector2(p.x, p.y));
        private float PlanElev(Vector2 p) => Plan.ElevationAt(p);
        private float PlanElev(Vector3 p) => Plan.ElevationAt(new Vector2(p.x, p.y));

        // =============================================================================================
        //  the region rectangle holds what it is asked to hold
        // =============================================================================================

        [Test]
        public void EverythingAuthoredSitsInsideTheRegionsOwnRectangle()
        {
            Vector2 half = NineMileCreekMainland.RegionWorldSize * 0.5f;
            Vector2 centre = NineMileCreekMainland.RegionWorldCenter;

            void Inside(Vector2 p, string what)
            {
                Assert.That(Mathf.Abs(p.x - centre.x), Is.LessThanOrEqualTo(half.x),
                    $"{what} at {p} is outside the region rectangle in X");
                Assert.That(Mathf.Abs(p.y - centre.y), Is.LessThanOrEqualTo(half.y),
                    $"{what} at {p} is outside the region rectangle in Y");
            }

            for (int i = 0; i < NineMileCreekMainland.CoastPoints.Length; i++)
                Inside(NineMileCreekMainland.CoastPoints[i], $"coast point {i}");

            Inside(NineMileCreekMainland.BarFrom, "the bar's shoreward end");
            Inside(NineMileCreekMainland.BarTo, "the bar's seam end");
            Inside(NineMileCreekMainland.ToStPetersPassagePos, "the St Peters passage");
            Inside(NineMileCreekMainland.WalkArrivalPos, "the walk-in arrival");
            Inside(NineMileCreekMainland.ToCovePassagePos, "the Coddle Cove passage");
            Inside(NineMileCreekMainland.DockZonePos, "the dock zone");
            Inside(NineMileCreekMainland.ArrivalPos, "the boat arrival");
            Inside(NineMileCreekMainland.DisembarkPos, "the disembark point");
            // The scene's own arrival, on the third issue's quay (NMC H2).
            Inside(NineMileCreekBuilder.DockZonePos, "the scene's dock zone");
            Inside(NineMileCreekBuilder.ArrivalPos, "the scene's boat arrival");
            Inside(NineMileCreekBuilder.DisembarkPos, "the scene's disembark point");

            foreach (var lot in NineMileCreekMainland.TownLots) Inside(lot, "a town lot");
            foreach (var shanty in NineMileCreekMainland.ShantyRow) Inside(shanty, "a shanty");

            // Both networks: the lines the scene paves and the plan's own.
            foreach (var route in SceneRoutes())
                for (int i = 0; i < route.Length; i++) Inside(route[i], "a road node");
            foreach (var route in PlanRoutes())
                for (int i = 0; i < route.Length; i++) Inside(route[i], "a plan road node");
        }

        [Test]
        public void TheSeabedTextureThisExtentAsksForIsAffordable()
        {
            var def = ScriptableObject.CreateInstance<RegionDef>();
            try
            {
                def.WorldCenter = NineMileCreekMainland.RegionWorldCenter;
                def.WorldSizeMeters = NineMileCreekMainland.RegionWorldSize;
                def.SeabedPixelsPerMetre = NineMileCreekMainland.SeabedPixelsPerMetre;

                Assert.That(def.HasUsableExtent, Is.True,
                    $"{def.SeabedTexels} texels is not a paintable map — check pixels-per-metre");
                Assert.That(def.SeabedBytes, Is.LessThan(4 * 1024 * 1024),
                    $"the painted seabed costs {def.SeabedBytes / 1024f / 1024f:0.00} MiB of R8; a " +
                    "region asking for more than a few has almost certainly mistyped its resolution");
            }
            finally { Object.DestroyImmediate(def); }
        }

        // =============================================================================================
        //  ⭐ THE CROSSING — one bar, two regions
        // =============================================================================================

        [Test]
        public void TheMainlandRunsTheTideStPetersDoes()
        {
            // Not a style choice. The bar's exposure is a function of (crest, amplitude, phase), and the
            // bar spans the seam — so two tides means two bars.
            Assert.That(NineMileCreekMainland.TideMean, Is.EqualTo(StPetersBuilder.TideMean).Within(1e-4f));
            Assert.That(NineMileCreekMainland.TideAmplitude,
                Is.EqualTo(StPetersBuilder.TideAmplitude).Within(1e-4f));
            Assert.That(NineMileCreekMainland.TidePhaseHours,
                Is.EqualTo(StPetersBuilder.TidePhaseHours).Within(1e-4f),
                "a phase offset between the two halves would put high water on one side of the seam and " +
                "low water on the other, which is worse than a different amplitude, not better");

            // And the plan's analytic ground must have been authored against that same tide. (The scene's
            // painted G3 ground carries no tide reference: its heights are metres, NMC H2.)
            Assert.That(Plan.TideAmplitude,
                Is.EqualTo(NineMileCreekMainland.TideAmplitude).Within(1e-4f),
                "the terrain's authoring reference must match the region's own tide, or every " +
                "tide-fraction elevation in it means something else");
        }

        [Test]
        public void TheBarsCrestIsATideFraction_AndItIsStPetersCrest()
        {
            Assert.That(NineMileCreekMainland.BarCrestElevation,
                Is.EqualTo(StPetersBuilder.SandbarCrestElevation).Within(1e-4f),
                "a bar with two crests is two bars");

            Assert.That(NineMileCreekMainland.BarCrestElevation,
                Is.EqualTo(0.4f * NineMileCreekMainland.TideAmplitude).Within(1e-3f),
                "the crest is a RATIO of the amplitude (0.4), not a height — that ratio is what keeps " +
                "the bar flooding at every point of the lunar month when the owner retunes the tide");

            Assert.That(NineMileCreekMainland.BarCrestElevation,
                Is.LessThan(0.45f * NineMileCreekMainland.TideAmplitude),
                "the crest must clear NEAP high water from below, or for part of every lunar month the " +
                "bar never floods and the crossing's one tide gate silently switches itself off");
        }

        [Test]
        public void TheSeamSitsAtTheBarsMidpoint()
        {
            float island = Vector2.Distance(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo);
            float mainland = Vector2.Distance(NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo);

            Assert.That(mainland, Is.EqualTo(island).Within(island * 0.02f),
                $"the two halves are {island:0.0} m and {mainland:0.0} m; the seam is authored as the " +
                "bar's MIDPOINT, so they should match to within a couple of percent");

            Assert.That(NineMileCreekMainland.CrossingTotalMetres, Is.EqualTo(island + mainland).Within(0.01f));
        }

        [Test]
        public void TheCrossingIsOneBarEitherSideOfTheSeam()
        {
            // The ground under each region's passage band. Walk out of one and you stand on the other:
            // if these disagree, the crossing bares at a different tide on each side of the load. This
            // side is the scene's G3 ground (NMC H2).
            float islandGround = Island.ElevationAt(
                new Vector2(StPetersBuilder.ToNineMileCreekPassagePos.x,
                            StPetersBuilder.ToNineMileCreekPassagePos.y));
            float mainlandGround = Elev(NineMileCreekMainland.ToStPetersPassagePos);

            Assert.That(mainlandGround, Is.EqualTo(islandGround).Within(0.05f),
                $"the seam's ground is {islandGround:0.000} m on St Peters' side and " +
                $"{mainlandGround:0.000} m on the mainland's — they must bare at the same tide");

            Assert.That(mainlandGround, Is.GreaterThan(SpringLow),
                "and both must actually BARE at spring low, or the crossing has a swim in the middle");

            // The flats must not visibly narrow across the load either. This is what the bar's shoulder
            // is for: the mainland's bay floor is -6 m where St Peters' is -4 m, and without a matching
            // shoulder the same half-width would bare a quarter narrower on this side.
            float islandWidth = BaredHalfWidth(Island, StPetersBuilder.SandbarFrom,
                                               StPetersBuilder.SandbarTo, 0.6f);
            float mainlandWidth = BaredHalfWidth(_ground, NineMileCreekMainland.BarFrom,
                                                 NineMileCreekMainland.BarTo, 0.6f);

            Assert.That(mainlandWidth, Is.EqualTo(islandWidth).Within(1f),
                $"the bar bares {islandWidth:0.0} m either side on St Peters and {mainlandWidth:0.0} m " +
                "here; a crossing that changes width at a scene load reads as a bug");
        }

        [Test]
        public void TheBarBaresFromTheCrestDownAndTheGutStaysWet()
        {
            Vector2 from = NineMileCreekMainland.BarFrom;
            Vector2 to = NineMileCreekMainland.BarTo;

            // On the crest, clear of the gut, the bar stands at its authored height.
            Vector2 crestPoint = Vector2.Lerp(from, to, 0.8f);
            Assert.That(Elev(crestPoint),
                Is.EqualTo(NineMileCreekMainland.BarCrestElevation).Within(0.02f));

            // The gut is a trough THROUGH the bar — boat-crossable at higher water, shoaling as the tide
            // falls, and never as deep as the open bay.
            Vector2 gutPoint = Vector2.Lerp(from, to, NineMileCreekMainland.GutAlong);
            float gut = Elev(gutPoint);
            Assert.That(gut, Is.EqualTo(NineMileCreekMainland.GutBedElevation).Within(0.02f));
            Assert.That(gut, Is.LessThan(NineMileCreekMainland.BarCrestElevation),
                "the gut has to be lower than the crest or it is not a gut");
            Assert.That(gut, Is.GreaterThan(NineMileCreekMainland.BayFloorElevation),
                "...and shallower than the bay, or it never shoals as the tide drops");
            Assert.That(gut, Is.GreaterThan(SpringLow),
                "the gut dries at dead low spring — that is what makes the crossing walkable end to end");
        }

        [Test]
        public void TheBarIsDrownedAtSpringHighAndBaredAtSpringLow()
        {
            Vector2 mid = Vector2.Lerp(NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo, 0.75f);
            float ground = Elev(mid);
            Assert.That(ground, Is.LessThan(SpringHigh),
                "the crossing must FLOOD — being cut off is the region's one lesson");
            Assert.That(ground, Is.GreaterThan(SpringLow),
                "...and must BARE, or there is no crossing to be cut off from");
        }

        // =============================================================================================
        //  the wharf — the ruled 1.6 m gate
        // =============================================================================================

        /// <summary>⚠ The plan's authoring since NMC H2, on the premise: the analytic trench along the plan's own
        /// berth line. The scene's berths lie on the third issue's wall in G3's dredged basin, and are held there by
        /// <c>NineMileCreekWharfTests.EveryBerth_IsWaterTheFleetCanLieIn_NotDryLandAndNotTheOpenBay</c>.</summary>
        [Test]
        public void EveryBerthLiesInTheTrenchThatWasCutForIt_NotOnTheDeckAndNotInTheOpenBay()
        {
            // ⚠ OWNER RULING 2026-09-04, and it moved this assertion: *"the bullpen should always have water at low tide so all the lobster boats can park on the wall."* The −1.6 m shoal is still the gate everywhere else in the basin — it is what the flats, the crossing and the guts are measured against — but along the berth line the trench (NineMileCreekMainland §8b¾) has cut it to the depth the fleet lies in. What used to be "exactly the gate" is now "deeper than the gate, by the trench's own arithmetic".
            float needed = NineMileCreekMainland.BerthDepthNeededMetres;
            for (int i = 0; i < NineMileCreekMainland.BerthCount; i++)
            {
                Vector2 berth = NineMileCreekMainland.BerthPos(i);
                float bed = PlanElev(berth);
                Assert.That(NineMileCreekMainland.SpringLowWater - bed,
                    Is.GreaterThanOrEqualTo(needed - 1e-3f),
                    $"berth {i} at {berth} sits on {bed:0.00} m and carries only " +
                    $"{NineMileCreekMainland.SpringLowWater - bed:0.00} m at dead low spring. The boats " +
                    "cannot park on the wall, which is the whole of the 2026-09-04 ruling.");
                Assert.That(bed, Is.GreaterThan(NineMileCreekMainland.BayFloorElevation),
                    $"berth {i} has been cut to the open bay's own floor — a berth in the bay is not a " +
                    "berth, and the trench is not supposed to reach that deep");
            }
        }

        [Test]
        public void TheBasinGateAdmitsTheStarterFleetAndMakesTheBiggestHullWaitForWater()
        {
            // The gate is emergent (waterLevel - bed > draught), so it is stated here as the DEPTH the
            // wharf offers at mean water — the number the wharf doc's three-harbour ladder rules on.
            float depthAtMeanWater = NineMileCreekMainland.TideMean - NineMileCreekMainland.BasinBedElevation;
            Assert.That(depthAtMeanWater, Is.EqualTo(1.6f).Within(0.01f),
                "the ruled ladder is St Peters ~0.6 m, Nine Mile Creek ~1.6 m, Port Greywick 6 m — this " +
                "is the LOBSTER-BOAT berth, which is the owner's stated ceiling for the starter world");

            // And it is genuinely a step between the other two, not a re-statement of either.
            Assert.That(depthAtMeanWater, Is.GreaterThan(0.6f), "deeper than St Peters' dock");
            Assert.That(depthAtMeanWater, Is.LessThan(6f), "shallower than Greywick's dredged harbour");
        }

        /// <summary>⚠ The plan's authoring since NMC H2, on the premise: the analytic fills and the plan's deck.
        /// The scene's quay is G3's (its deck at <see cref="NineMileCreekGroundPlan.DeckElevation"/>), held dry
        /// along its whole length by <c>NineMileCreekWharfTests.TheWholeQuay_StaysDryAtSpringHigh</c>.</summary>
        [Test]
        public void TheWharfIsDryAtEveryTideAndItsFaceIsATallOne()
        {
            Assert.That(PlanElev(NineMileCreekMainland.NorthWallFill.Center), Is.GreaterThan(SpringHigh),
                "the deck has to stay above the water the fleet is floating in");
            Assert.That(PlanElev(NineMileCreekMainland.WestWallFill.Center), Is.GreaterThan(SpringHigh));
            Assert.That(PlanElev(NineMileCreekMainland.SpitFill.Center), Is.GreaterThan(SpringHigh),
                "the yard carries the sheds and the trucks");
            Assert.That(PlanElev(NineMileCreekMainland.BreakwaterFill.Center), Is.GreaterThan(SpringHigh),
                "a breakwater awash at high water shelters nothing");

            // You step DOWN from the yard onto the wharf, which is how a working wharf is built.
            Assert.That(NineMileCreekMainland.SpitYardElevation,
                Is.GreaterThan(NineMileCreekMainland.WharfDeckElevation));

            // ⚠ The number Phase B has to solve against, asserted so it cannot drift silently: the quay
            // face is metres, not the 0.75 m the ISO kit bakes.
            float face = NineMileCreekMainland.WharfDeckElevation - NineMileCreekMainland.BasinBedElevation;
            Assert.That(face, Is.GreaterThan(4f),
                "a big-tide wharf has a tall face — Phase B must tile the kit's 24 px face vertically " +
                "rather than assume one course covers it");
        }

        [Test]
        public void ArrivingByBoatPutsYouInReachOfTheDeck()
        {
            // The scene's arrival (NMC H2): the builder's anchors, which are the third issue's
            // (NineMileCreekGroundPlan), on G3.
            float dockToArrival = Vector3.Distance(NineMileCreekBuilder.ArrivalPos,
                                                   NineMileCreekBuilder.DockZonePos);
            Assert.That(dockToArrival, Is.LessThan(NineMileCreekMainland.DockZoneRadius),
                "the persistent ControlSwitcher disembarks on a pure DISTANCE test, so a boat parked " +
                "outside the dock radius on arrival cannot be got off (the #52 playtest gap)");

            // ⚠ OWNER RULING 2026-09-04, and it moved this assertion: *"the bullpen should always have water at low tide so all the lobster boats can park on the wall."* The −1.6 m shoal is still the gate everywhere else in the basin — it is what the flats, the crossing and the guts are measured against — but along the berth line the trench (NineMileCreekMainland §8b¾) has cut it to the depth the fleet lies in. What used to be "exactly the gate" is now "deeper than the gate, by the trench's own arithmetic".
            // The dock zone and the arrival park sit ON the berth line, so they came with it:
            // you used to arrive at dead low water onto mud.
            float needed = NineMileCreekMainland.BerthDepthNeededMetres;
            Assert.That(NineMileCreekMainland.SpringLowWater - Elev(NineMileCreekBuilder.DockZonePos),
                Is.GreaterThanOrEqualTo(needed - 1e-3f),
                "you dock in the basin, and it holds water at every state of tide now");
            Assert.That(NineMileCreekMainland.SpringLowWater - Elev(NineMileCreekBuilder.ArrivalPos),
                Is.GreaterThanOrEqualTo(needed - 1e-3f),
                "and you arrive in water, not on a wall and not on mud");
            Assert.That(Elev(NineMileCreekBuilder.DisembarkPos),
                Is.EqualTo(NineMileCreekGroundPlan.DeckElevation).Within(0.05f),
                "and you step ashore onto the deck");
        }

        [Test]
        public void EveryWharfMarkerStandsOnMadeGroundThatStaysDry()
        {
            // On the scene's ground (G3, NMC H2): the sheds, the stores, the parking, the buyer and the dory.
            void Dry(Vector3 p, string what) =>
                Assert.That(Elev(p), Is.GreaterThan(SpringHigh),
                    $"{what} at {p} stands on {Elev(p):0.00} m — under spring high water ({SpringHigh:0.0} m)");

            foreach (var shanty in NineMileCreekMainland.ShantyRow) Dry(shanty, "a shanty");
            Dry(NineMileCreekMainland.BaitShedPos, "the bait shed");
            Dry(NineMileCreekMainland.TrapStorePos, "the trap store");
            Dry(NineMileCreekMainland.FishStorePos, "the fish store");
            Dry(NineMileCreekMainland.ParkingPos, "the parking");
            Dry(NineMileCreekMainland.FishBuyerPos, "the fish buyer's truck");
            Dry(NineMileCreekMainland.DerelictDoryPos, "the derelict dory");

            // Two of the plan's markers are later waves', so they are held on the plan's own made ground (its
            // AUTHORING) and claim nothing about the scene: at the winch site the scene's apron gear is stood
            // down by step 05, the davit's id kept for the crane (H5/N9), and the harbour beacon waits at the old
            // arm's head for the two head lights (H5; NineMileCreekWharfTests.TheBreakwaterHead_IsWhereTheBeaconStands).
            void PlanDry(Vector3 p, string what) =>
                Assert.That(PlanElev(p), Is.GreaterThan(SpringHigh),
                    $"{what} at {p} stands on {PlanElev(p):0.00} m of the plan's ground — under spring high water");

            PlanDry(NineMileCreekMainland.WinchPos, "the winch");
            PlanDry(NineMileCreekMainland.HarbourBeaconPos, "the harbour beacon");
        }

        [Test]
        public void TheShantiesDoNotStandInEachOther()
        {
            var row = NineMileCreekMainland.ShantyRow;
            float minimum = 2f * NineMileCreekMainland.WharfShedRadius;
            for (int i = 0; i < row.Length; i++)
                for (int j = i + 1; j < row.Length; j++)
                {
                    float d = Vector3.Distance(row[i], row[j]);
                    Assert.That(d, Is.GreaterThan(minimum),
                        $"shanties {i} and {j} are {d:0.0} m apart; each reserves " +
                        $"{NineMileCreekMainland.WharfShedRadius:0.0} m");
                }
        }

        // =============================================================================================
        //  the roads and the town
        // =============================================================================================

        [Test]
        public void EveryRoadStaysAboveTheHighestWaterAlongItsWholeLength()
        {
            // Sampled at a metre, not at the nodes: the first draft's bar road had dry NODES and ran
            // straight through the marsh pool in between.
            //
            // The scene's roads on the scene's ground (NMC H2): the bar road, Wharf Road and Route 91 on the
            // lines step 06 paves, and the gully path. Route 91 crosses the river on its bridge, so its samples
            // on the deck stand on the deck, not on the river under it, and the deck itself must clear the water.
            Rect deck = NineMileCreekGroundPlan.BridgeDeck;
            Assert.That(NineMileCreekGroundPlan.BridgeDeckElevation, Is.GreaterThan(SpringHigh),
                "the bridge deck itself must stand above spring high water");

            int onDeck = 0;
            foreach (var route in SceneRoutes())
            {
                for (int i = 0; i < route.Length - 1; i++)
                {
                    float length = Vector2.Distance(route[i], route[i + 1]);
                    int steps = Mathf.Max(2, Mathf.CeilToInt(length));
                    for (int k = 0; k <= steps; k++)
                    {
                        Vector2 p = Vector2.Lerp(route[i], route[i + 1], k / (float)steps);
                        if (ReferenceEquals(route, NineMileCreekGroundPlan.Route91) && deck.Contains(p))
                        {
                            onDeck++;
                            continue;
                        }
                        float e = Elev(p);
                        Assert.That(e, Is.GreaterThan(SpringHigh),
                            $"a road passes through {p} where the ground is {e:0.00} m — under spring " +
                            $"high water ({SpringHigh:0.0} m). Roads do not ford ponds.");
                    }
                }
            }
            Assert.That(onDeck, Is.GreaterThan(0), "Route 91 crosses no bridge: the river crossing is gone");
        }

        [Test]
        public void TheOwnersTwoNamedRoadsActuallyGoWhereHeSaidTheyGo()
        {
            // "From the crossing, a road the character can follow leads to Nine Mile Creek, the town."
            Vector2 landing = new Vector2(NineMileCreekMainland.BarFrom.x, NineMileCreekMainland.BarFrom.y);
            float roadToLanding = NineMileCreekMainland.DistanceToRoute(NineMileCreekMainland.BarRoad, landing);
            Assert.That(roadToLanding, Is.LessThan(25f),
                "the bar road has to start AT the crossing's landing, not near it");

            // "A road named Wharf Road runs all the way to Nine Mile Creek's wharf front." On the lines the scene
            // paves (NineMileCreekGroundPlan, NMC H2) Wharf Road climbs the final ramp and ends ON the turning
            // circle at the head of the yard, and the circle opens onto the yard's made ground; the yard's own
            // road beyond it is a later wave's.
            Vector2 roadEnd = NineMileCreekGroundPlan.WharfRoad[NineMileCreekGroundPlan.WharfRoad.Length - 1];
            Assert.That(Vector2.Distance(roadEnd, NineMileCreekGroundPlan.TurningPadCentre),
                Is.LessThan(NineMileCreekGroundPlan.TurningPadRadius),
                "Wharf Road has to reach the turning circle at the yard, not stop short of it");
            Vector2 intoTheYard = NineMileCreekGroundPlan.TurningPadExit;
            var yard = NineMileCreekMainland.SpitFill;
            Assert.That(Mathf.Abs(intoTheYard.x - yard.Center.x) <= yard.HalfSize.x &&
                        Mathf.Abs(intoTheYard.y - yard.Center.y) <= yard.HalfSize.y, Is.True,
                $"the circle opens at {intoTheYard}, outside the yard: Wharf Road has to reach the yard, not " +
                "stop at the shore");
            Assert.That(Elev(intoTheYard), Is.GreaterThan(SpringHigh),
                "and the yard it opens onto is made ground that stays dry");

            // The two meet, or the crossing does not connect to the town at all.
            float junctionToBarRoad =
                NineMileCreekMainland.DistanceToRoute(NineMileCreekMainland.BarRoad, NineMileCreekMainland.RoadJunction);
            float junctionToWharfRoad =
                NineMileCreekMainland.DistanceToRoute(NineMileCreekGroundPlan.WharfRoad, NineMileCreekMainland.RoadJunction);
            Assert.That(junctionToBarRoad, Is.LessThan(0.5f), "the junction must be ON the bar road");
            Assert.That(junctionToWharfRoad, Is.LessThan(0.5f), "and ON Wharf Road");

            // And the through-road passes through the town end of Wharf Road: Route 91, on the third issue's
            // line (NMC H2).
            Vector2 townEnd = NineMileCreekGroundPlan.WharfRoad[0];
            Assert.That(NineMileCreekMainland.DistanceToRoute(NineMileCreekGroundPlan.Route91, townEnd),
                Is.LessThan(0.5f), "Wharf Road starts on the through-road the community is strung along");
        }

        [Test]
        public void TheGullyPathLeavesTheRoadAndReachesTheAccessSector()
        {
            var path = NineMileCreekMainland.GullyPath;
            Assert.That(NineMileCreekMainland.DistanceToRoute(NineMileCreekMainland.BarRoad, path[0]),
                Is.LessThan(0.5f), "the gully path has to spur off the road, not float beside it");

            // Its far end must project onto an Access sector — the one way down to the foreshore.
            MainlandCoast.Project(NineMileCreekMainland.CoastPoints, path[path.Length - 1],
                                  out float along, out _);
            CoastClass c = MainlandCoast.ClassAt(NineMileCreekMainland.CoastSectors, along);
            Assert.That(c, Is.EqualTo(CoastClass.Access),
                $"the path ends at s={along:0.0} where the coast is {c}; it is supposed to reach the gully");
        }

        /// <summary>⚠ The plan's authoring since NMC H2, on the premise: the legacy zone math, kept as the town
        /// wave's own premise, on the town's five lots by id (<see cref="NineMileCreekMainland.TownLotIds"/>).
        ///
        /// <para>What moved in NMC H4, and why: the lots are G3's now, not the buildings'. The chandlery's is
        /// <c>lot.nmc_chandlery</c> on Water Street's north side; the farmhouse's, <c>lot.nmc_house_south</c>, is at
        /// (−200, −8), south of the hedge at y 0 (call 85); the Mill Road house's, <c>lot.nmc_house_mill</c>, is at
        /// (−264.5, 152), 2.5 m west, on the ground the tavern gave up for the plaza's parking; the boat shed's,
        /// <c>lot.nmc_boat_shed</c>, is on the river at (−94, 158). The parish hall's has not moved. The
        /// harbourmaster's, the general store's, the tavern's and the two north houses' lots are the plan's gone lots
        /// (<c>lots.gone</c>: the river valley takes the north two). The lots on the scene's ground, footprint by
        /// footprint, are <see cref="EveryG3LotStandsDryAloneAndOffTheRoads_SaveTheCreekLaneFourAndTwoOpenCalls"/>.</para>
        /// </summary>
        [Test]
        public void EveryTownLotIsOnDryFieldsClearOfItsNeighboursAndOffTheRoad()
        {
            var lots = NineMileCreekMainland.TownLots;
            float clearance = 2f * NineMileCreekMainland.TownLotRadius;

            for (int i = 0; i < lots.Length; i++)
            {
                Assert.That(PlanElev(lots[i]), Is.EqualTo(NineMileCreekMainland.LandElevation).Within(0.05f),
                    $"town lot {i} at {lots[i]} is not on the plateau — is it in a pond?");

                foreach (var route in PlanRoutes())
                {
                    float d = NineMileCreekMainland.DistanceToRoute(route, lots[i]);
                    Assert.That(d, Is.GreaterThan(NineMileCreekMainland.RoadHalfWidth
                                                  + NineMileCreekMainland.TownLotRadius),
                        $"town lot {i} at {lots[i]} is {d:0.0} m from a road — its footprint would " +
                        "stand in the carriageway");
                }

                for (int j = i + 1; j < lots.Length; j++)
                {
                    float d = Vector3.Distance(lots[i], lots[j]);
                    Assert.That(d, Is.GreaterThan(clearance),
                        $"town lots {i} and {j} are {d:0.0} m apart; each reserves " +
                        $"{NineMileCreekMainland.TownLotRadius:0.0} m at every facing");
                }
            }
        }

        /// <summary>
        /// ⭐ THE TOWN'S LOTS ON THE SCENE'S GROUND, FOOTPRINT BY FOOTPRINT (NMC H4). Every one of G3's 43 building
        /// lots (<see cref="NineMileCreekGroundMasks.Lots"/>: the plan's 25 town lots, 7 waterfront lots, 9 south-shore
        /// houses, the truck park and the laydown), its whole box sampled every half metre, stands on G3 above spring
        /// high water; no two boxes overlap; no box reaches into a town street's carriageway; and every box stands
        /// <see cref="NineMileCreekRoadNetwork.LotCorridorClearanceMetres"/> clear of every road corridor in road kit
        /// v4's network (the plan's roads of class route, paved, street or lane: the plan's lot rule), save the named.
        ///
        /// <para>⚠ The named, held exact: each must still be inside the clearance, by the road it names, and the day
        /// one clears, this asks for it to come off.</para>
        /// <list type="bullet">
        /// <item>The plan's own exception: Creek Lane (north)'s four waterfront lots, inside 3 m at their boxes'
        /// nearest corners (2.23, 2.89, 1.55 and 1.20 m). Lots 1, 3 and 4 were ruled 10-02 to stay as drawn; lot 2 is
        /// open (call 57).</item>
        /// <item><c>lot.nmc_boat_shed</c>: the river lane's drawn end stands 3 m inside the shed's west face. The lane
        /// is laid as drawn (ruled 10-23); where the shed stands is its own wave's, and open.</item>
        /// <item><c>lot.nmc_laydown</c>: Loop C's north arc passes 0.25 m off the apron's south edge, its lane strip.
        /// Open.</item>
        /// </list>
        ///
        /// <para>Replaces <c>EveryTownLotStandsOnDryGroundClearOfTheScenesRoads_SaveTheTwoNorthLotsTheRiverTakes</c>
        /// (NMC H2): the same premise, the town's lots dry on G3 and clear of the roads the scene carries, now on the
        /// lots' own boxes instead of a 6 m disc round nine building centres, and on v4's roads instead of four lines.
        /// Its two named lots, the north houses in the river valley, are among the plan's gone lots.</para>
        /// </summary>
        [Test]
        public void EveryG3LotStandsDryAloneAndOffTheRoads_SaveTheCreekLaneFourAndTwoOpenCalls()
        {
            var lots = NineMileCreekGroundMasks.Lots;
            var network = NineMileCreekRoadNetwork.Load();
            float clearance = NineMileCreekRoadNetwork.LotCorridorClearanceMetres;
            var named = new Dictionary<string, string>
            {
                { "lot.nmc_waterfront_1", "road.nmc_north_creek_lane" },
                { "lot.nmc_waterfront_2", "road.nmc_north_creek_lane" },
                { "lot.nmc_waterfront_3", "road.nmc_north_creek_lane" },
                { "lot.nmc_waterfront_4", "road.nmc_north_creek_lane" },
                { "lot.nmc_boat_shed", "road.nmc_stream_lane" },
                { "lot.nmc_laydown", "road.nmc_camp_loop_c" },
            };
            Assert.That(lots.Count, Is.EqualTo(43), "G3's plan has 43 building lots");

            for (int i = 0; i < lots.Count; i++)
            {
                var lot = lots[i];
                Rect box = LotBox(lot);

                // The lowest ground under the whole box, on a half-metre grid.
                float lowest = float.MaxValue;
                Vector2 lowestAt = box.center;
                for (float x = box.xMin; x <= box.xMax + 1e-3f; x += 0.5f)
                    for (float y = box.yMin; y <= box.yMax + 1e-3f; y += 0.5f)
                    {
                        float e = Elev(new Vector2(x, y));
                        if (e < lowest) { lowest = e; lowestAt = new Vector2(x, y); }
                    }
                Assert.That(lowest, Is.GreaterThan(SpringHigh),
                    $"{lot.Id} has ground of {lowest:0.00} m at {lowestAt}, under spring high water " +
                    $"({SpringHigh:0.0} m). A house does not stand in the tide.");

                for (int j = i + 1; j < lots.Count; j++)
                    Assert.That(box.Overlaps(LotBox(lots[j])), Is.False,
                        $"{lot.Id} and {lots[j].Id} stand on each other");

                float nearest = float.MaxValue;
                NineMileCreekRoadNetwork.Route by = null;
                foreach (var road in network.Routes)
                {
                    if (!road.IsPlanRoad) continue;
                    float d = road.EdgeDistanceTo(box);
                    if (road.Class == "town")
                        Assert.That(d, Is.GreaterThanOrEqualTo(0f),
                            $"{lot.Id} reaches {-d:0.00} m into {road}'s carriageway");
                    else if (road.IsCorridor && d < nearest) { nearest = d; by = road; }
                }

                if (named.TryGetValue(lot.Id, out string namedRoad))
                {
                    Assert.That(nearest, Is.LessThan(clearance),
                        $"{lot.Id} stands {nearest:0.00} m clear of every road corridor now: take it off the named list");
                    Assert.That(by.Id, Is.EqualTo(namedRoad), $"{lot.Id} is inside {clearance} m of {by}, not the road it was named for");
                }
                else
                    Assert.That(nearest, Is.GreaterThanOrEqualTo(clearance),
                        $"{lot.Id} stands {nearest:0.00} m from {by}'s carriageway edge, inside the plan's {clearance} m");
            }
        }

        // =============================================================================================
        //  the ponds, and the ground behind the shore
        // =============================================================================================

        [Test]
        public void BothPondsFillAndDrainWithTheTide()
        {
            // On the scene's ground (G3, NMC H2), at the plan's two carve centres.
            foreach (var pond in new[] { NineMileCreekMainland.BarachoisCarve,
                                         NineMileCreekMainland.MarshPoolCarve })
            {
                float bed = Elev(pond.Center);
                Assert.That(bed, Is.LessThan(SpringHigh),
                    "a pond behind the shore has to take the tide, or it is a field");
                Assert.That(bed, Is.GreaterThan(SpringLow),
                    "...and has to DRAIN, or it is never the red mudflat the photographs show");
                Assert.That(bed, Is.EqualTo(pond.Elevation).Within(0.05f),
                    "the carve must actually reach its authored bed — a carve over ground that is " +
                    "already lower does nothing at all (the defect that made the first draft's basin " +
                    "four metres too deep)");
            }
        }

        /// <summary>⚠ The plan's authoring since NMC H2, on the premise: in the analytic plan a mainland is a
        /// SIDE. G3 is not one: its south shore reaches up to y −180 and the river runs north of the town, so the
        /// scene's claim is the town's own ground (<see cref="EveryG3LotStandsDryAloneAndOffTheRoads_SaveTheCreekLaneFourAndTwoOpenCalls"/>).</summary>
        [Test]
        public void TheFieldsInlandAreDryEverywhereTheTownStands()
        {
            // A mainland is a SIDE, not a shape: inland of the coast run the ground is the plateau all
            // the way to the region edge. Spot-check the quarter the town occupies.
            for (float x = -360f; x <= -80f; x += 40f)
                for (float y = -260f; y <= 260f; y += 40f)
                {
                    Vector2 p = new Vector2(x, y);
                    // Skip the two ponds; they are meant to be wet.
                    if (InZone(p, NineMileCreekMainland.BarachoisCarve)) continue;
                    if (InZone(p, NineMileCreekMainland.MarshPoolCarve)) continue;
                    Assert.That(PlanElev(p), Is.GreaterThan(SpringHigh),
                        $"the fields at {p} are under water at spring high");
                }
        }

        [Test]
        public void TheOpenBayIsDeepEnoughThatNothingGroundsInIt()
        {
            foreach (var p in new[] { new Vector2(340f, 200f), new Vector2(300f, 260f),
                                      new Vector2(370f, 120f) })
                Assert.That(Elev(p), Is.EqualTo(NineMileCreekMainland.BayFloorElevation).Within(0.05f),
                    $"the open bay at {p} should be the region's floor");
        }

        /// <summary>⚠ The plan's authoring since NMC H2, on the premise: the analytic terrain and its memo. The
        /// scene's G3 ground is a decoded map, pinned code for code by <c>NineMileCreekGroundImportTests</c>.</summary>
        [Test]
        public void TheTerrainIsAPureFunctionOfPosition_AcrossSampleOrderAndAcrossRebuilds()
        {
            // Deterministic from geometry alone: no RNG, no clock, nothing saved (rule 5) — and the whole
            // simulation contract rests on it.
            //
            // ⚠ Asserting PlanElev(p) == PlanElev(p) would prove nothing (the compiler is free to fold it). The
            // two things that could actually break purity here are ORDER and CACHED STATE: the terrain
            // memoises the coast run's length rather than re-summing the polyline on all 1.7 million
            // samples of a seabed bake, so a stale cache is a live failure mode this feature introduced.
            // Sample forwards, rebuild the component from the plan, sample BACKWARDS, and compare.
            var probes = new[]
            {
                new Vector2(200f, -175f),   // the bar's crest
                new Vector2(-150f, 118f),   // a town lot
                new Vector2(128f, 92f),     // the wharf deck
                new Vector2(24f, 152f),     // inside the creek mouth's notch
                new Vector2(98f, 85f),      // a berth, over the gate
                new Vector2(340f, 200f),    // the open bay
                new Vector2(33f, -6f),      // the Access gully's foreshore
            };

            var forwards = new float[probes.Length];
            for (int i = 0; i < probes.Length; i++) forwards[i] = PlanElev(probes[i]);

            var rebuiltGo = new GameObject("NineMileCreekMainland_DeterminismRebuild");
            try
            {
                var rebuilt = rebuiltGo.AddComponent<MainlandTidalTerrain>();
                NineMileCreekMainland.ConfigureTerrain(rebuilt);

                for (int i = probes.Length - 1; i >= 0; i--)
                    Assert.That(rebuilt.ElevationAt(probes[i]), Is.EqualTo(forwards[i]).Within(0f),
                        $"the ground at {probes[i]} came out {rebuilt.ElevationAt(probes[i]):0.0000} on a " +
                        $"fresh build sampled in reverse, against {forwards[i]:0.0000} on the first — the " +
                        "height field depends on something other than the position");
            }
            finally { Object.DestroyImmediate(rebuiltGo); }
        }

        /// <summary>⚠ The plan's authoring since NMC H2, on the premise: the analytic terrain's one cache.</summary>
        [Test]
        public void ReAuthoringTheCoastlineInvalidatesTheCachedRunLength()
        {
            // The memoised run length is the one piece of mutable state on the terrain. If a re-author
            // does not clear it, the blend keeps measuring the LAST coastline's end — which reads as a
            // coast plan that silently refuses to move, the worst kind of bug to chase in a builder that
            // is meant to converge on re-run.
            var plan = Plan;
            float authored = plan.CoastRunLength;
            Assert.That(authored, Is.EqualTo(MainlandCoast.RunLength(NineMileCreekMainland.CoastPoints))
                .Within(0.01f));

            plan.CoastPoints = new[] { new Vector2(0f, -100f), new Vector2(0f, 100f) };
            Assert.That(plan.CoastRunLength, Is.EqualTo(200f).Within(0.01f),
                "re-authoring the coastline must invalidate the cached run length");

            NineMileCreekMainland.ConfigureTerrain(plan);
            Assert.That(plan.CoastRunLength, Is.EqualTo(authored).Within(0.01f),
                "and so must a full re-configure");
        }

        // ---- helpers --------------------------------------------------------------------------------

        /// <summary>The roads the scene carries: the bar road and the gully path, and Route 91 and Wharf Road on the
        /// lines step 06 paves (<see cref="NineMileCreekGroundPlan"/>, NMC H2).</summary>
        private static Vector2[][] SceneRoutes() => new[]
        {
            NineMileCreekMainland.BarRoad,
            NineMileCreekGroundPlan.WharfRoad,
            NineMileCreekGroundPlan.Route91,
            NineMileCreekMainland.GullyPath,
        };

        /// <summary>The plan's own roads: the premise's authoring.</summary>
        private static Vector2[][] PlanRoutes() => new[]
        {
            NineMileCreekMainland.BarRoad,
            NineMileCreekMainland.WharfRoad,
            NineMileCreekMainland.ThroughRoad,
            NineMileCreekMainland.GullyPath,
        };

        /// <summary>A G3 lot's box, as the masks cut round it.</summary>
        private static Rect LotBox(NineMileCreekGroundMasks.Lot lot) =>
            new Rect((float)(lot.X - lot.SizeX / 2), (float)(lot.Y - lot.SizeY / 2), (float)lot.SizeX, (float)lot.SizeY);

        private static bool InZone(Vector2 p, MainlandZone z) =>
            MainlandTidalTerrain.DistanceOutsideRect(p, z.Center, z.HalfSize) <= z.Falloff;

        /// <summary>
        /// How far either side of a bar's centre-line the ground is bared at spring low, measured by
        /// walking outward until the ground drops under the water. The one measure that says whether the
        /// crossing is the same WIDTH on both sides of the seam.
        /// </summary>
        private static float BaredHalfWidth(ITidalTerrain terrain, Vector2 from, Vector2 to, float along)
        {
            Vector2 axis = (to - from).normalized;
            Vector2 normal = new Vector2(-axis.y, axis.x);
            Vector2 centre = Vector2.Lerp(from, to, along);

            float bared = 0f;
            for (float offset = 0f; offset <= 60f; offset += 0.25f)
            {
                if (terrain.ElevationAt(centre + normal * offset) <= SpringLow) break;
                bared = offset;
            }
            return bared;
        }
    }
}
