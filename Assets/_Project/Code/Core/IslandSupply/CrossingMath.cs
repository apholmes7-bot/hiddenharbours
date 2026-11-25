using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>When the bar can be crossed, and how many crossings fit</b> (island supply slice S1). Pure and
    /// deterministic: a function of the water level, the route and the clock constants, never stored (rule 5).
    ///
    /// <para><b>The water is passed in, not copied.</b> Core cannot see <c>HiddenHarbours.Environment</c>, so
    /// every query takes <c>waterAt(totalSeconds) → metres</c>. Callers hand it the real tide:
    /// <c>t => TideModel.Height(t, profile, config)</c> with the island region's own profile. Time is on the
    /// clock's axis (<c>GameClock.TotalSeconds</c>, the axis <c>TideModel</c> reads: one game day is
    /// <c>GameConfig.SecondsPerDay</c>). Mover speeds are metres per real second on that same axis, so a leg of
    /// <c>d</c> metres at <c>v</c> takes <c>d / v</c> clock seconds.</para>
    ///
    /// <para><b>The crossing rule.</b> The sandbar is the road: it must stand DRY (water below the crest) for
    /// every moment a mover is on it, and while the mover is inside the gut the water there must be shallower
    /// than the mover's wade limit (water below <c>gut bed + wade</c>). The island path and the mainland road
    /// never flood.</para>
    /// </summary>
    public static class CrossingMath
    {
        private const double MinutesPerDay = 24.0 * 60.0;
        private const double HoursPerDay = 24.0;
        private const int EdgeBisections = 40;

        // ---- clock ------------------------------------------------------------------------------------

        /// <summary>Clock seconds in one game hour (<c>SecondsPerDay / 24</c>; 75 at the shipped 1800).</summary>
        public static double SecondsPerGameHour(GameConfig cfg) => cfg.SecondsPerDay / HoursPerDay;

        /// <summary>Clock seconds expressed as game hours.</summary>
        public static double ToGameHours(double clockSeconds, GameConfig cfg) => clockSeconds / SecondsPerGameHour(cfg);

        /// <summary>Game hours expressed as clock seconds.</summary>
        public static double FromGameHours(double gameHours, GameConfig cfg) => gameHours * SecondsPerGameHour(cfg);

        /// <summary>The configured search resolution in clock seconds.</summary>
        public static double SearchStepSeconds(IslandSupplyConfig route, GameConfig cfg) =>
            Math.Max(1e-3, route.SearchStepGameMinutes * cfg.SecondsPerDay / MinutesPerDay);

        // ---- windows ----------------------------------------------------------------------------------

        /// <summary>
        /// Every window in which the water stands below <paramref name="threshold"/> and which STARTS inside
        /// [<paramref name="fromSeconds"/>, <paramref name="toSeconds"/>). Edges are sampled at
        /// <paramref name="stepSeconds"/> and then bisected, so lengths are exact to well under a second. A
        /// threshold the water never rises above returns one window spanning the whole range.
        /// </summary>
        public static List<TideWindow> FindWindows(Func<double, float> waterAt, float threshold,
                                                   double fromSeconds, double toSeconds,
                                                   double stepSeconds, GameConfig cfg)
        {
            var result = new List<TideWindow>();
            if (waterAt == null || cfg == null || !(toSeconds > fromSeconds) || !(stepSeconds > 0.0)) return result;

            // Scan one tide period either side so a window straddling an edge is seen whole.
            double pad = Math.Max(stepSeconds, cfg.TidalPeriodHours * SecondsPerGameHour(cfg));
            double scanFrom = fromSeconds - pad;
            double scanTo = toSeconds + pad;

            bool everWet = false;
            bool inside = waterAt(scanFrom) < threshold;
            bool openAtScanStart = inside;
            double start = scanFrom;
            double t = scanFrom;
            while (t < scanTo)
            {
                double next = Math.Min(t + stepSeconds, scanTo);
                bool nowInside = waterAt(next) < threshold;
                if (!nowInside) everWet = true;
                if (nowInside && !inside)
                {
                    start = Bisect(waterAt, threshold, t, next);
                    openAtScanStart = false;
                }
                else if (!nowInside && inside)
                {
                    double end = Bisect(waterAt, threshold, t, next);
                    if (!openAtScanStart && start >= fromSeconds && start < toSeconds)
                        result.Add(new TideWindow(start, end));
                    openAtScanStart = false;
                }
                inside = nowInside;
                t = next;
            }

            if (!everWet) result.Add(new TideWindow(fromSeconds, toSeconds));
            return result;
        }

        /// <summary>The crest windows (the bar dry) starting inside the range, at the route's search step.</summary>
        public static List<TideWindow> BarWindows(Func<double, float> waterAt, IslandSupplyConfig route,
                                                  GameConfig cfg, double fromSeconds, double toSeconds) =>
            FindWindows(waterAt, route.BarCrestElevationMetres, fromSeconds, toSeconds,
                        SearchStepSeconds(route, cfg), cfg);

        /// <summary>The gut windows for a wade limit: water below <c>gut bed + wade</c>.</summary>
        public static List<TideWindow> GutWindows(Func<double, float> waterAt, IslandSupplyConfig route,
                                                  float wadeDepthMetres, GameConfig cfg,
                                                  double fromSeconds, double toSeconds) =>
            FindWindows(waterAt, route.GutBedElevationMetres + wadeDepthMetres, fromSeconds, toSeconds,
                        SearchStepSeconds(route, cfg), cfg);

        // Finds the crossing of `threshold` between a (one side) and b (the other side).
        private static double Bisect(Func<double, float> waterAt, float threshold, double a, double b)
        {
            bool aInside = waterAt(a) < threshold;
            for (int i = 0; i < EdgeBisections; i++)
            {
                double m = 0.5 * (a + b);
                if ((waterAt(m) < threshold) == aInside) a = m; else b = m;
            }
            return 0.5 * (a + b);
        }

        // ---- legs -------------------------------------------------------------------------------------

        /// <summary>Clock seconds to cross the bar at <paramref name="speed"/> m/s.</summary>
        public static double BarLegSeconds(IslandSupplyConfig route, double speed) => route.BarLengthMetres / speed;

        /// <summary>Clock seconds from the moment the role leaves the island's bar head to the moment it is back
        /// there: bar out, road out, loading, road back, bar back. The village path is not included (it is
        /// never flooded, so it does not use window time).</summary>
        public static double RoundTripBarHeadSeconds(SupplyRoleDef role, IslandSupplyConfig route, GameConfig cfg) =>
            2.0 * BarLegSeconds(route, role.SandSpeedMetresPerSecond) + TownTurnaroundSeconds(role, route, cfg);

        /// <summary>Village green to village green, clock seconds: the round trip plus the island path both ways.</summary>
        public static double RoundTripVillageSeconds(SupplyRoleDef role, IslandSupplyConfig route, GameConfig cfg) =>
            RoundTripBarHeadSeconds(role, route, cfg) + 2.0 * route.IslandPathMetres / role.RoadSpeedMetresPerSecond;

        /// <summary>Time off the bar on the mainland side: road to the errand stop and back, plus loading.</summary>
        public static double TownTurnaroundSeconds(SupplyRoleDef role, IslandSupplyConfig route, GameConfig cfg) =>
            2.0 * route.MainlandRoadMetres / role.RoadSpeedMetresPerSecond + FromGameHours(role.LoadingHours, cfg);

        /// <summary>
        /// Can a mover leaving one end of the bar at <paramref name="departSeconds"/> at <paramref name="speed"/>
        /// cross without the crest being wet under it or the gut deeper than <paramref name="wadeDepthMetres"/>
        /// while it is in the gut? <paramref name="fromIsland"/> sets which end it starts from.
        /// </summary>
        public static bool BarLegFits(Func<double, float> waterAt, IslandSupplyConfig route, double departSeconds,
                                      double speed, float wadeDepthMetres, bool fromIsland, double stepSeconds)
        {
            double length = route.BarLengthMetres;
            double duration = length / speed;
            double gutFromStart = fromIsland ? route.GutCentreFromIslandMetres : length - route.GutCentreFromIslandMetres;
            double half = 0.5 * route.GutWidthMetres;
            double gutEnter = departSeconds + Math.Max(0.0, gutFromStart - half) / speed;
            double gutLeave = departSeconds + Math.Min(length, gutFromStart + half) / speed;
            float crest = route.BarCrestElevationMetres;
            float gutLimit = route.GutBedElevationMetres + wadeDepthMetres;

            // The crest must be dry for the whole leg; samples on the step plus both ends.
            double arrive = departSeconds + duration;
            for (double t = departSeconds; t < arrive; t += stepSeconds)
                if (!(waterAt(t) < crest)) return false;
            if (!(waterAt(arrive) < crest)) return false;

            // The gut must be wadeable from entry to exit; samples on the step plus both edges.
            for (double t = gutEnter; t < gutLeave; t += stepSeconds)
                if (!(waterAt(t) < gutLimit)) return false;
            return waterAt(gutLeave) < gutLimit;
        }

        // ---- trips in a window ------------------------------------------------------------------------

        /// <summary>
        /// The earliest departure from the island's bar head, at or after <paramref name="earliestSeconds"/>,
        /// that lets the role go over, do its town turnaround and come back over within one window, home no
        /// later than <paramref name="latestHomeSeconds"/>. Returns false if none exists.
        /// </summary>
        public static bool TryRoundTripInWindow(Func<double, float> waterAt, TideWindow window, SupplyRoleDef role,
                                                IslandSupplyConfig route, GameConfig cfg,
                                                double earliestSeconds, double latestHomeSeconds,
                                                out double departSeconds)
        {
            departSeconds = 0.0;
            double step = SearchStepSeconds(route, cfg);
            double speed = role.SandSpeedMetresPerSecond;
            double leg = BarLegSeconds(route, speed);
            double returnOffset = leg + TownTurnaroundSeconds(role, route, cfg);
            double total = returnOffset + leg;
            double last = Math.Min(window.EndSeconds, latestHomeSeconds) - total;
            for (double t = Math.Max(window.StartSeconds, earliestSeconds); t <= last; t += step)
            {
                if (!BarLegFits(waterAt, route, t, speed, role.MaxWadeDepthMetres, true, step)) continue;
                if (!BarLegFits(waterAt, route, t + returnOffset, speed, role.MaxWadeDepthMetres, false, step)) continue;
                departSeconds = t;
                return true;
            }
            return false;
        }

        /// <summary>
        /// How many one-way crossings the role can make in one window, alternating direction (first from the
        /// island), with its town turnaround between an arrival in town and the next departure, and each
        /// departure taken at the earliest moment the leg fits. A walker whose answer is 1 must stay over.
        /// </summary>
        public static int MaxOneWayCrossingsInWindow(Func<double, float> waterAt, TideWindow window, SupplyRoleDef role,
                                                     IslandSupplyConfig route, GameConfig cfg)
        {
            double step = SearchStepSeconds(route, cfg);
            double speed = role.SandSpeedMetresPerSecond;
            double leg = BarLegSeconds(route, speed);
            double turnaround = TownTurnaroundSeconds(role, route, cfg);
            bool fromIsland = true;
            double t = window.StartSeconds;
            int count = 0;
            while (true)
            {
                bool found = false;
                for (; t + leg <= window.EndSeconds; t += step)
                {
                    if (!BarLegFits(waterAt, route, t, speed, role.MaxWadeDepthMetres, fromIsland, step)) continue;
                    found = true;
                    break;
                }
                if (!found) return count;
                count++;
                // Arrived in town: errands first. Arrived home: the next run is another day's.
                if (!fromIsland) return count;
                t += leg + turnaround;
                fromIsland = false;
            }
        }

        /// <summary>
        /// The earliest same-window round trip on game day <paramref name="day"/> (0-based, day 0 starting at
        /// clock 0) that leaves no earlier than the route's run-day start and is home by its run-day end.
        /// </summary>
        public static bool TryDaylightRoundTrip(Func<double, float> waterAt, IList<TideWindow> barWindows, int day,
                                                SupplyRoleDef role, IslandSupplyConfig route, GameConfig cfg,
                                                out double departSeconds)
        {
            departSeconds = 0.0;
            double dayStart = day * (double)cfg.SecondsPerDay;
            double earliest = dayStart + FromGameHours(route.RunDayStartHour, cfg);
            double latest = dayStart + FromGameHours(route.RunDayEndHour, cfg);
            for (int i = 0; i < barWindows.Count; i++)
            {
                TideWindow w = barWindows[i];
                if (w.EndSeconds <= earliest || w.StartSeconds >= latest) continue;
                if (TryRoundTripInWindow(waterAt, w, role, route, cfg, earliest, latest, out departSeconds)) return true;
            }
            return false;
        }
    }
}
