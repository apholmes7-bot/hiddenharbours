using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// PLACEHOLDER dev controls so you can feel the dory in the greybox. Up/W = ahead, Down/S = astern
    /// (reverse — back onto the dock to disembark), Left-Right/A-D = steer. To ship, replace this with
    /// the mobile control scheme through an InputService (design/ux-and-mobile-controls.md, owned by ui-ux).
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
            if (kb == null) return;
            float throttle = ((kb.wKey.isPressed || kb.upArrowKey.isPressed) ? 1f : 0f)
                           - ((kb.sKey.isPressed || kb.downArrowKey.isPressed) ? 1f : 0f);  // S/Down = astern
            float steer = ((kb.dKey.isPressed || kb.rightArrowKey.isPressed) ? 1f : 0f)
                        - ((kb.aKey.isPressed || kb.leftArrowKey.isPressed) ? 1f : 0f);
            _boat.SetControl(throttle, steer);
        }
    }
}
