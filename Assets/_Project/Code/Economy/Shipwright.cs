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
    ///
    /// <para><b>Damaged → repair (St Peters opening).</b> An offer flagged <see cref="ShipwrightOffer.StartsDamaged"/>
    /// is bought/owned like any boat (it still raises <c>BoatPurchased</c> so the fleet grants it) but
    /// stays UNUSABLE until the player pays <see cref="TryRepair()"/>: that deducts the repair fee, marks
    /// the hull repaired in the save (<see cref="RepairLedger"/>), and raises <c>BoatRepaired</c>. A
    /// non-damaged buy is repaired-on-grant, so it's usable immediately. The "usable" gate itself
    /// (boarding) is gameplay-systems' — it reads <see cref="RepairLedger.IsRepaired"/> off the save.</para>
    /// </summary>
    public class Shipwright : MonoBehaviour
    {
        [Tooltip("The boat offered for sale here (id + price).")]
        [SerializeField] private ShipwrightOffer _offer;
        [Tooltip("A GameObject carrying an IWallet (the player's PlayerWallet).")]
        [SerializeField] private GameObject _walletProvider;

        [Tooltip("Inspector hook fired with the ₲ price on a successful purchase (UI can bind this later).")]
        [SerializeField] private UnityEvent<int> _onPurchased;
        [Tooltip("Inspector hook fired with the ₲ repair fee when a damaged boat is repaired.")]
        [SerializeField] private UnityEvent<int> _onRepaired;

        private IWallet _wallet;

        /// <summary>The boat offered for sale here (id + price). Null until wired.</summary>
        public ShipwrightOffer Offer => _offer;

        /// <summary>True iff the most recent <see cref="TryBuy()"/> went through.</summary>
        public bool LastPurchaseSucceeded { get; private set; }

        /// <summary>True iff the most recent <see cref="TryRepair()"/> went through.</summary>
        public bool LastRepairSucceeded { get; private set; }

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
        ///
        /// <para>If the offer is <b>not</b> damaged, the boat is marked repaired (usable) on grant. If it
        /// <b>is</b> damaged, it's owned but left unrepaired — the player must <see cref="TryRepair()"/>
        /// it before it's usable. Repair state is written to the live save (null-safe in EditMode).</para>
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

            // A boat bought in usable condition is repaired the moment it's owned; a damaged boat stays
            // unusable until TryRepair pays for it. Mark on the live save (no-op headless / no save).
            if (!offer.StartsDamaged)
                RepairLedger.MarkRepaired(GameServices.Save?.Current, offer.BoatId);

            LastPurchaseSucceeded = true;
            string condition = offer.StartsDamaged ? "DAMAGED — needs repair" : "ready to sail";
            Debug.Log($"[Shipwright] Bought {offer.DisplayName} ({offer.BoatId}) for ₲{offer.Price} ({condition}).");
            _onPurchased?.Invoke(offer.Price);
            return true;
        }

        /// <summary>The no-arg repair entrypoint (dev input / the future repair screen). Repairs the wired
        /// damaged offer with the wired wallet, against the live save.</summary>
        public bool TryRepair() => TryRepair(_offer, _wallet, GameServices.Save?.Current);

        /// <summary>
        /// Core repair seam (testable): pay <see cref="ShipwrightOffer.RepairCost"/> from the wallet to
        /// turn an owned-but-damaged boat into a usable one. On success marks the hull repaired in
        /// <paramref name="save"/> (<see cref="RepairLedger"/>), persists, and raises
        /// <c>BoatRepaired(boatId, cost)</c>. Greybox-minimal: instant repair on payment. Returns true
        /// iff a repair was paid. No-ops (false, charges nothing) when: the offer isn't damaged, the boat
        /// is already repaired, the wallet can't afford it, or inputs are null. Money is only deducted on
        /// success (<see cref="IWallet.TrySpend"/> is atomic).
        /// </summary>
        public bool TryRepair(ShipwrightOffer offer, IWallet wallet, SaveData save)
        {
            LastRepairSucceeded = false;
            if (offer == null) { Debug.LogWarning("[Shipwright] No offer to repair.", this); return false; }
            if (wallet == null) return false;
            if (!offer.StartsDamaged)
            {
                Debug.Log($"[Shipwright] {offer.DisplayName} isn't a damaged boat — nothing to repair.");
                return false;
            }
            if (RepairLedger.IsRepaired(save, offer.BoatId))
            {
                Debug.Log($"[Shipwright] {offer.DisplayName} is already repaired.");
                return false;
            }

            if (!wallet.TrySpend(offer.RepairCost))
            {
                Debug.Log($"[Shipwright] Can't afford the repair on {offer.DisplayName}: need ₲{offer.RepairCost}, have ₲{wallet.Money}.");
                return false;
            }

            RepairLedger.MarkRepaired(save, offer.BoatId);
            if (ReferenceEquals(save, GameServices.Save?.Current)) GameServices.Save?.Save();
            EventBus.Publish(new BoatRepaired(offer.BoatId, offer.RepairCost));
            LastRepairSucceeded = true;
            Debug.Log($"[Shipwright] Repaired {offer.DisplayName} ({offer.BoatId}) for ₲{offer.RepairCost} — she's ready to sail.");
            _onRepaired?.Invoke(offer.RepairCost);
            return true;
        }
    }
}
