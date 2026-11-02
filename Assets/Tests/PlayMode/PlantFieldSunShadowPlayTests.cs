#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class PlantFieldSunShadowPlayTests
    {
        const string Output = "artifacts/shadows/c";
        readonly HashSet<GameObject> _residents = new HashSet<GameObject>();
        readonly List<GameObject> _sleeping = new List<GameObject>();
        readonly Dictionary<string, Vector4> _globals = new Dictionary<string, Vector4>();
        WharfNightStage _stage;
        PlantField _field;
        Camera _camera;
        Behaviour _follow, _pixel;
        bool _followEnabled, _pixelEnabled, _cameraHeld, _ignore, _intro, _title;
        float _scale, _cameraAspect, _cameraSize;
        Vector3 _cameraPosition;
        RenderTexture _target, _previousTarget;
        IEnvironmentService _environment;
        IGameClock _clock;
        bool _clockCaptured, _clockPaused;
        double _clockSeconds;
        float _clockScale;
        GameConfig _config;
        ISaveService _save;
        ClearMood _mood;

        sealed class ClearMood : IEnvironmentService
        {
            public readonly IEnvironmentService Inner;
            public ClearMood(IEnvironmentService inner) { Inner = inner; }
            public int WorldSeed => Inner.WorldSeed;
            public TideProfile ActiveTideProfile { get => Inner.ActiveTideProfile; set => Inner.ActiveTideProfile = value; }
            public EnvironmentSample Sample()
            {
                var sample = Inner.Sample();
                return new EnvironmentSample(sample.WindVector, sample.CurrentVector, sample.TideHeight,
                    (SeaState)Mathf.FloorToInt(0.20f * (int)SeaState.Storm), 1, 0.20f);
            }
            public float TideHeightAt(double time) => Inner.TideHeightAt(time);
            public float WaterLevelAt(double time) => Inner.WaterLevelAt(time);
        }

        static GameObject[] PersistentRoots()
        {
            var probe = new GameObject("PlantShadowLifetimeProbe"); Object.DontDestroyOnLoad(probe);
            var scene = probe.scene; Object.DestroyImmediate(probe); return scene.GetRootGameObjects();
        }

        [SetUp] public void SetUp()
        {
            _scale = Time.timeScale; _ignore = LogAssert.ignoreFailingMessages;
            _environment = GameServices.Environment; _clock = GameServices.Clock;
            _clockCaptured = _clock != null && (!(_clock is Object clockObject) || clockObject != null);
            if (_clockCaptured) { _clockSeconds = _clock.TotalSeconds; _clockScale = _clock.TimeScale; _clockPaused = _clock.IsPaused; }
            _config = GameServices.Config; _save = GameServices.Save; _intro = GameServices.OpeningCinematicRunning;
            _title = ShellFlow.AtTitle;
            _residents.Clear(); foreach (var root in PersistentRoots()) _residents.Add(root);
            _globals.Clear();
            foreach (string name in new[] { "_SunDir", "_WindWorld" }) _globals[name] = Shader.GetGlobalVector(name);
            _globals["_DayNightTint"] = Shader.GetGlobalColor("_DayNightTint");
            foreach (string name in new[] { "_SunElevation", "_ShadowStrength", "_FoliageSnow" }) _globals[name] = new Vector4(Shader.GetGlobalFloat(name), 0, 0, 0);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (_cameraHeld && _camera != null)
            {
                _camera.targetTexture = _previousTarget; _camera.aspect = _cameraAspect; _camera.orthographicSize = _cameraSize;
                _camera.transform.position = _cameraPosition;
                if (_follow != null) _follow.enabled = _followEnabled;
                if (_pixel != null) _pixel.enabled = _pixelEnabled;
            }
            if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); }
            if (_mood != null && ReferenceEquals(GameServices.Environment, _mood)) GameServices.Environment = _mood.Inner;
            if (_clockCaptured && (!(_clock is Object clockObjectBefore) || clockObjectBefore != null))
            { _clock.SeekTo(_clockSeconds); _clock.TimeScale = _clockScale; _clock.IsPaused = _clockPaused; }
            if (_stage != null) yield return _stage.TearDown();
            if (_stage != null)
            {
                foreach (var root in PersistentRoots())
                    if (root != null && !_residents.Contains(root)) { root.SetActive(false); Object.DestroyImmediate(root); }
                GameServices.Reset(); ShellFlow.Reset();
                if (!(_clock is Object clockObject) || clockObject != null) GameServices.Clock = _clock;
                if (!(_environment is Object envObject) || envObject != null) GameServices.Environment = _environment;
                GameServices.Config = _config; GameServices.Save = _save; GameServices.OpeningCinematicRunning = _intro;
                if (_title) ShellFlow.EnterTitle();
            }
            foreach (var root in _sleeping) if (root != null) root.SetActive(true);
            _sleeping.Clear();
            foreach (string name in new[] { "_SunDir", "_WindWorld" }) Shader.SetGlobalVector(name, _globals[name]);
            Shader.SetGlobalColor("_DayNightTint", _globals["_DayNightTint"]);
            foreach (string name in new[] { "_SunElevation", "_ShadowStrength", "_FoliageSnow" }) Shader.SetGlobalFloat(name, _globals[name].x);
            _field = null; _stage = null; _camera = null; _target = null; _mood = null; _cameraHeld = false;
            LogAssert.ignoreFailingMessages = _ignore; Time.timeScale = _scale;
            yield return null;
        }

        IEnumerator Load()
        {
            Time.timeScale = 1;
            // Earlier scene fixtures may retain persistent actors. Sleep those actors for this case,
            // then restore their state; only roots introduced here are destroyed in teardown.
            foreach (var root in _residents)
                if (root != null && root.activeSelf && root.GetComponentInChildren<PersistentObject>(true) != null)
                { root.SetActive(false); _sleeping.Add(root); }
            GameServices.Clock = null; GameServices.Environment = null;
            _stage = new WharfNightStage("StPeters", "PlantFieldSunShadowProof");
            yield return _stage.Load();
            ShellFlow.EnterTitle();
            Assert.That(ShellFlow.AtTitle, Is.True, "the fixture stays at the title so the save is never writable");
            Assert.That(GameServices.Clock, Is.Not.Null); Assert.That(GameServices.Environment, Is.Not.Null);
            _mood = new ClearMood(GameServices.Environment); GameServices.Environment = _mood;
            var fields = Object.FindObjectsByType<PlantField>(FindObjectsSortMode.None)
                .Where(field => field.gameObject.scene.name == "StPeters").ToArray();
            Assert.That(fields, Has.Length.EqualTo(1)); _field = fields[0];
            Assert.That(_field.AuthoredPlantCount, Is.EqualTo(9139));
            Assert.That(_field.Definition.SunShadowStrength, Is.EqualTo(1));
        }

        IEnumerator Pin(int day, int minute)
        {
            Time.timeScale = 1;
            double time = (day + minute / 1440.0) * GameServices.Config.SecondsPerDay;
            GameServices.Clock.SeekTo(time); GameServices.Clock.TimeScale = 0;
            Assert.That(GameServices.Clock.TotalSeconds, Is.EqualTo(time).Within(0.01));
            Color previous = Shader.GetGlobalColor("_DayNightTint"); int stable = 0;
            float first = Time.realtimeSinceStartup, deadline = first + 15;
            while (true)
            {
                yield return null;
                Color tint = Shader.GetGlobalColor("_DayNightTint");
                float delta = Mathf.Abs(tint.r - previous.r) + Mathf.Abs(tint.g - previous.g) + Mathf.Abs(tint.b - previous.b);
                stable = delta < 0.0001f ? stable + 1 : 0; previous = tint;
                if (Time.realtimeSinceStartup - first >= 1.1f && stable >= 4) break;
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "the requested hour must settle before capture");
            }
            _field.RefreshAppearance();
            Assert.That(GameServices.Clock.Season, Is.EqualTo(Season.HighSummer));
            Assert.That(_field.MissingCells, Is.Zero);
        }

        [UnityTest] public IEnumerator TheField_CastsByDay()
        {
            yield return Load(); yield return Pin(44, 720);
            Assert.That(Shader.GetGlobalFloat("_SunElevation"), Is.GreaterThan(0));
            Assert.That(_field.ShadowRoot, Is.Not.Null); Assert.That(_field.ShadowRoot.activeSelf, Is.True);
            Assert.That(_field.ShadowDrawCount, Is.GreaterThan(0));
        }

        [UnityTest] public IEnumerator AtTwentyThree_TheShadowRootIsInactive()
        {
            yield return Load(); yield return Pin(44, 23 * 60);
            Assert.That(Shader.GetGlobalFloat("_SunElevation"), Is.LessThanOrEqualTo(0));
            Assert.That(_field.ShadowRoot, Is.Not.Null); Assert.That(_field.ShadowRoot.activeSelf, Is.False);
            Assert.That(_field.ShadowDrawCount, Is.Zero);
        }

        [UnityTest, Timeout(600000)] public IEnumerator ProofFrames_OffOn()
        {
            WharfNightStage.RequireAGraphicsDevice();
            if (System.Environment.GetEnvironmentVariable("HH_SHADOWS_C_PLATES") != "1") Assert.Ignore("local field plates are opt-in");
            yield return Load();
            _camera = Camera.main; Assert.That(_camera, Is.Not.Null);
            _previousTarget = _camera.targetTexture; _cameraAspect = _camera.aspect; _cameraSize = _camera.orthographicSize;
            _cameraPosition = _camera.transform.position; _cameraHeld = true;
            _follow = _camera.GetComponents<Behaviour>().FirstOrDefault(b => b.GetType().Name == "CameraFollow");
            _pixel = _camera.GetComponents<Behaviour>().FirstOrDefault(b => b.GetType().Name == "PixelPerfectCamera");
            if (_follow != null) { _followEnabled = _follow.enabled; _follow.enabled = false; }
            if (_pixel != null) { _pixelEnabled = _pixel.enabled; _pixel.enabled = false; }
            yield return Pair("ginnys-0800", 58, 118, 22.229f, 72.229f, 44, 480, false);
            yield return Pair("ginnys-1200", 58, 118, 22.229f, 72.229f, 44, 720, false);
            yield return Pair("ginnys-1700", 58, 118, 22.229f, 72.229f, 44, 1020, false);
            yield return Pair("landing-high-water-1431", 190.555f, 215.445f, -4.351f, 9.649f, 29, 871, true);
        }

        IEnumerator Pair(string name, float x0, float x1, float bottom, float top, int day, int minute, bool highWater)
        {
            yield return Pin(day, minute);
            int width = Mathf.RoundToInt((x1 - x0) * 32), height = Mathf.RoundToInt((top - bottom) * 32);
            _camera.targetTexture = null;
            if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); }
            _target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf); _target.Create();
            _camera.targetTexture = _target; _camera.orthographic = true; _camera.aspect = (float)width / height;
            _camera.orthographicSize = height / 64f;
            _camera.transform.position = new Vector3((x0 + x1) / 2, (bottom + top) / 2, _camera.transform.position.z);
            // The overlay must fit this target and camera while engine time still runs, as in P1Capture.
            float until = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < until) yield return null;
            _camera.Render();
            until = Time.realtimeSinceStartup + 60;
            while (UnityEditor.ShaderUtil.anythingCompiling)
            { yield return null; Assert.That(Time.realtimeSinceStartup, Is.LessThan(until), "plant shadow shader compilation"); }
            _field.RefreshAppearance();
            Time.timeScale = 0; yield return null;
            var shadows = _field.ShadowRoot.GetComponentsInChildren<MeshRenderer>(true);
            var enabled = shadows.Select(r => r.enabled).ToArray();
            var planes = GeometryUtility.CalculateFrustumPlanes(_camera);
            int plantCount = _field.GetComponentsInChildren<MeshRenderer>().Count(r => r.transform.parent == _field.transform && GeometryUtility.TestPlanesAABB(planes, r.bounds));
            int shadowCount = shadows.Count(r => r.enabled && r.gameObject.activeInHierarchy && GeometryUtility.TestPlanesAABB(planes, r.bounds));
            var off = new long[2]; var on = new long[2];
            byte[] before, after;
            try
            {
                foreach (var renderer in shadows) renderer.enabled = false;
                yield return null; before = Capture(width, height); yield return Counters(off);
                for (int i = 0; i < shadows.Length; i++) shadows[i].enabled = enabled[i];
                yield return null; after = Capture(width, height); yield return Counters(on);
            }
            finally { for (int i = 0; i < shadows.Length; i++) if (shadows[i] != null) shadows[i].enabled = enabled[i]; }
            int changed = 0;
            for (int i = 0; i < before.Length; i += 4)
                if (before[i] != after[i] || before[i + 1] != after[i + 1] || before[i + 2] != after[i + 2]) changed++;
            if (!highWater) Assert.That(changed, Is.GreaterThan(0), name + ": the field shade must be visible");
            double time = GameServices.Clock.TotalSeconds;
            float tide = GameServices.Environment.WaterLevelAt(time);
            if (highWater) Assert.That(tide, Is.EqualTo(1.999786f).Within(0.005f));
            Directory.CreateDirectory(Output + "/plates");
            var rgba = new byte[before.Length * 2];
            for (int y = 0; y < height; y++)
            {
                Array.Copy(before, y * width * 4, rgba, y * width * 8, width * 4);
                Array.Copy(after, y * width * 4, rgba, y * width * 8 + width * 4, width * 4);
            }
            var sheet = new Texture2D(width * 2, height, TextureFormat.RGBA32, false);
            sheet.LoadRawTextureData(rgba); sheet.Apply(); File.WriteAllBytes(Output + "/plates/compare-" + name + ".png", sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            string caption = $"OFF left | ON right; native panels {width}x{height}; day={day}; minute={minute}; HighSummer; strength=1; held clear visibility=1, sea=0.20; wind/tide/seed live.\n" +
                $"station x={x0}..{x1}, y={bottom}..{top}; tide={tide}; changedPixels={changed}; plantChunksInView={plantCount}; shadowDrawsInView OFF/ON=0/{shadowCount}; totalPlantChunks={_field.ChunkCount}; totalShadowDraws={_field.ShadowDrawCount}.\n" +
                $"Profiler draws OFF/ON={off[0]}/{on[0]}; batches={off[1]}/{on[1]}; <=0 means no positive counter reading.\n";
            File.WriteAllText(Output + "/plates/" + name + ".txt", caption); TestContext.WriteLine(caption);
        }

        byte[] Capture(int width, int height)
        {
            _camera.Render(); var previous = RenderTexture.active; RenderTexture.active = _target;
            var image = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); RenderTexture.active = previous;
            var pixels = image.GetPixels(); var result = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                var color = new Color(Mathf.Clamp01(pixels[i].r), Mathf.Clamp01(pixels[i].g), Mathf.Clamp01(pixels[i].b), 1).gamma;
                result[i * 4] = (byte)Mathf.RoundToInt(color.r * 255); result[i * 4 + 1] = (byte)Mathf.RoundToInt(color.g * 255);
                result[i * 4 + 2] = (byte)Mathf.RoundToInt(color.b * 255); result[i * 4 + 3] = 255;
            }
            Object.DestroyImmediate(image); return result;
        }

        static IEnumerator Counters(long[] values)
        {
            using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            using var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            for (int i = 0; i < 3; i++) yield return null;
            values[0] = draws.Valid ? draws.LastValue : -1; values[1] = batches.Valid ? batches.LastValue : -1;
        }
    }
}
#endif
