using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the baked slice of the DRIFT WEED kit sheets under
    /// <c>Assets/_Project/Art/Sprites/Shore/Drift/</c> — the seaweed/flotsam surface-drift decor
    /// (owner drop 2026-07-23): four species, each variant COLUMNS × 3 ramp ROWS (living / golden /
    /// bleached, top to bottom). The slice lives in the <c>.meta</c>, not in code, so nothing at
    /// runtime would notice it rotting: a re-bake that drifts the grid, a re-slice that loses a
    /// pivot, or an importer setting that downscales a sheet all land as silently wrong sprites.
    ///
    /// <para><b>The pivot is per-COLUMN — each variant's BUOY (buoyancy centre), the sidecar's
    /// "register the sprite to the water surface here".</b> Cell sizes, column counts and every
    /// buoy point are restated below as literals from the kit README + <c>DriftWeed.json</c> as
    /// delivered, imported from neither <c>DriftWeedSheetSlicer</c> nor the live sidecar file —
    /// asserting the slicer's input against the slicer's input is the self-referential blind spot
    /// that let the mirrored boat art ship. The literals also pin the LIVE sidecar
    /// (<see cref="Sidecar_IsOnDisk_AndBuoyPointsMatchTheKitContractLiterals"/>), closing the loop
    /// from both ends: sidecar edits and slicer drift are each loud, and each failure points at
    /// its own file.</para>
    ///
    /// <para>No heading anywhere — these are flat water-surface clumps (the kit bakes NO turntable,
    /// NO mirrored cells; each variant is its own seed-locked build, identical structure across the
    /// 3 ramp rows). Nothing here touches the GPU, so no Null-device gate is needed.</para>
    /// </summary>
    public class DriftWeedSheetSliceTests
    {
        private const string Drift = "Assets/_Project/Art/Sprites/Shore/Drift/";
        private const string SidecarPath = Drift + "DriftWeed.json";
        private const string RigPath = "docs/art/rigs/driftWeedRig.js";
        private const int RampRows = 3;   // living / golden / bleached, top to bottom

        // ---- the kit contract, restated as literals (README cell math + sidecar buoys) --------

        private readonly struct Kit
        {
            public readonly Vector2Int Cell;
            /// <summary>Per-variant buoy in the sidecar's frame: cell-local px, origin TOP-left,
            /// +y down-screen. Index = variant column.</summary>
            public readonly Vector2Int[] Buoys;
            public Kit(int w, int h, params (int x, int y)[] buoys)
            {
                Cell = new Vector2Int(w, h);
                Buoys = buoys.Select(b => new Vector2Int(b.x, b.y)).ToArray();
            }
            public int Cols => Buoys.Length;
        }

        private static readonly Dictionary<string, Kit> Sheets = new Dictionary<string, Kit>
        {
            // Bladderwrack — 48×36, 4 variants (seeds 4101–4104).
            ["Bladderwrack"] = new Kit(48, 36, (24, 18), (24, 19), (23, 18), (23, 18)),
            // Sugar Kelp — 64×36, 3 variants (seeds 4201–4203).
            ["SugarKelp"] = new Kit(64, 36, (30, 18), (29, 19), (31, 17)),
            // Eelgrass — 32×24, 4 variants (seeds 4301–4304).
            ["Eelgrass"] = new Kit(32, 24, (17, 13), (17, 12), (16, 13), (17, 13)),
            // Torn Mat — 64×48, 3 variants (seeds 4401–4403).
            ["TornMat"] = new Kit(64, 48, (35, 26), (32, 23), (33, 23)),
        };

        private static IEnumerable<string> AllSheets() => Sheets.Keys.OrderBy(s => s);

        private static Kit KitOf(string stem) => Sheets[stem];

        /// <summary>⚠️ Multiple-mode sheets return null from LoadAssetAtPath&lt;Sprite&gt; — LoadAllAssets is the rule.</summary>
        private static Sprite[] LoadSlices(string stem) =>
            AssetDatabase.LoadAllAssetsAtPath(Drift + stem + ".png").OfType<Sprite>().ToArray();

        private static Texture2D LoadSheet(string stem)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Drift + stem + ".png");
            Assert.IsNotNull(tex, $"{stem}.png: failed to load as Texture2D — is the PNG (and its .meta) committed?");
            return tex;
        }

        /// <summary>Expected pivot in BOTTOM-origin pixels (what Unity stores) for a variant column:
        /// the sidecar's buoy is top-left origin, so y flips (cellH − buoy.y).</summary>
        private static Vector2 ExpectedPivotPx(Kit kit, int col) =>
            new Vector2(kit.Buoys[col].x, kit.Cell.y - kit.Buoys[col].y);

        // ---- the assertions -------------------------------------------------------------------

        [Test]
        public void TheGuardedSet_IsTheFullKit()
        {
            // The set arithmetic itself, so a future edit that drops a species or variant by
            // accident is loud: 4 + 3 + 4 + 3 variants × 3 ramp rows = 42 cells.
            Assert.AreEqual(4, Sheets.Count);
            Assert.AreEqual(14, Sheets.Values.Sum(k => k.Cols));
            Assert.AreEqual(42, Sheets.Values.Sum(k => k.Cols) * RampRows);
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_IsSlicedMultipleMode_IntoVariantColumnsTimesThreeRampRows(string stem)
        {
            var importer = AssetImporter.GetAtPath(Drift + stem + ".png") as TextureImporter;
            Assert.IsNotNull(importer, $"{stem}: no TextureImporter — is the .meta committed?");
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode,
                            $"{stem}: must stay grid-sliced (Multiple), not a Single sprite");

            var tex = LoadSheet(stem);
            Kit kit = KitOf(stem);

            Assert.AreEqual(kit.Cols * kit.Cell.x, tex.width,
                            $"{stem}: sheet must be {kit.Cols} variant columns of {kit.Cell.x} px");
            Assert.AreEqual(RampRows * kit.Cell.y, tex.height,
                            $"{stem}: sheet must be {RampRows} ramp rows of {kit.Cell.y} px");

            Assert.AreEqual(kit.Cols * RampRows, LoadSlices(stem).Length,
                            $"{stem}: expected {kit.Cols} variants × {RampRows} ramps = {kit.Cols * RampRows} slices");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_ImportsAtNativeRes_NotDownscaled(string stem)
        {
            // The largest sheet here is 192×144 — nowhere near the 2048 default cap — so this
            // should never bite. Assert it anyway (the discipline of the Cape Islander lesson): a
            // downscaled sheet cannot carry a source-pixel grid while the sprite COUNT still
            // matches, so only this and the pivot tests would ever catch it.
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
        public void EverySlice_IsOneCell_AndPivotsOnItsVariantsBuoy(string stem)
        {
            // ⚠️ Pixels, not normalized, and per COLUMN — a per-sheet pivot or a flipped y reads
            // as a plausible number and silently floats the clump off its buoyancy centre (3 cm
            // per pixel at PPU 32, forever, in the future drift feature).
            Kit kit = KitOf(stem);
            var slices = LoadSlices(stem);
            Assert.IsNotEmpty(slices, $"{stem}: no slices loaded");
            foreach (var s in slices)
            {
                Assert.AreEqual(kit.Cell.x, s.rect.width, 0.01f, $"{s.name}: cell width drifted");
                Assert.AreEqual(kit.Cell.y, s.rect.height, 0.01f, $"{s.name}: cell height drifted");

                int col = Mathf.RoundToInt(s.rect.x) / kit.Cell.x;
                Vector2 exp = ExpectedPivotPx(kit, col);
                Assert.AreEqual(exp.x, s.pivot.x, 0.01f,
                                $"{s.name}: pivot.x off variant {col}'s buoy ({kit.Buoys[col].x} px from the left)");
                Assert.AreEqual(exp.y, s.pivot.y, 0.01f,
                                $"{s.name}: pivot.y off variant {col}'s buoy — is it inverted? The sidecar's " +
                                $"buoy.y is {kit.Buoys[col].y} px down from the cell TOP; Unity stores " +
                                $"bottom-origin px, so the stored value must be {kit.Cell.y} − {kit.Buoys[col].y} = {exp.y}");
            }
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void EverySlice_NormalizedPivot_IsItsBuoyRule(string stem)
        {
            // The same rule in NORMALIZED terms — the number actually stored in the .meta. This
            // catches a "generalisation" that quietly reused one column's fraction on another
            // column's cell (the buoys genuinely differ column to column).
            Kit kit = KitOf(stem);
            foreach (var s in LoadSlices(stem))
            {
                int col = Mathf.RoundToInt(s.rect.x) / kit.Cell.x;
                float expectedX = (float)kit.Buoys[col].x / kit.Cell.x;
                float expectedY = (float)(kit.Cell.y - kit.Buoys[col].y) / kit.Cell.y;
                Assert.AreEqual(expectedX, s.pivot.x / s.rect.width, 0.0005f,
                                $"{s.name}: normalized pivot.x must be {kit.Buoys[col].x}/{kit.Cell.x} = {expectedX}");
                Assert.AreEqual(expectedY, s.pivot.y / s.rect.height, 0.0005f,
                                $"{s.name}: normalized pivot.y must be ({kit.Cell.y}−{kit.Buoys[col].y})/{kit.Cell.y} = {expectedY}");
            }
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void RampRows_ShareTheirColumnsPivot(string stem)
        {
            // The kit contract: every ramp row is the SAME seed-locked build recoloured, so the
            // buoy — an area centroid of the structure — is identical down a column. Three rows,
            // one pivot per column; a row that drifts means the bake broke seed-stability.
            Kit kit = KitOf(stem);
            foreach (var byCol in LoadSlices(stem).GroupBy(s => Mathf.RoundToInt(s.rect.x) / kit.Cell.x))
            {
                Assert.AreEqual(RampRows, byCol.Count(),
                                $"{stem} column {byCol.Key}: expected one slice per ramp row");
                var pivots = byCol.Select(s => s.pivot).Distinct().ToArray();
                Assert.AreEqual(1, pivots.Length,
                                $"{stem} column {byCol.Key}: ramp rows disagree on the pivot " +
                                $"({string.Join(" vs ", pivots.Select(p => p.ToString("F3")))})");
            }
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Slices_TileTheSheet_WithNoGapsAndNoOverlap(string stem)
        {
            Kit kit = KitOf(stem);
            var occupied = new HashSet<(int, int)>();
            foreach (var s in LoadSlices(stem))
            {
                Assert.AreEqual(0, Mathf.RoundToInt(s.rect.x) % kit.Cell.x, $"{s.name}: x not on the cell grid");
                Assert.AreEqual(0, Mathf.RoundToInt(s.rect.y) % kit.Cell.y, $"{s.name}: y not on the cell grid");
                var c = (Mathf.RoundToInt(s.rect.x) / kit.Cell.x, Mathf.RoundToInt(s.rect.y) / kit.Cell.y);
                Assert.IsTrue(occupied.Add(c), $"{s.name}: two slices overlap cell {c}");
            }
            for (int c = 0; c < kit.Cols; c++)
                for (int r = 0; r < RampRows; r++)
                    Assert.IsTrue(occupied.Contains((c, r)), $"{stem}: no slice covers cell (col {c}, row {r})");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Slices_AreNamedByRowMajorIndex_FromTheTopLeft(string stem)
        {
            // House scheme (SpriteSheetSlicer / the flower sheets): <Stem>_<index>, row-major from
            // the TOP-LEFT cell — index = rampRow×cols + variant, so index/cols is the ramp
            // (0 living · 1 golden · 2 bleached) and index%cols is the variant. A name states
            // geometry; the ramp/variant semantics live in the sidecar.
            Kit kit = KitOf(stem);
            var seen = new HashSet<string>();
            foreach (var s in LoadSlices(stem))
            {
                StringAssert.StartsWith(stem + "_", s.name, $"{s.name}: unexpected slice name");
                Assert.IsTrue(seen.Add(s.name), $"{s.name}: duplicate slice name");
                Assert.IsTrue(int.TryParse(s.name.Substring(stem.Length + 1), out int index),
                              $"{s.name}: must be <stem>_<index>");

                int rowFromTop = RampRows - 1 - Mathf.RoundToInt(s.rect.y) / kit.Cell.y;
                int col = Mathf.RoundToInt(s.rect.x) / kit.Cell.x;
                Assert.AreEqual(rowFromTop * kit.Cols + col, index,
                                $"{s.name}: name says index {index} but the rect sits at ramp row " +
                                $"{rowFromTop} (from the top), variant column {col}");
            }
        }

        [Test]
        public void EveryDriftPngInTheFolder_IsCoveredByThisTest()
        {
            // A new sheet dropped into the folder must not slip past the guard unnoticed, and a
            // guarded sheet going missing must be loud.
            var onDisk = Directory.GetFiles(Drift, "*.png")
                                  .Select(Path.GetFileNameWithoutExtension)
                                  .OrderBy(s => s).ToArray();
            CollectionAssert.AreEquivalent(AllSheets().ToArray(), onDisk,
                                           "Drift weed sheets on disk differ from the guarded set");
        }

        // ---- the sidecar itself ---------------------------------------------------------------

        [Test]
        public void Sidecar_IsOnDisk_AndBuoyPointsMatchTheKitContractLiterals()
        {
            // The literals above are the kit AS DELIVERED (2026-07-23). The live sidecar is the
            // slicer's input, so an edit to it would re-pivot the next slice silently — this pins
            // the file to the delivered contract, and a legitimate future re-bake updates both.
            Assert.IsTrue(File.Exists(SidecarPath), $"missing {SidecarPath}");
            var sidecar = JsonUtility.FromJson<Sidecar>(File.ReadAllText(SidecarPath));

            foreach (var (stem, species) in new (string, Species)[]
            {
                ("Bladderwrack", sidecar.species.Bladderwrack),
                ("SugarKelp", sidecar.species.SugarKelp),
                ("Eelgrass", sidecar.species.Eelgrass),
                ("TornMat", sidecar.species.TornMat),
            })
            {
                Kit kit = KitOf(stem);
                Assert.IsNotNull(species, $"{stem}: species block missing from the sidecar");
                Assert.AreEqual(stem + ".png", species.sheet, $"{stem}: sidecar names a different sheet");
                Assert.AreEqual(kit.Cell.x, species.cellW, $"{stem}: sidecar cellW drifted");
                Assert.AreEqual(kit.Cell.y, species.cellH, $"{stem}: sidecar cellH drifted");
                Assert.AreEqual(kit.Cols, species.cols, $"{stem}: sidecar cols drifted");
                Assert.AreEqual(RampRows, species.rows, $"{stem}: sidecar rows drifted");
                Assert.AreEqual(kit.Cols, species.variants.Length, $"{stem}: variant count drifted");
                foreach (var v in species.variants)
                {
                    Assert.AreEqual(kit.Buoys[v.cell].x, v.buoy.px[0],
                                    $"{stem} variant {v.cell}: buoy.x drifted from the delivered kit");
                    Assert.AreEqual(kit.Buoys[v.cell].y, v.buoy.px[1],
                                    $"{stem} variant {v.cell}: buoy.y drifted from the delivered kit");
                }
            }
        }

        [Test]
        public void Sidecar_RigHash_MatchesTheLandedRigSource()
        {
            // The kit's own drift tripwire: derivedFromRigSha256 is the hash of driftWeedRig.js as
            // shipped — if the rig source changes, the sidecar (and the sheets) must be re-baked
            // together. Hashed over CRLF→LF-normalized bytes so a Windows autocrlf checkout of the
            // (LF-authored) rig cannot fake a drift; a real edit still fires.
            Assert.IsTrue(File.Exists(RigPath), $"missing {RigPath}");
            var sidecar = JsonUtility.FromJson<Sidecar>(File.ReadAllText(SidecarPath));
            Assert.IsFalse(string.IsNullOrEmpty(sidecar.derivedFromRigSha256),
                           "sidecar carries no derivedFromRigSha256");

            byte[] normalized = File.ReadAllBytes(RigPath).Where(b => b != (byte)'\r').ToArray();
            string hash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                hash = string.Concat(sha.ComputeHash(normalized).Select(b => b.ToString("x2")));

            Assert.AreEqual(sidecar.derivedFromRigSha256, hash,
                            $"{RigPath} no longer matches the sidecar's derivedFromRigSha256 — the rig " +
                            "source changed after the bake; re-bake the sheets + sidecar together (the " +
                            "rig is art-director source: never edit it in-repo).");
        }

        [Test]
        public void Sidecar_OwnerRulings_AreRecorded_AndNoConfirmRemains()
        {
            // The four _confirm judgments shipped with the kit were RULED by the owner 2026-07-23
            // and recorded in place as _ruled (the PR #247 append-only style: original judgment
            // text kept — provenance, not deletion). Guard both directions: the rulings stay, and
            // no un-ruled _confirm key sneaks back in with a future re-bake.
            string json = File.ReadAllText(SidecarPath);
            StringAssert.Contains("\"_ruled\"", json, "the owner's rulings block is missing");
            foreach (string key in new[] { "TornMat.dragTail", "golden_rows", "snag_radius", "stranded_set" })
                StringAssert.Contains($"\"{key}\": \"was _confirm:", json,
                                      $"ruling '{key}' must keep its original judgment text (append-only)");
            Assert.IsFalse(json.Contains("\"_confirm\""),
                           "an un-ruled \"_confirm\" block is present — a re-bake reintroduced open " +
                           "judgments; take them to the owner and record the rulings");
        }

        // ---- minimal sidecar model (deliberately restated here, not imported from the slicer:
        // the test must fail if the slicer's parse and the file disagree) ------------------------

        [System.Serializable] private class Buoy { public int[] px; }
        [System.Serializable] private class Variant { public int cell; public Buoy buoy; }

        [System.Serializable]
        private class Species
        {
            public string sheet;
            public int cellW, cellH, cols, rows;
            public Variant[] variants;
        }

        [System.Serializable]
        private class SpeciesSet { public Species Bladderwrack, SugarKelp, Eelgrass, TornMat; }

        [System.Serializable]
        private class Sidecar
        {
            public string derivedFromRigSha256;
            public SpeciesSet species;
        }
    }
}
