using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// First food slice: eating lowers hunger by exactly the food's relief through the real
    /// <see cref="NeedsService"/>, clamps at fed, refuses a raw catch without using anything up, and says so
    /// once on the EventBus. Every Def and number here is the test's own, so retuning shipped food never
    /// breaks these.
    /// </summary>
    public class FoodEaterTests
    {
        private const double Hour = 100d;

        private readonly List<Object> _made = new List<Object>();
        private readonly List<AteFood> _ate = new List<AteFood>();
        private readonly List<EatRefused> _refused = new List<EatRefused>();
        private TraitFixtures.MemorySave _save;
        private FixedClock _clock;
        private FakeHands _hands;
        private NeedsService _needs;
        private FoodBook _book;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<AteFood>();
            EventBus.Clear<EatRefused>();
            EventBus.Subscribe<AteFood>(OnAte);
            EventBus.Subscribe<EatRefused>(OnRefused);
            _ate.Clear();
            _refused.Clear();
            _save = new TraitFixtures.MemorySave();
            _clock = new FixedClock();
            _hands = new FakeHands();
            _needs = new NeedsService(() => _save, () => _clock, () => NeedsSettings.Default,
                                      () => new TraitService(() => _save, () => new TraitDef[0]), () => Hour);
            _book = new FoodBook(new[]
            {
                Food("food.test_bread", "supply.test_bread", true, 0.3f),
                Food("food.test_cod", "fish.test_cod", false, 0f, "food.test_fillet", "food.test_fried"),
                Food("food.test_clam", "fish.test_clam", true, 0.1f),
            });
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<AteFood>(OnAte);
            EventBus.Unsubscribe<EatRefused>(OnRefused);
            EventBus.Clear<AteFood>();
            EventBus.Clear<EatRefused>();
            GameServices.Reset();
            foreach (Object o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private void OnAte(AteFood e) => _ate.Add(e);
        private void OnRefused(EatRefused e) => _refused.Add(e);

        private FoodDef Food(string id, string item, bool raw, float relief, string prepped = "", string cooked = "")
        {
            var def = ScriptableObject.CreateInstance<FoodDef>();
            def.Id = id; def.ItemId = item; def.EdibleRaw = raw; def.HungerRelief = relief;
            def.PreppedId = prepped; def.CookedId = cooked;
            _made.Add(def);
            return def;
        }

        // Since the food follow-up a bite goes through the body (which relieves hunger through the same
        // needs), so the eater is given a real body over the same save, clock and needs.
        private FoodEater Eater()
        {
            var messes = new MessRegistry(() => _save);
            var body = new BodyService(() => _save, () => _clock, () => BodySettings.Default,
                                       () => NeedsSettings.Default, () => _needs, () => messes, () => Hour);
            return new FoodEater(() => _book, () => _needs, () => _save, () => _hands, () => body,
                                 () => Vector2.zero, () => "region.test");
        }

        private void SetHunger(float level)
            => _save.Current.PlayerNeeds = new PlayerNeedsDto
            {
                Hunger = new NeedStateDto(level, 0d),
                Tiredness = new NeedStateDto(0f, 0d),
            };

        private static CatchItem Catch(string species) => new CatchItem(species, species, FishCategory.InshoreGroundfish, 1f, 5, 0f);

        // ---- eating from the locker ----------------------------------------------------------------

        [Test]
        public void EatFromStock_RelievesByExactlyTheDefAmount_AndUsesOneUnit()
        {
            SetHunger(0.8f);
            SupplyLocker.Add(_save.Current, "supply.test_bread", 2);

            Assert.AreEqual(EatRefusal.None, Eater().TryEatFromStock("supply.test_bread"));

            Assert.AreEqual(0.8f - 0.3f, _needs.Level(NeedKind.Hunger), 1e-6f, "hunger falls by the food's relief");
            Assert.AreEqual(1, SupplyLocker.Count(_save.Current, "supply.test_bread"), "one unit used up");
        }

        [Test]
        public void EatFromStock_ClampsAtFed()
        {
            SetHunger(0.1f);
            SupplyLocker.Add(_save.Current, "supply.test_bread", 1);

            Assert.AreEqual(EatRefusal.None, Eater().TryEatFromStock("supply.test_bread"));

            Assert.AreEqual(0f, _needs.Level(NeedKind.Hunger), "never below fed");
            Assert.AreEqual(0, SupplyLocker.Count(_save.Current, "supply.test_bread"));
        }

        [Test]
        public void EatFromStock_PublishesAteFoodOnce_WithTheIdsAndRelief()
        {
            SetHunger(0.5f);
            SupplyLocker.Add(_save.Current, "supply.test_bread", 1);

            Eater().TryEatFromStock("supply.test_bread");

            Assert.AreEqual(1, _ate.Count, "one signal per bite");
            Assert.AreEqual("food.test_bread", _ate[0].FoodId);
            Assert.AreEqual("supply.test_bread", _ate[0].ItemId);
            Assert.AreEqual(0.3f, _ate[0].Relief);
            Assert.IsEmpty(_refused);
        }

        [Test]
        public void EatFromStock_WithNone_IsRefused_AndChangesNothing()
        {
            SetHunger(0.5f);

            Assert.AreEqual(EatRefusal.NothingToEat, Eater().TryEatFromStock("supply.test_bread"));

            Assert.AreEqual(0.5f, _needs.Level(NeedKind.Hunger), 1e-6f);
            Assert.IsEmpty(_ate);
            Assert.AreEqual(1, _refused.Count);
            Assert.AreEqual(EatRefusal.NothingToEat, _refused[0].Reason);
        }

        [Test]
        public void EatFromStock_NotFood_IsRefused_AndKeepsTheStock()
        {
            SetHunger(0.5f);
            SupplyLocker.Add(_save.Current, "supply.ice", 1);

            Assert.AreEqual(EatRefusal.NotFood, Eater().TryEatFromStock("supply.ice"));

            Assert.AreEqual(1, SupplyLocker.Count(_save.Current, "supply.ice"), "ice is not eaten");
            Assert.AreEqual(0.5f, _needs.Level(NeedKind.Hunger), 1e-6f);
            Assert.AreEqual(1, _refused.Count);
            Assert.AreEqual("supply.ice", _refused[0].ItemId);
        }

        // ---- eating from the hands -------------------------------------------------------------------

        [Test]
        public void EatFromHand_RawCatch_IsRefused_AndStaysInTheHand()
        {
            SetHunger(0.6f);
            _hands.Held = Catch("fish.test_cod");

            Assert.AreEqual(EatRefusal.NotEdibleRaw, Eater().TryEatFromHand());

            Assert.IsTrue(_hands.Held.HasValue, "the cod is still in the hand");
            Assert.AreEqual(0, _hands.Takes, "nothing was taken");
            Assert.AreEqual(0.6f, _needs.Level(NeedKind.Hunger), 1e-6f, "hunger untouched");
            Assert.IsEmpty(_ate);
            Assert.AreEqual(1, _refused.Count, "refused once");
            Assert.AreEqual(EatRefusal.NotEdibleRaw, _refused[0].Reason);
            Assert.AreEqual("fish.test_cod", _refused[0].ItemId);
        }

        [Test]
        public void EatFromHand_EdibleRawCatch_IsTakenAndEaten()
        {
            SetHunger(0.6f);
            _hands.Held = Catch("fish.test_clam");

            Assert.AreEqual(EatRefusal.None, Eater().TryEatFromHand());

            Assert.IsFalse(_hands.Held.HasValue, "the hand is empty");
            Assert.AreEqual(1, _hands.Takes);
            Assert.AreEqual(0.6f - 0.1f, _needs.Level(NeedKind.Hunger), 1e-6f);
            Assert.AreEqual(1, _ate.Count);
            Assert.AreEqual("food.test_clam", _ate[0].FoodId);
        }

        [Test]
        public void EatFromHand_EmptyHands_IsRefused()
        {
            Assert.AreEqual(EatRefusal.NothingToEat, Eater().TryEatFromHand());
            Assert.AreEqual(1, _refused.Count);
            Assert.IsNull(_refused[0].ItemId);
        }

        [Test]
        public void Check_ChangesNothing_AndPublishesNothing()
        {
            Assert.AreEqual(EatRefusal.NotEdibleRaw, Eater().Check("fish.test_cod", out FoodDef cod));
            Assert.AreEqual("food.test_fried", cod.CookedId, "the cooked form is declared");
            Assert.AreEqual(EatRefusal.NotFood, Eater().Check("fish.unknown", out _));
            Assert.AreEqual(EatRefusal.NothingToEat, Eater().Check("", out _));
            Assert.IsEmpty(_ate);
            Assert.IsEmpty(_refused);
        }

        // ---- determinism and the book ----------------------------------------------------------------

        [Test]
        public void Eating_IsDeterministic_SameSaveSameBits()
        {
            float RunOnce()
            {
                SetHunger(0.73f);
                SupplyLocker.Add(_save.Current, "supply.test_bread", 1);
                Eater().TryEatFromStock("supply.test_bread");
                return _needs.Level(NeedKind.Hunger);
            }

            float a = RunOnce();
            float b = RunOnce();
            CollectionAssert.AreEqual(System.BitConverter.GetBytes(a), System.BitConverter.GetBytes(b));
        }

        [Test]
        public void FoodBook_SkipsBlankAndDuplicateIds_AndLowestIdWinsAnItem()
        {
            var book = new FoodBook(new[]
            {
                Food("food.b", "supply.same", true, 0.2f),
                Food("food.a", "supply.same", true, 0.1f),
                Food("food.a", "supply.other", true, 0.5f),
                Food("", "supply.blank", true, 0.5f),
                null,
            });

            Assert.AreEqual(2, book.All.Count);
            Assert.AreEqual("food.a", book.FindByItem("supply.same").Id, "ordinal lowest id, not load order");
            Assert.IsNull(book.FindByItem("supply.other"), "the duplicate id was skipped");
            Assert.IsNull(book.FindByItem("supply.blank"));
            Assert.IsNull(book.Find(null));
        }

        [Test]
        public void GameServices_Food_IsNeverNull_AndResetDropsIt()
        {
            FoodEater first = GameServices.Food;
            Assert.IsNotNull(first);
            GameServices.Reset();
            Assert.AreNotSame(first, GameServices.Food);
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
