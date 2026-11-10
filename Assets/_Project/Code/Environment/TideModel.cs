using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.Environment
{
    /// <summary>
    /// Pure, deterministic tide math (no Unity, no state) so it is trivially unit-testable and
    /// reproducible from time alone. Semidiurnal (two highs/two lows per tidal day) with a
    /// spring/neap envelope on the lunar month. See design/time-tides-weather.md.
    /// </summary>
    public static class TideModel
    {
        private const double TwoPi = Math.PI * 2.0;

        /// <summary>Where in its own period the carrier <c>sin</c> peaks: a quarter of the way round.
        /// High water is the carrier's peak, so a tide cycle is counted from quarter-turn to
        /// quarter-turn. A property of the sine, not a tunable.</summary>
        private const double HighWaterTurn = 0.25;

        /// <summary>Water level in metres relative to chart datum at <paramref name="totalSeconds"/>.</summary>
        public static float Height(double totalSeconds, TideProfile profile, GameConfig cfg)
        {
            double secondsPerHour = cfg.SecondsPerDay / 24.0;
            double tHours = totalSeconds / secondsPerHour;

            // Semidiurnal carrier.
            double carrier = Math.Sin(TwoPi * (tHours + profile.PhaseHours) / cfg.TidalPeriodHours);

            // Spring/neap envelope: spring tides occur twice per lunar month, so the envelope
            // period is half the lunar month. env in [0,1], 1 at spring, 0 at neap.
            double lunarHours = cfg.LunarMonthDays * 24.0;
            double env = 0.5 + 0.5 * Math.Cos(TwoPi * tHours / (lunarHours / 2.0));

            double amp = profile.Amplitude *
                         (cfg.NeapAmplitudeFraction + (1.0 - cfg.NeapAmplitudeFraction) * env);

            return (float)(profile.MeanLevel + amp * carrier);
        }

        /// <summary>
        /// Which tide <paramref name="totalSeconds"/> belongs to: an index that steps by exactly one at
        /// each high water of the semidiurnal carrier <see cref="Height"/> uses (same phase, same
        /// period), and is constant from one high water to the next. Low water sits in the middle of a
        /// cycle, so everything dug on one ebb shares an index and the next flood turns it.
        ///
        /// <para>Pure and allocation-free, like <see cref="Height"/>: a function of the clock, the
        /// region's <see cref="TideProfile.PhaseHours"/> and the config's period, never stored (rule 5).
        /// The spring/neap envelope scales the range, not the timing, so it does not enter here.
        /// A config with no usable period or day length reads as one endless tide (index 0).</para>
        /// </summary>
        public static long CycleIndex(double totalSeconds, TideProfile profile, GameConfig cfg)
        {
            if (cfg == null || !(cfg.TidalPeriodHours > 0f) || !(cfg.SecondsPerDay > 0f)) return 0L;

            double secondsPerHour = cfg.SecondsPerDay / 24.0;
            double tHours = totalSeconds / secondsPerHour;
            double turns = (tHours + profile.PhaseHours) / cfg.TidalPeriodHours;
            return (long)Math.Floor(turns - HighWaterTurn);
        }

        /// <summary>True if the tide is making (rising) at this instant.</summary>
        public static bool IsRising(double totalSeconds, TideProfile profile, GameConfig cfg)
        {
            double dt = (cfg.SecondsPerDay / 24.0) * 0.05; // ~3 in-game minutes
            return Height(totalSeconds + dt, profile, cfg) > Height(totalSeconds, profile, cfg);
        }

        /// <summary>Rate of change of water level, metres per in-game second (signed).</summary>
        public static float Rate(double totalSeconds, TideProfile profile, GameConfig cfg)
        {
            double dt = (cfg.SecondsPerDay / 24.0) * 0.05;
            return (float)((Height(totalSeconds + dt, profile, cfg) - Height(totalSeconds, profile, cfg)) / dt);
        }
    }
}
