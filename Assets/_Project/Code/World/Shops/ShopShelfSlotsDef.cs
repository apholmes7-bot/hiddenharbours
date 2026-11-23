using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>One place on a store's shelves where a product normally sits.</summary>
    [Serializable]
    public sealed class ShopShelfSlot
    {
        /// <summary>Stable slot id, <c>slot.snake_case</c>, unique in its store. Append-only.</summary>
        public string Id;

        /// <summary>The shelf line whose product sits here (<c>line.snake_case</c>, a line of the store's
        /// island shelves). When that line holds nothing, a sold-out card stands here instead.</summary>
        public string LineId;

        /// <summary>Where the card stands, in world metres from the building's ground point, measured on the
        /// baked room picture at the facing the store is placed at.</summary>
        public Vector2 Offset;

        /// <summary>The counter listing (<c>supply.bread</c>) the player takes off the shelf here and pays for
        /// at the shelf (buying at the shelf, slice 1). Empty for a slot that only carries a card or a cover.
        /// It must be one of this slot's line's counter listings.</summary>
        public string ListingId = "";

        /// <summary>The shelf drawn EMPTY at this place, over the room picture, so the painted goods there do
        /// not show once the real ones are gone. Optional.</summary>
        public Sprite Cover;

        /// <summary>The goods themselves, drawn at this place while the line has stock. Optional.</summary>
        public Sprite Goods;

        /// <summary>No card here when the line is empty (another slot on the same line carries it).</summary>
        public bool HideSoldOutCard;

        /// <summary>The good is carried off in a hand, so a purchase needs a free one. Off for goods that go
        /// straight to the supply locker or the tackle box.</summary>
        public bool NeedsFreeHand;
    }

    /// <summary>
    /// <b>Where a store's products sit on its shelves</b> (store sold-out slice; the owner's ruling: a shelf
    /// shows a sold-out sign where the product normally sits). One asset per store, under
    /// <c>Resources/ShopShelfSlots</c>, read by <see cref="SoldOutSignsDirector"/>. Nothing here is state.
    /// </summary>
    [CreateAssetMenu(menuName = "World/Shop Shelf Slots", fileName = "ShopShelfSlots")]
    public sealed class ShopShelfSlotsDef : ScriptableObject
    {
        /// <summary>Where these Defs are loaded from (under a Resources folder).</summary>
        public const string ResourcesFolder = "ShopShelfSlots";

        /// <summary>Stable id, <c>shelfslots.snake_case</c>. Append-only.</summary>
        public string Id;

        /// <summary>The shop (<c>shop.snake_case</c>) whose island shelves decide the cards.</summary>
        public string ShopId;

        /// <summary>The name of the building's GameObject in its region scene (the one carrying its
        /// <see cref="BuildingInterior"/>). The cards show only while the player is inside, downstairs.</summary>
        public string BuildingName;

        /// <summary>The card drawn on an empty slot.</summary>
        public Sprite SoldOutSign;

        /// <summary>Sorting order above the room picture, so the card stands on the shelf in front of it.</summary>
        public int SortingOrderAboveRoom = 1;

        /// <summary>Sorting order above the room picture for a slot's empty-shelf cover (under the goods).</summary>
        public int CoverSortingOrderAboveRoom = 1;

        /// <summary>Sorting order above the room picture for the goods on a slot (under the card).</summary>
        public int GoodsSortingOrderAboveRoom = 2;

        /// <summary>How near (m) the player must stand to a slot's good to see its price and buy one.</summary>
        [Min(0f)] public float ReachMeters = 1f;

        /// <summary>The slots.</summary>
        public List<ShopShelfSlot> Slots = new List<ShopShelfSlot>();
    }
}
