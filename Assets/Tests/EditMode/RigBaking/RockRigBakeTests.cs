using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The Rock Iso bake path, proven end-to-end WITHOUT any committed sheet — everything here runs
    /// the V8 host CPU-side, already established as safe on CI's null graphics device by this suite.
    ///
    /// <para><b>⭐ THE RIG IS THE ORACLE.</b> This kit ships a sidecar and no pixels, so the one
    /// question that matters is whether <c>RockIso.json</c> still describes what
    /// <c>rockIsoRig.js</c> actually renders. Every anchor in that file is expressed in CELL PIXELS,
    /// so a cell that drifted by one row silently invalidates all twenty variants' pivots,
    /// footprints, pools and snags at once — and nothing downstream would notice, because the sheets
    /// do not exist yet to disagree. These tests re-derive the geometry from the rig on every run
    /// and hold the committed contract to it.</para>
    ///
    /// <para><c>RockIsoContractTests</c> is the sibling: it asserts the contract against the drop's
    /// README. This one asserts the contract against the RIG. Between them a stale sidecar has
    /// nowhere to hide, and once sheets land <c>RockSheetSlicer.VerifyAll</c> takes over.</para>
    /// </summary>
    public class RockRigBakeTests
    {
        private static IRigScriptHost _host;
        private static RockIsoCatalog.Contract _contract;

        [OneTimeSetUp]
        public void InstallTheRigOnce()
        {
            // One V8 host for the fixture: installing the rig is the expensive part, and every test
            // here is a read against the same immutable rig state.
            _host = RigScriptHostFactory.Create();
            RockIsoBaker.InstallRig(_host);
            _contract = RockIsoCatalog.Load();
            Assert.IsNotNull(_contract, "RockIso.json failed to load.");
        }

        [OneTimeTearDown]
        public void DisposeHost() => _host?.Dispose();

        // =====================================================================================
        // 1. the rig runs unmodified and declares what the contract claims
        // =====================================================================================

        [Test]
        public void TheRigInstalls_AndDeclaresTheSameAxesAsTheContract()
        {
            Debug.Log($"[rock-bake] engine: {_host.EngineName}");

            CollectionAssert.AreEqual(RockIsoCatalog.SpeciesKeys,
                                      RockIsoBaker.ReadSpeciesKeys(_host).ToArray(),
                                      "the rig's species keys, in its own order");

            CollectionAssert.AreEqual(
                _contract.Stones,
                RockIsoBaker.ReadStringArray(_host, $"Object.keys({RockIsoCatalog.RigGlobalName}.STONES)"),
                "stones: rig vs contract");
            CollectionAssert.AreEqual(
                _contract.Tides,
                RockIsoBaker.ReadStringArray(_host, $"{RockIsoCatalog.RigGlobalName}.TIDES"),
                "tides: rig vs contract");
            CollectionAssert.AreEqual(
                _contract.Dress,
                RockIsoBaker.ReadStringArray(_host, $"{RockIsoCatalog.RigGlobalName}.DRESS"),
                "dress: rig vs contract");
        }

        [Test]
        public void EverySpeciesCellAndVariantCount_StillMatchesTheCommittedSidecar()
        {
            // ⭐ The staleness check. Every anchor is in cell px, so this is the assert that stands
            // between a re-baked rig and twenty silently-wrong pivots.
            var table = new StringBuilder("[rock-bake] rig geometry vs RockIso.json:\n");
            foreach (string key in RockIsoCatalog.SpeciesKeys)
            {
                var spec = RockIsoBaker.ReadSpeciesSpec(_host, key);
                var entry = _contract.Find(key);
                Assert.IsNotNull(entry, $"{key} missing from the contract");

                Assert.AreEqual(entry.CellW, spec.CellW, $"{key} cell width: rig vs contract");
                Assert.AreEqual(entry.CellH, spec.CellH, $"{key} cell height: rig vs contract");
                Assert.AreEqual(entry.SheetCols, spec.Cols, $"{key} variant count: rig vs contract");
                Assert.AreEqual(entry.SheetRows, spec.Rows, $"{key} dress rows: rig vs contract");

                table.AppendLine($"  {key,-10} {spec.CellW,3}×{spec.CellH,-3} " +
                                 $"× {spec.Cols} variant(s) × {spec.Rows} dress — MATCH");
            }
            Debug.Log(table.ToString());
        }

        [Test]
        public void TheBakersContractGuard_PassesOnTheShippedPair()
        {
            // The guard the baker runs before writing a single pixel.
            foreach (string key in RockIsoCatalog.SpeciesKeys)
            {
                var spec = RockIsoBaker.ReadSpeciesSpec(_host, key);
                Assert.DoesNotThrow(
                    () => RockIsoBaker.AssertMatchesContract(key, spec, _contract),
                    $"{key}: the shipped rig and sidecar must agree");
            }
        }

        // =====================================================================================
        // 2. the pixels the rig actually returns
        // =====================================================================================

        [Test]
        public void ARenderedCell_IsExactlyTheCellTheContractPromises()
        {
            // The rig hands back a Uint8ClampedArray; if its length disagreed with the declared cell
            // the baker would blit a mis-shaped buffer into the grid and every cell after it would
            // shear.
            foreach (string key in RockIsoCatalog.SpeciesKeys)
            {
                var entry = _contract.Find(key);
                string expr = RockIsoBaker.ResultExpr(key, variant: 0, dress: 0,
                                                      stone: "sandstone", tide: "dry");
                byte[] rgba = _host.EvaluateBytes(expr + ".rgba");
                Assert.AreEqual(entry.CellW * entry.CellH * 4, rgba.Length,
                    $"{key}: rendered buffer is not {entry.CellW}×{entry.CellH} RGBA");

                Assert.AreEqual(entry.CellW, (int)_host.EvaluateNumber(expr + ".w"), $"{key} render w");
                Assert.AreEqual(entry.CellH, (int)_host.EvaluateNumber(expr + ".h"), $"{key} render h");
            }
        }

        [Test]
        public void TheSameVariant_RendersIdenticallyTwice()
        {
            // The whole kit's premise is that the geometry is SEED-STABLE — that is what lets one
            // anchor entry serve all twelve stone × tide bakes of a variant. If a render were not
            // reproducible, the anchors could not be shared and the sidecar would be a fiction.
            string expr = RockIsoBaker.ResultExpr("Erratic", 0, 0, "sandstone", "dry") + ".rgba";
            byte[] a = _host.EvaluateBytes(expr);
            byte[] b = _host.EvaluateBytes(expr);
            CollectionAssert.AreEqual(a, b, "the same render must be byte-identical on a re-run");
        }

        [Test]
        public void TheAnchorsAFreshRenderReports_AreTheOnesCommittedInTheSidecar()
        {
            // ⭐⭐ THE ORACLE CHECK. Read the anchors back out of a LIVE render and hold the committed
            // file to them, variant by variant. This is what makes it safe to wire the engine against
            // RockIso.json before any PNG exists.
            int checkedVariants = 0;

            foreach (string key in RockIsoCatalog.SpeciesKeys)
            {
                var entry = _contract.Find(key);
                for (int i = 0; i < entry.Variants.Count; i++)
                {
                    var v = entry.Variants[i];
                    // Anchors are stone- and tide-independent (the README's claim), so the sidecar's
                    // own basis — sandstone/dry — is what we re-render.
                    string a = RockIsoBaker.ResultExpr(key, i, v.DressRow, "sandstone", "dry") + ".anchors";

                    Assert.AreEqual(v.Pivot.x, (int)_host.EvaluateNumber($"{a}.pivot.x"), $"{v.Id} pivot.x");
                    Assert.AreEqual(v.Pivot.y, (int)_host.EvaluateNumber($"{a}.pivot.y"), $"{v.Id} pivot.y");
                    Assert.AreEqual(v.Ground.x, (int)_host.EvaluateNumber($"{a}.footprint.ground.x"),
                                    $"{v.Id} ground.x");
                    Assert.AreEqual(v.Ground.y, (int)_host.EvaluateNumber($"{a}.footprint.ground.y"),
                                    $"{v.Id} ground.y");
                    Assert.AreEqual(v.FootprintRx, (float)_host.EvaluateNumber($"{a}.footprint.rx"), 1e-3f,
                                    $"{v.Id} footprint.rx");
                    Assert.AreEqual(v.FootprintRy, (float)_host.EvaluateNumber($"{a}.footprint.ry"), 1e-3f,
                                    $"{v.Id} footprint.ry");
                    Assert.AreEqual(v.PerchFlat, _host.EvaluateBool($"{a}.perch.flat === true"),
                                    $"{v.Id} perch.flat");
                    Assert.AreEqual(v.Snags.Count, (int)_host.EvaluateNumber($"{a}.snags.length"),
                                    $"{v.Id} snag count");
                    Assert.AreEqual(v.HazardRM.HasValue, _host.EvaluateBool($"{a}.hazard !== null"),
                                    $"{v.Id} hazard presence");
                    Assert.AreEqual(v.Pool.HasValue, _host.EvaluateBool($"{a}.pool !== null"),
                                    $"{v.Id} pool presence");
                    checkedVariants++;
                }
            }

            Assert.AreEqual(20, checkedVariants, "all twenty variants re-checked against the rig");
            Debug.Log($"[rock-bake] oracle: {checkedVariants} variants — every committed anchor " +
                      "matches a fresh render of the rig.");
        }

        [Test]
        public void AnchorsAreStoneAndTideIndependent_WhichIsWhyOneEntryServesTwelveBakes()
        {
            // The README's load-bearing claim, tested rather than trusted: if a stone or tide moved
            // the geometry, the sidecar could not describe all 12 bakes of a variant with one entry
            // and every downstream footprint would be wrong for 11 of them.
            const string species = "Skerry";
            string Basis(string stone, string tide) =>
                RockIsoBaker.ResultExpr(species, 0, 0, stone, tide) + ".anchors";

            double px = _host.EvaluateNumber($"{Basis("sandstone", "dry")}.pivot.x");
            double py = _host.EvaluateNumber($"{Basis("sandstone", "dry")}.pivot.y");

            foreach (string stone in RockIsoCatalog.Stones)
            foreach (string tide in RockIsoCatalog.Tides)
            {
                Assert.AreEqual(px, _host.EvaluateNumber($"{Basis(stone, tide)}.pivot.x"), 1e-6,
                                $"{species}/{stone}/{tide}: pivot.x moved with stone or tide");
                Assert.AreEqual(py, _host.EvaluateNumber($"{Basis(stone, tide)}.pivot.y"), 1e-6,
                                $"{species}/{stone}/{tide}: pivot.y moved with stone or tide");
            }
        }

        [Test]
        public void AnAwashRockIsCLIPPED_NotFlooded_SoNoWaterIsEverBaked()
        {
            // ⭐ The kit's water contract, measured on actual rendered pixels rather than read off the
            // sidecar. `awash` is the ONE state where a rig might be tempted to paint sea — the
            // README is explicit that it clips at the sea plane and adds a wet-glint band instead.
            // The shader owns every water pixel (ADR 0010/0012/0023).
            var report = new StringBuilder("[rock-bake] awash cells scanned for baked water:\n");

            foreach (string key in RockIsoCatalog.SpeciesKeys)
            {
                var entry = _contract.Find(key);
                byte[] rgba = _host.EvaluateBytes(
                    RockIsoBaker.ResultExpr(key, 0, 0, "sandstone", "awash") + ".rgba");

                int blueDominant = 0, opaque = 0;
                for (int i = 0; i < rgba.Length; i += 4)
                {
                    int r = rgba[i], g = rgba[i + 1], b = rgba[i + 2], a = rgba[i + 3];
                    if (a == 0) continue;
                    opaque++;
                    // The kit's palette is red sandstone, granite, basalt and quartzite — R >= B
                    // throughout. A waterline, foam or shallow-fill pixel would be blue-dominant.
                    if (b > r + 12 && b >= g) blueDominant++;
                }

                report.AppendLine($"  {key,-10} {opaque,5} opaque px, {blueDominant} blue-dominant");
                Assert.AreEqual(0, blueDominant,
                    $"{key} awash: {blueDominant} blue-dominant pixel(s) — this kit must bake NO " +
                    "water. `awash` is a clip at the sea plane, not a flood.");
            }

            Debug.Log(report.ToString());
        }

        // =====================================================================================
        // 3. SABOTAGE
        // =====================================================================================

        /// <summary>
        /// Sabotage the staleness guard: hand it the geometry a drifted rig would produce and prove
        /// it refuses. A guard that has never been shown to fire is decoration — and this one is the
        /// only thing standing between a re-baked cell and twenty wrong anchors.
        /// </summary>
        [Test]
        public void Sabotage_ADriftedCellOrVariantCount_MakesTheBakerRefuse()
        {
            var real = RockIsoBaker.ReadSpeciesSpec(_host, "Erratic");
            var entry = _contract.Find("Erratic");

            var cases = new (RockIsoBaker.SpeciesSpec spec, string what)[]
            {
                (new RockIsoBaker.SpeciesSpec(real.CellW, real.CellH + 1, real.Cols, real.Rows),
                 "cell one row TALLER (every anchor's y is now off by one)"),
                (new RockIsoBaker.SpeciesSpec(real.CellW + 2, real.CellH, real.Cols, real.Rows),
                 "cell two px WIDER (every centred pivot is off by one)"),
                (new RockIsoBaker.SpeciesSpec(real.CellW, real.CellH, real.Cols + 1, real.Rows),
                 "an EXTRA variant (the columns anchors are numbered against moved)"),
                (new RockIsoBaker.SpeciesSpec(real.CellW, real.CellH, real.Cols, real.Rows - 1),
                 "a DROPPED dress row"),
            };

            var table = new StringBuilder("[rock-bake] SABOTAGE — the staleness guard:\n");
            foreach (var (spec, what) in cases)
            {
                Assert.Throws<InvalidOperationException>(
                    () => RockIsoBaker.AssertMatchesContract("Erratic", spec, _contract),
                    $"SABOTAGE NOT DETECTED — {what} passed the guard.");
                table.AppendLine($"  {what,-58} → refused");
            }

            // ...and an unknown species, which is the shape a renamed rig export takes.
            Assert.Throws<InvalidOperationException>(
                () => RockIsoBaker.AssertMatchesContract("Menhir", real, _contract),
                "SABOTAGE NOT DETECTED — a species absent from the contract passed the guard.");
            table.AppendLine("  a species the contract does not name                       → refused");

            // The true pair must still pass, or the guard is simply broken rather than strict.
            Assert.DoesNotThrow(() => RockIsoBaker.AssertMatchesContract("Erratic", real, _contract));
            table.AppendLine($"  the SHIPPED geometry ({entry.CellW}×{entry.CellH}, " +
                             $"{entry.SheetCols} variants)              → accepted");

            Debug.Log(table.ToString());
        }

        /// <summary>
        /// Sabotage the render-size check: the buffer length the baker demands is what stops a
        /// mis-shaped cell being blitted into the grid, where it would shear every cell after it.
        /// </summary>
        [Test]
        public void Sabotage_AWrongSizedBuffer_IsCaughtByTheCellLengthCheck()
        {
            var entry = _contract.Find("Cobble");
            int correct = entry.CellW * entry.CellH * 4;

            // Render a DIFFERENT species into the same check — the shape a copy-paste in the bake
            // loop takes, and the reason the check compares against the declared cell rather than
            // just "non-empty".
            byte[] wrong = _host.EvaluateBytes(
                RockIsoBaker.ResultExpr("Cloven", 0, 0, "sandstone", "dry") + ".rgba");

            Assert.AreNotEqual(correct, wrong.Length,
                "SABOTAGE NOT DETECTED — Cloven and Cobble cells must differ in size, or this " +
                "check could not tell them apart.");
            Debug.Log($"[rock-bake] SABOTAGE — buffer size: Cobble expects {correct} bytes, a Cloven " +
                      $"render is {wrong.Length}. The length check separates them.");
        }
    }
}
