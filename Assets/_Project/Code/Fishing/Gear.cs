namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// Fishing gear. A flags enum so a species can be catchable by several methods, and a boat can
    /// carry several. The player fishes with one selected gear at a time. (design/fish-and-content.md)
    /// </summary>
    [System.Flags]
    public enum Gear
    {
        None     = 0,
        Handline = 1 << 0,
        Longline = 1 << 1,
        Net      = 1 << 2,
        Trap     = 1 << 3,
        Dredge   = 1 << 4,
        Jig      = 1 << 5,

        /// <summary>
        /// Hand-dig with a clam shovel/fork on the bared flats (St Peters opening). The dedicated tag
        /// economy flagged for review (fish-and-content §3.5a): the soft-shell clam was authored on
        /// <see cref="Handline"/> as a STOPGAP so it had *some* gear, but a clam isn't hooked on a line —
        /// it's dug by hand. This is the additive, append-only reconciliation (a new flag bit; no existing
        /// value renumbered, so no other asset shifts). The clam is re-tagged to this; it keeps the clam
        /// OUT of the rod's resolver pool (the rod fishes Handline/Jig), which is correct — you can't reel
        /// up a clam. The on-foot dig (<c>ClamDig</c>) is what yields it. FLAG: append-only enum change,
        /// re-tagged Data/Fish/SoftShellClam.asset off the Handline stopgap.
        /// </summary>
        ClamFork = 1 << 6
    }

    /// <summary>Which seasons a species bites in. Flags so a fish can span several.</summary>
    [System.Flags]
    public enum SeasonMask
    {
        None        = 0,
        EarlySpring = 1 << 0,
        HighSummer  = 1 << 1,
        TheTurn     = 1 << 2,
        HardWinter  = 1 << 3,
        AllYear     = EarlySpring | HighSummer | TheTurn | HardWinter
    }
}
