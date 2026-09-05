using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What a machine and her driver are doing in one block of a scheduled trip. A TAG — nothing
    /// in the plan branches on it; it is carried so a presenter, a test failure and the owner reading a
    /// timetable all have a word for the block. Append-only.</summary>
    public enum VehicleTripStage
    {
        /// <summary>Standing in a bay with nobody aboard; her driver is at his post.</summary>
        Resting = 0,
        /// <summary>Her driver is walking to her door. She has not moved.</summary>
        Boarding = 1,
        /// <summary>Under way along the route, her driver aboard and out of sight in the cab.</summary>
        Driving = 2,
        /// <summary>Parked; her driver is walking from her door to his post.</summary>
        Alighting = 3,
    }

    /// <summary>
    /// Where a machine is, where her driver is, and what the two of them are doing at one instant. A
    /// value, so the rule that produces it is POCO-testable.
    ///
    /// <para>⭐ <b>The two directions are in DIFFERENT conventions and are therefore both handed back as
    /// raw unit vectors.</b> A machine's heading is world-XY (<c>transform.up</c> is her nose — the
    /// fleet's one convention, the number <c>VehicleMeshDriver</c> reads back to pick her picture); a
    /// walker's facing is a GROUND bearing, the iso squash un-done, because a character's facing row
    /// depicts a bearing across the ground. They differ by up to 12.5°. Publishing bearings here would
    /// mean picking one convention for both, and the loser is visibly crabbed.</para>
    /// </summary>
    public readonly struct VehicleTripPose
    {
        /// <summary>The machine's world position, metres.</summary>
        public readonly Vector2 MachinePosition;

        /// <summary>Her nose, as a unit vector in world XY. Zero only for a plan with no geometry at
        /// all — a caller holds its previous heading rather than pointing north.</summary>
        public readonly Vector2 MachineDirection;

        /// <summary>Her driver's world position, metres. Meaningless while <see cref="DriverAboard"/> —
        /// he is inside the cab and not drawn.</summary>
        public readonly Vector2 DriverPosition;

        /// <summary>Which way her driver is turned, as a unit world delta (apply
        /// <c>IsoGround.BearingDegrees</c> to it — see the struct note).</summary>
        public readonly Vector2 DriverDirection;

        /// <summary>True while her driver is in the cab: not drawn, not talkable, and wherever she
        /// is.</summary>
        public readonly bool DriverAboard;

        /// <summary>True while the machine is under way.</summary>
        public readonly bool Moving;

        /// <summary>True while her driver is on his feet and covering ground.</summary>
        public readonly bool DriverWalking;

        /// <summary>Which block of the trip is running.</summary>
        public readonly int LegIndex;

        /// <summary>What the two of them are doing (the block's tag).</summary>
        public readonly VehicleTripStage Stage;

        public VehicleTripPose(Vector2 machinePosition, Vector2 machineDirection, Vector2 driverPosition,
                               Vector2 driverDirection, bool driverAboard, bool moving, bool driverWalking,
                               int legIndex, VehicleTripStage stage)
        {
            MachinePosition = machinePosition;
            MachineDirection = machineDirection;
            DriverPosition = driverPosition;
            DriverDirection = driverDirection;
            DriverAboard = driverAboard;
            Moving = moving;
            DriverWalking = driverWalking;
            LegIndex = legIndex;
            Stage = stage;
        }
    }

    /// <summary>
    /// <b>Everything a region has to say about one trip</b>, in the units the region already thinks in —
    /// two roads and four points. The planner turns it into a timetable; the region never computes an
    /// hour and the plan never computes a metre of geometry.
    /// </summary>
    public readonly struct VehicleTripSpec
    {
        /// <summary>The machine's road out: first point is her origin bay, last is her destination
        /// bay.</summary>
        public readonly Vector2[] Outbound;

        /// <summary>Her road home. First point is the destination bay, last is the origin bay — the
        /// reverse of <see cref="Outbound"/> in the simple case, but its own array so a one-way pair of
        /// streets is expressible without a second plan.</summary>
        public readonly Vector2[] Return;

        /// <summary>Where her driver stands at the origin end when he is not driving.</summary>
        public readonly Vector2 OriginPost;

        /// <summary>Which way he is turned there, as a unit world delta.</summary>
        public readonly Vector2 OriginPostFacing;

        /// <summary>Where her driver stands at the far end — his stall, his counter, the thing he drove
        /// there to do.</summary>
        public readonly Vector2 DestinationPost;

        /// <summary>Which way he is turned there, as a unit world delta.</summary>
        public readonly Vector2 DestinationPostFacing;

        /// <summary>
        /// Her driver's door in HER OWN metres (<c>VehicleMeshDef.DriveDoorLocal</c>) — measured art, not
        /// a number anybody types. The planner swings it through her parked heading at each end, so the
        /// walk to the door lands where the door actually is rather than at the middle of the truck.
        /// </summary>
        public readonly Vector2 DoorLocal;

        /// <summary>The hour she leaves the origin bay — strictly, the hour her driver sets off for her
        /// door, because a departure is when somebody starts moving (the routine engine's law).</summary>
        public readonly float OutboundDepartureHour;

        /// <summary>The hour her driver leaves his far post to come home.</summary>
        public readonly float ReturnDepartureHour;

        /// <summary>How fast she travels the road, m/s.</summary>
        public readonly float CruiseMetresPerSecond;

        /// <summary>How fast her driver walks the last few metres to her door, m/s.</summary>
        public readonly float WalkMetresPerSecond;

        public VehicleTripSpec(Vector2[] outbound, Vector2[] returnLeg, Vector2 originPost,
                               Vector2 originPostFacing, Vector2 destinationPost,
                               Vector2 destinationPostFacing, Vector2 doorLocal,
                               float outboundDepartureHour, float returnDepartureHour,
                               float cruiseMetresPerSecond, float walkMetresPerSecond)
        {
            Outbound = outbound;
            Return = returnLeg;
            OriginPost = originPost;
            OriginPostFacing = originPostFacing;
            DestinationPost = destinationPost;
            DestinationPostFacing = destinationPostFacing;
            DoorLocal = doorLocal;
            OutboundDepartureHour = outboundDepartureHour;
            ReturnDepartureHour = returnDepartureHour;
            CruiseMetresPerSecond = cruiseMetresPerSecond;
            WalkMetresPerSecond = walkMetresPerSecond;
        }
    }

    /// <summary>
    /// ⭐ <b>ONE MACHINE'S DAY, READY TO READ OFF THE CLOCK.</b> Built once (on region load, or in a
    /// test) from a <see cref="VehicleTripSpec"/>; after that <see cref="SampleAt"/> is a PURE,
    /// allocation-free function of the hour — which is CLAUDE.md rule 5 in one method:
    ///
    /// <list type="bullet">
    ///   <item>the same <c>(worldSeed, gameTime)</c> yields the same pose, this run and every future run;</item>
    ///   <item>nothing is ticked, integrated or accumulated, so nothing can drift and nothing needs
    ///   saving — a save taken mid-trip re-derives the truck's place on the road from the clock alone;</item>
    ///   <item>a region loaded mid-leg shows her ON the road, mid-journey — not at the bay she left and
    ///   not at the one she is headed for — because "mid-leg" is just what the function returns.</item>
    /// </list>
    ///
    /// <para><b>WHY A POSE PLAN AND NOT A LIVE DRIVER.</b> The alternative is a <c>RouteDriver</c> on the
    /// real <c>VehicleController</c>, started at the departure hour. It is deterministic given a
    /// deterministic integrator, but a save taken mid-trip must then re-run the whole journey to land her
    /// where she was, and a region streamed in at 06:12 has to fast-forward eleven minutes of physics
    /// before it can draw a truck. A trip is a kinematic thing by design (ADR 0035: a truck has no
    /// rigidbody dynamics, only a demand and an integrator), and the sea fleet already settled the shape
    /// of the argument next door — <c>AmbientFleetSchedule</c> is a pure function of the clock and the
    /// presenter's rule for joining a session is <i>recompute, don't replay</i>. This goes one step
    /// further and poses the machine outright, because a truck's route is a ROAD: a body posed on the
    /// centre-line cannot wander off the carriageway, whereas an integrator on a 300 m road can.</para>
    ///
    /// <para>The live driver still exists (<c>Vehicles.RouteDriver</c>) and drives the same
    /// <see cref="RouteFollowMath"/>: it is what the PlayMode journey puts through the real seat, and it
    /// is what a player's cruise control would be built on.</para>
    ///
    /// <para><b>Eight blocks, flat arrays, one allocation at construction and none afterwards</b>
    /// (rule 7). A sample is a few dozen float operations — cheaper than the <c>GetComponent</c> it would
    /// take to avoid it.</para>
    /// </summary>
    public sealed class VehicleTripPlan
    {
        /// <summary>How many blocks a trip has: rest, board, drive, alight — at each end. Fixed, because
        /// a trip that is not "there and back" is two trips.</summary>
        public const int LegCount = 8;

        /// <summary>Block indices, named so a test and a failure message do not count on their
        /// fingers.</summary>
        public const int LegRestAtOrigin = 0, LegBoardAtOrigin = 1, LegDriveOut = 2, LegAlightAtDestination = 3,
                         LegRestAtDestination = 4, LegBoardAtDestination = 5, LegDriveHome = 6,
                         LegAlightAtOrigin = 7;

        /// <summary>Departure hour of each block, in [0, 24). Block 0's is the moment the driver gets back
        /// to his origin post, which is why they are DERIVED rather than authored: only two of the eight
        /// are the owner's, and the other six are what the geometry and the speeds make them.</summary>
        public readonly float[] DepartureHours;

        /// <summary>What each block is.</summary>
        public readonly VehicleTripStage[] Stages;

        /// <summary>True for the blocks the driver spends in the cab.</summary>
        public readonly bool[] DriverAboard;

        /// <summary>The world's day length, in real seconds per game hour, that this plan's derived hours
        /// were computed against. A holder compares it and rebuilds when the owner moves the knob —
        /// otherwise a plan built at the default day length would keep the wrong six minutes for ever.</summary>
        public readonly float SecondsPerGameHour;

        private readonly ScheduledLegs _machine;
        private readonly ScheduledLegs _driver;

        /// <summary>Which way she is pointing at the start and the end of each boarding block, indexed by
        /// leg — see <see cref="SampleAt"/>'s note on the turn in the bay. Zero for every block that is
        /// not a boarding one.</summary>
        private readonly Vector2[] _turnFrom;
        private readonly Vector2[] _turnTo;

        private VehicleTripPlan(float[] departureHours, VehicleTripStage[] stages, bool[] driverAboard,
                                ScheduledLegs machine, ScheduledLegs driver, Vector2[] turnFrom,
                                Vector2[] turnTo, float secondsPerGameHour)
        {
            DepartureHours = departureHours;
            Stages = stages;
            DriverAboard = driverAboard;
            _machine = machine;
            _driver = driver;
            _turnFrom = turnFrom;
            _turnTo = turnTo;
            SecondsPerGameHour = secondsPerGameHour;
        }

        /// <summary>The machine's legs — exposed so content tests can measure the road she is actually
        /// put on rather than the road somebody meant to put her on.</summary>
        public ScheduledLegs MachineLegs => _machine;

        /// <summary>Her driver's legs.</summary>
        public ScheduledLegs DriverLegs => _driver;

        /// <summary>Where she stands at the origin bay.</summary>
        public Vector2 OriginBay => _machine.PointAt(LegRestAtOrigin, 0f);

        /// <summary>Where she stands at the far bay.</summary>
        public Vector2 DestinationBay => _machine.PointAt(LegRestAtDestination, 0f);

        /// <summary>How long the whole round trip takes, in game hours, door to door — the number a
        /// content test measures against the day so a timetable that cannot fit fails loudly.</summary>
        public float RoundTripHours =>
            DaySchedule.ElapsedHours(DepartureHours[LegRestAtOrigin], DepartureHours[LegBoardAtOrigin]);

        /// <summary>
        /// <b>Build the timetable from the geometry.</b> Only two hours are authored; the other six fall
        /// out of how long each leg takes at its own speed, so a route the owner lengthens automatically
        /// arrives later rather than teleporting to keep an authored arrival.
        ///
        /// <para>Returns null with a stated <paramref name="problem"/> for a spec that cannot make a trip
        /// — the same fail-loud-and-stand-still contract <c>RoutinePlanner</c> keeps, because a machine
        /// that quietly does not move is indistinguishable from one that has not been authored yet.</para>
        /// </summary>
        public static VehicleTripPlan Build(in VehicleTripSpec spec, float secondsPerGameHour,
                                            out string problem)
        {
            problem = null;

            if (spec.Outbound == null || spec.Outbound.Length < 2)
            { problem = "the outbound route has fewer than two points"; return null; }
            if (spec.Return == null || spec.Return.Length < 2)
            { problem = "the return route has fewer than two points"; return null; }
            if (spec.CruiseMetresPerSecond <= 0f)
            { problem = "the cruise speed is zero — she would never arrive"; return null; }
            if (spec.WalkMetresPerSecond <= 0f)
            { problem = "the walk speed is zero — her driver would never reach the door"; return null; }
            if (secondsPerGameHour <= 0f)
            { problem = "the day has no length"; return null; }

            Vector2 originBay = spec.Outbound[0];
            Vector2 destinationBay = spec.Outbound[spec.Outbound.Length - 1];

            const float SameSq = 0.01f;   // 10 cm², i.e. the same bay
            if ((spec.Return[0] - destinationBay).sqrMagnitude > SameSq)
            { problem = "the road home does not start where the road out finished"; return null; }
            if ((spec.Return[spec.Return.Length - 1] - originBay).sqrMagnitude > SameSq)
            { problem = "the road home does not finish where the road out started"; return null; }

            // ⭐ EVERY PARKED NOSE IS DERIVED FROM THE ROAD, never authored. She points where the road
            // she arrived on left her pointing, and she leaves pointing along the road she sets off down.
            // Authoring either would let a bay heading disagree with its own route, and she would snap.
            Vector2 arriveAtDestination = LastDirection(spec.Outbound);
            Vector2 arriveAtOrigin = LastDirection(spec.Return);
            Vector2 leaveOrigin = FirstDirection(spec.Outbound);
            Vector2 leaveDestination = FirstDirection(spec.Return);

            // ⭐ THE DOOR IS READ AT THE HEADING SHE IS ACTUALLY AT. There are TWO door points per bay,
            // because she turns in it (see SampleAt): the driver getting OUT walks from the door as she
            // arrived, and the driver getting IN walks to the door as she will be lying when he arrives.
            // One point for both would put him at the wrong corner of the truck by up to her own width.
            Vector2 doorAlightOrigin = DoorWorld(originBay, arriveAtOrigin, spec.DoorLocal);
            Vector2 doorBoardOrigin = DoorWorld(originBay, leaveOrigin, spec.DoorLocal);
            Vector2 doorAlightDestination = DoorWorld(destinationBay, arriveAtDestination, spec.DoorLocal);
            Vector2 doorBoardDestination = DoorWorld(destinationBay, leaveDestination, spec.DoorLocal);

            Vector2 originFacing = Unit(spec.OriginPostFacing, -arriveAtOrigin);
            Vector2 destinationFacing = Unit(spec.DestinationPostFacing, -arriveAtDestination);

            // ---- the machine's eight legs: she stands in a bay for six of them and drives two ---------
            var machine = ScheduledLegs.Build(new[]
            {
                new[] { originBay },                      // 0 rest at the origin bay
                new[] { originBay },                      // 1 boarding — she turns, she does not move
                spec.Outbound,                            // 2 the road out
                new[] { destinationBay },                 // 3 alighting
                new[] { destinationBay },                 // 4 rest at the far bay
                new[] { destinationBay },                 // 5 boarding for home — she turns again
                spec.Return,                              // 6 the road home
                new[] { originBay },                      // 7 alighting
            }, new[]
            {
                0f, 0f, spec.CruiseMetresPerSecond, 0f, 0f, 0f, spec.CruiseMetresPerSecond, 0f,
            }, new[]
            {
                arriveAtOrigin, leaveOrigin, arriveAtDestination, arriveAtDestination,
                arriveAtDestination, leaveDestination, arriveAtOrigin, arriveAtOrigin,
            });

            // The turn in the bay, per block: only the two boarding ones have one.
            var turnFrom = new Vector2[LegCount];
            var turnTo = new Vector2[LegCount];
            turnFrom[LegBoardAtOrigin] = arriveAtOrigin;
            turnTo[LegBoardAtOrigin] = leaveOrigin;
            turnFrom[LegBoardAtDestination] = arriveAtDestination;
            turnTo[LegBoardAtDestination] = leaveDestination;

            // ---- her driver's eight: two posts, four short walks, two legs in the cab ----------------
            var driver = ScheduledLegs.Build(new[]
            {
                new[] { spec.OriginPost },                                // 0 at his origin post
                new[] { spec.OriginPost, doorBoardOrigin },               // 1 out to her door
                new[] { doorBoardOrigin },                                // 2 aboard (not drawn)
                new[] { doorAlightDestination, spec.DestinationPost },    // 3 down from the cab to his post
                new[] { spec.DestinationPost },                           // 4 at his post — the whole point
                new[] { spec.DestinationPost, doorBoardDestination },     // 5 back to her door
                new[] { doorBoardDestination },                           // 6 aboard (not drawn)
                new[] { doorAlightOrigin, spec.OriginPost },              // 7 down and back to his post
            }, new[]
            {
                0f, spec.WalkMetresPerSecond, 0f, spec.WalkMetresPerSecond,
                0f, spec.WalkMetresPerSecond, 0f, spec.WalkMetresPerSecond,
            }, new[]
            {
                originFacing, originFacing, originFacing, destinationFacing,
                destinationFacing, destinationFacing, destinationFacing, originFacing,
            });

            // ---- the timetable: two authored hours, six derived from how long each leg takes ---------
            var hours = new float[LegCount];
            hours[LegBoardAtOrigin] = DaySchedule.Wrap24(spec.OutboundDepartureHour);
            hours[LegDriveOut] = Next(hours[LegBoardAtOrigin], driver, LegBoardAtOrigin, secondsPerGameHour);
            hours[LegAlightAtDestination] = Next(hours[LegDriveOut], machine, LegDriveOut, secondsPerGameHour);
            hours[LegRestAtDestination] =
                Next(hours[LegAlightAtDestination], driver, LegAlightAtDestination, secondsPerGameHour);

            hours[LegBoardAtDestination] = DaySchedule.Wrap24(spec.ReturnDepartureHour);
            hours[LegDriveHome] =
                Next(hours[LegBoardAtDestination], driver, LegBoardAtDestination, secondsPerGameHour);
            hours[LegAlightAtOrigin] = Next(hours[LegDriveHome], machine, LegDriveHome, secondsPerGameHour);
            hours[LegRestAtOrigin] = Next(hours[LegAlightAtOrigin], driver, LegAlightAtOrigin, secondsPerGameHour);

            var stages = new[]
            {
                VehicleTripStage.Resting, VehicleTripStage.Boarding, VehicleTripStage.Driving,
                VehicleTripStage.Alighting, VehicleTripStage.Resting, VehicleTripStage.Boarding,
                VehicleTripStage.Driving, VehicleTripStage.Alighting,
            };
            var aboard = new[] { false, false, true, false, false, false, true, false };

            return new VehicleTripPlan(hours, stages, aboard, machine, driver, turnFrom, turnTo,
                                       secondsPerGameHour);
        }

        /// <summary>
        /// <b>The pose at an hour of the game day.</b> Pure, total, allocation-free.
        ///
        /// <para>The block is whichever departed most recently; each body is <c>elapsed × its own speed</c>
        /// metres along that block's own route; past the end of a route you are standing at its last
        /// point, which IS arrival, so it needs no branch. A block whose travel outlasts the block itself
        /// simply means the next block starts from where the body had got to — graceful rather than
        /// glitchy, and a content test says so out loud.</para>
        ///
        /// <para>⭐ <b>SHE TURNS IN THE BAY WHILE HER DRIVER WALKS OVER, and that is not decoration.</b>
        /// There is one road into a truck park and one out, so the way she arrived is the reverse of the
        /// way she must leave — a fact of the geometry, not of this file. A posed body cannot back out
        /// (its nose is the direction it is travelling), so without this the truck would flip 180° in a
        /// single frame at the departure instant, at both ends, every day. Spreading the turn across the
        /// boarding block puts it where a manoeuvre belongs: she comes round to face the exit over the
        /// seconds her driver is crossing the gravel towards her.</para>
        ///
        /// <para><b>What it is NOT: a three-point turn.</b> The truck park is sized for one (20.1 × 13.4 m,
        /// "one to park in and one to turn in") and a machine that cannot reverse cannot do one. She
        /// pivots about her own centre instead. At 32 px/m and 200 m away that reads as a truck
        /// manoeuvring; up close it does not. The honest fix is an astern flag on a leg, which is a
        /// follow-up and is named as one in the PR body.</para>
        /// </summary>
        public VehicleTripPose SampleAt(float hourOfDay)
        {
            int leg = DaySchedule.BlockIndexAt(hourOfDay, DepartureHours);
            if (leg < 0)
                return new VehicleTripPose(Vector2.zero, Vector2.up, Vector2.zero, Vector2.down,
                                           false, false, false, -1, VehicleTripStage.Resting);

            float elapsed = DaySchedule.ElapsedHours(hourOfDay, DepartureHours[leg]);

            _machine.Sample(leg, elapsed, SecondsPerGameHour, out Vector2 machineAt,
                            out Vector2 machineDir, out bool machineMoving);
            _driver.Sample(leg, elapsed, SecondsPerGameHour, out Vector2 driverAt,
                           out Vector2 driverDir, out bool driverMoving);

            if (_turnTo[leg] != Vector2.zero)
                machineDir = TurnedBy(_turnFrom[leg], _turnTo[leg],
                                      Progress(elapsed, _driver.TravelHours(leg, SecondsPerGameHour)));

            bool aboard = DriverAboard[leg];
            return new VehicleTripPose(machineAt, machineDir, driverAt, driverDir, aboard, machineMoving,
                                       driverMoving && !aboard, leg, Stages[leg]);
        }

        /// <summary>How far through a block of <paramref name="lengthHours"/> we are, clamped. A block
        /// with no length is already over — which is what a driver who has no walk to make means, and
        /// leaves the turn instantaneous rather than dividing by zero.</summary>
        private static float Progress(float elapsedHours, float lengthHours)
            => lengthHours > 0f ? Mathf.Clamp01(elapsedHours / lengthHours) : 1f;

        /// <summary>Rotate from one direction to another by <paramref name="t"/>, the short way round.
        /// Angle-space rather than a vector lerp: a lerp between two opposite directions passes through
        /// zero, and a zero direction is the one answer that means "she is pointing nowhere".</summary>
        private static Vector2 TurnedBy(Vector2 from, Vector2 to, float t)
        {
            if (from == Vector2.zero) return to;
            if (to == Vector2.zero) return from;
            float a0 = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
            float a1 = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            float a = Mathf.LerpAngle(a0, a1, t) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        // ---- construction helpers ---------------------------------------------------------------------

        /// <summary>The hour a block ends: its own departure plus however long its body takes to cover its
        /// route. A block whose body does not move ends immediately, which is why the two authored hours
        /// are the ones that stop the timetable collapsing to a point.</summary>
        private static float Next(float departure, ScheduledLegs legs, int leg, float secondsPerGameHour)
            => DaySchedule.Wrap24(departure + legs.TravelHours(leg, secondsPerGameHour));

        /// <summary>The direction of a route's last real segment — the way she is pointing when she gets
        /// there, and therefore the way she is left standing.</summary>
        private static Vector2 LastDirection(Vector2[] route)
        {
            Vector2 dir = Polyline.TangentAlong(route, 0, route.Length, Polyline.Length(route, 0, route.Length));
            return dir == Vector2.zero ? Vector2.up : dir;
        }

        /// <summary>The direction of a route's first real segment — the way she has to be pointing before
        /// she can set off down it.</summary>
        private static Vector2 FirstDirection(Vector2[] route)
        {
            Vector2 dir = Polyline.TangentAlong(route, 0, route.Length, 0f);
            return dir == Vector2.zero ? Vector2.up : dir;
        }

        /// <summary>Her driver's door in world metres, for a machine standing at <paramref name="bay"/>
        /// with her nose along <paramref name="nose"/>. The same transform <c>VehicleDoor</c> applies
        /// live, done here on a heading the plan already knows: her local +Y is the nose, so the door's
        /// local (x, y) swings with her.</summary>
        private static Vector2 DoorWorld(Vector2 bay, Vector2 nose, Vector2 doorLocal)
        {
            Vector2 up = nose == Vector2.zero ? Vector2.up : nose.normalized;
            Vector2 right = new(up.y, -up.x);       // +X when +Y is the nose (a right-handed 2D frame)
            return bay + right * doorLocal.x + up * doorLocal.y;
        }

        private static Vector2 Unit(Vector2 v, Vector2 fallback)
        {
            if (v.sqrMagnitude > 1e-6f) return v.normalized;
            return fallback.sqrMagnitude > 1e-6f ? fallback.normalized : Vector2.down;
        }
    }
}
