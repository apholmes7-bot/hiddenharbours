using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Owner tunables for the tide table — the almanac page the player reads to plan a crossing
    /// (VS-06). Named and surfaced on <c>GameConfig</c> rather than buried in the panel (rule 6): how
    /// far ahead the page looks, and how finely the turn-finder hunts each high and low water.
    ///
    /// <para><b>Why it lives in Core.</b> Both readers of the tide table share it — the in-game
    /// <c>TidePanel</c> and the editor Tide Table window — and neither may reference the other. The
    /// same split already used for <see cref="SeakeepingSettings"/>: the tunable struct sits on the
    /// Core <c>GameConfig</c> the owner edits, while the maths that consumes it lives with its
    /// consumer (<c>HiddenHarbours.UI.TideAlmanac</c>).</para>
    ///
    /// <para>The two step sizes are expressed as <b>fractions of an in-game hour</b> rather than raw
    /// seconds, so a change to <c>SecondsPerDay</c> (a 20-minute day becoming a 30-minute day) rescales
    /// them automatically instead of silently changing the table's precision. <see cref="Default"/>
    /// reproduces the values the HUD and the editor tool have always used, so the shipped page reads
    /// exactly as it did before these became tunable.</para>
    /// </summary>
    [Serializable]
    public struct TideTableSettings
    {
        [Tooltip("How many in-game days the page covers, counting from the top of TODAY. 2 = today and " +
                 "tomorrow — the span a crossing is actually planned over. Larger prints more of the " +
                 "future at the cost of a longer page.")]
        [Min(1)] public int WindowDays;

        [Tooltip("Forward finite-difference step used to decide rising-vs-falling, as a fraction of an " +
                 "in-game HOUR. 0.05 = ~3 in-game minutes, mirroring TideModel.IsRising — keep it there " +
                 "unless the tide model's own test changes, or the table and the HUD gauge can disagree " +
                 "about which way the water is going.")]
        [Range(0.001f, 0.5f)] public float SlopeStepHours;

        [Tooltip("Granularity of the forward scan that hunts the next turn, as a fraction of an in-game " +
                 "HOUR. 0.10 = ~6 in-game minutes. Smaller brackets each high/low more tightly before " +
                 "the bisection refines it, at the cost of more samples per page. The page is built once " +
                 "on open (time is frozen while it is read), so this is not a per-frame cost.")]
        [Range(0.001f, 1f)] public float ScanStepHours;

        [Tooltip("Safety cap on how many turns one page will list — a runaway guard for a degenerate " +
                 "tide profile (a flat or zero-amplitude region) where no turn is ever found. A real " +
                 "semidiurnal day has ~4, so 64 is far above any honest page.")]
        [Min(1)] public int MaxTurns;

        /// <summary>
        /// The shipped tuning. <c>SlopeStepHours</c>/<c>ScanStepHours</c> are exactly the constants the
        /// HUD's tide read and the editor Tide Table window already hard-coded (<c>SecondsPerHour ×
        /// 0.05</c> and <c>× 0.10</c>), so promoting them to tunables changes no displayed value.
        /// </summary>
        public static TideTableSettings Default => new TideTableSettings
        {
            WindowDays = 2,
            SlopeStepHours = 0.05f,
            ScanStepHours = 0.10f,
            MaxTurns = 64,
        };
    }
}
