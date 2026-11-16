using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>A place a carried tool is put away</b> — a rod rack now, a gear locker later. One asset per kind,
    /// stable append-only id (rule 2). The CAPABILITY side of a piece of storage gear: what it takes and how
    /// many. Its ECONOMIC side (price, the purchase record, the shelf) is a <c>GearOffer</c>, linked by
    /// <see cref="GearId"/> — the same split <see cref="ToolDef"/> keeps with the rod's offer.
    ///
    /// <para>Read by <see cref="StowageRack"/>, which asks <see cref="StowageRule.Check"/> with
    /// <see cref="Accepts"/> and <see cref="Capacity"/>; nothing here enforces anything itself.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Stowage", fileName = "Stowage")]
    public class StowageDef : ScriptableObject
    {
        [Tooltip("Stable id, type.snake_case, APPEND-ONLY (stowage.rod_rack).")]
        public string Id;

        [Tooltip("What the game calls it in a stow or refusal line ('rod rack').")]
        public string DisplayName;

        [Tooltip("The owned-gear id this storage corresponds to (gear.rod_rack): the link to the shop's " +
                 "GearOffer and the save's owned list. Empty is legal: a rack built into a wharf shed " +
                 "belongs to nobody and still holds rods.")]
        public string GearId;

        [Tooltip("What kind of thing it takes (Core.StowageAccepts flags). A rod rack is Rods only; a gear " +
                 "locker that also takes rods would be General | Rods. None refuses everything.")]
        public StowageAccepts Accepts = StowageAccepts.None;

        [Min(0)]
        [Tooltip("How many items it holds at once (a rod rack: how many rods). Owner-tunable. 0 holds " +
                 "nothing.")]
        public int Capacity;

        [Tooltip("One line for the stow message and later its label. Greybox copy.")]
        [TextArea(2, 4)]
        public string Flavor;
    }
}
