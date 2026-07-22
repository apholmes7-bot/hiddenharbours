using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A rig-extracted 3D hull, as a committed asset (ADR 0022 phase 4).</b> Everything the facet
    /// render path needs to draw one hull mesh, in plain engine types: the mesh itself (a sub-asset),
    /// the rig's palette ramps and lighting constants, its dither matrix, its cell geometry — and the
    /// two per-artwork facts gameplay needs to POSE it (the measured azimuth convention and the rig's
    /// own rock amplitudes).
    ///
    /// <para><b>Builder-generated and committed</b>, like the <c>BoatVisualDef</c>s: the owner does
    /// not run anything to get a mesh hull, and re-runs the baker
    /// (Hidden Harbours ▸ Art ▸ 3D Hulls ▸ Bake…) only when the art director's rig changes. Phase 3
    /// deliberately did not invent this format ("phase 4 owns turning this into a baked asset") —
    /// this is that format.</para>
    ///
    /// <para><b>Why it lives in Core.</b> The Boats module poses the hull (heading, wave rock) and
    /// the Art module draws it (the facet URP pass), and neither may reference the other
    /// (CLAUDE.md rule 4). This asset is the data they share, so it lives in the module both are
    /// allowed to see — the same reasoning as <c>CharacterVisualDef</c>. It deliberately contains no
    /// URP type: Art converts it to its own runtime setup on install.</para>
    ///
    /// <para>⚠️ <b><see cref="AzimuthCounterClockwise"/> is MEASURED, never assumed.</b> The baker
    /// runs <c>RigAzimuthProbe</c> over the rig's own rendered pixels — the declared facing order has
    /// been wrong five times in this project, and every time because someone trusted a declaration
    /// (see <c>iso-art-baked-counter-clockwise</c>). This flag is the mesh path's whole heading
    /// mapping: get it wrong and she sails stern-first at E/W. The end-to-end acceptance test
    /// compares the mesh render against her baked sheet through this very field, so a flip goes red
    /// in pixels, not in a code review.</para>
    /// </summary>
    public class HullMeshDef : ScriptableObject
    {
        /// <summary>One rig material: a palette ramp plus its constant shade-index offset.</summary>
        [Serializable]
        public struct Ramp
        {
            [Tooltip("The palette ramp, dark to light, exactly as the rig's MATS entry holds it.")]
            public Color32[] Colors;
            [Tooltip("The rig material's constant ramp-index offset ('off'; the blk/dark aliases are negative).")]
            public int Offset;
        }

        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): hullmesh.snake_case.")]
        public string Id = "hullmesh.unnamed";

        [Tooltip("The rig source this was extracted from (repo-relative), for provenance and re-bakes.")]
        public string SourceRigPath = "";

        [Header("Geometry (extracted by RigMeshExtractor, built by RigMeshBuilder)")]
        [Tooltip("The hull mesh, stored as a sub-asset of this def. RigMeshBuilder layout: flat " +
                 "per-face normals, UV0 = (materialId, faceBias b, depthBias db, 0).")]
        public Mesh Mesh;

        [Header("Shading (the rig's own pipeline, verbatim)")]
        [Tooltip("Palette ramp + offset per rig material, in the rig's MATS order (max 16 — the facet shader's _RampMeta).")]
        public Ramp[] Ramps = Array.Empty<Ramp>();

        [Tooltip("The rig's key light LN, normalised, in the rig's own right-handed frame. The Art side " +
                 "applies the reflection sign — this is handed over untouched.")]
        public Vector3 LightN = Vector3.forward;
        public float Gain = 1f;
        public float Bias = 0f;

        [Tooltip("The rig's 4×4 ordered-dither thresholds, already (v+0.5)/16, row-major [x*4+y].")]
        public float[] Bayer16 = Array.Empty<float>();

        public Color32 Keyline = new Color32(0, 0, 0, 255);

        [Header("Cell (the rig's screen geometry)")]
        [Tooltip("Cell pivot in pixels from the cell's TOP-LEFT — the rig's screen origin.")]
        public Vector2 PivotPx;
        public int PxPerMetre = 32;
        public int CellW, CellH;
        [Tooltip("The rig's bake elevation (degrees above the horizon; 40 for the boat rigs).")]
        public float ElevationDeg = 40f;

        [Header("Pose facts (per-artwork; measured or read off the rig — never tuned)")]
        [Tooltip("MEASURED azimuth convention (RigAzimuthProbe over rendered pixels): true = the rig's " +
                 "dir argument turns the hull COUNTER-CLOCKWISE (dir d depicts compass heading −45°·d), " +
                 "so the compass→dir mapping negates. True of every boat rig measured so far. " +
                 "⚠️ Load-bearing: flipping it mirrors the hull's heading end to end.")]
        public bool AzimuthCounterClockwise = true;

        [Tooltip("The rig's ROCK.rollA — peak roll (degrees) of its canned rock cycle. 0 = no rock.")]
        public float RockRollDegrees;
        [Tooltip("The rig's ROCK.pitchA — peak pitch (degrees).")]
        public float RockPitchDegrees;
        [Tooltip("The rig's ROCK.heaveA — peak heave (rig PIXELS; world metres = px / PxPerMetre).")]
        public float RockHeavePixels;

        /// <summary>
        /// True when this def can actually be drawn: a mesh, at least one non-empty ramp (≤ the
        /// shader's 16), a full 4×4 dither matrix and sane cell geometry. The skinner gates the mesh
        /// path on this and falls back to the sprite compass when it fails — an incomplete def must
        /// degrade to the shipped look, never to an invisible boat.
        /// </summary>
        public bool IsUsable()
        {
            if (Mesh == null) return false;
            if (Ramps == null || Ramps.Length == 0 || Ramps.Length > 16) return false;
            for (int i = 0; i < Ramps.Length; i++)
                if (Ramps[i].Colors == null || Ramps[i].Colors.Length == 0) return false;
            if (Bayer16 == null || Bayer16.Length != 16) return false;
            return PxPerMetre > 0 && CellW > 0 && CellH > 0;
        }
    }
}
