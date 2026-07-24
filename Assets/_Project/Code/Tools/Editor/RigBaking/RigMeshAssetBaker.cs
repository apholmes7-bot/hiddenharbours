using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Turns a rig into a committed <see cref="HullMeshDef"/> asset (ADR 0022 phase 4)</b> — the
    /// baked format phase 3 deliberately did not invent. Extraction (<see cref="RigMeshExtractor"/>)
    /// and mesh building (<see cref="RigMeshBuilder"/>) are unchanged; this adds the two per-artwork
    /// POSE facts the runtime needs and writes everything as one asset with the mesh as a sub-asset:
    ///
    /// <list type="number">
    ///   <item><b>The azimuth convention, MEASURED</b> (<see cref="RigAzimuthProbe"/> over the rig's
    ///   own quarter-turn render) — never read off a declaration, which has shipped mirrored boats
    ///   five times. This is the sign of the whole compass→dir mapping
    ///   (<see cref="HullMeshMath.HeadingToDirUnits"/>).</item>
    ///   <item><b>The rock amplitudes</b>, read off the rig's exported <c>ROCK</c> block (rollA /
    ///   pitchA / heaveA) — transcription, not tuning, exactly like the motor rock amplitudes in
    ///   BoatVisualLibraryBuilder.</item>
    /// </list>
    ///
    /// <para><b>Non-destructive re-bakes:</b> an existing asset is refreshed in place (same guid), so
    /// nothing pointing at the def breaks; only the mesh sub-asset is replaced. The output is
    /// committed — the owner re-runs this only when the art director's rig changes.</para>
    ///
    /// <para>⚠️ <c>docs/art/rigs/**</c> is read-only here as everywhere (art-director's lane); the
    /// extractor's tests assert the sources are byte-identical after a run.</para>
    /// </summary>
    public static class RigMeshAssetBaker
    {
        const string HullMeshFolder = "Assets/_Project/Data/Boats/HullMeshes";

        /// <summary>
        /// <b>The whole fleet, in one pass (ADR 0022 phase 6).</b> Every hull in
        /// <see cref="HullMeshFleet.Hulls"/>: extract, build, measure, write the def, then either
        /// WIRE the existing sheet-built visual or CREATE a mesh-only one.
        ///
        /// <para>Phases 4 and 5 each hand-wrote one of these. That was the right shape while the
        /// question was still "does this work at all"; phase 5 answered it (the dragger needed zero
        /// changes to the baker, the shader or the seam) and the owner ruled the whole fleet goes
        /// mesh. So the per-hull code became per-hull data and this became the entry point.</para>
        ///
        /// <para><b>One hull's failure does not abort the rest.</b> A bake is a long editor operation
        /// over eleven hulls; stopping at the first exception would mean the owner learns about one
        /// problem per run. Every hull is attempted, failures are collected, and the run ends with a
        /// single report — errors last, because that is what he needs to read.</para>
        /// </summary>
        [MenuItem(RigMeshGate.MenuRoot + "/Bake ALL fleet hull meshes", priority = 219)]
        public static void BakeFleet() => BakeFleetInternal(HullMeshFleet.Hulls);

        [MenuItem(RigMeshGate.MenuRoot + "/Bake ALL fleet hull meshes", validate = true)]
        static bool BakeFleetValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (-executeMethod) for the whole-fleet bake.</summary>
        public static void BakeFleetCli()
        {
            try
            {
                int failed = BakeFleetInternal(HullMeshFleet.Hulls);
                if (failed > 0)
                {
                    Debug.LogError($"[rig-mesh] CLI fleet bake FAILED: {failed} hull(s) did not bake.");
                    EditorApplication.Exit(1);
                    return;
                }
                Debug.Log("[rig-mesh] CLI fleet bake OK.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[rig-mesh] CLI fleet bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Returns the number of hulls that failed. Reports every hull either way.</summary>
        static int BakeFleetInternal(IReadOnlyList<FleetHull> hulls)
        {
            var report = new StringBuilder($"[rig-mesh] fleet bake — {hulls.Count} hulls\n");
            var failures = new List<string>();

            for (int i = 0; i < hulls.Count; i++)
            {
                FleetHull hull = hulls[i];
                try
                {
                    EditorUtility.DisplayProgressBar(
                        "Baking fleet hull meshes", hull.Label, (i + 0.5f) / hulls.Count);

                    HullMeshDef def = Bake(hull.ScriptPath, hull.GlobalName, hull.MeshAssetPath, hull.MeshId);
                    WireVisuals(hull, def);

                    long sheetBytes = (long)def.CellW * def.CellH * 4 * 32 * 4;  // 32 facings × 4 rock, RGBA32
                    long meshBytes = new FileInfo(hull.MeshAssetPath).Length;
                    report.Append(
                        $"  ✓ {hull.Label}\n" +
                        $"      {def.Mesh.vertexCount} verts / {def.Mesh.triangles.Length / 3} tris, " +
                        $"cell {def.CellW}×{def.CellH} @ {def.PxPerMetre} px/m, elev {def.ElevationDeg}°\n" +
                        $"      azimuth {(def.AzimuthCounterClockwise ? "CCW" : "CW")} (MEASURED), " +
                        $"rock roll {def.RockRollDegrees}° pitch {def.RockPitchDegrees}° " +
                        $"heave {def.RockHeavePixels}px\n" +
                        $"      asset {meshBytes / 1024.0:F1} KB vs a {sheetBytes / 1048576.0:F1} MiB " +
                        $"sheet set — {sheetBytes / (double)meshBytes:N0}× smaller\n");
                }
                catch (Exception e)
                {
                    failures.Add(hull.Key);
                    report.Append($"  ✗ {hull.Label}: FAILED — {e.Message}\n");
                }
            }

            EditorUtility.ClearProgressBar();
            report.Append(failures.Count == 0
                ? "\n  All hulls baked. The fleet is mesh."
                : $"\n  ⚠️ {failures.Count} FAILED: {string.Join(", ", failures)}");

            if (failures.Count == 0) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());
            return failures.Count;
        }

        /// <summary>The first mesh hull end-to-end: the lobster boat (she has both a mesh and a baked
        /// sheet to compare — ADR 0022's own phasing). Kept as its own item because she is the A/B
        /// hull and gets re-baked on her own more than any other; it is a catalog lookup now, so it
        /// cannot drift from what the fleet bake does to her.</summary>
        [MenuItem(RigMeshGate.MenuRoot + "/Bake Lobster Boat hull-mesh asset", priority = 220)]
        public static void BakeLobsterBoat() => BakeOne("lobsterBoat");

        [MenuItem(RigMeshGate.MenuRoot + "/Bake Lobster Boat hull-mesh asset", validate = true)]
        static bool BakeLobsterBoatValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (-executeMethod) for the same bake.</summary>
        public static void BakeLobsterBoatCli() => BakeOneCli("lobsterBoat");

        /// <summary>
        /// <b>The hull that motivated the ADR (phase 5): the side dragger.</b> 25 m of riveted steel
        /// whose sheet set would have been <b>433.1 MiB</b> at 32 facings × 4 rock frames — against
        /// 143.9 KB of mesh, a ratio of ~3,082×. Mesh-only, so her bake CREATES her visual rather than
        /// wiring one; see <see cref="EnsureMeshOnlyVisual"/> for why that is a different job.
        /// </summary>
        [MenuItem(RigMeshGate.MenuRoot + "/Bake Side Dragger hull-mesh asset", priority = 221)]
        public static void BakeSideDragger() => BakeOne("sideDragger");

        [MenuItem(RigMeshGate.MenuRoot + "/Bake Side Dragger hull-mesh asset", validate = true)]
        static bool BakeSideDraggerValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (-executeMethod) for the dragger's bake.</summary>
        public static void BakeSideDraggerCli() => BakeOneCli("sideDragger");

        /// <summary>Bake one catalog hull by key, and wire whatever visuals it owns.</summary>
        public static HullMeshDef BakeOne(string key)
        {
            FleetHull hull = HullMeshFleet.Get(key);
            HullMeshDef def = Bake(hull.ScriptPath, hull.GlobalName, hull.MeshAssetPath, hull.MeshId);
            WireVisuals(hull, def);
            return def;
        }

        static void BakeOneCli(string key)
        {
            try
            {
                BakeOne(key);
                Debug.Log("[rig-mesh] CLI bake OK.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[rig-mesh] CLI bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Point every <c>BoatVisualDef</c> this hull dresses at the freshly baked mesh. Which of the
        /// two paths runs is the catalog's <see cref="FleetHull.HasBakedSheet"/> — see that field for
        /// why the difference matters to the owner rather than only to the code.
        /// </summary>
        static void WireVisuals(in FleetHull hull, HullMeshDef def)
        {
            for (int v = 0; v < hull.VisualAssetPaths.Length; v++)
            {
                string path = hull.VisualAssetPaths[v];
                if (hull.HasBakedSheet) WireSheetedVisual(path, def);
                else EnsureMeshOnlyVisual(path, hull.VisualIds[v], def);
            }
        }

        /// <summary>
        /// Wire a visual that was BUILT FROM A SHEET: flip it to the mesh variant and point it at the
        /// def, leaving its sprite compass fully populated.
        ///
        /// <para><b>Leaving the compass is the point, not an oversight.</b> It is what keeps the
        /// owner's V-key A/B alive at the helm — the only check on the mesh path that works by eye
        /// rather than by test — and it is what lets the sprite-only overlays (oars, outboards) keep
        /// binding. Field-scoped for the same reason the mesh-only path is: a re-run of Build Boat
        /// Visual Defs does not know these two fields, so it cannot undo them, and nothing the owner
        /// changes in the Inspector is stomped by a re-bake.</para>
        /// </summary>
        static void WireSheetedVisual(string assetPath, HullMeshDef def)
        {
            var visual = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatVisualDef>(assetPath);
            if (visual == null)
            {
                Debug.LogWarning($"[rig-mesh] {assetPath} not found — {def.Id} was baked but no visual " +
                                 "points at it. Run Build Boat Visual Defs first, then re-bake.");
                return;
            }

            visual.HullMesh = def;
            visual.Variant = HiddenHarbours.Boats.BoatHullVariant.Mesh;
            EditorUtility.SetDirty(visual);
            AssetDatabase.SaveAssets();
            Debug.Log($"[rig-mesh] {visual.Id}: Variant → Mesh, HullMesh → {def.Id}. Her sprite " +
                      "compass stays wired — that is the A/B comparison (V at the helm).");
        }

        /// <summary>
        /// Create or refresh the <see cref="HiddenHarbours.Boats.BoatVisualDef"/> for a hull that has
        /// NO baked sheet — the mesh is the whole picture. Refreshed in place (same guid) so a
        /// <c>BoatHullDef</c> pointing at it never breaks, and <b>field-scoped</b>: it writes only the
        /// facts this bake actually knows, so the owner's <c>SortingOrder</c> (or anything else he
        /// touches in the Inspector) survives a re-bake.
        ///
        /// <para><c>Facings</c> is left EMPTY on purpose — that is what makes
        /// <see cref="HiddenHarbours.Boats.BoatVisualDef.HasFullCompass"/> false, which is the honest
        /// answer for a hull with no sheet. The consequences are all correct: the V key reports "this
        /// hull has only one look" instead of offering a sprite half of an A/B that does not exist,
        /// and sprite-only overlays (oars, outboard) refuse to bind rather than draw wrongly.</para>
        /// </summary>
        static void EnsureMeshOnlyVisual(string assetPath, string id, HullMeshDef mesh)
        {
            var visual = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatVisualDef>(assetPath);
            bool created = visual == null;
            if (created)
            {
                EnsureFolder(System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/'));
                visual = ScriptableObject.CreateInstance<HiddenHarbours.Boats.BoatVisualDef>();
                visual.Id = id;
                AssetDatabase.CreateAsset(visual, assetPath);
            }

            visual.HullMesh = mesh;
            visual.Variant = HiddenHarbours.Boats.BoatHullVariant.Mesh;
            // The bake's own art facts, so the anchors and the wake foreshorten against the same
            // camera the mesh is projected through. Zero heading is 0 for every boat rig (element 0
            // is the North-facing view) — stated as data rather than assumed at the call site.
            visual.ArtBakeElevationDegrees = mesh.ElevationDeg;
            visual.ZeroHeadingDegrees = 0f;

            EditorUtility.SetDirty(visual);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);
            Debug.Log($"[rig-mesh] {(created ? "Created" : "Refreshed")} {assetPath}: {visual.Id} is " +
                      $"MESH-ONLY (no sheet, no compass) → {mesh.Id}, elevation {mesh.ElevationDeg}°.");
        }

        /// <summary>
        /// Extract + build + measure + write one rig's hull-mesh asset. Returns the (created or
        /// refreshed) def.
        /// </summary>
        public static HullMeshDef Bake(string scriptPath, string globalName, string assetPath, string id)
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = RigMeshExtractor.ExtractFrom(host, scriptPath, globalName);
            RigMeshBuild build = RigMeshBuilder.Build(data, $"{globalName}HullMesh");

            // --- the measured azimuth convention (quarter turn: broadside, least ambiguous) --------
            var quarter = new RigViewOptions(2, data.DefaultElev);   // dir 2 of 8 = 90° of turntable
            byte[] rgba = host.EvaluateBytes($"{globalName}.render({quarter.ToJsArgs()})");
            RigAzimuthProbe.Result probe = RigAzimuthProbe.MeasureFromQuarterTurn(rgba, data.W, data.H);
            Debug.Log($"[rig-mesh] {globalName} azimuth probe:\n{probe.Report}");

            // --- the rock amplitudes, off the exported ROCK block (optional; 0 = no rock) ----------
            float rollA = 0f, pitchA = 0f, heaveA = 0f;
            bool hasRock = host.EvaluateBool(
                $"typeof {globalName}.ROCK === 'object' && {globalName}.ROCK !== null");
            if (hasRock)
            {
                rollA = (float)host.EvaluateNumber($"{globalName}.ROCK.rollA || 0");
                pitchA = (float)host.EvaluateNumber($"{globalName}.ROCK.pitchA || 0");
                heaveA = (float)host.EvaluateNumber($"{globalName}.ROCK.heaveA || 0");
            }
            else
            {
                Debug.LogWarning($"[rig-mesh] {globalName} exports no ROCK block — the mesh hull will " +
                                 "not rock. If the rig has one, ask the art director to export it.");
            }

            // --- write the asset (create or refresh in place — the guid must survive) --------------
            EnsureFolder(HullMeshFolder);
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(assetPath);
            bool created = def == null;

            // ⚠️ A FILE THAT EXISTS BUT DOES NOT LOAD IS NOT A NEW ASSET — IT IS A BROKEN ONE.
            //
            // Treating it as new is silent data loss: the create path runs field initialisers, so
            // every field this baker does NOT write is quietly reset — today that is
            // RestingDraftMeters, which the hull-waterline work tunes per hull and no re-bake has any
            // business touching. MEASURED 2026-07-23, and it is not hypothetical: a run whose
            // Library/ carried a stale script→guid map wrote `m_Script: {fileID: 0}` into every def,
            // after which LoadAssetAtPath<HullMeshDef> returned null for assets that were sitting
            // right there, and the NEXT run "created" them over the top and reset the drafts to 0.
            // Failing loudly turns a silent stomp into a stop.
            if (created && File.Exists(assetPath))
                throw new InvalidOperationException(
                    $"{assetPath} exists on disk but did not load as a HullMeshDef, so this bake was " +
                    "about to recreate it and silently reset every field the baker does not write " +
                    "(RestingDraftMeters among them).\n" +
                    "Usual cause: a stale or borrowed Library/ — the script→guid map is wrong, the " +
                    "asset serialises with `m_Script: {fileID: 0}`, and it stops resolving to its " +
                    "type. Delete Library/ and let the project reimport, then bake again. If the " +
                    "asset really is meant to be replaced, delete it (and its .meta) deliberately.");

            if (created) def = ScriptableObject.CreateInstance<HullMeshDef>();

            def.Id = id;
            def.SourceRigPath = scriptPath;
            def.LightN = data.LightN.ToVector3();
            def.Gain = (float)data.Gain;
            def.Bias = (float)data.Bias;
            def.Keyline = data.Keyline;
            def.PivotPx = new Vector2((float)data.PivotX, (float)data.PivotY);
            def.PxPerMetre = data.PxPerMetre;
            def.CellW = data.W;
            def.CellH = data.H;
            def.ElevationDeg = (float)data.DefaultElev;
            def.AzimuthCounterClockwise = probe.Convention == AzimuthConvention.CounterClockwise;
            def.RockRollDegrees = rollA;
            def.RockPitchDegrees = pitchA;
            def.RockHeavePixels = heaveA;

            def.Ramps = new HullMeshDef.Ramp[data.Materials.Count];
            for (int m = 0; m < data.Materials.Count; m++)
                def.Ramps[m] = new HullMeshDef.Ramp
                {
                    Colors = data.Materials[m].Ramp,
                    Offset = data.Materials[m].Off,
                };

            def.Bayer16 = new float[16];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    def.Bayer16[x * 4 + y] = (float)data.Bayer[x, y];

            // The mesh sub-asset: replace, never accumulate. DestroyImmediate on the old one removes
            // it from the asset file; the new one is added under the same def.
            Mesh oldMesh = def.Mesh;
            def.Mesh = build.Mesh;
            if (created)
            {
                AssetDatabase.CreateAsset(def, assetPath);
            }
            else if (oldMesh != null)
            {
                AssetDatabase.RemoveObjectFromAsset(oldMesh);
                UnityEngine.Object.DestroyImmediate(oldMesh, allowDestroyingAssets: true);
            }
            AssetDatabase.AddObjectToAsset(build.Mesh, def);
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);

            Debug.Log($"[rig-mesh] {(created ? "Created" : "Refreshed")} {assetPath}: {build} — " +
                      $"azimuth {(def.AzimuthCounterClockwise ? "CCW (mapping negates)" : "CW")}, " +
                      $"rock ({rollA}, {pitchA}, {heaveA}), usable = {def.IsUsable()}.");
            if (!def.IsUsable())
                throw new InvalidOperationException($"Baked def at {assetPath} is not usable — see fields.");
            return def;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
