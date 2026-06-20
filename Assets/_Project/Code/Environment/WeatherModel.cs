using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Environment
{
    /// <summary>
    /// Pure, deterministic weather from (seed, time): wind vector, sea state and fog. Uses
    /// hash-based value noise so it is smooth and fully reproducible — no UnityEngine.Random,
    /// nothing saved. See design/time-tides-weather.md and tech-architecture.md §1.
    /// </summary>
    public static class WeatherModel
    {
        public static void Sample(double totalSeconds, int seed, GameConfig cfg,
                                  out Vector2 wind, out SeaState sea, out float visibility)
        {
            double hours = totalSeconds / (cfg.SecondsPerDay / 24.0);
            float t = (float)(hours / Mathf.Max(0.1f, cfg.WeatherChangeHours));

            // Independent noise channels (offset the sample point per channel).
            float dir = (Noise(t, seed) * 0.5f + 0.5f) * Mathf.PI * 2f;           // 0..2π
            float strengthN = Noise(t + 137.0f, seed) * 0.5f + 0.5f;             // 0..1
            float fogN = Noise(t + 911.0f, seed) * 0.5f + 0.5f;                  // 0..1

            float strength = Mathf.Max(0f,
                cfg.BaseWindStrength + (strengthN - 0.5f) * 2f * cfg.WindVariability);

            wind = new Vector2(Mathf.Cos(dir), Mathf.Sin(dir)) * strength;
            sea = SeaFromWind(strength);
            float fog = Mathf.Clamp01(cfg.BaseFogBias + (fogN - 0.5f));
            visibility = 1f - fog;
        }

        /// <summary>Map wind strength (m/s) to the canon sea-state scale (rough Beaufort-ish).</summary>
        public static SeaState SeaFromWind(float strength)
        {
            if (strength < 0.5f) return SeaState.Glass;
            if (strength < 2f)   return SeaState.Calm;
            if (strength < 4f)   return SeaState.Light;
            if (strength < 6f)   return SeaState.Moderate;
            if (strength < 8f)   return SeaState.Lively;
            if (strength < 11f)  return SeaState.Rough;
            if (strength < 14f)  return SeaState.Gale;
            return SeaState.Storm;
        }

        // --- deterministic 1D value noise in [-1, 1] ---
        private static float Noise(float x, int seed)
        {
            int xi = Mathf.FloorToInt(x);
            float xf = x - xi;
            float a = Hash(xi, seed);
            float b = Hash(xi + 1, seed);
            float u = xf * xf * (3f - 2f * xf);   // smoothstep
            return Mathf.Lerp(a, b, u);
        }

        private static float Hash(int n, int seed)
        {
            unchecked
            {
                n = (n * 73856093) ^ (seed * 19349663) ^ 0x5f3759df;
                n = (n << 13) ^ n;
                int m = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
                return 1f - m / 1073741824.0f;    // [-1, 1]
            }
        }
    }
}
