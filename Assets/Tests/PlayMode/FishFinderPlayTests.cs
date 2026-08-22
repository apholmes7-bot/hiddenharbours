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
    /// On-water integration for the ADR 0025 S3b fish finder: the equipment gate handing the brow cutout
    /// to the colour sonar (and the plain sounder standing down), the card's show/hide and focus, the
    /// owner's Ruling A controls reaching the persisted preferences through the Core seam, the fish
    /// arriving from <see cref="IFishSchools"/> — including the shipped EMPTY SEA — and the repaint
    /// cadence that keeps the instrument inside the frame budget. This is the half EditMode structurally
    /// cannot see: <see cref="HelmControlRelay"/> claims its Core slots in <c>OnEnable</c>, which never
    /// fires in EditMode.
    /// </summary>
    public class FishFinderPlayTests
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
            public int Saves;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() => Saves++;
        }

        /// <summary>The stand-in for S3a's model: one school, everywhere, always. Deliberately the ONLY
        /// thing this slice knows about producing schools — the UI is a pure reader, and swapping this for
        /// the real model is the one assignment the seam promises.</summary>
        private sealed class OneSchool : IFishSchools
        {
            public FishMark Mark = new FishMark(8f, 3, 1f);
            public int Calls;

            public int SchoolsAt(Vector2 worldPos, double gameSeconds, List<FishSchool> into)
            {
                into?.Clear();
                return 0;
            }

            public int MarksAt(Vector2 worldPos, double gameSeconds, List<FishMark> into)
            {
                Calls++;
                into?.Clear();
                into?.Add(Mark);
                return 1;
            }
        }

        private readonly List<Object> _spawned = new();
        private FlatTerrain _terrain;
        private FlatEnv _env;
        private FakeSaveService _save;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _terrain = new FlatTerrain { Elevation = -12f };
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
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private BoatHullDef NewConsoleHull(string id)
        {
            var console = ScriptableObject.CreateInstance<HelmConsoleDef>();
            console.Id = "helm.test_console";
            console.Rig = ConsoleRigKind.Console;
            console.DefaultSounder = SounderKind.None;
            console.SupportsFishFinder = true;
            _spawned.Add(console);

            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.DisplayName = "Test Console Hull";
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

        private (GameObject go, BoatController boat, HelmControlRelay relay) NewBoat()
        {
            var go = new GameObject("FinderTestBoat");
            var boat = go.AddComponent<BoatController>();   // Awake self-adds the relay
            var col = go.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, 4.0f);
            _spawned.Add(go);
            // ⭐ THE PLAYER IS AT THIS BOAT'S HELM. The Core slot is arbitrated by OCCUPANCY now
            // (HelmSlot) rather than granted to whichever relay enabled last, so the one boat in
            // this fixture has to be DECLARED hers — which is what the fixture always meant.
            GameServices.Helm.SetPilotedHull(boat);
            return (go, boat, go.GetComponent<HelmControlRelay>());
        }

        private FishFinderOverlayHost TheHost()
        {
            FishFinderOverlayHost host = FishFinderOverlayHost.Instance;
            if (host == null)
            {
                var go = new GameObject("FishFinderOverlayHost(test)");
                _spawned.Add(go);
                host = go.AddComponent<FishFinderOverlayHost>();
            }
            return host;
        }

        private SounderOverlayHost TheSounderHost()
        {
            SounderOverlayHost host = SounderOverlayHost.Instance;
            if (host == null)
            {
                var go = new GameObject("SounderOverlayHost(test)");
                _spawned.Add(go);
                host = go.AddComponent<SounderOverlayHost>();
            }
            return host;
        }

        // ---- the cutout ------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheFinderTakesTheCutout_AndThePlainSounderStandsDown()
        {
            FishFinderOverlayHost finder = TheHost();
            SounderOverlayHost sounder = TheSounderHost();

            var (_, boat, relay) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = false;
            boat.SetHull(NewConsoleHull("boat.test_finder"));
            yield return null;
            Assert.That(finder.Showing, Is.False, "no instrument, no card");

            InstrumentLocker.Add(_save.Current, "boat.test_finder", BoatEquipment.DepthSounderId);
            yield return null;
            Assert.That(sounder.Showing, Is.True, "the basic unit lights the brow first");
            Assert.That(finder.Showing, Is.False);

            InstrumentLocker.Add(_save.Current, "boat.test_finder", BoatEquipment.FishFinderId);
            yield return null;
            Assert.That(GameServices.HelmInstruments.Fit.Sounder, Is.EqualTo(SounderKind.Fish),
                        "the upgrade wins the shared cutout");
            Assert.That(finder.Showing, Is.True, "…and the sonar card is on screen");
            Assert.That(sounder.Showing, Is.False, "…with the depth card stood down, never both");
            Assert.That(finder.Focused, Is.False, "the finder starts as the small dash card");
        }

        [UnityTest]
        public IEnumerator WithNoSounding_TheGlassShowsNothing_RatherThanAStaleNumber()
        {
            FishFinderOverlayHost host = TheHost();
            var (_, boat, relay) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = false;
            boat.SetHull(NewConsoleHull("boat.test_nosound"));
            InstrumentLocker.Add(_save.Current, "boat.test_nosound", BoatEquipment.FishFinderId);
            yield return null;
            Assert.That(host.Showing, Is.True);

            GameServices.TidalTerrain = null;    // left the region — there is no transducer read
            yield return WaitSeconds(GameServices.DepthSounder.ReadIntervalSec * 1.5f);
            Assert.That(host.Showing, Is.False);
        }

        // ---- the fish come from the seam --------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheEmptySea_RunsAndDraws_WithNoFishModelInTheProjectAtAll()
        {
            // The state this whole slice was built against, and the right SHIPPED behaviour in a region
            // with no fish authored yet.
            FishFinderOverlayHost host = TheHost();
            Assert.That(GameServices.FishSchools, Is.SameAs(EmptyFishSchools.Instance),
                        "GameServices.FishSchools is never null — it is the empty sea by default");

            var (_, boat, relay) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = false;
            boat.SetHull(NewConsoleHull("boat.test_empty"));
            InstrumentLocker.Add(_save.Current, "boat.test_empty", BoatEquipment.FishFinderId);
            yield return null;
            yield return null;
            Assert.That(host.Showing, Is.True, "an empty sonar is a picture, not an exception");
            Assert.That(host.RepaintCount, Is.GreaterThan(0), "…and it actually painted");
        }

        [UnityTest]
        public IEnumerator AFishModel_ReachesTheGlass_WithNoUiChange()
        {
            // The seam's promise: one assignment swaps the model in and the finder starts drawing marks.
            FishFinderOverlayHost host = TheHost();
            var model = new OneSchool();
            GameServices.FishSchools = model;

            var (_, boat, relay) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = false;
            boat.SetHull(NewConsoleHull("boat.test_fish"));
            InstrumentLocker.Add(_save.Current, "boat.test_fish", BoatEquipment.FishFinderId);
            yield return WaitSeconds(0.3f);

            Assert.That(model.Calls, Is.GreaterThan(0), "the finder asked the seam where the fish are");
            Assert.That(GameServices.HelmInstruments.TryReadPosition(out Vector2 p), Is.True,
                        "…at the transducer's own position");
            Assert.That(p, Is.EqualTo((Vector2)boat.transform.position));

            GameServices.FishSchools = null;
            Assert.That(GameServices.FishSchools, Is.SameAs(EmptyFishSchools.Instance),
                        "and a model that goes away degrades to the empty sea, never to a null deref");
        }

        // ---- the cadence (rule 7) ----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheScan_RepaintsOnItsOwnCadence_NotEveryFrame()
        {
            // ⚠ Frame count is NOT time: headless frames run as fast as the machine allows, so the budget
            // is computed from MEASURED elapsed Time.time, never from the frame count.
            FishFinderOverlayHost host = TheHost();
            var (_, boat, relay) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = false;
            boat.SetHull(NewConsoleHull("boat.test_cadence"));
            InstrumentLocker.Add(_save.Current, "boat.test_cadence", BoatEquipment.FishFinderId);
            yield return null;
            yield return null;

            float hz = GameServices.FishFinder.WaterfallHz;
            int before = host.RepaintCount;
            float t0 = Time.time;
            const int frames = 120;
            for (int i = 0; i < frames; i++) yield return null;
            float elapsed = Time.time - t0;
            int rasters = host.RepaintCount - before;

            // Budget: the scan's own buckets, plus the throttled sounding tick, plus slack for a boundary.
            int budget = Mathf.CeilToInt(elapsed * hz) + Mathf.CeilToInt(elapsed / 0.25f) + 3;
            Assert.That(rasters, Is.LessThanOrEqualTo(budget),
                        $"{frames} frames in {elapsed:F3}s produced {rasters} rasters (budget {budget}) — " +
                        "the scan must repaint on WaterfallHz, not per frame");
            Assert.That(rasters, Is.LessThan(frames),
                        "a per-frame repaint is a rule-7 break on its own: O(width) spans + a texture upload");
        }

        // ---- Ruling A through the seam -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator ThePushers_MovePreferences_ThroughTheSeam()
        {
            FishFinderOverlayHost host = TheHost();
            var (_, boat, relay) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = false;
            boat.SetHull(NewConsoleHull("boat.test_push"));
            InstrumentLocker.Add(_save.Current, "boat.test_push", BoatEquipment.FishFinderId);
            yield return null;

            IHelmInstruments glass = GameServices.HelmInstruments;
            DepthSounderSettings sounder = GameServices.DepthSounder;
            FishFinderSettings finder = GameServices.FishFinder;

            // Fresh prefs must take the OWNER's tuned range, not the code default (the T6 handover).
            Assert.That(glass.SounderPrefs.RangeMetres,
                        Is.EqualTo(finder.DefaultRangeMetres).Within(1e-4f));

            // ▲ in the default RANGE mode walks the rig's ladder.
            float range0 = glass.SounderPrefs.RangeMetres;
            host.Apply(glass, glass.SounderPrefs, in sounder, in finder, 1);
            Assert.That(glass.SounderPrefs.RangeMetres,
                        Is.EqualTo(FishRigGeometry.StepRange(range0, +1)).Within(1e-4f));
            Assert.That(_save.Saves, Is.GreaterThan(0), "a changed scale is written to disk");

            host.Apply(glass, glass.SounderPrefs, in sounder, in finder, 2);
            Assert.That(glass.SounderPrefs.RangeMetres, Is.EqualTo(range0).Within(1e-4f));

            // MODE hands ▲/▼ to the alarm, and changes no preference itself.
            int saves = _save.Saves;
            host.Apply(glass, glass.SounderPrefs, in sounder, in finder, 0);
            Assert.That(host.Adjust, Is.EqualTo(FishRigAdjust.Alarm));
            Assert.That(_save.Saves, Is.EqualTo(saves), "MODE is thumb position, not a preference");

            float alarm0 = glass.SounderPrefs.AlarmMetres;
            host.Apply(glass, glass.SounderPrefs, in sounder, in finder, 1);
            Assert.That(glass.SounderPrefs.AlarmMetres,
                        Is.EqualTo(DepthSounder.StepAlarm(alarm0, +1, in sounder)).Within(1e-4f),
                        "the set-point moves through the SOUNDER's own rule (Ruling E)");
            Assert.That(glass.SounderPrefs.RangeMetres, Is.EqualTo(range0).Within(1e-4f),
                        "…and the scale is untouched while in ALARM mode");

            // The three glass regions.
            bool night = glass.SounderPrefs.Night;
            host.Apply(glass, glass.SounderPrefs, in sounder, in finder,
                       FishFinderOverlayLayout.StatusHit);
            Assert.That(glass.SounderPrefs.Night, Is.EqualTo(!night), "a tap on the strip lights it");

            bool feet = glass.SounderPrefs.Feet;
            host.Apply(glass, glass.SounderPrefs, in sounder, in finder, FishFinderOverlayLayout.RulerHit);
            Assert.That(glass.SounderPrefs.Feet, Is.EqualTo(!feet), "a tap on the ruler flips the units");

            bool id = host.FishId;
            saves = _save.Saves;
            host.Apply(glass, glass.SounderPrefs, in sounder, in finder, FishFinderOverlayLayout.SonarHit);
            Assert.That(host.FishId, Is.EqualTo(!id), "a tap on the glass toggles fish-ID");
            Assert.That(_save.Saves, Is.EqualTo(saves), "…which is transient, not a saved preference");

            host.Apply(glass, glass.SounderPrefs, in sounder, in finder, -1);      // a miss does nothing
            Assert.That(host.FishId, Is.EqualTo(!id));
        }

        [UnityTest]
        public IEnumerator TheRange_RidesTheHull_AndSurvivesAReload()
        {
            var (_, boat, relay) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = false;
            boat.SetHull(NewConsoleHull("boat.test_range"));
            InstrumentLocker.Add(_save.Current, "boat.test_range", BoatEquipment.FishFinderId);
            IHelmInstruments glass = GameServices.HelmInstruments;

            SounderPrefs p = glass.SounderPrefs;
            p.RangeMetres = 60f;
            glass.SetSounderPrefs(in p);
            Assert.That(glass.SounderPrefs.RangeMetres, Is.EqualTo(60f).Within(1e-4f));

            // …through the locker, so it is on the save rather than in the host.
            SounderPrefs stored = InstrumentLocker.PrefsFor(_save.Current, "boat.test_range",
                                                           new SounderPrefs(0f, false, false, false, 1f));
            Assert.That(stored.RangeMetres, Is.EqualTo(60f).Within(1e-4f));

            // A round-trip through the save format keeps it (schema v9).
            string json = SaveSerialization.ToJson(_save.Current);
            SaveData back = SaveSerialization.FromJson(json);
            Assert.That(InstrumentLocker.PrefsFor(back, "boat.test_range",
                            new SounderPrefs(0f, false, false, false, 1f)).RangeMetres,
                        Is.EqualTo(60f).Within(1e-4f));

            // Another hull's finder is its own.
            boat.SetHull(NewConsoleHull("boat.test_range_other"));
            Assert.That(glass.SounderPrefs.RangeMetres,
                        Is.EqualTo(GameServices.FishFinder.DefaultRangeMetres).Within(1e-4f),
                        "preferences ride the hull, not the player");
        }

        // Wait REAL elapsed seconds — headless frames are not time.
        private static IEnumerator WaitSeconds(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
        }
    }
}
