namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>THE DEED BOX</b> — does the player own this home? The first ownership record for a place to
    /// live, and deliberately the smallest one that can be true.
    ///
    /// <para><b>Why it is here and not in Economy.</b> Three lanes ask the question and none of them may
    /// reference the others (rule 4): Economy takes the money, the world places the building and decides
    /// whether its door opens, and a test grants a deed to set up a fixture. That is exactly the argument
    /// <see cref="SupplyLocker"/> and <see cref="TackleBox"/> already make for living in Core, so this
    /// follows them rather than inventing a service.</para>
    ///
    /// <para><b>⭐ ONE FLAG, NOT A PARCEL SYSTEM — and that is a deliberate ceiling, not an oversight.</b>
    /// A deed here is a single persisted boolean keyed by a stable home id. There is no land, no parcel,
    /// no lease, no tenancy and no map of who owns what: a sweep of the repo for such a concept in #551
    /// found only <c>BoatOwnerDef</c>, and the land-purpose vision that would need one is phase-gated
    /// (CLAUDE.md rule 8). The residence ladder in <c>design/progression-and-housing.md</c> §4.1 —
    /// upgrades, comfort scores, decor, multiple residences — is the shape this GROWS into when its phase
    /// arrives, and every one of those is additive to a boolean. Building the tower now would be the
    /// scope creep rule 8 names.</para>
    ///
    /// <para><b>No new save field, and no schema bump.</b> It rides <see cref="ISaveService.GetFlag"/>'s
    /// keyed store — the same store the onboarding beats and the fronted-fee grant already use (see
    /// <see cref="SaveMigration"/>'s v6→v7 note, which set that precedent explicitly). A deed is exactly
    /// what that store is for: one stable key, one bool, append-only. Adding a <c>List&lt;string&gt;</c>
    /// to <see cref="SaveData"/> would have cost a migration to say the same thing.</para>
    ///
    /// <para>Static and null-safe throughout: with no save service attached (EditMode rigs, pre-boot) a
    /// deed reads as NOT owned and a grant is a silent no-op, rather than throwing — the established
    /// greybox posture. That also means a granted deed before boot does not survive, which is correct:
    /// there is nowhere to write it.</para>
    /// </summary>
    public static class HomeDeeds
    {
        /// <summary>
        /// The flag-key prefix every deed shares, so home ownership is greppable in a save file and can
        /// never collide with an onboarding beat. A deed's full key is this plus the home id, e.g.
        /// <c>deed.home.ginny_lot_camper</c>.
        /// </summary>
        public const string KeyPrefix = "deed.";

        /// <summary>The persisted flag key for a home id. Empty for a null/empty id, which
        /// <see cref="ISaveService.SetFlag"/> then no-ops on.</summary>
        public static string KeyFor(string homeId) =>
            string.IsNullOrEmpty(homeId) ? string.Empty : KeyPrefix + homeId;

        /// <summary>
        /// True iff the player holds the deed to this home.
        ///
        /// <para>⚠ A null/empty id is NOT owned — the opposite of
        /// <see cref="ILicenseService.IsLicensed"/>, which treats an empty id as "ungated" and returns
        /// true. The two are right for opposite reasons: a fish that names no licence needs none, but a
        /// door that names no home has nothing to unlock it, and answering "owned" there would open every
        /// unconfigured door in the world.</para>
        /// </summary>
        public static bool IsOwned(ISaveService save, string homeId)
        {
            if (save == null || string.IsNullOrEmpty(homeId)) return false;
            return save.GetFlag(KeyFor(homeId));
        }

        /// <summary>Read the deed off the live save service (<see cref="GameServices.Save"/>) — the form
        /// the runtime uses. False when no save service is attached.</summary>
        public static bool IsOwned(string homeId) => IsOwned(GameServices.Save, homeId);

        /// <summary>
        /// Record the deed (idempotent) and persist it. Does <b>NOT</b> charge money — the door checks
        /// the price and spends through <see cref="IWallet"/>, and grants only on a successful spend.
        /// That split is <see cref="ILicenseService.Grant"/>'s, for its reason: a grant that could also
        /// take money is a grant no test can set up without a wallet.
        ///
        /// <para>Returns true iff this call is what made the home owned, so a caller can tell "bought it"
        /// from "already had it" without reading back.</para>
        /// </summary>
        public static bool Grant(ISaveService save, string homeId)
        {
            if (save == null || string.IsNullOrEmpty(homeId)) return false;
            if (save.GetFlag(KeyFor(homeId))) return false;   // already held — nothing to persist
            save.SetFlag(KeyFor(homeId), true);
            return true;
        }

        /// <summary>Record the deed on the live save service. See <see cref="Grant(ISaveService,string)"/>.</summary>
        public static bool Grant(string homeId) => Grant(GameServices.Save, homeId);
    }
}
