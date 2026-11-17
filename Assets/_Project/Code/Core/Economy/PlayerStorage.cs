using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The storage containers the player keeps things in</b>: his boat's hold, the freezer at Ginny's, a
    /// rod rack or gear locker at home. Not what he carries (that is <see cref="GameServices.PlayerHold"/> and
    /// his hands), and never somebody else's hold. A lane whose container is the player's registers it here,
    /// so a reader in another lane (the gift rows, which list what he has anywhere) can find it through Core
    /// without naming the class.
    ///
    /// <para><b>What is listed.</b> A container is an <see cref="IStorageContainer"/> (a stable id and a
    /// priority), an <see cref="IHold"/> (catch), or both. A reader takes the facet it needs:
    /// <see cref="Get"/> answers only holds (a rod rack is not a catch hold), <see cref="GetContainer"/> only
    /// named containers, and <see cref="Find"/> looks one up by id. <c>Register(IHold)</c> stays for a hold
    /// with no id.</para>
    ///
    /// <para><b>Order is deterministic, never registration order.</b> Containers sort by
    /// <see cref="IStorageContainer.StoragePriority"/> (lower first; a hold with no id counts as
    /// <see cref="DefaultPriority"/>), then by ordinal <see cref="IStorageContainer.StorageId"/>; only exact
    /// ties keep the order they were listed in. The order is what a reader walks: the Give rows' order and the
    /// container a gift is taken from. Priority and id are read once, when the container is listed.</para>
    ///
    /// <para><b>Ids are unique.</b> Listing a second container under an id already listed logs a warning
    /// (both stay listed, so nothing is lost; the content-validation test is what keeps authored ids apart).
    /// Listing the same object twice is a no-op, so a component that registers on every enable stays listed
    /// once.</para>
    ///
    /// <para><b>Lifetime: register on enable, unregister on destroy, never on disable</b> (the
    /// <see cref="GameServices.PlayerHold"/> rule): root toggling is how a region hop works, and a container
    /// that left the list on disable would vanish mid-crossing. A destroyed container left behind reads as
    /// null (Unity's fake-null is laundered), so a reader skips it either way. The list is not the save: a
    /// container's CONTENTS live on the save under its id (<see cref="StoredContainers"/>), and the scene
    /// object is a view that binds to them.</para>
    ///
    /// <para>Allocation-free to read: a count and an index, no enumerator (rule 7). Cleared by
    /// <see cref="GameServices.Reset"/>: anything that outlives a Reset registers again.</para>
    /// FLAG lead-architect: Core contract (the player's storage; read by the gift rows).
    /// </summary>
    public static class PlayerStorage
    {
        /// <summary>The boat's hold sorts first: a gift comes off the boat before the freezer at home.</summary>
        public const int PriorityBoatHold = 10;

        /// <summary>A home freezer.</summary>
        public const int PriorityHomeFreezer = 20;

        /// <summary>A placed rack or locker (holds tools and gear, not catch).</summary>
        public const int PriorityFixture = 50;

        /// <summary>A container with no priority of its own (a hold registered without an id).</summary>
        public const int DefaultPriority = 100;

        private struct Entry
        {
            public object Thing;
            public int Priority;
            public string Id;
        }

        private static readonly List<Entry> _stores = new List<Entry>(4);

        /// <summary>How many containers are registered (a destroyed one still counts until it is
        /// unregistered; the getters return null for it).</summary>
        public static int Count => _stores.Count;

        /// <summary>Container <paramref name="index"/> as a catch hold, or null when out of range, destroyed,
        /// or not a hold (a rod rack).</summary>
        public static IHold Get(int index) => Live(index) as IHold;

        /// <summary>Container <paramref name="index"/> as a named container, or null when out of range,
        /// destroyed, or registered without an id.</summary>
        public static IStorageContainer GetContainer(int index) => Live(index) as IStorageContainer;

        /// <summary>The live container listed under <paramref name="storageId"/>, or null.</summary>
        public static IStorageContainer Find(string storageId)
        {
            if (string.IsNullOrEmpty(storageId)) return null;
            for (int i = 0; i < _stores.Count; i++)
            {
                if (!string.Equals(_stores[i].Id, storageId, StringComparison.Ordinal)) continue;
                if (Live(i) is IStorageContainer found) return found;
            }
            return null;
        }

        /// <summary>List <paramref name="hold"/> as one of the player's storage containers. Null and a hold
        /// already listed are ignored.</summary>
        public static void Register(IHold hold) => Add(hold);

        /// <summary>List <paramref name="container"/>, sorted by its priority and id. Null and a container
        /// already listed are ignored; another container under the same id is warned about.</summary>
        public static void Register(IStorageContainer container) => Add(container);

        /// <summary>For a container that is both (the boat's hold, the freezer): one entry, sorted by id.</summary>
        public static void Register<T>(T container) where T : class, IHold, IStorageContainer => Add(container);

        /// <summary>Take <paramref name="hold"/> off the list. A hold not listed is ignored.</summary>
        public static void Unregister(IHold hold) => Remove(hold);

        /// <summary>Take <paramref name="container"/> off the list. One not listed is ignored.</summary>
        public static void Unregister(IStorageContainer container) => Remove(container);

        /// <summary>For a container that is both.</summary>
        public static void Unregister<T>(T container) where T : class, IHold, IStorageContainer => Remove(container);

        /// <summary>Forget every container (test isolation, <see cref="GameServices.Reset"/>).</summary>
        public static void Clear() => _stores.Clear();

        private static void Add(object thing)
        {
            if (thing == null || IndexOf(thing) >= 0) return;
            var named = thing as IStorageContainer;
            var entry = new Entry
            {
                Thing = thing,
                Priority = named != null ? named.StoragePriority : DefaultPriority,
                Id = named != null ? named.StorageId ?? "" : "",
            };

            if (entry.Id.Length > 0)
                for (int i = 0; i < _stores.Count; i++)
                    if (string.Equals(_stores[i].Id, entry.Id, StringComparison.Ordinal) && Live(i) != null)
                    {
                        UnityEngine.Debug.LogWarning("[PlayerStorage] Two containers share the storage id '" +
                                                     entry.Id + "'. Ids are save keys and must be unique.");
                        break;
                    }

            int at = _stores.Count;
            for (int i = 0; i < _stores.Count; i++)
                if (Compare(in entry, _stores[i]) < 0) { at = i; break; }
            _stores.Insert(at, entry);
        }

        private static void Remove(object thing)
        {
            int i = IndexOf(thing);
            if (i >= 0) _stores.RemoveAt(i);
        }

        private static int Compare(in Entry a, Entry b)
        {
            if (a.Priority != b.Priority) return a.Priority < b.Priority ? -1 : 1;
            return string.CompareOrdinal(a.Id, b.Id);
        }

        private static object Live(int index)
        {
            if (index < 0 || index >= _stores.Count) return null;
            object thing = _stores[index].Thing;
            return thing is UnityEngine.Object o && o == null ? null : thing;
        }

        private static int IndexOf(object thing)
        {
            if (thing == null) return -1;
            for (int i = 0; i < _stores.Count; i++)
                if (ReferenceEquals(_stores[i].Thing, thing)) return i;
            return -1;
        }
    }
}
