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
    ///   <item><b>Floats her, if she is a machine that floats</b> — see <see cref="PoseFlotation"/>.
    ///   Byte-inert for anything whose rig publishes no flotation, which is every road vehicle.</item>
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

        /// <summary>Whether the waterline clamp is currently RAISED on the renderer. Dirty-checked so
        /// the toggle is two writes per water's edge rather than two per frame — and it starts false
        /// because a freshly installed machine is installed ashore-shaped (0/0, the road vehicle's
        /// documented "clamp off").</summary>
        private bool _waterlineClampRaised;

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
            // A freshly installed renderer carries the ashore clamp (0/0), so the dirty flag must
            // agree with it or the first afloat frame would decide there was nothing to raise.
            _waterlineClampRaised = false;
        }

        private void LateUpdate() => Drive();

        /// <summary>One pose push — the LateUpdate body, callable directly so EditMode tests (where
        /// the player loop does not run) drive the exact production path.</summary>
        public void Drive()
        {
            if (_renderer == null || _visual == null || _def == null) return;

            _visual.rotation = Quaternion.identity;
            _renderer.HeadingDirUnits = CurrentDirUnits;

            PoseFlotation();
            PoseWheels();
        }

        /// <summary>
        /// ⭐ <b>Sink her onto her waterline, ride the sea she is in, and cut her at her own hull —
        /// or do exactly nothing.</b>
        ///
        /// <para><b>The sink is a runtime Z offset on the DRY mesh, and that is measured.</b>
        /// <c>OtterIsoKitProbeTests</c> posed her rig at <c>float:1</c> and compared every vertex's
        /// displacement against one common offset: max deviation <b>0</b>, translation
        /// <c>[0, 0, −0.52]</c>, linear in between. So there is no afloat bake, no second variant and
        /// no reshape — she is the same geometry, lowered. It arrives through the heave channel
        /// because the rig applies its own <c>dz</c> BEFORE projection, which is what a heave is: her
        /// z = 0 plane projects to the pivot row at every facing and every pose, so a uniform model-z
        /// offset is a uniform screen offset, and the renderer's calibrated iso z moves with it — the
        /// sea climbs to her waterline for free instead of by a second number that could disagree.</para>
        ///
        /// <para><b>The ride is the SHARED heave and nothing else.</b> No rock amplitudes, no storm
        /// loft, no head-sea pitch: the ruled scope is displacement plus the shared lift, so
        /// <see cref="IHullMeshRenderer.RollDegrees"/> and <see cref="IHullMeshRenderer.PitchDegrees"/>
        /// are never written here at all. An amphibian is a machine crossing a cove, not a hull
        /// working a sea, and the seakeeping channels are a boat's tuned data that she does not have.
        /// Like every hull's ride the sink stays inside the cell (it is the rig's own in-cell dz) and
        /// only the WORLD ride carries the compositing window with it.</para>
        ///
        /// <para><b>And the clamp follows the medium.</b> Afloat she carries her own published
        /// waterline so the sea cuts her at her hull and can never come over her transom; ashore she
        /// carries the road vehicle's 0/0 — the documented "clamp off" #560 ships — because a machine
        /// on gravel has no waterline to hold anything below. Never the HULL defaults: those are a
        /// boat's numbers about a boat's deck.</para>
        ///
        /// <para><b>Byte-inert for anything that does not float.</b> A rig with no
        /// <see cref="VehicleMeshDef.FloatSinkMeters"/> returns before touching a channel, so the
        /// Dually's render is the one #560 shipped, to the bit.</para>
        /// </summary>
        private void PoseFlotation()
        {
            if (_def.FloatSinkMeters <= 0f) return;

            bool afloat = _controller != null && _controller.IsAfloat;
            float pxPerMetre = Mathf.Max(1, _def.PxPerMetre);

            // The world's lift under her — 0 ashore, and 0 afloat with no displaced sea published
            // (the A/B contract: she then simply sits at her flat waterline).
            float rideMeters = afloat
                ? VehicleGrounding.SharedSeaRideMetersNow((Vector2)transform.position)
                : 0f;
            float sinkMeters = afloat ? _def.FloatSinkMeters : 0f;

            _renderer.HeavePixels = (rideMeters - sinkMeters) * pxPerMetre;
            _renderer.RidePixels = rideMeters * pxPerMetre;

            SetWaterlineClamp(afloat);
        }

        /// <summary>Raise or drop the renderer's watertight clamp — her own numbers afloat, 0/0
        /// ashore. Dirty-checked, so this is two writes at each water's edge rather than two every
        /// frame, and it goes through the Core presentation seam because Vehicles may not name an Art
        /// type (rule 4). No service registered — an EditMode fixture, an edit-time builder — is a
        /// no-op rather than a null reference.</summary>
        private void SetWaterlineClamp(bool afloat)
        {
            if (_waterlineClampRaised == afloat) return;

            IVehicleMeshPresentationService service = VehicleMeshPresentation.Service;
            GameObject host = _visual != null ? _visual.gameObject : null;
            if (service == null || host == null) return;

            service.SetWaterlineClamp(host,
                                      afloat ? _def.WatertightDeckHeightMeters : 0f,
                                      afloat ? _def.WatertightHalfBeamMeters : 0f);
            _waterlineClampRaised = afloat;
        }

        /// <summary>
        /// Turn and spin every fitting from the ONE steer number and the odometer readings the
        /// controller publishes, so no wheel can drift out of step with the machine.
        ///
        /// <para><b>A skid machine's two sides take their OWN distances</b> (<c>rollL</c> /
        /// <c>rollR</c>, the rig's own pair of axes): through a turn one side has genuinely travelled
        /// further than the other, and driving both off the body odometer would draw a machine that
        /// pivots with her tyres turning in step — the exact "splits without yawing" disagreement her
        /// sidecar warns the rig will not catch. Her steer angles come out 0 for free: the Ackermann
        /// solve returns nothing at all for a rig that publishes no lock angle, which is precisely
        /// what a machine with no steering axle should draw.</para>
        /// </summary>
        private void PoseWheels()
        {
            if (_wheels.Length == 0) return;

            float steer = _controller != null ? _controller.EffectiveSteer : 0f;

            VehicleSteeringMath.AckermannDegrees(
                steer, _def.MaxInnerSteerDegrees, _def.WheelbaseMeters, _def.FrontTrackMeters,
                out float leftDegrees, out float rightDegrees);

            // ⚠️ NEGATED, and this is measured rather than chosen. The rig's +roll rotates a hub
            // vertex from +y toward +z, which carries the TOP of the wheel toward the tail — a wheel
            // rolling backwards for a truck whose nose is +y. Driving forward is therefore negative
            // rig roll. A sign slip here spins all six wheels the wrong way, which is the single
            // most-noticed defect a driven vehicle can have.
            float rollLeftDegrees, rollRightDegrees;
            if (_controller != null && _controller.IsSkidSteered)
            {
                SkidSteerMath.TrackRollRevolutions(
                    _controller.LeftTrackOdometerMeters, _controller.RightTrackOdometerMeters,
                    _def.WheelRadiusMeters, out float revolutionsLeft, out float revolutionsRight);
                rollLeftDegrees = -revolutionsLeft * 360f;
                rollRightDegrees = -revolutionsRight * 360f;
            }
            else
            {
                float distance = _controller != null ? _controller.OdometerMeters : 0f;
                float revolutions =
                    -VehicleSteeringMath.RollRevolutions(distance, _def.WheelRadiusMeters);
                rollLeftDegrees = revolutions * 360f;
                rollRightDegrees = rollLeftDegrees;
            }

            for (int i = 0; i < _wheels.Length && i < _fitments.Length; i++)
            {
                IHullPropRenderer wheel = _wheels[i];
                if (wheel == null) continue;

                VehicleFitment f = _fitments[i];
                bool left = f.Side == VehicleFitmentSide.Left;
                float steerDegrees = left ? leftDegrees : rightDegrees;
                float rollDegrees = left ? rollLeftDegrees : rollRightDegrees;

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
