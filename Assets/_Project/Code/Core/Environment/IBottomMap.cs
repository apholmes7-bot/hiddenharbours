using UnityEngine;

namespace HiddenHarbours.Core
{
    public enum BottomType { Unknown = 0, Rock, Mud, Sand }

    /// <summary>The single gameplay substrate query. Inject the same instance into every reader.
    /// Geology is fixed by seed and position, independent of tide, season and query order.</summary>
    public interface IBottomMap
    {
        BottomType BottomAt(Vector2 worldPosition);
    }
}
