using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the baked slice of the FISHING KIT sheets under
    /// <c>Assets/_Project/Art/Fishing/Iso/</c> — Rod Fishing v2 wave 3's fight art: the parametric
    /// fish (4 species × 8 states), the rod bobber (4 state strips) and the rod overlay
    /// (3 tiers × 9 states). The slice lives in the <c>.meta</c>, not in code, so nothing at
    /// runtime would notice it rotting: a re-bake that drifts the grid, a re-slice that loses a
    /// pivot, or an importer setting that downscales a sheet all land as silently wrong sprites.
    ///
    /// <para><b>THREE kits, three cells, three pivots — none of them ground contact.</b>
    /// Fish 64×64 pivoting on THE WATER-SURFACE POINT (32,38 top-left → normalized (0.5, 26/64));
    /// bobber 16×22 on THE WATERLINE (8,12 → (0.5, 10/22)), and only ONE row — it is a state
    /// sprite, not a turntable; rod 112×112 on THE GRIP (56,72 → (0.5, 40/112)). All three are
    /// restated here as literals ON PURPOSE, imported from neither <c>FishingSheetSlicer</c> nor
    /// the rigs — asserting the slicer's config against the slicer's config is the
    /// self-referential blind spot that let the mirrored boat art ship. (The slicer↔rig half of
    /// the loop is closed by <c>FishingKitBakeTests.SlicerKitSpecs_MatchTheLiveRigs</c>, which
    /// checks the slicer's constants against the live rigs on every run.)</para>
    ///
    /// <para><b>Expectations otherwise come from the ART:</b> frame counts, row counts and total
    /// sprite counts derive from the PNG dimensions read off disk. The one thing that cannot be
    /// derived is the cell size itself (a 448 px sheet is a whole number of both 64 px and 112 px
    /// cells), so it is the contract under test.</para>
    ///
    /// <para>Rows are emitted by the baker with each rig's MEASURED azimuth convention applied, so
    /// row d genuinely depicts heading 45°·d — and slices are still named by ROW INDEX
    /// (<c>_d&lt;row&gt;_f&lt;col&gt;</c>), never by compass name: a name states geometry, not
    /// semantics.</para>
    /// </summary>
    public class FishingKitSheetSliceTests
    {
        private const string Iso = "Assets/_Project/Art/Fishing/Iso/";

        // ---- the guarded set, built from the kit's stated axes --------------------------------

        private static readonly string[] FishSpecies = { "cod", "haddock", "pollock", "mackerel" };

        /// <summary>Water anims then dry rests, frame counts as the drop states them.</summary>
        private static readonly (string state, int frames)[] FishStates =
        {
            ("swim", 4), ("dart", 2), ("thrash", 4), ("shadow", 2),
            ("deck", 4), ("gill", 2), ("tail", 2), ("cradle", 2),
        };

        private static readonly (string state, int frames)[] BobberStates =
        {
            ("float", 4), ("nibble", 4), ("strike", 4), ("fly", 2),
        };

        private static readonly string[] RodTiers = { "cane", "coast", "deep" };

        /// <summary>The seven tool anims (frame counts from the character rig's ANIMS table, the
        /// same numbers CharacterRigBakeTests pins) plus the three rests.
        ///
        /// <para>The rests used to be two ONE-FRAME props, <c>ground</c> and <c>stored</c>. A rest of
        /// one cell cannot be entered from the fisher's hand without the rod jumping — which is what
        /// it did — so each rest is now a <c>RodIso.REST_FRAMES</c>-frame hand-over whose first frame
        /// is the hold stance, and <c>stored</c> is spelled <c>stowV</c> beside its new horizontal
        /// twin <c>stowH</c>. See <c>RodContinuityTests</c> for the law all three are held to.</para></summary>
        private static readonly (string state, int frames)[] RodStates =
        {
            ("hold", 6), ("bite", 6), ("strike", 6), ("reel", 12), ("land", 12),
            ("castBack", 6), ("castRelease", 8), ("ground", 6), ("stowV", 6), ("stowH", 6),
        };

        /// <summary>The rest stems, which the guards below hold pending the owner's re-bake.</summary>
        private static readonly string[] RodRestStates = { "ground", "stowV", "stowH" };

        private readonly struct Kit
        {
            public readonly Vector2Int Cell;
            public readonly int Rows;
            public readonly Vector2 PivotPx;   // bottom-origin PIXELS within one cell
            public Kit(int w, int h, int rows, float pivotPxX, float pivotPxY)
            { Cell = new Vector2Int(w, h); Rows = rows; PivotPx = new Vector2(pivotPxX, pivotPxY); }
        }

        // Pivots restated in BOTTOM-origin pixels (what Unity stores): fish (32,38) top-left on a
        // 64-tall cell → y = 64−38 = 26; bobber (8,12) on 22 → 10; rod (56,72) on 112 → 40.
        private static readonly Kit FishKit = new Kit(64, 64, rows: 8, pivotPxX: 32, pivotPxY: 26);
        private static readonly Kit BobberKit = new Kit(16, 22, rows: 1, pivotPxX: 8, pivotPxY: 10);
        private static readonly Kit RodKit = new Kit(112, 112, rows: 8, pivotPxX: 56, pivotPxY: 40);

        private static readonly Dictionary<string, Kit> Sheets = BuildGuardedSet();
        private static readonly Dictionary<string, int> ExpectedFrames = BuildExpectedFrames();

        private static Dictionary<string, Kit> BuildGuardedSet()
        {
            var d = new Dictionary<string, Kit>();
            foreach (var sp in FishSpecies)
                foreach (var (state, _) in FishStates) d[$"Fish_{sp}_{state}"] = FishKit;
            foreach (var (state, _) in BobberStates) d[$"Bobber_{state}"] = BobberKit;
            foreach (var tier in RodTiers)
                foreach (var (state, _) in RodStates) d[$"Rod_{tier}_{state}"] = RodKit;
            return d;   // 4×8 + 4 + 3×9 = 63 stems
        }

        private static Dictionary<string, int> BuildExpectedFrames()
        {
            var d = new Dictionary<string, int>();
            foreach (var sp in FishSpecies)
                foreach (var (state, frames) in FishStates) d[$"Fish_{sp}_{state}"] = frames;
            foreach (var (state, frames) in BobberStates) d[$"Bobber_{state}"] = frames;
            foreach (var tier in RodTiers)
                foreach (var (state, frames) in RodStates) d[$"Rod_{tier}_{state}"] = frames;
            return d;
        }

        /// <summary>
        /// Guarded stems whose sheets are SPECIFIED but not yet on disk (the owner-bake guard
        /// pattern of PR #252/#260). The whole kit was owner-baked and committed 2026-07-23, so the
        /// set is now EMPTY per its own rule — every stem is held to every assertion, and a missing
        /// sheet FAILS the closed-set guard rather than quietly reading as "pending". Add a stem
        /// here ONLY when a future kit extension specs a sheet ahead of its bake.
        /// </summary>
        private static readonly HashSet<string> AwaitingOwnerBake = new HashSet<string>(
            RodTiers.SelectMany(t => new[] { "stowV", "stowH" }.Select(r => $"Rod_{t}_{r}")));

        /// <summary>
        /// Stems that ARE on disk but whose spec above has moved past them — the other half of the
        /// owner-bake guard, and a distinction worth keeping: an ABSENT sheet tightens by itself the
        /// moment it lands, whereas a STALE one is present, loads, slices, and is simply the wrong
        /// art. Only re-baking clears it, so it is excluded outright rather than tested.
        ///
        /// <para>Here: the three tiers' <c>ground</c> sheets, baked as one-frame props before the rod
        /// rig made the rests animated hand-overs. <b>The PR that commits the re-bake must empty this
        /// set</b> — as it must <see cref="RetiredUntilRebake"/> below, which covers the sheets that
        /// lost their state entirely rather than merely changing shape.</para>
        /// </summary>
        private static readonly HashSet<string> StaleUntilRebake = new HashSet<string>(
            RodTiers.Select(t => $"Rod_{t}_ground"));

        /// <summary>
        /// Sheets the spec no longer names at all, still on disk until the re-bake deletes them.
        /// <c>stored</c> became <c>stowV</c> when the rests stopped being one-frame props, so the
        /// three <c>Rod_*_stored.png</c> are now orphans — no rod state asks for them. They are
        /// tolerated ON DISK (deleting shipped art before its replacement is baked would leave the
        /// repo poorer, not cleaner) but they are not in <see cref="Sheets"/> and nothing asserts
        /// against them. <b>The PR that commits the re-bake deletes them and empties this set.</b>
        /// </summary>
        private static readonly HashSet<string> RetiredUntilRebake = new HashSet<string>(
            RodTiers.Select(t => $"Rod_{t}_stored"));

        private static bool OnDisk(string stem) => File.Exists(Iso + stem + ".png");

        /// <summary>
        /// The ONE rule for which guarded stems the per-sheet assertions apply to: everything except
        /// a sheet that is stale (present but superseded — only a re-bake fixes it) or specified but
        /// not yet baked. Spelled once, because it used to be written out three times in three
        /// slightly different ways, and a guard added to one of them reached that one test only.
        /// </summary>
        private static bool IsAsserted(string stem)
            => !StaleUntilRebake.Contains(stem)
            && (!AwaitingOwnerBake.Contains(stem) || OnDisk(stem));

        /// <summary>Sentinel yielded when EVERY stem is still awaiting the owner's bake: NUnit fails a
        /// [TestCaseSource] outright when its source is empty ("No arguments were provided"), so the
        /// pre-bake state must yield SOMETHING — each per-sheet test Assert.Ignores it (reads as
        /// Skipped, honestly, instead of a fake red or a fake green).</summary>
        private const string NothingBakedYet = "@awaiting-owner-bake";

        private static IEnumerable<string> AllSheets()
        {
            string[] present = Sheets.Keys.Where(IsAsserted).OrderBy(s => s).ToArray();
            return present.Length > 0 ? present : new[] { NothingBakedYet };
        }

        /// <summary>The per-sheet tests' first line: skip the pre-bake sentinel.</summary>
        private static void SkipIfNothingBaked(string stem)
        {
            if (stem == NothingBakedYet)
                Assert.Ignore("Every fishing-kit sheet is awaiting the owner's bake (Hidden Harbours ▸ " +
                              "Art ▸ Bake Fishing Kit) — nothing to assert yet.");
        }

        private static Kit KitOf(string stem) => Sheets[stem];

        /// <summary>⚠️ Multiple-mode sheets return null from LoadAssetAtPath&lt;Sprite&gt; — LoadAllAssets is the rule.</summary>
        private static Sprite[] LoadSlices(string stem) =>
            AssetDatabase.LoadAllAssetsAtPath(Iso + stem + ".png").OfType<Sprite>().ToArray();

        private static Texture2D LoadSheet(string stem)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Iso + stem + ".png");
            Assert.IsNotNull(tex, $"{stem}.png: failed to load as Texture2D — is the PNG (and its .meta) committed?");
            return tex;
        }

        // ---- the assertions -------------------------------------------------------------------

        [Test]
        public void TheGuardedSet_IsTheFullKit()
        {
            // The set arithmetic itself, so a future edit that drops a species or tier by accident
            // is loud: 4 species × 8 states + 4 bobber states + 3 tiers × 10 states.
            Assert.AreEqual(4 * 8 + 4 + 3 * 10, Sheets.Count);
            Assert.AreEqual(Sheets.Count, ExpectedFrames.Count);

            // Every guarded stem must be a stem the spec actually names — a guard on a typo would
            // silently exempt nothing and hide the sheet it was meant to cover.
            foreach (string stem in AwaitingOwnerBake.Concat(StaleUntilRebake))
                Assert.IsTrue(Sheets.ContainsKey(stem), $"guarded stem '{stem}' is not in the kit");
            Assert.AreEqual(RodTiers.Length * RodRestStates.Length,
                            AwaitingOwnerBake.Count + StaleUntilRebake.Count,
                            "every rod REST is pending the owner's re-bake — when that lands, both " +
                            "guard sets empty and this assertion is what says the job is finished.");

            // A retired stem is the opposite: it must NOT be in the kit, or it is not retired.
            foreach (string stem in RetiredUntilRebake)
                Assert.IsFalse(Sheets.ContainsKey(stem),
                               $"'{stem}' is listed as retired but the spec still names it");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_IsSlicedMultipleMode_IntoItsKitsRowsOfTheArtsOwnFrameCount(string stem)
        {
            SkipIfNothingBaked(stem);
            var importer = AssetImporter.GetAtPath(Iso + stem + ".png") as TextureImporter;
            Assert.IsNotNull(importer, $"{stem}: no TextureImporter — is the .meta committed?");
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode,
                            $"{stem}: must stay grid-sliced (Multiple), not a Single sprite");

            var tex = LoadSheet(stem);
            Kit kit = KitOf(stem);

            Assert.AreEqual(0, tex.width % kit.Cell.x,
                            $"{stem}: {tex.width} px wide is not a whole number of {kit.Cell.x} px cells");
            Assert.AreEqual(kit.Rows * kit.Cell.y, tex.height,
                            $"{stem}: this kit's sheets are {kit.Rows} row(s) × {kit.Cell.y} px tall");

            int cols = tex.width / kit.Cell.x;
            Assert.AreEqual(kit.Rows * cols, LoadSlices(stem).Length,
                            $"{stem}: expected {kit.Rows} row(s) × {cols} frames = {kit.Rows * cols} slices");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_ImportsAtNativeRes_NotDownscaled(string stem)
        {
            SkipIfNothingBaked(stem);
            // The widest sheet here is 1344 px (Rod_*_reel) — under the 2048 default cap — so this
            // should never bite. Assert it anyway: a downscaled sheet cannot carry a source-pixel
            // grid while the sprite COUNT still matches, so only this and the pivot tests would
            // ever catch it.
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
            // ⚠️ Pixels, not normalized. A flipped pivot reads as a plausible number and silently
            // floats the fish 12 px off its waterline / hangs the rod 32 px off its grip.
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
        public void EverySlice_NormalizedPivot_IsTheKitsRule(string stem)
        {
            SkipIfNothingBaked(stem);
            // The same rule in NORMALIZED terms — the number actually stored in the .meta:
            // fish (0.5, 26/64), bobber (0.5, 10/22), rod (0.5, 40/112). Three numbers, one
            // inversion rule — this catches a "generalisation" that quietly reused one kit's
            // fraction on another kit's cell.
            Kit kit = KitOf(stem);
            float expectedX = kit.PivotPx.x / kit.Cell.x;
            float expectedY = kit.PivotPx.y / kit.Cell.y;

            foreach (var s in LoadSlices(stem))
            {
                Assert.AreEqual(expectedX, s.pivot.x / s.rect.width, 0.0005f,
                                $"{s.name}: normalized pivot.x must be {expectedX}");
                Assert.AreEqual(expectedY, s.pivot.y / s.rect.height, 0.0005f,
                                $"{s.name}: normalized pivot.y must be {kit.PivotPx.y}/{kit.Cell.y} = {expectedY}");
            }
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Slices_TileTheSheet_WithNoGapsAndNoOverlap(string stem)
        {
            SkipIfNothingBaked(stem);
            var tex = LoadSheet(stem);
            Kit kit = KitOf(stem);
            int cols = tex.width / kit.Cell.x;

            var occupied = new HashSet<(int, int)>();
            foreach (var s in LoadSlices(stem))
            {
                Assert.AreEqual(0, Mathf.RoundToInt(s.rect.x) % kit.Cell.x, $"{s.name}: x not on the cell grid");
                Assert.AreEqual(0, Mathf.RoundToInt(s.rect.y) % kit.Cell.y, $"{s.name}: y not on the cell grid");
                var c = (Mathf.RoundToInt(s.rect.x) / kit.Cell.x, Mathf.RoundToInt(s.rect.y) / kit.Cell.y);
                Assert.IsTrue(occupied.Add(c), $"{s.name}: two slices overlap cell {c}");
            }

            for (int c = 0; c < cols; c++)
                for (int r = 0; r < kit.Rows; r++)
                    Assert.IsTrue(occupied.Contains((c, r)), $"{stem}: no slice covers cell (col {c}, row {r})");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Slices_AreNamedByRowIndex_NotByCompassName(string stem)
        {
            SkipIfNothingBaked(stem);
            var tex = LoadSheet(stem);
            Kit kit = KitOf(stem);
            int cols = tex.width / kit.Cell.x;

            var seen = new HashSet<string>();
            foreach (var s in LoadSlices(stem))
            {
                StringAssert.StartsWith(stem + "_d", s.name, $"{s.name}: unexpected slice name");
                Assert.IsTrue(seen.Add(s.name), $"{s.name}: duplicate slice name");

                string tail = s.name.Substring(stem.Length + 2);          // "<row>_f<col>"
                string[] parts = tail.Split(new[] { "_f" }, System.StringSplitOptions.None);
                Assert.AreEqual(2, parts.Length, $"{s.name}: must be <stem>_d<row>_f<col>");
                Assert.IsTrue(int.TryParse(parts[0], out int d), $"{s.name}: unparseable row index");
                Assert.IsTrue(int.TryParse(parts[1], out int f), $"{s.name}: unparseable frame index");
                Assert.Less(d, kit.Rows, $"{s.name}: row index out of range");
                Assert.Less(f, cols, $"{s.name}: frame index out of range");

                // Row 0 is the TOP row of the canvas; Unity rects are bottom-origin.
                int rectRowFromTop = kit.Rows - 1 - Mathf.RoundToInt(s.rect.y) / kit.Cell.y;
                Assert.AreEqual(d, rectRowFromTop,
                                $"{s.name}: name says row {d} but the rect sits at row {rectRowFromTop} from the top");
                Assert.AreEqual(f, Mathf.RoundToInt(s.rect.x) / kit.Cell.x,
                                $"{s.name}: name says frame {f} but the rect sits in a different column");
            }
        }

        [Test]
        public void FrameCounts_MatchTheKitsStatedTables()
        {
            // Checked against the PNGs, so a re-bake that quietly changed a state's length is
            // caught rather than absorbed. (The same numbers are pinned against the LIVE RIGS in
            // FishingKitBakeTests, so rig and art cannot drift apart unnoticed either way.)
            foreach (var kv in ExpectedFrames.Where(kv => IsAsserted(kv.Key)))
            {
                var tex = LoadSheet(kv.Key);
                int cols = tex.width / KitOf(kv.Key).Cell.x;
                Assert.AreEqual(kv.Value, cols,
                                $"{kv.Key}: expected {kv.Value} frames but the sheet is {tex.width} px " +
                                $"wide = {cols} cells of {KitOf(kv.Key).Cell.x} px");
            }
        }

        [Test]
        public void EveryFishingKitPngInTheFolder_IsCoveredByThisTest()
        {
            // A new sheet dropped into the folder must not slip past the guard unnoticed. The
            // expected set is every guarded stem except those still awaiting the owner's bake — so
            // an UNGUARDED png on disk still fails (it is not in Sheets at all), and a guarded,
            // already-shipped sheet going missing still fails; only a spec'd-but-not-yet-baked
            // sheet is tolerated as absent. (No folder at all = the owner has not baked yet.)
            var onDisk = Directory.Exists(Iso)
                ? Directory.GetFiles(Iso, "*.png")
                           .Select(Path.GetFileNameWithoutExtension)
                           .OrderBy(s => s)
                           .ToArray()
                : new string[0];
            // NOTE: compare against the raw filtered stems, NOT AllSheets() — that helper yields the
            // @awaiting-owner-bake sentinel (a TestCaseSource can't be empty), which is not a file.
            // This test is about what EXISTS, so it does not use IsAsserted: a STALE sheet is still
            // on disk and must still be counted, and a RETIRED one is on disk without being in the
            // spec at all. Both sets empty when the re-bake lands, and this reverts to "the spec".
            string[] expected = Sheets.Keys.Where(s => !AwaitingOwnerBake.Contains(s) || OnDisk(s))
                                     .Concat(RetiredUntilRebake.Where(OnDisk))
                                     .OrderBy(s => s).ToArray();
            CollectionAssert.AreEquivalent(expected, onDisk,
                                           "Fishing kit sheets on disk differ from the guarded set");
        }
    }
}
