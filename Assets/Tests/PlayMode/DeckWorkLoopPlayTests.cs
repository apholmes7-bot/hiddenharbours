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
    /// Build 7 — the trap loop's DECK WORK, end-to-end in PlayMode over the real runtime lifecycle
    /// (Awake/OnEnable bus wiring, the components as the greybox rigs them on one boat GameObject):
    /// place a baited pot → soak → haul with the swell → the POT lands ON DECK with the deterministic
    /// catch inside (nothing in the hold yet) → pick (grab-hold) → sort (the deterministic keeper/short
    /// split) → band (the unchanged FishCaught land path) → re-bait (consumes the bait stock) → T sets
    /// her again, pre-baited. The CoreLoopSmokeTests harness style: real production logic end to end,
    /// minimal Core-contract doubles for the stateful holders, a glass sea for determinism.
    /// </summary>
    public class DeckWorkLoopPlayTests
    {
        private sealed class CapHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 6;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { if (_items.Count >= CapacityUnits) return false; _items.Add(item); return true; }
            public void Clear() => _items.Clear();
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

        private sealed class GlassEnv : IEnvironmentService
        {
            public int WorldSeed => 20260713;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;   // glass sea → the deterministic calm wind-in
            public float TideHeightAt(double totalSeconds) => 2f;
            public float WaterLevelAt(double totalSeconds) => 2f;
        }

        private sealed class TestSave : ISaveService
        {
            public TestSave(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private sealed class DeepTerrain : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => -10f;
        }

        private readonly List<Object> _spawned = new();
        private TestClock _clock;
        private SaveData _save;

        private const double PlaceTime = 5000.0;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<DevNotice>();
            EventBus.Clear<ControlModeChanged>();
            GameServices.Reset();
            InteractionGate.Reset();
            FishSpeciesRegistry.Reset();

            _clock = new TestClock { Seconds = PlaceTime };
            _save = SaveMigration.NewGame();
            GameServices.Clock = _clock;
            GameServices.Environment = new GlassEnv();
            GameServices.Save = new TestSave(_save);
            GameServices.TidalTerrain = new DeepTerrain();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<DevNotice>();
            EventBus.Clear<ControlModeChanged>();
            InteractionGate.Reset();
            GameServices.Reset();
            FishSpeciesRegistry.Reset();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // ---- builders -------------------------------------------------------------------------

        private FishSpeciesDef MakeLobster()
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = "fish.lobster"; f.DisplayName = "American Lobster"; f.Category = FishCategory.Shellfish;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.AllowedGear = Gear.Trap; f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 0.5f; f.MaxWeightKg = 1.5f; f.BaseValue = 28; f.SupplyElasticity = 0.35f;
            f.SpawnWeight = 1f;
            _spawned.Add(f);
            return f;
        }

        /// <summary>The shipped-shape ruleset, with the gauge INSIDE the size window — the keeper/short
        /// split is real and the test derives the expected verdict from the same pure streams the game
        /// uses (self-consistent determinism, the rule-5 guarantee). Nips forced OFF so the number of
        /// grab holds is fixed.</summary>
        private DeckWorkDef MakeDeckDef()
        {
            var d = ScriptableObject.CreateInstance<DeckWorkDef>();
            d.Id = "deckwork.play_test"; d.DisplayName = "Play-test deck work";
            d.NipChanceRushed01 = 0f; d.NipChanceCareful01 = 0f;
            // Multi-animal layout for a FIXED test worker (the real player walks the deck): the keeper
            // row starts farther from the worker than the pot, so the contextual "nearest work wins"
            // verb keeps GRABBING while the pot holds animals (keepers never out-rank her mid-pick),
            // and the reach covers the whole 3-keeper row for the banding pass.
            d.WorkReachMeters = 2.0f;
            d.KeeperRowOffset = new Vector2(0.6f, -0.25f);
            d.SpeciesRules = new[]
            {
                new SpeciesDeckRule
                {
                    SpeciesId = "fish.lobster", MinKeepSizeMm = 83f, SizeMinMm = 62f, SizeMaxMm = 140f,
                    CanBeBerried = true, BerriedChance01 = 0.15f, Shape = DeckAnimalShape.Lobster,
                },
            };
            _spawned.Add(d);
            return d;
        }

        private (TrapDef trap, BaitDef bait) MakeContent(DeckWorkDef deckDef)
        {
            var bait = ScriptableObject.CreateInstance<BaitDef>();
            bait.Id = "bait.herring"; bait.DisplayName = "Herring";
            bait.FavorsSpeciesIds = new[] { "fish.lobster" };
            _spawned.Add(bait);

            var trap = ScriptableObject.CreateInstance<TrapDef>();
            trap.Id = "trap.lobster"; trap.DisplayName = "Lobster Pot";
            trap.AllowedCatchFishIds = new[] { "fish.lobster" };
            trap.RequiredBaitId = "bait.herring";
            trap.SoakHours = 12f; trap.MinSoakDepthMeters = 3f; trap.MaxSoakDepthMeters = 40f;
            // Multi-catch: a 3-animal pot, full at 24h (ready at 12h) — the test soaks her to FULL so the
            // deck works a whole pot, not one animal. Capacity 3 keeps the greybox keeper row within the
            // Def's default working reach of the fixed test worker (the real player walks the deck).
            trap.CapacityUnits = 3; trap.HoursToFullPot = 24f;
            trap.DeckWork = deckDef;
            _spawned.Add(trap);
            return (trap, bait);
        }

        /// <summary>One deck verb: clear any pending release, hold, release (the real key rhythm).</summary>
        private static void Work(PotDeckWorkController deck, float seconds)
        {
            deck.TickWork(0.02f, holding: false);
            deck.TickWork(seconds, holding: true);
            deck.TickWork(0.02f, holding: false);
        }

        private static int BaitCount(SaveData save, string baitId)
        {
            if (save?.BaitStock == null) return 0;
            for (int i = 0; i < save.BaitStock.Count; i++)
                if (save.BaitStock[i].BaitId == baitId) return save.BaitStock[i].Count;
            return 0;
        }

        // ---- the full cycle ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator FullDeckCycle_HaulPickSortBandBaitSet_LandsKeepersOnly()
        {
            FishSpeciesRegistry.Register(MakeLobster());
            DeckWorkDef deckDef = MakeDeckDef();
            (TrapDef trap, BaitDef bait) = MakeContent(deckDef);

            // Bait aboard (the G-grant stock) — 2: one for the first set, one for the deck re-bait.
            _save.BaitStock = new List<BaitStock> { new BaitStock(bait.Id, 2) };

            // The service + the boat rig on ONE GameObject, the greybox shape (real Awake/OnEnable).
            var svcGo = new GameObject("PlacedTrapService");
            _spawned.Add(svcGo);
            var svc = svcGo.AddComponent<PlacedTrapService>();
            svc.Configure(new[] { trap }, new[] { bait }, svcGo.transform);

            var railGo = new GameObject("Rail");
            railGo.transform.position = new Vector2(1f, 1f);
            _spawned.Add(railGo);

            var hold = new CapHold();
            var boatGo = new GameObject("Dory");
            _spawned.Add(boatGo);
            var haul = boatGo.AddComponent<TrapHaulController>();
            haul.Configure(svc, railGo.transform, hold, "region.coddle_cove", calmHaulRate: 0.6f);

            // On deck — through the BUS, the real path (the components subscribed in OnEnable).
            EventBus.Publish(new ControlModeChanged(ControlMode.OnDeck));

            // SET the pot (consumes bait #1), soak it FULL (24h = HoursToFullPot — the whole pot).
            PlacedTrap placed = svc.PlaceTrap(trap, bait, new Vector2(1f, 1f), "region.st_peters");
            Assert.IsNotNull(placed);
            Assert.AreEqual(1, BaitCount(_save, bait.Id));
            _clock.Seconds = PlaceTime + trap.HoursToFullPot * 3600.0;

            // What WILL the pot land? Derive the expected LIST + per-animal sort from the same pure
            // streams the game uses BEFORE the haul — the test's determinism oracle (rule 5: recompute,
            // don't peek).
            var ctx = new CatchContext("region.coddle_cove", 2f, 12f, Season.HighSummer, Gear.Trap);
            var expected = new List<CatchItem>();
            int expectedCount = placed.ResolveCatches(_clock.Seconds, in ctx, expected);
            Assert.AreEqual(trap.CapacityUnits, expectedCount,
                "a full soak fills the pot to capacity — the owner's multi-catch ask");
            var expectedSize = new float[expectedCount];
            var expectedBerried = new bool[expectedCount];
            var expectedKeeper = new bool[expectedCount];
            int expectedKeepers = 0;
            for (int i = 0; i < expectedCount; i++)
            {
                uint sizeHash = DeckWork.AnimalHash(placed.WorldSeed, placed.InstanceId,
                    placed.PlacementGameTimeSeconds, expected[i].SpeciesId, i, DeckWork.SizeChannel);
                uint berriedHash = DeckWork.AnimalHash(placed.WorldSeed, placed.InstanceId,
                    placed.PlacementGameTimeSeconds, expected[i].SpeciesId, i, DeckWork.BerriedChannel);
                expectedSize[i] = DeckWork.SizeMm(sizeHash, 62f, 140f);
                expectedBerried[i] = DeckWork.RollBerried(berriedHash, true, 0.15f);
                expectedKeeper[i] = DeckWork.IsKeeper(expectedSize[i], expectedBerried[i], 83f);
                if (expectedKeeper[i]) expectedKeepers++;
            }

            int caught = 0;
            void OnCaught(FishCaught _) => caught++;
            EventBus.Subscribe<FishCaught>(OnCaught);

            // HAUL with the swell (glass sea → the steady wind-in; two 1s holds surface her).
            Assert.IsTrue(haul.TryStartHaul());
            haul.TickHaul(1f, holding: true);
            haul.TickHaul(1f, holding: true);
            Assert.IsFalse(haul.IsHauling, "the pot surfaced");

            // THE POT IS ABOARD — nothing landed yet; the trap left the world and the save.
            var deck = boatGo.GetComponent<PotDeckWorkController>();
            Assert.IsNotNull(deck, "the haul spawned its deck-work sibling at pot-aboard");
            Assert.IsTrue(deck.HasPotAboard);
            Assert.AreEqual(0, hold.UsedUnits, "the catch rides in the pot, not the hold");
            Assert.AreEqual(0, caught);
            Assert.AreEqual(0, svc.Live.Count);
            Assert.AreEqual(0, _save.PlacedTraps.Count, "no re-haul possible after a reload — no dupes");

            // Let a real frame pass mid-work: the pot survives the frame (transient but stable).
            yield return null;
            Assert.IsTrue(deck.HasPotAboard, "the pot rides the deck across frames");

            // The game derived the same ANIMALS the oracle did — the whole list, in catch order.
            Assert.AreEqual(expectedCount, deck.Pot.Animals.Count, "all the pot's animals came aboard");
            for (int i = 0; i < expectedCount; i++)
            {
                DeckPot.Animal animal = deck.Pot.Animals[i];
                Assert.AreEqual(expected[i].SpeciesId, animal.Item.SpeciesId, $"animal {i}: WHAT was caught is untouched");
                Assert.AreEqual(expected[i].WeightKg, animal.Item.WeightKg, 0f, $"animal {i}: the resolved weight untouched");
                Assert.AreEqual(expectedSize[i], animal.SizeMm, 0f, $"animal {i}: size matches the pure-stream oracle");
                Assert.AreEqual(expectedBerried[i], animal.Berried, $"animal {i}: berried flag matches");
                Assert.AreEqual(expectedKeeper[i], animal.Keeper, $"animal {i}: the sort verdict is the deterministic one");
            }

            // PICK the pot empty (full, careful holds — nips are off, so one grab per animal). Each
            // clean grab sorts on the spot: keepers wait on the deck row, shorts/berried splash back.
            for (int grabs = 0; grabs < expectedCount; grabs++)
            {
                Assert.AreEqual(expectedCount - grabs, deck.Pot.InPotCount, "one animal leaves the pot per grab");
                Work(deck, 1.0f);
            }
            Assert.AreEqual(0, deck.Pot.InPotCount, "picked out");
            Assert.AreEqual(expectedKeepers, deck.Pot.OnDeckCount, "the keepers wait on the deck, unbanded");
            Assert.AreEqual(expectedCount - expectedKeepers, deck.Pot.ReturnedCount,
                "the honest fishery returned every short/berried hen");
            Assert.AreEqual(0, hold.UsedUnits, "nothing counts until it's banded");
            Assert.AreEqual(0, caught);

            // BAND every waiting keeper (only banded keepers count — the unchanged FishCaught path).
            for (int bands = 0; bands < expectedKeepers; bands++)
                Work(deck, 1.0f);
            Assert.AreEqual(0, deck.Pot.OnDeckCount, "every keeper banded");
            Assert.AreEqual(expectedKeepers, hold.UsedUnits, "every banded keeper landed in the hold");
            Assert.AreEqual(expectedKeepers, caught, "one FishCaught per keeper — counts match the oracle");
            for (int i = 0; i < hold.UsedUnits; i++)
                Assert.AreEqual("fish.lobster", hold.Items[i].SpeciesId,
                    "the landed items ARE the resolved catch — zero economy change");

            // RE-BAIT (consumes bait #2) → READY → T-set her again, pre-baited (no third bait).
            Assert.IsTrue(deck.Pot.NeedsBait);
            Work(deck, 1.2f);
            Assert.IsTrue(deck.Pot.ReadyToSet, "baited by hand on the deck");
            Assert.AreEqual(0, BaitCount(_save, bait.Id), "the re-bait consumed the second (last) bait");

            var dev = boatGo.AddComponent<DevTrapInput>();
            dev.Configure(svc, boatGo.transform, trap, bait, "region.st_peters");
            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            Assert.AreEqual(PlacedTrapService.PlaceResult.Placed, dev.DropTrap(), "T sets the worked pot");

            EventBus.Unsubscribe<FishCaught>(OnCaught);

            Assert.IsFalse(deck.HasPotAboard, "the deck is squared away");
            Assert.AreEqual(1, svc.Live.Count, "she soaks again — the loop closed");
            Assert.AreEqual(1, _save.PlacedTraps.Count, "and the save mirrors the new set");
            Assert.AreEqual(0, BaitCount(_save, bait.Id), "the pre-baited set charged nothing extra");

            yield return null;
        }
    }
}
