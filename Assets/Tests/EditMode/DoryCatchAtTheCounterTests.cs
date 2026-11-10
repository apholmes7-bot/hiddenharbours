using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// A fish caught from the dory can be SOLD: a counter takes the catch in the player's boat when
    /// she lies at a wharf, at that counter's own data-driven price, and leaves the pail's sale
    /// exactly as it was.
    ///
    /// <para>No scene. The counter, the market, the pail and the boat's hold are the real components;
    /// the wharf fitting and the wallet are fakes of their Core contracts. Everything is placed far
    /// from the origin so a registrant another fixture leaked cannot stand in for this one's wharf.</para>
    ///
    /// <para>EditMode does not run <c>OnEnable</c> on <c>AddComponent</c>, so the boat's hold is put
    /// into <see cref="BoatHolds"/> by hand here — and one test drives the real lifecycle to prove the
    /// component does it for itself in play.</para>
    /// </summary>
    public class DoryCatchAtTheCounterTests
    {
        private const FishCategory Groundfish = FishCategory.InshoreGroundfish;
        private const FishCategory Shellfish = FishCategory.Shellfish;

        // Test data, not shipped balance: the cod and the clam are this fixture's own.
        private const int CodBase = 14;
        private const float CodElasticity = 0.3f;
        private const int ClamBase = 20;
        private const float ClamElasticity = 0.45f;

        /// <summary>The wharf fitting. The boat is placed on a 3-4-5 triangle from it so "exactly at
        /// the reach" is exact in floating point (9 + 16 = 25).</summary>
        private static readonly Vector2 Bollard = new Vector2(5000f, 5000f);
        private static readonly Vector2 FiveMetresOff = Bollard + new Vector2(3f, 4f);
        private static readonly Vector2 FiftyMetresOff = Bollard + new Vector2(30f, 40f);

        // ---- fakes for the Core contracts ---------------------------------------------------------

        private sealed class FakeWallet : IWallet
        {
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        private sealed class FakeCleat : IMooringCleat
        {
            public FakeCleat(Vector2 at, CleatSide side) { WorldPosition = at; Side = side; }
            public string Id => "cleat.test";
            public CleatSide Side { get; }
            public Vector2 WorldPosition { get; }
            public float ElevationMeters => 0f;
        }

        private sealed class FakeBoatHold : IBoatHold
        {
            private readonly List<CatchItem> _items = new();
            public Vector2 WorldPosition { get; set; }
            public int CapacityUnits => 99;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FakeClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        /// <summary>One counter, as the island store stands: a market, a buyer, the sell point, the
        /// pail it is wired to and the wallet it pays.</summary>
        private sealed class Counter
        {
            public Market Market;
            public WharfSellPoint Point;
            public ClamBucket Pail;
            public FakeWallet Wallet;
        }

        private readonly List<Object> _spawned = new();
        private readonly List<IBoatHold> _registeredBoats = new();
        private readonly List<IMooringCleat> _registeredCleats = new();
        private readonly List<CatchSold> _sales = new();
        private readonly List<string> _notices = new();
        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(_config);
            GameServices.Config = _config;
            _sales.Clear();
            _notices.Clear();
            EventBus.Subscribe<CatchSold>(OnSold);
            EventBus.Subscribe<DevNotice>(OnNotice);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<CatchSold>(OnSold);
            EventBus.Unsubscribe<DevNotice>(OnNotice);
            foreach (IBoatHold b in _registeredBoats) BoatHolds.Unregister(b);
            foreach (IMooringCleat c in _registeredCleats) MooringCleats.Unregister(c);
            _registeredBoats.Clear();
            _registeredCleats.Clear();
            GameServices.Reset();
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private void OnSold(CatchSold e) => _sales.Add(e);
        private void OnNotice(DevNotice e) => _notices.Add(e.Text);

        // ---- builders -----------------------------------------------------------------------------

        private static void SetPrivate(object target, string field, object value)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"field '{field}' not found on {target.GetType().Name}");
            f.SetValue(target, value);
        }

        private Counter MakeCounter()
        {
            var go = new GameObject("GeneralStoreCounter");
            _spawned.Add(go);
            var market = go.AddComponent<Market>();
            market.Configure(_config, MarketId.StPetersStore);
            var buyer = go.AddComponent<FishBuyer>();
            SetPrivate(buyer, "_market", market);

            var player = new GameObject("Player");
            _spawned.Add(player);
            var pail = player.AddComponent<ClamBucket>();
            pail.Configure(capacity: 20, requireOwnedBucket: false);

            var wallet = new FakeWallet();
            var point = go.AddComponent<WharfSellPoint>();
            SetPrivate(point, "_buyer", buyer);
            SetPrivate(point, "_hold", pail);      // EditMode: Awake does not run on AddComponent
            SetPrivate(point, "_wallet", wallet);
            return new Counter { Market = market, Point = point, Pail = pail, Wallet = wallet };
        }

        private ShipHold MakeBoat(Vector2 at, int cod, bool register = true)
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.HoldUnits = 6;
            _spawned.Add(hull);

            var go = new GameObject("Dory");
            _spawned.Add(go);
            go.transform.position = at;
            var hold = go.AddComponent<ShipHold>();
            hold.SetHull(hull);
            for (int i = 0; i < cod; i++) Assert.IsTrue(hold.TryAdd(Cod()), "the test hold takes the cod");
            if (register) Register(hold);
            return hold;
        }

        private void Register(IBoatHold boat)
        {
            BoatHolds.Register(boat);
            _registeredBoats.Add(boat);
        }

        private FakeCleat MakeBollard(Vector2 at, CleatSide side = CleatSide.Shore)
        {
            var cleat = new FakeCleat(at, side);
            MooringCleats.Register(cleat);
            _registeredCleats.Add(cleat);
            return cleat;
        }

        private static CatchItem Cod()
            => new CatchItem("fish.atlantic_cod", "Atlantic Cod", Groundfish, 4f, CodBase, CodElasticity);

        private static CatchItem Clam()
            => new CatchItem("fish.soft_shell_clam", "Soft-shell Clam", Shellfish, 0.1f, ClamBase, ClamElasticity);

        private static void Fill(IHold hold, int clams)
        {
            for (int i = 0; i < clams; i++) Assert.IsTrue(hold.TryAdd(Clam()), "the pail takes the clam");
        }

        /// <summary>What this outlet pays for a lot, summed from the till's own per-unit helper and the
        /// market's own data — never a number typed into the test.</summary>
        private static int LotPrice(Market market, FishCategory category, int baseValue, float elasticity,
                                    int count, float supplyBefore = 0f)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
                total += SellPricing.MarginalPrice(baseValue, elasticity, supplyBefore, i,
                                                   market.DemandFor(category), 1f, market.PriceLevel);
            return total;
        }

        // =====================================================================================
        //  1. The boat at the wharf: her catch is sold, at the counter's price, and she is emptied
        // =====================================================================================

        [Test]
        public void ABoatAtTheWharf_SellsHerCatchAtTheCountersOwnPrice_AndIsEmptied()
        {
            Counter counter = MakeCounter();
            MakeBollard(Bollard);
            ShipHold dory = MakeBoat(FiveMetresOff, cod: 4);
            int expected = LotPrice(counter.Market, Groundfish, CodBase, CodElasticity, 4);
            Assert.Greater(expected, 0, "the reference price is a real price");

            int paid = counter.Point.SellOverTheCounter();

            Assert.AreEqual(expected, paid, "the boat's lot pays the store's own marginal price");
            Assert.AreEqual(expected, counter.Wallet.Money, "and the wallet holds exactly that");
            Assert.AreEqual(0, dory.UsedUnits, "what was sold has left the boat");
            Assert.AreEqual(4, counter.Point.LastUnitsSold, "the counter reports the four fish");
            Assert.AreEqual(expected, counter.Point.LastPayout);
            Assert.AreEqual(1, _sales.Count, "one sale: the pail was empty, so only the boat's lot sold");
            Assert.AreEqual(4, _sales[0].Count);
            Assert.AreEqual(expected, _sales[0].TotalPaid);
            Assert.IsEmpty(_notices, "nothing is refused, so nothing is said");
        }

        [Test]
        public void ThePriceIsTheOutletsData_MoveTheLevelAndThePayoutMoves()
        {
            // The same four cod at the same wharf, under two authored price levels. Nothing else differs.
            _config.MarketPriceLevelStPetersStore = 0.6f;
            Counter low = MakeCounter();
            MakeBollard(Bollard);
            ShipHold first = MakeBoat(FiveMetresOff, cod: 4);
            int atLow = low.Point.SellOverTheCounter();
            BoatHolds.Unregister(first);

            _config.MarketPriceLevelStPetersStore = 1f;
            Counter high = MakeCounter();
            MakeBoat(FiveMetresOff, cod: 4);
            int atHigh = high.Point.SellOverTheCounter();

            Assert.AreEqual(LotPrice(high.Market, Groundfish, CodBase, CodElasticity, 4), atHigh);
            Assert.Greater(atHigh, atLow, "the store's price level is the lever on the boat's catch too");
        }

        // =====================================================================================
        //  2. The reach is the owner's number
        // =====================================================================================

        [Test]
        public void TheReach_IsTheOwnersNumber_InsideSellsAndOutsideLeavesHerAlone()
        {
            Counter counter = MakeCounter();
            MakeBollard(Bollard);
            ShipHold dory = MakeBoat(FiveMetresOff, cod: 4);     // exactly 5 m from the bollard

            _config.CounterBerthReachMeters = 4.9f;
            Assert.AreEqual(0, counter.Point.SellOverTheCounter(), "a reach short of her sells nothing");
            Assert.AreEqual(4, dory.UsedUnits, "and her catch is still aboard");
            Assert.AreEqual(0, counter.Wallet.Money);
            Assert.AreEqual(0, counter.Point.LastUnitsSold);
            Assert.IsEmpty(_sales);
            CollectionAssert.AreEqual(new[] { BerthedCatch.NoticeBoatNotBerthed }, _notices,
                "the player is told why the fish did not sell, once");

            _notices.Clear();
            _config.CounterBerthReachMeters = 5f;
            Assert.Greater(counter.Point.SellOverTheCounter(), 0, "at exactly the reach she is berthed");
            Assert.AreEqual(0, dory.UsedUnits);
            Assert.IsEmpty(_notices);
        }

        [Test]
        public void AReachOfZero_IsTheOffSwitch_EvenForABoatOnTheBollard()
        {
            Counter counter = MakeCounter();
            MakeBollard(Bollard);
            ShipHold dory = MakeBoat(Bollard, cod: 3);
            _config.CounterBerthReachMeters = 0f;

            Assert.AreEqual(0, counter.Point.SellOverTheCounter());
            Assert.AreEqual(3, dory.UsedUnits, "zero means counters never take a boat's catch");
        }

        [Test]
        public void IsBerthed_MeasuresToShoreFittingsOnly_AndABadReachClosesIt()
        {
            var shore = new List<IMooringCleat> { new FakeCleat(Bollard, CleatSide.Shore) };
            var boatSide = new List<IMooringCleat> { new FakeCleat(Bollard, CleatSide.Boat) };

            Assert.IsTrue(BerthedCatch.IsBerthed(FiveMetresOff, shore, 5f), "at the reach is inside it");
            Assert.IsFalse(BerthedCatch.IsBerthed(FiveMetresOff, shore, 4.99f));
            Assert.IsFalse(BerthedCatch.IsBerthed(FiveMetresOff, boatSide, 50f),
                "another hull's cleat is not a wharf — a raft of boats at sea lands nothing");
            Assert.IsFalse(BerthedCatch.IsBerthed(FiveMetresOff, new List<IMooringCleat>(), 50f),
                "a region with no wharf fittings never lands a boat's catch");
            Assert.IsFalse(BerthedCatch.IsBerthed(FiveMetresOff, null, 50f));
            Assert.IsFalse(BerthedCatch.IsBerthed(Bollard, shore, 0f));
            Assert.IsFalse(BerthedCatch.IsBerthed(Bollard, shore, -3f));
            Assert.IsFalse(BerthedCatch.IsBerthed(Bollard, shore, float.NaN));
            Assert.IsFalse(BerthedCatch.IsBerthed(Bollard, shore, float.PositiveInfinity),
                "an infinite reach must not open every boat in the region to every counter");
        }

        [Test]
        public void Alongside_IsTheFirstLadenBerthedBoat_InRegistryOrder()
        {
            var shore = new List<IMooringCleat> { new FakeCleat(Bollard, CleatSide.Shore) };
            var emptyAtTheWharf = new FakeBoatHold { WorldPosition = FiveMetresOff };
            var ladenAtSea = new FakeBoatHold { WorldPosition = FiftyMetresOff };
            var ladenAtTheWharf = new FakeBoatHold { WorldPosition = FiveMetresOff };
            var alsoLadenAtTheWharf = new FakeBoatHold { WorldPosition = Bollard };
            ladenAtSea.TryAdd(Cod());
            ladenAtTheWharf.TryAdd(Cod());
            alsoLadenAtTheWharf.TryAdd(Cod());
            var boats = new List<IBoatHold> { null, emptyAtTheWharf, ladenAtSea, ladenAtTheWharf, alsoLadenAtTheWharf };
            var pail = new FakeBoatHold();

            Assert.AreSame(ladenAtTheWharf, BerthedCatch.Alongside(pail, boats, shore, 10f),
                "an empty boat at the wharf never hides the laden one behind her");
            Assert.IsTrue(BerthedCatch.CatchLeftAboard(pail, boats, shore, 10f),
                "and the laden boat out at sea is the one whose catch stays aboard");
            Assert.IsNull(BerthedCatch.Alongside(ladenAtTheWharf, new List<IBoatHold> { ladenAtTheWharf }, shore, 10f),
                "a counter wired to her is not offered her again");
            Assert.IsNull(BerthedCatch.Alongside(pail, null, shore, 10f));
            Assert.IsFalse(BerthedCatch.CatchLeftAboard(pail, null, shore, 10f));
            Assert.AreSame(ladenAtTheWharf, BerthedCatch.Alongside(null, boats, shore, 10f),
                "a counter with no wired hold at all can still take a berthed boat's catch");
        }

        [Test]
        public void UnwiredConfig_FallsBackToTheCodeDefault_NotToZero()
        {
            GameServices.Config = null;
            Assert.AreEqual(GameConfig.DefaultCounterBerthReachMeters, BerthedCatch.ReachMeters);
            Assert.Greater(GameConfig.DefaultCounterBerthReachMeters, 0f);

            GameServices.Config = _config;
            _config.CounterBerthReachMeters = 7.5f;
            Assert.AreEqual(7.5f, BerthedCatch.ReachMeters, "read live, so a slider moved in play applies");
        }

        // =====================================================================================
        //  3. The boat away from the wharf: the counter offers only the pail, as today
        // =====================================================================================

        [Test]
        public void ABoatAwayFromTheWharf_TheCounterTakesOnlyThePail()
        {
            // The reference: the same pail at the same kind of counter with no boat in the world.
            Counter reference = MakeCounter();
            Fill(reference.Pail, 5);
            int pailOnly = reference.Point.SellOverTheCounter();
            Assert.Greater(pailOnly, 0);
            _sales.Clear();

            Counter counter = MakeCounter();
            Fill(counter.Pail, 5);
            MakeBollard(Bollard);
            ShipHold dory = MakeBoat(FiftyMetresOff, cod: 4);

            int paid = counter.Point.SellOverTheCounter();

            Assert.AreEqual(pailOnly, paid, "the pail fetches what it always did");
            Assert.AreEqual(pailOnly, counter.Wallet.Money);
            Assert.AreEqual(0, counter.Pail.UsedUnits);
            Assert.AreEqual(5, counter.Point.LastUnitsSold, "only the pail's five left anyone's hands");
            Assert.AreEqual(4, dory.UsedUnits, "the boat was not touched");
            Assert.IsTrue(dory.Items.All(i => i.SpeciesId == "fish.atlantic_cod"));
            Assert.AreEqual(1, _sales.Count, "one sale, the pail's");
            CollectionAssert.AreEqual(new[] { BerthedCatch.NoticeBoatNotBerthed }, _notices);
        }

        // =====================================================================================
        //  4. The pail's sale is unchanged, to the coin and the unit
        // =====================================================================================

        [Test]
        public void ThePailSale_IsUnchanged_WithNoBoat_WithAnEmptyBoat_AndAheadOfAFullOne()
        {
            // (a) No boat anywhere — the shipped behaviour, taken as the reference.
            Counter a = MakeCounter();
            Fill(a.Pail, 7);
            int expectedPail = LotPrice(a.Market, Shellfish, ClamBase, ClamElasticity, 7);
            int paidA = a.Point.SellOverTheCounter();
            Assert.AreEqual(expectedPail, paidA, "the reference is the till's own arithmetic");
            Assert.AreEqual(1, _sales.Count);
            CatchSold referenceSale = _sales[0];
            int referenceUnits = a.Point.LastUnitsSold;
            int referencePayout = a.Point.LastPayout;
            _sales.Clear();

            // (b) An EMPTY boat at the wharf — nothing about the sale may differ.
            MakeBollard(Bollard);
            ShipHold empty = MakeBoat(FiveMetresOff, cod: 0);
            Counter b = MakeCounter();
            Fill(b.Pail, 7);
            Assert.AreEqual(paidA, b.Point.SellOverTheCounter());
            Assert.AreEqual(paidA, b.Wallet.Money);
            Assert.AreEqual(referenceUnits, b.Point.LastUnitsSold);
            Assert.AreEqual(referencePayout, b.Point.LastPayout);
            Assert.AreEqual(1, _sales.Count);
            Assert.AreEqual(referenceSale.TotalPaid, _sales[0].TotalPaid);
            Assert.AreEqual(referenceSale.Count, _sales[0].Count);
            Assert.IsEmpty(_notices, "an empty boat is nothing to mention");
            BoatHolds.Unregister(empty);
            _sales.Clear();

            // (c) A boat WITH a catch at the wharf — the pail sells first and identically; the boat's
            //     lot is a second sale on top.
            ShipHold dory = MakeBoat(FiveMetresOff, cod: 4);
            Counter c = MakeCounter();
            Fill(c.Pail, 7);
            int boatLot = LotPrice(c.Market, Groundfish, CodBase, CodElasticity, 4);

            int paidC = c.Point.SellOverTheCounter();

            Assert.AreEqual(2, _sales.Count, "the pail's sale, then the boat's");
            Assert.AreEqual(referenceSale.TotalPaid, _sales[0].TotalPaid, "the pail's coin is untouched");
            Assert.AreEqual(referenceSale.Count, _sales[0].Count, "and so are its units");
            Assert.AreEqual(boatLot, _sales[1].TotalPaid);
            Assert.AreEqual(4, _sales[1].Count);
            Assert.AreEqual(paidA + boatLot, paidC, "the answer is the two lots together");
            Assert.AreEqual(paidC, c.Wallet.Money);
            Assert.AreEqual(paidC, c.Point.LastPayout);
            Assert.AreEqual(7 + 4, c.Point.LastUnitsSold);
            Assert.AreEqual(0, c.Pail.UsedUnits);
            Assert.AreEqual(0, dory.UsedUnits);
        }

        [Test]
        public void TheBoatsLot_IsPricedIntoTheGlutThePailJustMade()
        {
            // Same category in both: three cod carried up in the pail, four more in the boat. The boat's
            // first cod is the market's FOURTH, not its first — the counter does not pay the opening
            // price twice.
            Counter counter = MakeCounter();
            for (int i = 0; i < 3; i++) counter.Pail.TryAdd(Cod());
            MakeBollard(Bollard);
            MakeBoat(FiveMetresOff, cod: 4);
            int pailLot = LotPrice(counter.Market, Groundfish, CodBase, CodElasticity, 3);
            int boatLot = LotPrice(counter.Market, Groundfish, CodBase, CodElasticity, 4, supplyBefore: 3f);
            int sevenAtOnce = LotPrice(counter.Market, Groundfish, CodBase, CodElasticity, 7);

            int paid = counter.Point.SellOverTheCounter();

            Assert.AreEqual(pailLot + boatLot, paid);
            Assert.AreEqual(sevenAtOnce, paid, "two lots over one counter price as one lot of seven");
        }

        // =====================================================================================
        //  5. A counter already wired to the boat's hold never sells it twice
        // =====================================================================================

        [Test]
        public void ACounterWiredToTheBoatsOwnHold_ThroughTheTravelProxy_SellsItOnce()
        {
            Counter counter = MakeCounter();
            MakeBollard(Bollard);
            ShipHold dory = MakeBoat(FiveMetresOff, cod: 4);

            // The Nine Mile Creek shape: the counter's provider is a stand-in that forwards to her hold.
            var proxyGo = new GameObject("PersistentProviders");
            _spawned.Add(proxyGo);
            var proxy = proxyGo.AddComponent<PersistentHoldProxy>();
            proxy.Bind(dory);
            SetPrivate(counter.Point, "_hold", proxy);
            int expected = LotPrice(counter.Market, Groundfish, CodBase, CodElasticity, 4);

            Assert.IsNull(BerthedCatch.AlongsideNow(proxy),
                "the stand-in and the boat are one catch — she is not offered a second time");
            int paid = counter.Point.SellOverTheCounter();

            Assert.AreEqual(expected, paid, "four cod, paid for once");
            Assert.AreEqual(expected, counter.Wallet.Money);
            Assert.AreEqual(1, _sales.Count);
            Assert.AreEqual(4, counter.Point.LastUnitsSold);
            Assert.AreEqual(0, dory.UsedUnits);
            Assert.IsEmpty(_notices);
        }

        [Test]
        public void ACounterWiredStraightToTheBoat_SellsItOnce()
        {
            Counter counter = MakeCounter();
            MakeBollard(Bollard);
            ShipHold dory = MakeBoat(FiftyMetresOff, cod: 4);   // not berthed: the WIRED sale needs no berth
            SetPrivate(counter.Point, "_hold", dory);
            int expected = LotPrice(counter.Market, Groundfish, CodBase, CodElasticity, 4);

            Assert.AreEqual(expected, counter.Point.SellOverTheCounter());
            Assert.AreEqual(1, _sales.Count);
            Assert.IsEmpty(_notices, "her catch was sold, so there is nothing left aboard to mention");
        }

        // =====================================================================================
        //  6. The boat's side of the seam
        // =====================================================================================

        [Test]
        public void TheShipHold_SaysWhereSheLies_AndIsInTheRegistryOnlyWhileEnabled()
        {
            ShipHold dory = MakeBoat(FiveMetresOff, cod: 1, register: false);
            Assert.AreEqual(FiveMetresOff, ((IBoatHold)dory).WorldPosition);
            dory.transform.position = FiftyMetresOff;
            Assert.AreEqual(FiftyMetresOff, dory.WorldPosition, "read live — she drifts");

            CollectionAssert.DoesNotContain(BoatHolds.Active.ToList(), dory);
            MethodInfo onEnable = typeof(ShipHold).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo onDisable = typeof(ShipHold).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(onEnable);
            Assert.IsNotNull(onDisable);
            try
            {
                onEnable.Invoke(dory, null);
                onEnable.Invoke(dory, null);
                Assert.AreEqual(1, BoatHolds.Active.Count(h => ReferenceEquals(h, dory)),
                    "enabled twice is still one boat — she must not be offered to a buyer twice");
            }
            finally
            {
                onDisable.Invoke(dory, null);
                onDisable.Invoke(dory, null);   // the second enable's bus subscription
            }
            CollectionAssert.DoesNotContain(BoatHolds.Active.ToList(), dory, "a disabled boat is not here");
        }

        [Test]
        public void ADestroyedBoat_LeftInTheRegistry_IsSkippedRatherThanSold()
        {
            Counter counter = MakeCounter();
            MakeBollard(Bollard);
            ShipHold dory = MakeBoat(FiveMetresOff, cod: 2);
            Object.DestroyImmediate(dory.gameObject);   // EditMode: no OnDisable ran, so she is still listed

            Assert.IsNull(BerthedCatch.AlongsideNow(counter.Pail));
            Assert.AreEqual(0, counter.Point.SellOverTheCounter());
            Assert.IsEmpty(_notices);
        }

        // =====================================================================================
        //  7. Rule 5: positions and a distance, nothing else
        // =====================================================================================

        [Test]
        public void TheSameWorld_SellsForTheSameCoin_EveryTime()
        {
            MakeBollard(Bollard);
            var payouts = new List<int>();
            var units = new List<int>();
            for (int run = 0; run < 3; run++)
            {
                Counter counter = MakeCounter();
                Fill(counter.Pail, 6);
                ShipHold dory = MakeBoat(FiveMetresOff, cod: 5);
                payouts.Add(counter.Point.SellOverTheCounter());
                units.Add(counter.Point.LastUnitsSold);
                BoatHolds.Unregister(dory);
            }
            Assert.AreEqual(payouts[0], payouts[1]);
            Assert.AreEqual(payouts[0], payouts[2]);
            Assert.AreEqual(11, units[0]);
            Assert.AreEqual(units[0], units[1]);
            Assert.AreEqual(units[0], units[2]);
        }

        [Test]
        public void NothingInThePath_ReadsAWallClockOrRollsDice()
        {
            string code = Path.Combine(Application.dataPath, "_Project", "Code");
            string[] files =
            {
                Path.Combine(code, "Economy", "BerthedCatch.cs"),
                Path.Combine(code, "Economy", "WharfSellPoint.cs"),
                Path.Combine(code, "Core", "Economy", "BoatHolds.cs"),
                Path.Combine(code, "Core", "Economy", "IBoatHold.cs"),
            };
            string[] forbidden =
            {
                "UnityEngine.Random", "System.Random", "Random.", "new Random", "DateTime", "Stopwatch",
                "Time.time", "Time.deltaTime", "Time.unscaledTime", "Time.realtimeSinceStartup", "TickCount",
            };
            foreach (string file in files)
            {
                Assert.IsTrue(File.Exists(file), $"the path under test moved: {file}");
                string text = File.ReadAllText(file);
                foreach (string token in forbidden)
                    StringAssert.DoesNotContain(token, text,
                        $"{Path.GetFileName(file)} must stay a pure read of positions (rule 5)");
            }
        }

        // =====================================================================================
        //  8. The premise this route stands on: the deck's ice box is not a way ashore
        // =====================================================================================

        /// <summary>
        /// The counter taking a berthed boat's catch is the ONLY way that catch leaves the boat for
        /// money. The one other thing on deck that rewrites the hold is the ice box, and it only ever
        /// re-stamps the same units in place. If this test goes red because the ice box (or anything
        /// added to it) learned to move a catch, the counter route has become a second route and
        /// should be looked at again.
        /// </summary>
        [Test]
        public void TheDeckIceBox_NeverMovesACatchOffTheBoat_ItOnlyRestampsIt()
        {
            var clock = new FakeClock();
            GameServices.Clock = clock;

            var container = ScriptableObject.CreateInstance<DeckContainerDef>();
            container.IceHoursPerUnit = 6f;
            container.LidMeltFactor = 2f;
            container.MaxIceUnits = 4;
            _spawned.Add(container);
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.HoldUnits = 6;
            hull.DeckContainer = container;
            _spawned.Add(hull);
            var go = new GameObject("Dory");
            _spawned.Add(go);
            var hold = go.AddComponent<ShipHold>();
            hold.SetHull(hull);
            var box = go.AddComponent<DeckIceBox>();
            box.Configure(hold);

            var pailGo = new GameObject("Player");
            _spawned.Add(pailGo);
            var pail = pailGo.AddComponent<ClamBucket>();
            pail.Configure(capacity: 20, requireOwnedBucket: false);
            GameServices.PlayerHold = pail;

            string[] landed = { "fish.atlantic_cod", "fish.haddock", "fish.atlantic_cod" };
            foreach (string id in landed)
                hold.TryAdd(new CatchItem(id, id, Groundfish, 4f, CodBase, CodElasticity,
                                          1f, Freshness.Landed(0d)));

            // Every state-changing thing the ice box can do, in the order a trip does them.
            Assert.IsTrue(box.TryAddIce(1));
            box.SetLid(true);
            Assert.AreEqual("Open the ice box", box.VerbLabel, "its one verb is the lid");
            box.SetLid(false);
            Assert.AreEqual("Close the ice box", box.VerbLabel);
            clock.TotalSeconds = 1e7;      // long past the ice giving out
            box.TickProtection();

            CollectionAssert.AreEqual(landed, hold.Items.Select(i => i.SpeciesId).ToArray(),
                "the same fish, in the same order, are still aboard");
            Assert.AreEqual(0, pail.UsedUnits, "nothing went into the pail");

            // The tripwire: the ice box's whole public surface, by name. A new verb here is a new way
            // to move a catch until proven otherwise.
            string[] verbs = typeof(DeckIceBox)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Select(m => m.Name)
                .OrderBy(n => n, System.StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEqual(
                new[] { "Configure", "Interact", "SetLid", "TickProtection", "TryAddIce" }, verbs);
        }
    }
}
