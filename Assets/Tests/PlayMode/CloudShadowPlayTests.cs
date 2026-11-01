#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>Run this GPU class alone in a fresh editor. Captures freeze both clocks.</summary>
    public sealed class CloudShadowPlayTests
    {
        const string Output = "artifacts/shadows/c3";
        readonly HashSet<GameObject> _residents = new HashSet<GameObject>();
        readonly List<GameObject> _sleeping = new List<GameObject>();
        readonly Dictionary<string, Vector4> _globals = new Dictionary<string, Vector4>();
        WharfNightStage _stage;
        CloudShadowSystem _system, _original;
        CloudShadowFrame? _originalFrame;
        bool _originalEnabled, _originalCasting, _ignore, _title, _intro;
        IGameClock _clock;
        IEnvironmentService _environment;
        CloudMood _mood;
        GameConfig _config;
        ISaveService _save;
        double _seconds;
        float _clockScale, _timeScale;
        bool _paused, _clockLive;
        Camera _camera;
        GameObject _syntheticCamera;
        RenderTexture _target, _oldTarget;
        Behaviour _follow, _pixel;
        bool _followEnabled, _pixelEnabled;
        float _size, _aspect;
        Vector3 _position;

        sealed class CloudMood : IEnvironmentService
        {
            public readonly IEnvironmentService Inner;
            readonly DayNightProfile _profile;
            public float Overcast;
            public CloudMood(IEnvironmentService inner)
            { Inner = inner; _profile = Resources.Load<DayNightProfile>("DayNightProfile"); }
            public int WorldSeed => Inner.WorldSeed;
            public TideProfile ActiveTideProfile { get => Inner.ActiveTideProfile; set => Inner.ActiveTideProfile = value; }
            public EnvironmentSample Sample()
            {
                var s = Inner.Sample();
                float visibility = Mathf.Lerp(1, _profile.FogVisibilityForFullDim, Overcast);
                return new EnvironmentSample(s.WindVector, s.CurrentVector, s.TideHeight,
                    (SeaState)Mathf.FloorToInt(.2f * (int)SeaState.Storm), visibility, .2f);
            }
            public float TideHeightAt(double seconds) => Inner.TideHeightAt(seconds);
            public float WaterLevelAt(double seconds) => Inner.WaterLevelAt(seconds);
        }

        static GameObject[] Roots()
        {
            var probe = new GameObject("CloudLifetimeProbe"); Object.DontDestroyOnLoad(probe);
            var scene = probe.scene; Object.DestroyImmediate(probe); return scene.GetRootGameObjects();
        }

        [SetUp] public void SetUp()
        {
            _timeScale = Time.timeScale; _ignore = LogAssert.ignoreFailingMessages;
            _clock = GameServices.Clock; _environment = GameServices.Environment; _config = GameServices.Config; _save = GameServices.Save;
            _title = ShellFlow.AtTitle; _intro = GameServices.OpeningCinematicRunning;
            _clockLive = _clock != null && (!(_clock is Object clockObject) || clockObject != null);
            if (_clockLive) { _seconds = _clock.TotalSeconds; _clockScale = _clock.TimeScale; _paused = _clock.IsPaused; }
            _residents.Clear(); foreach (var root in Roots()) _residents.Add(root);
            foreach (string name in new[] { "_SunDir", "_WindWorld" }) _globals[name] = Shader.GetGlobalVector(name);
            _globals["_DayNightTint"] = Shader.GetGlobalColor("_DayNightTint");
            foreach (string name in new[] { "_SunElevation", "_ShadowStrength", "_FoliageSnow" }) _globals[name] = new Vector4(Shader.GetGlobalFloat(name), 0, 0, 0);
            _original = CloudShadowSystem.Instance;
            if (_original != null)
            {
                _originalEnabled = _original.enabled; _originalCasting = _original.Casting; _originalFrame = _original.HeldFrame;
                _original.SetCasting(false); _original.enabled = false;
            }
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            RestoreCamera();
            if (_system != null) Object.DestroyImmediate(_system.gameObject); _system = null;
            if (_syntheticCamera != null) Object.DestroyImmediate(_syntheticCamera); _syntheticCamera = null;
            if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); } _target = null;
            if (_clockLive && (!(_clock is Object clockObject) || clockObject != null))
            { _clock.SeekTo(_seconds); _clock.TimeScale = _clockScale; _clock.IsPaused = _paused; }
            if (_mood != null && ReferenceEquals(GameServices.Environment, _mood)) GameServices.Environment = _mood.Inner;
            if (_stage != null)
            {
                yield return _stage.TearDown();
                foreach (var root in Roots()) if (root != null && !_residents.Contains(root)) { root.SetActive(false); Object.DestroyImmediate(root); }
                GameServices.Reset(); ShellFlow.Reset();
                if (!(_clock is Object previousClock) || previousClock != null) GameServices.Clock = _clock;
                if (!(_environment is Object previousEnvironment) || previousEnvironment != null) GameServices.Environment = _environment;
                GameServices.Config = _config; GameServices.Save = _save; GameServices.OpeningCinematicRunning = _intro;
                if (_title) ShellFlow.EnterTitle();
            }
            foreach (var root in _sleeping) if (root != null) root.SetActive(true); _sleeping.Clear();
            if (_original != null) { _original.HeldFrame = _originalFrame; _original.SetCasting(_originalCasting); _original.enabled = _originalEnabled; }
            foreach (string name in new[] { "_SunDir", "_WindWorld" }) Shader.SetGlobalVector(name, _globals[name]);
            Shader.SetGlobalColor("_DayNightTint", _globals["_DayNightTint"]);
            foreach (string name in new[] { "_SunElevation", "_ShadowStrength", "_FoliageSnow" }) Shader.SetGlobalFloat(name, _globals[name].x);
            _stage = null; _camera = null; _follow = null; _pixel = null; _original = null; _mood = null;
            Time.timeScale = _timeScale; LogAssert.ignoreFailingMessages = _ignore; yield return null;
        }

        void CreateSystem()
        {
            var def = Resources.Load<CloudShadowDef>(nameof(CloudShadowDef)); Assert.That(def, Is.Not.Null); Assert.That(def.IsValid(), Is.True);
            _system = new GameObject("CloudProofSystem").AddComponent<CloudShadowSystem>(); _system.Configure(def);
        }

        IEnumerator Load(string scene)
        {
            Time.timeScale = 1;
            // Earlier fixtures can retain actors. Sleep them for this class, then restore them; never delete a resident.
            foreach (var root in _residents)
                if (root != null && root.activeSelf && root.GetComponentInChildren<PersistentObject>(true) != null)
                { root.SetActive(false); _sleeping.Add(root); }
            GameServices.Clock = null; GameServices.Environment = null;
            _stage = new WharfNightStage(scene, "CloudShadowProof"); yield return _stage.Load();
            ShellFlow.EnterTitle(); Assert.That(ShellFlow.AtTitle, Is.True, "the capture never enables save writes");
            _mood = new CloudMood(GameServices.Environment); GameServices.Environment = _mood;
            _camera = Camera.main; Assert.That(_camera, Is.Not.Null);
            _oldTarget = _camera.targetTexture; _size = _camera.orthographicSize; _aspect = _camera.aspect; _position = _camera.transform.position;
            _follow = _camera.GetComponents<Behaviour>().FirstOrDefault(b => b.GetType().Name == "CameraFollow");
            _pixel = _camera.GetComponents<Behaviour>().FirstOrDefault(b => b.GetType().Name == "PixelPerfectCamera");
            if (_follow != null) { _followEnabled = _follow.enabled; _follow.enabled = false; }
            if (_pixel != null) { _pixelEnabled = _pixel.enabled; _pixel.enabled = false; }
            CreateSystem();
        }

        void RestoreCamera()
        {
            if (_camera == null || _syntheticCamera != null) return;
            _camera.targetTexture = _oldTarget; _camera.orthographicSize = _size; _camera.aspect = _aspect; _camera.transform.position = _position;
            if (_follow != null) _follow.enabled = _followEnabled;
            if (_pixel != null) _pixel.enabled = _pixelEnabled;
        }

        void Target(Vector2 centre, int width, int height)
        {
            _camera.targetTexture = null;
            if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); }
            _target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf); _target.Create();
            _camera.targetTexture = _target; _camera.aspect = (float)width / height; _camera.orthographicSize = height / 64f;
            _camera.transform.position = new Vector3(centre.x, centre.y, _camera.transform.position.z);
        }

        IEnumerator Pin(int minute)
        {
            Time.timeScale = 1;
            var clock = GameServices.Clock; clock.SeekTo((44 + minute / 1440.0) * GameServices.Config.SecondsPerDay); clock.TimeScale = 0;
            float first = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - first < 1.2f) yield return null;
            Assert.That(clock.TotalSeconds, Is.EqualTo((44 + minute / 1440.0) * GameServices.Config.SecondsPerDay).Within(.01));
            Time.timeScale = 0; yield return null;
        }

        byte[] Capture()
        {
            _camera.Render(); var previous = RenderTexture.active; RenderTexture.active = _target;
            var image = new Texture2D(_target.width, _target.height, TextureFormat.RGBAFloat, false, true);
            image.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0); image.Apply(); RenderTexture.active = previous;
            var pixels = image.GetPixels(); var bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = new Color(Mathf.Clamp01(pixels[i].r), Mathf.Clamp01(pixels[i].g), Mathf.Clamp01(pixels[i].b), 1).gamma;
                bytes[i * 4] = (byte)Mathf.RoundToInt(c.r * 255); bytes[i * 4 + 1] = (byte)Mathf.RoundToInt(c.g * 255);
                bytes[i * 4 + 2] = (byte)Mathf.RoundToInt(c.b * 255); bytes[i * 4 + 3] = 255;
            }
            Object.DestroyImmediate(image); return bytes;
        }

        static int Changed(byte[] a, byte[] b)
        { int n = 0; for (int i = 0; i < a.Length; i += 4) if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2]) n++; return n; }

        void Write(string name, byte[] off, byte[] on, string caption)
        {
            int width = _target.width, height = _target.height;
            var pixels = new byte[off.Length * 2];
            for (int y = 0; y < height; y++)
            { Array.Copy(off, y * width * 4, pixels, y * width * 8, width * 4); Array.Copy(on, y * width * 4, pixels, y * width * 8 + width * 4, width * 4); }
            var image = new Texture2D(width * 2, height, TextureFormat.RGBA32, false); image.LoadRawTextureData(pixels); image.Apply();
            Directory.CreateDirectory(Output + "/plates"); File.WriteAllBytes(Output + "/plates/compare-" + name + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image);
            caption = $"OFF left | ON right; native panels {width}x{height}; station {_camera.transform.position}; changedPixels={Changed(off, on)}.\n" + caption;
            File.WriteAllText(Output + "/plates/" + name + ".txt", caption); TestContext.WriteLine(caption);
        }

        [UnityTest] public IEnumerator Off_IsZeroPixelsAgainstTheSystemAbsent()
        {
            WharfNightStage.RequireAGraphicsDevice(); yield return Load("StPeters");
            Target(new Vector2(203, 2.649f), 796, 448); yield return Pin(720);
            _system.HeldFrame = new CloudShadowFrame(123, 0, Vector2.right, .5f, 1);
            _system.SetCasting(false); byte[] off = Capture(); Assert.That(_system.ShadowRenderer, Is.Null);
            Object.DestroyImmediate(_system.gameObject); _system = null;
            byte[] absent = Capture(); Assert.That(Changed(off, absent), Is.Zero, "OFF equals no system in the same frozen frame");
            Write("off-against-absent", absent, off, "OFF against absent, same frame, 0 px required.");
        }

        [UnityTest] public IEnumerator WorldAnchoring_HoldsAcrossCameraPanAndZoomRelease()
        {
            WharfNightStage.RequireAGraphicsDevice(); Time.timeScale = 0;
            _syntheticCamera = new GameObject("CloudWorldAnchorCamera"); _camera = _syntheticCamera.AddComponent<Camera>();
            _camera.orthographic = true; _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = Color.white;
            _camera.transform.position = new Vector3(-2000, -2000, -10);
            CreateSystem(); Target(new Vector2(-2000, -2000), 512, 512);
            _system.HeldFrame = new CloudShadowFrame(12, 0, new Vector2(3, 4), .5f, 1);
            _system.SetCasting(false); byte[] clear = Capture(); _system.SetCasting(true);
            byte[] before = Capture(); Assert.That(Changed(clear, before), Is.GreaterThan(0), "the anchoring guard must see real shade");
            _camera.transform.position += Vector3.right; byte[] moved = Capture();
            int bad = 0;
            for (int y = 0; y < 512; y++) for (int x = 0; x < 480; x++) for (int c = 0; c < 3; c++)
                if (Math.Abs(moved[(y * 512 + x) * 4 + c] - before[(y * 512 + x + 32) * 4 + c]) > 1) bad++;
            Assert.That(bad, Is.Zero, "a one-metre camera pan shifts the same world pattern exactly 32 pixels, allowing one byte for roundoff");
            _camera.orthographicSize *= 1.1f; Capture(); _camera.orthographicSize /= 1.1f;
            _camera.transform.position -= Vector3.right;
            Assert.That(Changed(before, Capture()), Is.Zero, "releasing a zoom returns the same pattern, without reseeding");
            yield return null;
        }

        [UnityTest, Timeout(600000)] public IEnumerator ProofFrames_WeatherWindAndFrameCost()
        {
            WharfNightStage.RequireAGraphicsDevice();
            if (System.Environment.GetEnvironmentVariable("HH_CLOUD_SHADOW_PLATES") != "1") Assert.Ignore("local cloud plates are opt-in; run this class alone in a fresh editor");
            yield return Load("StPeters"); Target(new Vector2(203, 2.649f), 796, 448);
            yield return Plate("stp-noon-clear", 720, 0, 0);
            yield return FrameCost();
            yield return Plate("stp-noon-motion-20", 720, 0, 20);
            yield return Plate("stp-noon-motion-40", 720, 0, 40);
            yield return Plate("stp-1700", 1020, 0, 0);
            yield return Plate("stp-noon-broken", 720, .5f, 0);
            yield return Plate("stp-noon-overcast", 720, 1, 0);
            yield return Plate("stp-noon-clear-sky", 720, 0, 0);
            yield return Plate("stp-night", 23 * 60, 0, 0);
            RestoreCamera(); if (_system != null) Object.DestroyImmediate(_system.gameObject); _system = null;
            if (ReferenceEquals(GameServices.Environment, _mood)) GameServices.Environment = _mood.Inner;
            yield return _stage.TearDown(); _stage = null;
            yield return Load("NineMileCreek");
            var wall = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .FirstOrDefault(x => x.gameObject.scene.name == "NineMileCreek" && x.name == "NorthWall");
            Assert.That(wall, Is.Not.Null, "read the quay station from the loaded scene");
            Target(new Vector2(wall.bounds.center.x, wall.bounds.center.y), 1280, 720);
            yield return Plate("nmc-wharf-noon", 720, 0, 0);
        }

        IEnumerator Plate(string name, int minute, float overcast, double elapsed)
        {
            _mood.Overcast = overcast;
            yield return Pin(minute);
            var environment = GameServices.Environment; var sample = environment.Sample();
            Assert.That(CloudShadowMath.Overcast(sample.Visibility, sample.SeaState01, Resources.Load<DayNightProfile>("DayNightProfile")),
                Is.EqualTo(overcast).Within(.0001), "the world's lighting and cloud curve see the same held weather signal");
            Assert.That(sample.WindVector.sqrMagnitude, Is.GreaterThan(0), "motion plates name the real downwind vector");
            double seconds = GameServices.Clock.TotalSeconds + elapsed;
            float elevation = Shader.GetGlobalFloat("_SunElevation");
            _system.HeldFrame = new CloudShadowFrame(environment.WorldSeed, seconds, sample.WindVector, overcast, elevation);
            _system.SetCasting(false); byte[] off = Capture();
            _system.SetCasting(true); byte[] on = Capture(); int changed = Changed(off, on);
            if (minute == 23 * 60 || overcast == 1) Assert.That(changed, Is.Zero, "night/full overcast is exact passthrough");
            else Assert.That(changed, Is.GreaterThan(0), name + " must visibly shade the wharf and water");
            Write(name, off, on, $"day 44, minute {minute}, normalized overcast {overcast}, game seconds {seconds:F3}, elapsed {elapsed}; wind DOWNWIND {sample.WindVector}; " +
                $"offset metres {_system.Offsets}; expected primary displacement over 20 game seconds {sample.WindVector * (_system.Definition.DriftMetresPerGameSecondPerMps * 20)}; " +
                $"owned draws OFF/ON=0/{_system.DrawCount}; strength {_system.Definition.Strength.Evaluate(overcast)}. Open cutaways receive the same world multiply; review any visible indoor issue.\n");
        }

        IEnumerator FrameCost()
        {
            const int count = 40;
            var off = new double[count]; var on = new double[count]; var watch = new Stopwatch();
            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            int offDraws = -1, onDraws = -1;
            try
            {
                for (int arm = 0; arm < 2; arm++)
                {
                    _system.SetCasting(arm != 0); for (int warm = 0; warm < 5; warm++) yield return null;
                    int draws = UnityEditor.UnityStats.drawCalls;
                    if (arm == 0) offDraws = draws; else onDraws = draws;
                    for (int i = 0; i < count; i++)
                    {
                        watch.Restart(); _camera.Render(); RenderTexture.active = _target;
                        pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); watch.Stop();
                        (arm == 0 ? off : on)[i] = watch.Elapsed.TotalMilliseconds; yield return null;
                    }
                }
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(pixel); }
            Array.Sort(off); Array.Sort(on);
            string report = $"St Peters noon, {count} held-frame samples per arm, after 5 warm frames. OFF p50={off[count / 2]:F3} ms p95={off[(int)Math.Ceiling(count * .95) - 1]:F3}; " +
                $"ON p50={on[count / 2]:F3} ms p95={on[(int)Math.Ceiling(count * .95) - 1]:F3}. Wider bracket: Camera.Render plus synchronous one-pixel GPU readback, driver and loaded-box scheduling; not isolated shader GPU time or gameplay fps.\n" +
                $"Renderer stats draws OFF/ON={offDraws}/{onDraws}; owned renderers/draws OFF/ON=0/1. Nonpositive stats mean unavailable and require a frame-debugger reading before the budget is signed off.\n";
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/frame-cost.txt", report); TestContext.WriteLine(report);
        }
    }
}
#endif
