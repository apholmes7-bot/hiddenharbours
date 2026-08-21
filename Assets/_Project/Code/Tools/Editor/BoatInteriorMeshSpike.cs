using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.Editor
{
    /// <summary>
    /// <b>S0 SPIKE — what, exactly, is the EXTERIOR half of ADR 0038's layer swap on a MESH hull?</b>
    ///
    /// <para>#622 shipped the interior runtime and flagged, rather than guessed at, the one thing the
    /// ADR left ambiguous: proposal 3 says "the cabin's occluding SHEET turns off", but it also says
    /// "the house is currently an occluder: it is GEOMETRY". On a sprite hull those are the same
    /// object; on a mesh hull they are not, and the pilotable fleet is mesh (ADR 0022). This probe
    /// MEASURES the difference instead of arguing about it.</para>
    ///
    /// <para><b>It decides nothing and changes nothing.</b> It loads committed assets, reads the baked
    /// meshes, and writes a report. No asset is written, no def is touched, nothing is re-baked — the
    /// coordinator rules on the numbers.</para>
    ///
    /// <para>Run: <b>Hidden Harbours ▸ Dev ▸ Spikes ▸ Boat Interior — Exterior Half</b>, or headless
    /// with <c>-executeMethod HiddenHarbours.Tools.Editor.BoatInteriorMeshSpike.Cli</c>. The report
    /// path can be overridden with <c>-spikeOut &lt;path&gt;</c>.</para>
    /// </summary>
    public static class BoatInteriorMeshSpike
    {
        private const string InteriorFolder = "Assets/_Project/Data/Boats/Interiors";
        private const string VisualFolder = "Assets/_Project/Data/Boats/Visuals";
        private const string DefaultOut = "boat-interior-mesh-spike.txt";

        [MenuItem("Hidden Harbours/Dev/Spikes/Boat Interior — Exterior Half")]
        public static void Run()
        {
            string report = Measure();
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, DefaultOut);
            File.WriteAllText(path, report);
            Debug.Log($"[BoatInteriorMeshSpike] wrote {path}\n{report}");
        }

        /// <summary>Headless entry point. Never throws past the logger: a spike that kills the editor
        /// tells you nothing.</summary>
        public static void Cli()
        {
            string outPath = null;
            string[] args = System.Environment.GetCommandLineArgs();   // ⚠ NOT bare Environment: inside
                                                                 // HiddenHarbours.Tools.Editor that binds to
                                                                 // HiddenHarbours.Environment, the module.
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], "-spikeOut", StringComparison.Ordinal)) outPath = args[i + 1];

            try
            {
                string report = Measure();
                if (string.IsNullOrEmpty(outPath))
                    outPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, DefaultOut);
                File.WriteAllText(outPath, report);
                Debug.Log($"[BoatInteriorMeshSpike] OK -> {outPath}\n{report}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoatInteriorMeshSpike] FAILED: {e}");
            }
        }

        private static string Measure()
        {
            var sb = new StringBuilder();
            sb.AppendLine("BOAT INTERIOR — THE EXTERIOR HALF OF THE SWAP, MEASURED (ADR 0038 S0 spike)");
            sb.AppendLine("Loads committed assets only. Writes nothing. Numbers, not opinions.");
            sb.AppendLine();

            string[] guids = AssetDatabase.FindAssets("t:BoatInteriorDef", new[] { InteriorFolder });
            Array.Sort(guids);
            sb.AppendLine($"interior defs found: {guids.Length}");
            sb.AppendLine();

            int meshCount = 0, spriteCount = 0, noVisual = 0, noMesh = 0;
            int anySubmeshSplit = 0, anyMaterialConfined = 0;
            var lines = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var interior = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(path);
                if (interior == null) continue;

                string stem = Path.GetFileNameWithoutExtension(path);
                string visualPath = $"{VisualFolder}/{stem}.asset";
                var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(visualPath);

                sb.AppendLine(new string('=', 78));
                sb.AppendLine($"{stem}   ({interior.Id})");
                sb.AppendLine($"   px/m {interior.PixelsPerMetre}   cell {interior.CellPixels.x}x{interior.CellPixels.y}" +
                              $"   pivot {interior.PivotPixels.x},{interior.PivotPixels.y}");

                // --- levels, as the sidecar measured them --------------------------------------
                float houseZ = float.NaN, lowestZ = float.NaN, highestZ = float.NaN;
                var levelBits = new List<string>();
                if (interior.Levels != null)
                {
                    foreach (BoatInteriorLevel l in interior.Levels)
                    {
                        if (l == null) continue;
                        levelBits.Add($"{l.Id}@{l.SoleZMeters.ToString("0.###", CultureInfo.InvariantCulture)}");
                        if (float.IsNaN(lowestZ) || l.SoleZMeters < lowestZ) lowestZ = l.SoleZMeters;
                        if (float.IsNaN(highestZ) || l.SoleZMeters > highestZ) highestZ = l.SoleZMeters;
                        if (l.Id != null && l.Id.Contains("house")) houseZ = l.SoleZMeters;
                    }
                }
                if (float.IsNaN(houseZ)) houseZ = highestZ;   // no level literally named "house"
                sb.AppendLine($"   levels: {string.Join(", ", levelBits)}");
                sb.AppendLine($"   door sill z {interior.Door?.ThresholdPoint.z.ToString("0.###", CultureInfo.InvariantCulture)}" +
                              $"   cue {interior.Door?.CueFrames}f @ {interior.Door?.CueMillisecondsPerFrame}ms");

                if (visual == null) { noVisual++; sb.AppendLine("   ⚠ NO VISUAL DEF"); continue; }

                bool isMesh = visual.Variant == BoatHullVariant.Mesh;
                HullMeshDef meshDef = visual.HullMesh;

                if (isMesh) meshCount++; else spriteCount++;
                sb.AppendLine($"   BoatVisualDef.Variant = {(isMesh ? "MESH" : "SPRITE")}" +
                              $"   HullMesh = {(meshDef != null ? meshDef.name : "<none>")}" +
                              $"   sprite compass cells = {(visual.Facings != null ? visual.Facings.Length : 0)}");

                if (meshDef == null || meshDef.Mesh == null) { noMesh++; sb.AppendLine("   ⚠ NO BAKED MESH"); continue; }

                Mesh mesh = meshDef.Mesh;
                sb.AppendLine($"   mesh: subMeshCount={mesh.subMeshCount}  verts={mesh.vertexCount}  " +
                              $"tris={mesh.triangles.Length / 3}   watertightDeckZ=" +
                              meshDef.WatertightDeckHeightMeters.ToString("0.###", CultureInfo.InvariantCulture));
                if (mesh.subMeshCount > 1) anySubmeshSplit++;

                // --- the per-face attribute channel: UV0 = (materialId, b, db, interiorFlag) ----
                var uv = new List<Vector4>();
                mesh.GetUVs(0, uv);
                Vector3[] verts = mesh.vertices;
                int[] tris = mesh.triangles;

                var mats = new Dictionary<int, MatStat>();
                int interiorFlagged = 0, faces = tris.Length / 3;
                int aboveHouse = 0, belowHouse = 0, straddling = 0;

                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    int mat = uv.Count > a ? Mathf.RoundToInt(uv[a].x) : -1;
                    bool flagged = uv.Count > a && uv[a].w > 0.5f;
                    if (flagged) interiorFlagged++;

                    float zlo = Mathf.Min(verts[a].z, Mathf.Min(verts[b].z, verts[c].z));
                    float zhi = Mathf.Max(verts[a].z, Mathf.Max(verts[b].z, verts[c].z));

                    if (!mats.TryGetValue(mat, out MatStat st))
                        st = new MatStat { ZMin = float.MaxValue, ZMax = float.MinValue };
                    st.Faces++;
                    st.ZMin = Mathf.Min(st.ZMin, zlo);
                    st.ZMax = Mathf.Max(st.ZMax, zhi);
                    mats[mat] = st;

                    if (!float.IsNaN(houseZ))
                    {
                        if (zlo >= houseZ) aboveHouse++;
                        else if (zhi <= houseZ) belowHouse++;
                        else straddling++;
                    }
                }

                sb.AppendLine($"   faces={faces}   sea-interior-flagged (UV0.w)={interiorFlagged}" +
                              $"   materialIds={mats.Count}");

                var ids = new List<int>(mats.Keys);
                ids.Sort();
                int confined = 0;
                foreach (int id in ids)
                {
                    MatStat st = mats[id];
                    bool aboveOnly = !float.IsNaN(houseZ) && st.ZMin >= houseZ;
                    if (aboveOnly) confined++;
                    sb.AppendLine($"      mat {id,2}: {st.Faces,6} faces   z [{st.ZMin,7:0.###} .. {st.ZMax,7:0.###}]" +
                                  (aboveOnly ? "   <-- entirely above the house sole" : ""));
                }
                if (confined > 0) anyMaterialConfined++;

                if (!float.IsNaN(houseZ))
                    sb.AppendLine($"   z-split at the house sole ({houseZ.ToString("0.###", CultureInfo.InvariantCulture)} m):" +
                                  $"  above={aboveHouse}  below={belowHouse}  STRADDLING={straddling}");
                sb.AppendLine();
            }

            sb.AppendLine(new string('=', 78));
            sb.AppendLine("SUMMARY");
            sb.AppendLine($"   interior-bearing hulls drawn as MESH   : {meshCount}");
            sb.AppendLine($"   interior-bearing hulls drawn as SPRITE : {spriteCount}");
            sb.AppendLine($"   no visual def / no baked mesh          : {noVisual} / {noMesh}");
            sb.AppendLine($"   hulls whose mesh has >1 SUBMESH        : {anySubmeshSplit}");
            sb.AppendLine($"   hulls with a material id confined above the house sole : {anyMaterialConfined}");
            foreach (string l in lines) sb.AppendLine(l);
            return sb.ToString();
        }

        private struct MatStat
        {
            public int Faces;
            public float ZMin, ZMax;
        }
    }
}
