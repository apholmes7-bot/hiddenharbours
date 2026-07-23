using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Rod Fishing v2 Wave 3 — the FULL deep→surface fight wired through <see cref="FishingController"/>
    /// (design §3, §5, §7). End-to-end, seeded, dt-injected (headless frames are not time): a species
    /// that OPTED INTO a <see cref="RodFightDef"/> runs Bite → strike window → FightDeep → FightSurface
    /// → Landed/Snapped; a species WITHOUT a Def keeps the legacy single-phase fight untouched. Pins the
    /// cozy guarantees (competent hand lands + exactly one item; blind pin snaps + costs nothing), the
    /// published Core reads (fish offset, rod bend, the fight's slack tell), seeded determinism through
    /// the whole controller, and the per-personality arc for each starter movement pattern.
    /// </summary>
    public class RodFightControllerTests
    {
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int Capacity = 6;
            public int CapacityUnits => Capacity;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item)
            {
                if (_items.Count >= Capacity) return false;
                _items.Add(item);
                return true;
            }
            public void Clear() => _items.Clear();
        }

        private readonly List<Object> _spawned = new();
        private readonly List<FishingState> _published = new();
        private int _fishCaught;

        private void OnFishCaught(FishCaught e) => _fishCaught++;
        private void OnState(FishingStateChanged e) => _published.Add(e.State);

        [SetUp]
        public void SetUp()
        {
            _fishCaught = 0;
            _published.Clear();
            EventBus.Clear<FishCaught>();
            EventBus.Clear<FishingStateChanged>();
            EventBus.Subscribe<FishCaught>(OnFishCaught);
            EventBus.Subscribe<FishingStateChanged>(OnState);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<FishCaught>(OnFishCaught);
            EventBus.Unsubscribe<FishingStateChanged>(OnState);
            EventBus.Clear<FishCaught>();
            EventBus.Clear<FishingStateChanged>();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- rig ----------------------------------------------------------------------------

        private RodFightDef MakeRodFight(RodFightMovement pattern, float strength = 0.5f,
                                         float surfaceAt = 0.5f)
        {
            var def = ScriptableObject.CreateInstance<RodFightDef>();
            def.Id = $"rodfight.test_{pattern.ToString().ToLowerInvariant()}";
            def.MovementPattern = pattern;
            def.Strength = strength;
            def.StaminaCadence = new StaminaCadence { RunSeconds = 1.6f, SlackSeconds = 1.2f, Jitter01 = 0.3f };
            def.tensionRisePerSec = 0.65f;
            def.tensionFallPerSec = 0.7f;
            def.landingFillPerSec = 0.32f;
            def.runTensionPressure = 0.3f;
            def.counterSteerRelief = 0.45f;
            def.surfaceThreshold01 = surfaceAt;
            _spawned.Add(def);
            return def;
        }

        private FishSpeciesDef MakeFish(string id, RodFightDef rodFight)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = id; f.Category = FishCategory.InshoreGroundfish;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.AllowedGear = Gear.Handline | Gear.Longline;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 6f;
            f.BaseValue = 12; f.SupplyElasticity = 0.2f; f.SpawnWeight = 1f;
            f.RodFight = rodFight;
            _spawned.Add(f);
            return f;
        }

        private FishingController MakeController(IHold hold, FishSpeciesDef[] pool, int seed)
        {
            var go = new GameObject("RodFighter");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            c.Configure(hold, pool, "region.coddle_cove", Gear.Handline, seed);
            return c;
        }

        /// <summary>Flick a line out and advance to the first v2 fight phase (auto-hook covers the
        /// strike window).</summary>
        private static void AdvanceIntoRodFight(FishingController c, float maxSeconds = 30f)
        {
            FlickGestures.CastLine(c);
            for (float t = 0f; c.Phase != FishingPhase.FightDeep && t < maxSeconds; t += 0.05f)
                c.Tick(0.05f, false);
        }

        private static bool IsResult(FishingPhase p)
            => p == FishingPhase.Landed || p == FishingPhase.Snapped || p == FishingPhase.NoBite
               || p == FishingPhase.Idle;

        /// <summary>The competent hand, driven purely off the PUBLISHED Core state (what a player can
        /// see): pull in the slack tell while tension is low, ease through runs, steer the pointer
        /// OPPOSITE the published fish offset once she's up. Ticks until a result or the cap.</summary>
        private void PlayCompetent(FishingController c, float maxSeconds = 180f)
        {
            Vector2 prevOffset = default;
            bool havePrev = false;
            for (float t = 0f; !IsResult(c.Phase) && t < maxSeconds; t += 0.02f)
            {
                FishingState s = c.State;
                bool reel = s.SlackWindowOpen && s.Tension01 < 0.6f;
                // Steer against her DART, read the honest way a player reads it: watch the published
                // far end move, and put the pointer opposite that motion. When she holds still there
                // is nothing to counter — pointer down (neutral).
                var offset = new Vector2(s.FishOffsetX, s.FishOffsetY);
                Vector2 delta = havePrev ? offset - prevOffset : Vector2.zero;
                prevOffset = offset; havePrev = s.Phase == FishingPhase.FightSurface;
                bool steerable = s.Phase == FishingPhase.FightSurface && delta.sqrMagnitude > 1e-8f;
                Vector2 pointer = (Vector2)c.transform.position - delta.normalized * 2f;
                c.Tick(0.02f, reel, steerable ? pointer : default, steerable);
            }
        }

        // ---- the arc, per personality --------------------------------------------------------

        [TestCase(RodFightMovement.Darter)]
        [TestCase(RodFightMovement.Bulldog)]
        [TestCase(RodFightMovement.Circler)]
        [TestCase(RodFightMovement.Thrasher)]
        public void EveryPersonality_RunsTheFullArc_AndLandsToACompetentHand(RodFightMovement pattern)
        {
            var hold = new FakeHold();
            var fish = MakeFish("fish.v2", MakeRodFight(pattern));
            var c = MakeController(hold, new[] { fish }, seed: 4242);

            AdvanceIntoRodFight(c);
            Assert.AreEqual(FishingPhase.FightDeep, c.Phase,
                $"{pattern}: a Def'd species opens the fight DEEP (timing only)");

            PlayCompetent(c);
            Assert.AreEqual(FishingPhase.Landed, c.Phase, $"{pattern}: pulse-and-steer must land her");
            Assert.AreEqual(1, hold.UsedUnits, "landing adds exactly one item");
            Assert.AreEqual(1, _fishCaught, "exactly one FishCaught");

            // The arc actually happened: both fight phases were published, Deep strictly first.
            int firstDeep = _published.FindIndex(s => s.Phase == FishingPhase.FightDeep);
            int firstSurface = _published.FindIndex(s => s.Phase == FishingPhase.FightSurface);
            Assert.GreaterOrEqual(firstDeep, 0, "FightDeep must publish");
            Assert.GreaterOrEqual(firstSurface, 0, "FightSurface must publish");
            Assert.Less(firstDeep, firstSurface, "deep first, then she breaks the surface");
            Assert.IsFalse(_published.FindIndex(firstSurface, s => s.Phase == FishingPhase.FightDeep) >= 0,
                "the crossing is one-way — no publish ever falls back to FightDeep");
        }

        [Test]
        public void BlindPin_ThrowsTheHook_CostsCatchAndTimeOnly()
        {
            var hold = new FakeHold();
            var fish = MakeFish("fish.v2", MakeRodFight(RodFightMovement.Darter));
            var c = MakeController(hold, new[] { fish }, seed: 4242);

            AdvanceIntoRodFight(c);
            for (float t = 0f; !IsResult(c.Phase) && t < 60f; t += 0.02f)
                c.Tick(0.02f, true);   // pin the reel, no pointer

            Assert.AreEqual(FishingPhase.Snapped, c.Phase, "a pinned reel must part the line");
            Assert.AreEqual(0, hold.UsedUnits, "cozy: a snap costs the catch only — nothing gained");
            Assert.AreEqual(0, _fishCaught);
            // …and the interaction resets cleanly: nothing lingers, recast at will.
            for (float t = 0f; c.Phase != FishingPhase.Idle && t < 10f; t += 0.05f) c.Tick(0.05f, false);
            Assert.AreEqual(FishingPhase.Idle, c.Phase, "cozy fail always returns to Idle");
        }

        // ---- the published Core reads --------------------------------------------------------

        [Test]
        public void FightPublishes_TheDiegeticReads_AndNeutralOutsideTheFight()
        {
            var hold = new FakeHold();
            var fish = MakeFish("fish.v2", MakeRodFight(RodFightMovement.Circler, surfaceAt: 0.35f));
            var c = MakeController(hold, new[] { fish }, seed: 11);

            AdvanceIntoRodFight(c);
            PlayCompetent(c);
            Assert.AreEqual(FishingPhase.Landed, c.Phase);

            bool sawBend = false, sawSlackTell = false, sawSurfaceOffsetMove = false;
            Vector2 deepOffset = default;
            bool haveDeepOffset = false;
            foreach (FishingState s in _published)
            {
                if (s.Phase == FishingPhase.FightDeep || s.Phase == FishingPhase.FightSurface)
                {
                    if (s.RodBend01 > 0f) sawBend = true;
                    if (s.SlackWindowOpen) sawSlackTell = true;
                    if (s.Phase == FishingPhase.FightDeep)
                    {
                        // Deep: the far end is the world-fixed entry anchor (the line runs straight
                        // down there) — every deep publish reads the same offset (the angler is still).
                        if (!haveDeepOffset) { deepOffset = new Vector2(s.FishOffsetX, s.FishOffsetY); haveDeepOffset = true; }
                        else Assert.AreEqual(deepOffset, new Vector2(s.FishOffsetX, s.FishOffsetY),
                            "deep publishes anchor the line at the fixed entry point");
                    }
                    else if (new Vector2(s.FishOffsetX, s.FishOffsetY) != deepOffset)
                    {
                        sawSurfaceOffsetMove = true;   // her darts move the line's far end (design §3)
                    }
                }
                else
                {
                    Assert.AreEqual(0f, s.RodBend01, $"{s.Phase}: rod bend is a fight-only read");
                    Assert.AreEqual(0f, s.FishOffsetX, $"{s.Phase}: fish offset is a fight-only read");
                    Assert.AreEqual(0f, s.FishOffsetY, $"{s.Phase}: fish offset is a fight-only read");
                }
            }
            Assert.IsTrue(haveDeepOffset, "deep publishes carry the entry anchor");
            Assert.IsTrue(sawBend, "her runs must show in the published rod bend");
            Assert.IsTrue(sawSlackTell, "the fight's slack windows must publish the PULL-now tell");
            Assert.IsTrue(sawSurfaceOffsetMove, "on the surface the line's far end moves with her");
        }

        // ---- determinism through the whole controller ---------------------------------------

        [Test]
        public void SameSeed_SameInputs_ReplayTheWholeInteraction()
        {
            List<(FishingPhase, float, float, float, float)> Run()
            {
                _published.Clear();
                var hold = new FakeHold();
                var fish = MakeFish("fish.v2", MakeRodFight(RodFightMovement.Thrasher));
                var c = MakeController(hold, new[] { fish }, seed: 90210);
                AdvanceIntoRodFight(c);
                PlayCompetent(c);
                var trace = new List<(FishingPhase, float, float, float, float)>();
                foreach (FishingState s in _published)
                    trace.Add((s.Phase, s.Tension01, s.Landing01, s.FishOffsetX, s.FishOffsetY));
                return trace;
            }

            var first = Run();
            var second = Run();
            Assert.AreEqual(first.Count, second.Count, "the two seeded runs must publish in lockstep");
            for (int i = 0; i < first.Count; i++)
                Assert.AreEqual(first[i], second[i], $"published state diverged at publish {i}");
        }

        // ---- the legacy path (a species WITHOUT a Def — untouched) --------------------------

        [Test]
        public void SpeciesWithoutADef_NeverEntersTheV2Fight_AndPublishesNeutralV2Reads()
        {
            var hold = new FakeHold();
            var fish = MakeFish("fish.legacy", rodFight: null);
            var c = MakeController(hold, new[] { fish }, seed: 999);

            FlickGestures.CastLine(c);
            for (float t = 0f; !IsResult(c.Phase) && t < 120f; t += 0.05f)
                c.Tick(0.05f, c.State.Tension01 < 0.5f);   // the pre-wave forgiving pulse

            Assert.AreEqual(FishingPhase.Landed, c.Phase, "the legacy fight still lands as it always did");
            foreach (FishingState s in _published)
            {
                Assert.AreNotEqual(FishingPhase.FightDeep, s.Phase, "no Def → never the v2 arc");
                Assert.AreNotEqual(FishingPhase.FightSurface, s.Phase, "no Def → never the v2 arc");
                Assert.AreEqual(0f, s.RodBend01, "the legacy fight's reads stay neutral (contract)");
                Assert.IsFalse(s.SlackWindowOpen && s.Phase == FishingPhase.Fighting,
                    "the legacy fight never signals the fish-slack tell");
                Assert.AreEqual(0f, s.FishOffsetX);
                Assert.AreEqual(0f, s.FishOffsetY);
            }
        }

        [Test]
        public void HandGatheredSpecies_KeepTending_EvenWithAStrayDef()
        {
            // A Def on a clam is an authoring mistake — the hand-gather must never grow a snap.
            var hold = new FakeHold();
            var crab = MakeFish("fish.crab", MakeRodFight(RodFightMovement.Thrasher));
            crab.Category = FishCategory.Shellfish;
            var c = MakeController(hold, new[] { crab }, seed: 5);

            FlickGestures.CastLine(c);
            for (float t = 0f; c.Phase != FishingPhase.Tending && !IsResult(c.Phase) && t < 30f; t += 0.05f)
                c.Tick(0.05f, false);
            Assert.AreEqual(FishingPhase.Tending, c.Phase, "hand-gathered categories always tend");
        }
    }
}
