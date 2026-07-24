using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>An outboard on the MESH path (ADR 0022 phase 7)</b> — the same swivel
    /// <see cref="OutboardMotorLayer"/> draws, posing real fittings through
    /// <see cref="IHullPropRenderer"/> instead of picking a baked column.
    ///
    /// <para><b>It is the same state machine, not a second one.</b> Every decision below goes through
    /// <see cref="OutboardMotorMath"/>: the same <see cref="OutboardMotorMath.TargetColumnForHelm"/>
    /// deadzone, the same <see cref="OutboardMotorMath.StepTowardColumn"/> cadence, the same per-hull
    /// authority off the visual. That matters most here, because the owner flips these boats between
    /// sprite and mesh to compare the LOOK — a second transcription of "how fast does she swivel"
    /// would quietly make him compare two different boats.</para>
    ///
    /// <para><b>The one thing that changes, and it is the whole point.</b> The accumulator has always
    /// been a float; the sprite path rounded it to one of nine baked columns because nine cells was
    /// all the art had. Here it is read CONTINUOUSLY
    /// (<see cref="OutboardMotorMath.SteerDegreesForPosition"/>) and turned into a rotation about the
    /// clamp. Same tuned feel, minus the rounding — as #243 did for the hull's rock and #285 for the
    /// dory's stroke.</para>
    ///
    /// <para><b>What it deletes — three whole mechanisms, none of them reimplemented below.</b>
    /// No rock coupling (five tunables leaning a level-baked overlay onto a rock-baked hull: a
    /// fitting is parented to the hull's posed mesh child and inherits roll, pitch and heave exactly).
    /// No <c>upper</c>/<c>lower</c> part split and no per-heading sorting band (the rigs'
    /// <c>MOTOR.behind = [3,4,5]</c> existed only so a sprite leg could be drawn under a sprite hull;
    /// a shared depth buffer settles it per pixel). No draw-the-far-engine-first rule for the twin,
    /// which is simply this def instantiated at each of its mounts.</para>
    ///
    /// <para>Visual-only: it READS the helm and never writes the sim (rule 5); every rate and deadzone
    /// is serialized (rule 6); allocation-free per frame (rule 7); Boats-internal (rule 4).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class OutboardMotorMeshLayer : MonoBehaviour
    {
        [Header("Steer feel (tunable — defaults shared with the sprite outboard)")]
        [Tooltip("Columns/sec the engine swivels toward the helm's target (the art README's tempo is " +
                 "~8) — the SAME accumulator the sprite layer steps, so both representations swivel " +
                 "at one cadence. 0 = snap instantly.")]
        [SerializeField] private float _steerColumnsPerSecond = 8f;

        [Tooltip("|helm| at or below which the wheel reads as CENTRED, so a resting stick never " +
                 "trickles the engine off dead ahead.")]
        [SerializeField] private float _helmDeadzone = 0.05f;

        [Tooltip("The boat whose helm swivels the engine. Read only while the controller is enabled " +
                 "— a dropped helm centres the motor rather than leaving it frozen hard-over (#205).")]
        [SerializeField] private BoatController _boat;

        // Set through Configure. Interfaces and art facts, so they are never serialized — a
        // scene-serialised layer idles until the skinner re-wires it, the same contract
        // DoryOarMeshLayer has.
        private IHullPropRenderer[] _engines = Array.Empty<IHullPropRenderer>();
        private float[] _mounts = Array.Empty<float>();
        private int _steerColumns = OutboardMotorMath.SteerColumns;
        private float _maxSteerDegrees = OutboardMotorMath.MaxSteerDegrees;
        private float _maxTiltDegrees;

        private float _steerPosition;

        /// <summary>True when at least one fitting is attached and configured — the gate the whole
        /// component runs behind. False = it poses nothing, rather than posing half an engine.</summary>
        public bool IsWired
        {
            get
            {
                if (_engines == null || _engines.Length == 0) return false;
                if (_mounts == null || _mounts.Length != _engines.Length) return false;
                for (int i = 0; i < _engines.Length; i++)
                    if (_engines[i] == null || !_engines[i].IsConfigured) return false;
                return true;
            }
        }

        /// <summary>How many engines hang on this transom — 1 for a single fit, 2 for the sport
        /// skiff's twin. Diagnostics and tests.</summary>
        public int EngineCount => _engines != null ? _engines.Length : 0;

        /// <summary>The steer angle the engine is DRAWN at this tick, in degrees (− = port,
        /// + = starboard). Continuous: it is not rounded to a baked column.</summary>
        public float SteerDegrees { get; private set; }

        /// <summary>The tilt the engine is drawn at, in degrees. Always 0 today: trimming the leg up
        /// is not a verb the game has, and the sprite sheets never had the pose either. The seam
        /// carries it because the RIGS do (<c>MOTOR.tiltMax</c>), so the day a trim button exists it
        /// is one setter and no new maths.</summary>
        public float TiltDegrees { get; private set; }

        /// <summary>This hull's steer authority in degrees either side of dead ahead, off her visual
        /// (punt ±32, skiffs ±30). An art fact, never a feel knob.</summary>
        public float MaxSteerDegrees => _maxSteerDegrees;

        /// <summary>The lateral clamp offset of engine <paramref name="index"/>, in metres.</summary>
        public float MountMeters(int index) =>
            _mounts != null && index >= 0 && index < _mounts.Length ? _mounts[index] : 0f;

        /// <summary>
        /// Wire the layer from the skinner. Idempotent — a re-skin re-points it rather than stacking a
        /// second layer (the component is <see cref="DisallowMultipleComponent"/>).
        /// </summary>
        /// <param name="engines">One per clamp position, positionally matched to
        /// <paramref name="mounts"/>.</param>
        /// <param name="mounts">Lateral clamp offsets in metres — <c>{0}</c> for a single fit, the
        /// def's own <c>±0.34</c> for the twin (<see cref="HullPropMeshDef.MountsForFit"/>).</param>
        public void Configure(IHullPropRenderer[] engines, float[] mounts, BoatController boat,
                              int steerColumns, float maxSteerDegrees, float maxTiltDegrees)
        {
            _engines = engines ?? Array.Empty<IHullPropRenderer>();
            _mounts = mounts ?? Array.Empty<float>();
            _boat = boat;
            if (steerColumns > 0) _steerColumns = steerColumns;
            // A zeroed authority is never authored; it would pin the engine dead ahead at every helm.
            if (maxSteerDegrees > 0f) _maxSteerDegrees = maxSteerDegrees;
            _maxTiltDegrees = Mathf.Max(0f, maxTiltDegrees);

            _steerPosition = OutboardMotorMath.CenterColumn(_steerColumns);
            SteerDegrees = 0f;
            TiltDegrees = 0f;

            // The clamp offsets are a fact of the FIT, not of the frame — written once, here, so
            // LateUpdate touches only the rotation (rule 7).
            if (IsWired)
                for (int i = 0; i < _engines.Length; i++)
                    _engines[i].LateralOffsetMeters = _mounts[i];
        }

        /// <summary>The helm the engine swivels to, PULLED off the boat. An unmanned helm reads dead
        /// ahead — the same #205 gate the sprite layer keeps, and for the same reason: a player who
        /// disembarks mid-turn must not leave an empty boat's engine pinned hard-over.</summary>
        public float Helm => IsHelmManned ? _boat.Steer : 0f;

        /// <summary>True when a controller is wired AND actually driving.</summary>
        public bool IsHelmManned => _boat != null && _boat.isActiveAndEnabled;

        private void OnEnable()
        {
            // Wake dead ahead — never on a stale hard-over.
            _steerPosition = OutboardMotorMath.CenterColumn(_steerColumns);
            SteerDegrees = 0f;
        }

        private void LateUpdate()
        {
            if (!IsWired) return;

            int target = OutboardMotorMath.TargetColumnForHelm(
                Helm, _helmDeadzone, _steerColumns, _maxSteerDegrees);
            _steerPosition = OutboardMotorMath.StepTowardColumn(
                _steerPosition, target, _steerColumnsPerSecond, Time.deltaTime);

            // ⚠️ The continuous read — NOT ColumnFromPosition. That rounding exists because the sprite
            // sheet has nine cells; a mesh has none, so keeping it would throw away the whole reason
            // this path exists and reintroduce a step the owner can see on a slow turn.
            SteerDegrees = OutboardMotorMeshPose.ClampSteer(
                OutboardMotorMath.SteerDegreesForPosition(_steerPosition, _steerColumns, _maxSteerDegrees),
                _maxSteerDegrees);
            TiltDegrees = OutboardMotorMeshPose.ClampTilt(TiltDegrees, _maxTiltDegrees);

            Quaternion r = OutboardMotorMeshPose.Rotation(SteerDegrees, TiltDegrees);
            for (int i = 0; i < _engines.Length; i++) _engines[i].LocalRotation = r;
        }
    }
}
