using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public class RoutineOutOfSightTests
    {
        readonly List<Object> _created = new List<Object>();
        T Keep<T>(T value) where T : Object { _created.Add(value); return value; }
        [TearDown] public void Cleanup()
        {
            foreach (Object value in _created) if (value != null) Object.DestroyImmediate(value);
            _created.Clear();
        }

        RoutinePlan Build(bool hidden, bool both = false)
        {
            var stations = Keep(new GameObject("stations")).AddComponent<RoutineStations>();
            var lanes = Keep(new GameObject("lanes")).AddComponent<RoutineLanes>();
            lanes.Configure(new[] { Vector2.zero, new Vector2(20f, 0f) }, new[] { -1, 0 }, new[] { "a", "b" },
                new int[2], new int[2], Array.Empty<Vector2>());
            stations.Configure(new[] {
                new RoutineStationEntry { Id = "a", Position = Vector2.zero, LaneNodeName = "a", OutOfSight = both },
                new RoutineStationEntry { Id = "b", Position = new Vector2(25f, 0f), LaneNodeName = "b", OutOfSight = hidden }
            });
            var routine = Keep(ScriptableObject.CreateInstance<RoutineDef>());
            routine.Id = "routine.test"; routine.WalkSpeedMetresPerSecond = 1f; routine.ScheduleJitterMinutes = 0f;
            routine.Entries = new[] {
                new RoutineEntry { StartHour = 0f, StationId = "a" },
                new RoutineEntry { StartHour = 8f, StationId = "b" }
            };
            var plan = RoutinePlanner.Build(routine, stations, lanes, 37, out _, out string problem);
            Assert.That(plan, Is.Not.Null, problem);
            return plan;
        }

        [Test] public void RoutineOutOfSight_OccupantNeitherDrawnNorTalkable()
        {
            RoutinePose pose = Build(true).SampleAt(12f, 75f);
            Assert.That(pose.Shelter, Is.EqualTo(RoutineShelter.OutOfSight));
            Assert.That(RoutinePlan.IsVisible(pose.Shelter, false), Is.False);
            Assert.That(RoutinePlan.IsVisible(pose.Shelter, true), Is.False);
            Assert.That(RoutinePlan.IsVisible(RoutineShelter.InsideDestination, false), Is.False);
            Assert.That(RoutinePlan.IsVisible(RoutineShelter.InsideDestination, true), Is.True);
        }

        [Test] public void RoutineOutOfSight_WalkDrawnToAndFromItsNode()
        {
            RoutinePlan plan = Build(true);
            Assert.That(plan.SampleAt(8f + 19f / 75f, 75f).Shelter, Is.EqualTo(RoutineShelter.Outdoors));
            Assert.That(plan.SampleAt(8f + 21f / 75f, 75f).Shelter, Is.EqualTo(RoutineShelter.OutOfSight));
            Assert.That(plan.SampleAt(4f / 75f, 75f).Shelter, Is.EqualTo(RoutineShelter.OutOfSight));
            Assert.That(plan.SampleAt(6f / 75f, 75f).Shelter, Is.EqualTo(RoutineShelter.Outdoors));
            Assert.That(plan.EnterAtMetres[1], Is.EqualTo(20f));
            Assert.That(plan.ExitAtMetres[0], Is.EqualTo(5f));
        }

        [Test] public void RoutineOutOfSight_BlockBetweenTwo_HasNoWalk()
        {
            RoutinePlan plan = Build(true, true);
            Assert.That(plan.RouteLengthMetres, Is.All.EqualTo(0f));
            Assert.That(plan.RouteCount, Is.All.EqualTo(1));
            for (int minute = 0; minute < 1440; minute++)
            {
                var pose = plan.SampleAt(minute / 60f, 75f);
                Assert.That(pose.Walking, Is.False);
                Assert.That(pose.Shelter, Is.EqualTo(RoutineShelter.OutOfSight));
                Assert.That(pose.Position, Is.EqualTo(minute < 480 ? Vector2.zero : new Vector2(25f, 0f)));
            }
        }

        [Test] public void RoutineOutOfSight_OffByDefault_MatchesUnmarkedPlan()
        {
            Assert.That(new RoutineStationEntry().OutOfSight, Is.False);
            // An all-false mark must be identical to the original constructor's omitted argument.
            RoutinePlan plan = Build(false);
            var old = new RoutinePlan(plan.DepartureHours, plan.Waypoints, plan.RouteStart, plan.RouteCount,
                plan.RouteLengthMetres, plan.Activities, plan.StandHeadingDegrees, plan.ExitAtMetres,
                plan.EnterAtMetres, plan.WalkSpeedMetresPerSecond, plan.Interruptible, plan.CatchUpSpeedMetresPerSecond);
            for (int minute = 0; minute < 1440; minute++)
                Assert.That(plan.SampleAt(minute / 60f, 75f), Is.EqualTo(old.SampleAt(minute / 60f, 75f)));
        }
    }
}
