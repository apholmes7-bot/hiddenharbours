using UnityEngine;
using UnityEngine.Events;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Sells BAIT by the lot over a counter (the island general store — plan-to-m1 §7.5). The third
    /// counted-stock vendor beside <see cref="PotShop"/> and <see cref="SupplyShop"/>, on the same buy
    /// pattern: check the price, spend from the <see cref="IWallet"/>, and on success add the lot to the
    /// player's bait stock and raise <see cref="BaitPurchased"/>.
    ///
    /// <para>This is the "third caller" <see cref="TackleBox"/> was written for — bait acquisition was a
    /// greybox starting grant until now, which is why the §10.2 bait economy has never been real money.
    /// Stock goes through <see cref="TackleBox.AddBait"/>, never by touching <see cref="SaveData"/> directly,
    /// so the shop, the rod and the pots all count the same box.</para>
    ///
    /// <para>Like pots and supplies there is <b>no "already owned" refusal</b> — bait is consumable and
    /// always re-buyable. The lot size lives on the <see cref="BaitDef"/> (data, rule 6) and the lot costs
    /// <c>Price × LotSize</c>; the Def's <c>Price</c> stays the UNIT price the pacing model reads.</para>
    ///
    /// <para><b>Economy side only</b> (rule 4): it records the purchase. Which bait is on the hook is the
    /// Fishing lane's (<c>FishingController</c>'s serialized <c>BaitDef</c>), and this class never touches
    /// it. No UI here (ui-ux's): the stall's <see cref="BuyScreen"/> lists this vendor via
    /// <see cref="BuyCatalog"/> and routes Confirm through <see cref="TryBuy()"/>.</para>
    /// </summary>
    public class BaitShop : MonoBehaviour
    {
        [Tooltip("The bait offered for sale here (id + unit price + lot size).")]
        [SerializeField] private BaitDef _bait;
        [Tooltip("A GameObject carrying an IWallet (the player's PlayerWallet / persistent proxy).")]
        [SerializeField] private GameObject _walletProvider;

        [Tooltip("Inspector hook fired with the ₲ price on a successful purchase (UI can bind this later).")]
        [SerializeField] private UnityEvent<int> _onPurchased;

        private IWallet _wallet;

        /// <summary>The bait offered here (id + unit price + lot size). Null until wired.</summary>
        public BaitDef Bait => _bait;

        /// <summary>True iff the most recent <see cref="TryBuy()"/> went through.</summary>
        public bool LastPurchaseSucceeded { get; private set; }

        /// <summary>How many baits one purchase buys — the Def's lot size, floored at 1 so an asset authored
        /// before the field existed still sells singly rather than selling nothing for full price.</summary>
        public static int LotSizeOf(BaitDef bait) => bait == null ? 0 : Mathf.Max(1, bait.LotSize);

        /// <summary>₲ one lot costs: the unit price × the lot size.</summary>
        public static int LotPriceOf(BaitDef bait) => bait == null ? 0 : Mathf.Max(0, bait.Price) * LotSizeOf(bait);

        private void Awake()
        {
            if (_walletProvider != null) _wallet = _walletProvider.GetComponent<IWallet>();
            if (_wallet == null)
                Debug.LogWarning("[BaitShop] No IWallet found on the wallet provider.", this);
        }

        /// <summary>The no-arg interaction entrypoint (the buy screen / tests). Buys ONE LOT of the wired
        /// bait with the wired wallet, recording it on the live save.</summary>
        public bool TryBuy() => TryBuy(_bait, _wallet, GameServices.Save?.Current);

        /// <summary>
        /// Core buy seam (testable): checks the lot price, spends from the wallet, and on success adds the
        /// whole lot to <paramref name="save"/>'s bait stock (<see cref="TackleBox.AddBait"/>), persists, and
        /// raises <c>BaitPurchased(baitId, lotPrice, lotSize, newOwnedTotal)</c>. Money is only deducted on
        /// success (<see cref="IWallet.TrySpend"/> is atomic). Refuses (false, charges nothing) on a null
        /// offer/wallet, an id-less offer, or a null save — bait with nowhere to be recorded would eat the
        /// player's coin (the <see cref="PotShop"/> rule).
        /// </summary>
        public bool TryBuy(BaitDef bait, IWallet wallet, SaveData save)
        {
            LastPurchaseSucceeded = false;
            if (bait == null) { Debug.LogWarning("[BaitShop] No bait to buy.", this); return false; }
            if (wallet == null) return false;
            if (string.IsNullOrEmpty(bait.Id))
            {
                Debug.LogWarning("[BaitShop] Bait has no id — cannot record the stock.", this);
                return false;
            }
            if (save == null)
            {
                Debug.LogWarning("[BaitShop] No save to record the bait in — refusing rather than eat the coin.", this);
                return false;
            }

            int lot = LotSizeOf(bait);
            int price = LotPriceOf(bait);
            if (!wallet.TrySpend(price))
            {
                Debug.Log($"[BaitShop] Can't afford {lot}× {bait.DisplayName}: need ₲{price}, have ₲{wallet.Money}.");
                return false;
            }

            TackleBox.AddBait(save, bait.Id, lot);
            int owned = TackleBox.BaitCount(save, bait.Id);
            if (ReferenceEquals(save, GameServices.Save?.Current)) GameServices.Save?.Save();

            EventBus.Publish(new BaitPurchased(bait.Id, price, lot, owned));
            LastPurchaseSucceeded = true;
            Debug.Log($"[BaitShop] Bought {lot}× {bait.DisplayName} ({bait.Id}) for ₲{price} — {owned} in the box.");
            _onPurchased?.Invoke(price);
            return true;
        }
    }
}
