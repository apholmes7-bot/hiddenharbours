using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The player's storage containers on the save</b> (save v25): find a container's rows by its key, put
    /// a world object or a counted lot in, take it out. Location-agnostic and module-agnostic: a rod rack at
    /// Ginny's, a gear locker in the camper or a shed's stores all pass their own key, and none of them is
    /// named here (rule 4). Capacity is the caller's Def's, passed in, so nothing here holds a tunable (rule 6).
    ///
    /// <para><b>One object, one place.</b> Putting a world object in a container takes it out of any other
    /// container first, so a save can never hold the same rod twice. A container row with nothing left in it
    /// is removed, so an emptied locker leaves no trace.</para>
    ///
    /// <para>Static and null-safe throughout, like <see cref="KitchenStores"/>: a null save or an empty key
    /// is a refusal, never a throw. Writes go to the save blob in memory; the next save writes the disk.</para>
    /// </summary>
    public static class StoredContainers
    {
        /// <summary>The save's container list (made on first use).</summary>
        public static List<StoredContainerDto> All(SaveData save)
            => save == null ? null : save.StoredContainers ??= new List<StoredContainerDto>();

        /// <summary>The container with <paramref name="key"/>, or null.</summary>
        public static StoredContainerDto Find(SaveData save, string key)
        {
            List<StoredContainerDto> all = save?.StoredContainers;
            if (all == null || string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && string.Equals(all[i].Key, key, StringComparison.Ordinal)) return all[i];
            return null;
        }

        /// <summary>How many items are in the container (a world object counts one, a lot its count).</summary>
        public static int CountIn(SaveData save, string key)
        {
            StoredContainerDto c = Find(save, key);
            if (c?.Items == null) return 0;
            int n = 0;
            for (int i = 0; i < c.Items.Count; i++)
                if (c.Items[i] != null) n += c.Items[i].Units;
            return n;
        }

        /// <summary>Copy the instance ids of the world objects in the container into <paramref name="into"/>,
        /// in the order they went in. Returns how many were added.</summary>
        public static int InstancesIn(SaveData save, string key, List<string> into)
        {
            StoredContainerDto c = Find(save, key);
            if (c?.Items == null || into == null) return 0;
            int n = 0;
            for (int i = 0; i < c.Items.Count; i++)
            {
                StoredItemDto item = c.Items[i];
                if (item == null || !item.IsInstance) continue;
                into.Add(item.InstanceId);
                n++;
            }
            return n;
        }

        /// <summary>The key of the container holding world object <paramref name="instanceId"/>, or null.</summary>
        public static string ContainerOf(SaveData save, string instanceId)
        {
            List<StoredContainerDto> all = save?.StoredContainers;
            if (all == null || string.IsNullOrEmpty(instanceId)) return null;
            for (int i = 0; i < all.Count; i++)
                if (IndexOfInstance(all[i], instanceId) >= 0) return all[i].Key;
            return null;
        }

        /// <summary>
        /// Put world object <paramref name="instanceId"/> (of kind <paramref name="itemId"/>) in the container,
        /// out of any other first. Refused (false, nothing changed) for a null save, an empty key, id or kind,
        /// or a container already holding <paramref name="capacity"/> items. Already in this container is a
        /// success that changes nothing.
        /// </summary>
        public static bool TryStoreInstance(SaveData save, string key, string containerDefId, string itemId,
                                            string instanceId, int capacity)
        {
            if (save == null || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(itemId) ||
                string.IsNullOrEmpty(instanceId)) return false;
            StoredContainerDto here = Find(save, key);
            if (IndexOfInstance(here, instanceId) >= 0) return true;
            if (CountIn(save, key) + 1 > capacity) return false;

            RemoveInstanceEverywhere(save, instanceId);
            here = Open(save, key, containerDefId);
            here.Items.Add(new StoredItemDto { ItemId = itemId, InstanceId = instanceId, Count = 1 });
            return true;
        }

        /// <summary>Take world object <paramref name="instanceId"/> out of the container. True iff it was there.</summary>
        public static bool RemoveInstance(SaveData save, string key, string instanceId)
        {
            StoredContainerDto c = Find(save, key);
            int i = IndexOfInstance(c, instanceId);
            if (i < 0) return false;
            c.Items.RemoveAt(i);
            DropIfEmpty(save, c);
            return true;
        }

        /// <summary>Take world object <paramref name="instanceId"/> out of whichever container holds it (every
        /// one, should a damaged save list it twice). True iff it was anywhere.</summary>
        public static bool ForgetInstance(SaveData save, string instanceId)
        {
            if (ContainerOf(save, instanceId) == null) return false;
            RemoveInstanceEverywhere(save, instanceId);
            return true;
        }

        /// <summary>
        /// Put up to <paramref name="count"/> of a counted kind in the container, never past
        /// <paramref name="capacity"/>. Returns how many went in (0 for a bad argument or a full container).
        /// </summary>
        public static int StoreCounted(SaveData save, string key, string containerDefId, string itemId,
                                       int count, int capacity)
        {
            if (save == null || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(itemId) || count <= 0) return 0;
            int n = Math.Min(count, capacity - CountIn(save, key));
            if (n <= 0) return 0;
            StoredContainerDto c = Open(save, key, containerDefId);
            for (int i = 0; i < c.Items.Count; i++)
            {
                StoredItemDto item = c.Items[i];
                if (item == null || item.IsInstance || !string.Equals(item.ItemId, itemId, StringComparison.Ordinal)) continue;
                item.Count += n;
                return n;
            }
            c.Items.Add(new StoredItemDto { ItemId = itemId, InstanceId = "", Count = n });
            return n;
        }

        /// <summary>Take up to <paramref name="count"/> of a counted kind out. Returns how many came out.</summary>
        public static int TakeCounted(SaveData save, string key, string itemId, int count)
        {
            StoredContainerDto c = Find(save, key);
            if (c?.Items == null || string.IsNullOrEmpty(itemId) || count <= 0) return 0;
            int taken = 0;
            for (int i = c.Items.Count - 1; i >= 0 && taken < count; i--)
            {
                StoredItemDto item = c.Items[i];
                if (item == null || item.IsInstance || !string.Equals(item.ItemId, itemId, StringComparison.Ordinal)) continue;
                int n = Math.Min(count - taken, item.Count);
                item.Count -= n;
                taken += n;
                if (item.Count <= 0) c.Items.RemoveAt(i);
            }
            DropIfEmpty(save, c);
            return taken;
        }

        // ---- catch (a container that is a catch hold) -----------------------------------------------------

        /// <summary>The catch records kept under <paramref name="key"/>, or null when there are none.</summary>
        public static IReadOnlyList<HeldCatchDto> CatchIn(SaveData save, string key) => Find(save, key)?.Catch;

        /// <summary>
        /// Write <paramref name="items"/> as the whole catch under <paramref name="key"/> (the
        /// <see cref="HoldCatchCodec"/> snapshot a hold takes on every change, the ShipHold pattern). Empty
        /// items clear it, and a container left with nothing in it leaves no row. Capacity is the hold's own.
        /// </summary>
        public static void WriteCatch(SaveData save, string key, string containerDefId, IReadOnlyList<CatchItem> items)
        {
            if (save == null || string.IsNullOrEmpty(key)) return;
            if (items == null || items.Count == 0)
            {
                StoredContainerDto found = Find(save, key);
                if (found == null) return;
                found.Catch?.Clear();
                DropIfEmpty(save, found);
                return;
            }
            StoredContainerDto c = Open(save, key, containerDefId);
            HoldCatchCodec.Capture(items, c.Catch);
        }

        // ---- helpers --------------------------------------------------------------------------------------

        /// <summary>True when a container row holds nothing at all: no item rows and no catch.</summary>
        public static bool IsEmpty(StoredContainerDto c)
            => c == null || ((c.Items == null || c.Items.Count == 0) && (c.Catch == null || c.Catch.Count == 0));

        private static StoredContainerDto Open(SaveData save, string key, string containerDefId)
        {
            StoredContainerDto c = Find(save, key);
            if (c == null)
            {
                c = new StoredContainerDto { Key = key, ContainerDefId = containerDefId ?? "" };
                All(save).Add(c);
            }
            else if (!string.IsNullOrEmpty(containerDefId))
            {
                c.ContainerDefId = containerDefId;   // a rebuilt fixture keeps what was in it
            }
            c.Items ??= new List<StoredItemDto>();
            c.Catch ??= new List<HeldCatchDto>();
            return c;
        }

        private static int IndexOfInstance(StoredContainerDto c, string instanceId)
        {
            if (c?.Items == null || string.IsNullOrEmpty(instanceId)) return -1;
            for (int i = 0; i < c.Items.Count; i++)
                if (c.Items[i] != null && string.Equals(c.Items[i].InstanceId, instanceId, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        private static void RemoveInstanceEverywhere(SaveData save, string instanceId)
        {
            List<StoredContainerDto> all = save?.StoredContainers;
            if (all == null) return;
            for (int k = all.Count - 1; k >= 0; k--)
            {
                StoredContainerDto c = all[k];
                int i;
                while ((i = IndexOfInstance(c, instanceId)) >= 0) c.Items.RemoveAt(i);
                if (c != null && c.Items != null && IsEmpty(c)) all.RemoveAt(k);
            }
        }

        private static void DropIfEmpty(SaveData save, StoredContainerDto c)
        {
            if (c == null || !IsEmpty(c)) return;
            save?.StoredContainers?.Remove(c);
        }
    }
}
