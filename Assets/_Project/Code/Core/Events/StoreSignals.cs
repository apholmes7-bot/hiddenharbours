namespace HiddenHarbours.Core
{
    /// <summary>
    /// Raised when the player buys a consumable SUPPLY over a counter (the island general store's ice —
    /// plan-to-m1 §7.5). The economy side has already deducted the price and incremented the counted stock
    /// (<see cref="SaveData.SupplyStock"/> via <see cref="SupplyLocker"/>); ui-ux can subscribe for a toast
    /// and a till-beat without referencing the Economy module (cross-module talk via Core/EventBus).
    /// Keyed by the stable <b>SupplyDef</b> id (e.g. "supply.ice") — the id the stock is counted under.
    ///
    /// <para>Lives beside <c>PotSignals</c>/<c>LicenseSignals</c>: economy-owned signals are added
    /// additively in their own Core/Events file, same EventBus (coordination.md §1 — "keep it additive").</para>
    /// </summary>
    public readonly struct SupplyPurchased
    {
        /// <summary>Stable SupplyDef id of the supply bought (e.g. "supply.ice").</summary>
        public readonly string SupplyId;
        /// <summary>₲ paid.</summary>
        public readonly int PricePaid;
        /// <summary>Total of this supply OWNED after the purchase.</summary>
        public readonly int OwnedCount;

        public SupplyPurchased(string supplyId, int pricePaid, int ownedCount)
        {
            SupplyId = supplyId; PricePaid = pricePaid; OwnedCount = ownedCount;
        }
    }

    /// <summary>
    /// Raised when the player buys BAIT over a counter (plan-to-m1 §7.5 — the store restocks the bait the
    /// rod and the pots eat). The economy side has already deducted the price and incremented the counted
    /// stock (<see cref="SaveData.BaitStock"/> via <see cref="TackleBox"/>). Keyed by the stable
    /// <b>BaitDef</b> id (e.g. "bait.capelin").
    /// </summary>
    public readonly struct BaitPurchased
    {
        /// <summary>Stable BaitDef id of the bait bought (e.g. "bait.capelin").</summary>
        public readonly string BaitId;
        /// <summary>₲ paid for the whole lot bought in this transaction.</summary>
        public readonly int PricePaid;
        /// <summary>How many individual baits this purchase added (the offer's lot size).</summary>
        public readonly int CountBought;
        /// <summary>Total of this bait OWNED after the purchase.</summary>
        public readonly int OwnedCount;

        public BaitPurchased(string baitId, int pricePaid, int countBought, int ownedCount)
        {
            BaitId = baitId; PricePaid = pricePaid; CountBought = countBought; OwnedCount = ownedCount;
        }
    }

    /// <summary>
    /// Raised when a one-time fee is FRONTED to the player — the mechanism behind the Aunt Ginny beat that
    /// unblocks the clam licence (plan-to-m1 §7.5's chicken-and-egg). The wallet has already been credited
    /// and the one-time flag persisted. world-content subscribes (or calls the grant's own seam from her
    /// dialogue) to put words to it; the economy never writes dialogue.
    /// </summary>
    public readonly struct FeeFronted
    {
        /// <summary>Stable flag key the one-time grant was recorded under (e.g. "ginny_fronted_clam_fee").</summary>
        public readonly string GrantKey;
        /// <summary>₲ added to the player's wallet.</summary>
        public readonly int Amount;

        public FeeFronted(string grantKey, int amount)
        {
            GrantKey = grantKey; Amount = amount;
        }
    }
}
