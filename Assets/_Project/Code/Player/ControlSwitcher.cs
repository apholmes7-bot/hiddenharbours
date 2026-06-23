using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Economy;   // RepairLedger only — the boarding gate's single source of truth (see BoardableNow)

namespace HiddenHarbours.Player
{
    /// <summary>
    /// The control-mode state machine (OnFoot ⇄ Aboard) — step 2 of the on-foot player, completing the
    /// walk → board → sail → fish/sell/buy → dock → disembark → walk loop. Owns the active mode and
    /// gates which controller receives input: on foot the <see cref="PlayerWalkController"/> drives;
    /// aboard the <see cref="BoatController"/> + its dev input drive. INTERACT (dev key E) boards when
    /// on foot within the dock zone, and disembarks when aboard with the boat back in the dock zone.
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

        [Header("Dock zone")]
        [Tooltip("The dock/mooring point. Board when the player is within range; disembark when the boat is.")]
        [SerializeField] private Transform _dockZone;
        [Tooltip("Board/dock zone radius (m). Forgiving (P5 cozy) so a reasonably-parked boat — bow stopped " +
                 "against the dock-head collider — still registers a disembark; boarding uses the on-foot " +
                 "player's position, so this stays comfortable for both.")]
        [SerializeField] private float _zoneRadius = 3.5f;
        [Tooltip("Where the on-foot player is placed after disembarking AT THE DOCK (on the dock planks). " +
                 "When disembarking away from the dock (near a shore), the player steps off at the boat's " +
                 "own position instead, so this is only used for the tidy dock landing.")]
        [SerializeField] private Transform _disembarkPoint;

        [Header("Disembark-anywhere (near land/shore)")]
        [Tooltip("Disembarking is allowed wherever the boat is near land/shore — not only at the dock. " +
                 "Shore is detected two ways (either suffices): the authored tidal terrain shoaling toward " +
                 "land (the boat is over water shallower than ShoreDepthThreshold), and/or a physical land/" +
                 "shore collider within ShoreProbeRadius on this mask. Leave the mask empty to rely on the " +
                 "terrain test alone (St Peters); set it to the layer your shore-edge/island colliders use " +
                 "(the cove's ShoreEdge is on Default) to allow step-off onto a hard shore.")]
        [SerializeField] private LayerMask _shoreMask = 0;
        [Tooltip("How close (m) a shore/land collider must be for the boat to count as 'near shore'. " +
                 "Forgiving so a boat nudged up to the beach reliably lets you step off (P5 cozy).")]
        [SerializeField] private float _shoreProbeRadius = 2.5f;
        [Tooltip("Water depth (m) at or below which the boat counts as 'near shore' over authored tidal " +
                 "terrain — i.e. the water has shoaled toward land. Read off the same deterministic depth " +
                 "(water level − ground) the boat-cross gate uses, so 'near shore' and 'too shallow to " +
                 "float here' agree. Open water (no terrain wired) reads infinite depth → never shore by " +
                 "this test, so set a shore mask for non-tidal regions.")]
        [SerializeField] private float _shoreDepthThreshold = 1.5f;

        public ControlMode Mode { get; private set; } = ControlMode.OnFoot;

        private Text _hint;

        private Transform Player => _playerWalk != null ? _playerWalk.transform : null;
        private Transform Boat   => _boatController != null ? _boatController.transform : null;

        // ---- zone tests (testable via positioned transforms) --------------------------------

        public bool InBoardZone()
            => Player != null && _dockZone != null
               && Vector2.Distance(Player.position, _dockZone.position) <= _zoneRadius;

        public bool InDockZone()
            => Boat != null && _dockZone != null
               && Vector2.Distance(Boat.position, _dockZone.position) <= _zoneRadius;

        /// <summary>
        /// Pure test: is the water under the boat shallow enough to count as "near shore"? Reads the SAME
        /// deterministic depth (water level − authored ground) the boat-cross gate uses, so "near shore"
        /// and "too shallow to float here" can't disagree. Open water (no terrain wired) reads
        /// <see cref="float.PositiveInfinity"/> → never near shore by depth. Pure + static so the
        /// disembark-near-land rule is EditMode-testable with a fake terrain/environment.
        /// </summary>
        public static bool IsNearShoreByDepth(float waterDepth, float shoreDepthThreshold)
            => waterDepth <= shoreDepthThreshold;

        /// <summary>
        /// True when the boat is near land/shore right now, so the player may step off here (not only at
        /// the dock). Two independent tells, either suffices (P5 forgiving):
        ///   • the authored tidal terrain has shoaled — water depth under the boat ≤ ShoreDepthThreshold
        ///     (the deterministic <see cref="BoatCrossing.DepthAt"/> read; open water = infinite = not shore);
        ///   • a physical land/shore collider sits within ShoreProbeRadius on the shore mask (an empty mask
        ///     skips this test). The cove's hard shore-edge is found this way; St Peters' soft flats by depth.
        /// </summary>
        public bool NearShore()
        {
            if (Boat == null) return false;

            // 1) Tidal-terrain shoaling (deterministic, the boat-cross gate's depth read).
            float depth = BoatCrossing.DepthAt(GameServices.TidalTerrain, GameServices.Environment,
                                               GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0,
                                               Boat.position);
            if (!float.IsPositiveInfinity(depth) && IsNearShoreByDepth(depth, _shoreDepthThreshold))
                return true;

            // 2) A physical land/shore collider close by (only when a mask is set; 0 = skip).
            if (_shoreMask.value != 0
                && Physics2D.OverlapCircle(Boat.position, _shoreProbeRadius, _shoreMask) != null)
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

        /// <summary>True if INTERACT would transition right now (in the right zone for the current mode).
        /// On foot this also requires the boat to be boardable (a damaged dory blocks boarding until
        /// repaired). Aboard, you may disembark at the dock OR anywhere the boat is near land/shore
        /// (<see cref="NearShore"/>) — disembarking is otherwise never gated.</summary>
        public bool CanInteract()
            => Mode == ControlMode.OnFoot
                ? (InBoardZone() && BoardableNow())
                : (InDockZone() || NearShore());

        /// <summary>Attempt the board/disembark transition. Returns true if it happened.</summary>
        public bool TryInteract()
        {
            if (!CanInteract()) return false;
            if (Mode == ControlMode.OnFoot) Board();
            else Disembark();
            return true;
        }

        // ---- transitions --------------------------------------------------------------------

        private void Board()
        {
            SetPlayerActive(false);                                  // hide & freeze the on-foot player
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

        private void Disembark()
        {
            // PARK WHERE LEFT (disembark-anywhere safety): bring the boat to rest before dropping the helm
            // so an un-crewed boat stays put and never coasts off / strands itself. (A wind/tide mooring-
            // drift mechanic for UNtied boats is a separate follow-up; here she's safe-moored on step-off.)
            bool atDock = InDockZone();
            if (_boatController != null) { _boatController.Stop(); _boatController.enabled = false; }
            if (_boatInput != null) _boatInput.enabled = false;

            // Place the on-foot player: a tidy landing on the dock planks when disembarking at the dock,
            // otherwise step off right where the boat is (onto the nearby shore/flats) so disembark-anywhere
            // doesn't teleport you back to a far dock. Null-safe (tests / no disembark point wired).
            if (Player != null)
            {
                if (atDock && _disembarkPoint != null) Player.position = _disembarkPoint.position;
                else if (Boat != null) Player.position = Boat.position;
            }
            SetPlayerActive(true);
            Mode = ControlMode.OnFoot;

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
                SetPlayerActive(false);
                if (_boatController != null) _boatController.enabled = true;
                if (_boatInput != null) _boatInput.enabled = true;

                BoatHullDef hull = _boatController != null ? _boatController.Hull : null;
                float height = hull != null ? hull.CameraWorldHeightMeters : 14f;
                string id = hull != null ? hull.Id : null;
                EventBus.Publish(new ActiveBoatChanged(id, height));
                EventBus.Publish(new ControlModeChanged(ControlMode.Aboard));
            }
            else
            {
                if (_boatController != null) _boatController.enabled = false;
                if (_boatInput != null) _boatInput.enabled = false;
                SetPlayerActive(true);
                EventBus.Publish(new ControlModeChanged(ControlMode.OnFoot));
            }
        }

        private void SetPlayerActive(bool active)
        {
            if (_playerWalk == null) return;
            _playerWalk.enabled = active;
            var sr = _playerWalk.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = active;
            var rb = _playerWalk.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;        // no residual drift while hidden
            // Drop the player's footprint collider while aboard so the boat backing onto the dock can't
            // bump the hidden, frozen on-foot player (the boat gained a hull collider this pass). Restored
            // on disembark. Null-safe: tests build a player without a footprint collider.
            var col = _playerWalk.GetComponent<Collider2D>();
            if (col != null) col.enabled = active;
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

        /// <summary>
        /// Re-point the board/dock zone to ANOTHER region's mooring on a VS-22 travel — WITHOUT changing
        /// the control mode (you arrive in the new region still aboard / still on foot). The persistent
        /// switcher carries across the hop; the App RegionTravelCoordinator calls this so boarding and
        /// disembarking work at whichever region's wharf you're standing in. Null args are tolerated (a
        /// region simply has no board zone until wired).
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
            UpdateHint();
        }

        private void UpdateHint()
        {
            if (_hint == null) return;

            // Cozy feedback (P5): when standing at the mooring of a damaged, unrepaired boat, say why you
            // can't board rather than showing nothing — the opening nudges the player to the shipwright.
            bool atDamagedBoat = Mode == ControlMode.OnFoot && InBoardZone() && !BoardableNow();
            bool show = CanInteract() || atDamagedBoat;
            if (_hint.enabled != show) _hint.enabled = show;
            if (show)
            {
                string text;
                if (atDamagedBoat) text = "She needs repairs before she'll sail";
                else if (Mode == ControlMode.OnFoot) text = "E: Board";
                else text = InDockZone() ? "E: Dock" : "E: Get off";   // near a shore away from the dock
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
