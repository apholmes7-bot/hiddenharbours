using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Kitchens and microwaves slice: the one recipe runner. Which recipes each station kind accepts, how
    /// long the work takes (recipe seconds times the station's multiplier), the microwave refusing raw food
    /// with nothing taken, and the cooked dish reaching the eat path through the supply locker. Every Def and
    /// number here is the test's own, so retuning shipped content never breaks these.
    /// </summary>
    public class RecipeRunnerTests
    {
        private const double Hour = 100d;
        private const string Key = "fixture.test.station";

        private readonly List<Object> _made = new List<Object>();
        private readonly List<FoodCookStarted> _started = new List<FoodCookStarted>();
        private readonly List<FoodCooked> _cooked = new List<FoodCooked>();
        private readonly List<CookRefused> _refused = new List<CookRefused>();
        private TraitFixtures.MemorySave _save;
        private FakeHands _hands;
        private RecipeBook _recipes;
        private FoodBook _foods;
        private double _now;

        private CookingStationDef _prep;
        private CookingStationDef _stove;
        private CookingStationDef _microwave;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<FoodCookStarted>();
            EventBus.Clear<FoodCooked>();
            EventBus.Clear<CookRefused>();
            EventBus.Subscribe<FoodCookStarted>(OnStarted);
            EventBus.Subscribe<FoodCooked>(OnCooked);
            EventBus.Subscribe<CookRefused>(OnRefused);
            _started.Clear(); _cooked.Clear(); _refused.Clear();
            _save = new TraitFixtures.MemorySave();
            _hands = new FakeHands();
            _swap = null;
            _now = 1000d;

            _recipes = new RecipeBook(new[]
            {
                Recipe("recipe.t_fillet", RecipeStations.Prep, RecipeInputKind.Species, "fish.t_cod", "food.t_fillet", 20f, 0f),
                Recipe("recipe.t_fried", RecipeStations.Cooktop, RecipeInputKind.Intermediate, "food.t_fillet", "food.t_fried", 40f, 0.6f),
                Recipe("recipe.t_hot_beans", RecipeStations.Microwave, RecipeInputKind.Item, "supply.t_beans", "food.t_hot_beans", 120f, 0.4f),
            });
            _foods = new FoodBook(new[]
            {
                Food("food.t_cod", "fish.t_cod", false, 0f, false),
                Food("food.t_beans", "supply.t_beans", true, 0.35f, true),
                Food("food.t_fried", "food.t_fried", true, 0.6f, true),
                Food("food.t_hot_beans", "food.t_hot_beans", true, 0.4f, true),
            });

            _prep = Station("cooking_station.t_prep", CookingStationKind.Prep, 1f, false, RecipeStations.Prep);
            _stove = Station("cooking_station.t_stove", CookingStationKind.Stove, 1f, false, RecipeStations.Cooktop, RecipeStations.Microwave);
            _microwave = Station("cooking_station.t_microwave", CookingStationKind.Microwave, 0.25f, true, RecipeStations.Microwave);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<FoodCookStarted>(OnStarted);
            EventBus.Unsubscribe<FoodCooked>(OnCooked);
            EventBus.Unsubscribe<CookRefused>(OnRefused);
            EventBus.Clear<FoodCookStarted>();
            EventBus.Clear<FoodCooked>();
            EventBus.Clear<CookRefused>();
            GameServices.Reset();
            foreach (Object o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private void OnStarted(FoodCookStarted e) => _started.Add(e);
        private void OnCooked(FoodCooked e) => _cooked.Add(e);
        private void OnRefused(CookRefused e) => _refused.Add(e);

        private SwapSave _swap;   // when set, the live save service (a load swaps its Current)

        private RecipeRunner Runner() => new RecipeRunner(() => _recipes, () => _foods,
            () => _swap != null ? (ISaveService)_swap : _save, () => _hands, () => _now, FindStation);

        private CookingStationDef FindStation(string id)
        {
            foreach (Object o in _made)
                if (o is CookingStationDef s && s.Id == id) return s;
            return null;
        }

        private RecipeDef Recipe(string id, string station, RecipeInputKind kind, string input, string output, float seconds, float relief)
        {
            var def = ScriptableObject.CreateInstance<RecipeDef>();
            def.Id = id; def.DisplayName = id; def.Station = station; def.InputKind = kind; def.InputId = input;
            def.OutputId = output; def.WorkSeconds = seconds; def.HungerRelief = relief;
            _made.Add(def);
            return def;
        }

        private FoodDef Food(string id, string item, bool raw, float relief, bool microwavable)
        {
            var def = ScriptableObject.CreateInstance<FoodDef>();
            def.Id = id; def.ItemId = item; def.EdibleRaw = raw; def.HungerRelief = relief;
            def.PreppedId = ""; def.CookedId = ""; def.Microwavable = microwavable;
            _made.Add(def);
            return def;
        }

        private CookingStationDef Station(string id, CookingStationKind kind, float multiplier, bool microwavableOnly, params string[] accepts)
        {
            var def = ScriptableObject.CreateInstance<CookingStationDef>();
            def.Id = id; def.Kind = kind; def.CookTimeMultiplier = multiplier; def.MicrowavableOnly = microwavableOnly;
            def.AcceptedStations = accepts;
            _made.Add(def);
            return def;
        }

        private static CatchItem Catch(string species) => new CatchItem(species, species, FishCategory.InshoreGroundfish, 1f, 5, 0f);

        private int Count(string item) => SupplyLocker.Count(_save.Current, item);

        // ---- acceptance per station kind -----------------------------------------------------------------

        [Test]
        public void Prep_TakesTheRawCatchInHand_AndOnlyPrepRecipes()
        {
            _hands.Held = Catch("fish.t_cod");
            RecipeRunner runner = Runner();

            Assert.AreEqual(CookRefusal.None, runner.Check(_prep, Key, out CookPlan plan));
            Assert.AreEqual("recipe.t_fillet", plan.Recipe.Id);
            Assert.IsTrue(plan.FromHand);

            _hands.Held = null;
            SupplyLocker.Add(_save.Current, "food.t_fillet", 1);
            SupplyLocker.Add(_save.Current, "supply.t_beans", 1);
            Assert.AreEqual(CookRefusal.NothingToCook, runner.Check(_prep, Key, out _),
                "a prep counter neither cooks a fillet nor heats beans");
        }

        [Test]
        public void Stove_CooksAPrepOutput_AndHeatsWhatAMicrowaveHeats()
        {
            RecipeRunner runner = Runner();
            SupplyLocker.Add(_save.Current, "food.t_fillet", 1);
            Assert.AreEqual(CookRefusal.None, runner.Check(_stove, Key, out CookPlan plan));
            Assert.AreEqual("recipe.t_fried", plan.Recipe.Id);
            Assert.IsFalse(plan.FromHand);

            _save.Current.SupplyStock.Clear();
            SupplyLocker.Add(_save.Current, "supply.t_beans", 1);
            Assert.AreEqual(CookRefusal.None, runner.Check(_stove, Key, out plan));
            Assert.AreEqual("recipe.t_hot_beans", plan.Recipe.Id, "the stove lists the microwave's station too");
        }

        [Test]
        public void Stove_RefusesARawCatch_AsNeedsPrep_AndLeavesItInTheHand()
        {
            _hands.Held = Catch("fish.t_cod");
            Assert.AreEqual(CookRefusal.NeedsPrep, Runner().TryStart(_stove, Key));
            Assert.IsTrue(_hands.Held.HasValue, "nothing taken");
            Assert.AreEqual(0, _hands.Takes);
            Assert.AreEqual(1, _refused.Count);
            Assert.AreEqual(CookRefusal.NeedsPrep, _refused[0].Reason);
            Assert.IsEmpty(_started);
        }

        [Test]
        public void Microwave_HeatsAMicrowavableItem()
        {
            SupplyLocker.Add(_save.Current, "supply.t_beans", 2);
            RecipeRunner runner = Runner();
            Assert.AreEqual(CookRefusal.None, runner.TryStart(_microwave, Key));
            Assert.AreEqual(1, Count("supply.t_beans"), "one tin used");
            Assert.AreEqual(1, _started.Count);
            Assert.AreEqual("recipe.t_hot_beans", _started[0].RecipeId);
        }

        [Test]
        public void Microwave_RefusesARawFishInTheHand_AndTakesNothing()
        {
            _hands.Held = Catch("fish.t_cod");
            SupplyLocker.Add(_save.Current, "supply.t_beans", 1);

            Assert.AreEqual(CookRefusal.NotMicrowavable, Runner().TryStart(_microwave, Key),
                "you can't microwave a raw fish (owner 2026-11-15)");

            Assert.IsTrue(_hands.Held.HasValue, "the fish stays in the hand");
            Assert.AreEqual(0, _hands.Takes);
            Assert.AreEqual(1, Count("supply.t_beans"), "what you hold is what you meant: the locker is untouched");
            Assert.AreEqual(1, _refused.Count);
            Assert.AreEqual(CookRefusal.NotMicrowavable, _refused[0].Reason);
            Assert.AreEqual("cooking_station.t_microwave", _refused[0].StationId);
            Assert.IsEmpty(_started);
        }

        [Test]
        public void Microwave_ThatListsTheCooktop_StillRefusesARawFillet()
        {
            CookingStationDef wide = Station("cooking_station.t_wide", CookingStationKind.Microwave, 0.25f, true,
                                             RecipeStations.Cooktop, RecipeStations.Microwave);
            SupplyLocker.Add(_save.Current, "food.t_fillet", 1);

            Assert.AreEqual(CookRefusal.NotMicrowavable, Runner().TryStart(wide, Key),
                "a raw cut has a cooktop recipe, but it is not microwavable");
            Assert.AreEqual(1, Count("food.t_fillet"), "nothing spent");
        }

        [Test]
        public void EmptyHandsAndLocker_AreNothingToCook_AndNullStationIsRefused()
        {
            RecipeRunner runner = Runner();
            Assert.AreEqual(CookRefusal.NothingToCook, runner.TryStart(_stove, Key));
            Assert.AreEqual(CookRefusal.NoStation, runner.TryStart(null, Key));
            Assert.AreEqual(CookRefusal.NoStation, runner.TryStart(_stove, ""));
            Assert.AreEqual(3, _refused.Count);
        }

        // ---- cook time ---------------------------------------------------------------------------------

        [Test]
        public void CookTime_IsRecipeSecondsTimesTheStationMultiplier()
        {
            RecipeDef fried = _recipes.Find("recipe.t_fried");
            RecipeDef beans = _recipes.Find("recipe.t_hot_beans");
            Assert.AreEqual(40f, RecipeRunner.CookSeconds(fried, _stove), 1e-5f);
            Assert.AreEqual(120f, RecipeRunner.CookSeconds(beans, _stove), 1e-5f);
            Assert.AreEqual(30f, RecipeRunner.CookSeconds(beans, _microwave), 1e-5f, "the microwave is fast");

            _stove.CookTimeMultiplier = 0f;
            Assert.AreEqual(40f, RecipeRunner.CookSeconds(fried, _stove), 1e-5f, "an unset multiplier reads as 1");
        }

        [Test]
        public void Output_ArrivesAtTheReadyTime_NotBefore()
        {
            _hands.Held = Catch("fish.t_cod");
            RecipeRunner runner = Runner();
            Assert.AreEqual(CookRefusal.None, runner.TryStart(_prep, Key));
            Assert.IsFalse(_hands.Held.HasValue, "the catch is on the board");
            Assert.AreEqual(_now + 20d, _started[0].ReadyAtGameSeconds, 1e-9);

            _now += 19.5d;
            Assert.IsFalse(runner.TryFinish(Key, out _), "not done yet");
            Assert.AreEqual(0, Count("food.t_fillet"));

            _now += 0.5d;
            Assert.IsTrue(runner.TryFinish(Key, out CookJob done));
            Assert.AreEqual("recipe.t_fillet", done.Recipe.Id);
            Assert.AreEqual(1, Count("food.t_fillet"), "the fillet is in the supply locker");
            Assert.AreEqual(1, _cooked.Count);
            Assert.AreEqual("food.t_fillet", _cooked[0].OutputId);
            Assert.IsFalse(runner.IsCooking(Key, out _), "the station is free again");
            Assert.IsFalse(runner.TryFinish(Key, out _), "delivered once");
        }

        [Test]
        public void ABusyStation_RefusesASecondStart_AndTwoStationsCookSeparately()
        {
            SupplyLocker.Add(_save.Current, "supply.t_beans", 3);
            RecipeRunner runner = Runner();
            Assert.AreEqual(CookRefusal.None, runner.TryStart(_microwave, Key));
            Assert.AreEqual(CookRefusal.Busy, runner.TryStart(_microwave, Key));
            Assert.AreEqual(2, Count("supply.t_beans"), "a busy station takes nothing");

            Assert.AreEqual(CookRefusal.None, runner.TryStart(_microwave, "fixture.test.other"));
            Assert.AreEqual(2, runner.ActiveCount);
            Assert.AreEqual(1, Count("supply.t_beans"));
        }

        [Test]
        public void LockerScan_IsOrdinal_NotInsertionOrder()
        {
            CookingStationDef both = Station("cooking_station.t_both", CookingStationKind.Stove, 1f, false,
                                             RecipeStations.Cooktop, RecipeStations.Microwave);
            SupplyLocker.Add(_save.Current, "supply.t_beans", 1);
            SupplyLocker.Add(_save.Current, "food.t_fillet", 1);
            Assert.AreEqual(CookRefusal.None, Runner().Check(both, Key, out CookPlan a));

            _save.Current.SupplyStock.Reverse();
            Assert.AreEqual(CookRefusal.None, Runner().Check(both, Key, out CookPlan b));
            Assert.AreEqual("food.t_fillet", a.InputItemId, "food.* sorts before supply.*");
            Assert.AreEqual(a.InputItemId, b.InputItemId);
        }

        [Test]
        public void SameStart_SameReadyTime_AndSameLocker()
        {
            string Run()
            {
                var save = new TraitFixtures.MemorySave();
                SupplyLocker.Add(save.Current, "supply.t_beans", 1);
                var runner = new RecipeRunner(() => _recipes, () => _foods, () => save, () => null, () => 500d, FindStation);
                runner.TryStart(_microwave, Key);
                runner.IsCooking(Key, out CookJob job);
                // a fresh runner over the same save finds the job in its rows and delivers it once
                var later = new RecipeRunner(() => _recipes, () => _foods, () => save, () => null, () => job.ReadyAt, FindStation);
                Assert.IsTrue(later.TryFinish(Key, out _));
                Assert.IsFalse(later.TryFinish(Key, out _));
                return job.ReadyAt.ToString("R") + "|" + SupplyLocker.Count(save.Current, "supply.t_beans")
                       + "|" + SupplyLocker.Count(save.Current, "food.t_hot_beans") + "|" + save.Current.CookJobs.Count;
            }
            Assert.AreEqual(Run(), Run());
        }

        // ---- the result reaches the player's food path ---------------------------------------------------

        [Test]
        public void ACookedDish_IsEatenFromTheLocker_ThroughTheExistingEatPath()
        {
            var clock = new FixedClock();
            var needs = new NeedsService(() => _save, () => clock, () => NeedsSettings.Default,
                                         () => new TraitService(() => _save, () => new TraitDef[0]), () => Hour);
            _save.Current.PlayerNeeds = new PlayerNeedsDto
            {
                Hunger = new NeedStateDto(0.9f, 0d),
                Tiredness = new NeedStateDto(0f, 0d),
            };
            SupplyLocker.Add(_save.Current, "food.t_fillet", 1);
            RecipeRunner runner = Runner();
            Assert.AreEqual(CookRefusal.None, runner.TryStart(_stove, Key));
            _now += 40d;
            Assert.IsTrue(runner.TryFinish(Key, out _));
            Assert.AreEqual(1, Count("food.t_fried"));

            var eater = new FoodEater(() => _foods, () => needs, () => _save, () => _hands, () => null,
                                      () => Vector2.zero, () => "region.test");
            Assert.AreEqual(EatRefusal.None, eater.TryEatFromStock("food.t_fried"));
            Assert.AreEqual(0.9f - 0.6f, needs.Level(NeedKind.Hunger), 1e-6f, "the dish's own relief");
            Assert.AreEqual(0, Count("food.t_fried"), "eaten");
        }

        [Test]
        public void GameServices_Cooking_IsNeverNull_AndResetDropsIt()
        {
            RecipeRunner first = GameServices.Cooking;
            Assert.IsNotNull(first);
            GameServices.Reset();
            Assert.AreNotSame(first, GameServices.Cooking);
        }

        // ---- loads and missing saves -------------------------------------------------------------------

        [Test]
        public void ALoadOfAnotherSave_ShowsOnlyThatSavesJobs_SoNothingDuplicates()
        {
            _swap = new SwapSave(SaveMigration.NewGame());
            SaveData before = _swap.Current;
            SupplyLocker.Add(before, "supply.t_beans", 1);
            RecipeRunner runner = Runner();
            GameServices.Cooking = runner;
            Assert.AreEqual(CookRefusal.None, runner.TryStart(_microwave, Key));

            _swap.Current = SaveMigration.NewGame();   // a load of a save with nothing cooking
            EventBus.Publish(default(GameLoaded));

            Assert.AreEqual(0, runner.ActiveCount, "the job belongs to the other save");
            _now += 1000d;
            Assert.IsFalse(runner.TryFinish(Key, out _));
            Assert.AreEqual(0, SupplyLocker.Count(_swap.Current, "food.t_hot_beans"), "no food duplicated into the loaded save");
            Assert.AreEqual(1, before.CookJobs.Count, "the other save keeps its own pot");
            Assert.IsEmpty(_cooked);
        }

        [Test]
        public void ALoad_ReReadsTheRows_EvenWhenTheSaveObjectIsReused()
        {
            SupplyLocker.Add(_save.Current, "supply.t_beans", 1);
            RecipeRunner runner = Runner();
            GameServices.Cooking = runner;
            Assert.AreEqual(CookRefusal.None, runner.TryStart(_microwave, Key));
            _save.Current.CookJobs.Clear();             // the loaded data has nothing on the stove

            EventBus.Publish(default(GameLoaded));

            Assert.AreEqual(0, runner.ActiveCount, "the slot's runner reads the loaded rows");
        }

        [Test]
        public void AReplacedRunner_NoLongerListensForLoads()
        {
            SupplyLocker.Add(_save.Current, "supply.t_beans", 1);
            RecipeRunner old = Runner();
            GameServices.Cooking = old;
            GameServices.Cooking = Runner();
            Assert.AreEqual(CookRefusal.None, old.TryStart(_microwave, Key));
            _save.Current.CookJobs.Clear();

            EventBus.Publish(default(GameLoaded));

            Assert.AreEqual(1, old.ActiveCount, "only the runner in the slot rebinds on a load");
            GameServices.Cooking = null;
        }

        // ---- saved with the game (v24 CookJobs rows) -----------------------------------------------------

        private SaveData SaveAndLoad(SaveData save)
            => SaveMigration.Migrate(SaveSerialization.FromJson(SaveSerialization.ToJson(save)));

        [Test]
        public void StartWritesTheRow_AndFinishRemovesIt()
        {
            SupplyLocker.Add(_save.Current, "supply.t_beans", 1);
            RecipeRunner runner = Runner();
            Assert.AreEqual(CookRefusal.None, runner.TryStart(_microwave, Key));

            Assert.AreEqual(1, _save.Current.CookJobs.Count);
            CookJobDto row = _save.Current.CookJobs[0];
            Assert.AreEqual(Key, row.StationKey);
            Assert.AreEqual("cooking_station.t_microwave", row.StationId);
            Assert.AreEqual("recipe.t_hot_beans", row.RecipeId);
            Assert.AreEqual(_now + 30d, row.ReadyAtGameSeconds, 1e-9);

            _now += 30d;
            Assert.IsTrue(runner.TryFinish(Key, out _));
            Assert.AreEqual(0, _save.Current.CookJobs.Count, "a finished pot leaves no row");
        }

        [Test]
        public void APotOnTheStove_SurvivesSaveAndLoad_AndFinishesAtTheSameGameTime()
        {
            _swap = new SwapSave(SaveMigration.NewGame());
            SupplyLocker.Add(_swap.Current, "food.t_fillet", 1);
            RecipeRunner runner = Runner();
            Assert.AreEqual(CookRefusal.None, runner.TryStart(_stove, Key));
            runner.IsCooking(Key, out CookJob started);
            SaveData before = _swap.Current;

            _swap.Current = SaveAndLoad(before);
            _now += 10d;   // played on after loading, short of the ready time

            Assert.IsTrue(runner.IsCooking(Key, out CookJob loaded), "the pot is still on the stove");
            Assert.AreEqual("recipe.t_fried", loaded.Recipe.Id);
            Assert.AreEqual("cooking_station.t_stove", loaded.StationId);
            Assert.AreEqual(started.ReadyAt, loaded.ReadyAt, "same ready game time");
            Assert.AreEqual(CookRefusal.Busy, runner.Check(_stove, Key, out _));
            Assert.AreEqual(0, SupplyLocker.Count(_swap.Current, "food.t_fillet"), "the fillet stayed on the stove");

            Assert.IsFalse(runner.TryFinish(Key, out _), "not done yet");
            _now = started.ReadyAt;
            Assert.IsTrue(runner.TryFinish(Key, out _));
            Assert.AreEqual(1, SupplyLocker.Count(_swap.Current, "food.t_fried"), "the dish arrives in the loaded save");
            Assert.AreEqual(0, _swap.Current.CookJobs.Count);
            Assert.AreEqual(0, SupplyLocker.Count(before, "food.t_fried"), "and not in the save it was copied from");
        }

        [Test]
        public void APotWhoseTimePassedWhileSaved_IsReadyAtOnceOnLoad()
        {
            SaveData save = SaveMigration.NewGame();
            save.CookJobs.Add(new CookJobDto
            {
                StationKey = Key, StationId = "cooking_station.t_microwave", RecipeId = "recipe.t_hot_beans",
                ReadyAtGameSeconds = 500d,
            });
            _swap = new SwapSave(SaveAndLoad(save));
            _now = 9000d;   // the loaded clock is long past the ready time

            RecipeRunner runner = Runner();
            Assert.IsTrue(runner.TryFinish(Key, out CookJob done));
            Assert.AreEqual("recipe.t_hot_beans", done.Recipe.Id);
            Assert.AreEqual(1, SupplyLocker.Count(_swap.Current, "food.t_hot_beans"));
            Assert.AreEqual(1, _cooked.Count);
            Assert.AreEqual(0, _swap.Current.CookJobs.Count);
        }

        [Test]
        public void ALoadedJobWhoseRecipeOrStationIsGone_IsDroppedWithoutACrash()
        {
            SaveData save = SaveMigration.NewGame();
            save.CookJobs.Add(new CookJobDto { StationKey = "k.gone_recipe", StationId = "cooking_station.t_stove", RecipeId = "recipe.retired", ReadyAtGameSeconds = 5d });
            save.CookJobs.Add(new CookJobDto { StationKey = "k.gone_station", StationId = "cooking_station.retired", RecipeId = "recipe.t_fried", ReadyAtGameSeconds = 5d });
            save.CookJobs.Add(new CookJobDto { StationKey = "k.no_station", StationId = "", RecipeId = "recipe.t_fried", ReadyAtGameSeconds = 5d });
            save.CookJobs.Add(new CookJobDto { StationKey = "k.good", StationId = "cooking_station.t_stove", RecipeId = "recipe.t_fried", ReadyAtGameSeconds = 5d });
            save.CookJobs.Add(new CookJobDto { StationKey = "k.good", StationId = "cooking_station.t_stove", RecipeId = "recipe.t_fried", ReadyAtGameSeconds = 9d });
            _swap = new SwapSave(SaveAndLoad(save));

            RecipeRunner runner = null;
            Assert.DoesNotThrow(() => { runner = Runner(); _ = runner.ActiveCount; });

            Assert.AreEqual(1, runner.ActiveCount, "only the job whose recipe and station still exist");
            Assert.IsTrue(runner.IsCooking("k.good", out CookJob good));
            Assert.AreEqual(5d, good.ReadyAt, "the first row for a key wins");
            Assert.AreEqual(1, _swap.Current.CookJobs.Count, "the dropped rows leave the save");
            Assert.AreEqual("k.good", _swap.Current.CookJobs[0].StationKey);
            Assert.IsFalse(runner.TryFinish("k.gone_recipe", out _));
        }

        [Test]
        public void AV23Save_LoadsWithNothingCooking()
        {
            _swap = new SwapSave(SaveSerialization.FromJson("{\"SchemaVersion\":23,\"GameTimeSeconds\":4321.0}"));
            SupplyLocker.Add(_swap.Current, "supply.t_beans", 1);
            RecipeRunner runner = Runner();

            Assert.AreEqual(0, runner.ActiveCount);
            Assert.IsNotNull(_swap.Current.CookJobs);
            Assert.AreEqual(CookRefusal.None, runner.TryStart(_microwave, Key), "and the stations work as usual");
            Assert.AreEqual(1, _swap.Current.CookJobs.Count);
        }

        [Test]
        public void NoSave_AtTheReadyTime_KeepsTheJob_AndAnnouncesNothing()
        {
            SupplyLocker.Add(_save.Current, "supply.t_beans", 1);
            TraitFixtures.MemorySave live = _save;
            RecipeRunner runner = Runner();
            Assert.AreEqual(CookRefusal.None, runner.TryStart(_microwave, Key));
            _now += 30d;

            _save = null;
            Assert.IsFalse(runner.TryFinish(Key, out _), "no save to put the food in");
            Assert.IsEmpty(_cooked, "no FoodCooked for food that went nowhere");
            Assert.IsTrue(runner.IsCooking(Key, out _), "the job waits for a save");

            _save = live;
            Assert.IsTrue(runner.TryFinish(Key, out _));
            Assert.AreEqual(1, Count("food.t_hot_beans"));
            Assert.AreEqual(1, _cooked.Count);
        }

        // ---- doubles -----------------------------------------------------------------------------------

        private sealed class FakeHands : ICatchHands
        {
            public CatchItem? Held;
            public int Takes;

            public bool TryPutInHand(in CatchItem item)
            {
                if (Held.HasValue) return false;
                Held = item;
                return true;
            }

            public bool TryTakeFromHand(out CatchItem item)
            {
                item = Held ?? default;
                if (!Held.HasValue) return false;
                Held = null;
                Takes++;
                return true;
            }

            public bool TryPeekHand(out CatchItem item)
            {
                item = Held ?? default;
                return Held.HasValue;
            }
        }

        private sealed class SwapSave : ISaveService
        {
            public SwapSave(SaveData current) { Current = current; }
            public SaveData Current { get; set; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private sealed class FixedClock : IGameClock
        {
            public double TotalSeconds => 0d;
            public GameTime Now => new GameTime(0d);
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
