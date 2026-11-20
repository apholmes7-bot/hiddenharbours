using UnityEngine;
using UnityEngine.Rendering;

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
    /// position, which is also <see cref="HullFootprint.Center"/>, moved up-screen by the tide's screen
    /// rise (<see cref="SortPoint"/>), so she sorts where her picture's waterline is drawn. It does not
    /// move with her heading, her length or her heel, and it does not ride the swell: waves move only her
    /// visual child, so her sort point stays put and she does not flicker against a rock as she rises and
    /// falls.</para>
    ///
    /// <para><b>Why one rung behind.</b> Something standing on her root (the player aboard, a figure on her
    /// deck) would land on exactly her order, and a tie between a sorting group and a sprite is broken by
    /// nothing we control. The hull yields the rung so what stands on her draws over her. A quarter metre is
    /// less than any hull's half-length, so nothing beside her can tell.</para>
    ///
    /// <para>Pure and allocation-free: one rounding and a clamp. The boat's root <c>SortingGroup</c> takes
    /// this order every <c>LateUpdate</c> (Boats <c>BoatSortingGroup</c>, ADR 0059 step 3), so the whole boat,
    /// with everything aboard her, sorts as one object; what is aboard keeps its own local orders inside
    /// the group.</para>
    /// </summary>
    public static class HullSorting
    {
        /// <summary>
        /// The order a hull whose waterline pivot lies at <paramref name="waterlinePivot"/> sorts at, with a
        /// situational rule (<paramref name="input"/>, such as the St Peters pier) asked first. The rule is an
        /// input to the boat's one writer, never a second writer to her group (ADR 0059 decision 4). A null
        /// input, or one that does not apply here, leaves <see cref="OrderAt"/>.
        /// </summary>
        public static int OrderFor(Vector2 waterlinePivot, IHullSortingInput input)
        {
            if (input != null && input.TryOrderAt(waterlinePivot, out int decided)) return decided;
            return OrderAt(waterlinePivot.y);
        }

        /// <summary>
        /// Her sort point: the root (<paramref name="root"/>) moved up-screen by the tide alone
        /// (<paramref name="tideScreenRise"/>, Core <see cref="IHullTideRide.ScreenRiseNow"/>), which is
        /// where her picture's waterline is drawn without any wave heave or roll. The tide is deterministic
        /// from <c>(worldSeed, gameTime)</c> and slow, so the key moves a rung per quarter metre of rise and
        /// never flickers with the swell. A non-finite rise is no rise.
        /// </summary>
        public static Vector2 SortPoint(Vector2 root, float tideScreenRise)
        {
            if (float.IsNaN(tideScreenRise) || float.IsInfinity(tideScreenRise)) return root;
            return new Vector2(root.x, root.y + tideScreenRise);
        }

        /// <summary>
        /// Lets <paramref name="go"/>'s renderers escape a boat's sorting group (ADR 0059 decision 2): a
        /// <c>SortingGroup</c> that sorts at the root, at <paramref name="order"/>. For what is parented to the
        /// boat but must clear all decor wherever she lies: the mooring rope, the anchor rode, the trap-haul
        /// rope. Idempotent; returns the group.
        /// </summary>
        public static SortingGroup EscapeTheBoat(GameObject go, int order)
        {
            if (go == null) return null;
            var group = go.GetComponent<SortingGroup>();
            if (group == null) group = go.AddComponent<SortingGroup>();
            group.sortAtRoot = true;
            group.sortingOrder = order;
            return group;
        }

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

    /// <summary>
    /// A situational rule that decides where a particular boat sorts in a particular place (ADR 0059
    /// decision 4), such as "a hull north of the St Peters pier drops behind its deck". It sits on the boat
    /// root beside her sorting group and is ASKED by the boat's one writer each frame; it never writes the
    /// group itself. Allocation-free by contract: a rectangle test and an int.
    /// </summary>
    public interface IHullSortingInput
    {
        /// <summary>True, with the order, when this rule decides the hull's order with her waterline pivot at
        /// <paramref name="waterlinePivot"/>; false to leave the decor band's rule.</summary>
        bool TryOrderAt(Vector2 waterlinePivot, out int order);
    }
}
