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
    /// <b>Turns a rig's articulated fitting into a committed <see cref="HullPropMeshDef"/></b> — the
    /// dory's oars, the outboards (ADR 0022 phase 7). Extraction and mesh building are the hull's,
    /// unchanged; what this adds is the one pose fact a fitting needs and a hull does not: the point
    /// it turns about.
    ///
    /// <para><b>Non-destructive, and field-scoped, exactly like the hull baker.</b> An existing asset
    /// is refreshed in place (same guid) so nothing pointing at it breaks, and a def that exists but
    /// will not LOAD is refused rather than recreated — recreating it runs field initialisers and
    /// silently resets every field the baker does not write, which is how a re-bake once zeroed two
    /// hand-tuned hull drafts.</para>
    /// </summary>
    public static class RigPropMeshBaker
    {
        const string PropFolder = "Assets/_Project/Data/Boats/HullProps";

        [MenuItem(RigMeshGate.MenuRoot + "/Bake ALL hull fittings (oars, outboards)", priority = 218)]
        public static void BakeAllProps() => BakeAllInternal(HullPropFleet.All);

        [MenuItem(RigMeshGate.MenuRoot + "/Bake ALL hull fittings (oars, outboards)", validate = true)]
        static bool BakeAllPropsValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (-executeMethod).</summary>
        public static void BakeAllPropsCli()
        {
            try
            {
                int failed = BakeAllInternal(HullPropFleet.All);
                if (failed > 0)
                {
                    Debug.LogError($"[rig-prop] CLI bake FAILED: {failed} fitting(s) did not bake.");
                    EditorApplication.Exit(1);
                    return;
                }
                Debug.Log("[rig-prop] CLI bake OK.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[rig-prop] CLI bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        static int BakeAllInternal(IReadOnlyList<FleetProp> props)
        {
            var report = new StringBuilder($"[rig-prop] fitting bake — {props.Count}\n");
            var failures = new List<string>();

            for (int i = 0; i < props.Count; i++)
            {
                FleetProp p = props[i];
                try
                {
                    EditorUtility.DisplayProgressBar("Baking hull fittings", p.Label,
                                                     (i + 0.5f) / props.Count);
                    HullPropMeshDef def = Bake(p);
                    report.Append(
                        $"  ✓ {p.Label}\n" +
                        $"      {def.Mesh.vertexCount} verts / {def.Mesh.triangles.Length / 3} tris, " +
                        $"cell {def.CellW}×{def.CellH} @ {def.PxPerMetre} px/m\n" +
                        $"      turns about {def.PivotLocalMeters} m (hull-local)\n");
                }
                catch (Exception e)
                {
                    failures.Add(p.Key);
                    report.Append($"  ✗ {p.Label}: FAILED — {e.Message}\n");
                }
            }

            EditorUtility.ClearProgressBar();
            report.Append(failures.Count == 0
                ? "\n  All fittings baked."
                : $"\n  ⚠️ {failures.Count} FAILED: {string.Join(", ", failures)}");

            if (failures.Count == 0) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());
            return failures.Count;
        }

        /// <summary>Extract + build + write one fitting. Returns the created or refreshed def.</summary>
        public static HullPropMeshDef Bake(in FleetProp p)
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = RigMeshExtractor.ExtractFrom(host, p.ScriptPath, p.GlobalName, p.Extraction);
            RigMeshBuild build = RigMeshBuilder.Build(data, $"{p.Key}PropMesh");

            // The pivot — the whole pose, in one vector. Read from the rig, never from a call site.
            Vector3 pivot = ReadVector3(host, $"{p.GlobalName}.{p.Extraction.PivotCall}",
                                        "the fitting's articulation pivot");

            float maxSteer = ReadOptionalNumber(host, p.GlobalName, p.Extraction.MaxSteerCall);
            float maxTilt = ReadOptionalNumber(host, p.GlobalName, p.Extraction.MaxTiltCall);
            float[] mounts = ReadOptionalNumbers(host, p.GlobalName, p.Extraction.LateralMountsCall);

            EnsureFolder(PropFolder);

            // ⚠️ EXISTS-BUT-WILL-NOT-LOAD is not the same as ABSENT, and treating it as absent is how
            // a re-bake silently resets tuned fields. Same guard as the hull baker's.
            bool fileExists = File.Exists(p.AssetPath);
            var def = AssetDatabase.LoadAssetAtPath<HullPropMeshDef>(p.AssetPath);
            if (def == null && fileExists)
                throw new InvalidOperationException(
                    $"{p.AssetPath} exists on disk but did not load as a {nameof(HullPropMeshDef)}. " +
                    "Refusing to overwrite it: recreating the asset would run field initialisers and " +
                    "reset every field this baker does not write. Fix the asset (usually a stale " +
                    "script→guid map — reimport, or delete Library/ and let Unity rebuild it) and " +
                    "re-run. Never 'fix' this by deleting the asset.");

            bool created = def == null;
            if (created) def = ScriptableObject.CreateInstance<HullPropMeshDef>();

            def.Id = p.PropId;
            def.SourceRigPath = p.ScriptPath;
            def.SourceFaceBuilder = p.Extraction.FaceBuilderCall;
            def.LightN = data.LightN.ToVector3();
            def.Gain = (float)data.Gain;
            def.Bias = (float)data.Bias;
            def.Keyline = data.Keyline;
            def.PivotPx = new Vector2((float)data.PivotX, (float)data.PivotY);
            def.PxPerMetre = data.PxPerMetre;
            def.CellW = data.W;
            def.CellH = data.H;
            def.ElevationDeg = (float)data.DefaultElev;
            def.PivotLocalMeters = pivot;
            def.MaxSteerDegrees = maxSteer;
            def.MaxTiltDegrees = maxTilt;
            def.LateralMountsMeters = mounts;

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

            Mesh oldMesh = def.Mesh;
            def.Mesh = build.Mesh;
            if (created)
            {
                AssetDatabase.CreateAsset(def, p.AssetPath);
            }
            else if (oldMesh != null)
            {
                AssetDatabase.RemoveObjectFromAsset(oldMesh);
                UnityEngine.Object.DestroyImmediate(oldMesh, allowDestroyingAssets: true);
            }
            AssetDatabase.AddObjectToAsset(build.Mesh, def);
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(p.AssetPath);

            Debug.Log($"[rig-prop] {(created ? "Created" : "Refreshed")} {p.AssetPath}: {build}, " +
                      $"turns about {pivot} m.");
            if (!def.IsUsable())
                throw new InvalidOperationException($"Baked fitting at {p.AssetPath} is not usable — see fields.");

            WireVisuals(p, def);
            return def;
        }

        /// <summary>
        /// Point every <see cref="HiddenHarbours.Boats.BoatVisualDef"/> that WEARS this fitting at the
        /// freshly baked def, in the named slot.
        ///
        /// <para><b>Field-scoped, exactly like the hull baker's wiring.</b> It writes ONE field and
        /// leaves everything else — sheets, sorting, variant — alone, so it composes with Build Boat
        /// Visual Defs in either order rather than racing it. It never CREATES a visual: a fitting is
        /// worn by a boat that already exists, and inventing one would guess at every other field.</para>
        /// </summary>
        static void WireVisuals(in FleetProp p, HullPropMeshDef def)
        {
            foreach ((string assetPath, string slot) in p.WornBy)
            {
                var visual = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatVisualDef>(assetPath);
                if (visual == null)
                {
                    Debug.LogWarning(
                        $"[rig-prop] {p.Label}: no BoatVisualDef at {assetPath}, so nothing was wired " +
                        "to wear it. The fitting is baked and committed; run Build Boat Visual Defs " +
                        "and re-bake to hang it on the boat.");
                    continue;
                }

                switch (slot)
                {
                    case "OarPort": visual.OarPortMesh = def; break;
                    case "OarStar": visual.OarStarMesh = def; break;
                    default:
                        Debug.LogError(
                            $"[rig-prop] {p.Label}: unknown fitting slot '{slot}' on {assetPath}. The " +
                            "prop catalog names a slot the visual has no field for — add the field, or " +
                            "fix the slot name in HullPropFleet.");
                        continue;
                }

                EditorUtility.SetDirty(visual);
                Debug.Log($"[rig-prop] wired {def.Id} into {assetPath} slot '{slot}'.");
            }
            AssetDatabase.SaveAssets();
        }

        static Vector3 ReadVector3(IRigScriptHost host, string expr, string what)
        {
            string blob = host.EvaluateString($"(function(){{var v={expr};return v.join(',');}})()");
            string[] parts = blob.Split(',');
            if (parts.Length != 3)
                throw new InvalidOperationException(
                    $"{expr} was expected to yield {what} as [x,y,z] but gave {parts.Length} values.");
            return new Vector3(
                float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
        }

        static float ReadOptionalNumber(IRigScriptHost host, string g, string call) =>
            string.IsNullOrEmpty(call) ? 0f : (float)host.EvaluateNumber($"{g}.{call}");

        static float[] ReadOptionalNumbers(IRigScriptHost host, string g, string call)
        {
            if (string.IsNullOrEmpty(call)) return Array.Empty<float>();
            string blob = host.EvaluateString($"(function(){{var v={g}.{call};return v.join(',');}})()");
            if (string.IsNullOrWhiteSpace(blob)) return Array.Empty<float>();
            string[] parts = blob.Split(',');
            var o = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                o[i] = float.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
            return o;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
