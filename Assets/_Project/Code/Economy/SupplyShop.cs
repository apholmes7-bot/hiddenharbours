using UnityEngine;
using UnityEngine.Events;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Sells a consumable SUPPLY over a counter (the island general store's ice — plan-to-m1 §7.5). The
    /// counted-stock sibling of <see cref="GearShop"/> and the twin of <see cref="PotShop"/>, reusing the
    /// same buy pattern: check the price, spend from the <see cref="IWallet"/>, and on success record the
    /// purchase in the save and raise <see cref="SupplyPurchased"/>.
    ///
    /// <para>Like pots and unlike gear there is <b>no "already owned" refusal</b> — you can always buy more
    /// ice, and that is the point: it makes the store a repeat destination rather than a one-visit shop.
    /// Stock is recorded through the Core <see cref="SupplyLocker"/> (counted per <see cref="SupplyDef"/>
    /// id), never by touching <see cref="SaveData"/> fields directly.</para>
    ///
    /// <para><b>Economy side only</b> (rule 4): it records the purchase. Spending a bag of ice to protect a
    /// container is the §7.3 cold-chain interactable's job, and it reads the same locker — this class never
    /// references it. No UI here (ui-ux's): the seller's wares book lists this vendor via
    /// <see cref="BuyCatalog"/> and routes Confirm through <see cref="TryBuy()"/>.</para>
    /// </summary>
    public class SupplyShop : MonoBehaviour
    {
        [Tooltip("The supply offered for sale here (id + price).")]
        [SerializeField] private SupplyDef _supply;

        [Tooltip("WHOSE counter this is (seller.snake_case, e.g. seller.leblancs). The other half of the " +
                 "catalog tag: a listing names the sellers that stock it, and this names which seller " +
                 "this component sells for. Empty means this vendor is in no book — it still works as a " +
                 "direct seam, it just is not listed anywhere.")]
        [SerializeField] private string _sellerId = "";
        [Tooltip("A GameObject carrying an IWallet (the player's PlayerWallet / persistent proxy).")]
        [SerializeField] private GameObject _walletProvider;

        [Tooltip("Inspector hook fired with the ₲ price on a successful purchase (UI can bind this later).")]
        [SerializeField] private UnityEvent<int> _onPurchased;

        private IWallet _wallet;

        /// <summary>The supply offered here (id + price). Null until wired.</summary>
        public SupplyDef Supply => _supply;

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
                Debug.LogWarning("[SupplyShop] No IWallet found on the wallet provider.", this);
        }

        /// <summary>The no-arg interaction entrypoint (the buy screen / tests). Buys ONE of the wired supply
        /// with the wired wallet, recording it on the live save.</summary>
        public bool TryBuy() => TryBuy(_supply, _wallet, GameServices.Save?.Current);
        /// <summary>
        /// Sell the supply the book picked, rather than the one wired into this component.
        ///
        /// <para>The catalog inversion needs this: one SupplyShop on a counter now stands for a seller, and
        /// the rows come from the listings tagged to that seller — so Confirm must be able to name which
        /// one. It forwards straight to the existing seam with this component's own wallet, so the money,
        /// the save write and the Core event are the same code they always were.</para>
        /// </summary>
        public bool TryBuy(SupplyDef supply) => TryBuy(supply, _wallet, GameServices.Save?.Current);

        /// <summary>
        /// Core buy seam (testable): checks the price, spends from the wallet, and on success adds ONE of the
        /// supply to <paramref name="save"/>'s stock (<see cref="SupplyLocker.Add"/>), persists, and raises
        /// <c>SupplyPurchased(supplyId, price, newOwnedTotal)</c>. Money is only deducted on success
        /// (<see cref="IWallet.TrySpend"/> is atomic). Refuses (false, charges nothing) on a null
        /// offer/wallet, an id-less offer, or a null save — a bag of ice with nowhere to be recorded would
        /// eat the player's coin (the <see cref="PotShop"/> rule).
        /// </summary>
        public bool TryBuy(SupplyDef supply, IWallet wallet, SaveData save)
        {
            LastPurchaseSucceeded = false;
            if (supply == null) { Debug.LogWarning("[SupplyShop] No supply to buy.", this); return false; }
            if (wallet == null) return false;
            if (string.IsNullOrEmpty(supply.Id))
            {
                Debug.LogWarning("[SupplyShop] Supply has no id — cannot record the stock.", this);
                return false;
            }
            if (save == null)
            {
                Debug.LogWarning("[SupplyShop] No save to record the supply in — refusing rather than eat the coin.", this);
                return false;
            }

            if (!wallet.TrySpend(supply.Price))
            {
                Debug.Log($"[SupplyShop] Can't afford {supply.DisplayName}: need ₲{supply.Price}, have ₲{wallet.Money}.");
                return false;
            }

            int owned = SupplyLocker.Add(save, supply.Id, 1);
            if (ReferenceEquals(save, GameServices.Save?.Current)) GameServices.Save?.Save();

            EventBus.Publish(new SupplyPurchased(supply.Id, supply.Price, owned));
            LastPurchaseSucceeded = true;
            Debug.Log($"[SupplyShop] Bought {supply.DisplayName} ({supply.Id}) for ₲{supply.Price} — {owned} owned.");
            _onPurchased?.Invoke(supply.Price);
            return true;
        }
    }
}
