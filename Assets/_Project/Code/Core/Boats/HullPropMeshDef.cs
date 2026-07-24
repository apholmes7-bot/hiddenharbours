using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>An articulated fitting on a mesh hull, as a committed asset (ADR 0022 phase 7)</b> — the
    /// dory's oars, the punt's outboard, the skiffs' outboard. Everything the facet render path needs
    /// to draw one moving part, plus the single art fact needed to POSE it: the point it turns about.
    ///
    /// <para><b>Why this exists at all, and why it is what unblocked the fleet.</b> Phase 6 baked all
    /// eleven hulls but could only PRESENT seven. The four small boats kept their sprites because
    /// their fittings — oars, outboards — are baked one cell per facing, and a mesh hull turns
    /// continuously, so there is no cell to look up. Flip those hulls without this and the dory loses
    /// her oars. A fitting therefore needs to become a mesh on exactly the same terms the hull did.</para>
    ///
    /// <para><b>Why it is a separate asset rather than more fields on <see cref="HullMeshDef"/>.</b>
    /// Two reasons, both structural. A fitting usually comes from its OWN rig with its own palette
    /// (<c>skiffMotorRig.js</c> ships PAINT/TRIM/STEEL/MOTO of its own), so it cannot share the hull's
    /// ramp table. And one fitting serves several hulls — the same outboard fits the console skiff and
    /// both sport skiffs — so folding it into a hull would duplicate the mesh per boat and make "one
    /// entity per file" (rule 2) a lie.</para>
    ///
    /// <para><b>What it deliberately does NOT hold: the articulation itself.</b> There is no "steer"
    /// or "stroke" field anywhere below. Boats already owns that arithmetic (<c>OutboardMotorMath</c>,
    /// <c>DoryOarMath</c>) and hands the renderer a finished local rotation; Art never learns what a
    /// stroke is. That is rule 4, and it is also what lets oars and outboards — which articulate
    /// nothing alike — ride one seam (<see cref="IHullPropRenderer"/>).</para>
    /// </summary>
    public class HullPropMeshDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): hullprop.snake_case.")]
        public string Id = "hullprop.unnamed";

        [Tooltip("The rig source this was extracted from (repo-relative), for provenance and re-bakes.")]
        public string SourceRigPath = "";

        [Tooltip("The rig expression that produced the face list, e.g. \"oarFaces\" or \"motorFaces\". " +
                 "Recorded because — unlike a hull, whose geometry is the rig's static F array — a " +
                 "fitting's faces come from a BUILDER that takes a pose, so which builder (and at " +
                 "which canonical pose) is part of what this asset means.")]
        public string SourceFaceBuilder = "";

        [Header("Geometry")]
        [Tooltip("The fitting's mesh, stored as a sub-asset. Same RigMeshBuilder layout as a hull: " +
                 "flat per-face normals, UV0 = (materialId, faceBias b, depthBias db, 0). Vertices are " +
                 "in the HULL's rig space — the rig's own face builders already inject the mount " +
                 "offset — so posing is a pure rotation about Pivot, with no translation to get wrong.")]
        public Mesh Mesh;

        [Header("Shading (this rig's own pipeline, verbatim)")]
        [Tooltip("Palette ramp + offset per rig material, in the rig's MATS order. A fitting from a " +
                 "separate rig carries a separate table — that is why it needs its own material.")]
        public HullMeshDef.Ramp[] Ramps = Array.Empty<HullMeshDef.Ramp>();

        [Tooltip("The rig's key light LN, normalised, in the rig's own right-handed frame.")]
        public Vector3 LightN = Vector3.forward;
        public float Gain = 1f;
        public float Bias = 0f;

        [Tooltip("The rig's 4×4 ordered-dither thresholds, already (v+0.5)/16, row-major [x*4+y].")]
        public float[] Bayer16 = Array.Empty<float>();

        public Color32 Keyline = new Color32(0, 0, 0, 255);

        [Header("Cell (the fitting's own screen geometry)")]
        [Tooltip("Cell pivot in pixels from the cell's TOP-LEFT, in the FITTING's cell.")]
        public Vector2 PivotPx;
        public int PxPerMetre = 32;

        [Tooltip("⚠️ A fitting's cell is often WIDER than its hull's (the skiff outboard is 272×216 " +
                 "against a 244×216 hull) because the engine swings outboard of the transom. The hull " +
                 "composites through one overlay quad sized from its own cell, so a renderer that " +
                 "ignores this CLIPS the engine at the hull's cell edge — visible as a sheared cowl on " +
                 "hard-over headings. The quad must be sized from the union; see IsoFacetHullRenderer.")]
        public int CellW, CellH;

        [Tooltip("The rig's bake elevation (degrees above the horizon). Must match the hull's, or the " +
                 "fitting is projected through a different camera than the boat it is bolted to.")]
        public float ElevationDeg = 40f;

        [Header("Pose facts (per-artwork; read off the rig — never tuned)")]
        [Tooltip("The point this fitting TURNS ABOUT, in hull-rig metres: the outboard's clamp swivel " +
                 "(just aft of the transom, at clamp height) or the oarlock. The whole pose is a " +
                 "rotation about this point, so it is the one number that must be right — get it wrong " +
                 "and the part still moves, but it pivots about the hull origin and swings through the " +
                 "boat. This project has shipped exactly that bug before on the sprite path " +
                 "(the motor's anti-phase bounce), which is why it is DATA read off the rig rather " +
                 "than a constant at a call site.")]
        public Vector3 PivotLocalMeters;

        [Tooltip("Peak steer, degrees either side of dead ahead (the rig's MOTOR.maxSteer — 30 on the " +
                 "skiff outboard, 32 on the punt's). 0 = this fitting does not steer.")]
        public float MaxSteerDegrees;

        [Tooltip("Peak tilt, degrees (the rig's MOTOR.tiltMax — the leg swinging aft-and-up out of the " +
                 "water). 0 = this fitting does not tilt.")]
        public float MaxTiltDegrees;

        [Tooltip("Lateral clamp positions in metres for a MULTI-fit, e.g. the sport skiff's twin " +
                 "outboard at ±0.34. Empty or a single 0 = one instance on the centreline. The mesh " +
                 "path just instantiates the same fitting once per entry — no second bake, and none " +
                 "of the sprite path's draw-the-far-engine-first ordering rules, because a shared " +
                 "depth buffer sorts them.")]
        public float[] LateralMountsMeters = Array.Empty<float>();

        /// <summary>
        /// True when this def carries everything the render path needs. Mirrors
        /// <see cref="HullMeshDef.IsUsable"/> — a fitting that is not usable must be REFUSED rather
        /// than drawn half-shaded, because a missing ramp table renders as flat magenta on a boat the
        /// owner is sailing.
        /// </summary>
        public bool IsUsable() =>
            Mesh != null && Mesh.vertexCount > 0 &&
            Ramps != null && Ramps.Length > 0 &&
            Bayer16 != null && Bayer16.Length == 16 &&
            CellW > 0 && CellH > 0 && PxPerMetre > 0;

        /// <summary>
        /// The lateral mounts, normalised: a def that names none still yields one instance on the
        /// centreline. Callers iterate this rather than the raw field so "no mounts declared" cannot
        /// silently mean "no engine".
        /// </summary>
        public float[] EffectiveMounts =>
            LateralMountsMeters != null && LateralMountsMeters.Length > 0
                ? LateralMountsMeters
                : SingleCentreMount;

        /// <summary>
        /// The mounts to instantiate this fitting at for a given FIT.
        ///
        /// <para><b>The fit belongs to the BOAT, not to the fitting.</b> One skiff outboard def serves
        /// three visuals — the console skiff and the sport skiff single (one engine, centreline) and
        /// the sport skiff twin (two, at the ±0.34 m its rig declares) — so reading
        /// <see cref="EffectiveMounts"/> unconditionally would bolt two engines onto the workboat.
        /// A multi fit on a def that declares fewer than two mounts falls back to one on the
        /// centreline rather than inventing a spacing.</para>
        /// </summary>
        public float[] MountsForFit(bool multiEngine) =>
            multiEngine && LateralMountsMeters != null && LateralMountsMeters.Length >= 2
                ? LateralMountsMeters
                : SingleCentreMount;

        static readonly float[] SingleCentreMount = { 0f };
    }
}
