using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>THE CHARACTER RIDES THE BOAT</b> — and, at the helm, is actually THERE.
    ///
    /// <para>Two defects, one cause. On deck the fisher stood bolt upright on a hull that visibly rolled
    /// around them; at the helm they vanished altogether, and the dory rowed with disembodied oars. Both
    /// come from the same structural fact: the boat's rock is applied ONLY to its counter-rotated visual
    /// child (<see cref="BoatWaveMotion"/>), while the player is parented to the PHYSICS ROOT and has
    /// their world rotation stomped back to identity every frame by
    /// <see cref="DeckWalkController"/>. So the character can never inherit hull tilt by parenting — that
    /// is by design, not a bug. The boat's own answer is to put its rock on a VISUAL CHILD. This is the
    /// character doing exactly the same thing.</para>
    ///
    /// <para><b>What it draws (and does not).</b> It picks NO sprites. The character's cell is still chosen
    /// by the one authority, <see cref="IsoCharacterSprite"/> on the player root — same heading rule, same
    /// gait ladder, same sheets — and this component only tells it WHICH STANCE to ask for and MIRRORS the
    /// finished picture onto a child renderer it is free to lean and lift. That is why the haul animation,
    /// the rod fight and the fishing cycle ride the deck for free: they write the root renderer, and the
    /// mirror carries whatever is on it. Nothing here re-implements a facing, a frame or a sheet lookup.</para>
    ///
    /// <list type="bullet">
    ///   <item><b>OnDeck</b> → the <see cref="CharacterStance.Balance"/> stance (the rig's deck-balance
    ///   clip) while standing; an ordinary walk while crossing, because the def's ladder falls back to the
    ///   free body for a gait the stance never baked.</item>
    ///   <item><b>Aboard (the helm)</b> → the hull's OWN pilot stance, read off the boat visual asset
    ///   (<see cref="BoatVisualDef.PilotStanceFor"/>): oars for a pulled hull, wheel/tiller for a steered
    ///   one. Never decided here — that would be a content rule in code (rule 2). The facing is pinned to
    ///   the hull's DRAWN heading, because a pilot stands still while the boat turns under them and motion
    ///   alone would leave them staring the wrong way.</item>
    ///   <item><b>OnFoot</b> → completely inert. The child renderer is off, the root renderer is back on
    ///   and the stance is released, so the ashore fisher is byte-identical to before this existed.</item>
    /// </list>
    ///
    /// <para><b>How the ride is read.</b> The hull publishes the wave phase it is DRAWING its rock at
    /// (<see cref="BoatWaveMotion.RockPhaseDegrees"/> — quantised to the baked frame on a sprite hull,
    /// continuous on a mesh one), and <see cref="DeckRideMath"/> turns that into a lean and a lift with
    /// this component's own tuned amplitudes. That is the established shape:
    /// <see cref="DoryOarLayer"/> rides the same rock the same way. A hull still on the LEGACY transform
    /// rock publishes no phase — there is no cycle to publish — so the rider falls back to the exact tilt
    /// that hull is applying to its visual (<see cref="IBoatHullPresenter.VisualTiltDegrees"/>): a lean
    /// with no heave, which is honest rather than invented.</para>
    ///
    /// <para><b>Why the lean lands on the feet.</b> The child sits at localPosition zero and the character
    /// sheets are baked with the pivot on GROUND CONTACT, so a z-rotation of the child rotates the figure
    /// about the spot where they meet the deck. Lean without slide, for free, because of how the art was
    /// baked.</para>
    ///
    /// <para><b>ORIENTATION IS DERIVED, NEVER INHERITED OR ACCUMULATED</b> (owner playtest 2026-08-07:
    /// <i>"the sprite doesn't follow the orientation of the boat, they slowly lose reference and spin
    /// horizontally"</i>). A rider lives inside a frame that rotates — the boat's physics root — and there
    /// were two ways for that rotation to leak into the picture. Both are now closed by stating the pose
    /// from the authority every frame instead of letting anything compose:</para>
    /// <list type="number">
    ///   <item><b>The figure's screen ROTATION.</b> The lean is written as a WORLD rotation, not a local
    ///   one, so the drawn body is exactly the pose <see cref="DeckRideMath"/> asked for and nothing else.
    ///   Until this it was a local rotation on a child of the player root, and the player root is only
    ///   stomped upright by <see cref="DeckWalkController"/> — which the switcher DISABLES at the helm. So
    ///   the moment #445 made the pilot visible, the pilot inherited the hull's own z rotation and lay over
    ///   further with every degree she turned. (The switcher now keeps the root square in every aboard mode
    ///   as well; this is the belt to that braces, and the reason a future component parked on the player
    ///   cannot re-open it.)</item>
    ///   <item><b>The figure's FACING and GAIT.</b> <see cref="IsoCharacterSprite"/> reads both off its own
    ///   <c>localPosition</c>, which is right for a drifting hull and wrong for a TURNING one: a hull coming
    ///   about moves a motionless fisher in her frame with no step taken, so the measured velocity is an
    ///   artefact of the turn — it sweeps round as she turns and the facing chases it, which is the slow
    ///   horizontal spin. The rider therefore STATES both: the facing is
    ///   <see cref="DeckRiderFacingMath.CompassHeading"/> of (the fisher's own deck bearing) + (the hull's
    ///   drawn heading), and the speed is metres of DECK per second read off
    ///   <see cref="DeckWalkController.DeckLocalPosition"/> — a hull-frame quantity, so the hull's turn is
    ///   not in it at all. A fisher standing still now turns WITH the deck; one walking the deck faces
    ///   their walk.</item>
    /// </list>
    ///
    /// <para><b>Y-sort (ADR 0032).</b> The child copies the root renderer's <c>sortingOrder</c> every
    /// frame, which <c>YSortSprite</c> has just written from the player's world Y. So the rider keeps
    /// exactly the layering the visible on-deck player already had — no second policy, no parked constant,
    /// nothing new inside the decor band.</para>
    ///
    /// <para><b>Rules.</b> Visual-only: it reads the rock the boat already computed and writes nothing back
    /// (rule 5). Every amplitude is a serialized tunable and <see cref="_rideStrength"/> 0 restores the old
    /// bolt-upright read exactly, so the owner can A/B it (rule 6). No allocation on the hot path: the
    /// boat's COMPONENTS are resolved once per binding and survive a re-skin, and only the presenter —
    /// a POCO the skinner replaces outright — is re-read live, at one <c>GetComponent</c> a read
    /// (<see cref="LiveHull"/>, rule 7). Player references Boats already
    /// (<see cref="DeckWalkController"/> does the same presenter read), so no module gains an edge (rule 4).</para>
    /// </summary>
    [DisallowMultipleComponent]
    // A READER of everything: the hull's rock (BoatWaveMotion −120), the hull's drawn facing
    // (DirectionalBoatSprite −110), the character's own cell and the player's Y-sort order (both at the
    // default 0). Running last is what makes the mirror a mirror rather than a frame-late copy.
    [DefaultExecutionOrder(100)]
    public sealed class DeckRiderVisual : MonoBehaviour
    {
        [Header("Wiring (the builder sets these)")]
        [Tooltip("The CHILD renderer the on-deck / pilot figure is drawn into — the one thing here that " +
                 "may lean, because the player ROOT's rotation is stomped upright every frame by design. " +
                 "Null = this component is completely inert and the root renderer draws as it always did.")]
        [SerializeField] private SpriteRenderer _riderRenderer;

        [Tooltip("The player's own renderer — what draws them ashore, and the source the rider MIRRORS. " +
                 "Auto-resolved off this object if left empty.")]
        [SerializeField] private SpriteRenderer _bodyRenderer;

        [Tooltip("The character presenter that picks the cell. This component tells it which STANCE to " +
                 "ask for and which way to face; it never picks a sprite itself. Auto-resolved if empty.")]
        [SerializeField] private IsoCharacterSprite _character;

        [Tooltip("The player's deck walk — read ONLY for where the fisher stands in the HULL's own frame, " +
                 "which is what makes their facing and gait immune to the hull's turn. Auto-resolved off " +
                 "this object if left empty. Absent (a rig with no deck walk) = the fisher holds the facing " +
                 "they boarded with and is drawn standing, which is honest: nothing is walking them.")]
        [SerializeField] private DeckWalkController _deckWalk;

        [Header("Ride (how the deck moves its passenger — all tunable, rule 6)")]
        [Tooltip("Master strength of the whole ride. 0 = the character stands bolt upright on a rolling " +
                 "deck exactly as before this component existed (the owner's A/B); 1 = the tuned read.")]
        [SerializeField, Min(0f)] private float _rideStrength = 1f;

        [Tooltip("Peak ROLL of the deck under the character, in degrees — an ART FACT of the hull's baked " +
                 "rock cycle (the iso dory's rig bakes rollA 5.0, the same number DoryOarLayer leans her " +
                 "oars by). Not a feel knob: it says what the DECK does, and Footing below says how much of " +
                 "it the fisher takes.\n\n" +
                 "⚠️ THIS IS PER-HULL IN THE ART AND NOT YET PER-HULL IN THE DATA. Each rig declares its " +
                 "own ROCK block — dory 5.0, punt 4.2, sport skiff 3.8, console 3.4, lobster boat 2.8, " +
                 "Cape Islander 2.6, side dragger 2.0, stern trawler 1.6, coastal packet 1.3, tanker 0.85 " +
                 "— so this one number is exact for the dory (the start boat, and the only hull that draws " +
                 "a ROWING pilot) and increasingly generous on the bigger hulls, where a rider would lean " +
                 "further than the deck under them. Fixing it properly means a HullRock triple on " +
                 "BoatVisualDef that this reads per boat; the existing MotorRock* fields CANNOT be reused " +
                 "for it, because they carry the console's initialiser on 12 of the 14 shipped visuals " +
                 "(see BoatVisualDef.MotorMountLocalMeters' own warning about exactly that).")]
        [SerializeField] private float _deckRollDegrees = 5f;

        [Tooltip("Peak HEAVE of the deck in PIXELS at the sheets' resolution (the dory's rig bakes heaveA " +
                 "1.6) — reproduced exactly, because the character's feet are ON the deck. Gaining this " +
                 "down would sink them through the planking at the crest. Per-hull in the art: see the " +
                 "roll tooltip above.")]
        [SerializeField] private float _deckHeavePixels = 1.6f;

        [Tooltip("Screen-vertical travel at the peak of the deck's PITCH, in metres. Small: a ¾ view reads " +
                 "a bow-up/bow-down tip mostly as vertical movement at this scale. (The dory's rig states " +
                 "pitchA as a 3° ROTATION; what that costs in screen travel at a body standing amidships " +
                 "is the small number here, not the rotation itself.)")]
        [SerializeField] private float _deckPitchLiftMeters = 0.02f;

        [Tooltip("Pixels-per-unit the character/hull sheets import at (32) — converts the baked pixel " +
                 "heave into metres.")]
        [SerializeField, Min(1f)] private float _pixelsPerUnit = 32f;

        [Tooltip("How much of the deck's LEAN the character takes: 1 = bolted to it (a crate), 0 = perfectly " +
                 "upright whatever the sea does (the old bug). A fisher braces, so the tuned default is " +
                 "under half — the deck rolls 5° and they take about 3°. This is the runtime's stand-in " +
                 "for the rig's own counterLean, which needs a re-bake to draw properly.")]
        [SerializeField, Range(0f, 1f)] private float _footing = 0.6f;

        [Header("Facing (which way the figure looks while aboard)")]
        [Tooltip("Deck-frame speed (m/s) below which a step is treated as noise and the fisher's DECK " +
                 "BEARING is held — so someone who stops keeps looking where they were going instead of " +
                 "snapping round. The deck-frame twin of IsoCharacterSprite's own heading floor, and the " +
                 "same small number for the same reason: the gait may read 'idle' while a real turn is " +
                 "still finishing.")]
        [SerializeField, Min(0f)] private float _deckStepMinSpeed = 0.05f;

        [Header("Pilot")]
        [Tooltip("Draw the character while they are AT THE HELM. Off restores the old behaviour (taking " +
                 "the helm hides the figure entirely) — kept as a switch because it is the single most " +
                 "visible change here and the owner may want to compare.")]
        [SerializeField] private bool _drawPilot = true;

        // ---- live state ---------------------------------------------------------------------------

        private ControlMode _mode = ControlMode.OnFoot;
        private Transform _boatRoot;

        // The boat's components, resolved ONCE per binding (rule 7) — re-armed when the BOAT changes.
        // The PRESENTER is the exception: a hull swapped in place does not change the boat, so _hull is
        // only the bind-time fallback and every read goes through LiveHull(). See its remarks.
        private BoatWaveMotion _wave;
        private BoatController _boat;
        private IBoatHullPresenter _hull;
        private bool _boatResolved;

        private Vector3 _riderBaseLocalPosition;
        private bool _baseCached;
        private bool _riding;

        // WHERE THE FISHER IS LOOKING, relative to the deck (0 = at the bow). The one piece of facing state
        // there is, and it is deliberately in the HULL's frame: the hull's turn cannot touch it, so composing
        // it with her drawn heading every frame carries the fisher round with the boat and never drifts.
        private float _deckBearingDegrees;
        // Last frame's hull-frame stand point, for the deck-frame velocity the bearing and the gait are read
        // from. Un-seeded (_deckTracked false) on the first riding frame and after any re-bind, so a SNAP
        // onto the deck is never read as one enormous stride.
        private Vector2 _lastDeckLocal;
        private bool _deckTracked;

        // THE OCCLUSION CHANNEL (see ApplyOcclusion). One property block, reused; the id is written
        // only when it CHANGES, which is twice a voyage rather than twice a second.
        private MaterialPropertyBlock _occluderBlock;
        private Material _occluderMaterial;
        private IBoatHullPresenter _occupiedHull;
        private float _occluderIdWritten;
        private float _occluderTopWritten;
        private bool _occluderBlockValid;
        /// <summary>The deck slot this rider holds on <see cref="_occupiedHull"/>, or -1. Claimed
        /// once on boarding and held until they step off — not per frame, so nothing can shuffle
        /// underneath them while they stand there.</summary>
        private int _occupantSlot = -1;

        /// <summary>The shader property the occluding hull id is written to, and the shader that
        /// reads it. Named here rather than reached for through Art, which Player may not reference
        /// (rule 4) — a shader name is a string contract, and this is the one place it is spelled on
        /// this side of the seam.</summary>
        private const string OccludedSpriteShader = "HiddenHarbours/DeckOccludedSprite";
        private static readonly int DeckOccluderIdProperty = Shader.PropertyToID("_HHDeckOccluderId");
        /// <summary>The top of the hull's fore-id block. The discard is a RANGE — an occupant is
        /// hidden by every band nearer than their own — so writing the low id without this one
        /// leaves an empty range and hides nobody.</summary>
        private static readonly int DeckOccluderIdTopProperty =
            Shader.PropertyToID("_HHDeckOccluderIdTop");

        /// <summary>The control mode this rider is presenting (set by the <see cref="ControlSwitcher"/>).</summary>
        public ControlMode Mode => _mode;

        /// <summary>True while the RIDER is drawing the character — i.e. aboard with a child renderer
        /// wired. False ashore, and false on any rig that has no rider child (where the root renderer
        /// draws exactly as it always did). The claim the PlayMode tests read.</summary>
        public bool IsDrawing => _riding;

        /// <summary>The stance being asked of the character right now — <see cref="CharacterStance.Free"/>
        /// ashore. For tests / tooling.</summary>
        public CharacterStance RequestedStance { get; private set; } = CharacterStance.Free;

        /// <summary>The ride pose applied to the rider child last tick. For tests / tooling.</summary>
        public DeckRidePose Pose { get; private set; } = DeckRidePose.Level;

        /// <summary>Where the fisher is looking RELATIVE TO THE DECK (degrees; 0 = at the bow, +90 = to
        /// starboard). Only their own walking changes it — the hull's turning cannot. For tests / tooling.</summary>
        public float DeckBearingDegrees => _deckBearingDegrees;

        /// <summary>The compass heading of the hull PICTURE this rider is reading RIGHT NOW — the quantity
        /// a presenter cached across a hull swap silently pins to north. For tests / tooling.</summary>
        public float HullDrawnHeadingDegrees => DrawnHeadingDegrees();

        /// <summary>True when a rider child is wired at all. A rig without one is legal and inert.</summary>
        public bool HasRider => _riderRenderer != null;

        // ---- wiring -------------------------------------------------------------------------------

        /// <summary>Wire the rider in one call (the editor builder / tests).</summary>
        public void Configure(SpriteRenderer riderRenderer, SpriteRenderer bodyRenderer,
                              IsoCharacterSprite character)
        {
            _riderRenderer = riderRenderer;
            _bodyRenderer = bodyRenderer;
            _character = character;
            _baseCached = false;
            _occluderBlockValid = false;   // a different renderer carries a different property block
            EnsureOccludableMaterial();
        }

        /// <summary>Tune the ride in one call (tests / editor feel sessions).</summary>
        public void ConfigureRide(float rideStrength, float deckRollDegrees, float deckHeavePixels,
                                  float deckPitchLiftMeters, float pixelsPerUnit, float footing)
        {
            _rideStrength = Mathf.Max(0f, rideStrength);
            _deckRollDegrees = deckRollDegrees;
            _deckHeavePixels = deckHeavePixels;
            _deckPitchLiftMeters = deckPitchLiftMeters;
            _pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
            _footing = Mathf.Clamp01(footing);
        }

        /// <summary>
        /// The control mode changed — the <see cref="ControlSwitcher"/> hands it here rather than this
        /// component listening on the bus, so the swap is deterministic and ordered with the rest of the
        /// boarding transition (the same way the deck controller is bound and enabled).
        ///
        /// <para><paramref name="boatRoot"/> is the boat's PHYSICS ROOT while aboard and null ashore.</para>
        /// </summary>
        public void SetMode(ControlMode mode, Transform boatRoot)
        {
            _mode = mode;
            if (_boatRoot != boatRoot)
            {
                // The boat we are LEAVING must be told her deck is empty before we forget her — a
                // stranded occupant would keep her splitting her own image, hiding the next thing
                // that ever stands in front of her, with nobody aboard to explain it.
                ClearDeckOccupant();
                _boatRoot = boatRoot;
                _boatResolved = false;    // a different boat — re-find her rock, her helm and her skin
            }
            // Every transition re-seats the fisher (BoardDeck / TakeHelm / LeaveHelm all snap them), so the
            // deck-frame step measured across one is a TELEPORT, not a stride. Drop the track and let the
            // next tick re-seed both it and the bearing.
            _deckTracked = false;
            StateContext();   // the presenter must not pick one cell from the OLD mode
            Apply();
        }

        private void Awake()
        {
            if (_bodyRenderer == null) _bodyRenderer = GetComponent<SpriteRenderer>();
            if (_character == null) _character = GetComponent<IsoCharacterSprite>();
            if (_deckWalk == null) _deckWalk = GetComponent<DeckWalkController>();
            EnsureOccludableMaterial();
        }

        private void OnEnable()
        {
            StateContext();
            Apply();
        }

        /// <summary>Hand the picture back on teardown: the child goes dark and the body renderer resumes
        /// under the pre-rider rule. A rider torn down mid-voyage therefore degrades to exactly the
        /// behaviour that shipped before it existed — never to nobody drawing at all.</summary>
        private void OnDisable() => StandDown();

        /// <summary>
        /// <b>STATE THE CONTEXT, in Update — before the presenter chooses a cell.</b>
        ///
        /// <para>The stance, the facing and the travelling speed are INPUTS to
        /// <see cref="IsoCharacterSprite"/>, and it consumes them in its own <c>LateUpdate</c> at execution
        /// order 0 — before this component's 100. Written from here they would always be read one frame
        /// late: a hull turning at 6°/frame draws her pilot a whole 45° facing bucket behind herself,
        /// which is exactly what a PlayMode test measuring the turn caught. (That lag has been there since
        /// the pilot was first drawn; it was invisible because nothing had asked the question.)</para>
        ///
        /// <para>Update is the honest place for it: the hull's drawn heading is computed LIVE off her
        /// transform, and physics has already run for the frame, so the number here is the same one
        /// LateUpdate would see. The MIRROR still runs in <see cref="LateUpdate"/> at order 100 — that
        /// half must be last, or it copies a stale cell. Inputs early, picture late.</para>
        /// </summary>
        private void Update() => StateContext();

        private void LateUpdate() => Apply();

        // ---- the one path ------------------------------------------------------------------------

        /// <summary>
        /// Tell the character presenter what it is standing on: which stance to ask for, which way the
        /// figure is looking, and how fast they are really travelling. It still owns every sheet decision.
        ///
        /// <para>Exactly ONCE per frame — <see cref="TrackDeckStep"/> measures a step against last frame's
        /// hull-frame position, and a second call in the same frame would read that step as zero and report
        /// a standing fisher. <see cref="SetMode"/> may call it out of turn, but a transition drops the
        /// track first, so that path takes the seed branch and measures nothing.</para>
        ///
        /// <para>The OCCLUSION is stated here for the same reason the facing is: the hull's own drawer
        /// composes her split in LateUpdate at execution order 0, before this component's 100, so an
        /// occupant written from there would be a frame stale — and at the moment somebody steps aboard
        /// or ashore, a frame stale is a frame of the wrong picture.</para>
        /// </summary>
        private void StateContext()
        {
            if (!Aboard() || _riderRenderer == null || !isActiveAndEnabled) return;

            ResolveBoat();
            ApplyOcclusion();

            if (_character == null) return;
            RequestedStance = StanceForMode();
            _character.Stance = RequestedStance;
            ApplyFacing();
        }

        /// <summary>Is a figure being drawn on a boat right now? On deck always; at the helm only with the
        /// pilot switched on.</summary>
        private bool Aboard()
            => _mode == ControlMode.OnDeck || (_mode == ControlMode.Aboard && _drawPilot);

        /// <summary>Present the character for the current mode. Idempotent and cheap — safe to call from
        /// the mode switch and from every LateUpdate.</summary>
        private void Apply()
        {
            if (!Aboard() || _riderRenderer == null || !isActiveAndEnabled)
            {
                StandDown();
                return;
            }

            if (!_baseCached)
            {
                _riderBaseLocalPosition = _riderRenderer.transform.localPosition;
                _baseCached = true;
            }

            ResolveBoat();

            // (1) MIRROR the finished picture. Everything the body renderer carries travels — the cell
            //     (whoever wrote it: the iso skin, the haul animation, the rod fight), the Y-sorted order,
            //     the flip and any tint — so there is exactly one place the character's look is decided.
            if (_bodyRenderer != null)
            {
                _riderRenderer.sprite = _bodyRenderer.sprite;
                _riderRenderer.sortingLayerID = _bodyRenderer.sortingLayerID;
                _riderRenderer.sortingOrder = _bodyRenderer.sortingOrder;
                _riderRenderer.flipX = _bodyRenderer.flipX;
                _riderRenderer.color = _bodyRenderer.color;
                if (_bodyRenderer.enabled) _bodyRenderer.enabled = false;   // one figure, not two
            }
            if (!_riderRenderer.enabled) _riderRenderer.enabled = true;

            // (2) RIDE. The rock the hull is drawing, in the rider's own tuned amplitudes.
            DeckRidePose pose = ReadRide();
            Pose = pose;
            _riderRenderer.transform.localPosition =
                _riderBaseLocalPosition + new Vector3(0f, pose.LiftMeters, 0f);
            // WORLD rotation, not local: the drawn figure's screen orientation IS the pose and nothing else.
            // A local write would compose the lean onto whatever the player root happens to be carrying, and
            // aboard that root is a child of the hull's ROTATING physics body — which is how the pilot came
            // to lie over further with every degree she turned. See the class doc's ORIENTATION note.
            _riderRenderer.transform.rotation = Quaternion.Euler(0f, 0f, pose.RollDegrees);
            _riding = true;
        }

        /// <summary>
        /// <b>THE BOAT DRAWS OVER HER OWN CREW</b> — owner playtest 2026-08-07: <i>"rider/player sprites
        /// visible THROUGH closed cabins"</i> on hulls with a cockpit and doors.
        ///
        /// <para><b>Why sorting could never have fixed it.</b> The figure is Y-sorted in the decor band
        /// (ADR 0032) and the hull composes at her own whole-object slot beneath it, so the fisher is drawn
        /// over the WHOLE boat — wheelhouse included. Dropping her under the hull instead would hide her
        /// entirely, which is the pre-#445 behaviour the owner asked to be rid of. Neither order is right,
        /// because sorting is per OBJECT and the question is per PIXEL: a wheelhouse roof is in front of a
        /// figure inside it and the deck under their boots is behind them, in the same picture.</para>
        ///
        /// <para><b>So the hull answers it, where the answer already lives.</b> A mesh hull's facet pass
        /// runs against a private z-buffer; handed the fisher's stand point in her own rig metres, she marks
        /// every fragment nearer the camera than that point with a second id, and this renderer's shader
        /// discards where it reads it. Per pixel, at any heading, on every hull in the fleet, with no
        /// authored cabin footprint and no per-hull tuning — the geometry that is genuinely in front of the
        /// fisher is exactly the geometry that covers them.</para>
        ///
        /// <para><b>The stand point is the FEET</b>, and a single depth is the right model for a billboard
        /// whose base is there: the figure is a vertical plane at that depth, so anything nearer covers it
        /// and anything farther does not. The hull's own planking is at the same depth and stays behind
        /// (the compare is strict). Hull-frame all the way — <c>DeckLocalPosition</c> plus the height of
        /// the deck under them — so a rocking, turning, sailing boat needs no re-projection here at all.</para>
        ///
        /// <para>Cleared the moment the rider stands down, and 0 on any hull that cannot answer (a sprite
        /// hull, a greybox boat), where the shader is inert and the figure draws exactly as before.</para>
        /// </summary>
        private void ApplyOcclusion()
        {
            // A hull re-skinned under the player's feet (the dev picker does exactly that) hands us a
            // NEW presenter for the same boat — which is why this reads the LIVE host and not the
            // bind-time field. Let the old one go first, or she keeps splitting an image nobody is
            // standing in front of.
            IBoatHullPresenter hull = LiveHull();
            if (_occupiedHull != null && !ReferenceEquals(_occupiedHull, hull)) ClearDeckOccupant();

            float occluderId = 0f, occluderTop = 0f;
            if (hull != null)
            {
                // PUBLISH THEN CONSULT, in that order and every frame. The id handed back depends on
                // this rider's DEPTH RANK among everything else standing on the same deck — gear, a
                // trap stack, a second hand — so it cannot be known until where they stand has been
                // published. The claim itself is held, not retaken: only the first frame aboard pays.
                //
                // The claim is re-asserted every frame, and that is not waste: it is IDEMPOTENT (an
                // owner who already holds a slot gets the same index back, at the cost of a dozen
                // reference compares), and it is what makes the rider survive a hull being disabled
                // and re-enabled under her — that empties every slot and takes a fresh id block, and
                // a rider clinging to the index she was given before would go on writing into a slot
                // that is no longer hers and quietly stop being hidden by anything.
                IDeckOccupantSlots slots = hull.DeckOccupants;
                _occupantSlot = slots.Claim(this);
                if (_occupantSlot >= 0)
                {
                    Vector2 stand = _deckWalk != null ? _deckWalk.DeckLocalPosition : Vector2.zero;
                    float height = _deckWalk != null ? _deckWalk.DeckHeightMeters : 0f;
                    slots.Set(_occupantSlot, this, new Vector3(stand.x, stand.y, height), true);
                    occluderId = slots.OccluderId(_occupantSlot);
                    occluderTop = slots.OccluderIdTop;
                }
                _occupiedHull = hull;
            }
            WriteOccluderId(occluderId, occluderTop);
        }

        /// <summary>Tell whichever hull we last stood on that her deck is empty. Held as its own
        /// reference rather than read off <see cref="_hull"/>, because the two part company exactly
        /// when it matters: a boat swapped under the player's feet, and a rider torn down after the
        /// presenter has already been re-resolved.</summary>
        private void ClearDeckOccupant()
        {
            if (_occupiedHull == null) return;
            // RELEASED, not just set inactive: a slot held by a rider who has gone is one the next
            // thing to stand on that deck cannot have, and twelve boardings would exhaust the hull.
            if (_occupantSlot >= 0) _occupiedHull.DeckOccupants.Release(_occupantSlot, this);
            _occupantSlot = -1;
            _occupiedHull = null;
        }

        /// <summary>Push the occluding id onto the rider's renderer through a property block — per
        /// renderer, no material instancing, no allocation after the first frame (rule 7). Skipped
        /// entirely while the value has not changed, which is every frame but the two either side of
        /// boarding.</summary>
        private void WriteOccluderId(float occluderId, float occluderTop)
        {
            if (_riderRenderer == null) return;
            if (_occluderIdWritten == occluderId && _occluderTopWritten == occluderTop
                && _occluderBlockValid) return;

            _occluderBlock ??= new MaterialPropertyBlock();
            _riderRenderer.GetPropertyBlock(_occluderBlock);
            _occluderBlock.SetFloat(DeckOccluderIdProperty, occluderId);
            _occluderBlock.SetFloat(DeckOccluderIdTopProperty, occluderTop);
            _riderRenderer.SetPropertyBlock(_occluderBlock);
            _occluderIdWritten = occluderId;
            _occluderTopWritten = occluderTop;
            _occluderBlockValid = true;
        }

        /// <summary>
        /// Give the rider child the material that CAN be occluded, once, at wake-up.
        ///
        /// <para><b>Built here rather than wired by the builder</b> so that every rig — the shipped
        /// persistent core, an older scene, a test fixture — gets it without a re-run, and so the
        /// component that owns the id also owns the shader that reads it. Owned and destroyed here,
        /// with <c>HideAndDontSave</c>, the same discipline the hull renderer's own materials keep.</para>
        ///
        /// <para><b>A missing shader is not fatal and must never be.</b> If it cannot be found (a
        /// player build that stripped it, a broken import) the rider keeps whatever material it had
        /// and simply draws un-occluded — the picture that shipped before this existed. A magenta
        /// fisher would be far worse than a fisher visible through a cabin.</para>
        /// </summary>
        private void EnsureOccludableMaterial()
        {
            if (_riderRenderer == null) return;

            if (_occluderMaterial == null)
            {
                Shader shader = Shader.Find(OccludedSpriteShader);
                if (shader == null)
                {
                    Debug.LogWarning($"[DeckRiderVisual] Shader '{OccludedSpriteShader}' not found — " +
                                     "the on-deck figure will draw through the boat's own " +
                                     "superstructure (the pre-fix picture). Nothing else is affected.");
                    return;
                }
                _occluderMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            // Assigned every time this runs, not only when the material was just built: Configure may
            // hand us a DIFFERENT renderer (a re-wired rig, a test fixture), and the material has to
            // follow the renderer rather than the other way round.
            if (_riderRenderer.sharedMaterial != _occluderMaterial)
                _riderRenderer.sharedMaterial = _occluderMaterial;
        }

        private void OnDestroy()
        {
            if (_occluderMaterial == null) return;
            if (Application.isPlaying) Destroy(_occluderMaterial);
            else DestroyImmediate(_occluderMaterial);
            _occluderMaterial = null;
        }

        /// <summary>
        /// State which way the figure looks, and how fast they are really travelling.
        ///
        /// <para><b>On deck</b> the facing is composed: the fisher's own DECK BEARING (moved only by their
        /// own walking, measured in the hull's frame) plus the hull's DRAWN heading. So a fisher standing
        /// still turns with the boat, and one walking the deck faces their walk — and because both terms are
        /// re-read from the authority every frame there is no delta anywhere for drift to accumulate in.</para>
        ///
        /// <para><b>At the helm</b> the bearing is the bow: a pilot is at their station, facing forward, and
        /// the deck walk is disabled so there is no step to read anyway. This is exactly the pinned
        /// <c>DrawnHeadingDegrees()</c> that shipped, restated through the one composition.</para>
        ///
        /// <para><b>Ashore</b> nothing is held and the presenter's own motion read stands untouched.</para>
        ///
        /// <para>With no boat at all — which cannot happen while aboard, but a torn-down rig can reach it —
        /// nothing is held and the presenter reads motion as it always did.</para>
        /// </summary>
        private void ApplyFacing()
        {
            if (_boatRoot == null)
            {
                _character.ReleaseHeading();
                _character.ReleaseSpeed();
                return;
            }

            float hullHeading = DrawnHeadingDegrees();
            float deckSpeed = 0f;

            if (_mode == ControlMode.OnDeck)
            {
                TrackDeckStep(hullHeading, out deckSpeed);
            }
            else
            {
                // At the helm: the station's own facing, and a body that is not travelling.
                _deckBearingDegrees = 0f;
                _deckTracked = false;
            }

            _character.HoldHeading(DeckRiderFacingMath.CompassHeading(hullHeading, _deckBearingDegrees));
            _character.HoldSpeed(deckSpeed);
        }

        /// <summary>
        /// The compass heading of the hull PICTURE the fisher is standing on — quantised for a sprite
        /// compass, continuous for a mesh hull, and the physics heading for a boat wearing neither.
        ///
        /// <para>Deliberately the same three-way read as <see cref="DeckWalkController"/>'s own, because the
        /// two must agree: the deck walk clamps the fisher onto the deck of the hull drawn at this heading,
        /// and the facing composed here is what that fisher is drawn looking along. A second, differently
        /// sourced heading would put the figure on one hull and facing along another.</para>
        /// </summary>
        private float DrawnHeadingDegrees()
        {
            IBoatHullPresenter hull = LiveHull();
            if (hull != null) return hull.DrawnHeadingDegrees();
            return _boatRoot != null
                ? DirectionalBoatSprite.HeadingDegreesFromBow(_boatRoot.up)
                : 0f;
        }

        /// <summary>
        /// <b>The presenter for the hull worn RIGHT NOW</b> — the host's when there is one, else the one
        /// resolved at bind. The same live read as <see cref="DeckWalkController"/>'s own <c>LiveHull()</c>,
        /// and it must STAY the same read, for the reason <see cref="DrawnHeadingDegrees"/> gives — the
        /// walk and the rider have to agree about which hull is under the fisher.
        ///
        /// <para>Live, because the dev hull picker re-skins a boat <i>in place</i>: the BOAT ROOT never
        /// changes, so <see cref="ResolveBoat"/> never re-arms and a presenter cached once per binding
        /// would go on answering for a hull that is no longer drawn.</para>
        ///
        /// <para>Silently, which is what made it worth pinning: a dead
        /// <see cref="MeshHullPresenter"/> returns heading 0 (north) by its null-tolerant contract rather
        /// than throwing, so the fisher would simply stop turning with the boat — and would keep marking
        /// her occupant on a hull nobody stands on, leaving the new one unable to draw over her crew.</para>
        ///
        /// <para>One <c>GetComponent</c> per read and no allocation (rule 7) — the cost every other
        /// consumer of this seam already pays (<see cref="BoatCleats"/>, the deck containers).</para>
        /// </summary>
        private IBoatHullPresenter LiveHull()
        {
            if (_boatRoot == null) return _hull;
            var host = _boatRoot.GetComponent<BoatHullPresenterHost>();
            return (host != null && host.Presenter != null) ? host.Presenter : _hull;
        }

        /// <summary>
        /// Read this tick's step in the HULL's own frame and turn it into the fisher's deck bearing and their
        /// honest travelling speed.
        ///
        /// <para>The hull frame is the whole trick: the walkable polygons live in it, so a heading change
        /// costs a standing fisher exactly zero deck-frame movement — which is precisely the property the
        /// world-frame read lacked. Metres of DECK per second is also the number the gait should be chosen
        /// from: it is what the fisher's legs are doing.</para>
        ///
        /// <para>The first tracked frame (and the first after any re-seating) measures nothing: the fisher
        /// was PUT there, and a snap is not a stride. The bearing is seeded from the facing they arrived
        /// with, so stepping aboard is continuous rather than a spin to face forward.</para>
        /// </summary>
        private void TrackDeckStep(float hullHeading, out float deckSpeed)
        {
            deckSpeed = 0f;
            if (_deckWalk == null) return;

            Vector2 deckLocal = _deckWalk.DeckLocalPosition;
            if (!_deckTracked)
            {
                _deckTracked = true;
                _lastDeckLocal = deckLocal;
                _deckBearingDegrees =
                    DeckRiderFacingMath.DeckBearingFor(_character.HeadingDegrees, hullHeading);
                return;
            }

            float dt = Time.deltaTime;
            Vector2 step = deckLocal - _lastDeckLocal;
            _lastDeckLocal = deckLocal;
            if (dt <= 1e-6f) return;

            Vector2 deckVelocity = step / dt;
            _deckBearingDegrees = DeckRiderFacingMath.DeckBearing(deckVelocity, _deckStepMinSpeed,
                                                                  _deckBearingDegrees);
            deckSpeed = deckVelocity.magnitude;
        }

        /// <summary>
        /// The rider is NOT drawing — ashore, unwired, disabled, or at a helm with the pilot switched off.
        /// The child goes dark and level, the character is released from its stance and held facing, and
        /// the BODY renderer takes the picture back under the pre-existing rule (visible ashore and on
        /// deck, hidden at the helm).
        ///
        /// <para>Idempotent, and deliberately total: it runs on every tick this component is not riding,
        /// and on <c>OnDisable</c>. That matters because "rider child off AND body renderer off" is the one
        /// unrecoverable state — an invisible player — and it must not be reachable by tearing this
        /// component down mid-voyage.</para>
        /// </summary>
        private void StandDown()
        {
            if (_riderRenderer != null && _riderRenderer.enabled) _riderRenderer.enabled = false;

            // The child's pose is put back ONCE, on the way out — not re-zeroed every ashore frame. Nothing
            // else writes it while the rider is standing down, so a per-frame reset would only dirty a
            // transform for the whole time the player is walking the island (rule 7).
            if (_riding)
            {
                if (_riderRenderer != null)
                {
                    // World, matching the write in Apply: "level" is a statement about the SCREEN, and a
                    // local identity would leave the child carrying whatever the root is carrying.
                    _riderRenderer.transform.rotation = Quaternion.identity;
                    if (_baseCached) _riderRenderer.transform.localPosition = _riderBaseLocalPosition;
                }
                if (_character != null)
                {
                    _character.Stance = CharacterStance.Free;
                    // Both holds go back together: the figure is no longer standing on anything this
                    // component knows the frame of, so the presenter's own motion read is the honest one
                    // again. ReleaseHeading keeps the direction they were last facing, so nobody snaps.
                    _character.ReleaseHeading();
                    _character.ReleaseSpeed();
                }
            }
            // The hull stops hiding anybody, and the figure stops discarding against her. Both must
            // go: a stale occupant would keep splitting a hull nobody is standing on, and a stale id
            // on the renderer would punch a boat-shaped hole in a fisher who is ashore.
            ClearDeckOccupant();
            WriteOccluderId(0f, 0f);

            _riding = false;
            _deckTracked = false;
            Pose = DeckRidePose.Level;
            RequestedStance = CharacterStance.Free;

            // The body draws again — EXCEPT at the helm, which is the pre-existing "taking the helm hides
            // you" rule (ControlSwitcher's `onFoot || onDeck`) restated by its new owner. Nothing else
            // writes this flag while a rider is wired, so the two can never disagree.
            if (_bodyRenderer != null)
            {
                bool bodyVisible = _mode != ControlMode.Aboard;
                if (_bodyRenderer.enabled != bodyVisible) _bodyRenderer.enabled = bodyVisible;
            }
        }

        /// <summary>Which stance this mode asks for. On deck it is the deck brace; at the helm it is the
        /// HULL's own answer, read off her visual asset — never a rule here (rule 2). A boat with no
        /// visual def wired (greybox, tests) reads Free and simply stands there.</summary>
        private CharacterStance StanceForMode()
        {
            if (_mode == ControlMode.OnDeck) return CharacterStance.Balance;

            BoatVisualDef visual = _boat != null && _boat.Hull != null ? _boat.Hull.Visual : null;
            return visual != null ? visual.PilotStanceFor() : CharacterStance.Free;
        }

        /// <summary>
        /// The deck's motion this tick. Preferred read is the rock PHASE the hull publishes — the frame she
        /// is actually drawing on a sprite hull, the continuous pose on a mesh one. A hull on the legacy
        /// TRANSFORM rock publishes no phase (there is no cycle to publish), so the fallback reads the tilt
        /// she is applying to her own visual and leans the rider by the same braced fraction: honest, and
        /// strictly better than standing square on a hull that visibly leans.
        /// </summary>
        private DeckRidePose ReadRide()
        {
            if (_rideStrength <= 0f) return DeckRidePose.Level;

            if (_wave != null && _wave.IsRocking)
                return DeckRideMath.Ride(_wave.RockPhaseDegrees, _deckRollDegrees, _deckHeavePixels,
                                         _deckPitchLiftMeters, _pixelsPerUnit, _footing, _rideStrength);

            if (_hull != null)
            {
                float tilt = _hull.VisualTiltDegrees;
                if (tilt != 0f)
                    return new DeckRidePose(tilt * Mathf.Clamp01(_footing) * _rideStrength, 0f);
            }

            return DeckRidePose.Level;
        }

        /// <summary>Find the boat's rock, helm and skin — once per binding, never on the hot path. Plain
        /// <c>!= null</c> against the components throughout (Unity fake-null: a destroyed boat must degrade
        /// to "no ride", not throw).
        ///
        /// <para>The rock and the helm are COMPONENTS on the physics root: a re-skin re-configures them in
        /// place, so caching them per binding is honest. The presenter is not — it is a POCO the skinner
        /// swaps out — so <see cref="_hull"/> is a fallback for a boat with no host, and readers go through
        /// <see cref="LiveHull"/>.</para></summary>
        private void ResolveBoat()
        {
            if (_boatResolved) return;
            _boatResolved = true;

            if (_boatRoot == null)
            {
                _wave = null;
                _boat = null;
                _hull = null;
                return;
            }
            _wave = _boatRoot.GetComponent<BoatWaveMotion>();
            _boat = _boatRoot.GetComponent<BoatController>();
            _hull = BoatHullPresenterHost.Resolve(_boatRoot.gameObject);
        }
    }
}
