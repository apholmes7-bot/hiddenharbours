using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The IMPORT half of the tree bake: what Unity did with the 30 committed sheets. Baking the
    /// right pixels and importing them wrongly produces art that looks fine and behaves wrongly,
    /// and two of these settings fail SILENTLY.
    ///
    /// <list type="bullet">
    ///   <item>🔴 <b>sRGB must be OFF on <c>_mask</c> and <c>_normal</c>.</b> They are data, not
    ///   colour. Leave it on and Unity gamma-curves the channels: the sprite still looks right in
    ///   the inspector, the lighting is simply wrong, and nothing but a numeric assert notices.
    ///   <c>ArtImportPipeline</c> stamps sRGB ON for everything under Art/, so this is an override
    ///   that can silently revert if the sheets are ever re-imported without the slicer.</item>
    ///   <item><b>Mesh Type must be <see cref="SpriteMeshType.Tight"/></b>, and the wind shader's
    ///   bend curve needs actual intermediate vertices — asserted as the CAPABILITY (three distinct
    ///   heights, one below this species' own anchor) rather than only the flag, following
    ///   <c>WindBendTessellationTests</c>.</item>
    ///   <item><b>No lossy compression</b> — a block-compressed mask is a wrong mask.</item>
    ///   <item><b>The pivot is the TRUNK FOOT</b>, per species, checked against the CONTRACT rather
    ///   than a literal. <c>ArtImportPipeline</c> defaults <c>/foliage/</c> to BottomCenter, which
    ///   would sink every species by its own flare pad.</item>
    ///   <item><b>≤ 2048 px on every axis</b> — over the cap Unity downscales silently and the
    ///   sprite COUNT still matches.</item>
    /// </list>
    ///
    /// <para>Importer metadata and sprite UVs only: no GPU, no render, nothing to gate on
    /// <c>GraphicsDeviceType.Null</c>.</para>
    /// </summary>
    public class TreeSheetImportTests
    {
        static TreeKitCatalog.Contract _contract;

        /// <summary>
        /// The rig's own rule-1 tolerance, as a percentage of foliage pixels too thin to carry a rim.
        /// ⚠️ <b>Not an exported constant</b> — <c>treeIsoRig3.js</c> spells it inline in
        /// <c>report.pass</c> as <c>sh.thin / sh.tot &lt;= 0.04</c>, unchanged from pass 2, so it is
        /// restated here WITH that provenance rather than silently invented, and
        /// <see cref="NothingIsHeldBack_TheSpeciesThatWasClearsTheGate_AndHerSheetsAreThisPass"/>
        /// is what catches the two drifting apart.
        /// </summary>
        const double RuleOneGatePct = 4.0;

        [SetUp]
        public void LoadContract()
        {
            string path = TreeKitCatalog.ContractPath;
            Assert.IsTrue(File.Exists(path),
                $"No contract at {path}. Run Hidden Harbours ▸ Art ▸ Bake Acadian Trees.");
            _contract = JsonUtility.FromJson<TreeKitCatalog.Contract>(File.ReadAllText(path));
            Assert.IsNotNull(_contract?.trees);
            Assert.IsNotEmpty(_contract.trees);
        }

        static (string path, string stem, TreeKitCatalog.Entry entry, TreeKitCatalog.Channel channel)[] Sheets()
        {
            var rows = new System.Collections.Generic.List<(string, string, TreeKitCatalog.Entry,
                                                            TreeKitCatalog.Channel)>();
            foreach (var e in _contract.trees)
            foreach (string season in e.seasons)
            foreach (var c in TreeKitCatalog.Channels)
            {
                string path = TreeKitCatalog.SheetPath(e.species, e.stage, season, c);
                rows.Add((path, Path.GetFileNameWithoutExtension(path), e, c));
            }
            return rows.ToArray();
        }

        static TextureImporter ImporterFor(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(imp, $"{path}: no TextureImporter — is the PNG committed?");
            return imp;
        }

        /// <summary>A V8 host with the CURRENT rig installed — needed only by the held-back test, which
        /// has to re-measure the rig's own verdict rather than trust a number typed in here.</summary>
        static IRigScriptHost CreateTreeHost()
        {
            var host = RigScriptHostFactory.Create();
            TreeRigBaker.InstallRig(host);
            return host;
        }

        // =================================================================================

        [Test]
        public void TheKit_IsTheRigsSpeciesMinusTheHeldBackOnes_TimesThreeChannels()
        {
            // Expressed STRUCTURALLY rather than as a literal 10 or 9: the committed set is exactly
            // "every species the rig declares, minus the ones held back at a previous pass", so
            // holding one back or releasing one moves this assert with it instead of breaking it.
            using var host = CreateTreeHost();
            var declared = TreeRigBaker.ReadSpeciesKeys(host);
            int expectedSpecies = declared.Count(k => !TreeKitCatalog.IsHeldBack(k));

            Assert.AreEqual(expectedSpecies, _contract.trees.Length,
                $"The rig declares {declared.Count} species and {TreeKitCatalog.HeldBackSpecies.Length} " +
                $"are held back, so this wave bakes {expectedSpecies} at mature/summer. Held back: " +
                string.Join(", ", TreeKitCatalog.HeldBackSpecies) + ".");
            foreach (string held in TreeKitCatalog.HeldBackSpecies)
                Assert.IsFalse(_contract.trees.Any(t => t.species == held),
                    $"{held} is held back but reached the contract.");

            var sheets = Sheets();
            Assert.AreEqual(expectedSpecies * TreeKitCatalog.Channels.Length, sheets.Length);

            foreach (var (path, _, _, _) in sheets)
                Assert.IsTrue(File.Exists(path), $"Missing committed sheet: {path}");

            // Nothing unclaimed may sit in the folder: a stray sheet has no cell and no pivot, and
            // the slicer refuses rather than guessing either.
            string[] onDisk = Directory.GetFiles(TreeKitCatalog.TreesRoot, "*.png")
                                       .Select(Path.GetFileName).OrderBy(s => s).ToArray();
            Assert.AreEqual(30, onDisk.Length,
                "Files under " + TreeKitCatalog.TreesRoot + " that the contract does not claim:\n  " +
                string.Join("\n  ", onDisk.Where(f =>
                    TreeKitCatalog.EntryForStem(_contract, Path.GetFileNameWithoutExtension(f)) == null)));
        }

        [Test]
        public void EverySheet_ImportsMultipleMode_WithOneSpritePerVariant()
        {
            foreach (var (path, stem, entry, _) in Sheets())
            {
                var imp = ImporterFor(path);
                Assert.AreEqual(SpriteImportMode.Multiple, imp.spriteImportMode,
                    $"{stem}: not sliced — run Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Slice Acadian Tree Sheets.");

                // ⚠️ These import as spriteMode Multiple; LoadAssetAtPath<Sprite> returns null.
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                int expected = (entry.sheetW / entry.cellW) * (entry.sheetH / entry.cellH);
                Assert.AreEqual(expected, sprites.Length, $"{stem}: slice count");

                foreach (var s in sprites)
                {
                    Assert.AreEqual(entry.cellW, Mathf.RoundToInt(s.rect.width), $"{s.name}: cell width");
                    Assert.AreEqual(entry.cellH, Mathf.RoundToInt(s.rect.height), $"{s.name}: cell height");
                }
            }
        }

        [Test]
        public void EverySheet_PivotsOnTheTrunkFoot_NotTheBottomOfTheCell()
        {
            foreach (var (path, stem, entry, _) in Sheets())
            {
                Vector2 expected = TreeKitCatalog.PivotPixels(entry);
                foreach (var s in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                {
                    Assert.AreEqual(expected.x, s.pivot.x, 0.01f,
                        $"{s.name}: pivot.x should be the trunk foot at {expected.x} px.");
                    Assert.AreEqual(expected.y, s.pivot.y, 0.01f,
                        $"{s.name}: pivot.y should be {expected.y} px — the flare pad — NOT 0. " +
                        $"Bottom-centre would sink {entry.species} {entry.nearFlarePad} px " +
                        $"({entry.nearFlarePad / 32f:F2} m) into the ground.");
                }

                // The trap in one assert: bottom-centre must be measurably WRONG for this species.
                Assert.Greater(expected.y, 0.5f,
                    $"{stem}: the trunk-foot pivot collapsed to the cell floor — then this kit " +
                    "would have no pivot trap at all, which contradicts the rig's own pad field.");
            }
        }

        [Test]
        public void MaskAndNormalSheets_ImportWithSRgbOFF_AndTheAlbedoWithItOn()
        {
            foreach (var (path, stem, _, channel) in Sheets())
            {
                var imp = ImporterFor(path);
                bool colour = TreeKitCatalog.IsColourChannel(channel);
                Assert.AreEqual(colour, TreeSheetSlicer.SRgbOf(imp),
                    colour
                        ? $"{stem}: the albedo IS colour — sRGB stays on."
                        : $"{stem}: 🔴 sRGB must be OFF. This channel is DATA (mask = key/rim/depth/" +
                          "coverage, normal = a view-space vector). With sRGB on Unity applies a " +
                          "gamma curve and the sprite still LOOKS fine — only a numeric assert " +
                          "catches it. ArtImportPipeline stamps sRGB ON for everything under Art/, " +
                          "so re-importing without TreeSheetSlicer silently reverts this.");
            }
        }

        [Test]
        public void EverySheet_IsUncompressedPointFilteredPixelArt()
        {
            foreach (var (path, stem, _, _) in Sheets())
            {
                var imp = ImporterFor(path);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, imp.textureCompression,
                    $"{stem}: a block-compressed mask is a wrong mask, and a block-compressed " +
                    "normal is worse.");
                Assert.IsFalse(imp.crunchedCompression, $"{stem}: crunch is lossy.");
                Assert.AreEqual(FilterMode.Point, imp.filterMode, $"{stem}: pixel art is Point-filtered.");
                Assert.IsFalse(imp.mipmapEnabled, $"{stem}: 2D art takes no mips.");
                Assert.AreEqual(ArtImportPipeline.PixelsPerUnit, imp.spritePixelsPerUnit, 1e-4f,
                    $"{stem}: PPU 32 is the scale standard — 32 px = 1 m.");
                Assert.AreEqual(TextureImporterType.Sprite, imp.textureType, $"{stem}: Sprite (2D and UI).");
            }
        }

        [Test]
        public void EverySheet_IsWithinTheImportSizeCap_SoNothingWasSilentlyDownscaled()
        {
            foreach (var (path, stem, entry, _) in Sheets())
            {
                var imp = ImporterFor(path);
                Assert.LessOrEqual(entry.sheetW, TreeKitCatalog.ImportSizeCap, $"{stem}: sheet width");
                Assert.LessOrEqual(entry.sheetH, TreeKitCatalog.ImportSizeCap, $"{stem}: sheet height");
                Assert.GreaterOrEqual(imp.maxTextureSize, Mathf.Max(entry.sheetW, entry.sheetH),
                    $"{stem}: the importer caps below the sheet's own size — Unity has already " +
                    "downscaled it, and the sprite COUNT would still come out right.");

                // The sliced rects are the proof: a downscale refits them and the cell size drifts.
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                Assert.IsNotEmpty(sprites, $"{stem}: no Sprite sub-assets");
                float widest = sprites.Max(s => s.rect.xMax);
                Assert.AreEqual(entry.sheetW, Mathf.RoundToInt(widest), $"{stem}: rects do not span " +
                    "the contract's sheet width — the classic signature of a silent downscale.");
            }
        }

        // =================================================================================
        // the tessellation the wind shader depends on
        // =================================================================================

        [Test]
        public void EverySheet_IsTightMeshed_SoTheBendCurveCanBeEvaluated()
        {
            foreach (var (path, stem, _, _) in Sheets())
                Assert.AreEqual(SpriteMeshType.Tight, TreeKitCatalog.MeshTypeOf(ImporterFor(path)),
                    $"{stem}: FullRect is a 4-vertex quad. HiddenHarboursTreeWind shapes " +
                    "bendW = smoothstep(_TrunkAnchor,1,uv.y)² in the VERTEX stage, so on a quad it " +
                    "is only evaluated at uv.y 0 and 1 and the rasteriser interpolates linearly: " +
                    "the squaring collapses and _TrunkAnchor goes inert for every value. All 43 of " +
                    "the old hand-drawn trees shipped that way (PR #297).");
        }

        [Test]
        public void TheAnchorLine_ActuallyCutsEverySpritesMesh_NotJustTouchesItsBottom()
        {
            // Tight is the mechanism; this is the requirement, and it is measured in ABSOLUTE atlas
            // uv on purpose.
            //
            // ⚠️ The equivalent assert on the OLD tree set (WindBendTessellationTests
            // .TreeSprites_HaveAVertexBelowTheTrunkAnchor…) normalises uv.y against the sprite's own
            // min/max and then asks for a vertex below the anchor — but the minimum vertex maps to
            // exactly 0, and 0 < anchor for every positive anchor, so that assert cannot fail. It is
            // vacuous. Here the anchor must genuinely SPLIT the mesh: at least one vertex strictly
            // below it and at least one strictly above, so there is a row to hold still and a row to
            // move. Absolute uv works because these sheets are ONE sway row — the sprite rect spans
            // the full sheet height, so atlas uv.y and cell-normalised height are the same number,
            // which is what the shader's saturate(IN.uv.y) reads.
            const int MaxVerticesPerSprite = 2000;

            foreach (var (path, stem, entry, _) in Sheets())
            {
                Assert.AreEqual(1, entry.sheetH / entry.cellH,
                    $"{stem}: this test's absolute-uv reasoning assumes one sway row. If rows grow, " +
                    "normalise uv.y against each rect's own span first.");
                Assert.Greater(entry.trunkAnchor, 0f, $"{stem}: a zero anchor plants nothing.");

                foreach (Sprite s in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                {
                    Vector2[] uv = s.uv;
                    Assert.IsNotEmpty(uv, $"{s.name}: no UVs");
                    Assert.LessOrEqual(uv.Length, MaxVerticesPerSprite,
                        $"{s.name}: {uv.Length} vertices — heavier than the foliage budget allows " +
                        "(rule 7). Profile before raising this.");

                    float lo = uv.Min(v => v.y), hi = uv.Max(v => v.y);
                    Assert.Greater(hi - lo, 1e-6f, $"{s.name}: degenerate UV range");

                    int distinctRows = uv.Select(v => Mathf.RoundToInt(v.y * entry.cellH))
                                         .Distinct().Count();
                    Assert.Greater(distinctRows, 2,
                        $"{s.name}: only {distinctRows} distinct vertex heights — a bend curve " +
                        "cannot be expressed with fewer than 3, it interpolates linearly.");

                    int below = uv.Count(v => v.y < entry.trunkAnchor);
                    Assert.Greater(below, 0,
                        $"{s.name}: no vertex below {entry.species}'s own trunk anchor " +
                        $"({entry.trunkAnchor:F4} = pad {entry.nearFlarePad} / cell {entry.cellH}), " +
                        "so there is no row the shader can hold still while the crown sways. " +
                        $"The mesh spans uv.y {lo:F4}..{hi:F4}.");
                    Assert.Less(below, uv.Length,
                        $"{s.name}: EVERY vertex is below the anchor — nothing would sway at all.");
                }
            }
        }

        // =================================================================================

        [Test]
        public void TheSlicersOwnVerifier_Agrees()
        {
            // One test that runs the same check the bake menu and the batch entry point run, so
            // "imported correctly" has a single definition rather than a test-only one.
            Assert.IsTrue(TreeSheetSlicer.VerifyAll(logEachPass: false),
                "TreeSheetSlicer.VerifyAll reported problems — see the errors above.");
        }

        [Test]
        public void TheContract_RecordsTheProvenanceOfThePixels()
        {
            Assert.AreEqual(TreeKitCatalog.RigScriptPath, _contract.rig);
            Assert.AreEqual(TreeKitCatalog.RigGlobalName, _contract.global);
            Assert.AreEqual((int)ArtImportPipeline.PixelsPerUnit, _contract.ppu,
                "PPU 32 is the locked scale standard — the contract must state the same one.");
            Assert.AreEqual(40f, _contract.camera.elevDeg, 1e-4f,
                "¾ from the south at 40°, the same camera as the boat, rock and shoreline bakes " +
                "(ADR 0006/0022) — a tree that disagreed would not stand in the same world.");
            Assert.AreEqual(2, _contract.rules.rimPx);
            Assert.AreEqual(6, _contract.rules.minBodyPx);
            Assert.AreEqual(5, _contract.rules.minClumpRadiusPx);
            Assert.AreEqual(4, _contract.sheet.cols, "4 variant columns.");
            Assert.AreEqual(TreeRigBaker.SwayRowsBaked, _contract.sheet.rows);

            foreach (var e in _contract.trees)
            {
                // 🔴 UNCONDITIONAL, over the COMMITTED SET. There is no exemption list: a species whose
                // pass-2 output the rig itself rejects is HELD BACK from the bake entirely
                // (TreeKitCatalog.HeldBackSpecies), so it never reaches this contract and this gate
                // never has to make an exception for it. Coordinator ruling 2026-07-29 — do not loosen
                // this into a per-species allowance.
                Assert.IsTrue(e.audit.pass,
                    $"{e.species}: the rig's own rule audit FAILED (thinPct {e.audit.thinPct}). " +
                    "That is an art-director rig matter, not something to bake past. If this species " +
                    "must not ship, hold it back in TreeKitCatalog.HeldBackSpecies so it leaves the " +
                    "contract — never wave it through here.");
                Assert.IsFalse(e.audit.underFloor,
                    $"{e.species}: under the mass floor — a shrub, not a tree, at this PPU.");
                Assert.Greater(e.metres, 1.0f, $"{e.species}: implausible true height.");
                Assert.AreEqual(TreeRigBaker.DefaultStage, e.stage);
                Assert.AreEqual(new[] { TreeRigBaker.DefaultSeason }, e.seasons);
            }
        }

        /// <summary>
        /// ✅ <b>NOTHING IS HELD BACK ANY MORE — AND THIS IS THE INVERSE OF THE TEST THAT SAID SO.</b>
        ///
        /// <para>It used to be <c>TheHeldBackSpecies_IsExcludedByMeasurement_AndItsSheetsAreStillThe
        /// PreviousPass</c>, and it proved the 2026-07-29 exclusion in three directions at once: the
        /// tamarack was absent from the contract, the rig still rejected her when re-measured live,
        /// and her sheets were still the previous pass's pixels. Its own first assert said what to do
        /// the day that stopped being true: <i>"If the rig now clears its own gate for every species,
        /// delete this test and the HeldBackSpecies entry together."</i></para>
        ///
        /// <para>Pass 3 (2026-09-02) is that day. The ENTRY is deleted — <see cref="TreeKitCatalog
        /// .HeldBackSpecies"/> is empty. The TEST is inverted rather than deleted, because the three
        /// facts it checked are exactly the three that must now hold the other way round, and because
        /// a hold-back mechanism with no test is how the next one ships wrong. The same move
        /// <c>HullLevelTagBakeTests.TheStandInLidTable_StaysRetired</c> makes for its retired table:
        /// the ledger inverts, it does not disappear.</para>
        ///
        /// <list type="number">
        ///   <item>the list is <b>EMPTY</b>, so the rule-1 gate above is unconditional for every
        ///   species in the contract — which is the property the exclusion existed to protect;</item>
        ///   <item>the rig <b>clears its own gate</b> for the species that was held, re-measured live
        ///   across every variant, the way <c>TreeRigBaker.BuildEntry</c> computes the audit;</item>
        ///   <item>her sheets <b>have been re-baked</b> and ARE this pass's pixels — the exact
        ///   inverse of what the old test asserted, and what says "came back" rather than "was
        ///   quietly let through".</item>
        /// </list>
        ///
        /// <para>The MECHANISM stays and is exercised below on a name no species has, so the day a
        /// future drop must hold one back, the predicate it depends on is known to work.</para>
        /// </summary>
        [Test]
        public void NothingIsHeldBack_TheSpeciesThatWasClearsTheGate_AndHerSheetsAreThisPass()
        {
            CollectionAssert.IsEmpty(TreeKitCatalog.HeldBackSpecies,
                "A species is held back again: " + string.Join(", ", TreeKitCatalog.HeldBackSpecies) +
                ". That is a legitimate state — but it makes the rule-1 gate above conditional, so " +
                "invert this test back the way it was in the same change, and say which gate the " +
                "species failed and at what measured value.");

            // The mechanism, exercised on a name no species has. It is one line and two predicates,
            // and it is what a future hold-back depends on; a dormant guard with no test is how the
            // next one ships wrong.
            Assert.IsFalse(TreeKitCatalog.IsHeldBack("Tamarack"), "the list is empty, so nothing is");
            Assert.IsFalse(TreeKitCatalog.IsHeldBackStem("Tamarack_mature_summer"),
                           "and no stem resolves to a held-back species either");

            const string CameBack = "Tamarack";
            using var host = CreateTreeHost();

            // (1) IN the contract — she is placeable again, which is what being un-held MEANS.
            TreeKitCatalog.Entry entry =
                TreeKitCatalog.Find(_contract, CameBack, TreeRigBaker.DefaultStage);
            Assert.IsNotNull(entry,
                $"{CameBack} is not held back but is absent from the contract, so no tool will place " +
                "her. Either the bake did not re-run or she was dropped from the rig's SPECIES.");

            // (2) The rig CLEARS its own gate — measured live across every variant, exactly as
            // TreeRigBaker.BuildEntry computes the audit it writes (worst thinPct, `pass` ANDed).
            //
            // ⚠️ ALL FOUR VARIANTS, and this is why: under pass 2 her VARIANT 0 passed at 1.2% while
            // the sheet as a whole failed at 5.4% — the failure was one individual tree. Measuring
            // variant 0 alone reported "fixed" on a species that was not, and the same shortcut would
            // now report "fixed" without having looked at the three that mattered.
            int variants = (int)host.EvaluateNumber($"{TreeKitCatalog.RigGlobalName}.VARIANTS");
            bool pass = true;
            double thinPct = 0;
            int bodyRatio = 0, worstVariant = -1;
            for (int v = 0; v < variants; v++)
            {
                string r = TreeRigBaker.ResultExpr(CameBack, TreeRigBaker.DefaultStage,
                                                   TreeRigBaker.DefaultSeason, variant: v, frame: 0);
                pass &= host.EvaluateBool($"{r}.report.pass");
                double t = host.EvaluateNumber($"{r}.report.thinPct");
                if (t > thinPct)
                {
                    thinPct = t;
                    bodyRatio = (int)host.EvaluateNumber($"{r}.report.bodyRatio");
                    worstVariant = v;
                }
            }

            Assert.IsTrue(pass,
                $"{CameBack} FAILS the rig's own rule audit again (worst thinPct {thinPct}% on variant " +
                $"{worstVariant} of {variants}). She must then go back into " +
                "TreeKitCatalog.HeldBackSpecies and leave the contract — never be waved through the " +
                "rule-1 gate above.");
            Assert.LessOrEqual(thinPct, RuleOneGatePct,
                $"{CameBack}'s worst thinPct is {thinPct}%, over the rig's own {RuleOneGatePct}% " +
                "rule-1 tolerance, yet report.pass is true — the two have drifted apart. Re-read the " +
                "rig before changing anything here.");

            Debug.Log($"[tree-held] ✅ {CameBack}/{TreeRigBaker.DefaultStage}/" +
                      $"{TreeRigBaker.DefaultSeason} CLEARS the pass-3 rig's rule 1: worst thinPct " +
                      $"{thinPct}% on variant {worstVariant} of {variants}, against the rig's own " +
                      $"{RuleOneGatePct}% gate (bodyRatio {bodyRatio}). Pass 2 failed at 5.4% with " +
                      "bodyRatio 66; pass 1 measured 1.1% / 80.");

            // (3) Her sheets ARE this pass's pixels — the exact inverse of the old assert, which
            // required the committed width NOT to be what the current rig bakes.
            foreach (var channel in TreeKitCatalog.Channels)
                Assert.IsTrue(
                    File.Exists(TreeKitCatalog.SheetPath(CameBack, TreeRigBaker.DefaultStage,
                                                         TreeRigBaker.DefaultSeason, channel)),
                    $"{CameBack}'s {channel} sheet is missing.");

            string albedoPath = TreeKitCatalog.SheetPath(CameBack, TreeRigBaker.DefaultStage,
                                                         TreeRigBaker.DefaultSeason,
                                                         TreeKitCatalog.Channel.Albedo);
            var spec = TreeRigBaker.ReadSheetSpec(host, CameBack, TreeRigBaker.DefaultStage);
            var tex = Decode(File.ReadAllBytes(albedoPath));
            try
            {
                Assert.AreEqual(spec.Cols * spec.CellW, tex.width,
                    $"{CameBack}'s committed sheet is {tex.width} px wide but the current rig bakes " +
                    $"{spec.Cols * spec.CellW}. She is un-held, so her pixels must be THIS pass's — " +
                    "re-run Hidden Harbours ▸ Art ▸ Bake Acadian Trees and commit the result.");
                Debug.Log($"[tree-held] {CameBack} committed sheet {tex.width}×{tex.height} matches " +
                          $"the {spec.CellW}×{spec.CellH} cell pass 3 bakes — re-baked, not merely " +
                          "re-admitted.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>Loading a committed PNG into a throwaway Texture2D reads its pixels without
        /// flipping <c>isReadable</c> on the shipped asset.</summary>
        static Texture2D Decode(byte[] png)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            Assert.IsTrue(t.LoadImage(png, markNonReadable: false), "Failed to decode PNG.");
            return t;
        }
    }
}
