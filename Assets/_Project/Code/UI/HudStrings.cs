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
    }
}
