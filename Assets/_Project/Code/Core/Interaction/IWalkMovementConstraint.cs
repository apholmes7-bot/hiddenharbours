using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>A physical restraint on an on-foot step, supplied without a feature-module dependency.</summary>
    public interface IWalkMovementConstraint
    {
        Vector2 ConstrainWalk(Vector2 destination);
    }

    /// <summary>The on-foot motor accepts a restraint; releasing it restores ordinary walking.</summary>
    public interface IConstrainedWalker
    {
        IWalkMovementConstraint MovementConstraint { get; set; }
    }
}
