namespace HiddenHarbours.Core
{
    /// <summary>Optional independent leaf poses addressed by the imported door id.</summary>
    public interface IHullDoorLeaves
    {
        bool HasDoorLeaf(string doorId);
        bool DoorLeafShownOpenFor(string doorId);
        void ShowDoorLeaf(string doorId, bool open);
    }
}
