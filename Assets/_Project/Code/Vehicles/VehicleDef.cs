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

        [Header("Camera")]
        [Tooltip("How much world the camera shows while driving her, in metres of height. Wider than " +
                 "a boat's default: she covers ground faster than anything else the player controls.")]
        [Min(1f)] public float CameraWorldHeightMeters = 18f;

        /// <summary>True when this def can actually be placed and driven. A vehicle without a usable
        /// mesh is refused rather than spawned invisible — and the mesh is also where her wheelbase
        /// lives, so a missing one would leave the drive model dividing by a default.</summary>
        public bool IsUsable() => Mesh != null && Mesh.IsUsable();
    }
}
