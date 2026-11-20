using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// v22 -> v23: THE FOOD STORES (kitchens slice 1, owner ask 2026-11-15), on top of the body's v21 and
    /// cleanliness's v22. Pins the three obligations of a schema bump: an older save survives and starts
    /// with every store empty (no food invented, no ice packed), a missing or damaged list is repaired, and a
    /// store with food and ice in it comes back from the real JSON exactly, so its freshness reads the same
    /// after a reload as before it (rule 5).
    /// </summary>
    public class SaveMigrationV23Tests
    {
        /// <summary>A store aboard ONE owned boat (BoatStoreKeyTests pins the shape).</summary>
        static readonly string Key = BoatInteriorKitchen.StoreKey(OwnedBoatId.FirstOf("boat.cape_islander"),
                                                                  "interior.cape_islander_iso", "cooler");

        [Test]
        public void TheSchemaIsAtLeast23()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 23);
        }

        [Test]
        public void AV22Save_StartsWithEveryStoreEmpty()
        {
            SaveData save = SaveSerialization.FromJson("{\"SchemaVersion\":22,\"GameTimeSeconds\":4321.0}");
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsNotNull(save.KitchenStores);
            Assert.AreEqual(0, save.KitchenStores.Count);
            Assert.AreEqual(4321d, save.GameTimeSeconds, "nothing else moved");
        }

        [Test]
        public void AV20Save_WalksTheWholeChain_AndStartsWithEveryStoreEmpty()
        {
            SaveData save = SaveSerialization.FromJson("{\"SchemaVersion\":20,\"GameTimeSeconds\":4321.0}");
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsNotNull(save.KitchenStores);
            Assert.AreEqual(0, save.KitchenStores.Count);
            Assert.IsNotNull(save.PlayerBody, "the body's v21 step still ran");
            Assert.IsNotNull(save.PlayerCleanliness, "cleanliness's v22 step still ran");
        }

        [Test]
        public void AV22ObjectWithANullList_IsRepaired()
        {
            SaveData save = SaveMigration.Migrate(new SaveData { SchemaVersion = 22, KitchenStores = null });
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsNotNull(save.KitchenStores);
        }

        [Test]
        public void DamagedRowsAreHealed_AndGoodOnesKept()
        {
            SaveData save = SaveMigration.NewGame();
            save.KitchenStores = new List<KitchenStorageStateDto>
            {
                null,
                new KitchenStorageStateDto { Key = "", StorageDefId = "kitchen_storage.cooler" },
                new KitchenStorageStateDto
                {
                    Key = Key, StorageDefId = null, Stacks = new List<KitchenStackDto>
                    {
                        new KitchenStackDto("food.a", 0, Freshness.Landed(0d)),
                        new KitchenStackDto("", 2, Freshness.Landed(0d)),
                        new KitchenStackDto("food.b", 3, Freshness.Landed(0d)),
                    },
                },
                new KitchenStorageStateDto { Key = "fixture.boat.x.pantry", Stacks = null },
            };

            SaveMigration.Migrate(save);
            Assert.AreEqual(2, save.KitchenStores.Count, "a null row and a keyless row are dropped");
            KitchenStorageStateDto kept = save.KitchenStores[0];
            Assert.AreEqual(Key, kept.Key);
            Assert.AreEqual("", kept.StorageDefId);
            Assert.AreEqual(1, kept.Stacks.Count, "an empty lot and an unnamed lot are dropped");
            Assert.AreEqual("food.b", kept.Stacks[0].ItemId);
            Assert.IsNotNull(save.KitchenStores[1].Stacks);
        }

        [Test]
        public void AStoreWithFoodAndIce_SurvivesTheRealJson_AndReadsTheSameAfter()
        {
            SaveData save = SaveMigration.NewGame();
            var store = new KitchenStorageStateDto
            {
                Key = Key,
                StorageDefId = "kitchen_storage.cooler",
                IceRunsOutAtGameSeconds = 98765.25d,
                IceOutAnnounced = false,
                Powered = true,
                Stacks = new List<KitchenStackDto>
                {
                    new KitchenStackDto("food.chowder", 3, new FreshnessState(0.125f, 90000.5d, StorageMode.Iced)),
                },
            };
            save.KitchenStores.Add(store);

            SaveData back = SaveMigration.Migrate(SaveSerialization.FromJson(SaveSerialization.ToJson(save)));
            KitchenStorageStateDto read = KitchenStores.Find(back, Key);
            Assert.IsNotNull(read);
            Assert.AreEqual("kitchen_storage.cooler", read.StorageDefId);
            Assert.AreEqual(98765.25d, read.IceRunsOutAtGameSeconds);
            Assert.IsTrue(read.Powered);
            Assert.AreEqual(1, read.Stacks.Count);
            Assert.AreEqual("food.chowder", read.Stacks[0].ItemId);
            Assert.AreEqual(3, read.Stacks[0].Count);
            Assert.AreEqual(0.125f, read.Stacks[0].SpoilAccrued);
            Assert.AreEqual(90000.5d, read.Stacks[0].LastSettleGameSeconds);
            Assert.AreEqual(StorageMode.Iced, read.Stacks[0].Freshness.Mode);
        }
    }
}
