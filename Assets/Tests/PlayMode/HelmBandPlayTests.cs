using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// THE HELM DASH BAND, H2 — <b>the band itself, in real frames</b>: the real helm host, the real
    /// follow camera, the real hide-all reader and the real instrument hosts, with nothing stubbed
    /// between the band and what it drives. The band is drawn and said through the seam, and the camera
    /// eases her up by half of it; M takes it all away and the camera eases back; a raised card sits
    /// where the owner's Q1 answer (B) puts it; the tiller's card stands still; the band's wheel steers
    /// and a key takes the helm back; its ANCH switch works her tackle; and a dome and a scope each have
    /// their own piece.
    ///
    /// <para>The screen is whatever the runner gave: every expectation is computed for it, and nothing
    /// here assumes a size. Waits run on the camera's own clock (<see cref="Time.deltaTime"/>).</para>
    /// </summary>
    public class HelmBandPlayTests
    {
        private const string Region = "region.test_band";
        private static readonly Rect Bounds = new Rect(0f, 0f, 760f, 520f);
        private const string BoatsFolder = "Assets/_Project/Data/Boats";
        // A stalled clock ends the wait; the assertion then says so. By the wall clock, not a frame
        // count: a batch frame can be a quarter of a millisecond, so 2000 of them are half a second.
        private const float StallSeconds = 10f;

        /// <summary>A flat painted bottom: 2 m of water at mean level, where any rode reaches.</summary>
        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation = -2f;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level = 0f;   // mean water
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Vector2.zero, Vector2.zero, Level, SeaState.Calm, 1f, 0f);
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
            public float HourOfDay { get; set; } = 12f;
            public float DayFraction => HourOfDay / 24f;
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

        private sealed class FakeBoatProbe : IActiveBoatService
        {
            public BoatKinematics Value = BoatKinematics.None;
            public bool HasActiveBoat => Value.HasBoat;
            public BoatKinematics Sample() => Value;
        }

        private readonly List<Object> _spawned = new();
        private FakeSaveService _save;
        private Keyboard _testKeyboard;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            BoatUiWindows.Reset();
            BoatUiWindowInput.ConfigureHudInput(null);
            HelmInstrumentExpansion.Collapse();
            HelmKeyCapture.Reset();          // no test may start with the helm deaf
            HelmFootprint.Reset();
            _save = new FakeSaveService(SaveMigration.NewGame());
            GameServices.TidalTerrain = new FlatTerrain();
            GameServices.Environment = new FlatEnv();
            GameServices.Clock = new StillClock();
            GameServices.Save = _save;
            GameServices.ActiveBoat = new FakeBoatProbe
            {
                Value = new BoatKinematics(true, 40f, new Vector2(0f, 3f), 3f),
            };
            GameServices.CurrentRegionId = Region;
            GameServices.CurrentRegionBounds = Bounds;
        }

        [TearDown]
        public void TearDown()
        {
            BoatUiWindowInput.ConfigureHudInput(null);
            BoatUiWindows.Reset();
            HelmInstrumentExpansion.Collapse();
            HelmKeyCapture.Reset();
            GameServices.Reset();
            if (_testKeyboard != null)
            {
                InputSystem.RemoveDevice(_testKeyboard);
                _testKeyboard = null;
            }
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            HelmFootprint.Reset();
        }

        // ---- the band, and the camera making room for it ------------------------------------------

        [UnityTest]
        public IEnumerator TheCape_DrawsItsBand_AndOnceEasedTheBoatSits72kAboveCentre()
        {
            AssertTheScreenHasSize();
            HelmOverlayHost host = Host();
            var (boatGo, boat, _) = NewBoat();
            GameObject target = Spawn("BandTestTarget");   // where she lies; nothing re-frames the view
            target.transform.position = boatGo.transform.position;
            CameraFollow follow = NewCamera(target.transform);
            Camera cam = follow.GetComponent<Camera>();
            yield return null;

            boat.SetHull(NewHull("boat.test_band_cape", NewConsole("band_cape", ConsoleRigKind.Cape)));
            yield return null;
            yield return null;
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Band), "a console hull earns the band");
            HelmBandController band = host.BandController;
            Assert.That(band.Rig, Is.EqualTo(ConsoleRigKind.Cape), "…its own helm's piece list");
            Assert.That(band.PieceCount, Is.EqualTo(HelmBandGeometry.PieceCount(ConsoleRigKind.Cape)));
            for (int i = 0; i < band.PieceCount; i++)
            {
                bool cap = band.Piece(i).Kind == HelmBandPieceKind.Cap;
                Assert.AreEqual(cap, band.TextureOf(i) == null,
                                $"piece {i} ({band.Piece(i).Kind}): textured, unless it is a tinted cap");
            }
            Assert.That(band.UploadCount, Is.GreaterThan(0), "⭐ it drew");
            Assert.IsTrue(HelmOverlayHost.BandUp, "and says it is up");
            Assert.AreEqual(HelmOverlayHost.FootprintOf(HelmOverlayHost.HelmCardKind.Band, default, Screen.height),
                            HelmFootprint.Current, "the seam says the band: the full width, 144k tall");

            HelmFootprintSettings tune = Tuning();
            Assert.AreEqual(0.5f, tune.CameraBandShare, "premise: the ruled share");
            float ortho = cam.orthographicSize;
            // The ease, then long enough for the follow's own lag (Smooth) to close on the goal.
            yield return WaitOnTheCamerasClock(tune.CameraEaseSeconds + 2f);
            yield return null;

            int k = HelmBandLayout.K(Screen.height);
            float sharePx = CameraFollow.BandShiftTargetPx(HelmFootprint.Current, tune.CameraBandShare);
            Assert.AreEqual(72f * k, sharePx, 1e-4f, "⭐ half of the 144k band: 72k screen px");
            Assert.AreEqual(ortho, cam.orthographicSize, "premise: the framing held still throughout");
            float metersPerPx = CameraFollow.MetersPerScreenPixel(false, cam.pixelWidth, cam.pixelHeight, 0, 0,
                                                                  CameraFollow.AssetsPPU, cam.orthographicSize);
            Assert.AreEqual(sharePx * metersPerPx, follow.BandShiftMeters, 1e-5f,
                            "⭐ the whole ease is done: the band's share, in metres by the framing on screen");

            // Where she sits on screen: above centre by the share, to within the follow's pixel snap.
            float abovePx = cam.WorldToScreenPoint(target.transform.position).y - cam.pixelHeight * 0.5f;
            float tolerancePx = follow.WorldUnitsPerRenderedPixel / metersPerPx + 0.5f;
            Assert.AreEqual(sharePx, abovePx, tolerancePx, "⭐ she sits 72k px above the centre of the screen");
        }

        [UnityTest]
        public IEnumerator M_HidesTheBand_AndTheCameraEasesBack()
        {
            AssertTheScreenHasSize();
            HelmOverlayHost host = Host();
            // The reader self-installs in the game; a second one destroys itself, so one reads either way.
            Spawn("BoatUiWindowInput(band test)").AddComponent<BoatUiWindowInput>();
            var held = new HeldHudIntents();
            BoatUiWindowInput.ConfigureHudInput(held);
            var (boatGo, boat, _) = NewBoat();
            GameObject target = Spawn("BandTestTarget");
            target.transform.position = boatGo.transform.position;
            CameraFollow follow = NewCamera(target.transform);
            yield return null;

            boat.SetHull(NewHull("boat.test_band_m", NewConsole("band_m", ConsoleRigKind.Cape)));
            HelmFootprintSettings tune = Tuning();
            yield return WaitOnTheCamerasClock(tune.CameraEaseSeconds + 0.25f);
            yield return null;
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Band), "premise: the band");
            Assert.IsTrue(HelmOverlayHost.BandUp, "premise: it is up");
            Assert.Greater(follow.BandShiftMeters, 0f, "premise: and the camera has made room for it");

            yield return PressHideAll(held);
            Assert.That(held.Reads, Is.GreaterThan(0), "the press was read through the Core intent");
            Assert.IsTrue(BoatUiWindows.AllHidden, "⭐ M hides the helm's UI");
            Assert.IsFalse(HelmOverlayHost.BandUp, "…the band with it");
            Assert.IsTrue(HelmFootprint.Current.IsNone, "…so the seam says nothing is covered");

            yield return WaitOnTheCamerasClock(tune.CameraEaseSeconds + 0.25f);
            yield return null;
            Assert.AreEqual(0f, follow.BandShiftMeters, "⭐ the camera eased back, to exactly nothing");

            yield return PressHideAll(held);
            Assert.IsFalse(BoatUiWindows.AllHidden, "a second M brings it back");
            yield return null;
            Assert.IsTrue(HelmOverlayHost.BandUp, "…the band is up again");
            Assert.AreEqual(HelmFootprintKind.Band, HelmFootprint.Current.Kind, "…and the seam says it again");
        }

        // ---- a raised card: the owner's Q1 answer (B) ---------------------------------------------

        [UnityTest]
        public IEnumerator ARaisedCard_SitsWhereQ1Says()
        {
            AssertTheScreenHasSize();
            HelmOverlayHost host = Host();
            const string hullId = "boat.test_band_raise";
            yield return Aboard(hullId, NewConsole("band_raise", ConsoleRigKind.Cape, supportsRadar: true),
                                BoatEquipment.RadarId);

            HelmFit fit = GameServices.HelmControl.Fit;
            Assert.IsTrue(fit.Radar, "premise: the scope is fitted");
            RadarOverlayHost radar = RadarOverlayHost.Instance;
            Assert.IsNotNull(radar, "premise: the scope's host stands up");
            Assert.IsTrue(radar.Showing, "premise: the scope is presented");
            Assert.IsFalse(radar.Expanded, "as its housing in the band, until it is raised");

            HelmBandController band = host.BandController;
            int ri = HelmBandGeometry.IndexOf(ConsoleRigKind.Cape, HelmBandPieceKind.RadarPlate);
            Assert.GreaterOrEqual(ri, 0, "premise: the Cape's band houses a scope");
            HelmInstrumentSlot slot = band.PlateAt(in fit, band.PieceRect(ri).center);
            Assert.AreEqual(HelmInstrumentSlot.Radar, slot, "a press on the scope's housing is the scope's");
            HelmInstrumentExpansion.Click(slot);
            yield return null;   // one frame to raise, one to draw
            yield return null;
            Assert.IsTrue(radar.Expanded, "raised");
            Assert.IsTrue(HelmInstrumentExpansion.TryRaisedCard(out Rect card), "…and its host drew the card");

            float reservedTop = HudBandLayout.ReservedTopPx();
            Rect expected = HelmBandLayout.RaisedCardRect(RadarRigRender.W, RadarRigRender.H,
                                                          Screen.width, Screen.height, reservedTop);
            AssertRect(expected, card, "⭐ the card sits where Q1 says, on this run's screen");
            float top = Screen.height - reservedTop;
            float bandTop = HelmBandLayout.BandHeightPx(Screen.height);
            Assert.LessOrEqual(card.yMax, top + 0.5f, "never into the top band");
            if (HelmBandLayout.RaisedCardWholeScale(RadarRigRender.W, RadarRigRender.H,
                                                    Screen.width, Screen.height, reservedTop) >= 1)
                Assert.GreaterOrEqual(card.yMin, bandTop - 0.5f,
                                      "a whole scale fits between the bands, so the band stays clear");
            else if (card.height > top - bandTop)
                Assert.Less(card.yMin, bandTop,
                            "⭐ no whole scale fits, so the card keeps today's scale and reaches down over the band");

            HelmInstrumentExpansion.Click(slot);
            yield return null;
            yield return null;
            Assert.IsFalse(radar.Expanded, "a second press on its housing lowers it");
            Assert.IsFalse(HelmInstrumentExpansion.TryRaisedCard(out _), "…and the card is withdrawn");
        }

        // ---- the tiller's card ----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator DoryOutboard_KeepsItsCardFixed_AndTheCameraHoldsStill()
        {
            AssertTheScreenHasSize();
            BoatHullDef dory = LoadCommitted<BoatHullDef>($"{BoatsFolder}/DoryOutboard.asset");
            Assert.IsNull(dory.Helm, "premise: the outboard dory has no console, so she steers by her tiller");
            HelmOverlayHost host = Host();
            var (boatGo, boat, _) = NewBoat();
            GameObject target = Spawn("BandTestTarget");
            target.transform.position = boatGo.transform.position;
            CameraFollow follow = NewCamera(target.transform);
            yield return null;

            boat.SetHull(dory);
            yield return null;
            Rect card = HelmOverlayLayout.CardRect(TillerRigRender.W, TillerRigRender.H,
                                                   GameServices.HelmOverlay, Screen.width, Screen.height);
            HelmFootprintArea said = HelmOverlayHost.FootprintOf(HelmOverlayHost.HelmCardKind.Tiller, card,
                                                                 Screen.height);
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Tiller),
                            $"frame {i}: the tiller's card, not a band");
                Assert.AreEqual(said, HelmFootprint.Current, $"frame {i}: ⭐ the card stands where its layout puts it");
                Assert.AreEqual(0f, follow.BandShiftMeters, $"frame {i}: ⭐ a card never moves the camera");
            }
            Assert.AreEqual(HelmFootprintKind.Card, HelmFootprint.Current.Kind, "a card, not a band");
            Assert.IsFalse(HelmOverlayHost.BandUp, "no band is up");
        }

        // ---- the band's controls --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheBandsWheel_SteersTheHull_AndAKeyPressTakesItBack()
        {
            ConfigWithSteerWind(0.0001f);   // the ease on, but settling within a frame
            HelmOverlayHost host = Host();
            var (boatGo, boat, _) = NewBoat();
            boatGo.AddComponent<DevBoatInput>();                    // the live key layer
            _testKeyboard = InputSystem.AddDevice<Keyboard>();      // headless has no device otherwise
            yield return null;
            boat.SetHull(NewHull("boat.test_band_wheel", NewConsole("band_wheel", ConsoleRigKind.Cape)));
            yield return null;
            yield return null;
            Assert.That(host.CardKind, Is.EqualTo(HelmOverlayHost.HelmCardKind.Band), "premise: the band");
            IHelmControl helm = GameServices.HelmControl;
            HelmBandController band = host.BandController;

            // The rim's top, and a point a quarter-hour clockwise of it, from the hub on the band's bottom
            // edge (piece px, y down, as the band's own geometry measures them).
            Rect wheel = band.PieceRect(HelmBandGeometry.WheelIndex(ConsoleRigKind.Cape));
            int k = band.K;
            float r = (float)WheelRigGeometry.R;
            Vector2 ToScreen(float lx, float ly) => new Vector2(wheel.xMin + lx * k, wheel.yMax - ly * k);
            Vector2 rimTop = ToScreen(HelmBandGeometry.HubX, HelmBandGeometry.HubY - r);
            float a = 30f * Mathf.Deg2Rad;
            Vector2 turned = ToScreen(HelmBandGeometry.HubX + r * Mathf.Sin(a), HelmBandGeometry.HubY - r * Mathf.Cos(a));

            Assert.IsTrue(band.PressControls(helm, rimTop, GameServices.HelmOverlay), "the rim takes the press");
            Assert.IsTrue(band.SteerSession, "…and opens the steer session");
            band.Track(helm, turned, true, 1f / 60f);
            Assert.AreNotEqual(0f, helm.Steer, "⭐ turning the band's wheel steers the hull");
            Assert.AreEqual((float)WheelRigGeometry.SteerFromDeg(band.WheelDeg, GameServices.HelmWheel.Turns),
                            helm.Steer, 1e-4f, "…by the wheel's own angle");
            Assert.IsTrue(helm.SteerDragActive, "the wheel holds the helm");
            yield return null;
            Assert.IsTrue(helm.SteerDragActive, "…across a frame of the key layer running with no key held");

            // Direct state write (not a queued event): an unfocused batchmode app drops keyboard EVENTS
            // for non-background devices, but InputState.Change lands regardless.
            InputState.Change(_testKeyboard, new KeyboardState(Key.A));
            yield return null;
            if (!_testKeyboard.aKey.isPressed)
                Assert.Ignore("virtual key press not deliverable here (unfocused headless input) — the band " +
                              "releasing its session is pinned headless in HelmBandControllerTests");
            yield return null;
            yield return null;   // the key layer, then the band's next paint
            Assert.IsFalse(helm.SteerDragActive, "⭐ a key press takes the helm back");
            Assert.IsFalse(band.SteerSession, "…and the band lets its session go");
            Assert.AreEqual(-1f, boat.Steer, 1e-5f, "the key owns the channel");

            InputState.Change(_testKeyboard, new KeyboardState());   // release
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheBandsAnchorSwitch_FlicksTheTackle()
        {
            HelmOverlayHost host = Host();
            var (boatGo, boat, _) = NewBoat();
            yield return null;
            boat.SetHull(NewHull("boat.test_band_anchor", NewConsole("band_anchor", ConsoleRigKind.Console),
                                 rodeMeters: 6f));
            yield return null;
            yield return null;
            var anchor = boatGo.GetComponent<BoatAnchor>();
            Assert.IsNotNull(anchor, "premise: the boat self-installs her ground tackle");
            IHelmControl helm = GameServices.HelmControl;
            Assert.IsTrue(helm.HasAnchor, "premise: she carries an anchor");
            Assert.AreEqual(AnchorState.Stowed, anchor.State, "premise: stowed");
            HelmBandController band = host.BandController;
            Assert.IsTrue(band.AnchorSwitchShown, "the switch panel carries her live ANCH switch");
            Assert.IsFalse(band.AnchorShown, "…shown up");

            int si = HelmBandGeometry.IndexOf(ConsoleRigKind.Console, HelmBandPieceKind.Switches);
            Assert.GreaterOrEqual(si, 0, "premise: the console's band has its switch panel");
            Assert.IsTrue(HelmBandGeometry.TryAnchorHitBox(ConsoleRigKind.Console, out RectInt box),
                          "premise: …with the ANCH switch on it");
            Rect piece = band.PieceRect(si);
            int k = band.K;
            var flick = new Vector2(piece.xMin + box.center.x * k, piece.yMax - box.center.y * k);

            Assert.IsTrue(band.PressControls(helm, flick, GameServices.HelmOverlay), "the switch takes the press");
            Assert.AreEqual(AnchorState.Set, anchor.State, "⭐ a flick of the band's ANCH switch drops the hook");
            yield return null;
            Assert.IsTrue(band.AnchorShown, "…and the switch shows it down");

            Assert.IsTrue(band.PressControls(helm, flick, GameServices.HelmOverlay));
            Assert.AreEqual(AnchorState.Stowed, anchor.State, "⭐ a second flick weighs it");
            yield return null;
            Assert.IsFalse(band.AnchorShown, "…and the switch shows it up");
        }

        [UnityTest]
        public IEnumerator ADomeAndAScope_BothHaveHousingsInTheBand()
        {
            // Before the band, a dome compass took the scope's flush station and the scope opened only as
            // a card. In the band each has its own piece.
            HelmOverlayHost host = Host();
            const string hullId = "boat.test_band_dome_scope";
            yield return Aboard(hullId,
                                NewConsole("band_dome_scope", ConsoleRigKind.Novi, CompassMount.Dome, supportsRadar: true),
                                BoatEquipment.RadarId);

            HelmFit fit = GameServices.HelmControl.Fit;
            Assert.AreEqual(CompassMount.Dome, fit.Compass, "premise: a dome compass");
            Assert.IsTrue(fit.Radar, "premise: and a scope");
            HelmBandController band = host.BandController;
            Assert.That(band.Rig, Is.EqualTo(ConsoleRigKind.Novi), "premise: the Novi's band");
            int ci = HelmBandGeometry.IndexOf(ConsoleRigKind.Novi, HelmBandPieceKind.Compass);
            int ri = HelmBandGeometry.IndexOf(ConsoleRigKind.Novi, HelmBandPieceKind.RadarPlate);
            Assert.GreaterOrEqual(ci, 0, "⭐ the dome has its piece");
            Assert.GreaterOrEqual(ri, 0, "⭐ …and the scope its housing");
            Assert.AreNotEqual(ci, ri, "neither takes the other's station");
            Assert.AreEqual(HelmBandController.RadarLabel, band.PlateLabelShown(ri),
                            "the scope's housing is named: it is fitted and live");

            RadarOverlayHost radar = RadarOverlayHost.Instance;
            Assert.IsNotNull(radar, "premise: the scope's host stands up");
            Assert.IsTrue(radar.Showing, "the scope is presented");
            Assert.IsFalse(radar.Expanded, "…as its housing");
            HelmInstrumentExpansion.Click(band.PlateAt(in fit, band.PieceRect(ri).center));
            yield return null;
            yield return null;
            Assert.IsTrue(radar.Expanded, "⭐ a press on its housing raises the scope, dome or no dome");
        }

        // ---- helpers --------------------------------------------------------------------------------

        private static void AssertTheScreenHasSize()
        {
            Assert.That(Screen.width, Is.GreaterThan(0),
                        "ENVIRONMENT: the player has no screen size, so no canvas has a rect and nothing can " +
                        "be placed on it. Run without -nographics (the documented verification posture).");
            Assert.That(Screen.height, Is.GreaterThan(0), "ENVIRONMENT: see above.");
        }

        private static void AssertRect(Rect expected, Rect actual, string message)
        {
            const float eps = 0.5f;
            Assert.AreEqual(expected.x, actual.x, eps, message + " (x)");
            Assert.AreEqual(expected.y, actual.y, eps, message + " (y)");
            Assert.AreEqual(expected.width, actual.width, eps, message + " (width)");
            Assert.AreEqual(expected.height, actual.height, eps, message + " (height)");
        }

        /// <summary>Wait until <paramref name="seconds"/> of the camera's clock have passed.</summary>
        private static IEnumerator WaitOnTheCamerasClock(float seconds)
        {
            float waited = 0f;
            float giveUp = Time.realtimeSinceStartup + seconds + StallSeconds;
            while (waited < seconds && Time.realtimeSinceStartup < giveUp)
            {
                yield return null;
                waited += Time.deltaTime;
            }
        }

        /// <summary>One M: pressed for a frame, then released.</summary>
        private static IEnumerator PressHideAll(HeldHudIntents held)
        {
            held.PressHideAll();
            yield return null;
            held.Release();
            yield return null;
        }

        private static HelmFootprintSettings Tuning()
            => GameServices.Config != null ? GameServices.Config.HelmFootprint : HelmFootprintSettings.Default;

        /// <summary>A <see cref="GameConfig"/> whose key-steer ease winds in the given time (S4.5).</summary>
        private void ConfigWithSteerWind(float seconds)
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            HelmWheelSettings wheel = cfg.HelmWheel;
            wheel.SteerEaseSeconds = seconds;
            cfg.HelmWheel = wheel;
            _spawned.Add(cfg);
            GameServices.Config = cfg;
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private HelmOverlayHost Host()
        {
            HelmOverlayHost host = HelmOverlayHost.Instance;
            if (host == null) host = Spawn("HelmOverlayHost(band test)").AddComponent<HelmOverlayHost>();
            return host;
        }

        private CameraFollow NewCamera(Transform target)
        {
            GameObject go = Spawn("BandTestCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(CameraFollow.DefaultWorldHeightMeters);
            go.transform.position = new Vector3(target.position.x, target.position.y, -10f);
            var follow = go.AddComponent<CameraFollow>();
            follow.Target = target;
            return follow;
        }

        /// <summary>A boat at her helm, and hers: the anchor switch works the player's own tackle.</summary>
        private (GameObject go, BoatController boat, HelmControlRelay relay) NewBoat()
        {
            GameObject go = Spawn("BandTestBoat");
            go.transform.position = new Vector3(400f, 300f, 0f);
            var boat = go.AddComponent<BoatController>();   // Awake self-adds HelmControlRelay
            var col = go.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, 4.0f);
            GameServices.Helm.SetPilotedHull(boat);
            GameServices.Helm.SetPlayersBoat(boat);
            return (go, boat, go.GetComponent<HelmControlRelay>());
        }

        /// <summary>Stand a piloted hull up with the shipped equipment gating, and buy what is named.</summary>
        private IEnumerator Aboard(string hullId, HelmConsoleDef console, params string[] bought)
        {
            var (_, boat, relay) = NewBoat();
            yield return null;                                  // Awake + OnEnable have run
            relay.DevIgnoreEquipmentGating = false;             // shipped-build gating, not the K cycle
            boat.SetHull(NewHull(hullId, console));
            foreach (string id in bought) InstrumentLocker.Add(_save.Current, hullId, id);
            yield return null;
            yield return null;                                  // one frame to resolve, one to paint
        }

        private HelmConsoleDef NewConsole(string id, ConsoleRigKind rig, CompassMount compass = CompassMount.Dome,
                                          bool supportsRadar = false)
        {
            var console = ScriptableObject.CreateInstance<HelmConsoleDef>();
            console.Id = "helm.test_" + id;
            console.Rig = rig;
            console.Lever = rig == ConsoleRigKind.Sport ? HelmLeverFinish.Chrome : HelmLeverFinish.Graphite;
            console.Wheel = HelmWheelRim.Rubber;
            console.DefaultCompass = compass;
            console.SupportsRadar = supportsRadar;
            console.DefaultRadar = false;                       // never fitted out of the box: it is bought
            _spawned.Add(console);
            return console;
        }

        private BoatHullDef NewHull(string id, HelmConsoleDef console, float rodeMeters = 0f)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.DisplayName = "Band Test Hull";
            h.Propulsion = PropulsionType.Engine;
            h.MassKg = 700f;
            h.EnginePower = 650f;
            h.RudderAuthority = 600f;
            h.ForwardDrag = 140f;
            h.LateralDrag = 360f;
            h.WindExposure = 0f;
            h.DraughtMeters = 0.5f;
            h.RodeMeters = rodeMeters;
            h.Helm = console;
            _spawned.Add(h);
            return h;
        }

        /// <summary>A committed asset, by path. Never added to the spawned list: an asset is not ours to
        /// destroy.</summary>
        private static T LoadCommitted<T>(string path) where T : ScriptableObject
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"missing {path}");
            return asset;
#else
            Assert.Ignore("Needs the AssetDatabase: this case reads a REAL committed hull.");
            return null;
#endif
        }
    }
}
