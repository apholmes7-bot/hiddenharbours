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

        // The capture announcement, kept apart from the follow's odometer mark because the two
        // measure different journeys: the follow steps every frame she is coupled, this polls only
        // while she is NOT, and sharing one mark would have each eat the other's distance.
        private float _lastCaptureOdometer;
        private bool _hasPolled;
        private string _announcedTrailerMeshId;
        private bool _announcedCapture;

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

            // A re-skin must not leave a controller carrying a load the hitch no longer has. The
            // trailer, if there is one, re-announces herself on the next Couple.
            if (controller != null && _trailer == null) controller.SetTow(default);
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
        ///
        /// <para>⚠️ A trailer another poser <see cref="TowedBody.IsHeld"/> is skipped as firmly as one
        /// already on a pin: a scheduled run under way owns her transform, and offering the player her
        /// pin would give two clocks one body.</para>
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
                if (body == null || body.IsCoupled || body.IsHeld || !body.Kingpin.Published) continue;

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

            // ⭐ The drive model learns she is loaded. Not a lookup the other way: what it needs is a
            // LOAD and a length, and the hitch is the one thing that knows a pin went in.
            if (_controller != null) _controller.SetTow(body.Kingpin);

            // Seat her: place the trailer so her pin is exactly on the plate, keeping her heading.
            body.FollowKingpin(CouplingPointWorld, HeadingDegrees, 0f, JackknifeCapDegrees);

            // ⚠️ An explicit null check, never `?.` — Unity's fake-null makes the null-conditional
            // operator lie about a destroyed component.
            var doors = body.GetComponent<VehicleDoors>();
            if (doors != null) doors.SetGroupTarget("gear", 1f);

            // The offer is SPENT: she is on the pin, so "get out and couple her" has to stop being
            // said. PollCapture returns early once coupled and would otherwise leave the last
            // announcement standing for as long as the pair is together.
            Announce(null);
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
            _hasPolled = false;   // off the pin and still in the slot — look again at once.
            _trailer = null;
            if (_controller != null) _controller.SetTow(default);
            return true;
        }

        /// <summary>Drop a trailer that has left the world. Not a release: nothing is set down,
        /// because there is no longer anything to set down.</summary>
        internal void ForgetTrailer(TowedBody body)
        {
            if (_trailer != body) return;
            _trailer = null;
            if (_controller != null) _controller.SetTow(default);
        }

        private void LateUpdate() => Step();

        /// <summary>One follow step — public so an EditMode test drives the production path without
        /// a player loop.</summary>
        public void Step()
        {
            // ⭐ Before the follow's early return, because this is the half that matters while she
            // is NOT on the pin — and the follow returns immediately in exactly that case. With no
            // controller there is no odometer and therefore no tick, which is the same nothing the
            // follow does below.
            PollCapture(_controller != null ? _controller.OdometerMeters : _lastCaptureOdometer);

            if (_trailer == null || _controller == null) return;

            float odometer = _controller.OdometerMeters;
            float travelled = odometer - _lastOdometer;
            _lastOdometer = odometer;

            // ⚠️ SIGNED, and that is the whole of reversing. The odometer accumulates speed × dt, so
            // backing gives a negative delta and the trailer folds the other way — which is what
            // makes backing one hard and is the reason a driver lines up before reversing.
            _trailer.FollowKingpin(CouplingPointWorld, HeadingDegrees, travelled, JackknifeCapDegrees);
        }

        /// <summary>
        /// ⭐⭐ <b>Tell the driver the pin went in</b> — the thing whose absence made coupling feel
        /// impossible.
        ///
        /// <para>Coupling is an act of BACKING, and the only signal it had ever produced was the verb
        /// on the release handle — which is an ON-FOOT interactable. So the loop the owner actually
        /// played was: reverse, get out, walk to the handle, read nothing, get back in, guess. The
        /// capture test was right and silent, which from the seat is indistinguishable from broken.
        /// This publishes <see cref="TrailerCaptureChanged"/> the moment the answer changes.</para>
        ///
        /// <para><b>THE TICK IS THE ODOMETER, and the step is the JAW'S OWN HALF-WIDTH</b>
        /// (<c>SlotHalfWidthMeters</c>, 0.06 m). Not a frame count and not a timer (rule 7): what can
        /// change the answer is the tractor MOVING under the pin, so distance is the honest clock —
        /// a truck stopped at the wrong angle polls nothing at all, however long she sits. And the
        /// step is art-published rather than picked (rule 6): the narrowest feature on the plate is
        /// the slot's half-width, so a truck that has moved less than that cannot have changed
        /// whether a pin is in it in any way a driver could see. It is ~11 polls across the window's
        /// own 0.71 m depth, and it moves on its own the day the art redraws the slot.</para>
        ///
        /// <para>⚠️ <b>The known gap, stated rather than papered over:</b> a driver who enters the
        /// window and stops within one step of its aft edge is not told until he nudges again — a
        /// 0.06 m sliver of a 0.71 m window. Closing it would mean polling a stationary truck every
        /// frame, which is the rule-7 cost this gate exists to refuse.</para>
        ///
        /// <para><b>Costs one bool on everything that is not a semi.</b> <c>FifthWheel.Published</c> is
        /// false on every machine in the pack but the two tractors, and a coupled plate cannot capture
        /// anything — so the scan of <see cref="TowedBody.All"/> only ever runs on an uncoupled
        /// tractor that has just moved.</para>
        /// </summary>
        /// <param name="odometerMeters">how far she has driven, all told. Taken rather than read
        /// for the same reason <see cref="TowedBody.FollowKingpin"/> takes its distance: the
        /// caller owns the clock, and an EditMode test then drives the production announcement
        /// through the production path with no player loop and no physics tick behind it.</param>
        public void PollCapture(float odometerMeters)
        {
            if (IsCoupled) return;

            VehicleFifthWheel wheel = FifthWheel;
            if (!wheel.Published) return;

            // ⚠️ The FIRST look always happens. Bay 0 at the laydown ships a pair ALREADY captured,
            // and a gate measured from a standing start would keep that silent until the driver had
            // moved 6 cm — which is the one pair the owner was told to go and try.
            if (_hasPolled &&
                Mathf.Abs(odometerMeters - _lastCaptureOdometer) < wheel.SlotHalfWidthMeters) return;
            _hasPolled = true;
            _lastCaptureOdometer = odometerMeters;

            Announce(CapturedTrailer());
        }

        /// <summary>
        /// Publish the capture state, and ONLY when it has changed — the same change-detection every
        /// other signal in this game keeps, so a truck reversing through the window does not fill the
        /// bus with the same sentence eleven times.
        ///
        /// <para>⚠️ A towed body has no identity of her own, so what is carried is her MESH id. That is
        /// enough to tell a reefer from a flatbed and is not enough to tell one reefer from another —
        /// which is why the id is not what the change is detected on. The BOOL is.</para>
        /// </summary>
        private void Announce(TowedBody captured)
        {
            bool has = captured != null;
            string meshId = has && captured.Mesh != null ? captured.Mesh.Id : null;
            if (has == _announcedCapture &&
                string.Equals(meshId, _announcedTrailerMeshId, System.StringComparison.Ordinal)) return;

            _announcedCapture = has;
            _announcedTrailerMeshId = meshId;
            EventBus.Publish(new TrailerCaptureChanged(_vehicleId, meshId, has));
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

        /// <summary>⚠️ Withdraw the announcement as well as the registration. A region unloaded
        /// under a standing offer would otherwise leave the line on screen with nothing behind
        /// it — the mirror of a signal nobody hears: a listener still
        /// hearing a publisher that has gone.</summary>
        private void OnDisable()
        {
            Interactables.Unregister(this);
            Announce(null);
        }
    }
}
