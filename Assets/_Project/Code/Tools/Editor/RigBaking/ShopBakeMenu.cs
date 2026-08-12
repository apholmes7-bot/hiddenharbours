#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Bakes the SHOP KIT — a shell and its floor plans for every build in <see cref="ShopKit.ShopSet"/> —
    /// and writes the contract its consumers read. The sibling of <see cref="InteriorBakeMenu"/> and
    /// <see cref="VillageBuildingBakeMenu"/>, deliberately shaped the same way.
    ///
    /// <para><b>What the contract carries that the others do not: <c>shellFacingOffset</c>, MEASURED.</b>
    /// The house family needs a facing correction because its room rig puts the door on the opposite
    /// gable from its shell. This kit does not — but "does not" is a measurement, not an assumption, so
    /// <see cref="ShopRegistrationProbe"/> runs it here and the full working goes into the contract
    /// beside the number. A number nobody can check is a declaration again.</para>
    ///
    /// <para><b>A failed build is left OUT of the contract</b> rather than entered with guessed geometry:
    /// a contract that claims a sheet which is not there makes the slicer cut a stale sheet against a
    /// fresh cell.</para>
    /// </summary>
    public static class ShopBakeMenu
    {
        public static string ShopsFolder => ShopKit.ShopsRoot.TrimEnd('/');

        // Priority 61 continues the Art menu's unbroken bake/slice run (interiors sit at 60). Unity draws
        // a separator at any priority gap over 10, so a "sensible" 80 renders this off in its own island
        // and reads as missing.
        [MenuItem("Hidden Harbours/Art/Bake Shops (shells + interiors)", priority = 61)]
        public static void BakeAllMenu()
        {
            string report = BakeAll(out int failed);
            AssetDatabase.Refresh();

            if (failed > 0)
                Debug.LogError($"[shops] {failed} build(s) FAILED — nothing was written for them and the " +
                               $"contract covers only what succeeded.\n{report}");
            else
                Debug.Log($"[shops] Bake complete.\n{report}\n" +
                          "Next: Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Slice Shop Sheets, " +
                          "then rebuild St Peters.");
        }

        /// <summary>
        /// Bake every shell and level in the set and write the contract. Returns a human-readable report
        /// and how many builds failed.
        /// </summary>
        public static string BakeAll(out int failed)
        {
            failed = 0;
            var log = new StringBuilder();
            var shells = new List<ShopKit.Entry>();
            var levels = new List<ShopKit.Entry>();

            int ppu = 0;
            string convention = "";
            long png = 0, rgba = 0, uncropped = 0;
            int widest = 0, tallest = 0;

            foreach (var build in ShopKit.ShopSet)
            {
                // ---- the shell ---------------------------------------------------------------------
                log.AppendLine($"  {build.Key} ({build.Label}):");
                try
                {
                    AssertPresetSize(build);

                    var req = BuildingBakeRequest.FromPreset(
                        "shopfront", build.Preset, RigCatalog.Get("shopfront").GlobalName,
                        ShopsFolder, ShopKit.ShellStemFor(build.Key),
                        ShopKit.Facings, ShopKit.ImportSizeCap);

                    BuildingBakeResult r = BuildingRigBaker.Bake(req);
                    shells.Add(ShellEntry(build, r));
                    ppu = r.PixelsPerMetre;
                    convention = r.MeasuredConvention.ToString();
                    png += r.PngBytes; rgba += r.RuntimeBytesRgba32; uncropped += r.UncroppedBytesRgba32;
                    widest = Mathf.Max(widest, r.SheetWidth); tallest = Mathf.Max(tallest, r.SheetHeight);
                    log.AppendLine($"    ✓ shell: {r.CellWidth}×{r.CellHeight} cell → {r.Columns}×{r.Rows} " +
                                   $"sheet {r.SheetWidth}×{r.SheetHeight}, pivot ({r.PivotX:F1},{r.PivotY:F1}), " +
                                   $"{r.FootprintWidthMeters:F2}×{r.FootprintLengthMeters:F2} m, " +
                                   $"{r.PngBytes / 1024.0:F0} KiB");
                }
                catch (Exception e)
                {
                    failed++;
                    log.AppendLine($"    ✗ shell: {e.Message}");
                    Debug.LogError($"[shops] shell '{build.Key}' FAILED:\n{e}");
                }

                // ---- its levels --------------------------------------------------------------------
                foreach (string level in build.Levels)
                {
                    try
                    {
                        AssertLevelExists(build, level);

                        ShopLevelBakeResult r = ShopLevelBaker.Bake(
                            build.Trade, level, build.Size,
                            ShopsFolder, ShopKit.LevelStemFor(build.Key, level),
                            ShopKit.Facings, ShopKit.ImportSizeCap);

                        levels.Add(LevelEntry(build, level, r));
                        if (ppu == 0) ppu = r.PixelsPerMetre;
                        png += r.PngBytes; rgba += r.RuntimeBytesRgba32; uncropped += r.UncroppedBytesRgba32;
                        widest = Mathf.Max(widest, r.SheetWidth); tallest = Mathf.Max(tallest, r.SheetHeight);
                        log.AppendLine("  " + ShopLevelBaker.Describe(build.Key, level, r).TrimStart());
                    }
                    catch (Exception e)
                    {
                        failed++;
                        log.AppendLine($"    ✗ {level}: {e.Message}");
                        Debug.LogError($"[shops] level '{build.Key}/{level}' FAILED:\n{e}");
                    }
                }
            }

            if (shells.Count > 0 && levels.Count > 0)
            {
                // 🔴 THE CHECK IN THE FRAME THAT SHIPS. Everything ShopRegistrationProbe measures is in
                // the RIGS' dir units; the sheets are in CELL units, and the two bakers each apply their
                // own cell → dir correction. On this kit's first bake they applied different ones and
                // every rig-frame check passed. This compares the anchors the two bakes actually WROTE.
                AssertBakedCellsRegister(shells, levels, log);

                string path = WriteContract(shells, levels, ppu, convention, out int offset);
                log.AppendLine();
                log.AppendLine($"  contract: {path} — {shells.Count} shell(s), {levels.Count} level(s), " +
                               $"level facing = shell facing + {offset} (MEASURED).");
            }
            else
            {
                log.AppendLine();
                log.AppendLine("  NO contract written — a contract needs at least one shell AND one level, " +
                               "because the facing offset every consumer reads is measured between them.");
            }

            log.AppendLine();
            log.AppendLine($"  {shells.Count + levels.Count} baked, {failed} failed.");
            if (uncropped > 0)
                log.AppendLine(
                    $"  budget: biggest sheet {widest}×{tallest} px against the {ShopKit.ImportSizeCap} px " +
                    $"import cap; {rgba / 1024.0 / 1024.0:F2} MiB RGBA32 cropped vs " +
                    $"{uncropped / 1024.0 / 1024.0:F0} MiB uncropped ({1.0 - rgba / (double)uncropped:P0} " +
                    $"saved); {png / 1024.0 / 1024.0:F2} MiB of PNG on disk.");

            return log.ToString();
        }

        // =================================================================================
        //  the guards
        // =================================================================================

        /// <summary>
        /// The build's <see cref="ShopKit.Build.Size"/> must equal the size its shopfront PRESET carries.
        ///
        /// <para><b>Why this can go wrong silently.</b> The shell is baked from the preset (so it takes
        /// the preset's size); every LEVEL is baked from <c>Build.Size</c>. If the two drift, both sheets
        /// bake perfectly and the shop stands — with an interior a few tenths of a metre out of its own
        /// walls. The registration probe would catch it too, but this catches it with the better message.</para>
        /// </summary>
        static void AssertPresetSize(ShopKit.Build build)
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("shopfront");
            RigCatalog.InstallModule(host, entry);

            string g = entry.GlobalName;
            if (!host.EvaluateBool($"!!{g}.PRESETS['{build.Preset}']"))
                throw new InvalidOperationException(
                    $"'{build.Preset}' is not a preset of {g}. Known: " +
                    host.EvaluateString($"Object.keys({g}.PRESETS).join(', ')") + ". A preset name that " +
                    "does not resolve bakes the rig's DEFAULT build with no error at all.");

            string trade = host.EvaluateString($"String({g}.PRESETS['{build.Preset}'].type)");
            if (!string.Equals(trade, build.Trade, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"build '{build.Key}' names trade '{build.Trade}' but preset '{build.Preset}' is a " +
                    $"'{trade}'. The level bake uses the trade and the shell bake uses the preset, so " +
                    "the two would be different buildings.");

            double presetSize = host.EvaluateNumber($"{g}.PRESETS['{build.Preset}'].size");
            if (Math.Abs(presetSize - build.Size) > 1e-9)
                throw new InvalidOperationException(
                    $"build '{build.Key}' carries size {build.Size} but preset '{build.Preset}' carries " +
                    $"{presetSize}. The shell bakes from the preset and the levels from the build, so a " +
                    "drift here puts the interior a few tenths of a metre out of its own walls — both " +
                    "sheets bake cleanly and the shop stands, which is why this is checked and not left " +
                    "to the eye.");
        }

        /// <summary>
        /// A level the trade does not have must not be baked.
        ///
        /// <para><b>MEASURED, because the rig will not tell you.</b> Asking <c>shopBuildingRig</c> for the
        /// upper level of a fish market returns the GROUND rooms under the name <c>upper</c> — not an
        /// error, not an empty plan. Baked, that is a duplicate ground floor stacked half a metre above
        /// the real one. <see cref="ShopKit.HasUpper"/> is the kit's plan table and this is where it
        /// bites.</para>
        /// </summary>
        static void AssertLevelExists(ShopKit.Build build, string level)
        {
            if (string.Equals(level, ShopKit.GroundLevel, StringComparison.Ordinal)) return;

            if (string.Equals(level, ShopKit.UpperLevel, StringComparison.Ordinal))
            {
                if (ShopKit.HasUpper(build.Trade)) return;
                throw new InvalidOperationException(
                    $"trade '{build.Trade}' has no upper storey, and the rig does not say so — asking it " +
                    "for one returns the GROUND rooms under the name 'upper', which bakes a second copy " +
                    "of the ground floor half a metre in the air. Drop 'upper' from this build's Levels.");
            }

            throw new InvalidOperationException(
                $"'{level}' is not a level. The rig knows '{ShopKit.GroundLevel}' and " +
                $"'{ShopKit.UpperLevel}'.");
        }

        /// <summary>
        /// Screen-px slack when matching a baked level cell's door anchor to its shell's. Both are the
        /// same projected ground point written by two different bakers, so this is rounding margin plus
        /// the one pixel of pad each crop carries.
        /// </summary>
        public const double BakedRegistrationTolerancePx = 1.5;

        /// <summary>
        /// Every level's baked door anchor must sit at the same screen-x as its shell's, CELL BY CELL.
        ///
        /// <para><b>Why this is not a duplicate of <see cref="ShopRegistrationProbe.Measure"/>.</b> That
        /// probe reads the two RIGS through <c>anchors(dir, …)</c> and answers a question about rig dirs.
        /// A baker sits between the rig and the sheet and maps cell → dir through
        /// <see cref="RigBaker.DirForCell"/>, which inverts the order for a counter-clockwise rig. Two
        /// rigs can agree perfectly in dir units and still produce mirrored SHEETS if their bakers
        /// disagree — which is precisely what this kit shipped on its first bake (shells corrected,
        /// levels not), with every rig-frame check green.</para>
        ///
        /// <para>Screen-x only. The y gap is real and constant by design — the shell's door anchor sits up
        /// its wall and the plan's on the floor — and the probe already checks that it is constant.
        /// A mirror shows up in x as a sign flip at the six non-axial cells, which is unmissable here and
        /// invisible everywhere else.</para>
        /// </summary>
        static void AssertBakedCellsRegister(List<ShopKit.Entry> shells, List<ShopKit.Entry> levels,
                                             StringBuilder log)
        {
            foreach (var level in levels)
            {
                ShopKit.Entry shell = shells.Find(s => string.Equals(s.key, level.key,
                                                                     StringComparison.Ordinal));
                if (shell?.doorX == null || level.doorX == null) continue;
                if (shell.doorX.Length != level.doorX.Length) continue;

                double worst = 0;
                int worstCell = -1;
                for (int cell = 0; cell < shell.doorX.Length; cell++)
                {
                    double sx = shell.doorX[cell] - shell.pivotX;
                    double lx = level.doorX[cell] - level.pivotX;
                    double d = Math.Abs(sx - lx);
                    if (d > worst) { worst = d; worstCell = cell; }
                }

                if (worst > BakedRegistrationTolerancePx)
                {
                    var detail = new StringBuilder();
                    for (int cell = 0; cell < shell.doorX.Length; cell++)
                        detail.AppendLine(
                            $"      cell {cell}: shell x={shell.doorX[cell] - shell.pivotX:+0.0;-0.0}   " +
                            $"level x={level.doorX[cell] - level.pivotX:+0.0;-0.0}");

                    throw new InvalidOperationException(
                        $"[shops] BAKED REGISTRATION FAILED for '{level.key}/{level.level}': the level's " +
                        $"door anchor is {worst:F1} px from its shell's at cell {worstCell}, measured from " +
                        "each sheet's own pivot.\n" + detail +
                        "\nIf the x offsets are equal and opposite, the two sheets are MIRRORED — one " +
                        "baker applied the cell → dir azimuth correction and the other did not. Cells 0 " +
                        "and 4 will look perfect and the other six will be a half-turn's worth of wrong. " +
                        "Refusing to write a contract for it.");
                }

                log.AppendLine($"    ✓ {level.key}: shell and {level.level} agree cell-for-cell " +
                               $"(worst door-x disagreement {worst:F2} px).");
            }
        }

        // =================================================================================
        //  the contract
        // =================================================================================

        static ShopKit.Entry ShellEntry(ShopKit.Build build, BuildingBakeResult r)
        {
            var rig = RigCatalog.Get("shopfront");
            var e = new ShopKit.Entry
            {
                key = build.Key,
                label = build.Label,
                level = "",
                trade = build.Trade,
                preset = build.Preset,
                size = (float)build.Size,
                rig = "shopfront",
                rigScript = rig.ScriptPath,
                rigGlobal = rig.GlobalName,
                sheet = Path.GetFileName(r.AssetPath),
                optionsJs = $"Object.assign({{}},{rig.GlobalName}.PRESETS['{build.Preset}'])",
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
                levelZ = 0f,
                convention = r.MeasuredConvention.ToString(),
                doorX = ToFloats(r.DoorX),
                doorY = ToFloats(r.DoorY),
                backDoorX = Array.Empty<float>(),
                backDoorY = Array.Empty<float>(),
                pngBytes = r.PngBytes,
                runtimeBytesRgba32 = r.RuntimeBytesRgba32,
                cropSaving = (float)r.CropSaving,
            };
            ApplyUnityPivot(e);
            return e;
        }

        static ShopKit.Entry LevelEntry(ShopKit.Build build, string level, ShopLevelBakeResult r)
        {
            var rig = RigCatalog.Get("shopBuilding");
            var e = new ShopKit.Entry
            {
                key = build.Key,
                label = build.Label,
                level = level,
                trade = build.Trade,
                preset = build.Preset,
                size = (float)build.Size,
                rig = "shopBuilding",
                rigScript = rig.ScriptPath,
                rigGlobal = rig.GlobalName,
                sheet = Path.GetFileName(r.AssetPath),
                optionsJs = r.OptionsJs,
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
                levelZ = (float)r.LevelZ,
                convention = RigCatalog.Get("shopBuilding").DeclaredConvention.ToString(),
                doorX = ToFloats(r.DoorX),
                doorY = ToFloats(r.DoorY),
                backDoorX = ToFloats(r.BackDoorX),
                backDoorY = ToFloats(r.BackDoorY),
                pngBytes = r.PngBytes,
                runtimeBytesRgba32 = r.RuntimeBytesRgba32,
                cropSaving = (float)r.CropSaving,
            };
            ApplyUnityPivot(e);
            return e;
        }

        // The Unity pivot is DERIVED from the cropped pivot rather than stored independently, so the two
        // can never disagree — ShopKit.NormalizedPivot is the single definition and a test asserts the
        // written values equal it.
        static void ApplyUnityPivot(ShopKit.Entry e)
        {
            Vector2 unity = ShopKit.NormalizedPivot(e);
            e.unityPivotX = unity.x;
            e.unityPivotY = unity.y;
        }

        static float[] ToFloats(double[] src)
        {
            if (src == null) return Array.Empty<float>();
            var dst = new float[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = (float)src[i];
            return dst;
        }

        /// <summary>
        /// Write <see cref="ShopKit.ContractPath"/>, MEASURING the shell↔level facing offset on the way
        /// through. Throws rather than defaulting: an unmeasured offset written as 0 looks exactly like a
        /// measured one, and the difference only shows up as a player walking in the front door and
        /// coming out behind the counter.
        /// </summary>
        static string WriteContract(List<ShopKit.Entry> shells, List<ShopKit.Entry> levels,
                                    int ppu, string convention, out int offset)
        {
            ShopRegistrationProbe.Registration reg = MeasureRegistration(levels);
            offset = reg.FacingOffset;

            var contract = new ShopKit.Contract
            {
                note = "GENERATED by Hidden Harbours ▸ Art ▸ Bake Shops. Do not hand-edit: every number " +
                       "here is read from the rigs at bake time (ADR 0021 §4), so an edit is silently " +
                       "undone by the next bake. Re-bake instead.",
                writtenBy = nameof(ShopBakeMenu),
                ppu = ppu,
                facings = ShopKit.Facings,
                shellFacingOffset = offset,
                importSizeCap = ShopKit.ImportSizeCap,
                conventionNote =
                    "MEASURED at bake time. All three shop rigs share one camBasis/projVert — checked per " +
                    "facing by ShopRegistrationProbe.ProjectionsAgree, not read off a header — and all " +
                    "three put the street door on the +Y gable. ⚠️ That last part is why this kit's " +
                    "offset is NOT the house family's 4: interiorIsoRig puts its door on −Y, so a cottage " +
                    "room stands a half-turn from its shell. A shop room does not. Do not carry a number " +
                    "between the two kits.",
                pivotNote =
                    "pivotX/pivotY are the GROUND CENTRE of the footprint, in cropped-cell px from the " +
                    "cell's TOP-LEFT. Unity wants (pivotX/cellW, (cellH−pivotY)/cellH), which is NOT " +
                    "bottom-centre: under the ¾ camera the whole NEAR HALF of the footprint projects " +
                    "below that centre, so a bottom-centre pivot sinks the building by that much, " +
                    "silently. ⚠️ Buildings pivot at ground CENTRE — characters and trees do not; do not " +
                    "carry a pivot convention across families either.",
                registrationReport = reg.Report,
                shells = shells.ToArray(),
                levels = levels.ToArray(),
            };

            string abs = Path.Combine(RigCatalog.RepoRoot, ShopKit.ContractPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            File.WriteAllText(abs, JsonUtility.ToJson(contract, prettyPrint: true));
            return ShopKit.ContractPath;
        }

        /// <summary>
        /// Measure the offset against the first level that baked. One host holds the whole kit, so the
        /// shell and the plan are compared inside the same engine at the same moment.
        /// </summary>
        static ShopRegistrationProbe.Registration MeasureRegistration(List<ShopKit.Entry> levels)
        {
            foreach (var level in levels)
            {
                var build = ShopKit.FindBuild(level.key);
                if (build == null) continue;

                using IRigScriptHost host = RigScriptHostFactory.Create();
                RigCatalog.InstallModule(host, RigCatalog.Get("shopBuilding"));   // installs all three

                string shellOpts =
                    $"Object.assign({{}},{RigCatalog.Get("shopfront").GlobalName}." +
                    $"PRESETS['{build.Value.Preset}'])";
                string levelOpts =
                    ShopLevelBaker.OptionsLiteral(build.Value.Trade, level.level, build.Value.Size);

                return ShopRegistrationProbe.Measure(
                    host,
                    RigCatalog.Get("shopfront").GlobalName, shellOpts,
                    RigCatalog.Get("shopBuilding").GlobalName, levelOpts,
                    ShopKit.Facings);
            }

            throw new InvalidOperationException(
                "[shops] No baked level belongs to a build in ShopKit.ShopSet, so the shell↔level facing " +
                "offset cannot be MEASURED. Refusing to write a contract with an assumed offset.");
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
                Debug.Log($"[shops] (batch) bake report:\n{report}");

                if (failed > 0)
                {
                    Debug.LogError($"[shops] {failed} build(s) failed.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[shops] batch bake threw: {e}");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
