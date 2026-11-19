using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    public static class CloudShadowMath
    {
        public static float Overcast(float visibility, float seaState, DayNightProfile profile)
            => profile != null && profile.WeatherDimMax > 0
                ? Mathf.Clamp01(DayNightMath.WeatherDim(visibility, seaState, profile) / profile.WeatherDimMax) : 0;

        public static float Strength(CloudShadowDef def, float overcast, float elevation)
            => def == null || !def.Enabled || elevation <= 0 ? 0
                : Mathf.Clamp01(def.Strength.Evaluate(Mathf.Clamp01(overcast)))
                    * Mathf.Clamp01(elevation / def.FullSunElevation);

        public static Vector2 SeedOrigin(int seed, CloudShadowDef def)
        {
            var random = new System.Random(seed);
            return new Vector2((float)random.NextDouble() * def.ScaleMetres, (float)random.NextDouble() * def.ScaleMetres);
        }

        public static Vector2 Offset(int seed, double gameSeconds, Vector2 downwind, CloudShadowDef def)
        {
            var origin = SeedOrigin(seed, def);
            return Advance(origin, downwind, gameSeconds, def.DriftMetresPerGameSecondPerMps, def.ScaleMetres);
        }

        public static Vector2 Advance(Vector2 origin, Vector2 wind, double seconds, double rate, double period)
            => new Vector2((float)Wrap(origin.x + wind.x * seconds * rate, period),
                           (float)Wrap(origin.y + wind.y * seconds * rate, period));

        public static double Wrap(double value, double period) => value - Math.Floor(value / period) * period;
    }

    public readonly struct CloudShadowFrame
    {
        public readonly int Seed;
        public readonly double Seconds;
        public readonly Vector2 Wind;
        public readonly float Overcast, Elevation;
        public CloudShadowFrame(int seed, double seconds, Vector2 wind, float overcast, float elevation)
        { Seed = seed; Seconds = seconds; Wind = wind; Overcast = overcast; Elevation = elevation; }
    }
}
