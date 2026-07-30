using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Slice 2 of the minigame lane — the §10.2 bite sequence WIRED into the controller. Pins the
    /// integration contracts: an opted-in species runs teases → the true take on the wire phases
    /// (BiteNibble / Bite), striking a tease never hooks, striking the take starts the fight,
    /// exhausted passes end in NoBite, a species WITHOUT a BiteDef auto-hooks bit-for-bit as
    /// shipped — and the TENTATIVE bait-spend flag: OFF spends at the bite (today), ON spends only
    /// on the landed catch.
    /// </summary>
    public class BiteSequenceControllerTests
    {
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 6;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { if (_items.Count >= 6) return false; _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<FishingStateChanged>();
            EventBus.Clear<FishCaught>();
            GameServices.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<FishingStateChanged>();
            EventBus.Clear<FishCaught>();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- rig ----------------------------------------------------------------------------------

        private BiteDef MakeBite(int nibbles, float window, float returnChance, int maxPasses)
        {
            var b = ScriptableObject.CreateInstance<BiteDef>();
            b.Id = "bite.test";
            b.FalseNibblesMin = nibbles; b.FalseNibblesMax = nibbles;
            b.NibbleGapMinSeconds = 0.5f; b.NibbleGapMaxSeconds = 1f;
            b.NibbleDipSeconds = 0.3f;
            b.TakeWindowSeconds = window;
            b.ReturnChance01 = returnChance;
            b.ReturnDelayMinSeconds = 0.5f; b.ReturnDelayMaxSeconds = 1f;
            b.MaxPasses = maxPasses;
            _spawned.Add(b);
            return b;
        }

        private FishSpeciesDef MakeFish(BiteDef bite)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = "fish.cod"; f.DisplayName = "Cod"; f.Category = FishCategory.InshoreGroundfish;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.AllowedGear = Gear.Handline | Gear.Longline;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 6f;
            f.BaseValue = 12; f.SupplyElasticity = 0.2f; f.SpawnWeight = 1f;
            f.Bite = bite;
            _spawned.Add(f);
            return f;
        }

        private FishingController MakeController(IHold hold, FishSpeciesDef fish, int seed,
                                                 GameConfig config = null)
        {
            var go = new GameObject("Fisher");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            c.Configure(hold, new[] { fish }, "region.coddle_cove", Gear.Handline, seed, null, config);
            return c;
        }

        /// <summary>Tick (no press) until <paramref name="phase"/>; fail rather than stall.</summary>
        private static void TickUntil(FishingController c, FishingPhase phase, float maxSeconds = 60f)
        {
            float t = 0f;
            while (c.Phase != phase && t < maxSeconds) { c.Tick(0.05f, false); t += 0.05f; }
            Assert.AreEqual(phase, c.Phase, $"never reached {phase} (sat in {c.Phase})");
        }

        // ---- the sequence on the wire ---------------------------------------------------------------

        [Test]
        public void OptedIn_TeasesShow_ThenTheTake_AndTheStrikeHooks()
        {
            var c = MakeController(new FakeHold(), MakeFish(MakeBite(1, 1.5f, 1f, 3)), seed: 42);
            FlickGestures.CastLine(c);

            TickUntil(c, FishingPhase.BiteNibble);   // the tease reaches the wire vocabulary
            TickUntil(c, FishingPhase.Bite);         // then the true take opens

            c.Tick(0.05f, actionHeld: true);            // strike inside the window
            Assert.AreEqual(FishingPhase.Fighting, c.Phase,
                "the strike sets the hook and the (legacy, no-RodFightDef) fight begins");
        }

        [Test]
        public void StrikingATease_NeverHooks_SheMayComeBack()
        {
            var c = MakeController(new FakeHold(), MakeFish(MakeBite(1, 1.5f, 1f, 3)), seed: 42);
            FlickGestures.CastLine(c);

            TickUntil(c, FishingPhase.BiteNibble);
            c.Tick(0.05f, actionHeld: true);            // fooled — struck the tease

            Assert.AreNotEqual(FishingPhase.Fighting, c.Phase, "a tease never sets the hook");
            Assert.AreEqual(FishingPhase.Waiting, c.Phase,
                "with her return chance at 1 she circles back — the wire parks on quiet Waiting");

            TickUntil(c, FishingPhase.Bite);         // the second pass reaches its take
            c.Tick(0.05f, actionHeld: true);
            Assert.AreEqual(FishingPhase.Fighting, c.Phase, "the second chance is a real one");
        }

        [Test]
        public void NeverStriking_ExhaustsHerPasses_IntoNoBite()
        {
            var c = MakeController(new FakeHold(), MakeFish(MakeBite(0, 0.4f, 1f, 2)), seed: 7);
            FlickGestures.CastLine(c);

            TickUntil(c, FishingPhase.NoBite, maxSeconds: 120f);
            TickUntil(c, FishingPhase.Idle);         // the result beat eases back to Idle
        }

        [Test]
        public void NoBiteDef_AutoHooks_ExactlyAsShipped()
        {
            var fish = MakeFish(bite: null);
            var c = MakeController(new FakeHold(), fish, seed: 42);
            FlickGestures.CastLine(c);

            // The legacy path must reach the fight with NO press at all (the forgiving auto-hook),
            // and must never pass through the new tease phase.
            float t = 0f;
            bool sawNibble = false;
            while (c.Phase != FishingPhase.Fighting && t < 60f)
            {
                c.Tick(0.05f, false);
                if (c.Phase == FishingPhase.BiteNibble) sawNibble = true;
                t += 0.05f;
            }
            Assert.AreEqual(FishingPhase.Fighting, c.Phase, "auto-hook still hooks with no press");
            Assert.IsFalse(sawNibble, "a species without a BiteDef never plays the nibble game");
        }

        // ---- the TENTATIVE bait-spend flag (§10.2 — economy-sim flagged) ---------------------------

        private (FishingController c, SaveData save) BaitedRig(bool spendOnCatchOnly, int seed)
        {
            var save = SaveMigration.NewGame();
            TackleBox.AddBait(save, "bait.herring", 1);
            GameServices.Save = new FakeSaveService(save);

            var bait = ScriptableObject.CreateInstance<BaitDef>();
            bait.Id = "bait.herring";
            _spawned.Add(bait);

            GameConfig config = null;
            if (spendOnCatchOnly)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                config.BaitSpentOnCatchOnly = true;
                _spawned.Add(config);
            }

            var c = MakeController(new FakeHold(), MakeFish(MakeBite(0, 2f, 1f, 3)), seed, config);
            c.SetBait(bait);
            return (c, save);
        }

        [Test]
        public void FlagOff_BaitSpendsAtTheBite_LandedOrNot()
        {
            var (c, save) = BaitedRig(spendOnCatchOnly: false, seed: 42);
            FlickGestures.CastLine(c);
            TickUntil(c, FishingPhase.Bite);

            Assert.AreEqual(0, TackleBox.BaitCount(save, "bait.herring"),
                "today's behaviour: something ate it — the bait is gone at the bite");
        }

        [Test]
        public void FlagOn_BaitSurvivesTeasesAndMisses_SpendsOnlyOnTheLandedCatch()
        {
            var (c, save) = BaitedRig(spendOnCatchOnly: true, seed: 42);
            FlickGestures.CastLine(c);
            TickUntil(c, FishingPhase.Bite);
            Assert.AreEqual(1, TackleBox.BaitCount(save, "bait.herring"),
                "the take is open and the bait is still yours — nothing is spent at the bite");

            c.Tick(0.05f, actionHeld: true);   // strike → the legacy fight
            Assert.AreEqual(FishingPhase.Fighting, c.Phase);
            Assert.AreEqual(1, TackleBox.BaitCount(save, "bait.herring"), "hooking spends nothing yet");

            float t = 0f;
            while (c.Phase == FishingPhase.Fighting && t < 120f)
            {
                c.Tick(0.05f, actionHeld: c.State.Tension01 < 0.5f);   // the paced, landing pulse
                t += 0.05f;
            }
            Assert.AreEqual(FishingPhase.Landed, c.Phase, "the paced fight lands");
            Assert.AreEqual(0, TackleBox.BaitCount(save, "bait.herring"),
                "…and NOW the bait is gone — 'perhaps bait is only lost after catching a fish'");
        }
    }
}
