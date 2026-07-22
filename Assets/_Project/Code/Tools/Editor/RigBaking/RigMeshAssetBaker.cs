using System;
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

        /// <summary>The first mesh hull end-to-end: the lobster boat (she has both a mesh and a baked
        /// sheet to compare — ADR 0022's own phasing). Also wires her BoatVisualDef to the def and
        /// flips it to the Mesh variant, so the bake IS the switch-on.</summary>
        [MenuItem(RigMeshGate.MenuRoot + "/Bake Lobster Boat hull-mesh asset", priority = 220)]
        public static void BakeLobsterBoat()
        {
            var def = Bake("docs/art/rigs/lobsterBoatIsoRig.js", "LobsterBoatIso",
                           $"{HullMeshFolder}/LobsterBoatIsoHullMesh.asset",
                           "hullmesh.lobster_boat_iso");

            // Wire the visual def: HullMesh + Variant = Mesh. Field-scoped, so a re-run of Build Boat
            // Visual Defs (which does not know these fields) cannot undo it.
            var visual = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatVisualDef>(
                "Assets/_Project/Data/Boats/Visuals/LobsterBoatIso.asset");
            if (visual != null)
            {
                visual.HullMesh = def;
                visual.Variant = HiddenHarbours.Boats.BoatHullVariant.Mesh;
                EditorUtility.SetDirty(visual);
                AssetDatabase.SaveAssets();
                Debug.Log($"[rig-mesh] {visual.Id}: Variant → Mesh, HullMesh → {def.Id}. Her sprite " +
                          "compass stays wired — that is the A/B comparison (V at the helm).");
            }
            else
            {
                Debug.LogWarning("[rig-mesh] LobsterBoatIso.asset not found — the hull-mesh def was " +
                                 "baked but no visual points at it. Run Build Boat Visual Defs first.");
            }
        }

        [MenuItem(RigMeshGate.MenuRoot + "/Bake Lobster Boat hull-mesh asset", validate = true)]
        static bool BakeLobsterBoatValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (-executeMethod) for the same bake.</summary>
        public static void BakeLobsterBoatCli()
        {
            try
            {
                BakeLobsterBoat();
                Debug.Log("[rig-mesh] CLI bake OK.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[rig-mesh] CLI bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
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
