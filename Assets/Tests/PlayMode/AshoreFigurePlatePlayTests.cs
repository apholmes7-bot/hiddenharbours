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
    /// <para><b>The class calls <see cref="IsoCharacterFigureRenderer.EnterAshore"/> itself</b>, on the
    /// skinned player, inside the test, and <see cref="IsoCharacterFigureRenderer.LeaveAshore"/> in the
    /// teardown. Nothing on main calls it; no production file is touched.</para>
    ///
    /// <para><b>⚠️ These SKIP on CI and prove nothing there</b> — a plate needs a GPU and CI runs the
    /// Null device. <b>⚠️ The plate directory is shared by every worktree</b>: a floor file is written
    /// first and only plates newer than it belong to this run. No scene is edited or saved; St Peters
    /// is the START scene and is left through <see cref="ShellFlow.Reset"/>, never
    /// <c>StartNewGame()</c>, which would overwrite the savegame every worktree shares.</para>
    ///
    /// <para><b>Every plate names its frame</b> in a caption beside it: scene, clock, her position, the
    /// camera's position and size, the plate's pixels, the sea and the lamps.</para>
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

            _figure = null; _sprite = null; _iso = null; _body = null; _bodyTaken = false; _player = null;
            _skin = null; _fid = 0; _seaTurnedOn = null; _boat = null; _skipper = null; _skipperSprite = null;

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

            Bounds rowBounds = BoundsOf(row[0]);
            foreach (GameObject go in row.Skip(1)) rowBounds.Encapsulate(BoundsOf(go));
            bool quadAtWest = BoundsOf(quad).center.x <= rowBounds.center.x;
            var at = new Vector2(quadAtWest ? rowBounds.min.x - SideStepMetres : rowBounds.max.x + SideStepMetres,
                                 quad.transform.position.y);
            foreach (GameObject go in row)
                _neighbours.Add($"{go.name} at {Fmt(go.transform.position)}, bounds x {rowBounds.min.x:0.00}.." +
                                $"{rowBounds.max.x:0.00} (row), {Vector2.Distance(go.transform.position, at):0.0} m from her");

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

            GameObject truck = GameObject.Find(NineMileCreekTruckPark.TruckName);
            if (truck == null)
                Assert.Fail($"[{PlateDir}] NO PLATE WRITTEN — {NineMileCreekScene} carries no " +
                            $"'{NineMileCreekTruckPark.TruckName}', so there is no truck to stand her beside.");

            Bounds tb = BoundsOf(truck);
            var at = new Vector2(tb.max.x + SideStepMetres, truck.transform.position.y);
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

        /// <summary>
        /// Her: the region's own on-foot player with a skin, stood at <paramref name="at"/> facing the
        /// camera, her mesh figure built beside her sprite (hidden), then the game's camera parked on
        /// <paramref name="centre"/> and the world frozen.
        /// </summary>
        IEnumerator StandHer(Vector2 at, Vector2 centre, float worldWidth, string why)
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

            _standAt = at;
            PutHerAt(at, withBody: true);
            BuildHerFigure();

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
                          $"elevation {_skin.ElevationDeg:0.0}, pose '{_pose}' frame 0, skin '{_skin.name}'");
            sb.AppendLine($"camera       {cam.transform.position} ortho {cam.orthographicSize:0.00} " +
                          $"(≈{2f * cam.orthographicSize * cam.aspect:0.0} × {2f * cam.orthographicSize:0.0} m), " +
                          $"aspect {cam.aspect:0.000}");
            sb.AppendLine($"plate        {_stage.Width} × {_stage.Height} px, timeScale {Time.timeScale:0.0}");
            sb.AppendLine($"sea          DisplacedWaterRegistry.Count {DisplacedWaterRegistry.Count} ({_seaNote}); " +
                          $"IsoFacetHullFeature.InteriorMaskEnabled {IsoFacetHullFeature.InteriorMaskEnabled}");
            sb.AppendLine("sprite       her sprite BODY hidden with SpriteRenderer.forceRenderingOff; enabled stays TRUE so " +
                          "SpriteShadow keeps casting her sprite-sheet silhouette (Q3)");
            sb.AppendLine($"lamps        {_lampsNote}");
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
