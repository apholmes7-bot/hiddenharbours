using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

using HiddenHarbours.Tests.EditMode;   // CatalogTestStall — same assembly, other namespace

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>
    /// plan-to-m1 §7.5 — the island general store. Covers the three things that make the store a store:
    /// it BUYS shellfish at deliberately worse prices than Nine Mile Creek, it SELLS counted stock you
    /// come back for (bait, ice) beside the one-off rod, and it VENDS the clam licence — with Ginny's
    /// fronted fee as the one-time mechanism that keeps the licence buyable before there is any clam money.
    ///
    /// <para><b>The load-bearing test here is <see cref="TheFirstUnitSold"/>.</b> Demand D cannot make the
    /// store worse on the first sale — at zero supply the <c>S/D</c> term is 1 for every D — so if the price
    /// LEVEL is ever removed and only demand is left, the whole "where you sell matters" lesson silently
    /// stops being taught in hour one. That test is the guard on it.</para>
    ///
    /// <para>Pure logic over the Core contracts + save DTO — headless, deterministic, no RNG (rule 5).</para>
    /// </summary>
    public class IslandGeneralStoreTests
    {
        private const FishCategory Shellfish = FishCategory.Shellfish;

        // ---- fakes for the Core contracts ---------------------------------------------------------

        private sealed class FakeWallet : IWallet
        {
            public FakeWallet(int starting) { Money = starting; }
            public int Money { get; private set; }
            public int Credits { get; private set; }
            public bool RefuseSpend;
            public System.Action OnSpend;
            public void Add(int amount) { Money += amount; Credits += amount; }
            public bool TrySpend(int amount)
            {
                if (RefuseSpend || amount < 0 || amount > Money) return false;
                Money -= amount;
                OnSpend?.Invoke();
                return true;
            }
        }

        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 999;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FlagSaveService : ISaveService
        {
            public FlagSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public readonly List<string> Writes = new();
            private int _depth;
            private bool _pending;
            public bool GetFlag(string key)
            {
                foreach (var flag in Current.OnboardingFlags)
                    if (flag.Key == key) return flag.Value;
                return false;
            }
            public void SetFlag(string key, bool value)
            {
                if (string.IsNullOrEmpty(key)) return;
                for (int i = 0; i < Current.OnboardingFlags.Count; i++)
                    if (Current.OnboardingFlags[i].Key == key)
                    {
                        Current.OnboardingFlags[i] = new SaveFlag(key, value);
                        Save();
                        return;
                    }
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
                Save();
            }
            public void Save()
            {
                if (_depth > 0) { _pending = true; return; }
                if (GameServices.Wallet != null) Current.Money = GameServices.Wallet.Money;
                Writes.Add(SaveSerialization.ToJson(Current));
            }
            public System.IDisposable DeferWrites() { _depth++; return new Scope(this); }
            private sealed class Scope : System.IDisposable
            {
                private readonly FlagSaveService _owner;
                public Scope(FlagSaveService owner) { _owner = owner; }
                public void Dispose()
                {
                    if (--_owner._depth == 0 && _owner._pending)
                    {
                        _owner._pending = false;
                        _owner.Save();
                    }
                }
            }
        }

        private sealed class FakeLicenses : ILicenseService
        {
            private readonly HashSet<string> _held = new();
            public bool IsLicensed(string id) => string.IsNullOrEmpty(id) || _held.Contains(id);
            public void Grant(string id) { if (!string.IsNullOrEmpty(id)) _held.Add(id); }
            public int Count => _held.Count;
        }

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<CatchSold>();
            EventBus.Clear<BaitPurchased>();
            EventBus.Clear<SupplyPurchased>();
            EventBus.Clear<FeeFronted>();
            EventBus.Clear<LicensePurchased>();
            GameServices.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<CatchSold>();
            EventBus.Clear<BaitPurchased>();
            EventBus.Clear<SupplyPurchased>();
            EventBus.Clear<FeeFronted>();
            EventBus.Clear<LicensePurchased>();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- builders -----------------------------------------------------------------------------

        /// <summary>The shipped SHAPE of the M1 outlet ladder: the store pays worst, the cove is the
        /// neutral baseline, Nine Mile Creek pays best. Values are the test's own so the real GameConfig
        /// asset stays the single source of truth for shipped balance (the MarketComparisonTests rule).</summary>
        private GameConfig MakeConfig(float storeDemand = 0.7f, float creekDemand = 1.4f,
                                      float storeLevel = 0.6f, float creekLevel = 1f)
        {
            var c = ScriptableObject.CreateInstance<GameConfig>();
            c.MarketDemandCove = 1f;
            c.MarketDemandNineMileCreek = creekDemand;
            c.MarketDemandStPetersStore = storeDemand;
            c.MarketPriceLevelCove = 1f;
            c.MarketPriceLevelNineMileCreek = creekLevel;
            c.MarketPriceLevelStPetersStore = storeLevel;
            c.MarketDailyRecovery = 0.5f;
            _spawned.Add(c);
            return c;
        }

        private Market MakeMarket(GameConfig config, MarketId id)
        {
            var go = new GameObject(id + "Market");
            _spawned.Add(go);
            var m = go.AddComponent<Market>();
            m.Configure(config, id);
            return m;
        }

        private FishBuyer MakeBuyer(Market market)
        {
            var buyer = market.gameObject.AddComponent<FishBuyer>();
            typeof(FishBuyer).GetField("_market", System.Reflection.BindingFlags.NonPublic |
                                                  System.Reflection.BindingFlags.Instance)
                             .SetValue(buyer, market);
            return buyer;
        }

        private static FakeHold ClamsOf(int n, int baseValue, float elasticity)
        {
            var hold = new FakeHold();
            for (int i = 0; i < n; i++)
                hold.TryAdd(new CatchItem("fish.soft_shell_clam", "Soft-shell Clam", Shellfish,
                                          0.1f, baseValue, elasticity));
            return hold;
        }

        // =====================================================================================
        //  1. BUY SHELLFISH — at deliberately worse prices than Nine Mile Creek
        // =====================================================================================

        /// <summary>
        /// ⭐ THE GUARD ON THE WHOLE FINDING. On a market nobody has sold into yet, demand is mathematically
        /// incapable of changing the price: <c>priceMult = 1/(1 + e·S/D)</c> is exactly 1 at S = 0 for every
        /// D. So the first clam a player ever sells would fetch identical coin at the village counter and on
        /// the Creek's wharf if demand were the only lever — and "where you sell matters" would not be
        /// taught in hour one at all. The per-outlet PRICE LEVEL is what makes it true.
        /// </summary>
        [Test]
        public void TheFirstUnitSold()
        {
            var config = MakeConfig();
            var store = MakeMarket(config, MarketId.StPetersStore);
            var creek = MakeMarket(config, MarketId.NineMileCreek);

            // Both markets untouched — supply 0 at each, so the supply curve contributes exactly 1.
            Assert.AreEqual(0f, store.SupplyOf(Shellfish), 1e-6f);
            Assert.AreEqual(0f, creek.SupplyOf(Shellfish), 1e-6f);

            // Demand alone: identical, and that is the point being pinned.
            Assert.AreEqual(MarketMath.PriceMultiplier(0f, 0.3f, store.DemandFor(Shellfish)),
                            MarketMath.PriceMultiplier(0f, 0.3f, creek.DemandFor(Shellfish)), 1e-6f,
                "at zero supply the S/D term is 1 for EVERY demand — demand cannot move the first sale");

            // The level: 20₲ of clam pays 12 at the counter and 20 on the wharf. Exact — both products
            // land on whole numbers, so no rounding ambiguity.
            Assert.AreEqual(12, store.NextUnitPrice(Shellfish, baseValue: 20, elasticity: 0.3f),
                "the store pays its 0.6 level on the very first unit");
            Assert.AreEqual(20, creek.NextUnitPrice(Shellfish, baseValue: 20, elasticity: 0.3f),
                "the Creek pays the book price on the very first unit");
        }

        [Test]
        public void AWholeBucket_PaysStrictlyLessAtTheStore_AndTheTillMatchesTheQuote()
        {
            const int n = 4, baseValue = 20;
            const float e = 0.3f;
            var config = MakeConfig();
            var store = MakeMarket(config, MarketId.StPetersStore);
            var creek = MakeMarket(config, MarketId.NineMileCreek);

            int paidStore = MakeBuyer(store).SellAll(ClamsOf(n, baseValue, e), new FakeWallet(0));
            int paidCreek = MakeBuyer(creek).SellAll(ClamsOf(n, baseValue, e), new FakeWallet(0));

            // Exact payouts, cross-checked against the independent RunningTotal path — so the sell screen's
            // running total and the coin the till pays cannot drift apart (the §7.5 contract).
            Assert.AreEqual(SellPricing.RunningTotal(baseValue, e, 0f, n, store.DemandFor(Shellfish),
                                                     store.PriceLevel), paidStore,
                "the store's payout is exactly the sum of its per-unit marginals");
            Assert.AreEqual(SellPricing.RunningTotal(baseValue, e, 0f, n, creek.DemandFor(Shellfish),
                                                     creek.PriceLevel), paidCreek,
                "and so is the Creek's");

            Assert.Less(paidStore, paidCreek,
                "the same bucket must fetch strictly less at the island counter — the economic reason to " +
                "walk the tide-gated sandbar (§7.5)");
        }

        [Test]
        public void EqualisingTheLevel_MakesTheGapVanishOnTheFirstUnit_IsolatingLevelAsTheLever()
        {
            // The mirror of the demand-isolation test next door: with the LEVEL equal, the first unit
            // quotes the same at both, proving the gap above is the level and not market identity.
            var config = MakeConfig(storeLevel: 1f, creekLevel: 1f);
            var store = MakeMarket(config, MarketId.StPetersStore);
            var creek = MakeMarket(config, MarketId.NineMileCreek);

            Assert.AreEqual(creek.NextUnitPrice(Shellfish, 20, 0.3f),
                            store.NextUnitPrice(Shellfish, 20, 0.3f),
                "equal levels → the same first-unit quote; the shipped gap is the level, not the id");
        }

        [Test]
        public void TheStoreStillGluts_AndStillRecovers_LikeAnyOtherChannel()
        {
            var config = MakeConfig();
            var store = MakeMarket(config, MarketId.StPetersStore);

            int fresh = store.NextUnitPrice(Shellfish, 20, 0.3f);
            store.RegisterSale(Shellfish, 10);
            int glutted = store.NextUnitPrice(Shellfish, 20, 0.3f);
            Assert.Less(glutted, fresh, "dumping a lot at the counter depresses its price too");

            for (int day = 0; day < 10; day++) store.SettleDaily();
            Assert.AreEqual(fresh, store.NextUnitPrice(Shellfish, 20, 0.3f),
                "and the island's own supply recovers over days, back to its (lower) opening price");
        }

        [Test]
        public void ExistingChannels_AreBitIdentical_AtTheNeutralLevel()
        {
            // The additive-change guarantee: a market with no config, and the cove/Creek at level 1, price
            // exactly as they did before the level existed.
            var config = MakeConfig();
            var cove = MakeMarket(config, MarketId.Cove);
            Assert.AreEqual(1f, cove.PriceLevel, 1e-6f, "the cove is the neutral baseline");

            for (float s = 0f; s <= 12f; s += 3f)
                Assert.AreEqual(MarketMath.MarginalPrice(30, s, 0.4f, cove.DemandFor(Shellfish)),
                                SellPricing.UnitPrice(30, 0.4f, s, cove.DemandFor(Shellfish), 1f, 1f),
                    $"level 1 must reproduce the pre-level price exactly (supply {s})");

            var unconfigured = MakeMarket(null, MarketId.StPetersStore);
            Assert.AreEqual(1f, unconfigured.PriceLevel, 1e-6f,
                "no GameConfig → a neutral level, never a silent 0.6 on an unwired market");
        }

        // =====================================================================================
        //  2. SELL BAIT — counted, repeatable stock (the §10.2 bait economy's missing half)
        // =====================================================================================

        private BaitDef MakeBait(string id, int unitPrice, int lotSize)
        {
            var b = ScriptableObject.CreateInstance<BaitDef>();
            b.Id = id; b.DisplayName = "Capelin"; b.Price = unitPrice; b.LotSize = lotSize;
            _spawned.Add(b);
            return b;
        }

        private BaitShop MakeBaitShop()
        {
            var go = new GameObject("BaitShop");
            _spawned.Add(go);
            return go.AddComponent<BaitShop>();
        }

        [Test]
        public void BuyingBait_ChargesTheLotPrice_AndFillsTheTackleBox()
        {
            var save = new SaveData();
            var wallet = new FakeWallet(100);
            var bait = MakeBait("bait.capelin", unitPrice: 2, lotSize: 10);

            var got = new List<BaitPurchased>();
            void OnBought(BaitPurchased e) => got.Add(e);
            EventBus.Subscribe<BaitPurchased>(OnBought);
            try
            {
                Assert.IsTrue(MakeBaitShop().TryBuy(bait, wallet, save));

                Assert.AreEqual(80, wallet.Money, "a lot of 10 at 2₲ each costs 20₲");
                Assert.AreEqual(10, TackleBox.BaitCount(save, "bait.capelin"),
                    "and the whole lot lands in the box the rod and the pots both spend from");
                Assert.AreEqual(1, got.Count);
                Assert.AreEqual(20, got[0].PricePaid);
                Assert.AreEqual(10, got[0].CountBought);
                Assert.AreEqual(10, got[0].OwnedCount);
            }
            finally { EventBus.Unsubscribe<BaitPurchased>(OnBought); }
        }

        [Test]
        public void BaitIsAlwaysReBuyable_AndNeverChargesWhatItCannotDeliver()
        {
            var save = new SaveData();
            var wallet = new FakeWallet(45);
            var bait = MakeBait("bait.capelin", unitPrice: 2, lotSize: 10);
            var shop = MakeBaitShop();

            Assert.IsTrue(shop.TryBuy(bait, wallet, save));
            Assert.IsTrue(shop.TryBuy(bait, wallet, save), "bait is consumable — there is no 'already owned'");
            Assert.AreEqual(20, TackleBox.BaitCount(save, "bait.capelin"));
            Assert.AreEqual(5, wallet.Money);

            Assert.IsFalse(shop.TryBuy(bait, wallet, save), "5₲ cannot buy a 20₲ lot");
            Assert.AreEqual(5, wallet.Money, "and a refused purchase charges nothing (TrySpend is atomic)");
            Assert.AreEqual(20, TackleBox.BaitCount(save, "bait.capelin"));

            Assert.IsFalse(shop.TryBuy(bait, new FakeWallet(999), null),
                "no save to record the bait in → refuse rather than eat the coin");
        }

        [Test]
        public void ALotSizeThatPredatesTheField_SellsSinglyRatherThanNothing()
        {
            // A BaitDef authored before LotSize existed deserialises with whatever the field initialiser
            // gives it; a 0 must never mean "pay full price for no bait".
            var legacy = MakeBait("bait.legacy", unitPrice: 3, lotSize: 0);
            Assert.AreEqual(1, BaitShop.LotSizeOf(legacy), "a 0 lot size floors at one bait");
            Assert.AreEqual(3, BaitShop.LotPriceOf(legacy));
        }

        // =====================================================================================
        //  3. SELL ICE — the first consumable SUPPLY (§7.3's cold chain, economic half)
        // =====================================================================================

        private SupplyDef MakeSupply(string id, int price)
        {
            var s = ScriptableObject.CreateInstance<SupplyDef>();
            s.Id = id; s.DisplayName = "Bag of Ice"; s.Price = price;
            _spawned.Add(s);
            return s;
        }

        private SupplyShop MakeSupplyShop()
        {
            var go = new GameObject("SupplyShop");
            _spawned.Add(go);
            return go.AddComponent<SupplyShop>();
        }

        [Test]
        public void BuyingIce_IsCountedStock_AndRepeatable()
        {
            var save = new SaveData();
            var wallet = new FakeWallet(20);
            var ice = MakeSupply("supply.ice", 6);
            var shop = MakeSupplyShop();

            var got = new List<SupplyPurchased>();
            void OnBought(SupplyPurchased e) => got.Add(e);
            EventBus.Subscribe<SupplyPurchased>(OnBought);
            try
            {
                Assert.IsTrue(shop.TryBuy(ice, wallet, save));
                Assert.IsTrue(shop.TryBuy(ice, wallet, save), "ice is consumable — you always come back for more");

                Assert.AreEqual(8, wallet.Money);
                Assert.AreEqual(2, SupplyLocker.Count(save, "supply.ice"));
                Assert.AreEqual(2, got.Count);
                Assert.AreEqual(2, got[1].OwnedCount);

                Assert.IsFalse(shop.TryBuy(ice, new FakeWallet(2), save), "2₲ cannot buy a 6₲ bag");
                Assert.AreEqual(2, SupplyLocker.Count(save, "supply.ice"), "a refused buy stocks nothing");
            }
            finally { EventBus.Unsubscribe<SupplyPurchased>(OnBought); }
        }

        [Test]
        public void TheSupplyLocker_SpendsOneAtATime_AndEmptiesCleanly()
        {
            var save = new SaveData();
            Assert.AreEqual(0, SupplyLocker.Count(save, "supply.ice"));
            Assert.IsFalse(SupplyLocker.TrySpend(save, "supply.ice"), "nothing to spend on an empty locker");

            SupplyLocker.Add(save, "supply.ice", 2);
            Assert.IsTrue(SupplyLocker.Has(save, "supply.ice"));
            Assert.IsTrue(SupplyLocker.TrySpend(save, "supply.ice"));
            Assert.IsTrue(SupplyLocker.TrySpend(save, "supply.ice"));
            Assert.IsFalse(SupplyLocker.TrySpend(save, "supply.ice"), "and then it is empty");
            Assert.AreEqual(0, SupplyLocker.Count(save, "supply.ice"));
            Assert.AreEqual(0, save.SupplyStock.Count, "an emptied entry is removed, not carried as a zero");

            Assert.AreEqual(0, SupplyLocker.Count(null, "supply.ice"), "null-safe (the greybox posture)");
            Assert.IsFalse(SupplyLocker.TrySpend(null, "supply.ice"));
        }

        [Test]
        public void SupplyStock_SurvivesTheV7Migration_AndAnOlderSaveGainsAnEmptyLocker()
        {
            var old = new SaveData { SchemaVersion = 6, Money = 40 };
            old.SupplyStock = null;                         // a pre-v7 blob simply had no such field

            SaveData up = SaveMigration.Migrate(old);

            Assert.AreEqual(SaveMigration.CurrentVersion, up.SchemaVersion, "a v6 save climbs to current");
            Assert.GreaterOrEqual(up.SchemaVersion, 7, "and v7's field exists from that climb");
            Assert.IsNotNull(up.SupplyStock, "an older save gains an empty locker, never a null");
            Assert.AreEqual(0, up.SupplyStock.Count);
            Assert.AreEqual(40, up.Money, "and nothing else is reinterpreted");
        }

        // =====================================================================================
        //  4. THE CLAM LICENCE + GINNY'S FRONTED FEE
        // =====================================================================================

        private LicenseDef MakeLicense(string id, int price, params string[] permitted)
        {
            var l = ScriptableObject.CreateInstance<LicenseDef>();
            l.Id = id; l.DisplayName = "Clam Digging Licence"; l.Price = price;
            l.PermittedSpeciesIds = permitted;
            _spawned.Add(l);
            return l;
        }

        [Test]
        public void TheClamLicence_VendsThroughTheExistingVendor_AndGatesOnlyTheClam()
        {
            var clam = MakeLicense("license.clam", 15, "fish.soft_shell_clam");
            var cod = MakeLicense("license.cod", 120, "fish.atlantic_cod");
            var all = new List<LicenseDef> { clam, cod };
            var licenses = new FakeLicenses();
            var wallet = new FakeWallet(20);

            var go = new GameObject("StoreCounter");
            _spawned.Add(go);
            var vendor = go.AddComponent<LicenseVendor>();

            Assert.IsFalse(CatchLicensePolicy.MayLand("fish.soft_shell_clam", all, licenses),
                "before the licence the clam cannot be landed (the gate fails closed)");

            Assert.IsTrue(vendor.TryBuy(clam, wallet, licenses), "the player buys it themselves — no new code");
            Assert.AreEqual(5, wallet.Money);
            Assert.IsTrue(licenses.IsLicensed("license.clam"));

            Assert.IsTrue(CatchLicensePolicy.MayLand("fish.soft_shell_clam", all, licenses),
                "and now the flats are legally theirs to dig");
            Assert.IsFalse(CatchLicensePolicy.MayLand("fish.atlantic_cod", all, licenses),
                "the cod licence is still the one you earn later at Nine Mile Creek — untouched");

            Assert.IsFalse(vendor.TryBuy(clam, wallet, licenses), "buying it twice is a no-op");
            Assert.AreEqual(5, wallet.Money, "and charges nothing");
        }

        private FrontedFeeGrant MakeGrant(GameConfig config, string key)
        {
            var go = new GameObject("FrontedFeeGrant");
            _spawned.Add(go);
            var g = go.AddComponent<FrontedFeeGrant>();
            g.Configure(config, key);
            return g;
        }

        [Test]
        public void GinnysFee_IsFrontedExactlyOnce_AndUnblocksTheLicence()
        {
            var config = MakeConfig();
            config.FrontedLicenceFee = 20;
            var save = new SaveData();
            var saver = new FlagSaveService(save);
            var wallet = new FakeWallet(0);
            GameServices.Save = saver;
            GameServices.Wallet = wallet;

            var got = new List<FeeFronted>();
            void OnFronted(FeeFronted e) => got.Add(e);
            EventBus.Subscribe<FeeFronted>(OnFronted);
            try
            {
                var grant = MakeGrant(config, FrontedFeeGrant.DefaultGrantKey);

                Assert.AreEqual(20, grant.GrantOnce(), "the fee is fronted");
                Assert.AreEqual(20, wallet.Money);
                Assert.IsTrue(saver.GetFlag(FrontedFeeGrant.DefaultGrantKey), "and recorded, so it persists");

                // Idempotent under any number of calls from any source — Ginny's dialogue AND the Start()
                // safety net can both fire without the player being paid twice.
                Assert.AreEqual(0, grant.GrantOnce());
                Assert.AreEqual(0, MakeGrant(config, FrontedFeeGrant.DefaultGrantKey).GrantOnce(),
                    "a second grant component on a reloaded scene fronts nothing either");
                Assert.AreEqual(20, wallet.Money, "the wallet is credited exactly once");
                Assert.AreEqual(1, got.Count, "and exactly one FeeFronted signal goes out");

                // The point of the whole beat: a player who started with nothing can now buy the licence.
                var clam = MakeLicense("license.clam", 15, "fish.soft_shell_clam");
                var vendorGo = new GameObject("StoreCounter");
                _spawned.Add(vendorGo);
                Assert.IsTrue(vendorGo.AddComponent<LicenseVendor>().TryBuy(clam, wallet, new FakeLicenses()),
                    "the fronted fee covers the clam licence — the §7.5 deadlock cannot happen");
            }
            finally { EventBus.Unsubscribe<FeeFronted>(OnFronted); }
        }

        [Test]
        public void AnUnwiredOrZeroFee_LeavesTheGrantArmed_RatherThanBurningTheFlag()
        {
            var save = new SaveData();
            var saver = new FlagSaveService(save);
            GameServices.Save = saver;
            GameServices.Wallet = new FakeWallet(0);

            // No config at all: nothing to front, and the flag must stay UNSET so a later wiring fix still
            // delivers it (the StartingPots rule — a misconfigured scene must not permanently swallow it).
            var unwired = MakeGrant(null, FrontedFeeGrant.DefaultGrantKey);
            Assert.AreEqual(0, unwired.GrantOnce());
            Assert.IsFalse(saver.GetFlag(FrontedFeeGrant.DefaultGrantKey), "the grant stays armed");

            var config = MakeConfig();
            config.FrontedLicenceFee = 0;
            Assert.AreEqual(0, MakeGrant(config, FrontedFeeGrant.DefaultGrantKey).GrantOnce(),
                "a zero fee grants nothing");
            Assert.IsFalse(saver.GetFlag(FrontedFeeGrant.DefaultGrantKey));

            config.FrontedLicenceFee = 20;
            Assert.AreEqual(20, MakeGrant(config, FrontedFeeGrant.DefaultGrantKey).GrantOnce(),
                "and once it IS wired, the fee still arrives");
        }

        [Test]
        public void TheGrant_NeedsNoSave_AndNeedsNoWallet_WithoutThrowing()
        {
            GameServices.Save = null;
            GameServices.Wallet = null;
            Assert.AreEqual(0, MakeGrant(MakeConfig(), FrontedFeeGrant.DefaultGrantKey).GrantOnce(),
                "no save (EditMode / pre-boot) → a no-op, never an exception");

            Assert.IsFalse(FrontedFeeGrant.Grant(null, 20), "no wallet → nothing granted");
            Assert.IsFalse(FrontedFeeGrant.Grant(new FakeWallet(0), 0), "and a non-positive fee is not a grant");
        }

        [Test]
        public void ClamFeeRecovery_IsAuthoredForTheExistingGrantAndOnlyItsIslandSeller()
        {
            int configured = 0;
            foreach (var license in CatalogSource.Licenses())
            {
                bool any = !string.IsNullOrWhiteSpace(license.FeeRecoveryGrantFlag)
                           || !string.IsNullOrWhiteSpace(license.FeeRecoveryUsedFlag)
                           || !string.IsNullOrWhiteSpace(license.FeeRecoverySellerId)
                           || !string.IsNullOrWhiteSpace(license.FeeRecoveryNote);
                if (!any) continue;
                configured++;
                Assert.AreEqual("license.clam", license.Id);
                Assert.AreEqual(FrontedFeeGrant.DefaultGrantKey, license.FeeRecoveryGrantFlag);
                Assert.AreEqual("ginny_clam_fee_recovered", license.FeeRecoveryUsedFlag);
                Assert.AreNotEqual(license.FeeRecoveryGrantFlag, license.FeeRecoveryUsedFlag);
                Assert.AreEqual("seller.leblancs", license.FeeRecoverySellerId);
                Assert.IsTrue(license.Catalog.Listed);
                CollectionAssert.Contains(license.Catalog.Sellers, license.FeeRecoverySellerId);
                Assert.Greater(license.Price, 0);
                Assert.IsFalse(string.IsNullOrWhiteSpace(license.FeeRecoveryNote));
            }
            Assert.AreEqual(1, configured, "only the first shellfish licence gets this recovery");
        }

        private const string RecoverySeller = "seller.leblancs";
        private const string RecoveryUsed = "ginny_clam_fee_recovered";

        private LicenseDef MakeRecoverableLicense()
        {
            var license = MakeLicense("license.clam", 15, "fish.soft_shell_clam");
            license.FeeRecoveryGrantFlag = FrontedFeeGrant.DefaultGrantKey;
            license.FeeRecoveryUsedFlag = RecoveryUsed;
            license.FeeRecoverySellerId = RecoverySeller;
            license.FeeRecoveryNote = "Put down what you have, child. I'll settle the rest.";
            return license;
        }

        private LicenseVendor RecoveryVendor(LicenseDef license, IWallet wallet,
                                              string seller = RecoverySeller)
        {
            var go = new GameObject("StoreCounter");
            _spawned.Add(go);
            var vendor = go.AddComponent<LicenseVendor>();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(LicenseVendor).GetField("_sellerId", flags).SetValue(vendor, seller);
            typeof(LicenseVendor).GetField("_wallet", flags).SetValue(vendor, wallet);
            typeof(LicenseVendor).GetField("_license", flags).SetValue(vendor, license);
            return vendor;
        }

        private LicenseService RestoreLicenses()
        {
            var go = new GameObject("LicenseService");
            _spawned.Add(go);
            var licenses = go.AddComponent<LicenseService>();
            licenses.Register();
            return licenses;
        }

        private static BuyRow LicenceRow(LicenseVendor vendor, LicenseDef license, int money,
                                         SaveData save, ILicenseService licenses)
        {
            var rows = new List<BuyRow>();
            var arms = new BuyCatalog.SellerArms(null, null, null, null, null, null, vendor);
            var stock = new BuyCatalog.CatalogStock(null, null, null, null, null, null, new[] { license });
            BuyCatalog.BuildFrom(arms, stock, money, save, licenses, rows);
            Assert.AreEqual(1, rows.Count);
            return rows[0];
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(14)]
        public void SpentFee_RecoversThroughTheBook_WithoutReplacementCoinsOrRepeatProfit(int left)
        {
            var saver = new FlagSaveService(SaveMigration.NewGame());
            var wallet = new FakeWallet(0);
            GameServices.Save = saver;
            GameServices.Wallet = wallet;
            var config = MakeConfig();
            config.FrontedLicenceFee = 20;
            var grant = MakeGrant(config, FrontedFeeGrant.DefaultGrantKey);
            Assert.AreEqual(20, grant.GrantOnce());
            Assert.IsTrue(wallet.TrySpend(20 - left));
            var license = MakeRecoverableLicense();
            var licenses = RestoreLicenses();
            var vendor = RecoveryVendor(license, wallet);
            var purchases = new List<LicensePurchased>();
            EventBus.Subscribe<LicensePurchased>(purchases.Add);

            int writesBefore = saver.Writes.Count;
            string beforeQuote = SaveSerialization.ToJson(saver.Current);
            var row = LicenceRow(vendor, license, wallet.Money, saver.Current, licenses);
            Assert.IsTrue(row.Quote.CanBuy, "the visible book must let the broke player confirm");
            Assert.AreEqual(left, row.Quote.Price);
            Assert.AreEqual(license.FeeRecoveryNote, row.Note);
            Assert.AreEqual("", row.Flavor, "the recovery explanation replaces the ordinary description");
            Assert.AreEqual(beforeQuote, SaveSerialization.ToJson(saver.Current), "browsing is read-only");

            Assert.IsTrue(BuyCatalog.Confirm(row));
            Assert.AreEqual(0, wallet.Money);
            Assert.AreEqual(20, wallet.Credits, "only the original grant credits the purse");
            Assert.IsTrue(licenses.IsLicensed(license.Id));
            Assert.IsTrue(CatchLicensePolicy.MayLand("fish.soft_shell_clam", new[] { license }, licenses));
            Assert.IsTrue(saver.GetFlag(FrontedFeeGrant.DefaultGrantKey));
            Assert.IsTrue(saver.GetFlag(RecoveryUsed));
            Assert.AreEqual(writesBefore + 1, saver.Writes.Count, "fee, licence and used flag persist together");
            Assert.AreEqual(1, purchases.Count);
            Assert.AreEqual(left, purchases[0].PricePaid);
            Assert.AreEqual(license.Id, purchases[0].LicenseId);
            Assert.AreEqual(RecoverySeller, purchases[0].VendorId);

            Assert.IsFalse(BuyCatalog.Confirm(row), "a stale row cannot redeem again");
            Assert.IsFalse(RecoveryVendor(license, wallet).TryBuy());
            Assert.AreEqual(0, grant.GrantOnce());
            Assert.AreEqual(1, purchases.Count);
            var saved = SaveSerialization.FromJson(saver.Writes[saver.Writes.Count - 1]);
            Assert.AreEqual(0, saved.Money);
            CollectionAssert.AreEqual(new[] { license.Id }, saved.OwnedLicenses);

            var resumed = new FlagSaveService(saved);
            var resumedWallet = new FakeWallet(saved.Money);
            GameServices.Save = resumed;
            GameServices.Wallet = resumedWallet;
            var resumedLicenses = RestoreLicenses();
            var resumedVendor = RecoveryVendor(license, resumedWallet);
            Assert.IsTrue(resumed.GetFlag(RecoveryUsed));
            Assert.IsFalse(resumedVendor.TryBuy());
            Assert.AreEqual(0, resumedWallet.Credits);
            Assert.AreEqual(0, resumed.Writes.Count);
            Assert.AreEqual("", LicenceRow(resumedVendor, license, 0, saved, resumedLicenses).Note);
        }

        [TestCase(15)]
        [TestCase(20)]
        [TestCase(100)]
        public void AffordableFirstLicence_KeepsItsPriceAndDoesNotUseRecovery(int money)
        {
            var save = SaveMigration.NewGame();
            save.OnboardingFlags.Add(new SaveFlag(FrontedFeeGrant.DefaultGrantKey, true));
            var saver = new FlagSaveService(save);
            var wallet = new FakeWallet(money);
            GameServices.Save = saver;
            GameServices.Wallet = wallet;
            var licenses = RestoreLicenses();
            var license = MakeRecoverableLicense();
            var vendor = RecoveryVendor(license, wallet);
            var purchases = new List<LicensePurchased>();
            EventBus.Subscribe<LicensePurchased>(purchases.Add);
            var row = LicenceRow(vendor, license, money, save, licenses);
            Assert.AreEqual(15, row.Quote.Price);
            Assert.AreEqual("", row.Note);
            Assert.AreEqual(license.Flavor, row.Flavor);
            Assert.IsTrue(BuyCatalog.Confirm(row));
            Assert.AreEqual(money - 15, wallet.Money);
            Assert.AreEqual(0, wallet.Credits);
            Assert.IsFalse(saver.GetFlag(RecoveryUsed));
            Assert.AreEqual(1, save.OnboardingFlags.Count, "normal buyers gain no recovery flag");
            Assert.AreEqual(1, purchases.Count);
            Assert.AreEqual(license.Price, purchases[0].PricePaid);
        }

        [TestCase(25)]
        [TestCase(26)]
        public void OlderLicensedSave_IsUnchangedByBrowsingBuyingOrTheGrant(int version)
        {
            var old = new SaveData { SchemaVersion = version, Money = 37 };
            old.OwnedLicenses.Add("license.clam");
            old.OnboardingFlags.Add(new SaveFlag(FrontedFeeGrant.DefaultGrantKey, true));
            old.OnboardingFlags.Add(new SaveFlag("met_ginny", true));
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(old));
            string before = SaveSerialization.ToJson(loaded);
            var saver = new FlagSaveService(loaded);
            var wallet = new FakeWallet(loaded.Money);
            GameServices.Save = saver;
            GameServices.Wallet = wallet;
            var licenses = RestoreLicenses();
            var license = MakeRecoverableLicense();
            var vendor = RecoveryVendor(license, wallet);
            var purchases = new List<LicensePurchased>();
            EventBus.Subscribe<LicensePurchased>(purchases.Add);
            var row = LicenceRow(vendor, license, wallet.Money, loaded, licenses);
            Assert.IsTrue(row.Quote.Owned);
            Assert.AreEqual("", row.Note);
            Assert.IsFalse(BuyCatalog.Confirm(row));
            Assert.IsFalse(vendor.TryBuy());
            Assert.AreEqual(0, MakeGrant(MakeConfig(), FrontedFeeGrant.DefaultGrantKey).GrantOnce());
            Assert.AreEqual(37, wallet.Money);
            Assert.AreEqual(0, wallet.Credits);
            Assert.AreEqual(0, saver.Writes.Count);
            Assert.AreEqual(0, purchases.Count);
            Assert.AreEqual(before, SaveSerialization.ToJson(loaded));
        }

        [TestCase("no_grant")]
        [TestCase("false_grant")]
        [TestCase("used")]
        [TestCase("wrong_seller")]
        [TestCase("no_save")]
        [TestCase("no_licences")]
        [TestCase("unconfigured")]
        [TestCase("no_used_flag")]
        [TestCase("same_flags")]
        public void Recovery_RequiresTheRecordedGrantAndItsAuthoredCounter(string missing)
        {
            var save = SaveMigration.NewGame();
            if (missing != "no_grant")
                save.OnboardingFlags.Add(new SaveFlag(FrontedFeeGrant.DefaultGrantKey, missing != "false_grant"));
            if (missing == "used") save.OnboardingFlags.Add(new SaveFlag(RecoveryUsed, true));
            var saver = new FlagSaveService(save);
            var wallet = new FakeWallet(4);
            GameServices.Save = missing == "no_save" ? null : saver;
            GameServices.Wallet = wallet;
            var licenses = missing == "no_licences" ? null : RestoreLicenses();
            var license = MakeRecoverableLicense();
            if (missing == "unconfigured") license.FeeRecoveryGrantFlag = "";
            if (missing == "no_used_flag") license.FeeRecoveryUsedFlag = "";
            if (missing == "same_flags") license.FeeRecoveryUsedFlag = license.FeeRecoveryGrantFlag;
            var vendor = RecoveryVendor(license, wallet, missing == "wrong_seller" ? "seller.other" : RecoverySeller);
            string before = SaveSerialization.ToJson(save);
            var row = LicenceRow(vendor, license, 4, GameServices.Save?.Current, licenses);
            Assert.IsFalse(row.Quote.CanBuy);
            Assert.AreEqual(15, row.Quote.Price);
            Assert.AreEqual("", row.Note);
            Assert.IsFalse(vendor.TryBuy());
            Assert.AreEqual(4, wallet.Money);
            Assert.AreEqual(0, wallet.Credits);
            Assert.AreEqual(0, saver.Writes.Count);
            Assert.AreEqual(before, SaveSerialization.ToJson(save));
        }

        [Test]
        public void RefusedSpend_LeavesRecoveryAvailable_AndReentrantCounterCannotClaimItTwice()
        {
            var save = SaveMigration.NewGame();
            save.OnboardingFlags.Add(new SaveFlag(FrontedFeeGrant.DefaultGrantKey, true));
            var saver = new FlagSaveService(save);
            var wallet = new FakeWallet(4) { RefuseSpend = true };
            GameServices.Save = saver;
            GameServices.Wallet = wallet;
            var licenses = RestoreLicenses();
            var license = MakeRecoverableLicense();
            var vendor = RecoveryVendor(license, wallet);
            Assert.IsFalse(vendor.TryBuy());
            Assert.IsFalse(saver.GetFlag(RecoveryUsed));
            Assert.IsFalse(licenses.IsLicensed(license.Id));
            Assert.AreEqual(4, wallet.Money);

            var otherCounter = RecoveryVendor(license, wallet);
            int nestedCalls = 0;
            wallet.RefuseSpend = false;
            wallet.OnSpend = () => { nestedCalls++; Assert.IsFalse(otherCounter.TryBuy()); };
            var purchases = new List<LicensePurchased>();
            EventBus.Subscribe<LicensePurchased>(purchases.Add);
            Assert.IsTrue(vendor.TryBuy());
            Assert.AreEqual(1, nestedCalls);
            Assert.AreEqual(1, purchases.Count);
            Assert.AreEqual(4, purchases[0].PricePaid);
            Assert.AreEqual(0, wallet.Money);
            Assert.IsTrue(saver.GetFlag(RecoveryUsed));
        }

        [Test]
        public void Recovery_ReplaysIdenticallyFromTheSameSavedSeedTimeAndFlags()
        {
            var start = SaveMigration.NewGame();
            start.WorldSeed = 481;
            start.GameTimeSeconds = 23456;
            start.Money = 7;
            start.OnboardingFlags.Add(new SaveFlag(FrontedFeeGrant.DefaultGrantKey, true));
            string input = SaveSerialization.ToJson(start);
            string first = null;
            for (int run = 0; run < 2; run++)
            {
                var saver = new FlagSaveService(SaveSerialization.FromJson(input));
                var wallet = new FakeWallet(saver.Current.Money);
                GameServices.Save = saver;
                GameServices.Wallet = wallet;
                RestoreLicenses();
                var license = MakeRecoverableLicense();
                var vendor = RecoveryVendor(license, wallet);
                var purchases = new List<LicensePurchased>();
                EventBus.Subscribe<LicensePurchased>(purchases.Add);
                Assert.IsTrue(vendor.TryBuy());
                Assert.AreEqual(1, saver.Writes.Count);
                Assert.AreEqual(1, purchases.Count);
                Assert.AreEqual(7, purchases[0].PricePaid);
                Assert.AreEqual(481, saver.Current.WorldSeed);
                Assert.AreEqual(23456, saver.Current.GameTimeSeconds);
                if (first == null) first = saver.Writes[0];
                else Assert.AreEqual(first, saver.Writes[0]);
                EventBus.Unsubscribe<LicensePurchased>(purchases.Add);
            }
        }

        [TestCase(0)]
        [TestCase(8)]
        [TestCase(20)]
        public void ChangedPurse_RequiresARefreshedContributionBeforeConfirm(int moneyNow)
        {
            var save = SaveMigration.NewGame();
            save.OnboardingFlags.Add(new SaveFlag(FrontedFeeGrant.DefaultGrantKey, true));
            var saver = new FlagSaveService(save);
            var wallet = new FakeWallet(4);
            GameServices.Save = saver;
            GameServices.Wallet = wallet;
            var licenses = RestoreLicenses();
            var license = MakeRecoverableLicense();
            var vendor = RecoveryVendor(license, wallet);
            var stale = LicenceRow(vendor, license, wallet.Money, save, licenses);
            Assert.AreEqual(4, stale.Quote.Price);
            if (moneyNow < wallet.Money) Assert.IsTrue(wallet.TrySpend(wallet.Money - moneyNow));
            else wallet.Add(moneyNow - wallet.Money);

            Assert.IsFalse(BuyCatalog.Confirm(stale), "never silently charge a different contribution");
            Assert.AreEqual(moneyNow, wallet.Money);
            Assert.IsFalse(licenses.IsLicensed(license.Id));
            Assert.AreEqual(0, saver.Writes.Count);

            var fresh = LicenceRow(vendor, license, wallet.Money, save, licenses);
            Assert.AreEqual(System.Math.Min(moneyNow, license.Price), fresh.Quote.Price);
            Assert.IsTrue(BuyCatalog.Confirm(fresh));
            Assert.AreEqual(System.Math.Max(0, moneyNow - license.Price), wallet.Money);
        }

        // =====================================================================================
        //  5. ONE COUNTER, THE WHOLE STORE — the buy screen lists it all from one stall
        // =====================================================================================

        [Test]
        public void OneStall_ListsTheRod_TheBait_TheIce_TheSounder_AndTheLicence_Together()
        {
            // ActiveHullId matters now: an INSTRUMENT is fitted to the boat you are aboard, so the
            // counter's chandlery row needs a boat to quote against (ADR 0025 S2 / ADR 0030).
            var save = new SaveData { ActiveHullId = "boat.dory" };
            var stall = new GameObject("GeneralStoreCounter");
            _spawned.Add(stall);

            var gear = ScriptableObject.CreateInstance<GearOffer>();
            gear.Id = "gear.rod"; gear.DisplayName = "Fishing Rod"; gear.Price = 60;
            _spawned.Add(gear);
            var gearShop = stall.AddComponent<GearShop>();
            typeof(GearShop).GetField("_offer", System.Reflection.BindingFlags.NonPublic |
                                                System.Reflection.BindingFlags.Instance)
                            .SetValue(gearShop, gear);

            var baitShop = stall.AddComponent<BaitShop>();
            typeof(BaitShop).GetField("_bait", System.Reflection.BindingFlags.NonPublic |
                                               System.Reflection.BindingFlags.Instance)
                            .SetValue(baitShop, MakeBait("bait.capelin", 2, 10));

            var supplyShop = stall.AddComponent<SupplyShop>();
            typeof(SupplyShop).GetField("_supply", System.Reflection.BindingFlags.NonPublic |
                                                   System.Reflection.BindingFlags.Instance)
                              .SetValue(supplyShop, MakeSupply("supply.ice", 6));

            var instrument = ScriptableObject.CreateInstance<InstrumentOffer>();
            instrument.Id = "instrument.depth_sounder";
            instrument.DisplayName = "Depth Sounder";
            instrument.Price = 40;
            _spawned.Add(instrument);
            var chandlery = stall.AddComponent<InstrumentShop>();
            typeof(InstrumentShop).GetField("_offer", System.Reflection.BindingFlags.NonPublic |
                                                      System.Reflection.BindingFlags.Instance)
                                  .SetValue(chandlery, instrument);

            var vendor = stall.AddComponent<LicenseVendor>();
            typeof(LicenseVendor).GetField("_license", System.Reflection.BindingFlags.NonPublic |
                                                       System.Reflection.BindingFlags.Instance)
                                 .SetValue(vendor, MakeLicense("license.clam", 15, "fish.soft_shell_clam"));

            var rows = new List<BuyRow>();
            CatalogTestStall.BuildWired(stall, 100, save, new FakeLicenses(), rows);

            var byId = new Dictionary<string, BuyRow>();
            foreach (BuyRow r in rows) byId[r.Id] = r;

            Assert.AreEqual(5, rows.Count, "a general store is five vendors on one counter, one screen");
            Assert.AreEqual(BuyRowKind.Gear, byId["gear.rod"].Quote.Kind);
            Assert.AreEqual(BuyRowKind.Bait, byId["bait.capelin"].Quote.Kind);
            Assert.AreEqual(BuyRowKind.Supply, byId["supply.ice"].Quote.Kind);
            Assert.AreEqual(BuyRowKind.Instrument, byId["instrument.depth_sounder"].Quote.Kind);
            Assert.AreEqual(BuyRowKind.License, byId["license.clam"].Quote.Kind);
            StringAssert.Contains("boat.dory", byId["instrument.depth_sounder"].Note,
                "the chandlery row names the boat it would be bolted into");

            Assert.AreEqual(20, byId["bait.capelin"].Quote.Price, "the bait row prices the LOT, not one hook");
            StringAssert.Contains("x10", byId["bait.capelin"].DisplayName,
                "and says so, so the lot price is not misread as a unit price");

            foreach (BuyRow r in rows)
                Assert.IsTrue(r.Quote.CanBuy, "with 100₲ every row on this counter is affordable");

            // Counted rows are never "owned out" — that is what makes the store a repeat destination.
            Assert.IsFalse(byId["bait.capelin"].Quote.Owned);
            Assert.IsFalse(byId["supply.ice"].Quote.Owned);
        }
    }
}
