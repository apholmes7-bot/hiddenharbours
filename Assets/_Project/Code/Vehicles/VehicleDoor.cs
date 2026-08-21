using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Vehicles
{
    /// <summary>
    /// <b>The driver's door — how a player gets into a truck</b> (ADR 0035, and the ruled path in its
    /// Consequences section).
    ///
    /// <para><b>It is a registration, not a key.</b> The dev-key ledger is spent A–Z, and #503 shipped the
    /// pressure valve for exactly this: a feature that wants a button registers an
    /// <see cref="IInteractable"/> and receives the interact press contextually. So getting into a truck is
    /// the SAME verb that boards a boat at a cleat and works a wet bucket, resolved by
    /// <see cref="InteractResolver"/> on distance, priority and facing — with no new binding, no private
    /// <c>Update()</c> reading the keyboard, and no hand-asserted "the ranges don't overlap" invariant.</para>
    ///
    /// <para><b>And it is the seat</b> (<see cref="IDriveSeat"/>), because the two are one thing from the
    /// player's side: the door you work is the door you are put back out of. Implementing both here means
    /// the reach point that decides whether you may get in is, by construction, the same point you are set
    /// down on — one field, read live, so entry and exit cannot drift apart the way two copies of a
    /// measured number always eventually do.</para>
    ///
    /// <para><b>Where the point comes from.</b> <c>VehicleMeshDef.DriveDoorLocal</c> — measured art off the
    /// rig's own sidecar, derived by the art side to stand outside the door leaf's swept disc (rule 6: it
    /// is not a literal here and it is not the owner's to tune). It is in RIG metres and is transformed
    /// through her live root each time it is read, so it travels with her and swings as she turns.</para>
    ///
    /// <para><b>Cross-module through Core only</b> (rule 4). This component may not name
    /// <c>ControlSwitcher</c> — Vehicles references Core and nothing else — so working the door publishes
    /// <see cref="DriveSeatRequested"/> and stops caring. With nothing subscribed (an art scene, an
    /// EditMode fixture) that is a no-op rather than a null reference.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleDoor : MonoBehaviour, IInteractable, IDriveSeat
    {
        [Tooltip("How close (m) the player must stand to work the door. Forgiving enough not to hunt for " +
                 "a spot (P5), tight enough that you cannot open the driver's door from the curb side — " +
                 "the two door points are 3.5 m apart, so anything past ~1.7 m would reach through her.")]
        [SerializeField, Min(0f)] private float _reachMeters = 1.5f;

        [Tooltip("Require the player to be FACING the door. Off by default — the established answer for " +
                 "something you operate by standing at it (the wet bucket's precedent), and the forgiving " +
                 "one for a spot you have already had to walk to the correct SIDE of.")]
        [SerializeField] private bool _requiresFacing;

        private VehicleController _controller;
        private ParkedVehicle _parked;
        private string _id;

        // ---- what she is -------------------------------------------------------------------------

        /// <summary>The def she carries, from her controller if she has one, else from the parked marker
        /// (which holds it before ever being skinned). Resolved live so the authoring ORDER cannot matter —
        /// see <see cref="ParkedVehicle.Configure"/> for the AddComponent-runs-OnEnable-first trap this is
        /// guarding against, which took out all five PlayMode fixtures once already.</summary>
        private VehicleDef Vehicle
        {
            get
            {
                if (_controller == null) _controller = GetComponent<VehicleController>();
                if (_controller != null && _controller.Vehicle != null) return _controller.Vehicle;
                if (_parked == null) _parked = GetComponent<ParkedVehicle>();
                return _parked != null ? _parked.Vehicle : null;
            }
        }

        private VehicleMeshDef Mesh
        {
            get { VehicleDef v = Vehicle; return v != null ? v.Mesh : null; }
        }

        // ---- the drive seat (Core seam) ----------------------------------------------------------

        /// <inheritdoc/>
        public string VehicleId
        {
            get { VehicleDef v = Vehicle; return v != null ? v.Id : null; }
        }

        /// <inheritdoc/>
        public bool IsDrivable
        {
            get
            {
                VehicleDef v = Vehicle;
                if (v == null || !v.IsUsable()) return false;
                if (_controller == null) _controller = GetComponent<VehicleController>();
                return _controller != null && HasDoor;
            }
        }

        /// <inheritdoc/>
        public Transform Root => transform;

        /// <inheritdoc/>
        public float CameraWorldHeightMeters
        {
            get { VehicleDef v = Vehicle; return v != null ? v.CameraWorldHeightMeters : 0f; }
        }

        /// <summary>Does this machine publish a driver's door at all? <c>(0,0)</c> is the sentinel for "not
        /// published" — a door at her own origin would be inside the cab, so it can never be a real
        /// value — and it is what a def baked before the field existed deserializes to. Refusing here is
        /// what stops such a def putting the player in the middle of the truck.</summary>
        private bool HasDoor
        {
            get { VehicleMeshDef m = Mesh; return m != null && m.DriveDoorLocal != Vector2.zero; }
        }

        /// <inheritdoc/>
        public Vector2 DoorWorldPosition
        {
            get
            {
                VehicleMeshDef m = Mesh;
                if (m == null) return transform.position;
                Vector2 local = m.DriveDoorLocal;
                // Through the ROOT: it carries her true heading (transform.up is the nose, the fleet's one
                // convention). Never the visual child — the mesh driver stomps that back to screen-identity
                // every LateUpdate, so a point read off it would not swing with her at all.
                Vector3 world = transform.TransformPoint(new Vector3(local.x, local.y, 0f));
                return new Vector2(world.x, world.y);
            }
        }

        /// <inheritdoc/>
        public bool ShowsDriver
        {
            get { VehicleMeshDef m = Mesh; return m != null && m.ShowsDriver; }
        }

        /// <inheritdoc/>
        public Vector2 DriverSeatWorldPosition
        {
            get
            {
                VehicleMeshDef m = Mesh;
                if (m == null) return transform.position;
                Vector3 seat = m.DriverSeatLocal;
                // Through the ROOT, and with the HEIGHT dropped, for the same two reasons the door is:
                // the root carries her true heading (transform.up is the nose), and the visual child is
                // stomped back to screen-identity every LateUpdate so a point read off it would not swing
                // with her at all. The z that goes into TransformPoint is deliberately 0 — the seat's
                // HEIGHT is not a place on the ground and must never be turned by her heading.
                Vector3 world = transform.TransformPoint(new Vector3(seat.x, seat.y, 0f));
                return new Vector2(world.x, world.y);
            }
        }

        /// <inheritdoc/>
        public float DriverSeatHeightMeters
        {
            get { VehicleMeshDef m = Mesh; return m != null ? m.DriverSeatLocal.z : 0f; }
        }

        /// <inheritdoc/>
        public bool IsAlive => this != null;   // MonoBehaviour's Unity-aware ==, which the interface's is not

        /// <inheritdoc/>
        public void TakeControls()
        {
            if (_controller == null) _controller = GetComponent<VehicleController>();
            if (_controller == null) return;
            _controller.enabled = true;
            _controller.Throttle = 0f;
            _controller.SteerDemand = 0f;
            _controller.Brake = false;
        }

        /// <inheritdoc/>
        public void SetDriveInput(float throttle, float steer, bool brake)
        {
            if (_controller == null) _controller = GetComponent<VehicleController>();
            if (_controller == null) return;
            _controller.Throttle = Mathf.Clamp(throttle, -1f, 1f);
            _controller.SteerDemand = Mathf.Clamp(steer, -1f, 1f);
            _controller.Brake = brake;
        }

        /// <inheritdoc/>
        public void ReleaseControls()
        {
            if (_controller == null) _controller = GetComponent<VehicleController>();
            if (_controller == null) return;
            _controller.Throttle = 0f;
            _controller.SteerDemand = 0f;
            // Left ON, as a handbrake. Throttle alone would leave her coasting the length of the park at
            // the def's small coast rate — a truck the player stepped out of should be where they left her.
            _controller.Brake = true;
        }

        // ---- the interact seam -------------------------------------------------------------------

        /// <summary>
        /// Stable id, unique among live registrants — which for a vehicle means <b>per instance</b>, not
        /// per model. The truck park is three bays wide and the village has more than one truck, so an id
        /// derived from the def alone would collide the moment two Duallys stood in it, and
        /// <see cref="InteractResolver"/>'s last tie-break would stop being total. It is also what the
        /// diegetic highlight needs: it must light THAT truck, not every truck of her model.
        /// </summary>
        public string Id
        {
            get
            {
                if (_id != null) return _id;
                string vehicle = VehicleId;
                // NOT latched until she knows what she is. AddComponent on a live object runs OnEnable
                // before the caller has said which vehicle this is (the #556 trap), so an id cached on
                // that first access would name her "unassigned" for the rest of her life.
                //
                // GetEntityId, not the deprecated GetInstanceID — 6000.5 marks that obsolete-as-ERROR,
                // which is a compile failure and not a warning. Same '#' separator the carriables and the
                // clam holes use for their per-instance ids.
                if (string.IsNullOrEmpty(vehicle)) return $"vehicle.unassigned.drive_door#{GetEntityId()}";
                return _id = $"{vehicle}.drive_door#{GetEntityId()}";
            }
        }

        /// <inheritdoc/>
        public Vector2 WorldPosition => DoorWorldPosition;

        /// <inheritdoc/>
        public float ReachMeters => _reachMeters;

        /// <summary>A truck is a thing bolted to the world that you walk up to and work — the default rung.
        /// She deliberately does NOT outrank what is in your hands: standing at her door holding a shovel,
        /// the press puts the shovel down, and the second press gets you in. That is the ladder working as
        /// designed (specificity wins), not a bug to route around.</summary>
        public int Priority => InteractPriority.Fixture;

        /// <inheritdoc/>
        public InteractContext Contexts => InteractContext.OnFoot;

        /// <inheritdoc/>
        public bool RequiresFacing => _requiresFacing;

        /// <summary>Available only when there is really something to get into: a usable def, a controller,
        /// and a published door. Scenery — a truck parked as dressing, which is what most trucks in a
        /// village should be — is not available, so it is neither resolved nor highlighted and the press
        /// falls through to whatever else is nearby.</summary>
        public bool IsAvailable => IsDrivable;

        /// <summary>What you do at a door you can open. Scenery never gets here — it is not
        /// <see cref="IsAvailable"/>, so the popup never offers a truck that is only dressing.</summary>
        public string VerbLabel => "Climb in";

        /// <summary>Ask for the wheel. The switcher re-reads its own gates before honouring it, so this
        /// never itself puts anyone behind one.</summary>
        public void Interact(in InteractActor actor) => EventBus.Publish(new DriveSeatRequested(this));

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);
    }
}
