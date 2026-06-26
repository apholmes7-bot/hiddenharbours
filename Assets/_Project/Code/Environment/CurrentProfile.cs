using UnityEngine;

namespace HiddenHarbours.Environment
{
    /// <summary>
    /// Per-region tidal-current character for the deterministic current field. Mirrors
    /// <see cref="WindProfile"/>: the current has a <b>prevailing channel axis</b> (the flood-runs-this-way
    /// bearing, carried by <c>EnvironmentService._channelAxis</c>) whose heading <b>gently wanders</b> over
    /// time on its OWN slow timescale — distinct from the wind's <see cref="WindProfile.ChangeHours"/> and
    /// from the tide's ~12.42 h semidiurnal period — so the set of the tide evolves "at its own rate"
    /// (design/time-tides-weather.md §3.7).
    ///
    /// <para>The wander is a pure value-noise rotation of the channel axis (no new RNG — it reuses the
    /// module's one <see cref="WeatherModel"/> noise), so the current is fully deterministic from
    /// <c>(worldSeed, gameTime)</c> and nothing is saved (CLAUDE.md rule 5).</para>
    ///
    /// <para><b>Gentle by design.</b> The current vector also drives <b>boat drift &amp; mooring</b> (the P1
    /// "what you see is what you sail" coupling: <c>BoatController</c> uses <c>vel − CurrentVector</c>), so a
    /// swing in the current heading is also a swing in sailing feel. <see cref="WanderDeg"/> is kept SMALL
    /// and <see cref="DriftHours"/> SLOW so sailing stays predictable; the owner tunes from here.</para>
    /// </summary>
    [System.Serializable]
    public struct CurrentProfile
    {
        [Tooltip("How far (± degrees) the tidal-current bearing wanders off the prevailing channel axis. " +
                 "Kept SMALL — the current drives boat drift, so a big swing is a big sailing-feel change.")]
        public float WanderDeg;

        [Tooltip("Slow timescale (in-game hours) of the bearing wander — the current's OWN rate, distinct " +
                 "from the wind's ChangeHours and the tide's ~12.42 h period. Larger = slower, smoother.")]
        public float DriftHours;

        /// <summary>
        /// The greybox vertical-slice default: a gentle, slow wander. The bearing eases ±18° off the
        /// channel axis over a long, ~9 in-game-hour cycle (slower than the wind's 6 h slow channel and not
        /// a harmonic of the 12.42 h tide), so the set stays readable and predictable for sailing.
        /// </summary>
        public static CurrentProfile CoddleCove => new CurrentProfile
        {
            WanderDeg  = 18f,   // gentle: a small lean off the channel, never a wild swing
            DriftHours = 9f,    // slow + its own rate: not the wind's 6 h, not a tide harmonic
        };
    }
}
