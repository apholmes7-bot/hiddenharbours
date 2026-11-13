using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Identity = HiddenHarbours.Tests.EditMode.DefIdentityLedger.Identity;
using Ledger = HiddenHarbours.Tests.EditMode.DefIdentityLedger.Ledger;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Identity-only coverage for Def types with Id or Spec.Id. Existing records are permanent:
    /// append new identities, preserve old GUID/type/id bindings, and handle retirement explicitly.
    /// Structural Defs are explicitly exempt; content ids are unique per type.</summary>
    public class DefIdentityContractTests
    {
        private const string ContentRoot = "Assets/_Project";
        // Fixtures such as CumberlandRegionTests save disposable proof copies of real Defs (same id, fresh GUID)
        // into a folder or file suffixed with a 32-hex run id, and keep them for inspection by design. They are
        // not committed content, so the identity contract does not count them.
        private static readonly Regex DisposableProof = new Regex("_[0-9a-f]{32}(/|\\.|$)");

        private const string PlantSpeciesType = "HiddenHarbours.Art.PlantSpeciesDef";
        private const string ShorePlantType = "HiddenHarbours.Art.ShorePlantDef";
        private static readonly Dictionary<string, string> StructuralDefs = new Dictionary<string, string>
        {
            { "HiddenHarbours.Art.SeagullVisualDef", "Resources singleton for the gull's animation, not a content entity." },
            { "HiddenHarbours.Boats.BoatInteriorCellsDef", "Imported pixel cache keyed by its boat interior's id, not a content entity." },
        };

        // Append-only record of the same plant represented by two systems. These 16 pairs are
        // the complete approved exception, not a prefix rule permitting future shared ids.
        private static readonly string[] SharedPlantIds =
        {
            "shoreplant.bayberry",
            "shoreplant.beach_pea",
            "shoreplant.black_rush",
            "shoreplant.bladderwrack",
            "shoreplant.cattail",
            "shoreplant.cordgrass",
            "shoreplant.eelgrass",
            "shoreplant.glasswort",
            "shoreplant.irish_moss",
            "shoreplant.knotted_wrack",
            "shoreplant.marram_grass",
            "shoreplant.saltmeadow_hay",
            "shoreplant.sea_lettuce",
            "shoreplant.sugar_kelp",
            "shoreplant.sweet_fern",
            "shoreplant.threesquare",
        };

        private static readonly Dictionary<Type, Identity[]> Observed = new Dictionary<Type, Identity[]>();
        private static Ledger _ledger;
        private static Ledger ReadLedger() => _ledger ??= DefIdentityLedger.Load();

        private static IEnumerable<Type> ConcreteDefTypes() => TypeCache.GetTypesDerivedFrom<ScriptableObject>()
            .Where(type => !type.IsAbstract && type.Name.EndsWith("Def", StringComparison.Ordinal)
                && type.Namespace != null && type.Namespace.StartsWith("HiddenHarbours", StringComparison.Ordinal))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        public static IEnumerable<Type> IdentifiedDefTypes() => ConcreteDefTypes()
            .Where(type => !StructuralDefs.ContainsKey(type.FullName) && HasIdentity(type));

        private static FieldInfo IdField(Type type)
        {
            var field = type.GetField("Id", BindingFlags.Instance | BindingFlags.Public);
            return field != null && field.FieldType == typeof(string) ? field : null;
        }

        private static FieldInfo SpecField(Type type) => type.GetField("Spec", BindingFlags.Instance | BindingFlags.Public);

        private static bool HasIdentity(Type type) => IdField(type) != null
            || (SpecField(type) != null && IdField(SpecField(type).FieldType) != null);

        private static string ReadIdentity(Type type, UnityEngine.Object asset)
        {
            if (IdField(type) != null) return (string)IdField(type).GetValue(asset);
            var specField = SpecField(type);
            object spec = specField.GetValue(asset);
            Assert.That(spec, Is.Not.Null, type.FullName + ".Spec");
            return (string)IdField(specField.FieldType).GetValue(spec);
        }

        [OneTimeTearDown]
        public void ClearInventory()
        {
            Observed.Clear();
            _ledger = null;
        }

        [TestCaseSource(nameof(IdentifiedDefTypes))]
        public void DefIds_AreNonEmptyAndUniqueWithinType(Type type)
        {
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in ReadAssets(type))
            {
                Assert.That(string.IsNullOrWhiteSpace(row.Id), Is.False, row.Guid);
                Assert.That(seen.ContainsKey(row.Id), Is.False,
                    type.FullName + " repeats " + row.Id + " at " + row.Guid);
                seen.Add(row.Id, row.Guid);
            }
        }

        [TestCaseSource(nameof(IdentifiedDefTypes))]
        public void DefIds_PreserveEveryRecordedIdentityAndRegisterAdditions(Type type)
        {
            var observed = ReadAssets(type).ToDictionary(row => row.Guid, StringComparer.Ordinal);
            var recorded = ReadLedger().Entries.Where(row => row.Type == type.FullName)
                .ToDictionary(row => row.Guid, StringComparer.Ordinal);
            foreach (var row in recorded.Values)
            {
                Assert.That(observed.ContainsKey(row.Guid), Is.True,
                    "Recorded content disappeared or changed type: " + row.Type + " " + row.Id + " " + row.Guid);
                Assert.That(observed[row.Guid].Id, Is.EqualTo(row.Id),
                    "A stable content id was renamed or reassigned: " + row.Guid);
            }
            foreach (var row in observed.Values)
                Assert.That(recorded.ContainsKey(row.Guid), Is.True,
                    "Register the new identity in a GUID addition file; preserve existing records: " + row.Type + " " + row.Id);
        }

        [Test]
        public void IdentityLedger_CoversEveryIdBearingDefTypeAndHasNoAmbiguousRows()
        {
            var ledger = ReadLedger();
            Assert.That(ledger, Is.Not.Null);
            CollectionAssert.AreEquivalent(IdentifiedDefTypes().Select(type => type.FullName), ledger.Types);
            CollectionAssert.AllItemsAreUnique(ledger.Types);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var guids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in ledger.Entries)
            {
                CollectionAssert.Contains(ledger.Types, row.Type);
                Assert.That(string.IsNullOrWhiteSpace(row.Id), Is.False);
                Assert.That(row.Guid, Does.Match("^[0-9a-f]{32}$"));
                Assert.That(ids.Add(row.Type + "\n" + row.Id), Is.True, row.Type + " " + row.Id);
                Assert.That(guids.Add(row.Guid), Is.True, row.Guid);
            }
        }

        [Test]
        public void DefTypes_RequireIdentityExceptTheTwoNamedStructuralTypes()
        {
            var types = ConcreteDefTypes().ToArray();
            Assert.That(StructuralDefs.Count, Is.EqualTo(2));
            foreach (var exemption in StructuralDefs)
            {
                var type = types.Single(candidate => candidate.FullName == exemption.Key);
                Assert.That(HasIdentity(type), Is.False, exemption.Key + ": " + exemption.Value);
                Assert.That(string.IsNullOrWhiteSpace(exemption.Value), Is.False, exemption.Key);
                CollectionAssert.DoesNotContain(ReadLedger().Types, exemption.Key);
            }
            foreach (var type in types.Where(type => !StructuralDefs.ContainsKey(type.FullName)))
                Assert.That(HasIdentity(type), Is.True, "Unclassified content Def: " + type.FullName);
        }

        [Test]
        public void CrossTypeIds_ContainExactlyTheSixteenRecordedPlantPairs()
        {
            Assert.That(SharedPlantIds.Length, Is.EqualTo(16));
            CollectionAssert.AllItemsAreUnique(SharedPlantIds);
            AssertSharedPlantPairs(IdentifiedDefTypes().SelectMany(ReadAssets));
        }

        private static void AssertSharedPlantPairs(IEnumerable<Identity> identities)
        {
            var shared = identities.GroupBy(row => row.Id, StringComparer.Ordinal)
                .Where(group => group.Select(row => row.Type).Distinct(StringComparer.Ordinal).Count() > 1)
                .ToDictionary(group => group.Key, group => group.Select(row => row.Type)
                    .Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
            CollectionAssert.AreEquivalent(SharedPlantIds, shared.Keys,
                "Preserve the approved pairs; a seventeenth shared id or any other cross-type collision requires a ruling.");
            foreach (string id in SharedPlantIds)
                CollectionAssert.AreEquivalent(new[] { PlantSpeciesType, ShorePlantType }, shared[id], id);
        }

        private static List<Identity> ApprovedPlantPairs() => SharedPlantIds.SelectMany(id => new[]
        {
            new Identity { Type = PlantSpeciesType, Id = id },
            new Identity { Type = ShorePlantType, Id = id },
        }).ToList();

        [Test]
        public void CrossTypePolicy_AcceptsRecordedPairsAndUnsharedContent()
        {
            var rows = ApprovedPlantPairs();
            rows.Add(new Identity { Type = "OtherDef", Id = "other.unique" });
            AssertSharedPlantPairs(rows);
        }

        [TestCase("seventeenth-plant")]
        [TestCase("unrelated-collision")]
        [TestCase("third-type")]
        [TestCase("missing-pair")]
        public void CrossTypePolicy_RejectsUnapprovedOrRemovedPairs(string mutation)
        {
            var rows = ApprovedPlantPairs();
            switch (mutation)
            {
                case "seventeenth-plant":
                    rows.Add(new Identity { Type = PlantSpeciesType, Id = "shoreplant.unapproved" });
                    rows.Add(new Identity { Type = ShorePlantType, Id = "shoreplant.unapproved" });
                    break;
                case "unrelated-collision":
                    rows.Add(new Identity { Type = "OtherDef", Id = "other.collision" });
                    rows.Add(new Identity { Type = "AnotherDef", Id = "other.collision" });
                    break;
                case "third-type":
                    rows.Add(new Identity { Type = "OtherDef", Id = SharedPlantIds[0] });
                    break;
                case "missing-pair":
                    rows.RemoveAt(0);
                    break;
            }
            Assert.Throws<AssertionException>(() => AssertSharedPlantPairs(rows));
        }

        private static Identity[] ReadAssets(Type type)
        {
            if (Observed.TryGetValue(type, out var cached)) return cached;
            var rows = new List<Identity>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name, new[] { ContentRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (DisposableProof.IsMatch(path)) continue;
                var asset = AssetDatabase.LoadAssetAtPath(path, type);
                Assert.That(asset, Is.Not.Null, path);
                Assert.That(asset.GetType(), Is.EqualTo(type), path);
                rows.Add(new Identity { Type = type.FullName, Guid = guid, Id = ReadIdentity(type, asset) });
            }
            return Observed[type] = rows.ToArray();
        }
    }
}
