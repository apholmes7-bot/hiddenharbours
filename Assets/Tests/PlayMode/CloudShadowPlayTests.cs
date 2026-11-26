#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>GPU guards for the cloud shade: off is exactly nothing, and the pattern is anchored to the
    /// world (a camera pan shifts it, a zoom released returns it). Both skip on the Null graphics device.</summary>
    public sealed class CloudShadowPlayTests
    {
        readonly HashSet<GameObject> _residents = new HashSet<GameObject>();
        readonly List<GameObject> _sleeping = new List<GameObject>();
        readonly Dictionary<string, Vector4> _globals = new Dictionary<string, Vector4>();
        WharfNightStage _stage;
        CloudShadowSystem _system, _original;
        CloudShadowFrame? _originalFrame;
        bool _originalEnabled, _originalCasting, _ignore, _title, _intro;
        IGameClock _clock;
        IEnvironmentService _environment;
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

        /// <summary>A steady wind law for the guards.</summary>
        sealed class SteadyWind : IWaveWindSource
        {
            public Vector2 Wind;
            public WaveWindSample WaveWindAt(double t) => new WaveWindSample(Wind, 0);
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
            _stage = null; _camera = null; _follow = null; _pixel = null; _original = null;
            Time.timeScale = _timeScale; LogAssert.ignoreFailingMessages = _ignore; yield return null;
        }

        void CreateSystem()
        {
            var def = Resources.Load<CloudShadowDef>(nameof(CloudShadowDef)); Assert.That(def, Is.Not.Null); Assert.That(def.IsValid(), Is.True);
            _system = new GameObject("CloudProofSystem").AddComponent<CloudShadowSystem>(); _system.Configure(def);
            _system.SetOutdoors(1);
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

        [UnityTest] public IEnumerator Off_IsZeroPixelsAgainstTheSystemAbsent()
        {
            WharfNightStage.RequireAGraphicsDevice(); yield return Load("StPeters");
            Target(new Vector2(203, 2.649f), 796, 448);
            _system.HeldFrame = new CloudShadowFrame(123, 1000, new SteadyWind { Wind = Vector2.right }, .5f, 1);
            // The first render after re-targeting the camera settles per-render state elsewhere in the scene
            // (it differs from every later render with or without this system), so it is thrown away.
            _system.SetCasting(false); Capture(); byte[] off = Capture(); Assert.That(_system.ShadowRenderer, Is.Null);
            Object.DestroyImmediate(_system.gameObject); _system = null;
            byte[] absent = Capture(); Assert.That(Changed(off, absent), Is.Zero, "OFF equals no system in the same frozen frame");
        }

        [UnityTest] public IEnumerator WorldAnchoring_HoldsAcrossCameraPanAndZoomRelease()
        {
            WharfNightStage.RequireAGraphicsDevice(); Time.timeScale = 0;
            _syntheticCamera = new GameObject("CloudWorldAnchorCamera"); _camera = _syntheticCamera.AddComponent<Camera>();
            _camera.orthographic = true; _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = Color.white;
            _camera.transform.position = new Vector3(-2000, -2000, -10);
            CreateSystem(); Target(new Vector2(-2000, -2000), 512, 512);
            _system.HeldFrame = new CloudShadowFrame(12, 1000, new SteadyWind { Wind = new Vector2(3, 4) }, .5f, 1);
            _system.SetCasting(false); byte[] clear = Capture(); _system.SetCasting(true);
            byte[] before = Capture(); Assert.That(Changed(clear, before), Is.GreaterThan(0), "the anchoring guard must see real shade");
            var pan = new Vector3(1, 1, 0);
            _camera.transform.position += pan; byte[] moved = Capture();
            int bad = 0;
            for (int y = 0; y < 480; y++) for (int x = 0; x < 480; x++) for (int c = 0; c < 3; c++)
                if (Math.Abs(moved[(y * 512 + x) * 4 + c] - before[((y + 32) * 512 + x + 32) * 4 + c]) > 1) bad++;
            Assert.That(bad, Is.Zero, "a one-metre pan right and up shifts the same world pattern exactly 32 pixels on both axes, allowing one byte for roundoff");
            _camera.orthographicSize *= 1.1f; Capture(); _camera.orthographicSize /= 1.1f;
            _camera.transform.position -= pan;
            Assert.That(Changed(before, Capture()), Is.Zero, "releasing a zoom returns the same pattern, without reseeding");
            yield return null;
        }
    }
}
#endif
