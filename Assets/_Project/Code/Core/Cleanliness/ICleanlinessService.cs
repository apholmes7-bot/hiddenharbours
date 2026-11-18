namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Cleanliness, as a seam</b> (cleanliness slice 1, owner ask 2026-11-15). The local player's dirt,
    /// worked out from the save and the game clock, and the three things that change it: work (a stretch,
    /// or a single action), the sea, and washing at a station. It is the THIRD need: the needs service
    /// reads <see cref="Dirt"/> as <see cref="NeedKind.Dirtiness"/>, so stinky joins hunger and tiredness in
    /// the one walk/price factor (<see cref="CharacterFactors"/>) and the one reply seam
    /// (<see cref="NeedsNotice"/>), and its stage crossings are the needs' own
    /// <see cref="NeedStageChanged"/>.
    ///
    /// <para><b>Never shown as a number.</b> No bar, meter or HUD element (owner ruling 2026-11-14): the
    /// player reads it from how villagers react and, later, from flies and stink lines on the character.</para>
    ///
    /// <para><b>Who calls it.</b> <see cref="CleanlinessResponder"/> turns the signals already on the bus into
    /// calls (a fish landed, a pot haul, swimming). A wash station's interaction calls
    /// <see cref="BeginWash"/>/<see cref="EndWash"/>; the body lane's activity entry point (a gym machine,
    /// engine work) calls <see cref="BeginActivity"/>/<see cref="EndActivity"/>.</para>
    /// </summary>
    public interface ICleanlinessService
    {
        /// <summary>The local player's dirt now, 0 clean .. 1 filthy (0 with no save).</summary>
        float Dirt { get; }

        /// <summary>The stretch of work going on (<see cref="DirtActivity.None"/> when none or washing).</summary>
        DirtActivity Activity { get; }

        /// <summary>The station being washed at, or null.</summary>
        WashStationDef WashingAt { get; }

        /// <summary>True while the wash going on uses soap (decided, and the soap used, when it began).</summary>
        bool WashSoaped { get; }

        /// <summary>True when the player has soap for at least one more wash (false with the soap rule off).</summary>
        bool HasSoap { get; }

        /// <summary>True when soap changes how clean <paramref name="station"/> can get the player (a shower
        /// or a bath; never the sink or the sea; never with the soap rule off).</summary>
        bool SoapMatters(WashStationDef station);

        /// <summary>
        /// How clean (0..1) washing at <paramref name="station"/> can get the player: for the wash going on
        /// there, what it was begun with; otherwise what a wash begun now would reach, with the soap held now.
        /// A station's own ceiling, or <see cref="CleanlinessSettings.UnsoapedCeiling"/> without soap.
        /// </summary>
        float CeilingFor(WashStationDef station);

        /// <summary>A stretch of <paramref name="kind"/> begins now (replaces any wash or other stretch).
        /// Re-beginning the same kind changes nothing. No save does nothing.</summary>
        void BeginActivity(DirtActivity kind);

        /// <summary>The stretch of <paramref name="kind"/> ends now; ignored if another is going on.</summary>
        void EndActivity(DirtActivity kind);

        /// <summary>One action of <paramref name="kind"/> (a fish landed): its one-off dirt, at once.</summary>
        void ReportAction(DirtActivity kind);

        /// <summary>Bank a bounded activity duration at the clock now, using only its hourly dirt rate.
        /// Does not open/replace a stretch or add a per-action bump. Do not also count this duration
        /// through BeginActivity. Non-finite/non-positive hours do nothing. Older implementations bank nothing.</summary>
        void ReportActivity(DirtActivity kind, double gameHours) { }

        /// <summary>Start washing at <paramref name="station"/> now, wherever it stands (a station is a kind
        /// plus an anchor). A toilet or a null station does nothing. Where soap matters and the player has
        /// some, one wash of soap is used and the wash can reach the station's own ceiling; without, it stops
        /// at the unsoaped cap. Publishes <see cref="PlayerWashChanged"/> (with its soap flags).</summary>
        void BeginWash(WashStationDef station);

        /// <summary>Stop washing now (no-op when not washing). Publishes <see cref="PlayerWashChanged"/>.</summary>
        void EndWash();

        /// <summary>The shipped station with <paramref name="id"/>, or null.</summary>
        WashStationDef FindStation(string id);

        /// <summary>Re-anchor the saved state at the clock now (a save write). No level changes.</summary>
        void Settle();

        /// <summary>The slow tick (from <see cref="INeedsService.Tick"/>): anchors a fresh game to the clock
        /// and closes a stretch that has run past its guard window. Never changes a level.</summary>
        void Tick();
    }
}
