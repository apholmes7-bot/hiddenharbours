using System.Globalization;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// THE ONE COPY of the instrument rigs' number formatting — ECMAScript
    /// <c>Number.prototype.toFixed(1)</c>, <c>String(Math.round(v))</c>, and the rigs' metres→feet
    /// constant. Every ported instrument that prints a number reads it from here.
    ///
    /// <para><b>Why it was hoisted out of <see cref="DepthRigGeometry"/> (ADR 0025 S3, Ruling B).</b>
    /// <c>fish-finder/Art/fishRig.js:128-130</c> is <em>character-identical</em> to
    /// <c>depth-finder/Art/depthRig.js:119-121</c> — same <c>M2FT</c>, same <c>fmtDepth</c>, same
    /// <c>fmtSet</c> — and the radar and chartplotter rigs print in the same house style. Porting it a
    /// second time would produce two implementations of a function whose whole difficulty is that the
    /// obvious implementation is subtly wrong: they would agree on every value a casual test looks at and
    /// disagree at 0.15. One copy, ported once, pinned once.</para>
    ///
    /// <para><b>The subtlety, stated once.</b> JS rounds HALF-UP on the <b>exact binary value</b> of the
    /// double. .NET's <c>ToString("F1")</c> and <c>(decimal)someDouble</c> both round the
    /// 15-significant-digit shortening instead, which disagrees at values like 0.15 (exactly
    /// 0.1499999999999999944…, so JS prints "0.1" and a naive port prints "0.2"), and .NET's default
    /// midpoint mode is half-to-EVEN where ECMAScript is half-up. See <see cref="FixedOne"/>.</para>
    ///
    /// <para><b>Pure and engine-free</b> — no <c>UnityEngine</c> at all, so it is EditMode-testable without
    /// a canvas, a scene or a rig.</para>
    /// </summary>
    public static class RigNumberFormat
    {
        /// <summary>Metres → feet, the rigs' own constant (<c>depthRig.js:119</c> ≡
        /// <c>fishRig.js:128</c>).</summary>
        public const double M2FT = 3.28084;

        /// <summary>
        /// The big 7-seg depth readout's string: metres or feet, one decimal — but a value at or past
        /// 100 loses the decimal and prints as a rounded integer (there is only room for four cells).
        /// <c>depthRig.js:120</c> ≡ <c>fishRig.js:129</c>.
        /// </summary>
        public static string FmtDepth(double metres, bool feet)
        {
            double v = feet ? metres * M2FT : metres;
            if (v >= 100.0) return JsRoundToString(v);
            return FixedOne(v);
        }

        /// <summary>The small set-point / scale-label string — always one decimal, in the display units.
        /// <c>depthRig.js:121</c> ≡ <c>fishRig.js:130</c>.</summary>
        public static string FmtSet(double metres, bool feet)
            => FixedOne(feet ? metres * M2FT : metres);

        /// <summary>
        /// ECMAScript <c>Number.prototype.toFixed(1)</c>, exactly: take the sign off, round the
        /// <b>exact</b> binary value of the double half-UP to one decimal, print with one decimal, put
        /// the sign back (so <c>(-0.04).toFixed(1)</c> is <c>"-0.0"</c>, as in JS).
        ///
        /// <para><b>Why the round-trip through a 17-digit string.</b> A direct
        /// <c>(decimal)someDouble</c> cast rounds to 15 significant digits FIRST, which turns
        /// 0.1499999999999999944… into a clean 0.150000000000000 and then rounds it UP — the port would
        /// print "0.2" where the rig prints "0.1". "G17" round-trips the double, so the tie cases (which
        /// at one decimal are exactly the quarter values 0.25, 0.75, 1.25, … — the only ones a binary
        /// double can land on) survive intact and round half-up as JS does.</para>
        /// </summary>
        public static string FixedOne(double v)
        {
            if (double.IsNaN(v)) return "NAN";
            if (double.IsInfinity(v)) return v > 0 ? "INFINITY" : "-INFINITY";
            bool negative = v < 0.0;
            double magnitude = negative ? -v : v;
            // Defensive floor, far outside an instrument's domain: decimal cannot hold ~1e28, and a double
            // that large has no fractional part left to round anyway.
            if (magnitude >= 1e15) return (negative ? "-" : "") + JsRoundToString(magnitude) + ".0";
            decimal exact = decimal.Parse(magnitude.ToString("G17", CultureInfo.InvariantCulture),
                                          NumberStyles.Float, CultureInfo.InvariantCulture);
            decimal rounded = System.Math.Round(exact, 1, System.MidpointRounding.AwayFromZero);
            return (negative ? "-" : "") + rounded.ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>JS <c>String(Math.round(v))</c> — half toward +∞, then a plain integer string.</summary>
        public static string JsRoundToString(double v)
            => ((long)System.Math.Floor(v + 0.5)).ToString(CultureInfo.InvariantCulture);
    }
}
