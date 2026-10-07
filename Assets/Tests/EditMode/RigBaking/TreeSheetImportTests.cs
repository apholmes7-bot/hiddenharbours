using System;
using System.Collections.Generic;
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
    /// The IMPORT half of the tree bake: what Unity did with the 125 committed sheets (three seasons,
    /// up to six channels each) beside the one shared palette. Baking the right pixels and importing
    /// them wrongly produces art that looks fine and behaves wrongly, and two of these settings fail
    /// SILENTLY.
    ///
    /// <list type="bullet">
    ///   <item>🔴 <b>sRGB must be OFF on every sheet but the albedo</b>: <c>_mask</c>, <c>_normal</c>,
    ///   and pass 4's <c>_wind</c>, <c>_phase</c> and <c>_snow</c>. They are data, not colour. Leave it
    ///   on and Unity gamma-curves the channels: the sprite still looks right in the inspector, the
    ///   lighting and the sway are simply wrong, and nothing but a numeric assert notices.
    ///   <c>ArtImportPipeline</c> stamps sRGB ON for everything under Art/, so this is an override
    ///   that can silently revert if the sheets are ever re-imported without the slicer.</item>
    ///   <item><b>Mesh Type <see cref="SpriteMeshType.FullRect"/></b> for the pass-4 kit
    ///   (<see cref="TreeKitCatalog.MeshTypeFor"/>): the fragment stage gathers a leaf from up to the
    ///   wind's reach beyond the rest outline, and a Tight mesh ends AT that outline. Asserted as the
    ///   CAPABILITY (every sprite's mesh spans its whole cell, and the rest pose leaves a margin in
    ///   the cell for it to cover) rather than only the flag.</item>
    ///   <item><b>No lossy compression</b>: a block-compressed mask is a wrong mask, and the wind and
    ///   phase sheets carry packed bits. The snow sheet's R8 override is held to the same.</item>
    ///   <item><b>The pivot is the TRUNK FOOT</b>, per species, checked against the CONTRACT rather
    ///   than a literal. <c>ArtImportPipeline</c> defaults <c>/foliage/</c> to BottomCenter, which
    ///   would sink every species by its own flare pad.</item>
    ///   <item><b>≤ 2048 px on every axis</b>: over the cap Unity downscales silently and the
    ///   sprite COUNT still matches.</item>
    /// </list>
    ///
    /// <para><b>Retired at the pass-4 switch (2026-09-27):</b>
    /// <c>TheAnchorLine_ActuallyCutsEverySpritesMesh_NotJustTouchesItsBottom</c>. It required each
    /// species' trunk anchor to split every sprite's mesh, so the VERTEX bend
    /// (<c>smoothstep(_TrunkAnchor, 1, uv.y)</c>) had a row to hold still while the crown swayed. A
    /// pass-4 tree binds its maps (<c>TreeTrunkAnchor</c> publishes <c>_TreeMaps 1</c> on its
    /// renderer), and <c>HiddenHarboursTreeWind</c> then skips the vertex bend: the quad stays put and
    /// each pixel moves by the wind map, whose lean is 0 at the trunk foot. A FullRect sprite is four
    /// vertices, so the claim has no subject left in the kit. Its successor is
    /// <see cref="EverySheet_IsMeshedFullRect_SoTheWindCanCarryALeafPastTheRestOutline"/>.</para>
    ///
    /// <para>Importer metadata, sprite UVs and decoded PNG bytes: no GPU, nothing to gate on
    /// <c>GraphicsDeviceType.Null</c>. The two provenance tests re-measure the live rig's own rule
    /// audit in V8 (<see cref="AuditRestFrames"/>): CPU only, and the slowest thing here.</para>
    /// </summary>
    public class TreeSheetImportTests
    {
        static TreeKitCatalog.Contract _contract;

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

        /// <summary>Every sheet the kit COMMITS. A season draws all six channels, the albedo alone (a
        /// deciduous autumn, recoloured over summer's maps) or nothing of its own (an evergreen's
        /// autumn is its summer): <see cref="TreeKitCatalog.ChannelsFor"/>, the routing the slicer's
        /// own verifier walks.</summary>
        static (string path, string stem, TreeKitCatalog.Entry entry, TreeKitCatalog.Channel channel)[] Sheets()
        {
            var rows = new List<(string, string, TreeKitCatalog.Entry, TreeKitCatalog.Channel)>();
            foreach (var e in _contract.trees)
            foreach (string season in e.seasons)
            foreach (var c in TreeKitCatalog.ChannelsFor(e, season))
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

        /// <summary>A V8 host with the LIVE rig installed the way <see cref="TreePass4Baker"/> installs
        /// it (rig 4, treeMaps4 and the glue). Needed only by the tests that re-measure the rig's own
        /// verdict rather than trust a number typed in here.</summary>
        static IRigScriptHost CreateTreeHost() => TreePass4TempBake.CreateHost();

        static string G => TreeKitCatalog.RigGlobalName;
        static string M => TreeKitCatalog.Pass4MapsGlobalName;
        static string Js(string s) => TreePass4TempBake.Js(s);

        static IReadOnlyList<string> RigStrings(IRigScriptHost host, string expr) =>
            FishingKitBaker.ReadStringArray(host, $"{G}.{expr}");

        /// <summary>The seasons whose REST frames the committed sheets were baked from: each season
        /// row's albedo season and maps season. An evergreen's autumn borrows both from summer, so it
        /// adds none; a deciduous autumn adds its own albedo over summer's maps.</summary>
        static string[] BakedSeasons(TreeKitCatalog.Entry e) =>
            e.seasonRows.SelectMany(r => new[] { r.albedo, r.maps }).Distinct().ToArray();

        /// <summary>Rig 4's own rule-1 verdict on one rest frame, off its <c>render().report</c>.</summary>
#pragma warning disable CS0649 // JsonUtility assigns these
        [Serializable]
        sealed class RestAudit
        {
            /// <summary><c>render()</c> returned the very frame <c>TreeMaps4.rest()</c> made.</summary>
            public bool same;
            public bool pass;
            /// <summary>Components carrying six or more foliage pixels: the ones rule 1 holds.</summary>
            public int masses;
            public int failed;
            /// <summary>The thinnest such mass's body in px (0 when there are none).</summary>
            public float minBody;
        }
#pragma warning restore CS0649

        /// <summary>
        /// The live rig's rule audit over every variant of one season's REST frame, the frame the glue
        /// bakes: <c>TreeMaps4.rest()</c>, then <c>render()</c> at the same calm wind and rest phase,
        /// which must hand back that frame from the rig's cache or the audit read some other pose;
        /// <c>clearCache()</c> after each, as the glue does.
        ///
        /// <para>⚠️ <b>Why live.</b> The committed contract carries no pass-4 audit:
        /// <see cref="TreePass4Baker"/> leaves every entry's <c>audit</c> block at its defaults (so it
        /// reads <c>pass: false</c>), because the glue's bake returns cells, not a report. The rig still
        /// reports. Its <c>render()</c> runs the mass rule rig 3's did (every mass carrying six or more
        /// foliage pixels needs a body of <c>MIN_BODY</c> px inside its rim) without rig 3's second
        /// term, the ≤ 4% share of foliage too thin to carry a rim, which rig 4 no longer has.</para>
        /// </summary>
        static RestAudit[] AuditRestFrames(IRigScriptHost host, TreeKitCatalog.Entry e, string season)
        {
            int variants = (int)host.EvaluateNumber($"{G}.VARIANTS");
            var audits = new RestAudit[variants];
            for (int v = 0; v < variants; v++)
            {
                string o = $"{{variant: {v}, season: {Js(season)}, stage: {Js(e.stage)}}}";
                string json = host.EvaluateString(
                    "(function () { " +
                    $"var o = {o}; " +
                    $"var rf = {M}.rest({Js(e.species)}, o); " +
                    $"var r = {G}.render({Js(e.species)}, Object.assign({{}}, o, " +
                    $"{{ wind: {{ w: 0, gust: 0, dir: 1 }}, phase: {M}.REST_PHASE }})); " +
                    "var out = { same: r.frame === rf, pass: r.report.pass, masses: r.report.masses, " +
                    "failed: r.report.failed, minBody: r.report.minBody }; " +
                    $"{G}.clearCache(); " +
                    "return JSON.stringify(out); })()");
                audits[v] = JsonUtility.FromJson<RestAudit>(json);
                Assert.IsNotNull(audits[v], $"{e.species}/{season} variant {v}: unreadable audit {json}");
            }
            return audits;
        }

        // =================================================================================

        [Test]
        public void TheKit_IsTheRigsSpeciesMinusTheHeldBackOnes_TimesEachSeasonsChannels()
        {
            // Expressed STRUCTURALLY first: the committed set is exactly "every species the live rig
            // declares, minus the ones held back", times the channels each of its seasons routes, so
            // holding one back or releasing one moves these asserts with it instead of breaking them.
            // The literals at the end are the ruled kit, pinned on purpose.
            using var host = CreateTreeHost();
            var declared = RigStrings(host, "SPECIES.map(function (s) { return s.key; })");
            int expectedSpecies = declared.Count(k => !TreeKitCatalog.IsHeldBack(k));

            Assert.AreEqual(expectedSpecies, _contract.trees.Length,
                $"The rig declares {declared.Count} species and {TreeKitCatalog.HeldBackSpecies.Length} " +
                $"are held back, so the kit bakes {expectedSpecies} at {TreeRigBaker.DefaultStage}. " +
                "Held back: " + string.Join(", ", TreeKitCatalog.HeldBackSpecies) + ".");
            foreach (string held in TreeKitCatalog.HeldBackSpecies)
                Assert.IsFalse(_contract.trees.Any(t => t.species == held),
                    $"{held} is held back but reached the contract.");

            var sheets = Sheets();
            foreach (var (path, _, _, _) in sheets)
                Assert.IsTrue(File.Exists(path), $"Missing committed sheet: {path}");

            // Nothing unclaimed may sit in the folder: a stray sheet has no cell and no pivot, and
            // the slicer refuses rather than guessing either. The one file beside the sheets is the
            // shared palette, which is imported whole and never sliced.
            var claimed = new HashSet<string>(sheets.Select(s => Path.GetFileName(s.path)), StringComparer.Ordinal)
            {
                TreeKitCatalog.PaletteFileName,
            };
            string[] onDisk = Directory.GetFiles(TreeKitCatalog.TreesRoot, "*.png")
                                       .Select(Path.GetFileName).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            string[] unclaimed = onDisk.Where(f => !claimed.Contains(f)).ToArray();
            Assert.IsEmpty(unclaimed,
                "Files under " + TreeKitCatalog.TreesRoot + " that the contract does not claim:\n  " +
                string.Join("\n  ", unclaimed));

            // The ruled kit ("882 all 3", 2026-09-27), pinned. All ten species draw six channels in
            // summer and in winter; in autumn the five that turn (Tamarack and the four broadleaves)
            // draw their own albedo over summer's maps, and the five evergreens draw summer whole.
            var seasons = RigStrings(host, "SEASONS");
            string perSeason = string.Join(", ", seasons.Select(season =>
                $"{season} {_contract.trees.Sum(e => TreeKitCatalog.ChannelsFor(e, season).Length)}"));
            Assert.AreEqual("summer 60, autumn 5, winter 60", perSeason,
                "The sheets each season commits. If a season was re-ruled, this pin moves with the " +
                "ruling and the kit's texture budget with it.");
            Assert.AreEqual(126, onDisk.Length,
                "10 species × 6 channels × (summer + winter), + 5 autumn albedos, + " +
                TreeKitCatalog.PaletteFileName + ".");
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
                          "coverage, normal = a view-space vector, wind and phase = lean, sway, wave " +
                          "and packed bits, snow = the cover byte a pixel turns at). With sRGB on Unity " +
                          "applies a gamma curve and the sprite still LOOKS fine — only a numeric " +
                          "assert catches it. ArtImportPipeline stamps sRGB ON for everything under " +
                          "Art/, so re-importing without TreeSheetSlicer silently reverts this.");
            }
        }

        [Test]
        public void EverySheet_IsUncompressedPointFilteredPixelArt()
        {
            foreach (var (path, stem, _, channel) in Sheets())
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

                if (!TreeKitCatalog.IsSingleChannel(channel)) continue;

                // Pass 4's snow sheet imports R8 through a platform override: a second set of import
                // settings, held to the same no-loss rule.
                TextureImporterPlatformSettings p =
                    imp.GetPlatformTextureSettings(TreeKitCatalog.SingleChannelPlatform);
                Assert.IsTrue(TreeSheetSlicer.IsR8(imp),
                    $"{stem}: the snow sheet imports R8 on {TreeKitCatalog.SingleChannelPlatform}; the " +
                    "shader reads its R alone, and RGBA32 would carry three channels of nothing.");
                Assert.AreEqual(TextureImporterCompression.Uncompressed, p.textureCompression,
                    $"{stem}: the R8 override compresses. The cover byte is a threshold, and a lossy " +
                    "one moves the snow line.");
                Assert.IsFalse(p.crunchedCompression, $"{stem}: the R8 override is crunched.");
            }
        }

        [Test]
        public void EverySheet_IsWithinTheImportSizeCap_SoNothingWasSilentlyDownscaled()
        {
            foreach (var (path, stem, entry, channel) in Sheets())
            {
                var imp = ImporterFor(path);
                Assert.LessOrEqual(entry.sheetW, TreeKitCatalog.ImportSizeCap, $"{stem}: sheet width");
                Assert.LessOrEqual(entry.sheetH, TreeKitCatalog.ImportSizeCap, $"{stem}: sheet height");
                Assert.GreaterOrEqual(imp.maxTextureSize, Mathf.Max(entry.sheetW, entry.sheetH),
                    $"{stem}: the importer caps below the sheet's own size — Unity has already " +
                    "downscaled it, and the sprite COUNT would still come out right.");
                if (TreeKitCatalog.IsSingleChannel(channel))
                    Assert.GreaterOrEqual(
                        imp.GetPlatformTextureSettings(TreeKitCatalog.SingleChannelPlatform).maxTextureSize,
                        Mathf.Max(entry.sheetW, entry.sheetH),
                        $"{stem}: the {TreeKitCatalog.SingleChannelPlatform} R8 override caps below the " +
                        "sheet's own size, so that platform downscales the snow sheet the default shows whole.");

                // The sliced rects are the proof: a downscale refits them and the cell size drifts.
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                Assert.IsNotEmpty(sprites, $"{stem}: no Sprite sub-assets");
                float widest = sprites.Max(s => s.rect.xMax);
                Assert.AreEqual(entry.sheetW, Mathf.RoundToInt(widest), $"{stem}: rects do not span " +
                    "the contract's sheet width — the classic signature of a silent downscale.");
            }
        }

        // =================================================================================
        // the mesh the wind shader draws through
        // =================================================================================

        /// <summary>
        /// Pass 4 is FullRect, the opposite of pass 3's Tight, for the opposite reason.
        ///
        /// <para>Pass 3 bent the quad's VERTICES, so it needed intermediate ones (Tight, and the
        /// retired anchor-line test). Pass 4 holds the quad still and moves the tree per FRAGMENT:
        /// each pixel gathers the leaf the wind carries there from up to <c>windReach</c> px away,
        /// which puts foliage where the rest pose has none. A Tight mesh ends at the rest pose's
        /// outline, so every leaf blown past it would be cut off at the mesh edge, and the tree would
        /// sway inside a cookie-cutter of its own calm shape. The cell is padded for the wind to have
        /// room; FullRect is what lets the mesh use it.</para>
        ///
        /// <para>Asserted three ways: the flag, per <see cref="TreeKitCatalog.MeshTypeFor"/>; the
        /// capability, every sprite's mesh spanning its whole cell; and a MEASURED SABOTAGE: the rest
        /// pose really does leave a margin inside its cell on the left, the right and the top, so a
        /// Tight mesh really would cut there and the first two are not vacuous.</para>
        /// </summary>
        [Test]
        public void EverySheet_IsMeshedFullRect_SoTheWindCanCarryALeafPastTheRestOutline()
        {
            foreach (var (path, stem, entry, _) in Sheets())
            {
                Assert.AreEqual(SpriteMeshType.FullRect, TreeKitCatalog.MeshTypeFor(entry),
                    $"{stem}: {entry.species} is not routed as a pass-4 tree (TreeKitCatalog.HasPass4), " +
                    "so the slicer would mesh it Tight; the committed kit is pass 4 throughout.");
                Assert.AreEqual(TreeKitCatalog.MeshTypeFor(entry), TreeKitCatalog.MeshTypeOf(ImporterFor(path)),
                    $"{stem}: a Tight mesh ends at the rest pose's outline, and the shader gathers " +
                    "leaves from beyond it, so every leaf the wind carries past the calm silhouette " +
                    "would be clipped at the mesh edge. Re-run Slice Acadian Tree Sheets.");

                float tolU = 0.5f / entry.sheetW, tolV = 0.5f / entry.sheetH;
                foreach (Sprite s in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                {
                    Vector2[] uv = s.uv;
                    Assert.IsNotEmpty(uv, $"{s.name}: no UVs");
                    Assert.AreEqual(s.rect.xMin / entry.sheetW, uv.Min(p => p.x), tolU, $"{s.name}: the mesh's left edge is not its cell's");
                    Assert.AreEqual(s.rect.xMax / entry.sheetW, uv.Max(p => p.x), tolU, $"{s.name}: the mesh's right edge is not its cell's");
                    Assert.AreEqual(s.rect.yMin / entry.sheetH, uv.Min(p => p.y), tolV, $"{s.name}: the mesh's bottom is not its cell's");
                    Assert.AreEqual(s.rect.yMax / entry.sheetH, uv.Max(p => p.y), tolV, $"{s.name}: the mesh's top is not its cell's");
                }
            }

            // ---- MEASURED SABOTAGE: the rest pose leaves room that a Tight mesh would cut ---------
            int worst = int.MaxValue, worstSide = int.MaxValue, albedos = 0;
            string worstAt = null, worstSideAt = null;
            foreach (var (path, stem, entry, channel) in Sheets())
            {
                if (channel != TreeKitCatalog.Channel.Albedo) continue;
                albedos++;
                Texture2D tex = Decode(File.ReadAllBytes(path));
                try
                {
                    Color32[] px = tex.GetPixels32();   // bottom row first
                    for (int v = 0; v < entry.sheetW / entry.cellW; v++)
                    {
                        int x0 = v * entry.cellW, minX = int.MaxValue, maxX = -1, maxY = -1;
                        for (int y = 0; y < entry.cellH; y++)
                        for (int x = x0; x < x0 + entry.cellW; x++)
                        {
                            if (px[y * tex.width + x].a == 0) continue;
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y > maxY) maxY = y;
                        }
                        Assert.GreaterOrEqual(maxX, 0, $"{stem} variant {v}: an empty cell");

                        int left = minX - x0, right = x0 + entry.cellW - 1 - maxX, top = entry.cellH - 1 - maxY;
                        string at = $"{stem} variant {v} (left {left}, right {right}, top {top} px; " +
                                    $"windReach {entry.wind.windReach})";
                        if (Math.Min(top, Math.Min(left, right)) < worst)
                        {
                            worst = Math.Min(top, Math.Min(left, right));
                            worstAt = at;
                        }
                        if (Math.Min(left, right) < worstSide)
                        {
                            worstSide = Math.Min(left, right);
                            worstSideAt = at;
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }

            Debug.Log($"[tree-import] over {albedos} albedo sheets the rest pose's narrowest margin in its " +
                      $"cell is {worst} px, at {worstAt}; the narrowest at the side, where the wind leans, " +
                      $"is {worstSide} px, at {worstSideAt}. Measured 2026-09-27: 9 px (Tamarack summer, " +
                      "variant 3, top) and 32 px (WhiteCedar summer, variant 3, left).");
            Assert.Greater(albedos, 0, "no albedo sheets were read");
            Assert.GreaterOrEqual(worst, 1,
                $"SABOTAGE: the rest pose touches its cell's edge at {worstAt}. There a Tight mesh and " +
                "a FullRect one cover the same pixels, so the mesh asserts above are not what keeps a " +
                "blown leaf; the rig's padding is gone. Re-read its sheetSpec before re-baking.");
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

        /// <summary>
        /// Where the pixels came from, and that the rig's own rule audit clears every rest frame they
        /// were baked from.
        ///
        /// <para>Re-measured on rig 4 at the pass-4 switch (2026-09-27). Until then the gate read each
        /// entry's <c>audit</c> block, which <c>TreeRigBaker.BuildEntry</c> filled from rig 3; the
        /// pass-4 contract's blocks are empty (see <see cref="AuditRestFrames"/>), so the same gate
        /// now runs on the live rig, over every season and variant whose rest frame a committed
        /// sheet came from. A bare tree passes rule 1 by having nothing to hold to it, so summer, in
        /// leaf, must report foliage masses for the gate to have tested anything; that replaces the
        /// old <c>underFloor</c> check, which an empty block would pass by default.</para>
        /// </summary>
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

            using var host = CreateTreeHost();
            int minBody = (int)host.EvaluateNumber($"{G}.MIN_BODY");
            Assert.AreEqual(minBody, _contract.rules.minBodyPx, "the contract's rule is the live rig's MIN_BODY");
            var seasons = RigStrings(host, "SEASONS");

            var log = new System.Text.StringBuilder();
            int frames = 0;
            foreach (var e in _contract.trees)
            {
                Assert.Greater(e.metres, 1.0f, $"{e.species}: implausible true height.");
                Assert.AreEqual(TreeRigBaker.DefaultStage, e.stage);
                CollectionAssert.AreEqual(seasons, e.seasons,
                    $"{e.species}: the kit is ruled to all of the rig's seasons, in its order (\"882 all 3\", 2026-09-27).");

                log.Append($"\n  {e.species,-15}");
                foreach (string season in BakedSeasons(e))
                {
                    RestAudit[] a = AuditRestFrames(host, e, season);
                    for (int v = 0; v < a.Length; v++)
                    {
                        string at = $"{e.species}/{e.stage}/{season} variant {v}";
                        Assert.IsTrue(a[v].same,
                            $"{at}: render() did not return the frame TreeMaps4.rest() made, so this " +
                            "audit read some other pose than the one the glue bakes.");

                        // 🔴 UNCONDITIONAL, over the COMMITTED SET. There is no exemption list: a species
                        // the rig rejects is HELD BACK from the bake entirely (TreeKitCatalog
                        // .HeldBackSpecies), so it never reaches this contract and this gate never has
                        // to make an exception for it. Coordinator ruling 2026-07-29 — do not loosen
                        // this into a per-species allowance.
                        Assert.IsTrue(a[v].pass,
                            $"{at}: the rig's own rule audit FAILED ({a[v].failed} of {a[v].masses} " +
                            $"foliage masses under MIN_BODY {minBody} px; thinnest {a[v].minBody} px). " +
                            "That is an art-director rig matter, not something to bake past. If this " +
                            "species must not ship, hold it back in TreeKitCatalog.HeldBackSpecies so it " +
                            "leaves the contract — never wave it through here.");
                        if (a[v].masses > 0)
                            Assert.GreaterOrEqual(a[v].minBody, minBody,
                                $"{at}: report.pass is true, yet its thinnest mass is {a[v].minBody} px, " +
                                $"under MIN_BODY {minBody}. The verdict and its own measure disagree; " +
                                "re-read the rig's massReport before trusting either.");
                        frames++;
                    }
                    if (season == TreeRigBaker.DefaultSeason)
                        Assert.IsTrue(a.All(x => x.masses > 0),
                            $"{e.species}/{season}: a variant in leaf reports no foliage mass, so rule 1 " +
                            "passed it without holding anything to the rule.");

                    int masses = a.Sum(x => x.masses);
                    log.Append(masses == 0
                        ? $" | {season} bare"
                        : $" | {season} {masses} masses, thinnest {a.Where(x => x.masses > 0).Min(x => x.minBody)} px");
                }
            }
            Debug.Log($"[tree-audit] rig 4's rule 1 CLEARS all {frames} rest frames the kit was baked " +
                      $"from (MIN_BODY {minBody} px; an evergreen's autumn is its summer):{log}");
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
        ///   on every season and variant her sheets were baked from (<see cref="AuditRestFrames"/>);</item>
        ///   <item>her sheets <b>have been re-baked</b> and ARE this pass's pixels — the exact
        ///   inverse of what the old test asserted, and what says "came back" rather than "was
        ///   quietly let through".</item>
        /// </list>
        ///
        /// <para>Re-measured on rig 4 at the pass-4 switch (2026-09-27). Rig 3's verdict had two terms:
        /// the mass rule, and at most 4% of foliage too thin to carry a rim, which rig 3 spelt inline
        /// in <c>report.pass</c> and this suite restated as <c>RuleOneGatePct</c> with a check that
        /// the two had not drifted apart. Rig 4's verdict is the mass rule alone, so the restated
        /// constant and its drift check went with rig 3; the drift check that remains is the verdict
        /// against its own thinnest body.</para>
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

            // (2) The rig CLEARS its own gate: every season her sheets were baked from, every variant.
            //
            // ⚠️ ALL FOUR VARIANTS, and this is why: under pass 2 her VARIANT 0 passed at 1.2% while
            // the sheet as a whole failed at 5.4% — the failure was one individual tree. Measuring
            // variant 0 alone reported "fixed" on a species that was not, and the same shortcut would
            // now report "fixed" without having looked at the three that mattered.
            int minBody = (int)host.EvaluateNumber($"{G}.MIN_BODY");
            var said = new List<string>();
            int variants = 0;
            foreach (string season in BakedSeasons(entry))
            {
                RestAudit[] a = AuditRestFrames(host, entry, season);
                variants = a.Length;
                for (int v = 0; v < a.Length; v++)
                {
                    Assert.IsTrue(a[v].same,
                        $"{CameBack}/{season} variant {v}: render() did not return the rest frame the glue bakes.");
                    Assert.IsTrue(a[v].pass,
                        $"{CameBack}/{season} FAILS the rig's own rule audit again on variant {v} of " +
                        $"{a.Length} ({a[v].failed} of {a[v].masses} masses under MIN_BODY {minBody} px; " +
                        $"thinnest {a[v].minBody} px). She must then go back into " +
                        "TreeKitCatalog.HeldBackSpecies and leave the contract — never be waved " +
                        "through the rule-1 gate above.");
                    if (a[v].masses > 0)
                        Assert.GreaterOrEqual(a[v].minBody, minBody,
                            $"{CameBack}/{season} variant {v}: report.pass is true, yet its thinnest mass " +
                            $"is {a[v].minBody} px, under MIN_BODY {minBody}. The two have drifted " +
                            "apart; re-read the rig before changing anything here.");
                }
                if (season == TreeRigBaker.DefaultSeason)
                    Assert.IsTrue(a.All(x => x.masses > 0),
                        $"{CameBack}/{season}: a variant in leaf reports no foliage mass, so rule 1 " +
                        "passed her without holding anything to it.");

                int masses = a.Sum(x => x.masses);
                said.Add(masses == 0
                    ? $"{season} bare"
                    : $"{season} thinnest body {a.Where(x => x.masses > 0).Min(x => x.minBody)} px over {masses} masses");
            }

            Debug.Log($"[tree-held] ✅ {CameBack}/{TreeRigBaker.DefaultStage} CLEARS the pass-4 rig's " +
                      $"rule 1 on every season her sheets were baked from, all {variants} variants: " +
                      $"{string.Join("; ", said)} (MIN_BODY {minBody} px). Rig 3's second term, at most " +
                      "4% of foliage too thin for a rim, has no rig-4 counterpart. Pass 2 failed at " +
                      "5.4% with bodyRatio 66; pass 1 measured 1.1% / 80.");

            // (3) Her sheets ARE this pass's pixels — the exact inverse of the old assert, which
            // required the committed width NOT to be what the current rig bakes.
            var spec = TreePass4Baker.ReadSheetSpec(host, CameBack, TreeRigBaker.DefaultStage, out _);
            foreach (string season in entry.seasons)
            foreach (var channel in TreeKitCatalog.ChannelsFor(entry, season))
            {
                string path = TreeKitCatalog.SheetPath(CameBack, entry.stage, season, channel);
                Assert.IsTrue(File.Exists(path), $"{CameBack}'s {season} {channel} sheet is missing.");
                if (channel != TreeKitCatalog.Channel.Albedo) continue;

                var tex = Decode(File.ReadAllBytes(path));
                try
                {
                    Assert.AreEqual(spec.Cols * spec.CellW, tex.width,
                        $"{CameBack}'s committed {season} sheet is {tex.width} px wide but the current " +
                        $"rig bakes {spec.Cols * spec.CellW}. She is un-held, so her pixels must be THIS " +
                        "pass's — re-run Hidden Harbours ▸ Art ▸ Bake Acadian Trees and commit the result.");
                    Assert.AreEqual(spec.Rows * spec.CellH, tex.height,
                        $"{CameBack}'s committed {season} sheet is {tex.height} px tall, not the " +
                        $"{spec.Rows} × {spec.CellH} the current rig bakes.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            Debug.Log($"[tree-held] {CameBack}'s committed sheets match the {spec.CellW}×{spec.CellH} " +
                      "cell pass 4 bakes — re-baked, not merely re-admitted.");
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
