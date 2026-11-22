using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A rig-baked PARKED DWELLING, as a committed asset (ADR 0060, option B).</b> The third door onto
    /// the facet renderer, beside <see cref="HullMeshDef"/> and <see cref="VehicleMeshDef"/>: the same
    /// render payload (mesh, the rig's ramps and lighting, its dither matrix, its cell), plus what a home
    /// that does not move needs and a hull or a truck does not: a floor outline per level, a threshold,
    /// and the fixture anchors the prefab builder binds to Def ids.
    ///
    /// <para><b>Why a separate type.</b> Every boat field (draft, rock, watertight clamp, foam stern) and
    /// every vehicle field (wheelbase, steer, suspension) would read zero on a camper, and "0 = not
    /// applicable" is the shape that lets a consumer read a draft off a house. The hull laws (coverage by
    /// <c>rollA</c>, deck tables, the converted-hull predicate) would also have to learn an exception
    /// each. See ADR 0060, option A.</para>
    ///
    /// <para><b>Why it lives in Core.</b> The Art module draws it, the World module decides when its room
    /// is open (<c>BuildingInterior</c> stays the one definition of inside), and neither may name the
    /// other's classes (rule 4). It holds no URP type: Art converts it to its own setup on install.</para>
    ///
    /// <para><b>Shell and room are one mesh</b> (<c>subMeshCount = 1</c>), one draw per dwelling. The room's
    /// faces carry their level's tag in <c>TexCoord1.x</c>; the exterior below the floor (chassis, wheels,
    /// steps) is tag 0 and never cut. Written by the dwelling baker; never hand-edited.</para>
    /// </summary>
    public class DwellingMeshDef : ScriptableObject
    {
        /// <summary>Entries in the rig's ordered-dither matrix (4 by 4).</summary>
        public const int DitherMatrixLength = 16;

        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md section 5): dwelling.snake_case, one per variant " +
                 "(dwelling.camper_clipper, dwelling.lot_shed).")]
        public string Id = "dwelling.unnamed";

        [Tooltip("The shell rig this was baked from (repo-relative), for provenance and re-bakes.")]
        public string SourceRigPath = "";

        [Tooltip("The room rig, when the room is a separate rig (the camper). Empty when one rig draws " +
                 "shell and room (the shed).")]
        public string SourceRoomRigPath = "";

        [Tooltip("The variant expression the faces came from, when one rig draws several dwellings. " +
                 "Empty = the rig's single build.")]
        public string SourceFaceBuilder = "";

        [Header("Geometry")]
        [Tooltip("Shell and room in one mesh. RigMeshBuilder layout: flat per-face normals, UV0 = " +
                 "(materialId, faceBias, depthBias, 0); TexCoord1.x = the face's level tag.")]
        public Mesh Mesh;

        [Header("Shading (the rig's own pipeline, verbatim)")]
        [Tooltip("Palette ramp per shell material (at most HullMeshDef.HullRampSlots).")]
        public HullMeshDef.Ramp[] Ramps = Array.Empty<HullMeshDef.Ramp>();

        [Tooltip("Palette ramp per room material, its own index space (at most " +
                 "HullMeshDef.InteriorRampSlots). Empty on a dwelling with no room geometry.")]
        public HullMeshDef.Ramp[] InteriorRamps = Array.Empty<HullMeshDef.Ramp>();

        [Tooltip("The rig's key light, normalised, in the rig's own frame.")]
        public Vector3 LightN = Vector3.forward;
        public float Gain = 1f;
        public float Bias = 0f;

        [Tooltip("The rig's 4x4 ordered-dither thresholds, already (v+0.5)/16, row-major [x*4+y].")]
        public float[] Bayer16 = Array.Empty<float>();

        public Color32 Keyline = new Color32(0, 0, 0, 255);

        [Header("Cell (the rig's screen geometry)")]
        [Tooltip("Cell pivot in pixels from the cell's top-left: the rig's screen origin, at the ground " +
                 "centre of the body footprint. This is also the dwelling's sort point.")]
        public Vector2 PivotPx;
        public int PxPerMetre = 32;
        public int CellW, CellH;
        [Tooltip("The rig's bake elevation, degrees above the horizon (the shared camera).")]
        public float ElevationDeg = 40f;

        [Header("Pose facts (measured at bake, never tuned)")]
        [Tooltip("Measured azimuth convention: true = the rig's dir argument turns the dwelling " +
                 "counter-clockwise. The site's ground bearing maps through this once, at install.")]
        public bool AzimuthCounterClockwise = true;

        [Tooltip("The compass heading, degrees, that the rig's dir 0 depicts.")]
        public float ZeroHeadingDegrees = 0f;

        [Header("Room (read off the shell and room sidecars)")]
        [Tooltip("One row per walkable level: its face tag, its lid, its storey and its floor outline.")]
        public DwellingLevel[] Levels = Array.Empty<DwellingLevel>();

        [Tooltip("The door, as the threshold BuildingInterior crosses.")]
        public DwellingThreshold Threshold;

        [Tooltip("Every fixture the room declares, with its verb and, where it has one, its Def id.")]
        public DwellingFixtureAnchor[] Anchors = Array.Empty<DwellingFixtureAnchor>();

        /// <summary>True when the bake appended room geometry with its own palette.</summary>
        public bool HasRoom() => InteriorRamps != null && InteriorRamps.Length > 0;

        /// <summary>The level named <paramref name="levelId"/>, or false.</summary>
        public bool TryGetLevel(string levelId, out DwellingLevel level)
        {
            int i = DwellingMeshRules.IndexOfLevel(Levels, levelId);
            level = i >= 0 ? Levels[i] : default;
            return i >= 0;
        }

        /// <summary>The fixture anchor named <paramref name="anchorId"/>, or false.</summary>
        public bool TryGetAnchor(string anchorId, out DwellingFixtureAnchor anchor)
        {
            int i = DwellingMeshRules.IndexOfAnchor(Anchors, anchorId);
            anchor = i >= 0 ? Anchors[i] : default;
            return i >= 0;
        }

        /// <summary>
        /// The cut that opens <paramref name="levelId"/>, in the renderer's own type, ready for
        /// <see cref="IHullCutaway.ShowCutaway"/>. <see cref="HullMeshDef.Cut.None"/> for an unknown or
        /// open level. Called on a threshold transition, not per frame.
        /// </summary>
        public HullMeshDef.Cut CutForLevel(string levelId) => DwellingMeshRules.CutFor(Levels, levelId);

        /// <summary>
        /// The first reason this def cannot be drawn and entered, or null when it can. The render half
        /// mirrors <see cref="HullMeshDef.IsUsable"/>; the room half is
        /// <see cref="DwellingMeshRules.FirstRoomProblem"/>. Allocates only when it finds a problem.
        /// </summary>
        public string FirstProblem()
        {
            if (Mesh == null || Mesh.vertexCount == 0) return "Mesh is missing or empty";
            string ramps = RampProblem(Ramps, nameof(Ramps), 1, HullMeshDef.HullRampSlots);
            if (ramps != null) return ramps;
            string interior = RampProblem(InteriorRamps, nameof(InteriorRamps), 0, HullMeshDef.InteriorRampSlots);
            if (interior != null) return interior;
            if (Bayer16 == null || Bayer16.Length != DitherMatrixLength)
                return $"Bayer16 must hold {DitherMatrixLength} entries";
            if (PxPerMetre <= 0 || CellW <= 0 || CellH <= 0) return "Cell geometry is not set";
            return DwellingMeshRules.FirstRoomProblem(Levels, Threshold, Anchors);
        }

        /// <summary>True when this def can be installed and entered. An unusable def is refused, never
        /// drawn half-shaded or entered through a wall.</summary>
        public bool IsUsable() => FirstProblem() == null;

        private static string RampProblem(HullMeshDef.Ramp[] ramps, string field, int min, int max)
        {
            int count = ramps?.Length ?? 0;
            if (count < min || count > max) return $"{field}: {count} ramps, expected {min} to {max}";
            for (int i = 0; i < count; i++)
                if (ramps[i].Colors == null || ramps[i].Colors.Length == 0) return $"{field}[{i}] is empty";
            return null;
        }
    }
}
