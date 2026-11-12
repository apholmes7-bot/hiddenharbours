using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The whole hull makes foam (the owner, 2026-11-02: "i would like the whole hull body to be responsive
    /// to making wake/foam, right now it just comes from stern from moving forward and looks off when going
    /// in reverse"). These are the owner's dials for where a hull churns the water besides her trail: the
    /// end she is driving into the water, and the side she is pushing through it. Presentation only: read
    /// by the foam injector, never by the boat.
    /// </summary>
    [Serializable]
    public struct HullFoamSettings
    {
        [Tooltip("Speed astern, m/s through the water, past which her trail is shed from the bow (the end " +
                 "that now trails), and speed ahead past which it returns to the transom. The gap between " +
                 "the two stops the trail swapping ends while she sits still. Zero uses 0.15.")]
        [Range(0f, 2f)] public float TrailFlipSpeed;

        [Tooltip("Weight of the foam the LEADING end throws (the bow ahead, the transom astern). " +
                 "0 = none, as the game shipped before.")]
        [Range(0f, 2f)] public float LeadWeight;
        [Tooltip("Speed along her length, m/s, at which the leading end's foam saturates. Zero uses 3.")]
        [Range(0f, 12f)] public float LeadSpeedKnee;
        [Tooltip("Half-length of the leading end's foam across her, in half-beams. Zero uses 0.8.")]
        [Range(0f, 2f)] public float LeadSpanHalfBeams;
        [Tooltip("Thickness of the leading end's foam, in half-beams. Zero uses 0.35.")]
        [Range(0f, 1f)] public float LeadRadiusHalfBeams;

        [Tooltip("Weight of the foam along the side she is pushing through the water (sliding, or the " +
                 "outside of a turn). 0 = none, as the game shipped before.")]
        [Range(0f, 2f)] public float SideWeight;
        [Tooltip("Speed sideways through the water, m/s, at which the side's foam saturates. Zero uses 1.2.")]
        [Range(0f, 6f)] public float SideSpeedKnee;
        [Tooltip("Thickness of the side's foam, in half-beams. Zero uses 0.3.")]
        [Range(0f, 1f)] public float SideRadiusHalfBeams;
        [Tooltip("How far toward each end the side's foam reaches, as a share of the half-length. Zero uses 0.7.")]
        [Range(0f, 1f)] public float SideReach;

        public static HullFoamSettings Default => new HullFoamSettings
        {
            TrailFlipSpeed = 0.15f,
            LeadWeight = 0.6f, LeadSpeedKnee = 3f, LeadSpanHalfBeams = 0.8f, LeadRadiusHalfBeams = 0.35f,
            SideWeight = 0.8f, SideSpeedKnee = 1.2f, SideRadiusHalfBeams = 0.3f, SideReach = 0.7f,
        };
    }
}
