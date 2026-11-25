using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Keeping the saved store purchases in their one order</b> (save v26,
    /// <see cref="SaveData.StorePurchases"/>). A store hands in what the player took off its shelves and its
    /// rows in the save are replaced by exactly that; every other store's rows are untouched. The list is
    /// left as the load repair leaves it (sorted by day, seconds, shop, line; rows equal in all four folded
    /// into one with their units added), so saving, loading and saving again writes the same rows.
    /// </summary>
    public static class StorePurchaseRows
    {
        private static readonly Comparison<StorePurchaseRowDto> Order = Compare;

        /// <summary>The replay order: day, seconds, shop, line (the order the load repair sorts into).</summary>
        public static int Compare(StorePurchaseRowDto a, StorePurchaseRowDto b)
        {
            int c = a.Day.CompareTo(b.Day);
            if (c != 0) return c;
            c = a.Seconds.CompareTo(b.Seconds);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.ShopId, b.ShopId);
            if (c != 0) return c;
            return string.CompareOrdinal(a.ShelfLineId, b.ShelfLineId);
        }

        /// <summary>
        /// Replaces the rows of <paramref name="shopId"/> in <paramref name="all"/> with
        /// <paramref name="rows"/> (taken into the list as they are; rows naming another shop are ignored),
        /// then sorts and folds. Returns how many rows that shop has afterwards.
        /// </summary>
        public static int ReplaceShop(List<StorePurchaseRowDto> all, string shopId, List<StorePurchaseRowDto> rows)
        {
            if (all == null || string.IsNullOrEmpty(shopId)) return 0;
            for (int i = all.Count - 1; i >= 0; i--)
                if (all[i] == null || string.Equals(all[i].ShopId, shopId, StringComparison.Ordinal)) all.RemoveAt(i);
            if (rows != null)
                for (int i = 0; i < rows.Count; i++)
                    if (rows[i] != null && string.Equals(rows[i].ShopId, shopId, StringComparison.Ordinal))
                        all.Add(rows[i]);

            all.Sort(Order);
            for (int i = all.Count - 1; i > 0; i--)
            {
                if (Compare(all[i - 1], all[i]) != 0) continue;
                all[i - 1].Units += all[i].Units;
                all.RemoveAt(i);
            }

            int kept = 0;
            for (int i = 0; i < all.Count; i++)
                if (string.Equals(all[i].ShopId, shopId, StringComparison.Ordinal)) kept++;
            return kept;
        }

        /// <summary>How many rows <paramref name="shopId"/> has in <paramref name="all"/>.</summary>
        public static int CountFor(List<StorePurchaseRowDto> all, string shopId)
        {
            if (all == null) return 0;
            int n = 0;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && string.Equals(all[i].ShopId, shopId, StringComparison.Ordinal)) n++;
            return n;
        }
    }
}