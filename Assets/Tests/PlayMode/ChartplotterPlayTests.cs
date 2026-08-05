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
    /// On-water integration for the ADR 0025 S6 chartplotter (PR 3): buy → mount → use, on the two hulls
    /// that carry a brow GPS slot. This is the half EditMode structurally cannot see — the equipment gate
    /// flipping on a purchase with no rebuild, the self-installing host actually drawing, and the
    /// preferences surviving the round trip to disk.
    ///
    /// <para><b>The differential guards are the load-bearing ones (the #421 lesson).</b> Night,
    /// orientation and range are all flags that compile, pass, and can be quietly ignored — the helm's
    /// night panel shipped dead twice exactly that way. Each is therefore asserted from what the LIVE
    /// host last rastered (<see cref="ChartplotterOverlayHost.NightShown"/> and friends), and each has a
    /// negative control proving the probe can report the other answer too.</para>
    ///
    /// <para><b>The repaint guard is measured, not claimed.</b> Steady sailing must cost a BOUNDED
    /// number of rasters and ZERO chart re-bakes, and both are read off counters the host increments on
    /// its real paint path.</para>
    /// </summary>
    public class ChartplotterPlayTests
    {
        private const string Region = "region.test_harbour";
        private static readonly Rect Bounds = new Rect(0f, 0f, 760f, 520f);

        /// <summary>A shelving coast, so the survey has real bands and the DPTH field has something to
        /// read. Analytic and deterministic.</summary>
        private sealed class ShelvingCoast : ITidalTerrain
        {
            public float ElevationAt(Vector2 p)
                => Mathf.Lerp(4f, -20f, Mathf.InverseLerp(60f, 620f, p.x));
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level = 1f;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private sealed class FakeClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay { get; set; } = 12f;      // noon — emphatically not night
            public float DayFraction => HourOfDay / 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public int Saves;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() => Saves++;
        }

        /// <summary>A stand-in for the boat probe, so the plotter has a heading and a speed without a
        /// physics step. The real producer is <c>ActiveBoatProbe</c>; this seam is the same one the HUD
        /// and the helm dash read.</summary>
        private sealed class FakeBoatProbe : IActiveBoatService
        {
            public BoatKinematics Value = BoatKinematics.None;
            public bool HasActiveBoat => Value.HasBoat;
            public BoatKinematics Sample() => Value;
        }

        private readonly List<Object> _spawned = new();
        private FakeSaveService _save;
        private FakeClock _clock;
        private FakeBoatProbe _probe;
        private GameObject _boatGo;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            HelmInstrumentExpansion.Collapse();
            HelmKeyCapture.Reset();          // no test may start with the helm deaf
            _boatGo = null;
            _save = new FakeSaveService(SaveMigration.NewGame());
            _clock = new FakeClock();
            _probe = new FakeBoatProbe
            {
                Value = new BoatKinematics(true, 40f, new Vector2(0f, 3f), 3f),
            };
            GameServices.TidalTerrain = new ShelvingCoast();
            GameServices.Environment = new FlatEnv();
            GameServices.Clock = _clock;
            GameServices.Save = _save;
            GameServices.ActiveBoat = _probe;
            GameServices.CurrentRegionId = Region;
            GameServices.CurrentRegionBounds = Bounds;
        }

        [TearDown]
        public void TearDown()
        {
            HelmInstrumentExpansion.Collapse();
            HelmKeyCapture.Reset();
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // ---- rig ---------------------------------------------------------------------------------------

        private BoatHullDef NewHull(string id, ConsoleRigKind rig, bool supportsGps)
        {
            var console = ScriptableObject.CreateInstance<HelmConsoleDef>();
            console.Id = "helm.test_" + rig;
            console.Rig = rig;
            console.DefaultSounder = SounderKind.None;
            console.SupportsFishFinder = true;
            console.SupportsGps = supportsGps;
            console.DefaultGps = false;                 // never fitted out of the box — it is bought
            _spawned.Add(console);

            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.DisplayName = "Test " + rig;
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

        private (BoatController boat, HelmControlRelay relay) NewBoat(Vector2 at)
        {
            var go = new GameObject("PlotterTestBoat");
            _boatGo = go;
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var boat = go.AddComponent<BoatController>();   // Awake self-adds the relay
            var col = go.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, 4.0f);
            _spawned.Add(go);
            return (boat, go.GetComponent<HelmControlRelay>());
        }

        /// <summary>Stand a piloted, GPS-capable hull up and (optionally) buy the unit. Returns the live
        /// host once it has had a frame to paint.</summary>
        private IEnumerator Aboard(string hullId, ConsoleRigKind rig, bool buyGps, bool supportsGps = true)
        {
            var (boat, relay) = NewBoat(new Vector2(400f, 300f));
            yield return null;                                  // Awake + OnEnable have run
            relay.DevIgnoreEquipmentGating = false;             // shipped-build gating, not the K cycle
            boat.SetHull(NewHull(hullId, rig, supportsGps));
            if (buyGps) InstrumentLocker.Add(_save.Current, hullId, BoatEquipment.GpsId);
            yield return null;
            yield return null;                                  // one frame to resolve, one to paint
        }

        private static ChartplotterOverlayHost Host => ChartplotterOverlayHost.Instance;

        private static ChartplotterSettings Cfg => GameServices.Chartplotter;

        private static NavRigState StateAt(Vector2 boat, float rangeNM, bool headUp)
            => new NavRigState(boat, true, 40f, 3f, rangeNM,
                               headUp ? NavRigGeometry.Orient.Head : NavRigGeometry.Orient.North,
                               false, true, 0f, false);

        /// <summary>Drive one control the way the pointer would, without synthesising a mouse.</summary>
        private static bool Press(int hit, Vector2 rigPx = default)
        {
            ChartplotterSettings cfg = Cfg;
            IHelmInstruments glass = GameServices.HelmInstruments;
            SaveData save = GameServices.Save.Current;
            ChartplotterPrefs prefs = InstrumentLocker.ChartplotterPrefsFor(
                save, glass.HullId, ChartplotterPrefs.FromDefaults(in cfg));
            NavRigState st = StateAt(new Vector2(400f, 300f), prefs.RangeNM(in cfg), prefs.HeadUp);
            return Host.Apply(glass, save, GameServices.CurrentRegionId, in prefs, in cfg, in st,
                              rigPx, hit);
        }

        private static ChartplotterPrefs StoredPrefs()
        {
            ChartplotterSettings cfg = Cfg;
            return InstrumentLocker.ChartplotterPrefsFor(GameServices.Save.Current,
                                                         GameServices.HelmInstruments.HullId,
                                                         ChartplotterPrefs.FromDefaults(in cfg));
        }

        // ---- buy → mount -------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AFreshNoviHasNoPlotter_AndThePurchaseFitsItWithNoRebuild()
        {
            yield return Aboard("boat.test_novi", ConsoleRigKind.Novi, buyGps: false);

            IHelmInstruments glass = GameServices.HelmInstruments;
            Assert.That(glass, Is.Not.Null, "the relay registered the instrument seam in OnEnable");
            Assert.That(glass.Fit.Gps, Is.False, "nothing fitted until it is bought");
            Assert.That(Host, Is.Not.Null, "the host self-installs");
            Assert.That(Host.Showing, Is.False, "no GPS, no chart");

            InstrumentLocker.Add(_save.Current, "boat.test_novi", BoatEquipment.GpsId);
            yield return null;
            yield return null;

            Assert.That(glass.Fit.Gps, Is.True, "the fit is resolved fresh — a purchase lands at once");
            Assert.That(Host.Showing, Is.True, "…and the brow lights up with no rebuild and no re-wire");
            Assert.That(Host.RepaintCount, Is.GreaterThan(0), "it actually rastered something");
        }

        [UnityTest]
        public IEnumerator TheCapeCarriesItToo_SoBothWheelhousesAreCovered()
        {
            yield return Aboard("boat.test_cape", ConsoleRigKind.Cape, buyGps: true);
            Assert.That(GameServices.HelmInstruments.Fit.Gps, Is.True);
            Assert.That(Host.Showing, Is.True);
        }

        [UnityTest]
        public IEnumerator ASkiffCannotCarryAPlotter_HoweverMuchIsBought()
        {
            // A console that does not support the slot: the purchase is recorded and still resolves to
            // no fit, because BoatEquipment gates on SupportsGps. The skiff has no brow to mount in.
            yield return Aboard("boat.test_skiff", ConsoleRigKind.Console, buyGps: true,
                                supportsGps: false);

            Assert.That(InstrumentLocker.Owns(_save.Current, "boat.test_skiff", BoatEquipment.GpsId),
                        Is.True, "the id really is owned…");
            Assert.That(GameServices.HelmInstruments.Fit.Gps, Is.False,
                        "…and the hull still cannot carry it");
            Assert.That(Host.Showing, Is.False, "so no chart appears on a skiff");
        }

        // ---- expand / collapse -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheGlassExpandsAndCollapses_AndOnlyOneInstrumentIsEverOpen()
        {
            yield return Aboard("boat.test_expand", ConsoleRigKind.Novi, buyGps: true);
            Assert.That(Host.Expanded, Is.False, "flush is the default (S4.5)");

            HelmInstrumentExpansion.Click(HelmInstrumentSlot.Chartplotter);
            yield return null;
            Assert.That(Host.Expanded, Is.True);

            // S6 PR 4: the MAX pusher is no longer the collapse — it opens the rig's MAX FACE and CNSL
            // brings the console face back, both WITHOUT leaving the expanded card. Collapsing is what
            // click-away, Esc and the mount itself are for.
            Assert.That(Press(ChartplotterOverlayLayout.KeyMax), Is.True);
            yield return null;
            Assert.That(Host.Expanded, Is.True, "MAX stays expanded — it changes face, not state");
            Assert.That(Host.MaxFace, Is.True, "…and the face it changes to is the MAX one");

            Assert.That(Press(ChartplotterOverlayLayout.KeyMax), Is.True, "CNSL goes back");
            yield return null;
            Assert.That(Host.MaxFace, Is.False);
            Assert.That(Host.Expanded, Is.True, "still expanded, still the console face");

            HelmInstrumentExpansion.Collapse();
            yield return null;
            Assert.That(Host.Expanded, Is.False);

            // And the arbiter is shared, so another instrument opening closes this one.
            HelmInstrumentExpansion.Click(HelmInstrumentSlot.Chartplotter);
            HelmInstrumentExpansion.Click(HelmInstrumentSlot.Sounder);
            yield return null;
            Assert.That(Host.Expanded, Is.False, "one expanded instrument at a time");
        }

        // ---- the six actions, all through NavLocker ----------------------------------------------------

        [UnityTest]
        public IEnumerator MarkingAWaypoint_GoesThroughTheLocker_AndSurvivesASaveLoadRoundTrip()
        {
            yield return Aboard("boat.test_mark", ConsoleRigKind.Novi, buyGps: true);

            Assert.That(NavLocker.WaypointCount(_save.Current), Is.EqualTo(0));
            Assert.That(Press(ChartplotterOverlayLayout.KeyMark), Is.True, "MARK drops one at own ship");
            Assert.That(NavLocker.WaypointCount(_save.Current), Is.EqualTo(1));
            Assert.That(_save.Saves, Is.GreaterThan(0), "a deliberate act is worth an I/O");

            // The round trip: serialise the save the way the real one does and read it back.
            string json = JsonUtility.ToJson(_save.Current);
            var reloaded = JsonUtility.FromJson<SaveData>(json);
            var read = new List<NavWaypoint>();
            NavLocker.WaypointsIn(reloaded, Region, read);
            Assert.That(read.Count, Is.EqualTo(1), "a waypoint you dropped must still be there tomorrow");
            Assert.That(read[0].Pos.x, Is.EqualTo(400f).Within(0.5f));
            Assert.That(read[0].RegionId, Is.EqualTo(Region), "region-stamped, so it means something");
        }

        [UnityTest]
        public IEnumerator ATapOnTheChart_RemovesTheWaypointUnderTheFingertip()
        {
            yield return Aboard("boat.test_remove", ConsoleRigKind.Novi, buyGps: true);

            Assert.That(Press(ChartplotterOverlayLayout.KeyMark), Is.True);
            Assert.That(NavLocker.WaypointCount(_save.Current), Is.EqualTo(1));
            yield return null;                          // the host re-reads its draw buffers

            // The mark is at own ship, and own ship is at the chart's centre — so tapping the centre is
            // tapping the mark.
            NavRigGeometry.Layout L = ChartplotterOverlayLayout.NativeLayout();
            var centre = new Vector2(L.Chart.x + L.Chart.width / 2f, L.Chart.y + L.Chart.height / 2f);
            Assert.That(Press(ChartplotterOverlayLayout.ChartHit, centre), Is.True);
            Assert.That(NavLocker.WaypointCount(_save.Current), Is.EqualTo(0), "removed through the locker");

            // NEGATIVE CONTROL: a tap far from any mark must NOT remove one, or the assertion above
            // would pass on a handler that simply deletes the nearest waypoint anywhere on the chart.
            Assert.That(Press(ChartplotterOverlayLayout.KeyMark), Is.True);
            yield return null;
            var corner = new Vector2(L.Chart.x + 3f, L.Chart.y + 3f);
            Assert.That(Press(ChartplotterOverlayLayout.ChartHit, corner), Is.False);
            Assert.That(NavLocker.WaypointCount(_save.Current), Is.EqualTo(1),
                        "a tap on open water is not a deletion");
        }

        [UnityTest]
        public IEnumerator TheRouteStrip_LaysARouteThroughTheWaypoints_AndClearsIt()
        {
            yield return Aboard("boat.test_route", ConsoleRigKind.Novi, buyGps: true);

            ChartplotterSettings cfg = Cfg;
            NavLocker.AddWaypoint(_save.Current,
                                  new NavWaypoint(Region, "A", new Vector2(380f, 300f),
                                                  NavWaypointKind.Mark), in cfg);
            NavLocker.AddWaypoint(_save.Current,
                                  new NavWaypoint(Region, "B", new Vector2(460f, 340f),
                                                  NavWaypointKind.Mark), in cfg);
            yield return null;                          // the host picks the new marks up

            Assert.That(Press(ChartplotterOverlayLayout.RouteHit), Is.True);
            var route = new List<Vector2>();
            NavLocker.RouteIn(_save.Current, Region, route);
            Assert.That(route.Count, Is.EqualTo(2), "one leg, from the two marks, in the order laid");
            yield return null;

            Assert.That(Press(ChartplotterOverlayLayout.RouteHit), Is.True, "and the same tap clears it");
            NavLocker.RouteIn(_save.Current, Region, route);
            Assert.That(route.Count, Is.EqualTo(0));
        }

        // ---- the differential guards (#421) ------------------------------------------------------------

        [UnityTest]
        public IEnumerator Night_FollowsTheStandingOrRule_OnTheRealPaintPath()
        {
            yield return Aboard("boat.test_night", ConsoleRigKind.Novi, buyGps: true);

            Assert.That(Host.NightShown, Is.False, "noon, lights off — this is the negative control");

            // Arm ONE side of the OR: the world clock.
            _clock.HourOfDay = 22f;
            yield return null;
            yield return null;
            Assert.That(Host.NightShown, Is.True,
                        "a lit panel lights what is mounted in it — the standing rule (#421)");

            // Back to noon, then arm the OTHER side: the player's own early-on tap.
            _clock.HourOfDay = 12f;
            yield return null;
            yield return null;
            Assert.That(Host.NightShown, Is.False, "…and it goes back, so the probe is not stuck true");

            Assert.That(Press(ChartplotterOverlayLayout.NightHit), Is.True);
            yield return null;
            yield return null;
            Assert.That(Host.NightShown, Is.True, "the strip tap is the early-on override");
            Assert.That(StoredPrefs().Night, Is.True, "…and it persists, per hull");
        }

        [UnityTest]
        public IEnumerator Orientation_ReachesTheRaster_AndPersists()
        {
            yield return Aboard("boat.test_orient", ConsoleRigKind.Novi, buyGps: true);

            Assert.That(Host.HeadUpShown, Is.False, "north-up is the chart convention (negative control)");
            Assert.That(Press(ChartplotterOverlayLayout.OrientHit), Is.True);
            yield return null;
            yield return null;

            Assert.That(Host.HeadUpShown, Is.True, "the toggle reaches the picture, not just the pref");
            Assert.That(StoredPrefs().HeadUp, Is.True);

            Assert.That(Press(ChartplotterOverlayLayout.OrientHit), Is.True);
            yield return null;
            yield return null;
            Assert.That(Host.HeadUpShown, Is.False, "and back again");
        }

        [UnityTest]
        public IEnumerator TheRangeKeys_StepTheLadder_AndReachTheRaster()
        {
            yield return Aboard("boat.test_range", ConsoleRigKind.Novi, buyGps: true);

            ChartplotterSettings cfg = Cfg;
            float started = Host.RangeShown;
            Assert.That(started, Is.EqualTo(cfg.RangeNMAt(cfg.SafeDefaultRangeStep)).Within(1e-4f),
                        "a fresh plotter wakes on the owner's nominated rung");

            Assert.That(Press(ChartplotterOverlayLayout.KeyIn), Is.True);
            yield return null;
            yield return null;
            Assert.That(Host.RangeShown, Is.LessThan(started), "IN is a CLOSER range");

            Assert.That(Press(ChartplotterOverlayLayout.KeyOut), Is.True);
            Assert.That(Press(ChartplotterOverlayLayout.KeyOut), Is.True);
            yield return null;
            yield return null;
            Assert.That(Host.RangeShown, Is.GreaterThan(started), "…and OUT is a wider one");
            Assert.That(StoredPrefs().RangeStep, Is.EqualTo(cfg.SafeDefaultRangeStep + 1));
        }

        // ---- the track ---------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheTrackDrawsFromTheRecordersData_WhichAccruesWhetherOrNotAPlotterIsFitted()
        {
            // No GPS bought at all — the breadcrumb must still accrue, because it is a record of the
            // voyage rather than an output of the instrument (the honesty invariant).
            yield return Aboard("boat.test_track", ConsoleRigKind.Novi, buyGps: false);

            ChartplotterSettings cfg = Cfg;
            Assert.That(NavLocker.RecordTrack(_save.Current, Region, new Vector2(400f, 300f), in cfg),
                        Is.True);
            Assert.That(NavLocker.RecordTrack(_save.Current, Region,
                                              new Vector2(400f + cfg.TrackMinSpacingMetres * 2f, 300f),
                                              in cfg), Is.True);

            var track = new List<Vector2>();
            NavLocker.TrackIn(_save.Current, Region, track);
            Assert.That(track.Count, Is.EqualTo(2),
                        "the track is the player's, not the plotter's — no instrument was fitted");

            // Now buy the glass: the crumbs that were laid before the purchase are on it immediately.
            InstrumentLocker.Add(_save.Current, "boat.test_track", BoatEquipment.GpsId);
            yield return null;
            yield return null;
            Assert.That(Host.Showing, Is.True);
            Assert.That(Host.RepaintCount, Is.GreaterThan(0),
                        "and the chart draws that existing history rather than starting from now");
        }

        // ---- the repaint budget (rule 7), MEASURED on the live host ------------------------------------

        [UnityTest]
        public IEnumerator SteadySailing_CostsBoundedRepaints_AndNeverRebakesTheChartBase()
        {
            yield return Aboard("boat.test_perf", ConsoleRigKind.Novi, buyGps: true);
            Assert.That(Host.Showing, Is.True);

            int bakesAfterFirstPaint = Host.BaseBakeCount;
            int repaintsAtStart = Host.RepaintCount;
            Assert.That(bakesAfterFirstPaint, Is.GreaterThan(0), "the survey was baked once, up front");

            // Sail straight north at a REALISTIC step: ~3.5 knots is ~1.8 m/s, which at 60 fps is
            // ~0.03 m per frame. A fixed per-frame step rather than one scaled by Time.deltaTime, so
            // the distance covered is identical however fast a headless runner spins.
            //
            // The number matters. One chart pixel at the default range is ~0.55 m, so a boat under way
            // crosses one roughly every 18 frames — which is exactly the gap the quantized key is
            // supposed to exploit. Step the boat by more than a chart pixel per frame and the picture
            // genuinely does change every frame; the guard would then be asserting nothing.
            const int frames = 90;
            const float metresPerFrame = 0.03f;
            Assert.That(_boatGo, Is.Not.Null);
            Vector2 pos = _boatGo.transform.position;
            for (int i = 0; i < frames; i++)
            {
                pos += new Vector2(0f, metresPerFrame);
                _boatGo.transform.position = new Vector3(pos.x, pos.y, 0f);
                yield return null;
            }

            Assert.That(Host.BaseBakeCount, Is.EqualTo(bakesAfterFirstPaint),
                        "the survey is a pure function of (region x palette x terrain) — NONE of which " +
                        "move under way, so sailing must never re-bake the chart base");

            int repaints = Host.RepaintCount - repaintsAtStart;
            Assert.That(repaints, Is.LessThan(frames / 3),
                        $"steady sailing must not raster every frame ({repaints} of {frames}) — the " +
                        "own-ship layer is keyed on a whole CHART PIXEL and the depth to the tenth it " +
                        "prints, not to a float position");

            // NEGATIVE CONTROL: the counter is not simply frozen. A control the player actually pressed
            // must still produce a raster, or the bound above would be satisfied by a dead host.
            int before = Host.RepaintCount;
            Assert.That(Press(ChartplotterOverlayLayout.KeyOut), Is.True);
            yield return null;
            yield return null;
            Assert.That(Host.RepaintCount, Is.GreaterThan(before),
                        "…while a real change still repaints — if this fails, the bound above proves " +
                        "nothing about the change detection");
        }

        // ---- the MAX face (S6 PR 4) --------------------------------------------------------------------

        /// <summary>Expand and switch to the rig's MAX face, the way the pushers do.</summary>
        private IEnumerator OnMaxFace()
        {
            HelmInstrumentExpansion.Click(HelmInstrumentSlot.Chartplotter);
            yield return null;
            Assert.That(Press(ChartplotterOverlayLayout.KeyMax), Is.True);
            yield return null;
            yield return null;
            Assert.That(Host.MaxFace, Is.True, "the MAX pusher put the full kit up");
            Assert.That(Host.MaxFaceShown, Is.True,
                        "…and it reached the RASTER, not just the state (the #421 seam)");
        }

        [UnityTest]
        public IEnumerator TheRail_SwitchesTheLIVELayers_AndRefusesTheDormantOnes()
        {
            yield return Aboard("boat.test_rail", ConsoleRigKind.Novi, buyGps: true);
            yield return OnMaxFace();

            // LIVE: each of the three has a source in this world, so each press must move the state.
            for (int slot = 0; slot < NavRigGeometry.RailIds.Length; slot++)
            {
                NavChartLayers layer = NavRigGeometry.RailLayer(slot);
                if (layer == NavChartLayers.None || (layer & NavChartLayers.Live) == 0) continue;

                bool wasOn = (Host.Layers & layer) != 0;
                Assert.That(Press(ChartplotterOverlayLayout.RailBase + slot), Is.True,
                            $"{NavRigGeometry.RailIds[slot]} is a live switch");
                yield return null;
                Assert.That((Host.Layers & layer) != 0, Is.Not.EqualTo(wasOn),
                            $"{NavRigGeometry.RailIds[slot]} must actually flip");
                Assert.That(Press(ChartplotterOverlayLayout.RailBase + slot), Is.True, "…and back");
                yield return null;
            }

            // DORMANT: LAND, ROCK, BUOY, TRFC have no source here. The press must REFUSE rather than
            // toggle a flag no pixel reads — a control that lights and does nothing is exactly the
            // defect #421 shipped twice.
            NavChartLayers before = Host.Layers;
            for (int slot = 0; slot < NavRigGeometry.RailIds.Length; slot++)
            {
                NavChartLayers layer = NavRigGeometry.RailLayer(slot);
                if (layer == NavChartLayers.None || (layer & NavChartLayers.Live) != 0) continue;
                Assert.That(Press(ChartplotterOverlayLayout.RailBase + slot), Is.False,
                            $"{NavRigGeometry.RailIds[slot]} has no source — it must say so by refusing");
            }
            yield return null;
            Assert.That(Host.Layers, Is.EqualTo(before), "…and none of them moved the state");
        }

        [UnityTest]
        public IEnumerator TheRail_PicksUpAndPutsDownEachTool()
        {
            yield return Aboard("boat.test_tools", ConsoleRigKind.Novi, buyGps: true);
            yield return OnMaxFace();
            Assert.That(Host.Tool, Is.EqualTo(NavChartTool.Pan), "PAN is the neutral default");

            foreach (int slot in new[] { 9, 10, 11 })
            {
                NavChartTool tool = NavRigGeometry.RailTool(slot);
                Assert.That(Press(ChartplotterOverlayLayout.RailBase + slot), Is.True);
                yield return null;
                Assert.That(Host.Tool, Is.EqualTo(tool), $"{NavRigGeometry.RailIds[slot]} selects");
                Assert.That(Press(ChartplotterOverlayLayout.RailBase + slot), Is.True);
                yield return null;
                Assert.That(Host.Tool, Is.EqualTo(NavChartTool.Pan), "…and pressing it again puts it down");
            }
        }

        [UnityTest]
        public IEnumerator TheMarkTool_PlacesAWaypointAtTheTap_AndTheRouteToolAppendsALeg()
        {
            yield return Aboard("boat.test_taptoplace", ConsoleRigKind.Novi, buyGps: true);
            yield return OnMaxFace();

            NavRigGeometry.Layout L = ChartplotterOverlayLayout.NativeLayout(NavRigGeometry.Face.Max);
            var offCentre = new Vector2(L.Chart.x + L.Chart.width * 0.3f,
                                        L.Chart.y + L.Chart.height * 0.3f);

            // PAN first: the neutral tool must NOT place anything — the negative control that makes the
            // assertion below about the TOOL rather than about the tap.
            Assert.That(Press(ChartplotterOverlayLayout.ChartHit, offCentre), Is.False,
                        "PAN on open water selects nothing and places nothing");
            Assert.That(NavLocker.WaypointCount(_save.Current), Is.EqualTo(0));

            Assert.That(Press(ChartplotterOverlayLayout.RailBase + 9), Is.True);   // MRK
            yield return null;
            Assert.That(Press(ChartplotterOverlayLayout.ChartHit, offCentre), Is.True);
            Assert.That(NavLocker.WaypointCount(_save.Current), Is.EqualTo(1),
                        "tap-to-place — the gesture the console face documented as belonging here");
            yield return null;

            var read = new List<NavWaypoint>();
            NavLocker.WaypointsIn(_save.Current, Region, read);
            Assert.That((read[0].Pos - new Vector2(400f, 300f)).magnitude, Is.GreaterThan(1f),
                        "…and it landed where the finger was, not at own ship");

            Assert.That(Press(ChartplotterOverlayLayout.RailBase + 10), Is.True);  // RTE
            yield return null;
            Assert.That(Press(ChartplotterOverlayLayout.ChartHit, offCentre), Is.True);
            var route = new List<Vector2>();
            NavLocker.RouteIn(_save.Current, Region, route);
            Assert.That(route.Count, Is.EqualTo(1), "the route tool appends, in the order you tap");
        }

        [UnityTest]
        public IEnumerator TheManagerColumn_Selects_Renames_AndDeletes_AllThroughTheLocker()
        {
            yield return Aboard("boat.test_manager", ConsoleRigKind.Novi, buyGps: true);

            ChartplotterSettings cfg = Cfg;
            NavLocker.AddWaypoint(_save.Current, new NavWaypoint(Region, "A", new Vector2(380f, 300f),
                                                                 NavWaypointKind.Mark), in cfg);
            NavLocker.AddWaypoint(_save.Current, new NavWaypoint(Region, "B", new Vector2(420f, 320f),
                                                                 NavWaypointKind.Mark), in cfg);
            yield return OnMaxFace();

            Assert.That(Host.SelectedWaypoint, Is.EqualTo(-1), "nothing picked to start with");
            Assert.That(Press(ChartplotterOverlayLayout.WaypointRowBase + 1), Is.True);
            yield return null;
            Assert.That(Host.SelectedWaypoint, Is.EqualTo(1), "clicking a row picks it");

            // NAME opens the field and takes the keyboard off the helm.
            Assert.That(Press(ChartplotterOverlayLayout.ActionName), Is.True);
            yield return null;
            Assert.That(Host.Editing, Is.True);
            Assert.That(Host.EditText, Is.EqualTo("B"), "seeded with the name it already had");

            // Commit through the same path the Enter key uses.
            Host.CommitName();
            yield return null;
            Assert.That(Host.Editing, Is.False);
            Assert.That(Host.SelectedWaypoint, Is.EqualTo(1),
                        "the row you were naming stays picked — you are still working on it");

            // DEL removes the selected row, through the locker.
            Assert.That(Press(ChartplotterOverlayLayout.ActionDelete), Is.True);
            yield return null;
            var read = new List<NavWaypoint>();
            NavLocker.WaypointsIn(_save.Current, Region, read);
            Assert.That(read.Count, Is.EqualTo(1));
            Assert.That(read[0].Name, Is.EqualTo("A"), "the row the column showed is the row that went");
            Assert.That(Host.SelectedWaypoint, Is.EqualTo(-1), "…and the selection went with it");
        }

        [UnityTest]
        public IEnumerator ATypedName_ReachesTheLocker_AndSurvivesASaveLoadRoundTrip()
        {
            yield return Aboard("boat.test_naming", ConsoleRigKind.Novi, buyGps: true);
            ChartplotterSettings cfg = Cfg;
            NavLocker.AddWaypoint(_save.Current, new NavWaypoint(Region, "MARK 1",
                                                                 new Vector2(380f, 300f),
                                                                 NavWaypointKind.Mark), in cfg);
            yield return OnMaxFace();

            Assert.That(Press(ChartplotterOverlayLayout.WaypointRowBase + 0), Is.True);
            yield return null;
            Assert.That(Press(ChartplotterOverlayLayout.ActionName), Is.True);
            yield return null;

            Host.TypeName("wreck é7");     // lower case, an undrawable letter, a digit
            Assert.That(Host.EditText, Is.EqualTo("WRECK 7"),
                        "sanitised on the way in, so the loud-tofu guard never has to fire for a name");
            Host.CommitName();
            yield return null;

            string json = JsonUtility.ToJson(_save.Current);
            var reloaded = JsonUtility.FromJson<SaveData>(json);
            var read = new List<NavWaypoint>();
            NavLocker.WaypointsIn(reloaded, Region, read);
            Assert.That(read.Count, Is.EqualTo(1));
            Assert.That(read[0].Name, Is.EqualTo("WRECK 7"),
                        "a name you typed must still be there tomorrow");
            Assert.That(read[0].Pos.x, Is.EqualTo(380f).Within(0.5f), "and the mark did not move");
        }

        // ---- TEXT ENTRY vs THE HELM --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TypingAName_TakesTheKeyboardOffTheHelm_AndGivesItBackBothWays()
        {
            yield return Aboard("boat.test_typing", ConsoleRigKind.Novi, buyGps: true);

            // The live helm input component, on the same hull the plotter is mounted to.
            var helm = _boatGo.GetComponent<DevBoatInput>();
            if (helm == null) helm = _boatGo.AddComponent<DevBoatInput>();
            yield return null;

            ChartplotterSettings cfg = Cfg;
            NavLocker.AddWaypoint(_save.Current, new NavWaypoint(Region, "A", new Vector2(380f, 300f),
                                                                 NavWaypointKind.Mark), in cfg);
            yield return OnMaxFace();

            // NEGATIVE CONTROL: with no field open, W/A/S/D are the helm's — as they have always been.
            Assert.That(HelmKeyCapture.IsCapturing, Is.False);
            Assert.That(helm.HelmKeysLive, Is.True, "the probe can report 'the helm has the keys'");

            Assert.That(Press(ChartplotterOverlayLayout.WaypointRowBase + 0), Is.True);
            yield return null;
            Assert.That(Press(ChartplotterOverlayLayout.ActionName), Is.True);
            yield return null;

            Assert.That(HelmKeyCapture.IsCapturing, Is.True, "the field owns the keyboard…");
            Assert.That(helm.HelmKeysLive, Is.False,
                        "…so typing 'WRECK' cannot also put the wheel hard over");

            // COMMIT gives it back.
            Host.CommitName();
            yield return null;
            Assert.That(HelmKeyCapture.IsCapturing, Is.False);
            Assert.That(helm.HelmKeysLive, Is.True, "closing the editor restores steering");

            // …and so does CANCEL, which is the arm that is easy to forget.
            Assert.That(Press(ChartplotterOverlayLayout.ActionName), Is.True);
            yield return null;
            Assert.That(helm.HelmKeysLive, Is.False);
            Host.CancelName();
            yield return null;
            Assert.That(HelmKeyCapture.IsCapturing, Is.False);
            Assert.That(helm.HelmKeysLive, Is.True, "cancelling restores it too");
        }

        [UnityTest]
        public IEnumerator CollapsingWhileNaming_NeverLeavesTheHelmDeaf()
        {
            // The failure this gate could cause is worse than the bug it prevents: a boat you cannot
            // steer. Every exit must release, including the ones nobody presses on purpose.
            yield return Aboard("boat.test_deaf", ConsoleRigKind.Novi, buyGps: true);
            var helm = _boatGo.GetComponent<DevBoatInput>();
            if (helm == null) helm = _boatGo.AddComponent<DevBoatInput>();
            ChartplotterSettings cfg = Cfg;
            NavLocker.AddWaypoint(_save.Current, new NavWaypoint(Region, "A", new Vector2(380f, 300f),
                                                                 NavWaypointKind.Mark), in cfg);
            yield return OnMaxFace();

            Assert.That(Press(ChartplotterOverlayLayout.WaypointRowBase + 0), Is.True);
            yield return null;
            Assert.That(Press(ChartplotterOverlayLayout.ActionName), Is.True);
            yield return null;
            Assert.That(helm.HelmKeysLive, Is.False, "mid-edit…");

            HelmInstrumentExpansion.Collapse();          // click-away, from underneath the editor
            yield return null;
            yield return null;
            Assert.That(HelmKeyCapture.IsCapturing, Is.False,
                        "…and the card closing under it hands the keyboard straight back");
            Assert.That(helm.HelmKeysLive, Is.True);
            Assert.That(Host.MaxFace, Is.False, "collapsed is always the console face");
        }

        // ---- the repaint budget on the MAX face --------------------------------------------------------

        [UnityTest]
        public IEnumerator SteadySailing_OnTheMaxFace_StaysBounded_AndTheProfileDoesNotRebakePerFrame()
        {
            yield return Aboard("boat.test_maxperf", ConsoleRigKind.Novi, buyGps: true);
            yield return OnMaxFace();

            int bakes = Host.BaseBakeCount;
            int repaintsAtStart = Host.RepaintCount;
            int profileAtStart = Host.ProfileBakeCount;

            // The same realistic step the console face's pin uses: ~0.03 m per frame, well under one
            // chart pixel, so a per-frame raster would be a real defect rather than honest work.
            const int frames = 90;
            Vector2 pos = _boatGo.transform.position;
            for (int i = 0; i < frames; i++)
            {
                pos += new Vector2(0f, 0.03f);
                _boatGo.transform.position = new Vector3(pos.x, pos.y, 0f);
                yield return null;
            }

            Assert.That(Host.BaseBakeCount, Is.EqualTo(bakes),
                        "the survey still never re-bakes under way, MAX face or not");
            int repaints = Host.RepaintCount - repaintsAtStart;
            Assert.That(repaints, Is.LessThan(frames / 3),
                        $"the MAX face must hold the same bound as the console face ({repaints} of " +
                        $"{frames}) — the tide, the set and the manager are all quantized to what they print");

            int profiles = Host.ProfileBakeCount - profileAtStart;
            Assert.That(profiles, Is.LessThanOrEqualTo(repaints),
                        "the depth-ahead line re-samples at MOST once per raster, never per frame");

            // NEGATIVE CONTROL: the counters are not simply frozen.
            int before = Host.RepaintCount;
            Assert.That(Press(ChartplotterOverlayLayout.RailBase + 5), Is.True);   // DPTH
            yield return null;
            yield return null;
            Assert.That(Host.RepaintCount, Is.GreaterThan(before),
                        "…while a real change still repaints — or the bound above proves nothing");
        }

        // ---- the dev cycle ------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheKDevCycle_WalksTheBrowSounder_AndLeavesTheGpsStationAlone()
        {
            var (boat, relay) = NewBoat(new Vector2(400f, 300f));
            yield return null;
            relay.DevIgnoreEquipmentGating = true;              // the K cycle's own predicate
            boat.SetHull(NewHull("boat.test_devcycle", ConsoleRigKind.Novi, supportsGps: true));
            InstrumentLocker.Add(_save.Current, "boat.test_devcycle", BoatEquipment.GpsId);
            yield return null;
            yield return null;

            Assert.That(Host.Showing, Is.True, "the bought plotter is on the brow");

            // K steps the BROW SOUNDER cutout (None -> Depth -> Fish -> None). The GPS lives in a
            // different slot and DevFit passes it through untouched, so cycling the sounder must never
            // disturb the chart — the owner can walk the cutout with the plotter still lit beside it.
            var cycle = relay.GetComponent<DevInstrumentCycle>();
            Assert.That(cycle, Is.Not.Null, "BoatController self-installs the dev cycle");
            for (int i = 0; i < 4; i++)
            {
                cycle.Step();
                yield return null;
                yield return null;
                Assert.That(GameServices.HelmInstruments.Fit.Gps, Is.True,
                            $"step {i}: the brow cycle must not un-fit the GPS");
                Assert.That(Host.Showing, Is.True, $"step {i}: …nor blank the chart");
            }
        }
    }
}
