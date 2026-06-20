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
        Jig      = 1 << 5
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
