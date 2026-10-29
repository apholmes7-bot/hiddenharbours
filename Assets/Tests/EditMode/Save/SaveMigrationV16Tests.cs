using System;
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public class SaveMigrationV16Tests
    {
        [Test]
        public void CurrentVersion_IncludesTheTwoLonglineLists()
            => Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 16);

        [Test]
        public void AV15Save_GainsTwoEmptyLists_AndKeepsExistingState()
        {
            const string json = "{\"SchemaVersion\":15,\"Money\":417,\"WorldSeed\":4242,\"GameTimeSeconds\":1234.125," +
                "\"PotStock\":[{\"TrapDefId\":\"trap.fixture\",\"Count\":3}]}";
            var data = SaveSerialization.FromJson(json);
            Assert.AreEqual(SaveMigration.CurrentVersion, data.SchemaVersion);
            Assert.IsNotNull(data.PlacedLonglines);
            Assert.IsNotNull(data.TubStock);
            Assert.IsEmpty(data.PlacedLonglines);
            Assert.IsEmpty(data.TubStock);
            Assert.AreEqual(417, data.Money);
            Assert.AreEqual(4242, data.WorldSeed);
            Assert.AreEqual(1234.125d, data.GameTimeSeconds);
            Assert.AreEqual(3, data.PotStock[0].Count);
        }

        [Test]
        public void AV16File_RoundTripsEveryPlacementFieldAndTubStock()
        {
            string dir = Path.Combine(Path.GetTempPath(), "hh_longline_v16_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "fixture.json");
            try
            {
                var data = SaveMigration.NewGame();
                data.PlacedLonglines.Add(Line());
                data.TubStock.Add(new TubStock("longline.fixture", 5));
                SaveStore.Write(data, path);
                var restored = SaveStore.Read(path);
                Assert.IsNotNull(restored);
                Assert.AreEqual(SaveMigration.CurrentVersion, restored.SchemaVersion);
                Assert.AreEqual(1, restored.PlacedLonglines.Count);
                var line = restored.PlacedLonglines[0];
                Assert.AreEqual("line.fixture", line.InstanceId);
                Assert.AreEqual("longline.fixture", line.LonglineDefId);
                Assert.AreEqual("region.fixture", line.Region);
                Assert.AreEqual(-1.25f, line.EndAX);
                Assert.AreEqual(2.5f, line.EndAY);
                Assert.AreEqual(98.75f, line.EndBX);
                Assert.AreEqual(2.5f, line.EndBY);
                Assert.AreEqual(1234.125d, line.SetGameTimeSeconds);
                Assert.AreEqual("bait.fixture", line.BaitId);
                Assert.AreEqual(2, line.Tubs);
                Assert.AreEqual(3, line.HooksHauledFromA);
                Assert.AreEqual(4, line.HooksHauledFromB);
                Assert.AreEqual(1, restored.TubStock.Count);
                Assert.AreEqual("longline.fixture", restored.TubStock[0].LonglineDefId);
                Assert.AreEqual(5, restored.TubStock[0].Count);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void NullLists_AreRepairedAtCurrentVersion()
        {
            var data = SaveMigration.Migrate(new SaveData
                { SchemaVersion = SaveMigration.CurrentVersion, PlacedLonglines = null, TubStock = null });
            Assert.IsNotNull(data.PlacedLonglines);
            Assert.IsNotNull(data.TubStock);
            Assert.IsEmpty(data.PlacedLonglines);
            Assert.IsEmpty(data.TubStock);
        }

        [Test]
        public void MigrationIsIdempotent_AndDoesNotRewriteAnExistingLine()
        {
            var data = SaveMigration.Migrate(new SaveData { SchemaVersion = 15 });
            data.PlacedLonglines.Add(Line());
            data.TubStock.Add(new TubStock("longline.fixture", 5));
            string before = SaveSerialization.ToJson(data);
            Assert.AreEqual(before, SaveSerialization.ToJson(SaveMigration.Migrate(data)));
        }

        [Test]
        public void TubAvailability_IsOwnedLessSet_UntilTheLineIsRemoved()
        {
            var data = SaveMigration.NewGame();
            Assert.AreEqual(5, TubLocker.AddOwned(data, "longline.fixture", 5));
            data.PlacedLonglines.Add(Line());
            Assert.AreEqual(5, TubLocker.OwnedCount(data, "longline.fixture"));
            Assert.AreEqual(2, TubLocker.DeployedCount(data, "longline.fixture"));
            Assert.AreEqual(3, TubLocker.AvailableCount(data, "longline.fixture"));
            data.PlacedLonglines.Clear();
            Assert.AreEqual(5, TubLocker.AvailableCount(data, "longline.fixture"));
            Assert.AreEqual(0, TubLocker.AddOwned(data, "longline.fixture", -1));
        }

        [Test]
        public void ANewGame_StartsWithBothListsEmpty()
        {
            var data = SaveMigration.NewGame();
            Assert.IsEmpty(data.PlacedLonglines);
            Assert.IsEmpty(data.TubStock);
        }

        private static PlacedLonglineDto Line() => new PlacedLonglineDto
        {
            InstanceId = "line.fixture", LonglineDefId = "longline.fixture", Region = "region.fixture",
            EndAX = -1.25f, EndAY = 2.5f, EndBX = 98.75f, EndBY = 2.5f,
            SetGameTimeSeconds = 1234.125d, BaitId = "bait.fixture", Tubs = 2,
            HooksHauledFromA = 3, HooksHauledFromB = 4,
        };
    }
}
