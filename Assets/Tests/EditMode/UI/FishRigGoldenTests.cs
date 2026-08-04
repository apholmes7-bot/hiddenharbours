using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The C# fish-finder port's DRIFT GUARD (ADR 0025 S3b) — the same honest shape as
    /// <see cref="DepthRigGoldenTests"/>: a full pixel golden-master still needs the editor Canvas2D shim
    /// we are not building, so we pin the numeric geometry, the canvas dims, the procedural bottom
    /// contour, and the rig's hash noise.
    ///
    /// <para><b>The goldens are an INDEPENDENT transcription of the rig source</b>
    /// (<c>docs/art/rigs/ui/fish-finder/Art/fishRig.js</c>): every table below was computed from the JS
    /// formulas — <c>layout</c> :137-155, <c>fishGeom</c> :157-160, the standalone inset :369,
    /// <c>depthFrac</c>/<c>bottomAt</c> :239-243, <c>hashRnd</c> :223 — in float64 by a SEPARATE
    /// transcription (Python), never by running the C# under test. A bug in the port therefore cannot leak
    /// into its own expected values, and a reviewer can recompute any row from the JS and get the same
    /// number.</para>
    ///
    /// <para><b>What is deliberately NOT goldened.</b> Whole images and speckle placement:
    /// <c>hashRnd</c> feeds <c>&gt; 0.86</c> / <c>&gt; 0.8</c> booleans for decorative fuzz, so a last-ulp
    /// difference between two <c>Math.Sin</c> implementations could legitimately flip one pixel. The
    /// FUNCTION is pinned to 1e-12; where its pixels land is not a contract.</para>
    ///
    /// <para><b>Number formatting is not re-tested here</b> — <c>fishRig.js:128-130</c> is
    /// character-identical to the sounder's and the one shared copy is pinned by
    /// <c>RigNumberFormatTests</c> / <see cref="DepthRigGoldenTests"/> (Ruling B).</para>
    /// </summary>
    public class FishRigGoldenTests
    {
        private const double Tol = 1e-6;

        // ---- canvas + layout --------------------------------------------------------------------------

        [Test]
        public void CanvasDims_AndTheStandaloneInset_ArePinned()
        {
            // fishRig.js:11 (PORTRAIT — not the sounder's 480x330) and :369 (X 6% / Y 11% / W 88% / H 78%).
            Assert.AreEqual(480, FishRigGeometry.W);
            Assert.AreEqual(660, FishRigGeometry.H);
            Assert.AreEqual(480, FishRigRender.W, "render and geometry share the cell");
            Assert.AreEqual(660, FishRigRender.H);
            Assert.AreEqual(29, FishRigGeometry.InsetX);
            Assert.AreEqual(73, FishRigGeometry.InsetY);
            Assert.AreEqual(422, FishRigGeometry.InsetW);
            Assert.AreEqual(515, FishRigGeometry.InsetH);

            RigRect m = FishRigGeometry.StandaloneMount();
            Assert.AreEqual(29, m.X); Assert.AreEqual(73, m.Y);
            Assert.AreEqual(422, m.W); Assert.AreEqual(515, m.H);
        }

        [Test]
        public void TheFinderIsPortrait_AndItsConstantsAreNotTheSoundersAtADifferentSize()
        {
            // ⚠ The single likeliest way to ship a broken finder is to reuse DepthRigGeometry.Layout.
            // Same mount box, different numbers — pinned here so a "tidy-up" that merges them fails.
            FishRigLayout f = FishRigGeometry.Layout(29, 73, 422, 515);
            DepthRigLayout d = DepthRigGeometry.Layout(29, 73, 422, 515);
            Assert.AreNotEqual(d.Pad, f.Pad, "pad: 4.5% vs 4%");
            Assert.AreNotEqual(d.BrandH, f.BrandH, "brandH: 13% vs 11.5%");
            Assert.AreNotEqual(d.ColW, f.ColW, "colW: 21.5% vs 12%");
            Assert.AreNotEqual(d.Button1.Y, f.Button1.Y, "pushers: stacked vs spread");

            Assert.Less((double)FishRigGeometry.W / FishRigGeometry.H, 1.0, "the finder rig is PORTRAIT");
            Assert.Greater((double)DepthRigGeometry.W / DepthRigGeometry.H, 1.0, "the sounder is landscape");
        }

        [Test]
        public void Layout_MatchesTheRigSource_AtTheStandaloneMount()
        {
            FishRigLayout l = FishRigGeometry.Layout(29, 73, 422, 515);
            Assert.AreEqual(17, l.Pad);
            Assert.AreEqual(59, l.BrandH);
            Assert.AreEqual(51, l.ColW);
            AssertRect(l.Lcd, 46, 90, 320, 422, "lcd");
            AssertRect(l.Col, 383, 90, 51, 422, "col");
            AssertRect(l.Brand, 46, 524, 388, 59, "brand");
            AssertRect(l.Status, 46, 90, 320, 24, "status strip");
            AssertRect(l.Ruler, 329, 114, 37, 398, "ruler");
            AssertRect(l.Sonar, 46, 114, 283, 398, "sonar");
            AssertRect(l.Button0, 383, 90, 51, 37, "button 0 (MODE)");
            AssertRect(l.Button1, 383, 283, 51, 37, "button 1 (▲)");
            AssertRect(l.Button2, 383, 475, 51, 37, "button 2 (▼)");
        }

        [Test]
        public void Layout_IsBoxParameterised_AtAHalfCanvasAndAnArbitraryBox()
        {
            // The rig's inset rule applied to a half-size canvas, and an arbitrary box. (The half canvas
            // is not what the card ships as — see FishFinderOverlayTests for why the card is a scaled blit
            // of the native render — but the layout must still resolve at any size, because S6 mounts it
            // in a brow.)
            RigRect card = FishRigGeometry.StandaloneMount(240, 330);
            AssertRect(card, 14, 36, 212, 257, "card mount");
            FishRigLayout c = FishRigGeometry.Layout(card.X, card.Y, card.W, card.H);
            Assert.AreEqual(8, c.Pad);
            Assert.AreEqual(30, c.BrandH);
            Assert.AreEqual(25, c.ColW);
            AssertRect(c.Lcd, 22, 44, 163, 211, "lcd (card)");
            AssertRect(c.Status, 22, 44, 163, 12, "status (card)");
            AssertRect(c.Ruler, 166, 56, 19, 199, "ruler (card)");
            AssertRect(c.Sonar, 22, 56, 144, 199, "sonar (card)");
            AssertRect(c.Button0, 193, 44, 25, 18, "button 0 (card)");
            AssertRect(c.Button1, 193, 141, 25, 18, "button 1 (card)");
            AssertRect(c.Button2, 193, 237, 25, 18, "button 2 (card)");
            AssertRect(c.Brand, 22, 261, 196, 30, "brand (card)");

            FishRigLayout a = FishRigGeometry.Layout(10, 8, 211, 322);
            Assert.AreEqual(8, a.Pad);
            Assert.AreEqual(37, a.BrandH);
            Assert.AreEqual(25, a.ColW);
            AssertRect(a.Lcd, 18, 16, 162, 269, "lcd (arbitrary)");
            AssertRect(a.Status, 18, 16, 162, 12, "status (arbitrary)");
            AssertRect(a.Ruler, 161, 28, 19, 257, "ruler (arbitrary)");
            AssertRect(a.Sonar, 18, 28, 143, 257, "sonar (arbitrary)");
            AssertRect(a.Button0, 188, 16, 25, 18, "button 0 (arbitrary)");
            AssertRect(a.Button1, 188, 142, 25, 18, "button 1 (arbitrary)");
            AssertRect(a.Button2, 188, 267, 25, 18, "button 2 (arbitrary)");
        }

        [Test]
        public void Layout_InAWideConsoleBrow_StillResolves_AndTheSonarIsSquashed()
        {
            // The S6 mount case, pinned so the squash is a measured fact rather than a surprise: the same
            // layout in a 300x120 brow leaves a 202x65 sonar — 3:1, against the portrait rig's 283x398.
            // "drawUnit fits any box" is a GEOMETRY claim; the eyeball proof is what judges the look.
            FishRigLayout b = FishRigGeometry.Layout(0, 0, 300, 120);
            Assert.AreEqual(12, b.Pad);
            Assert.AreEqual(14, b.BrandH);
            Assert.AreEqual(36, b.ColW);
            AssertRect(b.Lcd, 12, 12, 228, 82, "lcd (brow)");
            AssertRect(b.Status, 12, 12, 228, 17, "status (brow)");
            AssertRect(b.Ruler, 214, 29, 26, 65, "ruler (brow)");
            AssertRect(b.Sonar, 12, 29, 202, 65, "sonar (brow)");
            AssertRect(b.Button0, 252, 12, 36, 23, "button 0 (brow)");
            AssertRect(b.Button1, 252, 42, 36, 23, "button 1 (brow)");
            AssertRect(b.Button2, 252, 71, 36, 23, "button 2 (brow)");

            double portrait = (double)283 / 398, brow = (double)b.Sonar.W / b.Sonar.H;
            Assert.Less(portrait, 1.0, "the standalone sonar is taller than it is wide");
            Assert.Greater(brow, 3.0, "…and a wide brow makes it a letterbox");
        }

        [Test]
        public void TheThreePushers_AreDisjoint_AndTheGlassRegionsTile()
        {
            FishRigLayout l = FishRigGeometry.Layout(29, 73, 422, 515);
            Assert.Less(l.Button0.Y + l.Button0.H, l.Button1.Y, "no overlap between pushers 0 and 1");
            Assert.Less(l.Button1.Y + l.Button1.H, l.Button2.Y, "nor 1 and 2");
            Assert.LessOrEqual(l.Button2.Y + l.Button2.H, l.Col.Y + l.Col.H, "…and 2 stays in the column");
            Assert.LessOrEqual(l.Lcd.X + l.Lcd.W, l.Col.X, "the glass never runs under the pusher column");

            // status + (sonar | ruler) exactly cover the glass — nothing is unreachable, nothing overlaps.
            Assert.AreEqual(l.Lcd.H, l.Status.H + l.Sonar.H, "status and the panes tile the glass");
            Assert.AreEqual(l.Lcd.W, l.Sonar.W + l.Ruler.W, "sonar and ruler tile the glass's width");
            Assert.AreEqual(l.Sonar.X + l.Sonar.W, l.Ruler.X, "…side by side, with no gap");
            Assert.AreEqual(l.Sonar.Y, l.Ruler.Y);
            Assert.AreEqual(l.Sonar.H, l.Ruler.H);

            // A hit lands on exactly one thing — the host's whole interaction rests on this.
            Assert.IsTrue(l.Button1.Contains(l.Button1.X + 1, l.Button1.Y + 1));
            Assert.IsFalse(l.Sonar.Contains(l.Button1.X + 1, l.Button1.Y + 1));
            Assert.IsTrue(l.Sonar.Contains(l.Sonar.X + 1, l.Sonar.Y + 1));
            Assert.IsFalse(l.Status.Contains(l.Sonar.X + 1, l.Sonar.Y + 1));
            Assert.IsFalse(l.Ruler.Contains(l.Sonar.X + l.Sonar.W - 1, l.Sonar.Y + 1));
        }

        // ---- fishGeom ---------------------------------------------------------------------------------

        [Test]
        public void FishGeom_MatchesTheRigSource()
        {
            FishRigLayout l = FishRigGeometry.Layout(29, 73, 422, 515);   // sonar 46,114,283,398
            // The rig's own defaultSchool() (fishRig.js:132-134) plus both corners.
            Check(in l.Sonar, 0.60, 0.15, 0.85, 215.79999999999998, 173.7, 14.433);
            Check(in l.Sonar, 0.52, 0.285, 1.15, 193.16, 227.43, 19.526999999999997);
            Check(in l.Sonar, 0.76, 0.41, 1.0, 261.08000000000004, 277.17999999999995, 16.98);
            Check(in l.Sonar, 0.30, 0.52, 1.35, 130.89999999999998, 320.96000000000004, 22.923000000000002);
            Check(in l.Sonar, 0.0, 0.0, 1.0, 46.0, 114.0, 16.98);
            Check(in l.Sonar, 1.0, 1.0, 2.5, 329.0, 512.0, 42.45);

            // …and at the card size, where the 4 px FLOOR on the half-size starts to bite.
            RigRect card = FishRigGeometry.StandaloneMount(240, 330);
            FishRigLayout c = FishRigGeometry.Layout(card.X, card.Y, card.W, card.H);   // sonar 22,56,144,199
            Check(in c.Sonar, 0.60, 0.15, 0.85, 108.39999999999999, 85.85, 7.344);
            Check(in c.Sonar, 0.30, 0.52, 1.35, 65.19999999999999, 159.48000000000002, 11.664000000000001);
            Check(in c.Sonar, 0.5, 0.5, 0.5, 94.0, 155.5, 4.32);
        }

        [Test]
        public void FishGeom_FloorsTheHalfSizeBeforeTheMultiplier_NotAfter()
        {
            // fishRig.js:159 is max(4, min(w,h)*0.06) * size — so a tiny box floors at 4 and a small
            // `size` then scales BELOW 4. Getting the order wrong makes every mark on a card-size glass
            // the same size. 12x12 sonar: min*0.06 = 0.72 → floored to 4 → x0.5 = 2.
            var tiny = new RigRect(0, 0, 12, 12);
            FishRigGeometry.FishGeom(in tiny, 0.5, 0.5, 0.5, out _, out _, out double half);
            Assert.AreEqual(2.0, half, Tol);
            FishRigGeometry.FishGeom(in tiny, 0.5, 0.5, 2.0, out _, out _, out double big);
            Assert.AreEqual(8.0, big, Tol);
        }

        // ---- the procedural contour -------------------------------------------------------------------

        [Test]
        public void DepthFrac_MatchesTheRigSource_IncludingBothClampArms()
        {
            Assert.AreEqual(0.6799999999999999, FishRigGeometry.DepthFrac(13.6, 20), Tol);
            Assert.AreEqual(0.05, FishRigGeometry.DepthFrac(0.0, 20), Tol, "clamped at the surface");
            Assert.AreEqual(0.05, FishRigGeometry.DepthFrac(0.5, 20), Tol);
            Assert.AreEqual(0.985, FishRigGeometry.DepthFrac(19.7, 20), Tol, "clamped at the bottom");
            Assert.AreEqual(0.985, FishRigGeometry.DepthFrac(25.0, 20), Tol, "…and past it");
            Assert.AreEqual(0.985, FishRigGeometry.DepthFrac(13.6, 10), Tol);
            Assert.AreEqual(0.33999999999999997, FishRigGeometry.DepthFrac(13.6, 40), Tol);
            Assert.AreEqual(0.22666666666666666, FishRigGeometry.DepthFrac(13.6, 60), Tol);
            Assert.AreEqual(0.05, FishRigGeometry.DepthFrac(1.0, 60), Tol);
        }

        [Test]
        public void BottomAt_MatchesTheRigSource_AcrossPhasesAndPositions()
        {
            // depth 13.6 m on the 20 m scale — the rig's own default state (fishRig.js:316-317).
            double df = FishRigGeometry.DepthFrac(13.6, 20);
            var rows = new[]
            {
                new[] { 0.0,  0.0,    0.6799999999999999 },
                new[] { 0.25, 0.0,    0.7197594744812342 },
                new[] { 0.5,  0.0,    0.6689913675260792 },
                new[] { 0.75, 0.0,    0.6033184468530265 },
                new[] { 1.0,  0.0,    0.6933793033735015 },
                new[] { 0.0,  0.125,  0.6848798067986477 },
                new[] { 0.25, 0.125,  0.7266310511449784 },
                new[] { 0.5,  0.125,  0.6607920552873507 },
                new[] { 0.75, 0.125,  0.6049226154420647 },
                new[] { 1.0,  0.125,  0.6905149411825583 },
                new[] { 0.0,  1.0,    0.7054963775209385 },
                new[] { 0.5,  1.0,    0.6275803636120897 },
                new[] { 1.0,  1.0,    0.6827541138971945 },
                new[] { 0.0,  3.5,    0.7364281126308744 },
                new[] { 0.25, 3.5,    0.6182450348219698 },
                new[] { 0.75, 3.5,    0.7010233090165144 },
                new[] { 0.0,  12.75,  0.6710022491091041 },
                new[] { 0.5,  12.75,  0.626479978408415 },
                new[] { 1.0,  12.75,  0.7075108738367407 },
            };
            foreach (double[] r in rows)
                Assert.AreEqual(r[2], FishRigGeometry.BottomAt(r[0], r[1], df), Tol,
                                $"bottomAt(u={r[0]}, phase={r[1]}) — fishRig.js:240-243");
        }

        [Test]
        public void BottomAt_ClampsIntoTheGlass_AtBothEnds()
        {
            double shallow = FishRigGeometry.DepthFrac(0.0, 20);      // 0.05
            Assert.AreEqual(0.06, FishRigGeometry.BottomAt(0.0, 0.0, shallow), Tol, "floored at 0.06");
            Assert.AreEqual(0.08975947448123425, FishRigGeometry.BottomAt(0.25, 0.0, shallow), Tol);
            double deep = FishRigGeometry.DepthFrac(19.9, 20);        // 0.985
            Assert.AreEqual(0.985, FishRigGeometry.BottomAt(0.0, 0.0, deep), Tol);
            Assert.AreEqual(0.9155758107110169, FishRigGeometry.BottomAt(0.5, 2.0, deep), Tol);
            for (int i = 0; i <= 40; i++)
            {
                double u = i / 40.0;
                double v = FishRigGeometry.BottomAt(u, 7.3, deep);
                Assert.GreaterOrEqual(v, 0.06);
                Assert.LessOrEqual(v, 0.995, "the contour never leaves the glass");
            }
        }

        [Test]
        public void TheContour_HasNoHistory_ItIsAPureFunctionOfDepthAndPhase()
        {
            // ⚠ The "waterfall history" premise is wrong and this is the pin: fishRig.js keeps NOTHING
            // between frames. Two hosts at the same (u, phase, depth) MUST agree, and returning to an
            // earlier phase must reproduce the earlier picture exactly — which a ring buffer could not do.
            double df = FishRigGeometry.DepthFrac(9.0, 20);
            double a = FishRigGeometry.BottomAt(0.4, 2.5, df);
            _ = FishRigGeometry.BottomAt(0.4, 99.0, df);
            double b = FishRigGeometry.BottomAt(0.4, 2.5, df);
            Assert.AreEqual(a, b, 0d, "the contour is stateless — bit-identical on re-evaluation");
        }

        [Test]
        public void HashRnd_MatchesTheRigSource_ToOneInATrillion()
        {
            // Pinned to 1e-12 (the brief's bar). It only ever feeds `> 0.86` / `> 0.8` booleans for
            // decorative speckle, so pixel placement is deliberately NOT goldened — see the class doc.
            var rows = new[]
            {
                new[] { 0.0,    0.26163105465093395 },
                new[] { 1.0,    0.9822420553282427 },
                new[] { 2.0,    0.2403642617719015 },
                new[] { 7.0,    0.47376382717629895 },
                new[] { 13.0,   0.2734850998604088 },
                new[] { 42.0,   0.0936488463630667 },
                new[] { 100.0,  0.5630340742918634 },
                new[] { 255.0,  0.38094245768297696 },
                new[] { 1024.0, 0.43448522880498786 },
                new[] { 0.5,    0.5168349076120649 },
                new[] { 2.3,    0.04644884726440068 },
                new[] { 5.1,    0.8053075851494214 },
                new[] { 1.7,    0.8568549104675185 },
            };
            foreach (double[] r in rows)
                Assert.AreEqual(r[1], FishRigGeometry.HashRnd(r[0]), 1e-12,
                                $"hashRnd({r[0]}) — fishRig.js:223");

            for (int i = 0; i < 400; i++)
            {
                double v = FishRigGeometry.HashRnd(i * 3.7 + 0.9);
                Assert.GreaterOrEqual(v, 0.0);
                Assert.Less(v, 1.0, "hashRnd is a unit fraction");
            }
        }

        [Test]
        public void EveryCharacterTheFinderDraws_HasAGlyphInTheSharedFont()
        {
            // ⚠ The port reads its 3×5 font from RigDrawUtil, which carries consoleRig.js's SUBSET — J, Q,
            // X and Z are absent and an unknown character renders as a BLANK, silently. None of the
            // finder's own strings need them today (the mode legends, the wordmark, the units, the
            // numbers); this is what keeps a future mode label from losing a letter with no error.
            const string used = "MODE RANGE ALARM FF-2 HARBOUR SONAR FT M C V S T 0123456789.";
            var surface = new DrawSurface(24, 12);
            foreach (char ch in used)
            {
                if (ch == ' ') continue;
                surface.Clear();
                RigDrawUtil.Text(surface, ch.ToString(), 1, 1, 2, new Color32(255, 255, 255, 255));
                int lit = 0;
                for (int i = 0; i < surface.Pixels.Length; i++) if (surface.Pixels[i].a != 0) lit++;
                Assert.Greater(lit, 0, $"'{ch}' has no glyph in the shared font — it would draw as a blank");
            }
        }

        // ---- the RANGE ladder --------------------------------------------------------------------------

        [Test]
        public void RangeSteps_AreTheRigsOwn_AndStepClampsAtBothEnds()
        {
            CollectionAssert.AreEqual(new[] { 10f, 20f, 40f, 60f }, FishRigGeometry.RangeSteps,
                                      "fishRig.js:131");
            Assert.AreEqual(40f, FishRigGeometry.StepRange(20f, +1));
            Assert.AreEqual(10f, FishRigGeometry.StepRange(20f, -1));
            Assert.AreEqual(10f, FishRigGeometry.StepRange(10f, -1), "clamped at the shallowest scale");
            Assert.AreEqual(60f, FishRigGeometry.StepRange(60f, +1), "clamped at the deepest");
            Assert.AreEqual(60f, FishRigGeometry.StepRange(20f, +9));
            // An off-ladder value (an old save, an owner default off the steps) snaps to the NEAREST first.
            Assert.AreEqual(40f, FishRigGeometry.StepRange(22f, +1), "22 is nearest 20 → next is 40");
            Assert.AreEqual(20f, FishRigGeometry.StepRange(35f, -1), "35 is nearest 40 → previous is 20");
        }

        [Test]
        public void SafeRange_HealsTheOneValueThatMakesTheGlassGarbage()
        {
            // depthFrac is depth / range. At zero that is Inf or NaN, not "a small picture".
            Assert.AreEqual(20f, FishRigGeometry.SafeRange(0f, 20f));
            Assert.AreEqual(20f, FishRigGeometry.SafeRange(-5f, 20f));
            Assert.AreEqual(20f, FishRigGeometry.SafeRange(float.PositiveInfinity, 20f));
            Assert.AreEqual(20f, FishRigGeometry.SafeRange(float.NaN, 20f), "NaN is not > 0");
            Assert.AreEqual(40f, FishRigGeometry.SafeRange(40f, 20f), "a good range is untouched");
            Assert.AreEqual(20f, FishRigGeometry.SafeRange(0f, 0f), "…even a broken fallback lands somewhere");
            // And the thing it is protecting against, stated: an unhealed zero really is NaN.
            Assert.IsTrue(double.IsNaN(FishRigGeometry.DepthFrac(0.0, 0.0)),
                          "0/0 is NaN — this is why SafeRange exists");
        }

        // ---- NEGATIVE CONTROL: prove the goldens can fail ------------------------------------------------

        [Test]
        public void NegativeControl_PerturbingEitherContourConstant_MovesTheTable()
        {
            // Re-derive bottomAt with the FIRST train's amplitude off by 1% and with its frequency off by
            // 1%, and confirm both move the goldened rows. If they didn't, the contour table would be
            // pinning nothing.
            double df = FishRigGeometry.DepthFrac(13.6, 20);
            var us = new[] { 0.0, 0.25, 0.5, 0.75, 1.0 };
            int movedAmp = 0, movedFreq = 0;
            foreach (double u in us)
            {
                double good = FishRigGeometry.BottomAt(u, 1.0, df);
                if (System.Math.Abs(Perturbed(u, 1.0, df, 0.055 * 1.01, 6.3) - good) > 1e-9) movedAmp++;
                if (System.Math.Abs(Perturbed(u, 1.0, df, 0.055, 6.3 * 1.01) - good) > 1e-9) movedFreq++;
            }
            Assert.GreaterOrEqual(movedAmp, us.Length - 1, "a 1% amplitude error must show across the row");
            Assert.GreaterOrEqual(movedFreq, us.Length - 1, "and so must a 1% frequency error");
        }

        [Test]
        public void NegativeControl_TheSoundersLayout_DoesNotSatisfyTheFindersGoldens()
        {
            // The concrete mistake this file exists to catch: DepthRigGeometry.Layout at the finder's own
            // mount produces DIFFERENT rects, so a copy-pasted layout fails loudly rather than drawing
            // pushers where they are not clicked.
            DepthRigLayout d = DepthRigGeometry.Layout(29, 73, 422, 515);
            Assert.AreNotEqual(46, d.Lcd.X, "the sounder's glass does not start where the finder's does");
            Assert.IsFalse(d.Lcd.W == 320 && d.Lcd.H == 422, "nor is it the same size");
            Assert.AreNotEqual(383, d.Button1.X);
        }

        private static double Perturbed(double u, double ph, double df, double amp, double freq)
        {
            double w = amp * System.Math.Sin(u * freq + ph * 0.7)
                     + 0.03 * System.Math.Sin(u * 13.0 - ph * 1.05)
                     + 0.017 * System.Math.Sin(u * 22.0 + ph * 1.9);
            return FishRigGeometry.Clamp(df + w, 0.06, 0.995);
        }

        private static void Check(in RigRect sonar, double x01, double y01, double size,
                                  double cx, double cy, double half)
        {
            FishRigGeometry.FishGeom(in sonar, x01, y01, size, out double gx, out double gy, out double gh);
            Assert.AreEqual(cx, gx, Tol, $"fishGeom({x01},{y01},{size}).cx");
            Assert.AreEqual(cy, gy, Tol, $"fishGeom({x01},{y01},{size}).cy");
            Assert.AreEqual(half, gh, Tol, $"fishGeom({x01},{y01},{size}).half");
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
