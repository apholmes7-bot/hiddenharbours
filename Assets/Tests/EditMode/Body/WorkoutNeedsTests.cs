using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WorkoutNeedsTests
    {
        private MemorySave _save;
        private Clock _clock;
        private NeedsService _needs;
        private CleanlinessService _clean;
        private NeedsSettings _settings;
        private CleanlinessSettings _dirt;
        private readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();
        private const double Hour = 600d;

        [SetUp]
        public void SetUp()
        {
            _save = new MemorySave(); _clock = new Clock();
            _settings = NeedsSettings.Default;
            _settings.Hunger.RisePerGameHour = 0.1f;
            _settings.Tiredness.RisePerGameHour = 0.05f;
            _settings.Tiredness.MildAt = 0.4f;
            _settings.Tiredness.StrongAt = 0.8f;
            _dirt = CleanlinessSettings.Default;
            _dirt.Curve.RisePerGameHour = 0.02f;
            _dirt.Sources = new[] { new DirtSourceRate(DirtActivity.Gym, 0.15f, 0.9f) };
            _save.Current.PlayerNeeds = PlayerNeedsDto.FedAndRestedAt(0d);
            _save.Current.PlayerCleanliness = PlayerCleanlinessDto.CleanAt(0d);
            _clean = new CleanlinessService(() => _save, () => _clock, () => _dirt,
                                            () => Array.Empty<WashStationDef>(), () => Hour);
            _needs = new NeedsService(() => _save, () => _clock, () => _settings, () => null, () => Hour,
                                      () => null, () => _clean, () => _dirt);
        }

        [Test]
        public void Exert_SettlesPassiveRise_ClampsAndAnnouncesStage()
        {
            int changed = 0;
            Action<NeedStageChanged> listener = e => { if (e.Need == NeedKind.Tiredness) changed++; };
            EventBus.Subscribe(listener);
            try
            {
                _clock.T = Hour * 2d;
                _needs.Exert(0.35f);
                Assert.AreEqual(0.45f, _needs.Level(NeedKind.Tiredness), 1e-6f);
                Assert.AreEqual(1, changed);
                _needs.Exert(1f);
                Assert.AreEqual(1f, _needs.Level(NeedKind.Tiredness));
                Assert.AreEqual(2, changed);
                Assert.AreEqual(0.2f, _needs.Level(NeedKind.Hunger), 1e-6f);
                Assert.AreEqual(0, _save.Writes);
            }
            finally { EventBus.Unsubscribe(listener); }
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        [TestCase(0f)]
        public void InvalidExertion_DoesNothing(float amount)
        {
            _needs.Exert(amount);
            Assert.AreEqual(0f, _needs.Level(NeedKind.Tiredness));
            Assert.AreEqual(0d, _save.Current.PlayerNeeds.Tiredness.SinceGameSeconds);
        }

        [Test]
        public void BoundedSweat_UsesHoursNotActions_AndLeavesNoLiveStretch()
        {
            _clock.T = Hour;
            _clean.ReportActivity(DirtActivity.Gym, 0.5d);
            Assert.AreEqual(0.095f, _clean.Dirt, 1e-6f);
            Assert.AreEqual(DirtActivity.None, _clean.Activity);
            _clock.T += Hour;
            Assert.AreEqual(0.115f, _clean.Dirt, 1e-6f);
            _clean.ReportActivity(DirtActivity.Gym, 100d);
            Assert.AreEqual(1f, _clean.Dirt);
            Assert.AreEqual(0, _save.Writes);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1d)]
        [TestCase(0d)]
        public void InvalidSweatDuration_DoesNothing(double hours)
        {
            _clean.ReportActivity(DirtActivity.Gym, hours);
            Assert.AreEqual(0f, _clean.Dirt);
            Assert.AreEqual(0d, _save.Current.PlayerCleanliness.SinceGameSeconds);
        }

        [Test]
        public void MissingSave_IsHarmless()
        {
            _save.Data = null;
            Assert.DoesNotThrow(() => _needs.Exert(0.2f));
            Assert.DoesNotThrow(() => _clean.ReportActivity(DirtActivity.Gym, 1d));
            Assert.AreEqual(0, _save.Writes);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object obj in _made) UnityEngine.Object.DestroyImmediate(obj);
            _made.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        { T obj = ScriptableObject.CreateInstance<T>(); _made.Add(obj); return obj; }

        private BodyService Body() => new BodyService(() => _save, () => _clock,
            () => BodySettings.Default, () => _settings, () => _needs, () => null,
            () => Hour, () => StrengthSettings.Default, () => null);

        private WorkoutDef Plan()
        {
            var plan = Make<WorkoutDef>();
            plan.Id = "workout.integration";
            plan.Equipment = Make<GymEquipmentDef>(); plan.Equipment.Id = "gym_equipment.integration";
            plan.Exercise = Make<ActivityDef>(); plan.Exercise.Id = "activity.integration";
            plan.Exercise.Kind = ActivityKind.Exercise; plan.Exercise.Unit = ActivityUnit.Repetition;
            plan.Exercise.KcalPerUnitAboveRest = 20f;
            plan.Exercise.Training = new TrainingDoses { Grip = 1f, Back = 1f, Legs = 1f, Cardio = 1f };
            plan.Exercise.EquipmentIds = new[] { plan.Equipment.Id };
            plan.Equipment.ExerciseIds = new[] { plan.Exercise.Id };
            plan.DurationGameMinutes = 10f; plan.Units = 30f; plan.DailyTrainingUnits = 30f;
            plan.MinimumPace = 0.5f; plan.TirednessPerGameHour = 0.12f;
            return plan;
        }

        private WorkoutService Workout(IBodyService body) => new WorkoutService(_needs, _clean,
            BodySettings.Default, _settings, StrengthSettings.Default, Hour, body);

        [Test]
        public void Workout_TrainsAllSavedAnchors_AndRecreatingServicesPreservesPlayerBenefits()
        {
            BodyService body = Body(); WorkoutDef plan = Plan(); WorkoutService workout = Workout(body);
            float rod = body.RodStrength, pull = body.PullStrength, carry = body.CarryCapacityKg;
            Assert.AreEqual(WorkoutStartResult.Started, workout.TryStart(plan, "rack", 0d));
            _clock.T = workout.EndsAtGameSeconds;
            Assert.IsTrue(workout.Tick(_clock.T));
            PlayerBodyDto saved = _save.Current.PlayerBody.Clone();
            foreach (FitnessKind kind in StrengthMath.Kinds)
            {
                Assert.Greater(saved.Get(kind).Level, 0f);
                Assert.AreEqual(_clock.T, saved.Get(kind).SinceGameSeconds);
            }
            _save.Current.PlayerBody = saved.Clone();
            BodyService reloaded = Body(); WorkoutService fresh = Workout(reloaded);
            foreach (FitnessKind kind in StrengthMath.Kinds)
                Assert.AreEqual(body.FitnessLevel(kind), reloaded.FitnessLevel(kind));
            Assert.Greater(reloaded.RodStrength, rod);
            Assert.Greater(reloaded.PullStrength, pull);
            Assert.Greater(reloaded.CarryCapacityKg, carry);
            Assert.Greater(reloaded.PlayerMultiplier(TraitStat.WalkSpeed), 1f);
            Assert.AreEqual(0d, fresh.DailyCredit.CreditedUnits(plan.Exercise.Id, _clock.T));
            Assert.IsFalse(fresh.IsActive);
            Assert.AreEqual(0, _save.Writes);
        }

        [TestCase(false, 0.01d)]
        [TestCase(true, 0.01d)]
        [TestCase(false, 1d)]
        [TestCase(true, 1d)]
        public void HungryWorkout_PaysOverflowFromMass_EvenPastCap_AndStopsAtFloor(bool pastCap, double massAboveFloor)
        {
            BodyService body = Body(); WorkoutDef plan = Plan(); WorkoutService workout = Workout(body);
            if (pastCap)
            {
                Assert.AreEqual(WorkoutStartResult.Started, workout.TryStart(plan, "rack", 0d));
                _clock.T = workout.EndsAtGameSeconds; workout.Tick(_clock.T);
            }
            _save.Current.PlayerNeeds.Hunger = new NeedStateDto(1f, _clock.T);
            double floor = body.FloorMassKg;
            double initialMass = floor + massAboveFloor;
            _save.Current.PlayerBody.MassKg = initialMass;
            _save.Current.PlayerBody.MassSinceGameSeconds = _clock.T;
            BodySettings settings = BodySettings.Default;
            double kcal = StrengthMath.ActivityKcal(plan.Exercise, plan.Units, initialMass,
                body.CardioLevel, settings, StrengthSettings.Default);
            double kcalPerPoint = BodyMath.KcalPerHungerUnit(_settings.Hunger.RisePerGameHour, settings);
            // BodyService rounds hunger to float; Spend adds to full hunger and returns float overflow.
            float hunger = (float)(kcal / kcalPerPoint);
            float fullHunger = (float)(1f + hunger);
            float overflow = (float)(fullHunger - 1f);
            double expectedMass = Math.Max(floor, initialMass - overflow * kcalPerPoint / settings.KcalPerKgOfMass);
            var reports = new List<ActivityReported>();
            Action<ActivityReported> listener = e => reports.Add(e);
            EventBus.Subscribe(listener);
            try
            {
                Assert.AreEqual(WorkoutStartResult.Started, workout.TryStart(plan, "rack", _clock.T),
                    "Full hunger slows a set but never refuses it.");
                // Match the float returned by NeedsMath.Factor / NeedsService.PlayerMultiplier
                // before double division; an inline subtraction can retain extra precision.
                float pace = (float)(1f - _settings.Hunger.StrongWalkPenalty);
                Assert.AreEqual(_clock.T + 100d / pace,
                    workout.EndsAtGameSeconds, 1e-9d);
                // Report at the same body clock to isolate effort overflow from passive starvation.
                Assert.IsTrue(workout.Tick(workout.EndsAtGameSeconds));
                if (massAboveFloor == 0.01d)
                    Assert.AreEqual(floor, body.MassKg, "The double mass must clamp exactly to its floor.");
                else
                {
                    Assert.Greater(expectedMass, floor, "Keep a case where the floor cannot mask a wrong burn rate.");
                    // At this fixture's 56-58 kg, a float ulp is 2^-18 kg. Allow less than three
                    // ulps for float hunger intermediates; the expected burn is about 0.33 kg.
                    Assert.AreEqual(expectedMass, body.MassKg, 1e-5d,
                        "The training cap must not change the mass paid for the same effort.");
                }
                Assert.GreaterOrEqual(body.MassKg, floor);
                Assert.AreEqual(1f, _needs.Level(NeedKind.Hunger));
                Assert.AreEqual(pastCap ? 0d : 30d, workout.LastReceipt.Value.CreditedUnits);
                Assert.AreEqual(1, reports.Count);
                Assert.AreEqual(plan.Exercise.Id, reports[0].ActivityId);
                Assert.AreEqual(30f, reports[0].Units);
                Assert.Greater(reports[0].Kcal, 0d);
                Assert.AreEqual(reports[0].HungerAdded, workout.LastReceipt.Value.Hunger);
                Assert.AreEqual(0, _save.Writes);
            }
            finally { EventBus.Unsubscribe(listener); }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BodyEffort_UsesCurrentCardio_AlsoWhenTrainingIsDisabled(bool train)
        {
            BodyService body = Body(); WorkoutDef plan = Plan();
            // Current balance discounts Movement only, not Exercise. Both share this one effort path.
            plan.Exercise.Kind = ActivityKind.Movement;
            _save.Current.PlayerBody = new PlayerBodyDto
            {
                Cardio = new FitnessStateDto { Level = 0.8f, Peak = 0.8f, SinceGameSeconds = 0d,
                    MaintainedAtGameSeconds = 0d },
            };
            FitnessStateDto before = _save.Current.PlayerBody.Cardio;
            double expected = StrengthMath.ActivityKcal(plan.Exercise, 30f, body.MassKg, body.CardioLevel,
                BodySettings.Default, StrengthSettings.Default);
            double untrained = StrengthMath.ActivityKcal(plan.Exercise, 30f, body.MassKg, 0f,
                BodySettings.Default, StrengthSettings.Default);
            Assert.Less(expected, untrained);
            IBodyService effort = body;
            Assert.AreEqual(expected, effort.ReportActivity(plan.Exercise, 30f, train));
            if (train) Assert.Greater(body.CardioLevel, before.Level);
            else Assert.AreEqual(before, _save.Current.PlayerBody.Cardio);
        }

        private sealed class MemorySave : ISaveService
        {
            public SaveData Data = new SaveData();
            public SaveData Current => Data;
            public int Writes;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() => Writes++;
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
    }
}
