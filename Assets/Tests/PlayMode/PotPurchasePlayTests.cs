using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The P2 money wheel's first turn, end-to-end over the real runtime lifecycle: BUY a pot at the
    /// shipwright (PotShop spends the wallet, the save's stock increments) → T SETS it (the fresh-set
    /// stock gate spends the locker) → out of stock, the next T is the COZY REFUSAL naming the fix →
    /// buy another pot and the wheel turns again. Real production components (PotShop,
    /// PlacedTrapService, DevTrapInput) with the CoreLoopSmokeTests-style Core-contract doubles.
    /// The #193/#194 haul→deck→re-set cycle stays pinned by DeckWorkLoopPlayTests (which runs the
    /// re-set with ZERO owned pots — the stock-neutrality guarantee).
    /// </summary>
    public class PotPurchasePlayTests
    {
        private sealed class TestWallet : IWallet
        {
            public TestWallet(int starting) { Money = starting; }
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        private sealed class TestClock : IGameClock
        {
            public double Seconds;
            public double TotalSeconds => Seconds;
            public GameTime Now => default;
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => default;
            public bool IsMarketDay => false;
            public float HourOfDay => 12f;
            public float DayFraction => 0.5f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public void SeekTo(double totalSeconds) => Seconds = totalSeconds;
        }

        private sealed class DeepEnv : IEnvironmentService
        {
            public int WorldSeed => 20260715;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => 4f;
            public float WaterLevelAt(double totalSeconds) => 4f;
        }

        private sealed class DeepTerrain : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => -10f;
        }

        private sealed class TestSave : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new();
            public TestSave(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => _flags.TryGetValue(key, out var v) && v;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() { }
        }

        private readonly List<Object> _spawned = new();
        private readonly List<string> _notices = new();
        private void OnNotice(DevNotice n) => _notices.Add(n.Text);

        private SaveData _save;
        private TestWallet _wallet;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<DevNotice>();
            EventBus.Clear<PotPurchased>();
            EventBus.Clear<TrapPlaced>();
            GameServices.Reset();
            InteractionGate.Reset();

            _save = SaveMigration.NewGame();
            _wallet = new TestWallet(200);
            GameServices.Clock = new TestClock { Seconds = 1000.0 };
            GameServices.Environment = new DeepEnv();
            GameServices.TidalTerrain = new DeepTerrain();
            GameServices.Save = new TestSave(_save);
            GameServices.Wallet = _wallet;

            EventBus.Subscribe<DevNotice>(OnNotice);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<DevNotice>(OnNotice);
            EventBus.Clear<DevNotice>();
            EventBus.Clear<PotPurchased>();
            EventBus.Clear<TrapPlaced>();
            InteractionGate.Reset();
            GameServices.Reset();
            _notices.Clear();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        [UnityTest]
        public IEnumerator BuyAPot_SetIt_ExhaustStock_CozyRefusal_BuyAgain_TheWheelTurns()
        {
            // --- content (data, not code): the trap, its bait, and the shipwright's offer ---------
            var trap = ScriptableObject.CreateInstance<TrapDef>();
            trap.Id = "trap.lobster"; trap.DisplayName = "Lobster Pot";
            trap.AllowedCatchFishIds = new[] { "fish.lobster" };
            trap.RequiredBaitId = "bait.herring";
            trap.SoakHours = 12f; trap.MinSoakDepthMeters = 3f; trap.MaxSoakDepthMeters = 40f;
            _spawned.Add(trap);

            var bait = ScriptableObject.CreateInstance<BaitDef>();
            bait.Id = "bait.herring"; bait.DisplayName = "Herring";
            _spawned.Add(bait);
            _save.BaitStock.Add(new BaitStock(bait.Id, 5));

            var offer = ScriptableObject.CreateInstance<PotOffer>();
            offer.Id = "offer.lobster_pot"; offer.TrapDefId = trap.Id;
            offer.DisplayName = "Lobster Pot"; offer.Price = 80;
            _spawned.Add(offer);

            // --- the rigs: a shipwright stall and the boat's trap gear (real Awake/OnEnable) ------
            var stall = new GameObject("ShipwrightShed");
            _spawned.Add(stall);
            var shop = stall.AddComponent<PotShop>();

            var svcGo = new GameObject("PlacedTrapService");
            _spawned.Add(svcGo);
            var svc = svcGo.AddComponent<PlacedTrapService>();
            svc.Configure(new[] { trap }, new[] { bait }, svcGo.transform);

            var boatGo = new GameObject("Dory");
            _spawned.Add(boatGo);
            var dev = boatGo.AddComponent<DevTrapInput>();
            dev.Configure(svc, boatGo.transform, trap, bait, "region.st_peters");

            yield return null;   // let Awake/OnEnable run across the rigs

            // --- broke of pots: T is the cozy refusal, nothing conjured ---------------------------
            Assert.AreEqual(PlacedTrapService.PlaceResult.NoPotStock, dev.DropTrap());
            CollectionAssert.Contains(_notices, "No spare pots aboard — the shipwright sells them");
            Assert.AreEqual(0, svc.Live.Count);
            Assert.AreEqual(200, _wallet.Money, "no charge for a refusal");

            // --- BUY a pot: the wallet pays, the locker gains ------------------------------------
            Assert.IsTrue(shop.TryBuy(offer, _wallet, _save));
            Assert.AreEqual(120, _wallet.Money, "the pot cost ₲80");
            Assert.AreEqual(1, PotLocker.OwnedCount(_save, trap.Id));
            Assert.AreEqual(1, PotLocker.AvailableCount(_save, trap.Id, 0), "one spare in the locker");

            // --- T SETS the bought pot: stock spends into the water ------------------------------
            _notices.Clear();
            Assert.AreEqual(PlacedTrapService.PlaceResult.Placed, dev.DropTrap());
            CollectionAssert.Contains(_notices, "Pot set");
            Assert.AreEqual(1, svc.Live.Count, "the bought pot is soaking");
            Assert.AreEqual(1, _save.PlacedTraps.Count, "mirrored into the save");
            Assert.AreEqual(1, PotLocker.OwnedCount(_save, trap.Id), "setting relocates, never consumes");
            Assert.AreEqual(0, PotLocker.AvailableCount(_save, trap.Id, 0), "no spare left");

            // --- stock exhausted: the next fresh T is the refusal again ---------------------------
            _notices.Clear();
            Assert.AreEqual(PlacedTrapService.PlaceResult.NoPotStock, dev.DropTrap());
            CollectionAssert.Contains(_notices, "No spare pots aboard — the shipwright sells them");
            Assert.AreEqual(1, svc.Live.Count, "still just the one pot down");

            // --- buy another: the wheel turns ------------------------------------------------------
            Assert.IsTrue(shop.TryBuy(offer, _wallet, _save));
            Assert.AreEqual(40, _wallet.Money);
            Assert.AreEqual(2, PotLocker.OwnedCount(_save, trap.Id));

            _notices.Clear();
            Assert.AreEqual(PlacedTrapService.PlaceResult.Placed, dev.DropTrap());
            Assert.AreEqual(2, svc.Live.Count, "two bought pots, two buoys down");
            Assert.AreEqual(0, PotLocker.AvailableCount(_save, trap.Id, 0));

            // --- and the third buy is blocked by money, not magic ---------------------------------
            Assert.IsFalse(shop.TryBuy(offer, _wallet, _save), "₲40 can't cover ₲80 — earn it (P2)");
            Assert.AreEqual(40, _wallet.Money);
            Assert.AreEqual(2, PotLocker.OwnedCount(_save, trap.Id));

            yield return null;
        }
    }
}
