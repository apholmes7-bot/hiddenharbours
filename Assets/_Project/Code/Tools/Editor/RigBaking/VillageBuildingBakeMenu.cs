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
    /// Bakes the M1 VILLAGE BUILDING KIT — <see cref="VillageBuildingKit.M1Set"/> — and writes the
    /// contract its consumers read.
    ///
    /// <para>Menu: <c>Hidden Harbours ▸ Art ▸ Bake Village Buildings (M1 set)</c>. Re-runnable; each
    /// build overwrites its own sheet and sidecar and touches nothing else.</para>
    ///
    /// <para><b>Why this is a second menu and not a flag on <see cref="BuildingBakeMenu"/>.</b> That
    /// one bakes all twelve rig presets, deliberately including the cannery, because its job is to
    /// prove the crop-packing baker survives the worst case — and its sheets are not committed. This one
    /// bakes the five buildings M1 actually ships, into their own folder, and writes a contract. Two
    /// jobs, two commands, and no way for one's output to look like the other's orphan.</para>
    ///
    /// <para><b>What the contract is for.</b> Until now nothing consumed a building bake: the pivot is
    /// per-build data (the union crop's cell depends on the build), so a slicer had nothing to read and
    /// a placement path had no pivot. Everything written below comes from
    /// <see cref="BuildingBakeResult"/> — which read it from the rig — so re-baking a build at a
    /// different size updates its consumers automatically instead of silently disagreeing with them.</para>
    /// </summary>
    public static class VillageBuildingBakeMenu
    {
        /// <summary>Where the kit's sheets land — the kit's own folder, not the loose greybox
        /// <c>Buildings/</c> beside it. Trailing slash trimmed: the baker joins paths itself.</summary>
        public static string OutputFolder => VillageBuildingKit.BuildingsRoot.TrimEnd('/');

        // Priority 59 continues the unbroken 38–58 bake/slice run in the Art menu. Unity draws a
        // separator at any priority gap over 10, so a "sensible" 70 would render the command off in its
        // own island and read as missing — the exact trap that made "Bake Buildings" look absent at 23.
        [MenuItem("Hidden Harbours/Art/Bake Village Buildings (M1 set)", priority = 59)]
        public static void BakeAllMenu()
        {
            var report = BakeAll(out int failed);
            AssetDatabase.Refresh();

            if (failed > 0)
                Debug.LogError($"[village-buildings] {failed} build(s) FAILED — nothing was written for " +
                               $"them and the contract covers only what succeeded.\n{report}");
            else
                Debug.Log($"[village-buildings] Bake complete.\n{report}\n" +
                          "Next: Hidden Harbours ▸ Art ▸ Slice Village Building Sheets, then " +
                          "▸ Build Village Building Prefabs.");
        }

        /// <summary>
        /// Bake every build in the M1 set and write the contract. Returns a human-readable report and
        /// reports how many builds failed. A failed build is left out of the contract rather than
        /// entered with guessed geometry — a contract that claims a sheet which is not there is worse
        /// than a short one, because the slicer would then slice a stale sheet against a fresh cell.
        /// </summary>
        public static string BakeAll(out int failed)
        {
            failed = 0;
            var log = new StringBuilder();
            var entries = new List<VillageBuildingKit.Entry>();

            long croppedBytes = 0, uncroppedBytes = 0, pngBytes = 0;
            int ppu = 0;

            foreach (var build in VillageBuildingKit.M1Set)
            {
                try
                {
                    BuildingBakeResult r = Bake(build);
                    entries.Add(ToEntry(build, r));

                    croppedBytes += r.RuntimeBytesRgba32;
                    uncroppedBytes += r.UncroppedBytesRgba32;
                    pngBytes += r.PngBytes;
                    ppu = r.PixelsPerMetre;

                    log.AppendLine(Describe(build, r));
                }
                catch (Exception e)
                {
                    failed++;
                    log.AppendLine($"  ✗ {build.Key}: {e.Message}");
                    Debug.LogError($"[village-buildings] {build.Key} FAILED:\n{e}");
                }
            }

            if (entries.Count > 0)
            {
                string contractPath = WriteContract(entries, ppu);
                log.AppendLine();
                log.AppendLine($"  contract: {contractPath} ({entries.Count} building(s))");
            }

            log.AppendLine();
            log.AppendLine($"  {entries.Count} baked, {failed} failed.");
            if (uncroppedBytes > 0)
                log.AppendLine(
                    $"  texture memory: {croppedBytes / 1024.0 / 1024.0:F2} MiB RGBA32 cropped vs " +
                    $"{uncroppedBytes / 1024.0 / 1024.0:F0} MiB uncropped " +
                    $"({1.0 - croppedBytes / (double)uncroppedBytes:P0} saved); " +
                    $"{pngBytes / 1024.0 / 1024.0:F2} MiB of PNG on disk.");

            return log.ToString();
        }

        /// <summary>Bake ONE build. Exposed so a test or a one-off can bake a single building.</summary>
        public static BuildingBakeResult Bake(VillageBuildingKit.Build build)
        {
            string stem = VillageBuildingKit.StemFor(build.Key);
            var request = RequestFor(build, OutputFolder, stem);
            return BuildingRigBaker.Bake(request);
        }

        /// <summary>
        /// The bake request for a build — the ONE place the kit's preset/dialled split turns into an
        /// options expression.
        ///
        /// <para>Both paths opt into the differs-from-the-rig-default tripwire. For a preset that is
        /// already the default; for a dialled build it is the point: these rigs read options as
        /// <c>opts[k] != null ? opts[k] : fallback</c>, so a build whose every key was misspelled
        /// renders the default with no error at all.</para>
        ///
        /// <para>Pure and static (no JS engine) so a test can assert what a build asks the rig to
        /// draw without baking it.</para>
        /// </summary>
        public static BuildingBakeRequest RequestFor(VillageBuildingKit.Build build,
                                                     string outputFolder, string baseName)
        {
            // ⚠️ The grid is chosen against the KIT's import cap (2048), not the baker's hard 4096. The
            // first real bake of this set came out 2800–3876 px wide: legal for the baker, and every one
            // of them would have imported SILENTLY DOWNSCALED with the sprite count still correct. The
            // measurement is what caught it — the cap this kit imports at has to be the cap the pack is
            // solved for, or the two agree only by luck.
            if (build.IsPreset)
                return BuildingBakeRequest.FromPreset(
                    build.RigKey, build.Preset, RigCatalog.Get(build.RigKey).GlobalName,
                    outputFolder, baseName, VillageBuildingKit.Facings,
                    maxSheetDimension: VillageBuildingKit.ImportSizeCap);

            return BuildingBakeRequest.FromOptions(
                build.RigKey, OptionsLiteralFor(build), build.Key,
                outputFolder, baseName, VillageBuildingKit.Facings,
                requireDistinctFromDefault: true,
                maxSheetDimension: VillageBuildingKit.ImportSizeCap);
        }

        /// <summary>The dialled options as the JS literal <c>render()</c> takes, through the same
        /// serialiser the Building Studio uses (invariant culture, so a comma decimal separator can
        /// never split an option in two).</summary>
        public static string OptionsLiteralFor(VillageBuildingKit.Build build)
        {
            if (build.IsPreset)
                throw new ArgumentException(
                    $"'{build.Key}' is a preset build — its options come from the rig's PRESETS table, " +
                    "spread by BuildingBakeRequest.FromPreset.", nameof(build));

            var dialled = new List<KeyValuePair<string, object>>();
            foreach (var kv in build.Dialled) dialled.Add(kv);
            return BuildingAxes.ToOptionsLiteral(dialled);
        }

        // =================================================================================
        // the contract
        // =================================================================================

        /// <summary>
        /// One contract entry, built entirely from the bake result. Nothing here is typed by hand — the
        /// cell, the crop, the pivot, the footprint metres and the measured convention all came from the
        /// rig via <see cref="BuildingRigBaker"/>.
        /// </summary>
        static VillageBuildingKit.Entry ToEntry(VillageBuildingKit.Build build, BuildingBakeResult r)
        {
            var rig = RigCatalog.Get(build.RigKey);
            var entry = new VillageBuildingKit.Entry
            {
                key = build.Key,
                label = build.Label,
                rig = build.RigKey,
                rigScript = rig.ScriptPath,
                rigGlobal = rig.GlobalName,
                sheet = Path.GetFileName(r.AssetPath),
                preset = build.IsPreset ? build.Preset : "",
                fromPreset = build.IsPreset,
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
                footprintWidthMetres = (float)r.FootprintWidthMeters,
                footprintLengthMetres = (float)r.FootprintLengthMeters,
                convention = r.MeasuredConvention.ToString(),
                pngBytes = r.PngBytes,
                runtimeBytesRgba32 = r.RuntimeBytesRgba32,
                cropSaving = (float)r.CropSaving,
            };

            entry.optionsJs = build.IsPreset
                ? $"Object.assign({{}},{rig.GlobalName}.PRESETS['{build.Preset}'])"
                : OptionsLiteralFor(build);

            // The Unity pivot is DERIVED from the cropped pivot rather than stored independently, so the
            // two can never disagree — VillageBuildingKit.NormalizedPivot is the single definition and a
            // test asserts the written values equal it.
            Vector2 unity = VillageBuildingKit.NormalizedPivot(entry);
            entry.unityPivotX = unity.x;
            entry.unityPivotY = unity.y;

            entry.doorX = ToFloats(r.DoorX);
            entry.doorY = ToFloats(r.DoorY);
            return entry;
        }

        static float[] ToFloats(double[] src)
        {
            if (src == null) return Array.Empty<float>();
            var dst = new float[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = (float)src[i];
            return dst;
        }

        /// <summary>
        /// Write <see cref="VillageBuildingKit.ContractPath"/> with
        /// <see cref="JsonUtility.ToJson(object,bool)"/> — the same serializer
        /// <see cref="VillageBuildingKit.Load"/> parses with, so the bake and its consumers cannot drift
        /// apart. Returns the project-relative path written.
        /// </summary>
        static string WriteContract(List<VillageBuildingKit.Entry> entries, int ppu)
        {
            var contract = new VillageBuildingKit.Contract
            {
                note = "GENERATED by Hidden Harbours ▸ Art ▸ Bake Village Buildings. Do not hand-edit: " +
                       "every number here is read from the rig at bake time (ADR 0021 §4), so an edit " +
                       "is silently undone by the next bake. Re-bake instead.",
                writtenBy = nameof(VillageBuildingBakeMenu),
                ppu = ppu,
                facings = VillageBuildingKit.Facings,
                conventionNote = "MEASURED at bake time by BuildingRigAzimuthProbe (from the door " +
                                 "anchor — a building has no bow taper). The correction is already " +
                                 "applied to the sheet, so cell i genuinely depicts +45*i.",
                pivotNote = "pivotX/pivotY are the ground CENTRE - the middle of the footprint - in " +
                            "cropped-cell px from the cell's TOP-LEFT. Unity wants " +
                            "(pivotX/cellW, (cellH-pivotY)/cellH), which is NOT bottom-centre: under " +
                            "the 3/4 camera the whole NEAR HALF of the footprint projects below that " +
                            "centre, so the cell carries 104-209 px (3.3-6.5 m) of art beneath the " +
                            "pivot. Taking bottom-centre stands the building that far into the ground, " +
                            "silently.",
                buildings = entries.ToArray(),
            };

            string abs = Path.Combine(RigCatalog.RepoRoot, VillageBuildingKit.ContractPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            File.WriteAllText(abs, JsonUtility.ToJson(contract, prettyPrint: true));
            return VillageBuildingKit.ContractPath;
        }

        static string Describe(VillageBuildingKit.Build build, BuildingBakeResult r) =>
            $"  ✓ {build.Key,-15} {r.CellWidth,4}×{r.CellHeight,-4} cell ({r.Columns}×{r.Rows}) → " +
            $"{r.SheetWidth}×{r.SheetHeight}, {r.PngBytes / 1024.0:F0} KB png, " +
            $"crop saved {r.CropSaving:P0}, pivot ({r.PivotX:F0},{r.PivotY:F0}), " +
            $"{r.FootprintWidthMeters:F1}×{r.FootprintLengthMeters:F1} m, {r.MeasuredConvention}";

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
                Debug.Log($"[village-buildings] (batch) bake report:\n{report}");

                if (failed > 0)
                {
                    Debug.LogError($"[village-buildings] {failed} build(s) failed.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[village-buildings] batch bake threw: {e}");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
