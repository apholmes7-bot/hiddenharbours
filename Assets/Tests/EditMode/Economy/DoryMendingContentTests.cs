using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class DoryMendingContentTests
    {
        private const string Root = "Assets/_Project/Data/Resources/DoryMending/";

        [Test]
        public void ShippedSequenceAndMaterialsLoadAndValidate()
        {
            var def = AssetDatabase.LoadAssetAtPath<DoryMendingDef>(Root + "StPetersDory.asset");
            Assert.That(def, Is.Not.Null);
            var supplies = new Dictionary<string, SupplyDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:SupplyDef", new[] { "Assets/_Project/Data" }))
            {
                var supply = AssetDatabase.LoadAssetAtPath<SupplyDef>(AssetDatabase.GUIDToAssetPath(guid));
                supplies.Add(supply.Id, supply);
            }
            Assert.DoesNotThrow(() => def.Validate(supplies.ContainsKey));
            Assert.That(def.Id, Is.EqualTo("mending.st_peters_dory"));
            Assert.That(def.BoatId, Is.EqualTo("boat.dory"));
            Assert.That(def.PlaceId, Is.EqualTo("region.st_peters"));
            Assert.That(def.WorkBelowMetres, Is.EqualTo(-0.2f));
            Assert.That(def.LaunchAboveMetres, Is.EqualTo(0.8f));
            Assert.That(def.Jobs.Length, Is.EqualTo(3));
            string[] names = { "patch_plank", "caulk_seam", "paint" };
            float[] minutes = { 30, 25, 20 };
            int[] costs = { 32, 12, 16 };
            var used = new HashSet<string>();
            for (int i = 0; i < def.Jobs.Length; i++)
            {
                var job = def.Jobs[i];
                Assert.That(job.Order, Is.EqualTo(i));
                Assert.That(job.Id, Is.EqualTo("mendjob." + names[i]));
                Assert.That(job.CompletedFlag, Is.EqualTo("st_peters.dory_mend." + names[i]));
                Assert.That(job.NeedsLowWater, Is.True);
                Assert.That(job.WorkGameMinutes, Is.EqualTo(minutes[i]));
                int cost = 0;
                foreach (var material in job.Materials)
                {
                    var supply = supplies[material.ItemId];
                    Assert.That(supply.Catalog.Listed, Is.False, "placement and shop stocking remain separate");
                    Assert.That(supply.Price, Is.GreaterThan(0));
                    Assert.That(supply.DisplayName, Is.Not.Empty);
                    cost += supply.Price * material.Count;
                    used.Add(material.ItemId);
                }
                Assert.That(cost, Is.EqualTo(costs[i]));
            }
            CollectionAssert.AreEquivalent(new[] {
                "supply.patch_plank", "supply.copper_nails", "supply.oakum", "supply.linseed_paint"
            }, used);
            Assert.That(AssetDatabase.FindAssets("t:DoryMendingJobDef", new[] { Root.TrimEnd('/') }).Length, Is.EqualTo(3));
            Assert.That(AssetDatabase.FindAssets("t:DoryMendingDef", new[] { Root.TrimEnd('/') }).Length, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SequenceAcceptsEitherTideRuleOnAnIndividualJob(bool needsLowWater)
        {
            var source = AssetDatabase.LoadAssetAtPath<DoryMendingDef>(Root + "StPetersDory.asset");
            Assert.That(source, Is.Not.Null);
            var def = UnityEngine.Object.Instantiate(source);
            var job = UnityEngine.Object.Instantiate(source.Jobs[0]);
            try
            {
                def.Jobs = (DoryMendingJobDef[])source.Jobs.Clone();
                def.Jobs[0] = job;
                job.NeedsLowWater = needsLowWater;
                Assert.DoesNotThrow(() => def.Validate());
                Assert.That(def.Jobs[0].NeedsLowWater, Is.EqualTo(needsLowWater));
                Assert.That(def.Jobs[1].NeedsLowWater, Is.True);
                Assert.That(def.Jobs[2].NeedsLowWater, Is.True);
                Assert.That(source.Jobs[0].NeedsLowWater, Is.True, "the authored default must stay unchanged");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(job);
                UnityEngine.Object.DestroyImmediate(def);
            }
        }

        [TestCase("order")]
        [TestCase("id")]
        [TestCase("flag")]
        [TestCase("duration")]
        [TestCase("nan")]
        [TestCase("count")]
        [TestCase("duplicate_material")]
        [TestCase("unknown_material")]
        [TestCase("limits")]
        [TestCase("step")]
        [TestCase("missing_job")]
        public void InvalidContentCannotCreateAWorkSession(string fault)
        {
            var source = AssetDatabase.LoadAssetAtPath<DoryMendingDef>(Root + "StPetersDory.asset");
            Assert.That(source, Is.Not.Null);
            var def = UnityEngine.Object.Instantiate(source);
            var clones = new List<DoryMendingJobDef>();
            try
            {
                def.Jobs = (DoryMendingJobDef[])source.Jobs.Clone();
                for (int i = 0; i < def.Jobs.Length; i++)
                {
                    def.Jobs[i] = UnityEngine.Object.Instantiate(source.Jobs[i]);
                    def.Jobs[i].Materials = (MendingMaterial[])source.Jobs[i].Materials.Clone();
                    clones.Add(def.Jobs[i]);
                }
                switch (fault)
                {
                    case "order": def.Jobs[1].Order = 0; break;
                    case "id": def.Jobs[1].Id = def.Jobs[0].Id; break;
                    case "flag": def.Jobs[1].CompletedFlag = def.Jobs[0].CompletedFlag; break;
                    case "duration": def.Jobs[0].WorkGameMinutes = 0; break;
                    case "nan": def.Jobs[0].WorkGameMinutes = float.NaN; break;
                    case "count": def.Jobs[0].Materials[0].Count = 0; break;
                    case "duplicate_material": def.Jobs[0].Materials[1] = def.Jobs[0].Materials[0]; break;
                    case "limits": def.LaunchAboveMetres = def.WorkBelowMetres; break;
                    case "step": def.SearchStepGameMinutes = float.PositiveInfinity; break;
                    case "missing_job": def.Jobs[1] = null; break;
                }
                Assert.Throws<ArgumentException>(() => def.Validate(id => fault != "unknown_material"));
            }
            finally
            {
                foreach (var job in clones) UnityEngine.Object.DestroyImmediate(job);
                UnityEngine.Object.DestroyImmediate(def);
            }
        }
    }
}
