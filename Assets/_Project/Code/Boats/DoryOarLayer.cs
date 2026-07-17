using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// THE DORY ROWS — the iso dory's two INDEPENDENT oars, drawn as baked overlays on the hull picture and
    /// animated from the boat's REAL per-oar state (<see cref="BoatController.LeftOar"/> /
    /// <see cref="BoatController.RightOar"/>). Port pulls while starboard backs and the picture shows exactly
    /// that: each side owns its own stroke phase, so the owner's rowing scheme (W = both ahead, A = port ahead
    /// + starboard back, W+A = port only, …) reads straight off the water.
    ///
    /// <para><b>What it draws.</b> Per side, per frame: the heading ROW comes from the very same pure snap the
    /// hull uses (<see cref="DirectionalBoatSprite.HeadingToFacingIndex"/> on the hull's DRAWN heading), so the
    /// oars can never disagree with the facing under them; the COLUMN comes from that oar's state —
    /// <list type="bullet">
    ///   <item>forward pull → the stroke cycle, cols 0→7 at <see cref="_strokeFramesPerSecond"/>;</item>
    ///   <item>back-water → the SAME cycle in REVERSE, 7→0 (the art's design — reversed playback IS the
    ///   backing stroke, there is no separate set of cells);</item>
    ///   <item>idle while the other oar works → trailing (col 9), dragging in the water;</item>
    ///   <item>both idle past <see cref="_restGraceSeconds"/> → resting/shipped (col 8).</item>
    /// </list>
    /// A mid-stroke reversal continues from the current phase and sweeps back the way it came — see
    /// <see cref="DoryOarMath.AdvanceStrokePhase"/>.</para>
    ///
    /// <para><b>Why it parents to the hull's visual.</b> <see cref="DirectionalBoatSprite"/> force-resets its
    /// renderer's rotation to screen-identity every LateUpdate (the snap: the picture never rotates, it only
    /// changes). Hanging the oar renderers UNDER that child means they inherit the identical treatment for
    /// free — they can never smooth-rotate while the hull snaps — and, because the oar sheets share the hull's
    /// cell and waterline pivot, they register pixel-perfect at localPosition zero.</para>
    ///
    /// <para><b>Rock coupling.</b> The hull's rock is BAKED into its frames (#202) but the oar cells are drawn
    /// rock-free, so a rocking hull would otherwise leave its oars behind. Each frame the currently-drawn rock
    /// frame (<see cref="DirectionalBoatSprite.RockFrame"/>, which <see cref="BoatWaveMotion"/> selects from
    /// the wave under the hull) is turned into a small pose — heave EXACT, roll/pitch approximated — by
    /// <see cref="DoryOarMath.RockPose"/>. A calm/level hull (RockFrame −1) puts the oars back level.</para>
    ///
    /// <para><b>A dropped helm ships the oars.</b> The state is read only while the
    /// <see cref="BoatController"/> is enabled, so a boat left moored, trailing on its rope, or waiting while
    /// the player works the deck never rows itself — see the note in <c>LateUpdate</c> for why trusting
    /// <c>BoatController.Stop()</c> to have cleared it isn't enough.</para>
    ///
    /// <para><b>Rules.</b> Visual-only: it READS the oar state the controller already computed and writes
    /// nothing back (rule 5), and there is no RNG. Every rate/threshold/amplitude is serialized (rule 6). Two
    /// renderers, two array look-ups and no allocation per LateUpdate (rule 7). Boats-internal — no other
    /// module is touched (rule 4). <b>Scope: the player's dory (T0) only</b> — the Punt, the frozen T2+ hulls
    /// and the ambient fleet are untouched.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class DoryOarLayer : MonoBehaviour
    {
        [Header("Sheets (heading×column; the builder wires these — index = heading·columns + col)")]
        [Tooltip("Port-side oar sheet: 80 ordered slices, index = heading×10 + col (cols 0-7 stroke, 8 resting, 9 trailing).")]
        [SerializeField] private Sprite[] _portFrames;
        [Tooltip("Starboard-side oar sheet, same layout as the port sheet.")]
        [SerializeField] private Sprite[] _starFrames;
        [Tooltip("Columns per heading row in the sheets (the shipped oar sheets have 10).")]
        [SerializeField] private int _columnsPerHeading = DoryOarMath.ColumnsPerHeading;
        [Tooltip("Heading rows in the sheets — must match the hull's facing count (8: N..NW clockwise).")]
        [SerializeField] private int _headingCount = 8;
        [Tooltip("The compass heading (degrees, 0 = North, CW) row 0 is drawn for. 0 — same as the hull's facings.")]
        [SerializeField] private float _zeroHeadingDegrees = 0f;

        [Header("Wiring (the builder sets these)")]
        [Tooltip("The port oar's renderer — a CHILD of the hull's visual, so it inherits the hull's snap treatment.")]
        [SerializeField] private SpriteRenderer _portRenderer;
        [Tooltip("The starboard oar's renderer — same parent, drawn above the port oar (art README's order).")]
        [SerializeField] private SpriteRenderer _starRenderer;
        [Tooltip("The boat whose REAL per-oar state (LeftOar = port, RightOar = starboard) drives the strokes. Read only while the controller is enabled — a dropped helm ships the oars. Null = the component idles.")]
        [SerializeField] private BoatController _boat;
        [Tooltip("The hull's directional sprite: the source of the DRAWN heading (so the oars pick the hull's row) and of the baked rock frame the oars ride. Null = the heading falls back to this transform's bow and no rock coupling.")]
        [SerializeField] private DirectionalBoatSprite _directionalSprite;

        [Header("Stroke feel (tunable)")]
        [Tooltip("Frames/sec the 8-frame stroke cycle plays at a full-effort pull (the art README's tempo is ~9).")]
        [SerializeField] private float _strokeFramesPerSecond = 9f;
        [Tooltip("|oar state| at or below which the oar is idle (trailing/shipped) rather than stroking.")]
        [SerializeField] private float _oarDeadzone = 0.05f;
        [Tooltip("How much a GENTLE pull slows the sweep: 0 = every stroke runs at the full tempo; 1 = the rate is proportional to effort. 0.5 = a half-effort pull strokes at three-quarter speed.")]
        [Range(0f, 1f)]
        [SerializeField] private float _effortInfluence = 0.5f;
        [Tooltip("Seconds BOTH oars must be idle before they ship (col 8). Below this they trail (col 9), so a brief pause between strokes doesn't stow them.")]
        [SerializeField] private float _restGraceSeconds = 1.2f;

        [Header("Rock coupling (the oar cells are baked ROCK-FREE — this leans them onto the hull)")]
        [Tooltip("Master strength of the whole rock coupling. 0 = the oars sit level on a rocking hull; 1 = the tuned read.")]
        [SerializeField] private float _rockStrength = 1f;
        [Tooltip("Baked heave amplitude in PIXELS (art README: 1.6 px·sin(a)). Reproduced EXACTLY at the sheet's PPU — the oars rise and fall with the gunwale to the pixel.")]
        [SerializeField] private float _rockHeavePixels = 1.6f;
        [Tooltip("Pixels-per-unit the oar/hull sheets import at (32) — converts the baked pixel heave into metres.")]
        [SerializeField] private float _pixelsPerUnit = 32f;
        [Tooltip("Degrees of oar lean at the peak of the roll — the tunable approximation of the hull's baked 5°·sin(a) roll.")]
        [SerializeField] private float _rockRollDegrees = 5f;
        [Tooltip("Screen-vertical metres at the peak of the PITCH (the baked 3°·cos(a), read as vertical travel in ¾ view). Keep small.")]
        [SerializeField] private float _rockPitchOffsetMeters = 0.02f;
        [Tooltip("Rock frames per heading in the hull's rock sheet (DoryIsoRock ships 8) — the cycle the coupling reads.")]
        [SerializeField] private int _rockFrameCount = 8;

        // Per-oar phase accumulators — the oars are INDEPENDENT, so they never share one.
        private float _portPhase;
        private float _starPhase;
        private float _bothIdleSeconds;

        // Cached base pose of each oar renderer (they sit at the hull's registration; the rock pose is added
        // ON it). Cached on the first tick so the builder's wiring is what we return to.
        private bool _baseCached;
        private Vector3 _portBaseLocalPosition;
        private Vector3 _starBaseLocalPosition;

        /// <summary>True when both sheets and both renderers are wired with matching, full heading×column
        /// grids — the gate the whole component runs behind. False = it draws nothing (null-safe: missing or
        /// partially-sliced art leaves no half-state).</summary>
        public bool IsWired =>
            _portRenderer != null && _starRenderer != null &&
            _headingCount > 0 && _columnsPerHeading > 0 &&
            _portFrames != null && _portFrames.Length == _headingCount * _columnsPerHeading &&
            _starFrames != null && _starFrames.Length == _headingCount * _columnsPerHeading;

        /// <summary>The column the PORT oar drew last tick (diagnostics / tests).</summary>
        public int PortColumn { get; private set; } = DoryOarMath.RestingColumn;

        /// <summary>The column the STARBOARD oar drew last tick (diagnostics / tests).</summary>
        public int StarboardColumn { get; private set; } = DoryOarMath.RestingColumn;

        /// <summary>
        /// Wire the layer from code (the builder's path — mirrors <see cref="DirectionalBoatSprite.Configure"/>
        /// so a non-dev owner needn't touch the Inspector). The sheets must be the FULL ordered
        /// heading×column grids; anything else leaves <see cref="IsWired"/> false and the component inert.
        /// </summary>
        public void Configure(Sprite[] portFrames, Sprite[] starFrames,
                              SpriteRenderer portRenderer, SpriteRenderer starRenderer,
                              BoatController boat, DirectionalBoatSprite directionalSprite,
                              int headingCount, int columnsPerHeading)
        {
            _portFrames = portFrames;
            _starFrames = starFrames;
            _portRenderer = portRenderer;
            _starRenderer = starRenderer;
            _boat = boat;
            _directionalSprite = directionalSprite;
            _headingCount = Mathf.Max(1, headingCount);
            _columnsPerHeading = Mathf.Max(1, columnsPerHeading);
            _baseCached = false;
        }

        private void OnEnable()
        {
            // Wake shipped and level — never mid-stroke on a stale phase, never a frozen lean.
            _portPhase = 0f;
            _starPhase = 0f;
            _bothIdleSeconds = _restGraceSeconds;
        }

        private void LateUpdate()
        {
            if (!IsWired) return;

            if (!_baseCached)
            {
                _portBaseLocalPosition = _portRenderer.transform.localPosition;
                _starBaseLocalPosition = _starRenderer.transform.localPosition;
                _baseCached = true;
            }

            float dt = Time.deltaTime;

            // NOBODY AT THE HELM = NOBODY ROWING. The oar state is only read while the controller is actually
            // driving: BoatController.Stop() clears it on SOME helm-drop paths but not all (ControlSwitcher's
            // normal Disembark hands the rope to Mooring.Hold and only falls back to Stop() when there's no
            // mooring; stepping OnDeck doesn't stop either), so a player who disembarks mid-stroke would leave
            // LeftOar/RightOar pinned at their last value — and an empty boat would row itself across the
            // harbour. Gating on the controller instead of trusting the state to be cleared keeps this
            // visual-only (we never write to the sim) and is right by construction: a dropped helm ships the
            // oars after the usual grace.
            bool helmManned = _boat != null && _boat.isActiveAndEnabled;
            float portState = helmManned ? _boat.LeftOar : 0f;      // port = the LEFT oar
            float starState = helmManned ? _boat.RightOar : 0f;     // starboard = the RIGHT oar

            bool portWorking = DoryOarMath.IsWorking(portState, _oarDeadzone);
            bool starWorking = DoryOarMath.IsWorking(starState, _oarDeadzone);
            _bothIdleSeconds = (portWorking || starWorking) ? 0f : _bothIdleSeconds + dt;

            // Each oar advances its OWN phase, signed by its OWN state (forward +, back-water −). An idle oar
            // holds its phase, so picking the stroke back up resumes mid-sweep.
            _portPhase = DoryOarMath.AdvanceStrokePhase(
                _portPhase, DoryOarMath.StrokeDirection(portState, _oarDeadzone),
                DoryOarMath.StrokeFramesPerSecond(_strokeFramesPerSecond, portState, _effortInfluence),
                dt, DoryOarMath.StrokeColumns);
            _starPhase = DoryOarMath.AdvanceStrokePhase(
                _starPhase, DoryOarMath.StrokeDirection(starState, _oarDeadzone),
                DoryOarMath.StrokeFramesPerSecond(_strokeFramesPerSecond, starState, _effortInfluence),
                dt, DoryOarMath.StrokeColumns);

            PortColumn = DoryOarMath.ColumnForOar(portWorking, _portPhase, starWorking,
                                                  _bothIdleSeconds, _restGraceSeconds, DoryOarMath.StrokeColumns);
            StarboardColumn = DoryOarMath.ColumnForOar(starWorking, _starPhase, portWorking,
                                                       _bothIdleSeconds, _restGraceSeconds, DoryOarMath.StrokeColumns);

            int row = HeadingRow();
            Draw(_portRenderer, _portFrames, row, PortColumn);
            Draw(_starRenderer, _starFrames, row, StarboardColumn);

            ApplyRockPose();
        }

        /// <summary>
        /// The sheet ROW both oars index: the SAME pure snap the hull's picture used this frame. Taking the
        /// hull's already-DRAWN (quantized) heading and re-snapping it is idempotent, so the oars land on the
        /// hull's row by construction — they can never show a different heading than the boat under them. With
        /// no directional sprite wired, this transform's bow stands in (the same convention).
        ///
        /// <para>The MIRROR travels with it: the oar sheets are baked by the same rig as the hull, so they
        /// share its counter-clockwise cell order and must be mirrored the same way
        /// (<see cref="DirectionalBoatSprite.FacingsAreCounterClockwise"/>). Snapping the drawn heading stays
        /// idempotent because that heading is the TRUE quantized one — only the cell lookup flips.</para>
        /// </summary>
        private int HeadingRow()
        {
            float heading = _directionalSprite != null
                ? _directionalSprite.DrawnHeadingDegrees()
                : DirectionalBoatSprite.HeadingDegreesFromBow(transform.up);
            bool ccw = _directionalSprite != null && _directionalSprite.FacingsAreCounterClockwise;
            return DirectionalBoatSprite.HeadingToFacingIndex(heading, _headingCount, _zeroHeadingDegrees, ccw);
        }

        private void Draw(SpriteRenderer renderer, Sprite[] frames, int row, int column)
        {
            int index = DoryOarMath.OarGridIndex(row, column, _columnsPerHeading);
            if (index < 0 || index >= frames.Length) return;
            Sprite target = frames[index];
            // Direct sprite compare (not a cached index): a heading OR column change refreshes with no
            // allocation, and an unchanged frame skips the assignment entirely.
            if (target != null && renderer.sprite != target) renderer.sprite = target;
        }

        /// <summary>
        /// Lean the rock-free oar cells onto the hull's currently-drawn (rock-baked) frame. Local space is the
        /// hull's visual child, which <see cref="DirectionalBoatSprite"/> holds at world-identity rotation in
        /// snap mode — so local +Y is screen-up and a local offset/rotation IS the screen-space pose, with no
        /// dependence on which component ran first this frame.
        /// </summary>
        private void ApplyRockPose()
        {
            int rockFrame = _directionalSprite != null && _directionalSprite.HasRockGrid
                ? _directionalSprite.RockFrame
                : DoryOarMath.LevelRockFrame;

            DoryOarMath.OarRockPose pose = DoryOarMath.RockPose(
                rockFrame, _rockFrameCount, _rockRollDegrees, _rockPitchOffsetMeters,
                _rockHeavePixels, _pixelsPerUnit, _rockStrength);

            var lift = new Vector3(0f, pose.OffsetY, 0f);
            var lean = Quaternion.Euler(0f, 0f, pose.RollDegrees);

            _portRenderer.transform.localPosition = _portBaseLocalPosition + lift;
            _portRenderer.transform.localRotation = lean;
            _starRenderer.transform.localPosition = _starBaseLocalPosition + lift;
            _starRenderer.transform.localRotation = lean;
        }
    }
}
