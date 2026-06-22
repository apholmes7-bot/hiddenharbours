using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// PLACEHOLDER: press B ("buyer") to sell your hold at the wharf — but ONLY while ON FOOT and within
    /// reach of THIS Fish Buyer stall (selling no longer fires from anywhere). Proximity mirrors the
    /// dock-zone test: the on-foot player must be within range of the stall's own transform. Works in
    /// both regions (each region's stall gates on its own position). ui-ux replaces this with the real
    /// Interact intent through the InputService later.
    /// </summary>
    [RequireComponent(typeof(WharfSellPoint))]
    public class DevSellInput : MonoBehaviour
    {
        [Tooltip("On-foot + in-range gate: B only sells when the walking player is at this stall.")]
        [SerializeField] private StallReach _reach = new StallReach();

        private WharfSellPoint _wharf;

        private void Awake() => _wharf = GetComponent<WharfSellPoint>();
        private void OnEnable() => _reach.Enable();
        private void OnDisable() => _reach.Disable();

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.bKey.wasPressedThisFrame) return;

            if (_reach.CanInteract(transform.position))
                _wharf.Sell();
            else if (_reach.OnFoot)
                Debug.Log("[Wharf] Too far — step up to the Fish Buyer to sell.");
        }
    }
}
