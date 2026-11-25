using NUnit.Framework;
using HiddenHarbours.Tools.GoodsModel;

namespace HiddenHarbours.Tests.GoodsModel
{
    public class GoodsStackRulesTests
    {
        [Test]
        public void ThreePans_IncludeEveryShellAndContents_AndRequireCart()
        {
            var result = GoodsStackRules.Assess(new[]
            {
                GoodsModelFixtures.Member("a", 2000), GoodsModelFixtures.Member("b", 3000), GoodsModelFixtures.Member("c", 4000)
            }, GoodsModelFixtures.Limits());
            Assert.That(result.Result, Is.EqualTo(GoodsResult.Success));
            Assert.That(result.GrossMassG, Is.EqualTo(12000));
            Assert.That(result.HeightMm, Is.EqualTo(480));
            Assert.That(result.InstanceIds, Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(result.MayCarryByHand, Is.False);
        }

        [Test]
        public void FourthPan_IsNotAThirdPanAboveTheBase()
        {
            Assert.That(GoodsStackRules.Assess(new[]
            {
                GoodsModelFixtures.Member("a"), GoodsModelFixtures.Member("b"),
                GoodsModelFixtures.Member("c"), GoodsModelFixtures.Member("d")
            }, GoodsModelFixtures.Limits()).Result, Is.EqualTo(GoodsResult.InvalidStack));
        }

        [Test]
        public void DuplicateIdentity_CannotSupportItself()
        {
            Assert.That(GoodsStackRules.Assess(new[] { GoodsModelFixtures.Member("a"), GoodsModelFixtures.Member("a") },
                GoodsModelFixtures.Limits()).Result, Is.EqualTo(GoodsResult.InvalidStack));
        }

        [Test]
        public void EveryLowerRim_HasItsOwnWeightLimit()
        {
            var weak = GoodsModelFixtures.Pan();
            weak.MaxSupportedMassG = 1999;
            Assert.That(GoodsStackRules.Assess(new[] { GoodsModelFixtures.Member("a", spec: weak), GoodsModelFixtures.Member("b") },
                GoodsModelFixtures.Limits()).Result, Is.EqualTo(GoodsResult.InvalidStack));
            weak.MaxSupportedMassG = 2000;
            Assert.That(GoodsStackRules.Assess(new[] { GoodsModelFixtures.Member("a", spec: weak), GoodsModelFixtures.Member("b") },
                GoodsModelFixtures.Limits()).Result, Is.EqualTo(GoodsResult.Success));
        }

        [Test]
        public void MatchingGroup_StillNeedsAFittingFootprint()
        {
            var large = GoodsModelFixtures.Pan();
            large.Outside.LengthMm++;
            Assert.That(GoodsStackRules.Assess(new[] { GoodsModelFixtures.Member("a"), GoodsModelFixtures.Member("b", spec: large) },
                GoodsModelFixtures.Limits()).Result, Is.EqualTo(GoodsResult.InvalidStack));
        }

        [Test]
        public void DifferentStackGroups_RefuseEvenAtTheSameDimensions()
        {
            var other = GoodsModelFixtures.Pan();
            other.StackGroup = "other_rim";
            Assert.That(GoodsStackRules.Assess(new[] { GoodsModelFixtures.Member("a"), GoodsModelFixtures.Member("b", spec: other) },
                GoodsModelFixtures.Limits()).Result, Is.EqualTo(GoodsResult.InvalidStack));
        }

        [Test]
        public void LocatingLidMustBeClosed_AndItsMassCounts()
        {
            var tote = GoodsModelFixtures.Pan();
            tote.HasLid = true;
            tote.LidMassG = 500;
            tote.StackRequiresLid = true;
            tote.MaxStackCount = 2;
            var open = GoodsStackRules.Assess(new[] { GoodsModelFixtures.Member("a", spec: tote), GoodsModelFixtures.Member("b", spec: tote) },
                GoodsModelFixtures.Limits());
            Assert.That(open.Result, Is.EqualTo(GoodsResult.InvalidStack));
            var closed = GoodsStackRules.Assess(new[]
            {
                GoodsModelFixtures.Member("a", spec: tote, closed: true), GoodsModelFixtures.Member("b", spec: tote, closed: true)
            }, GoodsModelFixtures.Limits());
            Assert.That(closed.Result, Is.EqualTo(GoodsResult.Success));
            Assert.That(closed.GrossMassG, Is.EqualTo(5000));
        }

        [Test]
        public void HeightLimit_IsSeparateFromCount()
        {
            var spec = GoodsModelFixtures.Pan();
            spec.MaxStackHeightMm = 319;
            Assert.That(GoodsStackRules.Assess(new[] { GoodsModelFixtures.Member("a", spec: spec), GoodsModelFixtures.Member("b", spec: spec) },
                GoodsModelFixtures.Limits()).Result, Is.EqualTo(GoodsResult.InvalidStack));
        }

        [Test]
        public void HandCarryNeedsExplicitPermission_EnvelopeAndTotalMass()
        {
            var spec = GoodsModelFixtures.Pan();
            spec.AllowHandCarriedStack = true;
            Assert.That(GoodsStackRules.Assess(new[] { GoodsModelFixtures.Member("a", spec: spec), GoodsModelFixtures.Member("b", spec: spec) },
                GoodsModelFixtures.Limits()).MayCarryByHand, Is.True);
            Assert.That(GoodsStackRules.Assess(new[]
            {
                GoodsModelFixtures.Member("a", 8000, spec), GoodsModelFixtures.Member("b", 8000, spec)
            }, GoodsModelFixtures.Limits()).MayCarryByHand, Is.False);
            spec.RequiredHands = 0;
            Assert.That(GoodsStackRules.Assess(new[] { GoodsModelFixtures.Member("a", spec: spec) },
                GoodsModelFixtures.Limits()).MayCarryByHand, Is.False);
        }

        [TestCase(0, GoodsResult.SupportedByOthers)]
        [TestCase(1, GoodsResult.SupportedByOthers)]
        [TestCase(2, GoodsResult.Success)]
        [TestCase(3, GoodsResult.MissingItem)]
        public void OnlyTheTopCanBeOpenedOrRemoved(int index, GoodsResult expected)
        {
            Assert.That(GoodsStackRules.AccessMember(index, 3), Is.EqualTo(expected));
        }
    }
}
