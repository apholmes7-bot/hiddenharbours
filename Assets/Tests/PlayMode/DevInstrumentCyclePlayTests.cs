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
    /// The DEV BROW CYCLE on the water (ADR 0025 S3d): the key stepping the LIVE fit through the Core
    /// seam, the two instrument hosts handing the brow cutout over cleanly, the chosen tier riding an
    /// F-swap, and — the guard the whole slice rests on — the save staying untouched by a dev convenience.
    ///
    /// <para>This is the half EditMode structurally cannot see. <see cref="HelmControlRelay"/> claims its
    /// Core slots in <c>OnEnable</c>, which never fires in EditMode; and the two overlay hosts only decide
    /// who owns the cutout inside their own <c>Update</c>, so a handover bug is invisible to a pure
    /// function test.</para>
    /// </summary>
    public class DevInstrumentCyclePlayTests
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

        /// <summary>One school, planted at a stated place — enough to prove the dev log READS the seam at
        /// the transducer's own position and reports what is there. It never moves and never spawns; the
        /// cycle is a spectator.</summary>
        private sealed class PlantedSchool : IFishSchools
        {
            public FishSchool School;
            public bool Present;
            public int SchoolCalls;
            public Vector2 LastQueryPos;

            public int SchoolsAt(Vector2 worldPos, double gameSeconds, List<FishSchool> into)
            {
                SchoolCalls++;
                LastQueryPos = worldPos;
                into?.Clear();
                if (!Present) return 0;
                into?.Add(School);
                return 1;
            }

            public int MarksAt(Vector2 worldPos, double gameSeconds, List<FishMark> into)
            {
                into?.Clear();
                if (!Present) return 0;
                into?.Add(School.ToMark(worldPos));
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

        private BoatHullDef NewConsoleHull(string id, SounderKind defaultSounder = SounderKind.None,
                                           bool fishSlot = true)
        {
            var console = ScriptableObject.CreateInstance<HelmConsoleDef>();
            console.Id = "helm.test_" + id;
            console.Rig = ConsoleRigKind.Console;
            console.DefaultSounder = defaultSounder;
            console.SupportsFishFinder = fishSlot;
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

        private (GameObject go, BoatController boat, HelmControlRelay relay, DevInstrumentCycle cycle) NewBoat()
        {
            var go = new GameObject("CycleTestBoat");
            var boat = go.AddComponent<BoatController>();   // Awake self-adds the relay AND the cycle
            var col = go.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, 4.0f);
            _spawned.Add(go);
            return (go, boat, go.GetComponent<HelmControlRelay>(), go.GetComponent<DevInstrumentCycle>());
        }

        private FishFinderOverlayHost TheFinderHost()
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

        // ---- the cycle itself ---------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheComponent_SelfInstalls_OnEveryBoat_WithNoBuilderReRun()
        {
            var (_, _, relay, cycle) = NewBoat();
            yield return null;
            Assert.That(relay, Is.Not.Null, "the S1 relay still self-installs");
            Assert.That(cycle, Is.Not.Null,
                        "…and so does the S3d cycle — an already-built scene grows it on load");
        }

        [UnityTest]
        public IEnumerator TheCycle_StartsWhereS2LeftIt_TheDepthSounder()
        {
            var (_, boat, relay, _) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = true;
            boat.SetHull(NewConsoleHull("boat.test_start"));

            Assert.That(GameServices.HelmInstruments.Fit.Sounder, Is.EqualTo(SounderKind.Depth),
                        "F alone must still show what it showed before this slice existed — the S2 dev " +
                        "bypass granted exactly the depth sounder, so the ring starts there");
        }

        [UnityTest]
        public IEnumerator TheCycle_WalksTheBrow_ThroughEveryShippedInstrument()
        {
            var (_, boat, relay, cycle) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = true;
            boat.SetHull(NewConsoleHull("boat.test_walk"));
            IHelmInstruments glass = GameServices.HelmInstruments;

            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.Depth));
            cycle.Step();
            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.Fish), "one press fits the colour finder");
            cycle.Step();
            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.None), "the next takes the brow back to bare");
            cycle.Step();
            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.Depth), "…and the ring closes");
        }

        [UnityTest]
        public IEnumerator TheCycle_TakesTheBrowBack_EvenOnAHullThatShipsASounder()
        {
            // The Novi and Cape consoles carry a depth sounder in their AUTHORED default. A dev override
            // built only from granted ids could never show their brow bare — this is the narrowing half.
            var (_, boat, relay, cycle) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = true;
            boat.SetHull(NewConsoleHull("boat.test_ships_one", SounderKind.Depth));
            IHelmInstruments glass = GameServices.HelmInstruments;

            cycle.Step();                                              // Depth → Fish
            cycle.Step();                                              // Fish  → None
            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.None),
                        "a working boat's own sounder can still be stepped off the glass in dev");
        }

        [UnityTest]
        public IEnumerator ADoryHasNoBrow_AndTheCycleSaysSo_RatherThanEatingThePress()
        {
            var (_, boat, relay, cycle) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = true;

            var bare = ScriptableObject.CreateInstance<BoatHullDef>();
            bare.Id = "boat.test_no_console";
            bare.Propulsion = PropulsionType.Engine;
            bare.MassKg = 300f; bare.EnginePower = 200f; bare.RudderAuthority = 300f;
            bare.ForwardDrag = 100f; bare.LateralDrag = 200f;
            _spawned.Add(bare);
            boat.SetHull(bare);

            SounderKind before = relay.DevBrowStep;
            cycle.Step();
            Assert.That(relay.DevBrowStep, Is.EqualTo(before),
                        "no console, no step — the tier is not silently spun on a hull with no dash");
            Assert.That(GameServices.HelmInstruments.Fit.HasConsole, Is.False);
            Assert.That(GameServices.HelmInstruments.Fit.Sounder, Is.EqualTo(SounderKind.None));
        }

        // ---- T1: the save is never touched ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheCycle_NeverWritesTheSave_NoMatterHowFarItIsWalked()
        {
            // ⚠ THE guard. A dev convenience that reached InstrumentLocker would permanently fit
            // instruments the player never bought, in their real save file. Verified RED first by making
            // Step() call InstrumentLocker.Add — see the PR body.
            var (_, boat, relay, cycle) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = true;
            boat.SetHull(NewConsoleHull("boat.test_save"));

            SaveData save = _save.Current;
            Assert.That(save.HullInstruments, Is.Empty, "precondition: nothing owned");
            int savesBefore = _save.Saves;

            for (int lap = 0; lap < 3; lap++)
                for (int step = 0; step < 3; step++)
                {
                    cycle.Step();
                    // Read the fit too: resolving it is the other half of the path that could pollute.
                    _ = GameServices.HelmInstruments.Fit;
                    Assert.That(save.HullInstruments, Is.Empty,
                                $"lap {lap} step {step}: a dev view of the brow is not a purchase");
                }

            Assert.That(save.HullInstruments, Is.Empty, "…and nine presses later the locker is still bare");
            Assert.That(_save.Saves, Is.EqualTo(savesBefore), "nor did it write anything to disk");
        }

        // ---- T2: dead in a shipped build -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator WithoutTheDevFlag_TheCycleChangesNothing_AShippedBuildIsUnreachable()
        {
            var (_, boat, relay, cycle) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = false;                       // the shipped-build posture
            boat.SetHull(NewConsoleHull("boat.test_shipped"));
            IHelmInstruments glass = GameServices.HelmInstruments;

            for (int i = 0; i < 4; i++)
            {
                cycle.Step();
                Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.None),
                            "with the dev gate shut the fit is ownership + the console default, full stop");
            }
            Assert.That(_save.Current.HullInstruments, Is.Empty, "…and no free instrument was granted");

            // The real gate still works underneath.
            InstrumentLocker.Add(_save.Current, "boat.test_shipped", BoatEquipment.DepthSounderId);
            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.Depth), "a genuine purchase still lands");
        }

        // ---- T4: the two hosts hand the cutout over ------------------------------------------------------

        [UnityTest]
        public IEnumerator TheTwoHosts_HandTheCutoutOver_AndAreNeverBothOnScreen()
        {
            // The cycle exercises the handover on EVERY press, in both directions — which the purchase
            // path (sounder → finder, one way, once) never did.
            SounderOverlayHost sounder = TheSounderHost();
            FishFinderOverlayHost finder = TheFinderHost();

            var (_, boat, relay, cycle) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = true;
            boat.SetHull(NewConsoleHull("boat.test_hosts"));
            yield return null;

            var expected = new[] { SounderKind.Depth, SounderKind.Fish, SounderKind.None };
            for (int i = 0; i < expected.Length * 2 + 1; i++)
            {
                SounderKind want = expected[i % expected.Length];
                Assert.That(GameServices.HelmInstruments.Fit.Sounder, Is.EqualTo(want),
                            $"press {i}: the brow says {want}");

                // Both hosts must SEE this frame before either is judged — each decides in its own Update.
                yield return null;
                yield return null;

                Assert.That(sounder.Showing && finder.Showing, Is.False,
                            $"press {i}: the two cards share ONE cutout and must never be on screen together");
                Assert.That(sounder.Showing, Is.EqualTo(want == SounderKind.Depth),
                            $"press {i}: the depth card is up exactly when the brow says Depth");
                Assert.That(finder.Showing, Is.EqualTo(want == SounderKind.Fish),
                            $"press {i}: the sonar card is up exactly when the brow says Fish");

                cycle.Step();
            }
        }

        // ---- the F-swap interaction ----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheChosenTier_CARRIES_AcrossAHullSwap()
        {
            // The ruled behaviour (S3d): set the instrument once, then walk the fleet with F comparing the
            // SAME glass across four dashes — the picker's own "hold everything constant but the hull".
            var (_, boat, relay, cycle) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = true;
            boat.SetHull(NewConsoleHull("boat.test_swap_a"));
            IHelmInstruments glass = GameServices.HelmInstruments;

            cycle.Step();                                                  // Depth → Fish
            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.Fish));

            boat.SetHull(NewConsoleHull("boat.test_swap_b", SounderKind.Depth));   // the F-swap
            Assert.That(glass.HullId, Is.EqualTo("boat.test_swap_b"), "precondition: the hull changed");
            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.Fish),
                        "the finder rides the swap — otherwise it is two extra presses on every hull");
        }

        [UnityTest]
        public IEnumerator ACarriedFinder_FallsBackOnAHullThatCannotTakeIt_WithoutForgettingIt()
        {
            var (_, boat, relay, cycle) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = true;
            boat.SetHull(NewConsoleHull("boat.test_capable"));
            IHelmInstruments glass = GameServices.HelmInstruments;

            cycle.Step();                                                  // Depth → Fish
            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.Fish));

            boat.SetHull(NewConsoleHull("boat.test_small_brow", SounderKind.None, fishSlot: false));
            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.Depth),
                        "a brow that cannot take the finder shows the unit it CAN carry, never a lie");
            Assert.That(relay.DevBrowStep, Is.EqualTo(SounderKind.Fish),
                        "…and the request is remembered, not destroyed");

            boat.SetHull(NewConsoleHull("boat.test_capable_again"));
            Assert.That(glass.Fit.Sounder, Is.EqualTo(SounderKind.Fish),
                        "so F back onto a capable hull restores it with no extra press");
        }

        // ---- the empty-sonar log -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator LandingOnTheFinder_NamesTheSeaUnderTheTransducer()
        {
            var planted = new PlantedSchool
            {
                Present = true,
                // Due NORTH of the boat, 20 m off, fish at 6 m — every number in the line is checkable.
                School = new FishSchool(new Vector2(0f, 20f), 40f, 6f, 4, null, -1d, 1e9d),
            };
            GameServices.FishSchools = planted;

            var (go, boat, relay, cycle) = NewBoat();
            yield return null;
            go.transform.position = Vector3.zero;
            relay.DevIgnoreEquipmentGating = true;
            boat.SetHull(NewConsoleHull("boat.test_log"));

            string line = cycle.ReportSchools();
            Assert.That(planted.SchoolCalls, Is.GreaterThan(0), "the seam was actually asked");
            Assert.That(planted.LastQueryPos, Is.EqualTo(Vector2.zero),
                        "…at the transducer's own position — the same point the finder draws from");
            StringAssert.Contains("1 school", line);
            StringAssert.Contains("0°", line, "due north reads as bearing 0");
            StringAssert.Contains("20.0 m", line, "the distance to the school's centre");
            StringAssert.Contains("6.0 m", line, "the depth the fish are at");
        }

        [UnityTest]
        public IEnumerator OverEmptyWater_TheLogSaysSo_SoAnEmptySonarIsNotMistakenForABrokenOne()
        {
            GameServices.FishSchools = new PlantedSchool { Present = false };

            var (_, boat, relay, cycle) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = true;
            boat.SetHull(NewConsoleHull("boat.test_log_empty"));

            string line = cycle.ReportSchools();
            StringAssert.Contains("NO school", line);
            Assert.That(_save.Current.HullInstruments, Is.Empty, "reading the sea is not a write");
        }

        [UnityTest]
        public IEnumerator TheLog_NeverSpawnsASchool_ItOnlyReads()
        {
            var planted = new PlantedSchool { Present = false };
            GameServices.FishSchools = planted;

            var (_, boat, relay, cycle) = NewBoat();
            yield return null;
            relay.DevIgnoreEquipmentGating = true;
            boat.SetHull(NewConsoleHull("boat.test_readonly"));

            for (int i = 0; i < 6; i++) cycle.Step();

            Assert.That(planted.Present, Is.False,
                        "the cycle is a spectator — it never spawns, moves or forces a school");
            Assert.That(GameServices.FishSchools, Is.SameAs(planted),
                        "…nor swaps the registered model out from under the fishing path");
        }
    }
}
