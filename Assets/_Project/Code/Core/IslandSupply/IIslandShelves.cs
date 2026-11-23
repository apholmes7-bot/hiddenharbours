namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>What an island store has on its shelves</b>, recomputed from the clock (living island economy slice 2,
    /// <c>docs/design/st-peters-living-economy.md</c> §5). Counts are shelf items (a jerry can, a bag of ice),
    /// each a fixed number of a good's units. They follow the store's back room: the supply run fills it, the
    /// islanders' buying through the store's hours draws it down, and the player's purchases come off it.
    ///
    /// <para>Readers in other modules (a dialogue line, a shelf presenter, the counter) use this contract and
    /// never the Economy class (rule 4). Every read is a pure function of the time asked and the purchases
    /// recorded so far; nothing here is saved.</para>
    /// </summary>
    public interface IIslandShelvesView
    {
        /// <summary>The shop these shelves belong to (<c>shop.snake_case</c>).</summary>
        string ShopId { get; }

        /// <summary>True when the store has a shelf line with this id.</summary>
        bool HasLine(string lineId);

        /// <summary>The most items the line's shelf holds.</summary>
        int MaxOnShelf(string lineId);

        /// <summary>Items on the line's shelf at <paramref name="totalSeconds"/> (clock seconds), 0..max.</summary>
        int CountAt(string lineId, double totalSeconds);

        /// <summary>True when the supply run came over the bar on <paramref name="day"/>.</summary>
        bool RunCameOn(int day);

        /// <summary>Days since the run last came (0 = today), or -1 when it has not come within the replay
        /// window. What a keeper's "the quad has not been over" line reads.</summary>
        int DaysSinceRun(int day);
    }

    /// <summary>The shelves plus the player's side: taking one item off a shelf.</summary>
    public interface IIslandShelves : IIslandShelvesView
    {
        /// <summary>Takes one item off the line's shelf at <paramref name="totalSeconds"/>; false when the shelf
        /// is empty. A committed stock change only: it neither charges money nor gives the item. Held for the
        /// session only until the island's memory is saved (plan slice 8).</summary>
        bool TryTakeOne(string lineId, double totalSeconds);
    }
}
