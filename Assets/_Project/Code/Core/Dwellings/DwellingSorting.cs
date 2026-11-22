using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Where a parked dwelling sorts, and where the one standing inside it sorts (ADR 0060 section 5).</b>
    ///
    /// <para><b>The dwelling.</b> An upright world object: the decor band's Y-sort at its ground pivot, one
    /// rung behind, which is the hull rule (<see cref="HullSorting.OrderAt"/>) asked at the dwelling's
    /// ground pivot instead of a waterline. Held here under a dwelling name so a feature module asks Core,
    /// never <c>VehicleMeshDriver</c> or a boat type (rule 4). A dwelling never moves, so whoever writes its
    /// root group writes this once, at enable.</para>
    ///
    /// <para><b>The occupant.</b> He is never parented to a dwelling. While <c>BuildingInterior</c> says he
    /// is inside, his own sort writer takes the dwelling's order as an input and places him
    /// <see cref="OccupantRungsInFront"/> in front of it (<see cref="OccupantOrder"/>). That keeps one
    /// writer per order (ADR 0059 decision 4): decor and figures south of the dwelling still draw over him,
    /// and those north of it still draw under him.</para>
    ///
    /// <para>Pure and allocation-free.</para>
    /// </summary>
    public static class DwellingSorting
    {
        /// <summary>How many orders in front of his dwelling the occupant draws. One rung: the dwelling
        /// yields <see cref="SortingBands.HullYieldRungs"/> behind its own Y-sort so what stands in it draws
        /// over it, and he takes the rung back. Structural, not a balance value.</summary>
        public const int OccupantRungsInFront = 1;

        /// <summary>The order a dwelling whose ground pivot lies at <paramref name="groundPivot"/> sorts at.
        /// Only the pivot's Y matters; its heading and size do not move it.</summary>
        public static int OrderAt(Vector2 groundPivot) => HullSorting.OrderAt(groundPivot.y);

        /// <summary>The order the occupant of a dwelling sorting at <paramref name="dwellingOrder"/> draws
        /// at: <see cref="OccupantRungsInFront"/> in front of it, kept inside the decor band.</summary>
        public static int OccupantOrder(int dwellingOrder) =>
            Mathf.Clamp(dwellingOrder + OccupantRungsInFront, SortingBands.DecorFloor, SortingBands.DecorCeiling);
    }
}
