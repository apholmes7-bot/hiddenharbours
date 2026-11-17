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
        private MemorySave _save;
        private Clock _clock;
        private BodyService _body;
        private readonly List<ActivityReported> _reports = new List<ActivityReported>();
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
            _save = new MemorySave(); _clock = new Clock();
            _needs = new Needs(); _clean = new Clean();
            _body = Body(_save, _clock, _needs);
            _service = Service(_needs, _clean, _body);
            _reports.Clear();
            EventBus.Subscribe<ActivityReported>(OnActivity);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<ActivityReported>(OnActivity);
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

        private static WorkoutService Service(Needs needs, Clean clean, IBodyService body) => new WorkoutService(
            needs, clean, BodySettings.Default, NeedsSettings.Default, Settings, Hour, body);

        private void Start(double at = 0d, string instance = "rack.1")
            => Assert.AreEqual(WorkoutStartResult.Started, _service.TryStart(_plan, instance, at));

        [Test]
        public void Completion_ChargesExactlyOnce_AtTheBoundedEnd()
        {
            Start();
            Assert.AreEqual("rack.1", _service.EquipmentInstanceId);
            Assert.AreEqual(100d, _service.EndsAtGameSeconds);
            Assert.IsFalse(Tick(99d));
            Assert.AreEqual(0, _needs.SpendCalls);
            Assert.IsTrue(Tick(200d));
            Assert.IsFalse(Tick(200d));
            Assert.IsFalse(Cancel(201d));
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
            Assert.AreEqual(1, _reports.Count);
            Assert.AreEqual(_plan.Exercise.Id, _reports[0].ActivityId);
            Assert.AreEqual(30f, _reports[0].Units);
            Assert.AreEqual(60d, _reports[0].Kcal);
            Assert.AreEqual(r.Hunger, _reports[0].HungerAdded);
            Assert.IsFalse(_service.IsActive);
            Assert.AreEqual("", _service.EquipmentInstanceId);
        }

        [Test]
        public void TickPartition_AndLongJumpBoundWork_BodyAnchorsAtObservation()
        {
            Start();
            for (int i = 0; i <= 100; i++) { Tick(i); Tick(i); }
            var needs = new Needs(); var clean = new Clean();
            var jumpSave = new MemorySave(); var jumpClock = new Clock();
            var jumpBody = Body(jumpSave, jumpClock, needs);
            var jump = Service(needs, clean, jumpBody);
            Assert.AreEqual(WorkoutStartResult.Started, jump.TryStart(_plan, "rack.1", 0d));
            jumpClock.T = Hour * 24d * 8d;
            jump.Tick(jumpClock.T);
            Assert.AreEqual(_needs.Hunger, needs.Hunger);
            Assert.AreEqual(_needs.Tiredness, needs.Tiredness);
            Assert.AreEqual(_clean.Hours, clean.Hours);
            foreach (FitnessKind kind in StrengthMath.Kinds)
            {
                // Body settlement uses its observing clock; work units and peak gain agree,
                // while a late observation starts decay later than timely settlement.
                Assert.AreEqual(_save.Current.PlayerBody.Get(kind).Peak, jumpSave.Current.PlayerBody.Get(kind).Peak);
                Assert.AreEqual(FitnessMath.LevelAt(jumpSave.Current.PlayerBody.Get(kind),
                    StrengthMath.CurveFor(kind, Settings), jumpClock.T, Hour * 24d), jumpBody.FitnessLevel(kind));
            }
            Assert.AreEqual(100d, _service.LastReceipt.Value.EndGameSeconds);
            Assert.AreEqual(100d, jump.LastReceipt.Value.EndGameSeconds);
            Assert.AreEqual(100d, _save.Current.PlayerBody.Grip.SinceGameSeconds);
            Assert.AreEqual(jumpClock.T, jumpSave.Current.PlayerBody.Grip.SinceGameSeconds);
            Assert.AreEqual(0d, jump.DailyCredit.CreditedUnits(_plan.Exercise.Id, Hour * 24d * 8d));
        }

        [Test]
        public void HungerSlowsWithoutRefusal_AndPaceIsCaptured()
        {
            _needs.Pace = 0f;
            Start();
            Assert.AreEqual(200d, _service.EndsAtGameSeconds);
            _needs.Pace = 1f;
            Assert.IsFalse(Tick(100d));
            Assert.IsTrue(Tick(200d));
            Assert.AreEqual(30d, _service.LastReceipt.Value.CreditedUnits);
            Assert.AreEqual(0.04f, _needs.Tiredness, 1e-7f);
        }

        [Test]
        public void Cancel_ChargesAndTrainsOnlyElapsedFraction()
        {
            Start();
            Assert.IsTrue(Cancel(25d));
            var r = _service.LastReceipt.Value;
            Assert.IsFalse(r.Completed);
            Assert.AreEqual(7.5d, r.Units);
            Assert.AreEqual(7.5d, r.CreditedUnits);
            Assert.AreEqual(0.005f, r.Tiredness, 1e-7f);
            Assert.AreEqual(25d / Hour, _clean.Hours);
            Assert.IsFalse(Cancel(25d));
            Assert.AreEqual(1, _needs.SpendCalls);
        }

        [Test]
        public void ImmediateCancel_CannotFarmGainsOrCosts()
        {
            for (int i = 0; i < 20; i++) { Start(); Cancel(0d); }
            Assert.AreEqual(0d, _service.DailyCredit.CreditedUnits(_plan.Exercise.Id, 0d));
            Assert.AreEqual(0f, Level(FitnessKind.Grip, 0d));
            Assert.AreEqual(0, _needs.SpendCalls);
            Assert.AreEqual(0, _clean.Calls);
            Assert.AreEqual(0, _reports.Count);
        }

        [TestCase(100d)]
        [TestCase(101d)]
        public void CancelAtOrAfterEnd_IsCompletion(double at)
        { Start(); Cancel(at); Assert.IsTrue(_service.LastReceipt.Value.Completed); Assert.AreEqual(30d, _service.LastReceipt.Value.Units); }

        [Test]
        public void DailyCap_IsSharedAcrossMachinesAndPartialSets_CostsContinue()
        {
            float beforeCap = 0f;
            for (int i = 0; i < 5; i++)
            {
                Start(i * 50d, "rack." + i);
                Cancel((i + 1) * 50d);
                if (i == 3) beforeCap = Level(FitnessKind.Grip, 200d);
            }
            Assert.AreEqual(60d, _service.DailyCredit.CreditedUnits(_plan.Exercise.Id, 250d));
            Assert.AreEqual(0d, _service.LastReceipt.Value.CreditedUnits);
            Assert.AreEqual(15d, _service.LastReceipt.Value.Units);
            Assert.AreEqual(5, _needs.SpendCalls);
            Assert.AreEqual(0.05f, _needs.Tiredness, 1e-7f);
            Assert.AreEqual(beforeCap, Level(FitnessKind.Grip, 250d), "No extra gain past the cap.");
            Assert.AreEqual(5, _reports.Count);
            Assert.AreEqual(15f, _reports[4].Units);
            Assert.AreEqual(30d, _reports[4].Kcal);
        }

        [Test]
        public void SetCrossingDailyCap_ReportsBothParts_AndDoesNotMaintainUncreditedFitness()
        {
            _plan.DailyTrainingUnits = 45f;
            _plan.Exercise.Training = new TrainingDoses { Grip = 1f, Back = 1f, Legs = 1f, Cardio = 1f };
            Start(); Tick(100d);
            Start(100d); Tick(200d);
            Assert.AreEqual(15d, _service.LastReceipt.Value.CreditedUnits);
            Assert.AreEqual(3, _reports.Count);
            Assert.AreEqual(15f, _reports[1].Units);
            Assert.AreEqual(15f, _reports[2].Units);
            Assert.AreEqual(30d, _reports[1].Kcal);
            Assert.AreEqual(30d, _reports[2].Kcal);
            PlayerBodyDto trained = _save.Current.PlayerBody.Clone();
            foreach (FitnessKind kind in StrengthMath.Kinds)
                Assert.AreEqual(FitnessMath.Grow(0f, 0f, 45f,
                    StrengthMath.CurveFor(kind, Settings).GainPerDose, Settings.MemoryRegainFactor),
                    trained.Get(kind).Level, 1e-7f);
            Start(200d); Tick(300d);
            Assert.AreEqual(0d, _service.LastReceipt.Value.CreditedUnits);
            Assert.AreEqual(4, _reports.Count);
            Assert.AreEqual(30f, _reports[3].Units);
            Assert.AreEqual(60d, _reports[3].Kcal);
            foreach (FitnessKind kind in StrengthMath.Kinds)
                Assert.AreEqual(trained.Get(kind), _save.Current.PlayerBody.Get(kind),
                    "Excess effort must not change level, peak, maintenance, pending dose or anchor time.");
            Assert.AreEqual(3, _needs.ExertCalls);
            Assert.AreEqual(3, _clean.Calls);
        }

        [Test]
        public void Settlement_UsesCurrentBodyMass_InsteadOfAStartSnapshot()
        {
            Start();
            _save.Current.PlayerBody.MassKg = _body.MassKg * 2d;
            Tick(100d);
            Assert.AreEqual(1, _reports.Count);
            Assert.AreEqual(120d, _reports[0].Kcal);
            Assert.AreEqual(_reports[0].HungerAdded, _service.LastReceipt.Value.Hunger);
        }

        [Test]
        public void Midnight_UsesEndpointDay_AndOpensAnotherAllowance()
        {
            Start(); Tick(100d);
            Start(100d); Tick(200d);
            double day = Hour * 24d;
            Start(day - 50d); Tick(day + 50d);
            Assert.AreEqual(30d, _service.LastReceipt.Value.CreditedUnits);
            Assert.AreEqual(30d, _service.DailyCredit.CreditedUnits(_plan.Exercise.Id, day + 50d));
        }

        [Test]
        public void DifferentExercise_HasItsOwnDailyAllowance()
        {
            Start(); Tick(100d);
            Start(100d); Tick(200d);
            _plan.Exercise.Id = "activity.other";
            _plan.Equipment.ExerciseIds = new[] { _plan.Exercise.Id };
            Start(200d); Tick(300d);
            Assert.AreEqual(30d, _service.LastReceipt.Value.CreditedUnits);
            Assert.AreEqual(60d, _service.DailyCredit.CreditedUnits("activity.test", 300d));
        }

        [Test]
        public void Fitness_HoldsDecaysAndRegainsUsingExistingCurve()
        {
            Start(); Tick(100d);
            float peak = Level(FitnessKind.Grip, 100d);
            Assert.AreEqual((float)(1d - Math.Exp(-0.0005f * 30d)), peak);
            Assert.AreEqual(peak, Level(FitnessKind.Grip, 100d + Hour * 24d * 3d));
            double later = 100d + Hour * 24d * 4d;
            float faded = Level(FitnessKind.Grip, later);
            Assert.AreEqual(Math.Max(0f, peak - 0.01f), faded, 1e-7f);
            Start(later); Tick(later + 100d);
            float atEnd = Math.Max(0f, peak - 0.01f * (float)(1d + 100d / (Hour * 24d)));
            float expected = FitnessMath.Grow(atEnd, peak, 30f, 0.0005f, 4f);
            Assert.AreEqual(expected, Level(FitnessKind.Grip, later + 100d), 1e-7f);
            Assert.AreEqual(0f, Level(FitnessKind.Cardio, later + 100d));
        }

        [Test]
        public void ActiveSession_CapturesPlan_ButReportsTheAuthoritativeActivity()
        {
            Start();
            Assert.AreEqual(WorkoutStartResult.AlreadyActive, _service.TryStart(_plan, "rack.2", 0d));
            _plan.Units = 999f; _plan.DurationGameMinutes = 999f;
            _plan.TirednessPerGameHour = 1f; _plan.DailyTrainingUnits = 1f;
            _plan.Exercise.Training = new TrainingDoses { Legs = 20f };
            Tick(100d);
            Assert.AreEqual(30d, _service.LastReceipt.Value.Units);
            Assert.AreEqual(30d, _service.LastReceipt.Value.CreditedUnits);
            Assert.AreEqual(0.02f, _service.LastReceipt.Value.Tiredness, 1e-7f);
            Assert.AreEqual(FitnessMath.Grow(0f, 0f, 600f, Settings.Strength.GainPerDose, Settings.MemoryRegainFactor),
                            Level(FitnessKind.Legs, 100d));
            Assert.AreEqual(0f, Level(FitnessKind.Grip, 100d));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1d)]
        public void InvalidTime_RejectsBeforeMutatingSession(double time)
        {
            Start();
            Assert.Throws<ArgumentOutOfRangeException>(() => Tick(time));
            Assert.IsTrue(_service.IsActive);
            Assert.AreEqual(0, _needs.SpendCalls);
            Tick(100d);
            Assert.AreEqual(30d, _service.LastReceipt.Value.CreditedUnits);
        }

        [Test]
        public void RewindIsRejected_RecreateIsExplicitRuntimeReset()
        {
            Start(200d); Tick(250d);
            Assert.Throws<ArgumentOutOfRangeException>(() => Cancel(249d));
            Assert.IsTrue(_service.IsActive);
            Tick(300d);
            Assert.Greater(Level(FitnessKind.Grip, 300d), 0f);
            float savedGrip = _body.FitnessLevel(FitnessKind.Grip);
            _save.Data.PlayerBody = _save.Current.PlayerBody.Clone();
            var reloadedBody = Body(_save, _clock, _needs);
            var fresh = Service(_needs, new Clean(), reloadedBody);
            Assert.AreEqual(savedGrip, reloadedBody.FitnessLevel(FitnessKind.Grip));
            Assert.AreEqual(0d, fresh.DailyCredit.CreditedUnits(_plan.Exercise.Id, 0d));
            Assert.IsFalse(fresh.IsActive);
        }

        [Test]
        public void InvalidPlanBodyAndInstance_AreRefusedWithoutCosts()
        {
            _save.Current.PlayerBody.MassKg = double.PositiveInfinity;
            Assert.AreEqual(WorkoutStartResult.InvalidBody, _service.TryStart(_plan, "rack", 0d));
            _save.Current.PlayerBody.MassKg = PlayerBodyDto.Unset;
            Assert.AreEqual(WorkoutStartResult.InvalidEquipment, _service.TryStart(_plan, " ", 0d));
            _plan.MinimumPace = 0f;
            Assert.AreEqual(WorkoutStartResult.InvalidPlan, _service.TryStart(_plan, "rack", 0d));
            Assert.IsFalse(_service.IsActive);
            Assert.AreEqual(0, _needs.SpendCalls);
        }

        [Test]
        public void BodyMassScalesHunger_ButNotTraining()
        {
            Start(); Tick(100d);
            var needs = new Needs(); var save = new MemorySave(); var clock = new Clock();
            save.Current.PlayerBody.MassKg = _body.MassKg * 2d;
            var body = Body(save, clock, needs);
            var other = Service(needs, new Clean(), body);
            Assert.AreEqual(WorkoutStartResult.Started, other.TryStart(_plan, "rack", 0d));
            clock.T = 100d;
            other.Tick(100d);
            Assert.AreEqual(_needs.Hunger * 2f, needs.Hunger);
            Assert.AreEqual(Level(FitnessKind.Grip, 100d), body.FitnessLevel(FitnessKind.Grip));
        }

        [Test]
        public void InvalidConfigurationAndOverflow_CannotStartOrCharge()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorkoutService(_needs, _clean,
                BodySettings.Default, NeedsSettings.Default, Settings, double.PositiveInfinity, _body));
            var bad = Settings; bad.Strength.GainPerDose = float.NaN;
            Assert.Throws<ArgumentException>(() => new WorkoutService(_needs, _clean,
                BodySettings.Default, NeedsSettings.Default, bad, Hour, _body));
            _save.Current.PlayerBody.MassKg = double.MaxValue;
            Assert.AreEqual(WorkoutStartResult.InvalidPlan, _service.TryStart(_plan, "rack", 0d));
            _save.Current.PlayerBody.MassKg = PlayerBodyDto.Unset;
            _plan.Exercise.Training = new TrainingDoses { Grip = float.MaxValue };
            Assert.AreEqual(WorkoutStartResult.InvalidPlan, _service.TryStart(_plan, "rack", 0d));
            Assert.AreEqual(0, _needs.SpendCalls);
        }

        private void OnActivity(ActivityReported report) => _reports.Add(report);

        private bool Tick(double at) { _clock.T = at; return _service.Tick(at); }
        private bool Cancel(double at) { _clock.T = at; return _service.Cancel(at); }
        private float Level(FitnessKind kind, double at)
        {
            _clock.T = at;
            return _body.FitnessLevel(kind);
        }

        private static BodyService Body(MemorySave save, Clock clock, INeedsService needs) => new BodyService(
            () => save, () => clock, () => BodySettings.Default, () => NeedsSettings.Default,
            () => needs, () => null, () => Hour, () => Settings, () => null);

        private sealed class MemorySave : ISaveService
        {
            public SaveData Data = new SaveData { PlayerBody = new PlayerBodyDto() };
            public SaveData Current => Data;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() => Assert.Fail("Workouts must not write a save file.");
        }

        private sealed class Clock : IGameClock
        {
            public double T;
            public double TotalSeconds => T;
            public GameTime Now => new GameTime(T);
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => (int)(T / (Hour * 24d));
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
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
