using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>The dory's tiller seat</b>: the <see cref="ITillerStation"/> of a hull steered by a tiller
    /// outboard with no helm console. Installed by <see cref="BoatHullSkinner"/> beside the
    /// <see cref="OutboardMotorMeshLayer"/> it reads, only while the engine is fitted and the visual's
    /// <see cref="TillerStationConfig"/> is enabled. Each frame it answers where the tiller grip is
    /// (turned with the engine exactly as the engine's mesh is drawn), where his hips, feet and free
    /// hand go, and how far he leans with the throttle. Presentation only (rule 5): nothing in the
    /// simulation reads it, and it keeps no clock of its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OutboardTillerStation : MonoBehaviour, ITillerStation
    {
        private OutboardMotorMeshLayer _motor;
        private BoatController _boat;
        private Vector3 _pivot;
        private Vector3 _mount;
        private TillerStationConfig _config;
        private bool _configured;

        /// <summary>The station's config, as installed (tests and the inspector).</summary>
        public TillerStationConfig Config => _config;

        /// <summary>
        /// Wire the station to <paramref name="motor"/> (the engine the tiller belongs to; its first
        /// engine is the one steered by hand), the engine fitting's pivot and its mount translation
        /// (clamp position plus borrowed-fitting shift), and the hull's <paramref name="config"/>.
        /// <paramref name="boat"/> gives the throttle he leans into; null leans him at idle.
        /// </summary>
        public void Configure(OutboardMotorMeshLayer motor, BoatController boat, Vector3 enginePivotLocalMeters,
                              Vector3 engineMountMeters, in TillerStationConfig config)
        {
            _motor = motor;
            _boat = boat;
            _pivot = enginePivotLocalMeters;
            _mount = engineMountMeters;
            _config = config;
            _configured = motor != null && config.Enabled;
        }

        /// <inheritdoc/>
        public bool TryGetTillerTargets(out RowingTargets targets)
        {
            targets = default;
            if (!_configured || _motor == null || !_motor.IsWired) return false;
            Quaternion engine = OutboardMotorMeshPose.Rotation(_motor.SteerDegrees, _motor.TiltDegrees);
            float throttle = _boat != null && _boat.isActiveAndEnabled ? _boat.Throttle : 0f;
            targets = Targets(in _config, _pivot, _mount, engine, throttle, _motor.SteerDegrees, _motor.MaxSteerDegrees);
            return true;
        }

        /// <summary>
        /// The station's targets in the hull's rig frame for an engine posed by
        /// <paramref name="engineRotation"/> and a <paramref name="throttle"/> of -1..1. Pure: the
        /// EditMode tests drive it across the whole steering range without a scene.
        ///
        /// <para>The tiller is held by the hand on its side at rest, all the way across; the other "grip" is placed a
        /// metre to his other side and marked let go, so <see cref="RowingPoseSolver.ToFigure"/> gives
        /// the steering hand the tiller and rests the free one on his thigh.</para>
        ///
        /// <para><paramref name="steerDegrees"/> over <paramref name="maxSteerDegrees"/> (the engine's
        /// drawn steer and its authority) turns him into the turn; left at 0 he sits square.</para>
        /// </summary>
        public static RowingTargets Targets(in TillerStationConfig config, Vector3 enginePivot, Vector3 engineMount,
                                            Quaternion engineRotation, float throttle,
                                            float steerDegrees = 0f, float maxSteerDegrees = 0f)
        {
            Vector3 grip = TillerStationMath.Grip(config.TillerGripAtRestLocalMeters, enginePivot, engineRotation, engineMount);
            float bearing = config.FacingDeckBearingDegrees * Mathf.Deg2Rad;
            // His right in the hull frame (+x starboard, +y bow): the facing turned a quarter clockwise.
            var right = new Vector3(Mathf.Cos(bearing), -Mathf.Sin(bearing), 0f);
            // The hand is chosen by where the tiller lies at rest, never where it is now: he keeps
            // one hand on it through the whole sweep instead of swapping hands as it crosses his lap.
            Vector3 rest = TillerStationMath.Grip(config.TillerGripAtRestLocalMeters, enginePivot, Quaternion.identity, engineMount);
            bool onRight = Vector3.Dot(rest - config.SeatLocalMeters, right) >= 0f;
            Vector3 free = config.SeatLocalMeters + (onRight ? -right : right) * FreeHandSideMeters;

            return new RowingTargets
            {
                StarboardGripValid = onRight,
                PortGripValid = !onRight,
                StarboardGrip = onRight ? grip : free,
                PortGrip = onRight ? free : grip,
                StarboardHandleEnd = grip,
                PortHandleEnd = grip,
                Seat = config.SeatLocalMeters,
                PortBrace = config.PortFootBraceLocalMeters,
                StarboardBrace = config.StarboardFootBraceLocalMeters,
                LaybackDegrees = 0f,
                ReachLeanMaxDegrees = config.ReachLeanMaxDegrees,
                BodyClearanceMeters = config.BodyClearanceMeters,
                BodyClearLeanMaxDegrees = config.BodyClearLeanMaxDegrees,
                BodyClearTwistMaxDegrees = config.BodyClearTwistMaxDegrees,
                RestWeight = 1f,
                RestSlumpDegrees = TillerStationMath.Lean(config.IdleLeanDegrees, config.SitUpPerThrottleDegrees, throttle),
                RestBreathDegrees = config.BreathDegrees,
                RestBreathSeconds = config.BreathSeconds,
                RestHandAlongThigh = config.RestHandAlongThigh,
                RestHandAboveThighMeters = config.RestHandAboveThighMeters,
                TurnStarboardDegrees = TillerStationMath.Look(config.LookIntoTurnDegrees, steerDegrees, maxSteerDegrees),
                HeadTurnStarboardDegrees = TillerStationMath.Look(config.HeadLookIntoTurnDegrees, steerDegrees, maxSteerDegrees),
            };
        }

        /// <summary>How far to his side the let-go "grip" is placed, metres: only its side matters
        /// (it tells the solve which hand is free), never its distance.</summary>
        private const float FreeHandSideMeters = 1f;
    }
}
