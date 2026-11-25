using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// v25 -> v26: STORE PURCHASES, PAID SPECIAL ORDERS AND THE SHOPPING BAG (one bump for the three, the
    /// rows; the bag and the purchases are read and written by the game, the orders are reserved). Pins the obligations of a schema bump: an older
    /// save survives and starts with none of the three (nothing invented, nothing else moved), a missing or
    /// damaged list is repaired, full rows come back from the real JSON exactly, and the repair never
    /// destroys something the player paid for or owns.
    /// </summary>
    public class SaveMigrationV26Tests
    {
        const string Shop = "shop.st_peters_general";
        const string Bread = "supply.bread";
        const string Milk = "supply.milk";
        const string Beans = "supply.beans";
        const string Paper = "shoppingbag.paper";
        const string StPeters = "region.st_peters";

        // ---- 1. an older save ---------------------------------------------------------------------------

        [Test]
        public void TheSchemaIsAtLeast26()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 26);
        }

        [Test]
        public void AV25Save_LoadsWithNoneOfTheThree_AndItsOwnRowsUntouched()
        {
            SaveData save = SaveSerialization.FromJson(
                "{\"SchemaVersion\":25,\"WorldSeed\":77,\"GameTimeSeconds\":4321.5,\"Money\":250,\"DayIndex\":3," +
                "\"OwnedBoats\":[\"boat.dory\"],\"ActiveHullId\":\"boat.dory\"," +
                "\"OnboardingFlags\":[{\"Key\":\"met_ginny\",\"Value\":true}]," +
                "\"SupplyStock\":[{\"SupplyId\":\"supply.ice\",\"Count\":4},{\"SupplyId\":\"supply.bread\",\"Count\":2}]," +
                "\"CaughtSpecies\":[{\"SpeciesId\":\"fish.atlantic_cod\",\"DisplayName\":\"Atlantic cod\"," +
                    "\"Count\":3,\"ReleasedCount\":1,\"RecordWeightKg\":4.5}]," +
                "\"StoredContainers\":[{\"Key\":\"storage.stpeters.ginny_rod_rack\"," +
                    "\"ContainerDefId\":\"stowage.ginny_rod_rack\"," +
                    "\"Items\":[{\"ItemId\":\"tool.rod\",\"InstanceId\":\"tool.st_peters.rod\",\"Count\":1}]," +
                    "\"Catch\":[]}]}");

            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsNotNull(save.StorePurchases);
            Assert.IsNotNull(save.SpecialOrders);
            Assert.IsNotNull(save.ShoppingBags);
            Assert.AreEqual(0, save.StorePurchases.Count, "a v25 save recorded no purchase: the shelves are the calendar's");
            Assert.AreEqual(0, save.SpecialOrders.Count, "no order is invented");
            Assert.AreEqual(0, save.ShoppingBags.Count, "no bag is invented: his goods are in his supplies");
            Assert.AreEqual(1, save.NextSpecialOrderNumber, "the first order will be number 1");

            Assert.AreEqual(77, save.WorldSeed);
            Assert.AreEqual(4321.5d, save.GameTimeSeconds);
            Assert.AreEqual(250, save.Money);
            Assert.AreEqual(3, save.DayIndex);
            CollectionAssert.AreEqual(new[] { "boat.dory" }, save.OwnedBoats);
            Assert.AreEqual("boat.dory", save.ActiveHullId);
            Assert.AreEqual(1, save.OnboardingFlags.Count);
            Assert.AreEqual(2, save.SupplyStock.Count);
            Assert.AreEqual(4, SupplyLocker.Count(save, "supply.ice"));
            Assert.AreEqual(2, SupplyLocker.Count(save, "supply.bread"));
            Assert.AreEqual(1, save.CaughtSpecies.Count);
            Assert.AreEqual(3, save.CaughtSpecies[0].Count);
            Assert.AreEqual(1, save.StoredContainers.Count);
            Assert.AreEqual("tool.st_peters.rod", save.StoredContainers[0].Items[0].InstanceId);
        }

        [Test]
        public void TheStep_MovesNothingElseInTheWholeFile()
        {
            // The same game written twice: once as it stands, once stamped v25 and walked through the step.
            // With the three new lists empty in both, the two files must be the same text.
            SaveData save = SaveMigration.NewGame();
            save.WorldSeed = 4242;
            save.GameTimeSeconds = 9876.5;
            save.Money = 321;
            save.DayIndex = 8;
            save.OwnedBoats.Add("boat.dory");
            save.ActiveHullId = "boat.dory";
            save.OnboardingFlags.Add(new SaveFlag("met_ginny", true));
            save.OwnedGear.Add("tool.rod");
            SupplyLocker.Add(save, "supply.ice", 4);
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, "storage.stpeters.ginny_rod_rack",
                                                            "stowage.ginny_rod_rack", "tool.rod",
                                                            "tool.st_peters.rod", 2));
            save = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            string asItStands = SaveSerialization.ToJson(save);

            save.SchemaVersion = 25;
            SaveMigration.Migrate(save);

            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.AreEqual(asItStands, SaveSerialization.ToJson(save));
        }

        // ---- 2. the whole chain -------------------------------------------------------------------------

        [Test]
        public void AV20Save_WalksTheWholeChain()
        {
            SaveData save = SaveSerialization.FromJson("{\"SchemaVersion\":20,\"GameTimeSeconds\":4321.0,\"Money\":12}");
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.AreEqual(4321d, save.GameTimeSeconds);
            Assert.AreEqual(12, save.Money);
            Assert.IsNotNull(save.KitchenStores, "the food stores' v23 step still ran");
            Assert.IsNotNull(save.CaughtSpecies, "the catch record's v24 step still ran");
            Assert.IsNotNull(save.StoredContainers, "the storage step (v25) still ran");
            Assert.AreEqual(0, save.StorePurchases.Count);
            Assert.AreEqual(0, save.SpecialOrders.Count);
            Assert.AreEqual(0, save.ShoppingBags.Count);
            Assert.AreEqual(1, save.NextSpecialOrderNumber);
        }

        [Test]
        public void AVersionlessSave_WalksTheWholeChainToo()
        {
            SaveData save = SaveSerialization.FromJson("{\"SchemaVersion\":0,\"WorldSeed\":42,\"Money\":15}");
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.AreEqual(42, save.WorldSeed);
            Assert.AreEqual(15, save.Money);
            Assert.IsNotNull(save.StorePurchases);
            Assert.IsNotNull(save.SpecialOrders);
            Assert.IsNotNull(save.ShoppingBags);
            Assert.AreEqual(1, save.NextSpecialOrderNumber);
        }

        // ---- 3. null lists ------------------------------------------------------------------------------

        [Test]
        public void AV25ObjectWithNullLists_IsRepaired()
        {
            SaveData save = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = 25, StorePurchases = null, SpecialOrders = null, ShoppingBags = null,
                NextSpecialOrderNumber = 0,
            });
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsNotNull(save.StorePurchases);
            Assert.IsNotNull(save.SpecialOrders);
            Assert.IsNotNull(save.ShoppingBags);
            Assert.AreEqual(0, save.StorePurchases.Count + save.SpecialOrders.Count + save.ShoppingBags.Count);
            Assert.AreEqual(1, save.NextSpecialOrderNumber);
        }

        [Test]
        public void ACurrentSaveWithNullLists_IsRepaired()
        {
            SaveData save = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = SaveMigration.CurrentVersion,
                StorePurchases = null, SpecialOrders = null, ShoppingBags = null,
            });
            Assert.IsNotNull(save.StorePurchases);
            Assert.IsNotNull(save.SpecialOrders);
            Assert.IsNotNull(save.ShoppingBags);
        }

        [Test]
        public void ACurrentFileThatLeavesFieldsOut_LoadsUsable()
        {
            // A hand-edited file: an order with no Lines and no DeliverToId, a bag with no Items.
            SaveData save = SaveSerialization.FromJson(
                "{\"SchemaVersion\":" + SaveMigration.CurrentVersion + "," +
                "\"SupplyStock\":[{\"SupplyId\":\"supply.bread\",\"Count\":2}]," +
                "\"SpecialOrders\":[{\"OrderId\":\"order.shop.st_peters_general.3\"," +
                    "\"SellerId\":\"shop.st_peters_general\",\"PaidPrice\":120,\"DueDay\":6}]," +
                "\"ShoppingBags\":[{\"BagDefId\":\"shoppingbag.paper\",\"Held\":true}]}");

            Assert.AreEqual(1, save.SpecialOrders.Count, "he paid for it: it is kept");
            SpecialOrderDto order = save.SpecialOrders[0];
            Assert.AreEqual(120, order.PaidPrice);
            Assert.AreEqual(6, order.DueDay);
            Assert.IsNotNull(order.Lines);
            Assert.AreEqual("", order.DeliverToId, "left out means the seller's own counter");
            Assert.AreEqual(SpecialOrderDto.StateWaiting, order.State);
            Assert.AreEqual(4, save.NextSpecialOrderNumber, "number 3 is taken");
            Assert.AreEqual(0, save.ShoppingBags.Count, "a bag with nothing in it leaves no row");
            Assert.AreEqual(2, SupplyLocker.Count(save, "supply.bread"));
        }

        // ---- 4. full rows through the real JSON ---------------------------------------------------------

        [Test]
        public void FullRowsOfAllThreeKinds_ComeBackFromTheJsonExactly()
        {
            SaveData save = SaveMigration.NewGame();
            SupplyLocker.Add(save, Bread, 3);
            SupplyLocker.Add(save, Milk, 2);
            save.StorePurchases.Add(new StorePurchaseRowDto
            {
                ShopId = Shop, ShelfLineId = "line.bread", Units = 1.2, Day = 1, Seconds = 86461.125,
            });
            save.StorePurchases.Add(new StorePurchaseRowDto
            {
                ShopId = Shop, ShelfLineId = "line.milk", Units = 0.75, Day = 2, Seconds = 180000.5,
            });
            save.SpecialOrders.Add(new SpecialOrderDto
            {
                OrderId = "order.shop.st_peters_general.4", SellerId = Shop, DeliverToId = "counter.nmc_chandlery",
                Lines =
                {
                    new SpecialOrderLineDto { ListingId = "specialorder.outboard_9hp", Count = 1, PaidPrice = 1390 },
                    new SpecialOrderLineDto { ListingId = "specialorder.fuel_can", Count = 2, PaidPrice = 60 },
                },
                PaidPrice = 1450, OrderedAtSeconds = 90000.25, OrderedDay = 1, DueDay = 5,
                State = SpecialOrderDto.StateArrived,
            });
            save.NextSpecialOrderNumber = 5;
            save.ShoppingBags.Add(new ShoppingBagDto
            {
                BagDefId = Paper, Held = false, Region = StPeters, PosX = 12.5f, PosY = -3.25f, Level = 1,
                HullId = "", ByTheBed = true,
                Items = { new BagItemDto { SupplyId = Bread, Count = 2 }, new BagItemDto { SupplyId = Milk, Count = 1 } },
            });

            SaveData back = SaveSerialization.FromJson(SaveSerialization.ToJson(save));

            Assert.AreEqual(2, back.StorePurchases.Count);
            StorePurchaseRowDto p = back.StorePurchases[0];
            Assert.AreEqual(Shop, p.ShopId);
            Assert.AreEqual("line.bread", p.ShelfLineId);
            Assert.AreEqual(1.2, p.Units, "fractional units come back exactly");
            Assert.AreEqual(1, p.Day);
            Assert.AreEqual(86461.125, p.Seconds, "fractional seconds come back exactly");
            Assert.AreEqual("line.milk", back.StorePurchases[1].ShelfLineId);
            Assert.AreEqual(0.75, back.StorePurchases[1].Units);
            Assert.AreEqual(180000.5, back.StorePurchases[1].Seconds);

            Assert.AreEqual(1, back.SpecialOrders.Count);
            SpecialOrderDto o = back.SpecialOrders[0];
            Assert.AreEqual("order.shop.st_peters_general.4", o.OrderId);
            Assert.AreEqual(Shop, o.SellerId);
            Assert.AreEqual("counter.nmc_chandlery", o.DeliverToId, "an order collected somewhere else is held");
            Assert.AreEqual(2, o.Lines.Count, "one order may hold more than one kind of good");
            Assert.AreEqual("specialorder.outboard_9hp", o.Lines[0].ListingId);
            Assert.AreEqual(1, o.Lines[0].Count);
            Assert.AreEqual(1390, o.Lines[0].PaidPrice, "each line keeps what was paid for it");
            Assert.AreEqual("specialorder.fuel_can", o.Lines[1].ListingId);
            Assert.AreEqual(2, o.Lines[1].Count);
            Assert.AreEqual(60, o.Lines[1].PaidPrice);
            Assert.AreEqual(1450, o.PaidPrice, "the order keeps the total");
            Assert.AreEqual(90000.25, o.OrderedAtSeconds);
            Assert.AreEqual(1, o.OrderedDay, "the day he paid is saved, not worked out from the seconds");
            Assert.AreEqual(5, o.DueDay);
            Assert.AreEqual(SpecialOrderDto.StateArrived, o.State);
            Assert.AreEqual(5, back.NextSpecialOrderNumber);

            Assert.AreEqual(1, back.ShoppingBags.Count);
            ShoppingBagDto b = back.ShoppingBags[0];
            Assert.AreEqual(Paper, b.BagDefId);
            Assert.IsFalse(b.Held);
            Assert.AreEqual(StPeters, b.Region);
            Assert.AreEqual(12.5f, b.PosX);
            Assert.AreEqual(-3.25f, b.PosY);
            Assert.AreEqual(1, b.Level);
            Assert.AreEqual("", b.HullId);
            Assert.IsTrue(b.ByTheBed);
            Assert.AreEqual(2, b.Items.Count);
            Assert.AreEqual(Bread, b.Items[0].SupplyId);
            Assert.AreEqual(2, b.Items[0].Count);
            Assert.AreEqual(Milk, b.Items[1].SupplyId);
            Assert.AreEqual(1, b.Items[1].Count);

            Assert.AreEqual(SaveSerialization.ToJson(back),
                            SaveSerialization.ToJson(SaveSerialization.FromJson(SaveSerialization.ToJson(back))),
                            "write, load, write is a fixed point with the new rows filled in");
        }

        // ---- 5. damaged rows ----------------------------------------------------------------------------

        [Test]
        public void DamagedPurchasesAreDropped_EqualOnesFold_AndTheRestIsSortedForReplay()
        {
            SaveData save = SaveMigration.NewGame();
            save.StorePurchases = new List<StorePurchaseRowDto>
            {
                null,
                new StorePurchaseRowDto { ShopId = "", ShelfLineId = "line.bread", Units = 1, Day = 1, Seconds = 10 },
                new StorePurchaseRowDto { ShopId = null, ShelfLineId = "line.bread", Units = 1, Day = 1, Seconds = 10 },
                new StorePurchaseRowDto { ShopId = Shop, ShelfLineId = "", Units = 1, Day = 1, Seconds = 10 },
                new StorePurchaseRowDto { ShopId = Shop, ShelfLineId = "line.bread", Units = 1, Day = -1, Seconds = 10 },
                new StorePurchaseRowDto { ShopId = Shop, ShelfLineId = "line.bread", Units = 1, Day = 1, Seconds = -5 },
                new StorePurchaseRowDto { ShopId = Shop, ShelfLineId = "line.bread", Units = 1, Day = 1, Seconds = double.NaN },
                new StorePurchaseRowDto { ShopId = Shop, ShelfLineId = "line.bread", Units = 0, Day = 1, Seconds = 10 },
                new StorePurchaseRowDto { ShopId = Shop, ShelfLineId = "line.bread", Units = -1, Day = 1, Seconds = 10 },
                new StorePurchaseRowDto { ShopId = Shop, ShelfLineId = "line.bread", Units = double.NaN, Day = 1, Seconds = 10 },
                new StorePurchaseRowDto { ShopId = Shop, ShelfLineId = "line.bread", Units = double.PositiveInfinity, Day = 1, Seconds = 10 },
                // the good ones, out of order, one of them written twice
                new StorePurchaseRowDto { ShopId = Shop, ShelfLineId = "line.milk", Units = 1, Day = 2, Seconds = 200.5 },
                new StorePurchaseRowDto { ShopId = Shop, ShelfLineId = "line.milk", Units = 2, Day = 1, Seconds = 100.25 },
                new StorePurchaseRowDto { ShopId = Shop, ShelfLineId = "line.bread", Units = 0.5, Day = 1, Seconds = 100.25 },
                new StorePurchaseRowDto { ShopId = Shop, ShelfLineId = "line.bread", Units = 0.25, Day = 1, Seconds = 100.25 },
                new StorePurchaseRowDto { ShopId = "shop.another", ShelfLineId = "line.bread", Units = 3, Day = 1, Seconds = 100.25 },
                new StorePurchaseRowDto { ShopId = Shop, ShelfLineId = "line.bread", Units = 4, Day = 1, Seconds = 50 },
            };

            SaveMigration.Migrate(save);

            Assert.AreEqual("1|50|shop.st_peters_general|line.bread|4;" +
                            "1|100.25|shop.another|line.bread|3;" +
                            "1|100.25|shop.st_peters_general|line.bread|0.75;" +
                            "1|100.25|shop.st_peters_general|line.milk|2;" +
                            "2|200.5|shop.st_peters_general|line.milk|1;",
                            Purchases(save),
                            "bad rows go, the row written twice is one row with both amounts, and the order is " +
                            "day, seconds, shop, line");

            SaveMigration.Migrate(save);
            Assert.AreEqual("1|50|shop.st_peters_general|line.bread|4;" +
                            "1|100.25|shop.another|line.bread|3;" +
                            "1|100.25|shop.st_peters_general|line.bread|0.75;" +
                            "1|100.25|shop.st_peters_general|line.milk|2;" +
                            "2|200.5|shop.st_peters_general|line.milk|1;",
                            Purchases(save), "a second repair changes nothing");
        }

        static string Purchases(SaveData save)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            foreach (StorePurchaseRowDto r in save.StorePurchases)
                sb.Append(r.Day).Append('|').Append(r.Seconds.ToString(inv)).Append('|').Append(r.ShopId).Append('|')
                  .Append(r.ShelfLineId).Append('|').Append(r.Units.ToString(inv)).Append(';');
            return sb.ToString();
        }

        [Test]
        public void APaidOrderIsNeverDropped_HoweverDamaged()
        {
            SaveData save = SaveMigration.NewGame();
            save.Money = 10;
            save.SpecialOrders = new List<SpecialOrderDto>
            {
                null,
                // nothing paid and nothing on it: the only kind of row that goes
                new SpecialOrderDto { OrderId = "order.shop.a.7", SellerId = "shop.a", PaidPrice = 0 },
                // paid, with no id, no seller and every line unusable: kept, with what he paid
                new SpecialOrderDto
                {
                    OrderId = null, SellerId = null, DeliverToId = null, PaidPrice = 120,
                    Lines = { null, new SpecialOrderLineDto { ListingId = "", Count = 3 },
                              new SpecialOrderLineDto { ListingId = "listing.x", Count = 0 } },
                },
                // paid, with a null line list
                new SpecialOrderDto { OrderId = "order.shop.a.2", SellerId = "shop.a", PaidPrice = 40, Lines = null },
                // paid, marked collected: still kept (pruning collected rows is the feature's, not the load's)
                new SpecialOrderDto
                {
                    OrderId = "order.shop.a.3", SellerId = "shop.a", PaidPrice = 15,
                    State = SpecialOrderDto.StateCollected,
                    Lines = { new SpecialOrderLineDto { ListingId = "listing.rope", Count = 1 } },
                },
                // free, but goods are owed on it: kept
                new SpecialOrderDto
                {
                    OrderId = "order.shop.a.4", SellerId = "shop.a", PaidPrice = 0,
                    Lines = { new SpecialOrderLineDto { ListingId = "listing.rope", Count = 2 } },
                },
            };

            SaveMigration.Migrate(save);

            Assert.AreEqual(4, save.SpecialOrders.Count, "only the null row and the row carrying nothing are dropped");
            SpecialOrderDto damaged = save.SpecialOrders[0];
            Assert.AreEqual(120, damaged.PaidPrice, "what he paid is still on the row, for the refund");
            Assert.AreEqual("", damaged.OrderId, "an id is repaired to empty, never invented");
            Assert.AreEqual("", damaged.SellerId);
            Assert.AreEqual("", damaged.DeliverToId);
            Assert.AreEqual(0, damaged.Lines.Count);
            Assert.AreEqual(40, save.SpecialOrders[1].PaidPrice);
            Assert.IsNotNull(save.SpecialOrders[1].Lines);
            Assert.AreEqual(SpecialOrderDto.StateCollected, save.SpecialOrders[2].State);
            Assert.AreEqual(15, save.SpecialOrders[2].PaidPrice);
            Assert.AreEqual("order.shop.a.4", save.SpecialOrders[3].OrderId);
            Assert.AreEqual(10, save.Money, "the repair never touches his purse");

            string once = SaveSerialization.ToJson(save);
            SaveMigration.Migrate(save);
            Assert.AreEqual(once, SaveSerialization.ToJson(save), "a second repair changes nothing");
        }

        [Test]
        public void InsideAKeptOrder_BadLinesGo_EqualOnesFold_AndUnreadableNumbersReadSafe()
        {
            SaveData save = SaveMigration.NewGame();
            save.SpecialOrders = new List<SpecialOrderDto>
            {
                new SpecialOrderDto
                {
                    OrderId = "order.shop.a.1", SellerId = "shop.a", PaidPrice = 60,
                    OrderedAtSeconds = double.NaN, OrderedDay = -4, DueDay = -3, State = 9,
                    Lines =
                    {
                        new SpecialOrderLineDto { ListingId = "listing.rope", Count = 1 },
                        new SpecialOrderLineDto { ListingId = "", Count = 3 },
                        new SpecialOrderLineDto { ListingId = "listing.rope", Count = 2 },
                        new SpecialOrderLineDto { ListingId = "listing.hooks", Count = 0 },
                        new SpecialOrderLineDto { ListingId = "listing.paint", Count = 1 },
                    },
                },
                new SpecialOrderDto
                {
                    OrderId = "order.shop.a.2", SellerId = "shop.a", PaidPrice = -50, State = -1,
                    OrderedAtSeconds = -10, OrderedDay = 6, DueDay = 2,
                    Lines = { new SpecialOrderLineDto { ListingId = "listing.rope", Count = 1 } },
                },
            };

            SaveMigration.Migrate(save);

            Assert.AreEqual(2, save.SpecialOrders.Count);
            SpecialOrderDto a = save.SpecialOrders[0];
            Assert.AreEqual(2, a.Lines.Count);
            Assert.AreEqual("listing.rope", a.Lines[0].ListingId);
            Assert.AreEqual(3, a.Lines[0].Count, "two lines for one listing are one line with both counts");
            Assert.AreEqual("listing.paint", a.Lines[1].ListingId);
            Assert.AreEqual(60, a.PaidPrice);
            Assert.AreEqual(0d, a.OrderedAtSeconds);
            Assert.AreEqual(0, a.OrderedDay, "a negative ordered day reads 0");
            Assert.AreEqual(0, a.DueDay, "an unreadable due day is due at once, never later");
            Assert.AreEqual(SpecialOrderDto.StateWaiting, a.State,
                            "an unknown state reads as waiting: the one reading that cannot cost him the goods");

            SpecialOrderDto b = save.SpecialOrders[1];
            Assert.AreEqual(0, b.PaidPrice, "a negative price would be a refund that takes money");
            Assert.AreEqual(SpecialOrderDto.StateWaiting, b.State);
            Assert.AreEqual(0d, b.OrderedAtSeconds);
            Assert.AreEqual(1, b.Lines.Count, "goods are still owed on it, so it stays");
            Assert.AreEqual(6, b.OrderedDay, "a readable ordered day is kept");
            Assert.AreEqual(2, b.DueDay, "a due day before the ordered day is left as it is: the repair does not guess");

            string once = SaveSerialization.ToJson(save);
            SaveMigration.Migrate(save);
            Assert.AreEqual(once, SaveSerialization.ToJson(save), "a second repair changes nothing");
        }

        [Test]
        public void MoneyOnALineIsNeverDropped_NorRewritten()
        {
            SaveData save = SaveMigration.NewGame();
            save.Money = 10;
            save.SpecialOrders = new List<SpecialOrderDto>
            {
                // 0: unusable lines, some paid for: the paid ones stay, the unpaid ones go
                new SpecialOrderDto
                {
                    OrderId = "order.shop.a.1", SellerId = "shop.a", PaidPrice = 100,
                    Lines =
                    {
                        new SpecialOrderLineDto { ListingId = "specialorder.rope", Count = 0, PaidPrice = 30 },
                        new SpecialOrderLineDto { ListingId = "specialorder.hooks", Count = -2, PaidPrice = 20 },
                        new SpecialOrderLineDto { ListingId = null, Count = 1, PaidPrice = 5 },
                        new SpecialOrderLineDto { ListingId = "specialorder.paint", Count = 0, PaidPrice = 0 },
                        new SpecialOrderLineDto { ListingId = "", Count = 4, PaidPrice = -7 },
                    },
                },
                // 1: the order's own total reads 0, but a line was paid for: the row stays
                new SpecialOrderDto
                {
                    OrderId = "order.shop.a.2", SellerId = "shop.a", PaidPrice = 0,
                    Lines = { new SpecialOrderLineDto { ListingId = "", Count = 0, PaidPrice = 45 } },
                },
                // 2: two lines for one listing fold, prices added; the lines sum to 70, the order says 90:
                //    both are left exactly as they are
                new SpecialOrderDto
                {
                    OrderId = "order.shop.a.3", SellerId = "shop.a", PaidPrice = 90,
                    Lines =
                    {
                        new SpecialOrderLineDto { ListingId = "specialorder.rope", Count = 1, PaidPrice = 25 },
                        new SpecialOrderLineDto { ListingId = "specialorder.net", Count = 1, PaidPrice = 10 },
                        new SpecialOrderLineDto { ListingId = "specialorder.rope", Count = 2, PaidPrice = 35 },
                    },
                },
                // 3: the lines sum to more than the total: still both as they are
                new SpecialOrderDto
                {
                    OrderId = "order.shop.a.4", SellerId = "shop.a", PaidPrice = 10,
                    Lines = { new SpecialOrderLineDto { ListingId = "specialorder.net", Count = 1, PaidPrice = 500 } },
                },
                // 4: nothing paid on the order, and its only line is unusable and unpaid: the row goes
                new SpecialOrderDto
                {
                    OrderId = "order.shop.a.5", SellerId = "shop.a", PaidPrice = 0,
                    Lines = { new SpecialOrderLineDto { ListingId = "", Count = 2, PaidPrice = -3 } },
                },
            };

            SaveMigration.Migrate(save);

            Assert.AreEqual(4, save.SpecialOrders.Count, "only the row with no money and no usable line goes");

            SpecialOrderDto a = save.SpecialOrders[0];
            Assert.AreEqual(3, a.Lines.Count, "a line he paid for stays, whatever else is wrong with it");
            Assert.AreEqual("specialorder.rope", a.Lines[0].ListingId);
            Assert.AreEqual(0, a.Lines[0].Count);
            Assert.AreEqual(30, a.Lines[0].PaidPrice);
            Assert.AreEqual("specialorder.hooks", a.Lines[1].ListingId);
            Assert.AreEqual(0, a.Lines[1].Count, "a negative count on a kept line reads 0");
            Assert.AreEqual(20, a.Lines[1].PaidPrice);
            Assert.AreEqual("", a.Lines[2].ListingId, "a missing listing is repaired to empty, never invented");
            Assert.AreEqual(1, a.Lines[2].Count);
            Assert.AreEqual(5, a.Lines[2].PaidPrice);
            Assert.AreEqual(100, a.PaidPrice, "the total is not rewritten to fit the lines (55)");

            SpecialOrderDto b = save.SpecialOrders[1];
            Assert.AreEqual("order.shop.a.2", b.OrderId, "a paid line keeps its order alive");
            Assert.AreEqual(0, b.PaidPrice, "and the total is not made up from the line");
            Assert.AreEqual(1, b.Lines.Count);
            Assert.AreEqual(45, b.Lines[0].PaidPrice);

            SpecialOrderDto c = save.SpecialOrders[2];
            Assert.AreEqual(2, c.Lines.Count);
            Assert.AreEqual("specialorder.rope", c.Lines[0].ListingId);
            Assert.AreEqual(3, c.Lines[0].Count);
            Assert.AreEqual(60, c.Lines[0].PaidPrice, "folded lines add their prices, so the lines' sum is unchanged");
            Assert.AreEqual(10, c.Lines[1].PaidPrice);
            Assert.AreEqual(90, c.PaidPrice, "lines sum to 70, the order says 90: both stand");

            SpecialOrderDto d = save.SpecialOrders[3];
            Assert.AreEqual(10, d.PaidPrice);
            Assert.AreEqual(500, d.Lines[0].PaidPrice);

            Assert.AreEqual(10, save.Money, "the repair never touches his purse");

            string once = SaveSerialization.ToJson(save);
            SaveMigration.Migrate(save);
            Assert.AreEqual(once, SaveSerialization.ToJson(save), "a second repair changes nothing");
        }

        [Test]
        public void ADamagedFile_MigratedTwice_IsTheSameAsMigratedOnce()
        {
            // A v25 file with damage in its own rows: the same species twice, the same container twice, a
            // supply row with a count below zero and one with no id.
            const string damagedV25 =
                "{\"SchemaVersion\":25,\"WorldSeed\":77,\"GameTimeSeconds\":4321.5,\"Money\":250,\"DayIndex\":3," +
                "\"OwnedBoats\":[\"boat.dory\",\"boat.dory\"],\"ActiveHullId\":\"boat.dory\"," +
                "\"SupplyStock\":[{\"SupplyId\":\"supply.ice\",\"Count\":4},{\"SupplyId\":\"supply.ice\",\"Count\":2}," +
                    "{\"SupplyId\":\"supply.bread\",\"Count\":-3},{\"SupplyId\":\"\",\"Count\":1}]," +
                "\"CaughtSpecies\":[{\"SpeciesId\":\"fish.atlantic_cod\",\"DisplayName\":\"Atlantic cod\"," +
                    "\"Count\":3,\"ReleasedCount\":1,\"RecordWeightKg\":4.5}," +
                    "{\"SpeciesId\":\"fish.atlantic_cod\",\"DisplayName\":\"Atlantic cod\"," +
                    "\"Count\":2,\"ReleasedCount\":0,\"RecordWeightKg\":6.25}]," +
                "\"StoredContainers\":[{\"Key\":\"storage.stpeters.ginny_rod_rack\"," +
                    "\"ContainerDefId\":\"stowage.ginny_rod_rack\"," +
                    "\"Items\":[{\"ItemId\":\"tool.rod\",\"InstanceId\":\"tool.st_peters.rod\",\"Count\":1}]," +
                    "\"Catch\":[]}," +
                    "{\"Key\":\"storage.stpeters.ginny_rod_rack\",\"ContainerDefId\":\"stowage.ginny_rod_rack\"," +
                    "\"Items\":[{\"ItemId\":\"tool.rod\",\"InstanceId\":\"tool.st_peters.rod\",\"Count\":1}]," +
                    "\"Catch\":[]},{\"Key\":\"\",\"ContainerDefId\":\"\"}]}";

            // A current file with damage in every one of the three new lists.
            const string damagedV26 =
                "{\"SchemaVersion\":26,\"WorldSeed\":5,\"Money\":40,\"NextSpecialOrderNumber\":0," +
                "\"SupplyStock\":[{\"SupplyId\":\"supply.bread\",\"Count\":2}]," +
                "\"StorePurchases\":[{\"ShopId\":\"shop.b\",\"ShelfLineId\":\"line.milk\",\"Units\":1.5,\"Day\":2,\"Seconds\":9.5}," +
                    "{\"ShopId\":\"\",\"ShelfLineId\":\"line.milk\",\"Units\":1.0,\"Day\":1,\"Seconds\":1.0}," +
                    "{\"ShopId\":\"shop.a\",\"ShelfLineId\":\"line.bread\",\"Units\":-2.0,\"Day\":1,\"Seconds\":3.0}," +
                    "{\"ShopId\":\"shop.a\",\"ShelfLineId\":\"line.bread\",\"Units\":0.5,\"Day\":1,\"Seconds\":3.0}," +
                    "{\"ShopId\":\"shop.a\",\"ShelfLineId\":\"line.bread\",\"Units\":0.25,\"Day\":1,\"Seconds\":3.0}]," +
                "\"SpecialOrders\":[{\"OrderId\":\"order.seller.leblancs.9\",\"SellerId\":\"seller.leblancs\"," +
                    "\"PaidPrice\":80,\"OrderedAtSeconds\":-5.0,\"OrderedDay\":-1,\"DueDay\":-2,\"State\":7," +
                    "\"Lines\":[{\"ListingId\":\"specialorder.rope\",\"Count\":1,\"PaidPrice\":30}," +
                        "{\"ListingId\":\"specialorder.rope\",\"Count\":2,\"PaidPrice\":20}," +
                        "{\"ListingId\":\"\",\"Count\":0,\"PaidPrice\":15}," +
                        "{\"ListingId\":\"specialorder.net\",\"Count\":0,\"PaidPrice\":-4}]}," +
                    "{\"OrderId\":\"\",\"SellerId\":\"\",\"PaidPrice\":0,\"Lines\":[]}]," +
                "\"ShoppingBags\":[{\"BagDefId\":\"shoppingbag.paper\",\"Held\":false,\"Region\":\"region.st_peters\"," +
                    "\"PosX\":1.5,\"PosY\":2.5,\"Level\":0," +
                    "\"Items\":[{\"SupplyId\":\"supply.bread\",\"Count\":5},{\"SupplyId\":\"supply.milk\",\"Count\":1}]}," +
                    "{\"BagDefId\":\"shoppingbag.paper\",\"Held\":false,\"Region\":\"\"," +
                    "\"Items\":[{\"SupplyId\":\"supply.bread\",\"Count\":1}]}]}";

            foreach (string file in new[] { damagedV25, damagedV26 })
            {
                string once = SaveSerialization.ToJson(SaveSerialization.FromJson(file));
                string twice = SaveSerialization.ToJson(SaveSerialization.FromJson(once));
                Assert.AreEqual(once, twice, "load, save, load gives the file that one load gave");
            }

            // and the damaged current file did come out repaired, so the comparison is of repaired files
            SaveData healed = SaveSerialization.FromJson(damagedV26);
            Assert.AreEqual(1, healed.SpecialOrders.Count);
            Assert.AreEqual(2, healed.SpecialOrders[0].Lines.Count);
            Assert.AreEqual(50, healed.SpecialOrders[0].Lines[0].PaidPrice);
            Assert.AreEqual(15, healed.SpecialOrders[0].Lines[1].PaidPrice);
            Assert.AreEqual(80, healed.SpecialOrders[0].PaidPrice);
            Assert.AreEqual(0, healed.SpecialOrders[0].OrderedDay);
            Assert.AreEqual(10, healed.NextSpecialOrderNumber, "the counter is past the highest number in use");
            Assert.AreEqual(2, healed.StorePurchases.Count);
            Assert.AreEqual(1, healed.ShoppingBags.Count, "the bag with no region is gone, the other is kept");
            Assert.AreEqual(1, healed.ShoppingBags[0].Items.Count);
            Assert.AreEqual(2, healed.ShoppingBags[0].Items[0].Count, "held to the two loaves he owns");
            Assert.AreEqual(1, healed.SupplyStock.Count);
            Assert.AreEqual(2, healed.SupplyStock[0].Count, "the bag repair took nothing from his supplies");
        }

        // ---- 6. the bag never loses goods ---------------------------------------------------------------

        [Test]
        public void ABagIsHeldToWhatHeOwns_AnEmptiedBagGoes_AndHisSuppliesAreNeverTouched()
        {
            SaveData save = SaveMigration.NewGame();
            SupplyLocker.Add(save, Bread, 3);
            SupplyLocker.Add(save, Milk, 1);
            save.ShoppingBags = new List<ShoppingBagDto>
            {
                new ShoppingBagDto
                {
                    BagDefId = Paper, Held = true,
                    Items =
                    {
                        new BagItemDto { SupplyId = Bread, Count = 2 },
                        new BagItemDto { SupplyId = Milk, Count = 5 },      // he owns 1
                        new BagItemDto { SupplyId = Beans, Count = 1 },     // he owns none
                        new BagItemDto { SupplyId = Bread, Count = 4 },     // the same kind again
                        new BagItemDto { SupplyId = "", Count = 1 },
                        new BagItemDto { SupplyId = Bread, Count = 0 },
                        null,
                    },
                },
                // a second bag claiming bread the first already holds all of
                new ShoppingBagDto
                {
                    BagDefId = Paper, Held = false, Region = StPeters, PosX = 4f, PosY = 2f,
                    Items = { new BagItemDto { SupplyId = Bread, Count = 1 } },
                },
            };

            SaveMigration.Migrate(save);

            Assert.AreEqual(1, save.ShoppingBags.Count, "the bag left with nothing in it is dropped");
            ShoppingBagDto bag = save.ShoppingBags[0];
            Assert.AreEqual(2, bag.Items.Count);
            Assert.AreEqual(Bread, bag.Items[0].SupplyId);
            Assert.AreEqual(3, bag.Items[0].Count, "2 and 4 fold to 6, held to the 3 he owns");
            Assert.AreEqual(Milk, bag.Items[1].SupplyId);
            Assert.AreEqual(1, bag.Items[1].Count);

            Assert.AreEqual(2, save.SupplyStock.Count, "the repair never adds or removes a supply row");
            Assert.AreEqual(3, SupplyLocker.Owned(save, Bread), "and never changes what he owns");
            Assert.AreEqual(1, SupplyLocker.Owned(save, Milk));
            Assert.AreEqual(0, SupplyLocker.Owned(save, Beans));
            Assert.AreEqual(0, SupplyLocker.Count(save, Bread), "the bag's goods are set aside until it is put away");

            string once = SaveSerialization.ToJson(save);
            SaveMigration.Migrate(save);
            Assert.AreEqual(once, SaveSerialization.ToJson(save), "a second repair changes nothing");
        }

        [Test]
        public void ADamagedBagRowCanOnlyEverMeanFoundPutAway()
        {
            SaveData save = SaveMigration.NewGame();
            SupplyLocker.Add(save, Bread, 50);
            save.ShoppingBags = new List<ShoppingBagDto>
            {
                null,
                Bag(b => b.BagDefId = ""),                                   // no kind
                Bag(b => b.BagDefId = null),
                Bag(b => { b.Held = false; b.Region = ""; }),                // set down nowhere
                Bag(b => { b.Held = false; b.Region = null; }),
                Bag(b => { b.Held = false; b.PosX = float.NaN; }),           // set down at no position
                Bag(b => { b.Held = false; b.PosY = float.PositiveInfinity; }),
                Bag(b => { b.Held = false; b.Level = -1; }),                 // set down below the ground
                Bag(b => b.Items = null),                                    // nothing in it
                // kept: in his hand, its place fields unreadable (a held bag needs no place)
                Bag(b => { b.Held = true; b.Region = null; b.HullId = null; b.PosX = float.NaN; b.Level = -2; }),
                // kept: set down in a region no scene may claim today (a content question, not the load's)
                Bag(b => { b.Held = false; b.Region = "region.nowhere"; }),
            };

            SaveMigration.Migrate(save);

            Assert.AreEqual(2, save.ShoppingBags.Count);
            ShoppingBagDto held = save.ShoppingBags[0];
            Assert.IsTrue(held.Held);
            Assert.AreEqual("", held.Region);
            Assert.AreEqual("", held.HullId);
            Assert.AreEqual(0f, held.PosX);
            Assert.AreEqual(0, held.Level);
            Assert.AreEqual(1, held.Items.Count);
            Assert.AreEqual("region.nowhere", save.ShoppingBags[1].Region);

            Assert.AreEqual(1, save.SupplyStock.Count);
            Assert.AreEqual(50, SupplyLocker.Owned(save, Bread),
                            "every dropped bag's goods are still his: found put away, never lost");
            Assert.AreEqual(49, SupplyLocker.Count(save, Bread), "only the kept bag in his hand sets one aside");

            string once = SaveSerialization.ToJson(save);
            SaveMigration.Migrate(save);
            Assert.AreEqual(once, SaveSerialization.ToJson(save), "a second repair changes nothing");
        }

        static ShoppingBagDto Bag(System.Action<ShoppingBagDto> damage)
        {
            var bag = new ShoppingBagDto
            {
                BagDefId = Paper, Held = false, Region = StPeters, PosX = 4f, PosY = 2f, Level = 0,
                Items = { new BagItemDto { SupplyId = Bread, Count = 1 } },
            };
            damage(bag);
            return bag;
        }

        // ---- 7. the order counter -----------------------------------------------------------------------

        [Test]
        public void TheOrderCounter_NeverGoesBackwards_AndNeverReusesANumber()
        {
            // below 1: repaired to 1
            SaveData save = SaveMigration.NewGame();
            save.NextSpecialOrderNumber = -4;
            SaveMigration.Migrate(save);
            Assert.AreEqual(1, save.NextSpecialOrderNumber);

            // behind a number already on a row: moved past it
            save = SaveMigration.NewGame();
            save.NextSpecialOrderNumber = 5;
            save.SpecialOrders.Add(Paid("order.shop.a.9"));
            save.SpecialOrders.Add(Paid("order.shop.b.3"));
            SaveMigration.Migrate(save);
            Assert.AreEqual(10, save.NextSpecialOrderNumber);
            SaveMigration.Migrate(save);
            Assert.AreEqual(10, save.NextSpecialOrderNumber, "a second repair changes nothing");

            // already ahead: left exactly where it is
            save = SaveMigration.NewGame();
            save.NextSpecialOrderNumber = 20;
            save.SpecialOrders.Add(Paid("order.shop.a.9"));
            SaveMigration.Migrate(save);
            Assert.AreEqual(20, save.NextSpecialOrderNumber);

            // a row the repair drops still used its number
            save = SaveMigration.NewGame();
            save.SpecialOrders.Add(new SpecialOrderDto { OrderId = "order.shop.a.12", PaidPrice = 0 });
            SaveMigration.Migrate(save);
            Assert.AreEqual(0, save.SpecialOrders.Count);
            Assert.AreEqual(13, save.NextSpecialOrderNumber);

            // ids that carry no number change nothing
            save = SaveMigration.NewGame();
            save.NextSpecialOrderNumber = 2;
            save.SpecialOrders.Add(Paid("order.shop.a.abc"));
            save.SpecialOrders.Add(Paid("order.shop.a."));
            save.SpecialOrders.Add(Paid("nodots"));
            save.SpecialOrders.Add(Paid("order.shop.a.-8"));
            save.SpecialOrders.Add(Paid("order.shop.a.99999999999999"));
            SaveMigration.Migrate(save);
            Assert.AreEqual(5, save.SpecialOrders.Count);
            Assert.AreEqual(2, save.NextSpecialOrderNumber);

            // and the counter comes back through the real JSON
            save = SaveMigration.NewGame();
            save.NextSpecialOrderNumber = 41;
            Assert.AreEqual(41, SaveSerialization.FromJson(SaveSerialization.ToJson(save)).NextSpecialOrderNumber);
        }

        static SpecialOrderDto Paid(string orderId) => new SpecialOrderDto
        {
            OrderId = orderId, SellerId = "shop.a", PaidPrice = 10,
            Lines = { new SpecialOrderLineDto { ListingId = "listing.rope", Count = 1 } },
        };

        // ---- 9. a new game ------------------------------------------------------------------------------

        [Test]
        public void ANewGameStartsWithAllThreeEmpty()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsNotNull(save.StorePurchases);
            Assert.IsNotNull(save.SpecialOrders);
            Assert.IsNotNull(save.ShoppingBags);
            Assert.AreEqual(0, save.StorePurchases.Count);
            Assert.AreEqual(0, save.SpecialOrders.Count);
            Assert.AreEqual(0, save.ShoppingBags.Count);
            Assert.AreEqual(1, save.NextSpecialOrderNumber);

            SaveData back = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            Assert.AreEqual(0, back.StorePurchases.Count + back.SpecialOrders.Count + back.ShoppingBags.Count);
            Assert.AreEqual(1, back.NextSpecialOrderNumber);
        }
    }
}
