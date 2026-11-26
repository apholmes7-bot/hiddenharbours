using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>Copies a saved row into an observational view. Never writes or adopts save rows.</summary>
    internal static class SpecialOrderRowAdapter
    {
        internal static SpecialOrderState Capture(SpecialOrderDto row) => new SpecialOrderState
        {
            OrderId = row.OrderId, SellerId = row.SellerId, DeliverToId = row.DeliverToId,
            PaidPrice = row.PaidPrice, OrderedAtSeconds = row.OrderedAtSeconds,
            OrderedDay = row.OrderedDay, DueDay = row.DueDay, State = row.State,
            Lines = row.Lines?.ConvertAll(line => line == null ? null : new SpecialOrderLineState
                { ListingId = line.ListingId, Count = line.Count, PaidPrice = line.PaidPrice }).ToArray()
        };
    }
}
