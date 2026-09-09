using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Per-region wind character for the deterministic wind field (VS-05). Mirrors
    /// <see cref="HiddenHarbours.Core.TideProfile"/>: regions carry their own prevailing wind +
    /// liveliness, so the same clock blows a gentle SW'ly over Coddle Cove while other regions
    /// differ later. The wind tunables live HERE (not on <c>GameConfig</c>) so the wind FEEL is
    /// authored in the Environment lane and can move onto a RegionDef in M2 without a sim change.
    ///
    /// <para>⚠️ <b>The calm band is over (owner ruling, 2026-09-09).</b> Every rung of the
    /// sea-state ladder — Glass through Storm — is now reachable. What was holding it down was
    /// <b>not</b> <see cref="CalmMaxStrength"/>: measured over a game week on nine seeds, the wind
    /// ranged 0.58—5.33 m/s against a cap of 5.7, so <b>the cap never bound</b> and removing it
    /// would have changed nothing. The ceiling was the law's own amplitudes.</para>
    ///
    /// <para>A gale is not a big gust, it is a <b>weather system</b>, so the strength is now three
    /// channels rather than two: the slow swell, the fast gusts, and a much slower, deliberately
    /// SKEWED system channel that sits near zero most of the time and occasionally walks a depression
    /// through. That is what makes a Storm reachable without making every day rough (P5: cozy, but
    /// the sea is dangerous).</para>
    ///
    /// <para>⚠️ <b>The system channel is a WORLD fact, not a region one.</b> Its noise stream and
    /// its shape live on <c>WeatherModel</c> and are shared by every region; a region scales only its
    /// STRENGTH (<see cref="SystemStrength"/>). Otherwise Nine Mile Creek could blow a gale while
    /// St Peters, ten kilometres away on the same island, lay glass.</para>
    /// </summary>
    [System.Serializable]
    public struct WindProfile
    {
        [Tooltip("Prevailing bearing the wind blows TOWARD, in math degrees (0 = +x/East, 90 = +y/North). " +
                 "A Coddle Cove SW'ly blows toward the NE ≈ 45°.")]
        public float PrevailingDirectionDeg;
        [Tooltip("How far (± degrees) the direction wanders off prevailing on the slow channel.")]
        public float DirectionWanderDeg;

        [Tooltip("Baseline wind speed (m/s) — the calm mean.")]
        public float MeanStrength;
        [Tooltip("Slow ± swing (m/s) of strength about the mean.")]
        public float StrengthVariability;

        [Tooltip("Gust amplitude (m/s) on the fast channel — the liveliness.")]
        public float GustStrength;
        [Tooltip("Small direction veer (± degrees) that rides along with each gust.")]
        public float GustVeerDeg;

        [Tooltip("Slow-channel timescale (in-game hours): the prevailing wander + strength swell.")]
        public float ChangeHours;
        [Tooltip("Fast-channel timescale (in-game hours): the gusts.")]
        public float GustChangeHours;

        [Tooltip("Hard ceiling on wind speed (m/s). ⚠ This is a RAIL, not the tuning: the law's own " +
                 "amplitudes decide what the weather does, and this only stops an absurd profile. It " +
                 "sat at 5.7 through M1 and never once bound - the law could not reach it.")]
        public float CalmMaxStrength;

        [Tooltip("How hard a passing weather SYSTEM blows here (m/s added at the system channel's " +
                 "peak). This is the region's exposure: an open headland scales it up, a sheltered " +
                 "inlet down. The system's TIMING and SHAPE are the world's (WeatherModel), shared by " +
                 "every region - only this scale is local, so two regions are never in different " +
                 "weather on the same day.")]
        public float SystemStrength;

        /// <summary>
        /// The greybox vertical-slice region: a gentle, lively south-westerly. Calm band only —
        /// mean + slow swing + gust peaks at <see cref="CalmMaxStrength"/> (≤ Moderate on the sea scale).
        /// </summary>
        public static WindProfile CoddleCove => new WindProfile
        {
            PrevailingDirectionDeg = 45f,   // SW'ly: the wind blows toward the NE
            DirectionWanderDeg     = 35f,
            MeanStrength           = 2.2f,  // dropped from 3 so the floor can reach GLASS: the old
                                            // law bottomed at 0.5 m/s, exactly the Glass/Calm edge,
                                            // so a mirror was unreachable as well as a gale.
            StrengthVariability    = 2.0f,
            GustStrength           = 1.2f,  // base band = [0, 5.2] m/s before the system channel
            GustVeerDeg            = 10f,
            ChangeHours            = 6f,
            GustChangeHours        = 0.4f,  // gusts surge over a couple of in-game minutes
            SystemStrength         = 12f,   // a depression's own contribution at its peak. With the
                                            // world channel's skew this is near 0 most of the time;
                                            // the numbers it produces are in WindUncapReachTests.
            CalmMaxStrength        = 24f,   // a RAIL well above anything the law produces (measured
                                            // peak ~18 m/s), not a tuning. It must not bind.
        };
    }
}
