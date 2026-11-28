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
    /// <b>THE ROUGH CLIFFS, FOR THE OWNER'S EYE</b> (owner 11 26: <i>"its time to add rougher texture, they are
    /// currently far too smooth"</i>). St Peters is loaded once, the hour and the tide are pinned, and the same
    /// frames are shot through the play camera: long runs of the cliffs, close views of a face and its foot, the
    /// Harbour Stack, and the Whelps at low water. Run it on the commit before and the commit after and the two
    /// sets differ by the change and nothing else.
    ///
    /// <para>Plates land in <c>HH_CLIFF_ROUGH_PLATES</c> if the launch shell sets it, else
    /// <c>Application.temporaryCachePath/CliffRoughPlates</c>; each file name ends in <c>HH_CLIFF_ROUGH_TAG</c>
    /// (default <c>shot</c>).</para>
    ///
    /// <para><b>Explicit</b>: a picture, not a gate. No GPU on CI: it skips there before it loads anything.</para>
    /// </summary>
    [Explicit("Owner plates: run by name on a box with a GPU.")]
    public class CliffRoughEdgesPlatesPlayTests
    {
        const string StPeters = "StPeters";
        const string CleanupSceneName = "CliffRoughCleanup";
        const string PlateDirName = "CliffRoughPlates";
        const string PlateEnv = "HH_CLIFF_ROUGH_PLATES";
        const string TagEnv = "HH_CLIFF_ROUGH_TAG";
        const int PlateWidthPx = 1920;
        const int PlateHeightPx = CameraFollow.DesignScreenHeightPx;
        const float Noon = 13f;
        const float MidTide = 0f, LowTide = -1.2f;

        /// <summary>Shot name, the point framed, the framing as a share of the on-foot height, and the tide held.</summary>
        static readonly (string shot, Vector2 at, float zoom, float tide)[] Frames =
        {
            ("1-run-south-wall", new Vector2(72.5f, -71f), 2.5f, MidTide),
            ("1-run-east-ledges", new Vector2(106f, -71f), 2f, MidTide),
            ("1-run-south-west", new Vector2(-39f, -31f), 2f, MidTide),
            ("2-close-south-wall", new Vector2(72.5f, -71f), 0.55f, MidTide),
            ("2-close-south-east", new Vector2(178f, -32.5f), 0.55f, MidTide),
            ("2-close-steep-east", new Vector2(187.5f, -17.5f), 0.55f, MidTide),
            ("2-close-mapping-seam", new Vector2(184.5f, -22.5f), 0.55f, MidTide),
            ("2-close-east-ramp", new Vector2(190f, -8.5f), 0.55f, MidTide),
            ("2-close-east-boundary", new Vector2(169f, -39.5f), 0.55f, MidTide),
            ("2-close-west-boundary", new Vector2(-34f, -35f), 0.55f, MidTide),
            ("1-game-east-wall", new Vector2(187.5f, -17.5f), 1f, MidTide),
            ("3-whelps-close", new Vector2(187f, -47f), 0.55f, LowTide),
            ("3-harbour-stack", new Vector2(192f, -37f), 1f, MidTide),
            ("3-harbour-stack-close", new Vector2(192f, -36f), 0.55f, MidTide),
            ("3-whelps-low-water", new Vector2(187f, -47f), 1f, LowTide),
            ("4-light-station-run", new Vector2(206.5f, 56f), 2f, MidTide),
            ("4-light-station", new Vector2(206.5f, 55f), 1f, MidTide),
            ("4-light-station-brow", new Vector2(206.5f, 52f), 0.55f, MidTide),
            ("4-light-station-walker", new Vector2(206.5f, 52f), 0.55f, MidTide),
            ("0-wide-st-peters", new Vector2(85f, -5f), 4.5f, MidTide),
            // Every stretch of bank with no wall where the soft turf strip used to draw.
            ("5-west-bank-south", new Vector2(-48f, -5f), 2f, MidTide),
            ("5-west-bank-north", new Vector2(-40f, 35f), 2f, MidTide),
            ("5-north-shore-west", new Vector2(-5f, 52f), 2f, MidTide),
            ("5-north-shore-mid", new Vector2(30f, 68f), 2f, MidTide),
            ("5-north-shore-east", new Vector2(80f, 58f), 2f, MidTide),
            ("5-light-station-north", new Vector2(190f, 68f), 2f, MidTide),
            ("5-east-bank", new Vector2(190f, 15f), 2.5f, MidTide),
        };

        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        readonly List<Object> _spawned = new List<Object>();
        bool _loadedAny, _introOnEntry, _followCaptured;
        CameraFollow _follow;
        Camera _cam;
        Transform _followTargetOnEntry;
        float _followSmoothOnEntry;
        GameObject _anchor;
        RenderTexture _rt;
        IEnvironmentService _envHeld;
        TerrainShoreLinesDef _lines;
        TerrainShoreLinesDef.Step _turfOnEntry;
        const string StripEnv = "HH_CLIFF_ROUGH_STRIP";
        const string WalkerShot = "4-light-station-walker";
        static readonly Vector2 WalkerFeet = new Vector2(208.7f, 50f);

        sealed class HeldTide : IEnvironmentService
        {
            readonly IEnvironmentService _inner;
            readonly float _level;
            public HeldTide(IEnvironmentService inner, float level) { _inner = inner; _level = level; }
            public int WorldSeed => _inner.WorldSeed;
            public TideProfile ActiveTideProfile { get => _inner.ActiveTideProfile; set => _inner.ActiveTideProfile = value; }
            public EnvironmentSample Sample()
            {
                var s = _inner.Sample();
                return new EnvironmentSample(s.WindVector, s.CurrentVector, _level, s.SeaState, s.Visibility, s.SeaState01);
            }
            public float TideHeightAt(double seconds) => _level;
            public float WaterLevelAt(double seconds) => _level;
        }

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
            if (_envHeld != null) { GameServices.Environment = _envHeld; _envHeld = null; }
            if (_lines != null) { _lines.Turf = _turfOnEntry; _lines = null; }
            if (_follow != null && _followCaptured) { _follow.Target = _followTargetOnEntry; _follow.Smooth = _followSmoothOnEntry; }
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
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

        [UnityTest, Timeout(900000)]
        public IEnumerator StPeters_TheCliffsTheStackAndTheWhelps_AtNoon_OnAHeldTide()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED: no graphics device, so nothing rendered.");

            _introOnEntry = GameServices.OpeningCinematicRunning;
            GameConfig configBefore = GameServices.Config;
            IGameClock clockBefore = GameServices.Clock;
            IEnvironmentService envBefore = GameServices.Environment;
            LogAssert.ignoreFailingMessages = true;
            _loadedAny = true;
            // HH_CLIFF_ROUGH_STRIP=old shoots the banks as they were: the soft turf strip on, the bank lip hidden.
            bool oldStrip = System.Environment.GetEnvironmentVariable(StripEnv) == "old";
            if (oldStrip)
            {
                _lines = Resources.Load<TerrainShoreLinesDef>("TerrainShoreLines/StPetersShoreLines");
                Assert.IsNotNull(_lines);
                _turfOnEntry = _lines.Turf;
                _lines.Turf = TerrainShoreLinesDef.Step.Normal;
            }
            yield return HiddenHarbours.Tests.Support.SceneSettle.Load(StPeters, LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;
            var lipLog = new StringBuilder();
            foreach (MeshFilter lip in Resources.FindObjectsOfTypeAll<MeshFilter>())
            {
                if (lip.name != ShoreBrowLipSurface.ChildName || lip.sharedMesh == null) continue;
                if (oldStrip) { lip.gameObject.SetActive(false); continue; }
                // Where the bank lip hangs, in 20 m cells, so every stretch of it can be framed.
                var cells = new SortedDictionary<(int, int), int>();
                foreach (Vector3 v in lip.sharedMesh.vertices)
                {
                    Vector3 w = lip.transform.TransformPoint(v);
                    var k = (Mathf.FloorToInt(w.y / 20f), Mathf.FloorToInt(w.x / 20f));
                    cells.TryGetValue(k, out int n); cells[k] = n + 1;
                }
                foreach (var kv in cells)
                    lipLog.AppendLine($"  [bank-lip] x {kv.Key.Item2 * 20}..{kv.Key.Item2 * 20 + 20} y {kv.Key.Item1 * 20}..{kv.Key.Item1 * 20 + 20}: {kv.Value / 4} quads");
            }
            if (GameServices.Config == null && configBefore != null) GameServices.Config = configBefore;
            if (GameServices.Clock == null && clockBefore != null) GameServices.Clock = clockBefore;
            if (GameServices.Environment == null && envBefore != null) GameServices.Environment = envBefore;
            for (int f = 0; f < 240 && FindPersistentFollow() == null; f++) yield return null;
            Assert.IsNotNull(FindPersistentFollow(), "no play camera to shoot through");
            Assert.IsNotNull(GameServices.Environment, "no environment to hold the tide on");
            _envHeld = GameServices.Environment;

            string tag = System.Environment.GetEnvironmentVariable(TagEnv);
            if (string.IsNullOrEmpty(tag)) tag = "shot";
            var log = new StringBuilder();
            log.AppendLine($"St Peters rough-cliff plates '{tag}'. Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceName}.");
            log.AppendLine(oldStrip ? "the soft turf strip ON, the bank lip hidden" : "the bank lip:");
            log.Append(lipLog);
            yield return PinTheHour(Noon);
            // To find which piece of a wall draws a thing: HH_CLIFF_ROUGH_HIDE names child prefixes to leave out.
            string hide = System.Environment.GetEnvironmentVariable("HH_CLIFF_ROUGH_HIDE");
            if (!string.IsNullOrEmpty(hide))
                foreach (CliffWallSurface wall in Object.FindObjectsByType<CliffWallSurface>())
                    foreach (MeshRenderer r in wall.GetComponentsInChildren<MeshRenderer>())
                        if (hide.Split(',').Any(h => r.name.StartsWith(h) ||
                                                      (h == "sheared" && !wall.PlanarFace) ||
                                                      (h == "planar" && wall.PlanarFace))) r.enabled = false;
            float tideNow = float.NaN;
            foreach (var fr in Frames)
            {
                if (!Mathf.Approximately(fr.tide, tideNow)) { yield return HoldTheTide(fr.tide); tideNow = fr.tide; }
                yield return FrameOn(fr.at, fr.zoom);
                if (fr.shot == WalkerShot) yield return StandTheWalker(WalkerFeet, log);
                SavePlate($"{fr.shot}_{tag}.png", Capture());
                log.AppendLine($"{fr.shot}: camera ({_cam.transform.position.x:0.##}, {_cam.transform.position.y:0.##}), " +
                               $"{_cam.orthographicSize * 2f:0.##} m tall, tide {fr.tide:0.##} m");
                if (TimedShots.Contains(fr.shot)) log.AppendLine(TimeTheFault(fr.shot));
                if (!string.IsNullOrEmpty(hide))
                {
                    float halfH = _cam.orthographicSize * 0.35f, halfW = halfH * _cam.aspect;
                    var view = new Bounds(new Vector3(_cam.transform.position.x, _cam.transform.position.y, 0f),
                                          new Vector3(halfW * 2f, halfH * 2f, 1000f));
                    foreach (CliffWallSurface wall in Object.FindObjectsByType<CliffWallSurface>())
                        foreach (MeshRenderer r in wall.GetComponentsInChildren<MeshRenderer>(true))
                            if (r.name.StartsWith(CliffWallSurface.BandChildPrefix) && r.bounds.Intersects(view))
                                log.AppendLine($"  [in-view] {wall.name} planar {wall.PlanarFace} {r.name} {Folds(r)} " +
                                               $"centre ({r.bounds.center.x:0.#}, {r.bounds.center.y:0.#}) " +
                                               $"size ({r.bounds.size.x:0.#}, {r.bounds.size.y:0.#}) order {r.sortingOrder}");
                }
            }
            File.WriteAllText(Path.Combine(PlateDir(), $"plates_{tag}.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[cliff-rough-plates] " + log);
        }

        /// <summary>How many of a band's triangles are wound each way on screen: both counts above zero means the
        /// face folds over itself there, and its outline is then a fold and not a band edge.</summary>
        static string Folds(MeshRenderer r)
        {
            MeshFilter filter = r.GetComponent<MeshFilter>();
            Mesh m = filter != null ? filter.sharedMesh : null;
            if (m == null) return "no mesh";
            Vector3[] v = m.vertices;
            int[] t = m.triangles;
            int one = 0, other = 0;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                Vector2 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                if (area > 1e-7f) one++; else if (area < -1e-7f) other++;
            }
            return $"wound {one}/{other}";
        }

        /// <summary>Stands the player with his feet at a point, for a plate of what draws over what there.</summary>
        IEnumerator StandTheWalker(Vector2 feet, StringBuilder log)
        {
            var player = Object.FindAnyObjectByType<HiddenHarbours.Player.PlayerWalkController>(FindObjectsInactive.Include);
            if (player == null) { log.AppendLine("  [walker] no player in the loaded game"); yield break; }
            player.gameObject.SetActive(true);
            var body = player.GetComponent<Rigidbody2D>();
            var foot = player.GetComponent<CircleCollider2D>();
            Vector2 at = feet - (foot != null ? foot.offset : Vector2.zero);
            if (body != null) { body.position = at; body.linearVelocity = Vector2.zero; }
            player.transform.position = new Vector3(at.x, at.y, player.transform.position.z);
            Physics2D.SyncTransforms();
            Time.timeScale = 1f;
            float start = Time.time;
            while (Time.time - start < 0.5f) yield return null;
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
            log.AppendLine($"  [walker] feet asked ({feet.x:0.##}, {feet.y:0.##}), stands at ({player.transform.position.x:0.##}, {player.transform.position.y:0.##})");
        }

        IEnumerator HoldTheTide(float level)
        {
            GameServices.Environment = new HeldTide(_envHeld, level);
            float until = Time.realtimeSinceStartup + 20f;
            Time.timeScale = 1f;
            do { yield return null; }
            while (Time.realtimeSinceStartup < until &&
                   (Shader.GetGlobalVector("_HHSeaLevelWorld").w < .5f ||
                    Mathf.Abs(Shader.GetGlobalVector("_HHSeaLevelWorld").x - level) > .01f));
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

        IEnumerator FrameOn(Vector2 at, float zoom)
        {
            if (_follow == null)
            {
                _follow = FindPersistentFollow();
                _cam = _follow.GetComponent<Camera>();
                _followTargetOnEntry = _follow.Target; _followSmoothOnEntry = _follow.Smooth; _followCaptured = true;
            }
            Time.timeScale = 0f;
            if (_anchor == null) { _anchor = new GameObject("CliffRoughPlateAnchor"); _spawned.Add(_anchor); }
            _follow.Target = _anchor.transform;
            _follow.Smooth = 1000f;
            if (_rt == null) { _rt = new RenderTexture(PlateWidthPx, PlateHeightPx, 24, RenderTextureFormat.ARGBHalf); _rt.Create(); }
            _cam.targetTexture = _rt;
            _follow.SetFraming(_follow.WorldHeightFor(CameraFraming.OnFoot) * zoom, 0f);
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

        // The frames the faulted bedding's cost is measured on: a wall on the screen grid filling the frame.
        static readonly string[] TimedShots = { "2-close-steep-east", "1-game-east-wall" };
        const int TimedRenders = 60;
        static readonly int FaultBId = Shader.PropertyToID("_FaultB");

        /// <summary>A rough frame time for this frame with the faulted bedding as built and with it switched off on
        /// every cliff renderer (its property block's flag), whole camera renders, the queue flushed by a read.</summary>
        string TimeTheFault(string shot)
        {
            var faulted = new List<(MeshRenderer r, Vector4 b)>();
            var block = new MaterialPropertyBlock();
            foreach (CliffWallSurface wall in Object.FindObjectsByType<CliffWallSurface>())
                foreach (MeshRenderer r in wall.GetComponentsInChildren<MeshRenderer>())
                {
                    r.GetPropertyBlock(block);
                    Vector4 b = block.GetVector(FaultBId);
                    if (b.w > 0.5f) faulted.Add((r, b));
                }
            void Set(bool on)
            {
                foreach ((MeshRenderer r, Vector4 b) in faulted)
                {
                    r.GetPropertyBlock(block);
                    block.SetVector(FaultBId, on ? b : new Vector4(b.x, b.y, b.z, 0f));
                    r.SetPropertyBlock(block);
                }
            }
            float Ms()
            {
                Flush();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < TimedRenders; i++) _cam.Render();
                Flush();
                return (float)watch.Elapsed.TotalMilliseconds / TimedRenders;
            }
            Ms();                                   // warm
            float on1 = Ms();
            Set(false); float off1 = Ms();
            Set(true); float on2 = Ms();
            Set(false); float off2 = Ms();
            Set(true);
            return $"[cliff-time] {shot}: {faulted.Count} faulted renderers; ms a render at {PlateWidthPx}x{PlateHeightPx}: " +
                   $"fault on {on1:0.000} and {on2:0.000}, fault off {off1:0.000} and {off2:0.000}";
        }

        void Flush()
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var one = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            one.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
            RenderTexture.active = prev;
            Object.DestroyImmediate(one);
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
