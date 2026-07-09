using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Build 5 — the trap-loop keys are gated to the ON-DECK state: T (set a pot), G (dev bait) and
    /// H (start the haul) live only while <see cref="ControlMode.OnDeck"/>, never at the helm and never
    /// on foot; a live haul is cozily dropped when the player leaves the deck (helm or ashore, no
    /// penalty). Also pins the greybox DevNotice toasts the trap loop raises (the owner's on-screen
    /// feedback — Fishing publishes through Core, no UI reference). Driven through the public handlers,
    /// the OwnedFleet convention — no play-mode lifecycle.
    /// </summary>
    public class TrapDeckGatingTests
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

        private readonly List<Object> _spawned = new();
        private readonly List<string> _notices = new();
        private void OnNotice(DevNotice e) => _notices.Add(e.Text);

        private FakeClock _clock;
        private TrapDef _trap;
        private BaitDef _bait;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            FishSpeciesRegistry.Reset();
            EventBus.Clear<DevNotice>();
            _notices.Clear();
            EventBus.Subscribe<DevNotice>(OnNotice);

            _clock = new FakeClock { Seconds = 5000.0 };
            GameServices.Clock = _clock;
            GameServices.Environment = new FakeEnv();
            GameServices.Save = new FakeSaveService(SaveMigration.NewGame());

            _trap = ScriptableObject.CreateInstance<TrapDef>();
            _trap.Id = "trap.lobster"; _trap.DisplayName = "Lobster Pot";
            _trap.AllowedCatchFishIds = new[] { "fish.lobster" };
            _trap.RequiredBaitId = "bait.herring";
            _trap.SoakHours = 12f; _trap.MinSoakDepthMeters = 3f; _trap.MaxSoakDepthMeters = 40f;
            _spawned.Add(_trap);

            _bait = ScriptableObject.CreateInstance<BaitDef>();
            _bait.Id = "bait.herring"; _bait.DisplayName = "Herring";
            _bait.FavorsSpeciesIds = new[] { "fish.lobster" };
            _spawned.Add(_bait);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<DevNotice>(OnNotice);
            EventBus.Clear<DevNotice>();
            InteractionGate.Reset();
            GameServices.Reset();
            FishSpeciesRegistry.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private GameObject NewGo(string name)
        {
            var g = new GameObject(name);
            _spawned.Add(g);
            return g;
        }

        // ---- the gate: gear keys live ONLY on deck --------------------------------------------

        [Test]
        public void DevTrapInput_GearKeys_LiveOnlyOnDeck()
        {
            var dev = NewGo("DevTrap").AddComponent<DevTrapInput>();

            Assert.IsFalse(dev.GearKeysLive, "fresh component starts un-decked (keys dead)");

            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            Assert.IsTrue(dev.GearKeysLive, "ON DECK → the gear keys are live");

            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
            Assert.IsFalse(dev.GearKeysLive, "at the HELM you're steering — gear keys are dead");

            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
            Assert.IsFalse(dev.GearKeysLive, "ashore the gear keys are dead");
        }

        [Test]
        public void DevTrapInput_GearKeys_StandDownUnderAModalDialogue()
        {
            var dev = NewGo("DevTrap").AddComponent<DevTrapInput>();
            dev.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            Assert.IsTrue(dev.GearKeysLive);

            InteractionGate.IsBlocked = true;
            Assert.IsFalse(dev.GearKeysLive, "a modal dialogue owns the keys while up");
            InteractionGate.IsBlocked = false;
            Assert.IsTrue(dev.GearKeysLive, "released with the dialogue");
        }

        [Test]
        public void TrapHaul_GearKeys_LiveOnlyOnDeck()
        {
            var haul = NewGo("TrapHaul").AddComponent<TrapHaulController>();

            Assert.IsFalse(haul.GearKeysLive, "fresh component starts un-decked (keys dead)");

            haul.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            Assert.IsTrue(haul.GearKeysLive, "ON DECK → the haul keys are live");

            haul.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
            Assert.IsFalse(haul.GearKeysLive, "at the HELM the haul keys are dead");
        }

        [Test]
        public void TrapHaul_LeavingTheDeck_DropsALiveHaul_TrapStaysDown()
        {
            // A set trap + a live haul, then the player takes the helm mid-haul: the haul is cozily let
            // go (no penalty) and the trap stays down.
            FishSpeciesRegistry.Register(MakeSpecies());
            var svcGo = NewGo("PlacedTrapService");
            var svc = svcGo.AddComponent<PlacedTrapService>();
            svc.Configure(new[] { _trap }, new[] { _bait }, svcGo.transform);
            svc.PlaceTrap(_trap, _bait, new Vector2(1f, 1f), "region.st_peters");

            var railGo = NewGo("Rail");
            railGo.transform.position = new Vector2(1f, 1f);
            var haul = NewGo("TrapHaul").AddComponent<TrapHaulController>();
            haul.Configure(svc, railGo.transform, null, "region.coddle_cove",
                           maxGainPerPull: 0.5f, pullCooldownSeconds: 0f);

            haul.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            Assert.IsTrue(haul.TryStartHaul(), "a pot alongside starts a haul from the deck");
            Assert.IsTrue(haul.IsHauling);

            haul.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));   // took the helm

            Assert.IsFalse(haul.IsHauling, "leaving the deck lets go of the rope (no penalty)");
            Assert.AreEqual(1, svc.Live.Count, "the trap stays down to keep soaking");
        }

        // ---- the greybox toasts (DevNotice through Core — the owner's on-screen feedback) -------

        [Test]
        public void DropTrap_NothingWired_RaisesADevNotice()
        {
            var dev = NewGo("DevTrap").AddComponent<DevTrapInput>();   // no service/trap wired

            dev.DropTrap();   // logs a wiring WARNING (warnings don't fail the runner) + raises the notice

            Assert.AreEqual(1, _notices.Count, "every placement outcome raises an on-screen notice");
        }

        [Test]
        public void GrantDevSupply_RaisesABaitNotice_WithCountAndName()
        {
            var dev = NewGo("DevTrap").AddComponent<DevTrapInput>();
            dev.Configure(null, null, _trap, _bait, "region.st_peters");

            dev.GrantDevSupply();

            Assert.AreEqual(1, _notices.Count, "the grant shows on screen");
            StringAssert.Contains("Herring", _notices[0], "the notice names the bait");
            StringAssert.Contains("+", _notices[0], "and reads as a grant");
        }

        [Test]
        public void StartHaul_NoPotAlongside_RaisesADevNotice()
        {
            var svcGo = NewGo("PlacedTrapService");
            var svc = svcGo.AddComponent<PlacedTrapService>();
            svc.Configure(new[] { _trap }, new[] { _bait }, svcGo.transform);

            var railGo = NewGo("Rail");
            var haul = NewGo("TrapHaul").AddComponent<TrapHaulController>();
            haul.Configure(svc, railGo.transform, null, "region.coddle_cove");

            Assert.IsFalse(haul.TryStartHaul(), "no pot in reach → no haul");
            Assert.AreEqual(1, _notices.Count, "…and the refusal shows on screen");
        }

        // ---- haul legibility (owner "doesn't know how to haul") — the on-screen cues ---------------

        [Test]
        public void StartHaul_Success_RaisesTheHaulStartNotice()
        {
            var svcGo = NewGo("PlacedTrapService");
            var svc = svcGo.AddComponent<PlacedTrapService>();
            svc.Configure(new[] { _trap }, new[] { _bait }, svcGo.transform);
            svc.PlaceTrap(_trap, _bait, new Vector2(1f, 1f), "region.st_peters");

            var railGo = NewGo("Rail"); railGo.transform.position = new Vector2(1f, 1f);
            var haul = NewGo("TrapHaul").AddComponent<TrapHaulController>();
            haul.Configure(svc, railGo.transform, null, "region.coddle_cove",
                           maxGainPerPull: 0.5f, pullCooldownSeconds: 0f);
            haul.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));

            Assert.IsTrue(haul.TryStartHaul(), "a pot alongside starts the haul");
            Assert.AreEqual(1, _notices.Count, "the haul-start teaches the pull on screen (exactly one notice)");
            StringAssert.Contains("swell", _notices[0], "…and it tells the owner to tap in time with the swell");
        }

        [Test]
        public void EachPull_RaisesExactlyOneNotice_EventTimeNotPerFrame()
        {
            // Null the sea so the swell read is the forgiving calm path (phase 0 → always on the beat), so
            // every pull here is a deterministic CLEAN heave. The point is the per-EVENT contract: one cue
            // per pull, never per frame (the mistimed "Slipping!" branch is the same event-time publish).
            GameServices.Environment = null;

            var svcGo = NewGo("PlacedTrapService");
            var svc = svcGo.AddComponent<PlacedTrapService>();
            svc.Configure(new[] { _trap }, new[] { _bait }, svcGo.transform);
            svc.PlaceTrap(_trap, _bait, new Vector2(1f, 1f), "region.st_peters");

            var railGo = NewGo("Rail"); railGo.transform.position = new Vector2(1f, 1f);
            var haul = NewGo("TrapHaul").AddComponent<TrapHaulController>();
            // Small gain so three pulls don't surface the pot (a surface would add its own notice).
            haul.Configure(svc, railGo.transform, null, "region.coddle_cove",
                           maxGainPerPull: 0.1f, pullCooldownSeconds: 0f);
            haul.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));

            Assert.IsTrue(haul.TryStartHaul());
            _notices.Clear();   // drop the start notice; count only the pulls

            Assert.IsTrue(haul.Pull(), "a calm-sea pull lands on the beat");
            Assert.IsTrue(haul.Pull());
            Assert.IsTrue(haul.Pull());

            Assert.AreEqual(3, _notices.Count, "one on-screen cue per pull — event-time, not per frame");
            foreach (var n in _notices)
                StringAssert.Contains("Heave", n, "a clean on-beat pull reads as a heave");
        }

        private FishSpeciesDef MakeSpecies()
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = "fish.lobster"; f.DisplayName = "Lobster"; f.Category = FishCategory.Shellfish;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.AllowedGear = Gear.Trap; f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 0.5f; f.MaxWeightKg = 1.5f; f.BaseValue = 40; f.SupplyElasticity = 0.2f;
            f.SpawnWeight = 1f;
            _spawned.Add(f);
            return f;
        }
    }
}
