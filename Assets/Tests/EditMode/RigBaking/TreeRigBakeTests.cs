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

        [Test]
        public void TreeRig_RunsUnmodified_AndNeedsNoShim()
        {
            // No canvas mailbox, no string widening, no globals patched in first: the difference
            // between this rig and every other one in the repo, and the reason the baker calls its
            // public API directly.
            using var host = RigScriptHostFactory.Create();
            string source = File.ReadAllText(Path.Combine(RepoRoot, TreeKitCatalog.RigScriptPath));
            Assert.DoesNotThrow(() => host.Execute(source),
                "treeIsoRig.js must run in a BARE host. If this throws, something in the rig now " +
                "needs an environment global — and the shim belongs in host code, never in the " +
                "art director's file (ADR 0021 §5).");

            foreach (string fn in new[] { "render", "packMask", "normalView", "sheetSpec", "cellOf" })
                Assert.IsTrue(host.EvaluateBool($"typeof TreeRig.{fn} === 'function'"),
                    $"TreeRig.{fn}() is missing — the baker calls it directly.");
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
                      $"= {pct:F2}% of the cell. Measured 2026-07-26: 5405 px / 29.60%.");
            Assert.Greater(pct, 10.0,
                "An R↔G swap must change a LOT of the cell, or this assert is decoration — the key " +
                "and rim channels would be nearly interchangeable and the order would not matter.");
        }

        [Test]
        public void ChannelCoverage_AlbedoAndMaskAgree_ButTheNormalOmitsTheKeyline()
        {
            // A genuine asymmetry the kit README does not mention and a shader author would trip
            // over: the rig composites a 1 px keyline ring OUTSIDE the volume, so those pixels are
            // opaque in rgba (and therefore in the mask's A) but carry no surface normal. Light the
            // keyline from the mask; never from the normal.
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
            Assert.Greater(inMaskNotNormal, 0,
                "The keyline ring should leave the normal map smaller than the mask. If this is 0 " +
                "the rig stopped drawing the keyline — a look change, not a bake bug, but the " +
                "contract's coverageNote is now wrong.");

            // Unit normals, within 8-bit quantisation.
            Assert.That(minLen, Is.GreaterThan(0.97), "a decoded normal shorter than 0.97 is not a normal");
            Assert.That(maxLen, Is.LessThan(1.03));

            Debug.Log($"[tree-coverage] {Species}: albedo/mask {albedoCov} px, normal {normalCov} px, " +
                      $"keyline-only {inMaskNotNormal} px ({100.0 * inMaskNotNormal / maskCov:F1}% of " +
                      $"coverage). Decoded |n| ∈ [{minLen:F4}, {maxLen:F4}]. " +
                      "Measured 2026-07-26: 7601 / 6990 / 611 px.");
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
                      $"{worstPct:F2}% and {bestPct:F2}% of a cell. Measured 2026-07-26: " +
                      "5.04% (Red Spruce) to 18.56% (Trembling Aspen).");
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
                      $"{ShippedMaterialTrunkAnchor}. Measured 2026-07-26: 0.0833 (BlackSpruce) to " +
                      "0.1447 (RedOak).");

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
