using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// VS-24 headline visual — the tide-aware moving shoreline. Reads the live tide through the Core
    /// service (<c>GameServices.Environment.Sample().TideHeight</c>, metres above chart datum) and slides
    /// the water transform so a falling tide exposes more shore and a rising tide floods it
    /// (design/time-tides-weather.md §3.5: a tile is underwater iff <c>tideHeight &gt; seabedElevation</c>).
    ///
    /// <para><b>Visual only.</b> It consumes the same tide value the simulation uses, through the Core
    /// interface — it never modifies the tide (gameplay-systems owns that) nor reaches into another
    /// module's code. 1 world unit = 1 m (PPU 32), so tide metres map straight to world units.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TideShoreline : MonoBehaviour
    {
        [Tooltip("The water transform to slide. Defaults to this object.")]
        [SerializeField] private Transform _water;

        [Tooltip("Direction the water advances onto the land as the tide rises (this object's space).")]
        [SerializeField] private Vector2 _floodDirection = Vector2.up;

        [Tooltip("Metres the waterline travels per metre of vertical tide (foreshore slope). " +
                 "Coddle Cove swings ~±1.6 m; ~3 reads as a gentle beach.")]
        [SerializeField] private float _metresPerTideMetre = 3f;

        [Tooltip("Higher = snappier follow. The waterline eases toward the target so it never pops.")]
        [SerializeField] private float _followSharpness = 2f;

        private Vector3 _meanTideAnchor;   // local position that corresponds to tideHeight = 0
        private bool _anchored;

        private void Awake()
        {
            if (_water == null) _water = transform;
            _meanTideAnchor = _water.localPosition;
            _anchored = true;
        }

        private void LateUpdate()
        {
            // Guard: services may not be wired yet (edit mode / before GameRoot.Awake).
            if (!_anchored || GameServices.Environment == null) return;

            float tideHeight = GameServices.Environment.Sample().TideHeight; // m rel datum (+ = higher water)
            Vector3 target = _meanTideAnchor + WaterlineOffset(tideHeight, _metresPerTideMetre, _floodDirection);

            float t = 1f - Mathf.Exp(-Mathf.Max(0f, _followSharpness) * Time.deltaTime);
            _water.localPosition = Vector3.Lerp(_water.localPosition, target, t);
        }

        /// <summary>
        /// Pure mapping (testable): where the waterline sits relative to its mean-tide anchor for a given
        /// tide height. Positive tide floods along <paramref name="floodDirection"/>; negative recedes.
        /// Result is in world units (1 unit = 1 m).
        /// </summary>
        public static Vector3 WaterlineOffset(float tideHeight, float metresPerTideMetre, Vector2 floodDirection)
            => (Vector3)(floodDirection.normalized * (tideHeight * metresPerTideMetre));
    }
}
