using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.Art.Editor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Bakes the boat-interior sheets — for the hulls the intake cleared, and for no others.</b>
    ///
    /// <para>One sheet set per hull: columns are FACINGS, rows are cells wrapped from a LEVEL-MAJOR
    /// order, every cell drawn into the FULL hull cell at the hull pivot. That last clause is not a
    /// packing choice, it is the product: the kit's <c>cell.note</c> says a sheet "composites under
    /// the exterior 1:1", and it does so because the interior renders into the exterior's own cell on
    /// the exterior's own camera basis. Cropping the cells would save between 39% (the packet) and 82%
    /// (the trawlers) of the pixels and would make every consumer carry an offset — so the crop is a
    /// decision for whoever builds the runtime, not one to take silently here.</para>
    ///
    /// <para><b>The gates, in order, each a refusal rather than a warning:</b></para>
    /// <list type="number">
    /// <item><b>S0.</b> The hull's row in the intake ledger must say CLEAN. Absent means UNKNOWN means
    /// refused — the same gate <see cref="BoatInteriorDefBuilder"/> stands behind, for the same reason:
    /// art cut from a boat that has since changed shape looks entirely plausible.</item>
    /// <item><b>The sidecar</b> must read clean, which includes its <c>scale_px_per_m</c> — the tanker
    /// is 16 where the fleet is 32 and a default here would bake a hull at half scale (ADR 0038 P2).</item>
    /// <item><b>The hull</b> must resolve to exactly one committed gameplay sidecar, whose <c>variant</c>
    /// node supplies the triple the variants rig needs as an OBJECT.</item>
    /// <item><b>Registration</b> must be MEASURED, not assumed — see
    /// <see cref="BoatInteriorRegistrationProbe"/>.</item>
    /// <item><b>Every page</b> must fit <see cref="BoatInteriorKit.ImportSizeCap"/>, which is also the
    /// cap the slicer imports at.</item>
    /// </list>
    ///
    /// <para>It writes PNGs and a contract. It does not slice, and it does not touch a
    /// <c>BoatInteriorDef</c> — those belong to <c>BoatInteriorSheetSlicer</c> and
    /// <see cref="BoatInteriorDefBuilder"/>, which already own and test those rules.</para>
    /// </summary>
    public static class BoatInteriorSheetBaker
    {
        const string GameplayFolder = "docs/art/rigs/gameplay";
        const string RigFolder = "docs/art/rigs";

        public sealed class SheetResult
        {
            public BoatInteriorKit.SheetEntry Entry;
            public BoatInteriorRegistrationProbe.Report Probe;
            public long PngBytes;
            public double RenderMilliseconds;

            /// <summary>Pixels this hull's pages occupy — the number the performance budget pays at
            /// RGBA32, which is what the import lock keeps them at (CLAUDE.md rule 7).</summary>
            public long Pixels =>
                Entry?.pages?.Sum(p => (long)p.sheetW * p.sheetH) ?? 0L;

            public double RuntimeMegabytes => Pixels * 4.0 / (1024 * 1024);
        }

        public sealed class Refusal
        {
            public string Sidecar = "", HullStem = "", Reason = "", UpstreamAsk = "", Verdict = "";

            /// <summary>
            /// True when the S0 ledger itself refused this hull — the three the intake adjudicated
            /// (two sport fishers on an unstamped pin, the cape on a forked rig).
            ///
            /// <para><b>This is the flag that makes the headless exit code mean something.</b>
            /// <c>BoatInteriorDefBuilder.BuildAllCli</c> exits 1 on ANY refusal, so its exit code is
            /// permanently 1 while the ledger holds those three and a reader has to parse the log
            /// instead. A ledger refusal is the expected steady state, not a failure of this bake, so
            /// it does not colour the exit code here; anything else does.</para>
            /// </summary>
            public bool IsLedgerRefusal;
        }

        public sealed class BakeReport
        {
            public string EngineName = "";
            public string InteriorRigSha = "";
            public readonly List<SheetResult> Sheets = new List<SheetResult>();
            public readonly List<Refusal> Refusals = new List<Refusal>();
            public double TotalMilliseconds;

            public long TotalPixels => Sheets.Sum(s => s.Pixels);
            public long TotalPngBytes => Sheets.Sum(s => s.PngBytes);
            public double RuntimeMegabytes => TotalPixels * 4.0 / (1024 * 1024);
            public int PageCount => Sheets.Sum(s => s.Entry?.pages?.Length ?? 0);

            /// <summary>Hulls the S0 ledger refused — expected, and not a failure of this bake.</summary>
            public int LedgerRefusals => Refusals.Count(r => r.IsLedgerRefusal);

            /// <summary>Hulls the ledger CLEARED that still did not bake. Any of these is a real
            /// failure and is what the headless exit code reports.</summary>
            public int ClearedFailures => Refusals.Count(r => !r.IsLedgerRefusal);
        }

        // ---- the run ----------------------------------------------------------------------------

        public static BakeReport BakeAll(StringBuilder log)
        {
            var total = Stopwatch.StartNew();
            var report = new BakeReport();
            log ??= new StringBuilder();

            string repo = RigCatalog.RepoRoot;
            string kit = Path.Combine(repo, BoatInteriorKit.KitFolder);
            if (!Directory.Exists(kit))
                throw new DirectoryNotFoundException(
                    $"the interiors kit is not at {BoatInteriorKit.KitFolder}. Nothing to bake.");

            string interiorRigPath = Path.Combine(kit, BoatInteriorKit.InteriorRigFileName);
            if (!File.Exists(interiorRigPath))
                throw new FileNotFoundException(
                    $"{BoatInteriorKit.InteriorRigFileName} is missing from the kit — every sidecar " +
                    "would refuse its renderer pin.", interiorRigPath);

            byte[] interiorRigBytes = File.ReadAllBytes(interiorRigPath);
            // LF-normalised on purpose: the raw working-tree bytes are CRLF on a Windows checkout and
            // LF on a Linux one, so hashing them recorded a different number for the SAME rig depending
            // on where the bake ran. See DeckSidecarReader.Sha256HexLineEndingNormalised.
            report.InteriorRigSha = DeckSidecarReader.Sha256HexLineEndingNormalised(interiorRigBytes);
            log.AppendLine($"  renderer   : {BoatInteriorKit.InteriorRigFileName} " +
                           $"({report.InteriorRigSha.Substring(0, 12)}…)");

            Dictionary<string, BoatInteriorS0Entry> ledger =
                BoatInteriorS0Ledger.Parse(
                    File.ReadAllText(Path.Combine(repo, BoatInteriorS0Ledger.LedgerPath)));
            log.AppendLine($"  S0 ledger  : {ledger.Count} hull(s) adjudicated");

            IReadOnlyList<HullSidecarIdentity> catalogue = ReadHullCatalogue(repo, log);

            // The interior rig's own HULLS table, read by running the renderer alone.
            IReadOnlyList<BoatInteriorRigHost.HullBinding> table;
            using (IRigScriptHost probeHost = RigScriptHostFactory.Create())
            {
                report.EngineName = probeHost.EngineName;
                table = BoatInteriorRigHost.ReadHullTable(probeHost);
            }
            log.AppendLine($"  engine     : {report.EngineName}");
            log.AppendLine($"  HULLS      : {table.Count} row(s) — " +
                           string.Join(", ", table.Select(t => t.Key)));

            string[] sidecars = Directory.GetFiles(kit, "*.interior.json", SearchOption.AllDirectories);
            Array.Sort(sidecars, StringComparer.Ordinal);
            log.AppendLine($"  sidecars   : {sidecars.Length} found");
            log.AppendLine();

            // Plan first, bake second. Planning is cheap and it groups the work by exterior rig, so
            // the eighteen lobster variants share ONE host instead of loading their rig eighteen times.
            var plans = new List<Plan>();
            foreach (string path in sidecars)
            {
                string rel = RepoRelative(repo, path);
                Plan plan = PlanOne(repo, path, rel, interiorRigBytes, ledger, catalogue, table, out Refusal refusal);
                if (plan == null) { report.Refusals.Add(refusal); continue; }
                plans.Add(plan);
            }

            foreach (var refused in report.Refusals)
                log.AppendLine($"  ✗ {refused.Sidecar}\n      {refused.Reason}");
            if (report.Refusals.Count > 0) log.AppendLine();

            Directory.CreateDirectory(Path.Combine(repo, BoatInteriorKit.OutputFolder));

            // Grouped by (rig file, pick) — one repo cross-check per HULL OBJECT, not per file. Two
            // picks of one file are two hulls with two cells (the sport fishers), and a geometry
            // read for the first pick replayed onto the second refuses the wrong boat.
            foreach (var group in plans.GroupBy(p => (file: p.Binding.ExteriorRigFileName,
                                                      pick: p.Binding.Pick))
                                       .OrderBy(g => g.Key.file, StringComparer.Ordinal)
                                       .ThenBy(g => g.Key.pick, StringComparer.Ordinal))
            {
                RigGeometry? repoGeometry = ReadRepoExteriorGeometry(repo, group.First().Binding, log);

                using IRigScriptHost host = RigScriptHostFactory.Create();
                BoatInteriorRigHost.Install(host, group.First().Binding);

                foreach (Plan plan in group.OrderBy(p => p.HullStem, StringComparer.Ordinal))
                    report.Sheets.Add(BakeOne(repo, host, plan, repoGeometry, log));
            }

            WriteContract(repo, report);

            total.Stop();
            report.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return report;
        }

        // ---- planning ---------------------------------------------------------------------------

        sealed class Plan
        {
            public string SidecarRelative = "";
            public string HullStem = "";
            public string HullFileStem = "";
            public BoatInteriorRigHost.HullBinding Binding;
            public string VariantSize = "", VariantStyle = "", VariantRegion = "";
            public BoatInteriorRead Read;
            public string[] Levels = Array.Empty<string>();
        }

        static Plan PlanOne(string repo, string path, string rel, byte[] interiorRigBytes,
                            Dictionary<string, BoatInteriorS0Entry> ledger,
                            IReadOnlyList<HullSidecarIdentity> catalogue,
                            IReadOnlyList<BoatInteriorRigHost.HullBinding> table,
                            out Refusal refusal)
        {
            refusal = null;
            string json;
            try { json = File.ReadAllText(path); }
            catch (Exception e)
            {
                refusal = new Refusal { Sidecar = rel, Reason = $"unreadable ({e.Message})" };
                return null;
            }

            // GATE 1 — S0, before the geometry is read at all. Reading a refused hull's rooms invites
            // somebody to use them.
            string hullStem = PeekHullStem(json);
            BoatInteriorS0Entry verdict = BoatInteriorS0Ledger.For(ledger, hullStem);
            if (!verdict.IsClean)
            {
                refusal = new Refusal
                {
                    Sidecar = rel, HullStem = hullStem,
                    Verdict = verdict.Verdict.ToString(),
                    UpstreamAsk = verdict.UpstreamAsk,
                    Reason = BoatInteriorS0Ledger.RefusalMessage(verdict),
                    IsLedgerRefusal = true,
                };
                return null;
            }

            // GATE 2 — the sidecar, through the same reader the def builder uses.
            byte[] hullRigBytes = ReadKitHullRig(repo, json);
            BoatInteriorRead read = BoatInteriorSidecarReader.Read(json, rel, interiorRigBytes, hullRigBytes);
            if (!read.Ok)
            {
                refusal = new Refusal
                {
                    Sidecar = rel, HullStem = hullStem,
                    Reason = string.Join(" · ", read.Errors),
                };
                return null;
            }

            if (read.CellLevels.Length == 0)
            {
                refusal = new Refusal
                {
                    Sidecar = rel, HullStem = hullStem,
                    Reason = "cell.levels is absent, so nothing states WHICH levels bake. WALKABLE is " +
                             "not a substitute — it is the game's list of standable planes and carries " +
                             "weather decks the interior rig never draws.",
                };
                return null;
            }

            if (read.CellFacings.Length != BoatInteriorKit.Facings)
            {
                refusal = new Refusal
                {
                    Sidecar = rel, HullStem = hullStem,
                    Reason = $"cell.facings lists {read.CellFacings.Length} entries where this kit bakes " +
                             $"{BoatInteriorKit.Facings}. The sheet's column count is the sidecar's " +
                             "compass, not a constant — refusing rather than baking a compass nobody declared.",
                };
                return null;
            }

            // GATE 3 — the hull, resolved from the committed sidecars' FIELDS.
            BoatInteriorHullResolver.Resolution resolved =
                BoatInteriorHullResolver.Resolve(read.HullStem, catalogue);
            if (!resolved.Ok)
            {
                refusal = new Refusal { Sidecar = rel, HullStem = hullStem, Reason = resolved.Error };
                return null;
            }

            if (!BoatInteriorRigHost.TryBind(table, read.HullStem, out var binding, out string bindError))
            {
                refusal = new Refusal { Sidecar = rel, HullStem = hullStem, Reason = bindError };
                return null;
            }

            var plan = new Plan
            {
                SidecarRelative = rel,
                HullStem = read.HullStem,
                HullFileStem = resolved.HullFileStem,
                Binding = binding,
                Read = read,
                Levels = read.CellLevels,
            };

            // The variant triple comes from the committed sidecar's DECLARATION, never from splitting
            // the stem on underscores. A stem is a name; a name can be right about a file that is wrong
            // about itself.
            if (binding.VariantAware &&
                !TryReadVariantTriple(repo, resolved.HullFileStem, plan, out string variantError))
            {
                refusal = new Refusal { Sidecar = rel, HullStem = hullStem, Reason = variantError };
                return null;
            }

            return plan;
        }

        /// <summary>
        /// The <c>{size, style, region}</c> the variants rig needs, read from the hull's own committed
        /// gameplay sidecar.
        ///
        /// <para>⚠️ All three must be present. The rig resolves each one independently against its own
        /// table with a fall-through default (<c>standard</c>, <c>hardtop</c>, <c>northumberland</c>),
        /// so a single missing field silently substitutes a different boat's dimensions and the sheet
        /// looks perfectly fine.</para>
        /// </summary>
        static bool TryReadVariantTriple(string repo, string hullFileStem, Plan plan, out string error)
        {
            error = "";
            string path = Path.Combine(repo, GameplayFolder, hullFileStem + ".gameplay.json");
            if (!File.Exists(path))
            {
                error = $"{GameplayFolder}/{hullFileStem}.gameplay.json is missing, so nothing declares " +
                        "this variant's size/style/region.";
                return false;
            }

            object variant;
            try { variant = DeckSidecarJson.Member(DeckSidecarJson.Parse(File.ReadAllText(path)), "variant"); }
            catch (Exception e) { error = $"{hullFileStem}.gameplay.json will not parse ({e.Message})."; return false; }

            plan.VariantSize = DeckSidecarJson.String(DeckSidecarJson.Member(variant, "size")) ?? "";
            plan.VariantStyle = DeckSidecarJson.String(DeckSidecarJson.Member(variant, "style")) ?? "";
            plan.VariantRegion = DeckSidecarJson.String(DeckSidecarJson.Member(variant, "region")) ?? "";

            if (plan.VariantSize.Length > 0 && plan.VariantStyle.Length > 0 && plan.VariantRegion.Length > 0)
                return true;

            error = $"{hullFileStem}.gameplay.json declares variant size='{plan.VariantSize}' " +
                    $"style='{plan.VariantStyle}' region='{plan.VariantRegion}' — all three are needed. " +
                    "The rig resolves each against its own table with a fall-through default, so a " +
                    "missing field bakes a DIFFERENT boat under this name, with no error.";
            return false;
        }

        // ---- one hull ---------------------------------------------------------------------------

        static SheetResult BakeOne(string repo, IRigScriptHost host, Plan plan,
                                   RigGeometry? repoGeometry, StringBuilder log)
        {
            string variantJs = BoatInteriorRigHost.VariantLiteral(
                plan.VariantSize, plan.VariantStyle, plan.VariantRegion);

            // GATE 4 — registration, measured from pixels.
            BoatInteriorRegistrationProbe.Report probe = BoatInteriorRegistrationProbe.Measure(
                host, plan.Binding.Key, plan.HullStem, plan.Binding.ExteriorHullJs,
                plan.VariantSize, plan.VariantStyle, plan.VariantRegion,
                plan.Levels,
                plan.Read.CellPixels.x, plan.Read.CellPixels.y,
                plan.Read.PivotPixels.x, plan.Read.PivotPixels.y,
                repoGeometry);

            if (!probe.Ok)
                throw new InvalidOperationException(
                    $"REGISTRATION REFUSED for {plan.HullStem}:\n    " +
                    string.Join("\n    ", probe.Errors) +
                    "\n  This is the property the whole kit exists for. Baking anyway would produce art " +
                    "that looks right on its own and is wrong on the boat.");

            // The rig's own level list must agree with the sidecar's, or one of the two is stale and
            // the sheet would carry rows nothing asks for.
            string[] rigLevels = BoatInteriorRigHost.LevelsOf(host, plan.Binding.Key);
            if (!rigLevels.SequenceEqual(plan.Levels, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    $"{plan.HullStem}: the sidecar's cell.levels is [{string.Join(", ", plan.Levels)}] " +
                    $"but the rig draws [{string.Join(", ", rigLevels)}]. One of the two has moved.");

            int cellW = probe.InteriorW, cellH = probe.InteriorH;
            int cellsTotal = plan.Levels.Length * BoatInteriorKit.Facings;

            BoatInteriorKit.Pack(cellW, cellH, cellsTotal, BoatInteriorKit.Facings,
                                 out int columns, out int cellsPerPage);
            int pageCount = Mathf.CeilToInt(cellsTotal / (float)cellsPerPage);
            string assetName = BoatInteriorHullResolver.AssetName(plan.HullFileStem);

            var entry = new BoatInteriorKit.SheetEntry
            {
                hullStem = plan.HullStem,
                hullFileStem = plan.HullFileStem,
                defId = BoatInteriorHullResolver.DefId(plan.HullFileStem),
                interiorHullKey = plan.Binding.Key,
                variantSize = plan.VariantSize,
                variantStyle = plan.VariantStyle,
                variantRegion = plan.VariantRegion,
                cellW = cellW, cellH = cellH,
                pivotX = probe.InteriorPivotX, pivotY = probe.InteriorPivotY,
                pixelsPerMetre = plan.Read.PixelsPerMetre,
                levels = plan.Levels,
                facings = BoatInteriorKit.Facings,
                convention = probe.Convention.ToString(),
                doorOpen = BoatInteriorKit.BakedDoorOpen,
                night = BoatInteriorKit.BakedNight,
                lamp = BoatInteriorKit.BakedLamp,
                weather = BoatInteriorKit.BakedWeather,
                clutter = BoatInteriorKit.BakedClutter,
            };

            // The rig's px/m and the sidecar's must be the same number, or the sheet's pixels-per-unit
            // would disagree with the geometry it was measured in.
            if (probe.InteriorScale != plan.Read.PixelsPerMetre)
                throw new InvalidOperationException(
                    $"{plan.HullStem}: the sidecar says {plan.Read.PixelsPerMetre} px/m and the rig's " +
                    $"cell says {probe.InteriorScale}. This kit holds TWO pixel grids and the sheet's " +
                    "pixels-per-unit comes from this number — a disagreement here would place the hull " +
                    "at the wrong scale in the world.");

            var clock = new Stopwatch();
            var pages = new List<BoatInteriorKit.SheetPage>(pageCount);
            long pngBytes = 0;

            for (int page = 0; page < pageCount; page++)
            {
                int firstCell = page * cellsPerPage;
                int cellsHere = Mathf.Min(cellsPerPage, cellsTotal - firstCell);
                int rows = Mathf.CeilToInt(cellsHere / (float)columns);
                int pw = columns * cellW, ph = rows * cellH;

                if (pw > BoatInteriorKit.ImportSizeCap || ph > BoatInteriorKit.ImportSizeCap)
                    throw new InvalidOperationException(
                        $"{plan.HullStem} page {page} packs to {pw}×{ph}, past this kit's " +
                        $"{BoatInteriorKit.ImportSizeCap} px cap. A sheet between the cap and Unity's " +
                        "hard 4096 BAKES FINE and then imports SILENTLY DOWNSCALED with the sprite " +
                        "count still correct, which poisons every rect.");

                var pixels = new Color32[pw * ph];

                for (int i = 0; i < cellsHere; i++)
                {
                    int flat = firstCell + i;
                    int levelIndex = flat / BoatInteriorKit.Facings;
                    int facing = flat % BoatInteriorKit.Facings;

                    double dir = RigBaker.DirForCell(facing, BoatInteriorKit.Facings, probe.Convention);
                    string d = dir.ToString("R", CultureInfo.InvariantCulture);

                    clock.Start();
                    byte[] rgba = host.EvaluateBytes(BoatInteriorRigHost.RenderExpression(
                        plan.Binding.Key, plan.Levels[levelIndex], variantJs, d));
                    clock.Stop();

                    if (rgba.Length != cellW * cellH * 4)
                        throw new InvalidOperationException(
                            $"{plan.HullStem} {plan.Levels[levelIndex]}/d{facing} came back " +
                            $"{rgba.Length} bytes, expected {cellW * cellH * 4}. The interior rig " +
                            "returns a FOUR-BYTE buffer when it cannot resolve the hull — a refusal " +
                            "that looks like a very small picture.");

                    BoatInteriorKit.Slot(i, columns, out int col, out int rowFromTop);
                    RigBaker.Blit(rgba, cellW, cellH, pixels, pw, ph, col, rowFromTop);
                }

                string file = BoatInteriorKit.PageFileName(assetName, page, pageCount);
                pngBytes += WritePng(repo, pixels, pw, ph, $"{BoatInteriorKit.OutputFolder}/{file}");

                pages.Add(new BoatInteriorKit.SheetPage
                {
                    file = file, columns = columns, rows = rows,
                    sheetW = pw, sheetH = ph,
                    firstCell = firstCell, cellCount = cellsHere,
                });
            }

            entry.pages = pages.ToArray();

            var result = new SheetResult
            {
                Entry = entry, Probe = probe, PngBytes = pngBytes,
                RenderMilliseconds = clock.Elapsed.TotalMilliseconds,
            };

            log.AppendLine($"  ✓ {plan.HullStem}");
            log.AppendLine($"      → {entry.defId}  cell {cellW}×{cellH} pivot " +
                           $"({entry.pivotX},{entry.pivotY}) @{entry.pixelsPerMetre} px/m · " +
                           $"{entry.levels.Length} level(s) × {entry.facings} facings = {cellsTotal} cells");
            log.AppendLine($"      → {pages.Count} page(s): " +
                           string.Join(", ", pages.Select(p => $"{p.file} {p.sheetW}×{p.sheetH}")));
            log.AppendLine($"      {probe}");
            log.AppendLine($"      {result.RuntimeMegabytes:F1} MB RGBA32 · {pngBytes / 1024.0:F0} kB PNG · " +
                           $"{result.RenderMilliseconds / 1000.0:F1} s");
            return result;
        }

        // ---- filesystem -------------------------------------------------------------------------

        static long WritePng(string repo, Color32[] pixels, int w, int h, string assetPath)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(repo, assetPath), png);
                return png.Length;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static void WriteContract(string repo, BakeReport report)
        {
            var contract = new BoatInteriorKit.Contract
            {
                generated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                interiorRig = $"{BoatInteriorKit.KitFolder}/{BoatInteriorKit.InteriorRigFileName}",
                interiorRigSha256 = report.InteriorRigSha,
                engine = report.EngineName,
                importSizeCap = BoatInteriorKit.ImportSizeCap,
                sheets = report.Sheets
                               .Select(s => s.Entry)
                               .OrderBy(e => e.hullFileStem, StringComparer.Ordinal)
                               .ToArray(),
                refused = report.Refusals
                                .Select(r => new BoatInteriorKit.RefusedEntry
                                {
                                    hullStem = r.HullStem, verdict = r.Verdict, upstreamAsk = r.UpstreamAsk,
                                })
                                .OrderBy(r => r.hullStem, StringComparer.Ordinal)
                                .ToArray(),
            };

            string abs = Path.Combine(repo, BoatInteriorKit.ContractPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs));
            File.WriteAllText(abs, JsonUtility.ToJson(contract, prettyPrint: true));
        }

        /// <summary>
        /// The exterior rig an interior sidecar pins, read from the KIT. See
        /// <c>BoatInteriorDefBuilder.ReadHullRig</c> for why this is never the repository's copy: the
        /// pin names the rig that SHIPPED WITH the sidecar, and hashing the repository's would refuse
        /// all twenty-seven.
        /// </summary>
        static byte[] ReadKitHullRig(string repo, string interiorJson)
        {
            try
            {
                var pin = DeckSidecarJson.AsObject(
                    DeckSidecarJson.Member(DeckSidecarJson.Parse(interiorJson), "hullRigSha256"));
                // The unanimity rule (BoatInteriorHullResolver.TryUnanimousHullRigPin): the STEM
                // composes the path — a raw variant key would reach hull-rigs/<stem>.<variant>.js,
                // which does not exist, and the null here then reads downstream as an internally
                // inconsistent kit. A refused pin returns null; the sidecar reader owns the message.
                if (!BoatInteriorHullResolver.TryUnanimousHullRigPin(pin, out string stem, out _, out _))
                    return null;
                string p = Path.Combine(repo, BoatInteriorKit.KitFolder, "hull-rigs", stem + ".js");
                return File.Exists(p) ? File.ReadAllBytes(p) : null;
            }
            catch (Exception) { /* the reader reports an unparseable sidecar properly */ }
            return null;
        }

        /// <summary>
        /// The SHIPPED exterior rig's cell, read in a host of its own.
        ///
        /// <para>It needs its own host because the kit's revision and the repository's copy install the
        /// SAME global — one host can only hold one of them, and whichever ran last would silently
        /// answer for both. Null when the repository has no such rig, which the probe treats as
        /// "nothing to cross-check against" rather than as a failure.</para>
        /// </summary>
        static RigGeometry? ReadRepoExteriorGeometry(string repo, in BoatInteriorRigHost.HullBinding binding,
                                                     StringBuilder log)
        {
            string path = Path.Combine(repo, RigFolder, binding.ExteriorRigFileName);
            if (!File.Exists(path))
            {
                log.AppendLine($"  note: {RigFolder}/{binding.ExteriorRigFileName} is not in the " +
                               "repository, so the shipped rig's cell cannot be cross-checked.");
                return null;
            }

            try
            {
                using IRigScriptHost host = RigScriptHostFactory.Create();
                host.Execute(File.ReadAllText(path));
                string g = binding.ExteriorHullJs;
                if (!host.EvaluateBool($"!!(typeof {g} === 'object' && {g} && typeof {g}.W === 'number' && {g}.pivot)"))
                {
                    log.AppendLine($"  note: the repository's {binding.ExteriorRigFileName} exposes no " +
                                   "W/H/pivot triple — no cross-check.");
                    return null;
                }
                return new RigGeometry(
                    (int)host.EvaluateNumber($"{g}.W"),
                    (int)host.EvaluateNumber($"{g}.H"),
                    host.EvaluateNumber($"{g}.pivot.x"),
                    host.EvaluateNumber($"{g}.pivot.y"),
                    0, 0, 0);
            }
            catch (Exception e)
            {
                log.AppendLine($"  note: the repository's {binding.ExteriorRigFileName} would not run " +
                               $"({e.Message.Split('\n')[0]}) — no cross-check.");
                return null;
            }
        }

        static IReadOnlyList<HullSidecarIdentity> ReadHullCatalogue(string repo, StringBuilder log)
        {
            var catalogue = new List<HullSidecarIdentity>();
            string folder = Path.Combine(repo, GameplayFolder);
            if (!Directory.Exists(folder))
            {
                log.AppendLine($"  {GameplayFolder} not found — no hull can be resolved.");
                return catalogue;
            }

            string[] files = Directory.GetFiles(folder, "*.gameplay.json");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string f in files)
            {
                try
                {
                    object root = DeckSidecarJson.Parse(File.ReadAllText(f));
                    catalogue.Add(new HullSidecarIdentity(
                        Path.GetFileName(f).Replace(".gameplay.json", ""),
                        DeckSidecarJson.String(DeckSidecarJson.Member(root, "rig")),
                        BoatInteriorHullResolver.VariantKeyOf(DeckSidecarJson.Member(root, "variant"))));
                }
                catch (Exception e)
                {
                    log.AppendLine($"  hull catalogue: {Path.GetFileName(f)} unreadable ({e.Message})");
                }
            }
            log.AppendLine($"  hulls      : {catalogue.Count} committed gameplay sidecar(s)");
            return catalogue;
        }

        static string PeekHullStem(string json)
        {
            try
            {
                return DeckSidecarJson.String(
                    DeckSidecarJson.Member(DeckSidecarJson.Parse(json), "hull_stem")) ?? "";
            }
            catch (Exception) { return ""; }
        }

        static string RepoRelative(string repo, string path)
            => path.Substring(repo.Length).TrimStart('/', '\\').Replace('\\', '/');
    }
}
