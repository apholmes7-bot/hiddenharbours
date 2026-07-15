namespace HiddenHarbours.Core
{
    /// <summary>
    /// Raised when the player buys a pot/trap at the shipwright (the trap loop's P2 money wheel: catch
    /// lobster → afford more pots → catch more lobster). The economy side has already deducted the
    /// price and incremented the owned stock (<see cref="SaveData.PotStock"/> via <see cref="PotLocker"/>);
    /// ui-ux can subscribe for a toast and audio for a till-beat — without referencing the Economy
    /// module (cross-module talk via Core/EventBus). Keyed by the stable <b>TrapDef</b> id (e.g.
    /// "trap.lobster") — the id the stock is counted under — not the offer id.
    ///
    /// <para>Lives in its own Core/Events file (the <c>LicenseSignals</c> precedent): economy-owned
    /// signals are added additively beside <c>GameSignals.cs</c>, same EventBus, separate file
    /// (coordination.md §1 — "keep it additive").</para>
    /// </summary>
    public readonly struct PotPurchased
    {
        /// <summary>Stable TrapDef id of the pot kind bought (e.g. "trap.lobster").</summary>
        public readonly string TrapDefId;
        /// <summary>₲ paid.</summary>
        public readonly int PricePaid;
        /// <summary>Total pots of this kind OWNED after the purchase (deployed + aboard + spare).</summary>
        public readonly int OwnedCount;

        public PotPurchased(string trapDefId, int pricePaid, int ownedCount)
        {
            TrapDefId = trapDefId; PricePaid = pricePaid; OwnedCount = ownedCount;
        }
    }
}
