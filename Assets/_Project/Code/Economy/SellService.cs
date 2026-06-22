using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Executes wharf sales for the sell screen (VS-18) at marginal, self-glutting prices — so the
    /// total the screen displays is exactly the coin the player receives. Reads supply from the live
    /// <see cref="Market"/>, pays the <see cref="IWallet"/>, removes the sold catch from the
    /// <see cref="IHold"/>, registers the glut (so future prices drop), and raises the existing
    /// <c>CatchSold</c> signal so the HUD payout flash / money readout react — no new events. Static
    /// with Core-contract inputs → EditMode-testable without the screen.
    ///
    /// <para>Distinct from <see cref="FishBuyer.SellAll"/>, which prices a whole batch at one pre-glut
    /// price (instant "sell the lot"). This path glutts within the sale so the player sees the price
    /// fall per unit — the wharf now opens this screen instead of the instant sale.</para>
    /// </summary>
    public static class SellService
    {
        /// <summary>How many of <paramref name="speciesId"/> are in the hold.</summary>
        public static int CountOf(IHold hold, string speciesId)
        {
            if (hold == null || speciesId == null) return 0;
            int n = 0;
            var items = hold.Items;
            for (int i = 0; i < items.Count; i++)
                if (items[i].SpeciesId == speciesId) n++;
            return n;
        }

        /// <summary>₲ that selling <paramref name="quantity"/> of <paramref name="speciesId"/> would pay
        /// right now — what the screen shows. Equals <see cref="SellSpecies"/>'s payout for the same
        /// pre-sale state, so the displayed total is the coin received.</summary>
        public static int Quote(IHold hold, Market market, string speciesId, int quantity)
        {
            if (!TryGetSpecies(hold, speciesId, out CatchItem sample, out int available)) return 0;
            int q = Mathf.Clamp(quantity, 0, available);
            float supply = market != null ? market.SupplyOf(sample.Category) : 0f;
            float demand = market != null ? market.DemandFor(sample.Category) : 1f;
            return SellPricing.RunningTotal(sample.BaseValue, sample.SupplyElasticity, supply, q, demand);
        }

        /// <summary>Sell <paramref name="quantity"/> of one species at marginal prices. Returns ₲ paid
        /// (== <see cref="Quote"/> for the same pre-sale state). Removes exactly that many of that
        /// species, registers the glut, pays the wallet, raises one <c>CatchSold</c>.</summary>
        public static int SellSpecies(IHold hold, IWallet wallet, Market market, string speciesId, int quantity)
        {
            if (hold == null || wallet == null) return 0;
            if (!TryGetSpecies(hold, speciesId, out CatchItem sample, out int available)) return 0;

            int q = Mathf.Clamp(quantity, 0, available);
            if (q <= 0) return 0;

            float supply = market != null ? market.SupplyOf(sample.Category) : 0f;
            float demand = market != null ? market.DemandFor(sample.Category) : 1f;
            int total = SellPricing.RunningTotal(sample.BaseValue, sample.SupplyElasticity, supply, q, demand);

            RemoveSpecies(hold, speciesId, q);
            if (market != null) market.RegisterSale(sample.Category, q);
            wallet.Add(total);
            EventBus.Publish(new CatchSold(total, q));
            return total;
        }

        /// <summary>Sell the whole hold at marginal prices, self-glutting per category as it goes (two
        /// species of one category depress each other). Returns ₲ paid; clears the hold; pays once;
        /// raises one <c>CatchSold</c>.</summary>
        public static int SellAll(IHold hold, IWallet wallet, Market market)
        {
            if (hold == null || wallet == null || hold.UsedUnits == 0) return 0;

            // Walk the hold once, pricing each unit at its category's supply-so-far (base supply plus
            // however many of that category we have already priced into this sale).
            var soldPerCategory = new Dictionary<FishCategory, int>();
            int grandTotal = 0;
            var items = hold.Items;
            int count = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                CatchItem it = items[i];
                float baseSupply = market != null ? market.SupplyOf(it.Category) : 0f;
                float demand = market != null ? market.DemandFor(it.Category) : 1f;
                int already = soldPerCategory.TryGetValue(it.Category, out int s) ? s : 0;
                grandTotal += SellPricing.MarginalPrice(it.BaseValue, it.SupplyElasticity, baseSupply, already, demand);
                soldPerCategory[it.Category] = already + 1;
            }

            if (market != null)
                foreach (var kv in soldPerCategory)
                    market.RegisterSale(kv.Key, kv.Value);

            hold.Clear();
            wallet.Add(grandTotal);
            EventBus.Publish(new CatchSold(grandTotal, count));
            return grandTotal;
        }

        // ---- helpers ------------------------------------------------------------------------

        private static bool TryGetSpecies(IHold hold, string speciesId, out CatchItem sample, out int count)
        {
            sample = default;
            count = 0;
            if (hold == null || speciesId == null) return false;
            var items = hold.Items;
            bool found = false;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].SpeciesId != speciesId) continue;
                if (!found) { sample = items[i]; found = true; }
                count++;
            }
            return found;
        }

        // IHold exposes only Clear()/TryAdd() (no partial remove), so rebuild the hold without the sold
        // units: snapshot the keepers, clear, re-add. Cheap — holds are a handful of units.
        private static void RemoveSpecies(IHold hold, string speciesId, int quantity)
        {
            var keep = new List<CatchItem>(hold.Items.Count);
            int toRemove = quantity;
            var items = hold.Items;
            for (int i = 0; i < items.Count; i++)
            {
                CatchItem it = items[i];
                if (toRemove > 0 && it.SpeciesId == speciesId) { toRemove--; continue; }
                keep.Add(it);
            }
            hold.Clear();
            for (int i = 0; i < keep.Count; i++) hold.TryAdd(keep[i]);
        }
    }
}
