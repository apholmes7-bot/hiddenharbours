using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// A cargo hold for landed catch. Implemented by the boat (ShipHold, in Boats); written to by
    /// Fishing and read/emptied by the market — all through this Core contract so no module
    /// reaches into another's classes.
    /// </summary>
    public interface IHold
    {
        int CapacityUnits { get; }                 // from the boat's hull (HU)
        int UsedUnits { get; }
        IReadOnlyList<CatchItem> Items { get; }

        /// <summary>Add one catch (1 HU). Returns false if the hold is full.</summary>
        bool TryAdd(CatchItem item);

        /// <summary>Empty the hold (e.g. after selling).</summary>
        void Clear();
    }
}
