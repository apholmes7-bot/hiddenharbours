using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WaveBathymetryImporterTests
    {
        [OneTimeSetUp]
        public void ReportRunBegin() => Debug.Log("[W4-6a] EDITMODE_CASES_BEGIN");

        [OneTimeTearDown]
        public void ReportRunComplete() => Debug.Log("[W4-6a] EDITMODE_CASES_COMPLETE");

        [TestCase("StPeters")]
        [TestCase("NineMileCreek")]
        [TestCase("RicePoint")]
        [TestCase("Cumberland")]
        public void RawPngDecode_AgreesWithPaintedHeightField(string region)
        {
            var recipe = WaveBathymetryTests.ReadRecipe(region);
            var input = WaveBathymetrySource.Read(recipe, WaveBathymetryTests.PathFor(region), AssetDatabase.GUIDToAssetPath, _ => { });
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(recipe.seabedPath);
            Assert.IsNotNull(map.Field);
            for (int i = 0; i < input.Grid.ElevationMm.Length; i += 17)
            {
                float x = (input.Grid.MinXmm + (i % input.Grid.Width + 0.5f) * recipe.cellSizeMm) / 1000f;
                float y = (input.Grid.MinYmm + (i / input.Grid.Width + 0.5f) * recipe.cellSizeMm) / 1000f;
                Assert.AreEqual(map.Field.ElevationAt(new Vector2(x, y)), input.Grid.ElevationMm[i] / 1000f, 0.00051f);
            }
        }
        [TestCase("StPeters")]
        [TestCase("NineMileCreek")]
        [TestCase("RicePoint")]
        [TestCase("Cumberland")]
        public void ImportedBytes_EqualTheCoreBake(string region)
        {
            string path = WaveBathymetryTests.PathFor(region);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var cpu = WaveBathymetryAssets.LoadRequired(path, out var gpu);
            var recipe = WaveBathymetryTests.ReadRecipe(region);
            var input = WaveBathymetrySource.Read(recipe, path, AssetDatabase.GUIDToAssetPath, _ => { });
            var settings = recipe.Settings(); int at = 0;
            foreach (int level in settings.WaterLevelsMm)
                for (int bin = 0; bin < settings.DirectionX.Length; bin++)
                {
                    byte[] expected = WaveBathymetry.Bake(input.Grid, settings, level, bin).Pixels();
                    for (int i = 0; i < expected.Length; i++)
                        if (cpu.Pixels[at + i] != expected[i]) Assert.Fail(path + ": pixel byte " + (at + i) + " differs.");
                    at += expected.Length;
                }
            Assert.AreEqual(cpu.Pixels.Length, at);
            Assert.AreEqual(recipe.id, cpu.Id);
            Assert.AreEqual(TextureFormat.RGBAFloat, gpu.format);
            Assert.IsFalse(gpu.isReadable, "Retain exactly one CPU pixel payload.");
        }

        [TestCase("StPeters")]
        [TestCase("NineMileCreek")]
        [TestCase("RicePoint")]
        [TestCase("Cumberland")]
        public void DependencyList_CoversSeabedHeightStillWaterAndCode(string region)
        {
            string path = WaveBathymetryTests.PathFor(region); var recipe = WaveBathymetryTests.ReadRecipe(region);
            var dependencies = new List<string>();
            var input = WaveBathymetrySource.Read(recipe, path, AssetDatabase.GUIDToAssetPath, dependencies.Add);
            var expected = WaveBathymetrySource.CodePaths.Concat(new[] { recipe.seabedPath, input.HeightPath })
                .Concat(input.StillPath == null ? Array.Empty<string>() : new[] { input.StillPath }).ToArray();
            CollectionAssert.AreEquivalent(expected, dependencies);
            Assert.AreEqual(9, WaveBathymetrySource.CodePaths.Length, "Amend this pin with each code dependency.");
        }

        [TestCase("StPeters")]
        [TestCase("NineMileCreek")]
        [TestCase("RicePoint")]
        [TestCase("Cumberland")]
        public void ReimportTwice_KeepsBytesAndObjectIdentifiers(string region)
        {
            string path = WaveBathymetryTests.PathFor(region);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var first = WaveBathymetryAssets.LoadRequired(path, out var gpu);
            byte[] bytes = (byte[])first.Pixels.Clone();
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(first, out string cpuGuid, out long cpuId));
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(gpu, out string gpuGuid, out long gpuId));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var second = WaveBathymetryAssets.LoadRequired(path, out gpu);
            CollectionAssert.AreEqual(bytes, second.Pixels);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(second, out string newCpuGuid, out long newCpuId);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(gpu, out string newGpuGuid, out long newGpuId);
            Assert.AreEqual(cpuGuid, newCpuGuid); Assert.AreEqual(cpuId, newCpuId);
            Assert.AreEqual(gpuGuid, newGpuGuid); Assert.AreEqual(gpuId, newGpuId);
        }

        [Test]
        public void SourceIntake_RefusesUnsupportedAssetByPath_BeforePublishing()
        {
            var recipe = WaveBathymetryTests.ReadRecipe("StPeters");
            recipe.seabedPath = "Assets/_Project/Data/Regions/StPeters.asset";
            var error = Assert.Throws<InvalidDataException>(() => WaveBathymetrySource.Read(recipe,
                WaveBathymetryTests.PathFor("StPeters"), AssetDatabase.GUIDToAssetPath, _ => { }));
            StringAssert.Contains(recipe.seabedPath, error.Message);
        }

        [Test]
        public void NoGameCodeLoadsTheBakeYet()
        {
            string[] permitted = { "WaveBathymetry.cs", "WaveBathymetryData.cs" };
            foreach (string path in Directory.EnumerateFiles("Assets/_Project/Code", "*.cs", SearchOption.AllDirectories))
                if (!permitted.Contains(Path.GetFileName(path)))
                    StringAssert.DoesNotContain("WaveBathymetry", File.ReadAllText(path), path);
        }
    }
}
