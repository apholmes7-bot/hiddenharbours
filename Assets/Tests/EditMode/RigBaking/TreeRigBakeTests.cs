using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// THE ACCEPTANCE SUITE FOR THE TREE BAKE — and the rig is its own oracle.
    ///
    /// <para>A green test run against our own constants proves nothing here. The tree rigs run in
    /// V8 inside the editor, so every committed pixel can be compared against a FRESH render of the
    /// rig, byte for byte — the same shape of proof that settled the boat bakes
    /// (<c>PuntGoldenMasterTests</c>), except that for a rig-native kit the answer should be exact
    /// rather than "modulo a revision".</para>
    ///
    /// <para><b>TWO RIGS SINCE PASS 4 (2026-09-27).</b> The kit is baked from <c>treeIsoRig4.js</c>
    /// through <c>TreePass4Baker</c> (rig 4, <c>treeMaps4.js</c> and the <c>HHTreePass4</c> glue), so
    /// every test about the KIT — its pivots, its 2048 fit, its trunk anchors and its committed pixels
    /// — reads that live rig, and the committed pixels are compared with the glue's cells. Rig 3 stays
    /// committed and <c>TreeRigBaker</c> still drives it, so the tests about what THAT baker makes of
    /// it — the mask order, the coverage, the retired keyline, the sway frames and determinism — keep
    /// reading rig 3 through <c>TreeRigBaker.InstallRig</c>. <see cref="TreePass4BakeTests"/> owns the
    /// pass-4 bake itself, in <c>Temp/</c>.</para>
    ///
    /// <para><b>Every assert that matters carries a MEASURED SABOTAGE</b> in the same test: the
    /// mutation is applied, the check is shown to reject it, and the magnitude is logged. An assert
    /// with no sabotage curve is decoration — it can pass because it is right or because it is
    /// vacuous, and nothing distinguishes the two.</para>
    ///
    /// <para>CPU-only: the V8 host, <c>Texture2D.LoadImage</c> and <c>GetPixels32</c> need no
    /// graphics device, so nothing here has to gate on <c>GraphicsDeviceType.Null</c>.</para>
    /// </summary>
    public class TreeRigBakeTests
    {
        const string Stage = TreeRigBaker.DefaultStage;
        const string Season = TreeRigBaker.DefaultSeason;

        /// <summary><c>_TrunkAnchor</c> in <c>Assets/_Project/Art/Materials/Tree.mat</c> — the ONE
        /// material-wide constant this kit replaces with a per-species value.</summary>
        const float ShippedMaterialTrunkAnchor = 0.14f;

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        /// <summary>The PASS-3 rig, as <c>TreeRigBaker</c> installs it: the host for the tests about
        /// what that baker makes of rig 3.</summary>
        static IRigScriptHost CreateTreeHost()
        {
            var host = RigScriptHostFactory.Create();
            TreeRigBaker.InstallRig(host);
            return host;
        }

        /// <summary>The LIVE kit's rig — rig 4, its maps and the glue — as <c>TreePass4Baker</c>
        /// installs them: the host for every test about the committed kit.</summary>
        static IRigScriptHost CreateLiveTreeHost() => TreePass4TempBake.CreateHost();

        static TreeKitCatalog.Contract LoadContract()
        {
            string path = Path.Combine(RepoRoot, TreeKitCatalog.ContractPath);
            Assert.IsTrue(File.Exists(path),
                $"No contract at {TreeKitCatalog.ContractPath}. Run Hidden Harbours ▸ Art ▸ " +
                "Bake Acadian Trees — the sheets and the contract are written by one bake and are " +
                "only meaningful together.");
            var contract = JsonUtility.FromJson<TreeKitCatalog.Contract>(File.ReadAllText(path));
            Assert.IsNotNull(contract?.trees, "The contract parsed but carries no trees.");
            Assert.IsNotEmpty(contract.trees);
            return contract;
        }

        // =================================================================================
        // both rigs run unmodified, and expose exactly what their bakers call
        // =================================================================================

        /// <summary>The five entry points the PASS-3 baker (<c>TreeRigBaker</c>) calls straight off
        /// rig 3's global.</summary>
        static readonly string[] BakerEntryPoints =
        {
            "render", "packMask", "normalView", "sheetSpec", "cellOf",
        };

        /// <summary>The five the LIVE kit's baker (<c>TreePass4Baker</c>) and its glue call straight
        /// off rig 4's global. Rig 4 has no <c>packMask</c> or <c>normalView</c>: the glue builds the
        /// mask by rig 3's formulas and reads the normal through <c>view(rest, 'normal')</c>.</summary>
        static readonly string[] LiveBakerEntryPoints =
        {
            "sheetSpec", "relight", "view", "cellOf", "clearCache",
        };

        [Test]
        public void TreeRig_RunsUnmodified_AndNeedsNoShim()
        {
            // No canvas mailbox, no string widening, no globals patched in first: the difference
            // between these rigs and every other one in the repo, and the reason the bakers call
            // their public API directly.
            //
            // ⚠️ The rig FILES and the rig GLOBALS are read from TreeKitCatalog, never spelled out
            // here. They were hardcoded as treeIsoRig.js/TreeRig until the pass-2 swap (2026-07-29),
            // which is precisely the shape of test that keeps passing against the OLD rig after the
            // pipeline has moved on. Since pass 4 there are two: the kit's rig (RigScriptPath, which
            // TreePass4Baker drives) and the pass-3 rig TreeRigBaker still drives, each in its own
            // bare host.
            foreach (var (rig, g, entryPoints) in new[]
                     {
                         (TreeKitCatalog.RigScriptPath, TreeKitCatalog.RigGlobalName, LiveBakerEntryPoints),
                         (TreeKitCatalog.Pass3RigScriptPath, TreeKitCatalog.Pass3RigGlobalName, BakerEntryPoints),
                     })
            {
                using var host = RigScriptHostFactory.Create();
                string source = File.ReadAllText(Path.Combine(RepoRoot, rig));
                Assert.DoesNotThrow(() => host.Execute(source),
                    $"{rig} must run in a BARE host. If this throws, something in the rig now " +
                    "needs an environment global — and the shim belongs in host code, never in the " +
                    "art director's file (ADR 0021 §5).");

                Assert.IsTrue(host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"),
                    $"{rig} ran but did not install globalThis.{g}.");

                foreach (string fn in entryPoints)
                    Assert.IsTrue(host.EvaluateBool($"typeof {g}.{fn} === 'function'"),
                        $"{g}.{fn}() is missing — its baker calls it directly.");
            }
        }

        /// <summary>
        /// ⭐ WHAT THE PASS-4 SWITCH KEPT AND WHAT IT MOVED, asserted on the two rigs side by side.
        ///
        /// <para>It was <c>PassTwoRig_KeepsEveryContractConstant_SoTheSwapWasAReBakeAndNotAReDesign</c>,
        /// and at passes 2 and 3 that claim held whole: every world constant identical, only the
        /// pixels new. Pass 4 is a re-design and says so. Rig 4 bakes ONE rest pose per season and
        /// hands the sway to the shader, so <c>SWAY</c> (the frame count the pass-3 sheet's rows came
        /// from) is gone and <c>LOOP</c> (the wind loop the shader replays) takes its place, and
        /// <c>LIGHT</c>, <c>packMask</c> and <c>normalView</c> went with the pass-3 surface. What must
        /// NOT move is still asserted, because it is what the world was built under: PPU, the camera,
        /// SCALE, the three rules, VARIANTS, KEYLINE_DEFAULT, the seasons, the stages and the ten
        /// species keys in their order.</para>
        ///
        /// <para>The mask's meaning is the one thing rig 4 no longer carries: the glue lights the mask
        /// by rig 3's <c>LIGHT</c>, so the committed contract's light block must BE rig 3's, vector
        /// for vector — and must not be rig 4's own sun, which is the wrong answer it is shown to
        /// reject.</para>
        /// </summary>
        [Test]
        public void PassFourRig_KeepsEveryWorldConstant_AndMovesTheMotionOutOfTheSheet()
        {
            // Both passes are loaded into ONE host on purpose: they install different globals
            // (TreeRig3 vs TreeRig4), so they cannot collide, and comparing them in-process beats
            // comparing either against a number typed in here.
            using var host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Path.Combine(RepoRoot, TreeKitCatalog.Pass3RigScriptPath)));
            host.Execute(File.ReadAllText(Path.Combine(RepoRoot, TreeKitCatalog.RigScriptPath)));

            string p3 = TreeKitCatalog.Pass3RigGlobalName, p4 = TreeKitCatalog.RigGlobalName;
            Assert.AreNotEqual(p3, p4, "The two passes must install DIFFERENT globals.");
            foreach (string g in new[] { p3, p4 })
                Assert.IsTrue(host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"),
                    $"globalThis.{g} did not install — this test needs both passes side by side.");

            // ---- what STAYED: every scalar the contract carries as a world constant, and SCALE ----
            foreach (string k in new[] { "PPU", "RIM_PX", "MIN_BODY", "MIN_R", "VARIANTS",
                                         "ELEV", "CE", "SE", "SCALE" })
            {
                foreach (string g in new[] { p3, p4 })
                    Assert.IsTrue(host.EvaluateBool($"typeof {g}.{k} === 'number'"),
                        $"{g}.{k} is not a number — the rig no longer publishes it.");
                double a = host.EvaluateNumber($"{p3}.{k}"), b = host.EvaluateNumber($"{p4}.{k}");
                Assert.AreEqual(a, b, 1e-12,
                    $"{k} differs between the two passes ({a} vs {b}). Trees.json publishes this as " +
                    "a constant the pixels were built under — if a pass really changed it, the " +
                    "consumers of that number (Tree.mat, the wind shader, SpriteLightMath, the " +
                    "planter's spacing) all need re-deriving.");
            }

            // The axes, the species SET in the rig's own order (a re-ordering would silently
            // re-point every prefab and paint-tool index) and the stage multipliers. Compared as
            // JSON so ORDER is part of the assertion, not just membership.
            foreach (string expr in new[]
                     {
                         "SEASONS",
                         "STAGE_KEYS",
                         "SPECIES.map(function(s){return s.key;})",
                         "STAGES",
                     })
                Assert.AreEqual(host.EvaluateString($"JSON.stringify({p3}.{expr})"),
                                host.EvaluateString($"JSON.stringify({p4}.{expr})"),
                    $"{expr} differs between the two passes. Species keys are the sheet stems and " +
                    "the prefab names, their ORDER is what AcadianTreeCatalog.Scan publishes to the " +
                    "paint tool, and a stage's multiplier is what 'mature' MEANS.");

            // ---- what MOVED: the sway, out of the sheet and into the shader ---------------------
            var contract = LoadContract();
            Assert.IsTrue(host.EvaluateBool($"typeof {p3}.SWAY === 'number'"),
                $"{p3}.SWAY is gone — rig 3 is the committed pass-3 rig, which baked its sway as frames.");
            Assert.IsTrue(host.EvaluateBool($"typeof {p4}.SWAY === 'undefined'"),
                $"{p4}.SWAY is back. Rig 4 bakes one rest pose and the shader sways it; a sway FRAME " +
                "count on the live rig means the motion moved back into the sheet — re-read the " +
                "contract's sheet block before baking anything.");
            Assert.IsTrue(host.EvaluateBool($"typeof {p4}.LOOP === 'number'"), $"{p4}.LOOP is missing.");
            int loop = (int)host.EvaluateNumber($"{p4}.LOOP");
            Assert.AreEqual(loop, contract.maps.loop,
                "The contract's maps.loop is the rig's LOOP: the shader replays the rig's own wind loop.");
            Assert.AreEqual(loop / 4, contract.sheet.rigSwayRows,
                "The rig lays its wind loop out four frames wide, so its own sheet is LOOP/4 rows tall.");
            Assert.AreEqual(TreeRigBaker.SwayRowsBaked, contract.sheet.rows,
                "ONE row is baked: the four variants at rest.");

            // ---- what MOVED: the mask's light, out of the rig and into the glue ------------------
            Assert.IsTrue(host.EvaluateBool($"typeof {p4}.LIGHT === 'undefined'"),
                $"{p4} publishes a LIGHT again. The glue lights the mask by its own copy of rig 3's " +
                "LIGHT; compare the two, and say which one the mask should mean, before re-baking.");
            Assert.AreEqual(3, contract.light.key.Length, "the contract's key light is a 3-vector");
            Assert.AreEqual(3, contract.light.rim.Length, "the contract's rim light is a 3-vector");
            double sunGap = 0;
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(host.EvaluateNumber($"{p3}.LIGHT.key[{i}]"), contract.light.key[i], 1e-6,
                    $"contract light.key[{i}] is not rig 3's LIGHT.key — every baked mask byte would " +
                    "mean something different than pass 3's did.");
                Assert.AreEqual(host.EvaluateNumber($"{p3}.LIGHT.rim[{i}]"), contract.light.rim[i], 1e-6,
                    $"contract light.rim[{i}] is not rig 3's LIGHT.rim.");
                sunGap = Math.Max(sunGap, Math.Abs(host.EvaluateNumber($"{p4}.REF_SKY.sunV[{i}]") -
                                                   contract.light.key[i]));
            }

            // ---- MEASURED SABOTAGE: rig 4's own sun must NOT pass for the mask's key -------------
            Debug.Log($"[tree-pass4] the mask's key is rig 3's LIGHT.key; rig 4's REF_SKY.sunV (the " +
                      $"albedo's sun) differs from it by up to {sunGap:F4} per component.");
            Assert.Greater(sunGap, 1e-3,
                "SABOTAGE: rig 4's own sun is indistinguishable from rig 3's key at this tolerance, so " +
                "the check above could not tell the glue's light from the rig's.");

            // ---- MEASURED SABOTAGE: the pixels DID change, or the switch was a no-op -------------
            // Without this the constants above would also pass if RigScriptPath pointed back at
            // rig 3 — identical constants AND identical pixels.
            string o = $"{{variant:0,season:'{Season}',frame:0,stage:'{Stage}'}}";
            byte[] a3 = host.EvaluateBytes($"{p3}.render('RedSpruce',{o}).rgba");
            byte[] a4 = host.EvaluateBytes($"{p4}.render('RedSpruce',{o}).rgba");
            Debug.Log($"[tree-pass4] RedSpruce/{Stage}/{Season} albedo: pass 3 is {a3.Length / 4} px, " +
                      $"pass 4 is {a4.Length / 4} px (the cell grows by the wind's reach each side).");
            Assert.AreNotEqual(a3, a4,
                "Pass 3 and pass 4 rendered the SAME Red Spruce. Either RigScriptPath is still " +
                "pointing at pass 3, or the drop was not the revised rig.");

            // ---- ADR 0031: the keyline stays retired on BOTH rigs --------------------------------
            foreach (string g in new[] { p3, p4 })
                Assert.IsTrue(host.EvaluateBool($"{g}.KEYLINE_DEFAULT === false"),
                    $"{g}.KEYLINE_DEFAULT is not false. The outline is retired from world art " +
                    "(ADR 0031): rig 4 bakes the kit, and rig 3 is the rig TreeRigBaker still drives.");

            // The control, on rig 3: the flag must still be reachable, or "retired" would be
            // indistinguishable from "the ring pass was deleted".
            string plain3 = $"{p3}.render('RedSpruce',{o})";
            string inked3 = $"{p3}.render('RedSpruce',{{variant:0,season:'{Season}',frame:0,stage:'{Stage}',outline:true}})";
            Assert.AreNotEqual(host.EvaluateBytes($"{plain3}.rgba"), host.EvaluateBytes($"{inked3}.rgba"),
                $"{p3}: {{outline:true}} rendered RedSpruce identically to the default, so the A/B " +
                "arm is gone. Keep the ring code — ADR 0031 gates it, it does not delete it.");

            // ⚠️ Rig 4 renders {outline:true} identically to the default: the A/B arm ADR 0031 keeps
            // is gone from the live rig. Measured and REPORTED, not asserted either way — rig 4 is
            // the art director's file, and whether the arm comes back is theirs to rule.
            string inked4 = $"{p4}.render('RedSpruce',{{variant:0,season:'{Season}',frame:0,stage:'{Stage}',outline:true}})";
            bool armGone = a4.SequenceEqual(host.EvaluateBytes($"{inked4}.rgba"));
            Debug.Log($"[tree-pass4] ADR 0031: {p4} {{outline:true}} renders RedSpruce " +
                      (armGone
                          ? "IDENTICALLY to the default — the live rig has no A/B arm for the ring (reported to the art director)."
                          : "with a ring — the live rig keeps the A/B arm."));
        }

        [Test]
        public void TreeRig_IsNotInTheRigCatalog_BecauseATreeHasNoHeading()
        {
            // RigEntry is built around AzimuthConvention for 8-direction turntables. A tree's sheet
            // axes are variant × sway, there is nothing to probe, and forcing an entry would invite
            // a DirForCell call that means nothing here.
            Assert.IsFalse(RigCatalog.Entries.Keys.Any(k => k.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0),
                "A 'tree' entry appeared in RigCatalog. That struct declares an azimuth convention " +
                "and the non-directional rigs are deliberately baked by dedicated bakers instead.");
        }

        // =================================================================================
        // 🔴 THE PIVOT IS THE TRUNK FOOT — the highest-risk fact in the kit
        // =================================================================================

        [Test]
        public void ContractPivots_MatchAFreshSheetSpec_AndAOnePixelOffsetIsRejected()
        {
            var contract = LoadContract();
            using var host = CreateLiveTreeHost();

            int worstPad = 0;
            foreach (var entry in contract.trees)
            {
                // The LIVE rig's sheetSpec, as TreePass4Baker reads it: rig 4's cell carries the
                // wind's reach each side, so rig 3's would be the wrong cell for every species.
                var spec = TreePass4Baker.ReadSheetSpec(host, entry.species, entry.stage, out int windReach);
                Assert.AreEqual(windReach, entry.wind.windReach, $"{entry.species}: wind reach drifted");

                // Read from the rig, never from a literal and never from the sprite's alpha.
                Assert.AreEqual(spec.CellW, entry.cellW, $"{entry.species}: cell width drifted");
                Assert.AreEqual(spec.CellH, entry.cellH, $"{entry.species}: cell height drifted");
                Assert.AreEqual(spec.PivotX, entry.pivotX, $"{entry.species}: pivot.x drifted");
                Assert.AreEqual(spec.PivotY, entry.pivotY, $"{entry.species}: pivot.y drifted");
                Assert.AreEqual(spec.Pad, entry.nearFlarePad, $"{entry.species}: flare pad drifted");

                // The pad IS the reason a tree does not pivot bottom-centre.
                Assert.Greater(entry.nearFlarePad, 0,
                    $"{entry.species}: pad 0 would mean the flare stops at the trunk foot — then " +
                    "bottom-centre would be right and this whole trap would not exist. Check " +
                    "cellOf() before believing it.");
                worstPad = Mathf.Max(worstPad, entry.nearFlarePad);

                Vector2 expected = new Vector2((float)spec.PivotX / spec.CellW,
                                               (float)spec.Pad / spec.CellH);
                Assert.AreEqual(expected.x, entry.unityPivotX, 1e-6f, $"{entry.species}: unity pivot x");
                Assert.AreEqual(expected.y, entry.unityPivotY, 1e-6f, $"{entry.species}: unity pivot y");
                Assert.AreEqual(expected, TreeKitCatalog.NormalizedPivot(entry),
                    $"{entry.species}: the catalog's pivot helper disagrees with the contract");

                // ---- MEASURED SABOTAGE: shift the pivot down one pixel ----------------------
                var sabotaged = new TreeKitCatalog.Entry
                {
                    species = entry.species, stage = entry.stage,
                    cellW = entry.cellW, cellH = entry.cellH,
                    pivotX = entry.pivotX, pivotY = entry.pivotY,
                    nearFlarePad = entry.nearFlarePad - 1,
                };
                Vector2 wrong = TreeKitCatalog.NormalizedPivot(sabotaged);
                Assert.AreNotEqual(expected, wrong,
                    $"{entry.species}: a 1 px pivot offset must be visible to this check.");
                Assert.Greater(Mathf.Abs(wrong.y - expected.y), 1e-6f);
                Debug.Log($"[tree-pivot] {entry.species}: cell {entry.cellW}×{entry.cellH}, " +
                          $"trunk foot ({entry.pivotX},{entry.pivotY}), pad {entry.nearFlarePad} → " +
                          $"pivot.y {expected.y:F5}; a 1 px offset moves it to {wrong.y:F5} " +
                          $"(Δ {Mathf.Abs(wrong.y - expected.y):F5} = 1/{entry.cellH} of the cell " +
                          $"= {1f / 32f:F4} m at PPU 32).");
            }

            // The bottom-centre assumption every other tree sprite in this repo uses would sink
            // these by their own pad — state the worst case so the size of the bug is on record.
            Debug.Log($"[tree-pivot] Bottom-centre (0.5, 0) would sink the worst species by " +
                      $"{worstPad} px = {worstPad / 32f:F2} m. That is why the pivot is read from " +
                      "sheetSpec().pivot and never assumed.");
            Assert.GreaterOrEqual(worstPad, 10,
                "The worst flare pad collapsed below 10 px — if the rig really did that, re-read " +
                "cellOf() before relaxing anything downstream.");
        }

        // =================================================================================
        // 🔴 THE MASK CHANNEL ORDER IS THE RIG'S, NOT THE REFERENCE TECHNIQUE'S
        // =================================================================================

        [Test]
        public void MaskChannels_AreKeyRimDepthCoverage_NotTheReferenceTechniquesOrder()
        {
            // Every description of the sprite-light technique this serves says "green = front,
            // blue = rim". TreeRig.packMask() emits R = key light · G = back rim · B = depth ·
            // A = coverage. Anyone porting a snippet will swap two channels and get something that
            // looks SUBTLY wrong rather than obviously broken, so the order is pinned here with the
            // magnitude of the mistake attached.
            using var host = CreateTreeHost();
            const string Species = "RedSpruce";

            string res = TreeRigBaker.ResultExpr(Species, Stage, Season, variant: 0, frame: 0);
            byte[] mask = host.EvaluateBytes(TreeRigBaker.ChannelExpr(res, TreeKitCatalog.Channel.Mask));
            byte[] albedo = host.EvaluateBytes(TreeRigBaker.ChannelExpr(res, TreeKitCatalog.Channel.Albedo));

            // The rig's three grayscale masks, straight out of render().
            byte[] front = ReadMask(host, res, "front");
            byte[] rim = ReadMask(host, res, "rim");
            byte[] depth = ReadMask(host, res, "depth");

            int n = front.Length;
            Assert.AreEqual(n * 4, mask.Length, "packMask must be RGBA over the same cell.");

            int rWrong = 0, gWrong = 0, bWrong = 0, aWrong = 0, swapWouldChange = 0;
            for (int i = 0; i < n; i++)
            {
                if (mask[i * 4 + 0] != front[i]) rWrong++;
                if (mask[i * 4 + 1] != rim[i]) gWrong++;
                if (mask[i * 4 + 2] != depth[i]) bWrong++;
                if (mask[i * 4 + 3] != albedo[i * 4 + 3]) aWrong++;
                if (front[i] != rim[i]) swapWouldChange++;
            }

            Assert.AreEqual(0, rWrong, "packMask R must be masks.front (the KEY light).");
            Assert.AreEqual(0, gWrong, "packMask G must be masks.rim (the BACK rim).");
            Assert.AreEqual(0, bWrong, "packMask B must be masks.depth.");
            Assert.AreEqual(0, aWrong, "packMask A must be the sprite's coverage (rgba alpha).");

            // ---- MEASURED SABOTAGE: swap R and G --------------------------------------------
            double pct = 100.0 * swapWouldChange / n;
            Debug.Log($"[tree-mask] {Species}: R↔G swap would change {swapWouldChange} of {n} px " +
                      $"= {pct:F2}% of the cell. Measured 2026-07-29 (pass-2 rig): 4907 px / " +
                      "24.49%; pass 1 was 5405 px / 29.60%.");
            Assert.Greater(pct, 10.0,
                "An R↔G swap must change a LOT of the cell, or this assert is decoration — the key " +
                "and rim channels would be nearly interchangeable and the order would not matter.");
        }

        /// <summary>
        /// ⭐ THIS TEST'S CLAIM INVERTED WITH ADR 0031 (wave 2) — and the old version said so itself.
        ///
        /// <para>It used to assert <c>inMaskNotNormal &gt; 0</c>: the rig composited a 1 px keyline
        /// ring OUTSIDE the volume, so those pixels were opaque in rgba (and therefore in the mask's
        /// A) but carried no surface normal — the albedo/mask footprint was 11% larger than the
        /// normal's. Its failure message named this exact outcome: <i>"if this is 0 the rig stopped
        /// drawing the keyline — a look change, not a bake bug, but the contract's coverageNote is
        /// now wrong."</i> The rig has now stopped drawing it, so the assertion flips and the
        /// contract's <c>coverageNote</c> is corrected in the same change.</para>
        ///
        /// <para><b>Why the equality is worth pinning rather than deleting.</b> Three co-registered
        /// sheets agreeing on coverage is the property a shader author actually relies on, and it is
        /// only true because the ring is gone — if the ring ever came back by default, this fires
        /// immediately, which is the same tripwire pointed the other way. The advice it replaces
        /// ("light the keyline from the mask, never from the normal") is now advice about art that
        /// no longer exists.</para>
        /// </summary>
        [Test]
        public void ChannelCoverage_AlbedoMaskAndNormal_AllAgree_NowTheKeylineIsRetired()
        {
            using var host = CreateTreeHost();
            const string Species = "RedSpruce";
            string res = TreeRigBaker.ResultExpr(Species, Stage, Season, variant: 0, frame: 0);

            byte[] albedo = host.EvaluateBytes(TreeRigBaker.ChannelExpr(res, TreeKitCatalog.Channel.Albedo));
            byte[] mask = host.EvaluateBytes(TreeRigBaker.ChannelExpr(res, TreeKitCatalog.Channel.Mask));
            byte[] normal = host.EvaluateBytes(TreeRigBaker.ChannelExpr(res, TreeKitCatalog.Channel.Normal));

            int n = albedo.Length / 4;
            int albedoCov = 0, maskCov = 0, normalCov = 0, inMaskNotNormal = 0, inNormalNotMask = 0;
            double minLen = double.MaxValue, maxLen = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                bool a = albedo[i * 4 + 3] != 0, m = mask[i * 4 + 3] != 0, nn = normal[i * 4 + 3] != 0;
                if (a) albedoCov++;
                if (m) maskCov++;
                if (nn) normalCov++;
                if (m && !nn) inMaskNotNormal++;
                if (nn && !m) inNormalNotMask++;
                if (!nn) continue;
                double x = normal[i * 4 + 0] / 255.0 * 2 - 1;
                double y = normal[i * 4 + 1] / 255.0 * 2 - 1;
                double z = normal[i * 4 + 2] / 255.0 * 2 - 1;
                double len = Math.Sqrt(x * x + y * y + z * z);
                minLen = Math.Min(minLen, len);
                maxLen = Math.Max(maxLen, len);
            }

            Assert.AreEqual(albedoCov, maskCov,
                "The mask's A channel IS the albedo's coverage — they cannot disagree.");
            Assert.AreEqual(0, inNormalNotMask,
                "A pixel with a normal but no coverage would be shaded geometry outside the sprite.");
            Assert.AreEqual(0, inMaskNotNormal,
                $"{inMaskNotNormal} pixel(s) are covered by the albedo/mask but carry no normal. " +
                "With the keyline retired (ADR 0031) the only thing that ever produced such a " +
                "pixel is gone, so all three sheets must now cover exactly the geometry. A " +
                "non-zero here means the ring is being drawn again by default — check " +
                "KEYLINE_DEFAULT in treeIsoRig2.js.");

            // Unit normals, within 8-bit quantisation.
            Assert.That(minLen, Is.GreaterThan(0.97), "a decoded normal shorter than 0.97 is not a normal");
            Assert.That(maxLen, Is.LessThan(1.03));

            Debug.Log($"[tree-coverage] {Species}: albedo/mask {albedoCov} px, normal {normalCov} px, " +
                      $"keyline-only {inMaskNotNormal} px. Decoded |n| ∈ [{minLen:F4}, {maxLen:F4}]. " +
                      "Before ADR 0031 wave 2 this read 6570 / 5848 / 722 px = 11.0% (pass-2 rig; " +
                      "pass 1 was 7601 / 6990 / 611 px = 8.0%, the serrated pass-2 outline having " +
                      "more perimeter per unit area). Retiring the ring removed exactly those 722 " +
                      "px, which is why the three sheets now agree.");
        }

        /// <summary>
        /// ⭐ The outline retirement (ADR 0031, wave 2), pinned on rendered pixels — and pinned as
        /// the STRUCTURAL claim, not as a colour match. Follows the shore-plant pilot's shape
        /// (<c>ShorePlantRigBakeTests.TheKeylineIsRetired_AndTurningItBackOn_ChangesOnlyTheRing</c>).
        ///
        /// <para><b>The definition used here is exact.</b> A keyline pixel is one the shade pass
        /// made opaque where the rig has <i>no geometry</i> — <c>rgba.a != 0</c> while
        /// <c>res.alpha == 0</c>. That is what the ring pass does and the only thing it does, so it
        /// cannot be confused with a bark or shadow pixel that happens to be dark, and it stays
        /// true if the keyline colour is ever re-tuned for the A/B arm.</para>
        ///
        /// <para><b>Why the second half matters more than the first.</b> "No keyline" alone would
        /// also pass if the ring pass were broken, or the renderer returned nothing. So the test
        /// carries its own control: <c>{outline:true}</c> must bring the ring BACK, and — the real
        /// assertion — <b>every pixel that differs between the two arms must be a ring pixel</b>.
        /// That is what makes the retirement provably a pure ring deletion: no painted pixel of any
        /// tree changes value, so no colour, band, rim or leaf cell can have moved with it.</para>
        ///
        /// <para><b>Measured across all ten species</b> (variant 0, mature/summer): 6,821 ring px
        /// against 59,450 painted px = <b>0.11×</b>, 0 violations. Trees are the AREA end of the
        /// perimeter law — the shore plants paid 0.39× and glasswort 0.94× — so this family loses
        /// the least by dropping the ring, and rule 2's authored silhouette plus rule 3's rim were
        /// carrying the edge all along.</para>
        /// </summary>
        [Test]
        public void TheKeylineIsRetired_AndTurningItBackOn_ChangesOnlyTheRing()
        {
            using var host = CreateTreeHost();
            var report = new System.Text.StringBuilder("[tree-bake] ADR 0031 — keyline retirement, live:\n");
            int totalRing = 0, totalPainted = 0;

            foreach (string key in TreeRigBaker.ReadSpeciesKeys(host))
            {
                var shipped = RenderArm(host, key, opts: null);
                var restored = RenderArm(host, key, opts: "outline:true");

                // The geometry itself must be untouched by the flag — otherwise "only the ring
                // changed" would be comparing two different trees.
                CollectionAssert.AreEqual(shipped.Geometry, restored.Geometry,
                    $"{key}: the outline flag moved the GEOMETRY. The ring pass writes only where " +
                    "there is no geometry; if this fired it is doing something else as well.");

                int ringDefault = 0, ringRestored = 0, painted = 0, violations = 0;
                for (int i = 0, p = 0; i < shipped.Rgba.Length; i += 4, p++)
                {
                    bool hasGeometry = shipped.Geometry[p] != 0;
                    if (hasGeometry) painted++;
                    if (!hasGeometry && shipped.Rgba[i + 3] != 0) ringDefault++;
                    if (!hasGeometry && restored.Rgba[i + 3] != 0) ringRestored++;

                    bool differs = shipped.Rgba[i] != restored.Rgba[i] ||
                                   shipped.Rgba[i + 1] != restored.Rgba[i + 1] ||
                                   shipped.Rgba[i + 2] != restored.Rgba[i + 2] ||
                                   shipped.Rgba[i + 3] != restored.Rgba[i + 3];
                    if (differs && hasGeometry) violations++;
                }

                Assert.AreEqual(0, ringDefault,
                    $"{key}: {ringDefault} pixel(s) are opaque where the rig has no geometry, on a " +
                    "DEFAULT render. The keyline is retired (ADR 0031) — a default bake must draw " +
                    "no ring at all.");

                // The control. Without this, "0 ring pixels" would also be satisfied by a ring pass
                // that no longer works, or a species that renders nothing.
                Assert.Greater(ringRestored, 0,
                    $"{key}: {{outline:true}} produced no ring either — the A/B arm is broken, so " +
                    "the zero above proves nothing about the default.");

                Assert.AreEqual(0, violations,
                    $"{key}: {violations} PAINTED pixel(s) differ between the retired and restored " +
                    "arms. Retiring the keyline must be a pure ring deletion — if a tree's own " +
                    "pixels move with the flag, the ring pass is writing inside the silhouette.");

                totalRing += ringRestored;
                totalPainted += painted;
                report.AppendLine(
                    $"  {key,-16} {painted,6} painted px · ring {ringRestored,5} px " +
                    $"({ringRestored / (float)Math.Max(1, painted):F2}× painted) · " +
                    $"default ring {ringDefault} · painted-pixel diffs {violations}");
            }

            report.AppendLine(
                $"  ── {totalRing} ring px against {totalPainted} painted px " +
                $"({totalRing / (float)totalPainted:F2}×), 0 painted pixels touched. A tree is the " +
                "AREA end of the perimeter law, so it paid least for the ring (shore plants 0.39×).");
            Debug.Log(report.ToString());
        }

        readonly struct Arm
        {
            public readonly byte[] Rgba, Geometry;
            public Arm(byte[] rgba, byte[] geometry) { Rgba = rgba; Geometry = geometry; }
        }

        /// <summary>
        /// One render of <paramref name="species"/> with extra options spliced into the SAME
        /// expression the baker builds, so an A/B arm cannot drift from the production call in any
        /// other respect. The splice targets the trailing <c>})</c> of the baker's own options
        /// object rather than restating it — <paramref name="opts"/> is a bare JS fragment, e.g.
        /// <c>"outline:true"</c>.
        /// </summary>
        static Arm RenderArm(IRigScriptHost host, string species, string opts)
        {
            string expr = TreeRigBaker.ResultExpr(species, Stage, Season, variant: 0, frame: 0);
            if (!string.IsNullOrEmpty(opts))
            {
                int close = expr.LastIndexOf("})", StringComparison.Ordinal);
                Assert.Greater(close, 0,
                    "TreeRigBaker.ResultExpr no longer ends in an options object — this splice is " +
                    $"built on that shape. Got: {expr}");
                expr = expr.Substring(0, close) + "," + opts + expr.Substring(close);
            }

            const string Res = "__hhTreeArm";
            host.Execute($"globalThis.{Res} = {expr};");
            return new Arm(host.EvaluateBytes($"{Res}.rgba"), host.EvaluateBytes($"{Res}.alpha"));
        }

        // =================================================================================
        // ONE SWAY ROW — a decision, pinned, with the cost of getting the frame wrong measured
        // =================================================================================

        [Test]
        public void OneSwayRowIsBaked_AndAFlippedFrameWouldBeObvious()
        {
            var contract = LoadContract();
            Assert.AreEqual(1, TreeRigBaker.SwayRowsBaked,
                "The shader owns the swaying — see TreeRigBaker.SwayRowsBaked for the measurement " +
                "behind this, and do not raise it without re-reading it.");
            Assert.AreEqual(TreeRigBaker.SwayRowsBaked, contract.sheet.rows);
            Assert.AreEqual(4, contract.sheet.rigSwayRows,
                "The rig can make 4 sway rows; the contract records the difference on purpose.");

            using var host = CreateTreeHost();
            double worstPct = 100, bestPct = 0;
            foreach (var entry in contract.trees)
            {
                string f0 = TreeRigBaker.ResultExpr(entry.species, entry.stage, Season, 0, frame: 0);
                string f1 = TreeRigBaker.ResultExpr(entry.species, entry.stage, Season, 0, frame: 1);
                byte[] a = host.EvaluateBytes(TreeRigBaker.ChannelExpr(f0, TreeKitCatalog.Channel.Albedo));
                byte[] b = host.EvaluateBytes(TreeRigBaker.ChannelExpr(f1, TreeKitCatalog.Channel.Albedo));

                int n = a.Length / 4, diff = 0;
                for (int i = 0; i < n; i++)
                    if (a[i * 4] != b[i * 4] || a[i * 4 + 1] != b[i * 4 + 1] ||
                        a[i * 4 + 2] != b[i * 4 + 2] || a[i * 4 + 3] != b[i * 4 + 3]) diff++;

                double pct = 100.0 * diff / n;
                worstPct = Math.Min(worstPct, pct);
                bestPct = Math.Max(bestPct, pct);
                Debug.Log($"[tree-sway] {entry.species}: frame 1 differs from frame 0 in {diff} of " +
                          $"{n} px = {pct:F2}%.");
            }

            // ---- MEASURED SABOTAGE: bake frame 1 into the row we call frame 0 --------------
            Assert.Greater(worstPct, 3.0,
                "Even the stiffest species must shift by more than 3% of its cell between sway " +
                "frames, or 'we committed frame 0' would be unfalsifiable.");
            Debug.Log($"[tree-sway] Committing frame 1 by mistake would change between " +
                      $"{worstPct:F2}% and {bestPct:F2}% of a cell. Measured 2026-07-29 (pass-2 " +
                      "rig): 5.18% (Balsam Fir) to 20.37% (Trembling Aspen); pass 1 was 5.04% " +
                      "(Red Spruce) to 18.56% (Trembling Aspen).");
        }

        // =================================================================================
        // the 2048 cap, and the per-species trunk anchor
        // =================================================================================

        [Test]
        public void EverySpecies_FitsUnitys2048Cap_AssertedViaTheRigsOwnFitsFlag()
        {
            var contract = LoadContract();
            using var host = CreateLiveTreeHost();

            foreach (var entry in contract.trees)
            {
                var spec = TreePass4Baker.ReadSheetSpec(host, entry.species, entry.stage, out _);

                Assert.IsTrue(spec.RigFits,
                    $"{entry.species}: the rig's own sheetSpec().fits is false at " +
                    $"{spec.RigSheetW}×{spec.RigSheetH}.");
                Assert.AreEqual(spec.RigFits, entry.rigFitsUnity2048);
                Assert.AreEqual(spec.RigSheetW, entry.rigSheetW);
                Assert.AreEqual(spec.RigSheetH, entry.rigSheetH);

                Assert.LessOrEqual(entry.sheetW, TreeKitCatalog.ImportSizeCap,
                    $"{entry.species}: over the cap Unity imports DOWNSCALED with the sprite count " +
                    "still matching, so only a cell-size assert would ever catch it.");
                Assert.LessOrEqual(entry.sheetH, TreeKitCatalog.ImportSizeCap);
                Assert.AreEqual(spec.Cols * spec.CellW, entry.sheetW);
                Assert.AreEqual(TreeRigBaker.SwayRowsBaked * spec.CellH, entry.sheetH);
            }
        }

        [Test]
        public void TrunkAnchor_IsPerSpecies_AndOneMaterialConstantCannotServeThemAll()
        {
            var contract = LoadContract();
            using var host = CreateLiveTreeHost();

            float lo = float.MaxValue, hi = float.MinValue;
            string loKey = null, hiKey = null;

            foreach (var entry in contract.trees)
            {
                var spec = TreePass4Baker.ReadSheetSpec(host, entry.species, entry.stage, out _);
                Assert.AreEqual(spec.TrunkAnchor, entry.trunkAnchor, 1e-6f,
                    $"{entry.species}: trunkAnchor must be pad/cellH read from the live rig.");
                Assert.AreEqual(entry.unityPivotY, entry.trunkAnchor, 1e-6f,
                    $"{entry.species}: the sprite pivot's height and the shader's _TrunkAnchor are " +
                    "THE SAME fraction. If they part company, the lowest rows of near-root flare " +
                    "fall outside the planted band and slide under wind.");

                if (entry.trunkAnchor < lo) { lo = entry.trunkAnchor; loKey = entry.species; }
                if (entry.trunkAnchor > hi) { hi = entry.trunkAnchor; hiKey = entry.species; }
            }

            Debug.Log($"[tree-anchor] per-species _TrunkAnchor spans {lo:F4} ({loKey}) to " +
                      $"{hi:F4} ({hiKey}); the shipped Tree.mat constant is " +
                      $"{ShippedMaterialTrunkAnchor}. Measured 2026-09-27 (pass-4.1 rig): 0.0272 " +
                      "(WhitePine, TremblingAspen) to 0.0965 (WhiteCedar): pass 3's flare pads over " +
                      "pass 4's cell heights (pass 3 was 0.0271, WhitePine, to 0.0978, WhiteCedar). " +
                      "Measured 2026-07-29 (pass-2 rig): 0.0519 (TremblingAspen) to 0.0922 " +
                      "(WhiteCedar); pass 1 was 0.0833 (BlackSpruce) to 0.1447 (RedOak). ⚠️ Since " +
                      "pass 2 the band sits ENTIRELY below the shipped 0.14, so the single material " +
                      "value over-anchors all ten species — the case for the per-renderer anchor.");

            // The whole justification for making this per species: the spread is bigger than any
            // sane tolerance, and the one shipped constant is not even inside the middle of it.
            Assert.Greater(hi - lo, 0.02f,
                "If every species' trunk foot sat at the same fraction of its cell, one material " +
                "constant would be correct and this field would be noise.");
            Assert.Greater(ShippedMaterialTrunkAnchor, lo,
                "Tree.mat's single 0.14 anchors MORE than the shortest-flared species has trunk, " +
                "freezing canopy that should move — that is the reading this per-species value " +
                "replaces. If 0.14 dropped below the whole range, re-derive this note.");
        }

        // =================================================================================
        // determinism — the precondition for "bake vs fresh render, exact"
        // =================================================================================

        [Test]
        public void RigRenders_AreBitIdenticalAcrossCalls_SoAnExactDiffIsMeaningful()
        {
            using var host = CreateTreeHost();
            foreach (string key in TreeRigBaker.ReadSpeciesKeys(host))
            {
                string res = TreeRigBaker.ResultExpr(key, Stage, Season, variant: 1, frame: 0);
                foreach (var channel in TreeKitCatalog.Channels)
                {
                    byte[] a = host.EvaluateBytes(TreeRigBaker.ChannelExpr(res, channel));
                    byte[] b = host.EvaluateBytes(TreeRigBaker.ChannelExpr(res, channel));
                    Assert.AreEqual(a, b,
                        $"{key} {channel}: two renders of the same cell differ. Every exactness " +
                        "claim below rests on this — chase it before anything else.");
                }
            }
        }

        // =================================================================================
        // ⭐ THE ONE THAT MATTERS: the committed pixels ARE the rig's
        // =================================================================================

        /// <summary>
        /// Every committed sheet of the live kit — every species, every season that draws its own
        /// sheets, every channel <see cref="TreeKitCatalog.ChannelsFor"/> routes to it — is the glue's
        /// FRESH cell, byte for byte, in its column; and the committed palette is exactly the rows
        /// <see cref="TreePass4Baker.PaletteRows"/> reads off the contract, snow row at the bottom.
        ///
        /// <para>Re-pointed at pass 4 on the switch (2026-09-27). It compared each pass-3 sheet with
        /// <c>TreeRig3.render()</c>; the pass-4 bake reads its cells from <c>HHTreePass4.cell</c> after
        /// one <c>HHTreePass4.bake</c> per species, by the baker's exact call, so that is what a fresh
        /// render of the rig means now.</para>
        /// </summary>
        [Test]
        public void CommittedSheets_AreBitExact_AgainstAFreshRigRender()
        {
            var contract = LoadContract();
            using var host = CreateLiveTreeHost();

            int sheets = 0, cells = 0, routed = 0;
            foreach (var entry in contract.trees)
            {
                TreePass4TempBake.GlueBake(host, contract, entry);
                int cols = entry.sheetW / entry.cellW;
                foreach (string season in entry.seasons)
                foreach (var channel in TreeKitCatalog.ChannelsFor(entry, season))
                {
                    routed++;
                    string assetPath = TreeKitCatalog.SheetPath(entry.species, entry.stage, season, channel);
                    string full = Path.Combine(RepoRoot, assetPath);
                    Assert.IsTrue(File.Exists(full), $"Missing committed sheet: {assetPath}");

                    Texture2D tex = Decode(File.ReadAllBytes(full));
                    try
                    {
                        Assert.AreEqual(entry.sheetW, tex.width, $"{assetPath}: sheet width");
                        Assert.AreEqual(entry.sheetH, tex.height, $"{assetPath}: sheet height");

                        Color32[] px = tex.GetPixels32();
                        for (int v = 0; v < cols; v++)
                        {
                            byte[] fresh = host.EvaluateBytes(
                                $"HHTreePass4.cell({TreePass4TempBake.Js(season)}, " +
                                $"{TreePass4TempBake.Js(TreePass4Baker.GlueChannel(channel))}, {v})");
                            Assert.AreEqual(entry.cellW * entry.cellH * 4, fresh.Length,
                                $"{assetPath} variant {v}: the glue's cell is not the contract's cell.");
                            int mismatched = CompareCell(px, tex.width, tex.height, entry, v, fresh);
                            Assert.AreEqual(0, mismatched,
                                $"{assetPath} variant {v}: {mismatched} px differ from the glue's fresh " +
                                $"{channel} cell. The bake is not a paraphrase of the rig — if this " +
                                "fires, either the rig, its maps or the glue changed (re-bake) or the " +
                                "blit is wrong.");
                            cells++;
                        }
                        sheets++;
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(tex);
                    }
                }
                host.Execute("HHTreePass4.release();");
            }
            host.Execute("delete globalThis.HHTreePass4Last;");

            Assert.Greater(routed, 0, "The contract routes no sheets at all.");
            Assert.AreEqual(routed, sheets, "Not every routed sheet was compared.");

            // The palette: row 0 — the BOTTOM texel row, as the shader's Load numbers it — is the snow
            // row, and each season row's paletteRow is its gap row.
            string[][] rows = TreePass4Baker.PaletteRows(contract);
            Texture2D pal = Decode(File.ReadAllBytes(Path.Combine(RepoRoot, TreeKitCatalog.PalettePath)));
            try
            {
                Assert.AreEqual(TreeKitCatalog.PaletteWidth, pal.width, "palette width");
                Assert.AreEqual(rows.Length, pal.height, "palette rows");
                Color32[] p = pal.GetPixels32();
                for (int r = 0; r < rows.Length; r++)
                for (int x = 0; x < pal.width; x++)
                {
                    Color32 c = p[r * pal.width + x];
                    Assert.AreEqual(255, c.a, $"palette ({x}, {r}) is not opaque");
                    Assert.AreEqual(rows[r][x], TreePass4TempBake.Hex(c),
                        $"palette row {r} colour {x} is not the contract's " +
                        (r == 0 ? "snow row." : "gap row for it."));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pal);
            }

            Debug.Log($"[tree-golden] {sheets} sheets / {cells} cells are BIT-EXACT against the glue's " +
                      $"fresh cells, and the {rows.Length}-row palette is the contract's.");
        }

        [Test]
        public void CommittedSheets_WouldRejectAOnePixelVerticalShift()
        {
            // ---- MEASURED SABOTAGE for the exactness claim above ---------------------------
            // "Bit-exact" is only evidence if a near-miss fails. Shift the comparison window one
            // row and count what breaks; a sprite that survived that would mean the diff is blind.
            var contract = LoadContract();
            using var host = CreateLiveTreeHost();

            var entry = TreeKitCatalog.Find(contract, "RedSpruce", Stage);
            Assert.IsNotNull(entry, "RedSpruce/mature is the reference species for this suite.");

            string assetPath = TreeKitCatalog.SheetPath(entry.species, entry.stage, Season,
                                                        TreeKitCatalog.Channel.Albedo);
            Texture2D tex = Decode(File.ReadAllBytes(Path.Combine(RepoRoot, assetPath)));
            try
            {
                Color32[] px = tex.GetPixels32();
                TreePass4TempBake.GlueBake(host, contract, entry);
                byte[] fresh = host.EvaluateBytes(
                    $"HHTreePass4.cell({TreePass4TempBake.Js(Season)}, " +
                    $"{TreePass4TempBake.Js(TreePass4Baker.GlueChannel(TreeKitCatalog.Channel.Albedo))}, 0)");

                int aligned = CompareCell(px, tex.width, tex.height, entry, 0, fresh, rowShift: 0);
                int shifted = CompareCell(px, tex.width, tex.height, entry, 0, fresh, rowShift: 1);

                Assert.AreEqual(0, aligned);
                double pct = 100.0 * shifted / (entry.cellW * entry.cellH);
                Debug.Log($"[tree-golden] sabotage: a 1-row shift breaks {shifted} of " +
                          $"{entry.cellW * entry.cellH} px = {pct:F2}% of the Red Spruce cell. Measured " +
                          "2026-09-27 on the pass-4.1 sheet: 8398 of 81008 px = 10.37%, the tree " +
                          "being 19% of a cell padded by the wind's reach.");
                Assert.Greater(pct, 5.0,
                    "A one-row shift must break a meaningful fraction of the cell, or the exact " +
                    "comparison above could pass on a mis-blitted sheet.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
                host.Execute("HHTreePass4.release(); delete globalThis.HHTreePass4Last;");
            }
        }

        // =================================================================================
        // helpers
        // =================================================================================

        /// <summary>One of the rig's grayscale masks as bytes. <c>render()</c> returns them as
        /// <c>Uint8Array</c>, and the host's bulk readback wants a clamped array, so re-wrap.</summary>
        static byte[] ReadMask(IRigScriptHost host, string resultExpr, string which) =>
            host.EvaluateBytes(
                $"(function(){{var r={resultExpr};return new Uint8ClampedArray(r.masks.{which});}})()");

        /// <summary>Loading the committed PNG into a throwaway Texture2D reads its pixels without
        /// flipping <c>isReadable</c> on the shipped asset (the <c>PuntGoldenMasterTests</c>
        /// pattern).</summary>
        static Texture2D Decode(byte[] png)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            Assert.IsTrue(t.LoadImage(png, markNonReadable: false), "Failed to decode PNG.");
            return t;
        }

        /// <summary>
        /// Pixels differing between sheet cell <paramref name="col"/> and a fresh rig cell.
        /// The sheet is Unity-orientated (bottom-origin <see cref="Texture2D.GetPixels32"/>) while
        /// the rig's buffer is top-origin, so the row index is flipped exactly once — the same
        /// single flip <c>RigBaker.Blit</c> makes on the way in.
        /// </summary>
        static int CompareCell(IReadOnlyList<Color32> sheet, int sheetW, int sheetH,
                               TreeKitCatalog.Entry entry, int col, byte[] cell, int rowShift = 0)
        {
            int mismatched = 0;
            for (int y = 0; y < entry.cellH; y++)
            {
                int unityY = sheetH - 1 - (y + rowShift);
                if (unityY < 0 || unityY >= sheetH) { mismatched += entry.cellW; continue; }
                for (int x = 0; x < entry.cellW; x++)
                {
                    Color32 got = sheet[unityY * sheetW + col * entry.cellW + x];
                    int s = (y * entry.cellW + x) * 4;
                    if (got.r != cell[s] || got.g != cell[s + 1] ||
                        got.b != cell[s + 2] || got.a != cell[s + 3]) mismatched++;
                }
            }
            return mismatched;
        }
    }
}
