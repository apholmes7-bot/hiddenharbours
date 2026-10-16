#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // The same saved scene supplies play, plates and measurements. No neighbour is loaded.
    public abstract class RicePointSceneFixture
    {
        protected const string Evidence = "artifacts/rice-point/b";
        protected LandScenePlanDef Plan;
        protected Scene RegionScene;
        protected PlayerWalkController Walk;
        protected ControlSwitcher Switcher;
        protected BoatController Boat;
        protected HeldWalkIntents WalkInput;
        protected DevBoatInput Oars;
        protected string ColdMemory;
        readonly HashSet<GameObject> _resident = new();
        ISaveService _save;
        Scene _origin;

        sealed class MemorySave : ISaveService
        {
            public SaveData Current { get; } = SaveMigration.NewGame();
            readonly Dictionary<string, bool> _flags = new();
            public bool GetFlag(string key) => _flags.TryGetValue(key, out var value) && value;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() { }
        }

        [SetUp]
        public void RememberSession()
        {
            _origin = SceneManager.GetActiveScene();
            _resident.Clear();
            foreach (var root in PersistentRoots()) _resident.Add(root);
            _save = GameServices.Save;
            GameServices.Save = new MemorySave();
            ShellFlow.Reset();
            InteractionGate.Reset();
        }

        protected IEnumerator LoadDirect()
        {
            Assert.That(GameServices.Ready, Is.False, "Direct Play must begin without a core.");
            Plan = RicePointBuilder.LoadPlan();
            Assert.That(File.Exists(Plan.ScenePath), Is.True, "CREATE must precede this launch.");
            long allocatedBefore = Profiler.GetTotalAllocatedMemoryLong();
            string before = MemorySnapshot("before_load");
            yield return LoadDestination();
            for (int i = 0; i < 5; ++i) yield return null;
            Assert.That(GameServices.Ready, Is.True);
            Assert.That(Object.FindObjectsByType<GameRoot>().Length, Is.EqualTo(1));
            Walk = Object.FindFirstObjectByType<PlayerWalkController>();
            Switcher = Object.FindFirstObjectByType<ControlSwitcher>();
            Boat = Object.FindFirstObjectByType<BoatController>();
            Assert.That(Walk, Is.Not.Null); Assert.That(Switcher, Is.Not.Null); Assert.That(Boat, Is.Not.Null);
            Assert.That(Switcher.Mode, Is.EqualTo(ControlMode.OnFoot));
            WalkInput = new HeldWalkIntents();
            Walk.ConfigureWalkInput(WalkInput);
            Oars = Boat.GetComponent<DevBoatInput>();
            ColdMemory = before + "\n" + MemorySnapshot("after_load") + "\nscene_load_allocated_delta_bytes=" +
                (Profiler.GetTotalAllocatedMemoryLong() - allocatedBefore);
            SetHour(5 + 12 / 60d + 37 / 3600d);
            Assert.That(GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds),
                Is.EqualTo(0).Within(.02f), "The route uses the shipped mean-ebb tide.");
        }

        protected IEnumerator LoadDestination()
        {
            // A live coordinator switches off every root of the scene left behind; here that would be the runner.
            var leaving = SceneManager.GetActiveScene();
            var coordinator = Object.FindAnyObjectByType<RegionTravelCoordinator>();
            if (leaving == _origin && coordinator != null && coordinator.isActiveAndEnabled)
                Assert.Fail("LoadDestination would leave the test runner's scene '" + leaving.name +
                    "' while a RegionTravelCoordinator is live; it would switch the runner off and stop the test.");
            // Match the loader's scene-loaded activation, before destination Start callbacks.
            void Activate(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != Plan.Region.SceneName) return;
                RegionScene = scene;
                SceneManager.SetActiveScene(scene);
            }
            SceneManager.sceneLoaded += Activate;
            try { yield return SceneManager.LoadSceneAsync(Plan.Region.SceneName, LoadSceneMode.Additive); }
            finally { SceneManager.sceneLoaded -= Activate; }
        }

        protected static void SetHour(double hour)
        {
            GameServices.Clock.SeekTo(GameServices.Config.SecondsPerDay * hour / 24d);
            GameServices.Clock.IsPaused = true;
        }

        protected void StageStrand()
        {
            var arrival = Plan.Doors.Single(d => d.OwnArrival == "nmc_strand");
            Walk.transform.position = arrival.Disembark;
            Walk.GetComponent<Rigidbody2D>().position = arrival.Disembark;
            Physics2D.SyncTransforms();
        }

        protected IEnumerator WalkFor(float seconds, Vector2 direction)
        {
            WalkInput.Walk(direction);
            yield return null; // Input is read in Update, before the counted physics steps.
            int steps = Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
            for (int i = 0; i < steps; ++i) yield return new WaitForFixedUpdate();
            WalkInput.Release();
            yield return null;
        }

        protected IEnumerator BoardAtStart()
        {
            WalkInput.Release();
            Boat.Stop();
            Boat.GetComponent<Rigidbody2D>().position = Plan.BoatStart;
            Boat.transform.position = Plan.BoatStart;
            Walk.transform.position = Plan.BoatStart + Vector2.right * .5f;
            Walk.GetComponent<Rigidbody2D>().position = Walk.transform.position;
            Physics2D.SyncTransforms();
            Assert.That(Switcher.TryInteract(), Is.True, "Board the dory through the live boarding gate.");
            Assert.That(Switcher.Mode, Is.EqualTo(ControlMode.OnDeck));
            Walk.transform.position = Switcher.HelmWorldPosition;
            Walk.GetComponent<Rigidbody2D>().position = Walk.transform.position;
            Assert.That(Switcher.TryInteract(), Is.True, "Take the rowing station.");
            Assert.That(Switcher.Mode, Is.EqualTo(ControlMode.Aboard));
            Assert.That(Oars.RowingStationManned, Is.True);
            Oars.enabled = false; // Hardware polling must not overwrite scripted strokes.
            yield return null;
        }

        protected void StrokeToward(Vector2 target)
        {
            var body = Boat.GetComponent<Rigidbody2D>();
            Vector2 delta = target - body.position;
            float bearing = Mathf.Atan2(-delta.x, delta.y) * Mathf.Rad2Deg;
            float error = Mathf.DeltaAngle(body.rotation, bearing);
            float turn = Mathf.Clamp(error / 35f - body.angularVelocity / 90f, -1f, 1f);
            float ahead = Mathf.Abs(error) > 65f ? 0f : .7f;
            Oars.DriveOars(Mathf.Clamp(ahead - turn, -1, 1), Mathf.Clamp(ahead + turn, -1, 1), false);
        }

        protected IEnumerator RowTo(Vector2 target, float limitSeconds = 150)
        {
            float elapsed = 0;
            while (Vector2.Distance(Boat.transform.position, target) > 4 && elapsed < limitSeconds)
            {
                StrokeToward(target);
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }
            Oars.DriveOars(0, 0, true);
            Assert.That(Vector2.Distance(Boat.transform.position, target), Is.LessThanOrEqualTo(4),
                "The rowed route did not reach " + target + "; last position " + Boat.transform.position);
            Assert.That(Boat.IsAground, Is.False);
        }

        protected static string MemorySnapshot(string label)
        {
            long textures = Resources.FindObjectsOfTypeAll<Texture>().Sum(x => Profiler.GetRuntimeMemorySizeLong(x));
            long meshes = Resources.FindObjectsOfTypeAll<Mesh>().Sum(x => Profiler.GetRuntimeMemorySizeLong(x));
            return label + ": allocated_bytes=" + Profiler.GetTotalAllocatedMemoryLong() +
                ", reserved_bytes=" + Profiler.GetTotalReservedMemoryLong() +
                ", texture_bytes=" + textures + ", mesh_bytes=" + meshes;
        }

        static GameObject[] PersistentRoots() => Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(g => g.scene.IsValid() && g.scene.name == "DontDestroyOnLoad" && g.transform.parent == null).ToArray();

        [UnityTearDown]
        public IEnumerator LeaveSession()
        {
            WalkInput?.Release();
            if (Oars != null) Oars.DriveOars(0, 0, true);
            if (_origin.IsValid() && _origin.isLoaded) SceneManager.SetActiveScene(_origin);
            if (RegionScene.IsValid() && RegionScene.isLoaded) yield return SceneManager.UnloadSceneAsync(RegionScene);
            foreach (var root in PersistentRoots()) if (!_resident.Contains(root)) Object.Destroy(root);
            yield return null;
            GameServices.Reset();
            GameServices.Save = _save;
            ShellFlow.Reset();
            InteractionGate.Reset();
            Time.timeScale = 1;
        }
    }

    public sealed class RicePointPlayablePlayTests : RicePointSceneFixture
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator DirectPlay_OneCore_WalkStrand_RowPoint_CastHasNoEligibleSpecies()
        {
            yield return LoadDirect();
            StageStrand();
            Vector2 start = Walk.transform.position;
            yield return WalkFor(4, Vector2.left);
            Assert.That(start.x - Walk.transform.position.x, Is.GreaterThan(3));
            Assert.That(WalkInput.Reads, Is.GreaterThan(0));
            Assert.That(Walk.WaterState, Is.EqualTo(OnFootWaterState.Dry));
            yield return BoardAtStart();
            // Route clear of the ledges: down the gut, west past the point, then its outer west face.
            yield return RowTo(new Vector2(-53, -170));
            yield return RowTo(new Vector2(-185, -170));
            yield return RowTo(new Vector2(-225, -140));
            Boat.Stop();
            var fish = Boat.GetComponent<FishingController>();
            foreach (var species in Plan.DevFish)
                Assert.That(species.RegionIds, Does.Not.Contain(Plan.Region.Id), "Eligibility is reserved for RPT4.");
            Assert.That(fish.HasSomethingOnTheHook, Is.True);
            // The shared flick gesture, directed south into water from the live angler.
            Vector2 a = fish.Angler != null ? (Vector2)fish.Angler.position : (Vector2)fish.transform.position;
            foreach (float distance in new[] { -.5f, -1f, -1.5f, -.5f, .3f, 1f })
                fish.Tick(.02f, true, a + Vector2.down * distance, true);
            fish.Tick(.02f, false, a + Vector2.down, true);
            Assert.That(fish.LastCast.IsCast, Is.True);
            Assert.That(fish.Phase, Is.EqualTo(FishingPhase.Cast));
            for (int i = 0; i < 6000 && fish.Phase != FishingPhase.NoBite; ++i) fish.Tick(.05f, false);
            Assert.That(fish.Phase, Is.EqualTo(FishingPhase.NoBite));
            Assert.That(SceneManager.GetSceneByName("NineMileCreek").isLoaded, Is.False);
        }

        sealed class TestTruckSeat : IDriveSeat
        {
            public Transform Root { get; set; }
            public string VehicleId => "vehicle.rice_point_test_truck";
            public bool IsDrivable => true;
            public bool IsAlive => Root != null;
            public float CameraWorldHeightMeters => 18;
            public Vector2 DoorWorldPosition => Root.position;
            public bool HasAltDoor => false;
            public Vector2 AltDoorWorldPosition => DoorWorldPosition;
            public bool ShowsDriver => false;
            public Vector2 DriverSeatWorldPosition => Root.position;
            public float DriverSeatHeightMeters => 0;
            public void TakeControls() { }
            public void SetDriveInput(float throttle, float steer, bool brake) { }
            public void ReleaseControls() { }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator RoadRefusesDriving_AndArrivalKeepsThePersistentCore()
        {
            yield return LoadDirect();
            var road = RegionScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RegionPassage>())
                .Single(p => p.ArrivalKey == "rpt_route_91");
            Assert.That(road.TravelAllowed, Is.True);
            var truck = new GameObject("DisposableTruckSeat");
            truck.transform.position = Plan.PlayerStart;
            Assert.That(Switcher.TryEnterDriving(new TestTruckSeat { Root = truck.transform }), Is.True);
            Assert.That(Switcher.Mode, Is.EqualTo(ControlMode.Driving));
            Assert.That(road.TravelAllowed, Is.False);
            string pending = GameServices.PendingArrivalKey;
            road.Activate();
            yield return null;
            Assert.That(GameServices.PendingArrivalKey, Is.EqualTo(pending));
            Assert.That(SceneManager.GetSceneByName("NineMileCreek").isLoaded, Is.False);
            Assert.That(Switcher.LeaveDriving(), Is.True);
            Object.Destroy(truck);

            // Keep the actual persistent rig alive through an unload and additive arrival.
            var root = Object.FindFirstObjectByType<GameRoot>();
            var clock = GameServices.Clock;
            var player = Walk;
            yield return SceneManager.UnloadSceneAsync(RegionScene);
            Assert.That(GameServices.Ready, Is.True);
            yield return LoadDestination();
            yield return null; yield return null;
            Assert.That(Object.FindObjectsByType<GameRoot>().Single(), Is.SameAs(root));
            Assert.That(GameServices.Clock, Is.SameAs(clock));
            Assert.That(Object.FindObjectsByType<PlayerWalkController>().Single(), Is.SameAs(player));
            Assert.That(RegionScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Any(t => t.name == "DevCore"), Is.False);
            Assert.That(RegionScene.GetRootGameObjects().Any(g => g.name == "LandRegion"), Is.True);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Measure_SixtySecondWalkAndRow_AfterWarmup()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Measurement needs a graphics device.");
            yield return LoadDirect();
            StageStrand();
            yield return WalkFor(5, Vector2.left);
            var samples = new List<double>(20000);
            Vector2 walkStart = Walk.transform.position;
            double begin = Time.realtimeSinceStartupAsDouble;
            double previous = begin;
            while (Time.realtimeSinceStartupAsDouble - begin < 20)
            {
                double elapsed = Time.realtimeSinceStartupAsDouble - begin;
                WalkInput.Walk(elapsed < 10 ? Vector2.left : Vector2.right);
                yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                samples.Add((now - previous) * 1000); previous = now;
            }
            WalkInput.Release();
            float walked = Vector2.Distance(walkStart, Walk.transform.position);
            // The staging transition is excluded: 20 measured seconds walking + 40 rowing.
            yield return BoardAtStart();
            var body = Boat.GetComponent<Rigidbody2D>();
            body.position = new Vector2(-100, -170);
            body.rotation = 90;
            Boat.Stop();
            yield return null;
            for (int i = 0; i < 100; ++i)
            {
                StrokeToward(new Vector2(-220, -160));
                yield return new WaitForFixedUpdate();
            }
            Vector2 rowStart = body.position;
            begin = previous = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - begin < 40)
            {
                StrokeToward(new Vector2(-240, -145));
                yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                samples.Add((now - previous) * 1000); previous = now;
            }
            Oars.DriveOars(0, 0, true);
            Assert.That(Vector2.Distance(rowStart, body.position), Is.GreaterThan(5), "Measurement route must row.");
            samples.Sort();
            double median = samples[samples.Count / 2];
            double p95 = samples[Math.Max(0, (int)Math.Ceiling(samples.Count * .95) - 1)];
            Directory.CreateDirectory(Evidence);
            string report = "utc=" + DateTime.UtcNow.ToString("O") + "\n" + ColdMemory + "\n" +
                "route=20s strand west/east; staging excluded; 40s rowing west of point; 5s walk and 2s row warmup\n" +
                "samples=" + samples.Count + "\nmedian_ms=" + median.ToString("F3", CultureInfo.InvariantCulture) +
                "\np95_ms=" + p95.ToString("F3", CultureInfo.InvariantCulture) + "\np95_target_ms=16.67\nwithin_target=" +
                (p95 <= 16.67) + "\n" + MemorySnapshot("after_route") + "\nresolution=" + Screen.width + "x" + Screen.height +
                "\nquality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + "\nvsync=" + QualitySettings.vSyncCount +
                "\ntarget_frame_rate=" + Application.targetFrameRate + "\nrender_pipeline=" + GraphicsSettings.currentRenderPipeline +
                "\ngpu=" + SystemInfo.graphicsDeviceName + "\ngraphics_api=" + SystemInfo.graphicsDeviceType +
                "\nwalk_net_m=" + walked + "\nrow_m=" + Vector2.Distance(rowStart, body.position);
            File.WriteAllText(Path.Combine(Evidence, "measurements.txt"), report);
            File.WriteAllLines(Path.Combine(Evidence, "frame-times-ms.txt"), samples.Select(x => x.ToString("F4", CultureInfo.InvariantCulture)));
            Debug.Log("[rice-point] MEASUREMENTS " + report);
        }
    }
}
#endif
