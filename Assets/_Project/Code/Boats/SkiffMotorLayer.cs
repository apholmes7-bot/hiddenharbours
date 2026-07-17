using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// THE SKIFF'S OUTBOARD — the remote-steer four-stroke on the transom of either 7 m skiff, drawn as two
    /// baked overlays (<b>lower</b>: leg + plate + skeg + prop; <b>upper</b>: bracket + cowl) on the hull
    /// picture and swivelled from the boat's REAL helm state. Throw the wheel over and the engine swings on its
    /// clamp; centre it and she comes back to dead ahead.
    ///
    /// <para><b>What it draws.</b> Per layer, per frame: the heading ROW comes from the very same pure snap the
    /// hull uses (<see cref="DirectionalBoatSprite.HeadingToFacingIndex"/> on the hull's DRAWN heading), so the
    /// motor can never disagree with the facing under it. The COLUMN comes from the helm — col 0 = −30° (full
    /// port), col 4 = dead ahead, col 8 = +30° (full starboard) — stepped toward its target at
    /// <see cref="_steerColumnsPerSecond"/> (the art README's "~8 fps") so the engine swivels rather than
    /// teleports. <b>There is no tiller</b>: steering is remote from the console wheel, which is exactly why the
    /// column is a straight read of helm state and not of anything the engine itself does.</para>
    ///
    /// <para><b>Per-heading draw order.</b> The upper layer always composites OVER the hull; the lower layer
    /// goes UNDER it for the stern-away headings SE/S/SW and over it everywhere else (art README; the rig's
    /// <c>MOTOR.behind = [3,4,5]</c>). Because that band FLIPS as she turns, the sorting orders are
    /// re-evaluated every frame from the drawn heading — see <see cref="SkiffMotorMath.SortingOrder"/>.</para>
    ///
    /// <para><b>Twin fit (sport only).</b> <see cref="MotorFit.Twin"/> blits the SAME single-engine sheets twice
    /// at ±<see cref="_twinMountMetres"/>, offset by <see cref="SkiffMotorMath.MountOffset"/> — exact, because
    /// the bake is orthographic. Both engines steer together off the one wheel. The FAR engine draws first
    /// within each layer. The console workboat is single-engine; the fit is the builder's choice, as the layer
    /// deliberately knows nothing about which hull it is bolted to.</para>
    ///
    /// <para><b>Why it parents to the hull's visual.</b> <see cref="DirectionalBoatSprite"/> force-resets its
    /// renderer's rotation to screen-identity every LateUpdate — a world-identity stomp that silently eats any
    /// external write. Hanging the motor renderers UNDER that child means they inherit the identical treatment
    /// for free, and, because the motor cell shares the hull's pivot (motor 272×216 pivot (136,120), hull
    /// 244×216 pivot (122,120) — the motor is wider only so hard-over never clips, and <b>both normalise to the
    /// same origin</b>), they register pixel-perfect at localPosition zero. Composite by pinning pivots, never
    /// by corners.</para>
    ///
    /// <para><b>Rock coupling.</b> The hull's rock is BAKED into its frames but the motor cells are baked LEVEL,
    /// so a rocking hull would leave its engine behind. The currently-drawn rock frame
    /// (<see cref="DirectionalBoatSprite.RockFrame"/>) is turned into a small pose by
    /// <see cref="DoryOarMath.RockPose"/> — reused, not re-derived, because the skiff rigs' <c>rockMotion(i)</c>
    /// is algebraically identical to the dory's. A calm/level hull (RockFrame −1) puts the motor back level. The
    /// hull frames already carry the hull's rock: <b>do not double-rock</b>.</para>
    ///
    /// <para><b>A dropped helm centres the engine.</b> The helm is read only while the
    /// <see cref="BoatController"/> is enabled — an unmanned or moored skiff draws dead ahead rather than
    /// staying hard-over. This is the blind spot #205 fixed for the oars; it is not reintroduced here.</para>
    ///
    /// <para><b>Rules.</b> Visual-only: it READS helm state and writes nothing back (rule 5), and there is no
    /// RNG — the pose is a pure function of helm + wave phase. Every rate/deadzone/amplitude is serialized
    /// (rule 6). Direct sprite compare, no allocation per LateUpdate (rule 7). Boats-internal (rule 4). The
    /// RAISED/TILT pose is not on the sheets and is deliberately not faked.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class SkiffMotorLayer : MonoBehaviour
    {
        /// <summary>Which paint build of the outboard is wired — the two the art ships off one recipe. Identity
        /// only: the builder passes the matching sheets, so nothing switches at runtime. It exists so a wired
        /// rig is self-describing (and a mismatch is visible) rather than anonymous.</summary>
        public enum MotorVariant
        {
            /// <summary>Graphite cowl, brushed badge — the console skiff (the workboat).</summary>
            Work = 0,
            /// <summary>White cowl, teal side flash, stainless prop — the sport skiff.</summary>
            Sport = 1,
        }

        /// <summary>How many engines hang on the transom.</summary>
        public enum MotorFit
        {
            /// <summary>One engine on the centreline. The console workboat's only fit.</summary>
            Single = 0,
            /// <summary>Two engines at ±<see cref="_twinMountMetres"/>, steering together. <b>Sport only.</b></summary>
            Twin = 1,
        }

        [Header("Identity")]
        [Tooltip("Which paint build is wired (Work → console skiff, Sport → sport skiff). Identity only — the builder passes the matching sheets.")]
        [SerializeField] private MotorVariant _variant = MotorVariant.Work;
        [Tooltip("Single (console workboat) or Twin (sport skiff upgrade — the SAME sheets blitted twice, both steering off the one wheel).")]
        [SerializeField] private MotorFit _fit = MotorFit.Single;

        [Header("Sheets (heading×column; the builder wires these — index = heading·columns + col)")]
        [Tooltip("Lower layer: leg + cavitation plate + skeg + prop. 72 ordered slices, index = heading×9 + steer col.")]
        [SerializeField] private Sprite[] _lowerFrames;
        [Tooltip("Upper layer: clamp bracket + cowl. Same layout as the lower sheet.")]
        [SerializeField] private Sprite[] _upperFrames;
        [Tooltip("Steer columns per heading row (the shipped motor sheets have 9: col 0 = full port, 4 = dead ahead, 8 = full starboard).")]
        [SerializeField] private int _columnsPerHeading = SkiffMotorMath.SteerColumns;
        [Tooltip("Heading rows in the sheets — must match the hull's facing count (8: N..NW clockwise).")]
        [SerializeField] private int _headingCount = SkiffMotorMath.HeadingCount;
        [Tooltip("The compass heading (degrees, 0 = North, CW) row 0 is drawn for. 0 — same as the hull's facings.")]
        [SerializeField] private float _zeroHeadingDegrees = 0f;

        [Header("Wiring (the builder sets these)")]
        [Tooltip("Engine A's LOWER renderer — a CHILD of the hull's visual, so it inherits the hull's snap treatment. In a Twin fit this is the PORT engine; in a Single fit it is the only engine, on the centreline.")]
        [SerializeField] private SpriteRenderer _lowerA;
        [Tooltip("Engine A's UPPER renderer — same parent.")]
        [SerializeField] private SpriteRenderer _upperA;
        [Tooltip("Engine B's LOWER renderer — the STARBOARD engine. Twin fit only; leave null for Single.")]
        [SerializeField] private SpriteRenderer _lowerB;
        [Tooltip("Engine B's UPPER renderer. Twin fit only; leave null for Single.")]
        [SerializeField] private SpriteRenderer _upperB;
        [Tooltip("The boat whose helm swivels the engine. Read only while the controller is enabled — a dropped helm centres the motor. Null = the helm reads centred.")]
        [SerializeField] private BoatController _boat;
        [Tooltip("The hull's directional sprite: the source of the DRAWN heading (so the motor picks the hull's row) and of the baked rock frame the motor rides. Null = the heading falls back to this transform's bow and no rock coupling.")]
        [SerializeField] private DirectionalBoatSprite _directionalSprite;
        [Tooltip("The hull's own renderer — the motor sorts against its sortingOrder, on its sorting layer. Null = the fallback order below is used and the layer is left alone.")]
        [SerializeField] private SpriteRenderer _hullRenderer;
        [Tooltip("Sorting order to sort against when no hull renderer is wired (the hull visual ships at 1).")]
        [SerializeField] private int _fallbackHullSortingOrder = 1;

        [Header("Steer feel (tunable)")]
        [Tooltip("Columns/sec the engine swivels toward the helm's target column (the art README's tempo is ~8). 0 = snap instantly.")]
        [SerializeField] private float _steerColumnsPerSecond = 8f;
        [Tooltip("|helm| at or below which the wheel reads as CENTRED, so a resting stick never trickles the engine off dead ahead.")]
        [SerializeField] private float _helmDeadzone = 0.05f;
        [Tooltip("Steer authority at the sheet's extremes, in degrees either side of dead ahead. The shipped sheets bake ±30° across 9 columns (7.5° steps) — changing this re-maps the helm across those SAME columns, it does not re-bake the art.")]
        [SerializeField] private float _maxSteerDegrees = SkiffMotorMath.MaxSteerDegrees;

        [Header("Twin fit (sport skiff — the same sheets blitted twice)")]
        [Tooltip("Lateral clamp offset from the centreline, in METRES. The engines sit at ±this. The art rig's twin spacing is 0.34.")]
        [SerializeField] private float _twinMountMetres = 0.34f;
        [Tooltip("Elevation the sheets were baked at, in degrees — the foreshortening of the twin offset in the ¾ view. 40 matches the art; changing it will misplace the second engine.")]
        [SerializeField] private float _bakeElevationDegrees = SkiffMotorMath.BakeElevationDegrees;

        [Header("Rock coupling (the motor cells are baked LEVEL — this leans them onto the hull)")]
        [Tooltip("Master strength of the whole rock coupling. 0 = the motor sits level on a rocking hull; 1 = the tuned read.")]
        [SerializeField] private float _rockStrength = 1f;
        [Tooltip("Baked heave amplitude in PIXELS. Console 1.3 (heavier hull, stiffer), Sport 1.5 (light glass hull, livelier). Reproduced EXACTLY at the sheet's PPU.")]
        [SerializeField] private float _rockHeavePixels = 1.3f;
        [Tooltip("Pixels-per-unit the motor/hull sheets import at (32) — converts the baked pixel heave into metres.")]
        [SerializeField] private float _pixelsPerUnit = 32f;
        [Tooltip("Degrees of lean at the peak of the roll. Console 3.4, Sport 3.8 (the rigs' rollA).")]
        [SerializeField] private float _rockRollDegrees = 3.4f;
        [Tooltip("Screen-vertical metres at the peak of the PITCH (the rigs' pitchA read as vertical travel in ¾ view). Keep small.")]
        [SerializeField] private float _rockPitchOffsetMeters = 0.02f;
        [Tooltip("Rock frames per heading in the hull's rock sheet (the skiff rock sheets ship 8) — the cycle the coupling reads.")]
        [SerializeField] private int _rockFrameCount = 8;

        // The continuous steer position the engine is swivelling through (NOT the drawn column — that is the
        // nearest column to this). One accumulator: both engines of a twin steer together off the one wheel.
        private float _steerPosition;

        private bool _baseCached;
        private Vector3 _lowerABaseLocalPosition;
        private Vector3 _upperABaseLocalPosition;
        private Vector3 _lowerBBaseLocalPosition;
        private Vector3 _upperBBaseLocalPosition;

        /// <summary>
        /// The helm the engine swivels to: −1 = full port, 0 = dead ahead, +1 = full starboard.
        ///
        /// <para><b>Why this is written from outside</b> rather than read off the boat: <see cref="BoatController"/>
        /// exposes <c>LeftOar</c>/<c>RightOar</c> but keeps its rudder state (<c>_steer</c>, set via
        /// <c>SetControl</c>) private, so there is no getter to source. This mirrors the established seam
        /// <see cref="DirectionalBoatSprite.RockFrame"/> uses — a settable property the driving system writes each
        /// frame — and keeps the layer a pure consumer either way. It is IGNORED while the helm is unmanned
        /// (<see cref="IsHelmManned"/>), so a stale value can never leave an empty skiff hard-over.</para>
        /// </summary>
        public float Helm { get; set; }

        /// <summary>True when a controller is wired AND actually driving. The gate on <see cref="Helm"/>: a
        /// player who disembarks mid-turn, a moored boat, or a skiff waiting while the deck is worked must all
        /// draw the engine dead ahead — never frozen hard-over.</summary>
        public bool IsHelmManned => _boat != null && _boat.isActiveAndEnabled;

        /// <summary>True when both sheets are wired as matching, full heading×column grids and every renderer
        /// the current <see cref="MotorFit"/> needs is present — the gate the whole component runs behind. False
        /// = it draws nothing (null-safe: missing or partially-sliced art leaves no half-state).</summary>
        public bool IsWired =>
            _lowerA != null && _upperA != null &&
            (_fit != MotorFit.Twin || (_lowerB != null && _upperB != null)) &&
            _headingCount > 0 && _columnsPerHeading > 0 &&
            _lowerFrames != null && _lowerFrames.Length == _headingCount * _columnsPerHeading &&
            _upperFrames != null && _upperFrames.Length == _headingCount * _columnsPerHeading;

        /// <summary>The steer column the engine drew last tick (diagnostics / tests). Dead ahead until it runs.</summary>
        public int SteerColumn { get; private set; } = SkiffMotorMath.CenterColumn(SkiffMotorMath.SteerColumns);

        /// <summary>The heading row the motor drew last tick (diagnostics / tests).</summary>
        public int HeadingRowDrawn { get; private set; }

        /// <summary>Which paint build is wired.</summary>
        public MotorVariant Variant => _variant;

        /// <summary>How many engines hang on the transom.</summary>
        public MotorFit Fit => _fit;

        /// <summary>
        /// Wire the layer from code (the builder's path — mirrors <see cref="DoryOarLayer.Configure"/> so a
        /// non-dev owner needn't touch the Inspector). The sheets must be the FULL ordered heading×column grids
        /// (index = heading·columns + steer col); anything else leaves <see cref="IsWired"/> false and the
        /// component inert. Pass <paramref name="lowerB"/>/<paramref name="upperB"/> only for a
        /// <see cref="MotorFit.Twin"/> fit (sport skiff); Single ignores them.
        /// </summary>
        public void Configure(Sprite[] lowerFrames, Sprite[] upperFrames,
                              SpriteRenderer lowerA, SpriteRenderer upperA,
                              SpriteRenderer lowerB, SpriteRenderer upperB,
                              BoatController boat, DirectionalBoatSprite directionalSprite,
                              SpriteRenderer hullRenderer,
                              MotorVariant variant, MotorFit fit,
                              int headingCount, int columnsPerHeading)
        {
            _lowerFrames = lowerFrames;
            _upperFrames = upperFrames;
            _lowerA = lowerA;
            _upperA = upperA;
            _lowerB = lowerB;
            _upperB = upperB;
            _boat = boat;
            _directionalSprite = directionalSprite;
            _hullRenderer = hullRenderer;
            _variant = variant;
            _fit = fit;
            _headingCount = Mathf.Max(1, headingCount);
            _columnsPerHeading = Mathf.Max(1, columnsPerHeading);
            _baseCached = false;
        }

        private void OnEnable()
        {
            // Wake dead ahead and level — never on a stale hard-over, never a frozen lean.
            _steerPosition = SkiffMotorMath.CenterColumn(_columnsPerHeading);
            SteerColumn = SkiffMotorMath.CenterColumn(_columnsPerHeading);
        }

        private void LateUpdate()
        {
            if (!IsWired) return;

            if (!_baseCached)
            {
                _lowerABaseLocalPosition = _lowerA.transform.localPosition;
                _upperABaseLocalPosition = _upperA.transform.localPosition;
                if (_fit == MotorFit.Twin)
                {
                    _lowerBBaseLocalPosition = _lowerB.transform.localPosition;
                    _upperBBaseLocalPosition = _upperB.transform.localPosition;
                }
                _baseCached = true;
            }

            // NOBODY AT THE HELM = NOBODY STEERING. Gating on the controller rather than trusting the helm to
            // have been cleared is right by construction — and it is exactly the blind spot #205 fixed for the
            // oars: a player who disembarks mid-turn would otherwise leave the engine pinned hard-over on an
            // empty skiff. An unmanned helm reads centred, so the motor comes back to dead ahead.
            float helm = IsHelmManned ? Helm : 0f;

            float dt = Time.deltaTime;
            int target = SkiffMotorMath.TargetColumnForHelm(helm, _helmDeadzone, _columnsPerHeading, _maxSteerDegrees);
            _steerPosition = SkiffMotorMath.StepTowardColumn(_steerPosition, target, _steerColumnsPerSecond, dt);
            SteerColumn = SkiffMotorMath.ColumnFromPosition(_steerPosition, _columnsPerHeading);

            int row = HeadingRow();
            HeadingRowDrawn = row;

            DoryOarMath.OarRockPose pose = RockPose();
            var lift = new Vector3(0f, pose.OffsetY, 0f);
            var lean = Quaternion.Euler(0f, 0f, pose.RollDegrees);
            int hullOrder = _hullRenderer != null ? _hullRenderer.sortingOrder : _fallbackHullSortingOrder;

            if (_fit == MotorFit.Twin)
            {
                float portMount = -_twinMountMetres;
                float starMount = +_twinMountMetres;
                // Which engine is FARTHER swaps as she turns — re-decided every frame, never assumed.
                bool portIsFar = SkiffMotorMath.IsFarEngine(portMount, starMount, row, _headingCount);

                DrawEngine(_lowerA, _upperA, _lowerABaseLocalPosition, _upperABaseLocalPosition,
                           row, hullOrder, portMount, portIsFar, lift, lean);
                DrawEngine(_lowerB, _upperB, _lowerBBaseLocalPosition, _upperBBaseLocalPosition,
                           row, hullOrder, starMount, !portIsFar, lift, lean);
            }
            else
            {
                // Single engine, on the centreline: no mount offset, and it is trivially the near one.
                DrawEngine(_lowerA, _upperA, _lowerABaseLocalPosition, _upperABaseLocalPosition,
                           row, hullOrder, 0f, false, lift, lean);
            }
        }

        /// <summary>
        /// The sheet ROW the motor indexes: the SAME pure snap the hull's picture used this frame. Taking the
        /// hull's already-DRAWN (quantized) heading and re-snapping it is idempotent, so the motor lands on the
        /// hull's row by construction — it can never show a different heading than the transom under it. With no
        /// directional sprite wired, this transform's bow stands in (the same convention).
        /// </summary>
        private int HeadingRow()
        {
            float heading = _directionalSprite != null
                ? _directionalSprite.DrawnHeadingDegrees()
                : DirectionalBoatSprite.HeadingDegreesFromBow(transform.up);
            return DirectionalBoatSprite.HeadingToFacingIndex(heading, _headingCount, _zeroHeadingDegrees);
        }

        /// <summary>The pose that rides the level-baked motor cells on the hull's currently-drawn rock frame.
        /// <see cref="DoryOarMath.RockPose"/> is REUSED, not re-derived: the skiff rigs' rockMotion(i) is
        /// algebraically identical to the dory's baked cycle (roll = rollA·sin(a), offsetY = heave·sin(a) +
        /// pitch·cos(a), a = frame·45°) — only the amplitudes differ, and those are tunables above.</summary>
        private DoryOarMath.OarRockPose RockPose()
        {
            int rockFrame = _directionalSprite != null && _directionalSprite.HasRockGrid
                ? _directionalSprite.RockFrame
                : SkiffMotorMath.LevelRockFrame;

            return DoryOarMath.RockPose(rockFrame, _rockFrameCount, _rockRollDegrees, _rockPitchOffsetMeters,
                                        _rockHeavePixels, _pixelsPerUnit, _rockStrength);
        }

        /// <summary>Draw one engine's two layers: the steer frame, the per-heading sorting order (the lower
        /// layer's band flips across the stern-away headings), and the pose — the shared rock lean plus this
        /// engine's exact clamp offset.</summary>
        private void DrawEngine(SpriteRenderer lower, SpriteRenderer upper,
                                Vector3 lowerBase, Vector3 upperBase,
                                int row, int hullOrder, float mountMetres, bool isFar,
                                Vector3 lift, Quaternion lean)
        {
            Draw(lower, _lowerFrames, row, SteerColumn);
            Draw(upper, _upperFrames, row, SteerColumn);

            lower.sortingOrder = SkiffMotorMath.SortingOrder(
                hullOrder, SkiffMotorMath.MotorPart.Lower, row, _headingCount, isFar);
            upper.sortingOrder = SkiffMotorMath.SortingOrder(
                hullOrder, SkiffMotorMath.MotorPart.Upper, row, _headingCount, isFar);

            if (_hullRenderer != null)
            {
                lower.sortingLayerID = _hullRenderer.sortingLayerID;
                upper.sortingLayerID = _hullRenderer.sortingLayerID;
            }

            // The clamp offset is EXACT (orthographic bake), so the second engine lands where the art would
            // have baked it. A single engine is on the centreline and this is Vector2.zero.
            Vector2 mount = mountMetres != 0f
                ? SkiffMotorMath.MountOffset(row, mountMetres, _headingCount, _bakeElevationDegrees)
                : Vector2.zero;
            var offset = new Vector3(mount.x, mount.y, 0f) + lift;

            lower.transform.localPosition = lowerBase + offset;
            lower.transform.localRotation = lean;
            upper.transform.localPosition = upperBase + offset;
            upper.transform.localRotation = lean;
        }

        private void Draw(SpriteRenderer renderer, Sprite[] frames, int row, int column)
        {
            int index = SkiffMotorMath.MotorGridIndex(row, column, _columnsPerHeading);
            if (index < 0 || index >= frames.Length) return;
            Sprite target = frames[index];
            // Direct sprite compare (not a cached index): a heading OR column change refreshes with no
            // allocation, and an unchanged frame skips the assignment entirely.
            if (target != null && renderer.sprite != target) renderer.sprite = target;
        }
    }
}
