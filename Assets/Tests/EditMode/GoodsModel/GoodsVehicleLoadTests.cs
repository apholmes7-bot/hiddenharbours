using NUnit.Framework;
using HiddenHarbours.Tools.GoodsModel;

namespace HiddenHarbours.Tests.GoodsModel
{
    public class GoodsVehicleLoadTests
    {
        private static GoodsCarrierSpec Carrier(int length, int width, long payload) => new GoodsCarrierSpec
        {
            Id = "cargo.test_bed", Bed = new GoodsBox(length, width, 1200), PayloadG = payload, MaxStackCount = 3,
            AcceptedContainers = new[] { "container.test_pan" }
        };

        [TestCase(1700, 1500, 600000, TestName = "TruckBed_AcceptsTwoNonOverlappingPans")]
        [TestCase(1000, 600, 100000, TestName = "HandCart_FitsOnePanAndRefusesGuessedSecondSlot")]
        [TestCase(2400, 1500, 500000, TestName = "FutureTractorTrailer_HasABedRatherThanTractorSlots")]
        [TestCase(1200, 1000, 1500000, TestName = "FutureForklift_UsesSupportedFootprintAndRatedPayload")]
        public void CarrierProfiles_UseTheirOwnFootprintAndPayload(int length, int width, long payload)
        {
            var model = new GoodsVehicleLoad(Carrier(length, width, payload), GoodsModelFixtures.Limits());
            bool rotate = width >= 650;
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("a") }, 0, 0, rotate), Is.EqualTo(GoodsResult.Success));
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("b") }, rotate ? 450 : 650, 0, rotate),
                Is.EqualTo(width < 650 ? GoodsResult.DoesNotFit : GoodsResult.Success));
        }

        [Test]
        public void Cart_RejectsOversizeTubEvenWhenPayloadIsAvailable()
        {
            var tub = GoodsModelFixtures.Pan();
            tub.Outside = new GoodsBox(1230, 1030, 580);
            tub.MaxStackHeightMm = 1160;
            var model = new GoodsVehicleLoad(Carrier(1000, 600, 1000000), GoodsModelFixtures.Limits());
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("tub", spec: tub) }, 0, 0, false), Is.EqualTo(GoodsResult.DoesNotFit));
            Assert.That(model.LoadCount, Is.Zero);
        }

        [Test]
        public void Truck_WheelArchBlocksPlacement_EmptySpaceBesideItRemainsUsable()
        {
            var spec = Carrier(1700, 1500, 600000);
            spec.Obstructions = new[] { new GoodsSlot { Xmm = 600, Size = new GoodsBox(500, 300, 300) } };
            var model = new GoodsVehicleLoad(spec, GoodsModelFixtures.Limits());
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("a") }, 0, 0, false), Is.EqualTo(GoodsResult.Occupied));
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("a") }, 0, 300, false), Is.EqualTo(GoodsResult.Success));
            Assert.That(model.LoadedMassG, Is.EqualTo(2000));
        }

        [Test]
        public void CombinedPayloadAndOverlapRefusals_HaveZeroMutation()
        {
            var model = new GoodsVehicleLoad(Carrier(1700, 1500, 3999), GoodsModelFixtures.Limits());
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("a") }, 0, 0, false), Is.EqualTo(GoodsResult.Success));
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("b") }, 0, 0, false), Is.EqualTo(GoodsResult.Occupied));
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("b") }, 650, 0, false), Is.EqualTo(GoodsResult.TooHeavy));
            Assert.That(model.LoadCount, Is.EqualTo(1));
            Assert.That(model.LoadedMassG, Is.EqualTo(2000));
        }

        [Test]
        public void StackStaysOneLoad_AndUnloadRemovesOnlyThatChain()
        {
            var model = new GoodsVehicleLoad(Carrier(1700, 1500, 600000), GoodsModelFixtures.Limits());
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("a"), GoodsModelFixtures.Member("b") }, 0, 0, false), Is.EqualTo(GoodsResult.Success));
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("b") }, 650, 0, false), Is.EqualTo(GoodsResult.Occupied));
            Assert.That(model.Unload("b"), Is.EqualTo(GoodsResult.WrongSource));
            Assert.That(model.LoadCount, Is.EqualTo(1));
            Assert.That(model.Unload("a"), Is.EqualTo(GoodsResult.Success));
            Assert.That(model.LoadedMassG, Is.Zero);
            Assert.That(model.LoadCount, Is.Zero);
        }

        [Test]
        public void VehicleHeightAndAllowlist_AreIndependentGates()
        {
            var spec = Carrier(1700, 1500, 600000);
            spec.Bed.HeightMm = 319;
            var model = new GoodsVehicleLoad(spec, GoodsModelFixtures.Limits());
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("a"), GoodsModelFixtures.Member("b") }, 0, 0, false), Is.EqualTo(GoodsResult.DoesNotFit));
            spec.AcceptedContainers = new[] { "container.other" };
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("a") }, 0, 0, false), Is.EqualTo(GoodsResult.Incompatible));
            Assert.That(model.LoadCount, Is.Zero);
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(1051, 0)]
        public void ContainersCannotHangBeyondTheSupport(int xmm, int ymm)
        {
            var model = new GoodsVehicleLoad(Carrier(1700, 1500, 600000), GoodsModelFixtures.Limits());
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("a") }, xmm, ymm, false), Is.EqualTo(GoodsResult.DoesNotFit));
            Assert.That(model.LoadCount, Is.Zero);
        }

        [Test]
        public void ForkRatingChecksLoadCentreAndAggregateMoment_NotOnlyTonnage()
        {
            var spec = Carrier(1700, 1500, 1500000);
            spec.MaxLoadCentreMm = 500;
            spec.MaxLoadMomentGmm = 1000000;
            var model = new GoodsVehicleLoad(spec, GoodsModelFixtures.Limits());
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("a") }, 200, 0, false), Is.EqualTo(GoodsResult.TooHeavy));
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("a") }, 0, 0, false), Is.EqualTo(GoodsResult.Success));
            Assert.That(model.TryLoad(new[] { GoodsModelFixtures.Member("b") }, 0, 450, false), Is.EqualTo(GoodsResult.TooHeavy));
            Assert.That(model.LoadedMassG, Is.EqualTo(2000));
            Assert.That(model.LoadCount, Is.EqualTo(1));
        }
    }
}
