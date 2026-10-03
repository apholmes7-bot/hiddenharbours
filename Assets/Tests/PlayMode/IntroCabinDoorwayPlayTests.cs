using System;
using System.Collections;
using System.Collections.Generic;
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
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>Armand at his wheel</b> — S7 and S8 of the intro-doorway charter (playtest 2026-09-30: "the captain
    /// isnt stationed at the helm hes off centred into the wall").
    ///
    /// <para><b>What S7 reads is the PICTURE.</b> Never the helm slot, never <c>StandTheSkipperAt</c>'s argument,
    /// never <c>OccupantStandRigMeters</c>: a guard that reads what the code was told stays green while he
    /// stands in a wall (<c>AssertArmandKeepsTheHelm</c> did). Whichever draws him on a frame is what is read:
    /// the mesh (its root and the ankles of its drawn pose) when <c>CharacterFigurePresenter</c> draws instead
    /// of his sprite, otherwise the sprite's pivot. Both are read in the frame his hull is DRAWN in, her facet
    /// hull's posed mesh (the rig's metres: +x starboard, +y bow, +z up) — the geometry he is seen standing
    /// in, below decks and on deck alike, because the cape's house interior is geometry.</para>
    ///
    /// <para><b>The station</b> is the hull's own data, resolved by the rule the arrival states: her deck def's
    /// <c>HelmStationLocalMeters</c> when it has one, else the interior's <c>enter_helm</c> reach point. Each
    /// frame also records which of the two the LIVE boat root would give (the deck def arrives with her skin).</para>
    ///
    /// <para><b>Not headless.</b> These need the engine: a hull skinned as a facet mesh, the figure service
    /// registered before the scene loads, a posed mesh and the frame loop's LateUpdate order. They run in
    /// PlayMode only.</para>
    /// </summary>
    public class IntroCabinDoorwayPlayTests
    {
        /// <summary>§5 S7: his drawn feet within this of the helm station, rig metres.</summary>
        private const float StationToleranceMetres = 0.05f;

        /// <summary>
        /// His ankles: the rig's foot bones, whose joint is where the shin meets the foot (rig 9 names no
        /// <c>ankle</c> bone; <c>foot_L</c>/<c>foot_R</c> sit 0.075 m above his root at rest). Owner ruling (a),
        /// 2026-10-01: his feet are measured here, not at the middle of his soles, whose toe box reaches forward
        /// of where he stands.
        /// </summary>
        private static readonly string[] AnkleBoneIds = { "foot_L", "foot_R" };

        /// <summary>How long she stands below before she walks, and after she walks back in.</summary>
        private const float StandBelowSeconds = 0.5f;

        /// <summary>How long she stands on deck between her two crossings.</summary>
        private const float StandOnDeckSeconds = 1f;

        /// <summary>The bound on any one leg of her walk; past it the harness says where she was.</summary>
        private const float WalkLimitSeconds = 20f;

        /// <summary>How far out the fixture's passage starts: long enough that she is under way throughout.</summary>
        private const float ApproachMetres = 60f;

        /// <summary>The step ashore, abeam of the berth — ArrivalOpeningPlayTests' tie-up, turned to the heading.</summary>
        private const float AshoreMetres = 3f;

        /// <summary>The bollard, set back beyond the step so the honest scope is not clamped to slack.</summary>
        private const float BollardSetBackMetres = 2f;

        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        private const string SkipperPath = "Assets/_Project/Data/Boats/Skippers/StPetersArrivalSkipper.asset";
        private const string EnterHelm = "enter_helm";
        private const string Mesh = "mesh";
        private const string Sprite = "sprite";
        private const string Nobody = "nobody";

        /// <summary>The four cardinals, beside the intro's own headings.</summary>
        private static readonly float[] Cardinals = { 0f, 90f, 180f, 270f };

        private static readonly Vector2 Berth = Vector2.zero;

        private sealed class FakeSave : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
            public SaveData Current { get; } = new SaveData();
            public int Saves;
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool v) && v;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() => Saves++;
        }

        /// <summary>One drawn frame, read after every drawer.</summary>
        private struct Frame
        {
            public int Index;
            public bool Below;
            public string Drawer;
            public string Branch;
            public Vector3 Root;
            /// <summary>His feet: the middle of his two ankles (mesh), or the sprite's pivot on his deck.</summary>
            public Vector3 Feet;
            public float Error;
            public int Order;
            public string Cut;
            public string Why;
        }

        private GameObject _root;
        private GameObject _player;
        private ArrivalOpening _opening;
        private BoatOwnerDef _skipper;
        private GameConfig _config;
        private Vector3 _station;
        private string _stationSource;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("IntroCabinDoorwayFixture");
            _root.AddComponent<AudioListener>();   // one listener, or a full suite logs on every frame
            _player = new GameObject("Player");
            _player.transform.SetParent(_root.transform);
            _player.transform.position = new Vector3(999f, 999f, 0f);
            GameServices.PlayerTransform = _player.transform;
            // The SHIPPED config, not a blank one: whether he draws as his mesh is its MeshCast switch, and the
            // owner played with what it says.
            _config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            GameServices.Config = _config;
            _skipper = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(SkipperPath);
            MooringCleats.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            MooringCleats.Clear();
            Interactables.Clear();
            GameServices.Save = null;
            GameServices.PlayerTransform = null;
            GameServices.Config = null;   // the shipped asset itself is never destroyed
            GameServices.Reset();
        }

        /// <summary>The intro's own headings (its legs and its berth), then the cardinals, each once.</summary>
        private static IEnumerable<float> TheIntrosHeadings()
        {
            var seen = new List<float>();
            Vector2[] route = StPetersArrivalOpening.Route();
            for (int i = 1; i < route.Length; i++) Once(seen, ArrivalPilot.CompassOf(route[i] - route[i - 1]));
            Once(seen, StPetersArrivalOpening.BerthHeadingDegrees());
            foreach (float h in Cardinals) Once(seen, h);
            return seen;
        }

        private static void Once(List<float> seen, float heading)
        {
            float h = Mathf.Round(Mathf.Repeat(heading, 360f) * 10f) / 10f;
            if (!seen.Any(s => Mathf.Abs(Mathf.DeltaAngle(s, h)) < 0.05f)) seen.Add(h);
        }

        // =============================================================================================
        //  S7 — his drawn feet on his helm station, every frame, below and on deck, through her crossings
        // =============================================================================================

        [UnityTest]
        public IEnumerator S7_ArmandsDrawnFeetStayOnHisHelmStation_BelowAndOnDeck_ThroughHerCrossings(
            [ValueSource(nameof(TheIntrosHeadings))] float heading)
        {
            var frames = new List<Frame>();
            yield return RunThePassage(heading, frames);

            List<Frame> drawn = frames.Where(f => f.Drawer == Mesh || f.Drawer == Sprite).ToList();
            Assert.Greater(drawn.Count, 0,
                "harness: Armand was drawn on no frame of the passage, by his mesh or his sprite.\n" +
                Describe(frames, heading));
            Assert.IsFalse(drawn.Any(f => f.Drawer == Mesh && float.IsNaN(f.Feet.x)),
                "harness: his mesh drew but its drawn pose's ankles could not be read, so his feet cannot be.\n" +
                Describe(frames, heading));
            string report = Describe(frames, heading);
            Debug.Log(report);
            TestContext.WriteLine(AnkleLine(drawn, heading));
            List<Frame> off = drawn.Where(f => !(f.Error <= StationToleranceMetres)).ToList();
            Assert.AreEqual(0, off.Count,
                $"Armand's drawn feet were more than {StationToleranceMetres:0.00} m from his helm station on " +
                $"{off.Count} of the {drawn.Count} frames he was drawn (below: {off.Count(f => f.Below)}, on deck: " +
                $"{off.Count(f => !f.Below)}). The station is where he steers her; a figure drawn anywhere else " +
                "is the owner's \"off centred into the wall\".\n" + report);
        }

        // =============================================================================================
        //  S8 — report only: the frames the cut and his sorting change, against her crossing frames (D3)
        // =============================================================================================

        [UnityTest]
        public IEnumerator S8_ReportOnly_TheFramesTheCutawayAndHisSortingChange_AgainstHerCrossings()
        {
            var frames = new List<Frame>();
            yield return RunThePassage(TheIntrosHeadings().First(), frames);
            Debug.Log(Transitions(frames));
        }

        // =============================================================================================
        //  the passage: stand below, out through his door, stand on deck, back in, stand below
        // =============================================================================================

        private IEnumerator RunThePassage(float heading, List<Frame> frames)
        {
            Assert.IsNotNull(_skipper, $"harness: no arrival skipper at {SkipperPath}");
            Assert.IsNotNull(_config, $"harness: no shipped GameConfig at {ConfigPath}");
            Assert.IsInstanceOf<CharacterFigurePresentationService>(CharacterFigurePresentation.Service,
                "harness: Art registers the figure service before the first scene loads; without it no mesh " +
                "could be drawn and this would only ever read his sprite");
            _station = TheStation(out _stationSource);
            ArrivalOpening opening = Build(heading);
            Assert.IsTrue(opening.TryBegin(), "harness: a fresh save must be brought in");
            Assert.IsTrue(opening.IsBelowDecks, "harness: the game did not open below decks. " + Where());

            LateFrameProbe probe = _root.AddComponent<LateFrameProbe>();
            probe.Tick = () => frames.Add(ReadTheFrame(opening, frames.Count));

            yield return Stand(StandBelowSeconds);
            yield return ThroughHisDoor(opening);
            Assert.IsFalse(opening.IsBelowDecks, "harness: she never came up through his open door. " + Where());
            yield return Stand(StandOnDeckSeconds);
            yield return ThroughHisDoor(opening);
            Assert.IsTrue(opening.IsBelowDecks, "harness: she never walked back in off the deck. " + Where());
            yield return Stand(StandBelowSeconds);

            probe.Tick = null;
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

        /// <summary>Her key held through his open door until she is on its other side
        /// (<see cref="KeyThroughHisDoor"/>), on whichever floor she starts.</summary>
        private IEnumerator ThroughHisDoor(ArrivalOpening opening)
        {
            BoatCabinDoor door = opening.CabinDoor;
            Assert.IsNotNull(door, "harness: her cabin has no door");
            Assert.IsTrue(door.IsOpen, "harness: this walks through his OPEN door; nothing here presses one");
            bool startedBelow = opening.IsBelowDecks;
            float deadline = Time.time + WalkLimitSeconds;
            while (opening.IsBelowDecks == startedBelow && Time.time < deadline)
            {
                Vector2 key = KeyThroughHisDoor(opening);
                if (opening.IsBelowDecks) opening.WalkTheCabin(key, Time.deltaTime);
                else                      opening.WalkTheDeck(key, Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>
        /// The key a player holds to walk through his open door: toward its LIVE position while she is
        /// farther from it than its pull reaches (it rides his hull), and straight through it along its axis
        /// within that reach — out of his cabin below, into it on deck — the key the doorway carries (owner
        /// ruling D1, 2026-09-30: in the band with the key pointing through, she goes; a key held within its
        /// cone and reach is carried to the opening).
        /// </summary>
        private Vector2 KeyThroughHisDoor(ArrivalOpening opening)
        {
            BoatCabinDoor door = opening.CabinDoor;
            Vector2 here = opening.IsBelowDecks ? opening.CabinLocalPosition : opening.DeckLocalPosition;
            if (Vector2.Distance(here, BoatCabinThreshold.PointOf(door.Door))
                > BoatCabinThreshold.PullReachMetres(door.Door))
            {
                Vector2 toDoor = (Vector2)door.transform.position - (Vector2)_player.transform.position;
                return toDoor.sqrMagnitude > 1e-6f ? toDoor.normalized : Vector2.up;
            }
            return ScreenKeyFor(opening, opening.IsBelowDecks ? HisDoorsAxis(opening) : -HisDoorsAxis(opening));
        }

        /// <summary>The way out through his doorway, in the hull's metres — measured off its wall.</summary>
        private static Vector2 HisDoorsAxis(ArrivalOpening opening)
        {
            Vector2 outward = opening.CabinDoor.OutwardAxis;
            Assert.AreNotEqual(Vector2.zero, outward, "his doorway is cut in no wall it could measure");
            return outward;
        }

        /// <summary>
        /// The screen key that walks her along <paramref name="hullLocal"/> on the floor she is on, through
        /// the projection that floor's walk turns her key back with — the sole's turntable below
        /// (<c>BoatCabinWalkMath.HeldOnTheSole</c>), the deck's above (<c>DeckWalkController.HeldInHullFrame</c>)
        /// — so the direction she is handed is <paramref name="hullLocal"/> exactly. Off his LIVE drawn heading.
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

        // =============================================================================================
        //  reading the frame
        // =============================================================================================

        private Frame ReadTheFrame(ArrivalOpening opening, int index)
        {
            var f = new Frame
            {
                Index = index, Below = opening.IsBelowDecks, Drawer = Nobody, Branch = "-",
                Root = new Vector3(float.NaN, float.NaN, float.NaN), Feet = new Vector3(float.NaN, float.NaN, float.NaN),
                Error = float.NaN, Order = int.MinValue, Cut = "-", Why = "-",
            };
            if (opening.Boat == null) return f;
            Transform root = opening.Boat.transform;
            BoatDeckDef live = BoatDeckAreas.Resolve(root.gameObject);
            f.Branch = live != null && live.HasHelmStation ? "deck def" : EnterHelm;
            BoatCutaway cut = root.GetComponentInChildren<BoatCutaway>();
            if (cut != null) f.Cut = cut.RequestedCut.ToString();

            Transform skipper = root.GetComponentsInChildren<Transform>(true)
                                    .FirstOrDefault(t => t.name == MooredBoat.SkipperChildName);
            if (skipper == null) return f;
            var sprite = skipper.GetComponent<SpriteRenderer>();
            var presenter = skipper.GetComponent<CharacterFigurePresenter>();
            f.Order = sprite != null ? sprite.sortingOrder : int.MinValue;
            f.Why = presenter != null ? presenter.WhyNot.ToString() : "no presenter";

            IsoFacetHullRenderer facet = root.GetComponentInChildren<IsoFacetHullRenderer>();
            Transform posed = facet != null ? facet.PosedMesh : null;
            if (posed == null)
            {
                f.Drawer = "no facet hull";
                return f;
            }
            if (presenter != null && presenter.DrawsInsteadOfSprite && presenter.Figure != null)
            {
                f.Drawer = Mesh;
                f.Root = posed.InverseTransformPoint(presenter.Figure.transform.position);
                f.Feet = AnklesOf(presenter.Figure, posed);
            }
            else if (sprite != null && sprite.enabled && !sprite.forceRenderingOff && sprite.sprite != null &&
                     sprite.gameObject.activeInHierarchy)
            {
                f.Drawer = Sprite;
                f.Root = OnHisDeck(posed, sprite.transform.position, _station.z);
                f.Feet = f.Root;
            }
            else
            {
                return f;
            }
            f.Error = Mathf.Max(Vector3.Distance(f.Root, _station), OffDeckPlane(f.Feet, _station));
            return f;
        }

        /// <summary>
        /// The middle of his ankles in her rig metres, read from the pose his figure DREW: its bone matrices for
        /// the frame (<c>_world</c>, the matrices it skinned its posed mesh with, so in that mesh's space) at
        /// <see cref="AnkleBoneIds"/>, carried through the posed mesh's transform into her hull's. NaN when the
        /// figure's pose or its posed mesh cannot be read.
        /// </summary>
        private static Vector3 AnklesOf(IsoCharacterFigureRenderer figure, Transform posed)
        {
            var nan = new Vector3(float.NaN, float.NaN, float.NaN);
            var def = FigureDef.GetValue(figure) as CharacterSkinDef;
            var world = FigureWorld.GetValue(figure) as Matrix4x4[];
            var mesh = FigurePosedMesh.GetValue(figure) as UnityEngine.Mesh;
            if (def == null || world == null || mesh == null || world.Length != def.Bones.Length) return nan;
            MeshFilter filter = figure.GetComponentsInChildren<MeshFilter>(true)
                                      .FirstOrDefault(m => m.sharedMesh == mesh);
            if (filter == null) return nan;
            Vector3 sum = Vector3.zero;
            foreach (string id in AnkleBoneIds)
            {
                int bone = Array.FindIndex(def.Bones, b => b.Id == id);
                if (bone < 0) return nan;
                Vector3 joint = world[bone].MultiplyPoint3x4(Vector3.zero);
                sum += posed.InverseTransformPoint(filter.transform.TransformPoint(joint));
            }
            return sum / AnkleBoneIds.Length;
        }

        private const System.Reflection.BindingFlags Private =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        private static readonly System.Reflection.FieldInfo FigureDef =
            typeof(IsoCharacterFigureRenderer).GetField("_def", Private);

        private static readonly System.Reflection.FieldInfo FigureWorld =
            typeof(IsoCharacterFigureRenderer).GetField("_world", Private);

        private static readonly System.Reflection.FieldInfo FigurePosedMesh =
            typeof(IsoCharacterFigureRenderer).GetField("_posedMesh", Private);

        /// <summary>How far <paramref name="point"/> stands off <paramref name="station"/> across her deck
        /// (+x, +y): his ankles stand above the deck by the height of his ankle, which is not an offset.</summary>
        private static float OffDeckPlane(Vector3 point, Vector3 station) =>
            new Vector2(point.x - station.x, point.y - station.y).magnitude;

        /// <summary>The TestContext line the owner asked for: his largest ankle offset from the station at this
        /// heading, over the frames his mesh drew him.</summary>
        private string AnkleLine(List<Frame> drawn, float heading)
        {
            List<Frame> mesh = drawn.Where(f => f.Drawer == Mesh && !float.IsNaN(f.Feet.x)).ToList();
            if (mesh.Count == 0) return $"[S7 ankles] heading {heading:0.0}°: his mesh drew him on no frame.";
            Frame worst = mesh.OrderByDescending(f => OffDeckPlane(f.Feet, _station)).First();
            return $"[S7 ankles] heading {heading:0.0}°: his largest ankle offset from the station " +
                   $"{OffDeckPlane(worst.Feet, _station):0.000} m (frame {worst.Index}, " +
                   $"{(worst.Below ? "below" : "on deck")}; the middle of his ankles {Fmt(worst.Feet)}, the station " +
                   $"{Fmt(_station)}), over {mesh.Count} mesh frames.";
        }

        /// <summary>
        /// Where on her deck a flat picture's pivot appears to stand: the orthographic camera's line of sight
        /// (world +z) through the pivot, met with the plane at <paramref name="heightMetres"/> in her posed mesh.
        /// </summary>
        private static Vector3 OnHisDeck(Transform posed, Vector3 world, float heightMetres)
        {
            Vector3 origin = posed.InverseTransformPoint(new Vector3(world.x, world.y, 0f));
            Vector3 sight = posed.InverseTransformVector(Vector3.forward);
            if (Mathf.Abs(sight.z) < 1e-6f) return new Vector3(float.NaN, float.NaN, heightMetres);
            return origin + sight * ((heightMetres - origin.z) / sight.z);
        }

        /// <summary>The station, from the hull's data, by the arrival's rule: her deck def, else enter_helm.</summary>
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

        // =============================================================================================
        //  what the reports say
        // =============================================================================================

        private string Describe(List<Frame> frames, float heading)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[S7] heading {heading:0.0}°; the station {Fmt(_station)} from {_stationSource}; " +
                          $"{frames.Count} frames read.");
            foreach (var part in Parts(frames))
            {
                List<Frame> run = part.Value;
                if (run.Count == 0) continue;
                string drawers = string.Join(", ", run.GroupBy(f => f.Drawer).Select(g => $"{g.Key} {g.Count()}"));
                string branches = string.Join(", ", run.GroupBy(f => f.Branch).Select(g => $"{g.Key} {g.Count()}"));
                string whys = string.Join(", ", run.GroupBy(f => f.Why).Select(g => $"{g.Key} {g.Count()}"));
                List<Frame> drawn = run.Where(f => !float.IsNaN(f.Error)).ToList();
                sb.AppendLine($"  {part.Key}: frames {run.First().Index}–{run.Last().Index}; drawn by {drawers}; " +
                              $"the arrival's station branch {branches}; the presenter says {whys}.");
                if (drawn.Count == 0) continue;
                Frame worst = drawn.OrderByDescending(f => f.Error).First();
                sb.AppendLine($"    worst frame {worst.Index} ({worst.Drawer}): off by {worst.Error:0.000} m; root " +
                              $"{Fmt(worst.Root)}, ankles {Fmt(worst.Feet)}; median off " +
                              $"{drawn.Select(f => f.Error).OrderBy(e => e).ElementAt(drawn.Count / 2):0.000} m.");
            }
            sb.Append(Transitions(frames));
            return sb.ToString();
        }

        /// <summary>Below before she walks out, on deck, and below after she walks back in.</summary>
        private static List<KeyValuePair<string, List<Frame>>> Parts(List<Frame> frames)
        {
            var parts = new List<KeyValuePair<string, List<Frame>>>();
            string[] names = { "below, before she walks out", "on deck", "below, after she walks back in" };
            int part = 0;
            var current = new List<Frame>();
            for (int i = 0; i < frames.Count; i++)
            {
                if (i > 0 && frames[i].Below != frames[i - 1].Below)
                {
                    parts.Add(new KeyValuePair<string, List<Frame>>(names[Mathf.Min(part, names.Length - 1)], current));
                    current = new List<Frame>();
                    part++;
                }
                current.Add(frames[i]);
            }
            parts.Add(new KeyValuePair<string, List<Frame>>(names[Mathf.Min(part, names.Length - 1)], current));
            return parts;
        }

        /// <summary>S8: every frame on which her side, his drawer, his sorting or the cut changed.</summary>
        private static string Transitions(List<Frame> frames)
        {
            var sb = new StringBuilder("[S8] the frames that changed (report only):\n");
            List<int> crossings = new List<int>();
            for (int i = 1; i < frames.Count; i++)
            {
                Frame a = frames[i - 1], b = frames[i];
                var what = new List<string>();
                if (a.Below != b.Below)
                {
                    what.Add(b.Below ? "SHE CROSSED IN" : "SHE CROSSED OUT");
                    crossings.Add(b.Index);
                }
                if (a.Drawer != b.Drawer) what.Add($"his drawer {a.Drawer} → {b.Drawer}");
                if (a.Order != b.Order) what.Add($"his sorting {a.Order} → {b.Order}");
                if (a.Cut != b.Cut) what.Add($"the cut {a.Cut} → {b.Cut}");
                if (what.Count == 0) continue;
                int nearest = crossings.Count > 0 ? crossings.Last() : -1;
                string rel = nearest >= 0 ? $" (crossing {(b.Index - nearest >= 0 ? "+" : "")}{b.Index - nearest})" : "";
                sb.AppendLine($"  frame {b.Index}{rel}: {string.Join("; ", what)}");
            }
            return sb.ToString();
        }

        private static string Fmt(Vector3 v) =>
            float.IsNaN(v.x) ? "(unread)" : $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";

        private string Where()
        {
            if (_opening == null) return "There is no opening.";
            return $"[{_opening.Current}] the player is " +
                   (_opening.IsBelowDecks ? $"BELOW at sole {_opening.CabinLocalPosition}" : "ON DECK") +
                   $" at {_player.transform.position}.";
        }
    }
}
