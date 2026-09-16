using System;
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
    /// Guards the <b>Rock Px</b> kit (Rock Iso, pass three) as it LANDS — the contract
    /// <c>Art/Sprites/Shore/RockPx/RockPx.json</c>, its sixteen sidecars, its seat index, the 956
    /// PNGs, and the catalog and slicer that read them.
    ///
    /// <para><b>Unlike pass two, this kit ships its pixels</b>, so a missing file is a failure here
    /// rather than a kit that simply has not been baked yet. Pass two
    /// (<see cref="RockIsoCatalog"/>, <see cref="RockSheetSlicer"/>) is NOT retired by this PR and
    /// nothing below touches it.</para>
    ///
    /// <para>Every dimension and axis is restated as a LITERAL from the drop's README, then checked
    /// against a fresh read of the live JSON, then checked against the asset actually on disk — the
    /// three legs this suite always uses, because asserting the catalog against the catalog is the
    /// self-referential blind spot that let mirrored boat art ship.</para>
    ///
    /// <para><b>CI-safe and GPU-free.</b> Pixels are read with
    /// <c>Texture2D.LoadImage(bytes, markNonReadable: false)</c> + <c>GetPixels32</c>, the pair this
    /// suite already relies on because CI has no graphics device. No scene is loaded, no importer is
    /// driven, nothing is written.</para>
    ///
    /// <para><b>Two guards in here were RED on the first intake and are now green from
    /// upstream, not from this side.</b> <see cref="RigPair_LandedBesideTheKit"/> was red because
    /// the 2026-09-11 drop shipped 900 PNGs with no rig; the 2026-09-13 re-drop carries
    /// <c>bake/</c>, and the rig's bytes still hash to the digest every JSON already stamped — the
    /// rig was NOT regenerated, so nothing was re-pinned here.
    /// <see cref="EveryDeclaredFormStonePair_HasAFourVariantSidecarBlock"/> was red because six
    /// declared pairs had no sidecar block at all; the re-export ADDED those six blocks and left
    /// the other 276 variants byte-identical, so all 300 now carry a ground contact. Neither guard
    /// was relaxed and no anchor was hand-written.</para>
    /// </summary>
    public class RockPxKitContractTests
    {
        // ---- the drop's README and contract, restated as literals -------------------------------

        const string KitRoot = "Assets/_Project/Art/Sprites/Shore/RockPx/";
        const string ContractPath = KitRoot + "RockPx.json";
        const string TexDir = KitRoot + "tex/";
        const string SidecarDir = KitRoot + "sidecar/";
        const string SeatDir = KitRoot + "seat/";
        const string SeatsPath = SeatDir + "seats.json";

        const string RigDir = "docs/art/rigs/rock-px-kit/";
        const string BakeDir = RigDir + "bake/";
        const string RigPath = BakeDir + "pxRockIso2.js";
        const string ExportHarnessPath = BakeDir + "_pxRock2Export.js";
        const string ReadmePath = RigDir + "README.md";

        /// <summary>What the rig loads before it can run, in the README's order. They are shipped
        /// beside the rig rather than shared with another kit's copies because they are NOT the same
        /// bytes as the greenery kit's — a rig must load the libraries it was baked against.</summary>
        static readonly string[] RigDeps =
        {
            BakeDir + "pixelLanguage.js", BakeDir + "pxKit.js", BakeDir + "pxKit2.js",
        };

        /// <summary>The digest the README states and every JSON in the drop stamps.</summary>
        const string RigSha256 =
            "3b6ec36d7891893c1b970859b5e9de4c744c6404540d3b888ab3a0119615d58c";

        const int Ppu = 32;
        const int Cols = 8;
        const int Rows = 3;
        const int VariantsPerForm = 4;
        const int Forms = 16;
        const int Pairs = 75;            // (form × its own stones)
        const int Sheets = 225;          // Pairs × 3 dress
        const int SheetPngs = 900;       // Sheets × 4 channels
        const int Decals = 28;
        const int SeatPngs = 56;         // Decals × 2 channels
        const int TotalPngs = 956;
        const int Jsons = 18;            // contract + 16 sidecars + seats

        static readonly string[] Tides = { "dry", "wet", "awash" };
        static readonly string[] DressRows = { "bare", "barnacled", "weeded" };

        static readonly string[] FormKeys =
        {
            "erratic", "block", "perched", "slab", "fin", "wedge", "cloven", "knuckle",
            "bench", "shelf", "prisms", "scree", "cobbles", "skerry", "spine", "apron",
        };

        /// <summary>Cell size per form, read off the contract at intake and restated here so a
        /// silent cell change fails instead of re-deriving itself.</summary>
        static readonly Dictionary<string, Vector2Int> Cells = new Dictionary<string, Vector2Int>
        {
            { "erratic", new Vector2Int(56, 50) },  { "block",   new Vector2Int(56, 54) },
            { "perched", new Vector2Int(58, 62) },  { "slab",    new Vector2Int(74, 42) },
            { "fin",     new Vector2Int(46, 66) },  { "wedge",   new Vector2Int(54, 56) },
            { "cloven",  new Vector2Int(62, 86) },  { "knuckle", new Vector2Int(70, 38) },
            { "bench",   new Vector2Int(98, 46) },  { "shelf",   new Vector2Int(82, 46) },
            { "prisms",  new Vector2Int(66, 62) },  { "scree",   new Vector2Int(78, 46) },
            { "cobbles", new Vector2Int(58, 32) },  { "skerry",  new Vector2Int(102, 46) },
            { "spine",   new Vector2Int(122, 42) }, { "apron",   new Vector2Int(116, 58) },
        };

        // =====================================================================================
        // gate 2 — the rig digest pin
        // =====================================================================================

        /// <summary>
        /// The rig, its export harness and the three libraries they load. 900 PNGs with no rig is
        /// art nobody can ever rebake: the README's first line is "Regenerate, never hand-edit",
        /// and there would be nothing to regenerate from.
        /// </summary>
        [Test]
        public void RigPair_LandedBesideTheKit()
        {
            Assert.IsTrue(File.Exists(RigPath),
                $"'{RigPath}' is missing. The kit is baked FROM this file and every JSON in it " +
                "stamps the rig's digest — landing the sheets without it would land 900 PNGs " +
                "nothing can regenerate. Ask the art director for the rig pair; do not land the " +
                "kit rig-less.");

            Assert.IsTrue(File.Exists(ExportHarnessPath),
                $"'{ExportHarnessPath}' is missing — the export harness is half the pair, and " +
                "without it the rig alone cannot reproduce the sheet layout or the sidecars.");

            Assert.IsTrue(File.Exists(ReadmePath),
                $"'{ReadmePath}' is missing — the kit's shading law and channel table live there.");

            foreach (string dep in RigDeps)
                Assert.IsTrue(File.Exists(dep),
                    $"'{dep}' is missing — the rig loads it before it can draw a single rock, so " +
                    "the pair alone would not rebake this kit. The README names all five files and " +
                    "the order they load in.");
        }

        /// <summary>
        /// The digest pin. ⚠ A mismatch means the sheets and the contract have come apart — the fix
        /// is a REBAKE by the art director, never a re-stamp from this side.
        /// </summary>
        [Test]
        public void RigDigest_IsTheOneEveryJsonStamps()
        {
            Assert.AreEqual(RigSha256, RockPxCatalog.RigSha256,
                "the catalog's pin and this test's literal disagree — one of them was edited " +
                "without the other, which is exactly the drift the pin exists to catch.");

            if (!File.Exists(RigPath))
                Assert.Fail(
                    $"'{RigPath}' is not on the branch, so the digest cannot be computed. See " +
                    $"{nameof(RigPair_LandedBesideTheKit)} — this is the same blocker, not a " +
                    "second one.");

            string hash = Sha256Lf(File.ReadAllBytes(RigPath));
            Assert.AreEqual(RigSha256, hash,
                "the rig's LF-normalised sha256 does not match the digest every JSON in this kit " +
                "carries. The sheets and the contract have come apart. STOP: this is a REBAKE, " +
                "not a re-stamp — re-pinning the constant would make the mismatch invisible while " +
                "leaving the pixels wrong.");
        }

        /// <summary>All eighteen JSONs must carry the SAME digest — one drop, one rig.</summary>
        [Test]
        public void EveryJson_CarriesTheSameRigDigest()
        {
            string[] jsons = AllJsonPaths();
            Assert.AreEqual(Jsons, jsons.Length,
                $"expected {Jsons} JSONs (contract + {Forms} sidecars + seats), got " +
                string.Join(", ", jsons.Select(Path.GetFileName)));

            foreach (string path in jsons)
            {
                var d = MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
                Assert.IsNotNull(d, $"'{path}' did not parse to an object.");
                Assert.AreEqual(RigSha256, MiniJson.String(d, "derivedFromRigSha256"),
                    $"'{Path.GetFileName(path)}' stamps a different rig digest — the drop is mixed.");
                Assert.AreEqual(RockPxCatalog.ExportSymbol, MiniJson.String(d, "exportSymbol"),
                    $"'{Path.GetFileName(path)}' was written by a different export symbol.");
            }
        }

        // =====================================================================================
        // gate 4 — the census
        // =====================================================================================

        [Test]
        public void Census_Is956PngsAnd18Jsons()
        {
            Assert.IsTrue(Directory.Exists(KitRoot), $"'{KitRoot}' is missing — the kit did not land.");

            string[] tex = Directory.GetFiles(TexDir, "*.png", SearchOption.TopDirectoryOnly);
            string[] seat = Directory.GetFiles(SeatDir, "*.png", SearchOption.TopDirectoryOnly);

            Assert.AreEqual(SheetPngs, tex.Length,
                $"expected {Sheets} sheets × 4 channels = {SheetPngs} PNGs under {TexDir}.");
            Assert.AreEqual(SeatPngs, seat.Length,
                $"expected {Decals} decals × 2 channels = {SeatPngs} PNGs under {SeatDir}. " +
                "⚠ The intake handoff said 30 decals / 960 PNGs; the drop declares and ships 28.");
            Assert.AreEqual(TotalPngs, tex.Length + seat.Length);
            Assert.AreEqual(TotalPngs, RockPxCatalog.TotalPngCount,
                "the catalog's census constant and the files on disk disagree.");

            // The contract's OWN self-declared counts, read back rather than assumed.
            RockPxCatalog.Contract c = LoadContract();
            Assert.AreEqual(Sheets, c.SheetCount, "contract.sheetCount");
            Assert.AreEqual(SheetPngs, c.PngCount, "contract.pngCount");
            Assert.AreEqual(Pairs, c.PairCount,
                $"expected {Pairs} (form × stone) pairs across {Forms} forms.");
        }

        /// <summary>
        /// Every stem the CONTRACT implies exists on disk in all four channels, and nothing else is
        /// in <c>tex/</c>. A count alone would pass with 900 of the wrong files.
        /// </summary>
        [Test]
        public void EveryContractStem_ExistsInAllFourChannels()
        {
            RockPxCatalog.Contract c = LoadContract();

            var expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (RockPxCatalog.FormEntry f in c.Forms)
            foreach (string stone in f.Stones)
            foreach (string dress in DressRows)
            foreach (RockPxCatalog.Channel ch in RockPxCatalog.SheetChannels)
                expected.Add(Path.GetFileName(RockPxCatalog.SheetPath(f.Key, stone, dress, ch)));

            Assert.AreEqual(SheetPngs, expected.Count,
                "the contract implies a different number of sheet files than the census.");

            var onDisk = new HashSet<string>(
                Directory.GetFiles(TexDir, "*.png").Select(Path.GetFileName), StringComparer.Ordinal);

            var missing = expected.Except(onDisk).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            var extra = onDisk.Except(expected).OrderBy(s => s, StringComparer.Ordinal).ToArray();

            Assert.IsEmpty(missing, "the contract claims sheets that did not land: " +
                                    string.Join(", ", missing.Take(12)));
            Assert.IsEmpty(extra, "PNGs landed that no (form × stone × dress × channel) claims: " +
                                  string.Join(", ", extra.Take(12)));
        }

        // =====================================================================================
        // gate 1 — the kit contract
        // =====================================================================================

        [Test]
        public void ContractAxes_MatchTheReadmeLiterals()
        {
            RockPxCatalog.Contract c = LoadContract();

            Assert.AreEqual(RockPxCatalog.KitName, c.Kit);
            Assert.AreEqual(Ppu, c.Ppu, "PPU is locked at 32 (ADR 0006/0022).");
            Assert.AreEqual(1, c.Texel, "one texel per pixel — no half-res channels.");
            Assert.AreEqual(Cols, c.SheetCols, "8 cols = 4 variants then the same 4 MIRRORED.");
            Assert.AreEqual(Rows, c.SheetRows, "3 rows = tide.");
            CollectionAssert.AreEqual(Tides, c.Tides, "the ROW axis is tide, in this order.");
            CollectionAssert.AreEqual(DressRows, c.Dress,
                "dress is a separate SHEET and 'bare' is unsuffixed.");
            CollectionAssert.AreEqual(FormKeys, c.Forms.Select(f => f.Key).ToArray());

            Assert.AreEqual(Cols, RockPxCatalog.SheetCols);
            Assert.AreEqual(Rows, RockPxCatalog.SheetRows);
            Assert.AreEqual(Ppu, RockPxCatalog.Ppu);

            foreach (RockPxCatalog.FormEntry f in c.Forms)
            {
                Vector2Int cell = Cells[f.Key];
                Assert.AreEqual(cell.x, f.CellW, $"{f.Key} cell width");
                Assert.AreEqual(cell.y, f.CellH, $"{f.Key} cell height");
                Assert.Contains(f.NativeStone, f.Stones,
                    $"{f.Key} declares a native stone it does not ship.");
                CollectionAssert.AllItemsAreUnique(f.Stones, $"{f.Key} repeats a stone.");
                foreach (string stone in f.Stones)
                    Assert.Contains(stone, RockPxCatalog.Stones,
                        $"{f.Key} ships a stone the kit does not know.");
            }
        }

        /// <summary>
        /// All 75 declared (form, stone) pairs, four variants each. ⚠ This was RED on the first
        /// intake: six pairs — erratic/granite, block/granite, block/sandstone, knuckle/sandstone,
        /// spine/sandstone and spine/granite — shipped their sheets with no anchors, four of them
        /// the form's NATIVE stone. The 2026-09-13 re-export ADDED exactly those six blocks and
        /// changed nothing else, so the fix came from the rig and not from a hand-written anchor,
        /// which would have been a guess about where a rock touches the ground — the one number
        /// this kit exists to state.
        /// </summary>
        [Test]
        public void EveryDeclaredFormStonePair_HasAFourVariantSidecarBlock()
        {
            RockPxCatalog.Contract c = LoadContract();
            var missing = new List<string>();

            foreach (RockPxCatalog.FormEntry f in c.Forms)
            {
                RockPxCatalog.Sidecar sc = RockPxCatalog.LoadSidecar(f.Key);
                Assert.IsNotNull(sc, $"{RockPxCatalog.SidecarPath(f.Key)} failed to read.");
                Assert.AreEqual(f.Key, sc.Form, "the sidecar names a different form.");
                Assert.AreEqual(f.CellW, sc.CellW, $"{f.Key} sidecar cell width disagrees with the contract.");
                Assert.AreEqual(f.CellH, sc.CellH, $"{f.Key} sidecar cell height disagrees with the contract.");

                foreach (string stone in f.Stones)
                {
                    List<RockPxCatalog.VariantEntry> vs = sc.For(stone);
                    if (vs == null) { missing.Add($"{f.Key}/{stone} (no block)"); continue; }
                    if (vs.Count != VariantsPerForm)
                    {
                        missing.Add($"{f.Key}/{stone} ({vs.Count} variants)");
                        continue;
                    }

                    for (int i = 0; i < vs.Count; i++)
                    {
                        Assert.AreEqual(i, vs[i].Variant,
                            $"{f.Key}/{stone} variant {i} declares column {vs[i].Variant}.");
                        Assert.AreEqual($"{f.Key}{RockPxCatalog.VariantLetter(i)}", vs[i].Id,
                            $"{f.Key}/{stone} variant {i} has an unexpected id.");
                    }
                }
            }

            Assert.IsEmpty(missing,
                "declared (form, stone) pairs with no usable sidecar block — their sheets exist but " +
                "their anchors do not, so nothing can pivot them on their ground contact. This is an " +
                "UPSTREAM defect: re-export the sidecars. Do NOT hand-write anchors. Missing: " +
                string.Join(", ", missing));
        }

        /// <summary>Every anchor must lie inside the cell it is measured in — an anchor outside is a
        /// coordinate-space mistake, and it would place rope, perch or pool off the rock.</summary>
        [Test]
        public void EveryAnchor_LiesInsideItsCell()
        {
            ForEachVariant((form, stone, v) =>
            {
                int w = Cells[form].x, h = Cells[form].y;
                string who = $"{form}/{stone}/{v.Id}";

                Assert.AreEqual(w, v.CellW, $"{who} restates a different cell width.");
                Assert.AreEqual(h, v.CellH, $"{who} restates a different cell height.");

                InCell(who, "footprint.ground", v.Ground, w, h);
                InCell(who, "pivot", v.Pivot, w, h);
                InCell(who, "perch", v.Perch, w, h);
                for (int i = 0; i < v.Snags.Count; i++)
                    InCell(who, $"snags[{i}]", v.Snags[i], w, h);
                if (v.Hazard.HasValue) InCell(who, "hazard", v.Hazard.Value, w, h);

                Assert.That(v.WeedLineY, Is.InRange(0, h), $"{who} weedLine.y is outside the cell.");

                if (v.Pool.HasValue)
                {
                    RectInt p = v.Pool.Value;
                    Assert.That(p.xMin, Is.InRange(0, w), $"{who} pool.x");
                    Assert.That(p.yMin, Is.InRange(0, h), $"{who} pool.y");
                    Assert.That(p.xMax, Is.InRange(0, w), $"{who} pool right edge leaves the cell.");
                    Assert.That(p.yMax, Is.InRange(0, h), $"{who} pool bottom edge leaves the cell.");
                    Assert.Greater(p.width, 0, $"{who} pool has no width.");
                    Assert.Greater(p.height, 0, $"{who} pool has no height.");
                    Assert.Greater(v.PoolDepthM, 0f, $"{who} pool has no depth.");
                }

                Assert.Greater(v.FootprintRx, 0f, $"{who} footprint.rx");
                Assert.Greater(v.FootprintRy, 0f, $"{who} footprint.ry");
                Assert.Greater(v.TopM, 0f, $"{who} topM");
                Assert.Greater(v.SizeM, 0f, $"{who} sizeM");
            });
        }

        /// <summary>
        /// The pivot law, in one place: <c>anchors.pivot</c> IS <c>anchors.footprint.ground</c>, the
        /// contact sits on the cell's centre line (that is what "bottom-centre ground contact"
        /// means), and it is ABOVE the cell floor by the near-flank overhang — measured 5–18 px
        /// over all 300 variants, positive every time, which is why a BottomCenter pivot would float
        /// every rock in the kit. The range is unchanged by the re-export: the 24 variants it added
        /// land inside the 276 the first intake measured.
        /// </summary>
        [Test]
        public void Pivot_IsTheGroundContact_OnTheCentreLine_AboveTheCellFloor()
        {
            int minOverhang = int.MaxValue, maxOverhang = int.MinValue;

            ForEachVariant((form, stone, v) =>
            {
                int w = Cells[form].x, h = Cells[form].y;
                string who = $"{form}/{stone}/{v.Id}";

                Assert.AreEqual(v.Ground, v.Pivot,
                    $"{who}: anchors.pivot and anchors.footprint.ground disagree. The README names " +
                    "the ground contact as the pivot; two different answers means one of them is a " +
                    "bbox centre.");

                Assert.AreEqual(w / 2, v.Ground.x,
                    $"{who}: the ground contact is off the cell's centre line. 'bottom-CENTRE ground " +
                    "contact' is the README's own claim; a contact off centre also means the four " +
                    "MIRRORED columns need the mirrored pivot to stay on their own contact point.");

                int overhang = RockPxCatalog.GroundOverhangPx(v, h);
                Assert.Greater(overhang, 0,
                    $"{who}: the ground contact is at or below the cell floor, so there is no near " +
                    "flank — a BottomCenter pivot would then be right and this kit's whole pivot " +
                    "rule would be unnecessary. Re-measure before believing it.");
                Assert.Less(overhang, h,
                    $"{who}: the ground contact is above the top of the cell.");

                minOverhang = Mathf.Min(minOverhang, overhang);
                maxOverhang = Mathf.Max(maxOverhang, overhang);
            });

            // The range the catalog's docs quote, pinned so the doc cannot quietly go stale.
            Assert.AreEqual(5, minOverhang, "smallest near-flank overhang in the drop, in px.");
            Assert.AreEqual(18, maxOverhang, "largest near-flank overhang in the drop, in px.");
        }

        /// <summary>The mirror law, as arithmetic: a mirrored column pivots at <c>cellW − x</c>.
        /// Pure CPU, no asset — this is the rule, not the measurement.</summary>
        [Test]
        public void MirroredColumn_TakesTheMirroredPivot()
        {
            var v = new RockPxCatalog.VariantEntry { Ground = new Vector2Int(20, 30) };
            const int w = 56, h = 50;

            for (int c = 0; c < RockPxCatalog.SheetCols; c++)
            {
                Assert.AreEqual(c % 4, RockPxCatalog.VariantForColumn(c), $"column {c} variant");
                Assert.AreEqual(c >= 4, RockPxCatalog.IsMirroredColumn(c), $"column {c} mirror flag");

                Vector2Int p = RockPxCatalog.PivotPxForColumn(v, w, c);
                Assert.AreEqual(c >= 4 ? w - 20 : 20, p.x, $"column {c} pivot x");
                Assert.AreEqual(30, p.y, $"column {c} pivot y — a mirror is horizontal only.");

                Vector2 n = RockPxCatalog.NormalizedPivot(v, w, h, c);
                Assert.AreEqual(p.x / (float)w, n.x, 1e-5f, $"column {c} normalised x");
                Assert.AreEqual((h - p.y) / (float)h, n.y, 1e-5f,
                    $"column {c} normalised y — ADR 0026's (H − y)/H, not the tree kit's pad/H.");
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => RockPxCatalog.VariantForColumn(8));
            Assert.Throws<ArgumentOutOfRangeException>(() => RockPxCatalog.CellRect(w, h, 0, 3));
        }

        // =====================================================================================
        // gate 1 — the asset, measured
        // =====================================================================================

        /// <summary>Every one of the 900 sheets is exactly <c>cell.w × 8</c> by <c>cell.h × 3</c>,
        /// and clears the import cap. Dimensions are read from the PNG header, so this stays cheap
        /// and needs no decode.</summary>
        [Test]
        public void EverySheet_IsEightCellsByThreeAndClearsTheImportCap()
        {
            RockPxCatalog.Contract c = LoadContract();
            int checkedCount = 0;

            foreach (RockPxCatalog.FormEntry f in c.Forms)
            foreach (string stone in f.Stones)
            foreach (string dress in DressRows)
            foreach (RockPxCatalog.Channel ch in RockPxCatalog.SheetChannels)
            {
                string path = RockPxCatalog.SheetPath(f.Key, stone, dress, ch);
                Assert.IsTrue(File.Exists(path), $"'{path}' is missing.");

                Vector2Int size = PngSize(path);
                Assert.AreEqual(f.CellW * Cols, size.x,
                    $"'{Path.GetFileName(path)}' width: expected {Cols} cells of {f.CellW}.");
                Assert.AreEqual(f.CellH * Rows, size.y,
                    $"'{Path.GetFileName(path)}' height: expected {Rows} cells of {f.CellH}.");

                Assert.LessOrEqual(size.x, RockPxCatalog.ImportSizeCap,
                    $"'{Path.GetFileName(path)}' is wider than the {RockPxCatalog.ImportSizeCap} px " +
                    "import cap — Unity would DOWNSCALE it silently and the sprite count would still " +
                    "come out right.");
                Assert.LessOrEqual(size.y, RockPxCatalog.ImportSizeCap,
                    $"'{Path.GetFileName(path)}' is taller than the import cap.");

                checkedCount++;
            }

            Assert.AreEqual(SheetPngs, checkedCount);
        }

        /// <summary>
        /// <b>Gate 3 — channel co-registration.</b> The four channels of a sheet must describe the
        /// SAME pixels: a mask that covered one texel the albedo does not would relight a hole, and
        /// a normal trimmed differently would light the rim off by that texel.
        ///
        /// <para><b>This pins EQUALITY, not the tree kit's superset</b>, because equality is what
        /// this drop measures: over all 225 sheets × 3 data channels — 3,778,856 covered pixels —
        /// the count of texels covered in one channel but not the other is ZERO in BOTH directions.
        /// Pinning the weaker "data ⊇ albedo" relation here would let a future rebake silently start
        /// padding the data channels. Pin the relationship you measure.</para>
        ///
        /// <para>CI-safe: <c>LoadImage</c> + <c>GetPixels32</c>, no GPU, nothing written.</para>
        /// </summary>
        [Test]
        public void Channels_AreAlphaCoRegistered()
        {
            RockPxCatalog.Contract c = LoadContract();
            long covered = 0;
            int sheets = 0;

            foreach (RockPxCatalog.FormEntry f in c.Forms)
            foreach (string stone in f.Stones)
            foreach (string dress in DressRows)
            {
                bool[] albedo = Coverage(RockPxCatalog.SheetPath(
                    f.Key, stone, dress, RockPxCatalog.Channel.Albedo), out int w, out int h);
                covered += albedo.Count(b => b);
                sheets++;

                foreach (RockPxCatalog.Channel ch in RockPxCatalog.SheetChannels)
                {
                    if (ch == RockPxCatalog.Channel.Albedo) continue;

                    string path = RockPxCatalog.SheetPath(f.Key, stone, dress, ch);
                    bool[] other = Coverage(path, out int ow, out int oh);

                    Assert.AreEqual(w, ow, $"'{Path.GetFileName(path)}' width");
                    Assert.AreEqual(h, oh, $"'{Path.GetFileName(path)}' height");

                    int onlyAlbedo = 0, onlyOther = 0;
                    for (int i = 0; i < albedo.Length; i++)
                    {
                        if (albedo[i] && !other[i]) onlyAlbedo++;
                        else if (!albedo[i] && other[i]) onlyOther++;
                    }

                    Assert.AreEqual(0, onlyAlbedo,
                        $"'{Path.GetFileName(path)}': {onlyAlbedo} texel(s) are painted in the " +
                        "albedo but transparent in this channel — those pixels would relight to " +
                        "nothing.");
                    Assert.AreEqual(0, onlyOther,
                        $"'{Path.GetFileName(path)}': {onlyOther} texel(s) are covered here but " +
                        "transparent in the albedo — the channels are no longer co-registered.");
                }
            }

            Assert.AreEqual(Sheets, sheets);
            Assert.AreEqual(3778856L, covered,
                "total covered texels across the kit's albedo sheets — the population the " +
                "co-registration claim is measured over. A different number means the art moved, " +
                "and the zero above stops being evidence about THIS kit.");
        }

        // =====================================================================================
        // the seats
        // =====================================================================================

        [Test]
        public void SeatDecals_ShareTheirRocksCellAndShipABlendSibling()
        {
            RockPxCatalog.SeatSet seats = RockPxCatalog.LoadSeats();
            Assert.IsNotNull(seats, $"'{SeatsPath}' failed to read.");
            Assert.AreEqual(Decals, seats.Decals.Count,
                "⚠ the intake handoff said 30 decals; seats.json declares 28.");

            RockPxCatalog.Contract c = LoadContract();
            int seams = 0;

            foreach (RockPxCatalog.SeatDecal d in seats.Decals)
            {
                string who = d.Stem;
                Assert.IsNotNull(who, "a decal declares no file.");
                Assert.Contains(d.Form, FormKeys, $"{who} names an unknown form.");
                Assert.That(d.Variant, Is.InRange(0, VariantsPerForm - 1), $"{who} variant");
                Assert.Contains(d.Seat, RockPxCatalog.Seats, $"{who} names an unknown seat material.");
                Assert.Greater(d.Reach, 0, $"{who} has no reach.");

                // The decal is drawn OVER the rock on the rock's own pivot, so it must BE the cell.
                RockPxCatalog.FormEntry form = c.Find(d.Form);
                Assert.AreEqual(form.CellW, d.CellW, $"{who} cell width is not its form's.");
                Assert.AreEqual(form.CellH, d.CellH, $"{who} cell height is not its form's.");
                Assert.AreEqual(form.CellW / 2, d.Pivot.x,
                    $"{who} pivot is off the centre line — it would not sit on the rock it dresses.");
                Assert.That(d.Pivot.y, Is.InRange(1, form.CellH - 1), $"{who} pivot y");

                if (d.IsSeam)
                {
                    seams++;
                    Assert.Contains(d.Seat2, RockPxCatalog.Seats, $"{who} names an unknown seat2.");
                    Assert.AreNotEqual(d.Seat, d.Seat2,
                        $"{who} is a seam between a material and itself.");
                }

                foreach (RockPxCatalog.Channel ch in RockPxCatalog.SeatChannels)
                {
                    string path = d.Path(ch);
                    Assert.IsTrue(File.Exists(path), $"'{path}' is missing.");
                    Vector2Int size = PngSize(path);
                    Assert.AreEqual(d.CellW, size.x, $"'{Path.GetFileName(path)}' width");
                    Assert.AreEqual(d.CellH, size.y, $"'{Path.GetFileName(path)}' height");
                }
            }

            Assert.Greater(seams, 0,
                "no seam decals — the two-material frontier is half of what seat/ is for.");

            var stems = seats.Decals.Select(d => d.Stem).ToList();
            CollectionAssert.AllItemsAreUnique(stems, "two decals share a file name.");

            var onDisk = Directory.GetFiles(SeatDir, "*.png")
                                  .Select(Path.GetFileNameWithoutExtension)
                                  .Where(s => !s.EndsWith("_blend", StringComparison.Ordinal))
                                  .OrderBy(s => s, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEquivalent(stems.OrderBy(s => s, StringComparer.Ordinal).ToArray(),
                onDisk, "seats.json and the seat/ folder do not list the same decals.");
        }

        // =====================================================================================
        // the slicer, without an importer
        // =====================================================================================

        /// <summary>
        /// The grid the slicer would write: 24 rects, row-major from the TOP-LEFT, geometry-only
        /// names, and this COLUMN's ground-contact pivot. Pure CPU — no importer is driven, which is
        /// what lets this run on CI.
        /// </summary>
        [Test]
        public void BuildRects_LaysOut24CellsWithPerColumnGroundPivots()
        {
            RockPxCatalog.Contract c = LoadContract();
            RockPxCatalog.FormEntry form = c.Find("skerry");
            RockPxCatalog.Sidecar sc = RockPxCatalog.LoadSidecar("skerry");
            List<RockPxCatalog.VariantEntry> variants = sc.For(form.NativeStone);
            Assert.IsNotNull(variants, "skerry's native stone has no sidecar block.");

            const string stem = "Skerry_basalt";
            SpriteRect[] rects = RockPxSheetSlicer.BuildRects(stem, form, variants);

            Assert.AreEqual(Cols * Rows, rects.Length);
            CollectionAssert.AllItemsAreUnique(rects.Select(r => r.name).ToList());
            CollectionAssert.AllItemsAreUnique(rects.Select(r => r.spriteID).ToList());

            for (int r = 0; r < Rows; r++)
            for (int col = 0; col < Cols; col++)
            {
                SpriteRect sr = rects[r * Cols + col];

                Assert.AreEqual($"{stem}_c{col}_r{r}", sr.name,
                    "names state GEOMETRY only — which variant a column draws and which tide a row " +
                    "is live in the catalog, because a name that claims a meaning can lie.");
                Assert.AreEqual(col, RockPxSheetSlicer.ColumnOf(sr.name, stem));
                Assert.AreEqual(r, RockPxSheetSlicer.RowOf(sr.name, stem));

                // Row 0 is the TOP row, and Unity's rects are bottom-origin.
                Assert.AreEqual(col * form.CellW, (int)sr.rect.x, $"{sr.name} x");
                Assert.AreEqual((Rows - 1 - r) * form.CellH, (int)sr.rect.y, $"{sr.name} y");
                Assert.AreEqual(form.CellW, (int)sr.rect.width);
                Assert.AreEqual(form.CellH, (int)sr.rect.height);

                Assert.AreEqual(SpriteAlignment.Custom, sr.alignment,
                    "a per-column pivot cannot be expressed by a named alignment.");

                RockPxCatalog.VariantEntry v = variants[col % VariantsPerForm];
                Vector2 want = RockPxCatalog.NormalizedPivot(v, form.CellW, form.CellH, col);
                Assert.AreEqual(want.x, sr.pivot.x, 1e-5f, $"{sr.name} pivot x");
                Assert.AreEqual(want.y, sr.pivot.y, 1e-5f, $"{sr.name} pivot y");
                Assert.AreNotEqual(0f, sr.pivot.y,
                    $"{sr.name} pivots on the cell floor — that is the BottomCenter mistake.");
            }
        }

        /// <summary>Stems and channel suffixes round-trip, and <c>bare</c> stays unsuffixed.</summary>
        [Test]
        public void Stems_RoundTripThroughTheChannelSuffixes()
        {
            Assert.AreEqual("Erratic_granite", RockPxCatalog.StemFor("erratic", "granite", "bare"));
            Assert.AreEqual("Erratic_granite_weeded",
                            RockPxCatalog.StemFor("erratic", "granite", "weeded"));
            Assert.AreEqual(TexDir + "Spine_till_barnacled_mask.png",
                            RockPxCatalog.SheetPath("spine", "till", "barnacled",
                                                    RockPxCatalog.Channel.Mask));

            foreach (RockPxCatalog.Channel ch in RockPxCatalog.SheetChannels)
                Assert.AreEqual(ch, RockPxCatalog.ChannelOf("Erratic_granite" +
                                                            RockPxCatalog.SuffixFor(ch)));
            Assert.AreEqual(RockPxCatalog.Channel.Blend,
                            RockPxCatalog.ChannelOf("KnuckleA_skirt_grass_sand_blend"));

            Assert.IsTrue(RockPxCatalog.IsColourChannel(RockPxCatalog.Channel.Albedo));
            Assert.IsTrue(RockPxCatalog.IsColourChannel(RockPxCatalog.Channel.Unlit));
            Assert.IsFalse(RockPxCatalog.IsColourChannel(RockPxCatalog.Channel.Mask));
            Assert.IsFalse(RockPxCatalog.IsColourChannel(RockPxCatalog.Channel.Normal));
            Assert.IsFalse(RockPxCatalog.IsColourChannel(RockPxCatalog.Channel.Blend));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => RockPxCatalog.StemFor("erratic", "marble", "bare"));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => RockPxCatalog.StemFor("boulder", "granite", "bare"));
        }

        // =====================================================================================
        // the import lock
        // =====================================================================================

        /// <summary>
        /// The lock's data-channel rule, which is what keeps a FRESH CLONE honest: a brand-new PNG
        /// has no <c>.meta</c>, so <c>ArtImportPipeline.OnPreprocessTexture</c> is the only thing
        /// that ever imports it on a CI machine — long before any slicer menu is touched.
        /// </summary>
        [Test]
        public void ImportLock_TreatsMaskNormalAndBlendAsData()
        {
            Assert.IsFalse(ArtImportPipeline.IsDataChannel(
                TexDir + "Erratic_granite.png"), "the albedo is a picture.");
            Assert.IsFalse(ArtImportPipeline.IsDataChannel(
                TexDir + "Erratic_granite_unlit.png"), "the unlit sheet is a picture too.");
            Assert.IsTrue(ArtImportPipeline.IsDataChannel(TexDir + "Erratic_granite_mask.png"));
            Assert.IsTrue(ArtImportPipeline.IsDataChannel(TexDir + "Erratic_granite_normal.png"));
            Assert.IsTrue(ArtImportPipeline.IsDataChannel(
                SeatDir + "KnuckleA_skirt_grass_sand_blend.png"));

            // The tree kit's channels answer the same way — one rule, not a per-kit special case.
            Assert.IsTrue(ArtImportPipeline.IsDataChannel(
                "Assets/_Project/Art/Foliage/Trees/Spruce_mature_summer_mask.png"));

            Assert.AreEqual(32f, ArtImportPipeline.PixelsPerUnit,
                "the locked scale the kit is authored at.");
            Assert.AreEqual(1024, RockPxCatalog.ImportSizeCap,
                "the widest sheet is 976 px and the tallest 258 px; 1024 clears both without " +
                "leaving room for a sheet to grow past the cap unnoticed.");
        }

        // =====================================================================================
        // helpers
        // =====================================================================================

        static RockPxCatalog.Contract LoadContract()
        {
            RockPxCatalog.Contract c = RockPxCatalog.Load();
            Assert.IsNotNull(c, $"'{ContractPath}' failed to read — see the logged error.");
            return c;
        }

        static string[] AllJsonPaths()
        {
            var paths = new List<string> { ContractPath, SeatsPath };
            paths.AddRange(Directory.GetFiles(SidecarDir, "*.json").OrderBy(p => p, StringComparer.Ordinal));
            return paths.ToArray();
        }

        static void ForEachVariant(Action<string, string, RockPxCatalog.VariantEntry> body)
        {
            RockPxCatalog.Contract c = LoadContract();
            int seen = 0;

            foreach (RockPxCatalog.FormEntry f in c.Forms)
            {
                RockPxCatalog.Sidecar sc = RockPxCatalog.LoadSidecar(f.Key);
                Assert.IsNotNull(sc, $"{RockPxCatalog.SidecarPath(f.Key)} failed to read.");

                foreach (var kv in sc.Stones)
                foreach (RockPxCatalog.VariantEntry v in kv.Value)
                {
                    body(f.Key, kv.Key, v);
                    seen++;
                }
            }

            Assert.Greater(seen, 0, "no variants were examined — the sweep is measuring nothing.");
        }

        static void InCell(string who, string what, Vector2Int p, int w, int h)
        {
            Assert.That(p.x, Is.InRange(0, w), $"{who} {what}.x = {p.x} is outside a {w}px cell.");
            Assert.That(p.y, Is.InRange(0, h), $"{who} {what}.y = {p.y} is outside a {h}px cell.");
        }

        /// <summary>Alpha coverage of one PNG, row-major as <c>GetPixels32</c> returns it. Decoded on
        /// the CPU — CI has no graphics device.</summary>
        static bool[] Coverage(string path, out int w, out int h)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path), markNonReadable: false),
                    $"'{path}' did not decode.");
                w = tex.width;
                h = tex.height;
                Color32[] px = tex.GetPixels32();
                var covered = new bool[px.Length];
                for (int i = 0; i < px.Length; i++) covered[i] = px[i].a > 0;
                return covered;
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        /// <summary>Width/height from the PNG's IHDR — no decode, so the 900-sheet dimension sweep
        /// costs 24 bytes a file instead of a megabyte.</summary>
        static Vector2Int PngSize(string path)
        {
            using var fs = File.OpenRead(path);
            var head = new byte[24];
            Assert.AreEqual(24, fs.Read(head, 0, 24), $"'{path}' is too short to be a PNG.");
            Assert.AreEqual(0x89, head[0], $"'{path}' is not a PNG.");
            int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            int h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
            return new Vector2Int(w, h);
        }

        static string Sha256Lf(byte[] bytes)
        {
            // Normalise CRLF → LF so the hash is the one git stores, on either platform.
            var lf = new List<byte>(bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == (byte)'\r' && i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n') continue;
                lf.Add(bytes[i]);
            }
            using var sha = System.Security.Cryptography.SHA256.Create();
            return string.Concat(sha.ComputeHash(lf.ToArray()).Select(b => b.ToString("x2")));
        }
    }
}
