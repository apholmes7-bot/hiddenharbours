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
        public const string Settings = "Settings";
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

        // ---- the pause menu ------------------------------------------------------------------
        // Mid-session, over a stopped world. It says where you are and offers the four things a tester
        // needs at 11pm: carry on, change the volume, go back to the title, stop playing.

        public const string PauseTitle     = "Ashore for a moment";
        public const string Resume         = "Back to it";
        public const string QuitToTitle    = "Quit to title";
        public const string QuitToDesktop  = "Quit to desktop";
        public const string PauseHint      = "Esc to carry on   ·   the tide is stopped while you're here";

        /// <summary>Said under the two Quit lines, because both of them save and a player should know
        /// that before they pick one.</summary>
        public const string QuitSavesFirst = "Your harbour is saved either way.";

        // ---- settings ------------------------------------------------------------------------
        // Four independent faders (the M1 DoD's promise, which had no player-facing surface until now)
        // and the one display choice M1 offers. Graphics presets and key rebinding are deliberately not M1.

        public const string SettingsTitle    = "Settings";
        public const string VolumeMaster     = "Everything";
        public const string VolumeAmbience   = "Sea and weather";
        public const string VolumeSfx        = "Effects";
        public const string VolumeMusic      = "Music";
        public const string DisplayLabel     = "Window";
        public const string DisplayFullscreen = "Fullscreen";
        public const string DisplayWindowed   = "Windowed";
        public const string Back             = "Back";
        public const string SettingsHint     = "← → to set a level   ·   Esc to go back";

        /// <summary>Shown in place of the faders when no audio director is running (EditMode, a stripped
        /// build). An honest "there is nothing to move here" beats four sliders that do nothing.</summary>
        public const string AudioUnavailable = "No sound running to set.";

        // ---- composed lines ------------------------------------------------------------------

        /// <summary>
        /// The one-line description of the game waiting behind Continue — enough for a player (and a
        /// playtester filing a bug) to know which game they are about to walk back into. Day is 1-based
        /// for reading; the save counts from 0.
        /// </summary>
        public static string SavedGameSummary(int dayIndex, int money)
            => "Day " + (dayIndex + 1).ToString(CultureInfo.InvariantCulture)
             + "   ·   " + HudStrings.Currency + money.ToString(CultureInfo.InvariantCulture);

        /// <summary>A fader's level as a whole percent. Called only when that whole number changes, never
        /// per frame of a drag.</summary>
        public static string VolumePercent(float value01)
            => System.Math.Round(value01 * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";

        /// <summary>The word for the current display mode — the value half of the Window row.</summary>
        public static string DisplayMode(bool fullscreen)
            => fullscreen ? DisplayFullscreen : DisplayWindowed;
    }
}
