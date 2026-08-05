using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The rule-7 REPAINT COST measurement for all four helm dashes, taken headlessly (ADR 0025; the
    /// merge-seat's route past "no Unity machine here", PR #410).
    ///
    /// <para><b>Why this can run in EditMode at all.</b> <see cref="DrawSurface"/> is a
    /// <c>Color32[]</c> CPU buffer; <c>Texture2D</c> appears only in <c>ToTexture</c>, the upload. On
    /// S3b the split was 4.507 ms raster / 0.282 ms upload — ~94% of the cost is pure CPU, and it is
    /// exactly the part that scales with what these two new dashes added (the Cape's 1250 cork motes
    /// and 40 grain streaks, the Novi's 4-px carbon twill). So this times the RASTER only.</para>
    ///
    /// <para><b>Why all four in one run.</b> A single figure from one machine says little. Measuring
    /// the shipped skiffs and the new wheelhouses in the same run, on the same runner, under the same
    /// conditions makes the RATIO trustworthy even though the absolute numbers move with the hardware
    /// — better evidence than comparing two numbers taken months and machines apart.</para>
    ///
    /// <para><b>What this asserts, and what it does not.</b> CI runners are noisy and shared, so the
    /// numbers are LOGGED, not gated on a tight threshold — a timing test that fails on a busy runner
    /// trains people to ignore red. The only assertion is an absurdly loose ceiling that catches a
    /// pathological regression (an accidental O(n²), an unbounded loop) and nothing else.</para>
    ///
    /// <para><b>Reading the number.</b> Unlike the fish finder's free-running scan, a dash repaints on
    /// CHANGE DETECTION — a detent, a gear, a fit change — never per frame. A raster dearer than the
    /// skiffs' is therefore paid per state change, not per frame, which is why a figure that would
    /// have been unacceptable for the sonar can be fine here.</para>
    /// </summary>
    public class HelmDashRepaintCostTests
    {
        private const int Iterations = 50;
        private const float Drive = 0.5f, Rpm = 0.555f, Fuel = 0.62f;

        /// <summary>An absurdly loose ceiling — a smoke guard, not a budget. A dash that takes a third
        /// of a second to raster is broken, not merely slow; anything under it is a number to read.</summary>
        private const double SanityCeilingMs = 300.0;

        private static double MsPer(System.Action a)
        {
            a();                                   // warm: static ramps, the baked key, JIT
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++) a();
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds / Iterations;
        }

        [Test]
        public void AllFourDashes_RasterCost_IsMeasuredAndLogged()
        {
            var skiff = new DrawSurface(HelmDashGeometry.W, HelmDashGeometry.H);
            var pilot = new DrawSurface(HelmDashGeometry.PilotW, HelmDashGeometry.PilotH);

            // The SHIPPED fit of both wheelhouse hulls: a depth sounder, no compass, no radar, no gps.
            var noviFit = new HelmFit(ConsoleRigKind.Novi, SounderKind.Depth, CompassMount.None, false, false);
            var capeFit = new HelmFit(ConsoleRigKind.Cape, SounderKind.Depth, CompassMount.None, false, false);

            double console = MsPer(() => ConsoleDashRender.Render(skiff, true, Drive, Rpm, Fuel));
            double sport = MsPer(() => SportDashRender.Render(skiff, true, Drive, Rpm, Fuel));
            double novi = MsPer(() => NoviDashRender.Render(pilot, in noviFit, true, Rpm, Fuel));
            double cape = MsPer(() => CapeDashRender.Render(pilot, in capeFit, true, Rpm, Fuel));

            // Night costs extra on the wheelhouses only (the gauge backlights + the ice ambient wash).
            double noviNight = MsPer(() => NoviDashRender.Render(pilot, in noviFit, true, Rpm, Fuel, true));
            double capeNight = MsPer(() => CapeDashRender.Render(pilot, in capeFit, true, Rpm, Fuel, true));

            double skiffAvg = (console + sport) / 2.0;
            double pilotAvg = (novi + cape) / 2.0;
            string report =
                $"[HelmDashRepaintCost] raster ms/repaint, avg over {Iterations}, EditMode CPU, no GPU:\n" +
                $"  console  600x510  {console:F2} ms\n" +
                $"  sport    600x510  {sport:F2} ms\n" +
                $"  novi     600x548  {novi:F2} ms   (night {noviNight:F2} ms)\n" +
                $"  cape     600x548  {cape:F2} ms   (night {capeNight:F2} ms)\n" +
                $"  pilothouse / skiff ratio = {pilotAvg / skiffAvg:F2}x  " +
                $"(skiff avg {skiffAvg:F2} ms, pilothouse avg {pilotAvg:F2} ms)\n" +
                "  NOTE: dashes repaint on change detection (detent / gear / fit), never per frame.";
            TestContext.WriteLine(report);
            Debug.Log(report);

            Assert.That(console, Is.LessThan(SanityCeilingMs), "console dash raster");
            Assert.That(sport, Is.LessThan(SanityCeilingMs), "sport dash raster");
            Assert.That(novi, Is.LessThan(SanityCeilingMs), "novi dash raster");
            Assert.That(cape, Is.LessThan(SanityCeilingMs), "cape dash raster");
        }

        /// <summary>
        /// S4.5: the flush brow faces' cell rasters, measured at every mount box the fleet has. The
        /// depth face repaints only when an LCD string moves (still water ⇒ roughly never); the fish
        /// face repaints on the finder's WaterfallHz bucket (4/s shipped), so its number here is a
        /// per-scan-step cost, not a per-frame one — and at these mount sizes it is a fraction of the
        /// standalone card's measured ~4.8 ms at 480×660.
        /// </summary>
        [Test]
        public void FlushBrowFaces_RasterCost_IsMeasuredAndLogged()
        {
            var depthState = new DepthRigState(depth: 12.3f, feet: false, night: false, armed: true,
                                               alarm: 3f, tempC: 12f, blink: false);
            var fishState = new FishRigState(
                12.3f, 12f, 20f, 3f, false, false, true, true, false, false,
                0.25, FishRigAdjust.Range, 0.75f, true, 0.8f, 4f);
            var noMarks = new System.Collections.Generic.List<SonarMark>();

            HelmDashGeometry.SounderCutout(false, out _, out _, out int dw, out int dh);
            HelmDashGeometry.FinderCutout(false, out _, out _, out int fw, out int fh);
            HelmDashGeometry.SlotBoxOnCard(HelmDashGeometry.PilotSounderSlot, false,
                                           out _, out _, out int sw, out int sh);
            HelmDashGeometry.SlotBoxOnCard(HelmDashGeometry.PilotSounderSlot, true,
                                           out _, out _, out int pw, out int ph);

            var skiffDepth = new DrawSurface(dw, dh);
            var skiffFish = new DrawSurface(fw, fh);
            var pilotDepth = new DrawSurface(sw, sh);
            var pilotFish = new DrawSurface(pw, ph);

            double a = MsPer(() => DepthRigRender.DrawUnit(skiffDepth, 0, 0, dw, dh, in depthState));
            double b = MsPer(() => FishRigRender.DrawUnit(skiffFish, 0, 0, fw, fh, in fishState, noMarks));
            double c = MsPer(() => DepthRigRender.DrawUnit(pilotDepth, 0, 0, sw, sh, in depthState));
            double d = MsPer(() => FishRigRender.DrawUnit(pilotFish, 0, 0, pw, ph, in fishState, noMarks));

            string report =
                $"[HelmDashRepaintCost] S4.5 flush brow faces, raster ms/repaint, avg over {Iterations}:\n" +
                $"  skiff depth {dw}x{dh}  {a:F3} ms   (repaints on LCD-string change only)\n" +
                $"  skiff fish  {fw}x{fh}  {b:F3} ms   (repaints on the WaterfallHz bucket)\n" +
                $"  pilot depth {sw}x{sh}  {c:F3} ms\n" +
                $"  pilot fish  {pw}x{ph}  {d:F3} ms";
            TestContext.WriteLine(report);
            Debug.Log(report);

            Assert.That(a, Is.LessThan(SanityCeilingMs), "skiff depth flush face");
            Assert.That(b, Is.LessThan(SanityCeilingMs), "skiff fish flush face");
            Assert.That(c, Is.LessThan(SanityCeilingMs), "pilothouse depth flush face");
            Assert.That(d, Is.LessThan(SanityCeilingMs), "pilothouse fish flush face");
        }
    }
}
