using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>What the ground-cover retune costs to DRAW</b> — measured, reported, and bounded where a
    /// bound can honestly be drawn.
    ///
    /// <para><b>⚠ WHAT THIS CANNOT TELL YOU.</b> Not the frame time. Nothing headless can: CI has no
    /// graphics device, and the owner's 4060 is the only instrument that answers "does it run". What a
    /// test CAN measure is the two numbers frame time is spent on — how many renderers are inside the
    /// camera at gameplay framing, and how many distinct BATCHES they fall into — and those it measures
    /// on the same deciders the build uses, so the numbers in the log are the island's numbers.</para>
    ///
    /// <para><b>Why the batch count is the one to watch, not the tuft count.</b> 2D sprites batch by
    /// material AND texture, and only within a run of equal sorting order. <see cref="YSortSprite"/>
    /// gives every metre of world Y four distinct orders, so a screen of grass is chopped into bands
    /// and each band re-batches per texture it contains. Twenty thousand tufts on an island is cheap;
    /// two hundred textures interleaved across forty sorting bands on SCREEN is not. The island-wide
    /// count is a scene-size and load-time number; this is the drawing one.</para>
    ///
    /// <para><b>⚠ AND WHY THERE IS NO SPRITE ATLAS.</b> The obvious fix — pack the 29 grass PNGs into
    /// one atlas page so a screen of grass collapses to one batch per sorting band — is CLOSED, and it
    /// is worth writing down so nobody spends a day discovering it. <c>HiddenHarboursGrass.shader</c>
    /// bends the blade by <c>IN.uv.y</c> (0 at the root, 1 at the tip). An atlas remaps every sprite's
    /// uv into a page rectangle, so uv.y stops being "how far up this blade" and becomes "how far up
    /// the page" — every tuft would bend by an arbitrary constant. It is the same texture-space-uv trap
    /// that produced the shadow-shear defect in #431 and the one <c>FlowerCatalogTests</c> already
    /// guards for the flower sheets. If the batch count ever has to come down, the lever is fewer
    /// distinct textures IN A SORTING BAND, or merged static chunks outside the player's band — never
    /// an atlas.</para>
    /// </summary>
    public class StPetersGroundCoverBudgetTests
    {
        GameObject _go;
        TidalTerrain _terrain;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TidalTerrain_GroundCoverBudget");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        /// <summary>The camera at ON-FOOT framing, in metres — the tightest gameplay view, which is
        /// where the grass is densest on screen. Width is the design 16:9.</summary>
        const float ViewHeightM = HiddenHarbours.App.CameraFollow.OnFootWorldHeightMeters;
        const float ViewWidthM = ViewHeightM * 16f / 9f;

        /// <summary>Renderers the ground cover may put inside the camera at once. The 2026-08-05 retune
        /// measured 395 on its worst screen; a few hundred sprites is nothing for the target GPU, so
        /// this is set where a REGRESSION shows (someone drops the step again) rather than where the
        /// hardware complains.</summary>
        const int OnScreenTuftBudget = 900;

        /// <summary>Upper bound on the grass batches in one screen — measured as distinct
        /// (texture, sorting order) pairs, which is what the 2D renderer cannot merge across. The
        /// retune measured ≤114.</summary>
        const int OnScreenBatchBudget = 260;

        [Test]
        public void AScreenOfMeadow_StaysInsideItsRendererAndBatchBudget()
        {
            var library = GrassLibraryCatalog.Load();
            if (library == null || library.Entries.Count == 0)
                Assert.Ignore("The grass library manifest is not on disk in this checkout.");

            var imported = GrassLibraryCatalog.Imported(library);
            if (imported.Count == 0)
                Assert.Ignore("No grass sprite in the library is imported in this checkout (LFS).");

            var chooser = new StPetersWoodsPlanter.GrassArtChooser(imported);
            var sites = StPetersGrass.Scatter(_terrain);
            Assert.Greater(sites.Count, 0, "sanity: the meadow scattered nothing");

            // Slide the camera over the island and take the WORST screen — a mean would hide the one
            // view that stutters, and the worst screen is the one the owner will find.
            int worstTufts = 0, worstBatches = 0;
            Vector2 worstAt = Vector2.zero;

            // Bucket by view-sized cells so each window only tests its own neighbourhood.
            var buckets = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < sites.Count; i++)
            {
                var key = (Mathf.FloorToInt(sites[i].Position.x / ViewWidthM),
                           Mathf.FloorToInt(sites[i].Position.y / ViewHeightM));
                if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<int>();
                list.Add(i);
            }

            var pairs = new HashSet<(string, int)>();
            var candidates = new List<int>();
            foreach (var kv in buckets)
            {
                // The window anchored on this bucket, plus the eight around it (a tuft in a neighbour
                // can still fall inside the window).
                var centre = new Vector2((kv.Key.Item1 + 0.5f) * ViewWidthM,
                                         (kv.Key.Item2 + 0.5f) * ViewHeightM);
                candidates.Clear();
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (buckets.TryGetValue((kv.Key.Item1 + dx, kv.Key.Item2 + dy), out var near))
                        candidates.AddRange(near);

                pairs.Clear();
                int inView = 0;
                foreach (int i in candidates)
                {
                    var s = sites[i];
                    if (Mathf.Abs(s.Position.x - centre.x) > ViewWidthM * 0.5f) continue;
                    if (Mathf.Abs(s.Position.y - centre.y) > ViewHeightM * 0.5f) continue;
                    inView++;

                    var entry = chooser.Choose(s);
                    if (entry == null) continue;
                    // The planter's own defaults for a static tuft (YSortSprite's serialized values).
                    int order = YSortSprite.OrderFor(s.Position.y, 10f, 4f, 2, 40);
                    pairs.Add((entry.Path, order));
                }

                if (inView > worstTufts) { worstTufts = inView; worstAt = centre; }
                if (pairs.Count > worstBatches) worstBatches = pairs.Count;
            }

            Debug.Log($"[GroundCoverBudget] {sites.Count} tufts island-wide · worst screen " +
                      $"({ViewWidthM:F1} × {ViewHeightM:F1} m at {worstAt}): {worstTufts} renderers, " +
                      $"≤{worstBatches} grass batches.");

            Assert.Less(worstTufts, OnScreenTuftBudget,
                $"{worstTufts} tufts inside one on-foot screen. That is the number the GPU actually " +
                "draws, and it has moved a long way — check StPetersGrass.GrassStep before raising " +
                "this bound.");

            Assert.Less(worstBatches, OnScreenBatchBudget,
                $"≤{worstBatches} grass batches in one screen (distinct texture × sorting order). The " +
                "levers are fewer distinct textures per sorting band or merged static chunks — NOT a " +
                "SpriteAtlas, which would break the shader's uv.y bend (see this class's remarks).");
        }

        /// <summary>
        /// 🔴 <b>The sorting resolution the retune spent.</b> Every metre of world Y gets four sorting
        /// orders (<see cref="YSortSprite"/>'s default), so two tufts one grid step apart must land on
        /// DIFFERENT orders — otherwise the player can never walk between them, and rows of grass start
        /// sorting in slabs however correct the rest of the machinery is.
        ///
        /// <para>This is the EditMode half of
        /// <c>GroundCoverYSortPlayTests.TheWalkerInterleavesWithEveryTuftSeparately</c>: that one owns
        /// the mechanism, this one owns the number, because the grid constant lives in an editor-only
        /// assembly a PlayMode test cannot reference.</para>
        /// </summary>
        [Test]
        public void TheSortingBandResolvesOneGrassStep()
        {
            int a = YSortSprite.OrderFor(0f, 10f, 4f, 2, 40);
            int b = YSortSprite.OrderFor(StPetersGrass.GrassStep, 10f, 4f, 2, 40);

            Assert.AreNotEqual(a, b,
                $"two tufts one GrassStep ({StPetersGrass.GrassStep} m) apart both sort to order {a}. " +
                "The grid has outrun YSortSprite's 4 orders per metre, so rows of grass now sort in " +
                "slabs and the player cannot walk between them. Coarsen the step, or give the grass a " +
                "finer orderPerUnit.");
        }

        /// <summary>
        /// 🔴🔴 <b>A PRE-EXISTING DEFECT, MEASURED — not introduced by the ground cover and not fixed
        /// by it.</b>
        ///
        /// <para><see cref="YSortSprite"/> clamps its order into 2…40 around a base of 10 at 4 per
        /// metre, so it only RESOLVES world Y from −7.5 m to +2.0 m — a 9.5 m window. St Peters is
        /// 140 m tall. Every sprite outside that window saturates on the same order (2 up-island, 40
        /// down-island) and ties with its neighbours, and the PLAYER carries the same component with
        /// the same defaults, so up at the village the walker and the grass around them are all on
        /// order 2 and interleave by draw order rather than by position. The clamp is deliberate and
        /// tested (<c>YSortSpriteTests.OrderFor_ClampsToSafeBand</c>) — it exists so decor can never
        /// slip behind the water or above the HUD — but nothing sized the band against a region.</para>
        ///
        /// <para><b>Why this test only reports.</b> Fixing it is not an art-pipeline change: the band's
        /// ends are load-bearing for every fixed sorting order in the game (<c>StPetersWharf</c> solves
        /// its deck orders against "YSortSprite's band starts at 2"; the shore plants' submerged −8 sits
        /// below it by design), so re-basing the sort — camera-relative Y, or a wider band with every
        /// fixed order re-derived — is a lead-architect call. Asserting the island were correct would
        /// simply ship a red test. So this measures the cost and puts the number in the log, which is
        /// what a decision needs.</para>
        /// </summary>
        [Test]
        public void TheSortingBandsReach_IsMeasuredAgainstTheIsland()
        {
            var sites = StPetersGrass.Scatter(_terrain);
            Assert.Greater(sites.Count, 0, "sanity: the meadow scattered nothing");

            int saturated = 0;
            foreach (var s in sites)
            {
                int order = YSortSprite.OrderFor(s.Position.y, 10f, 4f, 2, 40);
                if (order == 2 || order == 40) saturated++;
            }

            float lowY = (10f - 40f) / 4f, highY = (10f - 2f) / 4f;
            Debug.Log($"[GroundCoverBudget] YSortSprite resolves world Y {lowY:F1} … {highY:F1} m " +
                      $"({highY - lowY:F1} m of a {StPetersBuilder.IslandRadiusY * 2f:F0} m island). " +
                      $"{saturated} of {sites.Count} tufts ({saturated * 100f / sites.Count:F0}%) sit " +
                      "on a saturated order and tie with their neighbours. PRE-EXISTING — see this " +
                      "test's remarks before treating it as a ground-cover regression.");

            // The only claim safe to ASSERT: the band still resolves SOMETHING, i.e. the component has
            // not been reconfigured into a single flat order for everything.
            Assert.Less(saturated, sites.Count,
                "every tuft on the island is on a saturated sorting order — YSortSprite is no longer " +
                "resolving anything at all, which is a different and much worse bug than the reach.");
        }

        /// <summary>
        /// 🔴 <b>The atlas trap, guarded rather than remembered.</b> A future pass reaching for the
        /// obvious batching win would break every blade's bend silently — the grass would still draw,
        /// it would just stop moving correctly, which is the kind of defect that ships. Same shape as
        /// <c>FlowerCatalogTests.NoSpriteAtlas_PacksTheFlowerSheets</c>, for the same reason.
        /// </summary>
        [Test]
        public void NoSpriteAtlas_PacksTheGrassLibrary()
        {
            var library = GrassLibraryCatalog.Load();
            if (library == null || library.Entries.Count == 0)
                Assert.Ignore("The grass library manifest is not on disk in this checkout.");

            var grassPaths = new HashSet<string>(library.Entries.Select(e => e.Path));

            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:SpriteAtlas"))
            {
                string atlasPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var atlas = UnityEditor.AssetDatabase
                    .LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(atlasPath);
                if (atlas == null) continue;

                foreach (var packable in UnityEditor.U2D.SpriteAtlasExtensions.GetPackables(atlas))
                {
                    if (packable == null) continue;
                    string packedPath = UnityEditor.AssetDatabase.GetAssetPath(packable);
                    bool hitsGrass = grassPaths.Contains(packedPath) ||
                                     grassPaths.Any(p => p.StartsWith(packedPath + "/",
                                                                     System.StringComparison.Ordinal));
                    Assert.IsFalse(hitsGrass,
                        $"🔴 '{atlasPath}' packs the grass library ('{packedPath}'). " +
                        "HiddenHarboursGrass.shader bends each blade by IN.uv.y — 0 at the root, 1 at " +
                        "the tip. An atlas remaps that into a page rectangle, so every tuft would bend " +
                        "by an arbitrary constant instead of hinging at its own root. The batching win " +
                        "is real and it is not available here.");
                }
            }
        }
    }
}
