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

        /// <summary>Where the held props' defs live (ADR 0053): one <see cref="HeldPropMeshDef"/> per
        /// held state, beside the character data that holds them.</summary>
        public const string HeldPropFolder = "Assets/_Project/Data/Characters/HeldProps";

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
                        $"      turns about {def.PivotLocalMeters} m (hull-local)\n" +
                        (def.FixedMesh == null
                            ? ""
                            : $"      + a bolted-down part: {def.FixedMesh.vertexCount} verts / " +
                              $"{def.FixedMesh.triangles.Length / 3} tris\n"));
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

            // A fitting may be bolted-down geometry PLUS geometry that articulates — the outboards'
            // clamp bracket is fixed to the transom while the engine swivels on it. Which faces are
            // which was MEASURED during extraction; here they simply become two meshes off one
            // extraction, so they can never disagree about palette, light or cell.
            var moving = new List<RigFace>();
            var fixedFaces = new List<RigFace>();
            foreach (RigFace f in data.Faces) (f.FixedInPose ? fixedFaces : moving).Add(f);

            RigMeshBuild build = RigMeshBuilder.Build(data.WithFaces(moving), $"{p.Key}PropMesh");
            RigMeshBuild fixedBuild = fixedFaces.Count == 0
                ? null
                : RigMeshBuilder.Build(data.WithFaces(fixedFaces), $"{p.Key}PropFixedMesh");

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

            Mesh oldMesh = def.Mesh, oldFixed = def.FixedMesh;
            def.Mesh = build.Mesh;
            def.FixedMesh = fixedBuild?.Mesh;
            if (created)
            {
                AssetDatabase.CreateAsset(def, p.AssetPath);
            }
            else
            {
                DropSubAsset(oldMesh);
                DropSubAsset(oldFixed);
            }
            AssetDatabase.AddObjectToAsset(build.Mesh, def);
            if (fixedBuild != null) AssetDatabase.AddObjectToAsset(fixedBuild.Mesh, def);
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(p.AssetPath);

            Debug.Log($"[rig-prop] {(created ? "Created" : "Refreshed")} {p.AssetPath}: {build}, " +
                      $"turns about {pivot} m" +
                      (fixedBuild == null ? "." : $"; plus a BOLTED-DOWN part that does not: {fixedBuild}."));
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
                    // ONE motor field, whatever the fit: the twin is this same def instantiated twice
                    // at the mounts the def carries, not a second engine asset.
                    case "Motor": visual.MotorMesh = def; break;
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

        // ---- THE HELD PROPS (ADR 0053) ----------------------------------------------------------
        //
        // A prop a FIGURE holds — a rod, the clam spade, a fish by the tail, a bucket at one fill —
        // baked by the same extractor and mesh builder as the fittings above into a def of its own
        // (HeldPropMeshDef, D3). Everything from here down is new, and nothing above it changed: the
        // fittings bake exactly as they did, which HullPropBakeUnchangedTests proves by re-extracting
        // all six and comparing them with the committed defs. Two steps a fitting never needed:
        //   · DropRepeatedCorners — a rig stores some triangles as quads with a corner repeated (the
        //     fish's nose and pectoral fins); built as a quad, that is a second, zero-area triangle;
        //   · UsedRampsOnly — the rod's and the fish's tables carry ramps a state never paints, so the
        //     def keeps only the ones its faces use, in the table's order, at most RampSlots of them.

        /// <summary>
        /// Bake every held state of every held rig (<see cref="HeldPropFleet.Expand"/>), in fleet order.
        /// A state that fails is reported and the rest still bake; the return value says how many failed.
        /// </summary>
        /// <param name="baked">The defs that baked, in fleet order.</param>
        public static int BakeAllHeld(out List<HeldPropMeshDef> baked)
        {
            baked = new List<HeldPropMeshDef>();
            IReadOnlyList<HeldProp> props = HeldPropFleet.Expand();
            var report = new StringBuilder($"[held-prop] held-prop bake — {props.Count} states\n");
            var failures = new List<string>();

            try
            {
                for (int i = 0; i < props.Count; i++)
                {
                    HeldProp p = props[i];
                    try
                    {
                        EditorUtility.DisplayProgressBar("Baking held props", p.Label, (i + 0.5f) / props.Count);
                        HeldPropMeshDef def = BakeHeld(p);
                        baked.Add(def);
                        report.Append(
                            $"  ✓ {def.Id}: {def.SourceFaceCount} faces → {def.Mesh.vertexCount} verts / " +
                            $"{def.Mesh.triangles.Length / 3} tris, {def.Ramps.Length} ramps " +
                            $"({string.Join(", ", def.RampNames)}), {def.Lines.Length} plotted line(s), " +
                            $"grip {def.GripLocalMeters} m\n");
                    }
                    catch (Exception e)
                    {
                        failures.Add(p.PropId);
                        report.Append($"  ✗ {p.PropId}: FAILED — {e.Message}\n");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            report.Append(failures.Count == 0
                ? "\n  All held props baked."
                : $"\n  ⚠️ {failures.Count} FAILED: {string.Join(", ", failures)}");
            if (failures.Count == 0) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());
            return failures.Count;
        }

        /// <summary>Extract + build + write one held state. Returns the created or refreshed def.</summary>
        public static HeldPropMeshDef BakeHeld(in HeldProp p)
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = RigMeshExtractor.ExtractFrom(host, p.ScriptPath, p.GlobalName, p.Extraction);

            // ⚠️ The extractor's face packer resolves each face's `mat` against the table and, for a
            // name the table does not have, paints with 'hull' or the first ramp SILENTLY. A hull's
            // faces and table are one rig object, so that never fires there; a held state's are two
            // builder calls, so prove they agree before a wrong ramp is baked in.
            string faces = p.HeldCall("faces"), mats = p.HeldCall("mats");
            int rigFaces = (int)host.EvaluateNumber($"{faces}.length");
            if (rigFaces != data.Faces.Count)
                throw new InvalidOperationException(
                    $"{p.PropId}: the extractor read {data.Faces.Count} faces but {faces} has {rigFaces}.");
            string strays = host.EvaluateString(
                $"(function(){{var M={mats},o={{}};{faces}.forEach(function(f){{" +
                "if(!Object.prototype.hasOwnProperty.call(M,f.mat))o[String(f.mat)]=1;});" +
                "return Object.keys(o).join(',');})()");
            if (strays.Length > 0)
                throw new InvalidOperationException(
                    $"{p.PropId}: faces paint with '{strays}', which {mats} does not have. The packer " +
                    "would paint them with another ramp; fix the rig or the held reconstruction.");

            int sourceFaces = data.Faces.Count;
            DropRepeatedCorners(data.Faces);
            UsedRampsOnly(data, HeldPropMeshDef.RampSlots);
            RigMeshBuild build = RigMeshBuilder.Build(data, p.MeshName);

            // The hold, the pose and the paint rules — read from the rig, never from a call site.
            Vector3 grip = ReadVector3(host, p.HeldCall("grip"), "the held prop's grip");
            string pose = host.EvaluateString(p.HeldCall("pose"));
            bool keyline = host.EvaluateBool(p.HeldCall("keyline"));
            float edge = (float)host.EvaluateNumber($"{p.GlobalName}.{HeldPropFleet.HeldSymbol}.edge");
            ReadLines(host, p, out HullMeshDef.Ramp[] lineRamps, out HeldPropMeshDef.Line[] lines);

            string tierId = "", fill = "", catchGroup = "";
            if (p.Family == HeldPropMeshDef.BucketFamily)
            {
                string[] meta = host.EvaluateString(p.HeldCall("meta")).Split('|');
                if (meta.Length != 3)
                    throw new InvalidOperationException(
                        $"{p.PropId}: {p.HeldCall("meta")} is not tier|fill|catch.");
                tierId = meta[0];
                fill = meta[1];
                catchGroup = meta[2];
            }

            string sha = CharacterSkinExtractor.LfSha256File(Path.Combine(RigCatalog.RepoRoot, p.ScriptPath));

            EnsureFolder(HeldPropFolder);

            // ⚠️ EXISTS-BUT-WILL-NOT-LOAD is not ABSENT — the same guard as the fittings'.
            bool fileExists = File.Exists(p.AssetPath);
            var def = AssetDatabase.LoadAssetAtPath<HeldPropMeshDef>(p.AssetPath);
            if (def == null && fileExists)
                throw new InvalidOperationException(
                    $"{p.AssetPath} exists on disk but did not load as a {nameof(HeldPropMeshDef)}. " +
                    "Refusing to overwrite it: recreating the asset would run field initialisers and " +
                    "reset every field this baker does not write. Fix the asset (usually a stale " +
                    "script→guid map — reimport, or delete Library/ and let Unity rebuild it) and " +
                    "re-run. Never 'fix' this by deleting the asset.");

            bool created = def == null;
            if (created) def = ScriptableObject.CreateInstance<HeldPropMeshDef>();

            def.Id = p.PropId;
            def.Family = p.Family;
            def.Variant = p.Variant;
            def.SourceRigPath = p.ScriptPath;
            def.SourceRigSha256 = sha;
            def.SourceFaceBuilder = p.Extraction.FaceBuilderCall;
            def.SourceMaterials = p.Extraction.MaterialsCall;
            def.SourcePose = pose;
            def.SourceFaceCount = sourceFaces;
            def.LightN = data.LightN.ToVector3();
            def.Gain = (float)data.Gain;
            def.Bias = (float)data.Bias;
            def.Keyline = data.Keyline;
            def.BackfaceRescueNeedsOptIn = p.Extraction.BackfaceRescueNeedsOptIn;
            def.DepthEdgeDarkening = p.Extraction.DepthEdgeDarkening;
            def.EdgeDepthMeters = edge;
            def.KeylineOutline = keyline;
            def.AzimuthCounterClockwise = p.Owner.AzimuthCounterClockwise;
            def.PivotPx = new Vector2((float)data.PivotX, (float)data.PivotY);
            def.PxPerMetre = data.PxPerMetre;
            def.CellW = data.W;
            def.CellH = data.H;
            def.ElevationDeg = (float)data.DefaultElev;
            def.GripLocalMeters = grip;
            def.LineRamps = lineRamps;
            def.Lines = lines;
            def.BucketTierId = tierId;
            def.BucketFill = fill;
            def.BucketCatch = catchGroup;

            def.Ramps = new HullMeshDef.Ramp[data.Materials.Count];
            def.RampNames = new string[data.Materials.Count];
            for (int m = 0; m < data.Materials.Count; m++)
            {
                def.Ramps[m] = new HullMeshDef.Ramp
                {
                    Colors = data.Materials[m].Ramp,
                    Offset = data.Materials[m].Off,
                };
                def.RampNames[m] = data.Materials[m].Name;
            }

            def.Bayer16 = new float[16];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    def.Bayer16[x * 4 + y] = (float)data.Bayer[x, y];

            Mesh oldMesh = def.Mesh;
            def.Mesh = build.Mesh;
            if (created) AssetDatabase.CreateAsset(def, p.AssetPath);
            else DropSubAsset(oldMesh);
            AssetDatabase.AddObjectToAsset(build.Mesh, def);
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(p.AssetPath);

            Debug.Log($"[held-prop] {(created ? "Created" : "Refreshed")} {p.AssetPath}: {build}, " +
                      $"grip {grip} m, {pose}.");
            if (!def.IsUsable())
                throw new InvalidOperationException($"Baked held prop at {p.AssetPath} is not usable — see fields.");
            return def;
        }

        /// <summary>
        /// Drop each corner that repeats the one before it (the last against the first, too), in place,
        /// so a triangle a rig stores as a quad bakes as a triangle and not as a triangle plus a
        /// zero-area one. Returns how many corners went.
        ///
        /// <para>⚠️ Only a repeat AFTER the first three corners may go. The face's normal is the rig's
        /// <c>normal(v0, v1, v2)</c>, and a repeat inside those three gives the rig a zero normal it
        /// really shades with; dropping it would light the face with a different normal than the
        /// picture's. That is refused, as is anything left with fewer than three corners.</para>
        /// </summary>
        public static int DropRepeatedCorners(List<RigFace> faces)
        {
            if (faces == null) throw new ArgumentNullException(nameof(faces));
            int dropped = 0;
            for (int i = 0; i < faces.Count; i++)
            {
                Vector3d[] v = faces[i].V;
                var kept = new List<Vector3d>(v.Length);
                for (int k = 0; k < v.Length; k++)
                {
                    if (kept.Count > 0 && Same(v[k], kept[kept.Count - 1]))
                    {
                        if (k < 3) throw NormalCorner(i, k);
                        dropped++;
                        continue;
                    }
                    kept.Add(v[k]);
                }
                // The wrap: a last corner that repeats the first.
                while (kept.Count > 1 && Same(kept[kept.Count - 1], kept[0]))
                {
                    if (kept.Count - 1 < 3) throw NormalCorner(i, kept.Count - 1);
                    kept.RemoveAt(kept.Count - 1);
                    dropped++;
                }
                if (kept.Count < 3)
                    throw new InvalidOperationException(
                        $"Face {i} has {kept.Count} distinct corners — not a face.");
                if (kept.Count != v.Length) faces[i].V = kept.ToArray();
            }
            return dropped;
        }

        static InvalidOperationException NormalCorner(int face, int corner) =>
            new InvalidOperationException(
                $"Face {face} repeats corner {corner} inside the three its normal is taken from. The rig " +
                "shades that face with a zero normal; dropping the repeat would re-light it.");

        static bool Same(in Vector3d a, in Vector3d b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

        /// <summary>
        /// Keep only the materials a face paints with, in the table's own order, and renumber each
        /// face's material to match. Returns how many were kept.
        ///
        /// <para>Refused, loudly: an interior face (a held prop has no rooms), a face whose material is
        /// outside the table, and more used ramps than <paramref name="maxSlots"/> — the facet shader
        /// holds that many and a sixteenth-plus ramp would wrap onto another.</para>
        /// </summary>
        public static int UsedRampsOnly(RigMeshData data, int maxSlots)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var used = new bool[data.Materials.Count];
            foreach (RigFace f in data.Faces)
            {
                if (f.Interior)
                    throw new InvalidOperationException(
                        "A held prop has no interior; this face list carries an interior face.");
                if (f.Mat < 0 || f.Mat >= used.Length)
                    throw new InvalidOperationException(
                        $"A face paints with material {f.Mat}, outside the table's {used.Length}.");
                used[f.Mat] = true;
            }

            var remap = new int[used.Length];
            var kept = new List<RigMaterial>();
            for (int m = 0; m < used.Length; m++)
            {
                remap[m] = used[m] ? kept.Count : -1;
                if (used[m]) kept.Add(data.Materials[m]);
            }
            if (kept.Count > maxSlots)
                throw new InvalidOperationException(
                    $"The faces use {kept.Count} ramps; the facet shader holds {maxSlots}.");

            foreach (RigFace f in data.Faces) f.Mat = remap[f.Mat];
            data.Materials = kept;
            return kept.Count;
        }

        /// <summary>
        /// The thin parts the rig PLOTS rather than builds (D2), as the reconstructed
        /// <c>held.lines(v)</c> samples them: the ramps the samples paint from, then each line.
        ///
        /// <para>Across the bridge as one string — <c>ramps @ lines</c>; ramps <c>;</c>-separated, each
        /// <c>,</c>-separated hex; lines <c>&amp;</c>-separated, each <c>name|bias|dots|points</c>;
        /// points <c>/</c>-separated, each <c>x,y,z,ramp,index,second</c>. None of those characters
        /// can appear in a hex colour, a number or a line name.</para>
        /// </summary>
        static void ReadLines(IRigScriptHost host, in HeldProp p,
                              out HullMeshDef.Ramp[] ramps, out HeldPropMeshDef.Line[] lines)
        {
            string call = p.HeldCall("lines");
            string blob = host.EvaluateString(
                $"(function(){{var L={call};" +
                "return L.ramps.map(function(r){return r.join(',');}).join(';')+'@'+" +
                "L.lines.map(function(l){return [l.name,l.bias,l.dots?1:0,l.pts.map(function(q){" +
                "return q.join(',');}).join('/')].join('|');}).join('&');})()");

            string[] halves = blob.Split('@');
            if (halves.Length != 2)
                throw new InvalidOperationException($"{call} came across malformed: '{blob}'.");

            ramps = halves[0].Length == 0
                ? Array.Empty<HullMeshDef.Ramp>()
                : Array.ConvertAll(halves[0].Split(';'), r => new HullMeshDef.Ramp
                {
                    // A plotted line picks its entry by INDEX and is not shaded, so it has no offset.
                    Colors = Array.ConvertAll(r.Split(','), h => ParseHexColour(h, call)),
                    Offset = 0,
                });

            if (halves[1].Length == 0)
            {
                lines = Array.Empty<HeldPropMeshDef.Line>();
                return;
            }
            string[] parts = halves[1].Split('&');
            lines = new HeldPropMeshDef.Line[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                string[] f = parts[i].Split('|');
                if (f.Length != 4)
                    throw new InvalidOperationException($"{call}: line '{parts[i]}' is not name|bias|dots|points.");
                string[] pts = f[3].Length == 0 ? Array.Empty<string>() : f[3].Split('/');
                var points = new HeldPropMeshDef.LinePoint[pts.Length];
                for (int k = 0; k < pts.Length; k++)
                {
                    string[] q = pts[k].Split(',');
                    if (q.Length != 6)
                        throw new InvalidOperationException($"{call}: line '{f[0]}' point {k} is not x,y,z,ramp,index,second.");
                    int ramp = ParseInt(q[3], call);
                    if (ramp < 0 || ramp >= ramps.Length)
                        throw new InvalidOperationException(
                            $"{call}: line '{f[0]}' point {k} paints from ramp {ramp} of {ramps.Length}.");
                    points[k] = new HeldPropMeshDef.LinePoint
                    {
                        Position = new Vector3(ParseFloat(q[0], call), ParseFloat(q[1], call), ParseFloat(q[2], call)),
                        Ramp = ramp,
                        Index = ParseInt(q[4], call),
                        SecondIndex = ParseInt(q[5], call),
                    };
                }
                lines[i] = new HeldPropMeshDef.Line
                {
                    Name = f[0],
                    DepthBiasMeters = ParseFloat(f[1], call),
                    Dots = f[2] == "1",
                    Points = points,
                };
            }
        }

        static float ParseFloat(string s, string what) =>
            float.TryParse(s, System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out float v)
                ? v
                : throw new InvalidOperationException($"{what} gave '{s}', which is not a number.");

        static int ParseInt(string s, string what) =>
            int.TryParse(s, System.Globalization.NumberStyles.AllowLeadingSign,
                         System.Globalization.CultureInfo.InvariantCulture, out int v)
                ? v
                : throw new InvalidOperationException($"{what} gave '{s}', which is not a whole number.");

        /// <summary><c>#rrggbb</c> (or <c>#rrggbbaa</c>) as the rigs write their ramps.</summary>
        static Color32 ParseHexColour(string hex, string what)
        {
            string h = hex.StartsWith("#", StringComparison.Ordinal) ? hex.Substring(1) : hex;
            if ((h.Length != 6 && h.Length != 8) ||
                !uint.TryParse(h, System.Globalization.NumberStyles.HexNumber,
                               System.Globalization.CultureInfo.InvariantCulture, out uint n))
                throw new InvalidOperationException($"{what} gave the colour '{hex}', which is not #rrggbb.");
            if (h.Length == 6) n = (n << 8) | 0xFF;
            return new Color32((byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n);
        }

        static void DropSubAsset(Mesh mesh)
        {
            if (mesh == null) return;
            AssetDatabase.RemoveObjectFromAsset(mesh);
            UnityEngine.Object.DestroyImmediate(mesh, allowDestroyingAssets: true);
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
