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
                HullPropMeshDef[] states = null;
                HullPropMeshDef prop;
                if (plan.Motion == VehicleFitmentMotion.DiscreteStates)
                {
                    states = WriteFittingStates(v, plan, chassis, data);
                    prop = states[0];      // the baked-at pose, so a caller that ignores states still draws
                }
                else
                {
                    prop = WriteFitting(v, plan, data, chassis);
                }
                fitments.Add(new VehicleFitment
                {
                    Slot = plan.Slot,
                    Prop = prop,
                    Motion = plan.Motion,
                    Side = plan.Side,
                    HingeAxis = plan.Axis.HingeAxis,
                    SweepDegrees = plan.Axis.SweepDegrees,
                    SlidePath = plan.SlidePath,
                    StateProps = states,
                    StateNames = plan.Axis.StateNames,
                    ParentSlot = plan.Axis.ParentSlot ?? "",
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
            def.DoorGroups = BuildDoorGroups(v, def, facts);

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

            // The coupling, if she does either. Both default to an unpublished struct, which every
            // consumer checks before reading - a zeroed plate would be a coupling at the origin.
            def.FifthWheel = facts.HasFifthWheel ? facts.FifthWheel : default;
            def.Kingpin = facts.HasKingpin ? facts.Kingpin : default;

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
                $"floats = {def.Floats}, " +
                (def.CanTow
                    ? $"fifth wheel seats at {def.FifthWheel.CouplingPointLocal.y:0.##} (slot " +
                      $"{def.FifthWheel.SlotHalfWidthMeters:0.###} x " +
                      $"{Mathf.Abs(def.FifthWheel.SlotSeatY - def.FifthWheel.SlotMouthY):0.##}, ramp " +
                      $"mouth {def.FifthWheel.RampMouthY:0.##}), capture within " +
                      $"{VehicleCouplingMath.CaptureHeadingToleranceDegrees(def.FifthWheel):0.##} deg."
                    : def.IsTowable
                        ? $"kingpin at {def.Kingpin.CouplingPointLocal.y:0.###}, follows on " +
                          $"{def.Kingpin.KingpinToAxleCentreMeters:0.###} m, jackknife cap " +
                          $"{VehicleCouplingMath.JackknifeCapDegrees(def.Kingpin.NoseHalfWidthMeters, def.Kingpin.KingpinSetMeters, 1.52f):0.#} deg."
                        : "neither tows nor is towed.") +
                (facts.Absences.Count == 0
                    ? ""
                    : "\n  Absent, and each absence is an ANSWER rather than a gap:\n    – " +
                      string.Join("\n    – ", facts.Absences)));
        }

        /// <summary>
        /// ⭐ <b>Resolve the declared handles against the art's own INTERACT block.</b>
        ///
        /// <para>The fleet table says which fittings a handle moves — that is a fact about the bake,
        /// because only the bake knows what the fittings are called. WHERE the handle is stays in the
        /// sidecar and is read here, so the place the player stands cannot drift into a C# table and
        /// then disagree with the document it came from.</para>
        ///
        /// <para>Both halves are refusals rather than warnings. A group naming an id the art does not
        /// publish is a handle nobody drew; a group naming a slot this vehicle did not bake is a
        /// handle wired to nothing. Either would install silently and do nothing when pressed.</para>
        /// </summary>
        static VehicleDoorGroup[] BuildDoorGroups(in VehicleRigFleet.Vehicle v, VehicleMeshDef def,
                                                  VehicleSidecarFacts facts)
        {
            if (v.DoorGroups == null || v.DoorGroups.Count == 0)
                return Array.Empty<VehicleDoorGroup>();

            var slots = new HashSet<string>(StringComparer.Ordinal);
            foreach (VehicleFitment f in def.Wheels) slots.Add(f.Slot);

            var built = new List<VehicleDoorGroup>(v.DoorGroups.Count);
            foreach (VehicleRigFleet.DoorGroupSpec spec in v.DoorGroups)
            {
                if (!facts.InteractIds.Contains(spec.Id))
                    throw new InvalidOperationException(
                        $"{v.Key}: a door group is declared for INTERACT id '{spec.Id}', which " +
                        $"{v.SidecarPath} does not publish. It publishes: " +
                        $"{string.Join(", ", facts.InteractIds)}.\n\u26a0\ufe0f The ids do not rhyme across " +
                        "the pack — the van calls her rear pair 'barn' and the trailer kit calls its " +
                        "own 'doors' — so this is very likely one copied from another vehicle. A " +
                        "handle nobody drew installs silently and does nothing when pressed.");

                foreach (string slot in spec.Slots)
                    if (!slots.Contains(slot))
                        throw new InvalidOperationException(
                            $"{v.Key}: door group '{spec.Id}' works a fitting called '{slot}', which " +
                            $"this vehicle did not bake. She has: {string.Join(", ", slots)}.");

                // The sidecar's literal, unless the art published a FORMULA there instead and the
                // fleet resolved it for this body — see DoorGroupSpec.ReachPointOverride. An
                // override where the sidecar ALSO gives a literal would be two sources for one
                // number, so that is refused rather than silently preferred.
                bool has = facts.ReachPoints.TryGetValue(spec.Id, out Vector2 reach);
                if (spec.ReachPointOverride.HasValue)
                {
                    if (has)
                        throw new InvalidOperationException(
                            $"{v.Key}: door group '{spec.Id}' carries a ReachPointOverride, but " +
                            $"{v.SidecarPath} publishes a literal point for it too " +
                            $"({reach.x:0.###}, {reach.y:0.###}). The override exists only for the " +
                            "case where the art writes a per-body FORMULA the reader cannot parse; " +
                            "with a literal available there would be two sources for one number and " +
                            "nothing to say which is current. Delete the override.");
                    reach = spec.ReachPointOverride.Value;
                    has = true;
                }
                built.Add(new VehicleDoorGroup
                {
                    Id = spec.Id,
                    Slots = spec.Slots,
                    ReachPointLocal = has ? reach : Vector2.zero,
                    HasReachPoint = has,
                    Work = spec.Work,
                });
            }

            Debug.Log($"[rig-vehicle] {v.Key} works {built.Count} handle(s): " +
                      string.Join("; ", built.ConvertAll(g =>
                          $"{g.Id} \u2192 [{string.Join(", ", g.Slots)}]" +
                          (g.HasReachPoint ? $" at ({g.ReachPointLocal.x:0.##}, {g.ReachPointLocal.y:0.##})"
                                           : " (the art publishes no numeric reach point)"))));
            return built.ToArray();
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

            /// <summary>The catalog entry this plan came from — carried whole rather than copied
            /// field by field, so a door's hinge, sweep, states and parent reach the written asset
            /// without five more constructor parameters.</summary>
            public readonly VehicleRigFleet.Axis Axis;

            /// <summary>For a Slide: the path measured off the rig, sample by sample.</summary>
            public readonly VehicleSlideSample[] SlidePath;

            public FitmentPlan(string slot, List<int> faces, VehicleFitmentMotion motion,
                               VehicleFitmentSide side, Vector3 pivot,
                               VehicleRigFleet.Axis axis = default,
                               VehicleSlideSample[] slidePath = null)
            {
                Slot = slot; Faces = faces; Motion = motion; Side = side; Pivot = pivot;
                Axis = axis; SlidePath = slidePath;
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
                function __vmoved(pose, sideSign, yMin, yMax, mats){{
                  var a = __vfaces({{}}), b = __vfaces(pose), out = [];
                  for (var i = 0; i < a.length; i++) {{
                    // ⭐ THE MATERIAL CLAIM, and the only filter that separates a landing gear's
                    // rigid shoes from its telescoping legs: they share a side and a station, one
                    // stacked on the other, so neither the side sign nor the y window can. Piped
                    // on both ends so 'iron' never matches 'irons'.
                    if (mats && mats.indexOf('|' + a[i].mat + '|') < 0) continue;
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

            InstallDoorProbes(host);

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
                foreach (int i in MovedFaces(host, axis.Probe, axis.SideSign, axis.YMin, axis.YMax,
                                             axis.Materials))
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

                // ⭐ A DOOR IS PROVED BEFORE IT IS PLANNED. Wheels are believed on their partition
                // alone; a door additionally has to be the rigid thing its declaration claims, and
                // the check runs HERE — on exactly the faces this axis claimed, after the earlier
                // axes took theirs — because that set is what the fitting will actually be.
                VehicleSlideSample[] slide = null;
                if (axis.Motion == VehicleFitmentMotion.HingeRotation)
                    VerifyHinge(host, v, axis, mine);
                else if (axis.Motion == VehicleFitmentMotion.Slide)
                    slide = SampleSlide(host, v, axis, mine);

                fitments.Add(new FitmentPlan(axis.Slot, mine, axis.Motion, axis.Side, axis.Pivot,
                                             axis, slide));
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

        // =============================================================================================
        //  THE DOORS — verified against their published hinge before anything is written
        // =============================================================================================

        /// <summary>
        /// How far, in METRES, a door's worst vertex may sit from where one rotation about its
        /// published pin would put it — <b>0.1 mm</b>, and the number is derived rather than picked.
        ///
        /// <para><b>The floor is float32, not zero.</b> A fitting's pivot is a <c>Vector3</c>, and
        /// the hinges are declared in C# floats: at a 53 ft trailer's coordinates (|y| ≈ 8 m) a
        /// float32 resolves about 1e-6 m, and her barn's own hinge — the rig computes
        /// <c>-S.L/2 + 0.02</c> in double, we store <c>-16.15f/2f + 0.02f</c> — differs by ~2e-7 m
        /// before anything rotates. Swung 255° that becomes a ~1e-6 m residual, which is what she
        /// measured. Demanding zero would be demanding a precision the asset cannot hold.</para>
        ///
        /// <para><b>The ceiling is a pixel.</b> The pack draws at 32 px/m, so one pixel is 31 mm.
        /// This sits 300× below that: nothing it admits can move a rendered pixel.</para>
        ///
        /// <para>⚠️ <b>And nothing in this drop is anywhere near it in either direction</b>, which is
        /// what makes the exact value unimportant. Every door that ships measures ~1e-6 m or less
        /// — a hundred times inside. Every part that genuinely is not one leaf misses by metres:
        /// the liftgate's platform by 0.65 m of radius error, the rollup's slats by 0.48 m at a
        /// quarter travel, the gear's legs by the full 0.78 m of their stretch. The gap between
        /// passing and failing is four orders of magnitude, so this threshold separates kinds, not
        /// degrees — which is the only kind of tolerance worth having.</para>
        /// </summary>
        const double HingeEpsilon = 1e-4;

        /// <summary>The measuring apparatus for doors, installed beside the partition's. Both work over
        /// EVERY vertex of the named faces and neither skips the ones that did not move — the landing
        /// gear's law, and the reason its telescope was mistaken for a rigid translation through two
        /// handoffs.</summary>
        static void InstallDoorProbes(IRigScriptHost host)
        {
            host.Execute(@"
                // How far the named faces sit from where ONE rotation about the published hinge
                // would put them - a residual in METRES, over every vertex.
                //
                // NOT an angle spread, and that distinction cost a bake. Reading each vertex's
                // turned angle and comparing the spread is ill-conditioned exactly where a door
                // leaf keeps most of its vertices: ON the hinge edge, where the radius is ~0 and
                // atan2 turns a last-bit position error into a large angular one. Measured that
                // way these doors reported spreads of 6e-5 to 1.6e-3 degrees while their radius
                // error was EXACTLY 0 - noise dressed as non-rigidity, and the obvious repair
                // would have been to loosen a tolerance until it stopped meaning anything.
                //
                // A positional residual has no such blind spot: a vertex on the axis contributes
                // ~0 because it genuinely does not move, and one at the leaf's free edge
                // contributes its full error. It is also the quantity that matters - does the mesh
                // land where the rotation says. Same law as the landing gear's: measure the
                // POSITIONS, never a scalar derived from them.
                //
                // The angle is the LEAST-SQUARES best fit over every vertex, not one vertex's own.
                // Taking it from a single best-conditioned vertex works and is what this did first,
                // but it inherits whatever rounding that one vertex carries and then charges it to
                // all the others scaled by their radius - which is how a 53 ft reefer's barn door
                // came back 1.03 microns out while the identical door on the 28 ft pup passed. The
                // closed-form fit is one pass and it is the estimator that actually minimises what
                // is being measured, so there is no vertex left to have trusted wrongly.
                // axisKind 'z' rotates in (x,y) leaving z; 'x' rotates in (y,z) leaving x.
                // Returns angleDeg, maxResidualMetres.
                function __vhinge(pose, idxCsv, axisKind, a, b){
                  var A = __vfaces({}), B = __vfaces(pose);
                  var idx = idxCsv.length ? idxCsv.split(',') : [];
                  var iu = axisKind === 'z' ? 0 : 1;
                  var iv = axisKind === 'z' ? 1 : 2;
                  var iw = axisKind === 'z' ? 2 : 0;

                  var sinAcc = 0, cosAcc = 0, seen = 0;
                  for (var n = 0; n < idx.length; n++) {
                    var i = +idx[n], p = A[i].v, q = B[i].v;
                    for (var k = 0; k < p.length; k++) {
                      var pu = p[k][iu] - a, pv = p[k][iv] - b;
                      var qu = q[k][iu] - a, qv = q[k][iv] - b;
                      sinAcc += pu*qv - pv*qu;      // the 2-D Procrustes solution: theta =
                      cosAcc += pu*qu + pv*qv;      // atan2(sum of crosses, sum of dots)
                      seen++;
                    }
                  }
                  if (!seen) return '0,0';
                  var ang = Math.atan2(sinAcc, cosAcc);

                  var c = Math.cos(ang), s = Math.sin(ang), worst = 0;
                  for (var n2 = 0; n2 < idx.length; n2++) {
                    var i2 = +idx[n2], p2 = A[i2].v, q2 = B[i2].v;
                    for (var k2 = 0; k2 < p2.length; k2++) {
                      var du = p2[k2][iu] - a, dv = p2[k2][iv] - b;
                      var eu = (a + du*c - dv*s) - q2[k2][iu];
                      var ev = (b + du*s + dv*c) - q2[k2][iv];
                      var ew = p2[k2][iw] - q2[k2][iw];
                      worst = Math.max(worst, Math.sqrt(eu*eu + ev*ev + ew*ew));
                    }
                  }
                  var deg = ang * 180 / Math.PI;
                  while (deg > 180) deg -= 360;
                  while (deg < -180) deg += 360;
                  return deg + ',' + worst;
                }

                // The named faces' rigid translation between two poses, over EVERY vertex.
                // Returns dx, dy, dz, deviation — deviation 0 means one offset moved all of them.
                function __vslide(fromPose, toPose, idxCsv){
                  var A = __vfaces(fromPose), B = __vfaces(toPose);
                  var idx = idxCsv.length ? idxCsv.split(',') : [];
                  var dx = null, worst = 0;
                  for (var n = 0; n < idx.length; n++) {
                    var i = +idx[n], p = A[i].v, q = B[i].v;
                    for (var k = 0; k < p.length; k++) {
                      var d = [q[k][0]-p[k][0], q[k][1]-p[k][1], q[k][2]-p[k][2]];
                      if (dx === null) dx = d;
                      for (var c = 0; c < 3; c++) worst = Math.max(worst, Math.abs(d[c] - dx[c]));
                    }
                  }
                  return dx === null ? '0,0,0,0' : (dx[0] + ',' + dx[1] + ',' + dx[2] + ',' + worst);
                }");
        }

        /// <summary>
        /// ⭐⭐ <b>A door is verified before it is believed.</b> Rotate every vertex of the faces this
        /// axis claimed back about the hinge it declares, and require that NOTHING is left over: the
        /// radius unchanged, the axis-parallel coordinate unchanged, and every vertex turned through
        /// the same angle — which must be the sweep the art publishes.
        ///
        /// <para><b>Why this is a refusal rather than a warning.</b> A door posed about the wrong pin
        /// or by the wrong angle still draws a door. It arrives shut and it arrives open, and the only
        /// thing wrong is the arc between — which is exactly the volume the sidecars publish
        /// <c>keep_clear</c> for. The landing gear got through two handoffs as "an exact rigid
        /// translation" on a measurement that skipped the vertices that did not move.</para>
        ///
        /// <para>⚠️ <b>The sweep is compared MODULO 360, and that is the trap it exists for.</b> A
        /// reefer's barn opens 255°, which through <c>atan2</c> comes back as −105° — the same pose,
        /// reached the other way round. Declaring the short one sweeps the leaf through the wrong half
        /// of the world: the published fan passes full outboard at 180°, |x| 2.37 m, wider than the
        /// trailer. So the measurement is allowed to wrap and the DECLARATION carries the real sweep.</para>
        /// </summary>
        static void VerifyHinge(IRigScriptHost host, in VehicleRigFleet.Vehicle v,
                                in VehicleRigFleet.Axis axis, List<int> faces)
        {
            string kind = axis.HingeAxis == VehicleHingeAxis.Vertical ? "z" : "x";
            double a = axis.HingeAxis == VehicleHingeAxis.Vertical ? axis.Pivot.x : axis.Pivot.y;
            double b = axis.HingeAxis == VehicleHingeAxis.Vertical ? axis.Pivot.y : axis.Pivot.z;
            string key = v.Key, slot = axis.Slot;

            string[] parts = host.EvaluateString(
                $"__vhinge({axis.Probe},'{string.Join(",", faces)}','{kind}'," +
                $"{a.ToString("R", Inv)},{b.ToString("R", Inv)})").Split(',');

            double measured = double.Parse(parts[0], Inv);
            double residual = double.Parse(parts[1], Inv);

            if (residual > HingeEpsilon)
                throw new InvalidOperationException(
                    $"{key}: '{slot}' is NOT one rigid leaf on the hinge it declares " +
                    $"({kind}-axis at {a:0.###}, {b:0.###}). Turning every vertex of its " +
                    $"{faces.Count} faces through one angle about that pin leaves them up to " +
                    $"{residual:0.#########} m from where the rig actually puts them.\n" +
                    "Either the pin moved or this part is not a leaf at all — the rollup fans and the " +
                    "liftgate is a four-bar linkage, and both are baked at their ends instead (PR 3c). " +
                    "⚠️ Do NOT loosen the tolerance to make a door fit: the residual is in METRES on " +
                    "a 32 px/m grid, so anything this test can see is a change of kind, not of precision.");
            double delta = Math.IEEERemainder(measured - axis.SweepDegrees, 360.0);
            if (Math.Abs(delta) > 1e-4)
                throw new InvalidOperationException(
                    $"{key}: '{slot}' swings {measured:0.####}° (mod 360) but is declared at " +
                    $"{axis.SweepDegrees:0.####}°. The art moved, or the declaration was copied from " +
                    "another door.");

            Debug.Log($"[rig-vehicle] {key} '{slot}': {faces.Count} faces, ONE rigid leaf on the " +
                      $"published {kind}-hinge at ({a:0.###}, {b:0.###}) — worst vertex " +
                      $"{residual:0.#########} m from where one rotation puts it. Declared " +
                      $"{axis.SweepDegrees:0.##}°, measured {measured:0.####}° (mod 360)." +
                      (Math.Abs(axis.SweepDegrees) > 180.0
                          ? " ⚠️ OVER A HALF TURN — animate her the long way; the short way reaches " +
                            "the same pose through the wrong fan."
                          : ""));
        }

        /// <summary>
        /// Sample a sliding part's path off the rig, asserting at every sample that ONE offset moved
        /// all of it. A slide that fails this is a telescope wearing a door's name.
        /// </summary>
        static VehicleSlideSample[] SampleSlide(IRigScriptHost host, in VehicleRigFleet.Vehicle v,
                                                in VehicleRigFleet.Axis axis, List<int> faces)
        {
            float[] ts = axis.SlideSampleTs;
            string key = v.Key, slot = axis.Slot;

            if (ts == null || ts.Length < 2)
                throw new InvalidOperationException(
                    $"{key}: '{slot}' is a Slide with fewer than two path samples. A path needs at " +
                    "least its two ends, and a sample at every corner between them.");

            string indices = string.Join(",", faces);
            var samples = new VehicleSlideSample[ts.Length];

            for (int i = 0; i < ts.Length; i++)
            {
                // The probe names the FULL travel, so a sample pose is that one parameter scaled.
                // ⚠️ t runs from the BAKED pose outward, which on a landing gear is the opposite
                // sense to `gear` — she bakes parked at gear 1 and t = 1 is gear 0, legs up.
                string pose = SamplePose(host, v, axis.Probe, ts[i]);
                string[] parts = host.EvaluateString($"__vslide({{}},{pose},'{indices}')").Split(',');

                double dev = double.Parse(parts[3], Inv);
                if (dev > HingeEpsilon)
                    throw new InvalidOperationException(
                        $"{key}: '{slot}' is not an exact rigid translation at t = {ts[i]} — deviation " +
                        $"{dev:0.######} over every vertex of its {faces.Count} faces. It is not a " +
                        "slide; do not ship it as one. (This is the measurement that separated the " +
                        "landing gear's rigid shoes from its telescoping legs.)");

                samples[i] = new VehicleSlideSample
                {
                    T = ts[i],
                    OffsetMeters = new Vector3(float.Parse(parts[0], Inv),
                                               float.Parse(parts[1], Inv),
                                               float.Parse(parts[2], Inv)),
                };
            }

            Debug.Log($"[rig-vehicle] {key} '{slot}': {faces.Count} faces slide an EXACT rigid " +
                      "translation at every sample — " +
                      string.Join("  ", Array.ConvertAll(samples,
                          x => $"t {x.T:0.###} -> {x.OffsetMeters:F4}")));
            return samples;
        }

        /// <summary>
        /// The pose at fraction <paramref name="t"/> along a slide's travel: its one parameter
        /// interpolated from the value the vehicle BAKES at to the value its probe names.
        ///
        /// <para>⚠️⚠️ <b>NOT the probe value scaled by t</b>, which is the obvious reading and is
        /// wrong on half the fleet. The van's slide bakes shut at <c>slide 0</c> and probes at
        /// <c>slide 1</c>, so scaling happens to work. A trailer's landing gear bakes PARKED at
        /// <c>gear 1</c> and probes RAISED at <c>gear 0</c> — scaling would hand every sample
        /// <c>gear 0</c>, every offset would come back identical, and the path would flatten into
        /// "the shoes never move" with a deviation of 0 at every sample. It would have passed.</para>
        ///
        /// <para>So the rest value is asked of the RIG rather than assumed to be zero, and t runs
        /// from the baked pose outward whichever way the parameter happens to point.</para>
        ///
        /// <para>Deliberately narrow otherwise: exactly one <c>name:number</c> pair, because a slide
        /// is one parameter's travel and a probe naming two leaves "the fraction" meaning nothing.</para>
        /// </summary>
        static string SamplePose(IRigScriptHost host, in VehicleRigFleet.Vehicle v,
                                 string probe, float t)
        {
            string inner = probe.Trim().TrimStart('{').TrimEnd('}');
            int colon = inner.IndexOf(':');
            if (colon < 0 || inner.IndexOf(',') >= 0)
                throw new InvalidOperationException(
                    $"A Slide axis's probe must name exactly one parameter; got '{probe}'. The path is " +
                    "sampled by interpolating that parameter, and a probe naming two has no single " +
                    "fraction to interpolate.");

            string name = inner.Substring(0, colon).Trim();
            double probeValue = double.Parse(inner.Substring(colon + 1).Trim(), Inv);

            double restValue = host.EvaluateNumber(
                $"{v.GlobalName}.resolve({v.RestPose}).{name}");
            if (double.IsNaN(restValue))
                throw new InvalidOperationException(
                    $"{v.Key}: the rig resolves no '{name}' at her rest pose {v.RestPose}, so there is " +
                    "no value for a slide sample to start from. Check the probe names an axis this " +
                    "body actually has.");

            double value = restValue + t * (probeValue - restValue);
            return "{" + name + ":" + value.ToString("R", Inv) + "}";
        }

        /// <summary>
        /// ⚠️ <b>Merge a state's pose onto the vehicle's rest pose</b> — <c>{body:'flatbed28'}</c> and
        /// <c>{gear:0}</c> become <c>{body:'flatbed28',gear:0}</c>.
        ///
        /// <para>On a container rig the rest pose carries the BODY, and a state pose that dropped it
        /// would bake the default trailer's legs under another trailer's name. Same
        /// <c>(file, pick)</c> trap as everywhere else in this drop, in the one place a second
        /// extraction is made.</para>
        /// </summary>
        static string MergePose(string restPose, string statePose)
        {
            string a = (restPose ?? "{}").Trim().TrimStart('{').TrimEnd('}').Trim();
            string b = (statePose ?? "{}").Trim().TrimStart('{').TrimEnd('}').Trim();
            if (a.Length == 0) return "{" + b + "}";
            if (b.Length == 0) return "{" + a + "}";
            return "{" + a + "," + b + "}";
        }

        static List<int> MovedFaces(IRigScriptHost host, string probePose, int sideSign,
                                    float yMin, float yMax, string[] materials = null)
        {
            // ⚠️ The bounds are written as JS SOURCE, so they are emitted explicitly rather than
            // left to ToString: a culture or runtime that spells infinity any other way would
            // produce a script that does not parse, and the window is usually infinite.
            string mats = materials == null || materials.Length == 0
                ? "null"
                : "'|" + string.Join("|", materials) + "|'";
            string csv = host.EvaluateString(
                $"__vmoved({probePose},{sideSign.ToString(Inv)}," +
                $"{JsNumber(yMin)},{JsNumber(yMax)},{mats})");
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
                                            RigMeshData data, in Chassis chassis,
                                            string stateName = "")
        {
            string suffix = stateName.Length == 0 ? "" : "_" + stateName;
            string path = $"{WheelFolder}/{v.Key}_{plan.Slot}{suffix}.asset";
            RigMeshBuild build = RigMeshBuilder.Build(data.WithFaces(Subset(data, plan.Faces)),
                                                      $"{v.Key}{plan.Slot}{suffix}Mesh");

            HullPropMeshDef def = LoadOrCreate<HullPropMeshDef>(path);
            def.Id = $"vehicleprop.{ToSnake(v.Key)}_{ToSnake(plan.Slot)}" +
                     (suffix.Length == 0 ? "" : "_" + stateName);
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
            // ⚠️ ONLY A STEERED WHEEL CARRIES THE STEERING LOCK. A door's travel is its own
            // published sweep and lives on the fitment, not here — handing a barn door the truck's
            // 30° Ackermann limit would be a number that means nothing about her.
            def.MaxSteerDegrees =
                plan.Motion == VehicleFitmentMotion.SteerAndRoll ||
                plan.Motion == VehicleFitmentMotion.SteerOnly ? chassis.MaxInnerDeg : 0f;
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

        /// <summary>
        /// ⚠️ <b>Bake a part that is NOT rigid at each end of its travel instead of faking it.</b>
        ///
        /// <para><b>Two ways to claim the faces, and the art picks which.</b></para>
        ///
        /// <list type="number">
        ///   <item><b>One probe (no <c>StateProbes</c>).</b> Every state is a pose of the same build,
        ///   so the face list keeps its length and its order and each state takes the SAME indices
        ///   the probe claimed. The landing gear's legs are this.</item>
        ///   <item><b>A probe per state.</b> The part's face COUNT changes across its travel, so
        ///   indices claimed in one build name different geometry in another and each state must be
        ///   claimed inside its own build. The rollup is this — her curtain rolls into a stack and
        ///   six faces stop existing (1090 → 1084 cabover, 1211 → 1205 conventional).</item>
        /// </list>
        ///
        /// <para>⭐ <b>The second way is only sound if the parameter touches the part and nothing
        /// else, so that is PROVED here rather than assumed.</b> Every state's build, minus that
        /// state's claimed faces, must equal the rest build minus its own — face for face, vertex for
        /// vertex, material for material. Measured today at exactly 0 on both trucks (1072 faces and
        /// 1190), and re-measured at every bake: a re-stamp that made the rollup nudge anything but
        /// her own door fails here instead of shipping a body that quietly changes shape when a door
        /// opens.</para>
        ///
        /// <para>⚠️ Each state pose is MERGED onto the vehicle's rest pose, so a container rig's body
        /// pick survives into the second extraction. Dropping it would bake the default trailer's
        /// legs under another trailer's name — the same silent fallback that runs through this whole
        /// drop.</para>
        /// </summary>
        static HullPropMeshDef[] WriteFittingStates(in VehicleRigFleet.Vehicle v, in FitmentPlan plan,
                                                    in Chassis chassis, RigMeshData restData)
        {
            string[] names = plan.Axis.StateNames, poses = plan.Axis.StatePoses,
                     probes = plan.Axis.StateProbes;
            if (names == null || poses == null || names.Length != poses.Length || names.Length < 2)
                throw new InvalidOperationException(
                    $"{v.Key}: '{plan.Slot}' is a DiscreteStates fitting without at least two matching " +
                    "state names and poses. A part baked at its ends needs both ends named.");
            if (probes != null && probes.Length != names.Length)
                throw new InvalidOperationException(
                    $"{v.Key}: '{plan.Slot}' names {names.Length} states but {probes.Length} state " +
                    "probes. A per-state claim needs one probe per state — see Axis.StateProbes.");

            var props = new HullPropMeshDef[names.Length];
            var counts = new int[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string pose = MergePose(v.RestPose, poses[i]);

                // A FRESH host per state: ExtractFrom runs the rig source, and re-running it into a
                // host that already holds one would stack two copies of the same IIFE.
                using IRigScriptHost stateHost = RigScriptHostFactory.Create();
                RigMeshData stateData = Build(stateHost, v, pose);

                List<int> faces;
                if (probes == null)
                {
                    // ---- one probe: every state is a pose of the same build ---------------------
                    if (stateData.Faces.Count <= plan.Faces[plan.Faces.Count - 1])
                        throw new InvalidOperationException(
                            $"{v.Key}: '{plan.Slot}' state '{names[i]}' built {stateData.Faces.Count} " +
                            $"faces, too few to hold the indices the probe claimed (up to " +
                            $"{plan.Faces[plan.Faces.Count - 1]}). A state that changes the face COUNT " +
                            "is a different build, not a pose — give this axis a StateProbes array so " +
                            "each state is claimed inside its own build.");
                    faces = plan.Faces;
                }
                else
                {
                    // ---- a probe per state: claimed INSIDE this build ---------------------------
                    using IRigScriptHost probeHost = RigScriptHostFactory.Create();
                    RigMeshData probeData =
                        Build(probeHost, v, MergePose(v.RestPose, probes[i]));

                    // ⚠️ The probe has to sit on the SAME SIDE of the topology change as its state.
                    // If it does not, the two builds are different shapes and the diff below would
                    // be meaningless — so this is checked rather than assumed. A re-stamp that moved
                    // the change past a probe value lands here.
                    if (probeData.Faces.Count != stateData.Faces.Count)
                        throw new InvalidOperationException(
                            $"{v.Key}: '{plan.Slot}' state '{names[i]}' builds " +
                            $"{stateData.Faces.Count} faces but its probe {probes[i]} builds " +
                            $"{probeData.Faces.Count}. The probe has crossed the part's topology " +
                            "change, so it can no longer say which faces are this state's. Move it " +
                            "to the state's own side of the change.");

                    faces = ClaimMoved(stateData, probeData, plan.Axis);
                    if (faces.Count == 0)
                        throw new InvalidOperationException(
                            $"{v.Key}: '{plan.Slot}' state '{names[i]}' claimed NO faces from probe " +
                            $"{probes[i]} — the probe is a no-op at this state, so the fitting would " +
                            "be an empty mesh.");

                    ProveTheBodyIsUntouched(v, plan, restData, stateData, faces, names[i]);
                }

                counts[i] = faces.Count;
                props[i] = WriteFitting(v, WithFaces(plan, faces), stateData, chassis, names[i]);
            }

            string sizes;
            if (probes == null) sizes = $"{plan.Faces.Count} faces";
            else
            {
                var parts = new string[names.Length];
                for (int p2 = 0; p2 < names.Length; p2++) parts[p2] = $"{counts[p2]} {names[p2]}";
                sizes = string.Join(" / ", parts);
            }
            Debug.Log($"[rig-vehicle] {v.Key} '{plan.Slot}': {sizes} baked at {names.Length} states " +
                      $"({string.Join(", ", names)}) because the part is not rigid — it neither " +
                      "rotates nor translates, so there is nothing to pose it by." +
                      (probes == null ? "" : " Claimed per state; body proved untouched."));
            return props;
        }

        /// <summary>One extraction of the whole body at a pose — the shape every state and probe
        /// build takes, in one place so they cannot drift apart.</summary>
        static RigMeshData Build(IRigScriptHost host, in VehicleRigFleet.Vehicle v, string pose) =>
            RigMeshExtractor.ExtractFrom(
                host, v.ScriptPath, v.GlobalName,
                hull: new RigHullExtraction
                {
                    FaceExpression = $"{v.FaceBuilderName}({v.GlobalName}.resolve({pose}))",
                    ExtraSymbols = v.Extraction.ExtraSymbols,
                    HullScope = v.Extraction.HullScope,
                    ViewOptions = v.Extraction.ViewOptions,
                });

        static FitmentPlan WithFaces(in FitmentPlan plan, List<int> faces) =>
            new FitmentPlan(plan.Slot, faces, plan.Motion, plan.Side, plan.Pivot,
                            plan.Axis, plan.SlidePath);

        /// <summary>
        /// The same claim <c>__vmoved</c> makes, asked of two builds already in hand — used when a
        /// state has to be claimed inside its own build rather than the rest one. The filters are
        /// applied in the same order and with the same meaning, so a per-state claim and a
        /// rest-claim cannot disagree about what a side sign or a station window means.
        /// </summary>
        static List<int> ClaimMoved(RigMeshData a, RigMeshData b, in VehicleRigFleet.Axis axis)
        {
            var mats = axis.Materials == null || axis.Materials.Length == 0
                ? null : new HashSet<string>(axis.Materials);
            var outIdx = new List<int>();

            for (int i = 0; i < a.Faces.Count; i++)
            {
                RigFace fa = a.Faces[i], fb = b.Faces[i];
                if (mats != null && !mats.Contains(a.Materials[fa.Mat].Name)) continue;

                bool moved = false;
                for (int k = 0; k < fa.V.Length && !moved; k++)
                    if (fa.V[k].X != fb.V[k].X || fa.V[k].Y != fb.V[k].Y || fa.V[k].Z != fb.V[k].Z)
                        moved = true;
                if (!moved) continue;

                if (axis.SideSign != 0)
                {
                    double cx = 0;
                    for (int m = 0; m < fa.V.Length; m++) cx += fa.V[m].X;
                    cx /= fa.V.Length;
                    if (axis.SideSign < 0 ? cx >= 0 : cx < 0) continue;
                }
                if (axis.YMin > float.NegativeInfinity || axis.YMax < float.PositiveInfinity)
                {
                    double cy = 0;
                    for (int n = 0; n < fa.V.Length; n++) cy += fa.V[n].Y;
                    cy /= fa.V.Length;
                    if (cy < axis.YMin || cy > axis.YMax) continue;
                }
                outIdx.Add(i);
            }
            return outIdx;
        }

        /// <summary>
        /// ⭐ <b>The proof that lets a body be baked once.</b> Remove this state's claimed part from
        /// its own build, remove the rest-claimed part from the rest build, and the two remainders
        /// must be the same body — same face count, same materials, same vertices exactly.
        ///
        /// <para>Without this, a per-state claim would be an assumption: "the parameter only moves
        /// the door". Measured today at worst-vertex-delta 0 on both box trucks (1072 faces and
        /// 1190), and re-measured here at every bake, it is a fact the bake refuses to proceed
        /// without. A rig change that made a rollup nudge her own door frame — plausible, invisible
        /// in play, and permanent once baked — stops here.</para>
        /// </summary>
        static void ProveTheBodyIsUntouched(in VehicleRigFleet.Vehicle v, in FitmentPlan plan,
                                            RigMeshData restData, RigMeshData stateData,
                                            List<int> stateFaces, string stateName)
        {
            List<RigFace> restBody = Remainder(restData, plan.Faces);
            List<RigFace> stateBody = Remainder(stateData, stateFaces);

            if (restBody.Count != stateBody.Count)
                throw new InvalidOperationException(
                    $"{v.Key}: '{plan.Slot}' state '{stateName}' leaves {stateBody.Count} body faces " +
                    $"but the rest build leaves {restBody.Count}. The parameter is moving more than " +
                    "this part, so the body cannot be baked once and shared across the states.");

            for (int i = 0; i < restBody.Count; i++)
            {
                RigFace p = restBody[i], q = stateBody[i];
                if (restData.Materials[p.Mat].Name != stateData.Materials[q.Mat].Name)
                    throw new InvalidOperationException(
                        $"{v.Key}: '{plan.Slot}' state '{stateName}' body face {i} is " +
                        $"'{stateData.Materials[q.Mat].Name}' where the rest build has " +
                        $"'{restData.Materials[p.Mat].Name}'. The body is not the same body.");

                if (p.V.Length != q.V.Length)
                    throw new InvalidOperationException(
                        $"{v.Key}: '{plan.Slot}' state '{stateName}' body face {i} has {q.V.Length} " +
                        $"vertices where the rest build has {p.V.Length}.");

                for (int k = 0; k < p.V.Length; k++)
                    if (p.V[k].X != q.V[k].X || p.V[k].Y != q.V[k].Y || p.V[k].Z != q.V[k].Z)
                        throw new InvalidOperationException(
                            $"{v.Key}: '{plan.Slot}' state '{stateName}' moved a BODY vertex — face " +
                            $"{i}, vertex {k}, ({p.V[k].X:0.######}, {p.V[k].Y:0.######}, " +
                            $"{p.V[k].Z:0.######}) → ({q.V[k].X:0.######}, {q.V[k].Y:0.######}, " +
                            $"{q.V[k].Z:0.######}). Working this part is supposed to move the part " +
                            "and nothing else; here it reshapes the truck, and baking the body once " +
                            "would freeze whichever state happened to be extracted first.");
            }
        }

        static List<RigFace> Remainder(RigMeshData data, List<int> taken)
        {
            var drop = new HashSet<int>(taken);
            var rest = new List<RigFace>(data.Faces.Count - drop.Count);
            for (int i = 0; i < data.Faces.Count; i++)
                if (!drop.Contains(i)) rest.Add(data.Faces[i]);
            return rest;
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
