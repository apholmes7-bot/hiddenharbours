using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The two halves of the interiors kit must describe ONE kit — and the pixels must be indexed
    /// the way they were actually baked.</b>
    ///
    /// <para>The kit is declared twice: once in the DEFS (<c>Data/Boats/Interiors/*.asset</c>, built
    /// from the art director's sidecars) and once in the SHEETS CONTRACT
    /// (<c>Art/Boats/Interiors/BoatInteriors.json</c>, written by the bake). Nothing compared them, so
    /// they were free to drift apart silently, and a runtime that trusts both would draw a room
    /// measured by one half at coordinates meant for the other.</para>
    ///
    /// <para><b>Why the sha comparison is not <c>AreEqual</c>.</b> No <c>.gitattributes</c> rule covers
    /// <c>docs/art/rigs/**/*.js</c> and <c>core.autocrlf</c> is true on Windows, so the rig is stored
    /// LF and checked out CRLF — ONE file with TWO digests. The defs transcribe the sidecar's pin,
    /// which is the LF form (the convention <c>docs/art/rigs/gameplay/README.md</c> prescribes); the
    /// bake computes its own from <c>File.ReadAllBytes</c>, which on this platform is the CRLF form.
    /// Both are correct descriptions of the same rig. Asserting raw equality of the two would be red
    /// on every machine for ever and no rebuild could turn it green, because each builder re-derives
    /// its own convention's digest. So the property asserted here is the real one — <b>both halves pin
    /// the rig that is actually on disk</b> — through <see cref="DeckSidecarReader.MatchRigHash"/>,
    /// which accepts either line-ending form and refuses anything else.</para>
    /// </summary>
    public class BoatInteriorDefContractAgreementTests
    {
        const string InteriorDefFolder = "Assets/_Project/Data/Boats/Interiors";

        static string RepoRoot => Directory.GetParent(Application.dataPath).FullName;

        static BoatInteriorKit.Contract _contract;
        static byte[] _interiorRigBytes;

        [OneTimeSetUp]
        public void LoadOnce()
        {
            string contractAbs = Path.Combine(RepoRoot, BoatInteriorKit.ContractPath);
            if (File.Exists(contractAbs))
                _contract = JsonUtility.FromJson<BoatInteriorKit.Contract>(File.ReadAllText(contractAbs));

            string rigAbs = Path.Combine(RepoRoot, BoatInteriorKit.KitFolder,
                                         BoatInteriorKit.InteriorRigFileName);
            if (File.Exists(rigAbs)) _interiorRigBytes = File.ReadAllBytes(rigAbs);
        }

        /// <summary>One case per committed def, so a per-hull failure names its own boat.</summary>
        static IEnumerable<TestCaseData> CommittedDefs()
        {
            string[] guids = AssetDatabase.FindAssets("t:BoatInteriorDef", new[] { InteriorDefFolder });
            if (guids == null || guids.Length == 0)
            {
                yield return new TestCaseData((string)null).Ignore(
                    $"no BoatInteriorDef assets under {InteriorDefFolder} — nothing to enumerate.");
                yield break;
            }

            // ADR 0041: a converted hull keeps her def and has NO sheet — there is nothing for the
            // two halves to agree on, so she is not a case here. Derived from the bake's switch.
            var converted = new HashSet<string>(ConvertedInteriors.All().Select(c => c.DefAssetPath),
                                                StringComparer.Ordinal);

            foreach (string path in guids.Select(AssetDatabase.GUIDToAssetPath)
                                         .Where(p => !converted.Contains(p))
                                         .OrderBy(p => p, StringComparer.Ordinal))
                yield return new TestCaseData(path).SetName($"Def_{Path.GetFileNameWithoutExtension(path)}");
        }

        static BoatInteriorKit.Contract Contract()
        {
            if (_contract == null)
                Assert.Fail($"no sheet contract at {BoatInteriorKit.ContractPath}.");
            return _contract;
        }

        static BoatInteriorDef Def(string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(path);
            Assert.NotNull(def, $"'{path}' did not load as a BoatInteriorDef.");
            return def;
        }

        static BoatInteriorKit.SheetEntry SheetFor(BoatInteriorDef def)
        {
            var sheet = Contract().sheets.FirstOrDefault(s => s.defId == def.Id);
            Assert.NotNull(sheet,
                $"def '{def.Id}' has no entry in {BoatInteriorKit.ContractPath}. A def with no sheet " +
                "is a room the runtime will try to draw and find no pixels for.");
            return sheet;
        }

        // ---- the rig both halves claim to describe -----------------------------------------------

        [Test, TestCaseSource(nameof(CommittedDefs))]
        public void DefAndContract_PinTheRigThatIsOnDisk(string path)
        {
            BoatInteriorDef def = Def(path);
            BoatInteriorKit.SheetEntry sheet = SheetFor(def);

            Assert.NotNull(_interiorRigBytes,
                $"{BoatInteriorKit.KitFolder}/{BoatInteriorKit.InteriorRigFileName} is not on disk, so " +
                "neither half's pin can be checked against anything.");

            RigHashMatch defMatch = DeckSidecarReader.MatchRigHash(
                _interiorRigBytes, def.InteriorRigSha256, out string actual);
            Assert.AreNotEqual(RigHashMatch.None, defMatch,
                $"def '{def.Id}' pins interior rig {Short(def.InteriorRigSha256)} but " +
                $"{BoatInteriorKit.InteriorRigFileName} hashes to {Short(actual)} (and to neither of its " +
                "line-ending normalisations). The renderer has changed since this def was built — " +
                "re-run BoatInteriorDefBuilder.BuildAllCli.");

            RigHashMatch sheetMatch = DeckSidecarReader.MatchRigHash(
                _interiorRigBytes, Contract().interiorRigSha256, out _);
            Assert.AreNotEqual(RigHashMatch.None, sheetMatch,
                $"the sheets contract pins interior rig {Short(Contract().interiorRigSha256)}, which is not " +
                $"{BoatInteriorKit.InteriorRigFileName} in any line-ending form. The sheets were baked " +
                "from a renderer this repo no longer holds — re-run BoatInteriorBakeMenu.BakeAndSliceCli.");
        }

        // ---- the cell the room is drawn in -------------------------------------------------------

        [Test, TestCaseSource(nameof(CommittedDefs))]
        public void DefAndContract_AgreeOnCellPivotAndScale(string path)
        {
            BoatInteriorDef def = Def(path);
            BoatInteriorKit.SheetEntry sheet = SheetFor(def);

            Assert.AreEqual(sheet.cellW, def.CellPixels.x,
                $"'{def.Id}': the def measures a {def.CellPixels.x} px cell and the sheet bakes " +
                $"{sheet.cellW}. Same room, two canvases — the cabin composites offset from its boat.");
            Assert.AreEqual(sheet.cellH, def.CellPixels.y,
                $"'{def.Id}': def cell height {def.CellPixels.y} px, sheet {sheet.cellH}.");

            Assert.AreEqual(sheet.pivotX, def.PivotPixels.x,
                $"'{def.Id}': the def pivots at x={def.PivotPixels.x} and the sheet at x={sheet.pivotX}. " +
                "The room would slide across the deck as she turns.");
            Assert.AreEqual(sheet.pivotY, def.PivotPixels.y,
                $"'{def.Id}': def pivot y={def.PivotPixels.y}, sheet y={sheet.pivotY}.");

            Assert.AreEqual(sheet.pixelsPerMetre, def.PixelsPerMetre,
                $"'{def.Id}': the def is {def.PixelsPerMetre} px/m and the sheet {sheet.pixelsPerMetre}. " +
                "Two pixel grids live in this kit (the tanker is 16 where the fleet is 32) and a " +
                "mismatch scales the whole room.");
        }

        // ---- levels are not rows, and the map between them must be total -------------------------

        [Test, TestCaseSource(nameof(CommittedDefs))]
        public void DefLevels_MapOntoSheetRows_Totally(string path)
        {
            BoatInteriorDef def = Def(path);
            BoatInteriorKit.SheetEntry sheet = SheetFor(def);

            var cells = Resources.Load<BoatInteriorCellsDef>(BoatInteriorCellsDef.PathFor(def.Id));
            Assert.NotNull(cells,
                $"'{def.Id}' has no cells asset under Resources/{BoatInteriorCellsDef.ResourcesFolder}. " +
                "The cabin loads its pixels from there at the door's cue — with none, it draws nothing.");

            Assert.AreEqual(sheet.facings, cells.Facings,
                $"'{def.Id}': the sheet bakes {sheet.facings} facings, the cells asset says " +
                $"{cells.Facings}. The index arithmetic (row * facings + facing) reads the wrong cell.");

            // ⚠️ DEF LEVELS ARE NOT SHEET ROWS, and the map is indexed by the FORMER. A hull may
            // declare a level the sheets do not draw — an open deck is a walkable with no room — and
            // that level maps to −1. Six of the twenty-seven are legitimately in this shape (the
            // packet, the dragger, both trawlers, the convertible and the tanker), so the map's LENGTH
            // must equal the def's level count and its VALUES must index the sheet's rows.
            Assert.AreEqual(def.Levels.Length, cells.CellRowForLevel.Length,
                $"'{def.Id}': the def declares {def.Levels.Length} levels but the row map has " +
                $"{cells.CellRowForLevel.Length} entries. The map is indexed BY DEF LEVEL, so a short " +
                "one leaves a level with no answer and BoatInterior refuses the whole sheet.");

            bool anyDrawn = false;
            for (int i = 0; i < cells.CellRowForLevel.Length; i++)
            {
                int row = cells.CellRowForLevel[i];
                Assert.That(row, Is.InRange(-1, sheet.levels.Length - 1),
                    $"'{def.Id}': level {i} ('{def.Levels[i].Id}') maps to row {row}, outside the " +
                    $"sheet's {sheet.levels.Length} rows. −1 — this level has no room to draw — is the " +
                    "only value below zero that means anything.");
                if (row >= 0) anyDrawn = true;
            }

            Assert.IsTrue(anyDrawn,
                $"'{def.Id}': every level maps to −1, so this hull has a cells asset that draws " +
                "nothing at all.");

            Assert.IsTrue(cells.IsUsableFor(def),
                $"'{def.Id}': the cells asset does not fit its own def — rows, map or array disagree. " +
                "BoatInterior refuses a partly wired sheet whole, so the room simply never appears.");
        }

        static string Short(string sha) =>
            string.IsNullOrEmpty(sha) ? "(none)" : sha.Substring(0, Math.Min(12, sha.Length)) + "…";
    }

    /// <summary>
    /// <b>The cells come off the press CLOCKWISE, and the runtime must index them that way.</b>
    ///
    /// <para>Every interior cell is rendered through
    /// <see cref="RigBaker.DirForCell"/>, which maps cell <c>k</c> to dir <c>(facings−k)%facings</c>
    /// for a counter-clockwise rig and to <c>k</c> for a clockwise one. <b>The correction happens at
    /// BAKE time</b>, so whatever the rig's handedness the CELLS are canonical: cell <c>i</c> depicts
    /// <c>+45°·i</c>. There is nothing left for the runtime to un-mirror, and
    /// <c>BoatInteriorCellsDef.CellsAreCounterClockwise</c> — the flag
    /// <c>IsoFacing.HeadingToFacingIndex</c> uses to reverse an index — must therefore be FALSE.</para>
    ///
    /// <para><b>The bug this exists to catch.</b> <c>BoatInteriorVisualWiring</c> used to set that flag
    /// from the contract's <c>convention</c> field. But <c>convention</c> records the RIG's handedness —
    /// "the convention the cells were corrected BY" — an INPUT, not a description of the output. Feeding
    /// it to the runtime mirrored an already-correct sheet, and all 27 hulls shipped drawing the wrong
    /// facing at every heading except the two that are their own mirror. It reached the owner's eye as
    /// a cabin composited on the wrong part of the boat. It is the same failure
    /// <c>BoatVisualLibraryBuilder</c>'s LobsterBoatIso block calls "the precise bug that shipped five
    /// times in this project before anyone measured".</para>
    ///
    /// <para><b>So this measures, and does not restate a constant.</b> The room sits off the hull's
    /// origin, so across the facings its ink centroid sweeps the iso projection of a circle — an
    /// ellipse. Un-squash the ellipse and the ground bearing steps by a clean ±45°; the SIGN of that
    /// step is the handedness of the pixels as baked. No labels, no silhouette taper heuristic (which
    /// called all eighteen lobster variants backwards once already), no exterior sheet.</para>
    /// </summary>
    public class BoatInteriorCellHandednessTests
    {
        /// <summary>Measured across the shipped fleet: 40.9°–49.5° per step. Tolerance leaves headroom
        /// without admitting a 90° error or a degenerate reading.</summary>
        const double StepToleranceDegrees = 8.0;

        /// <summary>The smallest x semi-axis on the shipped fleet is 52 px. Below this there is no
        /// sweep to take a sign from, and the test says so rather than reading noise.</summary>
        const double MinimumSweepPixels = 20.0;

        static string RepoRoot => Directory.GetParent(Application.dataPath).FullName;

        static BoatInteriorKit.Contract _contract;

        [OneTimeSetUp]
        public void LoadOnce()
        {
            string contractAbs = Path.Combine(RepoRoot, BoatInteriorKit.ContractPath);
            if (File.Exists(contractAbs))
                _contract = JsonUtility.FromJson<BoatInteriorKit.Contract>(File.ReadAllText(contractAbs));
        }

        static IEnumerable<TestCaseData> ShippedHulls()
        {
            string contractAbs = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                              BoatInteriorKit.ContractPath);
            if (!File.Exists(contractAbs))
            {
                yield return new TestCaseData("(no contract)").Ignore("no sheet contract to enumerate.");
                yield break;
            }

            var c = JsonUtility.FromJson<BoatInteriorKit.Contract>(File.ReadAllText(contractAbs));
            foreach (var s in c.sheets.OrderBy(s => s.hullFileStem, StringComparer.Ordinal))
                yield return new TestCaseData(s.hullStem).SetName($"Hull_{s.hullFileStem}");
        }

        static BoatInteriorKit.SheetEntry SheetFor(string hullStem)
        {
            Assert.NotNull(_contract, $"no sheet contract at {BoatInteriorKit.ContractPath}.");
            var sheet = _contract.sheets.FirstOrDefault(s => s.hullStem == hullStem);
            Assert.NotNull(sheet, $"no contract entry for '{hullStem}'.");
            return sheet;
        }

        // ---- the measurement ---------------------------------------------------------------------

        /// <summary>Alpha centroid of one flat cell, in cell-local pixels with <b>+y UP</b> (the
        /// convention <see cref="Texture2D.GetPixels32"/> already returns rows in). Null when the cell
        /// holds no ink at all.</summary>
        static Vector2? CentroidOf(BoatInteriorKit.SheetEntry sheet, int flatCell,
                                   ref string loadedFile, ref Color32[] loaded,
                                   ref int loadedW, ref int loadedH)
        {
            foreach (var page in sheet.pages)
            {
                if (flatCell < page.firstCell || flatCell >= page.firstCell + page.cellCount) continue;

                if (loadedFile != page.file)
                {
                    string abs = Path.Combine(RepoRoot, page.AssetPath);
                    Assert.IsTrue(File.Exists(abs), $"'{page.AssetPath}' is not on disk.");

                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
                    try
                    {
                        Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(abs)),
                                      $"'{page.file}' would not decode as a PNG.");
                        Assert.AreEqual(page.sheetW, tex.width,
                            $"'{page.file}' decodes {tex.width} px wide where the bake wrote {page.sheetW}.");
                        Assert.AreEqual(page.sheetH, tex.height,
                            $"'{page.file}' decodes {tex.height} px tall where the bake wrote {page.sheetH}.");
                        loaded = tex.GetPixels32();
                        loadedW = tex.width; loadedH = tex.height; loadedFile = page.file;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(tex); }
                }

                int indexOnPage = flatCell - page.firstCell;
                int col = indexOnPage % page.columns;
                int rowFromTop = indexOnPage / page.columns;

                int cw = sheet.cellW, ch = sheet.cellH;
                int x0 = col * cw;
                // GetPixels32 is BOTTOM-origin; the bake blits rows from the TOP.
                int y0 = loadedH - (rowFromTop + 1) * ch;

                double sx = 0, sy = 0; long n = 0;
                for (int y = 0; y < ch; y++)
                {
                    int rowStart = (y0 + y) * loadedW + x0;
                    for (int x = 0; x < cw; x++)
                    {
                        if (loaded[rowStart + x].a == 0) continue;
                        sx += x; sy += y; n++;      // y already counts UPWARD
                    }
                }
                return n == 0 ? (Vector2?)null : new Vector2((float)(sx / n), (float)(sy / n));
            }

            Assert.Fail($"flat cell {flatCell} of '{sheet.hullStem}' is on no page the contract lists.");
            return null;
        }

        [Test, TestCaseSource(nameof(ShippedHulls))]
        public void ShippedCells_AreBakedCanonicallyClockwise(string hullStem)
        {
            BoatInteriorKit.SheetEntry sheet = SheetFor(hullStem);
            int facings = sheet.facings;
            Assert.GreaterOrEqual(facings, 4, $"'{hullStem}': {facings} facings is too few to take a sign from.");

            string file = null; Color32[] pixels = null; int pw = 0, ph = 0;
            var cents = new Vector2[facings];
            for (int f = 0; f < facings; f++)
            {
                // Level 0 occupies flat cells 0..facings-1 (CellIndex is LEVEL-major, FACING-minor).
                Vector2? c = CentroidOf(sheet, f, ref file, ref pixels, ref pw, ref ph);
                Assert.IsTrue(c.HasValue,
                    $"'{hullStem}': level '{sheet.levels[0]}' facing {f} has no ink at all. An empty " +
                    "cell is a bake that did not finish, not a room with nothing in it.");
                cents[f] = c.Value;
            }

            var centre = new Vector2(cents.Average(p => p.x), cents.Average(p => p.y));
            double ax = cents.Max(p => Math.Abs(p.x - centre.x));
            double ay = cents.Max(p => Math.Abs(p.y - centre.y));

            Assert.Greater(ax, MinimumSweepPixels,
                $"'{hullStem}': the ink centroid sweeps only {ax:F1} px across {facings} facings, so " +
                "there is no ellipse to read a handedness from. Either the room is centred exactly on " +
                "the hull's origin or every facing rendered the same picture.");

            double k = ay / ax;                              // measured iso foreshortening (~0.643)
            Assert.Greater(k, 0.05, $"'{hullStem}': the sweep is flat (k={k:F3}); it is not an iso ellipse.");

            var bearings = new double[facings];
            for (int i = 0; i < facings; i++)
            {
                double dx = cents[i].x - centre.x;
                double dy = (cents[i].y - centre.y) / k;     // un-squash the ground plane
                bearings[i] = (Math.Atan2(dx, dy) * Mathf.Rad2Deg + 360.0) % 360.0;   // 0 = up, +ve CW
            }

            double expected = 360.0 / facings;
            int positive = 0;
            for (int i = 0; i < facings; i++)
            {
                double step = bearings[(i + 1) % facings] - bearings[i];
                step = ((step + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;             // wrap to (−180,180]
                if (step > 0) positive++;

                Assert.AreEqual(expected, Math.Abs(step), StepToleranceDegrees,
                    $"'{hullStem}': facing {i}→{(i + 1) % facings} steps {Math.Abs(step):F1}°, not " +
                    $"{expected:F0}°. The turntable is not what this kit assumes, so the SIGN below " +
                    "cannot be trusted either.");
            }

            Assert.That(positive, Is.EqualTo(facings).Or.EqualTo(0),
                $"'{hullStem}': the facings do not rotate consistently — {positive} of {facings} steps " +
                "advance one way. A sheet with a jumbled facing order cannot be indexed at all.");

            Assert.AreEqual(facings, positive,
                $"'{hullStem}': the shipped cells sweep COUNTER-CLOCKWISE. Every cell is rendered " +
                "through RigBaker.DirForCell, which corrects the rig's handedness at bake time, so the " +
                "cells must come off the press clockwise (cell i depicts +45°·i). If this fired, the " +
                "bake stopped applying that correction — fix it there, not by flipping the runtime flag.");
        }

        [Test, TestCaseSource(nameof(ShippedHulls))]
        public void CellsAsset_DoesNotAskTheRuntimeToMirrorACorrectSheet(string hullStem)
        {
            BoatInteriorKit.SheetEntry sheet = SheetFor(hullStem);

            var cells = Resources.Load<BoatInteriorCellsDef>(BoatInteriorCellsDef.PathFor(sheet.defId));
            Assert.NotNull(cells,
                $"'{sheet.defId}' has no cells asset under Resources/{BoatInteriorCellsDef.ResourcesFolder}.");

            Assert.IsFalse(cells.CellsAreCounterClockwise,
                $"'{sheet.defId}' asks IsoFacing.HeadingToFacingIndex to un-mirror its cells " +
                "(`idx = count − idx`), but they were baked canonically CLOCKWISE — " +
                "RigBaker.DirForCell already corrected for the rig's handedness at bake time. This " +
                $"flag MIRRORS A CORRECT SHEET: the room draws the wrong facing at every heading but " +
                "0 and facings/2, which is how the intro cabin came to sit on the cuddy roof. The " +
                "contract's `convention` field describes the RIG the room was cut from, NOT the order " +
                "of these cells — do not wire one to the other. Re-run " +
                "BoatInteriorVisualWiring.WireAllCli after fixing the builder.");
        }
    }
}
