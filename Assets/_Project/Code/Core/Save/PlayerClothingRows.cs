using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The one place a player's saved clothes are read and written</b> (save v27,
    /// <see cref="SaveData.PlayerClothing"/>). Ids only: the wardrobe turns them into garments with its own
    /// content and hands ids back when it changes. Keeps no state of its own.
    ///
    /// <para>Static and null-safe throughout: a null save (an EditMode rig, before boot) reads as "owns
    /// nothing, wears nothing" and a write to it does nothing.</para>
    /// </summary>
    public static class PlayerClothingRows
    {
        private static readonly IReadOnlyList<string> None = Array.Empty<string>();

        /// <summary>The garments <paramref name="playerKey"/> owns, as saved (empty when the save holds no row
        /// for him). The list is the save's own: read it, do not change it.</summary>
        public static IReadOnlyList<string> Owned(SaveData save, string playerKey)
        {
            PlayerClothingDto row = Find(save, playerKey);
            return row?.OwnedClothing ?? None;
        }

        /// <summary>The garments <paramref name="playerKey"/> has on, as saved (empty when none). The list is
        /// the save's own: read it, do not change it.</summary>
        public static IReadOnlyList<string> Worn(SaveData save, string playerKey)
        {
            PlayerClothingDto row = Find(save, playerKey);
            return row?.WornClothing ?? None;
        }

        /// <summary>
        /// Replace what the save holds for <paramref name="playerKey"/> with <paramref name="owned"/> and
        /// <paramref name="worn"/> (garment ids). Empty ids and repeats are skipped; a worn id he does not own
        /// is added to what he owns, the load repair's own rule, so writing and loading give the same row.
        /// Returns false, changing nothing, for a null save.
        /// </summary>
        public static bool Write(SaveData save, string playerKey, IReadOnlyList<string> owned, IReadOnlyList<string> worn)
        {
            if (save == null) return false;
            save.PlayerClothing ??= new List<PlayerClothingDto>();
            PlayerClothingDto row = Find(save, playerKey);
            if (row == null)
            {
                row = new PlayerClothingDto { PlayerKey = SavePlayerKeys.Normalise(playerKey) };
                save.PlayerClothing.Add(row);
            }
            row.OwnedClothing ??= new List<string>();
            row.WornClothing ??= new List<string>();
            row.OwnedClothing.Clear();
            row.WornClothing.Clear();
            if (owned != null)
                for (int i = 0; i < owned.Count; i++) AddOnce(row.OwnedClothing, owned[i]);
            if (worn != null)
                for (int i = 0; i < worn.Count; i++)
                {
                    if (!AddOnce(row.WornClothing, worn[i])) continue;
                    AddOnce(row.OwnedClothing, worn[i]);
                }
            return true;
        }

        /// <summary>The row for <paramref name="playerKey"/>, or null (an empty key means the local player).</summary>
        public static PlayerClothingDto Find(SaveData save, string playerKey)
        {
            if (save?.PlayerClothing == null) return null;
            string key = SavePlayerKeys.Normalise(playerKey);
            for (int i = 0; i < save.PlayerClothing.Count; i++)
            {
                PlayerClothingDto row = save.PlayerClothing[i];
                if (row != null && string.Equals(SavePlayerKeys.Normalise(row.PlayerKey), key, StringComparison.Ordinal))
                    return row;
            }
            return null;
        }

        /// <summary>Add <paramref name="id"/> unless it is empty or already there. True when it was added.</summary>
        internal static bool AddOnce(List<string> list, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], id, StringComparison.Ordinal)) return false;
            list.Add(id);
            return true;
        }
    }
}
