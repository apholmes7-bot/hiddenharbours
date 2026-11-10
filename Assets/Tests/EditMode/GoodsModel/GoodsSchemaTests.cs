using NUnit.Framework;
using HiddenHarbours.Tools.GoodsModel;

namespace HiddenHarbours.Tests.GoodsModel
{
    public class GoodsSchemaTests
    {
        [Test]
        public void ValidMeasuredEnvelopeAndSeparateCountVolumeLimits_AreAccepted()
        {
            Assert.That(GoodsModelFixtures.Pan().Validate(), Is.Empty);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveDimensions_AreRejected(int length)
        {
            var spec = GoodsModelFixtures.Pan();
            spec.Inside.LengthMm = length;
            Assert.That(spec.Validate(), Is.Not.Empty);
        }

        [Test]
        public void InsideCannotExceedShell_OrVolumeItsClearEnvelope()
        {
            var spec = GoodsModelFixtures.Pan();
            spec.Inside.LengthMm = 651;
            Assert.That(spec.Validate(), Does.Contain("invalid shell, inside or opening"));
            spec = GoodsModelFixtures.Pan();
            spec.UsableVolumeMl = 28801;
            Assert.That(spec.Validate(), Does.Contain("invalid usable volume"));
        }

        [Test]
        public void PlacementOutsideEnvelopeOrOverlapping_IsRejected()
        {
            var spec = GoodsModelFixtures.Pan();
            spec.Slots[1].Xmm = 301;
            Assert.That(spec.Validate(), Does.Contain("placement outside clear envelope or opening"));
            spec.Slots[1].Xmm = 299;
            Assert.That(spec.Validate(), Does.Contain("overlapping placements"));
        }

        [Test]
        public void OpeningCannotPromiseAnUnreachablePlacement()
        {
            var spec = GoodsModelFixtures.Pan();
            spec.Opening = new GoodsBox(200, 200, 120);
            Assert.That(spec.Validate(), Does.Contain("placement outside clear envelope or opening"));
        }

        [TestCase("", "container id")]
        [TestCase("fish.cod", "container id")]
        [TestCase("container.Upper", "container id")]
        public void StableContainerIds_HaveTheirOwnNamespace(string id, string reason)
        {
            var spec = GoodsModelFixtures.Pan();
            spec.Id = id;
            Assert.That(spec.Validate(), Has.Some.Contains(reason));
        }

        [Test]
        public void DuplicateProfilesAndMissingPlacementCount_AreRejected()
        {
            var spec = GoodsModelFixtures.Pan();
            spec.AcceptedProfiles = new[] { "goods.raw_fish", "goods.raw_fish" };
            spec.CountLimit = 3;
            Assert.That(spec.Validate(), Does.Contain("invalid or duplicate profile"));
            Assert.That(spec.Validate(), Does.Contain("invalid count or placements"));
        }

        [Test]
        public void ImpossibleLiftAndLidPolicies_AreRejected()
        {
            var spec = GoodsModelFixtures.Pan();
            spec.RequiredHands = 3;
            spec.StackRequiresLid = true;
            Assert.That(spec.Validate(), Does.Contain("invalid lift limits"));
            Assert.That(spec.Validate(), Does.Contain("invalid stack policy"));
        }

        [Test]
        public void YawRotationIsExplicit_AndCannotStandLongFishUpright()
        {
            var item = new GoodsBox(600, 200, 100);
            Assert.That(item.Fits(new GoodsBox(200, 600, 100)), Is.False);
            Assert.That(item.Fits(new GoodsBox(200, 600, 100), true), Is.True);
            Assert.That(item.Fits(new GoodsBox(200, 200, 600), true), Is.False);
        }

        [Test]
        public void HugeMalformedDimensions_CannotOverflowTheValidator()
        {
            var spec = GoodsModelFixtures.Pan();
            spec.Inside = new GoodsBox(int.MaxValue, int.MaxValue, int.MaxValue);
            Assert.That(spec.Validate(), Is.Not.Empty);
        }
    }
}
