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
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using HiddenHarbours.App.Editor;
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    // Evidence for the lee sea, where each wave train grows back over its own fetch. Noon lighting and one
    // wave clock throughout; a steady wind toward each region's prevailing bearing, at the base speed and in
    // a storm, at mean and high water. Each shore is a lee station: a waterline cell whose land lies upwind,
    // with no fetch at the waterline, full fetch 100 m downwind, water all the way out and a berth deep
    // enough for a Cape Islander at 20 m (the middle such cell). Shots: the waterline, the berth, 50 m off
    // and 100 m off.
    //
    // Then the open sea. Every train's factor is exactly 1 at full fetch, under today's law and the lee law
    // alike, so a frame where every pixel has full fetch, in water deeper than any surf, must not move by
    // one bit. It is shot three times inside one rendered frame: shipped, with the fetch model off, and
    // shipped again.
    //
    // The shore fringe reads the open sea's wind through the local fetch factor, so every lee station frame
    // is shot twice inside one rendered frame: shipped, and with the fringe's lee off (ceilings of 1 keep
    // every reading: the fringe as it was before it read the fetch). In light wind the two must not differ
    // by one bit. Those two are the only assertions; the rest is plates.

    public class LeeSeaPlatePlayTests
    {
        const int Seed = 4242, Pixels = 768;
        const float Width = 48f, Berth = 20f, BaseWind = 2.3f, StormWind = 10f;
        const float OpenMargin = 8f, SurfMargin = 1f;
        const double Start = 900; // 15 minutes of play / noon on day one.
        static readonly float[] Offsets = { 0f, Berth, 50f, 100f };

        readonly struct Shore
        {
            public readonly string Region, Tide;
            public readonly float Level;
            public readonly Vector2 Waterline, Open;

            public Shore(string region, string tide, float level, Vector2 waterline, Vector2 open)
            {
                Region = region; Tide = tide; Level = level; Waterline = waterline; Open = open;
            }
        }

        // Waterline: the lee station at that water level. Open: the 8 m lattice point nearest the 100 m
        // station whose frame has full fetch and 4 m of water on the seabed the water draws (chosen before
        // the run). The test checks again that the frame is open, and deeper than this sea's surf.
        static readonly Shore[] Shores =
        {
            new Shore("StPeters", "mean", 0f, new Vector2(-201.25f, 9.75f), new Vector2(-152f, 112f)),
            new Shore("StPeters", "high", 2.2f, new Vector2(90.25f, 65.25f), new Vector2(192f, 168f)),
            new Shore("NineMileCreek", "mean", 0f, new Vector2(108.25f, -151.75f), new Vector2(184f, -40f)),
            new Shore("NineMileCreek", "high", 2.2f, new Vector2(56.25f, 141.75f), new Vector2(288f, 216f)),
        };

        static readonly MethodInfo PushUniforms = typeof(WaterSurface).GetMethod("PushUniforms",
            BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

        readonly List<Object> _made = new List<Object>();
        readonly List<Camera> _cameras = new List<Camera>();
        IHullMeshPresentationService _hulls;
        RenderTexture _target;
        Camera _camera;
        bool _started;
#if UNITY_EDITOR
        bool _asyncShaders;
#endif

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

        // A painted seabed read on the CPU. St Peters' water draws its painted map, while its scene gives the
        // sim the analytic zones; NMC's water bakes the sim's terrain at 256, and its seabed asset is the
        // same terrain baked at full resolution. The captions give the fetch on both sides of that seam.
        sealed class Painted : ITidalTerrain
        {
            readonly PaintedHeightField _field;
            public Painted(PaintedHeightField field) { _field = field; }
            public float ElevationAt(Vector2 worldPos) => _field.ElevationAt(worldPos);
        }

        T Keep<T>(T item) where T : Object { _made.Add(item); return item; }
        GameObject Host(string name) => Keep(new GameObject(name));

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED: plate capture needs a GPU.");
            _started = true;
#if UNITY_EDITOR
            _asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
#endif
            _hulls = HullMeshPresentation.Service;
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
            GameServices.Reset();
#if UNITY_EDITOR
            ShaderUtil.allowAsyncCompilation = _asyncShaders;
#endif
            _started = false;
        }

        [UnityTest, Order(10000), Timeout(600000)]
        public IEnumerator LeeShores_TidesAndWinds_AndTheOpenSea()
        {
#if UNITY_EDITOR
            var clock = new PlateClock();
            var config = Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameConfig>(
                "Assets/_Project/Data/Config/GameConfig.asset")));
            GameServices.Config = config;
            GameServices.Clock = clock;
            HullMeshPresentation.Service = new IsoFacetHullPresentationService();
            var wind = new PlateWind();
            GameServices.Environment = wind;
            var bridge = Host("PlateWaveBridge").AddComponent<WaveFieldBridge>();
            _camera = Host("PlateCamera").AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.orthographicSize = Width / 2;
            _camera.nearClipPlane = 1;
            // Hull bounds include the depth shear until the vertex shader runs (as WaveMemoryPlatePlayTests).
            _camera.farClipPlane = 600;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(.08f, .12f, .16f, 1);
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _target = Keep(new RenderTexture(Pixels, Pixels, 24, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear));
            _camera.targetTexture = _target;
            var read = Keep(new Texture2D(Pixels, Pixels, TextureFormat.RGBAFloat, false, true));
            var water = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Water.mat");
            Assert.IsNotNull(water);
            var bed = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>("Assets/_Project/Data/Terrain/StPetersSeabed.asset");
            var creekBed = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>("Assets/_Project/Data/Terrain/NineMileCreekSeabed.asset");
            Assert.IsNotNull(bed);
            Assert.IsNotNull(creekBed);
            WaveFetchSettings shipped = config.WaveFetch, off = shipped, fringeOff = shipped;
            off.Strength = 0;
            fringeOff.FringeLeeRoughness = 1f;
            fringeOff.FringeLeeSeaState = 1f;
            // The lee law's per-train factor where this build has one; a build before it records today's.
            MethodInfo lee = typeof(WaveFetch).GetMethod("TrainEnvelope01", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(float), typeof(float), typeof(WaveFetchSettings).MakeByRefType() }, null);
            string folder = Path.Combine(Application.temporaryCachePath, "lee-sea-w4-3");
            Directory.CreateDirectory(folder);
            var html = new StringBuilder("<meta charset='utf-8'><title>Lee sea plates</title>");
            var frames = new StringBuilder("region,tide,level_m,wind,speed_m_per_s,toward_deg,offset_m,x,y,boat_x,boat_y,fetch01_drawn,fetch01_sim,today_factor,lee_factors,wavelengths_m,dominant,changed_fringe_off,max_abs_fringe_off\n");
            var opens = new StringBuilder("region,tide,wind,x,y,lattice_points,min_fetch01,min_depth_m,surf_outer_depth_m,pixels,changed_fetch_off,max_abs_fetch_off,changed_repeat,max_abs_repeat\n");
            var failures = new List<string>();
            foreach (string region in new[] { "StPeters", "NineMileCreek" })
            {
                bool st = region == "StPeters";
                float toward = AssetDatabase.LoadAssetAtPath<WindProfileDef>(
                    "Assets/_Project/Data/Wind/Wind_" + region + ".asset").Profile.PrevailingDirectionDeg;
                var d = new Vector2(Mathf.Cos(toward * Mathf.Deg2Rad), Mathf.Sin(toward * Mathf.Deg2Rad));
                string seabeds = st
                    ? "on the painted seabed the water draws, and on the analytic zones the sim reads"
                    : "on the seabed bake at full resolution (the water bakes the same terrain at 256), and on the terrain the sim reads";
                var terrainHost = Host(region + "Terrain");
                ITidalTerrain drawn, sim;
                if (st)
                {
                    var terrain = terrainHost.AddComponent<TidalTerrain>();
                    StPetersBuilder.ConfigureTidalTerrain(terrain);
                    GameServices.TidalTerrain = terrain;
                    sim = terrain;
                    drawn = new Painted(bed.Field);
                }
                else
                {
                    var terrain = terrainHost.AddComponent<MainlandTidalTerrain>();
                    NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);
                    GameServices.TidalTerrain = terrain;
                    sim = terrain;
                    drawn = new Painted(creekBed.Field);
                }
                var sea = Host(region + "Sea");
                sea.SetActive(false);
                var sr = sea.AddComponent<SpriteRenderer>();
                sr.sortingOrder = -5;
                sr.sharedMaterial = water;
                sr.sprite = WaterSceneTemplate.LoadSpriteAny("Assets/_Project/Art/Tilesets/Water/SeaTile.png");
                Vector2 size = st ? StPetersBuilder.RegionWorldSize : NineMileCreekBuilder.NineMileCreekSeaSize;
                WaterSceneTemplate.ConfigureSeaPlane(sr, size);
                var surface = sea.AddComponent<WaterSurface>();
                WaterSceneTemplate.ConfigureLandRegionWater(sea, Vector2.zero, size, 256, -6, 12, 1);
                if (st)
                    surface.ConfigurePaintedHeightMap(bed.HeightTexture, bed.WorldCenter,
                        bed.WorldSize, bed.MinElevation, bed.MaxElevation);
                sea.SetActive(true);
                foreach (var shore in Shores)
                {
                    if (shore.Region != region) continue;
                    wind.Level = shore.Level;
                    Vector2 berth = shore.Waterline + d * Berth;
                    var boat = Host("CapeIslander");
                    boat.transform.position = new Vector3(berth.x, berth.y, 0);
                    BoatHullSkinner.Apply(boat, AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                        "Assets/_Project/Data/Boats/Visuals/CapeIslanderIso.asset"), null,
                        new BoatHullSkinner.Options { SkipOars = true });
                    foreach (float speed in new[] { BaseWind, StormWind })
                    {
                        string mode = speed == BaseWind ? "base" : "storm";
                        wind.Wind = d * speed;
                        config.WaveFetch = shipped;
                        var field = GameServices.WaveField;
                        var trains = new WaveFieldAnimator().Tick(clock.TotalSeconds, wind, in field);
                        string head = F($"{region}; {shore.Tide} water ({shore.Level:F1} m); {mode} wind {speed:F1} m/s toward {toward:F0} deg, the prevailing bearing; seed {Seed}; noon lighting, clear; wave clock {Start:F0} s");
                        foreach (float offset in Offsets)
                        {
                            Vector2 at = shore.Waterline + d * offset;
                            _camera.transform.position = new Vector3(at.x, at.y, -100);
                            yield return null;
                            yield return null;
                            Publish(bridge, surface);
                            float fetch = WaveFetch.Fetch01(at, wind.Wind, shore.Level, drawn, in shipped);
                            float simFetch = WaveFetch.Fetch01(at, wind.Wind, shore.Level, sim, in shipped);
                            float today = WaveFetch.Envelope01(fetch, in shipped);
                            var factors = new StringBuilder();
                            var lambdas = new StringBuilder();
                            for (int slot = 0; slot < trains.Count; slot++)
                            {
                                if (slot > 0) { factors.Append(';'); lambdas.Append(';'); }
                                float lambda = trains[slot].Wavelength;
                                lambdas.Append(lambda.ToString("R", CultureInfo.InvariantCulture));
                                factors.Append(lee == null ? "n/a" : ((float)lee.Invoke(null,
                                    new object[] { fetch, lambda, shipped })).ToString("R", CultureInfo.InvariantCulture));
                            }
                            string stem = F($"{region}_{shore.Tide}_{mode}_{offset:000}m");
                            string caption = F($"{head}; {offset:F0} m off the lee waterline, at ({at.x:F2}, {at.y:F2}); a Cape Islander on the {Berth:F0} m berth at ({berth.x:F2}, {berth.y:F2}). Fetch here {fetch:F4} and {simFetch:F4}, {seabeds}. On the first, today's law gives every train {today:F4}; the lee law gives [{factors}] to trains of [{lambdas}] m.");
                            // Shipped, then the fringe's lee off, inside one rendered frame (no yield between them).
                            byte[] stationShot = Shoot(read, Path.Combine(folder, stem), false);
                            config.WaveFetch = fringeOff;
                            Publish(bridge, surface);
                            byte[] fringeAsBefore = Shoot(read, Path.Combine(folder, stem + "_fringeoff"), false);
                            config.WaveFetch = shipped;
                            Publish(bridge, surface);
                            Compare(stationShot, fringeAsBefore, out int changedFringe, out float maxFringe);
                            Mask(stationShot, fringeAsBefore, Path.Combine(folder, stem + "_fringediff.png"));
                            if (mode == "base" && changedFringe > 0)
                                failures.Add(F($"{region} {shore.Tide} water, light wind, {offset:F0} m off the lee waterline: the fringe's lee moved {changedFringe} pixels (max |d| {maxFringe:R}); in light wind it must keep every reading"));
                            caption += F($" Shot twice inside one rendered frame, shipped and with the fringe's lee off: {changedFringe} pixels differ (max |d| {maxFringe:R}).");
                            File.WriteAllText(Path.Combine(folder, stem + ".txt"), caption);
                            html.Append("<figure><img width='768' src='").Append(stem).Append(".png'><img width='768' src='")
                                .Append(stem).Append("_fringeoff.png'><img width='768' src='").Append(stem)
                                .Append("_fringediff.png'><figcaption>").Append(caption).Append("</figcaption></figure>\n");
                            frames.Append(F($"{region},{shore.Tide},{shore.Level:R},{mode},{speed:R},{toward:R},{offset:R},{at.x:R},{at.y:R},{berth.x:R},{berth.y:R},{fetch:R},{simFetch:R},{today:R},{factors},{lambdas},{trains.DominantIndex},{changedFringe},{maxFringe:R}\n"));
                        }

                        // The open sea, on the drawn seabed: full fetch, and water deeper than this sea's surf.
                        float outer = BreakerMath.ContourFor(trains.Dominant, 1f, GameServices.Breakers).OuterDepths.x;
                        float minFetch = 1f, minDepth = float.MaxValue;
                        int points = 0;
                        float reach = Width / 2 + OpenMargin;
                        for (float y = -reach; y <= reach; y += 1f)
                            for (float x = -reach; x <= reach; x += 1f)
                            {
                                var p = shore.Open + new Vector2(x, y);
                                minFetch = Mathf.Min(minFetch, WaveFetch.Fetch01(p, wind.Wind, shore.Level, drawn, in shipped));
                                minDepth = Mathf.Min(minDepth, shore.Level - drawn.ElevationAt(p));
                                points++;
                            }
                        _camera.transform.position = new Vector3(shore.Open.x, shore.Open.y, -100);
                        yield return null;
                        yield return null;
                        // Three shots inside one rendered frame (no yield between them): the same _Time.
                        string open = F($"{region}_{shore.Tide}_{mode}_open");
                        Publish(bridge, surface);
                        byte[] shot = Shoot(read, Path.Combine(folder, open), true);
                        config.WaveFetch = off;
                        Publish(bridge, surface);
                        byte[] noFetch = Shoot(read, Path.Combine(folder, open + "_fetchoff"), false);
                        config.WaveFetch = shipped;
                        Publish(bridge, surface);
                        byte[] again = Shoot(read, null, false);
                        Compare(shot, noFetch, out int changedOff, out float maxOff);
                        Compare(shot, again, out int changedRepeat, out float maxRepeat);
                        int pixels = Pixels * Pixels;
                        string where = F($"{region} {shore.Tide} water, {mode} wind, open sea at ({shore.Open.x:F0}, {shore.Open.y:F0})");
                        if (minFetch < 1f || minDepth < outer + SurfMargin)
                            failures.Add(F($"{where}: not open water on the drawn seabed (min fetch {minFetch:R}, min depth {minDepth:F2} m, surf to {outer:F2} m), so the frame proves nothing"));
                        else if (changedRepeat > 0)
                            failures.Add(F($"{where}: the same sea shot twice in one frame differs in {changedRepeat} pixels (max |d| {maxRepeat:R}), so the compare proves nothing"));
                        else if (changedOff > 0)
                            failures.Add(F($"{where}: the open sea moved with the fetch model: {changedOff} of {pixels} pixels differ from the fetch-off sea (max |d| {maxOff:R})"));
                        string openCaption = F($"{head}; open water at ({shore.Open.x:F0}, {shore.Open.y:F0}). On the drawn seabed every point of the frame and an {OpenMargin:F0} m margin, on 1 m spacing ({points} points), has fetch {minFetch:F4} or more and {minDepth:F2} m of water or more; this sea's surf ends in {outer:F2} m. Shot three times inside one rendered frame: shipped, fetch model off (Strength 0), shipped again. Pixels that differ from the fetch-off sea: {changedOff} of {pixels} (max |d| {maxOff:R}); from itself: {changedRepeat}.");
                        File.WriteAllText(Path.Combine(folder, open + ".txt"), openCaption);
                        html.Append("<figure><img width='768' src='").Append(open).Append(".png'><img width='768' src='")
                            .Append(open).Append("_fetchoff.png'><figcaption>").Append(openCaption).Append("</figcaption></figure>\n");
                        opens.Append(F($"{region},{shore.Tide},{mode},{shore.Open.x:R},{shore.Open.y:R},{points},{minFetch:R},{minDepth:R},{outer:R},{pixels},{changedOff},{maxOff:R},{changedRepeat},{maxRepeat:R}\n"));
                    }
                    Object.DestroyImmediate(boat);
                }
                Object.DestroyImmediate(sea);
                Object.DestroyImmediate(terrainHost);
            }
            File.WriteAllText(Path.Combine(folder, "frames.csv"), frames.ToString());
            File.WriteAllText(Path.Combine(folder, "open-sea.csv"), opens.ToString());
            File.WriteAllText(Path.Combine(folder, "index.html"), html.ToString());
            Debug.Log("[lee-sea-plates] " + folder);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
#else
            Assert.Ignore("SKIPPED: plate assets need the editor.");
            yield break;
#endif
        }

        static string F(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

        static void Publish(WaveFieldBridge bridge, WaterSurface surface)
        {
            bridge.SendMessage("Update");
            PushUniforms.Invoke(surface, null);
            Shader.SetGlobalVector("_DayNightTint", Color.white);
            Shader.SetGlobalFloat("_SunElevation", 1);
            Shader.SetGlobalVector("_SunDir", new Vector4(.4f, .8f, 0, 0));
        }

        // Renders twice and reads back, as WaveMemoryPlatePlayTests' Capture does. Returns the raw RGBA
        // floats; writes a gamma PNG (and the raw floats when asked) unless the stem is null.
        byte[] Shoot(Texture2D read, string stem, bool raw)
        {
            _camera.Render();
            _camera.Render();
            var prior = RenderTexture.active;
            RenderTexture.active = _target;
            read.ReadPixels(new Rect(0, 0, Pixels, Pixels), 0, 0);
            read.Apply();
            RenderTexture.active = prior;
            byte[] data = read.GetRawTextureData();
            if (stem == null) return data;
            if (raw) File.WriteAllBytes(stem + ".rgba", data);
            var png = new Texture2D(Pixels, Pixels, TextureFormat.RGB24, false);
            var colors = read.GetPixels();
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                for (int i = 0; i < colors.Length; i++) colors[i] = colors[i].gamma;
            png.SetPixels(colors); png.Apply();
            File.WriteAllBytes(stem + ".png", png.EncodeToPNG());
            Object.DestroyImmediate(png);
            return data;
        }

        // Pixels (16 bytes of RGBA floats each) that differ by any bit, and the largest channel difference.
        static void Compare(byte[] a, byte[] b, out int changed, out float maxAbs)
        {
            changed = 0;
            maxAbs = 0;
            if (a.Length != b.Length) { changed = a.Length / 16; maxAbs = float.PositiveInfinity; return; }
            for (int i = 0; i < a.Length; i += 16)
            {
                bool same = true;
                for (int k = 0; k < 16 && same; k++) same = a[i + k] == b[i + k];
                if (same) continue;
                changed++;
                for (int k = 0; k < 16; k += 4)
                    maxAbs = Mathf.Max(maxAbs, Mathf.Abs(BitConverter.ToSingle(a, i + k) - BitConverter.ToSingle(b, i + k)));
            }
        }

        // The pixels two shots differ in, white on black, laid out as the shots' PNGs are.
        static void Mask(byte[] a, byte[] b, string path)
        {
            var colors = new Color32[Pixels * Pixels];
            for (int p = 0, i = 0; p < colors.Length; p++, i += 16)
            {
                bool same = a.Length == b.Length;
                for (int k = 0; k < 16 && same; k++) same = a[i + k] == b[i + k];
                colors[p] = same ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255);
            }
            var png = new Texture2D(Pixels, Pixels, TextureFormat.RGB24, false);
            png.SetPixels32(colors);
            png.Apply();
            File.WriteAllBytes(path, png.EncodeToPNG());
            Object.DestroyImmediate(png);
        }
    }
}
