using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>CONTENT VALIDATION for days that leave their region</b> (<see cref="RoutineWeeklyDay"/>,
    /// <see cref="RoutineEntry.RegionId"/>) and for the crossings they walk (<see cref="RegionCrossingDef"/>).
    /// Every failure here is an authoring mistake that would otherwise show up as a villager who teleports,
    /// stands still, or is somewhere nobody can see why.
    /// </summary>
    public class RoutineCrossRegionContentTests
    {
        const string Data = "Assets/_Project/Data";
        const string StPeters = "region.st_peters";
        static readonly Regex CrossingId = new Regex(@"^crossing\.[a-z0-9]+(_[a-z0-9]+)*$");

        static T[] Load<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { Data })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null).ToArray();

        static Dictionary<string, RegionDef> Regions() =>
            Load<RegionDef>().GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First());

        static IEnumerable<(RoutineDef routine, RoutineWeeklyDay day)> WeeklyDays() =>
            Load<RoutineDef>().Where(r => r.WeeklyDays != null)
                .SelectMany(r => r.WeeklyDays.Select(d => (r, d)));

        [Test]
        public void Crossings_HaveStableIds_WalkablePaths_AndRealRegions()
        {
            RegionCrossingDef[] all = Load<RegionCrossingDef>();
            Assert.That(all.Length, Is.GreaterThan(0), "no crossing assets under Data");
            var regions = Regions();
            var ids = new HashSet<string>();

            foreach (RegionCrossingDef c in all)
            {
                Assert.That(CrossingId.IsMatch(c.Id ?? ""), Is.True, $"'{c.name}': id '{c.Id}' is not crossing.snake_case");
                Assert.That(ids.Add(c.Id), Is.True, $"duplicate crossing id '{c.Id}'");
                Assert.That(c.IsWalkable, Is.True, $"'{c.Id}': each path needs two points or more");
                Assert.That(c.FromRegionId, Is.Not.EqualTo(c.ToRegionId), $"'{c.Id}' crosses into its own region");
                Assert.That(regions.ContainsKey(c.FromRegionId), Is.True, $"'{c.Id}': no region '{c.FromRegionId}'");
                Assert.That(regions.ContainsKey(c.ToRegionId), Is.True, $"'{c.Id}': no region '{c.ToRegionId}'");
                Assert.That(string.IsNullOrEmpty(c.FromStationId) || string.IsNullOrEmpty(c.ToStationId), Is.False,
                            $"'{c.Id}' must name both end stations");

                AssertInside(c.FromPath, regions[c.FromRegionId], c.Id + " FromPath");
                AssertInside(c.ToPath, regions[c.ToRegionId], c.Id + " ToPath");

                if (c.FromRegionId == StPeters)
                    Assert.That(StPetersRoutines.AllStationIds, Contains.Item(c.FromStationId),
                                $"'{c.Id}' starts at '{c.FromStationId}', which St Peters does not declare");
                if (c.ToRegionId == StPeters)
                    Assert.That(StPetersRoutines.AllStationIds, Contains.Item(c.ToStationId),
                                $"'{c.Id}' ends at '{c.ToStationId}', which St Peters does not declare");
            }
        }

        static void AssertInside(Vector2[] path, RegionDef region, string what)
        {
            Vector2 half = region.WorldSizeMeters * 0.5f;
            foreach (Vector2 p in path)
            {
                Vector2 d = p - region.WorldCenter;
                Assert.That(Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.y) <= half.y, Is.True,
                            $"{what}: {p} lies outside {region.Id}'s {region.WorldSizeMeters} rectangle");
            }
        }

        [Test]
        public void WeeklyDays_AreWellFormed_StartAndEndAtHome_AndCrossOnlyByTheirCrossings()
        {
            var regions = Regions();
            int seen = 0;
            foreach (RoutineDef r in Load<RoutineDef>())
            {
                if (r.WeeklyDays == null) continue;
                var weekdays = new HashSet<Weekday>();
                foreach (RoutineWeeklyDay day in r.WeeklyDays)
                {
                    seen++;
                    string who = $"'{r.Id}' {day.Weekday}";
                    Assert.That(weekdays.Add(day.Weekday), Is.True, $"{who}: two weekly days on one weekday");
                    RoutineEntry[] e = day.Entries;
                    Assert.That(e != null && e.Length >= 2, Is.True, $"{who}: a day needs two blocks or more");

                    for (int i = 0; i < e.Length; i++)
                    {
                        Assert.That(e[i].StartHour, Is.GreaterThanOrEqualTo(0f).And.LessThan(24f), $"{who} block {i}");
                        if (i > 0) Assert.That(e[i].StartHour, Is.GreaterThan(e[i - 1].StartHour), $"{who} block {i} is out of order");
                        string region = r.RegionOf(e[i]);
                        Assert.That(regions.ContainsKey(region), Is.True, $"{who} block {i}: no region '{region}'");
                        if (region == StPeters)
                            Assert.That(StPetersRoutines.AllStationIds, Contains.Item(e[i].StationId),
                                        $"{who} block {i}: St Peters does not declare '{e[i].StationId}'");
                    }

                    Assert.That(r.RegionOf(e[0]), Is.EqualTo(r.RegionId), $"{who}: must start in {r.RegionId}");
                    Assert.That(r.RegionOf(e[e.Length - 1]), Is.EqualTo(r.RegionId), $"{who}: must end in {r.RegionId}");

                    for (int i = 1; i < e.Length; i++)
                    {
                        string from = r.RegionOf(e[i - 1]), to = r.RegionOf(e[i]);
                        if (from == to) continue;
                        RegionCrossingDef c = RoutineLegs.FindCrossing(r, from, to, out bool reversed);
                        Assert.That(c, Is.Not.Null, $"{who} block {i}: no crossing from {from} to {to} in Crossings");
                        Assert.That(e[i - 1].StationId, Is.EqualTo(c.NearStationId(reversed)),
                                    $"{who} block {i - 1}: a crossing over '{c.Id}' leaves from its near station");
                        Assert.That(e[i].StationId, Is.EqualTo(c.FarStationId(reversed)),
                                    $"{who} block {i}: a crossing over '{c.Id}' arrives at its far station");
                        Assert.That(e[i].Uninterruptible, Is.True,
                                    $"{who} block {i}: a crossing must not stop mid-bar to talk");
                    }
                }
            }
            Assert.That(seen, Is.GreaterThan(0), "no weekly days authored â€” the cross-region example is missing");
        }

        /// <summary>
        /// Under an always-open gate every crossing must ARRIVE before the next block sets out, for any
        /// world and any week. Then nothing is pushed, the region plan (which does not know about crossings)
        /// and the resolver agree on every in-region departure, and a villager never jumps on arrival.
        /// </summary>
        [Test]
        public void OpenBarCrossings_ArriveBeforeTheNextBlockLeaves_ForAnySeedAndWeek()
        {
            const int daysPerWeek = 7;
            float secondsPerGameHour = GameConfig.DefaultSecondsPerDay / RoutineSchedule.HoursPerDay;
            foreach (var (r, day) in WeeklyDays())
            {
                for (int seed = 0; seed < 40; seed++)
                for (int week = 0; week < 6; week++)
                {
                    int d = (int)day.Weekday + daysPerWeek * week;
                    var ctx = new RoutineLegContext
                    {
                        WorldSeed = seed * 7919, SecondsPerGameHour = secondsPerGameHour, DaysPerWeek = daysPerWeek,
                    };
                    RoutineEntry[] entries = RoutineLegs.EntriesFor(r, d, daysPerWeek, out int weekly);
                    var deps = new float[entries.Length];
                    RoutineLegs.DepartureHours(r, entries, weekly, d, ctx.WorldSeed, deps);

                    var window = new List<RoutineSegment>();
                    RoutineLegs.BuildWindow(r, d, ctx, window);
                    foreach (RoutineSegment s in window.Where(x => x.Day == d && x.Kind == RoutineLegKind.Crossing))
                    {
                        int next = s.Block + 1;
                        double nextLeaves = next < entries.Length
                            ? d * 24d + deps[next]
                            : (d + 1) * 24d;   // the next day's first block cannot come sooner than midnight here
                        Assert.That(s.End, Is.LessThanOrEqualTo(nextLeaves),
                                    $"'{r.Id}' {day.Weekday} seed {seed} week {week}: crossing block {s.Block} " +
                                    $"arrives at {s.End - d * 24d:0.00} h, after block {next} leaves at " +
                                    $"{nextLeaves - d * 24d:0.00} h. Widen the gap.");
                    }
                }
            }
        }
    }
}
