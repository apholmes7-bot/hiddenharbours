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
    /// Bakes the dooryard kit's boundary family: one sheet per row of <see cref="YardKit.Builds"/>, laid
    /// out as <b>columns = facings, one row</b>.
    ///
    /// <para><b>Why its own baker rather than joining <see cref="IsoPropSheetBaker"/>.</b> Everything
    /// that baker checks about a family holds here — the fixed 470×540 native buffer, the
    /// <c>render(key, dir, opts)</c> call, the bare RGBA return, the pivot-inclusive ink union — and
    /// <see cref="IsoPropSheetBaker.MeasureCell"/> IS the cell rule used below, reused rather than
    /// restated. What this kit cannot pass is <c>IsoPackContract.AssertKeylineGated</c>: the yard rig
    /// exposes no <c>KEYLINE_DEFAULT</c>, and that gate is the owner's 2026-08-06 ruling about the four
    /// ISO-PACK rigs, not a universal one (ADR 0031 leaves every other family its ring until its own
    /// redo). Baking here keeps that ruling exactly as narrow as it was made.</para>
    ///
    /// <para><b>The guard that matters most is the facing table, not a missing key.</b>
    /// <see cref="YardRegistrationProbe"/> runs before the first pixel and refuses on a handedness
    /// disagreement — because this kit's own sidecar publishes a MIRRORED facing list, and a bake that
    /// took it at its word would write eight good cells in the wrong order and nothing downstream would
    /// notice.</para>
    /// </summary>
    public static class YardSheetBaker
    {
        /// <summary>The line a headless run greps for. An exit code says the editor closed, not that the
        /// bake ran — a fresh worktree's first <c>-executeMethod</c> is skipped entirely and still exits
        /// 0.</summary>
        public const string CompletionLine = "[YardSheetBaker] BAKE COMPLETE";

        public sealed class SheetResult
        {
            /// <summary>The row this sheet was baked FROM, carried rather than looked back up by key —
            /// <see cref="BakeAll"/> takes an arbitrary build list, and a key lookup into the static
            /// table would write a contract full of the wrong dressing beside the right pixels.</summary>
            public YardKit.Build Build;

            public string Key, Piece, Label, Cat, AssetPath, OptionsJs;
            public int NativeW, NativeH, NativePivotX, NativePivotY;
            public int CellW, CellH, PivotX, PivotY, CropX, CropY;
            public int Columns, Rows, SheetW, SheetH, Facings, PngBytes;
            public bool PivotInsideInk;
            public float FootprintW, FootprintD, HeightM;
            public double RenderMilliseconds;

            public int CroppedPixels => SheetW * SheetH;

            public override string ToString() =>
                $"{Key}: {FootprintW:0.00}×{FootprintD:0.00} m, cell {CellW}×{CellH} pivot " +
                $"{PivotX},{PivotY} → {Columns}×{Rows} sheet {SheetW}×{SheetH} ({PngBytes:N0} B)";
        }

        public sealed class BakeReport
        {
            public YardRegistrationProbe.Report Probe;
            public readonly List<SheetResult> Sheets = new List<SheetResult>();
            public double TotalMilliseconds;
            public string EngineName;

            public long TotalPixels => Sheets.Sum(s => (long)s.CroppedPixels);
            public long TotalPngBytes => Sheets.Sum(s => (long)s.PngBytes);

            /// <summary>Runtime cost at RGBA32, which is what the import settings lock — the number the
            /// performance budget actually pays (CLAUDE.md rule 7), not the PNG size on disk.</summary>
            public double RuntimeMegabytes => TotalPixels * 4.0 / (1024 * 1024);
        }

        public static BakeReport BakeAll(IReadOnlyList<YardKit.Build> builds = null)
        {
            builds ??= YardKit.Builds;
            if (builds.Count == 0)
                throw new InvalidOperationException(
                    "YardKit.Builds is empty, so this bake would write nothing and report success.");

            var total = Stopwatch.StartNew();

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var report = new BakeReport { EngineName = host.EngineName };

            var entry = RigCatalog.Get(YardKit.RigKey);
            if (entry.GlobalName != YardKit.GlobalName)
                throw new InvalidOperationException(
                    $"The catalog installs '{entry.GlobalName}' for key '{YardKit.RigKey}' but this kit " +
                    $"reads '{YardKit.GlobalName}'.");

            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            AssertRigShape(host, g, geo);

            // ---- MEASURE the facing table before a pixel is written -----------------------------
            report.Probe = YardRegistrationProbe.Measure(host, g, YardKit.Facings,
                                                         entry.DeclaredConvention, YardKit.Elevation);

            foreach (var b in builds)
                report.Sheets.Add(BakeOne(host, g, b, geo, entry.DeclaredConvention));

            WriteContract(host, g, report, entry);

            total.Stop();
            report.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return report;
        }

        // ---- the standing shape checks ------------------------------------------------------------

        /// <summary>
        /// What this baker believes about the rig, asserted rather than assumed. Each of these is a
        /// number restated in <see cref="YardKit"/> for the consumers' benefit; this is where the two
        /// are held together, so a drop that moves one fails the bake instead of shifting every sprite.
        /// </summary>
        static void AssertRigShape(IRigScriptHost host, string g, in RigGeometry geo)
        {
            int ppu = (int)host.EvaluateNumber($"{g}.PX");
            if (ppu != YardKit.PixelsPerUnit)
                throw new InvalidOperationException(
                    $"{g}.PX is {ppu} but YardKit.PixelsPerUnit says {YardKit.PixelsPerUnit}. The " +
                    "importer locks the sprite's pixels-per-unit to the kit's number, so a mismatch " +
                    "scales every placed piece against the metres the world is measured in.");

            int order = (int)host.EvaluateNumber($"{g}.order.length");
            if (order != YardKit.Facings)
                throw new InvalidOperationException(
                    $"{g}.order carries {order} entries but this kit bakes {YardKit.Facings} facings. " +
                    "One of the two is stale.");

            // The pivot is the ground centre of the footprint — MEASURED on this kit's intake as exactly
            // (235,442) in cell px, which is what lets a placed piece take the boundary point as its
            // position with no offset arithmetic. Re-asserted because the whole placement depends on it.
            if (geo.Width <= 0 || geo.Height <= 0 || geo.PivotX <= 0 || geo.PivotY <= 0)
                throw new InvalidOperationException(
                    $"{g} reports a {geo.Width}×{geo.Height} buffer with pivot " +
                    $"({geo.PivotX},{geo.PivotY}). All four must be positive.");
        }

        /// <summary>
        /// Refuse a piece the rig does not know, BEFORE rendering.
        ///
        /// <para>⚠️ <c>render()</c> falls back to <c>PROPS.picketPanel</c> for any name it does not
        /// know — so an unknown key does not draw nothing and does not throw. It draws a PICKET FENCE,
        /// under another piece's file name, sliced into the right number of sprites. That is the worst
        /// of the three failure shapes a kit can have, because every downstream count is correct.</para>
        /// </summary>
        public static void AssertKnown(IRigScriptHost host, string g, string piece)
        {
            if (host.EvaluateBool($"typeof {g}.PROPS[{Quote(piece)}] === 'object'")) return;

            throw new InvalidOperationException(
                $"'{piece}' is not a piece this rig draws. Known: " +
                $"{host.EvaluateString($"{g}.list().join(', ')")}.\n" +
                "render() does not throw on an unknown name — it falls back to PROPS.picketPanel, so " +
                "this would bake a picket fence under another piece's file name.");
        }

        // ---- one sheet ------------------------------------------------------------------------------

        static SheetResult BakeOne(IRigScriptHost host, string g, in YardKit.Build b,
                                   in RigGeometry geo, AzimuthConvention convention)
        {
            AssertKnown(host, g, b.Piece);

            int facings = YardKit.Facings;
            string opts = YardKit.OptionsJs(b);
            string key = Quote(b.Piece);

            int nw = geo.Width, nh = geo.Height;
            int npx = Mathf.RoundToInt((float)geo.PivotX), npy = Mathf.RoundToInt((float)geo.PivotY);

            // ---- render every facing --------------------------------------------------------------
            var clock = new Stopwatch();
            int expected = nw * nh * 4;
            var frames = new byte[facings][];

            for (int cell = 0; cell < facings; cell++)
            {
                // DirForCell carries the counter-clockwise correction the catalog entry declares and
                // YardRegistrationProbe has just re-measured, so what lands on disk is genuinely
                // clockwise: cell k faces 45k° from north.
                double dir = RigBaker.DirForCell(cell, facings, convention);

                clock.Start();
                frames[cell] = host.EvaluateBytes($"{g}.render({key},{Num(dir)},{opts})");
                clock.Stop();

                if (frames[cell] == null || frames[cell].Length != expected)
                    throw new InvalidOperationException(
                        $"{g}.render({key},{Num(dir)},…) came back " +
                        $"{(frames[cell]?.Length.ToString() ?? "null")} bytes, expected {expected} for a " +
                        $"{nw}×{nh} RGBA buffer. This rig returns a BARE RGBA array; a wrapper means its " +
                        "return shape changed.");
            }

            // ---- crop: pivot-inclusive ink union across EVERY facing -------------------------------
            // One cell for all eight, so the rows slice into identical rects and the piece does not hop
            // as it turns. Seeded at the pivot, which is what keeps a piece whose ink does not span its
            // own ground point (a hedge top, a gate leaf hung high) inside its own cell.
            if (!IsoPropSheetBaker.MeasureCell(frames, nw, nh, npx, npy,
                                               out int cw, out int ch, out int pivotX, out int pivotY,
                                               out int left, out int top, out bool pivotInsideInk))
                throw new InvalidOperationException(
                    $"Every facing of '{b.Key}' rendered fully transparent. The piece name resolved " +
                    "(AssertKnown passed) and it still drew no pixels — refusing rather than writing an " +
                    "empty sheet. The likeliest cause is an option value the rig does not recognise.");

            int cropX = npx + left, cropY = npy + top;

            // ---- pack: columns are facings, one row -------------------------------------------------
            int cols = facings, rows = 1;
            int pw = cols * cw, ph = rows * ch;

            if (pw > YardKit.ImportSizeCap || ph > YardKit.ImportSizeCap)
                throw new InvalidOperationException(
                    $"'{b.Key}' packs to {pw}×{ph}, past this kit's {YardKit.ImportSizeCap} px import " +
                    "cap. A sheet between the cap and Unity's hard 4096 BAKES FINE and then imports " +
                    "SILENTLY DOWNSCALED with the sprite count still correct, which poisons every slice " +
                    "rect. The longest panel this table bakes is about 3 m against a 470 px native " +
                    "buffer, so a sheet this size means the rig or the len dial changed.");

            var pixels = new Color32[pw * ph];
            for (int cell = 0; cell < cols; cell++)
                BuildingRigBaker.BlitCropped(frames[cell], nw, nh, cropX, cropY, cw, ch,
                                             pixels, pw, ph, col: cell, rowFromTop: 0);

            // ---- the piece's own metres, at the BAKED len -------------------------------------------
            // ⚠️ Read from the rig at the exact options this sheet was drawn with, never from the
            // sidecar's size[] samples: those are tabulated at len 0, 0.4 and 1 only, so any other dial
            // would be interpolated and the placer's panels would drift out of step with their pixels.
            double footW = host.EvaluateNumber($"{g}.footprint({key},{opts}).w");
            double footD = host.EvaluateNumber($"{g}.footprint({key},{opts}).d");
            double heightM = host.EvaluateNumber($"{g}.height({key},{opts})");

            if (footW <= 0 || footD <= 0)
                throw new InvalidOperationException(
                    $"{g}.footprint({key},…) returned {footW:F3}×{footD:F3} m. The placer TILES on the " +
                    "width; a non-positive one would divide a run into an unbounded number of panels.");

            var result = new SheetResult
            {
                Build = b,
                Key = b.Key,
                Piece = b.Piece,
                Label = host.EvaluateString($"{g}.PROPS[{key}].label"),
                Cat = host.EvaluateString($"{g}.PROPS[{key}].cat"),
                OptionsJs = opts,
                NativeW = nw, NativeH = nh, NativePivotX = npx, NativePivotY = npy,
                CellW = cw, CellH = ch, PivotX = pivotX, PivotY = pivotY,
                CropX = cropX, CropY = cropY, PivotInsideInk = pivotInsideInk,
                Columns = cols, Rows = rows, SheetW = pw, SheetH = ph, Facings = facings,
                FootprintW = (float)footW, FootprintD = (float)footD, HeightM = (float)heightM,
                RenderMilliseconds = clock.Elapsed.TotalMilliseconds,
                AssetPath = YardKit.SheetPath(b.Key),
            };

            WritePng(pixels, pw, ph, result);
            return result;
        }

        // ---- the contract ---------------------------------------------------------------------------

        static void WriteContract(IRigScriptHost host, string g, BakeReport report, in RigEntry entry)
        {
            var first = report.Sheets.Count > 0 ? report.Sheets[0] : null;

            var contract = new YardKit.Contract
            {
                note = "Baked by Hidden Harbours ▸ Art ▸ Bake Yard Kit. The dooryard kit's BOUNDARY " +
                       "family — one sheet per piece, columns are facings. The other 52 pieces of the " +
                       "kit are dressing and are not baked yet.",
                generated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                rigKey = YardKit.RigKey,
                globalName = YardKit.GlobalName,
                convention = entry.DeclaredConvention.ToString(),
                conventionNote =
                    "MEASURED, not read. The rig's own dir frame steps " +
                    $"{StepOf(report.Probe?.RigDirFaceBearings):+0.000;-0.000}°/dir on the un-squashed " +
                    "ground plane — COUNTER-clockwise — while the kit's gameplay sidecar publishes " +
                    "contract.facings = [N,NE,E,SE,S,SW,W,NW], the clockwise list, against that same " +
                    "index. They agree only at dir 0 and dir 4. RigBaker.DirForCell applies the " +
                    "correction, so the CELLS on these sheets are a plain clockwise compass — cell k " +
                    "faces 45k° from north — which is what cellFaceBearings below records. ⚠️ Read the " +
                    "sidecar's facings list as documentation only; it is not a placement table. See " +
                    "docs/art/rigs/yard-landscaping-kit/IMPORT.md §6.",
                pivotNote =
                    "The pivot is the piece's GROUND CENTRE — the rig places it at model (0,0) and the " +
                    "projection of the origin does not move with the camera (measured: " +
                    $"{report.Probe?.PivotDeviationPx ?? 0:F4} px over all facings). So a placed piece " +
                    "takes the boundary point as its position with no offset arithmetic. Normalised " +
                    "bottom-origin y is (cellH − pivotY)/cellH per ADR 0026. ⚠️ NOT bottom-centre: " +
                    "under the ¾ camera the near half of the footprint projects BELOW the ground " +
                    "centre, so a bottom-centre pivot sinks the piece by that much, silently.",

                ppu = YardKit.PixelsPerUnit,
                facings = YardKit.Facings,
                importSizeCap = YardKit.ImportSizeCap,
                nativeCellW = first?.NativeW ?? 0,
                nativeCellH = first?.NativeH ?? 0,
                nativePivotX = first?.NativePivotX ?? 0,
                nativePivotY = first?.NativePivotY ?? 0,

                phase = YardKit.BakedPhase,
                snow = YardKit.BakedSnow,
                weather = YardKit.BakedWeather,
                elev = (float)YardKit.Elevation,
                night = YardKit.BakedNight,
                compose = YardKit.BakedCompose,

                cellFaceBearings = ToFloats(report.Probe?.CellFaceBearings),
                cellRunBearings = ToFloats(report.Probe?.CellRunBearings),

                pieces = report.Sheets.Select(s => new YardKit.Entry
                {
                    key = s.Key,
                    piece = s.Piece,
                    label = s.Label,
                    cat = s.Cat,
                    role = s.Build.Part.ToString(),
                    reason = s.Build.Reason,
                    optionsJs = s.OptionsJs,
                    paint = s.Build.Paint,
                    variant = s.Build.Variant,
                    seed = s.Build.Seed,
                    len = s.Build.Len,
                    kept = s.Build.Kept,
                    sheet = Path.GetFileName(s.AssetPath),
                    facings = s.Facings, columns = s.Columns, rows = s.Rows,
                    cellW = s.CellW, cellH = s.CellH,
                    cropX = s.CropX, cropY = s.CropY,
                    pivotX = s.PivotX, pivotY = s.PivotY,
                    sheetW = s.SheetW, sheetH = s.SheetH,
                    pivotInsideInk = s.PivotInsideInk,
                    footprintWidthMetres = s.FootprintW,
                    footprintDepthMetres = s.FootprintD,
                    heightMetres = s.HeightM,
                    pngBytes = s.PngBytes,
                    runtimeBytesRgba32 = (long)s.SheetW * s.SheetH * 4,
                }).ToArray(),
            };

            string abs = Path.Combine(RigCatalog.RepoRoot, YardKit.ContractPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            File.WriteAllText(abs, JsonUtility.ToJson(contract, prettyPrint: true));
        }

        static float[] ToFloats(double[] source)
        {
            if (source == null) return Array.Empty<float>();
            var f = new float[source.Length];
            for (int i = 0; i < source.Length; i++) f[i] = (float)source[i];
            return f;
        }

        static double StepOf(double[] bearings)
        {
            if (bearings == null || bearings.Length < 2) return 0;
            double sum = 0;
            for (int i = 1; i < bearings.Length; i++)
            {
                double d = (bearings[i] - bearings[i - 1]) % 360.0;
                if (d > 180.0) d -= 360.0;
                if (d <= -180.0) d += 360.0;
                sum += d;
            }
            return sum / (bearings.Length - 1);
        }

        // ---- write ---------------------------------------------------------------------------------

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

        static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        static string Quote(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    }
}
