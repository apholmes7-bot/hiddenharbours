using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// PLACEHOLDER: press B ("buyer") to sell your hold at the wharf, so you can test the
    /// catch→sell loop in the greybox. ui-ux replaces this with the real Interact intent through
    /// the InputService later.
    ///
    /// <para>Optional proximity gate: if <c>_boat</c> is assigned, you can only sell when the dory
    /// is within <c>_dockRadius</c> of this wharf (so you must bring the catch in). If <c>_boat</c>
    /// is unset, B always sells. <c>_dockRadius</c> is a serialized scene-layout tunable, not a
    /// balance value — a serialized field is the right home for it, not GameConfig.</para>
    /// </summary>
    [RequireComponent(typeof(WharfSellPoint))]
    public class DevSellInput : MonoBehaviour
    {
        [Tooltip("Optional: the dory. If set, you must be within the dock radius to sell.")]
        [SerializeField] private Transform _boat;
        [Tooltip("How close the boat must be to the wharf to sell (metres).")]
        [SerializeField] private float _dockRadius = 6f;

        private WharfSellPoint _wharf;

        private void Awake() => _wharf = GetComponent<WharfSellPoint>();

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.bKey.wasPressedThisFrame) return;

            if (_boat != null &&
                Vector2.Distance(_boat.position, transform.position) > _dockRadius)
            {
                Debug.Log("[Wharf] Too far out — bring the dory alongside to sell.");
                return;
            }

            _wharf.Sell();
        }
    }
}
