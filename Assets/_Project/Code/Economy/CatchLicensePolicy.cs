using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// The catch-side licence gate (St Peters opening): given the authored licenses and the player's
    /// licence wallet, answers <b>"may this species be landed right now?"</b> A species is gated iff some
    /// <see cref="LicenseDef"/> lists it in <see cref="LicenseDef.PermittedSpeciesIds"/>; landing it then
    /// requires holding that licence. The cod licence gates cod; everything ungated stays catchable.
    ///
    /// <para><b>One source of truth, kept in lane.</b> The mapping "cod → license.cod" is derived from the
    /// licence data, not duplicated — so authoring a licence is the only place the gate is declared, and
    /// it can't drift from the species. This is a pure helper over the Core <see cref="ILicenseService"/>
    /// contract: Fishing (gameplay-systems) calls <see cref="MayLand"/> at land-time WITHOUT referencing
    /// the Economy concrete classes (it passes the species id + reads <see cref="GameServices.Licenses"/>),
    /// and it's fully EditMode-testable. We deliberately do NOT add a RequiredLicenseId field to the
    /// Fishing <c>FishSpeciesDef</c> (that's gameplay-systems' schema) — the gate is keyed by id.</para>
    /// </summary>
    public static class CatchLicensePolicy
    {
        /// <summary>
        /// True iff <paramref name="speciesId"/> may be landed given the player's <paramref name="licenses"/>
        /// and the authored <paramref name="allLicenses"/>. An ungated species (no licence lists it) is
        /// always true. A gated species is true iff the player holds the gating licence. A null
        /// <paramref name="licenses"/> service means "no wallet yet" → gated species are NOT landable
        /// (fail closed: you can't take cod before you can hold a licence), ungated stay landable.
        /// </summary>
        public static bool MayLand(string speciesId, IReadOnlyList<LicenseDef> allLicenses, ILicenseService licenses)
        {
            if (string.IsNullOrEmpty(speciesId)) return true;

            string requiredLicenseId = RequiredLicenseFor(speciesId, allLicenses);
            if (string.IsNullOrEmpty(requiredLicenseId)) return true;   // ungated species

            // Gated: need the wallet AND the licence in it.
            return licenses != null && licenses.IsLicensed(requiredLicenseId);
        }

        /// <summary>The stable id of the licence that gates <paramref name="speciesId"/>, or null if no
        /// authored licence lists it (the species is ungated). The first licence whose
        /// <see cref="LicenseDef.PermittedSpeciesIds"/> contains the species wins (one gate per species
        /// by design).</summary>
        public static string RequiredLicenseFor(string speciesId, IReadOnlyList<LicenseDef> allLicenses)
        {
            if (string.IsNullOrEmpty(speciesId) || allLicenses == null) return null;
            for (int i = 0; i < allLicenses.Count; i++)
            {
                var lic = allLicenses[i];
                if (lic == null || lic.PermittedSpeciesIds == null || string.IsNullOrEmpty(lic.Id)) continue;
                for (int j = 0; j < lic.PermittedSpeciesIds.Length; j++)
                    if (lic.PermittedSpeciesIds[j] == speciesId)
                        return lic.Id;
            }
            return null;
        }
    }
}
