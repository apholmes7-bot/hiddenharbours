using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Vehicles
{
    /// <summary>
    /// <b>A driver who follows a line of points</b> — the shipped implementation of
    /// <see cref="IDriveInputSource"/> that is not a keyboard.
    ///
    /// <para>Every frame it is asked, it re-derives a lookahead point on its route and hands back the
    /// demand <see cref="RouteFollowMath"/> says will get there: the same arithmetic, from the same file,
    /// that the PlayMode journey drives every machine in the fleet through. That sharing is the point of
    /// the class — before it, the only driver that could follow a road lived inside a test fixture, and
    /// "the game drives the way the test says it does" was an assertion nobody could make.</para>
    ///
    /// <para><b>It is not what the village's trucks use.</b> A scheduled trip poses its machine from the
    /// clock (<see cref="VehicleTripPlan"/> — pure, nothing saved, a save mid-trip re-derives). This is
    /// the LIVE path: the journey fixture, and whatever asks a real <see cref="VehicleController"/> to
    /// integrate its way somewhere — a player's cruise control, a machine being driven off a ferry. Both
    /// exist deliberately and they share their maths, so a change to how a machine follows a road cannot
    /// land in one and not the other.</para>
    ///
    /// <para><b>Stateless between frames except for the leg it is on.</b> <see cref="Read"/> is polled
    /// once per frame while a machine is being driven and answers the demand for that frame; the only
    /// thing carried across is which waypoint is next, because "have I passed this one" is a question
    /// about the leg and not about the frame.</para>
    /// </summary>
    public sealed class RouteDriver : IDriveInputSource
    {
        private readonly Transform _machine;
        private Vector2[] _route;
        private RouteFollowMath.RouteFollowTuning _tuning;

        private int _next;               // the waypoint being hunted
        private Vector2 _legFrom;        // where the current leg started — the perpendicular rule needs it
        private bool _followCentreLine;

        /// <summary>How many frames the driver has actually been asked. The anti-vacuous number: a test
        /// proving the seam carries a demand must also prove the seam was consulted.</summary>
        public int Reads { get; private set; }

        /// <summary>True once every waypoint has been passed — the leg is over and the caller should stop
        /// and hand the wheel back.</summary>
        public bool Arrived => _route == null || _next >= _route.Length;

        /// <summary>Which waypoint she is hunting (or <see cref="Arrived"/>).</summary>
        public int NextWaypoint => _next;

        public RouteDriver(Transform machine, VehicleDef vehicle)
        {
            _machine = machine;
            _tuning = TuningFor(vehicle);
        }

        /// <summary>The route-following feel this machine drives on — her own tunables, filled in from
        /// the measured driver wherever a def predates the fields (see
        /// <see cref="RouteFollowMath.RouteFollowTuning.Sane"/>).</summary>
        public static RouteFollowMath.RouteFollowTuning TuningFor(VehicleDef vehicle)
            => vehicle != null ? vehicle.RouteFollow.Sane() : RouteFollowMath.RouteFollowTuning.Measured;

        /// <summary>
        /// Put her on a route. <paramref name="followCentreLine"/> steers by a lookahead point on the
        /// whole line — what a road wants, because it makes her CONVERGE onto the carriageway — while
        /// false hunts the waypoints one at a time, which is what an open yard wants.
        /// </summary>
        public void Follow(Vector2[] route, bool followCentreLine = true)
        {
            _route = route;
            _followCentreLine = followCentreLine;
            _next = 0;
            _legFrom = _machine != null ? (Vector2)_machine.position : Vector2.zero;
        }

        /// <summary>Stop asking for anything — throttle shut, wheel released.</summary>
        public void Release() => _route = null;

        /// <inheritdoc/>
        public DriveDemand Read()
        {
            Reads++;
            if (_machine == null || _route == null || _route.Length == 0) return DriveDemand.None;

            Vector2 pos = _machine.position;

            // Retire every waypoint she has passed THIS frame, not just one: a fast machine on a tight
            // chicane can cross two perpendiculars between reads, and a driver that only ever retires one
            // would spend a frame steering back at a point behind her.
            while (_next < _route.Length &&
                   RouteFollowMath.HasReached(_legFrom, _route[_next], pos, _tuning.WaypointReachMetres))
            {
                _legFrom = _route[_next];
                _next++;
            }
            if (_next >= _route.Length) return DriveDemand.None;

            Vector2 target = _followCentreLine
                ? RouteFollowMath.LookaheadTarget(_route, 0, _route.Length, pos, _tuning.LookaheadMetres)
                : _route[_next];

            // transform.up is the nose — the fleet's one heading convention, and the same number
            // VehicleMeshDriver reads back to pick her picture. Never a ground bearing.
            float heading = BoatKinematics.BearingDegrees(_machine.up);
            return RouteFollowMath.Toward(heading, pos, target, false, _tuning);
        }
    }
}
