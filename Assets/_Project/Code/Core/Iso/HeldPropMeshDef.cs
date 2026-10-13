using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A prop a figure HOLDS, as a facet mesh (ADR 0053)</b> — the rod, the clam shovel, a fish by
    /// the tail, a pail or a fish tray at one fill. Everything the facet render path needs to draw the
    /// thing in a hand, plus the one fact a hand needs to hold it: where the grip is.
    ///
    /// <para><b>Why a new type and not <see cref="HullPropMeshDef"/> (D3).</b> A hull fitting is
    /// POSED by rotating about a pivot — that is its whole contract, and its fields say so (steer,
    /// tilt, mounts). A held prop is not posed at all: it keeps its rig's own held pose and rides the
    /// hand (D1), so the fact it needs is the GRIP, and it carries two things a fitting never has —
    /// the rig's thin plotted lines (the rod blank, the shovel shaft, the bail, a fish's eyes) and
    /// the rig's paint rules, which differ prop by prop. Folding both into the boats' def would
    /// give every outboard a grip and every rod a steering limit.</para>
    ///
    /// <para><b>One asset per held STATE</b> (rule 2): a rod tier, a species, a bucket tier at one
    /// fill. A pail at "half, shell" is different geometry from "full, fish" — different faces, not
    /// different colours — so it is a different mesh, exactly as it is a different sprite cell.</para>
    ///
    /// <para><b>What reads this today: nothing.</b> PR 2 bakes and references the meshes; PR 4
    /// presents them on the figure's hand sockets and PR 3 draws the plotted lines. Until then the
    /// sprites are the picture and this asset is inert data.</para>
    /// </summary>
    public class HeldPropMeshDef : ScriptableObject
    {
        /// <summary>The families the held-prop bake knows. A family is one rig; its states are the
        /// variants that rig enumerates (rod tiers, species, bucket tier × fill × catch).</summary>
        public const string RodFamily = "rod";
        public const string ShovelFamily = "shovel";
        public const string FishFamily = "fish";
        public const string BucketFamily = "bucket";

        /// <summary>Every held-prop id starts here (CLAUDE.md §5: <c>type.snake_case</c>).</summary>
        public const string IdPrefix = "heldprop.";

        /// <summary>Ramp slots the facet shader's <c>_RampMeta</c> holds — the hull fleet's law,
        /// shared, never widened here. The bake keeps only the ramps a prop's faces USE, so no held
        /// prop comes near it (the widest uses five).</summary>
        public const int RampSlots = HullMeshDef.HullRampSlots;

        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): heldprop.snake_case — heldprop.rod_cane, " +
                 "heldprop.fish_cod, heldprop.bucket_pail_half_shell. Saves never store it today, but " +
                 "ids outlive assets, so a withdrawn one is retired, never re-used.")]
        public string Id = "heldprop.unnamed";

        [Tooltip("Which rig family this state belongs to: rod / shovel / fish / bucket.")]
        public string Family = "";

        [Tooltip("The state inside the family, in the rig's own words: a rod tier (cane), a species " +
                 "(cod), a bucket state (pail_half_shell). Empty for a family with one state (the " +
                 "shovel).")]
        public string Variant = "";

        [Header("Provenance (re-bakes and the bake guards read these)")]
        [Tooltip("The rig source this was extracted from (repo-relative).")]
        public string SourceRigPath = "";

        [Tooltip("SHA-256 of the rig source as LF text, at bake time. A rig edit without a re-bake " +
                 "is caught by comparing this against the file, not by eye.")]
        public string SourceRigSha256 = "";

        [Tooltip("The rig expression that produced the face list, e.g. held.faces('cane').")]
        public string SourceFaceBuilder = "";

        [Tooltip("The rig expression that produced the material table, e.g. held.mats('cane').")]
        public string SourceMaterials = "";

        [Tooltip("The held pose the faces were built at, as the rig resolved it — read off the rig, " +
                 "never tuned (D1).")]
        public string SourcePose = "";

        [Tooltip("Faces in the rig's own list, before a quad's repeated corner was dropped. The bake " +
                 "guard compares it with the rig's builder.")]
        public int SourceFaceCount;

        [Header("Geometry")]
        [Tooltip("The prop's mesh, a sub-asset. RigMeshBuilder's layout, the same as a hull's: flat " +
                 "per-face normals, UV0 = (materialId, faceBias b, depthBias db, 0). Vertices are in the " +
                 "prop's own metres with the GRIP at the origin, so the hand socket is the transform.")]
        public Mesh Mesh;

        [Header("Shading (this rig's own pipeline, verbatim)")]
        [Tooltip("Palette ramp + offset per material, USED ones only, in the rig's MATS order (at most " +
                 "RampSlots). The mesh's material ids index this table.")]
        public HullMeshDef.Ramp[] Ramps = Array.Empty<HullMeshDef.Ramp>();

        [Tooltip("The rig's name for each entry of Ramps, same order — what a guard compares by.")]
        public string[] RampNames = Array.Empty<string>();

        [Tooltip("The rig's key light LN, normalised, in the rig's own right-handed frame.")]
        public Vector3 LightN = Vector3.forward;
        public float Gain = 1f;
        public float Bias = 0f;

        [Tooltip("The rig's 4×4 ordered-dither thresholds, already (v+0.5)/16, row-major [x*4+y].")]
        public float[] Bayer16 = Array.Empty<float>();

        public Color32 Keyline = new Color32(0, 0, 0, 255);

        [Header("Paint rules (facts about the rig's rasteriser that its data cannot carry)")]
        [Tooltip("true = a back-lit face is re-lit only when its bias b ≤ −1 (the hull rule, which the " +
                 "rod, shovel and bucket rigs use). The fish rig rescues its fins by a per-face flag " +
                 "the mesh does not carry; it bakes the hull rule, see ADR 0053.")]
        public bool BackfaceRescueNeedsOptIn = true;

        [Tooltip("Whether the rig's render darkens a 1 px depth discontinuity. All four held rigs do.")]
        public bool DepthEdgeDarkening = true;

        [Tooltip("The rig's EDGE: the depth step, in metres, that counts as a discontinuity (0.12; the " +
                 "bucket rig's is 0.13).")]
        public float EdgeDepthMeters;

        [Tooltip("Whether the rig draws its 1 px outer keyline. The fish rig gates it off by default " +
                 "(KEYLINE_DEFAULT false); the rod, shovel and bucket always draw it.")]
        public bool KeylineOutline = true;

        [Tooltip("The turn direction the rig's facings were baked with, as RigCatalog DECLARES it — not " +
                 "measured: the boat probe's bow-taper test cannot read a pail or a rod. The proof sheet " +
                 "(mesh beside sprite at 8 facings) is the pixel check.")]
        public bool AzimuthCounterClockwise;

        [Header("Cell (the prop's own screen geometry)")]
        [Tooltip("The cell pivot in pixels from the cell's TOP-LEFT — the grip's pixel in the sprite.")]
        public Vector2 PivotPx;
        public int PxPerMetre = 32;
        public int CellW, CellH;

        [Tooltip("The rig's bake elevation (degrees above the horizon).")]
        public float ElevationDeg = 40f;

        [Header("Hold")]
        [Tooltip("The grip, in the mesh's metres, measured from the rig's own pose: the rod's " +
                 "gripLocal(), the shovel shaft at s = 0, the bucket's bail apex or tray near-rim after " +
                 "the carry offset, the fish's centreline at its gripU. The origin by construction; the " +
                 "bake guard asserts it.")]
        public Vector3 GripLocalMeters;

        [Header("Plotted lines (data for the facet shader's thin-line pass, PR 3 — nothing draws them yet)")]
        [Tooltip("The ramps the plotted lines paint from. LinePoint.Ramp indexes this table.")]
        public HullMeshDef.Ramp[] LineRamps = Array.Empty<HullMeshDef.Ramp>();

        [Tooltip("The thin parts the rig PLOTS instead of building as faces — the rod blank, the shovel " +
                 "shaft, the pail's bail — and the fish's two eye dots. Sampled exactly as the rig " +
                 "samples them, so a line pass can re-plot them at any facing.")]
        public Line[] Lines = Array.Empty<Line>();

        [Header("Bucket state (the bucket family only; empty otherwise)")]
        [Tooltip("The bucket rig's tier id: pail / tote / tray.")]
        public string BucketTierId = "";

        [Tooltip("The bucket rig's fill: empty / few / half / full / brim.")]
        public string BucketFill = "";

        [Tooltip("The bucket rig's catch group: fish / shell / crust. Empty when the fill is empty.")]
        public string BucketCatch = "";

        /// <summary>One plotted line, sampled the way its rig samples it.</summary>
        [Serializable]
        public struct Line
        {
            [Tooltip("What the line is: blank / shaft / bail / eyes.")]
            public string Name;

            [Tooltip("The samples, in the rig's order.")]
            public LinePoint[] Points;

            [Tooltip("How much nearer than its depth a sample is tested and written — the rig's plot " +
                     "bias (0.03 m for a line, 0.05 m for the eyes).")]
            public float DepthBiasMeters;

            [Tooltip("false = the rig's LINE rule: each sample is the pixel at floor(x, y), drawn when " +
                     "d − DepthBias is nearer than the depth there, which it then becomes; when the sample " +
                     "moved from the previous one a second pixel goes at (x, y+1) if |dx| ≥ |dy| else " +
                     "(x+1, y), painted SecondIndex.\n" +
                     "true = the rig's EYE rule: one pixel at round(x, y), painted only over a pixel the " +
                     "faces already inked and only when d − DepthBias is nearer than the depth there. It " +
                     "leaves that depth alone; only the edge pass sees the eye's own d.")]
            public bool Dots;
        }

        /// <summary>One sample of a plotted line.</summary>
        [Serializable]
        public struct LinePoint
        {
            [Tooltip("The sample, in the mesh's metres (grip at the origin).")]
            public Vector3 Position;

            [Tooltip("Index into LineRamps.")]
            public int Ramp;

            [Tooltip("The ramp entry the sample paints.")]
            public int Index;

            [Tooltip("The ramp entry the second pixel paints, or −1 where the rig draws none.")]
            public int SecondIndex;
        }

        /// <summary>
        /// True when this def carries everything the facet render path needs — the same test as
        /// <see cref="HullPropMeshDef.IsUsable"/>, plus the slot law. A def that is not usable must
        /// be REFUSED rather than drawn half-shaded.
        /// </summary>
        public bool IsUsable() =>
            Mesh != null && Mesh.vertexCount > 0 &&
            Ramps != null && Ramps.Length > 0 && Ramps.Length <= RampSlots &&
            Bayer16 != null && Bayer16.Length == 16 &&
            CellW > 0 && CellH > 0 && PxPerMetre > 0;
    }
}
