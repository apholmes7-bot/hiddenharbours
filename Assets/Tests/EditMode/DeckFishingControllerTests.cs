using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Rod Fishing v2 WAVE 4 — the deck-angle fight term wired through <see cref="FishingController"/>
    /// (design §4.2, the owner's locked "light real factor"): a fight fought off a published
    /// <see cref="DeckStance"/> reads the angler's stance against the fish each tick and adds the
    /// across-the-hull pressure to the tension side. Pins the CONTRACTS the wave ships on:
    /// the dock path is bit-for-bit unchanged (no stance ⇒ literal 0), the owner's off-switch is exact
    /// (factor 0 ⇒ bit-for-bit dock-parity even with a stance live), a clean rail grades exactly 0, a
    /// bad stance genuinely loads the line (a seeded pull snaps sooner), and every spatial read
    /// measures from the angler's LIVE transform (the moving-platform beat, decision #3).
    /// Seeded + dt-injected throughout (headless frames are not time).
    /// </summary>
    public class DeckFishingControllerTests
    {
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 6;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private readonly List<Object> _spawned = new();
        private readonly List<FishingState> _published = new();
        private GameObject _stanceOwner;   // the DeckStance publisher handle (cleared per test)

        private void OnState(FishingStateChanged e) => _published.Add(e.State);

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();                     // open water: casts land as flicked, no licence gate
            _published.Clear();
            EventBus.Clear<FishingStateChanged>();
            EventBus.Subscribe<FishingStateChanged>(OnState);
            _stanceOwner = new GameObject("DeckStanceOwner");
            _spawned.Add(_stanceOwner);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<FishingStateChanged>(OnState);
            EventBus.Clear<FishingStateChanged>();
            DeckStance.Clear(_stanceOwner);
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- rig (the RodFightControllerTests harness + a GameConfig with the deck factor) ------

        private RodFightDef MakeRodFight()
        {
            var def = ScriptableObject.CreateInstance<RodFightDef>();
            def.Id = "rodfight.test_deck";
            def.MovementPattern = RodFightMovement.Darter;
            def.Strength = 0.5f;
            def.StaminaCadence = new StaminaCadence { RunSeconds = 1.6f, SlackSeconds = 1.2f, Jitter01 = 0.3f };
            def.tensionRisePerSec = 0.65f;
            def.tensionFallPerSec = 0.7f;
            def.landingFillPerSec = 0.32f;
            def.runTensionPressure = 0.3f;
            def.counterSteerRelief = 0.45f;
            def.surfaceThreshold01 = 0.5f;
            _spawned.Add(def);
            return def;
        }

        private FishSpeciesDef MakeFish()
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = "fish.deck_v2"; f.DisplayName = "Deck Test Fish"; f.Category = FishCategory.InshoreGroundfish;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.AllowedGear = Gear.Handline | Gear.Longline;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 6f;
            f.BaseValue = 12; f.SupplyElasticity = 0.2f; f.SpawnWeight = 1f;
            f.RodFight = MakeRodFight();
            _spawned.Add(f);
            return f;
        }

        private FishingController MakeController(int seed, float deckAngleFactor)
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            var fight = cfg.RodFight;                 // keep every other default; dial only the deck factor
            fight.DeckAngleFactor = deckAngleFactor;
            cfg.RodFight = fight;
            _spawned.Add(cfg);

            var go = new GameObject("DeckFisher");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            c.Configure(new FakeHold(), new[] { MakeFish() }, "region.coddle_cove", Gear.Handline, seed,
                        licenses: null, config: cfg);
            return c;
        }

        // The greybox dory deck (DeckWalkController defaults). FlickGestures cast due NORTH of the
        // angler, so the stance is set by where the hull sits around the origin-standing angler:
        //   WORST — hull centred 1.5 m north: the angler stands at the stern, the fish lands beyond the
        //           bow, the line runs virtually the whole deck (across ≈ 0.97);
        //   CLEAN — hull centred 1.6 m south: the angler stands exactly ON the bow rail the line leaves
        //           over (across = 0 exactly — the bit-for-bit parity stance).
        private static readonly Vector2 DeckHalf = new Vector2(0.7f, 1.6f);
        private void PublishWorstStance()
            => DeckStance.Publish(_stanceOwner, new DeckStanceState(new Vector2(0f, 1.5f), 0f, Vector2.zero, DeckHalf));
        private void PublishCleanStance()
            => DeckStance.Publish(_stanceOwner, new DeckStanceState(new Vector2(0f, -1.6f), 0f, Vector2.zero, DeckHalf));

        private static void AdvanceIntoRodFight(FishingController c, float maxSeconds = 30f)
        {
            FlickGestures.CastLine(c);
            for (float t = 0f; c.Phase != FishingPhase.FightDeep && t < maxSeconds; t += 0.05f)
                c.Tick(0.05f, false);
        }

        private static bool IsResult(FishingPhase p)
            => p == FishingPhase.Landed || p == FishingPhase.Snapped || p == FishingPhase.NoBite
               || p == FishingPhase.Idle;

        /// <summary>Fight the whole interaction with a fixed, deterministic policy (pull only under
        /// low tension in the published slack tell — no pointer) and return the publish trace.</summary>
        private List<(FishingPhase, float, float)> RunTrace(int seed, float factor, System.Action stance)
        {
            _published.Clear();
            var c = MakeController(seed, factor);
            stance?.Invoke();
            AdvanceIntoRodFight(c);
            for (float t = 0f; !IsResult(c.Phase) && t < 180f; t += 0.02f)
                c.Tick(0.02f, c.State.SlackWindowOpen && c.State.Tension01 < 0.6f);
            var trace = new List<(FishingPhase, float, float)>();
            foreach (FishingState s in _published)
                trace.Add((s.Phase, s.Tension01, s.Landing01));
            return trace;
        }

        private static void AssertTracesIdentical(List<(FishingPhase, float, float)> a,
                                                  List<(FishingPhase, float, float)> b, string why)
        {
            Assert.AreEqual(a.Count, b.Count, why + " (publish counts differ)");
            for (int i = 0; i < a.Count; i++)
                Assert.AreEqual(a[i], b[i], $"{why} — diverged at publish {i}");
        }

        // ---- the contracts -----------------------------------------------------------------------

        [Test]
        public void NoStance_TheDockPath_IsBitForBit_WhateverTheFactor()
        {
            // Off a boat there IS no stance — the dock/shore fight must integrate a literal 0 whatever
            // the owner dialled, so cranking DeckAngleFactor can never leak into the dock checkpoint.
            var offFactor = RunTrace(seed: 777, factor: 0f, stance: null);
            var bigFactor = RunTrace(seed: 777, factor: 0.9f, stance: null);
            AssertTracesIdentical(offFactor, bigFactor, "the dock path must not read the deck factor");
        }

        [Test]
        public void FactorZero_IsTheOwnersOffSwitch_DockParityEvenOnDeck()
        {
            // The owner's dock-parity check: stand at the WORST stance with the factor at 0 and the
            // fight is bit-for-bit the dock fight.
            var dock = RunTrace(seed: 4242, factor: 0f, stance: null);
            var deckOff = RunTrace(seed: 4242, factor: 0f, stance: PublishWorstStance);
            AssertTracesIdentical(dock, deckOff, "factor 0 must be exact dock-parity");
        }

        [Test]
        public void CleanRail_GradesExactlyZero_BitForBitTheDockFight()
        {
            // Standing ON the rail the line leaves over: across = 0 exactly, so even with the term live
            // the integration is bit-identical — walking to the clean angle FULLY relieves the pressure.
            var dock = RunTrace(seed: 90210, factor: 0.3f, stance: null);
            var cleanRail = RunTrace(seed: 90210, factor: 0.3f, stance: PublishCleanStance);
            AssertTracesIdentical(dock, cleanRail, "the clean rail must read zero pressure");
        }

        [Test]
        public void WorstStance_LoadsTheLine_ASeededBlindPullSnapsSooner()
        {
            int TicksToResult(System.Action stance, out FishingPhase result)
            {
                var c = MakeController(seed: 1313, deckAngleFactor: 0.3f);
                stance?.Invoke();
                AdvanceIntoRodFight(c);
                int ticks = 0;
                while (!IsResult(c.Phase) && ticks < 30000) { c.Tick(0.02f, true); ticks++; }   // blind pull
                result = c.Phase;
                return ticks;
            }

            int dockTicks = TicksToResult(null, out FishingPhase dockResult);
            DeckStance.Clear(_stanceOwner);
            int deckTicks = TicksToResult(PublishWorstStance, out FishingPhase deckResult);

            Assert.AreEqual(FishingPhase.Snapped, dockResult, "a blind pull snaps on the dock (invariant 1)");
            Assert.AreEqual(FishingPhase.Snapped, deckResult, "…and still snaps on deck (the term only ADDS tension)");
            Assert.Less(deckTicks, dockTicks,
                "the worst stance must genuinely load the line — the same seeded blind pull parts sooner " +
                "when the whole hull lies between the angler and the fish");
        }

        [Test]
        public void EverySpatialRead_FollowsTheAnglersLiveTransform_TheMovingPlatform()
        {
            // Decision #3 (the moving platform): the deck drifts/weathervanes DURING the fight, carrying
            // the angler — so the published line geometry must be measured from the live transform every
            // tick, never cached. Move the angler mid-fight (as the swinging deck would) and the
            // published far-end offset must shift by exactly the opposite of the move, this very tick.
            var c = MakeController(seed: 55, deckAngleFactor: 0f);
            var anglerGo = new GameObject("DeckAngler");
            _spawned.Add(anglerGo);
            anglerGo.transform.position = Vector3.zero;
            c.Angler = anglerGo.transform;

            AdvanceIntoRodFight(c);
            Assert.AreEqual(FishingPhase.FightDeep, c.Phase);

            c.Tick(0.02f, false);
            Vector2 before = new Vector2(c.State.FishOffsetX, c.State.FishOffsetY);

            anglerGo.transform.position = new Vector3(2f, 0f, 0f);   // the deck carries the angler abeam
            c.Tick(0.02f, false);
            Vector2 after = new Vector2(c.State.FishOffsetX, c.State.FishOffsetY);

            // Deep phase: the far end is anchored in the WORLD, so walking the deck 2 m to starboard
            // swings the published line angle by −2 m. The tolerance covers the fish's own drift over the
            // single tick between the two reads — deep, the entry point now works around as she runs
            // (owner's ruling 2026-07-23), so it is no longer pinned to the metre.
            Assert.AreEqual(before.x - 2f, after.x, 0.25f, "the line angle must track the live angler (x)");
            Assert.AreEqual(before.y, after.y, 0.25f, "the line angle must track the live angler (y)");
        }

        [Test]
        public void TheWholeDeckFight_StaysWinnable_AndSeededDeterministic()
        {
            // The cozy floor: the worst stance is PRESSURE, not a wall — the same pull-in-the-slack hand
            // that wins the dock fight still wins it fought entirely from the worst stance (the shipped
            // guard-rail keeps a maintain net-negative, so tension is always recoverable)…
            var first = RunTrace(seed: 2026, factor: 0.15f, stance: PublishWorstStance);
            Assert.IsTrue(first.Exists(s => s.Item1 == FishingPhase.Landed),
                "the deck fight at the shipped factor must still be winnable by the ordinary pulse");

            // …and the deck term keeps the seeded-replay contract (nothing hidden, nothing cached).
            DeckStance.Clear(_stanceOwner);
            var second = RunTrace(seed: 2026, factor: 0.15f, stance: PublishWorstStance);
            AssertTracesIdentical(first, second, "a seeded deck fight must replay bit-for-bit");
        }
    }
}
