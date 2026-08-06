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
    /// On-water integration for the ADR 0025 S5 radar: fit → mount → sweep, on the two hulls that carry
    /// a brow radar station. This is the half EditMode structurally cannot see — the equipment gate
    /// flipping with no rebuild, the self-installing host actually drawing, the Core seam being read
    /// through <see cref="GameServices"/> rather than through a test double wired straight in, and the
    /// preferences surviving the round trip.
    ///
    /// <para><b>The differential guards are the load-bearing ones (the #421 lesson).</b> Night,
    /// orientation, range, TRANSMIT and the sea-clutter feed are all flags that compile, pass and can be
    /// quietly ignored. Each is asserted from what the LIVE host last rastered
    /// (<see cref="RadarOverlayHost.NightShown"/> and friends), and each has a negative control proving
    /// the probe can report the other answer too.</para>
    ///
    /// <para><b>The repaint guard is measured, not claimed.</b> A turning sweep is the one thing that
    /// genuinely wants to repaint every frame, so the rate cap and the "the coast is not re-scanned per
    /// repaint" claim are both read off counters the host increments on its real paint path.</para>
    /// </summary>
    public class RadarPlayTests
    {
        private const string Region = "region.test_harbour";
        private static readonly Rect Bounds = new Rect(0f, 0f, 760f, 520f);

        /// <summary>A shelving coast, so the land scan has a real shoreline to find.</summary>
        private sealed class ShelvingCoast : ITidalTerrain
        {
            public float ElevationAt(Vector2 p)
                => Mathf.Lerp(4f, -20f, Mathf.InverseLerp(60f, 620f, p.x));
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level;
            public float Sea01;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Vector2.zero, Vector2.zero, Level, SeaState.Calm, 1f, Sea01);
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

        private sealed class FakeBoatProbe : IActiveBoatService
        {
            public BoatKinematics Value = BoatKinematics.None;
            public bool HasActiveBoat => Value.HasBoat;
            public BoatKinematics Sample() => Value;
        }

        /// <summary>A hand-placed fleet on the Core seam — the same shape a real producer publishes, so
        /// the host is exercised through <see cref="GameServices.RadarContacts"/> rather than around it.</summary>
        private sealed class FakeSea : IRadarContacts
        {
            public readonly List<RadarContact> Out = new();
            public int Queries;

            public int ContactsAt(Vector2 worldPos, float rangeMetres, double gameSeconds,
                                  List<RadarContact> into)
            {
                Queries++;
                into?.Clear();
                if (into == null) return 0;
                int n = 0;
                foreach (RadarContact c in Out)
                {
                    if ((c.WorldPos - worldPos).sqrMagnitude > rangeMetres * rangeMetres) continue;
                    into.Add(c);
                    n++;
                }
                return n;
            }
        }

        private readonly List<Object> _spawned = new();
        private FakeSaveService _save;
        private FakeClock _clock;
        private FlatEnv _env;
        private FakeSea _sea;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            HelmInstrumentExpansion.Collapse();
            HelmKeyCapture.Reset();          // no test may start with the helm deaf
            _save = new FakeSaveService(SaveMigration.NewGame());
            _clock = new FakeClock();
            _env = new FlatEnv { Level = 0f, Sea01 = 0f };
            _sea = new FakeSea();
            GameServices.TidalTerrain = new ShelvingCoast();
            GameServices.Environment = _env;
            GameServices.Clock = _clock;
            GameServices.Save = _save;
            GameServices.ActiveBoat = new FakeBoatProbe
            {
                Value = new BoatKinematics(true, 40f, new Vector2(0f, 3f), 3f),
            };
            GameServices.RadarContacts = _sea;
            GameServices.CurrentRegionId = Region;
            GameServices.CurrentRegionBounds = Bounds;
        }

        [TearDown]
        public void TearDown()
        {
            HelmInstrumentExpansion.Collapse();
            HelmKeyCapture.Reset();
            GameServices.Reset();
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // ---- rig ----------------------------------------------------------------------------------

        private BoatHullDef NewHull(string id, ConsoleRigKind rig, bool supportsRadar,
                                    CompassMount compass = CompassMount.None)
        {
            var console = ScriptableObject.CreateInstance<HelmConsoleDef>();
            console.Id = "helm.test_" + rig;
            console.Rig = rig;
            console.DefaultSounder = SounderKind.None;
            console.SupportsFishFinder = true;
            console.SupportsRadar = supportsRadar;
            console.DefaultRadar = false;              // never fitted out of the box — it is bought
            console.DefaultCompass = compass;
            console.SupportsDomeCompass = true;
            console.SupportsFlushCompass = true;
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

        /// <summary>Stand a piloted, radar-capable hull up and (optionally) fit the set.</summary>
        private IEnumerator Aboard(string hullId, ConsoleRigKind rig, bool fitRadar,
                                   bool supportsRadar = true, CompassMount compass = CompassMount.None)
        {
            var go = new GameObject("RadarTestBoat");
            go.transform.position = new Vector3(400f, 300f, 0f);
            var boat = go.AddComponent<BoatController>();      // Awake self-adds the relay
            var col = go.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, 4.0f);
            _spawned.Add(go);

            yield return null;                                  // Awake + OnEnable have run
            go.GetComponent<HelmControlRelay>().DevIgnoreEquipmentGating = false;
            boat.SetHull(NewHull(hullId, rig, supportsRadar, compass));
            if (fitRadar) InstrumentLocker.Add(_save.Current, hullId, BoatEquipment.RadarId);
            yield return null;
            yield return null;                                  // one frame to resolve, one to paint
        }

        private static RadarOverlayHost Host => RadarOverlayHost.Instance;

        private static RadarSettings Cfg => GameServices.Radar;

        private static RadarPrefs Stored()
        {
            RadarSettings cfg = Cfg;
            return InstrumentLocker.RadarPrefsFor(GameServices.Save.Current,
                                                  GameServices.HelmInstruments.HullId,
                                                  RadarPrefs.FromDefaults(in cfg));
        }

        /// <summary>Spin until the host has actually RASTERED again (or give up).
        ///
        /// <para>⚠ Needed because the differential probes report what the last raster used, and the
        /// world's motion is rate-capped (<see cref="RadarSettings.SweepRepaintHz"/>) — so "two frames
        /// later" is not "after a repaint". A CONTROL change repaints immediately and does not need
        /// this; a contact arriving or the sea getting up does.</para></summary>
        private static IEnumerator NextRaster()
        {
            int was = Host.RepaintCount;
            for (int i = 0; i < 600 && Host.RepaintCount == was; i++) yield return null;
        }

        private static void Store(RadarPrefs p)
            => InstrumentLocker.SetRadarPrefs(GameServices.Save.Current,
                                              GameServices.HelmInstruments.HullId, in p);

        // ---- fit → mount ---------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AFreshNoviHasNoScope_AndFittingOneMountsItWithNoRebuild()
        {
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: false);
            Assert.IsFalse(Host.Showing, "an unfitted brow shows no instrument");

            InstrumentLocker.Add(_save.Current, "boat.novi", BoatEquipment.RadarId);
            yield return null;
            yield return null;

            Assert.IsTrue(Host.Showing, "the fit resolves fresh every frame — no rebuild, no reload");
            Assert.Greater(Host.RepaintCount, 0, "…and it has actually rastered something");

            // Flush-mounting is "iff there is a dash to mount IN" — asserted as that relationship rather
            // than as a bare true, because whether this harness brings a helm dash up is the dash host's
            // business, not the radar's. The dome-compass test below is the case that matters.
            bool dashUp = HelmOverlayHost.TryDashCard(out _, out _);
            Assert.AreEqual(dashUp, Host.FlushMounted,
                            "flush in the brow whenever a dash is published (S4.5), its own card otherwise");
        }

        [UnityTest]
        public IEnumerator ACapeIslanderCarriesItToo_AndASkiffCannot()
        {
            yield return Aboard("boat.cape", ConsoleRigKind.Cape, fitRadar: true);
            Assert.IsTrue(Host.Showing);
            Assert.AreEqual(HelmOverlayHost.TryDashCard(out _, out _), Host.FlushMounted);

            TearDown();
            SetUp();
            yield return Aboard("boat.skiff", ConsoleRigKind.Console, fitRadar: true, supportsRadar: false);
            Assert.IsFalse(Host.Showing,
                           "a centre-console skiff has no radar station — owning the unit changes nothing");
        }

        [UnityTest]
        public IEnumerator ADomeCompassTakesTheStation_SoTheScopeHasNoFlushMountButStillOpens()
        {
            // The one shipped case where a fitted instrument legitimately has nowhere to sit flush.
            yield return Aboard("boat.novi_dome", ConsoleRigKind.Novi, fitRadar: true,
                                compass: CompassMount.Dome);
            Assert.IsTrue(Host.Showing, "the set is fitted and must still be usable");
            Assert.IsFalse(Host.FlushMounted,
                           "…but the dome binnacle owns the centre brow station (noviRig.js:453)");
        }

        // ---- the differential probes ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator TransmitReachesTheRaster_AndSoDoesStandby()
        {
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: true);

            Store(Stored().WithMode(RadarMode.HeadUp));
            yield return null; yield return null;
            Assert.IsTrue(Host.TransmittingShown, "a set on air must RASTER as on air");

            Store(Stored().WithMode(RadarMode.Standby));
            yield return null; yield return null;
            Assert.IsFalse(Host.TransmittingShown,
                           "…and the negative control: standby must reach the raster too, or the " +
                           "probe above proves nothing");
        }

        [UnityTest]
        public IEnumerator NightAndOrientationAndRangeAllReachTheRaster()
        {
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: true);

            Assert.IsFalse(Host.NightShown, "noon, no override — the day phosphor");
            Store(Stored().WithNight(true));
            yield return null; yield return null;
            Assert.IsTrue(Host.NightShown, "the night override must reach the raster (the #421 flag)");

            Store(Stored().WithMode(RadarMode.HeadUp));
            yield return null; yield return null;
            Assert.IsTrue(Host.HeadUpShown);
            Store(Stored().WithMode(RadarMode.NorthUp));
            yield return null; yield return null;
            Assert.IsFalse(Host.HeadUpShown, "north-up must reach the raster as well as head-up");

            RadarSettings cfg = Cfg;
            float before = Host.RangeShown;
            Store(Stored().SteppedRange(-1, in cfg));       // OUT — a wider range
            yield return null; yield return null;
            Assert.Greater(Host.RangeShown, before, "RANGE must reach the raster");
        }

        [UnityTest]
        public IEnumerator TheSeaStateReachesTheGlass_WhichIsTheWeathersOnlyMarkOnIt()
        {
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: true);
            Assert.AreEqual(0f, Host.SeaClutterShown, 1e-4f, "flat calm speckles nothing");

            _env.Sea01 = 1f;
            yield return NextRaster();
            Assert.Greater(Host.SeaClutterShown, 0f,
                           "a full gale must throw clutter — the instrument answering to the weather");
            Assert.LessOrEqual(Host.SeaClutterShown, Cfg.SeaClutterAtFullSeaState + 1e-4f,
                               "…bounded by the owner's dial, never past it");

            _env.Sea01 = 0f;
            yield return NextRaster();
            Assert.AreEqual(0f, Host.SeaClutterShown, 1e-4f, "…and it cleans off again");
        }

        // ---- the contacts really are the world's -----------------------------------------------------

        [UnityTest]
        public IEnumerator ContactsOnTheCoreSeamAppearOnTheGlass_AtTheRightBearingAndRange()
        {
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: true);
            Store(Stored().WithMode(RadarMode.NorthUp));
            yield return null; yield return null;

            int coastOnly = Host.BlipsShown;
            Assert.Greater(_sea.Queries, 0, "the host must actually ASK the seam");

            // A boat 100 m due EAST of own ship at (400,300).
            _sea.Out.Add(new RadarContact(new Vector2(500f, 300f), RadarEchoKind.Vessel, 1.05f, 180f, 4f));
            yield return NextRaster();

            Assert.AreEqual(coastOnly + 1, Host.BlipsShown,
                            "a contact on the seam must reach the list the raster consumed");

            RadarBlip found = default;
            bool got = false;
            foreach (RadarBlip b in Host.Blips)
                if (b.Kind == RadarEchoKind.Vessel) { found = b; got = true; }
            Assert.IsTrue(got);
            Assert.AreEqual(90f, found.BearingTrue, 0.5f,
                            "due east is 090° in the ONE bearing convention (0 = N, clockwise)");
            Assert.AreEqual(NavMath.MetresToNM(100f), found.RangeNM, 1e-3f);
            Assert.Greater(found.TrailScale, 0f, "a boat making way drags an afterglow");
        }

        [UnityTest]
        public IEnumerator AContactBeyondTheRangeIsNotOnTheGlass_UntilYouRangeOut()
        {
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: true);
            RadarSettings cfg = Cfg;
            Store(new RadarPrefs(false, RadarMode.NorthUp, 0));      // the closest rung: 0.05 NM ≈ 93 m
            yield return null; yield return null;

            int before = Host.BlipsShown;
            _sea.Out.Add(new RadarContact(new Vector2(400f, 600f), RadarEchoKind.Vessel, 1.05f, 180f, 4f));
            yield return NextRaster();
            Assert.AreEqual(before, Host.BlipsShown, "300 m is outside a 93 m scope");

            Store(new RadarPrefs(false, RadarMode.NorthUp, cfg.SafeRangeStepCount - 1));
            yield return NextRaster();
            Assert.Greater(Host.BlipsShown, before, "…and ranging OUT brings her on");
        }

        [UnityTest]
        public IEnumerator WithNoProducerRegistered_TheScopeStillDrawsTheCoast()
        {
            // The null object's whole point: no producer is a QUIET sea, not a dead instrument, and land
            // comes from the terrain rather than from the seam so the picture is still honest.
            GameServices.RadarContacts = null;
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: true);

            Assert.IsTrue(Host.Showing);
            Assert.Greater(Host.RepaintCount, 0);
            Assert.Greater(Host.BlipsShown, 0,
                           "the shelving coast to the west must still paint with no contact producer");
            Assert.Greater(Host.LandScanCount, 0);
        }

        // ---- rule 7 --------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheSweepIsRateCapped_AndTheCoastIsNotRescannedPerRepaint()
        {
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: true);
            Store(Stored().WithMode(RadarMode.HeadUp));
            yield return null; yield return null;

            int repaints0 = Host.RepaintCount, scans0 = Host.LandScanCount;
            float t0 = Time.unscaledTime;
            for (int i = 0; i < 60; i++) yield return null;
            float elapsed = Mathf.Max(1e-3f, Time.unscaledTime - t0);

            int repaints = Host.RepaintCount - repaints0;
            int scans = Host.LandScanCount - scans0;

            // ⚠ Frames are not time (headless `yield return null` runs far faster than real seconds), so
            // the bound is taken against the ELAPSED wall clock the cap is actually written in, with a
            // frame's slack either side.
            float allowed = Cfg.SafeSweepRepaintHz * elapsed + 3f;
            Assert.LessOrEqual(repaints, Mathf.CeilToInt(allowed),
                               $"the sweep must be rate-capped, not per-frame ({repaints} rasters in " +
                               $"{elapsed:F3}s against a {Cfg.SafeSweepRepaintHz} Hz budget)");
            Assert.AreEqual(0, scans,
                            "a boat that has not moved must not re-scan the coast, however many times " +
                            "the sweep repaints");
        }

        [UnityTest]
        public IEnumerator AStandbySetCostsFarLessThanATransmittingOne()
        {
            // ⚠ NOT "exactly zero": the boat is a live hull and it drifts, so its contacts genuinely
            // re-measure and the scope genuinely has a new picture to draw. The claim is that removing
            // the SWEEP removes the dominant source of change — and that neither state is per-frame.
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: true);

            Store(Stored().WithMode(RadarMode.HeadUp));
            yield return null; yield return null;
            int txBefore = Host.RepaintCount;
            for (int i = 0; i < 60; i++) yield return null;
            int tx = Host.RepaintCount - txBefore;

            Store(Stored().WithMode(RadarMode.Standby));
            yield return null; yield return null;
            int stbyBefore = Host.RepaintCount;
            for (int i = 0; i < 60; i++) yield return null;
            int stby = Host.RepaintCount - stbyBefore;

            Assert.LessOrEqual(stby, tx,
                               $"standby ({stby}) must never cost more than transmitting ({tx})");
            Assert.Less(stby, 60, "…and neither state may repaint once per frame");
            Assert.Less(tx, 60);
        }

        // ---- preferences persist ----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator PreferencesArePerHull_AndSurviveSteppingOffAndBackOn()
        {
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: true);
            Store(new RadarPrefs(true, RadarMode.NorthUp, 4));
            yield return null; yield return null;
            Assert.IsTrue(Host.NightShown);
            Assert.IsFalse(Host.HeadUpShown);

            RadarPrefs kept = InstrumentLocker.RadarPrefsFor(_save.Current, "boat.novi",
                                                             RadarPrefs.FromDefaults(Cfg));
            Assert.IsTrue(kept.Night);
            Assert.AreEqual(RadarMode.NorthUp, kept.Mode);
            Assert.AreEqual(4, kept.RangeStep);

            RadarPrefs other = InstrumentLocker.RadarPrefsFor(_save.Current, "boat.some_other_hull",
                                                              RadarPrefs.FromDefaults(Cfg));
            Assert.IsFalse(other.Night, "another hull's set is another hull's — these are per-hull");
        }

        [UnityTest]
        public IEnumerator APusherPress_FLUSHESTheSave_NotJustTheInMemoryCopy()
        {
            // ⚠ The half that would look perfectly fine all session and be gone on the next load:
            // InstrumentLocker writes into the in-memory SaveData; only ISaveService.Save() commits it.
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: true);
            IHelmInstruments glass = GameServices.HelmInstruments;
            RadarSettings cfg = Cfg;

            int saves = _save.Saves;
            Assert.IsTrue(Host.Apply(glass, _save.Current, Stored().SteppedMode()),
                          "a mode step really is a change");
            Assert.AreEqual(saves + 1, _save.Saves, "…and a change must be COMMITTED, not just recorded");

            // …and the no-op control: a range press at the end of the ladder must not dirty the save.
            Store(new RadarPrefs(false, RadarMode.HeadUp, 0));
            saves = _save.Saves;
            Assert.IsFalse(Host.Apply(glass, _save.Current, Stored().SteppedRange(+1, in cfg)),
                           "a set already at its closest range does not move");
            Assert.AreEqual(saves, _save.Saves, "…so nothing is written and nothing is flushed");
        }

        // ---- the dev cycle -----------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ShiftK_FitsTheScopeWithoutTouchingTheSave()
        {
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: false);
            HelmControlRelay relay = Object.FindAnyObjectByType<HelmControlRelay>();
            var cycle = relay.GetComponent<DevInstrumentCycle>();
            Assert.IsNotNull(cycle, "the cycle self-installs from BoatController's Awake");

            relay.DevIgnoreEquipmentGating = true;
            cycle.StepPilot();                                    // → gps
            cycle.StepPilot();                                    // → gps + radar
            yield return null; yield return null;

            Assert.IsTrue(GameServices.HelmInstruments.Fit.Radar, "SHIFT+K must fit the scope");
            Assert.IsTrue(Host.Showing);
            Assert.IsFalse(InstrumentLocker.Owns(_save.Current, "boat.novi", BoatEquipment.RadarId),
                           "…and it must NEVER reach the player's ownership record");
            Assert.IsFalse(InstrumentLocker.Owns(_save.Current, "boat.novi", BoatEquipment.GpsId));

            cycle.StepPilot();                                    // → radar alone
            yield return null;
            Assert.IsTrue(GameServices.HelmInstruments.Fit.Radar);
            Assert.IsFalse(GameServices.HelmInstruments.Fit.Gps,
                           "the ring reaches the scope on its own — the whole point of a cycle");

            cycle.StepPilot();                                    // → as owned (nothing bought here)
            yield return null; yield return null;
            Assert.IsFalse(GameServices.HelmInstruments.Fit.Radar,
                           "the ring closes on 'as owned', which on an unbought hull is a bare deck");
            Assert.IsFalse(Host.Showing);
        }

        [UnityTest]
        public IEnumerator ShiftK_LeavesTheBrowCycleAlone()
        {
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: false);
            HelmControlRelay relay = Object.FindAnyObjectByType<HelmControlRelay>();
            var cycle = relay.GetComponent<DevInstrumentCycle>();
            relay.DevIgnoreEquipmentGating = true;

            cycle.Step();                                         // brow → fish finder (from Depth)
            SounderKind brow = relay.DevBrowStep;
            cycle.StepPilot();
            cycle.StepPilot();
            yield return null;

            Assert.AreEqual(brow, relay.DevBrowStep, "two rings, two axes — one must not move the other");
            Assert.AreEqual(brow, GameServices.HelmInstruments.Fit.Sounder);
        }

        [UnityTest]
        public IEnumerator TypingOnTheChartplotterDeafensTheCycleKey()
        {
            // W/A/S/D are helm keys AND letters; K is a letter too, and "KELP ROCK" would otherwise
            // re-fit the dash under an open name editor.
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: true);
            HelmControlRelay relay = Object.FindAnyObjectByType<HelmControlRelay>();
            relay.DevIgnoreEquipmentGating = true;

            HelmKeyCapture.Begin();
            Assert.IsTrue(HelmKeyCapture.IsCapturing);
            yield return null;
            yield return null;
            Assert.IsTrue(Host.Showing, "the instrument is unaffected — only the KEY goes deaf");
            HelmKeyCapture.End();
            Assert.IsFalse(HelmKeyCapture.IsCapturing, "…and the gate balances, never wedging the helm");
        }

        // ---- expansion ---------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator OnlyOneInstrumentIsEverExpanded()
        {
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: true);
            Assert.IsFalse(Host.Expanded, "flush is the default (S4.5)");

            HelmInstrumentExpansion.Click(HelmInstrumentSlot.Radar);
            yield return null;
            Assert.IsTrue(Host.Expanded);
            Assert.IsFalse(Host.FlushMounted, "expanded is its own card, not the brow mount");

            HelmInstrumentExpansion.Click(HelmInstrumentSlot.Chartplotter);
            yield return null;
            Assert.IsFalse(Host.Expanded, "a second instrument opening closes this one");
        }

        [UnityTest]
        public IEnumerator BoardingAHullWithNoScopeCollapsesIt()
        {
            // The real "losing the glass" case: instruments are owned PER HULL, so stepping onto a boat
            // whose helm has no radar station takes the scope away even though the unit is still bought.
            yield return Aboard("boat.novi", ConsoleRigKind.Novi, fitRadar: true);
            HelmInstrumentExpansion.Click(HelmInstrumentSlot.Radar);
            Host.StepCursorForTest(true, 45f, 0.1f);
            yield return null;
            Assert.IsTrue(Host.Expanded);
            Assert.IsTrue(Host.Showing);

            BoatController boat = Object.FindAnyObjectByType<BoatController>();
            boat.SetHull(NewHull("boat.skiff", ConsoleRigKind.Console, supportsRadar: false));
            yield return null;
            yield return null;

            Assert.IsFalse(Host.Showing, "no station on this hull, no scope");
            Assert.IsFalse(Host.Expanded, "an instrument you are not standing at cannot stay open");
        }
    }
}
