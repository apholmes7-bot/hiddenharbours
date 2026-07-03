using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// PLACEHOLDER INPUT (the screen it opens is real): press P while ON FOOT and within reach of a
    /// stall to open the <see cref="BuyScreen"/> for it — browse the stall's offers, see prices and
    /// what you can afford, and Confirm to buy through the vendors' existing seams. This replaces the
    /// old blind insta-<c>TryBuy()</c> keypress (VS-16): the same key, the same proximity gate, but the
    /// purchase now goes through a real screen. The instant-buy seams (<see cref="Shipwright.TryBuy()"/>
    /// etc.) remain for tests/automation.
    ///
    /// <para>Vendor-agnostic: it opens the screen for WHATEVER vendor components sit on this
    /// GameObject (Shipwright, GearShop, LicenseVendor — the screen enumerates them), so the same
    /// driver serves the Punt shed, the dory yard, the harbourmaster, and the general store.
    /// ui-ux swaps this key for the real Interact intent through the InputService later.</para>
    /// </summary>
    public class DevBuyInput : MonoBehaviour
    {
        [Tooltip("On-foot + in-range gate: P only opens the buy screen when the walking player is at this stall.")]
        [SerializeField] private StallReach _reach = new StallReach();

        private void OnEnable() => _reach.Enable();
        private void OnDisable() => _reach.Disable();

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.pKey.wasPressedThisFrame) return;

            if (_reach.CanInteract(transform.position))
                BuyScreen.Open(gameObject);
            else if (_reach.OnFoot && !BuyScreen.IsOpen)
                Debug.Log("[Buy] Too far — step up to the stall to browse.");
        }
    }
}
