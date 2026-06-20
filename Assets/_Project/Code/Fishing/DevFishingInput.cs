using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// PLACEHOLDER: press Space to cast, so you can test the catch loop in the greybox. Replace with
    /// the touch fishing interaction through the InputService later (ui-ux).
    /// </summary>
    [RequireComponent(typeof(FishingController))]
    public class DevFishingInput : MonoBehaviour
    {
        private FishingController _fishing;

        private void Awake() => _fishing = GetComponent<FishingController>();

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                _fishing.TryCast();
        }
    }
}
