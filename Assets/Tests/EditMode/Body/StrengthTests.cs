using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Body slice 2 (owner asks 2026-11-15: lifting capacity, carry speed by strength, muscle memory, a gym,
    /// cardio; and "work related tasks burn energy faster"). The numbers are the shipped code defaults
    /// (<see cref="StrengthSettings.Default"/>, <see cref="BodySettings.Default"/>), never the asset, so the
    /// owner retuning <see cref="GameConfig.Strength"/> breaks nothing here; activities are made in-test so
    /// retuning an <see cref="ActivityDef"/> asset breaks nothing either. One game hour is <see cref="Hour"/>
    /// clock seconds.
    /// </summary>
    public class StrengthTests
    {
        private const double Hour = 100d;
        private const double Day = 24d * Hour;

        private TraitFixtures.MemorySave _save;
        private StrengthClock _clock;
        private NeedsSettings _needsSettings;
        private BodySettings _bodySettings;
        private StrengthSettings _strength;
        private FixedLoad _load;
        private NeedsService _needs;
        private BodyService _body;

        private readonly List<ActivityReported> _reports = new List<ActivityReported>();
        private readonly List<Object> _made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<ActivityReported>();
            EventBus.Clear<NeedStageChanged>();
            EventBus.Subscribe<ActivityReported>(OnReported);
            _reports.Clear();

            _save = new TraitFixtures.MemorySave();
            _clock = new StrengthClock();
            _needsSettings = new NeedsSettings
            {
                Hunger = new NeedCurve
                {
                    RisePerGameHour = 0.1f, MildAt = 0.4f, StrongAt = 0.8f,
                    MildWalkPenalty = 0.05f, StrongWalkPenalty = 0.12f,
                },
                Tiredness = new NeedCurve { RisePerGameHour = 0.05f, MildAt = 0.5f, StrongAt = 0.9f },
                WalkFloor = 0.8f, SellFloor = 0.9f, TickGameMinutes = 5f,
            };
            _bodySettings = BodySettings.Default;
            _strength = StrengthSettings.Default;
            _load = new FixedLoad();
            (_needs, _body) = Wire(_save);
            _save.Current.PlayerNeeds.Hunger = new NeedStateDto(0f, 0d);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<ActivityReported>(OnReported);
            EventBus.Clear<ActivityReported>();
            EventBus.Clear<NeedStageChanged>();
            EventBus.Clear<ControlModeChanged>();
            for (int i = 0; i < _made.Count; i++) Object.DestroyImmediate(_made[i]);
            _made.Clear();
            ActivityCatalog.Reset();
            GameServices.Reset();
        }

        private void OnReported(ActivityReported e) => _reports.Add(e);

        private (NeedsService, BodyService) Wire(ISaveService save)
        {
            BodyService body = null;
            NeedsService needs = new NeedsService(() => save, () => _clock, () => _needsSettings, () => null,
                                                  () => Hour, () => body);
            body = new BodyService(() => save, () => _clock, () => _bodySettings, () => _needsSettings,
                                   () => needs, () => new MessRegistry(() => save), () => Hour,
                                   () => _strength, () => _load);
            return (needs, body);
        }

        private ActivityDef Activity(string id, ActivityKind kind, float kcal, float grip = 0f, float back = 0f,
                                     float legs = 0f, float cardio = 0f, ActivityUnit unit = ActivityUnit.Minute)
        {
            var def = ScriptableObject.CreateInstance<ActivityDef>();
            def.Id = id;
            def.Kind = kind;
            def.Unit = unit;
            def.KcalPerUnitAboveRest = kcal;
            def.Training = new TrainingDoses { Grip = grip, Back = back, Legs = legs, Cardio = cardio };
            _made.Add(def);
            return def;
        }

        // ---- growth, maintenance, memory, fade ------------------------------------------------------

        [Test]
        public void Gaining_CostsFarMoreThanMaintaining()
        {
            FitnessCurve c = _strength.Strength;
            foreach (float level in new[] { 0.1f, 0.3f, 0.5f, 0.7f })
            {
                double toGain = FitnessMath.DosesToGain(level, level + 0.05f, c.GainPerDose);
                Assert.Greater(toGain, 5d * c.MaintainDose,
                    $"at {level}: five points of growth cost far more than one grace's upkeep");
            }

            // Service: train legs to a level, then keep it up with the bare maintaining dose each grace.
            ActivityDef squat = Activity("activity.test_squat", ActivityKind.Exercise, 0.6f, legs: 1f,
                                         unit: ActivityUnit.Repetition);
            for (int session = 0; session < 20; session++)
            {
                _body.ReportActivity(squat, 200f);
                _clock.T += Day;
            }
            float trained = _body.FitnessLevel(FitnessKind.Legs);
            Assert.Greater(trained, 0.5f, "twenty heavy sessions train a lot");
            for (int week = 0; week < 20; week++)
            {
                _body.ReportActivity(squat, c.MaintainDose);
                _clock.T += c.GraceGameDays * Day;
            }
            Assert.GreaterOrEqual(_body.FitnessLevel(FitnessKind.Legs), trained,
                "the bare upkeep dose, once a grace, holds the level for sixty days");
        }

        [Test]
        public void RegainToThePeak_IsFasterByTheMemoryFactor()
        {
            FitnessCurve c = _strength.Strength;
            float r = _strength.MemoryRegainFactor;
            double firstTime = FitnessMath.DosesToGain(0.3f, 0.6f, c.GainPerDose);
            float regained = FitnessMath.Grow(0.3f, 0.6f, (float)(firstTime / r), c.GainPerDose, r);
            Assert.AreEqual(0.6f, regained, 1e-4f, "below the old peak, 1/R of the doses gets back to it");
            float fresh = FitnessMath.Grow(0.3f, 0.3f, (float)(firstTime / r), c.GainPerDose, r);
            Assert.Less(fresh, 0.45f, "the same doses with no memory get well short");
            Assert.AreEqual(0.6f, FitnessMath.Grow(0.3f, 0.3f, (float)firstTime, c.GainPerDose, r), 1e-4f,
                "first-time gain takes the full doses");

            float past = FitnessMath.Grow(0.3f, 0.6f, (float)(firstTime / r + 100d), c.GainPerDose, r);
            float plain = FitnessMath.Grow(0.6f, 0.6f, 100f, c.GainPerDose, r);
            Assert.AreEqual(plain, past, 1e-4f, "past the peak, training is first-time again");
        }

        [Test]
        public void Fade_HoldsForTheGrace_ThenFallsAtTheRate_NeverBelowZero_PeakKept()
        {
            FitnessCurve c = _strength.Strength;
            var state = FitnessStateDto.Untrained;
            FitnessMath.Train(ref state, 2000f, 0d, c, _strength.MemoryRegainFactor, Day);
            float level = state.Level;
            Assert.AreEqual(0d, state.MaintainedAtGameSeconds, "a big dose counts as keeping it up");

            Assert.AreEqual(level, FitnessMath.LevelAt(state, c, c.GraceGameDays * Day, Day), "held through the grace");
            float oneDay = FitnessMath.LevelAt(state, c, (c.GraceGameDays + 1f) * Day, Day);
            Assert.AreEqual(level - c.DecayPerGameDay, oneDay, 1e-5f, "then the decay per day");
            Assert.AreEqual(0f, FitnessMath.LevelAt(state, c, 10000d * Day, Day), "never below zero");

            FitnessMath.Settle(ref state, c, 10000d * Day, Day);
            Assert.AreEqual(0f, state.Level);
            Assert.AreEqual(level, state.Peak, "the peak never fades: muscle memory");
        }

        [Test]
        public void Fade_SteppedThroughSettles_EqualsOneCatchUp()
        {
            FitnessCurve c = _strength.Cardio;
            var stepped = FitnessStateDto.Untrained;
            FitnessMath.Train(ref stepped, 1500f, 0d, c, _strength.MemoryRegainFactor, Day);
            FitnessStateDto once = stepped;
            for (int h = 1; h <= 24 * 20; h++) FitnessMath.Settle(ref stepped, c, h * Hour, Day);
            Assert.AreEqual(FitnessMath.LevelAt(once, c, 20d * Day, Day), stepped.Level, 1e-4f);
        }

        [Test]
        public void SeekRebase_KeepsTheLevel_AndTheGraceLeft()
        {
            FitnessCurve c = _strength.Strength;
            var state = FitnessStateDto.Untrained;
            FitnessMath.Train(ref state, 2000f, 5d * Hour, c, _strength.MemoryRegainFactor, Day);
            FitnessMath.Settle(ref state, c, Day, Day);
            float level = state.Level;
            FitnessMath.Rebase(ref state, 900d * Day);
            Assert.AreEqual(level, FitnessMath.LevelAt(state, c, 900d * Day, Day));
            double graceLeft = c.GraceGameDays * Day + 5d * Hour - Day;
            Assert.AreEqual(level, FitnessMath.LevelAt(state, c, 900d * Day + graceLeft - 1d, Day), "grace kept");
            Assert.Less(FitnessMath.LevelAt(state, c, 900d * Day + graceLeft + Day, Day), level);
        }

        // ---- lifting and carrying -------------------------------------------------------------------

        [Test]
        public void BiggerFrame_LiftsMore_AtEqualTraining()
        {
            BodySettings s = _bodySettings;
            foreach (float level in new[] { 0f, 0.5f, 1f })
            {
                float previous = 0f;
                foreach ((float frame, float height) in new[] { (0f, s.MinHeightScale), (0.5f, 1f), (1f, s.MaxHeightScale) })
                {
                    double lean = BodyMath.LeanMassKg(frame, height, s);
                    float strength = StrengthMath.StrengthFactor(lean, frame, level, s, _strength);
                    float capacity = StrengthMath.CarryCapacityKg(strength, strength, _strength);
                    Assert.Greater(capacity, previous, $"frame {frame} at level {level} carries more");
                    previous = capacity;
                }
            }
        }

        [Test]
        public void CarrySpeed_FallsWithLoad_RisesWithStrength_AndIsFloored()
        {
            float previous = 2f;
            for (float kg = 0f; kg <= 200f; kg += 5f)
            {
                float walk = StrengthMath.CarryWalkFactor(kg, 30f, _strength);
                Assert.LessOrEqual(walk, previous, $"{kg} kg is no faster than less");
                Assert.GreaterOrEqual(walk, StrengthMath.MinimumCarryFloor, "carrying slows, never stops");
                previous = walk;
            }
            Assert.AreEqual(1f, StrengthMath.CarryWalkFactor(0f, 30f, _strength), "empty hands: today's walk");
            previous = 0f;
            for (float capacity = 10f; capacity <= 80f; capacity += 5f)
            {
                float walk = StrengthMath.CarryWalkFactor(20f, capacity, _strength);
                Assert.GreaterOrEqual(walk, previous, $"stronger ({capacity} kg capacity) carries 20 kg faster");
                previous = walk;
            }

            // Through the service: a load slows the walk, training legs and back gives some of it back.
            float empty = _body.PlayerMultiplier(TraitStat.WalkSpeed);
            _load.Kg = 25f;
            float loaded = _body.PlayerMultiplier(TraitStat.WalkSpeed);
            Assert.Less(loaded, empty);
            ActivityDef deadlift = Activity("activity.test_deadlift", ActivityKind.Exercise, 0.7f, back: 1.5f,
                                            legs: 1f, unit: ActivityUnit.Repetition);
            for (int i = 0; i < 10; i++) _body.ReportActivity(deadlift, 100f);
            Assert.Greater(_body.PlayerMultiplier(TraitStat.WalkSpeed), loaded, "stronger, the same load slows less");
            Assert.Greater(_body.CarryCapacityKg, _strength.ReferenceCarryCapacityKg);
        }

        // ---- cardio ---------------------------------------------------------------------------------

        [Test]
        public void Cardio_RisesWithMovement_FallsWhenSedentary_AndMakesMovingCheaper()
        {
            ActivityDef walk = Activity("activity.test_walk", ActivityKind.Movement, 2.9f, legs: 0.05f, cardio: 0.5f);
            ActivityDef drive = Activity("activity.test_drive", ActivityKind.Sedentary, 0.35f);
            double untrainedKcal = StrengthMath.ActivityKcal(walk, 60f, _body.MassKg, 0f, _bodySettings, _strength);

            for (int d = 0; d < 14; d++)
            {
                _body.ReportActivity(walk, 180f);
                _clock.T += Day;
            }
            float fit = _body.CardioLevel;
            Assert.Greater(fit, 0f, "walking every day raises cardio");
            Assert.Greater(_body.PlayerMultiplier(TraitStat.WalkSpeed), 1f, "and the walk is a little quicker");
            Assert.Less(StrengthMath.ActivityKcal(walk, 60f, _body.MassKg, fit, _bodySettings, _strength), untrainedKcal,
                "and moving costs less");

            for (int d = 0; d < 30; d++)
            {
                _body.ReportActivity(drive, 240f);
                _clock.T += Day;
            }
            Assert.Less(_body.CardioLevel, fit, "a month of only driving lets it fall");
            Assert.GreaterOrEqual(_body.CardioLevel, 0f);
        }

        // ---- energy ---------------------------------------------------------------------------------

        [Test]
        public void Workout_RaisesHungerMore_ThanTheSameTimeSedentary()
        {
            ActivityDef skipping = Activity("activity.test_skipping", ActivityKind.Exercise, 11.7f, legs: 0.4f, cardio: 1.6f);
            ActivityDef drive = Activity("activity.test_drive", ActivityKind.Sedentary, 0.35f);
            (NeedsService idleNeeds, BodyService idleBody) = Wire(_save2 = new TraitFixtures.MemorySave());
            _save2.Current.PlayerNeeds.Hunger = new NeedStateDto(0f, 0d);

            _body.ReportActivity(skipping, 30f);
            idleBody.ReportActivity(drive, 30f);
            _clock.T += 0.5d * Hour;
            Assert.Greater(_needs.Level(NeedKind.Hunger), idleNeeds.Level(NeedKind.Hunger));
            Assert.AreEqual(2, _reports.Count);
            Assert.Greater(_reports[0].Kcal, _reports[1].Kcal);
        }

        private TraitFixtures.MemorySave _save2;

        [Test]
        public void ADayOfHauling_NeedsMoreFood_ThanAnIdleDay_ForTheSameBody()
        {
            ActivityDef haul = Activity("activity.test_haul_pot", ActivityKind.Work, 5.9f, grip: 0.5f, back: 1f);
            (NeedsService idleNeeds, BodyService idleBody) = Wire(_save2 = new TraitFixtures.MemorySave());
            _save2.Current.PlayerNeeds.Hunger = new NeedStateDto(0f, 0d);

            for (int h = 0; h < 4; h++)
            {
                _body.ReportActivity(haul, 40f);
                _clock.T += Hour;
            }
            float worked = _needs.Level(NeedKind.Hunger), idle = idleNeeds.Level(NeedKind.Hunger);
            Assert.AreEqual(0.4f, idle, 1e-5f, "the idle day is exactly the configured rise");
            Assert.Greater(worked, idle, "the hauling day is hungrier");
            Assert.Greater(_body.FitnessLevel(FitnessKind.Back), 0f, "and the same work trains the back");
            Assert.AreEqual(0f, idleBody.FitnessLevel(FitnessKind.Back));
        }

        [Test]
        public void EffortPastFullHunger_BurnsMass_NeverBelowTheFloor()
        {
            ActivityDef haul = Activity("activity.test_haul_pot", ActivityKind.Work, 5.9f, back: 1f);
            _save.Current.PlayerNeeds.Hunger = new NeedStateDto(1f, 0d);
            double before = _body.MassKg;
            _body.ReportActivity(haul, 60f);
            Assert.Less(_body.MassKg, before, "starving work comes off the body");
            for (int i = 0; i < 2000; i++) _body.ReportActivity(haul, 240f);
            Assert.AreEqual(_body.FloorMassKg, _body.MassKg, 1e-9, "never below the frame's floor");
            Assert.AreEqual(1f, _needs.Level(NeedKind.Hunger));
        }

        // ---- today's player -------------------------------------------------------------------------

        [Test]
        public void DefaultBody_UntrainedAndEmptyHanded_IsTodaysPlayerExactly()
        {
            Assert.AreEqual(1f, _body.RodStrength);
            Assert.AreEqual(1f, _body.PullStrength);
            Assert.AreEqual(1f, _body.LegStrength);
            Assert.AreEqual(_strength.ReferenceCarryCapacityKg, _body.CarryCapacityKg);
            Assert.AreEqual(0f, _body.CardioLevel);
            CollectionAssert.AreEqual(System.BitConverter.GetBytes(1f),
                                      System.BitConverter.GetBytes(_body.PlayerMultiplier(TraitStat.WalkSpeed)));

            // The idle hunger is the configured line: no work reported, no change.
            _clock.T = 3d * Hour;
            Assert.AreEqual(0.3f, _needs.Level(NeedKind.Hunger), 1e-6f);
            Assert.AreEqual(0d, _body.ReportActivity(null, 10f));
            Assert.AreEqual(0d, _body.ReportActivity(Activity("activity.test", ActivityKind.Work, 5f), 0f));
            Assert.AreEqual(0.3f, _needs.Level(NeedKind.Hunger), 1e-6f);

            Assert.AreEqual(1f, NeutralStrength.Instance.RodStrength);
            Assert.AreSame(GameServices.Body, GameServices.PlayerStrength, "the body is the default strength reader");
            Assert.AreEqual(0f, GameServices.CarriedLoad.CarriedKg, "nothing carried by default");
        }

        [Test]
        public void NoSave_DoesNothing()
        {
            var none = new FixedSave(null);
            var body = new BodyService(() => none, () => _clock, () => _bodySettings, () => _needsSettings,
                                       () => null, () => new MessRegistry(() => none), () => Hour,
                                       () => _strength, () => _load);
            Assert.AreEqual(0d, body.ReportActivity(Activity("activity.test", ActivityKind.Work, 5f, back: 1f), 60f));
            Assert.AreEqual(1f, body.PullStrength);
            Assert.AreEqual(0f, body.FitnessLevel(FitnessKind.Back));
        }

        // ---- the signal listener --------------------------------------------------------------------

        [Test]
        public void Listener_CountsADrivingStretch_InGameMinutes_Capped_AndCanBeSwitchedOff()
        {
            ActivityDef drive = Activity("activity.test_drive", ActivityKind.Sedentary, 0.35f);
            var recorder = new RecordingBody();
            var listener = new ActivitySignalListener(() => recorder, () => _clock, () => _strength,
                                                      sig => sig == ActivitySignal.Driving ? drive : null, () => Day);
            listener.Subscribe();
            try
            {
                EventBus.Publish(new ControlModeChanged(ControlMode.Driving));
                _clock.T += 0.5d * Hour;
                EventBus.Publish(new ControlModeChanged(ControlMode.Driving));   // a repeat changes nothing
                _clock.T += 0.5d * Hour;
                EventBus.Publish(new ControlModeChanged(ControlMode.OnFoot));
                Assert.AreEqual(1, recorder.Calls.Count);
                Assert.AreSame(drive, recorder.Calls[0].def);
                Assert.AreEqual(60f, recorder.Calls[0].units, 1e-3f, "an hour at the wheel is 60 minutes");

                EventBus.Publish(new ControlModeChanged(ControlMode.Driving));
                _clock.T += 3d * Day;
                EventBus.Publish(new ControlModeChanged(ControlMode.OnFoot));
                Assert.AreEqual(_strength.MaxSignalWindowGameMinutes, recorder.Calls[1].units, 1e-3f, "capped");

                _strength.ReportWorkFromSignals = false;
                EventBus.Publish(new ControlModeChanged(ControlMode.Driving));
                _clock.T += Hour;
                EventBus.Publish(new ControlModeChanged(ControlMode.OnFoot));
                Assert.AreEqual(2, recorder.Calls.Count, "switched off: nothing counted");
            }
            finally
            {
                listener.Unsubscribe();
            }
            EventBus.Publish(new ControlModeChanged(ControlMode.Driving));
            _clock.T += Hour;
            EventBus.Publish(new ControlModeChanged(ControlMode.OnFoot));
            Assert.AreEqual(2, recorder.Calls.Count, "unsubscribed: nothing counted");
        }

        [Test]
        public void ShippedAsset_CarriesTheStrengthBlock()
        {
            string path = System.IO.Path.Combine(Application.dataPath, "_Project/Data/Config/GameConfig.asset");
            string text = System.IO.File.ReadAllText(path);
            StringAssert.Contains("\n  Strength:", text);
            foreach (string field in new[]
                     {
                         "MemoryRegainFactor", "MassExponent", "SmallFrameTrainableGain", "LargeFrameTrainableGain",
                         "ReferenceCarryCapacityKg", "CarrySlowAtCapacity", "CarryWalkFloor", "CardioSpeedGainMax",
                         "CardioMovementSavingMax", "ReportWorkFromSignals", "MaxSignalWindowGameMinutes",
                     })
                StringAssert.Contains("    " + field + ":", text, field);
            foreach (string field in new[] { "GainPerDose", "DecayPerGameDay", "GraceGameDays", "MaintainDose" })
                StringAssert.Contains("      " + field + ":", text, field);
        }

        // ---- fakes -----------------------------------------------------------------------------------

        private sealed class FixedLoad : ICarriedLoad
        {
            public float Kg;
            public float CarriedKg => Kg;
        }

        private sealed class FixedSave : ISaveService
        {
            public FixedSave(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private sealed class RecordingBody : IBodyService
        {
            public readonly List<(ActivityDef def, float units)> Calls = new List<(ActivityDef, float)>();
            public float Frame => 0.5f;
            public float HeightScale => 1f;
            public bool ShapeChosen => false;
            public double MassKg => 0d;
            public double LeanMassKg => 0d;
            public double MaxMassKg => 0d;
            public double FloorMassKg => 0d;
            public double DailyKcal => 0d;
            public float HungerRateScale => 1f;
            public float StomachMeals => 0f;
            public float PlayerMultiplier(TraitStat stat) => 1f;
            public bool ChooseShape(float frame, float heightScale) => false;
            public MealOutcome Eat(float meals, Vector2 position, string regionId) => MealOutcome.Nothing;
            public void Settle() { }
            public double ReportActivity(ActivityDef activity, float units)
            {
                Calls.Add((activity, units));
                return 0d;
            }
        }

        private sealed class StrengthClock : IGameClock
        {
            public double T;
            public double TotalSeconds => T;
            public GameTime Now => new GameTime(T);
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => 0;
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
