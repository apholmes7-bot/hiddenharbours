using UnityEngine;
using UnityEngine.Events;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Sells a fishing/gear license at a vendor (the Greywick harbourmaster, St Peters opening). Reuses
    /// the <see cref="Shipwright"/> buy pattern: check the fee, spend from the <see cref="IWallet"/>, and
    /// on success grant the license through the Core <see cref="ILicenseService"/> and raise
    /// <see cref="LicensePurchased"/>. <b>Economy side only</b> — it never touches Fishing/Player; the
    /// rod-fishes-cod gate is enforced by Fishing reading the license wallet (cross-module via Core).
    ///
    /// <para>The fee and what it unlocks live in data (<see cref="LicenseDef"/>), not code. The buy
    /// <i>screen</i> is ui-ux's job; no UI is built here. world-content places this on the harbourmaster
    /// next wave (it provides the component + data).</para>
    /// </summary>
    public class LicenseVendor : MonoBehaviour
    {
        [Tooltip("The license offered for sale here (id + fee + what it permits).")]
        [SerializeField] private LicenseDef _license;
        [Tooltip("A GameObject carrying an IWallet (the player's PlayerWallet).")]
        [SerializeField] private GameObject _walletProvider;

        [Tooltip("Inspector hook fired with the ₲ fee on a successful purchase (UI can bind this later).")]
        [SerializeField] private UnityEvent<int> _onPurchased;

        private IWallet _wallet;

        /// <summary>The license offered here (id + fee). Null until wired.</summary>
        public LicenseDef License => _license;

        /// <summary>True iff the most recent <see cref="TryBuy()"/> went through.</summary>
        public bool LastPurchaseSucceeded { get; private set; }

        private void Awake()
        {
            if (_walletProvider != null) _wallet = _walletProvider.GetComponent<IWallet>();
            if (_wallet == null)
                Debug.LogWarning("[LicenseVendor] No IWallet found on the wallet provider.", this);
        }

        /// <summary>The no-arg interaction entrypoint (dev input / the future buy screen). Buys the wired
        /// license with the wired wallet, granting through the live <see cref="GameServices.Licenses"/>.</summary>
        public bool TryBuy() => TryBuy(_license, _wallet, GameServices.Licenses);

        /// <summary>
        /// Core buy seam (testable): checks the fee, spends from the wallet, and on success grants the
        /// license and raises <c>LicensePurchased(id, fee)</c>. Money is only deducted if the purchase
        /// succeeds (<see cref="IWallet.TrySpend"/> is atomic). An already-held license is a no-op
        /// (returns false, charges nothing). Returns true iff a NEW license was bought.
        /// </summary>
        public bool TryBuy(LicenseDef license, IWallet wallet, ILicenseService licenses)
        {
            LastPurchaseSucceeded = false;
            if (license == null)
            {
                Debug.LogWarning("[LicenseVendor] No license to buy.", this);
                return false;
            }
            if (wallet == null) return false;
            if (string.IsNullOrEmpty(license.Id))
            {
                Debug.LogWarning("[LicenseVendor] License has no id — cannot grant.", this);
                return false;
            }

            // Don't double-charge for a license the player already holds.
            if (licenses != null && licenses.IsLicensed(license.Id))
            {
                Debug.Log($"[LicenseVendor] Already hold {license.DisplayName} ({license.Id}).");
                return false;
            }

            if (!wallet.TrySpend(license.Price))
            {
                Debug.Log($"[LicenseVendor] Can't afford {license.DisplayName}: need ₲{license.Price}, have ₲{wallet.Money}.");
                return false;
            }

            licenses?.Grant(license.Id);
            EventBus.Publish(new LicensePurchased(license.Id, license.Price));
            LastPurchaseSucceeded = true;
            Debug.Log($"[LicenseVendor] Bought {license.DisplayName} ({license.Id}) for ₲{license.Price}.");
            _onPurchased?.Invoke(license.Price);
            return true;
        }
    }
}
