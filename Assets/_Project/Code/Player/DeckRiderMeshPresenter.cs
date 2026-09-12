using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The seam a mesh figure takes the draw through.</b> <see cref="DeckRiderVisual"/> knows that
    /// something ELSE may be drawing the player this frame; it does not know what, and it must not —
    /// the sprite path is the one that has to stay byte-identical, so it learns exactly one fact
    /// (<see cref="DrawsInsteadOfSprite"/>) and is handed exactly one call
    /// (<see cref="PoseForRider"/>) at the point where its own inputs are already published.
    ///
    /// <para>With no figure installed every expression that mentions this interface collapses to the
    /// code that was there before it existed. That is the toggle-0 contract, and it is the reason the
    /// interface is this small.</para>
    /// </summary>
    public interface IDeckRiderFigure
    {
        /// <summary>True on the frames this figure is really on screen, so the sprite must not be.
        /// False the moment anything is missing — a skin, a hull, a clip — because two figures is a
        /// worse failure than the old one.</summary>
        bool DrawsInsteadOfSprite { get; }

        /// <summary>
        /// Pose from the rider's already-decided inputs. Called ONCE per frame from
        /// <see cref="DeckRiderVisual"/>'s own <c>LateUpdate</c>, BEFORE it decides whether to enable
        /// the sprite — never from the figure's own <c>Update</c>, because the answer depends on
        /// values <c>DeckRiderVisual</c> publishes and a second opinion about execution order is
        /// exactly the kind of second authority this lane was told not to invent.
        /// </summary>
        void PoseForRider(DeckRiderVisual rider, bool aboard);
    }

    /// <summary>
    /// <b>The player, drawn as one skinned mesh through the iso facet pass, while she is ABOARD</b>
    /// (ADR 0044 option d, behind <see cref="GameConfig.MeshCharacter"/>).
    ///
    /// <para><b>What this component is.</b> A reader. It computes nothing about the player: stance,
    /// facing, gait, the deck point under her boots and her bearing on that deck are all decided by
    /// <see cref="DeckRiderVisual"/> and consumed here at the same seam the sprite consumes them at.
    /// Its whole job is (state, gait) → clip key → frame → one posed mesh, parented under the hull's
    /// posed mesh child at the deck point the occupant slot is already fed from.</para>
    ///
    /// <para><b>⚠ ABOARD ONLY, and the fence is mechanical, not a promise.</b> The facet block is only
    /// recorded when a facet hull is registered, so ashore there is no pass to draw her through at
    /// all. This component does not fake one: it asks the rider for the hull she is standing on and
    /// gives up if that hull is not an <see cref="IsoFacetHullRenderer"/> — which is also, for free,
    /// the right answer aboard a SPRITE hull, where the same pass does not exist either. It never
    /// touches <c>IsoFacetHullFeature</c> or <c>IsoFacetHullRegistry</c>; those are the water lane's
    /// files.</para>
    ///
    /// <para><b>Occlusion is per pixel and costs nothing.</b> The figure wears the hull's own
    /// <c>HHHullFacet</c> pass and shares her private depth buffer with <c>ZWrite On / ZTest
    /// LEqual</c>, exactly as a fitting does (<see cref="IsoFacetPropRenderer"/>). A gunwale in front
    /// of her boots is in front of her boots because it is nearer, not because anybody discarded
    /// anything. The 12-slot <c>DeckOccupants</c> publication is untouched and still fed by
    /// <see cref="DeckRiderVisual"/> alone — this PR adds no second occlusion path, it reads the
    /// stand point that publication is already built from
    /// (<see cref="DeckRiderVisual.DeckStandRigLocal"/>).</para>
    ///
    /// <para><b>⚠ What she will NOT look like yet.</b> Two debts are stated, not paid, and both are
    /// the next PR's:</para>
    /// <list type="bullet">
    ///   <item><b>The look.</b> The facet shader is the boat's, and against the inked art the figure
    ///   measures 43–57% off. That is the shader look pass, not this one.</item>
    ///   <item><b>The face.</b> The sprite's face is a raster stamp the mesh does not carry: she has
    ///   no eyes, brows or mouth. Nothing here invents them.</item>
    ///   <item><b>The counter-lean and the head look.</b> Rig 7 never exported the additive bone
    ///   table its brief promised (§2.5) — its only rock surface bakes the lean into frames and the
    ///   extractor passes no options, so every baked clip is the zero-rock pose. Deriving those
    ///   rotations in C# would be authoring look in code. The whole-body rock stays the live hull
    ///   transform it already is; the additive layer is owed upstream to <c>art-director</c>.</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DeckRiderVisual))]
    public sealed class DeckRiderMeshPresenter : MonoBehaviour, IDeckRiderFigure
    {
        [Tooltip("The baked CharacterSkinDef to draw. Left empty, the presenter asks the character's " +
                 "own CharacterVisualDef for its Skin — one art def, one answer, so the mesh and the " +
                 "sheets can never describe two different people.")]
        [SerializeField] private CharacterSkinDef _skin;

        // ---------------------------------------------------------------- sabotage (tests only)

        /// <summary>
        /// <b>Sabotage: freeze the mesh on frame 0.</b> Arms the determinism/stepping guard — with it
        /// set, "a later game time draws a later pose" must FAIL.
        ///
        /// <para>⚠ It feeds the MESH and nothing else. A knob that also held the sprite would leave
        /// both sides agreeing and the guard would pass while measuring nothing.</para>
        /// </summary>
        [System.NonSerialized] public bool SabotageHoldFrameZero;

        /// <summary>
        /// <b>Sabotage: shift the figure off the published stand point</b>, in rig metres. Arms the
        /// placement guard — with it set, "the figure stands where the occupant slot says she stands"
        /// must FAIL. Mesh-only for the same reason.
        /// </summary>
        [System.NonSerialized] public Vector3 SabotageStandOffsetMetres;

        // ---------------------------------------------------------------- state

        private DeckRiderVisual _rider;
        private IsoCharacterFigureRenderer _figure;
        private Transform _figureRoot;
        private IsoFacetHullRenderer _hull;
        private CharacterSkinDef _configured;
        private string _stateKey;
        private double _stateStartSeconds;

        // ---------------------------------------------------------------- published

        /// <inheritdoc/>
        public bool DrawsInsteadOfSprite { get; private set; }

        /// <summary>The skinned figure, once one exists. Null before she has ever drawn.</summary>
        public IsoCharacterFigureRenderer Figure => _figure;

        /// <summary>The facet hull she is riding, or null.</summary>
        public IsoFacetHullRenderer Hull => _hull;

        /// <summary>The def actually in use — the serialized one, or the character art def's.</summary>
        public CharacterSkinDef Skin => _configured;

        /// <summary>The clip key drawn this frame, or null.</summary>
        public string DrawnStateKey => _figure != null ? _figure.DrawnStateKey : null;

        /// <summary>The clip frame drawn this frame (after the poisoned-frame fence), or -1.</summary>
        public int DrawnFrame => _figure != null ? _figure.DrawnFrame : -1;

        /// <summary>The frame ASKED for before the fence, or -1 — the pair a test compares.</summary>
        public int RequestedFrame => _figure != null ? _figure.RequestedFrame : -1;

        /// <summary>True when the clip the state map ASKED the def for was not in it and a shorter one
        /// stood in — i.e. the BAKE came up short.
        ///
        /// <para>⚠ This does NOT flag the helm/oars divergence. Helm and Oars map to the GAIT key
        /// before the def is ever consulted (there is no <c>helm</c> clip to miss), so the def carried
        /// exactly what was asked for and nothing fell back — while the sprite path, whose FisherIso def
        /// DOES carry helm and oars sheets, draws a different pose entirely. That divergence is a
        /// stated debt of this PR, not something this flag reports.</para></summary>
        public bool FellBackToGait { get; private set; }

        /// <summary>Where the figure was placed, in the hull's posed-mesh local metres.</summary>
        public Vector3 FigureLocalMetres { get; private set; }

        /// <summary>The yaw applied about the rig's up axis, in degrees.</summary>
        public float FigureYawDegrees { get; private set; }

        /// <summary>
        /// <b>Why she is not drawing as a mesh</b>, in words, or null while she is. Published because
        /// every one of the reasons below is a silent, correct-looking no-op — the sprite simply keeps
        /// drawing — and a plate that came back as the sprite would otherwise not say which of eight
        /// gates shut.
        /// </summary>
        public string NotDrawingReason { get; private set; } = "not posed yet";

        /// <summary>Point the presenter at a skin explicitly. The test path, and the override for a
        /// character whose art def does not name one.</summary>
        public void Configure(CharacterSkinDef skin)
        {
            _skin = skin;
            Release();
        }

        // ---------------------------------------------------------------- lifecycle

        private void Awake()
        {
            _rider = GetComponent<DeckRiderVisual>();
        }

        private void OnEnable()
        {
            if (_rider == null) _rider = GetComponent<DeckRiderVisual>();
            // Register with the rider rather than have the rider find us: the rider owns the sprite
            // and must keep working with no figure at all, so the dependency only ever points this way.
            if (_rider != null) _rider.SetFigureOverride(this);
        }

        private void OnDisable()
        {
            if (_rider != null) _rider.SetFigureOverride(null);
            DrawsInsteadOfSprite = false;
            NotDrawingReason = "presenter disabled";
            Release();
        }

        private void OnDestroy() => Release();

        // ---------------------------------------------------------------- the one path

        /// <inheritdoc/>
        public void PoseForRider(DeckRiderVisual rider, bool aboard)
        {
            DrawsInsteadOfSprite = false;

            if (rider == null) { Stop("no rider"); return; }
            if (!aboard) { Stop("ashore — the facet pass is not recorded there"); return; }

            GameConfig config = GameServices.Config;
            if (config == null) { Stop("no GameConfig"); return; }
            if (!config.MeshCharacter) { Stop("GameConfig.MeshCharacter is off"); return; }

            CharacterSkinDef skin = ResolveSkin(rider);
            if (skin == null) { Stop("no CharacterSkinDef — none set here and none on the art def"); return; }
            if (!skin.IsUsable()) { Stop($"CharacterSkinDef '{skin.Id}' is not usable — re-bake it"); return; }

            IsoFacetHullRenderer hull = ResolveHull(rider);
            if (hull == null) { Stop("the hull under her is not a facet mesh hull"); return; }

            IsoCharacterSprite character = rider.Character;
            if (character == null) { Stop("no IsoCharacterSprite to read stance and gait from"); return; }

            // ⚠ Stance is read as REQUESTED, not as DRAWN. DrawnStance is whatever survived the SHEET
            // ladder, so it reports on the SPRITE's art coverage rather than on what she is doing: any
            // def whose stance sheet is short collapses it to Free, and the mesh would then never reach
            // its own baked `balance` clip however well the bake covered it. Reading the request keeps
            // both paths on ONE authority, one step earlier — DeckRiderVisual wrote it itself, this
            // frame, in StateContext.
            if (!CharacterSkinStateMap.Resolve(skin, character.Stance, character.Gait,
                                               out string stateKey, out bool fellBack))
            {
                Stop($"no clip for stance {character.Stance} / gait {character.Gait}");
                return;
            }
            FellBackToGait = fellBack;

            // ADR 0041: the switch is PER STATE. A state not listed still plays its sheet, which is
            // how the sheets retire one at a time at parity instead of all at once on a promise.
            if (!skin.DrawsAsMesh(stateKey))
            {
                Stop($"state '{stateKey}' is not in CharacterSkinDef.MeshStates (ADR 0041)");
                return;
            }

            if (!EnsureFigure(skin, hull)) { Stop("the figure could not be built"); return; }
            if (!skin.TryGetClip(stateKey, out CharacterSkinDef.SkinClip clip)) { Stop($"clip '{stateKey}' vanished"); return; }

            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0d;
            if (!string.Equals(stateKey, _stateKey, System.StringComparison.Ordinal))
            {
                _stateKey = stateKey;
                _stateStartSeconds = now;
            }

            // ⚠ RULE 5. A LOOPING clip runs from absolute game time, so the pose is a pure function of
            // (worldSeed, gameTime) and replays identically on a reload and on another machine; the
            // seed-derived phase still keeps two figures out of lockstep. A ONE-SHOT clip is a
            // TRANSITION and has a beginning — it runs from the moment its state was entered, and
            // holds its last frame rather than wrapping.
            double clipStart = clip.Loop ? 0d : _stateStartSeconds;
            int seed = GameServices.Environment != null ? GameServices.Environment.WorldSeed : 0;
            int frame = SabotageHoldFrameZero
                        ? 0
                        : CharacterSkinPose.FrameFor(clip, seed, now, clipStart);

            if (!_figure.SetPose(stateKey, frame)) { Stop($"could not pose '{stateKey}' frame {frame}"); return; }

            Place(rider, skin);
            _figure.Visible = true;
            DrawsInsteadOfSprite = true;
            NotDrawingReason = null;
        }

        /// <summary>
        /// Stand the figure down and say why. Hides rather than destroys: boarding and dismounting are
        /// frequent enough on a working deck that rebuilding a posed mesh, a material and two ramp
        /// textures each time would be a stutter the player can see (rule 7). The build is only thrown
        /// away when the hull under her actually changes.
        /// </summary>
        private void Stop(string reason)
        {
            NotDrawingReason = reason;
            if (_hull == null) { Release(); return; }   // Unity-null: the hull was destroyed under her
            if (_figure != null) _figure.Visible = false;
        }

        // ---------------------------------------------------------------- resolution

        /// <summary>
        /// The skin: the one set here, else the one named by the character's own
        /// <see cref="CharacterVisualDef"/>. Going through the art def rather than a second serialized
        /// reference means the sheets and the mesh cannot describe two different people, and it is the
        /// same place every other fact about how this character looks already lives (ADR 0003).
        /// </summary>
        private CharacterSkinDef ResolveSkin(DeckRiderVisual rider)
        {
            if (_skin != null) return _skin;
            IsoCharacterSprite character = rider.Character;
            CharacterVisualDef visual = character != null ? character.Visual : null;
            return visual != null ? visual.Skin : null;
        }

        /// <summary>
        /// The facet hull she is standing on — through the rider's OWN hull resolution
        /// (<see cref="DeckRiderVisual.LiveHullPresenter"/>), never a second search of the scene. The
        /// presenter seam answers with the visual transform the skinner installed the renderer on, so
        /// a boat re-skinned under her feet answers with the new one on the very next frame.
        ///
        /// <para>A sprite hull answers null here, and that is correct rather than unfortunate: a
        /// sprite hull records no facet block, so there is no pass to draw a mesh figure through.</para>
        /// </summary>
        private IsoFacetHullRenderer ResolveHull(DeckRiderVisual rider)
        {
            IBoatHullPresenter presenter = rider.LiveHullPresenter;
            Transform visual = presenter != null ? presenter.Visual : null;
            if (visual == null) return null;
            IsoFacetHullRenderer hull = visual.GetComponent<IsoFacetHullRenderer>();
            return hull != null && hull.PosedMesh != null ? hull : null;
        }

        // ---------------------------------------------------------------- the figure

        private bool EnsureFigure(CharacterSkinDef skin, IsoFacetHullRenderer hull)
        {
            if (_figure != null && _hull == hull && _configured == skin) return true;

            Release();
            _hull = hull;
            _configured = skin;

            Transform posed = hull.PosedMesh;
            var go = new GameObject("MeshCharacter") { hideFlags = HideFlags.DontSave };
            go.layer = posed.gameObject.layer;      // the hull's layer, or the facet pass never sees her
            go.transform.SetParent(posed, false);
            _figureRoot = go.transform;

            // Parent FIRST, configure second: IsoCharacterFigureRenderer finds its hull by walking up
            // the parent chain, and a figure configured while unparented would carry no hull id.
            _figure = go.AddComponent<IsoCharacterFigureRenderer>();
            _figure.Configure(skin);
            _figure.Visible = false;
            _stateKey = null;
            return _figure.IsConfigured;
        }

        /// <summary>
        /// <b>Where she stands, and which way she faces — both borrowed, neither computed.</b>
        ///
        /// <para>The position is <see cref="DeckRiderVisual.DeckStandRigLocal"/>: the very point the
        /// deck-occupant slot is fed with, published at the line that feeds it. The hull's posed mesh
        /// child's local space IS the rig frame in metres — <c>IsoFacetHullRenderer.ViewDepthOf</c>
        /// proves it, since it answers a slot's depth by transforming exactly this vector through
        /// exactly this transform. So the figure's foot and her occluder band are, by construction,
        /// the same point.</para>
        ///
        /// <para>The yaw is the deck bearing, about the rig's up axis (+z), through the def's own
        /// MEASURED azimuth sign. This is not a guessed convention: the hull's heading enters the same
        /// frame as <c>Rz(dirUnits·45°)</c> with the identical negation
        /// (<c>HullMeshMath.HeadingToDirUnits</c>), and the bind mesh is baked at rig dir 0, so a
        /// figure at deck bearing 0 faces the bow. This lane has been CCW-mislabelled twice; the sign
        /// is read off the bake, not declared here.</para>
        /// </summary>
        private void Place(DeckRiderVisual rider, CharacterSkinDef skin)
        {
            Vector3 local = rider.DeckStandRigLocal + SabotageStandOffsetMetres;
            float yaw = skin.AzimuthCounterClockwise ? -rider.DeckBearingDegrees : rider.DeckBearingDegrees;
            FigureLocalMetres = local;
            FigureYawDegrees = yaw;
            _figureRoot.SetLocalPositionAndRotation(local, Quaternion.AngleAxis(yaw, Vector3.forward));
        }

        private void Release()
        {
            if (_figureRoot != null) DestroySafely(_figureRoot.gameObject);
            _figureRoot = null;
            _figure = null;
            _hull = null;
            _configured = null;
            _stateKey = null;
            FellBackToGait = false;
        }

        private static void DestroySafely(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
