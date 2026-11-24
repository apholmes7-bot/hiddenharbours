using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>How a press on the slip boat came out.</summary>
    public enum SlipBoatSaleResult
    {
        /// <summary>No sale, no save or no purse to read: nothing happened.</summary>
        NoGame,
        /// <summary>This game has no boat on the slip (an older save, or she has gone): nothing happened.</summary>
        NotForSale,
        /// <summary>She is already his: nothing happened.</summary>
        AlreadyHis,
        /// <summary>The purse is short of the price: nothing was spent.</summary>
        TooDear,
        /// <summary>Paid, and she is his.</summary>
        Bought,
    }

    /// <summary>
    /// <b>The sale of the boat on the slip</b>, as one pure step over the save and the purse. No scene, no
    /// Unity object: the slip dory's component calls it, and the tests call it the same way.
    ///
    /// <para><b>What a sale writes.</b> The price leaves the purse, the hull id joins the save's owned boats
    /// (once, however often he presses), and a <see cref="BoatPurchased"/> goes out on the bus so the rest
    /// of the game hears it the way it hears any boat bought. The repaired list is not touched: she is
    /// his and she is still broken.</para>
    /// </summary>
    public static class SlipBoatSale
    {
        /// <summary>True if the save already lists this sale's boat among his.</summary>
        public static bool IsHis(SlipBoatSaleDef sale, ISaveService save)
            => sale != null && save?.Current?.OwnedBoats != null
               && !string.IsNullOrEmpty(sale.BoatId) && save.Current.OwnedBoats.Contains(sale.BoatId);

        /// <summary>What a press would do right now, without doing it.</summary>
        public static SlipBoatSaleResult Peek(SlipBoatSaleDef sale, ISaveService save, IWallet wallet)
        {
            if (sale == null || save?.Current == null || string.IsNullOrEmpty(sale.BoatId))
                return SlipBoatSaleResult.NoGame;
            if (!SlipDoryStory.IsOnTheSlip(save)) return SlipBoatSaleResult.NotForSale;
            if (IsHis(sale, save)) return SlipBoatSaleResult.AlreadyHis;
            if (wallet == null) return SlipBoatSaleResult.NoGame;
            if (wallet.Money < sale.Price) return SlipBoatSaleResult.TooDear;
            return SlipBoatSaleResult.Bought;
        }

        /// <summary>
        /// Buy her if the purse allows. Every result but <see cref="SlipBoatSaleResult.Bought"/> changes
        /// nothing at all.
        /// </summary>
        public static SlipBoatSaleResult TryBuy(SlipBoatSaleDef sale, ISaveService save, IWallet wallet)
        {
            SlipBoatSaleResult would = Peek(sale, save, wallet);
            if (would != SlipBoatSaleResult.Bought) return would;
            if (!wallet.TrySpend(sale.Price)) return SlipBoatSaleResult.TooDear;

            // One file write for the whole sale: the purse, the owned list and the purchase record.
            using (save.DeferWrites())
            {
                var owned = save.Current.OwnedBoats ??= new System.Collections.Generic.List<string>();
                if (!owned.Contains(sale.BoatId)) owned.Add(sale.BoatId);
                EventBus.Publish(new BoatPurchased(sale.BoatId, sale.Price,
                    new TradeCounterparty(sale.SellerNpcId, sale.Id, null)));
                save.Save();
            }
            return SlipBoatSaleResult.Bought;
        }

        /// <summary>The thought that goes with a result, price written in; null for a press that says
        /// nothing (no game, not for sale).</summary>
        public static string LineFor(SlipBoatSaleDef sale, SlipBoatSaleResult result)
        {
            if (sale == null) return null;
            switch (result)
            {
                case SlipBoatSaleResult.TooDear:    return sale.WithPrice(sale.TooDearLine);
                case SlipBoatSaleResult.Bought:     return sale.WithPrice(sale.BoughtLine);
                case SlipBoatSaleResult.AlreadyHis: return sale.WithPrice(sale.NeedsMendingLine);
                default:                            return null;
            }
        }
    }
}
