using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public partial class ContentValidationTests
    {
        // Reuse the reflected identity inventory. Its existing uniqueness and append-only checks
        // remain authoritative, including the sixteen shared plant pairs.
        private List<Object> _identityAssets;
        private static readonly Regex DisposableIdentityProof = new Regex("_[0-9a-f]{32}(/|\\.|$)");
        private static readonly Regex ExternalReference = new Regex(
            @"\{fileID: (?<file>-?\d+), guid: (?<guid>[0-9a-fA-F]{32}), type: \d+\}");

        private List<Object> IdentityAssets()
        {
            if (_identityAssets != null) return _identityAssets;
            var assets = new List<Object>();
            foreach (Type type in DefIdentityContractTests.IdentifiedDefTypes())
                foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name, new[] { "Assets/_Project" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    // Same deliberate, uncommitted proof copies excluded by the identity harness.
                    if (DisposableIdentityProof.IsMatch(path)) continue;
                    var asset = AssetDatabase.LoadAssetAtPath(path, type);
                    Assert.That(asset, Is.Not.Null, path);
                    assets.Add(asset);
                }
            Assert.That(assets, Is.Not.Empty, "A missing content inventory must not pass vacuously.");
            return _identityAssets = assets;
        }

        [OneTimeTearDown]
        public void ClearContentIdentityInventory() => _identityAssets = null;

        [Test]
        public void AllDefIds_HaveReviewedPrefixesAndStrictShape_ExceptExactLegacyBindings()
        {
            CollectionAssert.AreEquivalent(DefIdentityContractTests.IdentifiedDefTypes().Select(t => t.FullName),
                ContentIdentityPolicy.Prefixes.Keys, "Review a semantic prefix for every new Def type.");
            CollectionAssert.AllItemsAreUnique(ContentIdentityPolicy.LegacyIdFormatAllowlist);
            var malformed = new List<string>();
            var wrongPrefixes = new List<string>();
            foreach (var asset in IdentityAssets())
            {
                string path = AssetDatabase.GetAssetPath(asset);
                using (var serialized = new SerializedObject(asset))
                {
                    var property = serialized.FindProperty("Id") ?? serialized.FindProperty("Spec.Id");
                    Assert.That(property, Is.Not.Null, path + ": serialized identity missing");
                    string id = property.stringValue;
                    string type = asset.GetType().FullName;
                    if (!ContentIdentityPolicy.IsWellFormed(id))
                        malformed.Add(type + "|" + AssetDatabase.AssetPathToGUID(path) + "|" + id);
                    if (!ContentIdentityPolicy.HasExpectedPrefix(type, id)) wrongPrefixes.Add(path + ": " + id);
                }
            }
            Assert.That(wrongPrefixes, Is.Empty, string.Join("\n", wrongPrefixes));
            CollectionAssert.AreEquivalent(ContentIdentityPolicy.LegacyIdFormatAllowlist, malformed,
                "Only the exact legacy GUID/type/id bindings are exempt; remove stale exceptions explicitly.");
        }

        [TestCase("fish.atlantic_cod", true)]
        [TestCase("cooking_station.prep", true)]
        [TestCase("fish.cod2", true)]
        [TestCase("fish.2_cod", true)]
        [TestCase("fish.cod\n", false)]
        [TestCase("fish.Cod", false)]
        [TestCase("fish.cod-red", false)]
        [TestCase("fish.region.cod", false)]
        [TestCase("fish.cod__red", false)]
        [TestCase("fish._cod", false)]
        [TestCase("fish.cod_", false)]
        [TestCase(" fish.cod", false)]
        [TestCase("fish.", false)]
        [TestCase(null, false)]
        public void ContentIdShape_RejectsMalformedNewIdentities(string id, bool expected) =>
            Assert.That(ContentIdentityPolicy.IsWellFormed(id), Is.EqualTo(expected));

        [TestCase("HiddenHarbours.Fishing.FishSpeciesDef", "fish.new_cod", true)]
        [TestCase("HiddenHarbours.Fishing.FishSpeciesDef", "boat.new_cod", false)]
        [TestCase("HiddenHarbours.Art.PlantSpeciesDef", "shoreplant.new_plant", true)]
        [TestCase("HiddenHarbours.Art.PlantSpeciesDef", "plant.new_plant", true)]
        [TestCase("UnknownDef", "fish.new_cod", false)]
        public void ContentIdPrefix_UsesAnExplicitSemanticNamespace(string type, string id, bool expected) =>
            Assert.That(ContentIdentityPolicy.HasExpectedPrefix(type, id), Is.EqualTo(expected));

        [Test]
        public void IdentifiedDefFiles_ContainExactlyOneDefAndItIsTheMainAsset()
        {
            foreach (var asset in IdentityAssets())
            {
                string path = AssetDatabase.GetAssetPath(asset);
                Assert.That(AssetDatabase.IsMainAsset(asset), Is.True, path);
                var defs = AssetDatabase.LoadAllAssetsAtPath(path).OfType<ScriptableObject>()
                    .Where(value => value.GetType().Name.EndsWith("Def", StringComparison.Ordinal)
                        || value.GetType() == typeof(HiddenHarbours.Economy.InstrumentOffer)).ToArray();
                Assert.That(defs.Length, Is.EqualTo(1), path + ": extra Def subassets bypass the identity inventory");
            }
        }

        [Test]
        public void AuthoredDefObjectReferences_ResolveGuidsAndDefLocalFileIds()
        {
            var defTypes = new HashSet<Type>(DefIdentityContractTests.IdentifiedDefTypes());
            var checkedTargets = new HashSet<string>(StringComparer.Ordinal);
            int edges = 0;
            foreach (var asset in IdentityAssets())
            {
                string source = AssetDatabase.GetAssetPath(asset);
                foreach (Match reference in ExternalReference.Matches(File.ReadAllText(source)))
                {
                    string guid = reference.Groups["guid"].Value;
                    string file = reference.Groups["file"].Value;
                    if (file == "0" || IsBuiltinGuid(guid)) continue; // Explicit null / engine resource.
                    edges++;
                    string context = source + " -> " + guid + ":" + file;
                    string target = AssetDatabase.GUIDToAssetPath(guid);
                    Assert.That(target, Is.Not.Empty, "Dangling authored reference: " + context);
                    if (!checkedTargets.Add(guid + ":" + file)) continue;
                    Type targetType = AssetDatabase.GetMainAssetTypeAtPath(target);
                    if (targetType == null || !defTypes.Contains(targetType)) continue;
                    long localId = long.Parse(file, System.Globalization.CultureInfo.InvariantCulture);
                    bool resolves = AssetDatabase.LoadAllAssetsAtPath(target).Any(candidate => candidate != null
                        && defTypes.Contains(candidate.GetType())
                        && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out string actualGuid, out long actualId)
                        && string.Equals(actualGuid, guid, StringComparison.OrdinalIgnoreCase) && actualId == localId);
                    Assert.That(resolves, Is.True, "GUID exists but its Def fileID does not: " + context);
                }
            }
            Assert.That(edges, Is.GreaterThan(0), "The authored reference sweep visited no edges.");
        }

        private static bool IsBuiltinGuid(string guid) => guid == "00000000000000000000000000000000"
            || guid == "0000000000000000e000000000000000"
            || guid == "0000000000000000f000000000000000";

        [TestCase("  Target: {fileID: 11400000, guid: 1234567890abcdef1234567890abcdef, type: 2}", 1)]
        [TestCase("  - {fileID: -22, guid: 1234567890abcdef1234567890abcdef, type: 2}", 1)]
        [TestCase("  Target: {fileID: 0}", 0)]
        public void AuthoredReferenceScan_FindsScalarAndArrayReferences(string yaml, int expected) =>
            Assert.That(ExternalReference.Matches(yaml).Count, Is.EqualTo(expected));

        [Test]
        public void EveryListedCatalogEntry_NamesExistingSellers()
        {
            var sellers = new HashSet<string>(LoadAll<HiddenHarbours.Economy.SellerDef>().Select(s => s.Id), StringComparer.Ordinal);
            Assert.That(sellers, Is.Not.Empty);
            int listed = 0;
            // Includes offers that do not end in Def; all seven shipped catalog arms share this interface.
            var listings = LoadAll<ScriptableObject>().OfType<HiddenHarbours.Economy.ICatalogListing>();
            foreach (var listing in listings)
            {
                if (!listing.Catalog.Listed) continue;
                listed++;
                Assert.That(listing.Catalog.Sellers, Is.Not.Null.And.Not.Empty, listing.ListingId);
                foreach (string seller in listing.Catalog.Sellers)
                    Assert.That(sellers.Contains(seller ?? ""), Is.True, listing.ListingId + ": unknown seller " + seller);
            }
            Assert.That(listed, Is.GreaterThan(0));
        }
    }
}
