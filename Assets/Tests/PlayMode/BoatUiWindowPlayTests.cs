using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// What is left of the boat-UI windowing (the owner's 2026-08-07 ruling) once the band retired the
    /// dash's and the instruments' windows (H2), end to end in a live scene: the hide-all press (M,
    /// through the HUD's intent seam) standing the band and a raised card down and bringing the band
    /// back, and the watch hiding on its own with the hide-all restore as its recovery.
    ///
    /// <para>PlayMode rather than pure because the thing that can break is the AGREEMENT between
    /// self-installing hosts that cannot see each other — the reader toggling, the helm standing its
    /// band down, the sounder lowering its card — in execution order. Scaffolding mirrors
    /// <see cref="SounderPlayTests"/>.</para>
    /// </summary>
    public class BoatUiWindowPlayTests
    {
        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private sealed class StillClock : IGameClock
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

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private readonly List<Object> _spawned = new();
        private FlatTerrain _terrain;
        private FlatEnv _env;
        private FakeSaveService _save;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            BoatUiWindows.Reset();
            HelmInstrumentExpansion.Collapse();
            _terrain = new FlatTerrain { Elevation = -4f };
            _env = new FlatEnv { Level = 1.2f };
            _save = new FakeSaveService(SaveMigration.NewGame());
            GameServices.TidalTerrain = _terrain;
            GameServices.Environment = _env;
            GameServices.Clock = new StillClock();
            GameServices.Save = _save;
        }

        [TearDown]
        public void TearDown()
        {
            BoatUiWindowInput.ConfigureHudInput(null);
            BoatUiWindows.Reset();
            HelmInstrumentExpansion.Collapse();
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private BoatHullDef NewConsoleHull(string id)
        {
            var console = ScriptableObject.CreateInstance<HelmConsoleDef>();
            console.Id = "helm.window_test_console";
            console.Rig = ConsoleRigKind.Console;
            console.DefaultSounder = SounderKind.Depth;
            console.SupportsFishFinder = true;
            _spawned.Add(console);

            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.DisplayName = "Window Test Hull";
            h.Propulsion = PropulsionType.Engine;
            h.MassKg = 700f;
            h.EnginePower = 650f;
            h.RudderAuthority = 600f;
            h.ForwardDrag = 140f;
            h.LateralDrag = 360f;
            h.WindExposure = 0f;
            h.DraughtMeters = 0.5f;
            h.Helm = console;
            _spawned.Add(h);
            return h;
        }

        /// <summary>A console hull under way with the shipped hosts running (they self-install; a
        /// stripped rig spawns them). Two frames so the helm raises its band and the sounder takes its
        /// housing in it.</summary>
        private IEnumerator RigConsoleBoat()
        {
            if (HelmOverlayHost.Instance == null)
            {
                var hgo = new GameObject("HelmOverlayHost(test)");
                _spawned.Add(hgo);
                hgo.AddComponent<HelmOverlayHost>();
            }
            if (SounderOverlayHost.Instance == null)
            {
                var sgo = new GameObject("SounderOverlayHost(test)");
                _spawned.Add(sgo);
                sgo.AddComponent<SounderOverlayHost>();
            }

            var go = new GameObject("WindowTestBoat");
            var boat = go.AddComponent<BoatController>();   // Awake self-adds the relay
            var col = go.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, 4.0f);
            _spawned.Add(go);
            // ⭐ THE PLAYER IS AT THIS BOAT'S HELM. The Core slot is arbitrated by OCCUPANCY now
            // (HelmSlot) rather than granted to whichever relay enabled last, so the one boat in
            // this fixture has to be DECLARED hers — which is what the fixture always meant.
            GameServices.Helm.SetPilotedHull(boat);
            yield return null;                       // Awake + OnEnable have run
            go.GetComponent<HelmControlRelay>().DevIgnoreEquipmentGating = false;
            boat.SetHull(NewConsoleHull("boat.window_test"));
            InstrumentLocker.Add(_save.Current, "boat.window_test", BoatEquipment.DepthSounderId);
            yield return null;                       // the helm raises its band
            yield return null;                       // …and the sounder takes its housing
        }

        /// <summary>A press of the hide-all through the HUD's seam: held for one frame, then let go
        /// (it is an edge), and one more frame so every host has read the toggle.</summary>
        private static IEnumerator PressHideAll(HeldHudIntents held)
        {
            held.PressHideAll();
            yield return null;                       // the reader takes the press and toggles
            held.Release();
            yield return null;                       // every host has seen the toggle
        }

        [UnityTest]
        public IEnumerator HideAll_StandsEveryCardDown_AndTheToggleBringsThemBack()
        {
            yield return RigConsoleBoat();
            // The press arrives through the HUD's intent seam, where the asset's M arrives (ADR 0043): a
            // PlayMode fixture cannot deliver a key to the Input System, so a held source stands in for
            // the device and drives the REAL reader. A second reader self-destroys if one is running.
            var input = new GameObject("BoatUiWindowInput(test)");
            _spawned.Add(input);
            input.AddComponent<BoatUiWindowInput>();
            var held = new HeldHudIntents();
            BoatUiWindowInput.ConfigureHudInput(held);

            Assert.That(HelmOverlayHost.Instance.CardKind,
                        Is.EqualTo(HelmOverlayHost.HelmCardKind.Band), "premise: the band is up");
            Assert.That(HelmOverlayHost.BandUp, Is.True);
            HelmInstrumentExpansion.Click(HelmInstrumentSlot.Sounder);
            yield return null;
            yield return null;
            Assert.That(SounderOverlayHost.Instance.Expanded, Is.True, "premise: the sounder's card is raised");
            Assert.That(HelmInstrumentExpansion.TryRaisedCard(out _), Is.True);

            int reads = held.Reads;
            yield return PressHideAll(held);
            Assert.That(held.Reads, Is.GreaterThan(reads), "the hide-all read the seam (not a vacuous pass)");
            Assert.That(BoatUiWindows.AllHidden, Is.True, "one press hides all");
            Assert.That(HelmOverlayHost.BandUp, Is.False, "hide-all: the band is gone");
            Assert.That(HelmOverlayHost.Instance.CardKind,
                        Is.EqualTo(HelmOverlayHost.HelmCardKind.None));
            Assert.That(SounderOverlayHost.Instance.Showing, Is.False, "…and the sounder with it");
            Assert.That(HelmInstrumentExpansion.AnyExpanded, Is.False,
                        "the raised card is lowered, not left invisible owning the pointer and Esc");

            yield return PressHideAll(held);
            yield return null;   // the helm raises its band again, then the sounder takes its housing
            Assert.That(BoatUiWindows.AllHidden, Is.False, "the second press restores");
            Assert.That(HelmOverlayHost.BandUp, Is.True, "the band is back");
            Assert.That(SounderOverlayHost.Instance.Showing, Is.True);
            Assert.That(HelmInstrumentExpansion.AnyExpanded, Is.False,
                        "the restore brings the band back, not the card the hide lowered");
        }

        [UnityTest]
        public IEnumerator TheWatch_HidesIndividually_AndTheHideAllRestoreBringsItBack()
        {
            var hudGo = new GameObject("HUD(watch-test)");
            _spawned.Add(hudGo);
            var hud = hudGo.AddComponent<HudController>();   // Awake builds the band + watch
            yield return null;
            yield return null;   // Ready services (SetUp) → UpdateClock paints the face

            var watchField = typeof(HudController).GetField(
                "_watchImage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(watchField, Is.Not.Null, "field '_watchImage' not found on HudController");
            var watch = (UnityEngine.UI.RawImage)watchField.GetValue(hud);
            Assert.That(watch, Is.Not.Null);
            Assert.That(watch.enabled, Is.True, "premise: the face lit once it had a time to show");

            BoatUiWindows.SetHidden(BoatUiWindowId.Watch, true);
            yield return null;
            Assert.That(watch.enabled, Is.False, "the watch hides individually (#447 + 2026-08-07)");

            // The recovery path: hide-all ON then OFF — the restore half clears individual hides.
            BoatUiWindows.ToggleHideAll();
            yield return null;
            Assert.That(watch.enabled, Is.False);
            BoatUiWindows.ToggleHideAll();
            yield return null;
            yield return null;   // the unhide transition forces a repaint, which re-enables the face
            Assert.That(watch.enabled, Is.True,
                        "the hide-all RESTORE brings back an individually hidden watch");
        }
    }
}
