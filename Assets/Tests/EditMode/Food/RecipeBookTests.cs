using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The pure <see cref="RecipeBook"/> lookup over test-made Defs: species beats category, ties break by
    /// id, load order never matters, intermediates chain, and every bad input is refused without throwing.
    /// </summary>
    public class RecipeBookTests
    {
        private readonly List<RecipeDef> _made = new List<RecipeDef>();

        [TearDown]
        public void DestroyMade()
        {
            foreach (RecipeDef def in _made) Object.DestroyImmediate(def);
            _made.Clear();
        }

        private RecipeDef Make(string id, string station, RecipeInputKind kind, string inputId,
                               FishCategory category, string outputId, float relief = 0f)
        {
            var def = ScriptableObject.CreateInstance<RecipeDef>();
            def.Id = id;
            def.DisplayName = id;
            def.Station = station;
            def.InputKind = kind;
            def.InputId = inputId;
            def.InputCategory = category;
            def.OutputId = outputId;
            def.WorkSeconds = 10f;
            def.HungerRelief = relief;
            _made.Add(def);
            return def;
        }

        private static CatchItem Catch(string speciesId, FishCategory category)
            => new CatchItem(speciesId, speciesId, category, 1f, 10, 0.1f);

        private RecipeDef CodPrep() => Make("recipe.cod_fillet", RecipeStations.Prep, RecipeInputKind.Species,
            "fish.atlantic_cod", FishCategory.InshoreGroundfish, "food.cod_fillet");

        private RecipeDef GroundfishPrep() => Make("recipe.groundfish_fillet", RecipeStations.Prep,
            RecipeInputKind.Category, null, FishCategory.InshoreGroundfish, "food.white_fish_fillet");

        private RecipeDef CodCook() => Make("recipe.pan_fried_cod", RecipeStations.Cooktop,
            RecipeInputKind.Intermediate, "food.cod_fillet", default, "food.pan_fried_cod", 0.6f);

        [Test]
        public void Species_BeatsCategory_InEitherLoadOrder()
        {
            RecipeDef cod = CodPrep(), ground = GroundfishPrep();
            foreach (var order in new[] { new[] { cod, ground }, new[] { ground, cod } })
            {
                var book = new RecipeBook(order);
                Assert.IsTrue(book.TryFind(RecipeStations.Prep, Catch("fish.atlantic_cod", FishCategory.InshoreGroundfish), out RecipeDef r));
                Assert.AreSame(cod, r);
                Assert.IsTrue(book.TryFind(RecipeStations.Prep, Catch("fish.haddock", FishCategory.InshoreGroundfish), out r));
                Assert.AreSame(ground, r, "haddock has no species recipe, so the category one answers");
            }
        }

        [Test]
        public void EquallySpecific_LowestIdWins_RegardlessOfOrder()
        {
            RecipeDef b = Make("recipe.b_ground", RecipeStations.Prep, RecipeInputKind.Category, null,
                FishCategory.InshoreGroundfish, "food.b");
            RecipeDef a = Make("recipe.a_ground", RecipeStations.Prep, RecipeInputKind.Category, null,
                FishCategory.InshoreGroundfish, "food.a");
            foreach (var order in new[] { new[] { a, b }, new[] { b, a } })
            {
                Assert.IsTrue(new RecipeBook(order).TryFind(RecipeStations.Prep,
                    Catch("fish.pollock", FishCategory.InshoreGroundfish), out RecipeDef r));
                Assert.AreSame(a, r);
            }
        }

        [Test]
        public void Station_Matters_ARawCatchFindsNothingOnTheCooktop()
        {
            var book = new RecipeBook(new[] { CodPrep(), GroundfishPrep(), CodCook() });
            Assert.IsFalse(book.TryFind(RecipeStations.Cooktop, Catch("fish.atlantic_cod", FishCategory.InshoreGroundfish), out RecipeDef r));
            Assert.IsNull(r, "the ruling: raw food is prepped first, the cooktop takes no raw catch");
        }

        [Test]
        public void Intermediate_ChainsPrepIntoCooktop()
        {
            RecipeDef prep = CodPrep(), cook = CodCook();
            var book = new RecipeBook(new[] { cook, prep });
            Assert.IsTrue(book.TryFind(RecipeStations.Prep, Catch("fish.atlantic_cod", FishCategory.InshoreGroundfish), out RecipeDef first));
            Assert.IsTrue(book.TryFind(RecipeStations.Cooktop, first.OutputId, out RecipeDef second));
            Assert.AreSame(cook, second);
            Assert.IsTrue(RecipeBook.Feeds(prep, cook));
            Assert.IsFalse(RecipeBook.Feeds(cook, prep));
            CollectionAssert.AreEqual(new[] { prep }, book.FeedersOf(cook, RecipeStations.Prep));
            Assert.IsTrue(book.IsReachableFromCatch(cook));
            Assert.IsFalse(book.TryFind(RecipeStations.Prep, first.OutputId, out _), "the fillet is not re-prepped");
        }

        [Test]
        public void DanglingOrCyclicIntermediate_IsUnreachable()
        {
            RecipeDef orphan = Make("recipe.orphan", RecipeStations.Cooktop, RecipeInputKind.Intermediate,
                "food.nothing_makes_this", default, "food.orphan", 0.5f);
            RecipeDef x = Make("recipe.x", RecipeStations.Prep, RecipeInputKind.Intermediate, "food.y", default, "food.x");
            RecipeDef y = Make("recipe.y", RecipeStations.Prep, RecipeInputKind.Intermediate, "food.x", default, "food.y");
            var book = new RecipeBook(new[] { orphan, x, y });
            Assert.IsFalse(book.IsReachableFromCatch(orphan));
            Assert.IsFalse(book.IsReachableFromCatch(x));
            Assert.IsFalse(book.IsReachableFromCatch(null));
        }

        [Test]
        public void UnknownOrEmptyInput_IsRefusedSafely()
        {
            var book = new RecipeBook(new[] { CodPrep(), GroundfishPrep(), CodCook() });
            RecipeDef r;
            Assert.IsFalse(book.TryFind(RecipeStations.Prep, Catch("fish.lobster", FishCategory.Shellfish), out r)); Assert.IsNull(r);
            Assert.IsFalse(book.TryFind(RecipeStations.Prep, default(CatchItem), out r)); Assert.IsNull(r);
            Assert.IsFalse(book.TryFind(null, Catch("fish.atlantic_cod", FishCategory.InshoreGroundfish), out r)); Assert.IsNull(r);
            Assert.IsFalse(book.TryFind("station.unknown", Catch("fish.atlantic_cod", FishCategory.InshoreGroundfish), out r)); Assert.IsNull(r);
            Assert.IsFalse(book.TryFind(RecipeStations.Cooktop, "food.unknown", out r)); Assert.IsNull(r);
            Assert.IsFalse(book.TryFind(RecipeStations.Cooktop, (string)null, out r)); Assert.IsNull(r);
            Assert.IsFalse(book.TryFind("", "food.cod_fillet", out r)); Assert.IsNull(r);
            Assert.IsNull(book.Find("recipe.unknown"));
            Assert.IsNull(book.Find(null));
        }

        [Test]
        public void NullBlankAndDuplicateDefs_AreSkipped_FirstByIdOrderKept()
        {
            RecipeDef cod = CodPrep();
            RecipeDef dup = Make("recipe.cod_fillet", RecipeStations.Prep, RecipeInputKind.Species,
                "fish.atlantic_cod", FishCategory.InshoreGroundfish, "food.other");
            RecipeDef blank = Make("", RecipeStations.Prep, RecipeInputKind.Category, null, FishCategory.Pelagic, "food.blank");
            var book = new RecipeBook(new[] { null, cod, blank, dup });
            Assert.AreEqual(1, book.All.Count);
            Assert.AreSame(cod, book.Find("recipe.cod_fillet"), "the first of two equal ids given is kept");
            Assert.IsFalse(book.TryFind(RecipeStations.Prep, Catch("fish.mackerel", FishCategory.Pelagic), out _));
            Assert.AreEqual(0, new RecipeBook(null).All.Count);
        }
    }
}
