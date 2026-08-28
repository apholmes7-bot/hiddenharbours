using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Sells a piece of gear/equipment at a shop (the Nine Mile Creek store, St Peters opening: the rod, the
    /// shovel). Reuses the <see cref="Shipwright"/>/<see cref="LicenseVendor"/> buy pattern: check the
    /// price, spend from the <see cref="IWallet"/>, and on success record the gear id as owned in the
    /// save and raise <see cref="GearPurchased"/>. <b>Economy side only</b> — it records the purchase;
    /// gameplay-systems maps the owned gear id to its <c>Gear</c> capability (the rod fishes cod only
    /// once the cod licence is also held — a separate gate). No UI here (ui-ux's job); world-content
    /// places this on the store next wave.
    /// </summary>
    public class GearShop : MonoBehaviour
    {
        [Tooltip("The gear offered for sale here (id + price).")]
        [SerializeField] private GearOffer _offer;

        [Tooltip("WHOSE counter this is (seller.snake_case, e.g. seller.leblancs). The other half of the " +
                 "catalog tag: a listing names the sellers that stock it, and this names which seller " +
                 "this component sells for. Empty means this vendor is in no book — it still works as a " +
                 "direct seam, it just is not listed anywhere.")]
        [SerializeField] private string _sellerId = "";
        [Tooltip("A GameObject carrying an IWallet (the player's PlayerWallet).")]
        [SerializeField] private GameObject _walletProvider;

        [Tooltip("Inspector hook fired with the ₲ price on a successful purchase (UI can bind this later).")]
        [SerializeField] private UnityEvent<int> _onPurchased;

        private IWallet _wallet;

        /// <summary>The gear offered here (id + price). Null until wired.</summary>
        public GearOffer Offer => _offer;

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
                Debug.LogWarning("[GearShop] No IWallet found on the wallet provider.", this);
        }

        /// <summary>The no-arg interaction entrypoint (dev input / the future buy screen). Buys the wired
        /// gear with the wired wallet, recording ownership on the live save.</summary>
        public bool TryBuy() => TryBuy(_offer, _wallet, GameServices.Save?.Current);
        /// <summary>
        /// Sell the piece of gear the book picked, rather than the one wired into this component.
        ///
        /// <para>The catalog inversion needs this: one GearShop on a counter now stands for a seller, and
        /// the rows come from the listings tagged to that seller — so Confirm must be able to name which
        /// one. It forwards straight to the existing seam with this component's own wallet, so the money,
        /// the save write and the Core event are the same code they always were.</para>
        /// </summary>
        public bool TryBuy(GearOffer offer) => TryBuy(offer, _wallet, GameServices.Save?.Current);

        /// <summary>
        /// Core buy seam (testable): checks the price, spends from the wallet, and on success records the
        /// gear id in <paramref name="save"/>.OwnedGear, persists, and raises <c>GearPurchased(id, price)</c>.
        /// Money is only deducted on success (atomic). An already-owned gear is a no-op (false, charges
        /// nothing). Returns true iff a NEW gear was bought.
        /// </summary>
        public bool TryBuy(GearOffer offer, IWallet wallet, SaveData save)
        {
            LastPurchaseSucceeded = false;
            if (offer == null) { Debug.LogWarning("[GearShop] No gear to buy.", this); return false; }
            if (wallet == null) return false;
            if (string.IsNullOrEmpty(offer.Id))
            {
                Debug.LogWarning("[GearShop] Gear has no id — cannot record ownership.", this);
                return false;
            }

            if (Owns(save, offer.Id))
            {
                Debug.Log($"[GearShop] Already own {offer.DisplayName} ({offer.Id}).");
                return false;
            }

            if (!wallet.TrySpend(offer.Price))
            {
                Debug.Log($"[GearShop] Can't afford {offer.DisplayName}: need ₲{offer.Price}, have ₲{wallet.Money}.");
                return false;
            }

            if (save != null)
            {
                save.OwnedGear ??= new List<string>();
                if (!save.OwnedGear.Contains(offer.Id)) save.OwnedGear.Add(offer.Id);
                if (ReferenceEquals(save, GameServices.Save?.Current)) GameServices.Save?.Save();
            }

            EventBus.Publish(new GearPurchased(offer.Id, offer.Price));
            LastPurchaseSucceeded = true;
            Debug.Log($"[GearShop] Bought {offer.DisplayName} ({offer.Id}) for ₲{offer.Price}.");
            _onPurchased?.Invoke(offer.Price);
            return true;
        }

        private static bool Owns(SaveData save, string gearId)
            => save?.OwnedGear != null && save.OwnedGear.Contains(gearId);
    }
}
