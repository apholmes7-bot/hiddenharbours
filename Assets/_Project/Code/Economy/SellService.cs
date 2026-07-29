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
    /// <para><b>Freshness (M1 §7.3).</b> Every sale is freshness-aware: a unit's price is scaled by
    /// its own <see cref="SpoilContext.ValueMultiplier"/> (two units of one species can differ), and
    /// <see cref="SpoilContext.IsSellable"/> is a HARD gate applied <b>before</b> the pricing maths —
    /// <c>SellPricing.UnitPrice</c> floors every priced unit at 1₲, so the multiplier alone could
    /// never make rot worthless; refused units simply never reach the till. Spoiled catch stays in
    /// the hold (rubbish occupies space until dumped — the dispose verb is the escape). The public
    /// entry points capture the live context (<see cref="SpoilContext.Capture"/>); the explicit-
    /// context overloads exist so tests pin exact numbers without wiring services. Legacy items
    /// (SpoilPerDay 0) price bit-identically to the pre-freshness path.</para>
    ///
    /// <para>Distinct from <see cref="FishBuyer.SellAll"/>, which prices a whole batch at one pre-glut
    /// price (instant "sell the lot"). This path glutts within the sale so the player sees the price
    /// fall per unit — the wharf now opens this screen instead of the instant sale.</para>
    /// </summary>
    public static class SellService
    {
        /// <summary>How many of <paramref name="speciesId"/> are in the hold (sellable or not).</summary>
        public static int CountOf(IHold hold, string speciesId)
        {
            if (hold == null || speciesId == null) return 0;
            int n = 0;
            var items = hold.Items;
            for (int i = 0; i < items.Count; i++)
                if (items[i].SpeciesId == speciesId) n++;
            return n;
        }

        /// <summary>How many of <paramref name="speciesId"/> a buyer would still take right now.</summary>
        public static int CountSellableOf(IHold hold, string speciesId, in SpoilContext spoil)
        {
            if (hold == null || speciesId == null) return 0;
            int n = 0;
            var items = hold.Items;
            for (int i = 0; i < items.Count; i++)
                if (items[i].SpeciesId == speciesId && spoil.IsSellable(items[i])) n++;
            return n;
        }

        /// <summary>How many units across the whole hold are RUBBISH — refused by every buyer, taking
        /// up space until dumped. The screen's "gone off" read and the dispose verb's count.</summary>
        public static int CountSpoiled(IHold hold, in SpoilContext spoil)
        {
            if (hold == null) return 0;
            int n = 0;
            var items = hold.Items;
            for (int i = 0; i < items.Count; i++)
                if (spoil.IsSpoiled(items[i])) n++;
            return n;
        }

        /// <summary>₲ that selling <paramref name="quantity"/> of <paramref name="speciesId"/> would pay
        /// right now — what the screen shows. Equals <see cref="SellSpecies(IHold,IWallet,Market,string,int)"/>'s
        /// payout for the same pre-sale state, so the displayed total is the coin received.</summary>
        public static int Quote(IHold hold, Market market, string speciesId, int quantity)
            => Quote(hold, market, speciesId, quantity, SpoilContext.Capture());

        /// <summary>The explicit-context <see cref="Quote(IHold,Market,string,int)"/> (tests pin numbers
        /// without service wiring). Walks the SAME sellable units in the SAME hold order the sale does.</summary>
        public static int Quote(IHold hold, Market market, string speciesId, int quantity, in SpoilContext spoil)
        {
            if (hold == null || speciesId == null) return 0;
            var items = hold.Items;
            float supply = 0f, demand = 1f;
            bool marketRead = false;

            int total = 0;
            int taken = 0;
            for (int i = 0; i < items.Count && taken < quantity; i++)
            {
                CatchItem it = items[i];
                if (it.SpeciesId != speciesId || !spoil.IsSellable(it)) continue;
                if (!marketRead)
                {
                    supply = market != null ? market.SupplyOf(it.Category) : 0f;
                    demand = market != null ? market.DemandFor(it.Category) : 1f;
                    marketRead = true;
                }
                total += SellPricing.MarginalPrice(it.BaseValue, it.SupplyElasticity, supply, taken,
                                                   demand, spoil.ValueMultiplier(it));
                taken++;
            }
            return total;
        }

        /// <summary>Sell <paramref name="quantity"/> of one species at marginal prices — sellable units
        /// only, in hold order. Returns ₲ paid (== <see cref="Quote(IHold,Market,string,int)"/> for the
        /// same pre-sale state). Removes exactly the sold units (spoiled ones STAY — rubbish is carried,
        /// not laundered), registers the glut, pays the wallet, raises one <c>CatchSold</c>.</summary>
        public static int SellSpecies(IHold hold, IWallet wallet, Market market, string speciesId, int quantity)
            => SellSpecies(hold, wallet, market, speciesId, quantity, SpoilContext.Capture());

        /// <summary>The explicit-context <see cref="SellSpecies(IHold,IWallet,Market,string,int)"/>.</summary>
        public static int SellSpecies(IHold hold, IWallet wallet, Market market, string speciesId,
                                      int quantity, in SpoilContext spoil)
        {
            if (hold == null || wallet == null || speciesId == null || quantity <= 0) return 0;

            var items = hold.Items;
            var keep = new List<CatchItem>(items.Count);
            float supply = 0f, demand = 1f;
            bool marketRead = false;
            FishCategory soldCategory = default;   // read off the first sold unit (one species = one category)

            int total = 0;
            int sold = 0;
            for (int i = 0; i < items.Count; i++)
            {
                CatchItem it = items[i];
                if (sold >= quantity || it.SpeciesId != speciesId || !spoil.IsSellable(it))
                {
                    keep.Add(it);
                    continue;
                }
                if (!marketRead)
                {
                    supply = market != null ? market.SupplyOf(it.Category) : 0f;
                    demand = market != null ? market.DemandFor(it.Category) : 1f;
                    soldCategory = it.Category;
                    marketRead = true;
                }
                total += SellPricing.MarginalPrice(it.BaseValue, it.SupplyElasticity, supply, sold,
                                                   demand, spoil.ValueMultiplier(it));
                sold++;
            }
            if (sold == 0) return 0;

            Rebuild(hold, keep);
            if (market != null) market.RegisterSale(soldCategory, sold);
            wallet.Add(total);
            EventBus.Publish(new CatchSold(total, sold));
            return total;
        }

        /// <summary>Sell every SELLABLE unit in the hold at marginal prices, self-glutting per category
        /// as it goes (two species of one category depress each other). Spoiled units stay in the hold —
        /// no buyer takes rubbish at any price. Returns ₲ paid; pays once; raises one <c>CatchSold</c>
        /// (only if anything actually sold).</summary>
        public static int SellAll(IHold hold, IWallet wallet, Market market)
            => SellAll(hold, wallet, market, SpoilContext.Capture());

        /// <summary>The explicit-context <see cref="SellAll(IHold,IWallet,Market)"/>.</summary>
        public static int SellAll(IHold hold, IWallet wallet, Market market, in SpoilContext spoil)
        {
            if (hold == null || wallet == null || hold.UsedUnits == 0) return 0;

            // Walk the hold once, pricing each sellable unit at its category's supply-so-far (base
            // supply plus however many of that category we have already priced into this sale).
            var soldPerCategory = new Dictionary<FishCategory, int>();
            var keep = new List<CatchItem>(hold.Items.Count);
            int grandTotal = 0;
            int soldCount = 0;
            var items = hold.Items;
            for (int i = 0; i < items.Count; i++)
            {
                CatchItem it = items[i];
                if (!spoil.IsSellable(it)) { keep.Add(it); continue; }   // rubbish: refused, stays aboard

                float baseSupply = market != null ? market.SupplyOf(it.Category) : 0f;
                float demand = market != null ? market.DemandFor(it.Category) : 1f;
                int already = soldPerCategory.TryGetValue(it.Category, out int s) ? s : 0;
                grandTotal += SellPricing.MarginalPrice(it.BaseValue, it.SupplyElasticity, baseSupply,
                                                        already, demand, spoil.ValueMultiplier(it));
                soldPerCategory[it.Category] = already + 1;
                soldCount++;
            }
            if (soldCount == 0) return 0;   // a hold of pure rubbish sells nothing — dump it instead

            if (market != null)
                foreach (var kv in soldPerCategory)
                    market.RegisterSale(kv.Key, kv.Value);

            Rebuild(hold, keep);
            wallet.Add(grandTotal);
            EventBus.Publish(new CatchSold(grandTotal, soldCount));
            return grandTotal;
        }

        // ---- helpers ------------------------------------------------------------------------

        // IHold exposes only Clear()/TryAdd() (no partial remove), so rebuild the hold with the
        // keepers: snapshot, clear, re-add. Cheap — holds are a handful of units.
        private static void Rebuild(IHold hold, List<CatchItem> keep)
        {
            hold.Clear();
            for (int i = 0; i < keep.Count; i++) hold.TryAdd(keep[i]);
        }
    }
}
