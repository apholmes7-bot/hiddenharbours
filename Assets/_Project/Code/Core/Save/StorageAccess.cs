namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>May the player use this store?</b> A store's Def names the home whose deed opens it
    /// (<c>StowageDef.DeedHomeId</c>, <see cref="KitchenStorageDef.DeedHomeId"/>): the camper's lockers,
    /// racks and freezer, and the shed's stores, open only once the camper's deed is his
    /// (<see cref="HomeDeeds"/>). A store that names no home is open to him, which is right for the stores
    /// he lodges beside at Ginny's: lodging needs no deed.
    ///
    /// <para>One question in Core so the rack (Player), the food stores (Core) and whatever fixture the
    /// interior lane stands next all answer it the same way (rule 4). Null-safe: with no save service a
    /// gated store reads as closed, the same posture as <see cref="HomeDeeds.IsOwned(ISaveService,string)"/>.</para>
    /// </summary>
    public static class StorageAccess
    {
        /// <summary>True when <paramref name="deedHomeId"/> is empty, or its deed is held on <paramref name="save"/>.</summary>
        public static bool IsOpen(ISaveService save, string deedHomeId)
            => string.IsNullOrEmpty(deedHomeId) || HomeDeeds.IsOwned(save, deedHomeId);

        /// <summary>Asked of the live save service (<see cref="GameServices.Save"/>).</summary>
        public static bool IsOpen(string deedHomeId) => IsOpen(GameServices.Save, deedHomeId);

        /// <summary>
        /// Asked of a save blob directly, for the model-level store functions that are handed one
        /// (<see cref="KitchenStores"/>): the deed is the same persisted flag <see cref="HomeDeeds"/> writes
        /// through <see cref="ISaveService.SetFlag"/>, read off <see cref="SaveData.OnboardingFlags"/>. A null
        /// save leaves a gated store closed.
        /// </summary>
        public static bool IsOpen(SaveData save, string deedHomeId)
        {
            if (string.IsNullOrEmpty(deedHomeId)) return true;
            var flags = save?.OnboardingFlags;
            if (flags == null) return false;
            string key = HomeDeeds.KeyFor(deedHomeId);
            for (int i = 0; i < flags.Count; i++)
                if (flags[i].Key == key) return flags[i].Value;
            return false;
        }
    }
}
