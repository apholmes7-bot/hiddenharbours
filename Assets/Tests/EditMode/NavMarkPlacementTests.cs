using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.Boats;   // NavBuoyMooringMath - the marks' own mooring
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE NAV MARKS — does every buoy float, and does every colour agree with the water?</b>
    ///
    /// <para>The terrains are built through the SAME configure calls the builders use
    /// (<see cref="NineMileCreekMainland.ConfigureTerrain"/> /
    /// <see cref="StPetersBuilder.ConfigureTidalTerrain"/>), so a test can never assert a channel the
    /// scene does not carry. Nothing here loads a scene: the whole scheme is a pure function of the
    /// published plan plus that terrain, which is why it can be asserted headlessly at all.</para>
    ///
    /// <para><b>The load-bearing test is <see cref="EveryLateralStandsOnTheSideItsColourClaims"/>.</b>
    /// A lateral system exists to make ONE defect impossible — a buoy whose colour and whose side
    /// disagree — and the only way to prove that is to check the colour (a string on the placed mark)
    /// against the geometry (which side of the derived centreline it actually sits on), never against
    /// the enum that produced both. <see cref="ASabotagedColourIsNamedByTheLateralTest"/> is the arm
    /// that proves the check can see a wrong one.</para>
    /// </summary>
    public class NavMarkPlacementTests
    {
        private GameObject _nmcGo;
        private MainlandTidalTerrain _nmc;
        private GameObject _spGo;
        private TidalTerrain _sp;

        private NavMarkPlanResult _nmcPlan;
        private NavMarkPlanResult _spPlan;

        [SetUp]
        public void SetUp()
        {
            _nmcGo = new GameObject("NineMileCreekMainland_NavMarkTest");
            _nmc = _nmcGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(_nmc);
            _nmcPlan = NineMileCreekNavMarks.Plan(_nmc);

            _spGo = new GameObject("StPeters_NavMarkTest");
            _sp = _spGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_sp);
            _spPlan = StPetersNavMarks.Plan(_sp);
        }

        [TearDown]
        public void TearDown()
        {
            if (_nmcGo != null) Object.DestroyImmediate(_nmcGo);
            if (_spGo != null) Object.DestroyImmediate(_spGo);
            GameServices.Reset();
        }

        // =============================================================================================
        //  1. EVERY MARK FLOATS — at the worst hour of the month
        // =============================================================================================

        /// <summary>
        /// A floating mark standing on bared mud reads as a bug, not as a harbour. So every mark the
        /// plan ships must have real water under it at the LOWEST water of the biggest spring tide —
        /// not at mean, not at high.
        /// </summary>
        [Test]
        public void EveryMarkFloatsAtTheLowestSpringTide()
        {
            AssertAllAfloat("Nine Mile Creek", _nmcPlan, _nmc.ElevationAt,
                            NineMileCreekMainland.TideMean - NineMileCreekMainland.TideAmplitude,
                            NineMileCreekNavMarks.Tuning.MinDepthAtSpringLowMetres);

            AssertAllAfloat("St Peters", _spPlan, _sp.ElevationAt,
                            StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude,
                            StPetersNavMarks.Tuning.MinDepthAtSpringLowMetres);
        }

        private static void AssertAllAfloat(string region, NavMarkPlanResult plan,
                                            System.Func<Vector2, float> elevationAt,
                                            float springLow, float floor)
        {
            Assert.That(plan.Marks.Count, Is.GreaterThan(0),
                $"{region} placed no nav marks at all. Either every station dried out or the channel " +
                "table is empty — both are findings, neither is a pass.");

            foreach (PlannedNavMark m in plan.Marks)
            {
                // Re-measured from the terrain, NOT read back off the plan's own cached depth: a test
                // that trusts the number the planner wrote down cannot catch the planner being wrong.
                float depth = springLow - elevationAt(m.At);
                Assert.That(depth, Is.GreaterThan(floor),
                    $"{region}: '{m.Id}' ({m.MarkType}) at ({m.At.x:0.#}, {m.At.y:0.#}) has {depth:0.00} m " +
                    $"under it at spring low, against a floor of {floor:0.00} m. It would stand on ground " +
                    "that bares. Marks go in water that never dries — see the region's class note.");
            }
        }

        // =============================================================================================
        //  2. THE LATERAL TEST — the colour against the geometry
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>IALA Region B: red to STARBOARD returning from seaward.</b> Walking each channel in
        /// its authored direction (index 0 is the seaward end), every red mark must lie to the right of
        /// the derived centreline and every green to the left.
        ///
        /// <para>The check reads the COLOUR off the mark's kit type and the SIDE off the geometry, so
        /// the two can be caught disagreeing. Checking the type against <c>PlannedNavMark.Hand</c>
        /// instead would only prove the planner is self-consistent, which it is by construction and
        /// which no defect would ever break.</para>
        /// </summary>
        [Test]
        public void EveryLateralStandsOnTheSideItsColourClaims()
        {
            List<string> nmc = LateralFaults(_nmcPlan);
            Assert.That(nmc, Is.Empty, "Nine Mile Creek:\n  " + string.Join("\n  ", nmc));

            List<string> sp = LateralFaults(_spPlan);
            Assert.That(sp, Is.Empty, "St Peters:\n  " + string.Join("\n  ", sp));
        }

        /// <summary>
        /// The sabotage arm. Flip ONE mark's colour in a copy of the plan and the lateral check must
        /// name it — otherwise the check above is decoration.
        /// </summary>
        [Test]
        public void ASabotagedColourIsNamedByTheLateralTest()
        {
            var sabotaged = new NavMarkPlanResult();
            sabotaged.Fairways.AddRange(_nmcPlan.Fairways);

            string victim = null;
            foreach (PlannedNavMark m in _nmcPlan.Marks)
            {
                PlannedNavMark copy = m;
                if (victim == null && m.IsLateral)
                {
                    victim = m.Id;
                    // Same hand, opposite colour: a hand-typed buoy that went in the wrong bucket.
                    copy.MarkType = m.MarkType == "StbdNun" || m.MarkType == "StbdLit" ? "PortCan" : "StbdNun";
                }
                sabotaged.Marks.Add(copy);
            }

            Assert.That(victim, Is.Not.Null, "No lateral to sabotage — the plan produced none.");

            List<string> faults = LateralFaults(sabotaged);
            Assert.That(faults.Count, Is.EqualTo(1),
                "Exactly one mark was sabotaged; the lateral check should report exactly one fault. " +
                "Got:\n  " + string.Join("\n  ", faults));
            StringAssert.Contains(victim, faults[0],
                "The lateral check found a fault but did not NAME the mark — a failure a human cannot " +
                "act on is most of the way to no failure at all.");
        }

        /// <summary>
        /// Every lateral whose colour disagrees with the side it stands on, described. Empty is a pass.
        /// </summary>
        private static List<string> LateralFaults(NavMarkPlanResult plan)
        {
            var faults = new List<string>();

            foreach (PlannedNavMark m in plan.Marks)
            {
                if (!m.IsLateral) continue;

                NavChannelFairway fairway = plan.Fairway(m.OwnerId);
                if (fairway == null || fairway.Stations.Count == 0)
                {
                    faults.Add($"'{m.Id}' belongs to channel '{m.OwnerId}', which produced no fairway.");
                    continue;
                }

                int s = NearestStation(fairway, m.AlongMetres);
                Vector2 fromCentre = m.At - fairway.Stations[s];
                float toStarboard = Vector2.Dot(
                    fromCentre, NavChannelGeometry.StarboardNormal(fairway.Course[s]));

                bool red = m.MarkType == "StbdNun" || m.MarkType == "StbdLit";
                bool green = m.MarkType == "PortCan" || m.MarkType == "PortLit";
                if (!red && !green)
                {
                    faults.Add($"'{m.Id}' is flagged lateral but wears '{m.MarkType}', which is neither " +
                               "a port nor a starboard hand.");
                    continue;
                }

                if (red && toStarboard <= 0f)
                    faults.Add($"'{m.Id}' is RED ({m.MarkType}) but lies {-toStarboard:0.0} m to PORT of " +
                               $"the centreline of '{m.OwnerId}'. IALA Region B puts red to starboard " +
                               "returning from seaward.");
                else if (green && toStarboard >= 0f)
                    faults.Add($"'{m.Id}' is GREEN ({m.MarkType}) but lies {toStarboard:0.0} m to " +
                               $"STARBOARD of the centreline of '{m.OwnerId}'. IALA Region B puts green " +
                               "to port returning from seaward.");
            }

            return faults;
        }

        private static int NearestStation(NavChannelFairway fairway, float along)
        {
            int best = 0;
            float bestGap = float.MaxValue;
            for (int i = 0; i < fairway.Along.Count; i++)
            {
                float gap = Mathf.Abs(fairway.Along[i] - along);
                if (gap < bestGap) { bestGap = gap; best = i; }
            }
            return best;
        }

        /// <summary>
        /// The direction of buoyage is the WAYPOINT ORDER and nothing else. Reverse a channel and every
        /// colour on it must swap — which is what "the handedness is derived, never hand-typed" means
        /// operationally.
        /// </summary>
        [Test]
        public void ReversingAChannelSwapsEveryColourOnIt()
        {
            NavChannel forward = NineMileCreekNavMarks.Entrance;
            var reversedPoints = new Vector2[forward.Waypoints.Length];
            for (int i = 0; i < reversedPoints.Length; i++)
                reversedPoints[i] = forward.Waypoints[reversedPoints.Length - 1 - i];

            var reversed = new NavChannel
            {
                Id = forward.Id, DisplayName = forward.DisplayName, Waypoints = reversedPoints,
                HalfWidthMetres = forward.HalfWidthMetres,
                SearchHalfWidthMetres = forward.SearchHalfWidthMetres,
                SizeId = forward.SizeId, LitLandfall = forward.LitLandfall,
                DeclaredDraughtMetres = forward.DeclaredDraughtMetres,
                NavigableTideFraction = forward.NavigableTideFraction,
                KeelClearanceMetres = forward.KeelClearanceMetres,
            };

            NavMarkPlanResult a = NavMarkPlan.Plan(new[] { forward }, null, _nmc.ElevationAt,
                NineMileCreekMainland.TideMean, NineMileCreekMainland.TideAmplitude,
                NineMileCreekNavMarks.Tuning);
            NavMarkPlanResult b = NavMarkPlan.Plan(new[] { reversed }, null, _nmc.ElevationAt,
                NineMileCreekMainland.TideMean, NineMileCreekMainland.TideAmplitude,
                NineMileCreekNavMarks.Tuning);

            Assert.That(a.Marks.Count, Is.GreaterThan(0), "The forward channel produced no marks.");
            Assert.That(b.Marks.Count, Is.GreaterThan(0), "The reversed channel produced no marks.");

            // Both plans are internally consistent — that is the point. What must differ is which
            // physical side of the SAME water carries red.
            Assert.That(LateralFaults(b), Is.Empty,
                "The reversed channel is itself inconsistent — the derivation, not the direction, is wrong.");

            int redToNorthForward = CountRedOnNorthSide(a);
            int redToNorthReversed = CountRedOnNorthSide(b);
            Assert.That(redToNorthForward, Is.Not.EqualTo(redToNorthReversed),
                "Reversing the direction of buoyage left the red marks on the same side of the water. " +
                "The colours are not being derived from the waypoint order.");
        }

        private static int CountRedOnNorthSide(NavMarkPlanResult plan)
        {
            int n = 0;
            foreach (PlannedNavMark m in plan.Marks)
            {
                if (!m.IsLateral) continue;
                NavChannelFairway f = plan.Fairway(m.OwnerId);
                if (f == null) continue;
                int s = NearestStation(f, m.AlongMetres);
                bool red = m.MarkType == "StbdNun" || m.MarkType == "StbdLit";
                if (red && m.At.y > f.Stations[s].y) n++;
            }
            return n;
        }

        // =============================================================================================
        //  3. THE CHANNEL CARRIES WHAT IT CLAIMS — the "don't mark mud as safe" test
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>A channel that marks shallow water as safe fails here, loudly.</b> Every route states
        /// the draught it carries and the state of tide it carries it at; this walks the whole derived
        /// centreline — not just the stations, so a bar between two marks cannot hide — and demands the
        /// claim hold at every metre of it.
        ///
        /// <para>⚠ Asserted at the tide the route CLAIMS, not at low water, because a channel into a
        /// drying harbour is a real channel that is simply not open all day. Both harbours in this
        /// world bare at spring low; testing at low water would delete them both.</para>
        /// </summary>
        [Test]
        public void EveryChannelCarriesTheDraughtItClaims()
        {
            AssertClaims("Nine Mile Creek", NineMileCreekNavMarks.Channels, _nmcPlan, _nmc.ElevationAt,
                         NineMileCreekMainland.TideMean, NineMileCreekMainland.TideAmplitude,
                         NineMileCreekNavMarks.Tuning);

            AssertClaims("St Peters", StPetersNavMarks.Channels, _spPlan, _sp.ElevationAt,
                         StPetersBuilder.TideMean, StPetersBuilder.TideAmplitude,
                         StPetersNavMarks.Tuning);
        }

        private static void AssertClaims(string region, IReadOnlyList<NavChannel> channels,
                                         NavMarkPlanResult plan, System.Func<Vector2, float> elevationAt,
                                         float tideMean, float tideAmplitude, NavMarkTuning tuning)
        {
            foreach (NavChannel c in channels)
            {
                NavChannelFairway f = plan.Fairway(c.Id);
                Assert.That(f, Is.Not.Null, $"{region}: channel '{c.Id}' produced no fairway.");

                bool carries = NavMarkPlan.FairwayCarriesItsClaim(
                    c, f, elevationAt, tideMean, tideAmplitude, tuning,
                    out Vector2 worstAt, out float worstDepth);

                float water = tideMean + c.NavigableTideFraction * tideAmplitude;
                float needed = c.DeclaredDraughtMetres + c.KeelClearanceMetres;
                Assert.That(carries, Is.True,
                    $"{region}: '{c.DisplayName}' claims {c.DeclaredDraughtMetres:0.00} m of draught " +
                    $"(+{c.KeelClearanceMetres:0.00} m clearance = {needed:0.00} m) at a water level of " +
                    $"{water:0.00} m, but its shallowest point is ({worstAt.x:0.#}, {worstAt.y:0.#}) with " +
                    $"only {worstDepth:0.00} m. The buoyed line runs over ground the boat it claims " +
                    "cannot clear. Move the route or change the claim — do not ship both.");
            }
        }

        // =============================================================================================
        //  4. CARDINALS — the quadrant against the seabed
        // =============================================================================================

        /// <summary>Every cardinal that got placed must have genuinely deeper water on the side it
        /// sends you.</summary>
        [Test]
        public void EveryCardinalsQuadrantPointsAwayFromItsDanger()
        {
            AssertCardinals("Nine Mile Creek", NineMileCreekNavMarks.Cardinals, _nmcPlan, _nmc.ElevationAt);
            AssertCardinals("St Peters", StPetersNavMarks.Cardinals, _spPlan, _sp.ElevationAt);
        }

        private static void AssertCardinals(string region, IReadOnlyList<NavCardinal> cardinals,
                                            NavMarkPlanResult plan, System.Func<Vector2, float> elevationAt)
        {
            int placed = 0;
            foreach (NavCardinal c in cardinals)
            {
                bool shipped = false;
                foreach (PlannedNavMark m in plan.Marks)
                    if (!m.IsLateral && m.Id == c.Id) shipped = true;
                if (!shipped) continue;
                placed++;

                bool agrees = NavMarkPlan.QuadrantAgreesWithSeabed(c, elevationAt, out float safe, out float danger);
                Assert.That(agrees, Is.True,
                    $"{region}: cardinal '{c.DisplayName}' is authored {c.Quadrant}, but {c.ProbeMetres:0} m " +
                    $"that way the bed is {safe:0.00} m and the other way {danger:0.00} m — it sends a " +
                    "skipper onto the shallower side, which is the thing it exists to prevent.");
            }

            Assert.That(placed, Is.GreaterThan(0),
                $"{region} shipped no cardinals at all. Every one was refused — read the build log.");
        }

        /// <summary>
        /// ⭐ <b>Do not author a mark you already know will be refused.</b> A refusal is a FINDING —
        /// the harbour dries, the geometry ran out of water — and findings only stay legible while the
        /// list is short. A cardinal that gets refused on every build forever is not a finding, it is
        /// noise that teaches the owner to skim past the real ones.
        ///
        /// <para>This caught a real one: the first draft authored a South cardinal off St Peters purely
        /// for symmetry, and the island's south coast is a <c>Cliff</c> whose foot is below the lowest
        /// water — no shelf, no gradient, nothing to guard. It is gone, and the class note now says
        /// why.</para>
        /// </summary>
        [Test]
        public void EveryAuthoredCardinalIsActuallyPlaced()
        {
            AssertNoCardinalRefused("Nine Mile Creek", NineMileCreekNavMarks.Cardinals, _nmcPlan);
            AssertNoCardinalRefused("St Peters", StPetersNavMarks.Cardinals, _spPlan);
        }

        private static void AssertNoCardinalRefused(string region, IReadOnlyList<NavCardinal> cardinals,
                                                    NavMarkPlanResult plan)
        {
            foreach (NavCardinal c in cardinals)
                foreach (NavMarkRefusal r in plan.Refusals)
                    Assert.That(r.OwnerId, Is.Not.EqualTo(c.Id),
                        $"{region}: cardinal '{c.DisplayName}' is authored but refused every build — " +
                        $"{r.Reason} Either re-site it onto a danger that is really there, or delete it " +
                        "and say why in the class note. Do not ship a standing refusal.");
        }

        /// <summary>The sabotage arm for cardinals: point one the wrong way and the PLAN must refuse
        /// it, rather than a test having to catch it downstream.</summary>
        [Test]
        public void ACardinalPointedAtItsOwnDangerIsRefused()
        {
            NavCardinal good = NineMileCreekNavMarks.Cardinals[0];
            var flipped = new NavCardinal
            {
                Id = good.Id,
                DisplayName = good.DisplayName,
                Quadrant = Opposite(good.Quadrant),
                At = good.At,
                SizeId = good.SizeId,
                ProbeMetres = good.ProbeMetres,
            };

            NavMarkPlanResult plan = NavMarkPlan.Plan(null, new[] { flipped }, _nmc.ElevationAt,
                NineMileCreekMainland.TideMean, NineMileCreekMainland.TideAmplitude,
                NineMileCreekNavMarks.Tuning);

            Assert.That(plan.Marks, Is.Empty,
                $"A {flipped.Quadrant} cardinal on a danger that wants {good.Quadrant} was PLACED. " +
                "The quadrant is being trusted rather than probed.");
            Assert.That(plan.Refusals.Count, Is.EqualTo(1));
            StringAssert.Contains(good.DisplayName, plan.Refusals[0].Reason);
        }

        private static NavCardinalQuadrant Opposite(NavCardinalQuadrant q)
        {
            switch (q)
            {
                case NavCardinalQuadrant.North: return NavCardinalQuadrant.South;
                case NavCardinalQuadrant.South: return NavCardinalQuadrant.North;
                case NavCardinalQuadrant.East:  return NavCardinalQuadrant.West;
                default:                        return NavCardinalQuadrant.East;
            }
        }

        // =============================================================================================
        //  5. THE SNAP — follow a gut, decline a slope
        // =============================================================================================

        /// <summary>
        /// ⚠⚠ <b>The defect the probe caught, pinned.</b> Every approach in this world runs at a shore,
        /// so the bed under it SLOPES — and on a slope the deepest point in any corridor is simply its
        /// outer edge. A search that took it walks the fairway downhill, away from the harbour it is
        /// supposed to reach. A real gut has its deepest point in the MIDDLE. That is the whole
        /// discriminator, and these two cases are it.
        /// </summary>
        [Test]
        public void TheSnapFollowsAGutAndDeclinesASlope()
        {
            var channel = new NavChannel
            {
                Id = "channel.test", Waypoints = new[] { new Vector2(0f, 0f), new Vector2(100f, 0f) },
                HalfWidthMetres = 10f, SearchHalfWidthMetres = 20f,
            };
            var tuning = new NavMarkTuning { SnapStepMetres = 1f, MaxSnapSlewMetres = 20f };

            // Travelling +x, the port normal is +y, so an offset is a distance north.
            Vector2 course = Vector2.right;
            Vector2 port = NavChannelGeometry.PortNormal(course);

            // A SLOPE: monotonically deeper to the north. The minimum is on the corridor's edge.
            float slope = NavMarkPlan.SnapOffset(new Vector2(50f, 0f), port, channel,
                                                 p => -p.y * 0.05f, tuning, 0f);
            Assert.That(slope, Is.EqualTo(0f).Within(1e-3f),
                "The snap followed a monotone slope to the edge of its corridor. That is walking " +
                "downhill, not following a channel — and it drags a fairway off the harbour it serves.");

            // A GUT: a V with its floor 6 m north of the authored line, interior to the corridor.
            float gut = NavMarkPlan.SnapOffset(new Vector2(50f, 0f), port, channel,
                                               p => -4f + Mathf.Abs(p.y - 6f) * 0.1f, tuning, 0f);
            Assert.That(gut, Is.EqualTo(6f).Within(1.01f),
                "The snap failed to follow a gut whose floor sits well inside its corridor. The route " +
                "is a proposal; the seabed is supposed to get a vote.");
        }

        /// <summary>The end stations are DECLARATIONS — the landfall and the destination — and a snap
        /// that slid them would produce a channel that goes almost home.</summary>
        [Test]
        public void TheSnapNeverMovesAChannelsEndStations()
        {
            foreach (NavChannelFairway f in _nmcPlan.Fairways)
            {
                Assert.That(Vector2.Distance(f.Stations[0], f.AuthoredStations[0]),
                    Is.LessThan(1e-3f), $"'{f.ChannelId}': the seaward end moved.");
                int last = f.Stations.Count - 1;
                Assert.That(Vector2.Distance(f.Stations[last], f.AuthoredStations[last]),
                    Is.LessThan(1e-3f), $"'{f.ChannelId}': the harbour end moved.");
            }
        }

        // =============================================================================================
        //  6. THE KIT'S OWN CONVENTIONS
        // =============================================================================================

        /// <summary>
        /// ⚠️ <b>THE NAV-BUOY KIT IS CLOCKWISE.</b> Cell order N NE E SE S SW W NW, cell <c>i</c>
        /// depicting compass heading <c>+45°·i</c> — the OPPOSITE handedness to every boat in the
        /// fleet. This pins the mapping so the fleet's counter-clockwise correction cannot be
        /// "helpfully" applied here, which would mirror all eight cells of every mark.
        /// </summary>
        [Test]
        public void FacingCellsFollowTheKitsClockwiseOrder()
        {
            Assert.That(NavChannelGeometry.FacingCell(0f), Is.EqualTo(0), "N");
            Assert.That(NavChannelGeometry.FacingCell(45f), Is.EqualTo(1), "NE");
            Assert.That(NavChannelGeometry.FacingCell(90f), Is.EqualTo(2), "E");
            Assert.That(NavChannelGeometry.FacingCell(180f), Is.EqualTo(4), "S");
            Assert.That(NavChannelGeometry.FacingCell(315f), Is.EqualTo(7), "NW");
            Assert.That(NavChannelGeometry.FacingCell(360f), Is.EqualTo(0), "N, wrapped");

            Assert.That(NavChannelGeometry.FacingCell(90f), Is.Not.EqualTo(6),
                "East resolved to cell 6, which is the COUNTER-CLOCKWISE answer. The nav-buoy kit is " +
                "clockwise by measurement (NavBuoyKit.GroundStepDeg); the fleet's correction does not " +
                "belong here. See the memory on this trap before 'fixing' it.");

            // And every facing the plan actually ships is in range for an 8-cell sheet.
            foreach (PlannedNavMark m in _nmcPlan.Marks)
                Assert.That(m.Facing, Is.InRange(0, 7), $"'{m.Id}' asks for facing cell {m.Facing}.");
        }

        /// <summary>Region B, and it is worth one line of test because Region A is the mirror of it and
        /// half the world flies that instead.</summary>
        [Test]
        public void RegionBPutsRedToStarboard()
        {
            Assert.That(NavChannelGeometry.IsRed(NavChannelHand.Starboard), Is.True);
            Assert.That(NavChannelGeometry.IsRed(NavChannelHand.Port), Is.False);
            Assert.That(NavChannelGeometry.LateralMarkType(NavChannelHand.Starboard, false), Is.EqualTo("StbdNun"));
            Assert.That(NavChannelGeometry.LateralMarkType(NavChannelHand.Port, false), Is.EqualTo("PortCan"));
            Assert.That(NavChannelGeometry.LateralMarkType(NavChannelHand.Starboard, true), Is.EqualTo("StbdLit"));
            Assert.That(NavChannelGeometry.LateralMarkType(NavChannelHand.Port, true), Is.EqualTo("PortLit"));
        }

        // =============================================================================================
        //  7. CONTENT VALIDATION — the plan asks only for marks the kit baked
        // =============================================================================================

        /// <summary>
        /// A mark type the kit never baked is a silently missing buoy: the placer warns and moves on,
        /// so the scene comes out short by a mark and nothing fails. This catches it at test time.
        /// </summary>
        [Test]
        public void EveryMarkTypeThePlanAsksForHasABakedDef()
        {
            Dictionary<string, HiddenHarbours.Boats.NavBuoyDef> defs =
                NavMarkPlacer.LoadDefsByMarkType(NineMileCreekNavMarks.DefFolder);

            Assert.That(defs.Count, Is.GreaterThan(0),
                $"No NavBuoyDef assets under {NineMileCreekNavMarks.DefFolder} — run " +
                "'Hidden Harbours ▸ Art ▸ Build Nav Buoy Defs'.");

            foreach (PlannedNavMark m in Concat(_nmcPlan.Marks, _spPlan.Marks))
            {
                Assert.That(defs.ContainsKey(m.MarkType), Is.True,
                    $"'{m.Id}' asks for mark type '{m.MarkType}', which no baked def carries. The kit " +
                    "bakes ten types; four more (SafeWater, Special, Regulatory, Spar) are one Build away.");

                HiddenHarbours.Boats.NavBuoyDef def = defs[m.MarkType];
                Assert.That(def.Size(m.SizeId), Is.Not.Null,
                    $"'{m.Id}' asks '{m.MarkType}' for size rung '{m.SizeId}', which that def does not " +
                    "carry. The kit's ladder is s12 s18 s20 s24 s30.");
            }
        }

        private static IEnumerable<PlannedNavMark> Concat(List<PlannedNavMark> a, List<PlannedNavMark> b)
        {
            foreach (PlannedNavMark m in a) yield return m;
            foreach (PlannedNavMark m in b) yield return m;
        }

        // =============================================================================================
        //  8. THE SCHEME IS A SCHEME, NOT A PICKET FENCE
        // =============================================================================================

        /// <summary>
        /// The handoff's own scoping, pinned: "enough to READ the channel, not a picket fence — tens per
        /// region, not hundreds". A spacing tunable that got a zero in it would otherwise ship thousands
        /// of buoys and only be noticed on the owner's next build.
        /// </summary>
        [Test]
        public void EachRegionShipsTensOfMarksNotHundreds()
        {
            Assert.That(_nmcPlan.Marks.Count, Is.InRange(4, 60),
                $"Nine Mile Creek planned {_nmcPlan.Marks.Count} marks.");
            Assert.That(_spPlan.Marks.Count, Is.InRange(4, 60),
                $"St Peters planned {_spPlan.Marks.Count} marks.");
        }

        /// <summary>
        /// The straight-fill path, which today's two regions are too short and too turny to exercise:
        /// both routes are corners end to end, so every station is a pair. A long straight must still
        /// get PACED SINGLES between its end pairs — "pairs at the ends and turns, singles along
        /// straights" — and an untested branch is how that quietly becomes a picket fence the first
        /// time somebody authors a 400 m leg.
        /// </summary>
        [Test]
        public void ALongStraightIsPacedWithSinglesBetweenItsEndPairs()
        {
            var channel = new NavChannel
            {
                Id = "channel.long_straight",
                DisplayName = "A long straight",
                Waypoints = new[] { new Vector2(0f, 0f), new Vector2(400f, 0f) },
                HalfWidthMetres = 10f,
                SearchHalfWidthMetres = 0f,
                SizeId = "s12",
                DeclaredDraughtMetres = 1f,
                KeelClearanceMetres = 0.2f,
            };
            var tuning = new NavMarkTuning { StraightSpacingMetres = 70f, MinDepthAtSpringLowMetres = 0.6f };

            // A flat bed 10 m down: nothing is refused, so the counts are purely about spacing.
            NavMarkPlanResult plan = NavMarkPlan.Plan(new[] { channel }, null, _ => -10f, 0f, 2.2f, tuning);
            NavChannelFairway f = plan.Fairway(channel.Id);

            Assert.That(f.Stations.Count, Is.GreaterThan(2),
                "A 400 m straight produced only its two end stations — the straight fill never ran.");

            // The two ends carry pairs; every station in between carries exactly one mark.
            var perStation = new Dictionary<float, int>();
            foreach (PlannedNavMark m in plan.Marks)
            {
                perStation.TryGetValue(m.AlongMetres, out int n);
                perStation[m.AlongMetres] = n + 1;
            }

            Assert.That(perStation[f.Along[0]], Is.EqualTo(2), "The seaward end is not a pair.");
            Assert.That(perStation[f.Along[f.Along.Count - 1]], Is.EqualTo(2), "The harbour end is not a pair.");
            for (int i = 1; i < f.Along.Count - 1; i++)
                Assert.That(perStation[f.Along[i]], Is.EqualTo(1),
                    $"Station {i} of a straight carries {perStation[f.Along[i]]} marks — singles pace a " +
                    "straight; pairs are for the ends and the turns.");

            // And the singles alternate sides, so a run reads as a channel rather than a fence down
            // one edge of it.
            var sides = new List<bool>();
            for (int i = 1; i < f.Along.Count - 1; i++)
                foreach (PlannedNavMark m in plan.Marks)
                    if (Mathf.Approximately(m.AlongMetres, f.Along[i]))
                        sides.Add(m.MarkType == "StbdNun" || m.MarkType == "StbdLit");

            for (int i = 1; i < sides.Count; i++)
                Assert.That(sides[i], Is.Not.EqualTo(sides[i - 1]),
                    "Two consecutive singles landed on the same hand — they are supposed to alternate.");
        }

        // =============================================================================================
        //  9. THE BUILD LOG IS THE DELIVERABLE
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The refusals ARE the finding, so the log has to carry them.</b> Both harbours bare at
        /// spring low, which means a floating mark cannot stand inside either — and the only way the
        /// owner learns that is the line this writes on every build. A refusal that dropped silently
        /// would read as "the scheme is thin" instead of "the basin dries".
        /// </summary>
        [Test]
        public void TheBuildReportNamesEveryRefusalAndItsReason()
        {
            string nmc = NavMarkPlacer.Report("NineMileCreekNavMarks", _nmcPlan, _nmcPlan.Marks.Count,
                                              NineMileCreekMainland.SpringLowWater);
            string sp = NavMarkPlacer.Report("StPetersNavMarks", _spPlan, _spPlan.Marks.Count,
                                             StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude);

            // Logged so a build (or a test run) shows the actual scheme, refusals and all.
            Debug.Log(nmc);
            Debug.Log(sp);

            foreach (var pair in new[] { (name: "Nine Mile Creek", plan: _nmcPlan, text: nmc),
                                         (name: "St Peters", plan: _spPlan, text: sp) })
            {
                StringAssert.Contains("IALA Region B", pair.text);
                if (pair.plan.Refusals.Count == 0) continue;

                StringAssert.Contains("REFUSED", pair.text,
                    $"{pair.name} refused {pair.plan.Refusals.Count} mark(s) and the report does not say so.");
                foreach (NavMarkRefusal r in pair.plan.Refusals)
                    StringAssert.Contains(r.OwnerId, pair.text,
                        $"{pair.name}: a refusal on '{r.OwnerId}' is missing from the build report.");
            }
        }

        /// <summary>
        /// Ids are stable and unique — they name GameObjects in a committed scene and appear in build
        /// logs and test failures, so a collision would silently merge two marks in a diff.
        /// </summary>
        [Test]
        public void EveryMarkIdIsUniqueWithinItsRegion()
        {
            AssertUniqueIds("Nine Mile Creek", _nmcPlan);
            AssertUniqueIds("St Peters", _spPlan);
        }

        private static void AssertUniqueIds(string region, NavMarkPlanResult plan)
        {
            var seen = new HashSet<string>();
            foreach (PlannedNavMark m in plan.Marks)
                Assert.That(seen.Add(m.Id), Is.True, $"{region}: duplicate mark id '{m.Id}'.");
        }

        // =============================================================================================
        //  7. THE TURN — a pair squared to one leg stands in the other
        // =============================================================================================

        /// <summary>
        /// ⚠⚠ <b>The defect the St Peters arrival ran into, pinned as pure geometry.</b> A station's
        /// course is the leg it lies ON; offsetting a pair square to that leg puts the inside mark
        /// <c>halfWidth × cos(turn)</c> from the leg the skipper LEAVES on, which at the entrance's
        /// 67.3° corner is 3.85 m of a 10 m fairway. The edge of a fairway is the OFFSET of its
        /// centreline, and an offset polyline mitres its corners on the bisector.
        ///
        /// <para>Measured against the LEGS, never against the formula that produced the offset — the
        /// old code was self-consistent too, and that is exactly why every test passed over it.</para>
        /// </summary>
        [Test]
        public void ATurnsPairStandsTheFullHalfWidthFromBothLegs()
        {
            const float halfWidth = 10f;

            // A 60-degree turn to starboard: in heading east, out heading south-east-ish.
            Vector2 courseIn = Vector2.right;
            Vector2 courseOut = new Vector2(Mathf.Cos(-60f * Mathf.Deg2Rad),
                                            Mathf.Sin(-60f * Mathf.Deg2Rad));
            Vector2 vertex = new Vector2(100f, 0f);

            Vector2 edge = NavChannelGeometry.PortEdgeOffset(courseIn, courseOut, 2f);

            foreach ((string hand, Vector2 at) in new[]
                     { ("port",      vertex + edge * halfWidth),
                       ("starboard", vertex - edge * halfWidth) })
            {
                float fromIn  = Mathf.Abs(Vector2.Dot(at - vertex, NavChannelGeometry.PortNormal(courseIn)));
                float fromOut = Mathf.Abs(Vector2.Dot(at - vertex, NavChannelGeometry.PortNormal(courseOut)));

                Assert.That(fromIn, Is.EqualTo(halfWidth).Within(1e-3f),
                    $"the {hand} mark stands {fromIn:F2} m from the leg she arrives on, not the " +
                    $"{halfWidth:F2} m the channel claims to be wide.");
                Assert.That(fromOut, Is.EqualTo(halfWidth).Within(1e-3f),
                    $"the {hand} mark stands {fromOut:F2} m from the leg she LEAVES on. A pair squared " +
                    "to one leg only is the defect: the inside mark ends up in the fairway and the " +
                    "boat runs through it.");
            }
        }

        /// <summary>
        /// On a straight there is no corner to mitre, and the mitre must be exactly the identity — not
        /// nearly. Every mark on every straight in both regions was placed by the old arithmetic and
        /// must not move by a millimetre for a change that is about turns.
        /// </summary>
        [Test]
        public void AStraightIsUnmovedByTheMitre()
        {
            foreach (Vector2 course in new[]
                     { Vector2.right, Vector2.up, new Vector2(3f, -4f).normalized })
            {
                Vector2 edge = NavChannelGeometry.PortEdgeOffset(course, course, 2f);
                Assert.That(Vector2.Distance(edge, NavChannelGeometry.PortNormal(course)),
                    Is.LessThan(1e-5f),
                    $"a station on a straight ({course}) moved its marks. The mitre is 1 where there " +
                    "is no turn, and anything else is a regression dressed as a fix.");
            }
        }

        /// <summary>
        /// ⚠ A mitre grows without bound as a turn approaches 180°, so an unclamped join would fling a
        /// hairpin's marks into open water (and, in both of this world's harbours, onto a shoal where
        /// they would be refused every build forever). The limit is a stated tunable, and this is what
        /// it buys: the offset never exceeds it, at any angle.
        /// </summary>
        [Test]
        public void TheMitreLimitHoldsAtEveryAngle()
        {
            const float limit = 2f;
            Vector2 courseIn = Vector2.right;

            for (int deg = 0; deg <= 179; deg++)
            {
                Vector2 courseOut = new Vector2(Mathf.Cos(-deg * Mathf.Deg2Rad),
                                                Mathf.Sin(-deg * Mathf.Deg2Rad));
                float length = NavChannelGeometry.PortEdgeOffset(courseIn, courseOut, limit).magnitude;

                Assert.That(length, Is.LessThanOrEqualTo(limit + 1e-4f),
                    $"a {deg}° turn pushed its marks out to {length:F2}× the half-width against a " +
                    $"limit of {limit:F1}×.");
                Assert.That(length, Is.GreaterThanOrEqualTo(1f - 1e-4f),
                    $"a {deg}° turn pulled its marks INSIDE the channel's own half-width " +
                    $"({length:F2}×). A turn may widen the join; it may never narrow the fairway.");
            }
        }

        /// <summary>
        /// The two courses a station has. On a straight they are one vector; at a vertex they are the
        /// legs either side of it — and reading only the first is what squared a pair to one leg.
        /// </summary>
        [Test]
        public void CoursesAtSeesBothLegsAtAVertex_AndOneOnAStraight()
        {
            var points = new[] { new Vector2(0f, 0f), new Vector2(100f, 0f), new Vector2(100f, 100f) };

            NavChannelGeometry.CoursesAt(points, 50f, out Vector2 midIn, out Vector2 midOut);
            Assert.That(Vector2.Distance(midIn, midOut), Is.LessThan(1e-5f),
                "mid-straight, the leg a station lies on and the leg it leads into are the same leg");

            NavChannelGeometry.CoursesAt(points, 100f, out Vector2 cornerIn, out Vector2 cornerOut);
            Assert.That(Vector2.Distance(cornerIn, Vector2.right), Is.LessThan(1e-4f),
                "at the vertex, the inbound leg is the one she arrives on");
            Assert.That(Vector2.Distance(cornerOut, Vector2.up), Is.LessThan(1e-4f),
                "at the vertex, the outbound leg is the one she leaves on — reading the inbound one " +
                "twice is exactly how a pair ends up square to half a turn");

            NavChannelGeometry.CoursesAt(points, 200f, out Vector2 endIn, out Vector2 endOut);
            Assert.That(Vector2.Distance(endIn, endOut), Is.LessThan(1e-5f),
                "at the harbour end there is no leg to lead into; the last one is all there is");
        }

        /// <summary>
        /// The regions, from the marks' side: no lateral on a real channel stands inside the width its
        /// own channel claims — measured against BOTH legs that meet at its station, which is the pair
        /// of lines the mark is a statement about. Same property as
        /// <see cref="ATurnsPairStandsTheFullHalfWidthFromBothLegs"/>, asked of the two routes that
        /// actually ship.
        ///
        /// <para>⚠ <b>Measured against the LEGS, not against the derived centreline — the first draft
        /// got that wrong and the run said so.</b> A station may be SNAPPED sideways into deeper water
        /// while its courses stay the authored ones (Nine Mile Creek's are), so the polyline through the
        /// derived stations runs at an angle to the legs the marks were squared to: one mark read
        /// 11.14 m from that line against a 12 m claim while being exactly 12 m from both of its own
        /// legs. The half-width is a promise about the LEGS, and asserting it about the snapped line
        /// asserts something the planner has never done and never claimed to.</para>
        /// </summary>
        [Test]
        public void NoLateralStandsInsideItsOwnFairway()
        {
            AssertNoLateralIntrudes("Nine Mile Creek", _nmcPlan, NineMileCreekNavMarks.Channels);
            AssertNoLateralIntrudes("St Peters", _spPlan, StPetersNavMarks.Channels);
        }

        private static void AssertNoLateralIntrudes(
            string region, NavMarkPlanResult plan,
            System.Collections.Generic.IReadOnlyList<NavChannel> channels)
        {
            foreach (PlannedNavMark m in plan.Marks)
            {
                if (!m.IsLateral) continue;

                NavChannelFairway fairway = plan.Fairway(m.OwnerId);
                NavChannel channel = null;
                foreach (NavChannel c in channels)
                    if (c.Id == m.OwnerId) channel = c;
                if (fairway == null || channel == null || channel.HalfWidthMetres <= 0f) continue;

                int s = NearestStation(fairway, m.AlongMetres);
                NavChannelGeometry.CoursesAt(channel.Waypoints, fairway.Along[s],
                                             out Vector2 courseIn, out Vector2 courseOut);

                Vector2 fromStation = m.At - fairway.Stations[s];
                foreach ((string leg, Vector2 course) in new[]
                         { ("she arrives on", courseIn), ("she leaves on", courseOut) })
                {
                    float across = Mathf.Abs(Vector2.Dot(
                        fromStation, NavChannelGeometry.PortNormal(course)));

                    Assert.That(across, Is.GreaterThanOrEqualTo(channel.HalfWidthMetres - 0.05f),
                        $"{region}: '{m.Id}' stands {across:F2} m off the leg {leg}, on a fairway that " +
                        $"claims {channel.HalfWidthMetres:F2} m each side. A mark inside its own " +
                        "channel is a mark the boats it guides will hit.");
                }
            }
        }

        // =============================================================================================
        //  8. THE MOORING — she yields, she rebounds, she settles, and she never leaves
        // =============================================================================================

        /// <summary>The integrator Unity uses on a rigidbody: velocity from the acceleration, then
        /// position from the velocity. Semi-implicit Euler, the same order <c>NavBuoyMooring</c>'s
        /// <c>AddForce</c> + <c>FixedUpdate</c> produces — so this walks the shipped arithmetic rather
        /// than a tidier one that would agree only with itself.</summary>
        private static void StepMooring(ref Vector2 offset, ref Vector2 velocity,
                                        float spring, float ratio, float watchRadius, float dt)
        {
            float damping = NavBuoyMooringMath.DampingFor(spring, ratio);
            velocity += NavBuoyMooringMath.RestoringAcceleration(offset, velocity, spring, damping) * dt;
            offset += velocity * dt;

            NavBuoyMooringMath.Held held =
                NavBuoyMooringMath.HoldTheWatchCircle(offset, velocity, watchRadius);
            offset = held.Offset;
            velocity = held.Velocity;
        }

        /// <summary>
        /// ⭐ <b>The owner's word, as arithmetic: "struck, it yields, rebounds, and settles home".</b>
        /// Shoved two metres off her anchor and still running, an under-damped mark must come back
        /// PAST her anchor at least once — that overshoot is the whole difference between a buoy and a
        /// door closer — and then be home and still.
        /// </summary>
        [Test]
        public void AStruckMarkRebounds_AndSettlesBackOnHerAnchor()
        {
            const float dt = 0.02f;               // Unity's default fixed step
            var offset = new Vector2(2f, 0f);
            var velocity = new Vector2(0.5f, 0f);

            bool rebounded = false;
            float farthest = offset.magnitude;
            for (int i = 0; i < 1500; i++)         // 30 s
            {
                StepMooring(ref offset, ref velocity, 4f, 0.5f, 3f, dt);
                farthest = Mathf.Max(farthest, offset.magnitude);
                if (offset.x < -0.05f) rebounded = true;
            }

            Assert.That(rebounded, Is.True,
                "she was shoved off her anchor and crept back without ever overshooting it. That is a " +
                "damper, not a mooring — check NavBuoyDef.MooringDampingRatio has not been pushed to 1.");
            Assert.That(offset.magnitude, Is.LessThan(0.01f),
                $"30 s after the knock she is still {offset.magnitude:F3} m off her anchor. A mark that " +
                "does not settle is a mark that no longer marks where it was placed.");
            Assert.That(velocity.magnitude, Is.LessThan(0.01f),
                "she settled onto her anchor but is still moving — the spring and the damping disagree.");
            Assert.That(farthest, Is.LessThanOrEqualTo(3f + 1e-3f),
                $"she reached {farthest:F2} m from her anchor against a 3.00 m watch circle.");
        }

        /// <summary>At critical damping she comes home without overshooting at all. The tunable is a
        /// RATIO of critical precisely so that this is the value 1 means and not a number to hunt for.</summary>
        [Test]
        public void AtCriticalDampingSheDoesNotOvershoot()
        {
            var offset = new Vector2(2f, 0f);
            var velocity = Vector2.zero;

            for (int i = 0; i < 1500; i++)
            {
                StepMooring(ref offset, ref velocity, 4f, 1f, 3f, 0.02f);
                Assert.That(offset.x, Is.GreaterThan(-1e-3f),
                    $"critically damped, she overshot her anchor to {offset.x:F4} m on step {i}.");
            }
            Assert.That(offset.magnitude, Is.LessThan(0.01f), "and she still has to get home");
        }

        /// <summary>
        /// ⚠ <b>The chain takes her OUTWARD speed and leaves her swing.</b> Killing the whole velocity
        /// at the rim would make a glancing blow vanish into the mooring, which reads as hitting the
        /// sea floor rather than a floating object. Asserted component by component, because "she
        /// slowed down" is true of both behaviours.
        /// </summary>
        [Test]
        public void TheChainTakesTheOutwardSpeedAndLeavesTheSwing()
        {
            var offset = new Vector2(5f, 0f);                 // 5 m out on a 3 m circle
            var velocity = new Vector2(2f, 1.5f);             // 2 outward, 1.5 across

            NavBuoyMooringMath.Held held = NavBuoyMooringMath.HoldTheWatchCircle(offset, velocity, 3f);

            Assert.That(held.Taut, Is.True, "5 m out on a 3 m circle and the chain is reported slack");
            Assert.That(held.Offset.magnitude, Is.EqualTo(3f).Within(1e-4f),
                "she is not on the rim of her own watch circle");
            Assert.That(held.Velocity.x, Is.EqualTo(0f).Within(1e-4f),
                "the outward speed survived the chain coming taut — she will keep going");
            Assert.That(held.Velocity.y, Is.EqualTo(1.5f).Within(1e-4f),
                "her speed ALONG the rim was taken too. A moored buoy struck a glancing blow swings " +
                "round her anchor; one that stops dead reads as a collision with the ground.");
        }

        /// <summary>Inside her watch circle the chain is slack and says nothing at all — the spring is
        /// the only thing acting. A chain that pulled from the middle would be a second spring.</summary>
        [Test]
        public void InsideTheWatchCircleTheChainIsSlack()
        {
            var offset = new Vector2(1f, 1f);                 // 1.41 m out on a 3 m circle
            var velocity = new Vector2(2f, -1f);

            NavBuoyMooringMath.Held held = NavBuoyMooringMath.HoldTheWatchCircle(offset, velocity, 3f);

            Assert.That(held.Taut, Is.False);
            Assert.That(Vector2.Distance(held.Offset, offset), Is.LessThan(1e-6f));
            Assert.That(Vector2.Distance(held.Velocity, velocity), Is.LessThan(1e-6f));
        }

        /// <summary>The ratio is a ratio: 1 IS critical damping for the spring beside it.</summary>
        [Test]
        public void TheDampingRatioIsAFractionOfCritical()
        {
            foreach (float spring in new[] { 1f, 4f, 9f, 25f })
                Assert.That(NavBuoyMooringMath.DampingFor(spring, 1f),
                    Is.EqualTo(2f * Mathf.Sqrt(spring)).Within(1e-4f),
                    $"a ratio of 1 must be critical damping for a spring of {spring}.");

            Assert.That(NavBuoyMooringMath.DampingFor(4f, 0.5f),
                Is.EqualTo(0.5f * 2f * Mathf.Sqrt(4f)).Within(1e-4f));
        }
    }
}
