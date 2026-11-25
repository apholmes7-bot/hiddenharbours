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
    /// or cooked until it is put away, the bag holds no more than its capacity, a new game does not inherit
    /// the bag, and putting away releases everything at once. The bag can be set down with its goods inside
    /// (owner ruling 2026-11-23): they stay set aside, and the bag says which region and where it stands. The
    /// bag lives in the save's own row (save v26), so each game has its own. Shelf lines sharing one good count
    /// and sell out separately and deterministically.
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
        public void ANewGameDoesNotInheritTheBagOfTheGameThatWasLeft()
        {
            var before = new SaveData();
            SupplyLocker.Add(before, Bread, 1);
            Assert.IsTrue(ShoppingBagContents.TryPut(before, Bread, Capacity));
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(before, "region.test", new Vector2(3f, 4f), false));

            var other = new SaveData();
            SupplyLocker.Add(other, Bread, 1);
            Assert.IsFalse(ShoppingBagContents.HoldsAny(other), "the bag belonged to the game that was left");
            Assert.IsFalse(ShoppingBagContents.IsSetDown(other), "and so did the place it stood");
            Assert.IsFalse(ShoppingBagContents.LiesIn(other, "region.test"));
            Assert.That(SupplyLocker.Count(other, Bread), Is.EqualTo(1), "this game's own loaf is all his");

            Assert.IsTrue(ShoppingBagContents.TryPut(other, Bread, Capacity), "a new bag for the new game");
            Assert.IsFalse(ShoppingBagContents.IsSetDown(other), "the new bag is in his hand, not where the old one stood");
            Assert.IsTrue(ShoppingBagContents.IsSetDown(before), "and the game that was left still has its own, where it stood");
            Assert.That(ShoppingBagContents.SetDownRegionId, Is.EqualTo("region.test"));
        }

        [Test]
        public void TheSameGameKeepsItsBag_HoweverLongItStands()
        {
            // Leaving for the title and coming back keeps the same game in memory: the bag is still his.
            var save = new SaveData();
            SupplyLocker.Add(save, Bread, 2);
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, "region.test", new Vector2(3f, 4f), true));

            Assert.That(ShoppingBagContents.Total(save), Is.EqualTo(2));
            Assert.That(SupplyLocker.Owned(save, Bread), Is.EqualTo(2), "bought, so never lost");
            Assert.That(SupplyLocker.Count(save, Bread), Is.EqualTo(0), "and not put away behind his back");
        }

        // ---- the bag set down ----------------------------------------------------------------------------------

        [Test]
        public void ASetDownBagKeepsItsGoodsSetAside_AndSaysWhereItStands()
        {
            var save = new SaveData();
            SupplyLocker.Add(save, Bread, 2);
            SupplyLocker.Add(save, Beans, 1);
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Beans, Capacity));
            Assert.IsFalse(ShoppingBagContents.IsSetDown(save), "a bag just filled is in his hand");

            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, "region.test", new Vector2(3f, 4f), true));
            Assert.IsTrue(ShoppingBagContents.IsSetDown(save));
            Assert.That(ShoppingBagContents.SetDownRegionId, Is.EqualTo("region.test"));
            Assert.That(ShoppingBagContents.SetDownPosition, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.IsTrue(ShoppingBagContents.SetDownByTheBed);

            Assert.That(ShoppingBagContents.InBag(save, Bread), Is.EqualTo(1), "the goods stay in it");
            Assert.That(ShoppingBagContents.InBag(save, Beans), Is.EqualTo(1));
            Assert.That(SupplyLocker.Count(save, Bread), Is.EqualTo(1), "and cannot be used from the floor");
            Assert.IsFalse(SupplyLocker.Has(save, Beans));

            ShoppingBagContents.MarkHeld();
            Assert.IsFalse(ShoppingBagContents.IsSetDown(save), "picked up again");
            Assert.That(ShoppingBagContents.SetDownRegionId, Is.EqualTo(""));
            Assert.IsFalse(ShoppingBagContents.SetDownByTheBed);
            Assert.That(ShoppingBagContents.Total(save), Is.EqualTo(2), "with everything still in it");
        }

        [Test]
        public void ASetDownBagLiesInTheRegionItWasLeftIn_AndNoOther()
        {
            var save = new SaveData();
            SupplyLocker.Add(save, Bread, 1);
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            Assert.IsFalse(ShoppingBagContents.LiesIn(save, "region.test"), "in his hand it lies nowhere");

            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, "region.test", Vector2.zero, false));
            Assert.IsTrue(ShoppingBagContents.LiesIn(save, "region.test"));
            Assert.IsFalse(ShoppingBagContents.LiesIn(save, "region.other"));
            Assert.IsFalse(ShoppingBagContents.LiesIn(save, null));

            // Set down where no region is known (a test scene): it lies wherever he is.
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, null, Vector2.zero, false));
            Assert.IsTrue(ShoppingBagContents.LiesIn(save, "region.other"));
            Assert.IsTrue(ShoppingBagContents.LiesIn(save, null));
        }

        [Test]
        public void AnEmptyBagCannotBeSetDown_AndABagEmptiedOrPutAwayStandsNowhere()
        {
            var save = new SaveData();
            Assert.IsFalse(ShoppingBagContents.MarkSetDown(save, "region.test", Vector2.zero, false), "nothing in it");
            Assert.IsFalse(ShoppingBagContents.MarkSetDown(null, "region.test", Vector2.zero, false));

            SupplyLocker.Add(save, Bread, 1);
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, "region.test", Vector2.zero, false));
            Assert.IsTrue(ShoppingBagContents.TryTakeOut(save, Bread));
            Assert.IsFalse(ShoppingBagContents.IsSetDown(save), "the last good left it: no bag stands there");
            Assert.IsFalse(ShoppingBagContents.Any);

            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            Assert.IsFalse(ShoppingBagContents.IsSetDown(save), "a bag started afresh is in his hand");
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, "region.test", Vector2.zero, false));
            Assert.That(ShoppingBagContents.Release(save), Is.EqualTo(1));
            Assert.IsFalse(ShoppingBagContents.IsSetDown(save));
            Assert.That(ShoppingBagContents.SetDownRegionId, Is.EqualTo(""));
        }

        [Test]
        public void TheBagRemembersItsKind_ForTheGameItBelongsTo()
        {
            var def = ScriptableObject.CreateInstance<ShoppingBagDef>();
            _spawned.Add(def);
            var save = new SaveData();
            SupplyLocker.Add(save, Bread, 1);

            ShoppingBagContents.NoteBag(save, def);
            Assert.IsNull(ShoppingBagContents.Bag, "no goods of this game are in a bag yet");

            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            ShoppingBagContents.NoteBag(new SaveData(), def);
            Assert.IsNull(ShoppingBagContents.Bag, "another game does not name this bag");
            ShoppingBagContents.NoteBag(save, def);
            Assert.AreSame(def, ShoppingBagContents.Bag);

            ShoppingBagContents.Release(save);
            Assert.IsNull(ShoppingBagContents.Bag, "put away: no bag");
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
        public void ResettingTheServicesForgetsTheGame_ButTheBagStaysWithItsSave()
        {
            var save = new SaveData();
            SupplyLocker.Add(save, Bread, 1);
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            Assert.IsTrue(ShoppingBagContents.Any);
            GameServices.Reset();
            Assert.IsFalse(ShoppingBagContents.Any, "no game is being asked about");
            Assert.IsTrue(ShoppingBagContents.HoldsAny(save), "the bag is part of the save, not of the session");
            Assert.That(SupplyLocker.Count(save, Bread), Is.EqualTo(0), "so its goods are still set aside");
        }

        [Test]
        public void TheBagIsTheSavesOwnRow_AndAnEmptyBagLeavesNoRow()
        {
            var save = new SaveData();
            SupplyLocker.Add(save, Bread, 2);
            Assert.That(save.ShoppingBags.Count, Is.EqualTo(0));
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            Assert.That(save.ShoppingBags.Count, Is.EqualTo(1), "one bag");
            Assert.IsTrue(save.ShoppingBags[0].Held);
            Assert.That(save.ShoppingBags[0].Items.Count, Is.EqualTo(1));
            Assert.That(save.ShoppingBags[0].Items[0].SupplyId, Is.EqualTo(Bread));
            Assert.That(save.ShoppingBags[0].Items[0].Count, Is.EqualTo(2));

            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, "region.test", new Vector2(3f, 4f), 1, true));
            ShoppingBagDto row = save.ShoppingBags[0];
            Assert.IsFalse(row.Held);
            Assert.That(row.Region, Is.EqualTo("region.test"));
            Assert.That(row.PosX, Is.EqualTo(3f));
            Assert.That(row.PosY, Is.EqualTo(4f));
            Assert.That(row.Level, Is.EqualTo(1));
            Assert.That(row.HullId, Is.EqualTo(""), "reserved: nothing is set down aboard");
            Assert.IsTrue(row.ByTheBed);
            Assert.That(ShoppingBagContents.SetDownLevel, Is.EqualTo(1));

            Assert.That(ShoppingBagContents.Release(save), Is.EqualTo(2));
            Assert.That(save.ShoppingBags.Count, Is.EqualTo(0), "put away: no row");
        }

        [Test]
        public void ABagSetDownWhereNoRegionIsKnownStillCarriesARegion()
        {
            var save = new SaveData();
            SupplyLocker.Add(save, Bread, 1);
            Assert.IsTrue(ShoppingBagContents.TryPut(save, Bread, Capacity));
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, "", Vector2.zero, false));
            Assert.That(save.ShoppingBags[0].Region, Is.EqualTo(ShoppingBagContents.UnknownRegionId),
                        "a set-down row with no region would be dropped by the load repair");
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

        // Room on the shelf for the whole share, so the count is the stock itself (no back room out of sight).
        private IslandStoreShelves Groceries(IslandGoodDef good)
        {
            var ledger = new IslandStoreLedger(new[] { good }, Plenty, Cadence, BufferDays, Lookback, _ => true);
            var lines = new List<IslandShelfLine>
            {
                new IslandShelfLine { Id = "line.test_bread", Good = good, ItemName = "loaf", UnitsPerItem = 1f,
                                      MaxOnShelf = 500, ShareOfGood = 0.5f, SoldAtShelf = true },
                new IslandShelfLine { Id = "line.test_beans", Good = good, ItemName = "tin", UnitsPerItem = 1f,
                                      MaxOnShelf = 500, ShareOfGood = 0.5f, SoldAtShelf = true },
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
