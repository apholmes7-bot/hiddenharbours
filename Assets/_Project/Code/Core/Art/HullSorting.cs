using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Where a boat sorts among the scenery (ADR 0059): by where she lies, like every other upright thing a
    /// person can walk round. Her order is the decor band's own Y-sort (ADR 0032, the rule every
    /// <c>YSortSprite</c> follows) taken at her <b>waterline pivot</b>, less
    /// <see cref="SortingBands.HullYieldRungs"/>. That is the mesh vehicle's rule
    /// (<c>VehicleMeshDriver.OrderAt</c>), held here once so boats, the ambient fleet, NPC boats, moored
    /// boats and the beached wreck all ask the same question.
    ///
    /// <para><b>The sort point is the waterline pivot amidships</b> (owner, 2026-11-17): the boat root's
    /// position, which is also <see cref="HullFootprint.Center"/>. It does not move with her heading, her
    /// length or her heel, and it does not ride the swell: the sea lifts only her visual child, so her sort
    /// point stays put and she does not flicker against a rock as she rises and falls.</para>
    ///
    /// <para><b>Why one rung behind.</b> Something standing on her root (the player aboard, a figure on her
    /// deck) would land on exactly her order, and a tie between a sorting group and a sprite is broken by
    /// nothing we control. The hull yields the rung so what stands on her draws over her. A quarter metre is
    /// less than any hull's half-length, so nothing beside her can tell.</para>
    ///
    /// <para>Pure and allocation-free: one rounding and a clamp. Not yet wired to any renderer; the boat's
    /// sorting group adopts it in a later step of the ADR 0059 rollout.</para>
    /// </summary>
    public static class HullSorting
    {
        /// <summary>
        /// The order a hull whose waterline pivot lies at world <paramref name="waterlinePivotY"/> sorts at:
        /// the decor band's Y-sort there, <see cref="SortingBands.HullYieldRungs"/> behind, kept inside the
        /// band. A non-finite Y (a hull that has not been placed) sorts at the middle of the band.
        /// </summary>
        public static int OrderAt(float waterlinePivotY)
        {
            float y = float.IsNaN(waterlinePivotY) || float.IsInfinity(waterlinePivotY) ? 0f : waterlinePivotY;
            int order = Mathf.RoundToInt(SortingBands.DecorBase - y * SortingBands.OrdersPerMetre)
                        - SortingBands.HullYieldRungs;
            return Mathf.Clamp(order, SortingBands.DecorFloor, SortingBands.DecorCeiling);
        }

        /// <summary>Her sort point's world Y: the waterline pivot amidships, which is her footprint's
        /// centre. Heading, length and beam do not move it.</summary>
        public static float SortPointY(in HullFootprint footprint) => footprint.Center.y;

        /// <summary>The order a hull lying on <paramref name="footprint"/> sorts at.</summary>
        public static int OrderFor(in HullFootprint footprint) => OrderAt(SortPointY(footprint));
    }
}
