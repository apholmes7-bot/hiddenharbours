using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public class FishingSupplyContractTests
    {
        private static StartingBait.BaitGrant Grant(string id, int count)
            => new StartingBait.BaitGrant { BaitId = id, Count = count };

        [Test]
        public void BaitGrant_MergesRepeatedKinds_InInputOrder_AndCountsAcceptedRows()
        {
            var save = new SaveData
            {
                BaitStock = new List<BaitStock> { new BaitStock("bait.existing", 2) }
            };
            Assert.AreEqual(3, StartingBait.Grant(save, new[]
            {
                Grant("bait.new", 3), Grant("bait.existing", 4), Grant("bait.new", 5)
            }));
            Assert.AreEqual(2, save.BaitStock.Count);
            Assert.AreEqual("bait.existing", save.BaitStock[0].BaitId);
            Assert.AreEqual(6, save.BaitStock[0].Count);
            Assert.AreEqual("bait.new", save.BaitStock[1].BaitId);
            Assert.AreEqual(8, save.BaitStock[1].Count);
        }

        [Test]
        public void BaitGrant_RejectsEmptyIdsAndNonPositiveCounts_WithoutChangingStock()
        {
            var stock = new List<BaitStock> { new BaitStock("bait.kept", 7) };
            var save = new SaveData { BaitStock = stock };
            Assert.AreEqual(0, StartingBait.Grant(save, new[]
            {
                Grant(null, 1), Grant("", 1), Grant("bait.kept", 0), Grant("bait.kept", -2)
            }));
            Assert.AreSame(stock, save.BaitStock);
            Assert.AreEqual(1, stock.Count);
            Assert.AreEqual("bait.kept", stock[0].BaitId);
            Assert.AreEqual(7, stock[0].Count);
        }

        [Test]
        public void BaitGrant_NullInputsAreNoOps_AndMissingStockIsRepaired()
        {
            var save = new SaveData { BaitStock = null };
            Assert.AreEqual(0, StartingBait.Grant(null, new[] { Grant("bait.fixture", 1) }));
            Assert.AreEqual(0, StartingBait.Grant(save, null));
            Assert.IsNull(save.BaitStock);
            Assert.AreEqual(1, StartingBait.Grant(save, new[] { Grant("bait.fixture", 2) }));
            Assert.AreEqual(1, save.BaitStock.Count);
            Assert.AreEqual(2, save.BaitStock[0].Count);
        }

        [Test]
        public void BaitGrant_IsAdditive_AndDoesNotOwnTheOnceOnlyGate()
        {
            var save = new SaveData();
            var grant = new[] { Grant("bait.fixture", 2) };
            Assert.AreEqual(1, StartingBait.Grant(save, grant));
            Assert.AreEqual(1, StartingBait.Grant(save, grant));
            Assert.AreEqual(1, save.BaitStock.Count);
            Assert.AreEqual(4, save.BaitStock[0].Count);
        }

        [Test]
        public void Locker_MissingListsReadEmpty_WithoutRepairingOrWritingTheBlob()
        {
            var save = new SaveData { PotStock = null, PlacedTraps = null };
            Assert.AreEqual(0, PotLocker.OwnedCount(save, "trap.fixture"));
            Assert.AreEqual(0, PotLocker.DeployedCount(save, "trap.fixture"));
            Assert.AreEqual(-2, PotLocker.AvailableCount(save, "trap.fixture", 2));
            Assert.IsNull(save.PotStock);
            Assert.IsNull(save.PlacedTraps);
        }

        [Test]
        public void Locker_QueriesArePerKind_AndDoNotNormalizeCanonicalIds()
        {
            var save = new SaveData
            {
                PotStock = new List<PotStock> { new PotStock("trap.fixture", 3) },
                PlacedTraps = new List<PlacedTrapDto>
                {
                    new PlacedTrapDto { InstanceId = "wet.one", TrapDefId = "trap.fixture" },
                    new PlacedTrapDto { InstanceId = "wet.other", TrapDefId = "trap.other" }
                }
            };
            Assert.AreEqual(1, PotLocker.AvailableCount(save, "trap.fixture", 1));
            Assert.AreEqual(-1, PotLocker.AvailableCount(save, "trap.other", 0));
            Assert.AreEqual(0, PotLocker.AvailableCount(save, "TRAP.FIXTURE", 0));
            Assert.AreEqual(0, PotLocker.DeployedCount(save, null));
            Assert.AreEqual(3, save.PotStock[0].Count);
            Assert.AreEqual(2, save.PlacedTraps.Count);
        }
    }
}
