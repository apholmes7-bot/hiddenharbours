using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The Build-7 deck-work SEAMS, driven end-to-end at the controller level with fakes (the
    /// TrapHaulControllerTests harness style — glass sea, no scene, no input, no Time):
    ///
    ///   (1) a surfacing haul on a deck-working trap lands the POT, not the catch — nothing in the hold,
    ///       the trap out of the world AND the save (a reload can never re-haul it: no dupes);
    ///   (2) the deck cycle over the live controller — pick → band (the unchanged FishCaught land path)
    ///       → bait (consumes stock) → T sets HER pre-baited (no second bait charge);
    ///   (3) the haul refuses to start while a pot is aboard (one pot at a time, cozy toast);
    ///   (4) THE HOLD-RELEASE LATCH (the #181/#184 trap, third verse): a pot being worked owns Space —
    ///       the cast stands down, and when the pot clears a still-held key must NOT flip into a cast;
    ///   (5) the cozy auto-resolve lands keepers per the deterministic sort — no dupes, one land path.
    /// </summary>
    public class PotDeckWorkControllerTests
    {
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int Capacity = 6;
            public int CapacityUnits => Capacity;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { if (_items.Count >= Capacity) return false; _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

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
            public int Seed = 4242;
            public int WorldSeed => Seed;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;      // glass sea → the deterministic calm wind-in
            public float TideHeightAt(double totalSeconds) => 2f;
            public float WaterLevelAt(double totalSeconds) => 2f;
        }

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private sealed class DeepTerrain : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => -10f;   // deep everywhere → the set gate opens
        }

        private readonly List<Object> _spawned = new();
        private FakeClock _clock;
        private SaveData _save;
        private TrapDef _trap;
        private BaitDef _bait;
        private DeckWorkDef _deckDef;

        private const double PlaceTime = 5000.0;
        private static readonly double SoakSpan = 12.0 * 3600.0;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<TrapPlaced>();
            EventBus.Clear<TrapRemoved>();
            EventBus.Clear<TrapHaulStateChanged>();
            EventBus.Clear<DevNotice>();
            EventBus.Clear<ControlModeChanged>();
            GameServices.Reset();
            InteractionGate.Reset();
            FishSpeciesRegistry.Reset();

            _clock = new FakeClock { Seconds = PlaceTime };
            _save = SaveMigration.NewGame();
            GameServices.Clock = _clock;
            GameServices.Environment = new FakeEnv();
            GameServices.Save = new FakeSaveService(_save);

            FishSpeciesRegistry.Register(MakeSpecies("fish.lobster", "American Lobster"));

            // A deck ruleset with FORCED outcomes so the cycle is fully deterministic in the seam test:
            // every animal a keeper (min keep 0), never berried, never nipped.
            _deckDef = ScriptableObject.CreateInstance<DeckWorkDef>();
            _deckDef.Id = "deckwork.test"; _deckDef.DisplayName = "Test deck work";
            _deckDef.NipChanceRushed01 = 0f; _deckDef.NipChanceCareful01 = 0f;
            _deckDef.SpeciesRules = new[]
            {
                new SpeciesDeckRule
                {
                    SpeciesId = "fish.lobster", MinKeepSizeMm = 0f, SizeMinMm = 62f, SizeMaxMm = 140f,
                    CanBeBerried = false, BerriedChance01 = 0f, Shape = DeckAnimalShape.Lobster,
                },
            };
            _spawned.Add(_deckDef);

            _trap = ScriptableObject.CreateInstance<TrapDef>();
            _trap.Id = "trap.lobster"; _trap.DisplayName = "Lobster Pot";
            _trap.AllowedCatchFishIds = new[] { "fish.lobster" };
            _trap.RequiredBaitId = "bait.herring";
            _trap.SoakHours = 12f; _trap.MinSoakDepthMeters = 3f; _trap.MaxSoakDepthMeters = 40f;
            _trap.DeckWork = _deckDef;                                  // ← the Build-7 opt-in
            _spawned.Add(_trap);

            _bait = ScriptableObject.CreateInstance<BaitDef>();
            _bait.Id = "bait.herring"; _bait.DisplayName = "Herring";
            _bait.FavorsSpeciesIds = new[] { "fish.lobster" };
            _spawned.Add(_bait);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<TrapPlaced>();
            EventBus.Clear<TrapRemoved>();
            EventBus.Clear<TrapHaulStateChanged>();
            EventBus.Clear<DevNotice>();
            EventBus.Clear<ControlModeChanged>();
            InteractionGate.Reset();
            GameServices.Reset();
            FishSpeciesRegistry.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private FishSpeciesDef MakeSpecies(string id, string name)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = name; f.Category = FishCategory.Shellfish;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.AllowedGear = Gear.Trap; f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 0.5f; f.MaxWeightKg = 1.5f; f.BaseValue = 28; f.SupplyElasticity = 0.35f;
            f.SpawnWeight = 1f;
            _spawned.Add(f);
            return f;
        }

        private PlacedTrapService MakeService()
        {
            var go = new GameObject("PlacedTrapService");
            _spawned.Add(go);
            var svc = go.AddComponent<PlacedTrapService>();
            svc.Configure(new[] { _trap }, new[] { _bait }, go.transform);
            return svc;
        }

        /// <summary>The boat rig at the trap (in reach), glass-calm fast wind-in — two 1s holds surface.</summary>
        private TrapHaulController MakeHaul(PlacedTrapService svc, FakeHold hold, Vector2 railPos)
        {
            var railGo = new GameObject("Rail");
            railGo.transform.position = railPos;
            _spawned.Add(railGo);

            var go = new GameObject("Dory");
            _spawned.Add(go);
            var ctrl = go.AddComponent<TrapHaulController>();
            ctrl.Configure(svc, railGo.transform, hold, "region.coddle_cove", calmHaulRate: 0.6f);
            ctrl.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            return ctrl;
        }

        /// <summary>Haul the placed pot up: start + two 1-second holds (glass sea) → Surface.</summary>
        private static void HaulToSurface(TrapHaulController haul)
        {
            Assert.IsTrue(haul.TryStartHaul(), "a pot alongside starts a haul");
            haul.TickHaul(1f, holding: true);
            haul.TickHaul(1f, holding: true);
            Assert.IsFalse(haul.IsHauling, "two calm holds surface the pot");
        }

        /// <summary>The deck controller the haul spawned on its own GameObject at pot-aboard.</summary>
        private static PotDeckWorkController DeckOf(TrapHaulController haul)
        {
            var deck = haul.GetComponent<PotDeckWorkController>();
            Assert.IsNotNull(deck, "the haul spawns its deck-work sibling when a deck-working pot surfaces");
            return deck;
        }

        /// <summary>One release-then-hold-then-release verb on the deck: clears any pending release
        /// latch, holds for <paramref name="seconds"/>, releases. The grab resolves on the release; a
        /// band/bait completes mid-hold. Mirrors real hands on the key.</summary>
        private static void Work(PotDeckWorkController deck, float seconds)
        {
            deck.TickWork(0.02f, holding: false);   // clear any require-release from the last action
            deck.TickWork(seconds, holding: true);
            deck.TickWork(0.02f, holding: false);
        }

        private void GrantBait(int count)
        {
            _save.BaitStock ??= new List<BaitStock>();
            _save.BaitStock.Add(new BaitStock(_bait.Id, count));
        }

        private static int BaitCount(SaveData save, string baitId)
        {
            if (save?.BaitStock == null) return 0;
            for (int i = 0; i < save.BaitStock.Count; i++)
                if (save.BaitStock[i].BaitId == baitId) return save.BaitStock[i].Count;
            return 0;
        }

        // ---- (1) the pot lands ABOARD, not in the hold --------------------------------------------

        [Test]
        public void SurfacingADeckWorkingTrap_LandsThePot_NotTheCatch()
        {
            var svc = MakeService();
            svc.PlaceTrap(_trap, _bait, new Vector2(1f, 1f), "region.st_peters");
            _clock.Seconds = PlaceTime + SoakSpan;

            var hold = new FakeHold();
            var haul = MakeHaul(svc, hold, new Vector2(1f, 1f));

            int caught = 0;
            void OnCaught(FishCaught _) => caught++;
            EventBus.Subscribe<FishCaught>(OnCaught);
            HaulToSurface(haul);
            EventBus.Unsubscribe<FishCaught>(OnCaught);

            var deck = DeckOf(haul);
            Assert.IsTrue(deck.HasPotAboard, "the pot is on the deck, catch still inside");
            Assert.AreEqual(0, hold.UsedUnits, "NOTHING landed yet — landing now happens through the deck work");
            Assert.AreEqual(0, caught, "no FishCaught until a keeper is banded");
            Assert.AreEqual(0, svc.Live.Count, "the trap left the world");
            Assert.AreEqual(0, _save.PlacedTraps.Count,
                "…and the save (a reload can never re-haul it — the no-dupes half of the compromise)");
            Assert.AreEqual(1, deck.Pot.Animals.Count, "the resolved catch rides in the pot");
        }

        [Test]
        public void AnUnsoakedDeckWorkingTrap_StillComesUpEmpty_AndStaysDown()
        {
            var svc = MakeService();
            svc.PlaceTrap(_trap, _bait, new Vector2(1f, 1f), "region.st_peters");
            _clock.Seconds = PlaceTime + SoakSpan * 0.25;      // NOT ready

            var haul = MakeHaul(svc, new FakeHold(), new Vector2(1f, 1f));
            HaulToSurface(haul);

            var deck = haul.GetComponent<PotDeckWorkController>();
            Assert.IsTrue(deck == null || !deck.HasPotAboard, "an empty pot never lands on the deck");
            Assert.AreEqual(1, svc.Live.Count, "the unready trap stays down to keep soaking (the old cozy beat)");
        }

        [Test]
        public void ALegacyTrap_WithoutADeckWorkDef_StillLandsInstantly()
        {
            _trap.DeckWork = null;                              // the pre-Build-7 shape
            var svc = MakeService();
            svc.PlaceTrap(_trap, _bait, new Vector2(1f, 1f), "region.st_peters");
            _clock.Seconds = PlaceTime + SoakSpan;

            var hold = new FakeHold();
            var haul = MakeHaul(svc, hold, new Vector2(1f, 1f));
            HaulToSurface(haul);

            Assert.AreEqual(1, hold.UsedUnits, "no DeckWorkDef → the legacy instant land (older content unchanged)");
            var deck = haul.GetComponent<PotDeckWorkController>();
            Assert.IsTrue(deck == null || !deck.HasPotAboard);
        }

        // ---- (2) the full deck cycle over the live controller --------------------------------------

        [Test]
        public void TheDeckCycle_PickBandBaitSet_EndToEnd()
        {
            GrantBait(2);
            var svc = MakeService();
            svc.PlaceTrap(_trap, _bait, new Vector2(1f, 1f), "region.st_peters");   // consumes 1 → 1 left
            Assert.AreEqual(1, BaitCount(_save, _bait.Id));
            _clock.Seconds = PlaceTime + SoakSpan;

            var hold = new FakeHold();
            var haul = MakeHaul(svc, hold, new Vector2(1f, 1f));
            HaulToSurface(haul);
            var deck = DeckOf(haul);
            deck.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));

            int caught = 0;
            void OnCaught(FishCaught _) => caught++;
            EventBus.Subscribe<FishCaught>(OnCaught);

            // PICK: a full careful hold, released — forced-keeper facts → the keeper waits on deck.
            Work(deck, 1.0f);
            Assert.AreEqual(0, deck.Pot.InPotCount, "picked out");
            Assert.AreEqual(1, deck.Pot.OnDeckCount, "the keeper waits for its bands");
            Assert.AreEqual(0, hold.UsedUnits, "an unbanded keeper does NOT count yet");

            // BAND: a hold over the keeper completes at BandSeconds → it lands, sellable.
            Work(deck, 1.0f);
            Assert.AreEqual(1, hold.UsedUnits, "banded → stowed through the unchanged IHold path");
            Assert.AreEqual(1, caught, "FishCaught fired — the same land path the rod uses");
            Assert.AreEqual("fish.lobster", hold.Items[0].SpeciesId);

            // BAIT: the emptied pot takes one herring from the locker.
            Assert.IsTrue(deck.Pot.NeedsBait);
            Work(deck, 1.2f);
            Assert.IsTrue(deck.Pot.ReadyToSet, "baited → ready to set");
            Assert.AreEqual(0, BaitCount(_save, _bait.Id), "the re-bait consumed the one bait in stock");

            EventBus.Unsubscribe<FishCaught>(OnCaught);

            // SET (T): pre-baited — the set consumes NO second bait and the deck clears.
            GameServices.TidalTerrain = new DeepTerrain();
            // The dev input must live on the BOAT GameObject to see the deck sibling — mirror the rig.
            var dev = haul.gameObject.AddComponent<DevTrapInput>();
            dev.Configure(svc, haul.transform, _trap, _bait, "region.st_peters");
            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));

            var result = dev.DropTrap();
            Assert.AreEqual(PlacedTrapService.PlaceResult.Placed, result, "a READY pot sets");
            Assert.IsFalse(deck.HasPotAboard, "the deck is clear — she's back in the water");
            Assert.AreEqual(1, svc.Live.Count, "the pot soaks again");
            Assert.AreEqual(1, _save.PlacedTraps.Count, "…and is mirrored back into the save");
            Assert.AreEqual(0, BaitCount(_save, _bait.Id), "the set charged NO second bait (pre-baited)");
        }

        [Test]
        public void T_RefusesAnUnworkedPot_WithTheCozyReason()
        {
            GrantBait(1);
            var svc = MakeService();
            svc.PlaceTrap(_trap, _bait, new Vector2(1f, 1f), "region.st_peters");
            _clock.Seconds = PlaceTime + SoakSpan;
            var haul = MakeHaul(svc, new FakeHold(), new Vector2(1f, 1f));
            HaulToSurface(haul);
            var deck = DeckOf(haul);

            GameServices.TidalTerrain = new DeepTerrain();
            var dev = haul.gameObject.AddComponent<DevTrapInput>();
            dev.Configure(svc, haul.transform, _trap, _bait, "region.st_peters");
            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));

            Assert.AreEqual(PotDeckWorkController.DeckSetState.StillFull, deck.SetState);
            Assert.AreEqual(PlacedTrapService.PlaceResult.PotNotReady, dev.DropTrap(),
                "a pot still full refuses the set — work her first");
            Assert.IsTrue(deck.HasPotAboard, "nothing lost on the refusal");
            Assert.AreEqual(0, svc.Live.Count, "and nothing placed");
        }

        [Test]
        public void BaitingWithAnEmptyLocker_Refuses_NothingSpentNothingBaited()
        {
            var svc = MakeService();
            svc.PlaceTrap(_trap, _bait, new Vector2(1f, 1f), "region.st_peters");   // greybox drop: no stock, still places
            _clock.Seconds = PlaceTime + SoakSpan;
            var hold = new FakeHold();
            var haul = MakeHaul(svc, hold, new Vector2(1f, 1f));
            HaulToSurface(haul);
            var deck = DeckOf(haul);
            deck.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));

            Work(deck, 1.0f);   // pick
            Work(deck, 1.0f);   // band
            Assert.IsTrue(deck.Pot.NeedsBait);
            Work(deck, 1.2f);   // try to bait — the locker is empty
            Assert.IsFalse(deck.Pot.Baited, "no bait aboard → the pot stays unbaited (cozy refusal)");
            Assert.AreEqual(PotDeckWorkController.DeckSetState.Unbaited, deck.SetState);
        }

        // ---- (3) one pot at a time -----------------------------------------------------------------

        [Test]
        public void AFreshHaul_RefusesToStart_WhileAPotIsAboard()
        {
            var svc = MakeService();
            svc.PlaceTrap(_trap, _bait, new Vector2(1f, 1f), "region.st_peters");
            svc.PlaceTrap(_trap, _bait, new Vector2(1.5f, 1f), "region.st_peters");
            _clock.Seconds = PlaceTime + SoakSpan;

            var haul = MakeHaul(svc, new FakeHold(), new Vector2(1f, 1f));
            HaulToSurface(haul);
            Assert.IsTrue(DeckOf(haul).HasPotAboard);

            Assert.IsFalse(haul.TryStartHaul(), "the deck works ONE pot at a time — square her away first");
            Assert.AreEqual(1, svc.Live.Count, "the second pot stays down, untouched");
        }

        // ---- (4) the hold-release latch (the #181/#184 trap, deck-work verse) -----------------------

        [Test]
        public void DeckWork_OwnsSpace_AndTheLatchCatchesTheCarriedHold_WhenThePotClears()
        {
            var svc = MakeService();
            svc.PlaceTrap(_trap, _bait, new Vector2(1f, 1f), "region.st_peters");
            _clock.Seconds = PlaceTime + SoakSpan;

            var hold = new FakeHold();
            var haul = MakeHaul(svc, hold, new Vector2(1f, 1f));

            // The full Dory rig: the handline + its dev input share the boat GameObject.
            var fishing = haul.gameObject.AddComponent<FishingController>();
            fishing.Configure(hold, new[] { MakeSpecies("fish.cod2", "Cod") }, "region.coddle_cove", Gear.Handline, seed: 7);
            var dev = haul.gameObject.AddComponent<DevFishingInput>();
            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));

            HaulToSurface(haul);
            var deck = DeckOf(haul);
            deck.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            Assert.IsTrue(deck.HasPotAboard);

            // (a) the stand-down: while the pot is being worked, Space belongs to the DECK — no cast.
            Assert.IsFalse(dev.FishingLive, "a pot aboard owns Space — the handline stands down");
            dev.TickFishing(0.05f, rawHeld: true);
            Assert.AreEqual(FishingPhase.Idle, fishing.Phase, "holding Space over the pot must never cast");

            // (b) the latch: clear the pot WITH the key still held (the auto-resolve path ends the work
            // exactly like a set would) — the reopened gate must swallow the carried-over hold.
            deck.AutoResolvePot();
            Assert.IsFalse(deck.HasPotAboard);
            Assert.IsTrue(dev.FishingLive, "pot cleared → the handline gate is open again");
            dev.TickFishing(0.05f, rawHeld: true);
            Assert.AreEqual(FishingPhase.Idle, fishing.Phase,
                "a key still held from the deck work must NOT auto-cast (the resurrected #184 bug, third verse)");

            // (c) a genuine release-then-press casts.
            dev.TickFishing(0.05f, rawHeld: false);
            dev.TickFishing(0.05f, rawHeld: true);
            Assert.AreEqual(FishingPhase.Waiting, fishing.Phase, "release-then-press casts a fresh line");
        }

        // ---- (5) the cozy auto-resolve over the live controller -------------------------------------

        [Test]
        public void AutoResolve_LandsTheKeepers_OneToast_NoDupes()
        {
            var svc = MakeService();
            svc.PlaceTrap(_trap, _bait, new Vector2(1f, 1f), "region.st_peters");
            _clock.Seconds = PlaceTime + SoakSpan;
            var hold = new FakeHold();
            var haul = MakeHaul(svc, hold, new Vector2(1f, 1f));
            HaulToSurface(haul);
            var deck = DeckOf(haul);

            int caught = 0, toasts = 0;
            void OnCaught(FishCaught _) => caught++;
            void OnNotice(DevNotice _) => toasts++;
            EventBus.Subscribe<FishCaught>(OnCaught);
            EventBus.Subscribe<DevNotice>(OnNotice);
            deck.AutoResolvePot();
            deck.AutoResolvePot();   // idempotent — a second call is a no-op
            EventBus.Unsubscribe<FishCaught>(OnCaught);
            EventBus.Unsubscribe<DevNotice>(OnNotice);

            Assert.AreEqual(1, hold.UsedUnits, "the keeper landed as if picked + banded (forced-keeper facts)");
            Assert.AreEqual(1, caught, "…through the unchanged FishCaught path");
            Assert.AreEqual(1, toasts, "ONE summary toast, and none for the idempotent second call");
            Assert.IsFalse(deck.HasPotAboard, "the deck is squared away");
        }

        [Test]
        public void OffDeck_IsACozyPause_ThePotStays_AndWorkResumes()
        {
            var svc = MakeService();
            svc.PlaceTrap(_trap, _bait, new Vector2(1f, 1f), "region.st_peters");
            _clock.Seconds = PlaceTime + SoakSpan;
            var hold = new FakeHold();
            var haul = MakeHaul(svc, hold, new Vector2(1f, 1f));
            HaulToSurface(haul);
            var deck = DeckOf(haul);

            // Step to the helm mid-work: the pot STAYS (unlike the live haul's drop) — resume on return.
            deck.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
            Assert.IsTrue(deck.HasPotAboard, "leaving the deck pauses the work, never dumps the pot");
            Assert.IsFalse(deck.GearKeysLive, "…but the keys are dead off the deck");

            deck.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            Assert.IsTrue(deck.GearKeysLive);
            Work(deck, 1.0f);
            Assert.AreEqual(1, deck.Pot.OnDeckCount, "back on deck, the pick works exactly as before");
        }
    }
}
