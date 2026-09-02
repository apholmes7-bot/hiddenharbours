using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.App
{
    /// <summary>
    /// The device half of the player's zoom (owner ruling 2026-08-19, <i>"mouse wheel modifies player
    /// zoom — closer to look at interiors, out when outside"</i>): it reads the wheel, turns it into
    /// whole tier notches, and hands them to <see cref="CameraFollow.NudgePlayerZoom"/>. Deliberately
    /// tiny and opinion-free — every rule about WHICH tiers exist, when the wheel is live, where the
    /// range ends, and whether a notch moves the walker's rung or her offset from a ruled framing lives
    /// in <see cref="CameraZoomPolicy"/> and the camera, the same split <c>TidePanelInput</c> keeps from
    /// <c>TidePanel</c>. This class has not changed for the 2026-08-22 aboard-band ruling and did not
    /// need to, which is the split earning its keep.
    ///
    /// <para><b>The binding audit.</b> The mouse wheel was unread anywhere in the project — no gameplay
    /// or UI component touched <c>Mouse.scroll</c>, and the only claim on it is the <c>UI</c> map's
    /// ScrollWheel in <c>HiddenHarbours.inputactions</c>, which serves the EventSystem and not the world
    /// (the tiered <c>Zoom</c> intent joins that asset's <c>UI</c> map in the input lane's PR 2, ADR 0043).
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
        [Tooltip("The camera whose framing the wheel moves. Left empty, it finds the " +
                 "CameraFollow on this same object — which is how both builders wire it.")]
        [SerializeField] private CameraFollow _camera;

        [Tooltip("Let the mouse wheel step the view. The owner's master switch is " +
                 "GameConfig.PlayerZoom.WheelEnabled; this is the per-rig one.")]
        [SerializeField] private bool _wheelSteps = true;

        [Tooltip("Let the pad's shoulder buttons step the view — LB out (wider), RB in " +
                 "(closer), one tier per press. Off = mouse wheel only.")]
        [SerializeField] private bool _padShouldersStep = true;

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

            // ⚠️ NAMED CONTROLS, NOT A SERIALIZED GamepadButton. The first cut serialized the binding
            // the way TidePanelInput serializes its Key — but `GamepadButton` lives in
            // UnityEngine.InputSystem.LowLevel, and dragging a LowLevel namespace into a gameplay
            // component to buy a rebind nobody asked for is the wrong trade. Keyboard letters are
            // scarce and contested (the exhausted A–Z ledger), which is why THAT binding is
            // serialized; the pad's shoulders are neither, and the binding decision is recorded in
            // the audit above and in the design doc.
            Gamepad pad = Gamepad.current;
            if (pad != null && _padShouldersStep)
            {
                if (pad.rightShoulder.wasPressedThisFrame) notches += 1;   // RB: +1 tier = closer
                if (pad.leftShoulder.wasPressedThisFrame) notches -= 1;    // LB: one tier wider
            }

            if (notches != 0) _camera.NudgePlayerZoom(notches);
        }
    }
}
