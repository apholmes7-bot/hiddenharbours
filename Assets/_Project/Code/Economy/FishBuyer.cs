using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// The wharf buyer: sells everything in a hold at current market prices, pays the wallet, and
    /// then registers the sales (so the batch sells at the pre-glut price, but future prices drop).
    /// Closes the catch→sell loop. Hook this to an NPC/UI interaction in the Greywick scene (VS-22).
    /// </summary>
    public class FishBuyer : MonoBehaviour
    {
        [SerializeField] private Market _market;

        /// <summary>Sell the whole hold to this buyer. Returns total ₲ paid.</summary>
        public int SellAll(IHold hold, IWallet wallet)
        {
            if (hold == null || wallet == null || hold.UsedUnits == 0) return 0;

            var items = hold.Items;
            int total = 0;
            int count = items.Count;

            // Price the whole batch at the current (pre-sale) market.
            for (int i = 0; i < items.Count; i++)
            {
                CatchItem it = items[i];
                float mult = _market != null ? _market.PriceMultiplier(it.Category, it.SupplyElasticity) : 1f;
                total += Mathf.Max(1, Mathf.RoundToInt(it.BaseValue * mult));
            }

            // Then depress future prices for what we just landed.
            if (_market != null)
                for (int i = 0; i < items.Count; i++)
                    _market.RegisterSale(items[i].Category);

            hold.Clear();
            wallet.Add(total);
            EventBus.Publish(new CatchSold(total, count));
            Debug.Log($"[Market] Sold {count} for ₲{total}.");
            return total;
        }
    }
}
