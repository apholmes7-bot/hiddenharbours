using System.Globalization;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Every word the shell says (M1 §7.8) — title page, confirm, pause menu, settings — in one place,
    /// on the same seam as <see cref="HudStrings"/>: there is no runtime localization system wired yet
    /// (a cross-cutting lead-architect call, and a hard M1 DoD line), so centralising the strings HERE is
    /// what makes wiring one a change to this file instead of a change to every call site. Kept separate
    /// from <see cref="HudStrings"/> because the HUD and the shell are different surfaces with different
    /// voices — and because a table split by screen is what a translator actually wants.
    ///
    /// <para><b>The voice.</b> Plain, quiet, coastal. The shell never shouts and never says "Are you
    /// sure?" — it says what will happen and what will be lost, and lets the player choose.</para>
    ///
    /// <para>Allocation-free where it matters: constants for the static words; the two composed lines
    /// build a string, and both are called exactly once when a page is built (never per frame).</para>
    /// </summary>
    public static class ShellStrings
    {
        // ---- the title page ------------------------------------------------------------------

        /// <summary>The wordmark. A place name, not a slogan.</summary>
        public const string GameTitle = "Hidden Harbours";

        public const string Continue = "Continue";
        public const string NewGame  = "New game";
        public const string Quit     = "Quit";

        /// <summary>The tick that marks the line the keyboard/gamepad is on — the shape channel that
        /// carries the selection without relying on the strip's tint (§8).</summary>
        public const string MenuMarker = "▸";

        /// <summary>How to drive the page, said once, quietly, at the foot of it.</summary>
        public const string MenuHint = "↑ ↓ to choose   ·   Enter to pick";

        /// <summary>Shown under Continue on a first launch, where there is nothing to go back to.</summary>
        public const string NoGameYet = "No harbour yet — start one.";

        // ---- the New Game confirm ------------------------------------------------------------
        // The item M1 §7.8 says actually blocks the playtest. One slot, no undo — so the page states the
        // consequence in full and puts the safe answer under the cursor.

        public const string ConfirmNewGameTitle  = "Start again?";
        public const string ConfirmNewGameBody   =
            "There is one harbour, and starting again writes over it. The boat, the money, the licences " +
            "and the day you were on are gone for good.";
        public const string ConfirmNewGameYes    = "Write over it";
        public const string ConfirmNewGameNo     = "Keep my harbour";

        // ---- composed lines ------------------------------------------------------------------

        /// <summary>
        /// The one-line description of the game waiting behind Continue — enough for a player (and a
        /// playtester filing a bug) to know which game they are about to walk back into. Day is 1-based
        /// for reading; the save counts from 0.
        /// </summary>
        public static string SavedGameSummary(int dayIndex, int money)
            => "Day " + (dayIndex + 1).ToString(CultureInfo.InvariantCulture)
             + "   ·   " + HudStrings.Currency + money.ToString(CultureInfo.InvariantCulture);
    }
}
