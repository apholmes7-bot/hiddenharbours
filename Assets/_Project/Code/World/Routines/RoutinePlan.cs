using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Which side of a threshold a pose is on. <b>A visibility question, not a position one</b>: an
    /// occupant of an interior is drawn only while the player shares that interior (the seamless-interiors
    /// reveal's own rule), and the whole point of walking through a real drawn door is that the crossing
    /// happens where the player can see it.
    ///
    /// <para>Two "inside" answers rather than one because a single walk can cross two thresholds — out of
    /// the house you slept in and into the school you teach at — and the two are different buildings, so
    /// "am I hidden?" is asked of a different <see cref="BuildingInterior"/> at each end.</para>
    /// </summary>
    public enum RoutineShelter
    {
        /// <summary>Out in the world. Always drawn.</summary>
        Outdoors = 0,
        /// <summary>Still inside the building this block set out FROM — has not crossed out yet.</summary>
        InsideOrigin = 1,
        /// <summary>Inside the building this block was headed FOR — has crossed in.</summary>
        InsideDestination = 2,
    }

    /// <summary>
    /// Where a villager is and what they are doing at one instant — the whole answer to "where is Aunt
    /// Ginny at 14:20?", and the only thing <see cref="VillagerRoutine"/> pushes onto a transform.
    /// A value, so the rule that produces it is POCO-testable.
    /// </summary>
    public readonly struct RoutinePose
    {
        /// <summary>World position, in metres.</summary>
        public readonly Vector2 Position;

        /// <summary>Which block of the day is running.</summary>
        public readonly int BlockIndex;

        /// <summary>What they are doing (the block's tag).</summary>
        public readonly RoutineActivity Activity;

        /// <summary>True while still on the way — the gait is a walk and the facing follows the route.</summary>
        public readonly bool Walking;

        /// <summary>Compass heading (degrees, 0 = North, CW): the route's direction while walking, the
        /// station's own stance once arrived.</summary>
        public readonly float HeadingDegrees;

        /// <summary>Which side of a threshold they are on — see <see cref="RoutineShelter"/>.</summary>
        public readonly RoutineShelter Shelter;

        public RoutinePose(Vector2 position, int blockIndex, RoutineActivity activity, bool walking,
                           float headingDegrees, RoutineShelter shelter)
        {
            Position = position;
            BlockIndex = blockIndex;
            Activity = activity;
            Walking = walking;
            HeadingDegrees = headingDegrees;
            Shelter = shelter;
        }
    }

    /// <summary>
    /// <b>One villager's day, resolved and ready to read off the clock.</b> Built once (at spawn, or in a
    /// test) from a <see cref="RoutineDef"/> plus the region's stations and lanes; after that
    /// <see cref="SampleAt"/> is a PURE, allocation-free function of the hour — which is the whole
    /// determinism contract (CLAUDE.md rule 5) in one method:
    ///
    /// <list type="bullet">
    ///   <item>the same <c>(worldSeed, gameTime)</c> yields the same pose, this run and every future run;</item>
    ///   <item>nothing is ticked, integrated or accumulated, so nothing can drift and nothing needs saving;</item>
    ///   <item>a villager spawning mid-leg on region load appears ON the leg, mid-stride — not at the
    ///   station they were heading for and not at the one they left — because "mid-leg" is just what the
    ///   function returns for that hour.</item>
    /// </list>
    ///
    /// <para><b>Flat arrays, one allocation at construction, none afterwards</b> (rule 7). Every block's
    /// route is a slice of one <see cref="Waypoints"/> array; sampling walks that slice with a running
    /// remainder. A day is a handful of blocks and a route a handful of points, so a sample is a few
    /// dozen float operations — cheaper than the <c>GetComponent</c> it would take to avoid it.</para>
    /// </summary>
    public sealed class RoutinePlan
    {
        /// <summary>Departure hour of each block, already jittered (see
        /// <see cref="RoutineSchedule.DepartureHour"/>).</summary>
        public readonly float[] DepartureHours;

        /// <summary>All blocks' routes, flattened. Block <c>i</c>'s route is the slice
        /// <c>[RouteStart[i], RouteStart[i] + RouteCount[i])</c>, running from the PREVIOUS block's station
        /// to this block's.</summary>
        public readonly Vector2[] Waypoints;

        public readonly int[] RouteStart;
        public readonly int[] RouteCount;

        /// <summary>Walked length of each block's route, in metres. Precomputed: it never changes, and
        /// re-measuring a polyline every frame would be the one avoidable cost here.</summary>
        public readonly float[] RouteLengthMetres;

        public readonly RoutineActivity[] Activities;

        /// <summary>Which way they stand once arrived at each block's station.</summary>
        public readonly float[] StandHeadingDegrees;

        /// <summary>
        /// Distance along block <c>i</c>'s route at which they step OUT of the building the block started
        /// in. 0 for a block that starts outdoors — the common case — so "am I still inside where I was?"
        /// is <c>distance &lt; this</c> and is false immediately.
        /// </summary>
        public readonly float[] ExitAtMetres;

        /// <summary>
        /// Distance along block <c>i</c>'s route at which they step INTO the building the block is headed
        /// for. <see cref="float.PositiveInfinity"/> for a block ending outdoors, so "have I crossed in?"
        /// is <c>distance &gt;= this</c> and is never true.
        /// </summary>
        public readonly float[] EnterAtMetres;

        /// <summary>Metres per second this person walks.</summary>
        public readonly float WalkSpeedMetresPerSecond;

        /// <summary>Metres per second they move when they are LATE — hurrying back onto the day after a
        /// conversation held them up. See <see cref="ConversationHold"/>.</summary>
        public readonly float CatchUpSpeedMetresPerSecond;

        /// <summary>
        /// Whether each block may be interrupted by a conversation — <b>authored data, not a heuristic</b>
        /// (<see cref="RoutineEntry.Uninterruptible"/>). Default true: stopping to talk is the rule and
        /// the exception is typed by a person who knows why.
        ///
        /// <para>Note it is a property of the BLOCK, not of the villager: the same fisher will stop for
        /// you on the green at noon and keep walking through the timed beat she must not miss.</para>
        /// </summary>
        public readonly bool[] Interruptible;

        public int BlockCount => DepartureHours.Length;

        /// <summary>Can a conversation stop them during block <paramref name="block"/>? Out-of-range
        /// answers YES — the same fail-open every other unauthored field here takes, because a villager
        /// who cannot be talked to is a worse bug than one who stops when she might not have.</summary>
        public bool InterruptibleAt(int block)
            => block < 0 || block >= Interruptible.Length || Interruptible[block];

        public RoutinePlan(float[] departureHours, Vector2[] waypoints, int[] routeStart, int[] routeCount,
                           float[] routeLengthMetres, RoutineActivity[] activities,
                           float[] standHeadingDegrees, float[] exitAtMetres, float[] enterAtMetres,
                           float walkSpeedMetresPerSecond, bool[] interruptible = null,
                           float catchUpSpeedMetresPerSecond = 0f)
        {
            DepartureHours = departureHours ?? System.Array.Empty<float>();
            Waypoints = waypoints ?? System.Array.Empty<Vector2>();
            RouteStart = routeStart ?? System.Array.Empty<int>();
            RouteCount = routeCount ?? System.Array.Empty<int>();
            RouteLengthMetres = routeLengthMetres ?? System.Array.Empty<float>();
            Activities = activities ?? System.Array.Empty<RoutineActivity>();
            StandHeadingDegrees = standHeadingDegrees ?? System.Array.Empty<float>();
            ExitAtMetres = exitAtMetres ?? System.Array.Empty<float>();
            EnterAtMetres = enterAtMetres ?? System.Array.Empty<float>();
            WalkSpeedMetresPerSecond = Mathf.Max(0f, walkSpeedMetresPerSecond);
            Interruptible = interruptible ?? System.Array.Empty<bool>();
            // A caller that names no hurry gets the walk itself, which converges on a stationary target
            // and simply cannot on a moving one — safe, and visibly wrong enough to be noticed.
            CatchUpSpeedMetresPerSecond = catchUpSpeedMetresPerSecond > 0f
                ? catchUpSpeedMetresPerSecond
                : WalkSpeedMetresPerSecond;
        }

        /// <summary>How many game hours block <paramref name="block"/>'s walk takes.</summary>
        public float TravelHours(int block, float secondsPerGameHour) =>
            block < 0 || block >= RouteLengthMetres.Length
                ? 0f
                : RoutineSchedule.TravelHours(RouteLengthMetres[block], WalkSpeedMetresPerSecond,
                                              secondsPerGameHour);

        /// <summary>
        /// <b>The pose at an hour of the game day.</b> Pure, total, allocation-free.
        ///
        /// <para>The block is whichever departed most recently; the walk is <c>elapsed × speed</c> metres
        /// along that block's route; past the end of the route you are standing at its last point, which is
        /// the station — so arrival is the clamp and needs no branch of its own. A block whose walk is
        /// longer than the block itself would still be walking when the next departure arrives; that is an
        /// authoring error <c>RoutineContentTests</c> catches, and here it simply means the next block's
        /// route starts from where they had got to, which is graceful rather than glitchy.</para>
        /// </summary>
        public RoutinePose SampleAt(float hourOfDay, float secondsPerGameHour)
        {
            if (BlockCount == 0)
                return new RoutinePose(Vector2.zero, -1, RoutineActivity.Home, false, 180f,
                                       RoutineShelter.Outdoors);

            int block = RoutineSchedule.BlockIndexAt(hourOfDay, DepartureHours);
            float elapsed = RoutineSchedule.ElapsedHours(hourOfDay, DepartureHours[block]);

            int start = RouteStart[block], count = RouteCount[block];
            float length = RouteLengthMetres[block];
            float travel = RoutineSchedule.TravelHours(length, WalkSpeedMetresPerSecond, secondsPerGameHour);
            bool walking = travel > 0f && elapsed < travel;

            float distance = walking
                ? RoutineSchedule.DistanceWalked(elapsed, WalkSpeedMetresPerSecond, secondsPerGameHour)
                : length;

            Vector2 position = RoutineLaneTree.PointAlong(Waypoints, start, count, distance);

            float stand = StandHeadingDegrees.Length > block ? StandHeadingDegrees[block] : 180f;
            float heading = walking
                ? RoutineLaneTree.HeadingAlong(Waypoints, start, count, distance, stand)
                : stand;

            // ⭐ WHERE THE THRESHOLD IS, IN METRES ALONG THE WALK — not "at the end of the block". A
            // villager who set off home is OUTDOORS all the way down the lane and must stay drawn; she is
            // inside only once she is past her own doorway, which is a distance along this route and is
            // where the player standing in the lane watches her go in. Flagging the whole block would make
            // her vanish the moment she set off, which is the teleport-from-the-lane the seamless-interiors
            // ruling forbids, in reverse.
            float exitAt = ExitAtMetres.Length > block ? ExitAtMetres[block] : 0f;
            float enterAt = EnterAtMetres.Length > block ? EnterAtMetres[block] : float.PositiveInfinity;
            RoutineShelter shelter = distance >= enterAt ? RoutineShelter.InsideDestination
                                   : distance < exitAt ? RoutineShelter.InsideOrigin
                                   : RoutineShelter.Outdoors;

            return new RoutinePose(position, block,
                                   Activities.Length > block ? Activities[block] : RoutineActivity.Home,
                                   walking, heading, shelter);
        }
    }
}
