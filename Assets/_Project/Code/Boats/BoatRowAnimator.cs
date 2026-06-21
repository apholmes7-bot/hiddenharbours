using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// Animates the dory's two oars as LAYERED sprites (the oar-rework rig — DoryHull base + DoryRower +
    /// LeftOar/RightOar children), replacing the old baked DoryRow frame strip. Each oar is one Oar.png
    /// instance parented under an oarlock pivot transform at its gunwale; this rotates each pivot about
    /// its fulcrum to row. Crucially each oar animates from ITS OWN per-oar state
    /// (<see cref="BoatController.LeftOar"/> / <see cref="BoatController.RightOar"/>, forward +1 / back -1
    /// / idle 0), so a one-sided stroke (e.g. port forward + starboard back) shows exactly that — the two
    /// oars sweep independently. Forward state rows a forward stroke, back state a backstroke, idle eases
    /// to a neutral (shipped) angle.
    ///
    /// It only animates while the home hull (the dory) is the active hull; buying an engine boat (the
    /// Punt) hides the whole oar rig so the swapped hull sprite stands alone. The per-oar angle is a pure
    /// static helper so the convention is unit-testable; the exact amplitude/bias/tempo are feel tunables.
    /// </summary>
    [RequireComponent(typeof(BoatController))]
    public class BoatRowAnimator : MonoBehaviour
    {
        [Header("Rig (wired by the greybox builder)")]
        [Tooltip("Port-oar oarlock pivot transform (Oar.png parented under it, rotated about the fulcrum).")]
        [SerializeField] private Transform _leftOarPivot;
        [Tooltip("Starboard-oar oarlock pivot transform.")]
        [SerializeField] private Transform _rightOarPivot;
        [Tooltip("Parent of the oars + rower; hidden when the active hull isn't the rowed dory (e.g. the Punt).")]
        [SerializeField] private GameObject _oarRig;

        [Header("Stroke feel (tunable)")]
        [Tooltip("Peak sweep of the oar about its oarlock, in degrees.")]
        [SerializeField] private float _strokeAmplitudeDeg = 32f;
        [Tooltip("How far the sweep CENTRE leans toward the stroke direction (deg) — distinguishes ahead from astern.")]
        [SerializeField] private float _strokeBiasDeg = 12f;
        [Tooltip("Oar-cycle phase rate (radians/sec) while rowing — the stroke tempo.")]
        [SerializeField] private float _strokeTempo = 6f;
        [Tooltip("Max degrees/sec the oar angle slews toward its target (smooths the catch and the ease to neutral).")]
        [SerializeField] private float _slewDegPerSec = 420f;
        [Tooltip("|oar state| below which the oar is idle and eases back to neutral.")]
        [SerializeField] private float _activeThreshold = 0.05f;

        // Left and right oars sweep oppositely for the same stroke direction (mirror); flip these if the
        // forward/back read of a stroke looks reversed at play (pure feel sign, no logic depends on it).
        private const float LeftSideSign = 1f;
        private const float RightSideSign = -1f;

        private BoatController _boat;
        private BoatHullDef _homeHull;     // the hull this oar rig belongs to (recorded at start)
        private float _leftPhase, _rightPhase;
        private float _leftAngle, _rightAngle;

        // ---- pure logic (unit-testable) -----------------------------------------------------

        /// <summary>
        /// Target oarlock angle (deg) for a stroke phase + signed oar state. The sweep oscillates around a
        /// direction-biased centre; forward (state &gt; 0) and back (state &lt; 0) lean opposite ways, and
        /// <paramref name="sideSign"/> mirrors port vs starboard. State 0 → neutral (0). Pure + static.
        /// </summary>
        public static float OarTargetAngle(float phase, float state, float sideSign, float biasDeg, float amplitudeDeg)
        {
            if (Mathf.Approximately(state, 0f)) return 0f;
            float dir = Mathf.Sign(state);   // forward (+) vs backstroke (-)
            return sideSign * dir * (biasDeg + amplitudeDeg * Mathf.Sin(phase));
        }

        // ---- lifecycle ----------------------------------------------------------------------

        private void Awake()
        {
            _boat = GetComponent<BoatController>();
            _homeHull = _boat != null ? _boat.Hull : null;   // the hull whose oar rig we own
        }

        private void Update()
        {
            if (_boat == null) return;

            // Only the rowed dory has oars: hide the rig when a bought engine hull (the Punt) is active.
            bool isHomeHull = _homeHull == null || _boat.Hull == _homeHull;
            if (_oarRig != null && _oarRig.activeSelf != isHomeHull) _oarRig.SetActive(isHomeHull);
            if (!isHomeHull) return;

            float dt = Time.deltaTime;
            AnimateOar(_leftOarPivot,  _boat.LeftOar,  LeftSideSign,  ref _leftPhase,  ref _leftAngle,  dt);
            AnimateOar(_rightOarPivot, _boat.RightOar, RightSideSign, ref _rightPhase, ref _rightAngle, dt);
        }

        private void AnimateOar(Transform pivot, float state, float sideSign, ref float phase, ref float angle, float dt)
        {
            float target;
            if (Mathf.Abs(state) > _activeThreshold)
            {
                phase += dt * _strokeTempo;
                target = OarTargetAngle(phase, state, sideSign, _strokeBiasDeg, _strokeAmplitudeDeg);
            }
            else
            {
                phase = 0f;
                target = 0f;   // oars shipped to neutral at rest
            }
            angle = Mathf.MoveTowardsAngle(angle, target, _slewDegPerSec * dt);
            if (pivot != null) pivot.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
