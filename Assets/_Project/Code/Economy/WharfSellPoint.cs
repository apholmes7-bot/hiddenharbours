using UnityEngine;
using UnityEngine.Events;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// The wharf sell interaction: the interactable point at Nine Mile Creek that closes the
    /// catch→sell loop (P4 "Earn It, Then Automate It"). It hands the dory's hold to the
    /// <see cref="FishBuyer"/>, which prices the batch at the current pre-glut market, pays the
    /// wallet, registers the sales (so future prices drop), and raises <c>CatchSold</c>.
    ///
    /// <para><c>CatchSold</c> (raised by <see cref="FishBuyer"/>) is the cross-module signal the HUD
    /// payout toast (ui-ux, VS-17/18) subscribes to — economy never references the UI module. The
    /// <c>_onSold</c> UnityEvent is an inspector-level hook so a designer can bind a local reaction
    /// (a sound, a particle) without code. The actual player-facing payout UI/toast is ui-ux's job
    /// (handoff per coordination §7) — no UI is built here.</para>
    /// </summary>
    public class WharfSellPoint : MonoBehaviour
    {
        [Tooltip("The buyer that prices and pays out the hold (on the Wharf).")]
        [SerializeField] private FishBuyer _buyer;
        [Tooltip("A GameObject carrying an IHold (the dory's ShipHold).")]
        [SerializeField] private GameObject _holdProvider;
        [Tooltip("A GameObject carrying an IWallet (the player's PlayerWallet).")]
        [SerializeField] private GameObject _walletProvider;

        [Tooltip("Inspector hook fired with the ₲ payout on a successful sale (UI can bind this later).")]
        [SerializeField] private UnityEvent<int> _onSold;

        private IHold _hold;
        private IWallet _wallet;

        /// <summary>The ₲ paid by the most recent sale (0 if the last attempt sold nothing).</summary>
        public int LastPayout { get; private set; }

        /// <summary>
        /// How many units left the hold in the most recent sale (0 if it sold nothing).
        ///
        /// <para><b>This, not <see cref="LastPayout"/>, is what says a sale happened.</b> A lot can price
        /// at ₲0 — a glutted market, a species nobody wants — and "she took it and gave you nothing"
        /// is a different thing to say than "your pail is empty". The clerk's sell row needs to tell them
        /// apart.</para>
        /// </summary>
        public int LastUnitsSold { get; private set; }

        private void Awake()
        {
            if (_holdProvider != null) _hold = _holdProvider.GetComponent<IHold>();
            if (_hold == null)
                Debug.LogWarning("[Wharf] No IHold found on the hold provider.", this);

            if (_walletProvider != null) _wallet = _walletProvider.GetComponent<IWallet>();
            if (_wallet == null)
                Debug.LogWarning("[Wharf] No IWallet found on the wallet provider.", this);
        }

        /// <summary>
        /// The no-arg interaction entrypoint (what dev input / the future Interact intent calls).
        /// VS-18: opens the <see cref="SellScreen"/> (choose a species + quantity, marginal pricing)
        /// instead of instant-selling the whole hold. The instant batch sale stays available via the
        /// <see cref="Sell(IHold, IWallet)"/> seam (tests/automation); returns 0 here because the
        /// payout now happens through the screen, not synchronously.
        /// </summary>
        public int Sell()
        {
            if (_hold == null || _wallet == null) return 0;
            if (_hold.UsedUnits == 0)
            {
                Debug.Log("[Wharf] Hold's empty — nothing to sell.");
                return 0;
            }
            SellScreen.Open(_hold, _wallet);
            return 0;
        }

        /// <summary>
        /// Core sell seam (testable): guards inputs, delegates the economics to the buyer, surfaces
        /// the payout via <see cref="LastPayout"/> and the <c>_onSold</c> hook. Returns total ₲ paid.
        /// </summary>
        public int Sell(IHold hold, IWallet wallet)
        {
            if (_buyer == null)
            {
                Debug.LogWarning("[Wharf] No buyer wired — can't sell.", this);
                return 0;
            }
            if (hold == null || wallet == null) return 0;

            if (hold.UsedUnits == 0)
            {
                Debug.Log("[Wharf] Hold's empty — nothing to sell.");
                return 0;
            }

            int before = hold.UsedUnits;
            int paid = _buyer.SellAll(hold, wallet);
            LastPayout = paid;
            LastUnitsSold = Mathf.Max(0, before - hold.UsedUnits);
            Debug.Log($"[Wharf] Sold the hold for ₲{paid}.");
            if (paid > 0) _onSold?.Invoke(paid);
            return paid;
        }

        /// <summary>
        /// Sell the WIRED hold to the WIRED wallet, and answer with the payout.
        ///
        /// <para><b>This is what the clerk's sell row fronts</b> (owner ruling R7, 2026-08-27). It is the
        /// existing <see cref="Sell(IHold, IWallet)"/> seam called with this counter's own providers — the
        /// same <see cref="FishBuyer"/>, the same market, the same <c>SellPricing</c>. No new economics
        /// are written here, and nothing is quoted twice.</para>
        ///
        /// <para><b>It exists because <see cref="Sell()"/> cannot serve.</b> The no-arg entrypoint opens
        /// <c>SellScreen</c> and returns 0 by design, which is exactly what a sale spoken in a bubble must
        /// not do: the row's whole point is that a sale at a counter is something a person tells you, with
        /// them still standing there. <c>SellScreen</c> is untouched and keeps its own callers.</para>
        /// </summary>
        public int SellOverTheCounter()
        {
            LastUnitsSold = 0;
            return Sell(_hold, _wallet);
        }
    }
}
