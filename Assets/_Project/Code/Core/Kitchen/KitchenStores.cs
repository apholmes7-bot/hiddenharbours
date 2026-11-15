using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The food stores on the save</b> (kitchens slice 1, save v21): find or open a store by its key,
    /// put food in, take it out, pack ice from the supply locker, and the slow tick that notices ice running
    /// out and a fridge's power changing. Every change settles through <see cref="KitchenStorageModel"/>
    /// and says so on the <see cref="EventBus"/>. Location-agnostic: a boat, a home, a shop or a
    /// player-built building passes its own key.
    /// </summary>
    public static class KitchenStores
    {
        /// <summary>The save's store list (made on first use).</summary>
        public static List<KitchenStorageStateDto> All(SaveData save)
            => save == null ? null : save.KitchenStores ??= new List<KitchenStorageStateDto>();

        /// <summary>The store with <paramref name="key"/>, or null.</summary>
        public static KitchenStorageStateDto Find(SaveData save, string key)
        {
            List<KitchenStorageStateDto> all = save?.KitchenStores;
            if (all == null || string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && string.Equals(all[i].Key, key, StringComparison.Ordinal)) return all[i];
            return null;
        }

        /// <summary>The store with <paramref name="key"/>, made empty (no ice, powered) when it is not
        /// there yet. A store found under another def id keeps its contents and takes the new id: a
        /// rebuilt galley never eats the food.</summary>
        public static KitchenStorageStateDto Open(SaveData save, string key, KitchenStorageDef def)
        {
            if (save == null || string.IsNullOrEmpty(key) || def == null) return null;
            KitchenStorageStateDto store = Find(save, key);
            if (store == null)
            {
                store = new KitchenStorageStateDto { Key = key, StorageDefId = def.Id };
                All(save).Add(store);
            }
            else if (!string.Equals(store.StorageDefId, def.Id, StringComparison.Ordinal))
            {
                store.StorageDefId = def.Id;
            }
            store.Stacks ??= new List<KitchenStackDto>();
            return store;
        }

        /// <summary>Put food in (see <see cref="KitchenStorageModel.Store"/>); raises
        /// <see cref="KitchenItemStored"/> when any went in. Returns how many.</summary>
        public static int Store(SaveData save, string key, KitchenStorageDef def, in KitchenContext ctx,
                                string itemId, int count, in FreshnessState arriving)
        {
            KitchenStorageStateDto store = Open(save, key, def);
            int n = KitchenStorageModel.Store(store, def, ctx, itemId, count, arriving);
            if (n > 0) EventBus.Publish(new KitchenItemStored(key, def.Id, itemId, n));
            return n;
        }

        /// <summary>Take food out (see <see cref="KitchenStorageModel.Take"/>); raises
        /// <see cref="KitchenItemTaken"/> when any came out. Returns how many.</summary>
        public static int Take(SaveData save, string key, KitchenStorageDef def, in KitchenContext ctx,
                               string itemId, int count, List<KitchenStackDto> taken = null)
        {
            KitchenStorageStateDto store = Find(save, key);
            if (store == null || def == null) return 0;
            int n = KitchenStorageModel.Take(store, def, ctx, itemId, count, taken);
            if (n > 0) EventBus.Publish(new KitchenItemTaken(key, def.Id, itemId, n));
            return n;
        }

        /// <summary>
        /// Pack up to <paramref name="units"/> bags of the cooler's ice from the supply locker
        /// (<see cref="SupplyLocker"/>, <see cref="KitchenStorageDef.IceSupplyId"/>), never more than it
        /// holds nor more than the cooler has room for. Raises <see cref="KitchenIcePacked"/>. Returns bags
        /// packed (0 for anything but a cooler).
        /// </summary>
        public static int PackIceFromLocker(SaveData save, string key, KitchenStorageDef def, in KitchenContext ctx,
                                            int units)
        {
            if (save == null || def == null || def.Kind != KitchenStorageKind.Cooler || units <= 0) return 0;
            KitchenStorageStateDto store = Open(save, key, def);
            int want = Math.Min(units, KitchenStorageModel.IceRoom(store, def, ctx));
            int have = SupplyLocker.Count(save, def.IceSupplyId);
            int n = Math.Min(want, have);
            if (n <= 0) return 0;
            int packed = KitchenStorageModel.PackIce(store, def, ctx, n);
            for (int i = 0; i < packed; i++) SupplyLocker.TrySpend(save, def.IceSupplyId);
            if (packed > 0) EventBus.Publish(new KitchenIcePacked(key, packed, store.IceRunsOutAtGameSeconds));
            return packed;
        }

        /// <summary>
        /// The slow tick: for each store, a cooler whose ice ran out is settled (its food goes ambient from
        /// the melt instant, wherever the tick lands) and <see cref="KitchenIceRanOut"/> is raised once; a
        /// fridge's power is read and a change banked now. Nothing else moves: the contents settle on read.
        /// </summary>
        public static void Tick(SaveData save, in KitchenContext ctx, Func<string, KitchenStorageDef> find,
                                IKitchenPower power)
        {
            List<KitchenStorageStateDto> all = save?.KitchenStores;
            if (all == null || find == null) return;
            power ??= AlwaysOnKitchenPower.Instance;
            for (int i = 0; i < all.Count; i++)
            {
                KitchenStorageStateDto store = all[i];
                if (store == null) continue;
                KitchenStorageDef def = find(store.StorageDefId);
                if (def == null) continue;
                switch (def.Kind)
                {
                    case KitchenStorageKind.Cooler:
                        if (store.IceRunsOutAtGameSeconds >= 0d && !store.IceOutAnnounced &&
                            ctx.NowGameSeconds >= store.IceRunsOutAtGameSeconds)
                        {
                            KitchenStorageModel.Settle(store, def, ctx);
                            store.IceOutAnnounced = true;
                            EventBus.Publish(new KitchenIceRanOut(store.Key, store.IceRunsOutAtGameSeconds));
                        }
                        break;
                    case KitchenStorageKind.Fridge:
                        KitchenStorageModel.SetPower(store, def, ctx, power.IsPowered(store.Key));
                        break;
                }
            }
        }

        /// <summary>The slow tick over the live services (the game clock calls this).</summary>
        public static void Tick()
        {
            SaveData save = GameServices.Save?.Current;
            if (save?.KitchenStores == null || save.KitchenStores.Count == 0) return;
            Tick(save, KitchenContext.Capture(), KitchenStorageCatalog.Find, GameServices.KitchenPower);
        }
    }

    /// <summary>The shipped <see cref="KitchenStorageDef"/>s, by id, from <c>Resources/Kitchen/Storage</c>.</summary>
    public static class KitchenStorageCatalog
    {
        /// <summary>The Resources folder the defs ship in.</summary>
        public const string ResourcesPath = "Kitchen/Storage";

        private static Dictionary<string, KitchenStorageDef> _byId;

        /// <summary>The def with <paramref name="id"/>, or null.</summary>
        public static KitchenStorageDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId == null)
            {
                _byId = new Dictionary<string, KitchenStorageDef>(StringComparer.Ordinal);
                KitchenStorageDef[] defs = Resources.LoadAll<KitchenStorageDef>(ResourcesPath);
                for (int i = 0; i < defs.Length; i++)
                    if (defs[i] != null && !string.IsNullOrEmpty(defs[i].Id)) _byId[defs[i].Id] = defs[i];
            }
            return _byId.TryGetValue(id, out KitchenStorageDef def) ? def : null;
        }

        /// <summary>Forget the cache (a test, or <see cref="GameServices.Reset"/>).</summary>
        public static void Clear() => _byId = null;
    }
}
