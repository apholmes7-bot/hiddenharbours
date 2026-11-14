using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Character traits, as a seam</b> (traits slice 1, owner ruling 2026-11-14, rule 4). A feature
    /// module asks one question, "what is the combined factor for MY stat for this character?", and never
    /// learns which traits exist or which module owns the others. The walk controller reads
    /// <see cref="TraitStat.WalkSpeed"/>, the till reads <see cref="TraitStat.SellPrice"/>, the rod reads
    /// <see cref="TraitStat.BiteWait"/>; none of them references another.
    ///
    /// <para><b>Per character.</b> <see cref="MultiplierFor"/> takes any character's trait ids, so a co-op
    /// player's own list answers for that player; <see cref="PlayerMultiplier"/> is the local player's,
    /// read from <see cref="SaveData.PlayerTraits"/>. A trait only moves what its own player does.</para>
    ///
    /// <para><b>Deterministic.</b> A fixed factor from the save and the shipped Defs: no roll, no clock.
    /// An unknown id (a retired trait, a hand-edited save) is ignored, never a crash.</para>
    /// </summary>
    public interface ITraitService
    {
        /// <summary>Every shipped trait, ordinal by id. Empty when none ship.</summary>
        IReadOnlyList<TraitDef> All { get; }

        /// <summary>The trait with <paramref name="id"/>, or null.</summary>
        TraitDef Find(string id);

        /// <summary>The local player's trait ids as saved (pick order). Empty with no save or no traits.</summary>
        IReadOnlyList<string> PlayerTraitIds { get; }

        /// <summary>The local player's combined factor for <paramref name="stat"/>; exactly 1 with no
        /// traits. Cheap and allocation-free on the hot path (the walk reads it every physics tick).</summary>
        float PlayerMultiplier(TraitStat stat);

        /// <summary>The combined factor for <paramref name="stat"/> for a character holding
        /// <paramref name="traitIds"/>: unknown ids and repeats are ignored, at most
        /// <see cref="GameServices.TraitPicksPerCharacter"/> are counted, combined in id order.</summary>
        float MultiplierFor(TraitStat stat, IReadOnlyList<string> traitIds);

        /// <summary>Keep <paramref name="traitIds"/> as the local player's traits, persist and announce
        /// them (<see cref="PlayerTraitsChanged"/>) only when they differ from what the save holds. False,
        /// with the refusal, for an unknown id, a repeat, more than the allowed picks, or no save; nothing
        /// is written then. An empty list is allowed (no traits).</summary>
        bool SetPlayerTraits(IReadOnlyList<string> traitIds, out string refusal);
    }

    /// <summary>The local player's traits changed (<see cref="ITraitService.SetPlayerTraits"/>). No
    /// payload: read them back through <see cref="GameServices.Traits"/>.</summary>
    public readonly struct PlayerTraitsChanged
    {
    }
}
