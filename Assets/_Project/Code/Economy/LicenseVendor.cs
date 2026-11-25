using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Sells a fishing/gear license at a vendor (the Nine Mile Creek harbourmaster, St Peters opening). Reuses
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

        [Tooltip("WHOSE counter this is (seller.snake_case, e.g. seller.leblancs). The other half of the " +
                 "catalog tag: a listing names the sellers that stock it, and this names which seller " +
                 "this component sells for. Empty means this vendor is in no book — it still works as a " +
                 "direct seam, it just is not listed anywhere.")]
        [SerializeField] private string _sellerId = "";
        [Tooltip("A GameObject carrying an IWallet (the player's PlayerWallet).")]
        [SerializeField] private GameObject _walletProvider;

        [Tooltip("Inspector hook fired with the ₲ fee on a successful purchase (UI can bind this later).")]
        [SerializeField] private UnityEvent<int> _onPurchased;

        private IWallet _wallet;

        // Shared by counters, but scoped to each player's purse and licence id. Weak keys do not
        // retain a departed player; the entries contain only transient transaction state.
        private static readonly ConditionalWeakTable<IWallet, HashSet<string>> PurchasesInProgress = new();

        /// <summary>The license offered here (id + fee). Null until wired.</summary>
        public LicenseDef License => _license;

        /// <summary>Whose counter this is. The book resolves a seller id to the components that own the
        /// purchase seams through this, which is why stock can be content and the scene can stay out of
        /// it (owner ruling R1: a field on the vendors, not a registry — the purchase flow does not
        /// move, and this can become a registry later without touching content).</summary>
        public string SellerId => _sellerId;

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
        /// Sell the licence the book picked, rather than the one wired into this component.
        ///
        /// <para>The catalog inversion needs this: one LicenseVendor on a counter now stands for a seller, and
        /// the rows come from the listings tagged to that seller — so Confirm must be able to name which
        /// one. It forwards straight to the existing seam with this component's own wallet, so the money,
        /// the save write and the Core event are the same code they always were.</para>
        /// </summary>
        public bool TryBuy(LicenseDef license) => TryBuy(license, _wallet, GameServices.Licenses);

        /// <summary>Confirm the contribution shown in the book. A changed purse requires a fresh quote.</summary>
        public bool TryBuy(LicenseDef license, int quotedPrice)
            => TryBuy(license, _wallet, GameServices.Licenses, GameServices.Save, quotedPrice);

        /// <summary>The book and the transaction use the same quote. Missing recovery data, save or
        /// licence service leaves the ordinary price in place. Reading a quote changes no state.</summary>
        public BuyQuote Quote(LicenseDef license, int money, SaveData save, ILicenseService licenses)
        {
            if (license == null || string.IsNullOrEmpty(license.Id))
                return new BuyQuote(BuyRowKind.License, 0, false, false);

            bool held = (licenses != null && licenses.IsLicensed(license.Id))
                        || (save?.OwnedLicenses != null && save.OwnedLicenses.Contains(license.Id));
            bool recover = licenses != null && license.Price > 0
                           && !string.IsNullOrWhiteSpace(license.FeeRecoverySellerId)
                           && license.FeeRecoverySellerId == _sellerId
                           && !string.IsNullOrWhiteSpace(license.FeeRecoveryUsedFlag)
                           && license.FeeRecoveryUsedFlag != license.FeeRecoveryGrantFlag
                           && HasFlag(save, license.FeeRecoveryGrantFlag)
                           && !HasFlag(save, license.FeeRecoveryUsedFlag);
            return BuyLogic.License(license.Price, money, held, recover);
        }

        private static bool HasFlag(SaveData save, string key)
        {
            if (string.IsNullOrWhiteSpace(key) || save?.OnboardingFlags == null) return false;
            foreach (var flag in save.OnboardingFlags)
                if (flag.Key == key) return flag.Value;
            return false;
        }

        /// <summary>
        /// Core buy seam (testable): checks the fee, spends from the wallet, and on success grants the
        /// license and raises <c>LicensePurchased(id, fee)</c>. Money is only deducted if the purchase
        /// succeeds (<see cref="IWallet.TrySpend"/> is atomic). An already-held license is a no-op
        /// (returns false, charges nothing). Returns true iff a NEW license was bought.
        /// </summary>
        public bool TryBuy(LicenseDef license, IWallet wallet, ILicenseService licenses)
            => TryBuy(license, wallet, licenses, GameServices.Save);

        /// <summary>Buy for an explicit player context. The wallet, licences and save must belong to
        /// that player; a shared counter does not own the player's entitlement or recovery state.</summary>
        public bool TryBuy(LicenseDef license, IWallet wallet, ILicenseService licenses,
                           ISaveService saver, int? quotedPrice = null)
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

            string licenseId = license.Id;
            var inProgress = PurchasesInProgress.GetValue(wallet, _ => new HashSet<string>());
            if (!inProgress.Add(licenseId)) return false;
            try
            {
                BuyQuote quote = Quote(license, wallet.Money, saver?.Current, licenses);
                if (quotedPrice.HasValue && quotedPrice.Value != quote.Price) return false;
                // Also respect saved ownership while the live licence service is being restored.
                if (quote.Owned)
                {
                    Debug.Log($"[LicenseVendor] Already hold {license.DisplayName} ({licenseId}).");
                    return false;
                }

                if (!quote.CanBuy)
                {
                    Debug.Log($"[LicenseVendor] Can't afford {license.DisplayName}: need ₲{license.Price}, have ₲{wallet.Money}.");
                    return false;
                }

                using (saver?.DeferWrites())
                {
                    // Both ordinary and discounted purchases are guarded before any spend callback.
                    // Record recovery only after a successful spend; deferred writes group all facts.
                    if (!wallet.TrySpend(quote.Price)) return false;
                    if (quote.Price < license.Price) saver.SetFlag(license.FeeRecoveryUsedFlag, true);
                    licenses?.Grant(licenseId);
                }
                EventBus.Publish(new LicensePurchased(licenseId, quote.Price, SellerDirectory.Resolve(_sellerId)));
                LastPurchaseSucceeded = true;
                Debug.Log($"[LicenseVendor] Bought {license.DisplayName} ({licenseId}) for ₲{quote.Price}.");
                _onPurchased?.Invoke(quote.Price);
                return true;
            }
            finally
            {
                inProgress.Remove(licenseId);
            }
        }
    }
}
