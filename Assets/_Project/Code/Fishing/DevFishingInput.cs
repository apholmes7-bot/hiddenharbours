using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// PLACEHOLDER one-thumb fishing input for the greybox: Space is the single fishing action.
    /// Press it to cast; after a bite, HOLD to reel and RELEASE to ease — pulse to land the fish before
    /// the line snaps. Feeds the held state to <see cref="FishingController.Tick"/> every frame. Replace
    /// with the real touch Action button via the InputService later (ui-ux, the Haul(hold/release) intent).
    /// </summary>
    [RequireComponent(typeof(FishingController))]
    public class DevFishingInput : MonoBehaviour
    {
        private FishingController _fishing;

        private void Awake() => _fishing = GetComponent<FishingController>();

        private void Update()
        {
            bool held = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            _fishing.Tick(Time.deltaTime, held);
        }
    }
}
