using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>What is in one of the player's storage containers, as saved</b> (save v25: a gear locker, a rod
    /// rack, a shed's stores). Keyed by the container's stable storage id
    /// (<see cref="IStorageContainer.StorageId"/>, e.g. <c>storage.stpeters.ginny_rod_rack</c>), with the
    /// container's def id beside it. Generic on purpose: any container with a stable id keeps its contents
    /// here with no further schema change. Food stores are NOT here: they keep their freshness clocks in
    /// <see cref="SaveData.KitchenStores"/> (v23). Read and written only through <see cref="StoredContainers"/>.
    /// Flat and JsonUtility-friendly.
    /// </summary>
    [Serializable]
    public sealed class StoredContainerDto
    {
        /// <summary>The container's storage id (<c>storage.*</c>), stable across saves.</summary>
        public string Key = "";

        /// <summary>The container's stable def id (<c>stowage.*</c>), so a reader can tell what it is.</summary>
        public string ContainerDefId = "";

        /// <summary>What is in it, in the order it went in.</summary>
        public List<StoredItemDto> Items = new List<StoredItemDto>();
    }

    /// <summary>
    /// <b>One thing in a storage container</b> (save v25). Either one WORLD OBJECT, named by its stable
    /// instance id (the starter rod, <c>tool.st_peters.rod</c>), so the load puts that same object back in
    /// the container instead of inventing a second one; or a COUNTED lot of a kind with no object of its own
    /// (<see cref="InstanceId"/> empty, <see cref="Count"/> how many). Ids only: a retuned item follows into
    /// every save.
    /// </summary>
    [Serializable]
    public sealed class StoredItemDto
    {
        /// <summary>The item kind's stable Def id (<c>tool.rod</c>).</summary>
        public string ItemId = "";

        /// <summary>The placed object's stable instance id, or empty for a counted lot.</summary>
        public string InstanceId = "";

        /// <summary>How many (a world object is always one, whatever is written here).</summary>
        public int Count = 1;

        /// <summary>True for a row that names one world object.</summary>
        public bool IsInstance => !string.IsNullOrEmpty(InstanceId);

        /// <summary>How many items this row is: one for a world object, the count for a lot.</summary>
        public int Units => IsInstance ? 1 : Math.Max(0, Count);
    }
}
