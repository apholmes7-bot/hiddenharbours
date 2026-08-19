using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
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
    }
}
