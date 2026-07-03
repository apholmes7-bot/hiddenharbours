using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// REVERSIBLE PROTOTYPE / A-B test harness (not the shipping render path). Lets the owner FEEL two
    /// ways to show a boat that turns, so he can decide before drawing more angles:
    ///
    ///   • <see cref="RotationMode.SnapDirectional"/> — swap among N pre-drawn facings by heading and keep
    ///     the sprite SCREEN-AXIS-ALIGNED (the body's transform may rotate with physics, but the picture
    ///     does NOT rotate — only the chosen facing changes). This is the approach for art with a baked
    ///     ¾ perspective (visible cabin faces) that looks wrong when rotated.
    ///   • <see cref="RotationMode.SmoothRotateSingle"/> — ignore the facing array and just let a single
    ///     sprite rotate with the hull (today's Dory/Punt behaviour) for a back-to-back comparison.
    ///
    /// It reads the boat's heading from the transform (bow = <c>transform.up</c>, matching
    /// <see cref="BoatController"/>) and drives a CHILD <see cref="SpriteRenderer"/>. It is ADDITIVE: it
    /// sits on a separate test boat and never changes how the existing Dory/Punt render.
    ///
    /// The heading→facing-index math is a pure static helper (<see cref="HeadingToFacingIndex"/>),
    /// generalised to ANY facing count so 8/16-way art drops in later; shipped configured for 4 (N/E/S/W).
    /// </summary>
    [DisallowMultipleComponent]
    public class DirectionalBoatSprite : MonoBehaviour
    {
        /// <summary>How this test boat presents its turn — the thing the owner is comparing.</summary>
        public enum RotationMode
        {
            /// <summary>Snap to the nearest pre-drawn facing; keep the sprite screen-aligned (no picture rotation).</summary>
            SnapDirectional,
            /// <summary>Let one sprite rotate with the hull (today's behaviour) — the A/B comparison.</summary>
            SmoothRotateSingle,
        }

        [Header("Mode (toggle live in the test harness)")]
        [Tooltip("SnapDirectional = swap facings + keep the picture screen-aligned. " +
                 "SmoothRotateSingle = rotate one sprite with the hull (today's behaviour).")]
        [SerializeField] private RotationMode _mode = RotationMode.SnapDirectional;

        [Header("Facings (CW from the zero heading)")]
        [Tooltip("The pre-drawn facings in CLOCKWISE order starting at the zero heading. " +
                 "For the 4-way fishing boat: element 0 = North (up), then East, South, West.")]
        [SerializeField] private Sprite[] _facings;

        [Tooltip("The compass heading (degrees, 0 = North/up, CW) that element 0 of the array is drawn for. " +
                 "Default 0 because element 0 is the North-facing sprite.")]
        [SerializeField] private float _zeroHeadingDegrees = 0f;

        [Tooltip("Child SpriteRenderer the facing is written to. In SnapDirectional it is counter-rotated " +
                 "to stay screen-axis-aligned; in SmoothRotateSingle it is left to rotate with the hull.")]
        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("Optional single sprite shown in SmoothRotateSingle. If null, the current facing[0] is used " +
                 "so the comparison is the SAME artwork, just rotated vs snapped.")]
        [SerializeField] private Sprite _smoothModeSprite;

        private int _lastIndex = -1;

        /// <summary>
        /// Additive visual tilt (degrees, +CCW about z) composed into the child renderer AFTER this
        /// component's per-mode rotation policy each LateUpdate — the seam <see cref="BoatWaveMotion"/>
        /// drives the wave ROLL through (ADR 0018 B2). This component force-resets the renderer's
        /// rotation every frame (screen-identity in Snap, hull yaw in Smooth — the stomp that once ate
        /// the boat spotlight), so an external rotation write would be silently overwritten; routing
        /// the tilt through here composes it after the reset instead of fighting it. 0 (the default)
        /// is EXACTLY today's behaviour.
        /// </summary>
        public float VisualTiltDegrees { get; set; }

        /// <summary>The mode this prototype is currently presenting. Settable so the harness can toggle it live.</summary>
        public RotationMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                _lastIndex = -1;   // force a re-evaluation of the sprite + rotation next frame
            }
        }

        /// <summary>Flip between the two modes (the live A/B key in the harness). Returns the new mode.</summary>
        public RotationMode ToggleMode()
        {
            Mode = _mode == RotationMode.SnapDirectional
                ? RotationMode.SmoothRotateSingle
                : RotationMode.SnapDirectional;
            return _mode;
        }

        /// <summary>
        /// Configure the prototype from code (used by the test builder so a non-dev owner needn't wire the
        /// Inspector). All optional past the facings array.
        /// </summary>
        public void Configure(Sprite[] facings, SpriteRenderer renderer, float zeroHeadingDegrees = 0f,
                              Sprite smoothModeSprite = null, RotationMode mode = RotationMode.SnapDirectional)
        {
            _facings = facings;
            _renderer = renderer;
            _zeroHeadingDegrees = zeroHeadingDegrees;
            _smoothModeSprite = smoothModeSprite;
            _mode = mode;
            _lastIndex = -1;
        }

        // ---- pure logic (unit-testable, deterministic) --------------------------------------

        /// <summary>
        /// Compass heading of the bow (degrees, 0 = North/+Y, 90 = East/+X, clockwise) for a bow direction.
        /// Matches the project's bearing convention (Core BoatKinematics.BearingDegrees) so the facing math
        /// and the compass/wind widgets never disagree on which way is North. A zero vector falls back to 0
        /// (North), never NaN. Pure + static.
        /// </summary>
        public static float HeadingDegreesFromBow(Vector2 bow)
        {
            if (bow.sqrMagnitude < 1e-12f) return 0f;
            float deg = Mathf.Atan2(bow.x, bow.y) * Mathf.Rad2Deg; // (x,y): 0 at +Y, +90 at +X → CW
            deg %= 360f;
            if (deg < 0f) deg += 360f;
            return deg;
        }

        /// <summary>
        /// Pick the facing-array index for a compass heading, snapping to the NEAREST of <paramref name="count"/>
        /// evenly-spaced facings laid out CLOCKWISE from <paramref name="zeroHeadingDeg"/> (the heading that
        /// element 0 is drawn for). Generalised to any count so 8/16-way art drops in unchanged.
        ///
        /// Boundary rule is explicit and off-by-one-free: a heading exactly on a bucket edge rounds to the
        /// NEXT facing clockwise (half-up), so e.g. for count=4 a heading of 45° (dead between N and E) picks
        /// East, 135° picks South, etc. — never an ambiguous tie. The result is always in [0, count).
        /// Pure + static + deterministic (no engine state, no allocation).
        /// </summary>
        public static int HeadingToFacingIndex(float headingDeg, int count, float zeroHeadingDeg)
        {
            if (count <= 0) return 0;
            float step = 360f / count;
            // Heading measured from the zero facing, wrapped to [0, 360).
            float rel = (headingDeg - zeroHeadingDeg) % 360f;
            if (rel < 0f) rel += 360f;
            // Half-up rounding (FloorToInt(x + 0.5)) so bucket edges resolve to the next facing CW,
            // deterministically — no banker's-rounding tie at 45/135/225/315 for count=4.
            int idx = Mathf.FloorToInt(rel / step + 0.5f);
            idx %= count;             // 360°≡0° wraps the top bucket back to element 0
            if (idx < 0) idx += count;
            return idx;
        }

        // ---- runtime ------------------------------------------------------------------------

        private void Reset()
        {
            // Convenience when added in-editor: grab a child renderer if present.
            _renderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (_renderer == null) return;

            if (_mode == RotationMode.SmoothRotateSingle)
            {
                ApplySmooth();
                return;
            }
            ApplySnap();
        }

        // Today's behaviour: one sprite that rotates with the hull. The child renderer simply inherits the
        // body's rotation (identity local rotation), so the picture turns with the boat.
        private void ApplySmooth()
        {
            Sprite single = _smoothModeSprite != null
                ? _smoothModeSprite
                : (_facings != null && _facings.Length > 0 ? _facings[0] : null);
            if (single != null && _renderer.sprite != single) _renderer.sprite = single;
            _lastIndex = -1;                                   // so re-entering Snap re-applies the facing
            // Rotate WITH the hull (inherit body yaw) + the additive wave tilt (0 = identity = today).
            _renderer.transform.localRotation = Quaternion.Euler(0f, 0f, VisualTiltDegrees);
        }

        // The prototype under test: swap to the nearest pre-drawn facing and counter-rotate the renderer so
        // the picture stays screen-axis-aligned — the body's physics yaw must NOT rotate the artwork.
        private void ApplySnap()
        {
            if (_facings == null || _facings.Length == 0) return;

            float heading = HeadingDegreesFromBow(transform.up);
            int idx = HeadingToFacingIndex(heading, _facings.Length, _zeroHeadingDegrees);
            if (idx != _lastIndex)
            {
                Sprite s = _facings[idx];
                if (s != null) _renderer.sprite = s;
                _lastIndex = idx;
            }
            // Counter-rotate to world-identity: cancel whatever yaw the body has so the chosen facing is
            // drawn screen-aligned (the snap shows a DIFFERENT picture, it never rotates one) — then
            // compose the additive wave tilt ON the screen-aligned picture (0 = identity = today).
            _renderer.transform.rotation = Quaternion.Euler(0f, 0f, VisualTiltDegrees);
        }
    }
}
