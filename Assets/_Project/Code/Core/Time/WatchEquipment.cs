using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Starting watches and the best owned watch share the existing owned-gear wallet.
    /// Wearing is derived each time; it has no saved field or migration.</summary>
    public static class WatchEquipment
    {
        private static WatchDef[] _catalog;

        /// <summary>Called by the live watch presenter after the clock is released. Grants authored
        /// starting equipment into the current blob before selecting, for new and resumed games alike.
        /// The normal save flow persists OwnedGear; this does not write a save file.</summary>
        public static WatchDef GetWorn(SaveData save)
        {
            if (save == null) return null;
            _catalog ??= Resources.LoadAll<WatchDef>("Watches");
            return GetWorn(save, _catalog);
        }

        /// <summary>Explicit catalog overload for deterministic logic tests. The starting grant is
        /// additive and idempotent, preserving legacy gear ids and everything already owned.</summary>
        public static WatchDef GetWorn(SaveData save, IReadOnlyList<WatchDef> catalog)
        {
            if (save == null || catalog == null) return null;
            for (int i = 0; i < catalog.Count; i++)
            {
                WatchDef def = catalog[i];
                if (def == null || !def.StartsOwned || string.IsNullOrEmpty(def.Id)) continue;
                save.OwnedGear ??= new List<string>();
                if (!save.OwnedGear.Contains(def.Id)) save.OwnedGear.Add(def.Id);
            }
            return SelectOwned(save, catalog);
        }

        /// <summary>Read-only selection by highest tier, then lowest ordinal id, independent of
        /// catalog and acquisition order. An authored legacy id counts as owning the same watch.</summary>
        public static WatchDef SelectOwned(SaveData save, IReadOnlyList<WatchDef> catalog)
        {
            if (save?.OwnedGear == null || catalog == null) return null;
            WatchDef best = null;
            for (int i = 0; i < catalog.Count; i++)
            {
                WatchDef def = catalog[i];
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                bool owned = save.OwnedGear.Contains(def.Id)
                    || (!string.IsNullOrEmpty(def.LegacyGearId) && save.OwnedGear.Contains(def.LegacyGearId));
                if (!owned) continue;
                if (best == null || def.Tier > best.Tier
                    || (def.Tier == best.Tier && string.CompareOrdinal(def.Id, best.Id) < 0))
                    best = def;
            }
            return best;
        }
    }
}
