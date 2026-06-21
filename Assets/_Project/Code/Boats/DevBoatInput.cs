using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// PLACEHOLDER dev controls so you can feel the boat in the greybox. The scheme follows the active
    /// hull's <see cref="PropulsionType"/>:
    ///   • Oars (the dory): W/S = both oars ahead/astern, A = a LEFT-oar stroke, D = a RIGHT-oar stroke
    ///     (held = a sustained pull; one-sided = turn the OTHER way), Space = brace the oars (brake).
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

            BoatHullDef hull = _boat.Hull;
            if (hull != null && hull.Propulsion == PropulsionType.Oars)
                ReadOars(kb);
            else
                ReadEngine(kb);
        }

        // Differential hand-rowing (the dory). W/S drive both oars; A/D add a one-sided pull → yaw.
        // Holding an oar key is a sustained pull; a tap is one pull. A gamepad would feed analog effort.
        private void ReadOars(Keyboard kb)
        {
            float both = ((kb.wKey.isPressed || kb.upArrowKey.isPressed) ? 1f : 0f)
                       - ((kb.sKey.isPressed || kb.downArrowKey.isPressed) ? 1f : 0f);
            float left  = Mathf.Clamp(both + ((kb.aKey.isPressed || kb.leftArrowKey.isPressed)  ? 1f : 0f), -1f, 1f);
            float right = Mathf.Clamp(both + ((kb.dKey.isPressed || kb.rightArrowKey.isPressed) ? 1f : 0f), -1f, 1f);
            bool brace = kb.spaceKey.isPressed;   // oars braced = brake/stop
            _boat.SetOarInput(left, right, brace);
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
