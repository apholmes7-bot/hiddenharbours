using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE TERRAIN PX FLIP, PHOTOGRAPHED AT THE PLAY CAMERA</b>: the owner judges the albedo flip from
    /// these plates, one BEFORE (main, the 36-slice detail array) and one AFTER (this branch, the 60-slice
    /// array) of each frame, shot by the same class in two runs.
    ///
    /// <para>Four plates (handoff 2026-09-17 terrain px flip, :76-78, and the owner's ruling):</para>
    /// <list type="number">
    /// <item><description>St Peters, the wharf root and the shore south of the deck.</description></item>
    /// <item><description>St Peters, the low-tide flats, twice as wide, so the owner can judge the 8 m repeat
    /// of Ripple and Foreshore now that they share the 256 array.</description></item>
    /// <item><description>St Peters, the red saltbox dooryard as drawn, and the FIXTURE: old Lawn beside the
    /// new Grass, so the owner can rule whether Lawn needs a pixel-language re-author.</description></item>
    /// <item><description>Nine Mile Creek, the richest shore in the region.</description></item>
    /// </list>
    ///
    /// <para><b>THE FRAME.</b> Every plate goes through the persistent core's camera and its own
    /// <see cref="CameraFollow"/>, asked for the on-foot framing. That asks 9 m, which picks zoom 4 at the
    /// 1080 px design screen (reference 480 x 270), and the PixelPerfectCamera then re-imposes the size from
    /// the TARGET: 1080 / (4 x 32) = 8.4375 m tall. So the render texture is 1080 px tall, which is what a
    /// 1080p player sees: 15 x 8.4375 m at 1920 px, 30 x 8.4375 m at 3840 px. A 900 px plate would frame
    /// 9.375 m through zoom 3, a view nobody plays. The frame centres were scored with no Unity against the
    /// committed height and splat maps at that size, at this hour.</para>
    ///
    /// <para><b>THE HOUR.</b> The day-1 daylight low water, found by scanning the live environment minute by
    /// minute from 06:00 to 18:00 (-2.087 m at 09:09 on the committed config). Same hour in both runs.</para>
    ///
    /// <para><b>THE PROOF A PLATE HAS A SUBJECT.</b> Each frame is also captured with the terrain quad's
    /// renderer switched off, and the two must differ over most of the frame. Hashes are an identity only
    /// within one run; the caption beside each PNG names the frame it was shot through.</para>
    ///
    /// <para>⚠ No GPU on CI: every case skips there before it loads anything, and the teardown is inert
    /// unless a region was loaded. Plates land in <c>Application.temporaryCachePath/terrain-px-flip/</c>,
    /// which every worktree shares, so a run reads its own plates by a floor file, never by name.</para>
    /// </summary>
    public class TerrainPxFlipPlatePlayTests
    {
        const string PlateDir = "terrain-px-flip";
        const string CleanupSceneName = "TerrainPxFlipCleanup";
        const string StPeters = "StPeters";
        const string NineMileCreek = "NineMileCreek";

        const int PlateHeightPx = CameraFollow.DesignScreenHeightPx;   // 1080: the PPC zoom depends on it
        const int PlateWidthPx = 1920;
        const int WidePlateWidthPx = 3840;

        // The follow eases its look-ahead (at most 2.5 m) out at 3/s: under half a zoom-4 pixel (1/256 m) in
        // ln(640) / 3 = 2.2 s. The wait gives it about three times that before the frame guard reads.
        const float LeadEaseOutSeconds = 6f;
        // ON the centre, not near it: far inside the 1/128 m step of the grid the camera snaps to at zoom 4.
        const float OnTheCentreMetres = 0.001f;

        const int FullArmDepth = 60;   // this branch: 20 materials x 3 steps in the 256 array
        const int BaseArmDepth = 36;   // main at 40f4656f: 12 materials x 3 steps (the other 7 in a 512 array)

        // Scored at -2.087 m on the play frame: deck 10%, water 2%; Grass 47, Shingle 25, Rockweed 7, Talus 5, Shelf 4.
        static readonly Vector2 WharfAndShore = new Vector2(189f, -6.5f);
        // 30 x 8.4375 m, all ground: Ripple 77, Foreshore 19, Shelf 4.
        static readonly Vector2 LowTideWide = new Vector2(-74f, 30.5f);
        // Marram 41, Sand 26, Foreshore 10, Rockweed 8, Grass 8, Ripple 4.
        static readonly Vector2 NineMileShore = new Vector2(52f, -75.5f);
        // Where the red saltbox's keeper stands, about (17.5, 7.67): Grass 96, Dirt 4 as drawn.
        static Vector2 RedSaltboxDooryardExact => StPetersInhabitants.Dooryard(StPetersBuilder.RedSaltboxPos);
        // Every centre sits on the half metre, which lies on every pixel grid the camera snaps to
        // (1 / (32 x zoom) m), so the camera can sit on it exactly in both runs.
        static Vector2 RedSaltboxDooryard => OnTheHalfMetre(RedSaltboxDooryardExact);

        static Vector2 OnTheHalfMetre(Vector2 v) => new Vector2(Mathf.Round(v.x * 2f) * 0.5f, Mathf.Round(v.y * 2f) * 0.5f);

        // Terrain pass 9 added _SplatF (Path, in its r). A region whose builder wires five maps pushes it as
        // the transparent 1x1, which is "nothing painted" and so is not the size of the others.
        static readonly string[] SplatIds = { "_SplatA", "_SplatB", "_SplatC", "_SplatD", "_SplatE", "_SplatF" };
        const int SplatF = 5;

        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        readonly List<Object> _spawned = new List<Object>();
        readonly List<Texture2D> _runtimeCopies = new List<Texture2D>();
        bool _loadedAny;
        bool _introOnEntry;
        string _healed = "none";

        Camera _cam;
        CameraFollow _follow;
        bool _followCaptured;
        Transform _followTargetOnEntry;
        float _followSmoothOnEntry;
        float _askedHeight;
        string _eased = "(not framed)";
        RenderTexture _rt;
        int _w, _h;

        TerrainSplatSurface _surface;
        MeshRenderer _ground;
        string _pinned = "(not pinned)";

        // =============================================================================================
        //  Set-up and teardown: leave nothing loaded (#764)
        // =============================================================================================

        [UnitySetUp]
        public IEnumerator SetUpRegion()
        {
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;   // ⚠ a STATIC: left at 0 it stops every test that follows

            if (_follow != null && _followCaptured)
            {
                _follow.Target = _followTargetOnEntry;
                _follow.Smooth = _followSmoothOnEntry;
            }
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            foreach (Texture2D t in _runtimeCopies) if (t != null) Object.Destroy(t);
            _runtimeCopies.Clear();

            // One fixture instance serves every case: nothing read from this case's core may reach the next.
            _follow = null;
            _cam = null;
            _followCaptured = false;
            _followTargetOnEntry = null;
            _surface = null;
            _ground = null;
            _pinned = "(not pinned)";
            _eased = "(not framed)";
            _healed = "none";

            // A case that skipped before loading anything (every case on CI) has nothing to put back.
            if (!_loadedAny) yield break;

            if (GameServices.Clock != null) GameServices.Clock.TimeScale = 1f;
            GameServices.OpeningCinematicRunning = _introOnEntry;
            GameServices.PendingArrivalKey = null;

            // ⚠ Look the cleanup scene up before creating it: CreateScene throws on a name that exists.
            Scene clean = SceneManager.GetSceneByName(CleanupSceneName);
            if (!clean.IsValid() || !clean.isLoaded) clean = SceneManager.CreateScene(CleanupSceneName);
            if (clean.IsValid()) SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && (s.name == StPeters || s.name == NineMileCreek))
                    yield return SceneManager.UnloadSceneAsync(s);
            }

            // ⚠ #764: a Single load of St Peters promotes the persistent core's roots into
            // DontDestroyOnLoad, and unloading the region leaves them there for every test that follows.
            // Destroy exactly the roots this case added, by identity, then clear the service slots.
            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go)) Object.DestroyImmediate(go);
            GameServices.Reset();
            _loadedAny = false;
            yield return null;
        }

        // =============================================================================================
        //  The plates
        // =============================================================================================

        /// <summary><b>Plate 1.</b> The wharf root and the shore south of the deck: Grass, Shingle, Rockweed,
        /// Talus and Shelf, with the deck's planks as the scale reference.</summary>
        [UnityTest]
        public IEnumerator StPeters_WharfAndShore_AtTheDaylightLowWater()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            yield return PinTheDaylightLowWater();
            yield return FrameOn(WharfAndShore, PlateWidthPx);
            ShootThePlate("plate-1-stpeters-wharf-and-shore",
                "St Peters: the wharf root and the shore south of the deck", WharfAndShore);
        }

        /// <summary><b>Plate 2 (the owner's ruling).</b> Ripple and Foreshore now repeat every 8 m in the 256
        /// array, where they repeated every 16 m in the retired 512 array. This frame is twice as wide as the
        /// play frame so the repeat shows, and every metre of it is exposed ground at this hour.</summary>
        [UnityTest]
        public IEnumerator StPeters_LowTideRippleAndForeshore_Wide()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            yield return PinTheDaylightLowWater();
            yield return FrameOn(LowTideWide, WidePlateWidthPx);
            ShootThePlate("plate-2-stpeters-low-tide-ripple-and-foreshore-wide",
                "St Peters: the low-tide flats, Ripple and Foreshore, 30 m wide to show the 8 m repeat", LowTideWide);
        }

        /// <summary>
        /// <b>Plate 3.</b> The red saltbox dooryard as drawn, then the FIXTURE the owner approved: the same
        /// frame with its left third painted full Lawn and its middle third painted full Grass, the right third
        /// left as drawn.
        ///
        /// <para>Lawn is not in the kit, so on the AFTER arm this is the old Lawn beside the new Grass. The
        /// fixture paints copies of the live splat maps; the committed maps, the scene and every painted
        /// dooryard are untouched.</para>
        ///
        /// <para>⚠ The splat value is both the weight and the step. A texel painted 1.0 in one channel draws
        /// that material at full weight, and full weight can only draw the _Hi step. So the strips are Lawn _Hi
        /// beside Grass _Hi. A lower value would mix in the band ground underneath and stop being a strip of
        /// one material.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator StPeters_RedSaltboxDooryard_OldLawnBesideTheNewGrass()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            yield return PinTheDaylightLowWater();
            Vector2 at = RedSaltboxDooryard;
            yield return FrameOn(at, PlateWidthPx);
            FindTheGround(at);
            string arm = ReadArm(out int depth);

            // --- as drawn, and the subject-removed control ---
            byte[] asDrawn = Capture();
            byte[] control = CaptureWithoutTheTerrain();
            ulong hAsDrawn = Fnv1a(asDrawn), hControl = Fnv1a(control);
            double terrainShare = ChangedShare(asDrawn, control, 0, _w);

            // --- the live maps, exactly as the surface pushed them ---
            var mpb = new MaterialPropertyBlock();
            _ground.GetPropertyBlock(mpb);
            var live = new Texture2D[SplatIds.Length];
            for (int i = 0; i < SplatIds.Length; i++)
            {
                live[i] = mpb.GetTexture(SplatIds[i]) as Texture2D;
                Assert.IsNotNull(live[i], $"the terrain pushed no {SplatIds[i]}, so there is nothing to paint the fixture on.");
                Assert.IsTrue(live[i].isReadable, $"{SplatIds[i]} '{live[i].name}' is not readable, so it cannot be copied.");
                if (i == SplatF && live[i].width == 1 && live[i].height == 1) continue;   // nothing painted with Path
                Assert.AreEqual(live[0].width, live[i].width, $"{SplatIds[i]} is not the size of _SplatA.");
                Assert.AreEqual(live[0].height, live[i].height, $"{SplatIds[i]} is not the size of _SplatA.");
            }
            Vector4 min4 = mpb.GetVector("_HeightWorldMin"), size4 = mpb.GetVector("_HeightWorldSize");
            var min = new Vector2(min4.x, min4.y);
            var size = new Vector2(size4.x, size4.y);
            Assert.Greater(size.x, 0f, "the terrain pushed no _HeightWorldSize; the splat maps cannot be placed.");
            Assert.Greater(size.y, 0f, "the terrain pushed no _HeightWorldSize; the splat maps cannot be placed.");

            // --- the three strips: left Lawn, middle Grass, right as drawn (a metre of margin past the frame) ---
            Vector3 lo = _cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
            Vector3 hi = _cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
            float third = (hi.x - lo.x) / 3f;
            Rect lawnStrip = Rect.MinMaxRect(lo.x - 1f, lo.y - 1f, lo.x + third, hi.y + 1f);
            Rect grassStrip = Rect.MinMaxRect(lo.x + third, lo.y - 1f, lo.x + 2f * third, hi.y + 1f);

            var fixture = new Texture2D[SplatIds.Length];
            for (int i = 0; i < SplatIds.Length; i++)
            {
                fixture[i] = CopyOf(live[i]);
                Color32[] px = fixture[i].GetPixels32();
                // Lawn is _SplatE.b (index 18); Grass is _SplatA.r (index 0). Every other channel goes to 0.
                Fill(px, fixture[i].width, fixture[i].height, lawnStrip, min, size,
                     i == 4 ? new Color32(0, 0, 255, 0) : new Color32(0, 0, 0, 0));
                Fill(px, fixture[i].width, fixture[i].height, grassStrip, min, size,
                     i == 0 ? new Color32(255, 0, 0, 0) : new Color32(0, 0, 0, 0));
                fixture[i].SetPixels32(px);
                fixture[i].Apply(fixture[i].mipmapCount > 1, false);
            }

            _surface.ConfigureSplat(fixture[0], fixture[1], fixture[2], fixture[3], fixture[4], fixture[5]);
            _surface.enabled = false;   // OnDisable hides the quad
            _surface.enabled = true;    // OnEnable rebuilds nothing and pushes every map, synchronously
            for (int i = 0; i < 2; i++) yield return null;
            byte[] withFixture = Capture();
            ulong hFixture = Fnv1a(withFixture);

            int thirdPx = _w / 3;
            // A painted texel reaches half a metre past its edge through the bilinear sample, and the rects are
            // rounded out to whole texels, so the right third is judged beyond 1.5 m of the Grass strip.
            int reachPx = Mathf.CeilToInt(1.5f * _w / (hi.x - lo.x));
            double lawnShare = ChangedShare(asDrawn, withFixture, 0, thirdPx);
            double grassShare = ChangedShare(asDrawn, withFixture, thirdPx, 2 * thirdPx);
            double rightShare = ChangedShare(asDrawn, withFixture, Mathf.Min(_w, 2 * thirdPx + reachPx), _w);

            // --- put the ground back, and say whether it came back ---
            _surface.ConfigureSplat(live[0], live[1], live[2], live[3], live[4], live[5]);
            _surface.enabled = false;
            _surface.enabled = true;
            for (int i = 0; i < 2; i++) yield return null;
            byte[] restored = Capture();
            bool cameBack = Fnv1a(restored) == hAsDrawn;
            double restoredShare = ChangedShare(asDrawn, restored, 0, _w);

            string dooryard = "plate-3-stpeters-red-saltbox-dooryard";
            string lawnBesideGrass = "plate-3-stpeters-red-saltbox-dooryard-lawn-beside-grass";
            SavePlate($"{dooryard}-{arm}.png", asDrawn);
            SavePlate($"{dooryard}-{arm}-control-no-terrain.png", control);
            SavePlate($"{lawnBesideGrass}-{arm}.png", withFixture);

            Vector2 exact = RedSaltboxDooryardExact;
            var extra = new StringBuilder();
            extra.AppendLine($"DOORYARD: StPetersInhabitants.Dooryard(StPetersBuilder.RedSaltboxPos) = ({exact.x:0.###}, {exact.y:0.###}), framed on the half metre at ({at.x:0.#}, {at.y:0.#}).");
            extra.AppendLine("FIXTURE: copies of the live splat maps, painted, pushed, captured, then the live maps pushed back.");
            extra.AppendLine($"  splat maps {live[0].width}x{live[0].height} over min ({min.x:0.###}, {min.y:0.###}) size ({size.x:0.###}, {size.y:0.###}) m; " +
                             $"format {live[0].format}, sRGB {live[0].isDataSRGB}, mips {live[0].mipmapCount}, filter {live[0].filterMode}, wrap {live[0].wrapMode}");
            extra.AppendLine($"  frame x {lo.x:0.###}..{hi.x:0.###}, y {lo.y:0.###}..{hi.y:0.###} m");
            extra.AppendLine($"  left third  x {lawnStrip.xMin:0.###}..{lawnStrip.xMax:0.###}: full Lawn (_SplatE.b = 1, every other channel 0), drawn at its _Hi step");
            extra.AppendLine($"  middle third x {grassStrip.xMin:0.###}..{grassStrip.xMax:0.###}: full Grass (_SplatA.r = 1, every other channel 0), drawn at its _Hi step");
            extra.AppendLine($"  right third: as drawn");
            extra.AppendLine($"  changed against as drawn (> 2/255 in any channel): left {lawnShare:P1}, middle {grassShare:P1}, right {rightShare:P1} (the right third is read beyond {reachPx} px, 1.5 m, of the Grass strip, and should be ~0; if not, the copy is not faithful)");
            extra.AppendLine($"  as drawn {hAsDrawn:x16}, fixture {hFixture:x16}; restored came back identical: {cameBack} (changed {restoredShare:P2})");
            SaveCaption($"{lawnBesideGrass}-{arm}.txt", Caption(lawnBesideGrass,
                "St Peters: the red saltbox dooryard, old Lawn (left) beside the new Grass (middle), as drawn (right)",
                at, arm, depth, hFixture, hControl, ChangedShare(withFixture, control, 0, _w), extra.ToString()));
            SaveCaption($"{dooryard}-{arm}.txt", Caption(dooryard,
                "St Peters: the red saltbox dooryard as drawn", at, arm, depth, hAsDrawn, hControl, terrainShare, null));

            Debug.Log($"[{PlateDir}] {lawnBesideGrass} ({arm}): left {lawnShare:P1}, middle {grassShare:P1}, right {rightShare:P1} changed; " +
                      $"restored identical {cameBack} ({restoredShare:P2} changed)");

            AssertTheTerrainIsTheSubject(dooryard, hAsDrawn, hControl, terrainShare);
            Assert.AreNotEqual(hAsDrawn, hFixture,
                "the fixture plate is the as-drawn plate: the painted copies never reached the terrain, so it " +
                "shows no Lawn and no Grass strip.");
            Assert.Greater(lawnShare + grassShare, 0.05,
                "painting two thirds of the frame changed almost nothing, so the strips are not in the picture.");
        }

        /// <summary>
        /// <b>Plate 4.</b> The richest shore in Nine Mile Creek: Marram, Sand, Foreshore, Rockweed, Grass and
        /// Ripple in one frame.
        ///
        /// <para>⚠ Reached the way play reaches it, St Peters first. Opened alone, the region seeds its
        /// editor-only dev core with a DevCamera that is never promoted, which is not the camera a player
        /// looks through. And arriving activates that dev core over the live one, which takes the clock, the
        /// environment, the wallet and the config down with it when it is destroyed. The live core's services
        /// are put back, and the caption says so.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator NineMileCreek_Shore_AtTheDaylightLowWater()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            yield return LoadRegion(NineMileCreek);
            yield return PinTheDaylightLowWater();
            yield return FrameOn(NineMileShore, PlateWidthPx);
            ShootThePlate("plate-4-nine-mile-creek-shore",
                "Nine Mile Creek: the shore at (52, -75.5)", NineMileShore);
        }

        // =============================================================================================
        //  The region, the hour and the frame
        // =============================================================================================

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED: no graphics device (Null Device), so nothing rendered " +
                              "and nothing was proved. Expected on CI; a plate of the ground needs a GPU.");
        }

        IEnumerator LoadRegion(string sceneName)
        {
            if (!_loadedAny) _introOnEntry = GameServices.OpeningCinematicRunning;

            GameConfig configBefore = GameServices.Config;
            IGameClock clockBefore = GameServices.Clock;
            IEnvironmentService envBefore = GameServices.Environment;
            IWallet walletBefore = GameServices.Wallet;

            LogAssert.ignoreFailingMessages = true;   // the regions log decor complaints of their own
            _loadedAny = true;
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;   // let the self-installing components register

            var healed = new List<string>();
            if (GameServices.Config == null && configBefore != null) { GameServices.Config = configBefore; healed.Add("Config"); }
            if (GameServices.Clock == null && clockBefore != null) { GameServices.Clock = clockBefore; healed.Add("Clock"); }
            if (GameServices.Environment == null && envBefore != null) { GameServices.Environment = envBefore; healed.Add("Environment"); }
            if (GameServices.Wallet == null && walletBefore != null) { GameServices.Wallet = walletBefore; healed.Add("Wallet"); }
            if (healed.Count > 0)
            {
                string line = $"{sceneName}: re-published {string.Join(", ", healed)} after the region's dev core took them down";
                _healed = _healed == "none" ? line : _healed + "; " + line;
                Debug.Log($"[{PlateDir}] {line}.");
            }

            int frames = 0;
            while (frames < 240 && FindPersistentFollow() == null)
            {
                yield return null;
                frames++;
            }
            Assert.IsNotNull(FindPersistentFollow(),
                $"{sceneName}: after 240 frames there is no CameraFollow on the persistent core's camera, so there " +
                "is no play camera to shoot through.");
            Debug.Log($"[{PlateDir}] {sceneName} loaded: region '{GameServices.CurrentRegionId}', camera up after {frames} frames.");
        }

        /// <summary>
        /// Stop the clock at the day-1 daylight low water, then let the frame catch up with it.
        ///
        /// <para>The clock reads wall time and stops only on its own TimeScale. The day/night controller ticks
        /// on SCALED time and reads the clock's hour on every tick, and the terrain pushes the water level on
        /// scaled time too, so a stopped clock under a running Time.timeScale gets both. The wait is at least
        /// one second, ten of the controller's ticks, because a tint that has not moved for four frames may
        /// simply not have ticked yet.</para>
        /// </summary>
        IEnumerator PinTheDaylightLowWater()
        {
            IGameClock clock = GameServices.Clock;
            IEnvironmentService env = GameServices.Environment;
            GameConfig config = GameServices.Config;
            Assert.IsNotNull(clock, "the region registered no clock, so the hour cannot be pinned.");
            Assert.IsNotNull(env, "the region registered no environment, so the tide cannot be read.");
            Assert.IsNotNull(config, "the region registered no GameConfig, so a day has no length.");

            double spd = config.SecondsPerDay;
            double best = (1.0 + 360.0 / 1440.0) * spd;
            float lowest = float.MaxValue;
            for (int m = 360; m <= 1080; m++)
            {
                double t = (1.0 + m / 1440.0) * spd;
                float level = env.WaterLevelAt(t);
                if (level < lowest) { lowest = level; best = t; }
            }

            Time.timeScale = 1f;
            clock.SeekTo(best);
            clock.TimeScale = 0f;
            Assert.LessOrEqual(System.Math.Abs(clock.TotalSeconds - best), 1.0,
                $"the clock did not land on {best:0.0} s (it reads {clock.TotalSeconds:0.0} s), so the plate would " +
                "be shot at some other tide.");

            Color tint = Shader.GetGlobalColor("_DayNightTint");
            float waited = 0f;
            int still = 0, frames = 0;
            while (frames < 900)
            {
                yield return null;
                frames++;
                waited += Time.deltaTime;
                Color now = Shader.GetGlobalColor("_DayNightTint");
                float move = Mathf.Abs(now.r - tint.r) + Mathf.Abs(now.g - tint.g) + Mathf.Abs(now.b - tint.b);
                still = move < 1e-4f ? still + 1 : 0;
                tint = now;
                if (waited >= 1f && still >= 4) break;
            }
            Assert.Less(frames, 900, "the day/night tint never settled at the pinned hour.");

            float water = env.WaterLevelAt(clock.TotalSeconds);
            float hour = clock.HourOfDay;
            int hh = Mathf.FloorToInt(hour), mm = Mathf.FloorToInt((hour - hh) * 60f);
            _pinned = $"day {clock.DayIndex} {hh:00}:{mm:00} ({hour:0.000} h, TotalSeconds {clock.TotalSeconds:0.0}, " +
                      $"SecondsPerDay {spd:0}); water {water:+0.000;-0.000} m, the lowest of 06:00-18:00 on day 1; " +
                      $"_DayNightTint ({tint.r:0.000}, {tint.g:0.000}, {tint.b:0.000}) after {frames} frames / {waited:0.00} s";
            Debug.Log($"[{PlateDir}] pinned: {_pinned}");
            Assert.AreEqual(lowest, water, 1e-3f, "the stopped clock is not at the low water it was seeked to.");
        }

        /// <summary>
        /// Hand the play camera's own follow a still anchor on the frame centre, at the on-foot framing, with
        /// the 1080 px target attached; let the world run until the frame is still; then stop the world.
        ///
        /// <para>⚠ The hand-over happens with the world STOPPED. The follow estimates its target's velocity
        /// from frame-to-frame motion, so an anchor 180 m from the player at full time reads as thousands of
        /// metres a second: a look-ahead tail that decays for seconds, and a speed pull-back that eases on
        /// UNSCALED time and can hold the view a zoom step wide. At Time.timeScale 0 the estimate is zero and
        /// the follow integrates nothing, so the camera is placed on the centre, the follow learns the anchor
        /// as its last position, and the world restarts with nothing to ease but the look-ahead the player's
        /// last motion left, which <see cref="EaseOntoTheCentre"/> waits out. Both arms are then shot from
        /// the same position and their plates line up pixel for pixel.</para>
        ///
        /// <para>The render texture goes on while the world is stopped and stays on: the PixelPerfectCamera
        /// sizes the view from the target, and the world then runs with it attached before anything is read.</para>
        /// </summary>
        IEnumerator FrameOn(Vector2 at, int widthPx)
        {
            _follow = FindPersistentFollow();
            Assert.IsNotNull(_follow, "no CameraFollow on the persistent core's camera.");
            Assert.IsTrue(_follow.isActiveAndEnabled, "the play camera's CameraFollow is disabled, so it will not follow the anchor.");
            _cam = _follow.GetComponent<Camera>();
            Assert.IsNotNull(_cam, "the play camera's CameraFollow has no Camera.");

            if (!_followCaptured)
            {
                _followTargetOnEntry = _follow.Target;
                _followSmoothOnEntry = _follow.Smooth;
                _followCaptured = true;
            }

            Time.timeScale = 0f;
            var anchor = new GameObject("TerrainPxFlipPlateAnchor");
            _spawned.Add(anchor);
            anchor.transform.position = new Vector3(at.x, at.y, 0f);
            _follow.Target = anchor.transform;
            _follow.Smooth = 1000f;   // any residue arrives in a frame, not over a second of easing
            _cam.transform.position = new Vector3(at.x, at.y, _cam.transform.position.z);
            _askedHeight = _follow.WorldHeightFor(CameraFraming.OnFoot);
            _follow.SetFraming(_askedHeight, 0f);

            _w = widthPx;
            _h = PlateHeightPx;
            _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf);
            _rt.Create();
            _cam.targetTexture = _rt;
            for (int i = 0; i < 2; i++) yield return null;   // the follow reads the anchor while nothing can move

            Time.timeScale = 1f;
            for (int i = 0; i < 8; i++) yield return null;
            yield return SettleCamera(_cam);
            yield return EaseOntoTheCentre(at);

            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;

            Vector3 p = _cam.transform.position;
            float worldHeight = _cam.orthographicSize * 2f;
            Assert.LessOrEqual(Vector2.Distance(new Vector2(p.x, p.y), at), OnTheCentreMetres,
                $"the camera sits at ({p.x:0.####}, {p.y:0.####}), not on the frame centre ({at.x:0.###}, {at.y:0.###}), " +
                $"after {_eased}: something else drives it (a region-bounds clamp, a cinematic), so the plate would " +
                "show other ground and the two arms would not line up.");
            Assert.That(worldHeight, Is.InRange(8f, 9.5f),
                $"the camera frames {worldHeight:0.###} m tall, not the on-foot view (8.4375 m at 1080 px).");
        }

        /// <summary>
        /// Run the world until the camera sits ON the frame centre, not merely still near it.
        ///
        /// <para>⚠ Still is not arrived. The follow leads its target by a look-ahead learned from the target's
        /// motion (velocity x 0.35 s, at most 2.5 m) and eases that lead out at 3/s. The hand-over, made with
        /// the world stopped, keeps whatever lead the player's last motion left, and the pixel snap rounds the
        /// easing camera onto the 1/128 m grid, so it can hold a pixel for five frames while the lead is still
        /// going out. Settled on stillness alone, the first BEFORE run shot every frame off its centre: the
        /// three St Peters frames 2 px west, and Nine Mile Creek 1 px west and 3 px south, which the old
        /// 0.02 m guard caught. Every centre lies on the half metre, so on that grid, and once the lead is
        /// under half a pixel the snap puts the camera on the centre exactly: both arms shoot the same
        /// pixels.</para>
        /// </summary>
        IEnumerator EaseOntoTheCentre(Vector2 at)
        {
            Vector2 stillAt = _cam.transform.position;
            float start = Time.time;
            int frames = 0;
            while (Vector2.Distance(_cam.transform.position, at) > OnTheCentreMetres && Time.time - start < LeadEaseOutSeconds)
            {
                yield return null;
                frames++;
            }
            _eased = $"still at ({stillAt.x:0.####}, {stillAt.y:0.####}), then {frames} frames / {Time.time - start:0.00} s " +
                     $"at full time for the look-ahead to ease out";
            Debug.Log($"[{PlateDir}] framed on ({at.x:0.###}, {at.y:0.###}): {_eased}");
        }

        static IEnumerator SettleCamera(Camera cam)
        {
            Vector3 lastPos = cam.transform.position;
            float lastSize = cam.orthographicSize;
            int still = 0, frames = 0;
            while (still < 5 && frames < 400)
            {
                yield return null;
                frames++;
                bool moved = (cam.transform.position - lastPos).sqrMagnitude > 1e-8f ||
                             Mathf.Abs(cam.orthographicSize - lastSize) > 1e-5f;
                still = moved ? 0 : still + 1;
                lastPos = cam.transform.position;
                lastSize = cam.orthographicSize;
            }
            Assert.Less(frames, 400, "the camera never came to rest.");
        }

        static CameraFollow FindPersistentFollow()
        {
            foreach (CameraFollow f in Object.FindObjectsByType<CameraFollow>())
                if (f != null && MoodGradeDirector.IsPersistentCamera(f.GetComponent<Camera>()))
                    return f;
            return null;
        }

        /// <summary>The terrain quad whose bounds hold the frame centre, and proof the camera can see it.</summary>
        void FindTheGround(Vector2 at)
        {
            _surface = null;
            _ground = null;
            foreach (TerrainSplatSurface s in Object.FindObjectsByType<TerrainSplatSurface>())
            {
                if (s == null || !s.isActiveAndEnabled) continue;
                foreach (MeshRenderer r in s.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r == null || r.gameObject.name != "TerrainSplatQuad" || !r.enabled) continue;
                    Bounds b = r.bounds;
                    if (at.x < b.min.x || at.x > b.max.x || at.y < b.min.y || at.y > b.max.y) continue;
                    _surface = s;
                    _ground = r;
                    break;
                }
                if (_ground != null) break;
            }
            Assert.IsNotNull(_ground, $"no terrain quad covers ({at.x:0.##}, {at.y:0.##}), so the plate would show no ground.");
            Assert.IsTrue(GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(_cam), _ground.bounds),
                "the camera's frustum misses the terrain quad.");
        }

        /// <summary>The arm is the detail array the ground actually samples: 60 slices here, 36 on main.</summary>
        string ReadArm(out int depth)
        {
            var mpb = new MaterialPropertyBlock();
            _ground.GetPropertyBlock(mpb);
            var array = mpb.GetTexture("_DetailArr256") as Texture2DArray;
            depth = array != null ? array.depth : -1;
            Assert.That(depth, Is.EqualTo(FullArmDepth).Or.EqualTo(BaseArmDepth),
                $"the ground samples a _DetailArr256 of depth {depth}, which is neither arm, so the plate cannot say " +
                "what it shows. (Was the array rebuilt?)");
            return depth == FullArmDepth ? "after" : "before";
        }

        // =============================================================================================
        //  Shooting
        // =============================================================================================

        void ShootThePlate(string plate, string subject, Vector2 at)
        {
            FindTheGround(at);
            string arm = ReadArm(out int depth);
            byte[] picture = Capture();
            byte[] control = CaptureWithoutTheTerrain();
            ulong hPicture = Fnv1a(picture), hControl = Fnv1a(control);
            double share = ChangedShare(picture, control, 0, _w);

            SavePlate($"{plate}-{arm}.png", picture);
            SavePlate($"{plate}-{arm}-control-no-terrain.png", control);
            SaveCaption($"{plate}-{arm}.txt", Caption(plate, subject, at, arm, depth, hPicture, hControl, share, null));
            AssertTheTerrainIsTheSubject(plate, hPicture, hControl, share);
        }

        static void AssertTheTerrainIsTheSubject(string plate, ulong hPicture, ulong hControl, double share)
        {
            Assert.AreNotEqual(hPicture, hControl,
                $"{plate}: the frame is identical with the terrain switched off, so the ground is not in the picture.");
            Assert.Greater(share, 0.25,
                $"{plate}: switching the terrain off changed only {share:P1} of the frame, so the ground is not the subject.");
        }

        byte[] CaptureWithoutTheTerrain()
        {
            _ground.enabled = false;
            try { return Capture(); }
            finally { _ground.enabled = true; }
        }

        byte[] Capture()
        {
            _cam.Render();
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_w, _h, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, _w, _h), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prevActive;

            Color[] px = tex.GetPixels();
            var outBytes = new byte[px.Length * 4];
            for (int i = 0; i < px.Length; i++)
            {
                Color c = new Color(Mathf.Clamp01(px[i].r), Mathf.Clamp01(px[i].g), Mathf.Clamp01(px[i].b), 1f).gamma;
                outBytes[i * 4] = (byte)Mathf.RoundToInt(c.r * 255f);
                outBytes[i * 4 + 1] = (byte)Mathf.RoundToInt(c.g * 255f);
                outBytes[i * 4 + 2] = (byte)Mathf.RoundToInt(c.b * 255f);
                outBytes[i * 4 + 3] = 255;
            }
            Object.DestroyImmediate(tex);
            return outBytes;
        }

        Texture2D CopyOf(Texture2D source)
        {
            bool mips = source.mipmapCount > 1;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, mips, !source.isDataSRGB)
            {
                name = source.name + " (terrain px flip fixture)",
                filterMode = source.filterMode,
                wrapMode = source.wrapMode,
            };
            _runtimeCopies.Add(copy);
            copy.SetPixels32(source.GetPixels32());
            copy.Apply(mips, false);
            return copy;
        }

        /// <summary>Paint a world rectangle into a map's pixels. Texture row 0 is the map's world min.y.</summary>
        static void Fill(Color32[] px, int width, int height, Rect world, Vector2 min, Vector2 size, Color32 value)
        {
            int c0 = Mathf.Clamp(Mathf.FloorToInt((world.xMin - min.x) / size.x * width), 0, width);
            int c1 = Mathf.Clamp(Mathf.CeilToInt((world.xMax - min.x) / size.x * width), 0, width);
            int r0 = Mathf.Clamp(Mathf.FloorToInt((world.yMin - min.y) / size.y * height), 0, height);
            int r1 = Mathf.Clamp(Mathf.CeilToInt((world.yMax - min.y) / size.y * height), 0, height);
            for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                    px[r * width + c] = value;
        }

        static ulong Fnv1a(byte[] data)
        {
            ulong h = 14695981039346656037UL;
            foreach (byte b in data)
            {
                h ^= b;
                h *= 1099511628211UL;
            }
            return h;
        }

        /// <summary>The share of pixels in columns [col0, col1) that differ by more than 2/255 in any channel.</summary>
        double ChangedShare(byte[] a, byte[] b, int col0, int col1)
        {
            long changed = 0, total = 0;
            for (int row = 0; row < _h; row++)
                for (int col = col0; col < col1; col++)
                {
                    int i = (row * _w + col) * 4;
                    total++;
                    if (Mathf.Abs(a[i] - b[i]) > 2 || Mathf.Abs(a[i + 1] - b[i + 1]) > 2 || Mathf.Abs(a[i + 2] - b[i + 2]) > 2)
                        changed++;
                }
            return total > 0 ? (double)changed / total : 0.0;
        }

        // =============================================================================================
        //  The caption: the frame each plate was shot through
        // =============================================================================================

        string Caption(string plate, string subject, Vector2 at, string arm, int depth,
                       ulong hPicture, ulong hControl, double share, string extra)
        {
            Vector3 p = _cam.transform.position;
            float worldHeight = _cam.orthographicSize * 2f;
            Bounds b = _ground.bounds;
            var sb = new StringBuilder();
            sb.AppendLine($"{plate}-{arm}");
            sb.AppendLine(subject);
            sb.AppendLine($"arm: {arm} (_DetailArr256 depth {depth}: {FullArmDepth} = this branch, {BaseArmDepth} = main at 40f4656f)");
            sb.AppendLine($"run: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z, Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceType} on {SystemInfo.graphicsDeviceName}. Hashes are an identity only within this run.");
            sb.AppendLine($"scene: {SceneManager.GetActiveScene().name}; region '{GameServices.CurrentRegionId}'; services healed: {_healed}");
            sb.AppendLine($"camera: '{_cam.name}' in scene '{_cam.gameObject.scene.name}', persistent {MoodGradeDirector.IsPersistentCamera(_cam)}");
            sb.AppendLine($"asked centre ({at.x:0.###}, {at.y:0.###}); camera at ({p.x:0.####}, {p.y:0.####}, {p.z:0.###}); {_eased}");
            sb.AppendLine($"ortho {_cam.orthographicSize:0.#####} -> {worldHeight * _cam.aspect:0.###} x {worldHeight:0.####} m, aspect {_cam.aspect:0.####}, render texture {_w}x{_h} px, " +
                          $"{_follow.WorldUnitsPerRenderedPixel:0.#####} m per rendered pixel");
            sb.AppendLine($"framing: asked {_askedHeight:0.###} m (WorldHeightFor OnFoot); committed {_follow.Framing} (known {_follow.FramingKnown})");
            sb.AppendLine(DescribePixelPerfect());
            sb.AppendLine($"hour and tide: {_pinned}");
            sb.AppendLine($"OpeningCinematicRunning {GameServices.OpeningCinematicRunning}; Time.timeScale {Time.timeScale}");
            sb.AppendLine($"ground: '{_surface.name}', quad bounds x {b.min.x:0.#}..{b.max.x:0.#}, y {b.min.y:0.#}..{b.max.y:0.#}");
            sb.AppendLine($"picture {hPicture:x16}; control with the terrain off {hControl:x16}; {share:P1} of the frame changed");
            if (!string.IsNullOrEmpty(extra)) sb.Append(extra);
            return sb.ToString();
        }

        string DescribePixelPerfect()
        {
            foreach (Behaviour b in _cam.GetComponents<Behaviour>())
            {
                if (b == null || b.GetType().Name != "PixelPerfectCamera") continue;
                var sb = new StringBuilder($"PixelPerfectCamera: enabled {b.enabled}");
                foreach (string n in new[] { "assetsPPU", "refResolutionX", "refResolutionY", "pixelRatio",
                                             "gridSnapping", "cropFrame", "upscaleRT", "pixelSnapping" })
                {
                    PropertyInfo pi = b.GetType().GetProperty(n, BindingFlags.Public | BindingFlags.Instance);
                    if (pi == null || !pi.CanRead) continue;
                    try { sb.Append($", {n} {pi.GetValue(b)}"); }
                    catch (System.Exception) { sb.Append($", {n} (unreadable)"); }
                }
                return sb.ToString();
            }
            return "PixelPerfectCamera: none on the camera";
        }

        void SavePlate(string name, byte[] rgbaBottomLeft)
        {
            var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgbaBottomLeft);
            tex.Apply();
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[{PlateDir}] plate written: {path}");
        }

        static void SaveCaption(string name, string text)
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), text, new UTF8Encoding(false));
        }

        // =============================================================================================
        //  DontDestroyOnLoad bookkeeping (#764)
        // =============================================================================================

        static Scene PersistentScene()
        {
            var probe = new GameObject("__ddolProbe");
            Object.DontDestroyOnLoad(probe);
            Scene s = probe.scene;
            Object.DestroyImmediate(probe);
            return s;
        }

        static List<GameObject> PersistentRoots()
        {
            Scene s = PersistentScene();
            return s.IsValid() ? s.GetRootGameObjects().ToList() : new List<GameObject>();
        }
    }
}
