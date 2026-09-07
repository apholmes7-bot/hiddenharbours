namespace HiddenHarbours.Core
{
    /// <summary>
    /// WHAT A HOOKED FISH DOES, asked by id — the named Core seam between the species data (which lives
    /// in the Fishing module, as <c>FishSpeciesDef</c> assets) and the presenters that draw a fish
    /// without being allowed to know what a Def is (CLAUDE.md rule 4).
    ///
    /// <para><b>Why this exists at all.</b> The owner's 2026-09-06 ruling is that a hooked bass jumps
    /// during the FIGHT the way a free one jumps in the shoal, and that a cod never does. The shoal
    /// drawer lives in Fishing and reads <c>FishSpeciesDef.BehaviorFlags</c> directly; the fight drawer
    /// (<c>RodFightPresenter</c>) lives in Player, which references Core, Boats, Economy and Art and
    /// deliberately not Fishing. Adding a module reference would have made the two drawers agree by
    /// coupling rather than by contract — and an asmdef reference is not a seam. This is the seam.</para>
    ///
    /// <para><b>Facts, not decisions.</b> Everything here is a static property of the species, so both
    /// drawers can schedule their own beat from the same truth. Nothing here says WHEN a fish jumps —
    /// that is each presenter's own deterministic schedule off its own seed (rule 5), and the two are
    /// deliberately not synchronised: a fish on the line and a fish in a shoal are not the same fish.</para>
    ///
    /// <para><b>Never null at the call site.</b> Read it through <c>GameServices.FishBehaviour</c>, which
    /// substitutes an empty implementation when nothing has registered — a scene with no species library
    /// draws a fish that never jumps rather than throwing at the top of a fight.</para>
    /// FLAG lead-architect: new Core contract (the fish-behaviour seam for the visible-fish arc).
    /// </summary>
    public interface IFishBehaviourFacts
    {
        /// <summary>
        /// Does this species CLEAR THE WATER — the owner's ruling made per species (bass, mackerel and
        /// herring do; cod, haddock, pollock and flounder never). False for an id nothing knows, which
        /// is the safe direction: a fish that has not been authored as a jumper does not spontaneously
        /// start leaving the water.
        /// </summary>
        bool Jumps(string fishId);

        /// <summary>
        /// How often a hooked fish of this species tries a jump while she is at the surface, in seconds.
        /// <b>A non-positive return means "unstated"</b> and the presenter uses its own authored
        /// fallback — the same posture every other opt-in Def field takes, so a species written before
        /// the field existed keeps behaving exactly as it did.
        /// </summary>
        float FightJumpPeriodSeconds(string fishId);
    }

    /// <summary>The "nobody has registered any species" answer: nothing jumps, nothing states a period.
    /// A real implementation is published by the Fishing module's species registrar at boot.</summary>
    public sealed class EmptyFishBehaviourFacts : IFishBehaviourFacts
    {
        public static readonly EmptyFishBehaviourFacts Instance = new EmptyFishBehaviourFacts();
        private EmptyFishBehaviourFacts() { }

        public bool Jumps(string fishId) => false;
        public float FightJumpPeriodSeconds(string fishId) => 0f;
    }
}
