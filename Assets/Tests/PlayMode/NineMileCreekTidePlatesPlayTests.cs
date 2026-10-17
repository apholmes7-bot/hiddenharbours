#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <para><b>NMC H2, PR 2 Phase B: the activated creek's plates.</b> The game's own camera over the scene as it
    /// ships, at the month's lowest, mean and highest DAYLIGHT water (09:00 to 16:00, found by searching the
    /// live tide, never by picking hours): the harbour at all three; the river mouths, the bridge approaches,
    /// Wharf Road's ramp, the harbour mouth, the north creek's lots, the south shore's estates and the pond F4,
    /// the gangway and the berths at low and high; the plaza's wells at mean; the wharf's two ladders at low.</para>
    ///
    /// <para>These are documents, not goldens: each case asserts only that a plate rendered something (not one
    /// flat colour) and logs the water, the ground and the on-foot depth at the plate's centre. The plates go to
    /// <c>temporaryCachePath/nmc-h2-b2/</c>, one render target reused for all of them. A case loads the creek
    /// afresh the way the loop does (the old cores go, the clock and environment slots are emptied, so the
    /// creek's own dev core wakes) and enters the world through Continue, which writes nothing.</para>
    /// </summary>
    public class NineMileCreekTidePlatesPlayTests
    {
        const string SceneName = "NineMileCreek";
        const string PlateDir = "nmc-h2-b2";
        const int PlateHeightPx = 900, PlateWidthPx = 1600;
        // One asset pixel per plate pixel: wide enough to read a river mouth or a bridge approach whole.
        const float PlateWorldHeightMetres = PlateHeightPx / (float)CameraFollow.AssetsPPU;
        const double DaylightFromHour = 9.0, DaylightToHour = 16.0;
        const double MonthDays = 30.0, StepsPerDay = 240.0;
        const int MaxSettleFrames = 600, MaxFrameOnFrames = 400;
        // Luma standard deviation, 0..255: below it the plate is one colour. A plate that drew nothing is the clear
        // colour alone (0); the dimmest real plate so far measured 3.8 (the south shore's lawn on the low-water morning).
        const double FlatPlateSpread = 1.0;

        [System.Flags]
        enum Water { Low = 1, Mean = 2, High = 4 }

        readonly struct Place
        {
            public readonly string Name;
            public readonly Vector2 At;
            public readonly Water When;
            public Place(string name, float x, float y, Water when) { Name = name; At = new Vector2(x, y); When = when; }
        }

        const Water LowHigh = Water.Low | Water.High;

        static readonly Place[] Places =
        {
            new Place("harbour", 110f, 80f, Water.Low | Water.Mean | Water.High),
            new Place("town-river-mouth", 40f, 167f, LowHigh),
            new Place("creek-channel-east", 196f, 148.5f, LowHigh),
            new Place("creek-channel-west", 146f, 148.5f, LowHigh),
            new Place("bridge-south-approach", -176.8f, 186f, LowHigh),
            new Place("bridge-north-approach", -176.8f, 224f, LowHigh),
            new Place("wharf-road-ramp", 30f, 108f, LowHigh),
            new Place("harbour-mouth", 137f, -2f, LowHigh),
            new Place("plaza-well-east", -221.5f, 141.65f, Water.Mean),
            new Place("plaza-well-west", -250f, 141.15f, Water.Mean),
            new Place("north-creek-lot-waterfront-1", -73.95f, 257.15f, LowHigh),
            new Place("north-creek-lot-waterfront-2", -46.1f, 256.7f, LowHigh),
            new Place("south-shore-estates-west", -330f, -140f, LowHigh),
            new Place("south-shore-estates-east", -250f, -115f, LowHigh),
            new Place("pond-F4", -211f, -164f, LowHigh),
            new Place("gangway", 105f, 78.85f, LowHigh),
            new Place("ladder-berth-0", 86.2f, 86f, Water.Low),
            new Place("ladder-east", 139.25f, 86f, Water.Low),
            new Place("berth-0", 86.2f, 83.1f, LowHigh),
            new Place("float-berths", 128f, 70f, LowHigh),
        };

        GameObject _core, _anchor;
        Camera _cam;
        RenderTexture _rt;
        float _engineTimeScale = 1f;

        [UnityTest, Timeout(900000)]
        public IEnumerator TheCreek_AtLowWater_ThePlates() => Shoot(Water.Low);

        [UnityTest, Timeout(900000)]
        public IEnumerator TheCreek_AtMeanWater_ThePlates() => Shoot(Water.Mean);

        [UnityTest, Timeout(900000)]
        public IEnumerator TheCreek_AtHighWater_ThePlates() => Shoot(Water.High);

        IEnumerator Shoot(Water state)
        {
            // ⚠️ FIRST statement, before any yield (see WharfNightStage.RequireAGraphicsDevice).
            WharfNightStage.RequireAGraphicsDevice();

            for (var e = LoadTheCreek(); e.MoveNext();) yield return e.Current;
            double t = FindTheWater(state, out float level, out double hour);
            for (var e = SeekAndSettle(t); e.MoveNext();) yield return e.Current;

            _rt = new RenderTexture(PlateWidthPx, PlateHeightPx, 24, RenderTextureFormat.ARGBHalf);
            _rt.Create();
            _cam.targetTexture = _rt;
            _cam.aspect = (float)PlateWidthPx / PlateHeightPx;

            int shot = 0;
            var flat = new List<string>();
            foreach (Place p in Places)
            {
                if ((p.When & state) == 0) continue;
                shot++;
                for (var e = FrameOn(p.At); e.MoveNext();) yield return e.Current;
                byte[] px = Capture();
                string tide = state.ToString().ToLowerInvariant();
                string name = $"{tide}-{shot:00}-{p.Name}-at-{tide}-water.png";
                string path = SavePlate(name, px);
                double spread = Spread(px);
                float ground = GameServices.TidalTerrain != null ? GameServices.TidalTerrain.ElevationAt(p.At) : float.NaN;
                float depth = TidalWalkability.DepthNow(p.At);
                float water = GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds);
                EnvironmentSample sky = GameServices.Environment.Sample();
                Color tint = Shader.GetGlobalColor("_DayNightTint");
                Debug.Log($"[{PlateDir}] {name}: {tide} water {water:0.000} m (searched {level:0.000} m at {hour:0.00} h, " +
                          $"seed {GameServices.Environment.WorldSeed}); " +
                          $"centre ({p.At.x:0.##}, {p.At.y:0.##}) ground {ground:0.###} m, on-foot depth {depth:0.###} m; " +
                          $"visibility {sky.Visibility:0.##}, sea state {sky.SeaState01:0.##}, " +
                          $"tint ({tint.r:0.###}, {tint.g:0.###}, {tint.b:0.###}); " +
                          $"camera ortho {_cam.orthographicSize:0.##}; luma spread {spread:0.#}");
                if (spread < FlatPlateSpread) flat.Add($"{name} ({spread:0.##})");
                Debug.Log($"[{PlateDir}] plate written: {path}");
            }
            Assert.That(shot, Is.GreaterThan(0), $"no place is shot at {state} water.");
            Assert.That(flat, Is.Empty, $"plates of one flat colour (nothing rendered): {string.Join(", ", flat)}");
        }

        [UnityTearDown]
        public IEnumerator TearDownTheCreek()
        {
            Time.timeScale = _engineTimeScale > 0f ? _engineTimeScale : 1f;
            if (_cam != null) { _cam.targetTexture = null; _cam.ResetAspect(); }
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            if (_anchor != null) Object.Destroy(_anchor);
            if (_core != null) Object.Destroy(_core);
            yield return null;
            GameServices.Clock = null;
            GameServices.Environment = null;

            const string cleanupName = "NineMileCreekTidePlatesCleanup";
            Scene clean = SceneManager.GetSceneByName(cleanupName);
            if (!clean.IsValid() || !clean.isLoaded) clean = SceneManager.CreateScene(cleanupName);
            if (clean.IsValid()) SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == SceneName)
                    yield return SceneManager.UnloadSceneAsync(s);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        // ---- the stage --------------------------------------------------------------------------------------

        IEnumerator LoadTheCreek()
        {
            LogAssert.ignoreFailingMessages = true;   // the region's known decor-import errors
            _engineTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            foreach (GameRoot old in Object.FindObjectsByType<GameRoot>()) Object.Destroy(old.transform.root.gameObject);
            yield return null;
            GameServices.Clock = null;
            GameServices.Environment = null;

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;

            GameRoot[] roots = Object.FindObjectsByType<GameRoot>();
            Assert.That(roots.Length, Is.EqualTo(1), $"{roots.Length} cores in play; the creek's own dev core must be the only one.");
            _core = roots[0].transform.root.gameObject;
            Assert.That(GameServices.Clock, Is.Not.Null, "the creek's core wired no clock.");
            Assert.That(GameServices.Environment, Is.Not.Null, "the creek's core wired no environment.");
            Assert.That(GameServices.Config, Is.Not.Null, "the creek's core published no GameConfig.");
            ShellFlow.ContinueGame();   // into the world; Continue writes nothing
            yield return null;

            _cam = Camera.main;
            Assert.That(_cam, Is.Not.Null, "no main camera in the creek's dev core.");
            Assert.That(Object.FindAnyObjectByType<CameraFollow>(), Is.Not.Null, "the creek's dev core has no CameraFollow.");
        }

        /// <summary>The month's lowest or highest daylight water, or the daylight moment nearest the mean.</summary>
        static double FindTheWater(Water state, out float level, out double hour)
        {
            IEnvironmentService env = GameServices.Environment;
            double day = GameServices.Config.SecondsPerDay;
            double start = day;   // from day 1, not the save's hour: the same plates on every run
            double best = start;
            float bestKey = float.MaxValue;
            level = 0f;
            for (int i = 0; i <= MonthDays * StepsPerDay; i++)
            {
                double t = start + i * day / StepsPerDay;
                double h = (t / day % 1.0) * 24.0;
                if (h < DaylightFromHour || h > DaylightToHour) continue;
                float w = env.WaterLevelAt(t);
                float key = state == Water.Low ? w : state == Water.High ? -w : Mathf.Abs(w);
                if (key < bestKey) { bestKey = key; best = t; level = w; }
            }
            hour = (best / day % 1.0) * 24.0;
            return best;
        }

        static IEnumerator SeekAndSettle(double at)
        {
            IGameClock clock = GameServices.Clock;
            Time.timeScale = 1f;
            clock.TimeScale = 1f;
            clock.SeekTo(at);
            Color tint = Shader.GetGlobalColor("_DayNightTint");
            int still = 0, frames = 0;
            while (still < 4 && frames < MaxSettleFrames)
            {
                yield return null;
                frames++;
                Color now = Shader.GetGlobalColor("_DayNightTint");
                float move = Mathf.Abs(now.r - tint.r) + Mathf.Abs(now.g - tint.g) + Mathf.Abs(now.b - tint.b);
                still = move < 1e-4f ? still + 1 : 0;
                tint = now;
            }
            clock.TimeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
        }

        IEnumerator FrameOn(Vector2 at)
        {
            var follow = Object.FindAnyObjectByType<CameraFollow>();
            if (_anchor == null) _anchor = new GameObject("NmcPlateAnchor (test)");
            _anchor.transform.position = new Vector3(at.x, at.y, 0f);
            follow.Target = _anchor.transform;
            follow.Smooth = 1000f;   // any residue arrives in a frame, not over a second of easing
            _cam.transform.position = new Vector3(at.x, at.y, _cam.transform.position.z);
            follow.SetFraming(PlateWorldHeightMetres, 0f);
            Time.timeScale = 0f;     // the follow runs on unscaled time; the scene holds still for the plate

            Vector3 lastPos = _cam.transform.position;
            float lastSize = _cam.orthographicSize;
            int still = 0, frames = 0;
            while (still < 5 && frames < MaxFrameOnFrames)
            {
                yield return null;
                frames++;
                bool moved = (_cam.transform.position - lastPos).sqrMagnitude > 1e-8f
                             || Mathf.Abs(_cam.orthographicSize - lastSize) > 1e-5f;
                still = moved ? 0 : still + 1;
                lastPos = _cam.transform.position;
                lastSize = _cam.orthographicSize;
            }
            Assert.That(frames, Is.LessThan(MaxFrameOnFrames), $"the camera never settled over ({at.x}, {at.y}).");
            for (int i = 0; i < 6; i++) yield return null;   // whatever streams in behind the camera
        }

        /// <summary>The game's camera as the player sees it, read back gamma-corrected (the project is linear).</summary>
        byte[] Capture()
        {
            _cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(PlateWidthPx, PlateHeightPx, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, PlateWidthPx, PlateHeightPx), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Color[] px = tex.GetPixels();
            var bytes = new byte[px.Length * 4];
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b); c.a = 1f;
                c = c.gamma;
                bytes[i * 4 + 0] = (byte)Mathf.RoundToInt(c.r * 255f);
                bytes[i * 4 + 1] = (byte)Mathf.RoundToInt(c.g * 255f);
                bytes[i * 4 + 2] = (byte)Mathf.RoundToInt(c.b * 255f);
                bytes[i * 4 + 3] = 255;
            }
            Object.DestroyImmediate(tex);
            return bytes;
        }

        static string SavePlate(string name, byte[] rgba)
        {
            var tex = new Texture2D(PlateWidthPx, PlateHeightPx, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgba);
            tex.Apply();
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }

        static double Spread(byte[] rgba)
        {
            double sum = 0, sq = 0;
            int n = 0;
            for (int i = 0; i < rgba.Length; i += 4 * 37)   // a sparse sample is plenty to tell one colour from a picture
            {
                double y = 0.2126 * rgba[i] + 0.7152 * rgba[i + 1] + 0.0722 * rgba[i + 2];
                sum += y;
                sq += y * y;
                n++;
            }
            double mean = sum / n;
            return System.Math.Sqrt(System.Math.Max(0.0, sq / n - mean * mean));
        }
    }
}
#endif
