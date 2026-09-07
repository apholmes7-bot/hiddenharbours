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
        /// <summary>Her driver is walking to the street-side handle to take a trailer. The pin goes in at
        /// the END of the block — the walk is the work, and it is the same handle and the same verb the
        /// player uses.</summary>
        Coupling = 4,
        /// <summary>Her driver is walking back to the handle to set a trailer down: the legs wind down
        /// over the block and the pin comes out at the end of it.</summary>
        Uncoupling = 5,
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

        /// <summary>True when this trip carries a towed body at all. False leaves every trailer field
        /// below meaningless rather than zero-and-plausible.</summary>
        public readonly bool HasTrailer;

        /// <summary>The trailer's world position — her ORIGIN, not her pin, which sits metres forward of
        /// it.</summary>
        public readonly Vector2 TrailerPosition;

        /// <summary>Her nose, as a unit vector in world XY — the same convention as
        /// <see cref="MachineDirection"/>, because she is drawn by the same mesh path.</summary>
        public readonly Vector2 TrailerDirection;

        /// <summary>True while her pin is in the slot. She is still drawn when it is false — she is
        /// standing in her bay on her own legs, which is most of her day.</summary>
        public readonly bool TrailerCoupled;

        /// <summary>Her landing gear, 0 down to 1 up — the same 0..1 the crank the player turns runs on
        /// (<c>VehicleDoors</c> group "gear"), so a presenter drives the same shoes rather than a second
        /// animation of them.</summary>
        public readonly float TrailerLegsUp;

        public VehicleTripPose(Vector2 machinePosition, Vector2 machineDirection, Vector2 driverPosition,
                               Vector2 driverDirection, bool driverAboard, bool moving, bool driverWalking,
                               int legIndex, VehicleTripStage stage)
            : this(machinePosition, machineDirection, driverPosition, driverDirection, driverAboard,
                   moving, driverWalking, legIndex, stage, false, Vector2.zero, Vector2.up, false, 0f)
        {
        }

        public VehicleTripPose(Vector2 machinePosition, Vector2 machineDirection, Vector2 driverPosition,
                               Vector2 driverDirection, bool driverAboard, bool moving, bool driverWalking,
                               int legIndex, VehicleTripStage stage, bool hasTrailer,
                               Vector2 trailerPosition, Vector2 trailerDirection, bool trailerCoupled,
                               float trailerLegsUp)
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
            HasTrailer = hasTrailer;
            TrailerPosition = trailerPosition;
            TrailerDirection = trailerDirection;
            TrailerCoupled = trailerCoupled;
            TrailerLegsUp = trailerLegsUp;
        }
    }

    /// <summary>
    /// <b>The towed half of a trip</b> — which plate, which pin, and where the region stood the trailer.
    /// Geometry and published art only: no hours, because the two coupling beats are DERIVED from the
    /// walk to the handle the same way the six existing blocks are derived from their own legs.
    ///
    /// <para><b>Nothing here is a position anybody typed.</b> The two structs come off the baked mesh
    /// defs (the art's own numbers), and the trailer's bay comes off the region's derived yard. The plan
    /// then CHECKS the pair against the shipped capture test rather than assuming they meet — see
    /// <see cref="VehicleTripPlan.Build"/>'s refusals.</para>
    /// </summary>
    public readonly struct VehicleTowedSpec
    {
        /// <summary>The tractor's published fifth wheel.</summary>
        public readonly VehicleFifthWheel Wheel;

        /// <summary>The trailer's published kingpin.</summary>
        public readonly VehicleKingpin Pin;

        /// <summary>Where the trailer's ORIGIN stands when she is on her legs — the region's own bay,
        /// used as the seed for the settle and then as the thing the capture test is asked about.</summary>
        public readonly Vector2 TrailerBay;

        /// <summary>Which way she lies there, as a compass bearing (0 north, clockwise positive) — the
        /// frame the coupling, the picture and the hitch all share.</summary>
        public readonly float TrailerHeadingDegrees;

        public VehicleTowedSpec(in VehicleFifthWheel wheel, in VehicleKingpin pin, Vector2 trailerBay,
                                float trailerHeadingDegrees)
        {
            Wheel = wheel;
            Pin = pin;
            TrailerBay = trailerBay;
            TrailerHeadingDegrees = trailerHeadingDegrees;
        }

        /// <summary>True when both halves of the coupling were actually baked. Checked, never inferred
        /// from zeros.</summary>
        public bool Published => Wheel.Published && Pin.Published;
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
        /// door, because a departure is when somebody starts moving (the routine engine's law). On a
        /// towing trip he sets off for the HANDLE first, which is the same law read one beat
        /// earlier.</summary>
        public readonly float OutboundDepartureHour;

        /// <summary>The hour her driver leaves his far post to come home.</summary>
        public readonly float ReturnDepartureHour;

        /// <summary>How fast she travels the road, m/s.</summary>
        public readonly float CruiseMetresPerSecond;

        /// <summary>How fast her driver walks the last few metres to her door, m/s.</summary>
        public readonly float WalkMetresPerSecond;

        /// <summary>True when this trip carries a towed body — the plan then runs ten blocks rather than
        /// eight, and refuses geometry a coupled pair could not actually work.</summary>
        public readonly bool Tows;

        /// <summary>The towed half. Meaningless unless <see cref="Tows"/>.</summary>
        public readonly VehicleTowedSpec Towed;

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
            Tows = false;
            Towed = default;
        }

        /// <summary>The same trip, with a trailer on the plate.</summary>
        public VehicleTripSpec(Vector2[] outbound, Vector2[] returnLeg, Vector2 originPost,
                               Vector2 originPostFacing, Vector2 destinationPost,
                               Vector2 destinationPostFacing, Vector2 doorLocal,
                               float outboundDepartureHour, float returnDepartureHour,
                               float cruiseMetresPerSecond, float walkMetresPerSecond,
                               in VehicleTowedSpec towed)
            : this(outbound, returnLeg, originPost, originPostFacing, destinationPost,
                   destinationPostFacing, doorLocal, outboundDepartureHour, returnDepartureHour,
                   cruiseMetresPerSecond, walkMetresPerSecond)
        {
            Tows = true;
            Towed = towed;
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
    ///
    /// <para>⭐⭐ <b>A TRIP THAT TOWS RUNS TEN BLOCKS, and the two extra ones are the WORK.</b> The pin,
    /// the legs and the walk to the street-side handle are a beat of their own at each end
    /// (<see cref="VehicleTripStage.Coupling"/> / <see cref="VehicleTripStage.Uncoupling"/>), on the
    /// hours the walk itself takes — so the yard shows a driver coupling up rather than a trailer
    /// appearing behind a truck (P3). The trailer's own pose comes from
    /// <see cref="TowedFollowTrack"/>, which is <see cref="VehicleCouplingMath.FollowStep"/>: the
    /// player's tow and this one are one computation.</para>
    ///
    /// <para>⚠️⚠️ <b>A COUPLED PAIR CANNOT TURN IN A BAY, AND THAT IS WHAT THE REFUSALS ARE ABOUT.</b> A
    /// posed body cannot reverse, so an eight-block trip solves "the way she arrived is the reverse of
    /// the way she must leave" by pivoting her about her own centre while her driver walks over. With a
    /// 53-footer on the pin that pivot would sweep the trailer through two neighbouring bays. So a towing
    /// trip does not pivot at all: BOTH ends have to be pull-throughs — she leaves on the heading she
    /// arrived on — and <see cref="Build"/> refuses, by name and with the miss measured, when they are
    /// not. That is a real constraint on where a towing trip can run and it is meant to be read as one.</para>
    /// </summary>
    public sealed class VehicleTripPlan
    {
        /// <summary>How many blocks a trip has: rest, board, drive, alight — at each end. Fixed, because
        /// a trip that is not "there and back" is two trips.</summary>
        public const int LegCount = 8;

        /// <summary>How many blocks a TOWING trip has: the same eight, plus the couple at the start of
        /// the day and the uncouple at the end of it.</summary>
        public const int TowedLegCount = 10;

        /// <summary>Block indices, named so a test and a failure message do not count on their
        /// fingers.</summary>
        public const int LegRestAtOrigin = 0, LegBoardAtOrigin = 1, LegDriveOut = 2, LegAlightAtDestination = 3,
                         LegRestAtDestination = 4, LegBoardAtDestination = 5, LegDriveHome = 6,
                         LegAlightAtOrigin = 7;

        /// <summary>The towing layout's block indices. Block 0 and block 1 keep their meaning — she is
        /// resting, and then her driver sets off — so <see cref="RoundTripHours"/> reads the same in
        /// both.</summary>
        public const int TowedLegRestAtOrigin = 0, TowedLegCouple = 1, TowedLegBoardAtOrigin = 2,
                         TowedLegDriveOut = 3, TowedLegAlightAtDestination = 4,
                         TowedLegRestAtDestination = 5, TowedLegBoardAtDestination = 6,
                         TowedLegDriveHome = 7, TowedLegUncouple = 8, TowedLegAlightAtOrigin = 9;

        /// <summary>
        /// How many times the day is walked to find the trailer's resting pose, and how close is close
        /// enough (degrees). Numerical, not a tunable: "drive out, then drive home" is a contraction —
        /// a trailer always swings TOWARD the tractor, never away — so its fixed point is unique and
        /// four or five walks land on it. The cap is there so a degenerate spec cannot loop.
        /// </summary>
        private const int SettleWalks = 12;
        private const float SettleToleranceDegrees = 1e-4f;

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
        /// not a boarding one, and zero for EVERY block of a towing plan (a coupled pair does not turn).</summary>
        private readonly Vector2[] _turnFrom;
        private readonly Vector2[] _turnTo;

        // ---- the towed half: null on a plan that does not tow -------------------------------------------
        private readonly TowedFollowTrack _trackOut;
        private readonly TowedFollowTrack _trackHome;
        private readonly VehicleKingpin _pin;
        private readonly Vector2 _trailerRest, _trailerAtDestination;
        private readonly float _trailerRestHeading, _trailerHeadingAtDestination;

        private VehicleTripPlan(float[] departureHours, VehicleTripStage[] stages, bool[] driverAboard,
                                ScheduledLegs machine, ScheduledLegs driver, Vector2[] turnFrom,
                                Vector2[] turnTo, float secondsPerGameHour,
                                TowedFollowTrack trackOut, TowedFollowTrack trackHome,
                                in VehicleKingpin pin)
        {
            DepartureHours = departureHours;
            Stages = stages;
            DriverAboard = driverAboard;
            _machine = machine;
            _driver = driver;
            _turnFrom = turnFrom;
            _turnTo = turnTo;
            SecondsPerGameHour = secondsPerGameHour;

            _trackOut = trackOut;
            _trackHome = trackHome;
            _pin = pin;

            if (trackOut == null) return;

            // ⚠️ WRAPPED. The follow accumulates — a lap of a circuit adds a whole turn — so a track
            // honestly reports a trailer at 718.8°, which is the same trailer with three fewer digits of
            // float left in her and an unreadable number in a failure message. Wrapping cannot move her
            // picture: every reader of a heading here goes through a sine and a cosine.
            _trailerRestHeading = Mathf.Repeat(trackHome.EndHeadingDegrees, 360f);
            _trailerRest = VehicleCouplingMath.BodyOriginFromKingpin(
                trackHome.PlateAt(trackHome.LengthMetres), _trailerRestHeading, pin);

            _trailerHeadingAtDestination = Mathf.Repeat(trackOut.EndHeadingDegrees, 360f);
            _trailerAtDestination = VehicleCouplingMath.BodyOriginFromKingpin(
                trackOut.PlateAt(trackOut.LengthMetres), _trailerHeadingAtDestination, pin);
        }

        /// <summary>The machine's legs — exposed so content tests can measure the road she is actually
        /// put on rather than the road somebody meant to put her on.</summary>
        public ScheduledLegs MachineLegs => _machine;

        /// <summary>Her driver's legs.</summary>
        public ScheduledLegs DriverLegs => _driver;

        /// <summary>How many blocks this plan runs — eight, or ten when she tows.</summary>
        public int BlockCount => DepartureHours.Length;

        /// <summary>True when this plan carries a towed body.</summary>
        public bool Tows => _trackOut != null;

        /// <summary>Her trailer down the road out, and her trailer coming home — the two solved tables.
        /// Exposed so a fixture can drive a live <c>TowedBody</c> over exactly these stations.</summary>
        public TowedFollowTrack TrackOut => _trackOut;

        /// <inheritdoc cref="TrackOut"/>
        public TowedFollowTrack TrackHome => _trackHome;

        /// <summary>⭐ Where the trailer is left standing between trips — the SETTLED pose, which is the
        /// fixed point of the day rather than the bay the region typed. See <see cref="Build"/>.</summary>
        public Vector2 TrailerRestPosition => _trailerRest;

        /// <inheritdoc cref="TrailerRestPosition"/>
        public float TrailerRestHeadingDegrees => _trailerRestHeading;

        /// <summary>Where she stands at the origin bay.</summary>
        public Vector2 OriginBay => _machine.PointAt(0, 0f);

        /// <summary>Where she stands at the far bay.</summary>
        public Vector2 DestinationBay =>
            _machine.PointAt(Tows ? TowedLegRestAtDestination : LegRestAtDestination, 0f);

        /// <summary>How long the whole round trip takes, in game hours, door to door — the number a
        /// content test measures against the day so a timetable that cannot fit fails loudly.</summary>
        public float RoundTripHours => DaySchedule.ElapsedHours(DepartureHours[0], DepartureHours[1]);

        /// <summary>
        /// <b>Build the timetable from the geometry.</b> Only two hours are authored; the others fall out
        /// of how long each leg takes at its own speed, so a route the owner lengthens automatically
        /// arrives later rather than teleporting to keep an authored arrival.
        ///
        /// <para>Returns null with a stated <paramref name="problem"/> for a spec that cannot make a trip
        /// — the same fail-loud-and-stand-still contract <c>RoutinePlanner</c> keeps, because a machine
        /// that quietly does not move is indistinguishable from one that has not been authored yet.</para>
        ///
        /// <para>⭐⭐ <b>A TOWING SPEC IS CHECKED AGAINST THE SHIPPED CAPTURE TEST, NOT AGAINST A
        /// TOLERANCE INVENTED HERE.</b> Three things have to be true and each is refused by name:</para>
        ///
        /// <list type="number">
        ///   <item>the FAR bay is a pull-through — she leaves it on the heading she arrived on, because a
        ///   coupled pair cannot pivot;</item>
        ///   <item>standing at her home bay on the heading the road out leaves on, her plate CAPTURES the
        ///   trailer the region stood there (<see cref="VehicleCouplingMath.WouldCapture"/> — the same
        ///   three conditions the player's hitch asks);</item>
        ///   <item>and the DAY CLOSES: the trailer the road home leaves behind is still on that plate, so
        ///   tomorrow's couple is offered. A trailer is not saved — she is re-derived from the clock like
        ///   everything else — so a day that does not close is not a trailer slowly drifting, it is a
        ///   trailer that teleports at midnight.</item>
        /// </list>
        ///
        /// <para>⭐ <b>Where she rests is SOLVED, not authored.</b> "Drive out, then drive home" is a map
        /// from her resting heading to her resting heading, and it is a contraction — so it has exactly
        /// one fixed point, and that is where a trailer on this road actually ends up. The plan walks the
        /// day a few times to find it and then builds the two tables from it, which is what makes the
        /// pose plan exactly periodic: no snap at midnight, and no drift. The region's authored bay is
        /// the SEED and the sanity check, never the answer.</para>
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

            return spec.Tows
                ? BuildTowing(spec, secondsPerGameHour, originBay, destinationBay, arriveAtDestination,
                              arriveAtOrigin, leaveOrigin, leaveDestination, out problem)
                : BuildPlain(spec, secondsPerGameHour, originBay, destinationBay, arriveAtDestination,
                             arriveAtOrigin, leaveOrigin, leaveDestination);
        }

        /// <summary>The eight-block trip: nobody on the pin, so she turns in the bay while her driver
        /// walks over. Unchanged since #728 — a towing plan is a different shape, not a modified one.</summary>
        private static VehicleTripPlan BuildPlain(in VehicleTripSpec spec, float secondsPerGameHour,
                                                  Vector2 originBay, Vector2 destinationBay,
                                                  Vector2 arriveAtDestination, Vector2 arriveAtOrigin,
                                                  Vector2 leaveOrigin, Vector2 leaveDestination)
        {
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
                                       secondsPerGameHour, null, null, default);
        }

        /// <summary>
        /// ⭐⭐ <b>The ten-block trip.</b> See <see cref="Build"/> for the three refusals and for why the
        /// trailer's resting pose is solved rather than authored.
        /// </summary>
        private static VehicleTripPlan BuildTowing(in VehicleTripSpec spec, float secondsPerGameHour,
                                                   Vector2 originBay, Vector2 destinationBay,
                                                   Vector2 arriveAtDestination, Vector2 arriveAtOrigin,
                                                   Vector2 leaveOrigin, Vector2 leaveDestination,
                                                   out string problem)
        {
            problem = null;
            VehicleTowedSpec towed = spec.Towed;

            if (!towed.Wheel.Published)
            { problem = "the machine publishes no fifth wheel — she does not tow"; return null; }
            if (!towed.Pin.Published)
            { problem = "the towed body publishes no kingpin — there is nothing to hook"; return null; }

            float band = VehicleCouplingMath.CaptureHeadingToleranceDegrees(towed.Wheel);
            float cap = VehicleCouplingMath.JackknifeCapDegrees(towed.Pin, towed.Wheel);

            float leaveOriginBearing = BoatKinematics.BearingDegrees(leaveOrigin);
            float arriveOriginBearing = BoatKinematics.BearingDegrees(arriveAtOrigin);
            float arriveDestinationBearing = BoatKinematics.BearingDegrees(arriveAtDestination);
            float leaveDestinationBearing = BoatKinematics.BearingDegrees(leaveDestination);

            // ⚠️ REFUSALS 1 AND 2 — BOTH bays have to be pull-throughs. She arrives coupled and leaves
            // coupled, and there is no pivot available in between: the eight-block trip's turn-in-the-bay
            // would sweep a 53-footer through whatever is parked either side of her. An out-and-back on
            // one road ALWAYS fails this, and that is the point — it is a true fact about towing a
            // trailer, not a limitation of the plan.
            problem = PullThrough("home", arriveOriginBearing, leaveOriginBearing, band)
                      ?? PullThrough("far", arriveDestinationBearing, leaveDestinationBearing, band);
            if (problem != null) return null;

            Vector2 doorOrigin = DoorWorld(originBay, leaveOrigin, spec.DoorLocal);
            Vector2 doorDestination = DoorWorld(destinationBay, arriveAtDestination, spec.DoorLocal);

            // The street-side release, swung through the heading she is standing on. ⚠️ ONE point for
            // both beats, because a towing trip does not turn in either bay — which is the whole reason
            // the refusals above exist.
            Vector2 handle = DoorWorld(originBay, leaveOrigin, towed.Wheel.ReleaseHandleLocal);

            Vector2 originFacing = Unit(spec.OriginPostFacing, -arriveAtOrigin);
            Vector2 destinationFacing = Unit(spec.DestinationPostFacing, -arriveAtDestination);

            var machine = ScheduledLegs.Build(new[]
            {
                new[] { originBay },              // 0 rest
                new[] { originBay },              // 1 the couple — she does not move, and does not turn
                new[] { originBay },              // 2 boarding
                spec.Outbound,                    // 3 the road out, towing
                new[] { destinationBay },         // 4 alighting
                new[] { destinationBay },         // 5 rest at the far bay
                new[] { destinationBay },         // 6 boarding for home
                spec.Return,                      // 7 the road home, towing
                new[] { originBay },              // 8 the uncouple
                new[] { originBay },              // 9 alighting
            }, new[]
            {
                0f, 0f, 0f, spec.CruiseMetresPerSecond, 0f, 0f, 0f, spec.CruiseMetresPerSecond, 0f, 0f,
            }, new[]
            {
                leaveOrigin, leaveOrigin, leaveOrigin, arriveAtDestination, arriveAtDestination,
                arriveAtDestination, arriveAtDestination, arriveAtOrigin, leaveOrigin, leaveOrigin,
            });

            var driver = ScheduledLegs.Build(new[]
            {
                new[] { spec.OriginPost },                          // 0 at his post
                new[] { spec.OriginPost, handle },                  // 1 out to the handle: the pin
                new[] { handle, doorOrigin },                       // 2 along the tractor to her door
                new[] { doorOrigin },                               // 3 aboard
                new[] { doorDestination, spec.DestinationPost },    // 4 down and over to his far post
                new[] { spec.DestinationPost },                     // 5 the thing he drove there to do
                new[] { spec.DestinationPost, doorDestination },    // 6 back to her door
                new[] { doorDestination },                          // 7 aboard
                new[] { doorOrigin, handle },                       // 8 down to the handle: legs, then pin
                new[] { handle, spec.OriginPost },                  // 9 back to his post
            }, new[]
            {
                0f, spec.WalkMetresPerSecond, spec.WalkMetresPerSecond, 0f, spec.WalkMetresPerSecond,
                0f, spec.WalkMetresPerSecond, 0f, spec.WalkMetresPerSecond, spec.WalkMetresPerSecond,
            }, new[]
            {
                originFacing, originFacing, originFacing, originFacing, destinationFacing,
                destinationFacing, destinationFacing, destinationFacing, originFacing, originFacing,
            });

            // ---- settle the day: where a trailer on THESE two roads actually comes to rest -------------
            int outStart = machine.Start[TowedLegDriveOut], outCount = machine.Count[TowedLegDriveOut];
            int homeStart = machine.Start[TowedLegDriveHome], homeCount = machine.Count[TowedLegDriveHome];
            var plateLocal = new Vector2(towed.Wheel.CouplingPointLocal.x, towed.Wheel.CouplingPointLocal.y);

            float rest = towed.TrailerHeadingDegrees;
            for (int i = 0; i < SettleWalks; i++)
            {
                float atDestination = TowedFollowTrack.WalkHeading(
                    machine.Waypoints, outStart, outCount, plateLocal, towed.Pin, cap, rest);
                float home = TowedFollowTrack.WalkHeading(
                    machine.Waypoints, homeStart, homeCount, plateLocal, towed.Pin, cap, atDestination);

                bool settled = Mathf.Abs(Mathf.DeltaAngle(home, rest)) < SettleToleranceDegrees;

                // ⚠️ Wrapped, because FollowStep ACCUMULATES: a lap of a circuit adds a whole turn to
                // her heading, and a resting pose reported as 1078.9° is the same trailer with three
                // fewer digits of float left. The comparison above is DeltaAngle, so wrapping cannot
                // move the fixed point.
                rest = Mathf.Repeat(home, 360f);
                if (settled) break;
            }

            TowedFollowTrack trackOut = TowedFollowTrack.Build(
                machine.Waypoints, outStart, outCount, plateLocal, towed.Pin, cap, rest);
            TowedFollowTrack trackHome = TowedFollowTrack.Build(
                machine.Waypoints, homeStart, homeCount, plateLocal, towed.Pin, cap,
                trackOut.EndHeadingDegrees);

            // ⚠️ REFUSAL 3 — the trailer the REGION stood at the bay has to be on the plate. This is the
            // shipped capture test, asked of the stance she actually rests in.
            if (!VehicleCouplingMath.WouldCapture(towed.Wheel, towed.Pin, originBay, leaveOriginBearing,
                                                  towed.TrailerBay, towed.TrailerHeadingDegrees))
            {
                problem = Miss("the trailer the region stood at the bay is not on her plate",
                               towed, originBay, leaveOriginBearing, towed.TrailerBay,
                               towed.TrailerHeadingDegrees, band);
                return null;
            }

            // ⚠️ REFUSAL 4 — and the day has to CLOSE. Nothing about a trailer is saved, so a trip whose
            // road home leaves her somewhere else does not drift: it teleports her at midnight, and
            // tomorrow's couple is refused.
            Vector2 settledRest = VehicleCouplingMath.BodyOriginFromKingpin(
                trackHome.PlateAt(trackHome.LengthMetres), trackHome.EndHeadingDegrees, towed.Pin);

            if (!VehicleCouplingMath.WouldCapture(towed.Wheel, towed.Pin, originBay, leaveOriginBearing,
                                                  settledRest, trackHome.EndHeadingDegrees))
            {
                problem = Miss("the day does not close — the road home does not leave the trailer where " +
                               "she was picked up, so tomorrow's couple would be refused",
                               towed, originBay, leaveOriginBearing, settledRest,
                               trackHome.EndHeadingDegrees, band);
                return null;
            }

            // ---- the timetable: still two authored hours, eight derived ------------------------------
            var hours = new float[TowedLegCount];
            hours[TowedLegCouple] = DaySchedule.Wrap24(spec.OutboundDepartureHour);
            hours[TowedLegBoardAtOrigin] = Next(hours[TowedLegCouple], driver, TowedLegCouple, secondsPerGameHour);
            hours[TowedLegDriveOut] =
                Next(hours[TowedLegBoardAtOrigin], driver, TowedLegBoardAtOrigin, secondsPerGameHour);
            hours[TowedLegAlightAtDestination] =
                Next(hours[TowedLegDriveOut], machine, TowedLegDriveOut, secondsPerGameHour);
            hours[TowedLegRestAtDestination] =
                Next(hours[TowedLegAlightAtDestination], driver, TowedLegAlightAtDestination, secondsPerGameHour);

            hours[TowedLegBoardAtDestination] = DaySchedule.Wrap24(spec.ReturnDepartureHour);
            hours[TowedLegDriveHome] =
                Next(hours[TowedLegBoardAtDestination], driver, TowedLegBoardAtDestination, secondsPerGameHour);
            hours[TowedLegUncouple] =
                Next(hours[TowedLegDriveHome], machine, TowedLegDriveHome, secondsPerGameHour);
            hours[TowedLegAlightAtOrigin] =
                Next(hours[TowedLegUncouple], driver, TowedLegUncouple, secondsPerGameHour);
            hours[TowedLegRestAtOrigin] =
                Next(hours[TowedLegAlightAtOrigin], driver, TowedLegAlightAtOrigin, secondsPerGameHour);

            var stages = new[]
            {
                VehicleTripStage.Resting, VehicleTripStage.Coupling, VehicleTripStage.Boarding,
                VehicleTripStage.Driving, VehicleTripStage.Alighting, VehicleTripStage.Resting,
                VehicleTripStage.Boarding, VehicleTripStage.Driving, VehicleTripStage.Uncoupling,
                VehicleTripStage.Alighting,
            };
            var aboard = new[] { false, false, false, true, false, false, false, true, false, false };

            return new VehicleTripPlan(hours, stages, aboard, machine, driver,
                                       new Vector2[TowedLegCount], new Vector2[TowedLegCount],
                                       secondsPerGameHour, trackOut, trackHome, towed.Pin);
        }

        /// <summary>⚠️ <b>Is this bay one she can drive out of the way she came in?</b> A coupled pair
        /// cannot pivot, so the answer has to be yes at both ends of a towing trip. The band is the
        /// slot's OWN alignment window — below it the pair is straighter than the coupling itself can
        /// tell, which is the only honest place to draw the line.</summary>
        private static string PullThrough(string which, float arriveBearing, float leaveBearing,
                                          float band)
        {
            float turn = Mathf.Abs(Mathf.DeltaAngle(arriveBearing, leaveBearing));
            if (turn <= band) return null;

            return $"a coupled pair cannot turn in a bay: she comes into the {which} bay on " +
                   $"{arriveBearing:0.0}° and leaves it on {leaveBearing:0.0}°, {turn:0.0}° apart " +
                   $"against the slot's own {band:0.00}° — that end has to be a pull-through she drives " +
                   "out of the way she came in";
        }

        /// <summary>A refused couple, with the miss measured in the two units a reader can act on: how far
        /// the pin is from the slot along the tractor's own axes, and how far across it she lies.</summary>
        private static string Miss(string what, in VehicleTowedSpec towed, Vector2 tractorOrigin,
                                   float tractorHeading, Vector2 trailerOrigin, float trailerHeading,
                                   float band)
        {
            Vector2 pinWorld = trailerOrigin + VehicleCouplingMath.LocalOffsetToWorld(
                new Vector2(towed.Pin.CouplingPointLocal.x, towed.Pin.CouplingPointLocal.y),
                trailerHeading);
            Vector2 local = VehicleCouplingMath.WorldOffsetToLocal(pinWorld - tractorOrigin, tractorHeading);
            Vector2 slot = new Vector2(towed.Wheel.CouplingPointLocal.x,
                                       (towed.Wheel.RampMouthY + towed.Wheel.SlotSeatY) * 0.5f);
            float across = Mathf.Abs(Mathf.DeltaAngle(tractorHeading, trailerHeading));

            return $"{what}: her pin sits {local.x - slot.x:0.00} m off the slot's centreline and " +
                   $"{local.y - slot.y:0.00} m fore-and-aft of its middle, lying {across:0.0}° across a " +
                   $"{band:0.00}° window (tractor {tractorHeading:0.0}°, trailer {trailerHeading:0.0}°)";
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
        ///
        /// <para>⚠️ <b>A TOWING PLAN NEVER TURNS IN EITHER BAY</b> — <see cref="Build"/> refuses the
        /// geometry that would need it, so <c>_turnTo</c> is empty and the trailer behind her is safe.</para>
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
            if (!Tows)
                return new VehicleTripPose(machineAt, machineDir, driverAt, driverDir, aboard,
                                           machineMoving, driverMoving && !aboard, leg, Stages[leg]);

            TrailerAt(leg, elapsed, out Vector2 trailerAt, out float trailerHeading, out bool coupled,
                      out float legsUp);
            return new VehicleTripPose(machineAt, machineDir, driverAt, driverDir, aboard, machineMoving,
                                       driverMoving && !aboard, leg, Stages[leg], true, trailerAt,
                                       NavMath.DirectionFromBearing(trailerHeading), coupled, legsUp);
        }

        /// <summary>
        /// ⭐ <b>Where the trailer is in this block.</b> Three answers and no fourth: she is on her legs
        /// in her bay, she is standing coupled at the far end, or she is on the road — and on the road
        /// her pose comes off the track that was solved from the player's own follow.
        ///
        /// <para><b>The two stationary poses are the TRACKS' OWN ENDS</b>, not the region's authored bay.
        /// That is what makes the day seamless: the road out starts from exactly where the road home left
        /// her, and both are the settled fixed point, so nothing moves at a block boundary.</para>
        ///
        /// <para>The legs are a fraction rather than a flag because they are a crank the player turns:
        /// they rise while her driver walks from the handle to the door, and wind down while he walks
        /// back — the crank's own published time is what the shipped hitch uses, and the block is what
        /// this one has (<c>VehicleTripStage.Coupling</c>).</para>
        /// </summary>
        private void TrailerAt(int leg, float elapsedHours, out Vector2 origin, out float headingDegrees,
                               out bool coupled, out float legsUp)
        {
            coupled = leg >= TowedLegBoardAtOrigin && leg <= TowedLegUncouple;

            switch (leg)
            {
                case TowedLegDriveOut:
                    _trackOut.SampleAt(MachineDistance(leg, elapsedHours), out origin, out headingDegrees);
                    legsUp = 1f;
                    return;

                case TowedLegDriveHome:
                    _trackHome.SampleAt(MachineDistance(leg, elapsedHours), out origin, out headingDegrees);
                    legsUp = 1f;
                    return;

                case TowedLegAlightAtDestination:
                case TowedLegRestAtDestination:
                case TowedLegBoardAtDestination:
                    origin = _trailerAtDestination;
                    headingDegrees = _trailerHeadingAtDestination;
                    legsUp = 1f;
                    return;

                default:
                    origin = _trailerRest;
                    headingDegrees = _trailerRestHeading;
                    legsUp = leg == TowedLegBoardAtOrigin
                        ? Progress(elapsedHours, _driver.TravelHours(leg, SecondsPerGameHour))
                        : leg == TowedLegUncouple
                            ? 1f - Progress(elapsedHours, _driver.TravelHours(leg, SecondsPerGameHour))
                            : 0f;
                    return;
            }
        }

        /// <summary>How far along her own route the machine has got this block — the same clamp
        /// <c>ScheduledLegs.Sample</c> applies, asked for the number rather than the point because the
        /// trailer's table is indexed by distance.</summary>
        private float MachineDistance(int leg, float elapsedHours)
        {
            float travelled = DaySchedule.DistanceTravelled(
                elapsedHours, _machine.SpeedMetresPerSecond[leg], SecondsPerGameHour);
            return Mathf.Clamp(travelled, 0f, _machine.LengthMetres[leg]);
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
        /// local (x, y) swings with her. Also what puts the street-side release handle in the world.</summary>
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
