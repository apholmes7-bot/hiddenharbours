using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>The anchor key</b> — the control that works the ground tackle from a hull that has no dash to
    /// flick a switch on. The rowed dory, the motorised dory, the punt: a small hook, one key, heaved
    /// over the bow by hand. A hull WITH a helm console shows the diegetic control instead — the ANCH
    /// switch on her dash (ADR 0039: a control the player can see and touch beats a key nobody was
    /// told about) — and the key keeps working there too, because it is the same verb reaching the same
    /// tackle. One <see cref="BoatAnchor.Toggle"/>, two controls, neither a fallback for the other.
    ///
    /// <para><b>The binding: Q, unmodified.</b> This is a REUSED VERB, not a new letter — the A–Z ledger
    /// is spent (ADR 0039 §6 says so in as many words), and the answer is that the game already has a
    /// ground-tackle verb and it is Q:</para>
    ///
    /// <list type="bullet">
    ///   <item><b>Q ashore</b> works the mooring you are standing by — hold the rope, or root it at your
    ///   feet (<c>ControlSwitcher.ToggleMooring</c>). <b>Q aboard</b> lets go, or weighs, the hook. Same
    ///   hand, same meaning — <i>make her fast to the ground, or let her go</i> — and which tackle it
    ///   means is decided by where you are standing, exactly the way it is on a real boat.</item>
    ///   <item><b>The two readings can never both be live.</b> <c>CanToggleMooring()</c> requires
    ///   <see cref="ControlMode.OnFoot"/>; this key requires <see cref="ControlMode.Aboard"/> or
    ///   <see cref="ControlMode.OnDeck"/>. The modes are disjoint by construction, so Q is unambiguous
    ///   in every frame without a modifier, a hold or an arbiter. (Aboard, Q does nothing at all today —
    ///   <c>ToggleMooring</c> no-ops out of mode — so this claims a genuinely dead press.)</item>
    ///   <item><b>Q is free at the asset level too:</b> <c>InputSystem_Actions.inputactions</c> binds
    ///   W/A/S/D, arrows, Space, E, C, 1, 2, Enter and LeftShift, and no Q. The half a code-only
    ///   <c>= Key.</c> grep misses was checked, because that grep is exactly what went wrong last time
    ///   (below).</item>
    /// </list>
    ///
    /// <para><b>Why NOT R, which this key used to be.</b> The dev binding claimed R "audited free of
    /// every other binding" — and it was not. <c>MooringController._workKey</c> is <c>Key.R</c>: the key
    /// that tightens, slackens (SHIFT) and — held — CASTS OFF a line already made fast, and that
    /// controller is live on a boat's deck. So standing on deck with a line on, one press of R both
    /// worked the rope and toggled the hook, and a held R cast her off while dropping the anchor. That
    /// double-booking has been in the build since the dev key landed; promoting the anchor to a shipping
    /// control is the moment to unpick it, and R goes back to the mooring lane alone.</para>
    ///
    /// <para><b>Where the key lives.</b> Only from the boat — at the helm (<see cref="ControlMode.Aboard"/>)
    /// or on her deck (<see cref="ControlMode.OnDeck"/>), never on foot ashore — and only on the boat the
    /// player is actually on, so a second hull in the scene (a rotation rig, a test harness, the ambient
    /// fleet) never eats the press. Stood down under a modal dialogue (<see cref="InteractionGate"/>) and
    /// while a text field owns the keyboard (<see cref="HelmKeyCapture"/> — Q is a letter, and naming a
    /// waypoint "QUARRY LEDGE" must not drop the hook).</para>
    ///
    /// <para>New Input System only (<c>Keyboard.current</c>) — legacy <c>UnityEngine.Input</c> compiles in
    /// this project and then THROWS at runtime.</para>
    /// </summary>
    [RequireComponent(typeof(BoatAnchor))]
    [DisallowMultipleComponent]
    public sealed class AnchorInput : MonoBehaviour
    {
        [Header("Keys")]
        [Tooltip("Let go or weigh the anchor. Q — the ground-tackle verb the game already has: ashore " +
                 "it works the mooring you are standing by, aboard it works the hook. The two can " +
                 "never both be live (the mooring toggle is OnFoot-only, this key is aboard-only), so " +
                 "one letter carries both with no modifier.")]
        [SerializeField] private Key _anchorKey = Key.Q;

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
        /// drops key events, so a test cannot press Q and watch the hook — but it CAN assert that the gate
        /// the read consults says the right thing, in both directions, on the live component.
        /// </summary>
        public bool AnchorKeyLive =>
            _aboard
            && !InteractionGate.IsBlocked
            && !HelmKeyCapture.IsCapturing
            && CarriesAnchor
            && IsThePlayersBoat;

        /// <summary>
        /// Does this hull carry ground tackle at all? Her own <see cref="BoatHullDef.HasAnchor"/> data,
        /// asked through the ONE resolver the sim and the dash switch also read
        /// (<see cref="AnchorMath.CarriesAnchor"/>) — a boat with no hook has no key either, rather than
        /// a key that presses and reports a refusal every time.
        /// </summary>
        private bool CarriesAnchor
        {
            get
            {
                EnsureRefs();
                return _boat != null && AnchorMath.CarriesAnchor(_boat.Hull);
            }
        }

        /// <summary>
        /// Is this the hull the player is actually on? Asked of the Core declaration
        /// (<see cref="HelmSlot.IsPlayersBoat"/>) — the boat she is ABOARD, which is deliberately a
        /// wider fact than the boat she is at the HELM of.
        ///
        /// <para><b>⚠ It must not be the helm question, and this key is where that goes wrong most
        /// easily.</b> The starting dory is ROWED: she has no helm at all (<c>HasHelm</c> is the engine
        /// question), so a helm-shaped test would kill the anchor on the very boat the game opens with —
        /// which is precisely the hull this key exists for. And stepping back from the wheel onto the
        /// deck gives up the helm, not the boat; this key lives on the deck by design ("anchoring is a
        /// boat job either way", above).</para>
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
                EnsureRefs();
                return GameServices.Helm.IsPlayersBoat(_boat);
            }
        }

        private void Awake() => EnsureRefs();

        /// <summary>Resolve the siblings lazily, so the gate answers correctly even when a rig wired this
        /// component up before <see cref="Awake"/> ran (<see cref="BoatAnchor"/>'s own lazy-resolve
        /// precedent).</summary>
        private void EnsureRefs()
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

            EnsureRefs();
            if (_anchor != null) _anchor.Toggle();
        }
    }
}
