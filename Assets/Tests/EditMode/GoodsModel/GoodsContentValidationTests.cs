using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Tools.GoodsModel;

namespace HiddenHarbours.Tests.GoodsModel
{
    public class GoodsContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data/GoodsModelResearch";

        private static GoodsContainerDef[] Containers() => AssetDatabase.FindAssets("t:GoodsContainerDef", new[] { DataRoot })
            .Select(guid => AssetDatabase.LoadAssetAtPath<GoodsContainerDef>(AssetDatabase.GUIDToAssetPath(guid)))
            .OrderBy(def => def.Spec.Id, StringComparer.Ordinal).ToArray();

        [Test]
        public void SixSeparateResearchDefs_LoadWithUniqueIdsAndValidData()
        {
            var defs = Containers();
            Assert.That(defs.Length, Is.EqualTo(6));
            Assert.That(defs.Select(def => def.Spec.Id).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(6));
            foreach (var def in defs)
            {
                Assert.That(def.Spec.Validate(), Is.Empty, def.name);
                Assert.That(def.DimensionSource, Is.Not.Null.And.Not.Empty, def.name);
                Assert.That(def.Provenance, Is.Not.Null.And.Not.Empty, def.name);
                Assert.That(def.Spec.AllowHandCarriedStack, Is.False, "No proposed stock profile hand-lifts a stack");
            }
        }

        [Test]
        public void SelectedTubRevisions_KeepMeasuredDimensionsVolumeAndMass()
        {
            var defs = Containers().ToDictionary(def => def.Spec.Id, def => def.Spec);
            var small = defs["container.research_fish_tub_460"];
            Assert.That(small.Outside.LengthMm, Is.EqualTo(1230));
            Assert.That(small.Outside.WidthMm, Is.EqualTo(1030));
            Assert.That(small.Outside.HeightMm, Is.EqualTo(580));
            Assert.That(small.UsableVolumeMl, Is.EqualTo(448000));
            Assert.That(small.EmptyMassG, Is.EqualTo(41000));
            var large = defs["container.research_fish_tub_660"];
            Assert.That(large.Outside.HeightMm, Is.EqualTo(750));
            Assert.That(large.UsableVolumeMl, Is.EqualTo(631000));
            Assert.That(large.EmptyMassG, Is.EqualTo(52000));
            foreach (var tub in new[] { small, large })
            {
                Assert.That(tub.RequiredHands, Is.Zero);
                Assert.That(tub.HasLid, Is.False, "Unmeasured lid geometry must not acquire a stacking promise");
                Assert.That(tub.MaxStackCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void RecommendedHandDefaults_AreData_AndIncludeTheExistingLegalCod()
        {
            var def = AssetDatabase.LoadAssetAtPath<GoodsHandlingDef>(DataRoot + "/HandlingResearch.asset");
            Assert.That(def, Is.Not.Null);
            Assert.That(def.Id, Is.EqualTo("handling.research_two_hands"));
            Assert.That(def.Limits.IsValid, Is.True);
            Assert.That(def.Limits.OneHandEnvelope.LengthMm, Is.EqualTo(620));
            Assert.That(def.Limits.OneHandMassG, Is.EqualTo(8000));
            Assert.That(def.Limits.TwoHandEnvelope.LengthMm, Is.EqualTo(1150));
            Assert.That(def.Limits.TwoHandMassG, Is.EqualTo(16000));
            Assert.That(def.Limits.MaxCatchMassG, Is.EqualTo(12000));
            var model = new GoodsCarryModel(def.Limits);
            model.Register(GoodsModelFixtures.Fish("cod", 1092, 12000), "boat");
            Assert.That(model.Take("cod", "boat", true), Is.EqualTo(GoodsResult.Success));
            Assert.That(model.LeftHand, Is.EqualTo("cod"));
            Assert.That(model.RightHand, Is.EqualTo("cod"));
        }

        [Test]
        public void ResearchAssetsHaveNoArtReferences_AndRemainOutsideResources()
        {
            foreach (var def in Containers())
            {
                string path = AssetDatabase.GetAssetPath(def);
                Assert.That(path.Split('/'), Does.Not.Contain("Resources"));
                foreach (string dependency in AssetDatabase.GetDependencies(path))
                    Assert.That(dependency == path || dependency.EndsWith(".cs", StringComparison.Ordinal), Is.True, dependency);
            }
            string root = Directory.GetParent(Application.dataPath).FullName;
            string asmdef = File.ReadAllText(Path.Combine(root, "Assets/_Project/Code/Tools/Editor/GoodsModel/HiddenHarbours.Tools.GoodsModel.asmdef"));
            StringAssert.Contains("\"includePlatforms\": [\"Editor\"]", asmdef);
            StringAssert.Contains("\"autoReferenced\": false", asmdef);
        }
    }
}
