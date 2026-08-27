#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Turns a road-vehicle rig into a committed <see cref="VehicleMeshDef"/> and its wheels
    /// (ADR 0035)</b> — the sibling of <see cref="RigMeshAssetBaker"/>. Extraction
    /// (<see cref="RigMeshExtractor"/>) and mesh building (<see cref="RigMeshBuilder"/>) are the
    /// hull's, unchanged and deliberately so: a vehicle rig packs the same faces through the same
    /// camera, and the only genuinely new work is below.
    ///
    /// <list type="number">
    ///   <item><b>The articulation split, MEASURED.</b> A baked mesh is static geometry at ONE pose,
    ///   so anything that moves relative to the body must come out as its own mesh — otherwise the
    ///   body draws a second, frozen copy of every wheel. Which faces belong to which body is found
    ///   by building the rig's face list at several poses and keeping what moved, the technique
    ///   <see cref="HullPropMeshDef.FixedMesh"/> documents. The partition is ASSERTED exact
    ///   (disjoint, covering) rather than trusted — see <see cref="Partition"/>.</item>
    ///   <item><b>The azimuth convention, from the rig's own ANCHORS rather than its silhouette.</b>
    ///   See <see cref="MeasureAzimuth"/>; this is the one place a vehicle genuinely cannot reuse the
    ///   hull baker, and reusing it would have been quietly wrong.</item>
    ///   <item><b>The chassis</b> — wheelbase, track, wheel radius, lock angles, suspension travel —
    ///   read off the rig's own exports. Transcription, not tuning.</item>
    /// </list>
    ///
    /// <para><b>Non-destructive re-bakes:</b> existing assets are refreshed in place (same guid), so
    /// nothing pointing at them breaks; only the mesh sub-assets are replaced.</para>
    ///
    /// <para>⚠️ <c>docs/art/rigs/**</c> is read-only here as everywhere (art-director's lane).</para>
    /// </summary>
    public static class VehicleMeshAssetBaker
    {
        const string MeshFolder = "Assets/_Project/Data/Vehicles/Meshes";
        const string WheelFolder = "Assets/_Project/Data/Vehicles/Wheels";

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [MenuItem(RigMeshGate.MenuRoot + "/Bake ALL road-vehicle meshes", priority = 230)]
        public static void BakeAll() => BakeAllInternal();

        [MenuItem(RigMeshGate.MenuRoot + "/Bake ALL road-vehicle meshes", validate = true)]
        static bool BakeAllValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (-executeMethod).</summary>
        public static void BakeAllCli()
        {
            try
            {
                int failed = BakeAllInternal();
                if (failed > 0)
                {
                    Debug.LogError($"[rig-vehicle] CLI bake FAILED: {failed} vehicle(s) did not bake.");
                    EditorApplication.Exit(1);
                    return;
                }
                Debug.Log("[rig-vehicle] CLI bake OK.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[rig-vehicle] CLI bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Returns the number that failed. Reports every vehicle either way — one
        /// vehicle's failure must not cost the report on the rest.</summary>
        static int BakeAllInternal()
        {
            var report = new System.Text.StringBuilder(
                $"[rig-vehicle] bake — {VehicleRigFleet.Vehicles.Count} vehicle(s)\n");
            var failures = new List<string>();

            foreach (VehicleRigFleet.Vehicle v in VehicleRigFleet.Vehicles)
            {
                // ⭐ THE TWO LEDGERS DECIDE WHAT IS BAKED, and they are read here rather than
                // implied by whether a mesh path happens to be filled in. Before 2026-08-27 this
                // loop bake-attempted EVERY registered vehicle, so the nine road-fleet bodies that
                // PR 1 landed unbaked turned a clean run into nine failures and an exit code of 1 —
                // a table that says "not baked, and here is why" was being argued with by the tool
                // that reads it. A skip is reported, never silent: an entry that has quietly stopped
                // being baked must look different from one that never was.
                if (VehicleRigFleet.SidecarHashRefused.TryGetValue(v.Key, out string refusal))
                {
                    report.Append($"  ⊘ {v.Key}: REFUSED — her sidecar does not pin her rig, so her " +
                                  $"published geometry may describe another shape. {Head(refusal)}\n");
                    continue;
                }

                if (VehicleRigFleet.NotBaked.TryGetValue(v.Key, out string reason))
                {
                    report.Append($"  – {v.Key}: not baked, by declaration. {Head(reason)}\n");
                    continue;
                }

                try
                {
                    VehicleMeshDef def = Bake(v);
                    report.Append(
                        $"  ✓ {v.Key}\n" +
                        $"      body {def.Mesh.vertexCount} verts / {def.Mesh.triangles.Length / 3} tris, " +
                        $"{def.Wheels.Length} fitting(s)\n" +
                        $"      cell {def.CellW}×{def.CellH} @ {def.PxPerMetre} px/m, " +
                        $"elev {def.ElevationDeg}°, azimuth " +
                        $"{(def.AzimuthCounterClockwise ? "CCW" : "CW")} (MEASURED)\n" +
                        $"      wheelbase {def.WheelbaseMeters:0.###} m, front track " +
                        $"{def.FrontTrackMeters:0.###} m, wheel r {def.WheelRadiusMeters:0.###} m, " +
                        $"lock {def.MaxInnerSteerDegrees:0.##}°/{def.MaxOuterSteerDegrees:0.##}°, " +
                        $"full-lock turn radius {def.FullLockTurnRadiusMeters:0.###} m\n");
                }
                catch (Exception e)
                {
                    failures.Add(v.Key);
                    report.Append($"  ✗ {v.Key}: FAILED — {e.Message}\n");
                }
            }

            if (failures.Count == 0) Debug.Log(report.ToString());
            else Debug.LogError(report.Append($"\n  ⚠️ {failures.Count} FAILED: " +
                                              $"{string.Join(", ", failures)}").ToString());
            return failures.Count;
        }

        /// <summary>The first sentence of a ledger reason — enough to read the skip in the log
        /// without pasting a paragraph into every line of the report.</summary>
        static string Head(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "";
            int stop = reason.IndexOf(". ", StringComparison.Ordinal);
            return stop < 0 ? reason : reason.Substring(0, stop + 1);
        }

        /// <summary>Extract + split + measure + write one vehicle's assets.</summary>
        public static VehicleMeshDef Bake(in VehicleRigFleet.Vehicle v)
        {
            // ⚠️⚠️ THE HASH REFUSAL, ENFORCED AT THE ONE PLACE THAT READS THE GEOMETRY. The ledger
            // and VehicleRigFleetTests already forbid a refused vehicle from being listed as Baked,
            // but a direct Bake(v) call bypasses both — and what this method now reads out of a
            // sidecar is her drive door, her solid box and her seats. A stamp that does not pin its
            // rig means those numbers may have been cut from a different shape, which is the one
            // failure that looks entirely correct until somebody walks through a wall.
            if (VehicleRigFleet.SidecarHashRefused.TryGetValue(v.Key, out string refusal))
                throw new InvalidOperationException(
                    $"{v.Key}: her gameplay sidecar does not pin her rig, so this bake will not read " +
                    $"her published geometry.\n{refusal}\n⚠️ The fix is UPSTREAM (art director). Do " +
                    "NOT re-stamp the hash in this repo: a hash corrected on our side comes back " +
                    "wrong on the next regeneration, and re-stamping a bad stamp fakes exactly the " +
                    "freshness the pin exists to prove.");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = RigMeshExtractor.ExtractFrom(host, v.ScriptPath, v.GlobalName,
                                                           hull: v.Extraction);

            // ⚠️ The split below indexes data.Faces against the rig's OWN face list, so the two must
            // correspond one-for-one and in order. ReadFaces appends in the rig's order and nothing
            // on the hull path reorders or drops (DropReverseDuplicateFaces is a FITTING knob and is
            // off), but a count mismatch would silently mis-assign whole wheels — so it is checked.
            int rigFaces = (int)host.EvaluateNumber($"{v.GlobalName}.{v.Extraction.FaceExpression}.length");
            if (rigFaces != data.Faces.Count)
                throw new InvalidOperationException(
                    $"{v.Key}: the extractor produced {data.Faces.Count} faces but the rig's own " +
                    $"{v.Extraction.FaceExpression} yields {rigFaces}. The articulation split assigns " +
                    "faces BY INDEX against the rig, so a mismatch would put someone else's geometry " +
                    "in a wheel. Refusing to bake.");

            VehiclePartition split = Partition(host, v, data.Faces.Count);
            AzimuthConvention convention = MeasureAzimuth(host, v, data.DefaultElev);
            Chassis chassis = ReadChassis(host, v);
            VehicleSidecarFacts facts = ReadSidecar(v);

            EnsureFolder(MeshFolder);
            EnsureFolder(WheelFolder);

            // --- the wheels, each off the SAME extraction so they cannot disagree about palette,
            //     light or cell ------------------------------------------------------------------
            var fitments = new List<VehicleFitment>(split.Fitments.Count);
            foreach (FitmentPlan plan in split.Fitments)
            {
                HullPropMeshDef prop = WriteFitting(v, plan, data, chassis);
                fitments.Add(new VehicleFitment
                {
                    Slot = plan.Slot,
                    Prop = prop,
                    Motion = plan.Motion,
                    Side = plan.Side,
                });
            }

            // --- the body: everything that did NOT move under any articulation --------------------
            RigMeshBuild body = RigMeshBuilder.Build(data.WithFaces(Subset(data, split.Body)),
                                                     $"{v.GlobalName}VehicleMesh");

            VehicleMeshDef def = LoadOrCreate<VehicleMeshDef>(v.MeshAssetPath);

            def.Id = v.MeshId;
            def.SourceRigPath = v.ScriptPath;
            def.SourceFaceBuilder = v.Extraction.FaceExpression;
            def.LightN = data.LightN.ToVector3();
            def.Gain = (float)data.Gain;
            def.Bias = (float)data.Bias;
            def.Keyline = data.Keyline;
            def.PivotPx = new Vector2((float)data.PivotX, (float)data.PivotY);
            def.PxPerMetre = data.PxPerMetre;
            def.CellW = data.W;
            def.CellH = data.H;
            def.ElevationDeg = (float)data.DefaultElev;
            def.AzimuthCounterClockwise = convention == AzimuthConvention.CounterClockwise;
            def.ZeroHeadingDegrees = 0f;

            def.WheelbaseMeters = chassis.Wheelbase;
            def.FrontTrackMeters = chassis.FrontTrack;
            def.WheelRadiusMeters = chassis.WheelRadius;
            def.FrontAxleY = chassis.FrontAxleY;
            def.RearAxleY = chassis.RearAxleY;
            def.MaxInnerSteerDegrees = chassis.MaxInnerDeg;
            def.MaxOuterSteerDegrees = chassis.MaxOuterDeg;
            def.SuspensionTravelFrontMeters = chassis.TravelFront;
            def.SuspensionTravelRearMeters = chassis.TravelRear;
            def.Wheels = fitments.ToArray();

            def.Ramps = ReadRamps(data);
            def.Bayer16 = ReadBayer(data);

            WriteSidecarFacts(v, def, facts);

            SwapMesh(def, ref def.Mesh, body.Mesh, v.MeshAssetPath);

            Debug.Log($"[rig-vehicle] {v.MeshAssetPath}: {body} — body {split.Body.Count} faces, " +
                      $"{fitments.Count} fitting(s), azimuth " +
                      $"{(def.AzimuthCounterClockwise ? "CCW (mapping negates)" : "CW")}, " +
                      $"cell {def.CellW}×{def.CellH} @ pivot ({def.PivotPx.x},{def.PivotPx.y}), " +
                      $"usable = {def.IsUsable()}.");

            if (!def.IsUsable())
                throw new InvalidOperationException($"Baked def at {v.MeshAssetPath} is not usable.");

            EnsureVehicleDef(v, def);
            return def;
        }

        /// <summary>
        /// Create or refresh the <see cref="HiddenHarbours.Vehicles.VehicleDef"/> this mesh dresses —
        /// the asset the world actually places.
        ///
        /// <para><b>FIELD-SCOPED, exactly like the hull baker's visual wiring, and for the same
        /// reason.</b> It writes only the two facts a bake genuinely knows — the id and which mesh
        /// she wears — and touches nothing else. Every handling number on that asset is a TUNABLE the
        /// owner is meant to change from the Inspector without asking anyone (rule 6, and §7 of
        /// CLAUDE.md), so a re-bake that reset his top speed would be taking that away. Created once
        /// with the class's own field defaults; refreshed in place forever after, same guid.</para>
        /// </summary>
        static void EnsureVehicleDef(in VehicleRigFleet.Vehicle v, VehicleMeshDef mesh)
        {
            if (string.IsNullOrEmpty(v.VehicleDefPath)) return;

            EnsureFolder(Path.GetDirectoryName(v.VehicleDefPath).Replace('\\', '/'));
            var def = LoadOrCreate<HiddenHarbours.Vehicles.VehicleDef>(v.VehicleDefPath);
            bool fresh = string.IsNullOrEmpty(def.Id) || def.Id == "vehicle.unnamed";

            def.Id = v.VehicleId;
            def.Mesh = mesh;
            if (fresh) def.DisplayName = v.Label;

            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(v.VehicleDefPath);

            Debug.Log($"[rig-vehicle] {v.VehicleDefPath}: {def.Id} wears {mesh.Id}. " +
                      (fresh
                          ? "Created with the class's tuning defaults — every handling number on it is " +
                            "the owner's to change from the Inspector."
                          : "Refreshed FIELD-SCOPED: her id and her mesh, and nothing the owner has tuned."));
        }

        // =============================================================================================
        //  THE FACTS A FACE LIST DOES NOT CARRY
        // =============================================================================================

        /// <summary>
        /// Read this vehicle's published geometry — the drive door, the driver's seat, the solid box
        /// and (for an amphibian) the four flotation numbers. Refuses on any error rather than
        /// baking a partial answer.
        /// </summary>
        static VehicleSidecarFacts ReadSidecar(in VehicleRigFleet.Vehicle v)
        {
            string full = Path.Combine(RigCatalog.RepoRoot, v.SidecarPath);
            if (!File.Exists(full))
                throw new FileNotFoundException(
                    $"{v.Key}: her gameplay sidecar is missing at {v.SidecarPath}. It carries the " +
                    "drive door, the collider box and the seats — none of which a face list contains " +
                    "— so a bake without it would write four deliberate-looking zeros.", full);

            VehicleSidecarFacts facts =
                VehicleSidecarFacts.Read(File.ReadAllText(full), v.SidecarPath, v.SidecarBodyScope);

            if (facts.Errors.Count > 0)
                throw new InvalidOperationException(
                    $"{v.Key}: {v.SidecarPath} could not be read —\n  " +
                    string.Join("\n  ", facts.Errors));

            return facts;
        }

        /// <summary>
        /// ⭐⭐ <b>Write what the ART published, and write the ABSENCES too.</b>
        ///
        /// <para><b>Why the zeros are written rather than left.</b> This is the Otter's trap, and it
        /// is worth restating because the road fleet is the inverted case. In #581 the baker wrote
        /// geometry and chassis only, so a freshly-baked amphibian carried <c>FloatSink = 0</c>,
        /// <c>Floats</c> read false, and she was a machine that drove into the water and never
        /// floated — with every existing test still green, because they build their defs in code and
        /// none of them read the asset. Here zero is the CORRECT answer for all eight bodies: a box
        /// truck sinks, and a towed body has no driver's door. But "correct" and "nobody looked" must
        /// not be the same bytes, so the flotation block is written from the sidecar on every bake
        /// and its absence is reported in the log as a measured zero.</para>
        ///
        /// <para><b>And they are READ, not typed.</b> The Otter's four numbers and her door were
        /// typed onto her asset by hand and pinned by a test whose own doc says why —
        /// <i>"typed numbers drift"</i>. Eight more bodies is eight more chances; the bake now reads
        /// the same document the test pins it against, so there is one number rather than two.</para>
        /// </summary>
        static void WriteSidecarFacts(in VehicleRigFleet.Vehicle v, VehicleMeshDef def,
                                      VehicleSidecarFacts facts)
        {
            def.DriveDoorLocal = facts.HasDriveDoor ? facts.DriveDoorLocal : Vector2.zero;
            def.DriverSeatLocal = facts.HasDriverSeat ? facts.DriverSeatLocal : Vector3.zero;

            def.ColliderMinMeters = facts.HasCollider ? facts.ColliderMin : Vector3.zero;
            def.ColliderMaxMeters = facts.HasCollider ? facts.ColliderMax : Vector3.zero;

            def.FloatSinkMeters = facts.HasFlotation ? facts.FloatSinkMeters : 0f;
            def.FloatDraftMeters = facts.HasFlotation ? facts.FloatDraftMeters : 0f;
            def.WatertightHalfBeamMeters = facts.HasFlotation ? facts.WatertightHalfBeamMeters : 0f;
            def.WatertightDeckHeightMeters =
                facts.HasFlotation ? facts.WatertightDeckHeightMeters : 0f;

            string where = string.IsNullOrEmpty(facts.BodyScope)
                ? v.SidecarPath
                : $"{v.SidecarPath} → bodies.{facts.BodyScope}";

            Debug.Log(
                $"[rig-vehicle] {v.Key} sidecar facts from {where}: " +
                $"drive door {(facts.HasDriveDoor ? def.DriveDoorLocal.ToString("0.###") : "none")}, " +
                $"driver seat {(facts.HasDriverSeat ? def.DriverSeatLocal.ToString("0.###") : "hidden")}, " +
                $"collider {(def.HasCollider ? $"{def.ColliderMinMeters:0.##}..{def.ColliderMaxMeters:0.##}" : "none")}, " +
                $"floats = {def.Floats}." +
                (facts.Absences.Count == 0
                    ? ""
                    : "\n  Absent, and each absence is an ANSWER rather than a gap:\n    – " +
                      string.Join("\n    – ", facts.Absences)));
        }

        // =============================================================================================
        //  THE ARTICULATION SPLIT
        // =============================================================================================

        /// <summary>One fitting the split found: which faces, how it moves, where it turns.</summary>
        readonly struct FitmentPlan
        {
            public readonly string Slot;
            public readonly List<int> Faces;
            public readonly VehicleFitmentMotion Motion;
            public readonly VehicleFitmentSide Side;
            /// <summary>The point it turns about, in rig metres — the hub centre for a wheel, which
            /// is also a point on the vertical steer axis, so ONE pivot serves both rotations.</summary>
            public readonly Vector3 Pivot;

            public FitmentPlan(string slot, List<int> faces, VehicleFitmentMotion motion,
                               VehicleFitmentSide side, Vector3 pivot)
            {
                Slot = slot; Faces = faces; Motion = motion; Side = side; Pivot = pivot;
            }
        }

        readonly struct VehiclePartition
        {
            public readonly List<int> Body;
            public readonly List<FitmentPlan> Fitments;
            public VehiclePartition(List<int> body, List<FitmentPlan> fitments)
            {
                Body = body; Fitments = fitments;
            }
        }

        /// <summary>
        /// <b>Split the rig's faces into a body and its moving parts, by MEASUREMENT.</b>
        ///
        /// <para>Each group is "the faces whose vertices moved when this one pose axis moved", asked
        /// of the rig rather than read off it by eye. On the Dually that yields, exactly: four roll
        /// groups of 103 (the two front wheels and the two rear DUAL PAIRS, each pair driven by one
        /// axis), two steer-only groups of 40 (the fender lip, hub cover and mudflap that swing with
        /// the corner but do not turn with the tyre), and 661 body faces — 1153 in total, which is
        /// her whole face list.</para>
        ///
        /// <para><b>The partition is ASSERTED, not assumed.</b> Disjoint and covering are both
        /// checked below, and both are load-bearing: an overlap would draw a wheel twice (once
        /// frozen in the body, once posed) and a gap would drop geometry silently. A rig revision
        /// that adds an axis this does not know about fails here rather than shipping a truck with a
        /// piece missing.</para>
        /// </summary>
        static VehiclePartition Partition(IRigScriptHost host, in VehicleRigFleet.Vehicle v, int faceCount)
        {
            string g = v.GlobalName;

            // A face "belongs to" a pose axis when moving that axis alone moved its vertices.
            // Exact inequality is the right test: the rig places a fixed face through the identity
            // both times, so its doubles are bit-identical, while anything that moved moved by far
            // more than a last bit.
            // `sideSign` 0 keeps every face this axis moves; ±1 keeps only those whose centroid is
            // on that side of the centreline. A per-wheel roll axis moves one wheel and needs no
            // filter; a STEER axis moves both front corners at once and does.
            // `yMin`/`yMax` narrow the same claim to ONE AXLE STATION — the filter a side-sign cannot
            // be. A skid-steer machine exports roll per SIDE (the Otter's `rollL`/`rollR`), so one
            // probe moves four wheels that share a side and only their fore-aft station separates
            // them. Defaults are ±Infinity, so a vehicle that does not need it is unaffected.
            // ⚠️⚠️ EVERY POSE IS MERGED ONTO THE VEHICLE'S REST POSE, and on a container rig that
            // rest pose carries the BODY. `__vfaces({})` was the neutral pose until 2026-08-27, and
            // on trailerIsoRig.js that is `reefer53` — so a flatbed's wheel probe would have diffed
            // the default body against the default body, found the reefer's wheels, and handed them
            // to the flatbed's fitting with the right count and no error. An unknown or absent body
            // does not throw on this rig; it falls back. The merge is written so the axis pose WINS
            // on a collision, but the body is never a probe key, so it cannot be overridden.
            host.Execute($@"
                function __vbase(){{ return {v.RestPose}; }}
                function __vpose(o){{
                  var s = __vbase();
                  for (var k in o) s[k] = o[k];
                  return s;
                }}
                function __vfaces(o){{ return {g}.{v.FaceBuilderName}({g}.resolve(__vpose(o))); }}
                function __vmoved(pose, sideSign, yMin, yMax){{
                  var a = __vfaces({{}}), b = __vfaces(pose), out = [];
                  for (var i = 0; i < a.length; i++) {{
                    var fa = a[i].v, fb = b[i].v, d = false;
                    for (var k = 0; k < fa.length && !d; k++)
                      for (var c = 0; c < 3; c++)
                        if (fa[k][c] !== fb[k][c]) {{ d = true; break; }}
                    if (!d) continue;
                    if (sideSign !== 0) {{
                      var cx = 0;
                      for (var m = 0; m < fa.length; m++) cx += fa[m][0];
                      cx /= fa.length;
                      if (sideSign < 0 ? cx >= 0 : cx < 0) continue;
                    }}
                    if (yMin > -Infinity || yMax < Infinity) {{
                      var cy = 0;
                      for (var n = 0; n < fa.length; n++) cy += fa[n][1];
                      cy /= fa.length;
                      if (cy < yMin || cy > yMax) continue;
                    }}
                    out.push(i);
                  }}
                  return out.join(',');
                }}");

            var claimed = new HashSet<int>();
            var fitments = new List<FitmentPlan>();

            // ⚠️ ORDER MATTERS, and it is the catalog's order. Each axis claims only what no earlier
            // axis took, so the SPECIFIC axes must come first: the four per-wheel roll axes take the
            // tyres and hubs, and the steer axes then find only what is left on their side — the
            // knuckle. Listing steer first would swallow both front wheels into the knuckle fittings
            // and leave the roll axes empty, which the emptiness check below would catch.
            foreach (VehicleRigFleet.Axis axis in v.Axes)
            {
                var mine = new List<int>();
                foreach (int i in MovedFaces(host, axis.Probe, axis.SideSign, axis.YMin, axis.YMax))
                    if (!claimed.Contains(i)) mine.Add(i);

                if (mine.Count == 0)
                    throw new InvalidOperationException(
                        $"{v.Key}: pose axis '{axis.Probe}' claimed NO faces, so fitting " +
                        $"'{axis.Slot}' would be an empty mesh. Either the rig dropped the axis, an " +
                        "earlier axis in the catalog already swallowed its geometry (see the ordering " +
                        "note above), or the probe value is a no-op — a roll axis is CYCLIC with " +
                        "period 1, so probing it at 1 ties with 0 exactly and reads as dead. Probe " +
                        "at a quarter.");

                foreach (int i in mine) claimed.Add(i);
                fitments.Add(new FitmentPlan(axis.Slot, mine, axis.Motion, axis.Side, axis.Pivot));
            }

            var body = new List<int>(faceCount - claimed.Count);
            for (int i = 0; i < faceCount; i++)
                if (!claimed.Contains(i)) body.Add(i);

            int total = body.Count;
            foreach (FitmentPlan p in fitments) total += p.Faces.Count;
            if (total != faceCount)
                throw new InvalidOperationException(
                    $"{v.Key}: the articulation split does not partition her faces — body " +
                    $"{body.Count} + fittings {total - body.Count} = {total}, but the rig has " +
                    $"{faceCount}. Overlapping groups draw a wheel twice; a gap drops geometry " +
                    "silently. Refusing to bake.");

            // ⭐ THE PARTITION CHECK ABOVE IS NOT ENOUGH ON ITS OWN, and that was measured rather
            // than reasoned: on 2026-08-17 a static-initialisation-order slip left `Axes` empty, the
            // body took all 1153 faces, and "body 1153 + nothing = 1153" passed cleanly. A truck
            // whose wheels are welded into her body is exactly the silent wrong-bake this table
            // exists to prevent, so the body is checked against the rig DIRECTLY: whatever the
            // master articulation axes move must have been claimed by SOMEONE.
            foreach (string axis in v.BodyMustNotMove)
            {
                var stuck = new List<int>();
                foreach (int i in MovedFaces(host, axis, 0,
                                             float.NegativeInfinity, float.PositiveInfinity))
                    if (!claimed.Contains(i)) stuck.Add(i);

                if (stuck.Count > 0)
                    throw new InvalidOperationException(
                        $"{v.Key}: {stuck.Count} face(s) move under {axis} but no fitting claimed " +
                        "them, so they would be baked INTO THE BODY and frozen there — wheels that " +
                        "never turn, or a steering corner welded straight ahead. Either an axis is " +
                        "missing from this vehicle's Axes list, or the list initialised empty (⚠️ " +
                        "check the static field DECLARATION ORDER — `Axes` must be declared before " +
                        "`Vehicles`, or it is captured as null and every wheel silently ends up here).");
            }

            Debug.Log($"[rig-vehicle] {v.Key} articulation split: body {body.Count} + " +
                      string.Join(" + ", fitments.ConvertAll(p => $"{p.Slot} {p.Faces.Count}")) +
                      $" = {faceCount} faces (disjoint and covering, asserted).");

            return new VehiclePartition(body, fitments);
        }

        static List<int> MovedFaces(IRigScriptHost host, string probePose, int sideSign,
                                    float yMin, float yMax)
        {
            // ⚠️ The bounds are written as JS SOURCE, so they are emitted explicitly rather than
            // left to ToString: a culture or runtime that spells infinity any other way would
            // produce a script that does not parse, and the window is usually infinite.
            string csv = host.EvaluateString(
                $"__vmoved({probePose},{sideSign.ToString(Inv)}," +
                $"{JsNumber(yMin)},{JsNumber(yMax)})");
            var list = new List<int>();
            if (string.IsNullOrEmpty(csv)) return list;
            foreach (string s in csv.Split(','))
                list.Add(int.Parse(s, Inv));
            return list;
        }

        /// <summary>A float as a JS numeric literal. Infinities are spelled the way JS spells
        /// them, because these go into script source rather than into a message.</summary>
        static string JsNumber(float f) =>
            float.IsPositiveInfinity(f) ? "Infinity"
            : float.IsNegativeInfinity(f) ? "-Infinity"
            : f.ToString("R", Inv);

        static List<RigFace> Subset(RigMeshData data, List<int> indices)
        {
            var faces = new List<RigFace>(indices.Count);
            foreach (int i in indices) faces.Add(data.Faces[i]);
            return faces;
        }

        // =============================================================================================
        //  THE AZIMUTH CONVENTION
        // =============================================================================================

        /// <summary>
        /// <b>Which way this rig's <c>dir</c> argument turns the vehicle</b> — from the rig's own
        /// anchors, and <b>never</b> from its silhouette.
        ///
        /// <para>⚠️ <b>This is the one place a vehicle must NOT reuse the hull baker.</b>
        /// <see cref="RigAzimuthProbe"/> finds the bow BY TAPER: it bins the silhouette along its
        /// principal axis and calls the narrower end the bow. That is a fact about boats. A crew-cab
        /// dually is a box — blunt at both ends — so her taper carries no signal at all, and the
        /// same heuristic has already been measured WRONG on eighteen lobster hulls at a taper ratio
        /// of 1.040. Baking a vehicle on the taper would be a coin flip that mirrors her whole
        /// heading mapping when it lands wrong.</para>
        ///
        /// <para><b>The rig answers it properly.</b> Its <c>anchors()</c> publishes <c>wheelFL</c> and
        /// <c>wheelFR</c> — a genuine ABEAM pair (same y, opposite x), which is exactly the oracle
        /// the hull baker prefers when a boat rig offers one. The bearing is read on the UN-SQUASHED
        /// ground plane: the ¾ projection scales screen depth by <c>sin(elevation)</c>, so an angle
        /// taken straight off screen coordinates is wrong by up to 12°, and the un-squash is
        /// self-checking — only the correct divisor lands the eight headings on exact 45° steps.</para>
        ///
        /// <para><b>Confirmed by a second, independent oracle</b> — the centreline fore-and-aft pair,
        /// read as a SIGN on the horizontal axis, which the projection leaves alone. ⚠️ Those two
        /// anchors are named PER VEHICLE (<see cref="VehicleRigFleet.Vehicle.AzimuthAftAnchor"/>):
        /// the abeam pair is <c>wheelFL</c>/<c>wheelFR</c> on every vehicle rig, but the fore-aft one
        /// is each rig's own vocabulary — the Dually says <c>hitch</c>→<c>hoodLatch</c>, the Otter,
        /// being a boat with wheels, says <c>transom</c>→<c>bow</c>. Asking one rig for the other's
        /// anchors reads <c>undefined.x</c>, which is what the Otter's first bake did on 2026-08-19.</para>
        ///
        /// <para>Measured on the Dually 2026-08-17: bearing exactly −90.00° and nose 202.24 px WEST at
        /// a quarter turn. Both counter-clockwise; a disagreement is an ERROR, because a wrong
        /// convention drives her backwards at E/W and this project has shipped that defect five
        /// times.</para>
        /// </summary>
        static AzimuthConvention MeasureAzimuth(IRigScriptHost host, in VehicleRigFleet.Vehicle v,
                                                double elevationDeg)
        {
            string g = v.GlobalName;
            string sin = Math.Sin(elevationDeg * Math.PI / 180.0).ToString("R", Inv);

            // ⚠️⚠️ WHICH BODY THE ANCHORS ARE ASKED FOR. `anchors(dir, {})` answers for whatever the
            // rig resolves, and on a container rig that is its DEFAULT body — so asking once for the
            // file would hand all four trailers reefer53's bearings. RigHullExtraction.ViewOptions
            // is exactly the descriptor for this ("what a probe needs in order to photograph THIS
            // hull rather than the generator's default"), and it is the fourth and last place in
            // this drop where a missing pick does not throw.
            string opts = string.IsNullOrEmpty(v.Extraction?.ViewOptions) ? "{}" : v.Extraction.ViewOptions;

            // ⚠️ The ABEAM pair is per-vehicle. Every DRIVEN rig publishes wheelFL/wheelFR; a towed
            // body has no front axle at all and publishes wheelL/wheelR instead. Asking a trailer
            // for wheelFL reads `undefined.y` — and the admissibility gate below is what turns that
            // into a throw rather than a NaN bearing that quietly resolves CounterClockwise.
            string abeamL = v.AzimuthAbeamLeftAnchor, abeamR = v.AzimuthAbeamRightAnchor;

            if (!host.EvaluateBool($"typeof {g}.anchors === 'function'"))
                throw new InvalidOperationException(
                    $"{v.Key}: the rig publishes no anchors(), so there is no analytic oracle for her " +
                    "azimuth. Refusing to fall back on the silhouette taper — that heuristic is " +
                    "meaningless on a box and would mirror her heading mapping on a coin flip.");

            if (!host.EvaluateBool(
                    $"(function(a){{return !!a && !!a.{abeamL} && !!a.{abeamR} && " +
                    $"Math.abs(a.{abeamR}.y - a.{abeamL}.y) < 1e-6 && " +
                    $"Math.abs(a.{abeamR}.x - a.{abeamL}.x) > 1e-6;}})({g}.anchors(0,{opts}))"))
                throw new InvalidOperationException(
                    $"{v.Key}: '{abeamL}' and '{abeamR}' are not an admissible ABEAM pair at heading 0 " +
                    "(both present, equal screen y, different screen x). Either the axle moved " +
                    "off-square or this machine does not publish those two anchors — a truck's are " +
                    "wheelFL/wheelFR, a towed body's are wheelL/wheelR. Either way the bearing below " +
                    "would not be a vehicle bearing.");

            double bearing = host.EvaluateNumber(
                $"(function(a){{return Math.atan2((a.{abeamR}.y-a.{abeamL}.y)/{sin}," +
                $"a.{abeamR}.x-a.{abeamL}.x)*180/Math.PI;}})({g}.anchors(2,{opts}))");
            AzimuthConvention abeam = bearing > 0 ? AzimuthConvention.Clockwise
                                                  : AzimuthConvention.CounterClockwise;

            string aft = v.AzimuthAftAnchor, fore = v.AzimuthForeAnchor;
            if (string.IsNullOrEmpty(aft) || string.IsNullOrEmpty(fore))
                throw new InvalidOperationException(
                    $"{v.Key}: no centreline azimuth anchors declared. The abeam pair alone is ONE " +
                    "oracle, and this bake refuses to map a heading on one — declare her aft and fore " +
                    "anchor names in VehicleRigFleet (the Dually's are hitch/hoodLatch, the Otter's " +
                    "transom/bow).");

            if (!host.EvaluateBool(
                    $"(function(a){{return !!a && !!a.{aft} && !!a.{fore} && " +
                    $"Math.abs(a.{fore}.x - a.{aft}.x) < 1e-6;}})({g}.anchors(0,{opts}))"))
                throw new InvalidOperationException(
                    $"{v.Key}: '{aft}' and '{fore}' are not an admissible CENTRELINE pair at heading 0 " +
                    "(both present, same screen x). One of them is missing from her anchors(), or it " +
                    "sits off the centreline — either way the nose dx below would not be a fore-aft " +
                    "signal.");

            double noseDx = host.EvaluateNumber(
                $"(function(a){{return a.{fore}.x - a.{aft}.x;}})({g}.anchors(2,{opts}))");
            AzimuthConvention foreAft = noseDx > 0 ? AzimuthConvention.Clockwise
                                                   : AzimuthConvention.CounterClockwise;

            if (abeam != foreAft)
                throw new InvalidOperationException(
                    $"{v.Key}: THE TWO AZIMUTH ORACLES DISAGREE — her front-axle abeam pair says " +
                    $"{abeam} (ground bearing {bearing:F2}° at a quarter turn), her centreline " +
                    $"{aft}→{fore} pair says {foreAft} (nose screen dx {noseDx:F1} px). Both are " +
                    "analytic and neither is a heuristic, so one of them is measuring something " +
                    "other than what it is named. Do not guess: render her beside a registered " +
                    "reference in one host and compare bearings before baking anything.");

            Debug.Log($"[rig-vehicle] {v.Key} azimuth {abeam} — CONFIRMED by two independent oracles: " +
                      $"abeam {abeamL}→{abeamR} ground bearing {bearing:F2}° at a quarter turn, and " +
                      $"centreline {aft}→{fore} screen dx {noseDx:F1} px, both at {opts}. The " +
                      "silhouette taper was NOT consulted: she is a box and it carries no signal.");
            return abeam;
        }

        // =============================================================================================
        //  THE CHASSIS
        // =============================================================================================

        readonly struct Chassis
        {
            public readonly float Wheelbase, FrontTrack, WheelRadius, FrontAxleY, RearAxleY;
            public readonly float MaxInnerDeg, MaxOuterDeg, TravelFront, TravelRear;

            public Chassis(float wheelbase, float frontTrack, float wheelRadius, float frontAxleY,
                           float rearAxleY, float maxInnerDeg, float maxOuterDeg,
                           float travelFront, float travelRear)
            {
                Wheelbase = wheelbase; FrontTrack = frontTrack; WheelRadius = wheelRadius;
                FrontAxleY = frontAxleY; RearAxleY = rearAxleY;
                MaxInnerDeg = maxInnerDeg; MaxOuterDeg = maxOuterDeg;
                TravelFront = travelFront; TravelRear = travelRear;
            }
        }

        /// <summary>Every number the controller solves against, read off the rig's own exports.
        /// Transcription, not tuning — and it is what makes the picture and the physics agree by
        /// construction rather than by two people typing the same value.</summary>
        static Chassis ReadChassis(IRigScriptHost host, in VehicleRigFleet.Vehicle v)
        {
            VehicleRigFleet.VehicleChassisSource src = v.ChassisSource;
            if (src == null)
                throw new InvalidOperationException(
                    $"{v.Key}: no ChassisSource. Every number the controller solves against is read " +
                    "off the rig's own exports, and the expressions are per-vehicle because the two " +
                    "vehicles do not share a vocabulary — the Dually publishes G.axF/G.axR/G.frontWX " +
                    "and a steer block, the Otter an axle ARRAY and no steer at all. Declare hers " +
                    "in VehicleRigFleet rather than teaching this method to guess.");

            // `v` is an `in` parameter and cannot be captured by a local function, so the one field
            // the message needs is copied out first.
            string key = v.Key;

            float Read(string expr, string what)
            {
                double d = host.EvaluateNumber(expr);
                if (double.IsNaN(d))
                    throw new InvalidOperationException(
                        $"{key}: chassis expression for {what} — `{expr}` — evaluated to NaN, which " +
                        "is what a missing export reads as. The rig does not publish what this " +
                        "expression asks for; fix the expression rather than the number it feeds.");
                return (float)d;
            }

            return new Chassis(
                wheelbase: Read(src.Wheelbase, "wheelbase"),
                frontTrack: Read(src.FrontTrack, "front track"),
                wheelRadius: Read(src.WheelRadius, "wheel radius"),
                frontAxleY: Read(src.FrontAxleY, "front axle y"),
                rearAxleY: Read(src.RearAxleY, "rear axle y"),
                maxInnerDeg: Read(src.MaxInnerDeg, "max inner steer"),
                maxOuterDeg: Read(src.MaxOuterDeg, "max outer steer"),
                travelFront: Read(src.TravelFront, "front travel"),
                travelRear: Read(src.TravelRear, "rear travel"));
        }

        // =============================================================================================
        //  WRITING
        // =============================================================================================

        /// <summary>Write one wheel or knuckle as a <see cref="HullPropMeshDef"/>.
        ///
        /// <para><b>That type is reused rather than duplicated</b>, and the reuse is honest: it is
        /// not really "a boat part", it is <i>a rig-baked rigid body with a pivot and a local
        /// rotation</i>, which is exactly what a wheel is. Reusing it means the Art side poses a
        /// wheel and an outboard through one renderer. <c>FixedMesh</c> is left null — a wheel is
        /// one rigid body; the part of the corner that does NOT turn with the tyre is its own
        /// fitting, because it takes the steer while the tyre takes steer AND roll.</para></summary>
        static HullPropMeshDef WriteFitting(in VehicleRigFleet.Vehicle v, in FitmentPlan plan,
                                            RigMeshData data, in Chassis chassis)
        {
            string path = $"{WheelFolder}/{v.Key}_{plan.Slot}.asset";
            RigMeshBuild build = RigMeshBuilder.Build(data.WithFaces(Subset(data, plan.Faces)),
                                                      $"{v.Key}{plan.Slot}Mesh");

            HullPropMeshDef def = LoadOrCreate<HullPropMeshDef>(path);
            def.Id = $"vehicleprop.{ToSnake(v.Key)}_{ToSnake(plan.Slot)}";
            def.SourceRigPath = v.ScriptPath;
            def.SourceFaceBuilder = v.Extraction.FaceExpression;
            def.LightN = data.LightN.ToVector3();
            def.Gain = (float)data.Gain;
            def.Bias = (float)data.Bias;
            def.Keyline = data.Keyline;
            def.PivotPx = new Vector2((float)data.PivotX, (float)data.PivotY);
            def.PxPerMetre = data.PxPerMetre;
            def.CellW = data.W;
            def.CellH = data.H;
            def.ElevationDeg = (float)data.DefaultElev;
            def.PivotLocalMeters = plan.Pivot;
            def.MaxSteerDegrees = plan.Motion == VehicleFitmentMotion.RollOnly ? 0f : chassis.MaxInnerDeg;
            def.MaxTiltDegrees = 0f;
            def.LateralMountsMeters = Array.Empty<float>();
            def.Ramps = ReadRamps(data);
            def.Bayer16 = ReadBayer(data);
            def.FixedMesh = null;

            SwapMesh(def, ref def.Mesh, build.Mesh, path);
            if (!def.IsUsable())
                throw new InvalidOperationException($"Baked fitting at {path} is not usable — see fields.");
            return def;
        }

        static HullMeshDef.Ramp[] ReadRamps(RigMeshData data)
        {
            var ramps = new HullMeshDef.Ramp[data.Materials.Count];
            for (int m = 0; m < data.Materials.Count; m++)
                ramps[m] = new HullMeshDef.Ramp
                {
                    Colors = data.Materials[m].Ramp,
                    Offset = data.Materials[m].Off,
                };
            return ramps;
        }

        static float[] ReadBayer(RigMeshData data)
        {
            var bayer = new float[16];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    bayer[x * 4 + y] = (float)data.Bayer[x, y];
            return bayer;
        }

        /// <summary>
        /// Load an asset, or create it — but ⚠️ <b>an asset that EXISTS and will not LOAD is a
        /// BROKEN one, not a new one</b>, and treating it as new runs the field initialisers and
        /// silently resets everything this baker does not write. The hull baker carries the same
        /// guard for the same reason: a run whose <c>Library/</c> held a stale script→guid map once
        /// zeroed two hand-tuned hull drafts exactly this way.
        /// </summary>
        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            if (File.Exists(path))
                throw new InvalidOperationException(
                    $"{path} exists on disk but did not load as a {typeof(T).Name}, so this bake was " +
                    "about to recreate it and silently reset every field the baker does not write.\n" +
                    "Usual cause: a stale or borrowed Library/ — the script→guid map is wrong and the " +
                    "asset stops resolving to its type. Delete Library/ and let the project reimport, " +
                    "then bake again. If it really is meant to be replaced, delete it (and its .meta) " +
                    "deliberately.");

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Replace a mesh sub-asset rather than accumulating one per bake.</summary>
        static void SwapMesh(ScriptableObject owner, ref Mesh field, Mesh fresh, string path)
        {
            Mesh old = field;
            field = fresh;
            if (old != null)
            {
                AssetDatabase.RemoveObjectFromAsset(old);
                UnityEngine.Object.DestroyImmediate(old, allowDestroyingAssets: true);
            }
            AssetDatabase.AddObjectToAsset(fresh, owner);
            EditorUtility.SetDirty(owner);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
        }

        static string ToSnake(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsUpper(c))
                {
                    if (i > 0 && !char.IsUpper(s[i - 1])) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else sb.Append(c);
            }
            return sb.ToString();
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
#endif
