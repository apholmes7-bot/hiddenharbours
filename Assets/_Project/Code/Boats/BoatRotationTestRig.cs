using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// DEV-ONLY runtime glue for the boat-rotation A/B prototype (paired with <see cref="DirectionalBoatSprite"/>,
    /// spawned by the "Hidden Harbours/Build Boat-Rotation Test" menu item). It does two owner-facing things,
    /// nothing else:
    ///
    ///   • <b>Live mode toggle</b> — a key (default <c>T</c>) flips the test boat between SnapDirectional
    ///     (swap N/E/S/W facings, picture stays screen-aligned) and SmoothRotateSingle (one sprite rotates
    ///     with the hull), so the owner can feel the 90° snap vs a smooth rotate back-to-back.
    ///   • <b>Optional slow auto-yaw</b> — turns the boat slowly on its own (toggle with <c>Y</c>) so he can
    ///     just press Play and WATCH it come around without touching the helm. Auto-yaw rotates the transform
    ///     directly (it's a visual harness, not the physics feel test), and yields to any manual helm input.
    ///
    /// It is additive and reversible: it lives only on the spawned test boat and never touches the real
    /// Dory/Punt. Keys are owner-editable fields. Uses the new Input System (<c>Keyboard.current</c>),
    /// matching <see cref="DevBoatInput"/>.
    /// </summary>
    public class BoatRotationTestRig : MonoBehaviour
    {
        [Header("The prototype this rig drives")]
        [SerializeField] private DirectionalBoatSprite _directional;

        [Header("Keys (owner-editable)")]
        [Tooltip("Flip SnapDirectional <-> SmoothRotateSingle live, to compare snap vs smooth.")]
        [SerializeField] private Key _toggleModeKey = Key.T;
        [Tooltip("Turn the slow auto-yaw on/off (watch the boat come around hands-free).")]
        [SerializeField] private Key _toggleAutoYawKey = Key.Y;

        [Header("Auto-yaw (hands-free demo)")]
        [Tooltip("Start with auto-yaw running so pressing Play immediately shows the boat turning.")]
        [SerializeField] private bool _autoYawOn = true;
        [Tooltip("How fast auto-yaw turns the boat (degrees/sec). Slow by design so the snap is easy to read.")]
        [SerializeField] private float _autoYawDegPerSec = 24f;

        private void Reset() => _directional = GetComponent<DirectionalBoatSprite>();

        private void Awake()
        {
            if (_directional == null) _directional = GetComponent<DirectionalBoatSprite>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (_directional != null && kb[_toggleModeKey].wasPressedThisFrame)
            {
                var mode = _directional.ToggleMode();
                Debug.Log($"[BoatRotationTest] Mode -> {mode}  (press {_toggleModeKey} to toggle)");
            }

            if (kb[_toggleAutoYawKey].wasPressedThisFrame)
            {
                _autoYawOn = !_autoYawOn;
                Debug.Log($"[BoatRotationTest] Auto-yaw -> {(_autoYawOn ? "ON" : "OFF")}  (press {_toggleAutoYawKey} to toggle)");
            }

            // Slow hands-free turn. Yields to a manual helm input so the owner can grab the wheel any time.
            if (_autoYawOn && !HelmTouched(kb))
                transform.Rotate(0f, 0f, _autoYawDegPerSec * Time.deltaTime);
        }

        // True while the owner is actively steering (A/D or arrows) — auto-yaw stands down so it doesn't fight him.
        private static bool HelmTouched(Keyboard kb)
            => kb.aKey.isPressed || kb.dKey.isPressed || kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed;
    }
}
