using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Vehicles
{
    /// <summary>
    /// <b>A tractor's fifth wheel at runtime</b> — what captures a trailer, what pulls her, and what
    /// lets her go.
    ///
    /// <para><b>Coupling is an act of DRIVING, not a button.</b> The art is explicit: the release
    /// handle is a street-side lever, and <i>"the coupling itself is backing under the nose"</i>. So
    /// there is no couple handle out in the yard — you line the truck up and reverse, the pin rides
    /// up the ramps into the slot, and the offer appears. Only letting her go is worked.</para>
    ///
    /// <para><b>Nothing here is tuned.</b> The capture window is the slot's own throat and reach, the
    /// heading tolerance is that slot's aspect, the fold limit is the trailer's nose swing against
    /// this cab's clearance, and the follow is solved on the kingpin-to-axle length her sidecar
    /// publishes. See <see cref="VehicleCouplingMath"/> — the arithmetic is there, pure and
    /// testable, and this component is the part that knows about GameObjects.</para>
    ///
    /// <para>⚠️ <b>Cross-module through Core only</b> (rule 4): this names <see cref="TowedBody"/>
    /// and <see cref="VehicleDoors"/>, both its own module's, and Core's coupling maths and interact
    /// seam. Nothing else.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleHitch : MonoBehaviour, IInteractable
    {
        [Tooltip("How close (m) the player must stand to the street-side release to work it.")]
        [SerializeField, Min(0f)] private float _reachMeters = 1.5f;

        private VehicleMeshDef _mesh;
        private VehicleController _controller;
        private VehicleDoors _doors;
        private string _vehicleId = "";

        private TowedBody _trailer;
        private float _lastOdometer;

        /// <summary>The trailer on the plate, or null.</summary>
        public TowedBody Trailer => _trailer;
        public bool IsCoupled => _trailer != null;

        public VehicleFifthWheel FifthWheel => _mesh != null ? _mesh.FifthWheel : default;

        /// <summary>Wire the hitch. Called by the skinner for any machine whose def publishes a
        /// plate; a machine that does not tow never gets one.</summary>
        public void Configure(VehicleMeshDef mesh, VehicleController controller, string vehicleId)
        {
            _mesh = mesh;
            _controller = controller;
            _doors = GetComponent<VehicleDoors>();
            _vehicleId = vehicleId ?? "";
            _lastOdometer = controller != null ? controller.OdometerMeters : 0f;
        }

        /// <summary>Her heading in the world, the same bearing the mesh driver poses from — so the
        /// trailer folds against the heading the player can see, not a second copy of it.</summary>
        public float HeadingDegrees => BoatKinematics.BearingDegrees(transform.up);

        /// <summary>Where the kingpin seats, in the world. The plate travels with her and swings as
        /// she turns, so it is computed live rather than sampled at install.</summary>
        public Vector2 CouplingPointWorld
        {
            get
            {
                VehicleFifthWheel wheel = FifthWheel;
                Vector3 local = new Vector3(wheel.CouplingPointLocal.x, wheel.CouplingPointLocal.y, 0f);
                Vector3 world = transform.TransformPoint(local);
                return new Vector2(world.x, world.y);
            }
        }

        /// <summary>How far the pair may fold, degrees — the trailer's nose swing against THIS cab's
        /// clearance. Solved per pair, so a longer-nosed trailer on a shorter cab would tighten it
        /// without anybody editing a number.</summary>
        public float JackknifeCapDegrees =>
            _trailer != null
                ? VehicleCouplingMath.JackknifeCapDegrees(_trailer.Kingpin, FifthWheel)
                : 0f;

        /// <summary>
        /// The uncoupled trailer whose pin is in the slot right now, or null — the capture test,
        /// asked of every towed body in the world.
        ///
        /// <para>Cheap by construction: a handful of trailers, a transform-point each, and no
        /// allocation (rule 7). The registry exists so this is not a scene search.</para>
        /// </summary>
        public TowedBody CapturedTrailer()
        {
            VehicleFifthWheel wheel = FifthWheel;
            if (!wheel.Published) return null;

            float heading = HeadingDegrees;
            var all = TowedBody.All;
            for (int i = 0; i < all.Count; i++)
            {
                TowedBody body = all[i];
                if (body == null || body.IsCoupled || !body.Kingpin.Published) continue;

                // The pin, in THIS tractor's frame — which is the frame the slot is drawn in.
                Vector2 pinWorld = body.KingpinWorld;
                Vector3 local = transform.InverseTransformPoint(new Vector3(pinWorld.x, pinWorld.y, 0f));

                if (VehicleCouplingMath.IsCaptured(wheel, body.Kingpin,
                                                   new Vector2(local.x, local.y),
                                                   Mathf.DeltaAngle(heading, body.HeadingDegrees)))
                    return body;
            }
            return null;
        }

        /// <summary>
        /// ⭐ <b>Take the trailer</b>, and wind her legs up — the kit's own discipline, in one place:
        /// <i>"couple → gear 0 BEFORE rolling; nothing in the rig stops a game dragging grounded
        /// shoes, and it will render exactly that."</i>
        ///
        /// <para>The legs are SENT up rather than snapped: the crank takes its published time, so a
        /// driver who couples and floors it does drag her shoes for a moment, which is the honest
        /// picture and is what the sidecar warns about.</para>
        /// </summary>
        public bool Couple(TowedBody body)
        {
            if (body == null || IsCoupled || body.IsCoupled) return false;
            if (!FifthWheel.Published || !body.Kingpin.Published) return false;

            _trailer = body;
            body.CoupledTo = this;
            _lastOdometer = _controller != null ? _controller.OdometerMeters : 0f;

            // Seat her: place the trailer so her pin is exactly on the plate, keeping her heading.
            body.FollowKingpin(CouplingPointWorld, HeadingDegrees, 0f, JackknifeCapDegrees);

            // ⚠️ An explicit null check, never `?.` — Unity's fake-null makes the null-conditional
            // operator lie about a destroyed component.
            var doors = body.GetComponent<VehicleDoors>();
            if (doors != null) doors.SetGroupTarget("gear", 1f);
            return true;
        }

        /// <summary>
        /// ⚠️ <b>Let her go — but not onto her belly.</b> A trailer whose gear is up is held off the
        /// ground by the pin alone, so releasing there drops her nose into the yard. The refusal is a
        /// FACT about her legs rather than a lock: wind them down and it clears.
        /// </summary>
        public bool TryUncouple(out string refusal)
        {
            refusal = null;
            if (!IsCoupled) { refusal = "Nothing on the plate."; return false; }

            if (!_trailer.LegsAreDown)
            {
                refusal = "Her legs are still up — wind them down before you pull the pin.";
                return false;
            }

            _trailer.CoupledTo = null;
            _trailer = null;
            return true;
        }

        /// <summary>Drop a trailer that has left the world. Not a release: nothing is set down,
        /// because there is no longer anything to set down.</summary>
        internal void ForgetTrailer(TowedBody body)
        {
            if (_trailer == body) _trailer = null;
        }

        private void LateUpdate() => Step();

        /// <summary>One follow step — public so an EditMode test drives the production path without
        /// a player loop.</summary>
        public void Step()
        {
            if (_trailer == null || _controller == null) return;

            float odometer = _controller.OdometerMeters;
            float travelled = odometer - _lastOdometer;
            _lastOdometer = odometer;

            // ⚠️ SIGNED, and that is the whole of reversing. The odometer accumulates speed × dt, so
            // backing gives a negative delta and the trailer folds the other way — which is what
            // makes backing one hard and is the reason a driver lines up before reversing.
            _trailer.FollowKingpin(CouplingPointWorld, HeadingDegrees, travelled, JackknifeCapDegrees);
        }

        // ---- the release handle ------------------------------------------------------------------

        /// <summary>⚠️ Computed live, never latched — the #556 trap: AddComponent on a live object
        /// registers before the caller has said which vehicle this is.</summary>
        public string Id =>
            string.IsNullOrEmpty(_vehicleId)
                ? $"vehicle.unassigned.hitch#{GetEntityId()}"
                : $"{_vehicleId}.hitch#{GetEntityId()}";

        public string VerbLabel => IsCoupled ? "Pull the release" : "Couple the trailer";

        public Vector2 WorldPosition
        {
            get
            {
                VehicleFifthWheel wheel = FifthWheel;
                Vector3 world = transform.TransformPoint(
                    new Vector3(wheel.ReleaseHandleLocal.x, wheel.ReleaseHandleLocal.y, 0f));
                return new Vector2(world.x, world.y);
            }
        }

        public float ReachMeters => _reachMeters;
        public InteractContext Contexts => InteractContext.OnFoot;
        public bool RequiresFacing => false;
        public int Priority => InteractPriority.Fixture;

        /// <summary>Offered when there is something to do: a pin in the slot to take, or a trailer on
        /// the plate to let go. Standing at the handle of a bobtail tractor with no trailer behind
        /// her offers nothing, which is correct.</summary>
        public bool IsAvailable =>
            FifthWheel.Published && (IsCoupled || CapturedTrailer() != null);

        public void Interact(in InteractActor actor)
        {
            if (IsCoupled) { TryUncouple(out _); return; }

            TowedBody captured = CapturedTrailer();
            if (captured != null) Couple(captured);
        }

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);
    }
}
