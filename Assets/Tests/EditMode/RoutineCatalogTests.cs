using System;
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
    }
}
