using System;
using System.Collections.Generic;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Detached transport rows matching the reserved v26 contract. These are not save fields or Core
    /// DTOs. A later integration copies them to/from those DTOs and persists the counter with the rows.
    /// </summary>
    [Serializable]
    public sealed class SpecialOrderRows
    {
        public int NextSpecialOrderNumber = 1;
        public List<SpecialOrderRow> SpecialOrders = new();
    }

    [Serializable]
    public sealed class SpecialOrderRow
    {
        public string OrderId = "";
        public string SellerId = "";
        public string DeliverToId = "";
        public List<SpecialOrderLineRow> Lines = new();
        public int PaidPrice;
        public double OrderedAtSeconds;
        public int OrderedDay;
        public int DueDay;
        public int State;
    }

    [Serializable]
    public sealed class SpecialOrderLineRow
    {
        public string ListingId = "";
        public int Count = 1;
        public int PaidPrice;
    }

    public static class SpecialOrderRowAdapter
    {
        public static SpecialOrderRows ToRows(SpecialOrderBook book)
        {
            if (book == null) throw new ArgumentNullException(nameof(book));
            var snapshot = book.Capture();
            var rows = new SpecialOrderRows { NextSpecialOrderNumber = snapshot.NextSpecialOrderNumber };
            foreach (var order in snapshot.Orders)
            {
                var row = new SpecialOrderRow
                {
                    OrderId = order.OrderId, SellerId = order.SellerId, DeliverToId = order.DeliverToId,
                    PaidPrice = order.PaidPrice, OrderedAtSeconds = order.OrderedAtSeconds,
                    OrderedDay = order.OrderedDay, DueDay = order.DueDay, State = order.State,
                    Lines = order.Lines == null ? null : new List<SpecialOrderLineRow>()
                };
                if (order.Lines != null)
                    foreach (var line in order.Lines)
                        row.Lines.Add(line == null ? null : new SpecialOrderLineRow
                            { ListingId = line.ListingId, Count = line.Count, PaidPrice = line.PaidPrice });
                rows.SpecialOrders.Add(row);
            }
            return rows;
        }

        public static void Restore(SpecialOrderBook book, SpecialOrderRows rows)
        {
            if (book == null) throw new ArgumentNullException(nameof(book));
            if (rows == null) return;
            var orders = new List<SpecialOrderState>();
            if (rows.SpecialOrders != null)
                foreach (var row in rows.SpecialOrders)
                    orders.Add(row == null ? null : new SpecialOrderState
                    {
                        OrderId = row.OrderId, SellerId = row.SellerId, DeliverToId = row.DeliverToId,
                        PaidPrice = row.PaidPrice, OrderedAtSeconds = row.OrderedAtSeconds,
                        OrderedDay = row.OrderedDay, DueDay = row.DueDay, State = row.State,
                        Lines = row.Lines?.ConvertAll(line => line == null ? null : new SpecialOrderLineState
                            { ListingId = line.ListingId, Count = line.Count, PaidPrice = line.PaidPrice }).ToArray()
                    });
            book.Restore(new SpecialOrderSnapshot
                { NextSpecialOrderNumber = rows.NextSpecialOrderNumber, Orders = orders.ToArray() });
        }
    }
}
