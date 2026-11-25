using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// One block of a day, RESOLVED: the hour they set off, the place they are going, and how the last
    /// few metres are walked. <see cref="RoutinePlanner.ResolveStops"/> makes one per
    /// <see cref="RoutineEntry"/>; <see cref="RoutinePlanner.BuildFromStops"/> turns a day of them into
    /// a <see cref="RoutinePlan"/>.
    ///
    /// <para><b>Why it exists.</b> A <see cref="RoutineEntry"/> names a station in the region's baked
    /// table and an authored hour. That is the whole of an ordinary day, but it cannot say "somewhere on
    /// the dry sand, wherever that is at this tide" — the place is not in the table and the hour is not
    /// authored. A stop can: it carries the station itself rather than an id, and the hour itself rather
    /// than a table to jitter. A day made only of resolved entries plans exactly as it always has.</para>
    ///
    /// <para><b>Never saved, never authored.</b> Stops are derived, every time, from the routine asset
    /// and (for a varied day) the clock and the seed. Rule 5.</para>
    /// </summary>
    public struct RoutineStop
    {
        /// <summary>The hour (0..24) they set off for <see cref="Station"/>. Final: no jitter is applied
        /// after this.</summary>
        public float DepartureHour;

        /// <summary>Where they are going. A row of the region's table, or a place worked out for the day
        /// (its <see cref="RoutineStationEntry.LaneNodeName"/> must still name a real lane node).</summary>
        public RoutineStationEntry Station;

        /// <summary>What they are doing there. A tag, as on <see cref="RoutineEntry"/>.</summary>
        public RoutineActivity Activity;

        /// <summary>May the player (or a neighbour) stop them to talk during this block.</summary>
        public bool Interruptible;

        /// <summary>
        /// The ground between the station's lane node and the station, lane end first, for a place the
        /// lanes do not reach. Null or empty: the last leg is the straight line from the node (which is
        /// what every baked station already is).
        /// </summary>
        public Vector2[] Approach;

        /// <summary>
        /// A name for the stretch of ground this stop is on, or null. Two consecutive stops with the SAME
        /// strand are walked straight between; any other pair goes by the lanes.
        /// </summary>
        public string Strand;
    }
}
