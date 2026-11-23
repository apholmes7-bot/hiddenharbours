using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Buying at the shelf, slice 2, the logic under the paper bag and the separate counts (owner ruling
    /// 2026-11-23: the player fills a bag with several items and puts them into his storage by hand; every
    /// item runs out on its own). A bagged good is bought into his supplies and set aside: it cannot be eaten
    /// or cooked until it is put away, the bag holds no more than its capacity, a different game ignores the
    /// bag, and putting away releases everything at once. The save never changes shape: reload, and the goods
    /// are found put away. Shelf lines sharing one good count and sell out separately and deterministically.
    /// </summary>
    public class ShoppingBagTests
    {
        private const string Bread = "supply.test_bread";
        private const string Beans = "supply.test_beans";
        private const int Capacity = 3;

        private const double SecondsPerDay = 1800.0;
        private const double Opens = 7.0, Closes = 19.0;
        private const int Cadence = 2, Lookback = 28;
        private const double BufferDays = 4.0;
        private static readonly MassVolume Plenty = new MassVolume(1_000_000.0, 1_000_000.0);
        private const string Shop = "shop.test_store";

        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp() => ShoppingBagContents.Clear();

        [TearDown]
        public void TearDown()
        {
            ShoppingBagContents.Clear();
            foreach (UnityEngine.Object o in _spawned)
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- the bag's contents ------------------------------------------------------------------------------

        [Test]
        public void ABaggedGoodIsOwnedButSetAside_UntilItIsPutAway()
        {
            var save = new SaveData();
            SupplyLocker.Add(save, Bread, 2);
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));

            Assert.That(SupplyLocker.Owned(save, Bread), Is.EqualTo(2), "bought: it is in the save");
            Assert.That(SupplyLocker.Count(save, Bread), Is.EqualTo(1), "one is in the bag, not in the cupboard");
            Assert.That(ShoppingBagContents.InBag(save, Bread), Is.EqualTo(1));
            Assert.That(ShoppingBagContents.Total(save), Is.EqualTo(1));
            Assert.IsTrue(ShoppingBagContents.HoldsAny(save));

            Assert.That(ShoppingBagContents.Release(save), Is.EqualTo(1));
            Assert.That(SupplyLocker.Count(save, Bread), Is.EqualTo(2), "put away: all of it is in his storage");
            Assert.IsFalse(ShoppingBagContents.HoldsAny(save));
        }

        [Test]
        public void ABaggedGoodCannotBeEatenOrCooked_FromTheBag()
        {
            var save = new SaveData();
            SupplyLocker.Add(save, Bread, 1);
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            Assert.IsFalse(SupplyLocker.Has(save, Bread));
            Assert.IsFalse(SupplyLocker.TrySpend(save, Bread), "the only loaf is in the bag");
            Assert.That(SupplyLocker.Owned(save, Bread), Is.EqualTo(1), "a refused spend takes nothing");
        }

        [Test]
        public void TheBagHoldsNoMoreThanItsCapacity_AcrossGoods()
        {
            var save = new SaveData();
            SupplyLocker.Add(save, Bread, 5);
            SupplyLocker.Add(save, Beans, 5);
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Beans, Capacity));
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            Assert.IsFalse(ShoppingBagContents.TryPut(save, Beans, Capacity), "full");
            Assert.That(ShoppingBagContents.Total(save), Is.EqualTo(Capacity));
            Assert.That(ShoppingBagContents.InBag(save, Bread), Is.EqualTo(2));
            Assert.That(ShoppingBagContents.InBag(save, Beans), Is.EqualTo(1));
            Assert.That(ShoppingBagContents.LineCount(save), Is.EqualTo(2));
        }

        [Test]
        public void NothingGoesInTheBagThatHeDoesNotOwn()
        {
            var save = new SaveData();
            Assert.IsFalse(ShoppingBagContents.TryPut(save, Bread, Capacity), "not bought");
            SupplyLocker.Add(save, Bread, 1);
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            Assert.IsFalse(ShoppingBagContents.TryPut(save, Bread, Capacity), "the one loaf is already in the bag");
            Assert.IsFalse(ShoppingBagContents.TryPut(null, Bread, Capacity));
            Assert.IsFalse(ShoppingBagContents.TryPut(save, "", Capacity));
        }

        [Test]
        public void ADifferentGameIgnoresTheBag_SoAReloadFindsTheGoodsPutAway()
        {
            var before = new SaveData();
            SupplyLocker.Add(before, Bread, 1);
            Assert.IsTrue(ShoppingBagContents.TryPut(before, Bread, Capacity));

            var reloaded = new SaveData();
            SupplyLocker.Add(reloaded, Bread, 1);
            Assert.IsFalse(ShoppingBagContents.HoldsAny(reloaded), "the bag belonged to the game that was left");
            Assert.That(SupplyLocker.Count(reloaded, Bread), Is.EqualTo(1), "found in his storage");
            Assert.That(ShoppingBagContents.Release(reloaded), Is.EqualTo(0));

            Assert.IsTrue(ShoppingBagContents.TryPut(reloaded, Bread, Capacity), "a new bag for the new game");
            Assert.IsFalse(ShoppingBagContents.HoldsAny(before), "and the old one is gone");
        }

        [Test]
        public void TakingOneOutGivesItBack_AndAnEmptyBagHoldsNothing()
        {
            var save = new SaveData();
            SupplyLocker.Add(save, Bread, 1);
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            Assert.IsTrue(ShoppingBagContents.TryTakeOut(save, Bread));
            Assert.IsFalse(ShoppingBagContents.TryTakeOut(save, Bread));
            Assert.IsFalse(ShoppingBagContents.HoldsAny(save));
            Assert.That(SupplyLocker.Count(save, Bread), Is.EqualTo(1));
        }

        [Test]
        public void ResettingTheServicesEmptiesTheBag()
        {
            var save = new SaveData();
            SupplyLocker.Add(save, Bread, 1);
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            GameServices.Reset();
            Assert.IsFalse(ShoppingBagContents.HoldsAny(save));
        }

        // ---- separate counts on one good ---------------------------------------------------------------------

        private IslandGoodDef Good(string id, float units)
        {
            var g = ScriptableObject.CreateInstance<IslandGoodDef>();
            g.Id = id;
            g.Source = GoodSource.MainlandOverBar;
            g.DailyUnits = units;
            g.DailyMassKg = units;
            g.DailyVolumeLitres = units;
            g.PlayerAllowanceFraction = 0f;
            g.WinterMultiplier = 1f;
            _spawned.Add(g);
            return g;
        }

        private IslandStoreShelves Groceries(IslandGoodDef good)
        {
            var ledger = new IslandStoreLedger(new[] { good }, Plenty, Cadence, BufferDays, Lookback, _ => true);
            var lines = new List<IslandShelfLine>
            {
                new IslandShelfLine { Id = "line.test_bread", Good = good, ItemName = "loaf", UnitsPerItem = 1f,
                                      MaxOnShelf = 6, ShareOfGood = 0.5f, SoldAtShelf = true },
                new IslandShelfLine { Id = "line.test_beans", Good = good, ItemName = "tin", UnitsPerItem = 1f,
                                      MaxOnShelf = 6, ShareOfGood = 0.5f, SoldAtShelf = true },
            };
            return new IslandStoreShelves(Shop, ledger, lines, SecondsPerDay, Opens, Closes);
        }

        private static double At(int day, double hour) => (day + hour / 24.0) * SecondsPerDay;

        [Test]
        public void LinesSharingAGoodCountSeparately_AndOneSellsOutWhileTheOtherStands()
        {
            IslandStoreShelves shelves = Groceries(Good("islandgood.test_groceries", 20f));
            double now = At(4, 8.0);
            int bread = shelves.CountAt("line.test_bread", now);
            int beans = shelves.CountAt("line.test_beans", now);
            Assert.That(bread, Is.GreaterThan(0));
            Assert.That(beans, Is.GreaterThan(0));

            for (int i = 0; i < bread; i++) Assert.IsTrue(shelves.TryTakeOne("line.test_bread", now));
            Assert.That(shelves.CountAt("line.test_bread", now), Is.EqualTo(0), "bread sold out");
            Assert.IsFalse(shelves.TryTakeOne("line.test_bread", now));
            Assert.That(shelves.CountAt("line.test_beans", now), Is.EqualTo(beans), "the beans are all still there");
        }

        [Test]
        public void SeparateCountsAreAPureFunctionOfTheMomentAndThePurchases()
        {
            IslandGoodDef good = Good("islandgood.test_groceries", 20f);
            IslandStoreShelves a = Groceries(good), b = Groceries(good);
            double t = At(4, 9.0);
            a.TryTakeOne("line.test_bread", t);
            a.TryTakeOne("line.test_bread", t);
            b.TryTakeOne("line.test_bread", t);
            b.TryTakeOne("line.test_bread", t);
            foreach (double later in new[] { At(4, 15.0), At(5, 8.0), At(7, 12.0) })
            {
                Assert.That(a.CountAt("line.test_bread", later), Is.EqualTo(b.CountAt("line.test_bread", later)));
                Assert.That(a.CountAt("line.test_beans", later), Is.EqualTo(b.CountAt("line.test_beans", later)));
            }
        }

        [Test]
        public void LinesShowingMoreThanAllOfAGoodAreRefused()
        {
            IslandGoodDef good = Good("islandgood.test_groceries", 20f);
            var ledger = new IslandStoreLedger(new[] { good }, Plenty, Cadence, BufferDays, Lookback, _ => true);
            var lines = new List<IslandShelfLine>
            {
                new IslandShelfLine { Id = "line.a", Good = good, ItemName = "a", UnitsPerItem = 1f, MaxOnShelf = 6, ShareOfGood = 0.7f },
                new IslandShelfLine { Id = "line.b", Good = good, ItemName = "b", UnitsPerItem = 1f, MaxOnShelf = 6, ShareOfGood = 0.6f },
            };
            Assert.Throws<ArgumentException>(() => new IslandStoreShelves(Shop, ledger, lines, SecondsPerDay, Opens, Closes));

            var whole = new List<IslandShelfLine>
            {
                new IslandShelfLine { Id = "line.a", Good = good, ItemName = "a", UnitsPerItem = 1f, MaxOnShelf = 6 },
                new IslandShelfLine { Id = "line.b", Good = good, ItemName = "b", UnitsPerItem = 1f, MaxOnShelf = 6 },
            };
            Assert.Throws<ArgumentException>(() => new IslandStoreShelves(Shop, ledger, whole, SecondsPerDay, Opens, Closes),
                                             "two lines each showing the whole good");
        }
    }
}
