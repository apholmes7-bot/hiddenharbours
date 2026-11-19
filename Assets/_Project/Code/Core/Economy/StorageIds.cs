using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The fixed storage ids</b> (<see cref="IStorageContainer.StorageId"/>) for the containers that are
    /// not placed fixtures and so have no authored field to carry one: the persistent boat hold and the
    /// freezers at Ginny's and in the camper. A placed rack or locker authors its id on its own component. Append-only: an id
    /// is a save key.
    /// </summary>
    public static class StorageIds
    {
        /// <summary>What every storage id starts with. Only an id with this prefix is remembered on the save.</summary>
        public const string Prefix = "storage.";

        /// <summary>The player's boat hold (the one the region coordinator carries across crossings).</summary>
        public const string BoatHold = "storage.boat_hold";

        /// <summary>The chest freezer at Ginny's.</summary>
        public const string GinnyFreezer = "storage.stpeters.ginny_freezer";

        /// <summary>The camper's chest freezer on Ginny's lot (owner 2026-11-17, "bigger freezer"): a catch
        /// hold like Ginny's, its catch kept in the v25 storage rows under this id.</summary>
        public const string CamperFreezer = "storage.stpeters.camper_freezer";

        /// <summary>True for an id that names a remembered container (<c>storage.</c> and something after it).</summary>
        public static bool IsStorageId(string id)
            => !string.IsNullOrEmpty(id) && id.Length > Prefix.Length &&
               id.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
