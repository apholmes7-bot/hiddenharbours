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
            ControlMode.OnDeck => WithinHelmReach() || InDockZone() || OnLand(),
            _ => true,   // at the helm → step back onto the deck, always allowed
        };

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
                    if (InDockZone() || OnLand()) { Disembark(); return true; }
                    return false;

                default: // Aboard (at the helm) → step back onto the deck
                    LeaveHelm();
                    return true;
            }
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

        /// <summary>OnDeck → Aboard: take the HELM station. The player figure hands over to the boat
        /// picture (hidden, still riding the hull); steering input goes live.</summary>
        private void TakeHelm()
        {
            ApplyPlayerFor(ControlMode.Aboard);
            if (_boatController != null) _boatController.enabled = true;
            if (_boatInput != null) _boatInput.enabled = true;
            Mode = ControlMode.Aboard;

            // Camera: zoom to the boat's framing (#17 ActiveBoatChanged) + retarget to it (ControlModeChanged).
            BoatHullDef hull = _boatController != null ? _boatController.Hull : null;
            float height = hull != null ? hull.CameraWorldHeightMeters : 14f;
            string id = hull != null ? hull.Id : null;
            EventBus.Publish(new ActiveBoatChanged(id, height));
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
            bool atDock = InDockZone();
            if (_boatController != null) _boatController.enabled = false;
            if (_boatInput != null) _boatInput.enabled = false;

            // Place the on-foot player: a tidy landing on the dock planks when disembarking at the dock,
            // otherwise step off right where the boat is (onto the nearby land/flats) so disembark-on-land
            // doesn't teleport you back to a far dock. Null-safe (tests / no disembark point wired).
            if (Player != null)
            {
                if (atDock && _disembarkPoint != null) Player.position = _disembarkPoint.position;
                else if (Boat != null) Player.position = Boat.position;
            }

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
            if (Mode == ControlMode.Aboard)
            {
                if (Mooring != null) Mooring.Stow();                 // at the helm → rope stowed
                ApplyPlayerFor(ControlMode.Aboard);
                if (_boatController != null) _boatController.enabled = true;
                if (_boatInput != null) _boatInput.enabled = true;

                BoatHullDef hull = _boatController != null ? _boatController.Hull : null;
                float height = hull != null ? hull.CameraWorldHeightMeters : 14f;
                string id = hull != null ? hull.Id : null;
                EventBus.Publish(new ActiveBoatChanged(id, height));
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
        ///   <item><b>Aboard (helm)</b> — both walk controllers dead, sprite hidden, physics off, still
        ///   parented (the hidden player rides the hull; stepping back to the deck re-shows them).</item>
        /// </list>
        /// Null-safe throughout: tests build a player without a footprint collider / deck controller.
        /// </summary>
        private void ApplyPlayerFor(ControlMode mode)
        {
            if (_playerWalk == null) return;
            bool onFoot = mode == ControlMode.OnFoot;
            bool onDeck = mode == ControlMode.OnDeck;

            _playerWalk.enabled = onFoot;

            var sr = _playerWalk.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = onFoot || onDeck;           // visible ashore + on deck; hidden at the helm

            var rb = _playerWalk.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.simulated = onFoot;                               // aboard, the transform (not physics) drives
                rb.linearVelocity = Vector2.zero;                    // no residual drift across the switch
            }
            // Drop the player's footprint collider while aboard so the hull collider can't bump the
            // frozen on-foot body. Restored on disembark.
            var col = _playerWalk.GetComponent<Collider2D>();
            if (col != null) col.enabled = onFoot;

            // Ride the boat: parent to the PHYSICS ROOT while aboard (deck or helm) so the boat's drift
            // carries the player for free; stand free ashore. worldPositionStays keeps the handoff clean.
            if (Player != null)
            {
                Transform parent = onFoot ? null : Boat;
                if (Player.parent != parent) Player.SetParent(parent, worldPositionStays: true);
                if (onFoot) Player.rotation = Quaternion.identity;   // never keep a hull tilt ashore
            }

            var deck = DeckWalk;
            if (deck != null)
            {
                if (onDeck) deck.Bind(Boat);
                deck.enabled = onDeck;
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

        private void Update()
        {
            // A modal dialogue (VS-21, world-content) owns the shared Interact key while it's up —
            // don't also board/disembark under it. Gate is a Core contract so neither lane references
            // the other (see InteractionGate). Hide our board/dock hint too while blocked.
            if (InteractionGate.IsBlocked)
            {
                if (_hint != null && _hint.enabled) _hint.enabled = false;
                return;
            }

            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) TryInteract();
            // Q holds/roots the rope of a moored boat you're standing by (the mooring interaction).
            if (kb != null && kb.qKey.wasPressedThisFrame) ToggleMooring();
            UpdateHint();
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
