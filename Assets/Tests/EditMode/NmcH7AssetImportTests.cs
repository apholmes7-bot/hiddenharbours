using System.Linq;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>CI/editor import proof, separate from the portable text/content validation suite.</summary>
    public class NmcH7AssetImportTests
    {
        [TestCaseSource(typeof(NmcH7ContentValidationTests), nameof(NmcH7ContentValidationTests.PieceKeys))]
        public void Metadata_ImportsAsTheExistingSetPieceDef_WithoutArt(string key)
        {
            string path = NmcH7ContentValidationTests.DefPath(key);
            var def = AssetDatabase.LoadAssetAtPath<SetPieceDef>(path);
            Assert.That(def, Is.Not.Null, path);
            Assert.That(def.Id, Is.EqualTo(NmcH7ContentValidationTests.Kinds.Single(k => k.Key == key).Id));
            Assert.That(def.RigKey, Is.EqualTo(key));
            Assert.That(def.Frames, Is.Empty);
            Assert.That(def.HasArt, Is.False);
            Assert.That(def.CellSize, Is.EqualTo(Vector2Int.zero));
            Assert.That(def.GameplayJson, Is.Not.Empty);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(NmcH7ContentValidationTests.GuidFor(path)));
            if (key == "chandlery")
            {
                Assert.That(def.TryGetAnchor("counter", out var counter), Is.True);
                Assert.That(counter, Is.EqualTo(new Vector3(0, 1.2f, 0)));
                Assert.That(def.HasVendor, Is.False);
            }
        }
    }
}
