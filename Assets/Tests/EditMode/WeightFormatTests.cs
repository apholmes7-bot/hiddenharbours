using System.Text;
using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The one weight wording every readout shares (<see cref="WeightFormat"/>): grams under a kilogram,
    /// kilograms to one decimal from one up. A 40 g smelt used to read "0.0 kg" in the landing line —
    /// a real catch shown as nothing. The wording matches the notebook's record line ("Best: 40 g",
    /// "Best: 7.5 kg").
    /// </summary>
    public class WeightFormatTests
    {
        [TestCase(0.02f, "20 g")]
        [TestCase(0.04f, "40 g")]
        [TestCase(0.0001f, "1 g")]      // a positive weight is never "0 g"
        [TestCase(0.0005f, "1 g")]      // half away from zero
        [TestCase(0.0125f, "13 g")]
        [TestCase(0.5f, "500 g")]
        [TestCase(0.999f, "999 g")]
        [TestCase(0.9996f, "1.0 kg")]   // rounds to 1000 g, so it reads as a kilo, not "1000 g"
        [TestCase(1.0f, "1.0 kg")]
        [TestCase(1.23f, "1.2 kg")]
        [TestCase(7.5f, "7.5 kg")]
        [TestCase(9.45f, "9.5 kg")]
        [TestCase(1234.56f, "1234.6 kg")]
        [TestCase(0f, "0 g")]
        [TestCase(-3f, "0 g")]
        [TestCase(float.NaN, "0 g")]
        public void Text_ReadsGramsUnderAKiloAndTenthsOfAKiloAbove(float kg, string expected)
        {
            Assert.AreEqual(expected, WeightFormat.Text(kg));
            Assert.AreEqual(expected, WeightFormat.Number(kg) + WeightFormat.Unit(kg),
                "the number and the unit spell the same reading as Text");
        }

        [TestCase(0.04f, 40)]
        [TestCase(0.0001f, 1)]
        [TestCase(0.9996f, 1000)]
        [TestCase(1.23f, 1230)]
        [TestCase(0f, 0)]
        [TestCase(-1f, 0)]
        public void Grams_AreWholeAndRoundedHalfAwayFromZero(float kg, int expected)
            => Assert.AreEqual(expected, WeightFormat.Grams(kg));

        [Test]
        public void WriteGrams_SpellsExactlyWhatTextSpells_WithoutAllocatingAString()
        {
            var sb = new StringBuilder(16);
            float[] weights = { 0.0001f, 0.04f, 0.18f, 0.5f, 0.999f, 0.9996f, 1f, 1.23f, 7.5f, 1234.56f };
            foreach (float kg in weights)
            {
                WeightFormat.WriteGrams(sb, WeightFormat.Grams(kg));
                Assert.AreEqual(WeightFormat.Text(kg), sb.ToString(), $"{kg} kg");
            }
            WeightFormat.WriteGrams(sb, -5);
            Assert.AreEqual("0 g", sb.ToString(), "a negative count is guarded");
        }

        [Test]
        public void Chars_CountsExactlyWhatWriteGramsLetters()
        {
            var sb = new StringBuilder(16);
            int[] counts = { -5, 0, 1, 9, 10, 99, 100, 999, 1000, 1049, 1050, 9949, 9950, 12300, 99999, 1234560 };
            foreach (int grams in counts)
            {
                WeightFormat.WriteGrams(sb, grams);
                Assert.AreEqual(sb.Length, WeightFormat.Chars(grams), $"{grams} g reads \"{sb}\"");
            }
        }

        // ---- the landing line uses it ---------------------------------------------------------------

        static CatchItem Smelt(float kg)
            => new CatchItem("fish.rainbow_smelt", "Rainbow Smelt", FishCategory.InshoreGroundfish, kg, 1, 0.3f);

        [Test]
        public void TheLandingLine_SaysASmeltsWeightInGrams_NeverZeroKilos()
        {
            FishFeelSettings feel = FishFeelSettings.Default;
            string line = LiftLine.For(Smelt(0.04f), 0.02f, 0.2f, in feel);
            StringAssert.StartsWith("Landed a 40 g rainbow smelt — ", line);
            StringAssert.DoesNotContain("0.0 kg", line);
        }

        [TestCase(0.08f, "Landed an 80 g ")]
        [TestCase(0.018f, "Landed an 18 g ")]
        [TestCase(0.011f, "Landed an 11 g ")]
        [TestCase(0.18f, "Landed a 180 g ")]
        [TestCase(0.11f, "Landed a 110 g ")]
        [TestCase(1.5f, "Landed a 1.5 kg ")]
        [TestCase(8.2f, "Landed an 8.2 kg ")]
        public void TheLandingLine_TakesTheArticleTheNumberIsSpokenWith(float kg, string start)
        {
            FishFeelSettings feel = FishFeelSettings.Default;
            StringAssert.StartsWith(start, LiftLine.For(Smelt(kg), 0.01f, 10f, in feel));
        }

        [Test]
        public void ACatchsOwnLabel_UsesTheSameWording()
            => StringAssert.EndsWith("(40 g)", Smelt(0.04f).ToString());
    }
}
