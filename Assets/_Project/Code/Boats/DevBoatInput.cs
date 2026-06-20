using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// PLACEHOLDER dev controls so you can feel the dory in the greybox. Up/W = throttle,
    /// Left-Right/A-D = steer. To ship, replace this with the mobile control scheme through an
    /// InputService (design/ux-and-mobile-controls.md, owned by ui-ux).
    ///
    /// Uses the legacy Input Manager — in Project Settings &gt; Player set
    /// "Active Input Handling" to "Both" (or "Input Manager (Old)") for this to run.
    /// </summary>
    [RequireComponent(typeof(BoatController))]
    public class DevBoatInput : MonoBehaviour
    {
        private BoatController _boat;

        private void Awake() => _boat = GetComponent<BoatController>();

        private void Update()
        {
            float throttle = Mathf.Clamp01(Input.GetAxisRaw("Vertical")); // forward only
            float steer = Input.GetAxisRaw("Horizontal");
            _boat.SetControl(throttle, steer);
        }
    }
}
