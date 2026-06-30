using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, deterministic MOON math (no Unity scene, no state) for the water's living-moon reflection.
    /// Everything is a pure function of the master clock (in-game seconds) + the lunar period, so it is
    /// trivially unit-testable and reproducible from time alone (CLAUDE.md rule 5 — saves nothing, adds no
    /// hidden randomness). <see cref="MoonCycle"/> evaluates these each throttled tick and publishes the
    /// result as shader globals; the water shader positions + shapes the reflected moon from them.
    ///
    /// <para><b>Three things the moon does</b> (the owner wants it ALIVE):
    /// <list type="number">
    /// <item><b>It MOVES across the night</b> — <see cref="MoonArc"/> rises on one side of the night, arcs
    ///   across, and sets on the other (a deterministic function of the day fraction), so the reflected
    ///   disc + glitter path travel over the course of the night.</item>
    /// <item><b>It has PHASES</b> — <see cref="Phase01"/> cycles new → crescent → quarter → gibbous → full →
    ///   waning over the ~28-day lunar month; <see cref="TerminatorSigned"/> turns that into a signed
    ///   crescent/gibbous terminator the shader masks the disc with.</item>
    /// <item><b>It is TIED TO THE TIDES</b> — the phase derives from the SAME lunar period that drives the
    ///   tide's spring/neap envelope (TideModel: <c>env = 0.5 + 0.5·cos(2π·tHours / (lunarHours/2))</c>),
    ///   so FULL MOON lands on a SPRING tide and NEW MOON on the other spring, exactly as in nature
    ///   (vision-and-pillars §5.5). See <see cref="Phase01"/> for the alignment proof.</item>
    /// </list></para>
    ///
    /// Angles/directions are returned in the water's ground-plane convention (xy), matching
    /// <c>_SunDir</c>/<c>_WindWorld</c>: a unit direction the shader uses to place the reflected moon.
    /// </summary>
    public static class MoonMath
    {
        private const double TwoPi = Math.PI * 2.0;

        /// <summary>
        /// The moon's phase through the lunar month, in <c>[0,1)</c>: <c>0</c> = NEW moon, <c>0.5</c> = FULL
        /// moon, wrapping back to new at <c>1</c>. Derived from the SAME lunar period the tide's spring/neap
        /// envelope uses, so the visible phase agrees with the tides.
        ///
        /// <para><b>Tide alignment (the proof).</b> TideModel's envelope is
        /// <c>env = 0.5 + 0.5·cos(2π·tHours / (lunarHours/2))</c>, which is at SPRING (env = 1) whenever
        /// <c>tHours / (lunarHours/2)</c> is an integer — i.e. twice per lunar month. With
        /// <c>phase01 = frac(tHours / lunarHours)</c>: at <c>phase01 = 0</c> (new) the envelope arg is 0 →
        /// env = 1 (spring); at <c>phase01 = 0.5</c> (full) the arg is 2π·(½·lunarHours)/(½·lunarHours) = 2π →
        /// cos(2π) = 1 → env = 1 (spring). So full moon ↔ spring tide and new moon ↔ the other spring, by
        /// construction — no separate moon clock to drift out of sync.</para>
        /// </summary>
        /// <param name="totalSeconds">The master clock (in-game seconds since new game; <c>IGameClock.TotalSeconds</c>).</param>
        /// <param name="lunarMonthDays">The lunar month in in-game days (GameConfig.LunarMonthDays; canon 28).</param>
        /// <param name="secondsPerDay">In-game seconds per day (GameConfig.SecondsPerDay; default 1200).</param>
        /// <param name="phaseOffsetDays">Optional offset (days) to set which day the game STARTS on in the
        /// cycle, so a new game can begin on, say, a half moon. 0 = start on a new moon.</param>
        public static float Phase01(double totalSeconds, float lunarMonthDays, float secondsPerDay,
                                    float phaseOffsetDays = 0f)
        {
            double lunarSeconds = Math.Max(lunarMonthDays, 1e-3) * Math.Max(secondsPerDay, 1e-3);
            double offsetSeconds = phaseOffsetDays * Math.Max(secondsPerDay, 1e-3);
            double frac = (totalSeconds + offsetSeconds) / lunarSeconds;
            frac -= Math.Floor(frac);               // wrap to [0,1)
            return (float)frac;
        }

        /// <summary>
        /// The moon's ILLUMINATED FRACTION (0..1) for a phase: <c>0</c> at new moon (<c>phase01 = 0</c>),
        /// rising to <c>1</c> at full moon (<c>phase01 = 0.5</c>), falling back to 0 at the next new moon.
        /// A simple raised cosine — smooth, symmetric, and monotonic on each half — used to DIM the moon's
        /// reflected light (a thin crescent gives far less glitter than a full moon).
        /// </summary>
        public static float IlluminatedFraction(float phase01)
        {
            float p = Mathf.Repeat(phase01, 1f);
            // 0 at p=0 (new), 1 at p=0.5 (full): (1 - cos(2π·p)) / 2.
            return 0.5f * (1f - Mathf.Cos(TwoPiF * p));
        }

        /// <summary>
        /// A SIGNED terminator value the shader uses to mask the reflected disc into a crescent/gibbous shape.
        /// <list type="bullet">
        /// <item><c>0</c> at new moon and full moon (a full terminator sweep — at full the whole disc is lit,
        ///   at new none of it is; the magnitude encodes how far the terminator has crossed the disc).</item>
        /// <item><b>sign</b> flips between the WAXING half (0..0.5) and the WANING half (0.5..1), so the
        ///   crescent appears on the correct side as the month turns.</item>
        /// </list>
        /// Concretely: <c>cos(2π·phase01)</c> — <c>+1</c> at new (terminator fully on the dark side → almost
        /// no disc), <c>0</c> at the quarters (half disc), <c>-1</c> at full (terminator gone → full disc).
        /// The shader compares this against the disc's along-axis coordinate to carve the lit crescent.
        /// </summary>
        public static float TerminatorSigned(float phase01)
        {
            float p = Mathf.Repeat(phase01, 1f);
            return Mathf.Cos(TwoPiF * p);
        }

        /// <summary>
        /// Where the moon is in its nightly ARC. The moon rises near dusk, climbs to its highest near the
        /// middle of the night, and sets near dawn (a simplified always-up-at-night model so there is a moon
        /// to navigate by; the per-night presence can be dimmed by phase separately). Returns:
        /// <list type="bullet">
        /// <item><b>dir</b> — a unit ground-plane direction (xy) sweeping from the EAST horizon at moonrise,
        ///   through overhead, to the WEST horizon at moonset, so the reflected disc travels across the water.</item>
        /// <item><b>aboveHorizon</b> — 0 below the horizon (day / not yet risen / already set), rising to 1 at
        ///   the peak of the arc; the caller fades the moon out as it dips so it doesn't pop at the edges.</item>
        /// </list>
        /// Deterministic in the day fraction; no randomness.
        /// </summary>
        /// <param name="dayFraction">0..1 through the current day (<c>IGameClock.DayFraction</c>).</param>
        /// <param name="moonriseFraction">Day fraction the moon rises (default 0.78 ≈ dusk).</param>
        /// <param name="moonsetFraction">Day fraction the moon sets (default 0.30 ≈ after dawn; wraps midnight).</param>
        public static void MoonArc(float dayFraction, out Vector2 dir, out float aboveHorizon,
                                   float moonriseFraction = 0.78f, float moonsetFraction = 0.30f)
        {
            // Map the night span (moonrise → moonset, wrapping past midnight) to a 0..1 progress along the arc.
            float night = NightProgress(dayFraction, moonriseFraction, moonsetFraction);
            if (night <= 0f || night >= 1f)
            {
                // Outside the moon's up-window: park it below the horizon, pointing at the rise side (east).
                dir = new Vector2(1f, 0f);
                aboveHorizon = 0f;
                return;
            }

            // Arc the AZIMUTH from east (+X) at rise, through overhead, to west (−X) at set. We sweep the
            // ground direction across a half-circle so the reflected moon glides across the water.
            float az = Mathf.Lerp(0f, Mathf.PI, night);          // 0 = east, π = west
            // East→up→west: x = cos(az) (east +1 → west −1), y = sin(az) (0 at horizons, 1 overhead).
            dir = new Vector2(Mathf.Cos(az), Mathf.Sin(az));
            if (dir.sqrMagnitude < 1e-8f) dir = new Vector2(1f, 0f);
            else dir = dir.normalized;

            // Height above the horizon: 0 at rise/set, 1 at the middle of the night (a smooth sine hump).
            aboveHorizon = Mathf.Clamp01(Mathf.Sin(night * Mathf.PI));
        }

        /// <summary>
        /// Progress 0..1 through the moon's up-window (moonrise → moonset), correctly WRAPPING past midnight
        /// when <paramref name="riseFraction"/> &gt; <paramref name="setFraction"/> (the moon is up across the
        /// night). Returns 0 when the moon is not up (daytime). Pure; the arc + presence build on it.
        /// </summary>
        public static float NightProgress(float dayFraction, float riseFraction, float setFraction)
        {
            float f = Mathf.Repeat(dayFraction, 1f);
            float rise = Mathf.Repeat(riseFraction, 1f);
            float set = Mathf.Repeat(setFraction, 1f);

            // span of the up-window, wrapping midnight if rise > set.
            float span = set - rise;
            if (span <= 0f) span += 1f;                  // wraps past midnight
            if (span <= 1e-5f) return 0f;

            // position of `f` past rise, wrapped.
            float since = f - rise;
            if (since < 0f) since += 1f;
            if (since > span) return 0f;                 // outside the up-window (daytime)
            return Mathf.Clamp01(since / span);
        }

        private const float TwoPiF = (float)TwoPi;
    }
}
