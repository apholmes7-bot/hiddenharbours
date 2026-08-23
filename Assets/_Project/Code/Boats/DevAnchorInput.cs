using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// PLACEHOLDER dev control for the ground tackle: <b>R drops or weighs the anchor</b> on the boat you
    /// are aboard. The real control is diegetic — a windlass/anchor switch on the helm console — and is
    /// <c>ui-ux</c>'s later work; this is the greybox key that makes the mechanic playable now, exactly as
    /// <see cref="DevBoatInput"/> stands in for the helm and <c>DevTrapInput</c> for the pots.
    ///
    /// <para><b>The binding.</b> <c>R</c> for RODE — audited free of every other binding in the project by
    /// a full sweep of <c>Key.</c> literals in code AND of <c>InputSystem_Actions.inputactions</c> (the
    /// half a <c>= Key.</c> grep misses): WASD/arrows helm, Space brace/haul/work, E interact, Q mooring,
    /// P buy, B sell, T trap-drop/rotation-mode, G grant, H haul, Y auto-yaw, L spotlight/lid, F
    /// fleet/freezer/bucket, V variant, I ice, O displaced water, N tide table, X dump, Z neutral, K
    /// instrument cycle, J/U character spike, and <b>C</b>/1/2/Enter/LeftShift claimed by
    /// <c>InputSystem_Actions</c> (C = Crouch — so C was NOT free, whatever a code-only grep says). That
    /// leaves M and R; R carries the meaning.</para>
    ///
    /// <para><b>Where the key lives.</b> Only from the boat — at the helm (<see cref="ControlMode.Aboard"/>)
    /// or on her deck (<see cref="ControlMode.OnDeck"/>), never on foot ashore — and only on the boat the
    /// player is actually on, so a second hull in the scene (a rotation rig, a test harness, the ambient
    /// fleet) never eats the press. Stood down under a modal dialogue (<see cref="InteractionGate"/>) and
    /// while a text field owns the keyboard (<see cref="HelmKeyCapture"/> — R is a letter, and naming a
    /// waypoint "ROCK LEDGE" must not drop the hook).</para>
    ///
    /// <para>New Input System only (<c>Keyboard.current</c>) — legacy <c>UnityEngine.Input</c> compiles in
    /// this project and then THROWS at runtime.</para>
    /// </summary>
    [RequireComponent(typeof(BoatAnchor))]
    [DisallowMultipleComponent]
    public sealed class DevAnchorInput : MonoBehaviour
    {
        [Header("Keys (dev only)")]
        [Tooltip("Drop or weigh the anchor. R for RODE — audited free of every other binding in code and " +
                 "in InputSystem_Actions.")]
        [SerializeField] private Key _anchorKey = Key.R;

        private BoatAnchor _anchor;
        private BoatController _boat;

        // Anchoring is done FROM THE BOAT: at the helm or on her deck, never ashore. Republished on every
        // transition (and on region arrival), so this survives a scene hop.
        private bool _aboard;

        /// <summary>The key that works the ground tackle (diagnostics / the key ledger's other end).</summary>
        public Key AnchorKey => _anchorKey;

        /// <summary>
        /// True while the anchor key may actually reach the tackle. Public and input-free for the reason
        /// <c>DevBoatInput.HelmKeysLive</c> and <c>DevTrapInput.GearKeysLive</c> are: headless batchmode
        /// drops key events, so a test cannot press R and watch the hook — but it CAN assert that the gate
        /// the read consults says the right thing, in both directions, on the live component.
        /// </summary>
        public bool AnchorKeyLive =>
            _aboard
            && !InteractionGate.IsBlocked
            && !HelmKeyCapture.IsCapturing
            && IsThePlayersBoat;

        /// <summary>
        /// Is this the hull the player is actually on? Asked of the Core declaration
        /// (<see cref="HelmSlot.IsPlayersBoat"/>) — the boat she is ABOARD, which is deliberately a
        /// wider fact than the boat she is at the HELM of.
        ///
        /// <para><b>⚠ It must not be the helm question, and this key is where that goes wrong most
        /// easily.</b> The starting dory is ROWED: she has no helm at all (<c>HasHelm</c> is the engine
        /// question), so a helm-shaped test would kill the anchor on the very boat the game opens with.
        /// And stepping back from the wheel onto the deck gives up the helm, not the boat — this key
        /// lives on the deck by design ("anchoring is a boat job either way", above).</para>
        ///
        /// <para>This used to compare against the Core helm slot, which answered correctly only by
        /// accident: every relay claimed that slot on enable, so "the slot is mine" meant no more than
        /// "I was the last hull to wake up". Once the slot became honest about who is piloting, the
        /// borrowed answer went dead on the player's own boat.</para>
        /// </summary>
        private bool IsThePlayersBoat
        {
            get
            {
                if (_boat == null) _boat = GetComponent<BoatController>();
                return GameServices.Helm.IsPlayersBoat(_boat);
            }
        }

        private void Awake()
        {
            if (_anchor == null) _anchor = GetComponent<BoatAnchor>();
            if (_boat == null) _boat = GetComponent<BoatController>();
        }

        private void OnEnable()
        {
            // Fresh components start ashore; every transition (and the region-arrival re-assert)
            // republishes the mode, which keeps this true across scene hops.
            _aboard = false;
            EventBus.Subscribe<ControlModeChanged>(OnControlModeChanged);
        }

        private void OnDisable() => EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);

        /// <summary>Public so tests can drive the aboard gate through the same path the bus uses.</summary>
        public void OnControlModeChanged(ControlModeChanged e)
            => _aboard = e.Mode == ControlMode.Aboard || e.Mode == ControlMode.OnDeck;

        private void Update()
        {
            if (!AnchorKeyLive) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!kb[_anchorKey].wasPressedThisFrame) return;

            if (_anchor == null) _anchor = GetComponent<BoatAnchor>();
            if (_anchor != null) _anchor.Toggle();
        }
    }
}
