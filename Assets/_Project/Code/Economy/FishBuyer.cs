using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// The wharf buyer: sells everything in a hold at MARGINAL, self-glutting market prices — each unit
    /// slides down its category's curve (and is priced at its per-category demand), exactly like the
    /// sell screen. Pays the wallet, removes the catch, and raises <c>CatchSold</c>. Closes the
    /// catch→sell loop. Hook this to an NPC/UI interaction in the Greywick scene (VS-22).
    /// </summary>
    public class FishBuyer : MonoBehaviour
    {
        [SerializeField] private Market _market;

        /// <summary>
        /// Sell the whole hold to this buyer. Returns total ₲ paid. Routes through the within-lot
        /// marginal path (<see cref="SellService.SellAll"/>) so the instant "sell the lot" slides down
        /// each category's curve — one source of truth with the sell screen, no pre-glut batch price.
        /// API unchanged.
        /// </summary>
        public int SellAll(IHold hold, IWallet wallet)
        {
            if (hold == null || wallet == null || hold.UsedUnits == 0) return 0;

            int count = hold.UsedUnits;
            // SellService prices each unit at its category's supply-so-far + demand, pays the wallet,
            // clears the hold, and raises the single CatchSold (no duplicate event here).
            int total = SellService.SellAll(hold, wallet, _market);
            Debug.Log($"[Market] Sold {count} for ₲{total}.");
            return total;
        }
    }
}
