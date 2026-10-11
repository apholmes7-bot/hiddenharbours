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
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>TERRAIN PR 5 B: ST PETERS' GROUND, PHOTOGRAPHED IN THE GAME.</b> The plates the three charters ask
    /// for (09-29 §4.3, amendment 1 §5, amendment 2 §5), and terrain PR 5w's of the walls on that ground (W1-W4:
    /// the 10-01 charter §4.6), each shot through the persistent core's own camera on the committed scene and
    /// maps, so the owner judges the ground the player will stand on.
    ///
    /// <para><b>THE CAMERA IS FREE.</b> A plate needs a frame the play framing never shows (the beach at
    /// CD's 32 px per metre, the crossing whole), so the camera's <see cref="CameraFollow"/> and its
    /// <c>PixelPerfectCamera</c> (found by type name: its 2D assembly is not this one's) are switched off,
    /// and the camera is placed and sized by hand. Both come back in the teardown. The camera must then hold
    /// still for 30 frames, or something else is driving it and the plate fails.</para>
    ///
    /// <para><b>THE TIDE.</b> Searched minute by minute over days 1 to 30, 08:00 to 17:00, on the live
    /// environment: the spring low is the lowest level, the spring high the highest, and mean tide the
    /// minute within 2 cm of the datum nearest 15:30. The clock is stopped there and the plate waits until
    /// the drawn sea has reached the level and the day/night tint has settled.</para>
    ///
    /// <para><b>STILL WATER.</b> The game registers none yet: only <c>PaintedTidalTerrain</c> registers it
    /// (ADR 0046), and that lands in the next PR (T11). The plates that show ponds and the run register
    /// <see cref="PaintedHeightMap.StillWater"/> from the committed seabed themselves, and say so in the
    /// caption; every other plate shows the game as it draws today.</para>
    ///
    /// <para><b>THE WALLS.</b> PR 5 B laid the ground and PR 5w lays its walls on it by their real ids
    /// (amendment 1 §4.10), so every caption names the cliff walls in its frame by real id, with what PR 5w
    /// did to each, read from its Def in <c>Data/Terrain/StPetersWalls/</c>. A retired or held wall stands in
    /// no scene: one in a frame is named as a fault.</para>
    ///
    /// <para><b>THE SAVE.</b> St Peters boots to the title, the save service writes nothing while the shell
    /// is there, and these plates never leave it. A case skips, before it moves anything, if the region did
    /// not boot to the title.</para>
    ///
    /// <para>⚠ No GPU on CI: every case skips there before it loads anything. Plates land in
    /// <c>Application.temporaryCachePath/terrain-pr5b/</c>, which every worktree shares, so a run reads its
    /// own plates by a floor file, never by name.</para>
    /// </summary>
    public class StPetersGroundPlatePlayTests
    {
        const string PlateDir = "terrain-pr5b";
        const string CleanupSceneName = "StPetersGroundPlateCleanup";
        const string StPeters = "StPeters";
        const string PlanDir = "Assets/_Project/Data/Terrain/StPetersPlan/";
        const string WallPrefix = "CliffWall_";

        // The tide search: days 1..30, in daylight.
        const int FirstDay = 1, LastDay = 30;
        const int FirstMinute = 8 * 60, LastMinute = 17 * 60;
        const float MeanBandMetres = 0.02f;
        const float MeanHourWanted = 15.5f;
        const float SeaLevelTolerance = 1e-3f;
        const float SeaLevelWaitSeconds = 20f;

        const int StillFramesAsked = 30;
        const float ShaderWaitSeconds = 120f;
        const float StayedPutMetres = 1e-4f;

        const int PlateWidthPx = 1920, PlateHeightPx = 1080;
        // CD's two beach boards are drawn at 32 px per metre over x -6..70, y -96..-46 (2432 x 1600 px);
        // the beach plates are shot over the same rectangle at the same scale, to sit beside them.
        const int BoardPxPerMetre = 32;
        static readonly Rect BoardRect = Rect.MinMaxRect(-6f, -96f, 70f, -46f);

        enum TideAt { SpringLow, Mean, SpringHigh, BoardNoon }

        sealed class Plate
        {
            public string Name, Subject, Ids;
            public Vector2 Centre;
            public float Ortho;
            public int Width = PlateWidthPx, Height = PlateHeightPx;
        }

        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        bool _loadedAny;
        bool _introOnEntry;
        string _healed = "none";

        Camera _cam;
        CameraFollow _follow;
        Behaviour _ppc;   // the URP 2D PixelPerfectCamera, by type name
        bool _cameraCaptured;
        bool _followEnabledOnEntry, _ppcEnabledOnEntry;
        float _orthoOnEntry;
        Vector3 _camPosOnEntry;
        string _framed = "(not framed)";
        RenderTexture _rt;
        int _w, _h;

        TerrainSplatSurface _surface;
        MeshRenderer _ground;
        string _pinned = "(not pinned)";

        bool _stillChanged;
        IStillWater _stillOnEntry;
        string _still = "NOT registered: the game registers no still water until PaintedTidalTerrain lands (T11, the next PR), so this is what the game draws today";

        PaintedHeightMap _seabed;

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

            if (_stillChanged)
            {
                GameServices.StillWater = ReferenceEquals(_stillOnEntry, EmptyStillWater.Instance) ? null : _stillOnEntry;
                _stillChanged = false;
            }
            if (_cameraCaptured)
            {
                if (_cam != null)
                {
                    _cam.targetTexture = null;
                    _cam.ResetAspect();
                    _cam.orthographicSize = _orthoOnEntry;
                    _cam.transform.position = _camPosOnEntry;
                }
                if (_ppc != null) _ppc.enabled = _ppcEnabledOnEntry;
                if (_follow != null) _follow.enabled = _followEnabledOnEntry;
            }
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }

            // One fixture instance serves every case: nothing read from this case's core may reach the next.
            _follow = null;
            _cam = null;
            _ppc = null;
            _cameraCaptured = false;
            _surface = null;
            _ground = null;
            _seabed = null;
            _stillOnEntry = null;
            _pinned = "(not pinned)";
            _framed = "(not framed)";
            _healed = "none";
            _still = "NOT registered: the game registers no still water until PaintedTidalTerrain lands (T11, the next PR), so this is what the game draws today";

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
                if (s.IsValid() && s != clean && s.name == StPeters)
                    yield return SceneManager.UnloadSceneAsync(s);
            }

            // ⚠ #764: a Single load of St Peters promotes the persistent core's roots into
            // DontDestroyOnLoad, and unloading the region leaves them there for every test that follows.
            // Destroy exactly the roots this case added, by identity, then clear the service slots.
            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go)) Object.DestroyImmediate(go);
            GameServices.Reset();
            // ⚠ The core's GameRoot.OnDestroy put the shell back to Playing, and the self-installed SaveService
            // writes the save on quit unless the shell is at the title: a launch of these plates alone wrote it.
            // Leave the shell at the title, where every case found the region.
            ShellFlow.EnterTitle();
            _loadedAny = false;
            yield return null;
        }

        // =============================================================================================
        //  The plates
        // =============================================================================================

        /// <summary><b>A1-A3 (amendment 1 §5, amendment 2 §4.5).</b> The main beach at a spring low, at
        /// mean tide and at a spring high, over the rectangle CD's two beach boards draw, at their 32 px per
        /// metre, so each sits beside <c>beach-high-summer-west.png</c> and <c>-east.png</c>.</summary>
        [UnityTest]
        public IEnumerator MainBeach_AtThreeTides_OverCDsBoards()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            // Issue 3 compares the complete derived ground-and-water maps with the boards.
            yield return RegisterTheStillWater();
            var plate = new Plate
            {
                Name = "a-main-beach",
                Subject = "St Peters: the main beach, over CD's two beach boards' rectangle at their 32 px per metre",
                Ids = "ground.stp_main_beach (the package's beach, its bands from the lane's CoastRecipeDef); " +
                      "ground.stp_main_beach_west_blend (fix 2) at the west edge",
                Centre = BoardRect.center,
                Ortho = BoardRect.height * 0.5f,
                Width = Mathf.RoundToInt(BoardRect.width * BoardPxPerMetre),
                Height = Mathf.RoundToInt(BoardRect.height * BoardPxPerMetre),
            };
            yield return FrameFree(plate);
            string[] tags = { "a1-spring-low", "a2-mean", "a3-spring-high" };
            TideAt[] tides = { TideAt.SpringLow, TideAt.Mean, TideAt.SpringHigh };
            for (int i = 0; i < tides.Length; i++)
            {
                yield return PinTheTide(tides[i], 15.5f);
                yield return HoldStill(plate);
                yield return Shoot(plate, tags[i], null);
            }
        }

        /// <summary><b>B (amendment 1 §5).</b> The beach's west end, where fix 2 holds the beach's toe at the
        /// flats' level, at a spring low. Terrain PR 5w shoots the same frame for the seam at x -8 (the owner's
        /// ruling, 10-04 21:12:53Z): the beach's paint fades out over its box's edge, so no straight step runs
        /// along it.</summary>
        [UnityTest]
        public IEnumerator MainBeach_WestEnd_Fix2_AtASpringLow()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            var plate = new Plate
            {
                Name = "b-beach-west-end", Centre = new Vector2(2f, -70f), Ortho = 16f,
                Subject = "St Peters: the main beach's west end (fix 2), and the seam at x -8 (PR 5w)",
                Ids = "ground.stp_main_beach_west_blend (fix 2: the beach's toe held at the flats' level, raised only); ground.stp_main_beach; " +
                      "bay.stp_main_beach (PR 5w: its paint fades over 5 m inside its box's edges, the edge wandering 4 m at 3 m, seed 580; " +
                      "the height map is main's)",
            };
            yield return PinTheTide(TideAt.SpringLow);
            yield return FrameFree(plate);
            yield return Shoot(plate, "spring-low", null);
        }

        /// <summary><b>C (amendment 1 §5).</b> The South-West Bluff's channel at a spring low, where fix 3
        /// holds its bed below the spring low along wall 068's toe.</summary>
        [UnityTest]
        public IEnumerator BluffChannel_Fix3_AtASpringLow()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            var plate = new Plate
            {
                Name = "c-bluff-channel", Centre = new Vector2(-37.4f, -35f), Ortho = 15f,
                Subject = "St Peters: the South-West Bluff's channel (fix 3)",
                Ids = "ground.stp_bluff_channel_hold (fix 3: the channel's bed along wall 068's toe held below the spring low, lowered only)",
            };
            yield return PinTheTide(TideAt.SpringLow);
            yield return FrameFree(plate);
            yield return Shoot(plate, "spring-low", null);
        }

        /// <summary><b>D (amendment 1 §4.10, §5; the 10-01 charter §4.6).</b> The Head's neck from the south,
        /// named as the interim: PR 5w puts no ring wall in (amendment 1 §4.2, §6), so the Head's south face
        /// stands as a bare step until the ring's own PR, and the ring's walls 079-146 are held Defs.</summary>
        [UnityTest]
        public IEnumerator HeadsNeck_FromTheSouth_TheInterim()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            var plate = new Plate
            {
                Name = "d-heads-neck-from-the-south", Centre = new Vector2(182f, 42f), Ortho = 20f,
                Subject = "St Peters: the Head's neck from the south, THE INTERIM (amendment 1 §4.10, §6): the Head's south face is a bare step at x 190.25 until the Head's ring (walls 079-146 held; PR 5w puts no ring wall in)",
                Ids = "ground.stp_ne_neck; ground.stp_ne_head; ground.stp_cannery_hold",
            };
            yield return PinTheTide(TideAt.Mean);
            yield return FrameFree(plate);
            yield return Shoot(plate, "mean", null);
        }

        /// <summary><b>E (amendment 2 §4.3, §5).</b> The hollow by the Head's neck after fix 4. The before is
        /// a map image made with no Unity from the same import with fix 4 left out.</summary>
        [UnityTest]
        public IEnumerator NeckHollow_AfterFix4()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            var hollow = new Vector2(176.5f, 36.2f);
            var plate = new Plate
            {
                Name = "e-neck-hollow-after-fix4", Centre = hollow, Ortho = 8f,
                Subject = "St Peters: the hollow by the Head's neck, AFTER fix 4 (the before is the headless map image beside it)",
                Ids = "ground.stp_ne_neck_hollow_fill (fix 4: the closed hollow filled to its spill, +3.755, raised only, outside the cannery's circle)",
            };
            yield return PinTheTide(TideAt.Mean);
            yield return FrameFree(plate);
            yield return Shoot(plate, "mean", $"GROUND at the hollow's centre ({hollow.x:0.0}, {hollow.y:0.0}) on the committed map: {Ground(hollow):+0.000;-0.000} m (fix 4 fills to +3.755)");
        }

        /// <summary><b>F (amendment 2 §5).</b> The dipping pool at its level, 4.960, as the game draws it
        /// today (no still water registered) and with the seabed's still water registered by the plate.</summary>
        [UnityTest]
        public IEnumerator DippingPool_AtItsLevel_WithAndWithoutStillWater()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            PondDef pool = LoadDef<PondDef>("Pool_GapDippingPool.asset");
            var plate = new Plate
            {
                Name = "f-dipping-pool", Centre = pool.Centre, Ortho = 5f,
                Subject = "St Peters: the dipping pool in the gap, at its level",
                Ids = $"{pool.Id} (surface {pool.Surface:0.000}, bed {pool.Bed:0.000}; ruled (a), 13:18:22Z)",
            };
            yield return PinTheTide(TideAt.Mean);
            yield return FrameFree(plate);
            yield return Shoot(plate, "mean-no-still-water", Pond(pool));
            yield return RegisterTheStillWater();
            yield return HoldStill(plate);
            yield return Shoot(plate, "mean-still-water-registered", Pond(pool));
        }

        /// <summary><b>G (amendment 2 §4.4, §5).</b> Ginny's track under the fall's rim.</summary>
        [UnityTest]
        public IEnumerator GinnysTrack_UnderTheFallsRim()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            var plate = new Plate
            {
                Name = "g-ginnys-track-under-the-rim", Centre = new Vector2(96f, 60f), Ortho = 10f,
                Subject = "St Peters: Ginny's track under the fall's rim",
                Ids = "path.stp_ginnys_track; ground.stp_alder_fall_cut; fall.stp_alder_fall",
            };
            yield return PinTheTide(TideAt.Mean);
            yield return FrameFree(plate);
            yield return Shoot(plate, "mean", null);
        }

        /// <summary><b>H (09-29 §4.3).</b> Ginny's plot and the fall, at low and at high water.</summary>
        [UnityTest]
        public IEnumerator GinnysPlot_AndTheFall_AtLowAndHighWater()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            var plate = new Plate
            {
                Name = "h-ginnys-plot-and-the-fall", Centre = new Vector2(95f, 62f), Ortho = 36f,
                Subject = "St Peters: Ginny's plot and the fall",
                Ids = "path.stp_ginnys_path; path.stp_ginnys_track; fall.stp_alder_fall; pond.stp_alder_plunge; coast.stp_ginnys_cove",
            };
            yield return FrameFree(plate);
            yield return PinTheTide(TideAt.SpringLow);
            yield return HoldStill(plate);
            yield return Shoot(plate, "spring-low", null);
            yield return PinTheTide(TideAt.SpringHigh);
            yield return HoldStill(plate);
            yield return Shoot(plate, "spring-high", null);
        }

        /// <summary><b>I (09-29 §4.3).</b> The Head and the NE bar, at a spring low and at mean tide.</summary>
        [UnityTest]
        public IEnumerator HeadAndNeBar_AtASpringLowAndMeanTide()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            var plate = new Plate
            {
                Name = "i-head-and-ne-bar", Centre = new Vector2(207f, 67f), Ortho = 40f,
                Subject = "St Peters: the Head and the NE bar",
                Ids = "ground.stp_ne_head; ground.stp_ne_neck; ground.stp_ne_platform; bar.stp_ne_bar",
            };
            yield return FrameFree(plate);
            yield return PinTheTide(TideAt.SpringLow);
            yield return HoldStill(plate);
            yield return Shoot(plate, "spring-low", null);
            yield return PinTheTide(TideAt.Mean);
            yield return HoldStill(plate);
            yield return Shoot(plate, "mean", null);
        }

        /// <summary><b>J (09-29 §4.3).</b> The crossing at a spring low: whole, then its gut.</summary>
        [UnityTest]
        public IEnumerator Crossing_AtASpringLow()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            var wide = new Plate
            {
                Name = "j1-crossing-wide", Centre = new Vector2(-213f, 0f), Ortho = 84.4f,
                Subject = "St Peters: the crossing to the mainland, whole",
                Ids = "coast.stp_the_bar and its tidal pools (pool.stp_bar_a01..., pool.stp_bar_b01...)",
            };
            var gut = new Plate
            {
                Name = "j2-crossing-gut", Centre = new Vector2(-234.1f, 0f), Ortho = 15f,
                Subject = "St Peters: the crossing's gut",
                Ids = "coast.stp_the_bar",
            };
            yield return PinTheTide(TideAt.SpringLow);
            yield return FrameFree(wide);
            yield return Shoot(wide, "spring-low", null);
            yield return FrameFree(gut);
            yield return Shoot(gut, "spring-low", null);
        }

        /// <summary><b>K1-K4 (09-29 §4.3).</b> The still water on the painted path, beside #890's plate: the
        /// Alder Run, the fall's pool and the Bog Pond drawn in full, and the restored Fen Pool. The still water is registered by the plate.</summary>
        [UnityTest]
        public IEnumerator StillWater_OnThePaintedPath_Registered()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            PondDef plunge = LoadDef<PondDef>("Pond_AlderPlunge.asset");
            PondDef bog = LoadDef<PondDef>("Pond_BogPond.asset");
            var plates = new[]
            {
                new Plate { Name = "k1-alder-run", Centre = new Vector2(104.5f, 82f), Ortho = 30f,
                            Subject = "St Peters: the Alder Run, drawn in full", Ids = "stream.stp_alder_run; fall.stp_alder_fall" },
                new Plate { Name = "k2-the-falls-pool", Centre = new Vector2(106f, 62f), Ortho = 6f,
                            Subject = "St Peters: the fall's pool", Ids = $"{plunge.Id} (surface {plunge.Surface:0.000}, bed {plunge.Bed:0.000}); fall.stp_alder_fall" },
                new Plate { Name = "k3-bog-pond", Centre = bog.Centre, Ortho = 7f,
                            Subject = "St Peters: the Bog Pond", Ids = $"{bog.Id} (surface {bog.Surface:0.000}, bed {bog.Bed:0.000})" },
                new Plate { Name = "k4-fen-pool-restored", Centre = new Vector2(108f, 51f), Ortho = 7f,
                            Subject = "St Peters: the restored Fen Pool at +5.25", Ids = "pond.stp_fen_pool; ground.stp_fen_pool_dry (retired by id)" },
            };
            yield return PinTheTide(TideAt.Mean);
            yield return RegisterTheStillWater();
            var fen = new Vector2(108f, 51f);
            string[] notes = { null, Pond(plunge), Pond(bog), $"STILL LEVEL at ({fen.x:0}, {fen.y:0}): {StillAt(fen)}; GROUND {Ground(fen):+0.000;-0.000} m" };
            for (int i = 0; i < plates.Length; i++)
            {
                yield return FrameFree(plates[i]);
                yield return Shoot(plates[i], "mean-still-water-registered", notes[i]);
            }
        }

        // Issue 3 frames reproduce tools/boards.json's x/v rectangles at 32 px per world unit.
        static Plate BoardPlate(string name, float x0, float x1, float v0, float v1, string ids) => new Plate
        {
            Name = name, Subject = "St Peters second issue: " + name, Ids = ids,
            Centre = new Vector2((x0 + x1) * 0.5f, (v0 + v1) * 0.5f), Ortho = (v1 - v0) * 0.5f,
            Width = Mathf.RoundToInt((x1 - x0) * BoardPxPerMetre), Height = Mathf.RoundToInt((v1 - v0) * BoardPxPerMetre),
        };

        [UnityTest]
        public IEnumerator SecondIssue_FenAndAlderHead_AtTheBoardsNoon()
        {
            RequireAGraphicsDevice(); yield return LoadStPeters();
            yield return RegisterTheStillWater(); yield return PinTheTide(TideAt.BoardNoon, 12f);
            var p = BoardPlate("issue3-fen-alder-noon", 91f, 121f, 50.053f, 70.053f,
                "pond.stp_fen_pool; stream.stp_alder_run; fall.stp_alder_fall");
            yield return FrameFree(p); yield return Shoot(p, "noon", "CD: parity/alder-fall-noon.png; exact board frame and noon clock; live tide recorded above.");
            Assert.AreEqual(5.25f, GameServices.StillWater.StillLevelAt(new Vector2(108f, 51f)), 0.001f, "the restored fen's centre holds its surface");
        }

        [UnityTest]
        public IEnumerator SecondIssue_HeadPools_AtLowAndHighWater()
        {
            RequireAGraphicsDevice(); yield return LoadStPeters(); yield return RegisterTheStillWater();
            var frames = new[] {
                BoardPlate("issue3-head-neck",158f,206f,40f,72f,"pool.stp_ne_platform_01..14"),
                BoardPlate("issue3-head-all-pools",168f,230f,34f,76f,"pool.stp_ne_platform_01..14") };
            foreach (var tide in new[] { TideAt.SpringLow, TideAt.SpringHigh })
            {
                yield return PinTheTide(tide,15.5f);
                foreach(var p in frames) { yield return FrameFree(p); yield return Shoot(p,tide.ToString(),"CD: places/head-neck.png; board frame plus overview covering all 14 pools."); }
            }
        }

        [UnityTest]
        public IEnumerator SecondIssue_LedgesPools_AtLowWater()
        {
            RequireAGraphicsDevice(); yield return LoadStPeters(); yield return RegisterTheStillWater();
            yield return PinTheTide(TideAt.SpringLow,15.5f);
            var frames = new[] {
                BoardPlate("issue3-west-ledges",-41f,-8f,-72f,-26f,"pool.stp_west_ledges_01..05"),
                BoardPlate("issue3-east-ledges",92f,148f,-84f,-46f,"pool.stp_east_ledges_01..05") };
            foreach(var p in frames) { yield return FrameFree(p); yield return Shoot(p,"spring-low","CD: places/west-ledges.png or places/east-ledges.png; same board frame and 15:30 light; board is mean tide, this plate exposes the pools at low tide."); }
        }

        [UnityTest]
        public IEnumerator SecondIssue_HeathMouth_AndLoftSpur()
        {
            RequireAGraphicsDevice(); yield return LoadStPeters(); yield return RegisterTheStillWater();
            yield return PinTheTide(TideAt.Mean,15.5f);
            var frames = new[] {
                BoardPlate("issue3-heath-mouth",4f,23f,-85f,-64f,"stream.stp_heath_brook"),
                BoardPlate("issue3-loft-spur",177f,194f,11f,30f,"path.stp_loft_spur") };
            foreach(var p in frames) { yield return FrameFree(p); yield return Shoot(p,"mean","Heath's 19 authored stations meet mean tide; loft spur is paint only."); }
        }

        /// <summary><b>L (the v49 update's item 6).</b> The two beach trees, re-seated on the new ground:
        /// the scene stores no heights, and the ground under each moved by under a millimetre.</summary>
        [UnityTest]
        public IEnumerator BeachTrees_OnTheNewGround()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            var plate = new Plate
            {
                Name = "l-the-two-beach-trees", Centre = new Vector2(49.5f, -57f), Ortho = 6f,
                Subject = "St Peters: the two beach trees on the new ground",
                Ids = "BlackSpruce_1, BlackSpruce_2 (scene objects; the ground's +0.18 m rise starts about 2 m south of them)",
            };
            yield return PinTheTide(TideAt.Mean);
            yield return FrameFree(plate);
            var trees = new StringBuilder();
            foreach (Transform t in Object.FindObjectsByType<Transform>())
            {
                if (t == null || (t.name != "BlackSpruce_1" && t.name != "BlackSpruce_2")) continue;
                Vector2 at = t.position;
                trees.AppendLine($"TREE '{t.name}' at ({at.x:0.00}, {at.y:0.00}) in '{t.gameObject.scene.name}': ground {Ground(at):+0.0000;-0.0000} m on the committed map");
            }
            yield return Shoot(plate, "mean", trees.Length > 0 ? trees.ToString().TrimEnd() : "TREES: neither BlackSpruce_1 nor BlackSpruce_2 is in the loaded scene");
        }

        // =============================================================================================
        //  TERRAIN PR 5w: the walls by their real ids, on this ground (the 10-01 charter §4.6). Each frame
        //  at a spring low and at mean tide; each caption names every wall in it, with its Def's state.
        // =============================================================================================

        /// <summary><b>W1 (PR 5w).</b> The main beach whole, then its banked ends: the west end, where 056
        /// and 057 bank with 057's new chunks 167-169, and the east end, where 042 banks and 043 returns.
        /// 044-055 are retired: no wall stands across the dune.</summary>
        [UnityTest]
        public IEnumerator W1_MainBeach_TheBankedEndsAndTheReturn()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            yield return AtASpringLowAndMean(
                new Plate { Name = "w1a-main-beach-whole", Centre = new Vector2(33.5f, -66f), Ortho = 24f,
                            Subject = "St Peters (PR 5w): the main beach whole, its banked ends and 043's return",
                            Ids = "walls 042, 056, 057 and 167-169 banked; 043 the return; 041, 058 kept; 044-055 retired" },
                new Plate { Name = "w1b-beach-west-end-banked", Centre = new Vector2(-3f, -58f), Ortho = 8f,
                            Subject = "St Peters (PR 5w): the beach's west end, where 056 and 057 bank",
                            Ids = "walls 056, 057 and 057's new chunks 167-169 banked; 058 kept; 055 retired" },
                new Plate { Name = "w1c-beach-east-end-banked", Centre = new Vector2(69.5f, -72f), Ortho = 8f,
                            Subject = "St Peters (PR 5w): the beach's east end, where 042 banks and 043 returns",
                            Ids = "wall 042 banked; 043 the return; 040, 041 kept; 044 retired" });
        }

        /// <summary><b>W2 (PR 5w).</b> The East Ledges: 028-038 re-lined on this ground, cut into their new
        /// chunks 147-166, between 027 and 039 (kept).</summary>
        [UnityTest]
        public IEnumerator W2_EastLedges_TheReLinedWallsAndTheirCuts()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            yield return AtASpringLowAndMean(
                new Plate { Name = "w2-east-ledges", Centre = new Vector2(117.7f, -63.2f), Ortho = 17f,
                            Subject = "St Peters (PR 5w): the East Ledges' re-lined walls and their cuts",
                            Ids = "walls 028-038 re-lined, with their new chunks 147-166; 027, 039 kept" });
        }

        /// <summary><b>W3 (PR 5w).</b> The West Ledges: 059-067 re-lined on this ground, cut into their new
        /// chunks 170-188, from 057's bank to the bluff's lifted toes.</summary>
        [UnityTest]
        public IEnumerator W3_WestLedges_TheReLinedWallsAndTheirCuts()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            yield return AtASpringLowAndMean(
                new Plate { Name = "w3-west-ledges", Centre = new Vector2(-22.6f, -44.6f), Ortho = 12f,
                            Subject = "St Peters (PR 5w): the West Ledges' re-lined walls and their cuts",
                            Ids = "walls 059-067 re-lined, with their new chunks 170-188; 057, 169 banked; 058 kept; 068, 069 toe lifted" });
        }

        /// <summary><b>W4 (PR 5w).</b> The South-West Bluff: 068-071 with their toes lifted, between 067's new
        /// chunks 185-188 and 072 (kept).</summary>
        [UnityTest]
        public IEnumerator W4_SouthWestBluff_TheLiftedToes()
        {
            RequireAGraphicsDevice();
            yield return LoadStPeters();
            yield return AtASpringLowAndMean(
                new Plate { Name = "w4-sw-bluff", Centre = new Vector2(-38.8f, -31.3f), Ortho = 7f,
                            Subject = "St Peters (PR 5w): the South-West Bluff, 068-071 with their toes lifted",
                            Ids = "walls 068-071 toe lifted; 066, 067 and 067's new chunks 185-188 re-lined; 072-074 kept" });
        }

        /// <summary>PR 5w's frames: each plate at a spring low, then each at mean tide. The tide is pinned
        /// once per level, and each frame is held still before its shot.</summary>
        IEnumerator AtASpringLowAndMean(params Plate[] plates)
        {
            TideAt[] tides = { TideAt.SpringLow, TideAt.Mean };
            string[] tags = { "spring-low", "mean" };
            for (int t = 0; t < tides.Length; t++)
            {
                yield return PinTheTide(tides[t]);
                foreach (Plate p in plates)
                {
                    yield return FrameFree(p);
                    yield return Shoot(p, tags[t], null);
                }
            }
        }

        // =============================================================================================
        //  The region, the tide, the still water and the frame
        // =============================================================================================

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED: no graphics device (Null Device), so nothing rendered " +
                              "and nothing was proved. Expected on CI; a plate of the ground needs a GPU.");
        }

        /// <summary>
        /// Load St Peters the way the existing plates do, then wait for the shell to settle at the title.
        /// ⚠ THE SAVE: the save service writes only once the shell has left the title, so a region that
        /// boots to it and a fixture that never leaves it cannot touch the owner's save.
        /// </summary>
        IEnumerator LoadStPeters()
        {
            if (!_loadedAny) _introOnEntry = GameServices.OpeningCinematicRunning;

            GameConfig configBefore = GameServices.Config;
            IGameClock clockBefore = GameServices.Clock;
            IEnvironmentService envBefore = GameServices.Environment;
            IWallet walletBefore = GameServices.Wallet;

            LogAssert.ignoreFailingMessages = true;   // the region logs decor complaints of its own
            _loadedAny = true;
            yield return SceneManager.LoadSceneAsync(StPeters, LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;   // let the self-installing components register

            var healed = new List<string>();
            if (GameServices.Config == null && configBefore != null) { GameServices.Config = configBefore; healed.Add("Config"); }
            if (GameServices.Clock == null && clockBefore != null) { GameServices.Clock = clockBefore; healed.Add("Clock"); }
            if (GameServices.Environment == null && envBefore != null) { GameServices.Environment = envBefore; healed.Add("Environment"); }
            if (GameServices.Wallet == null && walletBefore != null) { GameServices.Wallet = walletBefore; healed.Add("Wallet"); }
            if (healed.Count > 0)
            {
                _healed = $"{StPeters}: re-published {string.Join(", ", healed)} after the region's dev core took them down";
                Debug.Log($"[{PlateDir}] {_healed}.");
            }

            int frames = 0;
            while (frames < 240 && (FindPersistentFollow() == null || !ShellFlow.AtTitle))
            {
                yield return null;
                frames++;
            }
            if (!ShellFlow.AtTitle)
                Assert.Ignore($"SKIPPED: {StPeters} did not settle at the title (the shell is {ShellFlow.Phase}), " +
                              "so the world is live and the save could be written. A plate never risks the save.");
            Assert.IsNotNull(FindPersistentFollow(),
                $"{StPeters}: after 240 frames there is no CameraFollow on the persistent core's camera, so there " +
                "is no play camera to shoot through.");
            _seabed = UnityEditor.AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(StPetersTerrainPlan.SeabedPath);
            Assert.IsNotNull(_seabed, $"no seabed map at {StPetersTerrainPlan.SeabedPath}.");
            Debug.Log($"[{PlateDir}] {StPeters} loaded: region '{GameServices.CurrentRegionId}', shell {ShellFlow.Phase}, " +
                      $"camera up after {frames} frames.");
        }

        /// <summary>
        /// Find the asked tide in daylight over days 1..30, stop the clock on it, and let the world run until
        /// the drawn sea stands at that level and the day/night tint has settled. The clock reads wall time
        /// and stops on its own TimeScale; the sea eases its level on scaled time, so the world keeps running.
        /// </summary>
        IEnumerator PinTheTide(TideAt which, float? exactHour = null)
        {
            IGameClock clock = GameServices.Clock;
            IEnvironmentService env = GameServices.Environment;
            GameConfig config = GameServices.Config;
            Assert.IsNotNull(clock, "the region registered no clock, so the hour cannot be pinned.");
            Assert.IsNotNull(env, "the region registered no environment, so the tide cannot be read.");
            Assert.IsNotNull(config, "the region registered no GameConfig, so a day has no length.");

            double spd = config.SecondsPerDay;
            double best = -1.0;
            float bestLevel = 0f, low = float.MaxValue, high = float.MinValue;
            float bestHourGap = float.MaxValue;
            double meanFallback = -1.0;
            float meanFallbackAbs = float.MaxValue;
            for (int day = FirstDay; day <= LastDay; day++)
                for (int m = FirstMinute; m <= LastMinute; m++)
                {
                    if (exactHour.HasValue && m != Mathf.RoundToInt(exactHour.Value * 60f)) continue;
                    double t = (day + m / 1440.0) * spd;
                    float level = env.WaterLevelAt(t);
                    low = Mathf.Min(low, level);
                    high = Mathf.Max(high, level);
                    switch (which)
                    {
                        case TideAt.BoardNoon:
                            float boardGap = Mathf.Abs(level - 0.63f); // boards.json: alder-fall-noon, t=12, tide=0.63
                            if (best < 0.0 || boardGap < bestHourGap) { best = t; bestLevel = level; bestHourGap = boardGap; }
                            break;
                        case TideAt.SpringLow:
                            if (best < 0.0 || level < bestLevel) { best = t; bestLevel = level; }
                            break;
                        case TideAt.SpringHigh:
                            if (best < 0.0 || level > bestLevel) { best = t; bestLevel = level; }
                            break;
                        default:
                            float abs = Mathf.Abs(level);
                            if (abs < meanFallbackAbs) { meanFallbackAbs = abs; meanFallback = t; }
                            float gap = Mathf.Abs(m / 60f - MeanHourWanted);
                            if (abs <= MeanBandMetres && gap < bestHourGap) { bestHourGap = gap; best = t; bestLevel = level; }
                            break;
                    }
                }
            if (best < 0.0) { best = meanFallback; bestLevel = env.WaterLevelAt(best); }

            Time.timeScale = 1f;
            clock.SeekTo(best);
            clock.TimeScale = 0f;
            Assert.LessOrEqual(System.Math.Abs(clock.TotalSeconds - best), 1.0,
                $"the clock did not land on {best:0.0} s (it reads {clock.TotalSeconds:0.0} s), so the plate would " +
                "be shot at some other tide.");

            int seaLevelId = Shader.PropertyToID("_HHSeaLevelWorld");
            float target = env.WaterLevelAt(clock.TotalSeconds);
            Color tint = Shader.GetGlobalColor("_DayNightTint");
            float waited = 0f;
            int still = 0, frames = 0;
            Vector4 sea = Vector4.zero;
            while (waited < SeaLevelWaitSeconds)
            {
                yield return null;
                frames++;
                waited += Time.unscaledDeltaTime;
                Color now = Shader.GetGlobalColor("_DayNightTint");
                float move = Mathf.Abs(now.r - tint.r) + Mathf.Abs(now.g - tint.g) + Mathf.Abs(now.b - tint.b);
                still = move < 1e-4f ? still + 1 : 0;
                tint = now;
                sea = Shader.GetGlobalVector(seaLevelId);
                bool seaThere = sea.w > 0.5f && Mathf.Abs(sea.x - target) <= SeaLevelTolerance;
                if (waited >= 1f && still >= 4 && seaThere) break;
            }
            Assert.Less(waited, SeaLevelWaitSeconds,
                $"after {waited:0.0} s the drawn sea reads {sea.x:+0.000;-0.000} m (published {sea.w > 0.5f}) against " +
                $"the {target:+0.000;-0.000} m asked, or the day/night tint never settled.");

            float hour = clock.HourOfDay;
            int hh = Mathf.FloorToInt(hour), mm = Mathf.FloorToInt((hour - hh) * 60f);
            string how = which == TideAt.BoardNoon ? "the noon nearest CD board tide +0.63 m" : which == TideAt.SpringLow ? "the lowest" : which == TideAt.SpringHigh ? "the highest" :
                         $"the minute within {MeanBandMetres * 100f:0} cm of the datum nearest {MeanHourWanted:0.0} h";
            _pinned = $"{which}: day {clock.DayIndex} {hh:00}:{mm:00} ({hour:0.000} h, TotalSeconds {clock.TotalSeconds:0.0}, " +
                      $"SecondsPerDay {spd:0}); water {target:+0.000;-0.000} m, {how} of days {FirstDay}-{LastDay}, " +
                      $"{(exactHour.HasValue ? exactHour.Value.ToString("0.00") + " h fixed" : (FirstMinute / 60).ToString("00") + ":00-" + (LastMinute / 60).ToString("00") + ":00")} (that window runs {low:+0.000;-0.000} to {high:+0.000;-0.000} m); " +
                      $"drawn sea {sea.x:+0.0000;-0.0000} m after {frames} frames / {waited:0.00} s; " +
                      $"_DayNightTint ({tint.r:0.000}, {tint.g:0.000}, {tint.b:0.000})";
            Debug.Log($"[{PlateDir}] pinned: {_pinned}");
        }

        /// <summary>
        /// Register the committed seabed's still water as the game's (what PaintedTidalTerrain will do in the
        /// next PR), and wait until the shader globals carry it.
        /// </summary>
        IEnumerator RegisterTheStillWater()
        {
            PaintedStillWater still = _seabed.StillWater;
            Assert.IsNotNull(still, $"{StPetersTerrainPlan.SeabedPath} decodes no still water (no readable still-level texture).");
            if (!_stillChanged)
            {
                _stillOnEntry = GameServices.StillWater;
                _stillChanged = true;
            }
            GameServices.StillWater = still;
            for (int i = 0; i < 4; i++) yield return null;
            StillWaterMap map = still.Map;
            Assert.IsTrue(StillWaterGlobals.IsBound,
                $"the still water is registered but the shader globals are unset ({StillWaterGlobals.HolderCount} holders), so no pond is drawn.");
            Texture tex = _seabed.StillLevelTexture;
            _still = $"REGISTERED BY THIS PLATE (not by the game: T11 is the next PR): GameServices.StillWater = " +
                     $"PaintedHeightMap.StillWater of {StPetersTerrainPlan.SeabedPath} ('{(tex != null ? tex.name : "?")}' " +
                     $"{(tex != null ? tex.width + "x" + tex.height : "?")}, levels {map.MinLevel:0.###}..{map.MaxLevel:0.###} m over " +
                     $"min ({map.WorldMin.x:0.#}, {map.WorldMin.y:0.#}) size ({map.WorldSize.x:0.#}, {map.WorldSize.y:0.#})); " +
                     $"StillWaterGlobals bound, {StillWaterGlobals.HolderCount} holder(s)";
            Debug.Log($"[{PlateDir}] {_still}");
        }

        /// <summary>
        /// Take the play camera off its follow and its pixel-perfect sizing, place it on the plate's centre at
        /// the plate's ortho size with the plate's render texture, and prove it holds still.
        /// </summary>
        IEnumerator FrameFree(Plate p)
        {
            if (!_cameraCaptured)
            {
                _follow = FindPersistentFollow();
                Assert.IsNotNull(_follow, "no CameraFollow on the persistent core's camera.");
                _cam = _follow.GetComponent<Camera>();
                Assert.IsNotNull(_cam, "the play camera's CameraFollow has no Camera.");
                _ppc = null;
                foreach (Behaviour b in _cam.GetComponents<Behaviour>())
                    if (b != null && b.GetType().Name == "PixelPerfectCamera") { _ppc = b; break; }
                _followEnabledOnEntry = _follow.enabled;
                _ppcEnabledOnEntry = _ppc != null && _ppc.enabled;
                _orthoOnEntry = _cam.orthographicSize;
                _camPosOnEntry = _cam.transform.position;
                _cameraCaptured = true;
            }
            _follow.enabled = false;
            if (_ppc != null) _ppc.enabled = false;

            if (_rt == null || _w != p.Width || _h != p.Height)
            {
                _cam.targetTexture = null;
                if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }
                _w = p.Width;
                _h = p.Height;
                _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf);
                _rt.Create();
            }
            _cam.targetTexture = _rt;
            _cam.ResetAspect();
            _cam.orthographic = true;
            _cam.orthographicSize = p.Ortho;
            _cam.transform.position = new Vector3(p.Centre.x, p.Centre.y, _cam.transform.position.z);
            yield return HoldStill(p);
        }

        /// <summary>Run the world for 30 frames and prove nothing else moved or resized the camera.</summary>
        IEnumerator HoldStill(Plate p)
        {
            for (int i = 0; i < StillFramesAsked; i++) yield return null;
            Vector3 at = _cam.transform.position;
            Assert.LessOrEqual(Vector2.Distance(new Vector2(at.x, at.y), p.Centre), StayedPutMetres,
                $"{p.Name}: the free camera moved to ({at.x:0.####}, {at.y:0.####}) from ({p.Centre.x:0.###}, {p.Centre.y:0.###}) " +
                $"in {StillFramesAsked} frames with its follow off, so something else drives it and the plate would show other ground.");
            Assert.AreEqual(p.Ortho, _cam.orthographicSize, 1e-4f,
                $"{p.Name}: the free camera's ortho size went from {p.Ortho:0.###} to {_cam.orthographicSize:0.###} with its " +
                "pixel-perfect sizing off, so something else sizes it.");
            Assert.AreSame(_rt, _cam.targetTexture, $"{p.Name}: something took the plate's render texture off the camera.");
            _framed = $"free camera on ({p.Centre.x:0.###}, {p.Centre.y:0.###}) at ortho {p.Ortho:0.###}; CameraFollow and " +
                      $"PixelPerfectCamera off (on entry: follow {_followEnabledOnEntry}, PPC {_ppcEnabledOnEntry}; both put back " +
                      $"in the teardown); held still for {StillFramesAsked} frames";
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

        // =============================================================================================
        //  What the caption reads off the committed data
        // =============================================================================================

        static T LoadDef<T>(string file) where T : Object
        {
            T def = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(PlanDir + file);
            Assert.IsNotNull(def, $"no {typeof(T).Name} at {PlanDir + file}.");
            return def;
        }

        float Ground(Vector2 at)
        {
            PaintedHeightField field = _seabed != null ? _seabed.Field : null;
            return field != null ? field.ElevationAt(at) : float.NaN;
        }

        static string StillAt(Vector2 at)
        {
            float level = GameServices.StillWater.StillLevelAt(at);
            return float.IsNegativeInfinity(level) || float.IsNaN(level) ? "none" : $"{level:+0.000;-0.000} m";
        }

        string Pond(PondDef d) =>
            $"POND {d.Id} at ({d.Centre.x:0.00}, {d.Centre.y:0.00}): Def surface {d.Surface:0.000}, bed {d.Bed:0.000}; " +
            $"the committed map's ground there {Ground(d.Centre):+0.000;-0.000} m; still level there {StillAt(d.Centre)}";

        /// <summary>
        /// The cliff walls in the frame, by real id, each with what PR 5w did to it, read from its Def
        /// (<see cref="StateOf"/>).
        /// </summary>
        string WallsInFrame()
        {
            float halfH = _cam.orthographicSize, halfW = halfH * _cam.aspect;
            Vector3 c = _cam.transform.position;
            var view = Rect.MinMaxRect(c.x - halfW, c.y - halfH, c.x + halfW, c.y + halfH);
            var rows = new List<string>();
            foreach (Transform t in Object.FindObjectsByType<Transform>())
            {
                if (t == null || !t.name.StartsWith(WallPrefix, System.StringComparison.Ordinal)) continue;
                Renderer[] rs = t.GetComponentsInChildren<Renderer>(true);
                Rect r;
                if (rs.Length > 0)
                {
                    Bounds b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    r = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
                }
                else r = new Rect(t.position.x, t.position.y, 0f, 0f);
                if (!r.Overlaps(view, true)) continue;
                string tail = t.name.Length >= 3 ? t.name.Substring(t.name.Length - 3) : "";
                string status = int.TryParse(tail, out _) ? StateOf(tail) : "no id";
                rows.Add($"{t.name} ({status})");
            }
            rows.Sort(System.StringComparer.Ordinal);
            return rows.Count == 0 ? "none in frame" : string.Join("; ", rows);
        }

        Dictionary<string, CliffWallDef> _wallDefs;   // the walls' Defs by real id: content, so one load serves every case

        /// <summary>
        /// What PR 5w did to a wall, from its Def in <c>Data/Terrain/StPetersWalls/</c>: kept, re-lined, banked,
        /// the return or toe lifted, and for a new chunk the wall it was cut from. A retired or held wall stands
        /// in no scene, so one in a frame is named as a fault.
        /// </summary>
        string StateOf(string realId)
        {
            if (_wallDefs == null)
            {
                _wallDefs = new Dictionary<string, CliffWallDef>();
                foreach (CliffWallDef d in StPetersCliffWalls.LoadDefs()) _wallDefs[d.RealId] = d;
            }
            if (!_wallDefs.TryGetValue(realId, out CliffWallDef def)) return $"{realId}: NO DEF";
            string state;
            switch (def.Status)
            {
                case CliffWallStatus.Kept: state = "kept"; break;
                case CliffWallStatus.Relined: state = "re-lined"; break;
                case CliffWallStatus.Banked: state = "banked"; break;
                case CliffWallStatus.Return: state = "the return"; break;
                case CliffWallStatus.ToeLifted: state = "toe lifted"; break;
                default: state = $"{def.Status.ToString().ToUpperInvariant()}, AND STANDING: it should stand in no scene"; break;
            }
            if (!string.IsNullOrEmpty(def.SplitFrom) && def.SplitFrom.Length >= 3)
                state += $", new, cut from {def.SplitFrom.Substring(def.SplitFrom.Length - 3)}";
            return $"{realId} {state}";
        }

        // =============================================================================================
        //  Shooting
        // =============================================================================================

        IEnumerator Shoot(Plate p, string tag, string extra)
        {
            FindTheGround(p.Centre);
            yield return ShadersReady();
            byte[] picture = Capture();
            byte[] control = CaptureWithoutTheTerrain();
            ulong hPicture = Fnv1a(picture), hControl = Fnv1a(control);
            double share = ChangedShare(picture, control);
            string name = $"{p.Name}-{tag}";
            SavePlate(name + ".png", picture);
            SaveCaption(name + ".txt", Caption(p, name, hPicture, hControl, share, extra));
            Debug.Log($"[{PlateDir}] {name}: {share:P1} of the frame is the ground's; walls in frame: {WallsInFrame()}");
            Assert.AreNotEqual(hPicture, hControl,
                $"{name}: the frame is identical with the terrain switched off, so the ground is not in the picture.");
            Assert.Greater(share, 0.05,
                $"{name}: switching the terrain off changed only {share:P1} of the frame, so the ground is not in the picture.");
        }

        /// <summary>
        /// A plate is a picture of the shaders, not of their cyan stand-ins. Draw the frame once (with and
        /// without the terrain) so every variant it needs has been asked for, then let the editor's background
        /// compiler finish before the shot. Not <c>ShaderUtil.allowAsyncCompilation = false</c>: compiling in
        /// place hung a batch PlayMode run on D3D12 at its first capture (as <c>VillageReturnPlatePlayTests</c>).
        /// </summary>
        IEnumerator ShadersReady()
        {
            _cam.Render();
            _ground.enabled = false;
            try { _cam.Render(); }
            finally { _ground.enabled = true; }
            float until = Time.realtimeSinceStartup + ShaderWaitSeconds;
            int frames = 0;
            while (UnityEditor.ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < until)
            {
                yield return null;
                frames++;
            }
            if (UnityEditor.ShaderUtil.anythingCompiling)
                Debug.LogWarning($"[{PlateDir}] shaders still compiling after {frames} frame(s) ({ShaderWaitSeconds} s): " +
                                 "this shot may hold a stand-in.");
            else if (frames > 0)
                Debug.Log($"[{PlateDir}] waited {frames} frame(s) for the shaders to compile before the shot.");
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

        /// <summary>The share of pixels that differ by more than 2/255 in any channel.</summary>
        double ChangedShare(byte[] a, byte[] b)
        {
            long changed = 0, total = (long)_w * _h;
            for (long i = 0; i < total * 4; i += 4)
                if (Mathf.Abs(a[i] - b[i]) > 2 || Mathf.Abs(a[i + 1] - b[i + 1]) > 2 || Mathf.Abs(a[i + 2] - b[i + 2]) > 2)
                    changed++;
            return total > 0 ? (double)changed / total : 0.0;
        }

        string Caption(Plate p, string name, ulong hPicture, ulong hControl, double share, string extra)
        {
            Vector3 c = _cam.transform.position;
            float height = _cam.orthographicSize * 2f, width = height * _cam.aspect;
            Bounds b = _ground.bounds;
            var sb = new StringBuilder();
            sb.AppendLine(name);
            sb.AppendLine(p.Subject);
            sb.AppendLine($"ids: {p.Ids}");
            sb.AppendLine($"run: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z, Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceType} on {SystemInfo.graphicsDeviceName}. Hashes are an identity only within this run.");
            sb.AppendLine($"scene: {SceneManager.GetActiveScene().name}; region '{GameServices.CurrentRegionId}'; shell {ShellFlow.Phase} (the save is not written there); services healed: {_healed}");
            sb.AppendLine($"camera: '{_cam.name}' in scene '{_cam.gameObject.scene.name}', persistent {MoodGradeDirector.IsPersistentCamera(_cam)}; {_framed}");
            sb.AppendLine($"frame: x {c.x - width * 0.5f:0.###}..{c.x + width * 0.5f:0.###}, y {c.y - height * 0.5f:0.###}..{c.y + height * 0.5f:0.###} m " +
                          $"({width:0.###} x {height:0.###} m), render texture {_w}x{_h} px, {_h / height:0.###} px per metre");
            sb.AppendLine($"tide and hour: {_pinned}");
            sb.AppendLine($"still water: {_still}");
            sb.AppendLine($"ground at the centre ({p.Centre.x:0.###}, {p.Centre.y:0.###}): {Ground(p.Centre):+0.000;-0.000} m on {StPetersTerrainPlan.SeabedPath} (the map the terrain draws)");
            sb.AppendLine($"cliff walls in frame (by real id, each with what PR 5w did to it, from its Def): {WallsInFrame()}");
            sb.AppendLine($"ground: '{_surface.name}', quad bounds x {b.min.x:0.#}..{b.max.x:0.#}, y {b.min.y:0.#}..{b.max.y:0.#}");
            sb.AppendLine($"OpeningCinematicRunning {GameServices.OpeningCinematicRunning}; Time.timeScale {Time.timeScale}");
            sb.AppendLine($"picture {hPicture:x16}; control with the terrain off {hControl:x16}; {share:P1} of the frame changed");
            if (!string.IsNullOrEmpty(extra)) sb.AppendLine(extra);
            return sb.ToString();
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
