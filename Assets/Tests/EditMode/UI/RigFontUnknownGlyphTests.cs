using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// THE LOUD-FONT GUARD — a character the 3×5 table does not carry must never render as nothing.
    ///
    /// <para><b>The defect class this pins.</b> The rig sources resolve a glyph as
    /// <c>F[ch] || F[' ']</c> (consoleRig.js:99), and the port carried that idiom faithfully — which
    /// is how both shipped skiff dashes drew their "X100" tach label as " 100" for two slices while
    /// every test stayed green (S4 added the missing J/Q/X/Z but kept the silent fallback). S6's
    /// chartplotter (navRig.js:77, the same idiom) will push waypoint text through this path with
    /// characters nobody has audited. So an unknown character now rasterises as a SOLID tofu block
    /// (unmissable on the glass, unmistakable for an authored label) and raises one
    /// <c>Debug.LogError</c> per distinct character — which the test framework escalates to a
    /// failure in ANY EditMode test that repaints an offending label, golden tests included, while a
    /// player build keeps its helm: tofu on screen, an error in the log, no throw out of the draw
    /// path mid-repaint.</para>
    ///
    /// <para><b>The sabotage arms are the point</b> (the <c>FishSchoolHonestyTests</c> pattern): a
    /// visibility predicate that cannot condemn the old blank — or a polite-looking substitute such
    /// as '-' — proves nothing, so both liars are rendered for real through the shipping renderer
    /// and the predicate is REQUIRED to throw on them.</para>
    ///
    /// <para><b>Probe characters.</b> The arms probe U+0001/U+0002 — control characters that can
    /// never earn a glyph, so no future table addition can quietly turn these tests vacuous. The one
    /// plausible-future character (':') gets its own case; the day a slice adds ':' to the table,
    /// that case goes red on purpose — re-point its probe at a character still outside the table (or
    /// drop the case; the control-character arms carry the invariant).</para>
    /// </summary>
    public class RigFontUnknownGlyphTests
    {
        // One glyph cell at scale 2 (6×10 px) drawn well inside a small surface, so the predicate
        // can walk every pixel of the cell.
        private const int X = 3, Y = 4, Scale = 2;
        private static readonly Color32 Ink = new Color32(240, 200, 40, 255);

        private static DrawSurface Draw(string text)
        {
            var s = new DrawSurface(32, 24);
            RigDrawUtil.Text(s, text, X, Y, Scale, Ink);
            return s;
        }

        // The once-per-character dedup is static state that survives consecutive EditMode runs in one
        // editor session; re-arm both ways so no test here depends on being a character's first
        // sighting, and no character this fixture taints can silence a later run's expectation.
        [SetUp]
        public void ReArm() => RigDrawUtil.ResetUnknownGlyphReports();

        [TearDown]
        public void LeaveReArmed() => RigDrawUtil.ResetUnknownGlyphReports();

        /// <summary>THE PREDICATE both directions run through: every pixel of the probe's 3×5 cell
        /// is solid ink. Not "some ink" — a substitute with a plausible shape must fail exactly like
        /// a blank ('-' in a tach label would read "-100", which looks authored and is therefore
        /// worse than the blank it replaced).</summary>
        private static void AssertTheCellIsSolidTofu(DrawSurface s)
        {
            for (int py = 0; py < 5 * Scale; py++)
            for (int px = 0; px < 3 * Scale; px++)
            {
                Color32 got = s.Pixels[(Y + py) * s.Width + (X + px)];
                if (got.r != Ink.r || got.g != Ink.g || got.b != Ink.b || got.a != Ink.a)
                    Assert.Fail($"pixel ({px},{py}) of the glyph cell is not ink (rgba {got.r}," +
                                $"{got.g},{got.b},{got.a}) — an unknown character must render as a " +
                                "SOLID tofu block, never a blank or a plausible-looking substitute");
            }
        }

        // ---- the guard --------------------------------------------------------------------------------

        [Test]
        public void AnUnglyphableCharacter_DrawsSolidTofu_AndReportsAsAnError()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"U\+0001"));
            AssertTheCellIsSolidTofu(Draw("\u0001"));
        }

        [Test]
        public void ThePlausibleFutureCharacter_Colon_IsLoudUntilAGlyphIsActuallyAdded()
        {
            // S6's waypoint labels will plausibly want ':'; until the slice that needs it ADDS it,
            // the character must be tofu + error, never a quiet gap.
            LogAssert.Expect(LogType.Error, new Regex(@"U\+003A"));
            AssertTheCellIsSolidTofu(Draw(":"));
        }

        [Test]
        public void TheReport_FiresOncePerCharacter_NotPerRepaint()
        {
            // The draw path runs per repaint; the console must not. Three strikes of the same unknown
            // character across two Text calls may reach the log exactly once — a second error would
            // be an unhandled log message and fails this test on its own.
            LogAssert.Expect(LogType.Error, new Regex(@"U\+0002"));
            DrawSurface s = Draw("\u0002\u0002");
            AssertTheCellIsSolidTofu(s);                          // the TOFU is every draw…
            RigDrawUtil.Text(s, "\u0002", X, Y, Scale, Ink);      // …only the REPORT deduplicates
            AssertTheCellIsSolidTofu(s);

            // And the dedup is exactly the set the reset hook clears — the property this fixture's
            // own SetUp leans on.
            RigDrawUtil.ResetUnknownGlyphReports();
            LogAssert.Expect(LogType.Error, new Regex(@"U\+0002"));
            RigDrawUtil.Text(s, "\u0002", X, Y, Scale, Ink);
        }

        // ---- the table path must be exactly as it was -------------------------------------------------

        [Test]
        public void KnownGlyphs_AreUntouched_AndTheTwoAuthoredBlanksStayQuiet()
        {
            // ' ' is IN the table: a real space draws nothing and reports nothing. '·' likewise —
            // the wheelhouse standby plates author a U+00B7 separator (noviRig.js:359,
            // capeRig.js:348) that the sources' own fonts render as a gap, so the gap is pinned as
            // an AUTHORED glyph, not left to the fallback. No LogAssert.Expect in this test: any
            // report from these characters is an unexpected error and fails it.
            foreach (string authored in new[] { " ", "·" })
            {
                DrawSurface blank = Draw(authored);
                foreach (Color32 p in blank.Pixels)
                    Assert.AreEqual(0, p.a,
                        $"\"{authored}\" (U+{(int)authored[0]:X4}) legitimately draws nothing");
            }

            // And a table glyph still rasterises its exact pattern (consoleRig.js:75-104): 'I' at
            // scale 2, sampled cell-by-cell against its own row strings.
            DrawSurface s = Draw("I");
            string[] rows = { "###", ".#.", ".#.", ".#.", "###" };
            for (int r = 0; r < 5; r++)
            for (int c = 0; c < 3; c++)
            {
                bool lit = s.Pixels[(Y + r * Scale) * s.Width + (X + c * Scale)].a != 0;
                Assert.AreEqual(rows[r][c] == '#', lit, $"'I' cell ({c},{r})");
            }
        }

        // ---- the sabotage arms: the predicate must be ABLE to catch the liars -------------------------

        [Test]
        public void TheOldSilentBlank_IsCaught()
        {
            // The exact pre-guard defect, reproduced through the shipping renderer: the old fallback
            // returned the ' ' glyph for an unknown character, so drawing ' ' is byte-for-byte what
            // "X100"'s X rendered as for two shipped slices. The predicate is REQUIRED to condemn it.
            DrawSurface s = Draw(" ");
            Assert.Throws<AssertionException>(() => AssertTheCellIsSolidTofu(s),
                "SABOTAGE NOT DETECTED — the visibility predicate no longer condemns a blanked " +
                "glyph, so every tofu assertion in this fixture is pinning nothing");
        }

        [Test]
        public void APoliteLookingSubstitute_IsCaught()
        {
            // The subtler liar: unknown → some REAL glyph ('-', say) is visible but not obviously
            // wrong — "X100" would read "-100", which looks authored. Some ink is not the bar; the
            // full block is.
            DrawSurface s = Draw("-");
            Assert.Throws<AssertionException>(() => AssertTheCellIsSolidTofu(s),
                "SABOTAGE NOT DETECTED — the predicate passes a substitute that merely has some " +
                "ink, so a 'tidier' fallback could ship a plausible-looking wrong label again");
        }
    }
}
