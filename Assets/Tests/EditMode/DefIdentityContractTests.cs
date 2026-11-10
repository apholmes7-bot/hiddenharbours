using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Identity-only coverage for all Id-bearing Def types. Existing records are permanent:
    /// append new identities, preserve old GUID/type/id bindings, and handle retirement explicitly.
    /// Four Def types without Id are tracked separately by the content audit, not granted identities here.</summary>
    public class DefIdentityContractTests
    {
        private const string ContentRoot = "Assets/_Project";
        private const string LedgerPath = "Assets/Tests/EditMode/Fixtures/DefIdentityLedger.json";

        [Serializable]
        public class Identity
        {
            public string Type;
            public string Guid;
            public string Id;
        }

        [Serializable]
        public class Ledger
        {
            public string[] Types;
            public Identity[] Entries;
        }

        private static readonly Dictionary<Type, Identity[]> Observed = new Dictionary<Type, Identity[]>();
        private static Ledger _ledger;
        private static Ledger ReadLedger() => _ledger ??= JsonUtility.FromJson<Ledger>(File.ReadAllText(LedgerPath));

        public static IEnumerable<Type> IdentifiedDefTypes() => TypeCache.GetTypesDerivedFrom<ScriptableObject>()
            .Where(type => !type.IsAbstract && type.Name.EndsWith("Def", StringComparison.Ordinal)
                && type.Namespace != null && type.Namespace.StartsWith("HiddenHarbours", StringComparison.Ordinal)
                && IdField(type) != null)
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        private static FieldInfo IdField(Type type)
        {
            var field = type.GetField("Id", BindingFlags.Instance | BindingFlags.Public);
            return field != null && field.FieldType == typeof(string) ? field : null;
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
                    "Append the new content identity to the ledger; preserve existing records: " + row.Type + " " + row.Id);
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

        private static Identity[] ReadAssets(Type type)
        {
            if (Observed.TryGetValue(type, out var cached)) return cached;
            var rows = new List<Identity>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name, new[] { ContentRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(path, type);
                Assert.That(asset, Is.Not.Null, path);
                Assert.That(asset.GetType(), Is.EqualTo(type), path);
                rows.Add(new Identity { Type = type.FullName, Guid = guid, Id = (string)IdField(type).GetValue(asset) });
            }
            return Observed[type] = rows.ToArray();
        }
    }
}
