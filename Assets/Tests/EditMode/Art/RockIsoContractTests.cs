using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the Rock Iso kit's CONTRACT — <c>Art/Sprites/Shore/Rock/RockIso.json</c> — and the
    /// catalog and slicer that read it.
    ///
    /// <para><b>This kit ships no pixels, so the contract is the whole deliverable.</b> Six species ×
    /// four stones × three tides is 72 sheets; the rig bakes them to order and the sidecar exists so
    /// the engine can be wired before a single PNG does. That makes these tests unusual in this
    /// suite: they are what make the bake trustworthy BEFORE it runs, the same division
    /// <c>CharacterRigBakeTests</c> draws against <c>CharacterIsoSheetSliceTests</c>. The moment
    /// sheets land, <see cref="RockSheetSlicer.VerifyAll"/> takes over.</para>
    ///
    /// <para>Every dimension and axis below is restated as a LITERAL from the drop's README and
    /// checked against a fresh read of the live sidecar — asserting the catalog against the catalog
    /// is the self-referential blind spot that let mirrored boat art ship.</para>
    ///
    /// <para>No water is asserted anywhere, because the kit bakes none (ADR 0010/0012/0023). An
    /// <c>awash</c> sheet is CLIPPED at the sea plane and a tide pool is an empty basin plus a rect
    /// the shader fills. Nothing here touches the GPU.</para>
    /// </summary>
    public class RockIsoContractTests
    {
        // ---- the drop's README, restated as literals -------------------------------------------

        private const string RockRoot = "Assets/_Project/Art/Sprites/Shore/Rock/";
        private const string ContractPath = RockRoot + "RockIso.json";
        private const string RigPath = "docs/art/rigs/rockIsoRig.js";
        private const string BakeHarnessPath = "docs/art/rigs/_rockBake.js";
        private const int Ppu = 32;

        private static readonly string[] Stones = { "sandstone", "granite", "basalt", "quartzite" };
        private static readonly string[] Tides = { "dry", "wet", "awash" };
        private static readonly string[] Dress = { "bare", "barnacled", "weeded" };

        /// <summary>Species → (cellW, cellH, variants), verbatim from the README's species table.</summary>
        private static readonly (string key, int w, int h, int variants)[] SpeciesTable =
        {
            ("Erratic",   52, 44, 4),
            ("Outcrop",   88, 60, 3),
            ("PoolLedge", 80, 48, 3),
            ("Skerry",   104, 52, 4),
            ("Cloven",    60, 76, 3),
            ("Cobble",    52, 32, 3),
        };

        private const int TotalVariants = 20;

        /// <summary>The camera the whole fleet and both terrain kits share (ADR 0006/0022).</summary>
        private const float CameraElev = 40f, DepthSquash = 0.643f, HeightScale = 0.766f;

        private static string RepoPath(string relative) =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, relative);

        private static RockIsoCatalog.Contract Load()
        {
            var c = RockIsoCatalog.Load();
            Assert.IsNotNull(c, $"'{ContractPath}' failed to load/parse.");
            return c;
        }

        private static IEnumerable<(RockIsoCatalog.SpeciesEntry s, RockIsoCatalog.VariantEntry v)>
            AllVariants(RockIsoCatalog.Contract c) =>
                c.Species.SelectMany(s => s.Variants.Select(v => (s, v)));

        // =====================================================================================
        // 1. the kit is here and says what the README says
        // =====================================================================================

        [Test]
        public void TheContractAndBothRigSources_AreVersionedInTheRepo()
        {
            Assert.IsTrue(File.Exists(ContractPath), $"'{ContractPath}' is missing.");
            Assert.IsTrue(File.Exists(RepoPath(RigPath)), $"'{RigPath}' is missing.");
            Assert.IsTrue(File.Exists(RepoPath(BakeHarnessPath)),
                          $"'{BakeHarnessPath}' is missing — it is the recipe the C# baker mirrors.");
        }

        [Test]
        public void TheContract_NamesTheKitAndTheSharedCamera()
        {
            var c = Load();
            Assert.AreEqual(RockIsoCatalog.KitName, c.Kit, "the contract is not the Rock Iso kit");
            Assert.AreEqual(Ppu, c.Ppu, "PPU — 32 px is 1 m, the locked scale");

            // The same camera as the boat turntables, the tree bake and ShoreIso2 — that is what lets
            // a rock, a cliff toe and a hull sit in one space.
            Assert.AreEqual(CameraElev, c.CameraElev, 1e-3f, "camera elevation");
            Assert.AreEqual(DepthSquash, c.DepthSquash, 1e-3f, "depth squash");
            Assert.AreEqual(HeightScale, c.HeightScale, 1e-3f, "height scale");
        }

        [Test]
        public void TheContract_PromisesZeroBakedWater()
        {
            // The shader owns the sea, foam, spray and the tide-pool fill. An `awash` sheet is
            // clipped at the sea plane, not flooded.
            StringAssert.StartsWith("none baked", Load().Water,
                "the contract no longer promises zero baked water");
        }

        [Test]
        public void TheContract_PublishesTheAxesTheReadmeClaims()
        {
            var c = Load();
            CollectionAssert.AreEqual(Stones, c.Stones, "stones");
            CollectionAssert.AreEqual(Tides, c.Tides, "tides");
            CollectionAssert.AreEqual(Dress, c.Dress, "dress");

            CollectionAssert.AreEqual(Stones, RockIsoCatalog.Stones, "catalog stones");
            CollectionAssert.AreEqual(Tides, RockIsoCatalog.Tides, "catalog tides");
            CollectionAssert.AreEqual(Dress, RockIsoCatalog.Dress, "catalog dress");
            CollectionAssert.AreEqual(SpeciesTable.Select(t => t.key).ToArray(),
                                      RockIsoCatalog.SpeciesKeys, "catalog species");
        }

        [Test]
        public void EverySpecies_HasTheCellAndVariantCountTheReadmeClaims()
        {
            var c = Load();
            Assert.AreEqual(SpeciesTable.Length, c.Species.Count, "species count");
            Assert.AreEqual(TotalVariants, c.VariantCount, "total variants across the kit");

            var table = new StringBuilder("[rock] species table (contract vs README):\n");
            foreach (var want in SpeciesTable)
            {
                var s = c.Find(want.key);
                Assert.IsNotNull(s, $"'{want.key}' is missing from the contract.");
                Assert.AreEqual(want.w, s.CellW, $"{want.key} cell width");
                Assert.AreEqual(want.h, s.CellH, $"{want.key} cell height");
                Assert.AreEqual(want.variants, s.Variants.Count, $"{want.key} variant count");
                Assert.AreEqual(Dress.Length, s.SheetRows, $"{want.key} dress rows");
                table.AppendLine($"  {want.key,-10} cell {s.CellW,3}×{s.CellH,-3} " +
                                 $"sheet {s.SheetW,3}×{s.SheetH,-3} " +
                                 $"({s.SheetCols} variant × {s.SheetRows} dress)");
            }
            Debug.Log(table.ToString());
        }

        [Test]
        public void EverySheet_IsItsCellTimesItsGrid_AndFitsTheImportCap()
        {
            foreach (var s in Load().Species)
            {
                Assert.AreEqual(s.CellW * s.SheetCols, s.SheetW, $"{s.Key} sheet width");
                Assert.AreEqual(s.CellH * s.SheetRows, s.SheetH, $"{s.Key} sheet height");
                Assert.AreEqual(s.Variants.Count, s.SheetCols, $"{s.Key} cols == variants");

                // Over the cap Unity imports SILENTLY DOWNSCALED with the sprite COUNT still right.
                Assert.LessOrEqual(Math.Max(s.SheetW, s.SheetH), RockIsoCatalog.ImportSizeCap,
                    $"{s.Key} would bake over the {RockIsoCatalog.ImportSizeCap} px cap.");
            }
        }

        // =====================================================================================
        // 2. the pivot — the day-loser class
        // =====================================================================================

        [Test]
        public void EveryVariantsPivot_IsItsGroundContact_HorizontallyCentred()
        {
            // The rig computes `pivot` and `footprint.ground` from the same expression, so they must
            // never diverge — if they ever do, one of the two consumers is reading a stale field.
            foreach (var (s, v) in AllVariants(Load()))
            {
                Assert.AreEqual(v.Ground, v.Pivot,
                    $"{v.Id}: pivot and footprint.ground must be the same point.");
                Assert.AreEqual(s.CellW / 2, v.Pivot.x,
                    $"{v.Id}: the ground contact is horizontally centred in its cell.");
            }
        }

        [Test]
        public void EveryVariant_DrawsNearFlankBelowItsGroundContact()
        {
            // ⭐ THE REASON THE PIVOT RULE EXISTS. Under the 40° camera the near flank projects BELOW
            // the ground contact, so every cell carries rows underneath the pivot. A bottom-centre
            // pivot — the obvious default, and what ArtImportPipeline would pick — floats the rock by
            // exactly that overhang and reads as an art bug rather than an import bug.
            var c = Load();
            var table = new StringBuilder("[rock] ground overhang (bottom-centre would FLOAT by this):\n");
            float min = float.MaxValue, max = 0f;

            foreach (var (s, v) in AllVariants(c))
            {
                int px = RockIsoCatalog.GroundOverhangPx(s, v);
                float m = RockIsoCatalog.GroundOverhangMetres(s, v);
                Assert.Greater(px, 0,
                    $"{v.Id}: the ground contact must sit ABOVE the cell floor, else there is no " +
                    "near flank and the whole pivot rule is moot.");
                min = Mathf.Min(min, m);
                max = Mathf.Max(max, m);
                table.AppendLine($"  {v.Id,-12} {px,2} px = {m:F3} m");
            }

            table.AppendLine($"  range {min:F3}–{max:F3} m");
            Debug.Log(table.ToString());

            // The sabotage curve, stated as a range so a re-bake that changes the volumes cannot slip
            // through. If it ever collapses toward zero the trap has genuinely gone away.
            Assert.Greater(min, 0.15f,
                "Even the shallowest overhang must be a visible error, or this warning is noise.");
            Assert.That(max, Is.InRange(0.55f, 0.85f),
                "The worst variant floated 22 px = 0.688 m. If that moved, the bake changed.");
        }

        [Test]
        public void TheNormalizedPivot_IsAdr0026sFormula_NotTheTreeKitsPad()
        {
            // ⚠ SETTLED, do not re-derive (ADR 0026 / #300). `anchors.pivot` is a CONTINUOUS
            // top-left-origin coordinate and the contract publishes no `pad`, so the y term is
            // (H − y)/H — the same formula as RigGeometry.UnityNormalisedPivot. The tree kit's
            // pad/cellH is one row lower and equally correct BECAUSE its rig defines pad itself and
            // the same fraction doubles as the wind shader's _TrunkAnchor. Read the quantity the
            // contract publishes and use exactly that.
            foreach (var (s, v) in AllVariants(Load()))
            {
                Vector2 n = RockIsoCatalog.NormalizedPivot(s, v);
                Assert.AreEqual(v.Pivot.x / (float)s.CellW, n.x, 1e-5f, $"{v.Id} pivot x");
                Assert.AreEqual((s.CellH - v.Pivot.y) / (float)s.CellH, n.y, 1e-5f, $"{v.Id} pivot y");

                // Horizontally centred, and strictly inside the cell.
                Assert.AreEqual(0.5f, n.x, 1e-5f, $"{v.Id} pivot must be centred");
                Assert.That(n.y, Is.GreaterThan(0f).And.LessThan(1f), $"{v.Id} pivot y in-cell");

                // And in px-from-the-bottom, which is what Sprite.pivot reports.
                Vector2 px = RockIsoCatalog.PivotPixels(s, v);
                Assert.AreEqual(RockIsoCatalog.GroundOverhangPx(s, v), px.y, 1e-4f,
                                $"{v.Id}: pivot px from the bottom IS the overhang");
            }
        }

        // =====================================================================================
        // 3. the anchors mean what the README says they mean
        // =====================================================================================

        [Test]
        public void Hazard_IsPresentExactlyWhenTheRockIsAwash()
        {
            // "hazard: awash danger radius in m (waterline > 0 only)". A hazard on a dry rock would
            // put a danger ring on the beach; a missing one on an awash rock is an unmarked hull-opener.
            var awash = new List<string>();
            foreach (var (_, v) in AllVariants(Load()))
            {
                Assert.AreEqual(v.IsAwash, v.HazardRM.HasValue,
                    $"{v.Id}: waterline {v.Waterline} but hazard " +
                    $"{(v.HazardRM.HasValue ? "present" : "absent")}.");
                if (v.IsAwash)
                {
                    awash.Add(v.Id);
                    Assert.Greater(v.HazardRM.Value, 0f, $"{v.Id}: a hazard radius of 0 marks nothing.");
                }
            }
            Assert.AreEqual(7, awash.Count, "the kit ships seven awash variants");
            Debug.Log($"[rock] awash (hazard-bearing): {string.Join(", ", awash)}");
        }

        [Test]
        public void ATidePoolRect_BelongsToPoolLedgeAlone_AndFitsInsideItsCell()
        {
            // The basin is baked EMPTY and the rect is the shader's fill target — the kit bakes no
            // water. A rect that ran outside its cell would fill over the neighbouring variant.
            foreach (var (s, v) in AllVariants(Load()))
            {
                if (s.Key != "PoolLedge")
                {
                    Assert.IsFalse(v.Pool.HasValue, $"{v.Id}: only PoolLedge carries a tide pool.");
                    continue;
                }

                Assert.IsTrue(v.Pool.HasValue, $"{v.Id}: a PoolLedge without a basin rect.");
                RectInt r = v.Pool.Value;
                Assert.That(r.xMin, Is.GreaterThanOrEqualTo(0), $"{v.Id} pool x");
                Assert.That(r.yMin, Is.GreaterThanOrEqualTo(0), $"{v.Id} pool y");
                Assert.That(r.xMax, Is.LessThanOrEqualTo(s.CellW), $"{v.Id} pool runs past the cell");
                Assert.That(r.yMax, Is.LessThanOrEqualTo(s.CellH), $"{v.Id} pool runs past the cell");
                Assert.Greater(v.PoolDepthM, 0f, $"{v.Id}: a basin with no depth is not a basin.");
            }
        }

        [Test]
        public void PerchFlat_IsAPerVariantFlag_NotAPerSpeciesOne()
        {
            // ⚠ The README says "Cobble and most Skerry builds report perch.flat = false", which reads
            // like a species rule. Measured, it is not one: ALL four Skerry are false, Cobble is 2 of
            // 3, and PoolLedgeC is flat too. A gull/fox spawner that keyed off the species would put
            // a bird on a rounded boulder and skip the one flat cobble. Honour the FLAG.
            var flat = AllVariants(Load()).Where(t => t.v.PerchFlat).Select(t => t.v.Id).ToArray();
            CollectionAssert.AreEquivalent(new[] { "PoolLedgeC", "CobbleB" }, flat,
                "exactly two variants in the whole kit are standable");

            foreach (var (_, v) in AllVariants(Load()))
                Assert.That(v.PerchZM, Is.LessThanOrEqualTo(v.TopM + 1e-4f),
                            $"{v.Id}: the perch cannot stand above the rock's own top.");

            Debug.Log($"[rock] standable (perch.flat): {string.Join(", ", flat)} — " +
                      "every other variant is decorative.");
        }

        [Test]
        public void SnagsAreAtLeastSixtyDegreesApart_MeasuredInTHEGROUNDPLANE()
        {
            // ⭐ MEASURE THIS IN THE RIGHT SPACE OR IT LIES. The README promises snags "≥60° apart";
            // measured naively in SCREEN pixels, 9 of 19 variants appear to violate it (worst 42.5°).
            // They do not — the iso camera squashes depth by 0.643, so a screen angle is not a world
            // angle. Undo the squash and the true minimum is 60.6°, which is the bar holding by half
            // a degree across the whole kit. That margin is itself the evidence the rig enforces it
            // deliberately, and it is why this test divides by depthSquash.
            var c = Load();
            float worst = 360f;
            string worstId = null;

            foreach (var (_, v) in AllVariants(c))
            {
                if (v.Snags.Count < 2) continue;
                float sep = MinSeparationDegrees(v, c.DepthSquash);
                if (sep < worst) { worst = sep; worstId = v.Id; }
            }

            Debug.Log($"[rock] tightest snag separation in the ground plane: {worst:F1}° ({worstId}). " +
                      "In raw screen px the same pair measures under 45°, which is the wrong space.");
            Assert.GreaterOrEqual(worst, 60f,
                $"{worstId}: snags {worst:F1}° apart in the ground plane — under the kit's own 60° " +
                "rule, so two rope catches sit close enough to read as one.");
        }

        /// <summary>Smallest angle between any two snags about the ground contact, measured in the
        /// GROUND PLANE (screen depth un-squashed).</summary>
        private static float MinSeparationDegrees(RockIsoCatalog.VariantEntry v, float depthSquash)
        {
            var angles = v.Snags
                .Select(s => Mathf.Atan2((s.y - v.Ground.y) / depthSquash, s.x - v.Ground.x)
                             * Mathf.Rad2Deg)
                .ToList();

            float min = 360f;
            for (int i = 0; i < angles.Count; i++)
                for (int j = i + 1; j < angles.Count; j++)
                {
                    float d = Mathf.Abs(Mathf.DeltaAngle(angles[i], angles[j]));
                    min = Mathf.Min(min, d);
                }
            return min;
        }

        /// <summary>
        /// ⚠ A DOCUMENTED DEVIATION, flagged to the art director rather than patched
        /// (<c>docs/art/**</c> is his lane). The README promises "2–3" snags per variant; nineteen of
        /// the twenty carry exactly three and <b>SkerryD carries one</b>. One catch cannot hold a pot
        /// line the way two can, so this is a real gap and not a rounding of the prose.
        ///
        /// <para>This test asserts the CURRENT state deliberately: if a re-bake gives SkerryD its
        /// second and third snags, this goes red and someone deletes it — which is the point. It is a
        /// canary, not a blessing.</para>
        /// </summary>
        [Test]
        public void SnagCounts_AreThreeEverywhereExceptSkerryD_TheFlaggedDeviation()
        {
            var c = Load();
            var byCount = AllVariants(c).ToLookup(t => t.v.Snags.Count, t => t.v.Id);

            CollectionAssert.AreEquivalent(new[] { "SkerryD" }, byCount[1].ToArray(),
                "SkerryD is the kit's ONE under-strength variant — if this list changed, either the " +
                "deviation was fixed (delete this test) or a new one appeared (flag it).");
            Assert.AreEqual(TotalVariants - 1, byCount[3].Count(),
                "every other variant carries the full three snags");

            foreach (var (_, v) in AllVariants(c))
                Assert.That(v.Snags.Count, Is.InRange(1, 3), $"{v.Id}: snag count out of range");

            Debug.Log("[rock] ⚠ FLAGGED: SkerryD ships 1 snag where the README promises 2–3. " +
                      "Reported to the art director; not patched here.");
        }

        // =====================================================================================
        // 4. the catalog's own arithmetic
        // =====================================================================================

        [Test]
        public void Catalog_NamesSheetsTheWayTheBakeHarnessDoes()
        {
            Assert.AreEqual("Erratic_sandstone_dry", RockIsoCatalog.StemFor("Erratic", "sandstone", "dry"));
            Assert.AreEqual(RockRoot + "Skerry_basalt_awash.png",
                            RockIsoCatalog.SheetPath("Skerry", "basalt", "awash"));

            // 6 species × 4 stones × 3 tides — the full 72 the README quotes.
            var all = RockIsoCatalog.AllSheetStems().ToArray();
            Assert.AreEqual(72, all.Length, "the full bake is 72 sheets");
            Assert.AreEqual(72, all.Distinct().Count(), "every stem is unique");
        }

        [Test]
        public void Catalog_RejectsAnUnknownSpeciesStoneTideOrDressRow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RockIsoCatalog.StemFor("Boulder", "sandstone", "dry"));
            Assert.Throws<ArgumentOutOfRangeException>(() => RockIsoCatalog.StemFor("Erratic", "marble", "dry"));
            Assert.Throws<ArgumentOutOfRangeException>(() => RockIsoCatalog.StemFor("Erratic", "sandstone", "flooded"));

            var c = Load();
            var s = c.Find("Erratic");
            Assert.Throws<ArgumentOutOfRangeException>(() => RockIsoCatalog.CellRect(s, s.Variants[0], 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => RockIsoCatalog.CellRect(s, s.Variants[0], -1));
        }

        [Test]
        public void CellRects_TileTheirSheetExactly_WithNoOverlapAndNoGap()
        {
            // variant COLS × dress ROWS. If this ever drifted, every anchor would still be "right"
            // and would point at the wrong cell.
            foreach (var s in Load().Species)
            {
                var seen = new HashSet<string>();
                int area = 0;
                for (int d = 0; d < s.SheetRows; d++)
                foreach (var v in s.Variants)
                {
                    RectInt r = RockIsoCatalog.CellRect(s, v, d);
                    Assert.IsTrue(seen.Add($"{r.x},{r.y}"), $"{s.Key}: two cells share {r.x},{r.y}");
                    Assert.AreEqual(v.Col * s.CellW, r.x, $"{v.Id} col {v.Col} x");
                    Assert.AreEqual(d * s.CellH, r.y, $"{v.Id} dress row {d} y");
                    Assert.That(r.xMax, Is.LessThanOrEqualTo(s.SheetW), $"{v.Id} runs past the sheet");
                    Assert.That(r.yMax, Is.LessThanOrEqualTo(s.SheetH), $"{v.Id} runs past the sheet");
                    area += r.width * r.height;
                }
                Assert.AreEqual(s.SheetW * s.SheetH, area, $"{s.Key}: cells must tile the sheet exactly");
            }
        }

        [Test]
        public void EveryVariantsColumn_IsItsIndex_SoTheAnchorsAddressTheRightCell()
        {
            // `col` is what RockIso.json numbers its anchors against. If a variant's col ever stopped
            // being its position in the list, the slicer would hand column 2's pivot to column 1.
            foreach (var s in Load().Species)
                for (int i = 0; i < s.Variants.Count; i++)
                {
                    Assert.AreEqual(i, s.Variants[i].Col, $"{s.Variants[i].Id}: col must be its index");
                    Assert.That(s.Variants[i].DressRow, Is.InRange(0, Dress.Length - 1),
                                $"{s.Variants[i].Id}: dressRow outside the sheet");
                }
        }

        [Test]
        public void Slicer_BuildsOneRectPerCell_WithThisVariantsPivot()
        {
            // The slicer is what actually writes those pivots into the .meta, so exercise it directly
            // rather than trusting that it reads the catalog the same way this test does.
            var c = Load();
            var s = c.Find("Erratic");
            const string stem = "Erratic_sandstone_dry";

            var rects = RockSheetSlicer.BuildRects(stem, s);
            Assert.AreEqual(s.SheetCols * s.SheetRows, rects.Length, "one rect per cell");

            foreach (var v in s.Variants)
            for (int d = 0; d < s.SheetRows; d++)
            {
                var r = rects.Single(x => x.name == $"{stem}_v{v.Col}_d{d}");
                Assert.AreEqual(v.Col * s.CellW, r.rect.x, 1e-4f, $"{r.name} x");
                // Row 0 is the sheet's TOP, and Unity rects are bottom-origin.
                Assert.AreEqual((s.SheetRows - 1 - d) * s.CellH, r.rect.y, 1e-4f, $"{r.name} y");
                Assert.AreEqual(s.CellW, r.rect.width, 1e-4f, $"{r.name} width");
                Assert.AreEqual(s.CellH, r.rect.height, 1e-4f, $"{r.name} height");

                Vector2 want = RockIsoCatalog.NormalizedPivot(s, v);
                Assert.AreEqual(want.x, r.pivot.x, 1e-5f, $"{r.name} pivot x");
                Assert.AreEqual(want.y, r.pivot.y, 1e-5f, $"{r.name} pivot y");
                Assert.AreEqual(v.Col, RockSheetSlicer.VariantColumnOf(r.name, stem),
                                $"{r.name}: the name must round-trip to its variant column");
            }
        }

        [Test]
        public void Slicer_OnlyClaimsSheetsTheContractNames()
        {
            var c = Load();
            Assert.IsNotNull(RockSheetSlicer.SpeciesForStem(c, "Cloven_quartzite_wet"));
            Assert.IsNull(RockSheetSlicer.SpeciesForStem(c, "Cloven_marble_wet"), "unknown stone");
            Assert.IsNull(RockSheetSlicer.SpeciesForStem(c, "Boulder_sandstone_dry"), "unknown species");
            Assert.IsNull(RockSheetSlicer.SpeciesForStem(c, "Cloven_sandstone"), "not a sheet name");
            Assert.IsNull(RockSheetSlicer.SpeciesForStem(c, "_preview-rock"), "a doc preview");
        }

        // =====================================================================================
        // 5. THE SABOTAGES
        // =====================================================================================

        /// <summary>
        /// Sabotage the pivot: bottom-centre is the obvious wrong default — it is what
        /// <c>ArtImportPipeline</c> picks for standing art, and what a manifest row would give this
        /// kit. Measure what it would cost, per variant.
        /// </summary>
        [Test]
        public void Sabotage_BottomCentre_WouldFloatEveryVariant()
        {
            var c = Load();
            var table = new StringBuilder("[rock] SABOTAGE — bottom-centre pivot:\n");
            int floated = 0;
            float worst = 0f;

            foreach (var (s, v) in AllVariants(c))
            {
                Vector2 correct = RockIsoCatalog.NormalizedPivot(s, v);
                var bottomCentre = new Vector2(0.5f, 0f);
                float errM = Mathf.Abs(correct.y - bottomCentre.y) * s.CellH / Ppu;

                Assert.AreNotEqual(bottomCentre.y, correct.y,
                    $"{v.Id}: if bottom-centre were correct this whole rule would be unnecessary.");
                if (errM > 0.01f) floated++;
                worst = Mathf.Max(worst, errM);
                table.AppendLine($"  {v.Id,-12} floats {errM:F3} m");
            }

            table.AppendLine($"  {floated}/{c.VariantCount} visibly wrong, worst {worst:F3} m");
            Debug.Log(table.ToString());
            Assert.AreEqual(c.VariantCount, floated,
                "SABOTAGE NOT DETECTED — bottom-centre must be visibly wrong for EVERY variant.");
        }

        /// <summary>
        /// Sabotage the slicer's per-variant pivot: give the whole sheet ONE pivot — the thing a
        /// <c>SpriteSheetSlicer</c> manifest row would do — and measure how far it plants the other
        /// columns out.
        /// </summary>
        [Test]
        public void Sabotage_OnePivotForTheWholeSheet_MisplantsTheOtherColumns()
        {
            var c = Load();
            var table = new StringBuilder("[rock] SABOTAGE — one pivot per sheet (a manifest row):\n");
            int wrong = 0, total = 0;

            foreach (var s in c.Species)
            {
                // Pretend the sheet takes column 0's pivot for everything.
                Vector2 shared = RockIsoCatalog.NormalizedPivot(s, s.Variants[0]);
                foreach (var v in s.Variants.Skip(1))
                {
                    total++;
                    Vector2 correct = RockIsoCatalog.NormalizedPivot(s, v);
                    float errM = Mathf.Abs(correct.y - shared.y) * s.CellH / Ppu;
                    if (errM > 1f / Ppu) { wrong++; table.AppendLine($"  {v.Id,-12} off by {errM:F3} m"); }
                }
            }

            table.AppendLine($"  {wrong}/{total} non-first columns misplanted by more than a pixel");
            Debug.Log(table.ToString());
            Assert.Greater(wrong, 0,
                "SABOTAGE NOT DETECTED — if one pivot served every column, this kit would not need " +
                "its own slicer and RockSheetSlicer should be deleted in favour of a manifest row.");
        }

        /// <summary>
        /// Sabotage the snag-separation measurement itself: measured in RAW SCREEN pixels the same
        /// contract appears to be broken in nine places. This proves the ground-plane correction is
        /// load-bearing and not decoration — and stops a future reader "simplifying" it away.
        /// </summary>
        [Test]
        public void Sabotage_MeasuringSnagsInScreenSpace_InventsNineViolations()
        {
            var c = Load();
            int screenViolations = 0;
            float worstScreen = 360f;

            foreach (var (_, v) in AllVariants(c))
            {
                if (v.Snags.Count < 2) continue;
                float screen = MinSeparationDegrees(v, depthSquash: 1f);   // the WRONG space
                if (screen < 60f) screenViolations++;
                worstScreen = Mathf.Min(worstScreen, screen);
            }

            Debug.Log($"[rock] SABOTAGE — snags measured in screen px: {screenViolations} apparent " +
                      $"violations, worst {worstScreen:F1}°. Un-squashing depth by " +
                      $"{c.DepthSquash} clears all of them. The contract was never wrong; the " +
                      "naive measurement was.");

            Assert.AreEqual(9, screenViolations,
                "SABOTAGE NOT DETECTED — the screen-space measure is supposed to invent exactly " +
                "nine violations. If it stopped, either the anchors moved or the camera did.");
            Assert.Less(worstScreen, 60f, "the screen-space worst case must be under the bar");
        }
    }
}
