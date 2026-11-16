using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Food follow-up: eating a <see cref="FoodDef"/> goes through the body, once. Hunger falls by the relief
    /// exactly once, the food's <see cref="FoodDef.EnergyKcal"/> past what hunger was short of becomes mass,
    /// overeating real food is sick, and an asset without the new field eats exactly as the plain body meal.
    /// Body and needs numbers are the shipped code defaults, never the asset; foods are the test's own.
    /// </summary>
    public class FoodFeedsBodyTests
    {
        private const double Hour = 100d;

        private readonly List<Object> _made = new List<Object>();
        private readonly List<AteFood> _ate = new List<AteFood>();
        private readonly List<Vomited> _vomits = new List<Vomited>();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<AteFood>();
            EventBus.Clear<EatRefused>();
            EventBus.Clear<Vomited>();
            EventBus.Clear<MessLeft>();
            EventBus.Subscribe<AteFood>(OnAte);
            EventBus.Subscribe<Vomited>(OnVomit);
            _ate.Clear();
            _vomits.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<AteFood>(OnAte);
            EventBus.Unsubscribe<Vomited>(OnVomit);
            EventBus.Clear<AteFood>();
            EventBus.Clear<EatRefused>();
            EventBus.Clear<Vomited>();
            EventBus.Clear<MessLeft>();
            EventBus.Clear<NeedStageChanged>();
            GameServices.Reset();
            foreach (Object o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private void OnAte(AteFood e) => _ate.Add(e);
        private void OnVomit(Vomited e) => _vomits.Add(e);

        /// <summary>One player's world: a save, a clock at 0, needs, a body and an eater over them.</summary>
        private sealed class Rig
        {
            public readonly TraitFixtures.MemorySave Save = new TraitFixtures.MemorySave();
            public readonly ZeroClock Clock = new ZeroClock();
            public readonly NeedsService Needs;
            public readonly BodyService Body;
            public readonly FoodBook Book;

            public Rig(FoodBook book, float hunger)
            {
                Book = book;
                var messes = new MessRegistry(() => Save);
                BodyService body = null;
                Needs = new NeedsService(() => Save, () => Clock, () => NeedsSettings.Default,
                                         () => new TraitService(() => Save, () => new TraitDef[0]), () => Hour,
                                         () => body);
                body = new BodyService(() => Save, () => Clock, () => BodySettings.Default,
                                       () => NeedsSettings.Default, () => Needs, () => messes, () => Hour);
                Body = body;
                Save.Current.PlayerNeeds = new PlayerNeedsDto
                {
                    Hunger = new NeedStateDto(hunger, 0d),
                    Tiredness = new NeedStateDto(0f, 0d),
                };
            }

            public FoodEater Eater(bool withBody = true)
                => new FoodEater(() => Book, () => Needs, () => Save, () => null,
                                 withBody ? () => Body : (System.Func<IBodyService>)(() => null),
                                 () => Vector2.zero, () => "region.test");

            public float Hunger => Needs.Level(NeedKind.Hunger);
        }

        private FoodDef Food(string id, string item, float relief, float kcal)
        {
            var def = ScriptableObject.CreateInstance<FoodDef>();
            def.Id = id; def.ItemId = item; def.EdibleRaw = true; def.HungerRelief = relief;
            def.PreppedId = ""; def.CookedId = ""; def.EnergyKcal = kcal;
            _made.Add(def);
            return def;
        }

        private static double KcalPerMeal
            => BodyMath.KcalPerHungerUnit(NeedsSettings.Default.Hunger.RisePerGameHour, BodySettings.Default);

        // ---- one path ------------------------------------------------------------------------------

        [Test]
        public void Eating_RelievesHungerExactlyOnce_ThroughTheBody()
        {
            var rig = new Rig(new FoodBook(new[] { Food("food.t_bread", "supply.t_bread", 0.3f, 600f) }), 0.8f);
            SupplyLocker.Add(rig.Save.Current, "supply.t_bread", 1);

            Assert.AreEqual(EatRefusal.None, rig.Eater().TryEatFromStock("supply.t_bread"));

            Assert.AreEqual(0.8f - 0.3f, rig.Hunger, 1e-6f, "the relief lands once, never twice");
            Assert.AreEqual(0.3f, rig.Body.StomachMeals, 1e-6f, "the body received the bite");
            Assert.AreEqual(1, _ate.Count);
        }

        [Test]
        public void FoodEnergy_PastWhatHungerWasShortOf_BecomesMass_Once()
        {
            const float kcal = 600f;
            var rig = new Rig(new FoodBook(new[] { Food("food.t_bread", "supply.t_bread", 0.3f, kcal) }), 0f);
            SupplyLocker.Add(rig.Save.Current, "supply.t_bread", 1);
            double before = rig.Body.MassKg;

            rig.Eater().TryEatFromStock("supply.t_bread");

            double expected = BodyMath.MealGainKg((float)(kcal / KcalPerMeal), 0f, KcalPerMeal, BodySettings.Default);
            Assert.Greater(expected, 0d, "a fed body eating stores something");
            Assert.AreEqual(before + expected, rig.Body.MassKg, 1e-9, "the food's own energy, stored once");
            Assert.AreEqual(0f, rig.Hunger, "already fed stays fed");
        }

        [Test]
        public void RicherFood_StoresMore_ThanLeanFood_OfTheSameFill()
        {
            var lean = new Rig(new FoodBook(new[] { Food("food.t_a", "supply.t_a", 0.3f, 100f) }), 0f);
            var rich = new Rig(new FoodBook(new[] { Food("food.t_a", "supply.t_a", 0.3f, 900f) }), 0f);
            SupplyLocker.Add(lean.Save.Current, "supply.t_a", 1);
            SupplyLocker.Add(rich.Save.Current, "supply.t_a", 1);
            double leanBefore = lean.Body.MassKg, richBefore = rich.Body.MassKg;

            lean.Eater().TryEatFromStock("supply.t_a");
            rich.Eater().TryEatFromStock("supply.t_a");

            Assert.Greater(rich.Body.MassKg - richBefore, lean.Body.MassKg - leanBefore);
            Assert.AreEqual(lean.Hunger, rich.Hunger, "the same fill relieves the same hunger");
        }

        [Test]
        public void HungryEater_StoresNothing_TheMealGoesToTheHunger()
        {
            var rig = new Rig(new FoodBook(new[] { Food("food.t_bread", "supply.t_bread", 0.3f, 600f) }), 0.9f);
            SupplyLocker.Add(rig.Save.Current, "supply.t_bread", 1);
            double before = rig.Body.MassKg;

            rig.Eater().TryEatFromStock("supply.t_bread");

            Assert.AreEqual(before, rig.Body.MassKg, "600 kcal is less than a 0.9-hungry body was short of");
        }

        // ---- overeating through real food ----------------------------------------------------------

        [Test]
        public void Overeating_RealFood_IsSick_TheUnitIsStillUsed_AndHungerIsUntouched()
        {
            var rig = new Rig(new FoodBook(new[] { Food("food.t_stew", "supply.t_stew", 0.5f, 0f) }), 0f);
            SupplyLocker.Add(rig.Save.Current, "supply.t_stew", 4);
            float capacity = BodySettings.Default.StomachCapacityMeals;
            Assert.AreEqual(1.5f, capacity, "the claim below is three 0.5 meals fill a 1.5 stomach");

            for (int i = 0; i < 3; i++)
                Assert.AreEqual(EatRefusal.None, rig.Eater().TryEatFromStock("supply.t_stew"));
            Assert.IsEmpty(_vomits, "a full stomach is not yet an overfull one");
            float hungerBefore = rig.Hunger;
            double massBefore = rig.Body.MassKg;

            Assert.AreEqual(EatRefusal.None, rig.Eater().TryEatFromStock("supply.t_stew"), "it was eaten...");

            Assert.AreEqual(1, _vomits.Count, "...and came back up, once");
            Assert.AreEqual("region.test", _vomits[0].RegionId);
            Assert.IsFalse(string.IsNullOrEmpty(_vomits[0].MessId), "a mess was left");
            Assert.AreEqual(0, SupplyLocker.Count(rig.Save.Current, "supply.t_stew"), "the unit is gone");
            Assert.AreEqual(hungerBefore, rig.Hunger, "a meal brought up feeds nothing");
            Assert.AreEqual(massBefore, rig.Body.MassKg, "and stores nothing");
            Assert.AreEqual(0f, rig.Body.StomachMeals, "the stomach is empty");
            Assert.AreEqual(4, _ate.Count, "every unit used is announced");
        }

        // ---- defaults ------------------------------------------------------------------------------

        [Test]
        public void ANewFoodDef_StatesNoEnergy()
        {
            var def = ScriptableObject.CreateInstance<FoodDef>();
            _made.Add(def);
            Assert.AreEqual(0f, def.EnergyKcal, "an asset written before the field reads 0, unstated");
        }

        [Test]
        public void UnstatedEnergy_EatsExactlyAsThePlainBodyMeal()
        {
            var food = new Rig(new FoodBook(new[] { Food("food.t_old", "supply.t_old", 0.4f, 0f) }), 0.1f);
            var plain = new Rig(new FoodBook(new FoodDef[0]), 0.1f);
            SupplyLocker.Add(food.Save.Current, "supply.t_old", 1);

            food.Eater().TryEatFromStock("supply.t_old");
            plain.Body.Eat(0.4f, Vector2.zero, "region.test");

            Assert.AreEqual(plain.Body.MassKg, food.Body.MassKg, 1e-12, "same mass");
            Assert.AreEqual(plain.Hunger, food.Hunger, "same hunger");
            Assert.AreEqual(plain.Body.StomachMeals, food.Body.StomachMeals, "same stomach");
        }

        [Test]
        public void WithNoBody_TheBiteFallsBackToTheNeeds_Once()
        {
            var rig = new Rig(new FoodBook(new[] { Food("food.t_bread", "supply.t_bread", 0.3f, 600f) }), 0.8f);
            SupplyLocker.Add(rig.Save.Current, "supply.t_bread", 1);

            rig.Eater(withBody: false).TryEatFromStock("supply.t_bread");

            Assert.AreEqual(0.5f, rig.Hunger, 1e-6f);
            Assert.AreEqual(0f, rig.Body.StomachMeals, "the body was never asked");
        }

        [Test]
        public void AnOldBodyImplementation_TakesTheEnergyMealAsThePlainMeal()
        {
            var old = new PlainOnlyBody();
            IBodyService seam = old;
            seam.Eat(0.3f, 600f, Vector2.zero, "region.test");
            Assert.AreEqual(1, old.Meals, "the default interface method forwards once");
            Assert.AreEqual(0.3f, old.LastMeals);
        }

        // ---- shipped content -----------------------------------------------------------------------

        [Test]
        public void ShippedFoods_StateNonNegativeEnergy_AndRawCatchStatesNone()
        {
            FoodBook book = FoodBook.FromResources();
            Assert.IsNotEmpty(book.All);
            foreach (FoodDef def in book.All)
            {
                Assert.GreaterOrEqual(def.EnergyKcal, 0f, def.Id);
                if (!def.EdibleRaw)
                    Assert.AreEqual(0f, def.EnergyKcal, def.Id + ": not edible raw, so its raw energy is 0");
            }
        }

        // ---- doubles -------------------------------------------------------------------------------

        internal sealed class ZeroClock : IGameClock
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

        /// <summary>A body written before the energy overload: only the plain meal.</summary>
        private sealed class PlainOnlyBody : IBodyService
        {
            public int Meals;
            public float LastMeals;
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
            public MealOutcome Eat(float meals, Vector2 position, string regionId)
            {
                Meals++;
                LastMeals = meals;
                return MealOutcome.Eaten;
            }
            public void Settle() { }
            public double ReportActivity(ActivityDef activity, float units) => 0d;
        }
    }
}
