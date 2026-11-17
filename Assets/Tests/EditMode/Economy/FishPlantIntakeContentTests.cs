using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy.PlantIntake;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Economy
{
    public sealed class FishPlantIntakeContentTests
    {
        private const string Root = "Assets/_Project/Data/FishPlantIntake/";

        [TestCase(typeof(FishGradeDef), "fishgrade", 3)]
        [TestCase(typeof(FishIntakeDef), "fishintake", 1)]
        [TestCase(typeof(GradedFishLineDef), "gradedfishline", 1)]
        public void Content_AllNewDefsHaveUniqueStableIdsAndOneEntityPerFile(Type type, string prefix, int count)
        {
            string[] guids = AssetDatabase.FindAssets("t:" + type.Name, new[] { "Assets/_Project" });
            Assert.That(guids.Length, Is.EqualTo(count));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                StringAssert.StartsWith(Root, path);
                Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).Length, Is.EqualTo(1), path);
                var asset = AssetDatabase.LoadAssetAtPath(path, type);
                Assert.That(asset, Is.Not.Null, path);
                string id = (string)type.GetField("Id").GetValue(asset);
                StringAssert.IsMatch("^" + prefix + @"\.[a-z][a-z0-9_]*$", id);
                Assert.That(ids.Add(id), Is.True, path);
                Assert.DoesNotThrow(() => type.GetMethod("BuildSpec").Invoke(asset, null), path);
            }
        }

        [Test]
        public void Content_AuthoredIntakeAndLinePreserveSettlementProvenanceAndMass()
        {
            var intakeDef = AssetDatabase.LoadAssetAtPath<FishIntakeDef>(Root + "NmcHandIntake.asset");
            var lineDef = AssetDatabase.LoadAssetAtPath<GradedFishLineDef>(Root + "CodFilletSlot.asset");
            Assert.That(intakeDef, Is.Not.Null); Assert.That(lineDef, Is.Not.Null);
            CollectionAssert.AreEqual(new[] { "fish.atlantic_cod" }, intakeDef.AcceptedSpeciesIds);
            Assert.That(intakeDef.Grades.Length, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { "fishgrade.prime", "fishgrade.standard", "fishgrade.low" },
                Array.ConvertAll(intakeDef.Grades, g => g.Id));
            var speciesIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { "Assets/_Project/Data" }))
            {
                using var serialized = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)));
                speciesIds.Add(serialized.FindProperty("Id").stringValue);
            }
            foreach (string species in intakeDef.AcceptedSpeciesIds) Assert.That(speciesIds.Contains(species), Is.True, species);
            Assert.That(speciesIds.Contains(lineDef.SpeciesId), Is.True);
            CollectionAssert.Contains(intakeDef.AcceptedSpeciesIds, lineDef.SpeciesId);
            Assert.That(lineDef.MaximumInputGrams, Is.EqualTo(intakeDef.MaximumDeliveryGrams));

            var intake = new FishPlantIntake(intakeDef.BuildSpec());
            Assert.That(intake.TryAccept(new DeliveredCatchReceipt("delivery.content", "fish.atlantic_cod", 2501, 850000, 12), out var lot), Is.True);
            Assert.That(lot.Payment, Is.EqualTo(30));
            var line = new GradedFishLine(lineDef.BuildSpec());
            Assert.That(line.TryLoad(lot, 12), Is.True);
            Assert.That(line.ReadyAtGameSeconds, Is.EqualTo(75));
            line.AdvanceTo(74); Assert.That(line.HasOutput, Is.False);
            line.AdvanceTo(75); Assert.That(line.TryCollect(out var product), Is.True);
            Assert.That(product.Product, Is.EqualTo(FishProduct.FreshFillet));
            Assert.That(product.ProductGrams, Is.EqualTo(1125));
            Assert.That(product.ByproductGrams, Is.EqualTo(1376));
            Assert.That(product.SourceLot.Source.ReceiptId, Is.EqualTo("delivery.content"));
            Assert.That(product.SourceLot.GradeId, Is.EqualTo("fishgrade.prime"));
        }

        [Test]
        public void Content_BuildSpecDetachesDefEditsAndRejectsMissingGradeReferences()
        {
            var grade = ScriptableObject.CreateInstance<FishGradeDef>();
            var intake = ScriptableObject.CreateInstance<FishIntakeDef>();
            var line = ScriptableObject.CreateInstance<GradedFishLineDef>();
            try
            {
                grade.Id = "fishgrade.test"; grade.MinimumFreshnessPpm = 500000; grade.PricePerKg = 8;
                intake.Id = "fishintake.test"; intake.MaximumDeliveryGrams = 20000;
                intake.AcceptedSpeciesIds = new[] { "fish.atlantic_cod" }; intake.Grades = new[] { grade };
                line.Id = "gradedfishline.test"; line.SpeciesId = "fish.atlantic_cod";
                line.Product = FishProduct.FreshFillet; line.MaximumInputGrams = 20000;
                line.YieldPpm = 450000; line.ProcessGameSeconds = 60; line.SlowTickSeconds = 5;
                var intakeSpec = intake.BuildSpec(); var lineSpec = line.BuildSpec();
                grade.PricePerKg = 999; grade.MinimumFreshnessPpm = 1000000;
                intake.AcceptedSpeciesIds[0] = "fish.unknown"; intake.Grades[0] = null;
                line.YieldPpm = 1; line.ProcessGameSeconds = 1;
                Assert.That(new FishPlantIntake(intakeSpec).TryAccept(
                    new DeliveredCatchReceipt("delivery.snapshot", "fish.atlantic_cod", 2500, 900000, 0), out var lot), Is.True);
                Assert.That(lot.Payment, Is.EqualTo(20));
                var slot = new GradedFishLine(lineSpec);
                Assert.That(slot.TryLoad(lot, 0), Is.True);
                slot.AdvanceTo(60);
                Assert.That(slot.TryCollect(out var product), Is.True);
                Assert.That(product.ProductGrams, Is.EqualTo(1125));
                Assert.Throws<ArgumentException>(() => intake.BuildSpec());
                intake.Grades = null;
                Assert.Throws<ArgumentException>(() => intake.BuildSpec());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(grade);
                UnityEngine.Object.DestroyImmediate(intake);
                UnityEngine.Object.DestroyImmediate(line);
            }
        }
    }
}
