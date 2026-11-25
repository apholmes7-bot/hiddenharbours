using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class TrawlContentValidationTests
    {
        internal const string Root = "Assets/_Project/Data/Trawls/";

        [Test]
        public void Gear_IsValidRegisteredDraftContent_WithoutCatchOrLiveApproval()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TrawlGearAssetDef>(Root + "BottomOtterPrototype.asset");
            Assert.IsNotNull(asset);
            Assert.AreEqual("trawl_gear.bottom_otter_prototype", asset.Spec.Id);
            Assert.IsTrue(asset.Spec.IsValid);
            Assert.IsFalse(asset.ReviewedForUse);
            Assert.IsFalse(asset.TryGetReviewedSpec(out var rejected));
            Assert.IsNull(rejected.Id);
            Assert.AreEqual(0d, asset.Spec.CaptureFraction01);
            Assert.LessOrEqual(System.Math.Ceiling(asset.Spec.MaxTowGameSeconds / asset.Spec.SampleGameSeconds),
                asset.Spec.MaxSamples, "The authored maximum tow must fit its sampling budget.");
        }

        [TestCase("SternPrototype", "trawl_handling.stern_prototype", TrawlHandlingKind.Stern, TrawlWorkStation.Astern)]
        [TestCase("SideStarboardPrototype", "trawl_handling.side_starboard_prototype", TrawlHandlingKind.Side, TrawlWorkStation.Starboard)]
        public void Handling_IsValidDraftContent_WithAnExplicitWorkStation(string name, string id,
            TrawlHandlingKind kind, TrawlWorkStation station)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TrawlHandlingAssetDef>(Root + name + ".asset");
            Assert.IsNotNull(asset);
            Assert.AreEqual(id, asset.Spec.Id);
            Assert.IsTrue(asset.Spec.IsValid);
            Assert.AreEqual(kind, asset.Spec.Kind);
            Assert.AreEqual(station, asset.Spec.WorkStation);
            Assert.IsFalse(asset.ReviewedForUse);
            Assert.IsFalse(asset.TryGetReviewedSpec(out var rejected));
            Assert.IsNull(rejected.Id);
        }

        [Test]
        public void GearReview_RequiresBothApprovalAndValidData_AndReturnsACopy()
        {
            var asset = ScriptableObject.CreateInstance<TrawlGearAssetDef>();
            try
            {
                asset.Spec = AssetDatabase.LoadAssetAtPath<TrawlGearAssetDef>(Root + "BottomOtterPrototype.asset").Spec;
                Assert.IsFalse(asset.TryGetReviewedSpec(out _));
                asset.ReviewedForUse = true;
                Assert.IsTrue(asset.TryGetReviewedSpec(out var copy));
                copy.CodendCapacityIndividuals = 0;
                Assert.IsTrue(asset.Spec.IsValid);
                asset.Spec = copy;
                Assert.IsFalse(asset.TryGetReviewedSpec(out var rejected));
                Assert.IsNull(rejected.Id);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void HandlingReview_RejectsAnApprovedButWrongStation()
        {
            var asset = ScriptableObject.CreateInstance<TrawlHandlingAssetDef>();
            try
            {
                asset.Spec = AssetDatabase.LoadAssetAtPath<TrawlHandlingAssetDef>(Root + "SternPrototype.asset").Spec;
                Assert.IsFalse(asset.TryGetReviewedSpec(out _));
                asset.ReviewedForUse = true;
                Assert.IsTrue(asset.TryGetReviewedSpec(out _));
                asset.Spec.WorkStation = TrawlWorkStation.Port;
                Assert.IsFalse(asset.TryGetReviewedSpec(out var rejected));
                Assert.IsNull(rejected.Id);
            }
            finally { Object.DestroyImmediate(asset); }
        }
    }
}
