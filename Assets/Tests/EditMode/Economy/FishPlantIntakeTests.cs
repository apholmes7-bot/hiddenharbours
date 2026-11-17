using System;
using HiddenHarbours.Core;
using HiddenHarbours.Economy.PlantIntake;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Economy
{
    public sealed class FishPlantIntakeTests
    {
        private static FishGradeSpec[] Grades() => new[]
        {
            new FishGradeSpec("fishgrade.prime", 850000, 12),
            new FishGradeSpec("fishgrade.standard", 500000, 8),
            new FishGradeSpec("fishgrade.low", 150000, 3)
        };

        private static FishIntakeSpec IntakeSpec(int capacity = 20000) => new FishIntakeSpec(
            "fishintake.test", capacity, new[] { "fish.atlantic_cod", "fish.american_lobster" }, Grades());

        private static DeliveredCatchReceipt Receipt(string id = "delivery.a", int grams = 2500,
            int freshness = 900000, string species = "fish.atlantic_cod", long now = 0)
            => new DeliveredCatchReceipt(id, species, grams, freshness, now);

        private static GradedCatchLot Lot(string id = "delivery.a", int grams = 2500, long now = 0,
            string species = "fish.atlantic_cod")
        {
            Assert.That(new FishPlantIntake(IntakeSpec()).TryAccept(Receipt(id, grams, 900000, species, now), out var lot), Is.True);
            return lot;
        }

        private static GradedFishLine Line(int duration = 11, int tick = 5, int yield = 450000, int capacity = 20000)
            => new GradedFishLine(new GradedFishLineSpec("gradedfishline.test", "fish.atlantic_cod",
                FishProduct.FreshFillet, capacity, yield, duration, tick));

        [TestCase(1000000, "fishgrade.prime", 30)]
        [TestCase(850000, "fishgrade.prime", 30)]
        [TestCase(849999, "fishgrade.standard", 20)]
        [TestCase(500000, "fishgrade.standard", 20)]
        [TestCase(499999, "fishgrade.low", 7)]
        [TestCase(150000, "fishgrade.low", 7)]
        public void Intake_UsesExactGradeBoundariesAndFloorsWholeFishPrice(int freshness, string grade, int payment)
        {
            var receipt = Receipt(freshness: freshness, now: 17);
            Assert.That(new FishPlantIntake(IntakeSpec()).TryAccept(receipt, out var lot), Is.True);
            Assert.That(lot.GradeId, Is.EqualTo(grade));
            Assert.That(lot.Payment, Is.EqualTo(payment));
            Assert.That(lot.Source, Is.EqualTo(receipt));
            Assert.That(lot.Loaded, Is.False);
        }

        [TestCase(0, 900000, "fish.atlantic_cod", 0)]
        [TestCase(-1, 900000, "fish.atlantic_cod", 0)]
        [TestCase(20001, 900000, "fish.atlantic_cod", 0)]
        [TestCase(2500, -1, "fish.atlantic_cod", 0)]
        [TestCase(2500, 1000001, "fish.atlantic_cod", 0)]
        [TestCase(2500, 149999, "fish.atlantic_cod", 0)]
        [TestCase(2500, 0, "fish.atlantic_cod", 0)]
        [TestCase(2500, 900000, "fish.unknown", 0)]
        [TestCase(2500, 900000, null, 0)]
        [TestCase(2500, 900000, "fish.atlantic_cod", -1)]
        public void Intake_RefusesInvalidDeliveryWithoutBurningReceipt(int grams, int freshness, string species, long now)
        {
            var intake = new FishPlantIntake(IntakeSpec());
            Assert.That(intake.TryAccept(Receipt(grams: grams, freshness: freshness, species: species, now: now), out var rejected), Is.False);
            Assert.That(rejected, Is.Null);
            Assert.That(intake.TryAccept(Receipt(), out var accepted), Is.True);
            Assert.That(accepted.Payment, Is.EqualTo(30));
        }

        [TestCase(null)] [TestCase("")] [TestCase(" ")]
        public void Intake_RequiresCompletedDeliveryIdentity(string id)
        {
            Assert.That(new FishPlantIntake(IntakeSpec()).TryAccept(Receipt(id), out var lot), Is.False);
            Assert.That(lot, Is.Null);
        }

        [Test]
        public void Intake_DuplicateReceiptCannotProduceSecondSettlementEvenWithChangedPayload()
        {
            var intake = new FishPlantIntake(IntakeSpec());
            Assert.That(intake.TryAccept(Receipt(), out var first), Is.True);
            Assert.That(intake.TryAccept(Receipt(grams: 9000), out var duplicate), Is.False);
            Assert.That(duplicate, Is.Null);
            Assert.That(first.Payment, Is.EqualTo(30));
            Assert.That(intake.TryAccept(Receipt("delivery.b", 20000), out var atCapacity), Is.True);
            Assert.That(atCapacity.Payment, Is.EqualTo(240));
        }

        [Test]
        public void Intake_PriceUsesWideArithmeticAndNoHiddenMinimum()
        {
            var spec = new FishIntakeSpec("fishintake.large", int.MaxValue, new[] { "fish.atlantic_cod" },
                new[] { new FishGradeSpec("fishgrade.large", 1, int.MaxValue) });
            var intake = new FishPlantIntake(spec);
            Assert.That(intake.TryAccept(Receipt(grams: int.MaxValue), out var large), Is.True);
            Assert.That(large.Payment, Is.EqualTo((long)int.MaxValue * int.MaxValue / 1000));
            Assert.That(new FishPlantIntake(IntakeSpec()).TryAccept(Receipt(grams: 1), out var tiny), Is.True);
            Assert.That(tiny.Payment, Is.Zero);
        }

        [Test]
        public void Intake_SpecDetachesEditableSpeciesAndGradeArrays()
        {
            var species = new[] { "fish.atlantic_cod" };
            var grades = Grades();
            var spec = new FishIntakeSpec("fishintake.detached", 20000, species, grades);
            species[0] = "fish.unknown";
            grades[0] = new FishGradeSpec("fishgrade.changed", 1, 999);
            Assert.That(new FishPlantIntake(spec).TryAccept(Receipt(), out var lot), Is.True);
            Assert.That(lot.GradeId, Is.EqualTo("fishgrade.prime"));
            Assert.That(lot.Payment, Is.EqualTo(30));
        }

        [Test]
        public void Intake_RejectsAmbiguousOrEconomicallyInvertedDefinitions()
        {
            Assert.Throws<ArgumentException>(() => new FishIntakeSpec("bad", 1, new[] { "fish.cod" }, Grades()));
            Assert.Throws<ArgumentException>(() => new FishIntakeSpec("fishintake.test", 0, new[] { "fish.cod" }, Grades()));
            Assert.Throws<ArgumentException>(() => new FishIntakeSpec("fishintake.test", 1, new[] { "fish.cod", "fish.cod" }, Grades()));
            Assert.Throws<ArgumentException>(() => new FishIntakeSpec("fishintake.test", 1, null, Grades()));
            Assert.Throws<ArgumentException>(() => new FishIntakeSpec("fishintake.test", 1, new[] { "fish.cod" }, Array.Empty<FishGradeSpec>()));
            Assert.Throws<ArgumentException>(() => new FishIntakeSpec("fishintake.test", 1, new[] { "fish.cod" }, new FishGradeSpec[] { null }));
            var prime = Grades()[0];
            Assert.Throws<ArgumentException>(() => new FishIntakeSpec("fishintake.test", 1, new[] { "fish.cod" }, new[] { prime, prime }));
            Assert.Throws<ArgumentException>(() => new FishIntakeSpec("fishintake.test", 1, new[] { "fish.cod" },
                new[] { prime, new FishGradeSpec("fishgrade.equal", 850000, 10) }));
            Assert.Throws<ArgumentException>(() => new FishIntakeSpec("fishintake.test", 1, new[] { "fish.cod" },
                new[] { prime, new FishGradeSpec("fishgrade.inverted", 500000, 13) }));
        }

        [TestCase("fishgrade.bad-id", 1, 1)]
        [TestCase("fishgrade.test\n", 1, 1)]
        [TestCase("fishgrade.test", 0, 1)]
        [TestCase("fishgrade.test", 1000001, 1)]
        [TestCase("fishgrade.test", 1, 0)]
        public void Grade_RejectsInvalidData(string id, int freshness, int price)
            => Assert.Throws<ArgumentException>(() => new FishGradeSpec(id, freshness, price));

        [Test]
        public void Line_CompletesOnAbsoluteSlowTickAndRetainsMassGradeAndIdentity()
        {
            var line = Line();
            var lot = Lot(grams: 2501, now: 12);
            Assert.That(line.TryLoad(lot, 12), Is.True);
            Assert.That(line.ReadyAtGameSeconds, Is.EqualTo(25));
            line.AdvanceTo(24);
            Assert.That(line.HasOutput, Is.False);
            Assert.That(line.TryCollect(out _), Is.False);
            line.AdvanceTo(25);
            Assert.That(line.TryCollect(out var product), Is.True);
            Assert.That(product.SourceLot, Is.SameAs(lot));
            Assert.That(product.SourceLot.GradeId, Is.EqualTo("fishgrade.prime"));
            Assert.That(product.Product, Is.EqualTo(FishProduct.FreshFillet));
            Assert.That(product.ProductGrams, Is.EqualTo(1125));
            Assert.That(product.ByproductGrams, Is.EqualTo(1376));
            Assert.That(product.ProductGrams + product.ByproductGrams, Is.EqualTo(2501));
            Assert.That(line.TryCollect(out _), Is.False);
        }

        [Test]
        public void Line_PollingScheduleCannotChangeResult()
        {
            var frequent = Line(); var sparse = Line();
            Assert.That(frequent.TryLoad(Lot(), 3), Is.True);
            Assert.That(sparse.TryLoad(Lot(), 3), Is.True);
            for (int now = 3; now <= 100; now++) frequent.AdvanceTo(now);
            sparse.AdvanceTo(100);
            Assert.That(frequent.TryCollect(out var a), Is.True);
            Assert.That(sparse.TryCollect(out var b), Is.True);
            Assert.That(a.ProductGrams, Is.EqualTo(b.ProductGrams));
            Assert.That(a.ByproductGrams, Is.EqualTo(b.ByproductGrams));
            Assert.That(frequent.ReadyAtGameSeconds, Is.EqualTo(15));
            Assert.That(sparse.ReadyAtGameSeconds, Is.EqualTo(15));
        }

        [Test]
        public void Line_BlocksNewInputUntilExplicitCollectionAndClaimsLotAcrossLines()
        {
            var line = Line(); var other = Line();
            var first = Lot(); var waiting = Lot("delivery.b");
            Assert.That(line.TryLoad(first, 0), Is.True);
            Assert.That(other.TryLoad(first, 0), Is.False);
            Assert.That(line.TryLoad(waiting, 10), Is.False);
            Assert.That(waiting.Loaded, Is.False);
            line.AdvanceTo(1000000);
            Assert.That(line.HasOutput, Is.True);
            Assert.That(line.TryLoad(waiting, 1000000), Is.False);
            Assert.That(line.TryCollect(out _), Is.True);
            Assert.That(line.TryLoad(first, 1000000), Is.False);
            Assert.That(line.TryLoad(waiting, 1000000), Is.True);
        }

        [Test]
        public void Line_RefusesWrongSpeciesCapacityFutureDeliveryAndTimeReversal()
        {
            var line = Line(capacity: 2500);
            var wrong = Lot(species: "fish.american_lobster");
            var large = Lot(grams: 2501);
            var future = Lot(now: 10);
            Assert.That(line.TryLoad(null, 0), Is.False);
            Assert.That(line.TryLoad(wrong, 0), Is.False);
            Assert.That(line.TryLoad(large, 0), Is.False);
            Assert.That(line.TryLoad(future, 0), Is.False);
            Assert.That(wrong.Loaded || large.Loaded || future.Loaded, Is.False);
            Assert.That(line.TryLoad(future, 10), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => line.AdvanceTo(9));
            Assert.Throws<ArgumentOutOfRangeException>(() => line.TryLoad(Lot(), -1));
        }

        [Test]
        public void Line_OverflowCannotConsumeAnInput()
        {
            var line = Line(); var lot = Lot();
            Assert.Throws<OverflowException>(() => line.TryLoad(lot, long.MaxValue - 1));
            Assert.That(lot.Loaded, Is.False);
            Assert.That(line.HasOutput, Is.False);
        }

        [TestCase(0, 450000, 11, 5, FishProduct.FreshFillet)]
        [TestCase(20000, 0, 11, 5, FishProduct.FreshFillet)]
        [TestCase(20000, 1000001, 11, 5, FishProduct.FreshFillet)]
        [TestCase(20000, 450000, 0, 5, FishProduct.FreshFillet)]
        [TestCase(20000, 450000, 11, 0, FishProduct.FreshFillet)]
        [TestCase(20000, 450000, 11, 5, FishProduct.Raw)]
        [TestCase(20000, 450000, 11, 5, (FishProduct)999)]
        public void Line_RejectsInvalidRecipe(int capacity, int yield, int duration, int tick, FishProduct product)
            => Assert.Throws<ArgumentException>(() => new GradedFishLineSpec("gradedfishline.test", "fish.atlantic_cod",
                product, capacity, yield, duration, tick));
    }
}
