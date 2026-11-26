using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards for the CLIFF MIX (owner 11 25: <i>"a mix of our current cliffs and the boards cliffs for variance"</i>).
    ///
    /// <para><b>What these protect.</b> Not the look: the owner's eye owns that, and CI has no graphics device. They
    /// protect the three things the look stands on. The choice is <b>deterministic</b> from a toe position and a seed
    /// (rule 5: nothing random, nothing saved). It comes in <b>runs</b>, not a checkerboard. And it is <b>data</b>:
    /// the Defs exist, carry stable ids, name skins that exist, and the St Peters coast really wears both.</para>
    /// </summary>
    public class CliffFaceMixTests
    {
        const string SkinsFolder = "Assets/_Project/Data/Terrain/CliffSkins";
        const string StPetersMixId = "cliffmix.st_peters";
        const string StPetersScene = "StPeters";
        const string CurrentSkinId = "cliffskin.current_px";
        const string BoardSkinId = "cliffskin.board_bedded";

        /// <summary>Two stations this close (m) are neighbours on one stretch of coast.</summary>
        const float NeighbourMetres = 1.5f;

        /// <summary>At most this share of neighbouring stations may differ in skin: a checkerboard flips at half,
        /// a coast of runs a few tens of metres long at a few percent.</summary>
        const float MaxNeighbourFlipRate = 0.06f;

        /// <summary>How far the St Peters coast's own share may sit from the Def's (the coast is only a few runs
        /// long, so it lands near the plan's share, not on it).</summary>
        const float CoastShareTolerance = 0.15f;

        static readonly Regex IdRx = new Regex("^[a-z]+\\.[a-z0-9_]+$");

        [SetUp] public void ForgetTheCache() => CliffFaceMixDef.ClearCache();

        // ---- the arithmetic --------------------------------------------------------------------------------

        [Test]
        public void TheWeight_IsTheSame_EveryTimeItIsAsked()
        {
            for (int i = 0; i < 200; i++)
            {
                var p = new Vector2(-300f + i * 3.7f, -120f + i * 1.3f);
                float a = CliffFaceMixMath.VariantWeight(p, 0.45f, 16f, 3f, 1);
                float b = CliffFaceMixMath.VariantWeight(p, 0.45f, 16f, 3f, 1);
                Assert.AreEqual(a, b, 0f, $"the weight at {p} changed between two asks");
            }
        }

        [Test]
        public void TheCornerHash_IsPinned_SoAPlatformCannotMoveTheCoast()
        {
            // Pinned values: integer arithmetic only, so every machine and every build gives these exactly.
            Assert.AreEqual(CliffFaceMixMath.CornerHash(0, 0, 1), CliffFaceMixMath.CornerHash(0, 0, 1));
            Assert.AreNotEqual(CliffFaceMixMath.CornerHash(0, 0, 1), CliffFaceMixMath.CornerHash(0, 0, 2),
                "the seed does not reach the hash");
            Assert.AreNotEqual(CliffFaceMixMath.CornerHash(1, 0, 1), CliffFaceMixMath.CornerHash(0, 1, 1),
                "x and y hash alike: the noise would be mirrored on the diagonal");
            for (int x = -50; x < 50; x++)
            {
                float h = CliffFaceMixMath.CornerHash(x, x * 7 - 3, 1125);
                Assert.That(h, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void ADifferentSeed_MovesTheStretches()
        {
            int differ = 0, n = 400;
            for (int i = 0; i < n; i++)
            {
                var p = new Vector2(i * 2.1f, (i % 20) * 9f);
                bool a = CliffFaceMixMath.VariantWeight(p, 0.45f, 16f, 3f, 1) > 0.5f;
                bool b = CliffFaceMixMath.VariantWeight(p, 0.45f, 16f, 3f, 2) > 0.5f;
                if (a != b) differ++;
            }
            Assert.Greater(differ, n / 10, "two seeds draw nearly the same coast: the seed is not doing its job");
        }

        [Test]
        public void ShareZero_IsNoVariant_AndShareOne_IsAllVariant_Exactly()
        {
            for (int i = 0; i < 100; i++)
            {
                var p = new Vector2(i * 5.3f - 200f, i * 2.9f - 80f);
                Assert.AreEqual(0f, CliffFaceMixMath.VariantWeight(p, 0f, 16f, 3f, 1));
                Assert.AreEqual(1f, CliffFaceMixMath.VariantWeight(p, 1f, 16f, 3f, 1));
            }
        }

        [Test]
        public void OverAWidePlan_TheShare_IsTheShareAsked()
        {
            foreach (float share in new[] { 0.2f, 0.45f, 0.7f })
            {
                int v = 0, n = 0;
                for (int j = 0; j < 120; j++)
                    for (int i = 0; i < 120; i++)
                    {
                        if (CliffFaceMixMath.VariantWeight(new Vector2(i * 4f, j * 4f), share, 16f, 0f, 1) > 0.5f) v++;
                        n++;
                    }
                Assert.AreEqual(share, v / (float)n, 0.08f, $"asked for {share:P0} of the plan, got {v / (float)n:P0}");
            }
        }

        [Test]
        public void RaisingTheShare_OnlyEverAddsCoast()
        {
            for (int i = 0; i < 300; i++)
            {
                var p = new Vector2(i * 3.1f - 400f, (i % 17) * 6f - 50f);
                float lo = CliffFaceMixMath.VariantWeight(p, 0.3f, 16f, 3f, 1);
                float hi = CliffFaceMixMath.VariantWeight(p, 0.6f, 16f, 3f, 1);
                Assert.GreaterOrEqual(hi, lo, $"at {p} raising the share took the variant away");
            }
        }

        [Test]
        public void TheChange_IsABandOfAboutTheBlendWidth_NotALine()
        {
            // Walk a long straight line; every change of skin passes through partial weights, over a few metres.
            int partial = 0, steps = 0, changes = 0; bool? last = null;
            for (float x = 0f; x < 4000f; x += 0.25f)
            {
                float w = CliffFaceMixMath.VariantWeight(new Vector2(x, 13f), 0.45f, 16f, 3f, 1);
                if (w > 0f && w < 1f) partial++;
                bool v = w > 0.5f;
                if (last.HasValue && last.Value != v) changes++;
                last = v; steps++;
            }
            Assert.Greater(changes, 0, "a 4 km line never changed skin");
            float bandMetres = partial * 0.25f / changes;
            Assert.That(bandMetres, Is.InRange(0.75f, 12f),
                $"each change spans {bandMetres:F1} m of partial weight: the blend width is not reaching the weight");
        }

        [Test]
        public void TheBlockPick_CoversAboutTheWeight_AndIsWholeAtTheEnds()
        {
            var block = new Vector2(6f, 3f);
            Assert.IsFalse(CliffFaceMixMath.PicksVariant(0f, new Vector2(3, 4), block));
            Assert.IsTrue(CliffFaceMixMath.PicksVariant(1f, new Vector2(3, 4), block));
            int on = 0, n = 0;
            for (int y = 0; y < 300; y += 3)
                for (int x = 0; x < 600; x += 6) { if (CliffFaceMixMath.PicksVariant(0.5f, new Vector2(x, y), block)) on++; n++; }
            Assert.AreEqual(0.5f, on / (float)n, 0.1f, "half weight does not cover about half the blocks");
            // A block is whole: every texel inside one block makes the same pick.
            bool first = CliffFaceMixMath.PicksVariant(0.5f, new Vector2(60, 30), block);
            for (int dy = 0; dy < 3; dy++)
                for (int dx = 0; dx < 6; dx++)
                    Assert.AreEqual(first, CliffFaceMixMath.PicksVariant(0.5f, new Vector2(60 + dx, 30 + dy), block));
        }

        // ---- the data --------------------------------------------------------------------------------------

        static List<CliffFaceSkinDef> Skins() =>
            AssetDatabase.FindAssets("t:" + nameof(CliffFaceSkinDef), new[] { SkinsFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<CliffFaceSkinDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null).ToList();

        [Test]
        public void EverySkin_HasAStableId_OneAFile_AndItsPaletteIsWhereItSays()
        {
            List<CliffFaceSkinDef> skins = Skins();
            Assert.That(skins.Select(s => s.Id), Does.Contain(CurrentSkinId).And.Contain(BoardSkinId));
            Assert.AreEqual(skins.Count, skins.Select(s => s.Id).Distinct().Count(), "two skins share an id");
            foreach (CliffFaceSkinDef s in skins)
            {
                Assert.IsTrue(s.Id.StartsWith(CliffFaceSkinDef.IdPrefix) && IdRx.IsMatch(s.Id), $"'{s.Id}' is not cliffskin.snake_case");
                if (s.IsMaterialPalette) continue;
                Assert.IsNotNull(s.Palette, $"{s.Id} names a palette path but holds no palette");
                Assert.AreEqual(s.PalettePath, AssetDatabase.GetAssetPath(s.Palette),
                    $"{s.Id}'s palette is not the file its baker writes");
                Assert.AreEqual(8, s.Palette.width, $"{s.Id}'s palette is not the kit's 8 by 32 LUT");
                Assert.AreEqual(32, s.Palette.height, $"{s.Id}'s palette is not the kit's 8 by 32 LUT");
                Assert.AreEqual(FilterMode.Point, s.Palette.filterMode, $"{s.Id}'s palette is filtered: bands would bleed");
            }
        }

        [Test]
        public void StPeters_HasOneMix_ThatNamesTheCurrentAndTheBoardSkins()
        {
            CliffFaceMixDef[] all = Resources.LoadAll<CliffFaceMixDef>(CliffFaceMixDef.ResourcesFolder);
            Assert.AreEqual(all.Length, all.Select(m => m.Id).Distinct().Count(), "two mixes share an id");
            Assert.AreEqual(all.Length, all.Select(m => m.SceneName).Distinct().Count(), "two mixes claim one scene");
            foreach (CliffFaceMixDef m in all)
                Assert.IsTrue(m.Id.StartsWith(CliffFaceMixDef.IdPrefix) && IdRx.IsMatch(m.Id), $"'{m.Id}' is not cliffmix.snake_case");

            CliffFaceMixDef mix = CliffFaceMixDef.ForScene(StPetersScene);
            Assert.IsNotNull(mix, "St Peters has no cliff mix");
            Assert.AreEqual(StPetersMixId, mix.Id);
            Assert.AreEqual(CurrentSkinId, mix.BaseSkin != null ? mix.BaseSkin.Id : null);
            Assert.AreEqual(BoardSkinId, mix.VariantSkin != null ? mix.VariantSkin.Id : null);
            Assert.IsTrue(mix.IsActive, "the St Peters mix draws nothing (no variant palette, or share 0)");
            Assert.That(mix.VariantShare, Is.InRange(0.05f, 0.95f));
            Assert.Greater(mix.RunMetres, NeighbourMetres * 4f, "runs this short read as a checkerboard");
            Assert.That(mix.BlendMetres, Is.InRange(0f, mix.RunMetres));
            Assert.IsNull(CliffFaceMixDef.ForScene("NoSuchRegion"), "a region with no mix must draw as before");
        }

        [Test]
        public void TheStPetersCoast_WearsBothSkins_InRuns_NotACheckerboard()
        {
            CliffFaceMixDef mix = CliffFaceMixDef.ForScene(StPetersScene);
            Assert.IsNotNull(mix, "St Peters has no cliff mix");
            List<Vector2> toes = StPetersCliffWalls.LoadDefs().Where(d => d.IsLive).SelectMany(d => d.Toe).ToList();
            Assert.Greater(toes.Count, 100, "too few live St Peters toe stations to judge the mix");

            bool[] v = toes.Select(p => mix.VariantWeightAt(p) > 0.5f).ToArray();
            float share = v.Count(b => b) / (float)v.Length;
            int pairs = 0, flips = 0;
            for (int i = 0; i < toes.Count; i++)
                for (int j = i + 1; j < toes.Count; j++)
                    if ((toes[i] - toes[j]).sqrMagnitude < NeighbourMetres * NeighbourMetres)
                    {
                        pairs++;
                        if (v[i] != v[j]) flips++;
                    }
            float flipRate = flips / (float)Mathf.Max(1, pairs);
            TestContext.WriteLine($"St Peters cliff mix: {toes.Count} live toe stations, {share:P0} in {mix.VariantSkin.Id} " +
                                  $"(the Def asks {mix.VariantShare:P0}); {pairs} neighbour pairs, {flipRate:P1} change skin.");

            Assert.Greater(share, 0.05f, "the St Peters coast never wears the board skin");
            Assert.Less(share, 0.95f, "the St Peters coast never wears the current skin");
            Assert.AreEqual(mix.VariantShare, share, CoastShareTolerance,
                "the coast's own share has wandered far from the Def's: retune the seed or the share");
            Assert.Greater(pairs, 100, "the neighbour test found too few neighbouring stations to judge");
            Assert.Less(flipRate, MaxNeighbourFlipRate,
                $"{flipRate:P1} of neighbouring stations change skin: that is a checkerboard, not runs of rock");
        }

        /// <summary>Pass 2 (seat, 11 25): every framed stretch must show some of each skin. So around every live toe
        /// station, the coast within <see cref="ReachRadiusMetres"/> wears both: no long wall is all one skin.</summary>
        const float ReachRadiusMetres = 20f;
        const int ReachMinStations = 20;

        [Test]
        public void NoFortyMetreReachOfTheStPetersCoast_IsAllOneSkin()
        {
            CliffFaceMixDef mix = CliffFaceMixDef.ForScene(StPetersScene);
            Assert.IsNotNull(mix, "St Peters has no cliff mix");
            List<Vector2> toes = StPetersCliffWalls.LoadDefs().Where(d => d.IsLive).SelectMany(d => d.Toe).ToList();
            bool[] v = toes.Select(p => mix.VariantWeightAt(p) > 0.5f).ToArray();
            int judged = 0, oneSkin = 0;
            float worst = 1f;
            Vector2 worstAt = default;
            for (int i = 0; i < toes.Count; i++)
            {
                int n = 0, var = 0;
                for (int j = 0; j < toes.Count; j++)
                    if ((toes[i] - toes[j]).sqrMagnitude < ReachRadiusMetres * ReachRadiusMetres)
                    {
                        n++;
                        if (v[j]) var++;
                    }
                if (n < ReachMinStations) continue;
                judged++;
                float f = var / (float)n, mixed = Mathf.Min(f, 1f - f);
                if (mixed <= 0f) oneSkin++;
                if (mixed < worst) { worst = mixed; worstAt = toes[i]; }
            }
            TestContext.WriteLine($"{judged} reaches judged; the least mixed is {worst:P0} the minority skin, at {worstAt}.");
            Assert.Greater(judged, 100, "too few reaches to judge");
            Assert.AreEqual(0, oneSkin,
                $"{oneSkin} reaches of {ReachRadiusMetres * 2f:0} m are all one skin (worst at {worstAt}): retune the run or the seed");
        }
    }
}
