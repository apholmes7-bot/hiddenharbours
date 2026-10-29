using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.RigBaking
{
    public sealed class HullMaterialDitherTests
    {
        [Serializable] public sealed class Table { public HullMeshDef.Ramp[] Ramps; }

        static IRigScriptHost MaterialHost(string fragment)
        {
            var host = RigScriptHostFactory.Create();
            host.Execute("globalThis.DitherFixture={MATS:{paint:{off:-1,ramp:['#123456','#789abc']" + fragment + "}}};");
            return host;
        }

        [TestCase("", 1d)]
        [TestCase(",dith:null", 1d)]
        [TestCase(",dith:0", 0d)]
        [TestCase(",dith:0.28", 0.28d)]
        [TestCase(",dith:1", 1d)]
        public void LegacyReader_CarriesTheNumericValueThroughTheDef(string fragment, double expected)
        {
            using var host = MaterialHost(fragment);
            var data = new RigMeshData();
            RigMeshExtractor.ReadMaterials(host, "DitherFixture", data);
            var material = data.Materials.Single();
            var ramp = material.ToRamp();
            Assert.That((double)ramp.DitherWeight, Is.EqualTo((double)(float)expected));
            Assert.That(ramp.DitherWeight, Is.EqualTo((float)expected));
            Assert.That(ramp.Offset, Is.EqualTo(-1));
            Assert.That(ramp.Colors, Is.EqualTo(new[] { new Color32(0x12, 0x34, 0x56, 255), new Color32(0x78, 0x9a, 0xbc, 255) }));
        }

        [TestCase(false, null, 1d)]
        [TestCase(true, null, 1d)]
        [TestCase(true, 0d, 0d)]
        [TestCase(true, 0.28d, 0.28d)]
        public void LayoutReader_DistinguishesAbsentNullAndZero(bool present, object value, double expected)
        {
            var material = new Dictionary<string, object>();
            if (present) material.Add("dith", value);
            Assert.That(RigMaterial.ReadDitherWeight(material), Is.EqualTo((double)(float)expected));
        }

        [TestCase("", 1f)]
        [TestCase(",dith:null", 1f)]
        [TestCase(",dith:0", 0f)]
        [TestCase(",dith:0.28", 0.28f)]
        [TestCase(",dith:1", 1f)]
        public void PaintReader_KeepsTheWeight(string fragment, float expected)
        {
            using var host = MaterialHost(fragment);
            var hull = new HullPaintSchemeBaker.PaintedHull("fixture", "hullmesh.fixture", "Fixture", "paint.fixture_",
                "MATS", "[]", "defaultScheme");
            var table = HullPaintSchemeBaker.ReadMaterialTable(host, "DitherFixture", hull, "fixture");
            Assert.That(table.Single().DitherWeight, Is.EqualTo(expected));
        }

        [Test]
        public void SchemeGroups_DoNotMergeEqualColorsWithDifferentWeights()
        {
            using var host = RigScriptHostFactory.Create();
            host.Execute("globalThis.DitherFixture={schemeIds:['one','two'],matsFor:function(){return {MATS:{" +
                "a:{off:0,ramp:['#123456'],dith:0},b:{off:0,ramp:['#123456'],dith:0.28}}};}};");
            var names = new Dictionary<string, object> { ["a"] = null, ["b"] = null };
            var faces = new List<object> { new Dictionary<string, object> { ["mat"] = "a" }, new Dictionary<string, object> { ["mat"] = "b" } };
            var method = typeof(HullWalkRigExtractor).GetMethod("SchemeStableGroups", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var groups = (Dictionary<string, string>)method.Invoke(null, new object[] { host, "DitherFixture", names, faces });
            Assert.That(groups["a"], Is.Not.EqualTo(groups["b"]));
        }

        [Test]
        public void MissingSerializedMetadata_AndDefaultStruct_KeepFullBayer()
        {
            Assert.That(default(HullMeshDef.Ramp).DitherWeight, Is.EqualTo(1f));
            var old = JsonUtility.FromJson<Table>("{\"Ramps\":[{\"Colors\":[{\"r\":1,\"g\":2,\"b\":3,\"a\":255}],\"Offset\":-1}]}");
            Assert.That(old.Ramps.Single().DitherWeight, Is.EqualTo(1f));
            var fresh = new Table { Ramps = new[] { new RigMaterial { Ramp = old.Ramps[0].Colors, Off = -1 }.ToRamp() } };
            Assert.That(JsonUtility.ToJson(fresh), Is.EqualTo(JsonUtility.ToJson(old)), "Unweighted table keeps the absent representation byte for byte.");
        }

        [TestCase(0f)]
        [TestCase(0.28f)]
        [TestCase(1f)]
        public void SerializedRoundTrip_DoesNotLoseExplicitZero(float weight)
        {
            var table = new Table { Ramps = new[] { new HullMeshDef.Ramp { DitherWeight = weight } } };
            var copy = JsonUtility.FromJson<Table>(JsonUtility.ToJson(table));
            Assert.That(copy.Ramps.Single().DitherWeight, Is.EqualTo(weight));
        }

        [TestCase(0d)]
        [TestCase(0.28d)]
        [TestCase(1d)]
        public void Threshold_UsesTheRigRuleForEveryBayerCell(double weight)
        {
            double defWeight = (double)(float)weight;
            for (int i = 0; i < 16; i++)
            {
                double bay = (i + 0.5) / 16;
                double expected = defWeight <= 0 ? 0.5 : 0.5 + (bay - 0.5) * defWeight;
                Assert.That(RigMeshReferenceRasterizer.DitherThreshold(bay, defWeight), Is.EqualTo(expected));
            }
        }

        [TestCase("bowrider", "bowrider")]
        [TestCase("expressCruiser", "express_cruiser")]
        [TestCase("goFast", "go_fast")]
        public void LayoutExtraction_AndDefTransfer_AgreeWithTheRigsOwnMats(string key, string id)
        {
            var hull = HullMeshFleet.Get(key);
            using var host = RigScriptHostFactory.Create();
            var data = RigMeshExtractor.ExtractFrom(host, hull.ScriptPath, hull.GlobalName, hull: hull.Extraction);
            foreach (var material in data.Materials.Concat(data.InteriorMaterials))
            {
                string expression = hull.GlobalName + ".byId('" + id + "').matsFor().MATS['" + material.Name + "'].dith";
                double expected = (double)(float)host.EvaluateNumber("(function(){var x=" + expression + ";return x==null?1:x;})()");
                Assert.That(material.DitherWeight, Is.EqualTo(expected), material.Name);
                Assert.That(material.ToRamp().DitherWeight, Is.EqualTo((float)expected), material.Name);
            }
        }

        static IEnumerable<TestCaseData> NeutralHulls()
            => HullMeshFleet.Hulls.Where(h => !Regex.IsMatch(File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, h.ScriptPath)), @"\bdith\s*:"))
                .Select(h => new TestCaseData(h.Key).SetName("NeutralTable_" + h.Key));

        [TestCaseSource(nameof(NeutralHulls))]
        public void UnweightedHull_ReextractsByteIdenticalSerializedTables(string key)
        {
            var hull = HullMeshFleet.Get(key);
            var committed = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
            Assert.That(committed, Is.Not.Null);
            using var host = RigScriptHostFactory.Create();
            var fresh = RigMeshExtractor.ExtractFrom(host, hull.ScriptPath, hull.GlobalName, hull: hull.Extraction);
            RigMeshAssetBaker.AppendMeshInteriorIfConverted(host, hull.GlobalName, fresh, hull.Extraction);
            Assert.That(JsonUtility.ToJson(new Table { Ramps = fresh.Materials.Select(m => m.ToRamp()).ToArray() }),
                Is.EqualTo(JsonUtility.ToJson(new Table { Ramps = committed.Ramps })), key + " exterior");
            Assert.That(JsonUtility.ToJson(new Table { Ramps = fresh.InteriorMaterials.Select(m => m.ToRamp()).ToArray() }),
                Is.EqualTo(JsonUtility.ToJson(new Table { Ramps = committed.InteriorRamps })), key + " interior");
        }
    }

    public sealed class HullDitherUploadTests
    {
        [TestCase(0f)]
        [TestCase(0.28f)]
        [TestCase(1f)]
        public void Configure_UploadsExteriorInteriorAndFittingWeights(float weight)
        {
            var host = new GameObject("DitherUploadFixture");
            var mesh = new Mesh();
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward };
                mesh.SetUVs(0, new List<Vector4> { Vector4.zero, Vector4.zero, Vector4.zero });
                mesh.triangles = new[] { 0, 1, 2 };
                var colors = new[] { new Color32(12, 34, 56, 255), new Color32(78, 90, 123, 255) };
                var bayer = Enumerable.Range(0, 16).Select(i => (i + 0.5f) / 16).ToArray();
                var renderer = host.AddComponent<IsoFacetHullRenderer>();
                renderer.Configure(new IsoFacetHullSetup
                {
                    Mesh = mesh, Ramps = new[] { colors }, RampOffsets = new[] { -1 }, RampDitherWeights = new[] { weight },
                    InteriorRamps = new[] { colors }, InteriorRampOffsets = new[] { 1 }, InteriorRampDitherWeights = new[] { weight },
                    LightN = Vector3.forward, Gain = 1, Bayer16 = bayer, PxPerMetre = 32, CellW = 64, CellH = 64,
                    PivotPx = new Vector2(32, 48), ElevationDeg = 40,
                });
                var material = renderer.PosedMesh.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.That(material.GetVectorArray("_RampMeta")[0], Is.EqualTo(new Vector4(2, -1, weight, 0)));
                Assert.That(material.GetVectorArray("_RampMetaInterior")[0], Is.EqualTo(new Vector4(2, 1, weight, 0)));
                var part = new GameObject("Fitting");
                part.transform.SetParent(renderer.PosedMesh, false);
                var fitting = part.AddComponent<IsoFacetPropRenderer>();
                fitting.Configure(new IsoFacetPropSetup
                {
                    Mesh = mesh, Ramps = new[] { colors }, RampOffsets = new[] { -1 }, RampDitherWeights = new[] { weight },
                    LightN = Vector3.forward, Gain = 1, Bayer16 = bayer, PxPerMetre = 32, CellW = 64, CellH = 64,
                    PivotPx = new Vector2(32, 48), ElevationDeg = 40,
                });
                var fittingMaterial = part.GetComponentsInChildren<MeshRenderer>().Select(r => r.sharedMaterial)
                    .First(m => m.shader.name == "HiddenHarbours/IsoFacet");
                Assert.That(fittingMaterial.GetVectorArray("_RampMeta")[0], Is.EqualTo(new Vector4(2, -1, weight, 0)));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void AbsentSetupWeight_PacksOne()
            => Assert.That(IsoFacetRampMeta.Pack(2, -1, null, 0), Is.EqualTo(new Vector4(2, -1, 1, 0)));

        [TestCase(0f)]
        [TestCase(0.28f)]
        [TestCase(1f)]
        public void ProductionAdapters_CarryTheSharedRampWeight(float weight)
        {
            var hull = ScriptableObject.CreateInstance<HullMeshDef>();
            var part = ScriptableObject.CreateInstance<HullPropMeshDef>();
            var vehicle = ScriptableObject.CreateInstance<VehicleMeshDef>();
            var scheme = ScriptableObject.CreateInstance<HullPaintSchemeDef>();
            try
            {
                var ramp = new HullMeshDef.Ramp { Colors = new[] { new Color32(1, 2, 3, 255) }, DitherWeight = weight };
                hull.Ramps = hull.InteriorRamps = part.Ramps = vehicle.Ramps = new[] { ramp };
                var setup = IsoFacetHullPresentationService.ToSetup(hull);
                Assert.That(setup.RampDitherWeights.Single(), Is.EqualTo(weight));
                Assert.That(setup.InteriorRampDitherWeights.Single(), Is.EqualTo(weight));
                Assert.That(IsoFacetHullPresentationService.ToPropSetup(part).RampDitherWeights.Single(), Is.EqualTo(weight));
                Assert.That(IsoFacetVehiclePresentationService.ToSetup(vehicle).RampDitherWeights.Single(), Is.EqualTo(weight));
                scheme.HullMeshId = hull.Id;
                ramp.DitherWeight = 1f - weight;
                scheme.Ramps = new[] { ramp };
                var painted = IsoFacetHullPresentationService.ToSetup(hull, scheme);
                Assert.That(painted.RampDitherWeights.Single(), Is.EqualTo(1f - weight));
                Assert.That(painted.InteriorRampDitherWeights.Single(), Is.EqualTo(weight));
            }
            finally { Object.DestroyImmediate(hull); Object.DestroyImmediate(part); Object.DestroyImmediate(vehicle); Object.DestroyImmediate(scheme); }
        }
    }
}
