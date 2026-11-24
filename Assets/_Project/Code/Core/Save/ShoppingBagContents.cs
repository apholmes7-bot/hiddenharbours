using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>What is in the paper shopping bag, and where the bag is</b> (buying at the shelf, slice 2 and 2b),
    /// as goods <b>set aside</b> from his supplies.
    ///
    /// <para><b>Why set aside rather than a second store.</b> The save format does not change for the bag
    /// in this slice. So a bagged purchase goes into his supplies exactly as a counter purchase always did,
    /// and is saved at once; this list only says which of those supplies are still in the bag.
    /// <see cref="SupplyLocker"/> does not count them, so the stove, the soap and the recipes cannot use
    /// shopping he has not put away. Putting the bag away (<see cref="Release"/>) ends the set-aside.</para>
    ///
    /// <para><b>The bag can be set down</b> (the owner's ruling, 2026-11-23: "bag gets dropped by the bed,
    /// bag is droppable with items inside"). The goods stay set aside while it lies there;
    /// <see cref="IsSetDown"/>, <see cref="SetDownRegionId"/> and <see cref="SetDownPosition"/> say where.
    /// </para>
    ///
    /// <para><b>The bag and the save, until the save holds it.</b> This list lives for the running game
    /// and is tied to the <see cref="SaveData"/> it was filled against. Leaving for the title and coming
    /// back keeps that game in memory, so the bag is still his, and it is put back in his hand. A new game
    /// is another <see cref="SaveData"/>, so the bag of the game that was left is forgotten. A full quit
    /// forgets the list: the goods are his either way (they were saved when bought), so after a restart
    /// they are in his supplies and there is no bag. Nothing is lost and nothing is paid twice. Saving the
    /// set-down bag is the next save change's job.</para>
    ///
    /// <para>One bag at a time (one player). Static and null-safe; reads allocate nothing.</para>
    /// </summary>
    public static class ShoppingBagContents
    {
        private static SaveData _save;
        private static readonly List<string> _ids = new List<string>(8);
        private static readonly List<int> _counts = new List<int>(8);
        private static int _total;

        // ---- where the bag is ----------------------------------------------------------------------

        /// <summary>The kind of bag the goods are in, or null with no bag. Kept so the bag can be put back
        /// in his hand when its picture was lost (the scene it stood in was unloaded).</summary>
        public static ShoppingBagDef Bag { get; private set; }

        /// <summary>True while any goods are set aside, whichever game they belong to. The cheap test a
        /// per-frame reader asks first.</summary>
        public static bool Any => _total > 0;

        /// <summary>True while the bag of <paramref name="save"/> holds goods and lies set down in the
        /// world rather than in his hand.</summary>
        public static bool IsSetDown(SaveData save) => _setDown && Total(save) > 0;

        /// <summary>The region the bag was set down in ("" in his hand, or where no region is known).</summary>
        public static string SetDownRegionId { get; private set; } = "";

        /// <summary>Where the bag was set down, world metres.</summary>
        public static Vector2 SetDownPosition { get; private set; }

        /// <summary>True when the bag was set down by a bed as he lay down, rather than by his own press.</summary>
        public static bool SetDownByTheBed { get; private set; }

        private static bool _setDown;

        /// <summary>Names the kind of bag the goods of <paramref name="save"/> are in. Ignored for a game
        /// the bag does not belong to.</summary>
        public static void NoteBag(SaveData save, ShoppingBagDef bag)
        {
            if (save != null && ReferenceEquals(save, _save)) Bag = bag;
        }

        /// <summary>The bag of <paramref name="save"/> now lies at <paramref name="position"/> in
        /// <paramref name="regionId"/>. False, and nothing changes, when that bag holds nothing.</summary>
        public static bool MarkSetDown(SaveData save, string regionId, Vector2 position, bool byTheBed)
        {
            if (Total(save) <= 0) return false;
            _setDown = true;
            SetDownRegionId = regionId ?? "";
            SetDownPosition = position;
            SetDownByTheBed = byTheBed;
            return true;
        }

        /// <summary>The bag is in his hand again.</summary>
        public static void MarkHeld()
        {
            _setDown = false;
            SetDownRegionId = "";
            SetDownPosition = default;
            SetDownByTheBed = false;
        }

        /// <summary>Whether the set-down bag of <paramref name="save"/> lies in <paramref name="regionId"/>:
        /// a bag left in one region is not seen or picked up from another. A bag set down where no region
        /// was known lies wherever he is.</summary>
        public static bool LiesIn(SaveData save, string regionId)
        {
            if (!IsSetDown(save)) return false;
            return SetDownRegionId.Length == 0
                   || string.Equals(SetDownRegionId, regionId ?? "", StringComparison.Ordinal);
        }

        /// <summary>True when goods are set aside against <paramref name="save"/>.</summary>
        public static bool HoldsAny(SaveData save) => Total(save) > 0;

        /// <summary>How many goods are in the bag, against <paramref name="save"/> (0 for any other save).</summary>
        public static int Total(SaveData save) => save != null && ReferenceEquals(save, _save) ? _total : 0;

        /// <summary>How many of <paramref name="supplyId"/> are in the bag, against <paramref name="save"/>.</summary>
        public static int InBag(SaveData save, string supplyId)
        {
            if (save == null || !ReferenceEquals(save, _save) || string.IsNullOrEmpty(supplyId)) return 0;
            for (int i = 0; i < _ids.Count; i++)
                if (string.Equals(_ids[i], supplyId, StringComparison.Ordinal)) return _counts[i];
            return 0;
        }

        /// <summary>The number of different goods in the bag, against <paramref name="save"/>.</summary>
        public static int LineCount(SaveData save) => save != null && ReferenceEquals(save, _save) ? _ids.Count : 0;

        /// <summary>The good on line <paramref name="i"/> (see <see cref="LineCount"/>).</summary>
        public static string IdAt(int i) => i >= 0 && i < _ids.Count ? _ids[i] : null;

        /// <summary>How many of the good on line <paramref name="i"/>.</summary>
        public static int CountAt(int i) => i >= 0 && i < _counts.Count ? _counts[i] : 0;

        /// <summary>
        /// Sets one <paramref name="supplyId"/> aside as in the bag, if the bag (of <paramref name="capacity"/>)
        /// has room. The good must already be in his supplies (the purchase put it there); one that is not is
        /// refused, so the bag can never hold more than he owns. A bag filled against another save is emptied
        /// first: it belonged to a game that is no longer loaded.
        /// </summary>
        public static bool TryPut(SaveData save, string supplyId, int capacity)
        {
            if (save == null || string.IsNullOrEmpty(supplyId)) return false;
            if (!ReferenceEquals(save, _save)) { Clear(); _save = save; }
            if (_total >= capacity) return false;
            int at = -1;
            for (int i = 0; i < _ids.Count; i++)
                if (string.Equals(_ids[i], supplyId, StringComparison.Ordinal)) { at = i; break; }
            int bagged = at >= 0 ? _counts[at] : 0;
            if (SupplyLocker.Owned(save, supplyId) <= bagged) return false;
            if (at >= 0) _counts[at] = bagged + 1;
            else { _ids.Add(supplyId); _counts.Add(1); }
            _total++;
            return true;
        }

        /// <summary>Takes one <paramref name="supplyId"/> out of the bag (it was used up some other way).</summary>
        public static bool TryTakeOut(SaveData save, string supplyId)
        {
            if (save == null || !ReferenceEquals(save, _save)) return false;
            for (int i = 0; i < _ids.Count; i++)
            {
                if (!string.Equals(_ids[i], supplyId, StringComparison.Ordinal)) continue;
                if (_counts[i] > 1) _counts[i]--;
                else { _ids.RemoveAt(i); _counts.RemoveAt(i); }
                _total--;
                if (_total == 0) Clear();   // an empty bag is no bag: it is in no place either
                return true;
            }
            return false;
        }

        /// <summary>Empties the bag into his supplies (the goods were already counted there): from now on
        /// they can be eaten and used. Returns how many goods were put away.</summary>
        public static int Release(SaveData save)
        {
            int n = Total(save);
            Clear();
            return n;
        }

        /// <summary>Forgets the bag entirely (a new session, a test).</summary>
        public static void Clear()
        {
            _save = null;
            _ids.Clear();
            _counts.Clear();
            _total = 0;
            Bag = null;
            MarkHeld();
        }
    }
}
