using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Economy;   // RepairLedger only — the boarding gate's single source of truth (see BoardableNow)

namespace HiddenHarbours.Player
{
    /// <summary>
    /// The control-mode state machine (OnFoot ⇄ OnDeck ⇄ Aboard-at-the-helm) — trap arc Build 5, the
    /// owner's on-deck control state replacing the old binary board-and-drive model:
    ///
    /// <list type="bullet">
    ///   <item><b>Boarding lands you ON THE DECK.</b> INTERACT (dev key E) within reach of the boat puts
    ///   the player standing on the deck as a walkable character (<see cref="DeckWalkController"/>) while
    ///   the boat rocks and drifts under them — NOT instantly driving. The deck is where boat/gear work
    ///   happens (set a pot, haul a trap — those systems gate themselves to <see cref="ControlMode.OnDeck"/>
    ///   via the Core signal).</item>
    ///   <item><b>The helm is a station.</b> Walk to the helm spot (a tunable offset — the tiller on the
    ///   dory) and INTERACT to take the helm → <see cref="ControlMode.Aboard"/>, steering exactly as
    ///   before. INTERACT again steps back onto the deck. Steering input is dead unless at the helm.</item>
    ///   <item><b>Disembark happens from the deck</b> (near a dock or over standable land — the same
    ///   step-off rules as before, just sourced from the deck state).</item>
    ///   <item><b>Boarding and stepping ashore are MOVES, not repositions</b> (owner ask, 2026-08-06:
    ///   "you push E and get teleported; the character should jump/climb/whatever to get onto the boat").
    ///   E sends the fisher walking to the nearest point on THIS hull's rail — her authored deck outline,
    ///   side decks included where her data carries them — and then over it, landing where boarding has
    ///   always seated them. The state machine below is untouched: the move decides WHEN the same
    ///   transition lands, never WHETHER it may. See the boarding-move block for where the sim flips and
    ///   why that end.</item>
    /// </list>
    ///
    /// The deck-walking player (and the helm spot / deck bounds maths) ride the boat's PHYSICS ROOT —
    /// never the counter-rotated visual child, which is stomped back to identity every LateUpdate.
    ///
    /// Cross-module via Core only: it toggles the Player + Boats controllers it holds and hands the
    /// camera off through the Core <see cref="ControlModeChanged"/> / <see cref="ActiveBoatChanged"/>
    /// signals — it never references the App camera (that would be a circular dependency). Greybox dev
    /// input + a tiny prompt; a real InputService / interaction UI replace them later (ui-ux).
    /// </summary>
    public class ControlSwitcher : MonoBehaviour
    {
        [Header("Player (on foot)")]
        [SerializeField] private PlayerWalkController _playerWalk;

        [Header("Boat (aboard)")]
        [SerializeField] private BoatController _boatController;
        [Tooltip("The boat's input behaviour (DevBoatInput) — enabled only while aboard.")]
        [SerializeField] private Behaviour _boatInput;
        [Tooltip("The boat's rope/mooring (BoatMooring). Optional — auto-resolved off the boat if left empty. " +
                 "On disembark onto land the player HOLDS the rope (boat tethered to the player); the root " +
                 "key drops it to the ground (boat tethered there, player free to roam) and back to hand.")]
        [SerializeField] private BoatMooring _mooring;

        [Header("Dock zone (the tidy dock landing only)")]
        [Tooltip("The dock/mooring point. Disembarking AT the dock lands the player tidily on the planks; " +
                 "otherwise they step off where the boat is. Boarding no longer requires the dock (board " +
                 "from anywhere within reach of the boat).")]
        [SerializeField] private Transform _dockZone;
        [Tooltip("Dock landing radius (m): when the boat is within this of the dock on disembark, the player " +
                 "lands on the dock planks (DisembarkPoint) instead of at the boat. Forgiving (P5 cozy).")]
        [SerializeField] private float _zoneRadius = 3.5f;
        [Tooltip("Where the on-foot player is placed after disembarking AT THE DOCK (on the dock planks). " +
                 "When disembarking away from the dock (onto a shore), the player steps off at the boat's " +
                 "own position instead, so this is only used for the tidy dock landing.")]
        [SerializeField] private Transform _disembarkPoint;

        [Header("Board from anywhere (proximity to the boat)")]
        [Tooltip("How close (m) the on-foot player must be to the boat to board it — board from ANYWHERE " +
                 "within this reach, not only at a dock zone. Forgiving (P5 cozy) so you can step aboard a " +
                 "boat you've nudged up to a beach, not just one parked at the wharf.")]
        [SerializeField] private float _boardReach = 3.5f;

        [Header("Disembark only onto LAND (no stepping off over water)")]
        [Tooltip("Disembarking is allowed only where the step-off point is actual standable LAND — never over " +
                 "open or submerged water. Land is detected two ways (either suffices): the authored tidal " +
                 "terrain is EXPOSED under the boat (the ground is at/above the water line — walkable flat), " +
                 "and/or a physical land/shore collider sits within LandProbeRadius on this mask. Leave the " +
                 "mask empty to rely on the exposed-terrain test alone (St Peters); set it to the layer your " +
                 "shore-edge/island colliders use (the cove's ShoreEdge is on Default) to allow step-off onto " +
                 "a hard shore.")]
        [SerializeField] private LayerMask _landMask = 0;
        [Tooltip("How close (m) a land/shore collider must be for the boat to count as 'over land'. Forgiving " +
                 "so a boat nudged up to the beach reliably lets you step off (P5 cozy).")]
        [SerializeField] private float _landProbeRadius = 2.5f;

        [Header("Rope / mooring (hold · root)")]
        [Tooltip("How close (m) the on-foot player must stand to a moored boat to hold/root its rope. " +
                 "Forgiving (P5 cozy) so you don't have to stand on the exact spot.")]
        [SerializeField] private float _moorReach = 4f;

        [Header("Deck & helm (Build 5 — the on-deck control state; all greybox tunables, rule 6)")]
        [Tooltip("The deck-walk controller on the PLAYER — enabled only while OnDeck. Auto-resolved off " +
                 "the walk controller's object if left empty (so tests/older wiring need no change).")]
        [SerializeField] private DeckWalkController _deckWalk;
        [Tooltip("The HELM STATION spot as a world-axis offset from the boat's position (the tiller at " +
                 "the dory's stern). World-aligned to match the snap-directional boat picture the player " +
                 "sees (the physics body's true rotation is hidden). Walk here + E to take the helm.")]
        [SerializeField] private Vector2 _helmLocalOffset = new Vector2(0f, -1.3f);
        [Tooltip("How close (m) the on-deck player must stand to the helm spot for E to take the helm. " +
                 "Kept tighter than the deck so there's still deck left to disembark from.")]
        [SerializeField] private float _helmReach = 0.9f;
        [Tooltip("Where boarding LANDS you on the deck, as a world-axis offset from the boat's position " +
                 "(clamped to the deck bounds) — amidships, a step away from the helm.")]
        [SerializeField] private Vector2 _boardLocalOffset = new Vector2(0f, 0.4f);

        [Tooltip("The DECK RIDER on the player — what draws the on-deck / pilot figure riding the hull's " +
                 "rock. Auto-resolved off the walk controller's object if left empty. Absent = the old " +
                 "behaviour exactly: the fisher stands square on deck and vanishes at the helm.")]
        [SerializeField] private DeckRiderVisual _deckRider;

        [Header("The boarding MOVE (owner ask 2026-08-06 — you climb aboard, you are not teleported)")]
        [Tooltip("Play the boarding MOVE at all. Off restores the instant reposition exactly (E → you are " +
                 "simply on the deck), which is the A/B for whether the move reads better than the snap, " +
                 "and the escape hatch if it ever fights something. The state machine is identical either " +
                 "way — the move only decides WHEN the same transition lands.")]
        [SerializeField] private bool _boardingMove = true;
        [Tooltip("Pace of the APPROACH leg (m/s) — the fisher walking to the rail. Matched to the on-foot " +
                 "walk (PlayerWalkController's 3 m/s) on purpose: the character's sprite driver picks " +
                 "idle/walk/run from MEASURED speed alone, so travelling at walking pace is what makes the " +
                 "approach draw as a walk. Push it past the skin's run threshold (4.5 m/s on FisherIso) and " +
                 "the fisher sprints at the boat instead.")]
        [SerializeField, Min(0.1f)] private float _boardApproachSpeed = 3f;
        [Tooltip("Hard cap on the approach leg (s), so boarding from the far edge of BoardReach still feels " +
                 "prompt. At 3 m/s this only bites past ~2.4 m; the reach is 3.5 m, so the worst case is a " +
                 "slightly brisk walk rather than a long trudge.")]
        [SerializeField, Min(0f)] private float _boardApproachMaxSeconds = 0.8f;
        [Tooltip("The VAULT leg (s) — up and over the rail, or down onto the wharf. The one duration the " +
                 "move's read really lives on; short enough to stay responsive, long enough to see. Both " +
                 "legs freeze under a pause and resume with it (they run on scaled time).")]
        [SerializeField, Min(0.01f)] private float _boardVaultSeconds = 0.55f;
        [Tooltip("How high the vault arcs above the straight line, in metres of SCREEN (up-screen is up in " +
                 "a ¾ view — the same axis a deck's own height lifts the player along). Keep it modest: the " +
                 "player Y-sorts on world Y, so a big hop would briefly sort the fisher behind things they " +
                 "are in front of. 0 makes the vault a flat slide.")]
        [SerializeField, Min(0f)] private float _boardVaultHopMeters = 0.35f;

        [Header("The one interact VERB (M2-39 — the seam, not a new key)")]
        [Tooltip("Offer the registered-candidate verb at all. Off restores the pre-seam behaviour exactly " +
                 "(E is only board / helm / step ashore), which is both the A/B and the escape hatch. It " +
                 "makes no difference whatever with an empty registry: the verb is consulted LAST, so " +
                 "nothing it does can displace a transition the switcher would have made.")]
        [SerializeField] private bool _interactVerb = true;
        [Tooltip("Full width, in degrees, of the forward arc used for candidates that require facing — " +
                 "180 means the half-plane you are looking at. Only candidates that ask for facing are " +
                 "affected; a fixture you operate by standing at it ignores this entirely.")]
        [SerializeField, Range(0f, 360f)] private float _interactArcDegrees =
            InteractResolver.DefaultFacingArcDegrees;

        public ControlMode Mode { get; private set; } = ControlMode.OnFoot;

        private Text _hint;

        private Transform Player => _playerWalk != null ? _playerWalk.transform : null;
        private Transform Boat   => _boatController != null ? _boatController.transform : null;

        /// <summary>The boat's rope/mooring — the explicit wired reference, else auto-resolved off the boat
        /// (so the greybox builder and existing Configure() call sites need no change). Null only if the boat
        /// has no <see cref="BoatMooring"/> at all (the mechanic self-disables there).</summary>
        private BoatMooring Mooring
        {
            get
            {
                if (_mooring == null && _boatController != null)
                    _mooring = _boatController.GetComponent<BoatMooring>();
                return _mooring;
            }
        }

        /// <summary>The player's deck-walk controller — the explicit wired reference, else auto-resolved
        /// off the walk controller's object (so the builder and existing Configure() call sites need no
        /// change). Null if the player has no <see cref="DeckWalkController"/> (deck walking self-disables;
        /// the mode machine still works — tests position the player directly).</summary>
        private DeckWalkController DeckWalk
        {
            get
            {
                if (_deckWalk == null && _playerWalk != null)
                    _deckWalk = _playerWalk.GetComponent<DeckWalkController>();
                return _deckWalk;
            }
        }

        /// <summary>The player's deck rider — the explicit wired reference, else auto-resolved off the walk
        /// controller's object (so the builder and existing Configure() call sites need no change). Null on
        /// a rig that has none, where the pre-rider drawing rule stands.</summary>
        private DeckRiderVisual DeckRider
        {
            get
            {
                if (_deckRider == null && _playerWalk != null)
                    _deckRider = _playerWalk.GetComponent<DeckRiderVisual>();
                return _deckRider;
            }
        }

        // ---- zone tests (testable via positioned transforms) --------------------------------

        /// <summary>True when the boat is within the dock-landing radius (used only to pick the tidy
        /// dock-plank landing on disembark — boarding no longer depends on it).</summary>
        public bool InDockZone()
            => Boat != null && _dockZone != null
               && Vector2.Distance(Boat.position, _dockZone.position) <= _zoneRadius;

        /// <summary>
        /// True when the on-foot player is close enough to the boat to BOARD it — board from anywhere within
        /// reach, not only at a dock zone (the relaxed board gate). Pure proximity to the boat.
        /// </summary>
        public bool WithinBoardReach()
            => Player != null && Boat != null
               && Vector2.Distance(Player.position, Boat.position) <= _boardReach;

        /// <summary>The helm station's world position — the boat's position plus the tunable world-axis
        /// helm offset (the tiller). World-aligned to match the screen-aligned boat picture.</summary>
        public Vector3 HelmWorldPosition
            => Boat != null
               ? Boat.position + new Vector3(_helmLocalOffset.x, _helmLocalOffset.y, 0f)
               : Vector3.zero;

        /// <summary>True when the player stands close enough to the helm spot for E to take the helm
        /// (pure proximity; the mode dispatch decides when it applies).</summary>
        public bool WithinHelmReach()
            => Player != null && Boat != null
               && Vector2.Distance(Player.position, HelmWorldPosition) <= _helmReach;

        /// <summary>
        /// Pure test: is the step-off point over standable LAND by tidal terrain — i.e. the ground is EXPOSED
        /// (at or above the water line, <c>waterDepth ≤ 0</c>)? This is the tightened rule (owner playtest):
        /// merely-shallow but still-submerged water (<c>0 &lt; depth</c>) is NOT land, so you can't step off
        /// onto water. Reads the SAME deterministic depth (water level − authored ground) the boat-cross gate
        /// uses. Open water (no terrain wired) reads <see cref="float.PositiveInfinity"/> → never land. Pure +
        /// static so the disembark-only-on-land rule is EditMode-testable with a fake terrain/environment.
        /// </summary>
        public static bool IsStandableLandByDepth(float waterDepth)
            => waterDepth <= 0f;

        /// <summary>
        /// True when the boat sits over standable land right now, so the player may step off here (the
        /// disembark gate; not only at the dock). Two independent tells, either suffices (P5 forgiving) — but
        /// BOTH require actual land, never open/submerged water:
        ///   • the authored tidal terrain is EXPOSED under the boat (water depth ≤ 0 — a walkable flat/bar,
        ///     via the deterministic <see cref="BoatCrossing.DepthAt"/>; open water = infinite = not land);
        ///   • a physical land/shore collider sits within LandProbeRadius on the land mask (an empty mask
        ///     skips this test). The cove's hard shore-edge is found this way; St Peters' bared flats by depth.
        /// </summary>
        public bool OnLand()
        {
            if (Boat == null) return false;

            // 1) Exposed tidal terrain — the ground is bared (deterministic, the boat-cross gate's depth read).
            float depth = BoatCrossing.DepthAt(GameServices.TidalTerrain, GameServices.Environment,
                                               GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0,
                                               Boat.position);
            if (!float.IsPositiveInfinity(depth) && IsStandableLandByDepth(depth))
                return true;

            // 2) A physical land/shore collider close by (only when a mask is set; 0 = skip).
            if (_landMask.value != 0
                && Physics2D.OverlapCircle(Boat.position, _landProbeRadius, _landMask) != null)
                return true;

            return false;
        }

        /// <summary>
        /// The damaged-dory boarding gate (St Peters opening, P5): a boat bought DAMAGED is owned but
        /// unusable until the player pays the shipwright to repair it. We read the single source of truth —
        /// <see cref="RepairLedger.IsRepaired"/> over the live <see cref="SaveData"/> — so the switcher and
        /// the shipwright can't disagree about "is she ready to sail?". Economy owns that economy-state and
        /// raises <see cref="BoatRepaired"/> when paid; this reads it off the Core save seam (the owner of
        /// the rule, the Economy lane, explicitly delegated this boarding read to gameplay-systems).
        ///
        /// <para><b>Fail-safe defaults.</b> When there's NO save wired (EditMode, pre-bootstrap) or no
        /// active hull id, this returns <c>true</c> — the gate self-disables so it never blocks the
        /// ordinary already-usable boat or breaks tests. A boat only becomes UN-boardable when a save is
        /// present AND that specific hull id is recorded owned-but-not-yet-repaired. (A non-damaged boat is
        /// marked repaired on purchase, so it boards immediately.)</para>
        /// </summary>
        public bool BoardableNow()
        {
            var save = GameServices.Save?.Current;
            if (save == null) return true;                       // no save → don't gate (tests / pre-boot)

            string hullId = _boatController != null && _boatController.Hull != null
                ? _boatController.Hull.Id : null;
            if (string.IsNullOrEmpty(hullId)) return true;       // no identifiable hull → don't gate

            // Only gate a hull we actually OWN as damaged: an owned hull that isn't yet repaired is the
            // damaged dory awaiting the shipwright. An un-owned/unknown hull (the greybox start dory before
            // any purchase flow) isn't part of the buy+repair gate, so it stays boardable.
            bool owned = save.OwnedBoats != null && save.OwnedBoats.Contains(hullId);
            if (!owned) return true;

            return RepairLedger.IsRepaired(save, hullId);
        }

        /// <summary>True if INTERACT would transition right now.
        /// <para><b>On foot</b>: you may board (→ the DECK) whenever within reach of the boat (anywhere)
        /// and the boat is boardable (a damaged dory blocks boarding until repaired).</para>
        /// <para><b>On deck</b>: E is contextual — at the helm spot it takes the helm; elsewhere it
        /// disembarks, allowed only onto a standable step-off: at an authored DOCK/wharf (you step onto
        /// the planks) OR where the boat is over standable LAND (<see cref="OnLand"/>) — NEVER over open
        /// or merely-shallow-but-submerged water (owner playtest: you couldn't step off onto water).</para>
        /// <para><b>At the helm</b>: you may always step back onto the deck.</para></summary>
        public bool CanInteract() => Mode switch
        {
            ControlMode.OnFoot => WithinBoardReach() && BoardableNow(),
            ControlMode.OnDeck => WithinHelmReach() || CanStepAshore(),
            _ => true,   // at the helm → step back onto the deck, always allowed
        };

        /// <summary>Is there a standable step-off from the deck right now — the authored dock/wharf, or
        /// land under the boat? Named because three places must agree on it: the prompt, the transition,
        /// and the boarding move re-reading the rule at the far end of its arc.</summary>
        public bool CanStepAshore() => InDockZone() || OnLand();

        /// <summary>Attempt the contextual INTERACT transition for the current mode (board the deck /
        /// take the helm / step back / disembark). Returns true if a transition happened.</summary>
        public bool TryInteract()
        {
            switch (Mode)
            {
                case ControlMode.OnFoot:
                    if (!(WithinBoardReach() && BoardableNow())) return false;
                    BoardDeck();
                    return true;

                case ControlMode.OnDeck:
                    // The helm is a STATION: standing at it, E takes the helm; elsewhere on the deck,
                    // E steps ashore (when a standable step-off is there).
                    if (WithinHelmReach()) { TakeHelm(); return true; }
                    if (CanStepAshore()) { Disembark(); return true; }
                    return false;

                default: // Aboard (at the helm) → step back onto the deck
                    LeaveHelm();
                    return true;
            }
        }

        /// <summary>
        /// <b>The INTERACT key's entry point — E, as the player presses it.</b> Where
        /// <see cref="TryInteract"/> is the transition itself (instant, and still exactly that for every
        /// caller it has ever had), this is the same verb played as a MOVE: boarding and stepping ashore
        /// send the fisher walking to the rail and over it, and make the transition when they land.
        ///
        /// <para><b>It decides nothing the state machine does not.</b> The gates are the very same
        /// predicates the instant path reads, and everything the move cannot present — taking or leaving
        /// the helm (steps WITHIN the boat, not a boarding), a rig with no boat or no player, the move
        /// switched off — falls straight through to <see cref="TryInteract"/> and behaves as it always
        /// did. So there is one set of rules about when you may board, not two.</para>
        /// </summary>
        /// <returns>True if a move started or a transition happened.</returns>
        public bool BeginInteract()
        {
            if (IsBoardingMove) return false;          // a move in flight owns the verb until it lands

            if (Mode == ControlMode.OnFoot && WithinBoardReach() && BoardableNow()
                && BeginBoardingMove(BoardingMoveKind.Boarding)) return true;

            if (Mode == ControlMode.OnDeck && !WithinHelmReach() && CanStepAshore()
                && BeginBoardingMove(BoardingMoveKind.Disembarking)) return true;

            if (TryInteract()) return true;

            // ...and only then, the registry (M2-39). See TryInteractCandidate for why LAST.
            return TryInteractCandidate();
        }

        // ---- the one interact VERB (M2-39) --------------------------------------------------

        /// <summary>
        /// Hand the press to the nearest/highest-priority registered <see cref="IInteractable"/>, if there
        /// is one. This is where the M2-39 seam actually meets a key.
        ///
        /// <para><b>Why the verb is consulted LAST, after every branch above.</b> The alternative — resolve
        /// candidates first — is more expressive and it is what the seam should eventually become, but it
        /// can silently take the press away from boarding, taking the helm, or stepping ashore, and those
        /// are the three things the player cannot afford to lose. Consulting last makes the change
        /// provably non-regressive: <b>every</b> transition the switcher would have made, it still makes,
        /// on the same press, before the registry is even looked at. What the verb gets is the presses
        /// that used to do nothing at all.</para>
        ///
        /// <para><b>The cost of that choice, stated plainly.</b> Standing at a registered candidate with a
        /// boardable boat inside <see cref="_boardReach"/>, E boards rather than working the candidate —
        /// step a metre off the boat and it is yours again. That is legible, but it is a policy call and
        /// not obviously the right one for a thing lying at your feet; the correct end state is that
        /// boarding registers as a candidate too and the resolver arbitrates it against the rest by
        /// distance, priority and FACING, which is what a facing-aware verb is for. Flagged for
        /// lead-architect: <see cref="InteractPriority"/> already carries the ladder that ordering would
        /// need, and the switcher's own reach/priority would be its rung.</para>
        ///
        /// <para><b>At the helm, nothing can resolve.</b> <see cref="TryInteract"/>'s default arm always
        /// returns true (E at the helm steps back onto the deck, unconditionally), so this is unreachable
        /// in <see cref="ControlMode.Aboard"/>. <see cref="InteractContext.AtHelm"/> exists in the contract
        /// so the enum stays append-only when that changes; today a candidate declaring it can never be
        /// reached and should not expect to be.</para>
        /// </summary>
        private bool TryInteractCandidate()
            => _interactVerb && InteractVerb.TryPerform(ActorNow(), _interactArcDegrees);

        /// <summary>
        /// The actor, as Core sees it: where the player is standing, which way they face, and which of the
        /// three places they are in.
        ///
        /// <para><b>Facing is reported ONLY on foot, and that is deliberate.</b> The four-way facing lives
        /// on <see cref="PlayerWalkController"/>, which is disabled while aboard — on deck the fisher is
        /// driven by <see cref="DeckWalkController"/>, which carries no facing at all. Rather than hand
        /// back a stale heading from a disabled component (which would make deck candidates unreachable
        /// from whichever direction the fisher happened to be facing when she boarded), we report
        /// <see cref="Vector2.zero"/> — the contract's "unknown", under which the arc test is skipped and
        /// every direction is forward. Deck candidates therefore behave as proximity-only until the deck
        /// walker grows a facing; that is honest, and it fails open rather than closed.</para>
        /// </summary>
        private InteractActor ActorNow()
        {
            Transform p = Player;
            Vector2 pos = p != null ? (Vector2)p.position : Vector2.zero;
            Vector2 facing = _playerWalk != null && Mode == ControlMode.OnFoot
                ? PlayerWalkController.FacingUnitVector(_playerWalk.CurrentFacing)
                : Vector2.zero;
            return InteractActor.For(pos, facing, Mode);
        }

        // ---- rope: hold / root (the mooring mechanic — the owner's refinement) --------------

        /// <summary>
        /// True when the on-foot player may hold/root the rope right now: on foot, the boat has a mooring,
        /// it's actually moored (held or rooted), and the player stands within <see cref="_moorReach"/> of
        /// it. (Aboard — deck or helm — the rope is stowed; boarding stows it.)
        /// </summary>
        public bool CanToggleMooring()
            => Mode == ControlMode.OnFoot
               && Mooring != null && Mooring.IsMoored
               && Player != null && Boat != null
               && Vector2.Distance(Player.position, Boat.position) <= _moorReach;

        /// <summary>
        /// Toggle HOLD ⇄ ROOT the rope (the root key). Held → root the line at the player's feet (the boat
        /// tethers to that fixed spot; the player is free to roam); Rooted → take the line back in hand (the
        /// boat follows the player again). No-op unless <see cref="CanToggleMooring"/>. Returns true if it
        /// toggled.
        /// </summary>
        public bool ToggleMooring()
        {
            if (!CanToggleMooring()) return false;
            Mooring.ToggleRoot(Player.position, Player);   // root at the player's feet, or take back in hand
            return true;
        }

        // ---- transitions --------------------------------------------------------------------

        /// <summary>OnFoot → OnDeck: step aboard onto the DECK (owner's Build-5 model — boarding never
        /// takes the helm). The player stays visible and walks the deck; the boat stays un-driven (its
        /// controller/input off) and rocks/drifts under them; the rope is stowed while anyone's aboard.</summary>
        private void BoardDeck()
        {
            if (Mooring != null) Mooring.Stow();                     // the rope's stowed while you're aboard
            if (_boatController != null) _boatController.enabled = false;   // nobody at the helm yet
            if (_boatInput != null) _boatInput.enabled = false;

            ApplyPlayerFor(ControlMode.OnDeck);
            SnapPlayerToDeck(_boardLocalOffset);
            Mode = ControlMode.OnDeck;

            // Camera: retarget stays on the (visible, deck-walking) player at the on-foot framing —
            // the boat framing arrives only when the helm is taken (ActiveBoatChanged there).
            EventBus.Publish(new ControlModeChanged(ControlMode.OnDeck));
        }

        /// <summary>OnDeck → Aboard: take the HELM station. The player is seated exactly ON the helm spot
        /// and DRAWN there in the hull's own piloting stance (the deck rider) rather than hidden; steering
        /// input goes live.</summary>
        private void TakeHelm()
        {
            // Stand ON the helm, not merely within reach of it. E fires anywhere inside _helmReach, so a
            // figure left where the player happened to be standing would be drawn up to that far off the
            // tiller — visible slop the hidden sprite used to conceal. Seated BEFORE the mode applies, so
            // the rider's first frame already has them in place.
            SnapPlayerToDeck(_helmLocalOffset);
            ApplyPlayerFor(ControlMode.Aboard);
            if (_boatController != null) _boatController.enabled = true;
            if (_boatInput != null) _boatInput.enabled = true;
            Mode = ControlMode.Aboard;

            // Camera: zoom to the boat's framing (#17 ActiveBoatChanged) + retarget to it (ControlModeChanged).
            BoatHullDef hull = _boatController != null ? _boatController.Hull : null;
            float height = hull != null ? hull.CameraWorldHeightMeters : 14f;
            float hullLength = hull != null ? hull.LengthMeters : 0f;   // the camera floors the framing by it (§9.8)
            string id = hull != null ? hull.Id : null;
            EventBus.Publish(new ActiveBoatChanged(id, height, hullLength));
            EventBus.Publish(new ControlModeChanged(ControlMode.Aboard));
        }

        /// <summary>Aboard → OnDeck: step back from the helm onto the deck. Steering goes dead and the
        /// boat is brought to rest (throttle dropped — nobody's at the tiller); the player reappears at
        /// the helm spot and can walk the deck / work the gear / step ashore.</summary>
        private void LeaveHelm()
        {
            if (_boatController != null) { _boatController.enabled = false; _boatController.Stop(); }
            if (_boatInput != null) _boatInput.enabled = false;

            ApplyPlayerFor(ControlMode.OnDeck);
            SnapPlayerToDeck(_helmLocalOffset);                      // you step back from the tiller
            Mode = ControlMode.OnDeck;
            EventBus.Publish(new ControlModeChanged(ControlMode.OnDeck));
        }

        /// <summary>OnDeck → OnFoot: step ashore (the same standable-step-off rules as ever, now sourced
        /// from the deck state).</summary>
        private void Disembark()
        {
            // Drop the helm. The player steps off onto land and HOLDS the rope (the boat is tethered to the
            // player's hand and trails them on the leash). Press the root key to drop the line to the ground
            // (the boat tethers there; the player roams free). The boat always drifts on wind+tide on its
            // current tether (held or rooted) via the firm/slack rope physics.
            if (_boatController != null) _boatController.enabled = false;
            if (_boatInput != null) _boatInput.enabled = false;

            // Place the on-foot player: a tidy landing on the dock planks when disembarking at the dock,
            // otherwise step off right where the boat is (onto the nearby land/flats) so disembark-on-land
            // doesn't teleport you back to a far dock. Null-safe (tests / no disembark point wired), and
            // the same rule the boarding move arcs to — one source, so the landing can't pop.
            if (Player != null && TryDisembarkLanding(out Vector3 landing)) Player.position = landing;

            ApplyPlayerFor(ControlMode.OnFoot);
            Mode = ControlMode.OnFoot;

            // HOLD the rope (the owner's refinement): on disembark the player takes the line in hand, so the
            // boat is tethered to the player and follows on the leash. If the boat has no mooring component
            // the mechanic self-disables and we fall back to a bare stop (still parks).
            if (Mooring != null && Player != null) Mooring.Hold(Player);
            else if (_boatController != null) _boatController.Stop();

            // Camera: retarget to the player + reframe to the on-foot view (CameraFollow owns the value).
            EventBus.Publish(new ControlModeChanged(ControlMode.OnFoot));
        }

        /// <summary>Where stepping ashore PUTS you — the tidy landing on the dock planks when the boat is
        /// in the dock zone and a disembark point is wired, otherwise straight off the boat onto the land
        /// she is sitting over. Factored out because the boarding move has to arc the fisher to the very
        /// spot <see cref="Disembark"/> will place them at, and a second copy of this rule would put a pop
        /// at the end of every landing the moment the two drifted.</summary>
        /// <returns>False when there is nowhere to place them (no dock point AND no boat) — the same case
        /// in which <see cref="Disembark"/> leaves the player exactly where they stand.</returns>
        private bool TryDisembarkLanding(out Vector3 world)
        {
            if (InDockZone() && _disembarkPoint != null) { world = _disembarkPoint.position; return true; }
            if (Boat != null) { world = Boat.position; return true; }
            world = Player != null ? Player.position : Vector3.zero;
            return false;
        }

        // ---- the boarding MOVE (the owner's ask: climb aboard, don't teleport) ----------------
        //
        // WHAT IT IS. E used to reposition you instantly; now the fisher walks to the boat's rail, steps
        // up and over it, and lands where BoardDeck() has always seated them. Stepping ashore is the same
        // move mirrored: cross the deck to the rail nearest the landing, then down onto the wharf.
        //
        // WHERE THE SIM FLIPS, AND WHY THAT END. ONCE, at the FAR end of the arc — the instant the
        // fisher's feet meet the surface they were moving to. The move never re-implements a transition:
        // it lands by calling the very BoardDeck() / Disembark() the instant path calls, behind the very
        // gates that path reads, so everything downstream of them is byte-identical. The far end is the
        // only honest choice because of what ControlMode.OnDeck MEANS to the things that read it — "the
        // fisher is standing on this deck and may work her gear" (PotDeckWorkController,
        // TrapHaulController, DevTrapInput) and "publish a deck stance" (DeckStance → the rod fight's
        // deck-angle term). Every one of those is false while the fisher is in the air, so flipping at the
        // near end would put the world into a state the picture flatly contradicts for the whole move.
        //
        // TryInteract() is deliberately NOT the thing called at the landing: E is contextual on POSITION,
        // and by the far end the fisher has moved (onto a small hull's deck, they can easily be standing
        // inside the helm's reach), so a re-dispatch would answer a different question than the one the
        // move set out to ask. A move finishes the transition it started, or none at all.
        //
        // THE ONE WINDOW THAT LEAVES, AND HOW IT IS SHUT. Stepping ashore is still OnDeck while the
        // fisher arcs down to the planks, so those three gear gates would stay live for the length of the
        // arc. The move therefore HOLDS InteractionGate for its duration — the Core seam that already
        // means "something else owns the interact key right now", and the very flag all three of them
        // already check (`_onDeck && !InteractionGate.IsBlocked`). DeckStance needs no such help: the deck
        // walk is disabled for the transit, which clears the stance, which is the truth — nobody in mid-air
        // is standing on a deck.
        //
        // PHYSICS. The fisher's real transform travels the arc; there is no separate visual to keep in
        // step, and no teleport hidden under an animation. That is safe because the transit shape parks
        // everything that could fight it (both walk controllers off, Rigidbody2D un-simulated, footprint
        // collider off, un-parented) — see ApplyPlayerFor's `inTransit`. It also earns the animation for
        // free: IsoCharacterSprite picks idle/walk/run from MEASURED transform speed, so a fisher moved at
        // walking pace draws walking, with no new clip and no new selection seam.
        //
        // THE HULL DOES NOT HOLD STILL. Every waypoint on the boat is stored as a boat-relative offset and
        // re-projected through her LIVE drawn heading and position each tick, so a rocking, turning,
        // drifting hull carries the landing spot with her. Nothing here captures a world point off the boat.

        private enum BoardingMoveKind { None = 0, Boarding, Disembarking }

        /// <summary>How this move crosses the last stretch: over the rail in one step, or down a wharf
        /// ladder because the tide has opened a gap a step cannot honestly cross.</summary>
        private enum BoardingRoute { Step = 0, Ladder }

        private BoardingMoveKind _moveKind = BoardingMoveKind.None;
        private float _moveElapsed;
        private float _moveApproachSeconds;
        // The move's one WORLD-anchored end — the shore end, whichever end of the arc that is. Boarding:
        // where the fisher stood when they pressed E (leg 1's start, and where a refused move puts them
        // back). Stepping ashore: where they will land (leg 2's finish). The other two waypoints are on
        // the boat and are stored as boat-relative offsets, because the boat does not hold still.
        private Vector3 _moveShoreWorld;
        private Vector2 _moveDeckRelative;     // where they stood ON the deck (stepping ashore: leg 1's start)
        private Vector2 _moveRailRelative;     // the rail, as a boat-relative offset re-clamped every tick
        private bool _moveWashboards;          // does this hull carry side decks to step over?
        private bool _moveHeldGate;            // did WE raise InteractionGate? (only we may lower it)
        private bool _moveClipPlaying;         // is a move clip on the renderer right now?
        private CharacterClip _moveClipAsked;  // which clip THIS leg asked for (ask once per leg)
        private bool _moveClipAttempted;       // has the current leg already asked? (ask once)
        private CharacterClipPlayer _clipPlayer;
        private bool _clipPlayerResolved;

        // ---- the LADDER route's own state (see the ladder-boarding block below) --------------------
        private BoardingRoute _moveRoute = BoardingRoute.Step;
        private IBoardingLadder _moveLadder;
        private Vector3 _moveLadderSeat;       // world PLAN point the climber occupies while on the rungs
        private float _moveGapMetres;          // the tide gap this climb crosses (m), measured at the start
        private float _moveClimbSeconds;       // how long the rungs themselves take (gap ÷ stepZ × loop)
        private float _moveTransitionSeconds;  // the unauthored turn-around / step-off cover, each end
        private float _moveLegTwoSeconds;      // leg 2 in total: a vault, or turn + climb + step-off

        /// <summary>True while the fisher is mid-boarding — walking to the rail, over it, or down onto the
        /// wharf. The mode has NOT changed yet; it changes when they land.</summary>
        public bool IsBoardingMove => _moveKind != BoardingMoveKind.None;

        /// <summary>How far through the move, 0→1 (0 when none is running). For tests and tooling.</summary>
        public float BoardingMoveProgress
        {
            get
            {
                if (_moveKind == BoardingMoveKind.None) return 0f;
                float total = _moveApproachSeconds + Mathf.Max(0.01f, _moveLegTwoSeconds);
                return total > 1e-4f ? Mathf.Clamp01(_moveElapsed / total) : 1f;
            }
        }

        /// <summary>
        /// Start the move. Returns false — so the caller falls back to the instant transition — whenever
        /// the move cannot honestly be presented: switched off, no player or boat, or a hull the deck walk
        /// cannot place anyone on. The GATES are the caller's business and have already been read; this
        /// only decides whether the transition is played or made.
        /// </summary>
        private bool BeginBoardingMove(BoardingMoveKind kind)
        {
            if (!_boardingMove || kind == BoardingMoveKind.None) return false;
            if (Player == null || Boat == null) return false;

            var deck = DeckWalk;
            if (deck == null) return false;               // no deck walk → no rail to find; snap as before

            // Bind (inert while disabled) so the deck can be asked where this hull's rail and seat are.
            deck.Bind(Boat);
            _moveWashboards = deck.HullHasWashboards();

            Vector3 boatPos = Boat.position;
            Vector3 playerPos = Player.position;
            _moveKind = kind;
            _moveElapsed = 0f;
            _moveDeckRelative = (Vector2)(playerPos - boatPos);

            // The RAIL: for boarding, the point on her outline nearest where the fisher stands; for
            // stepping ashore, the point nearest where they are headed. Stored as the raw boat-relative
            // offset and clamped afresh every tick, so the rail stays on the hull as she moves.
            if (kind == BoardingMoveKind.Boarding)
            {
                _moveShoreWorld = playerPos;              // leg 1 starts here (world-anchored: the wharf)
                _moveRailRelative = _moveDeckRelative;
            }
            else
            {
                TryDisembarkLanding(out Vector3 landing);
                _moveShoreWorld = landing;                // leg 2 ends here (world-anchored: the wharf)
                _moveRailRelative = (Vector2)(landing - boatPos);
            }

            // WHICH ROUTE? Read the tide gap and look for a ladder BEFORE the approach is timed, because
            // the ladder head is somewhere else on the wharf and leg 1 has to walk to whichever it is.
            ChooseBoardingRoute();

            // The approach is a WALK, so its length is a distance at a walking pace — not a fixed
            // duration that would slide a distant fisher and dawdle a close one.
            float approachDistance = Vector3.Distance(ApproachStartWorld(), LegTwoStartWorld());
            _moveApproachSeconds = Mathf.Clamp(approachDistance / Mathf.Max(0.1f, _boardApproachSpeed),
                                               0f, Mathf.Max(0f, _boardApproachMaxSeconds));

            // Leg 2's budget. A step is the owner's tuned vault; a climb is two covers for the kit's
            // unauthored ends plus however many rungs the tide has put between the deck and the planks.
            _moveLegTwoSeconds = _moveRoute == BoardingRoute.Ladder
                ? _moveTransitionSeconds + _moveClimbSeconds + _moveTransitionSeconds
                : Mathf.Max(0.01f, _boardVaultSeconds);

            // The fisher is now a free body in the air: nothing drives them but this move.
            ApplyPlayerFor(Mode, inTransit: true);
            HoldInteractionGate();
            return true;
        }

        /// <summary>
        /// Advance an in-flight move. The Update pump calls this with <c>Time.deltaTime</c>; tests call it
        /// directly, because a PlayMode frame count is not time and a move measured in seconds must be
        /// driven in seconds. Harmless when no move is running.
        /// </summary>
        public void TickBoardingMove(float deltaTime)
        {
            if (_moveKind == BoardingMoveKind.None) return;

            // The gates are law, mid-arc as much as at the key-press: a hull that stops being boardable
            // (or stops existing) under a fisher in mid-air does not get boarded. Nothing has flipped yet,
            // so calling it off costs nothing but putting them back where they started.
            if (_moveKind == BoardingMoveKind.Boarding && (Boat == null || !BoardableNow()))
            {
                CancelBoardingMove(restorePosition: true);
                return;
            }

            _moveElapsed += Mathf.Max(0f, deltaTime);
            float legTwo = Mathf.Max(0.01f, _moveLegTwoSeconds);

            if (_moveElapsed >= _moveApproachSeconds + legTwo)
            {
                FinishBoardingMove();
                return;
            }

            if (Player == null) { CancelBoardingMove(restorePosition: true); return; }

            Vector3 start = LegTwoStartWorld();
            if (_moveElapsed < _moveApproachSeconds)
            {
                // LEG 1 — the approach: ashore to the rail (or to the LADDER HEAD), or across the deck to
                // it. Linear, because a walk is a walk; easing it would read as a drift.
                float u = _moveApproachSeconds > 1e-4f ? _moveElapsed / _moveApproachSeconds : 1f;
                Player.position = Vector3.Lerp(ApproachStartWorld(), start, u);
                return;
            }

            float legTwoElapsed = _moveElapsed - _moveApproachSeconds;
            if (_moveRoute == BoardingRoute.Ladder) { TickLadderLeg(legTwoElapsed, start); return; }

            // LEG 2 — the vault: over the rail onto the deck, or down off it onto the wharf. Eased out so
            // the fisher plants rather than arrives at speed, and lifted on a parabola that is zero at
            // both ends, so the arc joins the two legs with no step in position.
            float v = Mathf.Clamp01(legTwoElapsed / legTwo);
            Vector3 end = VaultEndWorld();
            Vector3 pos = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, v));
            pos.y += 4f * Mathf.Max(0f, _boardVaultHopMeters) * v * (1f - v);
            Player.position = pos;

            PlayMoveClip(_moveKind == BoardingMoveKind.Boarding ? CharacterClip.Board : CharacterClip.BoardDown,
                         HeadingBetween(start, end), legTwo, holdOnFinish: true);
        }

        // ---- the LADDER route (the tide-gap climb) ------------------------------------------------
        //
        // WHAT IT IS. A wharf deck stands still above chart datum; a boat floats. So the vertical gap
        // between the planks and her deck is TIDE-DRIVEN, and at some state of the ebb it stops being
        // something a fisher can step across. Past that the boarding move goes down the wharf ladder
        // instead: the same E, the same gates, the same landing — a different way across the last stretch.
        // At Nine Mile Creek (+3.0 m deck, 2.2 m amplitude, a dory's 0.55 m sheer) that is a step aboard
        // around high water and a climb for the bottom of the tide, which is P1 made physical.
        //
        // WHERE THE THRESHOLD COMES FROM. GameConfig.LadderBoarding.BoardClampMetres — owner data, not a
        // literal here. The kit states two different numbers for it and they measure different things; the
        // field's own tooltip carries that argument and the one-number way to change sides.
        //
        // NOTHING IS SAVED, AND NOTHING IS CACHED. The gap is read once, at the key-press, from the
        // deterministic water level (recomputed from (worldSeed, gameTime) — rule 5) and the wharf's fixed
        // deck. Read ONCE rather than per tick deliberately: a climb whose length changed under the
        // climber because the tide moved a centimetre mid-descent would be a rung appearing beneath a
        // planted foot. The tide decides whether you climb and how far; it does not get to redecide.
        //
        // ⚠ THE BODY DOES NOT SINK AT A CONSTANT RATE. The descent is a two-step STAIR — a rung eased
        // through each foot swing, flat while both feet are planted — and driving it linearly slides the
        // soles by up to 2.7 px at 32 px = 1 m. LadderBoardingMath.DescendMetresAt is the rig's own curve,
        // pinned frame-by-frame against the shipped sidecar. The clip is played at its BAKED rate and the
        // height is driven off the SAME phase, so picture and position cannot drift apart.
        //
        // ⚠ TWO ENDS THE KIT DOES NOT AUTHOR. There is no clip for the turn-around at the top (swinging
        // off the wharf edge onto the top rung) and none for the step off at the bottom onto a moving
        // gunwale. The kit names board/boardDown or a hard cut as the covers, and says the turn-around is
        // the one players notice — so both get the authored step rather than a cut: boardDown for the end
        // that steps DOWN, board for the end that steps UP, each scaled to TransitionSeconds. And there is
        // no ladderUp at all, so going up reuses this same descent stair sign-flipped — see
        // LadderBoardingMath.TravelMetresAt for why the rung quantization is the half that carries over.

        /// <summary>True while the fisher is on the rungs rather than stepping over a rail. For tests and
        /// tooling; the mode has not changed either way until they land.</summary>
        public bool IsLadderBoarding => _moveKind != BoardingMoveKind.None && _moveRoute == BoardingRoute.Ladder;

        /// <summary>The tide gap (m) the running move is crossing — 0 when it is a plain step.</summary>
        public float LadderGapMetres => _moveRoute == BoardingRoute.Ladder ? _moveGapMetres : 0f;

        /// <summary>
        /// Decide whether this move takes the ladder, and if so measure the climb. Sets
        /// <see cref="_moveRoute"/> and, for a ladder, the seat, the gap and the timings.
        ///
        /// <para>Falls back to the step for every "no" there is — the feature off, no ladder within reach,
        /// no boat, a gap inside the clamp, or a deck that rides ABOVE the planks (which is a step up, and
        /// the <c>board</c> clip's own business). A wharf with no ladder is a valid wharf: the fisher
        /// steps across a gap that is too big for it, exactly as they do on <c>main</c> today.</para>
        /// </summary>
        private void ChooseBoardingRoute()
        {
            _moveRoute = BoardingRoute.Step;
            _moveLadder = null;
            _moveGapMetres = 0f;
            _moveClimbSeconds = 0f;

            LadderBoardingSettings cfg = GameServices.LadderBoarding;
            _moveTransitionSeconds = Mathf.Max(0f, cfg.TransitionSeconds);
            if (Boat == null) return;

            // Where the fisher is trying to get across, in plan. _moveShoreWorld is the WHARF end of the
            // arc whichever way the move runs (see its own field doc), and the wharf is the side a ladder
            // is bolted to — so one read serves boarding and stepping ashore alike.
            if (!BoardingLadders.TryFindNearestNow((Vector2)_moveShoreWorld, cfg.LadderReachMetres,
                                                   out IBoardingLadder ladder))
                return;

            float gap = LadderBoardingMath.VerticalGap(ladder.TopElevationMeters, BoatDeckElevationNow());
            if (!LadderBoardingMath.NeedsLadder(gap, cfg.BoardClampMetres)) return;

            _moveRoute = BoardingRoute.Ladder;
            _moveLadder = ladder;
            _moveGapMetres = gap;
            // ClimbLoopSecondsNow(), never the raw field: a config asset that OMITTED the key deserializes
            // it to 0, and a zero loop would make an eight-rung climb instantaneous with nothing failing.
            _moveClimbSeconds = LadderBoardingMath.ClimbSeconds(gap, ladder.RungMetres, ClimbLoopSecondsNow());

            // WHERE THE CLIMBER STANDS. Not on the ladder line — the rig's pose was authored with the body
            // a MEASURED standoff off the plane, so seating the sprite on the stringers puts its hands
            // inside the wall. Never eyeballed (#454's contract states this outright).
            Vector2 seat = ladder.WorldPosition
                         + LadderBoardingMath.SeatOffset(ladder.FaceHeadingDegrees, ladder.StandoffMetres);
            _moveLadderSeat = new Vector3(seat.x, seat.y, 0f);
        }

        /// <summary>
        /// How high the boat's deck stands above chart datum right now: the deterministic water level plus
        /// how high that deck rides above her own floating waterline.
        ///
        /// <para>Composed from <see cref="LadderBoardingMath.BoatDeckElevation"/> — i.e. from the very
        /// <c>MooringLineMath</c> the ropes use — rather than re-derived, so the ladder and the mooring
        /// lines can never disagree about where the same deck is. With no environment service wired
        /// (EditMode, pre-bootstrap) the water reads 0 and the deck sits at its own freeboard above datum:
        /// the established gate-off shape, not a special case.</para>
        /// </summary>
        private float BoatDeckElevationNow()
        {
            var deck = DeckWalk;
            float deckHeight = deck != null ? deck.DeckHeightMeters : 0f;
            // _boatController, not Boat — Boat is the hull's TRANSFORM, and a draught is not a transform's
            // to know.
            float draught = _boatController != null && _boatController.Hull != null
                ? _boatController.Hull.DraughtMeters
                : 0f;

            IEnvironmentService env = GameServices.Environment;
            IGameClock clock = GameServices.Clock;
            float water = env != null ? env.WaterLevelAt(clock != null ? clock.TotalSeconds : 0.0) : 0f;

            return LadderBoardingMath.BoatDeckElevation(water, deckHeight, draught);
        }

        /// <summary>
        /// Leg 2 on the ladder route: the turn-around, the rungs, and the step off — three sub-legs on one
        /// clock. Position only; the transition itself still happens at
        /// <see cref="FinishBoardingMove"/> behind the same gates as ever.
        /// </summary>
        private void TickLadderLeg(float legTwoElapsed, Vector3 climbStart)
        {
            bool descending = _moveKind == BoardingMoveKind.Boarding;
            float trans = _moveTransitionSeconds;
            Vector3 climbEnd = LadderRungsEndWorld();
            Vector3 end = VaultEndWorld();
            float climberHeading = LadderBoardingMath.ClimberHeadingFor(FaceHeadingOfMoveLadder());

            // (a) GETTING ONTO THE RUNGS — the kit's first unauthored end, covered with the authored step.
            // IN PLACE: leg 1 has already walked the fisher to the end of the ladder they start from, and
            // what is missing is the turn of the body onto it, not a translation. Descending that is the
            // turn-around at the top (boardDown — swinging off the lip onto the first rung); coming up it
            // is the step from the gunwale onto the bottom rung (board — a step UP).
            if (legTwoElapsed < trans)
            {
                Player.position = climbStart;
                PlayMoveClip(descending ? CharacterClip.BoardDown : CharacterClip.Board,
                             climberHeading, trans, holdOnFinish: true);
                return;
            }

            // (b) THE RUNGS. The clip runs at its BAKED rate and the height comes off the SAME phase, so a
            // frame of picture and a metre of travel can never drift apart. Plan position is fixed: you
            // are on a ladder, you do not travel across while you are on it. Clamped to the gap so the
            // last part-rung cannot carry the fisher past the deck she is climbing to.
            float climbElapsed = legTwoElapsed - trans;
            if (climbElapsed < _moveClimbSeconds)
            {
                double phase = LadderBoardingMath.PhaseAt(climbElapsed, ClimbLoopSecondsNow());
                float travelled = LadderBoardingMath.TravelMetresAt(
                    phase, RungOfMoveLadder(), LadderBoardingMath.RigSwingFraction,
                    LadderBoardingMath.RigLeadFootPhase, LadderBoardingMath.RigTrailFootPhase,
                    _moveGapMetres, ascending: !descending);

                Player.position = climbStart + new Vector3(0f, travelled, 0f);

                // It LOOPS — played once, left to run, and Stop()ped when the move ends (StopVaultClip).
                // scaleToSeconds 0: a ladder is not a fixed-length move, so the clip keeps its baked rate
                // and the climb simply takes as many loops as the tide has put rungs between the two decks.
                PlayMoveClip(CharacterClip.LadderDown, climberHeading,
                             scaleToSeconds: 0f, holdOnFinish: false);
                return;
            }

            // (c) GETTING OFF THEM — the kit's other unauthored end. Descending, that is the step down onto
            // a gunwale that is NOT holding still, so the destination is re-read every tick; coming up it
            // is the heave over the lip onto the planks.
            float offElapsed = climbElapsed - _moveClimbSeconds;
            float u = trans > 1e-4f ? Mathf.Clamp01(offElapsed / trans) : 1f;
            Player.position = Vector3.Lerp(climbEnd, end, Mathf.SmoothStep(0f, 1f, u));
            PlayMoveClip(descending ? CharacterClip.BoardDown : CharacterClip.Board,
                         HeadingBetween(climbEnd, end), trans, holdOnFinish: true);
        }

        /// <summary>Where the RUNGS end — the bottom of the climb going down, the top of it coming up. The
        /// mirror of <see cref="LegTwoStartWorld"/>'s ladder answer.</summary>
        private Vector3 LadderRungsEndWorld()
            => _moveKind == BoardingMoveKind.Boarding
                ? _moveLadderSeat - new Vector3(0f, _moveGapMetres, 0f)
                : _moveLadderSeat;

        private float FaceHeadingOfMoveLadder() => _moveLadder != null ? _moveLadder.FaceHeadingDegrees : 180f;

        private float RungOfMoveLadder()
            => _moveLadder != null ? _moveLadder.RungMetres : LadderBoardingMath.RigRungMetres;

        private static float ClimbLoopSecondsNow()
        {
            float loop = GameServices.LadderBoarding.ClimbLoopSeconds;
            return loop > 1e-4f ? loop : LadderBoardingMath.RigLoopSeconds;
        }

        /// <summary>
        /// Where leg 2 begins, and therefore where leg 1 WALKS TO: the rail for a step, and for a climb
        /// the end of the ladder the fisher starts from — the head coming down off the wharf, the FOOT
        /// going up off the boat. (Getting that backwards would walk a fisher standing on a deck up the
        /// wall to the ladder head before she had climbed anything.) This is why the route is chosen
        /// before the approach is timed.
        /// </summary>
        private Vector3 LegTwoStartWorld()
        {
            if (_moveRoute != BoardingRoute.Ladder) return RailWorld();
            return _moveKind == BoardingMoveKind.Boarding
                ? _moveLadderSeat
                : _moveLadderSeat - new Vector3(0f, _moveGapMetres, 0f);
        }

        /// <summary>The compass heading of travel between two world points — the leg's own direction,
        /// taken from TRAVEL rather than from a hop-modified position so a rise never reads as a turn.
        ///
        /// <para>GROUND bearing, not the raw world-XY angle: the endpoints are world positions and world
        /// XY is the squashed ground plane, while the clip rows this feeds depict ground bearings (see
        /// <c>Core.IsoGround</c>). A vault taken on a diagonal would otherwise board her facing the
        /// neighbouring cell.</para></summary>
        private static float HeadingBetween(Vector3 from, Vector3 to)
            => IsoCharacterMath.GroundHeadingFor(new Vector2(to.x - from.x, to.y - from.y),
                                                 minSpeed: 0f, fallbackHeading: 0f);

        // ---- the vault's CLIP (art drop of 2026-08-06 — the rig's board / boardDown) ---------------
        //
        // The arc used to draw WALK frames the whole way over, because IsoCharacterSprite picks its cell
        // from measured transform speed and a fisher moved at walking pace looks like one walking. That
        // was the honest read while no boarding art existed; the pass-6.1 kit added `board` (step up and
        // over a rail) and `boardDown` (stepping ashore), so the vault now plays the clip it always meant.
        //
        // ONLY THE VAULT. Leg 1 is a walk to the rail and stays a walk — measured speed already draws it
        // right, and a boarding clip played while crossing a deck would be a lie about what the body is
        // doing. The clip starts the instant the arc does and stops when the move ends, however it ends.
        //
        // SCALED TO THE ARC, NEVER THE ARC TO THE CLIP. `_boardVaultSeconds` is owner-approved feel
        // (0.55 s by default); the rig's `board` is 10 f × 90 ms = 0.9 s. Stretching the move to suit the
        // art would retune a move the owner already signed off, so the clip is compressed to fit instead
        // — CharacterClipPlayer.Play's scaleToSeconds, which is exactly what it is for.
        //
        // PRESENTATION ONLY. Nothing here reads or writes a gate, a mode or a position: the whole block
        // is skippable, and where the clip is missing (an un-skinned character, a kit baked without it)
        // Play returns false and the arc draws the walk frames it drew before, unchanged.

        /// <summary>Start the vault's clip on the first tick of leg 2, then keep it aimed. Facing comes
        /// from the leg's TRAVEL direction rather than from the hop-modified position, so the parabola's
        /// rise never reads as the fisher turning.</summary>
        private void PlayMoveClip(CharacterClip clip, float heading, float scaleToSeconds, bool holdOnFinish)
        {
            var player = ResolveClipPlayer();
            if (player == null) return;

            if (!_moveClipAttempted || _moveClipAsked != clip)
            {
                // ONCE per LEG, whatever the answer. A character with no boarding art must not re-ask (and
                // re-walk its sheet) on every one of the arc's ~33 frames to be told "no" each time. The
                // clip is part of the key because the ladder route has three legs and each asks for its
                // own — but only when it CHANGES, so the climb's looping clip is started once and left to
                // run rather than restarted every tick (which would freeze it on frame 0).
                _moveClipAttempted = true;
                _moveClipAsked = clip;
                // holdOnFinish: the LEG owns the timing. A one-shot that rounds a hair short would
                // otherwise hand the renderer back for a frame mid-air, which reads as a flicker. The
                // looping ladder clip needs no hold — it never finishes; it is Stop()ped.
                // LATCHED, not assigned. Once a leg has taken the renderer, this move OWNS it until
                // StopVaultClip hands it back — and a LATER leg whose art is missing must not clear the
                // flag, or the previous leg's held pose would be left on the renderer with nothing left
                // to Stop() it. (The ladder route makes this reachable: a kit with board/boardDown baked
                // but no ladderDown plays the cover, then asks for a climb that is not there.)
                if (player.Play(clip, heading, scaleToSeconds, holdOnFinish)) _moveClipPlaying = true;
                return;
            }

            if (_moveClipPlaying) player.SetHeading(heading);
        }

        /// <summary>Take the move's clip off the renderer (idempotent). Called from
        /// <see cref="EndBoardingMove"/>, so it runs on every ending a move has — landed, refused at the
        /// rail, or torn down mid-air. <b>The ladder clip LOOPS</b>, so this is the only thing that ever
        /// ends it: without it a landed fisher would go on climbing while she walks the deck.</summary>
        private void StopVaultClip()
        {
            _moveClipAttempted = false;
            _moveClipAsked = CharacterClip.None;
            if (!_moveClipPlaying) return;
            _moveClipPlaying = false;
            _clipPlayer?.Stop();
        }

        /// <summary>Find the player's clip seam once, lazily — the switcher may be configured before the
        /// fisher has a skin, and a character with no clip player simply never gets one.</summary>
        private CharacterClipPlayer ResolveClipPlayer()
        {
            if (_clipPlayerResolved) return _clipPlayer;
            Transform t = Player;
            if (t == null) return null;              // not resolvable yet — try again next tick
            _clipPlayerResolved = true;
            _clipPlayer = t.GetComponent<CharacterClipPlayer>();
            return _clipPlayer;
        }

        /// <summary>
        /// The move landed: make the transition it set out to make. It calls the SAME
        /// <see cref="BoardDeck"/> / <see cref="Disembark"/> the instant path calls, behind the SAME
        /// gates, re-read here because a short arc is still time passing and the rules are the rules.
        ///
        /// <para>Deliberately NOT a re-dispatch through <see cref="TryInteract"/>. By the far end the
        /// fisher has moved, and E is contextual on POSITION: a fisher who has just arced onto a small
        /// hull's deck can easily be standing inside <see cref="_helmReach"/>, so re-dispatching would
        /// answer "take the helm" to a move that set out to step ashore. A move finishes the transition it
        /// started or none at all.</para>
        /// </summary>
        private void FinishBoardingMove()
        {
            BoardingMoveKind kind = _moveKind;
            EndBoardingMove();

            if (kind == BoardingMoveKind.Boarding)
            {
                // The REPAIR gate, not the reach gate. Reach ("are you close enough to set off?") is a
                // key-press question and is meaningless here — the fisher is standing on her. Whether she
                // may be boarded at all is the rule that still applies at the rail, and on a long hull the
                // seat can be further from her origin than BoardReach, so re-reading reach here would
                // refuse a perfectly good boarding on a dragger.
                if (Boat != null && BoardableNow()) { BoardDeck(); return; }
                // Refused at the rail — the fisher is put back down where they started, still ashore.
                ApplyPlayerFor(ControlMode.OnFoot);
                if (Player != null) Player.position = _moveShoreWorld;
                return;
            }

            if (CanStepAshore()) { Disembark(); return; }
            // The step-off stopped being standable mid-air (she drifted off her land) — the fisher never
            // left the deck as far as the state machine is concerned, so put them back on it.
            ApplyPlayerFor(ControlMode.OnDeck);
            SnapPlayerToDeck(_boardLocalOffset);
        }

        /// <summary>Call the move off without transitioning. The fisher goes back to the mode they never
        /// left — on the wharf where they started (a boarding that was refused mid-air), or re-seated on
        /// the deck (anything that tore the move down under them).</summary>
        private void CancelBoardingMove(bool restorePosition)
        {
            if (_moveKind == BoardingMoveKind.None) return;
            bool wasBoarding = _moveKind == BoardingMoveKind.Boarding;
            EndBoardingMove();

            if (restorePosition && Player != null && wasBoarding) Player.position = _moveShoreWorld;
            ApplyPlayerFor(Mode);
            if (Mode == ControlMode.OnDeck) SnapPlayerToDeck(_boardLocalOffset);
        }

        /// <summary>Clear the move's own state and hand the interact key back. Deliberately separate from
        /// what happens to the PLAYER, because finishing and cancelling disagree about that and agree
        /// about everything else.</summary>
        private void EndBoardingMove()
        {
            _moveKind = BoardingMoveKind.None;
            _moveElapsed = 0f;
            _moveRoute = BoardingRoute.Step;
            _moveLadder = null;
            _moveGapMetres = 0f;
            _moveClimbSeconds = 0f;
            _moveLegTwoSeconds = 0f;
            StopVaultClip();
            ReleaseInteractionGate();
        }

        /// <summary>The rail this move is going over, in world — a boat-relative offset clamped onto the
        /// hull's walkable outline (side decks included where she has them), re-read every tick.</summary>
        private Vector3 RailWorld() => DeckPointWorld(_moveRailRelative, _moveWashboards);

        /// <summary>Where the approach STARTS: the wharf the fisher is standing on (boarding), or the spot
        /// on the deck they were standing on (stepping ashore) — the latter re-clamped every tick so
        /// crossing the deck of a moving hull crosses the deck rather than the water beside it.</summary>
        private Vector3 ApproachStartWorld()
            => _moveKind == BoardingMoveKind.Boarding ? _moveShoreWorld : DeckPointWorld(_moveDeckRelative);

        /// <summary>Where the vault ENDS. Both answers are re-read every tick, and for the same reason: the
        /// arc must finish at the spot the transition is about to place the fisher at, or the landing pops.
        /// Boarding, that is the seat <see cref="BoardDeck"/> will snap them to on THIS hull at THIS
        /// heading; stepping ashore, it is whatever <see cref="Disembark"/> will choose — the dock planks
        /// while she is still in the zone, the boat's own spot the moment she drifts out of it.</summary>
        private Vector3 VaultEndWorld()
        {
            if (_moveKind == BoardingMoveKind.Boarding) return DeckPointWorld(_boardLocalOffset);
            return TryDisembarkLanding(out Vector3 landing) ? landing : _moveShoreWorld;
        }

        /// <summary>A boat-relative offset as the world point it lands on for this hull. Falls back to the
        /// raw offset on a rig with no deck walk (tests, greybox), which is what the switcher's own
        /// <see cref="SnapPlayerToDeck"/> does in the same case.</summary>
        private Vector3 DeckPointWorld(Vector2 boatRelative, bool includeWashboards = false)
        {
            var deck = DeckWalk;
            if (deck != null && deck.TryDeckPointWorld(boatRelative, includeWashboards, out Vector3 world))
                return world;
            return Boat != null ? Boat.position + new Vector3(boatRelative.x, boatRelative.y, 0f)
                                : (Player != null ? Player.position : Vector3.zero);
        }

        /// <summary>
        /// Take the interact key for the length of the move — the same Core seam a modal dialogue uses,
        /// and the flag the deck-gear gates already read. Raised only if it was DOWN, and lowered again
        /// only by whoever raised it, so a move started under an open dialogue neither claims that
        /// dialogue's hold nor hands it back.
        ///
        /// <para><b>The known edge, stated rather than hidden.</b> <see cref="InteractionGate"/> is a plain
        /// bool with several owners and no ref-count, so a modal that opens WHILE the move is in flight
        /// would have its hold lowered when the move lands. That is the same trade
        /// <see cref="ShellFlow"/>'s pause menu already makes with the same flag, and it stays narrow here
        /// because the move holds the key for well under a second and is the only thing that could raise a
        /// modal in that window. If the gate ever grows a counter, both this and the pause menu should use
        /// it — not one of them.</para>
        /// </summary>
        private void HoldInteractionGate()
        {
            if (InteractionGate.IsBlocked) return;
            InteractionGate.IsBlocked = true;
            _moveHeldGate = true;
        }

        private void ReleaseInteractionGate()
        {
            if (!_moveHeldGate) return;
            _moveHeldGate = false;
            InteractionGate.IsBlocked = false;
        }

        /// <summary>
        /// Re-assert the controller-enabled state to match the persisted <see cref="Mode"/> — and re-raise
        /// the camera/active-boat signals so the view follows the right target. Called after a region hop
        /// (the App <c>RegionTravelCoordinator</c> on arrival) so boat OR foot control is reliably LIVE
        /// after EVERY scene return: when aboard, the boat controller + its input are re-enabled and the
        /// walk frozen; on foot, the reverse. This is the fix for "helm goes dead after returning to a
        /// scene" — the persistent rig carries the mode across the toggle, but nothing re-enabled the
        /// controllers to match it, so a re-activated region could leave the active boat un-driven.
        /// Idempotent: safe to call on every arrival regardless of mode. Re-publishing the signals is
        /// cheap and keeps the camera framing correct on return (boat framing aboard, on-foot framing
        /// ashore). Null-safe throughout.
        /// </summary>
        public void ReassertControlMode()
        {
            // A region hop repositions player and boat independently, which is exactly the ground an
            // in-flight arc is interpolating over. Call it off first (keeping the mode it had not yet
            // left) and let the re-assert below settle the fisher properly.
            CancelBoardingMove(restorePosition: true);

            if (Mode == ControlMode.Aboard)
            {
                if (Mooring != null) Mooring.Stow();                 // at the helm → rope stowed
                ApplyPlayerFor(ControlMode.Aboard);
                // The hop repositions player and boat independently, so re-seat the pilot ON the helm —
                // the same re-seating the OnDeck branch below has always done, and now load-bearing
                // because the figure at the helm is DRAWN rather than hidden.
                SnapPlayerToDeck(_helmLocalOffset);
                if (_boatController != null) _boatController.enabled = true;
                if (_boatInput != null) _boatInput.enabled = true;

                BoatHullDef hull = _boatController != null ? _boatController.Hull : null;
                float height = hull != null ? hull.CameraWorldHeightMeters : 14f;
            float hullLength = hull != null ? hull.LengthMeters : 0f;   // the camera floors the framing by it (§9.8)
                string id = hull != null ? hull.Id : null;
                EventBus.Publish(new ActiveBoatChanged(id, height, hullLength));
                EventBus.Publish(new ControlModeChanged(ControlMode.Aboard));
            }
            else if (Mode == ControlMode.OnDeck)
            {
                if (Mooring != null) Mooring.Stow();                 // aboard (deck) → rope stowed
                if (_boatController != null) _boatController.enabled = false;   // nobody at the helm
                if (_boatInput != null) _boatInput.enabled = false;
                ApplyPlayerFor(ControlMode.OnDeck);
                // The hop may have teleported the player to the region's disembark spot — re-seat them
                // on the deck (the coordinator repositions player + boat independently).
                SnapPlayerToDeck(_boardLocalOffset);
                EventBus.Publish(new ControlModeChanged(ControlMode.OnDeck));
            }
            else
            {
                if (_boatController != null) _boatController.enabled = false;
                if (_boatInput != null) _boatInput.enabled = false;
                ApplyPlayerFor(ControlMode.OnFoot);
                EventBus.Publish(new ControlModeChanged(ControlMode.OnFoot));
            }
        }

        /// <summary>
        /// Put the PLAYER OBJECT into the right shape for a mode — the one place the walk/deck controllers,
        /// sprite, physics and parenting are toggled, shared by every transition and the re-assert:
        /// <list type="bullet">
        ///   <item><b>OnFoot</b> — walk controller live, sprite shown, footprint collider + physics on,
        ///   un-parented (free to roam).</item>
        ///   <item><b>OnDeck</b> — deck-walk controller live, sprite shown, physics OFF (the deck is
        ///   transform-driven; the hull collider must never fight the footprint collider), parented to the
        ///   boat's PHYSICS ROOT so its drift carries the player (never the counter-rotated visual child).</item>
        ///   <item><b>Aboard (helm)</b> — both walk controllers dead, physics off, still parented. The
        ///   figure is now DRAWN at the helm by the deck rider (see below); without one wired it is hidden,
        ///   as it always was.</item>
        ///   <item><b>In transit (the boarding move)</b> — the fisher is between the wharf and the deck and
        ///   belongs to neither: drawn as the ordinary ashore figure (a rider leans with a deck they are
        ///   not standing on), un-parented so the arc is a plain world path, and with both walk
        ///   controllers, the physics body and the footprint collider all parked, because the MOVE drives
        ///   the transform and nothing else may argue with it. <paramref name="mode"/> is still the mode
        ///   they have not left yet — the transit shape overlays it rather than replacing it, so there is
        ///   no fourth <see cref="ControlMode"/> for anyone to have to know about.</item>
        /// </list>
        /// Null-safe throughout: tests build a player without a footprint collider / deck controller.
        /// </summary>
        private void ApplyPlayerFor(ControlMode mode, bool inTransit = false)
        {
            if (_playerWalk == null) return;
            bool onFoot = mode == ControlMode.OnFoot;
            bool onDeck = mode == ControlMode.OnDeck;

            // The three things the transit shape actually changes, named once so the rest reads as before:
            // it is drawn ashore, it moves under nobody's power but the move's, and it rides no boat.
            bool drawnAshore = onFoot || inTransit;
            bool free = onFoot && !inTransit;
            bool ridesBoat = !onFoot && !inTransit;

            _playerWalk.enabled = free;

            // WHO DRAWS THE CHARACTER. With a deck rider wired it owns BOTH renderers for every mode — it
            // has to, because riding the hull's rock means drawing the figure on a child transform this
            // one's upright stomp cannot reach, and two owners of one `enabled` flag would fight. Without
            // one (older rigs, tests) the original rule stands untouched: visible ashore and on deck,
            // hidden at the helm.
            var rider = DeckRider;
            if (rider != null && rider.HasRider)
            {
                rider.SetMode(drawnAshore ? ControlMode.OnFoot : mode, ridesBoat ? Boat : null);
            }
            else
            {
                var sr = _playerWalk.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = drawnAshore || onDeck;
            }

            var rb = _playerWalk.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.simulated = free;                                 // aboard (or airborne), the transform drives
                rb.linearVelocity = Vector2.zero;                    // no residual drift across the switch
            }
            // Drop the player's footprint collider while aboard so the hull collider can't bump the
            // frozen on-foot body. Restored on disembark. Dropped mid-boarding for the same reason: the
            // arc passes straight through the hull she is being boarded from.
            var col = _playerWalk.GetComponent<Collider2D>();
            if (col != null) col.enabled = free;

            // Ride the boat: parent to the PHYSICS ROOT while aboard (deck or helm) so the boat's drift
            // carries the player for free; stand free ashore. worldPositionStays keeps the handoff clean.
            if (Player != null)
            {
                Transform parent = ridesBoat ? Boat : null;
                if (Player.parent != parent) Player.SetParent(parent, worldPositionStays: true);
                if (!ridesBoat) Player.rotation = Quaternion.identity;   // never keep a hull tilt ashore
            }

            var deck = DeckWalk;
            if (deck != null)
            {
                // Bind whenever ABOARD, not only while deck-walking. The deck frame is what seats the
                // player on the hull (SnapPlayerToDeck → SnapTo), and the helm needs that seating too now
                // that the pilot is drawn — an unbound deck would silently no-op the snap and leave the
                // figure wherever a region hop dropped them. Binding a DISABLED controller is inert: it
                // stores two references and nothing else runs — which is also why a boarding move can
                // bind it purely to ASK where this hull's rail and seat are, without waking the walk.
                if (!onFoot || inTransit) deck.Bind(Boat);
                deck.enabled = onDeck && !inTransit;
            }
        }

        /// <summary>Seat the player on the deck at a boat-relative (world-axis) spot, clamped to the deck
        /// bounds when a <see cref="DeckWalkController"/> is present (raw offset otherwise — tests).</summary>
        private void SnapPlayerToDeck(Vector2 boatRelative)
        {
            if (Player == null || Boat == null) return;
            var deck = DeckWalk;
            if (deck != null) deck.SnapTo(boatRelative);
            else Player.position = Boat.position + new Vector3(boatRelative.x, boatRelative.y, 0f);
        }

        /// <summary>Wire the switcher in one call (tests / editor) and start on foot.</summary>
        public void Configure(PlayerWalkController playerWalk, BoatController boatController, Behaviour boatInput,
                              Transform dockZone, float zoneRadius, Transform disembarkPoint)
        {
            _playerWalk = playerWalk;
            _boatController = boatController;
            _boatInput = boatInput;
            _dockZone = dockZone;
            _zoneRadius = zoneRadius;
            _disembarkPoint = disembarkPoint;
            Mode = ControlMode.OnFoot;
        }

        /// <summary>Tune the helm station in one call (tests / editor): where the helm spot sits relative
        /// to the boat (world-axis offset) and how close E must be pressed to take it.</summary>
        public void ConfigureHelm(Vector2 helmLocalOffset, float helmReach)
        {
            _helmLocalOffset = helmLocalOffset;
            _helmReach = helmReach;
        }

        /// <summary>Tune the one interact VERB in one call (tests / editor feel sessions). Passing
        /// <paramref name="enabled"/> false restores the pre-M2-39 behaviour exactly — E is board / helm /
        /// step ashore and nothing else, whatever is registered.</summary>
        public void ConfigureInteractVerb(bool enabled, float arcDegrees)
        {
            _interactVerb = enabled;
            _interactArcDegrees = Mathf.Clamp(arcDegrees, 0f, 360f);
        }

        /// <summary>Tune the boarding MOVE in one call (tests / editor feel sessions). Passing
        /// <paramref name="enabled"/> false restores the instant reposition exactly, which is both the
        /// owner's A/B and the shape every pre-move caller of <see cref="TryInteract"/> still gets.</summary>
        public void ConfigureBoardingMove(bool enabled, float approachSpeed, float approachMaxSeconds,
                                          float vaultSeconds, float vaultHopMeters)
        {
            _boardingMove = enabled;
            _boardApproachSpeed = Mathf.Max(0.1f, approachSpeed);
            _boardApproachMaxSeconds = Mathf.Max(0f, approachMaxSeconds);
            _boardVaultSeconds = Mathf.Max(0.01f, vaultSeconds);
            _boardVaultHopMeters = Mathf.Max(0f, vaultHopMeters);
        }

        /// <summary>
        /// Re-point the dock-landing zone to ANOTHER region's mooring on a VS-22 travel — WITHOUT changing
        /// the control mode (you arrive in the new region still aboard / still on foot). The persistent
        /// switcher carries across the hop; the App RegionTravelCoordinator calls this so the tidy dock
        /// landing works at whichever region's wharf you're standing in. Null args are tolerated (a region
        /// simply has no dock landing until wired — you still step off at the boat onto land).
        /// </summary>
        public void SetDock(Transform dockZone, Transform disembarkPoint)
        {
            _dockZone = dockZone;
            _disembarkPoint = disembarkPoint;
        }

        // ---- lifecycle (greybox dev input + prompt) -----------------------------------------

        private void Awake() => BuildHint();

        /// <summary>Torn down mid-move (scene teardown, a disabled rig): call the move off so it can hand
        /// back the interact key. A held <see cref="InteractionGate"/> outliving its holder is the one way
        /// this could wedge interaction off for the whole game. The interact candidate goes with it, for
        /// the same reason — nothing else republishes it, so a highlight would simply stay lit.</summary>
        private void OnDisable()
        {
            CancelBoardingMove(restorePosition: true);
            InteractVerb.ClearCandidate();
        }

        /// <summary>
        /// <b>THE PLAYER IS SCREEN-UPRIGHT WHENEVER THEY RIDE A BOAT</b> — in every mode, not only while
        /// deck-walking.
        ///
        /// <para>This is the invariant <see cref="ApplyPlayerFor"/> states for the ashore case
        /// (<c>if (!ridesBoat) Player.rotation = identity</c>) and that the fisher on deck has always had
        /// from <see cref="DeckWalkController"/>'s own LateUpdate stomp. The gap was the HELM: the switcher
        /// disables the deck walk there, so nothing squared the root, and the player object quietly inherited
        /// the rotation of the hull's physics body it is parented to. That cost nothing for as long as the
        /// figure at the helm was HIDDEN — and became the owner's 2026-08-07 defect the moment #445 drew the
        /// pilot, who then lay over further with every degree the boat turned (<i>"slowly lose reference and
        /// spin horizontally"</i>).</para>
        ///
        /// <para>It belongs here rather than in the rider because this is the component that PARENTED them to
        /// a rotating body, it is present in every rig (a rider is optional), and it therefore also covers
        /// anything else ever parked on the player. LateUpdate for the same reason the deck walk uses it: the
        /// hull's rotation is written by physics, so squaring in Update would be a step stale by the time
        /// anything is drawn. Idempotent and free when already square.</para>
        /// </summary>
        private void LateUpdate()
        {
            if (Mode == ControlMode.OnFoot || Player == null) return;
            if (Player.rotation != Quaternion.identity) Player.rotation = Quaternion.identity;
        }

        /// <summary>
        /// LEAVE-THE-HELM DRIFT (boats-and-navigation.md §3 "leave the helm, work the rail"; Rod
        /// Fishing v2 §4.1 — you fish unmanned): while the player is ON DECK nobody is steering, but the
        /// sea keeps working the hull — she sets with the current, is shoved by the wind and slewed by
        /// the seakeeping (the weathervane the deck angler repositions against). The boat controller is
        /// deliberately DISABLED on deck (enabled == "helm is manned" for the presentation layers), so
        /// the mode owner ticks its unmanned force pass explicitly. This is the fix for the deck
        /// suppressing the drift entirely (controller off + mooring stowed = a hull frozen in glass).
        /// OnFoot keeps its mooring-rope drift (BoatMooring); Aboard keeps the manned FixedUpdate.
        /// </summary>
        private void FixedUpdate()
        {
            if (Mode == ControlMode.OnDeck && _boatController != null)
                _boatController.TickUnmannedDrift();
        }

        private void Update()
        {
            // The SHELL is holding the world — the title page is up, or the pause menu is (M1 §7.8).
            // Stronger than the interaction gate below: time itself is stopped, so the controls are parked
            // rather than merely deaf. A pause you can sail through is not a pause.
            // Every early-out below drops the interact candidate as well as the hint: a diegetic highlight
            // left burning on a thing you can no longer act on teaches the player a lie (M2-39).
            if (ApplyShellInputBlock()) { InteractVerb.ClearCandidate(); return; }

            // A boarding move owns the frame while it runs, and is ticked BEFORE the gate check because
            // it is holding that gate itself (see the boarding-move block above). Scaled time, so a pause
            // freezes the fisher mid-vault and resumes them there rather than teleporting them on.
            if (IsBoardingMove)
            {
                if (_hint != null && _hint.enabled) _hint.enabled = false;
                InteractVerb.ClearCandidate();
                TickBoardingMove(Time.deltaTime);
                return;
            }

            // A modal dialogue (VS-21, world-content) owns the shared Interact key while it's up —
            // don't also board/disembark under it. Gate is a Core contract so neither lane references
            // the other (see InteractionGate). Hide our board/dock hint too while blocked.
            if (InteractionGate.IsBlocked)
            {
                if (_hint != null && _hint.enabled) _hint.enabled = false;
                InteractVerb.ClearCandidate();
                return;
            }

            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) BeginInteract();
            // Q holds/roots the rope of a moored boat you're standing by (the mooring interaction).
            if (kb != null && kb.qKey.wasPressedThisFrame) ToggleMooring();
            UpdateHint();

            // What the verb WOULD act on, republished only when it changes — the signal the diegetic
            // outline rides (M2-39; no screen-space prompt). Resolved after the press so the highlight
            // reflects the world the press left behind, not the one it found.
            if (_interactVerb) InteractVerb.PublishCandidate(ActorNow(), _interactArcDegrees);
        }

        // ---- the shell's hold on the world (M1 §7.8) ----------------------------------------

        private bool _inputParked;
        private bool _walkWasEnabled;
        private bool _boatInputWasEnabled;

        /// <summary>
        /// Park or restore the player's controls to match <see cref="ShellFlow.WorldInputBlocked"/>.
        /// Edge-triggered — it touches the components only when the answer CHANGES, so a paused game is
        /// not re-disabling three behaviours every frame — and it restores exactly what it found, so a
        /// helm that was dead before the pause (nobody aboard) is not woken by resuming.
        ///
        /// <para>Deliberately parks the INPUT behaviours only, not <c>BoatController</c>: the hull keeps
        /// its drag and settles where the player left it, where a disabled controller would let a moving
        /// boat coast on forever at constant velocity behind the menu.</para>
        /// </summary>
        /// <returns>True while the shell holds the world — the caller does nothing else this frame.</returns>
        private bool ApplyShellInputBlock()
        {
            bool blocked = ShellFlow.WorldInputBlocked;
            if (blocked == _inputParked) return blocked;

            _inputParked = blocked;
            if (blocked)
            {
                _walkWasEnabled = _playerWalk != null && _playerWalk.enabled;
                _boatInputWasEnabled = _boatInput != null && _boatInput.enabled;

                if (_playerWalk != null) _playerWalk.enabled = false;
                if (_boatInput != null) _boatInput.enabled = false;
                if (_hint != null) _hint.enabled = false;   // no "E: Board" prompt under a title page
            }
            else
            {
                if (_playerWalk != null) _playerWalk.enabled = _walkWasEnabled;
                if (_boatInput != null) _boatInput.enabled = _boatInputWasEnabled;
            }
            return blocked;
        }

        private void UpdateHint()
        {
            if (_hint == null) return;

            // Cozy feedback (P5): when standing near a damaged, unrepaired boat, say why you can't board
            // rather than showing nothing — the opening nudges the player to the shipwright.
            bool atDamagedBoat = Mode == ControlMode.OnFoot && WithinBoardReach() && !BoardableNow();
            bool canMoor = CanToggleMooring();
            bool show = CanInteract() || atDamagedBoat || canMoor;
            if (_hint.enabled != show) _hint.enabled = show;
            if (show)
            {
                string text;
                if (atDamagedBoat) text = "She needs repairs before she'll sail";
                else if (Mode == ControlMode.OnFoot) text = CanInteract() ? "E: Board" : null;
                else if (Mode == ControlMode.Aboard) text = "E: Leave the helm";
                else if (WithinHelmReach()) text = "E: Take the helm";
                else text = InDockZone() ? "E: Dock" : "E: Get off";   // step ashore from the deck

                // Rope prompt (the mooring interaction): while holding the rope, offer to root it to the
                // ground; while rooted, offer to take it back in hand. Shown alongside the board prompt when
                // both apply (on foot beside a moored boat).
                if (canMoor)
                {
                    string rope = Mooring.IsHeld ? "Q: Tie to ground" : "Q: Take rope";
                    text = string.IsNullOrEmpty(text) ? rope : text + "    " + rope;
                }
                if (_hint.text != text) _hint.text = text;
            }
        }

        // A tiny screen-space prompt shown only when a board/dock is available (nice-to-have; ui-ux polishes).
        private void BuildHint()
        {
            var canvasGo = new GameObject("ControlHint_Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("Hint", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(canvasGo.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 160f);
            rt.sizeDelta = new Vector2(400f, 60f);

            _hint = go.GetComponent<Text>();
            _hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _hint.fontSize = 34;
            _hint.alignment = TextAnchor.LowerCenter;
            _hint.color = Color.white;
            _hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hint.verticalOverflow = VerticalWrapMode.Overflow;
            _hint.raycastTarget = false;

            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            _hint.enabled = false;
        }
    }
}
