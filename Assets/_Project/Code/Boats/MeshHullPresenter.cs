using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b><see cref="IBoatHullPresenter"/> over the real-time 3D mesh path (ADR 0022 phase 4)</b> —
    /// the second implementation the seam was built for. A POCO constructed by
    /// <see cref="BoatHullSkinner"/> alongside <see cref="MeshHullDriver"/> (the MonoBehaviour that
    /// actually pushes the pose each LateUpdate); this object is the DESCRIPTION consumers read and
    /// the rock/tilt channel <see cref="BoatWaveMotion"/> writes.
    ///
    /// <para><b>How the mesh answers the sprite-shaped questions</b> (the contract phase 1 wrote
    /// down in <see cref="IBoatHullPresenter"/>):</para>
    /// <list type="bullet">
    ///   <item><see cref="DrawnHeadingDegrees"/> — the TRUE heading, continuously: a mesh hull has
    ///   no facing grid, so the picture on screen always points exactly where the physics bow
    ///   points. Read from the ROOT transform (heading consumers ride the root — the spotlight
    ///   lesson), never from the visual child.</item>
    ///   <item><see cref="FacingCount"/> 0 / <see cref="FacingCellIndex"/> 0 — the documented
    ///   "unquantised" signal. Overlay layers that pick sheet rows by cell CANNOT ride a mesh hull
    ///   (their art is baked per facing); the skinner refuses to wire them.</item>
    ///   <item><see cref="FacingsAreCounterClockwise"/> false — that flag describes a SHEET's cell
    ///   order and is meaningless here. The mesh path's own mirror question (the live rig's measured
    ///   azimuth convention) lives on <see cref="HullMeshDef.AzimuthCounterClockwise"/> and is
    ///   consumed inside <see cref="MeshHullDriver"/>, not surfaced as this sheet fact.</item>
    ///   <item><see cref="HasRockGrid"/> true / <see cref="SupportsContinuousRock"/> true — rock is
    ///   a transform (roll/pitch/heave through the facet renderer), free and continuous; the pose
    ///   comes from <see cref="SetRockPhaseDegrees"/> with the rig's own amplitudes
    ///   (<see cref="HullMeshMath.RockPose"/>).</item>
    /// </list>
    ///
    /// <para><b>Null-tolerant</b> like <see cref="SpriteHullPresenter"/>: a presenter whose driver
    /// was destroyed (a hull swap tore the rig off) reports the unskinned defaults rather than
    /// throwing.</para>
    /// </summary>
    public sealed class MeshHullPresenter : IBoatHullPresenter
    {
        private readonly MeshHullDriver _driver;
        private readonly MeshHullAnchors _anchors;

        public MeshHullPresenter(MeshHullDriver driver, MeshHullAnchors anchors = null)
        {
            _driver = driver;
            _anchors = anchors ?? new MeshHullAnchors(driver);
        }

        /// <inheritdoc/>
        public BoatHullVariant Variant => BoatHullVariant.Mesh;

        /// <inheritdoc/>
        public float DrawnHeadingDegrees() =>
            _driver != null ? DirectionalBoatSprite.HeadingDegreesFromBow(_driver.transform.up) : 0f;

        /// <inheritdoc/>
        public int FacingCellIndex => 0;

        /// <inheritdoc/>
        public int FacingCount => 0;

        /// <inheritdoc/>
        public bool FacingsAreCounterClockwise => false;

        /// <inheritdoc/>
        // The mesh's own projection bakes the same elevation into its object transform, so it
        // reports the same art fact a sheet would — anchors and the wake foreshorten identically.
        public float BakeElevationDegrees => _driver != null ? _driver.ElevationDegrees : 90f;

        /// <inheritdoc/>
        public bool HasRockGrid => true;

        /// <inheritdoc/>
        public int RockFrame
        {
            get => _driver != null ? _driver.RockFrame : MountedRockPoseMath.LevelRockFrame;
            set { if (_driver != null) _driver.RockFrame = value; }
        }

        /// <inheritdoc/>
        public bool SupportsContinuousRock => true;

        /// <inheritdoc/>
        public void SetRockPhaseDegrees(float phaseDegrees)
        {
            if (_driver != null) _driver.SetRockPhaseDegrees(phaseDegrees);
        }

        /// <inheritdoc/>
        public float VisualTiltDegrees
        {
            get => _driver != null ? _driver.VisualTiltDegrees : 0f;
            set { if (_driver != null) _driver.VisualTiltDegrees = value; }
        }

        /// <inheritdoc/>
        public Transform Visual => _driver != null ? _driver.Visual : null;

        /// <inheritdoc/>
        public IBoatHullAnchors Anchors => _anchors;
    }

    /// <summary>
    /// <b>Anchors for a mesh hull</b> — the same boat-local rig points a sprite hull tables
    /// (<see cref="SpriteHullAnchors"/>), projected for the CONTINUOUS pose currently drawn instead
    /// of for a snapped cell. Same projection (<see cref="MountedRockPoseMath.Project"/>), same
    /// result frame (screen-metre offsets from the pivot); the turntable angle is simply the live
    /// rig dir (<c>θ = dir·π/4</c>) instead of <c>cell·2π/count</c>.
    ///
    /// <para>Rock is level here for the same reason it is in <see cref="SpriteHullAnchors"/>:
    /// coupling anchors to the rock pose is a behaviour change belonging to a later phase, and the
    /// two implementations must answer identically at matching headings.</para>
    /// </summary>
    public sealed class MeshHullAnchors : IBoatHullAnchors
    {
        private static readonly int IdCount = System.Enum.GetValues(typeof(BoatAnchorId)).Length;

        private readonly MeshHullDriver _driver;
        private readonly Vector3[][] _localMetres = new Vector3[IdCount][];

        public MeshHullAnchors(MeshHullDriver driver)
        {
            _driver = driver;
        }

        /// <summary>Define (or clear) an anchor as boat-local points in METRES (rig axes: +X
        /// starboard, +Y bow, +Z up). Same contract as <see cref="SpriteHullAnchors.Define"/>.</summary>
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
            if (into == null || !Has(id) || _driver == null) return false;

            Vector3[] points = _localMetres[(int)id];
            float elevationRad = _driver.ElevationDegrees * Mathf.Deg2Rad;
            // The live turntable angle, continuous: the same θ the drawn mesh is posed at.
            float turntableRad = _driver.CurrentDirUnits * (Mathf.PI / 4f);

            for (int p = 0; p < points.Length; p++)
                into.Add(MountedRockPoseMath.Project(points[p], turntableRad,
                                                     rollRadians: 0f, pitchRadians: 0f,
                                                     elevationRadians: elevationRad));
            return true;
        }
    }
}
