using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Crew slice R2: the shipped roles and voyage records. Every hull has one record; minimums never
    /// pass the maximums; nobody sleeps aboard a hull without a bunk; every role a piece of work needs is
    /// a real role. The rules themselves are pinned in <see cref="CrewVoyageLimitsTests"/>.
    /// </summary>
    public class CrewVoyageContentTests
    {
        private const string ContentRoot = "Assets/_Project";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        // Disposable proof copies some fixtures leave under the content root (see DefIdentityContractTests).
        private static readonly Regex DisposableProof = new Regex("_[0-9a-f]{32}(/|\\.|$)");
        private static readonly Regex RoleId = new Regex("^role\\.[a-z0-9]+(_[a-z0-9]+)*$");
        private static readonly Regex TaskId = new Regex("^task\\.[a-z0-9]+(_[a-z0-9]+)*$");
        private static readonly Regex WorkId = new Regex("^work\\.[a-z0-9]+(_[a-z0-9]+)*$");

        private static List<T> Load<T>() where T : ScriptableObject
            => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { ContentRoot })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => !DisposableProof.IsMatch(path))
                .Select(path => AssetDatabase.LoadAssetAtPath<T>(path))
                .Where(asset => asset != null)
                .ToList();

        private static List<BoatHullDef> Hulls() => Load<BoatHullDef>();
        private static List<BoatVoyageDef> Voyages() => Load<BoatVoyageDef>();
        private static List<StaffRoleDef> RoleDefs() => Load<StaffRoleDef>();
        private static StaffRoleBook Book() => new StaffRoleBook(RoleDefs());

        private static IEnumerable<(BoatHullDef Hull, BoatVoyageDef Voyage, VoyageSpec Spec)> Joined()
        {
            var voyages = Voyages().ToDictionary(v => v.HullId, StringComparer.Ordinal);
            foreach (var hull in Hulls().OrderBy(h => h.Id, StringComparer.Ordinal))
            {
                Assert.That(voyages.TryGetValue(hull.Id, out var voyage), Is.True, "no voyage record for " + hull.Id);
                yield return (hull, voyage, BoatVoyageCatalog.SpecFor(hull, voyage));
            }
        }

        [Test]
        public void EveryHull_HasExactlyOneVoyageRecord_AndEveryRecordHasAHull()
        {
            var hulls = Hulls();
            var voyages = Voyages();
            Assert.That(hulls.Count, Is.GreaterThan(0));
            var hullIds = new HashSet<string>(hulls.Select(h => h.Id), StringComparer.Ordinal);
            foreach (var group in voyages.GroupBy(v => v.HullId, StringComparer.Ordinal))
                Assert.That(group.Count(), Is.EqualTo(1), "two voyage records for " + group.Key);
            foreach (var v in voyages)
            {
                Assert.That(hullIds.Contains(v.HullId), Is.True, v.Id + " names a hull that does not exist: " + v.HullId);
                Assert.That(v.Id, Is.EqualTo("voyage." + v.HullId.Substring("boat.".Length)), v.name);
            }
            foreach (var hull in hulls)
                Assert.That(voyages.Any(v => v.HullId == hull.Id), Is.True, "no voyage record for " + hull.Id);

            var catalog = new BoatVoyageCatalog(hulls, voyages);
            Assert.That(catalog.Count, Is.EqualTo(hulls.Count));
        }

        [Test]
        public void Minimums_NeverExceedTheMaximums()
        {
            var policy = CrewPolicy.Default;
            foreach (var (hull, voyage, spec) in Joined())
            {
                Assert.That(spec.DayMax, Is.EqualTo(hull.CrewSlots), hull.Id + ": the day limit is the crew slots");
                Assert.That(spec.MinCrew(TripKind.Day), Is.LessThanOrEqualTo(spec.DayMax), hull.Id);
                for (var trip = TripKind.Overnight; trip <= spec.LongestTripNow; trip++)
                    Assert.That(spec.MinCrew(trip), Is.LessThanOrEqualTo(spec.OvernightMax(policy)), hull.Id + " " + trip);
                foreach (var work in voyage.Work)
                {
                    Assert.That(work.MinCrew, Is.LessThanOrEqualTo(spec.DayMax), hull.Id + " " + work.WorkId);
                    Assert.That(work.RequiredRoleIds.Length, Is.LessThanOrEqualTo(work.MinCrew),
                        hull.Id + " " + work.WorkId + ": each required role is a person");
                }
            }
        }

        [Test]
        public void OvernightMax_NeverExceedsTheBunks_UnderTheShippedPolicy()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.That(shipped, Is.Not.Null, ConfigPath);
            Assert.That(shipped.Crew.HiredHandsShareBunks, Is.False, "audit Q3 is open: hand bunk-sharing ships OFF");
            Assert.That(shipped.Crew.MonthTripsNeedHiredSkipper, Is.True);
            foreach (var (hull, _, spec) in Joined())
            {
                Assert.That(spec.Bunks, Is.EqualTo(HullBunks.Of(hull)), hull.Id);
                Assert.That(spec.OvernightMax(shipped.Crew), Is.LessThanOrEqualTo(spec.Bunks), hull.Id);
                Assert.That(spec.OvernightMax(shipped.Crew), Is.LessThanOrEqualTo(spec.DayMax), hull.Id);
            }
        }

        [Test]
        public void AHullWithNoBunk_CannotGoOvernight()
        {
            var roles = Book();
            int none = 0;
            foreach (var (hull, _, spec) in Joined())
            {
                if (spec.Bunks > 0) continue;
                none++;
                for (var trip = TripKind.Overnight; trip <= TripKind.Month; trip++)
                {
                    var v = VoyageLimits.Check(spec, new[] { CrewMember.Player("p1", "role.skipper") },
                        new VoyagePlan(trip), roles, CrewPolicy.Default);
                    Assert.That(v.Allowed, Is.False, hull.Id + " " + trip);
                    Assert.That(v.Reason, Is.EqualTo(VoyageRefusal.NoBunks), hull.Id + " " + trip);
                }
            }
            Assert.That(none, Is.GreaterThan(0), "premise: the open boats have no bunk");
        }

        [Test]
        public void ASkipperAlone_CanTakeEveryHullOutForTheDay_AndAsFarAsSheHonestlyGoes()
        {
            var roles = Book();
            foreach (var (hull, _, spec) in Joined())
            {
                var crew = new[] { CrewMember.Player("p1", "role.skipper") };
                for (var trip = TripKind.Day; trip <= TripKind.Week; trip++)
                {
                    var v = VoyageLimits.Check(spec, crew, new VoyagePlan(trip), roles, CrewPolicy.Default);
                    Assert.That(v.Allowed, Is.EqualTo(trip <= spec.LongestTripNow), hull.Id + " " + trip + ": " + v);
                }
            }
        }

        [Test]
        public void EveryRoleAWorkNeeds_IsARealRole_AndEveryWorkHasASpecialist()
        {
            var roles = Book();
            foreach (var (hull, voyage, _) in Joined())
                foreach (var work in voyage.Work)
                {
                    Assert.That(WorkId.IsMatch(work.WorkId ?? ""), Is.True, hull.Id + ": " + work.WorkId);
                    Assert.That(roles.AnyRoleWorks(work.WorkId), Is.True, work.WorkId + " is named by no StaffRoleDef");
                    foreach (string role in work.RequiredRoleIds)
                        Assert.That(roles.Contains(role), Is.True, hull.Id + " " + work.WorkId + " needs unknown " + role);
                    Assert.That(work.RequiredRoleIds.Any(r => roles.HasTask(r, StaffRoleBook.HelmTask)), Is.True,
                        hull.Id + " " + work.WorkId + ": somebody on the work list steers");
                }
        }

        [Test]
        public void Roles_AreWellFormed_Unique_AndLoadFromResources()
        {
            var defs = RoleDefs();
            Assert.That(defs.Count, Is.GreaterThanOrEqualTo(5));
            CollectionAssert.AllItemsAreUnique(defs.Select(d => d.Id));
            foreach (var d in defs)
            {
                Assert.That(RoleId.IsMatch(d.Id ?? ""), Is.True, d.name + ": " + d.Id);
                Assert.That(string.IsNullOrWhiteSpace(d.DisplayName), Is.False, d.Id);
                Assert.That(d.TaskIds.Length, Is.GreaterThan(0), d.Id + " does no task");
                foreach (string t in d.TaskIds) Assert.That(TaskId.IsMatch(t ?? ""), Is.True, d.Id + ": " + t);
                foreach (string w in d.WorkIds) Assert.That(WorkId.IsMatch(w ?? ""), Is.True, d.Id + ": " + w);
                Assert.That(AssetDatabase.GetAssetPath(d), Does.Contain("/Resources/" + StaffRoleBook.ResourcesFolder + "/"), d.Id);
            }
            foreach (string core in new[] { "role.skipper", "role.mate", "role.engineer", "role.deckhand", "role.cook" })
                Assert.That(defs.Any(d => d.Id == core), Is.True, "the brief's core role " + core);
            Assert.That(defs.Any(d => d.TaskIds.Contains(StaffRoleBook.HelmTask)), Is.True);

            var book = StaffRoleBook.FromResources();
            CollectionAssert.AreEquivalent(defs.Select(d => d.Id), book.Ids);
        }

        [Test]
        public void ShippedData_GivesTheSameAnswerEveryTime()
        {
            var roles = Book();
            var crew = new[] { CrewMember.Player("p1", "role.skipper"), CrewMember.Hand("h1", "role.sternman") };
            string Run() => string.Join("|", Joined().Select(j =>
                j.Hull.Id + ":" + string.Join(",", Enumerable.Range(0, 4).Select(k =>
                    VoyageLimits.Check(j.Spec, crew, new VoyagePlan((TripKind)k, "work.lobster_pots"), roles,
                        CrewPolicy.Default).ToString()))));
            Assert.That(Run(), Is.EqualTo(Run()));
        }
    }
}
