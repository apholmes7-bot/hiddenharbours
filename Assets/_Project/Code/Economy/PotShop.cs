using UnityEngine;
using UnityEngine.Events;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Sells POTS at the shipwright (trap-fishing: the lobster pot, the crab pot) — the counted-stock
    /// sibling of <see cref="GearShop"/>, reusing the same buy pattern: check the price, spend from the
    /// <see cref="IWallet"/>, and on success record the purchase in the save and raise the Core
    /// <c>PotPurchased</c> signal. The one deliberate difference: pots are FINITE, REPEATABLE stock —
    /// a purchase increments <c>SaveData.PotStock</c> through <see cref="PotLocker"/> (counted per
    /// TrapDef id) instead of joining the presence-only OwnedGear wallet, and there is no
    /// "already owned" refusal — you can always buy another pot.
    ///
    /// <para><b>Economy side only</b> (rule 4): it records ownership; the Fishing lane gates the T-set
    /// on the derived available count (owned − deployed − aboard, <see cref="PotLocker"/>) without ever
    /// referencing this class. No UI here — the stall's <see cref="BuyScreen"/> lists this vendor via
    /// <see cref="BuyCatalog"/> and routes Confirm through <see cref="TryBuy()"/>.</para>
    /// </summary>
    public class PotShop : MonoBehaviour
    {
        [Tooltip("The pot offered for sale here (offer id + trap id + price).")]
        [SerializeField] private PotOffer _offer;
        [Tooltip("A GameObject carrying an IWallet (the player's PlayerWallet / persistent proxy).")]
        [SerializeField] private GameObject _walletProvider;

        [Tooltip("Inspector hook fired with the ₲ price on a successful purchase (UI can bind this later).")]
        [SerializeField] private UnityEvent<int> _onPurchased;

        private IWallet _wallet;

        /// <summary>The pot offered here (offer id + trap id + price). Null until wired.</summary>
        public PotOffer Offer => _offer;

        /// <summary>True iff the most recent <see cref="TryBuy()"/> went through.</summary>
        public bool LastPurchaseSucceeded { get; private set; }

        private void Awake()
        {
            if (_walletProvider != null) _wallet = _walletProvider.GetComponent<IWallet>();
            if (_wallet == null)
                Debug.LogWarning("[PotShop] No IWallet found on the wallet provider.", this);
        }

        /// <summary>The no-arg interaction entrypoint (the buy screen / tests). Buys one pot of the
        /// wired offer with the wired wallet, recording it on the live save.</summary>
        public bool TryBuy() => TryBuy(_offer, _wallet, GameServices.Save?.Current);

        /// <summary>
        /// Core buy seam (testable): checks the price, spends from the wallet, and on success adds ONE
        /// pot of the offer's kind to <paramref name="save"/>'s owned stock (<see cref="PotLocker"/>),
        /// persists, and raises <c>PotPurchased(trapDefId, price, newOwnedTotal)</c>. Money is only
        /// deducted on success (<see cref="IWallet.TrySpend"/> is atomic). Unlike gear there is no
        /// already-owned refusal — pots are counted stock, always re-buyable. Refuses (false, charges
        /// nothing) on null offer/wallet, an id-less offer, or a null save (a pot with nowhere to be
        /// recorded would eat the player's coin).
        /// </summary>
        public bool TryBuy(PotOffer offer, IWallet wallet, SaveData save)
        {
            LastPurchaseSucceeded = false;
            if (offer == null) { Debug.LogWarning("[PotShop] No pot to buy.", this); return false; }
            if (wallet == null) return false;
            if (string.IsNullOrEmpty(offer.TrapDefId))
            {
                Debug.LogWarning("[PotShop] Offer has no TrapDefId — cannot record the stock.", this);
                return false;
            }
            if (save == null)
            {
                Debug.LogWarning("[PotShop] No save to record the pot in — refusing rather than eat the coin.", this);
                return false;
            }

            if (!wallet.TrySpend(offer.Price))
            {
                Debug.Log($"[PotShop] Can't afford {offer.DisplayName}: need ₲{offer.Price}, have ₲{wallet.Money}.");
                return false;
            }

            int owned = PotLocker.AddOwned(save, offer.TrapDefId, 1);
            if (ReferenceEquals(save, GameServices.Save?.Current)) GameServices.Save?.Save();

            EventBus.Publish(new PotPurchased(offer.TrapDefId, offer.Price, owned));
            LastPurchaseSucceeded = true;
            Debug.Log($"[PotShop] Bought a {offer.DisplayName} ({offer.TrapDefId}) for ₲{offer.Price} — {owned} owned.");
            _onPurchased?.Invoke(offer.Price);
            return true;
        }
    }
}
