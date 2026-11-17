using System.Collections.Generic;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for <see cref="RecipeDef"/> (two-step cooking, owner ruling 2026-11-14: "Food
    /// needs to be prepped at a prep station first, cut/prepared then cooked on a cooktop"). The assets were
    /// hand-authored as YAML, so every check reads what Unity LOADS, never the file text.
    /// </summary>
    public class RecipeContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private static readonly Regex RecipeIdShape = new Regex("^recipe\\.[a-z0-9]+(_[a-z0-9]+)*$");
        private static readonly Regex StationIdShape = new Regex("^station\\.[a-z0-9]+(_[a-z0-9]+)*$");
        private static readonly Regex OutputIdShape = new Regex("^food\\.[a-z0-9]+(_[a-z0-9]+)*$");

        private static List<RecipeDef> LoadAll()
        {
            var list = new List<RecipeDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(RecipeDef), new[] { DataRoot }))
            {
                var def = AssetDatabase.LoadAssetAtPath<RecipeDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) list.Add(def);
            }
            return list;
        }

        private static HashSet<string> FishIds()
        {
            var ids = new HashSet<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(FishSpeciesDef), new[] { DataRoot }))
            {
                var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) ids.Add(def.Id);
            }
            return ids;
        }

        [Test]
        public void Recipes_HaveUniqueWellFormedIds_AndLiveUnderResources()
        {
            List<RecipeDef> all = LoadAll();
            Assert.IsNotEmpty(all, "the cooking slice ships RecipeDefs under Data/Resources/Recipes");
            var seen = new HashSet<string>();
            var outputs = new HashSet<string>();
            foreach (RecipeDef def in all)
            {
                string path = AssetDatabase.GetAssetPath(def);
                Assert.IsTrue(RecipeIdShape.IsMatch(def.Id ?? ""), path + ": id must be recipe.snake_case, got " + def.Id);
                Assert.IsTrue(seen.Add(def.Id), path + ": duplicate id " + def.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.DisplayName), path + ": no display name");
                Assert.IsTrue(StationIdShape.IsMatch(def.Station ?? ""), path + ": station must be station.snake_case, got " + def.Station);
                Assert.IsTrue(OutputIdShape.IsMatch(def.OutputId ?? ""), path + ": output must be food.snake_case, got " + def.OutputId);
                Assert.IsTrue(outputs.Add(def.OutputId), path + ": output " + def.OutputId + " is made by two recipes");
                Assert.IsTrue(System.Enum.IsDefined(typeof(RecipeInputKind), def.InputKind), path + ": unknown input kind");
                Assert.Greater(def.WorkSeconds, 0f, path + ": work takes time");
                StringAssert.StartsWith("Assets/_Project/Data/Resources/" + RecipeBook.ResourcesFolder + "/", path,
                    "the book loads recipes from Resources/" + RecipeBook.ResourcesFolder + " only");
            }
        }

        [Test]
        public void PrepTakesRawCatch_CooktopTakesAPrepOutput()
        {
            HashSet<string> fish = FishIds();
            foreach (RecipeDef def in LoadAll())
            {
                string path = AssetDatabase.GetAssetPath(def);
                if (def.Station == RecipeStations.Prep)
                {
                    Assert.AreNotEqual(RecipeInputKind.Intermediate, def.InputKind, path + ": prep takes a raw catch");
                    if (def.InputKind == RecipeInputKind.Species)
                        Assert.IsTrue(fish.Contains(def.InputId ?? ""), path + ": no fish species " + def.InputId);
                    else
                        Assert.IsTrue(System.Enum.IsDefined(typeof(FishCategory), def.InputCategory), path + ": unknown category");
                }
                else if (def.Station == RecipeStations.Cooktop)
                {
                    Assert.AreEqual(RecipeInputKind.Intermediate, def.InputKind,
                        path + ": the cooktop takes prepped food only (owner ruling 2026-11-14)");
                }
                else if (def.Station == RecipeStations.Microwave)
                {
                    // Microwave recipes take an item by id (a tin, a cooked dish), never a raw catch; that the
                    // item is microwavable is CookingStationContentTests' check.
                    Assert.AreEqual(RecipeInputKind.Intermediate, def.InputKind,
                        path + ": the microwave never takes a raw catch (owner 2026-11-15)");
                }
            }
        }

        [Test]
        public void EveryCooktopRecipe_IsFedByAPrepRecipe()
        {
            List<RecipeDef> all = LoadAll();
            var book = new RecipeBook(all);
            int cooktop = 0;
            foreach (RecipeDef def in all)
            {
                if (def.Station != RecipeStations.Cooktop) continue;
                cooktop++;
                string path = AssetDatabase.GetAssetPath(def);
                Assert.IsNotEmpty(book.FeedersOf(def, RecipeStations.Prep), path + ": no prep recipe makes " + def.InputId);
                Assert.IsTrue(book.IsReachableFromCatch(def), path + ": not reachable from a raw catch");
            }
            Assert.Greater(cooktop, 0, "the slice ships cooktop recipes");
        }

        [Test]
        public void HungerRelief_OnlyOnCookedOutputs_AndWithinOneMeal()
        {
            foreach (RecipeDef def in LoadAll())
            {
                string path = AssetDatabase.GetAssetPath(def);
                Assert.That(def.HungerRelief, Is.InRange(0f, 1f), path + ": relief is a share of one meal");
                if (def.Station == RecipeStations.Cooktop || def.Station == RecipeStations.Microwave)
                    Assert.Greater(def.HungerRelief, 0f, path + ": a cooked or heated dish feeds you");
                else
                    Assert.AreEqual(0f, def.HungerRelief, path + ": only cooked outputs feed you");
            }
        }

        [Test]
        public void TheShippedRecipes_LoadWithTheirStartingNumbers()
        {
            var byId = new Dictionary<string, RecipeDef>();
            foreach (RecipeDef def in LoadAll()) byId[def.Id] = def;

            AssertOne(byId, "recipe.cod_fillet", RecipeStations.Prep, RecipeInputKind.Species, "fish.atlantic_cod", "food.cod_fillet", 20f, 0f);
            AssertOne(byId, "recipe.groundfish_fillet", RecipeStations.Prep, RecipeInputKind.Category, null, "food.white_fish_fillet", 25f, 0f);
            AssertOne(byId, "recipe.mackerel_cleaned", RecipeStations.Prep, RecipeInputKind.Species, "fish.mackerel", "food.mackerel_cleaned", 15f, 0f);
            AssertOne(byId, "recipe.pan_fried_cod", RecipeStations.Cooktop, RecipeInputKind.Intermediate, "food.cod_fillet", "food.pan_fried_cod", 40f, 0.6f);
            AssertOne(byId, "recipe.pan_fried_fish", RecipeStations.Cooktop, RecipeInputKind.Intermediate, "food.white_fish_fillet", "food.pan_fried_fish", 40f, 0.5f);
            AssertOne(byId, "recipe.grilled_mackerel", RecipeStations.Cooktop, RecipeInputKind.Intermediate, "food.mackerel_cleaned", "food.grilled_mackerel", 35f, 0.45f);
            AssertOne(byId, "recipe.heated_beans", RecipeStations.Microwave, RecipeInputKind.Intermediate, "supply.tinned_beans", "food.hot_beans", 120f, 0.4f);
            Assert.AreEqual(FishCategory.InshoreGroundfish, byId["recipe.groundfish_fillet"].InputCategory);
        }

        [Test]
        public void ShippedBook_CodPrefersItsOwnFillet_HaddockFallsBackToGroundfish()
        {
            RecipeBook book = RecipeBook.FromResources();
            Assert.IsTrue(book.TryFind(RecipeStations.Prep,
                new CatchItem("fish.atlantic_cod", "Cod", FishCategory.InshoreGroundfish, 2f, 10, 0.1f), out RecipeDef cod));
            Assert.AreEqual("recipe.cod_fillet", cod.Id);
            Assert.IsTrue(book.TryFind(RecipeStations.Prep,
                new CatchItem("fish.haddock", "Haddock", FishCategory.InshoreGroundfish, 1.5f, 10, 0.1f), out RecipeDef haddock));
            Assert.AreEqual("recipe.groundfish_fillet", haddock.Id);
            Assert.IsTrue(book.TryFind(RecipeStations.Cooktop, haddock.OutputId, out RecipeDef dish));
            Assert.AreEqual("recipe.pan_fried_fish", dish.Id);
        }

        private static void AssertOne(Dictionary<string, RecipeDef> byId, string id, string station, RecipeInputKind kind,
                                      string inputId, string outputId, float seconds, float relief)
        {
            Assert.IsTrue(byId.TryGetValue(id, out RecipeDef def), "missing " + id);
            Assert.AreEqual(station, def.Station, id);
            Assert.AreEqual(kind, def.InputKind, id);
            if (inputId != null) Assert.AreEqual(inputId, def.InputId, id);
            Assert.AreEqual(outputId, def.OutputId, id);
            Assert.AreEqual(seconds, def.WorkSeconds, 1e-4f, id);
            Assert.AreEqual(relief, def.HungerRelief, 1e-4f, id);
        }
    }
}
