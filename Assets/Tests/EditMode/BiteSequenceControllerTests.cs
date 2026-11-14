using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
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
    /// shipped — and the switchable bait rule: OFF spends at the bite, ON (the live asset) spends
    /// only on the landed catch.
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

        private sealed class FakeHands : ICatchHands
        {
            public bool Accept = true;
            public int Landed;
            public bool TryPutInHand(in CatchItem item)
            {
                if (!Accept) return false;
                Landed++;
                return true;
            }
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
                                                 GameConfig config = null, LicenseDef[] licenses = null)
        {
            var go = new GameObject("Fisher");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            c.Configure(hold, new[] { fish }, "region.coddle_cove", Gear.Handline, seed, licenses, config);
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

        // ---- bait-spend policy: both modes remain supported ---------------------------------------

        private (FishingController c, SaveData save) BaitedRig(bool spendOnCatchOnly, int seed,
            int stock = 1, FishSpeciesDef fish = null, IHold hold = null, LicenseDef[] licenses = null)
        {
            var save = SaveMigration.NewGame();
            TackleBox.AddBait(save, "bait.herring", stock);
            GameServices.Save = new FakeSaveService(save);

            var bait = ScriptableObject.CreateInstance<BaitDef>();
            bait.Id = "bait.herring";
            _spawned.Add(bait);

            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.BaitSpentOnCatchOnly = spendOnCatchOnly;
            _spawned.Add(config);

            var c = MakeController(hold ?? new FakeHold(), fish ?? MakeFish(MakeBite(0, 2f, 1f, 3)),
                seed, config, licenses);
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
                "OFF preserves bite-time spending even when the fish is not landed");
        }

        [Test]
        public void FlagOn_LastBaitSurvivesTheBite_SpendsOnlyOnTheLandedCatch()
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
                "the landed catch spends the last bait");
        }

        [Test]
        public void ShippedConfig_EnablesCatchOnly_WhileNewConfigsKeepTheOffFallback()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(shipped);
            Assert.IsTrue(shipped.BaitSpentOnCatchOnly, "the authored live policy is ON");
            var fresh = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(fresh);
            Assert.IsFalse(fresh.BaitSpentOnCatchOnly, "the C# compatibility default stays OFF");
        }

        private FishSpeciesDef FishForFight(bool rodFight, bool biteSequence = true)
        {
            var fish = MakeFish(biteSequence ? MakeBite(1, 2f, 1f, 3) : null);
            if (rodFight)
            {
                var fight = ScriptableObject.CreateInstance<RodFightDef>();
                _spawned.Add(fight);
                fight.Strength = 0.5f;
                fight.StaminaCadence = new StaminaCadence { RunSeconds = 1.6f, SlackSeconds = 1.2f, Jitter01 = 0.3f };
                fight.tensionRisePerSec = 0.65f;
                fight.tensionFallPerSec = 0.7f;
                fight.landingFillPerSec = 0.32f;
                fight.runTensionPressure = 0.3f;
                fight.lineGiveBackPerSec = 0f; // plumbing fixture; give-back is covered by RodFightSimTests
                fish.RodFight = fight;
            }
            return fish;
        }

        private static void Hook(FishingController c, bool rodFight, bool autoHook = false)
        {
            FlickGestures.CastLine(c);
            TickUntil(c, FishingPhase.Bite);
            if (autoHook) TickUntil(c, rodFight ? FishingPhase.FightDeep : FishingPhase.Fighting);
            else c.Tick(0.05f, true);
            Assert.AreEqual(rodFight ? FishingPhase.FightDeep : FishingPhase.Fighting, c.Phase);
        }

        private static void FinishFight(FishingController c, bool win)
        {
            for (float t = 0f; t < 180f &&
                (c.Phase == FishingPhase.Fighting || c.Phase == FishingPhase.FightDeep ||
                 c.Phase == FishingPhase.FightSurface); t += 0.02f)
            {
                bool reel = !win || (c.State.Tension01 < 0.5f &&
                    (c.Phase == FishingPhase.Fighting || c.State.SlackWindowOpen));
                c.Tick(0.02f, reel);
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void MissedSequence_ChargesOnlyInOffMode(bool catchOnly, bool strikeTease)
        {
            var hold = new FakeHold();
            var fish = MakeFish(MakeBite(1, 0.4f, 1f, 2));
            var (c, save) = BaitedRig(catchOnly, 42, stock: 3, fish: fish, hold: hold);
            FlickGestures.CastLine(c);
            TickUntil(c, FishingPhase.BiteNibble);
            if (strikeTease) c.Tick(0.05f, true);
            TickUntil(c, FishingPhase.NoBite);
            Assert.AreEqual(catchOnly ? 3 : 2, TackleBox.BaitCount(save, "bait.herring"),
                "returning passes must not charge again; ON makes both early and late misses free");
            Assert.AreEqual(0, hold.UsedUnits);
            TickUntil(c, FishingPhase.Idle);
            Assert.AreEqual(catchOnly ? 3 : 2, TackleBox.BaitCount(save, "bait.herring"));
        }

        [Test]
        public void LostFight_ChargesOnlyInOffMode([Values] bool catchOnly, [Values] bool rodFight)
        {
            var hold = new FakeHold();
            var (c, save) = BaitedRig(catchOnly, 42, stock: 3, fish: FishForFight(rodFight), hold: hold);
            Hook(c, rodFight);
            FinishFight(c, win: false);
            Assert.AreEqual(FishingPhase.Snapped, c.Phase);
            Assert.AreEqual(0, hold.UsedUnits);
            Assert.AreEqual(catchOnly ? 3 : 2, TackleBox.BaitCount(save, "bait.herring"));
            TickUntil(c, FishingPhase.Idle);
            Assert.AreEqual(catchOnly ? 3 : 2, TackleBox.BaitCount(save, "bait.herring"));
        }

        [Test]
        public void LandedCatch_SpendsExactlyOne_AcrossFightAndDestinationPaths(
            [Values] bool catchOnly, [Values] bool rodFight, [Values] bool biteSequence,
            [Values("hand", "boat", "full-hands")] string destination)
        {
            var hold = new FakeHold();
            var hands = new FakeHands { Accept = destination != "full-hands" };
            GameServices.CatchHands = hands;
            var (c, save) = BaitedRig(catchOnly, 42, stock: 3,
                fish: FishForFight(rodFight, biteSequence), hold: hold);
            if (destination == "boat") c.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            int caught = 0;
            EventBus.Subscribe<FishCaught>(_ => caught++);
            Hook(c, rodFight, autoHook: !biteSequence);
            Assert.AreEqual(catchOnly ? 3 : 2, TackleBox.BaitCount(save, "bait.herring"));
            FinishFight(c, win: true);
            Assert.AreEqual(FishingPhase.Landed, c.Phase);
            Assert.AreEqual(2, TackleBox.BaitCount(save, "bait.herring"),
                "spare stock exposes a double charge; landing cannot be free either");
            Assert.AreEqual(destination == "hand" ? 1 : 0, hands.Landed);
            Assert.AreEqual(destination == "hand" ? 0 : 1, hold.UsedUnits);
            Assert.AreEqual(destination == "hand" ? 0 : 1, caught);
            TickUntil(c, FishingPhase.Idle);
            c.Tick(1f, false);
            Assert.AreEqual(2, TackleBox.BaitCount(save, "bait.herring"), "result/reset must not charge again");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnlicensedRelease_ChargesOnlyInOffMode(bool catchOnly)
        {
            var hold = new FakeHold();
            var fish = FishForFight(rodFight: false);
            var licence = ScriptableObject.CreateInstance<LicenseDef>();
            _spawned.Add(licence);
            licence.Id = "license.cod";
            licence.PermittedSpeciesIds = new[] { fish.Id };
            var (c, save) = BaitedRig(catchOnly, 42, stock: 3, fish: fish, hold: hold,
                licenses: new[] { licence });
            Hook(c, rodFight: false);
            FinishFight(c, win: true);
            Assert.AreEqual(FishingPhase.Snapped, c.Phase, "a won fight without its licence is released");
            Assert.AreEqual(0, hold.UsedUnits);
            Assert.AreEqual(catchOnly ? 3 : 2, TackleBox.BaitCount(save, "bait.herring"));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void EmptyBaitStock_StillRequiresTackle_AndNeverInventsBait(bool catchOnly, bool lure)
        {
            var hold = new FakeHold();
            var (c, save) = BaitedRig(catchOnly, 42, stock: 0, fish: FishForFight(false), hold: hold);
            if (lure)
            {
                var tackle = ScriptableObject.CreateInstance<TackleDef>();
                _spawned.Add(tackle);
                tackle.Lure = LureTag.Spoon;
                tackle.JigStyle = JigStyle.None;
                c.SetTackle(tackle);
            }
            Assert.AreEqual(lure, c.HasSomethingOnTheHook);
            if (lure)
            {
                Hook(c, rodFight: false);
                FinishFight(c, win: true);
                Assert.AreEqual(FishingPhase.Landed, c.Phase);
                Assert.AreEqual(1, hold.UsedUnits);
            }
            else
            {
                FlickGestures.CastLine(c);
                Assert.AreEqual(FishingPhase.Idle, c.Phase, "an empty configured hook still refuses a cast");
                Assert.AreEqual(0, hold.UsedUnits);
            }
            Assert.AreEqual(0, TackleBox.BaitCount(save, "bait.herring"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NothingBiting_SpendsNothingInEitherMode(bool catchOnly)
        {
            var fish = FishForFight(false);
            fish.RegionIds = new[] { "region.elsewhere" };
            var (c, save) = BaitedRig(catchOnly, 42, stock: 3, fish: fish);
            FlickGestures.CastLine(c);
            TickUntil(c, FishingPhase.NoBite);
            Assert.AreEqual(3, TackleBox.BaitCount(save, "bait.herring"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissThenLand_AndRecast_SpendsExactlyOnePerCatch(bool catchOnly)
        {
            var hold = new FakeHold();
            var (c, save) = BaitedRig(catchOnly, 42, stock: 3, fish: FishForFight(false), hold: hold);
            for (int landed = 1; landed <= 2; landed++)
            {
                FlickGestures.CastLine(c);
                TickUntil(c, FishingPhase.BiteNibble);
                c.Tick(0.05f, true); // miss the tease; this same fish returns
                TickUntil(c, FishingPhase.Bite);
                c.Tick(0.05f, true);
                Assert.AreEqual(FishingPhase.Fighting, c.Phase);
                FinishFight(c, win: true);
                Assert.AreEqual(FishingPhase.Landed, c.Phase);
                Assert.AreEqual(landed, hold.UsedUnits);
                Assert.AreEqual(3 - landed, TackleBox.BaitCount(save, "bait.herring"));
                TickUntil(c, FishingPhase.Idle);
            }
        }
    }
}
