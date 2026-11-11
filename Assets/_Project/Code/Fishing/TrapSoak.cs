using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>Soak progress from placement and clock seconds. Authored hours use the same
    /// configured day length as the game clock; progress is recomputed, never accumulated.</summary>
    public static class TrapSoak
    {
        private static double CurrentSecondsPerDay => GameServices.Config != null
            ? GameServices.Config.SecondsPerDay : GameConfig.DefaultSecondsPerDay;

        public static double SecondsPerHour => CurrentSecondsPerDay / 24d;

        /// <summary>Live clock-scale wrapper shared by traps, filling and longline bite/strip times.</summary>
        public static double SoakSeconds(float soakHours) => SoakSeconds(soakHours, CurrentSecondsPerDay);

        /// <summary>Pure conversion with an explicit clock scale. Invalid day lengths use the
        /// configured reference day, matching an unwired game rather than a civil-day assumption.</summary>
        public static double SoakSeconds(float soakHours, double secondsPerDay)
        {
            if (!(secondsPerDay > 0d) || double.IsInfinity(secondsPerDay))
                secondsPerDay = GameConfig.DefaultSecondsPerDay;
            return soakHours > 0f ? soakHours * (secondsPerDay / 24d) : 0d;
        }

        public static double ElapsedSeconds(double placedAtSeconds, double nowSeconds)
        {
            double elapsed = nowSeconds - placedAtSeconds;
            return elapsed > 0d ? elapsed : 0d;
        }

        public static float Progress01(double placedAtSeconds, double nowSeconds, float soakHours)
            => Progress01(placedAtSeconds, nowSeconds, soakHours, CurrentSecondsPerDay);

        public static float Progress01(double placedAtSeconds, double nowSeconds, float soakHours,
            double secondsPerDay)
        {
            double span = SoakSeconds(soakHours, secondsPerDay);
            if (span <= 0d) return 1f;
            double p = ElapsedSeconds(placedAtSeconds, nowSeconds) / span;
            if (p < 0d) p = 0d;
            if (p > 1d) p = 1d;
            return (float)p;
        }

        public static bool IsReady(double placedAtSeconds, double nowSeconds, float soakHours)
            => IsReady(placedAtSeconds, nowSeconds, soakHours, CurrentSecondsPerDay);

        public static bool IsReady(double placedAtSeconds, double nowSeconds, float soakHours,
            double secondsPerDay)
        {
            double span = SoakSeconds(soakHours, secondsPerDay);
            return span <= 0d || ElapsedSeconds(placedAtSeconds, nowSeconds) >= span;
        }
    }
}
