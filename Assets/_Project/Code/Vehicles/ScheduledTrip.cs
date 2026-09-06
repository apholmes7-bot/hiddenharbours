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

        /// <summary>Wire the whole thing up in one call — the region builder's path, and the tests'.</summary>
        public void Configure(VehicleTripDef trip, ParkedVehicle machine, Transform driver,
                              Vector2[] outbound, Vector2[] returnLeg,
                              Vector2 originPost, Vector2 originPostFacing,
                              Vector2 destinationPost, Vector2 destinationPostFacing,
                              SpriteRenderer driverRenderer = null, Behaviour driverTalkable = null)
        {
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
                var spec = new VehicleTripSpec(_outbound, _return, _originPost, _originPostFacing,
                                               _destinationPost, _destinationPostFacing, doorLocal,
                                               _trip.OutboundDepartureHour, _trip.ReturnDepartureHour,
                                               _trip.CruiseMetresPerSecond, _trip.WalkMetresPerSecond);
                _plan = VehicleTripPlan.Build(spec, _plannedSecondsPerGameHour, out problem);
            }

            if (_plan != null || _reported) return;

            _reported = true;
            Debug.LogWarning(
                $"[ScheduledTrip] {name} is standing still: {problem}. She keeps the spot the builder " +
                "placed her on, which is what a truck without a trip already does — fix the content " +
                "rather than the placement.", this);
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
