using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// <b>The rope between a pot on the bottom and its buoy on the surface</b>, as pure arithmetic. The rope has a
    /// fixed length; the water over the pot does not. What is left over once the rope has reached the surface is
    /// its <i>scope</i>, and the tide streams the buoy off the pot by a share of it: one way on the flood, the
    /// other on the ebb, closing up over the pot at slack and at high water. So the buoy follows the tide's height
    /// and its direction from <c>WaterLevelAt(t)</c> alone, with nothing saved (rule 5).
    /// </summary>
    public static class PotRopeMath
    {
        /// <summary>How far the buoy could lie from above the pot with the rope straight: sqrt(L*L - depth*depth), 0 when the
        /// water is as deep as the rope is long.</summary>
        public static float Scope(float ropeLengthMeters, float depthMeters)
        {
            float d = Mathf.Max(0f, depthMeters);
            float sq = ropeLengthMeters * ropeLengthMeters - d * d;
            return sq > 0f ? Mathf.Sqrt(sq) : 0f;
        }

        /// <summary>True when the water over the pot is deeper than its rope: the buoy is held under.</summary>
        public static bool IsDrowned(float ropeLengthMeters, float depthMeters) => depthMeters >= ropeLengthMeters;

        /// <summary>The tide's rise (+) or fall (-) in metres per game hour, from two water levels a known number of
        /// clock seconds apart.</summary>
        public static float RiseMetersPerHour(float levelBefore, float levelAfter, double clockSecondsBetween,
                                              double secondsPerDay)
        {
            if (clockSecondsBetween <= 0.0 || secondsPerDay <= 0.0) return 0f;
            double hours = clockSecondsBetween / (secondsPerDay / 24.0);
            return (float)((levelAfter - levelBefore) / hours);
        }

        /// <summary>The stream, -1 (full ebb) to +1 (full flood), from the tide's rate of rise.</summary>
        public static float Stream(float riseMetersPerHour, float fullStreamRiseMetersPerHour)
            => fullStreamRiseMetersPerHour > 0f
                ? Mathf.Clamp(riseMetersPerHour / fullStreamRiseMetersPerHour, -1f, 1f)
                : 0f;

        /// <summary>Where the buoy floats relative to the point above its pot.</summary>
        public static Vector2 FloatOffset(float ropeLengthMeters, float depthMeters, float stream,
                                          float floodBearingDegrees, float streamLean01)
        {
            float reach = Scope(ropeLengthMeters, depthMeters) * Mathf.Clamp01(streamLean01) * Mathf.Clamp(stream, -1f, 1f);
            return NpcPotDay.Along(floodBearingDegrees) * reach;
        }

        /// <summary>Where something <paramref name="depthMeters"/> under the surface at <paramref name="plan"/> is
        /// drawn in the three-quarter view: straight down-screen by a fraction of its depth.</summary>
        public static Vector2 DrawnAtDepth(Vector2 plan, float depthMeters, float depthDrawFraction)
            => plan + Vector2.down * (Mathf.Max(0f, depthMeters) * depthDrawFraction);

        /// <summary>How visible something under water is from above: 1 at the surface, 0 at
        /// <paramref name="vanishDepthMeters"/> and below.</summary>
        public static float SeenThroughWater01(float depthMeters, float vanishDepthMeters)
            => vanishDepthMeters > 0f ? 1f - Mathf.Clamp01(depthMeters / vanishDepthMeters) : 0f;
    }
}
