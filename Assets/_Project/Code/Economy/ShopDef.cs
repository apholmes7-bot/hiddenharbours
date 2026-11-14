using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>One shop per asset. No price copies, visual assets, runtime registration or save writes.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Shop", fileName = "Shop")]
    public sealed class ShopDef : ScriptableObject
    {
        public ShopStockSpec Spec = new ShopStockSpec();

        /// <summary>Setup/content validation only. Snapshot the real seller's existing catalog listings.</summary>
        public ShopStockSpec BuildSpec(IEnumerable<ICatalogListing> listings)
        {
            if (listings == null) throw new ArgumentNullException(nameof(listings));
            var byId = new Dictionary<string, ICatalogListing>(StringComparer.Ordinal);
            foreach (var listing in listings)
                if (listing != null) byId.Add(listing.ListingId, listing);
            Spec.Validate((seller, id) => byId.TryGetValue(id, out var listing) && listing.Catalog.IsStockedBy(seller));
            foreach (var line in Spec.Stock)
            {
                var listing = byId[line.ListingId];
                // Only existing portable wares belong on a shelf. Licences and fitted instruments stay at the desk.
                int packSize = listing is BaitDef bait ? Math.Max(1, bait.LotSize) : 1;
                if (!(listing is GearOffer || listing is BaitDef || listing is SupplyDef) || line.UnitsPerItem != packSize)
                    throw new ArgumentException("Shelf listing or pack size differs from the existing counter: " + line.ListingId);
            }
            return Spec.Snapshot();
        }
    }
}
