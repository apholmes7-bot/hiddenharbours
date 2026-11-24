using System;
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The shopping bag and the store purchases survive a restart</b> (the wiring over save v26's rows; no
    /// new saved field). Every case goes through the real JSON the save file holds. The bag comes back in
    /// his hand or where it was set down (region, place, storey, by the bed), an emptied bag leaves no row, an
    /// older save gains a bag the first time one is filled, and a damaged bag row never loses goods. A shelf
    /// bought empty is still empty after a load and fills again when the uninterrupted game's shelf does; the
    /// writer keeps the repair's order, drops rows past the shelf's lookback and leaves other shops' rows
    /// alone. Special orders are not touched.
    /// </summary>
    public class BagAndPurchasesSavedTests
    {
        private const string Bread = "supply.test_bread";
        private const string Beans = "supply.test_beans";
        private const string Paper = "shoppingbag.test_paper";
        private const string Home = "region.test_home";
        private const string Away = "region.test_away";
        private const int Capacity = 3;

        private const double SecondsPerDay = 1800.0;
        private const double Opens = 7.0, Closes = 19.0;
        private const int Cadence = 2, Lookback = 28;
        private const double BufferDays = 4.0;
        private static readonly MassVolume Plenty = new MassVolume(1_000_000.0, 1_000_000.0);
        private const string ShopA = "shop.test_store";
        private const string ShopB = "shop.test_other_store";
        private const string BreadLine = "line.test_bread";
        private const string BeansLine = "line.test_beans";

        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private ShoppingBagDef _paper;

        [SetUp]
        public void SetUp()
        {
            ShoppingBagContents.Clear();
            _paper = ScriptableObject.CreateInstance<ShoppingBagDef>();
            _paper.Id = Paper;
            _paper.Capacity = Capacity;
            _spawned.Add(_paper);
            ShoppingBagContents.BagSource = id => id == Paper ? _paper : null;
        }

        [TearDown]
        public void TearDown()
        {
            ShoppingBagContents.BagSource = null;
            ShoppingBagContents.Clear();
            foreach (UnityEngine.Object o in _spawned)
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        /// <summary>The save as the file holds it, read back as a fresh session would read it.</summary>
        private static SaveData Reloaded(SaveData save)
        {
            string json = SaveSerialization.ToJson(save);
            ShoppingBagContents.Clear();   // nothing in memory survives a restart
            return SaveSerialization.FromJson(json);
        }

        private SaveData GameWithBag(int bread, int beans = 0)
        {
            SaveData save = SaveMigration.NewGame();
            for (int i = 0; i < bread; i++) Buy(save, Bread);
            for (int i = 0; i < beans; i++) Buy(save, Beans);
            return save;
        }

        // A bagged purchase: bought into his supplies, then set aside in the bag, which is named.
        private void Buy(SaveData save, string supplyId)
        {
            SupplyLocker.Add(save, supplyId, 1);
            Assert.IsTrue(ShoppingBagContents.TryPut(save, supplyId, Capacity));
            ShoppingBagContents.NoteBag(save, _paper);
        }

        // ---- the bag ------------------------------------------------------------------------------------

        [Test]
        public void ABagInHisHand_IsInHisHandAfterALoad_WithItsGoodsStillSetAside()
        {
            SaveData loaded = Reloaded(GameWithBag(bread: 2, beans: 1));

            Assert.IsTrue(ShoppingBagContents.HoldsAny(loaded));
            Assert.IsFalse(ShoppingBagContents.IsSetDown(loaded), "it was in his hand");
            Assert.IsTrue(loaded.ShoppingBags[0].Held);
            Assert.That(ShoppingBagContents.InBag(loaded, Bread), Is.EqualTo(2));
            Assert.That(ShoppingBagContents.InBag(loaded, Beans), Is.EqualTo(1));
            Assert.That(ShoppingBagContents.Total(loaded), Is.EqualTo(3));
            Assert.That(SupplyLocker.Owned(loaded, Bread), Is.EqualTo(2), "bought, so owned");
            Assert.That(SupplyLocker.Count(loaded, Bread), Is.EqualTo(0), "but not put away behind his back");
            Assert.IsFalse(SupplyLocker.Has(loaded, Beans));
            Assert.That(ShoppingBagContents.Bag, Is.SameAs(_paper), "its kind is found from the saved id");
        }

        [Test]
        public void ABagSetDownAshore_StandsWhereItWasLeftAfterALoad()
        {
            SaveData save = GameWithBag(bread: 1);
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, Home, new Vector2(12.5f, -3.25f), 0, false));
            SaveData loaded = Reloaded(save);

            Assert.IsTrue(ShoppingBagContents.IsSetDown(loaded));
            Assert.That(ShoppingBagContents.SetDownRegionId, Is.EqualTo(Home));
            Assert.That(ShoppingBagContents.SetDownPosition, Is.EqualTo(new Vector2(12.5f, -3.25f)));
            Assert.That(ShoppingBagContents.SetDownLevel, Is.EqualTo(0));
            Assert.IsFalse(ShoppingBagContents.SetDownByTheBed);
            Assert.IsTrue(ShoppingBagContents.LiesIn(loaded, Home));
            Assert.That(ShoppingBagContents.InBag(loaded, Bread), Is.EqualTo(1));
            Assert.That(SupplyLocker.Count(loaded, Bread), Is.EqualTo(0), "the loaf cannot be used from the floor");
        }

        [Test]
        public void ABagLeftByTheBed_IsByTheBedAfterALoad()
        {
            SaveData save = GameWithBag(bread: 2);
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, Home, new Vector2(-9f, 3f), 0, true));
            SaveData loaded = Reloaded(save);

            Assert.IsTrue(ShoppingBagContents.IsSetDown(loaded));
            Assert.IsTrue(ShoppingBagContents.SetDownByTheBed);
            Assert.That(ShoppingBagContents.SetDownPosition, Is.EqualTo(new Vector2(-9f, 3f)));
            Assert.That(ShoppingBagContents.Total(loaded), Is.EqualTo(2));
        }

        [Test]
        public void ABagLeftOnTheUpperStorey_IsOnTheUpperStoreyAfterALoad()
        {
            SaveData save = GameWithBag(bread: 1);
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, Home, new Vector2(4f, 30f), 1, true));
            SaveData loaded = Reloaded(save);

            Assert.IsTrue(ShoppingBagContents.IsSetDown(loaded));
            Assert.That(ShoppingBagContents.SetDownLevel, Is.EqualTo(1));
            Assert.That(loaded.ShoppingBags[0].Level, Is.EqualTo(1));
            Assert.That(ShoppingBagContents.SetDownPosition, Is.EqualTo(new Vector2(4f, 30f)));
        }

        [Test]
        public void ABagLeftInAnotherRegion_StaysThereAfterALoad()
        {
            SaveData save = GameWithBag(bread: 1);
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, Away, new Vector2(100f, 200f), 0, false));
            SaveData loaded = Reloaded(save);

            Assert.IsTrue(ShoppingBagContents.IsSetDown(loaded));
            Assert.IsFalse(ShoppingBagContents.LiesIn(loaded, Home), "not where he is");
            Assert.IsTrue(ShoppingBagContents.LiesIn(loaded, Away), "where he left it");
            Assert.That(ShoppingBagContents.InBag(loaded, Bread), Is.EqualTo(1), "and the loaf is still in it");
            Assert.That(SupplyLocker.Count(loaded, Bread), Is.EqualTo(0));
        }

        [Test]
        public void ABagSetDownWhereNoRegionIsKnown_SurvivesTheLoadRepair()
        {
            SaveData save = GameWithBag(bread: 1);
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, null, new Vector2(1f, 2f), 0, false));
            SaveData loaded = Reloaded(save);

            Assert.IsTrue(ShoppingBagContents.IsSetDown(loaded), "the row carries a region, so it is kept");
            Assert.That(loaded.ShoppingBags[0].Region, Is.EqualTo(ShoppingBagContents.UnknownRegionId));
            Assert.IsTrue(ShoppingBagContents.LiesIn(loaded, Home), "it lies wherever he is");
        }

        [Test]
        public void AnEmptyBag_LeavesNothingInTheSave()
        {
            SaveData save = GameWithBag(bread: 1);
            Assert.IsTrue(ShoppingBagContents.TryTakeOut(save, Bread));
            SaveData loaded = Reloaded(save);
            Assert.That(loaded.ShoppingBags.Count, Is.EqualTo(0));
            Assert.IsFalse(ShoppingBagContents.HoldsAny(loaded));
            Assert.That(SupplyLocker.Count(loaded, Bread), Is.EqualTo(1), "the loaf is simply his");

            SaveData putAway = GameWithBag(bread: 2);
            Assert.That(ShoppingBagContents.Release(putAway), Is.EqualTo(2));
            loaded = Reloaded(putAway);
            Assert.That(loaded.ShoppingBags.Count, Is.EqualTo(0));
            Assert.That(SupplyLocker.Count(loaded, Bread), Is.EqualTo(2));
        }

        [Test]
        public void AFullBag_IsStillFullAfterALoad()
        {
            SaveData save = GameWithBag(bread: 2, beans: 1);
            Assert.That(ShoppingBagContents.Total(save), Is.EqualTo(Capacity));
            SaveData loaded = Reloaded(save);

            Assert.That(ShoppingBagContents.Total(loaded), Is.EqualTo(Capacity));
            Assert.That(ShoppingBagContents.LineCount(loaded), Is.EqualTo(2));
            SupplyLocker.Add(loaded, Beans, 1);
            Assert.IsFalse(ShoppingBagContents.TryPut(loaded, Beans, Capacity), "no room for another");
            Assert.That(SupplyLocker.Count(loaded, Beans), Is.EqualTo(1), "the extra tin is in his supplies");
        }

        [Test]
        public void TwoSavesInARow_WriteTheSameFile_AndTheLaterStateWins()
        {
            SaveData save = GameWithBag(bread: 2);
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, Home, new Vector2(3f, 4f), 1, true));
            string first = SaveSerialization.ToJson(save);
            string second = SaveSerialization.ToJson(save);
            Assert.That(second, Is.EqualTo(first), "saving twice changes nothing");

            SaveData loaded = SaveSerialization.FromJson(first);
            Assert.That(SaveSerialization.ToJson(loaded), Is.EqualTo(first), "a load and a save give the file back");
            Assert.That(SaveSerialization.ToJson(SaveSerialization.FromJson(SaveSerialization.ToJson(loaded))),
                        Is.EqualTo(first), "and again");

            // He picks it up and the game is saved again: the second file says so.
            Assert.IsTrue(ShoppingBagContents.IsSetDown(loaded));
            ShoppingBagContents.MarkHeld();
            SaveData again = Reloaded(loaded);
            Assert.IsFalse(ShoppingBagContents.IsSetDown(again));
            Assert.IsTrue(again.ShoppingBags[0].Held);
            Assert.That(ShoppingBagContents.InBag(again, Bread), Is.EqualTo(2));
        }

        [Test]
        public void AnOlderSave_LoadsWithNoBag_AndKeepsTheBagItThenFills()
        {
            SaveData save = SaveSerialization.FromJson(
                "{\"SchemaVersion\":25,\"WorldSeed\":77,\"GameTimeSeconds\":4321.5,\"Money\":250,\"DayIndex\":3," +
                "\"SupplyStock\":[{\"SupplyId\":\"" + Bread + "\",\"Count\":2}]}");
            Assert.That(save.SchemaVersion, Is.EqualTo(SaveMigration.CurrentVersion));
            Assert.IsFalse(ShoppingBagContents.HoldsAny(save), "no bag is invented");
            Assert.That(SupplyLocker.Count(save, Bread), Is.EqualTo(2), "what he bought before is in his supplies");

            Buy(save, Beans);
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, Home, new Vector2(1f, 1f), 0, false));
            SaveData loaded = Reloaded(save);

            Assert.That(loaded.WorldSeed, Is.EqualTo(77));
            Assert.That(loaded.Money, Is.EqualTo(250));
            Assert.IsTrue(ShoppingBagContents.IsSetDown(loaded));
            Assert.That(ShoppingBagContents.InBag(loaded, Beans), Is.EqualTo(1));
            Assert.That(ShoppingBagContents.InBag(loaded, Bread), Is.EqualTo(0));
            Assert.That(SupplyLocker.Count(loaded, Bread), Is.EqualTo(2), "the older goods stay put away");
            Assert.That(loaded.SpecialOrders.Count, Is.EqualTo(0), "special orders are not touched");
            Assert.That(loaded.NextSpecialOrderNumber, Is.EqualTo(1));
        }

        [Test]
        public void ADamagedBagRow_LosesNoGoods()
        {
            // Set down with no region: the repair drops the row, and the goods are found put away.
            SaveData save = GameWithBag(bread: 2, beans: 1);
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, Home, new Vector2(3f, 4f), 0, false));
            save.ShoppingBags[0].Region = "";
            SaveData loaded = Reloaded(save);
            Assert.IsFalse(ShoppingBagContents.HoldsAny(loaded), "the damaged bag is gone");
            Assert.That(SupplyLocker.Owned(loaded, Bread), Is.EqualTo(2), "but nothing he bought is");
            Assert.That(SupplyLocker.Count(loaded, Bread), Is.EqualTo(2));
            Assert.That(SupplyLocker.Count(loaded, Beans), Is.EqualTo(1));

            // A storey that cannot be.
            save = GameWithBag(bread: 1);
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, Home, Vector2.zero, 0, false));
            save.ShoppingBags[0].Level = -3;
            loaded = Reloaded(save);
            Assert.IsFalse(ShoppingBagContents.HoldsAny(loaded));
            Assert.That(SupplyLocker.Count(loaded, Bread), Is.EqualTo(1));

            // No kind.
            save = GameWithBag(bread: 1);
            save.ShoppingBags[0].BagDefId = "";
            loaded = Reloaded(save);
            Assert.IsFalse(ShoppingBagContents.HoldsAny(loaded));
            Assert.That(SupplyLocker.Count(loaded, Bread), Is.EqualTo(1));

            // A bag that claims more than he owns holds what he owns, and no more.
            save = GameWithBag(bread: 2);
            save.ShoppingBags[0].Items[0].Count = 9;
            loaded = Reloaded(save);
            Assert.That(SupplyLocker.Owned(loaded, Bread), Is.EqualTo(2), "nothing is invented");
            Assert.That(ShoppingBagContents.InBag(loaded, Bread), Is.EqualTo(2));
            Assert.That(SupplyLocker.Count(loaded, Bread), Is.EqualTo(0));

            // A bag of goods he does not own at all is no bag.
            save = GameWithBag(bread: 1);
            save.ShoppingBags[0].Items[0].SupplyId = "supply.test_never_bought";
            loaded = Reloaded(save);
            Assert.IsFalse(ShoppingBagContents.HoldsAny(loaded));
            Assert.That(SupplyLocker.Count(loaded, Bread), Is.EqualTo(1), "his loaf is in his supplies");
        }

        [Test]
        public void AKindOfBagThatNoLongerShips_IsNotFound_AndTheGoodsAreStillOwned()
        {
            SaveData save = GameWithBag(bread: 1);
            save.ShoppingBags[0].BagDefId = "shoppingbag.test_retired";
            SaveData loaded = Reloaded(save);
            Assert.IsTrue(ShoppingBagContents.HoldsAny(loaded), "the row is whole, so the repair keeps it");
            Assert.IsNull(ShoppingBagContents.Bag, "but no such bag ships");
            // The hands put the goods away when they cannot make the bag: that is this release.
            Assert.That(ShoppingBagContents.Release(loaded), Is.EqualTo(1));
            Assert.That(SupplyLocker.Count(loaded, Bread), Is.EqualTo(1));
        }

        [Test]
        public void TheSameShopping_WritesTheSameFile()
        {
            SaveData a = GameWithBag(bread: 2, beans: 1);
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(a, Home, new Vector2(3f, 4f), 1, true));
            SaveData b = GameWithBag(bread: 2, beans: 1);
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(b, Home, new Vector2(3f, 4f), 1, true));
            Assert.That(SaveSerialization.ToJson(b), Is.EqualTo(SaveSerialization.ToJson(a)));
        }

        [Test]
        public void TheBagAndThePurchases_ComeBackFromARealFileOnDisk()
        {
            IslandGoodDef good = Good("islandgood.test_groceries", 20f);
            IslandStoreShelves shelves = Groceries(ShopA, good);
            double now = At(4, 8.0);
            SaveData save = GameWithBag(bread: 2);
            Assert.IsTrue(ShoppingBagContents.MarkSetDown(save, Home, new Vector2(-9f, 3f), 0, true));
            Assert.IsTrue(shelves.TryTakeOne(BreadLine, now));
            Assert.IsTrue(shelves.TryTakeOne(BreadLine, now));
            IslandShelvesHost.WritePurchases(save, shelves);

            string dir = Path.Combine(Path.GetTempPath(), "bag-and-purchases-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "savegame.json");
            try
            {
                SaveStore.Write(save, path);
                SaveStore.Write(save, path);   // the second save replaces the first
                ShoppingBagContents.Clear();
                SaveData loaded = SaveStore.Read(path);
                Assert.IsNotNull(loaded);

                Assert.IsTrue(ShoppingBagContents.IsSetDown(loaded));
                Assert.IsTrue(ShoppingBagContents.SetDownByTheBed);
                Assert.That(ShoppingBagContents.InBag(loaded, Bread), Is.EqualTo(2));

                IslandStoreShelves rebuilt = Groceries(ShopA, good);
                Assert.That(IslandShelvesHost.ReadPurchases(loaded, rebuilt), Is.EqualTo(1), "two loaves at one moment are one row");
                Assert.That(rebuilt.CountAt(BreadLine, now), Is.EqualTo(shelves.CountAt(BreadLine, now)));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void TheShippedBagIsFoundByItsSavedId()
        {
            ShoppingBagBook book = ShoppingBagBook.FromResources();
            int shipped = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ShoppingBagDef", new[] { "Assets/_Project/Data" }))
            {
                var def = AssetDatabase.LoadAssetAtPath<ShoppingBagDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null) continue;
                shipped++;
                Assert.That(def.Id, Does.Match("^shoppingbag\\.[a-z0-9_]+$"));
                Assert.That(book.Find(def.Id), Is.SameAs(def),
                            def.Id + " must be under Resources/" + ShoppingBagBook.ResourcesFolder + " or a saved bag of it cannot be made again");
            }
            Assert.That(shipped, Is.GreaterThan(0), "a bag ships");
            Assert.That(book.Count, Is.EqualTo(shipped));
            Assert.IsNull(book.Find("shoppingbag.no_such_bag"));
            Assert.IsNull(book.Find(""));
        }

        // ---- the store purchases ------------------------------------------------------------------------

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

        // Room on the shelf for the whole share, so the count is the stock itself.
        private static IslandStoreShelves Groceries(string shopId, IslandGoodDef good)
        {
            var ledger = new IslandStoreLedger(new[] { good }, Plenty, Cadence, BufferDays, Lookback, _ => true);
            var lines = new List<IslandShelfLine>
            {
                new IslandShelfLine { Id = BreadLine, Good = good, ItemName = "loaf", UnitsPerItem = 1f,
                                      MaxOnShelf = 500, ShareOfGood = 0.5f, SoldAtShelf = true },
                new IslandShelfLine { Id = BeansLine, Good = good, ItemName = "tin", UnitsPerItem = 1f,
                                      MaxOnShelf = 500, ShareOfGood = 0.5f, SoldAtShelf = true },
            };
            return new IslandStoreShelves(shopId, ledger, lines, SecondsPerDay, Opens, Closes);
        }

        private static double At(int day, double hour) => (day + hour / 24.0) * SecondsPerDay;

        private static int BuyOut(IslandStoreShelves shelves, string line, double now)
        {
            int n = shelves.CountAt(line, now);
            Assert.That(n, Is.GreaterThan(0), "the shelf has something to buy");
            for (int i = 0; i < n; i++) Assert.IsTrue(shelves.TryTakeOne(line, now));
            Assert.That(shelves.CountAt(line, now), Is.EqualTo(0));
            return n;
        }

        [Test]
        public void AShelfBoughtEmpty_IsStillSoldOutAfterALoad_AndRestocksWhenTheUninterruptedShelfDoes()
        {
            IslandGoodDef good = Good("islandgood.test_groceries", 20f);
            IslandStoreShelves played = Groceries(ShopA, good);     // the game that was never closed
            IslandStoreShelves untouched = Groceries(ShopA, good);  // the same store with no purchase at all
            double now = At(4, 8.0);
            int beans = played.CountAt(BeansLine, now);
            int bought = BuyOut(played, BreadLine, now);

            SaveData save = SaveMigration.NewGame();
            IslandShelvesHost.WritePurchases(save, played);
            SaveData loaded = Reloaded(save);

            // Without the rows a load would put every loaf back.
            Assert.That(untouched.CountAt(BreadLine, now), Is.EqualTo(bought));

            IslandStoreShelves rebuilt = Groceries(ShopA, good);
            Assert.That(IslandShelvesHost.ReadPurchases(loaded, rebuilt), Is.GreaterThan(0));
            Assert.That(rebuilt.CountAt(BreadLine, now), Is.EqualTo(0), "still sold out");
            Assert.IsFalse(rebuilt.TryTakeOne(BreadLine, now), "and it sells nothing");
            Assert.That(rebuilt.CountAt(BeansLine, now), Is.EqualTo(beans), "the other shelf is as it was");

            // From here on the loaded game's shelves are the uninterrupted game's shelves, hour for hour.
            int restockDay = -1;
            for (int day = 4; day <= 4 + Lookback + 2; day++)
                foreach (double hour in new[] { 6.0, 8.0, 12.0, 18.0, 22.0 })
                {
                    double t = At(day, hour);
                    if (t < now) continue;
                    Assert.That(rebuilt.CountAt(BreadLine, t), Is.EqualTo(played.CountAt(BreadLine, t)), "bread, day " + day + " " + hour + "h");
                    Assert.That(rebuilt.CountAt(BeansLine, t), Is.EqualTo(played.CountAt(BeansLine, t)), "beans, day " + day + " " + hour + "h");
                    if (restockDay < 0 && played.CountAt(BreadLine, t) > 0) restockDay = day;
                }
            Assert.That(restockDay, Is.GreaterThan(4), "the shelf fills again on a later day, by the run rule");
            Assert.That(restockDay, Is.LessThanOrEqualTo(4 + Cadence + 1), "and not later than the next run");
            Assert.That(rebuilt.CountAt(BreadLine, At(restockDay, 8.0)), Is.GreaterThan(0));
        }

        [Test]
        public void PurchasesAreOneRowPerMoment_InTheRepairsOrder_AndTheRepairChangesNothing()
        {
            IslandGoodDef good = Good("islandgood.test_groceries", 20f);
            IslandStoreShelves shelves = Groceries(ShopA, good);
            // Bought out of order on purpose.
            Assert.IsTrue(shelves.TryTakeOne(BeansLine, At(5, 9.0)));
            Assert.IsTrue(shelves.TryTakeOne(BreadLine, At(4, 10.0)));
            Assert.IsTrue(shelves.TryTakeOne(BreadLine, At(4, 10.0)));
            Assert.IsTrue(shelves.TryTakeOne(BeansLine, At(4, 10.0)));
            Assert.IsTrue(shelves.TryTakeOne(BreadLine, At(4, 9.0)));

            SaveData save = SaveMigration.NewGame();
            IslandShelvesHost.WritePurchases(save, shelves);
            List<StorePurchaseRowDto> rows = save.StorePurchases;
            Assert.That(rows.Count, Is.EqualTo(4), "two loaves at one moment fold into one row");
            Assert.That(rows[0].ShelfLineId, Is.EqualTo(BreadLine));
            Assert.That(rows[0].Seconds, Is.EqualTo(At(4, 9.0)));
            Assert.That(rows[1].ShelfLineId, Is.EqualTo(BeansLine));
            Assert.That(rows[2].ShelfLineId, Is.EqualTo(BreadLine));
            Assert.That(rows[2].Units, Is.EqualTo(2.0), "both loaves");
            Assert.That(rows[2].Day, Is.EqualTo(4));
            Assert.That(rows[3].Day, Is.EqualTo(5));
            foreach (StorePurchaseRowDto row in rows) Assert.That(row.ShopId, Is.EqualTo(ShopA));

            string written = SaveSerialization.ToJson(save);
            SaveData loaded = SaveSerialization.FromJson(written);
            Assert.That(SaveSerialization.ToJson(loaded), Is.EqualTo(written), "the load repair has nothing to mend");

            // Writing again, and writing what was read, changes nothing either.
            IslandShelvesHost.WritePurchases(save, shelves);
            Assert.That(SaveSerialization.ToJson(save), Is.EqualTo(written));
            IslandStoreShelves rebuilt = Groceries(ShopA, good);
            IslandShelvesHost.ReadPurchases(loaded, rebuilt);
            IslandShelvesHost.WritePurchases(loaded, rebuilt);
            Assert.That(SaveSerialization.ToJson(loaded), Is.EqualTo(written));
            foreach (double t in new[] { At(4, 9.5), At(4, 11.0), At(5, 10.0), At(6, 8.0) })
            {
                Assert.That(rebuilt.CountAt(BreadLine, t), Is.EqualTo(shelves.CountAt(BreadLine, t)));
                Assert.That(rebuilt.CountAt(BeansLine, t), Is.EqualTo(shelves.CountAt(BeansLine, t)));
            }
        }

        [Test]
        public void TheSamePurchases_WriteTheSameRows_WhicheverShopIsWrittenFirst()
        {
            IslandGoodDef good = Good("islandgood.test_groceries", 20f);
            IslandStoreShelves a1 = Groceries(ShopA, good), b1 = Groceries(ShopB, good);
            IslandStoreShelves a2 = Groceries(ShopA, good), b2 = Groceries(ShopB, good);
            foreach (IslandStoreShelves s in new[] { a1, a2 })
            {
                Assert.IsTrue(s.TryTakeOne(BreadLine, At(4, 9.0)));
                Assert.IsTrue(s.TryTakeOne(BeansLine, At(6, 9.0)));
            }
            foreach (IslandStoreShelves s in new[] { b1, b2 })
                Assert.IsTrue(s.TryTakeOne(BreadLine, At(5, 9.0)));

            SaveData first = SaveMigration.NewGame(), second = SaveMigration.NewGame();
            IslandShelvesHost.WritePurchases(first, a1);
            IslandShelvesHost.WritePurchases(first, b1);
            IslandShelvesHost.WritePurchases(second, b2);
            IslandShelvesHost.WritePurchases(second, a2);
            Assert.That(SaveSerialization.ToJson(second), Is.EqualTo(SaveSerialization.ToJson(first)));
            Assert.That(first.StorePurchases.Count, Is.EqualTo(3));
            Assert.That(first.StorePurchases[1].ShopId, Is.EqualTo(ShopB), "in time order across shops");
        }

        [Test]
        public void WritingOneShop_LeavesTheOthersRowsAlone_AndEachShopReadsOnlyItsOwn()
        {
            IslandGoodDef good = Good("islandgood.test_groceries", 20f);
            IslandStoreShelves a = Groceries(ShopA, good), b = Groceries(ShopB, good);
            Assert.IsTrue(a.TryTakeOne(BreadLine, At(4, 9.0)));
            Assert.IsTrue(b.TryTakeOne(BreadLine, At(4, 9.0)));
            Assert.IsTrue(b.TryTakeOne(BeansLine, At(4, 9.5)));

            SaveData save = SaveMigration.NewGame();
            IslandShelvesHost.WritePurchases(save, a);
            IslandShelvesHost.WritePurchases(save, b);
            Assert.IsTrue(a.TryTakeOne(BreadLine, At(4, 11.0)));
            IslandShelvesHost.WritePurchases(save, a);
            Assert.That(StorePurchaseRows.CountFor(save.StorePurchases, ShopA), Is.EqualTo(2));
            Assert.That(StorePurchaseRows.CountFor(save.StorePurchases, ShopB), Is.EqualTo(2), "the other shop's rows are untouched");

            // Rows the store cannot use are skipped, not guessed at.
            save.StorePurchases.Add(new StorePurchaseRowDto { ShopId = ShopA, ShelfLineId = "line.test_gone", Units = 1, Day = 4, Seconds = At(4, 9.0) });
            SaveData loaded = Reloaded(save);
            IslandStoreShelves ra = Groceries(ShopA, good), rb = Groceries(ShopB, good);
            Assert.That(IslandShelvesHost.ReadPurchases(loaded, ra), Is.EqualTo(2), "its own two, not the line it no longer has");
            Assert.That(IslandShelvesHost.ReadPurchases(loaded, rb), Is.EqualTo(2));
            double t = At(4, 12.0);
            Assert.That(ra.CountAt(BreadLine, t), Is.EqualTo(a.CountAt(BreadLine, t)));
            Assert.That(rb.CountAt(BeansLine, t), Is.EqualTo(b.CountAt(BeansLine, t)));

            Assert.That(IslandShelvesHost.ReadPurchases(SaveMigration.NewGame(), ra), Is.EqualTo(0), "a new game holds no purchase");
            Assert.That(ra.CountAt(BreadLine, t), Is.EqualTo(Groceries(ShopA, good).CountAt(BreadLine, t)));
        }

        [Test]
        public void RowsOlderThanTheShelfsLookBack_AreDroppedByTheWriter()
        {
            IslandGoodDef good = Good("islandgood.test_groceries", 20f);
            IslandStoreShelves shelves = Groceries(ShopA, good);
            Assert.IsTrue(shelves.TryTakeOne(BreadLine, At(4, 9.0)));
            Assert.IsTrue(shelves.TryTakeOne(BreadLine, At(20, 9.0)));
            SaveData save = SaveMigration.NewGame();
            IslandShelvesHost.WritePurchases(save, shelves);
            Assert.That(save.StorePurchases.Count, Is.EqualTo(2));

            Assert.IsFalse(shelves.DropOldPurchases(At(4 + Lookback, 9.0)), "still inside the look-back");
            Assert.That(shelves.HeldDrawCount, Is.EqualTo(2));

            Assert.IsTrue(shelves.DropOldPurchases(At(5 + Lookback, 9.0)), "the day-4 purchase is past it");
            IslandShelvesHost.WritePurchases(save, shelves);
            Assert.That(save.StorePurchases.Count, Is.EqualTo(1));
            Assert.That(save.StorePurchases[0].Day, Is.EqualTo(20));
            Assert.IsFalse(shelves.DropOldPurchases(At(5 + Lookback, 9.0)), "nothing more to drop");

            // A purchase itself drops what is past the look-back, so the list a long game writes stays bounded.
            Assert.IsTrue(shelves.TryTakeOne(BreadLine, At(21 + Lookback, 9.0)));
            IslandShelvesHost.WritePurchases(save, shelves);
            Assert.That(save.StorePurchases.Count, Is.EqualTo(1));
            Assert.That(save.StorePurchases[0].Day, Is.EqualTo(21 + Lookback));
        }

        [Test]
        public void APurchaseTellsTheWriterOnce_AndARefusalTellsNoOne()
        {
            IslandGoodDef good = Good("islandgood.test_groceries", 20f);
            IslandStoreShelves shelves = Groceries(ShopA, good);
            int told = 0;
            shelves.PurchasesChanged = () => told++;
            double now = At(4, 8.0);
            int bought = BuyOut(shelves, BreadLine, now);
            Assert.That(told, Is.EqualTo(bought));
            Assert.IsFalse(shelves.TryTakeOne(BreadLine, now));

            Assert.That(told, Is.EqualTo(bought), "an empty shelf changes nothing");

            shelves.ImportPurchases(new List<StorePurchaseRowDto>());
            shelves.DropOldPurchases(At(400, 8.0));
            Assert.That(told, Is.EqualTo(bought), "a load and a clean-up are not purchases");
        }
    }
}
