using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public class SpeciesCatchItemFactoryTests
    {
        private readonly List<Object> _made = new();

        [SetUp]
        public void SetUp() { GameServices.Reset(); FishSpeciesRegistry.Reset(); }

        [TearDown]
        public void TearDown()
        {
            FishSpeciesRegistry.Reset();
            GameServices.Reset();
            foreach (Object item in _made) Object.DestroyImmediate(item);
            _made.Clear();
        }

        private FishSpeciesDef Fish(string id)
        {
            var fish = ScriptableObject.CreateInstance<FishSpeciesDef>();
            _made.Add(fish);
            fish.Id = id;
            fish.DisplayName = "Fixture catch";
            fish.Category = FishCategory.Shellfish;
            fish.MinWeightKg = 2f; fish.MaxWeightKg = 6f;
            fish.BaseValue = 23;
            fish.SupplyElasticity = 0.375f;
            fish.SpoilPerDay = 0.625f;
            return fish;
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("fish.missing")]
        public void UnknownSpecies_RefusesAndClearsTheOutput(string id)
        {
            var factory = new SpeciesCatchItemFactory();
            Assert.IsFalse(factory.TryCreate(id, out CatchItem item));
            Assert.AreEqual(default(CatchItem), item);
        }

        [Test]
        public void Restore_UsesRegisteredMetadataAndMidpointWeight_WithDefaultFreshness()
        {
            var fish = Fish("fish.fixture");
            FishSpeciesRegistry.Register(fish);
            Assert.IsTrue(new SpeciesCatchItemFactory().TryCreate("FISH.FIXTURE", out CatchItem item));
            Assert.AreEqual(fish.Id, item.SpeciesId);
            Assert.AreEqual(fish.DisplayName, item.DisplayName);
            Assert.AreEqual(fish.Category, item.Category);
            Assert.AreEqual(4f, item.WeightKg);
            Assert.AreEqual(fish.BaseValue, item.BaseValue);
            Assert.AreEqual(fish.SupplyElasticity, item.SupplyElasticity);
            Assert.AreEqual(fish.SpoilPerDay, item.SpoilPerDay);
            Assert.AreEqual(default(CatchItem).Freshness, item.Freshness);
        }

        [Test]
        public void Registry_FirstRegistrationWins_AndResolvePreservesOrderAndDuplicates()
        {
            var first = Fish("fish.first");
            var duplicate = Fish("FISH.FIRST");
            var second = Fish("fish.second");
            FishSpeciesRegistry.Register(first);
            FishSpeciesRegistry.Register(duplicate);
            FishSpeciesRegistry.Register(second);
            Assert.AreEqual(2, FishSpeciesRegistry.Count);
            Assert.AreSame(first, FishSpeciesRegistry.Get("FISH.FIRST"));
            CollectionAssert.AreEqual(new[] { second, first, first }, FishSpeciesRegistry.Resolve(
                new[] { "fish.second", null, "fish.missing", "FISH.FIRST", "fish.first" }));
            Assert.IsEmpty(FishSpeciesRegistry.Resolve(null));
        }

        [Test]
        public void Registry_InvalidEntriesDoNotPublishFacts_AndResetRemovesLookups()
        {
            FishSpeciesRegistry.Register(null);
            FishSpeciesRegistry.Register(Fish(" "));
            Assert.AreEqual(0, FishSpeciesRegistry.Count);
            Assert.AreSame(EmptyFishBehaviourFacts.Instance, GameServices.FishBehaviour);
            var fish = Fish("fish.fixture");
            fish.BehaviorFlags = FishFlags.Jumps;
            fish.FightJumpPeriodSeconds = 3.5f;
            FishSpeciesRegistry.Register(fish);
            Assert.AreSame(FishSpeciesRegistry.Facts, GameServices.FishBehaviour);
            Assert.IsTrue(FishSpeciesRegistry.Has("FISH.FIXTURE"));
            Assert.IsTrue(FishSpeciesRegistry.Facts.Jumps(fish.Id));
            Assert.AreEqual(3.5f, FishSpeciesRegistry.Facts.FightJumpPeriodSeconds(fish.Id));
            FishSpeciesRegistry.Reset();
            Assert.AreEqual(0, FishSpeciesRegistry.Count);
            Assert.IsFalse(FishSpeciesRegistry.Has(fish.Id));
            Assert.IsFalse(FishSpeciesRegistry.Facts.Jumps(fish.Id));
            Assert.AreEqual(0f, FishSpeciesRegistry.Facts.FightJumpPeriodSeconds(fish.Id));
        }

        [Test]
        public void Library_RegisterAll_SkipsNullEntriesAndKeepsTheFirstDuplicate()
        {
            var library = ScriptableObject.CreateInstance<FishSpeciesLibrary>();
            _made.Add(library);
            var first = Fish("fish.fixture");
            library.Species = new[] { null, first, Fish("FISH.FIXTURE") };
            library.RegisterAll();
            library.RegisterAll();
            Assert.AreEqual(1, FishSpeciesRegistry.Count);
            Assert.AreSame(first, FishSpeciesRegistry.Get("fish.fixture"));
        }

        [Test]
        public void Library_MissingArray_DoesNotClearAnExistingRegistry()
        {
            var library = ScriptableObject.CreateInstance<FishSpeciesLibrary>();
            _made.Add(library);
            var existing = Fish("fish.fixture");
            FishSpeciesRegistry.Register(existing);
            library.Species = null;
            library.RegisterAll();
            Assert.AreEqual(1, FishSpeciesRegistry.Count);
            Assert.AreSame(existing, FishSpeciesRegistry.Get(existing.Id));
        }
    }
}
