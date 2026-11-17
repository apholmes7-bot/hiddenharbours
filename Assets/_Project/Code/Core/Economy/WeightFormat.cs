using System;
using System.Globalization;
using System.Text;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// A catch's weight, written down: <b>one</b> rule for every surface that shows one (the landing
    /// line, the notebook register, the catch card, the protected-release line).
    ///
    /// <para><b>Whole grams under a kilogram, kilograms to one decimal from one up</b> — "40 g", "999 g",
    /// "1.0 kg", "7.5 kg". One decimal of a kilogram is 100 g, so a 40 g smelt written as kilograms
    /// read "0.0 kg": a real catch shown as nothing. Grams are the honest unit below a kilogram. The
    /// wording matches the notebook's record line ("Best: 40 g" / "Best: 7.5 kg").</para>
    ///
    /// <para><b>One rounding, in whole grams.</b> The weight is rounded once to grams (half away from
    /// zero, in doubles from the stored float, so it is the same on every machine), and the kilogram
    /// reading is rounded from those grams. A weight that rounds to 1000 g therefore reads "1.0 kg",
    /// never "1000 g"; and a positive weight never reads as nothing (at least "1 g"). Zero or less, or
    /// NaN, is <c>0</c> grams.</para>
    ///
    /// <para><see cref="Text"/> allocates (call it when the weight changes, never per frame);
    /// <see cref="WriteGrams"/> writes into a caller's builder without allocating, for a readout that
    /// counts up frame by frame. Invariant culture: the test oracles compare it literally.</para>
    /// </summary>
    public static class WeightFormat
    {
        /// <summary>Grams in a kilogram: below this many grams a weight reads in grams.</summary>
        public const int GramsPerKg = 1000;

        /// <summary>Grams in a tenth of a kilogram: the kilogram reading's one decimal.</summary>
        public const int GramsPerTenthKg = 100;

        /// <summary>The unit after a gram reading ("40 g").</summary>
        public const string GramsUnit = " g";

        /// <summary>The unit after a kilogram reading ("7.5 kg").</summary>
        public const string KgUnit = " kg";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// <paramref name="kg"/> in whole grams, rounded half away from zero; a positive weight is never
        /// below 1 g; zero, negative or NaN is 0. Capped at <see cref="int.MaxValue"/>.
        /// </summary>
        public static int Grams(float kg)
        {
            if (!(kg > 0f)) return 0;
            double grams = Math.Round((double)kg * GramsPerKg, MidpointRounding.AwayFromZero);
            if (grams < 1d) return 1;
            return grams >= int.MaxValue ? int.MaxValue : (int)grams;
        }

        /// <summary>True when <paramref name="grams"/> reads in grams rather than kilograms.</summary>
        public static bool ReadsInGrams(int grams) => grams < GramsPerKg;

        /// <summary>The number part alone, without the unit: "40" or "7.5". For a caller that puts
        /// words around it (an article before it, say).</summary>
        public static string Number(float kg) => NumberOfGrams(Grams(kg));

        /// <summary>The unit for <paramref name="kg"/>, with its leading space: " g" or " kg".</summary>
        public static string Unit(float kg) => ReadsInGrams(Grams(kg)) ? GramsUnit : KgUnit;

        /// <summary>The weight with its unit: "40 g", "999 g", "1.0 kg", "7.5 kg".</summary>
        public static string Text(float kg)
        {
            int grams = Grams(kg);
            return NumberOfGrams(grams) + (ReadsInGrams(grams) ? GramsUnit : KgUnit);
        }

        /// <summary>The same reading as <see cref="Text"/>, for a count already in whole grams, written
        /// into <paramref name="sb"/> (cleared first). No allocation: digits through
        /// <see cref="CountUpMath.WriteFixed"/>, the unit through <c>Append(string)</c>.</summary>
        public static void WriteGrams(StringBuilder sb, int grams)
        {
            int g = grams > 0 ? grams : 0;
            if (ReadsInGrams(g))
            {
                CountUpMath.WriteFixed(sb, g, 0);
                sb.Append(GramsUnit);
                return;
            }
            CountUpMath.WriteFixed(sb, TenthsKg(g), 1);
            sb.Append(KgUnit);
        }

        /// <summary>How many characters <see cref="WriteGrams"/> letters for <paramref name="grams"/>
        /// ("40 g" is 4, "12.3 kg" is 7), worked out without allocating: for a layout that reserves room
        /// for the reading beside another line.</summary>
        public static int Chars(int grams)
        {
            int g = grams > 0 ? grams : 0;
            if (ReadsInGrams(g)) return Digits(g) + GramsUnit.Length;
            return Digits(TenthsKg(g) / 10) + 2 + KgUnit.Length;   // whole kilograms, the point, one decimal
        }

        private static int Digits(int value)
        {
            int digits = 1;
            for (int v = value; v >= 10; v /= 10) digits++;
            return digits;
        }

        private static string NumberOfGrams(int grams)
        {
            if (ReadsInGrams(grams)) return grams.ToString(Inv);
            int tenths = TenthsKg(grams);
            return (tenths / 10).ToString(Inv) + "." + (tenths % 10).ToString(Inv);
        }

        /// <summary>Whole grams to tenths of a kilogram, rounded half up (grams are never negative here).</summary>
        private static int TenthsKg(int grams)
            => (int)(((long)grams + GramsPerTenthKg / 2) / GramsPerTenthKg);
    }
}
