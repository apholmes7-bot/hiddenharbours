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
    /// <para><b>Y-sort (ADR 0032).</b> The child copies the root renderer's <c>sortingOrder</c> every
    /// frame, which <c>YSortSprite</c> has just written from the player's world Y. So the rider keeps
    /// exactly the layering the visible on-deck player already had — no second policy, no parked constant,
    /// nothing new inside the decor band.</para>
    ///
    /// <para><b>Rules.</b> Visual-only: it reads the rock the boat already computed and writes nothing back
    /// (rule 5). Every amplitude is a serialized tunable and <see cref="_rideStrength"/> 0 restores the old
    /// bolt-upright read exactly, so the owner can A/B it (rule 6). No allocation and no
    /// <c>GetComponent</c> on the hot path — the boat's components are resolved once per binding, and a
    /// hull swapped under the player's feet re-resolves (rule 7). Player references Boats already
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

        [Tooltip("The player's deck walk — read ONLY for where the fisher stands in the HULL's own " +
                 "frame, which is what lets a mesh hull depth-test them against her wheelhouse. " +
                 "Auto-resolved off this object if left empty. Absent = the figure is drawn in-scene " +
                 "exactly as before, since nothing can say where on the deck they are standing.")]
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

        [Header("Pilot")]
        [Tooltip("Draw the character while they are AT THE HELM. Off restores the old behaviour (taking " +
                 "the helm hides the figure entirely) — kept as a switch because it is the single most " +
                 "visible change here and the owner may want to compare.")]
        [SerializeField] private bool _drawPilot = true;

        // ---- live state ---------------------------------------------------------------------------

        private ControlMode _mode = ControlMode.OnFoot;
        private Transform _boatRoot;

        // The boat's components, resolved ONCE per binding (rule 7). Re-armed by Bind, so the dev hull
        // picker swapping a hull under the player's feet is never read through a stale reference.
        private BoatWaveMotion _wave;
        private BoatController _boat;
        private IBoatHullPresenter _hull;
        private bool _boatResolved;

        private Vector3 _riderBaseLocalPosition;
        private bool _baseCached;
        private bool _riding;

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

        /// <summary>
        /// True when the HULL is drawing the figure — composited into her own image and depth-tested
        /// per pixel against her superstructure — rather than the in-scene child. False on a sprite
        /// hull, an unskinned boat and ashore, where the child draws exactly as it always did. Never
        /// both: <c>DrawnInHull</c> and <see cref="IsDrawing"/>'s child are mutually exclusive by
        /// construction. For tests / tooling.
        /// </summary>
        public bool DrawnInHull { get; private set; }

        /// <summary>True when a rider child is wired at all. A rig without one is legal and inert.</summary>
        public bool HasRider => _riderRenderer != null;

        // ---- wiring -------------------------------------------------------------------------------

        /// <summary>Wire the rider in one call (the editor builder / tests). <paramref name="deckWalk"/>
        /// is optional and only unlocks the hull-composited figure: without it the rider cannot say
        /// where on the deck the fisher stands, so the in-scene child draws, exactly as before.</summary>
        public void Configure(SpriteRenderer riderRenderer, SpriteRenderer bodyRenderer,
                              IsoCharacterSprite character, DeckWalkController deckWalk = null)
        {
            _riderRenderer = riderRenderer;
            _bodyRenderer = bodyRenderer;
            _character = character;
            if (deckWalk != null) _deckWalk = deckWalk;
            _baseCached = false;
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
                _boatRoot = boatRoot;
                _boatResolved = false;    // a different boat — re-find her rock, her helm and her skin
            }
            Apply();
        }

        private void Awake()
        {
            if (_bodyRenderer == null) _bodyRenderer = GetComponent<SpriteRenderer>();
            if (_character == null) _character = GetComponent<IsoCharacterSprite>();
            if (_deckWalk == null) _deckWalk = GetComponent<DeckWalkController>();
        }

        private void OnEnable() => Apply();

        /// <summary>Hand the picture back on teardown: the child goes dark and the body renderer resumes
        /// under the pre-rider rule. A rider torn down mid-voyage therefore degrades to exactly the
        /// behaviour that shipped before it existed — never to nobody drawing at all.</summary>
        private void OnDisable() => StandDown();

        private void LateUpdate() => Apply();

        // ---- the one path ------------------------------------------------------------------------

        /// <summary>Present the character for the current mode. Idempotent and cheap — safe to call from
        /// the mode switch and from every LateUpdate.</summary>
        private void Apply()
        {
            bool aboard = _mode == ControlMode.OnDeck ||
                          (_mode == ControlMode.Aboard && _drawPilot);

            if (!aboard || _riderRenderer == null || !isActiveAndEnabled)
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

            // (1) Ask the character presenter for the right stance, and — at the helm — for the right
            //     facing. It still owns every sheet decision; this only states the context.
            RequestedStance = StanceForMode();
            if (_character != null)
            {
                _character.Stance = RequestedStance;
                if (_mode == ControlMode.Aboard && _hull != null) _character.HoldHeading(_hull.DrawnHeadingDegrees());
                else _character.ReleaseHeading();
            }

            // (2) MIRROR the finished picture. Everything the body renderer carries travels — the cell
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

            // (3) RIDE. The rock the hull is drawing, in the rider's own tuned amplitudes.
            DeckRidePose pose = ReadRide();
            Pose = pose;
            _riderRenderer.transform.localPosition =
                _riderBaseLocalPosition + new Vector3(0f, pose.LiftMeters, 0f);
            _riderRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, pose.RollDegrees);

            // (4) WHO DRAWS THE FINISHED FIGURE. Offer it to the HULL first: a mesh hull composites
            //     the crew into her own recording, where her private z-buffer settles them per pixel
            //     and the wheelhouse covers them properly. She takes it or she does not (a sprite
            //     compass never can), and the in-scene child draws exactly when she does not — so
            //     there is always precisely one figure.
            DrawnInHull = TryDrawInHull(pose);
            if (_riderRenderer.enabled == DrawnInHull) _riderRenderer.enabled = !DrawnInHull;
            _riding = true;
        }

        /// <summary>
        /// <b>Hand the finished figure to the hull, to be drawn INSIDE her image</b> (owner playtest
        /// 2026-08-07: "sprites visible THROUGH closed cabins").
        ///
        /// <para>Sorting cannot fix that defect. A mesh hull composes through ONE overlay quad at one
        /// order, so the fisher is wholly in front of the boat or wholly behind her — and behind her
        /// is invisible, because the sole under their boots is hull pixels too. So the figure is not
        /// sorted against the boat at all: the hull takes the same finished picture this component
        /// would have drawn and composites it into her own off-screen recording, where the depth
        /// buffer answers per PIXEL.</para>
        ///
        /// <para><b>The one number that makes it work</b> is the boots' DEPTH in the hull's frame —
        /// <see cref="DeckAreaMath.DeckDepth"/>, the third row of the very projection the deck walk
        /// already uses for the first two, so a fisher clamped onto the deck and the deck they are
        /// clamped to agree about which is nearer by construction. Everything else in the pose is a
        /// finished presentation fact copied from the picture already assembled above; nothing here
        /// re-decides a cell, a facing or a tint.</para>
        ///
        /// <para>Returns FALSE — and the in-scene child draws as it always did — for a sprite hull, an
        /// unskinned boat, a rig with no deck walk to say where on the deck they stand, or a presenter
        /// torn off by a hull swap. Every one of those is the pre-existing behaviour, not a new
        /// failure mode.</para>
        /// </summary>
        private bool TryDrawInHull(in DeckRidePose pose)
        {
            if (_hull == null || _deckWalk == null || _riderRenderer.sprite == null) return false;

            // The child's ORIGIN is the boots: the character sheets pivot on ground contact, and the
            // ride's lift is already in that transform. So this is where the figure meets the deck,
            // lift and all, with nothing to keep in step by hand.
            Vector3 feet = _riderRenderer.transform.position;

            return _hull.SetDeckOccupant(DeckOccupantPoseMath.For(
                _riderRenderer.sprite, new Vector2(feet.x, feet.y),
                _deckWalk.DeckLocalPosition, _deckWalk.DeckHeightMeters,
                _hull.DrawnHeadingDegrees(), _hull.BakeElevationDegrees,
                pose.RollDegrees, _riderRenderer.flipX, _riderRenderer.color));
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

            // Take the figure back off the hull. Unconditional and first, because the failure it
            // guards is the same "nobody drawing" one this whole method exists for, mirrored: a hull
            // still compositing a fisher who has stepped ashore would draw them standing on a boat
            // they left. Null-safe — a torn-down or swapped-away presenter simply has nothing to clear.
            if (_hull != null) _hull.ClearDeckOccupant();
            DrawnInHull = false;

            // The child's pose is put back ONCE, on the way out — not re-zeroed every ashore frame. Nothing
            // else writes it while the rider is standing down, so a per-frame reset would only dirty a
            // transform for the whole time the player is walking the island (rule 7).
            if (_riding)
            {
                if (_riderRenderer != null)
                {
                    _riderRenderer.transform.localRotation = Quaternion.identity;
                    if (_baseCached) _riderRenderer.transform.localPosition = _riderBaseLocalPosition;
                }
                if (_character != null)
                {
                    _character.Stance = CharacterStance.Free;
                    _character.ReleaseHeading();
                }
            }
            _riding = false;
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
        /// to "no ride", not throw).</summary>
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
