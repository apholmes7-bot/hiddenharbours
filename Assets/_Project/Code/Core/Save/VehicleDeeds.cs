namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>THE LOG BOOK</b>: does the player own this land vehicle, and does it run? The vehicle twin of
    /// <see cref="HomeDeeds"/>, kept beside it in Core for the same reason (Economy takes the money,
    /// Vehicles hands over the machine, a test sets up a fixture, and none of them may reference the
    /// others, rule 4).
    ///
    /// <para><b>Keyed by the OFFER id, not the model id.</b> A deed here is <c>deed.vehicleoffer.*</c>,
    /// so two offers for the same <c>VehicleDef</c> are two machines with two deeds. The prefix is the
    /// same <c>deed.</c> the homes use, in the same keyed flag store, so every deed is greppable in a save
    /// the same way; the type prefix in the id (<c>home.</c> vs <c>vehicleoffer.</c>) keeps them apart.</para>
    ///
    /// <para><b>No new save field, and no schema bump.</b> It rides <see cref="ISaveService.GetFlag"/>'s
    /// keyed store exactly as <see cref="HomeDeeds"/> does. Where an owned vehicle was last parked is a
    /// later, separate save change (the plan's slice V3) and does not belong here.</para>
    ///
    /// <para><b>HOOK (lead-architect):</b> both the vehicle and home plans propose one shared
    /// <c>Deeds</c> helper that <see cref="HomeDeeds"/> forwards to. That needs an edit to
    /// <c>HomeDeeds</c>, which a sibling lane is changing, so this file deliberately duplicates the two
    /// one-line key rules instead of touching it. Fold the two together once that ruling lands.</para>
    ///
    /// <para>Static and null-safe throughout, the <see cref="HomeDeeds"/> posture: with no save service a
    /// deed reads as NOT owned and a grant is a silent no-op.</para>
    /// </summary>
    public static class VehicleDeeds
    {
        /// <summary>The deed prefix, the same one <see cref="HomeDeeds.KeyPrefix"/> uses.</summary>
        public const string KeyPrefix = "deed.";

        /// <summary>The prefix of the flag that says a damaged used machine has been repaired.</summary>
        public const string RepairedKeyPrefix = "repaired.";

        /// <summary>The persisted deed key for an offer id (<c>deed.vehicleoffer.x</c>). Empty for an
        /// empty id, which <see cref="ISaveService.SetFlag"/> no-ops on.</summary>
        public static string KeyFor(string offerId) =>
            string.IsNullOrEmpty(offerId) ? string.Empty : KeyPrefix + offerId;

        /// <summary>The persisted repaired key for an offer id (<c>repaired.vehicleoffer.x</c>).</summary>
        public static string RepairedKeyFor(string offerId) =>
            string.IsNullOrEmpty(offerId) ? string.Empty : RepairedKeyPrefix + offerId;

        /// <summary>True iff the player holds the deed to this offer's machine. An empty id is NOT owned
        /// (the <see cref="HomeDeeds.IsOwned(ISaveService,string)"/> rule, for its reason).</summary>
        public static bool IsOwned(ISaveService save, string offerId)
        {
            if (save == null || string.IsNullOrEmpty(offerId)) return false;
            return save.GetFlag(KeyFor(offerId));
        }

        /// <summary>Read the deed off the live save service.</summary>
        public static bool IsOwned(string offerId) => IsOwned(GameServices.Save, offerId);

        /// <summary>Record the deed (idempotent). Does NOT charge money; the seller spends through
        /// <see cref="IWallet"/> and grants only on a successful spend. True iff this call made it owned.</summary>
        public static bool Grant(ISaveService save, string offerId)
        {
            if (save == null || string.IsNullOrEmpty(offerId)) return false;
            if (save.GetFlag(KeyFor(offerId))) return false;
            save.SetFlag(KeyFor(offerId), true);
            return true;
        }

        /// <summary>True iff the repaired flag is set for this offer's machine.</summary>
        public static bool IsRepaired(ISaveService save, string offerId)
        {
            if (save == null || string.IsNullOrEmpty(offerId)) return false;
            return save.GetFlag(RepairedKeyFor(offerId));
        }

        /// <summary>Record that this offer's machine runs (idempotent). Does NOT charge money. True iff
        /// this call changed it.</summary>
        public static bool MarkRepaired(ISaveService save, string offerId)
        {
            if (save == null || string.IsNullOrEmpty(offerId)) return false;
            if (save.GetFlag(RepairedKeyFor(offerId))) return false;
            save.SetFlag(RepairedKeyFor(offerId), true);
            return true;
        }

        /// <summary>Owned AND runs: the question the Vehicles module asks before it lets the player drive
        /// an owned machine away. A machine bought sound is marked repaired at purchase.</summary>
        public static bool IsUsable(ISaveService save, string offerId) =>
            IsOwned(save, offerId) && IsRepaired(save, offerId);
    }
}
