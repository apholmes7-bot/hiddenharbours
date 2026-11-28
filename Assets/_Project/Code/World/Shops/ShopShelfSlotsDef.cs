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

        /// <summary>The good goes into the shopping bag in his hand (the store's <see cref="ShopShelfSlotsDef.Bag"/>)
        /// and is put away into his storage by hand (buying at the shelf, slice 2). Off for goods that go
        /// straight to the supply locker or the tackle box, and for hand tools (<see cref="NeedsFreeHand"/>).</summary>
        public bool IntoBag;
    }

    /// <summary>
    /// <b>One section of a store's shelves holding several goods</b> (store sections; owner ask 2026-11-25:
    /// several foods on one shelf, the player picks which with the chooser). The goods are ordinary slots
    /// of the same Def, each with its own line, picture and sold-out card, so each still runs out on its
    /// own; the section only gathers their purchases into ONE place to reach, whose press opens the chooser
    /// with one row per good (name, price, how many are left). A slot named here sells only through its
    /// section, never on its own.
    /// </summary>
    [Serializable]
    public sealed class ShopShelfSection
    {
        /// <summary>Stable section id, <c>section.snake_case</c>, unique in its store. Append-only.</summary>
        public string Id;

        /// <summary>The words in the interaction bubble before the press (<c>Look at the groceries</c>).</summary>
        public string VerbLabel;

        /// <summary>Where the player reaches the section, in world metres from the building's ground point,
        /// measured like a slot's <see cref="ShopShelfSlot.Offset"/>.</summary>
        public Vector2 Offset;

        /// <summary>The slots whose goods this section sells, in the order the chooser lists them. Each must
        /// be a slot of this Def with a <see cref="ShopShelfSlot.ListingId"/>, and in no other section.</summary>
        public List<string> SlotIds = new List<string>();
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

        /// <summary>Rungs above the room picture for a slot's empty-shelf cover (under the goods). Added to
        /// the room renderer's order; the default is the shared rung for a layer on a room sheet.</summary>
        public int CoverSortingOrderAboveRoom = HiddenHarbours.Core.SortingBands.RoomSheetCoverRungs;

        /// <summary>Rungs above the room picture for the goods on a slot (over the cover, under the card).
        /// Added to the room renderer's order; the default is the shared rung for goods on a room sheet.</summary>
        public int GoodsSortingOrderAboveRoom = HiddenHarbours.Core.SortingBands.RoomSheetGoodsRungs;

        /// <summary>How near (m) the player must stand to a slot's good to see its price and buy one.</summary>
        [Min(0f)] public float ReachMeters = 1f;

        /// <summary>The bag this store's bagged goods (<see cref="ShopShelfSlot.IntoBag"/>) go into.</summary>
        public HiddenHarbours.Core.ShoppingBagDef Bag;

        /// <summary>The slots.</summary>
        public List<ShopShelfSlot> Slots = new List<ShopShelfSlot>();

        /// <summary>The sections: several slots' goods bought at one place through the chooser. Optional; a
        /// slot in no section sells on its own, as before.</summary>
        public List<ShopShelfSection> Sections = new List<ShopShelfSection>();

        /// <summary>The section that sells <paramref name="slotId"/>'s good, or null.</summary>
        public ShopShelfSection SectionOf(string slotId)
        {
            if (string.IsNullOrEmpty(slotId) || Sections == null) return null;
            for (int i = 0; i < Sections.Count; i++)
            {
                ShopShelfSection s = Sections[i];
                if (s?.SlotIds != null && s.SlotIds.Contains(slotId)) return s;
            }
            return null;
        }
    }
}
