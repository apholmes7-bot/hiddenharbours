using System;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Where the oars lie while the boat is under power</b> (owner ruling 2026-11-22: "the oars
    /// will stay stowed lying lengthwise against the hull"). Serialized on the hull's
    /// <see cref="BoatVisualDef"/> beside the oars it dresses (rules 2 and 6), in the hull's rig frame
    /// (metres; +x starboard, +y forward, +z up). <see cref="Default"/> is the dory's, fitted to
    /// <c>doryIsoRig.js</c> as noted on each field.
    ///
    /// <para><b>Stowed is not shipped.</b> Shipped (the rig's <c>oarPose('resting')</c>) leaves each
    /// oar in its tholes with the blade out over the water: the right look for a rower resting on his
    /// oars, and what the rowed dory still draws. Stowed takes the oars OUT of the tholes and lays them
    /// fore and aft inside the boat, on the thwarts, blades forward: the look of a boat driven by her
    /// motor. It is a mesh-path placement only (a rotation about the oar's own oarlock plus a shift of
    /// the whole fitting, the same two channels every fitting already has), so nothing is baked.</para>
    ///
    /// <para>The port oar is authored; the starboard one mirrors it across the centreline.</para>
    /// </summary>
    [Serializable]
    public struct OarStowConfig
    {
        [Tooltip("Master switch. Off = an oar under power is drawn shipped in its tholes, as before the " +
                 "2026-11-22 ruling.")]
        public bool Enabled;

        [Tooltip("The stowed oar's sweep, degrees, in the rig's own oar angles (0 = straight out abeam, " +
                 "-90 = blade straight forward). Past -90 angles the blade in toward the centreline: the " +
                 "dory's -95 keeps the blade inside her narrowing bow while the handle lies along the side.")]
        [Range(-180f, 180f)] public float SweepDegrees;

        [Tooltip("The stowed oar's dip, degrees (+ = blade end down). 0 lies it level across the two rowing " +
                 "thwarts, whose tops differ by 2 mm on the dory.")]
        [Range(-30f, 30f)] public float DipDegrees;

        [Tooltip("Where the PORT oar's oarlock point (the spot on the loom that sits in the tholes when " +
                 "rowing) lies when stowed, rig metres. The starboard oar mirrors x. The dory's lies 0.38 m " +
                 "out from the centreline, inboard of the thwart knees; 0.33 m abaft amidships, which puts " +
                 "the handle end 0.15 m abaft the after thwart and the blade tip 0.15 m short of the bow " +
                 "thwart; and at 0.363, the forward thwart's top plus the loom's half thickness, so the loom " +
                 "rests on both rowing thwarts.")]
        public Vector3 PortLockLocalMeters;

        [Tooltip("How far nearer the camera the stowed oar is drawn, metres, straight along the view axis " +
                 "(so it does not move on screen; only its depth changes). The baked oar carries the rig's " +
                 "own depth bias (doryIsoRig.js buildOar: loom db -0.15, blade -0.2), which tucks a ROWING oar " +
                 "under the gunwale at the tholes; lying on a thwart, that same bias buries the loom in the " +
                 "thwart it rests on. 0.15 cancels the loom's bias, so it is drawn at its true depth.")]
        [Min(0f)] public float CameraLiftMeters;

        /// <summary>The stowed oar's lock point for <paramref name="side"/> (-1 port, +1 starboard):
        /// the authored port point, mirrored across the centreline for starboard.</summary>
        public Vector3 LockLocalMeters(int side) =>
            side < 0 ? PortLockLocalMeters
                     : new Vector3(-PortLockLocalMeters.x, PortLockLocalMeters.y, PortLockLocalMeters.z);

        /// <summary>The stowed pose in the rig's two oar angles.</summary>
        public DoryOarMeshPose.Pose Pose => new DoryOarMeshPose.Pose(SweepDegrees, DipDegrees);

        public static OarStowConfig Default => new OarStowConfig
        {
            Enabled = true,
            SweepDegrees = -95f,
            DipDegrees = 0f,
            PortLockLocalMeters = new Vector3(-0.38f, -0.33f, 0.363f),
            CameraLiftMeters = 0.15f,
        };
    }
}
