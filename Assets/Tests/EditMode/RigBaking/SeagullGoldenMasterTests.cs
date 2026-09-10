using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// PR 1 STEP 5 — THE CI GOLDEN.
    ///
    /// Re-bakes the seagull from the committed rig and asserts that the COMMITTED SHEET is what
    /// that bake produces, cell for cell, on twenty-four named cells: <c>fly</c> f0,
    /// <c>stand</c> f0 and <c>splash</c> f2 at every one of the eight facings. One flying pose and
    /// one standing pose per direction is the cheapest pair that still moves if the facing order,
    /// the pivot, the cell grid or the convention correction changes.
    ///
    /// <para><b>It compares against the COMMITTED sheet, never against the drop's own
    /// <c>Seagull.png</c>.</b> Those two are not the same file: the drop's bake is stale by ±1 in
    /// the water-tinted band of the four <c>waterZ:0</c> states (39 of 384 cells, 97 pixels, alpha
    /// identical throughout), which is recorded on the intake PR. Pointing this fixture at the drop
    /// would wire a known discrepancy into CI as if it were the standard.</para>
    ///
    /// <para><b>Why <c>splash</c> f2 is in the list.</b> The dry poses cannot tell the two sheets
    /// apart — MEASURED: <c>fly</c> f0 and <c>stand</c> f0 are byte-identical between our bake and
    /// the drop's at all eight facings, so a golden built only from them would stay green if the
    /// drop's stale sheet were ever committed by mistake. <c>splash</c> f2 (column 18) is the one
    /// cell that differs in ALL EIGHT rows, so it is what makes "our bake, never the drop"
    /// something CI actually enforces rather than something the PR body merely asserts.</para>
    ///
    /// <para><b>Why the expected numbers are not read off the sheet.</b> Everything asserted here is
    /// derived from RUNNING THE RIG — the freshly baked pixels, and an opaque-pixel table measured
    /// from that bake. A fixture that took its bar from the artefact it is checking would be a
    /// mirror and would pass on any sheet at all. Cf. the note in
    /// <see cref="PuntGoldenMasterTests"/> about re-baking the shipped punt in engine.</para>
    ///
    /// <para><b>The blank-raster defence.</b> Pixel equality alone is vacuous if both sides are
    /// empty — a bake that renders nothing matches a sheet of nothing perfectly. Three controls
    /// close that: every golden cell must carry its MEASURED opaque-pixel count, the twenty-four
    /// cells must be mutually DISTINCT, and the whole sheet must clear a total-ink floor. A blank
    /// sheet fails the first, a flat fill fails the second.</para>
    ///
    /// <para>A fourth closes the gap between them: <b>every one of the 384 cells</b> must clear a
    /// per-cell floor. The three controls above all pass on a sheet whose twenty-four golden cells
    /// are perfect and whose other 360 are empty — twenty-four correct cells is plenty of total
    /// ink — so the sampled cells alone cannot speak for the sheet.</para>
    /// </summary>
    public class SeagullGoldenMasterTests
    {
        const string CommittedSheet = "Assets/_Project/Art/Sprites/Creatures/Seagull.png";

        const int CellW = 64, CellH = 64, Columns = 48, Dirs = 8;
        const int SheetW = Columns * CellW;   // 3072
        const int SheetH = Dirs * CellH;      // 512

        /// <summary>Columns of <c>fly</c> f0, <c>stand</c> f0 and <c>splash</c> f2 in
        /// <c>sheetOrder()</c>.</summary>
        const int FlyF0Column = 0, StandF0Column = 34, SplashF2Column = 18;

        /// <summary>Opaque pixels in each golden cell, indexed by facing, measured from the rig's
        /// own bake. These are the blank-raster control: an empty cell reads 0 and cannot pass.</summary>
        static readonly int[] FlyF0Opaque    = { 162, 165, 159, 154, 144, 154, 159, 165 };
        static readonly int[] StandF0Opaque  = {  70,  84, 101,  93,  87,  92, 100,  85 };
        static readonly int[] SplashF2Opaque = { 162, 167, 162, 154, 141, 149, 159, 169 };

        /// <summary>Floor on the sheet's total ink. The bake renders no blank cells at all across
        /// the 384, so a sheet that has lost most of its art trips this long before the per-cell
        /// table gets subtle.</summary>
        const int MinTotalOpaque = 20000;

        /// <summary>Floor on EVERY cell's ink, not just the twenty-four golden ones. The total-ink
        /// control above is a sheet-wide sum, so a sheet whose golden cells are perfect and whose
        /// other 360 are blank clears it comfortably; this closes that. Measured on the bake: the
        /// thinnest of the 384 carries 65 opaque pixels (<c>preen</c> f3, facing 5), the fattest
        /// 217, and not one is blank. The floor sits at a third of the thinnest so a re-loft has
        /// room to move before it needs revisiting.</summary>
        const int MinCellOpaque = 24;

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        [Test]
        public void TheCommittedSheet_IsExactlyWhatTheRigBakes_OnTwentyFourGoldenCells()
        {
            string committedPath = Path.Combine(RepoRoot, CommittedSheet);
            Assert.IsTrue(File.Exists(committedPath),
                $"The committed seagull sheet is missing at {CommittedSheet}. This golden and the " +
                "sheet land in the SAME commit on purpose — a golden without its sheet is a red " +
                "that says nothing. Bake with Hidden Harbours/Art/Bake Seagull and commit both.");

            // Bake to scratch, OUTSIDE Assets. SeagullBaker.WriteSheet is plain file IO, so this
            // neither imports an asset nor touches the shipped sheet it is about to check.
            string outFolder = "artifacts/seagull-golden";
            Directory.CreateDirectory(Path.Combine(RepoRoot, outFolder));
            var result = SeagullBaker.Bake(outFolder);

            Assert.AreEqual(1, result.Sheets.Count, "The seagull is one page by construction.");
            var page = result.Sheets[0];
            Assert.AreEqual(SheetW, page.Width,  "Baked sheet width moved — the column list changed.");
            Assert.AreEqual(SheetH, page.Height, "Baked sheet height moved — the facing count changed.");

            Texture2D baked = null, committed = null;
            try
            {
                baked = Decode(File.ReadAllBytes(Path.Combine(RepoRoot, page.AssetPath)), "the fresh bake");
                committed = Decode(File.ReadAllBytes(committedPath), "the committed sheet");

                Assert.AreEqual(SheetW, committed.width,
                    "The committed sheet is not 3072 px wide. If it reads 2048 or less it was " +
                    "imported under Unity's default texture cap and SILENTLY DOWNSCALED — the " +
                    "sprite count still looks right when that happens. See requiredMaxTextureSize " +
                    "in the contract.");
                Assert.AreEqual(SheetH, committed.height, "The committed sheet is not 512 px tall.");

                var a = baked.GetPixels32();
                var b = committed.GetPixels32();

                var digests = new Dictionary<string, string>();

                foreach (var probe in GoldenCells())
                {
                    var bakedCell = CellOf(a, probe.Dir, probe.Column);
                    var shippedCell = CellOf(b, probe.Dir, probe.Column);

                    int differing = 0;
                    for (int i = 0; i < bakedCell.Length; i++)
                    {
                        var p = bakedCell[i];
                        var q = shippedCell[i];
                        if (p.r != q.r || p.g != q.g || p.b != q.b || p.a != q.a) differing++;
                    }

                    int opaque = 0;
                    foreach (var p in shippedCell) if (p.a != 0) opaque++;

                    Debug.Log($"[seagull-golden] {probe.Label} d{probe.Dir} (col {probe.Column}): " +
                              $"{differing} differing px, {opaque} opaque");

                    Assert.AreEqual(0, differing,
                        $"{probe.Label} at facing {probe.Dir} (column {probe.Column}) does not match " +
                        "the committed sheet — the rig now bakes something else there. If EVERY " +
                        "golden cell moved, the rig or the baker changed and the sheet needs " +
                        "re-baking and re-committing. If ONE moved, suspect that cell's pose.");

                    // ── the blank-raster control ────────────────────────────────────────────
                    // Exactness above is satisfied by two empty rasters. This is not.
                    Assert.AreEqual(probe.ExpectedOpaque, opaque,
                        $"{probe.Label} at facing {probe.Dir} carries {opaque} opaque pixels, not " +
                        $"the {probe.ExpectedOpaque} the rig renders. Zero means a BLANK cell — a " +
                        "blank sheet passes a pixel-equality check against a blank bake, which is " +
                        "why this table exists. A count from a DIFFERENT facing means the eight " +
                        "rows are in the wrong order (this rig is CLOCKWISE: row r = dir r).");

                    digests[$"{probe.Label} d{probe.Dir}"] = Digest(shippedCell);
                }

                // ── the flat-fill control ──────────────────────────────────────────────────
                var distinct = new HashSet<string>(digests.Values);
                Assert.AreEqual(digests.Count, distinct.Count,
                    "Two golden cells are byte-identical. Eight facings of one pose must all differ, " +
                    "and a flying gull cannot equal a standing or a splashing one — identical cells mean a flat " +
                    "fill, a stuck facing index, or the same cell blitted twenty-four times.");

                // ── the total-ink control ──────────────────────────────────────────────────
                int totalOpaque = 0;
                foreach (var p in b) if (p.a != 0) totalOpaque++;
                Debug.Log($"[seagull-golden] committed sheet total opaque pixels: {totalOpaque}");
                Assert.Greater(totalOpaque, MinTotalOpaque,
                    $"The committed sheet holds only {totalOpaque} opaque pixels. The bake renders " +
                    "no blank cells across all 384, so a sheet this empty has lost its art.");

                // ── the per-cell ink floor: every one of the 384, not just the twenty-four ──
                int thinnest = int.MaxValue, thinDir = -1, thinColumn = -1;
                for (int dir = 0; dir < Dirs; dir++)
                    for (int column = 0; column < Columns; column++)
                    {
                        int ink = 0;
                        foreach (var p in CellOf(b, dir, column)) if (p.a != 0) ink++;
                        if (ink >= thinnest) continue;
                        thinnest = ink;
                        thinDir = dir;
                        thinColumn = column;
                    }

                Debug.Log($"[seagull-golden] thinnest of the 384 cells: {thinnest} opaque px " +
                          $"at facing {thinDir}, column {thinColumn}.");
                Assert.GreaterOrEqual(thinnest, MinCellOpaque,
                    $"Cell at facing {thinDir}, column {thinColumn} carries {thinnest} opaque " +
                    $"pixels — under the {MinCellOpaque} floor, and 0 means it is BLANK. The " +
                    "twenty-four golden cells and the sheet-wide ink total both pass on a sheet " +
                    "whose other 360 cells never got drawn; this is the check that does not.");
            }
            finally
            {
                if (baked != null) UnityEngine.Object.DestroyImmediate(baked);
                if (committed != null) UnityEngine.Object.DestroyImmediate(committed);
            }
        }

        readonly struct GoldenCell
        {
            public readonly string Label;
            public readonly int Dir, Column, ExpectedOpaque;
            public GoldenCell(string label, int dir, int column, int expectedOpaque)
            { Label = label; Dir = dir; Column = column; ExpectedOpaque = expectedOpaque; }
        }

        static IEnumerable<GoldenCell> GoldenCells()
        {
            for (int d = 0; d < Dirs; d++)
                yield return new GoldenCell("fly f0", d, FlyF0Column, FlyF0Opaque[d]);
            for (int d = 0; d < Dirs; d++)
                yield return new GoldenCell("stand f0", d, StandF0Column, StandF0Opaque[d]);
            // The discriminator: the one cell that differs between our bake and the drop's in all
            // eight rows. Without it "never the drop" is unenforced. See the class remarks.
            for (int d = 0; d < Dirs; d++)
                yield return new GoldenCell("splash f2", d, SplashF2Column, SplashF2Opaque[d]);
        }

        /// <summary>
        /// Pulls one 64×64 cell out of a decoded sheet.
        ///
        /// <para><b>The row flip is load-bearing.</b> The baker blits facing <c>d</c> as
        /// <c>rowFromTop: d</c>, but <see cref="Texture2D.GetPixels32"/> hands back a BOTTOM-UP
        /// buffer where index 0 is the bottom-left pixel. Facing d therefore starts at
        /// <c>(Dirs-1-d) * CellH</c> from the bottom. Getting this wrong still passes the equality
        /// check — both sheets flip together — while quietly mislabelling every facing, which is
        /// exactly what the per-facing opaque table above is there to catch.</para>
        /// </summary>
        static Color32[] CellOf(Color32[] sheet, int dir, int column)
        {
            var cell = new Color32[CellW * CellH];
            int y0 = (Dirs - 1 - dir) * CellH;
            int x0 = column * CellW;
            for (int y = 0; y < CellH; y++)
                Array.Copy(sheet, (y0 + y) * SheetW + x0, cell, y * CellW, CellW);
            return cell;
        }

        /// <summary>FNV-1a over the cell's bytes. Only ever compared with another digest from this
        /// same function, so the choice of hash carries no weight beyond being order-sensitive.</summary>
        static string Digest(Color32[] cell)
        {
            ulong h = 14695981039346656037UL;
            foreach (var p in cell)
            {
                h = (h ^ p.r) * 1099511628211UL;
                h = (h ^ p.g) * 1099511628211UL;
                h = (h ^ p.b) * 1099511628211UL;
                h = (h ^ p.a) * 1099511628211UL;
            }
            return h.ToString("x16");
        }

        /// <summary>Decodes a PNG into a throwaway texture, so neither sheet needs isReadable.</summary>
        static Texture2D Decode(byte[] png, string what)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            Assert.IsTrue(t.LoadImage(png, markNonReadable: false), $"Failed to decode {what} as a PNG.");
            return t;
        }
    }
}
