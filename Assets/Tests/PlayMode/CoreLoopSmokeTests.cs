using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// VS-30 — the core-loop regression net. A deterministic PlayMode smoke test that exercises the
    /// loop's spine end-to-end WITHOUT depending on the Greybox scene's exact layout: a seeded fishing
    /// context resolves a catch (real <see cref="CatchResolver"/>) → it's added to the hold respecting
    /// capacity (Core <see cref="IHold"/> contract) → the real wharf sell path
    /// (<see cref="WharfSellPoint"/> → <see cref="FishBuyer"/> → <see cref="Market"/> →
    /// <see cref="MarketMath"/>) pays the wallet at the right price → money updates. This is the "the
    /// loop still works" guard that runs on every PR.
    ///
    /// Harness style mirrors <c>HudControllerSmokeTests</c>: real production logic for the parts that
    /// have logic (catch resolution + market pricing), with minimal Core-contract doubles for the two
    /// trivial stateful holders (hold/wallet). The hold double deliberately stands in for ShipHold so
    /// this guard stays decoupled from the Boats assembly (which boat-handling work churns) — its
    /// capacity rule is identical to ShipHold.TryAdd. Boat handling itself is out of scope here.
    /// </summary>
    public class CoreLoopSmokeTests
    {
        // ---- Core-contract doubles (faithful to ShipHold / PlayerWallet semantics) -----------

        /// <summary>An IHold with a fixed capacity — same accept/reject rule as ShipHold.TryAdd.</summary>
        private sealed class CapHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            private readonly int _capacity;
            public CapHold(int capacity) { _capacity = capacity; }
            public int CapacityUnits => _capacity;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item)
            {
                if (_items.Count >= _capacity) return false;  // full → refuse (mirrors ShipHold)
                _items.Add(item);
                return true;
            }
            public void Clear() => _items.Clear();
        }

        /// <summary>An IWallet that just accumulates — same money rule as PlayerWallet.Add.</summary>
        private sealed class TestWallet : IWallet
        {
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount) { if (amount > Money) return false; Money -= amount; return true; }
        }

        // The deterministic situation every test fish is authored to bite in.
        private const string Region = "region.coddle_cove";
        private const Gear TheGear = Gear.Handline;
        // (regionId, tideHeight, hourOfDay, season, gear) — mid-tide, midday, in season, with the gear.
        private static CatchContext OpenContext()
            => new CatchContext(Region, 0f, 12f, Season.HighSummer, TheGear);

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp() => EventBus.Clear<CatchSold>();

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<CatchSold>();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // ---- builders -----------------------------------------------------------------------

        /// <summary>A species authored to bite in <see cref="OpenContext"/> (all gates open).</summary>
        private FishSpeciesDef MakeFish(string id, string name, FishCategory cat, int baseValue,
                                        float minKg, float maxKg)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id;
            f.DisplayName = name;
            f.Category = cat;
            f.RegionIds = new[] { Region };
            f.AllowedGear = TheGear;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f;
            f.StartHour = 0f; f.EndHour = 24f;           // all day
            f.MinWeightKg = minKg; f.MaxWeightKg = maxKg;
            f.BaseValue = baseValue;
            f.SupplyElasticity = 0.3f;
            f.SpawnWeight = 1f;
            _spawned.Add(f);
            return f;
        }

        /// <summary>The real wharf sell rig (Market → FishBuyer → WharfSellPoint), wired like the
        /// greybox but in a bare GameObject (no scene). Selling goes through the production path.</summary>
        private WharfSellPoint MakeWharf()
        {
            var go = new GameObject("Wharf");
            _spawned.Add(go);
            var market = go.AddComponent<Market>();
            var buyer = go.AddComponent<FishBuyer>();
            var wharf = go.AddComponent<WharfSellPoint>();
            SetPrivate(buyer, "_market", market);
            SetPrivate(wharf, "_buyer", buyer);
            return wharf;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"field '{field}' not found on {target.GetType().Name}");
            f.SetValue(target, value);
        }

        /// <summary>Payout a FRESH market owes for these items, mirroring the now-MARGINAL
        /// FishBuyer.SellAll (routed through SellService): each unit prices at its category's
        /// supply-so-far — base 0 at a fresh market, but a 2nd+ fish of the SAME category self-glutts.
        /// Computed from the items actually landed so it's robust to which fish the RNG picked.</summary>
        private static int ExpectedFreshPayout(IReadOnlyList<CatchItem> items)
        {
            int total = 0;
            var soldPerCategory = new Dictionary<FishCategory, int>();
            for (int i = 0; i < items.Count; i++)
            {
                CatchItem it = items[i];
                int already = soldPerCategory.TryGetValue(it.Category, out int s) ? s : 0;
                total += SellPricing.MarginalPrice(it.BaseValue, it.SupplyElasticity, 0f, already);
                soldPerCategory[it.Category] = already + 1;
            }
            return total;
        }

        // ---- the chain ----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator CoreLoop_ResolveFillsHoldToCapacity_ThenSellPaysWalletExactly()
        {
            // A pool where every fish bites in the open context, with distinct base values.
            var pool = new List<FishSpeciesDef>
            {
                MakeFish("fish.test_cod",      "Test Cod",      FishCategory.InshoreGroundfish, 12, 2f, 6f),
                MakeFish("fish.test_mackerel", "Test Mackerel", FishCategory.Pelagic,            8, 1f, 3f),
                MakeFish("fish.test_lobster",  "Test Lobster",  FishCategory.Shellfish,         30, 0.5f, 1.5f),
            };
            var ctx = OpenContext();
            var rng = new System.Random(20300);   // seeded → deterministic & stable
            const int capacity = 4;
            var hold = new CapHold(capacity);

            // Cast more times than the hold can take: each cast resolves a biting fish; the hold takes
            // exactly `capacity` and refuses the rest (capacity respected).
            const int extraCasts = 3;
            int rejected = 0;
            for (int cast = 0; cast < capacity + extraCasts; cast++)
            {
                FishSpeciesDef fish = CatchResolver.Resolve(pool, in ctx, rng);
                Assert.IsNotNull(fish, "a cast in a fully-open context must resolve a biting fish");
                Assert.IsTrue(CatchResolver.Matches(fish, in ctx), "resolved fish must actually match the context");

                float kg = CatchResolver.RollWeight(fish, rng);
                var item = new CatchItem(fish.Id, fish.DisplayName, fish.Category, kg,
                                         fish.BaseValue, fish.SupplyElasticity);
                if (!hold.TryAdd(item)) rejected++;
            }

            Assert.AreEqual(capacity, hold.UsedUnits, "hold fills to capacity and no further");
            Assert.AreEqual(extraCasts, rejected, "every cast past capacity is refused by the hold");

            // Capture the right price BEFORE selling (the sale clears the hold).
            int expected = ExpectedFreshPayout(hold.Items);
            Assert.Greater(expected, 0, "a full hold of valued fish must be worth something");

            // Sell through the REAL wharf path at a fresh market (supply 0 → price multiplier 1.0).
            var wharf = MakeWharf();
            var wallet = new TestWallet();

            int seenTotal = -1, seenCount = -1, raised = 0;
            void OnSold(CatchSold e) { seenTotal = e.TotalPaid; seenCount = e.Count; raised++; }
            EventBus.Subscribe<CatchSold>(OnSold);

            int paid = wharf.Sell(hold, wallet);
            EventBus.Unsubscribe<CatchSold>(OnSold);

            Assert.AreEqual(expected, paid, "fresh-market payout = the marginal self-glutting total (per category)");
            Assert.AreEqual(paid, wallet.Money, "money updates by exactly the payout");
            Assert.AreEqual(0, hold.UsedUnits, "selling empties the hold");
            Assert.AreEqual(1, raised, "exactly one CatchSold is raised");
            Assert.AreEqual(capacity, seenCount, "CatchSold.Count equals the items sold");
            Assert.AreEqual(paid, seenTotal, "CatchSold.TotalPaid equals the payout");

            yield return null;
        }

        // ---- determinism guard --------------------------------------------------------------

        [Test]
        public void Resolve_IsDeterministic_ForAGivenSeed()
        {
            var pool = new List<FishSpeciesDef>
            {
                MakeFish("fish.a", "A", FishCategory.InshoreGroundfish, 12, 1f, 9f),
                MakeFish("fish.b", "B", FishCategory.Pelagic,            9, 1f, 9f),
                MakeFish("fish.c", "C", FishCategory.Shellfish,         20, 1f, 9f),
            };
            var ctx = OpenContext();

            string RunOnce(int seed)
            {
                var rng = new System.Random(seed);
                var sb = new StringBuilder();
                for (int i = 0; i < 12; i++)
                {
                    FishSpeciesDef f = CatchResolver.Resolve(pool, in ctx, rng);
                    float kg = CatchResolver.RollWeight(f, rng);
                    sb.Append(f.Id).Append('@').Append(kg.ToString("F4")).Append(';');
                }
                return sb.ToString();
            }

            // Same seed must reproduce the exact fish + weight sequence (no hidden global randomness).
            Assert.AreEqual(RunOnce(777), RunOnce(777), "a seeded resolve is reproducible");
        }

        // ---- 'nothing bites' branch ---------------------------------------------------------

        [Test]
        public void Resolve_ReturnsNull_WhenNothingBites()
        {
            // A fish that only bites in a different region → nothing matches → no catch.
            var elsewhere = MakeFish("fish.elsewhere", "Elsewhere", FishCategory.Pelagic, 5, 1f, 2f);
            elsewhere.RegionIds = new[] { "region.somewhere_else" };

            var pool = new List<FishSpeciesDef> { elsewhere };
            var ctx = OpenContext();
            var rng = new System.Random(1);

            Assert.IsNull(CatchResolver.Resolve(pool, in ctx, rng),
                "no biting fish in the context → null (the 'nothing bites' branch)");
        }
    }
}
