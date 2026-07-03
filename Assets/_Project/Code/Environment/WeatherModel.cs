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

        // The wind-strength (m/s) band edges of the canon sea-state scale (rough Beaufort-ish).
        // SeaBandEdges[k] is where the enum flips from state k-1 to state k; the LAST edge is where
        // Storm begins. Shared by BOTH SeaFromWind (the stepped enum, gameplay gates + the HUD readout)
        // and SeaState01 (the continuous presentation axis) so the two can never drift apart.
        private static readonly float[] SeaBandEdges = { 0f, 0.5f, 2f, 4f, 6f, 8f, 11f, 14f };

        /// <summary>Map wind strength (m/s) to the canon sea-state scale (rough Beaufort-ish).
        /// STEPPED by design — gameplay gates (e.g. <c>MaxSafeSeaState</c>) and the HUD readout want
        /// discrete bands. Presentation consumers (chop, palette, dim, mist, wake) must use the
        /// continuous <see cref="SeaState01"/> instead so the look never pops at a band edge.</summary>
        public static SeaState SeaFromWind(float strength)
        {
            for (int k = 1; k < SeaBandEdges.Length; k++)
                if (strength < SeaBandEdges[k]) return (SeaState)(k - 1);
            return SeaState.Storm;
        }

        /// <summary>
        /// The CONTINUOUS sea-state axis (0..1): the piecewise-linear inverse of the
        /// <see cref="SeaFromWind"/> thresholds. Monotonic in wind strength, clamped to [0, 1], and it
        /// equals the enum's normalised value (<c>(int)state / 7</c>) EXACTLY at every band edge — so
        /// converting a consumer from <c>(int)SeaFromWind(w)/7</c> to this is a pure de-quantization:
        /// identical output at the enum's flip points, smooth in between, no re-tune. Above the Storm
        /// edge it saturates at 1. A pure function of the wind strength (itself deterministic from
        /// (worldSeed, gameTime) — rule 5): no smoothing state, nothing saved; the enum's threshold
        /// chatter in a dithering wind becomes a small smooth wiggle here instead of a 1/7 jump.
        /// </summary>
        public static float SeaState01(float strength)
        {
            int top = SeaBandEdges.Length - 1;                      // 7 — the Storm edge index
            if (strength <= 0f) return 0f;
            if (strength >= SeaBandEdges[top]) return 1f;
            for (int k = 1; k <= top; k++)
            {
                if (strength < SeaBandEdges[k])
                {
                    float t = (strength - SeaBandEdges[k - 1]) / (SeaBandEdges[k] - SeaBandEdges[k - 1]);
                    return (k - 1 + t) / top;
                }
            }
            return 1f;   // unreachable (the >= top edge case returned above); keeps the compiler happy
        }

        // --- deterministic 1D value noise in [-1, 1] ---
        // Internal so sibling Environment sims (the tidal-current wander, CurrentModel) reuse the
        // EXACT same hash/value-noise — one deterministic noise source for the whole module, no new
        // RNG, no UnityEngine.Random (CLAUDE.md rule 5).
        internal static float Noise(float x, int seed)
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
