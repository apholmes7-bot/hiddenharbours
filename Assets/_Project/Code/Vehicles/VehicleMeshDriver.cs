using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Vehicles
{
    /// <summary>
    /// <b>The MonoBehaviour that poses a mesh vehicle (ADR 0035)</b> — the sibling of
    /// <c>MeshHullDriver</c>, on the physics root for the same reason. Each LateUpdate it:
    ///
    /// <list type="number">
    ///   <item><b>Stomps the visual child's world rotation</b> back to screen-identity. The child
    ///   must not inherit the body's physics yaw: the truck's on-screen turn is the MESH rotating
    ///   under the rig projection, not the picture rotating in screen space — inheriting both would
    ///   turn her twice.</item>
    ///   <item><b>Maps the true compass heading onto rig dir units</b> through the def's MEASURED
    ///   azimuth convention. <b>Continuous</b>, and that is the headline: the rig's own <c>yaw</c>
    ///   axis moves zero geometry — it is folded into <c>camBasis</c>, i.e. it is a camera rotation —
    ///   which is precisely what <see cref="IHullMeshRenderer.HeadingDirUnits"/> already does. So a
    ///   mesh vehicle reads at ANY heading between the eight facings for free: no yaw variants, no
    ///   second bake. Baking yaw into vertices would have turned her twice.</item>
    ///   <item><b>Poses every wheel</b> from the controller's steer and odometer.</item>
    /// </list>
    ///
    /// <para>Allocation-free per frame (rule 7): every write below is a float or a quaternion into a
    /// dirty-checked property.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-110)]   // the same slot MeshHullDriver takes, before the overlay readers
    public class VehicleMeshDriver : MonoBehaviour
    {
        private IHullMeshRenderer _renderer;
        private Transform _visual;
        private VehicleMeshDef _def;
        private VehicleController _controller;

        private VehicleFitment[] _fitments = System.Array.Empty<VehicleFitment>();
        private IHullPropRenderer[] _wheels = System.Array.Empty<IHullPropRenderer>();

        /// <summary>The visual child the renderer draws under (kept screen-identity). Null = idle.</summary>
        public Transform Visual => _visual;

        /// <summary>The rig dir units currently being presented — derived from the transform, so it
        /// is correct before the first LateUpdate.</summary>
        public float CurrentDirUnits =>
            _def == null
                ? 0f
                : HullMeshMath.HeadingToDirUnits(
                    BoatKinematics.BearingDegrees(transform.up),
                    _def.ZeroHeadingDegrees, _def.AzimuthCounterClockwise);

        /// <summary>
        /// Wire the driver — the skinner's path. Passing nulls parks it (a vehicle being torn down).
        /// </summary>
        public void Configure(Transform visual, IHullMeshRenderer renderer, VehicleMeshDef def,
                              VehicleController controller,
                              VehicleFitment[] fitments, IHullPropRenderer[] wheels)
        {
            _visual = visual;
            _renderer = renderer;
            _def = def;
            _controller = controller;
            _fitments = fitments ?? System.Array.Empty<VehicleFitment>();
            _wheels = wheels ?? System.Array.Empty<IHullPropRenderer>();
        }

        private void LateUpdate() => Drive();

        /// <summary>One pose push — the LateUpdate body, callable directly so EditMode tests (where
        /// the player loop does not run) drive the exact production path.</summary>
        public void Drive()
        {
            if (_renderer == null || _visual == null || _def == null) return;

            _visual.rotation = Quaternion.identity;
            _renderer.HeadingDirUnits = CurrentDirUnits;

            PoseWheels();
        }

        /// <summary>
        /// Turn and spin every fitting from the ONE steer number and the ONE odometer reading the
        /// controller publishes, so no wheel can drift out of step with the machine.
        /// </summary>
        private void PoseWheels()
        {
            if (_wheels.Length == 0) return;

            float steer = _controller != null ? _controller.EffectiveSteer : 0f;
            float distance = _controller != null ? _controller.OdometerMeters : 0f;

            VehicleSteeringMath.AckermannDegrees(
                steer, _def.MaxInnerSteerDegrees, _def.WheelbaseMeters, _def.FrontTrackMeters,
                out float leftDegrees, out float rightDegrees);

            // ⚠️ NEGATED, and this is measured rather than chosen. The rig's +roll rotates a hub
            // vertex from +y toward +z, which carries the TOP of the wheel toward the tail — a wheel
            // rolling backwards for a truck whose nose is +y. Driving forward is therefore negative
            // rig roll. A sign slip here spins all six wheels the wrong way, which is the single
            // most-noticed defect a driven vehicle can have.
            float revolutions = -VehicleSteeringMath.RollRevolutions(distance, _def.WheelRadiusMeters);
            float rollDegrees = revolutions * 360f;

            for (int i = 0; i < _wheels.Length && i < _fitments.Length; i++)
            {
                IHullPropRenderer wheel = _wheels[i];
                if (wheel == null) continue;

                VehicleFitment f = _fitments[i];
                float steerDegrees = f.Side == VehicleFitmentSide.Left ? leftDegrees : rightDegrees;

                wheel.LocalRotation = f.Motion switch
                {
                    // Both rotations pass through the hub centre — the steer axis is vertical
                    // THROUGH the wheel centre, with no kingpin offset modelled — so they compose
                    // about one pivot. Steer is applied outside the roll, so the axle turns with the
                    // corner and the wheel keeps rolling about its own (now-turned) axle.
                    VehicleFitmentMotion.SteerAndRoll =>
                        Quaternion.AngleAxis(steerDegrees, RigUp) *
                        Quaternion.AngleAxis(rollDegrees, RigRight),

                    VehicleFitmentMotion.SteerOnly =>
                        Quaternion.AngleAxis(steerDegrees, RigUp),

                    _ => Quaternion.AngleAxis(rollDegrees, RigRight),
                };
            }
        }

        /// <summary>The rig's own up axis (+z, out of the road). A positive rotation about it swings
        /// the nose toward −x, which is the LEFT lock the rig's <c>steer</c> sign means.</summary>
        private static readonly Vector3 RigUp = Vector3.forward;

        /// <summary>The rig's own lateral axis (+x, curb side) — every wheel's axle.</summary>
        private static readonly Vector3 RigRight = Vector3.right;
    }
}
