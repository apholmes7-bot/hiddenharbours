using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE SEAGULL SHEET SLICER — the grid guards.</b>
    ///
    /// <para>The slicer takes every number from the contract <see cref="SeagullBaker"/> writes beside
    /// the sheet, because the asmdef edge runs <c>RigBaking.Editor → Art.Editor</c> and never back.
    /// That is the right shape, and it has one cost: the two ends of the contract are stated twice,
    /// in two assemblies, and can drift. <see cref="TheSlicerAgreesWithTheBakerAboutTheSheet"/> pins
    /// them together.</para>
    ///
    /// <para>Everything else here is a refusal. A slicer's failures are all quiet ones — a zero cell
    /// slices 384 empty rects onto real art, a pivot read upside-down hangs every gull by its head,
    /// an <c>order</c> table one slot out names every picture after its neighbour, and all four
    /// import clean with the right sprite COUNT. Each one below is a test because none of them is
    /// visible in the Inspector.</para>
    ///
    /// <para>These run without the sheet on disk: the contract is built in code. The one test that
    /// needs the committed art says so and ignores itself until the bake lands
    /// (<see cref="TheCommittedContractIsOneTheSlicerAccepts"/>).</para>
    /// </summary>
    public class SeagullSheetSlicerTests
    {
        const int Cell = 64;
        const int PivotXPx = 32;
        const int PivotYPx = 46;
        const int Rows = 8;
        const int Columns = 48;

        /// <summary>The rig's own <c>AORDER</c> × frame counts — 13 states, 48 columns. Written out
        /// rather than derived so a rig that reshapes shows up as a failure here too.</summary>
        static readonly (string anim, int frames)[] Order =
        {
            ("fly", 6), ("glide", 2), ("swoop", 4), ("dive", 4), ("splash", 5), ("float", 2),
            ("preen", 4), ("peck", 3), ("land", 4), ("stand", 2), ("walk", 6), ("perch", 2),
            ("takeoff", 4),
        };

        static SeagullSheetSlicer.Contract Valid()
        {
            var c = new SeagullSheetSlicer.Contract
            {
                sheet = SeagullSheetSlicer.SheetFolder + "/Seagull.png",
                cell = new SeagullSheetSlicer.CellSize { w = Cell, h = Cell },
                pivotTopLeft = new SeagullSheetSlicer.PivotPx { x = PivotXPx, y = PivotYPx },
                rows = Rows,
                pixelsPerUnit = 32,
                requiredMaxTextureSize = 4096,
                facingsAreCounterClockwise = false,
                order = new List<SeagullSheetSlicer.ColumnEntry>(),
            };
            int col = 0;
            foreach (var (anim, frames) in Order)
                for (int f = 0; f < frames; f++)
                    c.order.Add(new SeagullSheetSlicer.ColumnEntry { col = col++, anim = anim, frame = f });
            c.columns = c.order.Count;
            return c;
        }

        static void Refuses(SeagullSheetSlicer.Contract c, string mustMention)
        {
            Assert.IsFalse(SeagullSheetSlicer.Validate(c, out string why),
                           "the slicer accepted a contract it should have refused");
            StringAssert.Contains(mustMention, why,
                                  "the refusal does not say what is wrong: " + why);
        }

        // =================================================================================
        // THE TWO ENDS OF THE CONTRACT
        // =================================================================================

        /// <summary>
        /// The slicer restates the baker's output paths and direction count because it cannot
        /// reference the baker. Restated is not derived, so pin the two together: a bake that moves
        /// its folder and a slicer that keeps looking in the old one both "work", and the sheet
        /// simply never gets sliced.
        /// </summary>
        [Test]
        public void TheSlicerAgreesWithTheBakerAboutTheSheet()
        {
            Assert.AreEqual(SeagullBaker.DefaultOutputFolder, SeagullSheetSlicer.SheetFolder,
                            "the baker writes somewhere the slicer does not look");
            Assert.AreEqual($"{SeagullBaker.DefaultOutputFolder}/{SeagullBaker.ContractFileName}",
                            SeagullSheetSlicer.ContractPath,
                            "the slicer reads a contract path the baker does not write");
            Assert.AreEqual(SeagullBaker.Dirs, SeagullSheetSlicer.Directions,
                            "the baker's rows and the slicer's d0..dN disagree");
            Assert.AreEqual(4096, SeagullBaker.SizeCap,
                            "the seagull's own 4096 cap is what lets a 3072 px sheet exist at all; " +
                            "FishingKitBaker's is 2048");
        }

        /// <summary>
        /// ADR 0026, and the reason it is written down: the contract's pivot is TOP-LEFT origin and
        /// Unity's normalised pivot is BOTTOM-LEFT. 46 in a 64 px cell is 0.28125 up from the
        /// bottom, not 0.71875. Both are plausible numbers; only one puts the bird on its feet.
        /// </summary>
        [Test]
        public void TheNormalisedPivotFlipsTheYAxisExactlyOnce()
        {
            Vector2 p = Valid().NormalisedPivot;
            Assert.AreEqual(PivotXPx / (float)Cell, p.x, 1e-6f);
            Assert.AreEqual((Cell - PivotYPx) / (float)Cell, p.y, 1e-6f);
            Assert.AreEqual(0.28125f, p.y, 1e-6f, "the contact point is low in the cell");
            Assert.AreNotEqual(PivotYPx / (float)Cell, p.y,
                               "the y flip was skipped — every gull hangs by its head");
        }

        // =================================================================================
        // THE REFUSALS
        // =================================================================================

        [Test]
        public void AContractTheBakerWouldWriteIsAccepted()
        {
            Assert.IsTrue(SeagullSheetSlicer.Validate(Valid(), out string why), why);
            Assert.AreEqual(Columns, Valid().columns, "13 states come to 48 columns");
            Assert.AreEqual(Columns * Cell, Valid().SheetWidth);
            Assert.AreEqual(Rows * Cell, Valid().SheetHeight);
        }

        [Test]
        public void ANullContractIsRefused() => Refuses(null, "did not parse");

        /// <summary>JsonUtility zeroes an object it cannot shape rather than throwing, so this is
        /// what a moved contract looks like — and a zero cell slices 384 empty rects onto real art,
        /// which imports clean and draws nothing.</summary>
        [Test]
        public void AZeroCellIsRefused()
        {
            var c = Valid(); c.cell.w = 0;
            Refuses(c, "no cell size");
        }

        [Test]
        public void AMissingPivotIsRefused()
        {
            var c = Valid(); c.pivotTopLeft = null;
            Refuses(c, "CONTACT POINT");
        }

        [Test]
        public void APivotOutsideItsOwnCellIsRefused()
        {
            var c = Valid(); c.pivotTopLeft.y = Cell + 1;
            Refuses(c, "outside its own");
        }

        [Test]
        public void AFacingCountOtherThanEightIsRefused()
        {
            var c = Valid(); c.rows = 16;
            Refuses(c, "facing rows");
        }

        [Test]
        public void AnEmptyOrderTableIsRefused()
        {
            var c = Valid(); c.order.Clear(); c.columns = 0;
            Refuses(c, "no columns");
        }

        /// <summary>The contract states the sheet's width twice — as a count and as a table. Two
        /// statements of one fact must be the same statement.</summary>
        [Test]
        public void AColumnCountThatDisagreesWithTheOrderTableIsRefused()
        {
            var c = Valid(); c.columns = Columns - 1;
            Refuses(c, "in `order`");
        }

        /// <summary>Reading a missing cap back as 0 imports this 3072 px sheet at Unity's 2048
        /// default — silently downscaled, with the right sprite count.</summary>
        [Test]
        public void AMissingImportSizeCapIsRefused()
        {
            var c = Valid(); c.requiredMaxTextureSize = 0;
            Refuses(c, "requiredMaxTextureSize");
        }

        [Test]
        public void APixelsPerUnitOtherThanThirtyTwoIsRefused()
        {
            var c = Valid(); c.pixelsPerUnit = 16;
            Refuses(c, "PPU");
        }

        /// <summary>The <c>order</c> table IS the column→(state, frame) map. One slot out and every
        /// column takes its neighbour's name; all 384 rects still slice.</summary>
        [Test]
        public void AnOutOfOrderColumnIndexIsRefused()
        {
            var c = Valid();
            (c.order[3].col, c.order[4].col) = (c.order[4].col, c.order[3].col);
            Refuses(c, "column index");
        }

        [Test]
        public void TwoColumnsSharingAStateAndFrameAreRefused()
        {
            var c = Valid();
            c.order[7].anim = c.order[0].anim;
            c.order[7].frame = c.order[0].frame;
            Refuses(c, "twice");
        }

        [Test]
        public void AnUnnamedStateIsRefused()
        {
            var c = Valid(); c.order[12].anim = "";
            Refuses(c, "no state name");
        }

        // =================================================================================
        // THE RECTS
        // =================================================================================

        [Test]
        public void BuildRectsEmitsOneNamedRectPerCell()
        {
            var rects = SeagullSheetSlicer.BuildRects(Valid());
            Assert.AreEqual(Rows * Columns, rects.Length, "384 sprites: 48 columns × 8 facings");
            Assert.AreEqual(rects.Length, rects.Select(r => r.name).Distinct().Count(),
                            "two rects share a name — Unity keeps one, and the sheet is quietly short");
            CollectionAssert.Contains(rects.Select(r => r.name).ToArray(), "gull_fly_0_d0");
            CollectionAssert.Contains(rects.Select(r => r.name).ToArray(), "gull_takeoff_3_d7");
        }

        /// <summary>
        /// The name order is <c>gull_&lt;state&gt;_&lt;frame&gt;_d&lt;dir&gt;</c> — the charter's
        /// order, and NOT this repo's usual <c>&lt;Stem&gt;_d&lt;row&gt;_f&lt;col&gt;</c>. A creature
        /// is asked for by behaviour first. <c>d</c> is a facing INDEX, never a compass name.
        /// </summary>
        [Test]
        public void TheSpriteNameIsStateThenFrameThenFacingIndex()
        {
            Assert.AreEqual("gull_walk_5_d3", SeagullSheetSlicer.SpriteName("walk", 5, 3));
            StringAssert.DoesNotContain("_NE_", SeagullSheetSlicer.SpriteName("walk", 5, 3));
        }

        /// <summary>
        /// The bake blits row-major from the TOP-LEFT cell; Unity's rects are BOTTOM-origin. Row 0
        /// must therefore sit at the HIGHEST y. Flipping that twice — or not at all — produces art
        /// that is exactly upside-down in facing: every gull walks backwards, and nothing errors.
        /// </summary>
        [Test]
        public void RowZeroIsTheTopOfTheSheetAndTheRectsTileItExactly()
        {
            var c = Valid();
            var rects = SeagullSheetSlicer.BuildRects(c);

            var top = rects.First(r => r.name == "gull_fly_0_d0");
            Assert.AreEqual(0f, top.rect.x, 1e-4f);
            Assert.AreEqual((Rows - 1) * Cell, top.rect.y, 1e-4f, "dir 0 is the TOP row");

            var bottom = rects.First(r => r.name == "gull_fly_0_d7");
            Assert.AreEqual(0f, bottom.rect.y, 1e-4f, "dir 7 is the BOTTOM row");

            var expected = new HashSet<(int, int)>();
            for (int row = 0; row < Rows; row++)
                for (int col = 0; col < Columns; col++)
                    expected.Add((col * Cell, (Rows - 1 - row) * Cell));
            var got = new HashSet<(int, int)>(
                rects.Select(r => ((int)r.rect.x, (int)r.rect.y)));
            Assert.AreEqual(expected.Count, got.Count, "the rects overlap");
            CollectionAssert.AreEquivalent(expected, got,
                                           "the rects do not tile the sheet without gaps");
            Assert.IsTrue(rects.All(r => r.rect.width == Cell && r.rect.height == Cell));
        }

        [Test]
        public void EveryRectCarriesTheContactPointPivot()
        {
            var c = Valid();
            var rects = SeagullSheetSlicer.BuildRects(c);
            Assert.IsTrue(rects.All(r => r.alignment == SpriteAlignment.Custom),
                          "a non-Custom alignment throws the pivot away");
            Assert.IsTrue(rects.All(r => r.pivot == c.NormalisedPivot),
                          "a rect does not carry the contract's pivot");
            Assert.IsTrue(rects.All(r => r.border == Vector4.zero));
        }

        /// <summary>
        /// Re-slicing unchanged art must produce a byte-identical .meta. Generating a fresh GUID per
        /// run rewrites all 384 spriteIDs and buries the owner's real changes in the diff.
        /// </summary>
        [Test]
        public void BuildRectsReusesTheSpriteIdAnAlreadySlicedNameCarries()
        {
            var c = Valid();
            var first = SeagullSheetSlicer.BuildRects(c);
            var ids = first.ToDictionary(r => r.name, r => r.spriteID);

            var second = SeagullSheetSlicer.BuildRects(c, ids);
            Assert.IsTrue(second.All(r => r.spriteID == ids[r.name]),
                          "a re-slice invented new spriteIDs");

            var fresh = SeagullSheetSlicer.BuildRects(c);
            Assert.IsFalse(fresh.All(r => r.spriteID == ids[r.name]),
                           "without the map the IDs should be new — otherwise this test proves nothing");
        }

        // =================================================================================
        // THE COMMITTED ARTEFACT
        // =================================================================================

        /// <summary>
        /// The contract that actually ships, put through the same refusals. This was written to
        /// SKIP while the sheet was held — a test that quietly passes on a missing file is a test
        /// that stops noticing when the file arrives wrong — and it turns into the failure below in
        /// the commit that lands the sheet, because from here on "it is not there" is the defect.
        /// </summary>
        [Test]
        public void TheCommittedContractIsOneTheSlicerAccepts()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string abs = Path.Combine(root, SeagullSheetSlicer.ContractPath);
            if (!File.Exists(abs))
                Assert.Fail($"'{SeagullSheetSlicer.ContractPath}' is missing. It ships with the sheet " +
                            "and the two are committed together — a sheet without its contract is a " +
                            "grid nothing can re-cut. Run \"Hidden Harbours/Art/Bake Seagull\".");

            var c = JsonUtility.FromJson<SeagullSheetSlicer.Contract>(File.ReadAllText(abs));
            Assert.IsTrue(SeagullSheetSlicer.Validate(c, out string why), why);
            Assert.AreEqual(Columns, c.columns, "the committed sheet is 48 columns wide");
            Assert.AreEqual(Rows, c.rows);
            Assert.AreEqual(Cell, c.cell.w);
            Assert.AreEqual(Cell, c.cell.h);
            Assert.AreEqual(PivotXPx, c.pivotTopLeft.x, 1e-4f);
            Assert.AreEqual(PivotYPx, c.pivotTopLeft.y, 1e-4f);
            Assert.IsFalse(c.facingsAreCounterClockwise,
                           "the seagull rig is CLOCKWISE — measured from the bill's own pixels");
            CollectionAssert.AreEqual(
                Order.SelectMany(o => Enumerable.Range(0, o.frames).Select(f => $"{o.anim}:{f}")).ToArray(),
                c.order.Select(o => $"{o.anim}:{o.frame}").ToArray(),
                "the committed column order is not the rig's AORDER × frame counts");
        }

        /// <summary>
        /// ⚠️ <b>THE SPRITE COUNT IS NOT THE GUARD — the count is what fooled us.</b>
        ///
        /// <para>MEASURED 2026-09-10, the seagull's first headless bake. The slicer refused (its cap
        /// raise was stranded inside <c>AssetDatabase.StartAssetEditing()</c>, which defers every
        /// import), the bake logged success anyway, and the <c>.meta</c> left on disk carried 384
        /// sprite rects — EXACTLY the right number. They were Unity's automatic alpha-trimmed
        /// detection over the DOWNSCALED 2048×341 import: named <c>Seagull_0</c>…<c>Seagull_383</c>,
        /// rects like <c>x:857 y:462 w:14 h:18</c>, pivot <c>{0,0}</c>, alignment 0. The count read
        /// right only because there are 384 cells each holding exactly one bird, and every one of
        /// those blobs is one bird. A sheet in that state imports clean, reports its 384 sprites to
        /// anything that asks, and draws every gull from the wrong pixels hung by the wrong point.</para>
        ///
        /// <para>So this asserts the GRID, and it takes its bar from this fixture's own constants
        /// rather than from the contract sitting beside the sheet: every sprite is named by
        /// <see cref="SeagullSheetSlicer.SpriteName"/>, sits on the 64×64 lattice at the column its
        /// state and frame occupy in <see cref="Order"/>, and hangs at the ADR 0026 flip of the
        /// contract's top-left pivot. It is what turns that defect into a red in CI — no editor
        /// session, no eyes on the art.</para>
        /// </summary>
        [Test]
        public void TheCommittedSheetCarriesTheSlicersGrid_NotUnitysAutoDetectedRects()
        {
            const string sheet = SeagullSheetSlicer.SheetFolder + "/Seagull.png";
            string root = Directory.GetParent(Application.dataPath).FullName;
            if (!File.Exists(Path.Combine(root, sheet)))
                Assert.Fail($"'{sheet}' is missing. It ships in this commit, via Git LFS — an LFS " +
                            "pointer that was never smudged leaves the path present but this file " +
                            "absent. Run \"Hidden Harbours/Art/Bake Seagull\".");

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(sheet);
            Assert.IsNotNull(tex, $"'{sheet}' is on disk but does not load as a Texture2D — is its " +
                                  ".meta committed alongside it?");
            Assert.AreEqual(Columns * Cell, tex.width,
                "the committed sheet imported at the wrong width. 2048 or less means Unity's default " +
                "texture cap downscaled it SILENTLY: rects refitted, pivots thrown away, and the " +
                "sprite count still reads 384. maxTextureSize must be 4096 in the .meta.");
            Assert.AreEqual(Rows * Cell, tex.height, "the committed sheet imported at the wrong height.");

            // ⚠️ Multiple-mode sheets return null from LoadAssetAtPath<Sprite> — LoadAllAssetsAtPath
            // is the rule. Cf. FlowerCatalogTests.
            var sprites = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().ToArray();
            Assert.AreEqual(Rows * Columns, sprites.Length,
                "the committed sheet does not carry 384 sprites. ZERO means spriteImportMode is " +
                "Multiple with no rects at all — which imports clean and reads back as a correctly " +
                "configured sheet everywhere except here.");

            var byName = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var sp in sprites)
                Assert.IsTrue(byName.TryAdd(sp.name, sp), $"two sprites are both named '{sp.name}'.");

            // The ADR 0026 flip: the contract's pivot is TOP-left, Unity's normalised pivot is
            // BOTTOM-left. Spelled out here rather than read from Contract.NormalisedPivot, so a
            // sign error in production cannot also move this fixture's expectation.
            var wantPivot = new Vector2((float)PivotXPx / Cell, (float)(Cell - PivotYPx) / Cell);

            int col = 0;
            foreach (var (anim, frames) in Order)
                for (int f = 0; f < frames; f++, col++)
                    for (int row = 0; row < Rows; row++)
                    {
                        string name = SeagullSheetSlicer.SpriteName(anim, f, row);
                        Assert.IsTrue(byName.TryGetValue(name, out var sp),
                            $"no sprite named '{name}'. Names of the form 'Seagull_<n>' mean Unity's " +
                            "automatic sprite detection wrote this .meta and the slicer never ran — " +
                            "and it produces exactly 384 of them on this sheet, so the count above " +
                            "cannot tell you. Re-bake and commit the .meta the slicer writes.");

                        Assert.AreEqual(new Rect(col * Cell, (Rows - 1 - row) * Cell, Cell, Cell), sp.rect,
                            $"'{name}' is not on the 64×64 lattice. A rect narrower or shorter than a " +
                            "cell is alpha-fitted auto-detection. A rect on the wrong row is the " +
                            "bottom-origin flip applied twice — art that is very nearly right.");

                        var got = new Vector2(sp.pivot.x / sp.rect.width, sp.pivot.y / sp.rect.height);
                        Assert.AreEqual(wantPivot.x, got.x, 1e-4f, $"'{name}': pivot x is {got.x}.");
                        Assert.AreEqual(wantPivot.y, got.y, 1e-4f,
                            $"'{name}': pivot y is {got.y}, want {wantPivot.y} — the flip of the " +
                            $"contract's top-left {PivotYPx}. A pivot of 0 is auto-detection's default " +
                            "and hangs every gull by the corner of its trimmed box.");

                        Assert.AreEqual(32f, sp.pixelsPerUnit, 1e-4f,
                            $"'{name}': {sp.pixelsPerUnit} PPU, want 32 — one pixel per world " +
                            "1/32 m is the whole of the strict-scale ruling.");
                    }

            Assert.AreEqual(Columns, col,
                "the Order table does not walk 48 columns, so the sweep above skipped part of the sheet");
        }
    }
}
