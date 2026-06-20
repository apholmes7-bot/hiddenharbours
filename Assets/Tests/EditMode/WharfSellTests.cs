using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-22 — the wharf sell interaction. Covers that selling pays the wallet, clears the hold,
    /// raises <see cref="CatchSold"/>, prices the batch at the pre-glut price (so future prices
    /// drop), and is null/empty-safe. Selling is deterministic given (hold contents, market supply):
    /// no RNG is introduced.
    /// </summary>
    public class WharfSellTests
    {
        // ---- in-file fakes for the Core contracts -------------------------------------------
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
            public bool TrySpend(int amount)
            {
                if (amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        private readonly List<GameObject> _spawned = new();

        // Tracks the last UnityEvent<int> _onSold payload (wired via reflection in MakeWharf).
        private int _onSoldPayload;
        private int _onSoldCalls;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<CatchSold>();
            _onSoldPayload = 0;
            _onSoldCalls = 0;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<CatchSold>();
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        // ---- helpers ------------------------------------------------------------------------
        private static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"field '{field}' not found on {target.GetType().Name}");
            f.SetValue(target, value);
        }

        private FishBuyer MakeBuyer(out Market market)
        {
            var go = new GameObject("Wharf");
            _spawned.Add(go);
            market = go.AddComponent<Market>();
            var buyer = go.AddComponent<FishBuyer>();
            SetPrivate(buyer, "_market", market);
            return buyer;
        }

        private WharfSellPoint MakeWharf(FishBuyer buyer, bool listenOnSold = false)
        {
            var go = new GameObject("SellPoint");
            _spawned.Add(go);
            var wharf = go.AddComponent<WharfSellPoint>();
            SetPrivate(wharf, "_buyer", buyer);

            if (listenOnSold)
            {
                var ev = new UnityEngine.Events.UnityEvent<int>();
                ev.AddListener(v => { _onSoldPayload = v; _onSoldCalls++; });
                SetPrivate(wharf, "_onSold", ev);
            }
            return wharf;
        }

        private static FakeHold HoldOf(FishCategory cat, int count, int baseValue = 14, float elasticity = 0.3f)
        {
            var hold = new FakeHold();
            for (int i = 0; i < count; i++)
                hold.TryAdd(new CatchItem($"fish.test_{i}", "Test Fish", cat, 5f, baseValue, elasticity));
            return hold;
        }

        // ---- tests --------------------------------------------------------------------------

        [Test]
        public void Sell_PaysWallet_AndClearsHold()
        {
            var buyer = MakeBuyer(out _);
            var wharf = MakeWharf(buyer);
            var hold = HoldOf(FishCategory.InshoreGroundfish, 4);
            var wallet = new FakeWallet();

            int paid = wharf.Sell(hold, wallet);

            Assert.Greater(paid, 0, "a non-empty hold should pay out");
            Assert.AreEqual(paid, wallet.Money, "wallet should gain exactly the returned total");
            Assert.AreEqual(0, hold.UsedUnits, "hold should be cleared after selling");
        }

        [Test]
        public void Sell_RaisesCatchSold_WithCountAndTotal()
        {
            var buyer = MakeBuyer(out _);
            var wharf = MakeWharf(buyer);
            const int n = 5;
            var hold = HoldOf(FishCategory.InshoreGroundfish, n);
            var wallet = new FakeWallet();

            int seenTotal = -1, seenCount = -1, raised = 0;
            void OnSold(CatchSold e) { seenTotal = e.TotalPaid; seenCount = e.Count; raised++; }
            EventBus.Subscribe<CatchSold>(OnSold);

            int paid = wharf.Sell(hold, wallet);
            EventBus.Unsubscribe<CatchSold>(OnSold);

            Assert.AreEqual(1, raised, "exactly one CatchSold should be raised");
            Assert.AreEqual(n, seenCount, "CatchSold.Count should equal items sold");
            Assert.AreEqual(paid, seenTotal, "CatchSold.TotalPaid should equal the returned total");
        }

        [Test]
        public void Sell_PricesPreGlut_ThenFuturePricesDrop()
        {
            var buyer = MakeBuyer(out Market market);
            var wharf = MakeWharf(buyer);
            var wallet = new FakeWallet();

            // First lot of a category — priced at supply 0 (pre-glut).
            var lot1 = HoldOf(FishCategory.Pelagic, 3);
            int total1 = wharf.Sell(lot1, wallet);

            // Identical refill — market supply persists (we never call Market.Update), so this
            // lot is priced into the glut the first sale created → it earns less.
            var lot2 = HoldOf(FishCategory.Pelagic, 3);
            int total2 = wharf.Sell(lot2, wallet);

            Assert.Greater(total1, 0);
            Assert.Less(total2, total1, "future prices should drop after the first lot floods supply");
            Assert.AreEqual(total1 + total2, wallet.Money, "wallet should hold the sum of both sales");
        }

        [Test]
        public void Sell_EmptyHold_PaysNothing_AndRaisesNoEvent()
        {
            var buyer = MakeBuyer(out _);
            var wharf = MakeWharf(buyer, listenOnSold: true);
            var hold = new FakeHold();           // empty
            var wallet = new FakeWallet();

            int raised = 0;
            void OnSold(CatchSold _) => raised++;
            EventBus.Subscribe<CatchSold>(OnSold);

            int paid = wharf.Sell(hold, wallet);
            EventBus.Unsubscribe<CatchSold>(OnSold);

            Assert.AreEqual(0, paid);
            Assert.AreEqual(0, wallet.Money, "wallet must be unchanged on an empty sell");
            Assert.AreEqual(0, raised, "no CatchSold on an empty hold");
            Assert.AreEqual(0, _onSoldCalls, "_onSold must not fire on an empty hold");
        }

        [Test]
        public void Sell_NullArguments_ChangeNothing()
        {
            var buyer = MakeBuyer(out _);
            var wharf = MakeWharf(buyer);
            var wallet = new FakeWallet();
            var hold = HoldOf(FishCategory.InshoreGroundfish, 2);

            int raised = 0;
            void OnSold(CatchSold _) => raised++;
            EventBus.Subscribe<CatchSold>(OnSold);

            Assert.AreEqual(0, wharf.Sell(null, wallet), "null hold sells nothing");
            Assert.AreEqual(0, wharf.Sell(hold, null), "null wallet sells nothing");

            EventBus.Unsubscribe<CatchSold>(OnSold);

            Assert.AreEqual(0, wallet.Money, "wallet unchanged");
            Assert.AreEqual(2, hold.UsedUnits, "hold unchanged when wallet is null");
            Assert.AreEqual(0, raised, "no event on null inputs");
        }

        [Test]
        public void Sell_SurfacesPayout_ViaLastPayoutAndOnSold()
        {
            var buyer = MakeBuyer(out _);
            var wharf = MakeWharf(buyer, listenOnSold: true);
            var hold = HoldOf(FishCategory.InshoreGroundfish, 3);
            var wallet = new FakeWallet();

            int paid = wharf.Sell(hold, wallet);

            Assert.Greater(paid, 0);
            Assert.AreEqual(paid, wharf.LastPayout, "LastPayout should mirror the returned total");
            Assert.AreEqual(1, _onSoldCalls, "_onSold should fire once on a successful sale");
            Assert.AreEqual(paid, _onSoldPayload, "_onSold should carry the payout");
        }
    }
}
