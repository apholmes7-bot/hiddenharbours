using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Vehicles
{
    /// <summary>
    /// ⭐ <b>A VILLAGER WALKS TO A TRUCK, GETS IN, DRIVES A ROUTE, PARKS AND GETS OUT</b> — the owner's
    /// 2026-09-04 ask, run entirely off the clock.
    ///
    /// <para>The component holds the derived geometry (serialized by the region builder, the way
    /// <c>RoutineStations</c> holds a village's places), builds a <see cref="VehicleTripPlan"/> once, and
    /// then does nothing per frame but READ it: <c>SampleAt(hour)</c> answers where the machine is, where
    /// her driver is, and whether he is in the cab, and this pushes those onto two transforms.</para>
    ///
    /// <para><b>Nothing is ticked, integrated or saved</b> (rule 5). Join a session at 06:12 and the truck
    /// is on the road where 06:12 says she is; save mid-trip and the load re-derives her from the clock
    /// with no trip state in the file at all. See <see cref="VehicleTripPlan"/> for why a pose plan rather
    /// than a live driver, and for the live driver that still exists beside it.</para>
    ///
    /// <para><b>The seat is CLAIMED for the whole trip, not just the driving.</b> From the moment her
    /// driver sets off for the door to the moment he is back on his feet at the far end, the wheel is his
    /// (<see cref="DriveSeats"/>) — so the player standing at her door is not offered "Climb in" on a
    /// truck that is about to pull away, and the switcher refuses the press if one gets through. She goes
    /// back to being a truck anybody may drive the moment she is parked and empty.</para>
    ///
    /// <para>⭐⭐ <b>A RUN THAT TOWS COUPLES UP IN FRONT OF YOU.</b> Give the timetable a
    /// <c>TowedBodyId</c> and wire the trailer the region placed, and the plan grows two beats: her
    /// driver walks to the street-side release, the pin goes in and the legs wind up before he gets in
    /// the cab — and the reverse at the end of the day. The trailer's own pose comes off
    /// <c>Core.TowedFollowTrack</c>, which is <c>VehicleCouplingMath.FollowStep</c>: the same
    /// off-tracking the player's own tow draws, not a second model of it.</para>
    ///
    /// <para>⚠️ <b>A towing run can be REFUSED, and it says why.</b> A coupled pair cannot pivot in a
    /// bay, so both ends of the road have to be pull-throughs; and the trailer has to actually be on
    /// the plate when the driver reaches the handle. Every refusal is named in
    /// <see cref="LastRefusalReason"/> and logged once — a truck that quietly does not tow is
    /// indistinguishable from one nobody has authored yet.</para>
    ///
    /// <para>⚠️ <b>The road fleet carries NO colliders</b> (a carried defect from the driveable charter,
    /// its own PR). A scheduled truck therefore drives THROUGH a walker rather than round or into her.
    /// Nothing here makes that worse and nothing here fixes it.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScheduledTrip : MonoBehaviour
    {
        [Tooltip("The timetable: whose trip this is, when she leaves and how fast she goes. The " +
                 "GEOMETRY below is derived by the region builder; only the hours are the owner's.")]
        [SerializeField] private VehicleTripDef _trip;

        [Tooltip("The machine that makes the trip. Her own transform is what gets posed.")]
        [SerializeField] private ParkedVehicle _machine;

        [Tooltip("Her driver — the villager who walks to the door and is hidden while aboard. Optional: " +
                 "a trip with nobody named still runs, and is a truck that drives itself, which is " +
                 "visibly wrong rather than silently missing.")]
        [SerializeField] private Transform _driver;

        [Tooltip("The driver's renderer, hidden while he is in the cab. Found on the driver if unset.")]
        [SerializeField] private SpriteRenderer _driverRenderer;

        [Tooltip("Anything on the driver that the player can walk up to and talk to. Switched off with " +
                 "the renderer: a hidden villager who still answers is the #354 defect (she answers " +
                 "through her own wall) with a truck door in place of the wall.")]
        [SerializeField] private Behaviour _driverTalkable;

        [Tooltip("The towed body this run hauls — the one the REGION placed. Optional: a run with no " +
                 "TowedBodyId on its timetable ignores it, and a run that wants one and has none is " +
                 "refused by name rather than half-hung.\n\n" +
                 "⚠️ Her POSE at build time is read off her transform, not typed here: she is where the " +
                 "region stood her, and the plan checks that spot against the shipped capture test.")]
        [SerializeField] private ParkedTrailer _trailer;

        [Header("Derived by the region builder — never typed")]
        [Tooltip("Her road out. First point is her home bay, last is the far bay.")]
        [SerializeField] private Vector2[] _outbound;

        [Tooltip("Her road home. First point is the far bay, last is her home bay.")]
        [SerializeField] private Vector2[] _return;

        [Tooltip("Where her driver stands at the HOME end when he is not driving.")]
        [SerializeField] private Vector2 _originPost;

        [Tooltip("Which way he is turned there, as a world delta (not a bearing — the two conventions " +
                 "differ by up to 12.5°; see VehicleTripPose).")]
        [SerializeField] private Vector2 _originPostFacing = Vector2.down;

        [Tooltip("Where her driver stands at the FAR end — his stall, his counter, the thing he drove " +
                 "there to do.")]
        [SerializeField] private Vector2 _destinationPost;

        [Tooltip("Which way he is turned there, as a world delta.")]
        [SerializeField] private Vector2 _destinationPostFacing = Vector2.down;

        private VehicleTripPlan _plan;
        private bool _planned;
        private bool _reported;
        private bool _trailerReported;
        private bool _holdsTrailer;
        private float _plannedSecondsPerGameHour;
        private IDriveSeat _seat;
        private bool _holdsSeat;
        private bool _driverHidden;

        /// <summary>The timetable she is running, once it has been built. Null until the services are up
        /// (or for good, if the content is unusable — see the warning <see cref="TryPlan"/> logs).</summary>
        public VehicleTripPlan Plan => _plan;

        /// <summary>What the clock said last frame. Exposed for a PlayMode journey that wants to pin the
        /// trip at three clock samples without re-deriving the plan itself.</summary>
        public VehicleTripPose Pose { get; private set; }

        /// <summary>Which trip asset she runs.</summary>
        public VehicleTripDef Trip => _trip;

        /// <summary>The towed body this run hauls, as the region wired her. Null on a solo run.</summary>
        public ParkedTrailer Trailer => _trailer;

        /// <summary>
        /// ⭐ <b>Why she is not towing</b>, in words, or null when there is nothing to explain.
        ///
        /// <para>Set for a run whose timetable asks for a trailer and did not get one: no body wired,
        /// the wrong body wired, an unbaked pin — or geometry a coupled pair could not work, in which
        /// case this carries <c>VehicleTripPlan.Build</c>'s own measured refusal. Read by the content
        /// tests, so a region that quietly stops towing reddens instead of going unnoticed (#767's
        /// pattern).</para>
        /// </summary>
        public string LastRefusalReason { get; private set; }

        /// <summary>Wire the whole thing up in one call — the region builder's path, and the tests'.</summary>
        public void Configure(VehicleTripDef trip, ParkedVehicle machine, Transform driver,
                              Vector2[] outbound, Vector2[] returnLeg,
                              Vector2 originPost, Vector2 originPostFacing,
                              Vector2 destinationPost, Vector2 destinationPostFacing,
                              SpriteRenderer driverRenderer = null, Behaviour driverTalkable = null,
                              ParkedTrailer trailer = null)
        {
            _trailer = trailer;
            _trip = trip;
            _machine = machine;
            _driver = driver;
            _outbound = outbound;
            _return = returnLeg;
            _originPost = originPost;
            _originPostFacing = originPostFacing;
            _destinationPost = destinationPost;
            _destinationPostFacing = destinationPostFacing;
            _driverRenderer = driverRenderer;
            _driverTalkable = driverTalkable;
            _planned = false;
            _reported = false;
            _trailerReported = false;
        }

        private void OnEnable()
        {
            // Re-plan on every enable rather than caching across one: a region that unloads and comes back
            // may come back with a different day length, and a stale plan would keep the wrong six
            // minutes for ever. The build is one allocation and happens once per activation.
            _planned = false;
            _reported = false;
        }

        private void OnDisable()
        {
            ReleaseSeat();
            ReleaseTrailer();
            ShowDriver();       // never leave a villager hidden — an invisible, un-talkable person reads
                                // as broken dialogue, not as somebody who is out (VillagerRoutine's rule)
        }

        private void Update()
        {
            IGameClock clock = GameServices.Clock;
            if (clock == null) return;

            if (!_planned) TryPlan();
            if (_plan == null) return;

            // The owner's day-length knob is what every derived hour was computed against, and it can
            // change under a live region (a new game with a different config). One float compare a frame
            // is the whole cost of not having to think about it again.
            if (!Mathf.Approximately(SecondsPerGameHour(), _plannedSecondsPerGameHour)) { _planned = false; return; }

            VehicleTripPose pose = _plan.SampleAt(clock.HourOfDay);
            Pose = pose;

            ApplyMachine(pose);
            ApplyDriver(pose);
            ApplySeat(pose);
            ApplyTrailer(pose);
        }

        // ---- the three things a sample turns into ----------------------------------------------------

        /// <summary>Pose the machine. Her ROOT carries her heading — <c>transform.up</c> is the nose, the
        /// fleet's one convention — and the mesh driver reads it back to pick her picture, so setting
        /// <c>up</c> is the whole of "point her that way". Z is left alone: the Y-sort owns draw order.</summary>
        private void ApplyMachine(in VehicleTripPose pose)
        {
            Transform root = _machine != null ? _machine.transform : transform;
            Vector3 p = root.position;
            root.position = new Vector3(pose.MachinePosition.x, pose.MachinePosition.y, p.z);
            if (pose.MachineDirection != Vector2.zero) root.up = pose.MachineDirection;

            // ⚠️ Her controller is left alone deliberately. It integrates a demand nobody is giving
            // (throttle 0, speed 0) and writes a zero velocity onto her rigidbody every fixed step, which
            // is exactly what a parked truck should be doing — and it stays available the instant a player
            // opens her door in a bay. Disabling it here would leave a machine the player climbs into
            // with no integrator until something re-enabled it.
        }

        /// <summary>Pose her driver — and hide him while he is in the cab. Hidden means the renderer AND
        /// whatever the player talks to: a villager who answers from inside a truck is the same defect as
        /// one who answers through her own wall.</summary>
        private void ApplyDriver(in VehicleTripPose pose)
        {
            if (_driver == null) return;

            if (pose.DriverAboard) { HideDriver(); return; }
            ShowDriver();

            Vector3 p = _driver.position;
            _driver.position = new Vector3(pose.DriverPosition.x, pose.DriverPosition.y, p.z);
        }

        /// <summary>
        /// ⭐ <b>Pose the trailer, and work her legs.</b>
        ///
        /// <para>Her heading goes through <c>TowedBody.HeadingDegrees</c> rather than onto the transform
        /// directly, because that setter is what keeps her field and her rotation in step — the fact
        /// the class exists to hold (a heading kept in two places is two headings).</para>
        ///
        /// <para><b>The legs are SENT, not set.</b> The crank the player turns is the same crank, at its
        /// own published speed, so a run that couples up drags her shoes for the moment the sidecar
        /// warns about rather than snapping them clear. One handle, one animation, one truth.</para>
        ///
        /// <para>⚠️ <b>A trailer somebody else has coupled is left alone</b>, and said so once. Two
        /// writers on one transform is the bug this whole file's seat claim exists to avoid; the truck
        /// keeps her day and runs the errand bobtail, which is visibly a haulier without her trailer
        /// rather than a village that stopped.</para>
        /// </summary>
        private void ApplyTrailer(in VehicleTripPose pose)
        {
            if (!pose.HasTrailer) return;

            TowedBody body = _trailer != null ? _trailer.Trailer : null;
            if (body == null) return;

            if (body.IsCoupled)
            {
                ReleaseTrailer();
                if (_trailerReported) return;
                _trailerReported = true;
                LastRefusalReason = "somebody else has her on a pin — this run goes bobtail today";
                Debug.LogWarning($"[ScheduledTrip] {name}: {LastRefusalReason}.", this);
                return;
            }

            // Claimed only while the plan has her pin in the slot: standing in her bay she is anybody's.
            if (pose.TrailerCoupled != _holdsTrailer)
                _holdsTrailer = pose.TrailerCoupled && body.TryHold(this);
            if (!pose.TrailerCoupled) body.Release(this);

            // ⚠️ A claim we did not get is a claim somebody else has, and posing her anyway would be the
            // two-writers-one-transform bug with an extra step. Only two trips authored onto one trailer
            // can reach this, which is a content error — so it is named rather than silently arbitrated.
            if (pose.TrailerCoupled && !_holdsTrailer)
            {
                if (_trailerReported) return;
                _trailerReported = true;
                LastRefusalReason = "another scheduled run is already posing her";
                Debug.LogWarning($"[ScheduledTrip] {name}: {LastRefusalReason} — two timetables cannot " +
                                 "haul one trailer. This run goes bobtail.", this);
                return;
            }

            Vector3 p = body.transform.position;
            body.transform.position = new Vector3(pose.TrailerPosition.x, pose.TrailerPosition.y, p.z);
            if (pose.TrailerDirection != Vector2.zero)
                body.HeadingDegrees = BoatKinematics.BearingDegrees(pose.TrailerDirection);

            // ⚠️ An explicit null check, never `?.` — Unity's fake-null makes the null-conditional
            // operator lie about a destroyed component.
            var doors = body.GetComponent<VehicleDoors>();
            if (doors != null) doors.SetGroupTarget("gear", pose.TrailerLegsUp);
        }

        private void ReleaseTrailer()
        {
            TowedBody body = _trailer != null ? _trailer.Trailer : null;
            if (body != null) body.Release(this);
            _holdsTrailer = false;
        }

        /// <summary>Hold her wheel for as long as her driver has it. The claim spans the walk to the door
        /// as well as the drive: a truck whose driver is three metres away and closing is not a truck to
        /// offer the player.</summary>
        private void ApplySeat(in VehicleTripPose pose)
        {
            bool wants = pose.Stage != VehicleTripStage.Resting;
            if (wants == _holdsSeat) return;

            if (wants) { if (Seat() != null) _holdsSeat = DriveSeats.TryClaim(_seat, this); }
            else ReleaseSeat();
        }

        private void ReleaseSeat()
        {
            DriveSeats.ReleaseAllFor(this);
            _holdsSeat = false;
        }

        private void HideDriver()
        {
            if (_driverHidden) return;
            _driverHidden = true;
            if (_driverRenderer != null) _driverRenderer.enabled = false;
            if (_driverTalkable != null) _driverTalkable.enabled = false;
        }

        private void ShowDriver()
        {
            if (!_driverHidden) return;
            _driverHidden = false;
            if (_driverRenderer != null) _driverRenderer.enabled = true;
            if (_driverTalkable != null) _driverTalkable.enabled = true;
        }

        // ---- becoming live ---------------------------------------------------------------------------

        private static float SecondsPerGameHour() => GameServices.SecondsPerDay / DaySchedule.HoursPerDay;

        /// <summary>
        /// Build the plan, once the services it needs are up. The DOOR comes off the machine's own mesh
        /// def — measured art, not a number anybody types — so the walk to the door lands where the door
        /// is rather than at the middle of the truck.
        /// </summary>
        private void TryPlan()
        {
            _planned = true;
            _plannedSecondsPerGameHour = SecondsPerGameHour();

            string problem = null;
            if (_trip == null) problem = "no trip asset";
            else if (!_trip.IsUsable()) problem = $"the trip asset '{_trip.Id}' is not usable — check " +
                                                  "its speeds and that its two hours differ";
            else if (_machine == null) problem = "no machine";
            else if (_machine.Vehicle == null) problem = "the machine carries no vehicle def";
            else if (_machine.Vehicle.Mesh == null) problem = "the machine's def has no mesh, so she has " +
                                                             "no door to walk to";

            if (problem == null)
            {
                Vector2 doorLocal = _machine.Vehicle.Mesh.DriveDoorLocal;

                VehicleTowedSpec towed = default;
                bool wants = _trip.Tows;
                bool tows = wants && TryTowedSpec(out towed);

                if (tows)
                {
                    var pair = new VehicleTripSpec(
                        _outbound, _return, _originPost, _originPostFacing, _destinationPost,
                        _destinationPostFacing, doorLocal, _trip.OutboundDepartureHour,
                        _trip.ReturnDepartureHour, _trip.CruiseMetresPerSecond,
                        _trip.WalkMetresPerSecond, towed);

                    _plan = VehicleTripPlan.Build(pair, _plannedSecondsPerGameHour, out string refused);
                    if (_plan == null) Refuse(refused);
                }

                // ⭐ A REFUSED PAIR IS NOT A REFUSED ERRAND. The geometry could not carry a trailer —
                // both ends of a towing road have to be pull-throughs — but the errand itself still can,
                // so she runs it bobtail unless the timetable says the load is the whole point. Both
                // readings are true of a real yard, which is why it is a field and not a rule (§Q2).
                if (_plan == null)
                {
                    if (wants && _trip.WhenTheTrailerIsNotThere == TrailerAbsence.StayHome)
                    {
                        problem = $"the load is the point of this run, and {LastRefusalReason}";
                    }
                    else
                    {
                        var solo = new VehicleTripSpec(
                            _outbound, _return, _originPost, _originPostFacing, _destinationPost,
                            _destinationPostFacing, doorLocal, _trip.OutboundDepartureHour,
                            _trip.ReturnDepartureHour, _trip.CruiseMetresPerSecond,
                            _trip.WalkMetresPerSecond);

                        _plan = VehicleTripPlan.Build(solo, _plannedSecondsPerGameHour, out problem);
                    }
                }
            }

            if (_plan != null || _reported) return;

            _reported = true;
            Debug.LogWarning(
                $"[ScheduledTrip] {name} is standing still: {problem}. She keeps the spot the builder " +
                "placed her on, which is what a truck without a trip already does — fix the content " +
                "rather than the placement.", this);
        }

        /// <summary>
        /// ⭐ <b>The towed half of the spec, off the two BAKED meshes and the trailer's own transform.</b>
        /// False — with <see cref="LastRefusalReason"/> set and logged — whenever the timetable asks for a
        /// trailer this machine cannot actually haul.
        ///
        /// <para>Nothing here is typed: the plate and the pin are the art's published numbers, and where
        /// she stands is where the region stood her. What IS checked is that the body wired into the
        /// scene is the body the timetable names — a region that quietly wires the wrong trailer would
        /// otherwise produce a run that works and hauls the wrong thing.</para>
        /// </summary>
        private bool TryTowedSpec(out VehicleTowedSpec towed)
        {
            towed = default;
            if (_trip == null || !_trip.Tows) return false;

            string problem = null;
            TowedBody body = _trailer != null ? _trailer.Trailer : null;

            if (_trailer == null) problem = $"the timetable hauls '{_trip.TowedBodyId}' and the region " +
                                            "wired no trailer at all";
            else if (_trailer.Body == null) problem = "the trailer the region wired carries no mesh";
            else if (_trailer.Body.Id != _trip.TowedBodyId)
                problem = $"the region wired '{_trailer.Body.Id}' and the timetable hauls " +
                          $"'{_trip.TowedBodyId}'";
            else if (body == null) problem = "the wired trailer has not skinned, so she has no pin";
            else if (!_machine.Vehicle.Mesh.CanTow)
                problem = $"{_machine.Vehicle.DisplayName} publishes no fifth wheel — she does not tow";
            else if (!body.Kingpin.Published)
                problem = $"'{_trailer.Body.Id}' publishes no kingpin — nothing to hook";

            if (problem != null) { Refuse(problem); return false; }

            towed = new VehicleTowedSpec(
                _machine.Vehicle.Mesh.FifthWheel, body.Kingpin,
                body.transform.position,
                BoatKinematics.BearingDegrees(body.transform.up));
            return true;
        }

        /// <summary>State a refusal once. A run that silently stops towing is the failure this exists to
        /// prevent, and a run that says so every frame is the other one.</summary>
        private void Refuse(string why)
        {
            LastRefusalReason = why;
            if (_trailerReported) return;
            _trailerReported = true;
            Debug.LogWarning($"[ScheduledTrip] {name} is not towing: {why}.", this);
        }

        /// <summary>Her drive seat, resolved live. Not cached in Awake: the skinner adds the door on the
        /// machine's first enable and a reference taken earlier would be null for her whole life (the
        /// #556 trap, which took out five fixtures at once).</summary>
        private IDriveSeat Seat()
        {
            if (_seat != null && _seat.IsAlive) return _seat;
            if (_machine == null) return null;
            _seat = _machine.Door;
            return _seat;
        }
    }
}
