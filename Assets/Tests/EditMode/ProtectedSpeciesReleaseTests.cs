using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Owner ruling 2026-11-15, pinned: "agreed they can be documented but must be released". A protected
    /// species is landed and documented exactly as a kept catch is (same item, same landing cue), then
    /// released: it never enters a hold, a pail, a freezer, a hand or a restored save. A species without
    /// the flag behaves exactly as before.
    /// </summary>
    public class ProtectedSpeciesReleaseTests
    {
        private const string ProtectedId = "fish.test_protected";
        private const string OpenId = "fish.test_open";

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

        private sealed class FakeConservation : IFishConservationFacts
        {
            private readonly HashSet<string> _protected;
            public FakeConservation(params string[] ids) { _protected = new HashSet<string>(ids); }
            public bool IsProtected(string fishId) => fishId != null && _protected.Contains(fishId);
        }

        private sealed class FakeFactory : ICatchItemFactory
        {
            public bool TryCreate(string speciesId, out CatchItem item)
            {
                item = Item(speciesId, 1f);
                return true;
            }
        }

        private sealed class StampClock : IGameClock
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

        private readonly List<Object> _spawned = new();
        private readonly List<CatchItem> _caught = new();
        private readonly List<CatchItem> _released = new();
        private readonly List<float> _landingCues = new();
        private int _inHand;

        private void OnCaught(FishCaught e) => _caught.Add(e.Item);
        private void OnReleased(CatchReleased e) => _released.Add(e.Item);
        private void OnLandedInHand(CatchLanded e) => _inHand++;
        private void OnCue(JuiceMomentCue e) { if (e.Kind == JuiceMoment.Landing) _landingCues.Add(e.Strength); }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            FishSpeciesRegistry.Reset();
            _caught.Clear(); _released.Clear(); _landingCues.Clear(); _inHand = 0;
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchReleased>();
            EventBus.Clear<CatchLanded>();
            EventBus.Clear<JuiceMomentCue>();
            EventBus.Clear<FishingStateChanged>();
            EventBus.Subscribe<FishCaught>(OnCaught);
            EventBus.Subscribe<CatchReleased>(OnReleased);
            EventBus.Subscribe<CatchLanded>(OnLandedInHand);
            EventBus.Subscribe<JuiceMomentCue>(OnCue);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<FishCaught>(OnCaught);
            EventBus.Unsubscribe<CatchReleased>(OnReleased);
            EventBus.Unsubscribe<CatchLanded>(OnLandedInHand);
            EventBus.Unsubscribe<JuiceMomentCue>(OnCue);
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchReleased>();
            EventBus.Clear<CatchLanded>();
            EventBus.Clear<JuiceMomentCue>();
            EventBus.Clear<FishingStateChanged>();
            GameServices.Reset();
            FishSpeciesRegistry.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- fixtures -----------------------------------------------------------------------------

        private static CatchItem Item(string id, float kg)
            => new CatchItem(id, id, FishCategory.InshoreGroundfish, kg, 12, 0.2f, 0f, Freshness.Landed(0d));

        private void ProtectInRegistry() => GameServices.FishConservation = new FakeConservation(ProtectedId);

        private FishSpeciesDef MakeFish(string id, bool isProtected)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = id; f.Category = FishCategory.InshoreGroundfish;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.AllowedGear = Gear.Handline | Gear.Longline;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 6f;
            f.BaseValue = 12; f.SupplyElasticity = 0.2f; f.SpawnWeight = 1f;
            f.Protected = isProtected;
            _spawned.Add(f);
            return f;
        }

        /// <summary>The FishingControllerTests harness: flick-cast, auto-hook, a forgiving pulse to the end.</summary>
        private FishingController LandOne(IHold hold, FishSpeciesDef fish)
        {
            var go = new GameObject("Fisher");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            c.Configure(hold, new[] { fish }, "region.coddle_cove", Gear.Handline, 999);

            FlickGestures.CastLine(c);
            float t = 0f;
            while (c.Phase != FishingPhase.Fighting && c.Phase != FishingPhase.Tending && t < 30f)
            {
                c.Tick(0.05f, false);
                t += 0.05f;
            }
            t = 0f;
            while (c.Phase != FishingPhase.Landed && c.Phase != FishingPhase.Snapped
                   && c.Phase != FishingPhase.NoBite && c.Phase != FishingPhase.Idle && t < 120f)
            {
                c.Tick(0.05f, actionHeld: c.State.Tension01 < 0.5f);
                t += 0.05f;
            }
            return c;
        }

        // ---- the pure rule ------------------------------------------------------------------------

        [Test]
        public void CatchKeep_refuses_only_a_protected_species()
        {
            var facts = new FakeConservation(ProtectedId);
            Assert.IsFalse(CatchKeep.MayKeep(ProtectedId, facts));
            Assert.IsTrue(CatchKeep.MayKeep(OpenId, facts), "an unflagged species is kept as before");
            Assert.IsTrue(CatchKeep.MayKeep(ProtectedId, null), "no facts registered: nothing is refused");
            Assert.IsTrue(CatchKeep.MayKeep(null, facts), "no id: nothing known to refuse");
        }

        [Test]
        public void CatchReleaseRule_reads_the_Def_flag_and_the_shared_facts()
        {
            Assert.IsTrue(CatchReleaseRule.MustRelease(MakeFish(ProtectedId, true), null),
                "the Def's own flag releases even with no registry");
            Assert.IsFalse(CatchReleaseRule.MustRelease(MakeFish(OpenId, false), new FakeConservation(ProtectedId)));
            Assert.IsTrue(CatchReleaseRule.MustRelease(MakeFish(ProtectedId, false), new FakeConservation(ProtectedId)),
                "the shared facts release too, so the landing and the containers agree");
            Assert.IsFalse(CatchReleaseRule.MustRelease(null, new FakeConservation(ProtectedId)));
        }

        [Test]
        public void FishSpeciesDef_defaults_to_not_protected()
        {
            Assert.IsFalse(MakeFishDefault().Protected, "a new Def is not protected unless the owner says so");
        }

        private FishSpeciesDef MakeFishDefault()
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            _spawned.Add(f);
            return f;
        }

        // ---- the rod choke point -------------------------------------------------------------------

        [Test]
        public void Rod_protected_catch_is_documented_exactly_as_a_kept_one_then_released()
        {
            // The same seed and the same species facts, once unflagged and once flagged: the documented
            // item must match the kept one (species, weight), and nothing may be kept.
            var keptHold = new FakeHold();
            FishingController kept = LandOne(keptHold, MakeFish(ProtectedId, false));
            Assert.AreEqual(FishingPhase.Landed, kept.Phase, "baseline: the paced fight lands");
            Assert.AreEqual(1, keptHold.UsedUnits);
            Assert.AreEqual(1, _caught.Count);
            CatchItem keptItem = _caught[0];
            float keptCue = _landingCues.Count == 1 ? _landingCues[0] : -1f;
            Assert.AreEqual(0, _released.Count, "an unflagged species is never released");

            _caught.Clear(); _landingCues.Clear();
            var hold = new FakeHold();
            FishingController rod = LandOne(hold, MakeFish(ProtectedId, true));

            Assert.AreEqual(FishingPhase.Landed, rod.Phase, "the fight is still won and shown as landed");
            Assert.AreEqual(0, hold.UsedUnits, "a protected catch never enters the hold");
            Assert.AreEqual(0, _caught.Count, "FishCaught means a hold took it: never raised");
            Assert.AreEqual(0, _inHand, "CatchLanded means it is in her hands: never raised");
            Assert.AreEqual(1, _released.Count, "documented exactly once");
            Assert.AreEqual(keptItem.SpeciesId, _released[0].SpeciesId);
            Assert.AreEqual(keptItem.WeightKg, _released[0].WeightKg, 0f, "documented at the same size");
            Assert.AreEqual(1, _landingCues.Count, "the same landing beat plays");
            Assert.AreEqual(keptCue, _landingCues[0], 0f);
        }

        // ---- every guarded entry point --------------------------------------------------------------

        [Test]
        public void ShipHold_refuses_a_protected_catch()
        {
            ProtectInRegistry();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.HoldUnits = 10;
            _spawned.Add(hull);
            var go = new GameObject("Boat");
            _spawned.Add(go);
            var hold = go.AddComponent<ShipHold>();
            hold.SetHull(hull);

            Assert.IsFalse(hold.TryAdd(Item(ProtectedId, 2f)));
            Assert.IsTrue(hold.TryAdd(Item(OpenId, 2f)));
            Assert.AreEqual(1, hold.UsedUnits);
        }

        [Test]
        public void ClamBucket_refuses_a_protected_catch()
        {
            ProtectInRegistry();
            var go = new GameObject("Pail");
            _spawned.Add(go);
            var pail = go.AddComponent<ClamBucket>();
            pail.Configure(20, requireOwnedBucket: false);

            Assert.IsFalse(pail.TryAdd(Item(ProtectedId, 0.1f)));
            Assert.IsTrue(pail.TryAdd(Item(OpenId, 0.1f)));
            Assert.AreEqual(1, pail.UsedUnits);
        }

        [Test]
        public void CarriableBucket_refuses_a_protected_catch()
        {
            ProtectInRegistry();
            var go = new GameObject("CarriedPail");
            _spawned.Add(go);
            var pail = go.AddComponent<CarriableBucket>();
            pail.Configure(20, go.AddComponent<SpriteRenderer>(), "container.test.protected");

            Assert.IsFalse(pail.TryAdd(Item(ProtectedId, 0.1f)));
            Assert.IsTrue(pail.TryAdd(Item(OpenId, 0.1f)));
            Assert.AreEqual(1, pail.UsedUnits);
        }

        [Test]
        public void Freezer_refuses_a_protected_catch()
        {
            ProtectInRegistry();
            GameServices.Clock = new StampClock { TotalSeconds = 100d };
            var go = new GameObject("Freezer");
            _spawned.Add(go);
            var freezer = go.AddComponent<GinnyFreezer>();

            Assert.IsFalse(freezer.TryAdd(Item(ProtectedId, 2f)));
            Assert.IsTrue(freezer.TryAdd(Item(OpenId, 2f)));
            Assert.AreEqual(1, freezer.UsedUnits);
        }

        [Test]
        public void Hands_refuse_a_protected_catch()
        {
            ProtectInRegistry();
            CarryHands hands = ToolInHand.Empty(_spawned);

            Assert.IsFalse(hands.TryPutInHand(Item(ProtectedId, 0.5f)), "a protected catch never goes in a hand");
            Assert.IsFalse(hands.IsCarrying);
            Assert.IsTrue(hands.TryPutInHand(Item(OpenId, 0.5f)), "an unflagged catch still does");
        }

        [Test]
        public void Save_restore_skips_a_protected_species()
        {
            ProtectInRegistry();
            var records = new List<HeldCatchDto>
            {
                new HeldCatchDto { SpeciesId = ProtectedId, Quantity = 2 },
                new HeldCatchDto { SpeciesId = OpenId, Quantity = 1 },
            };
            var into = new List<CatchItem>();

            int restored = HoldCatchCodec.Restore(records, new FakeFactory(), into);

            Assert.AreEqual(1, restored, "a species flagged after the save goes back in the water");
            Assert.AreEqual(OpenId, into[0].SpeciesId);
            Assert.AreEqual(2, _released.Count, "each dropped unit is reported as a release, not a lost id");
            Assert.AreEqual(ProtectedId, _released[0].SpeciesId);
        }

        [Test]
        public void Save_restore_pure_overload_reports_protected_units_as_released()
        {
            // No registered service: the facts are handed in, and nothing is published.
            var records = new List<HeldCatchDto>
            {
                new HeldCatchDto { SpeciesId = ProtectedId, Quantity = 3 },
                new HeldCatchDto { SpeciesId = OpenId, Quantity = 2 },
            };
            var into = new List<CatchItem>();
            var released = new List<CatchItem> { Item(OpenId, 9f) };   // stale entry: cleared first

            int restored = HoldCatchCodec.Restore(records, new FakeFactory(), new FakeConservation(ProtectedId),
                                                  into, released);

            Assert.AreEqual(2, restored);
            Assert.AreEqual(2, into.Count);
            Assert.AreEqual(3, released.Count, "every protected unit comes back as a release");
            foreach (CatchItem r in released) Assert.AreEqual(ProtectedId, r.SpeciesId);
            Assert.AreEqual(0, _released.Count, "the pure overload publishes nothing");

            int all = HoldCatchCodec.Restore(records, new FakeFactory(), null, into, released);
            Assert.AreEqual(5, all, "null facts drop nothing");
            Assert.AreEqual(0, released.Count);
        }

        // ---- the pot on the deck (the existing short/berried return path, extended) ----------------

        private DeckPot MakePot(params CatchItem[] items)
        {
            var d = ScriptableObject.CreateInstance<DeckWorkDef>();
            d.Id = "deckwork.test"; d.DisplayName = "Test deck work";
            d.QuickGrabSeconds = 0.15f; d.FullGrabSeconds = 0.9f;
            d.NipChanceRushed01 = 0f; d.NipChanceCareful01 = 0f;
            d.SpeciesRules = new[]
            {
                new SpeciesDeckRule
                {
                    SpeciesId = ProtectedId, MinKeepSizeMm = 0f, SizeMinMm = 62f, SizeMaxMm = 140f,
                    CanBeBerried = false, BerriedChance01 = 0f, Shape = DeckAnimalShape.Lobster,
                },
            };
            _spawned.Add(d);
            var t = ScriptableObject.CreateInstance<TrapDef>();
            t.Id = "trap.test"; t.DisplayName = "Test Pot";
            t.AllowedCatchFishIds = new[] { ProtectedId, OpenId };
            t.DeckWork = d;
            _spawned.Add(t);
            return new DeckPot(t, d, "trap.test#1", 1000.0, 4242, items);
        }

        [Test]
        public void DeckPot_returns_a_protected_animal_even_at_keeper_size()
        {
            ProtectInRegistry();
            DeckPot pot = MakePot(Item(ProtectedId, 0.9f), Item(OpenId, 0.9f));

            Assert.IsTrue(pot.Animals[0].Protected);
            Assert.IsFalse(pot.Animals[0].Keeper, "the gauge says keeper; protection says back she goes");
            Assert.IsTrue(pot.Animals[1].Keeper, "an unflagged species with no rule stays an always-keeper");

            Assert.AreEqual(GrabOutcome.ReturnedProtected, pot.TryGrabNext(1f, out DeckPot.Animal a));
            Assert.AreEqual(DeckAnimalFate.Returned, a.Fate);
            Assert.AreEqual(GrabOutcome.Keeper, pot.TryGrabNext(1f, out _));
        }

        [Test]
        public void DeckPot_closed_season_and_protection_both_hold()
        {
            // Both legal rules at once: an unflagged species out of season still goes back as out of
            // season; a protected one goes back as protected, whatever the season says.
            ProtectInRegistry();
            DeckPot seed = MakePot();
            DeckWorkDef def = seed.Def;
            const string port = "region.test_port";
            def.Seasons = new[] { ClosedSeason(ProtectedId, port), ClosedSeason(OpenId, port) };
            var pot = new DeckPot(seed.Trap, def, "trap.test#2", 1000.0, 4242,
                                  new[] { Item(ProtectedId, 0.9f), Item(OpenId, 0.9f) }, port,
                                  new SeasonDate(Season.HardWinter, 3, GameConfig.DefaultDaysPerSeason));

            Assert.IsTrue(pot.Animals[0].Protected);
            Assert.IsFalse(pot.Animals[0].Keeper);
            Assert.IsTrue(pot.Animals[1].OutOfSeason, "the season rule still applies to an unflagged species");
            Assert.IsFalse(pot.Animals[1].Keeper);
            Assert.AreEqual(GrabOutcome.ReturnedProtected, pot.TryGrabNext(1f, out _));
            Assert.AreEqual(GrabOutcome.ReturnedOutOfSeason, pot.TryGrabNext(1f, out _));
        }

        private FishingSeasonDef ClosedSeason(string speciesId, string region)
        {
            var s = ScriptableObject.CreateInstance<FishingSeasonDef>();
            s.Id = "season.test_" + speciesId;
            s.SpeciesId = speciesId;
            s.RegionId = region;
            s.OpenSeason = Season.EarlySpring; s.OpenDayOfSeason = 15;
            s.CloseSeason = Season.HighSummer; s.CloseDayOfSeason = 21;
            _spawned.Add(s);
            return s;
        }

        [Test]
        public void DeckPot_auto_resolve_never_stows_a_protected_animal()
        {
            ProtectInRegistry();
            DeckPot pot = MakePot(Item(ProtectedId, 0.9f), Item(OpenId, 0.9f));
            var hold = new FakeHold();
            var landed = new List<CatchItem>();

            pot.AutoResolve(hold, landed, out int kept, out int returned, out int noRoom);

            Assert.AreEqual(1, kept);
            Assert.AreEqual(1, returned);
            Assert.AreEqual(0, noRoom);
            Assert.AreEqual(1, hold.UsedUnits);
            Assert.AreEqual(OpenId, hold.Items[0].SpeciesId);
        }

        [Test]
        public void DeckPot_auto_resolve_hands_back_the_protected_animal_as_released()
        {
            ProtectInRegistry();
            DeckPot pot = MakePot(Item(ProtectedId, 0.9f), Item(OpenId, 0.9f));
            var hold = new FakeHold();
            var released = new List<CatchItem>();

            pot.AutoResolve(hold, null, released, out int kept, out int returned, out _);

            Assert.AreEqual(1, kept);
            Assert.AreEqual(1, returned);
            Assert.AreEqual(1, released.Count, "the automatic sort reports the release, like the hand grab");
            Assert.AreEqual(ProtectedId, released[0].SpeciesId);
        }

        [Test]
        public void Pot_sort_reads_the_Def_flag_when_the_service_is_missing()
        {
            // The registry knows the Def is protected, but no Core facts are registered.
            FishSpeciesRegistry.Register(MakeFish(ProtectedId, true));
            GameServices.FishConservation = null;

            Assert.IsTrue(CatchReleaseRule.MustRelease(ProtectedId, null));
            Assert.IsFalse(CatchReleaseRule.MustRelease(OpenId, null), "an unknown id with no facts is kept");

            DeckPot pot = MakePot(Item(ProtectedId, 0.9f), Item(OpenId, 0.9f));
            Assert.IsTrue(pot.Animals[0].Protected, "a missing service never makes a protected animal a keeper");
            Assert.IsFalse(pot.Animals[0].Keeper);
            Assert.IsTrue(pot.Animals[1].Keeper);
        }

        // ---- content: existing assets keep their meaning --------------------------------------------

        [Test]
        public void Every_fish_asset_without_the_field_reads_not_protected()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(FishSpeciesDef));
            Assert.IsNotEmpty(guids, "the slice ships fish");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(path);
                Assert.IsNotNull(def, path);
                bool authored = File.ReadAllText(path).Contains("\n  Protected:");
                if (!authored)
                    Assert.IsFalse(def.Protected, $"{path}: an asset written before the field must read not protected");
                if (def.Protected)
                    Assert.IsTrue(CatchReleaseRule.MustRelease(def, null), $"{path}: a flagged asset is release-only");
            }
        }
    }
}
