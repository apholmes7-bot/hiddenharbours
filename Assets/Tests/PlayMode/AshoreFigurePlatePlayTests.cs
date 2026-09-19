using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Vehicles;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>THE PLATES: the skinned player drawn ASHORE as her mesh figure</b> — the four GPU-only pixel
    /// claims #861 left to a picture (its condition d). Each claim gets a plate AND a number read from
    /// the plate's own pixels; a claim a plate cannot decide is written down as
    /// "unknown from this plate", never asserted:
    /// <list type="number">
    /// <item><b>she is drawn and inked through her own figure id</b> — before (the sprite) and after (the
    /// mesh) are shot in the SAME frame, same seed, same clock, same camera;</item>
    /// <item><b>IsoFacetOverlay keeps exactly her own pixels</b> — the overlay's footprint is compared
    /// with her id in <c>_HHHullScreenTex</c>, her cell padded 1 px;</item>
    /// <item><b>LampShadow, DeckOccludedSprite and IsoFacetResolve read her id correctly</b> — she cuts
    /// no fisher's sprite, and a hull's lamp shadow does not count her pixels as its own;</item>
    /// <item><b>HHHullGuard draws her while a displaced sea is live</b> — read from
    /// <c>_HHHullGuardTex</c> with and without her.</item>
    /// </list>
    ///
    /// <para><b>The shutters.</b> Every plate of a frame is shot inside ONE engine frame with no yield
    /// between them, by toggling renderers and calling <see cref="WharfNightStage.Capture"/> — so the
    /// water, the clock and the lamps cannot move between "before" and "after". Her sprite body is
    /// hidden with <see cref="Renderer.forceRenderingOff"/>, never <c>enabled = false</c>:
    /// <c>SpriteShadow</c> gates on <c>enabled</c>, so her sprite-sheet shadow (Q3: her shadow ashore
    /// stays the sprite sheet's silhouette) is in EVERY shutter and cancels out of every difference.
    /// The Q3 appendix then shows what <c>enabled = false</c> would have cost.</para>
    ///
    /// <para><b>The id and the guard are read INSIDE the render.</b> RenderGraph re-binds every global
    /// it published to 4x4 black when its graph ends, so a read after <c>Capture()</c> saw black on
    /// every render, and <c>4ade16c7</c> read "unknown" on every numeric claim. The
    /// <see cref="InFrameChannelReadback"/> pass (tests only) copies <c>_HHHullScreenTex</c> and
    /// <c>_HHHullGuardTex</c> out of the stage camera's graph while they are live, and each read below
    /// takes the copy that the capture just before it made.</para>
    ///
    /// <para><b>Frames 1–5 call <see cref="IsoCharacterFigureRenderer.EnterAshore"/> themselves</b>, on a
    /// plate figure built beside the skinned player inside the test, and
    /// <see cref="IsoCharacterFigureRenderer.LeaveAshore"/> in the teardown, with the switch OFF (the
    /// shipped asset), so the plate's figure is the only one ashore.</para>
    ///
    /// <para><b>⚠️ These SKIP on CI and prove nothing there</b> — a plate needs a GPU and CI runs the
    /// Null device. <b>⚠️ The plate directory is shared by every worktree</b>: a floor file is written
    /// first and only plates newer than it belong to this run. No scene is edited or saved; St Peters
    /// is the START scene and is left through <see cref="ShellFlow.Reset"/>, never
    /// <c>StartNewGame()</c>, which would overwrite the savegame every worktree shares.</para>
    ///
    /// <para><b>Every plate names its frame</b> in a caption beside it: scene, clock, her position, the
    /// camera's position and size, the plate's pixels, the sea and the lamps.</para>
    ///
    /// <para><b>The switch frames (S1–S5)</b> build no figure of their own and never touch her body. Each
    /// shoots one place three times: OFF (the shipped <c>GameConfig.asset</c>), ON (a runtime copy with
    /// <see cref="GameConfig.MeshCharacterAshore"/> set, published through
    /// <see cref="GameServices.Config"/>; the asset is never written), and OFF again. What draws her is
    /// <see cref="DeckRiderMeshPresenter"/>'s decision alone: the plate reads that decision, counts
    /// what draws her, and reads her id inside the frame. S1 is the flip plate; S4 lands on the re-seat
    /// call with no frame between; S5 is Nine Mile Creek, where the facet-id pool may refuse her, and
    /// then the refusal is the frame. A claim a plate cannot decide is written "unknown", never passed.</para>
    /// </summary>
    public class AshoreFigurePlatePlayTests
    {
        const string PlateDir           = "ashore-figure-plates";
        const string StPetersScene      = "StPeters";
        const string NineMileCreekScene = "NineMileCreek";
        const string OwnersFolder       = "Assets/_Project/Data/Boats/Owners";

        /// <summary>Midday: the sun is up, the lamps are off.</summary>
        const float Noon = 12f;
        /// <summary>Deep night: every lamp gated on and the tint long since still.</summary>
        const float Night = 2f;

        /// <summary>Compass south: she faces the camera, the facing her sprite and her mesh share.</summary>
        const float SouthDegrees = 180f;

        /// <summary>A pixel "changed" when its RGB sum moved by more than this (of 765).</summary>
        const int DiffThreshold = 6;
        /// <summary>The guard is R8: interior 1, exterior 0; a byte above this reads as interior.</summary>
        const int GuardHalf = 127;
        /// <summary>The guard read must sit on the hulls' own ids at least this well to be believed.</summary>
        const float GuardOnHullsAtLeast = 0.95f;

        /// <summary>How far she stands off the end of a vehicle: a step, clear of its collider.</summary>
        const float SideStepMetres = 0.7f;
        /// <summary>The frame's centre sits this far above her feet, so she is BELOW the middle of the
        /// plate and an id texture read upside down cannot land back on her.</summary>
        const float FrameLiftMetres = 1.2f;
        /// <summary>A lamp already standing within this of a builder site IS that site's lamp.</summary>
        const float LampSiteToleranceMetres = 1.5f;
        /// <summary>The north-face mooring, back from the pier head and clear of the builder's own dory.</summary>
        const float NorthMooringBackFromHeadMetres = 9.5f;
        /// <summary>A hull whose mesh carries no watertight half-beam is moored as if 1 m — named in the caption.</summary>
        const float FallbackHalfBeamMetres = 1f;

        const string Pass = "PASS";
        const string Fail = "FAIL";
        const string Unknown = "unknown from this plate";
        const string Info = "INFO";

        /// <summary>The asset the build ships: the switch frames read its OFF and never write it.</summary>
        const string ShippedConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        /// <summary>The name the presenter gives her ashore figure's GameObject.</summary>
        const string AshoreFigureName = "MeshCharacterAshore";
        /// <summary>Words of the presenter's own reasons (private there), matched as substrings.</summary>
        const string OffReasonMark = "MeshCharacterAshore is off";
        const string RefusedReasonMark = "facet-id pool is used up";
        /// <summary>How long the switch frames wait for the presenter, and for her headlamp, to appear.</summary>
        const int PresenterWaitFrames = 60;
        const int LampWaitFrames = 30;
        /// <summary>Her box on the plate, metres about her feet.</summary>
        const float HerBoxHalfWidthMetres = 0.7f;
        const float HerBoxBelowMetres = 0.4f;
        const float HerBoxHeightMetres = 2.2f;
        /// <summary>How far north of a fence rail she stands, so the rail is in front of her on screen.</summary>
        const float BehindTheFenceMetres = 0.4f;
        /// <summary>Where she lands on the wharf, in from the south lip, at the ladder.</summary>
        const float LadderHeadInFromLipMetres = 0.6f;
        /// <summary>The yards' fence pieces, by the dressing's own style keys (private there, so literals here).</summary>
        static readonly string[] FencePieceKeys = { "picketPanel", "fenceCorner", "picketGate", "postRail" };

        WharfNightStage _stage;
        InFrameChannelReadback _readback;

        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        bool _loadedAny;
        bool _introOnEntry;

        static string s_floor;

        // --- the frame --------------------------------------------------------------------------------
        string _scene;
        float _hour;
        float _plateOrtho;
        string _lampsNote;
        string _seaNote;
        DisplacedWaterSurface _seaTurnedOn;
        readonly List<string> _neighbours = new List<string>();

        // --- her --------------------------------------------------------------------------------------
        PlayerWalkController _player;
        SpriteRenderer _sprite;
        IsoCharacterSprite _iso;
        Rigidbody2D _body;
        bool _bodyTaken;
        bool _bodyWasSimulated;
        Vector2 _standAt;

        IsoCharacterFigureRenderer _figure;
        CharacterSkinDef _skin;
        string _pose;
        int _fid;
        float _yaw;

        // --- the moored hull (frames 4 and 5) ----------------------------------------------------------
        MooredBoat _boat;
        Transform _skipper;
        SpriteRenderer _skipperSprite;
        string _beamNote;

        // --- the switch (the S frames) -----------------------------------------------------------------
        GameConfig _shippedConfig;
        GameConfig _switchOn;
        string _configNote;
        string _switchNote;
        string _switchBodyNote;
        DeckRiderVisual _rider;
        DeckRiderMeshPresenter _presenter;
        Headlamp _headlampTurnedOn;
        WalkerLights _plateWalkerLights;
        string _lampUnlitWhy;
        readonly List<Renderer> _hiddenFence = new List<Renderer>();

        // --- the reading ------------------------------------------------------------------------------
        readonly List<string> _verdicts = new List<string>();
        int _fails;
        int _platesWritten;

        sealed class Arm
        {
            public string Key;
            public string Frame;
        }

        // =============================================================================================
        //  The floor, the snapshot and the #764 teardown
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The floor file, written before any plate.</b> The plate directory is one directory for
        /// the whole machine; <c>find -newer</c> against this file is the only way to say which pictures
        /// came out of THIS run.
        /// </summary>
        [OneTimeSetUp]
        public void WriteTheFloor()
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(dir);
            s_floor = Path.Combine(dir, "_floor.txt");
            File.WriteAllText(
                s_floor,
                "Floor for " + nameof(AshoreFigurePlatePlayTests) + " at " +
                System.DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                "Only plates in this directory NEWER than this file were shot by this run.\n",
                new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] FLOOR written: {s_floor} — read only plates newer than it " +
                      $"(`find \"{dir}\" -newer \"{s_floor}\" -name '*.png'`).");
        }

        [UnitySetUp]
        public IEnumerator SetUpRegion()
        {
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);

            _verdicts.Clear();
            _neighbours.Clear();
            _fails = 0;
            _platesWritten = 0;
            _lampsNote = "none placed by the plate";
            _seaNote = "as the scene loaded it";
            _beamNote = null;
            _switchNote = null; _lampUnlitWhy = null; _configNote = null; _switchBodyNote = null;
            yield return null;
        }

        /// <summary>
        /// Her first — the figure leaves ashore and gives its id back, the sprite is shown again, the
        /// held heading and the body are released, and a sea the plate switched on is switched off —
        /// then the #764 teardown: the stage unloads the region and every <c>DontDestroyOnLoad</c> root
        /// the region installed is destroyed by IDENTITY against the snapshot, never by name.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;   // ⚠ a STATIC: left at 0 by the frozen frame it stops every test after this one

            if (_figure != null)
            {
                _figure.Visible = false;
                _figure.LeaveAshore();
                Object.Destroy(_figure.gameObject);
            }
            if (_sprite != null)
            {
                _sprite.forceRenderingOff = false;
                _sprite.enabled = true;
            }
            if (_skipperSprite != null) _skipperSprite.enabled = true;
            if (_iso != null) _iso.ReleaseHeading();
            if (_body != null && _bodyTaken) _body.simulated = _bodyWasSimulated;
            if (_seaTurnedOn != null) _seaTurnedOn.SetDisplaced(false);
            // The switch frames: the shipped config back BEFORE the stage goes (GameRoot.OnDestroy nulls
            // GameServices.Config only while it holds its own asset), her lamp off, the fence shown.
            if (_shippedConfig != null) GameServices.Config = _shippedConfig;
            if (_switchOn != null) Object.Destroy(_switchOn);
            if (_headlampTurnedOn != null) _headlampTurnedOn.SetOn(false);
            if (_plateWalkerLights != null)
            {
                if (_plateWalkerLights.Beam != null) Object.Destroy(_plateWalkerLights.Beam.gameObject);
                if (_plateWalkerLights.Lantern != null) Object.Destroy(_plateWalkerLights.Lantern.gameObject);
                Object.Destroy(_plateWalkerLights.gameObject);
            }
            foreach (Renderer r in _hiddenFence) if (r != null) r.forceRenderingOff = false;
            _hiddenFence.Clear();

            _figure = null; _sprite = null; _iso = null; _body = null; _bodyTaken = false; _player = null;
            _skin = null; _fid = 0; _seaTurnedOn = null; _boat = null; _skipper = null; _skipperSprite = null;
            _shippedConfig = null; _switchOn = null; _rider = null; _presenter = null; _headlampTurnedOn = null; _plateWalkerLights = null;

            _readback?.Dispose();       // off beginCameraRendering, and its copies released
            _readback = null;

            if (_stage != null)
            {
                yield return _stage.TearDown();
                _stage = null;
            }

            if (!_loadedAny) yield break;

            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go)) Object.DestroyImmediate(go);

            GameServices.Reset();
            GameServices.OpeningCinematicRunning = _introOnEntry;
            ShellFlow.Reset();          // the slate the next fixture expects; publishes nothing
            InteractionGate.Reset();
            _loadedAny = false;
            yield return null;
        }

        // =============================================================================================
        //  The five frames
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>FRAME 1 — St Peters at noon, beside the UtilityQuad, the Trike200 and the Enduro250</b>
        /// (the machine row at the store; St Peters has no trucks). She stands a step off the quad's end
        /// of the row; the frame is centred on the row and holds all three.
        /// </summary>
        [UnityTest]
        public IEnumerator Ashore_BesideTheUtilityQuadTrikeAndEnduro_StPetersNoon()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Arrive(StPetersScene, Noon);

            List<GameObject> row = TheMachineRow(out Vector2 at, out Bounds rowBounds, out bool quadAtWest);

            yield return StandHer(at, new Vector2(rowBounds.center.x, at.y + FrameLiftMetres),
                                  rowBounds.size.x + 6f, "the whole machine row and 3 m either side");
            foreach (GameObject go in row) AssertTheSubjectIsInFrame(go.transform, $"frame 1: the {go.name}");

            yield return Shoot(new Arm
            {
                Key = "1-machines-stpeters-noon",
                Frame = $"{StPetersScene} 12:00, her {SideStepMetres:0.0} m off the {(quadAtWest ? "west" : "east")} " +
                        $"(UtilityQuad) end of the machine row at {Fmt(at)}; frame centred on the row",
            });
        }

        /// <summary>
        /// ⭐ <b>FRAME 2 — Nine Mile Creek at noon, beside its trucks.</b> She stands a step off the east
        /// end of the dually at the park; every vehicle within 30 m is named in the caption.
        /// </summary>
        [UnityTest]
        public IEnumerator Ashore_BesideTheTrucks_NineMileCreekNoon()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Arrive(NineMileCreekScene, Noon);

            GameObject truck = TheDually(out Bounds tb, out Vector2 at);
            NoteNeighbours(at, 30f);

            yield return StandHer(at, new Vector2(tb.center.x + 1f, at.y + FrameLiftMetres), tb.size.x + 8f,
                                  "the dually and 4 m either side");
            AssertTheSubjectIsInFrame(truck.transform, "frame 2: the dually");

            yield return Shoot(new Arm
            {
                Key = "2-dually-ninemile-noon",
                Frame = $"{NineMileCreekScene} 12:00, her {SideStepMetres:0.0} m off the east end of " +
                        $"{NineMileCreekTruckPark.TruckName} at {Fmt(at)}",
            });
        }

        /// <summary>
        /// ⭐ <b>FRAME 3 — St Peters at 02:00, under the pier-head lamp.</b> St Peters is committed
        /// without its pier lamps, so the builder's own <see cref="StPetersWharf.LampPostSites"/> are
        /// placed through <see cref="LampPosts.Place"/> (named in the caption). She stands 1 m south of
        /// the head lamp, in its light.
        /// </summary>
        [UnityTest]
        public IEnumerator Ashore_UnderTheHeadLamp_StPetersNight()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Arrive(StPetersScene, Night);
            yield return EnsureThePierLamps();

            Vector2 ladder = StPetersWharf.LadderPosition();
            var at = new Vector2(ladder.x, StPetersWharf.LampRowY - 1f);
            NoteNeighbours(at, 12f);

            yield return StandHer(at, at + new Vector2(0f, FrameLiftMetres), 10f, "her and the head lamp");

            yield return Shoot(new Arm
            {
                Key = "3-headlamp-stpeters-night",
                Frame = $"{StPetersScene} 02:00, her 1 m south of the pier-head lamp at {Fmt(at)}",
            });
        }

        /// <summary>
        /// ⭐ <b>FRAME 4 — St Peters at 02:00, a moored MESH hull under a lamp, her behind its
        /// skipper</b> — claim 3's frame. The hull is moored alongside the south (mooring) face under
        /// the ladder; she stands on the deck just behind it, so on screen the skipper's sprite stands
        /// over her id and the hull's lamp shadow has her pixels inside its rect. If no lamp reaches the
        /// hull, ONE plate lamp is added on the deck's south row and named.
        /// </summary>
        [UnityTest]
        public IEnumerator Ashore_BehindAMooredMeshHullsSkipper_UnderALamp_StPetersNight()
        {
            WharfNightStage.RequireAGraphicsDevice();
            BoatOwnerDef owner = PickMeshOwner();   // before the first yield: its skip must stay a skip

            yield return Arrive(StPetersScene, Night);
            yield return EnsureThePierLamps();

            float halfBeam = HalfBeamOf(owner);
            Vector2 ladder = StPetersWharf.LadderPosition();
            var boatAt = new Vector2(ladder.x,
                                     StPetersWharf.MooringFaceY - StPetersBuilder.AlongsideFenderGapMetres - halfBeam);
            yield return Moor(owner, boatAt, StPetersBuilder.DoryMooredHeadingDegrees);

            IsoFacetHullRenderer hull = _boat.GetComponentInChildren<IsoFacetHullRenderer>(true);
            if (hull == null)
                Assert.Fail($"[{PlateDir}] NO PLATE WRITTEN — {owner.Id} presented with no IsoFacetHullRenderer: " +
                            "it is not drawn as a mesh hull, so claim 3's hull frame does not exist.");
            yield return LightTheHull(hull);

            var at = new Vector2(_skipper.position.x, StPetersWharf.MooringFaceY + 0.2f);
            NoteNeighbours(at, 12f);

            yield return StandHer(at, new Vector2(boatAt.x, 0.5f * (at.y + boatAt.y) + FrameLiftMetres),
                                  owner.Boat.LengthMeters + 6f, $"{owner.Id}'s hull and 3 m either side");
            AssertTheSubjectIsInFrame(_boat.transform, "frame 4: the moored hull");

            yield return Shoot(new Arm
            {
                Key = "4-south-hull-lamp-stpeters-night",
                Frame = $"{StPetersScene} 02:00, {owner.Id} ({owner.Boat.name}, {owner.Boat.LengthMeters:0.0} m) " +
                        $"moored at the south face at {Fmt(boatAt)} heading {StPetersBuilder.DoryMooredHeadingDegrees:0}; " +
                        $"her on the deck at {Fmt(at)}, BEHIND the skipper on screen",
            });
        }

        /// <summary>
        /// ⭐ <b>FRAME 5 — St Peters at noon, her IN FRONT of a moored mesh hull with the displaced sea
        /// live</b> — claim 4's frame (and a second reading of claims 1–3). The same owner is moored
        /// alongside the north face, back from the head; if the scene does not load with a displaced sea
        /// live, the plate switches its <see cref="DisplacedWaterSurface"/> on (named, and off again in
        /// the teardown).
        /// </summary>
        [UnityTest]
        public IEnumerator Ashore_InFrontOfAMooredMeshHull_DisplacedSea_StPetersNoon()
        {
            WharfNightStage.RequireAGraphicsDevice();
            BoatOwnerDef owner = PickMeshOwner();

            yield return Arrive(StPetersScene, Noon);

            float halfBeam = HalfBeamOf(owner);
            var boatAt = new Vector2(StPetersWharf.HeadCellX - NorthMooringBackFromHeadMetres,
                                     StPetersWharf.NorthFaceY + StPetersBuilder.AlongsideFenderGapMetres + halfBeam);
            yield return Moor(owner, boatAt, StPetersBuilder.DoryMooredHeadingDegrees);
            yield return MakeTheSeaLive();

            var at = new Vector2(_skipper.position.x, StPetersWharf.NorthFaceY - 0.3f);
            NoteNeighbours(at, 12f);

            yield return StandHer(at, new Vector2(boatAt.x, 0.5f * (at.y + boatAt.y) + FrameLiftMetres),
                                  owner.Boat.LengthMeters + 6f, $"{owner.Id}'s hull and 3 m either side");
            AssertTheSubjectIsInFrame(_boat.transform, "frame 5: the moored hull");

            yield return Shoot(new Arm
            {
                Key = "5-north-hull-stpeters-noon",
                Frame = $"{StPetersScene} 12:00, {owner.Id} ({owner.Boat.name}, {owner.Boat.LengthMeters:0.0} m) " +
                        $"moored at the north face at {Fmt(boatAt)} heading {StPetersBuilder.DoryMooredHeadingDegrees:0}; " +
                        $"her on the deck at {Fmt(at)}, IN FRONT of the hull on screen; sea: {_seaNote}",
            });
        }

        // =============================================================================================
        //  The switch frames: OFF, ON, OFF again — the presenter decides, the plate reads
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>S1 — THE FLIP PLATE: St Peters at noon, beside the UtilityQuad, the Trike200 and the
        /// Enduro250</b>, frame 1's stand. The owner flips the switch on <c>b-on</c> against <c>a-off</c>:
        /// the same place, seed and clock, the shipped asset against a runtime copy with the switch on.
        /// </summary>
        [UnityTest]
        public IEnumerator Switch_BesideTheMachineRow_OffThenOn_StPetersNoon()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Arrive(StPetersScene, Noon);

            List<GameObject> row = TheMachineRow(out Vector2 at, out Bounds rowBounds, out bool quadAtWest);

            yield return StandHer(at, new Vector2(rowBounds.center.x, at.y + FrameLiftMetres),
                                  rowBounds.size.x + 6f, "the whole machine row and 3 m either side", plateFigure: false);
            foreach (GameObject go in row) AssertTheSubjectIsInFrame(go.transform, $"S1: the {go.name}");
            yield return FindHerPresenter();

            yield return ShootTheSwitch(new Arm
            {
                Key = "s1-switch-machines-stpeters-noon",
                Frame = $"{StPetersScene} 12:00, her {SideStepMetres:0.0} m off the {(quadAtWest ? "west" : "east")} " +
                        $"(UtilityQuad) end of the machine row at {Fmt(at)}; frame centred on the row; switch OFF / ON / OFF",
            });
        }

        /// <summary>
        /// ⭐⭐ <b>S2 — St Peters at 02:00, under HER OWN headlamp.</b> No pier lamp is placed: the light is
        /// the walker's headlamp, switched on by the plate. She stands at the ladder, a metre inside the
        /// lamp row. A lamp that will not light fails the frame by name AFTER its plates are written.
        /// </summary>
        [UnityTest]
        public IEnumerator Switch_UnderHerHeadlamp_OffThenOn_StPetersNight()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Arrive(StPetersScene, Night);

            Vector2 ladder = StPetersWharf.LadderPosition();
            var at = new Vector2(ladder.x, StPetersWharf.LampRowY - 1f);
            NoteNeighbours(at, 12f);

            yield return StandHer(at, at + new Vector2(0f, FrameLiftMetres), 10f, "her and her headlamp's pool",
                                  plateFigure: false);
            yield return FindHerPresenter();
            yield return TurnOnHerHeadlamp();

            yield return ShootTheSwitch(new Arm
            {
                Key = "s2-switch-headlamp-stpeters-night",
                Frame = $"{StPetersScene} {Night:00}:00, her at the ladder {Fmt(at)}, a metre inside the lamp row, under " +
                        "HER OWN headlamp; no pier lamp placed; switch OFF / ON / OFF",
            });
            if (_lampUnlitWhy != null)
                Assert.Fail($"[{PlateDir}] s2: plates written but the frame is not the charter's — {_lampUnlitWhy}.");
        }

        /// <summary>
        /// ⭐⭐ <b>S3 — St Peters at noon, behind a yard fence.</b> The nearest horizontal rail of the yards'
        /// dressing to her spawn (a picket panel, else a post-and-rail); she stands just north of it, so
        /// the rail is in front of her on screen. Every fence piece is hidden for one CONTROL shutter in
        /// each state, so the plate can say where her sprite and her mesh differ BEHIND the fence.
        /// </summary>
        [UnityTest]
        public IEnumerator Switch_BehindAYardFence_OffThenOn_StPetersNoon()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Arrive(StPetersScene, Noon);
            yield return FindHer();
            Vector2 spawn = _player.transform.position;

            GameObject yards = GameObject.Find(YardDressing.RootName);
            if (yards == null)
                Assert.Fail($"[{PlateDir}] NO PLATE WRITTEN — {StPetersScene} carries no '{YardDressing.RootName}' " +
                            "root, so there is no fence to stand her behind.");
            SpriteRenderer rail = PickTheRail(yards, "picketPanel", spawn);
            if (rail == null) rail = PickTheRail(yards, "postRail", spawn);
            if (rail == null)
                Assert.Fail($"[{PlateDir}] NO PLATE WRITTEN — '{YardDressing.RootName}' holds no enabled horizontal " +
                            "picketPanel or postRail piece, so there is no rail to stand her behind.");
            foreach (SpriteRenderer r in yards.GetComponentsInChildren<SpriteRenderer>())
                if (r.enabled && IsFencePiece(r.name)) _hiddenFence.Add(r);

            var at = new Vector2(rail.bounds.center.x, rail.transform.position.y + BehindTheFenceMetres);
            _neighbours.Add($"rail {(rail.transform.parent != null ? rail.transform.parent.name + "/" : "")}{rail.name} at " +
                            $"{Fmt(rail.transform.position)}, bounds {Fmt(rail.bounds.min)}..{Fmt(rail.bounds.max)}, " +
                            $"{Vector2.Distance(rail.transform.position, spawn):0.0} m from her spawn; " +
                            $"{_hiddenFence.Count} fence piece(s) in the control shutter");
            NoteNeighbours(at, 12f);

            yield return StandHer(at, at + new Vector2(0f, FrameLiftMetres), 8f, "her and the rail she stands behind",
                                  plateFigure: false);
            AssertTheSubjectIsInFrame(rail.transform, "S3: the fence rail");
            yield return FindHerPresenter();

            yield return ShootTheSwitch(new Arm
            {
                Key = "s3-switch-behind-fence-stpeters-noon",
                Frame = $"{StPetersScene} 12:00, her {BehindTheFenceMetres:0.0} m north of the rail '{rail.name}' at " +
                        $"{Fmt(at)} (the rail in front of her on screen); switch OFF / ON / OFF, each with a " +
                        "fence-hidden control shutter",
            }, fence: _hiddenFence);
        }

        /// <summary>
        /// ⭐⭐ <b>S4 — St Peters at noon, stepping off a moored hull.</b> The mesh owner is moored alongside
        /// under the ladder; she stands at the ladder head. ON is thrown by a RE-SEAT — the
        /// <see cref="DeckRiderVisual.SetMode"/> call <c>ControlSwitcher.ApplyPlayerFor</c> makes when she
        /// steps ashore — and captured with no frame between, so the plate shows what the call decided.
        /// Not a real boarding: her control mode is the one she arrived in.
        /// </summary>
        [UnityTest]
        public IEnumerator Switch_SteppingOffAMooredHull_LandsOnTheCall_StPetersNoon()
        {
            WharfNightStage.RequireAGraphicsDevice();
            BoatOwnerDef owner = PickMeshOwner();   // before the first yield: its skip must stay a skip

            yield return Arrive(StPetersScene, Noon);

            float halfBeam = HalfBeamOf(owner);
            Vector2 ladder = StPetersWharf.LadderPosition();
            var boatAt = new Vector2(ladder.x,
                                     StPetersWharf.MooringFaceY - StPetersBuilder.AlongsideFenderGapMetres - halfBeam);
            yield return Moor(owner, boatAt, StPetersBuilder.DoryMooredHeadingDegrees);

            var at = new Vector2(ladder.x, StPetersWharf.MooringFaceY + LadderHeadInFromLipMetres);
            NoteNeighbours(at, 12f);

            yield return StandHer(at, new Vector2(boatAt.x, 0.5f * (at.y + boatAt.y) + FrameLiftMetres),
                                  owner.Boat.LengthMeters + 6f, $"{owner.Id}'s hull and 3 m either side", plateFigure: false);
            AssertTheSubjectIsInFrame(_boat.transform, "S4: the moored hull");
            yield return FindHerPresenter();

            yield return ShootTheSwitch(new Arm
            {
                Key = "s4-switch-stepping-off-hull-stpeters-noon",
                Frame = $"{StPetersScene} 12:00, {owner.Id} ({owner.Boat.name}, {owner.Boat.LengthMeters:0.0} m) moored " +
                        $"alongside at {Fmt(boatAt)} heading {StPetersBuilder.DoryMooredHeadingDegrees:0}; her at the ladder " +
                        $"head {Fmt(at)}; ON by a RE-SEAT (DeckRiderVisual.SetMode(OnFoot, null), the call " +
                        "ControlSwitcher.ApplyPlayerFor makes when she steps ashore), captured with no frame between; " +
                        "not a real boarding",
            }, landing: true);
        }

        /// <summary>
        /// ⭐⭐ <b>S5 — Nine Mile Creek at noon, beside the dually: the refusal, shown honestly.</b> Frame 2's
        /// stand. Nine Mile Creek can use up the facet-id pool; when the pool refuses her, the frame must
        /// show her whole sprite and the presenter must SAY why. When it has room, the mesh claims apply.
        /// </summary>
        [UnityTest]
        public IEnumerator Switch_BesideTheDually_RefusalShownHonestly_NineMileCreekNoon()
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Arrive(NineMileCreekScene, Noon);
            int hullsAtArrival = IsoFacetHullRegistry.Count, figuresAtArrival = IsoFacetHullRegistry.FigureCount;

            GameObject truck = TheDually(out Bounds tb, out Vector2 at);
            NoteNeighbours(at, 30f);

            yield return StandHer(at, new Vector2(tb.center.x + 1f, at.y + FrameLiftMetres), tb.size.x + 8f,
                                  "the dually and 4 m either side", plateFigure: false);
            AssertTheSubjectIsInFrame(truck.transform, "S5: the dually");
            yield return FindHerPresenter();

            yield return ShootTheSwitch(new Arm
            {
                Key = "s5-switch-dually-ninemile-noon",
                Frame = $"{NineMileCreekScene} 12:00, her {SideStepMetres:0.0} m off the east end of " +
                        $"{NineMileCreekTruckPark.TruckName} at {Fmt(at)}; facet registry at arrival: {hullsAtArrival} " +
                        $"hull(s), {figuresAtArrival} figure id(s); switch OFF / ON / OFF",
            }, refusalIsTheFrame: true);
        }

        // =============================================================================================
        //  The shutters, the readings and the verdicts
        // =============================================================================================

        IEnumerator Shoot(Arm arm)
        {
            Camera cam = _stage.Camera;
            int w = _stage.Width, h = _stage.Height;
            AssertTheSubjectIsInFrame(_player.transform, arm.Key + ": her");
            ArmTheReadback(cam);

            // ---- THE SHUTTERS: one engine frame, no yield between them ------------------------------
            cam.orthographicSize = _plateOrtho;
            _sprite.enabled = true;              // SpriteShadow reads this: it stays TRUE throughout (Q3)
            _sprite.forceRenderingOff = true;    // her sprite BODY hidden, her sprite-sheet shadow kept
            _figure.Visible = false;
            byte[] c0 = _stage.Capture();
            byte[] c0b = _stage.Capture();

            _sprite.forceRenderingOff = false;
            byte[] spr = _stage.Capture();
            byte[] sprb = _stage.Capture();

            if (!_figure.EnterAshore(_sprite))
            {
                string why = !_figure.IsConfigured ? "the figure is not configured"
                           : _figure.GetComponentInParent<IsoFacetHullRenderer>() != null ? "she is under a hull (aboard)"
                           : "IsoFacetHullRegistry.RegisterFigure gave id 0 — the pool is starved; re-shoot in a " +
                             "FRESH editor on this class's filter alone";
                foreach (string claim in new[] { "1 drawn+inked by her id", "2 overlay keeps her pixels",
                                                 "3 fisher not cut", "3 lamp shadow/resolve", "4 guard draws her" })
                    Verdict(claim, arm, Unknown, "EnterAshore returned false: " + why);
                WriteVerdicts(arm, "NO PLATE WRITTEN");
                Assert.Fail($"[{PlateDir}] NO PLATE WRITTEN for {arm.Key}: EnterAshore(sortSource) returned " +
                            $"false — {why}.");
            }
            _fid = _figure.FigureId;
            _yaw = _skin.AzimuthCounterClockwise ? -SouthDegrees : SouthDegrees;
            _figure.SetAshoreYaw(_yaw);
            _figure.Visible = true;
            _sprite.forceRenderingOff = true;
            byte[] mesh = _stage.Capture();
            Raw rawIdsM = ReadGlobalChannel(IsoFacetShaderIds.HullScreenTex, "_HHHullScreenTex", 3, out string idHow);
            byte[] meshb = _stage.Capture();

            _figure.AshoreOverlay.enabled = false;     // her id still written; her colours not composited
            byte[] idOnly = _stage.Capture();
            Raw rawIdsI = ReadGlobalChannel(IsoFacetShaderIds.HullScreenTex, "_HHHullScreenTex", 3, out _);
            Raw rawGuardI = ReadGlobalChannel(IsoFacetShaderIds.GuardTex, "_HHHullGuardTex", 0, out string guardHow);

            _figure.Visible = false;                   // no mesh, no id: the "without her" control
            byte[] c1 = _stage.Capture();
            Raw rawIdsC1 = ReadGlobalChannel(IsoFacetShaderIds.HullScreenTex, "_HHHullScreenTex", 3, out _);
            Raw rawGuardC1 = ReadGlobalChannel(IsoFacetShaderIds.GuardTex, "_HHHullGuardTex", 0, out _);

            byte[] c1f = null, idOnlyF = null;
            if (_skipperSprite != null)
            {
                _skipperSprite.enabled = false;        // the fisher out, her id out
                c1f = _stage.Capture();
                _figure.Visible = true;
                _figure.AshoreOverlay.enabled = false; // the fisher out, her id in
                idOnlyF = _stage.Capture();
                _skipperSprite.enabled = true;
            }
            _figure.Visible = true;                    // back to "after": the mesh and its overlay

            Bounds quadBounds = _figure.AshoreOverlay.bounds;

            // ---- MAPPING: how the id texture lies on the plate — measured, not assumed ---------------
            // (an RTHandle sized by a larger earlier camera holds this render in its bottom-left
            // sub-rect; a scaled one is stretched; either may read upside down)
            bool[] changedC0M = Changed(c0, mesh);
            Mapping map = Calibrate(rawIdsM, changedC0M, out string orient);
            byte[] idsM = ToPlate(rawIdsM, map), idsI = ToPlate(rawIdsI, map), idsC1 = ToPlate(rawIdsC1, map);
            byte[] guardI = ToPlate(rawGuardI, map), guardC1 = ToPlate(rawGuardC1, map);
            int pad = rawIdsM == null || IsSubRect(map, rawIdsM) || rawIdsM.W >= w
                ? 1 : Mathf.CeilToInt(w / (float)rawIdsM.W);

            // ---- NOISE: A/A pairs from the same frame ----------------------------------------------
            bool[] aaC0 = Changed(c0, c0b), aaS = Changed(spr, sprb), aaM = Changed(mesh, meshb);
            bool[] noisy = Dilate(Or(Or(aaC0, aaS), aaM), w, h, 1);
            int noiseFloor = Mathf.Max(Count(aaC0), Mathf.Max(Count(aaS), Count(aaM)));
            int residue = Count(Changed(c0, c1));
            string noiseLine = $"A/A noise (same state twice): C0 {Count(aaC0)}, S {Count(aaS)}, M {Count(aaM)} px " +
                               $"-> floor {noiseFloor}; C0 vs C1 across EnterAshore {residue} px (INFO); " +
                               $"id read: {idHow}; mapping {orient}; pad {pad} px; guard read: {guardHow}";

            // ---- CLAIM 1: she is drawn, and inked, through her own figure id -----------------------
            bool idsReadable = idsM != null && idsI != null && idsC1 != null;
            bool[] her = idsReadable ? Where(idsM, _fid) : new bool[w * h];
            bool[] herI = idsReadable ? Where(idsI, _fid) : new bool[w * h];
            int herPx = Count(her);
            int spritePx = Count(Changed(c0, spr)), meshPx = Count(changedC0M), swapPx = Count(Changed(spr, mesh));
            bool[] ov = Changed(idOnly, mesh);
            int ovPx = Count(ov);
            int herChanged = Count(And(her, changedC0M));
            bool idSane = herPx > 0 && 2 * herChanged >= herPx;
            int idOnlyInside = Count(And(Changed(c1, idOnly), herI));
            bool anyOtherId = idsReadable && idsM.Any(b => b != 0 && b != _fid);

            bool[] ring = And(her, Not(Erode(her, w, h, 1)));
            bool[] core = Erode(her, w, h, 2);
            float ringLuma = WharfNightStage.MeanLuma(mesh, ring), coreLuma = WharfNightStage.MeanLuma(mesh, core);
            int ringDarker = 0;
            for (int p = 0; p < ring.Length; p++)
                if (ring[p] && WharfNightStage.Luma(mesh, p) < 0.8 * coreLuma) ringDarker++;

            string n1 = $"sprite {spritePx} px (C0->S), mesh {meshPx} px (C0->M), swap {swapPx} px (S->M); " +
                        $"her id {_fid} on {herPx} px, {herChanged} of them changed by her mesh; overlay {ovPx} px (I->M); " +
                        $"id-only inside her {idOnlyInside} px (C1->I, INFO); ink: ring {Count(ring)} px luma " +
                        $"{ringLuma:0.000} vs core {Count(core)} px {coreLuma:0.000}, {ringDarker} ring px darker than " +
                        "0.8 x core — judged by eye on b-mesh";
            if (meshPx == 0) Verdict("1 drawn+inked by her id", arm, Fail, "nothing of her mesh reached the picture; " + n1);
            else if (!idsReadable) Verdict("1 drawn+inked by her id", arm, Unknown, "the id texture could not be read; " + n1);
            else if (herPx == 0)
                Verdict("1 drawn+inked by her id", arm, anyOtherId ? Fail : Unknown,
                        (anyOtherId ? "other ids read back, hers is absent; " : "the id texture read all zeros; ") + n1);
            else if (!idSane) Verdict("1 drawn+inked by her id", arm, Unknown,
                                      "her id does not sit where her mesh changed the picture (misregistered read); " + n1);
            else if (ovPx == 0) Verdict("1 drawn+inked by her id", arm, Fail, "her id is written but the overlay draws nothing; " + n1);
            else Verdict("1 drawn+inked by her id", arm, Pass, n1);

            // ---- CLAIM 2: IsoFacetOverlay keeps exactly her own pixels (the cell padded 1 px) --------
            RectInt quadPx = ScreenRect(quadBounds.min, quadBounds.max, 1);
            int strayAtPad = Count(And(And(ov, Not(Dilate(her, w, h, pad))), Not(noisy)));
            int strayAtPad1 = Count(And(And(ov, Not(Dilate(her, w, h, pad + 1))), Not(noisy)));
            int herOutsideQuad = Count(And(her, Not(RectMask(quadPx, w, h))));
            int coverage = Count(And(ov, her));
            string n2 = $"overlay px off her id (dilated {pad}) {strayAtPad}, (dilated {pad + 1}) {strayAtPad1} (INFO); " +
                        $"her id px outside the overlay quad {quadPx} (+1 px) {herOutsideQuad}; overlay covers " +
                        $"{coverage} of her {herPx} id px (INFO)";
            if (!idsReadable || !idSane) Verdict("2 overlay keeps her pixels", arm, Unknown, "her id read is not usable; " + n2);
            else if (strayAtPad > 0 || herOutsideQuad > 0) Verdict("2 overlay keeps her pixels", arm, Fail, n2);
            else Verdict("2 overlay keeps her pixels", arm, Pass, n2);

            // ---- CLAIM 3a: DeckOccludedSprite — she cuts no fisher's sprite --------------------------
            if (_skipperSprite == null)
                Verdict("3 fisher not cut", arm, Unknown, "no fisher in this frame");
            else if (!idsReadable || !idSane)
                Verdict("3 fisher not cut", arm, Unknown, "her id read is not usable");
            else
            {
                bool[] fisherC1 = Changed(c1f, c1);
                bool[] fisherI = Changed(idOnlyF, idOnly);
                int overlap = Count(And(fisherC1, herI));
                bool[] lost = And(fisherC1, Not(fisherI));
                int cut = Count(And(And(lost, herI), Not(noisy)));
                int cutOutside = Count(And(And(lost, Not(herI)), Not(noisy)));
                int gained = Count(And(And(fisherI, Not(fisherC1)), herI));
                string n3 = $"fisher '{_skipper.name}' {Count(fisherC1)} px without her id, {Count(fisherI)} px with it; " +
                            $"{overlap} px under her id; cut under her {cut}, lost elsewhere {cutOutside} (INFO), " +
                            $"gained under her {gained} (INFO)";
                if (overlap == 0) Verdict("3 fisher not cut", arm, Unknown, "she does not overlap the fisher on screen; " + n3);
                else Verdict("3 fisher not cut", arm, cut > 0 ? Fail : Pass, n3);
            }

            // ---- CLAIM 3b: LampShadow + IsoFacetResolve — nothing outside her moves with her id -------
            bool lampsOn = WharfNightStage.LampsAreGatedOn(Shader.GetGlobalColor("_DayNightTint"));
            if (!idsReadable || !idSane)
                Verdict("3 lamp shadow/resolve", arm, Unknown, "her id read is not usable");
            else
            {
                bool[] herAny = Or(her, herI);
                bool[] far = And(Not(Dilate(herAny, w, h, pad + 2)), Not(noisy));
                bool[] near = And(Not(Dilate(herAny, w, h, pad)), Not(noisy));
                bool[] darker = Darker(c1, idOnly), lighter = Lighter(c1, idOnly), changed = Changed(c1, idOnly);
                int darkerOut = Count(And(darker, far)), lighterOut = Count(And(lighter, far));
                int changedOut = Count(And(changed, far)), darkerOutPad = Count(And(darker, near));
                var otherIds = idsC1.Where(b => b != 0 && b != _fid).Distinct().OrderBy(b => b).ToList();
                int herOverOther = 0;
                for (int p = 0; p < herI.Length; p++) if (herI[p] && idsC1[p] != 0 && idsC1[p] != _fid) herOverOther++;

                var hullRows = new List<string>();
                int decidable = 0;
                foreach (IsoFacetHullRenderer hr in Object.FindObjectsByType<IsoFacetHullRenderer>(FindObjectsSortMode.None)
                                                          .Where(x => x.HullId > 0).OrderBy(x => x.HullId))
                {
                    var caster = hr.GetComponent<HullLampShadowCaster>();
                    LampShadowCasterState st = default;
                    bool valid = caster != null && caster.isActiveAndEnabled && caster.TryGetLampShadowCaster(out st) && st.IsValid;
                    int herInRect = valid ? Count(And(herI, RectMask(ScreenRect(st.RectMin, st.RectMax, 0), w, h))) : 0;
                    SceneLight lamp = valid ? LampReaching(st.Foot, needShadows: true) : null;
                    if (valid && lamp != null && herInRect > 0) decidable++;
                    hullRows.Add($"hull '{hr.name}' id {hr.HullId}: caster {(caster == null ? "NONE" : valid ? "valid" : "not valid")}, " +
                                 $"her id px in its rect {herInRect}, shadow lamp in reach " +
                                 $"{(lamp != null ? $"'{lamp.name}' {Vector2.Distance(lamp.WorldOrigin, st.Foot):0.0} m" : "none")}");
                }
                string n3b = $"{(lampsOn ? "NIGHT" : "DAY")}; outside her (dilated {pad + 2}): darker {darkerOut}, lighter " +
                             $"{lighterOut} (INFO: a hole in a hull's shadow), changed {changedOut}; darker at dilate {pad} " +
                             $"{darkerOutPad} (INFO); other ids in the frame [{string.Join(",", otherIds)}], her px over " +
                             $"another id {herOverOther}; {decidable} hull(s) decide the lamp shadow — " +
                             (hullRows.Count > 0 ? string.Join("; ", hullRows) : "no mesh hull in the scene");
                if (lampsOn)
                {
                    if (darkerOut > 0) Verdict("3 lamp shadow/resolve", arm, Fail, "a lamp shadow grew with her id; " + n3b);
                    else if (decidable > 0) Verdict("3 lamp shadow/resolve", arm, Pass, n3b);
                    else Verdict("3 lamp shadow/resolve", arm, Unknown,
                                 "no hull with a valid caster, a shadow lamp in reach and her id in its rect; " + n3b);
                }
                else
                {
                    if (changedOut > 0) Verdict("3 lamp shadow/resolve", arm, Fail, "the picture moved outside her with her id; " + n3b);
                    else if (otherIds.Count == 0) Verdict("3 lamp shadow/resolve", arm, Unknown, "no other id in the frame to misread; " + n3b);
                    else Verdict("3 lamp shadow/resolve", arm, Pass, n3b);
                }
            }

            // ---- CLAIM 4: HHHullGuard draws her while a displaced sea is live ------------------------
            bool seaLive = DisplacedWaterRegistry.Count > 0;
            bool maskOn = IsoFacetHullFeature.InteriorMaskEnabled;
            string n4Head = $"sea live {seaLive} (DisplacedWaterRegistry.Count {DisplacedWaterRegistry.Count}; {_seaNote}); " +
                            $"InteriorMaskEnabled {maskOn}";
            if (!seaLive) Verdict("4 guard draws her", arm, Unknown, "no displaced sea is live, so no guard is recorded; " + n4Head);
            else if (!maskOn) Verdict("4 guard draws her", arm, Unknown, "the feature's interior mask is off; " + n4Head);
            else if (guardI == null || guardC1 == null) Verdict("4 guard draws her", arm, Unknown, $"{guardHow}; " + n4Head);
            else if (!idsReadable || !idSane) Verdict("4 guard draws her", arm, Unknown, "her id read is not usable; " + n4Head);
            else
            {
                bool[] gC1 = Above(guardC1, GuardHalf), gI = Above(guardI, GuardHalf);
                int interiorC1 = Count(gC1);
                int onHulls = 0;
                for (int p = 0; p < gC1.Length; p++) if (gC1[p] && idsC1[p] != 0 && idsC1[p] != _fid) onHulls++;
                float sanity = interiorC1 > 0 ? onHulls / (float)interiorC1 : 0f;
                int decidable = Count(And(herI, gC1));
                int flippedByHer = Count(And(And(herI, gC1), Not(gI)));
                bool[] guardMoved = new bool[gC1.Length];
                for (int p = 0; p < gC1.Length; p++) guardMoved[p] = gC1[p] != gI[p];
                int movedOutside = Count(And(guardMoved, Not(Dilate(herI, w, h, pad))));
                int herWritesInterior = Count(And(And(herI, gI), Not(gC1)));
                string n4 = $"{n4Head}; guard interior {interiorC1} px, {sanity:P1} on the hulls' ids; her id over " +
                            $"interior {decidable} px, of which drawn out by her {flippedByHer}; guard moved outside " +
                            $"her (dilated {pad}) {movedOutside}; her px turned INTERIOR {herWritesInterior} (INFO)";
                if (interiorC1 == 0 || sanity < GuardOnHullsAtLeast)
                    Verdict("4 guard draws her", arm, Unknown, "the guard read does not sit on the hulls' ids; " + n4);
                else if (decidable == 0)
                    Verdict("4 guard draws her", arm, Unknown, "she stands over no guarded interior in this frame; " + n4);
                else
                    Verdict("4 guard draws her", arm, flippedByHer == decidable && movedOutside == 0 ? Pass : Fail, n4);
            }

            // ---- THE PLATES --------------------------------------------------------------------------
            SavePlate(arm, "a-sprite", spr, "BEFORE: her sprite (the figure not ashore)");
            SavePlate(arm, "b-mesh", mesh, $"AFTER: her mesh figure ashore, id {_fid}, sprite body hidden (forceRenderingOff)");
            SavePlate(arm, "c-control-body-hidden", c0, "CONTROL: neither body drawn; her sprite-sheet shadow kept");
            SavePlate(arm, "d-id-only-overlay-off", idOnly, "her mesh writing its id, her overlay OFF (what her id alone does)");
            if (idsReadable)
            {
                SavePlate(arm, "e-her-id-mask", Tint(mesh, her), $"b-mesh with every pixel holding id {_fid} tinted magenta");
            }
            if (guardI != null && guardC1 != null)
            {
                SavePlate(arm, "f-guard-with-her", Grey(guardI), "_HHHullGuardTex R, her id written (white = interior)");
                SavePlate(arm, "g-guard-without-her", Grey(guardC1), "_HHHullGuardTex R, without her (white = interior)");
            }
            if (idOnlyF != null)
                SavePlate(arm, "h-fisher-hidden", idOnlyF, "d-id-only with the fisher's sprite hidden");

            // ---- Q3 APPENDIX: what enabled = false would have cost her shadow (frames yield here) ----
            _sprite.enabled = true;
            _sprite.forceRenderingOff = true;
            _figure.Visible = true;
            yield return null; yield return null;
            cam.orthographicSize = _plateOrtho;
            byte[] qa = _stage.Capture();
            _sprite.enabled = false;
            yield return null; yield return null;
            cam.orthographicSize = _plateOrtho;
            byte[] qoff = _stage.Capture();
            _sprite.enabled = true;
            yield return null; yield return null;
            cam.orthographicSize = _plateOrtho;
            byte[] qb = _stage.Capture();
            bool[] qaa = Changed(qa, qb);
            int shadowLost = Count(And(Lighter(qa, qoff), Not(qaa)));
            Verdict("Q3 sprite-sheet shadow", arm, Info, shadowLost > 0
                ? $"forceRenderingOff keeps {shadowLost} px of her sprite-sheet shadow that enabled=false drops " +
                  $"(A/A across the yields {Count(qaa)} px)"
                : $"{Unknown}: no pixel lightened when the sprite was disabled (A/A across the yields {Count(qaa)} px)");
            SavePlate(arm, "q-sprite-disabled", qoff, "Q3: her mesh figure with the SPRITE DISABLED (enabled=false) — " +
                                                      "compare b-mesh: the sprite-sheet shadow SpriteShadow drops");

            WriteVerdicts(arm, noiseLine);

            Assert.Greater(_platesWritten, 0, $"[{PlateDir}] {arm.Key}: no plate was written.");
            if (_fails > 0)
                Assert.Fail($"[{PlateDir}] {arm.Key}: {_fails} claim(s) FAILED on the plate's own pixels —\n" +
                            string.Join("\n", _verdicts.Where(v => v.Contains("| " + Fail + " |"))));
        }

        /// <summary>
        /// ⭐⭐ <b>THE SWITCH, shot three times in one place</b>: OFF (the shipped asset), ON (a runtime copy
        /// through <see cref="GameServices.Config"/>; the asset is never written), OFF again. The presenter
        /// alone decides what draws her; this reads what it decided and what reached the picture. Her figure
        /// is hidden for one CONTROL shutter only (and every fence piece for another, when a fence is named).
        /// </summary>
        IEnumerator ShootTheSwitch(Arm arm, bool refusalIsTheFrame = false, List<Renderer> fence = null,
                                   bool landing = false)
        {
            Camera cam = _stage.Camera;
            int w = _stage.Width, h = _stage.Height;
            AssertTheSubjectIsInFrame(_player.transform, arm.Key + ": her");
            ArmTheReadback(cam);
            SpriteRenderer body = _rider.BodyRenderer;

            // ---- OFF: the shipped frame ----
            SetTheSwitch(false);
            yield return Settle();
            cam.orthographicSize = _plateOrtho;
            string stOff = SwitchState("OFF");
            IsoCharacterFigureRenderer figOff = _presenter.AshoreFigure;
            bool offRight = !_presenter.DrawsAshore && Shown(body) && (figOff == null || !figOff.Visible);
            int drawersOff = Drawers(out string whichOff);
            int figuresBefore = IsoFacetHullRegistry.FigureCount;
            byte[] shotOff = _stage.Capture();
            Raw rawIdsOff = ReadGlobalChannel(IsoFacetShaderIds.HullScreenTex, "_HHHullScreenTex", 3, out string idHowOff);
            byte[] shotOffB = _stage.Capture();
            byte[] shotOffNoFence = null;
            if (fence != null)
            {
                foreach (Renderer r in fence) r.forceRenderingOff = true;   // CONTROL, one shutter: the fence out
                shotOffNoFence = _stage.Capture();
                foreach (Renderer r in fence) r.forceRenderingOff = false;
            }
            yield return Settle();
            cam.orthographicSize = _plateOrtho;
            byte[] shotOffLater = _stage.Capture();

            // ---- ON: the owner's flip ----
            SetTheSwitch(true);
            int reseatWas = _rider.ReseatCount;
            bool drawsOnCall = false;
            int reseatAfterCall = reseatWas;
            if (landing)
            {
                // The call ControlSwitcher.ApplyPlayerFor makes when she steps ashore (:2543): SetMode runs
                // Apply() synchronously, so the NEXT capture shows what it decided. No yield between.
                _rider.SetMode(ControlMode.OnFoot, null);
                drawsOnCall = _presenter.DrawsAshore;
                reseatAfterCall = _rider.ReseatCount;
            }
            else
            {
                yield return Settle();
            }
            cam.orthographicSize = _plateOrtho;
            string stOn = SwitchState("ON");
            IsoCharacterFigureRenderer fig = _presenter.AshoreFigure;
            bool refused = _presenter.AshoreRefused;
            int drawersOn = Drawers(out string whichOn);
            bool onRight = _presenter.DrawsAshore && fig != null && fig.Visible && body != null && body.enabled &&
                           body.forceRenderingOff;
            byte[] shotOn = _stage.Capture();
            Raw rawIdsOn = ReadGlobalChannel(IsoFacetShaderIds.HullScreenTex, "_HHHullScreenTex", 3, out string idHowOn);
            byte[] shotOnB = _stage.Capture();
            byte[] shotOnBare = null;
            if (fig != null && fig.Visible)
            {
                fig.Visible = false;     // CONTROL, one shutter: her figure out; the body the presenter hid stays hidden
                shotOnBare = _stage.Capture();
                fig.Visible = true;
            }
            byte[] shotOnNoFence = null;
            if (fence != null)
            {
                foreach (Renderer r in fence) r.forceRenderingOff = true;
                shotOnNoFence = _stage.Capture();
                foreach (Renderer r in fence) r.forceRenderingOff = false;
            }
            _fid = fig != null ? fig.FigureId : 0;
            _yaw = _presenter.AshoreYawDegrees;
            _pose = fig != null && fig.Visible ? $"'{fig.DrawnStateKey}' frame {fig.DrawnFrame}"
                                               : "none drawn (no visible ashore figure)";
            int figuresOn = IsoFacetHullRegistry.FigureCount;
            string stNext = null;
            byte[] shotOnNext = null;
            if (landing)
            {
                yield return Settle();
                cam.orthographicSize = _plateOrtho;
                stNext = SwitchState("ON, two frames after the call");
                shotOnNext = _stage.Capture();
            }

            // ---- OFF again ----
            SetTheSwitch(false);
            yield return Settle();
            cam.orthographicSize = _plateOrtho;
            string stOff2 = SwitchState("OFF again");
            int drawersOff2 = Drawers(out string whichOff2);
            int figuresAfter = IsoFacetHullRegistry.FigureCount;
            IsoCharacterFigureRenderer fig2 = _presenter.AshoreFigure;
            bool child2 = HasChildNamed(_player.transform, AshoreFigureName);
            byte[] shotOff2 = _stage.Capture();
            Raw rawIdsOff2 = ReadGlobalChannel(IsoFacetShaderIds.HullScreenTex, "_HHHullScreenTex", 3, out string idHowOff2);

            // ---- the noise, her box and the id mapping ----
            bool[] aaOff = Changed(shotOff, shotOffB), aaOn = Changed(shotOn, shotOnB);
            bool[] aaLater = Changed(shotOff, shotOffLater);
            bool[] noisy = Dilate(Or(Or(aaOff, aaOn), aaLater), w, h, 1);
            RectInt boxRect = HerBox(0f);
            bool[] box = RectMask(boxRect, w, h);
            bool[] boxWide = RectMask(HerBox(0.5f), w, h);
            bool[] changedMesh = shotOnBare != null ? Changed(shotOnBare, shotOn) : Changed(shotOff, shotOn);
            Mapping map = Mapping.Scaled;
            string orient = "not calibrated (she holds no figure id)";
            if (_fid != 0) map = Calibrate(rawIdsOn, changedMesh, out orient);
            byte[] idsOff = ToPlate(rawIdsOff, map), idsOn = ToPlate(rawIdsOn, map), idsOff2 = ToPlate(rawIdsOff2, map);
            string noiseLine = $"A/A noise (same state twice): OFF {Count(aaOff)}, ON {Count(aaOn)} px; OFF across two " +
                               $"frames {Count(aaLater)} px; her box {boxRect} px; id reads: OFF {idHowOff}; ON {idHowOn}; " +
                               $"OFF again {idHowOff2}; mapping {orient}";

            // ---- S-a: OFF is main's frame ----
            int spriteInBox = shotOnBare != null ? Count(And(And(Changed(shotOnBare, shotOff), box), Not(noisy))) : -1;
            string na = $"{stOff}; her sprite in her box " +
                        (spriteInBox >= 0 ? $"{spriteInBox} px (OFF vs ON with her figure hidden)" : "not priced");
            if (!offRight || spriteInBox == 0) Verdict("S-a off: the sprite draws", arm, Fail, na);
            else if (spriteInBox < 0)
                Verdict("S-a off: the sprite draws", arm, Unknown,
                        na + " — the state is main's but there is no bare frame to price her sprite against");
            else Verdict("S-a off: the sprite draws", arm, Pass, na);
            if (_fid == 0 || idsOff == null)
                Verdict("S-a off: no id of hers", arm, Unknown, _fid == 0 ? "she held no figure id on ON" : idHowOff);
            else
            {
                int herIdOff = Count(Where(idsOff, _fid));
                Verdict("S-a off: no id of hers", arm, herIdOff > 0 ? Fail : Pass, $"id {_fid} on {herIdOff} px of the OFF frame");
            }

            // ---- S-b: ON ----
            int meshIn = Count(And(And(changedMesh, box), Not(noisy)));
            int meshOut = Count(And(And(changedMesh, Not(boxWide)), Not(noisy)));
            string nb = $"{stOn}; mesh px in her box {meshIn}, outside her wide box {meshOut}; " +
                        $"figure ids {figuresBefore} -> {figuresOn}";
            bool meshClaims = true;
            if (refusalIsTheFrame && refused)
            {
                meshClaims = false;
                bool stateRight = !_presenter.DrawsAshore && Shown(body) && (fig == null || !fig.Visible) &&
                                  (_presenter.NotDrawingReason ?? "").Contains(RefusedReasonMark);
                int moved = Count(And(And(Changed(shotOff, shotOn), box), Not(noisy)));
                string nr = $"{stOn}; her box moved {moved} px OFF -> ON";
                if (!stateRight) Verdict("S-b on: refused honestly", arm, Fail, nr);
                else if (moved > 0) Verdict("S-b on: refused honestly", arm, Unknown, nr + " — the state is honest but her box moved");
                else Verdict("S-b on: refused honestly", arm, Pass, nr);
            }
            else if (refusalIsTheFrame)
                Verdict("S-b on: refused honestly", arm, Info,
                        "not refused on this run: the pool had room, so the mesh claims below apply");
            if (meshClaims)
            {
                if (!onRight)
                    Verdict("S-b on: the mesh draws", arm, Fail, nb + $" — the presenter did not draw her: '{_presenter.NotDrawingReason}'");
                else if (meshIn == 0)
                    Verdict("S-b on: the mesh draws", arm, Fail, nb + " — the state says drawn but no pixel of her box changed");
                else Verdict("S-b on: the mesh draws", arm, Pass, nb);
                if (meshOut > 0) Verdict("S-b on: mesh outside her box", arm, Info, nb);

                if (_fid == 0 || idsOn == null)
                    Verdict("S-b on: by her own id", arm, Unknown, _fid == 0 ? "she holds no figure id" : idHowOn);
                else
                {
                    bool[] her = Where(idsOn, _fid);
                    int herPx = Count(her);
                    int herChanged = Count(And(her, changedMesh));
                    bool idSane = herPx > 0 && 2 * herChanged >= herPx;
                    bool anyOtherId = idsOn.Any(b => b != 0 && b != _fid);
                    string ni = $"id {_fid} on {herPx} px, {herChanged} of them on her changed pixels; mapping {orient}";
                    if (herPx == 0)
                        Verdict("S-b on: by her own id", arm, anyOtherId ? Fail : Unknown,
                                ni + (anyOtherId ? " — other ids read, hers absent" : " — the id texture read empty"));
                    else if (!idSane) Verdict("S-b on: by her own id", arm, Unknown, ni + " — misregistered read");
                    else Verdict("S-b on: by her own id", arm, Pass, ni);
                }
            }

            // ---- S-c: one drawer in every state ----
            Verdict("S-c one drawer", arm, drawersOff == 1 && drawersOn == 1 && drawersOff2 == 1 ? Pass : Fail,
                    $"OFF {drawersOff} ({whichOff}); ON {drawersOn} ({whichOn}); OFF again {drawersOff2} ({whichOff2})");

            // ---- S-d: OFF again is main's frame again ----
            bool stateBack = !_presenter.DrawsAshore && fig2 == null && !child2 && figuresAfter == figuresBefore &&
                             Shown(body) && (_presenter.NotDrawingReason ?? "").Contains(OffReasonMark);
            int herIdOff2 = _fid != 0 && idsOff2 != null ? Count(Where(idsOff2, _fid)) : 0;
            int moved2 = Count(And(And(Changed(shotOff, shotOff2), box), Not(noisy)));
            string nd = $"{stOff2}; figure ids {figuresBefore} -> {figuresOn} -> {figuresAfter}; id {_fid} on " +
                        $"{herIdOff2} px; her box moved {moved2} px vs OFF";
            if (!stateBack || herIdOff2 > 0) Verdict("S-d off again: nothing", arm, Fail, nd);
            else if (moved2 > 0) Verdict("S-d off again: nothing", arm, Unknown, nd + " — the state is back but her box moved");
            else Verdict("S-d off again: nothing", arm, Pass, nd);

            // ---- S-e: the fence stays in front ----
            if (fence != null)
            {
                bool[] fenceOff = Erode(Changed(shotOffNoFence, shotOff), w, h, 1);
                int decidable = Count(And(And(And(fenceOff, Changed(shotOffNoFence, shotOnNoFence)), box), Not(noisy)));
                int leak = Count(And(And(And(fenceOff, Changed(shotOff, shotOn)), box), Not(noisy)));
                string ne = $"{fence.Count} fence piece(s); fence px where her sprite and mesh differ behind it " +
                            $"{decidable}; fence px that changed OFF -> ON {leak}";
                if (decidable == 0) Verdict("S-e fence stays in front", arm, Unknown, ne + " — nothing of her differs behind the fence");
                else Verdict("S-e fence stays in front", arm, leak > 0 ? Fail : Pass, ne);
            }

            // ---- S-f, S-g: the landing ----
            if (landing)
            {
                Verdict("S-f lands on the call", arm, drawsOnCall && meshIn > 0 && reseatAfterCall > reseatWas ? Pass : Fail,
                        $"ReseatCount {reseatWas} -> {reseatAfterCall}; DrawsAshore on the call {drawsOnCall}; " +
                        $"mesh px in her box on the call {meshIn}");
                int pop = Count(And(And(Changed(shotOn, shotOnNext), box), Not(noisy)));
                Verdict("S-g no pop after the call", arm, pop == 0 ? Pass : Info,
                        $"{stNext}; her box changed {pop} px from the call's frame to two frames later");
            }

            // ---- the plates ----
            _switchNote = $"{_configNote}; ON = a runtime copy '{(_switchOn != null ? _switchOn.name : "?")}' published " +
                          "through GameServices.Config (the asset is never written); " +
                          $"{_switchBodyNote}; {stOff} | {stOn} | {stOff2}" +
                          (landing ? $" | landing: ReseatCount {reseatWas} -> {reseatAfterCall}, DrawsAshore on the call {drawsOnCall}" : "");
            SavePlate(arm, "a-off", shotOff, "OFF — the shipped asset (MeshCharacterAshore off): her sprite, main's frame");
            SavePlate(arm, "b-on", shotOn, refused
                ? "ON — the pool refused her an id: she keeps her whole sprite, and the frame says so"
                : $"ON — the presenter's ashore mesh, id {_fid}, her sprite body hidden by the presenter");
            SavePlate(arm, "c-off-again", shotOff2, "OFF again — her sprite; the ashore figure and its id gone");
            if (idsOn != null && _fid != 0)
                SavePlate(arm, "d-her-id-mask", Tint(shotOn, Where(idsOn, _fid)), $"b-on with every pixel holding id {_fid} tinted magenta");
            if (shotOffNoFence != null) SavePlate(arm, "e-off-fence-hidden", shotOffNoFence, "CONTROL: OFF with every fence piece hidden");
            if (shotOnNoFence != null) SavePlate(arm, "e-on-fence-hidden", shotOnNoFence, "CONTROL: ON with every fence piece hidden");
            if (shotOnNext != null) SavePlate(arm, "e-on-next", shotOnNext, "ON, two frames after the re-seat call");
            if (shotOnBare != null)
                SavePlate(arm, "f-on-bare", shotOnBare, "CONTROL: ON with her figure hidden (the body stays hidden by the presenter)");
            WriteVerdicts(arm, noiseLine);

            Assert.Greater(_platesWritten, 0, $"[{PlateDir}] {arm.Key}: no plate was written.");
            if (_fails > 0)
                Assert.Fail($"[{PlateDir}] {arm.Key}: {_fails} claim(s) FAILED on the plate's own pixels —\n" +
                            string.Join("\n", _verdicts.Where(v => v.Contains("| " + Fail + " |"))));
        }

        void Verdict(string claim, Arm arm, string outcome, string numbers)
        {
            _verdicts.Add($"{claim,-26} | {arm.Key} | {outcome} | {numbers}");
            if (outcome == Fail) _fails++;
        }

        void WriteVerdicts(Arm arm, string head)
        {
            var sb = new StringBuilder();
            sb.AppendLine(FrameNote(arm, "VERDICTS — each claim, its outcome and the numbers read from this frame's pixels"));
            sb.AppendLine(head);
            sb.AppendLine();
            foreach (string v in _verdicts) sb.AppendLine(v);
            string file = Path.Combine(Application.temporaryCachePath, PlateDir, $"{arm.Key}-verdicts.txt");
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] VERDICTS — {file}\n{sb}");
        }

        // =============================================================================================
        //  Reading the id and the guard back
        // =============================================================================================

        /// <summary>One channel of a texture as it came off the GPU, before it is laid on the plate.</summary>
        sealed class Raw
        {
            public byte[] Px;
            public int W, H;
        }

        /// <summary>How a texture's texels lie on the plate's pixels (bottom-left origin).</summary>
        enum Mapping { Scaled, ScaledFlipped, SubRect, SubRectFlipped }

        bool IsSubRect(Mapping m, Raw raw) =>
            (m == Mapping.SubRect || m == Mapping.SubRectFlipped) && raw.W >= _stage.Width && raw.H >= _stage.Height;

        /// <summary>
        /// The mapping that puts her figure id on the pixels her mesh changed (C0 -> M) most often; every
        /// candidate's score is written down. Same-size textures make Scaled the identity.
        /// </summary>
        Mapping Calibrate(Raw ids, bool[] herChanged, out string note)
        {
            if (ids == null) { note = "not read"; return Mapping.Scaled; }
            int w = _stage.Width, h = _stage.Height;
            Mapping best = Mapping.Scaled;
            int bestScore = -1;
            var scores = new List<string>();
            foreach (Mapping m in new[] { Mapping.Scaled, Mapping.ScaledFlipped, Mapping.SubRect, Mapping.SubRectFlipped })
            {
                bool sub = m == Mapping.SubRect || m == Mapping.SubRectFlipped;
                if (sub && (ids.W < w || ids.H < h || (ids.W == w && ids.H == h))) continue;   // impossible, or = Scaled
                int s = Count(And(Where(ToPlate(ids, m), _fid), herChanged));
                scores.Add($"{m} {s}");
                if (s > bestScore) { bestScore = s; best = m; }
            }
            note = $"{best} ({ids.W}x{ids.H} onto {w}x{h}; her id on her changed pixels: {string.Join(", ", scores)})" +
                   (bestScore == 0 ? " — NO mapping puts her id on her mesh" : "");
            return best;
        }

        byte[] ToPlate(Raw raw, Mapping m)
        {
            if (raw == null) return null;
            int w = _stage.Width, h = _stage.Height;
            bool sub = IsSubRect(m, raw);
            bool flip = m == Mapping.ScaledFlipped || m == Mapping.SubRectFlipped;
            var o = new byte[w * h];
            for (int y = 0; y < h; y++)
            {
                int sy = sub || raw.H == h ? y : Mathf.Min(raw.H - 1, (int)((y + 0.5f) * raw.H / h));
                if (flip) sy = raw.H - 1 - sy;
                for (int x = 0; x < w; x++)
                {
                    int sx = sub || raw.W == w ? x : Mathf.Min(raw.W - 1, (int)((x + 0.5f) * raw.W / w));
                    o[y * w + x] = raw.Px[sy * raw.W + sx];
                }
            }
            return o;
        }

        /// <summary>
        /// Copy the facet block's two globals out of every render of the stage camera, inside the frame.
        /// Armed before the first shutter and disarmed in the teardown.
        /// </summary>
        void ArmTheReadback(Camera cam)
        {
            if (_readback == null)
            {
                _readback = new InFrameChannelReadback();
                _readback.Watch(IsoFacetShaderIds.HullScreenTex, "_HHHullScreenTex");
                _readback.Watch(IsoFacetShaderIds.GuardTex, "_HHHullGuardTex");
            }
            _readback.Arm(cam);
        }

        /// <summary>
        /// One channel of a global texture the facet block published in the render that just ran, as it came
        /// off the GPU. <see cref="InFrameChannelReadback"/> copied it out inside that render, before
        /// RenderGraph re-bound it to black. When that render made no copy (the global was not published,
        /// the graph's handle could not be had, the copy never ran), this returns null with the reason in
        /// <paramref name="how"/>: "unknown from this plate".
        /// </summary>
        Raw ReadGlobalChannel(int propertyId, string name, int channel, out string how)
        {
            string note = "the in-frame readback is not armed";
            RenderTexture rt = _readback?.Latest(propertyId, out note);
            if (rt == null || !rt.IsCreated())
            {
                how = $"{Unknown}: {name}: {note}";
                return null;
            }

            int tw = rt.width, th = rt.height;
            RenderTexture tmp = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(rt, tmp);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = tmp;
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false, true);
            tex.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Color32[] px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            RenderTexture.ReleaseTemporary(tmp);

            var raw = new Raw { Px = new byte[tw * th], W = tw, H = th };
            for (int i = 0; i < raw.Px.Length; i++) raw.Px[i] = channel == 3 ? px[i].a : px[i].r;
            how = $"{name}: {note} (plate {_stage.Width}x{_stage.Height})";
            return raw;
        }

        // =============================================================================================
        //  The region, her, the hull, the lamps and the sea
        // =============================================================================================

        IEnumerator Arrive(string scene, float hour)
        {
            _scene = scene;
            _hour = hour;
            _introOnEntry = GameServices.OpeningCinematicRunning;
            _loadedAny = true;
            _stage = new WharfNightStage(scene, PlateDir);
            yield return _stage.Load();

            // ⚠️ St Peters IS the start scene: Reset() is a slate wipe that publishes nothing;
            // StartNewGame() would begin the arrival AND overwrite the savegame every worktree shares.
            if (ShellFlow.WorldInputBlocked)
            {
                Debug.Log($"[{PlateDir}] arrived at the shell's title page (phase {ShellFlow.Phase}); " +
                          "ShellFlow.Reset() — not StartNewGame(), which would clobber the shared savegame.");
                ShellFlow.Reset();
            }
            InteractionGate.Reset();

            yield return _stage.SetNight(hour);
        }

        /// <summary>Her: the region's own on-foot player with a skin — found, never built.</summary>
        IEnumerator FindHer()
        {
            for (int f = 0; f < 240 && _player == null; f++)
            {
                foreach (PlayerWalkController p in Object.FindObjectsByType<PlayerWalkController>(FindObjectsSortMode.None))
                {
                    var iso = p.GetComponent<IsoCharacterSprite>();
                    if (iso == null || iso.Visual == null || iso.Visual.Skin == null) continue;
                    _player = p;
                    _iso = iso;
                    break;
                }
                if (_player == null) yield return null;
            }
            Assert.IsNotNull(_player, $"[{PlateDir}] {_scene} has no on-foot player with a character skin after " +
                                      "240 frames, so there is no skinned player to draw ashore.");
            Assert.IsNull(_player.GetComponentInParent<IsoFacetHullRenderer>(),
                          $"[{PlateDir}] the player is ABOARD a hull in {_scene}; these plates are of her ashore.");
            _sprite = _player.GetComponent<SpriteRenderer>();
            _body = _player.GetComponent<Rigidbody2D>();
            _skin = _iso.Visual.Skin;
        }

        /// <summary>
        /// Her, stood at <paramref name="at"/> facing the camera, her plate figure built beside her sprite
        /// (hidden) unless <paramref name="plateFigure"/> is false — the switch frames, where the game's own
        /// presenter is her figure — then the game's camera parked on <paramref name="centre"/> and the
        /// world frozen.
        /// </summary>
        IEnumerator StandHer(Vector2 at, Vector2 centre, float worldWidth, string why, bool plateFigure = true)
        {
            yield return FindHer();

            _standAt = at;
            PutHerAt(at, withBody: true);
            if (plateFigure) BuildHerFigure();

            yield return _stage.FrameOn(centre);
            FitTheFrame(worldWidth, why);
            _plateOrtho = _stage.Camera.orthographicSize;

            Vector2 now = _player.transform.position;
            float drift = Vector2.Distance(now, at);
            Debug.Log($"[{PlateDir}] her stand: asked {Fmt(at)}, found {Fmt(now)} after the framing " +
                      $"(drift {drift:0.000} m){(drift > 0.001f ? " — re-stood (transform only; the world is frozen)" : "")}.");
            if (drift > 0.001f) PutHerAt(at, withBody: false);
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

        void BuildHerFigure()
        {
            var go = new GameObject("AshoreFigurePlate");
            go.layer = _player.gameObject.layer;
            go.transform.SetParent(_player.transform, false);
            _figure = go.AddComponent<IsoCharacterFigureRenderer>();
            try
            {
                _figure.Configure(_skin);
            }
            catch (System.Exception e)
            {
                Assert.Fail($"[{PlateDir}] her skin '{_skin.name}' cannot build a figure: {e.Message}");
            }

            _pose = CharacterSkinStateMap.Idle;
            if (!_figure.SetPose(_pose, 0))
            {
                string first = _skin.Clips != null && _skin.Clips.Length > 0 ? _skin.Clips[0].State : null;
                Assert.IsFalse(string.IsNullOrEmpty(first),
                               $"[{PlateDir}] her skin '{_skin.name}' has no '{CharacterSkinStateMap.Idle}' clip and no clip at all.");
                _pose = first;
                Assert.IsTrue(_figure.SetPose(_pose, 0), $"[{PlateDir}] her skin '{_skin.name}' will not pose '{_pose}' frame 0.");
            }
            _figure.Visible = false;
        }

        /// <summary>
        /// The mesh owner every hull frame uses: a presentable owner whose boat draws as a MESH hull and
        /// carries a skipper, the SHORTEST such hull (a tight frame, an open deck), ties by id.
        /// </summary>
        static BoatOwnerDef PickMeshOwner()
        {
#if UNITY_EDITOR
            BoatOwnerDef owner = AssetDatabase.FindAssets("t:BoatOwnerDef", new[] { OwnersFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(o => o != null && o.IsPresentable() && o.Boat.Visual.Variant == BoatHullVariant.Mesh &&
                            o.Boat.Visual.HasHullMesh() && o.Skipper != null && o.AboardCount() > 0)
                .OrderBy(o => o.Boat.LengthMeters).ThenBy(o => o.Id, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (owner == null)
                Assert.Fail($"[{PlateDir}] NO PLATE WRITTEN — no owner under {OwnersFolder} has a presentable MESH " +
                            "hull with a skipper aboard, so there is no moored mesh hull to stand her beside.");
            return owner;
#else
            Assert.Ignore("Needs the AssetDatabase: these plates are of the REAL committed owners.");
            return null;
#endif
        }

        float HalfBeamOf(BoatOwnerDef owner)
        {
            float beam = owner.Boat.Visual.HullMesh.WatertightHalfBeamMeters;
            _beamNote = beam > 0f
                ? $"half-beam {beam:0.00} m (HullMeshDef.WatertightHalfBeamMeters)"
                : $"half-beam FALLBACK {FallbackHalfBeamMetres:0.00} m — the hull mesh carries no watertight half-beam";
            return beam > 0f ? beam : FallbackHalfBeamMetres;
        }

        IEnumerator Moor(BoatOwnerDef owner, Vector2 at, float heading)
        {
            var go = new GameObject($"Moored_{owner.Id}");
            go.SetActive(false);
            _stage.Track(go);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var moored = go.AddComponent<MooredBoat>();
            moored.Configure(owner, heading);
            go.SetActive(true);

            Transform skipper = null;
            for (int f = 0; f < 60 && skipper == null; f++)
            {
                yield return null;
                if (moored.IsPresented)
                    skipper = moored.GetComponentsInChildren<Transform>(true)
                                    .FirstOrDefault(t => t.name == MooredBoat.SkipperChildName);
            }
            Assert.IsTrue(moored.IsPresented, $"[{PlateDir}] {owner.Id} was moored but never presented in 60 frames.");
            Assert.IsNotNull(skipper, $"[{PlateDir}] {owner.Id} presented with no '{MooredBoat.SkipperChildName}' aboard.");
            if (!CanComposite(go.transform, owner.Id))
                Assert.Fail($"[{PlateDir}] NO PLATE WRITTEN — {owner.Id}'s hull holds facet id 0.");

            _boat = moored;
            _skipper = skipper;
            _skipperSprite = skipper.GetComponent<SpriteRenderer>();
            Debug.Log($"[{PlateDir}] moored {owner.Id} ({owner.Boat.name}, {owner.Boat.LengthMeters:0.0} m, {_beamNote}) " +
                      $"at {Fmt(at)} heading {heading:0.0}; skipper at {Fmt(skipper.position)}, sprite " +
                      $"{(_skipperSprite != null ? "found" : "NONE — claim 3's fisher is unknown from this plate")}.");
        }

        /// <summary>
        /// St Peters is committed WITHOUT its pier lamps (the scene holds no <c>lanternPost</c>), so the
        /// builder's own sites are placed by the builder's own <see cref="LampPosts.Place"/> — named in
        /// every caption. A site that already has a lamp standing on it is left alone.
        /// </summary>
        IEnumerator EnsureThePierLamps()
        {
            SceneLight[] standing = Object.FindObjectsByType<SceneLight>(FindObjectsSortMode.None);
            IReadOnlyList<LampPosts.Site> sites = StPetersWharf.LampPostSites();
            List<LampPosts.Site> missing = sites
                .Where(s => !standing.Any(l => Vector2.Distance(l.transform.position, s.Position) <= LampSiteToleranceMetres))
                .ToList();
            if (missing.Count > 0)
            {
                var host = _stage.Track(new GameObject("AshorePlatePierLamps"));
                int placed = LampPosts.Place(host.transform, missing, null, 0f, $"[{PlateDir}]");
                _lampsNote = $"{sites.Count} builder pier sites (StPetersWharf.LampPostSites): {sites.Count - missing.Count} " +
                             $"already standing, {placed} of {missing.Count} PLACED by the plate via LampPosts.Place " +
                             $"at {string.Join(", ", missing.Select(s => Fmt(s.Position)))}";
            }
            else
            {
                _lampsNote = $"all {sites.Count} builder pier sites already standing in the scene";
            }
            for (int f = 0; f < 4; f++) yield return null;
            _lampsNote += $"; flicker zeroed on {QuietTheFlicker()} lamp(s)";
        }

        /// <summary>If no lamp reaches the moored hull, ONE plate lamp on the deck's south row.</summary>
        IEnumerator LightTheHull(IsoFacetHullRenderer hull)
        {
            Vector2 p = hull.transform.position;
            SceneLight reaching = LampReaching(p, needShadows: false);
            if (reaching != null)
            {
                _lampsNote += $"; the hull at {Fmt(p)} is within reach of '{reaching.name}' " +
                              $"({Vector2.Distance(reaching.WorldOrigin, p):0.0} of {Reach(reaching):0.0} m)";
                yield break;
            }

            Vector2 ladder = StPetersWharf.LadderPosition();
            LampPosts.Site site = LampPosts.OnDeck(LampPosts.DecorFamily, LampPosts.LanternPost,
                                                   new Vector2(ladder.x - 1.5f, StPetersWharf.MinCellY + 0.5f),
                                                   SouthDegrees, StPetersWharf.DeckFootprint(),
                                                   "plate lamp: lights the south-moored hull");
            var host = _stage.Track(new GameObject("AshorePlateHullLamp"));
            int placed = LampPosts.Place(host.transform, new[] { site }, null, 0f, $"[{PlateDir}]");
            for (int f = 0; f < 4; f++) yield return null;
            _lampsNote += $"; ONE plate lamp ({LampPosts.LanternPost}) placed ({placed}/1) at {Fmt(site.Position)} " +
                          $"heading 180 because no lamp reached the hull at {Fmt(p)}; flicker zeroed on {QuietTheFlicker()} lamp(s)";
        }

        IEnumerator MakeTheSeaLive()
        {
            if (DisplacedWaterRegistry.Count > 0)
            {
                _seaNote = $"live as the scene loaded it ({DisplacedWaterRegistry.Count} surface(s))";
                yield break;
            }
            DisplacedWaterSurface[] surfaces = Object
                .FindObjectsByType<DisplacedWaterSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(s => s.isActiveAndEnabled ? 0 : 1).ThenBy(s => s.name, System.StringComparer.Ordinal).ToArray();
            if (surfaces.Length == 0)
            {
                _seaNote = "NO DisplacedWaterSurface in the scene";
                yield break;
            }
            surfaces[0].SetDisplaced(true);
            _seaTurnedOn = surfaces[0];
            for (int f = 0; f < 3; f++) yield return null;
            _seaNote = $"switched ON by the plate: '{surfaces[0].name}'.SetDisplaced(true) -> registry count " +
                       $"{DisplacedWaterRegistry.Count} (off again in the teardown)";
        }

        static int QuietTheFlicker()
        {
            SceneLight[] lights = Object.FindObjectsByType<SceneLight>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (SceneLight l in lights) l.FlickerAmount = 0f;
            return lights.Length;
        }

        static float Reach(SceneLight l) => l.ReachMetres > 0f ? l.ReachMetres : l.Range;

        static SceneLight LampReaching(Vector2 p, bool needShadows) =>
            Object.FindObjectsByType<SceneLight>(FindObjectsSortMode.None)
                  .Where(l => l.isActiveAndEnabled && (!needShadows || l.CastsShadows) &&
                              Vector2.Distance(l.WorldOrigin, p) <= Reach(l))
                  .OrderBy(l => Vector2.Distance(l.WorldOrigin, p))
                  .FirstOrDefault();

        void NoteNeighbours(Vector2 at, float radius)
        {
            var rows = new List<(float d, string s)>();
            foreach (VehicleController v in Object.FindObjectsByType<VehicleController>(FindObjectsSortMode.None))
            {
                float d = Vector2.Distance(v.transform.position, at);
                if (d <= radius) rows.Add((d, $"vehicle '{v.name}' at {Fmt(v.transform.position)}, {d:0.0} m"));
            }
            foreach (MooredBoat b in Object.FindObjectsByType<MooredBoat>(FindObjectsSortMode.None))
            {
                float d = Vector2.Distance(b.transform.position, at);
                if (d <= radius) rows.Add((d, $"moored '{b.name}' at {Fmt(b.transform.position)}, {d:0.0} m"));
            }
            foreach (SceneLight l in Object.FindObjectsByType<SceneLight>(FindObjectsSortMode.None))
            {
                float d = Vector2.Distance(l.WorldOrigin, at);
                if (d <= radius)
                    rows.Add((d, $"lamp '{l.name}' origin {Fmt(l.WorldOrigin)}, reach {Reach(l):0.0} m, casts shadows " +
                                 $"{l.CastsShadows}, {d:0.0} m"));
            }
            foreach (var r in rows.OrderBy(r => r.d).ThenBy(r => r.s, System.StringComparer.Ordinal).Take(14))
                _neighbours.Add(r.s);
        }

        // =============================================================================================
        //  The switch: the config, her presenter, her lamp, the fence, the stands
        // =============================================================================================

        /// <summary>
        /// Frame 1's and S1's stand: the machine row at the store (UtilityQuad, Trike200, Enduro250), her
        /// a step off the quad's end. Every member is named beside the plate; a missing quad is NO PLATE.
        /// </summary>
        List<GameObject> TheMachineRow(out Vector2 at, out Bounds rowBounds, out bool quadAtWest)
        {
            var row = new List<GameObject>();
            GameObject quad = null;
            foreach (string name in new[] { StPetersMachines.QuadName, StPetersMachines.TrikeName,
                                            StPetersMachines.EnduroName })
            {
                GameObject go = GameObject.Find(name);
                if (go == null) { _neighbours.Add($"{name} NOT FOUND in {StPetersScene}"); continue; }
                row.Add(go);
                if (name == StPetersMachines.QuadName) quad = go;
            }
            if (quad == null)
                Assert.Fail($"[{PlateDir}] NO PLATE WRITTEN — {StPetersScene} carries no " +
                            $"'{StPetersMachines.QuadName}', so there is no machine row to stand her beside.");

            rowBounds = BoundsOf(row[0]);
            foreach (GameObject go in row.Skip(1)) rowBounds.Encapsulate(BoundsOf(go));
            quadAtWest = BoundsOf(quad).center.x <= rowBounds.center.x;
            at = new Vector2(quadAtWest ? rowBounds.min.x - SideStepMetres : rowBounds.max.x + SideStepMetres,
                             quad.transform.position.y);
            foreach (GameObject go in row)
                _neighbours.Add($"{go.name} at {Fmt(go.transform.position)}, bounds x {rowBounds.min.x:0.00}.." +
                                $"{rowBounds.max.x:0.00} (row), {Vector2.Distance(go.transform.position, at):0.0} m from her");
            return row;
        }

        /// <summary>Frame 2's and S5's stand: a step off the east end of Nine Mile Creek's dually.</summary>
        GameObject TheDually(out Bounds tb, out Vector2 at)
        {
            GameObject truck = GameObject.Find(NineMileCreekTruckPark.TruckName);
            if (truck == null)
                Assert.Fail($"[{PlateDir}] NO PLATE WRITTEN — {NineMileCreekScene} carries no " +
                            $"'{NineMileCreekTruckPark.TruckName}', so there is no truck to stand her beside.");

            tb = BoundsOf(truck);
            at = new Vector2(tb.max.x + SideStepMetres, truck.transform.position.y);
            return truck;
        }

        /// <summary>
        /// ⭐ <b>The owner's switch, thrown at runtime.</b> OFF is the config the region loaded (the shipped
        /// asset); ON is a runtime COPY of it with <see cref="GameConfig.MeshCharacterAshore"/> set,
        /// published through <see cref="GameServices.Config"/>, which the presenter reads on every apply.
        /// The asset is never written. A loaded config that is not main's (the mesh off, or the switch
        /// already on) is NO PLATE: the OFF frame would not be main's frame.
        /// </summary>
        void SetTheSwitch(bool ashoreOn)
        {
            if (_switchOn == null)
            {
                _shippedConfig = GameServices.Config;
                if (_shippedConfig == null)
                    Assert.Fail($"[{PlateDir}] NO PLATE WRITTEN — {_scene} published no GameServices.Config, so " +
                                "there is no shipped switch to read OFF.");
                if (!_shippedConfig.MeshCharacter || _shippedConfig.MeshCharacterAshore)
                    Assert.Fail($"[{PlateDir}] NO PLATE WRITTEN — the loaded config '{_shippedConfig.name}' is not " +
                                $"main's: MeshCharacter {_shippedConfig.MeshCharacter}, MeshCharacterAshore " +
                                $"{_shippedConfig.MeshCharacterAshore} (main ships true / false).");
#if UNITY_EDITOR
                GameConfig asset = AssetDatabase.LoadAssetAtPath<GameConfig>(ShippedConfigPath);
                _configNote = asset == null
                    ? $"OFF = the loaded config '{_shippedConfig.name}' ({ShippedConfigPath} did not load to compare)"
                    : ReferenceEquals(asset, _shippedConfig)
                        ? $"OFF = the shipped asset {ShippedConfigPath} itself (MeshCharacterAshore {asset.MeshCharacterAshore})"
                        : $"OFF = the loaded config '{_shippedConfig.name}', NOT the asset object at {ShippedConfigPath} " +
                          $"(the asset reads MeshCharacterAshore {asset.MeshCharacterAshore})";
#else
                _configNote = $"OFF = the loaded config '{_shippedConfig.name}'";
#endif
                _switchOn = Object.Instantiate(_shippedConfig);
                _switchOn.name = "(plate: MeshCharacterAshore ON)";
                _switchOn.MeshCharacterAshore = true;
            }
            GameServices.Config = ashoreOn ? _switchOn : _shippedConfig;
        }

        /// <summary>
        /// Her rider and the presenter the game gave it. The presenter is installed by the rider only
        /// while <see cref="GameConfig.MeshCharacter"/> is on; its body is her sprite, so the teardown's
        /// restore of <see cref="_sprite"/> covers whatever the presenter left.
        /// </summary>
        IEnumerator FindHerPresenter()
        {
            _rider = _player.GetComponentInChildren<DeckRiderVisual>(true);
            if (_rider == null)
                Assert.Fail($"[{PlateDir}] NO PLATE WRITTEN — the player in {_scene} carries no DeckRiderVisual, so " +
                            "no presenter decides what draws her.");
            for (int f = 0; f < PresenterWaitFrames && _presenter == null; f++)
            {
                _presenter = _rider.GetComponent<DeckRiderMeshPresenter>();
                if (_presenter == null) yield return null;
            }
            if (_presenter == null)
                Assert.Fail($"[{PlateDir}] NO PLATE WRITTEN — no DeckRiderMeshPresenter on her rider after " +
                            $"{PresenterWaitFrames} frames (GameConfig.MeshCharacter " +
                            $"{(GameServices.Config != null && GameServices.Config.MeshCharacter)}, skin " +
                            $"'{(_skin != null ? _skin.name : "none")}').");

            SpriteRenderer body = _rider.BodyRenderer;
            _switchBodyNote = body == null ? "the rider names NO body renderer"
                            : body == _player.GetComponent<SpriteRenderer>()
                                ? $"the presenter's body is her own SpriteRenderer '{body.name}'"
                                : $"the presenter's body is '{body.name}', NOT her root SpriteRenderer";
            if (body != null) _sprite = body;
        }

        Headlamp FindHerHeadlamp()
        {
            Headlamp lamp = _player.GetComponentInChildren<Headlamp>(true);
            if (lamp != null) return lamp;
            Transform her = GameServices.PlayerTransform;
            return her != null ? her.GetComponentInChildren<Headlamp>(true) : null;
        }

        /// <summary>
        /// ⭐ <b>HER OWN headlamp, on</b> — the one <see cref="WalkerLights"/> hangs under her; no pier lamp is
        /// placed. If the runtime host is absent (an earlier fixture may have reset it), the plate hosts one
        /// and names it. A lamp that will not light is written down and fails the frame by name after its
        /// plates, so the picture is kept either way.
        /// </summary>
        IEnumerator TurnOnHerHeadlamp()
        {
            string source = "the runtime WalkerLights host";
            Headlamp lamp = FindHerHeadlamp();
            for (int f = 0; f < LampWaitFrames && lamp == null; f++)
            {
                yield return null;
                lamp = FindHerHeadlamp();
            }
            if (lamp == null)
            {
                _plateWalkerLights = new GameObject("AshorePlateWalkerLights").AddComponent<WalkerLights>();
                source = $"a WalkerLights hosted by the plate ('{_plateWalkerLights.name}': no runtime host hung one " +
                         $"on her in {LampWaitFrames} frames)";
                for (int f = 0; f < LampWaitFrames && lamp == null; f++)
                {
                    yield return null;
                    lamp = FindHerHeadlamp();
                }
            }
            if (lamp == null)
                Assert.Fail($"[{PlateDir}] NO PLATE WRITTEN — no Headlamp under her after {2 * LampWaitFrames} frames, " +
                            "even with a plate-hosted WalkerLights: there is no headlamp to stand her under.");

            lamp.SetOn(true);
            _headlampTurnedOn = lamp;
            yield return null;
            yield return null;

            bool lit = lamp.IsOn && lamp.Light != null && lamp.Light.enabled;
            if (!lit)
                _lampUnlitWhy = $"her headlamp is ON but not LIVE: WalkerLights.WalksHerOwnBeam " +
                                $"{WalkerLights.WalksHerOwnBeam} (InteractActorProbe.Has {InteractActorProbe.Has}" +
                                (InteractActorProbe.Has ? $", context {InteractActorProbe.Current.Context}" : "") +
                                $"), SceneLight {(lamp.Light == null ? "missing" : lamp.Light.enabled ? "enabled" : "disabled")}";

            Transform lanternT = lamp.transform.parent != null ? lamp.transform.parent.Find("WalkerLantern") : null;
            SceneLight lantern = lanternT != null ? lanternT.GetComponent<SceneLight>() : null;
            string lanternNote = lantern == null ? "not found" : lantern.enabled ? "enabled" : "off";
            _lampsNote = $"HER headlamp '{lamp.name}' under '{(lamp.transform.parent != null ? lamp.transform.parent.name : "?")}' " +
                         $"from {source}: {(lit ? "LIT" : "NOT LIT")}, beam axis {Fmt(lamp.transform.up)}, lift " +
                         $"{WalkerLights.HeadlampLiftMetres:0.00} m; her lantern {lanternNote}; NO pier lamp placed; " +
                         $"flicker zeroed on {QuietTheFlicker()} lamp(s)";
        }

        static bool IsFencePiece(string name) =>
            FencePieceKeys.Any(k => name.StartsWith(k + "_", System.StringComparison.Ordinal));

        /// <summary>The nearest enabled horizontal rail of one fence style, ties by name.</summary>
        static SpriteRenderer PickTheRail(GameObject yards, string key, Vector2 near) =>
            yards.GetComponentsInChildren<SpriteRenderer>()
                 .Where(r => r.enabled && r.name.StartsWith(key + "_", System.StringComparison.Ordinal) &&
                             r.bounds.size.x >= r.bounds.size.y)
                 .OrderBy(r => Vector2.Distance(r.transform.position, near))
                 .ThenBy(r => r.name, System.StringComparer.Ordinal)
                 .FirstOrDefault();

        /// <summary>Two engine frames: the rider's LateUpdate applies, the lamps publish.</summary>
        static IEnumerator Settle()
        {
            yield return null;
            yield return null;
        }

        /// <summary>Her box on the plate: her feet ± <see cref="HerBoxHalfWidthMetres"/>, grown by <paramref name="grow"/> m.</summary>
        RectInt HerBox(float grow)
        {
            Vector2 feet = _player.transform.position;
            return ScreenRect(feet + new Vector2(-HerBoxHalfWidthMetres - grow, -HerBoxBelowMetres - grow),
                              feet + new Vector2(HerBoxHalfWidthMetres + grow, HerBoxHeightMetres + grow), 1);
        }

        static bool Shown(Renderer r) => r != null && r.gameObject.activeInHierarchy && r.enabled && !r.forceRenderingOff;

        static bool HasChildNamed(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).Any(t => t != root && t.name == name);

        /// <summary>
        /// Everything that could be drawing HER right now: her sprite body, the rider's aboard sprite, and
        /// every visible figure under her (the presenter's aboard and ashore figures included).
        /// </summary>
        int Drawers(out string which)
        {
            var seen = new HashSet<Object>();
            var names = new List<string>();
            SpriteRenderer body = _rider.BodyRenderer;
            if (Shown(body) && seen.Add(body)) names.Add($"sprite body '{body.name}'");
            SpriteRenderer riderSprite = _rider.RiderTransform != null ? _rider.RiderTransform.GetComponent<SpriteRenderer>() : null;
            if (Shown(riderSprite) && seen.Add(riderSprite)) names.Add($"rider sprite '{riderSprite.name}'");
            var figures = new List<IsoCharacterFigureRenderer>(_player.GetComponentsInChildren<IsoCharacterFigureRenderer>(true));
            if (_presenter.Figure != null) figures.Add(_presenter.Figure);
            if (_presenter.AshoreFigure != null) figures.Add(_presenter.AshoreFigure);
            foreach (IsoCharacterFigureRenderer f in figures)
                if (f != null && f.isActiveAndEnabled && f.Visible && seen.Add(f))
                    names.Add($"figure '{f.name}' (id {f.FigureId}, {(f.IsAshore ? "ashore" : "aboard")})");
            which = names.Count == 0 ? "nothing" : string.Join(", ", names);
            return names.Count;
        }

        string SwitchState(string when)
        {
            SpriteRenderer body = _rider.BodyRenderer;
            IsoCharacterFigureRenderer fig = _presenter.AshoreFigure;
            int drawers = Drawers(out string which);
            GameConfig cfg = GameServices.Config;
            string sw = cfg == null ? "no config" : cfg == _switchOn ? "ON (the runtime copy)"
                      : cfg == _shippedConfig ? "OFF (the shipped config)" : $"'{cfg.name}'";
            return $"{when}: switch {sw}; DrawsAshore {_presenter.DrawsAshore}, AshoreRefused {_presenter.AshoreRefused}; " +
                   $"body {(body == null ? "none" : $"enabled {body.enabled}, forceRenderingOff {body.forceRenderingOff}")}; " +
                   $"ashore figure {(fig == null ? "none" : $"id {fig.FigureId}, visible {fig.Visible}")}, child " +
                   $"'{AshoreFigureName}' {(HasChildNamed(_player.transform, AshoreFigureName) ? "present" : "absent")}; " +
                   $"drawers {drawers} ({which}); registry {IsoFacetHullRegistry.Count} hull(s), " +
                   $"{IsoFacetHullRegistry.FigureCount} figure id(s); reason '{_presenter.NotDrawingReason ?? "none"}'";
        }

        // =============================================================================================
        //  Naming the frame, and the guards every plate passes
        // =============================================================================================

        /// <summary>
        /// Raise the zoom only far enough to hold <paramref name="worldWidth"/> metres, and never lower
        /// it: a plate must not be zoomed in past the look the scene itself is authored at.
        /// </summary>
        void FitTheFrame(float worldWidth, string why)
        {
            Camera cam = _stage.Camera;
            float was = cam.orthographicSize;
            float need = 0.5f * worldWidth / Mathf.Max(0.01f, cam.aspect);
            if (need <= was)
            {
                Debug.Log($"[{PlateDir}] zoom left as the scene authored it: ortho {was:0.00} " +
                          $"({2f * was * cam.aspect:0.0} m across) already holds {worldWidth:0.0} m — {why}.");
                return;
            }

            cam.orthographicSize = need;
            Debug.Log($"[{PlateDir}] zoom WIDENED for this plate: ortho {was:0.00} -> {need:0.00} " +
                      $"({2f * need * cam.aspect:0.0} m across) — {why}.");
        }

        /// <summary>⭐ <b>The frame this plate was shot through</b> — a number without its frame is not evidence.</summary>
        string FrameNote(Arm arm, string what)
        {
            Camera cam = _stage.Camera;
            bool lampsOn = WharfNightStage.LampsAreGatedOn(Shader.GetGlobalColor("_DayNightTint"));
            string clock = GameServices.Clock != null ? GameServices.Clock.HourOfDay.ToString("00.00") : "<no clock>";
            var sb = new StringBuilder();
            sb.AppendLine(what);
            sb.AppendLine($"frame        {arm.Frame}");
            sb.AppendLine($"scene        {_scene}, hour asked {_hour:00.0}, clock {clock}, lamps gated {(lampsOn ? "ON" : "off")}, " +
                          $"shell phase {ShellFlow.Phase}");
            sb.AppendLine($"her          at {Fmt(_player.transform.position)} (asked {Fmt(_standAt)}), heading held " +
                          $"{SouthDegrees:0} (compass; facing the camera), sprite heading {_iso.HeadingDegrees:0.0}");
            sb.AppendLine($"figure       id {_fid}, ashore yaw {_yaw:0.0} (skin azimuth ccw {_skin.AzimuthCounterClockwise}), " +
                          $"elevation {_skin.ElevationDeg:0.0}, " +
                          (_switchNote == null ? $"pose '{_pose}' frame 0" : $"pose {_pose} (posed by the presenter)") +
                          $", skin '{_skin.name}'");
            sb.AppendLine($"camera       {cam.transform.position} ortho {cam.orthographicSize:0.00} " +
                          $"(≈{2f * cam.orthographicSize * cam.aspect:0.0} × {2f * cam.orthographicSize:0.0} m), " +
                          $"aspect {cam.aspect:0.000}");
            sb.AppendLine($"plate        {_stage.Width} × {_stage.Height} px, timeScale {Time.timeScale:0.0}");
            sb.AppendLine($"sea          DisplacedWaterRegistry.Count {DisplacedWaterRegistry.Count} ({_seaNote}); " +
                          $"IsoFacetHullFeature.InteriorMaskEnabled {IsoFacetHullFeature.InteriorMaskEnabled}");
            sb.AppendLine(_switchNote == null
                ? "sprite       her sprite BODY hidden with SpriteRenderer.forceRenderingOff; enabled stays TRUE so " +
                  "SpriteShadow keeps casting her sprite-sheet silhouette (Q3)"
                : "sprite       her body is hidden by the PRESENTER alone (forceRenderingOff, only while it draws her " +
                  "mesh); enabled stays TRUE (Q3); this class never touches it");
            sb.AppendLine($"lamps        {_lampsNote}");
            if (_switchNote != null) sb.AppendLine($"switch       {_switchNote}");
            if (_boat != null) sb.AppendLine($"hull         {_boat.name} at {Fmt(_boat.transform.position)}, {_beamNote}");
            foreach (string n in _neighbours) sb.AppendLine($"beside       {n}");
            return sb.ToString();
        }

        void SavePlate(Arm arm, string suffix, byte[] frame, string what)
        {
            string file = $"{arm.Key}-{suffix}.png";
            _stage.SavePlate(file, frame);
            string note = FrameNote(arm, what);
            File.WriteAllText(Path.Combine(Application.temporaryCachePath, PlateDir, Path.ChangeExtension(file, ".txt")),
                              note, new UTF8Encoding(false));
            Debug.Log($"[{PlateDir}] SHOT THROUGH — {file}\n{note}");
            _platesWritten++;
        }

        /// <summary>
        /// ⭐⭐ <b>THE PLATE MUST CONTAIN ITS SUBJECT</b>: its point inside the frame AND something of it
        /// rasterised. Copied from <c>SloopsOnTheDevKeyPlatePlayTests</c>.
        /// </summary>
        void AssertTheSubjectIsInFrame(Transform subject, string what)
        {
            Camera cam = _stage.Camera;
            Vector3 vp = cam.WorldToViewportPoint(subject.position);
            var renderers = subject.GetComponentsInChildren<Renderer>(true);
            int visible = renderers.Count(r => r != null && r.enabled && r.isVisible);

            string diagnosis =
                $"[{PlateDir}] {what}: subject at world {subject.position}, viewport " +
                $"({vp.x:0.000}, {vp.y:0.000}, z {vp.z:0.00}); camera at {cam.transform.position} " +
                $"ortho {cam.orthographicSize:0.00} aspect {cam.aspect:0.000}; " +
                $"{visible} of {renderers.Length} renderers visible";
            Debug.Log(diagnosis);

            Assert.Greater(vp.z, 0f, $"the subject is BEHIND the camera. {diagnosis}");
            Assert.That(vp.x, Is.InRange(0.02f, 0.98f), $"the subject is off the side of the frame. {diagnosis}");
            Assert.That(vp.y, Is.InRange(0.02f, 0.98f), $"the subject is off the top or bottom of the frame. {diagnosis}");
            Assert.Greater(renderers.Length, 0, $"the subject carries no renderer at all. {diagnosis}");
            Assert.Greater(visible, 0, $"nothing of the subject rasterised into the frame. {diagnosis}");
        }

        /// <summary>⚠️ A mesh hull holding facet id 0 is not in the picture: the frame is discarded, named.</summary>
        static bool CanComposite(Transform subject, string what)
        {
            if (subject == null) return false;
            var facet = subject.GetComponentInChildren<IsoFacetHullRenderer>(true);
            if (facet == null) return true;
            if (facet.HullId > 0) return true;

            Debug.LogWarning(
                $"[{PlateDir}] NO PLATE WRITTEN for {what}: this hull holds facet id 0, so the compositor has no " +
                "place for it. Re-shoot this fixture in a FRESH editor, on its own filter.");
            return false;
        }

        // =============================================================================================
        //  Pixel masks (plate pixels, bottom-left origin, one bool per pixel)
        // =============================================================================================

        static int Sum(byte[] f, int p) => f[p * 4] + f[p * 4 + 1] + f[p * 4 + 2];

        static bool[] Changed(byte[] a, byte[] b)
        {
            var m = new bool[a.Length / 4];
            for (int p = 0; p < m.Length; p++)
            {
                int i = p * 4;
                m[p] = Mathf.Abs(a[i] - b[i]) + Mathf.Abs(a[i + 1] - b[i + 1]) + Mathf.Abs(a[i + 2] - b[i + 2]) > DiffThreshold;
            }
            return m;
        }

        /// <summary><paramref name="to"/> is darker than <paramref name="from"/>.</summary>
        static bool[] Darker(byte[] from, byte[] to)
        {
            var m = new bool[from.Length / 4];
            for (int p = 0; p < m.Length; p++) m[p] = Sum(from, p) - Sum(to, p) > DiffThreshold;
            return m;
        }

        /// <summary><paramref name="to"/> is lighter than <paramref name="from"/>.</summary>
        static bool[] Lighter(byte[] from, byte[] to)
        {
            var m = new bool[from.Length / 4];
            for (int p = 0; p < m.Length; p++) m[p] = Sum(to, p) - Sum(from, p) > DiffThreshold;
            return m;
        }

        static bool[] Where(byte[] ids, int id)
        {
            var m = new bool[ids.Length];
            for (int p = 0; p < m.Length; p++) m[p] = ids[p] == id;
            return m;
        }

        static bool[] Above(byte[] channel, int threshold)
        {
            var m = new bool[channel.Length];
            for (int p = 0; p < m.Length; p++) m[p] = channel[p] > threshold;
            return m;
        }

        static bool[] And(bool[] a, bool[] b)
        {
            var m = new bool[a.Length];
            for (int p = 0; p < m.Length; p++) m[p] = a[p] && b[p];
            return m;
        }

        static bool[] Or(bool[] a, bool[] b)
        {
            var m = new bool[a.Length];
            for (int p = 0; p < m.Length; p++) m[p] = a[p] || b[p];
            return m;
        }

        static bool[] Not(bool[] a)
        {
            var m = new bool[a.Length];
            for (int p = 0; p < m.Length; p++) m[p] = !a[p];
            return m;
        }

        static int Count(bool[] m)
        {
            int n = 0;
            for (int p = 0; p < m.Length; p++) if (m[p]) n++;
            return n;
        }

        /// <summary>Square dilation by <paramref name="r"/> px, separable, by running sums.</summary>
        static bool[] Dilate(bool[] m, int w, int h, int r)
        {
            if (r <= 0) return (bool[])m.Clone();
            var rows = new bool[m.Length];
            var cum = new int[Mathf.Max(w, h) + 1];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++) cum[x + 1] = cum[x] + (m[y * w + x] ? 1 : 0);
                for (int x = 0; x < w; x++)
                    rows[y * w + x] = cum[Mathf.Min(w, x + r + 1)] - cum[Mathf.Max(0, x - r)] > 0;
            }
            var result = new bool[m.Length];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++) cum[y + 1] = cum[y] + (rows[y * w + x] ? 1 : 0);
                for (int y = 0; y < h; y++)
                    result[y * w + x] = cum[Mathf.Min(h, y + r + 1)] - cum[Mathf.Max(0, y - r)] > 0;
            }
            return result;
        }

        static bool[] Erode(bool[] m, int w, int h, int r) => Not(Dilate(Not(m), w, h, r));

        RectInt ScreenRect(Vector2 worldMin, Vector2 worldMax, int grow)
        {
            Camera cam = _stage.Camera;
            Vector3 a = cam.WorldToScreenPoint(new Vector3(worldMin.x, worldMin.y, 0f));
            Vector3 b = cam.WorldToScreenPoint(new Vector3(worldMax.x, worldMax.y, 0f));
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x)) - grow, y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y)) - grow;
            int x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x)) + grow, y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y)) + grow;
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        static bool[] RectMask(RectInt r, int w, int h)
        {
            var m = new bool[w * h];
            for (int y = Mathf.Max(0, r.yMin); y < Mathf.Min(h, r.yMax); y++)
                for (int x = Mathf.Max(0, r.xMin); x < Mathf.Min(w, r.xMax); x++)
                    m[y * w + x] = true;
            return m;
        }

        static byte[] Tint(byte[] frame, bool[] mask)
        {
            var o = (byte[])frame.Clone();
            for (int p = 0; p < mask.Length; p++)
            {
                if (!mask[p]) continue;
                o[p * 4] = (byte)((o[p * 4] + 255) / 2);
                o[p * 4 + 1] = (byte)(o[p * 4 + 1] / 2);
                o[p * 4 + 2] = (byte)((o[p * 4 + 2] + 255) / 2);
            }
            return o;
        }

        static byte[] Grey(byte[] channel)
        {
            var o = new byte[channel.Length * 4];
            for (int p = 0; p < channel.Length; p++)
            {
                o[p * 4] = o[p * 4 + 1] = o[p * 4 + 2] = channel[p];
                o[p * 4 + 3] = 255;
            }
            return o;
        }

        static Bounds BoundsOf(GameObject go)
        {
            Collider2D[] cols = go.GetComponentsInChildren<Collider2D>().Where(c => c.enabled).ToArray();
            if (cols.Length > 0)
            {
                Bounds b = cols[0].bounds;
                foreach (Collider2D c in cols.Skip(1)) b.Encapsulate(c.bounds);
                return b;
            }
            Renderer[] rs = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                foreach (Renderer r in rs.Skip(1)) b.Encapsulate(r.bounds);
                return b;
            }
            return new Bounds(go.transform.position, Vector3.zero);
        }

        static string Fmt(Vector2 v) => $"({v.x:0.00}, {v.y:0.00})";

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
