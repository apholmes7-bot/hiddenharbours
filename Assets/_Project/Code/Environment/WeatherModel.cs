using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Environment
{
    /// <summary>
    /// Pure, deterministic weather from (seed, time): wind vector, sea state and fog. Uses
    /// hash-based value noise so it is smooth and fully reproducible — no UnityEngine.Random,
    /// nothing saved. See design/time-tides-weather.md and tech-architecture.md §1.
    ///
    /// <para>VS-05 — the wind is a smooth FIELD: a region's <b>prevailing</b> wind (a gentle SW'ly
    /// over Coddle Cove) that wanders on a slow channel, with faster <b>gusts</b> layered on top,
    /// all clamped into the <b>calm band</b> (no storms — that's M2). The wind tunables live on
    /// <see cref="WindProfile"/> (not GameConfig), mirroring how <see cref="TideProfile"/> carries
    /// per-region tide character. Same <c>(totalSeconds, seed, profile)</c> → identical wind.</para>
    /// </summary>
    public static class WeatherModel
    {
        /// <summary>
        /// Full weather sample for a region. <paramref name="wind"/> selects the prevailing-wind
        /// character; fog/visibility still read <see cref="GameConfig.BaseFogBias"/> /
        /// <see cref="GameConfig.WeatherChangeHours"/> (unchanged).
        /// </summary>
        public static void Sample(double totalSeconds, int seed, GameConfig cfg, WindProfile wind,
                                  out Vector2 windVector, out SeaState sea, out float visibility)
        {
            double secondsPerHour = cfg.SecondsPerDay / 24.0;

            windVector = SampleWind(totalSeconds, seed, secondsPerHour, wind);
            sea = SeaFromWind(windVector.magnitude);

            // Fog is an independent slow channel (unchanged), kept biasable from config.
            double hours = totalSeconds / secondsPerHour;
            float fogT = (float)(hours / Mathf.Max(0.1f, cfg.WeatherChangeHours));
            float fogN = Noise(fogT + 911.0f, seed) * 0.5f + 0.5f;             // 0..1
            float fog = Mathf.Clamp01(cfg.BaseFogBias + (fogN - 0.5f));
            visibility = 1f - fog;
        }

        /// <summary>
        /// Backwards-compatible overload using the greybox <see cref="WindProfile.CoddleCove"/>
        /// (the only region in M1). Callers that want a specific region pass a profile.
        /// </summary>
        public static void Sample(double totalSeconds, int seed, GameConfig cfg,
                                  out Vector2 windVector, out SeaState sea, out float visibility)
            => Sample(totalSeconds, seed, cfg, WindProfile.CoddleCove, out windVector, out sea, out visibility);

        /// <summary>
        /// The deterministic wind field (VS-05): prevailing direction + a slow wander, plus a faster
        /// gust octave (with a small veer), clamped into the calm band. Pure and engine-light (no
        /// GameConfig, no state) so it is trivially EditMode-testable and cheap at the 4 Hz tick.
        /// Smooth in time (value noise with a smoothstep fade — C1, so it never pops).
        /// </summary>
        /// <param name="totalSeconds">Master clock value (in-game seconds).</param>
        /// <param name="seed">World seed — the only entropy source (no hidden RNG).</param>
        /// <param name="secondsPerHour">In-game seconds per in-game hour (<c>GameConfig.SecondsPerHour</c>).</param>
        /// <param name="p">Region wind character. A default/unset profile heals to Coddle Cove.</param>
        public static Vector2 SampleWind(double totalSeconds, int seed, double secondsPerHour, WindProfile p)
        {
            if (p.CalmMaxStrength <= 0f) p = WindProfile.CoddleCove;   // heal a default/unset profile

            double hours = totalSeconds / System.Math.Max(1e-6, secondsPerHour);

            // Slow channel: the prevailing wander + the strength swell.
            float slowT = (float)(hours / Mathf.Max(0.01f, p.ChangeHours));
            float dirN = Noise(slowT + 17.0f, seed);      // [-1, 1]
            float strN = Noise(slowT + 137.0f, seed);     // [-1, 1]

            // Fast channel: the gusts (and a small direction veer that rides with them).
            float gustT = (float)(hours / Mathf.Max(0.01f, p.GustChangeHours));
            float gustN = Noise(gustT + 523.0f, seed);    // [-1, 1]

            float dirRad = p.PrevailingDirectionDeg * Mathf.Deg2Rad
                         + dirN  * p.DirectionWanderDeg * Mathf.Deg2Rad
                         + gustN * p.GustVeerDeg        * Mathf.Deg2Rad;

            float strength = p.MeanStrength
                           + strN  * p.StrengthVariability
                           + gustN * p.GustStrength;
            strength = Mathf.Clamp(strength, 0f, p.CalmMaxStrength);   // calm band — no storms in M1

            return new Vector2(Mathf.Cos(dirRad), Mathf.Sin(dirRad)) * strength;
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
