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
    /// <para>A green test run against our own constants proves nothing here. <c>treeIsoRig.js</c>
    /// runs in V8 inside the editor, so every committed pixel can be compared against a FRESH
    /// <c>TreeRig.render()</c>, byte for byte — the same shape of proof that settled the boat bakes
    /// (<c>PuntGoldenMasterTests</c>), except that for a rig-native kit the answer should be exact
    /// rather than "modulo a revision".</para>
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

        static IRigScriptHost CreateTreeHost()
        {
            var host = RigScriptHostFactory.Create();
            TreeRigBaker.InstallRig(host);
            return host;
        }

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
        // the rig runs unmodified, and exposes exactly what this baker calls
        // =================================================================================

        /// <summary>The five entry points the baker calls straight off the rig's global.</summary>
        static readonly string[] BakerEntryPoints =
        {
            "render", "packMask", "normalView", "sheetSpec", "cellOf",
        };

        [Test]
        public void TreeRig_RunsUnmodified_AndNeedsNoShim()
        {
            // No canvas mailbox, no string widening, no globals patched in first: the difference
            // between this rig and every other one in the repo, and the reason the baker calls its
            // public API directly.
            //
            // ⚠️ The rig FILE and the rig GLOBAL are both read from TreeKitCatalog, never spelled
            // out here. They were hardcoded as treeIsoRig.js/TreeRig until the pass-2 swap
            // (2026-07-29), which is precisely the shape of test that keeps passing against the OLD
            // rig after the pipeline has moved on.
            using var host = RigScriptHostFactory.Create();
            string rig = TreeKitCatalog.RigScriptPath, g = TreeKitCatalog.RigGlobalName;
            string source = File.ReadAllText(Path.Combine(RepoRoot, rig));
            Assert.DoesNotThrow(() => host.Execute(source),
                $"{rig} must run in a BARE host. If this throws, something in the rig now " +
                "needs an environment global — and the shim belongs in host code, never in the " +
                "art director's file (ADR 0021 §5).");

            Assert.IsTrue(host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"),
                $"{rig} ran but did not install globalThis.{g}.");

            foreach (string fn in BakerEntryPoints)
                Assert.IsTrue(host.EvaluateBool($"typeof {g}.{fn} === 'function'"),
                    $"{g}.{fn}() is missing — the baker calls it directly.");
        }

        [Test]
        public void PassTwoRig_KeepsEveryContractConstant_SoTheSwapWasAReBakeAndNotAReDesign()
        {
            // ⭐ THE PROOF BEHIND THE PASS-2 SWAP BEING TWO CONSTANTS AND A RE-BAKE.
            //
            // treeIsoRig2.js changed what gets BUILT (masses instead of one cloud, Worley leaf cells
            // instead of per-pixel noise, a serrated outline, visible branches) and therefore every
            // species' measured cell and pivot. It changed NOTHING that Trees.json records as a
            // world constant. That distinction is the whole reason the sprite-light mask contract,
            // the wind shader's _TrunkAnchor and the reflection wiring all survived the swap
            // untouched — so it is asserted rather than asserted-in-a-commit-message.
            //
            // Both passes are loaded into ONE host on purpose: they install different globals
            // (TreeRig vs TreeRig2), so they cannot collide, and comparing them in-process beats
            // comparing either against a number typed in here.
            using var host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Path.Combine(RepoRoot, TreeKitCatalog.PreviousRigScriptPath)));
            host.Execute(File.ReadAllText(Path.Combine(RepoRoot, TreeKitCatalog.RigScriptPath)));

            string p1 = TreeKitCatalog.PreviousRigGlobalName, p2 = TreeKitCatalog.RigGlobalName;
            Assert.AreNotEqual(p1, p2, "The two passes must install DIFFERENT globals.");
            foreach (string g in new[] { p1, p2 })
                Assert.IsTrue(host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"),
                    $"globalThis.{g} did not install — this test needs both passes side by side.");

            // Every scalar the contract carries as a world constant.
            foreach (string k in new[] { "PPU", "RIM_PX", "MIN_BODY", "MIN_R", "SWAY", "VARIANTS",
                                         "ELEV", "CE", "SE" })
            {
                double a = host.EvaluateNumber($"{p1}.{k}"), b = host.EvaluateNumber($"{p2}.{k}");
                Assert.AreEqual(a, b, 1e-12,
                    $"{k} differs between the two passes ({a} vs {b}). Trees.json publishes this as " +
                    "a constant the pixels were built under — if a pass really changed it, the " +
                    "consumers of that number (Tree.mat, the wind shader, SpriteLightMath) all need " +
                    "re-deriving, and that is not a re-bake.");
            }

            // The light vectors — the mask's whole meaning.
            foreach (string vec in new[] { "key", "rim" })
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(host.EvaluateNumber($"{p1}.LIGHT.{vec}[{i}]"),
                                host.EvaluateNumber($"{p2}.LIGHT.{vec}[{i}]"), 1e-12,
                    $"LIGHT.{vec}[{i}] moved between passes — every baked mask byte means something " +
                    "different than the last bake's did.");

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
                Assert.AreEqual(host.EvaluateString($"JSON.stringify({p1}.{expr})"),
                                host.EvaluateString($"JSON.stringify({p2}.{expr})"),
                    $"{expr} differs between the two passes. Species keys are the sheet stems and " +
                    "the prefab names, their ORDER is what AcadianTreeCatalog.Scan publishes to the " +
                    "paint tool, and a stage's multiplier is what 'mature' MEANS.");

            // ---- MEASURED SABOTAGE: the pixels DID change, or the swap was a no-op ----------
            // Without this the test above would also pass if someone pointed RigScriptPath back at
            // pass 1 — identical constants AND identical pixels.
            string res1 = $"{p1}.render('RedSpruce',{{variant:0,season:'{Season}',frame:0,stage:'{Stage}'}})";
            string res2 = $"{p2}.render('RedSpruce',{{variant:0,season:'{Season}',frame:0,stage:'{Stage}'}})";
            byte[] a1 = host.EvaluateBytes($"{res1}.rgba"), a2 = host.EvaluateBytes($"{res2}.rgba");
            Debug.Log($"[tree-pass2] RedSpruce/{Stage}/{Season} albedo: pass 1 is {a1.Length / 4} px, " +
                      $"pass 2 is {a2.Length / 4} px.");
            Assert.AreNotEqual(a1, a2,
                "Pass 1 and pass 2 rendered the SAME Red Spruce. Either RigScriptPath is still " +
                "pointing at pass 1, or the drop was not the revised rig.");

            // ---- ADR 0031: BOTH passes are retired, and pass 1 is the reason it matters --------
            // Pass 1 is not dead code. TreeKitCatalog.HeldBackSpecies keeps Tamarack on it, so
            // Tamarack's shipped sheets come off THIS rig — gating only pass 2 would have left one
            // species inked forever, and no pass-2 test could ever have caught it.
            foreach (string g in new[] { p1, p2 })
            {
                Assert.IsTrue(host.EvaluateBool($"{g}.KEYLINE_DEFAULT === false"),
                    $"{g}.KEYLINE_DEFAULT is not false. The outline is retired from world art " +
                    "(ADR 0031) and BOTH passes bake shipped sheets — pass 2 for the kit, pass 1 " +
                    $"for {string.Join("/", TreeKitCatalog.HeldBackSpecies)}.");

                // The control: the flag must still be reachable, or "retired" would be
                // indistinguishable from "the ring pass was deleted". Probed on the held-back
                // species where there is one, since that is the one pass 1 still bakes.
                string probe = TreeKitCatalog.HeldBackSpecies.FirstOrDefault() ?? "RedSpruce";
                string plain = $"{g}.render('{probe}',{{variant:0,season:'{Season}',frame:0,stage:'{Stage}'}})";
                string inked = $"{g}.render('{probe}',{{variant:0,season:'{Season}',frame:0,stage:'{Stage}',outline:true}})";
                Assert.AreNotEqual(host.EvaluateBytes($"{plain}.rgba"), host.EvaluateBytes($"{inked}.rgba"),
                    $"{g}: {{outline:true}} rendered {probe} identically to the default, so the A/B " +
                    "arm is gone. Keep the ring code — ADR 0031 gates it, it does not delete it.");
            }
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
            using var host = CreateTreeHost();

            int worstPad = 0;
            foreach (var entry in contract.trees)
            {
                var spec = TreeRigBaker.ReadSheetSpec(host, entry.species, entry.stage);

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
            using var host = CreateTreeHost();

            foreach (var entry in contract.trees)
            {
                var spec = TreeRigBaker.ReadSheetSpec(host, entry.species, entry.stage);

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
            using var host = CreateTreeHost();

            float lo = float.MaxValue, hi = float.MinValue;
            string loKey = null, hiKey = null;

            foreach (var entry in contract.trees)
            {
                var spec = TreeRigBaker.ReadSheetSpec(host, entry.species, entry.stage);
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
                      $"{ShippedMaterialTrunkAnchor}. Measured 2026-07-29 (pass-2 rig): 0.0519 " +
                      "(TremblingAspen) to 0.0922 (WhiteCedar); pass 1 was 0.0833 (BlackSpruce) to " +
                      "0.1447 (RedOak). ⚠️ The pass-2 band sits ENTIRELY below the shipped 0.14, so " +
                      "the single material value now over-anchors all ten species rather than " +
                      "eight of the ten — the case for the per-renderer anchor got stronger, not " +
                      "weaker.");

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

        [Test]
        public void CommittedSheets_AreBitExact_AgainstAFreshRigRender()
        {
            var contract = LoadContract();
            using var host = CreateTreeHost();

            int sheets = 0, cells = 0;
            foreach (var entry in contract.trees)
            foreach (string season in entry.seasons)
            foreach (var channel in TreeKitCatalog.Channels)
            {
                string assetPath = TreeKitCatalog.SheetPath(entry.species, entry.stage, season, channel);
                string full = Path.Combine(RepoRoot, assetPath);
                Assert.IsTrue(File.Exists(full), $"Missing committed sheet: {assetPath}");

                Texture2D tex = Decode(File.ReadAllBytes(full));
                try
                {
                    Assert.AreEqual(entry.sheetW, tex.width, $"{assetPath}: sheet width");
                    Assert.AreEqual(entry.sheetH, tex.height, $"{assetPath}: sheet height");

                    Color32[] px = tex.GetPixels32();
                    int cols = entry.sheetW / entry.cellW;

                    for (int v = 0; v < cols; v++)
                    {
                        string res = TreeRigBaker.ResultExpr(entry.species, entry.stage, season,
                                                             variant: v, frame: 0);
                        byte[] fresh = host.EvaluateBytes(TreeRigBaker.ChannelExpr(res, channel));
                        int mismatched = CompareCell(px, tex.width, tex.height, entry, v, fresh);
                        Assert.AreEqual(0, mismatched,
                            $"{assetPath} variant {v}: {mismatched} px differ from a fresh " +
                            $"{channel} render. The bake is not a paraphrase of the rig — if this " +
                            "fires, either the rig changed (re-bake) or the blit is wrong.");
                        cells++;
                    }
                    sheets++;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }

            int expected = contract.trees.Length * TreeKitCatalog.Channels.Length;
            Assert.AreEqual(expected, sheets, "Not every claimed sheet was compared.");
            Debug.Log($"[tree-golden] {sheets} sheets / {cells} cells are BIT-EXACT against a fresh " +
                      "TreeRig render.");
        }

        [Test]
        public void CommittedSheets_WouldRejectAOnePixelVerticalShift()
        {
            // ---- MEASURED SABOTAGE for the exactness claim above ---------------------------
            // "Bit-exact" is only evidence if a near-miss fails. Shift the comparison window one
            // row and count what breaks; a sprite that survived that would mean the diff is blind.
            var contract = LoadContract();
            using var host = CreateTreeHost();

            var entry = TreeKitCatalog.Find(contract, "RedSpruce", Stage);
            Assert.IsNotNull(entry, "RedSpruce/mature is the reference species for this suite.");

            string assetPath = TreeKitCatalog.SheetPath(entry.species, entry.stage, Season,
                                                        TreeKitCatalog.Channel.Albedo);
            Texture2D tex = Decode(File.ReadAllBytes(Path.Combine(RepoRoot, assetPath)));
            try
            {
                Color32[] px = tex.GetPixels32();
                string res = TreeRigBaker.ResultExpr(entry.species, entry.stage, Season, 0, 0);
                byte[] fresh = host.EvaluateBytes(
                    TreeRigBaker.ChannelExpr(res, TreeKitCatalog.Channel.Albedo));

                int aligned = CompareCell(px, tex.width, tex.height, entry, 0, fresh, rowShift: 0);
                int shifted = CompareCell(px, tex.width, tex.height, entry, 0, fresh, rowShift: 1);

                Assert.AreEqual(0, aligned);
                double pct = 100.0 * shifted / (entry.cellW * entry.cellH);
                Debug.Log($"[tree-golden] sabotage: a 1-row shift breaks {shifted} of " +
                          $"{entry.cellW * entry.cellH} px = {pct:F2}% of the Red Spruce cell.");
                Assert.Greater(pct, 5.0,
                    "A one-row shift must break a meaningful fraction of the cell, or the exact " +
                    "comparison above could pass on a mis-blitted sheet.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
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
