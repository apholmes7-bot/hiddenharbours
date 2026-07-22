using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b><see cref="IBoatHullPresenter"/> over the shipped sprite path.</b> A thin, allocation-light
    /// adapter around <see cref="DirectionalBoatSprite"/> — it owns no state of its own beyond the anchor
    /// table and <b>decides nothing</b>: every member forwards. That is deliberate. ADR 0022 phase 1 is the
    /// seam, not a rewrite, so the sprite hull must remain byte-for-byte the same behaviour it was before
    /// the interface existed. If a getter here ever grows a rule of its own, the seam has stopped being a
    /// seam.
    ///
    /// <para><b>A POCO, not a MonoBehaviour</b> (CLAUDE.md §5): it is constructed by
    /// <see cref="BoatHullSkinner"/> alongside the rig, needs no lifecycle of its own, and is unit-testable
    /// without a scene. The MonoBehaviour it wraps stays exactly where it was — on the PHYSICS ROOT, where
    /// heading consumers still ride.</para>
    ///
    /// <para><b>Null-tolerant.</b> A presenter whose <see cref="DirectionalBoatSprite"/> has been destroyed
    /// (a hull swap tore the skin off mid-frame) reports the unskinned defaults rather than throwing —
    /// <see cref="BoatHullSkinner.RemoveSkin"/> destroys that component, and a stale presenter held by a
    /// caller must degrade, not except.</para>
    /// </summary>
    public sealed class SpriteHullPresenter : IBoatHullPresenter
    {
        private readonly DirectionalBoatSprite _directional;
        private readonly SpriteHullAnchors _anchors;

        /// <summary>
        /// Wrap a configured <see cref="DirectionalBoatSprite"/>. <paramref name="anchors"/> may be null,
        /// in which case an empty (but non-null) anchor set is used — a hull whose rig defines no anchors
        /// is normal, and <see cref="Anchors"/> must never be null for a caller.
        /// </summary>
        public SpriteHullPresenter(DirectionalBoatSprite directional, SpriteHullAnchors anchors = null)
        {
            _directional = directional;
            _anchors = anchors ?? new SpriteHullAnchors(directional);
        }

        /// <inheritdoc/>
        public BoatHullVariant Variant => BoatHullVariant.Sprite;

        /// <summary>
        /// The concrete compass component behind this presenter — exposed for ONE reason: consumers
        /// with serialized legacy fields (BoatWaveMotion, the overlay layers) are configured at EDIT
        /// time by the scene builders, and a POCO presenter does not survive serialization. They
        /// persist this component instead and re-wrap it on reload. Do not use it to bypass the seam
        /// at runtime.
        /// </summary>
        public DirectionalBoatSprite Directional => _directional;

        /// <inheritdoc/>
        public float DrawnHeadingDegrees() => _directional != null ? _directional.DrawnHeadingDegrees() : 0f;

        /// <inheritdoc/>
        public int FacingCellIndex => _directional != null ? _directional.CurrentFacingIndex : 0;

        /// <inheritdoc/>
        public int FacingCount => _directional != null ? _directional.FacingCount : 0;

        /// <inheritdoc/>
        public bool FacingsAreCounterClockwise =>
            _directional != null && _directional.FacingsAreCounterClockwise;

        /// <inheritdoc/>
        // 90 = a plan view (no foreshortening), which is what an absent skin has always been treated as —
        // the same default DirectionalBoatSprite itself carries. Do not change it to 0.
        public float BakeElevationDegrees => _directional != null ? _directional.BakeElevationDegrees : 90f;

        /// <inheritdoc/>
        public bool HasRockGrid => _directional != null && _directional.HasRockGrid;

        /// <inheritdoc/>
        public int RockFrame
        {
            // −1 (the level pose) is the documented "no rock" value, so it is also the dead presenter's answer.
            get => _directional != null ? _directional.RockFrame : MountedRockPoseMath.LevelRockFrame;
            set { if (_directional != null) _directional.RockFrame = value; }
        }

        /// <inheritdoc/>
        // A sprite hull's rock IS the baked frame grid — there is nothing continuous to pose.
        public bool SupportsContinuousRock => false;

        /// <inheritdoc/>
        public void SetRockPhaseDegrees(float phaseDegrees)
        {
            // Deliberately a no-op, not an exception: the contract says a sprite presenter's rock
            // arrives as RockFrame, and a caller probing the capability reads SupportsContinuousRock.
        }

        /// <inheritdoc/>
        public float VisualTiltDegrees
        {
            get => _directional != null ? _directional.VisualTiltDegrees : 0f;
            set { if (_directional != null) _directional.VisualTiltDegrees = value; }
        }

        /// <inheritdoc/>
        public Transform Visual => _anchors.Visual;

        /// <inheritdoc/>
        public IBoatHullAnchors Anchors => _anchors;
    }

    /// <summary>
    /// <b>Anchors for a sprite hull</b> — a table of boat-local rig points, projected through the rig
    /// camera for the CELL currently drawn.
    ///
    /// <para><b>This is the same arithmetic the shipped outboard already does.</b>
    /// <see cref="OutboardMotorLayer"/> takes <see cref="BoatVisualDef.MotorMountLocalMeters"/> — a
    /// boat-local metre point straight off the rig — and pushes it through
    /// <see cref="MountedRockPoseMath"/>. Generalising that one hard-wired anchor into a named table is
    /// the whole of this class; the projection is not reimplemented, it is called.</para>
    ///
    /// <para><b>⚠ On the baked JSON.</b> ADR 0022 describes today's anchors as "baked per-cell JSON"
    /// (<c>Art/Boats/*Anchors.json</c>). Those files are real and the baker writes them, but <b>no runtime
    /// code reads them</b> — they are a bake artifact consumed by the editor tooling and by eye. The
    /// runtime's anchor, today, is the analytic projection above, and that is therefore what this
    /// implementation preserves exactly. A JSON-table-backed <see cref="IBoatHullAnchors"/> is a legal
    /// second implementation of this same interface if a hull ever needs per-cell hand-authored offsets —
    /// the contract was shaped so it would be — but shipping one now would be new runtime behaviour in a
    /// phase whose contract is that there is none.</para>
    ///
    /// <para><b>Rock is level in phase 1.</b> Roll and pitch are passed as 0, which is exactly what the
    /// anchor's callers get today outside <see cref="OutboardMotorLayer"/>'s own rock pose (which keeps
    /// owning its lever-arm maths — see <see cref="MountedRockPoseMath.Pose"/>). Coupling anchors to the
    /// rock frame is a behaviour change and belongs to a later phase.</para>
    /// </summary>
    public sealed class SpriteHullAnchors : IBoatHullAnchors
    {
        // Small, fixed-size, indexed by (int)BoatAnchorId — no dictionary, no hashing, no garbage on a
        // lookup that runs every LateUpdate.
        private static readonly int IdCount = System.Enum.GetValues(typeof(BoatAnchorId)).Length;

        private readonly DirectionalBoatSprite _directional;
        private readonly Vector3[][] _localMetres = new Vector3[IdCount][];

        /// <summary>The visual child the hull draws into, when one is wired. May be null.</summary>
        public Transform Visual { get; }

        /// <summary>
        /// <paramref name="directional"/> supplies the drawn cell and the bake elevation; may be null (an
        /// unskinned hull), in which case nothing is defined and every lookup misses.
        /// </summary>
        public SpriteHullAnchors(DirectionalBoatSprite directional, Transform visual = null)
        {
            _directional = directional;
            Visual = visual;
        }

        /// <summary>
        /// Define (or clear) an anchor as one or more BOAT-LOCAL points in METRES, in the rigs' own axes
        /// (+X starboard, +Y bow, +Z up). Null or empty clears it. The array is stored by reference and
        /// must not be mutated afterwards by the caller.
        /// </summary>
        public void Define(BoatAnchorId id, params Vector3[] localMetres)
        {
            int i = (int)id;
            if (i < 0 || i >= IdCount) return;
            _localMetres[i] = (localMetres != null && localMetres.Length > 0) ? localMetres : null;
        }

        /// <inheritdoc/>
        public bool Has(BoatAnchorId id)
        {
            int i = (int)id;
            return i >= 0 && i < IdCount && _localMetres[i] != null;
        }

        /// <inheritdoc/>
        public bool TryGetPoints(BoatAnchorId id, List<Vector2> into)
        {
            if (into == null || !Has(id)) return false;

            Vector3[] points = _localMetres[(int)id];

            // CELL space, not heading space: MountedRockPoseMath.Project wants the rig's turntable angle
            // θ = cell·2π/count. Feeding it the compass heading would be right for clockwise art and
            // MIRRORED for counter-clockwise art — the exact class of bug that shipped mirrored boats once.
            float elevationRad = BakeElevationDegrees() * Mathf.Deg2Rad;
            float turntableRad = TurntableRadians();

            for (int p = 0; p < points.Length; p++)
                into.Add(MountedRockPoseMath.Project(points[p], turntableRad,
                                                     rollRadians: 0f, pitchRadians: 0f,
                                                     elevationRadians: elevationRad));
            return true;
        }

        private float BakeElevationDegrees() => _directional != null ? _directional.BakeElevationDegrees : 90f;

        private float TurntableRadians()
        {
            if (_directional == null) return 0f;
            int count = _directional.FacingCount;
            if (count <= 0) return 0f;
            return _directional.CurrentFacingIndex * (2f * Mathf.PI) / count;
        }
    }
}
