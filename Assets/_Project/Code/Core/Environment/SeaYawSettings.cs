using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Every constant of the <b>sea yaw</b> (the wind and the sea turn a hull's head, P1), named and
    /// owner-tunable (rule 6). Carried on <c>GameConfig.SeaYaw</c>; the Boats-side law that consumes it
    /// is <c>SeaYawMath</c>. Lives in Core beside <see cref="SeakeepingSettings"/> for the same reason:
    /// GameConfig is Core and Core cannot reference Boats.
    ///
    /// <para><b>The unit is a share of her own helm.</b> Every strength here is quoted as the fraction
    /// of the hull's FULL helm torque (rudder at full authority, or the oars pulled full differential)
    /// that the wind or sea asks of her. That makes the two promises hold by construction across a
    /// ladder from a 400 kg dory to a 7,600 t tanker: the yaw is felt in proportion to how she steers,
    /// and <see cref="MaxHelmShare"/> below 1 means the rudder always wins once she has way on.</para>
    ///
    /// <para><b>Nothing changes in calm water or at the dock.</b> Wind under
    /// <see cref="CalmWindMetresPerSecond"/> turns nothing, a glass sea has no slope to turn her with,
    /// sheltered water (the seakeeping exposure ramp) scales both to zero, and a hull with a line out to
    /// the shore is held by it.</para>
    /// </summary>
    [Serializable]
    public struct SeaYawSettings
    {
        [Tooltip("Master switch. Off restores the handling from before the sea could turn a hull's head, exactly.")]
        public bool Enabled;

        [Header("Wind (the gusts nudge her head)")]
        [Tooltip("Wind (m/s) at and under which the wind turns nothing: light airs leave the heading alone. " +
                 "Above it the turn grows with the wind, so every gust in the weather model nudges the bow.")]
        [Min(0f)] public float CalmWindMetresPerSecond;

        [Tooltip("Wind (m/s) at which a beam wind asks the reference hull for exactly WindHelmShare of her helm. " +
                 "Must be above CalmWindMetresPerSecond.")]
        [Min(0.01f)] public float ReferenceWindMetresPerSecond;

        [Tooltip("Share of her full helm (0..1) a beam wind at the reference speed asks of the reference hull " +
                 "(a dory: ReferenceMassKg, ReferenceWindExposure). Lighter or more exposed hulls are asked more, " +
                 "heavier ones less. This is the main wind dial.")]
        [Range(0f, 1f)] public float WindHelmShare;

        [Tooltip("Which way the wind turns a hull with no sails drawing. +1 = her windage sits forward of her " +
                 "pivot and the bow blows off downwind (a motorboat or a rowed boat drifting); -1 = she " +
                 "weathervanes head to wind. The size scales the turn.")]
        public float HullWindYawBias;

        [Tooltip("The same bias while her anchor is down. The rode holds her by the bow, so she lies head to " +
                 "wind: negative.")]
        public float AnchoredWindYawBias;

        [Header("Waves (a quartering or following sea makes the stern wander)")]
        [Tooltip("Share of her full helm (0..1) asked by a difference of ReferenceWaveSlope between the sea's " +
                 "beam slope under her bow and under her stern. That difference is what turns a hull: it is " +
                 "largest on a quartering sea and lasts longest in a following sea, where she meets the waves " +
                 "slowly. Scaled per hull by her seakeeping response (a dory corks about, a trader shrugs).")]
        [Range(0f, 1f)] public float WaveHelmShare;

        [Tooltip("The bow-to-stern beam-slope difference that asks for exactly WaveHelmShare (dimensionless slope).")]
        [Min(0.0001f)] public float ReferenceWaveSlope;

        [Tooltip("How far from her middle the bow and stern are read, as a fraction of the dominant wavelength, " +
                 "when that is shorter than half her length. A hull longer than the waves bridges them and is " +
                 "turned less; 0.25 = a quarter wave either side.")]
        [Range(0f, 0.5f)] public float WaveSpanWavelengthFraction;

        [Header("Hull scaling (light hulls are pushed more)")]
        [Tooltip("Mass (kg) of the reference hull the shares are quoted for. The dory is 400.")]
        [Min(1f)] public float ReferenceMassKg;

        [Tooltip("How fast the wind's turn falls with mass: susceptibility x (ReferenceMassKg / MassKg)^this. " +
                 "0 = mass does not matter; higher = heavy hulls shrug the wind off sooner.")]
        [Min(0f)] public float MassExponent;

        [Tooltip("The hull WindExposure the shares are quoted for. A hull with twice the exposure is turned twice as hard.")]
        [Min(0.01f)] public float ReferenceWindExposure;

        [Tooltip("Ceiling on a hull's susceptibility to the wind and the sea, so a very light, very exposed " +
                 "hull is still never thrown about.")]
        [Min(0f)] public float MaxSusceptibility;

        [Header("Never a fight")]
        [Tooltip("The most of her helm the wind and the sea together may ask (0..1). Under 1, the rudder wins " +
                 "whenever she has way on; under the autopilot's MaxHelm, the autopilot can hold her too.")]
        [Range(0f, 1f)] public float MaxHelmShare;

        /// <summary>
        /// The shipped first-feel tuning: noticeable on a windy, rough day, never a fight. A beam wind of
        /// 10 m/s asks a dory for a fifth of her helm (a heavy cape islander for about a twentieth); a
        /// quartering sea's peak asks a dory for about the same again; together they are capped at 45%,
        /// under the autopilot's 60% MaxHelm.
        /// </summary>
        public static SeaYawSettings Default => new SeaYawSettings
        {
            Enabled = true,
            CalmWindMetresPerSecond = 3f,
            ReferenceWindMetresPerSecond = 10f,
            WindHelmShare = 0.2f,
            HullWindYawBias = 1f,
            AnchoredWindYawBias = -1f,
            WaveHelmShare = 0.2f,
            ReferenceWaveSlope = 0.1f,
            WaveSpanWavelengthFraction = 0.25f,
            ReferenceMassKg = 400f,
            MassExponent = 0.35f,
            ReferenceWindExposure = 0.6f,
            MaxSusceptibility = 1.25f,
            MaxHelmShare = 0.45f,
        };
    }
}
