using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A print's strength is a function of (madeAt, now) and nothing else.</b> No per-print state
    /// ticks down, so a sleep, a fast-forward or a clock that jumps on load ages every print correctly
    /// for free, and fading costs nothing beyond re-reading this when the layer is redrawn.
    /// </summary>
    public static class GroundPrintMath
    {
        /// <summary>Game hours in a game day: the clock's own definition, not a tunable.</summary>
        public const double HoursPerDay = 24d;

        /// <summary>A lifetime of <paramref name="gameHours"/> in clock seconds, for a day of
        /// <paramref name="secondsPerDay"/> clock seconds.</summary>
        public static double LifetimeSeconds(double gameHours, double secondsPerDay)
            => Math.Max(0d, gameHours) * Math.Max(0d, secondsPerDay) / HoursPerDay;

        /// <summary>
        /// The fade factor 0..1: 1 when fresh, falling monotonically, exactly 0 at
        /// <paramref name="lifetimeSeconds"/> of age and after. A print "from the future" (the clock was
        /// rewound by a load) is 0: it cannot have been made yet.
        /// </summary>
        public static float Strength(double madeAt, double now, double lifetimeSeconds, float exponent)
        {
            double age = now - madeAt;
            if (age < 0d || lifetimeSeconds <= 0d || age >= lifetimeSeconds) return 0f;
            double left = 1d - age / lifetimeSeconds;
            return (float)Math.Pow(left, Math.Max(1e-3, exponent));
        }

        /// <summary>True once a print has faded out (or cannot exist yet).</summary>
        public static bool IsGone(double madeAt, double now, double lifetimeSeconds)
        {
            double age = now - madeAt;
            return age < 0d || age >= lifetimeSeconds;
        }

        /// <summary>
        /// Band a strength into <paramref name="steps"/> flat steps, rounding UP: a print never vanishes
        /// a step early, and it only reaches 0 when <see cref="Strength"/> does.
        /// </summary>
        public static float Banded(float strength, int steps)
        {
            if (strength <= 0f) return 0f;
            if (steps <= 1) return 1f;
            return Mathf.Min(1f, Mathf.Ceil(strength * steps) / steps);
        }

        /// <summary>Snap a unit heading to one of <paramref name="steps"/> evenly spaced directions.</summary>
        public static Vector2 SnapHeading(Vector2 heading, int steps)
        {
            if (heading.sqrMagnitude < 1e-12f) return Vector2.up;
            if (steps < 1) return heading.normalized;
            double step = 2d * Math.PI / steps;
            double a = Math.Atan2(heading.y, heading.x);
            a = Math.Round(a / step) * step;
            return new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
        }

        /// <summary>Snap a world point onto the rendered pixel grid (no-op when the grid is unknown).</summary>
        public static Vector2 SnapToPixel(Vector2 p, float worldUnitsPerPixel)
        {
            if (worldUnitsPerPixel <= 0f) return p;
            return new Vector2(Mathf.Round(p.x / worldUnitsPerPixel) * worldUnitsPerPixel,
                               Mathf.Round(p.y / worldUnitsPerPixel) * worldUnitsPerPixel);
        }

        /// <summary>
        /// Is a print at ground <paramref name="groundElevation"/> under water at
        /// <paramref name="waterLevel"/>? The one tide rule (<see cref="TidalExposure"/>): the sea that
        /// covers the sand wipes what was written on it.
        /// </summary>
        public static bool IsWashed(float waterLevel, float groundElevation)
            => TidalExposure.IsSubmerged(waterLevel, groundElevation);
    }
}
