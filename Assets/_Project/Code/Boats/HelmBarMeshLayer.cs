using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The pose of a helm bar (a jetski's handlebars): one rotation about the steering column's own
    /// axis. Pure, so the sign and the axis are pinned by a test and not by a plate.
    /// </summary>
    public static class HelmBarPose
    {
        /// <summary>Degrees of bar for a helm of -1..1, clamped to the lock.</summary>
        public static float DegreesForHelm(float helm, float lockDegrees) =>
            Mathf.Clamp(helm, -1f, 1f) * Mathf.Max(0f, lockDegrees);

        /// <summary>
        /// The rotation carrying the bar from its baked pose (dead ahead) to <paramref name="degrees"/>.
        /// The rigs turn a bar in the (starboard, toward-the-rider) plane of the column, which is a turn
        /// about the column axis by the NEGATIVE angle; a zero axis means straight up.
        /// </summary>
        public static Quaternion Rotation(float degrees, Vector3 axis)
        {
            Vector3 n = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector3.forward;
            return Quaternion.AngleAxis(-degrees, n);
        }

        /// <summary>Move <paramref name="current"/> toward <paramref name="target"/> at a bounded rate.</summary>
        public static float Step(float current, float target, float degreesPerSecond, float dt) =>
            Mathf.MoveTowards(current, target, Mathf.Max(0f, degreesPerSecond) * Mathf.Max(0f, dt));
    }

    /// <summary>
    /// Turns a hull's helm bar fitting with the helm. A visual layer: it PULLS the helm off the boat
    /// and writes one rotation a frame; nothing reads it back, so it never feeds the sim.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HelmBarMeshLayer : MonoBehaviour
    {
        [Tooltip("How fast the bar follows the helm, degrees a second. A hand on a bar is quick; this " +
                 "only keeps a keyboard's full-lock tap from snapping.")]
        [SerializeField, Min(0f)] private float _degreesPerSecond = 240f;

        private IHullPropRenderer _bar;
        private BoatController _boat;
        private Vector3 _axis;
        private float _lockDegrees;

        /// <summary>Where the bar stands now, degrees; + is the helm's + (the rigs' own sign).</summary>
        public float BarDegrees { get; private set; }

        public bool IsWired => _bar != null;

        /// <summary>The helm the bar turns to. An unmanned helm reads dead ahead.</summary>
        public float Helm => _boat != null && _boat.isActiveAndEnabled ? _boat.Steer : 0f;

        /// <summary>Wire the layer from the skinner. Idempotent: a re-skin re-points it.</summary>
        public void Configure(IHullPropRenderer bar, BoatController boat, Vector3 axis, float lockDegrees)
        {
            _bar = bar;
            _boat = boat;
            _axis = axis;
            _lockDegrees = Mathf.Max(0f, lockDegrees);
            BarDegrees = 0f;
            if (_bar != null) _bar.LocalRotation = Quaternion.identity;
        }

        private void OnEnable() => BarDegrees = 0f;

        private void LateUpdate()
        {
            if (!IsWired) return;
            BarDegrees = HelmBarPose.Step(BarDegrees, HelmBarPose.DegreesForHelm(Helm, _lockDegrees),
                                          _degreesPerSecond, Time.deltaTime);
            _bar.LocalRotation = HelmBarPose.Rotation(BarDegrees, _axis);
        }
    }
}
