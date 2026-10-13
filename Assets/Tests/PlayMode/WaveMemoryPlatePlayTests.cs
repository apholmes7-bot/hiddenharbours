using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    // Evidence only. Lighting stays at noon while wave time advances by 5 s of play
    // (4 in-game minutes), through 120 s of play (1.6 in-game hours).

    public class WaveMemoryPlatePlayTests
    {
        const int Seed = 4242, Pixels = 768;
        const float Width = 48f;
        const double Start = 900; // 15 minutes of play / noon on day one.
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
            public WindProfile Profile;
            public bool Storm, Gust;
            public int WorldSeed => Seed;
            public TideProfile ActiveTideProfile { get; set; }
            public float TideHeightAt(double t) => 0;
            public float WaterLevelAt(double t) => 0;
            public WaveWindSample WaveWindAt(double t)
            {
                var baseline = WeatherModel.SampleWind(Start, Seed, 75, Profile);
                float angle = Mathf.Atan2(baseline.y, baseline.x);
                float speed = Storm ? 14 : 4;
                if (t >= Start)
                {
                    if (Gust) speed *= 1 + .5f * Mathf.Clamp01((float)((t - Start) / 5));
                    else angle += Mathf.PI / 6;
                }
                var wind = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                return new WaveWindSample(wind, WeatherModel.SeaState01(speed));
            }
            public EnvironmentSample Sample()
            {
                var w = WaveWindAt(GameServices.Clock.TotalSeconds);
                return new EnvironmentSample(w.Wind, Vector2.zero, 0,
                    WeatherModel.SeaFromWind(w.Wind.magnitude), 1, w.SeaState01);
            }
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
        public IEnumerator Regions_BaseAndStorm_VeerAndGust()
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
            // Hull bounds include the depth shear until the vertex shader runs.
            // NMC's bounds reach 428 m from this camera before that shear is removed.
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
            var gauge = Keep(new Material(MakeGaugeShader()));
            gauge.CopyPropertiesFromMaterial(water);
            var rows = new StringBuilder("region,mode,event,frame,play_s,clock_hours,radius_m,wind_x,wind_y,slot,dir_x,dir_y,lambda,phase,amplitude,turn_slide_m_per_play_s\n");
            string folder = Path.Combine(Application.temporaryCachePath, "wave-memory-w4-2");
            Directory.CreateDirectory(folder);
            var html = new StringBuilder("<meta charset='utf-8'><title>Wave memory plates</title>");
            foreach (string region in new[] { "StPeters", "NineMileCreek" })
            {
                bool st = region == "StPeters";
                wind.Profile = AssetDatabase.LoadAssetAtPath<WindProfileDef>(
                    "Assets/_Project/Data/Wind/Wind_" + region + ".asset").Profile;
                Vector2 at = st ? new Vector2(262, 0) : new Vector2(278, 0);
                var terrainHost = Host(region + "Terrain");
                if (st)
                {
                    var terrain = terrainHost.AddComponent<TidalTerrain>();
                    StPetersBuilder.ConfigureTidalTerrain(terrain);
                    GameServices.TidalTerrain = terrain;
                }
                else
                {
                    var terrain = terrainHost.AddComponent<MainlandTidalTerrain>();
                    NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);
                    GameServices.TidalTerrain = terrain;
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
                {
                    var bed = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>("Assets/_Project/Data/Terrain/StPetersSeabed.asset");
                    Assert.IsNotNull(bed);
                    surface.ConfigurePaintedHeightMap(bed.HeightTexture, bed.WorldCenter,
                        bed.WorldSize, bed.MinElevation, bed.MaxElevation);
                }
                sea.SetActive(true);
                var boat = Host("CapeIslander");
                boat.transform.position = new Vector3(at.x - 9, at.y - 9, 0);
                BoatHullSkinner.Apply(boat, AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                    "Assets/_Project/Data/Boats/Visuals/CapeIslanderIso.asset"), null,
                    new BoatHullSkinner.Options { SkipOars = true });
                _camera.transform.position = new Vector3(at.x, at.y, -100);
                var diagnostic = Host("CrestGauge");
                var dsr = diagnostic.AddComponent<SpriteRenderer>();
                dsr.sprite = sr.sprite;
                dsr.sharedMaterial = gauge;
                dsr.sortingOrder = 30000;
                diagnostic.transform.position = new Vector3(at.x, at.y, 0);
                WaterSceneTemplate.ConfigureSeaPlane(dsr, new Vector2(Width, Width));
                diagnostic.SetActive(false);
                foreach (bool storm in new[] { false, true })
                foreach (bool gust in new[] { false, true })
                {
                    wind.Storm = storm; wind.Gust = gust;
                    string mode = storm ? "storm" : "base", change = gust ? "gust" : "veer";
                    WaveTrains prior = default;
                    for (int frame = 0; frame <= 24; frame++)
                    {
                        int elapsed = frame * 5;
                        clock.TotalSeconds = Start + elapsed;
                        yield return null;
                        yield return null;
                        bridge.SendMessage("Update");
                        typeof(WaterSurface).GetMethod("PushUniforms",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                            null, Type.EmptyTypes, null).Invoke(surface, null);
                        Shader.SetGlobalVector("_DayNightTint", Color.white);
                        Shader.SetGlobalFloat("_SunElevation", 1);
                        Shader.SetGlobalVector("_SunDir", new Vector4(.4f, .8f, 0, 0));
                        var field = GameServices.WaveField;
                        var trains = new WaveFieldAnimator().Tick(clock.TotalSeconds, wind, in field);
                        var w = wind.WaveWindAt(clock.TotalSeconds).Wind;
                        float slide = frame == 0 ? 0 : Mathf.Abs(Vector2.SignedAngle(prior[0].Direction,
                            trains[0].Direction)) * Mathf.Deg2Rad * at.magnitude / 5;
                        string stem = region + "_" + mode + "_" + change + "_" + frame.ToString("D2");
                        string caption = F($"{region}; {mode}; {change}; seed {Seed}; frame {frame}; {elapsed} s of play / {elapsed * .8:F1} in-game minutes; wave clock {12 + elapsed / 75.0:F4} h; noon lighting, clear; wind ({w.x:F3},{w.y:F3}) m/s; turn-only crest slide {slide:F6} world m/s of play at {at.magnitude:F1} m from (0,0).");
                        Capture(read, Path.Combine(folder, stem), false);
                        diagnostic.SetActive(true);
                        Capture(read, Path.Combine(folder, stem + "_crests"), true);
                        diagnostic.SetActive(false);
                        File.WriteAllText(Path.Combine(folder, stem + ".txt"), caption);
                        html.Append("<figure><img width='768' src='").Append(stem)
                            .Append(".png'><figcaption>").Append(caption).Append("</figcaption></figure>\n");
                        for (int slot = 0; slot < trains.Count; slot++)
                        {
                            var tr = trains[slot];
                            rows.Append(F($"{region},{mode},{change},{frame},{elapsed},{12 + elapsed / 75.0:R},{at.magnitude:R},{w.x:R},{w.y:R},{slot},{tr.Direction.x:R},{tr.Direction.y:R},{tr.Wavelength:R},{tr.PhaseOffset:R},{tr.Amplitude:R},{slide:R}\n"));
                        }
                        prior = trains;
                    }
                }
                Object.DestroyImmediate(diagnostic);
                Object.DestroyImmediate(boat);
                Object.DestroyImmediate(sea);
                Object.DestroyImmediate(terrainHost);
            }
            File.WriteAllText(Path.Combine(folder, "trains.csv"), rows.ToString());
            File.WriteAllText(Path.Combine(folder, "index.html"), html.ToString());
            Debug.Log("[wave-memory-plates] " + folder);
#else
            Assert.Ignore("SKIPPED: plate assets need the editor.");
            yield break;
#endif
        }

        static string F(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

        void Capture(Texture2D read, string stem, bool gauge)
        {
            _camera.Render();
            _camera.Render();
            var prior = RenderTexture.active;
            RenderTexture.active = _target;
            read.ReadPixels(new Rect(0, 0, Pixels, Pixels), 0, 0);
            read.Apply();
            RenderTexture.active = prior;
            if (gauge) File.WriteAllBytes(stem + ".rgba", read.GetRawTextureData());
            var png = new Texture2D(Pixels, Pixels, TextureFormat.RGB24, false);
            var colors = read.GetPixels();
            if (!gauge && QualitySettings.activeColorSpace == ColorSpace.Linear)
                for (int i = 0; i < colors.Length; i++) colors[i] = colors[i].gamma;
            png.SetPixels(colors); png.Apply();
            File.WriteAllBytes(stem + ".png", png.EncodeToPNG());
            Object.DestroyImmediate(png);
        }

#if UNITY_EDITOR
        Shader MakeGaugeShader()
        {
            // Retain the shipped vertex path and wave function. RG encode the longest
            // train's crest phase; B encodes full-field height as .5 + height / 8.
            // This measurement pass removes paint/noise ambiguity, with no asset writes.
            string text = File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursWater.shader");
            text = text.Replace("Shader \"HiddenHarbours/Water\"", "Shader \"HiddenHarbours/WaveMemoryPlate\"");
            string marker = "half4 frag(Varyings IN) : SV_Target";
            int begin = text.IndexOf('{', text.IndexOf(marker, StringComparison.Ordinal));
            text = text.Insert(begin + 1, @"
                {
                float theta = _WaveTrain0.z * dot(_WaveTrain0.xy, IN.worldXY) + _WavePhases.x;
                float h, crest, primary; float2 slope;
                WaveFieldSample(IN.worldXY, 1, 1, h, slope, crest, primary);
                return half4(.5 + .5 * sin(theta), .5 + .5 * cos(theta), .5 + h / 8, 1);
                }
            ");
            var shader = Keep(ShaderUtil.CreateShaderAsset(text));
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "Crest gauge shader must compile.");
            return shader;
        }
#endif
    }
}
