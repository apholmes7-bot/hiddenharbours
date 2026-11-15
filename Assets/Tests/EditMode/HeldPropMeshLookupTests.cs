using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Data resolution only. These tests do not claim a held mesh can be rendered: the
    /// depth-aware plotted-line path required by ADR 0053 is still a separate prerequisite.</summary>
    public class HeldPropMeshLookupTests
    {
        private readonly List<Object> _created = new List<Object>();

        private T Make<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            _created.Add(value);
            return value;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object value in _created) Object.DestroyImmediate(value);
            _created.Clear();
        }

        [Test]
        public void ToolVariantsComeFromData_AndNeverFromArrayOrderOrItemNames()
        {
            var tool = Make<ToolDef>();
            var first = Make<HeldPropMeshDef>();
            var second = Make<HeldPropMeshDef>();
            first.Variant = "test_state_a";
            second.Variant = "test_state_b";
            tool.HeldMeshes = new[] { null, second, first };

            Assert.AreSame(first, tool.HeldMeshFor(first.Variant));
            Assert.AreSame(second, tool.HeldMeshFor(second.Variant));
            Assert.IsNull(tool.HeldMeshFor("TEST_STATE_A"));
            Assert.IsNull(tool.HeldMeshFor("missing_state"));
            Assert.IsNull(tool.HeldMeshFor(null));
            Assert.IsNull(tool.HeldMeshFor(""));
        }

        [Test]
        public void AToolWithOneUnlabelledState_ResolvesOnlyThatState()
        {
            var tool = Make<ToolDef>();
            var mesh = Make<HeldPropMeshDef>();
            mesh.Variant = "";
            tool.HeldMeshes = new[] { mesh };
            Assert.AreSame(mesh, tool.HeldMeshFor(""));
            Assert.IsNull(tool.HeldMeshFor("missing_state"));
            Assert.IsNull(tool.HeldMeshFor(null));
        }

        [Test]
        public void AToolWithNoMesh_ReturnsTheSpriteFallbackSignal()
        {
            var tool = Make<ToolDef>();
            Assert.IsNull(tool.HeldMeshFor("test_state"));
            tool.HeldMeshes = null;
            Assert.IsNull(tool.HeldMeshFor("test_state"));
        }

        [Test]
        public void MissingCatchAndBucketMeshes_ReturnNullWithoutBorrowingAnotherState()
        {
            var library = Make<CatchItemLibrary>();
            var fish = Make<HeldPropMeshDef>();
            var bucket = Make<HeldPropMeshDef>();
            library.Configure(new[]
            {
                new CatchItemLibrary.KindEntry { Kind = "test_meshed", HeldMesh = fish },
                new CatchItemLibrary.KindEntry { Kind = "test_sprite" },
            }, new CatchItemLibrary.SpeciesEntry[0], bucketMeshes: new[]
            {
                new CatchItemLibrary.BucketMeshEntry
                {
                    TierId = "test_tier", Band = CatchFillBand.Half, Group = "test_group", Mesh = bucket,
                },
            });

            Assert.AreSame(fish, library.HeldMeshFor("test_meshed"));
            Assert.IsNull(library.HeldMeshFor("test_sprite"));
            Assert.IsNull(library.HeldMeshFor("missing_kind"));
            Assert.IsNull(library.HeldMeshFor(null));
            Assert.AreSame(bucket, library.BucketMesh("test_tier", CatchFillBand.Half, "test_group"));
            Assert.IsNull(library.BucketMesh("missing_tier", CatchFillBand.Half, "test_group"));
            Assert.IsNull(library.BucketMesh("test_tier", CatchFillBand.Full, "test_group"));
            Assert.IsNull(library.BucketMesh("test_tier", CatchFillBand.Half, "missing_group"));
        }

        [Test]
        public void EveryCommittedHeldMesh_ResolvesThroughItsAuthoredOwner()
        {
            const string data = "Assets/_Project/Data";
            var resolved = new HashSet<HeldPropMeshDef>();
            string[] toolGuids = AssetDatabase.FindAssets("t:ToolDef", new[] { data });
            Assert.IsNotEmpty(toolGuids);
            foreach (string guid in toolGuids)
            {
                var tool = AssetDatabase.LoadAssetAtPath<ToolDef>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (HeldPropMeshDef mesh in tool.HeldMeshes)
                {
                    Assert.IsNotNull(mesh, tool.Id);
                    Assert.AreSame(mesh, tool.HeldMeshFor(mesh.Variant), tool.Id);
                    Assert.IsTrue(mesh.IsUsable(), mesh.Id);
                    resolved.Add(mesh);
                }
            }

            string[] libraryGuids = AssetDatabase.FindAssets("t:CatchItemLibrary", new[] { data });
            Assert.AreEqual(1, libraryGuids.Length);
            var library = AssetDatabase.LoadAssetAtPath<CatchItemLibrary>(
                AssetDatabase.GUIDToAssetPath(libraryGuids[0]));
            var rows = new SerializedObject(library);
            SerializedProperty kinds = rows.FindProperty("_kinds");
            Assert.IsTrue(kinds.isArray);
            for (int i = 0; i < kinds.arraySize; i++)
            {
                SerializedProperty row = kinds.GetArrayElementAtIndex(i);
                string kind = row.FindPropertyRelative("Kind").stringValue;
                var mesh = row.FindPropertyRelative("HeldMesh").objectReferenceValue as HeldPropMeshDef;
                Assert.AreSame(mesh, library.HeldMeshFor(kind), kind);
                if (mesh != null) resolved.Add(mesh);
            }
            SerializedProperty buckets = rows.FindProperty("_bucketMeshes");
            Assert.IsTrue(buckets.isArray);
            for (int i = 0; i < buckets.arraySize; i++)
            {
                SerializedProperty row = buckets.GetArrayElementAtIndex(i);
                var mesh = row.FindPropertyRelative("Mesh").objectReferenceValue as HeldPropMeshDef;
                Assert.IsNotNull(mesh);
                Assert.AreSame(mesh, library.BucketMesh(row.FindPropertyRelative("TierId").stringValue,
                    (CatchFillBand)row.FindPropertyRelative("Band").intValue,
                    row.FindPropertyRelative("Group").stringValue), mesh.Id);
                resolved.Add(mesh);
            }

            var committed = new HashSet<HeldPropMeshDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:HeldPropMeshDef", new[] { data }))
            {
                var mesh = AssetDatabase.LoadAssetAtPath<HeldPropMeshDef>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsTrue(mesh.IsUsable(), mesh.Id);
                committed.Add(mesh);
            }
            Assert.IsNotEmpty(committed);
            CollectionAssert.AreEquivalent(committed, resolved,
                "Every baked state must resolve through tool or catch data, with no orphan or extra state.");
        }
    }
}
