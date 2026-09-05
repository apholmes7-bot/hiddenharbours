using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A wharf, at an hour, held still, photographed through the game's own camera</b> — the scaffolding
    /// every lighting plate in this project needs, and the four ordering laws that each cost a convincing
    /// WRONG plate before they were found.
    ///
    /// <para><b>Why a real region and the game's own camera.</b> What a lighting change does is a claim about
    /// what a player sees: the day/night multiply, the additive glows above it and the lamp shadows above
    /// those, composited in that order over actual planks and bollards. None of that exists in a synthetic
    /// stage — and the overlay and every light quad are pinned to <c>Camera.main</c>'s own frustum and depth,
    /// so a second camera photographs a frame with the night missing.</para>
    ///
    /// <para><b>⚠️⚠️ THE FOUR LAWS, in the order they bite:</b>
    /// <list type="number">
    ///   <item><b>The game clock holds the sun; it does not hold the SEA.</b> The water animates on ENGINE
    ///     time, and 639,757 pixels of a 1,080,000-pixel plate changed between two IDENTICAL frames — a noise
    ///     floor that swallows anything a lamp does. Engine time has to stop too.</item>
    ///   <item><b><c>DayNightController</c> publishes on init and thereafter follows a MOVING clock.</b> Seek
    ///     and stop the clock in one breath and the frame keeps the hour the SCENE LOADED at: a 02:00 plate
    ///     came back over a bright noon sea, and a night-gated lamp over a daylit sea emits nothing at all,
    ///     which reads as a broken feature. Let the clock RUN until the tint settles, and confirm it arrived
    ///     by asking <see cref="LightMath"/> whether the lamps are gated the way the hour wants.</item>
    ///   <item><b>Freeze AFTER the camera has arrived.</b> The overlay is fitted to the camera every
    ///     LateUpdate, so a camera that moves or zooms while time is frozen leaves the night behind as a
    ///     RECTANGLE INSET in the plate with daylight round it.</item>
    ///   <item><b>Attaching a RenderTexture CHANGES a camera's aspect</b> — and the overlay is fitted to
    ///     <c>orthographicSize × aspect</c>. Hand the camera its plate target BEFORE the world stops and
    ///     leave it there; swapping it inside the capture gives the same inset-rectangle picture from the
    ///     other direction.</item>
    /// </list></para>
    ///
    /// <para>⚠️ <c>LightsAreSourcesPlatePlayTests</c> (#733) predates this file and still carries its own copy
    /// of the scaffolding; it was in flight when this was extracted and moving it would have meant rewriting
    /// a fixture under review. Moving it across is a follow-up, and the laws above are the reason the copy
    /// must not be allowed to drift.</para>
    /// </summary>
    public sealed class WharfNightStage
    {
        /// <summary>Plate height in pixels; the WIDTH is derived from the camera's own aspect and never
        /// chosen — see law 4.</summary>
        public const int PlateHeightPx = 900;

        /// <summary>Below this tint luminance the day/night cycle is treated as not running — the threshold
        /// <c>LampShadowSystem</c> uses, so the fixture and the systems agree about what night is.</summary>
        private const float CycleActiveLuminance = 0.001f;

        private readonly string _sceneName;
        private readonly string _plateDir;
        private readonly List<Object> _spawned = new List<Object>();

        public Camera Camera { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        private RenderTexture _rt;

        public WharfNightStage(string sceneName, string plateDir)
        {
            _sceneName = sceneName;
            _plateDir = plateDir;
        }

        /// <summary>Track an object the stage should destroy on teardown.</summary>
        public T Track<T>(T o) where T : Object { _spawned.Add(o); return o; }

        /// <summary>
        /// ⚠️ Call as the FIRST statement of a test, before any <c>yield</c>. An <c>Assert.Ignore</c> raised
        /// from inside a coroutine that has already yielded unwinds through the runner, the teardown still
        /// runs, and anything that throws there records the case as FAILED with the skip text attached —
        /// which is exactly how a fixture that should cost CI nothing turned a PR red.
        /// </summary>
        public static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device (Null Device), so nothing rendered " +
                              "and nothing was proved. Expected on CI; a plate of a lamp needs a GPU.");
        }

        /// <summary>Load the region. The regions log decor-import complaints that have nothing to do with
        /// lights, so the log guard goes up — and comes down again in <see cref="TearDown"/>, because it is
        /// a STATIC and leaving it on hands every following test a suite in which no error can fail
        /// anything.</summary>
        public IEnumerator Load()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(_sceneName, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
        }

        /// <summary>Seek to an hour and wait for the frame to actually WEAR it — laws 1 and 2. Engine time
        /// keeps running until <see cref="FrameOn"/>, which is law 3.</summary>
        public IEnumerator SetNight(float hour)
        {
            if (GameServices.Clock == null || GameServices.Config == null)
            {
                Assert.Ignore("SKIPPED — the region registered no clock, so the night cannot be pinned and a " +
                              "plate of it would be a plate of whatever hour the run happened to be in.");
                yield break;
            }
            Time.timeScale = 1f;
            GameServices.Clock.SeekTo((1.0 + hour / 24.0) * GameServices.Config.SecondsPerDay);

            bool wantLamps = hour < 5f || hour > 20f;
            Color tint = Shader.GetGlobalColor("_DayNightTint");
            int still = 0, frames = 0;
            while (frames < 900)
            {
                yield return null;
                frames++;
                Color now = Shader.GetGlobalColor("_DayNightTint");
                float move = Mathf.Abs(now.r - tint.r) + Mathf.Abs(now.g - tint.g) + Mathf.Abs(now.b - tint.b);
                still = move < 1e-4f ? still + 1 : 0;
                tint = now;
                if (still >= 4 && LampsAreGatedOn(tint) == wantLamps) break;
            }
            GameServices.Clock.TimeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;

            Debug.Log($"[{_plateDir}] asked for {hour:0.00} h; clock reads {GameServices.Clock.HourOfDay:0.00} h; " +
                      $"tint settled after {frames} frames at ({tint.r:0.000}, {tint.g:0.000}, {tint.b:0.000})");
            Assert.Less(frames, 900,
                $"the day/night tint never reached {hour:0.00} h — the lamps are gated " +
                $"{(LampsAreGatedOn(tint) ? "ON" : "OFF")} where this hour wants them " +
                $"{(wantLamps ? "ON" : "OFF")}, so every plate below would be of some other time of day.");
        }

        /// <summary>Park the game's own camera on a place, take the game's own framing, and only then stop
        /// the world — laws 3 and 4.</summary>
        public IEnumerator FrameOn(Vector2 at)
        {
            var follow = Object.FindFirstObjectByType<CameraFollow>();
            if (follow == null)
            {
                Assert.Ignore("SKIPPED — the region has no CameraFollow, so the plate would be framed by the " +
                              "fixture rather than by the game.");
                yield break;
            }

            var anchor = Track(new GameObject("PlateAnchor"));
            anchor.transform.position = new Vector3(at.x, at.y, 0f);
            follow.Target = anchor.transform;
            // ⚠ Smooth = 0 means NEVER MOVE, not move instantly: the follow lerps by 1-exp(-Smooth·dt).
            follow.Smooth = 1000f;

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "no main camera — the overlay and every light quad are pinned to it");

            Vector3 lastPos = cam.transform.position;
            float lastSize = cam.orthographicSize;
            int still = 0, frames = 0;
            while (still < 5 && frames < 400)
            {
                yield return null;
                frames++;
                bool moved = (cam.transform.position - lastPos).sqrMagnitude > 1e-8f
                             || Mathf.Abs(cam.orthographicSize - lastSize) > 1e-5f;
                still = moved ? 0 : still + 1;
                lastPos = cam.transform.position;
                lastSize = cam.orthographicSize;
            }
            Assert.Less(frames, 400, "the camera never stopped moving, so the overlay it is fitted to would " +
                                     "be a frame behind every plate below");

            Camera = cam;
            Height = PlateHeightPx;
            Width = Mathf.RoundToInt(Height * cam.aspect);
            _rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGBHalf);
            _rt.Create();
            Camera.targetTexture = _rt;             // law 4: before the freeze, and it stays
            for (int i = 0; i < 6; i++) yield return null;

            Time.timeScale = 0f;                    // law 1: and only now
            for (int i = 0; i < 2; i++) yield return null;
        }

        /// <summary>Render the game's camera as the player sees it and read it back, gamma-corrected — the
        /// project is LINEAR, so a raw float read-back saves far too dark.</summary>
        public byte[] Capture()
        {
            Camera.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
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

        public void SavePlate(string name, byte[] rgbaBottomLeft)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgbaBottomLeft);
            tex.Apply();
            string dir = Path.Combine(Application.temporaryCachePath, _plateDir);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[{_plateDir}] plate written: {path}");
        }

        /// <summary>Put the world back: the log guard, engine time, the spawned objects and the region — all
        /// four are STATE that outlives a test, and a PlayMode run shares one player loop.</summary>
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;

            if (Camera != null) Camera.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();

            // ⚠️ LOOK THE CLEANUP SCENE UP BEFORE CREATING IT. CreateScene THROWS on a name that already
            // exists, and a case that SKIPPED before loading the region leaves it resident because no
            // LoadSceneMode.Single ever came along to sweep it. On CI that is every case, and the throw
            // turned the skips into FAILURES carrying their own skip text.
            string cleanupName = _plateDir + "Cleanup";
            Scene clean = SceneManager.GetSceneByName(cleanupName);
            if (!clean.IsValid() || !clean.isLoaded) clean = SceneManager.CreateScene(cleanupName);
            if (clean.IsValid()) SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == _sceneName)
                    yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        // ---- the measurements ------------------------------------------------------------------------

        /// <summary>How many pixels got brighter than <paramref name="threshold"/> (summed over three
        /// channels of a 0..255 read-back). It counts AREA, because "a round glow" is a claim about area.</summary>
        public static int Footprint(byte[] before, byte[] after, int threshold = 30)
        {
            int n = 0;
            for (int i = 0; i + 3 < before.Length; i += 4)
            {
                int d = (after[i] - before[i]) + (after[i + 1] - before[i + 1]) + (after[i + 2] - before[i + 2]);
                if (d > threshold) n++;
            }
            return n;
        }

        /// <summary>The same, as a mask, so a second arm can be judged on the FIRST arm's pixels.</summary>
        public static bool[] LitMask(byte[] before, byte[] after, out int count, int threshold = 30)
        {
            var mask = new bool[before.Length / 4];
            count = 0;
            for (int i = 0, p = 0; i + 3 < before.Length; i += 4, p++)
            {
                int d = (after[i] - before[i]) + (after[i + 1] - before[i + 1]) + (after[i + 2] - before[i + 2]);
                if (d > threshold) { mask[p] = true; count++; }
            }
            return mask;
        }

        /// <summary>
        /// <b>Relative local contrast over a mask</b> — mean neighbour-to-neighbour luminance step divided by
        /// mean luminance. What "washed out" means as a number.
        ///
        /// <para>⚠ It has to be LOCAL and it has to be RELATIVE. Plain variance says a big smooth cream DISC
        /// is the more detailed picture, because a smooth gradient has enormous variance and no structure —
        /// it measures the very blob under review. Neighbour steps ignore the gradient and count the planks;
        /// dividing by the mean is what makes ADDING a flat sheet of light register as the LOSS it is, and
        /// what makes MULTIPLYING register as the no-op it is.</para>
        /// </summary>
        public float RelativeLocalContrast(byte[] frame, bool[] mask)
        {
            double stepSum = 0, lumaSum = 0; int n = 0;
            for (int y = 0; y < Height - 1; y++)
            {
                for (int x = 0; x < Width - 1; x++)
                {
                    int p = y * Width + x;
                    if (!mask[p]) continue;
                    double c = Luma(frame, p);
                    stepSum += System.Math.Abs(Luma(frame, p + 1) - c)
                             + System.Math.Abs(Luma(frame, p + Width) - c);
                    lumaSum += c;
                    n++;
                }
            }
            return (n == 0 || lumaSum <= 0) ? 0f : (float)(stepSum / lumaSum);
        }

        /// <summary>Mean luminance over a mask, 0..1 — "how lit is this patch".</summary>
        public static float MeanLuma(byte[] frame, bool[] mask)
        {
            double sum = 0; int n = 0;
            for (int i = 0, p = 0; i + 3 < frame.Length; i += 4, p++)
            {
                if (mask != null && !mask[p]) continue;
                sum += Luma(frame, p); n++;
            }
            return n == 0 ? 0f : (float)(sum / n);
        }

        public static double Luma(byte[] frame, int pixel)
        {
            int i = pixel * 4;
            return (0.299 * frame[i] + 0.587 * frame[i + 1] + 0.114 * frame[i + 2]) / 255.0;
        }

        /// <summary>Is the additive lights' night gate open at this tint? Asked of <see cref="LightMath"/> at
        /// <c>SceneLight</c>'s own shipped threshold and softness — the function the shader mirrors — so the
        /// fixture never decides for itself what counts as night.</summary>
        public static bool LampsAreGatedOn(Color tint)
        {
            float luminance = 0.299f * tint.r + 0.587f * tint.g + 0.114f * tint.b;
            return HiddenHarbours.Art.LightMath.NightGate(luminance, 0.12f, 0.35f) > 0.5f;
        }
    }
}
