using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A line down a beach, from ground that is always dry (<see cref="Top"/>) toward the water
    /// (<see cref="Toe"/>). Nobody is ever authored AT a point on it: where they stand along it is worked
    /// out for the tide of the day, which is the whole reason it is a line and not a station.
    /// </summary>
    [Serializable]
    public struct BeachTransect
    {
        [Tooltip("The landward end, world metres. Must stay dry on the highest tide.")]
        public Vector2 Top;

        [Tooltip("The seaward end, world metres. May be under water most of the day.")]
        public Vector2 Toe;

        [Tooltip("One line on why this line is here. Authoring note only.")]
        public string Why;

        /// <summary>Compass bearing (0 = North, clockwise) looking down the line, out at the water.</summary>
        public float SeawardHeadingDegrees
        {
            get
            {
                Vector2 d = Toe - Top;
                if (d.sqrMagnitude < 1e-8f) return 180f;
                float deg = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
                return deg < 0f ? deg + 360f : deg;
            }
        }
    }

    /// <summary>
    /// <b>A BEACH THE VILLAGE USES</b> — where it is, as lines down the sand rather than as fixed spots,
    /// because the tide decides where anybody can be (pillar 1) and no authored point survives a 4 m
    /// range.
    ///
    /// <para><b>It adds nothing to a scene.</b> The lines are world metres in an asset; the lane node is
    /// named, not referenced. A region's scene never has to be rebuilt or re-saved to gain a beach.</para>
    ///
    /// <para>One beach per file, stable id <c>beach.&lt;region&gt;.&lt;name&gt;</c> (rule 2).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Beach", fileName = "Beach")]
    public sealed class BeachDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (beach.<region>.<name>).")]
        public string Id = "beach.example";

        [Tooltip("The region this beach is in (region.snake_case). Only villagers of this region use it.")]
        public string RegionId = "";

        [Header("How people get there")]
        [Tooltip("The lane junction the beach hangs off, by NAME (a node of the region's RoutineLanes).")]
        public string LaneNodeName = "";

        [Tooltip("The ground from that lane node down to the beach, lane end first, for a beach the lanes " +
                 "stop short of. Empty when the node is already at the head of the sand.")]
        public Vector2[] Approach = Array.Empty<Vector2>();

        [Header("Where people can be")]
        [Tooltip("Lines to stand or sit on, looking out. One person per line.")]
        public BeachTransect[] Spots = Array.Empty<BeachTransect>();

        [Tooltip("Lines whose water's-edge points, taken in order, are a walk along the tide line.")]
        public BeachTransect[] Promenade = Array.Empty<BeachTransect>();

        [Tooltip("Lines out over the flats: ground that is bare only at low water (digging).")]
        public BeachTransect[] Flats = Array.Empty<BeachTransect>();

        [Tooltip("Lines down an even, gentle face that reaches below low water: where people leave " +
                 "their things above the tide and go in to wade. One person per line; keep them at " +
                 "least three metres apart.")]
        public BeachTransect[] Shallows = Array.Empty<BeachTransect>();

        [Header("Sampling")]
        [Tooltip("How finely the ground is read along a line or a leg, in metres.")]
        [Min(0.05f)] public float SampleStepMetres = 0.25f;

        /// <summary>Enough authored to send somebody there.</summary>
        public bool IsUsable => !string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(RegionId) &&
                                !string.IsNullOrEmpty(LaneNodeName);
    }
}
