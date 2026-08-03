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
    /// On-water integration for the ADR 0025 S2 depth sounder: the LIVE tide + terrain services reaching
    /// the instrument through the Core seam, the throttled sounding tick, the equipment gate flipping the
    /// moment a purchase is recorded (no rebuild, no re-wire), and the card host showing/hiding with the
    /// fit. This is the half EditMode structurally cannot see —
    /// <see cref="HelmControlRelay"/> claims its Core slots in <c>OnEnable</c>, which never fires in
    /// EditMode.
    ///
    /// <para><b>Time, not frames.</b> The throttle test waits on ELAPSED <c>Time.time</c>, never a frame
    /// count: headless frames run as fast as the machine allows, so "60 frames" can be 45 ms and a
    /// frame-counted wait would assert nothing about a 0.25 s interval.</para>
    /// </summary>
    public class SounderPlayTests
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

        private readonly List<Object> _spawned = new();
        private FlatTerrain _terrain;
        private FlatEnv _env;
        private FakeSaveService _save;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
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
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private BoatHullDef NewConsoleHull(string id, SounderKind defaultSounder)
        {
            var console = ScriptableObject.CreateInstance<HelmConsoleDef>();
            console.Id = "helm.test_console";
            console.Rig = ConsoleRigKind.Console;
            console.DefaultSounder = defaultSounder;
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
            var go = new GameObject("SounderTestBoat");
            var boat = go.AddComponent<BoatController>();   // Awake self-adds the relay
            var col = go.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, 4.0f);
            _spawned.Add(go);
            return (go, boat, go.GetComponent<HelmControlRelay>());
        }

        // ---- the reading is the sea ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheSounding_ComesFromTheLiveTideAndTerrain_ThroughTheCoreSeam()
        {
            var (_, boat, relay) = NewBoat();
            yield return null;                       // Awake + OnEnable have run
            relay.DevIgnoreEquipmentGating = false;
            boat.SetHull(NewConsoleHull("boat.test_sounder", SounderKind.Depth));

            IHelmInstruments glass = GameServices.HelmInstruments;
            Assert.That(glass, Is.Not.Null, "the relay registered the S2 instrument seam in OnEnable");
            Assert.That(glass.HullId, Is.EqualTo("boat.test_sounder"));
            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.Depth));

            Assert.That(glass.TryReadDepth(out float depth), Is.True);
            Assert.That(depth, Is.EqualTo(TidalExposure.WaterDepth(_env.Level, _terrain.Elevation))
                                  .Within(1e-4f),
                        "the glass reads exactly the shared water-depth rule, not a copy of it");
            Assert.That(depth, Is.EqualTo(5.2f).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator Aground_TheGlassReadsZero_NeverANegativeSounding()
        {
            var (_, boat, relay) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = false;
            boat.SetHull(NewConsoleHull("boat.test_aground", SounderKind.Depth));

            _terrain.Elevation = 2.5f;               // the bar is above the tide — dry ground
            _env.Level = 1.2f;

            IHelmInstruments glass = GameServices.HelmInstruments;
            yield return WaitSeconds(GameServices.DepthSounder.ReadIntervalSec * 2f);
            Assert.That(glass.TryReadDepth(out float depth), Is.True);
            Assert.That(depth, Is.EqualTo(0f), "hard aground reads 0.0");
            Assert.That(TidalExposure.IsExposed(_env.Level, _terrain.Elevation), Is.True,
                        "…which is the same fact the walkability sim reports");
        }

        [UnityTest]
        public IEnumerator TheSounding_IsThrottled_NotTakenEveryFrame()
        {
            var (_, boat, relay) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = false;
            boat.SetHull(NewConsoleHull("boat.test_throttle", SounderKind.Depth));
            IHelmInstruments glass = GameServices.HelmInstruments;

            glass.TryReadDepth(out float first);     // primes the tick
            float interval = GameServices.DepthSounder.ReadIntervalSec;

            // Move the seabed a long way and poll HARD inside the interval — the cached sounding stands.
            _terrain.Elevation = -20f;
            float t0 = Time.time;
            bool changedEarly = false;
            while (Time.time - t0 < interval * 0.4f)
            {
                glass.TryReadDepth(out float d);
                if (!Mathf.Approximately(d, first)) changedEarly = true;
                yield return null;
            }
            Assert.That(changedEarly, Is.False,
                        "the tide moves in minutes — the transducer does not re-sound every frame (rule 7)");

            // Past the interval it picks the new bottom up on its own.
            yield return WaitSeconds(interval * 1.5f);
            glass.TryReadDepth(out float later);
            Assert.That(later, Is.EqualTo(21.2f).Within(1e-3f), "and the next tick reads the new bottom");
        }

        // ---- the equipment gate --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator APurchase_FlipsTheFit_WithNoRebuild()
        {
            var (_, boat, relay) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = false;                 // shipped-build gating
            boat.SetHull(NewConsoleHull("boat.test_buy", SounderKind.None));   // a bare brow

            IHelmInstruments glass = GameServices.HelmInstruments;
            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.None),
                        "nothing fitted until it is bought");

            InstrumentLocker.Add(_save.Current, "boat.test_buy", BoatEquipment.DepthSounderId);

            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.Depth),
                        "the fit is RESOLVED on read — the purchase lands with no re-wire");
            Assert.That(glass.Fit.Rig, Is.EqualTo(ConsoleRigKind.Console));
        }

        [UnityTest]
        public IEnumerator TheDevOverride_ShowsItOnEveryConsoledHull_ButNeverOnTheDory()
        {
            var (_, boat, relay) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = true;                  // the F-cycle posture
            boat.SetHull(NewConsoleHull("boat.test_dev", SounderKind.None));

            IHelmInstruments glass = GameServices.HelmInstruments;
            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.Depth),
                        "with the dev gate open a bare console shows the sounder");
            Assert.IsEmpty(_save.Current.HullInstruments,
                        "…without writing a purchase the player never made");

            // A hull with no console has no dash to mount anything in, dev flag or not.
            var bare = ScriptableObject.CreateInstance<BoatHullDef>();
            bare.Id = "boat.test_bare";
            bare.Propulsion = PropulsionType.Engine;
            bare.MassKg = 300f; bare.EnginePower = 200f; bare.RudderAuthority = 300f;
            bare.ForwardDrag = 100f; bare.LateralDrag = 200f;
            _spawned.Add(bare);
            boat.SetHull(bare);
            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.None));
            Assert.That(glass.Fit.HasConsole, Is.False);
        }

        // ---- preferences persist --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator PreferencesRideTheHull_AndPersist()
        {
            var (_, boat, relay) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = false;
            boat.SetHull(NewConsoleHull("boat.test_prefs", SounderKind.Depth));
            IHelmInstruments glass = GameServices.HelmInstruments;

            SounderPrefs defaults = glass.SounderPrefs;
            Assert.That(defaults.AlarmMetres,
                        Is.EqualTo(GameServices.DepthSounder.DefaultAlarmMetres).Within(1e-4f));

            glass.SetSounderPrefs(new SounderPrefs(6.5f, true, true, true));
            Assert.That(_save.Saves, Is.GreaterThan(0), "a changed set-point is written to disk");

            SounderPrefs back = glass.SounderPrefs;
            Assert.That(back.AlarmMetres, Is.EqualTo(6.5f).Within(1e-4f));
            Assert.That(back.Feet, Is.True);
            Assert.That(back.Night, Is.True);

            int saves = _save.Saves;
            glass.SetSounderPrefs(new SounderPrefs(6.5f, true, true, true));
            Assert.That(_save.Saves, Is.EqualTo(saves), "writing the same preferences costs no disk write");

            // The OTHER boat's glass is its own.
            boat.SetHull(NewConsoleHull("boat.test_prefs_other", SounderKind.Depth));
            Assert.That(glass.SounderPrefs.AlarmMetres,
                        Is.EqualTo(GameServices.DepthSounder.DefaultAlarmMetres).Within(1e-4f),
                        "preferences ride the hull, not the player");
        }

        // ---- the card host --------------------------------------------------------------------------

        /// <summary>The host self-installs once per play session, and a duplicate destroys itself in
        /// Awake — so drive the running one. (Falls back to spawning it if the bootstrap has not fired,
        /// e.g. a stripped test environment.)</summary>
        private SounderOverlayHost TheHost()
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

        [UnityTest]
        public IEnumerator TheCard_ShowsWithTheFit_AndStandsDownWithout()
        {
            SounderOverlayHost host = TheHost();

            var (_, boat, relay) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = false;
            boat.SetHull(NewConsoleHull("boat.test_card", SounderKind.None));   // bare brow

            yield return null;
            Assert.That(host.Showing, Is.False, "no instrument, no card");

            InstrumentLocker.Add(_save.Current, "boat.test_card", BoatEquipment.DepthSounderId);
            yield return null;
            Assert.That(host.Showing, Is.True, "the bought sounder puts a card on screen");
            Assert.That(host.Focused, Is.False, "and starts as the small dash card");

            // Pull the height map (leaving the region) — there is no sounding, so nothing to draw. Wait
            // past the throttle interval: the last sounding is legitimately still current until then.
            GameServices.TidalTerrain = null;
            yield return WaitSeconds(GameServices.DepthSounder.ReadIntervalSec * 1.5f);
            Assert.That(host.Showing, Is.False,
                        "with no transducer read the glass shows nothing rather than a stale number");
        }

        [UnityTest]
        public IEnumerator ThePushers_MovePreferences_ThroughTheSeam()
        {
            SounderOverlayHost host = TheHost();

            var (_, boat, relay) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = false;
            boat.SetHull(NewConsoleHull("boat.test_push", SounderKind.Depth));
            yield return null;

            IHelmInstruments glass = GameServices.HelmInstruments;
            DepthSounderSettings cfg = GameServices.DepthSounder;
            float start = glass.SounderPrefs.AlarmMetres;

            host.Apply(glass, glass.SounderPrefs, in cfg, 1);          // ALARM up
            Assert.That(glass.SounderPrefs.AlarmMetres,
                        Is.EqualTo(start + cfg.AlarmStepMetres).Within(1e-4f));

            host.Apply(glass, glass.SounderPrefs, in cfg, 2);          // ALARM down
            Assert.That(glass.SounderPrefs.AlarmMetres, Is.EqualTo(start).Within(1e-4f));

            bool feet = glass.SounderPrefs.Feet;
            host.Apply(glass, glass.SounderPrefs, in cfg, 0);          // SET = units
            Assert.That(glass.SounderPrefs.Feet, Is.EqualTo(!feet));

            bool night = glass.SounderPrefs.Night;
            host.Apply(glass, glass.SounderPrefs, in cfg, SounderOverlayLayout.LcdHit);
            Assert.That(glass.SounderPrefs.Night, Is.EqualTo(!night), "a tap on the glass lights it");

            host.Apply(glass, glass.SounderPrefs, in cfg, -1);         // a miss changes nothing
            Assert.That(glass.SounderPrefs.Night, Is.EqualTo(!night));
        }

        // Wait REAL elapsed seconds — headless frames are not time
        // ([[playmode-frame-count-is-not-time]]).
        private static IEnumerator WaitSeconds(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
        }
    }
}
