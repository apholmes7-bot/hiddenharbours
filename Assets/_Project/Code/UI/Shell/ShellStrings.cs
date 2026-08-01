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

        // ---- the build stamp -------------------------------------------------------------------
        // M1 §7.8's exit criterion ends "...and can tell you which build they were on". A closed playtest
        // collects reports from several builds at once, and "it broke when I hauled a pot" is worth very
        // little if nobody can say WHICH build broke. So the page says it, quietly, in the corner.

        /// <summary>Marks the number as a version — "v0.1.0", the way a person writes it.</summary>
        public const string VersionPrefix = "v";

        /// <summary>Precedes the build id. The word matters: a tester copying "build 4f9c2a1" into a bug
        /// report has said something unambiguous, where a bare hex string reads like noise.</summary>
        public const string BuildLabel = "build ";

        /// <summary>Said instead of a build id when there is no build — the editor. Honest: an editor
        /// session is not a build, and a bug found in one is not attributable to the same thing.</summary>
        public const string EditorLabel = "editor";

        /// <summary>Marks a Development Build. Worth its four characters: stack traces, the profiler and
        /// the deep-profile costs all differ, so "is it slow?" has a different answer here.</summary>
        public const string DevelopmentLabel = "dev";

        /// <summary>
        /// The corner line: which version, and which build of it. Pure and parameterised so the whole
        /// thing is testable without a build — <see cref="BuildInfo"/> supplies the live values.
        /// An empty <paramref name="buildId"/> means "no build was made", i.e. the editor.
        /// </summary>
        public static string BuildStamp(string version, string buildId, bool development)
        {
            string stamp = string.IsNullOrEmpty(version) ? string.Empty : VersionPrefix + version;

            string which = string.IsNullOrEmpty(buildId) ? EditorLabel : BuildLabel + buildId;
            stamp = stamp.Length == 0 ? which : stamp + Separator + which;

            // Only in a player: Debug.isDebugBuild is true of every editor session, so saying it there
            // would be noise on the one line that exists to say something.
            if (development && !string.IsNullOrEmpty(buildId)) stamp += Separator + DevelopmentLabel;
            return stamp;
        }

        /// <summary>The shell's separator between two small facts on one line — the same spacing the
        /// hints use, so the corner reads in the page's own voice.</summary>
        public const string Separator = "   ·   ";

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
