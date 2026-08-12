using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One paint scheme for one hull: her material table in another colour, and nothing else.</b>
    ///
    /// <para><b>Why this is a ramp table and not a mesh, a sheet or a shader.</b> A rig mesh carries
    /// its material INDEX per vertex (<c>RigMeshBuilder</c> layout: UV0 = (materialId, b, db, 0)) and
    /// resolves the colour at draw time out of <see cref="HullMeshDef.Ramps"/>, which the facet
    /// renderer uploads as a per-instance ramp texture. So the geometry never knew its own colour —
    /// swapping the table is the whole of a repaint, and every scheme shares one mesh, one silhouette
    /// and one draw call. MEASURED on the lobster boat's paint kit (2026-08-12): her 676-face list is
    /// byte-identical with and without paint, vertices to 1e-9, and all 12 schemes share an alpha mask
    /// at every one of the 8 headings.</para>
    ///
    /// <para><b>What the alternative cost.</b> Re-baking her SHEET family per scheme — the road this
    /// design rejected — is 3 sheets totalling 30.6 M pixels per scheme: 116.9 MB of RGBA32 for one
    /// paint job, 1.37 GB for twelve. This asset is ~250 bytes. That is not a trade, which is why it
    /// was not escalated as one.</para>
    ///
    /// <para><b>The table is BAKED, never typed.</b> <c>HullPaintSchemeBaker</c> reads it out of the
    /// rig's own resolver (<c>matsFor(id)</c>) through the same extractor path that writes
    /// <see cref="HullMeshDef.Ramps"/>. No ramp is ever transcribed into C# — the one habit that has
    /// shipped mirrored and miscoloured boats in this repo before.</para>
    ///
    /// <para>⚠️ <b><see cref="Ramps"/> is POSITIONAL and its order is the rig's MATS order.</b> Index
    /// 0 is whatever the rig's first material is, not "the hull". A table baked against a different
    /// rig — or against a rig whose material list has since grown — recolours the boat wrongly and
    /// silently, so <see cref="IsUsableFor"/> gates on the hull id AND the arity, and the renderer
    /// falls back to the def's own ramps rather than draw a lie.</para>
    /// </summary>
    public class HullPaintSchemeDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): paint.snake_case, e.g. paint.harbour.")]
        public string Id = "paint.";

        [Tooltip("The name the harbour uses — 'HARBOUR NAVY'. From the rig's own PAINTS table.")]
        public string Label = "";

        [Tooltip("The art director's one-line description of the scheme. Verbatim from the rig.")]
        public string Note = "";

        [Header("Provenance (what this table was baked FROM, and what it fits)")]
        [Tooltip("The HullMeshDef.Id this table is a repaint OF ('hullmesh.lobster_boat_iso'). A " +
                 "scheme is only meaningful against the material table it was baked beside.")]
        public string HullMeshId = "";

        [Tooltip("The rig source (repo-relative), for provenance and re-bakes.")]
        public string SourceRigPath = "";

        [Tooltip("The rig's OWN id for this scheme ('harbour'), i.e. the argument the baker passed " +
                 "to the rig's paint resolver. Kept so a re-bake is reproducible from the asset.")]
        public string RigPaintId = "";

        [Header("The paint (baked from the rig — never typed by hand)")]
        [Tooltip("The FULL material table in the rig's MATS order — the same shape and order as " +
                 "HullMeshDef.Ramps, so the renderer substitutes it wholesale. Positional: index n " +
                 "is the rig's nth material, whether or not this scheme repaints it.")]
        public HullMeshDef.Ramp[] Ramps = Array.Empty<HullMeshDef.Ramp>();

        /// <summary>
        /// True when this table can stand in for <paramref name="def"/>'s: same hull, same number of
        /// materials, every ramp non-empty and within the facet shader's 16.
        ///
        /// <para>The arity check is the load-bearing one. Ramps are matched BY INDEX, so a table one
        /// entry short does not colour 10 of 11 materials — it shifts every material after the gap
        /// onto its neighbour's ramp, which repaints the glass with the boot-top and looks like a
        /// shader bug rather than a stale asset.</para>
        /// </summary>
        public bool IsUsableFor(HullMeshDef def)
        {
            if (def == null || Ramps == null) return false;
            if (Ramps.Length == 0 || Ramps.Length > 16) return false;
            if (def.Ramps == null || Ramps.Length != def.Ramps.Length) return false;
            if (!string.IsNullOrEmpty(HullMeshId) && HullMeshId != def.Id) return false;
            for (int i = 0; i < Ramps.Length; i++)
                if (Ramps[i].Colors == null || Ramps[i].Colors.Length == 0) return false;
            return true;
        }

        /// <summary>Why <see cref="IsUsableFor"/> said no, for the log line the renderer prints
        /// before falling back. Empty when it said yes.</summary>
        public string ExplainUnusableFor(HullMeshDef def)
        {
            if (def == null) return "no hull def";
            if (Ramps == null || Ramps.Length == 0) return $"'{Id}' has no ramps";
            if (Ramps.Length > 16) return $"'{Id}' has {Ramps.Length} ramps; the facet shader holds 16";
            if (def.Ramps == null) return $"hull '{def.Id}' has no ramps";
            if (Ramps.Length != def.Ramps.Length)
                return $"'{Id}' has {Ramps.Length} ramps but hull '{def.Id}' has {def.Ramps.Length} — " +
                       "ramps match by index, so a mismatched table recolours the wrong materials. Re-bake it.";
            if (!string.IsNullOrEmpty(HullMeshId) && HullMeshId != def.Id)
                return $"'{Id}' was baked for '{HullMeshId}', not '{def.Id}'";
            for (int i = 0; i < Ramps.Length; i++)
                if (Ramps[i].Colors == null || Ramps[i].Colors.Length == 0)
                    return $"'{Id}' ramp {i} is empty";
            return "";
        }
    }
}
