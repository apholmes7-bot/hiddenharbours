using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// A gift taken from a storage container that offers the surgical remove (<see cref="IHoldRemove"/>: the
    /// boat's hold, the freezer, the clam bucket) takes exactly that one item and touches nothing else: the
    /// hold is never emptied and refilled, so a persisted hold's other items are never re-moded or rewritten.
    /// A hold without it is still rebuilt, as before.
    /// </summary>
    public class GiftSurgicalTakeTests
    {
        private const string CodId = "fish.atlantic_cod";
        private const string HaddockId = "fish.haddock";

        private sealed class SurgicalHold : IHold, IHoldRemove
        {
            public readonly List<CatchItem> List = new List<CatchItem>();
            public int Clears, Adds, Removes;

            public int CapacityUnits => 20;
            public int UsedUnits => List.Count;
            public IReadOnlyList<CatchItem> Items => List;
            public bool TryAdd(CatchItem item) { Adds++; List.Add(item); return true; }
            public void Clear() { Clears++; List.Clear(); }

            public bool TryRemoveAt(int index)
            {
                if (index < 0 || index >= List.Count) return false;
                Removes++;
                List.RemoveAt(index);
                return true;
            }
        }

        private static CatchItem Cod() => new CatchItem(CodId, "Atlantic Cod", FishCategory.InshoreGroundfish, 2f, 10, 0.1f);
        private static CatchItem Haddock() => new CatchItem(HaddockId, "Haddock", FishCategory.InshoreGroundfish, 1.5f, 8, 0.1f);

        [SetUp] public void SetUp() => PlayerStorage.Clear();
        [TearDown] public void TearDown() => PlayerStorage.Clear();

        [Test]
        public void TheOneGiftIsRemoved_AndNothingElseIsTouched()
        {
            var hold = new SurgicalHold();
            hold.List.AddRange(new[] { Haddock(), Cod(), Haddock() });
            PlayerStorage.Register(hold);

            var gifts = new GiftInventory();
            gifts.Gather(null, null, null, null);
            Assert.IsTrue(gifts.TryTakeOne(CodId, out CatchItem taken));

            Assert.AreEqual(CodId, taken.SpeciesId);
            Assert.AreEqual(1, hold.Removes);
            Assert.AreEqual(0, hold.Clears, "never emptied");
            Assert.AreEqual(0, hold.Adds, "never refilled");
            Assert.AreEqual(2, hold.UsedUnits);
            Assert.AreEqual(HaddockId, hold.List[0].SpeciesId);
            Assert.AreEqual(HaddockId, hold.List[1].SpeciesId);
        }

        [Test]
        public void AnOverfullSurgicalHold_CanStillBeGivenFrom()
        {
            // An over-capacity hold (its gear was lost) cannot be rebuilt, but a surgical remove needs no room.
            var hold = new OverfullSurgicalHold();
            hold.List.AddRange(new[] { Cod(), Cod(), Cod() });
            PlayerStorage.Register(hold);

            var gifts = new GiftInventory();
            gifts.Gather(null, null, null, null);
            Assert.AreEqual(3, gifts.CountOf(0));
            Assert.IsTrue(gifts.TryTakeOne(CodId, out _));
            Assert.AreEqual(2, hold.UsedUnits);
        }

        private sealed class OverfullSurgicalHold : IHold, IHoldRemove
        {
            public readonly List<CatchItem> List = new List<CatchItem>();
            public int CapacityUnits => 0;
            public int UsedUnits => List.Count;
            public IReadOnlyList<CatchItem> Items => List;
            public bool TryAdd(CatchItem item) => false;
            public void Clear() => List.Clear();
            public bool TryRemoveAt(int index)
            {
                if (index < 0 || index >= List.Count) return false;
                List.RemoveAt(index);
                return true;
            }
        }
    }
}
