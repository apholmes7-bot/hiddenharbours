using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// PLACEHOLDER: press P ("Punt") to buy the boat offered at the Shipwright — but ONLY while ON FOOT
    /// and within reach of THIS Shipwright (buying no longer fires from anywhere). Proximity mirrors the
    /// dock-zone test: the on-foot player must be within range of the stall's own transform. Works in
    /// both regions (each region's Shipwright gates on its own position). ui-ux replaces this with the
    /// real Shipwright buy screen / Interact intent later.
    /// </summary>
    [RequireComponent(typeof(Shipwright))]
    public class DevBuyInput : MonoBehaviour
    {
        [Tooltip("On-foot + in-range gate: P only buys when the walking player is at this Shipwright.")]
        [SerializeField] private StallReach _reach = new StallReach();

        private Shipwright _shipwright;

        private void Awake() => _shipwright = GetComponent<Shipwright>();
        private void OnEnable() => _reach.Enable();
        private void OnDisable() => _reach.Disable();

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.pKey.wasPressedThisFrame) return;

            if (_reach.CanInteract(transform.position))
                _shipwright.TryBuy();
            else if (_reach.OnFoot)
                Debug.Log("[Shipwright] Too far — step up to the Shipwright to buy.");
        }
    }
}
