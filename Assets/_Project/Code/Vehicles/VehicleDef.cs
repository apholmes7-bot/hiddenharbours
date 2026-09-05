using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Vehicles
{
    /// <summary>
    /// <b>One road vehicle, as a committed asset (ADR 0035)</b> — what she is, what she looks like,
    /// and how she drives. The <c>vehicle.*</c> half of "content is data, not code" (rule 2): a new
    /// truck is a new asset with a stable id, never a new class.
    ///
    /// <para><b>Shaped after <c>BoatHullDef</c>'s Engine branch on purpose</b>, because the two
    /// answer the same questions — mass, power, how hard she turns, how much the world drags on her,
    /// how far the camera pulls back — and an owner who has tuned a boat should recognise every
    /// field here. What is deliberately absent is everything the sea owns: no draught, no
    /// seaworthiness, no seakeeping, no ground tackle. A truck does not have a sea state.</para>
    ///
    /// <para><b>The handling numbers here are TUNABLES; the geometry ones are not.</b> Wheelbase,
    /// track, lock angle and wheel radius live on <see cref="VehicleMeshDef"/> because they are
    /// facts about the artwork, read off the rig by the baker and not the owner's to change — move
    /// one and the wheels stop matching the picture. Everything on this asset is feel, and the owner
    /// is meant to change it.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleDef", menuName = "Hidden Harbours/Vehicle", order = 60)]
    public class VehicleDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): vehicle.snake_case — 'vehicle.dually_3500'.")]
        public string Id = "vehicle.unnamed";

        public string DisplayName = "Unnamed Vehicle";

        [Tooltip("WHAT KIND of machine she is, as a token VehicleKinds translates: 'road_vehicle' " +
                 "(roads and yards only — water is a wall) or 'amphibious_vehicle' (drives ashore " +
                 "and swims). The art side's own shipped spellings are accepted too; " +
                 "VehicleKinds.CanonicalToken is what an asset we WRITE should carry.\n\n" +
                 "⚠️ A token, not the enum, and it reaches code through the one translation table " +
                 "rather than through a literal at a call site — the rule VehicleKinds exists to " +
                 "enforce. An unrecognised word makes the whole def unusable (see IsUsable) rather " +
                 "than quietly becoming a road vehicle.")]
        public string KindToken = "road_vehicle";

        [Header("Art")]
        [Tooltip("The baked mesh and the chassis geometry the controller solves against. Without one " +
                 "she cannot be drawn OR driven — the wheelbase and lock angles live there.")]
        public VehicleMeshDef Mesh;

        [Header("Mass & power")]
        [Tooltip("Kerb mass. Not yet used by the kinematic drive model — recorded because the ferry, " +
                 "the wharf crane and any future load limit will all ask, and because a one-tonne " +
                 "dually's whole identity is what she can carry.")]
        [Min(1f)] public float MassKg = 3500f;

        [Tooltip("Top speed on good ground, metres per second. 11 m/s ≈ 40 km/h — a working speed " +
                 "for gravel, not a highway.")]
        [Min(0.1f)] public float MaxSpeedMetersPerSecond = 11f;

        [Tooltip("Top speed in reverse, metres per second. Lower than ahead, as a real gearbox is.")]
        [Min(0.1f)] public float MaxReverseSpeedMetersPerSecond = 4f;

        [Tooltip("How hard she pulls away, metres per second squared, at full throttle.")]
        [Min(0.1f)] public float AccelerationMetersPerSecondSquared = 4.5f;

        [Tooltip("How hard she stops under braking, metres per second squared. Higher than the " +
                 "acceleration — every vehicle stops harder than it starts.")]
        [Min(0.1f)] public float BrakingMetersPerSecondSquared = 8f;

        [Tooltip("Deceleration with the throttle released and no brake, metres per second squared — " +
                 "engine braking plus rolling resistance. Small: a truck coasts a long way.")]
        [Min(0f)] public float CoastDecelerationMetersPerSecondSquared = 2.2f;

        [Header("Steering feel")]
        [Tooltip("How fast the steering wheel itself moves, in units of full lock per second. The " +
                 "LOCK ANGLES are art (VehicleMeshDef); this is how quickly the driver can reach " +
                 "them, and it is the difference between a truck and a go-kart. 2 = half a second " +
                 "from centre to full lock.")]
        [Min(0.1f)] public float SteerRateFullLocksPerSecond = 2f;

        [Tooltip("How fast the wheel self-centres when the driver lets go, in full locks per second. " +
                 "Faster than the input rate, as a real castering front end is.")]
        [Min(0f)] public float SteerReturnFullLocksPerSecond = 3f;

        [Tooltip("Speed (m/s) at which steering authority has fallen to half. Above it she turns " +
                 "lazily, below it she is nimble — the geometric bicycle model on its own turns " +
                 "TIGHTER the faster you go, which is exactly backwards from how a vehicle feels. " +
                 "0 disables the falloff and leaves the pure geometric model.")]
        [Min(0f)] public float SteerFalloffHalfSpeedMetersPerSecond = 9f;

        [Header("Skid steer (a machine with no steering axle — SkidSteerMath)")]
        [Tooltip("How fast she spins ON THE SPOT at full stick, degrees per second, on the ground. " +
                 "The skid machine's whole steering feel in one number: a differential's yaw rate " +
                 "does not depend on how fast she is going, so this is not a maximum she works up " +
                 "to — it is what full stick means.\n\n" +
                 "Ignored by a machine with a steering axle: hers comes off the rig's lock angles, " +
                 "which are art and not the owner's to tune.")]
        [Min(0f)] public float SpinRateDegreesPerSecond = 75f;

        [Tooltip("The same, AFLOAT. Lower: she is pushing water with eight tires rather than " +
                 "digging into gravel, and a machine that pivots as briskly in a cove as in a yard " +
                 "reads as weightless.")]
        [Min(0f)] public float AfloatSpinRateDegreesPerSecond = 30f;

        [Header("Afloat (an amphibian's second set of pedals)")]
        [Tooltip("Top speed swimming, metres per second. Her propulsion afloat is the TIRES — the " +
                 "rig publishes no prop, no jet and no rudder — so this is a walking pace at best " +
                 "and is meant to be: crossing water is a decision, not a shortcut (P5).")]
        [Min(0.1f)] public float AfloatMaxSpeedMetersPerSecond = 1.6f;

        [Tooltip("Top speed swimming astern, metres per second.")]
        [Min(0.1f)] public float AfloatMaxReverseSpeedMetersPerSecond = 0.9f;

        [Tooltip("How hard she gathers way afloat, metres per second squared. Small — eight tires " +
                 "are a poor paddle.")]
        [Min(0.1f)] public float AfloatAccelerationMetersPerSecondSquared = 0.8f;

        [Tooltip("How fast the water takes her way off, metres per second squared, with the " +
                 "throttle shut.\n\n" +
                 "⚠️ It is ALSO what the brake does afloat, because there is no brake afloat: a " +
                 "wheel brake in the water stops the tires and nothing else. The pedal is not " +
                 "refused — refusing an input the player is holding reads as a bug — it simply has " +
                 "the same authority the water already had.")]
        [Min(0.1f)] public float AfloatDragDecelerationMetersPerSecondSquared = 1.2f;

        [Tooltip("The dead band either side of her float draft, in metres, that keeps the water's " +
                 "edge from being a flicker line. She floats past (draft + this) and takes the " +
                 "beach again below (draft − this), so a wave, a tide inch or a wheel-radius " +
                 "sample cannot swap her medium several times a second.\n\n" +
                 "Feel, not art: the DRAFT is measured off the rig (VehicleMeshDef.FloatDraftMeters) " +
                 "and is not the owner's; this band is.")]
        [Min(0f)] public float SwimHysteresisMeters = 0.06f;

        [Header("Following a road (how a driver who is not the player drives her — RouteFollowMath)")]
        [Tooltip("How close (m) to a waypoint counts as arrived, BEFORE the perpendicular rule. Small on " +
                 "its own, and widening it is not the fix for an overshot waypoint: passing its " +
                 "perpendicular is, and that is geometry rather than a tunable.")]
        [Min(0f)] public float RouteWaypointReachMeters = 3f;

        [Tooltip("How far ahead (m) along the road she aims. Too short and she saws down a straight; too " +
                 "long and she cuts every corner. 12 m is a little under two truck lengths.")]
        [Min(0f)] public float RouteLookaheadMeters = 12f;

        [Tooltip("Throttle fraction on the straight, 0–1 of her own ceiling.")]
        [Range(0f, 1f)] public float RouteCruiseThrottle = 0.6f;

        [Tooltip("Throttle fraction through a turn, 0–1. Well under cruise: speed-sensitive steering " +
                 "widens every machine's circle, so a driver hunting a waypoint at full throttle orbits " +
                 "it rather than reaching it.")]
        [Range(0f, 1f)] public float RouteTurnThrottle = 0.15f;

        [Tooltip("How far off (degrees) her nose has to be from where she is aiming before she comes off " +
                 "cruise — the 'brake for the turn ahead' knob. An ANGLE, not a radius: a driver aiming " +
                 "at a lookahead point has a heading error already in hand and no radius at all.")]
        [Min(0f)] public float RouteSlowForTurnDegrees = 12f;

        [Tooltip("Degrees of heading error that mean full lock. Smaller is twitchier.")]
        [Min(0f)] public float RouteSteerGainDegrees = 20f;

        [Header("Camera")]
        [Tooltip("How much world the camera shows while driving her, in metres of height. Wider than " +
                 "a boat's default: she covers ground faster than anything else the player controls.")]
        [Min(1f)] public float CameraWorldHeightMeters = 18f;

        /// <summary>
        /// What kind of machine she is — resolved through <see cref="VehicleKinds"/>, the ONE table
        /// that turns a token into a kind, never a literal here.
        ///
        /// <para><b>An unresolvable token reads as <see cref="VehicleKind.RoadVehicle"/>, and the
        /// direction of that fallback is the whole argument for it.</b> <see cref="VehicleKinds"/>
        /// refuses an unknown SHIPPED token because either mistake is possible there — an amphibian
        /// read as a truck, or a truck read as an amphibian. Here only one of the two can happen: a
        /// def whose token did not resolve is already refused by <see cref="IsUsable"/> and never
        /// reaches the road, and if she somehow did, an amphibian mis-read as a road vehicle is
        /// simply held at the water's edge — an unbuilt feature. The opposite default would drive a
        /// three-and-a-half-tonne truck into the harbour.</para>
        /// </summary>
        public VehicleKind Kind =>
            VehicleKinds.TryFromToken(KindToken, out VehicleKind kind) ? kind : VehicleKind.RoadVehicle;

        /// <summary>
        /// Her route-following feel, as the one struct <see cref="RouteFollowMath"/> reads.
        ///
        /// <para>⚠️ Raw, deliberately — call <c>.Sane()</c> before driving on it. A def baked before these
        /// fields existed deserializes them all to zero, and a zero lookahead is a driver aiming at her
        /// own bumper; filling in belongs at the point of USE so a content test can still see the honest
        /// zeros and say so.</para>
        /// </summary>
        public RouteFollowMath.RouteFollowTuning RouteFollow => new(
            RouteWaypointReachMeters, RouteLookaheadMeters, RouteCruiseThrottle,
            RouteTurnThrottle, RouteSlowForTurnDegrees, RouteSteerGainDegrees);

        /// <summary>Her drive envelope on the ground — what the pedals mean on gravel.</summary>
        public DriveEnvelope LandEnvelope => new(
            MaxSpeedMetersPerSecond, MaxReverseSpeedMetersPerSecond,
            AccelerationMetersPerSecondSquared, BrakingMetersPerSecondSquared,
            CoastDecelerationMetersPerSecondSquared);

        /// <summary>
        /// Her drive envelope AFLOAT. The drag rate stands in for both the brake and the coast,
        /// because in the water they are the same thing (see
        /// <see cref="AfloatDragDecelerationMetersPerSecondSquared"/>).
        /// </summary>
        public DriveEnvelope AfloatEnvelope => new(
            AfloatMaxSpeedMetersPerSecond, AfloatMaxReverseSpeedMetersPerSecond,
            AfloatAccelerationMetersPerSecondSquared, AfloatDragDecelerationMetersPerSecondSquared,
            AfloatDragDecelerationMetersPerSecondSquared);

        /// <summary>True when this def can actually be placed and driven. A vehicle without a usable
        /// mesh is refused rather than spawned invisible — and the mesh is also where her wheelbase
        /// lives, so a missing one would leave the drive model dividing by a default. A def whose
        /// <see cref="KindToken"/> nobody recognises is refused for the same reason: the kind decides
        /// whether water is a wall or a road, and guessing it is how something drowns.</summary>
        public bool IsUsable() =>
            Mesh != null && Mesh.IsUsable() && VehicleKinds.IsVehicleToken(KindToken);
    }

    /// <summary>
    /// <b>What the pedals mean in one medium</b> — the five numbers <c>VehicleController.StepSpeed</c>
    /// integrates against, lifted out of <see cref="VehicleDef"/> so the SAME integrator serves gravel
    /// and water.
    ///
    /// <para>A struct rather than a second copy of the integrator, deliberately. An amphibian's speed
    /// really does obey one law with two sets of constants — she accelerates to a ceiling, she stops
    /// under the brake, she coasts down with nothing pressed — and the alternative (a
    /// <c>StepSpeedAfloat</c> beside <c>StepSpeed</c>) is two bodies of arithmetic that must be kept
    /// in step by hand for ever. The land overload's signature is unchanged, so every existing caller
    /// and every test written against it is untouched.</para>
    /// </summary>
    public readonly struct DriveEnvelope
    {
        /// <summary>Top speed ahead, m/s.</summary>
        public readonly float MaxAheadMetersPerSecond;
        /// <summary>Top speed astern, m/s — its own, lower ceiling.</summary>
        public readonly float MaxAsternMetersPerSecond;
        /// <summary>How hard she pulls away, m/s².</summary>
        public readonly float AccelerationMetersPerSecondSquared;
        /// <summary>How hard she stops under the brake, m/s².</summary>
        public readonly float BrakingMetersPerSecondSquared;
        /// <summary>How hard she slows with nothing pressed, m/s².</summary>
        public readonly float CoastDecelerationMetersPerSecondSquared;

        public DriveEnvelope(float maxAhead, float maxAstern, float acceleration, float braking,
                             float coast)
        {
            MaxAheadMetersPerSecond = maxAhead;
            MaxAsternMetersPerSecond = maxAstern;
            AccelerationMetersPerSecondSquared = acceleration;
            BrakingMetersPerSecondSquared = braking;
            CoastDecelerationMetersPerSecondSquared = coast;
        }

        /// <summary>The ceiling in whichever direction <paramref name="speed"/> is going — what a
        /// track's own top speed is, and what the skid model's give-and-take is measured
        /// against.</summary>
        public float CeilingFor(float speed)
            => speed >= 0f ? MaxAheadMetersPerSecond : MaxAsternMetersPerSecond;
    }
}
