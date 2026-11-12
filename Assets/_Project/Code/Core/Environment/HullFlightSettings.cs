using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A HULL CAN LEAVE THE WATER.</b> The owner, 2026-11-12: <i>"Boats also need airborn capability
    /// with impact and splashing if conditions/waves/speed would cause that. Water should react in
    /// realism."</i> Every number of that flight and its landing lives here (rule 6).
    ///
    /// <para>The law itself is <c>HullFlightMath</c> (Boats): the hull is a mass on the spring of her own
    /// waterplane, pushed up as hard as the water likes and pulled down by nothing but gravity. Meeting
    /// a steep face at speed she is thrown up faster than the back of the wave falls away, her keel
    /// clears the water, and she is in the air until she comes down on it. A slow hull follows the
    /// surface and never leaves it.</para>
    ///
    /// <para><see cref="Enabled"/> off: no hull ever flies, no speed is shed, no event is raised. That is
    /// the A/B, and the one number that reverses the change.</para>
    /// </summary>
    [Serializable]
    public struct HullFlightSettings
    {
        [Tooltip("Master switch. OFF = no hull ever leaves the water: no flight, no slam, no splash.")]
        public bool Enabled;

        [Tooltip("How far her keel must rise above the water under her before she is flying, as a share " +
                 "of her own draught. 1 = the whole keel is out. Lower flies sooner.")]
        [Range(0.25f, 2f)] public float KeelClearFraction;

        [Tooltip("Seconds her keel must stay clear before it counts as a flight. Shorter hops raise no " +
                 "event, shed no speed and make no splash.")]
        [Range(0f, 1f)] public float MinAirSeconds;

        [Tooltip("How much harder the water pushes a hull UP as she goes faster for her length (planing " +
                 "lift): her spring stiffens by 1 + this x her Froude number (speed / sqrt(g x length)). " +
                 "0 = a hull at any speed rides like one at rest, and almost nothing flies.")]
        [Range(0f, 6f)] public float PlaningStiffnessPerFroude;

        [Tooltip("Ceiling on that stiffening, as a multiple of her spring at rest.")]
        [Range(1f, 12f)] public float MaxPlaningStiffness;

        [Tooltip("Floor on the damping of her heave against the water, as a ratio of critical. At 1 or " +
                 "more she never bounces herself clear of a sea she is only bobbing in.")]
        [Range(0.3f, 3f)] public float MinDampingRatio;

        [Tooltip("The steepest face she can be carried up, as rise over run: the water lifts her no " +
                 "faster than FollowRiseBase + this x her speed. Steeper than that she punches through. " +
                 "This is the dial for how high a fast hull is thrown.")]
        [Range(0f, 1f)] public float MaxFollowSlope;

        [Tooltip("The fastest the water lifts a hull that is not making way, m/s.")]
        [Min(0f)] public float FollowRiseBase;

        [Tooltip("How deep she can be pushed under before the water carries her bodily, in draughts.")]
        [Range(1f, 6f)] public float MaxBuryDraughts;

        [Tooltip("Closing speed with the water, m/s, below which a landing is soft: no slam.")]
        [Min(0f)] public float SlamSpeedKnee;

        [Tooltip("Closing speed with the water, m/s, at which the slam is as hard as it gets.")]
        [Min(0.1f)] public float SlamFullSpeed;

        [Tooltip("Share of her speed a full slam takes off her. 0 = landing costs nothing.")]
        [Range(0f, 0.9f)] public float SpeedShedAtFullSlam;

        [Tooltip("How much of the height she flies at the picture adds to her drawn ride. 1 = true.")]
        [Range(0f, 3f)] public float DrawnClearanceScale;

        [Tooltip("Degrees her bow lifts per metre of air under her keel.")]
        [Range(0f, 30f)] public float NoseUpDegreesPerMetre;

        [Tooltip("Ceiling on the bow's lift in flight, degrees.")]
        [Range(0f, 30f)] public float MaxNoseUpDegrees;

        [Tooltip("Seconds the patch of foam she lands in keeps being laid, at a full slam.")]
        [Range(0f, 3f)] public float LandingFoamSeconds;

        [Tooltip("How far past her own foam band the landing patch spreads at a full slam, in multiples " +
                 "of that band.")]
        [Range(0f, 6f)] public float LandingFoamSpread;

        [Tooltip("Drops in the landing splash at a full slam.")]
        [Range(0, 64)] public int SlamSplashDrops;

        [Tooltip("Seconds the landing splash's drops live.")]
        [Range(0.05f, 3f)] public float SlamSplashSeconds;

        public static HullFlightSettings Default => new HullFlightSettings
        {
            Enabled = true,
            KeelClearFraction = 1f,
            MinAirSeconds = 0.15f,
            PlaningStiffnessPerFroude = 2f,
            MaxPlaningStiffness = 8f,
            MinDampingRatio = 1.5f,
            MaxFollowSlope = 0.2f,
            FollowRiseBase = 0.5f,
            MaxBuryDraughts = 2f,
            SlamSpeedKnee = 1.5f,
            SlamFullSpeed = 9f,
            SpeedShedAtFullSlam = 0.25f,
            DrawnClearanceScale = 1f,
            NoseUpDegreesPerMetre = 8f,
            MaxNoseUpDegrees = 12f,
            LandingFoamSeconds = 0.9f,
            LandingFoamSpread = 2f,
            SlamSplashDrops = 28,
            SlamSplashSeconds = 0.7f,
        };
    }
}
