namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The player started or stopped washing</b> (cleanliness slice 1): at a station or in the sea. Raised
    /// by <see cref="ICleanlinessService"/> once per edge, never per frame. Nothing listens in slice 1; it is
    /// the hook for the shower/sink/bath water loop (audio), steam or splash (art), and the wash pose
    /// (character). Additive, in its own file (coordination.md section 1).
    ///
    /// <para>Becoming stinky (or clean again) is the needs' own <see cref="NeedStageChanged"/> with
    /// <see cref="NeedKind.Dirtiness"/>: that is the signal an NPC bark, a step-back, flies or stink lines
    /// hang on.</para>
    /// </summary>
    public readonly struct PlayerWashChanged
    {
        /// <summary>True when washing began, false when it ended.</summary>
        public readonly bool Washing;

        /// <summary>What kind of station (the sea is <see cref="WashStationKind.Sea"/>).</summary>
        public readonly WashStationKind Kind;

        /// <summary>The station's <see cref="WashStationDef.Id"/>.</summary>
        public readonly string StationId;

        public PlayerWashChanged(bool washing, WashStationKind kind, string stationId)
        {
            Washing = washing;
            Kind = kind;
            StationId = stationId ?? "";
        }
    }
}
