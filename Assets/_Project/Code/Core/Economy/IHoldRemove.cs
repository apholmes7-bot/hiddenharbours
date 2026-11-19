namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Take one item out of a hold, and only that one</b>. A reader that takes a single catch (a gift) asks
    /// for this facet with <c>as IHoldRemove</c> instead of emptying the hold and adding the rest back: a
    /// rebuild re-modes and re-checks every item it re-adds, and on a persisted hold it rewrites the save
    /// row for nothing. Implemented by the holds that already keep a list (the boat's hold, the freezer at
    /// Ginny's, the clam bucket).
    /// FLAG lead-architect: new Core contract (surgical remove).
    /// </summary>
    public interface IHoldRemove
    {
        /// <summary>Remove the item at <paramref name="index"/> of <see cref="IHold.Items"/>, leaving every
        /// other item exactly as it was. False, with nothing changed, when the index is out of range.</summary>
        bool TryRemoveAt(int index);
    }
}
