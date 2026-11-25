using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEditor;

namespace HiddenHarbours.Tests.EditMode
{
    public class AutopilotHullEligibilityTests
    {
        // Read the actual Def fields without introducing a feature-module dependency.
        static KeyValuePair<string, bool> ReadFlag(string path)
        {
            var hull = AssetDatabase.LoadMainAssetAtPath(path);
            Assert.IsNotNull(hull, path);
            using (var serialized = new SerializedObject(hull))
            {
                var id = serialized.FindProperty("Id");
                var offered = serialized.FindProperty("AutopilotOffered");
                Assert.IsNotNull(id, path);
                Assert.IsNotNull(offered, path);
                Assert.IsNotEmpty(id.stringValue, path);
                return new KeyValuePair<string, bool>(id.stringValue, offered.boolValue);
            }
        }

        static KeyValuePair<string, bool>[] Hulls() => ShippedHullPaths().Select(ReadFlag).ToArray();

        public static IEnumerable<string> ShippedHullPaths() => AssetDatabase.FindAssets("t:BoatHullDef", new[] { "Assets/_Project/Data" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, System.StringComparer.Ordinal);

        [Test]
        public void ShippedHullRegistry_HasBothOfferedAndRefusedHulls()
        {
            var hulls = Hulls();
            Assert.That(hulls, Is.Not.Empty, "do not silently run zero hull cases");
            Assert.IsTrue(hulls.Any(h => h.Value));
            Assert.IsTrue(hulls.Any(h => !h.Value));
            CollectionAssert.AllItemsAreUnique(hulls.Select(h => h.Key).ToArray());
        }

        [TestCaseSource(nameof(ShippedHullPaths))]
        public void EveryShippedHull_CanTakeAutopilotExactlyWhenItsDefOffersIt(string path)
        {
            var hull = ReadFlag(path);
            var eligibility = new AutopilotEligibility(Hulls());
            var service = new AutopilotFittingService(eligibility, new InMemoryAutopilotFitments());
            Assert.AreEqual(hull.Value, eligibility.CanTakeAutopilot(hull.Key), hull.Key);
            Assert.AreEqual(hull.Value, service.CanFit(hull.Key), hull.Key);
            Assert.AreEqual(hull.Value, service.CanFit(hull.Key + "#2"), "owned instance of " + hull.Key);
        }

        [Test]
        public void UnknownAndAmbiguousHullIds_AreRefused()
        {
            var hulls = Hulls();
            var offered = hulls.First(h => h.Value);
            var eligibility = new AutopilotEligibility(hulls.Concat(new[] { offered }));
            Assert.IsFalse(eligibility.CanTakeAutopilot(offered.Key), "duplicate identities must fail closed");
            Assert.IsFalse(eligibility.CanTakeAutopilot("boat.unknown"));
            Assert.IsFalse(eligibility.CanTakeAutopilot(null));
            Assert.IsFalse(new AutopilotEligibility(null).CanTakeAutopilot(offered.Key));
        }
    }
}
