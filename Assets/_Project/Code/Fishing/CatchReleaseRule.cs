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
    }
}
