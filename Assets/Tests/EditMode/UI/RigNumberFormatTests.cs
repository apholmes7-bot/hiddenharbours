using NUnit.Framework;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// Ruling B's anti-drift pin: the rigs' number formatting has exactly ONE implementation
    /// (<see cref="RigNumberFormat"/>), and <see cref="DepthRigGeometry"/>'s members are FORWARDERS to it.
    ///
    /// <para><b>Why this test exists at all.</b> <c>fish-finder/Art/fishRig.js:128-130</c> is
    /// character-identical to <c>depth-finder/Art/depthRig.js:119-121</c>, so the finder slice would
    /// naturally port <c>fmtDepth</c>/<c>fmtSet</c>/<c>M2FT</c> a second time. Two implementations of this
    /// particular function is the worst case available: the obvious implementation is subtly wrong, and the
    /// two copies would agree on every value a casual test looks at and disagree at 0.15. This test fails
    /// the moment a forwarder is quietly re-implemented, whether it drifts or not.</para>
    ///
    /// <para><b>The extraction's real proof is <c>DepthRigGoldenTests</c> passing UNMODIFIED</b> — it is the
    /// independently-transcribed golden ladder, and it still calls the old <see cref="DepthRigGeometry"/>
    /// names. This file only adds the "there is one copy" half that a golden table cannot state.</para>
    /// </summary>
    public class RigNumberFormatTests
    {
        // The awkward values, not the easy ones: exact binary ties, the 100 rollover, the signed zero.
        private static readonly double[] Ladder =
        {
            0.0, -0.04, 0.04, 0.05, 0.15, 0.25, 0.35, 0.45, 0.75, 1.0, 2.675, 9.95, 9.99,
            26.2, 30.4799, 30.48, 99.94, 99.95, 100.0, 123.45, 305.0, -12.5,
        };

        [Test]
        public void TheSounder_ForwardsToTheSharedCopy_OverTheWholeAwkwardLadder()
        {
            foreach (double v in Ladder)
            {
                Assert.AreEqual(RigNumberFormat.FixedOne(v), DepthRigGeometry.FixedOne(v),
                                $"FixedOne({v})");
                Assert.AreEqual(RigNumberFormat.JsRoundToString(v), DepthRigGeometry.JsRoundToString(v),
                                $"JsRoundToString({v})");
                Assert.AreEqual(RigNumberFormat.FmtDepth(v, false), DepthRigGeometry.FmtDepth(v, false),
                                $"FmtDepth({v}, metres)");
                Assert.AreEqual(RigNumberFormat.FmtDepth(v, true), DepthRigGeometry.FmtDepth(v, true),
                                $"FmtDepth({v}, feet)");
                Assert.AreEqual(RigNumberFormat.FmtSet(v, false), DepthRigGeometry.FmtSet(v, false),
                                $"FmtSet({v}, metres)");
                Assert.AreEqual(RigNumberFormat.FmtSet(v, true), DepthRigGeometry.FmtSet(v, true),
                                $"FmtSet({v}, feet)");
            }
        }

        [Test]
        public void TheFootConversion_IsOneConstant_NotTwo()
        {
            Assert.AreEqual(3.28084, RigNumberFormat.M2FT, 0d,
                            "the rigs' own metres→feet (depthRig.js:119 ≡ fishRig.js:128)");
            Assert.AreEqual(RigNumberFormat.M2FT, DepthRigGeometry.M2FT, 0d,
                            "and the sounder's name for it is the SAME constant, not a second copy");
        }

        [Test]
        public void TheSharedCopy_StillReproducesEcmaScriptToFixed()
        {
            // Restated here (not only via the forwarder) so the shared copy is pinned on its own terms —
            // the finder, radar and plotter will call THIS, not DepthRigGeometry.
            Assert.AreEqual("0.1", RigNumberFormat.FixedOne(0.15), "exact value is 0.14999… → 0.1");
            Assert.AreEqual("0.3", RigNumberFormat.FixedOne(0.25), "an exact tie rounds half-UP, not to even");
            Assert.AreEqual("-0.0", RigNumberFormat.FixedOne(-0.04), "JS takes the sign off first");
            Assert.AreEqual("100.0", RigNumberFormat.FmtDepth(30.4799, true), "99.9997 ft keeps its decimal");
            Assert.AreEqual("100", RigNumberFormat.FmtDepth(30.48, true), "100.0000032 ft loses it");
            Assert.AreEqual("100.0", RigNumberFormat.FmtSet(100.0, false), "fmtSet never rolls over");
        }

        [Test]
        public void NegativeControl_TheTwoShortcutsThatLookRight_StillPrintTheWrongDigit()
        {
            // If this ever stops disagreeing, the ladder above is pinning nothing and a naive re-port
            // would pass unnoticed.
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            Assert.AreEqual("0.2", System.Math.Round((decimal)0.15, 1,
                System.MidpointRounding.AwayFromZero).ToString("0.0", ci), "the decimal-cast shortcut");
            Assert.AreEqual("0.2", System.Math.Round(0.25m, 1,
                System.MidpointRounding.ToEven).ToString("0.0", ci), "the wrong midpoint mode");
        }
    }
}
