namespace HiddenHarbours.Core
{
    /// <summary>What came of asking to buy a garment off a rack. Anything but <see cref="Bought"/> changed
    /// nothing: no money moved, the rack is as it was, he owns and wears what he did. Append-only.</summary>
    public enum ClothingBuyResult
    {
        /// <summary>Paid for once, one taken off the rack, his, and put on.</summary>
        Bought = 0,
        /// <summary>This rack does not offer that garment.</summary>
        NotStocked = 1,
        /// <summary>None left on the rack.</summary>
        SoldOut = 2,
        /// <summary>His money does not cover the price.</summary>
        TooDear = 3,
        /// <summary>He owns one already (a garment is owned once).</summary>
        AlreadyOwned = 4,
        /// <summary>There is no wallet to pay from (nothing loaded).</summary>
        NoWallet = 5,
    }

    /// <summary>
    /// <b>Buying a garment off a rack</b> (store clothing, pass 1): the one transaction, pure over its
    /// seams. Every refusal is checked before anything moves, then the money is taken ONCE, one garment
    /// comes off the rack, he owns it and puts it on.
    /// </summary>
    public static class ClothingShop
    {
        /// <summary>
        /// Buy one <paramref name="def"/> from <paramref name="stock"/> for <paramref name="wardrobe"/>'s
        /// player, paying from <paramref name="wallet"/>. On <see cref="ClothingBuyResult.Bought"/> he wears
        /// it; a price of 0 takes no money.
        /// </summary>
        public static ClothingBuyResult TryBuy(ClothingRackStock stock, PlayerWardrobe wardrobe, IWallet wallet,
                                               ClothingDef def)
        {
            if (stock == null || wardrobe == null || def == null || !stock.Stocks(def))
                return ClothingBuyResult.NotStocked;
            if (wardrobe.Owns(def) || wardrobe.Owns(def.Id)) return ClothingBuyResult.AlreadyOwned;
            if (stock.Left(def) <= 0) return ClothingBuyResult.SoldOut;
            if (wallet == null) return ClothingBuyResult.NoWallet;
            int price = def.Price > 0 ? def.Price : 0;
            if (price > 0)
            {
                if (wallet.Money < price) return ClothingBuyResult.TooDear;
                if (!wallet.TrySpend(price)) return ClothingBuyResult.TooDear;
            }
            stock.TryTakeOne(def);   // checked above: cannot fail
            wardrobe.Grant(def);     // checked above: cannot fail
            wardrobe.Wear(def);
            return ClothingBuyResult.Bought;
        }
    }
}
