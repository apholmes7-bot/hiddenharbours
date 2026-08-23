#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Bakes the INTERIOR KIT — <see cref="InteriorKit.RoomSet"/> and <see cref="InteriorKit.PropSet"/> —
    /// and writes the contract its consumers read. The sibling of
    /// <see cref="VillageBuildingBakeMenu"/>, and deliberately shaped the same way.
    ///
    /// <para>Menu: <c>Hidden Harbours ▸ Art ▸ Bake Interiors (rooms + props)</c>. Re-runnable; each
    /// build overwrites its own sheet and touches nothing else.</para>
    ///
    /// <para><b>What the contract carries that the building one does not:
    /// <c>exteriorFacingOffset</c>.</b> A room is the inside of a shell, and the two rigs put their
    /// doors on OPPOSITE gables, so the room that belongs under exterior facing <c>f</c> is not
    /// interior facing <c>f</c>. The offset is measured here (the one constant that lines the two door
    /// anchors up at all eight facings) rather than declared anywhere, and the full working is written
    /// into the contract beside it so a later reader can check the measurement instead of trusting the
    /// number.</para>
    /// </summary>
    public static class InteriorBakeMenu
    {
        public static string RoomsFolder => InteriorKit.RoomsRoot.TrimEnd('/');
        public static string PropsFolder => InteriorKit.PropsRoot.TrimEnd('/');

        // Priority 60 continues the Art menu's unbroken bake/slice run (the village bake sits at 59).
        // Unity draws a separator at any priority gap over 10, so a "sensible" 80 would render this
        // command off in its own island and read as missing.
        [MenuItem("Hidden Harbours/Art/Bake Interiors (rooms + props)", priority = 60)]
        public static void BakeAllMenu()
        {
            string report = BakeAll(out int failed);
            AssetDatabase.Refresh();

            if (failed > 0)
                Debug.LogError($"[interiors] {failed} build(s) FAILED — nothing was written for them and " +
                               $"the contract covers only what succeeded.\n{report}");
            else
                Debug.Log($"[interiors] Bake complete.\n{report}\n" +
                          "Next: Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Slice Interior " +
                          "Sheets, then rebuild St Peters.");
        }

        /// <summary>
        /// Bake every room and prop and write the contract. Returns a human-readable report and reports
        /// how many builds failed. A failed build is left OUT of the contract rather than entered with
        /// guessed geometry: a contract that claims a sheet which is not there makes the slicer slice a
        /// stale sheet against a fresh cell.
        /// </summary>
        public static string BakeAll(out int failed)
        {
            failed = 0;
            var log = new StringBuilder();
            var rooms = new List<InteriorKit.Entry>();
            var props = new List<InteriorKit.Entry>();
            var results = new List<InteriorBakeResult>();

            int ppu = 0;
            string convention = "";

            log.AppendLine("  rooms:");
            foreach (var build in InteriorKit.RoomSet)
            {
                try
                {
                    InteriorBakeResult r = InteriorRigBaker.Bake(RequestFor(build));
                    rooms.Add(ToEntry(build, r));
                    results.Add(r);
                    ppu = r.PixelsPerMetre;
                    convention = r.Convention.ToString();
                    log.AppendLine(InteriorRigBaker.Describe(r));
                }
                catch (Exception e)
                {
                    failed++;
                    log.AppendLine($"  ✗ {build.Key}: {e.Message}");
                    Debug.LogError($"[interiors] room '{build.Key}' FAILED:\n{e}");
                }
            }

            log.AppendLine("  props:");
            foreach (var build in InteriorKit.PropSet)
            {
                try
                {
                    InteriorBakeResult r = InteriorRigBaker.Bake(RequestFor(build));
                    props.Add(ToEntry(build, r));
                    results.Add(r);
                    if (ppu == 0) ppu = r.PixelsPerMetre;
                    log.AppendLine(InteriorRigBaker.Describe(r));
                }
                catch (Exception e)
                {
                    failed++;
                    log.AppendLine($"  ✗ {build.Key}: {e.Message}");
                    Debug.LogError($"[interiors] prop '{build.Key}' FAILED:\n{e}");
                }
            }

            if (rooms.Count > 0)
            {
                string contractPath = WriteContract(rooms, props, ppu, convention, out int offset);
                log.AppendLine();
                log.AppendLine($"  contract: {contractPath} — {rooms.Count} room(s), {props.Count} " +
                               $"prop(s), interior facing = exterior facing + {offset} (MEASURED).");
            }
            else
            {
                log.AppendLine();
                log.AppendLine("  NO contract written — not one room baked, and a contract with no room " +
                               "cannot carry the measured facing offset every consumer reads.");
            }

            log.AppendLine();
            log.AppendLine($"  {rooms.Count + props.Count} baked, {failed} failed.");
            if (results.Count > 0)
            {
                long png = 0, rgba = 0, uncropped = 0;
                int widest = 0, tallest = 0;
                foreach (var r in results)
                {
                    png += r.PngBytes;
                    rgba += r.RuntimeBytesRgba32;
                    uncropped += r.UncroppedBytesRgba32;
                    widest = Mathf.Max(widest, r.SheetWidth);
                    tallest = Mathf.Max(tallest, r.SheetHeight);
                }
                log.AppendLine(
                    $"  budget: biggest sheet {widest}×{tallest} px against the " +
                    $"{InteriorKit.ImportSizeCap} px import cap; {rgba / 1024.0 / 1024.0:F2} MiB RGBA32 " +
                    $"cropped vs {uncropped / 1024.0 / 1024.0:F0} MiB uncropped " +
                    $"({1.0 - rgba / (double)uncropped:P0} saved); {png / 1024.0 / 1024.0:F2} MiB of PNG " +
                    "on disk.");
            }

            return log.ToString();
        }

        /// <summary>
        /// The bake request for a build — the ONE place the kit's preset/dialled split turns into an
        /// options expression. Pure and static (no JS engine), so a test can assert what a build asks
        /// the rig to draw without baking it.
        /// </summary>
        public static InteriorBakeRequest RequestFor(InteriorKit.Build build)
        {
            if (build.IsProp)
                return InteriorBakeRequest.Prop(
                    build.PropName, OptionsLiteralFor(build), build.Label,
                    PropsFolder, InteriorKit.PropStemFor(build.Key), InteriorKit.Facings,
                    InteriorKit.ImportSizeCap,
                    // ⚠️ OFF for props, and this is a measured decision rather than a shortcut. The
                    // room rig's `g()` falls back to a RIG-WIDE default, so a room whose options were
                    // all ignored renders the generic room and the byte comparison sees it. The prop
                    // rig's `g()` falls back to that PROP's own `def` — so a bed asked for the walnut
                    // frame and sage quilt the rig already gives a bed renders byte-identical to the
                    // default BY DESIGN. Leaving the tripwire on would force every prop to be dialled
                    // away from the rig's own taste purely to satisfy it.
                    //
                    // What defends a prop instead is the failure that is actually silent for one: an
                    // unknown NAME. `PROPS[name] || PROPS.chair` means a typo bakes a chair under the
                    // dresser's name and nothing says a word — so the baker asserts the name, and
                    // InteriorRigBakeTests checks every key in the set against the rig's own list.
                    requireDistinctFromDefault: false);

            if (build.IsPreset)
                return InteriorBakeRequest.RoomPreset(
                    build.Preset, RigCatalog.Get("interior").GlobalName,
                    RoomsFolder, InteriorKit.RoomStemFor(build.Key), InteriorKit.Facings,
                    InteriorKit.ImportSizeCap);

            return InteriorBakeRequest.Room(
                OptionsLiteralFor(build), build.Label,
                RoomsFolder, InteriorKit.RoomStemFor(build.Key), InteriorKit.Facings,
                InteriorKit.ImportSizeCap);
        }

        /// <summary>The dialled options as the JS literal <c>render()</c> takes, through the same
        /// serialiser the building kit uses (invariant culture, so a comma decimal separator can never
        /// split an option in two).</summary>
        public static string OptionsLiteralFor(InteriorKit.Build build)
        {
            if (build.IsPreset)
                throw new ArgumentException(
                    $"'{build.Key}' is a preset build — its options come from the rig's PRESETS table, " +
                    "spread by InteriorBakeRequest.RoomPreset.", nameof(build));

            var dialled = new List<KeyValuePair<string, object>>();
            foreach (var kv in build.Dialled) dialled.Add(kv);
            return BuildingAxes.ToOptionsLiteral(dialled);
        }

        // =================================================================================
        //  the contract
        // =================================================================================

        static InteriorKit.Entry ToEntry(InteriorKit.Build build, InteriorBakeResult r)
        {
            var rig = RigCatalog.Get(r.RigKey);
            var e = new InteriorKit.Entry
            {
                key = build.Key,
                label = build.Label,
                rig = r.RigKey,
                rigScript = rig.ScriptPath,
                rigGlobal = rig.GlobalName,
                propName = build.PropName ?? "",
                sheet = Path.GetFileName(r.AssetPath),
                optionsJs = build.IsPreset
                    ? $"Object.assign({{}},{rig.GlobalName}.PRESETS['{build.Preset}'])"
                    : OptionsLiteralFor(build),
                facings = r.Facings,
                cols = r.Columns,
                rows = r.Rows,
                cellW = r.CellWidth,
                cellH = r.CellHeight,
                nativeCellW = r.NativeCellWidth,
                nativeCellH = r.NativeCellHeight,
                cropX = r.CropX,
                cropY = r.CropY,
                pivotX = (float)r.PivotX,
                pivotY = (float)r.PivotY,
                sheetW = r.SheetWidth,
                sheetH = r.SheetHeight,
                footprintWidthMetres = (float)r.FootprintWidthMetres,
                footprintLengthMetres = (float)r.FootprintLengthMetres,
                storeyHeightMetres = (float)r.StoreyHeightMetres,
                propFootprintWidth = (float)r.PropFootprintWidth,
                propFootprintDepth = (float)r.PropFootprintDepth,
                convention = r.Convention.ToString(),
                doorX = ToFloats(r.DoorX),
                doorY = ToFloats(r.DoorY),
                floorX = ToFloats(r.FloorX),
                floorY = ToFloats(r.FloorY),
                hearthX = ToFloats(r.HearthX),
                hearthY = ToFloats(r.HearthY),
                pngBytes = r.PngBytes,
                runtimeBytesRgba32 = r.RuntimeBytesRgba32,
                cropSaving = (float)r.CropSaving,
            };

            // The Unity pivot is DERIVED from the cropped pivot rather than stored independently, so the
            // two can never disagree — InteriorKit.NormalizedPivot is the single definition and a test
            // asserts the written values equal it.
            Vector2 unity = InteriorKit.NormalizedPivot(e);
            e.unityPivotX = unity.x;
            e.unityPivotY = unity.y;
            return e;
        }

        static float[] ToFloats(double[] src)
        {
            if (src == null) return Array.Empty<float>();
            var dst = new float[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = (float)src[i];
            return dst;
        }

        /// <summary>
        /// Write <see cref="InteriorKit.ContractPath"/>, MEASURING the exterior↔interior facing offset
        /// on the way through.
        ///
        /// <para>The measurement runs against the first room that has a shell in
        /// <see cref="VillageBuildingKit"/>: install both rigs into one host, check they project
        /// identically at every facing, then find the single offset that puts the room's doorway on the
        /// shell's door at all eight. The full working goes into the contract as
        /// <c>registrationReport</c> — a number nobody can check is a declaration again.</para>
        /// </summary>
        static string WriteContract(List<InteriorKit.Entry> rooms, List<InteriorKit.Entry> props,
                                    int ppu, string convention, out int offset)
        {
            InteriorRigAzimuthProbe.Registration reg = MeasureRegistration(rooms);
            offset = reg.FacingOffset;

            var contract = new InteriorKit.Contract
            {
                note = "GENERATED by Hidden Harbours ▸ Art ▸ Bake Interiors. Do not hand-edit: every " +
                       "number here is read from the rigs at bake time (ADR 0021 §4), so an edit is " +
                       "silently undone by the next bake. Re-bake instead.",
                writtenBy = nameof(InteriorBakeMenu),
                ppu = ppu,
                facings = InteriorKit.Facings,
                exteriorFacingOffset = offset,
                conventionNote =
                    "MEASURED at bake time. The room rig shares houseIsoRig's camBasis/projVert — " +
                    "checked per facing by InteriorRigAzimuthProbe.ProjectionsAgree, not read off a " +
                    "header — so it turns the same way and the correction is already applied to the " +
                    "sheet: cell i genuinely depicts +45*i. ⚠️ Do NOT run BuildingRigAzimuthProbe on " +
                    "this rig: it infers handedness from which side the DOOR lands on and assumes the " +
                    "door is on the +Y gable, which is true of the exterior rigs and false here.",
                pivotNote =
                    "pivotX/pivotY are the FLOOR CENTRE - the middle of the room's footprint - in " +
                    "cropped-cell px from the cell's TOP-LEFT. Unity wants (pivotX/cellW, " +
                    "(cellH-pivotY)/cellH), which is NOT bottom-centre: under the 3/4 camera the whole " +
                    "NEAR HALF of the floor projects below that centre, so a bottom-centre pivot sinks " +
                    "the room by that much, silently.",
                registrationReport = reg.Report,
                rooms = rooms.ToArray(),
                props = props.ToArray(),
            };

            string abs = Path.Combine(RigCatalog.RepoRoot, InteriorKit.ContractPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            File.WriteAllText(abs, JsonUtility.ToJson(contract, prettyPrint: true));
            return InteriorKit.ContractPath;
        }

        /// <summary>
        /// Measure the facing offset against <b>every</b> room that has a shell, and refuse if they do
        /// not all agree. Throws rather than defaulting: an unmeasured offset written as 0 would put
        /// every doorway against the back wall, and it would look like the art was wrong.
        ///
        /// <para><b>⚠️ THIS USED TO RETURN ON THE FIRST ROOM IT COULD MEASURE.</b> With one room baked
        /// that was the same thing; with four it is a kit-wide constant derived from whichever row
        /// happens to sort first, applied unchecked to the rest. The contract carries ONE
        /// <c>exteriorFacingOffset</c> because the house family genuinely has one — but "genuinely"
        /// is a measurement, not an assumption, and it is the kind that stays true right up until a
        /// build is added whose shell turns differently. Measuring all of them costs four probes at
        /// bake time and turns a silent half-turn into a refusal that names the building.</para>
        ///
        /// <para>Each probe also re-checks that its room and its shell resolve the SAME footprint, so
        /// this is where a room dialled to the wrong <c>size</c> is caught as well.</para>
        /// </summary>
        static InteriorRigAzimuthProbe.Registration MeasureRegistration(List<InteriorKit.Entry> rooms)
        {
            var houseRig = RigCatalog.Get("house");
            var interiorRig = RigCatalog.Get("interior");

            InteriorRigAzimuthProbe.Registration first = default;
            string firstKey = null;
            var report = new StringBuilder();

            foreach (var room in rooms)
            {
                string exteriorKey = InteriorKit.ExteriorKeyFor(room.key);
                if (exteriorKey == null) continue;

                var exterior = VillageBuildingKit.FindBuild(exteriorKey);
                if (exterior == null) continue;

                using IRigScriptHost host = RigScriptHostFactory.Create();
                RigCatalog.Install(host, houseRig);
                RigCatalog.Install(host, interiorRig);

                string exteriorOpts = exterior.Value.IsPreset
                    ? $"Object.assign({{}},{houseRig.GlobalName}.PRESETS['{exterior.Value.Preset}'])"
                    : VillageBuildingBakeMenu.OptionsLiteralFor(exterior.Value);

                InteriorRigAzimuthProbe.Registration reg = InteriorRigAzimuthProbe.MeasureRegistration(
                    host, houseRig.GlobalName, exteriorOpts,
                    interiorRig.GlobalName, room.optionsJs, InteriorKit.Facings);

                report.AppendLine($"--- {room.key} (inside '{exteriorKey}') ---");
                report.AppendLine(reg.Report);

                if (firstKey == null) { first = reg; firstKey = room.key; continue; }

                if (reg.FacingOffset != first.FacingOffset)
                    throw new InvalidOperationException(
                        $"[interiors] THE ROOMS DO NOT AGREE ON THE FACING OFFSET. '{firstKey}' " +
                        $"measures +{first.FacingOffset} and '{room.key}' measures " +
                        $"+{reg.FacingOffset}, but the contract carries ONE offset for the whole kit " +
                        "and every consumer reads it through InteriorKit.InteriorFacingFor.\n\n" +
                        report +
                        "\nOne of these two shells turns differently from the other, so no single " +
                        "constant can be right for both: whichever room lost the coin-flip would " +
                        "stand under its house a half-turn out, with its doorway against the back " +
                        "wall. Refusing rather than picking one. If the kit really does need two " +
                        "offsets, the contract has to carry a per-room offset and " +
                        "InteriorKit.InteriorFacingFor has to take a key.");
            }

            if (firstKey == null)
                throw new InvalidOperationException(
                    "[interiors] No baked room has an exterior in VillageBuildingKit, so the exterior↔" +
                    "interior facing offset cannot be MEASURED. Refusing to write a contract with an " +
                    "assumed offset: at the wrong offset every doorway lands against the back wall, the " +
                    "player walks in the front door and appears at the back of the room, and it reads " +
                    "as an art bug. Give a room the same key as the village building it belongs inside.");

            return new InteriorRigAzimuthProbe.Registration(
                first.FacingOffset, first.ExteriorGable, first.InteriorGable,
                first.WidthMetres, first.LengthMetres, first.DoorHeightGapPx,
                report.ToString());
        }

        /// <summary>
        /// Headless entry point for <c>-executeMethod</c>. Exits non-zero if any build fails, so a batch
        /// bake fails loudly instead of leaving a folder and a contract that disagree.
        /// </summary>
        public static void BakeAllFromCommandLine()
        {
            try
            {
                string report = BakeAll(out int failed);
                AssetDatabase.Refresh();
                Debug.Log($"[interiors] (batch) bake report:\n{report}");

                if (failed > 0)
                {
                    Debug.LogError($"[interiors] {failed} build(s) failed.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[interiors] batch bake threw: {e}");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
