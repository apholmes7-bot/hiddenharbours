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
    ///     Z snaps to neutral. A/D = steer: still MOMENTARY (release returns to centre), but the keys
    ///     now set a TARGET the commanded steer EASES toward over GameConfig.HelmWheel
    ///     .SteerEaseSeconds (S4.5) instead of slamming lock-to-lock in a frame — so the dash wheel,
    ///     which mirrors the steer, winds round like a wheel. Gamepad rides the SAME actions: D-pad
    ///     up/down = detents, B (east) = neutral, left stick X = steer (analog, passed through undamped).
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
                 "(WASD/arrows helm, Space brace/haul, E interact, Q mooring, " +
                 "T trap-drop, G grant, H haul, Y auto-yaw, L spotlight/ice, F fleet/freezer/bucket, " +
                 "V variant, I icebox, O displaced-water, N tide table, R anchor/rode, X DUMP SPOILED — " +
                 "CatchDumpInput listens scene-wide, so X here would dump the catch on every chop " +
                 "to neutral).")]
        [SerializeField] private Key _neutralKey = Key.Z;

        private BoatController _boat;
        private HelmControlRelay _relay;   // steer-session arbitration (S2a) — same GameObject

        // Auto-repeat timers for the held throttle keys (transient input state — never saved, rule 5).
        private HeldRepeatState _aheadRepeat;
        private HeldRepeatState _asternRepeat;

        private void Awake()
        {
            _boat = GetComponent<BoatController>();
            _relay = GetComponent<HelmControlRelay>();
        }

        /// <summary>
        /// True while these helm KEYS may actually reach the hull — false while a text field owns the
        /// keyboard (<see cref="HiddenHarbours.Core.HelmKeyCapture"/>, ADR 0025 S6 PR 4: naming a
        /// waypoint on the chartplotter's MAX face types W/A/S/D like any other letters).
        ///
        /// <para>Exposed as a property for the reason <c>DevFishingInput.GearKeysLive</c> and
        /// <c>TrapHaulController.GearKeysLive</c> are: headless batchmode drops key events, so a test
        /// cannot press W and watch the rudder — but it CAN assert that the gate the read consults says
        /// the right thing, in both directions, on the live component.</para>
        /// </summary>
        public bool HelmKeysLive => !HiddenHarbours.Core.HelmKeyCapture.IsCapturing;

        private void Update()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            if ((kb == null && gp == null) || _boat == null) return;

            // A text field owns the keyboard (naming a waypoint on the plotter) — the helm's KEY reads
            // go deaf for as long as it does, on the RAW momentary read, before the steer ease and
            // before the wheel-session arbitration below (HelmKeyCapture's remarks say why that
            // ordering is the load-bearing part). The GAMEPAD stays live: you cannot type on one.
            if (!HelmKeysLive) kb = null;
            if (kb == null && gp == null) return;

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
        // Steer stays momentary: keys full-lock, gamepad stick analog.
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

            // Steer keys are a momentary TARGET (−1/0/+1), not the command itself (S4.5).
            float target = ((kb != null && (kb.dKey.isPressed || kb.rightArrowKey.isPressed)) ? 1f : 0f)
                         - ((kb != null && (kb.aKey.isPressed || kb.leftArrowKey.isPressed)) ? 1f : 0f);
            bool analog = false;
            if (gp != null && target == 0f)
            {
                float stick = gp.leftStick.x.ReadValue();
                // A DEFLECTED stick is already an analog position — easing it would only add lag, so
                // it goes straight through. At rest it is indistinguishable from "no key", and falls
                // through to the ease, which winds a partial deflection out in a fraction of a lock.
                if (stick != 0f) { target = stick; analog = true; }
            }

            // Wheel steer-session arbitration (S2a, the IHelmControl contract) runs on the RAW
            // momentary read, BEFORE the ease: the eased tail after a key release is this component's
            // own follow-through, not a fresh input, and must never read as one and break a wheel
            // session it never touched.
            if (_relay == null) _relay = GetComponent<HelmControlRelay>();
            bool sessionActive = _relay != null && _relay.SteerDragActive;
            target = ArbitrateSteer(target, sessionActive, _boat.Steer, out bool endSession);
            if (endSession) _relay.EndSteerDrag();

            // Ease the COMMAND toward the target — never the wheel graphic, which mirrors Steer and
            // would otherwise show less lock than the rudder has (a lying instrument). The ease reads
            // the live steer back from the ONE owner rather than keeping a copy, so taking the channel
            // over from a held wheel session starts at the wheel's own angle and cannot snap, and a
            // live session (whose arbitrated target IS the held steer) is passed through untouched.
            float steer = analog
                ? target
                : HelmSteerEase.Step(_boat.Steer, target, dt, GameServices.HelmWheel.SteerEaseSeconds);

            _boat.SetControl(drive, steer);
        }
    }
}
