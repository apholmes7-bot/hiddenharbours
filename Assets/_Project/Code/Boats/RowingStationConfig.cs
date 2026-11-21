using System;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Where the rower sits, braces and holds</b>, on a hull whose oars are mesh fittings. Serialized
    /// on the hull's <see cref="BoatVisualDef"/> beside the oars it dresses (rules 2 and 6), in the
    /// hull's rig frame (metres; +x starboard, +y forward, +z up). <see cref="Default"/> is the dory's,
    /// read off <c>doryIsoRig.js</c> and fitted to the figures as noted on each field.
    ///
    /// <para>The figure is solved onto these points each frame (Core's <c>RowingPoseSolver</c>):
    /// these are the knobs to move if a hand ever runs out of reach. Move the grip or the seat; the
    /// arm is never stretched to make up the difference.</para>
    /// </summary>
    [Serializable]
    public struct RowingStationConfig
    {
        [Tooltip("Master switch. Off = the rower plays his clip as authored, hands wherever it put them.")]
        public bool Enabled;

        [Tooltip("Where each hand holds its oar: metres INBOARD of the oarlock along the loom. The rig's " +
                 "inboard loom is 0.65 m, so 0.55 is a hand's width in from the handle's end: the hands work either " +
                 "side of his ribs, about 0.44 m apart at the finish.")]
        [Min(0f)] public float GripInboardMeters;

        [Tooltip("The oar's whole inboard loom, oarlock to handle end, metres (the rig's O_LIN, 0.65 on the " +
                 "dory: 5 cm short of the tholes' 0.697 m half-beam, so the two handles never meet). The handle " +
                 "end is kept out of his body with his arms.")]
        [Min(0f)] public float LoomInboardMeters;

        [Tooltip("The top of the thwart under his sitting bones. The dory's after thwart is at y -0.72, " +
                 "its top 0.3056 (the deck's helm station), 0.22 m fore and aft; he sits 4 cm toward its after " +
                 "edge, toward the tholes (0.369 m abaft its centre), which keeps the grips in the arm's reach " +
                 "through the whole stroke, the children's too.")]
        public Vector3 SeatLocalMeters;

        [Tooltip("The STARBOARD foot's brace: the point on the boards ahead of the thwart (toward the " +
                 "way he faces) where the sole rests; the port one mirrors x. The ankle is lifted above " +
                 "it by the clip's own ankle height. The dory's sits 0.72 m from the seat, on the boards just " +
                 "forward of the stern sheets: knees a little bent and low, so his hands ride well above them on the recovery.")]
        public Vector3 FootBraceLocalMeters;

        [Tooltip("How far he lies back at the finish, degrees, on top of the clip's own lean. Scaled " +
                 "by the rig's drive, so it peaks with his hands in.")]
        [Range(0f, 45f)] public float LaybackDegrees;

        [Tooltip("The most extra forward lean he may take to reach a grip at the catch, degrees.")]
        [Range(0f, 60f)] public float ReachLeanMaxDegrees;

        [Header("Clear of himself")]
        [Tooltip("How far the loom must pass from the centre of either knee, metres, through the whole " +
                 "stroke: a knee's half-thickness plus the loom's, plus air. The station's promise; the " +
                 "EditMode tests hold every skin to it.")]
        [Min(0f)] public float KneeClearanceMeters;

        [Tooltip("How far his hands and forearms (and the elbow end of the upper arm) stay outside his " +
                 "own torso, metres. When an arm would pass inside it the elbow is swung out first, and " +
                 "only if that is not enough does he lean back, by at most BodyClearLeanMaxDegrees.")]
        [Min(0f)] public float BodyClearanceMeters;

        [Tooltip("The most extra lean back he may take to keep his arms out of his body, degrees.")]
        [Range(0f, 30f)] public float BodyClearLeanMaxDegrees;

        [Tooltip("The most he may turn his shoulders toward the handles to keep his arms out of his body, " +
                 "degrees either way: the last resort, after the elbows and the lean, for a figure turned on " +
                 "the hull or a broad trunk.")]
        [Range(0f, 30f)] public float BodyClearTwistMaxDegrees;

        [Tooltip("How far above the top of either knee both palms ride through the recovery, metres: the " +
                 "station's promise that the hands travel over his thighs, not down in his lap. The EditMode " +
                 "tests hold every skin to it.")]
        [Min(0f)] public float HandAboveKneeMeters;

        [Tooltip("Seen from dead ahead (the boat's heading 0, the camera over her stern), how far above his " +
                 "hip line both palms are DRAWN through the late recovery and the catch, as a share of the drawn " +
                 "hip-to-shoulder height (0 = at the hips, 1 = at the shoulders). There the hands reach toward the " +
                 "camera, so the projection pulls them down; the tholes stand on chocks so they still read at his " +
                 "belly. The EditMode tests hold every skin to it.")]
        [Range(0f, 1f)] public float HandAboveHipLineShareFromAhead;

        [Tooltip("Half the thickness of the oar's loom, metres (the rig's O_TS): how far the wood reaches " +
                 "from the loom's centreline.")]
        [Min(0f)] public float LoomHalfThicknessMeters;

        [Tooltip("The air between the two looms where they pass closest, metres, through the whole stroke: " +
                 "the handles stay apart and never pass through each other. The EditMode tests hold the " +
                 "station to it.")]
        [Min(0f)] public float LoomClearanceMeters;

        [Header("Resting")]
        [Tooltip("Where the blades settle in a rowing style's rest between strokes: the dip, degrees. 17 " +
                 "(off the chocked tholes) hangs the blade tips a few centimetres over the water, feathered, handles at his belly.")]
        [Range(0f, 30f)] public float RestBladeDipDegrees;

        [Tooltip("How far he slumps forward when resting, degrees: the shoulders come down and round.")]
        [Range(0f, 30f)] public float RestSlumpDegrees;

        [Tooltip("How far each breath rocks him while resting, degrees either side.")]
        [Range(0f, 10f)] public float RestBreathDegrees;

        [Tooltip("Seconds per breath with the oars resting (shipped or trailing). A style's rest between " +
                 "strokes breathes once per rest instead.")]
        [Min(0.1f)] public float RestBreathSeconds;

        [Tooltip("With the oars let go, where each hand rests along his thigh: 0 at the hip, 1 at the knee.")]
        [Range(0f, 1f)] public float RestHandAlongThigh;

        [Tooltip("With the oars let go, how high above the thigh bone his palm rests, metres (the thigh's " +
                 "own half-thickness).")]
        [Min(0f)] public float RestHandAboveThighMeters;

        public static RowingStationConfig Default => new RowingStationConfig
        {
            Enabled = true,
            GripInboardMeters = 0.55f,
            LoomInboardMeters = 0.65f,
            SeatLocalMeters = new Vector3(0f, -0.76f, 0.3056f),
            FootBraceLocalMeters = new Vector3(0.16f, -1.48f, 0.115f),
            LaybackDegrees = 6f,
            ReachLeanMaxDegrees = 40f,
            KneeClearanceMeters = 0.11f,
            BodyClearanceMeters = 0.02f,
            BodyClearLeanMaxDegrees = 12f,
            BodyClearTwistMaxDegrees = 15f,
            HandAboveKneeMeters = 0.20f,
            HandAboveHipLineShareFromAhead = 0.13f,
            LoomHalfThicknessMeters = 0.055f,
            LoomClearanceMeters = 0.02f,
            RestBladeDipDegrees = 17f,
            RestSlumpDegrees = 8f,
            RestBreathDegrees = 1.5f,
            RestBreathSeconds = 4f,
            RestHandAlongThigh = 0.86f,
            RestHandAboveThighMeters = 0.07f,
        };
    }
}
