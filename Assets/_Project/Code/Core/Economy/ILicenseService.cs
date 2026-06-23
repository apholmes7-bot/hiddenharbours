namespace HiddenHarbours.Core
{
    /// <summary>
    /// The player's <b>license wallet</b> — the legal right to fish a species or use a gear class
    /// (design/progression-and-housing.md §2.2). A real, data-driven currency, not a single flag:
    /// each license is keyed by a stable id (e.g. "license.cod") and the world buys them at a vendor.
    /// Implemented by Economy's <c>LicenseService</c>; feature modules read it through
    /// <see cref="GameServices.Licenses"/> so no module references the Economy concrete class.
    ///
    /// <para><b>The gate this exists for:</b> using the rod to fish cod requires the cod license.
    /// Fishing (gameplay-systems) consults <see cref="IsLicensed"/> at catch-time WITHOUT depending on
    /// Economy — exactly the IWallet/IHold pattern. This is the minimal M2 license seam; the full
    /// proficiency/reputation eligibility tower (progression-and-housing §2.1/§2.3) is deliberately
    /// NOT here yet — a license is currently money-only, structured so eligibility can be added later
    /// without changing this contract.</para>
    /// </summary>
    public interface ILicenseService
    {
        /// <summary>True iff the player holds the license with this stable id (e.g. "license.cod").
        /// A null/empty id is treated as "no license required" → returns true (ungated content).</summary>
        bool IsLicensed(string licenseId);

        /// <summary>Grant the license with this stable id (idempotent). Does NOT charge money — the
        /// vendor handles the fee through <see cref="IWallet"/> and grants only on a successful spend.
        /// No-op on a null/empty id.</summary>
        void Grant(string licenseId);

        /// <summary>How many distinct licenses the player currently holds. For UI / tests.</summary>
        int Count { get; }
    }
}
