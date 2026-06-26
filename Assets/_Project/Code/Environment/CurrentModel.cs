using UnityEngine;

namespace HiddenHarbours.Environment
{
    /// <summary>
    /// The deterministic tidal-<b>current</b> field: the set &amp; drift the boat floats in
    /// (design/time-tides-weather.md §3.7). Pure and engine-light (no MonoBehaviour, no GameConfig,
    /// no state) so it is trivially EditMode-testable and cheap at the 4 Hz tick.
    ///
    /// <para>The current runs along a region's prevailing <b>channel axis</b> (flood-positive), its
    /// magnitude following the tide's rate-of-change (peak at mid-tide, ~0 at slack). On top of that,
    /// the <b>bearing gently wanders</b> over time on its OWN slow timescale (<see cref="CurrentProfile.DriftHours"/>)
    /// — distinct from the wind's <see cref="WindProfile.ChangeHours"/> and from the tide's ~12.42 h
    /// semidiurnal period — so the set of the tide evolves "at its own rate". The wander is a value-noise
    /// rotation that reuses the module's single <see cref="WeatherModel"/> noise (no new RNG,
    /// no UnityEngine.Random), so the current is fully deterministic from <c>(worldSeed, gameTime)</c>
    /// and nothing is saved (CLAUDE.md rule 5).</para>
    ///
    /// <para><b>Gentle by design.</b> This vector also drives <b>boat drift &amp; mooring</b>
    /// (<c>BoatController</c> sails on <c>vel − CurrentVector</c>), so the wander is a sailing-feel change
    /// too — <see cref="CurrentProfile.WanderDeg"/> is kept small and <see cref="CurrentProfile.DriftHours"/>
    /// slow so the set stays readable.</para>
    /// </summary>
    public static class CurrentModel
    {
        /// <summary>
        /// World-space tidal current (m/s). The channel axis is rotated by a slow, deterministic
        /// value-noise angle, then scaled by the signed tide rate so flood/ebb still flips along the
        /// (wandered) channel and the magnitude peaks at mid-tide.
        /// </summary>
        /// <param name="totalSeconds">Master clock value (in-game seconds) — the only time input.</param>
        /// <param name="seed">World seed — the only entropy source (no hidden RNG).</param>
        /// <param name="secondsPerHour">In-game seconds per in-game hour (<c>GameConfig.SecondsPerHour</c>).</param>
        /// <param name="channelAxis">Prevailing flood bearing for the region (need not be normalized).</param>
        /// <param name="currentFactor">Scales the tide rate-of-change into a current speed (m/s).</param>
        /// <param name="tideRate">Signed tide rate-of-change (<c>TideModel.Rate</c>, m / in-game second);
        /// sign flips flood↔ebb and magnitude peaks at mid-tide. <paramref name="currentFactor"/> scales
        /// it to a speed.</param>
        /// <param name="p">Region current character. A default/unset profile heals to Coddle Cove.</param>
        public static Vector2 SampleCurrent(double totalSeconds, int seed, double secondsPerHour,
                                            Vector2 channelAxis, float currentFactor, float tideRate,
                                            CurrentProfile p)
        {
            if (p.DriftHours <= 0f) p = CurrentProfile.CoddleCove;   // heal a default/unset profile

            Vector2 axis = channelAxis.sqrMagnitude > 1e-12f ? channelAxis.normalized : Vector2.right;

            float wanderRad = WanderAngleRad(totalSeconds, seed, secondsPerHour, p);
            Vector2 wandered = Rotate(axis, wanderRad);

            return wandered * (tideRate * currentFactor);
        }

        /// <summary>
        /// The deterministic bearing-wander angle (radians) applied to the channel axis at this time —
        /// exposed so tests can assert the wander envelope independently of the tide-rate magnitude.
        /// A single slow value-noise channel on the current's OWN timescale, smooth (smoothstep fade,
        /// C1 — never pops) and zero-mean, so the long-run mean bearing stays on the channel axis.
        /// </summary>
        public static float WanderAngleRad(double totalSeconds, int seed, double secondsPerHour, CurrentProfile p)
        {
            if (p.DriftHours <= 0f) p = CurrentProfile.CoddleCove;

            double hours = totalSeconds / System.Math.Max(1e-6, secondsPerHour);
            float driftT = (float)(hours / Mathf.Max(0.01f, p.DriftHours));
            // Offset (+613) keys this onto a different lane of the shared noise than wind dir/strength/gust/fog,
            // so the current wander is independent of the wind wander (its own rate AND its own phase).
            float driftN = WeatherModel.Noise(driftT + 613.0f, seed);   // [-1, 1], zero-mean
            return driftN * p.WanderDeg * Mathf.Deg2Rad;
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
