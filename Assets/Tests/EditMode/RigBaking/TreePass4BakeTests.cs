using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ONE real pass-4 bake into <c>Temp/</c>, for a fixture to read: the contract exported from rig 4 +
    /// treeMaps4 + the glue, then every sheet baked from it, for two species that between them cover
    /// both kinds of tree (BlackSpruce, a conifer whose winter is its own tree; TremblingAspen, a
    /// broadleaf that shimmers in calm and stands bare in winter) and both seasons the ruled kit bakes.
    ///
    /// <para>⚠️ <c>Temp/</c> and never the kit: the game still draws pass 3, and
    /// <see cref="TreePass4Baker.RefuseTheLiveKitWhilePass3"/> refuses a pass-4 write under
    /// <see cref="TreeKitCatalog.TreesRoot"/> until <c>RigScriptPath</c> switches. Nothing here is
    /// imported, and each fixture deletes only its own subfolder.</para>
    ///
    /// <para>A failed bake is CAPTURED, not thrown, so the tests that read no bake (the path guard, the
    /// 2048 gate) still run and report; every test that reads one calls <see cref="Run.Require"/>
    /// first and fails with the bake's own exception.</para>
    /// </summary>
    internal static class TreePass4TempBake
    {
        public const string Root = "Temp/hh-tree-pass4-tests";
        public const string Stage = TreeRigBaker.DefaultStage;
        public const int Variants = 4;
        public static readonly string[] Species = { "BlackSpruce", "TremblingAspen" };
        public static readonly string[] Seasons = { "summer", "winter" };

        /// <summary>The rig's own species order, as the exporter and the tests read it.</summary>
        public const string SpeciesKeysExpr = "TreeRig4.SPECIES.map(function (s) { return s.key; })";

        public sealed class Run
        {
            public string Folder;
            public string ContractPath;
            public string SheetsFolder;
            public TreePass4ExportResult Export;
            public TreePass4BakeResult Bake;
            public Exception Error;

            public void Require()
            {
                if (Error != null)
                    Assert.Fail($"The Temp pass-4 bake into {Folder} failed, so there is nothing to read:\n{Error}");
                Assert.IsNotNull(Export, "The Temp export reported nothing.");
                Assert.IsNotNull(Bake, "The Temp bake reported nothing.");
            }
        }

        public static Run BakeInto(string name)
        {
            var run = new Run { Folder = Root + "/" + name };
            run.ContractPath = run.Folder + "/" + TreeKitCatalog.ContractFileName;
            run.SheetsFolder = run.Folder + "/sheets";
            try
            {
                DeleteFolder(run.Folder);
                run.Export = TreePass4Baker.ExportContract(Species, Stage, Seasons, run.ContractPath);
                run.Bake = TreePass4Baker.Bake(outputFolder: run.SheetsFolder, contractPath: run.ContractPath);
            }
            catch (Exception ex)
            {
                run.Error = ex;
            }
            return run;
        }

        public static string RepoRoot => Directory.GetParent(Application.dataPath).FullName;

        public static string Abs(string rel) => Path.Combine(RepoRoot, rel);

        public static void DeleteFolder(string rel)
        {
            string full = Abs(rel);
            if (Directory.Exists(full))
                Directory.Delete(full, recursive: true);
        }

        public static TreeKitCatalog.Contract ReadContract(string rel) =>
            JsonUtility.FromJson<TreeKitCatalog.Contract>(File.ReadAllText(Abs(rel)));

        public static string SheetOf(Run run, TreeKitCatalog.Entry e, string season, TreeKitCatalog.Channel c) =>
            $"{run.SheetsFolder}/{TreeKitCatalog.StemFor(e.species, e.stage, season, c)}.png";

        /// <summary>A written PNG decoded CPU-side, in Unity's order (row 0 at the BOTTOM).</summary>
        public static Color32[] ReadSheet(string assetPath, out int width, out int height)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                Assert.IsTrue(t.LoadImage(File.ReadAllBytes(Abs(assetPath)), markNonReadable: false),
                              $"{assetPath} does not decode as a PNG.");
                width = t.width;
                height = t.height;
                return t.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(t);
            }
        }

        /// <summary>Column <paramref name="col"/> of a one-row sheet, back in the RIG's order (row 0 at the
        /// top, RGBA), which is how the glue hands a cell over.</summary>
        public static byte[] CellFromSheet(Color32[] px, int sheetW, int sheetH, int cellW, int cellH, int col)
        {
            var cell = new byte[cellW * cellH * 4];
            for (int y = 0; y < cellH; y++)
            {
                int unityY = sheetH - 1 - y;
                for (int x = 0; x < cellW; x++)
                {
                    Color32 c = px[unityY * sheetW + col * cellW + x];
                    int o = (y * cellW + x) * 4;
                    cell[o] = c.r;
                    cell[o + 1] = c.g;
                    cell[o + 2] = c.b;
                    cell[o + 3] = c.a;
                }
            }
            return cell;
        }

        /// <summary>How many texels of column <paramref name="col"/> differ from a rig-order cell
        /// (<see cref="TreeRigBakeTests"/>' comparison, with the cell given by its size).</summary>
        public static int CompareCell(Color32[] sheet, int sheetW, int sheetH, int cellW, int cellH, int col,
                                      byte[] cell, int rowShift = 0)
        {
            int mismatched = 0;
            for (int y = 0; y < cellH; y++)
            {
                int unityY = sheetH - 1 - (y + rowShift);
                if (unityY < 0 || unityY >= sheetH) { mismatched += cellW; continue; }
                for (int x = 0; x < cellW; x++)
                {
                    Color32 got = sheet[unityY * sheetW + col * cellW + x];
                    int s = (y * cellW + x) * 4;
                    if (got.r != cell[s] || got.g != cell[s + 1] || got.b != cell[s + 2] || got.a != cell[s + 3])
                        mismatched++;
                }
            }
            return mismatched;
        }

        // ---- the baker's private JS quoting, copied (TreePass4Baker keeps its own private) -------------

        public static string Js(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        public static string JsArray(IEnumerable<string> items)
        {
            var sb = new StringBuilder("[");
            foreach (string s in items)
            {
                if (sb.Length > 1) sb.Append(", ");
                sb.Append(Js(s));
            }
            return sb.Append(']').ToString();
        }

        public static string GapRowsJs(TreeKitCatalog.Entry e)
        {
            var sb = new StringBuilder("{");
            for (int i = 0; i < e.seasonRows.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Js(e.seasonRows[i].season)).Append(": ").Append(JsArray(e.seasonRows[i].gapRow));
            }
            return sb.Append('}').ToString();
        }

        /// <summary>A V8 host with rig 4, treeMaps4 and the glue installed, as the baker installs them.</summary>
        public static IRigScriptHost CreateHost()
        {
            var host = RigScriptHostFactory.Create();
            try
            {
                TreePass4Baker.InstallRig(host);
                return host;
            }
            catch
            {
                host.Dispose();
                throw;
            }
        }

        /// <summary>The glue's bake of one entry, by the baker's exact call: its cells are then held
        /// for <c>HHTreePass4.cell(season, channel, variant)</c> until <c>release()</c>.</summary>
        public static void GlueBake(IRigScriptHost host, TreeKitCatalog.Contract contract, TreeKitCatalog.Entry e) =>
            host.Execute($"globalThis.HHTreePass4Last = JSON.parse(HHTreePass4.bake({Js(e.species)}, " +
                         $"{Js(e.stage)}, {JsArray(e.seasons)}, {JsArray(contract.snowRow)}, " +
                         $"{GapRowsJs(e)}));");

        /// <summary>An Int32Array (the rig's <c>shade().src</c>, <c>.v.st</c>) as ints, through a byte view.</summary>
        public static int[] Ints(IRigScriptHost host, string int32ArrayExpr)
        {
            byte[] b = host.EvaluateBytes(
                $"(function () {{ var a = {int32ArrayExpr}; return new Uint8Array(a.buffer, a.byteOffset, a.byteLength); }})()");
            var r = new int[b.Length / 4];
            Buffer.BlockCopy(b, 0, r, 0, b.Length);
            return r;
        }

        public static string F(double d) => d.ToString("0.####", CultureInfo.InvariantCulture);

        public static string Hex(Color32 c) => $"{c.r:x2}{c.g:x2}{c.b:x2}";

        public static bool IsHex6(string s) =>
            s != null && s.Length == 6 && s.All(ch => (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f'));
    }

    /// <summary>
    /// THE ACCEPTANCE SUITE FOR THE PASS-4 TREE BAKE — the contract, the sheets, the palette, the refusals,
    /// and the mask that must keep OUR order. The rig is its own oracle again: every sheet is compared
    /// with the glue's cell for it, byte for byte, and the contract's geometry and wind with the rig's
    /// own <c>sheetSpec()</c> and treeMaps4's <c>constants()</c>.
    ///
    /// <para><b>MEASURED SABOTAGE.</b> Each check carries the wrong answer it must reject, run in the same
    /// test: the next variant's cell and a one-row shift against every sheet, the palette read top-down,
    /// the snow sheet counted as RGBA, a 1% bend, a drifted cell, a foreign snow row, the next glue version,
    /// and R↔G and B↔A swapped masks. A check that cannot tell those apart proves nothing.</para>
    ///
    /// <para>CPU-only, like <see cref="TreeRigBakeTests"/>: the V8 host, <c>Texture2D.LoadImage</c> and
    /// <c>GetPixels32</c> need no graphics device, so this runs in batchmode CI.</para>
    /// </summary>
    public class TreePass4BakeTests
    {
        const string Stage = TreePass4TempBake.Stage;
        const int Variants = TreePass4TempBake.Variants;

        TreePass4TempBake.Run _run;

        [OneTimeSetUp]
        public void BakeOnce() => _run = TreePass4TempBake.BakeInto("shared");

        [OneTimeTearDown]
        public void DeleteTheBake()
        {
            if (_run != null) TreePass4TempBake.DeleteFolder(_run.Folder);
        }

        static string Abs(string rel) => TreePass4TempBake.Abs(rel);

        // =====================================================================================
        // the contract
        // =====================================================================================

        /// <summary>
        /// The exported contract carries everything the pass-4 path reads — the maps block, the glue's
        /// version, the snow row, the palette and one season row per baked season — and says it the same
        /// way in memory and after <c>JsonUtility</c> has written and re-read it from disk.
        /// </summary>
        [Test]
        public void TheContract_CarriesThePass4Fields_OnDiskAsInMemory()
        {
            _run.Require();
            var memory = _run.Export.Contract;
            var disk = TreePass4TempBake.ReadContract(_run.ContractPath);
            Assert.IsNotNull(memory, "ExportContract reported no contract.");
            Assert.AreEqual(_run.ContractPath, _run.Export.ContractPath);

            foreach (var (label, c) in new[] { ("in memory", memory), ("on disk", disk) })
                AssertPass4Contract(label, c);

            int floats = 0, exact = 0;
            foreach (var (name, m, d) in FloatPairs(memory, disk))
            {
                floats++;
                if (m == d) { exact++; continue; }
                double rel = Math.Abs(m - d) / Math.Max(Math.Abs(m), Math.Abs(d));
                Assert.LessOrEqual(rel, 1e-6, $"{name}: {m:R} in memory but {d:R} on disk.");
            }
            Assert.Greater(floats, 0, "No floats compared.");
            Debug.Log($"[TreePass4Bake] contract floats: {exact} of {floats} exact through the JsonUtility round trip, " +
                      "the rest within 1e-6 relative.");
        }

        static void AssertPass4Contract(string label, TreeKitCatalog.Contract c)
        {
            string who = $"The contract {label}";
            Assert.IsTrue(TreeKitCatalog.HasPass4(c), $"{who} is not a pass-4 contract.");
            Assert.AreEqual(TreeKitCatalog.Pass4RigScriptPath, c.rig, who);
            Assert.AreEqual(TreeKitCatalog.Pass4RigGlobalName, c.global, who);
            Assert.AreEqual(TreeKitCatalog.Pass4MapsScriptPath, c.maps.script, who);
            Assert.AreEqual(TreeKitCatalog.Pass4MapsGlobalName, c.maps.global, who);
            Assert.AreEqual(TreeKitCatalog.Pass4GlueGlobalName, c.maps.glue, who);
            Assert.AreEqual(TreePass4Glue.Version, c.maps.glueVersion, $"{who}: glue version");
            Assert.AreEqual(TreeWindMath.Loop, c.maps.loop, $"{who}: the maps' loop is the twin's");

            Assert.AreEqual(TreeKitCatalog.PaletteWidth, c.snowRow.Length, $"{who}: snow row");
            foreach (string hex in c.snowRow)
                Assert.IsTrue(TreePass4TempBake.IsHex6(hex), $"{who}: snow colour '{hex}' is not six lowercase hex digits.");

            Assert.AreEqual(TreeKitCatalog.PaletteWidth, c.palette.width, $"{who}: palette width");
            Assert.AreEqual(TreeKitCatalog.PaletteFileName, c.palette.file, $"{who}: palette file");
            int distinctGapRows = c.trees.SelectMany(t => t.seasonRows)
                                   .Select(r => string.Join(",", r.gapRow))
                                   .Distinct(StringComparer.Ordinal).Count();
            Assert.AreEqual(1 + distinctGapRows, c.palette.rows, $"{who}: the snow row plus one row per distinct gap row");
            Assert.GreaterOrEqual(c.palette.rows, 2, $"{who}: palette rows");

            Assert.AreEqual(Variants, c.sheet.cols, $"{who}: sheet columns");
            Assert.AreEqual(TreeRigBaker.SwayRowsBaked, c.sheet.rows, $"{who}: sheet rows");

            CollectionAssert.AreEqual(TreePass4TempBake.Species, c.trees.Select(t => t.species).ToArray(),
                                      $"{who}: the species, in the order asked");
            foreach (var e in c.trees)
            {
                string tree = $"{who}, {e.species}";
                Assert.IsTrue(TreeKitCatalog.HasPass4(e), $"{tree} carries no wind block or season rows.");
                CollectionAssert.AreEqual(TreePass4TempBake.Seasons, e.seasons, $"{tree}: seasons, in the rig's order");
                Assert.AreEqual(Stage, e.stage, tree);
                Assert.AreEqual(Variants * e.cellW, e.sheetW, $"{tree}: one row of {Variants} cells");
                Assert.AreEqual(e.cellH, e.sheetH, $"{tree}: one row of cells");
                foreach (string season in e.seasons)
                    Assert.IsNotNull(TreeKitCatalog.SeasonRowFor(e, season), $"{tree}: no season row for {season}");
            }
        }

        static IEnumerable<(string name, float m, float d)> FloatPairs(TreeKitCatalog.Contract m, TreeKitCatalog.Contract d)
        {
            yield return ("camera.elevDeg", m.camera.elevDeg, d.camera.elevDeg);
            yield return ("camera.heightScale", m.camera.heightScale, d.camera.heightScale);
            yield return ("camera.depthScale", m.camera.depthScale, d.camera.depthScale);
            Assert.AreEqual(m.light.key.Length, d.light.key.Length, "light.key");
            for (int i = 0; i < m.light.key.Length; i++)
                yield return ($"light.key[{i}]", m.light.key[i], d.light.key[i]);
            Assert.AreEqual(m.light.rim.Length, d.light.rim.Length, "light.rim");
            for (int i = 0; i < m.light.rim.Length; i++)
                yield return ($"light.rim[{i}]", m.light.rim[i], d.light.rim[i]);
            Assert.AreEqual(m.trees.Length, d.trees.Length, "trees");
            for (int t = 0; t < m.trees.Length; t++)
            {
                var a = m.trees[t];
                var b = d.trees[t];
                string s = a.species;
                yield return ($"{s}.metres", a.metres, b.metres);
                yield return ($"{s}.trunkAnchor", a.trunkAnchor, b.trunkAnchor);
                yield return ($"{s}.unityPivotX", a.unityPivotX, b.unityPivotX);
                yield return ($"{s}.unityPivotY", a.unityPivotY, b.unityPivotY);
                yield return ($"{s}.wind.H", a.wind.H, b.wind.H);
                yield return ($"{s}.wind.bendPx", a.wind.bendPx, b.wind.bendPx);
                yield return ($"{s}.wind.limbPx", a.wind.limbPx, b.wind.limbPx);
                yield return ($"{s}.wind.bob", a.wind.bob, b.wind.bob);
                yield return ($"{s}.wind.flutter", a.wind.flutter, b.wind.flutter);
                int ma = a.wind.shimmer?.Length ?? 0, mb = b.wind.shimmer?.Length ?? 0;
                Assert.AreEqual(ma, mb, $"{s}.wind.shimmer");
                for (int i = 0; i < ma; i++)
                    yield return ($"{s}.wind.shimmer[{i}]", a.wind.shimmer[i], b.wind.shimmer[i]);
            }
        }

        /// <summary>The note carries no clock and the species keep their order, so the same export twice is
        /// the same bytes: a regenerated <c>Trees.json</c> diffs only where the rig moved.</summary>
        [Test]
        public void TheContract_ExportsByteIdentical_Twice()
        {
            string a = _run.Folder + "/again/a.json", b = _run.Folder + "/again/b.json";
            var one = new[] { "BlackSpruce" };
            TreePass4Baker.ExportContract(one, Stage, TreePass4TempBake.Seasons, a);
            TreePass4Baker.ExportContract(one, Stage, TreePass4TempBake.Seasons, b);
            byte[] x = File.ReadAllBytes(Abs(a)), y = File.ReadAllBytes(Abs(b));
            int first = -1;
            for (int i = 0; i < Math.Min(x.Length, y.Length) && first < 0; i++)
                if (x[i] != y[i]) first = i;
            Assert.IsTrue(x.Length == y.Length && first < 0,
                          $"Two exports of the same species differ: {x.Length} vs {y.Length} bytes, first difference at byte {first}.");
            Assert.Greater(x.Length, 0);
        }

        // =====================================================================================
        // the sheets
        // =====================================================================================

        /// <summary>
        /// Every sheet the bake wrote is exactly the glue's cells for it, each in its own column: the
        /// folder holds the routed stems and the palette and nothing else, and every texel of every
        /// variant matches. The next variant's cell and a one-row shift must NOT match.
        /// <para>Also the proof the PNG round trip keeps what the maps carry: RGB under A = 0 (the wind
        /// map's word bytes) survives <c>EncodeToPNG</c> and <c>LoadImage</c> unchanged.</para>
        /// </summary>
        [Test]
        public void EverySheet_IsTheGluesCell_ByteForByte_InItsColumn()
        {
            _run.Require();
            var contract = TreePass4TempBake.ReadContract(_run.ContractPath);
            var bake = _run.Bake;

            var routed = new List<(TreeKitCatalog.Entry e, string season, TreeKitCatalog.Channel c)>();
            foreach (var e in contract.trees)
                foreach (string season in e.seasons)
                    foreach (var c in TreeKitCatalog.ChannelsFor(e, season))
                        routed.Add((e, season, c));
            Assert.AreEqual(24, routed.Count, "2 species × 2 seasons × 6 channels: every season here draws its own maps.");
            Assert.AreEqual(routed.Count, bake.Sheets.Count, "sheets reported");
            Assert.AreEqual(routed.Count * Variants, bake.CellsRead, "cells read from the glue");

            string[] want = routed.Select(r => TreeKitCatalog.StemFor(r.e.species, r.e.stage, r.season, r.c) + ".png")
                                  .Append(TreeKitCatalog.PaletteFileName)
                                  .OrderBy(n => n, StringComparer.Ordinal).ToArray();
            string[] got = Directory.GetFiles(Abs(_run.SheetsFolder)).Select(p => Path.GetFileName(p))
                                    .OrderBy(n => n, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(want, got, "The sheets folder holds exactly the routed sheets and the palette.");

            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in bake.Sheets)
            {
                Assert.AreEqual(TreeKitCatalog.StemFor(s.Species, s.Stage, s.Season, s.Channel), s.Stem, s.ToString());
                Assert.AreEqual($"{_run.SheetsFolder}/{s.Stem}.png", s.AssetPath, s.Stem);
                Assert.AreEqual(Stage, s.Stage, s.Stem);
                Assert.AreEqual(Variants, s.Cols, s.Stem);
                Assert.AreEqual(TreeRigBaker.SwayRowsBaked, s.Rows, s.Stem);
                Assert.IsTrue(reported.Add($"{s.Species}/{s.Season}/{s.Channel}"), $"{s.Stem} reported twice");
            }
            foreach (var r in routed)
                Assert.IsTrue(reported.Contains($"{r.e.species}/{r.season}/{r.c}"), $"{r.e.species}/{r.season}/{r.c} was not reported");

            var log = new StringBuilder("[TreePass4Bake] sheets against the glue's cells (next variant / one row down):\n");
            using var host = TreePass4TempBake.CreateHost();
            foreach (var e in contract.trees)
            {
                TreePass4TempBake.GlueBake(host, contract, e);
                foreach (string season in e.seasons)
                    foreach (var channel in TreeKitCatalog.ChannelsFor(e, season))
                    {
                        string path = TreePass4TempBake.SheetOf(_run, e, season, channel);
                        var sheet = TreePass4TempBake.ReadSheet(path, out int w, out int h);
                        Assert.AreEqual(Variants * e.cellW, w, $"{path}: width");
                        Assert.AreEqual(e.cellH, h, $"{path}: height");
                        Assert.LessOrEqual(Math.Max(w, h), TreeKitCatalog.ImportSizeCap, path);

                        var cells = new byte[Variants][];
                        for (int v = 0; v < Variants; v++)
                        {
                            cells[v] = host.EvaluateBytes(
                                $"HHTreePass4.cell({TreePass4TempBake.Js(season)}, " +
                                $"{TreePass4TempBake.Js(TreePass4Baker.GlueChannel(channel))}, {v})");
                            Assert.AreEqual(e.cellW * e.cellH * 4, cells[v].Length, $"{path}: glue cell {v}");
                        }

                        int nextWorst = int.MaxValue, shiftWorst = int.MaxValue;
                        for (int v = 0; v < Variants; v++)
                        {
                            Assert.AreEqual(0, TreePass4TempBake.CompareCell(sheet, w, h, e.cellW, e.cellH, v, cells[v]),
                                            $"{path}: column {v} is not the glue's variant {v}, byte for byte.");
                            int next = TreePass4TempBake.CompareCell(sheet, w, h, e.cellW, e.cellH, v, cells[(v + 1) % Variants]);
                            int shift = TreePass4TempBake.CompareCell(sheet, w, h, e.cellW, e.cellH, v, cells[v], rowShift: 1);
                            nextWorst = Math.Min(nextWorst, next);
                            shiftWorst = Math.Min(shiftWorst, shift);
                            if (channel == TreeKitCatalog.Channel.Albedo)
                            {
                                Assert.Greater(next, 0, $"SABOTAGE: {path} column {v} also matches variant {(v + 1) % Variants}.");
                                Assert.Greater(shift, 0, $"SABOTAGE: {path} column {v} also matches itself one row down.");
                            }
                        }
                        log.AppendLine($"  {Path.GetFileName(path)} {w}×{h}: 0 mismatches; sabotage fewest {nextWorst} / {shiftWorst}");
                    }
                host.Execute("HHTreePass4.release();");
            }
            host.Execute("delete globalThis.HHTreePass4Last;");
            Debug.Log(log.ToString());
        }

        /// <summary>The palette is 8 colours wide: row 0 (the BOTTOM of the texture, as the shader loads it)
        /// is the snow row, and every season row's <c>paletteRow</c> is its own gap row.</summary>
        [Test]
        public void ThePalette_IsTheSnowRowAtTheBottom_ThenEachGapRow()
        {
            _run.Require();
            var contract = TreePass4TempBake.ReadContract(_run.ContractPath);
            Assert.AreEqual($"{_run.SheetsFolder}/{TreeKitCatalog.PaletteFileName}", _run.Bake.PalettePath);

            var px = TreePass4TempBake.ReadSheet(_run.Bake.PalettePath, out int w, out int h);
            Assert.AreEqual(TreeKitCatalog.PaletteWidth, w, "palette width");
            Assert.AreEqual(contract.palette.rows, h, "palette rows, against the contract");
            Assert.AreEqual(_run.Bake.PaletteRows, h, "palette rows, against the bake's report");
            Assert.IsTrue(px.All(c => c.a == 255), "Every palette texel is opaque.");

            string[] Row(int r) => Enumerable.Range(0, w).Select(x => TreePass4TempBake.Hex(px[r * w + x])).ToArray();

            CollectionAssert.AreEqual(contract.snowRow, Row(0), "Row 0 (the bottom) is the snow row.");
            int rowsChecked = 0;
            foreach (var e in contract.trees)
                foreach (var sr in e.seasonRows)
                {
                    Assert.That(sr.paletteRow, Is.InRange(1, h - 1), $"{e.species}/{sr.season}: palette row");
                    CollectionAssert.AreEqual(sr.gapRow, Row(sr.paletteRow), $"{e.species}/{sr.season}: gap row {sr.paletteRow}");
                    rowsChecked++;
                }
            Assert.AreEqual(4, rowsChecked);

            CollectionAssert.AreNotEqual(contract.snowRow, Row(h - 1),
                                         "SABOTAGE: read top-down, the top row is not the snow row.");
        }

        /// <summary>The bake's texture budget counts the snow sheet as ONE channel (it imports R8) and every
        /// other sheet and the palette as RGBA32, from the sizes the PNGs actually are.</summary>
        [Test]
        public void TextureBytes_CountsTheSnowSheetAsOneChannel()
        {
            _run.Require();
            long want = 0, asRgba = 0, snowTexels = 0;
            foreach (var s in _run.Bake.Sheets)
            {
                var (w, h) = PngSize(Abs(s.AssetPath));
                long texels = (long)w * h;
                bool one = s.Channel == TreeKitCatalog.Channel.Snow;
                Assert.AreEqual(one, TreeKitCatalog.IsSingleChannel(s.Channel), s.Stem);
                want += texels * (one ? 1 : 4);
                asRgba += texels * 4;
                if (one) snowTexels += texels;
            }
            var (pw, ph) = PngSize(Abs(_run.Bake.PalettePath));
            Assert.AreEqual(TreeKitCatalog.PaletteWidth, pw, "palette width");
            want += (long)pw * ph * 4;
            asRgba += (long)pw * ph * 4;

            Assert.AreEqual(want, _run.Bake.TextureBytes, "TextureBytes against the PNGs' own IHDR sizes");
            Assert.Greater(snowTexels, 0, "No snow sheet was baked.");
            Assert.AreEqual(3 * snowTexels, asRgba - _run.Bake.TextureBytes,
                            "SABOTAGE: counting the snow sheet as RGBA must differ by three bytes a snow texel.");
            Debug.Log($"[TreePass4Bake] {_run.Bake.Sheets.Count} sheets + palette: {_run.Bake.TextureBytes / 1048576.0:F2} MB " +
                      $"imported ({asRgba / 1048576.0:F2} MB if the snow were RGBA32); PNG bytes {_run.Bake.TotalPngBytes}.");
        }

        /// <summary>Width and height from a PNG's IHDR (big-endian, bytes 16–23), independent of the baker.</summary>
        static (int w, int h) PngSize(string fullPath)
        {
            var head = new byte[24];
            using (var f = File.OpenRead(fullPath))
                Assert.AreEqual(24, f.Read(head, 0, 24), fullPath);
            Assert.IsTrue(head[0] == 0x89 && head[1] == (byte)'P' && head[2] == (byte)'N' && head[3] == (byte)'G',
                          $"{fullPath} is not a PNG.");
            Assert.AreEqual("IHDR", Encoding.ASCII.GetString(head, 12, 4), fullPath);
            int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            int h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
            return (w, h);
        }

        // =====================================================================================
        // every species: the geometry and the wind the contract carries
        // =====================================================================================

        /// <summary>
        /// Exported for EVERY species, the contract carries the rig's own geometry (the cell, the trunk
        /// foot, the sheet, <c>trunkAnchor</c> and the wind pad) and treeMaps4's own wind response,
        /// in the rig's species order. The per-species numbers reach the shader only this way.
        /// </summary>
        [Test]
        public void EverySpecies_ExportsTheRigsGeometry_AndItsWindResponse()
        {
            string path = _run.Folder + "/all/" + TreeKitCatalog.ContractFileName;
            TreePass4Baker.ExportContract(null, Stage, new[] { TreeRigBaker.DefaultSeason }, path);
            var contract = TreePass4TempBake.ReadContract(path);

            using var host = TreePass4TempBake.CreateHost();
            var keys = FishingKitBaker.ReadStringArray(host, TreePass4TempBake.SpeciesKeysExpr);
            CollectionAssert.AreEqual(keys.ToArray(), contract.trees.Select(t => t.species).ToArray(),
                                      "Every species, in the rig's order.");

            var table = new StringBuilder("[TreePass4Bake] species: cell | pivot | pad | sheet | trunkAnchor | reach | bend/limb/bob/flutter | shimmer | conifer\n");
            foreach (var e in contract.trees)
            {
                string who = e.species;
                var spec = TreePass4Baker.ReadSheetSpec(host, e.species, Stage, out int reach);
                Assert.AreEqual(spec.CellW, e.cellW, $"{who}: cellW");
                Assert.AreEqual(spec.CellH, e.cellH, $"{who}: cellH");
                Assert.AreEqual(spec.PivotX, e.pivotX, $"{who}: pivotX");
                Assert.AreEqual(spec.PivotY, e.pivotY, $"{who}: pivotY");
                Assert.AreEqual(spec.Pad, e.nearFlarePad, $"{who}: nearFlarePad");
                Assert.AreEqual(spec.SheetW, e.sheetW, $"{who}: sheetW");
                Assert.AreEqual(spec.SheetH, e.sheetH, $"{who}: sheetH");
                Assert.AreEqual(spec.TrunkAnchor, e.trunkAnchor, 1e-6f, $"{who}: trunkAnchor is pad / cellH");
                Assert.AreEqual(reach, e.wind.windReach, $"{who}: windReach");
                Assert.AreEqual(spec.RigSheetW, e.rigSheetW, $"{who}: rigSheetW");
                Assert.AreEqual(spec.RigSheetH, e.rigSheetH, $"{who}: rigSheetH");
                Assert.AreEqual(spec.RigFits, e.rigFitsUnity2048, $"{who}: rigFitsUnity2048");

                CollectionAssert.IsEmpty(WindDrift(host, e.species, e.wind),
                                         $"{who}: the contract's wind is not treeMaps4.constants()'s.");

                Assert.AreEqual(1, e.seasonRows.Length, $"{who}: one season row");
                var row = e.seasonRows[0];
                Assert.AreEqual(TreeKitCatalog.PaletteWidth, row.gapRow.Length, $"{who}: gap row");
                foreach (string hex in row.gapRow)
                    Assert.IsTrue(TreePass4TempBake.IsHex6(hex), $"{who}: gap colour '{hex}'");
                Assert.That(row.paletteRow, Is.InRange(1, contract.palette.rows - 1), $"{who}: palette row");

                var w = e.wind;
                table.AppendLine($"  {who}: {e.cellW}×{e.cellH} | {e.pivotX},{e.pivotY} | {e.nearFlarePad} | {e.sheetW}×{e.sheetH} | " +
                                 $"{e.trunkAnchor:0.0000} | {reach} | {w.bendPx:0.####}/{w.limbPx:0.####}/{w.bob:0.##}/{w.flutter:0.##} | " +
                                 $"{TreeKitCatalog.ShimmerCalm(w):0.###} | {w.conifer}");
            }

            var first = contract.trees[0];
            var drifted = JsonUtility.FromJson<TreeKitCatalog.WindBlock>(JsonUtility.ToJson(first.wind));
            drifted.bendPx *= 1.01f;
            CollectionAssert.AreEqual(new[] { "bendPx" }, WindDrift(host, first.species, drifted),
                                      "SABOTAGE: a 1% bend must be named, and only it.");
            host.Execute("delete globalThis.HHTestC;");
            Debug.Log(table.ToString());
        }

        /// <summary>The fields of a wind block that disagree with treeMaps4's <c>constants(key, stage)</c>,
        /// the numbers the glue wraps for the exporter.</summary>
        static List<string> WindDrift(IRigScriptHost host, string species, TreeKitCatalog.WindBlock w)
        {
            host.Execute($"globalThis.HHTestC = TreeMaps4.constants({TreePass4TempBake.Js(species)}, {TreePass4TempBake.Js(Stage)});");
            var drift = new List<string>();

            bool Near(float got, string expr)
            {
                double want = host.EvaluateNumber(expr);
                return Math.Abs(got - want) <= 1e-5 * Math.Abs(want);
            }

            if (!Near(w.H, "HHTestC.H")) drift.Add("H");
            if (!Near(w.bendPx, "HHTestC.bendPx")) drift.Add("bendPx");
            if (!Near(w.limbPx, "HHTestC.limbPx")) drift.Add("limbPx");
            if (!Near(w.bob, "HHTestC.bob")) drift.Add("bob");
            if (!Near(w.flutter, "HHTestC.flutter")) drift.Add("flutter");
            if (host.EvaluateBool("HHTestC.shimmer == null"))
            {
                if (w.shimmer != null && w.shimmer.Length != 0) drift.Add("shimmer");
            }
            else if (w.shimmer == null || w.shimmer.Length != 2 ||
                     !Near(w.shimmer[0], "HHTestC.shimmer[0]") || !Near(w.shimmer[1], "HHTestC.shimmer[1]"))
            {
                drift.Add("shimmer");
            }
            if (host.EvaluateBool("!!HHTestC.conifer") != w.conifer) drift.Add("conifer");
            return drift;
        }

        // =====================================================================================
        // the refusals: a bake that disagrees with its contract writes NOTHING
        // =====================================================================================

        [Test]
        public void ABake_RefusesACellThatDriftedFromTheContract_AndWritesNothing()
        {
            _run.Require();
            var ex = RefusedBake("c1", new[] { "TremblingAspen" },
                                 c => c.trees.First(t => t.species == "TremblingAspen").cellW += 1);
            StringAssert.Contains("the rig's cell is", ex.Message);
        }

        [Test]
        public void ABake_RefusesASnowRowTheRigDoesNotSnowIn_AndWritesNothing()
        {
            _run.Require();
            var ex = RefusedBake("c2", new[] { "BlackSpruce" },
                                 c => c.snowRow = Enumerable.Repeat("000001", TreeKitCatalog.PaletteWidth).ToArray());
            StringAssert.Contains("the glue refused the bake", ex.Message);
            StringAssert.Contains("is not in the contract", ex.ToString());
        }

        [Test]
        public void ABake_RefusesAContractFromAnotherGlueVersion_AndWritesNothing()
        {
            _run.Require();
            var ex = RefusedBake("c3", new[] { "BlackSpruce" }, c => c.maps.glueVersion += 1);
            StringAssert.Contains("glue version", ex.Message);
            StringAssert.Contains("Export the contract again", ex.Message);
        }

        /// <summary>A Temp copy of the shared contract, mutated, then baked: the bake must refuse, and the
        /// sheets folder must not exist — the baker writes only after every entry has passed.</summary>
        InvalidOperationException RefusedBake(string name, string[] species, Action<TreeKitCatalog.Contract> mutate)
        {
            string folder = _run.Folder + "/" + name;
            string contractPath = folder + "/" + TreeKitCatalog.ContractFileName;
            string sheets = folder + "/sheets";
            var contract = TreePass4TempBake.ReadContract(_run.ContractPath);
            mutate(contract);
            Directory.CreateDirectory(Abs(folder));
            File.WriteAllText(Abs(contractPath), JsonUtility.ToJson(contract, prettyPrint: true) + "\n");

            var ex = Assert.Catch<InvalidOperationException>(
                () => TreePass4Baker.Bake(species, outputFolder: sheets, contractPath: contractPath),
                $"{name}: the bake did not refuse.");
            Assert.IsFalse(Directory.Exists(Abs(sheets)), $"{name}: the bake wrote {sheets} before it refused.");
            return ex;
        }

        [Test]
        public void AssertMatchesContract_NamesTheFieldThatDrifted()
        {
            _run.Require();
            var contract = TreePass4TempBake.ReadContract(_run.ContractPath);
            var e = contract.trees.First(t => t.species == "BlackSpruce");
            using var host = TreePass4TempBake.CreateHost();
            var spec = TreePass4Baker.ReadSheetSpec(host, e.species, Stage, out int reach);
            Assert.DoesNotThrow(() => TreePass4Baker.AssertMatchesContract(e, spec, reach), "The contract as exported.");

            var cases = new (string field, Action<TreeKitCatalog.Entry> mutate, string phrase)[]
            {
                ("cellW", x => x.cellW++, "the rig's cell is"),
                ("cellH", x => x.cellH++, "the rig's cell is"),
                ("pivotX", x => x.pivotX++, "the rig's trunk foot is"),
                ("pivotY", x => x.pivotY++, "the rig's trunk foot is"),
                ("nearFlarePad", x => x.nearFlarePad++, "the rig's trunk foot is"),
                ("sheetW", x => x.sheetW++, "the rig lays out"),
                ("sheetH", x => x.sheetH++, "the rig lays out"),
                ("wind.windReach", x => x.wind.windReach++, "pads the cell by"),
            };
            foreach (var (field, mutate, phrase) in cases)
            {
                var clone = JsonUtility.FromJson<TreeKitCatalog.Entry>(JsonUtility.ToJson(e));
                mutate(clone);
                var ex = Assert.Throws<InvalidOperationException>(
                    () => TreePass4Baker.AssertMatchesContract(clone, spec, reach), $"{field} drifted but passed.");
                StringAssert.Contains(phrase, ex.Message, field);
            }
        }

        // =====================================================================================
        // the kit stays pass 3's until the switch
        // =====================================================================================

        /// <summary>
        /// While the game draws pass 3, nothing pass-4 is written into the kit: the refusal fires for the
        /// contract, for any file or folder under <see cref="TreeKitCatalog.TreesRoot"/>, and for the root
        /// itself, but not for Temp; and it fires FIRST — before a host opens or a contract is read (a
        /// missing contract would otherwise throw <see cref="FileNotFoundException"/>).
        /// </summary>
        [Test]
        public void WhilePass3IsLive_NothingPass4IsWrittenIntoTheKit()
        {
            string probeFile = TreeKitCatalog.TreesRoot + "Pass4Probe_DoNotCommit.json";
            string probeFolder = TreeKitCatalog.TreesRoot + "Pass4Probe_DoNotCommit";
            if (TreeKitCatalog.IsPass4Live)
            {
                Assert.DoesNotThrow(() => TreePass4Baker.RefuseTheLiveKitWhilePass3(TreeKitCatalog.ContractPath, "write a probe"),
                                    "Pass 4 is live: the kit is pass 4's to write.");
                return;
            }

            var made = new List<string>();
            try
            {
                foreach (string kit in new[] { TreeKitCatalog.ContractPath, probeFile, TreeKitCatalog.TreesRoot })
                    Assert.Throws<InvalidOperationException>(() => TreePass4Baker.RefuseTheLiveKitWhilePass3(kit, "write a probe"), kit);
                Assert.DoesNotThrow(() => TreePass4Baker.RefuseTheLiveKitWhilePass3(_run.Folder + "/d1/Trees.json", "write a probe"));

                Assert.Throws<InvalidOperationException>(
                    () => TreePass4Baker.ExportContract(new[] { "BlackSpruce" }, Stage, new[] { TreeRigBaker.DefaultSeason }, probeFile));
                Assert.IsFalse(File.Exists(Abs(probeFile)), "The refused export wrote its contract into the kit.");

                Assert.Throws<InvalidOperationException>(
                    () => TreePass4Baker.Bake(outputFolder: probeFolder, contractPath: _run.Folder + "/d1/no-such-contract.json"),
                    "The kit refusal must fire before the contract is read.");
                Assert.IsFalse(Directory.Exists(Abs(probeFolder)), "The refused bake made a folder in the kit.");
            }
            finally
            {
                foreach (string p in new[] { probeFile, probeFile + ".meta", probeFolder + ".meta" })
                    if (File.Exists(Abs(p))) { made.Add(p); File.Delete(Abs(p)); }
                if (Directory.Exists(Abs(probeFolder))) { made.Add(probeFolder); Directory.Delete(Abs(probeFolder), recursive: true); }
            }
            CollectionAssert.IsEmpty(made, "Written into the kit (now removed).");
        }

        [Test]
        public void IsInsideTheLiveKit_ResolvesThePathBeforeComparing()
        {
            string root = TreeKitCatalog.TreesRoot;
            string foliage = root.TrimEnd('/').Substring(0, root.TrimEnd('/').LastIndexOf('/'));
            foreach (string inside in new[]
                     {
                         root,
                         root.TrimEnd('/'),
                         root.ToLowerInvariant(),
                         foliage + "/Other/../Trees/x.png",
                     })
                Assert.IsTrue(TreePass4Baker.IsInsideTheLiveKit(inside), inside);
            foreach (string outside in new[]
                     {
                         foliage + "/TreesX/a.png",
                         TreePass4TempBake.Root + "/x.png",
                         root + "../Shrubs/x.png",
                     })
                Assert.IsFalse(TreePass4Baker.IsInsideTheLiveKit(outside), outside);
        }

        // =====================================================================================
        // the 2048 gate
        // =====================================================================================

        [Test]
        public void EverySheet_FitsUnder2048_OneRowOfFourVariants()
        {
            using var host = TreePass4TempBake.CreateHost();
            var keys = FishingKitBaker.ReadStringArray(host, TreePass4TempBake.SpeciesKeysExpr);
            Assert.IsNotEmpty(keys);
            int largest = 0;
            string largestKey = null;
            foreach (string key in keys)
            {
                var spec = TreePass4Baker.ReadSheetSpec(host, key, Stage, out _);
                Assert.LessOrEqual(spec.SheetW, TreeKitCatalog.ImportSizeCap, $"{key}: width");
                Assert.LessOrEqual(spec.SheetH, TreeKitCatalog.ImportSizeCap, $"{key}: height");
                Assert.AreEqual(Variants, spec.Cols, $"{key}: columns");
                Assert.AreEqual(TreeRigBaker.SwayRowsBaked, spec.Rows, $"{key}: rows");
                Assert.AreEqual(Variants * spec.CellW, spec.SheetW, $"{key}: one row of whole cells");
                Assert.AreEqual(spec.CellH, spec.SheetH, $"{key}: one row of whole cells");
                Assert.DoesNotThrow(() => TreeRigBaker.AssertFits(key, Stage, spec), key);
                int side = Math.Max(spec.SheetW, spec.SheetH);
                if (side > largest) { largest = side; largestKey = key; }
            }
            Debug.Log($"[TreePass4Bake] the largest sheet side is {largestKey}'s {largest} px: " +
                      $"{TreeKitCatalog.ImportSizeCap - largest} px under the {TreeKitCatalog.ImportSizeCap} cap.");

            var wide = new TreeRigBaker.SheetSpec(513, 100, 256, 90, 9, 4, 1, 2052, 400, true, 10f);
            StringAssert.Contains("would bake to",
                Assert.Throws<InvalidOperationException>(() => TreeRigBaker.AssertFits("Probe", Stage, wide)).Message,
                "SABOTAGE: four 513 px cells are 2052 px.");
            var unfit = new TreeRigBaker.SheetSpec(100, 100, 50, 90, 9, 4, 1, 400, 400, false, 10f);
            StringAssert.Contains("fits is FALSE",
                Assert.Throws<InvalidOperationException>(() => TreeRigBaker.AssertFits("Probe", Stage, unfit)).Message,
                "SABOTAGE: the rig's own fits flag.");
        }

        // =====================================================================================
        // the mask keeps OUR order: R key, G back rim, B depth, A coverage
        // =====================================================================================

        /// <summary>
        /// 🔴 The pass-4 mask is OUR packing (<c>TreeRigBakeTests.MaskChannels_AreKeyRimDepthCoverage_…</c>
        /// pins it for pass 3), lit by rig 3's own light, which the glue carries bit for bit: R is the key
        /// light's Lambert term from rig 4's normals, G is the back rim and so is 0 on every texel that
        /// faces away from it, B is rig 4's depth view exactly, and A is the albedo's coverage. Swap R and
        /// G, or B and A, and the checks fail.
        /// </summary>
        [Test]
        public void TheMask_KeepsOurOrder_KeyRimDepthCoverage_ByRig3sLight()
        {
            _run.Require();
            double[] key3, rim3;
            using (var host3 = RigScriptHostFactory.Create())
            {
                host3.Execute(File.ReadAllText(Abs("docs/art/rigs/treeIsoRig3.js")));
                key3 = Vec3(host3, "TreeRig3.LIGHT.key");
                rim3 = Vec3(host3, "TreeRig3.LIGHT.rim");
            }

            var contract = TreePass4TempBake.ReadContract(_run.ContractPath);
            using var host = TreePass4TempBake.CreateHost();
            double[] key4 = Vec3(host, "HHTreePass4.KEY"), rim4 = Vec3(host, "HHTreePass4.RIM");
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(key3[i] == key4[i], $"KEY[{i}]: rig 3 {key3[i]:R}, the glue {key4[i]:R}");
                Assert.IsTrue(rim3[i] == rim4[i], $"RIM[{i}]: rig 3 {rim3[i]:R}, the glue {rim4[i]:R}");
            }
            double hyp = Math.Sqrt(rim3[0] * rim3[0] + rim3[1] * rim3[1]);
            double rs0 = rim3[0] / hyp, rs1 = rim3[1] / hyp;

            var log = new StringBuilder("[TreePass4Bake] mask v0 (covered | B exact | R within 3, worst | away-facing, G>0 there | G>0 | swaps):\n");
            foreach (var e in contract.trees)
                foreach (string season in e.seasons)
                {
                    string at = $"{e.species}/{season}";
                    var row = TreeKitCatalog.SeasonRowFor(e, season);
                    var maskPx = TreePass4TempBake.ReadSheet(TreePass4TempBake.SheetOf(_run, e, row.maps, TreeKitCatalog.Channel.Mask), out int w, out int h);
                    var albPx = TreePass4TempBake.ReadSheet(TreePass4TempBake.SheetOf(_run, e, row.albedo, TreeKitCatalog.Channel.Albedo), out int aw, out int ah);
                    Assert.IsTrue(aw == w && ah == h, $"{at}: the mask and the albedo sheets differ in size");
                    byte[] mask = TreePass4TempBake.CellFromSheet(maskPx, w, h, e.cellW, e.cellH, 0);
                    byte[] alb = TreePass4TempBake.CellFromSheet(albPx, w, h, e.cellW, e.cellH, 0);

                    host.Execute($"globalThis.HHTestRF = TreeMaps4.rest({TreePass4TempBake.Js(e.species)}, " +
                                 $"{{ stage: {TreePass4TempBake.Js(Stage)}, season: {TreePass4TempBake.Js(row.maps)}, variant: 0 }});");
                    Assert.AreEqual(e.cellW, (int)host.EvaluateNumber("HHTestRF.v.w"), $"{at}: the rest frame's width");
                    Assert.AreEqual(e.cellH, (int)host.EvaluateNumber("HHTestRF.v.h"), $"{at}: the rest frame's height");
                    byte[] nrm = host.EvaluateBytes("TreeRig4.view(HHTestRF, 'normal')");
                    byte[] dep = host.EvaluateBytes("TreeRig4.view(HHTestRF, 'depth')");
                    int n = e.cellW * e.cellH;
                    Assert.AreEqual(n * 4, nrm.Length, $"{at}: normal view");
                    Assert.AreEqual(n * 4, dep.Length, $"{at}: depth view");

                    int cov = 0, aBad = 0, baBad = 0, b0 = 0, b1 = 0, bBad = 0, rIn3 = 0, rWorst = 0;
                    int away = 0, awayG = 0, awayR = 0, gPos = 0, rgDiff = 0, gIn3 = 0;
                    for (int i = 0; i < n; i++)
                    {
                        int o = i * 4;
                        if (mask[o + 3] != alb[o + 3]) aBad++;
                        if (mask[o + 2] != alb[o + 3]) baBad++;
                        if (alb[o + 3] == 0) continue;
                        cov++;
                        int db = Math.Abs(mask[o + 2] - dep[o]);
                        if (db == 0) b0++; else if (db == 1) b1++; else bBad++;
                        double nx = nrm[o] / 255.0 * 2 - 1, ny = -(nrm[o + 1] / 255.0 * 2 - 1), nz = nrm[o + 2] / 255.0 * 2 - 1;
                        int lam = (int)Math.Round(Math.Pow(Math.Max(0, nx * key3[0] + ny * key3[1] + nz * key3[2]), 1.35) * 255);
                        int dr = Math.Abs(mask[o] - lam);
                        if (dr <= 3) rIn3++;
                        if (dr > rWorst) rWorst = dr;
                        if (Math.Abs(mask[o + 1] - lam) <= 3) gIn3++;
                        double nl = Math.Sqrt(nx * nx + ny * ny);
                        if (nl > 0.05 && (nx * rs0 + ny * rs1) / nl < -0.05)
                        {
                            away++;
                            if (mask[o + 1] != 0) awayG++;
                            if (mask[o] != 0) awayR++;
                        }
                        if (mask[o + 1] > 0) gPos++;
                        if (mask[o] != mask[o + 1]) rgDiff++;
                    }

                    Assert.Greater(cov, 0, $"{at}: nothing covered");
                    Assert.AreEqual(0, aBad, $"{at}: the mask's A is not the albedo's coverage");
                    Assert.AreEqual(cov, b0, $"{at}: the mask's B is not rig 4's depth exactly (±1 {b1}, worse {bBad})");
                    Assert.GreaterOrEqual(rIn3, 0.99 * cov, $"{at}: R within 3 of the key light on {rIn3} of {cov} (worst {rWorst})");
                    Assert.GreaterOrEqual(away, 100, $"{at}: too few texels face away from the rim to prove G");
                    Assert.AreEqual(0, awayG, $"{at}: G lights {awayG} of {away} texels that face away from the rim");
                    Assert.Greater(gPos, 0, $"{at}: no rim at all");

                    Assert.Greater(rgDiff, 0.5 * cov, $"SABOTAGE {at}: R and G agree on {cov - rgDiff} of {cov}; a swap would pass.");
                    Assert.Less(gIn3, 0.99 * cov, $"SABOTAGE {at}: with R and G swapped the key check must fail.");
                    Assert.Greater(awayR, 0, $"SABOTAGE {at}: with R and G swapped the away-facing check must fail.");
                    Assert.Greater(baBad, 0, $"SABOTAGE {at}: with B and A swapped the coverage check must fail.");

                    log.AppendLine($"  {at}: {cov} | {b0} | {100.0 * rIn3 / cov:F2}%, {rWorst} | {away}, {awayG} | {gPos} | " +
                                   $"R≠G {100.0 * rgDiff / cov:F1}%, swapped G-as-key {100.0 * gIn3 / cov:F1}%, swapped away R>0 {awayR}, B↔A {baBad}");
                }
            host.Execute("HHTreePass4.release(); TreeRig4.clearCache(); delete globalThis.HHTestRF;");
            Debug.Log(log.ToString());
        }

        static double[] Vec3(IRigScriptHost host, string expr) =>
            new[] { host.EvaluateNumber(expr + "[0]"), host.EvaluateNumber(expr + "[1]"), host.EvaluateNumber(expr + "[2]") };
    }
}
