using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The T-set STOCK GATE (pots are OWNED, bought at the shipwright): a fresh set through
    /// <see cref="PlacedTrapService.TryPlaceGated"/> needs a SPARE owned pot —
    /// available = owned − deployed − aboard (<see cref="PotLocker"/>) — while the #193 deck RE-SET
    /// (<see cref="PlacedTrapService.TryPlacePreBaited"/>) stays deliberately ungated (it sets the pot
    /// that is already aboard: stock-neutral by construction). Covers the refusal, the spend-down as
    /// pots go in the water, the aboard pot counting against the locker, the haul freeing stock, and
    /// the cozy owner-facing toast. Fakes for clock/env/terrain/save (the TrapPlacementTests harness).
    /// </summary>
    public class PotStockGatingTests
    {
        private sealed class FakeClock : IGameClock
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

        private sealed class FakeEnv : IEnvironmentService
        {
            public int WorldSeed => 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => 4f;
            public float WaterLevelAt(double totalSeconds) => 4f;
        }

        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;   // depth = 4 − this
        }

        private sealed class FakeSaveService : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new();
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => _flags.TryGetValue(key, out var v) && v;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() { }
        }

        private readonly List<Object> _spawned = new();
        private readonly List<string> _notices = new();
        private void OnNotice(DevNotice n) => _notices.Add(n.Text);

        private SaveData _save;
        private TrapDef _trap;
        private BaitDef _bait;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<TrapPlaced>();
            EventBus.Clear<TrapRemoved>();
            EventBus.Clear<DevNotice>();
            GameServices.Reset();

            _save = SaveMigration.NewGame();
            GameServices.Clock = new FakeClock { Seconds = 1000.0 };
            GameServices.Environment = new FakeEnv();
            GameServices.TidalTerrain = new FlatTerrain();   // 4 m everywhere → depth gate open
            GameServices.Save = new FakeSaveService(_save);

            _trap = ScriptableObject.CreateInstance<TrapDef>();
            _trap.Id = "trap.lobster"; _trap.DisplayName = "Lobster Pot";
            _trap.AllowedCatchFishIds = new[] { "fish.lobster" };
            _trap.RequiredBaitId = "bait.herring";
            _trap.SoakHours = 12f; _trap.MinSoakDepthMeters = 3f; _trap.MaxSoakDepthMeters = 40f;
            _spawned.Add(_trap);

            _bait = ScriptableObject.CreateInstance<BaitDef>();
            _bait.Id = "bait.herring"; _bait.DisplayName = "Herring";
            _spawned.Add(_bait);

            // Plenty of bait — these tests isolate the POT gate (bait has its own tests).
            _save.BaitStock.Add(new BaitStock(_bait.Id, 10));
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<DevNotice>(OnNotice);
            EventBus.Clear<TrapPlaced>();
            EventBus.Clear<TrapRemoved>();
            EventBus.Clear<DevNotice>();
            GameServices.Reset();
            _notices.Clear();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private PlacedTrapService MakeService()
        {
            var go = new GameObject("PlacedTrapService");
            _spawned.Add(go);
            var svc = go.AddComponent<PlacedTrapService>();
            svc.Configure(new[] { _trap }, new[] { _bait }, go.transform);
            return svc;
        }

        // ---- the fresh-set gate ---------------------------------------------------------------------

        [Test]
        public void TryPlaceGated_NoOwnedPots_RefusesNoPotStock_NothingPlacedNoBaitSpent()
        {
            var svc = MakeService();

            var result = svc.TryPlaceGated(_trap, _bait, Vector2.zero, "region.st_peters", out PlacedTrap placed);

            Assert.AreEqual(PlacedTrapService.PlaceResult.NoPotStock, result);
            Assert.IsNull(placed);
            Assert.AreEqual(0, svc.Live.Count, "no pot conjured from thin air");
            Assert.AreEqual(0, _save.PlacedTraps.Count);
            Assert.AreEqual(10, _save.BaitStock[0].Count, "a refused set spends no bait");
        }

        [Test]
        public void TryPlaceGated_SpendsTheLockerDown_ThenRefuses()
        {
            PotLocker.AddOwned(_save, _trap.Id, 2);
            var svc = MakeService();

            Assert.AreEqual(PlacedTrapService.PlaceResult.Placed,
                svc.TryPlaceGated(_trap, _bait, new Vector2(0f, 0f), "region.st_peters", out _), "1st of 2");
            Assert.AreEqual(PlacedTrapService.PlaceResult.Placed,
                svc.TryPlaceGated(_trap, _bait, new Vector2(5f, 0f), "region.st_peters", out _), "2nd of 2");
            Assert.AreEqual(PlacedTrapService.PlaceResult.NoPotStock,
                svc.TryPlaceGated(_trap, _bait, new Vector2(10f, 0f), "region.st_peters", out _),
                "both owned pots are in the water — the third set needs a purchase (the P2 wheel)");

            Assert.AreEqual(2, svc.Live.Count);
            Assert.AreEqual(2, PotLocker.OwnedCount(_save, _trap.Id), "setting never changes OWNED");
            Assert.AreEqual(0, PotLocker.AvailableCount(_save, _trap.Id, 0));
        }

        [Test]
        public void RemoveTrap_ReturnsThePotToTheLocker_FreshSetWorksAgain()
        {
            PotLocker.AddOwned(_save, _trap.Id, 1);
            var svc = MakeService();

            Assert.AreEqual(PlacedTrapService.PlaceResult.Placed,
                svc.TryPlaceGated(_trap, _bait, Vector2.zero, "region.st_peters", out PlacedTrap placed));
            Assert.AreEqual(PlacedTrapService.PlaceResult.NoPotStock,
                svc.TryPlaceGated(_trap, _bait, new Vector2(5f, 0f), "region.st_peters", out _));

            // Haul/clear her out of the world — the DTO leaves the save, the pot is spare again.
            svc.RemoveTrap(placed);
            Assert.AreEqual(1, PotLocker.AvailableCount(_save, _trap.Id, 0));
            Assert.AreEqual(PlacedTrapService.PlaceResult.Placed,
                svc.TryPlaceGated(_trap, _bait, new Vector2(5f, 0f), "region.st_peters", out _),
                "the same owned pot sets again — nothing was consumed, only relocated");
        }

        // ---- the pot ABOARD counts against the locker -------------------------------------------------

        [Test]
        public void AboardPot_CountsAgainstTheLocker_ViaTheRegisteredCounter()
        {
            PotLocker.AddOwned(_save, _trap.Id, 1);
            var svc = MakeService();

            // The deck-work lane reports one pot of this kind aboard (the hauled deck pot).
            svc.SetAboardPotCounter(id => id == _trap.Id ? 1 : 0);

            Assert.AreEqual(PlacedTrapService.PlaceResult.NoPotStock,
                svc.TryPlaceGated(_trap, _bait, Vector2.zero, "region.st_peters", out _),
                "the one owned pot is riding the deck — a fresh set may not conjure a second");

            svc.SetAboardPotCounter(null);   // deck cleared
            Assert.AreEqual(PlacedTrapService.PlaceResult.Placed,
                svc.TryPlaceGated(_trap, _bait, Vector2.zero, "region.st_peters", out _));
        }

        [Test]
        public void DeckController_RegistersItselfAsTheAboardSource_AndTracksThePot()
        {
            PotLocker.AddOwned(_save, _trap.Id, 1);
            var svc = MakeService();

            // A deck-working trap kind (the #193 opt-in) with one resolved animal aboard.
            var deckDef = ScriptableObject.CreateInstance<DeckWorkDef>();
            deckDef.Id = "deckwork.test"; deckDef.DisplayName = "Test deck work";
            deckDef.SpeciesRules = new[]
            {
                new SpeciesDeckRule
                {
                    SpeciesId = "fish.lobster", MinKeepSizeMm = 0f, SizeMinMm = 62f, SizeMaxMm = 140f,
                    CanBeBerried = false, BerriedChance01 = 0f, Shape = DeckAnimalShape.Lobster,
                },
            };
            _spawned.Add(deckDef);
            _trap.DeckWork = deckDef;

            var boatGo = new GameObject("Dory");
            _spawned.Add(boatGo);
            var deck = boatGo.AddComponent<PotDeckWorkController>();
            deck.Configure(svc, null, boatGo.transform, null);   // registers the aboard counter

            Assert.AreEqual(0, svc.AboardPotCount(_trap.Id), "empty deck → nothing aboard");

            // Bring a hauled pot aboard (the DTO has already left the save at haul — mirror that here).
            PlacedTrap placed = svc.PlaceTrap(_trap, _bait, Vector2.zero, "region.st_peters");
            _save.PlacedTraps.Clear();
            var items = new List<CatchItem>
            {
                new CatchItem("fish.lobster", "American Lobster", FishCategory.Shellfish, 1f, 28, 0.35f),
            };
            Assert.IsTrue(deck.BringAboard(placed, items));

            Assert.AreEqual(1, svc.AboardPotCount(_trap.Id), "the deck pot reports itself aboard");
            Assert.AreEqual(0, svc.AboardPotCount("trap.crab"), "only its own kind");
            Assert.AreEqual(PlacedTrapService.PlaceResult.NoPotStock,
                svc.TryPlaceGated(_trap, _bait, new Vector2(5f, 0f), "region.st_peters", out _),
                "owned 1 − deployed 0 − aboard 1 = none spare");

            deck.ClearPot();
            Assert.AreEqual(0, svc.AboardPotCount(_trap.Id), "cleared deck frees the derivation");
        }

        // ---- the #193 re-set flow stays ungated (stock-neutral) ----------------------------------------

        [Test]
        public void TryPlacePreBaited_IsNotStockGated_TheDeckPotIsAlreadyYours()
        {
            var svc = MakeService();
            Assert.AreEqual(0, PotLocker.OwnedCount(_save, _trap.Id), "locker empty on purpose");

            var result = svc.TryPlacePreBaited(_trap, _bait, Vector2.zero, "region.st_peters", out PlacedTrap placed);

            Assert.AreEqual(PlacedTrapService.PlaceResult.Placed, result,
                "the deck re-set places the pot that is ALREADY aboard — no stock check, no charge");
            Assert.IsNotNull(placed);
        }

        // ---- the cozy owner-facing refusal ---------------------------------------------------------------

        [Test]
        public void DevTrapInput_OutOfStock_ToastsTheCozyRefusal_NamingTheFix()
        {
            var svc = MakeService();
            var go = new GameObject("Boat");
            _spawned.Add(go);
            var dev = go.AddComponent<DevTrapInput>();
            dev.Configure(svc, go.transform, _trap, _bait, "region.st_peters");

            EventBus.Subscribe<DevNotice>(OnNotice);
            var result = dev.DropTrap();

            Assert.AreEqual(PlacedTrapService.PlaceResult.NoPotStock, result);
            CollectionAssert.Contains(_notices, "No spare pots aboard — the shipwright sells them",
                "the refusal names the fix (diegetic outcomes-only voice)");
        }
    }
}
