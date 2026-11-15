using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>Resolves existing vendor seller ids through data without scene or prefab changes.</summary>
    public static class SellerDirectory
    {
        public const string ResourceFolder = "Sellers";
        public const string AssetFolder = "Assets/_Project/Data/Resources/Sellers";

        public static TradeCounterparty Resolve(string sellerId)
            => Resolve(sellerId, Resources.LoadAll<SellerDef>(ResourceFolder));

        /// <summary>Explicit data seam for tests. Ambiguous identities fail to unknown, never first-wins.</summary>
        public static TradeCounterparty Resolve(string sellerId, System.Collections.Generic.IReadOnlyList<SellerDef> sellers)
        {
            SellerDef found = null;
            if (!string.IsNullOrWhiteSpace(sellerId) && sellers != null)
                for (int i = 0; i < sellers.Count; i++)
                    if (sellers[i] != null && sellers[i].Id == sellerId)
                    {
                        if (found != null) return new TradeCounterparty(null, sellerId, null);
                        found = sellers[i];
                    }
            return found != null ? found.Counterparty : new TradeCounterparty(null, sellerId, null);
        }
    }
}
