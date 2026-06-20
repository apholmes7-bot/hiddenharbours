using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// PLACEHOLDER: press P ("Punt") to buy the boat offered at the Shipwright, so the buy flow is
    /// exercisable in the greybox. ui-ux replaces this with the real Shipwright buy screen / Interact
    /// intent through the InputService later.
    /// </summary>
    [RequireComponent(typeof(Shipwright))]
    public class DevBuyInput : MonoBehaviour
    {
        private Shipwright _shipwright;

        private void Awake() => _shipwright = GetComponent<Shipwright>();

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
                _shipwright.TryBuy();
        }
    }
}
