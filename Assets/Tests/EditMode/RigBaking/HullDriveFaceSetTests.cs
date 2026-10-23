using System;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;

namespace HiddenHarbours.Tests.RigBaking
{
    public sealed class HullDriveFaceSetTests
    {
        [TestCase("goFast")]
        [TestCase("bowrider")]
        [TestCase("expressCruiser")]
        public void DeclaredDriveFaces_SurviveExtractionAndBake_WithTheRigsOwnCount(string key)
        {
            var hull = HullMeshFleet.Get(key);
            using var host = RigScriptHostFactory.Create();
            var data = RigMeshExtractor.ExtractFrom(host, hull.ScriptPath, hull.GlobalName, hull: hull.Extraction);
            string scope = hull.Extraction.ScopeOr(hull.GlobalName);
            int expected = (int)host.EvaluateNumber("(function(){var H=" + scope + ";" +
                "var p=H.geometry().parts.filter(function(p){return p.id==='drive';})[0];" +
                "return H.faceList({}).concat(H.doorFaces(0),H.interiorFaces()).filter(function(f){return f.tag===p.tag;}).length;})()");
            Assert.That(expected, Is.GreaterThan(0), key + ": no authored drive faces");
            Assert.That(RigMeshAssetBaker.DriveFaceRanges(data).Length, Is.EqualTo(expected), key + ": extraction");

            // Exactly the bake's ordering: append room, then lift the independently posed leaf.
            var leaf = RigMeshAssetBaker.FindDoorLeaf(host, hull.GlobalName, data, hull.Extraction);
            var sides = RigMeshInteriorClassifier.ClassifySides(data);
            RigMeshAssetBaker.AppendMeshInteriorIfConverted(host, hull.GlobalName, data, hull.Extraction);
            Array.Resize(ref sides, data.Faces.Count);
            RigMeshAssetBaker.LiftDoorLeaf(data, leaf, sides);
            var ranges = RigMeshAssetBaker.DriveFaceRanges(data);
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
            Assert.That(def, Is.Not.Null, key);
            Assert.That(def.Mesh, Is.Not.Null, key);
            Assert.That(def.DriveFaceVertexRanges.Length, Is.EqualTo(expected), key + ": committed face set");
            CollectionAssert.AreEqual(ranges, def.DriveFaceVertexRanges, key + ": baked vertex ranges");
            int end = 0;
            foreach (var range in def.DriveFaceVertexRanges)
            {
                Assert.That(range.x, Is.GreaterThanOrEqualTo(end), key + ": overlapping ranges");
                Assert.That(range.y, Is.GreaterThanOrEqualTo(3), key + ": not a face");
                end = range.x + range.y;
                Assert.That(end, Is.LessThanOrEqualTo(def.Mesh.vertexCount), key + ": outside body mesh");
            }
            TestContext.WriteLine(key + ": " + expected + " declared drive faces; saved " + ranges.Length);
        }

        [Test]
        public void EveryHullWithoutADeclaredDrive_KeepsAnEmptySet()
        {
            foreach (var hull in HullMeshFleet.Hulls)
            {
                using var host = RigScriptHostFactory.Create();
                var data = RigMeshExtractor.ExtractFrom(host, hull.ScriptPath, hull.GlobalName, hull: hull.Extraction);
                string scope = hull.Extraction == null ? hull.GlobalName : hull.Extraction.ScopeOr(hull.GlobalName);
                bool hasDrive = host.EvaluateBool("(function(){var H=" + scope + ";" +
                    "if(typeof H.geometry!=='function')return false;var g=H.geometry();" +
                    "return Array.isArray(g.parts)&&g.parts.some(function(p){return p.id==='drive';});})()");
                if (hasDrive) continue;
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                Assert.That(def, Is.Not.Null, hull.MeshId);
                Assert.That(RigMeshAssetBaker.DriveFaceRanges(data), Is.Empty, hull.MeshId + ": extracted");
                Assert.That(def.DriveFaceVertexRanges, Is.Empty, hull.MeshId + ": saved");
                TestContext.WriteLine(hull.MeshId + ": empty drive set");
            }
        }
    }
}
