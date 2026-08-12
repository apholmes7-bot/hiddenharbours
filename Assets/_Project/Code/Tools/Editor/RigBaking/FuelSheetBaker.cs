using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.Art.Editor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Bakes the fuel kit: one sheet per (vessel × size × grade), laid out as
    /// <b>columns = facings, rows = fill states</b>.
    ///
    /// <para>Why this kit needs its own baker rather than joining <see cref="IsoPropSheetBaker"/>:
    /// that one serves families with a FIXED native buffer and one cell for the whole family, called
    /// as <c>render(key, dir, opts)</c>. Here the buffer is sized per (type, size, mode) by the rig's
    /// own <c>cell()</c>, and the sheet carries a second axis — fill — that no other kit has.</para>
    ///
    /// <para><b>No silent skips and no silent defaults.</b> Every render passes an explicit
    /// <c>size</c>, <c>fuel</c>, <c>fill</c> and <c>wear</c>, because each of those has a
    /// fall-through in the rig that produces a plausible wrong picture rather than an error.</para>
    /// </summary>
    public static class FuelSheetBaker
    {
        public sealed class SheetResult
        {
            public string Key, Type, Size, Grade, AssetPath;
            public int NativeW, NativeH, CellW, CellH, PivotX, PivotY;
            public int Columns, Rows, SheetW, SheetH, Facings, FillCount, PngBytes;
            public bool PivotInsideInk;
            public double RenderMilliseconds;

            public int CroppedPixels => SheetW * SheetH;

            public override string ToString() =>
                $"{Key}: cell {CellW}×{CellH} pivot {PivotX},{PivotY} → {Columns}×{Rows} " +
                $"sheet {SheetW}×{SheetH} ({PngBytes:N0} B)";
        }

        public sealed class BakeReport
        {
            public FuelRegistrationProbe.Report Probe;
            public readonly List<SheetResult> Sheets = new();
            public double TotalMilliseconds;
            public string EngineName;

            public long TotalPixels => Sheets.Sum(s => (long)s.CroppedPixels);
            public long TotalPngBytes => Sheets.Sum(s => (long)s.PngBytes);

            /// <summary>Runtime cost at RGBA32, which is what the import settings lock. The number
            /// the performance budget actually pays (CLAUDE.md rule 7), not the PNG size on disk.</summary>
            public double RuntimeMegabytes => TotalPixels * 4.0 / (1024 * 1024);
        }

        public static BakeReport BakeAll(IReadOnlyList<FuelStorageKit.Build> builds = null)
        {
            builds ??= FuelStorageKit.Builds;
            var total = Stopwatch.StartNew();

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var report = new BakeReport { EngineName = host.EngineName };

            // ---- measure before baking ----------------------------------------------------------
            report.Probe = FuelRegistrationProbe.Measure(host);
            var entry = RigCatalog.Get(FuelStorageKit.RigKey);

            if (report.Probe.Measured != entry.DeclaredConvention)
                throw new InvalidOperationException(
                    $"The fuel rig MEASURES {report.Probe.Measured} but the catalog DECLARES " +
                    $"{entry.DeclaredConvention}. Getting this wrong does not fail — it writes every " +
                    "facing in reverse order. Correct the catalog only after confirming the " +
                    "measurement by eye.");

            FuelRegistrationProbe.AssertVocabularyMatches(host, entry.GlobalName, report.Probe);

            foreach (var b in builds)
                report.Sheets.Add(BakeOne(host, entry.GlobalName, b, entry.DeclaredConvention));

            WriteContract(host, entry.GlobalName, report);

            total.Stop();
            report.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return report;
        }

        // ---- one sheet ------------------------------------------------------------------------

        static SheetResult BakeOne(IRigScriptHost host, string g, in FuelStorageKit.Build b,
                                   AzimuthConvention convention)
        {
            AssertKnown(host, g, b);

            bool rest = FuelStorageKit.IsCarry(b.Type) && FuelStorageKit.BakeCarryTypesAtRest;
            int facings = FuelStorageKit.FacingsFor(b.Type);
            var fills = FuelStorageKit.FillsFor(b.Type);

            // The rig sizes the buffer per (type, size, mode) and pins the pivot identically in every
            // cell. Read it from the rig — never restate it (ADR 0021 §4).
            string cellExpr = $"{g}.cell('{b.Type}','{b.Size}',{Opts(b, rest, fills[0])})";
            int nw = (int)host.EvaluateNumber($"{cellExpr}.W");
            int nh = (int)host.EvaluateNumber($"{cellExpr}.H");
            int npx = Mathf.RoundToInt((float)host.EvaluateNumber($"{cellExpr}.cx"));
            int npy = Mathf.RoundToInt((float)host.EvaluateNumber($"{cellExpr}.cy"));

            // ---- render every (fill, facing) ---------------------------------------------------
            var clock = new Stopwatch();
            int expected = nw * nh * 4;
            var frames = new byte[fills.Count][][];

            for (int f = 0; f < fills.Count; f++)
            {
                frames[f] = new byte[facings][];
                for (int cell = 0; cell < facings; cell++)
                {
                    double dir = RigBaker.DirForCell(cell, facings, convention);
                    string d = dir.ToString("R", CultureInfo.InvariantCulture);

                    clock.Start();
                    frames[f][cell] = host.EvaluateBytes(
                        $"{g}.render('{b.Type}',{d},{Opts(b, rest, fills[f])})");
                    clock.Stop();

                    if (frames[f][cell] == null || frames[f][cell].Length != expected)
                        throw new InvalidOperationException(
                            $"{g}.render('{b.Type}',{d},…) came back " +
                            $"{(frames[f][cell]?.Length.ToString() ?? "null")} bytes, expected " +
                            $"{expected} for a {nw}×{nh} RGBA buffer. This rig returns a BARE RGBA " +
                            "array; a wrapper means its return shape changed.");
                }
            }

            // ---- crop: pivot-inclusive ink union across EVERY facing AND EVERY FILL --------------
            // Unioning over fills as well as facings is what makes the fill rows slice into identical
            // rects. A per-fill crop would shift the pivot row by row, and a container would appear to
            // hop as it drained — silently, because every individual cell would look correct.
            var all = frames.SelectMany(row => row).ToList();
            if (!IsoPropSheetBaker.MeasureCell(all, nw, nh, npx, npy,
                                               out int cw, out int ch, out int pivotX, out int pivotY,
                                               out int left, out int top, out bool pivotInsideInk))
                throw new InvalidOperationException(
                    $"Every cell of {b.Key} rendered fully transparent. The vessel resolved but drew " +
                    "no pixels — refusing rather than writing an empty sheet.");

            // ---- pack: columns are facings, rows are fills ---------------------------------------
            int cols = facings, rows = fills.Count;
            int pw = cols * cw, ph = rows * ch;

            if (pw > FuelStorageKit.ImportSizeCap || ph > FuelStorageKit.ImportSizeCap)
                throw new InvalidOperationException(
                    $"{b.Key} packs to {pw}×{ph}, past this kit's {FuelStorageKit.ImportSizeCap} px " +
                    "import cap. A sheet between the cap and Unity's hard 4096 BAKES FINE and then " +
                    "imports SILENTLY DOWNSCALED with the sprite count still correct, which poisons " +
                    "every slice rect. Reduce a facing or fill count rather than lifting the cap.");

            var pixels = new Color32[pw * ph];
            for (int f = 0; f < rows; f++)
                for (int cell = 0; cell < cols; cell++)
                    BuildingRigBaker.BlitCropped(frames[f][cell], nw, nh,
                                                 npx + left, npy + top, cw, ch,
                                                 pixels, pw, ph,
                                                 col: cell, rowFromTop: f);

            var result = new SheetResult
            {
                Key = b.Key, Type = b.Type, Size = b.Size, Grade = b.Grade,
                NativeW = nw, NativeH = nh,
                CellW = cw, CellH = ch, PivotX = pivotX, PivotY = pivotY,
                PivotInsideInk = pivotInsideInk,
                Columns = cols, Rows = rows, SheetW = pw, SheetH = ph,
                Facings = facings, FillCount = fills.Count,
                RenderMilliseconds = clock.Elapsed.TotalMilliseconds,
                AssetPath = $"{FuelStorageKit.OutputFolder}/{b.Key}.png",
            };

            WritePng(pixels, pw, ph, result);
            return result;
        }

        /// <summary>
        /// The render options, spelled out in full on every single call.
        ///
        /// <para>⚠️ Every one of these has a fall-through that produces a picture rather than an
        /// error, so none may be left to the rig's default. <c>size</c> falls through to a different
        /// vessel; <c>fill</c> defaults to 0.6; and <c>wear</c> — the worst of the three — selects a
        /// phantom state that shares a geometry-cache key with <c>'working'</c>, so omitting it makes
        /// the output depend on what was rendered earlier in the session.</para>
        /// </summary>
        static string Opts(in FuelStorageKit.Build b, bool rest, float fill)
        {
            string f = fill.ToString("R", CultureInfo.InvariantCulture);
            return $"{{size:'{b.Size}',fuel:'{b.Grade}',fill:{f}," +
                   $"wear:'{FuelStorageKit.BakedWear}'{(rest ? ",rest:true" : "")}}}";
        }

        /// <summary>
        /// Refuse a vessel or size the rig does not know, BEFORE rendering.
        ///
        /// <para><c>resolveSize</c> falls through to <c>sizes[min(len−1,1)]</c> for anything
        /// unrecognised, so a typo'd size bakes a real vessel of the WRONG size under the right
        /// filename — a defect that survives every downstream check because the sheet is valid.</para>
        /// </summary>
        static void AssertKnown(IRigScriptHost host, string g, in FuelStorageKit.Build b)
        {
            if (!host.EvaluateBool($"typeof {g}.TYPES['{b.Type}'] === 'object'"))
                throw new InvalidOperationException(
                    $"'{b.Type}' is not a vessel this rig draws. Known: " +
                    $"{host.EvaluateString($"{g}.TYPE_ORDER.join(', ')")}.");

            if (!host.EvaluateBool($"{g}.sizesOf('{b.Type}').indexOf('{b.Size}') >= 0"))
                throw new InvalidOperationException(
                    $"'{b.Size}' is not a size of '{b.Type}' (the rig has " +
                    $"{host.EvaluateString($"{g}.sizesOf('{b.Type}').join(', ')")}). resolveSize " +
                    "would silently substitute a different size and the sheet would look fine.");

            if (!host.EvaluateBool($"typeof {g}.FUELS['{b.Grade}'] === 'object'"))
                throw new InvalidOperationException(
                    $"'{b.Grade}' is not a grade this rig knows. Known: " +
                    $"{host.EvaluateString($"{g}.FUEL_ORDER.join(', ')")}.");
        }

        // ---- the contract ----------------------------------------------------------------------

        static void WriteContract(IRigScriptHost host, string g, BakeReport report)
        {
            var contract = new FuelStorageKit.Contract
            {
                generated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                rigKey = FuelStorageKit.RigKey,
                globalName = FuelStorageKit.GlobalName,
                convention = report.Probe.Measured.ToString(),
                pixelsPerUnit = FuelStorageKit.PixelsPerUnit,
                importSizeCap = FuelStorageKit.ImportSizeCap,
                bakedWear = FuelStorageKit.BakedWear,
                carryFacings = FuelStorageKit.CarryFacings,
                storeFacings = FuelStorageKit.StoreFacings,
                sheets = report.Sheets.Select(s => new FuelStorageKit.SheetEntry
                {
                    key = s.Key, type = s.Type, size = s.Size, grade = s.Grade,
                    kind = FuelStorageKit.IsCarry(s.Type) ? "carry" : "store",
                    wear = FuelStorageKit.BakedWear,
                    rest = FuelStorageKit.IsCarry(s.Type) && FuelStorageKit.BakeCarryTypesAtRest,
                    facings = s.Facings,
                    cellW = s.CellW, cellH = s.CellH,
                    pivotX = s.PivotX, pivotY = s.PivotY,
                    columns = s.Columns, rows = s.Rows,
                    sheetW = s.SheetW, sheetH = s.SheetH,
                    fills = FuelStorageKit.FillsFor(s.Type).ToArray(),
                    pivotInsideInk = s.PivotInsideInk,
                }).ToArray(),
            };

            // The read mechanism, the shape and the volumes are the RIG's facts, so they are read
            // back from it rather than restated here — same rule as the cell and the pivot.
            EnrichFromRig(host, g, contract);

            string abs = Path.Combine(RigCatalog.RepoRoot, FuelStorageKit.ContractPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            File.WriteAllText(abs, JsonUtility.ToJson(contract, prettyPrint: true));
        }

        /// <summary>Fills the read/shape/volume fields the rig owns, from the live rig.</summary>
        public static void EnrichFromRig(IRigScriptHost host, string g, FuelStorageKit.Contract c)
        {
            foreach (var s in c.sheets)
            {
                s.read = host.EvaluateString($"{g}.TYPES['{s.type}'].read");
                s.shape = host.EvaluateString($"{g}.TYPES['{s.type}'].shape");
                s.litres = (float)host.EvaluateNumber($"{g}.TYPES['{s.type}'].sizes['{s.size}'].L");
                s.tare = (float)host.EvaluateNumber($"{g}.TYPES['{s.type}'].sizes['{s.size}'].tare");
                s.fullKg = (float)host.EvaluateNumber(
                    $"{g}.level('{s.type}','{s.size}',1,{{fuel:'{s.grade}'}}).kg");
            }
        }

        static void WritePng(Color32[] pixels, int w, int h, SheetResult result)
        {
            string outAbs = Path.Combine(RigCatalog.RepoRoot,
                                         Path.GetDirectoryName(result.AssetPath) ?? "");
            Directory.CreateDirectory(outAbs);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, result.AssetPath), png);
                result.PngBytes = png.Length;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
