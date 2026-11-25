using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The landing choke point's keep-or-release verdict (owner ruling 2026-11-15: a protected species
    /// can be documented but must be released). Pure: a function of the species Def and the Core facts.
    ///
    /// <para>The Def's own <see cref="FishSpeciesDef.Protected"/> flag is read first, so a rod pool
    /// species that never reached the registry still goes back. The shared <see cref="CatchKeep"/> rule
    /// is asked as well, so the landing and every downstream container answer from the same data.</para>
    /// </summary>
    public static class CatchReleaseRule
    {
        /// <summary>True when this landed fish must be documented and released, never kept.</summary>
        public static bool MustRelease(FishSpeciesDef fish, IFishConservationFacts facts)
            => fish != null && (fish.Protected || !CatchKeep.MayKeep(fish.Id, facts));

        /// <summary>
        /// The same verdict for a resolved catch known only by id (the pot and trap sorts): the species
        /// Def is looked up in <see cref="FishSpeciesRegistry"/> so its own flag holds even with no Core
        /// facts registered, and the facts are asked as well. An unknown id with no facts is kept.
        /// </summary>
        public static bool MustReleaseById(string speciesId, IFishConservationFacts facts)
            => !string.IsNullOrEmpty(speciesId)
               && (MustRelease(FishSpeciesRegistry.Get(speciesId), facts) || !CatchKeep.MayKeep(speciesId, facts));
    }
}
