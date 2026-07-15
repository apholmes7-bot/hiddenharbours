using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for the POT ECONOMY data (rule 2 — content is data): every authored
    /// <see cref="PotOffer"/> has a stable, unique id, a positive price, and points at a REAL authored
    /// <see cref="TrapDef"/> by stable id (a dangling TrapDefId would sell stock no gate can spend);
    /// the committed shipwright offers exist with the ratified ids; and the GameConfig starter kit's
    /// entries resolve to authored traps too (a typo there would grant phantom pots).
    /// </summary>
    public class PotOfferContentValidationTests
    {
        private static List<T> LoadAll<T>(string filter) where T : ScriptableObject
        {
            return AssetDatabase.FindAssets(filter, new[] { "Assets/_Project/Data" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null)
                .ToList();
        }

        private static List<PotOffer> AllOffers() => LoadAll<PotOffer>("t:PotOffer");
        private static List<TrapDef> AllTraps() => LoadAll<TrapDef>("t:TrapDef");

        [Test]
        public void CommittedPotOffers_ExistWithTheRatifiedIds()
        {
            var ids = AllOffers().Select(o => o.Id).ToList();
            CollectionAssert.Contains(ids, "offer.lobster_pot", "the lobster pot is on sale at the shipwright");
            CollectionAssert.Contains(ids, "offer.crab_pot", "the crab pot is on sale at the shipwright");
        }

        [Test]
        public void PotOffers_HaveUniqueNonEmptyIds_AndDisplayNames()
        {
            var offers = AllOffers();
            Assert.IsNotEmpty(offers, "at least the two committed pot offers load");
            foreach (var o in offers)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(o.Id), $"{o.name}: offer id must be set");
                Assert.IsTrue(o.Id.StartsWith("offer."), $"{o.name}: offer ids follow type.snake_case ('{o.Id}')");
                Assert.IsFalse(string.IsNullOrWhiteSpace(o.DisplayName), $"{o.name}: display name must be set");
            }
            var dupes = offers.GroupBy(o => o.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.IsEmpty(dupes, "pot offer ids must be unique: " + string.Join(", ", dupes));
        }

        [Test]
        public void PotOffers_PointAtRealAuthoredTraps_WithPositivePrices()
        {
            var trapIds = AllTraps().Select(t => t.Id).ToHashSet();
            foreach (var o in AllOffers())
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(o.TrapDefId),
                    $"{o.name}: TrapDefId must be set (it keys the owned stock)");
                Assert.IsTrue(trapIds.Contains(o.TrapDefId),
                    $"{o.name}: TrapDefId '{o.TrapDefId}' must name an authored TrapDef " +
                    "(else the shop sells stock no gate can ever spend)");
                Assert.Greater(o.Price, 0, $"{o.name}: a pot must cost something (the P2 wheel needs a price)");
            }
        }

        [Test]
        public void GameConfig_StarterPotKit_ResolvesToAuthoredTraps()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(
                "Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(config, "the shared GameConfig asset exists");
            Assert.IsNotNull(config.StarterPotKit, "the starter kit array exists (may be empty, never null)");

            var trapIds = AllTraps().Select(t => t.Id).ToHashSet();
            foreach (var entry in config.StarterPotKit)
            {
                if (string.IsNullOrEmpty(entry.TrapDefId) && entry.Count <= 0) continue;   // an inert row
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.TrapDefId),
                    "a starter-kit entry with a count needs a trap id");
                Assert.IsTrue(trapIds.Contains(entry.TrapDefId),
                    $"starter kit entry '{entry.TrapDefId}' must name an authored TrapDef " +
                    "(a typo would grant phantom pots)");
                Assert.GreaterOrEqual(entry.Count, 0, "starter counts are never negative");
            }
        }

        [Test]
        public void StarterPotKit_DefaultGrantsSomething_TheLoopIsPlayableFromMinuteOne()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(
                "Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(config);
            int total = 0;
            foreach (var entry in config.StarterPotKit)
                if (!string.IsNullOrEmpty(entry.TrapDefId)) total += Mathf.Max(0, entry.Count);
            Assert.Greater(total, 0,
                "the cozy starter kit grants at least one pot — a new game must be able to set gear " +
                "before its first shipwright visit (owner-tunable on GameConfig)");
        }
    }
}
