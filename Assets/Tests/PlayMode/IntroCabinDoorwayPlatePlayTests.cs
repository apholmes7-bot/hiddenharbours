using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The intro doorway's plates</b> — Phase B of the intro-doorway charter (playtest 2026-09-30). The tests
    /// that say "smooth" are S1–S8; these are the record a person reads by eye, at three headings:
    /// <list type="number">
    /// <item>the first walk out from the opening pose;</item>
    /// <item>in through his door on one held key, from 1.5 m out on deck;</item>
    /// <item>straight back out at once, on the opposite key from the very next frame;</item>
    /// <item>the room with the sole's outline and its four boxes drawn over it (C8: a box larger than its
    /// furniture shows here);</item>
    /// <item>Armand at his wheel below, on deck, and through her crossings (S7);</item>
    /// </list>
    /// and S8's frames, report only: every frame on which her floor, the cut, Armand's sorting or a drawer
    /// changed, against the crossing frame.
    ///
    /// <para><b>One held key, never re-aimed.</b> The keyboard holds one of eight directions, and C9 is that the
    /// tests re-aimed every frame. Each walk holds the one of the eight nearest the doorway's axis on screen at
    /// the moment it starts (the replay's <c>key</c> input), handed to the arrival as a
    /// <see cref="HeldDeckIntents"/> so her own <c>Update</c> walks her, once a frame, as the keys do. Only the
    /// harness's step to 1.5 m out re-aims, and it is not shot.</para>
    ///
    /// <para><b>Every frame, at 60 fps.</b> <c>Time.captureDeltaTime</c> holds the frame at the PC baseline's
    /// 1/60 s whatever the editor manages, so a plate's frame numbers are the owner's frames. Each frame is shot
    /// by <see cref="LateFrameProbe"/>, after every drawer, so it is the frame as it will be drawn.</para>
    ///
    /// <para><b>GPU only.</b> CI has no graphics device and skips these. Each run writes a floor file first;
    /// read only the plates newer than it.</para>
    /// </summary>
    public class IntroCabinDoorwayPlatePlayTests
    {
        private const string PlateDir = "intro-doorway";

        /// <summary>The editor's background compiler, before the first shot.</summary>
        private const float ShaderWaitSeconds = 120f;

        /// <summary>The frame the plates are shot at: the 60 fps the PC baseline targets (CLAUDE.md rule 7).</summary>
        private const float FrameSeconds = 1f / 60f;

        /// <summary>How long she stands below before the first walk, and on deck before she walks back in.</summary>
        private const float StandBelowSeconds = 0.5f;
        private const float StandOnDeckSeconds = 0.5f;

        /// <summary>Standing frames shot before a walk's key goes down, and frames shot after she crosses.</summary>
        private const int LeadInFrames = 6;
        private const int TrailFrames = 12;

        /// <summary>A held key that has not carried her through in this long is shown not doing so.</summary>
        private const float WalkLimitSeconds = 6f;

        /// <summary>Where the walk in starts: 1.5 m out on deck along the doorway's axis (the replay's
        /// <c>deck_1.5</c>, C2's figure), reached by the harness's re-aimed step, which is not shot.</summary>
        private const float DeckStartMetres = 1.5f;
        private const float StartToleranceMetres = 0.05f;
        private const float StartLimitSeconds = 4f;

        /// <summary>The fixture's passage, as <see cref="IntroCabinDoorwayPlayTests"/> sails it: from 60 m out,
        /// the step ashore abeam of the berth and the bollard set back beyond it.</summary>
        private const float ApproachMetres = 60f;
        private const float AshoreMetres = 3f;
        private const float BollardSetBackMetres = 2f;

        /// <summary>The route leg the headless replay found the worst catch on: the nearest of the eight keys
        /// lies 22.3° off the doorway's axis on screen there (IntroCabinDoorwayReplayTests' input table).</summary>
        private const int WorstCatchLeg = 2;

        /// <summary>What each frame shows, in metres at the hull's own pixel scale: the doorway with the room
        /// forward of it and the deck 1.5 m aft of it, at every heading.</summary>
        private static readonly Vector2 WalkFrameMetres = new Vector2(7f, 6f);
        private static readonly Vector2 RoomFrameMetres = new Vector2(4f, 3.5f);
        private static readonly Vector2 ArmandFrameMetres = new Vector2(2.5f, 3f);

        /// <summary>How far up the screen Armand's crop is centred above his station: he stands up from it.</summary>
        private const float ArmandLiftMetres = 0.9f;

        /// <summary>Each plate's nearest-neighbour enlargement of the art's own pixels.</summary>
        private const int WalkScale = 2;
        private const int RoomScale = 3;
        private const int ArmandScale = 4;

        private const int MaxTiles = 16;
        private const int Columns = 4;
        private const int Gap = 2;
        private const int BarPixels = 6;
        private const int TickPixels = 10;
        private const int TickGapPixels = 4;
        private const float CameraZ = -100f;

        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        private const string SkipperPath = "Assets/_Project/Data/Boats/Skippers/StPetersArrivalSkipper.asset";
        private const string FisherVisualPath = "Assets/_Project/Data/Characters/FisherIso.asset";
        private const string EnterHelm = "enter_helm";
        private const string Mesh = "mesh";
        private const string Sprite = "sprite";
        private const string Nobody = "nobody";

        private static readonly Vector2 Berth = Vector2.zero;

        private static readonly Color SeaColour = new Color32(38, 66, 82, 255);
        private static readonly Color32 SheetColour = new Color32(24, 24, 24, 255);
        private static readonly Color32 BelowBar = new Color32(59, 111, 216, 255);
        private static readonly Color32 DeckBar = new Color32(224, 138, 43, 255);
        private static readonly Color32 CrossedBar = new Color32(216, 59, 59, 255);
        private static readonly Color32 StationTick = new Color32(0, 255, 255, 255);
        private static readonly Color32 OutlineColour = new Color32(255, 230, 0, 255);

        /// <summary>The boxes' colours, in the order the sole lists them; the caption names each.</summary>
        private static readonly Color32[] BoxColours =
        {
            new Color32(255, 64, 64, 255), new Color32(255, 160, 0, 255), new Color32(0, 230, 255, 255),
            new Color32(255, 0, 255, 255), new Color32(120, 255, 80, 255), new Color32(255, 255, 255, 255),
        };

        private static readonly string[] BoxColourNames = { "red", "orange", "cyan", "magenta", "green", "white" };

        private sealed class FakeSave : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
            public SaveData Current { get; } = new SaveData();
            public int Saves;
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool v) && v;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() => Saves++;
        }

        private struct Segment
        {
            public Vector2 A, B;
            public Color32 Colour;
        }

        /// <summary>One drawn frame, read after every drawer.</summary>
        private sealed class Shot
        {
            public int Index;
            public float Seconds;
            public bool Below;
            /// <summary>Her floor changed on this frame.</summary>
            public bool Crossed;
            public Vector2 Local;
            /// <summary>The key her own Update walked her with on this frame.</summary>
            public Vector2 Key;
            public string Cut = "-";
            public int ArmandOrder = int.MinValue;
            public string ArmandDrawer = Nobody;
            public string HerDrawer = Nobody;
            public bool Compiling;
            /// <summary>His helm station, where the arrival places him this frame, in this frame's pixels.</summary>
            public Vector2 Station = new Vector2(float.NaN, float.NaN);
            /// <summary>The sole's centre in this frame's pixels (below only).</summary>
            public Vector2 RoomCentre = new Vector2(float.NaN, float.NaN);
            /// <summary>The sole's outline and its boxes' edges in this frame's pixels (below only).</summary>
            public List<Segment> Room;
            /// <summary>The frame, bottom row first; null for a frame no plate is taking.</summary>
            public Color32[] Pixels;
        }

        private sealed class Record
        {
            public string Name;
            public readonly List<Shot> Shots = new List<Shot>();
        }

        private static string s_floor;

        private GameObject _root;
        private GameObject _player;
        private SpriteRenderer _body;
        private SpriteRenderer _riderSprite;
        private ArrivalOpening _opening;
        private BoatOwnerDef _skipper;
        private GameConfig _config;
        private CharacterVisualDef _herArt;
        private HeldDeckIntents _held;
        private BoatInteriorLevel _level;
        private Vector2 _frameCentre;
        private Vector2 _soleCentre;
        private Vector3 _station;
        private string _stationSource;
        private int _ppm;
        private Camera _cam;
        private RenderTexture _rt;
        private Texture2D _read;
        private bool _watching;
        private Vector2 _keyNextFrame;
        private Transform _armand;
        private BoatCutaway _cutaway;
        private string _shaderNote = "";
        private readonly List<Shot> _frames = new List<Shot>();
        private readonly List<Record> _open = new List<Record>();

        [OneTimeSetUp]
        public void WriteTheFloor()
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            s_floor = Path.Combine(dir, "_floor.txt");
            File.WriteAllText(
                s_floor,
                "Floor for " + nameof(IntroCabinDoorwayPlatePlayTests) + " at " +
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                "Only plates in this directory NEWER than this file were shot by this run.\n",
                new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] FLOOR written: {s_floor} — read only plates newer than it " +
                      $"(`find \"{dir}\" -newer \"{s_floor}\" -name '*.png'`).");
        }

        [SetUp]
        public void SetUp()
        {
            // The plates are evidence, read by eye: a committed hull's own complaints must not stop them.
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = FrameSeconds;
            _keyNextFrame = Vector2.zero;           // one fixture runs every heading: no key carries over
            _root = new GameObject("IntroCabinDoorwayPlateFixture");
            _root.AddComponent<AudioListener>();   // one listener, or a full suite logs on every frame
            _player = new GameObject("Player");
            _player.transform.SetParent(_root.transform);
            _player.transform.position = new Vector3(999f, 999f, 0f);
            GameServices.PlayerTransform = _player.transform;
            // The SHIPPED config, as IntroCabinDoorwayPlayTests reads it: whether either figure draws as its
            // mesh is its switches', and the owner played with what they say.
            _config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            GameServices.Config = _config;
            _skipper = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(SkipperPath);
            _herArt = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(FisherVisualPath);
            MooringCleats.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _watching = false;
            _open.Clear();
            _frames.Clear();
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null)
            {
                _rt.Release();
                Object.DestroyImmediate(_rt);
                _rt = null;
            }
            if (_read != null) Object.DestroyImmediate(_read);
            if (_root != null) Object.DestroyImmediate(_root);
            MooringCleats.Clear();
            Interactables.Clear();
            GameServices.Save = null;
            GameServices.PlayerTransform = null;
            GameServices.Config = null;   // the shipped asset itself is never destroyed
            GameServices.Reset();
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>
        /// The three headings the charter names: the route's longest leg (the heading the intro holds longest
        /// under way), the berth's (held from alongside until she steps ashore; sailed here as a leg, so the
        /// hull is drawn as she lies there), and <see cref="WorstCatchLeg"/>.
        /// </summary>
        private static IEnumerable<float> ThePlatedHeadings()
        {
            Vector2[] route = StPetersArrivalOpening.Route();
            int longest = 1;
            for (int i = 2; i < route.Length; i++)
                if ((route[i] - route[i - 1]).sqrMagnitude > (route[longest] - route[longest - 1]).sqrMagnitude)
                    longest = i;
            var seen = new List<float>();
            Once(seen, ArrivalPilot.CompassOf(route[longest] - route[longest - 1]));
            Once(seen, StPetersArrivalOpening.BerthHeadingDegrees());
            Once(seen, ArrivalPilot.CompassOf(route[WorstCatchLeg] - route[WorstCatchLeg - 1]));
            return seen;
        }

        private static void Once(List<float> seen, float heading)
        {
            float h = Mathf.Round(Mathf.Repeat(heading, 360f) * 10f) / 10f;
            if (!seen.Any(s => Mathf.Abs(Mathf.DeltaAngle(s, h)) < 0.05f)) seen.Add(h);
        }

        // =============================================================================================
        //  the plates
        // =============================================================================================

        [UnityTest]
        public IEnumerator Plates_TheDoorwayOnOneHeldKey_TheRoomsBoxes_AndArmandAtHisWheel(
            [ValueSource(nameof(ThePlatedHeadings))] float heading)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device. These pictures need the local GPU.");

            Assert.IsNotNull(_skipper, $"harness: no arrival skipper at {SkipperPath}");
            Assert.IsNotNull(_config, $"harness: no shipped GameConfig at {ConfigPath}");
            Assert.IsNotNull(_herArt, $"harness: no player art at {FisherVisualPath}");
            Assert.IsInstanceOf<CharacterFigurePresentationService>(CharacterFigurePresentation.Service,
                "harness: Art registers the figure service before the first scene loads; without it no mesh " +
                "could be drawn and these would only ever show sprites");
            _station = TheStation(out _stationSource);
            DressHer();

            ArrivalOpening opening = Build(heading);
            Assert.IsTrue(opening.TryBegin(), "harness: a fresh save must be brought in");
            Assert.IsTrue(opening.IsBelowDecks, "harness: the game did not open below decks. " + Where());
            BoatCabinDoor door = opening.CabinDoor;
            Assert.IsNotNull(door, "harness: her cabin has no door");
            Assert.IsTrue(door.IsOpen, "harness: these walk through his OPEN door; nothing here presses one");
            BoatInteriorDef interior = TheVisual().Interior;
            Assert.IsNotNull(interior, "harness: her hull carries no interior def to draw the room's boxes from");
            _level = TheLevelSheOpensOn(interior, opening.CabinLocalPosition);
            Vector2 threshold = BoatCabinThreshold.PointOf(door.Door);
            Vector2 outward = HisDoorsAxis(opening);
            Vector2 deckStart = threshold + outward * DeckStartMetres;
            _soleCentre = CentreOf(_level.Outline);
            _frameCentre = 0.5f * (deckStart + _soleCentre);
            MakeTheCamera(interior.PixelsPerMetre);

            _held = new HeldDeckIntents();
            opening.ConfigureWalkInput(_held);
            LateFrameProbe probe = _root.AddComponent<LateFrameProbe>();
            probe.Tick = () => Shoot(opening);
            yield return ShadersReady(opening);
            _watching = true;

            yield return Stand(StandBelowSeconds);

            // ---- 1: the first walk out, from the opening pose -----------------------------------------
            Vector2 keyOut = OneOfTheEightKeys(ScreenKeyFor(opening, outward));
            Record walkOut = Open("1_walk_out");
            yield return Hold(Vector2.zero, LeadInFrames);
            yield return HoldUntilSheCrosses(opening, keyOut);
            yield return Hold(keyOut, TrailFrames);
            Close(walkOut);

            // ---- the harness's step to 1.5 m out on deck, re-aimed and not shot ----------------------
            yield return WalkTheDeckTo(opening, deckStart);
            yield return Stand(StandOnDeckSeconds);
            // Where the step left her, as the caption says it: the deck's clamp or the room's keep-out may
            // hold her short of the mark.
            string startNote = opening.IsBelowDecks
                ? "from below: the step to the deck mark took her down again"
                : $"from {Vector2.Dot(opening.DeckLocalPosition - threshold, outward):0.00} m out on deck " +
                  $"(asked {DeckStartMetres:0.0} m; {(opening.DeckLocalPosition - deckStart).magnitude:0.00} m " +
                  "off the mark)";

            // ---- 2: in through his door on one held key ------------------------------------------------
            Vector2 keyIn = OneOfTheEightKeys(ScreenKeyFor(opening, -outward));
            Record walkIn = Open("2_in_on_one_held_key");
            yield return Hold(Vector2.zero, LeadInFrames);
            yield return HoldUntilSheCrosses(opening, keyIn);

            // ---- 3: straight back out at once, on the opposite key from the very next frame -----------
            // This resumes on her crossing frame, before it is shot, so both sheets take it; the walk in
            // closes on the frame after, the first under the reversed key.
            Record back = Open("3_straight_back");
            if (opening.IsBelowDecks)
            {
                _held.Walk(-keyIn);
                float deadline = Time.time + WalkLimitSeconds;
                for (int frame = 1; Time.time < deadline; frame++)
                {
                    yield return null;
                    if (frame == 2) Close(walkIn);
                    if (!opening.IsBelowDecks) break;
                }
                yield return Hold(-keyIn, TrailFrames);
            }
            Close(walkIn);
            Close(back);
            _watching = false;
            _held.Release();

            // ---- the plates ----------------------------------------------------------------------------
            string h = heading.ToString("000.0", CultureInfo.InvariantCulture);
            var report = new StringBuilder();
            report.AppendLine($"[{PlateDir}] heading {heading:0.0}°: the station {Fmt(_station)} from " +
                              $"{_stationSource}; the sole '{_level.Id}'. {_shaderNote}");
            report.AppendLine(SaveWalk(h, walkOut, "the first walk out from the opening pose", keyOut,
                                       ScreenKeyFor(opening, outward), heading));
            report.AppendLine(SaveWalk(h, walkIn, "in through his door on one held key, " + startNote, keyIn,
                                       null, heading));
            report.AppendLine(SaveWalk(h, back, "straight back out at once, on the opposite key", -keyIn, null,
                                       heading));
            report.AppendLine(SaveRoom(h, walkOut, interior, heading));
            report.AppendLine(SaveArmand(h, walkOut, walkIn, back, heading));
            string s8 = Transitions(heading);
            report.Append(s8);
            File.WriteAllText(Path.Combine(PlatePath(), $"h{h}_s8.txt"), s8, new UTF8Encoding(false));
            TestContext.WriteLine(report.ToString());
            Debug.Log(report.ToString());

            // The plates are written whatever happened; a walk that never got through is then said aloud.
            Assert.IsTrue(walkOut.Shots.Any(s => s.Crossed),
                $"the first walk out never came up through his door within {WalkLimitSeconds:0} s.\n{report}");
            Assert.IsTrue(walkIn.Shots.Any(s => s.Crossed),
                $"the walk in on one held key never went below within {WalkLimitSeconds:0} s.\n{report}");
            Assert.IsTrue(back.Shots.Skip(1).Any(s => s.Crossed),
                $"straight back at once never came up again within {WalkLimitSeconds:0} s.\n{report}");
        }

        // =============================================================================================
        //  the passage
        // =============================================================================================

        /// <summary>Her own art on the player, as IntroPassengerMeshFigurePlayTests dresses her: the sheets,
        /// the deck rider and its sprite, so the arrival draws her as the game does.</summary>
        private void DressHer()
        {
            _body = _player.AddComponent<SpriteRenderer>();
            var sheets = _player.AddComponent<IsoCharacterSprite>();
            sheets.Configure(_herArt);
            var riderGo = new GameObject("DeckRider");
            riderGo.transform.SetParent(_player.transform, false);
            _riderSprite = riderGo.AddComponent<SpriteRenderer>();
            _riderSprite.enabled = false;
            _player.AddComponent<DeckRiderVisual>().Configure(_riderSprite, _body, sheets);
        }

        private ArrivalOpening Build(float heading)
        {
            GameServices.Save = new FakeSave();
            float r = heading * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Sin(r), Mathf.Cos(r));
            var abeam = new Vector2(-along.y, along.x);
            Vector2 ashore = Berth + abeam * AshoreMetres;

            var bollard = new GameObject("Bollard");
            bollard.transform.SetParent(_root.transform);
            Vector2 post = ashore + abeam * BollardSetBackMetres;
            bollard.transform.position = new Vector3(post.x, post.y, 0f);
            bollard.AddComponent<ShoreCleat>().Configure("fixture.bollard", elevationMeters: 1.5f);

            var go = new GameObject("ArrivalOpening");
            go.transform.SetParent(_root.transform);
            go.SetActive(false);
            var opening = go.AddComponent<ArrivalOpening>();
            opening.Configure(_skipper, new[] { Berth - along * ApproachMetres, Berth }, Berth, heading, ashore,
                              channelBedElevation: -4f);
            opening.ConfigurePilot(ArrivalPilot.Settings.Default);
            go.SetActive(true);
            _opening = opening;
            return opening;
        }

        private static IEnumerator Stand(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
        }

        /// <summary>Hold <paramref name="key"/> (none for zero) for <paramref name="frames"/> frames.</summary>
        private IEnumerator Hold(Vector2 key, int frames)
        {
            if (key == Vector2.zero) _held.Release();
            else                     _held.Walk(key);
            for (int i = 0; i < frames; i++) yield return null;
        }

        /// <summary>Hold one key, unchanged, until her floor changes or <see cref="WalkLimitSeconds"/> pass.</summary>
        private IEnumerator HoldUntilSheCrosses(ArrivalOpening opening, Vector2 key)
        {
            bool startedBelow = opening.IsBelowDecks;
            _held.Walk(key);
            float deadline = Time.time + WalkLimitSeconds;
            while (opening.IsBelowDecks == startedBelow && Time.time < deadline) yield return null;
        }

        /// <summary>The harness's step to <paramref name="target"/> on deck, re-aimed every frame (it places
        /// her; it is not a walk any plate shows), then her keys released.</summary>
        private IEnumerator WalkTheDeckTo(ArrivalOpening opening, Vector2 target)
        {
            float deadline = Time.time + StartLimitSeconds;
            while (!opening.IsBelowDecks && Time.time < deadline)
            {
                Vector2 off = target - opening.DeckLocalPosition;
                if (off.magnitude <= StartToleranceMetres) break;
                _held.Walk(ScreenKeyFor(opening, off.normalized));
                yield return null;
            }
            _held.Release();
        }

        private Record Open(string name)
        {
            var record = new Record { Name = name };
            _open.Add(record);
            return record;
        }

        private void Close(Record record) => _open.Remove(record);

        /// <summary>The one of the keyboard's eight directions nearest <paramref name="screen"/>.</summary>
        private static Vector2 OneOfTheEightKeys(Vector2 screen)
        {
            float degrees = Mathf.Round(Mathf.Atan2(screen.y, screen.x) * Mathf.Rad2Deg / 45f) * 45f;
            return new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
        }

        /// <summary>The way out through his doorway, in the hull's metres — measured off its wall.</summary>
        private static Vector2 HisDoorsAxis(ArrivalOpening opening)
        {
            Vector2 outward = opening.CabinDoor.OutwardAxis;
            Assert.AreNotEqual(Vector2.zero, outward, "his doorway is cut in no wall it could measure");
            return outward;
        }

        /// <summary>
        /// The screen direction that walks her along <paramref name="hullLocal"/> on the floor she is on,
        /// through the projection that floor's walk turns her key back with (IntroCabinDoorwayPlayTests'
        /// <c>ScreenKeyFor</c>). Off his LIVE drawn heading.
        /// </summary>
        private static Vector2 ScreenKeyFor(ArrivalOpening opening, Vector2 hullLocal)
        {
            IBoatHullPresenter hull = BoatHullPresenterHost.Resolve(opening.Boat.gameObject);
            Assert.IsNotNull(hull, "his hull has no presenter to read his drawn heading from");
            BoatVisualDef visual = opening.Boat.Hull != null ? opening.Boat.Hull.Visual : null;
            float heading = hull.DrawnHeadingDegrees();
            Vector2 screen = opening.IsBelowDecks
                ? BoatCabinWalkMath.ToWorldOffset(hullLocal, 0f, heading,
                                                  BoatInteriorInstaller.BakeElevationDegrees(visual),
                                                  BoatInteriorInstaller.ExteriorAzimuthCounterClockwise(visual))
                : DeckAreaMath.DeckToWorld(hullLocal, 0f, heading, hull.BakeElevationDegrees);
            return screen.sqrMagnitude > 1e-12f ? screen.normalized : Vector2.zero;
        }

        /// <summary>The level whose outline holds where she opens; the first, if none does.</summary>
        private static BoatInteriorLevel TheLevelSheOpensOn(BoatInteriorDef interior, Vector2 opensAt)
        {
            Assert.IsTrue(interior.Levels != null && interior.Levels.Length > 0,
                          $"harness: interior def '{interior.name}' has no levels");
            BoatInteriorLevel level = interior.Levels.FirstOrDefault(l => l != null && Holds(l.Outline, opensAt));
            return level ?? interior.Levels[0];
        }

        private static bool Holds(Vector2[] polygon, Vector2 p)
        {
            if (polygon == null || polygon.Length < 3) return false;
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static Vector2 CentreOf(Vector2[] polygon)
        {
            if (polygon == null || polygon.Length == 0) return Vector2.zero;
            Vector2 min = polygon[0], max = polygon[0];
            foreach (Vector2 p in polygon)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return 0.5f * (min + max);
        }

        /// <summary>The station, from the hull's data, by the arrival's rule: her deck def, else enter_helm
        /// (IntroCabinDoorwayPlayTests' <c>TheStation</c>).</summary>
        private Vector3 TheStation(out string source)
        {
            BoatVisualDef visual = TheVisual();
            BoatDeckDef deck = visual.Deck;
            if (deck != null && deck.HasHelmStation)
            {
                source = $"deck def '{deck.name}' ({deck.HelmStationSource})";
                return deck.HelmStationLocalMeters;
            }
            BoatInteriorDef interior = visual.Interior;
            BoatInteriorAnchor anchor = interior != null && interior.Anchors != null
                ? interior.Anchors.FirstOrDefault(a => a != null && a.Action == EnterHelm) : null;
            Assert.IsNotNull(anchor, "harness: her hull states no helm station, neither in her deck def nor as an " +
                                     "enter_helm anchor");
            source = $"interior def '{interior.name}' {EnterHelm} (the fallback)";
            return anchor.ReachPoint;
        }

        private BoatVisualDef TheVisual()
        {
            BoatVisualDef visual = _skipper.Boat != null ? _skipper.Boat.Visual : null;
            Assert.IsNotNull(visual, "harness: the arrival skipper's boat has no visual def");
            return visual;
        }

        private string Where()
        {
            if (_opening == null) return "There is no opening.";
            return $"[{_opening.Current}] the player is " +
                   (_opening.IsBelowDecks ? $"BELOW at sole {_opening.CabinLocalPosition}" : "ON DECK") +
                   $" at {_player.transform.position}.";
        }

        // =============================================================================================
        //  shooting the frame
        // =============================================================================================

        private void MakeTheCamera(int pixelsPerMetre)
        {
            _ppm = Mathf.Max(1, pixelsPerMetre);
            int w = Mathf.RoundToInt(WalkFrameMetres.x * _ppm), h = Mathf.RoundToInt(WalkFrameMetres.y * _ppm);
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            _rt.Create();
            var camGo = new GameObject("IntroDoorwayPlateCamera");
            camGo.transform.SetParent(_root.transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = h / (2f * _ppm);
            _cam.nearClipPlane = 1f;
            _cam.farClipPlane = 400f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = SeaColour;
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            _cam.targetTexture = _rt;
            _read = new Texture2D(w, h, TextureFormat.RGBA32, false);
        }

        /// <summary>The camera on the frame's centre, through the deck's projection on both floors (one
        /// projection, so her crossing never moves the camera), on the art's whole pixels.</summary>
        private void FrameTheCamera(ArrivalOpening opening, IBoatHullPresenter hull, float heading)
        {
            Vector2 centre = (Vector2)opening.Boat.transform.position +
                             DeckAreaMath.DeckToWorld(_frameCentre, _level.SoleZMeters, heading,
                                                      hull.BakeElevationDegrees);
            float m = 1f / _ppm;
            _cam.transform.position = new Vector3(Mathf.Round(centre.x / m) * m, Mathf.Round(centre.y / m) * m, CameraZ);
        }

        /// <summary>
        /// Ask for every variant the shot needs, then let the editor's background compiler finish, with the
        /// passage held still (timeScale 0) so no metre of it is sailed unshot. Not
        /// <c>ShaderUtil.allowAsyncCompilation = false</c>: compiling in place hung a batch PlayMode run on
        /// D3D12 at its first capture (HelmBandPlatePlayTests' note).
        /// </summary>
        private IEnumerator ShadersReady(ArrivalOpening opening)
        {
            _shaderNote = "Shaders: none were compiling at the first shot.";
#if UNITY_EDITOR
            Time.timeScale = 0f;
            IBoatHullPresenter hull = BoatHullPresenterHost.Resolve(opening.Boat.gameObject);
            Assert.IsNotNull(hull, "his hull has no presenter to read his drawn heading from");
            FrameTheCamera(opening, hull, hull.DrawnHeadingDegrees());
            _cam.Render();
            float until = Time.realtimeSinceStartup + ShaderWaitSeconds;
            int frames = 0;
            while (UnityEditor.ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < until)
            {
                yield return null;
                _cam.Render();
                frames++;
            }
            Time.timeScale = 1f;
            if (UnityEditor.ShaderUtil.anythingCompiling)
                _shaderNote = $"Shaders: still compiling after {frames} frame(s) ({ShaderWaitSeconds} s).";
            else if (frames > 0)
                _shaderNote = $"Shaders: waited {frames} frame(s) for them to compile, the passage held still.";
#else
            yield break;
#endif
        }

        /// <summary>One frame, after every drawer: the picture, and what drew it.</summary>
        private void Shoot(ArrivalOpening opening)
        {
            if (!_watching || opening == null || opening.Boat == null) return;
            Transform boat = opening.Boat.transform;
            IBoatHullPresenter hull = BoatHullPresenterHost.Resolve(boat.gameObject);
            if (hull == null) return;
            BoatVisualDef visual = opening.Boat.Hull != null ? opening.Boat.Hull.Visual : null;
            float heading = hull.DrawnHeadingDegrees();
            float cabinElevation = BoatInteriorInstaller.BakeElevationDegrees(visual);
            bool counterClockwise = BoatInteriorInstaller.ExteriorAzimuthCounterClockwise(visual);
            bool below = opening.IsBelowDecks;

            FrameTheCamera(opening, hull, heading);
            _cam.Render();

            var shot = new Shot
            {
                Index = _frames.Count,
                Seconds = _frames.Count * FrameSeconds,
                Below = below,
                Crossed = _frames.Count > 0 && _frames[_frames.Count - 1].Below != below,
                Local = below ? opening.CabinLocalPosition : opening.DeckLocalPosition,
                Key = _keyNextFrame,
                HerDrawer = HerDrawer(),
#if UNITY_EDITOR
                Compiling = UnityEditor.ShaderUtil.anythingCompiling,
#endif
            };
            // The key set this frame (after Update) is the one her Update reads on the next.
            _keyNextFrame = _held != null ? _held.Current.Move : Vector2.zero;

            if (_cutaway == null) _cutaway = boat.GetComponentInChildren<BoatCutaway>();
            if (_cutaway != null) shot.Cut = _cutaway.RequestedCut.ToString();
            if (_armand == null)
                _armand = boat.GetComponentsInChildren<Transform>(true)
                              .FirstOrDefault(t => t.name == MooredBoat.SkipperChildName);
            if (_armand != null)
            {
                var sprite = _armand.GetComponent<SpriteRenderer>();
                var presenter = _armand.GetComponent<CharacterFigurePresenter>();
                shot.ArmandOrder = sprite != null ? sprite.sortingOrder : int.MinValue;
                shot.ArmandDrawer = presenter != null && presenter.DrawsInsteadOfSprite && presenter.Figure != null
                    ? Mesh : Draws(sprite) ? Sprite : Nobody;
                // Where ArrivalOpening places him this frame: the station through HER floor's projection.
                var station = new Vector2(_station.x, _station.y);
                Vector2 offset = below
                    ? BoatCabinWalkMath.ToWorldOffset(station, _station.z, heading, cabinElevation, counterClockwise)
                    : DeckAreaMath.DeckToWorld(station, _station.z, heading, hull.BakeElevationDegrees);
                if (_armand.parent != null)
                    shot.Station = Pixel(_armand.parent.TransformPoint(new Vector3(offset.x, offset.y, 0f)));
            }
            if (below)
            {
                shot.RoomCentre = Pixel(boat.position + (Vector3)BoatCabinWalkMath.ToWorldOffset(
                    _soleCentre, _level.SoleZMeters, heading, cabinElevation, counterClockwise));
                shot.Room = TheRoomsLines(boat.position, heading, cabinElevation, counterClockwise);
            }
            if (_open.Count > 0) shot.Pixels = ReadTheFrame();
            foreach (Record record in _open) record.Shots.Add(shot);
            _frames.Add(shot);
        }

        private Color32[] ReadTheFrame()
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            _read.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0, false);
            _read.Apply(false);
            RenderTexture.active = prev;
            return _read.GetPixels32();
        }

        private Vector2 Pixel(Vector3 world)
        {
            Vector3 s = _cam.WorldToScreenPoint(world);
            return new Vector2(s.x, s.y);
        }

        /// <summary>
        /// The sole's outline at its own height, and each box the sole measures as a wire box: its footprint
        /// at the sole, again at its height above the sole, and its four uprights. Placed by the transform her
        /// cabin walk places her by (the hull's pivot plus the sole point through the room's turntable), so a
        /// box that blocks her where nothing is drawn stands out against the furniture under it.
        /// </summary>
        private List<Segment> TheRoomsLines(Vector3 boatPosition, float heading, float elevation, bool counterClockwise)
        {
            var lines = new List<Segment>();
            Vector2 At(Vector2 local, float z) => Pixel(boatPosition + (Vector3)BoatCabinWalkMath.ToWorldOffset(
                local, z, heading, elevation, counterClockwise));

            float sole = _level.SoleZMeters;
            Ring(lines, _level.Outline, p => At(p, sole), OutlineColour);
            BoatInteriorObstruction[] boxes = _level.Obstructions ?? Array.Empty<BoatInteriorObstruction>();
            for (int i = 0; i < boxes.Length; i++)
            {
                BoatInteriorObstruction box = boxes[i];
                if (box == null || box.Footprint == null || box.Footprint.Length < 2) continue;
                Color32 colour = BoxColours[i % BoxColours.Length];
                float top = sole + Mathf.Max(0f, box.HeightAboveSoleMeters);
                Ring(lines, box.Footprint, p => At(p, sole), colour);
                if (top <= sole) continue;
                Ring(lines, box.Footprint, p => At(p, top), colour);
                foreach (Vector2 corner in box.Footprint)
                    lines.Add(new Segment { A = At(corner, sole), B = At(corner, top), Colour = colour });
            }
            return lines;
        }

        private static void Ring(List<Segment> lines, Vector2[] polygon, Func<Vector2, Vector2> at, Color32 colour)
        {
            if (polygon == null || polygon.Length < 2) return;
            for (int i = 0; i < polygon.Length; i++)
                lines.Add(new Segment { A = at(polygon[i]), B = at(polygon[(i + 1) % polygon.Length]), Colour = colour });
        }

        private string HerDrawer()
        {
            var mesh = _player.GetComponent<DeckRiderMeshPresenter>();
            if (mesh != null && mesh.DrawsInsteadOfSprite) return Mesh;
            if (Draws(_riderSprite)) return "rider sprite";
            return Draws(_body) ? Sprite : Nobody;
        }

        private static bool Draws(SpriteRenderer sprite)
            => sprite != null && sprite.enabled && !sprite.forceRenderingOff && sprite.sprite != null &&
               sprite.gameObject.activeInHierarchy;

        // =============================================================================================
        //  the plates themselves
        // =============================================================================================

        /// <summary>A walk's sheet: up to <see cref="MaxTiles"/> of its frames, always the crossing frame and
        /// the frames either side of it. The bar over each tile: blue below, orange on deck, red the frame her
        /// floor changed.</summary>
        private string SaveWalk(string h, Record walk, string what, Vector2 key, Vector2? axis, float heading)
        {
            List<int> tiles = PickTiles(walk.Shots, MaxTiles);
            int fw = _rt.width, fh = _rt.height;
            int w = fw * WalkScale, th = fh * WalkScale + BarPixels;
            int cols = Mathf.Max(1, Mathf.Min(Columns, tiles.Count));
            int rows = Mathf.Max(1, (tiles.Count + cols - 1) / cols);
            int sw = cols * w + (cols + 1) * Gap, sh = rows * th + (rows + 1) * Gap;
            Color32[] sheet = Filled(sw * sh, SheetColour);
            for (int t = 0; t < tiles.Count; t++)
            {
                Shot s = walk.Shots[tiles[t]];
                int x0 = Gap + t % cols * (w + Gap);
                int y0 = sh - (Gap + t / cols * (th + Gap)) - th;
                Blit(sheet, sw, sh, s.Pixels, fw, fh, 0, 0, fw, fh, x0, y0, WalkScale);
                FillRect(sheet, sw, sh, x0, y0 + th - BarPixels, w, BarPixels,
                         s.Crossed ? CrossedBar : s.Below ? BelowBar : DeckBar);
            }

            var caption = new StringBuilder();
            caption.AppendLine($"[{PlateDir}] heading {heading:0.0}°: {what}.");
            caption.AppendLine($"The key, held unchanged: screen ({key.x:0.000}, {key.y:0.000})" +
                               (axis.HasValue
                                   ? $", the nearest of the eight to the doorway's axis on screen, " +
                                     $"{Vector2.SignedAngle(axis.Value, key):0.0}° off it."
                                   : "."));
            caption.AppendLine(Crossing(walk, key));
            caption.AppendLine("Tiles, left to right and top to bottom (the bar: blue below, orange on deck, red the " +
                               "frame her floor changed):");
            for (int t = 0; t < tiles.Count; t++) caption.AppendLine($"  tile {t + 1}: {Line(walk.Shots[tiles[t]])}");
            caption.AppendLine("Every frame of the walk:");
            foreach (Shot s in walk.Shots) caption.AppendLine("  " + Line(s));
            caption.AppendLine(Compiling(walk.Shots));
            string path = WritePlate($"h{h}_{walk.Name}.png", sheet, sw, sh, caption.ToString());
            return $"  {walk.Name}: {Crossing(walk, key)} Plate {Path.GetFileName(path)}.";
        }

        /// <summary>C8: the room as drawn, beside the same frame with the sole's outline and its boxes over it.
        /// The first frame of the walk out: she stands at the opening pose and the room is cut away.</summary>
        private string SaveRoom(string h, Record walkOut, BoatInteriorDef interior, float heading)
        {
            Shot s = walkOut.Shots.FirstOrDefault(f => f.Below && f.Room != null && f.Pixels != null);
            if (s == null) return "  room: no frame below was shot, so there is no room plate.";
            int fw = _rt.width, fh = _rt.height;
            int cw = Mathf.RoundToInt(RoomFrameMetres.x * _ppm), ch = Mathf.RoundToInt(RoomFrameMetres.y * _ppm);
            int sx = Mathf.RoundToInt(s.RoomCentre.x - cw / 2f), sy = Mathf.RoundToInt(s.RoomCentre.y - ch / 2f);
            int pw = cw * RoomScale, ph = ch * RoomScale;
            int sw = 2 * pw + 3 * Gap, sh = ph + 2 * Gap;
            Color32[] sheet = Filled(sw * sh, SheetColour);
            Blit(sheet, sw, sh, s.Pixels, fw, fh, sx, sy, cw, ch, Gap, Gap, RoomScale);
            int rx = 2 * Gap + pw;
            Blit(sheet, sw, sh, s.Pixels, fw, fh, sx, sy, cw, ch, rx, Gap, RoomScale);
            foreach (Segment seg in s.Room)
                Line(sheet, sw, sh, Upscaled(seg.A, sx, sy, rx, Gap, RoomScale),
                     Upscaled(seg.B, sx, sy, rx, Gap, RoomScale), seg.Colour, rx, Gap, pw, ph);

            var caption = new StringBuilder();
            caption.AppendLine($"[{PlateDir}] heading {heading:0.0}°: C8, the room on frame {s.Index} (she stands at " +
                               "the opening pose). Left as drawn; right with the sole's outline and its boxes over it.");
            caption.AppendLine($"Interior def '{interior.name}', sole '{_level.Id}' at {_level.SoleZMeters:0.###} m. " +
                               "Each box is drawn at the sole and again at its height above it, with its uprights, " +
                               "through the projection her cabin walk places her by.");
            caption.AppendLine($"  yellow: the sole's outline {Bounds(_level.Outline)}");
            BoatInteriorObstruction[] boxes = _level.Obstructions ?? Array.Empty<BoatInteriorObstruction>();
            for (int i = 0; i < boxes.Length; i++)
            {
                BoatInteriorObstruction box = boxes[i];
                if (box == null) continue;
                caption.AppendLine($"  {BoxColourNames[i % BoxColourNames.Length]}: '{box.Id}' " +
                                   $"{Bounds(box.Footprint)}, {box.HeightAboveSoleMeters:0.###} m high, " +
                                   $"treatment '{box.Treatment}'");
            }
            caption.AppendLine(Compiling(new List<Shot> { s }));
            string path = WritePlate($"h{h}_4_room_c8.png", sheet, sw, sh, caption.ToString());
            return $"  room (C8): frame {s.Index}, {boxes.Length} boxes. Plate {Path.GetFileName(path)}.";
        }

        /// <summary>S7 by eye: Armand at his wheel below, on deck, and on the frames either side of each of her
        /// crossings. The cyan ticks either side mark his helm station where the arrival places him that frame.</summary>
        private string SaveArmand(string h, Record walkOut, Record walkIn, Record back, float heading)
        {
            var picks = new List<(string what, Shot shot)>();
            Shot first = walkOut.Shots.FirstOrDefault();
            if (first != null) picks.Add(("below, before the walk out", first));
            AddCrossing(picks, walkOut, "her crossing out");
            Shot onDeck = walkIn.Shots.FirstOrDefault();
            if (onDeck != null) picks.Add(("on deck, before the walk in", onDeck));
            AddCrossing(picks, walkIn, "her crossing in");
            Shot backOut = back.Shots.Skip(1).FirstOrDefault(s => s.Crossed);
            if (backOut != null) picks.Add(("her crossing straight back out", backOut));
            picks = picks.Where(p => p.shot.Pixels != null && !float.IsNaN(p.shot.Station.x)).ToList();
            if (picks.Count == 0) return "  Armand: he was placed on no shot frame, so there is no plate.";

            int fw = _rt.width, fh = _rt.height;
            int cw = Mathf.RoundToInt(ArmandFrameMetres.x * _ppm), ch = Mathf.RoundToInt(ArmandFrameMetres.y * _ppm);
            int pw = cw * ArmandScale, ph = ch * ArmandScale + BarPixels;
            int sw = picks.Count * pw + (picks.Count + 1) * Gap, sh = ph + 2 * Gap;
            Color32[] sheet = Filled(sw * sh, SheetColour);
            var caption = new StringBuilder();
            caption.AppendLine($"[{PlateDir}] heading {heading:0.0}°: Armand at his wheel (S7 by eye). The cyan ticks " +
                               $"mark his helm station {Fmt(_station)} from {_stationSource}, projected through the " +
                               "floor her picture is on that frame, where the arrival places him.");
            for (int i = 0; i < picks.Count; i++)
            {
                Shot s = picks[i].shot;
                int sx = Mathf.RoundToInt(s.Station.x - cw / 2f);
                int sy = Mathf.RoundToInt(s.Station.y + ArmandLiftMetres * _ppm - ch / 2f);
                int x0 = Gap + i * (pw + Gap);
                Blit(sheet, sw, sh, s.Pixels, fw, fh, sx, sy, cw, ch, x0, Gap, ArmandScale);
                FillRect(sheet, sw, sh, x0, Gap + ph - BarPixels, pw, BarPixels,
                         s.Crossed ? CrossedBar : s.Below ? BelowBar : DeckBar);
                Vector2Int at = Upscaled(s.Station, sx, sy, x0, Gap, ArmandScale);
                for (int d = 0; d < 2; d++)
                {
                    Line(sheet, sw, sh, new Vector2Int(at.x - TickGapPixels - TickPixels, at.y + d),
                         new Vector2Int(at.x - TickGapPixels, at.y + d), StationTick, x0, Gap, pw, ph - BarPixels);
                    Line(sheet, sw, sh, new Vector2Int(at.x + TickGapPixels, at.y + d),
                         new Vector2Int(at.x + TickGapPixels + TickPixels, at.y + d), StationTick, x0, Gap, pw,
                         ph - BarPixels);
                }
                caption.AppendLine($"  tile {i + 1}: {picks[i].what}; {Line(s)}");
            }
            caption.AppendLine(Compiling(picks.Select(p => p.shot).ToList()));
            string path = WritePlate($"h{h}_5_armand.png", sheet, sw, sh, caption.ToString());
            return $"  Armand: {picks.Count} tiles. Plate {Path.GetFileName(path)}.";
        }

        private static void AddCrossing(List<(string what, Shot shot)> picks, Record walk, string what)
        {
            int at = walk.Shots.FindIndex(s => s.Crossed);
            if (at < 0) return;
            if (at > 0) picks.Add(($"{what}, the frame before", walk.Shots[at - 1]));
            picks.Add(($"{what}, the crossing frame", walk.Shots[at]));
            if (at + 1 < walk.Shots.Count) picks.Add(($"{what}, the frame after", walk.Shots[at + 1]));
        }

        /// <summary>S8, report only: every frame of the passage on which her floor, the cut, Armand's sorting,
        /// his drawer or hers changed, each against the nearest crossing frame.</summary>
        private string Transitions(float heading)
        {
            var sb = new StringBuilder($"[S8] heading {heading:0.0}°: the frames that changed (report only; " +
                                       $"{_frames.Count} frames at {1f / FrameSeconds:0} fps):\n");
            List<int> crossings = _frames.Where(f => f.Crossed).Select(f => f.Index).ToList();
            for (int i = 1; i < _frames.Count; i++)
            {
                Shot a = _frames[i - 1], b = _frames[i];
                var what = new List<string>();
                if (a.Below != b.Below) what.Add(b.Below ? "SHE CROSSED IN" : "SHE CROSSED OUT");
                if (a.Cut != b.Cut) what.Add($"the cut {a.Cut} → {b.Cut}");
                if (a.ArmandOrder != b.ArmandOrder) what.Add($"his sorting {a.ArmandOrder} → {b.ArmandOrder}");
                if (a.ArmandDrawer != b.ArmandDrawer) what.Add($"his drawer {a.ArmandDrawer} → {b.ArmandDrawer}");
                if (a.HerDrawer != b.HerDrawer) what.Add($"her drawer {a.HerDrawer} → {b.HerDrawer}");
                if (what.Count == 0) continue;
                string rel = "";
                if (crossings.Count > 0)
                {
                    int nearest = crossings.OrderBy(c => Mathf.Abs(b.Index - c)).First();
                    int d = b.Index - nearest;
                    rel = $" (crossing {(d >= 0 ? "+" : "")}{d})";
                }
                sb.AppendLine($"  frame {b.Index}{rel}: {string.Join("; ", what)}");
            }
            return sb.ToString();
        }

        private static List<int> PickTiles(List<Shot> shots, int max)
        {
            if (shots.Count <= max) return Enumerable.Range(0, shots.Count).ToList();
            var keep = new SortedSet<int> { 0, shots.Count - 1 };
            for (int i = 0; i < shots.Count; i++)
                if (shots[i].Crossed)
                    for (int d = -1; d <= 1; d++) keep.Add(Mathf.Clamp(i + d, 0, shots.Count - 1));
            int spare = max - keep.Count;
            for (int k = 1; k <= spare; k++) keep.Add(Mathf.RoundToInt(k * (shots.Count - 1) / (float)(spare + 1)));
            return keep.Take(max).ToList();
        }

        /// <summary>Her crossing under the walk's own <paramref name="key"/>: the walk back's sheet opens on the
        /// crossing in, which was the key before it.</summary>
        private static string Crossing(Record walk, Vector2 key)
        {
            int keyDown = walk.Shots.FindIndex(s => s.Key == key);
            int crossed = walk.Shots.FindIndex(s => s.Crossed && s.Key == key);
            if (crossed < 0) return $"She did not cross within {WalkLimitSeconds:0} s of the key going down.";
            int frames = keyDown >= 0 ? crossed - keyDown + 1 : crossed + 1;
            return $"She crossed on frame {walk.Shots[crossed].Index}, the {Ordinal(frames)} frame the key was held " +
                   $"({frames * FrameSeconds:0.000} s).";
        }

        private static string Ordinal(int n)
        {
            int tens = n % 100;
            string suffix = tens >= 11 && tens <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return n.ToString(CultureInfo.InvariantCulture) + suffix;
        }

        private static string Line(Shot s) =>
            $"frame {s.Index} ({s.Seconds:0.000} s), {(s.Below ? "below" : "on deck")}{(s.Crossed ? ", CROSSED" : "")}, " +
            $"at ({s.Local.x:0.000}, {s.Local.y:0.000}), key ({s.Key.x:0.00}, {s.Key.y:0.00}), the cut {s.Cut}, " +
            $"his sorting {(s.ArmandOrder == int.MinValue ? "-" : s.ArmandOrder.ToString(CultureInfo.InvariantCulture))}, " +
            $"his drawer {s.ArmandDrawer}, her drawer {s.HerDrawer}{(s.Compiling ? ", SHADERS COMPILING" : "")}";

        private static string Compiling(List<Shot> shots)
        {
            int n = shots.Count(s => s.Compiling);
            return n == 0
                ? "Shaders: none were compiling on these frames."
                : $"Shaders: {n} of these frames were shot while the editor was still compiling; they may hold stand-ins.";
        }

        private static string Bounds(Vector2[] polygon)
        {
            if (polygon == null || polygon.Length == 0) return "(no points)";
            float x0 = polygon.Min(p => p.x), x1 = polygon.Max(p => p.x);
            float y0 = polygon.Min(p => p.y), y1 = polygon.Max(p => p.y);
            return $"x {x0:0.###} to {x1:0.###}, y {y0:0.###} to {y1:0.###}";
        }

        private static string Fmt(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";

        private static string PlatePath() => Path.Combine(Application.temporaryCachePath, PlateDir);

        private static string WritePlate(string name, Color32[] pixels, int w, int h, string caption)
        {
            string dir = PlatePath();
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            var png = new Texture2D(w, h, TextureFormat.RGBA32, false);
            png.SetPixels32(pixels);
            png.Apply(false);
            File.WriteAllBytes(path, png.EncodeToPNG());
            Object.DestroyImmediate(png);
            File.WriteAllText(Path.ChangeExtension(path, ".txt"), caption, new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] plate written: {path}");
            return path;
        }

        // ---- pixels, bottom row first ---------------------------------------------------------------------

        private static Color32[] Filled(int n, Color32 colour)
        {
            var pixels = new Color32[n];
            for (int i = 0; i < n; i++) pixels[i] = colour;
            return pixels;
        }

        private static void FillRect(Color32[] dst, int dw, int dh, int x0, int y0, int w, int h, Color32 colour)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(dh, y0 + h); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(dw, x0 + w); x++)
                    dst[y * dw + x] = colour;
        }

        /// <summary>Copy the <paramref name="cw"/> × <paramref name="ch"/> crop at (<paramref name="sx"/>,
        /// <paramref name="sy"/>) of the source to (<paramref name="dx"/>, <paramref name="dy"/>), each pixel a
        /// <paramref name="scale"/>-square block; what falls outside the source is left as the sheet.</summary>
        private static void Blit(Color32[] dst, int dw, int dh, Color32[] src, int fw, int fh,
                                 int sx, int sy, int cw, int ch, int dx, int dy, int scale)
        {
            if (src == null) return;
            for (int y = 0; y < ch; y++)
            {
                int py = sy + y;
                if (py < 0 || py >= fh) continue;
                for (int x = 0; x < cw; x++)
                {
                    int px = sx + x;
                    if (px < 0 || px >= fw) continue;
                    Color32 c = src[py * fw + px];
                    c.a = 255;
                    FillRect(dst, dw, dh, dx + x * scale, dy + y * scale, scale, scale, c);
                }
            }
        }

        private static Vector2Int Upscaled(Vector2 framePixel, int sx, int sy, int dx, int dy, int scale) =>
            new Vector2Int(dx + Mathf.FloorToInt((framePixel.x - sx) * scale),
                           dy + Mathf.FloorToInt((framePixel.y - sy) * scale));

        /// <summary>A one-pixel line, clipped to the panel at (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
        private static void Line(Color32[] dst, int dw, int dh, Vector2Int a, Vector2Int b, Color32 colour,
                                 int cx, int cy, int cw, int ch)
        {
            int x = a.x, y = a.y, dx = Mathf.Abs(b.x - a.x), dy = -Mathf.Abs(b.y - a.y);
            int stepX = a.x < b.x ? 1 : -1, stepY = a.y < b.y ? 1 : -1, err = dx + dy;
            for (int guard = 0; guard < 20000; guard++)
            {
                if (x >= cx && x < cx + cw && y >= cy && y < cy + ch && x >= 0 && x < dw && y >= 0 && y < dh)
                    dst[y * dw + x] = colour;
                if (x == b.x && y == b.y) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x += stepX; }
                if (e2 <= dx) { err += dx; y += stepY; }
            }
        }
    }
}
