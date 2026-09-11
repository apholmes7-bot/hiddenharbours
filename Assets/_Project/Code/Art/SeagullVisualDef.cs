using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>THE SEAGULL'S ART TABLE AND ITS BEHAVIOUR NUMBERS, IN ONE ASSET (rule 2).</b>
    ///
    /// <para>384 baked cells — 8 facings × 48 columns — plus the numbers
    /// <c>docs/art/rigs/gameplay/seagullIsoRig.gameplay.json</c> declares about how a gull moves. Both
    /// halves are DERIVED: <c>SeagullVisualDefBuilder</c> regenerates this asset from the sliced sheet
    /// and the sidecar, and <see cref="DerivedFromRigSha256"/> pins which rig they came from. Nobody
    /// hand-edits it; the owner retunes birds by re-exporting the rig.</para>
    ///
    /// <para><b>Why an asset and not a loose <c>Sprite[]</c>.</b> A bare array cannot carry the facts
    /// that make its cells usable — the pivot is a CONTACT POINT, the facings are CLOCKWISE (this rig
    /// is the minority convention in this repo), 32 px is one metre, and altitude is a screen offset
    /// rather than a scale. Those travel with the cells or they get guessed wrong.</para>
    ///
    /// <para><b>⚠️ Sprite references resolve by the slice's <c>internalID</c>.</b> A re-slice that
    /// renumbers the cells must be followed by a rebuild of this asset, or a bird silently draws the
    /// wrong pose. <c>SeagullVisualDefTests</c> checks all 384 resolve.</para>
    ///
    /// <para>Pure content metadata: never serialized into a save, no determinism concern. Lives in
    /// <c>Resources</c> so the presenter loads it with no scene wiring — the
    /// <c>FishSwimSpriteLibrary</c> pattern.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SeagullVisualDef",
                     menuName = "Hidden Harbours/Art/Seagull Visual Def")]
    public sealed class SeagullVisualDef : ScriptableObject
    {
        /// <summary>Resources path (no extension) the presenter loads the def from.</summary>
        public const string ResourcesPath = "SeagullVisualDef";

        // ── one strip of the sheet ──────────────────────────────────────────────────────────────

        /// <summary>Where one state's frames start on the page, and how many there are — the
        /// contract's <c>order</c> table, collapsed to one row per state.</summary>
        [Serializable]
        public sealed class StripEntry
        {
            [Tooltip("Sidecar / sheet state id: fly, glide, swoop, dive, splash, float, preen, peck, land, stand, walk, perch, takeoff")]
            public string State;

            [Tooltip("First column of this state's frames on the page")]
            public int Column;

            [Tooltip("Frames in the strip")]
            public int Frames;
        }

        /// <summary>One row of the sidecar's <c>STATES</c> table, in a shape Unity can serialize.
        /// <see cref="SeagullGameplay"/> is engine-light and its lists are readonly, so it cannot be a
        /// serialized field — this is its mirror, carrying only what the runtime reads.</summary>
        [Serializable]
        public sealed class StateEntry
        {
            public string State;
            [Tooltip("Frames in the strip (the sidecar's frames)")] public int Frames;
            [Tooltip("Milliseconds a frame is held (ms)")] public float FrameMilliseconds;
            [Tooltip("Ground speed in the state (v, m/s)")] public float SpeedMetresPerSecond;
            [Tooltip("Climb rate (vz, m/s) — negative descends")] public float ClimbMetresPerSecond;
            [Tooltip("The sidecar's alt[0], raw")] public float AltitudeA;
            [Tooltip("The sidecar's alt[1], raw")] public float AltitudeB;
            public bool Loop;
            [Tooltip("Chain target for a oneshot (next); empty for a looping state")] public string Next;
            [Tooltip("Ground metres the state carries the bird (travel)")] public float TravelMetres;
            [Tooltip("Loops before handing back (cycles); 0 = loops forever")] public int CyclesMin, CyclesMax;
        }

        /// <summary>One declared edge of <c>TRANSITIONS</c>.</summary>
        [Serializable]
        public sealed class EdgeEntry
        {
            public string From;
            public string To;
        }

        // ── the sheet ───────────────────────────────────────────────────────────────────────────

        [Header("Sheet")]
        [SerializeField, Tooltip("Facing rows on the page (the rig's 8-way turntable)")]
        private int _directions = 8;

        [SerializeField, Tooltip("Columns on the page — the sum of every strip's frames")]
        private int _columns = 48;

        [SerializeField, Tooltip("Cell size in pixels")]
        private int _cellWidth = 64, _cellHeight = 64;

        [SerializeField, Tooltip("Pivot in TOP-LEFT pixel coordinates, as the contract states it")]
        private int _pivotTopLeftX = 32, _pivotTopLeftY = 46;

        [SerializeField, Tooltip("Pixels per world unit — 32 px = 1 m, at EVERY altitude")]
        private float _pixelsPerUnit = 32f;

        [SerializeField, Tooltip("⚠️ FALSE for this rig: the seagull turntable is CLOCKWISE, so sheet row r is facing r with no remap")]
        private bool _facingsAreCounterClockwise;

        [SerializeField, Tooltip("Which rig these cells and numbers were baked from")]
        private string _derivedFromRigSha256 = string.Empty;

        [SerializeField, Tooltip("Where each state's frames start on the page")]
        private List<StripEntry> _strips = new List<StripEntry>();

        [SerializeField, Tooltip("The page, indexed [row * Columns + column] — 8 rows of 48")]
        private List<Sprite> _cells = new List<Sprite>();

        // ── the creature ────────────────────────────────────────────────────────────────────────

        [Header("Creature (metres)")]
        [SerializeField] private float _lengthMetres = 0.6f;
        [SerializeField, Tooltip("1.40 m — 45 px at 32 px/m, on the wharf and 25 m up alike")]
        private float _wingspanMetres = 1.4f;
        [SerializeField] private float _massKg = 1f;
        [SerializeField] private float _draftMetres = 0.05f;
        [SerializeField] private float _bodyCentreZStand = 0.19f, _bodyCentreZPerch = 0.165f, _bodyCentreZFloat = 0.035f;
        [SerializeField] private Vector2 _footprintStand = new Vector2(0.16f, 0.3f);
        [SerializeField] private Vector2 _footprintWingsOpen = new Vector2(1.4f, 0.4f);

        // ── behaviour ───────────────────────────────────────────────────────────────────────────

        [Header("Behaviour (from the sidecar — do not hand-edit)")]
        [SerializeField] private List<StateEntry> _states = new List<StateEntry>();
        [SerializeField] private List<EdgeEntry> _transitions = new List<EdgeEntry>();

        [Header("LAND")]
        [SerializeField, Tooltip("Clear box the bird needs, metres: across x along. 1.4 is the WINGSPAN")]
        private Vector2 _landNeedsClear = new Vector2(1.4f, 0.6f);
        [SerializeField] private bool _landApproachIntoWind = true;
        [SerializeField] private float _landMinFlatMetres = 0.3f;

        [Header("WATER")]
        [SerializeField] private float _floatBodyZMetres = 0.035f;
        [SerializeField] private int _splashBurstFrame = 2;
        [SerializeField] private bool _driftWithCurrent = true;
        [SerializeField, Tooltip("The fleet ROCK pose, capped for a bird — a gull rides higher than a hull")]
        private float _rockRollDegreesMax = 2.4f;
        [SerializeField] private float _rockHeavePixelsMax = 1f;

        [Header("FLOCK")]
        [SerializeField] private int _flockSizeMin = 3, _flockSizeMax = 9;
        [SerializeField] private float _flockRadiusMetres = 6f;
        [SerializeField] private float _flockAltitudeMinMetres = 3f, _flockAltitudeMaxMetres = 14f;
        [SerializeField] private float _flockPeriodMinSeconds = 7f, _flockPeriodMaxSeconds = 14f;
        [SerializeField] private float _flockGlideDuty = 0.45f;
        [SerializeField] private float _flockSwoopEveryMinSeconds = 4f, _flockSwoopEveryMaxSeconds = 9f;
        [SerializeField] private float _flockSpacingMetres = 1.6f;
        [SerializeField] private float _flockAttractRadiusMetres = 40f;
        [SerializeField] private float _flockFleeRadiusMetres = 2.5f;
        [SerializeField] private float _flockSettleAfterSeconds = 20f;
        [SerializeField, Tooltip("The sidecar's flee.regroup_s — also the ramp a bird takes to rejoin the wheel after a takeoff")]
        private float _flockRegroupSeconds = 8f;

        [Header("GULL -> FISH (the drop's dive yield, and its one wireable attractor)")]
        [SerializeField, Range(0f, 1f), Tooltip(
            "The drop's WATER dive yield, README: p 0.35 (herring / mackerel). How often a strike onto " +
            "a shoal showing at the surface comes up with a fish, so the bird carries it off instead of " +
            "settling to float.\n\nA PICTURE and nothing more: no player inventory, no market and no " +
            "counter reads this. 0 turns the yield off entirely.")]
        private float _diveCatchProbability = 0.35f;

        [SerializeField, Range(0f, 1f), Tooltip(
            "The drop's FLOCK attractor `shoal_surface`, weight 0.5 (source: FishIso2.shoal at z > -0.2) " +
            "— how hard a chosen landing spot is pulled toward fish showing at the surface, inside the " +
            "flock's own attract radius. 0 = the birds ignore the fish entirely.\n\nIt is the only one " +
            "of the drop's five attractors this game can honour today; gutting, the open tub, the trawler " +
            "wake and the bait bucket name things that do not exist yet and are deliberately NOT wired.")]
        private float _shoalAttractWeight01 = 0.5f;

        [NonSerialized] private SeagullBehaviour _behaviour;
        [NonSerialized] private Dictionary<string, StripEntry> _stripByState;

        // ── art facts ───────────────────────────────────────────────────────────────────────────

        /// <summary>The drop's dive yield — see the field tooltip. A picture, never an economy.</summary>
        public float DiveCatchProbability => _diveCatchProbability;

        /// <summary>The drop's `shoal_surface` attractor weight — see the field tooltip.</summary>
        public float ShoalAttractWeight01 => _shoalAttractWeight01;

        public int Directions => _directions;
        public int Columns => _columns;
        public int CellWidth => _cellWidth;
        public int CellHeight => _cellHeight;
        public int PivotTopLeftX => _pivotTopLeftX;
        public int PivotTopLeftY => _pivotTopLeftY;
        public float PixelsPerUnit => _pixelsPerUnit;

        /// <summary>⚠️ FALSE for the seagull. Kept as a field rather than a constant because it is a
        /// measured property of the rig, and the one thing a lane copying this pattern gets wrong.</summary>
        public bool FacingsAreCounterClockwise => _facingsAreCounterClockwise;

        public string DerivedFromRigSha256 => _derivedFromRigSha256;

        public float LengthMetres => _lengthMetres;
        public float WingspanMetres => _wingspanMetres;
        public float MassKg => _massKg;
        public float DraftMetres => _draftMetres;
        public float BodyCentreZStand => _bodyCentreZStand;
        public float BodyCentreZPerch => _bodyCentreZPerch;
        public float BodyCentreZFloat => _bodyCentreZFloat;
        public Vector2 FootprintStand => _footprintStand;
        public Vector2 FootprintWingsOpen => _footprintWingsOpen;

        public IReadOnlyList<StripEntry> Strips => _strips;
        public IReadOnlyList<StateEntry> States => _states;
        public IReadOnlyList<EdgeEntry> Transitions => _transitions;
        public int CellCount => _cells.Count;

        // ── cells ───────────────────────────────────────────────────────────────────────────────

        /// <summary>The strip for a state id, or null.</summary>
        public StripEntry Strip(string state)
        {
            if (string.IsNullOrEmpty(state)) return null;
            if (_stripByState == null)
            {
                _stripByState = new Dictionary<string, StripEntry>(StringComparer.Ordinal);
                for (int i = 0; i < _strips.Count; i++)
                {
                    var s = _strips[i];
                    if (s != null && !string.IsNullOrEmpty(s.State)) _stripByState[s.State] = s;
                }
            }
            return _stripByState.TryGetValue(state, out var found) ? found : null;
        }

        /// <summary>
        /// The baked cell for a facing row, a state and a frame. <paramref name="dir"/> IS the sheet
        /// row — this rig is clockwise, so no remap (see <see cref="FacingsAreCounterClockwise"/>).
        /// Out-of-range asks return null rather than throwing: a presenter that has lost its place
        /// should draw nothing for a frame, not take the game down.
        /// </summary>
        public Sprite Cell(int dir, string state, int frame)
        {
            var strip = Strip(state);
            if (strip == null || strip.Frames <= 0) return null;
            if (dir < 0 || dir >= _directions) return null;
            int f = frame % strip.Frames;
            if (f < 0) f += strip.Frames;
            int index = dir * _columns + strip.Column + f;
            return index >= 0 && index < _cells.Count ? _cells[index] : null;
        }

        /// <summary>The cell at a raw page coordinate — what the builder and the coverage test use.</summary>
        public Sprite CellAt(int row, int column)
        {
            int index = row * _columns + column;
            return index >= 0 && index < _cells.Count ? _cells[index] : null;
        }

        // ── behaviour ───────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The state table, built once from the serialized rows. Throws if the asset is incomplete —
        /// deliberately, because a half-built table produces birds that look fine and behave wrongly,
        /// which is the expensive kind of bug.
        /// </summary>
        public SeagullBehaviour Behaviour
        {
            get
            {
                if (_behaviour != null) return _behaviour;

                var rows = new List<SeagullStateRow>(_states.Count);
                for (int i = 0; i < _states.Count; i++)
                {
                    var e = _states[i];
                    if (e == null) continue;
                    rows.Add(new SeagullStateRow(
                        e.State, e.Frames, e.FrameMilliseconds,
                        e.SpeedMetresPerSecond, e.ClimbMetresPerSecond,
                        e.AltitudeA, e.AltitudeB, e.Loop,
                        string.IsNullOrEmpty(e.Next) ? null : e.Next,
                        e.TravelMetres, e.CyclesMin, e.CyclesMax));
                }

                var edges = new List<SeagullEdge>(_transitions.Count);
                for (int i = 0; i < _transitions.Count; i++)
                {
                    var e = _transitions[i];
                    if (e != null) edges.Add(new SeagullEdge(e.From, e.To));
                }

                _behaviour = new SeagullBehaviour(
                    rows, edges,
                    new SeagullLandRules(_landNeedsClear.x, _landNeedsClear.y,
                                         _landApproachIntoWind, _landMinFlatMetres),
                    new SeagullWaterRules(_floatBodyZMetres, _splashBurstFrame, _driftWithCurrent,
                                          _rockRollDegreesMax, _rockHeavePixelsMax),
                    new SeagullFlockRules(_flockSizeMin, _flockSizeMax, _flockRadiusMetres,
                                          _flockAltitudeMinMetres, _flockAltitudeMaxMetres,
                                          _flockPeriodMinSeconds, _flockPeriodMaxSeconds,
                                          _flockGlideDuty,
                                          _flockSwoopEveryMinSeconds, _flockSwoopEveryMaxSeconds,
                                          _flockSpacingMetres, _flockAttractRadiusMetres,
                                          _flockFleeRadiusMetres, _flockSettleAfterSeconds,
                                          _flockRegroupSeconds));
                return _behaviour;
            }
        }

        /// <summary>
        /// Everything a caller must be able to trust before it draws a bird, checked in one pass:
        /// the page is fully populated, every strip fits it, the state table is complete, and the rig
        /// hash is present. Returns the first failure rather than a list — the first one is always the
        /// one to fix.
        /// </summary>
        public bool TryValidate(out string error)
        {
            error = null;

            if (_directions <= 0 || _columns <= 0) { error = "Seagull def: the page has no rows or no columns."; return false; }
            int expected = _directions * _columns;
            if (_cells.Count != expected)
            {
                error = $"Seagull def: the page should hold {expected} cells ({_directions} x {_columns}) but holds {_cells.Count}.";
                return false;
            }
            for (int i = 0; i < _cells.Count; i++)
                if (_cells[i] == null)
                {
                    error = $"Seagull def: cell {i} (row {i / _columns}, column {i % _columns}) is missing. " +
                            "A re-slice renumbers internalIDs — rebuild the def.";
                    return false;
                }

            if (string.IsNullOrEmpty(_derivedFromRigSha256))
            { error = "Seagull def: no derivedFromRigSha256 — these cells cannot be traced to a rig."; return false; }

            int sum = 0;
            for (int i = 0; i < _strips.Count; i++)
            {
                var s = _strips[i];
                if (s == null || string.IsNullOrEmpty(s.State)) { error = $"Seagull def: strip {i} names no state."; return false; }
                if (SeagullStates.IndexOf(s.State) < 0) { error = $"Seagull def: strip '{s.State}' is not a state the sheet is baked for."; return false; }
                if (s.Frames <= 0) { error = $"Seagull def: strip '{s.State}' has no frames."; return false; }
                if (s.Column < 0 || s.Column + s.Frames > _columns)
                { error = $"Seagull def: strip '{s.State}' runs off the page ({s.Column}..{s.Column + s.Frames - 1} of {_columns})."; return false; }
                sum += s.Frames;
            }
            if (_strips.Count != SeagullStates.Order.Length)
            { error = $"Seagull def: {_strips.Count} strips for {SeagullStates.Order.Length} states."; return false; }
            if (sum != _columns)
            { error = $"Seagull def: the strips cover {sum} columns of {_columns}."; return false; }

            try { _ = Behaviour; }
            catch (Exception e) { error = "Seagull def: " + e.Message; return false; }

            for (int i = 0; i < _states.Count; i++)
            {
                var e = _states[i];
                var strip = Strip(e.State);
                if (strip != null && strip.Frames != e.Frames)
                {
                    error = $"Seagull def: state '{e.State}' declares {e.Frames} frames but its strip is {strip.Frames} wide.";
                    return false;
                }
            }

            return true;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only write path for <c>SeagullVisualDefBuilder</c>. Not a runtime API: the
        /// asset is generated, and anything that mutates it at play time is a bug.</summary>
        internal void EditorPopulate(int directions, int columns, int cellWidth, int cellHeight,
                                     int pivotTopLeftX, int pivotTopLeftY, float pixelsPerUnit,
                                     bool facingsAreCounterClockwise, string derivedFromRigSha256,
                                     List<StripEntry> strips, List<Sprite> cells,
                                     List<StateEntry> states, List<EdgeEntry> transitions)
        {
            _directions = directions; _columns = columns;
            _cellWidth = cellWidth; _cellHeight = cellHeight;
            _pivotTopLeftX = pivotTopLeftX; _pivotTopLeftY = pivotTopLeftY;
            _pixelsPerUnit = pixelsPerUnit;
            _facingsAreCounterClockwise = facingsAreCounterClockwise;
            _derivedFromRigSha256 = derivedFromRigSha256;
            _strips = strips; _cells = cells; _states = states; _transitions = transitions;
            _behaviour = null; _stripByState = null;
        }

        /// <summary>Editor-only write path for the creature and rule blocks.</summary>
        internal void EditorPopulateRules(float lengthMetres, float wingspanMetres, float massKg,
                                          float draftMetres, float standZ, float perchZ, float floatZ,
                                          Vector2 footprintStand, Vector2 footprintWingsOpen,
                                          Vector2 landNeedsClear, bool landApproachIntoWind, float landMinFlat,
                                          float floatBodyZ, int splashBurstFrame, bool driftWithCurrent,
                                          float rockRollDegreesMax, float rockHeavePixelsMax,
                                          int sizeMin, int sizeMax, float radius,
                                          float altMin, float altMax, float periodMin, float periodMax,
                                          float glideDuty, float swoopEveryMin, float swoopEveryMax,
                                          float spacing, float attractRadius, float fleeRadius,
                                          float settleAfter, float regroup)
        {
            _lengthMetres = lengthMetres; _wingspanMetres = wingspanMetres; _massKg = massKg;
            _draftMetres = draftMetres;
            _bodyCentreZStand = standZ; _bodyCentreZPerch = perchZ; _bodyCentreZFloat = floatZ;
            _footprintStand = footprintStand; _footprintWingsOpen = footprintWingsOpen;
            _landNeedsClear = landNeedsClear; _landApproachIntoWind = landApproachIntoWind;
            _landMinFlatMetres = landMinFlat;
            _floatBodyZMetres = floatBodyZ; _splashBurstFrame = splashBurstFrame;
            _driftWithCurrent = driftWithCurrent;
            _rockRollDegreesMax = rockRollDegreesMax; _rockHeavePixelsMax = rockHeavePixelsMax;
            _flockSizeMin = sizeMin; _flockSizeMax = sizeMax; _flockRadiusMetres = radius;
            _flockAltitudeMinMetres = altMin; _flockAltitudeMaxMetres = altMax;
            _flockPeriodMinSeconds = periodMin; _flockPeriodMaxSeconds = periodMax;
            _flockGlideDuty = glideDuty;
            _flockSwoopEveryMinSeconds = swoopEveryMin; _flockSwoopEveryMaxSeconds = swoopEveryMax;
            _flockSpacingMetres = spacing; _flockAttractRadiusMetres = attractRadius;
            _flockFleeRadiusMetres = fleeRadius; _flockSettleAfterSeconds = settleAfter;
            _flockRegroupSeconds = regroup;
            _behaviour = null;
        }

        /// <summary>Editor-only write path for the gull-to-fish block. Kept off
        /// <see cref="EditorPopulateRules"/> deliberately: that call is already twenty-five positional
        /// parameters, and two more unlabelled floats on the end is how a drop lands transposed.</summary>
        internal void EditorPopulateGullSplash(float diveCatchProbability, float shoalAttractWeight01)
        {
            _diveCatchProbability = Mathf.Clamp01(diveCatchProbability);
            _shoalAttractWeight01 = Mathf.Clamp01(shoalAttractWeight01);
        }
#endif
    }
}
