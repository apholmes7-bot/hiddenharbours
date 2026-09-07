using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the baked slice of the CATCH STORAGE sheets under
    /// <c>Assets/_Project/Art/Fishing/Storage/</c> — the container-fill wave: catch item strips
    /// (lobster / crab / mussel / clam), the insulated tote (5 colours × 3 lids + the opening
    /// mask) and the bucket kit (3 tiers × fills × catches). Sibling of
    /// <c>FishingKitSheetSliceTests</c>, same architecture: the slice lives in the <c>.meta</c>,
    /// so a drifted grid or flipped pivot lands as silently wrong sprites unless pinned here.
    ///
    /// <para><b>Cells and pivots are restated as literals ON PURPOSE</b>, imported from neither
    /// <c>CatchStorageSheetSlicer</c> nor the rigs — asserting the slicer's config against the
    /// slicer's config is the self-referential blind spot that shipped the mirrored boat art. The
    /// slicer↔rig half of the loop is closed by <c>CatchStorageBakeTests</c> against the LIVE
    /// rigs: tote 64×72 pivot (32,60) ground-centre; bucket 48×52 REST pivot (24,42) base-centre;
    /// crustacean items 64×64 (32,36); shellfish items 14×12 (7,10). Container sheets are 8
    /// direction rows × 1 frame; item strips ONE row × 4 lay variants; the mask matches the tote
    /// exactly (it must overlay pixel-on-pixel).</para>
    ///
    /// <para><b>The clam hod is THREE stems on one cell.</b> <c>Hod2_back</c> and
    /// <c>Hod2_front</c> are the far and near halves of the wire, and <c>Hod2_heap_&lt;band&gt;</c>
    /// is what goes between them — same 40×40 cell, same (20,30) pivot, so the three draw on one
    /// transform and the same <c>Hod2_</c> slicer spec covers all of them. Only the four bands with
    /// a non-zero fill fraction are baked; <c>empty</c> heaps nothing.</para>
    ///
    /// <para><b>The kit has LANDED</b> — every stem once started in <see cref="AwaitingOwnerBake"/>
    /// (the owner-bake guard pattern of PR #252/#260: sheets specced before any PNG exists, each
    /// per-sheet test reading Skipped honestly until the owner ran <i>Hidden Harbours ▸ Art ▸ Bake
    /// Catch Storage Kit</i>). All 59 arrived on 2026-07-25 and the set is now empty, so every
    /// stem is held to every assertion and a missing sheet fails loudly.</para>
    /// </summary>
    public class CatchStorageSheetSliceTests
    {
        private const string Storage = "Assets/_Project/Art/Fishing/Storage/";

        // ---- the guarded set, built from the kit's stated axes --------------------------------

        private static readonly string[] ItemKinds = { "lobster", "crab", "mussel", "clam" };
        private static readonly string[] ToteColours = { "navy", "steel", "plast", "rust", "teal" };
        private static readonly string[] ToteLids = { "on", "off", "lean" };
        private static readonly string[] BucketTiers = { "pail", "tote", "tray" };
        private static readonly string[] BucketFills = { "few", "half", "full", "brim" };

        /// <summary>The hod’s heap bands — <c>ClamHod.FILLS</c> minus <c>empty</c>, which heaps
        /// nothing and therefore has no sheet (an empty hod is the back+front pair with nothing
        /// between them). Restated as literals here like every other axis in this file.</summary>
        private static readonly string[] HodHeapBands = { "few", "half", "full", "brim" };
        private static readonly string[] BucketCatches = { "fish", "shell", "crust" };

        private readonly struct Kit
        {
            public readonly Vector2Int Cell;
            public readonly int Rows;
            public readonly int Frames;        // storage sheets have FIXED frame counts per stem
            public readonly Vector2 PivotPx;   // bottom-origin PIXELS within one cell
            public Kit(int w, int h, int rows, int frames, float pivotPxX, float pivotPxY)
            {
                Cell = new Vector2Int(w, h); Rows = rows; Frames = frames;
                PivotPx = new Vector2(pivotPxX, pivotPxY);
            }
        }

        // Pivots restated in BOTTOM-origin pixels (what Unity stores): tote (32,60) top-left on a
        // 72-tall cell → y = 72−60 = 12; bucket rest (24,42) on 52 → 10; crustacean (32,36) on
        // 64 → 28; shellfish (7,10) on 12 → 2.
        private static readonly Kit ToteKit = new Kit(64, 72, rows: 8, frames: 1, 32, 12);
        private static readonly Kit BucketKit = new Kit(48, 52, rows: 8, frames: 1, 24, 10);
        private static readonly Kit CrustItemKit = new Kit(64, 64, rows: 1, frames: 4, 32, 28);
        private static readonly Kit ShellItemKit = new Kit(14, 12, rows: 1, frames: 4, 7, 2);

        // ---- catch pass 2 ---------------------------------------------------------------------
        //
        // Bottom-origin again: Crustacean2's ground pivot (32,40) on a 64-tall cell → 64−40 = 24;
        // its HELD pivot (32,12) → 64−12 = 52, high in the cell because the animal dangles BELOW
        // the grip; the hod's (20,30) on 40 → 10; Shellfish2's item pivot (4,6) on 8 → 2, and its
        // handful GRIP pivot (4,4) → 4.
        //
        // ⚠️ The pass-2 shellfish cell is 8×8, not pass 1's 14×12. That is the strict world scale,
        // not a mistake: at scale 1 a mussel is 2×1 px and a periwinkle is ONE pixel.
        private static readonly string[] Pass2ShellKinds =
            { "mussel", "clam", "scallop", "oyster", "periwinkle" };

        /// <summary>The poses each animal genuinely owns. The rig remaps what it cannot do to
        /// <c>walk</c> with zero pixels of difference, so these are what
        /// <c>CatchPass2StorageBaker.PosesOwnedBy</c> discovers by RENDERING; the remaps themselves
        /// are asserted against the rig by <c>CatchPass2KitTests</c>.</summary>
        private static readonly (string pose, int frames)[] LobsterPoses =
            { ("walk", 4), ("rear", 1), ("defend", 1), ("flip", 4) };
        private static readonly (string pose, int frames)[] CrabPoses =
            { ("walk", 4), ("rear", 1), ("defend", 1), ("sidle", 4), ("burrow", 4) };

        private static readonly Kit Crust2HeldKit = new Kit(64, 64, rows: 8, frames: 2, 32, 52);
        private static readonly Kit Hod2Kit = new Kit(40, 40, rows: 8, frames: 1, 20, 10);
        private static readonly Kit Shell2ItemKit = new Kit(8, 8, rows: 1, frames: 4, 4, 2);
        private static readonly Kit Shell2HandKit = new Kit(8, 8, rows: 1, frames: 2, 4, 4);
        private static readonly Kit CatchItem2CrustKit = new Kit(64, 64, rows: 1, frames: 4, 32, 24);
        private static readonly Kit CatchItem2ShellKit = new Kit(8, 8, rows: 1, frames: 4, 4, 2);

        static Kit Crust2Kit(int frames) => new Kit(64, 64, rows: 8, frames: frames, 32, 24);

        private static readonly Dictionary<string, Kit> Sheets = BuildGuardedSet();

        private static Dictionary<string, Kit> BuildGuardedSet()
        {
            var d = new Dictionary<string, Kit>();
            foreach (var kind in ItemKinds)
                d[$"CatchItem_{kind}"] = kind == "lobster" || kind == "crab" ? CrustItemKit : ShellItemKit;
            foreach (var colour in ToteColours)
                foreach (var lid in ToteLids)
                    d[$"Tote_{colour}_{lid}"] = ToteKit;
            d["ToteMask"] = ToteKit;
            foreach (var tier in BucketTiers)
            {
                d[$"Bucket_{tier}_empty"] = BucketKit;
                foreach (var fill in BucketFills)
                    foreach (var catchKind in BucketCatches)
                        d[$"Bucket_{tier}_{fill}_{catchKind}"] = BucketKit;
            }

            // ---- catch pass 2 (every stem carries a 2, so pass 1 above is untouched) ----------
            foreach (var (pose, frames) in LobsterPoses) d[$"Crust2_lobster_{pose}"] = Crust2Kit(frames);
            foreach (var (pose, frames) in CrabPoses) d[$"Crust2_crab_{pose}"] = Crust2Kit(frames);
            d["Crust2Held_lobster"] = Crust2HeldKit;
            d["Crust2Held_crab"] = Crust2HeldKit;

            foreach (var kind in Pass2ShellKinds)
            {
                d[$"Shell2_{kind}"] = Shell2ItemKit;
                d[$"Shell2Hand_{kind}"] = Shell2HandKit;
                d[$"CatchItem2_{kind}"] = CatchItem2ShellKit;
            }

            d["Hod2_back"] = Hod2Kit;
            d["Hod2_front"] = Hod2Kit;
            foreach (var band in HodHeapBands) d[$"Hod2_heap_{band}"] = Hod2Kit;
            d["CatchItem2_lobster"] = CatchItem2CrustKit;
            d["CatchItem2_crab"] = CatchItem2CrustKit;

            // pass 1: 4 + 5×3 + 1 + 3×(1 + 4×3) = 59
            // pass 2: (4 + 5) crustacean poses + 2 held + 5×3 shellfish + 2 hod layers
            //         + 4 hod heap bands + 2 crust items = 34
            return d;   // 93 stems
        }

        /// <summary>
        /// Guarded stems whose sheets are SPECIFIED but not yet on disk. <b>EMPTY since the owner
        /// ran the bake (2026-07-25) and all 59 sheets landed</b> — per this set's own rule, so a
        /// missing sheet now fails loudly instead of reading as a patient Skip.
        /// </summary>
        private static readonly HashSet<string> AwaitingOwnerBake = new HashSet<string>();

        private static bool OnDisk(string stem) => File.Exists(Storage + stem + ".png");

        /// <summary>Sentinel yielded while EVERY stem awaits the owner's bake — NUnit fails an
        /// empty [TestCaseSource] outright, so the pre-bake state must yield SOMETHING and read
        /// as Skipped, honestly, instead of a fake red or a fake green.</summary>
        private const string NothingBakedYet = "@awaiting-owner-bake";

        private static IEnumerable<string> AllSheets()
        {
            string[] present = Sheets.Keys.Where(s => !AwaitingOwnerBake.Contains(s) || OnDisk(s))
                                     .OrderBy(s => s).ToArray();
            return present.Length > 0 ? present : new[] { NothingBakedYet };
        }

        private static void SkipIfNothingBaked(string stem)
        {
            if (stem == NothingBakedYet)
                Assert.Ignore("Every catch-storage sheet is awaiting the owner's bake (Hidden " +
                              "Harbours ▸ Art ▸ Bake Catch Storage Kit) — nothing to assert yet.");
        }

        private static Kit KitOf(string stem) => Sheets[stem];

        /// <summary>⚠️ Multiple-mode sheets return null from LoadAssetAtPath&lt;Sprite&gt; — LoadAllAssets is the rule.</summary>
        private static Sprite[] LoadSlices(string stem) =>
            AssetDatabase.LoadAllAssetsAtPath(Storage + stem + ".png").OfType<Sprite>().ToArray();

        private static Texture2D LoadSheet(string stem)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Storage + stem + ".png");
            Assert.IsNotNull(tex, $"{stem}.png: failed to load as Texture2D — is the PNG (and its .meta) committed?");
            return tex;
        }

        // ---- the assertions -------------------------------------------------------------------

        [Test]
        public void TheGuardedSet_IsTheFullKit()
        {
            // The set arithmetic, so an accidental drop of a colour/tier/fill/band is loud:
            // pass 1: 4 items + 5 colours × 3 lids + 1 mask + 3 tiers × (1 empty + 4 fills × 3 catches) = 59,
            // + pass 2: 7 catch items + 9 crustacean poses + 2 held + 5 shells + 5 handfuls
            //   + 2 hod layers + 4 hod heap bands = 34.
            Assert.AreEqual(4 + 5 * 3 + 1 + 3 * (1 + 4 * 3) + 34, Sheets.Count);
            Assert.AreEqual(93, Sheets.Count);
        }

        [Test]
        public void TheSlicerManifest_CoversEveryGuardedStem()
        {
            // Every guarded stem must resolve to a slicer kit whose geometry matches this test's
            // independent literals — a stem the slicer would refuse (or slice to another grid)
            // fails here BEFORE the owner ever bakes.
            foreach (var kv in Sheets)
            {
                FishingSheetSlicer.KitSpec? spec = CatchStorageSheetSlicer.KitFor(kv.Key);
                Assert.IsNotNull(spec, $"{kv.Key}: no slicer kit covers this stem");
                Assert.AreEqual(kv.Value.Cell, spec.Value.Cell, $"{kv.Key}: slicer cell differs");
                Assert.AreEqual(kv.Value.Rows, spec.Value.Rows, $"{kv.Key}: slicer rows differ");
                Assert.AreEqual(kv.Value.PivotPx.x, spec.Value.NormalizedPivot.x * kv.Value.Cell.x, 1e-4f,
                                $"{kv.Key}: slicer pivot.x differs");
                Assert.AreEqual(kv.Value.PivotPx.y, spec.Value.NormalizedPivot.y * kv.Value.Cell.y, 1e-4f,
                                $"{kv.Key}: slicer pivot.y differs");
            }
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_IsSlicedMultipleMode_IntoItsKitsGrid(string stem)
        {
            SkipIfNothingBaked(stem);
            var importer = AssetImporter.GetAtPath(Storage + stem + ".png") as TextureImporter;
            Assert.IsNotNull(importer, $"{stem}: no TextureImporter — is the .meta committed?");
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode,
                            $"{stem}: must stay grid-sliced (Multiple), not a Single sprite");

            var tex = LoadSheet(stem);
            Kit kit = KitOf(stem);

            Assert.AreEqual(kit.Frames * kit.Cell.x, tex.width,
                            $"{stem}: this sheet is {kit.Frames} column(s) of {kit.Cell.x} px");
            Assert.AreEqual(kit.Rows * kit.Cell.y, tex.height,
                            $"{stem}: this kit's sheets are {kit.Rows} row(s) × {kit.Cell.y} px tall");
            Assert.AreEqual(kit.Rows * kit.Frames, LoadSlices(stem).Length,
                            $"{stem}: expected {kit.Rows} × {kit.Frames} slices");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_ImportsAtNativeRes_NotDownscaled(string stem)
        {
            SkipIfNothingBaked(stem);
            // The tallest sheet here is 576 px (tote), far under the 2048 default cap — assert
            // anyway: a downscale keeps the sprite COUNT plausible and only this catches it.
            var tex = LoadSheet(stem);
            var slices = LoadSlices(stem);
            Assert.IsNotEmpty(slices, $"{stem}: no slices loaded");
            Assert.AreEqual(tex.width, slices.Max(s => s.rect.xMax), 0.01f,
                            $"{stem}: slices do not span the sheet width — importer downscaled or grid drifted");
            Assert.AreEqual(tex.height, slices.Max(s => s.rect.yMax), 0.01f,
                            $"{stem}: slices do not span the sheet height — importer downscaled or grid drifted");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void EverySlice_IsOneCell_AndPivotsOnItsKitsAnchorPoint(string stem)
        {
            SkipIfNothingBaked(stem);
            // ⚠️ Pixels, not normalized: a flipped pivot reads as a plausible number and silently
            // floats every container off its deck spot.
            Kit kit = KitOf(stem);
            var slices = LoadSlices(stem);
            Assert.IsNotEmpty(slices, $"{stem}: no slices loaded");
            foreach (var s in slices)
            {
                Assert.AreEqual(kit.Cell.x, s.rect.width, 0.01f, $"{s.name}: cell width drifted");
                Assert.AreEqual(kit.Cell.y, s.rect.height, 0.01f, $"{s.name}: cell height drifted");
                Assert.AreEqual(kit.PivotPx.x, s.pivot.x, 0.01f, $"{s.name}: pivot.x off the kit's anchor");
                Assert.AreEqual(kit.PivotPx.y, s.pivot.y, 0.01f,
                                $"{s.name}: pivot.y off the kit's anchor point — is it inverted? " +
                                $"Unity stores bottom-origin px; this kit's anchor is {kit.PivotPx.y} " +
                                $"px up from the cell bottom ({kit.Cell.y - kit.PivotPx.y} down from the top)");
            }
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Slices_TileTheSheet_AndAreNamedByRowIndex(string stem)
        {
            SkipIfNothingBaked(stem);
            var tex = LoadSheet(stem);
            Kit kit = KitOf(stem);
            int cols = tex.width / kit.Cell.x;

            var occupied = new HashSet<(int, int)>();
            foreach (var s in LoadSlices(stem))
            {
                StringAssert.StartsWith(stem + "_d", s.name, $"{s.name}: unexpected slice name");
                string tail = s.name.Substring(stem.Length + 2);
                string[] parts = tail.Split(new[] { "_f" }, System.StringSplitOptions.None);
                Assert.AreEqual(2, parts.Length, $"{s.name}: must be <stem>_d<row>_f<col>");
                Assert.IsTrue(int.TryParse(parts[0], out int d), $"{s.name}: unparseable row index");
                Assert.IsTrue(int.TryParse(parts[1], out int f), $"{s.name}: unparseable frame index");

                Assert.AreEqual(0, Mathf.RoundToInt(s.rect.x) % kit.Cell.x, $"{s.name}: x off the cell grid");
                Assert.AreEqual(0, Mathf.RoundToInt(s.rect.y) % kit.Cell.y, $"{s.name}: y off the cell grid");
                var c = (Mathf.RoundToInt(s.rect.x) / kit.Cell.x, Mathf.RoundToInt(s.rect.y) / kit.Cell.y);
                Assert.IsTrue(occupied.Add(c), $"{s.name}: two slices overlap cell {c}");

                // Row 0 is the TOP row of the canvas; Unity rects are bottom-origin.
                int rectRowFromTop = kit.Rows - 1 - Mathf.RoundToInt(s.rect.y) / kit.Cell.y;
                Assert.AreEqual(d, rectRowFromTop,
                                $"{s.name}: name says row {d} but the rect sits at row {rectRowFromTop} from the top");
                Assert.AreEqual(f, Mathf.RoundToInt(s.rect.x) / kit.Cell.x,
                                $"{s.name}: name says frame {f} but the rect sits in a different column");
            }

            for (int c = 0; c < cols; c++)
                for (int r = 0; r < kit.Rows; r++)
                    Assert.IsTrue(occupied.Contains((c, r)), $"{stem}: no slice covers cell (col {c}, row {r})");
        }

        [Test]
        public void EveryStoragePngInTheFolder_IsCoveredByThisTest()
        {
            // A new sheet dropped into the folder must not slip past the guard unnoticed; a
            // spec'd-but-unbaked sheet is tolerated as absent; an unguarded png on disk fails.
            var onDisk = Directory.Exists(Storage)
                ? Directory.GetFiles(Storage, "*.png")
                           .Select(Path.GetFileNameWithoutExtension)
                           .OrderBy(s => s)
                           .ToArray()
                : new string[0];
            string[] expected = Sheets.Keys.Where(s => !AwaitingOwnerBake.Contains(s) || OnDisk(s))
                                     .OrderBy(s => s).ToArray();
            CollectionAssert.AreEquivalent(expected, onDisk,
                                           "Catch storage sheets on disk differ from the guarded set");
        }
    }
}
