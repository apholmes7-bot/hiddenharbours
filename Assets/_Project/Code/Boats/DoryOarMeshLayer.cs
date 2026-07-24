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
    /// <para>Visual-only, allocation-free per frame, and it never writes to the sim (rule 5): it READS
    /// the per-oar state <see cref="BoatController"/> already computed.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class DoryOarMeshLayer : MonoBehaviour
    {
        [Header("Stroke feel (tunable — defaults shared with the sprite oars)")]
        [Tooltip("Frames/sec the 8-frame stroke cycle plays at a full-effort pull. Same default as " +
                 "DoryOarLayer's: the two representations must row at the same tempo.")]
        [SerializeField] private float _strokeFramesPerSecond = DoryOarMath.DefaultStrokeFramesPerSecond;

        [Tooltip("|oar state| at or below which the oar is idle (trailing/shipped) rather than stroking.")]
        [SerializeField] private float _oarDeadzone = DoryOarMath.DefaultOarDeadzone;

        [Tooltip("How much a GENTLE pull slows the sweep: 0 = every stroke runs at the full tempo; " +
                 "1 = the rate is proportional to effort.")]
        [Range(0f, 1f)]
        [SerializeField] private float _effortInfluence = DoryOarMath.DefaultEffortInfluence;

        [Tooltip("Seconds BOTH oars must be idle before they ship. Below this they trail, so a brief " +
                 "pause between strokes doesn't stow them.")]
        [SerializeField] private float _restGraceSeconds = DoryOarMath.DefaultRestGraceSeconds;

        [Tooltip("The boat whose REAL per-oar state (LeftOar = port, RightOar = starboard) drives the " +
                 "strokes. Read only while the controller is enabled — a dropped helm ships the oars.")]
        [SerializeField] private BoatController _boat;

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

        /// <summary>Wire the layer from the skinner. Idempotent — a re-skin re-points it rather than
        /// stacking a second layer (the component is <see cref="DisallowMultipleComponent"/>).</summary>
        public void Configure(IHullPropRenderer port, IHullPropRenderer star, BoatController boat)
        {
            _port = port;
            _star = star;
            _boat = boat;
            _portPhase = 0f;
            _starPhase = 0f;
            _bothIdleSeconds = 0f;
        }

        private void LateUpdate()
        {
            if (!IsWired) return;

            float dt = Time.deltaTime;

            // NOBODY AT THE HELM = NOBODY ROWING — the same gate the sprite layer uses, and for the
            // same reason: BoatController.Stop() is not called on every helm-drop path, so trusting
            // LeftOar/RightOar to have been cleared would leave an empty boat rowing itself across the
            // harbour. Gating on the controller is right by construction.
            bool helmManned = _boat != null && _boat.isActiveAndEnabled;
            float portState = helmManned ? _boat.LeftOar : 0f;
            float starState = helmManned ? _boat.RightOar : 0f;

            bool portWorking = DoryOarMath.IsWorking(portState, _oarDeadzone);
            bool starWorking = DoryOarMath.IsWorking(starState, _oarDeadzone);
            _bothIdleSeconds = (portWorking || starWorking) ? 0f : _bothIdleSeconds + dt;

            _portPhase = DoryOarMath.AdvanceStrokePhase(
                _portPhase, DoryOarMath.StrokeDirection(portState, _oarDeadzone),
                DoryOarMath.StrokeFramesPerSecond(_strokeFramesPerSecond, portState, _effortInfluence),
                dt, DoryOarMath.StrokeColumns);
            _starPhase = DoryOarMath.AdvanceStrokePhase(
                _starPhase, DoryOarMath.StrokeDirection(starState, _oarDeadzone),
                DoryOarMath.StrokeFramesPerSecond(_strokeFramesPerSecond, starState, _effortInfluence),
                dt, DoryOarMath.StrokeColumns);

            PortPose = PoseFor(DoryOarMath.ColumnForOar(portWorking, _portPhase, starWorking,
                                                        _bothIdleSeconds, _restGraceSeconds,
                                                        DoryOarMath.StrokeColumns), _portPhase);
            StarboardPose = PoseFor(DoryOarMath.ColumnForOar(starWorking, _starPhase, portWorking,
                                                             _bothIdleSeconds, _restGraceSeconds,
                                                             DoryOarMath.StrokeColumns), _starPhase);

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
        public static DoryOarMeshPose.Pose PoseFor(int column, float phase)
        {
            if (column == DoryOarMath.RestingColumn) return DoryOarMeshPose.Resting;
            if (column == DoryOarMath.TrailingColumn) return DoryOarMeshPose.Trailing;
            return DoryOarMeshPose.Row(phase / DoryOarMath.StrokeColumns);
        }
    }
}
