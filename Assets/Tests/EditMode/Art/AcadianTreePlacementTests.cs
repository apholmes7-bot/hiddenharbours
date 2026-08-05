using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// PLACING the rig-baked Acadian trees — what the owner actually drags into a scene, as opposed
    /// to what came off the bake (<c>TreeRigBakeTests</c>) or how it imported
    /// (<c>TreeSheetImportTests</c>). Three questions the kit is waiting on an eye for, and each one
    /// has an assert here with a measured cost attached:
    /// <list type="number">
    ///   <item><b>Do they sit on the ground?</b> A tree pivots at its TRUNK FOOT, with
    ///   <c>nearFlarePad</c> rows of near-root flare hanging BELOW that. Bottom-centre — the
    ///   convention every other tree sprite in this repo follows — sinks the species by 13–23 px.</item>
    ///   <item><b>Is the scale right?</b> The rig bakes the camera's foreshortening in, so drawn
    ///   height is <c>metres × sin 40°</c>, not <c>metres</c>.</item>
    ///   <item><b>Does each species move like itself?</b> The trunk anchor is per species; the shared
    ///   material ships one value at the top of the range.</item>
    /// </list>
    ///
    /// <para>Builds into a SCRATCH folder, never the owner's real (untracked, generated)
    /// <c>Prefabs/Decor/</c> tree — the same rule <c>FlowerPrefabBuilderTests</c> follows.</para>
    ///
    /// <para>No GPU here: everything asserts renderer/sprite/importer state, so nothing needs gating
    /// on <c>GraphicsDeviceType.Null</c>.</para>
    /// </summary>
    public class AcadianTreePlacementTests
    {
        private const string Scratch = "Assets/_Project/Prefabs/_AcadianTreeBuilderTestScratch";
        private const string ScratchLeaf = "_AcadianTreeBuilderTestScratch";

        private List<AcadianTreeCatalog.Placement> _trees;
        private TreeKitCatalog.Contract _contract;

        [SetUp]
        public void SetUp()
        {
            _contract = TreeKitCatalog.Load();
            Assert.IsNotNull(_contract, $"No contract at {TreeKitCatalog.ContractPath} — the kit is not baked.");
            _trees = AcadianTreeCatalog.Scan();
            Assert.IsNotEmpty(_trees, "The contract parsed but claims no placeable trees.");

            if (AssetDatabase.IsValidFolder(Scratch)) AssetDatabase.DeleteAsset(Scratch);
            AssetDatabase.CreateFolder("Assets/_Project/Prefabs", ScratchLeaf);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Scratch)) AssetDatabase.DeleteAsset(Scratch);
        }

        private int Ppu => _contract.ppu;

        private GameObject PrefabFor(AcadianTreeCatalog.Placement p) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{Scratch}/{p.Stem}.prefab");

        // =================================================================================
        // the builder produces something draggable
        // =================================================================================

        [Test]
        public void Builder_MakesOnePrefabPerBakedTree_NamedForItsSheet()
        {
            int built = AcadianTreePrefabBuilder.BuildAll(Scratch);

            Assert.AreEqual(_trees.Count, built,
                "Every baked species+season should yield exactly one drag-and-drop prefab.");

            foreach (var p in _trees)
            {
                var prefab = PrefabFor(p);
                Assert.IsNotNull(prefab,
                    $"No prefab was built for '{p.Stem}' — the owner would have nothing to drag.");
                Assert.AreEqual(p.Stem, prefab.name);
            }
        }

        /// <summary>
        /// The canonical tree is exactly six components: Transform, SpriteRenderer, YSortSprite,
        /// TreeTrunkAnchor, ReflectiveObject and SpriteShadow. No Animator — the sway is the shader
        /// reading the shared sim wind, and an Animator per tree would be deaf to it AND batch-break
        /// (rule 7). If one ever appears here, that is the design being quietly reversed.
        ///
        /// <para><b>ReflectiveObject joined the shape in the ADR 0027 activation pass</b>, and the list
        /// is deliberately EXACT so that is a decision somebody makes rather than a drift somebody
        /// notices later. It earns its place: a tree at the water's edge reflects in it, the TreeWind
        /// material this species already uses is the one carrying the HHReflect pass, and the
        /// component's own distance gate keeps an inland forest out of the reflective set — so every
        /// tree may carry it while only the bankside ones cost anything.</para>
        ///
        /// <para><b>SpriteShadow joined it on the owner's 2026-08-05 casting ruling</b>, by the same
        /// argument in the same place: <see cref="AcadianTreeCatalog.Configure"/> is the one method a
        /// tree is built through, so a PREFAB dragged into a scene rakes at dawn exactly like a tree
        /// the planter placed. The EXACT list is what makes that a decision rather than a drift —
        /// which is also why it costs an edit here, and should.</para>
        /// </summary>
        [Test]
        public void EveryTreePrefab_IsTheCanonicalDecorShape_AndCarriesNoAnimator()
        {
            AcadianTreePrefabBuilder.BuildAll(Scratch);

            var wanted = new[] { "ReflectiveObject", "SpriteRenderer", "SpriteShadow", "Transform",
                                 "TreeTrunkAnchor", "YSortSprite" }
                         .OrderBy(n => n).ToArray();
            var problems = new List<string>();

            foreach (var p in _trees)
            {
                var prefab = PrefabFor(p);
                if (prefab == null) { problems.Add($"{p.Stem}: not built"); continue; }

                var kinds = prefab.GetComponents<Component>()
                                  .Select(c => c.GetType().Name).OrderBy(n => n).ToArray();
                if (!kinds.SequenceEqual(wanted))
                    problems.Add($"{p.Stem}: components are [{string.Join(", ", kinds)}], expected " +
                                 $"[{string.Join(", ", wanted)}]");

                Assert.IsNull(prefab.GetComponentInChildren<Animator>(true),
                    $"{p.Stem} carries an Animator. The canopy sways via the shader off the shared " +
                    "sim wind — an Animator cannot react to wind (CLAUDE.md rule 7).");

                if (prefab.transform.localScale != Vector3.one)
                    problems.Add($"{p.Stem}: scale {prefab.transform.localScale}, expected 1 " +
                                 "(the bake is already at honest metric size).");
            }
            Assert.IsEmpty(problems, string.Join("\n  ", problems));
        }

        [Test]
        public void EveryTreePrefab_ShowsVariantZeroOfItsAlbedoSheet_NotTheMaskOrTheNormal()
        {
            AcadianTreePrefabBuilder.BuildAll(Scratch);

            foreach (var p in _trees)
            {
                var sr = PrefabFor(p).GetComponent<SpriteRenderer>();
                Assert.IsNotNull(sr.sprite,
                    $"'{p.Stem}' has no sprite — the sheets import spriteMode Multiple, so a plain " +
                    "LoadAssetAtPath<Sprite> on them silently returns null.");
                Assert.AreEqual($"{p.Stem}_v0", sr.sprite.name,
                    $"'{p.Stem}' should show drawn variant 0 of its albedo sheet.");

                string texture = sr.sprite.texture.name;
                Assert.IsFalse(texture.EndsWith(TreeKitCatalog.MaskSuffix) ||
                               texture.EndsWith(TreeKitCatalog.NormalSuffix),
                    $"'{p.Stem}' is showing the '{texture}' sheet. The mask and normal are DATA " +
                    "channels for a lighting shader nothing has written yet — placed as a sprite " +
                    "they render as noise.");
            }
        }

        /// <summary>
        /// One material for all ten species, referenced as an ASSET. Touching
        /// <c>renderer.material</c> instead of <c>sharedMaterial</c> silently clones a material per
        /// tree — an asset leak that also guarantees nothing ever batches.
        /// </summary>
        [Test]
        public void EveryTreePrefab_SharesTheOneCanopyMaterialAsset_NoPerTreeClone()
        {
            AcadianTreePrefabBuilder.BuildAll(Scratch);
            var shared = AcadianTreeCatalog.LoadMaterial();
            Assert.IsNotNull(shared, $"{AcadianTreeCatalog.MaterialPath} did not load.");

            foreach (var p in _trees)
            {
                var sr = PrefabFor(p).GetComponent<SpriteRenderer>();
                Assert.AreSame(shared, sr.sharedMaterial,
                    $"'{p.Stem}' is not on the shared canopy material.");
                Assert.IsTrue(AssetDatabase.Contains(sr.sharedMaterial),
                    $"'{p.Stem}' carries a material CLONE, not the asset — somebody used " +
                    "renderer.material instead of sharedMaterial.");
            }
        }

        [Test]
        public void Builder_IsRerunnable_RebuildingInPlaceWithoutDuplicating()
        {
            int first = AcadianTreePrefabBuilder.BuildAll(Scratch);
            int second = AcadianTreePrefabBuilder.BuildAll(Scratch);

            Assert.AreEqual(first, second, "Re-running the builder must rebuild in place, not accumulate.");
            Assert.AreEqual(first, AssetDatabase.FindAssets("t:Prefab", new[] { Scratch }).Length,
                "Re-running left duplicate or orphaned prefabs behind.");
        }

        // =================================================================================
        // 1. do they sit on the ground?
        // =================================================================================

        /// <summary>
        /// The headline trap, asserted where the owner would SEE it: a planted tree's image must hang
        /// BELOW its transform by exactly that species' flare pad, because the near-root flare
        /// projects under the trunk foot at 40°. Bottom-centre would put <c>bounds.min.y</c> at 0 and
        /// sink the tree by the whole pad — the failure message carries that number per species.
        /// </summary>
        [Test]
        public void EveryTreePrefab_HangsItsFlareBelowTheGroundLine_NotSittingOnIt()
        {
            AcadianTreePrefabBuilder.BuildAll(Scratch);

            var table = new StringBuilder("[tree-place] flare drop below the transform (the trunk foot):\n");
            foreach (var p in _trees)
            {
                var e = p.Entry;
                var sprite = PrefabFor(p).GetComponent<SpriteRenderer>().sprite;

                float expected = AcadianTreeCatalog.FlareDropMetres(e, Ppu);   // pad / ppu
                // sprite.pivot is the pivot in px from the rect's own bottom-left, so the cell's
                // bottom EDGE sits that far below the transform. Deliberately not sprite.bounds:
                // these import Tight, and a mesh-derived extent would be measuring where the lowest
                // opaque pixel happens to fall rather than where the art says the ground line is.
                float actual = sprite.pivot.y / Ppu;

                table.AppendLine(
                    $"  {e.species,-16} pad {e.nearFlarePad,2} px = {expected:F3} m " +
                    $"(bottom-centre would sink it by exactly that)");

                Assert.AreEqual(expected, actual, 1e-4f,
                    $"{e.species}: the sprite hangs {actual:F3} m below its origin but its flare pad " +
                    $"is {e.nearFlarePad} px = {expected:F3} m. A bottom-centre pivot gives 0.000 m " +
                    $"here and plants the tree {expected:F3} m too high — it reads as an art bug, " +
                    "not a placement bug.");
            }
            Debug.Log(table.ToString());
        }

        /// <summary>
        /// The sabotage curve for the pivot, stated as a range rather than a single number so a
        /// re-bake that changes the flare cannot slip through. If this ever collapses toward zero the
        /// trap has genuinely gone away and the warnings can be relaxed; if it grows, they matter
        /// more.
        ///
        /// <para><b>⚠️ RE-DERIVED FOR THE PASS-2 RIG (2026-07-29), and this test firing is exactly
        /// what it was built to do.</b> Pass 1 gave every species a broad root skirt: 13–23 px of
        /// flare, 0.406–0.719 m. Pass 2 draws the root flare as <b>three splayed buttresses with dark
        /// splits</b> instead, which is a narrower footprint by design, so the pads dropped to
        /// <b>8–13 px = 0.250–0.406 m</b> — measured, not estimated.</para>
        ///
        /// <para>The trap has NOT gone away, which is the only question that decides whether these
        /// bounds move or the warnings do: 8 px at PPU 32 is a quarter of a metre of visible sink on
        /// the shallowest species, and the tree that used to be worst (23 px) is now the best case
        /// (13 px). So the range is re-measured against the new bake and the bounds keep their
        /// original job — refuse a flare that has collapsed to nothing, and refuse one that has
        /// grown, either of which means the bake moved under the placement code.</para>
        /// </summary>
        [Test]
        public void BottomCentre_WouldSinkEverySpecies_ByBetweenAQuarterAndFourTenthsOfAMetre()
        {
            var drops = _trees.Select(p => AcadianTreeCatalog.FlareDropMetres(p.Entry, Ppu)).ToList();
            float min = drops.Min(), max = drops.Max();

            Debug.Log($"[tree-place] bottom-centre sabotage: sinks the ten species by " +
                      $"{min:F3}–{max:F3} m ({_trees.Min(p => p.Entry.nearFlarePad)}–" +
                      $"{_trees.Max(p => p.Entry.nearFlarePad)} px at PPU {Ppu}). " +
                      "Pass 2 measured 2026-07-29: 0.250–0.406 m (8–13 px). Pass 1 was 0.406–0.719 m " +
                      "(13–23 px) — the buttressed root flare is a narrower footprint by design.");

            // 0.20 m = 6.4 px. Below that a bottom-centre pivot stops being a visible error and the
            // warnings this suite exists to justify would genuinely be noise.
            Assert.Greater(min, 0.20f,
                "Even the shallowest flare must be a visible error, or this whole warning is noise.");
            Assert.That(max, Is.InRange(0.35f, 0.5f),
                "The worst species sinks 13 px = 0.406 m under the pass-2 rig (it was 23 px = " +
                "0.719 m under pass 1). If that moved again, the bake changed — re-measure before " +
                "touching this bound.");
        }

        // =================================================================================
        // 2. is the scale right?
        // =================================================================================

        /// <summary>
        /// A tree's DRAWN height is its true height already foreshortened by the 40° camera
        /// (<c>heightScale = sin 40° = 0.766</c>), which is why <c>localScale</c> stays 1. The
        /// tempting "fix" — scaling a tree up to its <c>metres</c> — would make every one of them
        /// about 31% too tall AND knock the sprite off the pixel grid. Both numbers are logged.
        /// </summary>
        [Test]
        public void TheDrawnHeight_IsTheTrueHeightForeshortenedByTheCamera_NotTheRawMetres()
        {
            float heightScale = _contract.camera.heightScale;
            Assert.Greater(heightScale, 0.5f, "The contract's camera block did not parse.");

            var table = new StringBuilder(
                $"[tree-place] drawn height vs true height (heightScale {heightScale:F4}):\n");
            foreach (var p in _trees)
            {
                var e = p.Entry;
                float drawn = AcadianTreeCatalog.DrawnHeightMetres(e, Ppu);
                float expected = e.metres * heightScale;
                float ratio = drawn / expected;

                table.AppendLine(
                    $"  {e.species,-16} {e.metres:F1} m tall -> draws {drawn:F2} m " +
                    $"({e.pivotY} px), foreshortened expectation {expected:F2} m, ratio {ratio:F3}");

                // The upper bound is 1.12, re-derived for the pass-2 rig (2026-07-29). Two things
                // legitimately push a DRAWN height above the pure foreshortening of the TRUNK, and
                // neither is an error:
                //   • Crown depth. The cell's top row is set by the crown's silhouette, not the
                //     leader, and the crown extends toward the camera — that projection adds rows
                //     the trunk's own height×0.766 does not predict. Pass 2's serrated outline and
                //     hanging masses under the branch line extend it further than pass 1's smooth
                //     ellipsoid did.
                //   • The rig ROUNDS `metres` to one decimal, so `expected` carries up to ±1% of
                //     quantisation on its own (±1.04% on the 4.8 m cedar).
                // Measured span 2026-07-29: 0.998 (RedMaple) to 1.081 (WhiteBirch) — the old 1.08
                // cap clipped WhiteBirch by 0.0007. 1.12 keeps the assert's real job, which is to
                // catch a wrong PPU or a wrong camera constant: scaling to raw metres instead is
                // +30.5%, rejected with 18 points to spare.
                Assert.That(ratio, Is.InRange(0.92f, 1.12f),
                    $"{e.species} draws {drawn:F2} m above its trunk foot but a {e.metres:F1} m tree " +
                    $"under a {heightScale:F3} foreshortening should draw about {expected:F2} m. " +
                    "Either the PPU is wrong or the camera constant is (crown depth accounts for a " +
                    "few percent, not tens).");
            }

            float naive = 1f / heightScale;
            table.AppendLine($"  SABOTAGE: scaling a tree to its raw metres instead would be " +
                             $"{(naive - 1f) * 100f:F0}% too tall, and off the PPU {Ppu} pixel grid.");
            Debug.Log(table.ToString());

            Assert.Greater(naive, 1.25f,
                "If foreshortening ever stopped mattering, so would this test — and the class doc " +
                "warning it carries.");
        }

        // =================================================================================
        // 3. does each species move like itself?
        // =================================================================================

        [Test]
        public void EveryTreePrefab_CarriesItsOwnSpeciesTrunkAnchor()
        {
            AcadianTreePrefabBuilder.BuildAll(Scratch);

            foreach (var p in _trees)
            {
                var anchor = PrefabFor(p).GetComponent<TreeTrunkAnchor>();
                Assert.IsNotNull(anchor,
                    $"'{p.Stem}' has no TreeTrunkAnchor, so it would run on the material's single " +
                    "value — which is the reading this component exists to replace.");
                Assert.AreEqual(TreeKitCatalog.TrunkAnchorFor(_contract, p.Species, p.Entry.stage),
                                anchor.Anchor, 1e-6f,
                    $"'{p.Stem}' carries {anchor.Anchor:F4} but the contract says " +
                    $"{p.Entry.trunkAnchor:F4}. The value must come from the bake, not a literal.");
            }
        }

        /// <summary>
        /// The serialized float is only half the job: it has to reach the RENDERER. A
        /// MaterialPropertyBlock is not serialized by Unity, so this asserts against an INSTANCE
        /// (where <c>OnEnable</c> has run) rather than the prefab asset, and reads the value back off
        /// the renderer rather than off the field that was written to it.
        /// </summary>
        [Test]
        public void TheAnchorReachesTheRenderer_AndTheSpriteSurvivesTheHandover()
        {
            AcadianTreePrefabBuilder.BuildAll(Scratch);

            foreach (var p in _trees)
            {
                var instance = Object.Instantiate(PrefabFor(p));
                try
                {
                    var sr = instance.GetComponent<SpriteRenderer>();
                    Assert.IsTrue(sr.HasPropertyBlock(),
                        $"'{p.Stem}': no property block on the renderer — the per-species anchor " +
                        "never left the component and the material's single value is what runs.");
                    Assert.AreEqual(p.Entry.trunkAnchor, TreeTrunkAnchor.AnchorOn(sr), 1e-6f,
                        $"'{p.Stem}': the renderer is not carrying this species' anchor.");

                    // Handing a SpriteRenderer a FRESH property block drops the per-renderer
                    // overrides it keeps there — including its texture — and Tree.mat's _MainTex is
                    // empty, so the tree would draw as a white rectangle. Get-modify-Set is why it
                    // does not.
                    Assert.IsNotNull(sr.sprite,
                        $"'{p.Stem}': the instance lost its sprite when the anchor was applied.");
                }
                finally { Object.DestroyImmediate(instance); }
            }
        }

        /// <summary>
        /// THE MEASURED SABOTAGE for the anchor: flatten every species onto the single value
        /// <c>Tree.mat</c> ships and measure what it costs, in the shader's own terms.
        ///
        /// <para>Two different costs, and they are NOT the same size:</para>
        /// <list type="bullet">
        ///   <item><b>The planted band moves</b> — rows the shader holds exactly still that should be
        ///   swaying (or vice versa). Whole pixels, and it is what the anchor is FOR.</item>
        ///   <item><b>The canopy's motion changes</b> — and at the material's shipped sway settings
        ///   this comes out SUB-PIXEL, because the shader pixel-snaps its offset to the PPU 32 grid.
        ///   Logged in pixels so nobody has to take that on trust, and so it re-measures itself if
        ///   the owner turns the sway up.</item>
        /// </list>
        /// </summary>
        [Test]
        public void FlatteningTheAnchorOntoTheMaterialsSingleValue_MovesThePlantedLine()
        {
            var material = AcadianTreeCatalog.LoadMaterial();
            Assert.IsNotNull(material);
            float flat = material.GetFloat(TreeTrunkAnchor.ShaderProperty);

            // The shader's peak canopy travel at full wind, from the material's own tunables:
            //   offset = (windLean + gust*gustStrength) * (idleSway + windStrength*swayAmount) * bendW
            float peakTravelMetres =
                (material.GetFloat("_WindLean") + material.GetFloat("_GustStrength")) *
                (material.GetFloat("_IdleSway") + material.GetFloat("_SwayAmount"));

            var table = new StringBuilder(
                $"[tree-place] SABOTAGE — flattening every anchor to the material's {flat:F4}\n" +
                $"  (peak canopy travel at full wind {peakTravelMetres:F3} m = " +
                $"{peakTravelMetres * Ppu:F2} px, pixel-snapped by the shader)\n");

            int overAnchored = 0;
            float worstBandPx = 0f, worstTravelPx = 0f;

            foreach (var p in _trees)
            {
                var e = p.Entry;
                float own = e.trunkAnchor;
                if (own < flat) overAnchored++;

                // Rows that change between "held exactly still" and "moving at all".
                float bandPx = Mathf.Abs(own - flat) * e.cellH;

                // Worst canopy-weight difference anywhere up the cell, sampled per pixel row.
                float worstWeight = 0f;
                for (int row = 0; row <= e.cellH; row++)
                {
                    float uv = (float)row / e.cellH;
                    worstWeight = Mathf.Max(worstWeight,
                        Mathf.Abs(TreeTrunkAnchor.CanopyWeight(own, uv) -
                                  TreeTrunkAnchor.CanopyWeight(flat, uv)));
                }
                float travelPx = worstWeight * peakTravelMetres * Ppu;

                worstBandPx = Mathf.Max(worstBandPx, bandPx);
                worstTravelPx = Mathf.Max(worstTravelPx, travelPx);

                table.AppendLine(
                    $"  {e.species,-16} anchor {own:F4} vs {flat:F4} -> " +
                    $"planted line moves {bandPx,5:F1} px, peak canopy travel changes " +
                    $"{travelPx:F2} px ({worstWeight * 100f:F1}% of full sway)");
            }
            Debug.Log(table.ToString());

            Assert.GreaterOrEqual(overAnchored, 8,
                $"{overAnchored} of {_trees.Count} species sit below the material's {flat:F4}. The " +
                "measured finding was 8 of 10 over-anchored — if that changed, either the bake or " +
                "the material did.");
            Assert.Greater(worstBandPx, 5f,
                "The worst species' planted line should move by whole pixels under the flattened " +
                "anchor. If it does not, the per-species anchor is buying nothing and this should " +
                "go back to one material value.");

            // Stated as a fact rather than a hope: the MOTION difference is sub-pixel at the shipped
            // sway. It is the planted LINE that justifies the per-species value today; turn the sway
            // up and the motion difference starts to show too.
            Assert.Less(worstTravelPx, 1f,
                $"Peak canopy travel now differs by {worstTravelPx:F2} px between the per-species " +
                "anchor and the flattened one. That used to be sub-pixel at the shipped sway " +
                "settings — if the owner has turned the sway up, this test's note is out of date " +
                "(and the per-species anchor is now visible, which is good news).");
        }

        // =================================================================================
        // layering, and what the property block actually costs
        // =================================================================================

        [Test]
        public void TreesLayerByPosition_SoOneDownScreenDrawsInFrontOfOneUpScreen()
        {
            AcadianTreePrefabBuilder.BuildAll(Scratch);
            var prefab = PrefabFor(_trees[0]);

            var near = Object.Instantiate(prefab, new Vector3(0f, -2f, 0f), Quaternion.identity);
            var far = Object.Instantiate(prefab, new Vector3(0f, 2f, 0f), Quaternion.identity);
            try
            {
                int nearOrder = near.GetComponent<SpriteRenderer>().sortingOrder;
                int farOrder = far.GetComponent<SpriteRenderer>().sortingOrder;

                Assert.Greater(nearOrder, farOrder,
                    "A tree lower on screen (smaller Y) must draw IN FRONT of one higher up, or the " +
                    "player walks through trunks instead of past them.");
                foreach (int order in new[] { nearOrder, farOrder })
                    Assert.That(order, Is.InRange(2, 40),
                        "A tree's order left the decor safe band — it could sink behind the ground " +
                        "tiles or rise above the HUD.");
            }
            finally
            {
                Object.DestroyImmediate(near);
                Object.DestroyImmediate(far);
            }
        }

        /// <summary>
        /// What driving the anchor per renderer actually costs, MEASURED rather than asserted away.
        ///
        /// <para>Sprites batch only when material, TEXTURE and sorting order all agree. Every species
        /// is its own sheet (its own texture) and <see cref="YSortSprite"/> gives each tree an order
        /// off its world Y, so a mixed stand is already shattered into many batches before any
        /// property block appears. This counts, for a deterministic 200-tree mixed stand: how many
        /// draw calls the ideal batcher could reach WITHOUT per-renderer blocks, versus the one
        /// draw call per tree that a block forces. The gap is the real price.</para>
        ///
        /// <para>The number is logged, not silently swallowed — if it ever gets large, the fallback
        /// is a material per species (batching identical to the no-block case) at the cost of ten
        /// generated assets.</para>
        /// </summary>
        [Test]
        public void APropertyBlockCostsBatchingTheseTreesNeverHad()
        {
            const int Stand = 200;
            const float DepthMetres = 10f;   // a visible band of ground, not the whole region

            var rng = new System.Random(20260726);
            var keys = new HashSet<(string texture, int order)>();

            for (int i = 0; i < Stand; i++)
            {
                var e = _trees[rng.Next(_trees.Count)].Entry;
                float y = (float)(rng.NextDouble() * DepthMetres - DepthMetres * 0.8);
                int order = YSortSprite.OrderFor(y, 10f, 4f, 2, 40);
                keys.Add((e.species, order));
            }

            int withoutBlocks = keys.Count;   // best case: one draw call per distinct batch key
            int withBlocks = Stand;           // worst case: a block makes every renderer its own
            float extra = (withBlocks - withoutBlocks) / (float)withoutBlocks * 100f;

            Debug.Log(
                $"[tree-place] batching: a {Stand}-tree mixed stand over {DepthMetres:F0} m of depth " +
                $"already splits into {withoutBlocks} batch keys (species texture x Y-sorted order) " +
                $"before any property block. Per-renderer blocks take that to {withBlocks} draw " +
                $"calls — {extra:F0}% more. Fallback if it ever bites: a material per species, " +
                "which batches exactly like the no-block case.");

            Assert.Greater(withoutBlocks, Stand / 4,
                "If a stand collapsed into a handful of batches, the property block WOULD be the " +
                "expensive choice and this decision should be revisited.");
            Assert.Less(withBlocks - withoutBlocks, Stand,
                "Sanity: the block cannot cost more draw calls than there are trees.");
        }

        // =================================================================================
        // the lineup the owner is asked to look at
        // =================================================================================

        /// <summary>
        /// The lineup is the deliverable for the art judgement, so it gets an assert: every species
        /// present, exactly once, standing on one ground line, and spaced so no two crowns can
        /// overlap (otherwise it is not a comparison of silhouettes).
        /// </summary>
        [Test]
        public void TheLineup_StandsEverySpeciesOnOneGroundLine_WithNoOverlappingCrowns()
        {
            int placed = AcadianTreePrefabBuilder.PlaceLineup(Vector3.zero, Scratch);
            var root = GameObject.Find(AcadianTreePrefabBuilder.LineupRootName);

            try
            {
                Assert.AreEqual(_trees.Count, placed, "The lineup skipped a species.");
                Assert.IsNotNull(root);

                var trees = root.GetComponentsInChildren<TreeTrunkAnchor>()
                                .OrderBy(t => t.transform.localPosition.x).ToList();
                Assert.AreEqual(_trees.Count, trees.Count);

                for (int i = 0; i < trees.Count; i++)
                {
                    Assert.AreEqual(0f, trees[i].transform.localPosition.y, 1e-4f,
                        $"{trees[i].name} is off the shared ground line — heights can only be " +
                        "compared from one baseline.");

                    if (i == 0) continue;
                    var a = trees[i - 1].GetComponent<SpriteRenderer>();
                    var b = trees[i].GetComponent<SpriteRenderer>();
                    // Cell edges relative to each tree's own transform, from the pivot and rect
                    // (see the flare test for why not sprite.bounds).
                    float aRight = a.transform.localPosition.x +
                                   (a.sprite.rect.width - a.sprite.pivot.x) / Ppu;
                    float bLeft = b.transform.localPosition.x - b.sprite.pivot.x / Ppu;
                    float gap = bLeft - aRight;
                    Assert.Greater(gap, 0f,
                        $"{a.name} and {b.name} overlap by {-gap:F2} m — their silhouettes cannot be " +
                        "told apart.");
                }
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }
        }
    }
}
