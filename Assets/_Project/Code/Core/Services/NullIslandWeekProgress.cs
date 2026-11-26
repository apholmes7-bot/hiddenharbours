namespace HiddenHarbours.Core
{
    /// <summary>
    /// The island-week reading when no rules are loaded (a fixture, the shell, a build whose rules
    /// asset is missing or invalid): no fact holds and nothing is listed as missing, so a reader
    /// neither opens anything nor nags about steps it cannot name.
    /// </summary>
    public sealed class NullIslandWeekProgress : IIslandWeekProgress
    {
        public static readonly NullIslandWeekProgress Instance = new NullIslandWeekProgress();

        private NullIslandWeekProgress() { }

        public IslandWeekSnapshot Read(SaveData save, int dayIndex) => default;
    }
}
