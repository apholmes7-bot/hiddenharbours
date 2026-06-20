using System;
using System.Globalization;
using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Pure string formatters for the HUD readouts. These DO allocate (they build display strings),
    /// so the HUD only calls them when the underlying displayed value actually changes — a minute
    /// ticked, a height bucket changed, the balance moved. Kept pure (no Unity, no state) so they
    /// are EditMode-testable and so the no-per-frame-allocation discipline lives entirely in the
    /// controller's change-detection, not hidden in here.
    /// </summary>
    public static class HudFormat
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>"06:30" from an hour-of-day in [0,24). 24h clock, zero-padded. Minutes are
        /// rounded (not truncated) so 23h59m reads "23:59", not "23:58".</summary>
        public static string ClockHHMM(float hourOfDay)
        {
            // Work in whole minutes-of-day so rounding is exact and wrapping is trivial.
            int totalMinutes = Mathf.RoundToInt(hourOfDay * 60f);
            totalMinutes %= 24 * 60;
            if (totalMinutes < 0) totalMinutes += 24 * 60;

            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            return hours.ToString("00", Inv) + ":" + minutes.ToString("00", Inv);
        }

        /// <summary>"1:42" from in-game seconds → in-game H:MM (for time-to-next-turn). Drops the
        /// leading zero on hours (it's a duration, not a clock). Caps display sanity at 99h.</summary>
        public static string DurationHMM(double inGameSeconds, float secondsPerHour)
        {
            if (inGameSeconds < 0.0 || secondsPerHour <= 0f) return "--";

            double inGameHours = inGameSeconds / secondsPerHour;
            int hours = (int)inGameHours;
            int minutes = (int)((inGameHours - hours) * 60.0);
            if (minutes >= 60) { minutes -= 60; hours += 1; }
            if (hours > 99) hours = 99;
            return hours.ToString(Inv) + ":" + minutes.ToString("00", Inv);
        }

        /// <summary>Tide height like "+1.6 m" / "-0.3 m" / "0.0 m" (one decimal).</summary>
        public static string HeightMeters(float metres)
        {
            // Explicit sign for non-negative so the read "is it high or low" is instant.
            string sign = metres >= 0.05f ? "+" : (metres <= -0.05f ? "" : "");
            return sign + metres.ToString("0.0", Inv) + " m";
        }

        /// <summary>Money like "₲1,240".</summary>
        public static string Money(int balance)
            => HudStrings.MoneyPrefix + balance.ToString("N0", Inv);

        /// <summary>A payout flash like "+₲48" (positive) or "₲0" guard.</summary>
        public static string PayoutFlash(int amount)
        {
            if (amount <= 0) return HudStrings.MoneyPrefix + "0";
            return "+" + HudStrings.MoneyPrefix + amount.ToString("N0", Inv);
        }

        /// <summary>Wind label like "12 kt" (knots, no decimal — glanceable).</summary>
        public static string WindKnots(float knots)
            => Math.Max(0, (int)Math.Round(knots)).ToString(Inv) + " kt";

        /// <summary>Beaufort label like "F4".</summary>
        public static string BeaufortLabel(int force) => "F" + force.ToString(Inv);
    }
}
