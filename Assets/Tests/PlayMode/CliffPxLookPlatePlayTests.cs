using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE PX CLIFF LOOK, PHOTOGRAPHED AT THE PLAY CAMERA</b>: the owner judges the cliff kit's px look
    /// from these plates, the same frames at the same hours shot by this class in two runs — the v10 look,
    /// then (after <c>Bake Cliff Kit — px</c>) the px look — and composed side by side by the second run.
    ///
    /// <para><b>THE FRAMES.</b> Nine stations on both coasts, every one a wall of till over sandstone: Nine
    /// Mile Creek's tallest east wall (brow and toe) and an east ramp; St Peters' south wall (brow and
    /// toe), a steep east face, the south-east and south-west faces and a ledge. Each goes through the
    /// persistent core's camera and its own <see cref="CameraFollow"/> at the on-foot framing (8.4375 m
    /// tall at 1080 px, the view a player has), with her stood beside the wall for scale.</para>
    ///
    /// <para><b>THE HOURS.</b> 09:00 (morning sun on the east faces, beside day 1's daylight low water, so
    /// the toes are bare), 13:00 (solar noon), 19:30 (golden hour, the sun still up), 20:30 (dusk, the sun
    /// down) and 02:00 the next night. The clock only runs forward inside a case, so every station due at
    /// an hour is shot at that hour before the clock moves on.</para>
    ///
    /// <para><b>THE PROOF A PLATE HAS A SUBJECT.</b> Every capture is paired, in the SAME frame, with a
    /// control shot with every cliff renderer switched off, and the two must differ; the named wall's
    /// bands and her figure must be inside the frame. Hashes are an identity only within one run.</para>
    ///
    /// <para><b>THE LOOK, THREE WAYS.</b> The walls' shared material's keyword, the colour slot each band
    /// actually samples (<c>_index</c> in px, <c>_unlit</c> in v10) and the material's palette LUT (bound
    /// in px only) must agree, or the plate would be labelled with a look it does not show.</para>
    ///
    /// <para><b>THE −1 ARM (px only).</b> The shader builds its light per wall and leaves the kit's
    /// <c>_AspectShift</c> at 0. To show the owner what −1 on the east faces would look like, the arm
    /// reads each east band's committed <c>_index</c> bytes, lowers the tier of every rock texel by one
    /// (R −= 1 where B = 255 and R mod 6 &gt; 0 — identical to the kit's shift, clamping at tier 0) and
    /// shoots the frame with that copy on the band's property block, then puts the original back. An
    /// UNSHIFTED copy is shot in the same frame too, and must draw the identical picture, or the arm's
    /// difference is not the shift's. (Phase A planned to toggle the wall component instead; a toggle
    /// rebuilds the bands through play mode's deferred Destroy, so the arm swaps the property block and
    /// reads it back.)</para>
    ///
    /// <para><b>THE NUMBERS (13:00, two stations).</b> Draw calls and batches with the cliffs on and off
    /// (NOT MEASURED when the recorder reads 0), a structural census of the cliff renderers in frame, and
    /// the runtime memory of the textures they bind plus the LUT. The px run compares its census with the
    /// v10 run's.</para>
    ///
    /// <para>⚠ No GPU on CI: every case skips there before it loads anything, and the teardown is inert
    /// unless a region was loaded. A box with no baked faces skips too. Plates land in
    /// <c>Application.temporaryCachePath/CliffPxLookPlates/</c>, which every worktree shares, so the v10
    /// run leaves a run token (this box, its start) and the px run composes only against v10 plates that
    /// token's run wrote.</para>
    /// </summary>
    public class CliffPxLookPlatePlayTests
    {
        const string PlateDir = "CliffPxLookPlates";
        const string CleanupSceneName = "CliffPxLookCleanup";
        const string StPeters = "StPeters";
        const string NineMileCreek = "NineMileCreek";
        const string RunTokenFile = "run-v10.txt";

        // CliffCatalog.PxKeyword, CliffCatalog.MaterialPath, CliffCatalog.IndexChannel and UnlitChannel
        // (Art.Editor, CliffCatalog.cs), spelled here because this assembly does not reference Art.Editor.
        const string PxKeyword = "_HH_CLIFF_PX";
        const string CliffMaterialPath = "Assets/_Project/Art/Materials/CliffFace.mat";
        const string IndexSuffix = "_index";
        const string UnlitSuffix = "_unlit";

        const int PlateHeightPx = CameraFollow.DesignScreenHeightPx;   // 1080: the PPC zoom depends on it
        const int PlateWidthPx = 1920;
        // The follow eases its look-ahead out at 3/s: under half a zoom-4 pixel in ~2.2 s. Three times that.
        const float LeadEaseOutSeconds = 6f;
        // ON the centre: far inside the 1/128 m step of the grid the camera snaps to at zoom 4.
        const float OnTheCentreMetres = 0.001f;
        const float SouthDegrees = 180f;   // her heading: facing the camera
        // Switching every cliff off must change at least this share of the frame, or the cliff is not
        // the subject. Every station frames a face over most of its height.
        const double CliffShareFloor = 0.05;
        // The px index's legal range (the kit's contract): 25 LUT rows, 5 bands, B a rock flag.
        const int MaxIndexRow = 24, MaxIndexBand = 4, TierRows = 6;

        static readonly int IdUnlit = Shader.PropertyToID("_Unlit");
        static readonly int IdNormal = Shader.PropertyToID("_Normal");
        static readonly int IdMask = Shader.PropertyToID("_Mask");
        static readonly int IdPalette = Shader.PropertyToID("_Palette");
        static readonly int IdPxDecal = Shader.PropertyToID("_PxDecal");

        // =============================================================================================
        //  The stations (Phase A §4.2: every centre on the half metre, on every grid the camera snaps to)
        // =============================================================================================

        sealed class Station
        {
            public readonly string Shot, Wall, Subject;
            public readonly Vector2 Centre;
            public readonly Vector2? Figure;

            public Station(string shot, string wall, Vector2 centre, Vector2? figure, string subject)
            {
                Shot = shot; Wall = wall; Centre = centre; Figure = figure; Subject = subject;
            }
        }

        readonly struct Take
        {
            public readonly Station Station;
            public readonly bool Arm, Census;
            public Take(Station station, bool arm, bool census) { Station = station; Arm = arm; Census = census; }
        }

        static Take At(Station s, bool arm = false, bool census = false) => new Take(s, arm, census);

        static readonly Station N1 = new Station("n1-nmc-east-wall-brow", "CliffWall_DeepShoreCliff_E_steep_071",
            new Vector2(51.5f, 88f), new Vector2(50.21f, 89.55f),
            "Nine Mile Creek: the tallest east wall (drop 12.00 m), its upper face, her for scale");
        static readonly Station N2 = new Station("n2-nmc-east-wall-toe", "CliffWall_DeepShoreCliff_E_steep_071",
            new Vector2(52f, 82.5f), null,
            "Nine Mile Creek: the same east wall at its toe");
        static readonly Station N3 = new Station("n3-nmc-east-ramp", "CliffWall_Cliff_E_ramp_031",
            new Vector2(33.5f, 10f), new Vector2(31.66f, 12.2f),
            "Nine Mile Creek: an east ramp (drop 7.74 m), the whole face, her for scale");
        static readonly Station S1 = new Station("s1-stp-south-wall-brow", "CliffWall_Cliff_S_steep_042",
            new Vector2(72.5f, -71f), new Vector2(72.39f, -69.39f),
            "St Peters: the south wall (drop 9.19 m), its upper face, her for scale");
        static readonly Station S2 = new Station("s2-stp-south-wall-toe", "CliffWall_Cliff_S_steep_042",
            new Vector2(72.5f, -75.5f), null,
            "St Peters: the same south wall at its toe");
        static readonly Station S3 = new Station("s3-stp-east-wall", "CliffWall_DeepShoreCliff_E_steep_008",
            new Vector2(187.5f, -17.5f), new Vector2(186.19f, -15.96f),
            "St Peters: a steep east face (drop 10.00 m), its upper face, her for scale");
        static readonly Station S4 = new Station("s4-stp-ledge", "CliffWall_LedgeCliff_S_ramp_055",
            new Vector2(2.5f, -59.5f), new Vector2(2.84f, -57.37f),
            "St Peters: the ledge (drop 4.43 m), the whole face, her for scale");
        static readonly Station S5 = new Station("s5-stp-south-west-wall", "CliffWall_DeepShoreCliff_SW_steep_070",
            new Vector2(-39f, -31f), new Vector2(-38.13f, -29.4f),
            "St Peters: the south-west face (drop 10.00 m), its upper face, her for scale");
        static readonly Station S6 = new Station("s6-stp-south-east-wall", "CliffWall_DeepShoreCliff_SE_steep_017",
            new Vector2(178f, -32.5f), new Vector2(176.96f, -30.82f),
            "St Peters: the south-east face (drop 10.00 m), its upper face, her for scale");

        // =============================================================================================
        //  Fixture state — one instance serves every case, so the teardown clears all of it
        // =============================================================================================

        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        readonly List<Object> _spawned = new List<Object>();
        readonly List<Texture2D> _runtimeCopies = new List<Texture2D>();
        readonly Dictionary<Texture2D, (Texture2D same, Texture2D shifted, string note)> _armCopies =
            new Dictionary<Texture2D, (Texture2D, Texture2D, string)>();
        readonly HashSet<string> _controlsSaved = new HashSet<string>();
        readonly List<string> _lights = new List<string>();
        DateTime _runStartedUtc;
        bool _numbersStarted;
        bool _loadedAny;
        bool _introOnEntry;
        string _healed = "none";

        Camera _cam;
        CameraFollow _follow;
        bool _followCaptured;
        Transform _followTargetOnEntry;
        float _followSmoothOnEntry;
        GameObject _anchor;
        float _askedHeight;
        string _eased = "(not framed)";
        RenderTexture _rt;
        int _w, _h;

        List<CliffWallSurface> _walls = new List<CliffWallSurface>();
        List<Renderer> _cliffRenderers = new List<Renderer>();
        Material _wallMaterial;
        bool _px;
        string _look = "(not read)";
        string _lookEvidence = "(not read)";
        string _pinned = "(not pinned)";
        double _pinnedSeconds;

        PlayerWalkController _player;
        IsoCharacterSprite _iso;
        Rigidbody2D _body;
        bool _bodyTaken, _bodyWasSimulated;

        [OneTimeSetUp]
        public void MarkTheRun() => _runStartedUtc = DateTime.UtcNow;

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

            if (_iso != null) _iso.ReleaseHeading();
            if (_body != null && _bodyTaken) _body.simulated = _bodyWasSimulated;
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
            _armCopies.Clear();

            _follow = null; _cam = null; _followCaptured = false; _followTargetOnEntry = null; _anchor = null;
            _walls = new List<CliffWallSurface>(); _cliffRenderers = new List<Renderer>(); _wallMaterial = null;
            _player = null; _iso = null; _body = null; _bodyTaken = false;
            _look = "(not read)"; _lookEvidence = "(not read)"; _pinned = "(not pinned)";
            _eased = "(not framed)"; _healed = "none";
            _lights.Clear();

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

            // ⚠ #764: a Single load of a region promotes the persistent core's roots into
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

        /// <summary><b>N1 + N2.</b> Nine Mile Creek's tallest wall, brow and toe, through a whole day; the
        /// −1 arm on its east face at 09:00 and 13:00, and the numbers at 13:00.</summary>
        [UnityTest]
        public IEnumerator NineMileCreek_TheTallestEastWall_FromMorningToNight()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);   // reached the way play reaches it (see LoadRegion)
            yield return LoadRegion(NineMileCreek);
            yield return ShootTheSchedule(
                (9f, new[] { At(N1, arm: true), At(N2) }),
                (13f, new[] { At(N1, arm: true, census: true), At(N2) }),
                (19.5f, new[] { At(N1) }),
                (20.5f, new[] { At(N1) }),
                (26f, new[] { At(N1) }));   // 02:00 the next night: the clock only runs forward
        }

        /// <summary><b>N3.</b> A Nine Mile Creek east ramp, whole, at noon and in the golden hour.</summary>
        [UnityTest]
        public IEnumerator NineMileCreek_TheEastRamp_AtNoonAndGoldenHour()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            yield return LoadRegion(NineMileCreek);
            yield return ShootTheSchedule(
                (13f, new[] { At(N3) }),
                (19.5f, new[] { At(N3) }));
        }

        /// <summary><b>S1 + S2.</b> St Peters' south wall, brow and toe, from the morning low water to the
        /// night; the numbers at 13:00.</summary>
        [UnityTest]
        public IEnumerator StPeters_TheSouthWall_FromNoonToNight()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            yield return ShootTheSchedule(
                (9f, new[] { At(S2) }),
                (13f, new[] { At(S1, census: true), At(S2) }),
                (19.5f, new[] { At(S1) }),
                (20.5f, new[] { At(S1) }),
                (26f, new[] { At(S1) }));
        }

        /// <summary><b>S3 + S6.</b> The steep east face with the −1 arm, morning and noon, and the
        /// south-east face in the morning sun.</summary>
        [UnityTest]
        public IEnumerator StPeters_TheSteepEastFaces_ForTheAspectShift()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            yield return ShootTheSchedule(
                (9f, new[] { At(S3, arm: true), At(S6) }),
                (13f, new[] { At(S3, arm: true) }));
        }

        /// <summary><b>S4 + S5.</b> The ledge at noon and the south-west face in the golden hour.</summary>
        [UnityTest]
        public IEnumerator StPeters_TheLedgeAndTheSouthWestWall()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            yield return ShootTheSchedule(
                (13f, new[] { At(S4) }),
                (19.5f, new[] { At(S5) }));
        }

        // =============================================================================================
        //  The schedule: the look once, then every hour in order and every station due at it
        // =============================================================================================

        IEnumerator ShootTheSchedule(params (float hours, Take[] takes)[] schedule)
        {
            ReadTheLook(schedule.SelectMany(s => s.takes).Select(t => t.Station).Distinct().ToList());
            if (!_px) WriteTheRunToken();

            int plates = 0;
            foreach ((float hours, Take[] takes) in schedule)
            {
                yield return PinTheHour(hours);
                foreach (Take take in takes)
                {
                    yield return ShootTheTake(take, hours);
                    plates++;
                }
            }
            AssertEveryHourMovedTheLight();
            Debug.Log($"[{PlateDir}] {plates} captures in the {_look} look; " +
                      (_px ? "composed against the v10 run where its plates were this box's." : "run token written."));
        }

        IEnumerator ShootTheTake(Take take, float hours)
        {
            Station s = take.Station;
            if (s.Figure.HasValue)
            {
                yield return FindHer();
                PutHerAt(s.Figure.Value, withBody: true);
            }
            yield return FrameOn(s.Centre);
            if (s.Figure.HasValue && Vector2.Distance(_player.transform.position, s.Figure.Value) > 0.001f)
                PutHerAt(s.Figure.Value, withBody: false);   // transform only; the world is frozen

            // Evidence first, verdict last: every plate and the caption reach the disk before any check can
            // stop the case, so a failing frame can still be looked at.
            var failures = new List<string>();
            string hhmm = Hhmm(hours);
            string plate = $"{s.Shot}-{hhmm}-{_look}";
            RefreshTheCliffRenderers();
            string inFrame = CheckTheWallIsInFrame(FindWall(s), s, failures);
            string her = s.Figure.HasValue
                ? CheckTheSubjectIsInFrame(_player.transform, $"{s.Shot}: her, for scale", failures)
                : "not in this frame";

            // --- one frame: the picture, its subject-removed control and, in px, the −1 arm (no yield) ---
            byte[] picture = Capture();
            byte[] control = CaptureWithoutTheCliffs();
            SavePlate($"{plate}.png", picture, _w);
            if (_controlsSaved.Add($"{s.Shot}-{_look}")) SavePlate($"{s.Shot}-control-{_look}.png", control, _w);
            byte[] same = null, shifted = null;
            string armNote = null;
            if (take.Arm && _px) CaptureTheAspectShift(failures, out same, out shifted, out armNote);

            ulong hPicture = Fnv1a(picture), hControl = Fnv1a(control);
            double share = ChangedShare(picture, control);
            var extra = new StringBuilder();
            extra.AppendLine($"wall: {inFrame}");
            extra.AppendLine($"her: {her}");
            extra.AppendLine($"picture {hPicture:x16}; control with every cliff renderer off {hControl:x16}; " +
                             $"{share:P1} of the frame changed");
            if (armNote != null) extra.Append(armNote);
            if (shifted != null)
            {
                ulong hSame = Fnv1a(same), hShifted = Fnv1a(shifted);
                double armShare = ChangedShare(picture, shifted);
                SavePlate($"{s.Shot}-{hhmm}-px-aspect-1.png", shifted, _w);
                extra.AppendLine($"−1 ARM: unshifted copy {hSame:x16} (must equal the picture); shifted copy " +
                                 $"{hShifted:x16}, {armShare:P2} of the frame changed against the picture");
                if (hSame != hPicture)
                    failures.Add("the east faces drawn from an UNSHIFTED copy of their own _index bytes are not the " +
                                 "picture, so the copy is not faithful and the −1 plate would show the copy, not the shift");
                if (hShifted == hPicture)
                    failures.Add("the −1 arm drew the identical picture: the shifted index never reached the walls");
            }
            if (_px) extra.Append(ComposeAgainstTheV10Run(s, hhmm, picture, shifted));

            if (take.Census) yield return TakeTheNumbers(s, hhmm, extra, failures);

            if (hPicture == hControl)
                failures.Add("the frame is identical with every cliff renderer off, so no cliff is in the picture");
            else if (share <= CliffShareFloor)
                failures.Add($"switching the cliffs off changed only {share:P1} of the frame (floor " +
                             $"{CliffShareFloor:P0}), so the cliff is not the subject");
            extra.AppendLine(failures.Count == 0 ? "VERDICT: every check held." : $"VERDICT: {failures.Count} FAILED — " +
                                                                                 string.Join("; ", failures));
            SaveCaption($"{plate}.txt", Caption(plate, s, extra.ToString()));
            Debug.Log($"[{PlateDir}] {plate}: look {_look}; {_pinned}; centre ({s.Centre.x:0.#}, {s.Centre.y:0.#}); " +
                      $"{share:P1} changed without the cliffs; {Path.Combine(PlatePath(), plate + ".png")}");

            Assert.That(failures, Is.Empty, $"{plate}: {string.Join("; ", failures)}");
        }

        void RefreshTheCliffRenderers()
        {
            _walls = _walls.Where(w => w != null).ToList();
            _cliffRenderers = _walls.SelectMany(w => w.GetComponentsInChildren<Renderer>(true))
                                    .Where(r => r != null).ToList();
        }

        // =============================================================================================
        //  The look, three ways — and a box with no faces skips
        // =============================================================================================

        void ReadTheLook(List<Station> stations)
        {
            _walls = Object.FindObjectsByType<CliffWallSurface>().Where(w => w != null && w.isActiveAndEnabled).ToList();
            Assert.Greater(_walls.Count, 0, $"{SceneManager.GetActiveScene().name} has no cliff wall at all.");
            _cliffRenderers = _walls.SelectMany(w => w.GetComponentsInChildren<Renderer>(true)).ToList();

            foreach (Station s in stations)
            {
                CliffWallSurface wall = FindWall(s);
                foreach (string band in new[] { "Overburden", "Rock" })
                {
                    Renderer r = BandRenderer(wall, band);
                    if (r == null || ColourOf(r) == null)
                        Assert.Ignore($"SKIPPED, NOT VERIFIED: {s.Wall} has no baked {band} band on this box — the " +
                                      "cliff faces are gitignored and this checkout has not baked them (Hidden " +
                                      "Harbours ▸ Dev ▸ Bake Cliff Kit — v10), so there is no cliff to photograph.");
                }
            }

            List<Renderer> bands = _walls.SelectMany(BandRenderers).ToList();
            _wallMaterial = bands[0].sharedMaterial;
            Assert.IsNotNull(_wallMaterial, $"{bands[0].name} renders no material.");
            bool keyword = _wallMaterial.IsKeywordEnabled(PxKeyword);
            bool palette = _wallMaterial.GetTexture(IdPalette) != null;
            int index = 0, unlit = 0, other = 0, none = 0, strangers = 0;
            string otherName = null;
            foreach (Renderer r in bands)
            {
                if (r.sharedMaterial != _wallMaterial) strangers++;
                Texture colour = ColourOf(r);
                if (colour == null) none++;
                else if (colour.name.EndsWith(IndexSuffix)) index++;
                else if (colour.name.EndsWith(UnlitSuffix)) unlit++;
                else { other++; otherName = otherName ?? colour.name; }
            }

            _px = keyword;
            _look = keyword ? "px" : "v10";
            _lookEvidence = $"material '{_wallMaterial.name}' {PxKeyword} {(keyword ? "ON" : "off")}; _Palette " +
                            $"{(palette ? $"'{_wallMaterial.GetTexture(IdPalette).name}'" : "unbound")}; {bands.Count} " +
                            $"band renderers sample _index {index}, _unlit {unlit}, other {other}, nothing {none}; " +
                            $"{strangers} on another material";
            Debug.Log($"[{PlateDir}] the look: {_look} — {_lookEvidence}");

            Assert.AreEqual(0, strangers, $"some cliff bands render another material: {_lookEvidence}");
            Assert.AreEqual(0, none, $"some cliff bands sample no colour at all: {_lookEvidence}");
            Assert.AreEqual(0, other, $"a cliff band samples '{otherName}', which is neither look's colour slot: {_lookEvidence}");
            Assert.AreEqual(keyword, palette,
                $"the material's keyword and its palette LUT disagree about the look: {_lookEvidence}");
            Assert.AreEqual(0, keyword ? unlit : index,
                $"the material is in the {_look} look but some bands sample the other look's colour slot — the faces " +
                $"on disk were baked in the other look, and every plate would show a mismatch: {_lookEvidence}");
#if UNITY_EDITOR
            var committed = AssetDatabase.LoadAssetAtPath<Material>(CliffMaterialPath);
            Assert.IsNotNull(committed, $"no wall material at {CliffMaterialPath}.");
            Assert.AreEqual(committed.IsKeywordEnabled(PxKeyword), keyword,
                $"the walls render a material whose look is not the committed {CliffMaterialPath}'s: {_lookEvidence}");
#endif
        }

        CliffWallSurface FindWall(Station s)
        {
            CliffWallSurface wall = _walls.FirstOrDefault(w => w != null && w.gameObject.name == s.Wall);
            Assert.IsNotNull(wall, $"{s.Shot}: no cliff wall named {s.Wall} in {SceneManager.GetActiveScene().name}, " +
                                   "so the station's subject is gone (was the region rebuilt?).");
            return wall;
        }

        static IEnumerable<Renderer> BandRenderers(CliffWallSurface wall) =>
            wall.GetComponentsInChildren<Renderer>(true)
                .Where(r => r != null && r.gameObject.name.StartsWith(CliffWallSurface.BandChildPrefix));

        static Renderer BandRenderer(CliffWallSurface wall, string label) =>
            BandRenderers(wall).FirstOrDefault(r => r.gameObject.name == CliffWallSurface.BandChildPrefix + label);

        static Texture ColourOf(Renderer r)
        {
            if (!r.HasPropertyBlock()) return null;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            return mpb.GetTexture(IdUnlit);
        }

        /// <summary>The named wall must be drawn in this frame: at least one of its bands enabled, inside the
        /// frustum and rasterised. (A toe shot of a tall wall leaves its overburden above the frame.)</summary>
        string CheckTheWallIsInFrame(CliffWallSurface wall, Station s, List<string> failures)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_cam);
            var seen = new List<string>();
            int inside = 0;
            foreach (Renderer r in BandRenderers(wall))
            {
                bool drawn = r.enabled && GeometryUtility.TestPlanesAABB(planes, r.bounds) && r.isVisible;
                if (drawn) inside++;
                Texture colour = ColourOf(r);
                seen.Add($"{r.name.Substring(CliffWallSurface.BandChildPrefix.Length)} {(drawn ? "IN" : "out of")} frame, " +
                         $"bounds {r.bounds.min.x:0.#}..{r.bounds.max.x:0.#} x {r.bounds.min.y:0.#}..{r.bounds.max.y:0.#}, " +
                         $"'{(colour != null ? colour.name : "no colour")}'");
            }
            if (inside == 0)
                failures.Add($"none of {s.Wall}'s bands is drawn in the frame at ({s.Centre.x:0.#}, {s.Centre.y:0.#}), " +
                             "so the plate does not show the wall it names");
            return $"{s.Wall}: {string.Join("; ", seen)}";
        }

        // =============================================================================================
        //  The −1 arm (px only)
        // =============================================================================================

        /// <summary>Every east-facing band in the frame drawn from a copy of its own committed _index — once
        /// unshifted (the copy's control) and once shifted — in this frame; then the originals are put back
        /// and read back.</summary>
        void CaptureTheAspectShift(List<string> failures, out byte[] same, out byte[] shifted, out string note)
        {
            same = shifted = null;
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_cam);
            var swapped = new List<(Renderer r, Texture2D original)>();
            foreach (CliffWallSurface w in _walls)
            {
                if (Aspect(w) != "E") continue;
                foreach (Renderer r in BandRenderers(w))
                {
                    if (!r.enabled || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                    var mpb = new MaterialPropertyBlock();
                    r.GetPropertyBlock(mpb);
                    if (mpb.GetFloat(IdPxDecal) > 0.5f) continue;   // a decal strip, never a face
                    if (mpb.GetTexture(IdUnlit) is Texture2D original) swapped.Add((r, original));
                }
            }
            if (swapped.Count == 0)
            {
                note = "−1 ARM: no east-facing band in the frame, so nothing was shifted.\n";
                failures.Add("there is no east-facing band in the frame for the −1 arm to shift");
                return;
            }

            var notes = new StringBuilder();
            List<Texture2D> faces = swapped.Select(p => p.original).Distinct().ToList();
            bool copied = true;
            foreach (Texture2D original in faces)
            {
                if (!_armCopies.TryGetValue(original, out var copies)) _armCopies[original] = copies = CopiesOf(original);
                notes.AppendLine($"  {copies.note}");
                if (copies.shifted != null) continue;
                copied = false;
                failures.Add(copies.note);
            }
            note = $"−1 ARM: {swapped.Count} east bands in frame over {faces.Count} faces:\n{notes}";
            if (!copied) return;

            try
            {
                Swap(swapped, useShifted: false);
                same = Capture();
                Swap(swapped, useShifted: true);
                shifted = Capture();
            }
            finally
            {
                foreach ((Renderer r, Texture2D original) in swapped) SetColour(r, original);
            }
            // Every later plate in the case would be drawn from the copy: stop now, not at the verdict.
            foreach ((Renderer r, Texture2D original) in swapped)
                Assert.AreSame(original, ColourOf(r),
                    $"{r.transform.parent.name}/{r.name}: the original _index was not put back after the −1 arm.");
        }

        void Swap(List<(Renderer r, Texture2D original)> swapped, bool useShifted)
        {
            foreach ((Renderer r, Texture2D original) in swapped)
                SetColour(r, useShifted ? _armCopies[original].shifted : _armCopies[original].same);
        }

        static void SetColour(Renderer r, Texture texture)
        {
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetTexture(IdUnlit, texture);
            r.SetPropertyBlock(mpb);
        }

        /// <summary>The wall's aspect from its name: <c>CliffWall_&lt;class&gt;_&lt;aspect&gt;_&lt;batter&gt;_&lt;n&gt;</c>.</summary>
        static string Aspect(CliffWallSurface w)
        {
            string[] parts = w.gameObject.name.Split('_');
            return parts.Length >= 5 ? parts[parts.Length - 3] : "";
        }

        (Texture2D same, Texture2D shifted, string note) CopiesOf(Texture2D original)
        {
#if UNITY_EDITOR
            string assetPath = AssetDatabase.GetAssetPath(original);
            if (!assetPath.EndsWith(IndexSuffix + ".png"))
                return (null, null, $"the east band samples '{assetPath}', not a px _index face, so there is no index to shift");
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            Color32[] px;
            int width;
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!decoded.LoadImage(File.ReadAllBytes(file), false))
                    return (null, null, $"{file} does not decode as a PNG");
                if (decoded.width != original.width || decoded.height != original.height)
                    return (null, null, $"{file} is {decoded.width}x{decoded.height} but the imported face is " +
                                        $"{original.width}x{original.height}, so a copy cannot stand in for it");
                px = decoded.GetPixels32();   // format-agnostic: whatever LoadImage chose, these are the bytes
                width = decoded.width;
            }
            finally
            {
                Object.DestroyImmediate(decoded);
            }

            var shiftedPx = (Color32[])px.Clone();
            int covered = 0, rock = 0, moved = 0, illegal = 0;
            string firstIllegal = null;
            for (int i = 0; i < px.Length; i++)
            {
                Color32 c = px[i];
                if (c.a == 0) continue;   // outside the face: never drawn
                covered++;
                if (c.r > MaxIndexRow || c.g > MaxIndexBand || (c.b != 0 && c.b != 255))
                {
                    illegal++;
                    firstIllegal = firstIllegal ?? $"texel {i % width},{i / width} = ({c.r}, {c.g}, {c.b}, {c.a})";
                    continue;
                }
                if (c.b != 255) continue;
                rock++;
                if (c.r % TierRows == 0) continue;   // tier 0: the kit's shift clamps there too
                c.r -= 1;
                shiftedPx[i] = c;
                moved++;
            }
            if (illegal > 0)
                return (null, null, $"{assetPath} holds {illegal} texels outside the px index contract (R ≤ " +
                                    $"{MaxIndexRow}, G ≤ {MaxIndexBand}, B 0 or 255), first {firstIllegal}: it is not " +
                                    "an index the −1 arm can shift");
            if (moved == 0)
                return (null, null, $"{assetPath} has no rock texel above tier 0, so −1 would change nothing");

            Texture2D same = CopyWith(original, px, " (px copy)");
            Texture2D shifted = CopyWith(original, shiftedPx, " (px aspect -1)");
            string note = $"{assetPath}: {covered} covered texels, {rock} rock, {moved} lowered one tier; source " +
                          $"filter {original.filterMode}, wrap {original.wrapModeU}/{original.wrapModeV}, mips " +
                          $"{original.mipmapCount}, sRGB {original.isDataSRGB}";
            return (same, shifted, note);
#else
            Assert.Ignore("Needs the AssetDatabase: the −1 arm reads each east face's committed _index bytes.");
            return (null, null, null);
#endif
        }

        /// <summary>A runtime face sampled the way the imported one is (filter, wrap, colour space, a mip chain
        /// if it has one). The unshifted copy's plate must equal the picture, which is what proves it.</summary>
        Texture2D CopyWith(Texture2D original, Color32[] pixels, string suffix)
        {
            bool mips = original.mipmapCount > 1;
            var copy = new Texture2D(original.width, original.height, TextureFormat.RGBA32, mips, !original.isDataSRGB)
            {
                name = original.name + suffix,
                filterMode = original.filterMode,
                wrapModeU = original.wrapModeU,
                wrapModeV = original.wrapModeV,
                anisoLevel = original.anisoLevel,
            };
            _runtimeCopies.Add(copy);
            copy.SetPixels32(pixels);
            copy.Apply(mips, false);
            return copy;
        }

        // =============================================================================================
        //  The numbers: draw calls, the structural census and texture memory (13:00)
        // =============================================================================================

        IEnumerator TakeTheNumbers(Station s, string hhmm, StringBuilder into, List<string> failures)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_cam);
            int total = 0, inFrame = 0, withBlock = 0;
            var materials = new HashSet<Material>();
            var textures = new HashSet<Texture>();
            foreach (Renderer r in _cliffRenderers)
            {
                if (r == null) continue;
                total++;
                if (!r.enabled || !r.gameObject.activeInHierarchy || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                inFrame++;
                materials.Add(r.sharedMaterial);
                if (!r.HasPropertyBlock()) continue;
                withBlock++;
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                foreach (int id in new[] { IdUnlit, IdNormal, IdMask })
                {
                    Texture t = mpb.GetTexture(id);
                    if (t != null) textures.Add(t);
                }
            }
            long textureBytes = textures.Sum(t => Profiler.GetRuntimeMemorySizeLong(t));
            Texture lut = _wallMaterial.GetTexture(IdPalette);
            long lutBytes = lut != null ? Profiler.GetRuntimeMemorySizeLong(lut) : 0;
            string faces = string.Join(", ", textures.Select(t => $"{t.name} {t.width}x{t.height} " +
                                                                  $"{Profiler.GetRuntimeMemorySizeLong(t)} B"));

            long[] on = new long[2], off = new long[2];
            yield return CountDrawCalls(cliffsOn: true, on);
            yield return CountDrawCalls(cliffsOn: false, off);

            string key = $"{s.Shot}-{hhmm}";
            string line = $"{key} cliffRenderers={total} inFrame={inFrame} materials={materials.Count} " +
                          $"propertyBlocks={withBlock} textures={textures.Count} textureBytes={textureBytes} " +
                          $"lutBytes={lutBytes} drawCalls={on[0]} batches={on[1]} drawCallsNoCliffs={off[0]} " +
                          $"batchesNoCliffs={off[1]}";
            AppendNumbers(line);

            into.AppendLine($"NUMBERS ({_look}): {line}");
            into.AppendLine(on[0] > 0
                ? $"  draw calls {on[0]} with the cliffs, {off[0]} without ({on[0] - off[0]} the cliffs'); batches " +
                  $"{on[1]} / {off[1]}"
                : "  draw calls and batches: NOT MEASURED (the recorder read 0 in this editor) — the census below " +
                  "stands in for them");
            into.AppendLine($"  census: {inFrame} of {total} cliff renderers enabled in frame, {materials.Count} " +
                            $"material(s), {withBlock} with a property block");
            into.AppendLine($"  memory: {textures.Count} textures bound, {textureBytes} B (a 384x288 RGBA32 face is " +
                            $"442368 B in either slot); LUT {(lut != null ? $"'{lut.name}' {lutBytes} B (8x32 RGBA32 is 1024 B)" : "none")}");
            into.AppendLine($"  faces: {faces}");
            into.Append(CompareWithTheV10Numbers(key, inFrame, materials.Count, withBlock, textureBytes, lutBytes, on, off,
                                                 failures));
            Debug.Log($"[{PlateDir}] numbers ({_look}): {line}");

            if (materials.Count != 1)
                failures.Add($"the cliff renderers in frame draw {materials.Count} materials; the kit is one material");
        }

        IEnumerator CountDrawCalls(bool cliffsOn, long[] into)
        {
            var off = new List<Renderer>();
            if (!cliffsOn)
                foreach (Renderer r in _cliffRenderers)
                    if (r != null && r.enabled) { r.enabled = false; off.Add(r); }
            var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            try
            {
                for (int i = 0; i < 3; i++) yield return null;   // the world is frozen; the camera still renders
                into[0] = drawCalls.Valid ? drawCalls.LastValue : 0;
                into[1] = batches.Valid ? batches.LastValue : 0;
            }
            finally
            {
                drawCalls.Dispose();
                batches.Dispose();
                foreach (Renderer r in off) if (r != null) r.enabled = true;
            }
        }

        void AppendNumbers(string line)
        {
            string path = Path.Combine(PlatePath(), $"numbers-{_look}.txt");
            if (!_numbersStarted) File.WriteAllText(path, "", new UTF8Encoding(false));   // this run's alone
            _numbersStarted = true;
            File.AppendAllText(path, line + "\n", new UTF8Encoding(false));
        }

        string CompareWithTheV10Numbers(string key, int inFrame, int materials, int withBlock, long textureBytes,
                                        long lutBytes, long[] on, long[] off, List<string> failures)
        {
            if (!_px) return "";
            string path = Path.Combine(PlatePath(), "numbers-v10.txt");
            if (!TheV10RunIsThisBoxs(out DateTime started, out string why) || !File.Exists(path) ||
                File.GetLastWriteTimeUtc(path) < started)
                return $"  against v10: no v10 numbers from this box's v10 run ({why ?? "no numbers file"}).\n";
            string line = File.ReadAllLines(path).FirstOrDefault(l => l.StartsWith(key + " "));
            if (line == null) return $"  against v10: the v10 run recorded no numbers for {key}.\n";
            Dictionary<string, long> v10 = line.Split(' ').Skip(1).Select(kv => kv.Split('='))
                .Where(kv => kv.Length == 2).ToDictionary(kv => kv[0], kv => long.Parse(kv[1], CultureInfo.InvariantCulture));

            bool same = v10["inFrame"] == inFrame && v10["materials"] == materials && v10["propertyBlocks"] == withBlock;
            if (!same)
                failures.Add($"the px census is not v10's: {inFrame} renderers in frame (v10 {v10["inFrame"]}), " +
                             $"{materials} materials (v10 {v10["materials"]}), {withBlock} property blocks (v10 " +
                             $"{v10["propertyBlocks"]}) — the look should change the pictures, never the draw list");
            return $"  against v10: census {(same ? "identical" : "DIFFERENT")} ({inFrame} renderers, {materials} " +
                   $"material, {withBlock} blocks; v10 {v10["inFrame"]}, {v10["materials"]}, {v10["propertyBlocks"]}); " +
                   $"texture bytes {textureBytes - v10["textureBytes"]:+#;-#;0} B, LUT {lutBytes - v10["lutBytes"]:+#;-#;0} B; " +
                   (on[0] > 0 && v10["drawCalls"] > 0
                       ? $"draw calls {on[0] - v10["drawCalls"]:+#;-#;0} ({v10["drawCalls"]} → {on[0]}), the cliffs' own " +
                         $"{(on[0] - off[0]) - (v10["drawCalls"] - v10["drawCallsNoCliffs"]):+#;-#;0}; batches " +
                         $"{on[1] - v10["batches"]:+#;-#;0}.\n"
                       : "draw calls NOT MEASURED in one run or both.\n");
        }

        // =============================================================================================
        //  The two runs: the v10 run's token, and the px run's composites
        // =============================================================================================

        void WriteTheRunToken()
        {
            File.WriteAllText(Path.Combine(PlatePath(), RunTokenFile),
                $"box={Application.dataPath}\nstarted={_runStartedUtc.ToString("o", CultureInfo.InvariantCulture)}\n",
                new UTF8Encoding(false));
        }

        bool TheV10RunIsThisBoxs(out DateTime started, out string why)
        {
            started = DateTime.MaxValue;
            string path = Path.Combine(PlatePath(), RunTokenFile);
            if (!File.Exists(path)) { why = "no v10 run token"; return false; }
            Dictionary<string, string> token = File.ReadAllLines(path).Select(l => l.Split(new[] { '=' }, 2))
                .Where(kv => kv.Length == 2).ToDictionary(kv => kv[0], kv => kv[1]);
            if (!token.TryGetValue("box", out string box) || box != Application.dataPath)
            {
                why = $"the v10 token is another box's ({box ?? "none"})";
                return false;
            }
            if (!token.TryGetValue("started", out string at) ||
                !DateTime.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out started))
            {
                why = "the v10 token has no start";
                return false;
            }
            started = started.ToUniversalTime();
            why = null;
            return true;
        }

        string ComposeAgainstTheV10Run(Station s, string hhmm, byte[] px, byte[] pxAspect)
        {
            string v10Path = Path.Combine(PlatePath(), $"{s.Shot}-{hhmm}-v10.png");
            if (!TheV10RunIsThisBoxs(out DateTime started, out string why))
                return $"NO COMPOSITE: {why}.\n";
            if (!File.Exists(v10Path) || File.GetLastWriteTimeUtc(v10Path) < started)
                return $"NO COMPOSITE: {v10Path} was not written by this box's v10 run (started {started:o}).\n";

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(v10Path), false) || tex.width != _w || tex.height != _h)
                    return $"NO COMPOSITE: {v10Path} is not a {_w}x{_h} plate.\n";
                // Pixels, not the raw data: LoadImage picks the format, and only GetPixels32 is independent of it.
                Color32[] v10Pixels = tex.GetPixels32();
                var v10 = new byte[v10Pixels.Length * 4];
                for (int i = 0; i < v10Pixels.Length; i++)
                {
                    v10[i * 4] = v10Pixels[i].r;
                    v10[i * 4 + 1] = v10Pixels[i].g;
                    v10[i * 4 + 2] = v10Pixels[i].b;
                    v10[i * 4 + 3] = 255;
                }
                byte[][] panels = pxAspect != null ? new[] { v10, px, pxAspect } : new[] { v10, px };
                string name = $"{s.Shot}-{hhmm}-v10-vs-px.png";
                SavePlate(name, Compose(panels), _w * panels.Length);
                return $"COMPOSITE: {name} = v10 | px{(pxAspect != null ? " | px aspect −1" : "")}; v10 from the run " +
                       $"started {started:o}; {ChangedShare(v10, px):P1} of the frame changed v10 → px.\n";
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        byte[] Compose(byte[][] panels)
        {
            int n = panels.Length, row = _w * 4;
            var outBytes = new byte[row * n * _h];
            for (int y = 0; y < _h; y++)
                for (int p = 0; p < n; p++)
                    Buffer.BlockCopy(panels[p], y * row, outBytes, (y * n + p) * row, row);
            return outBytes;
        }

        // =============================================================================================
        //  Her, for scale
        // =============================================================================================

        IEnumerator FindHer()
        {
            for (int f = 0; f < 240 && _player == null; f++)
            {
                foreach (PlayerWalkController p in Object.FindObjectsByType<PlayerWalkController>())
                {
                    var iso = p.GetComponent<IsoCharacterSprite>();
                    if (iso == null || iso.Visual == null || iso.Visual.Skin == null) continue;
                    _player = p;
                    _iso = iso;
                    break;
                }
                if (_player == null) yield return null;
            }
            Assert.IsNotNull(_player, "no on-foot player with a character skin after 240 frames, so there is no " +
                                      "figure to stand beside the wall for scale.");
            Assert.IsNull(_player.GetComponentInParent<IsoFacetHullRenderer>(),
                "the player is ABOARD a hull; these plates stand her ashore beside the wall.");
            _body = _player.GetComponent<Rigidbody2D>();
        }

        void PutHerAt(Vector2 at, bool withBody)
        {
            if (withBody && _body != null)
            {
                if (!_bodyTaken) { _bodyWasSimulated = _body.simulated; _bodyTaken = true; }
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
                _body.position = at;
                _body.simulated = false;
            }
            Vector3 was = _player.transform.position;
            _player.transform.position = new Vector3(at.x, at.y, was.z);
            _iso.HoldHeading(SouthDegrees);
        }

        /// <summary>⭐⭐ THE PLATE MUST CONTAIN ITS SUBJECT: its point inside the frame AND something of it
        /// rasterised (as <c>AshoreFigurePlatePlayTests</c>).</summary>
        string CheckTheSubjectIsInFrame(Transform subject, string what, List<string> failures)
        {
            Vector3 vp = _cam.WorldToViewportPoint(subject.position);
            Renderer[] renderers = subject.GetComponentsInChildren<Renderer>(true);
            int visible = renderers.Count(r => r != null && r.enabled && r.isVisible);
            string diagnosis = $"{what}: at {subject.position}, viewport ({vp.x:0.000}, {vp.y:0.000}, z {vp.z:0.00}); " +
                               $"{visible} of {renderers.Length} renderers visible";
            if (vp.z <= 0f) failures.Add($"the subject is BEHIND the camera. {diagnosis}");
            else if (vp.x < 0.02f || vp.x > 0.98f) failures.Add($"the subject is off the side of the frame. {diagnosis}");
            else if (vp.y < 0.02f || vp.y > 0.98f) failures.Add($"the subject is off the top or bottom of the frame. {diagnosis}");
            else if (visible == 0) failures.Add($"nothing of the subject rasterised into the frame. {diagnosis}");
            return diagnosis;
        }

        // =============================================================================================
        //  The region, the hour and the frame
        // =============================================================================================

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED: no graphics device (Null Device), so nothing rendered " +
                              "and nothing was proved. Expected on CI; a plate of the cliffs needs a GPU.");
        }

        /// <summary>
        /// Load a region the way the existing plates do. ⚠ Nine Mile Creek is reached the way play reaches
        /// it, St Peters first: opened alone, it seeds an editor-only dev core with a DevCamera that is never
        /// promoted, and arriving can take the live core's services down with the dev core — they are put
        /// back and the caption says so.
        /// </summary>
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
        /// Stop the clock at <paramref name="hours"/> after day 1's midnight, then let the light catch up.
        ///
        /// <para>The clock stops only on its own TimeScale. The day/night controller ticks on SCALED time and
        /// reads the clock's hour on every tick, so a stopped clock under a running Time.timeScale gets the
        /// light; the wait is at least one second, because a tint that has not moved for four frames may
        /// simply not have ticked yet. The camera holds its anchor throughout.</para>
        /// </summary>
        IEnumerator PinTheHour(float hours)
        {
            IGameClock clock = GameServices.Clock;
            IEnvironmentService env = GameServices.Environment;
            GameConfig config = GameServices.Config;
            Assert.IsNotNull(clock, "the region registered no clock, so the hour cannot be pinned.");
            Assert.IsNotNull(env, "the region registered no environment, so the tide cannot be read.");
            Assert.IsNotNull(config, "the region registered no GameConfig, so a day has no length.");

            double spd = config.SecondsPerDay;
            double t = (1.0 + hours / 24.0) * spd;
            Time.timeScale = 1f;
            clock.SeekTo(t);
            clock.TimeScale = 0f;
            Assert.LessOrEqual(Math.Abs(clock.TotalSeconds - t), 1.0,
                $"the clock did not land on {t:0.0} s (it reads {clock.TotalSeconds:0.0} s), so the plate would be " +
                "shot at some other hour.");

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
            Assert.Less(frames, 900, $"the day/night tint never settled at {Hhmm(hours)}.");

            Vector4 sun = Shader.GetGlobalVector("_SunDir");
            float elevation = Shader.GetGlobalFloat("_SunElevation");
            float water = env.WaterLevelAt(clock.TotalSeconds);
            float hour = clock.HourOfDay;
            int hh = Mathf.FloorToInt(hour), mm = Mathf.FloorToInt((hour - hh) * 60f + 0.5f) % 60;
            _pinnedSeconds = clock.TotalSeconds;
            _pinned = $"day {clock.DayIndex} {hh:00}:{mm:00} ({hour:0.000} h, TotalSeconds {clock.TotalSeconds:0.0}, " +
                      $"SecondsPerDay {spd:0}); water {water:+0.000;-0.000} m; _DayNightTint ({tint.r:0.000}, " +
                      $"{tint.g:0.000}, {tint.b:0.000}); _SunDir ({sun.x:0.000}, {sun.y:0.000}); _SunElevation " +
                      $"{elevation:0.000}; settled after {frames} frames / {waited:0.00} s";
            _lights.Add($"{Hhmm(hours)}|{tint.r:0.000}|{tint.g:0.000}|{tint.b:0.000}|{sun.x:0.000}|{sun.y:0.000}|{elevation:0.000}");
            Debug.Log($"[{PlateDir}] pinned: {_pinned}");
        }

        /// <summary>Every pinned hour must light the coast differently from every other, or a pin did not
        /// take and two plates labelled with different hours show the same light.</summary>
        void AssertEveryHourMovedTheLight()
        {
            for (int i = 0; i < _lights.Count; i++)
                for (int j = i + 1; j < _lights.Count; j++)
                {
                    string[] a = _lights[i].Split('|'), b = _lights[j].Split('|');
                    bool differ = a.Skip(1).Zip(b.Skip(1), (x, y) => x != y).Any(d => d);
                    Assert.IsTrue(differ, $"{a[0]} and {b[0]} were lit identically (tint, sun direction and " +
                                          $"elevation {string.Join(", ", a.Skip(1))}): one of the pins did not move the light.");
                }
        }

        /// <summary>
        /// Hand the play camera's own follow a still anchor on the station, at the on-foot framing, with the
        /// 1080 px target attached; let the world run until the camera is ON the centre; then stop the world.
        /// ⚠ The hand-over happens with the world STOPPED, so the follow reads the anchor's jump as no motion
        /// (see <c>TerrainPxFlipPlatePlayTests.FrameOn</c>). The render texture stays attached from the first
        /// framing on, so the PixelPerfectCamera sizes every frame from it.
        /// </summary>
        IEnumerator FrameOn(Vector2 at)
        {
            if (_follow == null)
            {
                _follow = FindPersistentFollow();
                Assert.IsNotNull(_follow, "no CameraFollow on the persistent core's camera.");
                Assert.IsTrue(_follow.isActiveAndEnabled, "the play camera's CameraFollow is disabled, so it will not follow the anchor.");
                _cam = _follow.GetComponent<Camera>();
                Assert.IsNotNull(_cam, "the play camera's CameraFollow has no Camera.");
            }
            if (!_followCaptured)
            {
                _followTargetOnEntry = _follow.Target;
                _followSmoothOnEntry = _follow.Smooth;
                _followCaptured = true;
            }

            Time.timeScale = 0f;
            if (_anchor == null)
            {
                _anchor = new GameObject("CliffPxLookPlateAnchor");
                _spawned.Add(_anchor);
            }
            _anchor.transform.position = new Vector3(at.x, at.y, 0f);
            _follow.Target = _anchor.transform;
            _follow.Smooth = 1000f;   // any residue arrives in a frame, not over a second of easing
            _cam.transform.position = new Vector3(at.x, at.y, _cam.transform.position.z);
            _askedHeight = _follow.WorldHeightFor(CameraFraming.OnFoot);
            _follow.SetFraming(_askedHeight, 0f);

            if (_rt == null)
            {
                _w = PlateWidthPx;
                _h = PlateHeightPx;
                _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf);
                _rt.Create();
            }
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
                $"the camera sits at ({p.x:0.####}, {p.y:0.####}), not on the station ({at.x:0.###}, {at.y:0.###}), " +
                $"after {_eased}: something else drives it (a region-bounds clamp, a cinematic), so the plate would " +
                "show another stretch of coast and the two runs would not line up.");
            Assert.That(worldHeight, Is.InRange(8f, 9.5f),
                $"the camera frames {worldHeight:0.###} m tall, not the on-foot view (8.4375 m at 1080 px).");
        }

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
                     "at full time for the look-ahead to ease out";
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

        // =============================================================================================
        //  Shooting
        // =============================================================================================

        byte[] CaptureWithoutTheCliffs()
        {
            var off = new List<Renderer>();
            foreach (Renderer r in _cliffRenderers)
                if (r != null && r.enabled) { r.enabled = false; off.Add(r); }
            try { return Capture(); }
            finally { foreach (Renderer r in off) r.enabled = true; }
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
                if (Math.Abs(a[i] - b[i]) > 2 || Math.Abs(a[i + 1] - b[i + 1]) > 2 || Math.Abs(a[i + 2] - b[i + 2]) > 2)
                    changed++;
            return total > 0 ? (double)changed / total : 0.0;
        }

        static string Hhmm(float hours)
        {
            float h = hours % 24f;
            int hh = Mathf.FloorToInt(h), mm = Mathf.RoundToInt((h - hh) * 60f);
            return $"{hh:00}{mm:00}";
        }

        // =============================================================================================
        //  The caption: the frame each plate was shot through
        // =============================================================================================

        string Caption(string plate, Station s, string extra)
        {
            Vector3 p = _cam.transform.position;
            float worldHeight = _cam.orthographicSize * 2f;
            var sb = new StringBuilder();
            sb.AppendLine(plate);
            sb.AppendLine(s.Subject);
            sb.AppendLine($"look: {_look} — {_lookEvidence}");
            sb.AppendLine($"run: started {_runStartedUtc:yyyy-MM-dd HH:mm:ss}Z, Unity {Application.unityVersion}, " +
                          $"{SystemInfo.graphicsDeviceType} on {SystemInfo.graphicsDeviceName}, box {Application.dataPath}. " +
                          "Hashes are an identity only within this run.");
            sb.AppendLine($"scene: {SceneManager.GetActiveScene().name}; region '{GameServices.CurrentRegionId}'; services healed: {_healed}");
            sb.AppendLine($"camera: '{_cam.name}' in scene '{_cam.gameObject.scene.name}', persistent {MoodGradeDirector.IsPersistentCamera(_cam)}");
            sb.AppendLine($"asked centre ({s.Centre.x:0.###}, {s.Centre.y:0.###}); camera at ({p.x:0.####}, {p.y:0.####}, {p.z:0.###}); {_eased}");
            sb.AppendLine($"ortho {_cam.orthographicSize:0.#####} -> {worldHeight * _cam.aspect:0.###} x {worldHeight:0.####} m, " +
                          $"render texture {_w}x{_h} px, {_follow.WorldUnitsPerRenderedPixel:0.#####} m per rendered pixel");
            sb.AppendLine($"framing: asked {_askedHeight:0.###} m (WorldHeightFor OnFoot); committed {_follow.Framing} (known {_follow.FramingKnown})");
            sb.AppendLine(DescribePixelPerfect());
            sb.AppendLine($"hour, sun and tide: {_pinned}");
            sb.AppendLine(s.Figure.HasValue
                ? $"her: asked ({s.Figure.Value.x:0.##}, {s.Figure.Value.y:0.##}), at {_player.transform.position}, heading {SouthDegrees:0}"
                : "her: not in this frame");
            sb.AppendLine($"OpeningCinematicRunning {GameServices.OpeningCinematicRunning}; Time.timeScale {Time.timeScale}");
            sb.Append(extra);
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
                    catch (Exception) { sb.Append($", {n} (unreadable)"); }
                }
                return sb.ToString();
            }
            return "PixelPerfectCamera: none on the camera";
        }

        static string PlatePath()
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            return dir;
        }

        static void SavePlate(string name, byte[] rgbaBottomLeft, int width)
        {
            var tex = new Texture2D(width, rgbaBottomLeft.Length / (width * 4), TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgbaBottomLeft);
            tex.Apply();
            string path = Path.Combine(PlatePath(), name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[{PlateDir}] plate written: {path}");
        }

        static void SaveCaption(string name, string text) =>
            File.WriteAllText(Path.Combine(PlatePath(), name), text, new UTF8Encoding(false));

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
