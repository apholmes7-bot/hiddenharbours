using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    /// <b>Every stop of the days that cross to Nine Mile Creek names a station that stands in the region it names</b>,
    /// read from the committed scenes (the tables villagers actually resolve), and the creek's own two stations are
    /// where the region's geometry says: the bar landing ashore at the end of the crossing's path, the fuel stop in
    /// reach of the forecourt's gas hose.
    /// </summary>
    public class RoutineStopsNameRealStationsTests
    {
        const string Data = "Assets/_Project/Data";
        const string Nmc = "region.nine_mile_creek";
        const string CrossingId = "crossing.st_peters_nine_mile_creek_bar";
        static readonly string[] RoutineIds = { "routine.junior_poirier", "routine.rose_macisaac" };

        static T[] Load<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { Data })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null).ToArray();

        static readonly Dictionary<string, Dictionary<string, Vector2>> Tables = new Dictionary<string, Dictionary<string, Vector2>>();

        /// <summary>Every RoutineStations row in the region's committed scene: id → position.</summary>
        static Dictionary<string, Vector2> StationsIn(string regionId)
        {
            if (Tables.TryGetValue(regionId, out var cached)) return cached;
            RegionDef region = Load<RegionDef>().FirstOrDefault(r => r.Id == regionId);
            Assert.That(region, Is.Not.Null, "no RegionDef " + regionId);
            string path = "Assets/_Project/Scenes/" + region.SceneName + ".unity";
            Assert.That(File.Exists(path), Is.True, path);
            string script = AssetDatabase.AssetPathToGUID("Assets/_Project/Code/World/Routines/RoutineStations.cs");
            Assert.That(script, Is.Not.Empty);
            var scene = StPetersLayerRefresh.SceneYaml.Parse(File.ReadAllText(path).Replace("\r\n", "\n"));
            var rows = new Dictionary<string, Vector2>(System.StringComparer.Ordinal);
            var row = new Regex(@"^  - Id: ([^\n]*)\n(?:    [^\n]*\n)*?    Position: \{x: ([^,]+), y: ([^}]+)\}", RegexOptions.Multiline);
            foreach (var doc in scene.Docs.Where(d => d.ClassId == 114 && d.Text.Contains("guid: " + script)))
                foreach (Match m in row.Matches(doc.Text))
                {
                    string id = m.Groups[1].Value.Trim();
                    Assert.That(rows.ContainsKey(id), Is.False, path + " declares " + id + " twice");
                    rows[id] = new Vector2(float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                                           float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
                }
            Tables[regionId] = rows;
            return rows;
        }

        static RegionCrossingDef Bar() => Load<RegionCrossingDef>().Single(c => c.Id == CrossingId);

        [Test]
        public void EveryStopOfTheseDays_ResolvesToAStationInTheRegionItNames()
        {
            RoutineDef[] all = Load<RoutineDef>();
            var missing = new List<string>();
            foreach (string id in RoutineIds)
            {
                RoutineDef r = all.SingleOrDefault(x => x.Id == id);
                Assert.That(r, Is.Not.Null, "no routine " + id);
                var stops = r.Entries.Select(e => ("ordinary", e))
                    .Concat((r.WeeklyDays ?? System.Array.Empty<RoutineWeeklyDay>())
                        .SelectMany(d => d.Entries.Select(e => (d.Weekday.ToString(), e))));
                foreach (var (day, stop) in stops)
                {
                    string region = r.RegionOf(stop);
                    if (!StationsIn(region).ContainsKey(stop.StationId))
                        missing.Add($"{id} {day} {stop.StartHour:0.00} h: {stop.StationId} in {region}");
                }
            }
            Assert.That(missing, Is.Empty, string.Join("\n", missing));
        }

        [Test]
        public void TheBarCrossing_EndsOnStationsThatExist_AndItsPathEndsAtTheLanding()
        {
            RegionCrossingDef bar = Bar();
            Assert.That(StationsIn(bar.FromRegionId).ContainsKey(bar.FromStationId), Is.True, bar.FromStationId);
            Assert.That(StationsIn(bar.ToRegionId).TryGetValue(bar.ToStationId, out Vector2 landing), Is.True, bar.ToStationId);
            Vector2 end = bar.ToPath[bar.ToPath.Length - 1];
            Assert.That(Vector2.Distance(end, landing), Is.LessThan(0.01f),
                $"the crossing ends at {end} but the landing stands at {landing}: a held walker would wait off his station");
        }

        [Test]
        public void TheCreeksTable_IsTheDerivation()
        {
            var scene = StationsIn(Nmc);
            foreach (RoutineStationEntry s in NmcRoutineStationsPatch.Stations())
            {
                Assert.That(scene.TryGetValue(s.Id, out Vector2 at), Is.True, s.Id + " is not in the committed scene");
                Assert.That(Vector2.Distance(at, s.Position), Is.LessThan(1e-3f),
                    $"{s.Id}: the scene says {at}, the derivation {s.Position}; re-run the patch");
            }
        }

        [Test]
        public void TheBarLanding_IsAshore_AboveSpringHigh_AndOffTheVehicleRoads()
        {
            Vector2 p = NmcRoutineStationsPatch.BarLanding();
            float ground = NineMileCreekGround.Load().ElevationAt(p);
            Assert.That(ground, Is.GreaterThan(NineMileCreekMainland.SpringHighWater + 1f),
                $"the landing at {p} stands at {ground:0.00} m; spring high is {NineMileCreekMainland.SpringHighWater} m");

            foreach (var r in NineMileCreekRoadNetwork.Load().Routes.Where(r => r.Vehicles && r.Class != "footpath"))
            {
                float d = float.PositiveInfinity;
                for (int i = 1; i < r.Points.Length; i++) d = Mathf.Min(d, SegmentDistance(p, r.Points[i - 1], r.Points[i]));
                Assert.That(d - r.HalfWidth, Is.GreaterThan(1f), $"the landing at {p} stands on or by {r}");
            }
        }

        [Test]
        public void TheFuelStop_IsInReachOfTheGasHose_AndOffItsIsland()
        {
            var hose = NmcRoutineStationsPatch.FuelHose();
            Vector2 at = NmcRoutineStationsPatch.FuelStop();
            float d = Vector2.Distance(at, hose.Nozzle);
            Assert.That(d, Is.LessThanOrEqualTo(NmcRoutineStationsPatch.HoseReachMetres), "out of the hose's reach");
            Assert.That(Vector2.Distance(at, hose.Twin), Is.GreaterThan(d), "on the island's far side, not the near one");
            Assert.That(d, Is.GreaterThan(0.6f), "inside the pump island (its wall measured 0.6 m off the nozzle)");
            Assert.That(hose.Id, Does.EndWith("_gas"), "the quad burns gas");
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-9f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
