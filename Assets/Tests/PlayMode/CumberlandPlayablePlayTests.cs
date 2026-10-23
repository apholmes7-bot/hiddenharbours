#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
    public abstract class CumberlandSceneFixture
    {
        protected const string Evidence = "artifacts/cumberland";
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
        Scene _standIn;

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
            Step("setup");
            _resident.Clear();
            foreach (var root in PersistentRoots()) _resident.Add(root);
            var pulse = new GameObject("CumberlandTestProgress");
            pulse.AddComponent<CumberlandTestProgress>();
            Object.DontDestroyOnLoad(pulse);
            _save = GameServices.Save;
            GameServices.Save = new MemorySave();
            ShellFlow.Reset();
            InteractionGate.Reset();
        }

        protected static void Step(string step) => CumberlandTestProgress.Mark(step);

        protected IEnumerator LoadDirect()
        {
            Step("load-direct/begin");
            Assert.That(GameServices.Ready, Is.False, "Direct Play must begin without a core.");
            long allocatedBefore = Profiler.GetTotalAllocatedMemoryLong();
            string before = MemorySnapshot("before_plan_and_scene_load");
            Plan = CumberlandBuilder.LoadPlan();
            Assert.That(File.Exists(Plan.ScenePath), Is.True, "CREATE must precede this launch.");
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
            try
            {
                Step("load-destination/begin");
                var load = SceneManager.LoadSceneAsync(Plan.Region.SceneName, LoadSceneMode.Additive);
                while (!load.isDone) yield return null;
                Step("load-destination/done");
            }
            finally { SceneManager.sceneLoaded -= Activate; }
        }

        // The coordinator switches off every root of the scene an arrival leaves. Make an empty scene
        // active before the region unloads, so the arrival leaves that and not the runner's scene.
        protected void LeaveForAStandIn()
        {
            _standIn = SceneManager.CreateScene("CumberlandStandIn");
            SceneManager.SetActiveScene(_standIn);
        }

        protected static void SetHour(double hour)
        {
            GameServices.Clock.SeekTo(GameServices.Config.SecondsPerDay * hour / 24d);
            GameServices.Clock.IsPaused = true;
        }

        protected void StageNorthBeach()
        {
            var arrival = Plan.Doors.Single(d => d.OwnArrival == "nmc_north_beach");
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
            Walk.transform.position = Plan.PlayerStart;
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
            Step("row-to/" + target);
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
            Step("teardown/begin");
            WalkInput?.Release();
            if (Oars != null) Oars.DriveOars(0, 0, true);
            if (_origin.IsValid() && _origin.isLoaded) SceneManager.SetActiveScene(_origin);
            if (RegionScene.IsValid() && RegionScene.isLoaded) yield return SceneManager.UnloadSceneAsync(RegionScene);
            if (_standIn.IsValid() && _standIn.isLoaded) yield return SceneManager.UnloadSceneAsync(_standIn);
            foreach (var root in PersistentRoots()) if (!_resident.Contains(root)) Object.Destroy(root);
            yield return null;
            GameServices.Reset();
            GameServices.Save = _save;
            ShellFlow.Reset();
            InteractionGate.Reset();
            Time.timeScale = 1;
            Step("teardown/done");
        }
    }

    public sealed class CumberlandPlayablePlayTests : CumberlandSceneFixture
    {
        [UnityTest]
        public IEnumerator DirectPlay_OneCore_WalkNorthBeach_RowBasinAndBay_CastHasNoEligibleSpecies()
        {
            Step("direct/load");
            yield return LoadDirect();
            SetHour(8 + 18 / 60d + 44 / 3600d);
            StageNorthBeach();
            Step("direct/walk-north-beach");
            Vector2 start = Walk.transform.position;
            yield return WalkFor(4, Vector2.up);
            Assert.That(Walk.transform.position.y - start.y, Is.GreaterThan(3));
            Assert.That(WalkInput.Reads, Is.GreaterThan(0));
            Assert.That(Walk.WaterState, Is.EqualTo(OnFootWaterState.Dry));
            SetHour(5 + 12 / 60d + 37 / 3600d);
            Step("direct/board-at-apron");
            yield return BoardAtStart();
            yield return RowTo(new Vector2(220, -150));
            yield return RowTo(new Vector2(198, -170));
            yield return RowTo(new Vector2(174, -215));
            yield return RowTo(new Vector2(205, -240));
            Step("direct/cast");
            Boat.Stop();
            var fish = Boat.GetComponent<FishingController>();
            foreach (var species in Plan.DevFish)
                Assert.That(species.RegionIds, Does.Not.Contain(Plan.Region.Id), "Eligibility is reserved for CMB4.");
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
            Step("direct/PASS");
        }

        sealed class TestTruckSeat : IDriveSeat
        {
            public Transform Root { get; set; }
            public string VehicleId => "vehicle.cumberland_test_truck";
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

        [UnityTest]
        public IEnumerator RoadRefusesDriving_AndArrivalKeepsThePersistentCore()
        {
            Step("road/load");
            yield return LoadDirect();
            var road = RegionScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RegionPassage>())
                .Single(p => p.ArrivalKey == "cmb_route_91");
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
            Step("arrival/unload-region-with-core-alive");
            LeaveForAStandIn();
            yield return SceneManager.UnloadSceneAsync(RegionScene);
            Assert.That(GameServices.Ready, Is.True);
            Step("arrival/reload-destination");
            yield return LoadDestination();
            yield return null; yield return null;
            Assert.That(Object.FindObjectsByType<GameRoot>().Single(), Is.SameAs(root));
            Assert.That(GameServices.Clock, Is.SameAs(clock));
            Assert.That(Object.FindObjectsByType<PlayerWalkController>().Single(), Is.SameAs(player));
            Assert.That(RegionScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Any(t => t.name == "DevCore"), Is.False);
            Step("arrival/PASS");
            Assert.That(RegionScene.GetRootGameObjects().Any(g => g.name == "LandRegion"), Is.True);
        }

        [UnityTest]
        public IEnumerator Measure_SeparateSixtySecondWalkAndRow_AfterWarmup()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Measurement needs a graphics device.");
            Step("measure/load");
            yield return LoadDirect();
            Walk.transform.position = Plan.PlayerStart;
            Walk.GetComponent<Rigidbody2D>().position = Plan.PlayerStart;
            yield return WalkFor(3, Vector2.right);
            yield return WalkFor(3, Vector2.left);
            Step("measure/walk-sixty-seconds");
            yield return SampleRoute("walk", false);
            Step("measure/board");
            yield return BoardAtStart();
            // Stage in open bay, beyond the breakwater; staging is excluded from both samples.
            var body = Boat.GetComponent<Rigidbody2D>();
            body.position = new Vector2(200, -235);
            body.rotation = -90;
            Boat.Stop(); Physics2D.SyncTransforms();
            for (int i = 0; i < 150; ++i)
            {
                StrokeToward(new Vector2(300, -235));
                yield return new WaitForFixedUpdate();
            }
            Step("measure/row-sixty-seconds");
            yield return SampleRoute("row", true);
            Step("measure/PASS");
        }

        IEnumerator SampleRoute(string name, bool rowing)
        {
            var samples = new List<double>(12000);
            var body = Boat.GetComponent<Rigidbody2D>();
            Vector2 previousPosition = rowing ? body.position : (Vector2)Walk.transform.position;
            float distance = 0;
            double begin = Time.realtimeSinceStartupAsDouble, previous = begin;
            while (Time.realtimeSinceStartupAsDouble - begin < 60)
            {
                double elapsed = Time.realtimeSinceStartupAsDouble - begin;
                if (rowing) StrokeToward(elapsed < 30 ? new Vector2(330, -235) : new Vector2(195, -235));
                else WalkInput.Walk(((int)(elapsed / 8) % 2) == 0 ? Vector2.right : Vector2.left);
                yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                samples.Add((now - previous) * 1000); previous = now;
                Vector2 at = rowing ? body.position : (Vector2)Walk.transform.position;
                distance += Vector2.Distance(previousPosition, at); previousPosition = at;
            }
            WalkInput.Release();
            Oars.DriveOars(0, 0, true);
            Assert.That(distance, Is.GreaterThan(5), "The measured route must actually move.");
            samples.Sort();
            double median = samples[samples.Count / 2];
            double p95 = samples[Math.Max(0, (int)Math.Ceiling(samples.Count * .95) - 1)];
            Directory.CreateDirectory(Evidence);
            string report = "utc=" + DateTime.UtcNow.ToString("O") + "\n" + ColdMemory + "\n" +
                "route=" + (rowing ? "60s open bay east then west at y=-235; 3s rowing warmup; staging excluded" :
                    "60s apron east/west in 8s legs; 6s walking warmup") + "\n" +
                "samples=" + samples.Count + "\nmedian_ms=" + median.ToString("F3", CultureInfo.InvariantCulture) +
                "\np95_ms=" + p95.ToString("F3", CultureInfo.InvariantCulture) + "\np95_target_ms=16.67\nwithin_target=" +
                (p95 <= 16.67) + "\n" + MemorySnapshot("after_" + name) + "\nresolution=" + Screen.width + "x" + Screen.height +
                "\nquality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + "\nvsync=" + QualitySettings.vSyncCount +
                "\ntarget_frame_rate=" + Application.targetFrameRate + "\nrender_pipeline=" + GraphicsSettings.currentRenderPipeline +
                "\ngpu=" + SystemInfo.graphicsDeviceName + "\ngraphics_api=" + SystemInfo.graphicsDeviceType +
                "\ndistance_m=" + distance.ToString("F3", CultureInfo.InvariantCulture);
            File.WriteAllText(Path.Combine(Evidence, "measure-" + name + ".txt"), report);
            File.WriteAllLines(Path.Combine(Evidence, "frame-times-" + name + "-ms.txt"),
                samples.Select(x => x.ToString("F4", CultureInfo.InvariantCulture)));
            Debug.Log("[cumberland] MEASURE " + name + " " + report);
        }
    }

    public sealed class CumberlandTestProgress : MonoBehaviour
    {
        static string _step;
        float _next;
        public static void Mark(string step)
        {
            _step = step;
            Debug.Log("[cumberland] STEP " + step + " frame=" + Time.frameCount);
        }
        void Update()
        {
            if (Time.realtimeSinceStartup < _next) return;
            _next = Time.realtimeSinceStartup + 10;
            Debug.Log("[cumberland] PROGRESS " + _step + " frame=" + Time.frameCount);
        }
    }
}
namespace HiddenHarbours.Tests.PlayMode
{
    public sealed class CumberlandPlatePlayTests : CumberlandSceneFixture
    {
        [Serializable] sealed class SourceViews { public SourceViewList keyscenes; }
        [Serializable] sealed class SourceViewList { public SourceView[] list; }
        [Serializable] sealed class SourceView { public string id; public float[] box; }
        const int Width = 1920, Height = 1080;
        Camera _camera;
        RenderTexture _target;
        bool _shaderSettingCaptured, _asyncShaders;

        [UnityTest]
        public IEnumerator Marina_NorthBeach_Fork_AtThreeShippedTides()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Plates need a graphics device.");
            _asyncShaders = ShaderUtil.allowAsyncCompilation;
            _shaderSettingCaptured = true;
            ShaderUtil.allowAsyncCompilation = false;
            Step("plates/load");
            yield return LoadDirect();
            _camera = Camera.main;
            Assert.That(_camera, Is.Not.Null);
            foreach (var component in _camera.GetComponents<Behaviour>())
                if (component is CameraFollow || component.GetType().Name == "PixelPerfectCamera") component.enabled = false;
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            _target.Create();
            _camera.targetTexture = _target;
            _camera.aspect = (float)Width / Height;
            var views = JsonUtility.FromJson<SourceViews>(Plan.Source.text).keyscenes.list;
            string folder = Path.Combine(Application.temporaryCachePath, "cumberland-cmb0-b");
            Directory.CreateDirectory(folder);
            Assert.That(File.Exists(Path.Combine(folder, "cmb0-phase-b.floor")), Is.True, "A plate floor must precede capture.");
            var manifest = new StringBuilder("view,tide,day_index,hour,water_m,path,utc\n");
            var hours = new[] { 2 + 6 / 60d + 16 / 3600d, 5 + 12 / 60d + 37 / 3600d, 8 + 18 / 60d + 44 / 3600d };
            var tideNames = new[] { "spring-high", "mean-ebb", "spring-low" };
            var subjects = new[] { "marina", "north_beach", "fork" };
            for (int tide = 0; tide < hours.Length; ++tide)
            {
                SetHour(hours[tide]);
                Assert.That(GameServices.Clock.DayIndex, Is.Zero);
                double water = GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds);
                double expected = tide == 0 ? 2.1995 : tide == 1 ? 0 : -2.1927;
                Assert.That(water, Is.EqualTo(expected).Within(.02), "Shipped tide differs from the plate contract.");
                yield return new WaitForSecondsRealtime(2);
                foreach (string subject in subjects)
                {
                    Step("plate/" + subject + "/" + tideNames[tide]);
                    var box = views.Single(v => v.id == "keyscene.cmb_" + subject).box;
                    Vector3 centre = new Vector3((box[0] + box[1]) / 2, (box[2] + box[3]) / 2, -10);
                    _camera.transform.position = centre;
                    _camera.orthographicSize = Mathf.Max((box[3] - box[2]) / 2,
                        (box[1] - box[0]) / (2 * _camera.aspect)) * 1.05f;
                    for (int frame = 0; frame < 30; ++frame) yield return null;
                    Assert.That(Vector3.Distance(_camera.transform.position, centre), Is.LessThan(.001f));
                    string name = subject + "-" + tideNames[tide] + ".png";
                    Capture(Path.Combine(folder, name));
                    manifest.AppendLine(FormattableString.Invariant(
                        $"{subject},{tideNames[tide]},0,{hours[tide]:F8},{water:F6},{name},{DateTime.UtcNow:O}"));
                }
                // The three required subjects do not include the course. One mean-water view gives
                // the owner the requested first look at its existing Lawn/Grass treatment.
                if (tide == 1)
                {
                    Step("plate/course/mean-ebb");
                    var box = views.Single(v => v.id == "keyscene.cmb_clubhouse").box;
                    _camera.transform.position = new Vector3((box[0] + box[1]) / 2, (box[2] + box[3]) / 2, -10);
                    _camera.orthographicSize = Mathf.Max((box[3] - box[2]) / 2,
                        (box[1] - box[0]) / (2 * _camera.aspect)) * 1.05f;
                    for (int frame = 0; frame < 30; ++frame) yield return null;
                    Capture(Path.Combine(folder, "course-mean-ebb.png"));
                    manifest.AppendLine(FormattableString.Invariant(
                        $"course,mean-ebb,0,{hours[tide]:F8},{water:F6},course-mean-ebb.png,{DateTime.UtcNow:O}"));
                }
            }
            File.WriteAllText(Path.Combine(folder, "plates.csv"), manifest.ToString());
            Debug.Log("[cumberland] PLATES OK: 9 required and one course view saved in " + folder);
        }

        [UnityTest]
        public IEnumerator CoursePaint_DaylightAtShippedMeanFlood()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Plates need a graphics device.");
            _asyncShaders = ShaderUtil.allowAsyncCompilation;
            _shaderSettingCaptured = true;
            ShaderUtil.allowAsyncCompilation = false;
            Step("course-daylight/load");
            yield return LoadDirect();
            double hour = GameServices.Config.TidalPeriodHours - Plan.Region.TidePhaseHours;
            SetHour(hour);
            double water = GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds);
            Assert.That(water, Is.EqualTo(0).Within(.02));
            _camera = Camera.main;
            foreach (var component in _camera.GetComponents<Behaviour>())
                if (component is CameraFollow || component.GetType().Name == "PixelPerfectCamera") component.enabled = false;
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            _target.Create(); _camera.targetTexture = _target; _camera.aspect = (float)Width / Height;
            var views = JsonUtility.FromJson<SourceViews>(Plan.Source.text).keyscenes.list;
            var box = views.Single(v => v.id == "keyscene.cmb_clubhouse").box;
            _camera.transform.position = new Vector3((box[0] + box[1]) / 2, (box[2] + box[3]) / 2, -10);
            _camera.orthographicSize = Mathf.Max((box[3] - box[2]) / 2,
                (box[1] - box[0]) / (2 * _camera.aspect)) * 1.05f;
            yield return new WaitForSecondsRealtime(2);
            for (int frame = 0; frame < 30; ++frame) yield return null;
            string folder = Path.Combine(Application.temporaryCachePath, "cumberland-cmb0-b");
            Assert.That(File.Exists(Path.Combine(folder, "cmb0-phase-b.floor")), Is.True);
            Step("course-daylight/capture");
            Capture(Path.Combine(folder, "course-daylight-mean-flood.png"));
            File.WriteAllText(Path.Combine(folder, "course-daylight.csv"),
                "view,tide,day_index,hour,water_m,path,utc\n" + FormattableString.Invariant(
                    $"course,mean-flood,0,{hour:F8},{water:F6},course-daylight-mean-flood.png,{DateTime.UtcNow:O}\n"));
            Debug.Log("[cumberland] COURSE DAYLIGHT OK");
        }

        void Capture(string path)
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                _camera.Render();
                RenderTexture.active = _target;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        [TearDown]
        public void ReleasePlateResources()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_target != null) { _target.Release(); UnityEngine.Object.DestroyImmediate(_target); }
            if (_shaderSettingCaptured) ShaderUtil.allowAsyncCompilation = _asyncShaders;
            _shaderSettingCaptured = false;
        }
    }
}
#endif
