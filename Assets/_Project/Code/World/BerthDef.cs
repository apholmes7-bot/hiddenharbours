using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A BERTH</b>: a place alongside where a boat of a stated size lies afloat on every tide, and
    /// the way in to it from open water. It says what it takes (length, beam, draught) and what water
    /// it promises under the keel at the lowest tide of the year; the ground has to keep that promise,
    /// and a guard test holds the two together (the berth is data, the depth is the region's ground,
    /// neither is a special case in code).
    ///
    /// <para>The keel line runs <see cref="Stern"/> to <see cref="Bow"/> in world metres. The berth is
    /// the box that line makes with <see cref="BeamMeters"/>; the approach is a line of points with a
    /// half width, first point out at open water, last point at the berth.</para>
    ///
    /// <para>One berth per file, stable id <c>berth.&lt;region&gt;_&lt;name&gt;</c> (rule 2).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Berth", fileName = "Berth")]
    public sealed class BerthDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (berth.<region>_<name>).")]
        public string Id = "berth.example";

        [Tooltip("The region this berth is in (region.snake_case).")]
        public string RegionId = "";

        [Tooltip("What people call it.")]
        public string DisplayName = "";

        [Header("Where she lies")]
        [Tooltip("The stern end of the keel line, world metres.")]
        public Vector2 Stern;

        [Tooltip("The bow end of the keel line, world metres.")]
        public Vector2 Bow;

        [Header("What it takes")]
        [Tooltip("The widest hull the berth takes, metres. The berth is the keel line with this beam.")]
        [Min(0.1f)] public float BeamMeters = 4f;

        [Tooltip("The deepest hull the berth takes, metres of draught.")]
        [Min(0.1f)] public float MaxDraughtMeters = 1f;

        [Tooltip("Water promised under the keel of the deepest hull at the lowest tide of the year, metres.")]
        [Min(0f)] public float KeelClearanceMeters = 0.3f;

        [Header("The way in")]
        [Tooltip("The way in from open water, world metres: first point out at open water (the region's " +
                 "edge, for a berth reached from another region), last point at the berth.")]
        public Vector2[] Approach = Array.Empty<Vector2>();

        [Tooltip("Half the width of the way in that must carry the same water, metres.")]
        [Min(0.1f)] public float ApproachHalfWidthMeters = 4f;

        [Tooltip("One line on why this berth is here. Authoring note only.")]
        [TextArea] public string Why = "";

        /// <summary>The longest hull the berth takes: the length of its keel line.</summary>
        public float LengthMeters => Vector2.Distance(Stern, Bow);

        /// <summary>The least water the berth promises at the lowest tide: the deepest hull and what is under her.</summary>
        public float PromisedDepthMeters => MaxDraughtMeters + KeelClearanceMeters;

        /// <summary>Does a hull of this size fit? Length and beam against the box, draught against the promise.</summary>
        public bool Takes(float lengthMeters, float halfBeamMeters, float draughtMeters) =>
            lengthMeters <= LengthMeters + 1e-3f && halfBeamMeters * 2f <= BeamMeters + 1e-3f &&
            draughtMeters <= MaxDraughtMeters + 1e-4f;

        /// <summary>
        /// The highest ground anywhere in the berth's box, sampled every <paramref name="stepMeters"/>
        /// along and across: the spot a keel would find first.
        /// </summary>
        public float ShallowestBedInBerth(Func<Vector2, float> elevationAt, float stepMeters) =>
            Shallowest(elevationAt, Stern, Bow, BeamMeters * 0.5f, stepMeters);

        /// <summary>The highest ground anywhere on the way in, over its whole width.</summary>
        public float ShallowestBedOnApproach(Func<Vector2, float> elevationAt, float stepMeters)
        {
            float worst = float.NegativeInfinity;
            for (int i = 0; i + 1 < Approach.Length; i++)
                worst = Mathf.Max(worst, Shallowest(elevationAt, Approach[i], Approach[i + 1], ApproachHalfWidthMeters, stepMeters));
            return worst;
        }

        private static float Shallowest(Func<Vector2, float> elevationAt, Vector2 from, Vector2 to, float halfWidth, float step)
        {
            if (elevationAt == null) throw new ArgumentNullException(nameof(elevationAt));
            step = Mathf.Max(step, 0.05f);
            Vector2 along = to - from;
            float length = along.magnitude;
            if (length < 1e-4f) return elevationAt(from);
            along /= length;
            var across = new Vector2(along.y, -along.x);
            int alongSteps = Mathf.CeilToInt(length / step), acrossSteps = Mathf.CeilToInt(halfWidth / step);
            float worst = float.NegativeInfinity;
            for (int a = 0; a <= alongSteps; a++)
            {
                Vector2 onLine = from + along * (length * a / alongSteps);
                for (int c = -acrossSteps; c <= acrossSteps; c++)
                    worst = Mathf.Max(worst, elevationAt(onLine + across * (halfWidth * c / Mathf.Max(acrossSteps, 1))));
            }
            return worst;
        }
    }
}
