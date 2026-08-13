using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>THE DETERMINISM ORACLE.</b> "Where is she at 14:20?" has to be a PURE function of
    /// <c>(worldSeed, gameTime)</c> — recomputed, never ticked-and-saved (CLAUDE.md rule 5) — and this is
    /// where that is checked rather than asserted in a comment:
    ///
    /// <list type="bullet">
    ///   <item>the same seed at the same hour, twice, is the same position <b>bit for bit</b>;</item>
    ///   <item>a different seed moves somebody (the sabotage control — an oracle that cannot fail is not an
    ///   oracle);</item>
    ///   <item>a frozen clock freezes the village (the other sabotage control);</item>
    ///   <item>a villager sampled mid-leg is ON the leg — not at the station she left and not at the one
    ///   she is going to — which is what makes a region loading at 14:20 put her mid-stride;</item>
    ///   <item>the threshold is crossed at the DOOR, measured in metres along the walk, in both
    ///   directions — so she is drawn all the way down the lane and hidden only once she is inside.</item>
    /// </list>
    ///
    /// <para>Bit-exactness is asserted with <c>Is.EqualTo</c> on the raw floats deliberately. It is legal
    /// here where it would not be between two different transcriptions of the same formula: this is the
    /// SAME code path called twice in one process, so any difference at all would mean hidden state.</para>
    /// </summary>
    public class RoutinePlanTests
    {
        const float SecondsPerGameHour = 1800f / 24f;   // the pinned 1800 s day

        readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- the rig ----------------------------------------------------------------------------

        // green(0) ── lane(1) ── school(2)
        //        └─── shore(3)
        RoutineLanes MakeLanes()
        {
            var go = new GameObject("lanes");
            _spawned.Add(go);
            var lanes = go.AddComponent<RoutineLanes>();
            lanes.Configure(
                nodePositions: new[]
                {
                    new Vector2(0f, 0f), new Vector2(0f, 20f), new Vector2(-15f, 20f),
                    new Vector2(0f, -30f),
                },
                nodeParents: new[] { RoutineLaneTree.NoParent, 0, 1, 0 },
                nodeNames: new[] { "green", "lane", "school", "shore" },
                viaStart: new int[4], viaCount: new int[4], via: System.Array.Empty<Vector2>());
            Assert.That(lanes.Validate(), Is.Empty, "the test rig's own lane table must be sound");
            return lanes;
        }

        RoutineStations MakeStations(BuildingInterior indoor = null)
        {
            var go = new GameObject("stations");
            _spawned.Add(go);
            var stations = go.AddComponent<RoutineStations>();
            var list = new List<RoutineStationEntry>
            {
                new RoutineStationEntry
                {
                    Id = "s.green", Position = new Vector2(0f, 0f), StandHeadingDegrees = 0f,
                    LaneNodeName = "green", Why = "the green",
                },
                new RoutineStationEntry
                {
                    Id = "s.school_yard", Position = new Vector2(-15f, 20f), StandHeadingDegrees = 180f,
                    LaneNodeName = "school", Why = "the schoolhouse dooryard",
                },
                new RoutineStationEntry
                {
                    Id = "s.shore", Position = new Vector2(0f, -30f), StandHeadingDegrees = 270f,
                    LaneNodeName = "shore", Why = "the shore",
                },
            };
            if (indoor != null)
                list.Add(new RoutineStationEntry
                {
                    Id = "s.inside", Position = indoor.Footprint.Centre, StandHeadingDegrees = 0f,
                    Interior = indoor, LaneNodeName = "school", Why = "inside the schoolhouse",
                });
            stations.Configure(list.ToArray());
            return stations;
        }

        RoutineDef MakeRoutine(string id, params (float hour, string station)[] blocks)
        {
            var def = ScriptableObject.CreateInstance<RoutineDef>();
            _spawned.Add(def);
            def.Id = id;
            def.WalkSpeedMetresPerSecond = 1.2f;
            def.ScheduleJitterMinutes = 6f;
            var entries = new RoutineEntry[blocks.Length];
            for (int i = 0; i < blocks.Length; i++)
                entries[i] = new RoutineEntry
                {
                    StartHour = blocks[i].hour, StationId = blocks[i].station,
                    Activity = RoutineActivity.Work, Why = "test",
                };
            def.Entries = entries;
            // An NpcDef is only needed by the region builder's matching, not by the planner — Build takes
            // the def itself. Left null on purpose so this rig has no asset dependency at all.
            return def;
        }

        RoutinePlan Plan(int worldSeed, RoutineStations stations, RoutineLanes lanes, RoutineDef routine)
        {
            RoutinePlan plan = RoutinePlanner.Build(routine, stations, lanes, worldSeed, out _,
                                                   out string problem);
            Assert.That(plan, Is.Not.Null, problem);
            return plan;
        }

        RoutinePlan SimpleDay(int worldSeed = 1234)
        {
            RoutineLanes lanes = MakeLanes();
            RoutineStations stations = MakeStations();
            RoutineDef routine = MakeRoutine("routine.test",
                                             (7f, "s.green"), (9f, "s.school_yard"),
                                             (14f, "s.shore"), (20f, "s.green"));
            return Plan(worldSeed, stations, lanes, routine);
        }

        // ---- determinism -------------------------------------------------------------------------

        [Test]
        public void SameSeedSameHour_IsTheSamePose_BitForBit()
        {
            RoutinePlan a = SimpleDay(4242);
            RoutinePlan b = SimpleDay(4242);

            for (float h = 0f; h < 24f; h += 0.05f)
            {
                RoutinePose pa = a.SampleAt(h, SecondsPerGameHour);
                RoutinePose pb = b.SampleAt(h, SecondsPerGameHour);
                Assert.That(pb.Position.x, Is.EqualTo(pa.Position.x), $"x at h={h:0.00}");
                Assert.That(pb.Position.y, Is.EqualTo(pa.Position.y), $"y at h={h:0.00}");
                Assert.That(pb.BlockIndex, Is.EqualTo(pa.BlockIndex), $"block at h={h:0.00}");
                Assert.That(pb.Walking, Is.EqualTo(pa.Walking), $"walking at h={h:0.00}");
                Assert.That(pb.HeadingDegrees, Is.EqualTo(pa.HeadingDegrees), $"heading at h={h:0.00}");
            }
        }

        [Test]
        public void SamplingTheSameHourRepeatedly_NeverMovesAnybody()
        {
            RoutinePlan plan = SimpleDay();
            RoutinePose first = plan.SampleAt(14.37f, SecondsPerGameHour);
            for (int i = 0; i < 100; i++)
            {
                RoutinePose again = plan.SampleAt(14.37f, SecondsPerGameHour);
                Assert.That(again.Position, Is.EqualTo(first.Position),
                            "a frozen clock has to freeze the village — if this drifts, something is " +
                            "accumulating that should not be");
            }
        }

        /// <summary>
        /// ⭐ THE SABOTAGE CONTROL. Perturb the world seed and somebody has to move, or the "determinism"
        /// above is really just "the seed is ignored" and the test would pass on a broken engine.
        /// </summary>
        [Test]
        public void ADifferentWorldSeed_MovesSomebody()
        {
            RoutinePlan a = SimpleDay(1);
            RoutinePlan b = SimpleDay(2);

            bool departuresDiffer = false;
            for (int i = 0; i < a.BlockCount; i++)
                if (!Mathf.Approximately(a.DepartureHours[i], b.DepartureHours[i])) departuresDiffer = true;
            Assert.That(departuresDiffer, Is.True, "the seeded jitter must actually depend on the seed");

            bool posesDiffer = false;
            for (float h = 0f; h < 24f && !posesDiffer; h += 0.01f)
                if (a.SampleAt(h, SecondsPerGameHour).Position !=
                    b.SampleAt(h, SecondsPerGameHour).Position) posesDiffer = true;
            Assert.That(posesDiffer, Is.True,
                        "two different worlds must put her in different places at SOME hour of the day");
        }

        // ---- the walk ----------------------------------------------------------------------------

        [Test]
        public void AtTheDeparture_SheIsStillAtThePlaceSheIsLeaving()
        {
            RoutinePlan plan = SimpleDay();
            int block = 1;
            RoutinePose pose = plan.SampleAt(plan.DepartureHours[block], SecondsPerGameHour);
            Assert.That(pose.BlockIndex, Is.EqualTo(block));
            Vector2 from = plan.Waypoints[plan.RouteStart[block]];
            Assert.That(Vector2.Distance(pose.Position, from), Is.LessThan(1e-3f));
        }

        /// <summary>
        /// ⭐ THE MID-LEG CASE, which is the one that matters most: this is exactly what a region loading
        /// at an arbitrary hour has to produce. Halfway through the walk she is halfway along the route —
        /// on it, and at neither end of it.
        /// </summary>
        [Test]
        public void MidWalk_SheIsOnTheLeg_AtNeitherEnd()
        {
            RoutinePlan plan = SimpleDay();
            int block = 1;
            float travel = plan.TravelHours(block, SecondsPerGameHour);
            Assert.That(travel, Is.GreaterThan(0.05f), "the rig's leg must be long enough to be mid-way IN");

            float h = plan.DepartureHours[block] + travel * 0.5f;
            RoutinePose pose = plan.SampleAt(h, SecondsPerGameHour);

            Assert.That(pose.Walking, Is.True);
            Vector2 from = plan.Waypoints[plan.RouteStart[block]];
            Vector2 to = plan.Waypoints[plan.RouteStart[block] + plan.RouteCount[block] - 1];
            Assert.That(Vector2.Distance(pose.Position, from), Is.GreaterThan(1f), "left where she was");
            Assert.That(Vector2.Distance(pose.Position, to), Is.GreaterThan(1f), "not arrived yet");

            // ON the polyline: she is where walking that far along it puts her.
            //
            // ⚠️ Within a millimetre, NOT bit-exact — and the difference is the point. The determinism cases
            // above compare the SAME code path called twice, where any difference at all would mean hidden
            // state. This compares two TRANSCRIPTIONS of one formula: SampleAt derives its distance from
            // `ElapsedHours(departure + travel/2, departure)` while the line below derives it from
            // `travel/2` directly, and a float round-trip through that addition is not required to land on
            // the same bit.
            float walked = RoutineSchedule.DistanceWalked(travel * 0.5f, plan.WalkSpeedMetresPerSecond,
                                                          SecondsPerGameHour);
            Assert.That(walked, Is.EqualTo(plan.RouteLengthMetres[block] * 0.5f).Within(0.05f));
            Vector2 expected = RoutineLaneTree.PointAlong(plan.Waypoints, plan.RouteStart[block],
                                                          plan.RouteCount[block], walked);
            Assert.That(Vector2.Distance(pose.Position, expected), Is.LessThan(1e-3f),
                        $"sampled {pose.Position}, walked-along-the-route gives {expected}");
        }

        /// <summary>
        /// ⭐ THE DAY IS CONTINUOUS: every block's route BEGINS where the previous one ENDED. Nothing else
        /// stops the villager teleporting at a departure hour, and it is exactly what went wrong once — the
        /// flattened waypoint array's duplicate-collapse fired ACROSS the slice boundary and dropped each
        /// route's first point, so at every departure she jumped to the second point of her walk (20 m, on
        /// this rig). See <c>RoutinePlanner.Append</c>.
        /// </summary>
        [Test]
        public void EveryBlockSetsOffFromWhereTheLastOneArrived()
        {
            foreach (RoutinePlan plan in new[] { SimpleDay(3), SimpleDay(77) })
                for (int i = 0; i < plan.BlockCount; i++)
                {
                    int prev = (i - 1 + plan.BlockCount) % plan.BlockCount;
                    Vector2 arrived = plan.Waypoints[plan.RouteStart[prev] + plan.RouteCount[prev] - 1];
                    Vector2 setsOff = plan.Waypoints[plan.RouteStart[i]];
                    Assert.That(Vector2.Distance(arrived, setsOff), Is.LessThan(1e-3f),
                                $"block {prev} ends at {arrived} but block {i} sets off from {setsOff} — " +
                                "that gap is a teleport at the departure hour");
                }
        }

        [Test]
        public void OnceTheWalkIsDone_SheStandsAtTheStation_AndStaysThere()
        {
            RoutinePlan plan = SimpleDay();
            int block = 1;
            float travel = plan.TravelHours(block, SecondsPerGameHour);
            Vector2 station = plan.Waypoints[plan.RouteStart[block] + plan.RouteCount[block] - 1];

            RoutinePose onArrival = plan.SampleAt(plan.DepartureHours[block] + travel, SecondsPerGameHour);
            Assert.That(onArrival.Walking, Is.False);
            Assert.That(Vector2.Distance(onArrival.Position, station), Is.LessThan(1e-3f));

            // …and through the rest of the block.
            float gap = RoutineSchedule.GapHours(plan.DepartureHours, block);
            for (float t = travel; t < gap; t += 0.1f)
            {
                RoutinePose dwell = plan.SampleAt(plan.DepartureHours[block] + t, SecondsPerGameHour);
                Assert.That(dwell.Position, Is.EqualTo(station), $"drifted {t:0.0} h into the dwell");
                Assert.That(dwell.HeadingDegrees, Is.EqualTo(180f),
                            "and she keeps the station's own stance while she stands there");
            }
        }

        [Test]
        public void WhileWalking_TheFacingFollowsTheRoute_NotTheStation()
        {
            RoutinePlan plan = SimpleDay();
            int block = 1;   // green → the school dooryard: due north up the lane first.
            float travel = plan.TravelHours(block, SecondsPerGameHour);
            RoutinePose pose = plan.SampleAt(plan.DepartureHours[block] + travel * 0.1f, SecondsPerGameHour);
            Assert.That(pose.Walking, Is.True);
            Assert.That(pose.HeadingDegrees, Is.EqualTo(0f).Within(1e-2f),
                        "up the lane is due north, and the first frame of a leg has no previous position " +
                        "to difference — which is exactly why the heading comes off the route");
        }

        [Test]
        public void TheDayCloses_TheLastBlockLeadsBackToTheFirst()
        {
            RoutinePlan plan = SimpleDay();
            int last = plan.BlockCount - 1;
            Vector2 endOfDay = plan.Waypoints[plan.RouteStart[last] + plan.RouteCount[last] - 1];
            Vector2 startOfDay = plan.Waypoints[plan.RouteStart[0]];
            Assert.That(Vector2.Distance(endOfDay, startOfDay), Is.LessThan(1e-3f),
                        "block 0's route must set off from where the last block's ended, or the villager " +
                        "teleports once a day at whatever hour that is");
        }

        [Test]
        public void TwoBlocksAtOneStation_IsAZeroLengthWalk_NotADivision()
        {
            RoutineLanes lanes = MakeLanes();
            RoutineStations stations = MakeStations();
            RoutineDef routine = MakeRoutine("routine.still", (8f, "s.green"), (16f, "s.green"));
            RoutinePlan plan = Plan(7, stations, lanes, routine);

            for (float h = 0f; h < 24f; h += 0.25f)
            {
                RoutinePose pose = plan.SampleAt(h, SecondsPerGameHour);
                Assert.That(pose.Walking, Is.False);
                Assert.That(float.IsNaN(pose.Position.x) || float.IsNaN(pose.Position.y), Is.False);
                Assert.That(pose.Position, Is.EqualTo(new Vector2(0f, 0f)));
            }
        }

        // ---- the threshold -----------------------------------------------------------------------

        BuildingInterior MakeInterior(Vector2 centre)
        {
            var go = new GameObject("schoolhouse");
            _spawned.Add(go);
            go.transform.position = new Vector3(centre.x, centre.y, 0f);
            var interior = go.AddComponent<BuildingInterior>();
            // Facing 0 of 8, the house family's door on −y, centred. The depth scale is the shared bake
            // camera's sin 40°, which is what the real builder passes.
            interior.Configure(shell: null, room: null, props: null,
                               widthMetres: 6.4f, lengthMetres: 7.6f, facing: 0, facings: 8,
                               groundDepthScale: 0.6427876f,
                               wallThicknessMetres: 0.3f, doorwayWidthMetres: 1.4f);
            return interior;
        }

        /// <summary>
        /// ⭐ WALKING IN THROUGH THE DRAWN DOOR. She is OUTDOORS all the way up the lane and hidden only
        /// once she is past her own doorway — which is the diegetic payoff, and the inverse of the
        /// teleport-from-the-lane the seamless-interiors ruling forbids.
        /// </summary>
        [Test]
        public void WalkingIntoARoom_SheIsOutdoorsUntilTheDoor_ThenInside()
        {
            BuildingInterior interior = MakeInterior(new Vector2(-15f, 27f));
            RoutineLanes lanes = MakeLanes();
            RoutineStations stations = MakeStations(interior);
            RoutineDef routine = MakeRoutine("routine.inside", (8f, "s.green"), (10f, "s.inside"));
            RoutinePlan plan = Plan(11, stations, lanes, routine);

            const int inBlock = 1;
            float travel = plan.TravelHours(inBlock, SecondsPerGameHour);
            Assert.That(travel, Is.GreaterThan(0.05f));

            float length = plan.RouteLengthMetres[inBlock];
            float enterAt = plan.EnterAtMetres[inBlock];
            Assert.That(enterAt, Is.GreaterThan(0f).And.LessThan(length),
                        "the threshold has to be INSIDE the walk, or she never crosses it visibly");

            // A hair before the door: still out in the world.
            float beforeHours = RoutineSchedule.TravelHours(enterAt - 0.15f,
                                                            plan.WalkSpeedMetresPerSecond,
                                                            SecondsPerGameHour);
            RoutinePose before = plan.SampleAt(plan.DepartureHours[inBlock] + beforeHours,
                                               SecondsPerGameHour);
            Assert.That(before.Shelter, Is.EqualTo(RoutineShelter.Outdoors));

            // A hair after: inside, and hidden unless the player is in there too.
            float afterHours = RoutineSchedule.TravelHours(enterAt + 0.15f,
                                                           plan.WalkSpeedMetresPerSecond,
                                                           SecondsPerGameHour);
            RoutinePose after = plan.SampleAt(plan.DepartureHours[inBlock] + afterHours,
                                              SecondsPerGameHour);
            Assert.That(after.Shelter, Is.EqualTo(RoutineShelter.InsideDestination));

            // …and standing there is inside too.
            RoutinePose standing = plan.SampleAt(plan.DepartureHours[inBlock] + travel + 1f,
                                                 SecondsPerGameHour);
            Assert.That(standing.Shelter, Is.EqualTo(RoutineShelter.InsideDestination));
            Assert.That(standing.Walking, Is.False);
        }

        [Test]
        public void WalkingOutOfARoom_SheIsInsideUntilTheDoor_ThenOutdoors()
        {
            BuildingInterior interior = MakeInterior(new Vector2(-15f, 27f));
            RoutineLanes lanes = MakeLanes();
            RoutineStations stations = MakeStations(interior);
            RoutineDef routine = MakeRoutine("routine.outside", (8f, "s.inside"), (10f, "s.green"));
            RoutinePlan plan = Plan(11, stations, lanes, routine);

            const int outBlock = 1;   // s.inside → s.green
            float exitAt = plan.ExitAtMetres[outBlock];
            Assert.That(exitAt, Is.GreaterThan(0f), "leaving a room starts inside it");

            float insideHours = RoutineSchedule.TravelHours(exitAt * 0.5f,
                                                            plan.WalkSpeedMetresPerSecond,
                                                            SecondsPerGameHour);
            Assert.That(plan.SampleAt(plan.DepartureHours[outBlock] + insideHours,
                                      SecondsPerGameHour).Shelter,
                        Is.EqualTo(RoutineShelter.InsideOrigin));

            float outHours = RoutineSchedule.TravelHours(exitAt + 0.5f, plan.WalkSpeedMetresPerSecond,
                                                         SecondsPerGameHour);
            Assert.That(plan.SampleAt(plan.DepartureHours[outBlock] + outHours,
                                      SecondsPerGameHour).Shelter,
                        Is.EqualTo(RoutineShelter.Outdoors),
                        "past her own doorway she is out in the world and must be drawn");
        }

        [Test]
        public void TheRouteToARoom_PassesThroughTheDRAWNDoor_NotTheWallCentre()
        {
            BuildingInterior interior = MakeInterior(new Vector2(-15f, 27f));
            RoutineLanes lanes = MakeLanes();
            RoutineStations stations = MakeStations(interior);
            RoutineDef routine = MakeRoutine("routine.door", (8f, "s.green"), (10f, "s.inside"));
            RoutinePlan plan = Plan(11, stations, lanes, routine);

            const int inBlock = 1;
            int last = plan.RouteStart[inBlock] + plan.RouteCount[inBlock] - 1;
            Assert.That(Vector2.Distance(plan.Waypoints[last - 1], interior.DoorWorld),
                        Is.LessThan(1e-3f),
                        "the last-but-one waypoint IS the threshold the player can see her cross");
        }

        // ---- the planner refuses rather than guesses -----------------------------------------------

        [Test]
        public void Build_RefusesAStationThisRegionDoesNotHave_AndNamesIt()
        {
            RoutineDef routine = MakeRoutine("routine.bad", (8f, "s.green"), (12f, "s.nowhere"));
            RoutinePlan plan = RoutinePlanner.Build(routine, MakeStations(), MakeLanes(), 1, out _,
                                                   out string problem);
            Assert.That(plan, Is.Null);
            Assert.That(problem, Does.Contain("s.nowhere"));
        }

        [Test]
        public void Build_RefusesAOneBlockDay()
        {
            RoutineDef routine = MakeRoutine("routine.short", (8f, "s.green"));
            RoutinePlan plan = RoutinePlanner.Build(routine, MakeStations(), MakeLanes(), 1, out _,
                                                   out string problem);
            Assert.That(plan, Is.Null);
            Assert.That(problem, Does.Contain("at least two"));
        }

        [Test]
        public void Build_RefusesAStationHangingOffALaneNodeThatIsNotThere()
        {
            RoutineLanes lanes = MakeLanes();
            var go = new GameObject("stations");
            _spawned.Add(go);
            var stations = go.AddComponent<RoutineStations>();
            stations.Configure(new[]
            {
                new RoutineStationEntry { Id = "s.a", Position = Vector2.zero, LaneNodeName = "green" },
                new RoutineStationEntry { Id = "s.b", Position = Vector2.one, LaneNodeName = "nowhere" },
            });

            RoutineDef routine = MakeRoutine("routine.orphan", (8f, "s.a"), (12f, "s.b"));
            RoutinePlan plan = RoutinePlanner.Build(routine, stations, lanes, 1, out _, out string problem);
            Assert.That(plan, Is.Null);
            Assert.That(problem, Does.Contain("nowhere"));
        }

        [Test]
        public void Build_RefusesAnUnwalkableLaneNetwork()
        {
            var go = new GameObject("brokenLanes");
            _spawned.Add(go);
            var lanes = go.AddComponent<RoutineLanes>();
            lanes.Configure(new[] { Vector2.zero, Vector2.one },
                            new[] { 1, 0 },                        // a two-node cycle: no root
                            new[] { "a", "b" },
                            new int[2], new int[2], System.Array.Empty<Vector2>());
            Assert.That(lanes.Validate(), Is.Not.Empty);

            RoutineDef routine = MakeRoutine("routine.cycle", (8f, "s.green"), (12f, "s.shore"));
            RoutinePlan plan = RoutinePlanner.Build(routine, MakeStations(), lanes, 1, out _,
                                                   out string problem);
            Assert.That(plan, Is.Null);
            Assert.That(problem, Does.Contain("lane network"));
        }

        [Test]
        public void Build_ReportsAnInteriorPerBlock_NullForTheOutdoorOnes()
        {
            BuildingInterior interior = MakeInterior(new Vector2(-15f, 27f));
            RoutineDef routine = MakeRoutine("routine.mix",
                                             (8f, "s.green"), (10f, "s.inside"), (16f, "s.shore"));
            RoutinePlan plan = RoutinePlanner.Build(routine, MakeStations(interior), MakeLanes(), 1,
                                                   out BuildingInterior[] interiors, out string problem);
            Assert.That(plan, Is.Not.Null, problem);
            Assert.That(interiors.Length, Is.EqualTo(3));
            Assert.That(interiors[0], Is.Null);
            Assert.That(interiors[1], Is.SameAs(interior));
            Assert.That(interiors[2], Is.Null);
        }

        [Test]
        public void EveryBlocksRouteSlice_IsInsideTheWaypointArray()
        {
            RoutinePlan plan = SimpleDay();
            for (int i = 0; i < plan.BlockCount; i++)
            {
                Assert.That(plan.RouteStart[i], Is.GreaterThanOrEqualTo(0));
                Assert.That(plan.RouteCount[i], Is.GreaterThanOrEqualTo(1));
                Assert.That(plan.RouteStart[i] + plan.RouteCount[i],
                            Is.LessThanOrEqualTo(plan.Waypoints.Length));
                Assert.That(plan.RouteLengthMetres[i],
                            Is.EqualTo(RoutineLaneTree.PolylineLength(plan.Waypoints, plan.RouteStart[i],
                                                                       plan.RouteCount[i])).Within(1e-3f));
            }
        }
    }
}
