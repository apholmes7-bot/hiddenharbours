#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // Evidence for the South Water: the committed scene, loaded as it ships, shot at noon in two winds
    // (a light air and a gale) from two stations (the shelf inside the St Peters approach, and the deep
    // mud). The wind is a fixture: the plate shows how this sea draws in each, not what the sim says
    // today. Rain and the sky's mood are not in the frame. A Cape Islander lies at each station for scale.
    // GPU only; CI skips it. The assertions are small: the scene's own bed is the plan's, and the two
    // weathers do not draw the same sea.
    public class SouthWaterPlatePlayTests
    {
        const int Seed = 4242, Pixels = 768;
        const float Width = 48f, LightWind = 2.3f, GaleWind = 14f;
        const double Start = 900;

        static readonly MethodInfo PushUniforms = typeof(WaterSurface).GetMethod("PushUniforms",
            BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        static readonly MethodInfo SyncDisplaced = typeof(DisplacedWaterSurface).GetMethod("SyncUniforms",
            BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);

        readonly List<Object> _made = new List<Object>();
        readonly List<Camera> _cameras = new List<Camera>();
        IHullMeshPresentationService _hulls;
        RenderTexture _target;
        Camera _camera;
        Scene _scene;
        bool _started, _asyncShaders;

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
            public int WorldSeed => Seed;
            public TideProfile ActiveTideProfile { get; set; }
            public float TideHeightAt(double t) => 0f;
            public float WaterLevelAt(double t) => 0f;
            public WaveWindSample WaveWindAt(double t) =>
                new WaveWindSample(Wind, WeatherModel.SeaState01(Wind.magnitude));
            public EnvironmentSample Sample() => new EnvironmentSample(Wind, Vector2.zero, 0f,
                WeatherModel.SeaFromWind(Wind.magnitude), 1, WeatherModel.SeaState01(Wind.magnitude));
        }

        T Keep<T>(T item) where T : Object { _made.Add(item); return item; }

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED: plate capture needs a GPU.");
            _started = true;
            _asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            _hulls = HullMeshPresentation.Service;
            foreach (var c in Object.FindObjectsByType<Camera>())
                if (c.enabled) { _cameras.Add(c); c.enabled = false; }
            GameServices.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!_started) yield break;
            if (_camera != null) _camera.targetTexture = null;
            if (_target != null) _target.Release();
            for (int i = _made.Count - 1; i >= 0; i--)
                if (_made[i] != null) Object.DestroyImmediate(_made[i]);
            _made.Clear();
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
            foreach (var c in _cameras) if (c != null) c.enabled = true;
            _cameras.Clear();
            HullMeshPresentation.Service = _hulls;
            GameServices.Reset();
            ShaderUtil.allowAsyncCompilation = _asyncShaders;
            _started = false;
        }

        [UnityTest, Order(10000), Timeout(300000)]
        public IEnumerator TheOpenWater_InALightAirAndInAGale()
        {
            var clock = new PlateClock();
            var config = Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameConfig>(
                WaterSceneTemplate.GameConfigAsset)));
            var wind = new PlateWind();
            GameServices.Config = config;
            GameServices.Clock = clock;
            GameServices.Environment = wind;
            HullMeshPresentation.Service = new IsoFacetHullPresentationService();

            string path = WaterSceneTemplate.ScenePathFor(SouthWaterPlan.SceneName);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Additive));
            _scene = SceneManager.GetSceneByPath(path);
            Assert.IsTrue(_scene.IsValid() && _scene.isLoaded, path);
            yield return null;
            // The scene must not have replaced the fixture's clock or wind.
            GameServices.Config = config;
            GameServices.Clock = clock;
            GameServices.Environment = wind;

            WaterSurface surface = null;
            RectTidalTerrain bed = null;
            foreach (var root in _scene.GetRootGameObjects())
            {
                surface = surface != null ? surface : root.GetComponentInChildren<WaterSurface>();
                bed = bed != null ? bed : root.GetComponentInChildren<RectTidalTerrain>();
                foreach (var c in root.GetComponentsInChildren<Camera>())
                    if (c.enabled) { _cameras.Add(c); c.enabled = false; }
            }
            Assert.IsNotNull(surface, "the scene has a sea");
            Assert.IsNotNull(bed, "the scene has a bed");

            var stations = new (string Name, Vector2 At)[]
            {
                ("shelf", new Vector2(SouthWaterPlan.StPetersApproachX, 420f)),
                ("deep-mud", new Vector2(500f, -300f)),
            };
            foreach (var s in stations)
                Assert.AreEqual(SouthWaterPlan.ElevationAt(s.At), bed.ElevationAt(s.At), 1e-4f,
                    $"the committed scene's bed is the plan's at {s.Name}");

            var bridge = Keep(new GameObject("PlateWaveBridge")).AddComponent<WaveFieldBridge>();
            var camGo = Keep(new GameObject("PlateCamera"));
            _camera = camGo.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.orthographicSize = Width / 2;
            _camera.nearClipPlane = 1;
            _camera.farClipPlane = 600;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(.08f, .12f, .16f, 1);
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _target = Keep(new RenderTexture(Pixels, Pixels, 24, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear));
            _camera.targetTexture = _target;
            var read = Keep(new Texture2D(Pixels, Pixels, TextureFormat.RGBAFloat, false, true));

            string folder = Path.Combine(Application.temporaryCachePath, "south-water-plates");
            Directory.CreateDirectory(folder);
            var toward = new Vector2(1f, 0.35f).normalized;
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                "Assets/_Project/Data/Boats/Visuals/CapeIslanderIso.asset");

            foreach (var s in stations)
            {
                var boat = Keep(new GameObject("CapeIslander_" + s.Name));
                boat.transform.position = new Vector3(s.At.x, s.At.y, 0);
                BoatHullSkinner.Apply(boat, visual, null, new BoatHullSkinner.Options { SkipOars = true });
                _camera.transform.position = new Vector3(s.At.x, s.At.y, -100);
                byte[] light = null;
                foreach (float speed in new[] { LightWind, GaleWind })
                {
                    wind.Wind = toward * speed;
                    yield return null;
                    yield return null;
                    bridge.SendMessage("Update");
                    PushUniforms.Invoke(surface, null);
                    var displaced = surface.GetComponent<DisplacedWaterSurface>();
                    if (displaced != null && displaced.Displaced)
                        SyncDisplaced.Invoke(displaced, new object[] { false });
                    Shader.SetGlobalVector("_DayNightTint", Color.white);
                    Shader.SetGlobalFloat("_SunElevation", 1);
                    Shader.SetGlobalVector("_SunDir", new Vector4(.4f, .8f, 0, 0));
                    string mode = speed == LightWind ? "light-air" : "gale";
                    byte[] data = Shoot(read, Path.Combine(folder, $"south-water-{s.Name}-{mode}.png"));
                    if (light == null) light = data;
                    else Assert.IsFalse(Same(light, data), $"{s.Name}: a gale draws the same sea as a light air");
                }
                boat.SetActive(false);
            }
            Debug.Log("[south-water] plates written to " + folder);
        }

        byte[] Shoot(Texture2D read, string file)
        {
            _camera.Render();
            var prior = RenderTexture.active;
            RenderTexture.active = _target;
            read.ReadPixels(new Rect(0, 0, Pixels, Pixels), 0, 0);
            read.Apply();
            RenderTexture.active = prior;
            byte[] data = read.GetRawTextureData();
            var png = new Texture2D(Pixels, Pixels, TextureFormat.RGB24, false);
            var colors = read.GetPixels();
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                for (int i = 0; i < colors.Length; i++) colors[i] = colors[i].gamma;
            png.SetPixels(colors);
            png.Apply();
            File.WriteAllBytes(file, png.EncodeToPNG());
            Object.DestroyImmediate(png);
            return data;
        }

        static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
#endif
