using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The flower half of the decor prefab builder — what the owner actually drags into a scene.
    ///
    /// <para>Builds into a SCRATCH folder rather than the real <c>Assets/_Project/Prefabs/Decor/Flowers</c>,
    /// because that tree is generated build output that is deliberately untracked and also sits in the owner's own
    /// working copy — a test must not churn it.</para>
    /// </summary>
    public class FlowerPrefabBuilderTests
    {
        private const string Scratch = "Assets/_Project/Prefabs/_FlowerBuilderTestScratch";

        [SetUp]
        public void SetUp()
        {
            if (AssetDatabase.IsValidFolder(Scratch)) AssetDatabase.DeleteAsset(Scratch);
            AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "_FlowerBuilderTestScratch");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Scratch)) AssetDatabase.DeleteAsset(Scratch);
        }

        [Test]
        public void Builder_MakesOnePrefabPerSheetOnDisk_NamedForItsSheet()
        {
            int built = DecorPrefabBuilder.BuildFlowerPrefabs(Scratch);

            var expected = FlowerCatalog.SheetStems()
                .Where(s => FlowerCatalog.TrySplit(s, out _, out _))
                .ToList();

            Assert.AreEqual(expected.Count, built,
                "Every sliced flower sheet should yield exactly one drag-and-drop prefab.");

            foreach (string stem in expected)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Scratch}/{stem}.prefab");
                Assert.IsNotNull(prefab, $"No prefab was built for '{stem}'. The owner would have nothing to drag.");
                Assert.AreEqual(stem, prefab.name);
            }
        }

        /// <summary>
        /// The canonical decor prefab is exactly three components: a SpriteRenderer, a wind material, a
        /// YSortSprite. No Animator — the flowers' sway is the shader reading the shared sim wind, and an
        /// Animator per flower would batch-break AND be deaf to the wind. If one ever appears here, that is the
        /// design being quietly reversed.
        /// </summary>
        [Test]
        public void EveryFlowerPrefab_IsTheCanonicalDecorShape_AndCarriesNoAnimator()
        {
            DecorPrefabBuilder.BuildFlowerPrefabs(Scratch);

            var problems = new List<string>();
            foreach (string stem in FlowerCatalog.SheetStems())
            {
                if (!FlowerCatalog.TrySplit(stem, out _, out _)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Scratch}/{stem}.prefab");
                if (prefab == null) { problems.Add($"{stem}: not built"); continue; }

                var components = prefab.GetComponents<Component>();
                var kinds = components.Select(c => c.GetType().Name).OrderBy(n => n).ToArray();
                var wanted = new[] { "SpriteRenderer", "Transform", "YSortSprite" }.OrderBy(n => n).ToArray();
                if (!kinds.SequenceEqual(wanted))
                    problems.Add($"{stem}: components are [{string.Join(", ", kinds)}], expected " +
                                 $"[{string.Join(", ", wanted)}]");

                Assert.IsNull(prefab.GetComponentInChildren<Animator>(true),
                    $"{stem} carries an Animator. The flowers sway via the shader off the shared sim wind — an " +
                    "Animator per flower cannot react to wind and breaks batching (CLAUDE.md rule 7).");

                if (prefab.transform.localScale != Vector3.one)
                    problems.Add($"{stem}: scale {prefab.transform.localScale}, expected 1 (honest metric size).");
            }
            Assert.IsEmpty(problems, string.Join("\n  ", problems));
        }

        [Test]
        public void EveryFlowerPrefab_UsesItsOwnTiersWindMaterial_SoTheBendMatchesTheSprite()
        {
            DecorPrefabBuilder.BuildFlowerPrefabs(Scratch);

            foreach (string stem in FlowerCatalog.SheetStems())
            {
                if (!FlowerCatalog.TrySplit(stem, out _, out FlowerCatalog.Tier tier)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Scratch}/{stem}.prefab");
                Assert.IsNotNull(prefab, stem);

                var sr = prefab.GetComponent<SpriteRenderer>();
                Assert.AreSame(FlowerCatalog.MaterialFor(tier), sr.sharedMaterial,
                    $"'{stem}' is a {tier} but is not on the {tier} material. The tiers differ in _Rows (the bend " +
                    "line) and _RootedBend (hinge vs drift) — the wrong one bends the flower wrongly.");
            }
        }

        [Test]
        public void EveryFlowerPrefab_ShowsTheNeutralPoseOfItsFirstRow()
        {
            DecorPrefabBuilder.BuildFlowerPrefabs(Scratch);

            foreach (string stem in FlowerCatalog.SheetStems())
            {
                if (!FlowerCatalog.TrySplit(stem, out _, out FlowerCatalog.Tier tier)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Scratch}/{stem}.prefab");
                var sr = prefab.GetComponent<SpriteRenderer>();

                Assert.IsNotNull(sr.sprite, $"'{stem}' prefab has no sprite — the sheets import spriteMode " +
                                            "Multiple, so a LoadAssetAtPath<Sprite> would silently give null.");
                Assert.AreEqual($"{stem}_0", sr.sprite.name,
                    $"'{stem}' should show cell 0 (row 0, the neutral drawn pose).");
                Assert.AreSame(FlowerCatalog.LoadNeutral(stem, tier, 0), sr.sprite);
            }
        }

        /// <summary>
        /// Flowers layer BY POSITION, like the rest of the ¾ decor: a flower below you draws in front, one above
        /// you draws behind. That is <see cref="YSortSprite"/>'s job, and it OWNS sortingOrder — it recomputes it
        /// from world Y on enable, overwriting whatever the builder set (which is why asserting the builder's
        /// fixed default here would be asserting a value nothing ever reads). So this pins the behaviour the
        /// owner can actually see: order falls as the flower moves up-screen, and never leaves the decor band.
        /// </summary>
        [Test]
        public void FlowersLayerByPosition_SoOneDownScreenDrawsInFrontOfOneUpScreen()
        {
            DecorPrefabBuilder.BuildFlowerPrefabs(Scratch);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Scratch}/OxeyeDaisySingle.prefab");
            Assert.IsNotNull(prefab);

            var near = Object.Instantiate(prefab, new Vector3(0f, -2f, 0f), Quaternion.identity);
            var far = Object.Instantiate(prefab, new Vector3(0f, 2f, 0f), Quaternion.identity);
            try
            {
                int nearOrder = near.GetComponent<SpriteRenderer>().sortingOrder;
                int farOrder = far.GetComponent<SpriteRenderer>().sortingOrder;

                Assert.Greater(nearOrder, farOrder,
                    "A flower lower on screen (smaller Y) must draw IN FRONT of one higher up, or the meadow " +
                    "reads flat and the player walks through blooms instead of past them.");
                foreach (int order in new[] { nearOrder, farOrder })
                    Assert.That(order, Is.InRange(2, 40),
                        "A flower's order left the decor safe band — it could sink behind the ground tiles or " +
                        "rise above the HUD.");
            }
            finally
            {
                Object.DestroyImmediate(near);
                Object.DestroyImmediate(far);
            }
        }

        [Test]
        public void Builder_IsRerunnable_RebuildingInPlaceWithoutDuplicating()
        {
            int first = DecorPrefabBuilder.BuildFlowerPrefabs(Scratch);
            int second = DecorPrefabBuilder.BuildFlowerPrefabs(Scratch);

            Assert.AreEqual(first, second, "Re-running the builder must rebuild in place, not accumulate.");
            int onDisk = AssetDatabase.FindAssets("t:Prefab", new[] { Scratch }).Length;
            Assert.AreEqual(first, onDisk, "Re-running left duplicate or orphaned prefabs behind.");
        }
    }
}
