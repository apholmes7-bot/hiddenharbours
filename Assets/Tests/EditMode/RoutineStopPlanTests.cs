using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A DAY IS A LIST OF STOPS</b> — the seam a varied day is built on. The planner's old single step
    /// is now two (<see cref="RoutinePlanner.ResolveStops"/>, then
    /// <see cref="RoutinePlanner.BuildFromStops"/>), so something that varies a day can change the stops
    /// in between. What is held: the two steps together are exactly the old one; a stop can hang off a
    /// lane junction by its own approach path, walked out and walked back; and two stops on the same
    /// strand are walked between directly, not by way of the village.
    /// </summary>
    public class RoutineStopPlanTests
    {
        static readonly Vector2 Green = new Vector2(0f, 0f);
        static readonly Vector2 Head = new Vector2(-40f, 0f);
        const float Walk = 1.2f, CatchUp = 2f;

        GameObject _host;
        RoutineLanes _lanes;
        RoutineStations _stations;
        RoutineDef _routine;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("RoutineStopPlanTests") { hideFlags = HideFlags.HideAndDontSave };
            _lanes = _host.AddComponent<RoutineLanes>();
            _lanes.Configure(
                nodePositions: new[] { Green, Head },
                nodeParents: new[] { RoutineLaneTree.NoParent, 0 },
                nodeNames: new[] { "green", "head" },
                viaStart: new int[2], viaCount: new int[2], via: System.Array.Empty<Vector2>());
            Assert.That(_lanes.Validate(), Is.Empty);

            _stations = _host.AddComponent<RoutineStations>();
            _stations.Configure(new[]
            {
                new RoutineStationEntry { Id = "s.green", Position = Green, LaneNodeName = "green", Why = "green" },
                new RoutineStationEntry { Id = "s.head", Position = Head, LaneNodeName = "head", Why = "head" },
            });

            _routine = ScriptableObject.CreateInstance<RoutineDef>();
            _routine.Id = "routine.stop_plan_test";
            _routine.WalkSpeedMetresPerSecond = Walk;
            _routine.ScheduleJitterMinutes = 12f;
            _routine.Entries = new[]
            {
                new RoutineEntry { StartHour = 8f, StationId = "s.green", Activity = RoutineActivity.Work },
                new RoutineEntry { StartHour = 17f, StationId = "s.head", Activity = RoutineActivity.Recreation },
                new RoutineEntry { StartHour = 21f, StationId = "s.green", Activity = RoutineActivity.Home },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_routine);
            Object.DestroyImmediate(_host);
        }

        RoutineStop Stop(float hour, Vector2 at, string node, string strand = null, Vector2[] approach = null) =>
            new RoutineStop
            {
                DepartureHour = hour,
                Station = new RoutineStationEntry { Id = "t." + hour, Position = at, LaneNodeName = node, Why = "test" },
                Activity = RoutineActivity.Recreation,
                Interruptible = true,
                Approach = approach,
                Strand = strand,
            };

        RoutinePlan Plan(params RoutineStop[] stops)
        {
            RoutinePlan plan = RoutinePlanner.BuildFromStops("routine.stop_plan_test", stops, _lanes, Walk, CatchUp,
                                                             out _, out string problem);
            Assert.That(plan, Is.Not.Null, problem);
            return plan;
        }

        [Test]
        public void TheTwoSteps_AreExactlyTheOldOne()
        {
            RoutinePlan whole = RoutinePlanner.Build(_routine, _stations, _lanes, 4242, out _, out string problem);
            Assert.That(whole, Is.Not.Null, problem);

            RoutineStop[] stops = RoutinePlanner.ResolveStops(_routine, _stations, _lanes, 4242, out problem);
            Assert.That(stops, Is.Not.Null, problem);
            Assert.That(stops.Length, Is.EqualTo(3));
            RoutinePlan again = RoutinePlanner.BuildFromStops(_routine.Id, stops, _lanes, Walk,
                                                              _routine.CatchUpSpeedMetresPerSecond, out _, out problem);
            Assert.That(again, Is.Not.Null, problem);

            CollectionAssert.AreEqual(whole.DepartureHours, again.DepartureHours);
            CollectionAssert.AreEqual(whole.RouteLengthMetres, again.RouteLengthMetres);
            for (float h = 0f; h < 24f; h += 0.2f)
                Assert.That(again.SampleAt(h, 75f).Position, Is.EqualTo(whole.SampleAt(h, 75f).Position), $"{h} h");
        }

        [Test]
        public void TheSeedMovesTheHours_AndTheSameSeedGivesTheSameHours()
        {
            RoutineStop[] a = RoutinePlanner.ResolveStops(_routine, _stations, _lanes, 1, out _);
            RoutineStop[] b = RoutinePlanner.ResolveStops(_routine, _stations, _lanes, 1, out _);
            for (int i = 0; i < a.Length; i++)
                Assert.That(b[i].DepartureHour, Is.EqualTo(a[i].DepartureHour));
        }

        [Test]
        public void AStopOffTheLanes_IsReachedByItsApproach_OutAndBack()
        {
            var bend = new Vector2(-40f, -6f);
            var spot = new Vector2(-48f, -6f);
            RoutinePlan plan = Plan(Stop(8f, Green, "green"),
                                    Stop(17f, spot, "head", approach: new[] { bend }));

            float expected = 40f + 6f + 8f;   // green -> head, head -> bend, bend -> spot
            Assert.That(plan.RouteLengthMetres[0], Is.EqualTo(expected).Within(0.01f));
            Assert.That(plan.RouteLengthMetres[1], Is.EqualTo(expected).Within(0.01f));
        }

        [Test]
        public void TwoStopsOnOneStrand_AreWalkedBetweenDirectly()
        {
            var a = new Vector2(-48f, -6f);
            var b = new Vector2(-48f, 6f);
            RoutinePlan apart = Plan(Stop(8f, Green, "green"), Stop(17f, a, "head"), Stop(18f, b, "head"));
            RoutinePlan together = Plan(Stop(8f, Green, "green"), Stop(17f, a, "head", "sand"),
                                        Stop(18f, b, "head", "sand"));

            // The leg from a to b is the one that arrives for the 18:00 stop.
            float direct = Vector2.Distance(a, b);
            Assert.That(together.RouteLengthMetres, Has.Some.EqualTo(direct).Within(0.01f),
                        "same strand: straight across the sand");
            Assert.That(apart.RouteLengthMetres, Has.None.EqualTo(direct).Within(0.01f),
                        "no strand: back by the lane junction, as any two stations are");
        }
    }
}
