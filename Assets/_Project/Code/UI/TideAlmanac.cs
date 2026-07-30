using System;
using System.Collections.Generic;

namespace HiddenHarbours.UI
{
    /// <summary>One high or low water: when it falls, how high it stands, and which it is.</summary>
    public readonly struct TideTurn
    {
        /// <summary>Master-clock time of the turn, in in-game seconds.</summary>
        public readonly double Seconds;

        /// <summary>Water level at the turn, metres relative to chart datum.</summary>
        public readonly float HeightMeters;

        /// <summary>True for a high water, false for a low.</summary>
        public readonly bool IsHigh;

        public TideTurn(double seconds, float heightMeters, bool isHigh)
        {
            Seconds = seconds;
            HeightMeters = heightMeters;
            IsHigh = isHigh;
        }
    }

    /// <summary>
    /// The tide table's maths: every high and low water across a span of days, plus the small
    /// calendar helpers a page needs to sort them into days and clock times.
    ///
    /// <para><b>It does not fork the tide.</b> Turns are located by walking
    /// <see cref="TideReadout.Derive"/> — the same rising/turn logic the always-on HUD gauge uses —
    /// over a height function the caller supplies. In game that function is
    /// <c>IEnvironmentService.TideHeightAt</c>, so the page, the HUD and the editor Tide Table window
    /// are all reading one source and can never disagree. Nothing here is stored: a page is derived
    /// from <c>(the active region's profile, gameTime)</c> every time it is opened (rule 5).</para>
    ///
    /// <para>Pure, engine-free and allocation-conscious (results go into a caller-owned list), so it
    /// is EditMode-testable against a synthetic sine whose turning points are known analytically.
    /// This is the shared half of VS-06: <c>TidePanel</c> draws it for the player,
    /// <c>TideTableWindow</c> draws it for the owner.</para>
    /// </summary>
    public static class TideAlmanac
    {
        /// <summary>
        /// Collect every high/low water in <c>[windowStartSeconds, windowEndSeconds)</c> into
        /// <paramref name="results"/> (cleared first), in ascending time order.
        /// </summary>
        /// <param name="heightAt">Water level (m rel. datum) as a function of in-game seconds.</param>
        /// <param name="windowStartSeconds">First instant of the page (typically the top of today).</param>
        /// <param name="windowEndSeconds">Exclusive end of the page.</param>
        /// <param name="slopeDtSeconds">Forward finite-difference step for the rising test. Mirror
        /// <c>TideModel</c>: <c>SecondsPerHour × TideTableSettings.SlopeStepHours</c>. Must be &gt; 0.</param>
        /// <param name="scanStepSeconds">Forward scan granularity when hunting a turn. Must be &gt; 0.</param>
        /// <param name="horizonSeconds">How far past each probe to look for the next turn — one tidal
        /// period is ample, since a turn always falls within half a period.</param>
        /// <param name="maxTurns">Runaway guard for a degenerate (flat) profile that never turns.</param>
        /// <param name="results">Caller-owned list, reused across pages so a redraw allocates nothing.</param>
        public static void FindTurns(Func<double, float> heightAt,
                                     double windowStartSeconds, double windowEndSeconds,
                                     double slopeDtSeconds, double scanStepSeconds, double horizonSeconds,
                                     int maxTurns, List<TideTurn> results)
        {
            if (heightAt == null) throw new ArgumentNullException(nameof(heightAt));
            if (results == null) throw new ArgumentNullException(nameof(results));

            results.Clear();
            if (slopeDtSeconds <= 0.0 || scanStepSeconds <= 0.0 || horizonSeconds <= 0.0) return;
            if (windowEndSeconds <= windowStartSeconds) return;

            double t = windowStartSeconds;
            int guard = 0;
            while (t < windowEndSeconds && guard++ < maxTurns)
            {
                TideState st = TideReadout.Derive(heightAt, t, slopeDtSeconds, scanStepSeconds, horizonSeconds);
                if (!st.HasTurn) break;                  // no turn within a period — a flat profile

                double turnT = t + st.SecondsToTurn;
                if (turnT >= windowEndSeconds) break;

                // Rising INTO the turn ⇒ it is a high water; falling into it ⇒ a low.
                results.Add(new TideTurn(turnT, heightAt(turnT), st.Rising));

                // Step just past the turn so the next probe is unambiguously on the new slope.
                t = turnT + slopeDtSeconds * 4.0;
            }
        }

        /// <summary>Absolute 0-based in-game day index containing <paramref name="seconds"/>.</summary>
        public static int DayIndexOf(double seconds, double secondsPerDay)
            => secondsPerDay > 0.0 ? (int)Math.Floor(seconds / secondsPerDay) : 0;

        /// <summary>Master-clock time at the top of the day containing <paramref name="seconds"/>.</summary>
        public static double DayStartSeconds(double seconds, double secondsPerDay)
            => secondsPerDay > 0.0 ? Math.Floor(seconds / secondsPerDay) * secondsPerDay : 0.0;

        /// <summary>Hour-of-day in [0,24) for a master-clock value — what the page prints as a time.</summary>
        public static float HourOfDayAt(double seconds, double secondsPerDay)
        {
            if (secondsPerDay <= 0.0) return 0f;
            double d = seconds / secondsPerDay;
            return (float)((d - Math.Floor(d)) * 24.0);
        }
    }
}
