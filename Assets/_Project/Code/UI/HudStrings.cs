using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The single home for every user-facing string the HUD shows. There is no runtime
    /// localization system wired yet (that is a cross-cutting, lead-architect call — see the M1
    /// DoD "all player-facing strings go through localization tables"). Centralising the strings
    /// here is the seam: when a loc system lands, these accessors route to the loc tables instead
    /// of returning literals, and no call site changes. Flagged as a known DoD gap in the PR.
    ///
    /// Kept allocation-free: returns interned literals / cached constants, never builds strings.
    /// </summary>
    public static class HudStrings
    {
        // Glyphs (shape-based redundant coding, never colour alone — accessibility §8).
        public const string TideRisingArrow  = "▲"; // ▲ filled up-triangle
        public const string TideFallingArrow = "▼"; // ▼ filled down-triangle
        public const string TurnGlyph        = "⤴"; // ⤴ "turns in" marker
        public const string Currency         = "₲"; // ₲ guarani sign — the game's coin

        // Wind-strength barbs — LENGTH encodes strength (colourblind-safe), pairing the kt/Beaufort
        // numbers with a glanceable shape. Marine convention: a full barb ≈ 10 kt, a half ≈ 5 kt.
        public const string WindBarbFull     = "▮"; // ▮ full barb ≈ 10 kt
        public const string WindBarbHalf     = "▪"; // ▪ half barb ≈ 5 kt
        public const string WindCalm         = "○"; // ○ calm (met ring)

        // Placeholders shown before services are ready / when a value is unknown.
        public const string Unknown   = "--";
        public const string Booting   = "";   // empty = HUD shows nothing until services are up

        // Static labels.
        public const string MoneyPrefix = Currency;

        /// <summary>Word for a sea state (redundant text alongside the icon/number).</summary>
        public static string SeaState(SeaState state) => state switch
        {
            Core.SeaState.Glass    => "Glass",
            Core.SeaState.Calm     => "Calm",
            Core.SeaState.Light    => "Light",
            Core.SeaState.Moderate => "Moderate",
            Core.SeaState.Lively   => "Lively",
            Core.SeaState.Rough    => "Rough",
            Core.SeaState.Gale     => "Gale",
            Core.SeaState.Storm    => "Storm",
            _ => Unknown
        };

        /// <summary>Short season label for the clock readout.</summary>
        public static string Season(Season season) => season switch
        {
            Core.Season.EarlySpring => "Early Spring",
            Core.Season.HighSummer  => "High Summer",
            Core.Season.TheTurn     => "The Turn",
            Core.Season.HardWinter  => "Hard Winter",
            _ => Unknown
        };

        /// <summary>Tide arrow glyph for a rising/falling state.</summary>
        public static string TideArrow(bool rising) => rising ? TideRisingArrow : TideFallingArrow;

        // ---- the tide table (VS-06) ---------------------------------------------------------
        // The almanac page's words, on the same loc seam as everything above. The ones that take an
        // argument DO allocate; the page is built once on open (time is frozen while it is read), so
        // none of them are ever called per frame.

        /// <summary>The mark for where "now" falls among the day's turns — a pencil line on the page.</summary>
        public const string TideTableNowMarker = "◀ now";

        public const string TideTableTitle        = "Tide Table";
        public const string TideTableNowLabel     = "Now";
        public const string TideTableClose        = "Close";
        public const string TideTableCloseHint    = "Esc or N to put the table away. Time is stopped while you read.";
        public const string TideTableNoTurnsToday = "(no turn this day)";
        public const string TideTableNoTurn       = "No turn found within a tidal period.";
        public const string TideTableUnavailable  = "No tide to read here yet.";

        /// <summary>"High water" / "Low water", with the shape glyph that carries it without colour (§8).</summary>
        public static string TideTurnLabel(bool high)
            => high ? TideRisingArrow + " High water" : TideFallingArrow + " Low water";

        /// <summary>The word for which way the water is going — "making" floods, "ebbing" falls.</summary>
        public static string TideDirection(bool rising) => rising ? "making (flood)" : "ebbing";

        /// <summary>"Next high water in" / "Next low water in" — the lead-in to a duration.</summary>
        public static string TideTableNextTurn(bool rising)
            => rising ? "Next high water in" : "Next low water in";

        /// <summary>A column heading: "Today" / "Tomorrow", else the absolute day for a wider page.</summary>
        public static string TideTableDayHeading(bool isToday, int dayIndex)
            => isToday ? "Today" : "Tomorrow — day " + dayIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Which place the page is for — a tide table is always local.</summary>
        public static string TideTableForRegion(string regionId) => "for " + regionId;
    }
}
