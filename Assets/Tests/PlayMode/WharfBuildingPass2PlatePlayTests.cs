using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The wharf buildings in pass 2, photographed</b> (#910, drop 13 Phase B): the plates and the numbers
    /// the owner rules on. St Peters' four placed wharf buildings (the cannery on the west shore, and Aunt
    /// Ginny's woodshed, net store and lean-to) at noon, in the golden hour and at night, each station framed
    /// at 1:1, one sheet pixel to one screen pixel; and what the re-bake costs: the texture memory the four
    /// sheets hold, the draw calls with the four drawn and without them, and the frame time.
    ///
    /// <para><b>Before and after are one fixture.</b> Which look the scene is in is read off the village
    /// contract the four sheets were baked into: all four drawn by <c>wharfBuilding</c> is <c>before</c>
    /// (pass 1, main's sheets), all four drawn by <c>wharfBuilding2</c> is <c>after</c> (the re-bake). A mix
    /// is a half-baked kit and fails, and so does a sheet the scene draws at a size the contract does not
    /// give (a contract re-baked without its sheets re-imported). Plates, captions and numbers go to
    /// <c>temporaryCachePath/WharfBuildingPass2Plates/&lt;look&gt;/</c> under the same names both times, so
    /// a before and an after pair up by file name.</para>
    ///
    /// <para><b>The owner's save is never at risk</b>, as in <c>VillageReturnPlatePlayTests</c>: St Peters
    /// boots to the title, the save service writes nothing while the shell is there, and this fixture never
    /// leaves it. It skips, before it moves anything, if the region did not boot to it.</para>
    ///
    /// <para><b>The weather is HELD clear</b>: the region's own environment service is wrapped to answer
    /// clear air on a light sea, so a before and an after are shot under one sky whatever the simulation
    /// would have brought. Wind, tide and seed stay the region's, and the wrapper comes off in the
    /// teardown.</para>
    ///
    /// <para>Needs a GPU: skips loudly as NOT VERIFIED on CI's Null device rather than reading green.</para>
    /// </summary>
    public partial class WharfBuildingPass2PlatePlayTests
    {
        const string PlateDir = "WharfBuildingPass2Plates";
        const string CleanupSceneName = "WharfBuildingPass2PlateCleanup";
        const string StPeters = "StPeters";
        const string BuildingsContractPath = "_Project/Art/Sprites/Buildings/Village/Buildings.json";
        const string VillageSheetPrefix = "Village_";
        const string HouseRig = "house";
        const string PassOneRig = "wharfBuilding";
        const string PassTwoRig = "wharfBuilding2";
        const string YardKeyPrefix = "ginny";
        const string CanneryKey = "stPetersCannery";

        const int PlateHeightPx = CameraFollow.DesignScreenHeightPx;   // 1080: the PPC zoom depends on it
        const int PlateWidthPx = 1920;
        // Ladder step 1, the pivot: one asset pixel to one screen pixel. The widest a walker sees (step 3,
        // 11.25 m) cannot hold the cannery, which stands 21 m tall on its sheet.
        const int OneToOneStep = 1;

        const float LeadEaseOutSeconds = 6f;
        const float OnTheCentreMetres = 0.001f;
        const int WarmFrames = 10, TimedFrames = 120;

        const float Noon = 12f, GoldenHour = 19.5f, Night = 23f;
        static readonly float[] Hours = { Noon, GoldenHour, Night };   // always forward: sought, never wound back

        const float ClearVisibility = 1f, ClearSea01 = 0.20f;
        const float ClearRunSeconds = 1f;
        const float ShaderWaitSeconds = 120f;
        const float SameLight = 1e-3f;

        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>The fields of the village contract this fixture reads.</summary>
        [Serializable] public sealed class Buildings { public Building[] buildings; }

        [Serializable]
        public sealed class Building
        {
            public string key;
            public string rig;
            public int sheetW;
            public int sheetH;
        }

        sealed class Station
        {
            public readonly string Shot, Subject;
            public readonly Vector2 Centre;
            public readonly SpriteRenderer[] Subjects;   // each whole in the frame

            public Station(string shot, string subject, Vector2 centre, SpriteRenderer[] subjects)
            {
                Shot = shot;
                Subject = subject;
                Centre = centre;
                Subjects = subjects;
            }
        }

        /// <summary>The region's own environment with the weather HELD: the sample's visibility and sea
        /// state are the plate's; the wind, the current, the tide, the water level and the seed are asked
        /// of the service it wraps.</summary>
        sealed class HeldMood : IEnvironmentService
        {
            public readonly IEnvironmentService Inner;
            readonly float _visibility, _seaState01;

            public HeldMood(IEnvironmentService inner, float visibility, float seaState01)
            {
                Inner = inner;
                _visibility = visibility;
                _seaState01 = seaState01;
            }

            public int WorldSeed => Inner.WorldSeed;

            public TideProfile ActiveTideProfile
            {
                get => Inner.ActiveTideProfile;
                set => Inner.ActiveTideProfile = value;
            }

            public EnvironmentSample Sample()
            {
                EnvironmentSample s = Inner.Sample();
                // The stepped scale is the band the continuous axis sits in (EnvironmentSample.SeaState01).
                var band = (SeaState)Mathf.Clamp(Mathf.FloorToInt(_seaState01 * (int)SeaState.Storm),
                                                 (int)SeaState.Glass, (int)SeaState.Storm);
                return new EnvironmentSample(s.WindVector, s.CurrentVector, s.TideHeight, band, _visibility,
                                             _seaState01);
            }

            public float TideHeightAt(double totalSeconds) => Inner.TideHeightAt(totalSeconds);

            // ⚠ Spelled out, never the interface's default: a region's water level is not always its tide.
            public float WaterLevelAt(double totalSeconds) => Inner.WaterLevelAt(totalSeconds);
        }

        // =============================================================================================
        //  Fixture state: one instance serves every case, so the teardown clears all of it
        // =============================================================================================

        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        readonly List<Object> _spawned = new List<Object>();
        DateTime _runStartedUtc;
        bool _loadedAny;
        bool _introOnEntry;
        string _healed = "none";
        string _look = "(not read)";
        string _lookEvidence = "(not read)";
        string _pinned = "(not pinned)";
        string _mood = "(not held)";
        HeldMood _held;

        Camera _cam;
        CameraFollow _follow;
        bool _followCaptured;
        Transform _followTargetOnEntry;
        float _followSmoothOnEntry;
        GameObject _anchor;
        float _askedHeight;
        string _eased = "(not framed)";
        RenderTexture _rt;
        int _w, _h;

        [OneTimeSetUp]
        public void MarkTheRun() => _runStartedUtc = DateTime.UtcNow;

        // =============================================================================================
        //  Set-up and teardown: leave nothing loaded (#764), and the region's own weather back
        // =============================================================================================

        [UnitySetUp]
        public IEnumerator SetUpRegion()
        {
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;   // ⚠ a STATIC: left at 0 it stops every test that follows
            ReleaseTheMood();      // first: the wrapper holds the region's service, which is about to go

            if (_follow != null && _followCaptured)
            {
                _follow.Target = _followTargetOnEntry;
                _follow.Smooth = _followSmoothOnEntry;
            }
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();

            _follow = null; _cam = null; _followCaptured = false; _followTargetOnEntry = null; _anchor = null;
            _look = "(not read)"; _lookEvidence = "(not read)"; _pinned = "(not pinned)"; _mood = "(not held)";
            _eased = "(not framed)"; _healed = "none";

            // A case that skipped before loading anything (every case on CI) has nothing to put back.
            if (!_loadedAny) yield break;

            if (GameServices.Clock != null) GameServices.Clock.TimeScale = 1f;
            GameServices.OpeningCinematicRunning = _introOnEntry;
            GameServices.PendingArrivalKey = null;

            // ⚠ Look the cleanup scene up before creating it: CreateScene throws on a name that exists.
            Scene clean = SceneManager.GetSceneByName(CleanupSceneName);
            if (!clean.IsValid() || !clean.isLoaded) clean = SceneManager.CreateScene(CleanupSceneName);
            if (clean.IsValid()) SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == StPeters)
                    yield return SceneManager.UnloadSceneAsync(s);
            }

            // ⚠ #764: a Single load of a region promotes the persistent core's roots into
            // DontDestroyOnLoad, and unloading the region leaves them there for every test that follows.
            // Destroy exactly the roots this case added, by identity, then clear the service slots.
            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go)) Object.DestroyImmediate(go);
            GameServices.Reset();
            _loadedAny = false;
            yield return null;
        }

        // =============================================================================================
        //  The plates
        // =============================================================================================

        /// <summary>
        /// The cannery, whole, and Ginny's three outbuildings together, at 1:1: at noon, at 19:30 and at
        /// 23:00, in clear weather. The numbers are taken on the noon pass, at both stations.
        /// </summary>
        [UnityTest]
        public IEnumerator StPeters_TheCanneryAndGinnysSheds_AtNoonGoldenHourAndNight()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            List<(SpriteRenderer sr, string key, string rig)> wharf = WharfRenderers();
            ReadTheLook(wharf);
            EnsureTheFollow();
            List<Station> stations = Stations(wharf);
            List<SpriteRenderer> four = wharf.Select(w => w.sr).ToList();
            float height = CameraZoomPolicy.WorldHeightForStep(OneToOneStep, CameraFollow.AssetsPPU,
                                                               CameraFollow.DesignScreenHeightPx);
            string framing = $"ladder step {OneToOneStep}, the 1:1 pivot (one sheet pixel to one screen pixel), " +
                             $"{height:0.####} m";

            string captions = StartFile("captions.txt");
            string numbers = StartFile("numbers.txt");
            File.AppendAllText(numbers,
                $"look={_look} ({_lookEvidence}); run {_runStartedUtc:yyyy-MM-dd HH:mm:ss}Z; Unity " +
                $"{Application.unityVersion}; {SystemInfo.graphicsDeviceType} on {SystemInfo.graphicsDeviceName}\n" +
                Census("the four wharf buildings", four) + WhatTheyDrawWith(four), Utf8);

            var failures = new List<string>();
            var lights = new Dictionary<string, Color>();
            HoldTheMood();
            foreach (float hours in Hours)
            {
                yield return PinTheHour(hours, ClearRunSeconds);
                lights[Hhmm(hours)] = Shader.GetGlobalColor("_DayNightTint");

                foreach (Station s in stations)
                {
                    string plate = $"wharf-{Hhmm(hours)}-{s.Shot}";
                    yield return FrameOn(s.Centre, height);
                    yield return RunTheWorld(ClearRunSeconds, s.Centre);
                    yield return ShadersReady();

                    string said = CheckTheSubjectIsWhollyInFrame(s, plate, failures);
                    SavePlate(plate + ".png", Capture());
                    File.AppendAllText(captions, Caption(plate, s.Subject, s.Centre, $"framing: {framing}\n{said}"),
                                       Utf8);

                    if (hours == Noon) yield return TakeTheNumbers(s, four, numbers);
                }
            }

            AssertTheLightMoved(lights, failures);
            Assert.IsEmpty(failures, $"[{_look}] the wharf plates are not evidence as shot:\n  " +
                                     string.Join("\n  ", failures));
        }

        // =============================================================================================
        //  What is placed, and which look it is in
        // =============================================================================================

        /// <summary>Every renderer drawing a village sheet the contract gives a wharf rig (anything but the
        /// house): the four placed wharf buildings, with their contract rows.</summary>
        List<(SpriteRenderer sr, string key, string rig)> WharfRenderers()
        {
            Dictionary<string, Building> rows = ReadContract<Buildings>(BuildingsContractPath).buildings
                .Where(b => b.rig != HouseRig && (b.key == CanneryKey || b.key.StartsWith(YardKeyPrefix, StringComparison.Ordinal)))
                .ToDictionary(b => b.key, b => b);
            var found = new List<(SpriteRenderer, string, string)>();
            var sizes = new List<string>();
            foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>())
            {
                Texture2D albedo = sr.sprite != null ? sr.sprite.texture : null;
                if (albedo == null || !albedo.name.StartsWith(VillageSheetPrefix, StringComparison.Ordinal)) continue;
                string key = albedo.name.Substring(VillageSheetPrefix.Length);
                if (key != CanneryKey && !key.StartsWith(YardKeyPrefix, StringComparison.Ordinal)) continue;
                if (!rows.TryGetValue(key, out Building row)) continue;   // a house, or a channel sheet
                found.Add((sr, key, row.rig));
                sizes.Add($"{key} {albedo.width}x{albedo.height}");
                Assert.AreEqual((row.sheetW, row.sheetH), (albedo.width, albedo.height),
                    $"St Peters draws {albedo.name} at {albedo.width}x{albedo.height}, but the contract gives " +
                    $"{row.sheetW}x{row.sheetH}: a contract re-baked without its sheet re-imported, or the reverse. " +
                    "A plate of it is neither the before nor the after.");
            }
            CollectionAssert.AreEquivalent(rows.Keys, found.Select(f => f.Item2),
                "each wharf-rig row of the contract must be placed in St Peters exactly once");
            _lookEvidence = $"sheets {string.Join(", ", sizes.OrderBy(s => s, StringComparer.Ordinal))}";
            return found;
        }

        /// <summary>Before or after, read off the contract: all four drawn by pass 1 is main's look, all
        /// four by pass 2 is the re-bake's, and anything between is a half-baked kit that is neither.</summary>
        void ReadTheLook(List<(SpriteRenderer sr, string key, string rig)> wharf)
        {
            string[] rigs = wharf.Select(w => w.rig).Distinct().OrderBy(r => r, StringComparer.Ordinal).ToArray();
            _lookEvidence = $"the contract's rig for the {wharf.Count}: {string.Join("|", rigs)}; {_lookEvidence}";
            Assert.AreEqual(1, rigs.Length, $"the kit is half-baked: {_lookEvidence}.");
            Assert.That(rigs[0], Is.EqualTo(PassOneRig).Or.EqualTo(PassTwoRig), _lookEvidence);
            _look = rigs[0] == PassOneRig ? "before" : "after";
            Debug.Log($"[{PlateDir}] look '{_look}': {_lookEvidence}");
        }

        /// <summary>The cannery, centred on itself; and Ginny's yard, centred on her three outbuildings
        /// together.</summary>
        static List<Station> Stations(List<(SpriteRenderer sr, string key, string rig)> wharf)
        {
            var stations = new List<Station>();
            var cannery = wharf.Where(w => w.key == CanneryKey).ToList();
            Assert.AreEqual(1, cannery.Count, "St Peters places one cannery");
            SpriteRenderer c = cannery[0].sr;
            stations.Add(new Station("cannery",
                $"the cannery '{c.gameObject.name}' at {Metres(c.transform.position)}, drawn from '{c.sprite.name}'",
                OnTheHalfMetre(c.bounds.center), new[] { c }));

            var yard = wharf.Where(w => w.key.StartsWith(YardKeyPrefix, StringComparison.Ordinal))
                            .OrderBy(w => w.key, StringComparer.Ordinal).ToList();
            Assert.AreEqual(3, yard.Count, "Ginny's yard holds three outbuildings");
            Bounds b = yard[0].sr.bounds;
            foreach (var y in yard) b.Encapsulate(y.sr.bounds);
            stations.Add(new Station("yard-ginny",
                "Ginny's yard: " + string.Join(", ", yard.Select(y =>
                    $"the {y.key} '{y.sr.gameObject.name}' at {Metres(y.sr.transform.position)} from '{y.sr.sprite.name}'")),
                OnTheHalfMetre(b.center), yard.Select(y => y.sr).ToArray()));
            return stations;
        }

        static bool Drawn(Renderer r) => r != null && r.enabled && r.gameObject.activeInHierarchy;

        // =============================================================================================
        //  The numbers: what the four hold, what they draw, and what the frame costs
        // =============================================================================================

        /// <summary>Every distinct sheet the renderers draw, with its size, its format and its bytes: on
        /// the GPU, from the format and the mips; and as the profiler reports it in this editor.</summary>
        static string Census(string what, IEnumerable<SpriteRenderer> renderers)
        {
            var sheets = new SortedDictionary<string, Texture>(StringComparer.Ordinal);
            int count = 0;
            foreach (SpriteRenderer sr in renderers)
            {
                if (sr == null) continue;
                count++;
                Texture t = sr.sprite != null ? sr.sprite.texture : null;
                if (t != null && !sheets.ContainsKey(t.name)) sheets.Add(t.name, t);
            }

            long gpu = 0, runtime = 0;
            var lines = new StringBuilder();
            foreach (Texture t in sheets.Values)
            {
                long g = GpuBytes(t), r = Profiler.GetRuntimeMemorySizeLong(t);
                gpu += g;
                runtime += r;
                lines.AppendLine($"  {what}: sheet {t.name} {t.width}x{t.height} {t.graphicsFormat} mips={Mips(t)} " +
                                 $"gpuBytes={g} ({g / 1048576.0:0.00} MiB) runtimeBytes={r}");
            }
            return $"{what}: renderers={count} sheets={sheets.Count} gpuBytes={gpu} ({gpu / 1048576.0:0.00} MiB) " +
                   $"runtimeBytes={runtime} ({runtime / 1048576.0:0.00} MiB)\n" + lines;
        }

        /// <summary>What the four draw with, as the renderers hold it: the draws the four add at the least
        /// (a sprite batches only with another on the same sheet and material).</summary>
        static string WhatTheyDrawWith(List<SpriteRenderer> renderers)
        {
            var pairs = renderers.Where(r => r != null)
                .Select(r => $"{(r.sprite != null && r.sprite.texture != null ? r.sprite.texture.name : "none")}+" +
                             $"{(r.sharedMaterial != null ? r.sharedMaterial.name : "none")}")
                .Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
            string materials = string.Join("|", renderers.Where(r => r != null)
                .Select(r => r.sharedMaterial != null ? r.sharedMaterial.name : "none").Distinct());
            string binders = string.Join("|", renderers.Where(r => r != null)
                .Select(r => r.GetComponent<SpriteLightBinder>() != null ? "bound" : "unbound").Distinct());
            return $"draws: {renderers.Count} renderers on {pairs.Count} distinct sheet+material pair(s), so at " +
                   $"least {pairs.Count} draw call(s) from the four; material(s) {materials}; light binder(s) " +
                   $"{binders}\n";
        }

        static int Mips(Texture t) => t is Texture2D t2 ? Mathf.Max(1, t2.mipmapCount) : 1;

        static long GpuBytes(Texture t)
        {
            long bytes = 0;
            for (int m = 0; m < Mips(t); m++)
                bytes += GraphicsFormatUtility.ComputeMipmapSize(Mathf.Max(1, t.width >> m), Mathf.Max(1, t.height >> m),
                                                                 t.graphicsFormat);
            return bytes;
        }

        /// <summary>At a framed station, the world stopped: draw calls and batches with the four drawn and
        /// with them switched off, and the frame time both ways.</summary>
        IEnumerator TakeTheNumbers(Station s, List<SpriteRenderer> four, string numbersPath)
        {
            long[] on = new long[3], off = new long[3];
            yield return CountDrawCalls(four, drawn: true, on);
            yield return CountDrawCalls(four, drawn: false, off);
            double[] tOn = new double[5], tOff = new double[5];
            yield return TimeTheFrame(four, drawn: true, tOn);
            yield return TimeTheFrame(four, drawn: false, tOff);
            string line = $"{_look} station={s.Shot} drawCalls={on[0]} batches={on[1]} setPassCalls={on[2]} " +
                          $"drawCallsWithoutTheFour={off[0]} batchesWithoutTheFour={off[1]} " +
                          $"setPassCallsWithoutTheFour={off[2]} cpuMsMedian={tOn[0]:0.000} gpuMsMedian={tOn[1]:0.000} " +
                          $"mainThreadMsMean={tOn[2]:0.000} cpuMsMedianWithoutTheFour={tOff[0]:0.000} " +
                          $"gpuMsMedianWithoutTheFour={tOff[1]:0.000} mainThreadMsMeanWithoutTheFour={tOff[2]:0.000} " +
                          $"frames={TimedFrames} cpuSamples={tOn[3]:0}/{tOff[3]:0} gpuSamples={tOn[4]:0}/{tOff[4]:0} " +
                          "(the editor's whole frame, not a player build; a batch-mode editor may read its draw " +
                          "counters and GPU time as 0)";
            File.AppendAllText(numbersPath, line + "\n", Utf8);
            Debug.Log($"[{PlateDir}] numbers: {line}");
        }

        IEnumerator CountDrawCalls(List<SpriteRenderer> four, bool drawn, long[] into)
        {
            List<Renderer> off = drawn ? new List<Renderer>() : SwitchOff(four);
            var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            try
            {
                for (int i = 0; i < 3; i++) yield return null;   // the world is frozen; the camera still renders
                into[0] = drawCalls.Valid ? drawCalls.LastValue : 0;
                into[1] = batches.Valid ? batches.LastValue : 0;
                into[2] = setPass.Valid ? setPass.LastValue : 0;
            }
            finally
            {
                drawCalls.Dispose();
                batches.Dispose();
                setPass.Dispose();
                foreach (Renderer r in off) if (r != null) r.enabled = true;
            }
        }

        /// <summary>The frame's time over <see cref="TimedFrames"/> frames: the CPU and GPU medians from the
        /// frame-timing manager, and the main thread's mean from the profiler, whichever this editor gives.
        /// Zero where it gives none.</summary>
        IEnumerator TimeTheFrame(List<SpriteRenderer> four, bool drawn, double[] into)
        {
            List<Renderer> off = drawn ? new List<Renderer>() : SwitchOff(four);
            var mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", TimedFrames);
            var cpu = new List<double>(TimedFrames);
            var gpu = new List<double>(TimedFrames);
            var timing = new FrameTiming[1];
            try
            {
                for (int i = 0; i < WarmFrames; i++) yield return null;
                for (int i = 0; i < TimedFrames; i++)
                {
                    yield return null;
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timing) == 0) continue;
                    if (timing[0].cpuFrameTime > 0) cpu.Add(timing[0].cpuFrameTime);
                    if (timing[0].gpuFrameTime > 0) gpu.Add(timing[0].gpuFrameTime);
                }
                into[0] = Median(cpu);
                into[1] = Median(gpu);
                into[2] = MeanMilliseconds(mainThread);
                into[3] = cpu.Count;
                into[4] = gpu.Count;
            }
            finally
            {
                mainThread.Dispose();
                foreach (Renderer r in off) if (r != null) r.enabled = true;
            }
        }

        static List<Renderer> SwitchOff(List<SpriteRenderer> renderers)
        {
            var off = new List<Renderer>();
            foreach (SpriteRenderer r in renderers)
                if (r != null && r.enabled) { r.enabled = false; off.Add(r); }
            return off;
        }

        static double Median(List<double> values)
        {
            if (values.Count == 0) return 0;
            values.Sort();
            int mid = values.Count / 2;
            return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) * 0.5;
        }

        static double MeanMilliseconds(ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < recorder.Count; i++) sum += recorder.GetSample(i).Value;
            return sum / recorder.Count / 1e6;   // the recorder counts nanoseconds
        }

        // =============================================================================================
        //  The weather and the hour
        // =============================================================================================

        void HoldTheMood()
        {
            ReleaseTheMood();
            IEnvironmentService inner = GameServices.Environment;
            Assert.IsNotNull(inner, "the region registered no environment, so there is no weather to hold.");
            _held = new HeldMood(inner, ClearVisibility, ClearSea01);
            GameServices.Environment = _held;
            _mood = $"clear, HELD: visibility {ClearVisibility:0.00}, sea state {ClearSea01:0.00} " +
                    $"({_held.Sample().SeaState}); wind, tide and seed the region's own";
        }

        void ReleaseTheMood()
        {
            if (_held == null) return;
            // Only taken back while it is still ours: a service published over it since is not the plate's.
            if (ReferenceEquals(GameServices.Environment, _held)) GameServices.Environment = _held.Inner;
            _held = null;
        }

        /// <summary>
        /// Stop the clock at <paramref name="hours"/> after day 1's midnight, then let the light catch up:
        /// at least <paramref name="minSeconds"/> of engine time, and until the day/night tint has held
        /// still for four frames. The clock stops only on its own TimeScale, so the light (which ticks on
        /// engine time and reads the clock's hour) gets there while the hour cannot drift.
        /// </summary>
        IEnumerator PinTheHour(float hours, float minSeconds)
        {
            IGameClock clock = GameServices.Clock;
            GameConfig config = GameServices.Config;
            Assert.IsNotNull(clock, "the region registered no clock, so the hour cannot be pinned.");
            Assert.IsNotNull(config, "the region registered no GameConfig, so a day has no length.");

            double spd = config.SecondsPerDay;
            double t = (1.0 + hours / 24.0) * spd;
            Time.timeScale = 1f;
            clock.SeekTo(t);
            clock.TimeScale = 0f;
            Assert.LessOrEqual(Math.Abs(clock.TotalSeconds - t), 1.0,
                $"the clock did not land on {t:0.0} s (it reads {clock.TotalSeconds:0.0} s), so the plate would be " +
                "shot at some other hour.");

            Color tint = Shader.GetGlobalColor("_DayNightTint");
            float waited = 0f;
            int still = 0, frames = 0;
            while (frames < 900)
            {
                yield return null;
                frames++;
                waited += Time.deltaTime;
                Color now = Shader.GetGlobalColor("_DayNightTint");
                float move = Mathf.Abs(now.r - tint.r) + Mathf.Abs(now.g - tint.g) + Mathf.Abs(now.b - tint.b);
                still = move < 1e-4f ? still + 1 : 0;
                tint = now;
                if (waited >= minSeconds && still >= 4) break;
            }
            Assert.Less(frames, 900, $"the day/night tint never settled at {Hhmm(hours)}.");

            Vector4 sun = Shader.GetGlobalVector("_SunDir");
            float elevation = Shader.GetGlobalFloat("_SunElevation");
            float hour = clock.HourOfDay;
            int hh = Mathf.FloorToInt(hour), mm = Mathf.FloorToInt((hour - hh) * 60f + 0.5f) % 60;
            _pinned = $"day {clock.DayIndex} {hh:00}:{mm:00} ({hour:0.000} h, TotalSeconds {clock.TotalSeconds:0.0}); " +
                      $"_DayNightTint ({tint.r:0.000}, {tint.g:0.000}, {tint.b:0.000}); _SunDir ({sun.x:0.000}, " +
                      $"{sun.y:0.000}); _SunElevation {elevation:0.000}; settled after {frames} frames / {waited:0.00} s";
            Debug.Log($"[{PlateDir}] pinned: {_pinned}; weather {_mood}");
        }

        /// <summary>The light must move with every hour: a pin that did not take would caption two plates
        /// with different skies that were shot under one.</summary>
        static void AssertTheLightMoved(Dictionary<string, Color> lights, List<string> failures)
        {
            string[] hours = Hours.Select(Hhmm).ToArray();
            for (int a = 0; a < hours.Length; a++)
                for (int b = a + 1; b < hours.Length; b++)
                {
                    if (!lights.TryGetValue(hours[a], out Color x) || !lights.TryGetValue(hours[b], out Color y)) continue;
                    float d = Mathf.Abs(x.r - y.r) + Mathf.Abs(x.g - y.g) + Mathf.Abs(x.b - y.b);
                    if (d < SameLight)
                        failures.Add($"the light at {hours[a]} and at {hours[b]} is the same ({d:0.00000}): the hour did not take.");
                }
        }

        // =============================================================================================
        //  The region and the camera
        // =============================================================================================

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED: no graphics device (Null Device), so nothing rendered " +
                              "and nothing was proved. Expected on CI; a plate of the wharf needs a GPU.");
        }

        /// <summary>
        /// Load a region the way the existing plates do, then wait for the shell to settle at the title.
        /// ⚠ THE SAVE: the save service writes only once the shell has left the title, so a region that
        /// did not boot to it is skipped here, before anything is moved, rather than photographed live.
        /// </summary>
        IEnumerator LoadRegion(string sceneName)
        {
            if (!_loadedAny) _introOnEntry = GameServices.OpeningCinematicRunning;

            GameConfig configBefore = GameServices.Config;
            IGameClock clockBefore = GameServices.Clock;
            IEnvironmentService envBefore = GameServices.Environment;
            IWallet walletBefore = GameServices.Wallet;

            LogAssert.ignoreFailingMessages = true;   // the regions log decor complaints of their own
            _loadedAny = true;
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;   // let the self-installing components register

            var healed = new List<string>();
            if (GameServices.Config == null && configBefore != null) { GameServices.Config = configBefore; healed.Add("Config"); }
            if (GameServices.Clock == null && clockBefore != null) { GameServices.Clock = clockBefore; healed.Add("Clock"); }
            if (GameServices.Environment == null && envBefore != null) { GameServices.Environment = envBefore; healed.Add("Environment"); }
            if (GameServices.Wallet == null && walletBefore != null) { GameServices.Wallet = walletBefore; healed.Add("Wallet"); }
            if (healed.Count > 0)
            {
                _healed = $"{sceneName}: re-published {string.Join(", ", healed)} after the region's dev core took them down";
                Debug.Log($"[{PlateDir}] {_healed}.");
            }

            int frames = 0;
            while (frames < 240 && (FindPersistentFollow() == null || !ShellFlow.AtTitle))
            {
                yield return null;
                frames++;
            }
            if (!ShellFlow.AtTitle)
                Assert.Ignore($"SKIPPED: {sceneName} did not settle at the title (the shell is {ShellFlow.Phase}), " +
                              "so the world is live and the save could be written. A plate never risks the save.");
            Assert.IsNotNull(FindPersistentFollow(),
                $"{sceneName}: after 240 frames there is no CameraFollow on the persistent core's camera, so there " +
                "is no play camera to shoot through.");
            Debug.Log($"[{PlateDir}] {sceneName} loaded: region '{GameServices.CurrentRegionId}', shell " +
                      $"{ShellFlow.Phase}, camera up after {frames} frames.");
        }

        void EnsureTheFollow()
        {
            if (_follow != null) return;
            _follow = FindPersistentFollow();
            Assert.IsNotNull(_follow, "no CameraFollow on the persistent core's camera.");
            Assert.IsTrue(_follow.isActiveAndEnabled, "the play camera's CameraFollow is disabled, so it will not follow the anchor.");
            _cam = _follow.GetComponent<Camera>();
            Assert.IsNotNull(_cam, "the play camera's CameraFollow has no Camera.");
            _followTargetOnEntry = _follow.Target;
            _followSmoothOnEntry = _follow.Smooth;
            _followCaptured = true;
        }

        /// <summary>
        /// Hand the play camera's own follow a still anchor, at <paramref name="worldHeight"/>, with the
        /// 1080 px target attached; let the world run until the camera is ON the centre; then stop the world.
        /// ⚠ The hand-over happens with the world STOPPED, so the follow reads the anchor's jump as no motion
        /// (as <c>VillageReturnPlatePlayTests.FrameOn</c>).
        /// </summary>
        IEnumerator FrameOn(Vector2 at, float worldHeight)
        {
            EnsureTheFollow();
            Time.timeScale = 0f;
            if (_anchor == null)
            {
                _anchor = new GameObject("WharfBuildingPass2PlateAnchor");
                _spawned.Add(_anchor);
            }
            _anchor.transform.position = new Vector3(at.x, at.y, 0f);
            _follow.Target = _anchor.transform;
            _follow.Smooth = 1000f;   // any residue arrives in a frame, not over a second of easing
            _cam.transform.position = new Vector3(at.x, at.y, _cam.transform.position.z);
            _askedHeight = worldHeight;
            _follow.SetFraming(worldHeight, 0f);

            if (_rt == null)
            {
                _w = PlateWidthPx;
                _h = PlateHeightPx;
                _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf);
                _rt.Create();
            }
            _cam.targetTexture = _rt;
            for (int i = 0; i < 2; i++) yield return null;   // the follow reads the anchor while nothing can move

            Time.timeScale = 1f;
            for (int i = 0; i < 8; i++) yield return null;
            yield return SettleCamera(_cam);
            yield return EaseOntoTheCentre(at);

            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;

            Vector3 p = _cam.transform.position;
            float height = _cam.orthographicSize * 2f;
            Assert.LessOrEqual(Vector2.Distance(new Vector2(p.x, p.y), at), OnTheCentreMetres,
                $"the camera sits at ({p.x:0.####}, {p.y:0.####}), not on the station ({at.x:0.###}, {at.y:0.###}), " +
                $"after {_eased}: something else drives it (a region-bounds clamp, a cinematic), so the plate would " +
                "show another stretch of the shore and a before and an after would not line up.");
            Assert.AreEqual(worldHeight, height, worldHeight * 0.01f,
                $"the camera frames {height:0.###} m tall, not the {worldHeight:0.###} m asked for.");
        }

        IEnumerator EaseOntoTheCentre(Vector2 at)
        {
            Vector2 stillAt = _cam.transform.position;
            float start = Time.time;
            int frames = 0;
            while (Vector2.Distance(_cam.transform.position, at) > OnTheCentreMetres && Time.time - start < LeadEaseOutSeconds)
            {
                yield return null;
                frames++;
            }
            _eased = $"still at ({stillAt.x:0.####}, {stillAt.y:0.####}), then {frames} frames / {Time.time - start:0.00} s " +
                     "at full time for the look-ahead to ease out";
        }

        /// <summary>Run the world for <paramref name="seconds"/> on a framed station (the clock stays
        /// stopped, so the hour holds while the water moves), then stop it again. The camera must still be
        /// on the station afterwards.</summary>
        IEnumerator RunTheWorld(float seconds, Vector2 at)
        {
            Time.timeScale = 1f;
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
            Vector3 p = _cam.transform.position;
            Assert.LessOrEqual(Vector2.Distance(new Vector2(p.x, p.y), at), OnTheCentreMetres,
                $"the camera drifted off the station to ({p.x:0.####}, {p.y:0.####}) while the world ran.");
        }

        static IEnumerator SettleCamera(Camera cam)
        {
            Vector3 lastPos = cam.transform.position;
            float lastSize = cam.orthographicSize;
            int still = 0, frames = 0;
            while (still < 5 && frames < 400)
            {
                yield return null;
                frames++;
                bool moved = (cam.transform.position - lastPos).sqrMagnitude > 1e-8f ||
                             Mathf.Abs(cam.orthographicSize - lastSize) > 1e-5f;
                still = moved ? 0 : still + 1;
                lastPos = cam.transform.position;
                lastSize = cam.orthographicSize;
            }
            Assert.Less(frames, 400, "the camera never came to rest.");
        }

        static CameraFollow FindPersistentFollow()
        {
            foreach (CameraFollow f in Object.FindObjectsByType<CameraFollow>())
                if (f != null && MoodGradeDirector.IsPersistentCamera(f.GetComponent<Camera>()))
                    return f;
            return null;
        }

        /// <summary>⭐ THE PLATE MUST CONTAIN ITS SUBJECT, WHOLE: each subject drawn, and its sprite's bounds
        /// inside the frame, so a before and an after show every pixel of each sheet's facing.</summary>
        string CheckTheSubjectIsWhollyInFrame(Station s, string plate, List<string> failures)
        {
            var said = new StringBuilder();
            foreach (SpriteRenderer r in s.Subjects)
            {
                Vector3 lo = _cam.WorldToViewportPoint(r.bounds.min), hi = _cam.WorldToViewportPoint(r.bounds.max);
                bool whole = lo.x >= 0f && lo.y >= 0f && hi.x <= 1f && hi.y <= 1f;
                said.AppendLine($"subject '{r.gameObject.name}' at {Metres(r.transform.position)} from '{r.sprite.name}': " +
                                $"viewport ({lo.x:0.000}, {lo.y:0.000})-({hi.x:0.000}, {hi.y:0.000}), drawn {Drawn(r)}, " +
                                $"whole in frame {whole}");
                if (!Drawn(r)) failures.Add($"{plate}: '{r.gameObject.name}' is not drawn.");
                else if (!whole)
                    failures.Add($"{plate}: '{r.gameObject.name}' is cut by the frame (viewport ({lo.x:0.000}, " +
                                 $"{lo.y:0.000})-({hi.x:0.000}, {hi.y:0.000})).");
            }
            return said.ToString();
        }

        // =============================================================================================
        //  Shooting, captions and files
        // =============================================================================================

        /// <summary>
        /// A plate is a picture of the shaders, not of their cyan stand-ins. Draw the frame once so every
        /// variant it needs has been asked for, then let the editor's background compiler finish before the
        /// shot is taken. Not <c>ShaderUtil.allowAsyncCompilation = false</c>: compiling in place hung a batch
        /// PlayMode run on D3D12 at its first capture (VillageReturnPlatePlayTests, #898).
        /// </summary>
        IEnumerator ShadersReady()
        {
#if UNITY_EDITOR
            _cam.Render();
            float until = Time.realtimeSinceStartup + ShaderWaitSeconds;
            int frames = 0;
            while (UnityEditor.ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < until)
            {
                yield return null;
                frames++;
            }
            if (UnityEditor.ShaderUtil.anythingCompiling)
                Debug.LogWarning($"[{PlateDir}] shaders still compiling after {frames} frame(s) ({ShaderWaitSeconds} s): " +
                                 "this shot may hold a stand-in.");
            else if (frames > 0)
                Debug.Log($"[{PlateDir}] waited {frames} frame(s) for the shaders to compile before the shot.");
#endif
            yield break;
        }

        byte[] Capture()
        {
            _cam.Render();
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_w, _h, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, _w, _h), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prevActive;

            // ⚠ The project renders in LINEAR colour space, so a raw float read-back saves far too dark.
            Color[] px = tex.GetPixels();
            var outBytes = new byte[px.Length * 4];
            for (int i = 0; i < px.Length; i++)
            {
                Color c = new Color(Mathf.Clamp01(px[i].r), Mathf.Clamp01(px[i].g), Mathf.Clamp01(px[i].b), 1f).gamma;
                outBytes[i * 4 + 0] = (byte)Mathf.RoundToInt(c.r * 255f);
                outBytes[i * 4 + 1] = (byte)Mathf.RoundToInt(c.g * 255f);
                outBytes[i * 4 + 2] = (byte)Mathf.RoundToInt(c.b * 255f);
                outBytes[i * 4 + 3] = 255;
            }
            Object.DestroyImmediate(tex);
            return outBytes;
        }

        string Caption(string plate, string subject, Vector2 centre, string extra)
        {
            Vector3 p = _cam.transform.position;
            float height = _cam.orthographicSize * 2f;
            MoodGradeDirector grade = MoodGradeDirector.Instance;
            var sb = new StringBuilder();
            sb.AppendLine($"== {plate} [{_look}]");
            sb.AppendLine(subject);
            sb.AppendLine($"look: {_lookEvidence}");
            sb.AppendLine($"run: started {_runStartedUtc:yyyy-MM-dd HH:mm:ss}Z, Unity {Application.unityVersion}, " +
                          $"{SystemInfo.graphicsDeviceType} on {SystemInfo.graphicsDeviceName}, box {Application.dataPath}");
            sb.AppendLine($"scene: {SceneManager.GetActiveScene().name}; region '{GameServices.CurrentRegionId}'; shell " +
                          $"{ShellFlow.Phase}; services healed: {_healed}");
            sb.AppendLine($"asked centre {Metres(centre)}; camera at ({p.x:0.####}, {p.y:0.####}); {_eased}");
            sb.AppendLine($"ortho {_cam.orthographicSize:0.#####} -> {height * _cam.aspect:0.###} x {height:0.####} m " +
                          $"(asked {_askedHeight:0.####} m); render texture {_w}x{_h} px; " +
                          $"{_follow.WorldUnitsPerRenderedPixel:0.#####} m per rendered pixel");
            sb.AppendLine($"hour and light: {_pinned}");
            sb.AppendLine($"weather: {_mood}");
            sb.AppendLine(grade != null
                ? $"grade: graded {grade.LastTickGraded}, strength {grade.LastStrength:0.00}"
                : "grade: no MoodGradeDirector");
            sb.Append(extra);
            sb.AppendLine();
            return sb.ToString();
        }

        string PlatePath()
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir, _look);
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>This case's file, emptied: a caption or a number from an earlier run is never read as
        /// this one's.</summary>
        string StartFile(string name)
        {
            string path = Path.Combine(PlatePath(), name);
            File.WriteAllText(path, "", Utf8);
            return path;
        }

        void SavePlate(string name, byte[] rgbaBottomLeft)
        {
            var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgbaBottomLeft);
            tex.Apply();
            string path = Path.Combine(PlatePath(), name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[{PlateDir}] plate written: {path}");
        }

        static string Hhmm(float hours)
        {
            float h = hours % 24f;
            int hh = Mathf.FloorToInt(h), mm = Mathf.RoundToInt((h - hh) * 60f);
            return $"{hh:00}{mm:00}";
        }

        static string Metres(Vector3 v) => $"({v.x:0.##}, {v.y:0.##})";

        static string Metres(Vector2 v) => $"({v.x:0.##}, {v.y:0.##})";

        static Vector2 OnTheHalfMetre(Vector3 v) => new Vector2(Mathf.Round(v.x * 2f) * 0.5f, Mathf.Round(v.y * 2f) * 0.5f);

        static T ReadContract<T>(string relativePath) where T : class
        {
            string path = Path.Combine(Application.dataPath, relativePath);
            Assert.IsTrue(File.Exists(path), $"the contract is not at {path}");
            var contract = JsonUtility.FromJson<T>(File.ReadAllText(path));
            Assert.IsNotNull(contract, $"{path} does not parse");
            return contract;
        }

        // =============================================================================================
        //  DontDestroyOnLoad bookkeeping (#764)
        // =============================================================================================

        static Scene PersistentScene()
        {
            var probe = new GameObject("__ddolProbe");
            Object.DontDestroyOnLoad(probe);
            Scene s = probe.scene;
            Object.DestroyImmediate(probe);
            return s;
        }

        static List<GameObject> PersistentRoots()
        {
            Scene s = PersistentScene();
            return s.IsValid() ? s.GetRootGameObjects().ToList() : new List<GameObject>();
        }
    }
}
