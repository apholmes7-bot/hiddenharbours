using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode.Regression
{
    /// <summary>
    /// qa-test regression net (expand net 2) over the VS-18 wharf sell screen — the CONTRACT invariants
    /// the screen leans on, beyond what SellTests already pins (marginal fall / Quote==Paid for one
    /// SellSpecies / SellAll clears). Here: that the displayed Quote is read-only (a screen redraw never
    /// sells), that over-asking clamps to what you hold, that each per-species sale raises a matching
    /// <c>CatchSold</c>, and — the headline — that across the screen's quote→confirm→quote loop every
    /// DISPLAYED total equals the COIN received while the price glutts down to a ₲1 floor. Pure Economy
    /// contracts + in-file fakes; no scene, no RNG.
    /// </summary>
    public class SellScreenEconomicsTests
    {
        // ---- in-file fakes for the Core contracts (mirror SellTests/WharfSellTests) -----------
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 999;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FakeWallet : IWallet
        {
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount) { if (amount > Money) return false; Money -= amount; return true; }
        }

        private readonly List<GameObject> _spawned = new();

        [SetUp]
        public void SetUp() => EventBus.Clear<CatchSold>();

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<CatchSold>();
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private Market MakeMarket()
        {
            var go = new GameObject("Market");
            _spawned.Add(go);
            return go.AddComponent<Market>(); // Update() never runs in EditMode → supply is stable
        }

        private static void Add(FakeHold hold, string id, string name, FishCategory cat, int n,
                                int baseValue, float elasticity)
        {
            for (int i = 0; i < n; i++)
                hold.TryAdd(new CatchItem(id, name, cat, 4f, baseValue, elasticity));
        }

        // ---- the screen's Quote must be a pure read (a redraw never sells) -------------------

        [Test]
        public void Quote_IsReadOnly_AndRepeatable_DoesNotMutateHoldWalletOrMarket()
        {
            var market = MakeMarket();
            var hold = new FakeHold();
            Add(hold, "fish.cod", "Cod", FishCategory.InshoreGroundfish, 5, 20, 0.4f);
            var wallet = new FakeWallet();

            int unitsBefore = hold.UsedUnits;
            int moneyBefore = wallet.Money;
            float supplyBefore = market.SupplyOf(FishCategory.InshoreGroundfish);

            int q1 = SellService.Quote(hold, market, "fish.cod", 3);
            int q2 = SellService.Quote(hold, market, "fish.cod", 3);

            Assert.Greater(q1, 0);
            Assert.AreEqual(q1, q2, "quoting is deterministic and side-effect free (the slider can redraw freely)");
            Assert.AreEqual(unitsBefore, hold.UsedUnits, "Quote must not remove catch");
            Assert.AreEqual(moneyBefore, wallet.Money, "Quote must not pay the wallet");
            Assert.AreEqual(supplyBefore, market.SupplyOf(FishCategory.InshoreGroundfish),
                "Quote must not glut the market");
        }

        // ---- asking for more than you hold clamps to what you hold ---------------------------

        [Test]
        public void OverAsk_ClampsToAvailable_ForQuoteAndForSell()
        {
            var market = MakeMarket();
            var hold = new FakeHold();
            Add(hold, "fish.cod", "Cod", FishCategory.InshoreGroundfish, 3, 18, 0.4f);
            var wallet = new FakeWallet();

            int quoteAll       = SellService.Quote(hold, market, "fish.cod", 3);
            int quoteOverAsk   = SellService.Quote(hold, market, "fish.cod", 999);
            Assert.AreEqual(quoteAll, quoteOverAsk, "quoting more than you hold quotes only what you hold");

            int paid = SellService.SellSpecies(hold, wallet, market, "fish.cod", 999);
            Assert.AreEqual(quoteAll, paid, "selling more than you hold sells only what you hold, at that quote");
            Assert.AreEqual(0, SellService.CountOf(hold, "fish.cod"), "the whole species is gone");
            Assert.AreEqual(paid, wallet.Money);
        }

        // ---- each per-species confirm raises one matching CatchSold -------------------------

        [Test]
        public void SellSpecies_RaisesOneCatchSold_WithMatchingTotalAndCount()
        {
            var market = MakeMarket();
            var hold = new FakeHold();
            Add(hold, "fish.cod", "Cod", FishCategory.InshoreGroundfish, 5, 20, 0.4f);
            var wallet = new FakeWallet();

            int seenTotal = -1, seenCount = -1, raised = 0;
            void OnSold(CatchSold e) { seenTotal = e.TotalPaid; seenCount = e.Count; raised++; }
            EventBus.Subscribe<CatchSold>(OnSold);
            int paid = SellService.SellSpecies(hold, wallet, market, "fish.cod", 3);
            EventBus.Unsubscribe<CatchSold>(OnSold);

            Assert.AreEqual(1, raised, "one CatchSold per confirmed sale (HUD payout flash reads it)");
            Assert.AreEqual(paid, seenTotal, "the signal carries the coin paid");
            Assert.AreEqual(3, seenCount, "and the unit count");
        }

        // ---- the screen loop: every displayed total is the coin received, prices glutt down --

        [Test]
        public void ScreenLoop_EachDisplayedQuoteEqualsCoinReceived_AndPriceGluttsDown()
        {
            // Mirrors the screen's real flow: the player confirms one unit at a time; the displayed
            // "Total:" for that step (Quote) must be exactly what lands in the wallet (SellSpecies),
            // and each successive unit must pay no more than the last as supply floods.
            var market = MakeMarket();
            var hold = new FakeHold();
            Add(hold, "fish.cod", "Cod", FishCategory.InshoreGroundfish, 6, 20, 0.5f);
            var wallet = new FakeWallet();

            int runningWallet = 0;
            int prevUnit = int.MaxValue;
            int sales = 0;

            while (SellService.CountOf(hold, "fish.cod") > 0)
            {
                int displayed = SellService.Quote(hold, market, "fish.cod", 1); // the screen's "Total:" for 1
                int paid = SellService.SellSpecies(hold, wallet, market, "fish.cod", 1);

                Assert.AreEqual(displayed, paid, "the displayed total must equal the coin received, every step");
                Assert.LessOrEqual(paid, prevUnit, "each unit floods supply → never pays MORE than the last");
                Assert.GreaterOrEqual(paid, 1, "a unit always fetches at least ₲1");

                runningWallet += paid;
                prevUnit = paid;
                sales++;
            }

            Assert.AreEqual(6, sales, "all six units sold one by one");
            Assert.AreEqual(runningWallet, wallet.Money, "wallet holds exactly the sum of the displayed totals");
            Assert.AreEqual(0, hold.UsedUnits, "the hold is empty at the end");
            Assert.Less(prevUnit, SellPricing.UnitPrice(20, 0.5f, 0f),
                "the last unit sold for less than the opening price (the self-glutt actually bit)");
        }
    }
}
