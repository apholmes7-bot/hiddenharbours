using UnityEngine;

namespace HiddenHarbours.Environment
{
    /// <summary>
    /// Per-region wind character for the deterministic wind field (VS-05). Mirrors
    /// <see cref="HiddenHarbours.Core.TideProfile"/>: regions carry their own prevailing wind +
    /// liveliness, so the same clock blows a gentle SW'ly over Coddle Cove while other regions
    /// differ later. The wind tunables live HERE (not on <c>GameConfig</c>) so the wind FEEL is
    /// authored in the Environment lane and can move onto a RegionDef in M2 without a sim change.
    ///
    /// <para>M1 is <b>calm band only</b> — <see cref="CalmMaxStrength"/> hard-caps the speed so the
    /// sea never climbs past a cosy Moderate. Storms are M2.</para>
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

        [Tooltip("Hard cap on wind speed (m/s) that keeps M1 in the calm band (no storms).")]
        public float CalmMaxStrength;

        /// <summary>
        /// The greybox vertical-slice region: a gentle, lively south-westerly. Calm band only —
        /// mean + slow swing + gust peaks at <see cref="CalmMaxStrength"/> (≤ Moderate on the sea scale).
        /// </summary>
        public static WindProfile CoddleCove => new WindProfile
        {
            PrevailingDirectionDeg = 45f,   // SW'ly: the wind blows toward the NE
            DirectionWanderDeg     = 35f,
            MeanStrength           = 3f,    // ~Light air/breeze
            StrengthVariability    = 1.3f,
            GustStrength           = 1.2f,  // natural peak = 3 + 1.3 + 1.2 = 5.5 m/s (Moderate)
            GustVeerDeg            = 10f,
            ChangeHours            = 6f,
            GustChangeHours        = 0.4f,  // gusts surge over a couple of in-game minutes
            CalmMaxStrength        = 5.7f,  // safety net just above the natural peak, below the
                                            // 6 m/s Lively boundary — sea stays ≤ Moderate, never stormy
        };
    }
}
