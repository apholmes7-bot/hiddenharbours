using System.Collections.Generic;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for <see cref="FoodDef"/> (first food slice). The assets were hand-authored as
    /// YAML, so every check reads what Unity LOADS, never the file text.
    /// </summary>
    public class FoodContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private static readonly Regex FoodIdShape = new Regex("^food\\.[a-z0-9]+(_[a-z0-9]+)*$");

        private static List<T> LoadAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { DataRoot }))
            {
                var def = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) list.Add(def);
            }
            return list;
        }

        private static Dictionary<string, RecipeDef> RecipesByOutput()
        {
            var map = new Dictionary<string, RecipeDef>();
            foreach (RecipeDef r in LoadAll<RecipeDef>())
                if (!string.IsNullOrEmpty(r.OutputId)) map[r.OutputId] = r;
            return map;
        }

        [Test]
        public void Foods_HaveUniqueWellFormedIds_OneFoodPerItem_AndLiveUnderResources()
        {
            List<FoodDef> all = LoadAll<FoodDef>();
            Assert.IsNotEmpty(all, "the food slice ships FoodDefs under Data/Resources/Food");
            var ids = new HashSet<string>();
            var items = new HashSet<string>();
            foreach (FoodDef def in all)
            {
                string path = AssetDatabase.GetAssetPath(def);
                Assert.IsTrue(FoodIdShape.IsMatch(def.Id ?? ""), path + ": id must be food.snake_case, got " + def.Id);
                Assert.IsTrue(ids.Add(def.Id), path + ": duplicate id " + def.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.ItemId), path + ": names no item");
                Assert.IsTrue(items.Add(def.ItemId), path + ": " + def.ItemId + " is described by two foods");
                StringAssert.StartsWith("Assets/_Project/Data/Resources/" + FoodBook.ResourcesFolder + "/", path,
                    "the eater loads foods from Resources/" + FoodBook.ResourcesFolder + " only");
            }
            Assert.AreEqual(all.Count, FoodBook.FromResources().All.Count, "every asset reaches the shipped book");
        }

        [Test]
        public void Relief_IsInRange_EdibleRawFeeds_AndRawCatchDeclaresItsCookedForm()
        {
            foreach (FoodDef def in LoadAll<FoodDef>())
            {
                string path = AssetDatabase.GetAssetPath(def);
                Assert.That(def.HungerRelief, Is.InRange(0f, 1f), path + ": relief is a share of one meal");
                if (def.EdibleRaw)
                    Assert.Greater(def.HungerRelief, 0f, path + ": edible as it is, so eating it must feed you");
                else
                {
                    Assert.AreEqual(0f, def.HungerRelief, path + ": not edible raw, so its raw relief is 0");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(def.CookedId),
                        path + ": not edible raw and never cooked would be a food nobody can eat");
                }
            }
        }

        [Test]
        public void GalleyForms_ResolveToRecipeOutputs_PrepThenCooktop()
        {
            Dictionary<string, RecipeDef> byOutput = RecipesByOutput();
            foreach (FoodDef def in LoadAll<FoodDef>())
            {
                string path = AssetDatabase.GetAssetPath(def);
                RecipeDef prep = null;
                if (!string.IsNullOrEmpty(def.PreppedId))
                {
                    Assert.IsTrue(byOutput.TryGetValue(def.PreppedId, out prep), path + ": prepped form " + def.PreppedId + " is no recipe's output");
                    Assert.AreEqual(RecipeStations.Prep, prep.Station, path + ": the prepped form comes off the prep station");
                    if (prep.InputKind == RecipeInputKind.Species)
                        Assert.AreEqual(def.ItemId, prep.InputId, path + ": the prep recipe takes this item");
                }
                if (!string.IsNullOrEmpty(def.CookedId))
                {
                    Assert.IsTrue(byOutput.TryGetValue(def.CookedId, out RecipeDef cook), path + ": cooked form " + def.CookedId + " is no recipe's output");
                    Assert.AreEqual(RecipeStations.Cooktop, cook.Station, path + ": the cooked form comes off the cooktop");
                    Assert.Greater(cook.HungerRelief, 0f, path + ": a cooked dish feeds you");
                    if (prep != null)
                        Assert.IsTrue(RecipeBook.Feeds(prep, cook), path + ": prep then cooktop, one chain (owner ruling 2026-11-14)");
                }
            }
        }

        [Test]
        public void ItemIds_ResolveToRealItems_AndProduceAgreesWithItsOwnRawFigures()
        {
            var supplies = new HashSet<string>();
            foreach (SupplyDef s in LoadAll<SupplyDef>()) supplies.Add(s.Id);
            var fish = new HashSet<string>();
            foreach (FishSpeciesDef f in LoadAll<FishSpeciesDef>()) fish.Add(f.Id);
            var produce = new Dictionary<string, ProduceDef>();
            foreach (ProduceDef p in LoadAll<ProduceDef>()) produce[p.Id] = p;
            Dictionary<string, RecipeDef> outputs = RecipesByOutput();

            foreach (FoodDef def in LoadAll<FoodDef>())
            {
                string path = AssetDatabase.GetAssetPath(def);
                string item = def.ItemId ?? "";
                if (item.StartsWith("supply.")) Assert.IsTrue(supplies.Contains(item), path + ": no SupplyDef " + item);
                else if (item.StartsWith("fish.")) Assert.IsTrue(fish.Contains(item), path + ": no FishSpeciesDef " + item);
                else if (item.StartsWith("food.")) Assert.IsTrue(outputs.ContainsKey(item), path + ": no recipe makes " + item);
                else if (item.StartsWith("produce."))
                {
                    Assert.IsTrue(produce.TryGetValue(item, out ProduceDef p), path + ": no ProduceDef " + item);
                    Assert.AreEqual(p.EatenRaw, def.EdibleRaw, path + ": disagrees with " + item + " on eaten raw");
                    Assert.AreEqual(p.RawHungerRelief, def.HungerRelief, path + ": disagrees with " + item + " on raw relief");
                }
                else Assert.Fail(path + ": " + item + " is not a supply, fish, produce or food id");
            }
        }

        [Test]
        public void ShopFoods_AreOnACounter()
        {
            var listed = new Dictionary<string, SupplyDef>();
            foreach (SupplyDef s in CatalogSource.Supplies()) listed[s.Id] = s;
            int shopFoods = 0;
            foreach (FoodDef def in LoadAll<FoodDef>())
            {
                if (def.ItemId == null || !def.ItemId.StartsWith("supply.")) continue;
                shopFoods++;
                Assert.IsTrue(listed.TryGetValue(def.ItemId, out SupplyDef s), def.Id + ": its supply is not in the catalog");
                Assert.IsTrue(s.Catalog.Listed && !s.Catalog.IsOrphaned, def.Id + ": its supply is sold nowhere");
            }
            Assert.GreaterOrEqual(shopFoods, 2, "the slice ships shop staples");
        }

        [Test]
        public void FirstSet_HasStaples_RawProduce_AndARawCatchThatMustBeCooked()
        {
            var book = FoodBook.FromResources();
            int produce = 0, rawFish = 0;
            foreach (FoodDef def in book.All)
            {
                if (def.ItemId.StartsWith("produce.") && def.EdibleRaw) produce++;
                if (def.ItemId.StartsWith("fish.") && !def.EdibleRaw) rawFish++;
            }
            Assert.GreaterOrEqual(produce, 2, "two farm items eaten straight from the ground");
            Assert.GreaterOrEqual(rawFish, 1, "a catch that wants the galley");
            Assert.IsNotNull(book.FindByItem("supply.bread"), "bread is on the shelf");
        }
    }
}
