using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The Loran's remembered numbers, saved per hull in the v24 <see cref="SaveData.HullLoranWaypoints"/>
    /// rows: a store opened on a save and a hull loads that hull's rows with their slot numbers, every change
    /// writes them back, and a reload through the real JSON shows the same numbers. Two hulls never share a
    /// list, a damaged row is dropped on load, and an older save starts with none.
    /// </summary>
    public class LoranWaypointPersistTests
    {
        const string Cape = "boat.cape_islander";
        const string Dory = "boat.dory";
        const string Chain = "loran_chain.northeast";
        const int Capacity = 10;

        static SaveData Reload(SaveData save)
            => SaveSerialization.FromJson(SaveSerialization.ToJson(save));

        static void AssertRow(LoranWaypoint w, int number, int secA, int secB, double tdA, double tdB)
        {
            Assert.AreEqual(number, w.Number, "slot number");
            Assert.AreEqual(Chain, w.ChainId);
            Assert.AreEqual(secA, w.SecondaryA);
            Assert.AreEqual(secB, w.SecondaryB);
            Assert.AreEqual(tdA, w.TdA);
            Assert.AreEqual(tdB, w.TdB);
        }

        [Test]
        public void AHullsNumbers_RoundTripThroughTheSave_WithTheirSlotNumbers()
        {
            SaveData save = SaveMigration.NewGame();
            var store = new LoranWaypointStore(Capacity, save, Cape);
            Assert.IsTrue(store.IsBound);
            Assert.IsTrue(store.TryEnter(Chain, 0, 1, 13456.75d, 44123.125d, out int first));
            Assert.IsTrue(store.TryEnter(Chain, 1, 3, 13500.5d, 44200.25d, out int second));
            Assert.IsTrue(store.TryEnter(Chain, 0, 2, 13600.0d, 44300.0d, out int third));
            Assert.AreEqual(1, first);
            Assert.AreEqual(2, second);
            Assert.AreEqual(3, third);
            Assert.IsTrue(store.Remove(1), "forgetting slot 1 leaves a gap the next number fills");
            Assert.AreEqual(2, save.HullLoranWaypoints.Count, "every change is written straight back to the save");

            SaveData back = Reload(save);
            var again = new LoranWaypointStore(Capacity, back, Cape);

            Assert.AreEqual(2, again.Count);
            AssertRow(again.At(0), 2, 1, 3, 13500.5d, 44200.25d);
            AssertRow(again.At(1), 3, 0, 2, 13600.0d, 44300.0d);
            Assert.IsTrue(again.TryEnter(Chain, 1, 2, 13700.0d, 44400.0d, out int refill));
            Assert.AreEqual(1, refill, "the reloaded set still takes the lowest free number");
            Assert.AreEqual(3, back.HullLoranWaypoints.Count);
        }

        [Test]
        public void TwoHulls_KeepSeparateLists()
        {
            SaveData save = SaveMigration.NewGame();
            var cape = new LoranWaypointStore(Capacity, save, Cape);
            var dory = new LoranWaypointStore(Capacity, save, Dory);
            Assert.IsTrue(cape.TryEnter(Chain, 0, 1, 11000.0d, 26000.0d, out _));
            Assert.IsTrue(cape.TryEnter(Chain, 0, 1, 11100.0d, 26100.0d, out _));
            Assert.IsTrue(dory.TryEnter(Chain, 1, 2, 12000.0d, 27000.0d, out int doryFirst));
            Assert.AreEqual(1, doryFirst, "each hull numbers its own slots");
            Assert.IsTrue(dory.Remove(1));
            Assert.IsTrue(dory.TryEnter(Chain, 1, 2, 12500.0d, 27500.0d, out _));

            SaveData back = Reload(save);
            var capeBack = new LoranWaypointStore(Capacity, back, Cape);
            var doryBack = new LoranWaypointStore(Capacity, back, Dory);

            Assert.AreEqual(2, capeBack.Count, "the dory's changes never touched the Cape Islander's rows");
            AssertRow(capeBack.At(0), 1, 0, 1, 11000.0d, 26000.0d);
            AssertRow(capeBack.At(1), 2, 0, 1, 11100.0d, 26100.0d);
            Assert.AreEqual(1, doryBack.Count);
            AssertRow(doryBack.At(0), 1, 1, 2, 12500.0d, 27500.0d);
            Assert.AreEqual(0, new LoranWaypointStore(Capacity, back, "boat.lobster_boat").Count,
                "a hull with no rows opens an empty set");
        }

        [Test]
        public void AnInvalidRow_IsDroppedOnLoad_AndTheRestKept()
        {
            const string json = "{\"SchemaVersion\":24,\"HullLoranWaypoints\":["
                + "{\"HullId\":\"boat.cape_islander\",\"Number\":1,\"ChainId\":\"loran_chain.northeast\",\"SecondaryA\":2,\"SecondaryB\":2,\"TdA\":11000.0,\"TdB\":26000.0},"
                + "{\"HullId\":\"boat.cape_islander\",\"Number\":2,\"ChainId\":\"loran_chain.northeast\",\"SecondaryA\":0,\"SecondaryB\":1,\"TdA\":11500.0,\"TdB\":26500.0},"
                + "{\"HullId\":\"\",\"Number\":3,\"ChainId\":\"loran_chain.northeast\",\"SecondaryA\":0,\"SecondaryB\":1,\"TdA\":1.0,\"TdB\":2.0}"
                + "]}";

            SaveData save = SaveSerialization.FromJson(json);
            Assert.AreEqual(1, save.HullLoranWaypoints.Count, "the every-load repair drops both readings on one secondary, and a row with no hull");

            var store = new LoranWaypointStore(Capacity, save, Cape);
            Assert.AreEqual(1, store.Count);
            AssertRow(store.At(0), 2, 0, 1, 11500.0d, 26500.0d);
            Assert.IsTrue(store.TryEnter(Chain, 0, 2, 11600.0d, 26600.0d, out int n));
            Assert.AreEqual(1, n, "the dropped row's slot is free again");
        }

        [Test]
        public void ALoad_SkipsUnusableAndRepeatedRows_EvenWithoutTheMigration()
        {
            SaveData save = SaveMigration.NewGame();
            save.HullLoranWaypoints = new List<LoranWaypointDto>
            {
                null,
                new LoranWaypointDto { HullId = Cape, Number = 4, ChainId = Chain, SecondaryA = 1, SecondaryB = 1, TdA = 1d, TdB = 2d },
                new LoranWaypointDto { HullId = Cape, Number = 3, ChainId = Chain, SecondaryA = 0, SecondaryB = 1, TdA = 3d, TdB = 4d },
                new LoranWaypointDto { HullId = Cape, Number = 3, ChainId = Chain, SecondaryA = 0, SecondaryB = 2, TdA = 5d, TdB = 6d },
                new LoranWaypointDto { HullId = Cape, Number = 1, ChainId = Chain, SecondaryA = 1, SecondaryB = 2, TdA = 7d, TdB = 8d },
            };

            var store = new LoranWaypointStore(Capacity, save, Cape);

            Assert.AreEqual(2, store.Count);
            AssertRow(store.At(0), 1, 1, 2, 7d, 8d);
            AssertRow(store.At(1), 3, 0, 1, 3d, 4d);
        }

        [Test]
        public void AV23Save_LoadsWithNoNumbers()
        {
            const string json = "{\"SchemaVersion\":23,\"GameTimeSeconds\":4321.0,\"HullLoranWaypoints\":["
                + "{\"HullId\":\"boat.cape_islander\",\"Number\":1,\"ChainId\":\"loran_chain.northeast\",\"SecondaryA\":0,\"SecondaryB\":1,\"TdA\":11000.0,\"TdB\":26000.0}"
                + "]}";

            SaveData save = SaveSerialization.FromJson(json);

            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.AreEqual(0, new LoranWaypointStore(Capacity, save, Cape).Count,
                "a v23 game never saved any numbers, so whatever the field holds is not trusted");
            Assert.AreEqual(0, new LoranWaypointStore(Capacity, SaveSerialization.FromJson("{\"SchemaVersion\":23}"), Cape).Count);
        }

        [Test]
        public void ALoadPastCapacity_KeepsEverySavedRow_AndRefusesMore()
        {
            SaveData save = SaveMigration.NewGame();
            var big = new LoranWaypointStore(5, save, Cape);
            for (int i = 0; i < 5; i++) Assert.IsTrue(big.TryEnter(Chain, 0, 1, 11000.0d + i, 26000.0d, out _));

            var small = new LoranWaypointStore(3, save, Cape);

            Assert.AreEqual(5, small.Count, "a smaller memory never drops a saved row quietly");
            Assert.IsTrue(small.IsFull);
            Assert.IsFalse(small.TryEnter(Chain, 0, 1, 12000.0d, 26000.0d, out _));
            Assert.AreEqual(5, save.HullLoranWaypoints.Count, "a refused entry writes nothing");
        }

        [Test]
        public void AnUnboundStore_KeepsItsNumbersInMemoryOnly()
        {
            SaveData save = SaveMigration.NewGame();
            var noHull = new LoranWaypointStore(Capacity, save, "");
            var noSave = new LoranWaypointStore(Capacity, null, Cape);
            var plain = new LoranWaypointStore(Capacity);

            Assert.IsFalse(noHull.IsBound);
            Assert.IsFalse(noSave.IsBound);
            Assert.IsFalse(plain.IsBound);
            Assert.IsTrue(noHull.TryEnter(Chain, 0, 1, 11000.0d, 26000.0d, out _));
            Assert.IsTrue(noSave.TryEnter(Chain, 0, 1, 11000.0d, 26000.0d, out _));
            Assert.IsTrue(plain.TryEnter(Chain, 0, 1, 11000.0d, 26000.0d, out _));
            Assert.AreEqual(0, save.HullLoranWaypoints.Count);
        }

        [Test]
        public void Rebinding_SwapsToTheOtherHullsRows()
        {
            SaveData save = SaveMigration.NewGame();
            new LoranWaypointStore(Capacity, save, Dory).TryEnter(Chain, 1, 2, 12000.0d, 27000.0d, out _);
            var store = new LoranWaypointStore(Capacity, save, Cape);
            Assert.IsTrue(store.TryEnter(Chain, 0, 1, 11000.0d, 26000.0d, out _));

            store.BindTo(save, Dory);

            Assert.AreEqual(Dory, store.HullId);
            Assert.AreEqual(1, store.Count);
            AssertRow(store.At(0), 1, 1, 2, 12000.0d, 27000.0d);
            Assert.AreEqual(2, save.HullLoranWaypoints.Count, "rebinding writes nothing");
        }
    }
}
