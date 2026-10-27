using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;
#if UNITY_EDITOR
using HiddenHarbours.App.Editor;
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    // ADR 0040 B2: the bounded curling sheet on the GPU, at Nine Mile Creek's one picked stretch
    // (breaker_site.nine_mile_creek_low_south) in the pilot's sea: a gale of 12.95 m/s toward 50 deg on one
    // wave clock and noon lighting, at low, mean and high water, built as the lee sea plates build the creek.
    //
    // Every compare is two shots inside one rendered frame (no yield between them): the curl's master at 0
    // and at 1, or the dory there and gone. The silhouette sequences walk one period at 60 Hz at each tide:
    // no pixel may change outside the CPU pose's own bounds (so a frame with no live pose changes none), a
    // readable pose must draw, and low water must clear the readable bar. The dory test berths a hull on
    // the band the curl draws at its peak: in their overlap every pixel is the hull's or the curl's, and the
    // hull wins somewhere. The zero dial renders exactly as no curl at all, and leaves no trace after a
    // live frame.
    // The worst view is the curl filling a 1080p frame at twice the bar's scale: its CPU frame, its GPU cost
    // and its allocations, against the budget. Plates and CSVs go to temporaryCachePath/curl-b2/.
    public class CurlingBreakerSilhouettePlayTests
    {
        const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const string SitePath = "Assets/_Project/Data/Regions/NineMileCreek/BreakerSites/NineMileCreekLowSouth.asset";
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursCurlingBreaker.shader";
        const string WaterPath = "Assets/_Project/Art/Materials/Water.mat";
        const string SeaTilePath = "Assets/_Project/Art/Tilesets/Water/SeaTile.png";
        const string DoryPath = "Assets/_Project/Data/Boats/Visuals/DoryIso.asset";
        const string OutFolder = "curl-b2";
        const int Seed = 4242, PhaseSeed = 12345, Pixels = 768, Frames = 264;
        const int MaxSettleFrames = 60, MaxCompileFrames = 600;
        // The events read covers this many windows of Frames either side of the plates' own.
        const int EventWindows = 5;
        const int WorstWidth = 1920, WorstHeight = 1080, BudgetWarmUp = 30, BudgetSamples = 120, SamplesPerYield = 30;
        const float GaleSpeed = 12.95f, GaleTowardDeg = 50f, SlackPx = 2f;
        // The closest framing the plates take: twice the bar's 96 px/m, the curl filling a 1080p view.
        const float WorstPixelsPerMetre = 192f;
        // One bore per event: the event cresting 208 frames after 900 s is born at 0, under MinBore 0.1, and never
        // draws. The window opens one event earlier (263 frames, the period's 263.007 to the whole frame), so its
        // crest frame holds the bore born at 0.5012: 63 frames live, 23 readable, full throw 0.3718 m.
        const double Start = 900 - 263 / 60.0;
        const double CpuTickBudgetMs = 0.25, GpuDeltaBudgetMs = 0.5;
        static readonly string[] TideNames = { "low", "mean", "high" };
        static readonly float[] TideLevels = { -2.2f, 0f, 2.2f };

        static readonly MethodInfo PushUniforms = typeof(WaterSurface).GetMethod("PushUniforms",
            BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        static readonly MethodInfo SyncDisplaced = typeof(DisplacedWaterSurface).GetMethod("SyncUniforms",
            BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);
        static readonly FieldInfo DisplacedChunks = typeof(DisplacedWaterSurface).GetField("_chunkRenderers",
            BindingFlags.Instance | BindingFlags.NonPublic);

        readonly List<Object> _made = new List<Object>();
        readonly List<Camera> _cameras = new List<Camera>();
        IHullMeshPresentationService _hulls;
        float _lookStrength, _deposit, _drawnScale;
        bool _started;
        GameConfig _config;
        PlateClock _clock;
        PlateWind _wind;
        WaveFieldBridge _bridge;
        WaterSurface _surface;
        SpriteRenderer _sea;
        CurlingBreakerSiteDef _site;
        CurlingBreakerRegion _host;
        Camera _camera;
        RenderTexture _target;
        Texture2D _read, _probe;
        object _sink;

        sealed class PlateClock : IGameClock
        {
            public double TotalSeconds { get; set; } = Start;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 12;
            public float DayFraction => .5f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1;
        }

        sealed class PlateWind : IEnvironmentService, IWaveWindSource
        {
            public Vector2 Wind;
            public float Level;
            public int WorldSeed => Seed;
            public TideProfile ActiveTideProfile { get; set; }
            public float TideHeightAt(double t) => Level;
            public float WaterLevelAt(double t) => Level;
            public WaveWindSample WaveWindAt(double t) =>
                new WaveWindSample(Wind, WeatherModel.SeaState01(Wind.magnitude));
            public EnvironmentSample Sample() => new EnvironmentSample(Wind, Vector2.zero, Level,
                WeatherModel.SeaFromWind(Wind.magnitude), 1, WeatherModel.SeaState01(Wind.magnitude));
        }

        T Keep<T>(T item) where T : Object { _made.Add(item); return item; }
        GameObject Host(string name) => Keep(new GameObject(name));

        void Begin()
        {
            _started = true;
            _hulls = HullMeshPresentation.Service;
            _lookStrength = FoamInjectionRegistry.LookStrength;
            _deposit = FoamInjectionRegistry.SurfDepositStrength;
            _drawnScale = FoamInjectionRegistry.DrawnWaveScale;
            foreach (var c in Object.FindObjectsByType<Camera>())
                if (c.enabled) { _cameras.Add(c); c.enabled = false; }
            GameServices.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            if (!_started) return;
            if (_camera != null) _camera.targetTexture = null;
            if (_target != null) _target.Release();
            for (int i = _made.Count - 1; i >= 0; i--)
                if (_made[i] != null) Object.DestroyImmediate(_made[i]);
            _made.Clear();
            foreach (var c in _cameras) if (c != null) c.enabled = true;
            _cameras.Clear();
            HullMeshPresentation.Service = _hulls;
            FoamInjectionRegistry.PublishLookStrength(_lookStrength);
            FoamInjectionRegistry.PublishSurfDeposit(_deposit, _drawnScale);
            CurlingBreakerPasses.Lower();
            GameServices.Reset();
            _sink = null;
            _started = false;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator SilhouetteSequences()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("Graphics device required for B2.");
#if UNITY_EDITOR
            Begin();
            float ppm = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath).CurlingBreakers.DefaultPixelsPerMetre;
            BuildCreek(Pixels, Pixels, ppm, RenderTextureFormat.ARGBFloat);
            CurlingBreakerSettings bar = _config.CurlingBreakers;
            int frameRate = bar.FrameRate;
            string folder = Folder();
            var csv = new StringBuilder("tide,level_m,frame,t_s,live,phase,peak_px,throw_px,cpu_readable,changed_px,outside_px,box_x,box_y,box_w,box_h\n");
            var failures = new List<string>();
            for (var e = Settle(TideLevels[0]); e.MoveNext();) yield return e.Current;
            ReadEvents(folder, frameRate);
            for (var e = Compile(PeakFrame(frameRate), frameRate); e.MoveNext();) yield return e.Current;
            for (int k = 0; k < TideNames.Length; k++)
            {
                string tide = TideNames[k];
                float level = TideLevels[k];
                for (var e = Settle(level); e.MoveNext();) yield return e.Current;

                // From here to the tide's last frame, one rendered frame (no yield): one _Time for every compare.
                At(Start);
                Curl(false);
                if (Changed(Shoot(null), Shoot(null), Pixels, default, out _, out _) > 0)
                {
                    failures.Add(F($"{tide} water: the same sea shot twice in one frame differs, so no compare proves anything"));
                    continue;
                }
                int live = 0, readable = 0;
                for (int f = 0; f < Frames; f++)
                {
                    double t = Start + f / (double)frameRate;
                    At(t);
                    Curl(false);
                    byte[] off = Shoot(null);
                    bool isLive = Curl(true);
                    CurlingBreakerPose pose = _host.Pool[0].Pose;
                    float peakPx = isLive ? pose.Peak * ppm : 0f;
                    float throwPx = isLive ? pose.Throw * ppm : 0f;
                    bool cpuReadable = isLive && peakPx >= bar.MinReadableHeight && throwPx >= bar.MinReadableThrow;
                    RectInt inside = Footprint();
                    string stem = isLive && k == 0 ? Path.Combine(folder, F($"{tide}_{f:000}")) : null;
                    byte[] on = Shoot(stem);
                    int changed = Changed(off, on, Pixels, inside, out RectInt box, out int outside);
                    if (stem != null) Mask(off, on, Pixels, stem + "_diff.png");
                    if (isLive) live++;
                    if (outside > 0)
                        failures.Add(F($"{tide} water, frame {f}: {outside} of {changed} changed pixels lie outside the CPU pose's bounds (live {isLive})"));
                    if (cpuReadable && changed == 0)
                        failures.Add(F($"{tide} water, frame {f}: a readable pose ({peakPx:F1} px tall, thrown {throwPx:F1} px) drew nothing"));
                    if (cpuReadable && changed > 0 && outside == 0 && box.height >= bar.MinReadableHeight) readable++;
                    csv.Append(F($"{tide},{level:R},{f},{t:R},{isLive},{(isLive ? pose.Phase : 0f):R},{peakPx:R},{throwPx:R},{cpuReadable},{changed},{outside},{box.x},{box.y},{box.width},{box.height}\n"));
                }
                if (k == 0 && readable < bar.ReadableFrameBar)
                    failures.Add(F($"low water: {readable} readable frames of {live} live, under the bar of {bar.ReadableFrameBar}"));
                Debug.Log(F($"[curl-b2] {tide} water ({level:F1} m): {live} live frames, {readable} readable, of {Frames}"));
            }
            File.WriteAllText(Path.Combine(folder, "silhouette.csv"), csv.ToString());
            Debug.Log("[curl-b2] " + folder);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
#else
            Assert.Ignore("SKIPPED: plate assets need the editor.");
            yield break;
#endif
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator BoatOcclusion()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("Graphics device required for B2.");
#if UNITY_EDITOR
            Begin();
            float ppm = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath).CurlingBreakers.DefaultPixelsPerMetre;
            BuildCreek(Pixels, Pixels, ppm, RenderTextureFormat.ARGBFloat);
            int frameRate = _config.CurlingBreakers.FrameRate;
            string folder = Folder();
            for (var e = Settle(TideLevels[0]); e.MoveNext();) yield return e.Current;
            int peak = PeakFrame(frameRate);
            double t = Start + peak / (double)frameRate;
            At(t);
            Assert.IsTrue(Curl(true), "premise: the peak frame is live");
            CurlingBreakerPose pose = _host.Pool[0].Pose;
            // On the band the pose draws at this frame: the centre column's front, the band's foot (the band
            // stands the pose's peak above it and reaches the pose's throw shoreward of it).
            Vector2 berth = pose.Front[pose.Columns / 2];
            string band = F($"the band there {pose.Peak * ppm:F1} px tall and thrown {pose.Throw * ppm:F2} px (phase {pose.Phase:R}, period {pose.Period:R} s, peak {pose.Peak:R} m, full throw {pose.FullThrow:R} m)");
            var boat = Host("Dory");
            boat.transform.position = new Vector3(berth.x, berth.y, 0);
            BoatHullSkinner.Apply(boat, AssetDatabase.LoadAssetAtPath<BoatVisualDef>(DoryPath), null,
                new BoatHullSkinner.Options { SkipOars = true });
            for (var e = Settle(TideLevels[0]); e.MoveNext();) yield return e.Current;
            for (var e = Compile(peak, frameRate); e.MoveNext();) yield return e.Current;

            // A (dory, curl), B (dory), C (curl), D (neither): one rendered frame, no yield between them.
            At(t);
            Assert.IsTrue(Curl(true), "premise: live with the dory there");
            byte[] a = Shoot(Path.Combine(folder, "boat_a_dory_curl"));
            Curl(false);
            byte[] b = Shoot(Path.Combine(folder, "boat_b_dory"));
            boat.SetActive(false);
            Curl(true);
            byte[] c = Shoot(Path.Combine(folder, "boat_c_curl"));
            Curl(false);
            byte[] d = Shoot(Path.Combine(folder, "boat_d_sea"));
            boat.SetActive(true);

            int overlap = 0, boatWins = 0, curlWins = 0, neither = 0, curlOnlyMoved = 0, boatOnlyMoved = 0, restMoved = 0;
            var overlapMask = new Color32[Pixels * Pixels];
            for (int p = 0, i = 0; i < a.Length; p++, i += 16)
            {
                bool boatThere = !Same(b, d, i), curlThere = !Same(c, d, i);
                bool asBoat = Same(a, b, i), asCurl = Same(a, c, i);
                overlapMask[p] = new Color32(0, 0, 0, 255);
                if (boatThere && curlThere)
                {
                    overlap++;
                    overlapMask[p] = asBoat ? new Color32(255, 255, 255, 255) : asCurl
                        ? new Color32(128, 128, 128, 255) : new Color32(255, 0, 0, 255);
                    if (asBoat && !asCurl) boatWins++;
                    else if (asCurl && !asBoat) curlWins++;
                    else if (!asBoat && !asCurl) neither++;
                }
                else if (curlThere) { if (!asCurl) curlOnlyMoved++; }
                else if (boatThere) { if (!asBoat) boatOnlyMoved++; }
                else if (!Same(a, d, i)) restMoved++;
            }
            WriteMask(overlapMask, Pixels, Path.Combine(folder, "boat_overlap.png"));
            string counts = F($"overlap {overlap} px: the dory's {boatWins}, the curl's {curlWins}, neither {neither}; curl-only moved {curlOnlyMoved}, dory-only moved {boatOnlyMoved}, elsewhere moved {restMoved}");
            File.WriteAllText(Path.Combine(folder, "boat.txt"), F($"Dory berthed on the curl's centre front at ({berth.x:F2}, {berth.y:F2}), {band}, low water, frame {peak} (t {t:R} s). {counts}."));
            Assert.Greater(overlap, 0, "premise: the dory and the curl overlap on screen; " + counts);
            Assert.AreEqual(0, neither, "an overlap pixel is neither the dory's nor the curl's; " + counts);
            Assert.Greater(boatWins, 0, "the dory in front never hid the curl; " + counts);
            Assert.AreEqual(0, curlOnlyMoved + boatOnlyMoved + restMoved, "the dory and the curl changed each other's pixels; " + counts);
#else
            Assert.Ignore("SKIPPED: plate assets need the editor.");
            yield break;
#endif
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator ZeroDialPixelIdentity()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("Graphics device required for B2.");
#if UNITY_EDITOR
            Begin();
            float ppm = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath).CurlingBreakers.DefaultPixelsPerMetre;
            BuildCreek(Pixels, Pixels, ppm, RenderTextureFormat.ARGBFloat);
            int frameRate = _config.CurlingBreakers.FrameRate;
            string folder = Folder();
            for (var e = Settle(TideLevels[0]); e.MoveNext();) yield return e.Current;
            int peak = PeakFrame(frameRate);
            for (var e = Compile(peak, frameRate); e.MoveNext();) yield return e.Current;

            // No host, the master at 0, the master at 1, then 0 again: one rendered frame, no yield between them.
            At(Start + peak / (double)frameRate);
            _host.enabled = false;
            byte[] none = Shoot(Path.Combine(folder, "zero_no_host"));
            float flagNone = Shader.GetGlobalFloat(CurlingBreakerPasses.EnabledId);
            _host.enabled = true;
            bool liveAtZero = Curl(false);
            byte[] zero = Shoot(Path.Combine(folder, "zero_master_0"));
            float flagZero = Shader.GetGlobalFloat(CurlingBreakerPasses.EnabledId);
            bool live = Curl(true);
            byte[] one = Shoot(Path.Combine(folder, "zero_master_1"));
            Curl(false);
            byte[] after = Shoot(null);
            float flagAfter = Shader.GetGlobalFloat(CurlingBreakerPasses.EnabledId);

            Assert.IsFalse(liveAtZero, "the master at 0 held a live curl");
            Assert.IsTrue(live, "premise: the master at 1 draws at the peak frame");
            Assert.Greater(Changed(zero, one, Pixels, default, out _, out _), 0, "premise: the live curl changes the frame");
            Assert.AreEqual(0, Changed(none, zero, Pixels, default, out _, out _), "the master at 0 is not the frame with no curl at all");
            Assert.AreEqual(0, Changed(zero, after, Pixels, default, out _, out _), "a live frame left a trace in the next zero-dial frame");
            Assert.AreEqual(0f, flagNone, "the handoff flag was up with no curl host");
            Assert.AreEqual(0f, flagZero, "the handoff flag was up with the master at 0");
            Assert.AreEqual(0f, flagAfter, "the handoff flag stayed up after the curl was released");
#else
            Assert.Ignore("SKIPPED: plate assets need the editor.");
            yield break;
#endif
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator WorstViewBudget()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("Graphics device required for B2.");
#if UNITY_EDITOR
            Begin();
            BuildCreek(WorstWidth, WorstHeight, WorstPixelsPerMetre, RenderTextureFormat.ARGBHalf);
            int frameRate = _config.CurlingBreakers.FrameRate;
            string folder = Folder();
            for (var e = Settle(TideLevels[0]); e.MoveNext();) yield return e.Current;
            int peak = PeakFrame(frameRate);
            for (var e = Compile(peak, frameRate); e.MoveNext();) yield return e.Current;
            At(Start + peak / (double)frameRate);
            Assert.IsTrue(Curl(true), "premise: the worst view is live");

            // The CPU frame: one Tick, after a warm-up, at the peak.
            for (int i = 0; i < BudgetWarmUp; i++) _host.Tick();
            var cpu = new List<double>(BudgetSamples);
            var sw = new Stopwatch();
            for (int i = 0; i < BudgetSamples; i++)
            {
                sw.Restart();
                _host.Tick();
                sw.Stop();
                cpu.Add(sw.Elapsed.TotalMilliseconds);
            }
            Assert.AreEqual(1, _host.LiveCount, "premise: every measured tick drew the curl");

            // Nothing allocated per frame (rule 7), once the meshes exist.
            Assert.That(() => { _sink = new object[64]; }, Is.AllocatingGCMemory(),
                        "harness: the GC.Alloc recorder saw no allocation at all");
            Assert.That(() => _host.Tick(), Is.Not.AllocatingGCMemory(),
                        "a live curl frame allocated (rule 7: nothing per frame)");

            // The GPU: the camera's render alone, synced by a one-pixel readback, the curl on and off in turn.
            for (int i = 0; i < BudgetWarmUp; i++)
            {
                Curl(i % 2 == 0);
                _camera.Render();
            }
            var on = new List<double>(BudgetSamples);
            var off = new List<double>(BudgetSamples);
            for (int i = 0; i < 2 * BudgetSamples; i++)
            {
                bool curl = i % 2 == 0;
                Curl(curl);
                Sync();
                sw.Restart();
                _camera.Render();
                Sync();
                sw.Stop();
                (curl ? on : off).Add(sw.Elapsed.TotalMilliseconds);
                if (i % SamplesPerYield == SamplesPerYield - 1) yield return null;
            }
            double cpuMs = Percentile(cpu, 0.5), onMs = Percentile(on, 0.5), offMs = Percentile(off, 0.5);
            double gpuDeltaMs = onMs - offMs;
            File.WriteAllText(Path.Combine(folder, "worst-view.csv"),
                "view_px,px_per_m,cpu_tick_p50_ms,cpu_tick_p95_ms,render_on_p50_ms,render_off_p50_ms,render_delta_ms,render_on_p95_ms,render_off_p95_ms,gpu\n" +
                F($"{WorstWidth}x{WorstHeight},{WorstPixelsPerMetre:R},{cpuMs:R},{Percentile(cpu, 0.95):R},{onMs:R},{offMs:R},{gpuDeltaMs:R},{Percentile(on, 0.95):R},{Percentile(off, 0.95):R},{SystemInfo.graphicsDeviceName}\n"));
            Assert.LessOrEqual(cpuMs, CpuTickBudgetMs, F($"the curl's CPU frame is {cpuMs:F4} ms at the median"));
            Assert.LessOrEqual(gpuDeltaMs, GpuDeltaBudgetMs, F($"the curl costs {gpuDeltaMs:F4} ms of render at the median ({onMs:F3} on, {offMs:F3} off)"));
#else
            Assert.Ignore("SKIPPED: plate assets need the editor.");
            yield break;
#endif
        }

        // ---- the stage --------------------------------------------------------------------------------------

#if UNITY_EDITOR
        // The creek as the lee sea plates build it (G3, its sea plane and displaced surface), the pilot's sea,
        // the picked site's host, and a camera on the site's centre at the given scale.
        void BuildCreek(int width, int height, float pixelsPerMetre, RenderTextureFormat format)
        {
            _clock = new PlateClock();
            _config = Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath)));
            WaveFieldSettings field = _config.WaveField;
            field.PhaseSeed = PhaseSeed;
            _config.WaveField = field;
            Dial(false);
            GameServices.Config = _config;
            GameServices.Clock = _clock;
            HullMeshPresentation.Service = new IsoFacetHullPresentationService();
            float toward = GaleTowardDeg * Mathf.Deg2Rad;
            _wind = new PlateWind { Wind = new Vector2(Mathf.Sin(toward), Mathf.Cos(toward)) * GaleSpeed, Level = TideLevels[0] };
            GameServices.Environment = _wind;
            _bridge = Host("CurlWaveBridge").AddComponent<WaveFieldBridge>();

            var ground = NineMileCreekBuilder.LoadGround();   // G3; throws rather than return no ground
            var terrain = Host("NineMileCreekTerrain").AddComponent<PaintedTidalTerrain>();
            terrain.Map = ground;
            GameServices.TidalTerrain = terrain;

            var sea = Host("NineMileCreekSea");
            sea.SetActive(false);
            _sea = sea.AddComponent<SpriteRenderer>();
            _sea.sortingOrder = -5;
            _sea.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(WaterPath);
            _sea.sprite = WaterSceneTemplate.LoadSpriteAny(SeaTilePath);
            Vector2 size = NineMileCreekBuilder.NineMileCreekSeaSize;
            WaterSceneTemplate.ConfigureSeaPlane(_sea, size);
            _surface = sea.AddComponent<WaterSurface>();
            WaterSceneTemplate.ConfigureLandRegionWater(sea, Vector2.zero, size, 256, -6, 12, 1);
            _surface.ConfigurePaintedHeightMap(ground.HeightTexture, ground.WorldCenter, ground.WorldSize,
                ground.MinElevation, ground.MaxElevation);
            sea.SetActive(true);

            _site = AssetDatabase.LoadAssetAtPath<CurlingBreakerSiteDef>(SitePath);
            Assert.IsNotNull(_site, "premise: the picked site");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.IsNotNull(shader, "premise: the curl shader");
            _host = Host("CurlingBreakers").AddComponent<CurlingBreakerRegion>();
            _host.Configure(new[] { _site }, _sea, shader);

            _camera = Host("CurlCamera").AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.orthographicSize = height / pixelsPerMetre / 2f;
            _camera.nearClipPlane = 1;
            // Hull bounds include the depth shear until the vertex shader runs (as the lee sea plates).
            _camera.farClipPlane = 600;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(.08f, .12f, .16f, 1);
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.transform.position = new Vector3(_site.Centre.x, _site.Centre.y, -100);
            _target = Keep(new RenderTexture(width, height, 24, format, RenderTextureReadWrite.Linear));
            _camera.targetTexture = _target;
            _read = Keep(new Texture2D(width, height, TextureFormat.RGBAFloat, false, true));
            _probe = Keep(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true));
        }

        // Frames until the sea is drawn at the tide's level and the displaced surface has published its frame.
        IEnumerator Settle(float level)
        {
            _wind.Level = level;
            for (int i = 0; i < MaxSettleFrames; i++)
            {
                yield return null;
                Publish();
                if (DrawnLevel() == level && DisplacedWaterRegistry.TryGetIsoDepthFrame(out _)) yield break;
            }
            Assert.Fail(F($"premise: the sea never settled at {level:F1} m (drawn at {DrawnLevel():R} m)"));
        }

        // A live frame drawn once, then frames until no shader is compiling (async compilation stays on: a
        // PlayMode plate that turns it off has frozen on D3D12).
        IEnumerator Compile(int frame, int frameRate)
        {
            At(Start + frame / (double)frameRate);
            Curl(true);
            _camera.Render();
            for (int i = 0; i < MaxCompileFrames && ShaderUtil.anythingCompiling; i++)
            {
                yield return null;
                _camera.Render();
            }
            Assert.IsFalse(ShaderUtil.anythingCompiling, "premise: the plate's shaders finished compiling");
        }
#endif

        // The live frame of one period with the tallest curl, on the CPU alone.
        int PeakFrame(int frameRate)
        {
            int best = -1;
            float tallest = 0f;
            for (int f = 0; f < Frames; f++)
            {
                At(Start + f / (double)frameRate);
                if (!Curl(true) || _host.Pool[0].Pose.Peak <= tallest) continue;
                tallest = _host.Pool[0].Pose.Peak;
                best = f;
            }
            Assert.GreaterOrEqual(best, 0, "premise: low water earns a curl within one period");
            return best;
        }

        // Low water's events around the window on the CPU alone, before any premise: each frame's event clock,
        // whether the curl drew, the event's one bore beside the births the centre and both ends read on their own,
        // and the full throw and readable count that one bore gives. Written to events.csv; it asserts nothing.
        void ReadEvents(string folder, int frameRate)
        {
            var csv = new StringBuilder("frame,t_s,q,live,event_birth,own_mid,own_first,own_last,working,plunging,"
                + "whitewater,eligible_mid,eligible_first,eligible_last,full_throw_m,own_throw_m,peak_m,readable_frames\n");
            for (int f = -EventWindows * Frames; f < (EventWindows + 1) * Frames; f++)
            {
                double t = Start + f / (double)frameRate;
                At(t);
                bool live = Curl(true);
                CurlingBreakerSettings settings = CurlingBreakerPasses.Settings;
                if (!_host.TryReadLive(GameServices.TidalTerrain, _wind.Level, out CurlingBreakerInputs inputs))
                {
                    csv.Append(F($"{f},{t:R},,{live}\n"));
                    continue;
                }
                WaveFetchSample fetch = GameServices.FetchAt(_site.Centre);
                CurlingBreakerLook look = _host.ReadLook();
                SurfState mid = CurlingBreakerSampler.SurfAt(in inputs, fetch, _site.Centre);
                Vector2 half = new Vector2(-mid.ShorewardDirection.y, mid.ShorewardDirection.x) * (_site.LengthMeters * 0.5f);
                SurfState first = CurlingBreakerSampler.SurfAt(in inputs, fetch, _site.Centre - half);
                SurfState last = CurlingBreakerSampler.SurfAt(in inputs, fetch, _site.Centre + half);
                float birth = mid.IsWorking ? CurlingBreakerSampler.EventBirthEnergy01(in inputs, fetch, _site.Centre, in mid) : 0f;
                float period = BreakerMath.PeriodSeconds(inputs.Trains.Dominant);
                float q = CurlingBreakerMath.PhaseFraction(mid.BorePhaseDegrees, period);
                float peak = settings.HeightFraction * mid.StandingHeightMeters;
                float fullThrow = CurlingBreakerMath.EventThrow(mid.StandingHeightMeters, mid.PlungingWeight01,
                    look.Plunge, birth, look.LipThrow);
                float ownThrow = CurlingBreakerMath.EventThrow(mid.StandingHeightMeters, mid.PlungingWeight01,
                    look.Plunge, mid.BirthEnergy01, look.LipThrow);
                bool eligibleMid = CurlingBreakerSampler.Eligible(in mid, birth, in settings);
                bool eligibleFirst = CurlingBreakerSampler.Eligible(in first, birth, in settings);
                bool eligibleLast = CurlingBreakerSampler.Eligible(in last, birth, in settings);
                int readable = CurlingBreakerMath.ReadableFrames(peak, fullThrow, period, in settings);
                csv.Append(F($"{f},{t:R},{q:R},{live},{birth:R},{mid.BirthEnergy01:R},{first.BirthEnergy01:R},{last.BirthEnergy01:R},"))
                    .Append(F($"{mid.IsWorking},{mid.PlungingWeight01:R},{mid.Whitewater01:R},{eligibleMid},{eligibleFirst},{eligibleLast},"))
                    .Append(F($"{fullThrow:R},{ownThrow:R},{peak:R},{readable}\n"));
            }
            File.WriteAllText(Path.Combine(folder, "events.csv"), csv.ToString());
        }

        void Dial(bool on)
        {
            CurlingBreakerSettings dial = _config.CurlingBreakers;
            dial.Master = on ? 1 : 0;
            _config.CurlingBreakers = dial;
        }

        // The curl's frame at the dial: true when its one slot holds a live pose.
        bool Curl(bool on)
        {
            Dial(on);
            _host.Tick();
            return _host.LiveCount > 0;
        }

        void At(double seconds)
        {
            _clock.TotalSeconds = seconds;
            Publish();
        }

        void Publish()
        {
            _bridge.SendMessage("Update");
            PushUniforms.Invoke(_surface, null);
            // The displaced chunks draw the sea from their own copy of the surface's block, taken in their own
            // Update: copy it now (as the lee sea plates do).
            var displaced = _surface.GetComponent<DisplacedWaterSurface>();
            if (displaced != null && displaced.Displaced)
                SyncDisplaced.Invoke(displaced, new object[] { false });
            Shader.SetGlobalVector("_DayNightTint", Color.white);
            Shader.SetGlobalFloat("_SunElevation", 1);
            Shader.SetGlobalVector("_SunDir", new Vector4(.4f, .8f, 0, 0));
            // The foam buffer is held off: every compare is two shots inside one frame, and a buffer that moves
            // between renders would differ between them for reasons that are not the curl's.
            FoamInjectionRegistry.PublishLookStrength(0f);
        }

        // The level the sea is drawn at: the displaced chunks' copy when they draw it (NaN if two chunks
        // disagree), else the surface's own block.
        float DrawnLevel()
        {
            var block = new MaterialPropertyBlock();
            var displaced = _surface.GetComponent<DisplacedWaterSurface>();
            if (displaced != null && displaced.Displaced)
            {
                var chunks = (List<MeshRenderer>)DisplacedChunks.GetValue(displaced);
                float level = float.NaN;
                for (int i = 0; i < chunks.Count; i++)
                {
                    chunks[i].GetPropertyBlock(block);
                    float chunk = block.GetFloat("_WaterLevel");
                    if (i > 0 && chunk != level) return float.NaN;
                    level = chunk;
                }
                if (chunks.Count > 0) return level;
            }
            _surface.GetComponent<Renderer>().GetPropertyBlock(block);
            return block.GetFloat("_WaterLevel");
        }

        // The screen rect the live curl's sheet and mask may touch (their meshes' bounds, plus slack), or an
        // empty rect when no curl is live.
        RectInt Footprint()
        {
            if (_host.LiveCount <= 0) return default;
            CurlingBreakerPool.Slot slot = _host.Pool[0];
            Bounds bounds = slot.Sheet.bounds;
            bounds.Encapsulate(slot.Mask.bounds);
            Vector3 lo = _camera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, 0f));
            Vector3 hi = _camera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, 0f));
            int x0 = Mathf.FloorToInt(Mathf.Min(lo.x, hi.x) - SlackPx);
            int y0 = Mathf.FloorToInt(Mathf.Min(lo.y, hi.y) - SlackPx);
            int x1 = Mathf.CeilToInt(Mathf.Max(lo.x, hi.x) + SlackPx);
            int y1 = Mathf.CeilToInt(Mathf.Max(lo.y, hi.y) + SlackPx);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        // Renders twice and reads back (as the lee sea plates' Shoot). Returns the raw RGBA floats; writes a gamma
        // PNG unless the stem is null.
        byte[] Shoot(string stem)
        {
            _camera.Render();
            _camera.Render();
            var prior = RenderTexture.active;
            RenderTexture.active = _target;
            _read.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
            _read.Apply();
            RenderTexture.active = prior;
            byte[] data = _read.GetRawTextureData();
            if (stem == null) return data;
            var png = new Texture2D(_read.width, _read.height, TextureFormat.RGB24, false);
            var colors = _read.GetPixels();
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                for (int i = 0; i < colors.Length; i++) colors[i] = colors[i].gamma;
            png.SetPixels(colors);
            png.Apply();
            File.WriteAllBytes(stem + ".png", png.EncodeToPNG());
            Object.DestroyImmediate(png);
            return data;
        }

        // A one-pixel readback of the target: the CPU waits here until the GPU has drawn it.
        void Sync()
        {
            RenderTexture prior = RenderTexture.active;
            RenderTexture.active = _target;
            _probe.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
            RenderTexture.active = prior;
        }

        static string Folder()
        {
            string folder = Path.Combine(Application.temporaryCachePath, OutFolder);
            Directory.CreateDirectory(folder);
            return folder;
        }

        static string F(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

        static bool Same(byte[] a, byte[] b, int i)
        {
            for (int k = 0; k < 16; k++)
                if (a[i + k] != b[i + k]) return false;
            return true;
        }

        // Pixels (16 bytes of RGBA floats each) that differ by any bit, their bounding box, and how many of them
        // fall outside the rect.
        static int Changed(byte[] a, byte[] b, int width, RectInt inside, out RectInt box, out int outside)
        {
            int changed = 0, x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            outside = 0;
            for (int p = 0, i = 0; i < a.Length; p++, i += 16)
            {
                if (Same(a, b, i)) continue;
                int x = p % width, y = p / width;
                changed++;
                x0 = Mathf.Min(x0, x);
                y0 = Mathf.Min(y0, y);
                x1 = Mathf.Max(x1, x);
                y1 = Mathf.Max(y1, y);
                if (!inside.Contains(new Vector2Int(x, y))) outside++;
            }
            box = changed > 0 ? new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1) : default;
            return changed;
        }

        // The pixels two shots differ in, white on black, laid out as the shots' PNGs are.
        static void Mask(byte[] a, byte[] b, int width, string path)
        {
            var colors = new Color32[a.Length / 16];
            for (int p = 0, i = 0; p < colors.Length; p++, i += 16)
                colors[p] = Same(a, b, i) ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255);
            WriteMask(colors, width, path);
        }

        static void WriteMask(Color32[] colors, int width, string path)
        {
            var png = new Texture2D(width, colors.Length / width, TextureFormat.RGB24, false);
            png.SetPixels32(colors);
            png.Apply();
            File.WriteAllBytes(path, png.EncodeToPNG());
            Object.DestroyImmediate(png);
        }

        static double Percentile(List<double> samples, double p)
        {
            var sorted = new List<double>(samples);
            sorted.Sort();
            return sorted[Mathf.Clamp(Mathf.CeilToInt((float)(p * sorted.Count)) - 1, 0, sorted.Count - 1)];
        }
    }
}
