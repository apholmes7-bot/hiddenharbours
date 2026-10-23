using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public class CumberlandCourseTests
    {
        readonly List<Object> _created = new List<Object>();
        CumberlandCourse Course() => CumberlandCourse.Read();
        T Keep<T>(T obj) where T : Object { _created.Add(obj); return obj; }

        [TearDown] public void Cleanup()
        {
            foreach (Object obj in _created) if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        RoutinePlan Plan(CumberlandCourse course, HeronViewRoster.Day day, int seed = 27)
        {
            var stations = Keep(new GameObject("stations")).AddComponent<RoutineStations>();
            var lanes = Keep(new GameObject("lanes")).AddComponent<RoutineLanes>();
            CumberlandRoutines.Configure(course, stations, lanes);
            NpcDef npc = Keep(ScriptableObject.CreateInstance<NpcDef>());
            RoutineDef def = Keep(HeronViewRoutinesBuilder.Apply(day, npc, null, false));
            var plan = RoutinePlanner.Build(def, stations, lanes, seed, out _, out string problem);
            Assert.That(plan, Is.Not.Null, problem);
            return plan;
        }

        [Test] public void CumberlandCourse_StationsDeriveFromThePlansHoles()
        {
            CumberlandCourse c = Course();
            Assert.That(c.Stations.Length, Is.EqualTo(43));
            foreach (var h in c.Holes)
                foreach (bool tee in new[] { true, false })
                {
                    if (tee ? !h.Tee.HasValue : !h.Green.HasValue) continue;
                    for (int s = 0; s < 4; s++)
                    {
                        Vector2 at = c.Station(CumberlandCourse.SlotId(tee, h.Number, s)).Position;
                        Vector2 direction = tee ? CumberlandCourse.Direction(h) : CumberlandCourse.LastDirection(h);
                        if (tee)
                        {
                            Vector2 delta = at - h.Tee.Value;
                            Assert.That(Vector2.Dot(delta, direction),
                                Is.EqualTo(-CumberlandCourse.TeeBackMetres - (s - 1.5f) * CumberlandCourse.SlotGapMetres).Within(0.001));
                            Assert.That(Vector2.Dot(delta, new Vector2(-direction.y, direction.x)),
                                Is.EqualTo(0f).Within(0.001));
                        }
                        else
                        {
                            Vector2 radial = at - h.Green.Value;
                            Assert.That(radial.magnitude, Is.EqualTo(CumberlandCourse.GreenStandRadiusMetres).Within(0.001));
                            Assert.That(Vector2.SignedAngle(-direction, radial),
                                Is.EqualTo((s - 1.5f) * CumberlandCourse.GreenSlotAngleDegrees).Within(0.001));
                        }
                        Vector2 facing = tee ? direction : h.Green.Value - at;
                        Assert.That(c.Station(CumberlandCourse.SlotId(tee, h.Number, s)).StandHeadingDegrees,
                            Is.EqualTo(CumberlandCourse.Heading(facing)).Within(0.001));
                    }
                }
            Assert.That(c.Station(CumberlandCourse.Clubhouse).Position,
                Is.EqualTo(c.LotCentre + Vector2.down * c.LotSize.y / 2));
            Assert.That(c.Stations.Where(s => s.OutOfSight).Select(s => s.Id),
                Is.EquivalentTo(new[] { CumberlandCourse.Clubhouse, CumberlandCourse.EdgeIn, CumberlandCourse.EdgeOut }));
        }

        [Test] public void CumberlandCourse_FourStationsATee_NeverStack()
        {
            CumberlandCourse c = Course();
            foreach (var h in c.Holes)
                foreach (bool tee in new[] { true, false })
                {
                    if (tee ? !h.Tee.HasValue : !h.Green.HasValue) continue;
                    Vector2 centre = tee ? h.Tee.Value : h.Green.Value;
                    Vector2[] at = Enumerable.Range(0, 4).Select(s => c.Station(CumberlandCourse.SlotId(tee, h.Number, s)).Position).ToArray();
                    foreach (Vector2 p in at)
                        Assert.That(Vector2.Distance(p, centre), Is.LessThan(tee ? c.TeeRadius : c.GreenRadius));
                    for (int a = 0; a < 4; a++) for (int b = a + 1; b < 4; b++)
                        Assert.That(Vector2.Distance(at[a], at[b]), Is.GreaterThanOrEqualTo(CumberlandCourse.SlotGapMetres - 0.001f));
                }
        }

        [Test] public void CumberlandCourse_LanesAreOneTree_RootedAtTheClubhouse()
        {
            CumberlandCourse c = Course();
            Assert.That(RoutineLaneTree.IsTree(c.Parents, out _), Is.True);
            Assert.That(c.Parents.Count(p => p == -1), Is.EqualTo(1));
            Assert.That(c.Nodes[Array.IndexOf(c.Parents, -1)], Is.EqualTo(c.Station(CumberlandCourse.Clubhouse).Position));
            Assert.That(c.NodeNames.Distinct().Count(), Is.EqualTo(c.Nodes.Length));
            Assert.That(c.NodeNames.All(n => !string.IsNullOrEmpty(n)), Is.True);
            foreach (var station in c.Stations) Assert.That(c.NodeNames, Does.Contain(station.LaneNodeName));
        }

        [Test] public void CumberlandCourse_EdgeStationsSitOnTheEdge()
        {
            CumberlandCourse c = Course();
            foreach (var item in new[] { (5, CumberlandCourse.EdgeOut), (18, CumberlandCourse.EdgeIn) })
            {
                Vector2 p = c.Station(item.Item2).Position;
                Assert.That(p.x, Is.EqualTo(c.EastEdge).Within(0.001));
                Vector2[] line = c.GetHole(item.Item1).Line;
                Assert.That(Enumerable.Range(1, line.Length - 1).Min(i => PointSegment(p, line[i - 1], line[i])), Is.LessThan(0.001));
            }
        }

        [Test] public void CumberlandCourse_NoLaneThroughWaterOrSand()
        {
            CumberlandCourse c = Course();
            var segments = new List<(Vector2 a, Vector2 b)>();
            for (int i = 0; i < c.Nodes.Length; i++)
            {
                if (c.Parents[i] < 0) continue;
                Vector2 a = c.Nodes[i];
                for (int k = 0; k < c.ViaCount[i]; k++)
                { Vector2 b = c.Via[c.ViaStart[i] + k]; segments.Add((a, b)); a = b; }
                segments.Add((a, c.Nodes[c.Parents[i]]));
            }
            foreach (var station in c.Stations)
                segments.Add((station.Position, c.Nodes[Array.IndexOf(c.NodeNames, station.LaneNodeName)]));
            int crossings = 0;
            foreach (var segment in segments)
            {
                foreach (var bunker in c.Bunkers)
                    Assert.That(PointSegment(new Vector2(bunker.x, bunker.y), segment.a, segment.b),
                        Is.GreaterThan(bunker.z), $"Sand at {bunker}; lane {segment.a} to {segment.b}");
                foreach (var pond in c.Ponds)
                {
                    Vector2 Scale(Vector2 p)
                    {
                        p -= pond.Centre;
                        float angle = -pond.Rotation * Mathf.Deg2Rad;
                        return new Vector2((p.x * Mathf.Cos(angle) - p.y * Mathf.Sin(angle)) / pond.Radii.x,
                            (p.x * Mathf.Sin(angle) + p.y * Mathf.Cos(angle)) / pond.Radii.y);
                    }
                    Assert.That(PointSegment(Vector2.zero, Scale(segment.a), Scale(segment.b)), Is.GreaterThan(1f),
                        $"{pond.Id}; lane {segment.a} to {segment.b}");
                }
                for (int j = 1; j < c.Brook.Length; j++)
                {
                    float bankGap = Mathf.Min(Mathf.Min(PointSegment(segment.a, c.Brook[j - 1], c.Brook[j]),
                        PointSegment(segment.b, c.Brook[j - 1], c.Brook[j])),
                        Mathf.Min(PointSegment(c.Brook[j - 1], segment.a, segment.b), PointSegment(c.Brook[j], segment.a, segment.b)));
                    if (PointSegment(c.Bridge, segment.a, segment.b) > 0.001f)
                        Assert.That(bankGap, Is.GreaterThan(Mathf.Max(c.BrookWidths[j - 1], c.BrookWidths[j]) / 2f));
                    if (!Intersection(segment.a, segment.b, c.Brook[j - 1], c.Brook[j], out Vector2 at)) continue;
                    crossings++;
                    Assert.That(Vector2.Distance(at, c.Bridge), Is.LessThan(0.001), "Brook crossing must use the bridge.");
                }
            }
            Assert.That(crossings, Is.EqualTo(1));
        }

        [Test] public void HeronViewRound_EveryWalkArrivesBeforeItsNextDeparture()
        {
            CumberlandCourse c = Course();
            foreach (var day in HeronViewRoster.Build(c, HeronViewRoster.SecondsPerGameHour))
            {
                RoutinePlan plan = Plan(c, day);
                for (int b = 0; b < plan.BlockCount; b++)
                    Assert.That(RoutineSchedule.GapHours(plan.DepartureHours, b) - plan.TravelHours(b, HeronViewRoster.SecondsPerGameHour),
                        Is.GreaterThanOrEqualTo(day.DwellFloorMinutes[b] / 60f - 0.0001), day.Person.Name + " block " + b);
            }
        }

        [Test] public void HeronViewRound_FrontPartyOutBeforeSunset()
        {
            var profile = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Art.DayNightProfile>("Assets/_Project/Resources/DayNightProfile.asset");
            Assert.That(profile, Is.Not.Null);
            CumberlandCourse c = Course();
            foreach (var day in HeronViewRoster.Build(c, HeronViewRoster.SecondsPerGameHour).Where(d => d.Person.Party == 0))
            {
                RoutinePlan plan = Plan(c, day);
                int b = Array.FindIndex(day.Blocks, e => e.StationId == CumberlandCourse.EdgeOut);
                Assert.That(plan.DepartureHours[b] + plan.TravelHours(b, HeronViewRoster.SecondsPerGameHour), Is.LessThan(profile.SunsetHour));
            }
        }

        [Test] public void HeronViewParty_WalksInFile()
        {
            CumberlandCourse c = Course();
            var days = HeronViewRoster.Build(c, HeronViewRoster.SecondsPerGameHour);
            foreach (var party in days.GroupBy(d => d.Person.Party))
            {
                var members = party.ToArray();
                var plans = members.Select(d => Plan(c, d)).ToArray();
                Assert.That(members.All(d => d.JitterMinutes == 0 && d.Speed == HeronViewRoster.WalkSpeed), Is.True);
                for (int b = 1; b < members[0].Blocks.Length; b++)
                    if (party.Key == 0 || b == 2)
                        for (int i = 1; i < members.Length; i++)
                            Assert.That(members[i].Blocks[b].StartHour - members[i - 1].Blocks[b].StartHour,
                                Is.EqualTo(HeronViewRoster.FileGapMinutes / 60f).Within(0.0001));
                for (int sample = 0; sample < 8640; sample++)
                    for (int a = 0; a < plans.Length; a++) for (int b = a + 1; b < plans.Length; b++)
                    {
                        var pa = plans[a].SampleAt(sample / 360f, HeronViewRoster.SecondsPerGameHour);
                        var pb = plans[b].SampleAt(sample / 360f, HeronViewRoster.SecondsPerGameHour);
                        if (pa.Shelter != RoutineShelter.Outdoors || pb.Shelter != RoutineShelter.Outdoors) continue;
                        Assert.That(Vector2.Distance(pa.Position, pb.Position),
                            Is.GreaterThanOrEqualTo(CumberlandCourse.SlotGapMetres - 0.001f),
                            $"Party {party.Key}; minute {sample / 6f}; members {a}/{b}; blocks {pa.BlockIndex}/{pb.BlockIndex}; at {pa.Position}/{pb.Position}");
                    }
            }
        }

        [Test] public void HeronViewRound_FirstTeeHasAnHourWithAllFourOnTheirSlots()
        {
            CumberlandCourse c = Course();
            var plans = HeronViewRoster.Build(c, HeronViewRoster.SecondsPerGameHour).Take(4).Select(d => Plan(c, d)).ToArray();
            bool found = false;
            for (int minute = 0; minute < 1440; minute++)
                if (plans.Select((p, s) => p.SampleAt(minute / 60f, HeronViewRoster.SecondsPerGameHour)).Select((p, s) =>
                    !p.Walking && p.Shelter == RoutineShelter.Outdoors &&
                    Vector2.Distance(p.Position, c.Station(CumberlandCourse.SlotId(true, 1, s)).Position) < 0.001f).All(v => v))
                    found = true;
            Assert.That(found, Is.True);
        }

        [Test] public void HeronViewRound_IsAPureFunctionOfTheClock()
        {
            CumberlandCourse c = Course();
            foreach (var day in HeronViewRoster.Build(c, HeronViewRoster.SecondsPerGameHour))
            {
                RoutinePlan a = Plan(c, day, 29), b = Plan(c, day, 29);
                for (int minute = 1439; minute >= 0; minute--)
                    Assert.That(b.SampleAt(minute / 60f, HeronViewRoster.SecondsPerGameHour),
                        Is.EqualTo(a.SampleAt(minute / 60f, HeronViewRoster.SecondsPerGameHour)));
            }
        }

        [Test] public void HeronViewRoutines_BuilderCreatesMissing_NeverOverwrites()
        {
            var day = HeronViewRoster.Build(Course(), HeronViewRoster.SecondsPerGameHour)[0];
            NpcDef npc = Keep(ScriptableObject.CreateInstance<NpcDef>());
            RoutineDef created = Keep(HeronViewRoutinesBuilder.Apply(day, npc, null, false));
            Assert.That(created.Id, Is.EqualTo(day.Person.RoutineId));
            Assert.That(created.IsWalkable, Is.True);
            created.Entries = new[] { new RoutineEntry { StartHour = 3f, StationId = "edited" } };
            created.WalkSpeedMetresPerSecond = 0.75f; created.Npc = null;
            var kept = HeronViewRoutinesBuilder.Apply(day, npc, created, false);
            Assert.That(kept, Is.SameAs(created)); Assert.That(kept.Entries[0].StationId, Is.EqualTo("edited"));
            Assert.That(kept.Npc, Is.Null); Assert.That(kept.WalkSpeedMetresPerSecond, Is.EqualTo(0.75f));
            Assert.That(HeronViewRoutinesBuilder.Apply(day, npc, kept, true).Entries.Length, Is.EqualTo(day.Blocks.Length));
        }

        [Test] public void HeronViewProposals_IdsAndDaysAreComplete()
        {
            var people = HeronViewRoster.Visitors;
            Assert.That(people.Length, Is.EqualTo(8));
            Assert.That(people.GroupBy(p => p.Party).Select(g => g.Count()).ToArray(), Is.EqualTo(new[] { 4, 2, 2 }));
            var ids = people.SelectMany(p => new[] { p.NpcId, p.RoutineId, p.DialogueId, p.BuildId }).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(32));
            foreach (string id in ids)
                Assert.That(System.Text.RegularExpressions.Regex.IsMatch(id, @"^(npc|routine|dialogue|char_build)\.[a-z]+(_[a-z]+)*$"), Is.True, id);
            foreach (var person in people)
            {
                Assert.That(person.Name, Is.Not.Empty); Assert.That(person.Look, Is.Not.Empty); Assert.That(person.Line, Is.Not.Empty);
            }
            CumberlandCourse c = Course();
            foreach (var day in HeronViewRoster.Build(c, HeronViewRoster.SecondsPerGameHour))
            {
                Assert.That(day.Blocks[0].StationId, Is.EqualTo(CumberlandCourse.Clubhouse));
                Assert.That(day.Blocks[day.Blocks.Length - 1].StationId, Is.EqualTo(CumberlandCourse.Clubhouse));
                Assert.That(day.Blocks.All(b => b.Activity == RoutineActivity.Recreation && c.Stations.Any(s => s.Id == b.StationId)), Is.True);
                Assert.That(day.Blocks.Select(b => b.StartHour), Is.Ordered.And.All.LessThan(24f));
            }
        }

        static float PointSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float t = d.sqrMagnitude == 0f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude);
            return Vector2.Distance(p, a + d * t);
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool Intersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 point)
        {
            Vector2 u = b - a, v = d - c; float denom = Cross(u, v); point = default;
            if (Mathf.Abs(denom) < 1e-6f) return false;
            float t = Cross(c - a, v) / denom, s = Cross(c - a, u) / denom;
            point = a + u * t;
            return t >= 0f && t <= 1f && s >= 0f && s <= 1f;
        }
    }
}
