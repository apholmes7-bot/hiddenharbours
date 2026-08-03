using UnityEngine;
using UnityEngine.Events;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Sells a HELM INSTRUMENT over a counter and fits it to a boat (ADR 0025 S2 — the depth sounder is
    /// the first). Same buy pattern as <see cref="GearShop"/>/<see cref="SupplyShop"/>: check the price,
    /// spend from the <see cref="IWallet"/>, and on success record the purchase in the save and raise
    /// <see cref="InstrumentPurchased"/>.
    ///
    /// <para><b>Which boat it fits — the rule, stated once.</b> The instrument goes into the hull the
    /// player is currently aboard (<see cref="SaveData.ActiveHullId"/>). It is bolted into a dash, not
    /// carried, so "the boat you came in on" is the only rule that needs no extra UI; with NO active
    /// hull the shop <b>refuses</b> rather than take money for a fitting with nowhere to go — the
    /// <see cref="SupplyShop"/> "never eat the coin" rule. (Owner may veto this in favour of a
    /// pick-a-boat screen; that would be a UI addition, not a change here.)</para>
    ///
    /// <para><b>Economy side only</b> (rule 4): it records the fitment. Whether that hull's console can
    /// TAKE the instrument, and what the dash then shows, is <c>BoatEquipment.EffectiveFit</c>'s — the
    /// Boats lane — and this class never references it. Buying an instrument for a hull whose helm
    /// cannot mount it is money spent on nothing, exactly as buying a fish-finder for a bare brow always
    /// was; the buy screen shows the hull it will go on so the choice is informed.</para>
    /// </summary>
    public class InstrumentShop : MonoBehaviour
    {
        [Tooltip("The instrument offered for sale here (id + price).")]
        [SerializeField] private InstrumentOffer _offer;
        [Tooltip("A GameObject carrying an IWallet (the player's PlayerWallet / persistent proxy).")]
        [SerializeField] private GameObject _walletProvider;

        [Tooltip("Inspector hook fired with the ₲ price on a successful purchase (UI can bind this later).")]
        [SerializeField] private UnityEvent<int> _onPurchased;

        private IWallet _wallet;

        /// <summary>The instrument offered here (id + price). Null until wired.</summary>
        public InstrumentOffer Offer => _offer;

        /// <summary>True iff the most recent <see cref="TryBuy()"/> went through.</summary>
        public bool LastPurchaseSucceeded { get; private set; }

        private void Awake()
        {
            if (_walletProvider != null) _wallet = _walletProvider.GetComponent<IWallet>();
            if (_wallet == null)
                Debug.LogWarning("[InstrumentShop] No IWallet found on the wallet provider.", this);
        }

        /// <summary>The no-arg interaction entrypoint (the buy screen / dev input / tests). Fits the wired
        /// instrument to the hull the player is aboard, paying from the wired wallet.</summary>
        public bool TryBuy() => TryBuy(_offer, _wallet, GameServices.Save?.Current);

        /// <summary>
        /// Core buy seam (testable): checks the price, spends from the wallet, and on success records the
        /// instrument against the save's <see cref="SaveData.ActiveHullId"/>
        /// (<see cref="InstrumentLocker.Add"/>), persists, and raises
        /// <c>InstrumentPurchased(instrumentId, hullId, price)</c>. Money is only deducted on success
        /// (<see cref="IWallet.TrySpend"/> is atomic). Refuses — charging nothing — on a null
        /// offer/wallet/save, an id-less offer, no active hull, or an instrument this hull already
        /// carries.
        /// </summary>
        public bool TryBuy(InstrumentOffer offer, IWallet wallet, SaveData save)
        {
            LastPurchaseSucceeded = false;
            if (offer == null) { Debug.LogWarning("[InstrumentShop] No instrument to buy.", this); return false; }
            if (wallet == null) return false;
            if (string.IsNullOrEmpty(offer.Id))
            {
                Debug.LogWarning("[InstrumentShop] Instrument has no id — cannot record the fitment.", this);
                return false;
            }
            if (save == null)
            {
                Debug.LogWarning("[InstrumentShop] No save to record the fitment in — refusing rather than " +
                                 "eat the coin.", this);
                return false;
            }

            string hullId = TargetHull(save);
            if (string.IsNullOrEmpty(hullId))
            {
                Debug.Log($"[InstrumentShop] No boat to fit {offer.DisplayName} to — come back aboard.");
                return false;
            }
            if (InstrumentLocker.Owns(save, hullId, offer.Id))
            {
                Debug.Log($"[InstrumentShop] {hullId} already carries {offer.DisplayName} ({offer.Id}).");
                return false;
            }

            if (!wallet.TrySpend(offer.Price))
            {
                Debug.Log($"[InstrumentShop] Can't afford {offer.DisplayName}: need ₲{offer.Price}, " +
                          $"have ₲{wallet.Money}.");
                return false;
            }

            InstrumentLocker.Add(save, hullId, offer.Id);
            if (ReferenceEquals(save, GameServices.Save?.Current)) GameServices.Save?.Save();

            EventBus.Publish(new InstrumentPurchased(offer.Id, hullId, offer.Price));
            LastPurchaseSucceeded = true;
            Debug.Log($"[InstrumentShop] Fitted {offer.DisplayName} ({offer.Id}) to {hullId} for ₲{offer.Price}.");
            _onPurchased?.Invoke(offer.Price);
            return true;
        }

        /// <summary>The hull a purchase here would be fitted to — the boat the player is aboard. Public +
        /// static so the buy screen can NAME it on the row rather than making the player guess.</summary>
        public static string TargetHull(SaveData save) => save != null ? save.ActiveHullId : null;
    }
}
