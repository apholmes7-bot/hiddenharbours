using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>What is in the paper shopping bag, and where the bag is</b> (buying at the shelf), as goods
    /// <b>set aside</b> from his supplies.
    ///
    /// <para><b>Why set aside rather than a second store.</b> A bagged purchase goes into his supplies
    /// exactly as a counter purchase always did; the bag only says which of those supplies are still in it.
    /// <see cref="SupplyLocker"/> does not count them, so the stove, the soap and the recipes cannot use
    /// shopping he has not put away. Putting the bag away (<see cref="Release"/>) ends the set-aside.</para>
    ///
    /// <para><b>The bag is saved</b> (save v26). There is no state here: every read and every change goes
    /// straight to the bag's row in <see cref="SaveData.ShoppingBags"/>, so whatever writes the save writes
    /// the bag as it stands, and a loaded game has its bag where it was left: in his hand, or set down in
    /// its region, at its position, on its storey, by the bed or not. A set-down bag always carries a
    /// region (the load repair drops one without); where none is known it is <see cref="UnknownRegionId"/>.
    /// If a row is damaged the repair drops it and the goods are simply in his supplies: nothing is lost.</para>
    ///
    /// <para><b>The bag can be set down</b> (the owner's ruling, 2026-11-23: "bag gets dropped by the bed,
    /// bag is droppable with items inside"). The goods stay set aside while it lies there.</para>
    ///
    /// <para>One bag at a time (one player): the first row of the list. The reads that take no save answer
    /// for the save last asked about. Static and null-safe; reads allocate nothing.</para>
    /// </summary>
    public static class ShoppingBagContents
    {
        /// <summary>The region written for a bag set down where the game knows no region (a scene played
        /// on its own). Such a bag lies wherever he is.</summary>
        public const string UnknownRegionId = "region.unknown";

        private static SaveData _save;          // the save last asked about
        private static ShoppingBagDef _noted;   // the kind last named or looked up
        private static Func<string, ShoppingBagDef> _bagSource;
        private static ShoppingBagBook _book;

        /// <summary>How a bag's kind is found from its saved id. Null (the default) reads the shipped kinds
        /// (<see cref="ShoppingBagBook.FromResources"/>, loaded once); a test hands in its own.</summary>
        public static Func<string, ShoppingBagDef> BagSource
        {
            get => _bagSource;
            set { _bagSource = value; _noted = null; }
        }

        private static ShoppingBagDto Row(SaveData save)
        {
            if (save == null) return null;
            _save = save;
            List<ShoppingBagDto> bags = save.ShoppingBags;
            return bags != null && bags.Count > 0 ? bags[0] : null;
        }

        private static int Total(ShoppingBagDto row)
        {
            if (row?.Items == null) return 0;
            int n = 0;
            for (int i = 0; i < row.Items.Count; i++)
                if (row.Items[i] != null && row.Items[i].Count > 0) n += row.Items[i].Count;
            return n;
        }

        // ---- where the bag is ----------------------------------------------------------------------

        /// <summary>The kind of bag the goods are in, or null with no bag (or a kind that no longer
        /// ships).</summary>
        public static ShoppingBagDef Bag
        {
            get
            {
                ShoppingBagDto row = Row(_save);
                if (Total(row) <= 0) return null;
                string id = row.BagDefId ?? "";
                if (_noted != null && string.Equals(_noted.Id ?? "", id, StringComparison.Ordinal)) return _noted;
                if (id.Length == 0) return null;
                if (_bagSource != null) _noted = _bagSource(id);
                else
                {
                    _book ??= ShoppingBagBook.FromResources();
                    _noted = _book.Find(id);
                }
                return _noted;
            }
        }

        /// <summary>True while any goods are set aside in the game last asked about.</summary>
        public static bool Any => Total(Row(_save)) > 0;

        /// <summary>True while the bag of <paramref name="save"/> holds goods and lies set down in the
        /// world rather than in his hand.</summary>
        public static bool IsSetDown(SaveData save)
        {
            ShoppingBagDto row = Row(save);
            return row != null && !row.Held && Total(row) > 0;
        }

        private static ShoppingBagDto SetDownRow()
        {
            ShoppingBagDto row = Row(_save);
            return row != null && !row.Held ? row : null;
        }

        /// <summary>The region the bag was set down in ("" in his hand).</summary>
        public static string SetDownRegionId => SetDownRow()?.Region ?? "";

        /// <summary>Where the bag was set down, world metres.</summary>
        public static Vector2 SetDownPosition
        {
            get
            {
                ShoppingBagDto row = SetDownRow();
                return row != null ? new Vector2(row.PosX, row.PosY) : default;
            }
        }

        /// <summary>The storey the bag was set down on: 0 the ground and everywhere outdoors, 1 the floor
        /// above.</summary>
        public static int SetDownLevel => SetDownRow()?.Level ?? 0;

        /// <summary>True when the bag was set down by a bed as he lay down, rather than by his own press.</summary>
        public static bool SetDownByTheBed => SetDownRow()?.ByTheBed ?? false;

        /// <summary>Names the kind of bag the goods of <paramref name="save"/> are in. Ignored while that
        /// game has nothing in a bag.</summary>
        public static void NoteBag(SaveData save, ShoppingBagDef bag)
        {
            ShoppingBagDto row = Row(save);
            if (Total(row) <= 0) return;
            row.BagDefId = bag != null ? bag.Id ?? "" : "";
            _noted = bag;
        }

        /// <summary>The bag of <paramref name="save"/> now lies at <paramref name="position"/> in
        /// <paramref name="regionId"/>, on the ground storey. False, and nothing changes, when that bag
        /// holds nothing.</summary>
        public static bool MarkSetDown(SaveData save, string regionId, Vector2 position, bool byTheBed)
            => MarkSetDown(save, regionId, position, 0, byTheBed);

        /// <summary>The bag of <paramref name="save"/> now lies at <paramref name="position"/> in
        /// <paramref name="regionId"/> on storey <paramref name="level"/>. False, and nothing changes, when
        /// that bag holds nothing.</summary>
        public static bool MarkSetDown(SaveData save, string regionId, Vector2 position, int level, bool byTheBed)
        {
            ShoppingBagDto row = Row(save);
            if (Total(row) <= 0) return false;
            row.Held = false;
            row.Region = string.IsNullOrEmpty(regionId) ? UnknownRegionId : regionId;
            row.PosX = position.x;
            row.PosY = position.y;
            row.Level = level > 0 ? level : 0;
            row.HullId = "";
            row.ByTheBed = byTheBed;
            return true;
        }

        /// <summary>The bag is in his hand again.</summary>
        public static void MarkHeld()
        {
            ShoppingBagDto row = Row(_save);
            if (row != null) Hold(row);
        }

        private static void Hold(ShoppingBagDto row)
        {
            row.Held = true;
            row.Region = "";
            row.PosX = 0f;
            row.PosY = 0f;
            row.Level = 0;
            row.HullId = "";
            row.ByTheBed = false;
        }

        /// <summary>Whether the set-down bag of <paramref name="save"/> lies in <paramref name="regionId"/>:
        /// a bag left in one region is not seen or picked up from another. A bag set down where no region
        /// was known (<see cref="UnknownRegionId"/>) lies wherever he is.</summary>
        public static bool LiesIn(SaveData save, string regionId)
        {
            if (!IsSetDown(save)) return false;
            string region = Row(save).Region ?? "";
            return region.Length == 0
                   || string.Equals(region, UnknownRegionId, StringComparison.Ordinal)
                   || string.Equals(region, regionId ?? "", StringComparison.Ordinal);
        }

        /// <summary>True when goods are set aside against <paramref name="save"/>.</summary>
        public static bool HoldsAny(SaveData save) => Total(Row(save)) > 0;

        /// <summary>How many goods are in the bag of <paramref name="save"/>.</summary>
        public static int Total(SaveData save) => Total(Row(save));

        /// <summary>How many of <paramref name="supplyId"/> are in the bag of <paramref name="save"/>.</summary>
        public static int InBag(SaveData save, string supplyId)
        {
            if (string.IsNullOrEmpty(supplyId)) return 0;
            ShoppingBagDto row = Row(save);
            if (row?.Items == null) return 0;
            for (int i = 0; i < row.Items.Count; i++)
            {
                BagItemDto item = row.Items[i];
                if (item != null && string.Equals(item.SupplyId, supplyId, StringComparison.Ordinal))
                    return item.Count > 0 ? item.Count : 0;
            }
            return 0;
        }

        /// <summary>The number of different goods in the bag of <paramref name="save"/>.</summary>
        public static int LineCount(SaveData save) => Row(save)?.Items?.Count ?? 0;

        /// <summary>The good on line <paramref name="i"/> (see <see cref="LineCount"/>).</summary>
        public static string IdAt(int i)
        {
            List<BagItemDto> items = Row(_save)?.Items;
            return items != null && i >= 0 && i < items.Count ? items[i]?.SupplyId : null;
        }

        /// <summary>How many of the good on line <paramref name="i"/>.</summary>
        public static int CountAt(int i)
        {
            List<BagItemDto> items = Row(_save)?.Items;
            return items != null && i >= 0 && i < items.Count && items[i] != null ? items[i].Count : 0;
        }

        /// <summary>
        /// Sets one <paramref name="supplyId"/> aside as in the bag, if the bag (of <paramref name="capacity"/>)
        /// has room. The good must already be in his supplies (the purchase put it there); one that is not is
        /// refused, so the bag can never hold more than he owns. The first good starts a bag in his hand.
        /// </summary>
        public static bool TryPut(SaveData save, string supplyId, int capacity)
        {
            if (save == null || string.IsNullOrEmpty(supplyId)) return false;
            ShoppingBagDto row = Row(save);
            if (Total(row) >= capacity) return false;
            int bagged = InBag(save, supplyId);
            if (SupplyLocker.Owned(save, supplyId) <= bagged) return false;

            if (row == null)
            {
                row = new ShoppingBagDto { Held = true };
                save.ShoppingBags ??= new List<ShoppingBagDto>();
                save.ShoppingBags.Insert(0, row);
            }
            else if (Total(row) == 0) Hold(row);   // an emptied row starts again as a bag in his hand
            row.Items ??= new List<BagItemDto>();
            for (int i = 0; i < row.Items.Count; i++)
            {
                BagItemDto item = row.Items[i];
                if (item == null || !string.Equals(item.SupplyId, supplyId, StringComparison.Ordinal)) continue;
                item.Count = bagged + 1;
                return true;
            }
            row.Items.Add(new BagItemDto { SupplyId = supplyId, Count = 1 });
            return true;
        }

        /// <summary>Takes one <paramref name="supplyId"/> out of the bag (it was used up some other way).</summary>
        public static bool TryTakeOut(SaveData save, string supplyId)
        {
            ShoppingBagDto row = Row(save);
            if (row?.Items == null) return false;
            for (int i = 0; i < row.Items.Count; i++)
            {
                BagItemDto item = row.Items[i];
                if (item == null || item.Count < 1
                    || !string.Equals(item.SupplyId, supplyId, StringComparison.Ordinal)) continue;
                if (item.Count > 1) item.Count--;
                else row.Items.RemoveAt(i);
                if (Total(row) == 0) save.ShoppingBags.Remove(row);   // an empty bag is no bag, and in no place
                return true;
            }
            return false;
        }

        /// <summary>Empties the bag into his supplies (the goods were already counted there): from now on
        /// they can be eaten and used. Returns how many goods were put away.</summary>
        public static int Release(SaveData save)
        {
            ShoppingBagDto row = Row(save);
            if (row == null) return 0;
            int n = Total(row);
            save.ShoppingBags.Remove(row);
            return n;
        }

        /// <summary>Forgets which game was last asked about and which kind of bag was last named (the
        /// services were torn down, a test). The bag itself stays with its save.</summary>
        public static void Clear()
        {
            _save = null;
            _noted = null;
        }
    }
}
