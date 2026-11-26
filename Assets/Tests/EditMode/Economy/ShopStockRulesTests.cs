using System;
using HiddenHarbours.Economy;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode.Economy
{
    public class ShopStockRulesTests
    {
        private const string Shop = "shop.test";
        private const string Line = "line.test";

        private static ShopStockSpec Spec(int maximum = 5, int floor = 0, int delivery = 2) => new ShopStockSpec
        {
            Id = Shop, SellerId = "seller.test", PremisesId = "premises.test",
            Shelves = new[] { new ShopShelfSlots { FixtureId = "fixture.test", SlotCount = maximum } },
            Stock = new[] { new ShopStockLine
            {
                Id = Line, ListingId = "gear.test", FixtureId = "fixture.test", FirstSlot = 0,
                MaxOnShelf = maximum, UnitsPerItem = 1, Floor = floor, ItemsPerDelivery = delivery,
                DeliveryWeekdays = ShopDeliveryDays.Monday | ShopDeliveryDays.Thursday
            } }
        };

        [TestCase(true)]
        [TestCase(false)]
        public void RepeatedLineOrListingIdentitiesAreRejectedEvenOnDistinctFixtures(bool repeatLine)
        {
            var spec = Spec(); var second = spec.Stock[0].Snapshot();
            second.FixtureId = "fixture.other";
            if (repeatLine) second.ListingId = "gear.other";
            else second.Id = "line.other";
            spec.Shelves = new[] { spec.Shelves[0], new ShopShelfSlots { FixtureId = second.FixtureId, SlotCount = 5 } };
            spec.Stock = new[] { spec.Stock[0], second };
            Assert.Throws<ArgumentException>(() => spec.Validate());
        }

        [Test]
        public void InvalidListingOrSellerIsRejectedByContentValidation()
        {
            var spec = Spec();
            Assert.DoesNotThrow(() => spec.Validate((seller, item) => seller == "seller.test" && item == "gear.test"));
            spec.Stock[0].ListingId = "gear.missing";
            Assert.Throws<ArgumentException>(() => spec.Validate((seller, item) => item == "gear.test"));
            spec.Stock[0].ListingId = "gear.test"; spec.SellerId = "seller.other";
            Assert.Throws<ArgumentException>(() => spec.Validate((seller, item) => seller == "seller.test"));
        }

        [Test]
        public void DuplicateSlotsOrUnknownFixturesAreRejected()
        {
            var spec = Spec(); var extra = spec.Stock[0].Snapshot();
            extra.Id = "line.other"; extra.ListingId = "gear.other";
            spec.Stock = new[] { spec.Stock[0], extra };
            Assert.Throws<ArgumentException>(() => spec.Validate());
            extra.FixtureId = "fixture.missing";
            Assert.Throws<ArgumentException>(() => spec.Validate());
        }

        [TestCase("capacity")]
        [TestCase("floor")]
        [TestCase("units")]
        [TestCase("days")]
        [TestCase("delivery")]
        [TestCase("line_id")]
        [TestCase("shop_id")]
        [TestCase("credit")]
        public void InvalidAuthoredStockFailsClosed(string field)
        {
            var spec = Spec(); var line = spec.Stock[0];
            switch (field)
            {
                case "capacity": line.FirstSlot = int.MaxValue; break;
                case "floor": line.Floor = 6; break;
                case "units": line.UnitsPerItem = 0; break;
                case "days": line.DeliveryWeekdays = (ShopDeliveryDays)128; break;
                case "delivery": line.ItemsPerDelivery = 0; break;
                case "line_id": line.Id = "line.BAD"; break;
                case "shop_id": spec.Id = "shop.BAD"; break;
                case "credit": spec.Credit.Enabled = true; break;
            }
            Assert.Throws<ArgumentException>(() => spec.Validate());
        }
    }
}
