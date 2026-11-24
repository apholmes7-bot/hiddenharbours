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

        /// <summary>The slots.</summary>
        public List<ShopShelfSlot> Slots = new List<ShopShelfSlot>();
    }
}
