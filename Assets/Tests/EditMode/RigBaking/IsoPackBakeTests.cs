using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The ISO rig pack's bake path, proven end-to-end WITHOUT a single committed sheet — everything
    /// here runs the V8 host CPU-side, which this suite has already established as safe on CI's null
    /// graphics device.
    ///
    /// <para><b>⭐ THE CONTRACT IS THE ORACLE AND THE RIG IS THE TRUTH.</b> The four contracts under
    /// <c>Assets/_Project/Art/Sprites/</c> are what every baker asserts against, and their generator was
    /// never committed — so the only thing keeping them honest is re-deriving all 156 cells from the
    /// live rigs on every run. That is what <see cref="EveryCellReproducesFromTheLiveRig"/> does. It
    /// takes about ten seconds and it is the reason this file exists.</para>
    ///
    /// <para><b>The rule is NOT the same for all four families</b>, and using the wrong one does not
    /// throw — it silently disagrees with the oracle on every key at once. Each family's rule is pinned
    /// here through the baker's own <c>MeasureCell</c>, so the test and the bake cannot drift apart:
    /// there is one implementation and the test calls it.</para>
    ///
    /// <para><b>What is deliberately NOT here:</b> no sheet is written. The bakers' PNG path is a
    /// <c>Texture2D.EncodeToPNG</c> and a <c>File.WriteAllBytes</c>; what can actually be wrong is the
    /// geometry, and the geometry is measurable without touching the disk.</para>
    /// </summary>
    public class IsoPackBakeTests
    {
        private static IRigScriptHost _host;
        private static readonly Dictionary<string, IsoPackContract> Contracts =
            new Dictionary<string, IsoPackContract>(StringComparer.Ordinal);
        private static readonly Dictionary<string, RigGeometry> Geometry =
            new Dictionary<string, RigGeometry>(StringComparer.Ordinal);

        /// <summary>The two fixed-sheet families, which share a baker and a cell rule.</summary>
        private static readonly string[] FixedSheet = { "wharfDecor", "utilityIso" };

        /// <summary>Every family, in the order the pack README lists them.</summary>
        private static readonly string[] AllFamilies = { "wharfIso", "wharfDecor", "utilityIso", "shoreFinds" };

        [OneTimeSetUp]
        public void InstallTheRigsOnce()
        {
            // One V8 host for the whole fixture. The four rigs share no state and declare no globals
            // beyond their own objects, so they cohabit — and installing them is the expensive part.
            _host = RigScriptHostFactory.Create();

            foreach (string key in AllFamilies)
            {
                var contract = IsoPackContract.Load(key);
                Contracts[key] = contract;

                var entry = RigCatalog.Get(key);
                if (contract.NeedsInstallModule)
                {
                    // wharfIso and shoreFinds expose no pivot global; plain Install throws on that.
                    RigCatalog.InstallModule(_host, entry);
                }
                else
                {
                    Geometry[key] = IsoPropSheetBaker.InstallAndGuard(_host, entry, contract, key);
                }
            }
        }

        [OneTimeTearDown]
        public void DisposeHost() => _host?.Dispose();

        private static IsoPackContract C(string key) => Contracts[key];
        private static string G(string key) => RigCatalog.Get(key).GlobalName;
        private static string Q(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        // =====================================================================================
        // 1. the contracts are internally sound
        // =====================================================================================

        [Test]
        public void EveryContractIsSelfConsistent()
        {
            foreach (string key in AllFamilies)
            {
                // cellRule prose vs the resolved rule, worstSheetByMaxDim vs the cells, and every
                // committed sheet plan's arithmetic and cap.
                Assert.DoesNotThrow(() => C(key).AssertSelfConsistent(), $"{key} contract");
                Debug.Log($"[iso-pack] {C(key)}");
            }
        }

        [Test]
        public void DescribingAContractNeverThrows_EvenForTheNonDirectionalFamily()
        {
            // Found by this suite on its first run: ToString() interpolated `Facings`, which THROWS
            // for shoreFinds by design. A ToString that throws takes out the log line, the debugger
            // watch, and — worst — the message of whatever exception was being reported when someone
            // interpolated the contract into it. The guard and the diagnostic must not fight.
            foreach (string key in AllFamilies)
            {
                string described = null;
                Assert.DoesNotThrow(() => described = C(key).ToString(), $"{key}.ToString()");
                Assert.IsNotEmpty(described);
                StringAssert.Contains(C(key).RigName, described);
            }
        }

        [Test]
        public void EveryCommittedSheetPlanIsExact_AndUnderItsFamilyCap()
        {
            foreach (string key in AllFamilies)
            {
                var contract = C(key);
                int perSheet = contract.CellsPerSheet;

                foreach (var cell in contract.Cells)
                {
                    Assert.AreEqual(perSheet, cell.sheet.cols * cell.sheet.rows,
                        $"{key}.{cell.key}: a {cell.sheet.cols}×{cell.sheet.rows} grid does not hold " +
                        $"exactly {perSheet} cells. Every plan in this pack is an exact factorisation — " +
                        "a ragged tail would bake transparent padding that still slices as a sprite.");

                    int maxDim = Mathf.Max(cell.sheet.sheetW, cell.sheet.sheetH);
                    Assert.LessOrEqual(maxDim, contract.ImportSizeCap,
                        $"{key}.{cell.key} is {cell.sheet.sheetW}×{cell.sheet.sheetH} = {maxDim} px on " +
                        $"its longest side, over the {contract.ImportSizeCap} px cap. Over the cap a " +
                        "sheet imports SILENTLY DOWNSCALED with the sprite count still correct.");
                }
            }
        }

        // =====================================================================================
        // 2. the keyline gate — and the positive control that proves it is a gate, not a blank
        // =====================================================================================

        [Test]
        public void EveryRigShipsTheKeylineGate_AndTheContractsRecordItOff()
        {
            foreach (string key in AllFamilies)
            {
                Assert.DoesNotThrow(() => C(key).AssertKeylineGated(_host, G(key), key),
                    $"{key} must expose KEYLINE_DEFAULT (#463) before anything bakes.");

                Assert.IsFalse(_host.EvaluateBool($"{G(key)}.KEYLINE_DEFAULT"),
                    $"{key}.KEYLINE_DEFAULT is true — the ring is back on and every sheet would bake " +
                    "with it in. ADR 0031 retired it.");

                Assert.IsFalse(C(key).KeylineDefault,
                    $"{key}'s contract still records keylineDefault=true, so its cells were measured " +
                    "with the ring ON while the rig now draws without it. Regenerate the contract.");
            }
        }

        [Test]
        public void TheRingIsGoneByDefault_AndComesBackWhenForced()
        {
            // ⚠️ THE POSITIVE CONTROL. Zero ring pixels alone would also pass on a renderer that draws
            // nothing at all, which is exactly the failure a gate can introduce. Both arms are required.
            foreach (var (family, piece) in new[]
                     {
                         ("wharfDecor", "trapStack"),
                         ("utilityIso", "radioMast"),
                         ("shoreFinds", "SoftshellClam"),
                     })
            {
                Color32 keyline = ParseHex(C(family).Keyline);

                int off = CountKeylinePixels(family, piece, "{}", keyline);
                int on = CountKeylinePixels(family, piece, "{outline:true}", keyline);

                Assert.Zero(off,
                    $"{family}.{piece} still draws {off} keyline pixels at the rig default — the gate " +
                    "is not doing anything.");
                Assert.Greater(on, 0,
                    $"{family}.{piece} draws no keyline pixels even with {{outline:true}}. The gate did " +
                    "not switch a ring off — the renderer has stopped drawing, which zero-ring alone " +
                    "would have reported as a pass.");

                Debug.Log($"[iso-pack] gate A/B {family}.{piece}: default {off} px, forced-on {on} px.");
            }
        }

        [Test]
        public void TheKeylineConstantAgreesWithTheContract_UnderEachFamilysOwnExportName()
        {
            // ⚠️ The three COLD rigs export the keyline as `KEY`; ShoreFinds calls the same thing
            // `KEYLINE`. Reading the wrong name gives `undefined`, not an error.
            foreach (string key in AllFamilies)
            {
                string exportName = key == "shoreFinds" ? "KEYLINE" : "KEY";
                Assert.IsTrue(_host.EvaluateBool($"typeof {G(key)}.{exportName} === 'string'"),
                    $"{key} does not export its keyline as `{exportName}`.");

                Assert.AreEqual(C(key).Keyline, _host.EvaluateString($"{G(key)}.{exportName}"),
                    $"{key}: the contract's keyline and the rig's {exportName} disagree.");
            }
        }

        // =====================================================================================
        // 3. THE BIG ONE — all 156 cells re-derived from the live rigs, each by its own rule
        // =====================================================================================

        [Test]
        public void EveryCellReproducesFromTheLiveRig()
        {
            var failures = new List<string>();
            int checkedCells = 0;

            // ---- wharfIso: pivot-aligned union of the returned BUFFER extents, floor/ceil ----------
            foreach (var cell in C("wharfIso").Cells)
            {
                checkedCells++;
                var facings = WharfIsoSheetBaker.RenderFacings(
                    _host, G("wharfIso"), cell.key, C("wharfIso").Facings,
                    RigCatalog.Get("wharfIso").DeclaredConvention);

                WharfIsoSheetBaker.MeasureCell(facings, out int w, out int h, out int px, out int py);
                Compare(failures, "wharfIso", cell, w, h, px, py, null);
            }

            // ---- wharfDecor / utilityIso: pivot-INCLUSIVE union of the INK, seeded at the pivot ----
            foreach (string family in FixedSheet)
            {
                var contract = C(family);
                var geo = Geometry[family];
                int gx = Mathf.RoundToInt((float)geo.PivotX), gy = Mathf.RoundToInt((float)geo.PivotY);

                foreach (var cell in contract.Cells)
                {
                    checkedCells++;
                    byte[][] rendered = IsoPropSheetBaker.RenderFacings(
                        _host, family, cell.key, contract, geo,
                        RigCatalog.Get(family).DeclaredConvention);

                    bool anyInk = IsoPropSheetBaker.MeasureCell(
                        rendered, geo.Width, geo.Height, gx, gy,
                        out int w, out int h, out int px, out int py, out _, out _, out bool inside);

                    if (!anyInk) { failures.Add($"{family}.{cell.key}: every facing is transparent"); continue; }
                    Compare(failures, family, cell, w, h, px, py, inside);
                }
            }

            // ---- shoreFinds: cellOf(key) verbatim, ANALYTIC ----------------------------------------
            foreach (var cell in C("shoreFinds").Cells)
            {
                checkedCells++;
                ReadAnalyticCell(cell.key, out int w, out int h, out int px, out int py);
                Compare(failures, "shoreFinds", cell, w, h, px, py, null);
            }

            Assert.AreEqual(156, checkedCells,
                "the pack is 17 + 61 + 42 + 36 = 156 cells; a different total means a family gained or " +
                "lost keys without its contract being regenerated.");

            if (failures.Count == 0) return;

            Assert.Fail(
                $"{failures.Count} of {checkedCells} cells disagree with their contract.\n\n" +
                "Check the RULE before suspecting the rig: the four families do not share one, and " +
                "measuring the wrong quantity fails on EVERY key rather than one. A whole family " +
                "shifting by (−2,−2) with pivots at (−1,−1) is the keyline gate — regenerate that " +
                "family's contract.\n\n" + string.Join("\n", failures.Take(20)) +
                (failures.Count > 20 ? $"\n… (+{failures.Count - 20} more)" : ""));
        }

        private static void Compare(List<string> failures, string family, IsoPackContract.Cell cell,
                                    int w, int h, int px, int py, bool? pivotInsideInk)
        {
            if (cell.cellW != w || cell.cellH != h)
                failures.Add($"{family}.{cell.key}: cell {cell.cellW}×{cell.cellH} vs measured {w}×{h} " +
                             $"(Δ {w - cell.cellW},{h - cell.cellH})");
            if (cell.pivotX != px || cell.pivotY != py)
                failures.Add($"{family}.{cell.key}: pivot {cell.pivotX},{cell.pivotY} vs measured {px},{py}");
            if (pivotInsideInk != null && cell.pivotInsideInk != pivotInsideInk.Value)
                failures.Add($"{family}.{cell.key}: pivotInsideInk {cell.pivotInsideInk} vs measured " +
                             $"{pivotInsideInk.Value}");
        }

        [Test]
        public void FireCabinetIsTheOnlyPieceWhoseInkMissesItsOwnPivot()
        {
            // The seeding in IsoPropSheetBaker.MeasureCell exists for this one piece out of 61. If it
            // ever stops being the only one, the rule is load-bearing somewhere new — and if it stops
            // being true at all, someone has "tidied" the seeding away.
            var wallHung = C("wharfDecor").Cells.Where(c => !c.pivotInsideInk).Select(c => c.key).ToArray();
            CollectionAssert.AreEqual(new[] { "fireCabinet" }, wallHung,
                "pivotInsideInk=false should be exactly [fireCabinet] — it is wall-hung, so its ink " +
                "stops above its own pivot and a crop tight to ink would put its ground contact " +
                "outside its own cell.");

            var cabinet = C("wharfDecor")["fireCabinet"];
            Assert.AreEqual(cabinet.cellH, cabinet.pivotY + 1,
                "fireCabinet's cell should end exactly ON its pivot row — cellH = pivotY + 1 is what " +
                "the pivot-INCLUSIVE rule yields, and 52 (exclusive) is the off-by-one it shipped with.");

            foreach (var c in C("utilityIso").Cells)
                Assert.IsTrue(c.pivotInsideInk, $"utilityIso.{c.key}: nothing in this family is wall-hung.");
        }

        // =====================================================================================
        // 4. facings come from the CONTRACT, never from the rig's DIRS
        // =====================================================================================

        [Test]
        public void ThreeOfFourRigsReportZeroNativeFacings_SoTheContractIsTheSource()
        {
            // Only wharfIso declares `DIRS: 8` as a number. WharfDecor and UtilityIso declare no DIRS
            // global at all, and ShoreFinds declares a string ARRAY — so RigCatalog.Install's
            // `typeof DIRS === 'number'` test is false and it reports 0, SILENTLY, because 0
            // legitimately means "the rig does not say".
            Assert.IsTrue(_host.EvaluateBool($"typeof {G("wharfIso")}.DIRS === 'number'"),
                "wharfIso is the one rig that does declare a numeric DIRS.");

            foreach (string key in new[] { "wharfDecor", "utilityIso" })
                Assert.IsTrue(_host.EvaluateBool($"typeof {G(key)}.DIRS === 'undefined'"),
                    $"{key} is documented as one of the SAFE rigs, and it still declares no DIRS.");

            Assert.IsTrue(_host.EvaluateBool($"Array.isArray({G("shoreFinds")}.DIRS)"),
                "ShoreFinds.DIRS is a string array of lie-angle names, not a facing count.");

            foreach (string key in FixedSheet)
            {
                Assert.Zero(Geometry[key].NativeDirs,
                    $"{key} should report 0 native facings — if this ever reports 8, the rig gained a " +
                    "DIRS global and the baker's guard needs revisiting.");
                Assert.AreEqual(8, C(key).Facings, $"{key}'s contract is the source of the facing count.");
            }
        }

        [Test]
        public void AskingTheNonDirectionalFamilyForFacingsThrows()
        {
            // The guard that stops a copy-pasted turntable loop from compiling into a plausible wrong
            // sheet: shoreFinds has no facings at all, and answering 0 would bake one cell.
            var contract = C("shoreFinds");
            Assert.IsFalse(contract.IsDirectional);
            Assert.Throws<InvalidOperationException>(() => { _ = contract.Facings; },
                "shoreFinds must refuse to answer 'how many facings', not answer 0.");

            Assert.AreEqual(8, contract.LieAngleCount);
            Assert.AreEqual(3, contract.Variants);
            Assert.AreEqual(3, contract.StateNames.Count);
            Assert.AreEqual(24, contract.CellsPerSheet,
                "a finds sheet is 8 lie angles × 3 variants; the states are separate SHEETS.");

            foreach (string key in AllFamilies.Where(k => k != "shoreFinds"))
                Assert.AreEqual(8, C(key).CellsPerSheet, $"{key} packs one cell per facing.");
        }

        // =====================================================================================
        // 5. packing comes from the COMMITTED PLAN — the ruling that a re-derived grid would undo
        // =====================================================================================

        [Test]
        public void GridForReturnsTheCommittedPlan_ForEveryKeyInEveryFamily()
        {
            foreach (string key in AllFamilies)
            {
                var contract = C(key);
                foreach (var cell in contract.Cells)
                {
                    contract.GridFor(cell.key, out int cols, out int rows);
                    Assert.AreEqual(cell.sheet.cols, cols, $"{key}.{cell.key} cols");
                    Assert.AreEqual(cell.sheet.rows, rows, $"{key}.{cell.key} rows");
                }
            }
        }

        [Test]
        public void TimberQuayStaysFourByTwo_WhereAReDerivedGridWouldPutItBackOnOneRow()
        {
            // ⭐ THE REGRESSION PIN for VERIFICATION.md §2. timberQuay was repacked 8×1 → 4×2 under
            // coordinator ruling so its longest side drops 4048 → 2024 px, leaving the family 312 px of
            // headroom instead of 48. ChooseGrid — the helper every OTHER kit in this repo packs with —
            // prefers the widest grid under the cap and puts it straight back on one row at 4048 px,
            // which is under the 4096 cap and therefore passes every other check silently.
            //
            // If someone "simplifies" a baker back to ChooseGrid, this is the test that says no.
            var contract = C("wharfIso");
            var quay = contract["timberQuay"];

            contract.GridFor("timberQuay", out int cols, out int rows);
            Assert.AreEqual(4, cols, "timberQuay packs 4 columns, per the §2 ruling.");
            Assert.AreEqual(2, rows, "timberQuay packs 2 rows, per the §2 ruling.");
            Assert.AreEqual(2024, Mathf.Max(cols * quay.cellW, rows * quay.cellH),
                "the repack is what keeps timberQuay's longest side at 2024 px.");

            BuildingRigBaker.ChooseGrid(quay.cellW, quay.cellH, contract.Facings,
                                        out int derivedCols, out int derivedRows,
                                        contract.ImportSizeCap);
            Assert.AreEqual(8, derivedCols,
                "if ChooseGrid has changed and no longer disagrees here, this pin has lost its teeth — " +
                "re-derive what the new disagreement is before deleting it.");
            Assert.AreEqual(4048, derivedCols * quay.cellW,
                "ChooseGrid's 8×1 is 4048 px — 48 px under the cap, and it passes AssertSheetFits.");

            // And the assert that catches it if a baker packs the derived grid anyway.
            Assert.Throws<InvalidOperationException>(
                () => contract.AssertSheetFits("timberQuay", derivedCols, derivedRows,
                                               derivedCols * quay.cellW, derivedRows * quay.cellH),
                "AssertSheetFits must refuse a pack that disagrees with the committed plan.");
        }

        [Test]
        public void TheCommittedPlansAreNotWhatChooseGridWouldPick()
        {
            // Not a style note: 7 of wharfIso's 17 presets and 5 of shoreFinds' 36 differ. Counting them
            // here documents the size of the divergence so a future reader does not assume timberQuay is
            // a one-off.
            int diverged = 0, total = 0;
            foreach (string key in AllFamilies)
            {
                var contract = C(key);
                foreach (var cell in contract.Cells)
                {
                    total++;
                    BuildingRigBaker.ChooseGrid(cell.cellW, cell.cellH, contract.CellsPerSheet,
                                                out int cols, out int rows, contract.ImportSizeCap);
                    if (cols != cell.sheet.cols || rows != cell.sheet.rows) diverged++;
                }
            }

            Assert.AreEqual(12, diverged,
                $"{diverged} of {total} committed plans differ from ChooseGrid's choice. This test " +
                "exists to keep that number visible; if it moved, the packing rule or a cell did.");
        }

        // =====================================================================================
        // 6. the slicer reads the same contract, and converts the pivot the one right way
        // =====================================================================================

        [Test]
        public void TheSlicerNormalisesEveryContractPivotTheAdr0026Way()
        {
            // The contracts record pivots from the TOP-LEFT; Unity normalises from the BOTTOM-LEFT.
            // Inverted, a piece plants itself through the deck and nothing reports an error — so the
            // formula is asserted per cell, in pixels, against the contract's own numbers.
            foreach (string key in AllFamilies)
            {
                foreach (var cell in C(key).Cells)
                {
                    Vector2 p = IsoPackSheetSlicer.NormalisedPivot(cell.cellW, cell.cellH,
                                                                   cell.pivotX, cell.pivotY);

                    Assert.AreEqual(cell.pivotX, p.x * cell.cellW, 0.001,
                        $"{key}.{cell.key}: x must round-trip to the contract's pixel column.");
                    Assert.AreEqual(cell.cellH - cell.pivotY, p.y * cell.cellH, 0.001,
                        $"{key}.{cell.key}: y must be (cellH − pivotY), measured UP from the bottom. " +
                        "If this reads pivotY directly, every piece is flipped about its own centre.");

                    Assert.That(p.x, Is.InRange(0f, 1f), $"{key}.{cell.key} pivot x out of the cell");
                    Assert.That(p.y, Is.InRange(0f, 1f), $"{key}.{cell.key} pivot y out of the cell");
                }
            }
        }

        [Test]
        public void TheSlicerAndTheBakerAgreeOnTheNormalisedPivot()
        {
            // Two implementations across an asmdef boundary — Art.Editor cannot see IsoPackContract, so
            // the slicer carries its own reader. That duplication is only safe while they agree.
            var cell = C("wharfDecor")["fireCabinet"];
            var baked = new IsoPropBakeResult
            {
                CellWidth = cell.cellW, CellHeight = cell.cellH,
                PivotX = cell.pivotX, PivotY = cell.pivotY,
            };

            Assert.AreEqual(baked.NormalisedPivot,
                            IsoPackSheetSlicer.NormalisedPivot(cell.cellW, cell.cellH,
                                                               cell.pivotX, cell.pivotY),
                "the baker's result pivot and the slicer's must be the same number, on the piece where " +
                "the pivot sits outside the ink and a sign error is hardest to see.");
        }

        /// <summary>
        /// <b>THE REGISTRATION PARITY GUARD.</b> Three registries have to agree that a family exists —
        /// the contract registry (what a baker asserts against), the slicer (what turns a PNG into
        /// sprites) and the catalog (what rig to install) — and a family missing from any one of them
        /// fails in a way nobody reads as a failure. #472 shipped exactly that omission.
        ///
        /// <para>Deliberately driven off the REGISTRIES rather than off this fixture's own
        /// <c>AllFamilies</c> array: a family that is registered but forgotten here would be the same
        /// omission one level up.</para>
        /// </summary>
        [Test]
        public void TheSlicerAndTheCatalogKnowTheSameFamilies_AtTheSameContractsAndFolders()
        {
            CollectionAssert.AreEquivalent(
                IsoPackContract.Families.ToArray(),
                IsoPackSheetSlicer.Families.Select(f => f.Key).ToArray(),
                "the slicer and the baker must cover the same families, or a baked sheet goes unsliced " +
                "— which imports as spriteMode Multiple with EMPTY rects and loads as nothing.");

            foreach (var fam in IsoPackSheetSlicer.Families)
            {
                Assert.AreEqual(IsoPackContract.Paths[fam.Key], fam.ContractPath,
                    $"{fam.Key}: the slicer and the baker must read the SAME contract file.");

                // One file may hold several families (the deck-loop kit's five do). Reading the same
                // file but a different SECTION of it is a sheet sliced against another family's grid.
                Assert.AreEqual(IsoPackContract.Registry[fam.Key].Section, fam.Section,
                    $"{fam.Key}: the slicer and the baker must read the same section of it.");

                Assert.AreEqual(IsoPackContract.SheetFolderFor(fam.Key) + "/", fam.Folder,
                    $"{fam.Key}: the baker writes its sheets somewhere the slicer does not look.");
            }

            foreach (string key in IsoPackContract.Families)
                Assert.IsTrue(RigCatalog.Entries.ContainsKey(key),
                    $"'{key}' is registered as a contract family but is not in RigCatalog — there is no " +
                    "rig source to install, so the bake cannot start. This is the shape of #472's " +
                    $"omission. Catalog keys: {string.Join(", ", RigCatalog.Entries.Keys)}.");
        }

        [Test]
        public void EverySliceRectLandsInsideItsSheet_AndTheGridIsRowMajorFromTheTopLeft()
        {
            // Driftwood is the finds' non-trivial grid: 12×2 rather than the usual 24×1.
            var cell = C("shoreFinds")["Driftwood"];
            int cols = cell.sheet.cols, rows = cell.sheet.rows;
            int sheetW = cols * cell.cellW, sheetH = rows * cell.cellH;
            Assert.AreEqual(2, rows, "this test wants a MULTI-ROW grid, or the row flip is untested.");

            var seen = new HashSet<Vector2>();
            for (int i = 0; i < 24; i++)
            {
                Rect r = IsoPackSheetSlicer.CellRect(i, cols, rows, cell.cellW, cell.cellH);
                Assert.That(r.xMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(r.yMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(r.xMax, Is.LessThanOrEqualTo(sheetW), $"slice {i} runs off the right edge");
                Assert.That(r.yMax, Is.LessThanOrEqualTo(sheetH), $"slice {i} runs off the top edge");
                Assert.IsTrue(seen.Add(r.position), $"slice {i} overlaps an earlier cell at {r.position}");
            }

            // Index 0 is the TOP-LEFT cell, which in Unity's bottom-origin space is the HIGHEST y.
            Rect first = IsoPackSheetSlicer.CellRect(0, cols, rows, cell.cellW, cell.cellH);
            Assert.AreEqual(0, first.xMin);
            Assert.AreEqual(sheetH - cell.cellH, first.yMin,
                "slice 0 must be the TOP-left cell; if it is at y=0 the sheet reads bottom-up and every " +
                "lie angle is paired with the wrong variant.");
            Assert.AreEqual(cell.cellW,
                            IsoPackSheetSlicer.CellRect(1, cols, rows, cell.cellW, cell.cellH).xMin,
                            "slice 1 is the next cell ACROSS, not down.");
            Assert.AreEqual(sheetH - 2 * cell.cellH,
                            IsoPackSheetSlicer.CellRect(cols, cols, rows, cell.cellW, cell.cellH).yMin,
                            "the cell after the last column wraps to the next row DOWN the sheet.");
        }

        // =====================================================================================
        // 7. shoreFinds — the traps that are specific to the one non-directional family
        // =====================================================================================

        [Test]
        public void TheExportedCellOfIsCellFull_NotTheRigsInternalCellOf()
        {
            // ⚠️ The rig declares BOTH `cellOf` and `cellFull`, and exports the latter under the
            // former's name (`cellOf: cellFull`). The internal one returns pvy: 0; only cellFull fills
            // in pvy = h − _half. Porting the internal function's body — the obvious thing to do when
            // reading the rig top to bottom — plants every find by its top edge, and 0 is a
            // legitimate-looking pivot row, so nothing downstream would flag it.
            foreach (var cell in C("shoreFinds").Cells)
            {
                ReadAnalyticCell(cell.key, out _, out int h, out _, out int py);
                Assert.Greater(py, 0,
                    $"shoreFinds.{cell.key}: cellOf() returned pivotY 0. That is the INTERNAL cellOf, " +
                    "not the exported cellFull — every find would sit by its top edge.");
                Assert.Less(py, h, $"shoreFinds.{cell.key}: pivotY must be inside the cell.");
            }
        }

        [Test]
        public void TheFindsRenderReturnsRgba_NotDataAndNotABareArray()
        {
            // A THIRD return shape. The prop rigs hand back a bare RGBA array and the wharf kit returns
            // {data,w,h,px,py,wet}; this one returns {w,h,rgba,pivot,…}. Reading `.data` here does not
            // throw at the JS boundary — it hands back nothing.
            _host.Execute($"globalThis.__t = {G("shoreFinds")}.render('SoftshellClam',0," +
                          "{state:'wet',variant:2});");
            try
            {
                Assert.IsTrue(_host.EvaluateBool("typeof __t.rgba !== 'undefined'"),
                    "the finds' pixels live on .rgba");
                Assert.IsTrue(_host.EvaluateBool("typeof __t.data === 'undefined'"),
                    "…and NOT on .data — if .data ever appears, the guard in ShoreFindsSheetBaker " +
                    "needs rewriting rather than relaxing.");
                Assert.IsTrue(_host.EvaluateBool("__t.rgba.constructor.name === 'Uint8ClampedArray'"));
                Assert.IsTrue(_host.EvaluateBool("typeof __t.pivot === 'object'"),
                    "the finds report their pivot per render, as an object");
            }
            finally
            {
                _host.Execute("globalThis.__t = null;");
            }
        }

        [Test]
        public void OneFindIsOneCell_AcrossEveryStateVariantAndLieAngle()
        {
            // The whole sheet is one cell size, and the cell is computed for the LARGEST variant. If a
            // variant or a lie angle ever returned a different buffer it would land in the grid at the
            // wrong scale and merely look "a bit off".
            //
            // Sampled rather than swept: the full cross-product is 36 × 3 × 3 × 8 = 2,592 renders. These
            // four cover the extremes of the size range (25 px to 128 px) and both grid shapes.
            foreach (string find in new[] { "BlueMussel", "Driftwood", "RopeScrap", "SoftshellClam" })
            {
                ReadAnalyticCell(find, out int cw, out int ch, out int px, out int py);

                foreach (string state in C("shoreFinds").StateNames)
                    for (int v = 0; v < C("shoreFinds").Variants; v++)
                        for (int lie = 0; lie < C("shoreFinds").LieAngleCount; lie++)
                        {
                            _host.Execute($"globalThis.__t = {G("shoreFinds")}.render({Q(find)},{lie}," +
                                          $"{{state:{Q(state)},variant:{v}}});");
                            try
                            {
                                Assert.AreEqual(cw, (int)_host.EvaluateNumber("__t.w"),
                                    $"{find} {state} v{v} lie{lie}: width");
                                Assert.AreEqual(ch, (int)_host.EvaluateNumber("__t.h"),
                                    $"{find} {state} v{v} lie{lie}: height");
                                Assert.AreEqual(px, (int)_host.EvaluateNumber("__t.pivot.x"),
                                    $"{find} {state} v{v} lie{lie}: pivot x");
                                Assert.AreEqual(py, (int)_host.EvaluateNumber("__t.pivot.y"),
                                    $"{find} {state} v{v} lie{lie}: pivot y");
                            }
                            finally
                            {
                                _host.Execute("globalThis.__t = null;");
                            }
                        }
            }
        }

        [Test]
        public void TheFindsGroundForeshortenIsItsOwn_NotTheIsoRigs()
        {
            // 0.72 here against sin(40°) = 0.6427876097 for the other three. Carrying one across
            // families is the un-squash mistake this repo keeps making, so the value is per family.
            Assert.AreEqual(0.72, C("shoreFinds").GroundForeshorten, 1e-9);
            Assert.AreEqual(0.72, _host.EvaluateNumber($"{G("shoreFinds")}.Q"), 1e-9);

            foreach (string key in new[] { "wharfIso", "wharfDecor", "utilityIso" })
                Assert.AreEqual(Math.Sin(40.0 * Math.PI / 180.0), C(key).GroundForeshorten, 1e-6,
                    $"{key} projects at sin(40°), not the finds' 0.72.");
        }

        [Test]
        public void AnUnknownFindOrStateWouldBakeSilently_SoTheBakerRefusesFirst()
        {
            // ⚠️ Neither the key nor the state throws in the rig: `byKey[key] || FINDS[0]` and
            // `STATES.indexOf(o.state) >= 0 ? o.state : 'dry'` both fall back. So an unknown key bakes a
            // perfectly good sheet OF THE WRONG OBJECT under the requested name. This test pins the
            // fallback so the guards that front it are never mistaken for belt-and-braces.
            _host.Execute($"globalThis.__t = {G("shoreFinds")}.render('NoSuchFind',0,{{state:'sopping'}});");
            try
            {
                Assert.AreEqual("dry", _host.EvaluateString("__t.report.state"),
                    "an unknown state silently becomes 'dry'");
                ReadAnalyticCell(C("shoreFinds").Cells[0].key, out int w, out _, out _, out _);
                Assert.AreEqual(w, (int)_host.EvaluateNumber("__t.w"),
                    "an unknown key silently renders the FIRST find — the sheet is valid and wrong.");
            }
            finally
            {
                _host.Execute("globalThis.__t = null;");
            }
        }

        // =====================================================================================
        // helpers
        // =====================================================================================

        private static void ReadAnalyticCell(string find, out int w, out int h, out int px, out int py)
        {
            _host.Execute($"globalThis.__c = {G("shoreFinds")}.cellOf({Q(find)});");
            try
            {
                w = (int)_host.EvaluateNumber("__c.w");
                h = (int)_host.EvaluateNumber("__c.h");
                px = (int)_host.EvaluateNumber("__c.pvx");
                py = (int)_host.EvaluateNumber("__c.pvy");
            }
            finally
            {
                _host.Execute("globalThis.__c = null;");
            }
        }

        /// <summary>Count pixels drawn at exactly the family's keyline colour, for one facing.</summary>
        private static int CountKeylinePixels(string family, string piece, string opts, Color32 keyline)
        {
            byte[] rgba;
            if (family == "shoreFinds")
            {
                _host.Execute($"globalThis.__t = {G(family)}.render({Q(piece)},0,{opts});");
                try { rgba = _host.EvaluateBytes("__t.rgba"); }
                finally { _host.Execute("globalThis.__t = null;"); }
            }
            else
            {
                rgba = _host.EvaluateBytes($"{G(family)}.render({Q(piece)},0,{opts})");
            }

            int n = 0;
            for (int i = 0; i < rgba.Length; i += 4)
                if (rgba[i + 3] > 0 && rgba[i] == keyline.r && rgba[i + 1] == keyline.g &&
                    rgba[i + 2] == keyline.b) n++;
            return n;
        }

        private static Color32 ParseHex(string hex)
        {
            string s = hex.TrimStart('#');
            return new Color32(byte.Parse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                               byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                               byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                               255);
        }
    }
}
