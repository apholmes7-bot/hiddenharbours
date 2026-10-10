using System;
using HiddenHarbours.Art;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class PlantFieldScatterTests
    {
        static PlantMaterialRecipe Recipe(float density) => new PlantMaterialRecipe { Density = density,
            Choices = new[] { new PlantChoice { Species = 1, Weight = 1 }, new PlantChoice { Species = 48, Weight = 3 } } };
        [TestCase(0.15f)] [TestCase(0.6f)] [TestCase(1.2f)] [TestCase(3f)]
        public void DensityAndMixStayWithinSamplingError(float density)
        {
            var row = Recipe(density); int n = 0, second = 0, young = 0;
            for (int y = 0; y < 400; y++) for (int x = 0; x < 400; x++)
            {
                var v = PlantFieldScatter.Choose(row, 4, 0.25, x, y, 9109, .3f); if (v == 0) continue;
                n++; if (PlantFieldScatter.Species(v) == 48) second++; if (PlantFieldScatter.Stage(v) == 1) young++;
            }
            Assert.That(n / 40000.0, Is.EqualTo(density).Within(.025));
            Assert.That(second / (double)n, Is.EqualTo(.75).Within(.025));
            Assert.That(young / (double)n, Is.EqualTo(.3).Within(.025));
        }
        [Test] public void CellAndSeedControlEveryChoice()
        {
            var r = Recipe(2); int changed = 0;
            for (int i = 0; i < 1000; i++)
            {
                byte a = PlantFieldScatter.Choose(r, 4, .25, i, -9, 123, .3f);
                Assert.That(PlantFieldScatter.Choose(r, 4, .25, i, -9, 123, .3f), Is.EqualTo(a));
                if (PlantFieldScatter.Choose(r, 4, .25, i, -9, 124, .3f) != a) changed++;
                Assert.That(Math.Abs(PlantFieldScatter.Offset(i, -9, 123, false, .5f)), Is.LessThanOrEqualTo(.2f));
                Assert.That(PlantFieldScatter.Variant(i, -9, 123), Is.InRange(0, 3));
            }
            Assert.That(changed, Is.GreaterThan(300));
        }
        [Test] public void BandsAndBareMaterialsDoNotGrow()
        {
            var r = Recipe(4); foreach (var c in r.Choices) { c.MinElevation = -1; c.MaxElevation = 1; }
            Assert.That(PlantFieldScatter.Choose(r, 3, .25, 1, 1, 1, .3f), Is.Zero);
            Assert.That(PlantFieldScatter.Choose(null, 0, .25, 1, 1, 1, .3f), Is.Zero);
            Assert.Throws<ArgumentException>(() => PlantFieldScatter.Choose(r, 0, double.NaN, 1, 1, 1, .3f));
        }
        [Test] public void ByteCodesCannotAlias()
        {
            for (int s = 1; s <= 63; s++) for (int stage = 0; stage < 3; stage++)
            {
                byte cell = PlantFieldScatter.Pack(s, stage);
                Assert.That(PlantFieldScatter.Species(cell), Is.EqualTo(s)); Assert.That(PlantFieldScatter.Stage(cell), Is.EqualTo(stage));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => PlantFieldScatter.Pack(64, 0));
        }
    }
}
