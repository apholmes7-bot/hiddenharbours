using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The food stores (kitchens slice 1, owner ask 2026-11-15: "food storage, a fridge, cooler which needs
    /// ice, and pantry"). Pins the cooler's melt as an exact instant, the capacity cap, oldest-first takes,
    /// a pantry that is never cold, a fridge whose power change is banked, packing ice from the supply
    /// locker, the once-only "ice ran out" signal, and the determinism rule: settling once across a long
    /// stretch lands where settling every tick does (rule 5).
    /// </summary>
    public class KitchenStorageModelTests
    {
        const float SecondsPerDay = 2400f;      // 100 game seconds to the game hour
        const string Food = "food.test_chowder";
        const string Key = "fixture.boat.test.cooler";

        /// <summary>Iced food still spoils, a quarter as fast, so the protected leg is visible in the sums.</summary>
        static readonly SpoilPolicy Policy = new SpoilPolicy(1f, 0f, 0f, 0.25f, 0.9f);

        sealed class FixedPerishability : IFoodPerishability
        {
            public float SpoilPerDay(string itemId) => itemId == Food ? 0.5f : 0f;
        }

        sealed class SwitchPower : IKitchenPower
        {
            public bool On = true;
            public bool IsPowered(string storageKey) => On;
        }

        readonly List<Object> _made = new List<Object>();
        readonly List<KitchenIceRanOut> _ranOut = new List<KitchenIceRanOut>();
        System.Action<KitchenIceRanOut> _onRanOut;

        [SetUp]
        public void SetUp()
        {
            _ranOut.Clear();
            _onRanOut = _ranOut.Add;
            EventBus.Subscribe(_onRanOut);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_onRanOut);
            foreach (Object o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        KitchenStorageDef Def(KitchenStorageKind kind, int capacity = 12, int iceUnits = 2, float iceHours = 12f)
        {
            var def = ScriptableObject.CreateInstance<KitchenStorageDef>();
            def.Id = "kitchen_storage.test_" + kind.ToString().ToLowerInvariant();
            def.Kind = kind;
            def.CapacityItems = capacity;
            def.ColdMode = StorageMode.Iced;
            def.IceCapacityUnits = iceUnits;
            def.IceHoursPerUnit = iceHours;
            _made.Add(def);
            return def;
        }

        static KitchenContext At(double t, IFoodPerishability p = null)
            => new KitchenContext(t, SecondsPerDay, Policy, p ?? new FixedPerishability());

        static KitchenStorageStateDto Empty(string defId) => new KitchenStorageStateDto { Key = Key, StorageDefId = defId };

        [Test]
        public void TheCoolersIceRunsOutAtOneExactInstant()
        {
            KitchenStorageDef cooler = Def(KitchenStorageKind.Cooler);
            KitchenStorageStateDto store = Empty(cooler.Id);

            Assert.AreEqual(2, KitchenStorageModel.PackIce(store, cooler, At(1000d), 5), "never more bags than it holds");
            Assert.AreEqual(1000d + 2 * 12 * 100d, store.IceRunsOutAtGameSeconds, "two twelve-hour bags from 1000 s");

            Assert.AreEqual(2, KitchenStorageModel.IceUnitsLeft(store, cooler, At(1000d)));
            Assert.AreEqual(2, KitchenStorageModel.IceUnitsLeft(store, cooler, At(2199d)), "a bag half gone is still a bag");
            Assert.AreEqual(1, KitchenStorageModel.IceUnitsLeft(store, cooler, At(2201d)));
            Assert.IsTrue(KitchenStorageModel.IsCold(store, cooler, At(3399d)));
            Assert.IsFalse(KitchenStorageModel.IsCold(store, cooler, At(3400d)), "the instant it runs out it is just a box");
            Assert.AreEqual(0, KitchenStorageModel.IceUnitsLeft(store, cooler, At(3400d)));
        }

        [Test]
        public void IceRoomAndTopUpExtendFromTheOldMeltInstant()
        {
            KitchenStorageDef cooler = Def(KitchenStorageKind.Cooler);
            KitchenStorageStateDto store = Empty(cooler.Id);
            KitchenStorageModel.PackIce(store, cooler, At(0d), 2);
            Assert.AreEqual(0, KitchenStorageModel.IceRoom(store, cooler, At(0d)), "full");
            Assert.AreEqual(1, KitchenStorageModel.IceRoom(store, cooler, At(1300d)), "a bag's worth gone");
            Assert.AreEqual(1, KitchenStorageModel.PackIce(store, cooler, At(1300d), 3));
            Assert.AreEqual(2400d + 1200d, store.IceRunsOutAtGameSeconds, "a top-up extends from the old melt instant");
        }

        [Test]
        public void AStoreHoldsNoMoreThanItsCapacity()
        {
            KitchenStorageDef pantry = Def(KitchenStorageKind.Pantry, capacity: 3);
            KitchenStorageStateDto store = Empty(pantry.Id);
            Assert.AreEqual(3, KitchenStorageModel.Store(store, pantry, At(0d), Food, 5, Freshness.Landed(0d)));
            Assert.AreEqual(0, KitchenStorageModel.Free(store, pantry));
            Assert.AreEqual(0, KitchenStorageModel.Store(store, pantry, At(10d), Food, 1, Freshness.Landed(10d)));
            Assert.AreEqual(3, KitchenStorageModel.Total(store));
        }

        [Test]
        public void TakingFoodOutTakesTheOldestFirst()
        {
            KitchenStorageDef pantry = Def(KitchenStorageKind.Pantry);
            KitchenStorageStateDto store = Empty(pantry.Id);
            IFoodPerishability none = NothingSpoilsPerishability.Instance;
            KitchenStorageModel.Store(store, pantry, At(0d, none), Food, 2, new FreshnessState(0.3f, 0d, StorageMode.Ambient));
            KitchenStorageModel.Store(store, pantry, At(50d, none), Food, 2, new FreshnessState(0f, 50d, StorageMode.Ambient));

            var taken = new List<KitchenStackDto>();
            Assert.AreEqual(3, KitchenStorageModel.Take(store, pantry, At(60d, none), Food, 3, taken));
            Assert.AreEqual(2, taken.Count);
            Assert.AreEqual(2, taken[0].Count);
            Assert.AreEqual(0.3f, taken[0].SpoilAccrued, 1e-6f, "the older lot came out first");
            Assert.AreEqual(1, taken[1].Count);
            Assert.AreEqual(0f, taken[1].SpoilAccrued, 1e-6f);
            Assert.AreEqual(1, KitchenStorageModel.Count(store, Food));
        }

        [Test]
        public void APantryIsNeverCold_AndTakesNoIce()
        {
            KitchenStorageDef pantry = Def(KitchenStorageKind.Pantry);
            KitchenStorageStateDto store = Empty(pantry.Id);
            Assert.AreEqual(0, KitchenStorageModel.PackIce(store, pantry, At(0d), 2));
            Assert.IsFalse(KitchenStorageModel.IsCold(store, pantry, At(0d)));
            Assert.AreEqual(StorageMode.Ambient, KitchenStorageModel.HeldMode(store, pantry, At(0d)));

            KitchenStorageModel.Store(store, pantry, At(0d), Food, 1, Freshness.Landed(0d));
            Assert.AreEqual(0.5f, KitchenStorageModel.SpoilAt(store, pantry, store.Stacks[0], At(SecondsPerDay)), 1e-5f,
                            "a day in a pantry is a day in the open");
        }

        [Test]
        public void AFridgeLosingPowerBanksTheColdStretchFirst()
        {
            KitchenStorageDef fridge = Def(KitchenStorageKind.Fridge, capacity: 30);
            KitchenStorageStateDto store = Empty(fridge.Id);
            KitchenStorageModel.Store(store, fridge, At(0d), Food, 1, Freshness.Landed(0d));
            Assert.AreEqual((int)StorageMode.Iced, store.Stacks[0].Mode, "a powered fridge holds its food cold");

            KitchenStorageModel.SetPower(store, fridge, At(1200d), false);
            Assert.AreEqual(0.5f * 0.25f * 0.5f, store.Stacks[0].SpoilAccrued, 1e-6f, "half a day cold, banked at the change");
            Assert.AreEqual((int)StorageMode.Ambient, store.Stacks[0].Mode);
            Assert.AreEqual(0.0625f + 0.25f, KitchenStorageModel.SpoilAt(store, fridge, store.Stacks[0], At(2400d)), 1e-5f,
                            "then half a day warm");
        }

        [Test]
        public void SettlingOnceAcrossTheMeltEqualsSettlingEveryTick()
        {
            KitchenStorageDef cooler = Def(KitchenStorageKind.Cooler);
            var power = new SwitchPower();
            KitchenStorageStateDto oneShot = Empty(cooler.Id), stepped = Empty(cooler.Id);
            foreach (KitchenStorageStateDto s in new[] { oneShot, stepped })
            {
                KitchenStorageModel.PackIce(s, cooler, At(0d), 1);
                KitchenStorageModel.Store(s, cooler, At(0d), Food, 4, Freshness.Landed(0d));
            }

            for (double t = 70d; t <= 3010d; t += 70d)
            {
                KitchenStorageModel.Settle(stepped, cooler, At(t));
                KitchenStores.Tick(new SaveData { KitchenStores = new List<KitchenStorageStateDto> { stepped } },
                                   At(t), id => cooler, power);
            }
            KitchenStorageModel.Settle(stepped, cooler, At(3010d));
            KitchenStorageModel.Settle(oneShot, cooler, At(3010d));

            // 1200 s iced at a quarter rate, then 1810 s in the open: 0.5/day x (1200/2400 x 0.25 + 1810/2400).
            float expected = 0.5f * (1200f / 2400f * 0.25f + 1810f / 2400f);
            Assert.AreEqual(expected, oneShot.Stacks[0].SpoilAccrued, 1e-5f);
            Assert.AreEqual(oneShot.Stacks[0].SpoilAccrued, stepped.Stacks[0].SpoilAccrued, 1e-5f, "catch-up equals stepping");
            Assert.AreEqual((int)StorageMode.Ambient, oneShot.Stacks[0].Mode);
            Assert.AreEqual((int)StorageMode.Ambient, stepped.Stacks[0].Mode);
        }

        [Test]
        public void PackingFromTheLockerSpendsTheIce_AndNoMoreThanFits()
        {
            KitchenStorageDef cooler = Def(KitchenStorageKind.Cooler);
            SaveData save = SaveMigration.NewGame();
            Assert.AreEqual(0, KitchenStores.PackIceFromLocker(save, Key, cooler, At(0d), 2), "no ice in the locker, none packed");

            SupplyLocker.Add(save, cooler.IceSupplyId, 5);
            Assert.AreEqual(2, KitchenStores.PackIceFromLocker(save, Key, cooler, At(0d), 4));
            Assert.AreEqual(3, SupplyLocker.Count(save, cooler.IceSupplyId), "only what went in left the locker");
            Assert.AreEqual(2400d, KitchenStores.Find(save, Key).IceRunsOutAtGameSeconds);
        }

        [Test]
        public void TheIceRanOutSignalIsRaisedOnce_AtTheMeltInstant()
        {
            KitchenStorageDef cooler = Def(KitchenStorageKind.Cooler);
            SaveData save = SaveMigration.NewGame();
            KitchenStorageStateDto store = KitchenStores.Open(save, Key, cooler);
            KitchenStorageModel.PackIce(store, cooler, At(0d), 1);

            KitchenStores.Tick(save, At(1199d), id => cooler, null);
            Assert.AreEqual(0, _ranOut.Count, "still cold");
            KitchenStores.Tick(save, At(1500d), id => cooler, null);
            KitchenStores.Tick(save, At(1600d), id => cooler, null);
            Assert.AreEqual(1, _ranOut.Count, "once per packing");
            Assert.AreEqual(1200d, _ranOut[0].RanOutAtGameSeconds, "the melt instant, not the tick that noticed");

            KitchenStorageModel.PackIce(store, cooler, At(2000d), 1);
            KitchenStores.Tick(save, At(3300d), id => cooler, null);
            Assert.AreEqual(2, _ranOut.Count, "a fresh packing can run out again");
        }

        [Test]
        public void AStoreReopenedUnderANewDefKeepsItsFood()
        {
            KitchenStorageDef pantry = Def(KitchenStorageKind.Pantry);
            KitchenStorageDef locker = Def(KitchenStorageKind.Pantry);
            locker.Id = "kitchen_storage.test_locker";
            SaveData save = SaveMigration.NewGame();
            KitchenStores.Store(save, Key, pantry, At(0d), Food, 2, Freshness.Landed(0d));
            KitchenStorageStateDto again = KitchenStores.Open(save, Key, locker);
            Assert.AreEqual(locker.Id, again.StorageDefId);
            Assert.AreEqual(2, KitchenStorageModel.Count(again, Food));
            Assert.AreEqual(1, save.KitchenStores.Count);
        }
    }
}
