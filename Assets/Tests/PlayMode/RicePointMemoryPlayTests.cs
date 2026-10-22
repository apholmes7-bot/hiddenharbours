#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doors = HiddenHarbours.App.Editor.NineMileCreekRicePointDoors;
using Object = UnityEngine.Object;
using Process = System.Diagnostics.Process;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>WHAT RICE POINT ADDS TO A GAME THAT HAS BEEN TO ST PETERS AND THE CREEK</b> (NR1a's memory gate,
    /// measured in its Phase B). The loader never unloads: a region it has loaded is switched off when the player
    /// leaves and on again when they come back (<see cref="RegionSceneLoader"/>), and a switched-off region's moving
    /// water keeps its chunk meshes, which <see cref="DisplacedWaterSurface"/> frees only when it is destroyed. So
    /// each land region visited is held for the rest of the session, and Rice Point is the third.
    ///
    /// <para>Two trips, each booted at St Peters, the start scene, and each in a launch of its own: St Peters, the
    /// creek by the core's own loader (as the plate tests cross), then Rice Point through the creek's strand half on
    /// foot; and the same trip without Rice Point, which stays at the creek for one more stop instead. At each stop,
    /// after an unload of unused assets and a collection, the case reads the process's private bytes and working set,
    /// the profiler's totals, every mesh and texture, and each region's moving-water meshes; each hop prints its frame
    /// times from its start to <see cref="SettleFrames"/> frames after the arrival. A measurement, not a gate: it
    /// asserts only that the trip was made (one core, each hop arrived, nothing unloaded), and writes every number to
    /// <c>artifacts/rice-point-nr1/b/</c>, one file per trip.</para>
    ///
    /// <para>St Peters is the start scene, so the shell is cleared with <see cref="ShellFlow.Reset"/>, never entered:
    /// entering the world starts the arrival opening, which takes the player off their feet, and
    /// <c>StartNewGame()</c> writes the save. No graphics device, no measurement: CI skips both cases.</para>
    /// </summary>
    public sealed class RicePointMemoryPlayTests
    {
        const string StPetersScene = "StPeters";
        const string Evidence = "artifacts/rice-point-nr1/b";
        const string CleanupScene = "RicePointMemoryCleanup";
        /// <summary>Frames kept after each arrival, and frames run before each stop's unload and collection.</summary>
        const int SettleFrames = 30;
        /// <summary>Frames for St Peters' self-installing components to register after its load.</summary>
        const int BootFrames = 4;
        const float HopLimitSeconds = 180f;
        const double BytesPerMiB = 1024d * 1024d;

        readonly HashSet<GameObject> _resident = new();
        readonly List<string> _tripScenes = new();
        readonly List<string> _lines = new();
        readonly List<(string Label, long Private, long WorkingSet)> _stops = new();
        RegionDef _creek, _ricePoint;

        [UnityTest, Timeout(600000)]
        public IEnumerator TheTripToRicePoint_HoldsStPetersTheCreekAndRicePoint_MemoryAtEachStop()
        {
            RequireAGraphicsDevice();   // first, before any yield: a skip from a coroutine that has yielded is not clean
            ReadTheRegions();
            yield return Stop("launch");
            yield return BootStPeters();
            yield return Stop("st_peters");
            yield return HopToTheCreek();
            yield return Stop("nine_mile_creek");

            RegionPassage strand = TheCreeksStrandHalf();
            yield return Hop("the creek to Rice Point, walked over the strand", () =>
            {
                strand.Activate();
                Assert.That(GameServices.PendingArrivalKey, Is.EqualTo(Doors.StrandDoorArrivalKey),
                    "the creek's strand half asks for its way in, and the crossing began");
            }, _ricePoint);
            yield return Stop("rice_point");

            StillLoaded(StPetersScene, _creek.SceneName, _ricePoint.SceneName);
            Report("with-rice-point");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TheSameTripWithoutRicePoint_HoldsStPetersAndTheCreek_MemoryAtEachStop()
        {
            RequireAGraphicsDevice();
            ReadTheRegions();
            yield return Stop("launch");
            yield return BootStPeters();
            yield return Stop("st_peters");
            yield return HopToTheCreek();
            yield return Stop("nine_mile_creek");
            yield return Stop("nine_mile_creek_again");   // where the other trip crosses to Rice Point, this one stays

            StillLoaded(StPetersScene, _creek.SceneName);
            Assert.That(SceneManager.GetSceneByName(_ricePoint.SceneName).isLoaded, Is.False, "premise: Rice Point was never loaded");
            Report("without-rice-point");
        }

        // ---- the trip --------------------------------------------------------------------------------------------

        /// <summary>The creek and Rice Point as Rice Point's plan names them: its strand door leads to the creek.</summary>
        void ReadTheRegions()
        {
            LandScenePlanDef plan = RicePointBuilder.LoadPlan();
            Assert.That(plan, Is.Not.Null, "premise: Rice Point's plan");
            _ricePoint = plan.Region;
            _creek = plan.Doors.Single(d => d.OwnArrival == Doors.StrandDoorArrivalKey).Target;
            _tripScenes.Clear();
            _tripScenes.AddRange(new[] { StPetersScene, _creek.SceneName, _ricePoint.SceneName });
        }

        IEnumerator BootStPeters()
        {
            Assert.That(Object.FindObjectsByType<GameRoot>(), Is.Empty, "premise: the trip boots from no core, in a launch of its own");
            LogAssert.ignoreFailingMessages = true;   // the regions log decor complaints a measurement makes no claim on
            yield return SceneManager.LoadSceneAsync(StPetersScene, LoadSceneMode.Single);
            for (int i = 0; i < BootFrames; i++) yield return null;
            Assert.That(Object.FindObjectsByType<GameRoot>().Length, Is.EqualTo(1), "premise: St Peters boots one core");
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            InteractionGate.Reset();
        }

        /// <summary>To the creek by the core's own loader: the one in the persistent scene that knows the creek.</summary>
        IEnumerator HopToTheCreek()
        {
            Scene persistent = PersistentScene();
            RegionSceneLoader[] loaders = Object.FindObjectsByType<RegionSceneLoader>(FindObjectsInactive.Include)
                .Where(l => l.gameObject.scene == persistent && l.Registry.Contains(_creek.Id)).ToArray();
            Assert.That(loaders.Length, Is.EqualTo(1), $"premise: one loader in the core knows {_creek.Id}");
            yield return Hop("St Peters to the creek, by the core's loader", () => loaders[0].TravelTo(_creek.Id), _creek);
        }

        /// <summary>The creek's strand half (NR1a), wired as the walker meets it.</summary>
        RegionPassage TheCreeksStrandHalf()
        {
            RegionPassage[] halves = SceneManager.GetSceneByName(_creek.SceneName).GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<RegionPassage>(true))
                .Where(p => p.name == Doors.StrandDoorName).ToArray();
            Assert.That(halves.Length, Is.EqualTo(1), "premise: the bake placed one strand half in the creek");
            RegionPassage strand = halves[0];
            Assert.That(strand.Target != null ? strand.Target.Id : null, Is.EqualTo(_ricePoint.Id), "premise: it leads to Rice Point");
            var switcher = Object.FindFirstObjectByType<ControlSwitcher>();
            Assert.That(strand.TravelAllowed, Is.True, "premise: the walker is on foot, so the strand lets them through " +
                $"(mode {(switcher != null ? switcher.Mode.ToString() : "no switcher")}, opening {GameServices.OpeningCinematicRunning})");
            return strand;
        }

        /// <summary>Start a hop and run until it lands (the active scene and the current region are the far side's),
        /// then <see cref="SettleFrames"/> frames more, and print its frame times.</summary>
        IEnumerator Hop(string leg, Action go, RegionDef to)
        {
            var frames = new List<float>();
            double start = Time.realtimeSinceStartupAsDouble, across = 0;
            bool there = false;
            go();
            for (int settled = 0; !there || settled < SettleFrames;)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble - start, Is.LessThan(HopLimitSeconds),
                    $"{leg}: not across inside {HopLimitSeconds} s; the active scene is {SceneManager.GetActiveScene().name}, " +
                    $"the region {GameServices.CurrentRegionId}");
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000f);
                if (there) { settled++; continue; }
                there = SceneManager.GetActiveScene().name == to.SceneName && GameServices.CurrentRegionId == to.Id;
                if (there) across = Time.realtimeSinceStartupAsDouble - start;
            }
            Assert.That(Object.FindObjectsByType<GameRoot>().Length, Is.EqualTo(1), $"{leg}: one persistent core");
            frames.Sort();
            float p95 = frames[Mathf.Max(0, Mathf.CeilToInt(frames.Count * .95f) - 1)];
            Line($"{leg}: across in {across.ToString("0.00", CultureInfo.InvariantCulture)} s; {frames.Count} frames to " +
                 $"{SettleFrames} after the arrival, p95 {p95.ToString("0.0", CultureInfo.InvariantCulture)} ms, " +
                 $"worst {frames[frames.Count - 1].ToString("0.0", CultureInfo.InvariantCulture)} ms");
        }

        static void StillLoaded(params string[] scenes)
        {
            foreach (string scene in scenes)
                Assert.That(SceneManager.GetSceneByName(scene).isLoaded, Is.True, $"premise: the loader never unloads, so {scene} is still loaded");
        }

        // ---- the stops -------------------------------------------------------------------------------------------

        /// <summary>Settle, unload unused assets and collect, then read the process and the engine.</summary>
        IEnumerator Stop(string label)
        {
            for (int i = 0; i < SettleFrames; i++) yield return null;
            yield return Resources.UnloadUnusedAssets();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            yield return null;
            Record(label);
        }

        void Record(string label)
        {
            long privateBytes, workingSet;
            using (Process process = Process.GetCurrentProcess())
            {
                privateBytes = OrUnread(() => process.PrivateMemorySize64);
                workingSet = OrUnread(() => process.WorkingSet64);
            }
            Mesh[] meshes = Resources.FindObjectsOfTypeAll<Mesh>();
            Texture[] textures = Resources.FindObjectsOfTypeAll<Texture>();
            _stops.Add((label, privateBytes, workingSet));
            Line($"{label}: private_bytes={privateBytes}, working_set_bytes={workingSet}, " +
                 $"allocated_bytes={Profiler.GetTotalAllocatedMemoryLong()}, reserved_bytes={Profiler.GetTotalReservedMemoryLong()}, " +
                 $"graphics_driver_bytes={Profiler.GetAllocatedMemoryForGraphicsDriver()}, mono_used_bytes={Profiler.GetMonoUsedSizeLong()}, " +
                 $"meshes={meshes.Length}, mesh_bytes={meshes.Sum(m => Profiler.GetRuntimeMemorySizeLong(m))}, " +
                 $"textures={textures.Length}, texture_bytes={textures.Sum(t => Profiler.GetRuntimeMemorySizeLong(t))}, " +
                 $"scenes=[{LoadedScenes()}], moving_water=[{MovingWater()}]");
        }

        /// <summary>A process figure the runtime cannot read prints as -1, so the stop still reports the rest.</summary>
        static long OrUnread(Func<long> read)
        {
            try { return read(); }
            catch (Exception) { return -1; }
        }

        static string LoadedScenes() => string.Join(" ", Enumerable.Range(0, SceneManager.sceneCount)
            .Select(SceneManager.GetSceneAt).Where(s => s.isLoaded).Select(s => s.name));

        /// <summary>Each surface's scene, then the meshes it draws in the water pass and their bytes; a switched-off
        /// region's are counted too, marked off, as its surface holds them until it is destroyed.</summary>
        static string MovingWater() => string.Join(" ", Object.FindObjectsByType<DisplacedWaterSurface>(FindObjectsInactive.Include)
            .Select(surface =>
            {
                Mesh[] chunks = surface.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(r => (r.renderingLayerMask & DisplacedWaterRegistry.RenderingLayer) != 0)
                    .Select(r => r.GetComponent<MeshFilter>())
                    .Where(f => f != null && f.sharedMesh != null)
                    .Select(f => f.sharedMesh).Distinct().ToArray();
                return $"{surface.gameObject.scene.name}:{chunks.Length}/{chunks.Sum(m => Profiler.GetRuntimeMemorySizeLong(m))}" +
                       (surface.isActiveAndEnabled ? "" : "(off)");
            }));

        /// <summary>Each stop less the one before, and less the launch; then the trip's file.</summary>
        void Report(string trip)
        {
            (string Label, long Private, long WorkingSet) launch = _stops[0];
            for (int i = 1; i < _stops.Count; i++)
                Line($"{_stops[i].Label} less {_stops[i - 1].Label}: private {MiB(_stops[i].Private - _stops[i - 1].Private)} MiB, " +
                     $"working set {MiB(_stops[i].WorkingSet - _stops[i - 1].WorkingSet)} MiB; less launch: private " +
                     $"{MiB(_stops[i].Private - launch.Private)} MiB, working set {MiB(_stops[i].WorkingSet - launch.WorkingSet)} MiB");
            string[] head =
            {
                "trip=" + trip, "utc=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                "unity=" + Application.unityVersion, "gpu=" + SystemInfo.graphicsDeviceName,
                "graphics_api=" + SystemInfo.graphicsDeviceType, "gpu_memory_mb=" + SystemInfo.graphicsMemorySize,
                "system_memory_mb=" + SystemInfo.systemMemorySize,
            };
            Directory.CreateDirectory(Evidence);
            File.WriteAllLines(Path.Combine(Evidence, "memory-" + trip + ".txt"), head.Concat(_lines));
        }

        static string MiB(long bytes) => (bytes / BytesPerMiB).ToString("0.0", CultureInfo.InvariantCulture);

        void Line(string line)
        {
            _lines.Add(line);
            TestContext.WriteLine(line);
            Debug.Log("[RicePointMemory] " + line);
        }

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT MEASURED: no graphics device (Null Device), so no mesh the water draws " +
                              "ever reaches a GPU and the numbers would not be the game's. Expected on CI.");
        }

        // ---- the session -----------------------------------------------------------------------------------------

        /// <summary>The persistent scene, reached the only way Unity offers: put something in it and ask.</summary>
        static Scene PersistentScene()
        {
            var probe = new GameObject("RicePointMemoryProbe");
            Object.DontDestroyOnLoad(probe);
            Scene scene = probe.scene;
            Object.DestroyImmediate(probe);
            return scene;
        }

        static GameObject[] PersistentRoots()
        {
            Scene scene = PersistentScene();
            return scene.IsValid() ? scene.GetRootGameObjects() : Array.Empty<GameObject>();
        }

        [SetUp]
        public void RememberTheResidents()
        {
            _resident.Clear();
            foreach (GameObject go in PersistentRoots()) _resident.Add(go);
            _tripScenes.Clear();
            _lines.Clear();
            _stops.Clear();
        }

        /// <summary>Put the session back. St Peters' core outlives its scene's unload (<see cref="PersistentObject"/>),
        /// so after the trip's scenes go, the roots the trip added to the persistent scene go too, by identity.</summary>
        [UnityTearDown]
        public IEnumerator LeaveTheTrip()
        {
            GameServices.PendingArrivalKey = null;
            // The trip is over: no arrival in the scene the cleanup makes active.
            foreach (RegionTravelCoordinator coordinator in Object.FindObjectsByType<RegionTravelCoordinator>(FindObjectsInactive.Include))
                coordinator.enabled = false;
            Scene clean = SceneManager.GetSceneByName(CleanupScene);
            if (!clean.IsValid() || !clean.isLoaded) clean = SceneManager.CreateScene(CleanupScene);
            SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s.isLoaded && _tripScenes.Contains(s.name)) yield return SceneManager.UnloadSceneAsync(s);
            }
            foreach (GameObject go in PersistentRoots())
                if (go != null && !_resident.Contains(go)) Object.DestroyImmediate(go);
            GameServices.Reset();
            ShellFlow.Reset();
            InteractionGate.Reset();
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }
    }
}
#endif
