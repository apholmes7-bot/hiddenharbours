using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    public static partial class CrossingMath
    {
        /// <summary>Strictly below for work, strictly above for launch. Equality is closed.
        /// Reads the service at this instant; no cache, delegate, or allocation.</summary>
        public static bool IsWorkableNow(IEnvironmentService tide, TideProfile place,
                                          double seconds, float height, bool above = false)
        {
            if (tide == null) throw new ArgumentNullException(nameof(tide));
            RequireFinite(seconds, nameof(seconds));
            RequireFinite(height, nameof(height));
            return OnWorkSide(tide.TideHeightAt(seconds, place), height, above);
        }

        /// <summary>Windows intersecting a zero-based game day, clipped to its midnight boundaries.
        /// Adjacent days describe the two pieces of a window crossing midnight. Times use the clock's
        /// total-seconds axis. Reuses the tide table's scan resolution and the crossing edge bisection.
        /// As with FindWindows, excursions shorter than a scan step may not be resolved.</summary>
        public static void FindWorkWindows(IEnvironmentService tide, TideProfile place, int gameDay,
                                             float height, bool above, GameConfig cfg,
                                             List<TideWindow> results)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (gameDay < 0) throw new ArgumentOutOfRangeException(nameof(gameDay));
            double day = cfg.SecondsPerDay;
            double start = gameDay * day;
            FindWorkWindows(tide, place, start, start + day, height, above,
                            cfg.TideTable.ScanStepHours * day / HoursPerDay, results);
        }

        /// <summary>Windows intersecting a bounded time span, clipped at either end. Results are
        /// cleared, sorted and disjoint. The caller chooses a step fine enough for the tide curve.</summary>
        public static void FindWorkWindows(IEnvironmentService tide, TideProfile place,
                                             double from, double to, float height, bool above,
                                             double step, List<TideWindow> results)
        {
            ValidateWorkSearch(tide, from, to, height, step);
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            Func<double, float> water = t => tide.TideHeightAt(t, place);
            bool inside = OnWorkSide(water(from), height, above);
            double start = from;
            for (double t = from; t < to;)
            {
                double next = NextProbe(t, to, step);
                bool nextInside = OnWorkSide(water(next), height, above);
                if (nextInside != inside)
                {
                    double edge = Bisect(water, height, t, next, above);
                    if (nextInside) start = edge;
                    else if (edge > start) results.Add(new TideWindow(start, edge));
                }
                inside = nextInside;
                t = next;
            }
            if (inside && to > start) results.Add(new TideWindow(start, to));
        }

        /// <summary>Now if already open, otherwise the first opening found through searchEnd.
        /// False means none was found in this horizon, not that the place never opens.</summary>
        public static bool TryNextWorkOpening(IEnvironmentService tide, TideProfile place,
                                                double now, double searchEnd, float height, bool above,
                                                double step, out double opening)
        {
            ValidateWorkSearch(tide, now, searchEnd, height, step);
            opening = now;
            if (IsWorkableNow(tide, place, now, height, above)) return true;
            return TryWorkEdge(tide, place, now, searchEnd, height, above, step, true, out opening);
        }

        /// <summary>Zero if closed. False means the closing edge was not found by searchEnd;
        /// secondsLeft then reads NaN, so an incomplete forecast cannot promise unlimited work.</summary>
        public static bool TryWorkSecondsLeft(IEnvironmentService tide, TideProfile place,
                                                double now, double searchEnd, float height, bool above,
                                                double step, out double secondsLeft)
        {
            ValidateWorkSearch(tide, now, searchEnd, height, step);
            secondsLeft = 0;
            if (!IsWorkableNow(tide, place, now, height, above)) return true;
            if (!TryWorkEdge(tide, place, now, searchEnd, height, above, step, false, out double close))
            {
                secondsLeft = double.NaN;
                return false;
            }
            secondsLeft = close - now;
            return true;
        }

        private static bool TryWorkEdge(IEnvironmentService tide, TideProfile place, double from,
                                       double to, float height, bool above, double step,
                                       bool targetInside, out double edge)
        {
            Func<double, float> water = t => tide.TideHeightAt(t, place);
            for (double t = from; t < to;)
            {
                double next = NextProbe(t, to, step);
                if (OnWorkSide(water(next), height, above) == targetInside)
                {
                    edge = Bisect(water, height, t, next, above);
                    return true;
                }
                t = next;
            }
            edge = double.NaN;
            return false;
        }

        private static bool OnWorkSide(float water, float height, bool above) =>
            above ? water > height : water < height;

        private static void ValidateWorkSearch(IEnvironmentService tide, double from, double to,
                                               float height, double step)
        {
            if (tide == null) throw new ArgumentNullException(nameof(tide));
            RequireFinite(from, nameof(from));
            RequireFinite(to, nameof(to));
            RequireFinite(height, nameof(height));
            RequireFinite(step, nameof(step));
            if (!(to > from)) throw new ArgumentOutOfRangeException(nameof(to));
            if (!(step > 0)) throw new ArgumentOutOfRangeException(nameof(step));
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name);
        }

        private static double NextProbe(double time, double end, double step)
        {
            double next = Math.Min(time + step, end);
            if (!(next > time)) throw new ArgumentOutOfRangeException(nameof(step));
            return next;
        }
    }
}
