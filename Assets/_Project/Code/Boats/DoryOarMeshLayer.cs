using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>The iso dory's oars on the MESH path (ADR 0022 phase 7)</b> — the same two independent
    /// strokes <see cref="DoryOarLayer"/> draws, posing two real meshes through
    /// <see cref="IHullPropRenderer"/> instead of picking a baked cell.
    ///
    /// <para><b>It is the same state machine, not a second one.</b> Every decision below goes through
    /// <see cref="DoryOarMath"/> — the same signed phase accumulator, the same
    /// <see cref="DoryOarMath.StrokeFramesPerSecond"/> effort curve, the same
    /// <see cref="DoryOarMath.ColumnForOar"/> working/trailing/shipped rule with the same grace. That
    /// matters more here than anywhere: the owner flips the dory between sprite and mesh to compare
    /// the LOOK, and a second transcription of "when does an oar ship" would quietly make him compare
    /// two different boats.</para>
    ///
    /// <para><b>The one thing that changes, and it is the whole point.</b>
    /// <see cref="DoryOarMath.ColumnForOar"/> is asked WHICH STATE the oar is in and its rounding is
    /// then thrown away: a working oar is posed from the CONTINUOUS phase
    /// (<c>phase / StrokeColumns</c> turns) rather than from the floor of it. Same tuned stroke, same
    /// tempo, read between the sprite path's eight frames — exactly what #243 did for the hull's wave
    /// rock, and for the same reason.</para>
    ///
    /// <para><b>What it deletes.</b> No rock coupling. The sprite oars are baked rock-FREE and have to
    /// be leaned onto a rock-BAKED hull frame by hand (<see cref="DoryOarMath.RockPose"/>: a heave in
    /// metres, an approximated roll, a pitch stand-in — five tunables to keep an overlay on the
    /// gunwale). A mesh fitting is parented to the hull's posed mesh child, so it inherits roll, pitch
    /// and heave exactly, for nothing. None of that is transcribed below, on purpose.</para>
    ///
    /// <para><b>The one thing it decides that the sprite layer does not: whether the boat is under
    /// POWER</b> (§7.7). The dory is the first hull to carry oars and an outboard at once — a mesh
    /// fitting is posed by rotation rather than indexed by facing, so it can share a hull with the
    /// oars where the sprite sheets' sorting bands cannot — and a boat with a running prop stows her
    /// oars rather than trailing them beside it. That is the existing SHIPPED state, wired to the
    /// engine (see <see cref="ColumnForOar"/>), not a fourth pose; cut the motor and the rowing
    /// machine takes over again, unchanged, from where it left off.</para>
    ///
    /// <para>Visual-only, allocation-free per frame, and it never writes to the sim (rule 5): it READS
    /// the per-oar state <see cref="BoatController"/> already computed.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class DoryOarMeshLayer : MonoBehaviour, IOarStroke, IRowingStation
    {
        // The stroke's TEMPO (frames per second, effort curve, the phase lock) moved to the boat's own
        // stroke clock (BoatController, advanced on the physics step) so the hull's surge and these
        // oars read one phase. What stays here is the state rule: when an oar counts as rowed, and how
        // long both must rest before they ship.
        [Header("Oar state (tunable — defaults shared with the sprite oars)")]
        [Tooltip("|oar state| at or below which the oar is idle (trailing/shipped) rather than stroking.")]
        [SerializeField] private float _oarDeadzone = DoryOarMath.DefaultOarDeadzone;

        [Tooltip("Seconds BOTH oars must be idle before they ship. Below this they trail, so a brief " +
                 "pause between strokes doesn't stow them.")]
        [SerializeField] private float _restGraceSeconds = DoryOarMath.DefaultRestGraceSeconds;

        [Tooltip("The boat whose REAL per-oar state (LeftOar = port, RightOar = starboard) drives the " +
                 "strokes. Read only while the controller is enabled — a dropped helm ships the oars.")]
        [SerializeField] private BoatController _boat;

        [Tooltip("This boat's OUTBOARD, when she has one. While it runs the oars are SHIPPED — stowed " +
                 "fore along the gunwale — and cutting it brings them straight back out. Null on a " +
                 "rowed hull, which is the dory before she buys her kicker.")]
        [SerializeField] private OutboardMotorMeshLayer _motor;

        // Set through Configure. Interfaces, so they are never serialized — a scene-serialised layer
        // idles until the skinner re-wires it, which is the same contract the mesh HULL driver has.
        private IHullPropRenderer _port, _star;

        private float _portPhase, _starPhase, _bothIdleSeconds;

        /// <summary>True when both fittings are attached and configured — the gate the whole component
        /// runs behind. False = it poses nothing, rather than posing one oar.</summary>
        public bool IsWired =>
            _port != null && _port.IsConfigured && _star != null && _star.IsConfigured;

        /// <summary>The port oar's live pose, for tests and the inspector.</summary>
        public DoryOarMeshPose.Pose PortPose { get; private set; } = DoryOarMeshPose.Resting;

        /// <summary>The starboard oar's live pose.</summary>
        public DoryOarMeshPose.Pose StarboardPose { get; private set; } = DoryOarMeshPose.Resting;

        /// <summary>True while the port oar is drawn ROWING — a stroke pose, not trailing, not
        /// shipped, not stowed under power. The oar splash's gate.</summary>
        public bool PortStroking { get; private set; }

        /// <summary>True while the starboard oar is drawn rowing.</summary>
        public bool StarboardStroking { get; private set; }

        /// <summary>The port oar's signed state while it is rowing (+ ahead, − backing), else 0.</summary>
        public float PortOarState { get; private set; }

        /// <summary>The starboard oar's signed state while it is rowing, else 0.</summary>
        public float StarboardOarState { get; private set; }

        /// <summary>True while either oar is drawn rowing on a live, wired layer: what the rower's
        /// body follows (<see cref="IOarStroke"/>).</summary>
        public bool IsStroking => _stroking && isActiveAndEnabled && IsWired;

        /// <summary>The leading oar's stroke phase in [0, 1), 0 = catch, 0.5 = release; 0 while not
        /// stroking. Read from the SAME phase the oar was just posed from, so the body cannot drift
        /// from the blade.</summary>
        public float StrokePhase => IsStroking ? _strokePhase : 0f;

        /// <summary>The part of the stroke <see cref="StrokePhase"/> is in.</summary>
        public OarStrokePart StrokePart => OarStrokeMath.PartOf(IsStroking, _strokePhase);

        private bool _stroking;
        private float _strokePhase;

        /// <summary>
        /// The phase (in stroke columns) of the oar the rower's body should follow, or false when
        /// neither is rowing. One body, two oars: it follows the one pulling HARDER (a turn on one oar
        /// is rowed with that oar), and port on a tie, so a straight pull reads one oar consistently.
        /// </summary>
        public static bool LeadingOarPhase(bool portStroking, float portPhase, float portState,
                                           bool starStroking, float starPhase, float starState,
                                           out float phase)
        {
            if (portStroking && (!starStroking || Mathf.Abs(portState) >= Mathf.Abs(starState)))
            {
                phase = portPhase;
                return true;
            }
            if (starStroking)
            {
                phase = starPhase;
                return true;
            }
            phase = 0f;
            return false;
        }

        /// <summary>The <see cref="IOarStroke"/> phase of an oar at <paramref name="phaseColumns"/> of
        /// its <see cref="DoryOarMath.StrokeColumns"/>-column cycle.</summary>
        public static float StrokePhaseOf(float phaseColumns) =>
            OarStrokeMath.PhaseFromOarTurns(phaseColumns / DoryOarMath.StrokeColumns, DoryOarMeshPose.RowCatchTurns);

        /// <summary>True when <paramref name="column"/> is one of the stroke cycle's own columns
        /// rather than the trailing or shipped pose.</summary>
        public static bool IsStrokeColumn(int column) => column >= 0 && column < DoryOarMath.StrokeColumns;

        /// <summary>Wire the layer from the skinner. Idempotent — a re-skin re-points it rather than
        /// stacking a second layer (the component is <see cref="DisallowMultipleComponent"/>).
        ///
        /// <para><paramref name="motor"/> is this boat's outboard, or null on a rowed hull. It is a
        /// LIVE reference, not a flag copied at install: an engine that stops — because the layer was
        /// disabled, or because the skinner took it off on a hull swap — brings the oars back out on
        /// the next tick, which is what "cutting the motor" has to mean.</para></summary>
        public void Configure(IHullPropRenderer port, IHullPropRenderer star, BoatController boat,
                              OutboardMotorMeshLayer motor = null)
        {
            _port = port;
            _star = star;
            _boat = boat;
            _motor = motor;
            _portPhase = 0f;
            _starPhase = 0f;
            _bothIdleSeconds = 0f;
        }

        // The rowing station: where the oarlocks are (the fittings' pivots, hull rig metres) and the
        // hull's station data. Set through ConfigureStation; unset = no station, the rower plays his clip.
        private bool _hasStation;
        private Vector3 _portLock, _starLock;
        private RowingStationConfig _station;

        /// <summary>
        /// Wire the rowing station from the skinner: the two oarlocks (the fittings'
        /// <c>PivotLocalMeters</c>, the point each oar turns about) and the hull's
        /// <see cref="RowingStationConfig"/>. A disabled config leaves no station.
        /// </summary>
        public void ConfigureStation(Vector3 portOarlock, Vector3 starboardOarlock, in RowingStationConfig station)
        {
            _portLock = portOarlock;
            _starLock = starboardOarlock;
            _station = station;
            _hasStation = station.Enabled;
        }

        /// <summary>
        /// <see cref="IRowingStation"/>: the grips where the oars were posed this frame, the seat and the
        /// braces, all in the hull's rig frame. Answers whenever the station is wired, rowed or not: a
        /// rower resting on his oars stays sat on his thwart instead of standing up between strokes. A
        /// trailing or shipped oar's grip is marked invalid: that hand lets go rather than chase a
        /// handle out of reach.
        /// </summary>
        public bool TryGetRowingTargets(out RowingTargets targets)
        {
            targets = default;
            if (!_hasStation || !IsWired || !isActiveAndEnabled) return false;
            targets = StationTargets(_portLock, _starLock, in _station, PortPose, PortStroking,
                                     StarboardPose, StarboardStroking);
            targets.LaybackDegrees *= _laybackScale;
            targets.RestWeight = _restWeight;
            targets.RestBreath = _restBreath;
            return true;
        }

        // The rower's style's rest this frame (RowingStyle.RestWeight / RestBreath), taken with the pose.
        private float _restWeight, _restBreath;

        /// <summary>
        /// A rowing oar's pose with the style's rest folded in: through a rest the blade settles from the
        /// stroke's own dip to <paramref name="restBladeDipDegrees"/> (feathered just over the water) by
        /// <paramref name="restWeight"/>, so the hands sit relaxed instead of holding the stroke's highest
        /// blade still. The sweep is the stroke's. Weight 0 is the stroke exactly.
        /// </summary>
        public static DoryOarMeshPose.Pose Rested(in DoryOarMeshPose.Pose rowing, float restWeight, float restBladeDipDegrees)
        {
            if (restWeight <= 0f) return rowing;
            return new DoryOarMeshPose.Pose(rowing.SweepDegrees,
                                            Mathf.Lerp(rowing.DipDegrees, restBladeDipDegrees, Mathf.Clamp01(restWeight)));
        }

        // The rower's style's lean-back scale, taken with the pose each frame (1 = the station's own).
        private float _laybackScale = 1f;

        /// <summary>
        /// The phase (stroke columns) an oar is DRAWN at when the stroke clock reads
        /// <paramref name="rawColumns"/> and the rower rows <paramref name="style"/>: the style's timing
        /// warp, applied in the stroke's own phase (0 = catch) and handed back in columns. The plain
        /// stroke returns <paramref name="rawColumns"/> itself, untouched.
        /// </summary>
        public static float StyledColumns(in RowingStyle style, float rawColumns)
        {
            if (style.IdentityTiming) return rawColumns;
            float warped = style.WarpStrokePhase(RowStrokeClock.StrokePhaseOfColumns(rawColumns));
            float columns = Mathf.Repeat((warped + DoryOarMeshPose.RowCatchTurns) * DoryOarMath.StrokeColumns,
                                         DoryOarMath.StrokeColumns);
            return columns >= DoryOarMath.StrokeColumns ? 0f : columns;
        }

        /// <summary>The station's targets for two oar poses. Pure, so the arithmetic is provable without
        /// a scene.</summary>
        public static RowingTargets StationTargets(Vector3 portLock, Vector3 starLock, in RowingStationConfig station,
                                                   DoryOarMeshPose.Pose portPose, bool portRowing,
                                                   DoryOarMeshPose.Pose starPose, bool starRowing)
        {
            Vector3 portDir = DoryOarMeshPose.Direction(PortSide, portPose.SweepDegrees, portPose.DipDegrees);
            Vector3 starDir = DoryOarMeshPose.Direction(StarboardSide, starPose.SweepDegrees, starPose.DipDegrees);
            return new RowingTargets
            {
                PortGripValid = portRowing,
                StarboardGripValid = starRowing,
                PortGrip = RowingStationMath.Grip(portLock, portDir, station.GripInboardMeters),
                StarboardGrip = RowingStationMath.Grip(starLock, starDir, station.GripInboardMeters),
                PortHandleEnd = RowingStationMath.Grip(portLock, portDir, station.LoomInboardMeters),
                StarboardHandleEnd = RowingStationMath.Grip(starLock, starDir, station.LoomInboardMeters),
                Seat = station.SeatLocalMeters,
                StarboardBrace = station.FootBraceLocalMeters,
                PortBrace = RowingStationMath.MirrorAcrossCentreline(station.FootBraceLocalMeters),
                LaybackDegrees = station.LaybackDegrees,
                ReachLeanMaxDegrees = station.ReachLeanMaxDegrees,
                BodyClearanceMeters = station.BodyClearanceMeters,
                BodyClearLeanMaxDegrees = station.BodyClearLeanMaxDegrees,
                BodyClearTwistMaxDegrees = station.BodyClearTwistMaxDegrees,
                RestSlumpDegrees = station.RestSlumpDegrees,
                RestBreathDegrees = station.RestBreathDegrees,
                RestBreathSeconds = station.RestBreathSeconds,
                RestHandAlongThigh = station.RestHandAlongThigh,
                RestHandAboveThighMeters = station.RestHandAboveThighMeters,
            };
        }

        /// <summary>
        /// <b>True while this boat is under her own power</b> — the state that ships the oars.
        ///
        /// <para>An outboard that is WIRED is an outboard that is running: M1 has no ignition, no
        /// fuel and no kill switch (those questions are the owner's, and open), so the honest signal
        /// available is whether the engine the skinner bolted on is there and live. That is exactly
        /// the D8 shape — the engine is a purchase, so <c>boat.dory_outboard</c> has one and
        /// <c>boat.dory</c> does not — and it is why this reads the layer every tick instead of
        /// caching a bool: the day a kill switch exists, disabling the motor layer is already the
        /// whole of "cut the engine, ship the oars back out".</para>
        /// </summary>
        public bool MotorIsRunning => _motor != null && _motor.isActiveAndEnabled && _motor.IsWired;

        private void LateUpdate()
        {
            if (!IsWired)
            {
                _stroking = false;
                return;
            }

            float dt = Time.deltaTime;
            bool underPower = MotorIsRunning;

            // NOBODY AT THE HELM = NOBODY ROWING — the same gate the sprite layer uses, and for the
            // same reason: BoatController.Stop() is not called on every helm-drop path, so trusting
            // LeftOar/RightOar to have been cleared would leave an empty boat rowing itself across the
            // harbour. Gating on the controller is right by construction.
            bool helmManned = _boat != null && _boat.isActiveAndEnabled;
            float portState = helmManned ? _boat.LeftOar : 0f;
            float starState = helmManned ? _boat.RightOar : 0f;

            bool portWorking = DoryOarMath.IsWorking(portState, _oarDeadzone);
            bool starWorking = DoryOarMath.IsWorking(starState, _oarDeadzone);

            // THE ACCUMULATORS FREEZE UNDER POWER. Stowed oars are not stroking and not resting-up
            // toward anything, so neither the phase nor the grace advances — cutting the motor puts
            // the oars back in the hands that left them, mid-sweep, instead of at the top of a cycle.
            if (!underPower)
            {
                _bothIdleSeconds = (portWorking || starWorking) ? 0f : _bothIdleSeconds + dt;

                // THE BOAT'S STROKE CLOCK, drawn where the hull is drawn. The phase is advanced on the
                // physics step (BoatController), the same phase her thrust reads, so the surge lines up
                // with the blades; an interpolated hull is drawn up to one step behind its body, and the
                // oars are read at that same offset so they sit on it. Nobody at the helm: the phases
                // hold where the oars left them, as they always did.
                if (helmManned)
                {
                    RowStrokeClock clock = _boat.StrokeClock;
                    float behind = RowStrokeClock.InterpolationLag(Time.time, Time.fixedTime, Time.fixedDeltaTime);
                    _portPhase = clock.PortAt(-behind);
                    _starPhase = clock.StarboardAt(-behind);
                }
            }

            // The rower's style shapes WHEN the oar is where (a slower pull, a rest with the blades
            // out); the angles are the rig's own. The plain stroke passes the phase through unchanged.
            RowingStyle style = _boat != null ? _boat.RowingStyle : RowingStyle.Unstyled;
            _laybackScale = style.LaybackScale;
            float portStyled = StyledColumns(in style, _portPhase);
            float starStyled = StyledColumns(in style, _starPhase);

            int portColumn = ColumnForOar(underPower, portWorking, _portPhase, starWorking,
                                          _bothIdleSeconds, _restGraceSeconds, DoryOarMath.StrokeColumns);
            int starColumn = ColumnForOar(underPower, starWorking, _starPhase, portWorking,
                                          _bothIdleSeconds, _restGraceSeconds, DoryOarMath.StrokeColumns);
            PortPose = PoseFor(portColumn, portStyled);
            StarboardPose = PoseFor(starColumn, starStyled);

            // The style's rest: the blades settle feathered over the water and the rower relaxes, by the
            // same weight, read off the same raw phase the warp held still.
            float portRest = IsStrokeColumn(portColumn) ? style.RestWeight(RowStrokeClock.StrokePhaseOfColumns(_portPhase)) : 0f;
            float starRest = IsStrokeColumn(starColumn) ? style.RestWeight(RowStrokeClock.StrokePhaseOfColumns(_starPhase)) : 0f;
            if (_hasStation)
            {
                PortPose = Rested(PortPose, portRest, _station.RestBladeDipDegrees);
                StarboardPose = Rested(StarboardPose, starRest, _station.RestBladeDipDegrees);
            }
            bool portLeads = portRest >= starRest;
            _restWeight = Mathf.Max(portRest, starRest);
            _restBreath = _restWeight > 0f
                ? style.RestBreath(RowStrokeClock.StrokePhaseOfColumns(portLeads ? _portPhase : _starPhase))
                : 0f;

            // What the oar splash reads: the SAME decision the pose was just made from, so a blade
            // can only splash on a frame it was drawn rowing.
            PortStroking = IsStrokeColumn(portColumn);
            StarboardStroking = IsStrokeColumn(starColumn);
            PortOarState = PortStroking ? portState : 0f;
            StarboardOarState = StarboardStroking ? starState : 0f;

            // What the rower's body reads: the same decision, the same phase.
            _stroking = LeadingOarPhase(PortStroking, portStyled, PortOarState,
                                        StarboardStroking, starStyled, StarboardOarState, out float leading);
            _strokePhase = _stroking ? StrokePhaseOf(leading) : 0f;

            _port.LocalRotation = DoryOarMeshPose.Rotation(PortSide, PortPose);
            _star.LocalRotation = DoryOarMeshPose.Rotation(StarboardSide, StarboardPose);
        }

        /// <summary>The rig's side factor: port is −1 on the beam axis, starboard +1.</summary>
        public const int PortSide = -1;
        public const int StarboardSide = +1;

        /// <summary>
        /// The pose a column stands for, with the column's ROUNDING discarded for a working oar.
        ///
        /// <para><see cref="DoryOarMath.ColumnForOar"/> answers two questions at once — which state the
        /// oar is in, and (for a working one) which of eight cells the sprite path would draw. The mesh
        /// path wants only the first: the state selects between the rig's three named poses, and a
        /// working oar is posed from the continuous phase instead of the cell. Shipped and trailing are
        /// the rig's own fixed poses (<c>oarPose('resting')</c> / <c>('trailing')</c>), so they are the
        /// same on both paths by construction.</para>
        /// </summary>
        /// <summary>
        /// <b>Which column an oar draws, once the boat's ENGINE gets a say.</b> Under power the
        /// answer is <see cref="DoryOarMath.RestingColumn"/> — shipped, at once, with no grace to
        /// wait out — and otherwise it is <see cref="DoryOarMath.ColumnForOar"/>, unchanged and
        /// untouched.
        ///
        /// <para>That is the whole of "ship the oars when the motor runs" (§7.7): the shipped state
        /// already existed and is the rig's own <c>oarPose('resting')</c>, so this wires it rather
        /// than inventing a fourth state. Written as a pure static, like everything else this layer
        /// decides, so the rule is provable without a scene — and so the sprite path can borrow it
        /// unchanged the day a sprite hull ever wears both.</para>
        /// </summary>
        public static int ColumnForOar(bool motorRunning, bool thisOarWorking, float phase,
                                       bool otherOarWorking, float bothIdleSeconds,
                                       float restGraceSeconds, int strokeColumns)
            => motorRunning
                ? DoryOarMath.RestingColumn
                : DoryOarMath.ColumnForOar(thisOarWorking, phase, otherOarWorking, bothIdleSeconds,
                                           restGraceSeconds, strokeColumns);

        public static DoryOarMeshPose.Pose PoseFor(int column, float phase)
        {
            if (column == DoryOarMath.RestingColumn) return DoryOarMeshPose.Resting;
            if (column == DoryOarMath.TrailingColumn) return DoryOarMeshPose.Trailing;
            return DoryOarMeshPose.Row(phase / DoryOarMath.StrokeColumns);
        }
    }
}
