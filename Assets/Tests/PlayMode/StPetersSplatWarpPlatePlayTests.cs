using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE SPLAT WARP'S PLATES, SETTLED</b> (terrain-fidelity 3b; seat rulings t11, t12, t16). The same
    /// class shoots the BEFORE run (this harness alone) and the AFTER run (the warp on top of it), so the
    /// two runs differ by the warp and nothing else; the harness is its own commit and the warp does not
    /// touch it.
    ///
    /// <para><b>SETTLED</b> (t11): the clock is frozen at the pose's hour and tide (the game clock stopped,
    /// <see cref="Time.timeScale"/> 0, so URP's <c>_Time</c> stands still; both are read into the sidecar), a
    /// fixed settle of <see cref="SettleRenders"/> renders, then each plate is the per-pixel, per-channel
    /// MEDIAN of <see cref="MedianRenders"/> renders. A same-state CONTROL follows at every pose:
    /// <see cref="ControlGapFrames"/> frames later, the same settled capture again; the pixels the two
    /// differ in are the pose's noise mask, written beside the plate, and a before/after comparison
    /// excludes them and lists them by count.</para>
    ///
    /// <para><b>THE POSES</b>: the board's three on-foot frames (piece 2,
    /// <c>StPetersMainBeachWalkPlayTests.TheBoardsFrames</c>) and piece 3's four splat windows, the south
    /// sand patch among them (the crop the owner judges by, at 1 to 1). Noon.</para>
    ///
    /// <para><b>THE COST</b> (t12, under 0.3 ms): the warp cannot be switched inside one run (no uniform: the
    /// include's names are pinned), so each run prices what the warp lives in, the ground and the sea drawn
    /// against both hidden, interleaved, at the board's plate pose; the warp's cost is the AFTER run's
    /// difference less the BEFORE run's. A frame time read across launches drifts more than the bar; a
    /// difference inside each launch does not carry that drift.</para>
    ///
    /// <para>⚠ No GPU on CI: every case skips there before it loads anything, and the teardown is inert
    /// unless a region was loaded. Plates land in <c>Application.temporaryCachePath/SplatWarpPlates/</c>,
    /// which every worktree shares, so a run reads its own plates by a floor file, never by name. Each case
    /// asserts the run's own save before it shoots.</para>
    /// </summary>
    public class StPetersSplatWarpPlatePlayTests
    {
        const string PlateDir = "SplatWarpPlates";
        const string StPeters = "StPeters";
        const string CleanupSceneName = "SplatWarpPlatesCleanup";
        const string TerrainShader = "HiddenHarbours/TerrainSplat";
        const string WaterShader = "HiddenHarbours/Water";

        const int PlateHeightPx = CameraFollow.DesignScreenHeightPx;   // 1080: the PPC zoom depends on it
        const int PlateWidthPx = 1920;
        const float LeadEaseOutSeconds = 6f;
        const float OnTheCentreMetres = 0.001f;
        const float NoonHours = 12f;

        // ---- the settled capture (t11) ----------------------------------------------------------------------
        const int SettleRenders = 4, MedianRenders = 3, ControlGapFrames = 30;

        // ---- the cost (t12) ---------------------------------------------------------------------------------
        const int CostRounds = 5, CostSettleFrames = 30, CostWindowFrames = 240, GpuDraws = 10, GpuWarmSamples = 3, GpuSamples = 15;

        /// <summary>Piece 2's board frames, then piece 3's splat windows (the headless run-count's windows).</summary>
        static readonly (string Name, Vector2 Centre)[] Poses =
        {
            ("board-1-the-path-and-the-steps", new Vector2(10.5f, -60f)),
            ("board-2-the-sand-to-the-water", new Vector2(10f, -72f)),
            ("board-3-the-bay-east", new Vector2(26f, -70f)),
            ("window-south-sand-patch", new Vector2(77.5f, -87.5f)),
            ("window-south-sand-west", new Vector2(-2f, -87.5f)),
            ("window-ginnys-path", new Vector2(65f, 27.5f)),
            ("window-shore-path-east", new Vector2(45f, 59f)),
        };

        readonly List<Object> _spawned = new List<Object>();
        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        readonly List<Renderer> _hidden = new List<Renderer>();
        DateTime _runStartedUtc;
        bool _loadedAny, _introOnEntry;
        Vector2 _askedCentre;
        string _healed = "none", _pinned = "(not pinned)", _saveLine = "(not read)", _eased = "(not framed)";

        CameraFollow _follow;
        Camera _cam;
        bool _followCaptured;
        Transform _followTargetOnEntry;
        float _followSmoothOnEntry;
        GameObject _anchor;
        RenderTexture _rt;
        int _w, _h, _askedGridPx;
        float _askedHeight;

        [OneTimeSetUp]
        public void MarkTheRun() => _runStartedUtc = DateTime.UtcNow;

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
            Time.timeScale = 1f;   // a STATIC: left at 0 it stops every test that follows
            foreach (Renderer r in _hidden) if (r != null) r.enabled = true;
            _hidden.Clear();

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
            _pinned = "(not pinned)"; _saveLine = "(not read)"; _eased = "(not framed)"; _healed = "none";

            if (!_loadedAny) yield break;   // a case that skipped (every case on CI) loaded nothing

            if (GameServices.Clock != null) GameServices.Clock.TimeScale = 1f;
            GameServices.OpeningCinematicRunning = _introOnEntry;
            GameServices.PendingArrivalKey = null;

            Scene clean = SceneManager.GetSceneByName(CleanupSceneName);
            if (!clean.IsValid() || !clean.isLoaded) clean = SceneManager.CreateScene(CleanupSceneName);
            if (clean.IsValid()) SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == StPeters)
                    yield return SceneManager.UnloadSceneAsync(s);
            }

            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go)) Object.DestroyImmediate(go);
            GameServices.Reset();
            _loadedAny = false;
            yield return null;
        }

        // =============================================================================================
        //  The cases
        // =============================================================================================

        /// <summary>Every pose at noon: the settled plate, then its same-state control and noise mask.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator TheSplatEdges_PlatedSettledAtNoon()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            AssertTheSaveIsTheRunsOwn();
            yield return PinTheHour(NoonHours);
            var log = new StringBuilder();
            foreach (var f in Poses)
            {
                yield return FrameOn(f.Centre);   // ends with the world stopped (Time.timeScale 0)
                double clockA = GameServices.Clock.TotalSeconds;
                Vector4 timeA = Shader.GetGlobalVector("_Time");
                Color32[] plate = ReadMedian();
                for (int i = 0; i < ControlGapFrames; i++) yield return null;
                double clockB = GameServices.Clock.TotalSeconds;
                Vector4 timeB = Shader.GetGlobalVector("_Time");
                Color32[] control = ReadMedian();

                var noise = new Color32[plate.Length];
                int noisy = 0;
                for (int i = 0; i < plate.Length; i++)
                {
                    bool differs = plate[i].r != control[i].r || plate[i].g != control[i].g || plate[i].b != control[i].b;
                    if (differs) noisy++;
                    byte v = differs ? (byte)255 : (byte)0;
                    noise[i] = new Color32(v, v, v, 255);
                }

                string name = "splat-warp-" + f.Name;
                SavePlate(name + ".png", plate, _w);
                SavePlate(name + "-control.png", control, _w);
                SavePlate(name + "-noise.png", noise, _w);
                string settled = $"settled: {SettleRenders} settle renders, each capture the per-channel median of {MedianRenders} renders; " +
                                 $"the control {ControlGapFrames} frames later; noise {noisy} px of {plate.Length}; " +
                                 $"clock {clockA:0.000} s -> {clockB:0.000} s; _Time.y {timeA.y:0.0000} -> {timeB.y:0.0000}; " +
                                 $"Time.timeScale {Time.timeScale}, clock TimeScale {GameServices.Clock.TimeScale}";
                SaveCaption(name + ".txt", Caption(name, "the splat warp's settled plate: " + f.Name, _askedCentre, settled));
                log.AppendLine($"{f.Name}: noise {noisy} px; clock moved {clockB - clockA:0.000} s; _Time.y moved {timeB.y - timeA.y:0.0000}");
            }
            SaveCaption("splat-warp-plates-log.txt", log.ToString());
            Debug.Log($"[{PlateDir}] settled plates:\n{log}");
        }

        /// <summary>What the warp lives in, priced at the board's plate pose: the ground and the sea drawn against
        /// both hidden, three interleaved rounds of 240 frames each way, vsync off. The bar (t12, under 0.3 ms) is
        /// the AFTER run's difference less the BEFORE run's; this case writes the figure and asserts it is a figure.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator TheGroundAndTheSea_CostAtThePlatePose()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            AssertTheSaveIsTheRunsOwn();
            yield return PinTheHour(NoonHours);
            (string Name, Vector2 Centre) pose = Poses[1];
            yield return FrameOn(pose.Centre);

            // The ground's quad is made with HideFlags.DontSave (TerrainSplatSurface), which FindObjectsByType
            // never returns: walk the region scene's roots instead, where GetComponentsInChildren sees it.
            List<Renderer> drawn = SceneManager.GetSceneByName(StPeters).GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Renderer>())
                .Where(r => r.enabled && r.sharedMaterial != null &&
                            r.sharedMaterial.shader != null &&
                            (r.sharedMaterial.shader.name == TerrainShader || r.sharedMaterial.shader.name == WaterShader))
                .ToList();
            int ground = drawn.Count(r => r.sharedMaterial.shader.name == TerrainShader);
            int sea = drawn.Count - ground;
            Assert.Greater(ground, 0, "St Peters draws no ground with " + TerrainShader + ": there is nothing to price.");
            Assert.Greater(sea, 0, "St Peters draws no sea with " + WaterShader + ": there is no floor to price.");
            _hidden.AddRange(drawn);

            int vsync = QualitySettings.vSyncCount, target = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.timeScale = 1f;
            var shown = new List<float>();
            var hidden = new List<float>();
            var shownGpu = new List<float>();
            var hiddenGpu = new List<float>();
            // The CPU never waits on the GPU by itself, so a frame time alone misses the draw: each frame reads one
            // pixel back (the CPU waits for the last frame's draw), and the GPU's own frame time is kept beside it
            // wherever FrameTimingManager reports one.
            var probe = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            var timings = new FrameTiming[1];
            try
            {
                for (int round = 0; round < CostRounds; round++)
                {
                    foreach (bool on in new[] { true, false })
                    {
                        foreach (Renderer r in drawn) r.enabled = on;
                        for (int i = 0; i < CostSettleFrames; i++)
                        {
                            yield return null;
                            WaitForTheGpu(probe);
                        }
                        var window = new List<float>(CostWindowFrames);
                        var gpuWindow = new List<float>(CostWindowFrames);
                        for (int i = 0; i < CostWindowFrames; i++)
                        {
                            yield return null;
                            WaitForTheGpu(probe);
                            window.Add(Time.unscaledDeltaTime * 1000f);
                            FrameTimingManager.CaptureFrameTimings();
                            if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0.0)
                                gpuWindow.Add((float)timings[0].gpuFrameTime);
                        }
                        (on ? shown : hidden).Add(Median(window));
                        if (gpuWindow.Count > 0) (on ? shownGpu : hiddenGpu).Add(Median(gpuWindow));
                    }
                }
            }
            finally
            {
                foreach (Renderer r in drawn) if (r != null) r.enabled = true;
                _hidden.Clear();
                QualitySettings.vSyncCount = vsync;
                Application.targetFrameRate = target;
                Object.Destroy(probe);
            }

            // The GPU's own price, with the rest of the frame left out: the visible ground and sea drawn GpuDraws times
            // back to back by one command buffer into the plate texture (every pass, depth cleared between draws),
            // timed between two GPU waits, less an empty buffer timed the same way. The GPU stays loaded the whole
            // time, so its clock does not wander the way it does between waited frames.
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_cam);
            List<Renderer> seen = drawn.Where(r => r != null && GeometryUtility.TestPlanesAABB(planes, r.bounds)).ToList();
            int seenGround = seen.Count(r => r.sharedMaterial.shader.name == TerrainShader);
            var price = new List<float>();
            var waitProbe = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            var full = new CommandBuffer { name = "splat-warp-price" };
            var empty = new CommandBuffer { name = "splat-warp-price-empty" };
            try
            {
                foreach (CommandBuffer cb in new[] { full, empty })
                {
                    cb.SetRenderTarget(_rt);
                    cb.SetViewProjectionMatrices(_cam.worldToCameraMatrix, _cam.projectionMatrix);
                }
                for (int k = 0; k < GpuDraws; k++)
                {
                    full.ClearRenderTarget(true, false, Color.clear);
                    empty.ClearRenderTarget(true, false, Color.clear);
                    foreach (Renderer r in seen)
                    {
                        Material[] mats = r.sharedMaterials;
                        for (int m = 0; m < mats.Length; m++)
                            if (mats[m] != null) full.DrawRenderer(r, mats[m], m, -1);
                    }
                }
                for (int s = 0; s < GpuWarmSamples + GpuSamples; s++)
                {
                    float fullMs = TimeOnTheGpu(full, waitProbe), emptyMs = TimeOnTheGpu(empty, waitProbe);
                    if (s >= GpuWarmSamples) price.Add((fullMs - emptyMs) / GpuDraws);
                }
            }
            finally
            {
                full.Release();
                empty.Release();
                Object.Destroy(waitProbe);
            }
            var sortedPrice = price.OrderBy(v => v).ToList();
            string gpuPrice = $"GPU price of the visible ground and sea ({seenGround} ground and {seen.Count - seenGround} sea " +
                              $"renderers in view, every pass, {GpuDraws} draws per sample): median {Median(price):0.####} ms per draw, " +
                              $"quartiles {sortedPrice[sortedPrice.Count / 4]:0.####} to {sortedPrice[(3 * sortedPrice.Count) / 4]:0.####} ms, " +
                              $"{GpuSamples} samples [{string.Join(", ", price.Select(v => v.ToString("0.####")))}]";

            float shownMs = shown.Average(), hiddenMs = hidden.Average(), delta = shownMs - hiddenMs;
            string gpu = shownGpu.Count == CostRounds && hiddenGpu.Count == CostRounds
                ? $"GPU frame time drawn {shownGpu.Average():0.###} ms [{string.Join(", ", shownGpu.Select(v => v.ToString("0.###")))}], " +
                  $"hidden {hiddenGpu.Average():0.###} ms [{string.Join(", ", hiddenGpu.Select(v => v.ToString("0.###")))}]; " +
                  $"GPU difference {shownGpu.Average() - hiddenGpu.Average():+0.###;-0.###} ms"
                : $"GPU frame time not reported by FrameTimingManager ({shownGpu.Count} drawn and {hiddenGpu.Count} hidden windows of {CostRounds})";
            string figures = $"the ground and the sea at the plate pose {pose.Name} ({pose.Centre}), noon: " +
                             $"{ground} ground and {sea} sea renderers; frame median (each frame waits on the GPU) drawn {shownMs:0.###} ms " +
                             $"[{string.Join(", ", shown.Select(v => v.ToString("0.###")))}], hidden {hiddenMs:0.###} ms " +
                             $"[{string.Join(", ", hidden.Select(v => v.ToString("0.###")))}]; difference {delta:+0.###;-0.###} ms; " +
                             $"{gpu}; {CostRounds} interleaved rounds of {CostWindowFrames} frames, vsync off; {gpuPrice}; render texture {_w}x{_h}";
            SaveCaption("splat-warp-cost-ground-and-sea.txt", figures);
            Debug.Log($"[{PlateDir}] {figures}");
            Assert.IsTrue(shown.Concat(hidden).Concat(price).All(v => v > 0f && !float.IsInfinity(v) && !float.IsNaN(v)), figures);
        }

        // =============================================================================================
        //  The settled capture
        // =============================================================================================

        /// <summary>The fixed settle, then the per-channel median of three renders, read back as gamma bytes (the
        /// render texture is linear half float). Rows bottom-up, as Unity reads them.</summary>
        Color32[] ReadMedian()
        {
            RenderTexture prevActive = RenderTexture.active;
            var tex = new Texture2D(_w, _h, TextureFormat.RGBAFloat, false, true);
            var frames = new Color32[MedianRenders][];
            try
            {
                _cam.targetTexture = _rt;
                for (int i = 0; i < SettleRenders; i++) _cam.Render();
                for (int k = 0; k < MedianRenders; k++)
                {
                    _cam.Render();
                    RenderTexture.active = _rt;
                    tex.ReadPixels(new Rect(0, 0, _w, _h), 0, 0);
                    tex.Apply(false, false);
                    Color[] px = tex.GetPixels();
                    var bytes = new Color32[px.Length];
                    for (int i = 0; i < px.Length; i++)
                    {
                        Color c = new Color(Mathf.Clamp01(px[i].r), Mathf.Clamp01(px[i].g), Mathf.Clamp01(px[i].b), 1f).gamma;
                        bytes[i] = new Color32((byte)Mathf.RoundToInt(c.r * 255f), (byte)Mathf.RoundToInt(c.g * 255f),
                                               (byte)Mathf.RoundToInt(c.b * 255f), 255);
                    }
                    frames[k] = bytes;
                }
            }
            finally
            {
                RenderTexture.active = prevActive;
                Object.DestroyImmediate(tex);
            }
            var median = new Color32[frames[0].Length];
            for (int i = 0; i < median.Length; i++)
                median[i] = new Color32(Middle(frames[0][i].r, frames[1][i].r, frames[2][i].r),
                                        Middle(frames[0][i].g, frames[1][i].g, frames[2][i].g),
                                        Middle(frames[0][i].b, frames[1][i].b, frames[2][i].b), 255);
            return median;
        }

        static byte Middle(byte a, byte b, byte c) => (byte)(a + b + c - Math.Min(a, Math.Min(b, c)) - Math.Max(a, Math.Max(b, c)));

        /// <summary>One pixel read back from the plate texture: the CPU waits until the last frame's draw is done.</summary>
        void WaitForTheGpu(Texture2D probe)
        {
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = _rt;
            probe.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
            RenderTexture.active = prevActive;
        }

        /// <summary>A command buffer's run, from a GPU wait before it to a GPU wait after it, in ms.</summary>
        float TimeOnTheGpu(CommandBuffer cb, Texture2D probe)
        {
            WaitForTheGpu(probe);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Graphics.ExecuteCommandBuffer(cb);
            WaitForTheGpu(probe);
            return (float)watch.Elapsed.TotalMilliseconds;
        }

        static float Median(List<float> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            return n == 0 ? float.NaN : n % 2 == 1 ? sorted[n / 2] : 0.5f * (sorted[n / 2 - 1] + sorted[n / 2]);
        }

        // =============================================================================================
        //  The region, the hour, the save and the frame (StPetersMainBeachWalkPlayTests' own steps)
        // =============================================================================================

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED: no graphics device (Null Device), so nothing rendered. " +
                              "Expected on CI; the splat warp's plates need a GPU.");
        }

        IEnumerator LoadStPeters()
        {
            if (!_loadedAny) _introOnEntry = GameServices.OpeningCinematicRunning;
            GameConfig configBefore = GameServices.Config;
            IGameClock clockBefore = GameServices.Clock;
            IEnvironmentService envBefore = GameServices.Environment;
            IWallet walletBefore = GameServices.Wallet;

            LogAssert.ignoreFailingMessages = true;   // the region logs decor complaints of its own
            _loadedAny = true;
            yield return SceneManager.LoadSceneAsync(StPeters, LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;

            var healed = new List<string>();
            if (GameServices.Config == null && configBefore != null) { GameServices.Config = configBefore; healed.Add("Config"); }
            if (GameServices.Clock == null && clockBefore != null) { GameServices.Clock = clockBefore; healed.Add("Clock"); }
            if (GameServices.Environment == null && envBefore != null) { GameServices.Environment = envBefore; healed.Add("Environment"); }
            if (GameServices.Wallet == null && walletBefore != null) { GameServices.Wallet = walletBefore; healed.Add("Wallet"); }
            if (healed.Count > 0) _healed = $"{StPeters}: re-published {string.Join(", ", healed)}";

            int frames = 0;
            while (frames < 240 && FindPersistentFollow() == null) { yield return null; frames++; }
            Assert.IsNotNull(FindPersistentFollow(), $"{StPeters}: no CameraFollow on the persistent core's camera after 240 frames.");
            Assert.IsNotNull(GameServices.Environment, "St Peters registered no environment, so there is no tide to pin.");
        }

        /// <summary>The run's own save is the one open; the shared save is not.</summary>
        void AssertTheSaveIsTheRunsOwn()
        {
            string active = SaveStore.ActivePath;
            Assert.That(active, Is.Not.Null.And.Not.Empty, $"no {SaveStore.TestRunSaveVariable} redirect: this case never opens the shared save.");
            Assert.That(Path.GetFullPath(active), Is.Not.EqualTo(Path.GetFullPath(SaveStore.DefaultPath)).IgnoreCase,
                "the active save IS the shared save; the case stops before it shoots.");
            SaveService save = Object.FindAnyObjectByType<SaveService>();
            if (save != null)
                Assert.That(Path.GetFullPath(save.SavePath), Is.EqualTo(Path.GetFullPath(active)).IgnoreCase,
                    "the running save service holds another file than the run's redirect.");
            _saveLine = $"save: the run's own ({active}); the shared save is not open";
        }

        IEnumerator PinTheHour(float hours)
        {
            IGameClock clock = GameServices.Clock;
            GameConfig config = GameServices.Config;
            Assert.IsNotNull(clock, "the region registered no clock, so the hour cannot be pinned.");
            Assert.IsNotNull(config, "the region registered no GameConfig, so a day has no length.");
            double t = (1.0 + hours / 24.0) * config.SecondsPerDay;
            Time.timeScale = 1f;
            clock.SeekTo(t);
            clock.TimeScale = 0f;
            Assert.LessOrEqual(Math.Abs(clock.TotalSeconds - t), 1.0, $"the clock did not land on {t:0.0} s.");
            float waited = 0f;
            while (waited < 1f) { yield return null; waited += Time.deltaTime; }
            float water = GameServices.Environment.WaterLevelAt(clock.TotalSeconds);
            _pinned = $"day {clock.DayIndex} {clock.HourOfDay:0.000} h (TotalSeconds {clock.TotalSeconds:0.0}); water {water:+0.000;-0.000} m; " +
                      $"_SunElevation {Shader.GetGlobalFloat("_SunElevation"):0.000}";
        }

        /// <summary>The play camera's own follow, on a still anchor at the on-foot stop, the 1080 px target attached
        /// before the follow is asked its height; the asked centre snapped to the camera's own pixel grid.</summary>
        IEnumerator FrameOn(Vector2 at)
        {
            if (_follow == null)
            {
                _follow = FindPersistentFollow();
                Assert.IsNotNull(_follow, "no CameraFollow on the persistent core's camera.");
                _cam = _follow.GetComponent<Camera>();
                Assert.IsNotNull(_cam, "the play camera's CameraFollow has no Camera.");
            }
            if (!_followCaptured)
            {
                _followTargetOnEntry = _follow.Target;
                _followSmoothOnEntry = _follow.Smooth;
                _followCaptured = true;
            }

            Time.timeScale = 0f;
            if (_anchor == null) { _anchor = new GameObject("SplatWarpPlateAnchor"); _spawned.Add(_anchor); }
            _anchor.transform.position = new Vector3(at.x, at.y, 0f);
            _follow.Target = _anchor.transform;
            _follow.Smooth = 1000f;
            _cam.transform.position = new Vector3(at.x, at.y, _cam.transform.position.z);
            if (_rt == null)
            {
                _w = PlateWidthPx;
                _h = PlateHeightPx;
                _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf);
                _rt.Create();
            }
            _cam.targetTexture = _rt;
            _askedGridPx = _cam.targetTexture.height;
            _askedHeight = _follow.WorldHeightFor(CameraFraming.OnFoot);
            _follow.SetFraming(_askedHeight, 0f);
            for (int i = 0; i < 2; i++) yield return null;

            float grid = _follow.WorldUnitsPerRenderedPixel;
            if (grid > 0f && GameServices.PixelGridSnap) at = PixelGrid.Snap(at, grid);
            _askedCentre = at;
            _anchor.transform.position = new Vector3(at.x, at.y, 0f);
            _cam.transform.position = new Vector3(at.x, at.y, _cam.transform.position.z);

            Time.timeScale = 1f;
            for (int i = 0; i < 8; i++) yield return null;
            yield return SettleCamera(_cam);
            Vector2 stillAt = _cam.transform.position;
            float start = Time.time;
            int frames = 0;
            while (Vector2.Distance(_cam.transform.position, at) > OnTheCentreMetres && Time.time - start < LeadEaseOutSeconds)
            {
                yield return null;
                frames++;
            }
            _eased = $"still at ({stillAt.x:0.####}, {stillAt.y:0.####}), then {frames} frames / {Time.time - start:0.00} s for the look-ahead to ease out";
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;

            Vector3 p = _cam.transform.position;
            float worldHeight = _cam.orthographicSize * 2f;
            Assert.LessOrEqual(Vector2.Distance(new Vector2(p.x, p.y), at), OnTheCentreMetres,
                $"the camera sits at ({p.x:0.####}, {p.y:0.####}), not on ({at.x:0.###}, {at.y:0.###}), after {_eased}.");
            Assert.That(worldHeight, Is.EqualTo(_askedHeight).Within(0.5f),
                $"the camera frames {worldHeight:0.###} m tall, not the on-foot stop its follow was asked for ({_askedHeight:0.###} m).");
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
                bool moved = (cam.transform.position - lastPos).sqrMagnitude > 1e-8f || Mathf.Abs(cam.orthographicSize - lastSize) > 1e-5f;
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

        string Caption(string plate, string subject, Vector2 centre, string extra)
        {
            Vector3 p = _cam.transform.position;
            float worldHeight = _cam.orthographicSize * 2f;
            var sb = new StringBuilder();
            sb.AppendLine(plate);
            sb.AppendLine(subject);
            sb.AppendLine($"run: started {_runStartedUtc:yyyy-MM-dd HH:mm:ss}Z, Unity {Application.unityVersion}, " +
                          $"{SystemInfo.graphicsDeviceType} on {SystemInfo.graphicsDeviceName}");
            sb.AppendLine($"scene: {SceneManager.GetActiveScene().name}; region '{GameServices.CurrentRegionId}'; services healed: {_healed}");
            sb.AppendLine(_saveLine);
            sb.AppendLine($"asked centre ({centre.x:0.#####}, {centre.y:0.#####}); camera at ({p.x:0.#####}, {p.y:0.#####}, {p.z:0.###}); {_eased}");
            sb.AppendLine($"ortho {_cam.orthographicSize:0.######} -> {worldHeight * _cam.aspect:0.######} x {worldHeight:0.######} m, render texture {_w}x{_h} px");
            sb.AppendLine($"framing: asked {_askedHeight:0.###} m (WorldHeightFor OnFoot on a {_askedGridPx} px grid), framed {worldHeight:0.###} m");
            sb.AppendLine($"hour, sun and tide: {_pinned}");
            if (extra != null) sb.AppendLine(extra);
            return sb.ToString();
        }

        static string PlatePath()
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            return dir;
        }

        static void SavePlate(string name, Color32[] bottomUp, int width)
        {
            var tex = new Texture2D(width, bottomUp.Length / width, TextureFormat.RGBA32, false);
            tex.SetPixels32(bottomUp);
            tex.Apply();
            string path = Path.Combine(PlatePath(), name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[{PlateDir}] plate written: {path}");
        }

        static void SaveCaption(string name, string text) =>
            File.WriteAllText(Path.Combine(PlatePath(), name), text, new UTF8Encoding(false));

        static List<GameObject> PersistentRoots()
        {
            var probe = new GameObject("__ddolProbe");
            Object.DontDestroyOnLoad(probe);
            Scene s = probe.scene;
            Object.DestroyImmediate(probe);
            return s.IsValid() ? s.GetRootGameObjects().ToList() : new List<GameObject>();
        }
    }
}
