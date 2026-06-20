namespace HiddenHarbours.Core
{
    /// <summary>
    /// The canon sea-state scale, calm to storm (docs/vision-and-pillars.md §5.8).
    /// Ordered: numeric value rises with severity, so comparisons work
    /// (e.g. <c>if (sample.SeaState &gt;= SeaState.Gale) ...</c>).
    /// </summary>
    public enum SeaState
    {
        Glass = 0,    // mirror-flat
        Calm,
        Light,
        Moderate,
        Lively,
        Rough,
        Gale,
        Storm         // do not be out in this
    }
}
