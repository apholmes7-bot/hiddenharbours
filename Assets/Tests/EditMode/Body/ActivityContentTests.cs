using System.Collections.Generic;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The shipped <see cref="ActivityDef"/> and <see cref="GymEquipmentDef"/> assets (body slice 2): ids
    /// well formed and unique, every cross-reference resolves, the owner's first exercise set and work list
    /// are present, every effort costs more than sitting still, and each game signal is bound at most once.
    /// </summary>
    public class ActivityContentTests
    {
        private static readonly Regex ActivityId = new Regex(@"^activity\.[a-z0-9]+(_[a-z0-9]+)*$");
        private static readonly Regex EquipmentId = new Regex(@"^gym_equipment\.[a-z0-9]+(_[a-z0-9]+)*$");

        [SetUp]
        public void SetUp() => ActivityCatalog.Reset();

        [TearDown]
        public void TearDown() => ActivityCatalog.Reset();

        [Test]
        public void Activities_HaveUniqueWellFormedIds_AndPositiveEnergy()
        {
            IReadOnlyList<ActivityDef> all = ActivityCatalog.Activities;
            Assert.Greater(all.Count, 0, "activities load from Resources/Activities");
            var seen = new HashSet<string>();
            foreach (ActivityDef def in all)
            {
                Assert.IsNotNull(def);
                Assert.IsTrue(def.Id != null && ActivityId.IsMatch(def.Id), def.name + ": '" + def.Id + "' is not activity.<snake_case>");
                Assert.IsTrue(seen.Add(def.Id), "duplicate id " + def.Id);
                Assert.IsFalse(string.IsNullOrEmpty(def.DisplayName), def.Id);
                Assert.GreaterOrEqual(def.KcalPerUnitAboveRest, 0f, def.Id);
                if (def.Kind == ActivityKind.Exercise)
                    Assert.Greater(def.EquipmentIds?.Length ?? 0, 0, def.Id + ": an exercise names its equipment");
            }
        }

        [Test]
        public void TheOwnersWorkAndExerciseLists_ArePresent()
        {
            foreach (string id in new[]
                     {
                         // work (2026-11-15: "hauling pots, lines and nets; reeling; rowing; digging clams;
                         // carrying; farm work")
                         "activity.haul_pot", "activity.haul_line", "activity.haul_net", "activity.reel_fish",
                         "activity.row_dory", "activity.dig_clams", "activity.carry_load", "activity.farm_work",
                         "activity.deck_work", "activity.walk", "activity.run", "activity.drive",
                         // the first exercise set
                         "activity.barbell_squat", "activity.deadlift", "activity.bench_press", "activity.pull_up",
                         "activity.seated_row", "activity.grip_work", "activity.treadmill_run",
                         "activity.rowing_machine", "activity.skipping",
                     })
                Assert.IsNotNull(ActivityCatalog.Find(id), id);
        }

        [Test]
        public void Equipment_AndActivities_ReferenceEachOther()
        {
            IReadOnlyList<GymEquipmentDef> equipment = ActivityCatalog.Equipment;
            Assert.Greater(equipment.Count, 0, "equipment loads from Resources/GymEquipment");
            var ids = new HashSet<string>();
            foreach (GymEquipmentDef eq in equipment)
            {
                Assert.IsTrue(eq.Id != null && EquipmentId.IsMatch(eq.Id), eq.name + ": '" + eq.Id + "' is not gym_equipment.<snake_case>");
                Assert.IsTrue(ids.Add(eq.Id), "duplicate id " + eq.Id);
                Assert.Greater(eq.ExerciseIds?.Length ?? 0, 0, eq.Id + " offers an exercise");
                Assert.Greater(eq.FootprintTiles.x, 0, eq.Id);
                Assert.Greater(eq.FootprintTiles.y, 0, eq.Id);
                foreach (string ex in eq.ExerciseIds)
                {
                    ActivityDef def = ActivityCatalog.Find(ex);
                    Assert.IsNotNull(def, eq.Id + " names " + ex);
                    CollectionAssert.Contains(def.EquipmentIds, eq.Id, ex + " names its machine back");
                }
            }
            foreach (ActivityDef def in ActivityCatalog.Activities)
                foreach (string eq in def.EquipmentIds ?? System.Array.Empty<string>())
                    Assert.IsTrue(ids.Contains(eq), def.Id + " needs unknown equipment " + eq);
        }

        [Test]
        public void EveryEffort_CostsMoreThanSittingStill_AndEachSignalIsBoundOnce()
        {
            ActivityDef drive = ActivityCatalog.Find("activity.drive");
            Assert.AreEqual(ActivityKind.Sedentary, drive.Kind);
            var signals = new HashSet<ActivitySignal>();
            foreach (ActivityDef def in ActivityCatalog.Activities)
            {
                if (def.Signal != ActivitySignal.None)
                    Assert.IsTrue(signals.Add(def.Signal), def.Signal + " is bound twice");
                if (def.Kind == ActivityKind.Sedentary) continue;
                // A repetition is a few seconds: compare a minute of steady sets (ten) with a minute driving.
                float perMinute = def.Unit == ActivityUnit.Repetition ? def.KcalPerUnitAboveRest * 10f
                                                                     : def.KcalPerUnitAboveRest;
                Assert.Greater(perMinute, drive.KcalPerUnitAboveRest * 3f, def.Id + " burns well above driving");
                float doses = def.Training.Grip + def.Training.Back + def.Training.Legs + def.Training.Cardio;
                Assert.Greater(doses, 0f, def.Id + " trains something");
            }
            Assert.AreEqual(0f, drive.Training.Grip + drive.Training.Back + drive.Training.Legs + drive.Training.Cardio,
                "driving trains nothing");
            foreach (ActivitySignal s in new[] { ActivitySignal.FishFight, ActivitySignal.TrapHaul,
                                                 ActivitySignal.DeckWork, ActivitySignal.Driving })
                Assert.IsNotNull(ActivityCatalog.ForSignal(s), s + " has an activity");
        }
    }
}
