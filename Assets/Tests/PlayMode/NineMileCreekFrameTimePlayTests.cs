#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <para><b>NMC H2, PR 2 Phase B: the creek's frame time and memory at 1080p.</b> A measurement, not a gate:
    /// the case asserts only that it measured. It loads the creek the way the loop does, enters the world through
    /// Continue (which writes nothing), pins the daylight water nearest the mean from day 1 (the same moment on
    /// every run), and points the game's own camera, at its own on-foot framing, into a 1920x1080 target over
    /// three views: the harbour, the plaza and the bar's landfall.</para>
    ///
    /// <para>Per view, after a warm-up: <b>frames</b> — the wall time between one frame's resume and the next,
    /// each frame ending in a one-pixel readback of the target so the GPU's work sits inside the frame (no overlap
    /// credit: a conservative number; when the batch loop does not draw the camera itself, the case draws it once
    /// per frame); and <b>renders</b> — the camera's render alone, synced the same way, back to back. Memory is
    /// the profiler's totals after an unload of unused assets, before the views and after them. The machine, the
    /// scene file's SHA-256 and every number go to a JSON under <c>temporaryCachePath/nmc-h2-b2/</c>, so the same
    /// case run on PR 1's scene and on PR 2's gives the base-against-head pair.</para>
    /// </summary>
    public class NineMileCreekFrameTimePlayTests
    {
        const string SceneName = "NineMileCreek";
        const string ScenePath = "Assets/_Project/Scenes/NineMileCreek.unity";
        const string OutDir = "nmc-h2-b2";
        const int WidthPx = 1920, HeightPx = 1080;
        const int WarmFrames = 120, LoopRenderProbeFrames = 60, FrameSamples = 600, RenderSamples = 300;
        const int RenderSamplesPerYield = 30;
        const double DaylightFromHour = 9.0, DaylightToHour = 16.0, MonthDays = 30.0, StepsPerDay = 240.0;
        const int MaxSettleFrames = 600, MaxFrameOnFrames = 400;

        static readonly string[] ViewNames = { "harbour", "plaza", "bar-landfall" };
        static readonly Vector2[] ViewAt = { new Vector2(105f, 80f), new Vector2(-235f, 141f), new Vector2(45f, -150f) };

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
        }

        [System.Serializable]
        public class Report
        {
            public string utc, scenePath, sceneSha256, unity, cpu, gpu, gpuApi, os;
            public int cpuCount, cpuMHz, gpuMemoryMB, systemMemoryMB, width, height, worldSeed;
            public float water;
            public double hour;
            public int renderers, renderersDrawing, sceneTransforms;
            public string rendererKinds;
            public long allocatedBytes, reservedBytes, monoUsedBytes, monoHeapBytes, graphicsDriverBytes;
            public long allocatedBytesAfter, reservedBytesAfter, monoUsedBytesAfter, monoHeapBytesAfter, graphicsDriverBytesAfter;
            public List<ViewResult> views = new List<ViewResult>();
        }

        GameObject _core, _anchor;
        Camera _cam;
        RenderTexture _rt;
        Texture2D _probe;
        bool _qualityTouched, _counting;
        int _vSyncCount, _targetFrameRate, _camDraws;
        float _engineTimeScale = 1f;

        [UnityTest, Timeout(1200000)]
        public IEnumerator TheCreek_At1080p_FrameTimeAndMemory()
        {
            // ⚠️ FIRST statement, before any yield (see WharfNightStage.RequireAGraphicsDevice).
            WharfNightStage.RequireAGraphicsDevice();

            for (var e = LoadTheCreek(); e.MoveNext();) yield return e.Current;
            double t = FindTheMeanDaylightWater(out float level, out double hour);
            for (var e = SeekAndSettle(t); e.MoveNext();) yield return e.Current;

            _vSyncCount = QualitySettings.vSyncCount;
            _targetFrameRate = Application.targetFrameRate;
            _qualityTouched = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            _rt = new RenderTexture(WidthPx, HeightPx, 24, RenderTextureFormat.ARGBHalf);
            _rt.Create();
            _cam.targetTexture = _rt;
            _cam.aspect = (float)WidthPx / HeightPx;
            _probe = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);

            yield return Resources.UnloadUnusedAssets();
            System.GC.Collect();
            yield return null;

            var report = new Report
            {
                utc = System.DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"),
                scenePath = ScenePath,
                sceneSha256 = Sha256Of(ScenePath),
                unity = Application.unityVersion,
                cpu = SystemInfo.processorType,
                cpuCount = SystemInfo.processorCount,
                cpuMHz = SystemInfo.processorFrequency,
                gpu = SystemInfo.graphicsDeviceName,
                gpuApi = $"{SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceVersion})",
                gpuMemoryMB = SystemInfo.graphicsMemorySize,
                systemMemoryMB = SystemInfo.systemMemorySize,
                os = SystemInfo.operatingSystem,
                width = WidthPx,
                height = HeightPx,
                worldSeed = GameServices.Environment.WorldSeed,
                water = level,
                hour = hour,
                allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                reservedBytes = Profiler.GetTotalReservedMemoryLong(),
                monoUsedBytes = Profiler.GetMonoUsedSizeLong(),
                monoHeapBytes = Profiler.GetMonoHeapSizeLong(),
                graphicsDriverBytes = Profiler.GetAllocatedMemoryForGraphicsDriver(),
            };
            CountTheScene(report);

            var follow = Object.FindAnyObjectByType<CameraFollow>();
            for (int v = 0; v < ViewNames.Length; v++)
            {
                for (var e = FrameOn(ViewAt[v]); e.MoveNext();) yield return e.Current;
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
                    view = ViewNames[v],
                    x = ViewAt[v].x,
                    y = ViewAt[v].y,
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
                };
                report.views.Add(r);
                Debug.Log($"[{OutDir}] perf {r.view} ({r.x:0.#}, {r.y:0.#}) at {WidthPx}x{HeightPx}, world height " +
                          $"{r.worldHeightMetres:0.##} m, loop draws the camera {r.loopDrawsTheCamera}: frame p50 " +
                          $"{r.frameP50Ms:0.00} ms, p95 {r.frameP95Ms:0.00} ms, max {r.frameMaxMs:0.00} ms ({r.frames}); " +
                          $"render p50 {r.renderP50Ms:0.00} ms, p95 {r.renderP95Ms:0.00} ms, max {r.renderMaxMs:0.00} ms ({r.renders})");
            }
            if (follow != null) follow.Target = null;

            yield return Resources.UnloadUnusedAssets();
            System.GC.Collect();
            yield return null;
            report.allocatedBytesAfter = Profiler.GetTotalAllocatedMemoryLong();
            report.reservedBytesAfter = Profiler.GetTotalReservedMemoryLong();
            report.monoUsedBytesAfter = Profiler.GetMonoUsedSizeLong();
            report.monoHeapBytesAfter = Profiler.GetMonoHeapSizeLong();
            report.graphicsDriverBytesAfter = Profiler.GetAllocatedMemoryForGraphicsDriver();

            string dir = Path.Combine(Application.temporaryCachePath, OutDir);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"perf-{report.sceneSha256.Substring(0, 8)}-{report.utc}.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log($"[{OutDir}] perf machine: {report.cpu} x{report.cpuCount} @ {report.cpuMHz} MHz, {report.gpu} " +
                      $"{report.gpuApi} {report.gpuMemoryMB} MB, RAM {report.systemMemoryMB} MB, {report.os}, Unity {report.unity}");
            Debug.Log($"[{OutDir}] perf scene {report.sceneSha256}: {report.sceneTransforms} transforms, {report.renderers} " +
                      $"renderers ({report.renderersDrawing} drawing: {report.rendererKinds}); memory allocated " +
                      $"{Mb(report.allocatedBytes)} -> {Mb(report.allocatedBytesAfter)} MB, reserved {Mb(report.reservedBytes)} -> " +
                      $"{Mb(report.reservedBytesAfter)} MB, mono {Mb(report.monoUsedBytes)} -> {Mb(report.monoUsedBytesAfter)} MB, " +
                      $"graphics driver {Mb(report.graphicsDriverBytes)} -> {Mb(report.graphicsDriverBytesAfter)} MB");
            Debug.Log($"[{OutDir}] perf written: {path}");

            Assert.That(report.views.Count, Is.EqualTo(ViewNames.Length), "a view was not measured.");
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

            const string cleanupName = "NineMileCreekFrameTimeCleanup";
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

        /// <summary>The daylight moment of the month (from day 1) whose water is nearest the mean.</summary>
        static double FindTheMeanDaylightWater(out float level, out double hour)
        {
            IEnvironmentService env = GameServices.Environment;
            double day = GameServices.Config.SecondsPerDay;
            double best = day;
            float bestKey = float.MaxValue;
            level = 0f;
            for (int i = 0; i <= MonthDays * StepsPerDay; i++)
            {
                double t = day + i * day / StepsPerDay;
                double h = (t / day % 1.0) * 24.0;
                if (h < DaylightFromHour || h > DaylightToHour) continue;
                float w = env.WaterLevelAt(t);
                if (Mathf.Abs(w) < bestKey) { bestKey = Mathf.Abs(w); best = t; level = w; }
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

        /// <summary>The game's own framing (on foot) over a view: only the follow's target moves.</summary>
        IEnumerator FrameOn(Vector2 at)
        {
            var follow = Object.FindAnyObjectByType<CameraFollow>();
            if (_anchor == null) _anchor = new GameObject("NmcFrameTimeAnchor (test)");
            _anchor.transform.position = new Vector3(at.x, at.y, 0f);
            follow.Target = _anchor.transform;
            follow.Smooth = 1000f;   // any residue arrives in a frame, not over a second of easing
            _cam.transform.position = new Vector3(at.x, at.y, _cam.transform.position.z);

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

        static void CountTheScene(Report report)
        {
            var kinds = new SortedDictionary<string, int>();
            Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            int drawing = 0;
            foreach (Renderer r in all)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                drawing++;
                string k = r.GetType().Name;
                kinds[k] = kinds.TryGetValue(k, out int n) ? n + 1 : 1;
            }
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> kv in kinds) parts.Add($"{kv.Key} {kv.Value}");
            report.renderers = all.Length;
            report.renderersDrawing = drawing;
            report.rendererKinds = string.Join(", ", parts);

            int transforms = 0;
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid())
                foreach (GameObject root in scene.GetRootGameObjects())
                    transforms += root.GetComponentsInChildren<Transform>(true).Length;
            report.sceneTransforms = transforms;
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
