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

        /// <summary>A landed-fish celebration card like "Atlantic Cod — 3.4 kg — ₲48!". Weight to one
        /// decimal; value is the fish's base worth (the market sale is a separate payout flash).</summary>
        public static string CatchCard(string displayName, float weightKg, int baseValue)
        {
            string name = string.IsNullOrEmpty(displayName) ? "Catch" : displayName;
            int value = baseValue > 0 ? baseValue : 0;
            return name + " — " + weightKg.ToString("0.0", Inv) + " kg — "
                 + HudStrings.MoneyPrefix + value.ToString("N0", Inv) + "!";
        }

        /// <summary>Wind label like "12 kt" (knots, no decimal — glanceable).</summary>
        public static string WindKnots(float knots)
            => Math.Max(0, (int)Math.Round(knots)).ToString(Inv) + " kt";

        /// <summary>Beaufort label like "F4".</summary>
        public static string BeaufortLabel(int force) => "F" + force.ToString(Inv);

        /// <summary>
        /// Wind-strength "barbs" — a marine-style glyph whose LENGTH encodes speed, so the wind reads
        /// strong/weak by shape, not just by the "12 kt"/"F4" numbers (redundant coding,
        /// colourblind-safe). Each full barb ▮ ≈ 10 kt, a trailing half barb ▪ ≈ 5 kt, calm is the
        /// ring ○. Knots are rounded to the nearest 5; negatives clamp to calm.
        /// </summary>
        public static string WindBarbs(int knots)
        {
            int k = knots < 0 ? 0 : knots;
            int k5 = ((k + 2) / 5) * 5;                  // round to nearest 5 kt
            if (k5 == 0) return HudStrings.WindCalm;      // ○

            int full = k5 / 10;                          // ▮ per 10 kt
            bool half = (k5 % 10) >= 5;                  // ▪ for the odd +5 kt
            string barbs = full > 0 ? new string(HudStrings.WindBarbFull[0], full) : string.Empty;
            return half ? barbs + HudStrings.WindBarbHalf : barbs;
        }
    }
}
