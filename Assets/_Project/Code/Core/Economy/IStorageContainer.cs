namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One of the player's storage containers, by a stable id</b>: the boat's hold, the freezer at
    /// Ginny's, a rod rack or gear locker at home. Listed in <see cref="PlayerStorage"/>, whose readers find a
    /// container by <see cref="StorageId"/> and filter for what they need (<c>as IHold</c> for catch,
    /// <c>as IHoldRemove</c> to take one item out) without naming the class (rule 4).
    ///
    /// <para><b>The id is the save key.</b> A container whose contents persist keeps them under this id
    /// (<see cref="StoredContainers"/>, save v25), so it is <c>storage.snake_case</c>
    /// (<see cref="StorageIds"/>), authored, unique and append-only: renaming one orphans what the player
    /// left in it. A content-validation test keeps the authored ids unique.</para>
    /// FLAG lead-architect: new Core contract (stable storage ids, PlayerStorage order).
    /// </summary>
    public interface IStorageContainer
    {
        /// <summary>The stable <c>storage.*</c> id. Empty for an unnamed container (listed, never saved).</summary>
        string StorageId { get; }

        /// <summary>Where it sorts in <see cref="PlayerStorage"/>: lower first, then by ordinal id
        /// (<see cref="PlayerStorage.PriorityBoatHold"/> and its siblings).</summary>
        int StoragePriority { get; }
    }
}
