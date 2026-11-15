using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Body slice 1 (owner ask 2026-11-15: frame, weight, metabolism, overeating). The body numbers are the
    /// shipped code default (<see cref="BodySettings.Default"/>), never the asset, so the owner retuning
    /// <see cref="GameConfig.Body"/> never breaks these; the needs numbers are the test's own. Expectations
    /// are computed through <see cref="BodyMath"/> rather than pinned as literals wherever the claim is a
    /// relation (more, less, equal, bounded). One game hour is <see cref="Hour"/> clock seconds.
    /// </summary>
    public class BodyServiceTests
    {
        private const double Hour = 100d;
        private const double Day = 24d * Hour;

        private TraitFixtures.MemorySave _save;
        private BodyClock _clock;
        private NeedsSettings _needsSettings;
        private BodySettings _bodySettings;
        private MessRegistry _messes;
        private NeedsService _needs;
        private BodyService _body;

        private readonly List<Vomited> _vomits = new List<Vomited>();
        private readonly List<MessLeft> _left = new List<MessLeft>();
        private readonly List<MessCleanedUp> _cleaned = new List<MessCleanedUp>();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            ClearChannels();
            EventBus.Subscribe<Vomited>(OnVomit);
            EventBus.Subscribe<MessLeft>(OnLeft);
            EventBus.Subscribe<MessCleanedUp>(OnCleaned);
            _vomits.Clear();
            _left.Clear();
            _cleaned.Clear();

            _save = new TraitFixtures.MemorySave();
            _clock = new BodyClock();
            _needsSettings = NeedsSettingsForTests();
            _bodySettings = BodySettings.Default;
            Wire(_save);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<Vomited>(OnVomit);
            EventBus.Unsubscribe<MessLeft>(OnLeft);
            EventBus.Unsubscribe<MessCleanedUp>(OnCleaned);
            ClearChannels();
            GameServices.Reset();
        }

        private static void ClearChannels()
        {
            EventBus.Clear<Vomited>();
            EventBus.Clear<MessLeft>();
            EventBus.Clear<MessCleanedUp>();
            EventBus.Clear<NeedStageChanged>();
        }

        private void OnVomit(Vomited e) => _vomits.Add(e);
        private void OnLeft(MessLeft e) => _left.Add(e);
        private void OnCleaned(MessCleanedUp e) => _cleaned.Add(e);

        /// <summary>Hunger +0.1 per game hour (empty to full in ten hours), peckish 0.4, hungry 0.8.</summary>
        private static NeedsSettings NeedsSettingsForTests() => new NeedsSettings
        {
            Hunger = new NeedCurve
            {
                RisePerGameHour = 0.1f, MildAt = 0.4f, StrongAt = 0.8f,
                MildWalkPenalty = 0.05f, StrongWalkPenalty = 0.12f,
            },
            Tiredness = new NeedCurve { RisePerGameHour = 0.05f, MildAt = 0.5f, StrongAt = 0.9f },
            WalkFloor = 0.8f,
            SellFloor = 0.9f,
            TickGameMinutes = 5f,
        };

        private void Wire(ISaveService save)
        {
            _messes = new MessRegistry(() => save);
            _needs = new NeedsService(() => save, () => _clock, () => _needsSettings, () => null, () => Hour,
                                      () => _body);
            _body = new BodyService(() => save, () => _clock, () => _bodySettings, () => _needsSettings,
                                    () => _needs, () => _messes, () => Hour);
        }

        private void SetHunger(float level, double at)
            => _save.Current.PlayerNeeds.Hunger = new NeedStateDto(level, at);

        // ---- metabolism ------------------------------------------------------------------------------

        [Test]
        public void Metabolism_RisesWithMass_AndWithHeight()
        {
            BodySettings s = _bodySettings;
            double previous = 0d;
            for (double kg = 40d; kg <= 160d; kg += 10d)
            {
                double kcal = BodyMath.DailyKcal(kg, 1f, s);
                Assert.Greater(kcal, previous, $"heavier needs more ({kg} kg)");
                previous = kcal;
            }
            previous = 0d;
            for (float h = s.MinHeightScale; h <= s.MaxHeightScale + 1e-4f; h += 0.02f)
            {
                double kcal = BodyMath.DailyKcal(70d, h, s);
                Assert.Greater(kcal, previous, $"taller needs more (scale {h})");
                previous = kcal;
            }
            Assert.Less(BodyMath.HungerRateScale(55d, 0f, s.MinHeightScale, s), 1f, "a small body eats less");
            Assert.Greater(BodyMath.HungerRateScale(95d, 1f, s.MaxHeightScale, s), 1f, "a large body eats more");
        }

        [Test]
        public void Metabolism_IsMifflinStJeorTimesActivity()
        {
            BodySettings s = _bodySettings;
            double expected = (10d * 70d + 6.25d * 175d - 5d * s.MetabolicAgeYears + s.MetabolicSexConstantKcal)
                              * s.ActivityFactor;
            Assert.AreEqual(expected, BodyMath.DailyKcal(70d, 1f, s), 1e-6);
        }

        [Test]
        public void ALargeBody_GetsHungrySooner_ASmallOneLater()
        {
            SetHunger(0f, 0d);
            _clock.T = 5d * Hour;
            float medium = _needs.Level(NeedKind.Hunger);

            _save.Current.PlayerBody = new PlayerBodyDto();
            Assert.IsTrue(_body.ChooseShape(1f, _bodySettings.MaxHeightScale));
            SetHunger(0f, _clock.T);
            _clock.T += 5d * Hour;
            float large = _needs.Level(NeedKind.Hunger);

            _save.Current.PlayerBody = new PlayerBodyDto();
            Assert.IsTrue(_body.ChooseShape(0f, _bodySettings.MinHeightScale));
            SetHunger(0f, _clock.T);
            _clock.T += 5d * Hour;
            float small = _needs.Level(NeedKind.Hunger);

            Assert.Greater(large, medium);
            Assert.Less(small, medium);
        }

        // ---- the default body is today's player, exactly ------------------------------------------------

        [Test]
        public void DefaultBody_HungerRate_IsExactlyTheOldOne()
        {
            Assert.AreEqual(1f, _body.HungerRateScale);
            SetHunger(0.15f, 30d);
            foreach (double t in new[] { 30d, 77d, 3.5d * Hour, 9.25d * Hour, 40d * Hour })
            {
                _clock.T = t;
                float old = NeedsMath.LevelAt(_save.Current.PlayerNeeds.Hunger, _needsSettings.Hunger, t, Hour);
                Assert.AreEqual(Bits(old), Bits(_needs.Level(NeedKind.Hunger)), $"bit for bit at {t}");
            }
        }

        [Test]
        public void DefaultBody_WalkSpeed_IsExactlyTheOldOne()
        {
            Assert.AreEqual(Bits(1f), Bits(_body.PlayerMultiplier(TraitStat.WalkSpeed)));
            foreach (TraitStat stat in System.Enum.GetValues(typeof(TraitStat)))
                Assert.AreEqual(1f, _body.PlayerMultiplier(stat), stat.ToString());

            // Through the one composition seam, hungry: needs x body is the needs factor itself.
            GameServices.Save = _save;
            GameServices.Needs = _needs;
            GameServices.Body = _body;
            GameServices.Traits = new TraitService(() => _save, () => new TraitDef[0]);
            SetHunger(0.9f, 0d);
            _clock.T = 1d;
            float needsOnly = GameServices.Traits.PlayerMultiplier(TraitStat.WalkSpeed)
                            * _needs.PlayerMultiplier(TraitStat.WalkSpeed);
            Assert.Less(needsOnly, 1f, "the fixture is hungry");
            Assert.AreEqual(Bits(needsOnly), Bits(CharacterFactors.Player(TraitStat.WalkSpeed)));
        }

        [Test]
        public void AnExistingSave_LoadsWithTodaysHungerRateAndWalk()
        {
            SaveData old = SaveSerialization.FromJson(
                "{\"SchemaVersion\":20,\"GameTimeSeconds\":5000.0,\"PlayerName\":\"Skipper\"," +
                "\"PlayerNeeds\":{\"Hunger\":{\"Level\":0.3,\"SinceGameSeconds\":5000.0}," +
                "\"Tiredness\":{\"Level\":0.1,\"SinceGameSeconds\":5000.0}}}");
            var save = new FixedSave(old);
            Wire(save);
            _clock.T = 5000d + 4d * Hour;

            Assert.AreEqual(Bits(1f), Bits(_body.HungerRateScale));
            Assert.AreEqual(Bits(1f), Bits(_body.PlayerMultiplier(TraitStat.WalkSpeed)));
            Assert.AreEqual(_body.LeanMassKg, _body.MassKg, "starts at the default body's lean mass");
            float expected = NeedsMath.LevelAt(old.PlayerNeeds.Hunger, _needsSettings.Hunger, _clock.T, Hour);
            Assert.AreEqual(Bits(expected), Bits(_needs.Level(NeedKind.Hunger)));

            // A save write settles at exactly the needs slice's numbers.
            var reference = new PlayerNeedsDto { Hunger = old.PlayerNeeds.Hunger, Tiredness = old.PlayerNeeds.Tiredness };
            NeedsMath.Settle(reference, _clock.T, _needsSettings, Hour);
            BodyMath.Settle(old.PlayerBody, old.PlayerNeeds, _clock.T, _needsSettings, _bodySettings, Hour);
            Assert.AreEqual(Bits(reference.Hunger.Level), Bits(old.PlayerNeeds.Hunger.Level));
            Assert.AreEqual(reference.Hunger.SinceGameSeconds, old.PlayerNeeds.Hunger.SinceGameSeconds);
            Assert.AreEqual((double)PlayerBodyDto.Unset, old.PlayerBody.MassKg, 0d, "an unchanged mass stays 'lean'");
        }

        // ---- speed -----------------------------------------------------------------------------------

        [Test]
        public void Taller_WalksFaster_Heavier_WalksSlower()
        {
            BodySettings s = _bodySettings;
            float frame = 0.5f;
            float shortWalk = BodyMath.WalkFactor(BodyMath.LeanMassKg(frame, s.MinHeightScale, s), frame, s.MinHeightScale, s);
            float tallWalk = BodyMath.WalkFactor(BodyMath.LeanMassKg(frame, s.MaxHeightScale, s), frame, s.MaxHeightScale, s);
            Assert.Less(shortWalk, 1f);
            Assert.Greater(tallWalk, 1f);

            double lean = BodyMath.LeanMassKg(frame, 1f, s), max = BodyMath.MaxMassKg(frame, 1f, s);
            float previous = 2f;
            for (double kg = lean; kg <= max; kg += (max - lean) / 10d)
            {
                float f = BodyMath.WalkFactor(kg, frame, 1f, s);
                Assert.LessOrEqual(f, previous, $"heavier is never faster ({kg:F1} kg)");
                previous = f;
            }
            Assert.Less(previous, 1f, "a full frame walks slower than a lean one");
            Assert.AreEqual(1f, BodyMath.WalkFactor(lean * 0.9d, frame, 1f, s), "no bonus below lean");
        }

        [Test]
        public void Speed_IsBounded_NobodyIsUnplayablySlow()
        {
            BodySettings s = _bodySettings;
            foreach (float frame in new[] { 0f, 0.5f, 1f })
            foreach (float h in new[] { s.MinHeightScale, 1f, s.MaxHeightScale })
            {
                float f = BodyMath.WalkFactor(BodyMath.MaxMassKg(frame, h, s), frame, h, s);
                Assert.GreaterOrEqual(f, s.WalkFloor, $"frame {frame}, height {h}");
            }

            BodySettings wild = s;
            wild.WalkFloor = 0f;
            wild.SlowPerExtraLeanMass = 1f;
            Assert.GreaterOrEqual(BodyMath.WalkFactor(10000d, 1f, 1f, wild), BodyMath.MinimumWalkFloor,
                                  "a mis-set config cannot stop anyone");

            // The worst body with the worst needs, through the composition seam.
            GameServices.Save = _save;
            GameServices.Traits = new TraitService(() => _save, () => new TraitDef[0]);
            GameServices.Needs = new FixedFactor(NeedsMath.MinimumFloor);
            GameServices.Body = new FixedBody(BodyMath.MinimumWalkFloor);
            Assert.GreaterOrEqual(CharacterFactors.Player(TraitStat.WalkSpeed), NeedsMath.MinimumFloor,
                                  "hunger slows, it never stops");
        }

        // ---- mass ------------------------------------------------------------------------------------

        [Test]
        public void Overfilling_AddsBoundedMass_AndNeverPassesTheFramesMax()
        {
            SetHunger(0f, 0d);
            double lean = _body.LeanMassKg, max = _body.MaxMassKg;
            Assert.AreEqual(MealOutcome.Eaten, _body.Eat(1f, Vector2.zero, "region.test"));
            double first = _body.MassKg - lean;
            double kcalPerMeal = BodyMath.KcalPerHungerUnit(_needsSettings.Hunger.RisePerGameHour, _bodySettings);
            Assert.AreEqual(BodyMath.MealGainKg(1f, 0f, kcalPerMeal, _bodySettings), first, 1e-9);
            Assert.Greater(first, 0d);
            Assert.LessOrEqual(first, _bodySettings.MaxGainKgPerMeal);

            for (int i = 0; i < 2000; i++)
            {
                _clock.T += 4d * Hour;   // the stomach empties a meal; hunger rises well short of a meal
                Assert.AreNotEqual(MealOutcome.Vomited, _body.Eat(1f, Vector2.zero, "region.test"));
                Assert.LessOrEqual(_body.MassKg, max);
            }
            Assert.AreEqual(max, _body.MassKg, 1e-9, "a frame fills to its max and no further");
            Assert.Less(_body.PlayerMultiplier(TraitStat.WalkSpeed), 1f, "and walks slower for it");
        }

        [Test]
        public void EatingWithinHunger_AddsNoMass()
        {
            SetHunger(0.8f, 0d);
            double before = _body.MassKg;
            _body.Eat(0.5f, Vector2.zero, "region.test");
            Assert.AreEqual(before, _body.MassKg, "hunger is the buffer: no surplus, no mass");
        }

        [Test]
        public void Starving_LosesMassAtTheBoundedRate_DownToTheFloor()
        {
            SetHunger(1f, 0d);
            double lean = _body.LeanMassKg;
            _clock.T = Day;
            Assert.AreEqual(lean - _bodySettings.StarvingLossKgPerGameDay, _body.MassKg, 1e-9);
            _clock.T = 1000d * Day;
            Assert.AreEqual(_body.FloorMassKg, _body.MassKg, 1e-9, "never below the frame's floor");
            Assert.GreaterOrEqual(_body.PlayerMultiplier(TraitStat.WalkSpeed), BodyMath.MinimumWalkFloor);
        }

        [Test]
        public void SteppingEqualsCatchUp_ThroughASleptThroughFast()
        {
            // Two identical large bodies, half hungry: one is settled every game hour (a save, a meal-less
            // tick), the other once at the end (a night slept through). 60 hours: rising, then starving.
            TraitFixtures.MemorySave stepped = NewLargeBody(0.5f), jumped = NewLargeBody(0.5f);
            double end = 60d * Hour;
            for (double t = Hour; t <= end + 1e-9; t += Hour)
                BodyMath.Settle(stepped.Current.PlayerBody, stepped.Current.PlayerNeeds, t, _needsSettings,
                                _bodySettings, Hour);
            BodyMath.Settle(jumped.Current.PlayerBody, jumped.Current.PlayerNeeds, end, _needsSettings,
                            _bodySettings, Hour);

            Assert.Less(jumped.Current.PlayerBody.MassKg, BodyMath.LeanMassKg(1f, _bodySettings.MaxHeightScale, _bodySettings),
                        "the fixture did starve");
            Assert.AreEqual(jumped.Current.PlayerBody.MassKg, stepped.Current.PlayerBody.MassKg, 1e-4);
            Assert.AreEqual(jumped.Current.PlayerNeeds.Hunger.Level, stepped.Current.PlayerNeeds.Hunger.Level, 1e-5f);

            // And the live read with no settle agrees with both.
            Wire(_save = NewLargeBody(0.5f));
            _clock.T = end;
            Assert.AreEqual(jumped.Current.PlayerBody.MassKg, _body.MassKg, 1e-4);
        }

        private TraitFixtures.MemorySave NewLargeBody(float hunger)
        {
            var save = new TraitFixtures.MemorySave();
            save.Current.PlayerBody = new PlayerBodyDto
            {
                Frame = 1f, HeightScale = _bodySettings.MaxHeightScale, ShapeChosen = true,
                MassSinceGameSeconds = 0d,
            };
            save.Current.PlayerNeeds.Hunger = new NeedStateDto(hunger, 0d);
            return save;
        }

        [Test]
        public void TheShape_IsChosenOnce_AndFixedAfterwards()
        {
            _clock.T = 10d;
            Assert.IsFalse(_body.ShapeChosen);
            Assert.IsTrue(_body.ChooseShape(0.9f, 1.05f));
            Assert.AreEqual(0.9f, _body.Frame);
            Assert.AreEqual(1.05f, _body.HeightScale);
            Assert.AreEqual(_body.LeanMassKg, _body.MassKg, "a new shape starts lean");
            Assert.IsFalse(_body.ChooseShape(0.1f, 0.95f), "bone structure is fixed afterwards");
            Assert.AreEqual(0.9f, _body.Frame);
            Assert.AreEqual(_bodySettings.MaxHeightScale, BodyMath.ClampHeightScale(9f, _bodySettings));
        }

        // ---- overeating and the mess -----------------------------------------------------------------

        [Test]
        public void Overeating_VomitsOnce_LeavesOneMess_AndLosesTheMeal()
        {
            SetHunger(0.9f, 0d);
            _clock.T = 1d;
            Assert.AreEqual(MealOutcome.Eaten, _body.Eat(1f, Vector2.zero, "region.harbour"));
            float hungerAfterFirst = _needs.Level(NeedKind.Hunger);
            double massAfterFirst = _body.MassKg;

            var at = new Vector2(12.5f, -3.25f);
            Assert.AreEqual(MealOutcome.Vomited, _body.Eat(1f, at, "region.harbour"),
                            "1 + 1 meals overfills a 1.5-meal stomach");

            Assert.AreEqual(1, _vomits.Count, "exactly one Vomited");
            Assert.AreEqual(1, _left.Count, "exactly one mess left");
            Assert.AreEqual(1, _messes.All.Count);
            Assert.AreEqual("region.harbour", _vomits[0].RegionId);
            Assert.AreEqual(at, _vomits[0].Position);
            Assert.AreEqual(_messes.All[0].Id, _vomits[0].MessId);
            Assert.AreEqual(at, _messes.All[0].Position);
            Assert.AreEqual(1d, _messes.All[0].GameSeconds);
            Assert.AreEqual(1f, _vomits[0].MealsLost);

            Assert.AreEqual(hungerAfterFirst, _needs.Level(NeedKind.Hunger), "the meal's energy is lost");
            Assert.AreEqual(massAfterFirst, _body.MassKg, "and adds no mass");
            Assert.AreEqual(0f, _body.StomachMeals, "the stomach is empty");
            Assert.AreEqual(MealOutcome.Eaten, _body.Eat(1f, at, "region.harbour"), "so the next meal stays down");
            Assert.AreEqual(1, _vomits.Count);
        }

        [Test]
        public void TheStomach_EmptiesOverTime()
        {
            SetHunger(1f, 0d);
            _body.Eat(1f, Vector2.zero, "region.test");
            Assert.AreEqual(1f, _body.StomachMeals);
            _clock.T = 2d * Hour;
            Assert.AreEqual(1f - 2f * _bodySettings.StomachEmptiesPerGameHour, _body.StomachMeals, 1e-6f);
            _clock.T = 100d * Hour;
            Assert.AreEqual(0f, _body.StomachMeals);
        }

        [Test]
        public void AMess_SurvivesSaveAndLoad_UntilCleanedUp()
        {
            SetHunger(0.9f, 0d);
            _body.Eat(1f, Vector2.zero, "region.harbour");
            _body.Eat(1f, new Vector2(4f, 5f), "region.harbour");
            string id = _messes.All[0].Id;

            SaveData back = SaveSerialization.FromJson(SaveSerialization.ToJson(_save.Current));
            var loaded = new MessRegistry(() => new FixedSave(back));
            Assert.AreEqual(1, loaded.All.Count, "the mess is saved");
            Assert.IsTrue(loaded.TryGet(id, out MessRecordDto mess));
            Assert.AreEqual("region.harbour", mess.RegionId);
            Assert.AreEqual(new Vector2(4f, 5f), mess.Position);
            var here = new List<MessRecordDto>();
            Assert.AreEqual(1, loaded.CopyIn("region.harbour", here));
            Assert.AreEqual(0, loaded.CopyIn("region.elsewhere", here));

            Assert.IsFalse(loaded.CleanUp("mess.unknown"));
            Assert.AreEqual(0, _cleaned.Count, "no signal for a mess that is not there");
            Assert.IsTrue(loaded.CleanUp(id));
            Assert.AreEqual(1, _cleaned.Count);
            Assert.AreEqual(id, _cleaned[0].Mess.Id);
            Assert.AreEqual(0, loaded.All.Count, "cleanup is the only way it goes");
            Assert.IsFalse(loaded.CleanUp(id), "and only once");

            string next = loaded.Add("region.harbour", Vector2.one, 7d);
            Assert.AreNotEqual(id, next, "an id is never reused");
        }

        [Test]
        public void NoSave_DoesNothing()
        {
            var none = new FixedSave(null);
            var body = new BodyService(() => none, () => _clock, () => _bodySettings, () => _needsSettings,
                                       () => null, () => new MessRegistry(() => none), () => Hour);
            Assert.AreEqual(MealOutcome.Nothing, body.Eat(1f, Vector2.zero, "region.test"));
            Assert.AreEqual(1f, body.PlayerMultiplier(TraitStat.WalkSpeed));
            Assert.AreEqual(1f, body.HungerRateScale);
            Assert.IsFalse(body.ChooseShape(1f, 1f));
            Assert.AreEqual(MealOutcome.Nothing, _body.Eat(0f, Vector2.zero, "region.test"));
        }

        [Test]
        public void ShippedAsset_CarriesTheBodyBlock()
        {
            string path = System.IO.Path.Combine(Application.dataPath, "_Project/Data/Config/GameConfig.asset");
            string text = System.IO.File.ReadAllText(path);
            foreach (string field in new[]
                     {
                         "ReferenceHeightMetres", "DefaultFrame", "SmallFrameLeanBmi", "LargeFrameMaxBmi",
                         "MetabolicSexConstantKcal", "ActivityFactor", "KcalPerKgOfMass", "StarvingLossKgPerGameDay",
                         "HeightStrideExponent", "SlowPerExtraLeanMass", "StomachCapacityMeals",
                         "StomachEmptiesPerGameHour",
                     })
                StringAssert.Contains("    " + field + ":", text, field);
        }

        private static byte[] Bits(float f) => System.BitConverter.GetBytes(f);

        // ---- fakes -----------------------------------------------------------------------------------

        private sealed class FixedSave : ISaveService
        {
            public FixedSave(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private sealed class FixedFactor : INeedsService
        {
            private readonly float _walk;
            public FixedFactor(float walk) { _walk = walk; }
            public float Level(NeedKind need) => 1f;
            public NeedStage Stage(NeedKind need) => NeedStage.Strong;
            public float PlayerMultiplier(TraitStat stat) => stat == TraitStat.WalkSpeed ? _walk : 1f;
            public void Eat(float amount) { }
            public void Sleep() { }
            public void Tick() { }
        }

        private sealed class FixedBody : IBodyService
        {
            private readonly float _walk;
            public FixedBody(float walk) { _walk = walk; }
            public float Frame => 1f;
            public float HeightScale => 1f;
            public bool ShapeChosen => true;
            public double MassKg => 0d;
            public double LeanMassKg => 0d;
            public double MaxMassKg => 0d;
            public double FloorMassKg => 0d;
            public double DailyKcal => 0d;
            public float HungerRateScale => 1f;
            public float StomachMeals => 0f;
            public float PlayerMultiplier(TraitStat stat) => stat == TraitStat.WalkSpeed ? _walk : 1f;
            public bool ChooseShape(float frame, float heightScale) => false;
            public MealOutcome Eat(float meals, Vector2 position, string regionId) => MealOutcome.Nothing;
            public void Settle() { }
        }

        private sealed class BodyClock : IGameClock
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
