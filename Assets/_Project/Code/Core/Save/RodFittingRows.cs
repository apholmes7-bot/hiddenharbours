using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The one place a player's saved rod fittings are read and written</b> (save v27,
    /// <see cref="SaveData.RodFittings"/>). Ids only; an empty part id is the rod's own default part, and a rod
    /// with both parts at default has no row. Keeps no state of its own; static and null-safe (a null save reads
    /// every part as default and a write to it does nothing).
    /// </summary>
    public static class RodFittingRows
    {
        /// <summary>
        /// The line <paramref name="playerKey"/> has fitted to <paramref name="rodKey"/>, or "" for the rod's own
        /// line. When <paramref name="partExists"/> is given and says the saved id has no Def, "" is returned
        /// too; the row itself keeps the id as written.
        /// </summary>
        public static string LineOf(SaveData save, string playerKey, string rodKey, Func<string, bool> partExists = null)
            => Known(Find(save, playerKey, rodKey)?.LineId, partExists);

        /// <summary>The reel fitted, read the way <see cref="LineOf"/> reads the line.</summary>
        public static string ReelOf(SaveData save, string playerKey, string rodKey, Func<string, bool> partExists = null)
            => Known(Find(save, playerKey, rodKey)?.ReelId, partExists);

        /// <summary>
        /// Set what <paramref name="playerKey"/> has fitted to <paramref name="rodKey"/> (null or empty part =
        /// the rod's own). Both at default removes the row. Returns false, changing nothing, for a null save or
        /// an empty rod key.
        /// </summary>
        public static bool Fit(SaveData save, string playerKey, string rodKey, string lineId, string reelId)
        {
            if (save == null || string.IsNullOrEmpty(rodKey)) return false;
            save.RodFittings ??= new List<RodFittingDto>();
            lineId ??= "";
            reelId ??= "";
            RodFittingDto row = Find(save, playerKey, rodKey);
            if (lineId.Length == 0 && reelId.Length == 0)
            {
                if (row != null) save.RodFittings.Remove(row);
                return true;
            }
            if (row == null)
            {
                row = new RodFittingDto { PlayerKey = SavePlayerKeys.Normalise(playerKey), RodKey = rodKey };
                save.RodFittings.Add(row);
            }
            row.LineId = lineId;
            row.ReelId = reelId;
            return true;
        }

        /// <summary>The row for this player and rod, or null (an empty key means the local player).</summary>
        public static RodFittingDto Find(SaveData save, string playerKey, string rodKey)
        {
            if (save?.RodFittings == null || string.IsNullOrEmpty(rodKey)) return null;
            string key = SavePlayerKeys.Normalise(playerKey);
            for (int i = 0; i < save.RodFittings.Count; i++)
            {
                RodFittingDto row = save.RodFittings[i];
                if (row != null
                    && string.Equals(row.RodKey, rodKey, StringComparison.Ordinal)
                    && string.Equals(SavePlayerKeys.Normalise(row.PlayerKey), key, StringComparison.Ordinal))
                    return row;
            }
            return null;
        }

        private static string Known(string id, Func<string, bool> partExists)
        {
            if (string.IsNullOrEmpty(id)) return "";
            return partExists == null || partExists(id) ? id : "";
        }
    }
}
