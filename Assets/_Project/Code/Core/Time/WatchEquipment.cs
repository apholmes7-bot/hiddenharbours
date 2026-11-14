using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Wearing is derived from authored starting equipment and the existing owned-gear
    /// wallet. Reading it never changes the save; it has no saved field or migration.</summary>
    public static class WatchEquipment
    {
        private static WatchDef[] _catalog;

        /// <summary>Called by the live watch presenter after the clock is released. Starting
        /// equipment is available to new and resumed games without adding ids to the save.</summary>
        public static WatchDef GetWorn(SaveData save)
        {
            if (save == null) return null;
            _catalog ??= Resources.LoadAll<WatchDef>("Watches");
            return GetWorn(save, _catalog);
        }

        /// <summary>Explicit catalog overload for deterministic, read-only selection.</summary>
        public static WatchDef GetWorn(SaveData save, IReadOnlyList<WatchDef> catalog)
        {
            return SelectOwned(save, catalog);
        }

        /// <summary>Read-only selection by highest tier, then lowest ordinal id, independent of
        /// catalog and acquisition order. Authored starting equipment is implicitly owned; an
        /// authored legacy id counts as owning the same watch. Neither rewrites the wallet.</summary>
        public static WatchDef SelectOwned(SaveData save, IReadOnlyList<WatchDef> catalog)
        {
            if (save == null || catalog == null) return null;
            WatchDef best = null;
            for (int i = 0; i < catalog.Count; i++)
            {
                WatchDef def = catalog[i];
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                bool owned = def.StartsOwned || (save.OwnedGear != null
                    && (save.OwnedGear.Contains(def.Id)
                        || (!string.IsNullOrEmpty(def.LegacyGearId) && save.OwnedGear.Contains(def.LegacyGearId))));
                if (!owned) continue;
                if (best == null || def.Tier > best.Tier
                    || (def.Tier == best.Tier && string.CompareOrdinal(def.Id, best.Id) < 0))
                    best = def;
            }
            return best;
        }
    }
}
