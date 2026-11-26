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
    /// <b>THE CLIFF MIX, BEFORE AND AFTER, IN ONE RUN</b> (owner 11 25: <i>"a mix of our current cliffs and the
    /// boards cliffs for variance"</i>). St Peters is loaded once with its mix built; every frame is shot twice
    /// at the same hour through the play camera, once with the mix's second palette switched off on every band
    /// (<c>_VariantOn</c> 0 on its property block: exactly the look on main) and once on. So a pair differs by the
    /// mix and nothing else.
    ///
    /// <para><b>The frames.</b> Seven St Peters cliff stretches (the plate stations the px look was judged at, and
    /// the two ledges) at 13:00 and 20:30, at the on-foot framing; four of them again at twice that height for the
    /// run of the coast. Plates land in <c>HH_CLIFF_MIX_PLATES</c> if the launch shell sets it, else
    /// <c>Application.temporaryCachePath/CliffFaceMixPlates</c>.</para>
    ///
    /// <para><b>Explicit</b>: a picture for the owner's eye, not a gate. ⚠ No GPU on CI: it skips there before it
    /// loads anything.</para>
    /// </summary>
    [Explicit("Owner plates: run by name on a box with a GPU.")]
    public class CliffFaceMixPlatesPlayTests
    {
        const string StPeters = "StPeters";
        const string CleanupSceneName = "CliffFaceMixCleanup";
        const string PlateDirName = "CliffFaceMixPlates";
        const string PlateEnv = "HH_CLIFF_MIX_PLATES";
        const int PlateWidthPx = 1920;
        const int PlateHeightPx = CameraFollow.DesignScreenHeightPx;
        const float WideFactor = 2f;
        const float Noon = 13f, Dusk = 20.5f;

        static readonly int IdVariantOn = Shader.PropertyToID("_VariantOn");

        static readonly (string shot, Vector2 at, bool wide)[] Frames =
        {
            ("s1-south-wall", new Vector2(72.5f, -71f), true),
            ("s3-steep-east", new Vector2(187.5f, -17.5f), true),
            ("s5-south-west", new Vector2(-39f, -31f), true),
            ("s6-south-east", new Vector2(178f, -32.5f), false),
            ("s4-ledge", new Vector2(0.5f, -58.1f), false),
            ("east-ledges", new Vector2(106f, -71f), true),
            ("west-ledges", new Vector2(-20f, -51f), false),
        };

        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        readonly List<Object> _spawned = new List<Object>();
        readonly List<MeshRenderer> _mixed = new List<MeshRenderer>();
        bool _loadedAny, _introOnEntry, _followCaptured;
        CameraFollow _follow;
        Camera _cam;
        Transform _followTargetOnEntry;
        float _followSmoothOnEntry;
        GameObject _anchor;
        RenderTexture _rt;
        MaterialPropertyBlock _mpb;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;
            if (_follow != null && _followCaptured) { _follow.Target = _followTargetOnEntry; _follow.Smooth = _followSmoothOnEntry; }
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear(); _mixed.Clear();
            _follow = null; _cam = null; _anchor = null; _followCaptured = false;
            if (!_loadedAny) yield break;

            ShellFlow.Reset();
            InteractionGate.Reset();
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
                    yield return HiddenHarbours.Tests.Support.SceneSettle.Unload(s);
            }
            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go)) Object.DestroyImmediate(go);
            GameServices.Reset();
            ShellFlow.Reset();
            InteractionGate.Reset();
            _loadedAny = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator StPeters_TheCliffMix_BeforeAndAfter_AtNoonAndDusk()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED: no graphics device, so nothing rendered.");

            _introOnEntry = GameServices.OpeningCinematicRunning;
            GameConfig configBefore = GameServices.Config;
            IGameClock clockBefore = GameServices.Clock;
            IEnvironmentService envBefore = GameServices.Environment;
            LogAssert.ignoreFailingMessages = true;
            _loadedAny = true;
            yield return HiddenHarbours.Tests.Support.SceneSettle.Load(StPeters, LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;
            if (GameServices.Config == null && configBefore != null) GameServices.Config = configBefore;
            if (GameServices.Clock == null && clockBefore != null) GameServices.Clock = clockBefore;
            if (GameServices.Environment == null && envBefore != null) GameServices.Environment = envBefore;
            for (int f = 0; f < 240 && FindPersistentFollow() == null; f++) yield return null;
            Assert.IsNotNull(FindPersistentFollow(), "no play camera to shoot through");

            CliffWallSurface[] walls = Object.FindObjectsByType<CliffWallSurface>();
            Assert.IsNotEmpty(walls, "St Peters has no cliff walls");
            int withMix = walls.Count(w => w.FaceMix != null);
            Assert.Greater(withMix, 0, "no St Peters wall was built with the cliff mix");
            _mpb = new MaterialPropertyBlock();
            foreach (CliffWallSurface w in walls)
                foreach (MeshRenderer r in w.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.GetPropertyBlock(_mpb);
                    if (_mpb.GetFloat(IdVariantOn) > 0.5f) _mixed.Add(r);
                }
            Assert.IsNotEmpty(_mixed, "no cliff band carries the mix's palette");

            var log = new StringBuilder();
            log.AppendLine($"St Peters cliff mix plates: {walls.Length} walls, {withMix} built with '{walls.First(w => w.FaceMix != null).FaceMix.Id}', " +
                           $"{_mixed.Count} band renderers carry the variant palette. Unity {Application.unityVersion}, " +
                           $"{SystemInfo.graphicsDeviceName}.");
            int changedFrames = 0;
            foreach (float hours in new[] { Noon, Dusk })
            {
                yield return PinTheHour(hours);
                foreach (var fr in Frames)
                    foreach (bool wide in fr.wide && hours == Noon ? new[] { false, true } : new[] { false })
                    {
                        yield return FrameOn(fr.at, wide);
                        string stem = $"{fr.shot}_{Hhmm(hours)}{(wide ? "_wide" : "")}";
                        SetVariant(false);
                        byte[] before = Capture();
                        SetVariant(true);
                        byte[] after = Capture();
                        SavePlate(stem + "_before.png", before);
                        SavePlate(stem + "_after.png", after);
                        double changed = ChangedShare(before, after);
                        if (changed > 0.001) changedFrames++;
                        log.AppendLine($"{stem}: camera ({_cam.transform.position.x:0.##}, {_cam.transform.position.y:0.##}), " +
                                       $"{_cam.orthographicSize * 2f:0.##} m tall; {changed:P2} of pixels changed by the mix");
                    }
            }
            File.WriteAllText(Path.Combine(PlateDir(), "plates.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[cliff-mix-plates] " + log);
            Assert.Greater(changedFrames, 0, "the mix changed no pixel in any frame: the variant palette never drew");
        }

        void SetVariant(bool on)
        {
            foreach (MeshRenderer r in _mixed)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(IdVariantOn, on ? 1f : 0f);
                r.SetPropertyBlock(_mpb);
            }
        }

        IEnumerator PinTheHour(float hours)
        {
            IGameClock clock = GameServices.Clock;
            GameConfig config = GameServices.Config;
            Assert.IsNotNull(clock, "no clock"); Assert.IsNotNull(config, "no GameConfig");
            double t = (1.0 + hours / 24.0) * config.SecondsPerDay;
            Time.timeScale = 1f;
            clock.SeekTo(t);
            clock.TimeScale = 0f;
            Color tint = Shader.GetGlobalColor("_DayNightTint");
            float waited = 0f; int still = 0, frames = 0;
            while (frames < 900)
            {
                yield return null; frames++; waited += Time.deltaTime;
                Color now = Shader.GetGlobalColor("_DayNightTint");
                float move = Mathf.Abs(now.r - tint.r) + Mathf.Abs(now.g - tint.g) + Mathf.Abs(now.b - tint.b);
                still = move < 1e-4f ? still + 1 : 0; tint = now;
                if (waited >= 1f && still >= 4) break;
            }
        }

        IEnumerator FrameOn(Vector2 at, bool wide)
        {
            if (_follow == null)
            {
                _follow = FindPersistentFollow();
                _cam = _follow.GetComponent<Camera>();
                _followTargetOnEntry = _follow.Target; _followSmoothOnEntry = _follow.Smooth; _followCaptured = true;
            }
            Time.timeScale = 0f;
            if (_anchor == null) { _anchor = new GameObject("CliffFaceMixPlateAnchor"); _spawned.Add(_anchor); }
            _follow.Target = _anchor.transform;
            _follow.Smooth = 1000f;
            if (_rt == null) { _rt = new RenderTexture(PlateWidthPx, PlateHeightPx, 24, RenderTextureFormat.ARGBHalf); _rt.Create(); }
            _cam.targetTexture = _rt;
            float height = _follow.WorldHeightFor(CameraFraming.OnFoot) * (wide ? WideFactor : 1f);
            _follow.SetFraming(height, 0f);
            if (GameServices.PixelGridSnap) at = PixelGrid.Snap(at, _follow.WorldUnitsPerRenderedPixel);
            _anchor.transform.position = new Vector3(at.x, at.y, 0f);
            _cam.transform.position = new Vector3(at.x, at.y, _cam.transform.position.z);
            for (int i = 0; i < 2; i++) yield return null;
            Time.timeScale = 1f;
            float start = Time.time;
            while (Time.time - start < 1.5f || (Vector2.Distance(_cam.transform.position, at) > 0.01f && Time.time - start < 6f))
                yield return null;
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
        }

        static CameraFollow FindPersistentFollow()
        {
            foreach (CameraFollow f in Object.FindObjectsByType<CameraFollow>())
                if (f != null && MoodGradeDirector.IsPersistentCamera(f.GetComponent<Camera>())) return f;
            return null;
        }

        byte[] Capture()
        {
            _cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(PlateWidthPx, PlateHeightPx, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, PlateWidthPx, PlateHeightPx), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prev;
            Color[] px = tex.GetPixels();
            var bytes = new byte[px.Length * 4];
            for (int i = 0; i < px.Length; i++)
            {
                Color c = new Color(Mathf.Clamp01(px[i].r), Mathf.Clamp01(px[i].g), Mathf.Clamp01(px[i].b), 1f).gamma;
                bytes[i * 4] = (byte)Mathf.RoundToInt(c.r * 255f);
                bytes[i * 4 + 1] = (byte)Mathf.RoundToInt(c.g * 255f);
                bytes[i * 4 + 2] = (byte)Mathf.RoundToInt(c.b * 255f);
                bytes[i * 4 + 3] = 255;
            }
            Object.DestroyImmediate(tex);
            return bytes;
        }

        static double ChangedShare(byte[] a, byte[] b)
        {
            long changed = 0, total = a.Length / 4;
            for (long i = 0; i < a.Length; i += 4)
                if (Math.Abs(a[i] - b[i]) > 2 || Math.Abs(a[i + 1] - b[i + 1]) > 2 || Math.Abs(a[i + 2] - b[i + 2]) > 2) changed++;
            return total > 0 ? (double)changed / total : 0.0;
        }

        static string Hhmm(float hours)
        {
            int hh = Mathf.FloorToInt(hours), mm = Mathf.RoundToInt((hours - hh) * 60f);
            return $"{hh:00}{mm:00}";
        }

        static string PlateDir()
        {
            string env = System.Environment.GetEnvironmentVariable(PlateEnv);
            string dir = string.IsNullOrEmpty(env) ? Path.Combine(Application.temporaryCachePath, PlateDirName) : env;
            Directory.CreateDirectory(dir);
            return dir;
        }

        static void SavePlate(string name, byte[] rgbaBottomLeft)
        {
            var tex = new Texture2D(PlateWidthPx, PlateHeightPx, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgbaBottomLeft);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(PlateDir(), name), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

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
