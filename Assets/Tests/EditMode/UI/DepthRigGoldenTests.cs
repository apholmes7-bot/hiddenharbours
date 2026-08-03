using NUnit.Framework;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The C# depth-sounder port's DRIFT GUARD (ADR 0025 addendum — the honest S2 version: the full
    /// pixel golden-master still needs the editor Canvas2D shim we are not building yet, so we pin
    /// numeric geometry, the canvas dims, and the two LCD formatters).
    ///
    /// <para><b>The goldens are an independent transcription of the rig source</b>
    /// (<c>docs/art/rigs/ui/depth-finder/Art/depthRig.js</c>): the tables below were computed from the
    /// JS formulas (<c>fmtDepth</c>/<c>fmtSet</c> :119-121, <c>layout</c> :124-136, the standalone
    /// inset :262) in float64 by a SEPARATE transcription (Python, with <c>decimal</c> reproducing
    /// ECMAScript's exact <c>toFixed</c> semantics), never by running the C# under test — so a bug in
    /// the port cannot leak into its own expected values.</para>
    ///
    /// <para><b>Why the formatters get this much attention.</b> Number formatting is where a port drifts
    /// silently: JS <c>toFixed(1)</c> rounds half-UP on the EXACT binary value of the double, while
    /// .NET's <c>ToString("F1")</c> and a <c>(decimal)</c> cast both round the 15-significant-digit
    /// shortening first. They disagree at 0.15 (JS "0.1", naive port "0.2") and agree everywhere a
    /// casual test would look. The tie rows below are exactly those cases.</para>
    ///
    /// <para><b>Negative-controlled:</b> perturbing the metres→feet constant by 1% moves the printed
    /// strings, proving the table can actually fail.</para>
    /// </summary>
    public class DepthRigGoldenTests
    {
        // depth (m) → fmtDepth metres · fmtDepth feet · fmtSet metres · fmtSet feet
        private static readonly object[][] Fmt =
        {
            new object[] { 0.0,    "0.0",   "0.0",  "0.0",   "0.0" },
            new object[] { 0.04,   "0.0",   "0.1",  "0.0",   "0.1" },
            new object[] { 0.05,   "0.1",   "0.2",  "0.1",   "0.2" },
            new object[] { 0.15,   "0.1",   "0.5",  "0.1",   "0.5" },   // tie: exact value is 0.14999…
            new object[] { 0.25,   "0.3",   "0.8",  "0.3",   "0.8" },   // tie: exactly 0.25 → half-UP
            new object[] { 0.75,   "0.8",   "2.5",  "0.8",   "2.5" },   // tie: exactly 0.75 → half-UP
            new object[] { 1.0,    "1.0",   "3.3",  "1.0",   "3.3" },
            new object[] { 2.5,    "2.5",   "8.2",  "2.5",   "8.2" },
            new object[] { 3.0,    "3.0",   "9.8",  "3.0",   "9.8" },
            new object[] { 9.95,   "9.9",  "32.6",  "9.9",  "32.6" },   // tie: exact value is 9.9499…
            new object[] { 9.99,  "10.0",  "32.8", "10.0",  "32.8" },
            new object[] { 26.2,  "26.2",  "86.0", "26.2",  "86.0" },   // the rig's own default depth
            new object[] { 30.47, "30.5", "100.0", "30.5", "100.0" },   // 99.967 ft — still one decimal
            new object[] { 30.48, "30.5",   "100", "30.5", "100.0" },   // 100.000003 ft — DIGIT ROLLOVER
            new object[] { 30.49, "30.5",   "100", "30.5", "100.0" },
            new object[] { 99.94, "99.9",   "328", "99.9", "327.9" },
            new object[] { 99.95,"100.0",   "328","100.0", "327.9" },   // rounds INTO 100 but keeps ".0"
            new object[] { 100.0,  "100",   "328","100.0", "328.1" },   // …and only ≥100 loses it
            new object[] { 123.45, "123",   "405","123.5", "405.0" },
            new object[] { 305.0,  "305",  "1001","305.0","1000.7" },
        };

        [Test]
        public void Formatters_MatchTheRigSource_OverTheWholeLadder()
        {
            foreach (object[] row in Fmt)
            {
                double m = (double)row[0];
                Assert.AreEqual((string)row[1], DepthRigGeometry.FmtDepth(m, false),
                                $"fmtDepth({m}, metres) — depthRig.js:120");
                Assert.AreEqual((string)row[2], DepthRigGeometry.FmtDepth(m, true),
                                $"fmtDepth({m}, feet) — depthRig.js:120");
                Assert.AreEqual((string)row[3], DepthRigGeometry.FmtSet(m, false),
                                $"fmtSet({m}, metres) — depthRig.js:121");
                Assert.AreEqual((string)row[4], DepthRigGeometry.FmtSet(m, true),
                                $"fmtSet({m}, feet) — depthRig.js:121");
            }
        }

        [Test]
        public void TheDigitRollover_HappensExactlyAtOneHundred_NotNear()
        {
            // The rig drops the decimal at v >= 100 (depthRig.js:120) — four cells, no room. Pinned at
            // both sides of the boundary in FEET, where the conversion decides it: 30.4799 m is
            // 99.9997 ft and keeps its decimal; 30.48 m is 100.0000032 ft and loses it.
            Assert.AreEqual("100.0", DepthRigGeometry.FmtDepth(30.4799, true));
            Assert.AreEqual("100", DepthRigGeometry.FmtDepth(30.48, true));
            Assert.AreEqual("100", DepthRigGeometry.FmtDepth(30.4801, true));
            // And in metres, where it is the raw value.
            Assert.AreEqual("99.9", DepthRigGeometry.FmtDepth(99.9, false));
            Assert.AreEqual("100.0", DepthRigGeometry.FmtDepth(99.99, false));
            Assert.AreEqual("100", DepthRigGeometry.FmtDepth(100.0, false));
            // fmtSet NEVER rolls over — the set-point always carries its decimal (depthRig.js:121).
            Assert.AreEqual("100.0", DepthRigGeometry.FmtSet(100.0, false));
            Assert.AreEqual("1000.7", DepthRigGeometry.FmtSet(305.0, true));
        }

        [Test]
        public void FixedOne_ReproducesEcmaScriptToFixed_IncludingTheSignedZero()
        {
            // JS takes the sign off FIRST, so a small negative prints "-0.0" rather than "0.0".
            Assert.AreEqual("-0.0", DepthRigGeometry.FixedOne(-0.04));
            Assert.AreEqual("-0.3", DepthRigGeometry.FixedOne(-0.25), "ties round away from zero");
            Assert.AreEqual("0.0", DepthRigGeometry.FixedOne(0.0));
            Assert.AreEqual("2.7", DepthRigGeometry.FixedOne(2.675), "exact value is 2.67499… → 2.7");
            Assert.AreEqual("0.5", DepthRigGeometry.FixedOne(0.45), "exact value is 0.45000…1 → 0.5");
            Assert.AreEqual("0.3", DepthRigGeometry.FixedOne(0.35), "exact value is 0.34999… → 0.3");
        }

        // ---- layout / canvas geometry ---------------------------------------------------------------

        [Test]
        public void CanvasDims_AndTheStandaloneInset_ArePinned()
        {
            // depthRig.js:11 (cell) and :262 (X 6% / Y 11% / W 88% / H 78%).
            Assert.AreEqual(480, DepthRigGeometry.W);
            Assert.AreEqual(330, DepthRigGeometry.H);
            Assert.AreEqual(480, DepthRigRender.W, "render and geometry share the cell");
            Assert.AreEqual(330, DepthRigRender.H);
            Assert.AreEqual(29, DepthRigGeometry.InsetX);
            Assert.AreEqual(36, DepthRigGeometry.InsetY);
            Assert.AreEqual(422, DepthRigGeometry.InsetW);
            Assert.AreEqual(257, DepthRigGeometry.InsetH);
            Assert.AreEqual(3.28084, DepthRigGeometry.M2FT, 0d, "the rig's own metres→feet (depthRig.js:119)");
        }

        [Test]
        public void Layout_MatchesTheRigSource_AtTheStandaloneSize()
        {
            DepthRigLayout l = DepthRigGeometry.Layout(29, 36, 422, 257);
            Assert.AreEqual(19, l.Pad);
            Assert.AreEqual(33, l.BrandH);
            Assert.AreEqual(91, l.ColW);
            AssertRect(l.Lcd, 48, 55, 274, 186, "lcd");
            AssertRect(l.Col, 341, 55, 91, 186, "col");
            AssertRect(l.Button0, 341, 55, 91, 56, "button 0 (SET / units)");
            AssertRect(l.Button1, 341, 120, 91, 56, "button 1 (alarm +)");
            AssertRect(l.Button2, 341, 185, 91, 56, "button 2 (alarm −)");
            AssertRect(l.Brand, 48, 254, 384, 33, "brand strip");
        }

        [Test]
        public void Layout_IsBoxParameterised_NotHardCodedToTheStandaloneBox()
        {
            // The whole point of the rig's layout(): the same numbers fall out at ANY mount size, which
            // is what lets S2a/S6 flush-mount the sounder in a console brow.
            DepthRigLayout l = DepthRigGeometry.Layout(10, 8, 211, 128);
            Assert.AreEqual(9, l.Pad);
            Assert.AreEqual(17, l.BrandH);
            Assert.AreEqual(45, l.ColW);
            AssertRect(l.Lcd, 19, 17, 139, 93, "lcd (half size)");
            AssertRect(l.Button0, 167, 17, 45, 27, "button 0 (half size)");
            AssertRect(l.Button1, 167, 49, 45, 27, "button 1 (half size)");
            AssertRect(l.Button2, 167, 81, 45, 27, "button 2 (half size)");
            AssertRect(l.Brand, 19, 116, 193, 17, "brand strip (half size)");
        }

        [Test]
        public void TheThreePushers_AreDisjoint_AndSitBesideTheGlass()
        {
            DepthRigLayout l = DepthRigGeometry.Layout(29, 36, 422, 257);
            Assert.Less(l.Button0.Y + l.Button0.H, l.Button1.Y, "no overlap between pushers 0 and 1");
            Assert.Less(l.Button1.Y + l.Button1.H, l.Button2.Y, "nor 1 and 2");
            Assert.LessOrEqual(l.Lcd.X + l.Lcd.W, l.Col.X, "the glass never runs under the pusher column");
            // A hit must land on exactly one thing (the host's whole interaction rests on this).
            Assert.IsTrue(l.Button1.Contains(l.Button1.X + 1, l.Button1.Y + 1));
            Assert.IsFalse(l.Lcd.Contains(l.Button1.X + 1, l.Button1.Y + 1));
            Assert.IsTrue(l.Lcd.Contains(l.Lcd.X + 1, l.Lcd.Y + 1));
        }

        // ---- NEGATIVE CONTROL: prove the goldens can fail --------------------------------------------

        [Test]
        public void NegativeControl_APerturbedFootConversion_BreaksTheTable()
        {
            // Re-derive the feet column with M2FT off by 1% and confirm the printed strings MOVE. If
            // they didn't, the feet half of the golden table would be pinning nothing.
            const double bad = 3.28084 * 1.01;
            int moved = 0;
            foreach (object[] row in Fmt)
            {
                double m = (double)row[0];
                string good = (string)row[2];
                double v = m * bad;
                string perturbed = v >= 100.0
                    ? DepthRigGeometry.JsRoundToString(v)
                    : DepthRigGeometry.FixedOne(v);
                if (perturbed != good) moved++;
            }
            Assert.Greater(moved, Fmt.Length / 2,
                "a 1% error in the foot conversion must show up across most of the ladder");
        }

        [Test]
        public void NegativeControl_TheTwoShortcutsThatLookRight_BothPrintTheWrongDigit()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;

            // (1) THE DECIMAL-CAST SHORTCUT. `(decimal)someDouble` rounds to 15 significant digits
            // FIRST, so 0.1499999999999999944… becomes a clean 0.150000000000000 and then rounds UP.
            decimal cast = System.Math.Round((decimal)0.15, 1, System.MidpointRounding.AwayFromZero);
            Assert.AreEqual("0.2", cast.ToString("0.0", ci), "the shortcut's answer (specified behaviour)");
            Assert.AreEqual("0.1", DepthRigGeometry.FixedOne(0.15), "the rig's answer — and ours");

            // (2) THE WRONG MIDPOINT MODE. .NET's default is round-half-to-EVEN; ECMAScript is half-up.
            decimal even = System.Math.Round(0.25m, 1, System.MidpointRounding.ToEven);
            Assert.AreEqual("0.2", even.ToString("0.0", ci), "half-to-even's answer at an exact tie");
            Assert.AreEqual("0.3", DepthRigGeometry.FixedOne(0.25), "the rig's answer — and ours");
        }

        private static void AssertRect(RigRect r, int x, int y, int w, int h, string what)
        {
            Assert.AreEqual(x, r.X, $"{what}.x");
            Assert.AreEqual(y, r.Y, $"{what}.y");
            Assert.AreEqual(w, r.W, $"{what}.w");
            Assert.AreEqual(h, r.H, $"{what}.h");
        }
    }
}
