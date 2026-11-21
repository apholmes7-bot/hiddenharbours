using System;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Where the skipper sits, braces and holds on a boat steered by a tiller outboard</b> with no
    /// helm console. Serialized on the hull's <see cref="BoatVisualDef"/> beside the engine it serves
    /// (rules 2 and 6), in the hull's rig frame (metres; +x starboard, +y forward, +z up). Read only
    /// when the hull being drawn has her engine FITTED (<see cref="BoatVisualDef.SeatsAtTiller"/>): the
    /// dory and the dory with her motor share one visual, and only the second has a tiller to sit at.
    ///
    /// <para>The figure is solved onto these points each frame by the rowing station's own solver
    /// (Core's <c>RowingPoseSolver</c>): hips on the bench, feet on the boards, the hand nearer the
    /// tiller on its grip and the other hand on his thigh. Move the seat or the braces if a hand runs
    /// out of reach; the arm is never stretched to make up the difference.</para>
    /// </summary>
    [Serializable]
    public struct TillerStationConfig
    {
        [Tooltip("Master switch. Off = the skipper of this hull is drawn as the hull's own pilot stance " +
                 "says (a rower at the oars, a standing helmsman), with no tiller seat.")]
        public bool Enabled;

        [Tooltip("The top of the bench under his sitting bones. The dory's stern bench (the motor build) spans " +
                 "y -1.795 to -1.535 and x -0.41 to 0.41, its top at 0.3584; he sits off the " +
                 "centreline to port, beside the engine, so the tiller works at his right hand.")]
        public Vector3 SeatLocalMeters;

        [Tooltip("The PORT foot's brace on the boards ahead of the bench, metres.")]
        public Vector3 PortFootBraceLocalMeters;

        [Tooltip("The STARBOARD foot's brace on the boards ahead of the bench, metres.")]
        public Vector3 StarboardFootBraceLocalMeters;

        [Tooltip("The skin clip he is drawn from while seated at the tiller (a seated clip the solve then " +
                 "puts on the bench, the boards and the grip). Empty, or a clip his skin lacks, keeps the " +
                 "standing pilot.")]
        public string SeatedState;

        [Tooltip("Where the figure's root stands while seated (his floor origin), metres: the boards " +
                 "under the bench. Not the cushion point; the solver lifts his hips onto the bench.")]
        public Vector3 FloorOriginLocalMeters;

        [Tooltip("Which way he faces, as a deck bearing in degrees (0 = the bow, + toward starboard): " +
                 "forward and a little inboard, toward the engine he is minding.")]
        [Range(-90f, 90f)] public float FacingDeckBearingDegrees;

        [Tooltip("The tiller grip's centre on the ENGINE fitting at rest (dead ahead, untilted), in that " +
                 "fitting's own rig frame, metres. The punt's tiller outboard (worn by the dory on loan) " +
                 "has it at GRIP_L = (0, 0.685, 0.627) motor-local, YA = -2.66, so (0, -1.975, 0.627). It " +
                 "is turned with the engine and shifted by the visual's MotorMeshFitmentOffsetMeters.")]
        public Vector3 TillerGripAtRestLocalMeters;

        [Tooltip("How far forward he leans sitting easy with the engine ticking over, degrees.")]
        [Range(0f, 30f)] public float IdleLeanDegrees;

        [Tooltip("How far he sits up out of that easy lean at full throttle (ahead or astern), degrees, " +
                 "scaled by the throttle's size: he straightens and braces as she gets under way. Equal to " +
                 "the idle lean = bolt upright at full throttle. Sitting up (rather than leaning forward) " +
                 "also keeps the tiller, which lies behind his hip, inside every skin's reach.")]
        [Range(0f, 30f)] public float SitUpPerThrottleDegrees;

        [Tooltip("How far each breath rocks him, degrees either side.")]
        [Range(0f, 10f)] public float BreathDegrees;

        [Tooltip("Seconds per breath.")]
        [Min(0.1f)] public float BreathSeconds;

        [Tooltip("Where the free hand rests along his thigh: 0 at the hip, 1 at the knee.")]
        [Range(0f, 1f)] public float RestHandAlongThigh;

        [Tooltip("How high above the thigh bone the free palm rests, metres.")]
        [Min(0f)] public float RestHandAboveThighMeters;

        [Tooltip("The most extra forward lean he may take to keep the grip in reach, degrees (a hard-over " +
                 "tiller pushed away from him).")]
        [Range(0f, 60f)] public float ReachLeanMaxDegrees;

        [Tooltip("How far his hands and forearms stay outside his own torso, metres.")]
        [Min(0f)] public float BodyClearanceMeters;

        [Tooltip("The most extra lean back he may take to keep his arms out of his body, degrees.")]
        [Range(0f, 30f)] public float BodyClearLeanMaxDegrees;

        [Tooltip("The most he may turn his shoulders to keep his arms out of his body, degrees either way.")]
        [Range(0f, 45f)] public float BodyClearTwistMaxDegrees;

        /// <summary>Off: no hull sits at a tiller until her visual says so.</summary>
        public static TillerStationConfig Default => new TillerStationConfig
        {
            Enabled = false,
            SeatedState = "",
            BreathSeconds = 4.5f,
        };
    }
}
