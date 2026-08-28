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

        /// <summary>The extra renderers a DiscreteStates fitting needs — one per baked state, the
        /// driver showing exactly one. Null at every index whose fitting poses instead of swapping,
        /// which is all of them but a trailer's landing-gear legs.</summary>
        private IHullPropRenderer[][] _states = System.Array.Empty<IHullPropRenderer[]>();

        /// <summary>How open every opening is. Null on a machine with none.</summary>
        private VehicleDoors _doors;

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
                              VehicleFitment[] fitments, IHullPropRenderer[] wheels,
                              IHullPropRenderer[][] states = null, VehicleDoors doors = null)
        {
            _visual = visual;
            _renderer = renderer;
            _def = def;
            _controller = controller;
            _fitments = fitments ?? System.Array.Empty<VehicleFitment>();
            _wheels = wheels ?? System.Array.Empty<IHullPropRenderer>();
            _states = states ?? System.Array.Empty<IHullPropRenderer[]>();
            _doors = doors;
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

            PosePixelGrid();
            PoseFlotation();
            PoseWheels();
        }

        /// <summary>
        /// ⭐ <b>Put the machine's PICTURE back on the pixel grid</b> (owner playtest 2026-08-23 —
        /// "the running fisher and the Otter go soft during movement").
        ///
        /// <para><b>Nothing else was ever going to do it.</b> The locked
        /// <c>PixelPerfectCamera</c> runs <c>GridSnapping.PixelSnapping</c>, and that mode publishes
        /// <c>PixelPerfectRendering.pixelSnapSpacing</c> — a grid <b>SpriteRenderers</b> snap to, and
        /// only SpriteRenderers. A mesh vehicle draws through <c>IsoFacetHullRenderer</c>'s
        /// MeshRenderer (ADR 0022 phase 3), so the engine's snap never reached her at all: she
        /// rasterised at whatever sub-pixel offset her rigidbody happened to hold, and her facets'
        /// edges resampled every frame. That is the soft read — a wobble in texel SIZE, not a
        /// blur.</para>
        ///
        /// <para><b>It fixes her dither for free, and that is not a coincidence.</b> The facet
        /// shader indexes its Bayer cell from <c>(worldXY − hullOrigin)·PPU</c>, and
        /// <c>IsoFacetHullRenderer</c> takes that origin from THIS visual's own
        /// <c>transform.position</c>. Hull-locked already, so the pattern never crawled across her
        /// (ADR 0022's 13–16% crawl class was closed); but a hull-locked grid hung off a sub-pixel
        /// origin still lands its cells between screen pixels. Snapping the origin lands the whole
        /// lattice on whole pixels, so her shading quantises exactly where her silhouette does.</para>
        ///
        /// <para><b>The BODY is never touched</b> — this writes the visual child's world position and
        /// nothing else, the same visual-only discipline <c>BoatController</c> states for rigidbody
        /// interpolation. Physics, the odometer, the flotation read and every saved value keep the
        /// honest float (rule 5). The grid itself is the camera's, relayed through Core because
        /// Vehicles may not name App (rule 4); a frame where no camera has published one yet — a
        /// bare EditMode fixture, the first frame of a scene — leaves her exactly where she was.</para>
        ///
        /// <para>Dirty-checked, so the OFF path is one write at the flip rather than one per frame,
        /// and it restores <c>localPosition</c> zero: the picture the owner is A/B-ing against.</para>
        /// </summary>
        private void PosePixelGrid()
        {
            float grid = VehicleGridSnapMeters;
            if (grid <= 0f)
            {
                if (_visual.localPosition != Vector3.zero) _visual.localPosition = Vector3.zero;
                return;
            }

            Vector3 snapped = PixelGrid.Snap(transform.position, grid);
            if (_visual.position != snapped) _visual.position = snapped;
        }

        /// <summary>The pixel grid to draw on, or <b>0 for "do not snap"</b> — the owner's A/B flag
        /// off, or no camera having published a grid yet. One property so the gate is stated once and
        /// the pose body above stays about posing.</summary>
        private static float VehicleGridSnapMeters
            => GameServices.PixelGridSnap ? GameServices.WorldUnitsPerRenderedPixel : 0f;

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

                // ⭐ A DOOR is posed from how open it is, not from the road. Handled before the
                // road arms so the switch below stays about wheels — a hood driven by an odometer
                // would be a quietly hilarious bug.
                if (f.Motion == VehicleFitmentMotion.HingeRotation ||
                    f.Motion == VehicleFitmentMotion.Slide ||
                    f.Motion == VehicleFitmentMotion.DiscreteStates)
                {
                    PoseOpening(i, f, wheel);
                    continue;
                }

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

                    VehicleFitmentMotion.RollOnly =>
                        Quaternion.AngleAxis(rollDegrees, RigRight),

                    // ⚠️ NAMED ARMS AND A THROWING DEFAULT — the standard #560 set for append-only
                    // Core enums, applied here 2026-08-27. This was `_ => roll`, which is the trap
                    // that standard exists for: the next motion appended to VehicleFitmentMotion
                    // would have inherited "roll" in silence, and a part that does not turn would
                    // have spun about the axle axis every frame with nothing to grep for.
                    _ => throw new System.ArgumentOutOfRangeException(
                        nameof(f.Motion), f.Motion,
                        $"fitting '{f.Slot}' takes a motion this driver has no arm for. A baked " +
                        "asset newer than this code, or a value nobody wired — either way, posing " +
                        "it as something else is worse than stopping."),
                };
            }
        }

        /// <summary>
        /// Pose one worked opening from how open it is.
        ///
        /// <para><b>Three kinds, and the kind is a measurement.</b> A leaf that turned out to be one
        /// rigid body about a published pin gets that rotation; a part that turned out to be an exact
        /// rigid translation gets its sampled path; a part that turned out to be neither gets the
        /// mesh baked at whichever end it is nearest, because there is nothing to pose it BY. See
        /// <see cref="VehicleFitmentMotion"/> for what each was measured to be.</para>
        ///
        /// <para>⚠️ <b>The hinge sweeps the FULL published angle</b>, which for a reefer's barn leaf
        /// is 255° and not the −105° that reaches the same pose. Multiplying openness by the sweep is
        /// what keeps the leaf travelling through the fan the art published — out to full outboard
        /// at 180° before folding back along the side — rather than taking the short way through
        /// whatever is parked alongside.</para>
        ///
        /// <para>⚠️ <b>A door on a PARENT rides it.</b> The cabover's leaves are cut out of a cab that
        /// tilts, so their own swing composes INSIDE the cab's: parent first, then child, which is
        /// the order that keeps a door on its hinges when the cab goes over.</para>
        /// </summary>
        private void PoseOpening(int index, in VehicleFitment f, IHullPropRenderer part)
        {
            float open = _doors != null ? _doors.Openness(index) : 0f;

            switch (f.Motion)
            {
                case VehicleFitmentMotion.HingeRotation:
                    Vector3 axis = f.HingeAxis == VehicleHingeAxis.Lateral ? RigRight : RigUp;
                    Quaternion swing = Quaternion.AngleAxis(open * f.SweepDegrees, axis);
                    part.LocalRotation = ParentSwingOf(f.ParentSlot) * swing;
                    break;

                case VehicleFitmentMotion.Slide:
                    // FitmentOffsetMeters moves the whole fitting, pivot included — which is what a
                    // slide is. The path was sampled at its corners and asserted rigid at each.
                    part.FitmentOffsetMeters = f.SlideOffsetAt(open);
                    part.LocalRotation = ParentSwingOf(f.ParentSlot);
                    break;

                case VehicleFitmentMotion.DiscreteStates:
                    ShowState(index, f, open >= 0.5f ? 1 : 0);
                    break;
            }
        }

        /// <summary>The swing of the fitting a door hangs off, or identity for one on the body. Looked
        /// up by slot rather than cached because a machine has at most a handful of fittings and a
        /// cache that went stale on a re-skin would hang a door off the wrong thing.</summary>
        private Quaternion ParentSwingOf(string parentSlot)
        {
            if (string.IsNullOrEmpty(parentSlot) || _doors == null) return Quaternion.identity;

            int at = _doors.IndexOfSlot(parentSlot);
            if (at < 0 || at >= _fitments.Length) return Quaternion.identity;

            VehicleFitment parent = _fitments[at];
            if (parent.Motion != VehicleFitmentMotion.HingeRotation) return Quaternion.identity;

            Vector3 axis = parent.HingeAxis == VehicleHingeAxis.Lateral ? RigRight : RigUp;
            return Quaternion.AngleAxis(_doors.Openness(at) * parent.SweepDegrees, axis);
        }

        /// <summary>Show exactly one baked state of a part that is neither a rotation nor a
        /// translation. ⚠️ Exactly ONE: leaving two visible draws a telescoping leg at both lengths
        /// at once, which reads as a graphical fault rather than as a door.</summary>
        private void ShowState(int index, in VehicleFitment f, int state)
        {
            if (index >= _states.Length) return;
            IHullPropRenderer[] built = _states[index];
            if (built == null) return;

            for (int k = 0; k < built.Length; k++)
                if (built[k] != null) built[k].Visible = k == state;
        }

        /// <summary>The rig's own up axis (+z, out of the road). A positive rotation about it swings
        /// the nose toward −x, which is the LEFT lock the rig's <c>steer</c> sign means.</summary>
        private static readonly Vector3 RigUp = Vector3.forward;

        /// <summary>The rig's own lateral axis (+x, curb side) — every wheel's axle.</summary>
        private static readonly Vector3 RigRight = Vector3.right;
    }
}
