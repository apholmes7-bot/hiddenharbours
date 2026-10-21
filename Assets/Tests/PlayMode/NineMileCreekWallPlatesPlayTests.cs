#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <para><b>NMC H3 Phase B: the creek's walls on G3, as plates and as a frame-time pair.</b> The game's own camera
    /// over the scene as it ships, at the month's lowest and highest DAYLIGHT water (09:00 to 16:00, found by searching
    /// the live tide from day 1, never by picking hours): every standing wall of the <c>CliffWalls</c> root, framed on
    /// what it draws; the south shore's bluff, the salt pond's berm, the ramp's opening (where the eight walls step 07
    /// stood down stood) and the wharf's west corner.</para>
    ///
    /// <para>These are documents, not goldens: a plate case asserts only that each plate rendered something (not one
    /// flat colour) and that every standing wall draws (a wall with no renderer has no baked face). The bake is
    /// gitignored and its GUIDs are the baking checkout's own, so on a box with a bake of its own every case first
    /// binds, in play only, each wall that draws nothing to this box's faces for the same face
    /// (<see cref="BindThisBoxsFaces"/>), and logs how many it bound. Each plate's log
    /// line names the wall, its renderers and drawn extent, the water and the ground at the plate's centre. The plates
    /// go to <c>temporaryCachePath/nmc-h3-b/</c>, one render target reused for all of them.</para>
    ///
    /// <para>The frame-time case is a measurement, run on the scene before step 11 and after it for the base-against-head
    /// pair: the game's own on-foot framing into a 1920x1080 target over the ramp's opening, two runs of walls and the
    /// harbour (a control), with frames and synced renders timed as <c>NineMileCreekFrameTimePlayTests</c> times them,
    /// the profiler's draw-call, batch and set-pass counters per view (they may read 0 in a batch editor; the census
    /// of renderers in frame stands in for them), and texture memory. The machine, the scene's SHA-256 and every number
    /// go to a JSON beside the plates. A case loads the creek afresh the way the loop does and enters the world through
    /// Continue, which writes nothing.</para>
    ///
    /// <para>Every case needs a GPU: with no graphics device (CI's Null device) each one reads Skipped before it loads
    /// anything, and the teardown has nothing to put back.</para>
    /// </summary>
    public class NineMileCreekWallPlatesPlayTests
    {
        const string SceneName = "NineMileCreek";
        const string PlateDir = "nmc-h3-b";
        const int PlateHeightPx = 900, PlateWidthPx = 1600;
        // One asset pixel per plate pixel: a wall's whole run, brow to toe, with its shore around it.
        const float PlateWorldHeightMetres = PlateHeightPx / (float)CameraFollow.AssetsPPU;
        const int PerfWidthPx = 1920, PerfHeightPx = 1080;
        const int WarmFrames = 120, LoopRenderProbeFrames = 60, FrameSamples = 600, RenderSamples = 300;
        const int RenderSamplesPerYield = 30;
        const double DaylightFromHour = 9.0, DaylightToHour = 16.0, MonthDays = 30.0, StepsPerDay = 240.0;
        const int MaxSettleFrames = 600, MaxFrameOnFrames = 400;
        // Luma standard deviation, 0..255: below it the plate is one colour (NineMileCreekTidePlatesPlayTests' bar).
        const double FlatPlateSpread = 1.0;

        enum Water { Low, High }

        readonly struct Place
        {
            public readonly string Name;
            public readonly Vector2 At;
            public Place(string name, float x, float y) { Name = name; At = new Vector2(x, y); }
        }

        /// <summary>The places that are not one wall (game metres): the bluff along the south shore's estates, west to
        /// east; the salt pond's berm; the eight retired walls' drawn extent across Wharf Road's ramp; the wharf's west
        /// corner, which goes to the quay's item.</summary>
        static readonly Place[] Places =
        {
            new Place("bluff-west", -345f, -164f),
            new Place("bluff-middle", -310f, -164f),
            new Place("bluff-east", -270f, -164f),
            new Place("salt-pond", -224f, -168f),
            new Place("ramp-opening", 54.3f, 90.5f),
            new Place("wharf-west-corner", 49.3f, 71f),
        };

        /// <summary>The frame-time views: where the eight retired walls stood, the runs 036 to 045 and 000 to 016 (the
        /// centres of their drawn extents after step 11), and the harbour, where no wall is in frame.</summary>
        static readonly Place[] Views =
        {
            new Place("ramp-opening", 54.3f, 90.5f),
            new Place("walls-036-045", 35.6f, 25.2f),
            new Place("walls-000-016", 40.0f, -58.2f),
            new Place("harbour", 105f, 80f),
        };

        static readonly int IdUnlit = Shader.PropertyToID("_Unlit");
        static readonly int IdNormal = Shader.PropertyToID("_Normal");
        static readonly int IdMask = Shader.PropertyToID("_Mask");
        static readonly FieldInfo BrowPlan = typeof(CliffWallSurface).GetField("_browPlan", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo ToePlan = typeof(CliffWallSurface).GetField("_toePlan", BindingFlags.NonPublic | BindingFlags.Instance);

        [System.Serializable]
        public class ViewResult
        {
            public string view;
            public float x, y, worldHeightMetres;
            public bool loopDrawsTheCamera;
            public int frames;
            public double frameP50Ms, frameP95Ms, frameMaxMs;
            public int renders;
            public double renderP50Ms, renderP95Ms, renderMaxMs;
            public bool countersValid;
            public long drawCallsP50, batchesP50, setPassP50;
            public int renderersInFrame, wallsInFrame, wallRenderersInFrame, wallMaterials, wallTextures;
            public long wallTextureBytes;
            public string renderersInFrameByKind;
        }

        [System.Serializable]
        public class Report
        {
            public string utc, scenePath, sceneSha256, unity, cpu, gpu, gpuApi, os;
            public int cpuCount, cpuMHz, gpuMemoryMB, systemMemoryMB, width, height, worldSeed;
            public float water;
            public double hour;
            public int walls, wallRenderers, renderersDrawing;
            public string faces;
            public long textureMemoryCounter, textureCount, currentTextureMemory, totalTextureMemory;
            public long allocatedBytes, reservedBytes, graphicsDriverBytes;
            public long allocatedBytesAfter, reservedBytesAfter, graphicsDriverBytesAfter;
            public List<ViewResult> views = new List<ViewResult>();
        }

        GameObject _core, _anchor;
        Camera _cam;
        RenderTexture _rt;
        Texture2D _probe;
        bool _loaded, _qualityTouched, _counting;
        int _vSyncCount, _targetFrameRate, _camDraws;
        float _engineTimeScale = 1f;

        [UnityTest, Timeout(1800000)]
        public IEnumerator TheWalls_AtLowWater_ThePlates()
        {
            // ⚠️ FIRST statement, before any yield (see WharfNightStage.RequireAGraphicsDevice).
            WharfNightStage.RequireAGraphicsDevice();
            for (var e = Shoot(Water.Low); e.MoveNext();) yield return e.Current;
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator TheWalls_AtHighWater_ThePlates()
        {
            // ⚠️ FIRST statement, before any yield (see WharfNightStage.RequireAGraphicsDevice).
            WharfNightStage.RequireAGraphicsDevice();
            for (var e = Shoot(Water.High); e.MoveNext();) yield return e.Current;
        }

        IEnumerator Shoot(Water state)
        {
            for (var e = LoadTheCreek(); e.MoveNext();) yield return e.Current;
            string faces = BindThisBoxsFaces(TheWalls());
            double t = FindTheWater(state, out float level, out double hour);
            for (var e = SeekAndSettle(t); e.MoveNext();) yield return e.Current;

            _rt = new RenderTexture(PlateWidthPx, PlateHeightPx, 24, RenderTextureFormat.ARGBHalf);
            _rt.Create();
            _cam.targetTexture = _rt;
            _cam.aspect = (float)PlateWidthPx / PlateHeightPx;

            List<CliffWallSurface> walls = TheWalls();
            Assert.That(walls, Is.Not.Empty, $"the creek's {NineMileCreekCliffWalls.RootName} root holds no standing wall.");
            var shots = new List<(string name, Vector2 at, string note)>();
            var undrawn = new List<string>();
            foreach (CliffWallSurface w in walls)
            {
                Bounds? drawn = DrawnBounds(w);
                Vector2 at = drawn.HasValue ? (Vector2)drawn.Value.center : PlanCentre(w);
                shots.Add(($"wall-{Id(w)}", at, $"{w.name}: {w.RendererCount} renderers, " +
                           (drawn.HasValue ? $"drawn {drawn.Value.size.x:0.##} x {drawn.Value.size.y:0.##} m" : "draws nothing")));
                if (!drawn.HasValue) undrawn.Add(w.name);
            }
            foreach (Place p in Places) shots.Add((p.Name, p.At, p.Name));

            string tide = state.ToString().ToLowerInvariant();
            int shot = 0;
            var flat = new List<string>();
            foreach (var s in shots)
            {
                shot++;
                for (var e = FrameOn(s.at, PlateWorldHeightMetres); e.MoveNext();) yield return e.Current;
                byte[] px = Capture(PlateWidthPx, PlateHeightPx);
                string name = $"{tide}-{shot:000}-{s.name}-at-{tide}-water.png";
                string path = SavePlate(name, px);
                double spread = Spread(px);
                float ground = GameServices.TidalTerrain != null ? GameServices.TidalTerrain.ElevationAt(s.at) : float.NaN;
                float water = GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds);
                Debug.Log($"[{PlateDir}] {name}: {s.note}; {tide} water {water:0.000} m (searched {level:0.000} m at " +
                          $"{hour:0.00} h, seed {GameServices.Environment.WorldSeed}); centre ({s.at.x:0.##}, {s.at.y:0.##}) " +
                          $"ground {ground:0.###} m; camera ortho {_cam.orthographicSize:0.##}; luma spread {spread:0.#}");
                if (spread < FlatPlateSpread) flat.Add($"{name} ({spread:0.##})");
                Debug.Log($"[{PlateDir}] plate written: {path}");
            }
            Debug.Log($"[{PlateDir}] {tide} water: {walls.Count} standing walls and {Places.Length} places, {shot} plates; {faces}");
            Assert.That(flat, Is.Empty, $"plates of one flat colour (nothing rendered): {string.Join(", ", flat)}");
            Assert.That(undrawn, Is.Empty, $"standing walls that draw nothing (no baked face?): {string.Join(", ", undrawn)}");
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator TheWalls_At1080p_FrameTimeAndMemory()
        {
            // ⚠️ FIRST statement, before any yield (see WharfNightStage.RequireAGraphicsDevice).
            WharfNightStage.RequireAGraphicsDevice();

            for (var e = LoadTheCreek(); e.MoveNext();) yield return e.Current;
            string faces = BindThisBoxsFaces(TheWalls());
            double t = FindTheWater(null, out float level, out double hour);
            for (var e = SeekAndSettle(t); e.MoveNext();) yield return e.Current;

            _vSyncCount = QualitySettings.vSyncCount;
            _targetFrameRate = Application.targetFrameRate;
            _qualityTouched = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            _rt = new RenderTexture(PerfWidthPx, PerfHeightPx, 24, RenderTextureFormat.ARGBHalf);
            _rt.Create();
            _cam.targetTexture = _rt;
            _cam.aspect = (float)PerfWidthPx / PerfHeightPx;
            _probe = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);

            yield return Resources.UnloadUnusedAssets();
            System.GC.Collect();
            yield return null;

            List<CliffWallSurface> walls = TheWalls();
            var wallRenderers = new HashSet<Renderer>(walls.SelectMany(w => w.GetComponentsInChildren<Renderer>(false)));
            var report = new Report
            {
                utc = System.DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"),
                scenePath = NineMileCreekLayerRefresh.ScenePath,
                sceneSha256 = Sha256Of(NineMileCreekLayerRefresh.ScenePath),
                unity = Application.unityVersion,
                cpu = SystemInfo.processorType,
                cpuCount = SystemInfo.processorCount,
                cpuMHz = SystemInfo.processorFrequency,
                gpu = SystemInfo.graphicsDeviceName,
                gpuApi = $"{SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceVersion})",
                gpuMemoryMB = SystemInfo.graphicsMemorySize,
                systemMemoryMB = SystemInfo.systemMemorySize,
                os = SystemInfo.operatingSystem,
                width = PerfWidthPx,
                height = PerfHeightPx,
                worldSeed = GameServices.Environment.WorldSeed,
                water = level,
                hour = hour,
                walls = walls.Count,
                faces = faces,
                wallRenderers = wallRenderers.Count,
                renderersDrawing = Object.FindObjectsByType<Renderer>().Count(r => r.enabled && r.gameObject.activeInHierarchy),
                allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                reservedBytes = Profiler.GetTotalReservedMemoryLong(),
                graphicsDriverBytes = Profiler.GetAllocatedMemoryForGraphicsDriver(),
            };

            var follow = Object.FindAnyObjectByType<CameraFollow>();
            foreach (Place v in Views)
            {
                for (var e = FrameOn(v.At, null); e.MoveNext();) yield return e.Current;
                for (int i = 0; i < WarmFrames; i++) yield return null;

                // Does the batch loop draw the camera by itself? (A batch editor has no game view to repaint.)
                _camDraws = 0;
                _counting = true;
                RenderPipelineManager.endCameraRendering += CountCamDraws;
                for (int i = 0; i < LoopRenderProbeFrames; i++) yield return null;
                RenderPipelineManager.endCameraRendering -= CountCamDraws;
                _counting = false;
                bool loopDraws = _camDraws >= LoopRenderProbeFrames / 2;

                var frames = new List<double>(FrameSamples);
                var draws = new List<double>(FrameSamples);
                var batches = new List<double>(FrameSamples);
                var setPasses = new List<double>(FrameSamples);
                var drawCounter = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                var batchCounter = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                var setPassCounter = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                bool valid;
                try
                {
                    var wall = Stopwatch.StartNew();
                    double last = 0.0;
                    for (int i = 0; i < FrameSamples; i++)
                    {
                        yield return null;
                        if (!loopDraws) _cam.Render();
                        Sync();
                        double now = wall.Elapsed.TotalMilliseconds;
                        frames.Add(now - last);
                        last = now;
                        draws.Add(drawCounter.Valid ? drawCounter.LastValue : 0);
                        batches.Add(batchCounter.Valid ? batchCounter.LastValue : 0);
                        setPasses.Add(setPassCounter.Valid ? setPassCounter.LastValue : 0);
                    }
                    valid = drawCounter.Valid && batchCounter.Valid && setPassCounter.Valid;
                }
                finally
                {
                    drawCounter.Dispose();
                    batchCounter.Dispose();
                    setPassCounter.Dispose();
                }

                var renders = new List<double>(RenderSamples);
                var sw = new Stopwatch();
                for (int i = 0; i < RenderSamples; i++)
                {
                    Sync();   // the GPU idle before the clock starts
                    sw.Restart();
                    _cam.Render();
                    Sync();
                    sw.Stop();
                    renders.Add(sw.Elapsed.TotalMilliseconds);
                    if (i % RenderSamplesPerYield == RenderSamplesPerYield - 1) yield return null;
                }

                var r = new ViewResult
                {
                    view = v.Name,
                    x = v.At.x,
                    y = v.At.y,
                    worldHeightMetres = CameraFollow.WorldHeightForOrthoSize(_cam.orthographicSize),
                    loopDrawsTheCamera = loopDraws,
                    frames = frames.Count,
                    frameP50Ms = Percentile(frames, 0.50),
                    frameP95Ms = Percentile(frames, 0.95),
                    frameMaxMs = Percentile(frames, 1.00),
                    renders = renders.Count,
                    renderP50Ms = Percentile(renders, 0.50),
                    renderP95Ms = Percentile(renders, 0.95),
                    renderMaxMs = Percentile(renders, 1.00),
                    countersValid = valid,
                    drawCallsP50 = (long)Percentile(draws, 0.50),
                    batchesP50 = (long)Percentile(batches, 0.50),
                    setPassP50 = (long)Percentile(setPasses, 0.50),
                };
                TakeTheCensus(walls, wallRenderers, r);
                report.views.Add(r);
                Debug.Log($"[{PlateDir}] perf {r.view} ({r.x:0.#}, {r.y:0.#}) at {PerfWidthPx}x{PerfHeightPx}, world height " +
                          $"{r.worldHeightMetres:0.##} m, loop draws the camera {r.loopDrawsTheCamera}: frame p50 " +
                          $"{r.frameP50Ms:0.00} ms, p95 {r.frameP95Ms:0.00} ms, max {r.frameMaxMs:0.00} ms ({r.frames}); " +
                          $"render p50 {r.renderP50Ms:0.00} ms, p95 {r.renderP95Ms:0.00} ms, max {r.renderMaxMs:0.00} ms ({r.renders}); " +
                          (r.countersValid && r.drawCallsP50 > 0
                              ? $"draw calls {r.drawCallsP50}, batches {r.batchesP50}, set-pass {r.setPassP50} (p50); "
                              : "draw calls NOT MEASURED (the counters read 0 or are not offered); ") +
                          $"in frame {r.renderersInFrame} renderers ({r.renderersInFrameByKind}), {r.wallsInFrame} walls with " +
                          $"{r.wallRenderersInFrame} renderers, {r.wallMaterials} wall material(s), {r.wallTextures} wall textures " +
                          $"{r.wallTextureBytes} B");
            }
            if (follow != null) follow.Target = null;

            using (var texMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory"))
            using (var texCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Count"))
            {
                for (int i = 0; i < 3; i++) yield return null;
                report.textureMemoryCounter = texMemory.Valid ? texMemory.LastValue : -1;
                report.textureCount = texCount.Valid ? texCount.LastValue : -1;
            }
            report.currentTextureMemory = (long)Texture.currentTextureMemory;
            report.totalTextureMemory = (long)Texture.totalTextureMemory;

            yield return Resources.UnloadUnusedAssets();
            System.GC.Collect();
            yield return null;
            report.allocatedBytesAfter = Profiler.GetTotalAllocatedMemoryLong();
            report.reservedBytesAfter = Profiler.GetTotalReservedMemoryLong();
            report.graphicsDriverBytesAfter = Profiler.GetAllocatedMemoryForGraphicsDriver();

            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"perf-{report.sceneSha256.Substring(0, 8)}-{report.utc}.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log($"[{PlateDir}] perf machine: {report.cpu} x{report.cpuCount} @ {report.cpuMHz} MHz, {report.gpu} " +
                      $"{report.gpuApi} {report.gpuMemoryMB} MB, RAM {report.systemMemoryMB} MB, {report.os}, Unity {report.unity}");
            Debug.Log($"[{PlateDir}] perf scene {report.sceneSha256}: {report.walls} walls ({report.wallRenderers} renderers), " +
                      $"{report.renderersDrawing} renderers drawing; texture memory counter {Mb(report.textureMemoryCounter)} MB " +
                      $"({report.textureCount} textures), current {Mb(report.currentTextureMemory)} MB, total " +
                      $"{Mb(report.totalTextureMemory)} MB; allocated {Mb(report.allocatedBytes)} -> {Mb(report.allocatedBytesAfter)} MB, " +
                      $"reserved {Mb(report.reservedBytes)} -> {Mb(report.reservedBytesAfter)} MB, graphics driver " +
                      $"{Mb(report.graphicsDriverBytes)} -> {Mb(report.graphicsDriverBytesAfter)} MB");
            Debug.Log($"[{PlateDir}] perf written: {path}");

            Assert.That(report.views.Count, Is.EqualTo(Views.Length), "a view was not measured.");
            foreach (ViewResult r in report.views)
            {
                Assert.That(r.frames, Is.EqualTo(FrameSamples), $"{r.view}: frames missing.");
                Assert.That(r.renders, Is.EqualTo(RenderSamples), $"{r.view}: renders missing.");
                Assert.That(r.frameP95Ms, Is.GreaterThan(0.0), $"{r.view}: no frame time measured.");
                Assert.That(r.renderP95Ms, Is.GreaterThan(0.0), $"{r.view}: no render time measured.");
            }
        }

        [UnityTearDown]
        public IEnumerator TearDownTheCreek()
        {
            // A case the guard skipped (every case on CI) loaded nothing and changed nothing: there is nothing to put back.
            if (!_loaded) yield break;
            _loaded = false;
            if (_counting) { RenderPipelineManager.endCameraRendering -= CountCamDraws; _counting = false; }
            if (_qualityTouched)
            {
                QualitySettings.vSyncCount = _vSyncCount;
                Application.targetFrameRate = _targetFrameRate;
                _qualityTouched = false;
            }
            Time.timeScale = _engineTimeScale > 0f ? _engineTimeScale : 1f;
            if (_cam != null) { _cam.targetTexture = null; _cam.ResetAspect(); }
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            if (_probe != null) { Object.DestroyImmediate(_probe); _probe = null; }
            if (_anchor != null) Object.Destroy(_anchor);
            if (_core != null) Object.Destroy(_core);
            yield return null;
            GameServices.Clock = null;
            GameServices.Environment = null;

            const string cleanupName = "NineMileCreekWallPlatesCleanup";
            Scene clean = SceneManager.GetSceneByName(cleanupName);
            if (!clean.IsValid() || !clean.isLoaded) clean = SceneManager.CreateScene(cleanupName);
            if (clean.IsValid()) SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == SceneName)
                    yield return SceneManager.UnloadSceneAsync(s);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        // ---- the stage --------------------------------------------------------------------------------------

        IEnumerator LoadTheCreek()
        {
            _loaded = true;   // from here the case changes what its teardown puts back
            LogAssert.ignoreFailingMessages = true;   // the region's known decor-import errors
            _engineTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            foreach (GameRoot old in Object.FindObjectsByType<GameRoot>()) Object.Destroy(old.transform.root.gameObject);
            yield return null;
            GameServices.Clock = null;
            GameServices.Environment = null;

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;

            GameRoot[] roots = Object.FindObjectsByType<GameRoot>();
            Assert.That(roots.Length, Is.EqualTo(1), $"{roots.Length} cores in play; the creek's own dev core must be the only one.");
            _core = roots[0].transform.root.gameObject;
            Assert.That(GameServices.Clock, Is.Not.Null, "the creek's core wired no clock.");
            Assert.That(GameServices.Environment, Is.Not.Null, "the creek's core wired no environment.");
            Assert.That(GameServices.Config, Is.Not.Null, "the creek's core published no GameConfig.");
            ShellFlow.ContinueGame();   // into the world; Continue writes nothing
            yield return null;

            _cam = Camera.main;
            Assert.That(_cam, Is.Not.Null, "no main camera in the creek's dev core.");
            Assert.That(Object.FindAnyObjectByType<CameraFollow>(), Is.Not.Null, "the creek's dev core has no CameraFollow.");
        }

        /// <summary>The standing walls: the active <see cref="CliffWallSurface"/>s under the scene's <c>CliffWalls</c>
        /// root, by real id (a wall's name ends in it).</summary>
        static List<CliffWallSurface> TheWalls()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == NineMileCreekCliffWalls.RootName);
            Assert.That(root, Is.Not.Null, $"the creek has no {NineMileCreekCliffWalls.RootName} root.");
            return root.GetComponentsInChildren<CliffWallSurface>(false).OrderBy(Id, System.StringComparer.Ordinal).ToList();
        }

        static string Id(CliffWallSurface w) => w.name.Length >= 3 ? w.name.Substring(w.name.Length - 3) : w.name;

        static Bounds? DrawnBounds(CliffWallSurface w)
        {
            Bounds? b = null;
            foreach (Renderer r in w.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled) continue;
                if (b.HasValue) { Bounds u = b.Value; u.Encapsulate(r.bounds); b = u; }
                else b = r.bounds;
            }
            return b;
        }

        /// <summary>Where a wall that draws nothing stands: the middle of its brow and toe stations.</summary>
        static Vector2 PlanCentre(CliffWallSurface w)
        {
            var points = ((Vector2[])BrowPlan.GetValue(w)).Concat((Vector2[])ToePlan.GetValue(w)).ToArray();
            return points.Length == 0 ? (Vector2)w.transform.position
                                      : new Vector2(points.Average(p => p.x), points.Average(p => p.y));
        }

        // ---- this box's faces -------------------------------------------------------------------------------

        readonly struct BoxFace
        {
            public readonly CliffFaceBand[] Bands;
            public readonly Texture2D Profile, Brow, Toe;
            public BoxFace(CliffFaceBand[] bands, Texture2D profile, Texture2D brow, Texture2D toe)
            { Bands = bands; Profile = profile; Brow = brow; Toe = toe; }

            public bool SameAs(BoxFace o) =>
                Profile == o.Profile && Brow == o.Brow && Toe == o.Toe && Bands.Length == o.Bands.Length &&
                Bands.Zip(o.Bands, (a, b) => a.Unlit == b.Unlit && a.Normal == b.Normal && a.Mask == b.Mask).All(x => x);
        }

        /// <summary>
        /// ⚠️ <b>A wall finds its faces by the GUIDs in the bake's .meta files, and the bake is gitignored</b>: those
        /// GUIDs exist only in the checkout that baked them (<c>CliffBaker</c>), so on a box with a bake of its own
        /// the scene's walls point at nothing and draw nothing. In play only, never saved, each standing wall that
        /// draws nothing is given the faces the creek's wall writer loads on this box for the same face: a live
        /// wall's from its Def's chunk (<c>NineMileCreekCliffWalls.TryLoadBands</c> and <c>LoadDecals</c>), and a
        /// wall step 11 retires from a live wall wearing the same face in the scene's text (a face's fields are one
        /// text on every wall that wears it). A rebound band keeps the scene's own label and depths and takes only
        /// this box's pixels; a wall whose band recipe on this box is not the scene's is refused. A wall whose faces
        /// resolve is left as it is. The editor-only look (<c>CliffBakeMenu.IsPxLook</c>,
        /// <c>CliffCatalog.ColourChannel</c>) is asked by reflection: this assembly does not reference them.
        /// </summary>
        static string BindThisBoxsFaces(List<CliffWallSurface> walls)
        {
            List<CliffWallSurface> faceless = walls.Where(w => w.RendererCount == 0).ToList();
            if (faceless.Count == 0) return $"all {walls.Count} standing walls draw with the faces the scene names";

            const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            const BindingFlags Field = BindingFlags.NonPublic | BindingFlags.Instance;
            bool px = (bool)EditorType("HiddenHarbours.Tools.RigBaking.CliffBakeMenu").GetProperty("IsPxLook", Static).GetValue(null);
            string colour = (string)EditorType("HiddenHarbours.Art.Editor.CliffCatalog").GetMethod("ColourChannel", Static)
                                                                                         .Invoke(null, new object[] { px });

            string text = File.ReadAllText(NineMileCreekLayerRefresh.ScenePath).Replace("\r\n", "\n");
            StPetersLayerRefresh.SceneYaml scene = StPetersLayerRefresh.SceneYaml.Parse(text);
            Dictionary<string, string> faceOf = StPetersCliffWalls.WallsInScene(text)
                .ToDictionary(w => w.Name, w => FaceText(scene.Get(w.Surface).Text));

            List<CliffWallDef> defs = NineMileCreekCliffWalls.LoadDefs();
            List<StPetersCliffWalls.Chunk> chunks = StPetersCliffWalls.ChunksOfDefs(
                defs, NineMileCreekCliffWalls.ChunkToeSpanMetres, out List<CliffWallDef> owners);
            MethodInfo tryLoadBands = typeof(NineMileCreekCliffWalls).GetMethod("TryLoadBands", Static);
            MethodInfo loadDecals = typeof(NineMileCreekCliffWalls).GetMethod("LoadDecals", Static);
            Assert.That(tryLoadBands != null && loadDecals != null, "NineMileCreekCliffWalls no longer has TryLoadBands/LoadDecals.");

            var byFace = new Dictionary<string, BoxFace>();
            var refused = new List<string>();
            for (int k = 0; k < chunks.Count; k++)
            {
                string name = StPetersCliffWalls.WallName(owners[k]);
                if (!faceOf.TryGetValue(name, out string face) || face == null) continue;
                var bandArgs = new object[] { chunks[k], colour, null, null };
                if (!(bool)tryLoadBands.Invoke(null, bandArgs)) continue;
                var decalArgs = new object[] { chunks[k], null, null };
                loadDecals.Invoke(null, decalArgs);
                var f = new BoxFace((CliffFaceBand[])bandArgs[2], (Texture2D)bandArgs[3], (Texture2D)decalArgs[1], (Texture2D)decalArgs[2]);
                if (byFace.TryGetValue(face, out BoxFace had) && !had.SameAs(f))
                    refused.Add($"{name}: wears a face another live wall wears, and loads other pixels for it on this box");
                else byFace[face] = f;
            }

            FieldInfo bandsField = typeof(CliffWallSurface).GetField("_bands", Field);
            FieldInfo profileField = typeof(CliffWallSurface).GetField("_profile", Field);
            FieldInfo browField = typeof(CliffWallSurface).GetField("_browDecal", Field);
            FieldInfo toeField = typeof(CliffWallSurface).GetField("_toeDecal", Field);
            int bound = 0;
            var left = new List<string>();
            foreach (CliffWallSurface w in faceless)
            {
                if (!faceOf.TryGetValue(w.name, out string face) || face == null || !byFace.TryGetValue(face, out BoxFace f))
                {
                    left.Add($"{w.name} (no live wall wears its face)");
                    continue;
                }
                var scenes = (CliffFaceBand[])bandsField.GetValue(w);
                bool sameRecipe = scenes.Length == f.Bands.Length && scenes.Zip(f.Bands, (a, b) =>
                    a.Label == b.Label && Mathf.Abs(a.StartSurfaceMetres - b.StartSurfaceMetres) < 1e-4f &&
                    Mathf.Abs(a.EndSurfaceMetres - b.EndSurfaceMetres) < 1e-4f).All(x => x);
                if (!sameRecipe)
                {
                    refused.Add($"{w.name}: the scene's bands ({string.Join(", ", scenes.Select(Describe))}) are not the ones " +
                                $"its face loads on this box ({string.Join(", ", f.Bands.Select(Describe))})");
                    continue;
                }
                var bands = new CliffFaceBand[scenes.Length];
                for (int i = 0; i < bands.Length; i++)
                {
                    bands[i] = scenes[i];
                    bands[i].Unlit = f.Bands[i].Unlit;
                    bands[i].Normal = f.Bands[i].Normal;
                    bands[i].Mask = f.Bands[i].Mask;
                }
                bandsField.SetValue(w, bands);
                profileField.SetValue(w, f.Profile);
                browField.SetValue(w, f.Brow);
                toeField.SetValue(w, f.Toe);
                w.enabled = false;   // OnDisable tears the wall down, OnEnable rebuilds it on the faces just given
                w.enabled = true;
                if (w.RendererCount > 0) bound++;
                else left.Add($"{w.name} (bound, still draws nothing)");
            }
            Assert.That(refused, Is.Empty, $"faces this box cannot stand in for:\n{string.Join("\n", refused)}");
            string summary = $"{faceless.Count} of {walls.Count} standing walls named faces this box's bake does not hold " +
                             $"(its GUIDs are its own; colour slot {colour}); {bound} bound in play to this box's faces for " +
                             $"the same face over {byFace.Count} faces, {left.Count} not" +
                             (left.Count > 0 ? $": {string.Join(", ", left)}" : "");
            Debug.Log($"[{PlateDir}] faces: {summary}");
            return summary;
        }

        /// <summary>A wall surface's face fields as the scene writes them: <c>_bands</c> to <c>_toeDecal</c>.</summary>
        static string FaceText(string surface)
        {
            int from = surface.IndexOf("\n  _bands:", System.StringComparison.Ordinal);
            int to = surface.IndexOf("\n  _wallAzimuth:", System.StringComparison.Ordinal);
            return from < 0 || to < from ? null : surface.Substring(from, to - from);
        }

        static string Describe(CliffFaceBand b) => $"{b.Label} {b.StartSurfaceMetres:0.###}..{b.EndSurfaceMetres:0.###}";

        static System.Type EditorType(string fullName)
        {
            System.Type t = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName)).FirstOrDefault(x => x != null);
            Assert.That(t, Is.Not.Null, $"{fullName} is not loaded.");
            return t;
        }

        /// <summary>The month's lowest or highest daylight water from day 1, or (no state) the moment nearest the mean.</summary>
        static double FindTheWater(Water? state, out float level, out double hour)
        {
            IEnvironmentService env = GameServices.Environment;
            double day = GameServices.Config.SecondsPerDay;
            double best = day;   // from day 1, not the save's hour: the same moment on every run
            float bestKey = float.MaxValue;
            level = 0f;
            for (int i = 0; i <= MonthDays * StepsPerDay; i++)
            {
                double t = day + i * day / StepsPerDay;
                double h = (t / day % 1.0) * 24.0;
                if (h < DaylightFromHour || h > DaylightToHour) continue;
                float w = env.WaterLevelAt(t);
                float key = state == Water.Low ? w : state == Water.High ? -w : Mathf.Abs(w);
                if (key < bestKey) { bestKey = key; best = t; level = w; }
            }
            hour = (best / day % 1.0) * 24.0;
            return best;
        }

        static IEnumerator SeekAndSettle(double at)
        {
            IGameClock clock = GameServices.Clock;
            Time.timeScale = 1f;
            clock.TimeScale = 1f;
            clock.SeekTo(at);
            Color tint = Shader.GetGlobalColor("_DayNightTint");
            int still = 0, frames = 0;
            while (still < 4 && frames < MaxSettleFrames)
            {
                yield return null;
                frames++;
                Color now = Shader.GetGlobalColor("_DayNightTint");
                float move = Mathf.Abs(now.r - tint.r) + Mathf.Abs(now.g - tint.g) + Mathf.Abs(now.b - tint.b);
                still = move < 1e-4f ? still + 1 : 0;
                tint = now;
            }
            clock.TimeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
        }

        /// <summary>Frames the camera on a point: at a plate's height when one is given (the scene then holds still),
        /// else at the game's own on-foot framing, where only the follow's target moves.</summary>
        IEnumerator FrameOn(Vector2 at, float? worldHeightMetres)
        {
            var follow = Object.FindAnyObjectByType<CameraFollow>();
            if (_anchor == null) _anchor = new GameObject("NmcWallPlateAnchor (test)");
            _anchor.transform.position = new Vector3(at.x, at.y, 0f);
            follow.Target = _anchor.transform;
            follow.Smooth = 1000f;   // any residue arrives in a frame, not over a second of easing
            _cam.transform.position = new Vector3(at.x, at.y, _cam.transform.position.z);
            if (worldHeightMetres.HasValue)
            {
                follow.SetFraming(worldHeightMetres.Value, 0f);
                Time.timeScale = 0f;     // the follow runs on unscaled time; the scene holds still for the plate
            }

            Vector3 lastPos = _cam.transform.position;
            float lastSize = _cam.orthographicSize;
            int still = 0, frames = 0;
            while (still < 5 && frames < MaxFrameOnFrames)
            {
                yield return null;
                frames++;
                bool moved = (_cam.transform.position - lastPos).sqrMagnitude > 1e-8f
                             || Mathf.Abs(_cam.orthographicSize - lastSize) > 1e-5f;
                still = moved ? 0 : still + 1;
                lastPos = _cam.transform.position;
                lastSize = _cam.orthographicSize;
            }
            Assert.That(frames, Is.LessThan(MaxFrameOnFrames), $"the camera never settled over ({at.x}, {at.y}).");
            if (worldHeightMetres.HasValue)
                for (int i = 0; i < 6; i++) yield return null;   // whatever streams in behind the camera
        }

        /// <summary>What the camera has in frame: every drawing renderer by kind, and the walls' renderers, materials
        /// and the face textures their property blocks bind (the profiler's runtime size of each, once).</summary>
        void TakeTheCensus(List<CliffWallSurface> walls, HashSet<Renderer> wallRenderers, ViewResult into)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_cam);
            var kinds = new SortedDictionary<string, int>();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                into.renderersInFrame++;
                string k = r.GetType().Name;
                kinds[k] = kinds.TryGetValue(k, out int n) ? n + 1 : 1;
            }
            into.renderersInFrameByKind = string.Join(", ", kinds.Select(kv => $"{kv.Key} {kv.Value}"));

            var materials = new HashSet<Material>();
            var textures = new HashSet<Texture>();
            var mpb = new MaterialPropertyBlock();
            foreach (CliffWallSurface w in walls)
            {
                bool inFrame = false;
                foreach (Renderer r in w.GetComponentsInChildren<Renderer>(false))
                {
                    if (!wallRenderers.Contains(r) || !r.enabled || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                    inFrame = true;
                    into.wallRenderersInFrame++;
                    materials.Add(r.sharedMaterial);
                    if (!r.HasPropertyBlock()) continue;
                    r.GetPropertyBlock(mpb);
                    foreach (int id in new[] { IdUnlit, IdNormal, IdMask })
                    {
                        Texture tex = mpb.GetTexture(id);
                        if (tex != null) textures.Add(tex);
                    }
                }
                if (inFrame) into.wallsInFrame++;
            }
            foreach (Material m in materials)
                if (m != null)
                    foreach (int id in m.GetTexturePropertyNameIDs())
                    {
                        Texture tex = m.GetTexture(id);
                        if (tex != null) textures.Add(tex);
                    }
            into.wallMaterials = materials.Count;
            into.wallTextures = textures.Count;
            into.wallTextureBytes = textures.Sum(tex => Profiler.GetRuntimeMemorySizeLong(tex));
        }

        /// <summary>The game's camera as the player sees it, read back gamma-corrected (the project is linear).</summary>
        byte[] Capture(int width, int height)
        {
            _cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Color[] px = tex.GetPixels();
            var bytes = new byte[px.Length * 4];
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b); c.a = 1f;
                c = c.gamma;
                bytes[i * 4 + 0] = (byte)Mathf.RoundToInt(c.r * 255f);
                bytes[i * 4 + 1] = (byte)Mathf.RoundToInt(c.g * 255f);
                bytes[i * 4 + 2] = (byte)Mathf.RoundToInt(c.b * 255f);
                bytes[i * 4 + 3] = 255;
            }
            Object.DestroyImmediate(tex);
            return bytes;
        }

        static string SavePlate(string name, byte[] rgba)
        {
            var tex = new Texture2D(PlateWidthPx, PlateHeightPx, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgba);
            tex.Apply();
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }

        static double Spread(byte[] rgba)
        {
            double sum = 0, sq = 0;
            int n = 0;
            for (int i = 0; i < rgba.Length; i += 4 * 37)   // a sparse sample is plenty to tell one colour from a picture
            {
                double y = 0.2126 * rgba[i] + 0.7152 * rgba[i + 1] + 0.0722 * rgba[i + 2];
                sum += y;
                sq += y * y;
                n++;
            }
            double mean = sum / n;
            return System.Math.Sqrt(System.Math.Max(0.0, sq / n - mean * mean));
        }

        /// <summary>A one-pixel readback of the target: the CPU waits here until the GPU has drawn it.</summary>
        void Sync()
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            _probe.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
            RenderTexture.active = prev;
        }

        void CountCamDraws(ScriptableRenderContext context, Camera camera)
        {
            if (camera == _cam) _camDraws++;
        }

        static double Percentile(List<double> samples, double q)
        {
            var sorted = new List<double>(samples);
            sorted.Sort();
            int rank = Mathf.Clamp((int)System.Math.Ceiling(q * sorted.Count) - 1, 0, sorted.Count - 1);
            return sorted[rank];
        }

        static string Sha256Of(string path)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(File.ReadAllBytes(Path.GetFullPath(path)));
                var sb = new System.Text.StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        static string Mb(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("0.0");
    }
}
#endif
