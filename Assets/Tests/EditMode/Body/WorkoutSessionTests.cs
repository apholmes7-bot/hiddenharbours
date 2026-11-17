using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WorkoutSessionTests
    {
        private readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();
        private WorkoutDef _plan;
        private Needs _needs;
        private Clean _clean;
        private WorkoutService _service;
        private double _mass;
        private const double Hour = 600d;

        [SetUp]
        public void SetUp()
        {
            _plan = Make<WorkoutDef>();
            _plan.Id = "workout.test";
            _plan.Equipment = Make<GymEquipmentDef>();
            _plan.Equipment.Id = "gym_equipment.test";
            _plan.Exercise = Make<ActivityDef>();
            _plan.Exercise.Id = "activity.test";
            _plan.Exercise.Kind = ActivityKind.Exercise;
            _plan.Exercise.Unit = ActivityUnit.Repetition;
            _plan.Exercise.KcalPerUnitAboveRest = 2f;
            _plan.Exercise.Training = new TrainingDoses { Grip = 1f, Back = 0.5f };
            _plan.Exercise.EquipmentIds = new[] { _plan.Equipment.Id };
            _plan.Equipment.ExerciseIds = new[] { _plan.Exercise.Id };
            _plan.DurationGameMinutes = 10f;
            _plan.Units = 30f;
            _plan.DailyTrainingUnits = 60f;
            _plan.TirednessPerGameHour = 0.12f;
            _plan.MinimumPace = 0.5f;
            _mass = StrengthMath.ReferenceLeanKg(BodySettings.Default);
            _needs = new Needs(); _clean = new Clean();
            _service = Service(_needs, _clean);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object obj in _made) UnityEngine.Object.DestroyImmediate(obj);
            _made.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        { T obj = ScriptableObject.CreateInstance<T>(); _made.Add(obj); return obj; }

        private static StrengthSettings Settings => new StrengthSettings
        {
            Strength = new FitnessCurve { GainPerDose = 0.0005f, DecayPerGameDay = 0.01f, GraceGameDays = 3f, MaintainDose = 15f },
            Cardio = new FitnessCurve { GainPerDose = 0.0007f, DecayPerGameDay = 0.02f, GraceGameDays = 2f, MaintainDose = 20f },
            MemoryRegainFactor = 4f,
        };

        private static WorkoutService Service(Needs needs, Clean clean) => new WorkoutService(
            needs, clean, BodySettings.Default, NeedsSettings.Default, Settings, Hour);

        private void Start(double at = 0d, string instance = "rack.1")
            => Assert.AreEqual(WorkoutStartResult.Started, _service.TryStart(_plan, instance, at, _mass));

        [Test]
        public void Completion_ChargesExactlyOnce_AtTheBoundedEnd()
        {
            Start();
            Assert.AreEqual("rack.1", _service.EquipmentInstanceId);
            Assert.AreEqual(100d, _service.EndsAtGameSeconds);
            Assert.IsFalse(_service.Tick(99d));
            Assert.AreEqual(0, _needs.SpendCalls);
            Assert.IsTrue(_service.Tick(200d));
            Assert.IsFalse(_service.Tick(200d));
            Assert.IsFalse(_service.Cancel(201d));
            WorkoutReceipt r = _service.LastReceipt.Value;
            Assert.IsTrue(r.Completed);
            Assert.AreEqual(100d, r.EndGameSeconds);
            Assert.AreEqual(30d, r.Units);
            Assert.AreEqual(30d, r.CreditedUnits);
            Assert.AreEqual(0.02f, _needs.Tiredness, 1e-7f);
            Assert.AreEqual(1d / 6d, _clean.Hours, 1e-12d);
            Assert.AreEqual(DirtActivity.Gym, _clean.Kind);
            double expected = 60d / BodyMath.KcalPerHungerUnit(NeedsSettings.Default.Hunger.RisePerGameHour, BodySettings.Default);
            Assert.AreEqual(expected, _needs.Hunger, 1e-7d);
            Assert.AreEqual(1, _needs.SpendCalls);
            Assert.AreEqual(1, _needs.ExertCalls);
            Assert.AreEqual(1, _clean.Calls);
            Assert.IsFalse(_service.IsActive);
            Assert.AreEqual("", _service.EquipmentInstanceId);
        }

        [Test]
        public void TickPartition_DuplicateTime_AndLongJumpAgree()
        {
            Start();
            for (int i = 0; i <= 100; i++) { _service.Tick(i); _service.Tick(i); }
            var needs = new Needs(); var clean = new Clean();
            var jump = Service(needs, clean);
            Assert.AreEqual(WorkoutStartResult.Started, jump.TryStart(_plan, "rack.1", 0d, _mass));
            jump.Tick(Hour * 24d * 8d);
            Assert.AreEqual(_needs.Hunger, needs.Hunger);
            Assert.AreEqual(_needs.Tiredness, needs.Tiredness);
            Assert.AreEqual(_clean.Hours, clean.Hours);
            foreach (FitnessKind kind in StrengthMath.Kinds)
                Assert.AreEqual(_service.Fitness.Level(kind, Hour * 24d * 8d), jump.Fitness.Level(kind, Hour * 24d * 8d));
            Assert.AreEqual(0d, jump.Fitness.CreditedUnits(_plan.Exercise.Id, Hour * 24d * 8d));
        }

        [Test]
        public void HungerSlowsWithoutRefusal_AndPaceIsCaptured()
        {
            _needs.Pace = 0f;
            Start();
            Assert.AreEqual(200d, _service.EndsAtGameSeconds);
            _needs.Pace = 1f;
            Assert.IsFalse(_service.Tick(100d));
            Assert.IsTrue(_service.Tick(200d));
            Assert.AreEqual(30d, _service.LastReceipt.Value.CreditedUnits);
            Assert.AreEqual(0.04f, _needs.Tiredness, 1e-7f);
        }

        [Test]
        public void Cancel_ChargesAndTrainsOnlyElapsedFraction()
        {
            Start();
            Assert.IsTrue(_service.Cancel(25d));
            var r = _service.LastReceipt.Value;
            Assert.IsFalse(r.Completed);
            Assert.AreEqual(7.5d, r.Units);
            Assert.AreEqual(7.5d, r.CreditedUnits);
            Assert.AreEqual(0.005f, r.Tiredness, 1e-7f);
            Assert.AreEqual(25d / Hour, _clean.Hours);
            Assert.IsFalse(_service.Cancel(25d));
            Assert.AreEqual(1, _needs.SpendCalls);
        }

        [Test]
        public void ImmediateCancel_CannotFarmGainsOrCosts()
        {
            for (int i = 0; i < 20; i++) { Start(); _service.Cancel(0d); }
            Assert.AreEqual(0d, _service.Fitness.CreditedUnits(_plan.Exercise.Id, 0d));
            Assert.AreEqual(0f, _service.Fitness.Level(FitnessKind.Grip, 0d));
            Assert.AreEqual(0, _needs.SpendCalls);
            Assert.AreEqual(0, _clean.Calls);
        }

        [TestCase(100d)]
        [TestCase(101d)]
        public void CancelAtOrAfterEnd_IsCompletion(double at)
        { Start(); _service.Cancel(at); Assert.IsTrue(_service.LastReceipt.Value.Completed); Assert.AreEqual(30d, _service.LastReceipt.Value.Units); }

        [Test]
        public void DailyCap_IsSharedAcrossMachinesAndPartialSets_CostsContinue()
        {
            float beforeCap = 0f;
            for (int i = 0; i < 5; i++)
            {
                Start(i * 50d, "rack." + i);
                _service.Cancel((i + 1) * 50d);
                if (i == 3) beforeCap = _service.Fitness.Level(FitnessKind.Grip, 200d);
            }
            Assert.AreEqual(60d, _service.Fitness.CreditedUnits(_plan.Exercise.Id, 250d));
            Assert.AreEqual(0d, _service.LastReceipt.Value.CreditedUnits);
            Assert.AreEqual(15d, _service.LastReceipt.Value.Units);
            Assert.AreEqual(5, _needs.SpendCalls);
            Assert.AreEqual(0.05f, _needs.Tiredness, 1e-7f);
            Assert.AreEqual(beforeCap, _service.Fitness.Level(FitnessKind.Grip, 250d), "No extra gain past the cap.");
        }

        [Test]
        public void Midnight_UsesEndpointDay_AndOpensAnotherAllowance()
        {
            Start(); _service.Tick(100d);
            Start(100d); _service.Tick(200d);
            double day = Hour * 24d;
            Start(day - 50d); _service.Tick(day + 50d);
            Assert.AreEqual(30d, _service.LastReceipt.Value.CreditedUnits);
            Assert.AreEqual(30d, _service.Fitness.CreditedUnits(_plan.Exercise.Id, day + 50d));
        }

        [Test]
        public void DifferentExercise_HasItsOwnDailyAllowance()
        {
            Start(); _service.Tick(100d);
            Start(100d); _service.Tick(200d);
            _plan.Exercise.Id = "activity.other";
            _plan.Equipment.ExerciseIds = new[] { _plan.Exercise.Id };
            Start(200d); _service.Tick(300d);
            Assert.AreEqual(30d, _service.LastReceipt.Value.CreditedUnits);
            Assert.AreEqual(60d, _service.Fitness.CreditedUnits("activity.test", 300d));
        }

        [Test]
        public void Fitness_HoldsDecaysAndRegainsUsingExistingCurve()
        {
            Start(); _service.Tick(100d);
            float peak = _service.Fitness.Level(FitnessKind.Grip, 100d);
            Assert.AreEqual((float)(1d - Math.Exp(-0.0005f * 30d)), peak);
            Assert.AreEqual(peak, _service.Fitness.Level(FitnessKind.Grip, 100d + Hour * 24d * 3d));
            double later = 100d + Hour * 24d * 4d;
            float faded = _service.Fitness.Level(FitnessKind.Grip, later);
            Assert.AreEqual(Math.Max(0f, peak - 0.01f), faded, 1e-7f);
            Start(later); _service.Tick(later + 100d);
            float atEnd = Math.Max(0f, peak - 0.01f * (float)(1d + 100d / (Hour * 24d)));
            float expected = FitnessMath.Grow(atEnd, peak, 30f, 0.0005f, 4f);
            Assert.AreEqual(expected, _service.Fitness.Level(FitnessKind.Grip, later + 100d), 1e-7f);
            Assert.AreEqual(0f, _service.Fitness.Level(FitnessKind.Cardio, later + 100d));
        }

        [Test]
        public void ActiveSession_CannotBeReplaced_AndDefEditsDoNotChangeIt()
        {
            Start();
            Assert.AreEqual(WorkoutStartResult.AlreadyActive, _service.TryStart(_plan, "rack.2", 0d, _mass));
            _plan.Units = 999f; _plan.DurationGameMinutes = 999f;
            _plan.TirednessPerGameHour = 1f; _plan.DailyTrainingUnits = 1f;
            _plan.Exercise.Training = new TrainingDoses { Legs = 20f };
            _service.Tick(100d);
            Assert.AreEqual(30d, _service.LastReceipt.Value.Units);
            Assert.AreEqual(30d, _service.LastReceipt.Value.CreditedUnits);
            Assert.AreEqual(0.02f, _service.LastReceipt.Value.Tiredness, 1e-7f);
            Assert.AreEqual(0f, _service.Fitness.Level(FitnessKind.Legs, 100d));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1d)]
        public void InvalidTime_RejectsBeforeMutatingSession(double time)
        {
            Start();
            Assert.Throws<ArgumentOutOfRangeException>(() => _service.Tick(time));
            Assert.IsTrue(_service.IsActive);
            Assert.AreEqual(0, _needs.SpendCalls);
            _service.Tick(100d);
            Assert.AreEqual(30d, _service.LastReceipt.Value.CreditedUnits);
        }

        [Test]
        public void RewindIsRejected_RecreateIsExplicitRuntimeReset()
        {
            Start(200d); _service.Tick(250d);
            Assert.Throws<ArgumentOutOfRangeException>(() => _service.Cancel(249d));
            Assert.IsTrue(_service.IsActive);
            _service.Tick(300d);
            Assert.Greater(_service.Fitness.Level(FitnessKind.Grip, 300d), 0f);
            var fresh = Service(new Needs(), new Clean());
            Assert.AreEqual(0f, fresh.Fitness.Level(FitnessKind.Grip, 0d));
            Assert.AreEqual(0d, fresh.Fitness.CreditedUnits(_plan.Exercise.Id, 0d));
            Assert.IsFalse(fresh.IsActive);
        }

        [Test]
        public void InvalidPlanBodyAndInstance_AreRefusedWithoutCosts()
        {
            Assert.AreEqual(WorkoutStartResult.InvalidBody, _service.TryStart(_plan, "rack", 0d, double.NaN));
            Assert.AreEqual(WorkoutStartResult.InvalidEquipment, _service.TryStart(_plan, " ", 0d, _mass));
            _plan.MinimumPace = 0f;
            Assert.AreEqual(WorkoutStartResult.InvalidPlan, _service.TryStart(_plan, "rack", 0d, _mass));
            Assert.IsFalse(_service.IsActive);
            Assert.AreEqual(0, _needs.SpendCalls);
        }

        [Test]
        public void BodyMassScalesHunger_ButNotTraining()
        {
            Start(); _service.Tick(100d);
            var needs = new Needs(); var other = Service(needs, new Clean());
            Assert.AreEqual(WorkoutStartResult.Started, other.TryStart(_plan, "rack", 0d, _mass * 2d));
            other.Tick(100d);
            Assert.AreEqual(_needs.Hunger * 2f, needs.Hunger);
            Assert.AreEqual(_service.Fitness.Level(FitnessKind.Grip, 100d), other.Fitness.Level(FitnessKind.Grip, 100d));
        }

        [Test]
        public void InvalidConfigurationAndOverflow_CannotStartOrCharge()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorkoutService(_needs, _clean,
                BodySettings.Default, NeedsSettings.Default, Settings, double.PositiveInfinity));
            var bad = Settings; bad.Strength.GainPerDose = float.NaN;
            Assert.Throws<ArgumentException>(() => new WorkoutService(_needs, _clean,
                BodySettings.Default, NeedsSettings.Default, bad, Hour));
            Assert.AreEqual(WorkoutStartResult.InvalidPlan, _service.TryStart(_plan, "rack", 0d, double.MaxValue));
            _plan.Exercise.Training = new TrainingDoses { Grip = float.MaxValue };
            Assert.AreEqual(WorkoutStartResult.InvalidPlan, _service.TryStart(_plan, "rack", 0d, _mass));
            Assert.AreEqual(0, _needs.SpendCalls);
        }

        private sealed class Needs : INeedsService
        {
            public float Pace = 1f, Hunger, Tiredness;
            public int SpendCalls, ExertCalls;
            public float Level(NeedKind kind) => kind == NeedKind.Hunger ? Hunger : Tiredness;
            public NeedStage Stage(NeedKind kind) => NeedStage.None;
            public float PlayerMultiplier(TraitStat stat) => Pace;
            public float Spend(float amount) { SpendCalls++; Hunger += amount; return 0f; }
            public void Exert(float amount) { ExertCalls++; Tiredness += amount; }
            public void Eat(float amount) { }
            public void Sleep() { }
            public void Tick() { }
        }

        private sealed class Clean : ICleanlinessService
        {
            public double Hours;
            public int Calls;
            public DirtActivity Kind;
            public float Dirt => 0f;
            public DirtActivity Activity => DirtActivity.None;
            public WashStationDef WashingAt => null;
            public void ReportActivity(DirtActivity kind, double hours) { Calls++; Kind = kind; Hours += hours; }
            public void BeginActivity(DirtActivity kind) { }
            public void EndActivity(DirtActivity kind) { }
            public void ReportAction(DirtActivity kind) { }
            public void BeginWash(WashStationDef station) { }
            public void EndWash() { }
            public WashStationDef FindStation(string id) => null;
            public void Settle() { }
            public void Tick() { }
        }
    }
}
