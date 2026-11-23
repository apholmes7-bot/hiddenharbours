using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>What is in the paper shopping bag in his hand</b> (buying at the shelf, slice 2), as goods
    /// <b>set aside</b> from his supplies.
    ///
    /// <para><b>Why set aside rather than a second store.</b> Held things are not saved (a fish in hand is
    /// not either), and the save format may not change for the bag. So a bagged purchase goes into his
    /// supplies exactly as a counter purchase always did, and is saved at once; this list only says which of
    /// those supplies are still in the bag. <see cref="SupplyLocker"/> does not count them, so the stove,
    /// the soap and the recipes cannot use shopping he has not put away. Putting the bag away
    /// (<see cref="Release"/>) ends the set-aside. A reload has no bag, and the list is tied to the
    /// <see cref="SaveData"/> it was filled against, so the goods are then simply found put away: nothing
    /// lost, nothing paid twice.</para>
    ///
    /// <para>One bag at a time (one player). Static and null-safe; reads allocate nothing.</para>
    /// </summary>
    public static class ShoppingBagContents
    {
        private static SaveData _save;
        private static readonly List<string> _ids = new List<string>(8);
        private static readonly List<int> _counts = new List<int>(8);
        private static int _total;

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
        }
    }
}
