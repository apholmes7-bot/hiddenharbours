using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// PLACEHOLDER dev controls so you can feel the boat in the greybox. The scheme follows the active
    /// hull's <see cref="PropulsionType"/>:
    ///   • Oars (the dory) — per the owner's rowing table (each oar is forward +1 / back -1 / idle 0):
    ///     W = both ahead · S = both astern · A = port-oar stroke · D = starboard-oar stroke ·
    ///     W+A = port oar only ahead · W+D = stbd only · S+A = port only astern · S+D = stbd only ·
    ///     A or D with no W/S = a stationary pivot (oars opposite) · Space = brace (both oars → brake).
    ///     UNCHANGED — the stepped throttle below is motorised hulls ONLY (owner directive 2026-08-03).
    ///   • Engine (boats you buy) — the STEPPED-AND-HELD notched throttle (ADR 0025 S1, owner directive
    ///     2026-08-03: a key can't hold an analog position, so each press bumps a detent and the drive
    ///     STAYS there): W/Up press = +1 detent, S/Down press = −1 detent, the drive HOLDS between
    ///     presses; holding a key auto-repeats after a data-driven delay (GameConfig.HelmThrottle);
    ///     Z snaps to neutral. A/D = steer — momentary, but the COMMAND is eased toward full lock
    ///     over GameConfig.HelmWheel.KeySteerSecondsToLock (S4.5: the mirrored wheel turns gradually
    ///     and the rudder follows the same curve). Gamepad rides the SAME actions: D-pad up/down =
    ///     detents, B (east) = neutral, left stick X = steer (analog, un-eased).
    /// The drive value lives in <see cref="BoatController"/> alone (read back through
    /// <see cref="BoatController.Throttle"/> each frame) — this component holds only repeat TIMERS,
    /// so the mouse drag path (HelmControlRelay) and these keys can never fight over a second copy.
    /// To ship, replace this with the control scheme through an InputService (design/ux-and-mobile-
    /// controls.md, owned by ui-ux); a gamepad maps analog oar effort straight to BoatController.SetOarInput.
    ///
    /// Uses the new Input System (Keyboard.current/Gamepad.current), matching this project's input setting.
    /// </summary>
    [RequireComponent(typeof(BoatController))]
    public class DevBoatInput : MonoBehaviour
    {
        [Header("Keys (owner-editable)")]
        [Tooltip("Snap the notched throttle straight to NEUTRAL (motorised hulls). Z — verified free " +
                 "of every other binding by a project-wide Key./KeyControl/.inputactions sweep " +
                 "(WASD/arrows helm, Space brace/haul, E interact, Q mooring, P buy, B sell, " +
                 "T trap-drop, G grant, H haul, Y auto-yaw, L spotlight/ice, F fleet/freezer/bucket, " +
                 "V variant, I icebox, O displaced-water, N tide table, X DUMP SPOILED — " +
                 "CatchDumpInput listens scene-wide, so X here would dump the catch on every chop " +
                 "to neutral).")]
        [SerializeField] private Key _neutralKey = Key.Z;

        private BoatController _boat;
        private HelmControlRelay _relay;   // steer-session arbitration (S2a) — same GameObject

        // Auto-repeat timers for the held throttle keys (transient input state — never saved, rule 5).
        private HeldRepeatState _aheadRepeat;
        private HeldRepeatState _asternRepeat;

        // The eased key-steer command (S4.5) — where the walk toward the keys' target has reached.
        // Transient input state, never saved (rule 5); synced to the boat's held steer whenever the
        // keys are not the channel's owner, so a takeover never snaps.
        private float _steerEase;

        private void Awake()
        {
            _boat = GetComponent<BoatController>();
            _relay = GetComponent<HelmControlRelay>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            if ((kb == null && gp == null) || _boat == null) return;

            // The propulsion branch is the SAME decision the controller's physics uses (one source of
            // truth in BoatController.UsesEngineHelm) so input + physics can never disagree about a hull:
            // the Punt (Engine) gets the outboard helm, the Dory (Oars) keeps per-oar rowing.
            BoatHullDef hull = _boat.Hull;
            if (hull == null || BoatController.UsesEngineHelm(hull.Propulsion))
                ReadEngine(kb, gp);
            else if (kb != null)
                ReadOars(kb);
        }

        /// <summary>
        /// The wheel steer-session arbitration (S2a, the <see cref="HiddenHarbours.Core.IHelmControl"/>
        /// contract): while the focused wheel holds a live session, a ZERO momentary read PRESERVES
        /// the wheel's held steer instead of stomping it back to centre; a REAL key/stick input ends
        /// the session and takes the channel back — one decisive handover, keys win, never a
        /// per-frame fight. Pure + static so the truth table is EditMode-testable without input
        /// devices (headless batchmode drops key events, so the PlayMode key-press check can only
        /// run opportunistically).
        /// </summary>
        public static float ArbitrateSteer(float momentarySteer, bool sessionActive, float heldSteer,
                                           out bool endSession)
        {
            endSession = false;
            if (!sessionActive) return momentarySteer;
            if (momentarySteer == 0f) return heldSteer;
            endSession = true;
            return momentarySteer;
        }

        /// <summary>
        /// The eased steer walk (S4.5, owner ask 3: "the steering wheel needs to follow the turning
        /// from the arrow keys — gradual and smooth"). The COMMAND is eased, not the wheel graphic —
        /// if only the picture eased, the wheel would transiently show less lock than the rudder has
        /// (a lying instrument); easing the command keeps the existing mirror exact and gives the
        /// boat a progressive key-steer feel. Linear walk at 1/<paramref name="secondsToFullLock"/>
        /// per second (centre→lock in that time; a full reversal sweeps through centre in ≈2×), and
        /// it SETTLES EXACTLY — within one step of the target it returns the target, so the mirrored
        /// wheel's change key stops moving and the dash stops repainting. <c>secondsToFullLock ≤ 0</c>
        /// = instant (the pre-S4.5 momentary snap, and what a stale GameConfig.asset row degrades
        /// to). Pure + injected dt: PlayMode frame count is NOT time, so the maths never reads a
        /// clock of its own.
        /// </summary>
        public static float EaseSteer(float current, float target, float dt, float secondsToFullLock)
        {
            if (secondsToFullLock <= 0f) return target;
            if (dt < 0f) dt = 0f;
            float maxStep = dt / secondsToFullLock;
            float delta = target - current;
            if (delta > maxStep) return current + maxStep;
            if (delta < -maxStep) return current - maxStep;
            return target;
        }

        /// <summary>
        /// The whole per-frame steer decision (S4.5) — arbitration + ease as ONE pure function so the
        /// truth table pins in EditMode. <see cref="ArbitrateSteer"/> runs on the RAW momentary read
        /// (keys, else stick), so the eased tail after a key release can never read as "real input"
        /// and break a wheel session it should not:
        /// <list type="bullet">
        /// <item><b>Session live, raw zero</b> → the wheel's held steer stands, and the ease state is
        /// SYNCED to it — any eased tail dies, and a later key press starts from the wheel's lock
        /// (taking over must not snap).</item>
        /// <item><b>Keys down</b> → the command eases toward ±1 (from the held steer on the frame a
        /// key breaks a session; from the running ease otherwise). Keys win over the stick, as
        /// before.</item>
        /// <item><b>Stick deflected</b> → analog passes through UN-eased (easing a stick only adds
        /// lag) and the ease state tracks it.</item>
        /// <item><b>Nothing</b> → the command eases back to centre (keys stay momentary — the return
        /// is just as gradual as the turn).</item>
        /// </list>
        /// </summary>
        public static float ComposeSteer(float keySteer, float stickSteer, bool sessionActive,
                                         float heldSteer, float easeFrom, float dt,
                                         float secondsToFullLock,
                                         out float easeNext, out bool endSession)
        {
            float momentary = keySteer != 0f ? keySteer : stickSteer;
            float arbitrated = ArbitrateSteer(momentary, sessionActive, heldSteer, out endSession);
            if (sessionActive && !endSession)
            {
                easeNext = arbitrated;              // the wheel holds; the ease tracks its steer
                return arbitrated;
            }
            if (keySteer != 0f)
            {
                float from = endSession ? heldSteer : easeFrom;   // wheel→keys handover: no snap
                easeNext = EaseSteer(from, keySteer, dt, secondsToFullLock);
                return easeNext;
            }
            if (stickSteer != 0f)
            {
                easeNext = stickSteer;              // analog stick: straight through
                return stickSteer;
            }
            easeNext = EaseSteer(easeFrom, 0f, dt, secondsToFullLock);
            return easeNext;
        }

        /// <summary>
        /// Map the keyboard combo to each oar's stroke state (forward +1 / backward -1 / idle 0), per the
        /// owner's rowing table. W/S drive both oars ahead/astern; A engages the PORT (left) oar, D the
        /// STARBOARD (right). A one-sided key rows just that oar in the W/S direction; with no W/S it rows
        /// that oar forward and back-waters the other for a stationary pivot. Both (or neither) of A/D →
        /// both oars track the W/S drive. Pure + static so the table is unit-testable without the input loop.
        /// </summary>
        public static (float left, float right) OarStateFor(bool ahead, bool astern, bool portKey, bool stbdKey)
        {
            float drive = (ahead ? 1f : 0f) - (astern ? 1f : 0f);   // -1 / 0 / +1
            bool portOnly = portKey && !stbdKey;
            bool stbdOnly = stbdKey && !portKey;
            if (portOnly) return drive != 0f ? (drive, 0f) : (1f, -1f);   // port oar in drive dir, else pivot bow-right
            if (stbdOnly) return drive != 0f ? (0f, drive) : (-1f, 1f);   // stbd oar in drive dir, else pivot bow-left
            return (drive, drive);                                        // both oars together (or A+D cancel) → straight
        }

        // Differential hand-rowing (the dory): each oar's state comes from the combo table, then drives
        // the per-oar physics surface. Space braces both oars (a strong braking drag).
        private void ReadOars(Keyboard kb)
        {
            bool ahead  = kb.wKey.isPressed || kb.upArrowKey.isPressed;
            bool astern = kb.sKey.isPressed || kb.downArrowKey.isPressed;
            bool portKey = kb.aKey.isPressed || kb.leftArrowKey.isPressed;
            bool stbdKey = kb.dKey.isPressed || kb.rightArrowKey.isPressed;
            var (left, right) = OarStateFor(ahead, astern, portKey, stbdKey);
            _boat.SetOarInput(left, right, kb.spaceKey.isPressed);   // Space = brace = brake/stop
        }

        // Engine helm — the STEPPED-AND-HELD notched throttle (owner directive 2026-08-03). Presses
        // step a detent; the drive HOLDS between presses (read back from the controller — the ONE
        // owner); held keys walk on after a data-driven delay; X (or gamepad B) chops to neutral.
        // Steer stays momentary, but the key COMMAND is eased toward lock (S4.5); the stick is analog.
        private void ReadEngine(Keyboard kb, Gamepad gp)
        {
            HelmThrottleSettings cfg = GameServices.HelmThrottle;
            HelmControlRelay.EffectiveNotches(_boat.Hull, in cfg, out int aheadN, out int asternN);

            bool aheadEdge  = (kb != null && (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame))
                           || (gp != null && gp.dpad.up.wasPressedThisFrame);
            bool aheadHeld  = (kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed))
                           || (gp != null && gp.dpad.up.isPressed);
            bool asternEdge = (kb != null && (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame))
                           || (gp != null && gp.dpad.down.wasPressedThisFrame);
            bool asternHeld = (kb != null && (kb.sKey.isPressed || kb.downArrowKey.isPressed))
                           || (gp != null && gp.dpad.down.isPressed);
            bool neutral    = (kb != null && kb[_neutralKey].wasPressedThisFrame)
                           || (gp != null && gp.buttonEast.wasPressedThisFrame);

            float dt = Time.deltaTime;
            int steps = HelmThrottleStepMath.TickRepeat(ref _aheadRepeat, aheadEdge, aheadHeld,
                                                        dt, cfg.HoldRepeatDelaySec, cfg.HoldRepeatPerSec)
                      - HelmThrottleStepMath.TickRepeat(ref _asternRepeat, asternEdge, asternHeld,
                                                        dt, cfg.HoldRepeatDelaySec, cfg.HoldRepeatPerSec);

            // The drive is read back from the controller — whoever moved it last (these keys, the
            // gamepad, or the overlay's mouse drag) — then stepped. No second copy, no drift.
            float drive = _boat.Throttle;
            if (neutral) drive = 0f;
            else if (steps != 0) drive = HelmThrottleStepMath.StepMany(drive, steps, aheadN, asternN);

            float keySteer = ((kb != null && (kb.dKey.isPressed || kb.rightArrowKey.isPressed)) ? 1f : 0f)
                           - ((kb != null && (kb.aKey.isPressed || kb.leftArrowKey.isPressed)) ? 1f : 0f);
            float stickSteer = gp != null ? gp.leftStick.x.ReadValue() : 0f;

            // Wheel steer-session arbitration (S2a) + the eased key steer (S4.5) — one pure step.
            // Arbitration sees the RAW keys; the eased command is what reaches the controller, so
            // the mirrored wheel turns gradually and stays an exact mirror of the rudder.
            if (_relay == null) _relay = GetComponent<HelmControlRelay>();
            bool sessionActive = _relay != null && _relay.SteerDragActive;
            float steer = ComposeSteer(keySteer, stickSteer, sessionActive, _boat.Steer, _steerEase,
                                       dt, GameServices.HelmWheel.KeySteerSecondsToLock,
                                       out _steerEase, out bool endSession);
            if (endSession) _relay.EndSteerDrag();

            _boat.SetControl(drive, steer);
        }
    }
}
