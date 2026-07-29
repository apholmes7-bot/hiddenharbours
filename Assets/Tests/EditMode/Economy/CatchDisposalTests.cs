using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>
    /// M1 §7.3 — the DISPOSE verb. A spoiled catch is refused by every buyer yet still occupies hold
    /// space, so without dumping a fully-spoiled hold is a soft-lock. These pin the verb's safety
    /// contract: it removes ONLY rubbish (never sellable catch), it announces itself on
    /// <see cref="CatchDumped"/> (never <see cref="CatchSold"/> — nothing was paid), and a hold of
    /// pure rubbish empties completely (the escape hatch the refusal gate requires).
    /// </summary>
    public class CatchDisposalTests
    {
        private const float SecondsPerDay = 1200f;

        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 999;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private int _dumpedEvents;
        private int _lastDumpCount;

        [SetUp]
        public void SetUp()
        {
            _dumpedEvents = 0;
            _lastDumpCount = -1;
            EventBus.Clear<CatchDumped>();
            EventBus.Subscribe<CatchDumped>(OnDumped);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<CatchDumped>(OnDumped);
            EventBus.Clear<CatchDumped>();
        }

        private void OnDumped(CatchDumped e) { _dumpedEvents++; _lastDumpCount = e.Count; }

        private static CatchItem CodLandedAt(double t)
            => new CatchItem("fish.cod", "Cod", FishCategory.InshoreGroundfish, 4f, 20, 0.3f,
                             1f, Freshness.Landed(t));

        private static SpoilContext At(double now)
            => new SpoilContext(now, SecondsPerDay, SpoilPolicy.Default);

        [Test]
        public void DumpSpoiled_RemovesOnlyTheRubbish()
        {
            var hold = new FakeHold();
            hold.TryAdd(CodLandedAt(0d));                    // rubbish at read time
            hold.TryAdd(CodLandedAt(SecondsPerDay));         // fresh at read time
            var now = At(SecondsPerDay);

            int dumped = CatchDisposal.DumpSpoiled(hold, now);

            Assert.AreEqual(1, dumped);
            Assert.AreEqual(1, hold.UsedUnits, "the fresh one is untouched — dumping is never a clear-all");
            Assert.IsTrue(now.IsSellable(hold.Items[0]), "what remains is the sellable one");
            Assert.AreEqual(1, _dumpedEvents, "one CatchDumped covers the dump");
            Assert.AreEqual(1, _lastDumpCount);
        }

        [Test]
        public void DumpSpoiled_FullySpoiledHold_EmptiesIt()
        {
            // The soft-lock case the verb exists for: nothing sells, everything must go.
            var hold = new FakeHold();
            hold.TryAdd(CodLandedAt(0d));
            hold.TryAdd(CodLandedAt(0d));
            hold.TryAdd(CodLandedAt(0d));

            int dumped = CatchDisposal.DumpSpoiled(hold, At(SecondsPerDay * 2));

            Assert.AreEqual(3, dumped);
            Assert.AreEqual(0, hold.UsedUnits, "the hold is usable again");
        }

        [Test]
        public void DumpSpoiled_NothingSpoiled_IsANoOp()
        {
            var hold = new FakeHold();
            hold.TryAdd(CodLandedAt(0d));

            int dumped = CatchDisposal.DumpSpoiled(hold, At(0d));   // just landed — perfectly fresh

            Assert.AreEqual(0, dumped);
            Assert.AreEqual(1, hold.UsedUnits, "fresh catch never goes over the side");
            Assert.AreEqual(0, _dumpedEvents, "no dump → no event");
        }

        [Test]
        public void DumpSpoiled_IsIdempotent()
        {
            var hold = new FakeHold();
            hold.TryAdd(CodLandedAt(0d));
            hold.TryAdd(CodLandedAt(SecondsPerDay));
            var now = At(SecondsPerDay);

            Assert.AreEqual(1, CatchDisposal.DumpSpoiled(hold, now));
            Assert.AreEqual(0, CatchDisposal.DumpSpoiled(hold, now),
                            "a second press finds nothing — safe for the proxy double-visit too");
            Assert.AreEqual(1, hold.UsedUnits);
        }

        [Test]
        public void ConfigDefaults_MirrorTheCorePolicy()
        {
            // The shipped GameConfig.asset predates the freshness block, so FreshnessSettings.Default
            // IS the live game — it must equal SpoilPolicy.Default or config and maths drift silently.
            SpoilPolicy fromConfig = FreshnessSettings.Default.Policy;
            SpoilPolicy core = SpoilPolicy.Default;

            Assert.AreEqual(core.AmbientRateMultiplier, fromConfig.AmbientRateMultiplier);
            Assert.AreEqual(core.LiveRateMultiplier, fromConfig.LiveRateMultiplier);
            Assert.AreEqual(core.FrozenRateMultiplier, fromConfig.FrozenRateMultiplier);
            Assert.AreEqual(core.IcedRateMultiplier, fromConfig.IcedRateMultiplier);
            Assert.AreEqual(core.UnsellableSpoil, fromConfig.UnsellableSpoil);
        }
    }
}
