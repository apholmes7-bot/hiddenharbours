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
    /// Bakes the shop kit's FIXTURE family: one sheet per row of <see cref="ShopFixtureKit.Builds"/>,
    /// laid out as <b>columns = facings, one row</b>.
    ///
    /// <para>Why this needs its own baker rather than joining <see cref="ShopLevelBaker"/>: that one
    /// bakes buildings through <c>render()</c> and <c>ShopBuilding.sheet()</c>, whose cell is per trade
    /// and per level. A fixture comes out of <c>renderItem()</c>, which always draws into the rig's
    /// fixed 1180 × 900 native buffer no matter how small the prop is — so the entire cell is decided
    /// by the union crop, and a 0.42 m bar stool and a 3.2 m bar counter arrive in the same envelope.</para>
    ///
    /// <para><b>The one guard that matters most.</b> <c>renderItem</c> with a name the rig does not know
    /// returns a full-size, fully transparent buffer and does not throw. Baked, that is a valid sheet of
    /// nothing: right dimensions, right sprite count once sliced, no ink and no error anywhere.
    /// <see cref="AssertKnown"/> runs before the first render for that reason.</para>
    ///
    /// <para><b>Load order.</b> Only <c>shopInterior</c> is installed — MEASURED sufficient, not
    /// assumed. The kit's three documented load-order divergences are real (<c>dims()</c> really does
    /// return the whole shell without <c>ShopBuilding</c>) and they reach <c>render()</c>, not
    /// <c>renderItem()</c>, which never builds a shell.
    /// <c>ShopFixtureRegistrationProbe.MeasureLoadOrderInvariance</c> pins that, and goes red the day a
    /// drop makes a fixture read its room.</para>
    /// </summary>
    public static class ShopFixtureSheetBaker
    {
        /// <summary>The line a headless run greps for. An exit code says the editor closed, not that
        /// the bake ran — a fresh worktree's first <c>-executeMethod</c> is skipped entirely and still
        /// exits 0.</summary>
        public const string CompletionLine = "[ShopFixtureSheetBaker] BAKE COMPLETE";

        public sealed class SheetResult
        {
            /// <summary>The row this sheet was baked FROM, carried rather than looked back up by key.
            /// <c>BakeAll</c> takes an arbitrary build list, so a key-based lookup into the static table
            /// would miss for a caller-supplied row and write a contract full of default dressing beside
            /// a sheet baked with the real thing — wrong, and silent.</summary>
            public ShopFixtureKit.Build Build;

            public string Key, Fixture, Trade, AssetPath, OptionsJs;
            public int NativeW, NativeH, CellW, CellH, PivotX, PivotY, CropX, CropY;
            public int Columns, Rows, SheetW, SheetH, Facings, PngBytes;
            public bool PivotInsideInk;
            public float FootprintW, FootprintD;
            public float[] FrontX, FrontY;
            public double RenderMilliseconds;

            public int CroppedPixels => SheetW * SheetH;

            public override string ToString() =>
                $"{Key}: cell {CellW}×{CellH} pivot {PivotX},{PivotY} → {Columns}×{Rows} " +
                $"sheet {SheetW}×{SheetH} ({PngBytes:N0} B)";
        }

        public sealed class BakeReport
        {
            public ShopFixtureRegistrationProbe.Report Probe;
            public readonly List<SheetResult> Sheets = new();
            public double TotalMilliseconds;
            public string EngineName;

            public long TotalPixels => Sheets.Sum(s => (long)s.CroppedPixels);
            public long TotalPngBytes => Sheets.Sum(s => (long)s.PngBytes);

            /// <summary>Runtime cost at RGBA32, which is what the import settings lock — the number the
            /// performance budget actually pays (CLAUDE.md rule 7), not the PNG size on disk.</summary>
            public double RuntimeMegabytes => TotalPixels * 4.0 / (1024 * 1024);
        }

        public static BakeReport BakeAll(IReadOnlyList<ShopFixtureKit.Build> builds = null)
        {
            builds ??= ShopFixtureKit.Builds;
            var total = Stopwatch.StartNew();

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var report = new BakeReport { EngineName = host.EngineName };

            var entry = RigCatalog.Get(ShopFixtureKit.RigKey);
            RigCatalog.InstallModule(host, entry);

            if (entry.GlobalName != ShopFixtureKit.GlobalName)
                throw new InvalidOperationException(
                    $"The catalog installs '{entry.GlobalName}' for key '{ShopFixtureKit.RigKey}' but " +
                    $"this kit reads '{ShopFixtureKit.GlobalName}'.");

            string g = entry.GlobalName;

            // ---- measure before baking ----------------------------------------------------------
            // The first build's fixture is the probe subject: handedness is a property of the
            // turntable, but the service face is read off a real fixture's own footprint.
            if (builds.Count == 0)
                throw new InvalidOperationException(
                    "ShopFixtureKit.Builds is empty, so this bake would write nothing and report success.");

            report.Probe = ShopFixtureRegistrationProbe.Measure(
                host, g, builds[0].Fixture, builds[0].Trade, builds[0].Size,
                ShopFixtureKit.Facings, entry.DeclaredConvention);

            foreach (var b in builds)
                report.Sheets.Add(BakeOne(host, g, b, entry.DeclaredConvention));

            WriteContract(host, g, report, entry);

            total.Stop();
            report.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return report;
        }

        // ---- one sheet ------------------------------------------------------------------------

        static SheetResult BakeOne(IRigScriptHost host, string g, in ShopFixtureKit.Build b,
                                   AzimuthConvention convention)
        {
            AssertKnown(host, g, b);

            int facings = ShopFixtureKit.Facings;
            string optionsJs = ShopFixtureRegistrationProbe.Opts(b.Trade, b.Size, ShopFixtureKit.BakedRot);

            // The native buffer is a rig constant for renderItem — it resets the view every call — but
            // it is READ rather than restated, because a drop that resizes it must move the crop with it.
            int nw = (int)host.EvaluateNumber($"{g}.W");
            int nh = (int)host.EvaluateNumber($"{g}.H");
            int npx = Mathf.RoundToInt((float)host.EvaluateNumber($"{g}.pivot.x"));
            int npy = Mathf.RoundToInt((float)host.EvaluateNumber($"{g}.pivot.y"));

            // ---- render every facing -------------------------------------------------------------
            var clock = new Stopwatch();
            int expected = nw * nh * 4;
            var frames = new byte[facings][];

            for (int cell = 0; cell < facings; cell++)
            {
                double dir = RigBaker.DirForCell(cell, facings, convention);

                clock.Start();
                frames[cell] = host.EvaluateBytes(
                    $"{g}.renderItem('{b.Fixture}',{Num(dir)},{optionsJs})");
                clock.Stop();

                if (frames[cell] == null || frames[cell].Length != expected)
                    throw new InvalidOperationException(
                        $"{g}.renderItem('{b.Fixture}',{Num(dir)},…) came back " +
                        $"{(frames[cell]?.Length.ToString() ?? "null")} bytes, expected {expected} for a " +
                        $"{nw}×{nh} RGBA buffer. This rig returns a BARE RGBA array; a wrapper means its " +
                        "return shape changed.");
            }

            // ---- crop: pivot-inclusive ink union across EVERY facing -------------------------------
            // One cell for all eight, so the rows slice into identical rects and the fixture does not
            // hop as it turns — the same rule every kit in this folder bakes to.
            if (!IsoPropSheetBaker.MeasureCell(frames, nw, nh, npx, npy,
                                               out int cw, out int ch, out int pivotX, out int pivotY,
                                               out int left, out int top, out bool pivotInsideInk))
                throw new InvalidOperationException(
                    $"Every facing of {b.Key} rendered fully transparent. The fixture name resolved " +
                    "(AssertKnown passed) and it still drew no pixels — refusing rather than writing an " +
                    "empty sheet, which is exactly what an unknown name produces.");

            int cropX = npx + left, cropY = npy + top;

            // ---- pack: columns are facings, one row -----------------------------------------------
            int cols = facings, rows = 1;
            int pw = cols * cw, ph = rows * ch;

            if (pw > ShopFixtureKit.ImportSizeCap || ph > ShopFixtureKit.ImportSizeCap)
                throw new InvalidOperationException(
                    $"{b.Key} packs to {pw}×{ph}, past this kit's {ShopFixtureKit.ImportSizeCap} px " +
                    "import cap. A sheet between the cap and Unity's hard 4096 BAKES FINE and then " +
                    "imports SILENTLY DOWNSCALED with the sprite count still correct, which poisons " +
                    "every slice rect. Measured, the largest fixture the rig draws is the bar at " +
                    "864×123 — so a sheet this size means the rig changed, not that the cap is tight.");

            var pixels = new Color32[pw * ph];
            for (int cell = 0; cell < cols; cell++)
                BuildingRigBaker.BlitCropped(frames[cell], nw, nh, cropX, cropY, cw, ch,
                                             pixels, pw, ph, col: cell, rowFromTop: 0);

            // ---- the service face, per baked cell, in cropped-cell px ------------------------------
            // Read from the rig's own projection of the fixture's own footprint. Same shape and
            // semantics as ShopKit's door anchors, so BuildingFacing aims a counter with the code that
            // already aims a door.
            double depth = host.EvaluateNumber($"{g}.fixtureFoot('{b.Fixture}',0).d");
            double width = host.EvaluateNumber($"{g}.fixtureFoot('{b.Fixture}',0).w");
            var frontX = new float[facings];
            var frontY = new float[facings];
            for (int cell = 0; cell < facings; cell++)
            {
                double dir = RigBaker.DirForCell(cell, facings, convention);
                string p = $"{g}.project({Num(dir)},[0,{Num(depth / 2.0)},0],{Num(ShopFixtureKit.Elevation)})";
                frontX[cell] = (float)(host.EvaluateNumber($"{p}.x") - cropX);
                frontY[cell] = (float)(host.EvaluateNumber($"{p}.y") - cropY);
            }

            var result = new SheetResult
            {
                Build = b,
                Key = b.Key, Fixture = b.Fixture, Trade = b.Trade, OptionsJs = optionsJs,
                NativeW = nw, NativeH = nh,
                CellW = cw, CellH = ch, PivotX = pivotX, PivotY = pivotY,
                CropX = cropX, CropY = cropY, PivotInsideInk = pivotInsideInk,
                Columns = cols, Rows = rows, SheetW = pw, SheetH = ph, Facings = facings,
                FootprintW = (float)width, FootprintD = (float)depth,
                FrontX = frontX, FrontY = frontY,
                RenderMilliseconds = clock.Elapsed.TotalMilliseconds,
                AssetPath = ShopFixtureKit.SheetPath(b.Key),
            };

            WritePng(pixels, pw, ph, result);
            return result;
        }

        /// <summary>
        /// Refuse a fixture or a trade the rig does not know, BEFORE rendering.
        ///
        /// <para>⚠️ Both fall through silently and they fail in different ways. An unknown FIXTURE draws
        /// nothing at all — <c>placeItem</c> opens <c>if(!P) return;</c> — so the sheet is a valid
        /// rectangle of transparency that slices into the right number of empty sprites. An unknown
        /// TRADE is worse, because it produces a real, plausible picture: <c>resolve()</c> falls through
        /// to <c>generalStore</c>, so a misspelled trade bakes the general store's counter under the
        /// restaurant's file name and every downstream check passes.</para>
        /// </summary>
        public static void AssertKnown(IRigScriptHost host, string g, in ShopFixtureKit.Build b)
        {
            if (!host.EvaluateBool($"typeof {g}.FIXTURES['{b.Fixture}'] === 'object'"))
                throw new InvalidOperationException(
                    $"'{b.Fixture}' is not a fixture this rig draws. Known: " +
                    $"{host.EvaluateString($"{g}.list().join(', ')")}.\n" +
                    "renderItem does not throw on an unknown name — it returns a full-size, fully " +
                    "TRANSPARENT buffer, which bakes into a valid sheet of nothing.");

            if (!host.EvaluateBool($"typeof {g}.TYPES['{b.Trade}'] === 'object'"))
                throw new InvalidOperationException(
                    $"'{b.Trade}' is not a trade this rig knows. Known: " +
                    $"{host.EvaluateString($"Object.keys({g}.TYPES).join(', ')")}.\n" +
                    "resolve() substitutes 'generalStore' for anything it does not recognise, so this " +
                    "would bake the general store's fixture under another trade's file name.");
        }

        // ---- the contract ----------------------------------------------------------------------

        static void WriteContract(IRigScriptHost host, string g, BakeReport report, in RigEntry entry)
        {
            var contract = new ShopFixtureKit.Contract
            {
                note = "Baked by Hidden Harbours ▸ Art ▸ Bake Shop Fixtures. Standalone fixtures from " +
                       "ShopInterior.renderItem() — one sheet per fixture, columns are facings.",
                generated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                rigKey = ShopFixtureKit.RigKey,
                globalName = ShopFixtureKit.GlobalName,
                convention = report.Probe.MeasuredInCellFrame.ToString(),
                conventionNote =
                    $"MEASURED in the BAKED-CELL frame: the service face steps {report.Probe.CellStepDeg:+0.0;-0.0}° " +
                    "per cell on the un-squashed ground plane. That is what entitles BuildingFacing to " +
                    "derive its bearing table as `atZero − 360/facings × cell`. The rig's own dir frame " +
                    "turns the other way; RigBaker.DirForCell is the correction, and two kits that " +
                    "apply different ones produce mirrored sheets while agreeing perfectly on paper.",
                pivotNote =
                    "The pivot is the fixture's GROUND CENTRE — renderItem places it at world (0,0), and " +
                    "the projection of the origin does not move with the camera (measured: " +
                    $"{report.Probe.PivotDeviationPx:F4} px over all facings). Normalised bottom-origin " +
                    "y is (cellH − pivotY)/cellH per ADR 0026. ⚠️ NOT bottom-centre: under the ¾ camera " +
                    "the near half of the footprint projects BELOW the ground centre, so a bottom-centre " +
                    "pivot sinks the fixture by that much, silently.",
                ppu = ShopFixtureKit.PixelsPerUnit,
                facings = ShopFixtureKit.Facings,
                importSizeCap = ShopFixtureKit.ImportSizeCap,
                nativeCellW = report.Sheets.Count > 0 ? report.Sheets[0].NativeW : 0,
                nativeCellH = report.Sheets.Count > 0 ? report.Sheets[0].NativeH : 0,
                fixtures = report.Sheets.Select(s =>
                {
                    var b = s.Build;
                    return new ShopFixtureKit.Entry
                    {
                        key = s.Key, fixture = s.Fixture, trade = s.Trade,
                        label = host.EvaluateString($"{g}.FIXTURES['{s.Fixture}'].label"),
                        cat = host.EvaluateString($"{g}.FIXTURES['{s.Fixture}'].cat"),
                        size = (float)b.Size,
                        optionsJs = s.OptionsJs,
                        stock = b.Stock, weather = b.Weather, night = b.Night, seed = b.Seed,
                        rot = ShopFixtureKit.BakedRot, elev = (float)ShopFixtureKit.Elevation,
                        sheet = Path.GetFileName(s.AssetPath),
                        facings = s.Facings, columns = s.Columns, rows = s.Rows,
                        cellW = s.CellW, cellH = s.CellH,
                        cropX = s.CropX, cropY = s.CropY,
                        pivotX = s.PivotX, pivotY = s.PivotY,
                        sheetW = s.SheetW, sheetH = s.SheetH,
                        pivotInsideInk = s.PivotInsideInk,
                        footprintWidthMetres = s.FootprintW, footprintDepthMetres = s.FootprintD,
                        frontX = s.FrontX, frontY = s.FrontY,
                        pngBytes = s.PngBytes,
                        runtimeBytesRgba32 = (long)s.SheetW * s.SheetH * 4,
                    };
                }).ToArray(),
            };

            string abs = Path.Combine(RigCatalog.RepoRoot, ShopFixtureKit.ContractPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            File.WriteAllText(abs, JsonUtility.ToJson(contract, prettyPrint: true));
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

        static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    }
}
