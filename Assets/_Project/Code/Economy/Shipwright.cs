using UnityEngine;
using UnityEngine.Events;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// The Shipwright buy flow (VS-16): purchase a boat with the player's wallet. <b>Economy side only</b> —
    /// it checks the offer price, calls <see cref="IWallet.TrySpend"/>, and on success raises the Core
    /// <c>BoatPurchased</c> event. <c>gameplay-systems</c> listens for that to add the hull to the owned
    /// fleet and swap the active boat; this class never touches the Boats module (cross-module talk goes
    /// through Core/EventBus).
    ///
    /// <para>The price lives in data (<see cref="ShipwrightOffer"/>), not in code. <c>BoatPurchased</c> is
    /// the cross-module signal a HUD/"new boat!" toast (ui-ux) can subscribe to; <c>_onPurchased</c> is an
    /// inspector hook for a local reaction. The actual buy <i>screen</i> (browse stats, confirm) is ui-ux's
    /// job (VS-16/18) — no UI is built here.</para>
    /// </summary>
    public class Shipwright : MonoBehaviour
    {
        [Tooltip("The boat offered for sale here (id + price).")]
        [SerializeField] private ShipwrightOffer _offer;
        [Tooltip("A GameObject carrying an IWallet (the player's PlayerWallet).")]
        [SerializeField] private GameObject _walletProvider;

        [Tooltip("Inspector hook fired with the ₲ price on a successful purchase (UI can bind this later).")]
        [SerializeField] private UnityEvent<int> _onPurchased;

        private IWallet _wallet;

        /// <summary>The boat offered for sale here (id + price). Null until wired.</summary>
        public ShipwrightOffer Offer => _offer;

        /// <summary>True iff the most recent <see cref="TryBuy()"/> went through.</summary>
        public bool LastPurchaseSucceeded { get; private set; }

        private void Awake()
        {
            if (_walletProvider != null) _wallet = _walletProvider.GetComponent<IWallet>();
            if (_wallet == null)
                Debug.LogWarning("[Shipwright] No IWallet found on the wallet provider.", this);
        }

        /// <summary>
        /// The no-arg interaction entrypoint (what dev input / the future buy screen calls). Buys the
        /// wired offer with the wired wallet.
        /// </summary>
        public bool TryBuy() => TryBuy(_offer, _wallet);

        /// <summary>
        /// Core buy seam (testable): checks the price, spends from the wallet, and on success raises
        /// <c>BoatPurchased(offer.BoatId, offer.Price)</c>. Money is only deducted if the purchase
        /// succeeds (<see cref="IWallet.TrySpend"/> is atomic). Returns true iff the purchase went through.
        /// </summary>
        public bool TryBuy(ShipwrightOffer offer, IWallet wallet)
        {
            LastPurchaseSucceeded = false;
            if (offer == null)
            {
                Debug.LogWarning("[Shipwright] No offer to buy.", this);
                return false;
            }
            if (wallet == null) return false;

            if (!wallet.TrySpend(offer.Price))
            {
                Debug.Log($"[Shipwright] Can't afford {offer.DisplayName}: need ₲{offer.Price}, have ₲{wallet.Money}.");
                return false;
            }

            EventBus.Publish(new BoatPurchased(offer.BoatId, offer.Price));
            LastPurchaseSucceeded = true;
            Debug.Log($"[Shipwright] Bought {offer.DisplayName} ({offer.BoatId}) for ₲{offer.Price}.");
            _onPurchased?.Invoke(offer.Price);
            return true;
        }
    }
}
