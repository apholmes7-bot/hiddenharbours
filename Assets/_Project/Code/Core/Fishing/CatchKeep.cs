namespace HiddenHarbours.Core
{
    /// <summary>
    /// IS THIS SPECIES PROTECTED, asked by id — the Core seam between the species data (the
    /// <c>FishSpeciesDef.Protected</c> flag, in the Fishing module) and every container, hand and restore
    /// path that must refuse a protected catch without knowing what a Def is (rule 4).
    ///
    /// <para>Owner ruling 2026-11-15: a protected species may be caught and documented, but it must be
    /// released. It never enters a hold, a cooler, a pail, a hand, the market or a recipe.</para>
    /// <para>Published by the Fishing module's species registry beside <see cref="IFishSizeFacts"/>.
    /// May be null (a bare scene, an EditMode rig): <see cref="CatchKeep"/> then lets everything through,
    /// and the landing choke points, which read the Def directly, still release.</para>
    /// FLAG lead-architect: new Core contract (2026-11-15, protected species are release-only).
    /// </summary>
    public interface IFishConservationFacts
    {
        /// <summary>True when the species is protected (release-only). False for an unknown id.</summary>
        bool IsProtected(string fishId);
    }

    /// <summary>
    /// The ONE shared keep rule. Every landing choke point asks it before a catch becomes kept, and every
    /// downstream entry (hold, pail, freezer, hand, save restore) asks it again as defence in depth.
    /// Pure: the answer is a function of the species id and the facts handed in.
    /// </summary>
    public static class CatchKeep
    {
        /// <summary>May a catch of <paramref name="speciesId"/> be kept? False only when the facts say
        /// the species is protected. No facts, or no id, keeps (nothing known to refuse).</summary>
        public static bool MayKeep(string speciesId, IFishConservationFacts facts)
            => facts == null || string.IsNullOrEmpty(speciesId) || !facts.IsProtected(speciesId);

        /// <summary><see cref="MayKeep(string, IFishConservationFacts)"/> against the registered facts.</summary>
        public static bool MayKeep(string speciesId)
            => MayKeep(speciesId, GameServices.FishConservation);

        /// <summary><see cref="MayKeep(string)"/> for a whole catch item.</summary>
        public static bool MayKeep(in CatchItem item) => MayKeep(item.SpeciesId);
    }

    /// <summary>
    /// <b>A protected catch was documented and put back.</b> Published by a landing choke point at the
    /// moment it releases a protected fish, carrying the whole item (species, weight) exactly as it would
    /// have been kept, so a record of species seen and sizes can listen here. It never implies the catch
    /// entered a hold or a hand: <see cref="FishCaught"/> and <see cref="CatchLanded"/> are not raised.
    /// </summary>
    public readonly struct CatchReleased
    {
        public readonly CatchItem Item;

        public CatchReleased(in CatchItem item) => Item = item;
    }

    /// <summary>
    /// <b>A catch went back because the player holds no licence for its species.</b> Owner ruling
    /// 2026-11-23: nothing stops him digging, catching or trapping, but a species he is not licensed for
    /// is released at once, he says so, and it is NOT written in the Notebook. Published by every landing
    /// choke point (the rod, the dig, the trap haul) at the moment it lets such a catch go.
    ///
    /// <para>Deliberately a different signal from <see cref="CatchReleased"/>: that one is "documented,
    /// then put back" and the catch record counts it. Nothing that keeps a record listens to this one; it
    /// exists so other systems (a hint, a sound, a later licence nudge) can react. It never implies the
    /// catch entered a hold, a hand or a pail.</para>
    /// </summary>
    public readonly struct UnlicensedCatchReleased
    {
        /// <summary>The catch exactly as it would have been kept (species, name, weight).</summary>
        public readonly CatchItem Item;

        /// <summary>The stable id of the licence that would have let him keep it (<c>license.*</c>), or
        /// empty when the data could not name one.</summary>
        public readonly string RequiredLicenceId;

        public UnlicensedCatchReleased(in CatchItem item, string requiredLicenceId)
        {
            Item = item;
            RequiredLicenceId = requiredLicenceId ?? "";
        }
    }
}
