using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WorkoutContentTests
    {
        [Test]
        public void ShippedPlans_CoverEveryExerciseExactlyOnce_WithStableIdsAndReciprocalLinks()
        {
            WorkoutDef[] plans = Resources.LoadAll<WorkoutDef>("Workouts");
            Assert.AreEqual(9, plans.Length, "The first gym offering contains exactly nine exercise plans.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var exercises = new HashSet<string>(StringComparer.Ordinal);
            var equipment = new HashSet<string>(StringComparer.Ordinal);
            var expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (ActivityDef exercise in ActivityCatalog.Activities)
                if (exercise.Kind == ActivityKind.Exercise) expected.Add(exercise.Id);
            foreach (WorkoutDef plan in plans)
            {
                Assert.IsNull(plan.ValidationError(), plan.name);
                Assert.IsTrue(Regex.IsMatch(plan.Id, "^workout\\.[a-z0-9]+(?:_[a-z0-9]+)*$"), plan.Id);
                Assert.IsTrue(ids.Add(plan.Id), "Duplicate workout id " + plan.Id);
                Assert.IsTrue(exercises.Add(plan.Exercise.Id), "Conflicting daily caps for " + plan.Exercise.Id);
                Assert.AreSame(ActivityCatalog.Find(plan.Exercise.Id), plan.Exercise, plan.Id);
                CollectionAssert.Contains(ActivityCatalog.Equipment, plan.Equipment, plan.Id);
                equipment.Add(plan.Equipment.Id);
                Assert.GreaterOrEqual(plan.DailyTrainingUnits, plan.Units, plan.Id);
            }
            CollectionAssert.AreEquivalent(expected, exercises);
            Assert.AreEqual(7, equipment.Count, "Each of the seven equipment kinds has a workout.");
        }

        [Test]
        public void Validation_RejectsMalformedValuesAndBrokenEquipmentLinks()
        {
            var plan = ScriptableObject.CreateInstance<WorkoutDef>();
            var exercise = ScriptableObject.CreateInstance<ActivityDef>();
            var equipment = ScriptableObject.CreateInstance<GymEquipmentDef>();
            try
            {
                plan.Id = "workout.validation"; plan.Equipment = equipment; plan.Exercise = exercise;
                equipment.Id = "gym_equipment.test"; exercise.Id = "activity.test";
                equipment.ExerciseIds = new[] { exercise.Id }; exercise.EquipmentIds = new[] { equipment.Id };
                exercise.Kind = ActivityKind.Exercise; exercise.Unit = ActivityUnit.Minute;
                exercise.Training = new TrainingDoses { Cardio = 1f };
                plan.DurationGameMinutes = plan.Units = 10f;
                plan.DailyTrainingUnits = 30f; plan.MinimumPace = 0.6f;
                Assert.IsNull(plan.ValidationError());
                foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -1f, 0f })
                {
                    plan.DurationGameMinutes = invalid; Assert.IsNotNull(plan.ValidationError()); plan.DurationGameMinutes = 10f;
                    plan.Units = invalid; Assert.IsNotNull(plan.ValidationError()); plan.Units = 10f;
                    plan.DailyTrainingUnits = invalid; Assert.IsNotNull(plan.ValidationError()); plan.DailyTrainingUnits = 30f;
                    plan.MinimumPace = invalid; Assert.IsNotNull(plan.ValidationError()); plan.MinimumPace = 0.6f;
                }
                plan.TirednessPerGameHour = float.NaN; Assert.IsNotNull(plan.ValidationError()); plan.TirednessPerGameHour = 0f;
                plan.MinimumPace = 1.1f; Assert.IsNotNull(plan.ValidationError()); plan.MinimumPace = 0.6f;
                plan.Units = 11f; Assert.IsNotNull(plan.ValidationError()); plan.Units = 10f;
                exercise.KcalPerUnitAboveRest = float.PositiveInfinity; Assert.IsNotNull(plan.ValidationError()); exercise.KcalPerUnitAboveRest = 0f;
                exercise.Training = new TrainingDoses { Cardio = float.NaN }; Assert.IsNotNull(plan.ValidationError());
                exercise.Training = new TrainingDoses(); Assert.IsNotNull(plan.ValidationError());
                exercise.Training = new TrainingDoses { Cardio = 1f };
                exercise.Kind = ActivityKind.Work; Assert.IsNotNull(plan.ValidationError()); exercise.Kind = ActivityKind.Exercise;
                exercise.Signal = ActivitySignal.FishFight; Assert.IsNotNull(plan.ValidationError()); exercise.Signal = ActivitySignal.None;
                exercise.Unit = (ActivityUnit)99; Assert.IsNotNull(plan.ValidationError()); exercise.Unit = ActivityUnit.Minute;
                equipment.ExerciseIds = Array.Empty<string>(); Assert.IsNotNull(plan.ValidationError());
                equipment.ExerciseIds = new[] { exercise.Id };
                exercise.EquipmentIds = Array.Empty<string>(); Assert.IsNotNull(plan.ValidationError());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(plan);
                UnityEngine.Object.DestroyImmediate(exercise);
                UnityEngine.Object.DestroyImmediate(equipment);
            }
        }
    }
}
