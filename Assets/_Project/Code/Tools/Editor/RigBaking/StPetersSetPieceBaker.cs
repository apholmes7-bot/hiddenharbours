using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HiddenHarbours.Art.Editor;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Bakes the eight St Peters set pieces at their TODAY options (the key scenes' today variant, held
    /// in <see cref="SetPieceSheetSlicer.TodayPath"/>), eight facings each and the vane's sixteen
    /// headings, into one sheet per piece under <see cref="SetPieceSheetSlicer.SheetFolder"/>.
    ///
    /// <para><b>Refuses, never guesses.</b> Before a pixel is written it proves the kit still writes the
    /// today file's gameplay block for every piece, that the bake sky lights no lamp, and that every
    /// render's cell is the one the gameplay block names. Any disagreement throws.</para>
    ///
    /// <para><b>The lattice.</b> A frame's box is its ink plus its own pivot; every cell on a sheet is the
    /// largest box, and the grid is the largest divisor of the cell count that fits the 2048 cap on both
    /// sides (<see cref="NavBuoyKit.PlanSheet"/>). A piece that does not fit throws: that is a stop, not a
    /// re-pack.</para>
    ///
    /// <para><b>Restored variants are not baked.</b> The today file lists them under <c>leftOut</c>: the
    /// restored boiler, trolley line, trolley and conveyor, the smoke plume and the purchase hook all land
    /// with the restoration work, not here.</para>
    /// </summary>
    public static class StPetersSetPieceBaker
    {
        /// <summary>
        /// The one render call, defined once in the host so the heading formula (step·360/steps) and the
        /// option layering (bake sky and lamp under the piece's options) cannot drift between cells.
        /// Returns the rig's own cell and whether it is the one the gameplay block names.
        /// </summary>
        const string RenderFunction =
            "globalThis.__spRender = function (i, dir, step) {\n" +
            "  var P = __spToday.pieces[i], e = {};\n" +
            "  if (step >= 0) e[P.gameplay.animated.input] = step * 360 / P.gameplay.animated.steps;\n" +
            "  var o = Object.assign({ sky: __spSky, lit: __spToday.bake.lit, outline: __spToday.bake.outline }, P.options, e);\n" +
            "  var r = " + StPetersSetPieceKit.GlobalName + ".render(P.key, dir, o);\n" +
            "  var c = step >= 0\n" +
            "    ? " + StPetersSetPieceKit.GlobalName + ".gameplay(P.key, Object.assign({}, P.options, e)).cells[dir]\n" +
            "    : P.gameplay.cells[dir];\n" +
            "  return { w: r.w, h: r.h, px: r.px, py: r.py, rgba: r.rgba,\n" +
            "           cellOk: c.dir === dir && c.w === r.w && c.h === r.h && c.pivot[0] === r.px && c.pivot[1] === r.py };\n" +
            "};";

        [MenuItem("Hidden Harbours/Art/Bake St Peters Set Pieces (today)", priority = 73)]
        public static void BakeMenu()
        {
            var problems = BakeSliceBuild();
            if (problems.Count > 0)
                Debug.LogError($"[StPetersSetPieceBaker] {problems.Count} problem(s):\n  " + string.Join("\n  ", problems));
            else
                Debug.Log("[StPetersSetPieceBaker] Baked, sliced and built all eight set pieces.");
        }

        /// <summary>Headless entry (-executeMethod): bake, import, slice, build; exit 1 on ANY problem.</summary>
        public static void BakeSliceBuildFromCommandLine()
        {
            int code = 1;
            try
            {
                var problems = BakeSliceBuild();
                foreach (string p in problems) Debug.LogError("[StPetersSetPieceBaker] " + p);
                code = problems.Count == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StPetersSetPieceBaker] headless bake failed: {ex}");
            }
            EditorApplication.Exit(code);
        }

        /// <summary>
        /// The whole Phase B chain. Importer work happens OUTSIDE StartAssetEditing and synchronously, so
        /// the slicer reads the sheets it was just handed rather than a deferred import.
        /// </summary>
        public static List<string> BakeSliceBuild()
        {
            var problems = new List<string>();
            var contract = Bake(out string log);
            Debug.Log(log);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var piece in contract.pieces)
                AssetDatabase.ImportAsset(SetPieceSheetSlicer.SheetPath(piece.key),
                                          ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SetPieceSheetSlicer.ContractPath, ImportAssetOptions.ForceSynchronousImport);

            int sliced = SetPieceSheetSlicer.SliceAll(problems);
            if (sliced != contract.pieces.Count)
                problems.Add($"sliced {sliced} of {contract.pieces.Count} sheets.");
            if (problems.Count == 0)
                problems.AddRange(SetPieceDefBuilder.BuildAll());
            return problems;
        }

        /// <summary>Renders, packs and writes every sheet and the contract. Throws on any refusal.</summary>
        public static SetPieceSheetSlicer.Contract Bake(out string log)
        {
            var total = Stopwatch.StartNew();
            var lines = new List<string>();

            string todayText = StPetersSetPieceKit.ReadTodayText();
            object today = DeckSidecarJson.Parse(todayText);
            StPetersSetPieceKit.CheckToday(today);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            StPetersSetPieceKit.Install(host);
            host.Execute("globalThis.__spToday = " + todayText + ";");
            host.Execute("globalThis.__spSky = WeatherSky.at(__spToday.bake.sky);");
            host.Execute(RenderFunction);

            if (!host.EvaluateBool("CoastalPass.light.lampNeed(__spSky) === __spToday.bake.lit"))
                throw new InvalidOperationException(
                    "The bake sky's lamp need is not the today file's lit value: the frames would bake with " +
                    "their windows in a state the Defs do not record.");

            var contract = new SetPieceSheetSlicer.Contract
            {
                schema = SetPieceSheetSlicer.ContractSchema,
                importSizeCap = (int)host.EvaluateNumber("__spToday.bake.importSizeCap"),
                facings = (int)host.EvaluateNumber("__spToday.bake.facings"),
                kitVersion = host.EvaluateString($"{StPetersSetPieceKit.GlobalName}.VERSION"),
                todaySha256Lf = StPetersSetPieceKit.Sha256Hex(System.Text.Encoding.UTF8.GetBytes(StPetersSetPieceKit.Lf(todayText))),
                skyJson = host.EvaluateString("JSON.stringify(__spToday.bake.sky)"),
                bakedLit = (float)host.EvaluateNumber("__spToday.bake.lit"),
                outline = host.EvaluateBool("__spToday.bake.outline === true"),
                pixelsPerMetre = (float)host.EvaluateNumber($"{StPetersSetPieceKit.GlobalName}.PPU"),
                elevationDeg = (float)host.EvaluateNumber(
                    $"(function (B) {{ return Math.round(Math.atan2(B.se, B.ce) * 180 / Math.PI * 1e6) / 1e6; }})" +
                    $"({StPetersSetPieceKit.GlobalName}.basis(0))"),
            };

            if (contract.facings != StPetersSetPieceKit.Facings || contract.importSizeCap != StPetersSetPieceKit.ImportSizeCap)
                throw new InvalidOperationException(
                    $"the today file bakes {contract.facings} facings at a {contract.importSizeCap} px cap; this " +
                    $"baker is {StPetersSetPieceKit.Facings} at {StPetersSetPieceKit.ImportSizeCap}.");

            lines.Add($"[StPetersSetPieceBaker] {host.EngineName}, kit {contract.kitVersion}, sky {contract.skyJson}, " +
                      $"lit {contract.bakedLit.ToString(CultureInfo.InvariantCulture)}, cap {contract.importSizeCap}");

            int n = (int)host.EvaluateNumber("__spToday.pieces.length");
            if (n != StPetersSetPieceKit.Pieces.Count)
                throw new InvalidOperationException($"the today file bakes {n} pieces, not {StPetersSetPieceKit.Pieces.Count}.");

            for (int i = 0; i < n; i++)
                contract.pieces.Add(BakePiece(host, i, contract, lines));

            string contractJson = StPetersSetPieceKit.Lf(JsonUtility.ToJson(contract, true)) + "\n";
            File.WriteAllText(StPetersSetPieceKit.Abs(SetPieceSheetSlicer.ContractPath), contractJson);

            total.Stop();
            lines.Add($"[StPetersSetPieceBaker] {n} sheets and the contract in {total.Elapsed.TotalMilliseconds:F0} ms.");
            log = string.Join("\n", lines);
            return contract;
        }

        struct Cell
        {
            public int Index, RigDir, Heading, W, H, Px, Py, X0, Y0, X1, Y1, Opaque;
            public byte[] Rgba;
        }

        static SetPieceSheetSlicer.Piece BakePiece(IRigScriptHost host, int i, SetPieceSheetSlicer.Contract contract,
                                                   List<string> lines)
        {
            string key = host.EvaluateString($"__spToday.pieces[{i}].key");
            string id = host.EvaluateString($"__spToday.pieces[{i}].id");
            if (StPetersSetPieceKit.Pieces[i].Key != key || StPetersSetPieceKit.Pieces[i].Value != id)
                throw new InvalidOperationException(
                    $"today piece {i} is {key} ({id}); the kit's order says {StPetersSetPieceKit.Pieces[i].Key} " +
                    $"({StPetersSetPieceKit.Pieces[i].Value}).");

            // THE ORACLE: the kit, today, still writes the gameplay block the Defs are built from.
            string g = StPetersSetPieceKit.GlobalName;
            if (!host.EvaluateBool($"JSON.stringify({g}.gameplay(__spToday.pieces[{i}].key, " +
                                   $"Object.assign({{}}, __spToday.pieces[{i}].options))) === " +
                                   $"JSON.stringify(__spToday.pieces[{i}].gameplay)"))
                throw new InvalidOperationException(
                    $"{key}: {g}.gameplay at the today options is no longer the today file's block. The kit or " +
                    "the file changed; regenerate the file and re-read the diff before baking.");

            int headings = host.EvaluateBool($"__spToday.pieces[{i}].gameplay.animated != null")
                ? (int)host.EvaluateNumber($"__spToday.pieces[{i}].gameplay.animated.steps")
                : 0;
            int count = contract.facings + headings;

            var cells = new Cell[count];
            int cellW = 0, cellH = 0;
            for (int c = 0; c < count; c++)
            {
                bool heading = c >= contract.facings;
                int dir = heading ? StPetersSetPieceKit.HeadingRigDir : StPetersSetPieceKit.RigDirForFrame(c);
                int step = heading ? c - contract.facings : -1;

                host.Execute($"globalThis.__spR = __spRender({i}, {dir}, {step});");
                var cell = new Cell
                {
                    Index = c, RigDir = dir, Heading = step,
                    W = (int)host.EvaluateNumber("__spR.w"), H = (int)host.EvaluateNumber("__spR.h"),
                    Px = (int)host.EvaluateNumber("__spR.px"), Py = (int)host.EvaluateNumber("__spR.py"),
                };
                if (!host.EvaluateBool("__spR.cellOk && [__spR.w, __spR.h, __spR.px, __spR.py].every(Number.isInteger)"))
                    throw new InvalidOperationException(
                        $"{key} cell {c} (dir {dir}, step {step}): the render's {cell.W}×{cell.H} pivot " +
                        $"({cell.Px},{cell.Py}) is not the cell its gameplay block names.");
                cell.Rgba = host.EvaluateBytes("__spR.rgba");
                host.Execute("globalThis.__spR = null;");

                if (cell.Rgba.Length != cell.W * cell.H * 4)
                    throw new InvalidOperationException(
                        $"{key} cell {c}: {cell.Rgba.Length} bytes for a {cell.W}×{cell.H} render.");
                if (!NavBuoyKit.InkBox(cell.Rgba, cell.W, cell.H, out int x0, out int y0, out int x1, out int y1))
                    throw new InvalidOperationException($"{key} cell {c} draws nothing.");

                // The frame's box: its ink, seeded at its own pivot (a cell must hold its ground point).
                cell.X0 = Math.Min(x0, cell.Px); cell.Y0 = Math.Min(y0, cell.Py);
                cell.X1 = Math.Max(x1, cell.Px); cell.Y1 = Math.Max(y1, cell.Py);
                for (int a = 3; a < cell.Rgba.Length; a += 4)
                    if (cell.Rgba[a] == 255) cell.Opaque++;

                cellW = Math.Max(cellW, cell.X1 - cell.X0 + 1);
                cellH = Math.Max(cellH, cell.Y1 - cell.Y0 + 1);
                cells[c] = cell;
            }

            if (!NavBuoyKit.PlanSheet(cellW, cellH, count, contract.importSizeCap,
                                      out int cols, out int rows, out int sheetW, out int sheetH))
                throw new InvalidOperationException(
                    $"{key}: {count} cells of {cellW}×{cellH} fit no grid under {contract.importSizeCap} px. " +
                    "Stop and report; do not raise the cap.");

            var piece = new SetPieceSheetSlicer.Piece
            {
                key = key, id = id, sheet = SetPieceSheetSlicer.SheetName(key),
                cells = count, headings = headings, cellW = cellW, cellH = cellH,
                cols = cols, rows = rows, sheetW = sheetW, sheetH = sheetH,
                optionsJson = host.EvaluateString($"JSON.stringify(__spToday.pieces[{i}].gameplay.options)"),
            };

            var pixels = new Color32[sheetW * sheetH];
            foreach (var cell in cells)
            {
                BuildingRigBaker.BlitCropped(cell.Rgba, cell.W, cell.H, cell.X0, cell.Y0, cellW, cellH,
                                             pixels, sheetW, sheetH, col: cell.Index % cols, rowFromTop: cell.Index / cols);
                piece.frames.Add(new SetPieceSheetSlicer.Frame
                {
                    cell = cell.Index, rigDir = cell.RigDir, heading = cell.Heading,
                    cropX = cell.X0, cropY = cell.Y0, pivotX = cell.Px - cell.X0, pivotY = cell.Py - cell.Y0,
                    nativeW = cell.W, nativeH = cell.H, nativePivotX = cell.Px, nativePivotY = cell.Py,
                    opaque = cell.Opaque,
                });
            }

            int bytes = WritePng(pixels, sheetW, sheetH, SetPieceSheetSlicer.SheetPath(key));
            lines.Add($"  {key,-14} {count,2} cells of {cellW}×{cellH} → {cols}×{rows} = {sheetW}×{sheetH} ({bytes:N0} B)");
            return piece;
        }

        static int WritePng(Color32[] pixels, int w, int h, string assetPath)
        {
            string abs = StPetersSetPieceKit.Abs(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs) ?? "");

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(abs, png);
                return png.Length;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
