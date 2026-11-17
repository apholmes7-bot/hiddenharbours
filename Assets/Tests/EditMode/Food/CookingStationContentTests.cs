using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Kitchens and microwaves slice, the shipped content: one station per kind, ids well formed, every
    /// accepted recipe station has a recipe, the microwave is microwavable-only and faster than the stove,
    /// raw food is never microwavable, the shipped books run cod from the hand to a dish in the locker, and
    /// the three placeable prefabs carry their station Def. The assets were hand-authored as YAML, so every
    /// check reads what Unity LOADS.
    /// </summary>
    public class CookingStationContentTests
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs/Props/Kitchen/";
        private static readonly Regex IdShape = new Regex("^cooking_station\\.[a-z0-9]+(_[a-z0-9]+)*$");
        private static readonly Regex StationIdShape = new Regex("^station\\.[a-z0-9]+(_[a-z0-9]+)*$");

        private static CookingStationDef[] Stations() => Resources.LoadAll<CookingStationDef>(CookingStationDef.ResourcesFolder);

        private static CookingStationDef Only(CookingStationKind kind)
        {
            CookingStationDef[] all = Stations().Where(s => s.Kind == kind).ToArray();
            Assert.AreEqual(1, all.Length, $"exactly one shipped {kind} station");
            return all[0];
        }

        [Test]
        public void OneStationPerKind_WithWellFormedIds_WordsAndAcceptedStations()
        {
            foreach (CookingStationKind kind in System.Enum.GetValues(typeof(CookingStationKind))) Only(kind);
            RecipeBook recipes = RecipeBook.FromResources();
            var ids = new HashSet<string>();
            foreach (CookingStationDef s in Stations())
            {
                string path = AssetDatabase.GetAssetPath(s);
                Assert.IsTrue(IdShape.IsMatch(s.Id ?? ""), path + ": id must be cooking_station.snake_case, got " + s.Id);
                Assert.IsTrue(ids.Add(s.Id), path + ": duplicate id " + s.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(s.DisplayName), path + ": no display name");
                Assert.IsFalse(string.IsNullOrWhiteSpace(s.VerbLabel), path + ": the popup has nothing to offer");
                Assert.IsFalse(string.IsNullOrWhiteSpace(s.StartedLine), path + ": no started line");
                Assert.IsFalse(string.IsNullOrWhiteSpace(s.ReadyLine), path + ": no ready line");
                Assert.IsFalse(string.IsNullOrWhiteSpace(s.NothingLine), path + ": no nothing-to-cook line");
                Assert.IsFalse(string.IsNullOrWhiteSpace(s.BusyLine), path + ": no busy line");
                Assert.Greater(s.CookTimeMultiplier, 0f, path);
                Assert.Greater(s.ReachMeters, 0f, path);
                Assert.IsNotEmpty(s.AcceptedStations, path + ": runs nothing");
                foreach (string st in s.AcceptedStations)
                {
                    Assert.IsTrue(StationIdShape.IsMatch(st ?? ""), path + ": accepted station must be station.snake_case, got " + st);
                    Assert.IsTrue(recipes.All.Any(r => r.Station == st), path + ": no recipe runs at " + st);
                }
            }
        }

        [Test]
        public void Microwave_IsMicrowavableOnly_FasterThanTheStove_AndNeverPrepsOrFries()
        {
            CookingStationDef microwave = Only(CookingStationKind.Microwave);
            CookingStationDef stove = Only(CookingStationKind.Stove);
            CookingStationDef prep = Only(CookingStationKind.Prep);

            Assert.IsTrue(microwave.MicrowavableOnly, "owner 2026-11-15: only cooked or microwavable items");
            Assert.IsFalse(string.IsNullOrWhiteSpace(microwave.NotMicrowavableLine));
            Assert.Less(microwave.CookTimeMultiplier, stove.CookTimeMultiplier, "a microwave is fast");
            Assert.IsFalse(microwave.Accepts(RecipeStations.Prep));
            Assert.IsFalse(microwave.Accepts(RecipeStations.Cooktop));
            Assert.IsTrue(stove.Accepts(RecipeStations.Cooktop));
            Assert.IsTrue(prep.Accepts(RecipeStations.Prep));
            Assert.IsFalse(stove.MicrowavableOnly);
            Assert.IsFalse(prep.MicrowavableOnly);
        }

        [Test]
        public void RawFood_IsNeverMicrowavable_AndEveryMicrowaveRecipeTakesMicrowavableFood()
        {
            FoodBook foods = FoodBook.FromResources();
            foreach (FoodDef f in foods.All)
            {
                if (!f.EdibleRaw) Assert.IsFalse(f.Microwavable, f.Id + ": raw food can't be microwaved");
                if (f.ItemId.StartsWith("fish.")) Assert.IsFalse(f.Microwavable, f.Id + ": a raw fish can't be microwaved");
            }

            int microwaveRecipes = 0;
            foreach (RecipeDef r in RecipeBook.FromResources().All)
            {
                if (r.Station != RecipeStations.Microwave) continue;
                microwaveRecipes++;
                FoodDef input = foods.FindByItem(r.InputId);
                Assert.IsNotNull(input, r.Id + ": its input " + r.InputId + " is no food");
                Assert.IsTrue(input.Microwavable, r.Id + ": its input must be microwavable");
                FoodDef output = foods.FindByItem(r.OutputId);
                Assert.IsNotNull(output, r.Id + ": its output " + r.OutputId + " has no FoodDef, so it cannot be eaten");
            }
            Assert.Greater(microwaveRecipes, 0, "the slice ships something to microwave");
        }

        [Test]
        public void EveryCooktopDish_IsAFoodTheEatPathCanEat_WithTheRecipesRelief()
        {
            FoodBook foods = FoodBook.FromResources();
            foreach (RecipeDef r in RecipeBook.FromResources().All)
            {
                if (r.Station != RecipeStations.Cooktop && r.Station != RecipeStations.Microwave) continue;
                FoodDef dish = foods.FindByItem(r.OutputId);
                Assert.IsNotNull(dish, r.Id + ": " + r.OutputId + " has no FoodDef");
                Assert.IsTrue(dish.EdibleRaw, r.Id + ": a cooked dish is eaten as it is");
                Assert.AreEqual(r.HungerRelief, dish.HungerRelief, 1e-5f, r.Id + ": recipe and food disagree on relief");
            }
        }

        [Test]
        public void ShippedBooks_RunCodFromTheHand_ToADishInTheLocker_AndTheMicrowaveRefusesRawCod()
        {
            var save = new TraitFixtures.MemorySave();
            var hands = new OneHand { Held = new CatchItem("fish.atlantic_cod", "Cod", FishCategory.InshoreGroundfish, 2f, 10, 0.1f) };
            double now = 0d;
            var runner = new RecipeRunner(RecipeBook.FromResources, FoodBook.FromResources, () => save, () => hands, () => now);

            Assert.AreEqual(CookRefusal.NotMicrowavable, runner.TryStart(Only(CookingStationKind.Microwave), "k.microwave"));
            Assert.IsTrue(hands.Held.HasValue, "the raw cod stays in the hand");

            Assert.AreEqual(CookRefusal.None, runner.TryStart(Only(CookingStationKind.Prep), "k.prep"));
            now += 10000d;
            Assert.IsTrue(runner.TryFinish("k.prep", out _));
            Assert.AreEqual(1, SupplyLocker.Count(save.Current, "food.cod_fillet"));

            Assert.AreEqual(CookRefusal.None, runner.TryStart(Only(CookingStationKind.Stove), "k.stove"));
            now += 10000d;
            Assert.IsTrue(runner.TryFinish("k.stove", out _));
            Assert.AreEqual(1, SupplyLocker.Count(save.Current, "food.pan_fried_cod"));
            Assert.AreEqual(EatRefusal.None, new FoodEater(FoodBook.FromResources).Check("food.pan_fried_cod", out _),
                "the eat path takes the dish");
        }

        [Test]
        public void Prefabs_CarryTheirStationDef_AndUniqueIds()
        {
            var ids = new HashSet<string>();
            foreach ((string file, CookingStationKind kind) in new[]
                     {
                         ("PrepCounter", CookingStationKind.Prep),
                         ("Stove", CookingStationKind.Stove),
                         ("Microwave", CookingStationKind.Microwave),
                     })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + file + ".prefab");
                Assert.IsNotNull(prefab, file + " prefab missing");
                var fixture = prefab.GetComponent<CookingStationFixture>();
                Assert.IsNotNull(fixture, file + ": no CookingStationFixture");
                Assert.IsNotNull(fixture.Def, file + ": no station Def");
                Assert.AreEqual(kind, fixture.Def.Kind, file);
                Assert.IsTrue(ids.Add(fixture.Id), file + ": duplicate interactable id " + fixture.Id);
                Assert.AreEqual(fixture.Def.VerbLabel, fixture.VerbLabel);
            }
        }

        private sealed class OneHand : ICatchHands
        {
            public CatchItem? Held;
            public bool TryPutInHand(in CatchItem item) { if (Held.HasValue) return false; Held = item; return true; }
            public bool TryTakeFromHand(out CatchItem item) { item = Held ?? default; if (!Held.HasValue) return false; Held = null; return true; }
            public bool TryPeekHand(out CatchItem item) { item = Held ?? default; return Held.HasValue; }
        }
    }
}
