using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;

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
        private FakeBagHands _bagHands;
        private ShoppingBagDef _paper;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractChooser.Reset();
            Interactables.Clear();
            InteractionGate.IsBlocked = false;
            InteractActionClaim.IsClaimed = false;
            InteractActorProbe.Clear();
            EventBus.Clear<AteFood>();
            EventBus.Clear<EatRefused>();
            EventBus.Subscribe<AteFood>(OnAte);
            EventBus.Subscribe<EatRefused>(OnRefused);
            _ate.Clear();
            _refused.Clear();
            _save = new TraitFixtures.MemorySave();
            _clock = new FixedClock();
            _hands = new FakeHands();
            _bagHands = new FakeBagHands();
            _needs = new NeedsService(() => _save, () => _clock, () => NeedsSettings.Default,
                                      () => new TraitService(() => _save, () => new TraitDef[0]), () => Hour);
            _book = new FoodBook(new[]
            {
                Food("food.test_bread", "supply.test_bread", true, 0.3f),
                Food("food.test_cod", "fish.test_cod", false, 0f, "food.test_fillet", "food.test_fried"),
                Food("food.test_clam", "fish.test_clam", true, 0.1f),
                Food("food.test_oatcakes", "supply.test_oatcakes", true, 0.2f),
            });
            _paper = ScriptableObject.CreateInstance<ShoppingBagDef>();
            _made.Add(_paper);
            _paper.Id = "shoppingbag.test_paper";
            _paper.Capacity = 10;
            _paper.PickUpReachMeters = 2f;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<AteFood>(OnAte);
            EventBus.Unsubscribe<EatRefused>(OnRefused);
            EventBus.Clear<AteFood>();
            EventBus.Clear<EatRefused>();
            InteractChooser.Reset();
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
            Interactables.Clear();
            GameServices.Reset();
        }

        private void OnAte(AteFood e) => _ate.Add(e);
        private void OnRefused(EatRefused e) => _refused.Add(e);

        private FoodDef Food(string id, string item, bool raw, float relief, string prepped = "", string cooked = "")
        {
            var def = ScriptableObject.CreateInstance<FoodDef>();
            def.Id = id; def.ItemId = item; def.EdibleRaw = raw; def.HungerRelief = relief;
            def.PreppedId = prepped; def.CookedId = cooked;
            def.BagEatLabel = "Eat " + id;
            _made.Add(def);
            return def;
        }

        // Since the food follow-up a bite goes through the body (which relieves hunger through the same
        // needs), so the eater is given a real body over the same save, clock and needs.
        private FoodEater Eater(bool liveHands = false, bool liveBag = false)
        {
            var messes = new MessRegistry(() => _save);
            var body = new BodyService(() => _save, () => _clock, () => BodySettings.Default,
                                       () => NeedsSettings.Default, () => _needs, () => messes, () => Hour);
            return new FoodEater(() => _book, () => _needs, () => _save,
                                 () => liveHands ? GameServices.CatchHands : _hands, () => body,
                                 () => Vector2.zero, () => "region.test",
                                 bagHands: () => liveBag ? GameServices.BagHands : _bagHands);
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

        private void Bag(string itemId, int count, int stored = 0)
        {
            SupplyLocker.Add(_save.Current, itemId, count + stored);
            for (int i = 0; i < count; i++)
                Assert.IsTrue(ShoppingBagContents.TryPut(_save.Current, itemId, 10));
            ShoppingBagContents.NoteBag(_save.Current, _paper);
        }

        [Test]
        public void EatFromBag_ConsumesExactlyOneReservedUnit_AndFeedsTheBodyOnce()
        {
            SetHunger(0.8f);
            Bag("supply.test_bread", 2, stored: 3);
            Bag("supply.ice", 1);
            FoodDef food = _book.FindByItem("supply.test_bread");

            Assert.AreEqual(EatRefusal.None, Eater().TryEatFromBag(food.ItemId));

            Assert.AreEqual(0.8f - food.HungerRelief, _needs.Level(NeedKind.Hunger), 1e-6f);
            Assert.AreEqual(food.HungerRelief, _save.Current.PlayerBody.StomachMeals);
            Assert.AreEqual(1, ShoppingBagContents.InBag(_save.Current, food.ItemId));
            Assert.AreEqual(4, SupplyLocker.Owned(_save.Current, food.ItemId));
            Assert.AreEqual(3, SupplyLocker.Count(_save.Current, food.ItemId), "stored food was not touched");
            Assert.AreEqual(1, ShoppingBagContents.InBag(_save.Current, "supply.ice"));
            Assert.AreEqual(1, _ate.Count);
            Assert.IsEmpty(_refused);
            Assert.AreEqual(0, _save.Saves, "eating is not a disk save point");
        }

        [Test]
        public void EatFromBag_LastUnitRemovesTheRow_AndCannotBeEatenTwice()
        {
            SetHunger(0.8f);
            Bag("supply.test_bread", 1);
            FoodEater eater = Eater();
            Assert.AreEqual(EatRefusal.None, eater.TryEatFromBag("supply.test_bread"));
            Assert.IsEmpty(_save.Current.ShoppingBags);
            Assert.AreEqual(0, SupplyLocker.Owned(_save.Current, "supply.test_bread"));
            Assert.AreEqual(EatRefusal.NothingToEat, eater.TryEatFromBag("supply.test_bread"));
            Assert.AreEqual(1, _ate.Count);
            Assert.AreEqual(0.5f, _needs.Level(NeedKind.Hunger), 1e-6f);
        }

        [TestCase("supply.ice", EatRefusal.NotFood)]
        [TestCase("fish.test_cod", EatRefusal.NotEdibleRaw)]
        public void EatFromBag_RefusedFoodLeavesEverySavedValueUntouched(string itemId, EatRefusal expected)
        {
            SetHunger(0.8f);
            Bag(itemId, 2);
            string before = SaveSerialization.ToJson(_save.Current);
            Assert.AreEqual(expected, Eater().TryEatFromBag(itemId));
            Assert.AreEqual(before, SaveSerialization.ToJson(_save.Current));
            Assert.IsEmpty(_ate);
            Assert.AreEqual(1, _refused.Count);
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void EatFromBag_RequiresBothPhysicalHandsAndHeldSaveRow(bool holdsBag, bool setDown)
        {
            SetHunger(0.8f);
            Bag("supply.test_bread", 2);
            _bagHands.HoldsBag = holdsBag;
            if (setDown) ShoppingBagContents.MarkSetDown(_save.Current, "region.away", Vector2.one, false);
            string before = SaveSerialization.ToJson(_save.Current);
            Assert.AreEqual(EatRefusal.NothingToEat, Eater().TryEatFromBag("supply.test_bread"));
            Assert.AreEqual(before, SaveSerialization.ToJson(_save.Current));
            Assert.IsEmpty(_ate);
        }

        [Test]
        public void EatFromBag_RejectsBrokenReservationWithoutReleasingAnyOfIt()
        {
            Bag("supply.test_bread", 2);
            _save.Current.SupplyStock.Clear();
            string before = SaveSerialization.ToJson(_save.Current);
            Assert.AreEqual(EatRefusal.NothingToEat, Eater().TryEatFromBag("supply.test_bread"));
            Assert.AreEqual(before, SaveSerialization.ToJson(_save.Current));
        }

        [Test]
        public void EatFromBag_CannotReachStoredFoodEvenWhenAnotherGoodIsCarried()
        {
            SupplyLocker.Add(_save.Current, "supply.test_bread", 1);
            Bag("supply.ice", 1);
            string before = SaveSerialization.ToJson(_save.Current);
            Assert.AreEqual(EatRefusal.NothingToEat, Eater().TryEatFromBag("supply.test_bread"));
            Assert.AreEqual(before, SaveSerialization.ToJson(_save.Current));
        }

        [Test]
        public void BagEating_SameSeedTimeAndChoices_ProducesIdenticalSavedState()
        {
            string RunOnce()
            {
                _save = new TraitFixtures.MemorySave();
                _save.Current.WorldSeed = 719;
                _clock.Seconds = 5d * Hour;
                _save.Current.GameTimeSeconds = _clock.Seconds;
                SetHunger(0.6f);
                Bag("supply.test_bread", 2);
                Assert.AreEqual(EatRefusal.None, Eater().TryEatFromBag("supply.test_bread"));
                return SaveSerialization.ToJson(_save.Current);
            }
            Assert.AreEqual(RunOnce(), RunOnce());
        }

        [Test]
        public void BagEating_RoundTripsThroughTheExistingSaveRows()
        {
            SetHunger(0.8f);
            Bag("supply.test_bread", 2);
            Assert.AreEqual(EatRefusal.None, Eater().TryEatFromBag("supply.test_bread"));
            SaveData loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(_save.Current));
            Assert.AreEqual(26, loaded.SchemaVersion);
            Assert.AreEqual(1, SupplyLocker.Owned(loaded, "supply.test_bread"));
            Assert.AreEqual(1, ShoppingBagContents.InBag(loaded, "supply.test_bread"));
            Assert.AreEqual(_save.Current.PlayerNeeds.Hunger.Level, loaded.PlayerNeeds.Hunger.Level);
            Assert.AreEqual(_save.Current.PlayerBody.StomachMeals, loaded.PlayerBody.StomachMeals);
        }

        private static InteractActor Actor => InteractActor.For(Vector2.zero, Vector2.right, ControlMode.OnFoot);

        private InteractChoiceList Choices()
        {
            var rows = new InteractChoiceList();
            rows.Build(Interactables.Active, Actor, InteractResolver.DefaultFacingArcDegrees,
                       GameConfig.DefaultInteractChooserMaxPriority, GameConfig.DefaultInteractChooserMaxRows);
            return rows;
        }

        [TestCase(null, 0)]
        [TestCase("tool.shovel", 0)]
        [TestCase("fish.test_cod", 0)]
        [TestCase("fish.test_clam", 1)]
        public void Chooser_OffersEatOnlyForEdibleFoodInHand(string itemId, int rowCount)
        {
            FoodEater eater = Eater();
            var choice = new HeldFoodInteraction(_book.FindByItem("fish.test_clam"), () => Vector2.zero, () => eater);
            Interactables.Register(choice);
            _hands.Held = itemId == null ? (CatchItem?)null : Catch(itemId);
            SupplyLocker.Add(_save.Current, "supply.test_bread", 1);
            var rows = Choices();
            Assert.AreEqual(rowCount, rows.Count);
            if (rowCount == 1) Assert.AreEqual("Eat", rows[0].Label);
            Assert.AreEqual(0, _hands.Takes);
            Assert.IsEmpty(_ate);
            Assert.IsEmpty(_refused);
        }

        [Test]
        public void HeldFoodPress_ConsumesOneAndUsesTheDefRelief_EvenAtFullHunger()
        {
            SetHunger(1f);
            _hands.Held = Catch("fish.test_clam");
            FoodEater eater = Eater();
            FoodDef food = _book.FindByItem("fish.test_clam");
            Interactables.Register(new HeldFoodInteraction(food, () => Vector2.zero, () => eater));
            Assert.IsTrue(InteractVerb.TryPress(Actor, InteractResolver.DefaultFacingArcDegrees));
            Assert.AreEqual(1, _hands.Takes);
            Assert.AreEqual(1f - food.HungerRelief, _needs.Level(NeedKind.Hunger), 1e-6f);
            Assert.AreEqual(1, _ate.Count);
            Assert.AreEqual(0, Choices().Count);
        }

        [Test]
        public void HeldFood_RefusedTakeChangesNeitherFoodNorHunger()
        {
            SetHunger(0.8f);
            _hands.Held = Catch("fish.test_clam");
            _hands.RefuseTake = true;
            Assert.AreEqual(EatRefusal.NothingToEat, Eater().TryEatFromHand());
            Assert.IsTrue(_hands.Held.HasValue);
            Assert.AreEqual(0, _hands.Takes);
            Assert.AreEqual(0.8f, _needs.Level(NeedKind.Hunger));
            Assert.IsEmpty(_ate);
        }

        [Test]
        public void HeldFood_StaleActionCannotEatADifferentEdibleItem()
        {
            FoodEater eater = Eater();
            var choice = new HeldFoodInteraction(_book.FindByItem("fish.test_clam"), () => Vector2.zero, () => eater);
            _hands.Held = Catch("fish.test_clam");
            Assert.IsTrue(choice.IsAvailable);
            _hands.Held = Catch("supply.test_bread");
            choice.Interact(Actor);
            Assert.AreEqual(0, _hands.Takes);
            Assert.AreEqual("supply.test_bread", _hands.Held.Value.SpeciesId);
        }

        private CarriableShoppingBag CarriedBag(int bread, bool secondFood = false)
        {
            GameServices.Save = _save;
            // Use the real carried-bag contract for these tests, and the same real body as the service tests.
            GameServices.Food = Eater(liveBag: true);
            var player = new GameObject("food test carrier");
            _made.Add(player);
            var hands = player.AddComponent<CarryHands>();
            SupplyLocker.Add(_save.Current, "supply.test_bread", bread);
            for (int i = 0; i < bread; i++)
                Assert.AreEqual(ShoppingBagRefusal.None, hands.TryBag(_paper, "supply.test_bread"));
            if (secondFood)
            {
                SupplyLocker.Add(_save.Current, "supply.test_oatcakes", 1);
                Assert.AreEqual(ShoppingBagRefusal.None, hands.TryBag(_paper, "supply.test_oatcakes"));
            }
            return hands.HeldBag;
        }

        [Test]
        public void BagChooser_KeepsSetDown_AndEatingTheLastUnitFreesTheActualHand()
        {
            SetHunger(0.8f);
            CarriableShoppingBag bag = CarriedBag(1);
            CarryHands hands = (CarryHands)GameServices.BagHands;
            Assert.IsTrue(InteractVerb.TryPress(Actor, InteractResolver.DefaultFacingArcDegrees));
            Assert.IsTrue(InteractChooser.IsOpen);
            Assert.AreEqual(2, InteractChooser.Rows.Count);
            Assert.AreEqual(CarriableShoppingBag.SetDownLabel, InteractChooser.Rows[0].Label);
            Assert.AreEqual(_book.FindByItem("supply.test_bread").BagEatLabel, InteractChooser.Rows[1].Label);
            Assert.AreEqual(0, _ate.Count, "opening is not eating");
            Assert.IsTrue(InteractChooser.Pick(1));
            Assert.AreEqual(1, _ate.Count);
            Assert.AreEqual(0.5f, _needs.Level(NeedKind.Hunger), 1e-6f);
            Assert.IsFalse(hands.HoldsBag);
            Assert.IsTrue(hands.Slots.IsEmpty);
            Assert.IsTrue(bag == null, "empty bag is removed through the existing carry cleanup");
        }

        [Test]
        public void BagChooser_CancelConsumesNothing()
        {
            CarriedBag(1);
            string before = SaveSerialization.ToJson(_save.Current);
            Assert.IsTrue(InteractVerb.TryPress(Actor, InteractResolver.DefaultFacingArcDegrees));
            InteractChooser.Cancel();
            Assert.AreEqual(before, SaveSerialization.ToJson(_save.Current));
            Assert.IsEmpty(_ate);
        }

        [Test]
        public void BagChooser_AChangedBagCannotRetargetAFrozenFoodRow()
        {
            CarriedBag(1, secondFood: true);
            Assert.IsTrue(InteractVerb.TryPress(Actor, InteractResolver.DefaultFacingArcDegrees));
            Assert.AreEqual(3, InteractChooser.Rows.Count);
            Assert.AreEqual(_book.FindByItem("supply.test_bread").BagEatLabel, InteractChooser.Rows[1].Label);
            Assert.AreEqual(EatRefusal.None, GameServices.Food.TryEatFromBag("supply.test_bread"));
            Assert.IsTrue(InteractChooser.Pick(1));
            Assert.AreEqual(1, _ate.Count, "the vanished bread row must not become the oatcakes row");
            Assert.AreEqual(1, ShoppingBagContents.InBag(_save.Current, "supply.test_oatcakes"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BagChooser_SelectsTheNamedFoodRegardlessOfPurchaseOrder(bool reverse)
        {
            SetHunger(0.8f);
            CarriedBag(2, secondFood: true);
            if (reverse) _save.Current.ShoppingBags[0].Items.Reverse();
            Assert.IsTrue(InteractVerb.TryPress(Actor, InteractResolver.DefaultFacingArcDegrees));
            Assert.AreEqual(3, InteractChooser.Rows.Count);
            FoodDef food = _book.FindByItem("supply.test_oatcakes");
            Assert.AreEqual(food.BagEatLabel, InteractChooser.Rows[2].Label);
            Assert.IsTrue(InteractChooser.Pick(2));
            Assert.AreEqual(1, _ate.Count);
            Assert.AreEqual(food.ItemId, _ate[0].ItemId);
            Assert.AreEqual(0.8f - food.HungerRelief, _needs.Level(NeedKind.Hunger), 1e-6f);
            Assert.AreEqual(0, SupplyLocker.Owned(_save.Current, food.ItemId));
            Assert.AreEqual(2, ShoppingBagContents.InBag(_save.Current, "supply.test_bread"));
        }

        [Test]
        public void BagContainingOnlyNonFoodKeepsItsSingleExistingAction()
        {
            CarriableShoppingBag bag = CarriedBag(1);
            Assert.IsTrue(ShoppingBagContents.TryTakeOut(_save.Current, "supply.test_bread"));
            Assert.IsTrue(SupplyLocker.TrySpend(_save.Current, "supply.test_bread"));
            Bag("supply.ice", 1);
            var rows = Choices();
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(CarriableShoppingBag.SetDownLabel, rows[0].Label);
            Assert.IsFalse(rows.IsChoice);
            Assert.IsTrue(bag.IsCarried);
        }

        [Test]
        public void EatFromBag_UsesTheFoodsEnergyAndExistingOvereatingRules()
        {
            SetHunger(0f);
            FoodDef food = _book.FindByItem("supply.test_bread");
            food.EnergyKcal = 600f;
            Bag(food.ItemId, 2);
            var body = new BodyService(() => _save, () => _clock, () => BodySettings.Default,
                () => NeedsSettings.Default, () => _needs, () => new MessRegistry(() => _save), () => Hour);
            double mass = body.MassKg;
            double unit = BodyMath.KcalPerHungerUnit(NeedsSettings.Default.Hunger.RisePerGameHour, BodySettings.Default);
            double gain = BodyMath.MealGainKg((float)(food.EnergyKcal / unit), 0f, unit, BodySettings.Default);
            Assert.AreEqual(EatRefusal.None, Eater().TryEatFromBag(food.ItemId));
            Assert.AreEqual(mass + gain, body.MassKg, 1e-9);
            Assert.Greater(gain, 0d);
            SetHunger(0.8f);
            _save.Current.PlayerBody.StomachMeals = BodySettings.Default.StomachCapacityMeals;
            _save.Current.PlayerBody.StomachSinceGameSeconds = _clock.TotalSeconds;
            int vomits = 0;
            System.Action<Vomited> onVomit = _ => vomits++;
            EventBus.Subscribe(onVomit);
            try
            {
                Assert.AreEqual(EatRefusal.None, Eater().TryEatFromBag(food.ItemId));
                Assert.AreEqual(1, vomits);
                Assert.AreEqual(0.8f, _needs.Level(NeedKind.Hunger));
                Assert.AreEqual(0, SupplyLocker.Owned(_save.Current, food.ItemId));
            }
            finally { EventBus.Unsubscribe(onVomit); }
        }

        [Test]
        public void BagChooser_SetDownBeforeConfirmationRefusesTheBite()
        {
            CarriedBag(1);
            Assert.IsTrue(InteractVerb.TryPress(Actor, InteractResolver.DefaultFacingArcDegrees));
            ShoppingBagContents.MarkSetDown(_save.Current, "region.test", Vector2.zero, false);
            Assert.IsTrue(InteractChooser.Pick(1));
            Assert.IsEmpty(_ate);
            Assert.AreEqual(1, ShoppingBagContents.InBag(_save.Current, "supply.test_bread"));
        }

        [Test]
        public void RealHands_RegisterAndRemoveTheHeldFoodChoiceAcrossTheirLifetime()
        {
            SetHunger(0.8f);
            GameServices.Save = _save;
            GameServices.Food = Eater(liveHands: true);
            var player = new GameObject("food test carrier");
            _made.Add(player);
            CarryHands hands = player.AddComponent<CarryHands>();
            Assert.AreEqual(0, Interactables.Count);
            Assert.IsTrue(hands.TryPutInHand(Catch("fish.test_clam")));
            Assert.AreEqual(1, Interactables.Count);
            player.SetActive(false);
            Assert.AreEqual(0, Interactables.Count);
            player.SetActive(true);
            Assert.AreEqual(1, Interactables.Count);
            Assert.AreEqual("Eat", Choices()[0].Label);
            Assert.IsTrue(InteractVerb.TryPress(Actor, InteractResolver.DefaultFacingArcDegrees));
            Assert.IsFalse(hands.TryPeekHand(out _));
            Assert.AreEqual(1, _ate.Count);
            Assert.AreEqual(0.7f, _needs.Level(NeedKind.Hunger), 1e-6f);
            Assert.AreEqual(0, Interactables.Count);
        }

        private object _allocationSink;

        [Test]
        public void FoodChooser_WarmedQueriesAllocateNoMemory()
        {
            CarriedBag(2, secondFood: true);
            FoodEater eater = Eater();
            Interactables.Register(new HeldFoodInteraction(_book.FindByItem("fish.test_clam"),
                () => Vector2.zero, () => eater));
            var rows = new InteractChoiceList();
            void Query()
            {
                for (int i = 0; i < 100; i++)
                    rows.Build(Interactables.Active, Actor, InteractResolver.DefaultFacingArcDegrees,
                        GameConfig.DefaultInteractChooserMaxPriority, GameConfig.DefaultInteractChooserMaxRows);
            }
            Query();
            Assert.That(() => { _allocationSink = new object[64]; },
                UnityEngine.TestTools.Constraints.Is.AllocatingGCMemory(), "the recorder must detect a known allocation");
            Assert.That(() => Query(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.AreEqual(3, rows.Count);
            _hands.Held = Catch("fish.test_clam");
            Query();
            Assert.That(() => Query(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("Eat", rows[0].Label);
        }

        // ---- doubles -----------------------------------------------------------------------------------

        private sealed class FakeHands : ICatchHands
        {
            public CatchItem? Held;
            public int Takes;
            public bool RefuseTake;

            public bool TryPutInHand(in CatchItem item)
            {
                if (Held.HasValue) return false;
                Held = item;
                return true;
            }

            public bool TryTakeFromHand(out CatchItem item)
            {
                item = Held ?? default;
                if (!Held.HasValue || RefuseTake) return false;
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
            public double Seconds;
            public double TotalSeconds => Seconds;
            public GameTime Now => new GameTime(Seconds);
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

        private sealed class FakeBagHands : IShoppingBagHands
        {
            public bool HoldsBag { get; set; } = true;
            public bool BagIsSetDown => false;
            public ShoppingBagRefusal CanBag(ShoppingBagDef bag) => ShoppingBagRefusal.None;
            public ShoppingBagRefusal TryBag(ShoppingBagDef bag, string supplyId) => ShoppingBagRefusal.None;
            public bool TryPutAway(string storageId) => false;
        }
    }
}
