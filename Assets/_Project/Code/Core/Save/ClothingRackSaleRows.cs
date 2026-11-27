using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The one place a clothing rack's saved sales are read and written</b> (save v27,
    /// <see cref="SaveData.ClothingRackSales"/>). A rack's stock left is its Def's stock less
    /// <see cref="Sold"/>; the rack never saves its own count. The list is kept as the load repair leaves it
    /// (sorted by day, rack, garment; equal rows folded), so saving, loading and saving again writes the
    /// same rows. Keeps no state of its own; static and null-safe.
    /// </summary>
    public static class ClothingRackSaleRows
    {
        /// <summary>The order rows are kept in: day, rack, garment.</summary>
        public static int Compare(ClothingRackSaleDto a, ClothingRackSaleDto b)
        {
            int c = a.Day.CompareTo(b.Day);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.RackId, b.RackId);
            if (c != 0) return c;
            return string.CompareOrdinal(a.ClothingId, b.ClothingId);
        }

        /// <summary>Every unit of <paramref name="clothingId"/> sold off <paramref name="rackId"/>, on any day.
        /// 0 for a null save or an empty id.</summary>
        public static int Sold(SaveData save, string rackId, string clothingId)
        {
            if (save?.ClothingRackSales == null || string.IsNullOrEmpty(rackId) || string.IsNullOrEmpty(clothingId))
                return 0;
            long total = 0;
            for (int i = 0; i < save.ClothingRackSales.Count; i++)
            {
                ClothingRackSaleDto r = save.ClothingRackSales[i];
                if (r == null || r.Units <= 0) continue;
                if (string.Equals(r.RackId, rackId, StringComparison.Ordinal)
                    && string.Equals(r.ClothingId, clothingId, StringComparison.Ordinal))
                    total += r.Units;
            }
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        /// <summary>
        /// Record <paramref name="units"/> of <paramref name="clothingId"/> sold off <paramref name="rackId"/>
        /// on game day <paramref name="day"/>: added to that day's row, or a new row in its place in the
        /// order. Returns false, changing nothing, for a null save, an empty id, units under 1 or a negative
        /// day.
        /// </summary>
        public static bool Record(SaveData save, string rackId, string clothingId, int units, int day)
        {
            if (save == null || string.IsNullOrEmpty(rackId) || string.IsNullOrEmpty(clothingId)
                || units <= 0 || day < 0)
                return false;
            save.ClothingRackSales ??= new List<ClothingRackSaleDto>();
            List<ClothingRackSaleDto> rows = save.ClothingRackSales;
            var sale = new ClothingRackSaleDto { RackId = rackId, ClothingId = clothingId, Units = units, Day = day };
            int at = 0;
            for (; at < rows.Count; at++)
            {
                ClothingRackSaleDto r = rows[at];
                if (r == null) continue;
                int c = Compare(r, sale);
                if (c == 0)
                {
                    long sum = (long)r.Units + units;
                    r.Units = sum > int.MaxValue ? int.MaxValue : (int)sum;
                    return true;
                }
                if (c > 0) break;
            }
            rows.Insert(at, sale);
            return true;
        }
    }
}
