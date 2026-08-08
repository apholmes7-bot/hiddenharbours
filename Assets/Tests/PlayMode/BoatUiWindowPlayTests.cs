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
    /// The boat-UI windowing (the owner's 2026-08-07 ruling) end to end, in a live scene: hide-all
    /// standing every card down and bringing them back, the flush faces following the HELM window,
    /// an expanded instrument keeping its own window, a dragged/collapsed dash actually moving the
    /// published card rect, and the whole thing changing NOTHING while the state is untouched.
    ///
    /// <para>PlayMode rather than pure because the thing that can break is the AGREEMENT between
    /// self-installing hosts that cannot see each other — the helm publishing its (windowed) card,
    /// the sounder mounting into it, the registry gating them both — in execution order. The
    /// geometry itself is EditMode-pinned (<c>BoatUiWindowTests</c>). Scaffolding mirrors
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
        /// stripped rig spawns them). Two frames so the helm publishes and the sounder mounts.</summary>
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
            yield return null;                       // Awake + OnEnable have run
            go.GetComponent<HelmControlRelay>().DevIgnoreEquipmentGating = false;
            boat.SetHull(NewConsoleHull("boat.window_test"));
            InstrumentLocker.Add(_save.Current, "boat.window_test", BoatEquipment.DepthSounderId);
            yield return null;                       // the helm publishes its card
            yield return null;                       // …and the sounder mounts into it
        }

        [UnityTest]
        public IEnumerator UntouchedState_ChangesNothing_TheDashAndFlushMountStand()
        {
            yield return RigConsoleBoat();

            Assert.That(HelmOverlayHost.Instance.CardKind,
                        Is.EqualTo(HelmOverlayHost.HelmCardKind.Dash), "premise: a dash is up");
            Assert.That(HelmOverlayHost.TryDashCard(out Rect card, out _), Is.True);
            Assert.That(card.width, Is.GreaterThan(0f));
            Assert.That(SounderOverlayHost.Instance.Showing, Is.True);
            Assert.That(SounderOverlayHost.Instance.FlushMounted, Is.True,
                        "default window state = the shipped S4.5 behaviour, untouched");
        }

        [UnityTest]
        public IEnumerator HideAll_StandsEveryCardDown_AndTheToggleBringsThemBack()
        {
            yield return RigConsoleBoat();

            BoatUiWindows.ToggleHideAll();
            yield return null;
            Assert.That(HelmOverlayHost.TryDashCard(out _, out _), Is.False,
                        "hide-all: the dash is gone (and with it the brow publication)");
            Assert.That(HelmOverlayHost.Instance.CardKind,
                        Is.EqualTo(HelmOverlayHost.HelmCardKind.None));
            Assert.That(SounderOverlayHost.Instance.Showing, Is.False,
                        "…and the flush sounder went with it");

            BoatUiWindows.ToggleHideAll();
            yield return null;
            yield return null;   // helm republishes, then the sounder remounts
            Assert.That(HelmOverlayHost.TryDashCard(out _, out _), Is.True, "one toggle restores");
            Assert.That(SounderOverlayHost.Instance.Showing, Is.True);
            Assert.That(SounderOverlayHost.Instance.FlushMounted, Is.True);
        }

        [UnityTest]
        public IEnumerator HidingTheHelmWindow_TakesTheFlushFaceWithIt_ButNotAnExpandedInstrument()
        {
            yield return RigConsoleBoat();

            BoatUiWindows.SetHidden(BoatUiWindowId.Helm, true);
            yield return null;
            Assert.That(SounderOverlayHost.Instance.Showing, Is.False,
                        "the flush face is bolted to the dash — it never pops out to a card the " +
                        "player did not ask for");

            // An EXPANDED instrument is its own window and stands on its own.
            HelmInstrumentExpansion.Click(HelmInstrumentSlot.Sounder);
            yield return null;
            Assert.That(SounderOverlayHost.Instance.Showing, Is.True);
            Assert.That(SounderOverlayHost.Instance.Expanded, Is.True);
            Assert.That(SounderOverlayHost.Instance.FlushMounted, Is.False);
        }

        [UnityTest]
        public IEnumerator HidingTheInstrumentWindow_ClosesItsExpansion_RatherThanStrandingIt()
        {
            yield return RigConsoleBoat();

            HelmInstrumentExpansion.Click(HelmInstrumentSlot.Sounder);
            yield return null;
            Assert.That(SounderOverlayHost.Instance.Expanded, Is.True, "premise");

            BoatUiWindows.SetHidden(BoatUiWindowId.Sounder, true);
            yield return null;
            Assert.That(SounderOverlayHost.Instance.Showing, Is.False);
            Assert.That(HelmInstrumentExpansion.AnyExpanded, Is.False,
                        "an invisible card must not keep owning the pointer and Esc");
        }

        [UnityTest]
        public IEnumerator ADraggedDash_MovesThePublishedCard_AndTheFlushMountFollows()
        {
            yield return RigConsoleBoat();

            Assert.That(HelmOverlayHost.TryDashCard(out Rect before, out _), Is.True);

            BoatUiWindowState st = BoatUiWindows.State(BoatUiWindowId.Helm);
            st.HasCustomCenter = true;
            st.Center01 = new Vector2(0.25f, 0.6f);
            BoatUiWindows.SetState(BoatUiWindowId.Helm, in st);
            yield return null;

            Assert.That(HelmOverlayHost.TryDashCard(out Rect after, out HelmFit fit), Is.True);
            Assert.That(after.center.x, Is.Not.EqualTo(before.center.x).Within(1f),
                        "the published dash card moved with the window");
            // The clamp may bite at the band/screen edges, so pin direction rather than exact centre.
            Assert.That(after.center.x, Is.LessThan(before.center.x),
                        "…toward the port quarter it was dragged to");

            // The flush mount is computed FROM the published card, so it followed for free.
            Assert.That(HelmInstrumentMountLayout.TryBrowSounderRect(in fit, after, out Rect mount),
                        Is.True);
            Assert.That(mount.xMin, Is.GreaterThanOrEqualTo(after.xMin - 0.5f));
            Assert.That(mount.xMax, Is.LessThanOrEqualTo(after.xMax + 0.5f));
            Assert.That(SounderOverlayHost.Instance.FlushMounted, Is.True);
        }

        [UnityTest]
        public IEnumerator ABarCollapsedDash_FoldsItsBrowAway_AndCompactKeepsIt()
        {
            yield return RigConsoleBoat();

            BoatUiWindowState st = BoatUiWindows.State(BoatUiWindowId.Helm);
            st.Collapse = BoatUiCollapse.Bar;
            BoatUiWindows.SetState(BoatUiWindowId.Helm, in st);
            yield return null;
            Assert.That(HelmOverlayHost.TryDashCard(out _, out _), Is.False,
                        "BAR: no dash picture, no brow publication");
            Assert.That(SounderOverlayHost.Instance.Showing, Is.False,
                        "…and the flush face folded away with it");

            st.Collapse = BoatUiCollapse.Compact;
            BoatUiWindows.SetState(BoatUiWindowId.Helm, in st);
            yield return null;
            yield return null;
            Assert.That(HelmOverlayHost.TryDashCard(out Rect compact, out _), Is.True,
                        "COMPACT still draws the dash");
            Assert.That(SounderOverlayHost.Instance.FlushMounted, Is.True);
            Assert.That(compact.width, Is.GreaterThan(0f));
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

        [UnityTest]
        public IEnumerator LosingTheHelm_LeavesTheWindowStateAlone_ForTheNextBoat()
        {
            yield return RigConsoleBoat();

            BoatUiWindowState st = BoatUiWindows.State(BoatUiWindowId.Helm);
            st.HasCustomCenter = true;
            st.Center01 = new Vector2(0.3f, 0.5f);
            BoatUiWindows.SetState(BoatUiWindowId.Helm, in st);
            yield return null;

            GameServices.HelmControl = null;   // stepped ashore
            yield return null;
            Assert.That(HelmOverlayHost.Instance.CardKind,
                        Is.EqualTo(HelmOverlayHost.HelmCardKind.None));
            Assert.That(BoatUiWindows.State(BoatUiWindowId.Helm).HasCustomCenter, Is.True,
                        "the layout is session state — going ashore does not reset where the " +
                        "player parked their helm");
        }
    }
}
