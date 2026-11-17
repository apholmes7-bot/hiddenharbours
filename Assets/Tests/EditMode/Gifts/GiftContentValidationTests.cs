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
    /// Content validation for the gift data (gifting slices 1 and 2). The config and the cast's tastes
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
            // Every shipped villager has tastes since gifting slice 2, so the "nothing authored" answer is
            // pinned on a fresh NpcDef: what a newly added villager does before anyone writes her tastes.
            NpcDef fresh = UnityEngine.ScriptableObject.CreateInstance<NpcDef>();
            try
            {
                Assert.IsTrue(fresh.Gifts == null || fresh.Gifts.IsEmpty);
                Assert.AreEqual(GiftReaction.Neutral, GiftRule.Preference(fresh.Gifts, Catch("fish.atlantic_cod", FishCategory.InshoreGroundfish)));
            }
            finally { UnityEngine.Object.DestroyImmediate(fresh); }
        }

        // ---- gifting slice 2: the whole cast has tastes and its own words -----------------------

        private static Dictionary<string, FishCategory> FishCategories()
        {
            var map = new Dictionary<string, FishCategory>(StringComparer.Ordinal);
            foreach (FishSpeciesDef fish in LoadAll<FishSpeciesDef>()) map[fish.Id] = fish.Category;
            return map;
        }

        private static List<NpcDef> People()
        {
            var people = new List<NpcDef>();
            foreach (NpcDef npc in LoadAll<NpcDef>())
                if (npc.Kind == InteractKind.Talk && !string.IsNullOrEmpty(npc.Id)) people.Add(npc);
            return people;
        }

        private static bool Any(string[] a) => a != null && a.Length > 0;

        [Test]
        public void EveryPersonInTheCast_HasSomethingTheyLove_AndSomethingTheyDislike()
        {
            List<NpcDef> people = People();
            Assert.GreaterOrEqual(people.Count, 27, "the shipped cast of people");
            foreach (NpcDef npc in people)
            {
                NpcGiftPreferences p = npc.Gifts;
                Assert.IsNotNull(p, npc.Id + ": no gift tastes");
                Assert.IsTrue(Any(p.LovedItemIds) || Any(p.LovedTags), npc.Id + ": loves nothing");
                Assert.IsTrue(Any(p.DislikedItemIds) || Any(p.DislikedTags), npc.Id + ": dislikes nothing");
            }
        }

        [Test]
        public void EveryPersonInTheCast_AnswersTheFourTastesInTheirOwnWords_AndNoTwoAlike()
        {
            GiftReaction[] tastes = { GiftReaction.Loved, GiftReaction.Liked, GiftReaction.Neutral, GiftReaction.Disliked };
            foreach (NpcDef npc in People())
            {
                GiftLineSet own = npc.Gifts != null ? npc.Gifts.Lines : null;
                Assert.IsNotNull(own, npc.Id + ": no lines");
                var said = new HashSet<string>(StringComparer.Ordinal);
                foreach (GiftReaction r in tastes)
                {
                    // No shared fallback here: each of the four must be the person's own.
                    string line = GiftRule.Line(r, own, null, 0, "Atlantic Cod");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(line), $"{npc.Id}: no own line for {r}");
                    Assert.IsTrue(said.Add(line), $"{npc.Id}: says the same thing for {r} as for another taste");
                }
            }
        }

        [Test]
        public void EveryPersonsListedFish_ResolveToTheirTaste_WithTheSpeciesRealCategory()
        {
            // Each listed id is walked through the rule with the category the species really ships with, so
            // a tag on the same person cannot quietly win (an id beats a tag, and nothing is listed twice).
            Dictionary<string, FishCategory> category = FishCategories();
            foreach (NpcDef npc in People())
            {
                NpcGiftPreferences p = npc.Gifts;
                if (p == null) continue;
                foreach (string id in p.LovedItemIds ?? Array.Empty<string>())
                    Assert.AreEqual(GiftReaction.Loved, GiftRule.Preference(p, Catch(id, category[id])), $"{npc.Id}: {id}");
                foreach (string id in p.LikedItemIds ?? Array.Empty<string>())
                    Assert.AreEqual(GiftReaction.Liked, GiftRule.Preference(p, Catch(id, category[id])), $"{npc.Id}: {id}");
                foreach (string id in p.DislikedItemIds ?? Array.Empty<string>())
                    Assert.AreEqual(GiftReaction.Disliked, GiftRule.Preference(p, Catch(id, category[id])), $"{npc.Id}: {id}");
            }
        }

        [Test]
        public void SliceTwoTastes_LoadAsAuthored()
        {
            Dictionary<string, FishCategory> category = FishCategories();
            GiftReaction Taste(string npc, string fish) => GiftRule.Preference(Npc(npc).Gifts, Catch(fish, category[fish]));

            // Rose takes anything gladly (the catch tag), but an id she dislikes beats it.
            Assert.AreEqual(GiftReaction.Loved, Taste("npc.rose_macisaac", "fish.lobster"));
            Assert.AreEqual(GiftReaction.Liked, Taste("npc.rose_macisaac", "fish.atlantic_cod"));
            Assert.AreEqual(GiftReaction.Disliked, Taste("npc.rose_macisaac", "fish.green_crab"));

            // Rene and Dougie share a gable and disagree about mackerel.
            Assert.AreEqual(GiftReaction.Loved, Taste("npc.rene_chiasson", "fish.mackerel"));
            Assert.AreEqual(GiftReaction.Disliked, Taste("npc.dougie_macneil", "fish.mackerel"));

            // The oyster man does not want oysters; the shopkeeper loves them.
            Assert.AreEqual(GiftReaction.Disliked, Taste("npc.rene_chiasson", "fish.american_oyster"));
            Assert.AreEqual(GiftReaction.Loved, Taste("npc.marguerite_leblanc", "fish.american_oyster"));

            // A person with no taste for a fish or its category takes it politely.
            Assert.AreEqual(GiftReaction.Neutral, Taste("npc.angus_gillis", "fish.mackerel"));
        }

        [Test]
        public void TheShippedDailyLimit_DeclinesTheSecondGiftToday_AndOpensAgainTomorrow()
        {
            List<GiftConfig> configs = LoadAll<GiftConfig>();
            Assert.AreEqual(1, configs.Count);
            int perDay = configs[0].GiftsPerNpcPerDay;
            Assert.Greater(perDay, 0);
            NpcDef ginny = Npc("npc.aunt_ginny");
            GiftItem cod = Catch("fish.atlantic_cod", FishCategory.InshoreGroundfish);
            var ledger = new GiftLedger();

            const int day = 12;
            for (int i = 0; i < perDay; i++)
            {
                Assert.AreEqual(GiftReaction.Loved, GiftRule.Evaluate(ginny.Gifts, in cod, ledger.GiftsToday(ginny.Id, day), perDay));
                ledger.Count(ginny.Id, day);
            }
            Assert.AreEqual(GiftReaction.AlreadyGiftedToday,
                GiftRule.Evaluate(ginny.Gifts, in cod, ledger.GiftsToday(ginny.Id, day), perDay));

            NpcDef basil = Npc("npc.basil_samson");
            GiftItem haddock = Catch("fish.haddock", FishCategory.InshoreGroundfish);
            Assert.AreEqual(GiftReaction.Loved,
                GiftRule.Evaluate(basil.Gifts, in haddock, ledger.GiftsToday(basil.Id, day), perDay),
                "the limit is per person");

            Assert.AreEqual(GiftReaction.Loved,
                GiftRule.Evaluate(ginny.Gifts, in cod, ledger.GiftsToday(ginny.Id, day + 1), perDay),
                "the next game day she takes one again");

            // Her own declined-today words are said, not the shared ones.
            string line = GiftRule.Line(GiftReaction.AlreadyGiftedToday, ginny.Gifts.Lines, configs[0].Lines, day, "Atlantic Cod");
            StringAssert.Contains("One is plenty for one day", line);
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
