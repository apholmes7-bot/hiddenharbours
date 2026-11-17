namespace HiddenHarbours.Core
{
    /// <summary>
    /// What kind of kitchen station a <see cref="CookingStationDef"/> is. <b>Append-only</b>: the value is
    /// serialized in station assets, so a new kind takes the next number and none is ever renumbered.
    /// The kind is a label for presentation and placement; what a station can run is its data
    /// (<see cref="CookingStationDef.AcceptedStations"/>, <see cref="CookingStationDef.MicrowavableOnly"/>).
    /// </summary>
    public enum CookingStationKind
    {
        /// <summary>A counter and a board: a raw catch in, a prepared cut out.</summary>
        Prep = 0,
        /// <summary>A stove or galley cooktop: a prepared cut in, a cooked dish out.</summary>
        Stove = 1,
        /// <summary>A microwave: fast, and only for cooked or microwavable food (owner ask 2026-11-15).</summary>
        Microwave = 2,
    }
}
