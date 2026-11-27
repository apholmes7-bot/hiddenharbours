using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;

namespace HiddenHarbours.Tests.EditMode
{
    public class RoutineCatalogTests
    {
        static RoutineDef[] All() => AssetDatabase.FindAssets("t:RoutineDef", new[] { RoutineCatalog.Folder })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<RoutineDef>).ToArray();

        static RegionDef[] Regions() => AssetDatabase.FindAssets("t:RegionDef", new[] { "Assets/_Project/Data/Regions" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<RegionDef>).ToArray();

        [Test] public void RoutineCatalog_EveryRoutineNamesARegionThatExists()
        {
            RoutineDef[] routines = All();
            Assert.That(routines, Is.Not.Empty);
            string[] regions = Regions().Select(r => r.Id).ToArray();
            foreach (RoutineDef routine in routines)
            {
                Assert.That(routine, Is.Not.Null);
                Assert.That(routine.RegionId, Is.Not.Null.And.Not.Empty, routine.Id);
                Assert.That(regions, Does.Contain(routine.RegionId), routine.Id);
            }
        }

        [Test] public void RoutineCatalog_IdsAreUniqueAcrossEveryRegion()
        {
            string[] ids = All().Select(r => r.Id).ToArray();
            Assert.That(ids, Is.Not.Empty);
            Assert.That(ids.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(ids.Length));
        }

        [Test] public void RoutineCatalog_EachRegionReadsOnlyItsOwn()
        {
            foreach (RegionDef region in Regions())
            {
                RoutineDef[] scoped = RoutineCatalog.LoadRegion(region.Id);
                Assert.That(scoped.Select(r => r.Id), Is.EquivalentTo(All().Where(r => r.RegionId == region.Id).Select(r => r.Id)), region.Id);
                Assert.That(scoped.All(r => r.RegionId == region.Id), Is.True, region.Id);
                Assert.That(scoped.Select(r => r.Id).SequenceEqual(scoped.Select(r => r.Id).OrderBy(id => id, StringComparer.Ordinal)), Is.True);
            }
            Assert.That(StPetersRoutines.LoadRoutines().Select(r => r.Id),
                Is.EquivalentTo(RoutineDefsBuilder.Roster.Select(d => d.RoutineId)));
            var cumberland = AssetDatabase.LoadAssetAtPath<RegionDef>("Assets/_Project/Data/Regions/Cumberland.asset");
            Assert.That(cumberland, Is.Not.Null);
            string[] golfIds = HeronViewRoster.Visitors.Select(p => p.RoutineId).ToArray();
            foreach (RoutineDef routine in RoutineCatalog.LoadRegion(cumberland.Id))
                Assert.That(golfIds, Does.Contain(routine.Id));
            Assert.That(RoutineCatalog.LoadRegion(""), Is.Empty);
        }

        [Test] public void RoutineCatalog_EveryDayIsWellFormed()
        {
            RoutineDef[] routines = All();
            Assert.That(routines, Is.Not.Empty);
            foreach (RoutineDef routine in routines)
            {
                Assert.That(Regex.IsMatch(routine.Id ?? "", @"^routine\.[a-z0-9]+(_[a-z0-9]+)*$"), Is.True, routine.Id);
                Assert.That(routine.IsWalkable, Is.True, routine.Id);
                float[] hours = routine.Entries.Select(e => e.StartHour).ToArray();
                for (int i = 0; i < hours.Length; i++)
                {
                    Assert.That(hours[i], Is.GreaterThanOrEqualTo(0f).And.LessThan(24f), routine.Id);
                    if (i > 0) Assert.That(hours[i], Is.GreaterThan(hours[i - 1]), routine.Id);
                }
                float total = Enumerable.Range(0, hours.Length).Sum(i => RoutineSchedule.GapHours(hours, i));
                Assert.That(total, Is.EqualTo(RoutineSchedule.HoursPerDay).Within(0.001f), routine.Id);
                Assert.That(routine.ScheduleJitterMinutes, Is.GreaterThanOrEqualTo(0f)
                    .And.LessThan(RoutineSchedule.SmallestGapHours(hours) * 30f), routine.Id);
            }
        }

        // Authoring declarations only: scene placement is audited separately. The mainland currently
        // declares two local keeper tables, not a region-wide network. Do not invent destinations.
        static Dictionary<string, HashSet<string>> DeclaredStations()
        {
            var result = Regions().ToDictionary(r => r.Id,
                _ => new HashSet<string>(StringComparer.Ordinal));
            result["region.st_peters"].UnionWith(StPetersRoutines.AllStationIds);
            result[HeronViewRoster.RegionId].UnionWith(CumberlandCourse.Read().Stations.Select(s => s.Id));
            foreach (var keeper in NmcWholesalerKeepers.All)
            {
                result["region.nine_mile_creek"].Add(keeper.CounterStationId);
                result["region.nine_mile_creek"].Add(keeper.HomeStationId);
            }
            return result;
        }

        static IEnumerable<(string name, RoutineEntry[] entries)> Days(RoutineDef routine)
        {
            yield return ("default", routine.Entries);
            if (routine.WeeklyDays == null) yield break;
            foreach (var day in routine.WeeklyDays)
                yield return (day.Weekday.ToString(), day.Entries);
        }

        [Test] public void RoutineCatalog_AllDaysNameDeclaredStations_ExceptKnownGaps()
        {
            var declared = DeclaredStations();
            var gaps = new List<string>();
            var routines = All();
            Assert.That(routines, Is.Not.Empty);
            foreach (var routine in routines)
            foreach (var day in Days(routine))
            {
                Assert.That(day.entries, Is.Not.Null.And.Not.Empty, routine.Id + "/" + day.name);
                for (int i = 0; i < day.entries.Length; i++)
                {
                    var entry = day.entries[i];
                    string region = routine.RegionOf(entry);
                    if (!declared.TryGetValue(region, out var stations) || !stations.Contains(entry.StationId))
                        gaps.Add($"{routine.Id}/{day.name}/{i}/{region}/{entry.StationId}");
                }
            }

            // Exact debt, not an ignored region or a permitted count. Repairs must remove their row;
            // a different missing station (including on a weekly override) must fail.
            Assert.That(gaps, Is.EquivalentTo(new[] {
                "routine.junior_poirier/Wednesday/2/region.nine_mile_creek/station.nine_mile_creek.bar_landing",
                "routine.junior_poirier/Wednesday/3/region.nine_mile_creek/station.nine_mile_creek.fuel_stop",
                "routine.junior_poirier/Wednesday/4/region.nine_mile_creek/station.nine_mile_creek.bar_landing",
            }), "New or repaired station gap: reconcile the exact baseline with npc-roster-stations-audit.md.");
        }

        [Test] public void RoutineCatalog_PeopleHaveAHomeActivityOnEveryDay_ExceptKnownGaps()
        {
            var npcs = AssetDatabase.FindAssets("t:NpcDef", new[] { "Assets/_Project/Data" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<NpcDef>)
                .Where(n => n.Kind == InteractKind.Talk).ToArray();
            Assert.That(npcs, Is.Not.Empty);
            var routines = All();
            var gaps = new List<string>();
            foreach (var npc in npcs)
            {
                var days = routines.Where(r => r.Npc == npc).ToArray();
                if (days.Length == 0) gaps.Add(npc.Id + "/no routine");
                foreach (var routine in days)
                foreach (var day in Days(routine))
                    if (day.entries == null || !day.entries.Any(e => e.Activity == RoutineActivity.Home
                        && !string.IsNullOrWhiteSpace(e.StationId) && routine.RegionOf(e) == routine.RegionId))
                        gaps.Add(npc.Id + "/" + routine.Id + "/" + day.name);
            }

            // A planned household or a hidden golf exit is not a Home activity in a playable day.
            // Readable objects are excluded by interaction kind; all living contacts remain in scope.
            Assert.That(gaps, Is.EquivalentTo(new[] {
                "npc.angus_gillis/no routine",
                "npc.claudette_boudreau/no routine",
                "npc.dougie_macneil/no routine",
                "npc.hector_bernard/no routine",
                "npc.lucie_landry/no routine",
                "npc.monique_landry/no routine",
                "npc.nmc_harbourmaster/no routine",
                "npc.nmc_yard/no routine",
                "npc.paul_landry/no routine",
                "npc.rene_chiasson/no routine",
                "npc.sam_landry/no routine",
                "npc.theresa_gillis/no routine",
                "npc.wendell_arsenault/no routine",
                // Visitors have full clock coverage, but only Recreation tags and no authored home.
                "npc.elsi_brindle/routine.elsi_brindle/default",
                "npc.ione_merrow/routine.ione_merrow/default",
                "npc.mara_fenwick/routine.mara_fenwick/default",
                "npc.nella_tarn/routine.nella_tarn/default",
                "npc.orrin_vale/routine.orrin_vale/default",
                "npc.pell_wickett/routine.pell_wickett/default",
                "npc.tobin_rell/routine.tobin_rell/default",
                "npc.vey_corren/routine.vey_corren/default",
            }), "New or repaired home gap: reconcile the exact baseline with npc-roster-stations-audit.md.");
        }
    }
}
