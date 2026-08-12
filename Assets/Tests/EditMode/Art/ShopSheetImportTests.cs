using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Acceptance for the SHOP KIT's consumer chain: the committed contract, the sliced sheets, and the
    /// per-facing door anchors that placement reads. Sibling of <c>VillageBuildingImportTests</c>, and
    /// shaped by the same fact — every failure in this chain is silent.
    ///
    /// <para><b>Two of the silent failures below actually happened on this kit's first bake</b>, which is
    /// why they are pinned here rather than described:</para>
    /// <list type="bullet">
    ///   <item>The sheets landed unsliced and it did not look like it. Unity's automatic slicing gave
    ///   each one EIGHT alpha-trimmed rects on an eight-facing sheet, named <c>_0…_7</c>, every pivot at
    ///   <c>(0,0)</c> and no two cells the same size (340×393 beside 442×455). Eight named sprites is
    ///   what a working slice looks like from a distance.</item>
    ///   <item>The cap was lifted in code and not honoured on disk: <c>Shopfront_generalStore</c> read
    ///   back <b>2048×546</b> against a 3878×1034 sheet, because <c>maxTextureSize</c> does nothing until
    ///   an asset is reimported.</item>
    /// </list>
    ///
    /// <para>Everything here IGNORES rather than fails when the kit has not been baked on this machine.
    /// The sheets are committed, so a fresh checkout has them — but a branch mid-rebake should not turn
    /// the suite red.</para>
    /// </summary>
    public class ShopSheetImportTests
    {
        ShopKit.Contract _contract;

        [SetUp]
        public void LoadContract()
        {
            if (!File.Exists(ShopKit.ContractPath))
                Assert.Ignore($"{ShopKit.ContractPath} is not present — run " +
                              "Hidden Harbours ▸ Art ▸ Bake Shops (shells + interiors).");

            _contract = ShopKit.Load();
            Assert.IsNotNull(_contract, "the contract is present but did not parse");
        }

        void RequireSheetsOnDisk()
        {
            foreach (var e in ShopKit.AllEntries(_contract))
                if (!File.Exists(ShopKit.SheetPathFor(e)))
                    Assert.Ignore($"{ShopKit.SheetPathFor(e)} is missing — the kit is mid-bake.");
        }

        // ---- the contract ----------------------------------------------------------------------

        [Test]
        public void TheContractCoversEveryBuildTheKitDeclares()
        {
            foreach (var build in ShopKit.ShopSet)
            {
                Assert.IsNotNull(ShopKit.FindShell(_contract, build.Key),
                    $"'{build.Key}' is in ShopSet but the contract carries no shell for it. Re-bake — do " +
                    "not hand-edit the contract.");

                foreach (string level in build.Levels)
                    Assert.IsNotNull(ShopKit.FindLevel(_contract, build.Key, level),
                        $"'{build.Key}/{level}' is in ShopSet but the contract carries no level for it.");
            }

            Assert.AreEqual(ShopKit.ShopSet.Length, _contract.shells.Length,
                "the contract should carry exactly ShopSet's shells: a stranger in it means a stale bake, " +
                "and the slicer would happily slice a sheet nothing places");
        }

        /// <summary>
        /// The offset is 0 — and the contract has to say it was MEASURED, not that someone typed a 0.
        /// The house family's answer is 4; carrying it here would put every shop's doorway against its
        /// back wall, and the symptom reads as an art bug.
        /// </summary>
        [Test]
        public void TheShellFacingOffsetIsZero_AndTheContractShowsItsWorking()
        {
            Assert.AreEqual(0, _contract.shellFacingOffset,
                "the level no longer stands under its shell at the same facing.");
            Assert.AreNotEqual(4, _contract.shellFacingOffset,
                "4 is the HOUSE family's answer — re-measure before trusting it.");

            Assert.IsFalse(string.IsNullOrEmpty(_contract.registrationReport),
                "the offset was written without the probe's working. A number nobody can check is a " +
                "declaration again — which is the whole reason ShopRegistrationProbe exists.");
            StringAssert.Contains("door gable", _contract.registrationReport,
                "the report should carry the measured gable for both rigs");
        }

        [Test]
        public void EveryEntrysDerivedFieldsMatchTheirDefinition()
        {
            // A hand-edited contract would be silently obeyed by every consumer, so the derived fields
            // are recomputed from the stored ones rather than trusted.
            foreach (var e in ShopKit.AllEntries(_contract))
            {
                Vector2 expected = ShopKit.NormalizedPivot(e);
                Assert.AreEqual(expected.x, e.unityPivotX, 1e-5f, $"{ShopKit.StemFor(e)} unityPivotX");
                Assert.AreEqual(expected.y, e.unityPivotY, 1e-5f, $"{ShopKit.StemFor(e)} unityPivotY");

                Assert.AreEqual(ShopKit.Facings, e.facings, $"{ShopKit.StemFor(e)} facings");
                Assert.GreaterOrEqual(e.cols * e.rows, e.facings,
                    $"{ShopKit.StemFor(e)}: the grid has fewer cells than facings");
                Assert.AreEqual(e.sheetW, e.cols * e.cellW, $"{ShopKit.StemFor(e)} sheet width");
                Assert.AreEqual(e.sheetH, e.rows * e.cellH, $"{ShopKit.StemFor(e)} sheet height");
            }
        }

        /// <summary>
        /// Every sheet fits the cap, and the cap is the ONE number. Recorded with the measurement that
        /// forced it: the restaurant's ground plan is 696×559, whose best 2048 grid is 3×3 at 2088 px —
        /// over by 40.
        /// </summary>
        [Test]
        public void EverySheetIsInsideTheImportCap_AndTheCapIsTheOneTheContractRecords()
        {
            Assert.AreEqual(ShopKit.ImportSizeCap, _contract.importSizeCap,
                "the contract was baked against a different cap from the one the importer now uses. Two " +
                "numbers that have to agree is the bug this kit records as ONE number.");

            foreach (var e in ShopKit.AllEntries(_contract))
            {
                int needed = Mathf.NextPowerOfTwo(Mathf.Max(e.sheetW, e.sheetH));
                Assert.LessOrEqual(needed, ShopKit.ImportSizeCap,
                    $"{ShopKit.StemFor(e)} is {e.sheetW}×{e.sheetH} and needs a {needed} px import, past " +
                    $"the {ShopKit.ImportSizeCap} px cap — Unity would DOWNSCALE it silently and the " +
                    "sprite count would still come out right.");
            }
        }

        [Test]
        public void EveryEntryMatchesTheSidecarTheSameBakeWrote()
        {
            // The shell bake writes two files — this kit's aggregate contract and BuildingRigBaker's
            // per-sheet sidecar (which carries the per-facing anchors). They come from one bake result,
            // so they cannot drift WITHIN a bake; a hand edit to either, or a half-finished re-bake,
            // shows up here and nowhere else.
            //
            // ⚠️ LEVELS HAVE NO SIDECAR — ShopLevelBaker writes the PNG and the contract entry only. So
            // this checks the shells and counts, rather than Assert.Ignore-ing the whole test on the
            // first level it meets, which is what a naive `if (!Exists) Ignore` did and it silently
            // skipped the shells too.
            int checkedCount = 0;
            foreach (var e in ShopKit.AllEntries(_contract))
            {
                string sidecar = ShopKit.ShopsRoot + ShopKit.StemFor(e) + ".json";
                if (!File.Exists(sidecar)) continue;

                checkedCount++;
                string json = File.ReadAllText(sidecar);
                StringAssert.Contains($"\"cellW\": {e.cellW}", json, $"{ShopKit.StemFor(e)} cellW");
                StringAssert.Contains($"\"cellH\": {e.cellH}", json, $"{ShopKit.StemFor(e)} cellH");
                StringAssert.Contains($"\"cropX\": {e.cropX}", json, $"{ShopKit.StemFor(e)} cropX");
                StringAssert.Contains($"\"cropY\": {e.cropY}", json, $"{ShopKit.StemFor(e)} cropY");
            }

            Assert.AreEqual(_contract.shells.Length, checkedCount,
                "every SHELL bake writes a sidecar beside its sheet; a missing one means an interrupted " +
                "bake, and a test that checked none of them would pass just as quietly.");
        }

        // ---- the slice -------------------------------------------------------------------------

        [Test]
        public void EverySheetImportedTheWayThisKitNeeds()
        {
            // One call, because VerifyAll is the single shared definition of "imported correctly" — the
            // batch entry point runs the same code, so the suite and a headless slice cannot disagree
            // about what passing means.
            RequireSheetsOnDisk();
            Assert.IsTrue(ShopSheetSlicer.VerifyAll(logEachPass: false),
                "VerifyAll failed — see the logged mismatches. Re-run Hidden Harbours ▸ Art ▸ Import " +
                "(after a new drop) ▸ Slice Shop Sheets.");
        }

        [Test]
        public void EverySheetSlicesToExactlyItsFacings_AtTheContractsCell()
        {
            RequireSheetsOnDisk();
            foreach (var e in ShopKit.AllEntries(_contract))
            {
                string path = ShopKit.SheetPathFor(e);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();

                Assert.AreEqual(e.facings, sprites.Length,
                    $"{ShopKit.StemFor(e)}: {e.facings} facings expected, got {sprites.Length}. " +
                    "⚠️ EIGHT is also what Unity's AUTOMATIC slicing produces on these sheets (one rect " +
                    "per alpha island), so a passing count here is only meaningful beside the cell and " +
                    "pivot asserts below.");

                foreach (var s in sprites)
                {
                    Assert.AreEqual(e.cellW, Mathf.RoundToInt(s.rect.width), $"{s.name} cell width");
                    Assert.AreEqual(e.cellH, Mathf.RoundToInt(s.rect.height), $"{s.name} cell height");
                }

                var names = sprites.Select(s => s.name).OrderBy(n => n).ToArray();
                var expected = Enumerable.Range(0, e.facings)
                                         .Select(f => ShopKit.SpriteNameFor(e, f))
                                         .OrderBy(n => n).ToArray();
                CollectionAssert.AreEqual(expected, names, $"{ShopKit.StemFor(e)} facing names");
            }
        }

        [Test]
        public void EveryFacingSharesOneCellAndOnePivot_SoAShopDoesNotShiftWhenItTurns()
        {
            RequireSheetsOnDisk();
            foreach (var e in ShopKit.AllEntries(_contract))
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(ShopKit.SheetPathFor(e))
                                           .OfType<Sprite>().ToArray();
                if (sprites.Length == 0) continue;

                Vector2 pivot = sprites[0].pivot;
                Vector2 size = sprites[0].rect.size;
                foreach (var s in sprites)
                {
                    Assert.AreEqual(pivot.x, s.pivot.x, 0.01f, $"{s.name} pivot x differs from facing 0");
                    Assert.AreEqual(pivot.y, s.pivot.y, 0.01f, $"{s.name} pivot y differs from facing 0");
                    Assert.AreEqual(size.x, s.rect.width, 0.01f, $"{s.name} cell width differs");
                    Assert.AreEqual(size.y, s.rect.height, 0.01f, $"{s.name} cell height differs");
                }
            }
        }

        [Test]
        public void ThePivotIsTheGroundCentre_AndBottomCentreWouldSinkEveryShop()
        {
            // Measured, not asserted in the abstract. ArtImportPipeline.PivotFor sends everything under
            // /buildings/ to BottomCenter and this kit lives under Sprites/Buildings/Shops/, so this is
            // the specific default being overridden and the specific error being avoided.
            RequireSheetsOnDisk();
            foreach (var e in ShopKit.AllEntries(_contract))
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(ShopKit.SheetPathFor(e))
                                           .OfType<Sprite>().ToArray();
                Vector2 expected = ShopKit.PivotPixels(e);

                foreach (var s in sprites)
                {
                    Assert.AreEqual(expected.x, s.pivot.x, 0.01f, $"{s.name} pivot x");
                    Assert.AreEqual(expected.y, s.pivot.y, 0.01f, $"{s.name} pivot y");
                }

                float sinkMetres = ShopKit.BelowGroundPad(e) / (float)_contract.ppu;
                Assert.Greater(sinkMetres, 1f,
                    $"{ShopKit.StemFor(e)}: bottom-centre would sink it by only {sinkMetres:0.00} m. " +
                    "That is suspiciously small for a ¾ camera — either the pivot convention moved or " +
                    "the contract is stale.");
            }
        }

        // ---- the grid maths, without assets ----------------------------------------------------

        [Test]
        public void BuildRectsWalksTheGridRowMajorFromTheTopLeft_AndStopsAtTheFacingCount()
        {
            // The general store's shell packs 7×2 = FOURTEEN cells for eight facings, so "stops at the
            // facing count" is not a corner case here: six cells are empty, and a sprite over an empty
            // cell is a transparent rect a placement tool would happily offer.
            var entry = new ShopKit.Entry
            {
                key = "generalStore", level = "", facings = 8,
                cols = 7, rows = 2, cellW = 554, cellH = 517,
                sheetW = 3878, sheetH = 1034, pivotX = 277f, pivotY = 338f,
            };

            SpriteRect[] rects = ShopSheetSlicer.BuildRects(entry);
            Assert.AreEqual(8, rects.Length, "six of the fourteen cells are empty and must not be cut");

            // Row 0 is the TOP row, which is the HIGHEST y in Unity's bottom-origin rects.
            Assert.AreEqual(new Rect(0, 517, 554, 517), rects[0].rect, "facing 0 is the top-left cell");
            Assert.AreEqual(new Rect(6 * 554, 517, 554, 517), rects[6].rect, "facing 6 ends the top row");
            Assert.AreEqual(new Rect(0, 0, 554, 517), rects[7].rect, "facing 7 wraps to the bottom row");

            Vector2 pivot = ShopKit.NormalizedPivot(entry);
            foreach (var r in rects)
            {
                Assert.AreEqual(SpriteAlignment.Custom, r.alignment, $"{r.name} alignment");
                Assert.AreEqual(pivot, r.pivot, $"{r.name} pivot");
            }

            CollectionAssert.AreEqual(
                Enumerable.Range(0, 8).Select(f => $"Shopfront_generalStore_d{f}").ToArray(),
                rects.Select(r => r.name).ToArray());
        }

        /// <summary>
        /// A LEVEL's stem must never collide with a SHELL's. They share a folder, so a stem built with
        /// the wrong prefix names a sheet that EXISTS — and the slicer would cut a shell against an
        /// interior's cell without anything throwing.
        /// </summary>
        [Test]
        public void ShellAndLevelStemsAreDistinct_AndEachResolvesBackToItsOwnEntry()
        {
            foreach (var e in ShopKit.AllEntries(_contract))
            {
                string stem = ShopKit.StemFor(e);
                ShopKit.Entry back = ShopKit.EntryForStem(_contract, stem);
                Assert.IsNotNull(back, $"'{stem}' does not resolve back to an entry");
                Assert.AreEqual(e.key, back.key, $"'{stem}' resolved to the wrong build");
                Assert.AreEqual(e.level, back.level, $"'{stem}' resolved to the wrong level");
            }

            var stems = ShopKit.AllEntries(_contract).Select(ShopKit.StemFor).ToArray();
            CollectionAssert.AllItemsAreUnique(stems, "two entries share a sheet stem");
        }

        // ---- the door anchors placement will read ----------------------------------------------

        /// <summary>
        /// 🔴 <b>WHICH WAY THE DOORS TURN, measured from the baked anchors rather than derived.</b>
        ///
        /// <para>Cell <c>i</c> of every rig in this repo is rendered at <c>RigBaker.DirForCell</c>, which
        /// for a counter-clockwise rig hands the rig <c>dir = (8 − i) mod 8</c> — so the MODEL turns
        /// <c>−45°</c> per cell under a fixed camera and a feature's ground bearing DECREASES as the cell
        /// index rises. That is a claim about pixels, so it is checked against pixels: the contract's
        /// per-facing door anchors, un-squashed into the ground plane, must step consistently one way.</para>
        ///
        /// <para>⚠️ The un-squash is not optional. The anchors are screen offsets and the world XY plane
        /// is the SQUASHED ground plane (<c>sin 40° ≈ 0.643</c>), so an angle taken straight from screen
        /// px is not a ground bearing. Taking angles without un-squashing first is a mistake this repo
        /// has made before; here it would not flip the sign, but it would move each step by up to 20°
        /// and make "45° per cell" unprovable.</para>
        ///
        /// <para><b>This test found the bug it was written to rule out.</b> On the first real bake the
        /// LEVEL sheets stepped <b>+45°</b> per cell and the shells <b>−45°</b>: <c>ShopLevelBaker</c>
        /// handed the cell index straight to the rig as its <c>dir</c> while <c>BuildingRigBaker</c>
        /// applied <c>RigBaker.DirForCell</c>'s counter-clockwise inversion. Six facings in eight had the
        /// interior mirrored inside its own shell, and every check in <c>ShopRegistrationProbe</c> passed
        /// — it compares the two RIGS, and the rigs were never the problem.</para>
        ///
        /// <para><b>⚠️ Exact only for a FLOOR anchor.</b> A shell's door anchor sits up its wall, and that
        /// constant height term does not rotate — un-squashing amplifies it by 1/0.643 and wobbles the
        /// per-step figure by ±5°. The level plans' anchors are on the floor and step exactly 45°, so the
        /// tight assert is theirs and the shells get the sign and the total.</para>
        /// </summary>
        [Test]
        public void TheDoorBearingTurnsOneWay_FortyFiveDegreesPerCell()
        {
            const float DepthScale = 0.6427876f;   // sin 40°, the shared bake camera

            foreach (var e in ShopKit.AllEntries(_contract))
            {
                if (e.doorX == null || e.doorX.Length != e.facings) continue;
                bool floorAnchor = !string.IsNullOrEmpty(e.level);

                var bearings = new float[e.facings];
                for (int i = 0; i < e.facings; i++)
                {
                    // Contract anchors are top-left origin, so screen-up is −y. Un-squash to get ground.
                    float dx = e.doorX[i] - e.pivotX;
                    float dy = -(e.doorY[i] - e.pivotY) / DepthScale;
                    bearings[i] = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                }

                float perCell = 360f / e.facings;
                float total = 0f;

                for (int i = 0; i < e.facings; i++)
                {
                    float step = Mathf.DeltaAngle(bearings[i], bearings[(i + 1) % e.facings]);
                    total += step;

                    Assert.Less(step, 0f,
                        $"{ShopKit.StemFor(e)}: the door bearing INCREASED from cell {i} to " +
                        $"{(i + 1) % e.facings} ({bearings[i]:0.0}° → " +
                        $"{bearings[(i + 1) % e.facings]:0.0}°). Every sheet in this kit must turn the " +
                        "same way; a family that turns the other way is mirrored at six facings in " +
                        "eight, and cells 0 and 4 still look perfect.");

                    if (floorAnchor)
                        Assert.AreEqual(-perCell, step, 0.01f,
                            $"{ShopKit.StemFor(e)}: cell {i}→{(i + 1) % e.facings} turned {step:0.00}°. " +
                            "A floor anchor is a rigid rotation about the pivot, so its step is exact.");
                    else
                        Assert.That(step, Is.InRange(-2f * perCell, -0.4f * perCell),
                            $"{ShopKit.StemFor(e)}: cell {i}→{(i + 1) % e.facings} turned {step:0.0}°, " +
                            $"not one step of a {e.facings}-facing turntable.");
                }

                Assert.AreEqual(-360f, total, 1f,
                    $"{ShopKit.StemFor(e)}: the {e.facings} steps sum to {total:0.0}°, not one full turn.");
            }
        }

        /// <summary>
        /// 🔴 <b>THE CHECK IN THE FRAME THAT SHIPS.</b> A level's door anchor must sit at the same
        /// screen-x as its shell's <b>cell by cell</b> — the two are the same projected ground point,
        /// written by two different bakers into two different sheets.
        ///
        /// <para>This is the assertion that would have caught the mirrored levels on the day, and none of
        /// the rig-frame checks could: <c>ShopRegistrationProbe</c> compares the RIGS in their own
        /// <c>dir</c> units, where they agree exactly, and a baker sits between the rig and the sheet.
        /// A mirror shows here as equal-and-opposite x at the six non-axial cells.</para>
        /// </summary>
        [Test]
        public void EveryLevelsDoorLandsOnItsShellsDoor_CellByCell()
        {
            foreach (var level in _contract.levels)
            {
                ShopKit.Entry shell = ShopKit.FindShell(_contract, level.key);
                Assert.IsNotNull(shell, $"{level.key}: no shell to register against");
                if (shell.doorX == null || level.doorX == null) continue;
                Assert.AreEqual(shell.doorX.Length, level.doorX.Length, $"{level.key}: facing counts");

                for (int cell = 0; cell < shell.doorX.Length; cell++)
                {
                    float sx = shell.doorX[cell] - shell.pivotX;
                    float lx = level.doorX[cell] - level.pivotX;
                    Assert.AreEqual(sx, lx, 1.5f,
                        $"{level.key} cell {cell}: the {level.level} plan's door is at x={lx:+0.0;-0.0} " +
                        $"from its pivot and the shell's at x={sx:+0.0;-0.0}. Equal and opposite means " +
                        "the two sheets are MIRRORED — one baker applied the cell → dir azimuth " +
                        "correction and the other did not.");
                }
            }
        }

        /// <summary>
        /// The cell whose door anchor sits LOWEST on screen is on the camera side — never cell 0, because
        /// both rigs put the street door on the +Y gable and +Y projects AWAY from the camera.
        ///
        /// <para><b>⚠️ It is NOT reliably <c>facings/2</c>, and that is worth knowing before anyone builds
        /// placement on it.</b> <c>VillageBuildingKit.FrontFacing</c> picks the lowest anchor, which is
        /// exact for a door centred on its gable — every village house — and AMBIGUOUS here. Measured: the
        /// general store and the post office give 4, and the restaurant gives <b>5</b>, on a 2 px
        /// difference between cells 4 and 5 (481.02 against 483.03). Its entrance is off-centre along the
        /// front wall, so the anchor is not on the footprint's axis and two cells nearly tie. Placement
        /// must therefore turn a shop by the whole measured bearing table, not by an index derived from
        /// one anchor.</para>
        /// </summary>
        [Test]
        public void TheLowestDoorAnchorIsOnTheCameraSide_AndNeverCellZero()
        {
            foreach (var e in ShopKit.AllEntries(_contract))
            {
                if (e.doorY == null || e.doorY.Length != e.facings) continue;

                int front = 0;
                for (int i = 1; i < e.facings; i++)
                    if (e.doorY[i] > e.doorY[front]) front = i;

                Assert.AreNotEqual(0, front,
                    $"{ShopKit.StemFor(e)}: the door faces the camera at cell 0. Cell 0 shows the +Y " +
                    "gable, which points AWAY from the camera — if this moved, the rig re-seated its " +
                    "doorway and every placement is a half-turn out.");

                // The camera side is the half of the turntable around facings/2 — a weaker claim than
                // "equals facings/2", and the true one for an off-centre door.
                int fromBack = Mathf.Min(front, e.facings - front);
                Assert.GreaterOrEqual(fromBack, e.facings / 2 - 1,
                    $"{ShopKit.StemFor(e)}: the lowest anchor is at cell {front}, which is on the BACK " +
                    "half of the turntable.");
            }
        }
    }
}
