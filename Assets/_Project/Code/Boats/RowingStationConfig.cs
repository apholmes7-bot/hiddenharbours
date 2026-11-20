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
                 "inboard loom is 0.85 m, so 0.70 is a hand's width in from the handle's end.")]
        [Min(0f)] public float GripInboardMeters;

        [Tooltip("The top of the thwart under his sitting bones. The dory's after thwart is at y -0.72, " +
                 "its top 0.3056 (the deck's helm station); he sits on its centre, which, with the tholes " +
                 "0.369 m abaft it, keeps the grips in the arm's reach through the whole stroke.")]
        public Vector3 SeatLocalMeters;

        [Tooltip("The STARBOARD foot's brace: the point on the boards ahead of the thwart (toward the " +
                 "way he faces) where the sole rests; the port one mirrors x. The ankle is lifted above " +
                 "it by the clip's own ankle height.")]
        public Vector3 FootBraceLocalMeters;

        [Tooltip("How far he lies back at the finish, degrees, on top of the clip's own lean. Scaled " +
                 "by the rig's drive, so it peaks with his hands in.")]
        [Range(0f, 45f)] public float LaybackDegrees;

        [Tooltip("The most extra forward lean he may take to reach a grip at the catch, degrees.")]
        [Range(0f, 60f)] public float ReachLeanMaxDegrees;

        public static RowingStationConfig Default => new RowingStationConfig
        {
            Enabled = true,
            GripInboardMeters = 0.70f,
            SeatLocalMeters = new Vector3(0f, -0.72f, 0.3056f),
            FootBraceLocalMeters = new Vector3(0.16f, -1.35f, 0.1098f),
            LaybackDegrees = 8f,
            ReachLeanMaxDegrees = 30f,
        };
    }
}
