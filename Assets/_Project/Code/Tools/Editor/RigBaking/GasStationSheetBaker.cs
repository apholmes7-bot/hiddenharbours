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
    /// Bakes the gas-station kit: one sheet per piece, grid-packed, columns first.
    ///
    /// <para>Why this kit needs its own baker rather than joining <see cref="IsoPropSheetBaker"/>:
    /// that one serves families with a FIXED native buffer and one cell for the whole family, called
    /// as <c>render(key, dir, opts)</c>. Here the buffer is sized per (type, size, opts) by the rig's
    /// own <c>cell()</c>; two different globals with two different argument orders write into the
    /// same sheet set; and the biggest piece will not fit a single strip at all, so the layout is
    /// solved per sheet instead of being a constant.</para>
    ///
    /// <para><b>No silent skips and no silent defaults.</b> Every render passes every option
    /// explicitly. This rig is measurably better behaved than the fuel kit about that — its
    /// <c>resolve()</c> funnels each option before both the geometry cache key and the wear pass, so
    /// an omission is the default rather than a phantom fourth state — but "measurably better today"
    /// is a reason to keep the check, not to drop the discipline.</para>
    /// </summary>
    public static class GasStationSheetBaker
    {
        public sealed class SheetResult
        {
            public string Key, Type, Size, Label, AssetPath;

            /// <summary>Where the sheet's <c>&lt;stem&gt;.recipe.json</c> went — the rig call that
            /// produced it (see <see cref="RigRecipe"/>).</summary>
            public string RecipePath;
            public bool Interior, Fold180;
            public int NativeW, NativeH, CellW, CellH, PivotX, PivotY;
            public int Columns, Rows, SheetW, SheetH, Facings, PngBytes;
            public bool PivotInsideInk;
            public double RenderMilliseconds;

            public int CroppedPixels => SheetW * SheetH;

            public override string ToString() =>
                $"{Key}: cell {CellW}×{CellH} pivot {PivotX},{PivotY} → {Columns}×{Rows} " +
                $"sheet {SheetW}×{SheetH} ({PngBytes:N0} B)";
        }

        public sealed class BakeReport
        {
            public StationRegistrationProbe.Report Probe;
            public readonly List<SheetResult> Sheets = new();
            public double TotalMilliseconds;
            public string EngineName;

            public long TotalPixels => Sheets.Sum(s => (long)s.CroppedPixels);
            public long TotalPngBytes => Sheets.Sum(s => (long)s.PngBytes);

            /// <summary>Runtime cost at RGBA32, which is what the import settings lock. The number the
            /// performance budget actually pays (CLAUDE.md rule 7), not the PNG size on disk.</summary>
            public double RuntimeMegabytes => TotalPixels * 4.0 / (1024 * 1024);
        }

        public static BakeReport BakeAll(IReadOnlyList<GasStationKit.Build> builds = null)
        {
            builds ??= GasStationKit.Builds;
            var total = Stopwatch.StartNew();

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var report = new BakeReport { EngineName = host.EngineName };

            // ---- measure before baking ----------------------------------------------------------
            report.Probe = StationRegistrationProbe.Measure(host);
            var entry = RigCatalog.Get(GasStationKit.RigKey);

            if (report.Probe.Measured != entry.DeclaredConvention)
                throw new InvalidOperationException(
                    $"The station rig MEASURES {report.Probe.Measured} but the catalog DECLARES " +
                    $"{entry.DeclaredConvention}. Getting this wrong does not fail — it writes every " +
                    "facing in reverse order. Correct the catalog only after confirming the " +
                    "measurement by eye.");

            var interiorEntry = RigCatalog.Get(GasStationKit.InteriorRigKey);
            if (interiorEntry.DeclaredConvention != entry.DeclaredConvention)
                throw new InvalidOperationException(
                    $"The shell declares {entry.DeclaredConvention} and its interior declares " +
                    $"{interiorEntry.DeclaredConvention}. The room paints into the shell's own cell " +
                    "through the shell's own camera; they cannot turn opposite ways.");

            StationRegistrationProbe.AssertVocabularyMatches(host, entry.GlobalName, report.Probe);

            foreach (var b in builds)
                report.Sheets.Add(BakeOne(host, b, entry.DeclaredConvention));

            WriteContract(host, report, entry.DeclaredConvention);

            total.Stop();
            report.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return report;
        }

        // ---- one sheet ---------------------------------------------------------------------------

        static SheetResult BakeOne(IRigScriptHost host, in GasStationKit.Build b,
                                   AzimuthConvention convention)
        {
            AssertKnown(host, b);

            string g = b.Interior ? GasStationKit.InteriorGlobalName : GasStationKit.GlobalName;
            bool fold = GasStationKit.IsFolded(b.Type);
            int facings = b.Interior ? GasStationKit.Facings : GasStationKit.FacingsFor(b.Type);
            string opts = Opts(b.Size);

            // The rig sizes the buffer per (type, size, opts) and pins the pivot identically in every
            // cell. Read it from the rig — never restate it (ADR 0021 §4). The interior asks the SHELL
            // for its cell, so both sheets of a storefront measure the same quad; they crop apart
            // afterwards and re-register on the shared pivot.
            string cellExpr = b.Interior
                ? $"{GasStationKit.InteriorGlobalName}.cell('{b.Size}',{opts})"
                : $"{GasStationKit.GlobalName}.cell('{b.Type}','{b.Size}',{opts})";

            int nw = (int)host.EvaluateNumber($"{cellExpr}.W");
            int nh = (int)host.EvaluateNumber($"{cellExpr}.H");
            int npx = Mathf.RoundToInt((float)host.EvaluateNumber($"{cellExpr}.cx"));
            int npy = Mathf.RoundToInt((float)host.EvaluateNumber($"{cellExpr}.cy"));

            // ---- render every facing ----------------------------------------------------------
            var clock = new Stopwatch();
            int expected = nw * nh * 4;
            var frames = new byte[facings][];

            for (int cell = 0; cell < facings; cell++)
            {
                // ⚠️ ALWAYS the EIGHT-facing mapping, even for a folded piece. RigBaker.DirForCell
                // with facings=4 means dirs 0·2·4·6 — a DECIMATION, four snapped quarters — and for a
                // 180°-symmetric piece that renders only two distinct pictures and throws away half
                // the angular resolution. The fold wants dirs 0·1·2·3, one per equivalence class, so
                // it asks for the eight-facing dir of the first four cells. Under either convention
                // that is a complete set of representatives.
                double dir = RigBaker.DirForCell(cell, GasStationKit.Facings, convention);
                string d = dir.ToString("R", CultureInfo.InvariantCulture);

                // ⚠️ TWO GLOBALS, TWO ARGUMENT ORDERS. StationIso is render(type, dir, opts);
                // StationInterior is render(size, dir, opts). Neither throws when handed the other's
                // shape — the shell renders a kerb post and the room renders the pay booth — so the
                // two call sites are spelled out separately rather than sharing a format string.
                string expr = b.Interior
                    ? $"{GasStationKit.InteriorGlobalName}.render('{b.Size}',{d},{opts})"
                    : $"{GasStationKit.GlobalName}.render('{b.Type}',{d},{opts})";

                clock.Start();
                frames[cell] = host.EvaluateBytes(expr);
                clock.Stop();

                if (frames[cell] == null || frames[cell].Length != expected)
                    throw new InvalidOperationException(
                        $"{expr} came back {(frames[cell]?.Length.ToString() ?? "null")} bytes, " +
                        $"expected {expected} for a {nw}×{nh} RGBA buffer. These rigs return a BARE " +
                        "RGBA array; a wrapper means a return shape changed.");
            }

            // ---- crop: pivot-inclusive ink union across EVERY baked facing ----------------------
            if (!IsoPropSheetBaker.MeasureCell(frames, nw, nh, npx, npy,
                                               out int cw, out int ch, out int pivotX, out int pivotY,
                                               out int left, out int top, out bool pivotInsideInk))
                throw new InvalidOperationException(
                    $"Every cell of {b.Key} rendered fully transparent. The piece resolved but drew " +
                    "no pixels — refusing rather than writing an empty sheet.");

            // ---- pack ---------------------------------------------------------------------------
            int cols = GasStationKit.Columns(facings, cw, ch);
            if (cols == 0)
                throw new InvalidOperationException(
                    $"{b.Key} has a {cw}×{ch} cell that will not pack {facings} cells inside this " +
                    $"kit's {GasStationKit.ImportSizeCap} px cap in ANY layout. A sheet between the " +
                    "cap and Unity's hard limit BAKES FINE and then imports SILENTLY DOWNSCALED with " +
                    "the sprite count still correct, which poisons every slice rect. Reduce the " +
                    "facing count rather than lifting the cap.");

            int rows = Mathf.CeilToInt(facings / (float)cols);
            int pw = cols * cw, ph = rows * ch;

            var pixels = new Color32[pw * ph];
            for (int cell = 0; cell < facings; cell++)
                BuildingRigBaker.BlitCropped(frames[cell], nw, nh,
                                             npx + left, npy + top, cw, ch,
                                             pixels, pw, ph,
                                             col: cell % cols, rowFromTop: cell / cols);

            var result = new SheetResult
            {
                Key = b.Key, Type = b.Type, Size = b.Size, Interior = b.Interior, Fold180 = fold,
                Label = Label(host, b),
                NativeW = nw, NativeH = nh,
                CellW = cw, CellH = ch, PivotX = pivotX, PivotY = pivotY,
                PivotInsideInk = pivotInsideInk,
                Columns = cols, Rows = rows, SheetW = pw, SheetH = ph,
                Facings = facings,
                RenderMilliseconds = clock.Elapsed.TotalMilliseconds,
                AssetPath = $"{GasStationKit.OutputFolder}/{b.Key}.png",
            };

            WritePng(pixels, pw, ph, result);

            // ---- the recipe: what drew this sheet, beside it --------------------------------------
            // ⚠️ The facing axis carries the EIGHT-facing dirs even on a folded piece, because the
            // fold bakes one representative per equivalence class (0·1·2·3) rather than the
            // decimation DirForCell(k, 4, …) would give (0·2·4·6). A recipe that recorded the
            // decimation would render two pictures where the sheet has four.
            result.RecipePath = RigRecipe.Write(result.AssetPath, new RigRecipe
            {
                Kit = RecipeKit,
                Baker = nameof(GasStationSheetBaker),
                Rig = RigRecipe.RigBlockFor(b.Interior ? GasStationKit.InteriorRigKey
                                                       : GasStationKit.RigKey),
                Call = new RigRecipe.CallBlock
                {
                    // TWO GLOBALS, TWO ARGUMENT ORDERS — StationIso is render(type, dir, opts),
                    // StationInterior is render(size, dir, opts). The template carries the
                    // difference; nothing downstream has to remember it.
                    Args = { b.Interior ? b.Size : b.Type, "$dir", "$opts" },
                    Opts = OptsOf(b.Size),
                },
                Grid = new RigRecipe.GridBlock
                {
                    Columns = cols, Rows = rows,
                    Axes = { FacingAxis(facings, convention) },
                },
                Pack = new RigRecipe.PackBlock
                {
                    NativeW = nw, NativeH = nh, NativePivotX = npx, NativePivotY = npy,
                    CropX = npx + left, CropY = npy + top,
                    CellW = cw, CellH = ch, PivotX = pivotX, PivotY = pivotY,
                    SheetW = pw, SheetH = ph,
                },
            });

            return result;
        }

        /// <summary>The kit id a recipe files itself under.</summary>
        public const string RecipeKit = "gas-station";

        /// <summary>
        /// The facing axis, folded or not — see the warning in <see cref="BakeOne"/>. The count of
        /// cells is <paramref name="facings"/>; the dirs are always the eight-facing mapping's first
        /// <paramref name="facings"/> entries.
        /// </summary>
        static RigRecipe.Axis FacingAxis(int facings, AzimuthConvention convention)
        {
            var axis = new RigRecipe.Axis { Name = "facing", Bind = "dir" };
            for (int cell = 0; cell < facings; cell++)
                axis.Values.Add(RigBaker.DirForCell(cell, GasStationKit.Facings, convention));
            return axis;
        }

        /// <summary>
        /// The render options, spelled out in full on every single call.
        ///
        /// <para><c>size</c> is the one that MUST be here: an unknown or omitted size does not throw,
        /// it resolves to the type's first entry and renders a different machine at a different cell
        /// size. The rest are spelled out because the alternative is trusting that a normalising
        /// <c>resolve()</c> stays normalising — which is precisely the assumption the fuel kit's
        /// omitted <c>wear</c> broke.</para>
        ///
        /// <para><c>grades</c> is not decoration: the rig generates hose count, sign rows and apron
        /// caps from that list, so it is a geometry input.</para>
        /// </summary>
        public static RigRecipe.OptionDict OptsOf(string size) =>
            new RigRecipe.OptionDict()
                .Set("size", size)
                .Set("grades", GasStationKit.BakedGrades.Select(x => (object)x).ToList())
                .Set("lit", GasStationKit.BakedLit)
                .Set("hoses", 0)
                .Set("sides", 0)
                .Set("out", GasStationKit.BakedOut)
                .Set("style", GasStationKit.BakedStyle)
                .Set("open", GasStationKit.BakedOpen)
                .Set("span", GasStationKit.BakedSpan)
                .Set("bollards", GasStationKit.BakedBollards)
                .Set("bin", GasStationKit.BakedBin)
                .Set("wear", GasStationKit.BakedWear)
                .Set("seed", 11)
                .Set("keyline", false)
                .Set("elev", 40);

        static string Opts(string size) => RigRecipe.Js(OptsOf(size));

        static string Label(IRigScriptHost host, in GasStationKit.Build b)
        {
            string type = b.Interior ? "store" : b.Type;
            string label = host.EvaluateString(
                $"{GasStationKit.GlobalName}.TYPES['{type}'].sizes['{b.Size}'].label");
            return b.Interior ? $"{label} — SALES FLOOR" : label;
        }

        /// <summary>
        /// Refuse a piece or size the rig does not know, BEFORE rendering.
        ///
        /// <para>⚠️ The two axes fail differently and only one of them is loud. An unknown TYPE
        /// throws. An unknown SIZE does not: <c>resolveSize</c> returns the type's first key, so
        /// <c>'sCardlock'</c> — the name the rig's OWN header comment gives the cardlock — renders a
        /// 25×52 kerb post under the cardlock's file name and reports success. The only honest check
        /// is that a size resolves to ITSELF.</para>
        /// </summary>
        static void AssertKnown(IRigScriptHost host, in GasStationKit.Build b)
        {
            string g = GasStationKit.GlobalName;
            string type = b.Interior ? "store" : b.Type;

            if (!host.EvaluateBool($"!!{g}.TYPES['{type}']"))
                throw new InvalidOperationException(
                    $"The rig has no piece '{type}'. The build table and the rig's TYPE_ORDER have " +
                    "come apart.");

            string resolved = host.EvaluateString($"{g}.resolveSize('{type}','{b.Size}')");
            if (!string.Equals(resolved, b.Size, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"'{b.Size}' is not a size of '{type}' — the rig resolves it to '{resolved}' and " +
                    $"would render THAT, at that piece's cell size, under the name {b.Key}. Sizes " +
                    "fall through silently in this rig; only this check catches it.");
        }

        // ---- the contract --------------------------------------------------------------------------

        static void WriteContract(IRigScriptHost host, BakeReport report, AzimuthConvention convention)
        {
            var contract = new GasStationKit.Contract
            {
                generated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                rigKey = GasStationKit.RigKey,
                interiorRigKey = GasStationKit.InteriorRigKey,
                globalName = GasStationKit.GlobalName,
                interiorGlobalName = GasStationKit.InteriorGlobalName,
                convention = convention.ToString(),
                pixelsPerUnit = GasStationKit.PixelsPerUnit,
                importSizeCap = GasStationKit.ImportSizeCap,
                bakedWear = GasStationKit.BakedWear,
                bakedLit = GasStationKit.BakedLit,
                bakedGrades = GasStationKit.BakedGrades.ToArray(),
                facings = GasStationKit.Facings,
                foldedFacings = GasStationKit.FoldedFacings,
                gradeIds = GasStationKit.RigGradeKeys.Select(GasStationKit.GradeId).ToArray(),
                slotPitch = (float)host.EvaluateNumber($"{GasStationKit.GlobalName}.SLOT_PITCH"),
                sheets = report.Sheets.Select(s => new GasStationKit.SheetEntry
                {
                    key = s.Key, type = s.Type, size = s.Size, label = s.Label,
                    interior = s.Interior,
                    wear = GasStationKit.BakedWear,
                    lit = GasStationKit.BakedLit,
                    grades = GasStationKit.BakedGrades.ToArray(),
                    facings = s.Facings, fold180 = s.Fold180,
                    cellW = s.CellW, cellH = s.CellH,
                    pivotX = s.PivotX, pivotY = s.PivotY,
                    columns = s.Columns, rows = s.Rows,
                    sheetW = s.SheetW, sheetH = s.SheetH,
                    nativeW = s.NativeW, nativeH = s.NativeH,
                    pivotInsideInk = s.PivotInsideInk,
                }).ToArray(),
            };

            string abs = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
                                      GasStationKit.ContractPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            File.WriteAllText(abs, JsonUtility.ToJson(contract, prettyPrint: true));
        }

        static void WritePng(Color32[] pixels, int w, int h, SheetResult result)
        {
            string abs = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
                                      result.AssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(abs, png);
                result.PngBytes = png.Length;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
