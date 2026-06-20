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
