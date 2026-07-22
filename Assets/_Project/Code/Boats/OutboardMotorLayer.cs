using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// A BOAT'S OUTBOARD — the four-stroke on the transom of either 7 m skiff or of the 5.2 m punt, drawn as
    /// two baked overlays (<b>lower</b>: leg + plate + skeg + prop; <b>upper</b>: bracket + cowl) on the hull
    /// picture and swivelled from the boat's REAL helm state. Throw the helm over and the engine swings;
    /// centre it and she comes back to dead ahead.
    ///
    /// <para><b>Not the skiffs' alone (this was <c>SkiffMotorLayer</c>).</b> The punt's engine indexes the
    /// same 9-column heading×steer grid and obeys the same draw-order rule, so she wears this very component
    /// — which is what the rename says out loud. Everything that DIFFERS between the boats is data on their
    /// <see cref="BoatVisualDef"/>: the sheets, the paint build, the fit, the rock amplitudes, the steer
    /// authority. No hull is named in this file.</para>
    ///
    /// <para><b>What it draws.</b> Per layer, per frame: the heading ROW comes from the very same pure snap the
    /// hull uses (<see cref="DirectionalBoatSprite.HeadingToFacingIndex"/> on the hull's DRAWN heading), so the
    /// motor can never disagree with the facing under it. The COLUMN comes from the helm — col 0 = full port,
    /// col 4 = dead ahead, col 8 = full starboard, spanning ±<see cref="_maxSteerDegrees"/> (the skiffs bake
    /// ±30°, the punt ±32°) — stepped toward its target at <see cref="_steerColumnsPerSecond"/> (the art
    /// READMEs' "~8 fps") so the engine swivels rather than teleports.</para>
    ///
    /// <para><b>Remote wheel vs. tiller is the ART's business, not this class's.</b> The skiffs steer remotely
    /// from a console wheel and the whole engine swivels on its clamp; the punt is TILLER-steered, the bar
    /// swinging across the transom under the operator's aft hand. Either way the column is a straight read of
    /// HELM STATE, never of anything the engine itself does — so one arithmetic drives both and the whole
    /// difference is which sheets the visual binds. (The punt kit also ships <c>PuntMotorGrips.json</c>, the
    /// tiller-grip point per heading×steer for seating an operator's hand. It is deliberately UNWIRED: no punt
    /// operator sprite exists yet, and faking one is not this phase's job.)</para>
    ///
    /// <para><b>Per-heading draw order.</b> The upper layer always composites OVER the hull; the lower layer
    /// goes UNDER it for the stern-away headings SE/S/SW and over it everywhere else (art README; the rig's
    /// <c>MOTOR.behind = [3,4,5]</c>). Because that band FLIPS as she turns, the sorting orders are
    /// re-evaluated every frame from the drawn heading — see <see cref="OutboardMotorMath.SortingOrder"/>.</para>
    ///
    /// <para><b>Twin fit (the sport skiff only).</b> <see cref="MotorFit.Twin"/> blits the SAME single-engine
    /// sheets twice at ±<see cref="_twinMountMetres"/>, offset by <see cref="OutboardMotorMath.MountOffset"/> —
    /// exact, because the bake is orthographic. Both engines steer together off the one wheel. The FAR engine
    /// draws first within each layer. The console workboat and the punt are single-engine (the punt kit ships
    /// no twin at all); the fit is the visual's choice, as the layer deliberately knows nothing about which
    /// hull it is bolted to.</para>
    ///
    /// <para><b>Why it parents to the hull's visual.</b> <see cref="DirectionalBoatSprite"/> force-resets its
    /// renderer's rotation to screen-identity every LateUpdate — a world-identity stomp that silently eats any
    /// external write. Hanging the motor renderers UNDER that child means they inherit the identical treatment
    /// for free, and, because <b>every kit draws its motor cell WIDER than its hull cell but on the SAME
    /// normalised pivot</b> (skiffs: motor 272×216 pivot (136,120) vs hull 244×216 pivot (122,120); punt:
    /// motor 212×168 pivot (106,94) vs hull 184×168 pivot (92,94) — the extra width is only so hard-over never
    /// clips), they register pixel-perfect at localPosition zero. Composite by pinning pivots, never by
    /// corners. Note the two kits' origins are NOT interchangeable: each is derived from its own cell
    /// (skiff y = 0.4444, punt y = 0.4405), and the slicer keeps them as separate consts for that reason.</para>
    ///
    /// <para><b>Rock coupling.</b> The hull's rock is BAKED into its frames but the motor cells are baked LEVEL,
    /// so a rocking hull would leave its engine behind. The currently-drawn rock frame
    /// (<see cref="DirectionalBoatSprite.RockFrame"/>) is turned into a small pose by
    /// <see cref="DoryOarMath.RockPose"/> — reused, not re-derived, because every boat rig's <c>rockMotion(i)</c>
    /// is algebraically identical to the dory's. A calm/level hull (RockFrame −1) puts the motor back level. The
    /// hull frames already carry the hull's rock: <b>do not double-rock</b>.</para>
    ///
    /// <para><b>A dropped helm centres the engine.</b> The helm is read only while the
    /// <see cref="BoatController"/> is enabled — an unmanned or moored boat draws dead ahead rather than
    /// staying hard-over. This is the blind spot #205 fixed for the oars; it is not reintroduced here.</para>
    ///
    /// <para><b>Rules.</b> Visual-only: it READS helm state and writes nothing back (rule 5), and there is no
    /// RNG — the pose is a pure function of helm + wave phase. Every rate/deadzone/amplitude is serialized
    /// (rule 6). Direct sprite compare, no allocation per LateUpdate (rule 7). Boats-internal (rule 4). The
    /// RAISED/TILT pose is not on the sheets and is deliberately not faked.</para>
    /// </summary>
    [DisallowMultipleComponent]
    // A READER of the rock frame + drawn heading, so it runs LAST of the boat's visual chain
    // (BoatWaveMotion −120 → DirectionalBoatSprite −110 → this −100). See BoatWaveMotion for why.
    [DefaultExecutionOrder(-100)]
    public class OutboardMotorLayer : MonoBehaviour
    {
        /// <summary>
        /// Which paint build of the outboard is wired — the ones the art ships. Identity only: the builder
        /// passes the matching sheets, so nothing switches at runtime. It exists so a wired rig is
        /// self-describing (and a mismatch is visible) rather than anonymous.
        ///
        /// <para><b>Append-only, like every id in this project (CLAUDE.md §5).</b> These serialize BY VALUE
        /// onto every authored <see cref="BoatVisualDef"/>, so renumbering <see cref="Work"/> or
        /// <see cref="Sport"/> would silently repaint the skiffs' engines. Add builds at the END; never
        /// reorder.</para>
        /// </summary>
        public enum MotorVariant
        {
            /// <summary>Graphite cowl, brushed badge — the console skiff (the workboat).</summary>
            Work = 0,
            /// <summary>White cowl, teal side flash, stainless prop — the sport skiff.</summary>
            Sport = 1,
            /// <summary>Weathered grey/black, paint scuffs, pan rust — the punt's STARTER tiller engine.</summary>
            Basic = 2,
            /// <summary>~15% larger domed cowl, gloss-black pan, white top, red wrap stripe + side flashes,
            /// brighter prop — the punt's UPGRADED tiller engine. A drop-in swap: same cell, pivot, steer
            /// columns and grip geometry as <see cref="Basic"/>, so the upgrade is two PNGs and nothing else.</summary>
            Upgraded = 3,
        }

        /// <summary>How many engines hang on the transom. Append-only (see <see cref="MotorVariant"/>).</summary>
        public enum MotorFit
        {
            /// <summary>One engine on the centreline — the console workboat's and the punt's only fit.</summary>
            Single = 0,
            /// <summary>Two engines at ±<see cref="_twinMountMetres"/>, steering together. <b>The sport skiff
            /// only</b>: no other kit ships a twin.</summary>
            Twin = 1,
        }

        [Header("Identity")]
        [Tooltip("Which paint build is wired (Work → console skiff, Sport → sport skiff, Basic/Upgraded → the punt's two engines). Identity only — the builder passes the matching sheets.")]
        [SerializeField] private MotorVariant _variant = MotorVariant.Work;
        [Tooltip("Single (the console workboat, the punt) or Twin (the sport skiff's upgrade — the SAME sheets blitted twice, both steering off the one wheel).")]
        [SerializeField] private MotorFit _fit = MotorFit.Single;

        [Header("Sheets (heading×column; the builder wires these — index = heading·columns + col)")]
        [Tooltip("Lower layer: leg + cavitation plate + skeg + prop. 72 ordered slices, index = heading×9 + steer col.")]
        [SerializeField] private Sprite[] _lowerFrames;
        [Tooltip("Upper layer: clamp bracket + cowl. Same layout as the lower sheet.")]
        [SerializeField] private Sprite[] _upperFrames;
        [Tooltip("Steer columns per heading row (the shipped motor sheets have 9: col 0 = full port, 4 = dead ahead, 8 = full starboard).")]
        [SerializeField] private int _columnsPerHeading = OutboardMotorMath.SteerColumns;
        [Tooltip("Heading rows in the sheets — must match the hull's facing count (8: N..NW clockwise).")]
        [SerializeField] private int _headingCount = OutboardMotorMath.HeadingCount;
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
        [Tooltip("Legacy wiring: the hull's directional sprite. Kept for scene-serialised rigs the skinner " +
                 "has not reconfigured; at runtime the layer reads the hull through IBoatHullPresenter " +
                 "(ADR 0022 phase 4) and this is only a fallback to wrap. Null + no presenter = the heading " +
                 "falls back to this transform's bow and no rock coupling.")]
        [SerializeField] private DirectionalBoatSprite _directionalSprite;

        // The hull as the seam describes it (ADR 0022 phase 4). Set by Configure; a scene-serialised
        // layer lazily wraps its legacy _directionalSprite field instead.
        private IBoatHullPresenter _presenter;
        [Tooltip("The hull's own renderer — the motor sorts against its sortingOrder, on its sorting layer. Null = the fallback order below is used and the layer is left alone.")]
        [SerializeField] private SpriteRenderer _hullRenderer;
        [Tooltip("Sorting order to sort against when no hull renderer is wired (the hull visual ships at 1).")]
        [SerializeField] private int _fallbackHullSortingOrder = 1;

        [Header("Steer feel (tunable)")]
        [Tooltip("Columns/sec the engine swivels toward the helm's target column (the art README's tempo is ~8). 0 = snap instantly.")]
        [SerializeField] private float _steerColumnsPerSecond = 8f;
        [Tooltip("|helm| at or below which the wheel reads as CENTRED, so a resting stick never trickles the engine off dead ahead.")]
        [SerializeField] private float _helmDeadzone = 0.05f;
        [Tooltip("Steer authority at the sheet's extremes, in degrees either side of dead ahead — an ART FACT " +
                 "of whichever sheets are wired, so the hull's BoatVisualDef supplies it (skiffs ±30° across " +
                 "9 columns = 7.5° steps; the punt ±32° = 8° steps). Changing this re-maps the helm across " +
                 "those SAME columns; it does not re-bake the art. The default is the skiffs' value, for a " +
                 "caller with no hull data.")]
        [SerializeField] private float _maxSteerDegrees = OutboardMotorMath.MaxSteerDegrees;

        [Header("Twin fit (sport skiff — the same sheets blitted twice)")]
        [Tooltip("Lateral clamp offset from the centreline, in METRES. The engines sit at ±this. The art rig's twin spacing is 0.34.")]
        [SerializeField] private float _twinMountMetres = 0.34f;
        [Tooltip("Elevation the sheets were baked at, in degrees — the foreshortening of the twin offset in the ¾ view. 40 matches the art; changing it will misplace the second engine.")]
        [SerializeField] private float _bakeElevationDegrees = OutboardMotorMath.BakeElevationDegrees;

        [Header("Rock coupling (the motor cells are baked LEVEL — this leans them onto the hull)")]
        [Tooltip("Master strength of the whole rock coupling. 0 = the motor sits level on a rocking hull; 1 = the tuned read.")]
        [SerializeField] private float _rockStrength = 1f;
        [Tooltip("Baked heave amplitude in PIXELS. Console 1.3 (heavier hull, stiffer), Sport 1.5 (light glass hull, livelier). Reproduced EXACTLY at the sheet's PPU.")]
        [SerializeField] private float _rockHeavePixels = 1.3f;
        [Tooltip("Pixels-per-unit the motor/hull sheets import at (32) — converts the baked pixel heave into metres.")]
        [SerializeField] private float _pixelsPerUnit = 32f;
        [Tooltip("Degrees of lean at the peak of the roll. Console 3.4, Sport 3.8 (the rigs' rollA).")]
        [SerializeField] private float _rockRollDegrees = 3.4f;
        [Tooltip("Degrees of bow-up/bow-down tip at the peak of the PITCH — the rigs' pitchA, verbatim (console 1.9, sport 2.2, punt 2.4). A ROTATION, not a screen offset: the screen travel it causes is derived from the mount below.")]
        [SerializeField] private float _rockPitchDegrees = 1.9f;
        [Tooltip("Where the clamp hangs, in boat-local metres (x = starboard, y = bow, z = up) — the rigs' MOUNT. Skiffs (0, −3.53, 0.72); punt (0, −2.63, 0.56). The lever arm the whole pose turns on.")]
        [SerializeField] private Vector3 _mountLocalMeters = new Vector3(0f, -3.53f, 0.72f);
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
        /// The helm the engine swivels to, PULLED off the boat: −1 = full port, 0 = dead ahead, +1 = full
        /// starboard. An unmanned helm (<see cref="IsHelmManned"/>) reads dead ahead.
        ///
        /// <para><b>Pull, not push.</b> This layer was born pushing — a settable <c>Helm</c> property some
        /// driving system had to write every frame — only because <see cref="BoatController"/> kept its rudder
        /// state private while exposing <c>LeftOar</c>/<c>RightOar</c>. It now exposes
        /// <see cref="BoatController.Steer"/> symmetrically, so the layer sources the wheel itself. A pushed
        /// copy is only ever as good as the last system that remembered to write it — the dropped-state blind
        /// spot #205 fixed for the oars; a pull cannot go stale. Read-only: the layer never writes the sim
        /// (rule 5).</para>
        /// </summary>
        public float Helm => IsHelmManned ? _boat.Steer : 0f;

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
        public int SteerColumn { get; private set; } = OutboardMotorMath.CenterColumn(OutboardMotorMath.SteerColumns);

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
        /// <see cref="MotorFit.Twin"/> fit (the sport skiff); Single ignores them.
        ///
        /// <para><paramref name="maxSteerDegrees"/> travels WITH the sheets because it describes them: the
        /// columns are drawn at fixed angles, and the number is what says which. It was a bare serialized 30
        /// until the punt arrived baking ±32 — a hard-coded authority would have drawn her engine 2° shy of
        /// the hard-over her own art holds, on every heading, forever. Non-positive = keep the serialized
        /// default, so a half-authored visual degrades to the skiffs' ±30 rather than to an engine that can
        /// no longer leave dead ahead.</para>
        /// </summary>
        public void Configure(Sprite[] lowerFrames, Sprite[] upperFrames,
                              SpriteRenderer lowerA, SpriteRenderer upperA,
                              SpriteRenderer lowerB, SpriteRenderer upperB,
                              BoatController boat, IBoatHullPresenter hull,
                              SpriteRenderer hullRenderer,
                              MotorVariant variant, MotorFit fit,
                              int headingCount, int columnsPerHeading,
                              float maxSteerDegrees = OutboardMotorMath.MaxSteerDegrees)
        {
            _lowerFrames = lowerFrames;
            _upperFrames = upperFrames;
            _lowerA = lowerA;
            _upperA = upperA;
            _lowerB = lowerB;
            _upperB = upperB;
            _boat = boat;
            _presenter = hull;
            _hullRenderer = hullRenderer;
            _variant = variant;
            _fit = fit;
            _headingCount = Mathf.Max(1, headingCount);
            _columnsPerHeading = Mathf.Max(1, columnsPerHeading);
            if (maxSteerDegrees > 0f) _maxSteerDegrees = maxSteerDegrees;
            _baseCached = false;
        }

        /// <summary>Steer authority at the wired sheet's extremes, in degrees either side of dead ahead
        /// (diagnostics / tests). ±30 on the skiffs, ±32 on the punt — an art fact of the sheets, supplied by
        /// the hull's <see cref="BoatVisualDef"/>.</summary>
        public float MaxSteerDegrees => _maxSteerDegrees;

        /// <summary>
        /// Wire the ROCK COUPLING from the hull's own data (the skinner's path) — the amplitudes that pose
        /// this LEVEL-baked engine onto the hull's baked rock. They are per-hull ART FACTS (the console is a
        /// heavier, stiffer boat than the sport), which is why they arrive from the
        /// <see cref="BoatVisualDef"/> rather than living as consts here: two hulls share this one component
        /// and must lean differently.
        ///
        /// <para>Split from <see cref="Configure"/> deliberately — a caller with no per-hull rock data (a
        /// test rig, a decor boat) gets the serialized defaults, which are the console's. Non-positive
        /// values are ignored rather than zeroing the coupling, so a half-authored asset degrades to the
        /// default lean instead of a dead-level engine on a rocking hull.</para>
        /// </summary>
        public void ConfigureRock(float rollDegrees, float pitchDegrees, float heavePixels, int rockFrameCount,
                                  Vector3 mountLocalMeters, float bakeElevationDegrees)
        {
            if (rollDegrees > 0f) _rockRollDegrees = rollDegrees;
            if (pitchDegrees > 0f) _rockPitchDegrees = pitchDegrees;
            if (heavePixels > 0f) _rockHeavePixels = heavePixels;
            if (rockFrameCount > 0) _rockFrameCount = rockFrameCount;
            // A zeroed mount is never authored — it would put the clamp on the boat's own origin, amidships at
            // the waterline, and quietly flatten the lever arm the pose is built on. Ignore it, like the rest.
            if (mountLocalMeters.sqrMagnitude > 1e-6f) _mountLocalMeters = mountLocalMeters;
            // The bake elevation is the sheets' camera. 40 on every kit shipped; a stale asset deserialises 0
            // (the #212 trap) and 0 would collapse the projection, so non-positive keeps the serialized value.
            if (bakeElevationDegrees > 0f) _bakeElevationDegrees = bakeElevationDegrees;
        }

        private void OnEnable()
        {
            // Wake dead ahead and level — never on a stale hard-over, never a frozen lean.
            _steerPosition = OutboardMotorMath.CenterColumn(_columnsPerHeading);
            SteerColumn = OutboardMotorMath.CenterColumn(_columnsPerHeading);
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
            // empty skiff. Helm already folds that gate in, reading dead ahead when nobody is driving.
            float helm = Helm;

            float dt = Time.deltaTime;
            int target = OutboardMotorMath.TargetColumnForHelm(helm, _helmDeadzone, _columnsPerHeading, _maxSteerDegrees);
            _steerPosition = OutboardMotorMath.StepTowardColumn(_steerPosition, target, _steerColumnsPerSecond, dt);
            SteerColumn = OutboardMotorMath.ColumnFromPosition(_steerPosition, _columnsPerHeading);

            int row = HeadingRow();
            HeadingRowDrawn = row;

            int rockFrame = RockFrame();
            int hullOrder = _hullRenderer != null ? _hullRenderer.sortingOrder : _fallbackHullSortingOrder;

            if (_fit == MotorFit.Twin)
            {
                float portMount = -_twinMountMetres;
                float starMount = +_twinMountMetres;
                // Which engine is FARTHER swaps as she turns — re-decided every frame, never assumed.
                bool portIsFar = OutboardMotorMath.IsFarEngine(portMount, starMount, row, _headingCount);

                DrawEngine(_lowerA, _upperA, _lowerABaseLocalPosition, _upperABaseLocalPosition,
                           row, rockFrame, hullOrder, portMount, portIsFar);
                DrawEngine(_lowerB, _upperB, _lowerBBaseLocalPosition, _upperBBaseLocalPosition,
                           row, rockFrame, hullOrder, starMount, !portIsFar);
            }
            else
            {
                // Single engine, on the centreline: no clamp shift, and it is trivially the near one.
                DrawEngine(_lowerA, _upperA, _lowerABaseLocalPosition, _upperABaseLocalPosition,
                           row, rockFrame, hullOrder, 0f, false);
            }
        }

        /// <summary>
        /// The sheet ROW the motor indexes: the SAME pure snap the hull's picture used this frame. Taking the
        /// hull's already-DRAWN (quantized) heading and re-snapping it is idempotent, so the motor lands on the
        /// hull's row by construction — it can never show a different heading than the transom under it. With no
        /// directional sprite wired, this transform's bow stands in (the same convention).
        ///
        /// <para>The MIRROR travels with it: the motor sheets are baked by the same rig as the hull, so they
        /// share its counter-clockwise cell order and must be mirrored the same way
        /// (<see cref="DirectionalBoatSprite.FacingsAreCounterClockwise"/>) — otherwise the engine would hang
        /// off the bow of a hull drawn from the mirrored cell. Note the row is all that flips: MountOffset,
        /// the LowerGoesUnderHull band and the rock pose are all correct in CELL space already.</para>
        /// </summary>
        private int HeadingRow()
        {
            var hull = Hull;
            float heading = hull != null
                ? hull.DrawnHeadingDegrees()
                : DirectionalBoatSprite.HeadingDegreesFromBow(transform.up);
            bool ccw = hull != null && hull.FacingsAreCounterClockwise;
            return DirectionalBoatSprite.HeadingToFacingIndex(heading, _headingCount, _zeroHeadingDegrees, ccw);
        }

        /// <summary>The hull presenter this layer reads — the configured one, or a lazy wrap of the
        /// legacy serialized <see cref="DirectionalBoatSprite"/> (scene-serialised rigs). May be null.</summary>
        private IBoatHullPresenter Hull =>
            _presenter ??= (_directionalSprite != null ? new SpriteHullPresenter(_directionalSprite) : null);

        /// <summary>The hull's currently-drawn rock frame — the wave the engine must ride. −1 (level) when the
        /// hull is drawing its static facing or has no rock grid at all, so a calm hull never gets a moving
        /// engine.</summary>
        private int RockFrame()
        {
            var hull = Hull;
            return hull != null && hull.HasRockGrid
                ? hull.RockFrame
                : OutboardMotorMath.LevelRockFrame;
        }

        /// <summary>
        /// The pose that rides ONE level-baked engine on the hull's currently-drawn rock frame — derived by
        /// <see cref="MountedRockPoseMath.Pose"/> from this kit's MOUNT POINT and bake elevation, at this
        /// heading.
        ///
        /// <para><b>This used to call <see cref="DoryOarMath.RockPose"/> and it was wrong four ways.</b> That
        /// function takes pitch as a pre-baked screen offset, and the value handed to it was the dory's
        /// hand-tuned 0.02 m linearly rescaled by pitchA — which threw away the one thing that actually decides
        /// the answer, the mount's lever arm. The dory's oarlocks sit near amidships; the outboard hangs ~3.5 m
        /// aft. So the engine got ~1/8 of the travel it needed, in the WRONG DIRECTION (a positive pitch is
        /// bow-UP, which puts the stern — and everything clamped to it — DOWN), no lateral travel at all, and a
        /// roll that was constant at every heading when the truth swings from full at N/S to nothing at E/W. The
        /// error was bigger than the signal: the engine was in ANTI-PHASE with the transom it hangs on, which is
        /// exactly the "bounces independently" the owner reported.</para>
        ///
        /// <para>The dory's oars keep <see cref="DoryOarMath.RockPose"/> deliberately — see
        /// <see cref="MountedRockPoseMath"/>. Their approximation is close where their oarlocks are, and the
        /// owner has a standing verdict that the rowing feels right.</para>
        /// </summary>
        private MountedRockPoseMath.MountRockPose RockPose(int row, int rockFrame, float lateralMetres)
            => MountedRockPoseMath.Pose(rockFrame, _rockFrameCount, row, _headingCount,
                                        _mountLocalMeters, lateralMetres, _bakeElevationDegrees,
                                        _rockRollDegrees, _rockPitchDegrees, _rockHeavePixels,
                                        _pixelsPerUnit, _rockStrength);

        /// <summary>Draw one engine's two layers: the steer frame, the per-heading sorting order (the lower
        /// layer's band flips across the stern-away headings), and the pose. The pose is derived PER ENGINE
        /// because it is derived from the mount, and a twin's two engines hang at different mounts — it carries
        /// the rock AND the clamp shift in one, so there is nothing left to add on top.</summary>
        private void DrawEngine(SpriteRenderer lower, SpriteRenderer upper,
                                Vector3 lowerBase, Vector3 upperBase,
                                int row, int rockFrame, int hullOrder, float mountMetres, bool isFar)
        {
            Draw(lower, _lowerFrames, row, SteerColumn);
            Draw(upper, _upperFrames, row, SteerColumn);

            lower.sortingOrder = OutboardMotorMath.SortingOrder(
                hullOrder, OutboardMotorMath.MotorPart.Lower, row, _headingCount, isFar);
            upper.sortingOrder = OutboardMotorMath.SortingOrder(
                hullOrder, OutboardMotorMath.MotorPart.Upper, row, _headingCount, isFar);

            if (_hullRenderer != null)
            {
                lower.sortingLayerID = _hullRenderer.sortingLayerID;
                upper.sortingLayerID = _hullRenderer.sortingLayerID;
            }

            // ONE pose per engine: the rock travel of ITS mount, plus its clamp shift off the centreline — the
            // level case collapses to exactly OutboardMotorMath.MountOffset (and to zero for a single engine),
            // which MountedRockPoseTests pins as a cross-check between the two derivations.
            MountedRockPoseMath.MountRockPose pose = RockPose(row, rockFrame, mountMetres);
            var offset = new Vector3(pose.Offset.x, pose.Offset.y, 0f);
            var lean = Quaternion.Euler(0f, 0f, pose.RollDegrees);

            lower.transform.localPosition = lowerBase + offset;
            lower.transform.localRotation = lean;
            upper.transform.localPosition = upperBase + offset;
            upper.transform.localRotation = lean;
        }

        private void Draw(SpriteRenderer renderer, Sprite[] frames, int row, int column)
        {
            int index = OutboardMotorMath.MotorGridIndex(row, column, _columnsPerHeading);
            if (index < 0 || index >= frames.Length) return;
            Sprite target = frames[index];
            // Direct sprite compare (not a cached index): a heading OR column change refreshes with no
            // allocation, and an unchanged frame skips the assignment entirely.
            if (target != null && renderer.sprite != target) renderer.sprite = target;
        }
    }
}
