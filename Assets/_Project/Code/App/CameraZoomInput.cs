using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.App
{
    /// <summary>
    /// The device half of the player's zoom (owner ruling 2026-08-19, <i>"mouse wheel modifies player
    /// zoom — closer to look at interiors, out when outside"</i>): it reads the wheel, turns it into
    /// whole tier notches, and hands them to <see cref="CameraFollow.NudgePlayerZoom"/>. Deliberately
    /// tiny and opinion-free — every rule about WHICH tiers exist, when the wheel is live, and where the
    /// range ends lives in <see cref="CameraZoomPolicy"/> and the camera, the same split
    /// <c>TidePanelInput</c> keeps from <c>TidePanel</c>.
    ///
    /// <para><b>The binding audit.</b> The mouse wheel was unread anywhere in the project — no gameplay
    /// or UI component touched <c>Mouse.scroll</c>, and the only claim on it is the stock
    /// <c>InputSystem_Actions</c> UI map's ScrollWheel, which serves the EventSystem and not the world.
    /// On the pad, <b>LB / RB</b> (shoulders) are the one pair unclaimed in BOTH code and the actions
    /// asset: the dpad's up/down are the helm throttle (<c>DevBoatInput</c>) and the wardrobe's row nav,
    /// the sticks steer and walk, and the four face buttons are Confirm/Cancel/interact. Shoulders also
    /// FIT: a discrete press is the right shape for a discrete tier, where a stick axis would need a
    /// deadzone and a repeat rate to say the same thing. (The exhausted A–Z dev-key ledger is about
    /// keyboard LETTERS and does not bear on either of these.)</para>
    ///
    /// <para><b>New Input System only</b> — legacy <c>UnityEngine.Input</c> compiles in this project and
    /// then throws at runtime, the wart <c>TidePanelInput</c> documents.</para>
    ///
    /// <para><b>Presentation, never simulation (rule 5).</b> Nothing here publishes a signal, touches
    /// the clock, or writes the save. A wheel turn changes what is on screen and nothing else, which is
    /// why the determinism guard can assert that no sim input ever reads the zoom.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraZoomInput : MonoBehaviour
    {
        [Tooltip("The camera whose walking framing the wheel moves. Left empty, it finds the " +
                 "CameraFollow on this same object — which is how both builders wire it.")]
        [SerializeField] private CameraFollow _camera;

        [Tooltip("Let the mouse wheel step the walking view. The owner's master switch is " +
                 "GameConfig.PlayerZoom.WheelEnabled; this is the per-rig one.")]
        [SerializeField] private bool _wheelSteps = true;

        [Tooltip("Gamepad button that steps the walking view OUT one tier (wider). LB by default — " +
                 "audited free of every other binding in code and in InputSystem_Actions.")]
        [SerializeField] private GamepadButton _zoomOutButton = GamepadButton.LeftShoulder;

        [Tooltip("Gamepad button that steps the walking view IN one tier (closer). RB by default.")]
        [SerializeField] private GamepadButton _zoomInButton = GamepadButton.RightShoulder;

        // Un-spent scroll, carried between frames so a trackpad's fine-grained stream earns tiers at
        // the same rate a wheel's detents do. Cleared whenever the wheel is not live, so a modal can
        // never bank scroll that fires the instant it closes.
        private float _scrollCarry;

        private void Awake()
        {
            if (_camera == null) _camera = GetComponent<CameraFollow>();
        }

        private void Update()
        {
            if (_camera == null) return;

            // Asked BEFORE the device is read, on purpose: a blocked wheel must also bank nothing.
            if (!_camera.WheelIsLive) { _scrollCarry = 0f; return; }

            int notches = 0;

            if (_wheelSteps)
            {
                Mouse mouse = Mouse.current;
                if (mouse != null)
                    notches += CameraZoomPolicy.NotchesFromScroll(
                        ref _scrollCarry, mouse.scroll.ReadValue().y, _camera.WheelUnitsPerNotch);
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                if (pad[_zoomInButton].wasPressedThisFrame) notches += 1;   // +1 tier = closer
                if (pad[_zoomOutButton].wasPressedThisFrame) notches -= 1;
            }

            if (notches != 0) _camera.NudgePlayerZoom(notches);
        }
    }
}
