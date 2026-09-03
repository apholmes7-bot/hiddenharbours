using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the baked slice of the Cape Islander iso kit — the ~12.9 m inshore working boat — as it has
    /// shipped since the full-mesh rollout's PR 2b: a <b>RigBaker output</b>, the lobster's recipe exactly
    /// (32 facings on an 8×4 base page, 4 rock frames across two 8×8 pages, the rig's declared pivot).
    /// Mirrors <see cref="Tests.RigBaking.LobsterBoatSheetSliceTests"/> for the biggest hull in the project.
    ///
    /// <para><b>THE DOWNSCALE TRAP IS REAL ON THIS KIT, not theoretical.</b> Each rock page is 3648×3360 —
    /// BOTH dimensions past Unity's default 2048 <c>maxTextureSize</c> — so without the cap lift it imports
    /// at 0.56× and every source-pixel rect is refit, alpha-trimmed, and stripped of its pivot. The failure is
    /// silent because <b>the sprite COUNT still comes out right</b>: 32 and 64, exactly as expected, on a
    /// sheet that is now mush. Only <see cref="Sheet_ImportsAtNativeRes_NotDownscaled"/> and the
    /// cell-size/pivot assertions in <see cref="EverySlice_SharesTheBoatOriginPivot"/> can tell the
    /// difference, which is why the cell rect is asserted explicitly here and not merely implied by the
    /// count.</para>
    ///
    /// <para><b>The pivot is READ FROM HER RIG now, and this fixture proves it.</b> Her first sheet (#224)
    /// arrived as two loose PNGs with neither README nor rig, so its pivot was RECOVERED FROM PIXELS at
    /// (228, 263) ±4 px. The re-bake replaced that with the rig's declared (228, 258), which RigBaker
    /// records in <c>CapeIslanderIsoAnchors.json</c>. <see cref="HerPivot_IsTheRigsDeclaredOne_NotTheRecoveredOne"/>
    /// reads the anchors file and pins the slices to it — and pins them AWAY from the old recovered number,
    /// so a tidy-up that "restores" 263 from a stale comment fails loudly.</para>
    ///
    /// <para><b>She is 32 facings, and her facings are genuinely CLOCKWISE.</b> Every hand-exported kit
    /// carries <c>FacingsAreCounterClockwise = true</c> to correct its mirror at runtime; hers (like the
    /// lobster's) was corrected at BAKE time. The PIXEL proof of that is <c>CapeIslanderFacingTests</c>;
    /// this file guards the geometry only.</para>
    /// </summary>
    public class CapeIslanderSheetSliceTests
    {
        private const string Boats = "Assets/_Project/Art/Boats/";
        private const string AnchorsJson = Boats + "CapeIslanderIsoAnchors.json";

        // The baked kit geometry, as expectations: (file, cols, rows, cellW, cellH). 8 cols × N rows,
        // row-major from the top-left; flat index = heading×rockFrames + frame.
        private static readonly (string File, int Cols, int Rows, int CellW, int CellH)[] Sheets =
        {
            ("CapeIslanderIso.png",      8, 4, 456, 420),   // 32 facings, no rock
            ("CapeIslanderIsoRock0.png", 8, 8, 456, 420),   // headings  0–15 × 4 rock frames
            ("CapeIslanderIsoRock1.png", 8, 8, 456, 420),   // headings 16–31 × 4 rock frames
        };

        /// <summary>Her boat origin (amidships, keel bottom, centreline), normalized. The rig declares
        /// (228, 258) from the cell's TOP-LEFT; flipped to Unity's bottom-left origin, y = 420 − 258 = 162.</summary>
        private const float OriginX = 228f / 456f;
        private const float OriginY = 162f / 420f;

        /// <summary>The number the OLD hand export carried — recovered from pixels, ≈±4 px honest
        /// uncertainty. Kept only so a regression back to it is named, not merely "off by 5 px".</summary>
        private const float RecoveredOriginY_Legacy = 157f / 420f;

        private static IEnumerable<(string File, int Cols, int Rows, int CellW, int CellH)> AllSheets() => Sheets;

        /// <summary>Multiple-mode sheets return null from LoadAssetAtPath&lt;Sprite&gt; — LoadAllAssets is the rule.</summary>
        private static Sprite[] LoadSlices(string file) =>
            AssetDatabase.LoadAllAssetsAtPath(Boats + file).OfType<Sprite>().ToArray();

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_IsSlicedMultipleMode_WithTheExpectedCellCount(
            (string File, int Cols, int Rows, int CellW, int CellH) s)
        {
            string path = Boats + s.File;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer, $"{s.File}: no TextureImporter — is the .meta committed?");
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode,
                            $"{s.File}: must stay grid-sliced (Multiple), not a Single sprite");

            var slices = LoadSlices(s.File);
            Assert.AreEqual(s.Cols * s.Rows, slices.Length,
                            $"{s.File}: expected {s.Cols}×{s.Rows} cells");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_LiftsTheSizeCap_SoItCanImportAtNativeRes(
            (string File, int Cols, int Rows, int CellW, int CellH) s)
        {
            // The cap itself, asserted on the importer rather than inferred from the result — so the
            // reason a future regression happens is legible, not just the symptom. 3648 needs 4096; the
            // 2048 default would silently halve the sheet. SpriteSheetSlicer lifts this automatically,
            // and this is the assertion that says it must stay lifted.
            var importer = AssetImporter.GetAtPath(Boats + s.File) as TextureImporter;
            Assert.IsNotNull(importer, $"{s.File}: no TextureImporter");
            int needed = Mathf.NextPowerOfTwo(Mathf.Max(s.Cols * s.CellW, s.Rows * s.CellH));
            Assert.GreaterOrEqual(importer.maxTextureSize, needed,
                $"{s.File}: the sheet is {s.Cols * s.CellW}×{s.Rows * s.CellH} but maxTextureSize is " +
                $"{importer.maxTextureSize} — it needs at least {needed}. Below that Unity DOWNSCALES the " +
                "texture on import and the grid slice becomes garbage, while the sprite COUNT still looks " +
                "right. Re-run Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Slice Environment + VFX Sheets.");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_ImportsAtNativeRes_NotDownscaled(
            (string File, int Cols, int Rows, int CellW, int CellH) s)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Boats + s.File);
            Assert.IsNotNull(tex, $"{s.File}: failed to load as Texture2D");
            Assert.AreEqual(s.Cols * s.CellW, tex.width,
                            $"{s.File}: width is not native — the importer downscaled the sheet");
            Assert.AreEqual(s.Rows * s.CellH, tex.height,
                            $"{s.File}: height is not native — the importer downscaled the sheet");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void EverySlice_SharesTheBoatOriginPivot(
            (string File, int Cols, int Rows, int CellW, int CellH) s)
        {
            // Pixels, not normalized: a heading- or frame-swap must never shift the boat. The CELL RECT is
            // asserted alongside the pivot on purpose — it is the assertion that survives a downscale even
            // if a future importer change happens to preserve the normalized pivot.
            float expX = OriginX * s.CellW;
            float expY = OriginY * s.CellH;
            foreach (var sprite in LoadSlices(s.File))
            {
                Assert.AreEqual(s.CellW, sprite.rect.width, 0.01f,
                    $"{sprite.name}: cell width is {sprite.rect.width}, not {s.CellW}. If it is ~0.56× that, " +
                    "the sheet imported DOWNSCALED past maxTextureSize — the count would still be right.");
                Assert.AreEqual(s.CellH, sprite.rect.height, 0.01f,
                    $"{sprite.name}: cell height is {sprite.rect.height}, not {s.CellH} — see above");
                Assert.AreEqual(expX, sprite.pivot.x, 0.01f, $"{sprite.name}: pivot.x off the boat origin");
                Assert.AreEqual(expY, sprite.pivot.y, 0.01f, $"{sprite.name}: pivot.y off the boat origin");
            }
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Slices_AreNamed_StemUnderscoreIndex_ContiguousFromZero(
            (string File, int Cols, int Rows, int CellW, int CellH) s)
        {
            // The `_N` suffix IS the index math contract (heading = index/rockFrames, frame = index%rockFrames)
            // and is what BoatVisualLibraryBuilder.SpriteIndex parses. A gap or a rename silently mis-maps headings.
            string stem = Path.GetFileNameWithoutExtension(s.File);
            var indices = new HashSet<int>();
            foreach (var sprite in LoadSlices(s.File))
            {
                StringAssert.StartsWith(stem + "_", sprite.name, $"{sprite.name}: unexpected slice name");
                Assert.IsTrue(int.TryParse(sprite.name.Substring(stem.Length + 1), out int i),
                              $"{sprite.name}: slice name must end in _<index>");
                Assert.IsTrue(indices.Add(i), $"{sprite.name}: duplicate index {i}");
            }
            for (int i = 0; i < s.Cols * s.Rows; i++)
                Assert.IsTrue(indices.Contains(i), $"{stem}: missing slice index {i}");
        }

        [Test]
        public void HullAndRockPages_ShareTheSameCellAndPivot_SoTheRockNeverShiftsTheBoat()
        {
            // The rock grid replaces the static facing frame-by-frame under the wave. If the pages disagreed
            // by even a pixel the whole boat would twitch every time the wave phase crossed a frame boundary
            // — a very visible bug for a very small cause. BOTH pages, because the second is the one a
            // page-split regression would drop.
            var hull = LoadSlices("CapeIslanderIso.png").First();
            foreach (string page in new[] { "CapeIslanderIsoRock0.png", "CapeIslanderIsoRock1.png" })
            {
                var rock = LoadSlices(page).First();
                Assert.AreEqual(hull.rect.size, rock.rect.size, $"{page}: hull and rock cells must match exactly");
                Assert.AreEqual(hull.pivot.x, rock.pivot.x, 1e-4f, $"{page}: hull/rock pivot.x diverged");
                Assert.AreEqual(hull.pivot.y, rock.pivot.y, 1e-4f, $"{page}: hull/rock pivot.y diverged");
            }
        }

        /// <summary>
        /// 32 facings is the owner's decision and the reason the baker exists. If a future change quietly
        /// re-bakes her at 8 — or restores the 8-cell hand export from history — a 13 m hull goes back to
        /// snapping between 45° steps, which is exactly the thing ADR 0021 set out to fix.
        /// </summary>
        [Test]
        public void SheDoesNotRegressBelow32Facings()
        {
            Assert.AreEqual(32, LoadSlices("CapeIslanderIso.png").Length,
                "The Cape Islander's base sheet must carry 32 facings.");
            Assert.AreEqual(128,
                LoadSlices("CapeIslanderIsoRock0.png").Length + LoadSlices("CapeIslanderIsoRock1.png").Length,
                "Her rock grid must carry 32 facings × 4 frames across two pages.");
        }

        /// <summary>
        /// The pivot comes off the RIG, through the baker's own record, and this fixture reads that record
        /// rather than restating the number: <c>CapeIslanderIsoAnchors.json</c> carries <c>pivotTopLeft</c>
        /// as RigBaker read it from <c>CapeIslanderIso.pivot</c>. The slices must agree with it — and must
        /// NOT agree with the old recovered (228, 263), which is 5 px away and was the one number in her
        /// kit that was ever measured by eye.
        /// </summary>
        [Test]
        public void HerPivot_IsTheRigsDeclaredOne_NotTheRecoveredOne()
        {
            Assert.IsTrue(File.Exists(AnchorsJson),
                $"{AnchorsJson} is missing — RigBaker writes it beside the sheet on every bake; commit it.");
            var m = Regex.Match(File.ReadAllText(AnchorsJson),
                                "\"pivotTopLeft\"\\s*:\\s*\\{\\s*\"x\"\\s*:\\s*([0-9.]+)\\s*,\\s*\"y\"\\s*:\\s*([0-9.]+)");
            Assert.IsTrue(m.Success, $"{AnchorsJson}: no pivotTopLeft record");
            float rigX = float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            float rigY = float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);

            var hull = LoadSlices("CapeIslanderIso.png").First();
            Assert.AreEqual(rigX, hull.pivot.x, 0.01f, "slice pivot.x is not the rig's declared pivot");
            Assert.AreEqual(hull.rect.height - rigY, hull.pivot.y, 0.01f,
                "slice pivot.y is not the rig's declared pivot (remember the top-left → bottom-left flip)");

            Assert.AreEqual(OriginY, hull.pivot.y / hull.rect.height, 1e-4f,
                "the fixture's own OriginY drifted off the rig — update the const from the anchors file, not by eye");
            Assert.That(hull.pivot.y / hull.rect.height, Is.Not.EqualTo(RecoveredOriginY_Legacy).Within(1e-3f),
                "the slice pivot is back on the OLD pixel-recovered origin (228, 263). That number was an " +
                "estimate with ±4 px of honest uncertainty; the rig declares 258. Do not restore it.");
        }

        [Test]
        public void HerCellIsTheBiggestInTheProject_AndThatIsAMemoryFact_NotAnAccident()
        {
            // Not a style assertion — a budget one (CLAUDE.md rule 7, "mind texture memory"). Each rock
            // page is 3648×3360 RGBA32 = ~46.8 MiB uncompressed, and she now ships TWO of them (the
            // lobster's budget exactly — the same cell, the same page split). If someone later re-exports
            // her at a larger cell this goes red and the cost gets re-argued rather than absorbed.
            foreach (string page in new[] { "CapeIslanderIsoRock0.png", "CapeIslanderIsoRock1.png" })
            {
                var rock = AssetDatabase.LoadAssetAtPath<Texture2D>(Boats + page);
                Assert.IsNotNull(rock, page);
                double mib = (double)rock.width * rock.height * 4 / (1024 * 1024);
                Assert.Less(mib, 50d,
                    $"{page} is {mib:0.0} MiB uncompressed RGBA. That is already the largest single texture " +
                    "class in the project; past 50 it needs an explicit decision (compression trades away the " +
                    "crisp pixel edges), not a silent import.");
            }
        }
    }
}
