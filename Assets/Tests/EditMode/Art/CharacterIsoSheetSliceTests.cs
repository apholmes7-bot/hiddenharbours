using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the baked slice of the 8-direction ISO CHARACTER sheets — the player's twenty-nine states
    /// at the folder root, and the nine cast presets one subfolder each. The slice lives in the
    /// <c>.meta</c>, not in code, so nothing at runtime would notice it rotting: a re-export that
    /// drifts the grid, a re-slice that loses the ground pivot, or an importer setting that downscales
    /// the sheet all land as silently wrong sprites.
    ///
    /// <para><b>ONE CELL SIZE: 64 × 92, every sheet — pass 6, 2026-08-02. It was 64 × 88.</b> There
    /// used to be two sizes: the rod poses were <b>128 × 128</b>, the same figure on a canvas padded
    /// for the rod arc and the flying lure. The art director split the rod out into its own overlay
    /// sheet, so the bodies are uniformly the plain character cell. <b>The pivot rule is unchanged in
    /// form and moved in value: <c>GroundInset</c> px above the cell bottom, on the centreline</b> →
    /// <c>(0.5, 10/92 ≈ 0.1087)</c>, where it used to be <c>8/88 ≈ 0.0909</c>. The inset is
    /// <c>H − pivotY</c> read off the rig and it moved with the cell; leaving it at 8 plants every
    /// character two pixels into the ground while every other assert here still passes. Inverting it
    /// buries them ~72 px. Both numbers are restated as literals here rather than imported from
    /// <c>CharacterSheetSlicer</c> — the duplication is the test.</para>
    ///
    /// <para><b>Expectations come from the ART, not from the slicer.</b> Row counts and total sprite
    /// counts are derived from the actual PNG dimensions read off disk (<c>cols = width / cellW</c>,
    /// <c>rows = height / cellH</c>) — asserting the slicer's grid config against the slicer's grid
    /// config is the self-referential blind spot that let the mirrored boat art ship, and it is
    /// deliberately avoided. This test never references <c>CharacterSheetSlicer</c>. The two things
    /// that cannot be derived — the cell size (a sheet width is a whole number of several plausible
    /// cell widths) and the per-anim frame counts — are restated here as the contract under test.</para>
    ///
    /// <para>Row order is asserted only as a <i>count</i> of 8 — which way the rows RUN is measured
    /// from the pixels in <c>CharacterIsoFacingTests</c>, not here. Slices stay named by row INDEX
    /// regardless: a slice name states geometry, not compass semantics, which is what kept the re-bake
    /// a data change instead of an asset-database migration.</para>
    /// </summary>
    public class CharacterIsoSheetSliceTests
    {
        private const string Iso = "Assets/_Project/Art/Characters/Iso/";

        /// <summary>Ground contact sits this many px above the cell bottom on EVERY sheet.
        /// 92 − 82 = 10, from the rig's own pivot. Was 8 when the cell was 88 tall.</summary>
        private const float GroundInsetPx = 10f;

        private const int CellW = 64, CellH = 92, Rows = 8;

        /// <summary>Every sheet's height: 8 direction rows × 92.</summary>
        private const int SheetHeight = Rows * CellH;   // 736

        /// <summary>
        /// The frame count of every state, restated as the contract. Cross-checked against the RIG's
        /// own ANIMS table by <c>CharacterRigBakeTests</c> and against the PNG widths here, so a
        /// re-export that quietly lengthened an animation is caught from both sides. A carry variant
        /// carries its base anim's count — the stance changes the pose, never the timing.
        /// </summary>
        private static readonly Dictionary<string, int> Frames = new Dictionary<string, int>
        {
            { "idle", 6 },   { "walk", 8 },        { "run", 6 },
            { "balance", 8 },{ "stagger", 10 },
            { "hold", 6 },   { "cast_short", 10 }, { "cast_long", 10 },
            { "castBack", 6 }, { "castRelease", 8 },
            { "bite", 6 },   { "strike", 6 },      { "reel", 12 }, { "land", 12 },
            { "dig", 10 },

            // The pass-6.2 clip families: the boarding vault up and back down, the deck haul, and the
            // ladder descent. Boarding is TWO authored clips, not one mirrored — going down the hand
            // only steadies and the weight drops ahead of the foot — so boardDown carries its own
            // count. Cross-checked against the rig's own ANIMS table by CharacterRigBakeTests.
            { "board", 10 },   { "boardDown", 6 },
            { "haul", 8 },     { "ladderDown", 10 },

            // The pass-6.3 deck-work family (#473/#474): the hauler drum, the bench, bait cutting,
            // and the lift/place/toss one-shots — plus the pot carry on idle and walk (a pot is
            // carried standing or walking, never at a run). Counts are the rig's own ANIMS values,
            // cross-checked by CharacterRigBakeTests like every family before it.
            { "hauler", 8 },  { "bench", 10 }, { "chop", 8 },
            { "lift", 8 },    { "place", 8 },  { "toss", 8 },
            { "idle_pot", 6 }, { "walk_pot", 8 },

            // The carry stances — separate sheets because the stance changes the POSE, not just
            // where the hands are. Which stance rides which anim is the RIG's CARRIES table: pails
            // and tray on all three gaits, helm and oars on idle and walk only (nobody runs a
            // tiller). Restated here as the contract; the bake grows the set from the rig.
            { "idle_buckets", 6 }, { "walk_buckets", 8 }, { "run_buckets", 6 },
            { "idle_tray", 6 },    { "walk_tray", 8 },    { "run_tray", 6 },
            { "idle_helm", 6 },    { "walk_helm", 8 },
            { "idle_oars", 6 },    { "walk_oars", 8 },
        };

        /// <summary>The player's thirty-seven: every state the rig declares, plus its carry stances and
        /// the pass-6.2 and 6.3 clip families.</summary>
        private static readonly string[] PlayerStates =
        {
            "idle", "walk", "run", "balance", "stagger",
            "idle_buckets", "walk_buckets", "run_buckets",
            "idle_tray", "walk_tray", "run_tray",
            "idle_helm", "walk_helm", "idle_oars", "walk_oars",
            "hold", "cast_short", "cast_long", "castBack", "castRelease",
            "bite", "strike", "reel", "land", "dig",
            "board", "boardDown", "haul", "ladderDown",
            "hauler", "bench", "chop", "lift", "place", "toss",
            "idle_pot", "walk_pot",
        };

        /// <summary>What a cast standee gets: the gaits, not the gear. (See the bake menu for why.)</summary>
        private static readonly string[] CastStates = { "idle", "walk" };

        /// <summary>The nine NPC presets: subfolder, then sheet stem. <c>fisher</c> is absent because
        /// he is the player, baked at the root.</summary>
        private static readonly (string folder, string stem)[] Cast =
        {
            ("ginny", "Ginny"), ("skipper", "Skipper"), ("nan", "Nan"),
            ("deckboss", "DeckBoss"), ("packer", "Packer"), ("cutter", "Cutter"),
            ("hand", "Hand"), ("boy", "Boy"), ("girl", "Girl"),
        };

        /// <summary>Project-relative path of every guarded sheet — the player's at the root, the
        /// cast's one folder each.</summary>
        private static IEnumerable<string> AllSheets()
        {
            var paths = new List<string>();
            foreach (string s in PlayerStates) paths.Add($"{Iso}Fisher_{s}.png");
            foreach (var (folder, stem) in Cast)
            foreach (string s in CastStates) paths.Add($"{Iso}{folder}/{stem}_{s}.png");
            paths.Sort(System.StringComparer.Ordinal);
            return paths.ToArray();
        }

        private static string StemOf(string path) => Path.GetFileNameWithoutExtension(path);

        /// <summary>The state name behind a sheet path: <c>Ginny_walk</c> → <c>walk</c>.</summary>
        private static string StateOf(string path)
        {
            string stem = StemOf(path);
            int i = stem.IndexOf('_');
            return i < 0 ? stem : stem.Substring(i + 1);
        }

        /// <summary>⚠️ Multiple-mode sheets return null from LoadAssetAtPath&lt;Sprite&gt; — LoadAllAssets is the rule.</summary>
        private static Sprite[] LoadSlices(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();

        private static Texture2D LoadSheet(string path)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(tex, $"{path}: failed to load as Texture2D — is the PNG (and its .meta) committed?");
            return tex;
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_IsSlicedMultipleMode_IntoEightDirectionRowsOfTheArtsOwnFrameCount(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer, $"{path}: no TextureImporter — is the .meta committed?");
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode,
                            $"{path}: must stay grid-sliced (Multiple), not a Single sprite");

            var tex = LoadSheet(path);

            // Derived from the art, not asserted against a constant.
            Assert.AreEqual(0, tex.width % CellW,
                            $"{path}: {tex.width} px wide is not a whole number of {CellW} px cells");
            Assert.AreEqual(0, tex.height % CellH,
                            $"{path}: {tex.height} px tall is not a whole number of {CellH} px cells");

            int cols = tex.width / CellW;
            int rows = tex.height / CellH;

            Assert.AreEqual(Rows, rows, $"{path}: an iso character sheet must have 8 direction rows");
            Assert.AreEqual(rows * cols, LoadSlices(path).Length,
                            $"{path}: expected {rows} direction rows × {cols} frames = {rows * cols} slices");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_ImportsAtNativeRes_NotDownscaled(string path)
        {
            // The widest sheet here is 768 px — comfortably under the 2048 default cap — so this should
            // not bite. Assert it anyway: a downscaled sheet cannot carry a source-pixel grid (rects get
            // refit and the pivot is thrown away) while the sprite COUNT still matches, so only this and
            // the pivot test would ever catch it.
            var tex = LoadSheet(path);
            var slices = LoadSlices(path);
            Assert.IsNotEmpty(slices, $"{path}: no slices loaded");

            Assert.AreEqual(tex.width, slices.Max(s => s.rect.xMax), 0.01f,
                            $"{path}: slices do not span the sheet width — importer downscaled or grid drifted");
            Assert.AreEqual(tex.height, slices.Max(s => s.rect.yMax), 0.01f,
                            $"{path}: slices do not span the sheet height — importer downscaled or grid drifted");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void EverySlice_IsOneCell_AndPivotsOnGroundContact(string path)
        {
            // ⚠️ Pixels, not normalized — one rule for any cell size: centreline, GroundInsetPx above
            // the cell bottom. A flipped pivot (10/92 → 82/92) reads as a plausible number but buries
            // the character 72 px in the ground on every frame.
            float pivotPxX = CellW / 2f;
            float pivotPxY = GroundInsetPx;

            var slices = LoadSlices(path);
            Assert.IsNotEmpty(slices, $"{path}: no slices loaded");
            foreach (var s in slices)
            {
                Assert.AreEqual(CellW, s.rect.width, 0.01f, $"{s.name}: cell width drifted");
                Assert.AreEqual(CellH, s.rect.height, 0.01f, $"{s.name}: cell height drifted");
                Assert.AreEqual(pivotPxX, s.pivot.x, 0.01f, $"{s.name}: pivot.x off the character centreline");
                Assert.AreEqual(pivotPxY, s.pivot.y, 0.01f,
                                $"{s.name}: pivot.y off ground contact — is it inverted, or still on the " +
                                $"old 8 px inset? ground contact is ({pivotPxX}, {CellH - GroundInsetPx}) " +
                                $"TOP-LEFT; Unity wants bottom-origin {pivotPxY}");
            }
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void EverySlice_NormalizedPivot_IsGroundInsetOverCellHeight(string path)
        {
            // The same rule again in NORMALIZED terms, because that is the number actually stored in the
            // .meta and the number a presenter reasons about: (0.5, 10/92 ≈ 0.1087). This is the assert
            // that goes red if the cell height moves and the inset does not follow it.
            float expectedY = GroundInsetPx / CellH;

            foreach (var s in LoadSlices(path))
            {
                Vector2 norm = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
                Assert.AreEqual(0.5f, norm.x, 0.0005f, $"{s.name}: normalized pivot.x must be 0.5");
                Assert.AreEqual(expectedY, norm.y, 0.0005f,
                                $"{s.name}: normalized pivot.y must be {GroundInsetPx}/{CellH} = {expectedY}");
            }
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Slices_TileTheSheet_WithNoGapsAndNoOverlap(string path)
        {
            // Every (col,row) origin the sheet's own dimensions imply must be covered exactly once.
            var tex = LoadSheet(path);
            int cols = tex.width / CellW;
            int rows = tex.height / CellH;

            var occupied = new HashSet<(int, int)>();
            foreach (var s in LoadSlices(path))
            {
                Assert.AreEqual(0, Mathf.RoundToInt(s.rect.x) % CellW, $"{s.name}: x not on the cell grid");
                Assert.AreEqual(0, Mathf.RoundToInt(s.rect.y) % CellH, $"{s.name}: y not on the cell grid");
                var c = (Mathf.RoundToInt(s.rect.x) / CellW, Mathf.RoundToInt(s.rect.y) / CellH);
                Assert.IsTrue(occupied.Add(c), $"{s.name}: two slices overlap cell {c}");
            }

            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                    Assert.IsTrue(occupied.Contains((c, r)), $"{path}: no slice covers cell (col {c}, row {r})");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Slices_AreNamedByRowIndex_NotByCompassName(string path)
        {
            // The `_d<row>_f<col>` scheme IS the contract, and the ABSENCE of a compass name is
            // deliberate: a compass name in a sprite name hard-codes a facing claim into the asset
            // database, where a re-measure cannot reach it. CharacterVisualDef carries the
            // FacingsAreCounterClockwise flag instead, as per-artwork data.
            string stem = StemOf(path);
            var tex = LoadSheet(path);
            int cols = tex.width / CellW;
            int rows = tex.height / CellH;

            var seen = new HashSet<string>();
            foreach (var s in LoadSlices(path))
            {
                StringAssert.StartsWith(stem + "_d", s.name, $"{s.name}: unexpected slice name");
                Assert.IsTrue(seen.Add(s.name), $"{s.name}: duplicate slice name");

                // Row index must map to the rect's own row — the name and the geometry must agree.
                string tail = s.name.Substring(stem.Length + 2);          // "<row>_f<col>"
                string[] parts = tail.Split(new[] { "_f" }, System.StringSplitOptions.None);
                Assert.AreEqual(2, parts.Length, $"{s.name}: must be <stem>_d<row>_f<col>");
                Assert.IsTrue(int.TryParse(parts[0], out int d), $"{s.name}: unparseable row index");
                Assert.IsTrue(int.TryParse(parts[1], out int f), $"{s.name}: unparseable frame index");
                Assert.Less(d, rows, $"{s.name}: row index out of range");
                Assert.Less(f, cols, $"{s.name}: frame index out of range");

                // Row 0 is the TOP row of the canvas; Unity rects are bottom-origin.
                int rectRowFromTop = rows - 1 - Mathf.RoundToInt(s.rect.y) / CellH;
                Assert.AreEqual(d, rectRowFromTop,
                                $"{s.name}: name says row {d} but the rect sits at row {rectRowFromTop} from the top");
                Assert.AreEqual(f, Mathf.RoundToInt(s.rect.x) / CellW,
                                $"{s.name}: name says frame {f} but the rect sits in a different column");
            }
        }

        [Test]
        public void FrameCounts_MatchTheRecipe_OnEverySheetOnDisk()
        {
            // The one place the stated frame counts are checked against the PNGs, so a re-export that
            // quietly changed an animation's length is caught rather than absorbed.
            foreach (string path in AllSheets())
            {
                string state = StateOf(path);
                Assert.IsTrue(Frames.TryGetValue(state, out int expected),
                              $"{path}: no frame count declared for state '{state}'");
                var tex = LoadSheet(path);
                int cols = tex.width / CellW;
                Assert.AreEqual(expected, cols,
                                $"{path}: expected {expected} frames but the sheet is {tex.width} px " +
                                $"wide = {cols} cells of {CellW} px");
            }
        }

        [Test]
        public void EveryBodySheet_IsThePassSixCell_736PxTall()
        {
            // The shape half of the port, asserted against the pixels. A pass-1 sheet is 704 px tall
            // (8 × 88) and a pre-rod-split one is 1024 (8 × 128); both fail here, so a half-applied
            // re-bake cannot hide. Reading a 736 px sheet on the 88 px grid gives 8.36 rows and fails
            // the row assert too — this catches the drift from either direction.
            foreach (string path in AllSheets())
            {
                var tex = LoadSheet(path);
                Assert.AreEqual(SheetHeight, tex.height,
                                $"{path}: every iso character BODY sheet must be 8 rows × {CellH} px = " +
                                $"{SheetHeight} px tall. 704 means the pass-1 art is still on disk; " +
                                "1024 means the rod-baked-in art is.");
                Assert.AreEqual(0, tex.width % CellW,
                                $"{path}: {tex.width} px wide is not a whole number of {CellW} px cells");
            }
        }

        [Test]
        public void EveryIsoCharacterPngInTheFolder_IsCoveredByThisTest()
        {
            // A new sheet dropped into the folder must not slip past the guard unnoticed — and a
            // guarded sheet going missing must fail. The scan is RECURSIVE because the cast lives one
            // subfolder deep; a stale pass-1 Ginny_idle.png left at the root would surface here.
            var onDisk = Directory.GetFiles(Iso, "*.png", SearchOption.AllDirectories)
                                  .Select(p => p.Replace('\\', '/'))
                                  .OrderBy(p => p, System.StringComparer.Ordinal)
                                  .ToArray();
            CollectionAssert.AreEquivalent(AllSheets().ToArray(), onDisk,
                                           "Iso character sheets on disk differ from the guarded set");
        }
    }
}
