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

        /// <summary>True when this wash uses soap (owner ask 2026-11-17): the hook for a lather or froth look
        /// and a scrubbing sound. Same value on the "began" and the "ended" edge of one wash.</summary>
        public readonly bool Soaped;

        /// <summary>True when soap would have taken this wash further and the player had none (a shower or a
        /// bath without soap stops short of fully clean). Never true together with <see cref="Soaped"/>.</summary>
        public readonly bool SoapMissing;

        public PlayerWashChanged(bool washing, WashStationKind kind, string stationId)
            : this(washing, kind, stationId, false, false) { }

        public PlayerWashChanged(bool washing, WashStationKind kind, string stationId, bool soaped, bool soapMissing)
        {
            Washing = washing;
            Kind = kind;
            StationId = stationId ?? "";
            Soaped = soaped;
            SoapMissing = soapMissing && !soaped;
        }
    }
}
