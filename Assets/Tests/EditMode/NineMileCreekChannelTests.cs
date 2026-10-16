using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.Boats;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE WATER THE BULLPEN KEEPS</b> — the owner's 2026-08-19 ruling, as arithmetic.
    ///
    /// <para>Referencing the real Nine Mile Creek chart he ruled that the wharf's bullpen and its
    /// channel ALWAYS hold water, even at spring low; that the channel SHRINKS IN WIDTH as the tide
    /// falls but always leaves enough to navigate out; and that it MEANDERS — an underwater river.
    /// This file is that ruling made checkable, and it is deliberately paranoid in both directions:
    /// the channel must be wet, and <b>everything the ruling did NOT name must still dry</b>.</para>
    ///
    /// <para><b>The reversal is two sites, not a new global rule.</b> Nine Mile Creek's teeth are its
    /// −1.6 m shoal: the fleet floats on the flood and takes the ground on the ebb, and four committed
    /// tests hold it. So the berths, the dock zone and the arrival park are asserted here to be
    /// EXACTLY untouched — the channel is a lane cut past them, not a deeper harbour.</para>
    ///
    /// <para><b>Two grounds since NMC H2.</b> The scene stands on G3 (<see cref="NineMileCreekLiveGround.Plan"/>):
    /// its dredged basin holds the berths' water from wall to wall, and the way out is the design boat's TRACK
    /// through the mouth between the arms (<c>NineMileCreekDesignBoatTests</c> sweeps her hull along it). What
    /// the scene must keep — the berths float, every berth reaches the bay, the flats outside the basin still
    /// bare, the gut still dries, the drawn faces meet the bed — is asserted on G3. The analytic channel (the
    /// meander, its banks, the berth trench and the cuttable ceiling) is the plan's AUTHORING through
    /// <see cref="NineMileCreekMainland.ConfigureTerrain"/>, the premise the later waves still build on: its tests
    /// are kept on that premise, each says so, and none of them claims the activated scene.</para>
    /// </summary>
    public class NineMileCreekChannelTests
    {
        private HiddenHarbours.Core.ITidalTerrain _terrain;   // G3: the ground the scene stands on
        private GameObject _planGo;
        private MainlandTidalTerrain _plan;      // the analytic premise, stood up only by the authoring tests

        private static float SpringLow => NineMileCreekMainland.SpringLowWater;    // -2.2
        private static float SpringHigh => NineMileCreekMainland.SpringHighWater;  //  2.2
        private static float Shoal => NineMileCreekMainland.BasinBedElevation;     // -1.6

        [SetUp]
        public void SetUp() => _terrain = NineMileCreekLiveGround.Plan;

        [TearDown]
        public void TearDown()
        {
            if (_planGo != null) Object.DestroyImmediate(_planGo);
            _planGo = null;
            _plan = null;
        }

        /// <summary>The plan's analytic ground, built through the call the builder still makes for the premise.</summary>
        private MainlandTidalTerrain Plan
        {
            get
            {
                if (_plan != null) return _plan;
                _planGo = new GameObject("NineMileCreekChannel_Test");
                _plan = _planGo.AddComponent<MainlandTidalTerrain>();
                NineMileCreekMainland.ConfigureTerrain(_plan);
                return _plan;
            }
        }

        private float Elev(Vector2 p) => _terrain.ElevationAt(p);
        private float Depth(Vector2 p, float water) => water - Elev(p);
        private float PlanElev(Vector2 p) => Plan.ElevationAt(p);
        private float PlanDepth(Vector2 p, float water) => water - PlanElev(p);

        // =============================================================================================
        //  1. THE DERIVATION — every number traced back to the fleet, not typed
        // =============================================================================================

        /// <summary>
        /// ⭐ The thalweg is arithmetic, and this is the arithmetic: spring low, less the deepest
        /// resident's draught, her clearance and the dredger's over-cut. If any input moves, the bed
        /// moves with it — which is the entire reason none of them is a literal.
        /// </summary>
        [Test]
        public void TheThalwegIsDerivedFromTheDeepestResident_NotTyped()
        {
            float expected = SpringLow - (NineMileCreekMainland.DeepestResidentDraughtMetres
                                          + NineMileCreekMainland.ChannelKeelClearanceMetres
                                          + NineMileCreekMainland.ChannelDredgeMarginMetres);

            Assert.That(NineMileCreekMainland.ChannelThalwegElevation, Is.EqualTo(expected).Within(1e-4f),
                "the thalweg must BE the derivation, not a number that happens to agree with it");
            Assert.That(NineMileCreekMainland.ChannelThalwegElevation, Is.EqualTo(-3.90f).Within(0.01f),
                "…and today that arithmetic is −2.20 − (1.40 + 0.20 + 0.10) = −3.90 m");

            Assert.That(NineMileCreekMainland.ChannelThalwegElevation,
                Is.GreaterThan(NineMileCreekMainland.BayFloorElevation),
                "a channel cut to the bay floor has no gradient left to run out on — and the composition " +
                "clamps at the floor, so it would silently stop being a channel at all");
        }

        /// <summary>
        /// ⭐ <b>THE PIN.</b> The two fleet numbers the channel is cut for are stated as constants so the
        /// geometry can be pure, and re-measured here off the actual owner defs so they cannot drift.
        /// Berth a deeper boat at this wharf and this fails BY NAME rather than grounding her.
        /// </summary>
        [Test]
        public void TheConstantsMatchTheFleetTheyClaimToDescribe()
        {
            List<BoatOwnerDef> owners = NineMileCreekMooredFleet.LoadOwners();
            Assert.IsNotEmpty(owners, "no owner defs — the channel would be cut for nobody");

            float deepest = 0f; string deepestWho = "(none)";
            float widest = 0f; string widestWho = "(none)";
            float longest = 0f; string longestWho = "(none)";
            var beamless = new List<string>();

            foreach (BoatOwnerDef o in owners)
            {
                if (o == null || o.Boat == null) continue;

                if (o.Boat.DraughtMeters > deepest)
                {
                    deepest = o.Boat.DraughtMeters;
                    deepestWho = $"{o.DisplayName}'s {o.Boat.Id}";
                }

                // ⚠ Measured HERE, above the beam block, because that block `continue`s past a
                // sprite-only boat — and length is a number every boat def carries, mesh or not.
                if (o.Boat.LengthMeters > longest)
                {
                    longest = o.Boat.LengthMeters;
                    longestWho = $"{o.DisplayName}'s {o.Boat.Id}";
                }

                // ⚠ A SPRITE-ONLY boat has no hull mesh and therefore no measured beam (Bernard's skiff
                // is the standing example). Collected rather than skipped silently: if the widest boat
                // here ever turns out to be one of them, the beam constant is unpinned and must say so.
                HiddenHarbours.Core.HullMeshDef mesh = o.Boat.Visual != null ? o.Boat.Visual.HullMesh : null;
                if (mesh == null || mesh.WatertightHalfBeamMeters <= 0f)
                {
                    beamless.Add($"{o.Id} ({o.Boat.Id})");
                    continue;
                }

                float beam = mesh.WatertightHalfBeamMeters * 2f;
                if (beam > widest) { widest = beam; widestWho = $"{o.DisplayName}'s {o.Boat.Id}"; }
            }

            Assert.That(NineMileCreekMainland.DeepestResidentDraughtMetres, Is.EqualTo(deepest).Within(0.005f),
                $"the deepest boat berthed here is {deepestWho} at {deepest:0.00} m, but the channel is " +
                $"cut for {NineMileCreekMainland.DeepestResidentDraughtMetres:0.00} m. A channel dredged " +
                "for the wrong boat strands the one it forgot.");

            Assert.That(NineMileCreekMainland.WidestResidentBeamMetres, Is.EqualTo(widest).Within(0.005f),
                $"the widest measured hull here is {widestWho} at {widest:0.00} m of beam, against the " +
                $"authored {NineMileCreekMainland.WidestResidentBeamMetres:0.00} m. The navigable-width " +
                $"floor is derived from it. (Sprite-only, unmeasurable: {string.Join(", ", beamless)})");

            // ⭐ THE WALL'S OWN PIN. The beam above says how far off the timber a boat lies; this
            // says how much of it she takes up, and NineMileCreekBerthLineTests spends it on the
            // berth line. Berth a longer boat here and this fails by name rather than drawing her
            // through her neighbour.
            Assert.That(NineMileCreekMainland.LongestResidentLengthMetres, Is.EqualTo(longest).Within(0.005f),
                $"the longest boat berthed here is {longestWho} at {longest:0.00} m, against the "
                + $"authored {NineMileCreekMainland.LongestResidentLengthMetres:0.00} m. The berth "
                + "line's spans are derived from it, so an unpinned value is two hulls in one place.");
        }

        // =============================================================================================
        //  2. THE RULING — the channel holds water, and never closes
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>THE RULING ITSELF.</b> Every authored station on the meander carries the deepest
        /// resident plus her clearance at the WORST hour of the month. Asserted along the AUTHORED
        /// spline rather than at a searched minimum — a found minimum slides downhill and would pass by
        /// measuring somewhere the boat is not.
        ///
        /// <para>⚠ <b>The plan's authoring since NMC H2, not the scene's water.</b> G3 has no meander: its basin
        /// is dredged wall to wall and the way out is the TRACK. That the scene's berths reach the bay with no dry
        /// gap is <see cref="EveryBerthCanLeaveOnAnyTide_TheWholeWayToTheBay"/>, on G3.</para>
        /// </summary>
        [Test]
        public void EveryStationOnTheAuthoredChannelIsWetAtSpringLow()
        {
            float needed = NineMileCreekMainland.DeepestResidentDraughtMetres
                           + NineMileCreekMainland.ChannelKeelClearanceMetres;

            foreach (Vector2 p in WalkTheChannel(0.5f))
            {
                float depth = PlanDepth(p, SpringLow);
                Assert.That(depth, Is.GreaterThanOrEqualTo(needed),
                    $"the channel carries only {depth:0.000} m at ({p.x:0.#}, {p.y:0.#}) at spring low, " +
                    $"against the {needed:0.00} m the deepest resident needs. The owner's ruling is that " +
                    "this lane ALWAYS floats her out.");
            }
        }

        /// <summary>
        /// ⚠⚠ <b>THE TEST THAT CAUGHT THE ONE REAL DEFECT IN THIS PASS — "navigate OUT", end to end.</b>
        ///
        /// <para>Every other test here walks the channel's own line, and a channel can be wet at every
        /// station of its own line and still go NOWHERE. The first draft stopped at the basin entrance
        /// and left <b>7 m of drying ground</b> between the lane and the bay, because the shoal's seaward
        /// flank does not fall past spring low until x ≈ 196. Depth-along-the-line said 1.70 m
        /// everywhere; the boat was still shut in. <b>A lane is only a channel if it JOINS something.</b></para>
        ///
        /// <para>So this walks the region's authored way to sea — <c>Entrance</c>'s own waypoints, the
        /// line the fleet actually sails — from the basin out to the landfall in the −6 m bay, and
        /// demands the deepest resident's water the whole way at the worst hour of the month.</para>
        ///
        /// <para><b>On G3 since NMC H2.</b> The entrance now runs from the landfall up the TRACK into the mouth
        /// between the arms; inside the mouth the berths' join is
        /// <see cref="EveryBerthCanLeaveOnAnyTide_TheWholeWayToTheBay"/>. The sill check moved with it: the
        /// analytic cut's seaward end is not the scene's, so it is the entrance's own landfall that must stand on
        /// natural bottom deeper than the thalweg.</para>
        /// </summary>
        [Test]
        public void TheChannelJoinsTheDeepBay_SoAHullCanActuallyGetOutAtSpringLow()
        {
            float needed = NineMileCreekMainland.DeepestResidentDraughtMetres
                           + NineMileCreekMainland.ChannelKeelClearanceMetres;

            Vector2[] approach = NineMileCreekNavMarks.Entrance.Waypoints;
            Assert.That(approach.Length, Is.GreaterThan(1), "the entrance must be a route");

            float worst = float.MaxValue; Vector2 worstAt = Vector2.zero;
            for (int i = 0; i < approach.Length - 1; i++)
            {
                Vector2 a = approach[i], b = approach[i + 1];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.5f));
                for (int k = 0; k <= n; k++)
                {
                    Vector2 p = Vector2.Lerp(a, b, k / (float)n);
                    float d = Depth(p, SpringLow);
                    if (d < worst) { worst = d; worstAt = p; }
                }
            }

            Assert.That(worst, Is.GreaterThanOrEqualTo(needed),
                $"the way out shoals to {worst:0.00} m at ({worstAt.x:0.#}, {worstAt.y:0.#}) at spring " +
                $"low, against the {needed:0.00} m the deepest resident needs. The bullpen holds its " +
                "water and the fleet is still shut in — a lane that does not JOIN the bay is not a way out.");

            // …and the seaward end must sit on ground the BAY already carries deeper than the thalweg,
            // which is what proves the cut has run out into real water rather than stopping on a sill.
            Vector2 seaward = approach[0];
            Assert.That(Elev(seaward), Is.LessThan(NineMileCreekMainland.ChannelThalwegElevation),
                $"the way out's seaward end at {seaward} stands on {Elev(seaward):0.00} m, which a cut " +
                "would have had to make. Carry the route further out until the natural bottom takes over, or " +
                "there is no proof the lane does not simply stop somewhere shallower still.");
        }

        /// <summary>
        /// ⭐ <b>"…but always leaves enough to navigate out."</b> The floor is derived rather than felt:
        /// one widest beam, with half a beam of water either side of her.
        ///
        /// <para>⚠ The plan's authoring since NMC H2 (the meander's banks, on the premise): G3's basin has no lane
        /// to narrow, and its way out is held to the design boat's whole swept hull instead.</para>
        /// </summary>
        [Test]
        public void TheChannelNeverNarrowsPastTheNavigableFloorAtSpringLow()
        {
            float floor = NineMileCreekMainland.NavigableWidthFloorMetres;
            Assert.That(floor, Is.EqualTo(10f).Within(0.01f), "5.0 m of beam, doubled");

            foreach (Vector2 p in WalkTheChannel(2f))
            {
                float w = LaneWidthAcross(p, SpringLow);
                Assert.That(w, Is.GreaterThanOrEqualTo(floor),
                    $"at ({p.x:0.#}, {p.y:0.#}) the lane closes to {w:0.00} m at spring low, under the " +
                    $"{floor:0.0} m a {NineMileCreekMainland.WidestResidentBeamMetres:0.0} m beam needs.");
            }
        }

        /// <summary>
        /// ⭐ <b>"The channel SHRINKS IN WIDTH at low tide."</b> Nothing animates it — the waterline is
        /// where the falling water meets a sloping bank — so the shrink is a property of the section and
        /// is measured as one: monotone as the water drops, and never zero.
        ///
        /// <para>⚠ The plan's authoring since NMC H2, on the premise. G3's dredged basin does not visibly
        /// narrow on the ebb: an owner question in the PR, not a claim this test makes about the scene.</para>
        /// </summary>
        [Test]
        public void TheLaneNarrowsAsTheTideFalls_AndNeverCloses()
        {
            // The station nearest the float run's midpoint — named by GEOMETRY rather than by array
            // index, so re-authoring the meander (or extending it seaward, as the sill fix did) moves
            // the measurement with the channel instead of silently pointing it somewhere else.
            Vector2 at = NearestChannelStationTo(new Vector2(
                (NineMileCreekMainland.FloatRunWestX + NineMileCreekMainland.FloatRunEastX) * 0.5f,
                NineMileCreekMainland.FloatRunY));

            float previous = float.MaxValue;
            for (float water = Shoal; water >= SpringLow - 1e-4f; water -= 0.1f)
            {
                float w = LaneWidthAcross(at, water);
                Assert.That(w, Is.GreaterThan(0f),
                    $"the lane closed completely at a water level of {water:0.00} m");
                Assert.That(w, Is.LessThanOrEqualTo(previous + 1e-3f),
                    $"the lane WIDENED as the water fell (to {w:0.00} m at {water:0.00} m) — a bank that " +
                    "does that is not a bank");
                previous = w;
            }

            float top = LaneWidthAcross(at, Shoal - 0.05f);
            float low = LaneWidthAcross(at, SpringLow);
            Assert.That(low, Is.LessThan(top * 0.9f),
                $"the shrink has to be VISIBLE: {top:0.0} m down to {low:0.0} m is what the owner asked " +
                "to see on the ebb, and a lane that barely moves reads as a canal");
        }

        /// <summary>
        /// The floats are the whole reason the bullpen's own water matters (S2 hangs a tide-following
        /// deck here, S3 moors the small craft on it). Every metre of the authored float run must float
        /// the deepest SMALL craft at spring low. On G3 since NMC H2: the float keeps its place in the scene.
        /// </summary>
        [Test]
        public void TheFloatRunLiesOverWaterAtEveryStateOfTheTide()
        {
            const float DeepestSmallCraftDraught = 0.50f;   // the punt — Dan Peters', berth 11
            float needed = DeepestSmallCraftDraught + NineMileCreekMainland.ChannelKeelClearanceMetres;

            for (float x = NineMileCreekMainland.FloatRunWestX;
                 x <= NineMileCreekMainland.FloatRunEastX; x += 0.5f)
            {
                var p = new Vector2(x, NineMileCreekMainland.FloatRunY);
                float depth = Depth(p, SpringLow);
                Assert.That(depth, Is.GreaterThanOrEqualTo(needed),
                    $"the float run carries {depth:0.00} m at x = {x:0.#} at spring low, against the " +
                    $"{needed:0.00} m a punt needs. A float that grounds is a jetty.");
            }
        }

        // =============================================================================================
        //  3. ⚠ THE REVERSAL IS TWO SITES — everything else still dries
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>THE BERTHS HOLD WATER AT SPRING LOW — the owner's 2026-09-04 reversal, measured.</b>
        /// *"the bullpen should always have water at low tide so all the lobster boats can park on the
        /// wall."* This test was the opposite of itself until then: it asserted every berth still lay on
        /// the −1.6 m gate to a tolerance of 1e-4, because the 08-19 pass read the ruling as the channel
        /// only. It now asserts the ruling that replaced it, and the dock zone and the arrival park come
        /// with the berths — they sit ON the berth line (y = 84.5, half a metre off it), which is why
        /// arriving by boat at dead low water used to put you down on mud.
        ///
        /// <para><b>Two depths, because a hull is not a point.</b> On the berth mark the trench carries
        /// the fairway's own standard; at the outboard edge of the widest hull's footprint it carries
        /// what she needs to float. The second is the one the bed was solved for and the one a
        /// flat-bottomed reading of the cut would have missed by 0.26 m.</para>
        ///
        /// <para><b>On G3 since NMC H2</b>, at the scene's berths (the player's alongside the north face, the
        /// wall berths a metre south with the face) and the scene's dock zone and arrival.</para>
        /// </summary>
        [Test]
        public void EveryBerthHoldsWaterAtSpringLow_AndSoDoTheDockZoneAndTheArrival()
        {
            float onTheMark = NineMileCreekMainland.DeepestResidentDraughtMetres
                            + NineMileCreekMainland.ChannelKeelClearanceMetres
                            + NineMileCreekMainland.ChannelDredgeMarginMetres;
            float underTheHull = NineMileCreekMainland.BerthDepthNeededMetres;
            float foot = NineMileCreekMainland.BerthFootprintHalfWidthMetres;

            foreach (var (i, berth) in NineMileCreekWharfTests.SceneBerths())
            {
                Assert.That(SpringLow - Elev(berth), Is.GreaterThanOrEqualTo(onTheMark - 1e-3f),
                    $"berth {i} at {berth} carries {SpringLow - Elev(berth):0.00} m at spring low against " +
                    $"the {onTheMark:0.00} m the fairway that leads to it is cut for. The boats cannot " +
                    "park on the wall, which is the whole of the ruling.");

                // …and out to the far side of the widest hull that lies here. SEAWARD only: the wall's
                // own fill stands up on the other side and a channel may not cut a wharf away.
                for (float dy = 0f; dy <= foot + 1e-4f; dy += 0.25f)
                {
                    Vector2 p = new Vector2(berth.x, berth.y - dy);
                    Assert.That(SpringLow - Elev(p), Is.GreaterThanOrEqualTo(underTheHull - 1e-3f),
                        $"berth {i} carries only {SpringLow - Elev(p):0.00} m at {dy:0.00} m outboard of " +
                        $"its mark, against the {underTheHull:0.00} m the widest resident needs. A hull " +
                        "is not a point and a smoothstep cut is shallowest at the edge of her.");
                }
            }

            foreach (var (what, p) in new (string, Vector2)[]
                     {
                         ("the dock zone", NineMileCreekGroundPlan.DockZonePos),
                         ("the arrival park", NineMileCreekGroundPlan.ArrivalPos),
                     })
            {
                Assert.That(SpringLow - Elev(new Vector2(p.x, p.y)),
                    Is.GreaterThanOrEqualTo(underTheHull - 1e-3f),
                    $"{what} at {p} carries {SpringLow - Elev(new Vector2(p.x, p.y)):0.00} m at spring " +
                    "low — you would arrive by boat onto mud");
            }
        }

        /// <summary>
        /// ⚠⚠ <b>THE JOURNEY, NOT THE TRENCH — the law this region earned the first time (#583).</b> A
        /// lane can be wet at every station of its own line and still go nowhere. Walked from berth 1 and
        /// from berth 14 all the way to the entrance's own seaward waypoint, every metre must carry the
        /// fairway's depth at spring low.
        ///
        /// <para><b>Sampled off both arrays on purpose.</b> The stations below are struck every metre
        /// ALONG the polylines rather than taken from their vertices, so the fixture cannot be satisfied
        /// by the very points the geometry was authored from — the blind spot that hid #583's original
        /// 7 m sill. Measured before this test existed: without the trench's junction leg the worst
        /// station on this walk is <b>0.53 m</b>.</para>
        ///
        /// <para><b>On G3 since NMC H2 the journey is the water itself.</b> There is no trench or meander to
        /// walk: the berths lie in a basin dredged from wall to wall and the way out is the TRACK through the
        /// mouth. So every berth must lie in ONE body of water, joined to the entrance's landfall in the bay,
        /// that carries the fairway's depth at spring low at every node of a half-metre lattice struck between
        /// G3's texel centres, never on them. Measured on G3: the berths (x 86.2 to 160.2) lie in the joined
        /// water, whose run along the berth line is x 78.5 to 186.5.</para>
        /// </summary>
        [Test]
        public void EveryBerthCanLeaveOnAnyTide_TheWholeWayToTheBay()
        {
            float needed = NineMileCreekMainland.DeepestResidentDraughtMetres
                         + NineMileCreekMainland.ChannelKeelClearanceMetres
                         + NineMileCreekMainland.ChannelDredgeMarginMetres;

            Vector2 landfall = NineMileCreekNavMarks.Entrance.Waypoints[0];
            System.Func<Vector2, bool> joined = WaterJoinedTo(landfall, SpringLow - needed);
            Assert.That(joined(landfall), Is.True,
                $"the entrance's landfall at {landfall} does not carry {needed:0.00} m at spring low itself");

            foreach (var (berth, at) in NineMileCreekWharfTests.SceneBerths())
                Assert.That(joined(at), Is.True,
                    $"leaving berth {berth} at {at} (bed {Elev(at):0.00} m) at dead low spring, no water carrying " +
                    $"the {needed:0.00} m the deepest resident needs joins her to the bay at {landfall}. " +
                    "A berth pocket that does not reach the fairway is a pond.");
        }

        /// <summary>
        /// ⚠ <b>THE WALL ITSELF IS STILL THERE.</b> The one thing the trench must not do is dredge the
        /// wharf it lies against: <c>MainlandChannel.CuttableCeiling</c> is what stops it, and this is the
        /// test that would notice if it were ever raised. The deck, the apron and the yard all stand
        /// exactly where they stood.
        ///
        /// <para>⚠ The plan's authoring since NMC H2, on the premise: the trench and its ceiling are the
        /// analytic cut's. G3 has no west apron or cross-basin breakwater (both retired from the scene), and
        /// the scene's quay standing above every tide is <c>NineMileCreekWharfTests</c>' whole-quay case, on G3.</para>
        /// </summary>
        [Test]
        public void TheTrenchDidNotDredgeTheWharfItLiesAgainst()
        {
            foreach (var (what, p) in new (string, Vector2)[]
                     {
                         ("the quay deck", new Vector2(128f, 92f)),
                         ("the quay's south lip", new Vector2(128f, 87.5f)),
                         ("the apron", new Vector2(87f, 68f)),
                         ("the breakwater crest", new Vector2(140f, 38f)),
                         ("the spit yard", new Vector2(119f, 118f)),
                     })
                Assert.That(PlanElev(p), Is.GreaterThan(SpringHigh),
                    $"{what} at {p} now stands at {PlanElev(p):0.00} m — the trench has cut into structure. " +
                    "Its CuttableCeiling is the guard, and something has moved it.");
        }

        /// <summary>
        /// ⚠ <b>THE COROLLARY THAT MUST NOT GENERALISE.</b> "Never assert depth at spring low" is
        /// reversed at the channel and NOWHERE ELSE: the harbour flats either side of the lane still
        /// bare, and if they stopped, the meander would have nothing to be a meander THROUGH.
        ///
        /// <para><b>On G3 since NMC H2</b> the reversal is the dredged basin, wall to wall, so the flats
        /// that must still bare are the ones outside it: its undredged corners and the strip inside the east
        /// arm. The plan's old flats (120, 56), (150, 52), (126, 48), (160, 46) and (180, 80) all lie in G3's
        /// basin now, at −4.3 to −4.5 m.</para>
        /// </summary>
        [Test]
        public void TheHarbourFlatsStillBareAtSpringLow()
        {
            // Outside the dredged basin, inside the arms: the ground the ebb uncovers.
            var flats = new[]
            {
                new Vector2(58f, 16f),    // the basin's undredged south-west corner, inside the south arm
                new Vector2(46f, 64f),    // the west shore
                new Vector2(190f, 32f),   // the undredged south-east corner, inside the east arm
                new Vector2(204f, 50f),   // the strip along the east arm's inside face
                new Vector2(204f, 30f),   // …further south along it
            };

            foreach (Vector2 p in flats)
            {
                Assert.That(Elev(p), Is.GreaterThan(SpringLow),
                    $"the flat at {p} no longer bares at spring low ({Elev(p):0.00} m). The dredging has " +
                    "spread past the basin, and a harbour with no flats left in it is a dock.");
                Assert.That(Elev(p), Is.LessThan(SpringHigh),
                    "…and it must still flood at spring high, or it is not a flat but a field");
            }
        }

        /// <summary>The drying world the ruling did NOT touch: the bar crest and the gut that carries the
        /// crossing to St Peters both still bare, exactly as their own tests say. On G3 since NMC H2.</summary>
        [Test]
        public void TheCrossingAndItsGutStillDry()
        {
            Vector2 crest = Vector2.Lerp(NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo, 0.75f);
            Vector2 gut = Vector2.Lerp(NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo,
                                       NineMileCreekMainland.GutAlong);

            Assert.That(Elev(crest), Is.GreaterThan(SpringLow), "the bar still bares — the pacing lever");
            Assert.That(Elev(gut), Is.GreaterThan(SpringLow),
                "and the gut still dries, which is what makes the crossing walkable end to end");
        }

        // =============================================================================================
        //  4. WHAT A CHANNEL MAY NOT DO
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>A CHANNEL DREDGES SEABED, NEVER STRUCTURE.</b> The route runs hard against the apron the
        /// gangway comes down — within the cut's own half-width of the west wall — so without the
        /// cuttable-ceiling guard it would dredge the wharf out from under the float it exists to serve.
        ///
        /// <para>⚠ The plan's authoring since NMC H2, on the premise: these are the analytic plan's fill
        /// zones and its channel. G3 has neither the west wall's apron nor the cross-basin breakwater; the
        /// scene's quay is held on G3 by <c>NineMileCreekWharfTests</c>.</para>
        /// </summary>
        [Test]
        public void TheChannelNeverCutsAStructure()
        {
            foreach (var (what, zone) in new (string, MainlandZone)[]
                     {
                         ("the north wall's deck", NineMileCreekMainland.NorthWallFill),
                         ("the west wall's apron", NineMileCreekMainland.WestWallFill),
                         ("the spit yard", NineMileCreekMainland.SpitFill),
                         ("the breakwater crest", NineMileCreekMainland.BreakwaterFill),
                     })
            {
                foreach (Vector2 p in Corners(zone))
                    Assert.That(PlanElev(p), Is.GreaterThan(SpringHigh),
                        $"{what} at {p} stands on {PlanElev(p):0.00} m — the channel has eaten it. A channel " +
                        "may only cut ground at or below the cuttable ceiling.");
            }
        }

        /// <summary>
        /// ⭐⭐ <b>THE ART FLOOR — and the 2026-09-05 finding that it never guarded a VISIBLE defect.</b>
        ///
        /// <para>This test used to read: the committed quay face is drawn in one course from the deck lip
        /// down to the rig's mud line (rig z −1.4 m = −3.60 m of game elevation), so cutting the bed below
        /// that anywhere a wall stands leaves <i>"a strip of nothing under a working wharf"</i>. It named
        /// itself "the constraint that shaped the route", and it carried the flag that floating the fleet
        /// at the WALL would need 0.10–0.20 m past what the art draws — <b>an art re-bake, not a terrain
        /// edit</b>. The owner gave that ruling on 2026-09-04, so the berth trench was cut and this test
        /// went red at (95.00, 86.25) by 0.14 m. It was then measured rather than obeyed.</para>
        ///
        /// <para><b>The strip is under water, at every state of tide, and always was.</b> The face's foot
        /// is at <c>springLow + mudZ</c>, and <c>mudZ</c> is NEGATIVE — the rig draws down past its own
        /// chart datum — so the foot is 1.40 m below the lowest water this coast ever has. Any bed the
        /// face fails to reach is by definition below the foot, hence lower still, hence never uncovered.
        /// What fills the gap is the sea, which is what a dredged berth looks like. Plated at dead low
        /// spring: <c>docs/art/spikes/nmc-wet-wall/</c>.</para>
        ///
        /// <para>So the guard is restated as the disjunction it always was, with both arms asserted. It
        /// keeps its teeth for the case that would make it bite: a re-bake that lifted <c>mudZ</c> to or
        /// above the rig's datum would put the drawn foot at or above the lowest water, and a bed under a
        /// wall could then be exposed and short at the same time.</para>
        ///
        /// <para><b>On G3 since NMC H2.</b> The faces are re-lined to G3's caps, the north face at y 86 and
        /// the crane apron's at y 76 (<see cref="NineMileCreekGroundPlan.NorthCap"/>,
        /// <see cref="NineMileCreekGroundPlan.CraneCap"/>), and the bed in front of them is G3's dredged
        /// basin, deeper again than the old trench.</para>
        /// </summary>
        [Test]
        public void TheQuayFacesFootIsNeverLeftSTANDINGShortOfGroundThatBARES()
        {
            // rig z = 0 is the LOWEST water, so the drawn face's foot in game elevation is simply
            // spring low plus the rig's mud line. Derived from the quay-face plan, never re-typed.
            float artFloor = SpringLow + NineMileCreekQuayFace.BakedRigMudZ;
            Assert.That(artFloor, Is.EqualTo(-3.60f).Within(0.01f), "−2.20 m of spring low, −1.40 m of mud");
            Assert.That(artFloor, Is.LessThan(SpringLow),
                "the drawn foot has come up to or above the lowest water. Every shortfall under this " +
                "wharf can now be SEEN, and the first arm below is the one that matters again");

            float deepest = float.MaxValue;
            foreach (Rect wall in new[] { NineMileCreekGroundPlan.NorthCap, NineMileCreekGroundPlan.CraneCap })
                foreach (Vector2 p in AroundTheFace(wall))
                {
                    float bed = Elev(p);
                    deepest = Mathf.Min(deepest, bed);
                    bool reaches = bed >= artFloor;
                    bool drowned = bed <= SpringLow;
                    Assert.That(reaches || drowned, Is.True,
                        $"the bed at {p} is {bed:0.00} m: the face is drawn only to {artFloor:0.00} m and " +
                        $"the lowest water is {SpringLow:0.00} m, so the shortfall would be UNCOVERED. " +
                        "A wall must either reach the mud or stand in water.");
                }

            // On the record, so the next reader has the number rather than the argument: how far the
            // trench takes the bed past what the art draws, everywhere a wall stands.
            Assert.That(deepest, Is.LessThan(artFloor),
                "no wall on this wharf now stands in water deeper than its own drawn face. If that is " +
                "true again the berth trench has been filled in and the owner's ruling has been undone");
            TestContext.WriteLine(
                $"[wet-wall] deepest bed against a wall {deepest:0.00} m, the drawn face reaches " +
                $"{artFloor:0.00} m, lowest water {SpringLow:0.00} m — the shortfall of " +
                $"{artFloor - deepest:0.00} m lies {SpringLow - artFloor:0.00} m under the surface.");
        }

        // =============================================================================================
        //  5. THE OWNER'S STANDING QUESTION — perch and withy, or floats?
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>THE ANSWER, MEASURED: floats now suffice for the inner channel.</b> #575 could not buoy
        /// it because the whole bullpen bared, so a floating mark would have stood on dry ground for part
        /// of every big tide and the plan rightly REFUSED it — leaving a perch/withy art ask open.
        ///
        /// <para>This measures what changed. A lateral mark stands at the channel's declared half-width
        /// off the fairway, and the region's floor is 0.60 m of water at spring low, so the question is
        /// simply: how far off the thalweg can a mark stand and still float? The window comfortably
        /// contains the 5.0 m a lane one beam wide (with half a beam either side) would declare.
        /// <b>No new art is needed to mark this channel.</b></para>
        ///
        /// <para>⚠ It also states the other half of the finding: <c>Entrance</c>'s authored 12 m
        /// half-width is WIDER than the lane itself at spring low, so its innermost marks stood on the
        /// flats and were refused. S2 found that narrowing the half-width could not fix it, because the
        /// lane had no wet ground outside itself.</para>
        ///
        /// <para>⚠ The plan's authoring since NMC H2, on the premise: both offsets are measured off the
        /// analytic channel's centre-line. The Entrance no longer follows that channel. The owner ruled it
        /// redrawn along the design boat's TRACK into G3's mouth, which closes the refusal on the ground the
        /// scene stands on (<see cref="TheEntrancesInnermostPairFloatsOnG3_TheRefusalIsClosed"/>).</para>
        /// </summary>
        [Test]
        public void AFloatingMarkNowFitsTheInnerChannel_WhichAnswersThePerchAndWithyQuestion()
        {
            const float MarkFloorAtSpringLow = 0.60f;         // NineMileCreekNavMarks.Tuning's own floor
            float declared = NineMileCreekMainland.NavigableWidthFloorMetres * 0.5f;   // 5.0 m

            float worst = WorstMarkDepthAtOffset(declared, out Vector2 worstAt);
            Assert.That(worst, Is.GreaterThan(MarkFloorAtSpringLow),
                $"a mark {declared:0.0} m off the thalweg would stand in {worst:0.00} m at spring low " +
                $"(at {worstAt}), under the {MarkFloorAtSpringLow:0.00} m floor — so the inner channel " +
                "still could not be buoyed with floats and the perch/withy ask would stand.");

            // …and the approach's own width does NOT fit, which is the half that needs the owner.
            float atEntranceWidth = WorstMarkDepthAtOffset(12f, out _);
            Assert.That(atEntranceWidth, Is.LessThan(MarkFloorAtSpringLow),
                "if a mark at the ENTRANCE channel's 12 m half-width now floats too, this finding is " +
                "stale and the PR body's advice to declare a narrower inner channel is wrong");
        }

        /// <summary>
        /// ⭐ <b>THE REFUSED PAIR IS CLOSED, ON G3.</b> Until NMC H2 this section held the measurement
        /// that the Entrance's innermost pair could not be saved by narrowing its half-width: outside the
        /// old dredged lane lay a drying flat, so a mark stood either in the fairway or on dry ground. The
        /// owner ruled the redraw instead. The Entrance now runs along the design boat's TRACK into G3's
        /// mouth (<see cref="NineMileCreekNavMarks.Entrance"/>), its last station stands in the mouth
        /// between the arms, and the ground either side of it is water, not a flat.
        ///
        /// <para>So the finding is asserted closed, on the ground the scene stands on: the plan refuses
        /// nothing on the Entrance, and the pair hung off its last station at the declared half-width floats
        /// at the worst tide of the month. If this goes red, the refusal is back, and the two remedies the
        /// old measurement named are owed again: perches or withies, or ending the buoyed channel short of
        /// the harbour.</para>
        /// </summary>
        [Test]
        public void TheEntrancesInnermostPairFloatsOnG3_TheRefusalIsClosed()
        {
            float floor = NineMileCreekNavMarks.Tuning.MinDepthAtSpringLowMetres;
            NavChannel entrance = NineMileCreekNavMarks.Entrance;
            NavMarkPlanResult plan = NineMileCreekNavMarks.Plan(_terrain);

            foreach (NavMarkRefusal r in plan.Refusals)
                Assert.That(r.OwnerId, Is.Not.EqualTo(entrance.Id),
                    $"the Entrance refused a mark at {r.At} on G3: {r.Reason}");

            // The last station is an END station, so the plan does not snap it: its pair hangs exactly
            // the half-width either side of it, and no other station's marks come that close.
            Vector2 station = entrance.Waypoints[entrance.Waypoints.Length - 1];
            var pair = new List<PlannedNavMark>();
            foreach (PlannedNavMark m in plan.Marks)
                if (m.OwnerId == entrance.Id && Vector2.Distance(m.At, station) <= entrance.HalfWidthMetres + 0.5f)
                    pair.Add(m);

            Assert.That(pair.Count, Is.EqualTo(2),
                $"the Entrance's last station at {station} should carry one pair, port and starboard; " +
                $"{pair.Count} mark(s) stand within its half-width");
            foreach (PlannedNavMark m in pair)
            {
                float depth = Depth(m.At, SpringLow);
                Assert.That(depth, Is.GreaterThan(floor),
                    $"'{m.Id}' at {m.At} stands in {depth:0.00} m at spring low on G3, under the " +
                    $"{floor:0.00} m floor: the refused pair is back");
                TestContext.WriteLine($"[entrance] '{m.Id}' at {m.At}: {depth:0.00} m at spring low");
            }
        }

        // =============================================================================================
        //  6. PURITY
        // =============================================================================================

        /// <summary>Elevation is a pure function of position (CLAUDE.md rule 5) — no RNG, nothing saved.
        /// A channel sampled twice, and a second terrain configured from the same plan, agree exactly.
        /// The plan's authoring since NMC H2, on the premise: it is the analytic terrain that is configured.
        /// </summary>
        [Test]
        public void TheChannelIsPureAndReconfiguresIdentically()
        {
            var otherGo = new GameObject("NineMileCreekChannel_Second");
            try
            {
                var other = otherGo.AddComponent<MainlandTidalTerrain>();
                NineMileCreekMainland.ConfigureTerrain(other);

                foreach (Vector2 p in WalkTheChannel(3f))
                {
                    Assert.That(PlanElev(p), Is.EqualTo(PlanElev(p)), "the same sample twice");
                    Assert.That(other.ElevationAt(p), Is.EqualTo(PlanElev(p)),
                        $"two terrains configured from one plan disagree at {p}");
                }
            }
            finally { Object.DestroyImmediate(otherGo); }
        }

        // =============================================================================================
        //  helpers
        // =============================================================================================

        /// <summary>The shallowest water any mark would stand in, offset this far either side of the
        /// authored centre-line — the question "may this channel be buoyed with floats?" as a number.
        /// On the premise, as the channel it walks is.</summary>
        private float WorstMarkDepthAtOffset(float offsetMetres, out Vector2 worstAt)
        {
            float worst = float.MaxValue;
            worstAt = Vector2.zero;

            foreach ((Vector2 at, Vector2 course) in WalkTheChannelWithCourse(1f))
            {
                var normal = new Vector2(-course.y, course.x);
                foreach (float side in new[] { 1f, -1f })
                {
                    Vector2 p = at + normal * (offsetMetres * side);
                    float d = PlanDepth(p, SpringLow);
                    if (d < worst) { worst = d; worstAt = p; }
                }
            }
            return worst;
        }

        /// <summary>Every point along the AUTHORED centre-line at a given spacing — the spline itself,
        /// never a searched minimum (a found minimum slides downhill off the line the boat steers).</summary>
        private static IEnumerable<Vector2> WalkTheChannel(float stepMetres)
        {
            foreach ((Vector2 at, Vector2 _) in WalkTheChannelWithCourse(stepMetres)) yield return at;
        }

        /// <summary>…and the unit course at each, for anything that needs a normal.</summary>
        private static IEnumerable<(Vector2 at, Vector2 course)> WalkTheChannelWithCourse(float stepMetres)
        {
            Vector2[] wp = NineMileCreekMainland.ChannelWaypoints;
            float step = Mathf.Max(0.1f, stepMetres);

            for (int i = 0; i < wp.Length - 1; i++)
            {
                Vector2 a = wp[i], b = wp[i + 1];
                float leg = Vector2.Distance(a, b);
                if (leg <= 1e-4f) continue;
                Vector2 course = (b - a) / leg;
                int n = Mathf.Max(1, Mathf.CeilToInt(leg / step));
                for (int k = 0; k <= n; k++)
                    yield return (Vector2.Lerp(a, b, k / (float)n), course);
            }
        }

        /// <summary>The wetted width of the lane measured ACROSS it at a point — marched perpendicular to
        /// the local course from the centre-line outward, stopping at the first dry step either side, so
        /// a puddle out on the flats can never be counted as part of the channel. On the premise, as the
        /// channel it measures is.</summary>
        private float LaneWidthAcross(Vector2 at, float water)
        {
            Vector2 course = CourseAt(at);
            var normal = new Vector2(-course.y, course.x);
            const float Step = 0.05f, Reach = 40f;

            if (PlanDepth(at, water) <= 0f) return 0f;

            float width = 0f;
            foreach (float side in new[] { 1f, -1f })
                for (float s = Step; s <= Reach; s += Step)
                {
                    if (PlanDepth(at + normal * (s * side), water) <= 0f) break;
                    width += Step;
                }
            return width;
        }

        /// <summary>The water joined to a place by water at least this deep, on G3: a flood fill over a
        /// half-metre lattice whose nodes stand on whole and half metres, between G3's texel centres and
        /// never on them, four neighbours to a node. Any point is answered by its nearest node.</summary>
        private System.Func<Vector2, bool> WaterJoinedTo(Vector2 from, float deepEnough)
        {
            const float X0 = 30f, X1 = 300f, Y0 = -60f, Y1 = 90f, Step = 0.5f;
            int w = Mathf.RoundToInt((X1 - X0) / Step) + 1, h = Mathf.RoundToInt((Y1 - Y0) / Step) + 1;
            var wet = new bool[w * h];
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    wet[j * w + i] = Elev(new Vector2(X0 + i * Step, Y0 + j * Step)) <= deepEnough;

            int NodeOf(Vector2 p) => Mathf.Clamp(Mathf.RoundToInt((p.y - Y0) / Step), 0, h - 1) * w
                                   + Mathf.Clamp(Mathf.RoundToInt((p.x - X0) / Step), 0, w - 1);
            var seen = new bool[w * h];
            int start = NodeOf(from);
            if (!wet[start]) return _ => false;

            var queue = new Queue<int>();
            seen[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int k = queue.Dequeue(), i = k % w, j = k / w;
                foreach (int n in new[] { i > 0 ? k - 1 : -1, i < w - 1 ? k + 1 : -1,
                                          j > 0 ? k - w : -1, j < h - 1 ? k + w : -1 })
                    if (n >= 0 && !seen[n] && wet[n]) { seen[n] = true; queue.Enqueue(n); }
            }
            return p => seen[NodeOf(p)];
        }

        /// <summary>The authored waypoint nearest a place of interest — so a test can name the station it
        /// measures by where it IS, not by where it sits in the array.</summary>
        private static Vector2 NearestChannelStationTo(Vector2 near)
        {
            Vector2[] wp = NineMileCreekMainland.ChannelWaypoints;
            Vector2 best = wp[0];
            foreach (Vector2 p in wp)
                if (Vector2.Distance(p, near) < Vector2.Distance(best, near)) best = p;
            return best;
        }

        /// <summary>The unit course of the nearest authored leg.</summary>
        private static Vector2 CourseAt(Vector2 p)
        {
            Vector2[] wp = NineMileCreekMainland.ChannelWaypoints;
            float best = float.MaxValue; Vector2 course = Vector2.right;
            for (int i = 0; i < wp.Length - 1; i++)
            {
                float d = MainlandTidalTerrain.DistanceToSegment(p, wp[i], wp[i + 1]);
                if (d >= best) continue;
                best = d;
                Vector2 v = wp[i + 1] - wp[i];
                if (v.sqrMagnitude > 1e-6f) course = v.normalized;
            }
            return course;
        }

        private static IEnumerable<Vector2> Corners(MainlandZone z)
        {
            yield return z.Center;
            yield return z.Center + new Vector2(z.HalfSize.x - 0.5f, z.HalfSize.y - 0.5f);
            yield return z.Center + new Vector2(-(z.HalfSize.x - 0.5f), z.HalfSize.y - 0.5f);
            yield return z.Center + new Vector2(z.HalfSize.x - 0.5f, -(z.HalfSize.y - 0.5f));
            yield return z.Center + new Vector2(-(z.HalfSize.x - 0.5f), -(z.HalfSize.y - 0.5f));
        }

        /// <summary>A dense run of samples just OUTSIDE a wall's footprint — the water-side ground its
        /// drawn face has to reach down to.</summary>
        private static IEnumerable<Vector2> AroundTheFace(Rect wall)
        {
            const float Step = 1f, Out = 0.75f;
            for (float x = wall.xMin; x <= wall.xMax; x += Step)
            {
                yield return new Vector2(x, wall.yMin - Out);
                yield return new Vector2(x, wall.yMax + Out);
            }
            for (float y = wall.yMin; y <= wall.yMax; y += Step)
            {
                yield return new Vector2(wall.xMin - Out, y);
                yield return new Vector2(wall.xMax + Out, y);
            }
        }
    }
}
