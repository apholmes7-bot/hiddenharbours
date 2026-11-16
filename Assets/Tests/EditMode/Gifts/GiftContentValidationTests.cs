using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for the gift data (gifting slice 1). The config and the three villagers' tastes
    /// were hand-authored as YAML, so every check reads what Unity LOADS: a bad indent fails silently to the
    /// field default, and only the loaded object tells.
    /// </summary>
    public class GiftContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string ResourcesRoot = "Assets/_Project/Data/Resources/";

        private static List<T> LoadAll<T>() where T : UnityEngine.Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { DataRoot }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        private static readonly GiftReaction[] EveryReaction =
        {
            GiftReaction.Loved, GiftReaction.Liked, GiftReaction.Neutral, GiftReaction.Disliked,
            GiftReaction.NotGiftable, GiftReaction.AlreadyGiftedToday,
        };

        [Test]
        public void TheConfig_ShipsOnce_AtItsResourcesPath_WithALimitAndALineForEveryReaction()
        {
            List<GiftConfig> all = LoadAll<GiftConfig>();
            Assert.AreEqual(1, all.Count, "exactly one GiftConfig ships");
            Assert.AreEqual(ResourcesRoot + GiftConfig.ResourcesPath + ".asset", AssetDatabase.GetAssetPath(all[0]));

            GiftConfig config = all[0];
            Assert.AreEqual(1, config.GiftsPerNpcPerDay, "the owner's lean: one gift per person per day");
            Assert.IsNotNull(config.Lines);
            foreach (GiftReaction r in EveryReaction)
                Assert.IsNotNull(GiftRule.PickLine(config.Lines.For(r), 0), "no shared line for " + r);
        }

        [Test]
        public void EveryVillagersTastes_NameRealFish_AndRealTags_AndNothingTwice()
        {
            var fishIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (FishSpeciesDef fish in LoadAll<FishSpeciesDef>()) fishIds.Add(fish.Id);
            Assert.IsNotEmpty(fishIds);

            var tags = new HashSet<string>(StringComparer.Ordinal) { GiftItem.CatchTag };
            foreach (FishCategory c in Enum.GetValues(typeof(FishCategory))) tags.Add(GiftItem.CategoryTag(c));

            foreach (NpcDef npc in LoadAll<NpcDef>())
            {
                NpcGiftPreferences p = npc.Gifts;
                if (p == null) continue;

                var seenIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (string[] list in new[] { p.LovedItemIds, p.LikedItemIds, p.DislikedItemIds })
                    foreach (string id in list ?? Array.Empty<string>())
                    {
                        Assert.IsTrue(fishIds.Contains(id), $"{npc.Id}: '{id}' is not a shipped fish id");
                        Assert.IsTrue(seenIds.Add(id), $"{npc.Id}: '{id}' is listed twice");
                    }

                var seenTags = new HashSet<string>(StringComparer.Ordinal);
                foreach (string[] list in new[] { p.LovedTags, p.LikedTags, p.DislikedTags })
                    foreach (string tag in list ?? Array.Empty<string>())
                    {
                        Assert.IsTrue(tags.Contains(tag), $"{npc.Id}: '{tag}' is not an item tag");
                        Assert.IsTrue(seenTags.Add(tag), $"{npc.Id}: tag '{tag}' is listed twice");
                    }
            }
        }

        private static NpcDef Npc(string id)
        {
            foreach (NpcDef npc in LoadAll<NpcDef>()) if (npc.Id == id) return npc;
            Assert.Fail("no NpcDef " + id);
            return null;
        }

        private static GiftItem Catch(string id, FishCategory category)
            => GiftItem.FromCatch(new CatchItem(id, id, category, 1f, 1, 0f), null);

        [Test]
        public void TheThreeAuthoredVillagers_LoadWithTheirTastes()
        {
            NpcDef ginny = Npc("npc.aunt_ginny");
            Assert.AreEqual(GiftReaction.Loved, GiftRule.Preference(ginny.Gifts, Catch("fish.atlantic_cod", FishCategory.InshoreGroundfish)));
            Assert.AreEqual(GiftReaction.Liked, GiftRule.Preference(ginny.Gifts, Catch("fish.blue_mussel", FishCategory.Shellfish)));
            Assert.AreEqual(GiftReaction.Disliked, GiftRule.Preference(ginny.Gifts, Catch("fish.green_crab", FishCategory.Shellfish)),
                "an id beats her shellfish tag");

            NpcDef junior = Npc("npc.junior_poirier");
            Assert.AreEqual(GiftReaction.Loved, GiftRule.Preference(junior.Gifts, Catch("fish.striped_bass", FishCategory.Estuary)));
            Assert.AreEqual(GiftReaction.Disliked, GiftRule.Preference(junior.Gifts, Catch("fish.soft_shell_clam", FishCategory.Shellfish)));

            NpcDef basil = Npc("npc.basil_samson");
            Assert.AreEqual(GiftReaction.Loved, GiftRule.Preference(basil.Gifts, Catch("fish.haddock", FishCategory.InshoreGroundfish)));
            Assert.AreEqual(GiftReaction.Liked, GiftRule.Preference(basil.Gifts, Catch("fish.pollock", FishCategory.InshoreGroundfish)));
            Assert.AreEqual(GiftReaction.Disliked, GiftRule.Preference(basil.Gifts, Catch("fish.cunner", FishCategory.InshoreGroundfish)));

            Assert.IsNotNull(GiftRule.PickLine(ginny.Gifts.Lines.For(GiftReaction.Loved), 0), "her own words loaded");
        }

        [Test]
        public void AVillagerWithNoTastes_TakesAGiftPolitely()
        {
            NpcDef rose = Npc("npc.rose_macisaac");
            Assert.AreEqual(GiftReaction.Neutral, GiftRule.Preference(rose.Gifts, Catch("fish.atlantic_cod", FishCategory.InshoreGroundfish)));
        }

        [Test]
        public void TheGiftRows_AreOrderedWarmToCool()
        {
            List<RelationshipConfig> all = LoadAll<RelationshipConfig>();
            Assert.AreEqual(1, all.Count);
            var byKind = new Dictionary<RelationshipEventKind, RelationshipRule>();
            foreach (RelationshipRule rule in all[0].Rules) byKind[rule.Kind] = rule;

            int loved = byKind[RelationshipEventKind.GiftLoved].PointsPerUnit;
            int liked = byKind[RelationshipEventKind.GiftLiked].PointsPerUnit;
            int neutral = byKind[RelationshipEventKind.GiftNeutral].PointsPerUnit;
            int disliked = byKind[RelationshipEventKind.GiftDisliked].PointsPerUnit;
            Assert.Greater(loved, liked);
            Assert.Greater(liked, neutral);
            Assert.Greater(neutral, 0);
            Assert.Less(disliked, 0, "a disliked gift cools");
            Assert.IsFalse(byKind[RelationshipEventKind.GiftDisliked].CanMakeRival, "a bad gift never makes a rival");
        }
    }
}
