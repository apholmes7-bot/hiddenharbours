using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// PLACEHOLDER dev controls so you can feel the boat in the greybox. The scheme follows the active
    /// hull's <see cref="PropulsionType"/>:
    ///   • Oars (the dory) — per the owner's rowing table (each oar is forward +1 / back -1 / idle 0):
    ///     W = both ahead · S = both astern · A = port-oar stroke · D = starboard-oar stroke ·
    ///     W+A = port oar only ahead · W+D = stbd only · S+A = port only astern · S+D = stbd only ·
    ///     A or D with no W/S = a stationary pivot (oars opposite) · Space = brace (both oars → brake).
    ///   • Engine (boats you buy): W/S = throttle ahead/astern, A/D = steer (UNCHANGED helm).
    /// To ship, replace this with the control scheme through an InputService (design/ux-and-mobile-
    /// controls.md, owned by ui-ux); a gamepad maps analog oar effort straight to BoatController.SetOarInput.
    ///
    /// Uses the new Input System (Keyboard.current), matching this project's input setting.
    /// </summary>
    [RequireComponent(typeof(BoatController))]
    public class DevBoatInput : MonoBehaviour
    {
        private BoatController _boat;

        private void Awake() => _boat = GetComponent<BoatController>();

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _boat == null) return;

            // The propulsion branch is the SAME decision the controller's physics uses (one source of
            // truth in BoatController.UsesEngineHelm) so input + physics can never disagree about a hull:
            // the Punt (Engine) gets the outboard helm, the Dory (Oars) keeps per-oar rowing.
            BoatHullDef hull = _boat.Hull;
            if (hull == null || BoatController.UsesEngineHelm(hull.Propulsion))
                ReadEngine(kb);
            else
                ReadOars(kb);
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

        // Engine helm — UNCHANGED: W/S = throttle (S/Down = astern), A/D = steer.
        private void ReadEngine(Keyboard kb)
        {
            float throttle = ((kb.wKey.isPressed || kb.upArrowKey.isPressed) ? 1f : 0f)
                           - ((kb.sKey.isPressed || kb.downArrowKey.isPressed) ? 1f : 0f);
            float steer = ((kb.dKey.isPressed || kb.rightArrowKey.isPressed) ? 1f : 0f)
                        - ((kb.aKey.isPressed || kb.leftArrowKey.isPressed) ? 1f : 0f);
            _boat.SetControl(throttle, steer);
        }
    }
}
